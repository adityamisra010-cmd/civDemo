using Sim.Core;
using Sim.Core.Kernel;
using Sim.Core.State;
using Sim.Core.Systems;
using Sim.Core.Systems.Trade;
using Sim.Tests.TestUtil;

namespace Sim.Tests.Systems;

/// <summary>
/// T3.6 acceptance (D-034), the spec's five criteria with their named traps:
///   1. flow runs low→high AND ceases at a measured turn when the gap closes
///      below transport cost — both halves asserted, the gap measured on both
///      sides of the cessation;
///   2. conservation exact world-wide (the FsCheck sequences live in
///      TradeConservationTests) AND the drawdown pinned where flow is
///      expected — moving nothing conserves perfectly (Crafting precedent);
///   3. unreachable pairs move exactly zero, both directions, forever —
///      non-vacuously (a reachable pair trades in the same world);
///   4. the oscillation detector is proven in OscillationDetectorTests,
///      committed BEFORE this file (the directed order);
///   5. transport cost measurably shapes what travels — bulk goods stay
///      local, dense goods travel — with the bulk table fixed in the spec
///      BEFORE this measurement (docs/t3.6-spec.md, committed first).
/// Plus the decision (c) pins: same-factor outflow scaling and the ±1-unit
/// remainder in pair order (the documented integer tie-break, tie-dense).
/// </summary>
public class TradeArbitrageTests
{
    private static EraTable FlatEra(double dtYears) => EraTableLoader.Load(
        $$"""{ "bands": [ { "name": "flat", "startYear": 0, "endYear": 100000, "dtYears": {{dtYears.ToString(System.Globalization.CultureInfo.InvariantCulture)}} } ] }""");

    /// <summary>A hand-built multi-settlement world: every settlement carries a
    /// stock row for every registry good (zeroed unless listed), distances are
    /// added BOTH directions per edge (the T2.5 layout), and a pair with no
    /// edge listed has no distance row at all — the unreachable case.</summary>
    internal static WorldState TradeWorld(
        SimConfig cfg, int settlementCount,
        (int Settlement, string Good, long Stock, long Produced)[] stocks,
        (int Settlement, string Good, double Price)[] prices,
        (int A, int B, double Cost)[] edges)
    {
        var world = new WorldState(7);
        var ledger = new Ledger(world.LedgerFlows);
        for (int s = 0; s < settlementCount; s++)
            world.Settlements.Add(new SettlementRow(new SettlementId(s), SiteCell: s, FoundedTurn: 0));

        foreach (GoodEntry g in cfg.Goods!.Goods)
        {
            for (int s = 0; s < settlementCount; s++)
            {
                long stock = 0, produced = 0;
                foreach ((int st, string name, long amount, long prod) in stocks)
                    if (st == s && name == g.Name) { stock = amount; produced = prod; }
                int idx = world.GoodStocks.Add(new GoodStockRow(
                    new SettlementId(s), new GoodId(g.Id), Conserved.Zero, 0.0, 0.0,
                    lastProducedUnits: produced,
                    lastInputDemandUnits: 0,
                    lastConsumptionDemandUnits: 0));
                if (stock > 0)
                {
                    ledger.Flow(ref world.GoodStocks.Ref(idx).Amount,
                        ConservedQuantityIds.OfGood(new GoodId(g.Id)), ReasonIds.InitialEndowment,
                        stock, FlowDirection.Source, OverdrawPolicy.Throw);
                }
                foreach ((int st, string name, double p) in prices)
                    if (st == s && name == g.Name)
                        world.Prices.Add(new PriceRow(new SettlementId(s), new GoodId(g.Id), p));
            }
        }

        foreach ((int a, int b, double cost) in edges)
        {
            world.SettlementDistances.Add(new SettlementDistanceRow(
                new SettlementId(a), new SettlementId(b), cost));
            world.SettlementDistances.Add(new SettlementDistanceRow(
                new SettlementId(b), new SettlementId(a), cost));
        }
        return world;
    }

    private static WorldState StepTrade(SimConfig cfg, WorldState w, double dt = 10.0) =>
        new TurnExecutor(FlatEra(dt), [SystemCatalog.TradeArbitrage(cfg)]).Step(w);

    internal static long Stock(WorldState w, SimConfig cfg, int settlement, string good)
    {
        int id = cfg.Goods!.IdOf(good);
        for (int i = 0; i < w.GoodStocks.Count; i++)
            if (w.GoodStocks[i].Settlement.Value == settlement && w.GoodStocks[i].Good.Value == id)
                return w.GoodStocks[i].Amount.Value;
        return 0;
    }

