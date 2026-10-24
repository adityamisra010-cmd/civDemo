using Sim.Core;
using Sim.Core.Kernel;
using Sim.Core.State;
using Sim.Core.Systems;
using Sim.Core.Systems.Research;
using Sim.Core.Worldgen;
using Sim.Tests.TestUtil;
using static Sim.Tests.TestUtil.ResearchRigs;

namespace Sim.Tests.Systems;

/// <summary>
/// ADR-048 — DESTINATION RESEARCH (tranche-2 contract §3): SetResearchTarget names any incomplete node; the target row is
/// the persistent destination; each step funds the planner's CURRENT node; ResearchQuery.Route is the one route
/// calculator. Purpose-built rigs (adversarial OR, AND, shared prerequisites, the exact/greedy bound, the sweep, two lanes),
/// then the engine turn by turn: automatic advancement, retargeting, cancel, save/load, replay and the turn-exact pin.
/// </summary>
public class ResearchDestinationTests
{
    private static readonly PolityId P = new(Player);

    // ------------------------------------------------------------------ the route rig (one pool, routing on, no sweep)

    private static Spec RouteSpec(bool routing = true)
    {
        var s = new Spec { Stage = "root", DestinationRouting = routing };
        s.Technologies.AddRange([
            new Node(1, "root", Cost: 1000.0),
            new Node(2, "m1", "root", Cost: 1500.0),
            new Node(3, "d1", "m1", Cost: 1000.0),                            // one missing prerequisite
            new Node(4, "and1", "root", Cost: 2000.0), new Node(5, "and2", "root", Cost: 1000.0),
            new Node(6, "d2", "and1 AND and2", Cost: 500.0),                  // several AND prerequisites
            new Node(7, "heavy", null, Cost: 5000.0), new Node(8, "imm_a", "heavy", Cost: 100.0),
            new Node(9, "imm_b", null, Cost: 2000.0),
            new Node(10, "d3", "imm_a OR imm_b", Cost: 500.0),                // adversarial OR: cheapest prerequisite ≠ cheapest route
            new Node(11, "shared", null, Cost: 3000.0), new Node(12, "sa", "shared", Cost: 200.0),
            new Node(13, "sb", "shared", Cost: 200.0), new Node(14, "alt", null, Cost: 4000.0),
            new Node(15, "d4", "(sa AND sb) OR alt", Cost: 100.0),            // shared once: 3 400 < 4 000 (twice: 6 400)
            new Node(16, "big", null, Cost: 1000.0), new Node(17, "x1", "big", Cost: 100.0),
            new Node(18, "x2", "big", Cost: 100.0), new Node(19, "y1", null, Cost: 700.0), new Node(20, "y2", null, Cost: 700.0),
            new Node(21, "d5", "(x1 OR y1) AND (x2 OR y2)", Cost: 100.0),     // sharing ACROSS ORs: exact 1 200 < greedy 1 400
            new Node(22, "ta", null, Cost: 400.0), new Node(23, "tb", null, Cost: 400.0),
            new Node(24, "d6", "tb OR ta", Cost: 100.0),                      // tie: lower key (ta) though written second
        ]);
        // The exact/greedy bound: twelve 2-way OR groups (4 096 combinations, exact) and thirteen (8 192, greedy).
        for (int g = 0; g < 13; g++)
        {
            s.Technologies.Add(new Node(40 + 2 * g, "ga" + g, null, Cost: 300.0 + g));
            s.Technologies.Add(new Node(41 + 2 * g, "gb" + g, null, Cost: 310.0 - g));
        }
        string Groups(int count)
        {
            var parts = new List<string>();
            for (int g = 0; g < count; g++) parts.Add("(ga" + g + " OR gb" + g + ")");
            return string.Join(" AND ", parts);
        }
        s.Technologies.Add(new Node(70, "d12", Groups(12), Cost: 100.0));
        s.Technologies.Add(new Node(71, "d13", Groups(13), Cost: 100.0));
        s.Civics.Add(new Node(1001, "council", "root", Cost: 9000.0));      // the Civics tree is never empty (D-044 R12)
        return s;
    }

    private static readonly Lazy<ResearchContent> Rig = new(() => RouteSpec().Load());
    private static ResearchContent R => Rig.Value;
    private static int I(ResearchContent c, string id) => c.IndexOfId(id);

