using System.Globalization;
using System.Text;
using Sim.Core;
using Sim.Core.Kernel;
using Sim.Core.State;
using Sim.Core.Systems;
using Sim.Core.Systems.Research;
using Sim.Core.Worldgen;
using Sim.Tests.TestUtil;
using Xunit.Abstractions;
using static Sim.Tests.TestUtil.ResearchRigs;

namespace Sim.Tests.Systems;

/// <summary>
/// THE RP / CP LIFECYCLE AUDIT (research-cost-pass, rp-cp-lifecycle.md) — every way Research Points and Civics Points are
/// banked, spent, lost, blocked or misdisplayed under the SHIPPED engine: two lanes (civicsPoints on), destination routing on,
/// tuning.researchOverflow OFF. Each [Fact] pins the CURRENT behaviour (not a desired one) with the arithmetic in its comment.
///
/// Rig calibration (ResearchRigs): one player polity, 10 000 people. RP per step R = ResearchQuery.ResearchPointPool (1 000 at
/// 10 × P^0.5, measured, never assumed); CP per dt-10 step G = ResearchQuery.CivicsPointsMilli = 1 000 000 milli-CP (1.0 × P^0.5
/// = 100 CP a sim-year × 10). Costs are sized from R so every node cost is ≥ 71 and valid against U × K^magnitude.
///
/// "RP spent" in a step is measured from state, never from the engine's internals: Σ over technology nodes of (progress after —
/// progress before), where a node completed (or levelled) in the step counts its EffectiveCost on PREV. No rig here has a Eureka,
/// so no credit is mixed in. "RP lost" = R − RP spent: the engine stores RP nowhere else (D-044 R20-D).
/// </summary>
public class ResearchPointLifecycleTests(ITestOutputHelper output)
{
    private static readonly PolityId P = new(Player);
    private const string Farming = "farming.yieldPerArableKm2PerYear";

    /// <summary>R: the rig's RP per step (population-driven, content-independent).</summary>
    private static readonly Lazy<double> Rp = new(() =>
    {
        ResearchContent probe = new Spec { Stage = "x", Technologies = { new Node(1, "x") }, Civics = { new Node(2001, "cv") } }.Load();
        return ResearchQuery.ResearchPointPool(PlayerWorld(), probe, P);
    });

    private static double R => Rp.Value;

    /// <summary>The shipped engine's switches on the rig: two lanes (CP 1.0 × P^0.5 a sim-year), destination routing, overflow
    /// OFF. t0 (key 1, 5 R) is the stage node and an always-available technology nobody names unless a test does.</summary>
    private static ResearchContent Rig(Action<Spec> add, bool levels = false)
    {
        var s = new Spec { Stage = "t0", DestinationRouting = true, CivicsPoints = (true, 1.0, 0.5), RepeatableLevels = levels ? true : null };
        s.Technologies.Add(new Node(1, "t0", Cost: 5.0 * R));
        add(s);
        if (s.Civics.Count == 0) s.Civics.Add(new Node(2001, "cv0", Cost: 400_000.0));
        ResearchContent c = s.Load();
        Assert.False(c.Tuning.ResearchOverflow);
        Assert.True(c.Tuning.TwoLanes && c.Tuning.DestinationRouting);
        return c;
    }

    private static WorldState Toward(params int[] keys)
    {
        WorldState w = PlayerWorld();
        foreach (int k in keys) w.ResearchTargets.Add(new ResearchTargetRow(P, Key(k)));
        return w;
    }

    /// <summary>RP that throughput added to technology progress in the step prev → next (see the class summary).</summary>
    private static double RpSpent(IReadOnlyWorldState prev, IReadOnlyWorldState next, ResearchContent c)
    {
        double added = 0.0;
        for (int i = 0; i < c.Nodes.Count; i++)
        {
            if (c.Nodes[i].Tree != ResearchTree.Technology) continue;
            ResearchNodeId k = c.Nodes[i].Key;
            bool completedNow = !ResearchQuery.IsCompleted(prev, P, k) && ResearchQuery.IsCompleted(next, P, k);
            bool levelled = ResearchQuery.Level(next, P, k) > ResearchQuery.Level(prev, P, k);
            double after = completedNow || levelled ? ResearchQuery.EffectiveCost(prev, c, P, i) : ResearchQuery.Progress(next, P, k);
            added += after - ResearchQuery.Progress(prev, P, k);
        }
        return added;
    }

    private static bool HasTarget(IReadOnlyWorldState w, ResearchContent c, ResearchTree lane) =>
        ResearchQuery.TryGetTarget(w, c, P, lane, out _);

    private static CivicsPointsRow Cp(IReadOnlyWorldState w) => ResearchQuery.CivicsPointsAccount(w, P);

    private static void Identity(IReadOnlyWorldState w) => Assert.Null(ResearchQuery.CivicsPointsInvariantViolation(w.CivicsPoints));

    private static SimConfig Cfg(ResearchContent c) =>
        TestConfigs.Sim() with { Research = c, Ages = null, Roads = null, Governance = null, Institutions = null };

    private static string LearningLine(IReadOnlyWorldState w, ResearchContent c) =>
        InfoQuery.Card(w, Cfg(c), P, InfoSubject.Research, actions: System.Collections.Immutable.ImmutableArray<ActionDescriptor>.Empty).StatusLine;

    private static string NodeLine(IReadOnlyWorldState w, ResearchContent c, int key) =>
        InfoQuery.Card(w, Cfg(c), P, InfoSubject.Node(Key(key)), actions: System.Collections.Immutable.ImmutableArray<ActionDescriptor>.Empty).StatusLine;

