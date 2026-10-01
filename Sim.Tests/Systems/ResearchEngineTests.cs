using Sim.Core.Kernel;
using Sim.Core.State;
using Sim.Core.Systems.Research;
using Sim.Tests.TestUtil;
using static Sim.Tests.TestUtil.ResearchRigs;

namespace Sim.Tests.Systems;

/// <summary>
/// ADR-029 engine semantics, one D-044 ruling per test group, on the
/// <see cref="ResearchRigs.Standard"/> graph. The world yields EXACTLY 1 000 RP per
/// turn at any dt: 10 000 people at the rig calibration (10 × P^0.5). A full Eureka is 40 %
/// of base cost (w's two Eurekas take 0.20 each: 500 of 2 500). Every cost is 2 500 unless
/// stated, so every expected progress value below is exact — there is no epsilon
/// anywhere (CLAUDE.md: exact equality).
/// Keys: a 1, b 2, c 3 (= a AND b; the stage), d 4 (= a OR b), e 5
/// (= (a AND c) OR (b AND d)), mil 6, med 7, eng 8, sci 9, agr 10, w 11 (Eurekas),
/// law 1001, code 1002 (Civics).
/// </summary>
public class ResearchEngineTests
{
    private static readonly ResearchContent Rig = Standard().Load();
    private static readonly PolityId P1 = new(Player);

    // ------------------------------------------------------------------ Research Points (ADR-030; D-045 §2 anchors)

    private static readonly ResearchTuning Canonical = TestConfigs.Research().Tuning;

    [Fact]
    public void ResearchPoints_TheAnchors_Population100Gives2_And1000Gives10_ToTolerance()
    {
        // 0.08 × P^0.699 — the published calibration values reproduce the Director's anchors to 0.1 %
        // (2.0003 and 10.002); the exponent is log10 5 rounded to three places.
        Assert.InRange(ResearchQuery.ResearchPoints(Canonical, 100), 2.0 * (1 - 1e-3), 2.0 * (1 + 1e-3));
        Assert.InRange(ResearchQuery.ResearchPoints(Canonical, 1_000), 10.0 * (1 - 1e-3), 10.0 * (1 + 1e-3));
        Assert.Equal(0.0, ResearchQuery.ResearchPoints(Canonical, 0));
        // The founded civilization of the cost unit's basis: 5,000 people ≈ 30.8 RP per turn.
        Assert.InRange(ResearchQuery.ResearchPoints(Canonical, 5_000), 30.75, 30.85);
    }

    [Fact]
    public void ResearchPoints_AreMonotone_AndSublinear_MoreResearchButLessPerPerson()
    {
        double previous = 0.0, previousPerPerson = double.MaxValue;
        foreach (double p in new double[] { 10, 100, 1_000, 5_000, 10_000, 100_000, 1_000_000, 108_000_000 })
        {
            double rp = ResearchQuery.ResearchPoints(Canonical, p);
            Assert.True(rp > previous, $"RP must grow with population ({p})");
            Assert.True(rp / p < previousPerPerson, $"RP per person must fall ({p})");
            Assert.True(ResearchQuery.ResearchPoints(Canonical, 10 * p) < 10 * rp, $"RP(10P) < 10·RP(P) at {p}");
            previous = rp;
            previousPerPerson = rp / p;
        }
    }

    [Fact]
    public void ResearchPointPool_IsTotalPopulationOfControlledSettlements_OnePoolPerPolity()
    {
        WorldState w = PlayerWorld();
        Assert.Equal(10_000, ResearchQuery.Population(w, P1));
        Assert.Equal(1000.0, ResearchQuery.ResearchPointPool(w, Rig, P1)); // 10 × 10 000^0.5

        // Two polities, three settlements: polity 1 controls 0 and 2, polity 2 controls 1. Children and
        // elders are population too: 7 000 of each added to settlement 0 count for polity 1 only.
        WorldState two = World([1, 2], [1, 2, 1], 2_500);
        var ledger = new Ledger(two.LedgerFlows);
        foreach (int cohort in new[] { 1, 13 })
        {
            int row = two.Buckets.Add(new BucketRow(new SettlementId(0), new CultureId(1), new ReligionId(1), new ClassId(1),
                cohort, Conserved.Zero, 0.0, 0.0, 0.0, 0.0));
            ledger.Flow(ref two.Buckets.Ref(row).Count, ConservedQuantityIds.Population,
                ReasonIds.InitialEndowment, 7_000, FlowDirection.Source, OverdrawPolicy.Throw);
        }
        Assert.Equal(19_000, ResearchQuery.Population(two, new PolityId(1)));
        Assert.Equal(2_500, ResearchQuery.Population(two, new PolityId(2)));
        Assert.Equal(500.0, ResearchQuery.ResearchPointPool(two, Rig, new PolityId(2))); // 10 × 2500^0.5

        // A polity with no settlement generates nothing (and Pow(0, e) is never asked).
        WorldState empty = World([1, 3], [1], 10_000);
        Assert.Equal(0.0, ResearchQuery.ResearchPointPool(empty, Rig, new PolityId(3)));
    }

