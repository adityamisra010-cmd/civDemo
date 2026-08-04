using Sim.Core;
using Sim.Core.Kernel;
using Sim.Core.State;
using Sim.Core.Systems;
using Sim.Core.Worldgen;
using Sim.Ui.ViewModel;
using Xunit;

namespace Sim.Ui.Tests;

/// <summary>
/// T3.9b item 2: the trade panel, headless. The packet's own §7.5 warning
/// governs this file: T3.6 measured ZERO flow on the canonical world, so a
/// display test whose only subject is that world asserts on a quantity resting
/// against its own limit and proves nothing about correctness.
///
/// So the two obligations are asserted by DIFFERENT tests and neither stands
/// in for the other:
///   (a) LEGIBLE WHEN NOTHING MOVES — the founded world, where the panel must
///       render "no trade" as a counted, reasoned state rather than emptiness.
///   (b) CORRECT WHEN SOMETHING MOVES — a CONSTRUCTED rig whose gap provably
///       clears its deadband, where the panel's content is asserted against
///       the flows the sim actually produced.
/// </summary>
public class TradePanelTests
{
    private static SimConfig SimCfg() => MarketPanelModelTests.SimCfg();

    // ---- (b) THE RIG --------------------------------------------------------
    //
    // CONSTRUCTION, stated (the T3.4c land-cap / T3.4d near-far precedent —
    // build the case, do not hope a sampled world contains it):
    //   two settlements, one distance row at travel cost 1.0, and CLOTH
    //   (bulkPerUnit 1.0 — the lowest non-numeraire bulk in the registry).
    //   deadband = bulk x pathCost x costPerBulkCostUnit = 1.0 x 1.0 x 0.16
    //            = 0.16
    //   gap      = |2.00 - 0.50| = 1.50
    // so the gap clears the deadband by ~9.4x.
    //
    // §7.16 — THE ASYMMETRIC MARGIN, NAMED AT THE MOMENT THE NUMBERS ARE
    // CHOSEN, AND THEN MEASURED. The fat side is the one just computed (gap vs
    // deadband, 9.4x): it is not where this rig can quietly die. The THIN side
    // is the FLOW QUANTITY: units = f x (gap - deadband) / sensitivity, floored
    // to whole units, so a rig with a real gap can still produce ZERO rows and
    // look exactly like the zero-flow world it is meant to contrast with.
    //
    // THAT THIN SIDE FIRED ON THE FIRST RUN, and the cause is worth recording
    // because it is counter-intuitive: sensitivity is
    //   lambda x dt x (priceLow/scaleLow + priceHigh/scaleHigh)
    // and MarketScale floors at marketScaleFloorPerYear x dt = 1.0 for a
    // market with no stock and no production. The first rig gave the BUYER
    // nothing (it is the settlement that wants cloth), so scaleHigh = 1.0,
    // sensitivity = 0.4 x (0.5/1e6 + 2.0/1.0) = 0.8, and
    // units = (long)(0.25 x 1.34 / 0.8) = (long)0.418 = 0 — no flow, from a
    // rig whose gap cleared its deadband 9.4x. The binding quantity is the
    // BUYER'S MARKET DEPTH, not the seller's stock.
    // So both markets carry stock: the buyer 100,000 (depth) and the seller
    // 2,000,000 (so the sweep-2 stock cap does not bind and the flow is a
    // modest fraction of the seller's holdings rather than its entire store).
    // The test asserts a quantity comfortably above 1 rather than merely
    // nonzero — "> 0" would sit one rounding step from vacuous — and prints
    // the measured numbers so the record transcribes those, not these.
    private static WorldState TradeRig(SimConfig cfg, out int clothId)
    {
        clothId = cfg.Goods!.IdOf("cloth");
        Assert.True(clothId > 0, "cloth missing from the goods registry — the rig names a real good");

        var world = new WorldState(11);
        var ledger = new Ledger(world.LedgerFlows);
        var cloth = new GoodId(clothId);

        for (int s = 0; s < 2; s++)
        {
            var id = new SettlementId(s);
            world.Settlements.Add(new SettlementRow(id, SiteCell: s, FoundedTurn: 0));
            int bucket = world.Buckets.Add(new BucketRow(
                id, new CultureId(1), new ReligionId(1), new ClassId(1),
                cohortIdx: 5, Conserved.Zero, 0.0, 0.0, 0.0, 0.0));
            ledger.Flow(ref world.Buckets.Ref(bucket).Count, ConservedQuantityIds.Population,
                ReasonIds.InitialEndowment, 1000, FlowDirection.Source, OverdrawPolicy.Throw);
        }

        // The seller (settlement 0, the LOW price) holds the stock; goods flow
        // low-price -> high-price.
        int sellerRow = world.GoodStocks.Add(new GoodStockRow(
            new SettlementId(0), cloth, Conserved.Zero, 0.0, 0.0));
        ledger.Flow(ref world.GoodStocks.Ref(sellerRow).Amount,
            ConservedQuantityIds.OfGood(cloth), ReasonIds.InitialEndowment,
            2_000_000, FlowDirection.Source, OverdrawPolicy.Throw);
        // The BUYER carries stock too — see the market-depth note above: a
        // buyer with none floors its market scale and kills the flow the rig
        // exists to produce.
        int buyerRow = world.GoodStocks.Add(new GoodStockRow(
            new SettlementId(1), cloth, Conserved.Zero, 0.0, 0.0));
        ledger.Flow(ref world.GoodStocks.Ref(buyerRow).Amount,
            ConservedQuantityIds.OfGood(cloth), ReasonIds.InitialEndowment,
            100_000, FlowDirection.Source, OverdrawPolicy.Throw);

        world.Prices.Add(new PriceRow(new SettlementId(0), cloth, 0.50));
        world.Prices.Add(new PriceRow(new SettlementId(1), cloth, 2.00));

        // Reachability: without a distance row PairCost is +infinity and the
        // pair is skipped forever — the rig would go silently vacuous.
        world.SettlementDistances.Add(
            new SettlementDistanceRow(new SettlementId(0), new SettlementId(1), TravelCost: 1.0));
        return world;
    }