    private static string N0(double v) => v.ToString("#,0", CultureInfo.InvariantCulture);
    private static string N1(double v) => v.ToString("#,0.#", CultureInfo.InvariantCulture);

    // ================================================================== (a) surplus past completion

    [Fact]
    public void A_SurplusPastCompletion_IsDiscarded()
    {
        // x (0.3 R) is the available destination; y (0.3 R) is available too but not on any route.
        // Before: pool R, x remaining 0.3 R.  Throughput: Credit(x, R) adds 0.3 R (capped at the remaining cost, ResearchSystem
        // Credit) → x complete.  After: 0.3 R spent, R − 0.3 R = 0.7 R discarded; y untouched; the destination row is gone.
        ResearchContent c = Rig(s => s.Technologies.AddRange([new Node(2, "x", Cost: 0.3 * R), new Node(3, "y", Cost: 0.3 * R)]));
        WorldState w = Toward(2);
        WorldState next = Executor(c).Step(w);
        Assert.True(Done(next, 2));
        Assert.Equal(0.0, Progress(next, 3));
        Assert.Empty(ResearchQuery.PartialProgress(next, P));
        double spent = RpSpent(w, next, c);
        Assert.Equal(0.3 * R, spent);
        Assert.Equal(R - 0.3 * R, R - spent);   // the WASTE: 700 RP at R = 1 000
        Assert.False(HasTarget(next, c, ResearchTree.Technology));
    }

    // ================================================================== (b) a step with no technology destination

    [Fact]
    public void B_NoTechnologyDestination_DiscardsTheWholeStep_AndTheScreensOnlySayIdle()
    {
        // No target row at all; t0 and x are available. Before: pool R. ResearchSystem step 3: fund[0] = −1 → nothing runs.
        // After: 0 spent, R lost; nothing is stored anywhere (no progress row, no bank).
        ResearchContent c = Rig(s => s.Technologies.Add(new Node(2, "x", Cost: 2.0 * R)));
        WorldState w = PlayerWorld();
        WorldState next = Executor(c).Step(w);
        Assert.Equal(0.0, RpSpent(w, next, c));
        Assert.Empty(ResearchQuery.PartialProgress(next, P));
        // CP is unaffected: no civic destination either, so the whole accrual is BANKED.
        Assert.Equal(ResearchQuery.CivicsPointsMilli(w, c, P, 10.0), Cp(next).BalanceMilli);

        // What the player sees: the route is None (no destination, no current node), the pool is still shown as R a turn, and
        // the Glass-Box research card is the only surface naming the loss — "Idle: R research points a turn go unused".
        ResearchRoute route = ResearchQuery.Route(next, c, P, ResearchTree.Technology);
        Assert.Null(route.Destination);
        Assert.Null(route.Current);
        Assert.False(route.Assisting);
        Assert.Equal(R, ResearchQuery.ResearchPointPool(next, c, P));
        Assert.StartsWith("Idle: " + N1(R) + " research points a turn go unused;", LearningLine(next, c));
    }

    // ================================================================== (c) switching or clearing mid-node

    [Fact]
    public void C_SwitchingOrClearingMidNode_KeepsEveryPartial_TheClearedStepIsLost()
    {
        // x and y cost 2.5 R. Step 1 (destination x): x 0 → R.  Step 2 (order: y): y 0 → R, x keeps R.
        // Step 3 (order: clear −1): nothing funded — x R, y R unchanged; R lost.  Step 4 (order: x): x R → 2 R.
        ResearchContent c = Rig(s => s.Technologies.AddRange([new Node(2, "x", Cost: 2.5 * R), new Node(3, "y", Cost: 2.5 * R)]));
        var log = new OrderLog();
        log.Append(Target(1, 3));
        log.Append(Target(2, ResearchQuery.ClearTechnologyTargetId));
        log.Append(Target(3, 2));
        TurnExecutor ex = Executor(c, log);
        WorldState w0 = Toward(2);
        WorldState w1 = ex.Step(w0);
        Assert.Equal(R, Progress(w1, 2));
        WorldState w2 = ex.Step(w1);
        Assert.Equal((R, R), (Progress(w2, 2), Progress(w2, 3)));
        WorldState w3 = ex.Step(w2);
        Assert.Equal((R, R), (Progress(w3, 2), Progress(w3, 3)));
        Assert.Equal(0.0, RpSpent(w2, w3, c));   // the cleared step: R lost
        Assert.False(HasTarget(w3, c, ResearchTree.Technology));
        WorldState w4 = ex.Step(w3);
        Assert.Equal((2 * R, R), (Progress(w4, 2), Progress(w4, 3)));
        // Over four steps: 4 R generated, 3 R in progress (x 2 R + y R), the cleared step's R the only loss.
        Assert.Equal(3 * R, Progress(w4, 2) + Progress(w4, 3));
    }

    // ================================================================== (d) a destination with no eligible node of its lane

