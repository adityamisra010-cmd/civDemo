using Sim.Core;
using Sim.Core.Kernel;
using Sim.Core.State;
using Sim.Core.Systems;
using Sim.Core.Systems.Research;
using Sim.Tests.TestUtil;
using static Sim.Tests.TestUtil.ResearchRigs;

namespace Sim.Tests.Systems;

/// <summary>
/// ADR-048 — THE AGE SWEEP (tranche-2 contract §2): a node of content Age a ≥ 3 is eligible only once EVERY sweep-cohort
/// node of Age a − 2, in both trees, is complete; repeatables are not cohort members; the predicate is one
/// (ResearchQuery.IsAvailable / AvailableMask / SweepSatisfied) and is reported apart from the prerequisites; the switch
/// off is the old availability exactly. A purpose-built rig: every boundary, then exhaustive over all 2^11 masks.
/// </summary>
public class ResearchAgeSweepTests
{
    //  A1: a (root) · b (root) · c (a) · mil (a, military subtree) · milrep (mil, RECURSIVE — repeatable) · law (a, CIVIC)
    //  A2: m2 (a) · n2 (b)        A3: z3 (m2) · x3 (a)        A4: y4 (z3)            stage: a
    internal static Spec SweepSpec(bool on, bool routing = false)
    {
        var s = new Spec { Stage = "a", AgeSweep = on, DestinationRouting = routing };
        s.Technologies.AddRange([
            new Node(1, "a", Cost: 1000.0), new Node(2, "b", Cost: 400.0), new Node(3, "c", "a", Cost: 700.0),
            new Node(4, "mil", "a", Cost: 500.0, Branch: "military"),
            new Node(5, "milrep", "mil", Cost: 900.0, Branch: "military"),
            new Node(6, "m2", "a", Cost: 500.0, Age: "A2"), new Node(7, "n2", "b", Cost: 800.0, Age: "A2"),
            new Node(8, "z3", "m2", Cost: 300.0, Age: "A3"), new Node(9, "x3", "a", Cost: 200.0, Age: "A3"),
            new Node(10, "y4", "z3", Cost: 300.0, Age: "A4"),
        ]);
        s.Recursive.Add("milrep");
        s.Civics.Add(new Node(1001, "law", "a", Cost: 600.0));
        return s;
    }

    private static readonly Lazy<ResearchContent> On = new(() => SweepSpec(true).Load());
    private static readonly Lazy<ResearchContent> Off = new(() => SweepSpec(false).Load());

    private static bool[] Done(ResearchContent c, params string[] ids)
    {
        var mask = new bool[c.Nodes.Count];
        foreach (string id in ids) mask[c.IndexOfId(id)] = true;
        return mask;
    }

    private static bool Avail(ResearchContent c, string id, params string[] done) =>
        ResearchQuery.AvailableMask(c, Done(c, done))[c.IndexOfId(id)];

    private static int[] Keys(ResearchNodeId[] ids)
    {
        var k = new int[ids.Length];
        for (int i = 0; i < ids.Length; i++) k[i] = ids[i].Value;
        return k;
    }

    [Fact]
    public void Ages1And2_AreExempt()
    {
        ResearchContent c = On.Value;
        Assert.True(Avail(c, "a"));
        Assert.True(Avail(c, "b"));
        Assert.True(Avail(c, "m2", "a"));                       // Age 2: b, c, mil, law (all Age 1) still outstanding
        Assert.False(Avail(c, "n2", "a"));                      // its own prerequisite (b) — not the sweep
        Assert.False(ResearchQuery.SweepBlock(c, Done(c, "a"), c.IndexOfId("m2")).Applies);
        Assert.False(ResearchQuery.SweepBlock(c, Done(c), c.IndexOfId("a")).Applies);
    }

