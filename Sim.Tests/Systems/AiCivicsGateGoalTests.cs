using System.Text.Json.Nodes;
using Sim.Core;
using Sim.Core.Kernel;
using Sim.Core.State;
using Sim.Core.Systems;
using Sim.Core.Systems.Research;
using Sim.Tests.Kernel;
using Sim.Tests.TestUtil;
using static Sim.Tests.TestUtil.ResearchRigs;

namespace Sim.Tests.Systems;

/// <summary>
/// ADR-053 (PROPOSED; Director ruling 2026-10-26) — CIVICS GATE GOALS (research.json tuning.aiCivicsGateGoals,
/// AiResearchPolicy.DecideLane / ChooseDestination / CivicsGateBlockers / CivicsGateDestination). With the two lanes,
/// destination routing and the Age sweep, a technology of Age a waits on every sweep-cohort node of Age a − 2, civics
/// included; the Civics lane, with no goal, buys the cheapest available civic, so a dear cohort civic is bought last while
/// the technology lane has nothing to fund (the measured caliphate stall). Switch on, while the technology tree has NO
/// available node and a technology fails only on the sweep with a civic outstanding in its cohort, the Civics lane's
/// destination is the cheapest-to-reach outstanding civic of the LOWEST such cohort; otherwise the AI rule exactly.
/// Rigs: an AI Empire of 10 000 people (1 000 RP and 1 000 CP a dt-10 turn), no Age content, no road goal; the tax
/// requirement names a known technology (no tax goal) unless a test gives it a civic.
/// </summary>
public class AiCivicsGateGoalTests
{
    private const int Ai = 2;
    private static readonly PolityId P = new(Ai);

    private static Spec Rig(bool on) =>
        new() { Stage = "a", AgeSweep = true, DestinationRouting = true, CivicsPoints = (true, 1.0, 0.5), AiCivicsGateGoals = on };

    /// <summary>The AI's configuration: the rig research, no Age content, no institutions, no road class; the tax requirement
    /// <paramref name="taxRequires"/> (a known technology = no tax goal).</summary>
    private static SimConfig AiConfig(ResearchContent c, string taxRequires = "a")
    {
        SimConfig sim = TestConfigs.Sim();
        return sim with
        {
            Research = c, Ages = null, Institutions = null,
            Governance = sim.Governance! with { TaxationRequires = taxRequires, TaxationMinAge = null },
            Roads = sim.Roads! with { Classes = [] },
        };
    }

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

    private static int Ix(ResearchContent c, string id) => c.IndexOfId(id);
    private static string IdOf(ResearchContent c, int i) => i < 0 ? "-" : c.Nodes[i].Id;

    private static bool[]? Blockers(ResearchContent c, WorldState w)
    {
        bool[] completed = ResearchQuery.CompletedMask(w, c, P);
        return AiResearchPolicy.CivicsGateBlockers(w, c, P, completed,
            ResearchQuery.AvailableMask(c, completed, ResearchQuery.LevelMask(w, c, P, completed)));
    }

    private static long? Civic(ResearchContent c, SimConfig cfg, WorldState w) =>
        AiResearchPolicy.DecideLane(w, cfg, P, ResearchTree.Civics) is { } o ? o.TargetId : null;

    /// <summary>One step as the engine runs it: the AI's orders (DecideAll, appended to the log the executor reads), then
    /// each lane's destination and funded node reconstructed with the engine's own planner over the destinations those
    /// orders leave (AiGateAwareGoalTests' reconstruction, both lanes).</summary>
    private sealed record Step(long Turn, int TechDest, int TechFunded, int CivicDest, int CivicFunded, bool TechAvailable);

