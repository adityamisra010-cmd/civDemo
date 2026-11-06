using System.Globalization;
using System.Security.Cryptography;
using System.Text;
using Proto.M7.Econ;
using Sim.Core.Kernel;
using Sim.Core.State;
using Sim.Core.Systems.Demographics;

namespace Proto.M7.Econ.Tests;

/// <summary>
/// M7 Phase C lane F: both food-allocation orderings (DIR5 §2.11, D-079 Q3 — the Director's open question), the granary
/// overflow rules and the comparison model. Every stock is a long and every conservation check is exact.
/// </summary>
public class FoodAllocationComparisonTests
{
    private static readonly FoodModelParams P = FoodModelParams.Shipped;

    private static void AssertConserved(LogisticsDecision d) =>
        Assert.Equal(d.Free, d.CivilianGranted + d.GrowthGranted + d.MilitaryGranted + d.Unallocated);

    // ------------------------------------------------------------------ the allocator switch

    [Fact]
    public void DefaultOverload_IsExactlyCivilianMilitaryGrowth_OverAGrid()
    {
        foreach ((long f, long r, long g, MoveRequest[] ms) in Grid())
        {
            LogisticsDecision a = LogisticsPriority.Resolve(f, r, g, ms);
            LogisticsDecision b = LogisticsPriority.Resolve(f, r, g, ms, AllocationOrder.CivilianMilitaryGrowth);
            Assert.Equal(a.Order, b.Order);
            Assert.Equal(AllocationOrder.CivilianMilitaryGrowth, a.Order);
            Assert.Equal((a.CivilianGranted, a.GrowthGranted, a.MilitaryGranted, a.Unallocated, a.Stage),
                (b.CivilianGranted, b.GrowthGranted, b.MilitaryGranted, b.Unallocated, b.Stage));
            Assert.Equal(a.Moves, b.Moves);
        }
    }

    /// <summary>Verification round 1 (F-V9): the test above only proves that the 4-argument overload DELEGATES (both sides run
    /// the same code). This one compares the CMG path with the PRE-lane-F allocator: the digest below was measured on the
    /// grid with fc5230c's LogisticsPriority.cs (the 4-argument Resolve, before lane F touched it) compiled on its own, with
    /// this digest code (2026-11-06). CGM must differ from it (the digest sees the order).</summary>
    [Fact]
    public void CivilianMilitaryGrowth_ReproducesThePreLaneFAllocator_OverAGrid_DigestPinnedAtFc5230c()
    {
        Assert.Equal(PreLaneFGridDigest, GridDigest(AllocationOrder.CivilianMilitaryGrowth));
        Assert.NotEqual(PreLaneFGridDigest, GridDigest(AllocationOrder.CivilianGrowthMilitary));
    }

    private const string PreLaneFGridDigest = "576 c28a6bff3d7333dcc433ff7b04fb644ddb60a418967bf94a23f562efa063752b";

    private static string GridDigest(AllocationOrder order)
    {
        CultureInfo inv = CultureInfo.InvariantCulture;
        var sb = new StringBuilder();
        int n = 0;
        foreach ((long f, long r, long g, MoveRequest[] ms) in Grid())
        {
            LogisticsDecision d = LogisticsPriority.Resolve(f, r, g, ms, order);
            long[] head = [f, r, g, d.Free, d.CivilianRequirement, d.CivilianGranted, d.CivilianShortfall, d.GrowthDemand, d.GrowthGranted,
                d.MilitaryRequested, d.MilitaryGranted, d.Unallocated, (long)d.Stage];
            for (int i = 0; i < head.Length; i++) sb.Append(head[i].ToString(inv)).Append(',');
            for (int i = 0; i < d.Moves.Length; i++)
            {
                MoveDecision m = d.Moves[i];
                long[] mv = [m.Id, m.Priority, m.Requested, m.RequestedDays, m.Granted, m.GrantedDays, (long)m.Outcome];
                sb.Append('|');
                for (int j = 0; j < mv.Length; j++) sb.Append(mv[j].ToString(inv)).Append(',');
            }
            sb.Append('\n');
            n++;
        }
        return n.ToString(inv) + " " + Convert.ToHexStringLower(SHA256.HashData(Encoding.UTF8.GetBytes(sb.ToString())));
    }

    [Fact]
    public void SameInputs_TheTwoOrdersServeTheSurplusDifferently_PinnedNumbers()
    {
        // Free 1000, R 600, G 200, one 300-unit 30-day move: surplus 400 < 500 claimed.
        MoveRequest[] move = [new MoveRequest(1, 0, 300, 30)];
        LogisticsDecision cmg = LogisticsPriority.Resolve(1_000, 600, 200, move, AllocationOrder.CivilianMilitaryGrowth);
        Assert.Equal((600L, 300L, 100L, 0L), (cmg.CivilianGranted, cmg.MilitaryGranted, cmg.GrowthGranted, cmg.Unallocated));
        Assert.Equal(ConstraintStage.GrowthReduced, cmg.Stage);
        Assert.Equal(MoveOutcome.Granted, cmg.Moves[0].Outcome);

        // CGM: growth takes its 200 first; the move gets 200: floor(200·30/300) = 20 days, ceil(20·300/30) = 200 units.
        LogisticsDecision cgm = LogisticsPriority.Resolve(1_000, 600, 200, move, AllocationOrder.CivilianGrowthMilitary);
        Assert.Equal((600L, 200L, 200L, 0L), (cgm.CivilianGranted, cgm.MilitaryGranted, cgm.GrowthGranted, cgm.Unallocated));
        Assert.Equal(ConstraintStage.MilitaryRestricted, cgm.Stage);
        Assert.Equal(new MoveDecision(1, 0, 300, 30, 200, 20, MoveOutcome.Shortened), cgm.Moves[0]);
        Assert.Equal(AllocationOrder.CivilianGrowthMilitary, cgm.Order);
        AssertConserved(cmg);
        AssertConserved(cgm);
    }

    [Fact]
    public void CivilianGrowthMilitary_StageSequence_IsMilitaryFirstThenGrowthThenCivilians()
    {
        MoveRequest[] move = [new MoveRequest(1, 0, 300, 30)];
        Assert.Equal(ConstraintStage.Unconstrained, LogisticsPriority.Resolve(1_100, 600, 200, move, AllocationOrder.CivilianGrowthMilitary).Stage);
        LogisticsDecision mil = LogisticsPriority.Resolve(1_000, 600, 200, move, AllocationOrder.CivilianGrowthMilitary);
        Assert.Equal(ConstraintStage.MilitaryRestricted, mil.Stage);
        Assert.False(mil.GrowthWasReduced);
        Assert.True(mil.MilitaryWasRestricted);
        LogisticsDecision gr = LogisticsPriority.Resolve(700, 600, 200, move, AllocationOrder.CivilianGrowthMilitary);
        Assert.Equal(ConstraintStage.GrowthReduced, gr.Stage);
        Assert.Equal((100L, 0L), (gr.GrowthGranted, gr.MilitaryGranted));
        Assert.Equal(MoveOutcome.Denied, gr.Moves[0].Outcome);
        LogisticsDecision civ = LogisticsPriority.Resolve(500, 600, 200, move, AllocationOrder.CivilianGrowthMilitary);
        Assert.Equal(ConstraintStage.CivilianShortfall, civ.Stage);
        Assert.Equal((500L, 0L, 0L), (civ.CivilianGranted, civ.GrowthGranted, civ.MilitaryGranted));
    }

    [Fact]
    public void BothOrders_ConserveExactly_FeedCiviliansFirst_AndServeTheirSecondClaimFirst_OverAGrid()
    {
        AllocationOrder[] orders = [AllocationOrder.CivilianGrowthMilitary, AllocationOrder.CivilianMilitaryGrowth];
        foreach ((long f, long r, long g, MoveRequest[] ms) in Grid())
            foreach (AllocationOrder o in orders)
            {
                LogisticsDecision d = LogisticsPriority.Resolve(f, r, g, ms, o);
                AssertConserved(d);
                Assert.Equal(Math.Min(f, r), d.CivilianGranted);
                if (d.CivilianGranted < r) Assert.Equal((0L, 0L), (d.GrowthGranted, d.MilitaryGranted));
                long surplus = Math.Max(0, f - r);
                if (o == AllocationOrder.CivilianGrowthMilitary)
                {
                    // Growth first: it gets min(surplus, G) whatever the moves ask.
                    Assert.Equal(f < r ? 0 : Math.Min(surplus, g), d.GrowthGranted);
                    if (d.MilitaryGranted > 0) Assert.Equal(g, d.GrowthGranted);
                }
                else
                {
                    // Military first: growth is served only when every move is whole.
                    if (d.GrowthGranted > 0) Assert.All(d.Moves, m => Assert.Equal(MoveOutcome.Granted, m.Outcome));
                }
                Assert.True(d.MilitaryGranted <= d.MilitaryRequested && d.GrowthGranted <= g);
            }
    }

    [Fact]
    public void TieDense_BothOrders_MovesAreServedById_WhateverTheInputOrder()
    {
        // Eight moves with equal priority and equal draws; 5.5 of them fit after growth.
        var reqs = new MoveRequest[8];
        for (int i = 0; i < reqs.Length; i++) reqs[i] = new MoveRequest(10 + i, 3, 20, 4);
        AllocationOrder[] orders = [AllocationOrder.CivilianGrowthMilitary, AllocationOrder.CivilianMilitaryGrowth];
        foreach (AllocationOrder o in orders)
        {
            LogisticsDecision baseline = LogisticsPriority.Resolve(150, 20, 20, reqs, o);
            for (int rot = 1; rot < reqs.Length; rot++)
            {
                var shuffled = new MoveRequest[reqs.Length];
                for (int i = 0; i < reqs.Length; i++) shuffled[i] = reqs[(i * 3 + rot) % reqs.Length];
                Assert.Equal(baseline.Moves, LogisticsPriority.Resolve(150, 20, 20, shuffled, o).Moves);
            }
            for (int i = 1; i < baseline.Moves.Length; i++) Assert.True(baseline.Moves[i - 1].Id < baseline.Moves[i].Id);
            // The restricted move is the lowest-id one that did not fit; every later id is denied.
            int firstNotGranted = Array.FindIndex(baseline.Moves, m => m.Outcome != MoveOutcome.Granted);
            Assert.True(firstNotGranted > 0);
            for (int i = firstNotGranted + 1; i < baseline.Moves.Length; i++) Assert.Equal(MoveOutcome.Denied, baseline.Moves[i].Outcome);
        }
    }