    [Fact]
    public void D1_TechDestinationWaitingOnACivic_LosesAllRp_WhileTheFreeCivicsLaneAssists()
    {
        // td (2 R) requires the civic cv (5 000 CP). Route(td) = {cv}: no TECHNOLOGY node is eligible → the Technology lane's
        // current node is −1 (ResearchRoutePlanner.Fund; PickCurrent filters by lane). The Civics lane has no destination, so it
        // ASSISTS td's route: CP G = 1 000 000 milli → cv progress 0 → 1 000 CP, bank 0.
        // RP: 0 spent, R lost (blocked spending: RP can never pay a civic, ADR-043).
        ResearchContent c = Rig(s =>
        {
            s.Technologies.Add(new Node(2, "td", "cv", Cost: 2.0 * R));
            s.Civics.Add(new Node(2001, "cv", Cost: 5000.0));
        });
        WorldState w = Toward(2);
        long g = ResearchQuery.CivicsPointsMilli(w, c, P, 10.0);
        Assert.Equal(1_000_000L, g);
        WorldState next = Executor(c).Step(w);
        Assert.Equal(0.0, RpSpent(w, next, c));
        Assert.Equal(g / 1000.0, Progress(next, 2001));
        Assert.Equal((0L, g, g), (Cp(next).BalanceMilli, Cp(next).GeneratedMilli, Cp(next).SpentMilli));
        ResearchRoute tech = ResearchQuery.Route(next, c, P, ResearchTree.Technology);
        Assert.Equal(Key(2), tech.Destination);
        Assert.Null(tech.Current);
        Assert.Equal(RouteReason.Prerequisite, Assert.Single(tech.Blockers).Reason);
        ResearchRoute civ = ResearchQuery.Route(next, c, P, ResearchTree.Civics);
        Assert.True(civ.Assisting);
        Assert.Equal(Key(2001), civ.Current);
    }

    [Fact]
    public void D2_TechDestinationWaitingOnACivic_WithACivicsDestinationOfItsOwn_LosesAllRp_AndTheCivicWaitsToo()
    {
        // As D1, but the Civics lane works its own available destination cz (5 000 CP). Fund: lane 1 funds cz itself (no plan),
        // so the Technology lane has no plan to assist and its own route has no technology → current −1.
        // RP: R lost every step until cz completes AND then cv completes (5 + 5 steps at G = 1 000 CP). CP: all G → cz.
        ResearchContent c = Rig(s =>
        {
            s.Technologies.Add(new Node(2, "td", "cv", Cost: 2.0 * R));
            s.Civics.AddRange([new Node(2001, "cv", Cost: 5000.0), new Node(2002, "cz", Cost: 5000.0)]);
        });
        WorldState w = Toward(2, 2002);
        WorldState next = Executor(c).Step(w);
        Assert.Equal(0.0, RpSpent(w, next, c));
        Assert.Equal(0.0, Progress(next, 2001));
        Assert.Equal(1000.0, Progress(next, 2002));
        ResearchRoute tech = ResearchQuery.Route(next, c, P, ResearchTree.Technology);
        Assert.Null(tech.Current);
        Assert.Equal(RouteReason.OtherLane, Assert.Single(tech.Blockers).Reason);   // the screen: "waiting on the other lane"
        // Ten steps to unblock: R lost on each of them (the cross-tree pincer).
        WorldState x = next;
        double lost = R;
        for (int t = 1; t < 10; t++)
        {
            WorldState y = Executor(c).Step(x);
            lost += R - RpSpent(x, y, c);
            x = y;
        }
        Assert.True(Done(x, 2002) && Done(x, 2001));
        Assert.Equal(10 * R, lost);
        Assert.True(ResearchQuery.Route(x, c, P, ResearchTree.Technology).DestinationAvailable);
    }

    // ================================================================== (e) cross-tree prerequisite wait (Civics side)

    [Fact]
    public void E_CivicsDestinationWaitingOnATechnology_BanksCp_NoLoss_AndAFreeTechLaneAssistsWithRp()
    {
        // cd (5 000 CP) requires the technology t (2 R). The Technology lane works its own available destination tx (5 R).
        // Step 1: Civics lane current −1 (no civic eligible), no assist (tech lane busy) → CP spent 0, bank 0 → G (BLOCKED, banked).
        //         RP: R → tx (spent R, lost 0).
        // Step 2 (order: clear the Technology lane): the free tech lane assists cd's route → t 0 → R; bank G → 2 G.
        ResearchContent c = Rig(s =>
        {
            s.Technologies.AddRange([new Node(2, "t", Cost: 2.0 * R), new Node(3, "tx", Cost: 5.0 * R)]);
            s.Civics.Add(new Node(2001, "cd", "t", Cost: 5000.0));
        });
        var log = new OrderLog();
        log.Append(Target(1, ResearchQuery.ClearTechnologyTargetId));
        TurnExecutor ex = Executor(c, log);
        WorldState w0 = Toward(3, 2001);
        long g = ResearchQuery.CivicsPointsMilli(w0, c, P, 10.0);
        WorldState w1 = ex.Step(w0);
        Assert.Equal(R, RpSpent(w0, w1, c));
        Assert.Equal(R, Progress(w1, 3));
        Assert.Equal((g, g, 0L), (Cp(w1).BalanceMilli, Cp(w1).GeneratedMilli, Cp(w1).SpentMilli));
        Assert.Null(ResearchQuery.Route(w1, c, P, ResearchTree.Civics).Current);
        WorldState w2 = ex.Step(w1);
        Assert.Equal(R, Progress(w2, 2));
        Assert.Equal(R, RpSpent(w1, w2, c));
        Assert.True(ResearchQuery.Route(w2, c, P, ResearchTree.Technology).Assisting);
        Assert.Equal((2 * g, 2 * g, 0L), (Cp(w2).BalanceMilli, Cp(w2).GeneratedMilli, Cp(w2).SpentMilli));
        Identity(w2);
    }

    // ================================================================== (f) the step after a destination completes

