using Sim.Core.Observability;
using Sim.Tests.TestUtil;

namespace Sim.Tests.Observability;

/// <summary>
/// D-055 / ADR-040 (batch 4, item 3) — THE CHECKER OF THE CROSS-PROCESS ALLOCATION LEG, TESTED ("nothing checked the checker",
/// the T3.12 lesson; the CorridorStatus precedent). <see cref="SplitReportCheck"/> judges a `sim run --split-report` file;
/// these tests feed it the REAL reports in Fixtures/split-report (a 24-step continuation of the seed-42 founded world from
/// turn 116: b = 30, road orders, granaries competing at settlements 0 and 2, and settlement 0's builders removed at turn
/// 118 so its promoted head SETS ASIDE the grant a younger head needed — README.md there) and, for every property, a MUTANT
/// report derived from them that breaks exactly that property. AIM: the real pair passes every property, with the witnesses
/// pinned. TEETH: each mutant fails its property, which the real report does not.
/// </summary>
public class SplitReportCheckTests
{
    internal static string FixturePath(string name) => Path.Combine(AppContext.BaseDirectory, "Fixtures", "split-report", name);

    internal static SplitReportRow[] A() => SplitReport.Parse(File.ReadAllText(FixturePath("a.jsonl")));

    internal static SplitReportRow[] Control() => SplitReport.Parse(File.ReadAllText(FixturePath("control.jsonl")));

    private static readonly SplitCheckResult Clean = SplitReportCheck.Check(A(), Control(), expectHoldAt: 120);

    private static SplitReportRow Row(SplitReportRow[] rows, long turn) => rows.Single(r => r.Turn == turn && r.Polity == 1);

    private static SplitGoodFigures Stone(SplitReportRow row) => row.Goods.Single(g => g.Good == "stone");

    /// <summary>The rows with good <paramref name="good"/> of the row at <paramref name="turn"/> replaced by
    /// <paramref name="change"/>.</summary>
    private static SplitReportRow[] WithGood(SplitReportRow[] rows, long turn, string good, Func<SplitGoodFigures, SplitGoodFigures> change) =>
        rows.Select(r => r.Turn != turn ? r : r with { Goods = r.Goods.Select(g => g.Good == good ? change(g) : g).ToArray() }).ToArray();

    /// <summary>Every row's good <paramref name="good"/> replaced where <paramref name="where"/> holds.</summary>
    private static SplitReportRow[] EveryGood(SplitReportRow[] rows, string good, Func<SplitReportRow, SplitGoodFigures, bool> where,
        Func<SplitGoodFigures, SplitGoodFigures> change) =>
        rows.Select(r => r with { Goods = r.Goods.Select(g => g.Good == good && where(r, g) ? change(g) : g).ToArray() }).ToArray();

    /// <summary>Every head of every row replaced by <paramref name="change"/>.</summary>
    private static SplitReportRow[] EveryHead(SplitReportRow[] rows, Func<SplitReportRow, SplitHeadFigures, SplitHeadFigures> change) =>
        rows.Select(r => r with { Heads = r.Heads.Select(h => change(r, h)).ToArray() }).ToArray();

    /// <summary>Every head good whose cause is HeldForEarlier replaced by <paramref name="change"/>.</summary>
    private static SplitReportRow[] EveryHeldGood(SplitReportRow[] rows, Func<SplitHeadGood, SplitHeadGood> change) =>
        EveryHead(rows, (_, h) => h with { Goods = h.Goods.Select(g => g.Cause == SplitReportCheck.HeldForEarlier ? change(g) : g).ToArray() });

    /// <summary>The rows with good <paramref name="good"/> of head (<paramref name="settlement"/>, <paramref name="slot"/>) in
    /// the row at <paramref name="turn"/> replaced by <paramref name="change"/>.</summary>
    private static SplitReportRow[] WithHeadGood(SplitReportRow[] rows, long turn, int settlement, int slot, string good,
        Func<SplitHeadGood, SplitHeadGood> change) =>
        rows.Select(r => r.Turn != turn ? r : r with
        {
            Heads = r.Heads.Select(h => h.Settlement != settlement || h.Slot != slot ? h : h with
            {
                Goods = h.Goods.Select(g => g.Good == good ? change(g) : g).ToArray(),
            }).ToArray(),
        }).ToArray();

    private static void AssertFails(string property, SplitCheckResult mutant)
    {
        Assert.False(Clean.Failed(property), $"premise: the real report passes {property}");
        Assert.True(mutant.Failed(property), $"the mutant report must fail {property}; it failed: " + string.Join(" | ", mutant.Failures));
    }