    [Fact]
    public void AnUnknownOrder_IsRefused() =>
        Assert.Throws<ArgumentOutOfRangeException>(() => LogisticsPriority.Resolve(10, 0, 0, [], (AllocationOrder)7));

    private static IEnumerable<(long, long, long, MoveRequest[])> Grid()
    {
        long[] frees = [0, 1, 7, 99, 100, 101, 250, 599, 600, 601, 999, 5_000];
        long[] reqs = [0, 1, 100, 600];
        long[] growths = [0, 1, 50, 400];
        MoveRequest[][] sets =
        [
            [],
            [new MoveRequest(1, 0, 100, 10)],
            [new MoveRequest(1, 0, 100, 10), new MoveRequest(2, 0, 333, 7), new MoveRequest(3, 1, 0, 2)],
        ];
        foreach (long r in reqs)
        foreach (long g in growths)
        foreach (MoveRequest[] ms in sets)
        foreach (long f in frees)
            yield return (f, r, g, ms);
    }

    // ------------------------------------------------------------------ the comparison model (synthetic baselines)

    /// <summary>The age shape of the real founded world (seed 42, turn 0, measured by FoodBaseline): 5,088 people, 2,925 of
    /// them 15–59 — a young, growing population, so a zero-growth cap binds as it does in the real worlds.</summary>
    private static readonly long[] RealShape = [736, 701, 590, 546, 547, 431, 373, 293, 268, 220, 144, 103, 63, 27, 29, 17];

    /// <summary>A young, fast-growing shape (births ≈ 311/yr against ≈ 180 deaths/yr): natural increase ≈ 2.4 %/yr, so a
    /// zero-growth headroom must bind — used where a test needs the growth cut to show in births.</summary>
    private static readonly long[] YoungShape = [600, 600, 600, 800, 800, 800, 800, 200, 100, 50, 20, 10, 5, 2, 1, 1];

    /// <summary>A synthetic baseline: the real age shape × <paramref name="scale"/>, a constant harvest, civilians eating 0.9
    /// grain per adult-equivalent-year, a constant headroom limit; labour twins that make the harvest proportional to
    /// labour.</summary>
    private static BaselineSeries Synthetic(int turns, long dtDays, long scale, long stock, long harvestPerStep, double limit,
        long soldiers = 20, long startTurn = 2, long[]? shape = null)
    {
        long[] form = shape ?? RealShape;
        double[] w = P.CohortWeights;
        var cohorts = new long[16];
        double nutr = 0.0;
        long adults = 0;
        for (int c = 0; c < 16; c++)
        {
            cohorts[c] = form[c] * scale;
            nutr += w[c] * cohorts[c];
            if (c >= 3 && c < 12) adults += cohorts[c];
        }
        double dtYears = dtDays / 360.0;
        long eaten = (long)Math.Round(0.9 * nutr * dtYears);
        var rows = new BaselineTurn[turns + 1];
        for (int t = 0; t <= turns; t++)
            rows[t] = new BaselineTurn(t, t == 0 ? 0 : dtDays, (long[])cohorts.Clone(), soldiers, 0, stock, adults, nutr, limit, false, eaten,
                t == 0 ? 0 : harvestPerStep, t == 0 ? 0 : eaten, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, "synthetic");
        long smallRemoved = Math.Min(960, adults / 2), largeRemoved = Math.Max(smallRemoved + 1, adults - 100);
        LabourTwin small = new(startTurn, smallRemoved, harvestPerStep, adults > 0 ? harvestPerStep - harvestPerStep * smallRemoved / adults : 0, dtYears);
        LabourTwin large = new(startTurn, largeRemoved, harvestPerStep, adults > 0 ? harvestPerStep - harvestPerStep * largeRemoved / adults : 0, dtYears);
        return new BaselineSeries(900, rows, [small, large], [], 0);
    }

    private static FoodScenario Scn(BaselineSeries s, string army, RegimeSpec regime, AllocationOrder order, OverflowRule overflow,
        UnfedConsequence consequence, SettlementGranularity granularity = SettlementGranularity.WarPulse, int horizon = 3, long t0 = 2) =>
        FoodComparison.Scenario(s, P, new EpochPlan(t0, horizon), army, regime, order, overflow, consequence, HaulBasis.FullLoad, granularity);

    private static readonly RegimeSpec Normal = new("NORMAL", 0, false);
    private static readonly RegimeSpec NormalEmpty = new("NORMAL+EMPTY", 0, true);
    private static readonly RegimeSpec SevereEmpty = new("SEVERE-2s+EMPTY", 2, true);
    private static readonly AllocationOrder[] Orders = [AllocationOrder.CivilianGrowthMilitary, AllocationOrder.CivilianMilitaryGrowth];

    private static void AssertLedgerCloses(FoodRunResult r)
    {
        // Exact identities on the final state, at the window's end and on every checkpoint.
        FoodLedger f = r.FinalLedger;
        Assert.Equal(r.FinalGrainStock, f.GrainImplied);
        Assert.Equal(r.FinalCivilians, f.CiviliansImplied);
        Assert.Equal(r.FinalSoldiers, f.SoldiersImplied);
        Assert.Equal(r.FinalCivilians + r.FinalSoldiers,
            f.CiviliansInitial + f.SoldiersInitial + f.Births - f.Deaths - f.Starvation - f.SoldierStarvation);
        Assert.Equal(r.WarEndStock, r.WindowEndLedger.GrainImplied);
        // The trade credit (trade rules only; zero otherwise) is never negative: imports never exceed opening + exports.
        Assert.True(f.CreditImplied >= 0 && f.Imported <= f.CreditInitial + f.Exported);
        Assert.Equal(r.Scenario.OpeningCredit, f.CreditInitial);
        if (!FoodPulseModel.IsTrade(r.Scenario.Overflow)) Assert.True(f.Exported == 0 && f.Imported == 0 && f.CreditInitial == 0);
        else Assert.Equal(0, f.Overflow);   // a trade rule sinks nothing: the overflow is exported
        foreach (FoodCheckpoint c in r.Checkpoints)
        {
            Assert.Equal(c.GrainStock, c.Ledger.GrainImplied);
            Assert.True(c.Ledger.CreditImplied >= 0);
            Assert.Equal(c.Civilians, c.Ledger.CiviliansImplied);
            Assert.Equal(c.Soldiers, c.Ledger.SoldiersImplied);
            Assert.Equal(c.Civilians + c.Soldiers,
                c.Ledger.CiviliansInitial + c.Ledger.SoldiersInitial + c.Ledger.Births - c.Ledger.Deaths - c.Ledger.Starvation - c.Ledger.SoldierStarvation);
        }
    }

    [Fact]
    public void Conservation_IsExact_ForBothOrders_EveryOverflowRule_AndEveryConsequence()
    {
        // Rich in armies and shortage: a large campaign from an emptied store in a 2σ drought, 20 turns (checkpoint +20).
        BaselineSeries s = Synthetic(25, 3600, 1, 7_000, 50_000, 8_000.0);
        OverflowRule[] rules = [OverflowRule.CapBeforeMilitary, OverflowRule.AllocateBeforeCap, OverflowRule.NoCap, OverflowRule.TradeCredit, OverflowRule.TradeCreditSinceFounding];
        UnfedConsequence[] cons = [UnfedConsequence.None, UnfedConsequence.Desert, UnfedConsequence.Starve];
        string[] armies = ["WAR-1000", "WAR-LARGE", "GARRISON-1000"];
        int checkedRuns = 0;
        foreach (string army in armies)
        foreach (OverflowRule o in rules)
        foreach (UnfedConsequence c in cons)
        foreach (AllocationOrder q in Orders)
        {
            FoodScenario sc = Scn(s, army, SevereEmpty, q, o, c, horizon: 20);
            if (o == OverflowRule.TradeCreditSinceFounding) sc = sc with { OpeningCredit = 5_000 };   // the synthetic series sinks no overflow before T0
            FoodRunResult r = FoodPulseModel.Run(s, sc, P);
            Assert.Single(r.Checkpoints);
            AssertLedgerCloses(r);
            Assert.True(r.Checkpoints[0].Ledger.Harvest > 0 && r.Checkpoints[0].Ledger.Mobilised > 0);
            checkedRuns++;
        }
        Assert.Equal(90, checkedRuns);
    }