    [Fact]
    public void F_TheStepAfterADestinationCompletes_WithNoNewOrder_LosesTheWholeStep()
    {
        // Step 1: x (0.3 R) completes; the destination row is REMOVED (ResearchSystem step 4: i == target[0] → −1).
        // Step 2, no order: fund[0] = −1 → R lost although y (2 R) is available. (The AI re-orders at the start of step 2 —
        // AiOrders runs on the state the step reads — so this loss is the PLAYER's; see L for the AI measurement.)
        ResearchContent c = Rig(s => s.Technologies.AddRange([new Node(2, "x", Cost: 0.3 * R), new Node(3, "y", Cost: 2.0 * R)]));
        WorldState w0 = Toward(2);
        WorldState w1 = Executor(c).Step(w0);
        Assert.True(Done(w1, 2));
        Assert.False(HasTarget(w1, c, ResearchTree.Technology));
        Assert.Null(ResearchQuery.Route(w1, c, P, ResearchTree.Technology).Destination);
        WorldState w2 = Executor(c).Step(w1);
        Assert.Equal(0.0, RpSpent(w1, w2, c));
        Assert.Equal(0.0, Progress(w2, 3));
        // Two steps: 2 R generated, 0.3 R spent, 1.7 R lost.
        Assert.Equal(2 * R - 0.3 * R, 2 * R - RpSpent(w0, w1, c) - RpSpent(w1, w2, c));
    }

    // ================================================================== (g) end of the finite tree

    [Fact]
    public void G1_FiniteTreeExhausted_NoRepeatables_NoAdmissibleDestination_AllRpLost()
    {
        // Every technology complete: IsDestinationAdmissible is false for all of them, the order builder refuses, and an order
        // naming one is ignored by the engine. RP: R lost every step, forever. CP keeps banking.
        ResearchContent c = Rig(s => s.Technologies.Add(new Node(2, "x", Cost: 0.3 * R)));
        WorldState w = WithCompleted(PlayerWorld(), 1, 2);
        Assert.Null(ResearchQuery.TargetOrder(w, c, P, Key(1)));
        Assert.Null(ResearchQuery.TargetOrder(w, c, P, Key(2)));
        var log = new OrderLog();
        log.Append(Target(0, 2));   // a hand-written order the builder would refuse
        WorldState next = Executor(c, log).Step(w);
        Assert.Equal(0.0, RpSpent(w, next, c));
        Assert.False(HasTarget(next, c, ResearchTree.Technology));
        Assert.Equal(ResearchQuery.CivicsPointsMilli(w, c, P, 10.0), Cp(next).BalanceMilli);
    }

    [Fact]
    public void G2_FiniteTreeExhausted_WithALevelledRepeatable_ItIsFundable_ButItsDestinationClearsEveryLevel()
    {
        // rep (levelled, 2 R at level 1, growth 1.25) is open once t0 is known. Destination rep:
        // Step 1: rep 0 → R.  Step 2: rep R → 2 R = cost → level 1; the destination row is REMOVED (completion clears the lane,
        // a level is a completion — ADR-049).  Step 3, no new order: R lost although level 2 (2.5 R) is available.
        ResearchContent c = Rig(s =>
        {
            s.Technologies.Add(new Node(3, "rep", "t0", Cost: 2.0 * R, Age: "A9", Levels: new LevelRule(1.25, [(Farming, 0.5)])));
            s.Recursive.Add("rep");
        }, levels: true);
        Assert.True(ResearchQuery.IsLevelled(c, c.IndexOf(Key(3))));
        WorldState w0 = WithCompleted(PlayerWorld(), 1);
        w0.ResearchTargets.Add(new ResearchTargetRow(P, Key(3)));
        WorldState w1 = Executor(c).Step(w0);
        Assert.Equal(R, Progress(w1, 3));
        WorldState w2 = Executor(c).Step(w1);
        Assert.Equal(1, ResearchQuery.Level(w2, P, Key(3)));
        Assert.False(HasTarget(w2, c, ResearchTree.Technology));
        Assert.Equal(2.5 * R, ResearchQuery.EffectiveCost(w2, c, P, c.IndexOf(Key(3))));
        WorldState w3 = Executor(c).Step(w2);
        Assert.Equal(0.0, RpSpent(w2, w3, c));
        Assert.Equal(1, ResearchQuery.Level(w3, P, Key(3)));
    }

    // ================================================================== (h) repeatable level surplus

    [Fact]
    public void H_RepeatableLevelSurplus_IsDiscarded_OneLevelPerStep()
    {
        // rep level 1 costs 0.25 R, level 2 0.25 R × 1.25 = 0.3125 R (exact binary fractions of R).
        // Step 1 (destination rep): level 1, spent 0.25 R, lost 0.75 R.  Step 2 (re-ordered): level 2, spent 0.3125 R,
        // lost 0.6875 R. A level is ONE completion per step; the rest of the step's RP goes nowhere.
        ResearchContent c = Rig(s =>
        {
            s.Technologies.Add(new Node(3, "rep", "t0", Cost: 0.25 * R, Age: "A9", Levels: new LevelRule(1.25, [(Farming, 0.5)])));
            s.Recursive.Add("rep");
        }, levels: true);
        var log = new OrderLog();
        log.Append(Target(1, 3));
        TurnExecutor ex = Executor(c, log);
        WorldState w0 = WithCompleted(PlayerWorld(), 1);
        w0.ResearchTargets.Add(new ResearchTargetRow(P, Key(3)));
        WorldState w1 = ex.Step(w0);
        Assert.Equal(1, ResearchQuery.Level(w1, P, Key(3)));
        Assert.Equal(0.25 * R, RpSpent(w0, w1, c));
        WorldState w2 = ex.Step(w1);
        Assert.Equal(2, ResearchQuery.Level(w2, P, Key(3)));
        Assert.Equal(0.3125 * R, RpSpent(w1, w2, c));
        Assert.Equal(2 * R - 0.5625 * R, 2 * R - RpSpent(w0, w1, c) - RpSpent(w1, w2, c));   // 1 437.5 RP lost of 2 000
    }

    // ================================================================== (i) CP: banked surplus, identity, one civic per step