    private static (List<Step> Steps, WorldState End) Drive(ResearchContent c, SimConfig cfg, WorldState w, int turns)
    {
        var log = new OrderLog();
        TurnExecutor exec = Executor(c, log);
        var steps = new List<Step>(turns);
        for (int t = 0; t < turns; t++)
        {
            OrderRecord[] orders = AiResearchPolicy.DecideAll(w, cfg, P);
            foreach (OrderRecord o in orders) log.Append(o);
            bool[] completed = ResearchQuery.CompletedMask(w, c, P);
            int[] dest = ResearchQuery.Destinations(w, c, P, completed);
            foreach (OrderRecord o in orders)
            {
                int i = c.IndexOf(new ResearchNodeId(o.TargetId));
                if (ResearchQuery.IsDestinationAdmissible(c, i, completed)) dest[ResearchRoutePlanner.LaneOf(c, i, true)] = i;
            }
            bool[] available = ResearchQuery.AvailableMask(c, completed, ResearchQuery.LevelMask(w, c, P, completed));
            LaneFunding[] lanes = ResearchRoutePlanner.Fund(c, completed, available, ResearchQuery.RemainingCosts(w, c, P), dest, true);
            bool techAvailable = false;
            for (int i = 0; i < c.Nodes.Count; i++) techAvailable |= available[i] && c.Nodes[i].Tree == ResearchTree.Technology;
            steps.Add(new Step(w.Clock.Turn, dest[0], lanes[0].Current, dest[1], lanes[1].Current, techAvailable));
            w = exec.Step(w);
        }
        return (steps, w);
    }

    private static string Trace(ResearchContent c, List<Step> steps, bool civics)
    {
        var parts = new List<string>(steps.Count);
        foreach (Step s in steps)
            parts.Add(civics ? IdOf(c, s.CivicDest) + ">" + IdOf(c, s.CivicFunded) : IdOf(c, s.TechDest) + ">" + IdOf(c, s.TechFunded));
        return string.Join(" ", parts);
    }

    /// <summary>The first step whose technology lane funds a node at or after <paramref name="from"/>, -1 when none.</summary>
    private static int FirstTechFunded(List<Step> steps, int from)
    {
        for (int k = from; k < steps.Count; k++) if (steps[k].TechFunded >= 0) return k;
        return -1;
    }

    // ------------------------------------------------------------------ the stall rig

    //  tech:   a (A1, 1000) · t3 (A3, 2000, a) — swept by the Age-1 cohort, whose only outstanding node is the civic law
    //  civics: law (A1, 3000, a) — the blocker · pol (A2, 500, a), pol2 (A2, 700, a) — cheaper, unrelated, a later Age
    //  [t4 (A4, 2000, a) when twoCohorts: swept by the Age-2 cohort — pol and pol2]
    private static Spec StallSpec(bool on, bool twoCohorts = false)
    {
        Spec s = Rig(on);
        s.Technologies.AddRange([new Node(1, "a", Cost: 1000.0), new Node(2, "t3", "a", Cost: 2000.0, Age: "A3")]);
        if (twoCohorts) s.Technologies.Add(new Node(3, "t4", "a", Cost: 2000.0, Age: "A4"));
        s.Civics.AddRange([
            new Node(1001, "law", "a", Cost: 3000.0), new Node(1002, "pol", "a", Cost: 500.0, Age: "A2"),
            new Node(1003, "pol2", "a", Cost: 700.0, Age: "A2"),
        ]);
        return s;
    }

    // ------------------------------------------------------------------ correct blocker

    [Fact]
    public void Blocker_TechStalledByTheAge1CivicsCohort_TheBlockerIsChosen_NotTheCheaperLaterCivic()
    {
        foreach (bool on in new[] { false, true })
        {
            ResearchContent c = StallSpec(on).Load();
            WorldState w = AiWorld(c, Ai, "a");
            // The stall: no technology is available; t3's route waits on law (the sweep).
            ResearchRoute tech = ResearchQuery.Route(Holding(c, AiWorld(c, Ai, "a"), Ai, "t3"), c, P, ResearchTree.Technology);
            Assert.Null(tech.Current);
            Assert.Contains(tech.Blockers, b => b.Cause == RouteReason.Sweep && b.Key.Value == 1001);
            // The trigger is a reading of state, the same on and off; only the decision consults the switch.
            bool[]? blockers = Blockers(c, w);
            Assert.NotNull(blockers);
            for (int i = 0; i < c.Nodes.Count; i++) Assert.Equal(i == Ix(c, "law"), blockers![i]);
            Assert.Equal(on ? 1001L : 1002L, Civic(c, AiConfig(c), w));   // off: pol, the cheapest civic (today's stall)
        }
    }