    [Fact]
    public void ResearchPoints_ArePerTurn_IdenticalAcrossDifferentDtYears_TheAdr030Exception()
    {
        // ADR-030 §5: research is the ONE per-turn quantity. The same world and target at dt 10,
        // dt 5, dt 1 and dt 0.5 receive the same 1 000 RP — research alone is not dt-integrated.
        var orders = new OrderLog();
        orders.Append(Target(0, 1));
        foreach (double dt in new[] { 10.0, 5.0, 1.0, 0.5 })
            Assert.Equal(1000.0, Progress(Executor(Rig, orders, dt).Step(PlayerWorld()), 1));
    }

    [Fact]
    public void Founding_StartsWithZeroCompletedResearchNodes()
    {
        WorldState w = Sim.Core.Worldgen.WorldFounding.Found(TestConfigs.DevWorldgen(), TestConfigs.Sim(), 42);
        Assert.Equal(0, w.ResearchCompleted.Count);
        Assert.Equal(0, w.ResearchCredits.Count);
    }

    // ------------------------------------------------------------------ active selection (turn-exact)

    [Fact]
    public void Selection_AnOrderStampedT_RetargetsTheStepFromT_AndThatStepsRpGoesToIt()
    {
        // D-044 R9 + §3.9 delivery: stamped turn 0 -> world 1 already carries the target
        // AND 1000 RP on it. Stamped turn 1 -> world 1 has neither; world 2 does.
        var early = new OrderLog();
        early.Append(Target(0, 1));
        WorldState e1 = Executor(Rig, early).Step(PlayerWorld());
        Assert.True(ResearchQuery.TryGetTarget(e1, P1, out ResearchNodeId t) && t.Value == 1);
        Assert.Equal(1000.0, Progress(e1, 1));

        var late = new OrderLog();
        late.Append(Target(1, 1));
        TurnExecutor ex = Executor(Rig, late);
        WorldState l1 = ex.Step(PlayerWorld());
        Assert.False(ResearchQuery.TryGetTarget(l1, P1, out _));
        Assert.Equal(0.0, Progress(l1, 1));
        WorldState l2 = ex.Step(l1);
        Assert.True(ResearchQuery.TryGetTarget(l2, P1, out ResearchNodeId t2) && t2.Value == 1);
        Assert.Equal(1000.0, Progress(l2, 1));
    }

    [Fact]
    public void Selection_TheTargetPersistsUntilChanged_ADirectiveNotAPerTurnOrder()
    {
        var orders = new OrderLog();
        orders.Append(Target(0, 1));
        WorldState w = Run(Executor(Rig, orders), PlayerWorld(), 2);
        Assert.Equal(2000.0, Progress(w, 1)); // no re-issue needed (D-042 §6.2)
    }

    [Fact]
    public void Selection_InvalidOrdersChangeNothing_TheLastValidOrderWins()
    {
        var orders = new OrderLog();
        orders.Append(Target(0, 1));        // valid: a
        orders.Append(Target(0, 3));        // c is LOCKED (needs a AND b): ignored
        orders.Append(Target(0, 999));      // no such key: ignored
        orders.Append(Target(0, 6));        // mil: subtree closed before the stage: ignored
        orders.Append(Target(0, 2, polity: 9)); // polity 9 is not registered: ignored
        WorldState w = Executor(Rig, orders).Step(PlayerWorld());
        Assert.True(ResearchQuery.TryGetTarget(w, P1, out ResearchNodeId t) && t.Value == 1);
        Assert.Equal(1000.0, Progress(w, 1));
        Assert.Single(w.ResearchProgress.ToArrayForTest());

        var cleared = new OrderLog();
        cleared.Append(Target(0, 1));
        cleared.Append(OrderRecord.From(0, P1, OrderKind.SetResearchTarget, -1, 0.0));
        WorldState c = Executor(Rig, cleared).Step(PlayerWorld());
        Assert.False(ResearchQuery.TryGetTarget(c, P1, out _));
        Assert.Equal(0, c.ResearchProgress.Count);
    }

    [Fact]
    public void Selection_AnUnregisteredActorsOrder_ChangesNothingAtAll()
    {
        // ADR-029 §4: the actor must be a registered polity, otherwise the order changes NOTHING —
        // asserted from the whole state, not only from the registered polity's view.
        var orders = new OrderLog();
        orders.Append(Target(0, 2, polity: 9)); // b is available to anyone; polity 9 is not on the roster
        WorldState w = Executor(Rig, orders).Step(PlayerWorld());
        Assert.Equal(0, w.ResearchTargets.Count);
        Assert.Equal(0, w.ResearchProgress.Count);
        Assert.Equal(0, w.ResearchCompleted.Count);
        Assert.Equal(0, w.ResearchEurekas.Count);
    }