    private static string[] Ids(ResearchContent c, int[] nodes)
    {
        var ids = new string[nodes.Length];
        for (int k = 0; k < nodes.Length; k++) ids[k] = c.Nodes[nodes[k]].Id;
        Array.Sort(ids, StringComparer.Ordinal);
        return ids;
    }

    private static ResearchRoutePlan PlanOf(ResearchContent c, WorldState w, string dest) =>
        ResearchRoutePlanner.Plan(c, ResearchQuery.CompletedMask(w, c, P), ResearchQuery.RemainingCosts(w, c, P), I(c, dest));

    private static WorldState WorldWith(ResearchContent c, params string[] done)
    {
        WorldState w = PlayerWorld();
        foreach (string id in done) w.ResearchCompleted.Add(new ResearchCompletedRow(P, c.Nodes[I(c, id)].Key));
        return w;
    }

    private static WorldState WithDestination(ResearchContent c, WorldState w, string dest)
    {
        w.ResearchTargets.Add(new ResearchTargetRow(P, c.Nodes[I(c, dest)].Key));
        return w;
    }

    [Fact]
    public void OneMissingPrerequisite_RouteIsThatNode_AndItIsFunded()
    {
        WorldState w = WithDestination(R, WorldWith(R, "root"), "d1");
        ResearchRoute route = ResearchQuery.Route(w, R, P, ResearchTree.Technology);
        Assert.Equal(R.Nodes[I(R, "d1")].Key, route.Destination);
        Assert.Equal(R.Nodes[I(R, "m1")].Key, route.Current);
        Assert.Equal(R.Nodes[I(R, "d1")].Key, route.Next);                   // once m1 is done, the destination itself
        RouteBlocker b = Assert.Single(route.Blockers);
        Assert.Equal((RouteReason.Prerequisite, true), (b.Reason, b.Eligible));
        Assert.Equal(2500.0, route.RemainingCost);
        WorldState next = Executor(R).Step(w);
        Assert.Equal(1000.0, Progress(next, 2));                                // the step funds m1 — what Route reported
        Assert.True(ResearchQuery.TryGetTarget(next, P, out ResearchNodeId t) && t.Value == 3);   // the row stays: destination
    }

    [Fact]
    public void SeveralAndPrerequisites_AllOnTheRoute_CheapestFundedFirst()
    {
        ResearchRoutePlan plan = PlanOf(R, WorldWith(R, "root"), "d2");
        Assert.Equal(["and1", "and2"], Ids(R, plan.Route));
        Assert.Equal(3500.0, plan.RemainingCost);
        WorldState w = WithDestination(R, WorldWith(R, "root"), "d2");
        Assert.Equal(R.Nodes[I(R, "and2")].Key, ResearchQuery.Route(w, R, P, ResearchTree.Technology).Current);
    }

    [Fact]
    public void Or_TheCheapestCompleteRouteWins_NotTheCheapestImmediatePrerequisite()
    {
        // imm_a (100) is the cheapest immediate prerequisite, written first, lower key — but its route needs heavy (5 000).
        ResearchRoutePlan plan = PlanOf(R, WorldWith(R), "d3");
        Assert.Equal(["imm_b"], Ids(R, plan.Route));
        Assert.Equal(2500.0, plan.RemainingCost);
        // Once heavy is known the OR flips to imm_a (100 < 2 000).
        Assert.Equal(["imm_a"], Ids(R, PlanOf(R, WorldWith(R, "heavy"), "d3").Route));
        // Partial progress counts: imm_b nearly paid for keeps it (remaining 50 < imm_a's 100) even with heavy known.
        WorldState w = WorldWith(R, "heavy");
        w.ResearchProgress.Add(new ResearchProgressRow(P, new ResearchNodeId(9), 1950.0));
        Assert.Equal(["imm_b"], Ids(R, PlanOf(R, w, "d3").Route));
    }

    [Fact]
    public void SharedPrerequisites_AreCountedOnce()
    {
        ResearchRoutePlan plan = PlanOf(R, WorldWith(R), "d4");
        Assert.Equal(["sa", "sb", "shared"], Ids(R, plan.Route));             // 3 400 (shared once) < alt 4 000
        Assert.Equal(3400.0 + 100.0, plan.RemainingCost);
        // Sharing across OR groups: the exact search sees x1 and x2 share big — {big, x1, x2} 1 200 beats {y1, y2} 1 400,
        // which an independent per-OR choice (each OR alone: x 1 100 > y 700) would take.
        ResearchRoutePlan d5 = PlanOf(R, WorldWith(R), "d5");
        Assert.True(d5.Exact);
        Assert.Equal(["big", "x1", "x2"], Ids(R, d5.Route));
        Assert.Equal(1300.0, d5.RemainingCost);
    }

