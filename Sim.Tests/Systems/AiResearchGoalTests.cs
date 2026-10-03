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
/// each only while unmet. The choice is goal-SEQUENTIAL (ADR-033 D5 follow-up): the goal with the cheapest
/// REMAINING closure (composite key (remainingCost, ordinal), tie-dense below), then its cheapest AVAILABLE node
/// (composite key (EffectiveCost, key), tie-dense below and in AiPolicyTests).
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
    public void TheChoice_IsGoalSequential_TheCheapestRemainingGoal_ThenItsCheapestAvailableNode()
    {
        WorldState w = Duo();
        bool[] completed = ResearchQuery.CompletedMask(w, Research, Rival);
        bool[] available = ResearchQuery.AvailableMask(Research, completed);
        bool[][] goals = AiResearchPolicy.Goals(w, Cfg, Rival);
        Assert.Equal(AiResearchPolicy.GoalCount, goals.Length);
        bool[] union = AiResearchPolicy.GoalClosure(w, Cfg, Rival);
        bool[] capability = Goals(w, Cfg);
        for (int i = 0; i < union.Length; i++)
        {
            Assert.Equal(capability[i], goals[1][i] || goals[2][i] || goals[3][i]);
            Assert.Equal(union[i], goals[0][i] || capability[i]);
        }

        // The goal: an independent recomputation of the (remainingCost, ordinal) argmin over goals with an available node.
        int expectedGoal = -1;
        double expectedCost = 0.0;
        for (int g = 0; g < goals.Length; g++)
        {
            double remaining = 0.0;
            bool reachable = false;
            for (int i = 0; i < goals[g].Length; i++)
                if (goals[g][i] && !completed[i]) { remaining += ResearchQuery.EffectiveCost(w, Research, Rival, i); reachable |= available[i]; }
            if (reachable && (expectedGoal < 0 || remaining < expectedCost)) { expectedGoal = g; expectedCost = remaining; }
        }
        Assert.Equal(expectedGoal, AiResearchPolicy.ChooseGoal(w, Research, Rival, goals, completed, available));
        int expected = AiResearchPolicy.Cheapest(w, Research, Rival, available, goals[expectedGoal]);
        Assert.True(expected >= 0 && goals[expectedGoal][expected] && available[expected]);
        Assert.Equal(expected, AiResearchPolicy.Choose(w, Cfg, Rival, completed, available));
        Assert.Equal(Research.Nodes[expected].Key.Value, AiResearchPolicy.Decide(w, Cfg, Rival)!.Value.TargetId);
    }

    [Fact]
    public void GoalChoice_TieDense_BitEqualRemainingCosts_GoToTheLowerOrdinal_AndUnreachableGoalsAreSkipped()
    {
        // Two singleton goals over the 520-RP knapping pair (bit-equal EffectiveCost, both available once
        // grinding_stone is known): whichever order they are listed in, ordinal 0 wins the tie.
        WorldState w = Duo();
        Complete(w, "grinding_stone");
        bool[] completed = ResearchQuery.CompletedMask(w, Research, Rival);
        bool[] available = ResearchQuery.AvailableMask(Research, completed);
        int[] tied = Enumerable.Range(0, available.Length).Where(i => available[i]).ToArray();
        double c0 = tied.Select(i => ResearchQuery.EffectiveCost(w, Research, Rival, i)).GroupBy(c => c).Where(g => g.Count() >= 2).Select(g => g.Key).Min();
        tied = tied.Where(i => ResearchQuery.EffectiveCost(w, Research, Rival, i) == c0).ToArray();
        Assert.True(tied.Length >= 2, "the canonical bit-equal tie this pins has disappeared");
        bool[] Only(int i) { var g = new bool[available.Length]; g[i] = true; return g; }
        int lo = tied[0], hi = tied[1];
        bool[] none = new bool[available.Length];
        Assert.Equal(0, AiResearchPolicy.ChooseGoal(w, Research, Rival, [Only(lo), Only(hi)], completed, available));
        Assert.Equal(0, AiResearchPolicy.ChooseGoal(w, Research, Rival, [Only(hi), Only(lo)], completed, available));
        // ...dense: every goal tied → ordinal 0; an empty (met) goal ahead of them is skipped, not chosen.
        Assert.Equal(1, AiResearchPolicy.ChooseGoal(w, Research, Rival, [none, Only(hi), Only(lo), Only(hi)], completed, available));
        // A strictly lower remaining cost wins regardless of ordinal: add a second uncompleted node to goal 0.
        bool[] heavier = Only(lo); heavier[hi] = true;
        Assert.Equal(1, AiResearchPolicy.ChooseGoal(w, Research, Rival, [heavier, Only(hi)], completed, available));
        // A goal whose uncompleted nodes are all unavailable has nothing to work on: it is skipped.
        int blocked = Enumerable.Range(0, available.Length).First(i => !available[i] && !completed[i]);
        Assert.Equal(1, AiResearchPolicy.ChooseGoal(w, Research, Rival, [Only(blocked), heavier], completed, available));
        Assert.Equal(-1, AiResearchPolicy.ChooseGoal(w, Research, Rival, [Only(blocked), none], completed, available));

        // Within-goal tie-dense: a goal holding the whole tied set picks the LOWER key, in either listing.
        bool[] pair = Only(lo); pair[hi] = true;
        int pick = AiResearchPolicy.Cheapest(w, Research, Rival, available, pair);
        Assert.Equal(Research.Nodes[lo].Key.Value < Research.Nodes[hi].Key.Value ? lo : hi, pick);
    }
}