    [Fact]
    public void Roster_ADuplicatedPolityRow_CountsOnce()
    {
        // ADR-029 §5: a doubled roster row is one Empire — one turn of RP, one Eureka credit.
        WorldState w = PlayerWorld();
        w.Polities.Add(new PolityRow(P1, CommandSource.Player));
        var orders = new OrderLog();
        orders.Append(Target(0, 1));
        WorldState w1 = Executor(Rig, orders).Step(w);
        Assert.Equal(1000.0, Progress(w1, 1)); // not 2000
        Assert.Single(w1.ResearchTargets.ToArrayForTest());

        WorldState e = WithCompleted(PlayerWorld([(4, 50)]), 1);
        e.Polities.Add(new PolityRow(P1, CommandSource.Player));
        WorldState e1 = Executor(Rig).Step(e);
        Assert.Equal(500.0, Progress(e1, 11)); // not 1000
        Assert.Single(e1.ResearchEurekas.ToArrayForTest());
    }

    // ------------------------------------------------------------------ no bank / partial progress / switching

    [Fact]
    public void NoTarget_RpReachesNoNode_AndNothingIsBanked()
    {
        // D-044 R20-D: three idle turns, then a target. The target receives ONE turn of RP,
        // not four — nothing accumulated while idle.
        var orders = new OrderLog();
        orders.Append(Target(3, 1));
        TurnExecutor ex = Executor(Rig, orders);
        WorldState w = Run(ex, PlayerWorld(), 3);
        Assert.Equal(0, w.ResearchProgress.Count);
        Assert.Equal(0, w.ResearchTargets.Count);
        w = ex.Step(w);
        Assert.Equal(1000.0, Progress(w, 1));
    }

    [Fact]
    public void Switching_KeepsPartialProgressOnEveryNode_TheSixtyOfAHundredRule()
    {
        // D-044 R9's own example, scaled: A reaches 1000 / 2500, the player switches to B,
        // A stays 1000; switching back resumes A from 1000.
        var orders = new OrderLog();
        orders.Append(Target(0, 1)); // a
        orders.Append(Target(1, 2)); // b
        orders.Append(Target(2, 1)); // back to a
        TurnExecutor ex = Executor(Rig, orders);
        WorldState w1 = ex.Step(PlayerWorld());
        WorldState w2 = ex.Step(w1);
        Assert.Equal(1000.0, Progress(w2, 1));
        Assert.Equal(1000.0, Progress(w2, 2));
        Assert.Equal(2, w2.ResearchProgress.Count); // many nodes may hold partial progress
        WorldState w3 = ex.Step(w2);
        Assert.Equal(2000.0, Progress(w3, 1));
        Assert.Equal(1000.0, Progress(w3, 2));
    }

    // ------------------------------------------------------------------ completion

    [Fact]
    public void Completion_AtEffectiveCost_IsImmediate_ClearsTheTarget_AndLosesTheOverflow()
    {
        var orders = new OrderLog();
        orders.Append(Target(0, 1));
        TurnExecutor ex = Executor(Rig, orders);
        WorldState w = Run(ex, PlayerWorld(), 2);
        Assert.Equal(2000.0, Progress(w, 1));
        Assert.False(Done(w, 1));
        w = ex.Step(w); // 1000 more, 500 needed: completes, 500 reaches nothing
        Assert.True(Done(w, 1));
        Assert.Equal(0, w.ResearchProgress.Count); // completion is the fact; no progress row left
        Assert.False(ResearchQuery.TryGetTarget(w, P1, out _));
        Assert.Equal(0.0, Progress(w, 2)); // the overflow went nowhere (no carry, no bank)
        Assert.Equal([Key(1)], ResearchQuery.CompletedBetween(Run(ex, PlayerWorld(), 2), w, P1));
    }

    [Fact]
    public void Completion_ReachingTheCost_SetsProgressToExactlyTheCost_NeverOneUlpShort()
    {
        // ADR-029 §5 step 4. cost - have is exactly 1000.0, so this turn's 1 000 RP covers the
        // remaining cost — but have + 1000 rounds to 1017.6696619502791, one ulp BELOW the cost.
        // Without the snap the node would stay one ulp short and completion would slip a turn.
        Spec spec = Standard();
        spec.Technologies[1] = spec.Technologies[1] with { Cost = 1017.6696619502792 };
        ResearchContent content = spec.Load();
        WorldState w = PlayerWorld();
        w.ResearchProgress.Add(new ResearchProgressRow(P1, Key(2), 17.669661950279135));
        Assert.True(17.669661950279135 + 1000.0 < 1017.6696619502792); // the premise: plain addition falls short
        var orders = new OrderLog();
        orders.Append(Target(0, 2));
        WorldState w1 = Executor(content, orders).Step(w);
        Assert.True(Done(w1, 2));
        Assert.Equal(0, w1.ResearchProgress.Count);
        Assert.False(ResearchQuery.TryGetTarget(w1, P1, out _));
    }