    [Fact]
    public void ExactUpTo4096Combinations_ThenGreedy_BothValid()
    {
        ResearchRoutePlan d12 = PlanOf(R, WorldWith(R), "d12");
        Assert.True(d12.Exact);
        Assert.Equal(4096, d12.Combinations);
        ResearchRoutePlan d13 = PlanOf(R, WorldWith(R), "d13");
        Assert.False(d13.Exact);
        Assert.True(d13.Combinations > ResearchRoutePlanner.ExactCombinationLimit);
        // Both pick, per group, the cheaper alternative (ga g: 300+g vs gb g: 310−g → ga for g ≤ 4, gb for g ≥ 6, g = 5 ties
        // at 305 → the lower key, ga5); both routes make the destination eligible.
        foreach ((ResearchRoutePlan plan, int groups) in new[] { (d12, 12), (d13, 13) })
        {
            var expect = new List<string>();
            for (int g = 0; g < groups; g++) expect.Add((g <= 5 ? "ga" : "gb") + g);
            expect.Sort(StringComparer.Ordinal);
            Assert.Equal([.. expect], Ids(R, plan.Route));
            var done = new bool[R.Nodes.Count];
            foreach (int i in plan.Route) done[i] = true;
            Assert.True(ResearchQuery.AvailableMask(R, done)[plan.Destination]);
        }
    }

    // ------------------------------------------------------------------ tie-breaking (tie-dense)

    [Fact]
    public void TieDense_BitEqualRoutes_GoToTheLowerKey_NotTheOneWrittenFirst()
    {
        Assert.Equal(R.Nodes[I(R, "ta")].BaseCost, R.Nodes[I(R, "tb")].BaseCost);
        Assert.Equal(["ta"], Ids(R, PlanOf(R, WorldWith(R), "d6").Route));    // "tb OR ta": ta has the lower key
    }

    [Fact]
    public void TieDense_CurrentNode_IsTheSmallestRemainingCostThenKey()
    {
        // Every one of the 26 group nodes is eligible; set all remaining costs bit-equal → the lowest key; one strictly
        // cheaper node with a higher key beats them; restored to bit-equal → the lowest key again.
        ResearchRoutePlan plan = PlanOf(R, WorldWith(R), "d13");
        bool[] completed = new bool[R.Nodes.Count];
        bool[] available = ResearchQuery.AvailableMask(R, completed);
        var remaining = new double[R.Nodes.Count];
        Array.Fill(remaining, 777.0);
        int lowest = plan.Route[0];
        foreach (int i in plan.Route) Assert.True(available[i]);
        Assert.Equal(lowest, ResearchRoutePlanner.PickCurrent(R, plan.Route, available, remaining, 0, false, -1));
        int last = plan.Route[^1];
        remaining[last] = Math.BitDecrement(777.0);
        Assert.Equal(last, ResearchRoutePlanner.PickCurrent(R, plan.Route, available, remaining, 0, false, -1));
        remaining[last] = 777.0;
        Assert.Equal(lowest, ResearchRoutePlanner.PickCurrent(R, plan.Route, available, remaining, 0, false, -1));
        Assert.Equal(plan.Route[1], ResearchRoutePlanner.PickCurrent(R, plan.Route, available, remaining, 0, false, lowest));
    }

    [Fact]
    public void Plan_IsDeterministic_RepeatedAndIndependentOfRowOrder()
    {
        WorldState a = WorldWith(R, "root", "big");
        WorldState b = PlayerWorld();
        b.ResearchCompleted.Add(new ResearchCompletedRow(P, new ResearchNodeId(16)));
        b.ResearchCompleted.Add(new ResearchCompletedRow(P, new ResearchNodeId(1)));
        foreach (string dest in new[] { "d1", "d2", "d3", "d4", "d5", "d6", "d12", "d13" })
        {
            ResearchRoutePlan x = PlanOf(R, a, dest), y = PlanOf(R, b, dest), z = PlanOf(R, a, dest);
            Assert.Equal(x.Route, y.Route);
            Assert.Equal(x.Route, z.Route);
            Assert.Equal(x.Cause, y.Cause);
            Assert.Equal(x.RemainingCost, y.RemainingCost);
        }
    }