    [Fact]
    public void Age3_NeedsEveryAge1Node_ReportedApartFromThePrerequisites()
    {
        ResearchContent c = On.Value;
        bool[] done = Done(c, "a", "m2");
        int z3 = c.IndexOfId("z3");
        Assert.True(ResearchQuery.PrerequisitesMet(c, z3, done));          // the authored prerequisite holds…
        Assert.False(ResearchQuery.AvailableMask(c, done)[z3]);            // …the sweep alone blocks it
        ResearchQuery.SweepState sweep = ResearchQuery.SweepBlock(c, done, z3);
        Assert.True(sweep.Applies && sweep.Blocked);
        Assert.Equal(1, sweep.CohortAge);
        Assert.Equal([2, 3, 4, 1001], Keys(sweep.Outstanding));           // b, c, mil, law — both trees; milrep exempt
        Assert.False(Avail(c, "x3", "a", "m2"));
        Assert.True(Avail(c, "z3", "a", "b", "c", "mil", "law", "m2"));
        Assert.True(Avail(c, "x3", "a", "b", "c", "mil", "law"));
    }

    [Fact]
    public void OneMissingCohortNode_Blocks_AndBothTreesCount()
    {
        ResearchContent c = On.Value;
        // Every Age-1 technology done, the Age-1 CIVIC missing: blocked by it alone.
        bool[] noCivic = Done(c, "a", "b", "c", "mil", "m2");
        ResearchQuery.SweepState s1 = ResearchQuery.SweepBlock(c, noCivic, c.IndexOfId("z3"));
        Assert.True(s1.Blocked);
        Assert.Equal([1001], Keys(s1.Outstanding));
        Assert.False(ResearchQuery.AvailableMask(c, noCivic)[c.IndexOfId("z3")]);
        // The civic done, one technology (c) missing: blocked by it alone.
        bool[] noTech = Done(c, "a", "b", "mil", "law", "m2");
        Assert.Equal([3], Keys(ResearchQuery.SweepBlock(c, noTech, c.IndexOfId("z3")).Outstanding));
        Assert.False(ResearchQuery.AvailableMask(c, noTech)[c.IndexOfId("z3")]);
        Assert.False(ResearchQuery.SweepSatisfied(c, c.IndexOfId("z3"), noTech));
    }

    [Fact]
    public void Repeatables_AreNotCohortMembers()
    {
        ResearchContent c = On.Value;
        ResearchNode rep = c.Nodes[c.IndexOfId("milrep")];
        Assert.True(rep.HasRepeatableDescriptor);
        Assert.False(ResearchQuery.IsSweepCohortMember(rep));
        bool[] done = Done(c, "a", "b", "c", "mil", "law", "m2");          // milrep (Age 1) NOT complete
        Assert.True(ResearchQuery.AvailableMask(c, done)[c.IndexOfId("z3")]);
        Assert.True(ResearchQuery.SweptAges(c, done)[1]);
    }

    [Fact]
    public void Age4_ReadsOnlyTheAge2Cohort()
    {
        ResearchContent c = On.Value;
        int y4 = c.IndexOfId("y4");
        // A mask (not a reachable history): every Age-2 node and z3 done, the Age-1 civic not — y4's sweep reads Age 2 only.
        Assert.True(ResearchQuery.SweepSatisfied(c, y4, Done(c, "m2", "n2", "z3")));
        bool[] missingN2 = Done(c, "a", "b", "c", "mil", "law", "m2", "z3");
        ResearchQuery.SweepState s = ResearchQuery.SweepBlock(c, missingN2, y4);
        Assert.Equal(2, s.CohortAge);
        Assert.Equal([7], Keys(s.Outstanding));
        Assert.False(ResearchQuery.AvailableMask(c, missingN2)[y4]);
    }