    [Fact]
    public void Blocker_TwoCohortsGate_TheLowestCohortWins()
    {
        // t3 (A3) is swept by the Age-1 cohort {law}; t4 (A4) by the Age-2 cohort {pol, pol2} — the cheaper civics. The
        // lowest cohort, whose completion unblocks technology soonest, is law's.
        ResearchContent c = StallSpec(true, twoCohorts: true).Load();
        WorldState w = AiWorld(c, Ai, "a");
        bool[] blockers = Blockers(c, w)!;
        Assert.True(blockers[Ix(c, "law")]);
        Assert.False(blockers[Ix(c, "pol")] || blockers[Ix(c, "pol2")]);
        Assert.Equal(1001L, Civic(c, AiConfig(c), w));
        // law known: t3 becomes AVAILABLE, so the trigger no longer holds at all (no available technology is required) —
        // the Age-2 cohort is not chased while t3 can be funded.
        WorldState lawDone = AiWorld(c, Ai, "a", "law");
        Assert.Null(Blockers(c, lawDone));
        Assert.Equal(1002L, Civic(c, AiConfig(c), lawDone));
        // t3 known too: only t4 is left, swept by the Age-2 cohort — now its civics are the blockers (pol, the cheaper).
        WorldState t3Done = AiWorld(c, Ai, "a", "law", "t3");
        bool[] second = Blockers(c, t3Done)!;
        Assert.True(second[Ix(c, "pol")] && second[Ix(c, "pol2")]);
        Assert.Equal(1002L, Civic(c, AiConfig(c), t3Done));
    }

    //  tech: a · t3 (A3, a)   civics: b1..b6 (A1, costs given, a) — the Age-1 cohort · cheap (A2, 100, a) — unrelated
    private static ResearchContent TieRig(params double[] costs)
    {
        Spec s = Rig(true);
        s.Technologies.AddRange([new Node(1, "a", Cost: 1000.0), new Node(2, "t3", "a", Cost: 2000.0, Age: "A3")]);
        for (int k = 0; k < costs.Length; k++) s.Civics.Add(new Node(1001 + k, "b" + (k + 1), "a", Cost: costs[k]));
        s.Civics.Add(new Node(1100, "cheap", "a", Cost: 100.0, Age: "A2"));
        return s.Load();
    }

    [Fact]
    public void Blocker_TieDense_EqualCostBlockers_GoToTheLowerIndex()
    {
        // All six bit-equal: the lowest index, b1.
        ResearchContent all = TieRig(2000.0, 2000.0, 2000.0, 2000.0, 2000.0, 2000.0);
        Assert.Equal(1001L, Civic(all, AiConfig(all), AiWorld(all, Ai, "a")));
        // Dense ties at two costs: the cheaper cost first, and within it the lower index — b2, not b3, b5 or b6.
        ResearchContent mixed = TieRig(3000.0, 2000.0, 2000.0, 3000.0, 2000.0, 2000.0);
        Assert.Equal(1002L, Civic(mixed, AiConfig(mixed), AiWorld(mixed, Ai, "a")));
        // The remaining cost is what counts: progress on b6 makes it strictly cheapest; progress that ties it with b2 does not.
        WorldState progressed = AiWorld(mixed, Ai, "a");
        progressed.ResearchProgress.Add(new ResearchProgressRow(P, new ResearchNodeId(1006), 1000.0));
        Assert.Equal(1006L, Civic(mixed, AiConfig(mixed), progressed));
        WorldState tied = AiWorld(mixed, Ai, "a");
        tied.ResearchProgress.Add(new ResearchProgressRow(P, new ResearchNodeId(1004), 1000.0));   // b4: 3000 − 1000 = b2's 2000
        Assert.Equal(1002L, Civic(mixed, AiConfig(mixed), tied));
        Assert.Equal(Ix(mixed, "b2"), AiResearchPolicy.CivicsGateDestination(tied, mixed, P, ResearchQuery.CompletedMask(tied, mixed, P),
            ResearchQuery.AvailableMask(tied, mixed, P), Blockers(mixed, tied)!));
    }

