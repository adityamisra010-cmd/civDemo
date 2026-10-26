using Sim.Core;
using Sim.Core.Kernel;
using Sim.Core.State;
using Sim.Core.Systems;
using Sim.Core.Systems.Research;
using Sim.Tests.TestUtil;
using static Sim.Tests.TestUtil.ResearchRigs;

namespace Sim.Tests.Systems;

/// <summary>
/// ADR-052 (PROPOSED; Director ruling 2026-10-26) — GATE-AWARE TECHNOLOGY GOALS (research.json tuning.aiGateAwareGoals,
/// AiResearchPolicy.DecideLane / ChooseDestination / FundableDestination). With the two lanes, destination routing and the
/// Age sweep, an AI's technology destination of Age a+2 waits on the Age-a CIVICS cohort: the engine's planner gives the
/// technology lane no current node and its RP is lost. Switch on, the AI re-decides such a destination and takes the first
/// goal sink it can fund now (existing priority order), else the cheapest available technology, else nothing. Switch off,
/// it waits — pinned here as today's behaviour. Rigs: an AI Empire of 10 000 people (1 000 RP and 1 000 CP a dt-10 turn),
/// goals from sim.json-shaped capability requirements (the tax requirement and a road class), the sweep on.
/// </summary>
public class AiGateAwareGoalTests
{
    private const int Ai = 2;
    private static readonly PolityId P = new(Ai);

    //  tech:   a (A1, 1000) · p (A1, 1000, a) · g3 (A3, 2000, p) — g3 is swept: it needs every Age-1 node, civics included
    //          alt (A2, 4000, a) — the road goal · f1 (A2, 1500, a) · f2 (A2, 1600, a) — no goal (fallback fillers)
    //  civics: law (A1, lawCost, a) — the Age-1 civics cohort the gate waits on
    //  goals:  tax requires g3 (ordinal 2, remaining p + g3 = 3000) · road class Trackway's entity requires alt (ordinal 1, 4000)
    private static Spec GateSpec(bool on, double lawCost)
    {
        var s = new Spec { Stage = "a", AgeSweep = true, DestinationRouting = true, CivicsPoints = (true, 1.0, 0.5), AiGateAwareGoals = on };
        s.Technologies.AddRange([
            new Node(1, "a", Cost: 1000.0), new Node(2, "p", "a", Cost: 1000.0), new Node(3, "g3", "p", Cost: 2000.0, Age: "A3"),
            new Node(4, "alt", "a", Cost: 4000.0, Age: "A2"),
            new Node(5, "f1", "a", Cost: 1500.0, Age: "A2"), new Node(6, "f2", "a", Cost: 1600.0, Age: "A2"),
        ]);
        s.Civics.Add(new Node(1001, "law", "a", Cost: lawCost));
        s.Entities.Add(new Entity("infra.track", "infrastructure", "alt"));
        return s;
    }

    /// <summary>The AI's configuration: the rig research, no Age content, no institutions; the tax requirement and ONE road
    /// class (Trackway) name rig nodes, so the AI's goals are the tax goal and the road goal.</summary>
    private static SimConfig AiConfig(ResearchContent c, string taxRequires, params (int EdgeType, string Entity)[] roads)
    {
        SimConfig sim = TestConfigs.Sim();
        var classes = new RoadClassConfig[roads.Length];
        for (int k = 0; k < roads.Length; k++) classes[k] = new RoadClassConfig(roads[k].EdgeType, roads[k].Entity, 1.0, 1, []);
        return sim with
        {
            Research = c, Ages = null, Institutions = null,
            Governance = sim.Governance! with { TaxationRequires = taxRequires, TaxationMinAge = null },
            Roads = sim.Roads! with { Classes = classes },
        };
    }

    private static SimConfig GateConfig(ResearchContent c) => AiConfig(c, "g3", (EdgeTypes.Trackway, "infra.track"));

    private static WorldState AiWorld(ResearchContent c, int polity, params string[] done)
    {
        WorldState w = World([polity], [polity], 10_000);
        foreach (string id in done) w.ResearchCompleted.Add(new ResearchCompletedRow(new PolityId(polity), c.Nodes[c.IndexOfId(id)].Key));
        return w;
    }