    private static long Flowed(WorldState w, SimConfig cfg, int from, int to, string good)
    {
        int id = cfg.Goods!.IdOf(good);
        long total = 0;
        for (int i = 0; i < w.TradeFlows.Count; i++)
            if (w.TradeFlows[i].From.Value == from && w.TradeFlows[i].To.Value == to
                && w.TradeFlows[i].Good.Value == id)
                total += w.TradeFlows[i].Quantity;
        return total;
    }

    private static double PriceOf(WorldState w, SimConfig cfg, int settlement, string good)
    {
        int id = cfg.Goods!.IdOf(good);
        for (int i = 0; i < w.Prices.Count; i++)
            if (w.Prices[i].Settlement.Value == settlement && w.Prices[i].Good.Value == id)
                return w.Prices[i].Price;
        return 1.0;
    }

    // --- 1a. flow runs low → high, and the DRAWDOWN is pinned ----------------

    [Fact]
    public void Flow_RunsLowToHigh_AndTheQuantityIsThePinnedDrawdown()
    {
        SimConfig cfg = TestConfigs.Sim();
        // cloth: bulk 1.0; edge cost 1.0 → threshold 1.0 × 1.0 × 0.16 = 0.16,
        // gap 2.0 clears it. Cloth stock 10,000 at the cheap side.
        // The buyer holds a small cloth stock: a market with NO supply at all
        // has scale = the floor, its price hypersensitive, and the gap-closing
        // quantity collapses below one unit — physically right (a place with
        // no cloth market absorbs almost nothing per price signal), but not
        // the case under test here.
        WorldState w = TradeWorld(cfg, 2,
            stocks: [(0, "cloth", 10_000, 0), (1, "cloth", 1_000, 0)],
            prices: [(0, "cloth", 1.0), (1, "cloth", 3.0)],
            edges: [(0, 1, 1.0)]);
        WorldState next = StepTrade(cfg, w);

        long moved = Flowed(next, cfg, 0, 1, "cloth");
        Assert.True(moved > 0, "a 2.0 gap over a 0.16 threshold must trade");
        Assert.Equal(0, Flowed(next, cfg, 1, 0, "cloth")); // never high → low
        // The DRAWDOWN, not just the balance (the timber-stranding trap):
        // seller lost exactly `moved`, buyer gained exactly `moved`.
        Assert.Equal(10_000 - moved, Stock(next, cfg, 0, "cloth"));
        Assert.Equal(1_000 + moved, Stock(next, cfg, 1, "cloth"));
        // And the realised flow is the f-capped gap-closing quantity, computed
        // independently here from the same Prev fields (glass-box pin):
        // scale = max(0 + 0.5·stock·dt, 0.1·dt); sensitivity = λ·dt·(p_l/s_l + p_h/s_h).
        double sLow = Math.Max(0.5 * 10_000 * 10.0, 0.1 * 10.0);
        double sHigh = Math.Max(0.5 * 1_000 * 10.0, 0.1 * 10.0);
        double sens = cfg.Price.Lambda * 10.0 * (1.0 / sLow + 3.0 / sHigh);
        long expected = (long)(cfg.Trade.GapClosingFraction * (2.0 - 0.16) / sens);
        Assert.Equal(expected, moved);
    }

    [Fact]
    public void Direction_FollowsThePrices_NotTheRowOrder()
    {
        SimConfig cfg = TestConfigs.Sim();
        // Same world, prices swapped: the flow must reverse.
        WorldState w = TradeWorld(cfg, 2,
            stocks: [(1, "cloth", 10_000, 0), (0, "cloth", 1_000, 0)],
            prices: [(0, "cloth", 3.0), (1, "cloth", 1.0)],
            edges: [(0, 1, 1.0)]);
        WorldState next = StepTrade(cfg, w);
        Assert.True(Flowed(next, cfg, 1, 0, "cloth") > 0);
        Assert.Equal(0, Flowed(next, cfg, 0, 1, "cloth"));
    }

    // --- 1b. flow CEASES at a measured turn, gap on both sides --------------