    [Fact]
    public void Completion_IsIdempotent_ACompletedNodeIsNeverAddedTwice_NorTargetable()
    {
        WorldState start = WithCompleted(PlayerWorld(), 1);
        // A stray progress row on the completed node (a hand-edited save) must not re-complete it.
        start.ResearchProgress.Add(new ResearchProgressRow(P1, Key(1), 99_999.0));
        var orders = new OrderLog();
        orders.Append(Target(0, 1)); // targeting a completed node is ignored
        WorldState w = Run(Executor(Rig, orders), start, 3);
        int rows = 0;
        for (int i = 0; i < w.ResearchCompleted.Count; i++) if (w.ResearchCompleted[i].Node.Value == 1) rows++;
        Assert.Equal(1, rows);
        Assert.False(ResearchQuery.TryGetTarget(w, P1, out _));
    }

    [Fact]
    public void Completion_KnowledgeSurvivesTheLossOfEverySettlement()
    {
        // D-044 R1: individuals do not own knowledge; losing the people does not lose it.
        WorldState w = WithCompleted(PlayerWorld(), 1, 2);
        w.Controls.Clear();
        w = Run(Executor(Rig), w, 2);
        Assert.True(Done(w, 1) && Done(w, 2));
        Assert.Equal(0.0, ResearchQuery.ResearchPointPool(w, Rig, P1));
    }

    // ------------------------------------------------------------------ prerequisites: AND / OR / nested

    [Fact]
    public void Prerequisites_And_BothRequired()
    {
        bool[] none = ResearchQuery.CompletedMask(PlayerWorld(), Rig, P1);
        int c = Rig.IndexOf(Key(3));
        Assert.False(ResearchQuery.PrerequisitesMet(Rig, c, none));
        Assert.False(ResearchQuery.PrerequisitesMet(Rig, c, ResearchQuery.CompletedMask(WithCompleted(PlayerWorld(), 1), Rig, P1)));
        Assert.True(ResearchQuery.PrerequisitesMet(Rig, c, ResearchQuery.CompletedMask(WithCompleted(PlayerWorld(), 1, 2), Rig, P1)));
    }

    [Fact]
    public void Prerequisites_Or_EitherSuffices()
    {
        int d = Rig.IndexOf(Key(4));
        Assert.False(ResearchQuery.PrerequisitesMet(Rig, d, ResearchQuery.CompletedMask(PlayerWorld(), Rig, P1)));
        Assert.True(ResearchQuery.PrerequisitesMet(Rig, d, ResearchQuery.CompletedMask(WithCompleted(PlayerWorld(), 1), Rig, P1)));
        Assert.True(ResearchQuery.PrerequisitesMet(Rig, d, ResearchQuery.CompletedMask(WithCompleted(PlayerWorld(), 2), Rig, P1)));
    }

    [Fact]
    public void Prerequisites_Nested_AAndC_Or_BAndD()
    {
        int e = Rig.IndexOf(Key(5));
        bool Met(params int[] keys) => ResearchQuery.PrerequisitesMet(
            Rig, e, ResearchQuery.CompletedMask(WithCompleted(PlayerWorld(), keys), Rig, P1));
        Assert.False(Met(1));          // a alone
        Assert.False(Met(1, 4));       // a AND d: crosses the two alternatives
        Assert.False(Met(2, 3));       // b AND c: crosses them the other way
        Assert.True(Met(1, 3));        // a AND c
        Assert.True(Met(2, 4));        // b AND d
    }

    [Fact]
    public void Prerequisites_GateTheOrderPathway_DependentsOpenTheStepAfterCompletion()
    {
        // c needs a AND b. Complete a (3 turns), b (3 turns); an order for c on the turn
        // b completes is ignored (availability reads PREV), the next turn's order lands.
        var orders = new OrderLog();
        orders.Append(Target(0, 1));
        orders.Append(Target(3, 2));
        orders.Append(Target(5, 3)); // step 5->6 completes b; c not yet available on PREV(5)
        orders.Append(Target(6, 3)); // PREV(6) has b: lands
        TurnExecutor ex = Executor(Rig, orders);
        WorldState w = Run(ex, PlayerWorld(), 6);
        Assert.True(Done(w, 1) && Done(w, 2));
        Assert.Equal(0.0, Progress(w, 3));
        Assert.False(ResearchQuery.TryGetTarget(w, P1, out _));
        w = ex.Step(w);
        Assert.Equal(1000.0, Progress(w, 3));
    }

    // ------------------------------------------------------------------ subtrees