    private static WorldState Holding(ResearchContent c, WorldState w, int polity, params string[] destinations)
    {
        foreach (string id in destinations) w.ResearchTargets.Add(new ResearchTargetRow(new PolityId(polity), c.Nodes[c.IndexOfId(id)].Key));
        return w;
    }

    /// <summary>One step as the engine runs it: the AI's orders for this turn (DecideAll, appended to the log the executor
    /// reads), and the technology lane's funded node reconstructed with the engine's own planner over the destinations those
    /// orders leave (ResearchPacingRunner's reconstruction).</summary>
    private sealed record Step(long Turn, int Destination, int Funded, bool TechAvailable, OrderRecord[] Orders, bool[] Available);

    private static (List<Step> Steps, WorldState End) Drive(ResearchContent c, SimConfig cfg, WorldState w, int turns, int polity = Ai)
    {
        var log = new OrderLog();
        TurnExecutor exec = Executor(c, log);
        var steps = new List<Step>(turns);
        var id = new PolityId(polity);
        for (int t = 0; t < turns; t++)
        {
            OrderRecord[] orders = AiResearchPolicy.DecideAll(w, cfg, id);
            foreach (OrderRecord o in orders) log.Append(o);
            bool[] completed = ResearchQuery.CompletedMask(w, c, id);
            int[] dest = ResearchQuery.Destinations(w, c, id, completed);
            foreach (OrderRecord o in orders)
            {
                int i = c.IndexOf(new ResearchNodeId(o.TargetId));
                if (ResearchQuery.IsDestinationAdmissible(c, i, completed)) dest[ResearchRoutePlanner.LaneOf(c, i, true)] = i;
            }
            bool[] available = ResearchQuery.AvailableMask(c, completed, ResearchQuery.LevelMask(w, c, id, completed));
            LaneFunding[] lanes = ResearchRoutePlanner.Fund(c, completed, available, ResearchQuery.RemainingCosts(w, c, id), dest, true);
            bool techAvailable = false;
            for (int i = 0; i < c.Nodes.Count; i++) techAvailable |= available[i] && c.Nodes[i].Tree == ResearchTree.Technology;
            steps.Add(new Step(w.Clock.Turn, dest[0], lanes[0].Current, techAvailable, orders, available));
            w = exec.Step(w);
        }
        return (steps, w);
    }

    private static string IdOf(ResearchContent c, int i) => i < 0 ? "-" : c.Nodes[i].Id;

    private static string Trace(ResearchContent c, List<Step> steps)
    {
        var parts = new List<string>(steps.Count);
        foreach (Step s in steps) parts.Add(IdOf(c, s.Destination) + ">" + IdOf(c, s.Funded));
        return string.Join(" ", parts);
    }

    private static int WaitingSteps(List<Step> steps)
    {
        int n = 0;
        foreach (Step s in steps) if (s.Destination >= 0 && s.Funded < 0) n++;
        return n;
    }

    // ------------------------------------------------------------------ (a) the blocked destination

    [Fact]
    public void A_BlockedBySweepCohort_SwitchOff_TheAiWaits_SwitchOn_ItRetargetsToAFundableGoal()
    {
        foreach (bool on in new[] { false, true })
        {
            ResearchContent c = GateSpec(on, 10_000.0).Load();
            SimConfig cfg = GateConfig(c);
            // p is known: g3's route is law (civic, the Age-1 cohort) and g3 itself — nothing the technology lane can fund.
            WorldState w = Holding(c, AiWorld(c, Ai, "a", "p"), Ai, "g3", "law");
            ResearchRoute route = ResearchQuery.Route(w, c, P, ResearchTree.Technology);
            Assert.Null(route.Current);
            Assert.Equal(LaneActivity.Waiting, ResearchQuery.Activity(route));
            Assert.Contains(route.Blockers, b => b.Cause == RouteReason.Sweep && b.Key.Value == 1001);
            Assert.False(AiResearchPolicy.TechnologyFundable(w, c, P, ResearchQuery.CompletedMask(w, c, P),
                ResearchQuery.AvailableMask(c, ResearchQuery.CompletedMask(w, c, P)), c.IndexOfId("g3")));

            OrderRecord? order = AiResearchPolicy.DecideLane(w, cfg, P, ResearchTree.Technology);
            if (!on)
            {
                Assert.Null(order);                                                  // today: the destination is kept and waits
                continue;
            }
            Assert.NotNull(order);
            Assert.Equal((OrderKind.SetResearchTarget, 4L), (order!.Value.Kind, (long)order.Value.TargetId));   // alt: the road goal
            // The new destination is funded this very step; g3's progress rows are untouched (nothing is lost).
            (List<Step> steps, _) = Drive(c, cfg, w, 1);
            Assert.Equal((c.IndexOfId("alt"), c.IndexOfId("alt")), (steps[0].Destination, steps[0].Funded));
            Assert.Equal(c.IndexOfId("alt"), AiResearchPolicy.ChooseDestination(w, cfg, P, ResearchQuery.CompletedMask(w, c, P),
                ResearchQuery.AvailableMask(c, ResearchQuery.CompletedMask(w, c, P)), ResearchTree.Technology));
        }
    }