    [Fact]
    public void I1_Cp_SurplusIsBanked_TheIdentityHolds_AndOnlyOneCivicIsPaidPerStep()
    {
        // c1 300 CP (destination), c2 100 CP (available, not named). G = 1 000 000 milli-CP a step.
        // Step 1: balance 0 + G = 1 000 000 → spend min(1 000 000, 300 000) = 300 000 → c1 complete, balance 700 000.
        //         c2 gets nothing although 700 CP sit in the bank (one civic per step; no overflow).
        // Step 2 (no civic destination): balance 1 700 000, generated 2 000 000, spent 300 000.
        // Step 3 (order c2): spend 100 000 → balance 2 600 000, generated 3 000 000, spent 400 000; c2 complete.
        ResearchContent c = Rig(s => s.Civics.AddRange([new Node(2001, "c1", Cost: 300.0), new Node(2002, "c2", Cost: 100.0)]));
        var log = new OrderLog();
        log.Append(Target(2, 2002));
        TurnExecutor ex = Executor(c, log);
        WorldState w0 = Toward(2001);
        Assert.Equal(1_000_000L, ResearchQuery.CivicsPointsMilli(w0, c, P, 10.0));
        WorldState w1 = ex.Step(w0);
        Assert.True(Done(w1, 2001));
        Assert.Equal(0.0, Progress(w1, 2002));
        Assert.Equal((700_000L, 1_000_000L, 300_000L), (Cp(w1).BalanceMilli, Cp(w1).GeneratedMilli, Cp(w1).SpentMilli));
        WorldState w2 = ex.Step(w1);
        Assert.Equal((1_700_000L, 2_000_000L, 300_000L), (Cp(w2).BalanceMilli, Cp(w2).GeneratedMilli, Cp(w2).SpentMilli));
        WorldState w3 = ex.Step(w2);
        Assert.True(Done(w3, 2002));
        Assert.Equal((2_600_000L, 3_000_000L, 400_000L), (Cp(w3).BalanceMilli, Cp(w3).GeneratedMilli, Cp(w3).SpentMilli));
        Identity(w1); Identity(w2); Identity(w3);
    }

    [Fact]
    public void I2_Cp_ACompletingSpendRoundsUpToTheMilliCp_TheFractionIsSpentButNotCredited()
    {
        // c1 costs 100.0004 CP. SpendCivics: needMilli = ceil(100.0004 × 1000) = 100 001 → spent 100 001 milli-CP, progress set to
        // EXACTLY the cost (100.0004). 100.001 − 100.0004 = 0.0006 CP left the bank without reaching any node: < 1 milli-CP per
        // civic completion, the rounding of an integer bank onto a double cost. Identity still exact.
        ResearchContent c = Rig(s => s.Civics.Add(new Node(2001, "c1", Cost: 100.0004)));
        int i = c.IndexOf(Key(2001));
        double cost = ResearchQuery.EffectiveCost(PlayerWorld(), c, P, i);
        WorldState w0 = Toward(2001);
        WorldState w1 = Executor(c).Step(w0);
        Assert.True(Done(w1, 2001));
        long need = (long)Math.Ceiling(cost * 1000.0);
        Assert.Equal(100_001L, need);
        Assert.Equal(need, Cp(w1).SpentMilli);
        Assert.True(need / 1000.0 > cost);   // spent beyond what was credited
        Identity(w1);
    }

    // ================================================================== (j) save / load

    [Fact]
    public void J_SaveLoad_PreservesTheCpBank_Progress_Destinations_AndLevels_Exactly()
    {
        // Built state: rep level 1 (step 1), x R (step 2), y R and destination y (step 3); civic destination cd waits on y
        // (cross-tree), so the bank holds 3 G with no spend. Snapshot.Save → Snapshot.Load must give every row back bit for bit,
        // the same WorldHash, the same routes, and the same next step.
        ResearchContent c = Rig(s =>
        {
            s.Technologies.AddRange([new Node(2, "x", Cost: 2.5 * R), new Node(3, "y", Cost: 2.5 * R),
                new Node(4, "rep", "t0", Cost: 0.25 * R, Age: "A9", Levels: new LevelRule(1.25, [(Farming, 0.5)]))]);
            s.Recursive.Add("rep");
            s.Civics.Add(new Node(2001, "cd", "y", Cost: 5000.0));
        }, levels: true);
        var log = new OrderLog();
        log.Append(Target(1, 2));
        log.Append(Target(2, 3));
        TurnExecutor ex = Executor(c, log);
        WorldState w = WithCompleted(PlayerWorld(), 1);
        w.ResearchTargets.Add(new ResearchTargetRow(P, Key(4)));
        w.ResearchTargets.Add(new ResearchTargetRow(P, Key(2001)));
        w = Run(ex, w, 3);
        Assert.Equal(1, ResearchQuery.Level(w, P, Key(4)));
        Assert.Equal((R, R), (Progress(w, 2), Progress(w, 3)));
        Assert.Equal((3_000_000L, 3_000_000L, 0L), (Cp(w).BalanceMilli, Cp(w).GeneratedMilli, Cp(w).SpentMilli));
        Assert.True(HasTarget(w, c, ResearchTree.Technology) && HasTarget(w, c, ResearchTree.Civics));

        using var buffer = new MemoryStream();
        Snapshot.Save(w, buffer);
        buffer.Position = 0;
        WorldState loaded = Snapshot.Load(buffer);
        Assert.Equal(WorldHash.ComputeHex(w), WorldHash.ComputeHex(loaded));
        Assert.Equal(Rows(w.CivicsPoints), Rows(loaded.CivicsPoints));
        Assert.Equal(Rows(w.ResearchProgress), Rows(loaded.ResearchProgress));
        Assert.Equal(Rows(w.ResearchTargets), Rows(loaded.ResearchTargets));
        Assert.Equal(Rows(w.ResearchLevels), Rows(loaded.ResearchLevels));
        Assert.Equal(Rows(w.ResearchCompleted), Rows(loaded.ResearchCompleted));
        foreach (ResearchTree lane in new[] { ResearchTree.Technology, ResearchTree.Civics })
        {
            ResearchRoute a = ResearchQuery.Route(w, c, P, lane), b = ResearchQuery.Route(loaded, c, P, lane);
            Assert.Equal((a.Destination, a.Current, a.Next, a.RemainingCost), (b.Destination, b.Current, b.Next, b.RemainingCost));
        }
        Assert.Equal(WorldHash.ComputeHex(ex.Step(w)), WorldHash.ComputeHex(ex.Step(loaded)));
    }