    // ================================================================== aim: the real pair passes, the witnesses pinned

    [Fact]
    public void TheRealReportAndItsControl_PassEveryProperty_WithTheWitnessesThisRunHas()
    {
        Assert.True(Clean.Passed, string.Join(" | ", Clean.Failures));
        Assert.Equal(24, A().Length);
        Assert.Equal(24, Control().Length);
        Assert.Equal(
        [
            "binding steps (P4): 117 stone; 118 stone; 119 stone; 121 stone; 122 stone; 123 stone; 125 stone; 126 stone",
            "road share to building (P5): 130 stone",
            "building share to roads (P6): 116 stone; 124 stone; 127 stone; 128 stone; 129 stone; 131 stone; 132 stone; 133 stone; "
                + "134 stone; 135 stone; 136 stone; 137 stone; ... (14 in all)",
            "competing heads (P7): 117 stone: s0:0 served, s2:0 waited; 119 stone: s0:1 served, s2:0 waited; "
                + "126 stone: s0:2 served, s2:2 waited",
            "held for an earlier head (P10): 119 s2:0 stone (holder s0:1); 121 s2:1 stone (holder s0:1); 122 s2:1 stone (holder s0:1); "
                + "123 s2:1 stone (holder s0:1)",
        ], Clean.Findings);

        // The first P10 witness, by hand: s0:1 (no builders) sets aside the whole stone grant of step 119->120 and s2:0, a
        // part-built head behind it in the building order, waits HeldForEarlier naming it, although it needed 3 of them.
        SplitReportRow r119 = Row(A(), 119);
        Assert.Equal((66L, 19L, 19L, 0L, 19L), (Stone(r119).S, Stone(r119).Start, Stone(r119).Gb, Stone(r119).Build, Stone(r119).Holds));
        Assert.Equal((0, 1, "Labour", 19L), (r119.Heads[0].Settlement, r119.Heads[0].Slot, r119.Heads[0].Limit, r119.Heads[0].GoodOf(5)!.Hold));
        SplitHeadGood waiting = r119.Heads[1].GoodOf(5)!;
        Assert.Equal((2, 850_000L, 850_000L), (r119.Heads[1].Settlement, r119.Heads[1].Before, r119.Heads[1].After));
        Assert.Equal(("HeldForEarlier", new SplitHeadRef(0, 1, 1), 3L, 0L), (waiting.Cause, waiting.Holder, waiting.Need, waiting.Draw));
    }

    // ================================================================== teeth: one mutant report per property

    [Fact]
    public void P0_AReportThatNeverRanANonDefaultSplitWithARoadOrder_IsVacuous()
    {
        SplitReportRow[] mutant = A().Select(r => r with { BuildingsPercent = 100 }).ToArray();
        AssertFails("P0", SplitReportCheck.Check(mutant));
        SplitReportRow[] noRoads = A().Select(r => r with { RoadPercent = null }).ToArray();
        AssertFails("P0", SplitReportCheck.Check(noRoads));
    }

    [Theory]
    [InlineData("build differs from its forecast")]
    [InlineData("holds differ from their forecast")]
    [InlineData("road differs from its forecast")]
    [InlineData("the heads drew less than the purse fell")]
    [InlineData("the heads set aside more than was published")]
    public void P1_AStepThatDidNotDoWhatTheRuleForecast_Fails(string clause)
    {
        SplitReportRow[] a = A();
        SplitReportRow[] mutant = clause switch
        {
            "build differs from its forecast" => WithGood(a, 117, "stone", g => g with { ForecastBuild = g.ForecastBuild + 1 }),
            "holds differ from their forecast" => WithGood(a, 119, "stone", g => g with { ForecastHolds = g.ForecastHolds - 1 }),
            "road differs from its forecast" => WithGood(a, 117, "stone", g => g with { ForecastRoad = g.ForecastRoad - 1 }),
            "the heads drew less than the purse fell" => a.Select(r => r.Turn != 117 ? r : r with
            {
                Heads = r.Heads.Select((h, i) => i != 0 ? h : h with
                {
                    Goods = h.Goods.Select(g => g.Good == "stone" ? g with { Draw = g.Draw - 1 } : g).ToArray(),
                }).ToArray(),
            }).ToArray(),
            "the heads set aside more than was published" => a.Select(r => r.Turn != 119 ? r : r with
            {
                Heads = r.Heads.Select((h, i) => i != 0 ? h : h with
                {
                    Goods = h.Goods.Select(g => g.Good == "stone" ? g with { Hold = g.Hold + 1 } : g).ToArray(),
                }).ToArray(),
            }).ToArray(),
            _ => throw new ArgumentOutOfRangeException(nameof(clause)),
        };
        AssertFails("P1", SplitReportCheck.Check(mutant, Control(), 120));
    }