    [Fact]
    public void A_AFundableHeldDestination_IsNeverSwitched_NoChurn()
    {
        ResearchContent c = GateSpec(true, 10_000.0).Load();
        SimConfig cfg = GateConfig(c);
        // g3 held with p still unknown: the lane funds p — fundable, so it is kept although a cheaper goal sink exists.
        WorldState w = Holding(c, AiWorld(c, Ai, "a"), Ai, "g3", "law");
        Assert.Null(AiResearchPolicy.DecideLane(w, cfg, P, ResearchTree.Technology));
        // f2 held (no goal at all, the dearer filler): fundable, kept.
        WorldState f = Holding(c, AiWorld(c, Ai, "a", "p"), Ai, "f2", "law");
        Assert.Null(AiResearchPolicy.DecideLane(f, cfg, P, ResearchTree.Technology));
    }

    [Fact]
    public void A_FromAnEmptyLane_TheExistingChoiceIsKeptWhenFundable_AndSkippedWhenNot()
    {
        foreach (bool on in new[] { false, true })
        {
            ResearchContent c = GateSpec(on, 10_000.0).Load();
            SimConfig cfg = GateConfig(c);
            // Nothing held, p unknown: the tax goal (3000) beats the road goal (4000) and its sink g3 is fundable (p) —
            // the same destination on and off.
            WorldState fresh = Holding(c, AiWorld(c, Ai, "a"), Ai, "law");
            Assert.Equal(3L, (long)AiResearchPolicy.DecideLane(fresh, cfg, P, ResearchTree.Technology)!.Value.TargetId);
        }
        // A stale tax goal: g3 is reachable only through a NEW available closure node — none here, so the tax goal is not
        // admitted at all; on and off agree on alt. The gate-aware difference is the held-destination case above.
        ResearchContent off = GateSpec(false, 10_000.0).Load();
        WorldState w = Holding(off, AiWorld(off, Ai, "a", "p"), Ai, "law");
        Assert.Equal(4L, (long)AiResearchPolicy.DecideLane(w, GateConfig(off), P, ResearchTree.Technology)!.Value.TargetId);
    }

    // ------------------------------------------------------------------ (b) nothing eligible

    [Fact]
    public void B_NoTechnologyAvailable_NoOrder_TheLaneFundsNothing_AndTheGatedDestinationIsKept()
    {
        ResearchContent c = GateSpec(true, 10_000.0).Load();
        SimConfig cfg = GateConfig(c);
        WorldState held = Holding(c, AiWorld(c, Ai, "a", "p", "alt", "f1", "f2"), Ai, "g3", "law");
        Assert.Null(AiResearchPolicy.DecideLane(held, cfg, P, ResearchTree.Technology));
        Assert.Empty(AiResearchPolicy.DecideAll(held, cfg, P));
        (List<Step> steps, WorldState after) = Drive(c, cfg, held, 1);
        Assert.Equal((c.IndexOfId("g3"), -1), (steps[0].Destination, steps[0].Funded));   // waits; RP is not sent to law
        Assert.True(ResearchQuery.TryGetTarget(after, c, P, ResearchTree.Technology, out ResearchNodeId kept));
        Assert.Equal(3, kept.Value);
        Assert.Equal(1000.0, ResearchQuery.Progress(after, P, new ResearchNodeId(1001)));      // law: this turn's CP only

        // No destination at all: nothing to order (the empty-lane rule's case, not the AI's).
        WorldState empty = Holding(c, AiWorld(c, Ai, "a", "p", "alt", "f1", "f2"), Ai, "law");
        Assert.Null(AiResearchPolicy.DecideLane(empty, cfg, P, ResearchTree.Technology));
        Assert.Equal(-1, AiResearchPolicy.ChooseDestination(empty, cfg, P, ResearchQuery.CompletedMask(empty, c, P),
            ResearchQuery.AvailableMask(c, ResearchQuery.CompletedMask(empty, c, P)), ResearchTree.Technology));
    }