    [Fact]
    public void Blocker_TieDense_ARoutedBlockerAndAnAvailableOne_AtBitEqualCost_GoToTheLowerIndex()
    {
        // c1 (A1, 1000) needs pre (A2, 1000): route 2000. c2 (A1, 2000) is available: 2000. Bit-equal → the lower index.
        foreach (bool routedFirst in new[] { true, false })
        {
            Spec s = Rig(true);
            s.Technologies.AddRange([new Node(1, "a", Cost: 1000.0), new Node(2, "t3", "a", Cost: 2000.0, Age: "A3")]);
            s.Civics.Add(new Node(1001, "pre", "a", Cost: 1000.0, Age: "A2"));
            if (routedFirst) s.Civics.AddRange([new Node(1002, "c1", "pre", Cost: 1000.0), new Node(1003, "c2", "a", Cost: 2000.0)]);
            else s.Civics.AddRange([new Node(1002, "c2", "a", Cost: 2000.0), new Node(1003, "c1", "pre", Cost: 1000.0)]);
            ResearchContent c = s.Load();
            Assert.Equal(1002L, Civic(c, AiConfig(c), AiWorld(c, Ai, "a")));
        }
    }

    [Fact]
    public void Blocker_NeverARepeatable_NeverATechnology_NeverAnotherCohort()
    {
        //  tech:   a · r (A1, trunk repeatable — exempt from every cohort) · t3 (A3, a) — swept by law
        //  civics: law (A1, 3000) · pol (A2, 500) · fin (A9, 900) · rep (A9, civic repeatable, fin)
        Spec s = Rig(true);
        s.Technologies.AddRange([
            new Node(1, "a", Cost: 1000.0), new Node(2, "r", "a", Cost: 500.0),
            new Node(3, "t3", "a", Cost: 2000.0, Age: "A3"),
        ]);
        s.Civics.AddRange([
            new Node(1001, "law", "a", Cost: 3000.0), new Node(1002, "pol", "a", Cost: 500.0, Age: "A2"),
            new Node(1003, "fin", "a", Cost: 900.0, Age: "A9"), new Node(1004, "rep", "fin", Cost: 400.0, Age: "A9"),
        ]);
        s.Recursive.AddRange(["r", "rep"]);
        ResearchContent c = s.Load();
        // r known (complete once), fin known: rep is an available civic repeatable; nothing technological is available.
        WorldState w = AiWorld(c, Ai, "a", "r", "fin");
        Assert.True(ResearchQuery.AvailableMask(w, c, P)[Ix(c, "rep")]);
        bool[] blockers = Blockers(c, w)!;
        for (int i = 0; i < c.Nodes.Count; i++) Assert.Equal(c.Nodes[i].Id == "law", blockers[i]);
        Assert.Equal(1001L, Civic(c, AiConfig(c), w));
    }

    // ------------------------------------------------------------------ civic prerequisites are respected

    //  tech:   a (A1, 1000) · t4 (A4, 2000, a) — swept by the Age-2 cohort {canon}
    //  civics: rite (A1, 1000, a) — canon's prerequisite, NOT a blocker (no A3 technology) · canon (A2, 2000, rite) — the
    //          blocker · fair (A1, 300, a) — the cheapest civic, unrelated
    private static ResearchContent PrereqRig(bool on)
    {
        Spec s = Rig(on);
        s.Technologies.AddRange([new Node(1, "a", Cost: 1000.0), new Node(2, "t4", "a", Cost: 2000.0, Age: "A4")]);
        s.Civics.AddRange([
            new Node(1001, "rite", "a", Cost: 1000.0), new Node(1002, "canon", "rite", Cost: 2000.0, Age: "A2"),
            new Node(1003, "fair", "a", Cost: 300.0),
        ]);
        return s.Load();
    }

    [Fact]
    public void Prerequisites_TheBlockerIsTheDestination_AndTheEngineFundsItsPrerequisiteFirst()
    {
        ResearchContent c = PrereqRig(true);
        SimConfig cfg = AiConfig(c);
        WorldState w = AiWorld(c, Ai, "a");
        bool[] blockers = Blockers(c, w)!;
        Assert.True(blockers[Ix(c, "canon")]);
        Assert.False(blockers[Ix(c, "rite")] || blockers[Ix(c, "fair")]);
        Assert.Equal(1002L, Civic(c, cfg, w));                     // the destination is canon, not its prerequisite node

        (List<Step> steps, WorldState end) = Drive(c, cfg, w, 6);
        // canon is held while its route is funded: rite (turn 0), then canon (turns 1–2). The trigger then clears (t4 is
        // available), and the lane returns to the normal choice, fair; the technology lane takes t4 the same turn.
        Assert.Equal("canon>rite canon>canon canon>canon fair>fair ->- ->-", Trace(c, steps, civics: true));
        Assert.Equal("->- ->- ->- t4>t4 t4>t4 ->-", Trace(c, steps, civics: false));
        Assert.True(Done(end, 1001, Ai) && Done(end, 1002, Ai) && Done(end, 1003, Ai) && Done(end, 2, Ai));
        foreach (Step s in steps)
        {
            if (s.TechFunded >= 0) Assert.Equal(ResearchTree.Technology, c.Nodes[s.TechFunded].Tree);   // RP never pays a civic
            if (s.CivicFunded >= 0) Assert.Equal(ResearchTree.Civics, c.Nodes[s.CivicFunded].Tree);
        }
    }