    [Fact]
    public void P1_DoesNotHoldRoadWorksToTheForecast_InAStepWhereTwoEmpiresDevelopRoads()
    {
        // SimulateCharges plans on PREV's routes — an approximation when a second Empire's order sees the first's
        // modernisation (RoadDevelopmentQuery). A second Empire with a road order in step 117->118 whose road works drew one
        // unit less than forecast: P1 does not judge road works there; with one road order it does.
        SplitReportRow r117 = Row(A(), 117);
        SplitReportRow second = r117 with
        {
            Polity = 2, Heads = [],
            Goods = r117.Goods.Select(g => g with { Build = 0, ForecastBuild = 0, Holds = 0, ForecastHolds = 0, Road = g.ForecastRoad - (g.Good == "stone" ? 1 : 0) }).ToArray(),
        };
        SplitReportRow[] two = A().SelectMany(r => r.Turn == 117 ? new[] { r, second } : new[] { r }).ToArray();
        Assert.False(SplitReportCheck.Check(two, Control(), 120).Failed("P1"));
        SplitReportRow[] alone = two.Select(r => r.Turn == 117 && r.Polity == 1 ? r with { RoadPercent = null } : r).ToArray();
        Assert.True(SplitReportCheck.Check(alone, Control(), 120).Failed("P1"));
    }

    [Theory]
    [InlineData("the shares do not split S")]
    [InlineData("build and holds exceed the grant")]
    [InlineData("the grant exceeds the claim")]
    [InlineData("the grant exceeds the cap")]
    [InlineData("build, holds and road exceed S")]
    public void P2_AGrantOutOfOrderOrAnOverdraw_Fails(string clause)
    {
        SplitReportRow[] a = A();
        Assert.Equal((82L, 24L, 58L, 23L, 23L, 3L, 20L, 58L),
            (Stone(Row(a, 120)).S, Stone(Row(a, 120)).Eb, Stone(Row(a, 120)).Er, Stone(Row(a, 120)).Db, Stone(Row(a, 120)).Gb,
             Stone(Row(a, 120)).Build, Stone(Row(a, 120)).Holds, Stone(Row(a, 120)).Road));
        SplitReportRow[] mutant = clause switch
        {
            "the shares do not split S" => WithGood(a, 120, "stone", g => g with { Er = g.Er - 1 }),
            "build and holds exceed the grant" => WithGood(a, 120, "stone", g => g with { Gb = 22 }),
            "the grant exceeds the claim" => WithGood(a, 120, "stone", g => g with { Db = 22 }),
            "the grant exceeds the cap" => WithGood(a, 120, "stone", g => g with { Cap = 22 }),
            "build, holds and road exceed S" => WithGood(a, 120, "stone", g => g with { Road = 60, ForecastRoad = 60 }),
            _ => throw new ArgumentOutOfRangeException(nameof(clause)),
        };
        AssertFails("P2", SplitReportCheck.Check(mutant, Control(), 120));
    }