    // ------------------------------------------------------------------ (c) a long wait

    [Fact]
    public void C_LongWait_TheLaneNeverFundsNothingWhileATechnologyIsAvailable()
    {
        // law costs 30 turns of CP; the technology lane has 1 (p) + 4 (alt) + 2 (f1) + 2 (f2) turns of fundable work.
        const int Turns = 34;
        var waiting = new int[2];
        var traces = new string[2];
        foreach (bool on in new[] { false, true })
        {
            ResearchContent c = GateSpec(on, 30_000.0).Load();
            (List<Step> steps, WorldState end) = Drive(c, GateConfig(c), AiWorld(c, Ai, "a"), Turns);
            waiting[on ? 1 : 0] = WaitingSteps(steps);
            traces[on ? 1 : 0] = Trace(c, steps);
            foreach (Step s in steps)
            {
                if (on) Assert.True(!s.TechAvailable || s.Funded >= 0, $"turn {s.Turn}: a technology is available and the lane funds nothing");
                if (s.Funded >= 0) Assert.Equal(ResearchTree.Technology, c.Nodes[s.Funded].Tree);   // RP never pays a civic
                if (s.Destination >= 0) Assert.Equal(ResearchTree.Technology, c.Nodes[s.Destination].Tree);
            }
            Assert.True(Done(end, 3, Ai), "g3 completes once the gate opens");
        }
        // Switch off: after p (turn 0) the lane holds g3 and waits until law completes (30 turns of CP: turns 0–29).
        Assert.Equal(29, waiting[0]);
        Assert.StartsWith("g3>p g3>- g3>-", traces[0]);
        // Switch on: alt, f1, f2 fill the gate (8 funded steps); then nothing is available — the lane has NO destination
        // and funds nothing (no waiting destination), until the gate opens and g3 is taken.
        Assert.Equal(0, waiting[1]);
        Assert.StartsWith("g3>p alt>alt alt>alt alt>alt alt>alt f1>f1 f1>f1 f2>f2 f2>f2 ->-", traces[1]);
    }

    // ------------------------------------------------------------------ (d) repeated gates

    //  tech:   a (A1) · p (A1, a) · f2a, f2b (A2, a) · g3 (A3, p) · f3a, f3b, p4 (A3, a) · g4 (A4, g3 AND p4)
    //  civics: law (A1, 6000, a) · court (A2, 12000, law) — gate 1 (g3) waits on law, gate 2 (g4) on court
    //  goals:  road classes Trackway (entity ← g3) then BuiltRoad (entity ← g4); no tax goal (its requirement is a known node)
    private static Spec TwoGates(bool on)
    {
        var s = new Spec { Stage = "a", AgeSweep = true, DestinationRouting = true, CivicsPoints = (true, 1.0, 0.5), AiGateAwareGoals = on };
        s.Technologies.AddRange([
            new Node(1, "a", Cost: 1000.0), new Node(2, "p", "a", Cost: 1000.0),
            new Node(3, "f2a", "a", Cost: 1500.0, Age: "A2"), new Node(4, "f2b", "a", Cost: 1500.0, Age: "A2"),
            new Node(5, "g3", "p", Cost: 2000.0, Age: "A3"),
            new Node(6, "f3a", "a", Cost: 1200.0, Age: "A3"), new Node(7, "f3b", "a", Cost: 1200.0, Age: "A3"),
            new Node(8, "g4", "g3 AND p4", Cost: 2000.0, Age: "A4"), new Node(9, "p4", "a", Cost: 1000.0, Age: "A3"),
        ]);
        s.Civics.AddRange([new Node(1001, "law", "a", Cost: 6000.0), new Node(1002, "court", "law", Cost: 12000.0, Age: "A2")]);
        s.Entities.Add(new Entity("infra.track", "infrastructure", "g3"));
        s.Entities.Add(new Entity("infra.built", "infrastructure", "g4"));
        return s;
    }