    [Fact]
    public void Prerequisites_CompletionOrder_ThePrerequisiteIsNeverSkipped()
    {
        ResearchContent c = PrereqRig(true);
        SimConfig cfg = AiConfig(c);
        var log = new OrderLog();
        TurnExecutor exec = Executor(c, log);
        WorldState w = AiWorld(c, Ai, "a");
        var completedAt = new Dictionary<int, long>();   // test-side bookkeeping, not sim logic
        for (int t = 0; t < 6; t++)
        {
            foreach (OrderRecord o in AiResearchPolicy.DecideAll(w, cfg, P)) log.Append(o);
            long turn = w.Clock.Turn;
            w = exec.Step(w);
            foreach (int key in new[] { 1001, 1002, 1003, 2 })
                if (Done(w, key, Ai) && !completedAt.ContainsKey(key)) completedAt[key] = turn;
        }
        Assert.Equal(0L, completedAt[1001]);   // rite
        Assert.Equal(2L, completedAt[1002]);   // canon, after its prerequisite
        Assert.Equal(3L, completedAt[1003]);   // fair, the normal choice once the gate has cleared
        Assert.Equal(4L, completedAt[2]);      // t4: 2000 RP, turns 3–4
    }

    // ------------------------------------------------------------------ does not starve

    //  tech:   a (A1, 1000) · t2 (A2, 1500, a) · t3 (A3, 2000, a) — swept by law
    //  civics: law (A1, 3000, a) · pol (A2, 500, a) · cg (A2, 900, pol) — the tax goal's civic when taxRequires = "cg"
    private static ResearchContent StarveRig(bool on)
    {
        Spec s = Rig(on);
        s.Technologies.AddRange([
            new Node(1, "a", Cost: 1000.0), new Node(2, "t2", "a", Cost: 1500.0, Age: "A2"), new Node(3, "t3", "a", Cost: 2000.0, Age: "A3"),
        ]);
        s.Civics.AddRange([
            new Node(1001, "law", "a", Cost: 3000.0), new Node(1002, "pol", "a", Cost: 500.0, Age: "A2"),
            new Node(1003, "cg", "pol", Cost: 900.0, Age: "A2"),
        ]);
        return s.Load();
    }

    private static string Decisions(ResearchContent c, SimConfig cfg, WorldState w)
    {
        var parts = new List<string>();
        foreach (OrderRecord o in AiResearchPolicy.DecideAll(w, cfg, P)) parts.Add(o.Kind + ":" + o.TargetId);
        return string.Join(",", parts);
    }