    /// <summary>
    /// P2's "the grant is spent" (fixer round, finding T3-L2-T3-V2-F2): build + holds = G_b on every row and good — the
    /// D-055 walk spends its whole grant by draws or set-asides (SplitReportCheck's header derives it). Before the fix a
    /// report in which a step spent LESS than its grant passed every property, P4 included, as long as some other step
    /// bound. TEETH: (a) the design's scarcity form — a binding step under-spends; (b) the general form — an under-spend
    /// where G_b = D_b (as set-asides counted twice would give), which a scarcity-only clause cannot see. Each mutant keeps
    /// P1 consistent (forecast and heads moved with the actuals) and every existence witness, so P2 alone fails. AIM: the
    /// real pair passes (the Clean premise), and so do the in-process and CLI reports (AllocationCiLegTests,
    /// RunFromSaveCliTests, SplitProbeTests assert P2 on live output).
    /// </summary>
    [Fact]
    public void P2_AStepThatSpentLessThanItsGrant_Fails_WhereTheSplitBinds_AndWhereItDoesNot()
    {
        SplitReportRow[] a = A();

        // (a) Step 118->119 stone binds (G_b 18 < D_b 21) and its heads spend the grant: s0:0 draws 1, s2:0 draws 17. The
        // mutant under-spends by 5 — build and its forecast 18 -> 13, s2:0's draw 17 -> 12.
        SplitReportRow r118 = Row(a, 118);
        Assert.Equal((61L, 18L, 21L, 18L, 0L), (Stone(r118).S, Stone(r118).Gb, Stone(r118).Db, Stone(r118).Build, Stone(r118).Holds));
        Assert.Equal((2, 0, 17L), (r118.Heads[1].Settlement, r118.Heads[1].Slot, r118.Heads[1].GoodOf(5)!.Draw));
        SplitReportRow[] scarce = WithHeadGood(
            WithGood(a, 118, "stone", g => g with { Build = g.Build - 5, ForecastBuild = g.ForecastBuild - 5 }),
            118, 2, 0, "stone", g => g with { Draw = g.Draw - 5 });
        SplitCheckResult s = SplitReportCheck.Check(scarce, Control(), 120);
        AssertFails("P2", s);
        // T3 round 2: P12 re-walks the heads and names the head that took less than the rule gives it (min(20, 18 - 1) = 17).
        // D-058 (2026-10-19) CAUSE BLOCK: P2's clause is now build + holds = min(G_b, R), R the row's unreleased heads' need
        // (SplitReportCheck's P2 note: with a completion's leftover capacity carried to the next entry the walk can reach
        // fewer entries than the claim's), so its message names R; here R = 1 + 20 = 21 ≥ G_b and the verdict is unchanged.
        Assert.Equal(["P2: step 118->119 (polity 1) stone: building work drew 13 and set aside 0, less than min(its grant G_b = 18, " +
                      "its heads' need 21) = 18 (D_b = 21): the walk spends the whole grant its heads need",
                      "P12: step 118->119 (polity 1) stone: head s2:0 (enqueued 117) drew 12 and set aside 0, but the D-055 walk gives " +
                      "an unreleased head min(its need, what the heads ahead left) = min(20, 17) = 17"], s.Failures);
        // P4 still has its other binding steps (118 alone dropped out): the existence form never saw the under-spend.
        Assert.StartsWith("binding steps (P4): 117 stone; 119 stone; 121 stone", s.Findings[0]);

        // (b) Step 117->118 timber does not bind (G_b = D_b = 80: s0:0 draws 38 and sets aside 2, s2:0 sets aside 40). The
        // mutant sets aside 2 fewer — s2:0's set-aside 40 -> 38, construction's published (and forecast) holds 42 -> 40.
        SplitGoodFigures t117 = Row(a, 117).Goods.Single(g => g.Good == "timber");
        Assert.Equal((80L, 80L, 38L, 42L), (t117.Gb, t117.Db, t117.Build, t117.Holds));
        SplitReportRow[] notScarce = WithHeadGood(
            WithGood(a, 117, "timber", g => g with { Holds = g.Holds - 2, ForecastHolds = g.ForecastHolds - 2 }),
            117, 2, 0, "timber", g => g with { Hold = g.Hold - 2 });
        SplitCheckResult n = SplitReportCheck.Check(notScarce, Control(), 120);
        Assert.Equal(["P2: step 117->118 (polity 1) timber: building work drew 38 and set aside 40, less than min(its grant G_b = 80, " +
                      "its heads' need 80) = 80 (D_b = 80): the walk spends the whole grant its heads need",
                      "P12: step 117->118 (polity 1) timber: head s2:0 (enqueued 117) drew 0 and set aside 38, but the D-055 walk gives " +
                      "an unreleased head min(its need, what the heads ahead left) = min(40, 40) = 40"], n.Failures);
    }