    [Fact]
    public void D_TwoSuccessiveSweepGates_EachIsHandled()
    {
        const int Turns = 24;
        var waiting = new int[2];
        var traces = new string[2];
        foreach (bool on in new[] { false, true })
        {
            ResearchContent c = TwoGates(on).Load();
            SimConfig cfg = AiConfig(c, "a", (EdgeTypes.Trackway, "infra.track"), (EdgeTypes.BuiltRoad, "infra.built"));
            (List<Step> steps, WorldState end) = Drive(c, cfg, AiWorld(c, Ai, "a"), Turns);
            waiting[on ? 1 : 0] = WaitingSteps(steps);
            traces[on ? 1 : 0] = Trace(c, steps);
            if (on)
                foreach (Step s in steps)
                    Assert.True(!s.TechAvailable || s.Funded >= 0, $"turn {s.Turn}: a technology is available and the lane funds nothing");
            Assert.True(Done(end, 5, Ai) && Done(end, 8, Ai), "both gated goals complete");
        }
        // Off: each gate waits 5 steps (turns 1–5 on law; turns 13–17 on court, after g4's technology cohort p4, f2a, f2b).
        Assert.Equal("g3>p g3>- g3>- g3>- g3>- g3>- g3>g3 g3>g3 g4>p4 g4>f2a g4>f2a g4>f2b g4>f2b g4>- g4>- g4>- g4>- g4>- " +
                     "g4>g4 g4>g4 f3a>f3a f3a>f3a f3b>f3b f3b>f3b", traces[0]);
        Assert.Equal(10, waiting[0]);
        // On: gate 1 is filled with f2a, f2b (turns 1–4); gate 2 with f3a, f3b (turns 9–12, after p4). What remains is the
        // lane with NO technology available at all (turn 5; turns 13–17): no destination, nothing funded — the empty-lane
        // rule's case. Each gated goal is taken the turn its gate opens (turns 6 and 18), as off.
        Assert.Equal("g3>p f2a>f2a f2a>f2a f2b>f2b f2b>f2b ->- g3>g3 g3>g3 g4>p4 f3a>f3a f3a>f3a f3b>f3b f3b>f3b ->- ->- ->- ->- ->- " +
                     "g4>g4 g4>g4 ->- ->- ->- ->-", traces[1]);
        Assert.Equal(0, waiting[1]);
    }

    // ------------------------------------------------------------------ (e) the first turns after the gate opens

    [Fact]
    public void E_AfterTheGateOpens_TheAiReturnsToTheGatedGoal_ByItsNormalChoice()
    {
        // law costs 6 turns of CP (turns 0–5), so g3 becomes eligible for turn 6's decision.
        var traces = new string[2];
        var lag = new int[2];
        foreach (bool on in new[] { false, true })
        {
            ResearchContent c = GateSpec(on, 6_000.0).Load();
            (List<Step> steps, WorldState end) = Drive(c, GateConfig(c), AiWorld(c, Ai, "a"), 12);
            int g3 = c.IndexOfId("g3");
            int open = steps.FindIndex(x => x.Available[g3]);
            int back = steps.FindIndex(open, x => x.Destination == g3 && x.Funded == g3);
            Assert.Equal(6, open);
            lag[on ? 1 : 0] = back - open;
            traces[on ? 1 : 0] = Trace(c, steps);
            Assert.True(Done(end, 3, Ai));
        }
        // Off: g3 is held through the gate and funded the turn it opens (lag 0).
        Assert.Equal("g3>p g3>- g3>- g3>- g3>- g3>- g3>g3 g3>g3 alt>alt alt>alt alt>alt alt>alt", traces[0]);
        Assert.Equal(0, lag[0]);
        // On: alt (turns 1–4) and f1 (turns 5–6) fill the gate. f1 is fundable, so it is NOT switched when the gate opens at
        // turn 6; its completion frees the lane and the normal choice takes g3 at turn 7 — a lag of 1 turn, bounded by the
        // remaining cost of the filler the lane holds when the gate opens (here 500 RP of f1, one step).
        Assert.Equal("g3>p alt>alt alt>alt alt>alt alt>alt f1>f1 f1>f1 g3>g3 g3>g3 f2>f2 f2>f2 ->-", traces[1]);
        Assert.Equal(1, lag[1]);
    }

    // ------------------------------------------------------------------ (f) the new ordering: tie-dense