    [Fact]
    public void Model_TheOrderDecidesWhoIsCut_GrowthFirstVersusMilitaryFirst()
    {
        // 8,000/yr harvest, ~4,240 adult-equivalents eating 0.9; a 500-man campaign (ration 500/yr + haul ~4.3k/yr) and a
        // growth claim (vacancy × 0.9) together exceed the surplus, while civilians are always covered. The store starts
        // empty so the claims compete from the first pulse.
        BaselineSeries s = Synthetic(6, 3600, 1, 7_000, 80_000, 8_000.0);
        FoodRunResult cgm = FoodPulseModel.Run(s, Scn(s, "WAR-500", NormalEmpty, AllocationOrder.CivilianGrowthMilitary, OverflowRule.CapBeforeMilitary, UnfedConsequence.None), P);
        FoodRunResult cmg = FoodPulseModel.Run(s, Scn(s, "WAR-500", NormalEmpty, AllocationOrder.CivilianMilitaryGrowth, OverflowRule.CapBeforeMilitary, UnfedConsequence.None), P);
        Assert.Equal(0, cgm.War.PeriodsCivilianShort);
        Assert.Equal(0, cmg.War.PeriodsCivilianShort);
        // CGM: the growth claim is served in full; the march is restricted.
        Assert.Equal(cgm.War.GrowthDemand, cgm.War.GrowthGranted);
        Assert.True(cgm.War.MarchDaysGranted < cgm.War.MarchDaysRequested);
        // CMG: the march gets more and growth is cut.
        Assert.True(cmg.War.GrowthGranted < cmg.War.GrowthDemand);
        Assert.True(cmg.War.MarchDaysGranted > cgm.War.MarchDaysGranted);
        Assert.True(cmg.War.HaulGranted > cgm.War.HaulGranted);
        // The growth cut is a real demographic consequence: fewer births in the window's turn under CMG.
        // (A young population, so the growth headroom binds — in the real worlds natural increase is ≈ 0.1 %/yr and the same
        // cut costs few births; the battery measures that.)
        BaselineSeries young = Synthetic(4, 3600, 1, 7_000, 100_000, 9_000.0, shape: YoungShape);
        FoodRunResult cgm1 = FoodPulseModel.Run(young, Scn(young, "WAR-500", NormalEmpty, AllocationOrder.CivilianGrowthMilitary, OverflowRule.CapBeforeMilitary, UnfedConsequence.None, horizon: 1), P);
        FoodRunResult cmg1 = FoodPulseModel.Run(young, Scn(young, "WAR-500", NormalEmpty, AllocationOrder.CivilianMilitaryGrowth, OverflowRule.CapBeforeMilitary, UnfedConsequence.None, horizon: 1), P);
        Assert.Equal(cgm1.War.GrowthDemand, cgm1.War.GrowthGranted);
        Assert.True(cmg1.War.GrowthGranted < cmg1.War.GrowthDemand);
        Assert.True(cgm1.WindowEndLedger.Births > cmg1.WindowEndLedger.Births,
            "births in window CGM " + cgm1.WindowEndLedger.Births + " CMG " + cmg1.WindowEndLedger.Births + "; pop " + cgm1.PopulationByTurn[1] + " vs " + cmg1.PopulationByTurn[1]);
        Assert.True(cgm1.PopulationByTurn[1] > cmg1.PopulationByTurn[1],
            "births in window CGM " + cgm1.WindowEndLedger.Births + " CMG " + cmg1.WindowEndLedger.Births + "; pop " + cgm1.PopulationByTurn[1] + " vs " + cmg1.PopulationByTurn[1]);
    }

    [Fact]
    public void Model_PeaceIsIdenticalUnderBothOrders_WhenNothingIsShort()
    {
        BaselineSeries s = Synthetic(6, 3600, 1, 7_000, 80_000, 8_000.0);
        FoodRunResult a = FoodPulseModel.Run(s, Scn(s, "PEACE", Normal, AllocationOrder.CivilianGrowthMilitary, OverflowRule.CapBeforeMilitary, UnfedConsequence.None), P);
        FoodRunResult b = FoodPulseModel.Run(s, Scn(s, "PEACE", Normal, AllocationOrder.CivilianMilitaryGrowth, OverflowRule.CapBeforeMilitary, UnfedConsequence.None), P);
        Assert.Equal(a.War.GrowthDemand, a.War.GrowthGranted);
        Assert.Equal(a.StateHash, b.StateHash);
    }

    [Fact]
    public void OverflowRule_TheExistingOrder_DestroysGrainTheArmyThenLacks_AtCampaignTurnGranularity()
    {
        // The shipped pipeline order at the shipped granularity: a turn's surplus is spoiled and capped (slot 6) before the
        // military draws (slot 23). Allocating before the cap feeds the same army from the same harvest.
        BaselineSeries s = Synthetic(6, 3600, 1, 0, 80_000, 8_000.0);
        foreach (AllocationOrder q in Orders)
        {
            FoodRunResult cap = FoodPulseModel.Run(s, Scn(s, "GARRISON-1000", Normal, q, OverflowRule.CapBeforeMilitary, UnfedConsequence.None, SettlementGranularity.CampaignTurn), P);
            FoodRunResult alloc = FoodPulseModel.Run(s, Scn(s, "GARRISON-1000", Normal, q, OverflowRule.AllocateBeforeCap, UnfedConsequence.None, SettlementGranularity.CampaignTurn), P);
            Assert.True(cap.Whole.RationGranted < cap.Whole.RationRequested);
            Assert.True(cap.FinalLedger.Overflow > 0);
            Assert.True(alloc.Whole.RationGranted > cap.Whole.RationGranted);
            Assert.True(cap.FinalLedger.Overflow > alloc.FinalLedger.Overflow);
            if (q == AllocationOrder.CivilianMilitaryGrowth) Assert.Equal(alloc.Whole.RationRequested, alloc.Whole.RationGranted);
        }
    }

    [Fact]
    public void Desert_ReturnsTheUnfedShareToCivilianLife_PeopleConserved()
    {
        BaselineSeries s = Synthetic(25, 3600, 1, 7_000, 50_000, 8_000.0);
        FoodRunResult none = FoodPulseModel.Run(s, Scn(s, "WAR-1000", SevereEmpty, AllocationOrder.CivilianGrowthMilitary, OverflowRule.CapBeforeMilitary, UnfedConsequence.None, horizon: 20), P);
        FoodRunResult desert = FoodPulseModel.Run(s, Scn(s, "WAR-1000", SevereEmpty, AllocationOrder.CivilianGrowthMilitary, OverflowRule.CapBeforeMilitary, UnfedConsequence.Desert, horizon: 20), P);
        FoodRunResult starve = FoodPulseModel.Run(s, Scn(s, "WAR-1000", SevereEmpty, AllocationOrder.CivilianGrowthMilitary, OverflowRule.CapBeforeMilitary, UnfedConsequence.Starve, horizon: 20), P);
        Assert.True(none.War.RationGranted < none.War.RationRequested);
        Assert.Equal(none.WarSoldiersAtStart, none.WarSoldiersAtEnd);              // the existing rule: nothing happens
        Assert.True(desert.WindowEndLedger.Deserted > 0);
        Assert.Equal(desert.WarSoldiersAtStart - desert.WindowEndLedger.Deserted, desert.WarSoldiersAtEnd);
        Assert.Equal(0, desert.WindowEndLedger.SoldierStarvation);
        Assert.True(starve.WindowEndLedger.SoldierStarvation > 0);
        Assert.Equal(0, starve.WindowEndLedger.Deserted);
        Assert.Equal(starve.WarSoldiersAtStart - starve.WindowEndLedger.SoldierStarvation, starve.WarSoldiersAtEnd);
        AssertLedgerCloses(desert);
        AssertLedgerCloses(starve);
    }

    [Fact]
    public void Starve_FollowsTheShippedStarvationLaw_NoMoreThanItsCeiling()
    {
        // A harvest of zero inside the window and an emptied store: the ration is wholly unfed, effective deficit 1, so a
        // soldier dies at starvationMortalityMaxPerYear (0.12/yr) for the window's 1,092 days — at most ⌈n(1 − e^(−0.12·1092/360))⌉.
        BaselineSeries s = Synthetic(4, 3600, 1, 7_000, 50_000, 8_000.0);
        FoodScenario sc = Scn(s, "GARRISON-1000", SevereEmpty, AllocationOrder.CivilianMilitaryGrowth, OverflowRule.CapBeforeMilitary, UnfedConsequence.Starve, horizon: 1)
            with { HarvestMultiplier = 0.0 };
        FoodRunResult r = FoodPulseModel.Run(s, sc, P);
        Assert.Equal(0, r.War.RationGranted);
        double days = r.WindowEndDayApplied - r.WindowStartDayApplied;
        double ceiling = r.WarSoldiersAtStart * (1.0 - Math.Exp(-P.StarvationMaxPerYear * days / 360.0));
        long dead = r.WindowEndLedger.SoldierStarvation;
        Assert.True(dead > 0.9 * ceiling && dead <= Math.Ceiling(ceiling), dead + " vs " + ceiling);
    }

    /// <summary>Verification round 1 (F-V2): the SIZE of desertion. A one-pulse war window (21 days) from an emptied store with
    /// the harvest scaled so the ration is PARTLY fed. The consequence acts after the allocation, so the None twin shows the
    /// period's grant; the Desert run must lose exactly ⌊soldiers × unfed share⌋ (remainder 0 at the first desertion).</summary>
    [Theory]
    [InlineData(AllocationOrder.CivilianMilitaryGrowth, 0.66)]
    [InlineData(AllocationOrder.CivilianGrowthMilitary, 1.00)]
    public void Desert_TheArmyLosesExactlyTheUnfedShare_OnePulse(AllocationOrder order, double harvestMultiplier)
    {
        BaselineSeries s = Synthetic(4, 3600, 1, 7_000, 80_000, 8_000.0);
        FoodScenario Pulse(UnfedConsequence c) => Scn(s, "WAR-1000", NormalEmpty, order, OverflowRule.CapBeforeMilitary, c, horizon: 1)
            with { WindowLengthDays = 21, HarvestMultiplier = harvestMultiplier };
        FoodRunResult none = FoodPulseModel.Run(s, Pulse(UnfedConsequence.None), P);
        FoodRunResult desert = FoodPulseModel.Run(s, Pulse(UnfedConsequence.Desert), P);
        Assert.Equal(1, none.War.Periods);
        long req = none.War.RationRequested, got = none.War.RationGranted, n = none.WarSoldiersAtStart;
        Assert.True(got > 0 && got < req, "the pulse must be partly fed: " + got + " of " + req);
        Assert.Equal((req, got, n), (desert.War.RationRequested, desert.War.RationGranted, desert.WarSoldiersAtStart));
        long expected = (long)Math.Floor(n * ((req - got) / (double)req));
        Assert.True(expected > 0 && expected < n);
        Assert.Equal(expected, desert.WindowEndLedger.Deserted);
        Assert.Equal(n - expected, desert.WarSoldiersAtEnd);
        Assert.Equal(n, none.WarSoldiersAtEnd);
        AssertLedgerCloses(desert);
    }