    [Fact]
    public void DoesNotStarve_WhenAnyTechnologyIsAvailable_OnEqualsOff_OverEveryState()
    {
        ResearchContent on = StarveRig(true), off = StarveRig(false);
        string[] ids = ["a", "t2", "t3", "law", "pol", "cg"];
        int availableStates = 0, triggerStates = 0, differing = 0;
        foreach (string tax in new[] { "a", "cg" })   // without a goal, and with the civic tax goal
            for (int mask = 0; mask < 1 << ids.Length; mask++)
            {
                var done = new List<string>();
                for (int b = 0; b < ids.Length; b++) if ((mask & (1 << b)) != 0) done.Add(ids[b]);
                if (!done.Contains("a")) continue;                                         // the root is known in every state
                if (done.Contains("cg") && !done.Contains("pol")) continue;                // prerequisite-consistent states only
                foreach (string? techHeld in new[] { null, "t2", "t3" })
                    foreach (string? civicHeld in new[] { null, "law", "pol", "cg" })
                        foreach (bool goalProgress in new[] { false, true })
                        {
                            if ((techHeld is { } th && done.Contains(th)) || (civicHeld is { } ch && done.Contains(ch))) continue;
                            if (goalProgress && done.Contains("cg")) continue;
                            WorldState Make(ResearchContent c)
                            {
                                WorldState w = AiWorld(c, Ai, [.. done]);
                                if (techHeld is not null) Holding(c, w, Ai, techHeld);
                                if (civicHeld is not null) Holding(c, w, Ai, civicHeld);
                                if (goalProgress) w.ResearchProgress.Add(new ResearchProgressRow(P, new ResearchNodeId(1003), 400.0));   // the goal in progress
                                return w;
                            }
                            WorldState wOn = Make(on), wOff = Make(off);
                            bool[] avail = ResearchQuery.AvailableMask(wOn, on, P);
                            bool techAvailable = false;
                            for (int i = 0; i < on.Nodes.Count; i++) techAvailable |= avail[i] && on.Nodes[i].Tree == ResearchTree.Technology;
                            string dOn = Decisions(on, AiConfig(on, tax), wOn), dOff = Decisions(off, AiConfig(off, tax), wOff);
                            bool trigger = Blockers(on, wOn) is not null;
                            if (techAvailable) { availableStates++; Assert.False(trigger); }
                            if (!trigger) Assert.True(dOn == dOff, $"state {{{string.Join(" ", done)}}} tax {tax} held {techHeld}/{civicHeld}: on {dOn} off {dOff}");
                            else { triggerStates++; if (dOn != dOff) differing++; }
                        }
            }
        // Not vacuous: many states have an available technology, and the rule does act where it is meant to.
        Assert.True(availableStates >= 100, $"{availableStates} states with an available technology");
        Assert.True(triggerStates > 0 && differing > 0, $"{triggerStates} trigger states, {differing} differing");
    }

    [Fact]
    public void DoesNotStarve_ACivicGoalInProgress_IsKeptWhileATechnologyIsAvailable_AndDivertedOnlyWhenTheTechTreeStalls()
    {
        ResearchContent c = StarveRig(true);
        SimConfig cfg = AiConfig(c, "cg");
        // t2 available: the tax goal's civic cg (held, in progress) is kept; a fresh lane takes the goal (pol, its route).
        WorldState heldGoal = Holding(c, AiWorld(c, Ai, "a", "pol"), Ai, "cg");
        heldGoal.ResearchProgress.Add(new ResearchProgressRow(P, new ResearchNodeId(1003), 400.0));
        Assert.Null(Civic(c, cfg, heldGoal));
        Assert.Equal(1003L, Civic(c, cfg, AiWorld(c, Ai, "a", "pol")));
        // t2 known: nothing technological is available and t3 waits on law — the goal is diverted to law; its progress stays.
        WorldState stalled = Holding(c, AiWorld(c, Ai, "a", "pol", "t2"), Ai, "cg");
        stalled.ResearchProgress.Add(new ResearchProgressRow(P, new ResearchNodeId(1003), 400.0));
        Assert.Equal(1001L, Civic(c, cfg, stalled));
        Assert.Equal(400.0, ResearchQuery.Progress(stalled, P, new ResearchNodeId(1003)));
    }

    // ------------------------------------------------------------------ re-decide while held

    [Fact]
    public void Held_ANonBlockerIsReDecidedToTheBlocker_ABlockerIsKept_OffKeepsEither()
    {
        foreach (bool on in new[] { false, true })
        {
            ResearchContent c = StallSpec(on).Load();
            SimConfig cfg = AiConfig(c);
            WorldState holdingPol = Holding(c, AiWorld(c, Ai, "a"), Ai, "pol");
            WorldState holdingLaw = Holding(c, AiWorld(c, Ai, "a"), Ai, "law");
            Assert.Equal(on ? 1001L : (long?)null, Civic(c, cfg, holdingPol));
            Assert.Null(Civic(c, cfg, holdingLaw));
        }
        // Two blockers of one cohort: the held dearer one is kept (no churn), though the other is cheaper to reach.
        ResearchContent tie = TieRig(3000.0, 2000.0);
        Assert.Null(Civic(tie, AiConfig(tie), Holding(tie, AiWorld(tie, Ai, "a"), Ai, "b1")));
        Assert.Equal(1002L, Civic(tie, AiConfig(tie), Holding(tie, AiWorld(tie, Ai, "a"), Ai, "cheap")));
    }

    // ------------------------------------------------------------------ off = today, pinned; end to end