    private static EraTable FlatEra(double dtYears) => EraTableLoader.Load(
        $$"""{ "bands": [ { "name": "flat", "startYear": 0, "endYear": 100000, "dtYears": {{dtYears.ToString(System.Globalization.CultureInfo.InvariantCulture)}} } ] }""");

    [Fact]
    public void Rig_GapClearsTheDeadband_PanelReportsTheFlowsTheSimProduced()
    {
        SimConfig cfg = SimCfg();
        WorldState world = TradeRig(cfg, out int clothId);

        // The rig's own premise, asserted before its conclusion: the margin is
        // real and computed from the SHIPPED constants, not from literals.
        GoodEntry clothEntry = cfg.Goods!.ById(clothId);
        double deadband = clothEntry.BulkPerUnit * 1.0 * cfg.Trade.CostPerBulkCostUnit;
        const double gap = 2.00 - 0.50;
        Assert.True(gap > deadband * 4.0,
            $"rig premise broken: gap {gap} is not comfortably above deadband {deadband}");

        var exec = new TurnExecutor(FlatEra(10.0), [SystemCatalog.TradeArbitrage(cfg)]);
        WorldState next = exec.Step(world);

        // The sim moved something — and by a margin, per the §7.16 note above.
        long moved = 0;
        for (int i = 0; i < next.TradeFlows.Count; i++)
            if (next.TradeFlows[i].Good.Value == clothId) moved += next.TradeFlows[i].Quantity;
        Assert.True(next.TradeFlows.Count > 0, "the rig produced no flow — vacuous, not passing");
        Assert.True(moved > 1,
            $"flow quantity {moved} is at the rounding floor — the thin side named in the rig's own comment");
        System.Console.WriteLine(
            $"trade rig: deadband {deadband:F4}, gap {gap:F4}, rows {next.TradeFlows.Count}, moved {moved}");

        // THE PANEL, asserted against what the sim actually produced.
        IReadOnlyList<TradeGoodRow> rows = TradeModel.Rows(next, cfg.Goods!);
        TradeGoodRow clothRow = rows.Single(r => r.GoodId == clothId);
        Assert.Equal(TradeState.Flowed, clothRow.State);
        Assert.Equal(moved, clothRow.TotalQuantity);
        Assert.Contains("moved", clothRow.Line);

        IReadOnlyList<TradeFlowLine> flows = TradeModel.Flows(next, cfg.Goods!);
        Assert.Equal(next.TradeFlows.Count, flows.Count);
        Assert.Equal(0, flows[0].From);          // low price sells
        Assert.Equal(1, flows[0].To);            // high price buys
        Assert.Equal("cloth", flows[0].Good);
        Assert.Equal(next.TradeFlows[0].Quantity, flows[0].Quantity);

        Assert.Contains("moved", TradeModel.SummaryLine(rows));
        Assert.DoesNotContain("no trade", TradeModel.SummaryLine(rows));
    }

    // ---- (a) LEGIBLE WHEN NOTHING MOVES -------------------------------------