    /// <summary>Verification round 1 (F-V3): the Starve proposal reuses the shipped law INCLUDING the dead zone
    /// (FoodState.EffectiveDeficit: d ≤ a → 0, else (d − a)/(1 − a), a = foodState.adaptationAbsorbableShortfall). One
    /// 10-year campaign-turn period (allocation before the cap, so the turn's harvest can feed the garrison) with the harvest
    /// scaled to an unfed share BELOW a (no soldier dies, although the share without the dead zone would kill some) and ABOVE
    /// it (exactly ⌊n(1 − e^(−max·dEff·h))⌋ die). The law is restated here, not called, so the test pins it.</summary>
    [Theory]
    [InlineData(AllocationOrder.CivilianMilitaryGrowth, 0.58, true)]
    [InlineData(AllocationOrder.CivilianGrowthMilitary, 0.58, true)]
    [InlineData(AllocationOrder.CivilianMilitaryGrowth, 0.50, false)]
    [InlineData(AllocationOrder.CivilianGrowthMilitary, 0.50, false)]
    public void Starve_TheDeadZoneAbsorbsASmallUnfedShare_AndTheExcessFollowsTheShippedLaw(AllocationOrder order, double harvestMultiplier, bool insideDeadZone)
    {
        BaselineSeries s = Synthetic(4, 3600, 1, 7_000, 80_000, 8_000.0);
        FoodScenario Turn(UnfedConsequence c) => Scn(s, "GARRISON-1000", Normal, order, OverflowRule.AllocateBeforeCap, c, SettlementGranularity.CampaignTurn, horizon: 1)
            with { HarvestMultiplier = harvestMultiplier };
        FoodRunResult none = FoodPulseModel.Run(s, Turn(UnfedConsequence.None), P);
        FoodRunResult starve = FoodPulseModel.Run(s, Turn(UnfedConsequence.Starve), P);
        Assert.Equal(1, none.War.Periods);
        long req = none.War.RationRequested, got = none.War.RationGranted, n = none.WarSoldiersAtStart;
        Assert.True(got > 0 && got < req, "the turn must be partly fed: " + got + " of " + req);
        double share = (req - got) / (double)req, a = P.AbsorbableShortfall, h = 3600 / (double)SimClock.YearDays;
        Assert.Equal(insideDeadZone, share <= a);
        double dEff = share <= a ? 0.0 : (share - a) / (1.0 - a);
        long expected = (long)Math.Floor(n * (1.0 - Math.Exp(-(P.StarvationMaxPerYear * dEff) * h)));
        Assert.Equal(expected, starve.WindowEndLedger.SoldierStarvation);
        Assert.Equal(n - expected, starve.FinalSoldiers);
        long withoutDeadZone = (long)Math.Floor(n * (1.0 - Math.Exp(-(P.StarvationMaxPerYear * share) * h)));
        if (insideDeadZone)
        {
            Assert.Equal(0, expected);
            Assert.True(withoutDeadZone > 0, "the case must have teeth: " + withoutDeadZone + " would die without the dead zone");
        }
        else Assert.True(expected > 0 && expected < withoutDeadZone, expected + " vs " + withoutDeadZone);
        AssertLedgerCloses(starve);
    }

    /// <summary>Verification round 1 (F-V4): the civilian-SHORT periods (nominal deficit) can differ by order while civilian
    /// starvation is identical — the shipped dead zone (adaptationAbsorbableShortfall) absorbs the small extra shortfalls
    /// CMG's military draw leaves in later pulses. Pinned on a synthetic 20-turn WAR-500 in a 1σ bad harvest.</summary>
    [Fact]
    public void CivilianShortPeriods_DifferByOrder_WhileCivilianStarvationIsIdentical()
    {
        BaselineSeries s = Synthetic(25, 3600, 1, 7_000, 50_000, 8_000.0);
        RegimeSpec bad = new("BAD-1s", 1, false);
        FoodRunResult cgm = FoodPulseModel.Run(s, Scn(s, "WAR-500", bad, AllocationOrder.CivilianGrowthMilitary, OverflowRule.CapBeforeMilitary, UnfedConsequence.None, horizon: 20), P);
        FoodRunResult cmg = FoodPulseModel.Run(s, Scn(s, "WAR-500", bad, AllocationOrder.CivilianMilitaryGrowth, OverflowRule.CapBeforeMilitary, UnfedConsequence.None, horizon: 20), P);
        Assert.True(cmg.War.PeriodsCivilianShort > cgm.War.PeriodsCivilianShort, "short periods CMG " + cmg.War.PeriodsCivilianShort + " CGM " + cgm.War.PeriodsCivilianShort);
        Assert.Equal(cgm.FinalLedger.Starvation, cmg.FinalLedger.Starvation);
        Assert.NotEqual(cgm.StateHash, cmg.StateHash);
    }

    // ------------------------------------------------------------------ the TRADE fate of the overflow (TradeCredit, PROPOSAL)

    [Fact]
    public void TradeCredit_WhileNoClaimIsShort_IsFoodIdenticalToTheLossFlow_OnlyTheLedgerReasonDiffers()
    {
        // Peace with a surplus, per War Pulse: the store overflows every pulse and NO CLAIM is ever short, so the allocator
        // never binds and the credit is never drawn. Only then does selling the overflow change nothing but the ledger: a
        // Transfer to the partner instead of a sink. (Verification round 1, F-V1: an undrawn credit is NOT neutral once a
        // claim is short — see the next test.)
        BaselineSeries s = Synthetic(6, 3600, 1, 7_000, 80_000, 8_000.0);
        foreach (AllocationOrder q in Orders)
        {
            FoodRunResult lost = FoodPulseModel.Run(s, Scn(s, "PEACE", Normal, q, OverflowRule.CapBeforeMilitary, UnfedConsequence.None), P);
            FoodRunResult sold = FoodPulseModel.Run(s, Scn(s, "PEACE", Normal, q, OverflowRule.TradeCredit, UnfedConsequence.None), P);
            Assert.Equal((lost.Whole.GrowthDemand, lost.Whole.RationRequested, 0L), (lost.Whole.GrowthGranted, lost.Whole.RationGranted, lost.Whole.PeriodsCivilianShort));
            Assert.True(lost.FinalLedger.Overflow > 0);
            Assert.Equal(lost.FinalLedger.Overflow, sold.FinalLedger.Exported);
            Assert.Equal(0, sold.FinalLedger.Overflow);
            Assert.Equal(0, sold.FinalLedger.Imported);
            Assert.Equal(sold.FinalLedger.Exported, sold.FinalLedger.CreditImplied);
            Assert.Equal(lost.PopulationByTurn, sold.PopulationByTurn);
            Assert.Equal(lost.StockByTurn, sold.StockByTurn);
            Assert.Equal(lost.FinalCivilians, sold.FinalCivilians);
            Assert.Equal(lost.FinalLedger.Births, sold.FinalLedger.Births);
            Assert.Equal(lost.FinalLedger.Spoilage, sold.FinalLedger.Spoilage);
            Assert.Equal(lost.Whole.GrowthGranted, sold.Whole.GrowthGranted);
            Assert.NotEqual(lost.StateHash, sold.StateHash);   // the trade counters are part of the TradeCredit state
            AssertLedgerCloses(sold);
        }
    }

    /// <summary>Verification round 1 (F-V1): an UNDRAWN credit still relaxes the allocator. Per campaign turn the 10-year
    /// growth claim exceeds the capped store, so under the loss flow growth takes the store (CGM) or what the small founding
    /// ration leaves (CMG). Under TradeCredit the allocator counts store + credit: the growth-headroom grant — not a physical
    /// draw — is counted against the credit, and under CGM the physical store is released to the ration. Nothing is ever
    /// bought back (Imported = 0), yet growth and (CGM) the ration are served more: an accounting effect of the proposal,
    /// not a buy-back of sold grain.</summary>
    [Fact]
    public void TradeCredit_AnUndrawnCreditStillRelaxesTheAllocator_TheGrowthGrantIsCountedAgainstTheCredit()
    {
        BaselineSeries s = Synthetic(6, 3600, 1, 7_000, 80_000, 8_000.0);
        foreach (AllocationOrder q in Orders)
        {
            FoodRunResult lost = FoodPulseModel.Run(s, Scn(s, "PEACE", Normal, q, OverflowRule.CapBeforeMilitary, UnfedConsequence.None, SettlementGranularity.CampaignTurn, horizon: 4), P);
            FoodRunResult sold = FoodPulseModel.Run(s, Scn(s, "PEACE", Normal, q, OverflowRule.TradeCredit, UnfedConsequence.None, SettlementGranularity.CampaignTurn, horizon: 4), P);
            Assert.True(lost.Whole.GrowthGranted < lost.Whole.GrowthDemand, q + ": the loss flow must bind on growth");
            Assert.Equal(0, sold.FinalLedger.Imported);                      // the credit is never drawn ...
            Assert.True(sold.FinalLedger.Exported > 0);
            Assert.True(sold.Whole.GrowthGranted > lost.Whole.GrowthGranted,  // ... yet growth is granted against it
                q + ": growth " + sold.Whole.GrowthGranted + " vs " + lost.Whole.GrowthGranted);
            if (q == AllocationOrder.CivilianGrowthMilitary)
            {
                // CGM: the loss flow gives the store to growth and the founding ration nothing; with the credit counted, the
                // ration is drawn from the PHYSICAL store, so the store ends lower by exactly the extra ration (no import).
                Assert.True(sold.Whole.RationGranted > lost.Whole.RationGranted, "ration " + sold.Whole.RationGranted + " vs " + lost.Whole.RationGranted);
                Assert.True(sold.FinalGrainStock < lost.FinalGrainStock);
            }
            AssertLedgerCloses(sold);
        }
    }

