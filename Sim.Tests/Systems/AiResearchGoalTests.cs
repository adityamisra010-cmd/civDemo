using Sim.Core;
using Sim.Core.Kernel;
using Sim.Core.State;
using Sim.Core.Systems;
using Sim.Core.Systems.Research;
using Sim.Core.Systems.Roads;
using Sim.Core.Worldgen;
using Sim.Tests.TestUtil;

namespace Sim.Tests.Systems;

/// <summary>
/// ADR-033 B — the AI's research goals cover the capability-gated actions it uses. S2's policy aimed only at the
/// next Age's core research and, measured over 320 turns with aiEmpires = 1, never researched a road class —
/// roads, taxation and universities stayed dead for AI polities. The goal closure is now the core closure UNION
/// the prerequisite closures of: the next unknown road class (sim.json roads.classes[].entity), the tax gate
/// (governance.taxationRequires) and the university founding (the founding projects' entity + founds.entity),
/// each only while unmet; the choice is the cheapest AVAILABLE node within the union (composite key
/// (EffectiveCost, key), tie-dense — AiPolicyTests pins the canonical 520-RP tie).
/// </summary>
public class AiResearchGoalTests
{
    private static readonly SimConfig Cfg = TestConfigs.Sim();
    private static readonly ResearchContent Research = TestConfigs.Research();
    private static readonly PolityId Rival = new(2);

    private static readonly Lazy<WorldState> DevDuo = new(() =>
        WorldFounding.Found(TestConfigs.DevWorldgen() with { AiEmpires = 1 }, Cfg, 42));

    private static WorldState Duo() => DevDuo.Value.Clone();

    private static bool[] Closure(params string[] ids)
    {
        var goal = new bool[Research.Nodes.Count];
        var stack = new Stack<int>();
        foreach (string id in ids) stack.Push(Research.IndexOfId(id));
        while (stack.Count > 0)
        {
            int i = stack.Pop();
            if (goal[i]) continue;
            goal[i] = true;
            foreach (int p in Research.Nodes[i].PrerequisiteNodes) stack.Push(p);
        }
        return goal;
    }

    private static void Complete(WorldState w, params string[] ids)
    {
        foreach (string id in ids)
            if (!ResearchQuery.IsCompleted(w, Rival, Research.Nodes[Research.IndexOfId(id)].Key))
                w.ResearchCompleted.Add(new ResearchCompletedRow(Rival, Research.Nodes[Research.IndexOfId(id)].Key));
    }

    private static bool[] Goals(WorldState w, SimConfig cfg) =>
        AiResearchPolicy.CapabilityGoalClosure(w, cfg, Rival);

    private static bool Subset(bool[] a, bool[] b)
    {
        for (int i = 0; i < a.Length; i++) if (a[i] && !b[i]) return false;
        return true;
    }

    [Fact]
    public void CapabilityGoals_AreTheNextRoadClass_TheTaxGate_AndTheUniversity_EachOnlyWhileUnmet()
    {
        WorldState w = Duo();
        SimConfig roadsOnly = Cfg with { Governance = null, Institutions = null };
        SimConfig taxOnly = Cfg with { Roads = null, Institutions = null };
        SimConfig universityOnly = Cfg with { Roads = null, Governance = null };

        // ROADS: the next unknown class is the track (infra.road_track ← track_road): exactly its closure.
        Assert.Equal(Closure("track_road"), Goals(w, roadsOnly));
        Complete(w, "track_road");
        Assert.Equal(2, RoadDevelopmentQuery.BestKnownClass(w, Research, Cfg.Roads!, Rival));
        // ...known → the NEXT class (infra.road_built ← track_road AND stone_dry) becomes the goal.
        Assert.Equal(Closure("track_road", "stone_dry"), Goals(w, roadsOnly));

        // TAX: the union of the four alternatives' closures while no taxation node is known; nothing once one is.
        Assert.Equal(Closure("arithmetic_babylonian", "surveying", "standard_weights", "coinage_electrum"), Goals(w, taxOnly));
        Complete(w, "arithmetic_babylonian");
        Assert.True(Governance.CanLevyTax(w, Cfg, Rival));
        Assert.DoesNotContain(true, Goals(w, taxOnly));

        // UNIVERSITY: building.university's requirement and inst.university's, the latter's institutions expanded
        // (library → archive, scribal_school) — exactly the research stage's closure (D-044 R4: the stage exists
        // to support specialized universities).
        bool[] university = Goals(w, universityOnly);
        Assert.Equal(Closure("medicine_hippocratic", "geometry_axiomatic", "algebra", "legal_code_roman",
            "cuneiform", "hieroglyphic", "chinese_script", "stamp_seal"), university);
        bool[] stage = new bool[Research.Nodes.Count];
        foreach (int atom in Research.Stage.AtomIds)
        {
            bool[] closure = Closure(Research.Nodes[atom].Id);
            for (int i = 0; i < stage.Length; i++) stage[i] |= closure[i];
        }
        Assert.True(Subset(stage, university), "every node the research stage needs is a university goal");
        UniversityRigs.Grant(w, Research, Rival);
        Assert.DoesNotContain(true, Goals(w, universityOnly));   // both entities eligible: the goal is met
    }

    [Fact]
    public void TheChoice_IsTheCheapestAvailableNodeOfTheUnion_AndTheCoreGoalIsStillPartOfIt()
    {
        WorldState w = Duo();
        bool[] completed = ResearchQuery.CompletedMask(w, Research, Rival);
        bool[] available = ResearchQuery.AvailableMask(Research, completed);
        bool[] union = AiResearchPolicy.GoalClosure(w, Cfg, Rival);
        bool[] core = AiResearchPolicy.CoreGoalClosure(w, Research, Cfg.Ages, Rival);
        Assert.True(Subset(core, union));
        Assert.True(Subset(Goals(w, Cfg), union));
        // The choice is the composite-key argmin over the available nodes of the union, recomputed independently.
        int expected = -1;
        for (int i = 0; i < available.Length; i++)
        {
            if (!available[i] || !union[i]) continue;
            if (expected < 0 || ResearchQuery.EffectiveCost(w, Research, Rival, i) < ResearchQuery.EffectiveCost(w, Research, Rival, expected)) expected = i;
        }
        Assert.Equal(expected, AiResearchPolicy.Choose(w, Cfg, Rival, completed, available));
        Assert.Equal(Research.Nodes[expected].Key.Value, AiResearchPolicy.Decide(w, Cfg, Rival)!.Value.TargetId);
    }
}