    [Fact]
    public void Subtrees_AreClosedBeforeTheStage_AndAllFiveOpenTogetherWhenItIsReached()
    {
        WorldState before = WithCompleted(PlayerWorld(), 1, 2); // a, b: c (the stage) not yet
        for (int b = 0; b < 5; b++)
        {
            Assert.False(ResearchQuery.IsBranchOpen(before, Rig, P1, b));
            Assert.Empty(ResearchQuery.AvailableNodes(before, Rig, P1, ResearchTree.Technology, b));
        }
        Assert.True(ResearchQuery.IsBranchOpen(before, Rig, P1, -1)); // the Main tree is always open

        WorldState after = WithCompleted(PlayerWorld(), 1, 2, 3);
        Assert.True(ResearchQuery.IsStageReached(after, Rig, P1));
        int[] entries = [6, 7, 8, 9, 10]; // mil, med, eng, sci, agr — one per subtree, in 1.1..1.5 order
        for (int b = 0; b < 5; b++)
        {
            Assert.True(ResearchQuery.IsBranchOpen(after, Rig, P1, b));
            Assert.Equal([Key(entries[b])], ResearchQuery.AvailableNodes(after, Rig, P1, ResearchTree.Technology, b));
        }
    }

    [Fact]
    public void Subtrees_AllFive_ResearchOnTheSameRpPool()
    {
        // After the stage, one node in each of the five subtrees is completed in turn, each
        // taking exactly 3 turns of the one pool (2500 / 1000 per turn).
        WorldState w = WithCompleted(PlayerWorld(), 1, 2, 3);
        var orders = new OrderLog();
        int[] entries = [6, 7, 8, 9, 10];
        for (int k = 0; k < entries.Length; k++) orders.Append(Target(3 * k, entries[k]));
        w = Run(Executor(Rig, orders), w, 15);
        foreach (int key in entries) Assert.True(Done(w, key));
        string[] branches = new string[5];
        for (int k = 0; k < 5; k++) branches[k] = Rig.Branches[Rig.Nodes[Rig.IndexOf(Key(entries[k]))].Branch].Id;
        Assert.Equal(ResearchContentLoader.RuledBranchIds, branches);
    }

    // ------------------------------------------------------------------ Eureka

    [Fact]
    public void Eureka_FiresOnce_CreditsItsOwnNode_ItsWeightTimesBaseCost()
    {
        // w (key 11) is available once a is known; timber (good 4) is stocked.
        WorldState w = WithCompleted(PlayerWorld([(4, 50)]), 1);
        TurnExecutor ex = Executor(Rig);
        WorldState w1 = ex.Step(w);
        Assert.Equal(500.0, Progress(w1, 11)); // weight 0.4 / 2 x BaseCost 2500 (w has two equally weighted Eurekas)
        Assert.True(ResearchQuery.EurekaFired(w1, P1, Key(11), 0));
        WorldState w2 = ex.Step(w1); // the condition still holds, but a fired Eureka never fires again
        Assert.Equal(500.0, Progress(w2, 11));
        Assert.Single(w2.ResearchEurekas.ToArrayForTest());
    }

    [Fact]
    public void Eureka_OnAnInactiveTechnology_AcceleratesIt_WhileTheTargetKeepsItsRp()
    {
        WorldState w = WithCompleted(PlayerWorld([(4, 50)]), 1);
        var orders = new OrderLog();
        orders.Append(Target(0, 2)); // target b; the Eureka is on w
        WorldState w1 = Executor(Rig, orders).Step(w);
        Assert.Equal(1000.0, Progress(w1, 2));
        Assert.Equal(500.0, Progress(w1, 11));
    }

    [Fact]
    public void Eureka_IsCappedAtTheRemainingCost_AndNeverOverflows()
    {
        // w already holds 2400 of 2500: the timber condition is worth 500 but only 100 remain.
        WorldState w = WithCompleted(PlayerWorld([(4, 50)]), 1);
        w.ResearchProgress.Add(new ResearchProgressRow(P1, Key(11), 2400.0));
        WorldState w1 = Executor(Rig).Step(w);
        Assert.True(Done(w1, 11));
        Assert.Equal(0, w1.ResearchProgress.Count); // nothing spilled anywhere
    }

    [Fact]
    public void Eureka_OnlyForAvailableNodes_ANodeAtomCondition_AndAnUnmetGoodCondition()
    {
        // No timber, a not yet complete: w is locked, so neither Eureka is even asked.
        WorldState locked = PlayerWorld();
        WorldState l1 = Executor(Rig).Step(locked);
        Assert.Equal(0, l1.ResearchEurekas.Count);

        // a and c complete, no timber: w is available; Eureka 1 ("c") fires, Eureka 0 (timber) does not.
        WorldState w = WithCompleted(PlayerWorld(), 1, 2, 3);
        WorldState w1 = Executor(Rig).Step(w);
        Assert.False(ResearchQuery.EurekaFired(w1, P1, Key(11), 0));
        Assert.True(ResearchQuery.EurekaFired(w1, P1, Key(11), 1));
        Assert.Equal(500.0, Progress(w1, 11));

        ResearchQuery.EurekaState[] states = ResearchQuery.Eurekas(w1, Rig, P1, Key(11));
        Assert.False(states[0].Fired);
        Assert.True(states[1].Fired);
        Assert.True(states[0].HoldsNow == false);
    }