    [Fact]
    public void Flow_Ceases_TheTurnTheGapClosesBelowTransportCost()
    {
        SimConfig cfg = TestConfigs.Sim();
        // Price + trade together: with zero demand and zero production, both
        // cloth prices decay multiplicatively (excess = −stockRelease), the
        // gap shrinks turn by turn and crosses the deadband. Edge cost 5.0 →
        // threshold 0.8, so cessation happens well off the band floor
        // (p_low ≈ 0.34 at crossing — the T3.4 band-edge vacuity trap).
        double threshold = 1.0 * 5.0 * cfg.Trade.CostPerBulkCostUnit;
        WorldState w = TradeWorld(cfg, 2,
            stocks: [(0, "cloth", 100_000, 0), (1, "cloth", 20_000, 0)],
            prices: [(0, "cloth", 1.0), (1, "cloth", 3.0)],
            edges: [(0, 1, 5.0)]);
        var executor = new TurnExecutor(FlatEra(10.0),
            [SystemCatalog.Price(cfg), SystemCatalog.TradeArbitrage(cfg)]);

        int cessationTurn = -1;
        double gapBefore = double.NaN, gapAtCessation = double.NaN;
        bool everFlowed = false;
        for (int turn = 0; turn < 30; turn++)
        {
            // The gap trade will read this turn (it reads PREV = current w).
            double gap = Math.Abs(PriceOf(w, cfg, 1, "cloth") - PriceOf(w, cfg, 0, "cloth"));
            w = executor.Step(w);
            long moved = Flowed(w, cfg, 0, 1, "cloth");
            if (moved > 0) { everFlowed = true; gapBefore = gap; }
            else if (everFlowed) { cessationTurn = turn; gapAtCessation = gap; break; }
        }

        Assert.True(everFlowed, "the rig must actually trade before it ceases");
        Assert.True(cessationTurn > 0, "flow must cease within the horizon");
        // Both halves of the criterion, measured on both sides of the turn:
        Assert.True(gapBefore > threshold,
            $"last trading turn's gap {gapBefore} must exceed the threshold {threshold}");
        Assert.True(gapAtCessation <= threshold,
            $"cessation turn's gap {gapAtCessation} must be at or below the threshold {threshold}");
        // Vacuity guard: cessation happened by gap-closing, not band pinning.
        Assert.True(PriceOf(w, cfg, 0, "cloth") > cfg.Price.BandMin * 1.001,
            "VACUOUS: the low price is resting on the band floor");
    }

    // --- 2. drawdown where flow is expected is pinned above; the world-wide
    //        conservation sweep over generated sequences is TradeConservationTests.

    // --- 3. unreachable pairs: exactly zero, both directions, forever --------

    [Fact]
    public void UnreachablePair_MovesExactlyZero_BothDirections_WhileAReachablePairTrades()
    {
        SimConfig cfg = TestConfigs.Sim();
        // S2 has no distance rows at all (an island); S0–S1 are connected.
        // S2's cloth price is maximally attractive — if reachability were not
        // enforced, S2 would be the FIRST destination.
        WorldState w = TradeWorld(cfg, 3,
            stocks: [(0, "cloth", 50_000, 0), (1, "cloth", 5_000, 0)],
            prices: [(0, "cloth", 1.0), (1, "cloth", 2.0), (2, "cloth", 19.0)],
            edges: [(0, 1, 1.0)]);
        var executor = new TurnExecutor(FlatEra(10.0), [SystemCatalog.TradeArbitrage(cfg)]);
        for (int turn = 0; turn < 10; turn++)
        {
            w = executor.Step(w);
            for (int i = 0; i < w.TradeFlows.Count; i++)
            {
                Assert.NotEqual(2, w.TradeFlows[i].From.Value);
                Assert.NotEqual(2, w.TradeFlows[i].To.Value);
            }
            Assert.Equal(0, Stock(w, cfg, 2, "cloth"));
        }
        // Non-vacuous: the reachable pair traded in this same world.
        Assert.True(Stock(w, cfg, 1, "cloth") > 5_000, "the reachable pair must have traded");
    }

    // --- 5. transport cost shapes WHAT travels -------------------------------