    [Fact]
    public void F_OrderByCost_TieDense_BitEqualCostsKeepAscendingIndex()
    {
        Assert.Equal([0, 1, 2, 3, 4, 5], AiResearchPolicy.OrderByCost([7.0, 7.0, 7.0, 7.0, 7.0, 7.0], [true, true, true, true, true, true]));
        Assert.Equal([1, 3, 5, 0, 2, 4], AiResearchPolicy.OrderByCost([2.0, 1.0, 2.0, 1.0, 2.0, 1.0], [true, true, true, true, true, true]));
        Assert.Equal([5, 3, 1], AiResearchPolicy.OrderByCost([3.0, 3.0, 2.0, 2.0, 1.0, 1.0], [false, true, false, true, false, true]));
        Assert.Empty(AiResearchPolicy.OrderByCost([1.0, 2.0], [false, false]));
        // 0.1 + 0.2 is not 0.3: no epsilon, bit-equality only
        Assert.Equal([1, 0], AiResearchPolicy.OrderByCost([0.1 + 0.2, 0.3], [true, true]));
        // Dense ties across 64 entries, 4 distinct costs: within a cost, ascending index; across, ascending cost.
        var cost = new double[64];
        var all = new bool[64];
        for (int i = 0; i < 64; i++) { cost[i] = (i * 7 % 4) * 0.5; all[i] = true; }
        int[] order = AiResearchPolicy.OrderByCost(cost, all);
        for (int k = 1; k < order.Length; k++)
            Assert.True(cost[order[k - 1]] < cost[order[k]] || (cost[order[k - 1]] == cost[order[k]] && order[k - 1] < order[k]));
    }

    //  tech: a (A1) · s1, s2 (A1, 800 each) · r1 (A1, 1600) · q (A1, 1000) · gq (A3, 600, q) · law (civic, A1, 9000)
    private static Spec TieSpec()
    {
        var s = new Spec { Stage = "a", AgeSweep = true, DestinationRouting = true, CivicsPoints = (true, 1.0, 0.5), AiGateAwareGoals = true };
        s.Technologies.AddRange([
            new Node(1, "a", Cost: 1000.0), new Node(2, "s1", "a", Cost: 800.0), new Node(3, "s2", "a", Cost: 800.0),
            new Node(4, "r1", "a", Cost: 1600.0, Age: "A2"), new Node(5, "q", "a", Cost: 1000.0), new Node(6, "gq", "q", Cost: 600.0, Age: "A3"),
        ]);
        s.Civics.Add(new Node(1001, "law", "a", Cost: 9000.0));
        s.Entities.Add(new Entity("infra.track", "infrastructure", "r1"));
        return s;
    }

    [Fact]
    public void F_TieDense_BitEqualGoals_GoToTheLowerOrdinal_BitEqualSinks_ToTheLowerKey()
    {
        ResearchContent c = TieSpec().Load();
        // Road goal {r1} = 1600; tax goal "s2 AND s1" = 1600 — bit-equal: the road goal (ordinal 1) before tax (2).
        SimConfig both = AiConfig(c, "s2 AND s1", (EdgeTypes.Trackway, "infra.track"));
        WorldState w = Holding(c, AiWorld(c, Ai, "a"), Ai, "law");
        Assert.Equal(4L, (long)AiResearchPolicy.DecideLane(w, both, P, ResearchTree.Technology)!.Value.TargetId);
        // Tax goal alone: two sinks of bit-equal route cost (800): the lower key s1, though written second.
        SimConfig tax = AiConfig(c, "s2 AND s1");
        Assert.Equal(2L, (long)AiResearchPolicy.DecideLane(w, tax, P, ResearchTree.Technology)!.Value.TargetId);
        // q known, gq held and swept (law, and s1, s2, the Age-1 tech cohort): gq's route funds s1/s2 — fundable, kept.
        WorldState held = Holding(c, AiWorld(c, Ai, "a", "q"), Ai, "gq", "law");
        Assert.Null(AiResearchPolicy.DecideLane(held, tax, P, ResearchTree.Technology));
        // s1 and s2 known too: gq waits on law alone → re-decided; the tax goal is met (s1 AND s2), so no goal: the
        // cheapest available technology, r1 (the only one) — never law.
        WorldState gated = Holding(c, AiWorld(c, Ai, "a", "q", "s1", "s2"), Ai, "gq", "law");
        Assert.Equal(4L, (long)AiResearchPolicy.DecideLane(gated, tax, P, ResearchTree.Technology)!.Value.TargetId);
    }