    // ------------------------------------------------------------------ the Age sweep on the route

    private static readonly Lazy<ResearchContent> Sweep = new(() => ResearchAgeSweepTests.SweepSpec(on: true, routing: true).Load());
    private static ResearchContent S => Sweep.Value;

    [Fact]
    public void SweepBlockedDestination_PicksTheCheapestEligibleCohortNode_LabelledSweepNeverPrerequisite()
    {
        WorldState w = WithDestination(S, WorldWith(S, "a"), "z3");
        ResearchRoute route = ResearchQuery.Route(w, S, P, ResearchTree.Technology);
        Assert.Equal(S.Nodes[I(S, "b")].Key, route.Current);                  // b (400): an Age-1 cohort node
        var bySweep = new List<string>();
        foreach (RouteBlocker b in route.BlockersOf(RouteReason.Sweep, 1)) bySweep.Add(S.Nodes[b.Index].Id);
        bySweep.Sort(StringComparer.Ordinal);
        Assert.Equal(["b", "c", "law", "mil"], bySweep);                      // milrep (repeatable) is not a member
        RouteBlocker m2 = Assert.Single(route.BlockersOf(RouteReason.Prerequisite));
        Assert.Equal("m2", S.Nodes[m2.Index].Id);
        foreach (RouteBlocker b in route.Blockers)
            if (b.Cause == RouteReason.Sweep) Assert.Equal(1, b.SweepAge);
        Assert.Equal(400 + 700 + 500 + 600 + 500 + 300.0, route.RemainingCost);
        Assert.True(route.Feasible);
        // Ordered: (remaining, key) as each becomes eligible: b 400, mil 500 (key 4) before m2 500 (key 6), law 600, c 700.
        Assert.Equal([2, 4, 6, 1001, 3], KeysOf(route.Ordered));
    }

    private static int[] KeysOf(IReadOnlyList<ResearchNodeId> ids)
    {
        var k = new int[ids.Count];
        for (int i = 0; i < k.Length; i++) k[i] = ids[i].Value;
        return k;
    }

    [Fact]
    public void AutomaticAdvancement_AfterEachCompletion_UntilTheDestinationCompletes()
    {
        var log = new OrderLog();
        log.Append(Target(0, 8));                                               // z3, unavailable: a destination
        TurnExecutor ex = Executor(S, log);
        WorldState w = WithCompleted(PlayerWorld(), 1);
        int[][] expected = [[2], [4], [6], [1001], [3], [8]];                  // b, mil, m2, law, c — then z3 itself
        for (int t = 0; t < expected.Length; t++)
        {
            ResearchRoute before = ResearchQuery.Route(w, S, P, ResearchTree.Technology);
            WorldState next = ex.Step(w);
            Assert.Equal(expected[t], KeysOf(ResearchQuery.CompletedBetween(w, next, P)));
            if (t > 0) Assert.Equal(expected[t][0], before.Current!.Value.Value);   // the query named what the step funded
            bool last = t == expected.Length - 1;
            Assert.Equal(!last, ResearchQuery.TryGetTarget(next, P, out ResearchNodeId d) && d.Value == 8);
            w = next;
        }
        Assert.Null(ResearchQuery.Route(w, S, P, ResearchTree.Technology).Destination);
    }

    [Fact]
    public void ChangingTheDestinationMidRoute_RetargetsThatStep_AndKeepsEveryProgress()
    {
        var log = new OrderLog();
        log.Append(Target(0, 8));                                               // z3
        log.Append(Target(2, 9));                                               // x3 at turn 2: route {law, c}; m2 no longer needed
        TurnExecutor ex = Executor(S, log);
        WorldState w = Run(ex, WithCompleted(PlayerWorld(), 1), 2);            // b, mil done
        Assert.True(ResearchRigs.Done(w, 2) && ResearchRigs.Done(w, 4));
        WorldState w3 = ex.Step(w);
        Assert.Equal([1001], KeysOf(ResearchQuery.CompletedBetween(w, w3, P)));   // law (600), not m2 (500, off the new route)
        Assert.False(ResearchRigs.Done(w3, 6));
        Assert.True(ResearchQuery.TryGetTarget(w3, P, out ResearchNodeId d) && d.Value == 9);
        WorldState w5 = Run(ex, w3, 2);
        Assert.True(ResearchRigs.Done(w5, 3) && ResearchRigs.Done(w5, 9));     // c, then x3; row removed
        Assert.False(ResearchQuery.TryGetTarget(w5, P, out _));
    }

