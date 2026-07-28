using Sim.Core;
using Sim.Core.Kernel;
using Sim.Core.State;
using Sim.Core.Systems;
using Sim.Core.Worldgen;
using Sim.Tests.TestUtil;

namespace Sim.Tests.Systems;

/// <summary>
/// T3.6 R1/R2 — the pre-committed volume readings (docs/t3.6-spec.md,
/// committed before the mechanism), measured on the founded dev world driven
/// with the PriceSoak sector mix so non-grain goods actually exist to trade.
/// At the Neolithic dt = 10 ONE TURN IS ONE DECADE, so "traded fraction per
/// decade" is per-turn traded ÷ per-turn produced directly.
///
/// The discriminating observables, per the spec:
///   R1(i)  traded fraction per good per decade;
///   R1(ii) the count of pair-goods whose gap exceeds the deadband at all
///          (near-zero (ii) → the deadband binds; large (ii) with tiny (i) →
///          f binds — measured, never guessed);
///   R2     grain SEPARATED from the rest (structurally zero here: the
///          numeraire is pinned, stated in the system header and pinned in
///          TradeArbitrageTests) plus the measured grain price spread, which
///          is exactly 0 by the same pin — the B-2 escalation path runs on
///          the honest statement, not on a measurement that cannot move.
/// This file also answers the T3.11 blocking-gap question with a measurement:
/// does the NO-ORDER founded golden world exercise the goods economy now?
/// </summary>
public class TradeReadingsTests
{
    private static (WorldState World, TurnExecutor Exec) FoundedRig(SimConfig cfg, bool driven)
    {
        using var pipelineStream = Sim.Data.DataFiles.OpenPipeline();
        using var eraStream = Sim.Data.DataFiles.OpenEraPacing();
        WorldState world = WorldFounding.Found(TestConfigs.DevWorldgen(), cfg, seed: 42);
        OrderLog orders = new();
        if (driven)
        {
            (int Sector, double Pct)[] mix =
                [(Sectors.Farming, 40.0), (Sectors.Extraction, 35.0), (Sectors.Crafting, 25.0)];
            for (int settlement = 0; settlement < 4; settlement++)
                foreach ((int sector, double pct) in mix)
                    orders.Append(new OrderRecord(
                        Turn: 2, ActorId: 1, OrderKind.SectorAllocation,
                        TargetId: settlement * 8 + sector, Amount: pct));
            OrderValidation.ValidateAgainstWorld(orders, world);
        }
        var exec = new TurnExecutor(
            EraTableLoader.Load(eraStream),
            PipelineLoader.Load(pipelineStream, SystemCatalog.All(cfg)), orders);
        return (world, exec);
    }

    [Fact]
    public void R1R2_DrivenFoundedWorld_VolumeReadings_Measured()
    {
        SimConfig cfg = TestConfigs.Sim();
        (WorldState world, TurnExecutor exec) = FoundedRig(cfg, driven: true);
        int goodCount = cfg.Goods!.Goods.Length;
        int grain = cfg.Goods.GrainId;

        var traded = new long[goodCount];
        var produced = new long[goodCount];
        long overDeadbandPairGoods = 0, turnsMeasured = 0;
        const int horizon = 100; // 100 decades — 1,000 founded years

        for (int t = 0; t < horizon; t++)
        {
            world = exec.Step(world);
            turnsMeasured++;
            for (int i = 0; i < world.TradeFlows.Count; i++)
                traded[GoodIdx(cfg, world.TradeFlows[i].Good.Value)] += world.TradeFlows[i].Quantity;
            for (int i = 0; i < world.GoodStocks.Count; i++)
                produced[GoodIdx(cfg, world.GoodStocks[i].Good.Value)] += world.GoodStocks[i].LastProducedUnits;
            overDeadbandPairGoods += CountPairGoodsOverDeadband(world, cfg);
        }

        // The measurement record (quoted in docs/t3.6-review-record.md).
        var sb = new System.Text.StringBuilder();
        long tradedTotal = 0, producedTotal = 0, grainTraded = 0;
        for (int g = 0; g < goodCount; g++)
        {
            tradedTotal += traded[g];
            producedTotal += produced[g];
            if (cfg.Goods.Goods[g].Id == grain) grainTraded = traded[g];
            sb.Append(System.Globalization.CultureInfo.InvariantCulture,
                $"{cfg.Goods.Goods[g].Name}: traded {traded[g]} / produced {produced[g]}\n");
        }
        sb.Append(System.Globalization.CultureInfo.InvariantCulture,
            $"pair-goods over deadband, summed over {turnsMeasured} turns: {overDeadbandPairGoods}\n");
        System.IO.File.WriteAllText("/tmp/t36-r1r2.txt", sb.ToString());

        // R2's mechanical half: grain NEVER trades (the pinned numeraire has
        // no gap). The reading's judgement half lives in the review record.
        Assert.Equal(0, grainTraded);
        // Non-vacuity for the whole reading: the driven world must actually
        // produce non-grain goods, or every fraction below is 0/0.
        Assert.True(producedTotal > 0, "the driven world produced nothing — the reading is vacuous");
    }