    [Fact]
    public void ZeroFlowWorld_PanelSaysSoLegibly_WithAReasonPerGood()
    {
        // The SAME rig with its gap removed: both markets price cloth
        // identically, so no pair has anything to arbitrage. This is the
        // canonical world's measured condition (T3.6 escalation 2: goods
        // pinned on COMMON band edges) reproduced deterministically, without
        // depending on the canonical world continuing to have zero trade —
        // that separate fact is R2's subject, asserted below.
        SimConfig cfg = SimCfg();
        WorldState world = TradeRig(cfg, out int clothId);
        for (int i = 0; i < world.Prices.Count; i++)
            if (world.Prices[i].Good.Value == clothId)
                world.Prices[i] = world.Prices[i] with { Price = 0.50 };

        var exec = new TurnExecutor(FlatEra(10.0), [SystemCatalog.TradeArbitrage(cfg)]);
        WorldState next = exec.Step(world);
        Assert.Equal(0, next.TradeFlows.Count);

        IReadOnlyList<TradeGoodRow> rows = TradeModel.Rows(next, cfg.Goods!);
        TradeGoodRow clothRow = rows.Single(r => r.GoodId == clothId);

        // The panel does not render emptiness: it names the state and the
        // shared price behind it.
        Assert.Equal(TradeState.GapZero, clothRow.State);
        Assert.Contains("no spread", clothRow.Line);
        Assert.Contains("0.5000", clothRow.Line);

        string summary = TradeModel.SummaryLine(rows);
        Assert.Contains("no trade this turn", summary);
        Assert.Contains("no spread", summary);
        Assert.Empty(TradeModel.Flows(next, cfg.Goods!));
    }

    [Fact]
    public void GapUnderTheDeadband_ReadsAsTheDeadband_NotAsNoSpread()
    {
        // The DISCRIMINATING case for the panel's diagnosis: a real spread
        // that does not clear the deadband must read differently from no
        // spread at all. Without this test the two reasons could be a single
        // undifferentiated "nothing moved" and the panel would look correct.
        // Gap 0.10 against the rig's 0.16 deadband: below, and deliberately
        // close to it — the reason a reader needs is precisely "there IS a
        // spread and it is not enough".
        SimConfig cfg = SimCfg();
        WorldState world = TradeRig(cfg, out int clothId);
        for (int i = 0; i < world.Prices.Count; i++)
        {
            if (world.Prices[i].Good.Value != clothId) continue;
            world.Prices[i] = world.Prices[i] with
            {
                Price = world.Prices[i].Settlement.Value == 0 ? 0.50 : 0.60,
            };
        }

        var exec = new TurnExecutor(FlatEra(10.0), [SystemCatalog.TradeArbitrage(cfg)]);
        WorldState next = exec.Step(world);
        Assert.Equal(0, next.TradeFlows.Count);

        TradeGoodRow clothRow = TradeModel.Rows(next, cfg.Goods!).Single(r => r.GoodId == clothId);
        Assert.Equal(TradeState.GapNoFlow, clothRow.State);
        // The model's own subtraction, not a transcribed literal: hi - lo in
        // doubles, which is 0.0999999999999999778 rather than a clean 0.1.
        Assert.Equal(0.60 - 0.50, clothRow.Gap);
        Assert.Contains("under the deadband", clothRow.Line);
        Assert.Contains("under the deadband", TradeModel.SummaryLine(TradeModel.Rows(next, cfg.Goods!)));
    }

    [Fact]
    public void Numeraire_IsLabelled_NeverCountedAsASilentGood()
    {
        // Grain cannot trade by construction (D-034 pins its price at 1.0
        // everywhere). Counting it among the "no spread" goods would be a
        // fabricated defect in the panel's own summary.
        SimConfig cfg = SimCfg();
        WorldState world = TradeRig(cfg, out _);
        var exec = new TurnExecutor(FlatEra(10.0), [SystemCatalog.TradeArbitrage(cfg)]);
        WorldState next = exec.Step(world);

        IReadOnlyList<TradeGoodRow> rows = TradeModel.Rows(next, cfg.Goods!);
        TradeGoodRow grain = rows.Single(r => r.Name == "grain");
        Assert.Equal(TradeState.Numeraire, grain.State);
        Assert.Contains("numeraire", grain.Line);
        Assert.Contains("D-034", grain.Line);
    }

    // ---- R2: the canonical world's own reading ------------------------------

    [Fact]
    public void R2_FoundedWorld_ShowsZeroFlow_TheMeasuredExpectation()
    {
        // R2, pre-committed in docs/t3.9b-spec.md: the founded world shows
        // ZERO flow, and that is the measured expectation rather than a
        // failure. NONZERO would mean trade changed since T3.6 — a finding
        // larger than this packet, and this test is where it would surface.
        SimConfig cfg = SimCfg();
        WorldState world = MarketPanelModelTests.SteppedWorld(cfg, 12);

        IReadOnlyList<TradeGoodRow> rows = TradeModel.Rows(world, cfg.Goods!);
        string summary = TradeModel.SummaryLine(rows);
        System.Console.WriteLine($"R2 founded-world trade panel: {summary}");

        Assert.Equal(0, world.TradeFlows.Count);
        Assert.Contains("no trade this turn", summary);
        Assert.DoesNotContain(rows, r => r.State == TradeState.Flowed);
        // Not an empty panel: every non-numeraire good carries a reason.
        Assert.All(rows, r => Assert.NotEqual(TradeState.Flowed, r.State));
        Assert.Contains(rows, r => r.State is TradeState.GapZero or TradeState.GapNoFlow);
    }
}