    /// <summary>
    /// D-058 (2026-10-19) — P2 AS min(G_b, R). With a completion's leftover capacity carried to the queue's next entry in the
    /// same step, the walk under the grant can reach fewer entries than the claim's walk on S, so its heads may need less
    /// than the grant: the step then spends exactly R, their need, and that is not an under-spend. Step 117->118 timber (G_b =
    /// D_b = 80; s0:0 draws 38 and sets aside 2, s2:0 sets aside 40) doctored so s2:0 needs 5 less and sets aside 5 less
    /// (R = 75 &lt; G_b): P2 and P12 pass. The same row with the need left at 40 is the under-spend (b) above, and fails.
    /// </summary>
    [Fact]
    public void P2_AStepWhoseHeadsNeedLessThanTheGrant_SpendsTheirNeed_AndPasses()
    {
        SplitReportRow[] a = A();
        SplitReportRow[] lessNeed = WithHeadGood(
            WithGood(a, 117, "timber", g => g with { Holds = g.Holds - 5, ForecastHolds = g.ForecastHolds - 5 }),
            117, 2, 0, "timber", g => g with { Hold = g.Hold - 5, Need = g.Need - 5 });
        SplitCheckResult r = SplitReportCheck.Check(lessNeed, Control(), 120);
        Assert.False(r.Failed("P2"), string.Join("\n", r.Failures));
        Assert.False(r.Failed("P12"), string.Join("\n", r.Failures));
        // One unit short of that need is an under-spend again.
        SplitReportRow[] under = WithHeadGood(
            WithGood(a, 117, "timber", g => g with { Holds = g.Holds - 6, ForecastHolds = g.ForecastHolds - 6 }),
            117, 2, 0, "timber", g => g with { Hold = g.Hold - 6, Need = g.Need - 5 });
        AssertFails("P2", SplitReportCheck.Check(under, Control(), 120));
    }

    [Fact]
    public void P3_BuildingWorkThatLeftRoadWorksLessThanMinOfTheirDemandAndShare_Fails()
    {
        // Step 117->118 leaves road works exactly min(D_r, E_r) = 45 of 64 stone; one unit more of road share and it is short.
        SplitGoodFigures s = Stone(Row(A(), 117));
        Assert.Equal((64L, 19L, 0L, 63L, 45L), (s.S, s.Build, s.Holds, s.Dr, s.Er));
        SplitReportRow[] mutant = WithGood(A(), 117, "stone", g => g with { Er = 46, Eb = 18 });
        AssertFails("P3", SplitReportCheck.Check(mutant, Control(), 120));
        // The guard excuses it: a guarded step owes road works nothing.
        SplitReportRow[] guarded = mutant.Select(r => r.Turn == 117 ? r with { Guarded = true } : r).ToArray();
        Assert.False(SplitReportCheck.Check(guarded, Control(), 120).Failed("P3"));
    }

    [Fact]
    public void P4_AReportInWhichTheSplitNeverBinds_Fails()
    {
        SplitReportRow[] mutant = EveryGood(A(), "stone", (_, g) => g.Gb < g.Db, g => g with { Db = g.Gb, HoldsBack = false });
        AssertFails("P4", SplitReportCheck.Check(mutant, Control(), 120));
    }

    [Fact]
    public void P5_NoStepWhereBuildingWorkUsedRoadWorksUnusedShare_Fails()
    {
        SplitReportRow[] mutant = EveryGood(A(), "stone", (_, g) => g.Db > g.Eb && g.Dr < g.Er, g => g with { Dr = g.Er });
        AssertFails("P5", SplitReportCheck.Check(mutant, Control(), 120));
    }

    [Fact]
    public void P6_NoStepWhereRoadWorksUsedBuildingWorksUnusedShare_Fails()
    {
        SplitReportRow[] mutant = EveryGood(A(), "stone", (_, g) => g.Db < g.Eb && g.Road > g.Er, g => g with { Road = g.Er, ForecastRoad = g.Er });
        AssertFails("P6", SplitReportCheck.Check(mutant, Control(), 120));
    }

    [Fact]
    public void P7_NoBindingStepWithAHeadServedWhileAnotherSettlementsHeadWaited_Fails()
    {
        // Every head that waited is made to move: no head ever waits behind another in a binding step.
        SplitReportRow[] mutant = EveryHead(A(), (_, h) => h.After == h.Before ? h with { After = h.Before + 1 } : h);
        AssertFails("P7", SplitReportCheck.Check(mutant, Control(), 120));
        // A waiter of the SAME settlement does not count: give every waiting head the settlement of the head before it.
        SplitReportRow[] sameSettlement = A().Select(r => r with
        {
            Heads = r.Heads.Select((h, i) => i > 0 && h.After == h.Before ? h with { Settlement = r.Heads[i - 1].Settlement } : h).ToArray(),
        }).ToArray();
        Assert.True(SplitReportCheck.Check(sameSettlement, Control(), 120).Failed("P7"));
    }