    [Fact]
    public void TransportCost_BulkGoodsStayLocal_DenseGoodsTravel_SameGapSameDistance()
    {
        SimConfig cfg = TestConfigs.Sim();
        // IDENTICAL 1.0 gap, IDENTICAL edge cost 2.0. cloth (bulk 1.0):
        // threshold 0.32 < 1.0 → travels. stone (bulk 10.0): threshold 3.2 >
        // 1.0 → stays, however large the stock. The expectation is NOT derived
        // from the constants under test (the circularity trap): it is the
        // reference-class claim itself — quarried stone did not move overland,
        // cloth did — with the bulk table fixed in the spec before this ran.
        WorldState w = TradeWorld(cfg, 2,
            stocks: [(0, "cloth", 10_000, 0), (0, "stone", 10_000, 0),
                     (1, "cloth", 5_000, 0), (1, "stone", 5_000, 0)],
            prices: [(0, "cloth", 1.0), (1, "cloth", 2.0),
                     (0, "stone", 1.0), (1, "stone", 2.0)],
            edges: [(0, 1, 2.0)]);
        WorldState next = StepTrade(cfg, w);
        Assert.True(Flowed(next, cfg, 0, 1, "cloth") > 0, "the dense good must travel");
        Assert.Equal(0, Flowed(next, cfg, 0, 1, "stone"));
        Assert.Equal(10_000, Stock(next, cfg, 0, "stone"));
    }

    [Fact]
    public void Deadband_IsStrict_AGapExactlyAtThresholdDoesNotTrade()
    {
        SimConfig cfg = TestConfigs.Sim();
        // gap 0.32 == threshold 1.0 × 2.0 × 0.16 exactly: a true dead zone.
        WorldState w = TradeWorld(cfg, 2,
            stocks: [(0, "cloth", 10_000, 0)],
            prices: [(0, "cloth", 1.0), (1, "cloth", 1.32)],
            edges: [(0, 1, 2.0)]);
        WorldState next = StepTrade(cfg, w);
        Assert.Equal(0, next.TradeFlows.Count);
        Assert.Equal(10_000, Stock(next, cfg, 0, "cloth"));
    }

    // --- grain: the numeraire never trades (stated in the system header) -----

    [Fact]
    public void Grain_NeverTrades_ItsPinnedPriceHasNoGap_NonVacuous()
    {
        SimConfig cfg = TestConfigs.Sim();
        // A huge grain imbalance and even a (fabricated) price gap row: the
        // pinned numeraire is excluded structurally. Cloth trades in the same
        // world, so the zero is not a dead mechanism.
        WorldState w = TradeWorld(cfg, 2,
            stocks: [(0, "grain", 1_000_000, 0), (0, "cloth", 10_000, 0),
                     (1, "grain", 1_000, 0), (1, "cloth", 1_000, 0)],
            prices: [(0, "grain", 1.0), (1, "grain", 3.0),
                     (0, "cloth", 1.0), (1, "cloth", 3.0)],
            edges: [(0, 1, 1.0)]);
        WorldState next = StepTrade(cfg, w);
        Assert.Equal(0, Flowed(next, cfg, 0, 1, "grain"));
        Assert.Equal(1_000_000, Stock(next, cfg, 0, "grain"));
        Assert.True(Flowed(next, cfg, 0, 1, "cloth") > 0);
    }

    // --- decision (c): same-factor scaling + remainder in pair order ---------

    [Fact]
    public void OverdrawnSeller_ScalesAllOutflowsBySameFactor_RemainderInPairOrder_TieDense()
    {
        SimConfig cfg = TestConfigs.Sim();
        // One seller, TWO identical buyers (same price, same stocks, same
        // distance) — the tie-dense case: desired flows are EQUAL, the seller
        // has 11 units, and the split must be exactly 6/5 with the odd unit
        // going to the EARLIER pair, (0,1) before (0,2) — the documented
        // integer tie-break. Any double-keyed or order-sensitive allocation
        // breaks this exact pin.
        WorldState w = TradeWorld(cfg, 3,
            stocks: [(0, "cloth", 11, 0), (1, "cloth", 10_000, 0), (2, "cloth", 10_000, 0)],
            prices: [(0, "cloth", 1.0), (1, "cloth", 19.0), (2, "cloth", 19.0)],
            edges: [(0, 1, 1.0), (0, 2, 1.0)]);
        WorldState next = StepTrade(cfg, w);

        long toS1 = Flowed(next, cfg, 0, 1, "cloth");
        long toS2 = Flowed(next, cfg, 0, 2, "cloth");
        Assert.Equal(11, toS1 + toS2); // everything available moves — same factor, nothing lost
        Assert.Equal(6, toS1);         // the +1 remainder follows pair order
        Assert.Equal(5, toS2);
        Assert.Equal(0, Stock(next, cfg, 0, "cloth"));
    }
}