    private static List<T> Rows<T>(IReadOnlyTable<T> table) where T : unmanaged
    {
        var rows = new List<T>(table.Count);
        for (int i = 0; i < table.Count; i++) rows.Add(table[i]);
        return rows;
    }

    // ================================================================== (k) the Glass Box research cards (InfoQuery)

    [Fact]
    public void K1_LearningCard_EveryNumber_IsResearchQuerys_WhenTheDestinationIsTheFundedNode()
    {
        // One step toward x (2.5 R): x holds R; the bank holds G (no civic destination). The card shows ResearchQuery's numbers.
        ResearchContent c = Rig(s => s.Technologies.Add(new Node(2, "x", Cost: 2.5 * R)));
        WorldState w = Executor(c).Step(Toward(2));
        int x = c.IndexOf(Key(2));
        string expected = "Researching X: " + N0(ResearchQuery.Progress(w, P, Key(2))) + " of " + N0(ResearchQuery.EffectiveCost(w, c, P, x))
            + " research points, +" + N1(ResearchQuery.ResearchPointPool(w, c, P)) + " a turn. Civics: no civics target; "
            + N1(ResearchQuery.CivicsPointsBalance(w, P)) + " civics points banked, +"
            + N1(ResearchQuery.CivicsPointsMilli(w, c, P, w.Clock.DtYears) / 1000.0) + " a turn.";
        Assert.Equal(expected, LearningLine(w, c));
        Assert.Equal("Researching X: 1,000 of 2,500 research points, +1,000 a turn. Civics: no civics target; 1,000 civics points banked, +1,000 a turn.",
            LearningLine(w, c));
    }

    [Fact]
    public void K2_LearningCard_WithRouting_ShowsTheDestinationsZeroProgress_NotTheRouteNodeTheStepFunds()
    {
        // DISPLAY DISCREPANCY. Destination d (2 R) needs x (2.5 R). The step funds x (Route.Current); the card names d and d's
        // progress: "Researching D: 0 of 2,000" — while x holds R. And x's own card says "Open to research now", never
        // "Being researched" (InfoQuery NodeCard requires target && available; the route node is not the target).
        ResearchContent c = Rig(s => s.Technologies.AddRange([new Node(2, "x", Cost: 2.5 * R), new Node(3, "d", "x", Cost: 2.0 * R)]));
        WorldState w = Executor(c).Step(Toward(3));
        Assert.Equal(R, Progress(w, 2));
        Assert.Equal(Key(2), ResearchQuery.Route(w, c, P, ResearchTree.Technology).Current);
        Assert.StartsWith("Researching D: 0 of 2,000 research points, +1,000 a turn.", LearningLine(w, c));
        Assert.StartsWith("Open to research now:", NodeLine(w, c, 2));
        Assert.StartsWith("Locked:", NodeLine(w, c, 3));
    }

    [Fact]
    public void K3_LearningCard_AFreeTechLaneThatAssists_IsReportedIdleAndUnused_WhileItsRpIsSpent()
    {
        // DISPLAY DISCREPANCY. No technology destination; the civic destination cd needs the technology t (2 R). The free tech
        // lane ASSISTS: the step adds R to t (spent R, lost 0). The card still says "Idle: 1,000 research points a turn go unused".
        ResearchContent c = Rig(s =>
        {
            s.Technologies.Add(new Node(2, "t", Cost: 2.0 * R));
            s.Civics.Add(new Node(2001, "cd", "t", Cost: 5000.0));
        });
        WorldState w0 = Toward(2001);
        WorldState w1 = Executor(c).Step(w0);
        Assert.Equal(R, RpSpent(w0, w1, c));
        Assert.True(ResearchQuery.Route(w1, c, P, ResearchTree.Technology).Assisting);
        Assert.StartsWith("Idle: 1,000 research points a turn go unused;", LearningLine(w1, c));
    }

    [Fact]
    public void K4_LearningCard_ALaneWaitingOnTheOtherLane_ReadsResearching_WhileEveryRpIsLost()
    {
        // DISPLAY DISCREPANCY (hides the loss). D2's state: td waits on the civic cv behind the Civics lane's own cz. RP lost R a
        // step; the card reads "Researching TD: 0 of 2,000 research points, +1,000 a turn." — no idle or waiting word.
        ResearchContent c = Rig(s =>
        {
            s.Technologies.Add(new Node(2, "td", "cv", Cost: 2.0 * R));
            s.Civics.AddRange([new Node(2001, "cv", Cost: 5000.0), new Node(2002, "cz", Cost: 5000.0)]);
        });
        WorldState w0 = Toward(2, 2002);
        WorldState w1 = Executor(c).Step(w0);
        Assert.Equal(0.0, RpSpent(w0, w1, c));
        Assert.StartsWith("Researching TD: 0 of 2,000 research points, +1,000 a turn.", LearningLine(w1, c));
    }