    [Fact]
    public void Eureka_SettlementScopedConditions_HoldIfAnyControlledSettlementHoldsThem()
    {
        // Timber only in settlement 0; polity 2 controls settlement 0, polity 1 controls 1.
        WorldState w = World([1, 2], [2, 1], 10_000, [(4, 50)]);
        foreach (int p in new[] { 1, 2 }) w.ResearchCompleted.Add(new ResearchCompletedRow(new PolityId(p), Key(1)));
        WorldState w1 = Executor(Rig).Step(w);
        Assert.Equal(500.0, Progress(w1, 11, polity: 2));
        Assert.Equal(0.0, Progress(w1, 11, polity: 1));
    }

    [Fact]
    public void Eureka_APolityWithSeveralSettlements_FiresWhenAnyOneOfThemHoldsTheGood()
    {
        // ANY, not ALL: the player controls both settlements, and only settlement 0 holds timber.
        WorldState first = WithCompleted(World([Player], [Player, Player], 10_000, [(4, 50)]), 1);
        WorldState f1 = Executor(Rig).Step(first);
        Assert.Equal(500.0, Progress(f1, 11));
        Assert.True(ResearchQuery.EurekaFired(f1, P1, Key(11), 0));

        // ...and not "the first controlled settlement": here only settlement 1 holds timber.
        WorldState second = WithCompleted(Stock(World([Player], [Player, Player], 10_000), 1, 4, 50), 1);
        WorldState s1 = Executor(Rig).Step(second);
        Assert.Equal(500.0, Progress(s1, 11));
        Assert.True(ResearchQuery.EurekaFired(s1, P1, Key(11), 0));
        Assert.True(ResearchQuery.EurekaHolds(s1, Rig, P1, Rig.Nodes[Rig.IndexOfId("w")].Eurekas[0].Condition!,
            ResearchQuery.CompletedMask(s1, Rig, P1)));
    }

    // ------------------------------------------------------------------ specialized universities

    [Fact]
    public void University_ReducesTheEffectiveCostOfItsOwnSubtreeOnly()
    {
        WorldState w = WithCompleted(PlayerWorld(), 1, 2, 3);
        w.ResearchCostModifiers.Add(new ResearchCostModifierRow(P1, UniversityType: 2, Factor: 0.5)); // medical
        int med = Rig.IndexOf(Key(7)), eng = Rig.IndexOf(Key(8)), trunk = Rig.IndexOf(Key(4)), law = Rig.IndexOf(Key(1001));
        Assert.Equal(1250.0, ResearchQuery.EffectiveCost(w, Rig, P1, med));
        Assert.Equal(2500.0, ResearchQuery.EffectiveCost(w, Rig, P1, eng));
        Assert.Equal(2500.0, ResearchQuery.EffectiveCost(w, Rig, P1, trunk));
        Assert.Equal(2500.0, ResearchQuery.EffectiveCost(w, Rig, P1, law));

        ResearchQuery.CostBreakdown b = ResearchQuery.EffectiveCostBreakdown(w, Rig, P1, Key(7));
        Assert.Equal(2500.0, b.BaseCost);
        Assert.Equal("medical_university", Assert.Single(b.Terms).UniversityId);
        Assert.Equal(1250.0, b.EffectiveCost);

        // The engine completes at the REDUCED cost: two turns (1000 + 250), not three.
        var orders = new OrderLog();
        orders.Append(Target(0, 7));
        WorldState done = Run(Executor(Rig, orders), w, 2);
        Assert.True(Done(done, 7));
        // Another polity's university does nothing for this one.
        WorldState other = WithCompleted(PlayerWorld(), 1, 2, 3);
        other.ResearchCostModifiers.Add(new ResearchCostModifierRow(new PolityId(7), 2, 0.5));
        Assert.Equal(2500.0, ResearchQuery.EffectiveCost(other, Rig, P1, med));
    }

    [Fact]
    public void University_MultipleTypes_EachActsOnItsSubtree_AndSameTypeRowsMultiplyInTableOrder()
    {
        WorldState w = WithCompleted(PlayerWorld(), 1, 2, 3);
        w.ResearchCostModifiers.Add(new ResearchCostModifierRow(P1, 2, 0.5));  // medical
        w.ResearchCostModifiers.Add(new ResearchCostModifierRow(P1, 3, 0.8));  // engineering
        w.ResearchCostModifiers.Add(new ResearchCostModifierRow(P1, 2, 0.5));  // a second medical term
        Assert.Equal(625.0, ResearchQuery.EffectiveCost(w, Rig, P1, Rig.IndexOf(Key(7))));   // 2500 x 0.5 x 0.5
        Assert.Equal(2000.0, ResearchQuery.EffectiveCost(w, Rig, P1, Rig.IndexOf(Key(8))));  // 2500 x 0.8
        Assert.Equal(2500.0, ResearchQuery.EffectiveCost(w, Rig, P1, Rig.IndexOf(Key(6))));  // military: none
        ResearchQuery.CostTerm[] all = ResearchQuery.CostModifiers(w, Rig, P1);
        Assert.Equal([0, 1, 2], [all[0].ModifierRow, all[1].ModifierRow, all[2].ModifierRow]);
        Assert.Equal(2, ResearchQuery.EffectiveCostBreakdown(w, Rig, P1, Key(7)).Terms.Length);
    }