    // ------------------------------------------------------------------ (g) the player, and the civics lane

    [Fact]
    public void G_APlayerPolity_IsNeverTouched()
    {
        ResearchContent c = GateSpec(true, 10_000.0).Load();
        SimConfig cfg = GateConfig(c);
        WorldState w = Holding(c, AiWorld(c, Player, "a", "p"), Player, "g3", "law");
        var player = new PolityId(Player);
        Assert.Null(ResearchQuery.Route(w, c, player, ResearchTree.Technology).Current);   // the player's lane waits…
        Assert.Null(AiResearchPolicy.DecideLane(w, cfg, player, ResearchTree.Technology));  // …and no order is made for it
        Assert.Empty(AiResearchPolicy.DecideAll(w, cfg, player));
        Assert.Null(AiResearchPolicy.Decide(w, cfg, player));
    }

    [Fact]
    public void G_TheCivicsLane_IsNotGateAware()
    {
        //  tech: a · t9 (a) · t8 (a)        civics: cx (A1, t9) — waits on a technology · cy (a, 700) · cz (cy, 300) · cw (a, 100)
        foreach (bool on in new[] { false, true })
        {
            var s = new Spec { Stage = "a", AgeSweep = true, DestinationRouting = true, CivicsPoints = (true, 1.0, 0.5), AiGateAwareGoals = on };
            s.Technologies.AddRange([new Node(1, "a", Cost: 1000.0), new Node(2, "t9", "a", Cost: 5000.0), new Node(3, "t8", "a", Cost: 900.0)]);
            s.Civics.AddRange([
                new Node(1001, "cx", "t9", Cost: 600.0), new Node(1002, "cy", "a", Cost: 700.0), new Node(1003, "cz", "cy", Cost: 300.0),
                new Node(1004, "cw", "a", Cost: 100.0),
            ]);
            ResearchContent c = s.Load();
            // A held civic destination waiting on the technology lane (t8 held there, so no assist) is kept on and off.
            WorldState w = Holding(c, AiWorld(c, Ai, "a"), Ai, "t8", "cx");
            Assert.Null(ResearchQuery.Route(w, c, P, ResearchTree.Civics).Current);
            Assert.Null(AiResearchPolicy.DecideLane(w, AiConfig(c, "a"), P, ResearchTree.Civics));
            // An empty civics lane takes its goal sink cz (the tax requirement; its route is cy, a civic) on and off — the
            // gate-aware walk, which reads the TECHNOLOGY lane's funding, never decides a civic.
            WorldState e = Holding(c, AiWorld(c, Ai, "a"), Ai, "t8");
            Assert.Equal(1003L, (long)AiResearchPolicy.DecideLane(e, AiConfig(c, "cz"), P, ResearchTree.Civics)!.Value.TargetId);
        }
    }

    // ------------------------------------------------------------------ within a goal: a gated sink is skipped

    [Fact]
    public void A_WithinAGoal_AGatedCheapestSink_IsSkippedForTheGoalsNextFundableSink()
    {
        // law costs 600: g3's route (law 600 + g3 2000 = 2600) is cheaper than alt (4000), so g3 is the goal's first sink;
        // the tax requirement "g3 AND alt" is one goal with two sinks.
        foreach (bool on in new[] { false, true })
        {
            ResearchContent c = GateSpec(on, 600.0).Load();
            SimConfig cfg = AiConfig(c, "g3 AND alt");
            WorldState empty = Holding(c, AiWorld(c, Ai, "a", "p"), Ai, "law");
            WorldState held = Holding(c, AiWorld(c, Ai, "a", "p"), Ai, "g3", "law");
            OrderRecord? fromEmpty = AiResearchPolicy.DecideLane(empty, cfg, P, ResearchTree.Technology);
            OrderRecord? fromHeld = AiResearchPolicy.DecideLane(held, cfg, P, ResearchTree.Technology);
            if (!on)
            {
                Assert.Equal(3L, (long)fromEmpty!.Value.TargetId);   // today: g3, which then waits on law
                Assert.Null(fromHeld);
                continue;
            }
            Assert.Equal(4L, (long)fromEmpty!.Value.TargetId);       // alt, the same goal's fundable sink
            Assert.Equal(4L, (long)fromHeld!.Value.TargetId);
        }
    }
}