    [Fact]
    public void P8_AControlThatDrewNoMoreAtTheFirstBindingStep_Fails()
    {
        // First binding step 117->118: the split run claims 19 stone of the 40 its heads need; the b = 100 control claims 40.
        Assert.Equal((19L, 0L), (Stone(Row(A(), 117)).Build, Stone(Row(A(), 117)).Holds));
        Assert.Equal((40L, 0L, 100), (Stone(Row(Control(), 117)).Build, Stone(Row(Control(), 117)).Holds, Row(Control(), 117).BuildingsPercent));
        SplitReportRow[] control = WithGood(Control(), 117, "stone", g => g with { Build = 19, Holds = 0 });
        AssertFails("P8", SplitReportCheck.Check(A(), control, 120));
        SplitReportRow[] noRow = Control().Where(r => r.Turn != 117).ToArray();
        Assert.True(SplitReportCheck.Check(A(), noRow, 120).Failed("P8"));
    }

    [Fact]
    public void P9_AControlThatSplitsOrDoesNotBranchFromTheReportsWorld_Fails()
    {
        SplitReportRow[] splits = Control().Select(r => r.Turn == 130 ? r with { BuildingsPercent = 30 } : r).ToArray();
        AssertFails("P9", SplitReportCheck.Check(A(), splits, 120));
        SplitReportRow[] elsewhere = WithGood(Control(), 116, "stone", g => g with { S = g.S + 1, Eb = g.Eb + 1 });
        AssertFails("P9", SplitReportCheck.Check(A(), elsewhere, 120));
        SplitReportRow[] later = Control().Where(r => r.Turn > 116).ToArray();
        Assert.True(SplitReportCheck.Check(A(), later, 120).Failed("P9"));
    }

    [Theory]
    [InlineData("no head is HeldForEarlier")]
    [InlineData("the holder is not ahead")]
    [InlineData("the holder is behind")]
    [InlineData("the holder sets none of it aside")]
    [InlineData("the draw did not fall")]
    [InlineData("labour, not materials, limited it")]
    public void P10_NoHeadWhoseDrawFellBecauseOfAnEarlierHeadsSetAside_Fails(string clause)
    {
        SplitReportRow[] a = A();
        SplitReportRow[] mutant = clause switch
        {
            "no head is HeldForEarlier" => EveryHeldGood(a, g => g with { Cause = "TakenByEarlier" }),
            "the holder is not ahead" => EveryHeldGood(a, g => g with { Holder = new SplitHeadRef(9, 0, 1) }),
            // The holder is a head of the row, but LATER in the building order than the head it is said to hold back.
            "the holder is behind" => a.Select(r => r with { Heads = r.Heads.Reverse().ToArray() }).ToArray(),
            "the holder sets none of it aside" => EveryHead(a, (r, h) => r.Heads.Any(x => x.Goods.Any(g => g.Holder is { } hd
                    && hd.Settlement == h.Settlement && hd.Slot == h.Slot))
                ? h with { Goods = h.Goods.Select(g => g with { Hold = 0 }).ToArray() } : h),
            "the draw did not fall" => EveryHeldGood(a, g => g with { Need = g.Draw }),
            "labour, not materials, limited it" => EveryHead(a, (_, h) =>
                h.Goods.Any(g => g.Cause == SplitReportCheck.HeldForEarlier) ? h with { Limit = "Labour" } : h),
            _ => throw new ArgumentOutOfRangeException(nameof(clause)),
        };
        AssertFails("P10", SplitReportCheck.Check(mutant));
        Assert.True(SplitReportCheck.Check(mutant, Control(), 120).Failed("PK"), "world 120's hold is gone with it");
    }

    [Fact]
    public void PK_ExpectHoldAt_NamesTheWorld_AStepWhoseSetAsideBindsNoYoungerHead_AndAMissingStep_Fail()
    {
        // World 120 (the step 119->120): s0:1 sets aside 19 stone and s2:0 is held for it.
        Assert.False(SplitReportCheck.Check(A(), null, 120).Failed("PK"));
        // World 121 (the step 120->121): s0:1 still sets aside 20, but no younger head was held for it (s2:0 completed).
        Assert.Equal(20L, Stone(Row(A(), 120)).Holds);
        SplitCheckResult at121 = SplitReportCheck.Check(A(), null, 121);
        Assert.True(at121.Failed("PK"));
        Assert.Contains("PK: world 121 holds no set-aside that binds a younger head", at121.Failures.Single(f => f.StartsWith("PK:", StringComparison.Ordinal)));
        // World 119 (the step 118->119): 6 timber set aside (s2:0's), but no head was held for it.
        Assert.Equal((0L, 6L), (Stone(Row(A(), 118)).Holds, Row(A(), 118).Goods.Single(g => g.Good == "timber").Holds));
        Assert.True(SplitReportCheck.Check(A(), null, 119).Failed("PK"));
        // The step must PUBLISH the set-aside: the same heads with the row's published holds zeroed do not satisfy PK.
        SplitReportRow[] unpublished = EveryGood(A(), "stone", (r, _) => r.Turn == 119, g => g with { Holds = 0, ForecastHolds = 0 });
        unpublished = EveryGood(unpublished, "timber", (r, _) => r.Turn == 119, g => g with { Holds = 0, ForecastHolds = 0 });
        Assert.True(SplitReportCheck.Check(unpublished, null, 120).Failed("PK"));
        Assert.Contains("PK: the report has no step that produced world 200",
            SplitReportCheck.Check(A(), null, 200).Failures.Single(f => f.StartsWith("PK:", StringComparison.Ordinal)));
    }