    /// <summary>Every one of the 2^11 completion masks of the rig: with the switch OFF availability is the pre-sweep
    /// formula exactly; ON it is that formula AND an independent statement of the sweep; the single-node and the
    /// precomputed (SweptAges) paths agree.</summary>
    [Fact]
    public void Exhaustive_AllMasks_SwitchOffIsTheOldRuleExactly_AndOnAddsExactlyTheSweep()
    {
        ResearchContent on = On.Value, off = Off.Value;
        int n = on.Nodes.Count;
        Assert.Equal(11, n);
        for (int bits = 0; bits < 1 << n; bits++)
        {
            var done = new bool[n];
            for (int i = 0; i < n; i++) done[i] = (bits & (1 << i)) != 0;
            bool[] maskOff = ResearchQuery.AvailableMask(off, done);
            bool[] maskOn = ResearchQuery.AvailableMask(on, done);
            bool stage = ResearchQuery.StageReached(on, done);
            for (int i = 0; i < n; i++)
            {
                bool old = OldRule(off, i, done);
                Assert.Equal(old, maskOff[i]);
                Assert.Equal(old, ResearchQuery.IsAvailable(off, i, done, stage));
                bool swept = true;
                int cohort = on.Nodes[i].AgeNumber - 2;
                for (int j = 0; j < n && cohort >= 1; j++)
                    if (on.Nodes[j].AgeNumber == cohort && !on.Nodes[j].HasRepeatableDescriptor && !done[j]) swept = false;
                Assert.Equal(old && swept, maskOn[i]);
                Assert.Equal(maskOn[i], ResearchQuery.IsAvailable(on, i, done, stage));
            }
        }
    }

    /// <summary>The pre-ADR-048 availability, restated: not completed, subtree open, prerequisites, recursive exhaustion
    /// (ADR-049: a subtree repeatable waits for its subtree's finite set, a civics repeatable for every finite A9 civic, a
    /// trunk repeatable for nothing more).</summary>
    private static bool OldRule(ResearchContent c, int i, bool[] done)
    {
        ResearchNode n = c.Nodes[i];
        if (done[i] || (n.Branch >= 0 && !ResearchQuery.StageReached(c, done))) return false;
        if (n.Prerequisite is { } p && !p.Evaluate(null, a => done[a], null)) return false;
        if (!n.IsRecursive) return true;
        IReadOnlyList<int> exhaust = n.Branch >= 0 ? c.FiniteNodesBySubtree[n.Branch]
            : n.Tree == ResearchTree.Civics ? c.FiniteFinalAgeCivics : [];
        foreach (int f in exhaust) if (!done[f]) return false;
        return true;
    }

    [Fact]
    public void ShippedContent_ShipsBothSwitchesOff_AndItsAvailabilityIsTheOldRule()
    {
        ResearchContent c = TestConfigs.Research();
        Assert.False(c.Tuning.AgeSweep);
        Assert.False(c.Tuning.DestinationRouting);
        int n = c.Nodes.Count;
        for (int variant = 0; variant < 12; variant++)
        {
            var done = new bool[n];
            for (int i = 0; i < n; i++) done[i] = variant < 6 ? i < variant * 90 : (i * 7 + variant) % 5 < variant - 5;
            bool[] mask = ResearchQuery.AvailableMask(c, done);
            for (int i = 0; i < n; i++) Assert.Equal(OldRule(c, i, done), mask[i]);
        }
    }

    [Fact]
    public void ShippedContent_SweepOn_EveryAgeLabelIsReadAndAge3IsBlockedFromScratch()
    {
        ResearchContent c = ContentWith(sweep: true, routing: false);
        var done = new bool[c.Nodes.Count];
        bool[] mask = ResearchQuery.AvailableMask(c, done);
        for (int i = 0; i < mask.Length; i++)
            if (c.Nodes[i].AgeNumber >= 3) Assert.False(mask[i], c.Nodes[i].Id + " is available from scratch under the sweep");
    }