    [Fact]
    public void Cancel_TheClearOrderRemovesTheDestination_AndRpReachesNoNode()
    {
        var log = new OrderLog();
        log.Append(Target(0, 8));
        log.Append(Target(1, ResearchQuery.ClearTechnologyTargetId));
        TurnExecutor ex = Executor(S, log);
        WorldState w1 = ex.Step(WithCompleted(PlayerWorld(), 1));
        Assert.True(ResearchRigs.Done(w1, 2));
        WorldState w2 = ex.Step(w1);
        Assert.False(ResearchQuery.TryGetTarget(w2, P, out _));
        Assert.Empty(ResearchQuery.CompletedBetween(w1, w2, P));
        Assert.Empty(ResearchQuery.PartialProgress(w2, P));
        Assert.Null(ResearchQuery.Route(w2, S, P, ResearchTree.Technology).Destination);
    }

    [Fact]
    public void SaveLoad_PreservesTheDestinationAndTheCurrentResearch()
    {
        var log = new OrderLog();
        log.Append(Target(0, 8));
        TurnExecutor ex = Executor(S, log);
        WorldState w = Run(ex, WithCompleted(PlayerWorld(), 1), 2);
        // A partial progress row on the route, so "current" depends on saved progress too.
        w.ResearchProgress.Add(new ResearchProgressRow(P, new ResearchNodeId(3), 650.0));   // c: remaining 50
        using var ms = new MemoryStream();
        Snapshot.Save(w, ms);
        ms.Position = 0;
        WorldState loaded = Snapshot.Load(ms);
        Assert.True(WorldStates.StateEquals(w, loaded));
        ResearchRoute a = ResearchQuery.Route(w, S, P, ResearchTree.Technology);
        ResearchRoute b = ResearchQuery.Route(loaded, S, P, ResearchTree.Technology);
        Assert.Equal(new ResearchNodeId(8), b.Destination);
        Assert.Equal(new ResearchNodeId(3), b.Current);                       // c, nearly paid for, now the cheapest remaining
        Assert.Equal((a.Destination, a.Current, a.Next, a.RemainingCost), (b.Destination, b.Current, b.Next, b.RemainingCost));
        Assert.Equal(a.Ordered, b.Ordered);
        Assert.Equal(a.Blockers, b.Blockers);
        WorldState x = Run(ex, w, 4), y = Run(ex, loaded, 4);
        Assert.Equal(WorldHash.ComputeHex(x), WorldHash.ComputeHex(y));
        Assert.True(ResearchRigs.Done(x, 8));
    }

    [Fact]
    public void TurnExactPin_ADestinationStampedT_FundsItsRouteInTheStepFromT()
    {
        // Stamped turn 0 → world 1 already carries the destination row AND b (its current node) complete.
        var early = new OrderLog();
        early.Append(Target(0, 8));
        WorldState e1 = Executor(S, early).Step(WithCompleted(PlayerWorld(), 1));
        Assert.True(ResearchQuery.TryGetTarget(e1, P, out ResearchNodeId t) && t.Value == 8);
        Assert.True(ResearchRigs.Done(e1, 2));
        // Stamped turn 1 → world 1 has neither; world 2 has both.
        var late = new OrderLog();
        late.Append(Target(1, 8));
        TurnExecutor ex = Executor(S, late);
        WorldState l1 = ex.Step(WithCompleted(PlayerWorld(), 1));
        Assert.False(ResearchQuery.TryGetTarget(l1, P, out _));
        Assert.False(ResearchRigs.Done(l1, 2));
        Assert.Empty(ResearchQuery.PartialProgress(l1, P));
        WorldState l2 = ex.Step(l1);
        Assert.True(ResearchQuery.TryGetTarget(l2, P, out ResearchNodeId t2) && t2.Value == 8);
        Assert.True(ResearchRigs.Done(l2, 2));
    }