    [Fact]
    public void TradeCredit_TheSoldOverflowFeedsTheArmyTheLossFlowStarves_AtCampaignTurnGranularity()
    {
        // The shipped order at the shipped granularity starves the garrison (the cap destroys the turn's surplus first);
        // when that surplus is sold instead, the credit buys the ration back — under both orders.
        BaselineSeries s = Synthetic(6, 3600, 1, 0, 80_000, 8_000.0);
        foreach (AllocationOrder q in Orders)
        {
            FoodRunResult lost = FoodPulseModel.Run(s, Scn(s, "GARRISON-1000", Normal, q, OverflowRule.CapBeforeMilitary, UnfedConsequence.None, SettlementGranularity.CampaignTurn), P);
            FoodRunResult sold = FoodPulseModel.Run(s, Scn(s, "GARRISON-1000", Normal, q, OverflowRule.TradeCredit, UnfedConsequence.None, SettlementGranularity.CampaignTurn), P);
            Assert.True(lost.Whole.RationGranted < lost.Whole.RationRequested);
            Assert.True(sold.Whole.RationGranted > lost.Whole.RationGranted, q + ": " + sold.Whole.RationGranted + " vs " + lost.Whole.RationGranted);
            Assert.True(sold.FinalLedger.Imported > 0);
            Assert.True(sold.FinalLedger.Imported <= sold.FinalLedger.Exported);
            Assert.Equal(0, sold.FinalLedger.Overflow);
            AssertLedgerCloses(sold);
        }
    }

    [Fact]
    public void TradeCredit_FeedsCiviliansFromTheCreditInALaterFamine_TheLossFlowDoesNot()
    {
        // Two turns of surplus (the overflow is sold), then a famine window (harvest × 0.25 for 3 years from turn 2's
        // start): the credit buys back what the store lacks for the civilians, so fewer periods are short and fewer starve.
        BaselineSeries s = Synthetic(6, 3600, 1, 7_000, 80_000, 8_000.0);
        foreach (AllocationOrder q in Orders)
        {
            FoodScenario Famine(OverflowRule o) => Scn(s, "PEACE", Normal, q, o, UnfedConsequence.None, horizon: 4)
                with { WindowStartDay = 2 * 3600, WindowLengthDays = 1080, HarvestMultiplier = 0.25 };
            FoodRunResult lost = FoodPulseModel.Run(s, Famine(OverflowRule.CapBeforeMilitary), P);
            FoodRunResult sold = FoodPulseModel.Run(s, Famine(OverflowRule.TradeCredit), P);
            Assert.True(lost.War.PeriodsCivilianShort > 0, "the loss flow leaves civilians short in the famine");
            Assert.True(sold.War.PeriodsCivilianShort < lost.War.PeriodsCivilianShort,
                q + ": short periods " + sold.War.PeriodsCivilianShort + " vs " + lost.War.PeriodsCivilianShort);
            Assert.True(sold.War.CivilianGranted > lost.War.CivilianGranted);
            Assert.True(sold.FinalLedger.Starvation < lost.FinalLedger.Starvation,
                q + ": starvation " + sold.FinalLedger.Starvation + " vs " + lost.FinalLedger.Starvation);
            Assert.True(sold.WindowEndLedger.Imported - sold.WindowStartLedger.Imported > 0);
            AssertLedgerCloses(sold);
        }
    }

    [Fact]
    public void TradeCreditSinceFounding_EnoughCredit_FeedsEveryClaim_AndTheOrderStopsMattering()
    {
        // A 2σ drought campaign from an emptied store: under the loss flow both orders leave the army short. With an
        // opening credit large enough to buy every claim, both orders serve civilians, growth, ration and march in full —
        // and reach the byte-identical state: the ordering question only exists while something is short.
        BaselineSeries s = Synthetic(6, 3600, 1, 7_000, 50_000, 8_000.0);
        FoodRunResult lost = FoodPulseModel.Run(s, Scn(s, "WAR-1000", SevereEmpty, AllocationOrder.CivilianGrowthMilitary, OverflowRule.CapBeforeMilitary, UnfedConsequence.None), P);
        Assert.True(lost.War.RationGranted < lost.War.RationRequested);
        var runs = new List<FoodRunResult>();
        foreach (AllocationOrder q in Orders)
        {
            FoodScenario sc = Scn(s, "WAR-1000", SevereEmpty, q, OverflowRule.TradeCreditSinceFounding, UnfedConsequence.None) with { OpeningCredit = 1_000_000 };
            FoodRunResult r = FoodPulseModel.Run(s, sc, P);
            Assert.Equal(r.War.RationRequested, r.War.RationGranted);
            Assert.Equal(r.War.MarchDaysRequested, r.War.MarchDaysGranted);
            Assert.Equal(r.War.GrowthDemand, r.War.GrowthGranted);
            Assert.Equal(0, r.War.PeriodsCivilianShort);
            Assert.True(r.FinalLedger.Imported > 0);
            AssertLedgerCloses(r);
            runs.Add(r);
        }
        Assert.Equal(runs[0].StateHash, runs[1].StateHash);
    }

    [Fact]
    public void AnOpeningCredit_WithoutATradeRule_IsRefused()
    {
        BaselineSeries s = Synthetic(4, 3600, 1, 7_000, 50_000, 8_000.0);
        FoodScenario sc = Scn(s, "PEACE", Normal, AllocationOrder.CivilianMilitaryGrowth, OverflowRule.CapBeforeMilitary, UnfedConsequence.None) with { OpeningCredit = 1 };
        Assert.Throws<ArgumentOutOfRangeException>(() => FoodPulseModel.Run(s, sc, P));
        FoodScenario negative = Scn(s, "PEACE", Normal, AllocationOrder.CivilianMilitaryGrowth, OverflowRule.TradeCreditSinceFounding, UnfedConsequence.None) with { OpeningCredit = -1 };
        Assert.Throws<ArgumentOutOfRangeException>(() => FoodPulseModel.Run(s, negative, P));
    }

    [Fact]
    public void TradeCredit_SameInputs_SameState_AndThePinnedHash()
    {
        BaselineSeries s = Synthetic(25, 3600, 1, 7_000, 50_000, 8_000.0);
        // A full store at the start (it overflows at once and is sold), then a 1,000-man campaign that draws the credit.
        FoodScenario sc = Scn(s, "WAR-1000", Normal, AllocationOrder.CivilianGrowthMilitary, OverflowRule.TradeCredit, UnfedConsequence.Desert, horizon: 20);
        FoodRunResult a = FoodPulseModel.Run(s, sc, P), b = FoodPulseModel.Run(s, sc, P);
        Assert.Equal(a.StateHash, b.StateHash);
        Assert.Equal(a.PopulationByTurn, b.PopulationByTurn);
        Assert.True(a.FinalLedger.Exported > 0 && a.FinalLedger.Imported > 0, "exported " + a.FinalLedger.Exported + " imported " + a.FinalLedger.Imported);
        Assert.True(PinnedTradeHash == a.StateHash, "state hash " + a.StateHash);
    }

    /// <summary>Pinned 2026-11-06 (Debug and Release must both reproduce it). Re-pinned in verification round 2: the growth
    /// claim's remainder accumulator (F2-5) moved it from 7372c81b…101a; with the per-period ceiling restored on the round-2
    /// tree the old value is reproduced exactly, so F2-5 is its only cause.</summary>
    private const string PinnedTradeHash = "a60d228209e998fe7b8cace97128bde7c6f813b539f6d7a93c90570b7365ce49";

    /// <summary>Verification round 1 (F-V8): under TradeCredit with growth EATEN (GrowthClaim.Consumed), a growth grant the
    /// physical store cannot cover is bought back from the credit. Per campaign turn CGM grants the 10-year growth claim
    /// against store + credit, beyond the capped store: every granted unit is eaten, the shortfall is imported for draws,
    /// and growth eats more than under the loss flow.</summary>
    [Fact]
    public void TradeCredit_GrowthEatenBeyondTheStore_IsBoughtBackFromTheCredit()
    {
        BaselineSeries s = Synthetic(6, 3600, 1, 7_000, 80_000, 8_000.0);
        FoodScenario Eaten(OverflowRule o) => FoodComparison.Scenario(s, P, new EpochPlan(2, 3), "PEACE", Normal, AllocationOrder.CivilianGrowthMilitary, o,
            UnfedConsequence.None, HaulBasis.FullLoad, SettlementGranularity.CampaignTurn, GrowthClaim.Consumed);
        FoodRunResult lost = FoodPulseModel.Run(s, Eaten(OverflowRule.CapBeforeMilitary), P);
        FoodRunResult sold = FoodPulseModel.Run(s, Eaten(OverflowRule.TradeCredit), P);
        Assert.Equal(sold.Whole.GrowthGranted, sold.FinalLedger.GrowthEaten);
        Assert.True(sold.FinalLedger.GrowthEaten > lost.FinalLedger.GrowthEaten, sold.FinalLedger.GrowthEaten + " vs " + lost.FinalLedger.GrowthEaten);
        long boughtForDraws = sold.FinalLedger.Imported - sold.FinalLedger.ImportedForCivilians;
        Assert.True(boughtForDraws > 0);
        // Everything the army and growth drew either came from the store or was bought back for them: the draws exceed what
        // the store alone could have paid by exactly the imports for draws (checked per period by the model's grain guard).
        Assert.True(sold.FinalLedger.GrowthEaten + sold.FinalLedger.Ration > lost.FinalLedger.GrowthEaten + lost.FinalLedger.Ration);
        AssertLedgerCloses(sold);
    }

