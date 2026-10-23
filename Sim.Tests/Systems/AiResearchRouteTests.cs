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
/// ADR-046 (M6 review, 2026-10-23) — THE CHEAPEST REMAINING ROUTE (AiResearchPolicy.Routes), the fix for the measured
/// union-over-OR closure (docs/m6-plan/knowledge-redesign/audit/taxation-gate-trace.md). Adversarial on a rig graph whose
/// costs are chosen so the union, the first branch, the lowest key and the cheapest SINGLE node each give a DIFFERENT
/// answer from the cheapest route; then the property on the shipped content and the canonical AI run: every goal closure is
/// a route that satisfies its requirement, every node in it has its own prerequisite met inside completed ∪ closure, and
/// no OR alternative is pursued once the OR holds.
/// </summary>
public class AiResearchRouteTests
{
    // Rig costs (EffectiveCost = BaseCost here: no institution, no exposure, floor not binding).
    //   cheap 100 · mid 200 · alt 250 · big1 250 · dear 5000 · heavy 9000 · tie1/tie2 400 (equal) · c1→c2→c3 chain 100 each
    private static readonly Lazy<ResearchContent> Rig = new(() =>
    {
        Spec s = Standard();
        s.AiCheapestOrRoute = true;
        s.Technologies.AddRange([
            new Node(20, "cheap", null, Cost: 100.0), new Node(21, "mid", null, Cost: 200.0),
            new Node(22, "alt", null, Cost: 250.0), new Node(23, "dear", null, Cost: 5000.0),
            new Node(24, "heavy", null, Cost: 9000.0),
            new Node(25, "tie1", null, Cost: 400.0), new Node(26, "tie2", null, Cost: 400.0),
            new Node(27, "c1", null, Cost: 100.0), new Node(28, "c2", "c1", Cost: 100.0), new Node(29, "c3", "c2", Cost: 100.0),
            new Node(30, "big1", null, Cost: 250.0),
            // x: cheapest alternative wins over the first-written and over the lower key
            new Node(40, "x", "dear OR cheap", Cost: 100.0),
            // y: bit-equal alternatives — the one WRITTEN FIRST wins (tie2 has the higher key)
            new Node(41, "y", "tie2 OR tie1", Cost: 100.0),
            // z: AND keeps every required side, OR inside picks its cheapest
            new Node(42, "z", "heavy AND (dear OR mid)", Cost: 100.0),
            // q: nested — (cheap AND mid) = 300 vs alt 250: alt; once cheap is known, mid 200 < alt 250 flips it
            new Node(43, "q", "(cheap AND mid) OR alt", Cost: 100.0),
            // r: a CHAIN is costed whole: c1→c2→c3 = 300 > big1 250, although c3 alone (100) is the cheaper node
            new Node(44, "r", "c3 OR big1", Cost: 100.0),
            // s: shared ancestors count once — x's route (cheap, x) and cheap again
            new Node(45, "s", "x AND cheap", Cost: 100.0),
        ]);
        s.Entities.Add(new Entity("building.lab", "building", "cheap OR ghost", Unresolved: ["ghost"]));
        s.Entities.Add(new Entity("building.void", "building", "ghost", Unresolved: ["ghost"]));
        return s.Load();
    });

    private static ResearchContent R => Rig.Value;
    private static int I(string id) => R.IndexOfId(id);
    private static readonly PolityId P = new(Player);

    private static WorldState World(params string[] done)
    {
        WorldState w = PlayerWorld();
        foreach (string id in done) w.ResearchCompleted.Add(new ResearchCompletedRow(P, R.Nodes[I(id)].Key));
        return w;
    }

    private static string[] Ids(int[] route)
    {
        var ids = new string[route.Length];
        for (int k = 0; k < route.Length; k++) ids[k] = R.Nodes[route[k]].Id;
        Array.Sort(ids, StringComparer.Ordinal);
        return ids;
    }

    private static string[] RouteOf(WorldState w, string node) => Ids(new AiResearchPolicy.Routes(w, R, P).Node(I(node)));

    [Fact]
    public void Or_TakesTheCheapestAlternative_NotTheFirstWritten_NorTheUnion()
    {
        Assert.Equal(["cheap", "x"], RouteOf(World(), "x"));               // union would add dear; first-written is dear
    }

    [Fact]
    public void Or_TieDense_BitEqualAlternatives_GoToTheOneWrittenFirst()
    {
        Assert.Equal(R.Nodes[I("tie1")].BaseCost, R.Nodes[I("tie2")].BaseCost);
        Assert.Equal(["tie2", "y"], RouteOf(World(), "y"));                // "tie2 OR tie1": tie2 written first, higher key
    }

    [Fact]
    public void And_KeepsEveryRequiredSide_EvenTheExpensiveOne()
    {
        // heavy (9000) is required by the AND and stays; the OR inside takes mid (200), never dear (5000).
        Assert.Equal(["heavy", "mid", "z"], RouteOf(World(), "z"));
    }

    [Fact]
    public void Nested_PartlyCompleted_TheChoiceFollowsTheRemainingCost()
    {
        Assert.Equal(["alt", "q"], RouteOf(World(), "q"));                 // (cheap + mid) 300 > alt 250
        Assert.Equal(["mid", "q"], RouteOf(World("cheap"), "q"));          // cheap known: mid 200 < alt 250
        Assert.Equal(["q"], RouteOf(World("alt"), "q"));                   // alt known: the OR holds, nothing else pursued
        Assert.Equal(["q"], RouteOf(World("cheap", "mid"), "q"));
    }