    [Theory]
    [InlineData(2, 0.0)]
    [InlineData(2, 1.5)]
    [InlineData(2, double.NaN)]
    [InlineData(99, 0.5)]
    public void University_AModifierOutsideTheContract_IsRefusedLoudly(int type, double factor)
    {
        WorldState w = WithCompleted(PlayerWorld(), 1, 2, 3);
        w.ResearchCostModifiers.Add(new ResearchCostModifierRow(P1, type, factor));
        var e = Assert.Throws<InvalidOperationException>(() => ResearchQuery.EffectiveCost(w, Rig, P1, Rig.IndexOf(Key(7))));
        Assert.Contains("ResearchCostModifiers[0]", e.Message);
    }

    // ------------------------------------------------------------------ Civics on the same pool

    [Fact]
    public void Civics_ResearchesWithProgressCompletionAndBranching()
    {
        WorldState w = WithCompleted(PlayerWorld(), 1);
        Assert.Equal([Key(1001)], ResearchQuery.AvailableNodes(w, Rig, P1, ResearchTree.Civics));
        var orders = new OrderLog();
        orders.Append(Target(0, 1001));
        w = Run(Executor(Rig, orders), w, 3);
        Assert.True(Done(w, 1001));
        Assert.Equal([Key(1001)], ResearchQuery.CompletedNodes(w, Rig, P1, ResearchTree.Civics));
        // code needs law AND c — a cross-tree prerequisite; available only once c is known too.
        Assert.Empty(ResearchQuery.AvailableNodes(w, Rig, P1, ResearchTree.Civics));
        w = WithCompleted(w, 2, 3);
        Assert.Equal([Key(1002)], ResearchQuery.AvailableNodes(w, Rig, P1, ResearchTree.Civics));
    }

    [Fact]
    public void SharedPool_OneTargetAcrossBothTrees_TotalInvestedEqualsTotalRp()
    {
        // Alternating Technology and Civics targets draw on ONE pool: after four turns
        // exactly 4000 RP is invested in total, never 4000 per tree.
        WorldState w = WithCompleted(PlayerWorld(), 1);
        var orders = new OrderLog();
        orders.Append(Target(0, 2));    // tech b
        orders.Append(Target(1, 1001)); // civic law replaces it — one target, not one per tree
        orders.Append(Target(2, 2));
        orders.Append(Target(3, 1001));
        TurnExecutor ex = Executor(Rig, orders);
        WorldState w2 = Run(ex, w, 2);
        Assert.True(ResearchQuery.TryGetTarget(w2, P1, out ResearchNodeId t) && t.Value == 1001);
        Assert.Single(w2.ResearchTargets.ToArrayForTest());
        WorldState w4 = Run(ex, w2, 2);
        Assert.Equal(2000.0, Progress(w4, 2));
        Assert.Equal(2000.0, Progress(w4, 1001));
        double total = 0.0;
        for (int i = 0; i < w4.ResearchProgress.Count; i++) total += w4.ResearchProgress[i].Progress;
        Assert.Equal(4000.0, total);
    }

    // ------------------------------------------------------------------ polities, enumeration, eligibility

    [Fact]
    public void Polities_ResearchIndependently_EachOnItsOwnPool()
    {
        WorldState w = World([1, 2], [1, 2], 10_000);
        var orders = new OrderLog();
        orders.Append(Target(0, 1, polity: 1));
        orders.Append(Target(0, 2, polity: 2));
        w = Run(Executor(Rig, orders), w, 3);
        Assert.True(Done(w, 1, polity: 1) && !Done(w, 2, polity: 1));
        Assert.True(Done(w, 2, polity: 2) && !Done(w, 1, polity: 2));
    }

    [Fact]
    public void Enumeration_IsKeyOrdered_AndIndependentOfRowInsertionOrder()
    {
        WorldState fwd = WithCompleted(PlayerWorld(), 1, 2, 3, 1001);
        WorldState rev = WithCompleted(PlayerWorld(), 1001, 3, 2, 1);
        ResearchNodeId[] a = ResearchQuery.AvailableNodes(fwd, Rig, P1);
        Assert.Equal(a, ResearchQuery.AvailableNodes(rev, Rig, P1));
        for (int i = 1; i < a.Length; i++) Assert.True(a[i - 1].Value < a[i].Value);
        Assert.Equal(ResearchQuery.CompletedNodes(fwd, Rig, P1), ResearchQuery.CompletedNodes(rev, Rig, P1));
        Assert.Equal([Key(1), Key(2), Key(3), Key(1001)], ResearchQuery.CompletedNodes(rev, Rig, P1));
    }