    // ================================================================== the file format

    [Fact]
    public void TheFixture_RoundTripsByteForByte_ThroughParseAndWrite()
    {
        foreach (string name in new[] { "a.jsonl", "control.jsonl" })
        {
            byte[] file = File.ReadAllBytes(FixturePath(name));
            using var ms = new MemoryStream();
            foreach (SplitReportRow row in SplitReport.Parse(File.ReadAllText(FixturePath(name)))) SplitReport.Write(ms, row);
            Assert.Equal(file, ms.ToArray());
        }
    }

    [Theory]
    [InlineData("schema", "split-report/v0", "schema \"split-report/v0\" is not \"split-report/v1\"")]
    [InlineData("to", "118", "\"to\" 118 is not turn 116 + 1")]
    [InlineData("missing", "polity", "split report line 1")]
    public void Parse_RefusesALineItCannotRead_NamingTheLine(string what, string value, string expected)
    {
        string first = File.ReadAllLines(FixturePath("a.jsonl"))[0];
        string doctored = what switch
        {
            "schema" => first.Replace("\"split-report/v1\"", "\"" + value + "\"", StringComparison.Ordinal),
            "to" => first.Replace("\"to\":117", "\"to\":" + value, StringComparison.Ordinal),
            _ => first.Replace("\"polity\":1,", "", StringComparison.Ordinal),
        };
        Assert.NotEqual(first, doctored);
        FormatException e = Assert.Throws<FormatException>(() => SplitReport.Parse(doctored + "\n"));
        Assert.Contains("split report line 1: ", e.Message);
        Assert.Contains(expected, e.Message);
    }

    /// <summary>
    /// T3 round 3 (finding T3-L2-T3V2-L2-F4) — WHAT THE PROBE COULD NOT HAVE WRITTEN IS REFUSED, NAMED: goods listed twice or
    /// out of id order, in a row or in a head (the probe writes each good once, ascending id — a duplicated stone at 117 used
    /// to pass every property, the checker reading only the first), and a missing or mistyped field, named with where it is
    /// (it used to read "The given key was not present in the dictionary."). Each is a FormatException naming the line —
    /// `sim split-check` exits 2 on it.
    /// </summary>
    [Theory]
    [InlineData("a good twice in a row", "split report line 2: goods[2] (stone, id 5) does not follow goods[1] (stone, id 5) — a row's goods are strictly ascending in id, each good once")]
    [InlineData("a row's goods descending", "split report line 2: goods[1] (stone, id 5) does not follow goods[0] (tools, id 12)")]
    [InlineData("a good twice in a head", "split report line 2: heads[0].goods[2] (stone, id 5) does not follow heads[0].goods[1] (stone, id 5) — a head's goods are strictly ascending in id, each good once")]
    [InlineData("a missing field", "split report line 1: goods[0]: missing field \"holds\"")]
    [InlineData("a mistyped field", "split report line 1: goods[0]: field \"start\" is not a whole number or null (\"a\")")]
    [InlineData("a head's missing field", "split report line 2: heads[1]: missing field \"limit\"")]
    [InlineData("a line with no schema", "split report line 25: missing field \"schema\"")]
    public void Parse_RefusesDuplicatedOrUnorderedGoods_AndNamesAMissingOrMistypedField(string what, string expected)
    {
        string[] lines = File.ReadAllLines(FixturePath("a.jsonl"));
        System.Text.Json.Nodes.JsonNode r117 = System.Text.Json.Nodes.JsonNode.Parse(lines[1])!;
        Assert.Equal(117, (int)r117["turn"]!);
        System.Text.Json.Nodes.JsonArray goods = r117["goods"]!.AsArray();
        Assert.Equal(["timber", "stone", "tools"], goods.Select(g => (string)g!["good"]!));
        switch (what)
        {
            case "a good twice in a row":
                goods.Insert(2, goods[1]!.DeepClone());
                lines[1] = r117.ToJsonString();
                break;
            case "a row's goods descending":
                System.Text.Json.Nodes.JsonNode[] reversed = goods.Reverse().Select(g => g!.DeepClone()).ToArray();
                goods.Clear();
                foreach (System.Text.Json.Nodes.JsonNode g in reversed) goods.Add(g);
                lines[1] = r117.ToJsonString();
                break;
            case "a good twice in a head":
                System.Text.Json.Nodes.JsonArray headGoods = r117["heads"]![0]!["goods"]!.AsArray();
                headGoods.Add(headGoods[^1]!.DeepClone());
                lines[1] = r117.ToJsonString();
                break;
            case "a missing field":
                lines[0] = lines[0].Replace("\"holds\":0,", "", StringComparison.Ordinal);
                break;
            case "a mistyped field":
                int at = lines[0].IndexOf("\"start\":", StringComparison.Ordinal);
                lines[0] = lines[0][..at] + "\"start\":\"a\",\"_\":" + lines[0][(at + "\"start\":".Length)..];
                break;
            case "a head's missing field":
                r117["heads"]![1]!.AsObject().Remove("limit");
                lines[1] = r117.ToJsonString();
                break;
            default:
                lines = [.. lines, "{}"];
                break;
        }
        FormatException e = Assert.Throws<FormatException>(() => SplitReport.Parse(string.Join("\n", lines) + "\n"));
        Assert.StartsWith(expected, e.Message);
    }