    [Fact]
    public void EndToEnd_Off_TheStallReproduces_On_TheTechnologyLaneResumesSooner()
    {
        const int Turns = 9;
        var techTraces = new string[2];
        var civicTraces = new string[2];
        var resume = new int[2];
        foreach (bool on in new[] { false, true })
        {
            ResearchContent c = StallSpec(on).Load();
            (List<Step> steps, WorldState end) = Drive(c, AiConfig(c), AiWorld(c, Ai, "a"), Turns);
            techTraces[on ? 1 : 0] = Trace(c, steps, civics: false);
            civicTraces[on ? 1 : 0] = Trace(c, steps, civics: true);
            resume[on ? 1 : 0] = FirstTechFunded(steps, 0);
            Assert.True(Done(end, 2, Ai), "t3 completes once law is known");
            foreach (Step s in steps)
                if (s.TechFunded >= 0) Assert.Equal(ResearchTree.Technology, c.Nodes[s.TechFunded].Tree);
        }
        // Off (today): the cheaper civics first — pol, pol2 (turns 0–1), then law (turns 2–4); the technology lane has
        // nothing to fund until t3 opens at turn 5.
        Assert.Equal("pol>pol pol2>pol2 law>law law>law law>law ->- ->- ->- ->-", civicTraces[0]);
        Assert.Equal("->- ->- ->- ->- ->- t3>t3 t3>t3 ->- ->-", techTraces[0]);
        Assert.Equal(5, resume[0]);
        // On: law first (turns 0–2); t3 opens at turn 3 — two turns sooner — and the cheap civics follow.
        Assert.Equal("law>law law>law law>law pol>pol pol2>pol2 ->- ->- ->- ->-", civicTraces[1]);
        Assert.Equal("->- ->- ->- t3>t3 t3>t3 ->- ->- ->- ->-", techTraces[1]);
        Assert.Equal(3, resume[1]);
    }

    // ------------------------------------------------------------------ the trigger reads the sweep only

    [Fact]
    public void Trigger_ATechnologyBlockedByAnUnmetAuthoredPrerequisite_DoesNotTrigger()
    {
        //  tech: a · t3 (A3, writ) — needs the civic writ AND is swept by law   civics: law (A1, 3000) · writ (A2, 800) · pol (A2, 500)
        foreach (bool on in new[] { false, true })
        {
            Spec s = Rig(on);
            s.Technologies.AddRange([new Node(1, "a", Cost: 1000.0), new Node(2, "t3", "writ", Cost: 2000.0, Age: "A3")]);
            s.Civics.AddRange([
                new Node(1001, "law", "a", Cost: 3000.0), new Node(1002, "writ", "a", Cost: 800.0, Age: "A2"),
                new Node(1003, "pol", "a", Cost: 500.0, Age: "A2"),
            ]);
            ResearchContent c = s.Load();
            // writ unknown: t3 fails on its prerequisite as well as the sweep — not "only the sweep": no trigger, today's pick.
            WorldState w = AiWorld(c, Ai, "a");
            Assert.Null(Blockers(c, w));
            Assert.Equal(1003L, Civic(c, AiConfig(c), w));
            // writ known: t3 now fails on the sweep alone — the trigger holds and law is chosen (on only).
            WorldState writ = AiWorld(c, Ai, "a", "writ");
            Assert.NotNull(Blockers(c, writ));
            Assert.Equal(on ? 1001L : 1003L, Civic(c, AiConfig(c), writ));
        }
    }

    [Fact]
    public void Trigger_ASweptTechnologyWhoseCohortHoldsNoOutstandingCivic_DoesNotTrigger()
    {
        //  tech:   a · q (A1, 1000, pol) — the Age-1 cohort's only outstanding node, a TECHNOLOGY · t3 (A3, a) — swept by q
        //  civics: law (A1, known) · pol (A2, 500, a) · cheap (A2, 100, a)
        Spec s = Rig(true);
        s.Technologies.AddRange([
            new Node(1, "a", Cost: 1000.0), new Node(2, "q", "pol", Cost: 1000.0), new Node(3, "t3", "a", Cost: 2000.0, Age: "A3"),
        ]);
        s.Civics.AddRange([
            new Node(1001, "law", "a", Cost: 3000.0), new Node(1002, "pol", "a", Cost: 500.0, Age: "A2"),
            new Node(1003, "cheap", "a", Cost: 100.0, Age: "A2"),
        ]);
        ResearchContent c = s.Load();
        // No technology is available (q waits on pol, t3 on the sweep), but t3's cohort holds no civic: no trigger.
        WorldState w = AiWorld(c, Ai, "a", "law");
        bool[] avail = ResearchQuery.AvailableMask(w, c, P);
        Assert.False(avail[Ix(c, "q")] || avail[Ix(c, "t3")]);
        Assert.Null(Blockers(c, w));
        Assert.Equal(1003L, Civic(c, AiConfig(c), w));
        // q available (pol known): a technology is available, so no trigger either.
        Assert.Null(Blockers(c, AiWorld(c, Ai, "a", "law", "pol")));
    }

