using Sim.Core.Kernel;
using Sim.Core.State;
using Sim.Core.Systems.Ages;

namespace Sim.Core.Systems.Research;

/// <summary>
/// ADR-033 D5 — THE PROVISIONAL AI RESEARCH POLICY (the AgeAdvancePolicy / RoadDevelopmentPolicy shape). It
/// is NOT a system and writes no state: it returns the SetResearchTarget ORDER built by
/// <see cref="ResearchQuery.TargetOrder"/> — the constructor the player's research tree uses, refusing
/// exactly what ResearchSystem would ignore — which the session appends to the order log, so an AI's
/// research goes through ResearchSystem as the player's does and an AI run replays from its log.
///
/// THE RULE, stated so nothing about it is hidden: an AI-commanded polity orders a target only when it has
/// none (none was ever set, or the last one completed). It then targets the cheapest AVAILABLE node among
/// the prerequisite-ancestors (the node itself included) of its NEXT Age's unmet CORE research milestones
/// (ages.json; e.g. cereal_cultivation for the Neolithic), and failing that the cheapest available node
/// anywhere. "Cheapest" is the composite key (EffectiveCost, node key): an ascending-key scan that replaces
/// the best only on a strictly lower cost, so among bit-equal costs the lowest key wins — the house rule for
/// any argmin over doubles (tie-dense test: AiPolicyTests). No node id appears in code: the goal comes from
/// ages.json. Strategic research planning is later AI work. Player-commanded polities are never touched.
/// </summary>
public static class AiResearchPolicy
{
    /// <summary>The order the AI issues for <paramref name="polity"/> this turn, or null.</summary>
    public static OrderRecord? Decide(IReadOnlyWorldState world, SimConfig cfg, PolityId polity)
    {
        if (cfg.Research is not { } content) return null;
        if (!EmpireQuery.TryGetCommandSource(world, polity, out CommandSource source) || source != CommandSource.Ai) return null;
        bool[] completed = ResearchQuery.CompletedMask(world, content, polity);
        bool[] available = ResearchQuery.AvailableMask(content, completed);
        // A live target is kept: switching would only scatter progress (it is retained, never lost).
        if (ResearchQuery.TryGetTarget(world, polity, out ResearchNodeId current)
            && content.IndexOf(current) is int held and >= 0 && available[held]) return null;

        int choice = Choose(world, content, cfg.Ages, polity, completed, available);
        return choice < 0 ? null : ResearchQuery.TargetOrder(world, content, polity, content.Nodes[choice].Key);
    }

    /// <summary>The node the rule picks (dense index), or -1 when nothing is available.</summary>
    public static int Choose(
        IReadOnlyWorldState world, ResearchContent content, AgeContent? ages, PolityId polity, bool[] completed, bool[] available)
    {
        bool[] goal = CoreGoalClosure(world, content, ages, polity);
        int best = Cheapest(world, content, polity, available, goal);
        return best >= 0 ? best : Cheapest(world, content, polity, available, null);
    }

    /// <summary>
    /// The prerequisite-ancestors (each node included) of the nodes named by the polity's NEXT Age's UNMET core
    /// milestones whose fact is research (AgeQuery.Milestone decides "met", the evaluator the Age systems use).
    /// All false at the final Age, without Age content, or when every core research milestone is met.
    /// </summary>
    public static bool[] CoreGoalClosure(IReadOnlyWorldState world, ResearchContent content, AgeContent? ages, PolityId polity)
    {
        var goal = new bool[content.Nodes.Count];
        if (ages is null || AgeQuery.NextAge(world, ages, polity) is not { Entry: { } entry }) return goal;
        var stack = new Stack<int>();
        foreach (AgeMilestone core in entry.Core)
        {
            if (core.Fact.Kind != MilestoneFactKind.Research || AgeQuery.Milestone(world, polity, core).Met) continue;
            foreach (int key in core.Fact.NodeKeys)
            {
                int index = content.IndexOf(new ResearchNodeId(key));
                if (index >= 0) stack.Push(index);
            }
        }
        while (stack.Count > 0)
        {
            int i = stack.Pop();
            if (goal[i]) continue;
            goal[i] = true;
            foreach (int p in content.Nodes[i].PrerequisiteNodes) stack.Push(p);
        }
        return goal;
    }

    /// <summary>The available node (within <paramref name="within"/>, when given) with the lowest
    /// EffectiveCost, ties to the LOWER key — composite key (EffectiveCost, key); -1 when none.</summary>
    public static int Cheapest(IReadOnlyWorldState world, ResearchContent content, PolityId polity, bool[] available, bool[]? within)
    {
        int best = -1;
        double bestCost = 0.0;
        for (int i = 0; i < available.Length; i++)   // ascending index = ascending key
        {
            if (!available[i] || (within is not null && !within[i])) continue;
            double cost = ResearchQuery.EffectiveCost(world, content, polity, i);
            if (best < 0 || cost < bestCost) { best = i; bestCost = cost; }
        }
        return best;
    }
}