    [Fact]
    public void Parse_RefusesRowsOutOfOrderOrDuplicated()
    {
        string[] lines = File.ReadAllLines(FixturePath("a.jsonl"));
        FormatException swapped = Assert.Throws<FormatException>(() => SplitReport.Parse(lines[1] + "\n" + lines[0] + "\n"));
        Assert.Contains("split report line 2: (turn 116, polity 1) does not follow (turn 117, polity 1)", swapped.Message);
        Assert.Throws<FormatException>(() => SplitReport.Parse(lines[0] + "\n" + lines[0] + "\n"));
    }

    // ================================================================== the verb, as a script runs it

    private static readonly TimeSpan Limit = TimeSpan.FromMinutes(2);

    [Fact]
    public void SplitCheck_ExitsZeroOnTheRealPair_OneNamingEveryFailedProperty_AndTwoOnAnUnreadableReport()
    {
        (int exit, string stdout, string stderr) = CliProcess.Run(Limit, "split-check", FixturePath("a.jsonl"),
            "--control", FixturePath("control.jsonl"), "--expect-hold-at", "120");
        Assert.True(exit == 0, stdout + stderr);
        Assert.Equal("PASS", CliProcess.Lines(stdout)[^1]);
        Assert.Equal("", stderr);

        string dir = Path.Combine(Path.GetTempPath(), "civsim-splitcheck-" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(dir);
        try
        {
            string mutant = Path.Combine(dir, "mutant.jsonl");
            using (FileStream f = File.Create(mutant))
                foreach (SplitReportRow row in EveryHeldGood(A(), g => g with { Cause = "TakenByEarlier" })) SplitReport.Write(f, row);
            (exit, stdout, stderr) = CliProcess.Run(Limit, "split-check", mutant, "--expect-hold-at", "120");
            Assert.Equal(1, exit);
            Assert.Contains("FAIL P10: no head was held for an earlier head", stdout);
            Assert.Contains("FAIL PK: world 120 holds no set-aside that binds a younger head", stdout);
            Assert.Equal("split-check: FAILED - P10, PK", CliProcess.Lines(stderr).Single());

            string broken = Path.Combine(dir, "broken.jsonl");
            File.WriteAllText(broken, "{\"schema\":\"split-report/v1\"}\n");
            (exit, stdout, stderr) = CliProcess.Run(Limit, "split-check", broken);
            Assert.Equal((2, ""), (exit, stdout));
            Assert.StartsWith("error: split report line 1: ", CliProcess.Lines(stderr).Single());

            (exit, _, stderr) = CliProcess.Run(Limit, "split-check");
            Assert.Equal(1, exit);
            Assert.StartsWith("error: split-check takes a REPORT file", CliProcess.Lines(stderr)[0]);
        }
        finally
        {
            Directory.Delete(dir, recursive: true);
        }
    }
}