    // ------------------------------------------------------------------ the player; the technology lane; the switches

    [Fact]
    public void APlayerPolity_IsNeverTouched_AndTheTechnologyLaneIsNotDiverted()
    {
        ResearchContent c = StallSpec(true).Load();
        SimConfig cfg = AiConfig(c);
        var player = new PolityId(Player);
        WorldState pw = Holding(c, AiWorld(c, Player, "a"), Player, "pol");
        Assert.Null(AiResearchPolicy.DecideLane(pw, cfg, player, ResearchTree.Civics));
        Assert.Empty(AiResearchPolicy.DecideAll(pw, cfg, player));
        // The AI's technology lane: nothing available, nothing ordered — never a civic (the gate acts on the Civics lane).
        WorldState w = AiWorld(c, Ai, "a");
        Assert.Null(AiResearchPolicy.DecideLane(w, cfg, P, ResearchTree.Technology));
        OrderRecord[] all = AiResearchPolicy.DecideAll(w, cfg, P);
        Assert.Equal([1001L], Array.ConvertAll(all, o => (long)o.TargetId));
    }

    [Fact]
    public void TheRuleNeedsTheSweepRoutingAndTwoLanes()
    {
        // Without the Age sweep nothing is ever swept: t3 is available and the civics lane takes the cheapest (pol).
        Spec noSweep = StallSpec(true);
        noSweep.AgeSweep = false;
        ResearchContent c = noSweep.Load();
        Assert.Equal(1002L, Civic(c, AiConfig(c), AiWorld(c, Ai, "a")));
        // Without destination routing (available-only targets): the AI rule exactly — pol.
        Spec noRouting = StallSpec(true);
        noRouting.DestinationRouting = false;
        ResearchContent r = noRouting.Load();
        Assert.Equal(1002L, Civic(r, AiConfig(r), AiWorld(r, Ai, "a")));
    }

    // ------------------------------------------------------------------ loader

    [Fact]
    public void Loader_SwitchParsed_AbsentIsFalse_ShippedResearchLeavesItAbsent()
    {
        Assert.True(StallSpec(true).Load().Tuning.AiCivicsGateGoals);
        Assert.False(StallSpec(false).Load().Tuning.AiCivicsGateGoals);
        Assert.Contains("\"aiCivicsGateGoals\":null", StallSpec(false).Json());   // the rig writes null for absent

        JsonObject tuning;
        using (var stream = Sim.Data.DataFiles.OpenResearch()) tuning = JsonNode.Parse(stream)!.AsObject()["tuning"]!.AsObject();
        Assert.False(tuning.ContainsKey("aiCivicsGateGoals"));
        Assert.False(ResearchPacingRunner.Config(overflow: false).Research!.Tuning.AiCivicsGateGoals);

        // The trial overlay sets exactly this switch and nothing else.
        string overlay = Path.Combine(AllocationScenario.RepoRoot(), "docs", "m6-plan", "knowledge-redesign", "research-cost-pass",
            "trial", "civics-gate-trial.json");
        JsonObject doc = JsonNode.Parse(File.ReadAllText(overlay))!.AsObject();
        Assert.Equal(["tuning"], doc.Select(kv => kv.Key).ToArray());
        Assert.Equal(["aiCivicsGateGoals"], doc["tuning"]!.AsObject().Select(kv => kv.Key).ToArray());
        ResearchTuning trial = ResearchPacingRunner.Config(overflow: false, overlays: [overlay]).Research!.Tuning;
        Assert.True(trial.AiCivicsGateGoals);
        Assert.False(trial.AiGateAwareGoals);
    }
}