    /// <summary>Verification round 1 (F-V5): the ration claim is a per-sim-year rate integrated with a carried remainder, so
    /// the grain requested over a turn does not depend on how the turn is cut: 172 War Pulses (171 × 21 d + 9 d) request what
    /// one 3,600-day period requests, ⌊soldiers × upkeep × years⌋ (±1 for the last remainder).</summary>
    [Theory]
    [InlineData("GARRISON-1000")]
    [InlineData("PEACE")]
    public void RationClaim_IsIntegratedPerSimYear_IndependentOfThePeriodLength(string army)
    {
        BaselineSeries s = Synthetic(6, 3600, 1, 7_000, 80_000, 8_000.0);
        FoodRunResult pulse = FoodPulseModel.Run(s, Scn(s, army, Normal, AllocationOrder.CivilianMilitaryGrowth, OverflowRule.CapBeforeMilitary, UnfedConsequence.None, SettlementGranularity.WarPulse, horizon: 3), P);
        FoodRunResult turn = FoodPulseModel.Run(s, Scn(s, army, Normal, AllocationOrder.CivilianMilitaryGrowth, OverflowRule.CapBeforeMilitary, UnfedConsequence.None, SettlementGranularity.CampaignTurn, horizon: 3), P);
        Assert.Equal(pulse.FinalSoldiers, pulse.WarSoldiersAtStart);   // a constant army (nothing deserts or dies)
        long exact = (long)Math.Floor(pulse.FinalSoldiers * P.UpkeepPerPersonYear * 3 * 3600 / (double)SimClock.YearDays);
        Assert.Equal(3 * 172, pulse.Whole.Periods);
        Assert.Equal(exact, turn.Whole.RationRequested);
        Assert.InRange(pulse.Whole.RationRequested, exact - 1, exact);
    }

    /// <summary>Verification round 1 (F-V6): the granary capacity is granaryYearsOfDemand × the CIVILIAN grain demand at the
    /// period's start (the shipped ConsumptionSystem basis), so raising a garrison shrinks it. Per campaign turn from an
    /// empty store with a large harvest the store is capped: the capacity read back from the run (end stock, plus what the
    /// military drew after the cap under CapBeforeMilitary) must equal ⌊1.5 × Σ w·civilians × grain need⌋ computed from
    /// the civilians left after the mobilisation, and the garrison's capacity is smaller than peace's.</summary>
    [Theory]
    [InlineData(OverflowRule.CapBeforeMilitary, AllocationOrder.CivilianMilitaryGrowth)]
    [InlineData(OverflowRule.CapBeforeMilitary, AllocationOrder.CivilianGrowthMilitary)]
    [InlineData(OverflowRule.AllocateBeforeCap, AllocationOrder.CivilianMilitaryGrowth)]
    [InlineData(OverflowRule.AllocateBeforeCap, AllocationOrder.CivilianGrowthMilitary)]
    public void GranaryCapacity_IsCivilianDemandOnly_SoMobilisationShrinksIt(OverflowRule rule, AllocationOrder order)
    {
        BaselineSeries s = Synthetic(3, 3600, 1, 0, 80_000, 8_000.0);
        double need = s.At(3).StepGrainEaten / (s.At(2).NutritionalPersons * s.At(3).DtYears);
        long Capacity(FoodRunResult r) => r.StockByTurn[1] + (rule == OverflowRule.CapBeforeMilitary ? r.FinalLedger.Ration + r.FinalLedger.Haul : 0);
        long Expected(long[] civ)
        {
            double nutr = 0.0;
            for (int c = 0; c < civ.Length; c++) nutr += P.CohortWeights[c] * civ[c];
            return (long)Math.Floor(P.GranaryYearsOfDemand * (nutr * need));
        }
        long[] peaceCiv = (long[])s.At(2).Cohorts.Clone();
        long[] garrisonCiv = (long[])peaceCiv.Clone();
        long[] take = FoodPulseModel.Proportional(1000 - s.At(2).Soldiers, garrisonCiv, Cohorts.FirstAdult, Cohorts.FirstElder);
        for (int c = 0; c < garrisonCiv.Length; c++) garrisonCiv[c] -= take[c];
        FoodRunResult peace = FoodPulseModel.Run(s, Scn(s, "PEACE", Normal, order, rule, UnfedConsequence.None, SettlementGranularity.CampaignTurn, horizon: 1), P);
        FoodRunResult garrison = FoodPulseModel.Run(s, Scn(s, "GARRISON-1000", Normal, order, rule, UnfedConsequence.None, SettlementGranularity.CampaignTurn, horizon: 1), P);
        Assert.True(peace.FinalLedger.Overflow > 0 && garrison.FinalLedger.Overflow > 0, "the store must be capped");
        Assert.Equal(1000, garrison.FinalSoldiers);
        Assert.Equal(Expected(peaceCiv), Capacity(peace));
        Assert.Equal(Expected(garrisonCiv), Capacity(garrison));
        Assert.True(Capacity(garrison) < Capacity(peace), Capacity(garrison) + " vs " + Capacity(peace));
    }

    /// <summary>Verification round 1 (F-V11): the trade CSV — one row per trade-rule run (none for the other rules), every
    /// row populated, and its columns equal to the run's ledgers.</summary>
    [Fact]
    public void TradeCsv_HasOneRowPerTradeRun_AndItsColumnsAreTheLedgers()
    {
        BaselineSeries s = Synthetic(8, 3600, 1, 7_000, 50_000, 8_000.0);
        FoodPlan plan = new([new EpochPlan(2, 4)], ["PEACE", "WAR-1000"], [Normal, SevereEmpty], [.. Orders],
            [OverflowRule.CapBeforeMilitary, OverflowRule.TradeCredit], [UnfedConsequence.None], MeanHaulSensitivity: false, Validation: false);
        FoodBattery b = FoodComparison.RunBattery([s], plan, P);
        string[] lines = FoodComparison.TradeCsv(b).Split('\n', StringSplitOptions.RemoveEmptyEntries);
        string[] header = lines[0].Split(',');
        int trade = 0;
        foreach (FoodRunResult r in b.Runs) if (FoodPulseModel.IsTrade(r.Scenario.Overflow)) trade++;
        Assert.Equal(8, trade);
        Assert.Equal(1 + trade, lines.Length);
        int k = 1;
        foreach (FoodRunResult r in b.Runs)
        {
            if (!FoodPulseModel.IsTrade(r.Scenario.Overflow)) continue;
            string[] f = lines[k++].Split(',');
            Assert.Equal(header.Length, f.Length);
            long Col(string name) => long.Parse(f[Array.IndexOf(header, name)], CultureInfo.InvariantCulture);
            Assert.Equal(r.FinalLedger.Exported, Col("exportedH"));
            Assert.Equal(r.FinalLedger.Imported, Col("importedH"));
            Assert.Equal(r.FinalLedger.ImportedForCivilians, Col("importedCivH"));
            Assert.Equal(r.FinalLedger.Ration, Col("rationH"));
            Assert.Equal(r.FinalLedger.CreditImplied, Col("creditEnd"));
            Assert.Equal(r.WindowEndLedger.Imported - r.WindowStartLedger.Imported, Col("importedWar"));
            Assert.Equal(r.StateHash, f[^1]);
        }
        long sumExported = 0, sumImported = 0;
        for (int i = 1; i < lines.Length; i++)
        {
            string[] f = lines[i].Split(',');
            sumExported += long.Parse(f[Array.IndexOf(header, "exportedH")], CultureInfo.InvariantCulture);
            sumImported += long.Parse(f[Array.IndexOf(header, "importedH")], CultureInfo.InvariantCulture);
        }
        Assert.True(sumExported > 0 && sumImported > 0, "a populated table: exported " + sumExported + " imported " + sumImported);
    }

    // ------------------------------------------------------------------ WA1 delivery: window edges at the boundary instant (±1 day)

    [Theory]
    [InlineData(0, 0)]       // at scenario start
    [InlineData(21, 21)]     // exactly a pulse start → that pulse
    [InlineData(20, 21)]     // one day before a pulse start → that pulse
    [InlineData(22, 42)]     // one day after → the next pulse
    public void WindowStart_AppliesToTheFirstPulseStartingAtOrAfterIt(long start, long expected)
    {
        BaselineSeries s = Synthetic(4, 3600, 1, 7_000, 80_000, 8_000.0);
        FoodScenario sc = Scn(s, "WAR-500", Normal, AllocationOrder.CivilianMilitaryGrowth, OverflowRule.CapBeforeMilitary, UnfedConsequence.None, horizon: 1)
            with { WindowStartDay = start, WindowLengthDays = 1092 - start };
        FoodRunResult r = FoodPulseModel.Run(s, sc, P);
        Assert.Equal(expected, r.WindowStartDayApplied);
        Assert.Equal(1092, r.WindowEndDayApplied);
        Assert.Equal((1092 - expected) / 21, r.War.Periods);
        Assert.Equal(r.War.Periods * 21, r.War.MarchDaysRequested);   // the army marched every pulse of the window, no more
    }

    [Theory]
    [InlineData(1071, 1071)]   // exactly a pulse start → demobilised there
    [InlineData(1070, 1071)]   // −1 day → the same pulse
    [InlineData(1072, 1092)]   // +1 day → the next pulse
    [InlineData(1080, 1092)]   // the 3-year window of the battery
    public void WindowEnd_AppliesToTheFirstPulseStartingAtOrAfterIt(long length, long expected)
    {
        BaselineSeries s = Synthetic(4, 3600, 1, 7_000, 80_000, 8_000.0);
        FoodScenario sc = Scn(s, "WAR-500", Normal, AllocationOrder.CivilianMilitaryGrowth, OverflowRule.CapBeforeMilitary, UnfedConsequence.None, horizon: 1)
            with { WindowLengthDays = length };
        FoodRunResult r = FoodPulseModel.Run(s, sc, P);
        Assert.Equal(expected, r.WindowEndDayApplied);
        Assert.Equal(expected / 21, r.War.Periods);
        // Mobilised at the window's start, demobilised exactly at its applied end: the army is back to the founding level.
        Assert.Equal(r.WindowEndLedger.Mobilised, r.FinalLedger.Demobilised);
        Assert.Equal(r.FinalLedger.SoldiersInitial, r.FinalSoldiers);
    }

    // ------------------------------------------------------------------ dt-correctness