    [Fact]
    public void CheapestAvailable_TieDense_LowestKeyWins_AndACheaperNodeBeatsIt()
    {
        // Every available node costs 2500 (a bit-exact tie): the lowest key wins.
        WorldState w = WithCompleted(PlayerWorld(), 1, 2, 3);
        Assert.Equal(Key(4), ResearchQuery.CheapestAvailable(w, Rig, P1));
        // Remove the winner: the next-lowest key among the tied nodes wins.
        Assert.Equal(Key(5), ResearchQuery.CheapestAvailable(WithCompleted(w.Clone(), 4), Rig, P1));
        // A university halves med (key 7): strictly cheaper beats every lower key.
        w.ResearchCostModifiers.Add(new ResearchCostModifierRow(P1, 2, 0.5));
        Assert.Equal(Key(7), ResearchQuery.CheapestAvailable(w, Rig, P1));
        // A tree filter keeps the same composite key within the tree.
        Assert.Equal(Key(1002), ResearchQuery.CheapestAvailable(WithCompleted(w.Clone(), 1001), Rig, P1, ResearchTree.Civics));
        Assert.Null(ResearchQuery.CheapestAvailable(PlayerWorld(), Rig, new PolityId(42), ResearchTree.Civics));
    }

    [Fact]
    public void KnowledgeEligibility_FollowsTheRequirement_AndUnlocksNothingPhysical()
    {
        int hall = Rig.EntityIndexOf("building.hall");
        Assert.False(ResearchQuery.IsKnowledgeEligible(Rig, hall, ResearchQuery.CompletedMask(WithCompleted(PlayerWorld(), 1, 2), Rig, P1)));
        WorldState w = WithCompleted(PlayerWorld(), 1, 2, 3);
        Assert.True(ResearchQuery.IsKnowledgeEligible(Rig, hall, ResearchQuery.CompletedMask(w, Rig, P1)));
        Assert.Equal(["building.hall"], ResearchQuery.KnowledgeEligibleEntities(w, Rig, P1));
        // D-044 R14: completion builds nothing — no structure or queue row appears.
        WorldState after = Run(Executor(Rig), w, 2);
        Assert.Equal(0, after.Structures.Count);
        Assert.Equal(0, after.ConstructionQueue.Count);
        Assert.Contains("c capability", ResearchQuery.UnlockedCapabilities(w, Rig, P1));
    }

    [Fact]
    public void NoResearchContent_TheSystemIsInert()
    {
        var orders = new OrderLog();
        orders.Append(Target(0, 1));
        WorldState start = PlayerWorld();
        WorldState w = Executor(null, orders).Step(start);
        Assert.Equal(0, w.ResearchTargets.Count + w.ResearchProgress.Count + w.ResearchCompleted.Count + w.ResearchEurekas.Count);
    }

    [Fact]
    public void GlassBoxQueries_MutateNothing()
    {
        WorldState w = WithCompleted(PlayerWorld([(4, 50)]), 1, 2, 3);
        w.ResearchProgress.Add(new ResearchProgressRow(P1, Key(4), 10.0));
        w.ResearchCostModifiers.Add(new ResearchCostModifierRow(P1, 2, 0.5));
        string before = WorldHash.ComputeHex(w);
        _ = ResearchQuery.AvailableNodes(w, Rig, P1);
        _ = ResearchQuery.CompletedNodes(w, Rig, P1);
        _ = ResearchQuery.PartialProgress(w, P1);
        _ = ResearchQuery.EffectiveCostBreakdown(w, Rig, P1, Key(7));
        _ = ResearchQuery.CostModifiers(w, Rig, P1);
        _ = ResearchQuery.Prerequisites(w, Rig, P1, Key(5));
        _ = ResearchQuery.Eurekas(w, Rig, P1, Key(11));
        _ = ResearchQuery.ResearchPointPool(w, Rig, P1);
        _ = ResearchQuery.AccelerationPoolOf(w, Rig, P1, Key(11));
        _ = ResearchQuery.EurekaProgressOf(w, Rig, P1, Key(11));
        _ = ResearchQuery.KnowledgeEligibleEntities(w, Rig, P1);
        _ = ResearchQuery.UnlockedCapabilities(w, Rig, P1);
        _ = ResearchQuery.CheapestAvailable(w, Rig, P1);
        Assert.Equal(before, WorldHash.ComputeHex(w));
    }
}

internal static class ResearchTableTestExtensions
{
    public static T[] ToArrayForTest<T>(this Table<T> table) where T : unmanaged
    {
        var result = new T[table.Count];
        for (int i = 0; i < table.Count; i++) result[i] = table[i];
        return result;
    }
}