    [Fact]
    public void K5_CpPerTurnShown_IsTheLastStepsDt_NotTheNextSteps_AtTurnZeroAndAtABandBoundary()
    {
        // DISPLAY DISCREPANCY. Every CP-a-turn figure (InfoQuery cards, ResearchSnapshot.CivicsPerTurn, the action surface's
        // CivicsLine) is CivicsPointsMilli(world, …, world.Clock.DtYears): the dt of the step that PRODUCED the world. The next
        // step accrues with the era table's dt at the current date (TurnExecutor (1)).
        // Turn 0: Clock.DtDays = 0 → shown "+0 a turn"; the first step accrues G = 1 000 CP.
        ResearchContent c = Rig(_ => { });
        WorldState w0 = PlayerWorld();
        Assert.Equal(0.0, w0.Clock.DtYears);
        Assert.EndsWith("0 civics points banked, +0 a turn.", LearningLine(w0, c));
        WorldState w1 = Executor(c).Step(w0);
        Assert.Equal(1_000_000L, Cp(w1).GeneratedMilli);
        // A band boundary: dt 10 for years [0, 20), dt 5 after. After two steps the clock says dt 10 (shown "+1,000 a turn"),
        // but the third step runs at dt 5 and accrues 500 CP.
        var era = EraTableLoader.Load("""{ "bands": [ { "name": "a", "startYear": 0, "endYear": 20, "dtYears": 10 }, { "name": "b", "startYear": 20, "endYear": 100000, "dtYears": 5 } ] }""");
        var ex = new TurnExecutor(era, [SystemCatalog.Research(TestConfigs.Sim() with { Research = c })]);
        WorldState b2 = Run(ex, PlayerWorld(), 2);
        Assert.Equal(10.0, b2.Clock.DtYears);
        Assert.EndsWith("civics points banked, +1,000 a turn.", LearningLine(b2, c));
        WorldState b3 = ex.Step(b2);
        Assert.Equal(500_000L, Cp(b3).GeneratedMilli - Cp(b2).GeneratedMilli);
    }

    // ================================================================== (l) the AI on the canonical world