    [Fact]
    public void ReplayEquality_TheLogReplaysHashForHash()
    {
        var log = new OrderLog();
        log.Append(Target(0, 8));
        log.Append(Target(2, 9));
        log.Append(Target(3, ResearchQuery.ClearTechnologyTargetId));
        log.Append(Target(4, 10));                                              // y4: Age 4, behind the Age-2 sweep
        WorldState start = WithCompleted(PlayerWorld(), 1);
        var live = new List<string>();
        WorldState w = start.Clone();
        TurnExecutor ex = Executor(S, log);
        for (int t = 0; t < 12; t++) { w = ex.Step(w); live.Add(WorldHash.ComputeHex(w)); }

        using var ms = new MemoryStream();
        log.Save(ms);
        ms.Position = 0;
        OrderLog replayLog = OrderLog.Load(ms);
        WorldState r = start.Clone();
        TurnExecutor rex = Executor(S, replayLog);
        for (int t = 0; t < 12; t++) { r = rex.Step(r); Assert.Equal(live[t], WorldHash.ComputeHex(r)); }
        Assert.True(ResearchRigs.Done(r, 10));                                  // the Age-4 destination was reached
    }

    [Fact]
    public void SwitchOff_AnOrderNamingAnUnavailableNode_ChangesNothing_TheTwin()
    {
        ResearchContent off = ResearchAgeSweepTests.SweepSpec(on: true, routing: false).Load();
        var log = new OrderLog();
        log.Append(Target(0, 8));
        WorldState w = Executor(off, log).Step(WithCompleted(PlayerWorld(), 1));
        Assert.False(ResearchQuery.TryGetTarget(w, P, out _));
        Assert.Empty(ResearchQuery.PartialProgress(w, P));
        Assert.Null(ResearchQuery.TargetOrder(WithCompleted(PlayerWorld(), 1), off, P, new ResearchNodeId(8)));
        Assert.NotNull(ResearchQuery.TargetOrder(WithCompleted(PlayerWorld(), 1), S, P, new ResearchNodeId(8)));
        Assert.Null(ResearchQuery.TargetOrder(WithCompleted(PlayerWorld(), 1), S, P, new ResearchNodeId(1)));   // completed
    }

    [Fact]
    public void InfeasibleDestination_IsKept_AndTheQueryReportsItsBlockers()
    {
        // late (Age 1) requires q3 (Age 3), whose sweep requires late: a deadlock the shipped graph never has.
        Spec s = ResearchAgeSweepTests.SweepSpec(on: true, routing: true);
        s.Technologies.Add(new Node(11, "q3", "a", Cost: 100.0, Age: "A3"));
        s.Technologies.Add(new Node(12, "late", "q3", Cost: 100.0));
        ResearchContent c = s.Load();
        var log = new OrderLog();
        log.Append(Target(0, 11));
        TurnExecutor ex = Executor(c, log);
        WorldState w = Run(ex, WithCompleted(PlayerWorld(), 1), 8);
        Assert.True(ResearchQuery.TryGetTarget(w, P, out ResearchNodeId d) && d.Value == 11);   // kept
        ResearchRoute route = ResearchQuery.Route(w, c, P, ResearchTree.Technology);
        Assert.False(route.Feasible);
        Assert.Null(route.Current);
        RouteBlocker late = Assert.Single(route.Blockers);
        Assert.Equal(("late", RouteReason.Sweep, 1, false), (c.Nodes[late.Index].Id, late.Reason, late.SweepAge, late.Eligible));
    }

    [Fact]
    public void ActionSurface_OffersAsDestinations_ExactlyTheIncompleteUnavailableNodes_AndOnlyWithRouting()
    {
        WorldState w = WithCompleted(PlayerWorld(), 1);
        bool[] done = ResearchQuery.CompletedMask(w, S, P);
        bool[] available = ResearchQuery.AvailableMask(S, done);
        var into = new List<ActionDescriptor>();
        AvailableActionsQuery.Research(w, TestConfigs.Sim() with { Research = S }, P, into);
        ActionDescriptor dest = into.Find(a => a.Id == 4)!;
        Assert.Equal("research.set-destination", dest.Key);
        var listed = new List<long>();
        foreach (ActionTarget t in dest.Targets) listed.Add(t.Id);
        var expected = new List<long>();
        for (int i = 0; i < S.Nodes.Count; i++) if (!done[i] && !available[i]) expected.Add(S.Nodes[i].Key.Value);
        Assert.Equal(expected, listed);
        foreach (long key in listed)                                             // every one is an order the engine admits
            Assert.NotNull(ResearchQuery.TargetOrder(w, S, P, new ResearchNodeId((int)key)));
        var off = new List<ActionDescriptor>();
        AvailableActionsQuery.Research(w, TestConfigs.Sim() with { Research = ResearchAgeSweepTests.SweepSpec(true).Load() }, P, off);
        Assert.DoesNotContain(off, a => a.Id == 4);
    }