    [Theory]
    [InlineData(3600L)]
    [InlineData(1800L)]
    public void HarvestAndDemography_DoNotDependOnThePeriodLength(long dtDays)
    {
        // Peace, nothing short: the War-Pulse and the campaign-turn runs harvest exactly the same grain (cumulative-floor
        // integration of the same rates) and grow exactly the same people (demography on the kernel's 180-day grid).
        BaselineSeries s = Synthetic(8, dtDays, 1, 7_000, 80_000 * dtDays / 3600, 8_000.0);
        FoodRunResult pulse = FoodPulseModel.Run(s, Scn(s, "PEACE", Normal, AllocationOrder.CivilianMilitaryGrowth, OverflowRule.CapBeforeMilitary, UnfedConsequence.None, SettlementGranularity.WarPulse, horizon: 5), P);
        FoodRunResult turn = FoodPulseModel.Run(s, Scn(s, "PEACE", Normal, AllocationOrder.CivilianMilitaryGrowth, OverflowRule.CapBeforeMilitary, UnfedConsequence.None, SettlementGranularity.CampaignTurn, horizon: 5), P);
        Assert.Equal(FoodModelParams.WarPulseDays(dtDays), pulse.LargestPeriodDays);
        Assert.Equal(dtDays, turn.LargestPeriodDays);
        Assert.Equal(turn.FinalLedger.Harvest, pulse.FinalLedger.Harvest);
        Assert.True(pulse.FinalLedger.Harvest > 0);
        Assert.Equal(turn.PopulationByTurn, pulse.PopulationByTurn);
        Assert.Equal(DemographicsSystem.MicroStepYears * SimClock.YearDays, FoodPulseModel.MicroDays);
    }

    [Theory]
    [InlineData(SettlementGranularity.WarPulse)]
    [InlineData(SettlementGranularity.CampaignTurn)]
    public void Spoilage_IsTheSurvivalExponentialOfThePeriodLength(SettlementGranularity g)
    {
        // A closed store (nobody eats, nothing is harvested, no capacity) loses 1 − e^(−0.08·10) over a 10-year turn
        // whatever the period: 1,000,000 → 449,329 (the campaign turn is one whole-unit flow; 172 pulses carry remainders).
        BaselineSeries s = Synthetic(3, 3600, 0, 1_000_000, 0, 0.0, soldiers: 0);
        FoodRunResult r = FoodPulseModel.Run(s, Scn(s, "PEACE", Normal, AllocationOrder.CivilianMilitaryGrowth, OverflowRule.NoCap, UnfedConsequence.None, g, horizon: 1), P);
        long expected = 1_000_000 - (long)Math.Floor(1_000_000 * (1.0 - Math.Exp(-P.SpoilagePerYear * 10.0)));
        Assert.Equal(449_329, expected);
        Assert.InRange(r.StockByTurn[1], expected - 1, expected + 1);
    }

    // ------------------------------------------------------------------ determinism and integer helpers

    [Fact]
    public void SameInputs_SameState_AndThePinnedHash()
    {
        BaselineSeries s = Synthetic(25, 3600, 1, 7_000, 50_000, 8_000.0);
        FoodScenario sc = Scn(s, "WAR-1000", SevereEmpty, AllocationOrder.CivilianGrowthMilitary, OverflowRule.CapBeforeMilitary, UnfedConsequence.Desert, horizon: 20);
        FoodRunResult a = FoodPulseModel.Run(s, sc, P), b = FoodPulseModel.Run(s, sc, P);
        Assert.Equal(a.StateHash, b.StateHash);
        Assert.Equal(a.PopulationByTurn, b.PopulationByTurn);
        Assert.True(PinnedHash == a.StateHash, "state hash " + a.StateHash);
    }

    /// <summary>Pinned in this session (Debug and Release must both reproduce it).</summary>
    private const string PinnedHash = "84a4a967827fdbd25be38e17927471b8facd48b42255ffb8645484f7e2d72b0b";

    [Fact]
    public void Proportional_IsLargestRemainder_TiesToTheLowerCohort()
    {
        var w = new long[16];
        for (int c = 3; c < 12; c++) w[c] = 100;
        long[] five = FoodPulseModel.Proportional(5, w, 3, 12);
        Assert.Equal(new long[] { 0, 0, 0, 1, 1, 1, 1, 1, 0, 0, 0, 0, 0, 0, 0, 0 }, five);
        long[] twelve = FoodPulseModel.Proportional(12, w, 3, 12);
        Assert.Equal(new long[] { 0, 0, 0, 2, 2, 2, 1, 1, 1, 1, 1, 1, 0, 0, 0, 0 }, twelve);
        w[7] = 101;   // a strictly larger remainder wins over a lower index
        Assert.Equal(1, FoodPulseModel.Proportional(1, w, 3, 12)[7]);
    }

    [Fact]
    public void BaselineText_RoundTripsExactly()
    {
        BaselineSeries s = Synthetic(5, 3600, 1, 7_000, 50_000, 8_000.123456789);
        s = s with { OverflowTwins = [new OverflowTwin(900, 2, 480, [new OverflowTwinTurn(3, 500, 1, 2, 3, 4, 5, 6, 7, 8, 9, 10)])] };
        string text = FoodBaseline.ToText(s);
        BaselineSeries back = FoodBaseline.FromText(text);
        Assert.Equal(text, FoodBaseline.ToText(back));
        Assert.Equal(s.Turns[3].HeadroomLimit, back.Turns[3].HeadroomLimit);
        Assert.Equal(s.Twins, back.Twins);
    }

    // ------------------------------------------------------------------ verification round 2 (F2-1 … F2-5)

    /// <summary>Verification round 2 (F2-1): the march haul is a per-sim-year bill. One LEG (the days between two resupplies)
    /// is billed by MovementFood — its rations carried over its route — and a period of len days draws len ÷ leg of it with
    /// a remainder: over a continuous march of D days the claim is ⌊legHaul × D ÷ leg⌋ (−1 at most) whatever the settlement
    /// period. Before round 2 the leg WAS the period (one move of the period's length per period), so the yearly haul grew
    /// with the period: a 7-day settlement billed a third of the 21-day one.</summary>
    [Theory]
    [InlineData(7L, 21L)]
    [InlineData(14L, 21L)]
    [InlineData(21L, 21L)]
    [InlineData(42L, 21L)]
    [InlineData(21L, 7L)]
    [InlineData(21L, 42L)]
    public void MarchHaul_IsBilledPerSimYear_ByTheLeg_NotByTheSettlementPeriod(long periodDays, long legDays)
    {
        BaselineSeries s = Synthetic(4, 3600, 1, 7_000, 80_000, 8_000.0);
        FoodScenario sc = Scn(s, "WAR-1000", Normal, AllocationOrder.CivilianMilitaryGrowth, OverflowRule.CapBeforeMilitary, UnfedConsequence.None, horizon: 1)
            with { PulseDays = periodDays, MarchLegDays = legDays };
        FoodRunResult r = FoodPulseModel.Run(s, sc, P);
        long n = r.WarSoldiersAtStart;
        Assert.Equal(n, r.WarSoldiersAtEnd);                                     // a constant army (consequence None)
        Assert.Equal(periodDays, r.LargestPeriodDays);
        long days = r.War.MarchDaysRequested;
        Assert.Equal(r.WindowEndDayApplied - r.WindowStartDayApplied, days);     // a continuous march: every window day
        long legHaul = MovementFood.FromRouteCost(P.March, 0, legDays * P.March.BaselineKmPerDay / P.March.KmPerCostUnit, 0.0, n, HaulBasis.FullLoad).HaulUnits;
        long expected = (long)Math.Floor(legHaul * (double)days / legDays);
        Assert.InRange(r.War.HaulRequested, expected - 1, expected);
    }

    /// <summary>Verification round 2 (F2-5): the growth claim is integrated with a remainder accumulator, as the civilian
    /// requirement and the ration: after every period the claims granted so far are the floor of the exact claims so far
    /// (0 ≤ Σ exact − Σ claimed &lt; 1). The per-period ceiling it replaces added up to one unit per period. The exact claim
    /// is read from the run's trace (the "Gx" field; the trace never feeds back into the run).</summary>
    [Theory]
    [InlineData(SettlementGranularity.WarPulse, 7L)]
    [InlineData(SettlementGranularity.WarPulse, 0L)]
    [InlineData(SettlementGranularity.CampaignTurn, 0L)]
    public void GrowthClaim_IsIntegratedWithARemainder_NoPerPeriodRounding(SettlementGranularity granularity, long pulseDays)
    {
        BaselineSeries s = Synthetic(4, 3600, 1, 7_000, 80_000, 8_000.0);
        FoodScenario sc = Scn(s, "PEACE", Normal, AllocationOrder.CivilianMilitaryGrowth, OverflowRule.CapBeforeMilitary, UnfedConsequence.None, granularity, horizon: 2)
            with { PulseDays = pulseDays };
        long claimed = 0;
        double exact = 0.0;
        int periods = 0;
        FoodRunResult r = FoodPulseModel.Run(s, sc, P, line =>
        {
            string[] t = line.Split(' ');
            long g = long.Parse(t[Array.IndexOf(t, "G") + 1], CultureInfo.InvariantCulture);
            double gx = double.Parse(t[Array.IndexOf(t, "Gx") + 1], CultureInfo.InvariantCulture);
            claimed += g;
            exact += gx;
            periods++;
            Assert.InRange(exact - claimed, -1e-3, 1.0 + 1e-3);
        });
        Assert.Equal(r.Whole.Periods, periods);
        Assert.Equal(r.Whole.GrowthDemand, claimed);
        Assert.True(exact > 1_000.0, "the headroom must bind (a finite, positive growth claim): " + exact.ToString(CultureInfo.InvariantCulture));
    }