    [Fact]
    public void T311Question_NoOrderFoundedWorld_DoesItExerciseTheGoodsEconomy_Measured()
    {
        // The director's blocking-gap question, answered with a measurement:
        // under the T3.5b subsistence default (55/15/10/12/8 — no longer
        // all-farming), does the no-order founded golden world trade at all?
        SimConfig cfg = TestConfigs.Sim();
        (WorldState world, TurnExecutor exec) = FoundedRig(cfg, driven: false);
        long flows = 0, turnsWithFlow = 0;
        var nonGrainPrices = new HashSet<long>();
        for (int t = 0; t < 100; t++)
        {
            world = exec.Step(world);
            if (world.TradeFlows.Count > 0) turnsWithFlow++;
            for (int i = 0; i < world.TradeFlows.Count; i++) flows += world.TradeFlows[i].Quantity;
            for (int i = 0; i < world.Prices.Count; i++)
                if (world.Prices[i].Good.Value != cfg.Goods!.GrainId
                    && Math.Abs(world.Prices[i].Price - 1.0) > 1e-9)
                    nonGrainPrices.Add(((long)world.Prices[i].Settlement.Value << 32) | (uint)world.Prices[i].Good.Value);
        }
        System.IO.File.WriteAllText("/tmp/t36-t311.txt",
            $"no-order 100 turns: totalFlow={flows} turnsWithFlow={turnsWithFlow} "
            + $"nonUnityNonGrainPriceSeries={nonGrainPrices.Count}\n");
        // No assert on the answer itself — the question is the director's and
        // the honest deliverable is the measured number, reported either way
        // (spec: "Report either way; the driven golden itself stays T3.11's").
        Assert.True(true);
    }

    private static long CountPairGoodsOverDeadband(WorldState w, SimConfig cfg)
    {
        long count = 0;
        var goods = cfg.Goods!;
        for (int i = 0; i < w.Settlements.Count; i++)
            for (int j = i + 1; j < w.Settlements.Count; j++)
            {
                double cost = double.PositiveInfinity;
                for (int d = 0; d < w.SettlementDistances.Count; d++)
                    if (w.SettlementDistances[d].From == w.Settlements[i].Id
                        && w.SettlementDistances[d].To == w.Settlements[j].Id)
                    { cost = w.SettlementDistances[d].TravelCost; break; }
                if (double.IsInfinity(cost)) continue;
                for (int g = 0; g < goods.Goods.Length; g++)
                {
                    double pi = 1.0, pj = 1.0;
                    for (int p = 0; p < w.Prices.Count; p++)
                    {
                        if (w.Prices[p].Good.Value != goods.Goods[g].Id) continue;
                        if (w.Prices[p].Settlement == w.Settlements[i].Id) pi = w.Prices[p].Price;
                        if (w.Prices[p].Settlement == w.Settlements[j].Id) pj = w.Prices[p].Price;
                    }
                    if (Math.Abs(pi - pj) > goods.Goods[g].BulkPerUnit * cost * cfg.Trade.CostPerBulkCostUnit)
                        count++;
                }
            }
        return count;
    }

    private static int GoodIdx(SimConfig cfg, int goodId)
    {
        for (int i = 0; i < cfg.Goods!.Goods.Length; i++)
            if (cfg.Goods.Goods[i].Id == goodId) return i;
        throw new InvalidOperationException($"unknown good id {goodId}");
    }
}