    // ------------------------------------------------------------------ two lanes: free-lane assist and otherLane

    //  tech: a · t1 (a) · dt (t1 AND civ1)        civics: civ1 (a) · civ2 (a) · dc (civ2)
    private static readonly Lazy<ResearchContent> Lanes = new(() =>
    {
        var s = new Spec { Stage = "a", DestinationRouting = true, CivicsPoints = (true, 1.0, 0.5) };
        s.Technologies.AddRange([
            new Node(1, "a", Cost: 1000.0), new Node(2, "t1", "a", Cost: 800.0), new Node(3, "dt", "t1 AND civ1", Cost: 500.0),
        ]);
        s.Civics.AddRange([
            new Node(1001, "civ1", "a", Cost: 600.0), new Node(1002, "civ2", "a", Cost: 300.0), new Node(1003, "dc", "civ2", Cost: 400.0),
        ]);
        return s.Load();
    });

    [Fact]
    public void FreeLaneAssist_TheCivicsLaneWithoutADestinationFundsTheTechRoutesCivic()
    {
        ResearchContent c = Lanes.Value;
        WorldState w = WithDestination(c, WorldWith(c, "a"), "dt");
        ResearchRoute tech = ResearchQuery.Route(w, c, P, ResearchTree.Technology);
        ResearchRoute civ = ResearchQuery.Route(w, c, P, ResearchTree.Civics);
        Assert.Equal(new ResearchNodeId(2), tech.Current);                     // t1 on RP
        Assert.True(civ.Assisting);
        Assert.Equal(new ResearchNodeId(1001), civ.Current);                   // civ1 on CP
        Assert.Equal(new ResearchNodeId(3), civ.AssistedDestination);
        Assert.Empty(tech.BlockersOf(RouteReason.OtherLane));                  // the other lane is free: not a blocker
        WorldState next = Executor(c).Step(w);
        Assert.True(ResearchRigs.Done(next, 2) && ResearchRigs.Done(next, 1001));
        WorldState done = Executor(c).Step(next);
        Assert.True(ResearchRigs.Done(done, 3));
    }

    [Fact]
    public void OtherLaneBusy_ItsNodeIsAnOtherLaneBlocker_AndThisLaneWaits()
    {
        ResearchContent c = Lanes.Value;
        WorldState w = WithDestination(c, WithDestination(c, WorldWith(c, "a", "t1"), "dt"), "dc");
        ResearchRoute tech = ResearchQuery.Route(w, c, P, ResearchTree.Technology);
        Assert.Null(tech.Current);                                              // nothing of its own lane left to fund
        RouteBlocker b = Assert.Single(tech.Blockers);
        Assert.Equal((1001, RouteReason.OtherLane, RouteReason.Prerequisite), (b.Key.Value, b.Reason, b.Cause));
        Assert.Equal(new ResearchNodeId(1002), ResearchQuery.Route(w, c, P, ResearchTree.Civics).Current);   // its own route
        WorldState next = Executor(c).Step(w);
        Assert.False(ResearchRigs.Done(next, 1001));
        Assert.True(ResearchRigs.Done(next, 1002));
    }

    // ------------------------------------------------------------------ the AI uses the same planner

    [Fact]
    public void PlayerAndAi_TheSameDestination_FundTheSameRouteNode()
    {
        WorldState w = World([Player, 2], [Player, 2], 10_000);
        WithCompleted(w, 1);
        w.ResearchCompleted.Add(new ResearchCompletedRow(new PolityId(2), new ResearchNodeId(1)));
        OrderRecord? ai = ResearchQuery.TargetOrder(w, S, new PolityId(2), new ResearchNodeId(8));   // the AI's builder
        Assert.NotNull(ai);
        var log = new OrderLog();
        log.Append(Target(0, 8));
        log.Append(ai!.Value);
        WorldState next = Executor(S, log).Step(w);
        Assert.Equal(KeysOf(ResearchQuery.CompletedBetween(w, next, P)), KeysOf(ResearchQuery.CompletedBetween(w, next, new PolityId(2))));
        Assert.Equal(ResearchQuery.Route(next, S, P, ResearchTree.Technology).Current,
            ResearchQuery.Route(next, S, new PolityId(2), ResearchTree.Technology).Current);
    }