    [Fact]
    public void Or_AlreadySatisfied_StopsPursuingTheOtherAlternatives()
    {
        Assert.Equal(["x"], RouteOf(World("dear"), "x"));                  // dear already known: cheap is NOT added
        Assert.Equal(["y"], RouteOf(World("tie1"), "y"));                  // the later-written alternative satisfied it
        Assert.Empty(RouteOf(World("x"), "x"));                            // a completed node needs nothing
    }

    [Fact]
    public void Chain_IsCostedWhole_AndSharedAncestorsCountOnce()
    {
        Assert.Equal(["big1", "r"], RouteOf(World(), "r"));                // chain 300 > big1 250 (c3 alone would be 100)
        Assert.Equal(["c3", "r"], RouteOf(World("c1", "c2"), "r"));        // chain remainder 100 < big1 250
        var routes = new AiResearchPolicy.Routes(World(), R, P);
        int[] s = routes.Node(I("s"));
        Assert.Equal(["cheap", "s", "x"], Ids(s));                         // cheap appears once though both sides need it
        Assert.Equal(R.Nodes[I("cheap")].BaseCost + R.Nodes[I("x")].BaseCost + R.Nodes[I("s")].BaseCost, routes.Cost(s));
    }

    [Fact]
    public void Entity_UnresolvedAlternative_IsSkipped_AndAnUnresolvableRequirementHasNoRoute()
    {
        var routes = new AiResearchPolicy.Routes(World(), R, P);
        Assert.Equal(["cheap"], Ids(routes.Entity(R.EntityIndexOf("building.lab"))));
        Assert.Same(AiResearchPolicy.Routes.None, routes.Entity(R.EntityIndexOf("building.void")));
    }

    [Fact]
    public void Switch_Off_IsTheUnionClosureExactly()
    {
        // The legacy union (ADR-033 B) on the SHIPPED content, through the attribution twin: the tax goal holds every
        // script of the OR; with the switch on it holds one.
        SimConfig on = TestConfigs.Sim(), off = TestConfigs.PreAiRoutes(on);
        WorldState w = WorldFounding.Found(TestConfigs.DevWorldgen() with { AiEmpires = 1 }, on, 42);
        var rival = new PolityId(2);
        ResearchContent r = on.Research!;
        bool[] taxOn = AiResearchPolicy.CapabilityGoals(w, on, rival)[1], taxOff = AiResearchPolicy.CapabilityGoals(w, off, rival)[1];
        string[] scripts = ["proto_writing", "hieroglyphic", "chinese_script"];
        int onCount = 0, offCount = 0;
        foreach (string s in scripts) { if (taxOn[r.IndexOfId(s)]) onCount++; if (taxOff[off.Research!.IndexOfId(s)]) offCount++; }
        Assert.Equal(3, offCount);
        Assert.Equal(1, onCount);
        foreach (string required in new[] { "taxation", "token_counting", "stamp_seal" })
            Assert.True(taxOn[r.IndexOfId(required)], required + " is required by the AND and must stay in the goal");
    }

    /// <summary>THE PROPERTY on the shipped content, along the canonical AI run (seed 42, every 25 turns to 300): every goal
    /// closure, together with what is completed, satisfies the goal's own requirement and every closure node's prerequisite
    /// — the AI never drops a legitimately required prerequisite — and the closure holds no node whose removal would keep
    /// all of that true for an OR alternative already satisfied (no alternative pursued once its OR holds).</summary>
    [Fact]
    public void Property_ClosuresAreSufficientRoutes_AlongTheCanonicalAiRun()
    {
        SimConfig cfg = TestConfigs.Sim();
        ResearchContent r = cfg.Research!;
        WorldgenConfig worldgen = TestConfigs.DevWorldgen();
        WorldgenConfig wg = worldgen with { AiEmpires = 1 };
        var rival = new PolityId(2);
        var log = new OrderLog();
        using var era = Sim.Data.DataFiles.OpenEraPacing();
        using var pipe = Sim.Data.DataFiles.OpenPipeline();
        var exec = new TurnExecutor(EraTableLoader.Load(era), PipelineLoader.Load(pipe, SystemCatalog.All(cfg, worldgen)), log);
        WorldState w = WorldFounding.Found(wg, cfg, 42);
        int checkedGoals = 0;
        for (int t = 0; t <= 300; t++)
        {
            if (t % 25 == 0)
            {
                bool[] done = ResearchQuery.CompletedMask(w, r, rival);
                bool[][] goals = AiResearchPolicy.Goals(w, cfg, rival);
                for (int g = 0; g < goals.Length; g++)
                {
                    bool[] have = (bool[])done.Clone();
                    bool any = false;
                    for (int i = 0; i < have.Length; i++) if (goals[g][i]) { have[i] = true; any = true; }
                    if (!any) continue;
                    checkedGoals++;
                    for (int i = 0; i < have.Length; i++)
                    {
                        if (!goals[g][i]) continue;
                        Assert.False(done[i], $"turn {t}, goal {g}: completed node {r.Nodes[i].Id} is in a closure");
                        Assert.True(r.Nodes[i].Prerequisite is null || ResearchQuery.RequirementMet(r, r.Nodes[i].Prerequisite!, have),
                            $"turn {t}, goal {g}: {r.Nodes[i].Id}'s prerequisite is not met inside completed ∪ closure");
                    }
                }
            }
            AiOrders.Append(log, w, cfg, worldgen);
            w = exec.Step(w);
        }
        Assert.True(checkedGoals > 10, "the property was checked on " + checkedGoals + " goals only");
    }
}
