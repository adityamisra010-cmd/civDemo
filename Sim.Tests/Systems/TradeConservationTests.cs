using FsCheck;
using FsCheck.Fluent;
using FsCheck.Xunit;
using Sim.Core;
using Sim.Core.Kernel;
using Sim.Core.State;
using Sim.Core.Systems;
using Sim.Tests.TestUtil;

namespace Sim.Tests.Systems;

/// <summary>
/// T3.6 acceptance criterion 2 — conservation EXACT, world-wide, over
/// GENERATED worlds (FsCheck), no epsilon (law 1). Every generated world is
/// stepped through the trade system and checked three ways after the step:
///   (a) the ConservationAuditor's Σ stocks + Σ sunk − Σ sourced = 0 per
///       quantity — trade uses only Transfer, so its flows never even appear
///       in the flow table;
///   (b) per-good world totals are BIT-identical before and after (a stricter
///       statement than (a): a transfer to the wrong good's row would pass a
///       per-quantity audit keyed by good only if the audit's keying is right,
///       so both are asserted);
///   (c) the published TradeFlowRow quantities reconcile exactly against the
///       per-settlement stock deltas, seller down and buyer up — the drawdown
///       side of the criterion, not just the balance (moving NOTHING conserves
///       perfectly; the trap the criterion names).
/// The integer-remainder allocation is attacked here by construction: scarce
/// sellers (small stocks) with several eager buyers force the same-factor
/// scaling + remainder path on many of the generated cases.
/// </summary>
public class TradeConservationTests
{
    private static EraTable FlatEra() => EraTableLoader.Load(
        """{ "bands": [ { "name": "flat", "startYear": 0, "endYear": 100000, "dtYears": 10.0 } ] }""");

    public record TradeWorldSpec(
        int SettlementCount, long[] Stocks, int[] PriceCents, byte[] EdgeCosts);

    private static Arbitrary<TradeWorldSpec> SpecArb() =>
        (from count in Gen.Choose(2, 4)
         // Three traded goods per settlement (cloth, pottery, stone) — stocks
         // deliberately include SCARCE values so the scaling+remainder path is
         // exercised, and zero so absent markets are.
         from stocks in Gen.ArrayOf(Gen.OneOf(
             Gen.Choose(0, 20).Select(i => (long)i),
             Gen.Choose(0, 5000).Select(i => (long)i),
             Gen.Constant(0L)), 12)
         // Prices in [0.05, 20.00] by integer cents — generated doubles would
         // shrink poorly and the band is what the solver guarantees anyway.
         from prices in Gen.ArrayOf(Gen.Choose(5, 2000), 12)
         // Pair edge costs 0..30; 255 = NO edge (unreachable pair).
         from edges in Gen.ArrayOf(Gen.OneOf(
             Gen.Choose(0, 30).Select(i => (byte)i),
             Gen.Constant((byte)255)), 6)
         select new TradeWorldSpec(count, stocks, prices, edges)).ToArbitrary();

    private static readonly string[] TradedGoods = ["cloth", "pottery", "stone"];

    [Property(MaxTest = 150)]
    public Property GeneratedWorlds_TradeStep_ConservesExactly_AndFlowsReconcile()
    {
        SimConfig cfg = TestConfigs.Sim();
        return Prop.ForAll(SpecArb(), spec =>
        {
            var stocks = new List<(int, string, long, long)>();
            var prices = new List<(int, string, double)>();
            int k = 0;
            for (int s = 0; s < spec.SettlementCount; s++)
            {
                for (int g = 0; g < TradedGoods.Length; g++, k++)
                {
                    stocks.Add((s, TradedGoods[g], spec.Stocks[k], 0));
                    prices.Add((s, TradedGoods[g], spec.PriceCents[k] / 100.0));
                }
            }
            var edges = new List<(int, int, double)>();
            int e = 0;
            for (int a = 0; a < spec.SettlementCount; a++)
                for (int b = a + 1; b < spec.SettlementCount; b++, e++)
                    if (spec.EdgeCosts[e] != 255)
                        edges.Add((a, b, spec.EdgeCosts[e]));

            WorldState before = TradeArbitrageTests.TradeWorld(
                cfg, spec.SettlementCount, [.. stocks], [.. prices], [.. edges]);
            long[] beforeTotals = GoodTotals(before, cfg);
            long[,] beforeBySettlement = StocksBySettlement(before, cfg, spec.SettlementCount);

            WorldState after = new TurnExecutor(FlatEra(), [SystemCatalog.TradeArbitrage(cfg)])
                .Step(before);

            // (a) the Ledger audit, exact.
            if (!ConservationAuditor.IsConserved(after, out string report))
                return false.Label(report);

            // (b) per-good world totals bit-identical.
            long[] afterTotals = GoodTotals(after, cfg);
            for (int i = 0; i < afterTotals.Length; i++)
                if (afterTotals[i] != beforeTotals[i])
                    return false.Label($"good index {i}: total {beforeTotals[i]} -> {afterTotals[i]}");

            // (c) flows reconcile against per-settlement deltas exactly.
            long[,] afterBySettlement = StocksBySettlement(after, cfg, spec.SettlementCount);
            long[,] expectedDelta = new long[spec.SettlementCount, cfg.Goods!.Goods.Length];
            for (int i = 0; i < after.TradeFlows.Count; i++)
            {
                TradeFlowRow row = after.TradeFlows[i];
                if (row.Quantity <= 0) return false.Label("published flow with quantity <= 0");
                int gi = GoodIndex(cfg, row.Good);
                expectedDelta[row.From.Value, gi] -= row.Quantity;
                expectedDelta[row.To.Value, gi] += row.Quantity;
            }
            for (int s = 0; s < spec.SettlementCount; s++)
                for (int g = 0; g < cfg.Goods.Goods.Length; g++)
                {
                    long actual = afterBySettlement[s, g] - beforeBySettlement[s, g];
                    if (actual != expectedDelta[s, g])
                        return false.Label(
                            $"settlement {s} good {cfg.Goods.Goods[g].Name}: stock delta {actual} " +
                            $"but published flows say {expectedDelta[s, g]}");
                    if (afterBySettlement[s, g] < 0)
                        return false.Label($"negative stock at settlement {s}");
                }
            return true.ToProperty();
        });
    }

    private static long[] GoodTotals(WorldState w, SimConfig cfg)
    {
        var totals = new long[cfg.Goods!.Goods.Length];
        for (int i = 0; i < w.GoodStocks.Count; i++)
            totals[GoodIndex(cfg, w.GoodStocks[i].Good)] += w.GoodStocks[i].Amount.Value;
        return totals;
    }

    private static long[,] StocksBySettlement(WorldState w, SimConfig cfg, int settlements)
    {
        var result = new long[settlements, cfg.Goods!.Goods.Length];
        for (int i = 0; i < w.GoodStocks.Count; i++)
            result[w.GoodStocks[i].Settlement.Value, GoodIndex(cfg, w.GoodStocks[i].Good)]
                += w.GoodStocks[i].Amount.Value;
        return result;
    }

    private static int GoodIndex(SimConfig cfg, GoodId good)
    {
        for (int i = 0; i < cfg.Goods!.Goods.Length; i++)
            if (cfg.Goods.Goods[i].Id == good.Value) return i;
        throw new InvalidOperationException($"unknown good id {good.Value}");
    }
}