    [Fact]
    public void Ai_GoalDestination_IsTheCheapestRouteSink_AndTheAiOrdersIt()
    {
        // A goal closure over d3 (via imm_b) and d1 (via m1): its sinks are d1 and d3 (m1 and imm_b have dependents in it).
        var closure = new bool[R.Nodes.Count];
        foreach (string id in new[] { "m1", "d1", "imm_b", "d3" }) closure[I(R, id)] = true;
        int Goal(WorldState w)
        {
            bool[] completed = ResearchQuery.CompletedMask(w, R, P);
            return AiResearchPolicy.GoalDestination(w, R, P, closure, completed, ResearchQuery.AvailableMask(R, completed));
        }
        // root known: d1's route (m1 1 500 + d1 1 000) and d3's (imm_b 2 000 + d3 500) are bit-equal → the lower key, d1.
        Assert.Equal(I(R, "d1"), Goal(WorldWith(R, "root")));
        // heavy known too: the PLANNER's route to d3 is imm_a (100) + 500 = 600 — cheaper, so d3 (the closure's imm_b
        // is not what costs it: the planner does).
        Assert.Equal(I(R, "d3"), Goal(WorldWith(R, "root", "heavy")));
        // Two available sinks of equal cost: the lower key.
        Array.Clear(closure);
        closure[I(R, "ta")] = closure[I(R, "tb")] = true;
        Assert.Equal(I(R, "ta"), AiResearchPolicy.GoalDestination(WorldWith(R), R, P, closure, new bool[R.Nodes.Count],
            ResearchQuery.AvailableMask(R, new bool[R.Nodes.Count])));
    }

    [Fact]
    public void ShippedContent_BothSwitchesOn_EveryNodeRoutesFeasibly_AndTheAiRunsThroughThePlanner()
    {
        ResearchContent c = ResearchAgeSweepTests.ContentWith(sweep: true, routing: true);
        var none = new bool[c.Nodes.Count];
        var remaining = new double[c.Nodes.Count];
        for (int i = 0; i < remaining.Length; i++) remaining[i] = c.Nodes[i].BaseCost;
        for (int i = 0; i < c.Nodes.Count; i++)
        {
            ResearchRoutePlan plan = ResearchRoutePlanner.Plan(c, none, remaining, i);
            (int[] ordered, bool feasible) = ResearchRoutePlanner.Order(c, none, remaining, plan);
            Assert.True(feasible, c.Nodes[i].Id + " has no feasible route from scratch");
            Assert.Equal(plan.Route.Length, ordered.Length);
        }

        SimConfig cfg = TestConfigs.Sim() with { Research = c };
        WorldState w = WorldFounding.Found(TestConfigs.DevWorldgen() with { AiEmpires = 1 }, cfg, 42);
        PolityId ai = default;
        for (int k = 0; k < w.Polities.Count; k++) if (w.Polities[k].Source == CommandSource.Ai) ai = w.Polities[k].Id;
        OrderRecord[] orders = AiResearchPolicy.DecideAll(w, cfg, ai);
        Assert.NotEmpty(orders);
        var log = new OrderLog();
        foreach (OrderRecord o in orders) log.Append(o);
        using var era = Sim.Data.DataFiles.OpenEraPacing();
        using var pipe = Sim.Data.DataFiles.OpenPipeline();
        var exec = new TurnExecutor(EraTableLoader.Load(era), PipelineLoader.Load(pipe, SystemCatalog.All(cfg, TestConfigs.Worldgen())), log);
        WorldState next = exec.Step(w);
        foreach (ResearchTree lane in new[] { ResearchTree.Technology, ResearchTree.Civics })
        {
            ResearchRoute route = ResearchQuery.Route(next, c, ai, lane);
            if (route.Destination is null) continue;
            Assert.True(route.Feasible);
            Assert.True(route.Current is not null || route.BlockersOf(RouteReason.OtherLane).Count > 0);
        }
    }
}