    /// <summary>
    /// (l) MEASURED: the canonical world (TestConfigs.Worldgen(), 1 AI Empire, seed 42), the shipped content, 400 turns. Per
    /// step and lane, the engine's own funding is reconstructed from PREV plus this step's AI orders (ResearchQuery.Destinations,
    /// the orders applied as ResearchSystem applies them, ResearchRoutePlanner.Fund) and checked against the step's outcome.
    /// Each step is classified: NO DESTINATION (with / without a fundable node of the lane), WAITING (a destination but no
    /// fundable node, no assist), ASSISTING, or FUNDED; RP lost (pool − RP added to technology progress, credits excluded) is
    /// summed per class. CP is never lost; its idle steps are counted and the milli-CP rounding of completions measured.
    /// </summary>
    [Fact]
    public void L_Ai_CanonicalWorld_Seed42_400Turns_NeverIdlesALaneWithFundableNodes()
    {
        SimConfig cfg = TestConfigs.Sim();
        ResearchContent rc = cfg.Research!;
        Assert.False(rc.Tuning.ResearchOverflow);
        WorldgenConfig shipped = TestConfigs.Worldgen();
        var log = new OrderLog();
        TurnExecutor exec;
        using (var era = Sim.Data.DataFiles.OpenEraPacing())
        using (var pipe = Sim.Data.DataFiles.OpenPipeline())
            exec = new TurnExecutor(EraTableLoader.Load(era), PipelineLoader.Load(pipe, SystemCatalog.All(cfg, shipped)), log);
        WorldState w = WorldFounding.Found(shipped with { AiEmpires = 1 }, cfg, 42);
        var ai = new PolityId(2);
        Assert.True(EmpireQuery.TryGetCommandSource(w, ai, out CommandSource src) && src == CommandSource.Ai);
        int n = rc.Nodes.Count;

        // [lane, class]: 0 no destination + fundable, 1 no destination + nothing fundable, 2 waiting, 3 assisting, 4 funded.
        var steps = new int[2, 5];
        var lost = new double[5];
        double generated = 0.0, spentTotal = 0.0, civicAdded = 0.0;
        int mismatches = 0;
        const int Turns = 400;
        for (int t = 0; t < Turns; t++)
        {
            OrderRecord[] orders = AiOrders.For(w, cfg, shipped);
            foreach (OrderRecord o in orders) log.Append(o);
            WorldState prev = w;
            bool[] completed = ResearchQuery.CompletedMask(prev, rc, ai);
            bool[] available = ResearchQuery.AvailableMask(rc, completed, ResearchQuery.LevelMask(prev, rc, ai, completed));
            int[] dest = ResearchQuery.Destinations(prev, rc, ai, completed);
            foreach (OrderRecord o in orders)
            {
                if (o.Kind != OrderKind.SetResearchTarget || o.ActorId != ai.Value) continue;
                if (o.TargetId == ResearchQuery.ClearTechnologyTargetId) { dest[0] = -1; continue; }
                if (o.TargetId == ResearchQuery.ClearCivicsTargetId) { dest[1] = -1; continue; }
                int idx = rc.IndexOf(new ResearchNodeId(o.TargetId));
                if (ResearchQuery.IsDestinationAdmissible(rc, idx, completed)) dest[ResearchRoutePlanner.LaneOf(rc, idx, true)] = idx;
            }
            double[] remaining = ResearchQuery.RemainingCosts(prev, rc, ai);
            LaneFunding[] lanes = ResearchRoutePlanner.Fund(rc, completed, available, remaining, dest, true);
            var fundable = new bool[2];
            for (int i = 0; i < n; i++) if (available[i]) fundable[rc.Nodes[i].Tree == ResearchTree.Technology ? 0 : 1] = true;

            double pool = ResearchQuery.ResearchPointPool(prev, rc, ai);
            double[] before = ProgressOf(prev, rc, ai);
            var cost = new double[n];
            for (int i = 0; i < n; i++) cost[i] = ResearchQuery.EffectiveCost(prev, rc, ai, i);
            (double techCreditsBefore, double civicCreditsBefore) = Credits(prev, rc, ai);
            w = exec.Step(prev);
            bool[] done = ResearchQuery.CompletedMask(w, rc, ai);
            double[] after = ProgressOf(w, rc, ai);
            double techAdded = 0.0, civAdded = 0.0;
            for (int i = 0; i < n; i++)
            {
                bool lv = ResearchQuery.Level(w, ai, rc.Nodes[i].Key) > ResearchQuery.Level(prev, ai, rc.Nodes[i].Key);
                double reached = (done[i] && !completed[i]) || lv ? cost[i] : after[i];
                if (rc.Nodes[i].Tree == ResearchTree.Technology) techAdded += reached - before[i];
                else civAdded += reached - before[i];
            }
            (double techCreditsAfter, double civicCreditsAfter) = Credits(w, rc, ai);
            double spent = Math.Min(pool, Math.Max(0.0, techAdded - (techCreditsAfter - techCreditsBefore)));
            civicAdded += civAdded - (civicCreditsAfter - civicCreditsBefore);
            generated += pool;
            spentTotal += spent;

            for (int lane = 0; lane < 2; lane++)
            {
                int k = dest[lane] < 0
                    ? (lanes[lane].Current >= 0 ? 3 : fundable[lane] ? 0 : 1)
                    : (lanes[lane].Current < 0 ? 2 : 4);
                steps[lane, k]++;
                if (lane == 0) lost[k] += pool - spent;
                // The reconstruction must be the engine's: a funded node gained progress or completed / levelled.
                int cur = lanes[lane].Current;
                if (cur >= 0 && !(after[cur] > before[cur] || (done[cur] && !completed[cur])
                    || ResearchQuery.Level(w, ai, rc.Nodes[cur].Key) > ResearchQuery.Level(prev, ai, rc.Nodes[cur].Key))
                    && (lane == 0 ? pool > 0.0 : ResearchQuery.CivicsPointsAccount(prev, ai).BalanceMilli + ResearchQuery.CivicsPointsMilli(prev, rc, ai, w.Clock.DtYears) > 0))
                    mismatches++;
            }
        }
        CivicsPointsRow account = ResearchQuery.CivicsPointsAccount(w, ai);
        string[] names = ["no-destination+fundable", "no-destination+nothing", "waiting", "assisting", "funded"];
        var sb = new StringBuilder();
        sb.AppendLine(CultureInfo.InvariantCulture, $"seed 42, {Turns} turns, AI polity {ai.Value}, end turn {w.Clock.Turn}, Age {AgeQuery.CurrentAge(w, cfg.Ages!, ai)}");
        for (int lane = 0; lane < 2; lane++)
        {
            sb.Append(lane == 0 ? "  technology steps:" : "  civics steps:    ");
            for (int k = 0; k < 5; k++) sb.Append(CultureInfo.InvariantCulture, $" {names[k]}={steps[lane, k]}");
            sb.AppendLine();
        }
        sb.Append("  RP lost by technology class:");
        for (int k = 0; k < 5; k++) sb.Append(CultureInfo.InvariantCulture, $" {names[k]}={lost[k]:0.###}");
        sb.AppendLine();
        sb.AppendLine(CultureInfo.InvariantCulture, $"  RP generated {generated:0.###} spent {spentTotal:0.###} lost {generated - spentTotal:0.###} ({100.0 * (generated - spentTotal) / generated:0.##}%)");
        sb.AppendLine(CultureInfo.InvariantCulture, $"  CP generated {account.GeneratedMilli} spent {account.SpentMilli} banked {account.BalanceMilli} milli; civic progress added {civicAdded * 1000.0:0.###} milli; spend-rounding {account.SpentMilli - civicAdded * 1000.0:0.###} milli");
        sb.AppendLine(CultureInfo.InvariantCulture, $"  reconstruction mismatches {mismatches}");
        output.WriteLine(sb.ToString());

        Assert.Equal(0, mismatches);
        // THE FINDING (pinned): the AI never leaves a lane without a destination while that lane has a fundable node.
        Assert.Equal(0, steps[0, 0]);
        Assert.Equal(0, steps[1, 0]);
        Assert.Null(ResearchQuery.CivicsPointsInvariantViolation(w.CivicsPoints));
    }

    private static double[] ProgressOf(IReadOnlyWorldState w, ResearchContent rc, PolityId polity)
    {
        var p = new double[rc.Nodes.Count];
        for (int r = 0; r < w.ResearchProgress.Count; r++)
        {
            ResearchProgressRow row = w.ResearchProgress[r];
            if (row.Polity.Value != polity.Value) continue;
            int i = rc.IndexOf(row.Node);
            if (i >= 0) p[i] = row.Progress;
        }
        return p;
    }

    private static (double Tech, double Civic) Credits(IReadOnlyWorldState w, ResearchContent rc, PolityId polity)
    {
        double tech = 0.0, civic = 0.0;
        for (int r = 0; r < w.ResearchCredits.Count; r++)
        {
            ResearchCreditRow row = w.ResearchCredits[r];
            if (row.Polity.Value != polity.Value) continue;
            int i = rc.IndexOf(row.Node);
            if (i < 0) continue;
            if (rc.Nodes[i].Tree == ResearchTree.Technology) tech += row.Amount;
            else civic += row.Amount;
        }
        return (tech, civic);
    }
}