    /// <summary>Verification round 2 (F2-3): the reporting split of the imports means what tables §13 say it means. With an
    /// empty store, no harvest inside the window and an ample credit, every unit the civilians eat in the window is bought
    /// back FOR CIVILIANS, and every unit the army draws (ration + haul) is bought back for draws:
    /// ImportedForCivilians = EatenCivilian and Imported − ImportedForCivilians = Ration + Haul, exactly, both orders.</summary>
    [Theory]
    [InlineData(AllocationOrder.CivilianGrowthMilitary)]
    [InlineData(AllocationOrder.CivilianMilitaryGrowth)]
    public void TradeCredit_TheImportSplit_CiviliansBuyBackWhatTheyAte_TheArmyWhatItDrew(AllocationOrder order)
    {
        BaselineSeries s = Synthetic(4, 3600, 1, 7_000, 80_000, 8_000.0);
        FoodScenario sc = Scn(s, "WAR-1000", NormalEmpty, order, OverflowRule.TradeCredit, UnfedConsequence.None, horizon: 1)
            with { HarvestMultiplier = 0.0, OpeningCredit = 100_000_000 };
        FoodRunResult r = FoodPulseModel.Run(s, sc, P);
        FoodLedger a = r.WindowStartLedger, b = r.WindowEndLedger;
        Assert.Equal(0, b.Harvest - a.Harvest);                                  // nothing grows in the window
        long eaten = b.EatenCivilian - a.EatenCivilian;
        long draws = b.Ration - a.Ration + (b.Haul - a.Haul);
        Assert.True(eaten > 0 && draws > 0 && b.Haul - a.Haul > 0, "eaten " + eaten + ", draws " + draws);
        Assert.Equal(r.War.CivilianRequested, eaten);                            // civilians fed in full, from the credit
        Assert.Equal(eaten, b.ImportedForCivilians - a.ImportedForCivilians);
        Assert.Equal(draws, b.Imported - a.Imported - (b.ImportedForCivilians - a.ImportedForCivilians));
        AssertLedgerCloses(r);
    }

    /// <summary>Verification round 2 (F2-4): every reported metric is the window ratio its name says, and each named CSV
    /// column carries that metric. The run is chosen so that the ratios a mix-up would substitute differ (march DAYS
    /// granted ≠ haul granted; growth served ≠ ration supplied).</summary>
    [Fact]
    public void Metrics_AreTheWindowRatiosTheirNamesSay_AndTheCsvColumnsCarryThem()
    {
        CultureInfo inv = CultureInfo.InvariantCulture;
        BaselineSeries s = Synthetic(22, 3600, 1, 7_000, 80_000, 8_000.0);
        FoodScenario Of(string army) => Scn(s, army, Normal, AllocationOrder.CivilianGrowthMilitary, OverflowRule.CapBeforeMilitary, UnfedConsequence.Desert, horizon: 20)
            with { HarvestMultiplier = 0.7 };
        FoodRunResult r = FoodPulseModel.Run(s, Of("WAR-1000"), P);
        FoodRunResult peace = FoodPulseModel.Run(s, Of("PEACE"), P);
        AllocationStats w = r.War;
        double march = w.MarchDaysGranted / (double)w.MarchDaysRequested, haul = w.HaulGranted / (double)w.HaulRequested;
        double ration = w.RationGranted / (double)w.RationRequested, growth = w.GrowthGranted / (double)w.GrowthDemand;
        Assert.True(w.MarchDaysGranted > 0 && march < 1.0 && march != haul, "march " + march + ", haul " + haul);
        Assert.True(growth != ration && r.FinalLedger.Deserted > 0, "growth " + growth + ", ration " + ration + ", deserted " + r.FinalLedger.Deserted);

        RunMetrics m = FoodComparison.Metrics(r, peace, P);
        Assert.Equal(march, m.MarchAvailability);
        Assert.Equal(ration, m.RationSupply);
        Assert.Equal(growth, m.GrowthServed);
        Assert.Equal(w.PeriodsGrowthReduced / (double)w.Periods, m.ShareGrowthReduced);
        Assert.Equal(w.PeriodsMilitaryRestricted / (double)w.Periods, m.ShareMilitaryRestricted);
        Assert.Equal(w.PeriodsCivilianShort, m.PeriodsCivilianShort);
        Assert.Equal(w.RationGranted / P.UpkeepPerPersonYear, m.FedSoldierYears);
        Assert.Equal((w.HaulGranted, w.RationGranted), (m.HaulWar, m.RationWar));   // what the window granted is what its ledger drew
        Assert.Equal((r.WarSoldiersAtStart, r.WarSoldiersAtEnd, r.FinalLedger.Deserted), (m.SoldiersWarStart, m.SoldiersWarEnd, m.Deserted));
        Assert.Equal(r.PopulationByTurn[20], m.Pop20);
        Assert.Equal(100.0 * (r.PopulationByTurn[20] - peace.PopulationByTurn[20]) / peace.PopulationByTurn[20], m.Gap20);
        Assert.Equal(r.WindowEndLedger.Overflow - r.WindowStartLedger.Overflow, m.OverflowWar);

        var battery = new FoodBattery { Series = [s], Params = P, Plan = FoodComparison.DefaultPlan() };
        battery.Runs.Add(r);
        battery.Runs.Add(peace);
        string[] lines = FoodComparison.Csv(battery).Split('\n');
        string[] head = lines[0].Split(',');
        string[] row = lines[1].Split(',');
        Assert.Equal("WAR-1000", row[Array.IndexOf(head, "army")]);
        double Col(string name) => double.Parse(row[Array.IndexOf(head, name)], inv);
        Assert.Equal(m.MarchAvailability, Col("marchAvail"), 6);
        Assert.Equal(m.RationSupply, Col("rationSupply"), 6);
        Assert.Equal(m.GrowthServed, Col("growthServed"), 6);
        Assert.Equal(m.Gap20, Col("gap20"), 3);
        Assert.Equal(m.HaulWar, (long)Col("haulWar"));
        Assert.Equal(m.SoldiersWarEnd, (long)Col("soldiersWarEnd"));
        Assert.Equal(m.Deserted, (long)Col("deserted"));
    }

    /// <summary>Verification round 2 (F2-2): the Starve proposal is a per-sim-year law, so under a constant deprivation
    /// (empty store, no harvest in the window: the ration is 0 every period) the army follows S0 × e^(−max × days ÷ year)
    /// whatever the settlement period, to integer rounding. The Desert proposal has no such property (an instant adjustment
    /// to the strength the period's grant feeds; report §15) and is not pinned here.</summary>
    [Theory]
    [InlineData(7L)]
    [InlineData(14L)]
    [InlineData(21L)]
    [InlineData(42L)]
    public void Starve_IsAPerSimYearLaw_TheSettlementPeriodDoesNotMoveTheArmy(long periodDays)
    {
        BaselineSeries s = Synthetic(4, 3600, 1, 7_000, 80_000, 8_000.0);
        FoodScenario sc = Scn(s, "GARRISON-1000", NormalEmpty, AllocationOrder.CivilianMilitaryGrowth, OverflowRule.CapBeforeMilitary, UnfedConsequence.Starve, horizon: 1)
            with { HarvestMultiplier = 0.0, PulseDays = periodDays };
        FoodRunResult r = FoodPulseModel.Run(s, sc, P);
        Assert.Equal(0, r.War.RationGranted);                                    // deprived in every period
        Assert.Equal(1000, r.WarSoldiersAtStart);
        double years = (r.WindowEndDayApplied - r.WindowStartDayApplied) / (double)SimClock.YearDays;
        double expected = 1000 * Math.Exp(-P.StarvationMaxPerYear * years);
        Assert.InRange(r.WarSoldiersAtEnd, expected - 2.0, expected + 2.0);
        Assert.True(r.WarSoldiersAtEnd < 1000);
        AssertLedgerCloses(r);
    }

    // ------------------------------------------------------------------ the REAL world (seed 42, 28 turns, ~20 s)

    [Fact]
    public void RealSeries_GrainIdentityCloses_TwinsAreMeasured_AndThePeaceModelIsPeriodInvariant()
    {
        BaselineSeries s = FoodBaseline.Measure(42, 28, [25], 2);
        foreach (BaselineTurn row in s.Turns) Assert.Equal(0, row.StepOtherGrainNet);   // no grain flow the series does not name
        (LabourTwin small, LabourTwin large) = s.TwinsAt(25);
        Assert.Equal(960, small.Removed);                                                 // 12 settlements × 4 Warbands × 20
        Assert.True(large.Removed > small.Removed && small.MarginalGrainPerAdultYear > 0);
        Assert.Equal(2, s.OverflowTwins.Length);
        Assert.Equal(FoodBaseline.ToText(s), FoodBaseline.ToText(FoodBaseline.FromText(FoodBaseline.ToText(s))));
        FoodScenario Peace(SettlementGranularity g) => FoodComparison.Scenario(s, P, new EpochPlan(25, 2), "PEACE", Normal,
            AllocationOrder.CivilianMilitaryGrowth, OverflowRule.CapBeforeMilitary, UnfedConsequence.None, HaulBasis.FullLoad, g);
        FoodRunResult pulse = FoodPulseModel.Run(s, Peace(SettlementGranularity.WarPulse), P);
        FoodRunResult turn = FoodPulseModel.Run(s, Peace(SettlementGranularity.CampaignTurn), P);
        Assert.Equal(turn.PopulationByTurn, pulse.PopulationByTurn);
        Assert.Equal(s.At(25).Population + s.At(25).Soldiers, pulse.PopulationByTurn[0]);
        // TradeCreditSinceFounding opens with exactly the grain the REAL world sank as GranaryOverflow in turns 1..25.
        long sunk = 0;
        for (long t = 1; t <= 25; t++) sunk += s.At(t).StepOverflow;
        Assert.True(sunk > 0);
        FoodScenario founding = FoodComparison.Scenario(s, P, new EpochPlan(25, 2), "PEACE", Normal, AllocationOrder.CivilianMilitaryGrowth,
            OverflowRule.TradeCreditSinceFounding, UnfedConsequence.None, HaulBasis.FullLoad);
        Assert.Equal(sunk, founding.OpeningCredit);
        Assert.Equal(0, FoodComparison.Scenario(s, P, new EpochPlan(25, 2), "PEACE", Normal, AllocationOrder.CivilianMilitaryGrowth,
            OverflowRule.TradeCredit, UnfedConsequence.None, HaulBasis.FullLoad).OpeningCredit);
    }
}