    /// <summary>The shipped research.json with the two ADR-048 switches set (a test-only variant).</summary>
    public static ResearchContent ContentWith(bool sweep, bool routing)
    {
        using var stream = Sim.Data.DataFiles.OpenResearch();
        var doc = System.Text.Json.Nodes.JsonNode.Parse(stream)!.AsObject();
        doc["tuning"]!["ageSweep"] = sweep;
        doc["tuning"]!["destinationRouting"] = routing;
        using var goods = Sim.Data.DataFiles.OpenGoods();
        return ResearchContentLoader.Load(doc.ToJsonString(), GoodsConfigLoader.Load(goods));
    }

    // ------------------------------------------------------------------ the engine applies it (player orders, city states)

    [Fact]
    public void Engine_AnOrderForASweepBlockedNode_ChangesNothing_AndTheTwinAcceptsIt()
    {
        foreach ((ResearchContent c, bool accepted) in new[] { (On.Value, false), (Off.Value, true) })
        {
            var log = new OrderLog();
            log.Append(Target(0, 9));                                        // x3: prerequisite a done; the sweep blocks it
            WorldState w = Executor(c, log).Step(WithCompleted(PlayerWorld(), 1));
            // Off: x3 (200 RP) is funded and completes in the step (1 000 RP). On: the order changed nothing at all.
            Assert.Equal(accepted, ResearchRigs.Done(w, 9));
            Assert.False(ResearchQuery.TryGetTarget(w, new PolityId(Player), out _));
            Assert.Equal(0.0, Progress(w, 9));
        }
    }

    [Fact]
    public void CityState_ObeysTheSweep_AndTheTwinDoesNot()
    {
        int x3Before = -1;
        foreach (bool on in new[] { true, false })
        {
            Spec spec = SweepSpec(on);
            spec.CityStatePace = 0.5;
            ResearchContent c = spec.Load();
            WorldState w = World([Player], [Player], 10_000);
            AddUncontrolledSettlement(w, 1, 10_000);
            PolityId local = SettlementKnowledge.LocalHolder(new SettlementId(1));
            TurnExecutor ex = Executor(c);
            var turnOf = new Dictionary<string, int>(StringComparer.Ordinal);   // test bookkeeping, not sim logic
            for (int t = 1; t <= 40; t++)
            {
                WorldState next = ex.Step(w);
                foreach (ResearchNodeId k in ResearchQuery.CompletedBetween(w, next, local)) turnOf[c.Nodes[c.IndexOf(k)].Id] = t;
                w = next;
            }
            Assert.True(turnOf.ContainsKey("x3") && turnOf.ContainsKey("z3"));
            int lastAge1 = 0;
            foreach (string id in new[] { "a", "b", "c", "mil", "law" }) lastAge1 = Math.Max(lastAge1, turnOf[id]);
            if (on)
            {
                Assert.True(turnOf["x3"] > lastAge1, "x3 completed before the Age-1 cohort");
                Assert.True(turnOf["z3"] > lastAge1, "z3 completed before the Age-1 cohort");
            }
            else
            {
                x3Before = turnOf["x3"] < lastAge1 ? 1 : 0;
            }
        }
        Assert.Equal(1, x3Before);   // the twin: the cheap Age-3 node jumps the Age-1 cohort (the rule has teeth here)
    }

    /// <summary>A settlement no Empire controls (a city-state) with <paramref name="adults"/> adults.</summary>
    internal static void AddUncontrolledSettlement(WorldState w, int settlement, long adults)
    {
        var id = new SettlementId(settlement);
        w.Settlements.Add(new SettlementRow(id, SiteCell: settlement, FoundedTurn: 0));
        int row = w.Buckets.Add(new BucketRow(id, new CultureId(1), new ReligionId(1), new ClassId(1),
            cohortIdx: 5, Conserved.Zero, 0.0, 0.0, 0.0, 0.0));
        new Ledger(w.LedgerFlows).Flow(ref w.Buckets.Ref(row).Count, ConservedQuantityIds.Population,
            ReasonIds.InitialEndowment, adults, FlowDirection.Source, OverdrawPolicy.Throw);
    }
}
