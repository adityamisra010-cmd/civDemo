using Sim.Core.Kernel;
using Sim.Core.State;
using Sim.Core.Systems.Ages;
using Sim.Core.Systems.ClassMobility;

namespace Sim.Core.Systems.Research;

/// <summary>
/// ADR-033 D5 — THE PROVISIONAL AI RESEARCH POLICY (the AgeAdvancePolicy / RoadDevelopmentPolicy shape). It
/// is NOT a system and writes no state: it returns the SetResearchTarget ORDER built by
/// <see cref="ResearchQuery.TargetOrder"/> — the constructor the player's research tree uses, refusing
/// exactly what ResearchSystem would ignore — which the session appends to the order log, so an AI's
/// research goes through ResearchSystem as the player's does and an AI run replays from its log.
///
/// THE RULE, stated so nothing about it is hidden: an AI-commanded polity orders a target only when it has
/// none (none was ever set, or the last one completed). It then targets the cheapest AVAILABLE node within
/// its GOAL CLOSURE, and failing that the cheapest available node anywhere. "Cheapest" is the composite key
/// (EffectiveCost, node key): an ascending-key scan that replaces the best only on a strictly lower cost, so
/// among bit-equal costs the lowest key wins — the house rule for any argmin over doubles (tie-dense tests:
/// AiPolicyTests, AiResearchGoalTests).
///
/// THE GOAL CLOSURE (ADR-033 B; generalized from S2's core-only goal, which in 320 measured turns never
/// reached a road class, so roads, taxation and universities stayed dead for AI polities) is the union of
/// the prerequisite-ancestors (each node included; union over OR) of:
/// <list type="bullet">
/// <item>its NEXT Age's unmet CORE research milestones (ages.json; e.g. cereal_cultivation for the Neolithic);</item>
/// <item>the requirements of the capability-gated actions the AI itself uses, while unmet: the NEXT unknown
///   road class (the lowest sim.json <c>roads.classes[]</c> above the best known, its <c>entity</c>'s
///   requirement — RoadDevelopmentPolicy), the taxation requirement (<c>governance.taxationRequires</c> —
///   AiGovernance), and the university requirement (each founding project's <c>entity</c> and
///   <c>founds.entity</c> — AiConstructionPolicy, ADR-033 D6). An institution named inside a requirement is
///   expanded to its own knowledge requirement, the evaluator's own reading.</item>
/// </list>
/// No node id appears in code: every goal comes from content. Strategic research planning is later AI work.
/// Player-commanded polities are never touched.
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

        int choice = Choose(world, cfg, polity, completed, available);
        return choice < 0 ? null : ResearchQuery.TargetOrder(world, content, polity, content.Nodes[choice].Key);
    }

    /// <summary>The node the rule picks (dense index), or -1 when nothing is available.</summary>
    public static int Choose(IReadOnlyWorldState world, SimConfig cfg, PolityId polity, bool[] completed, bool[] available)
    {
        ResearchContent content = cfg.Research ?? throw new ArgumentException("the AI research policy needs research content", nameof(cfg));
        bool[] goal = GoalClosure(world, cfg, polity);
        int best = Cheapest(world, content, polity, available, goal);
        return best >= 0 ? best : Cheapest(world, content, polity, available, null);
    }

    /// <summary>THE GOAL CLOSURE (see the header): <see cref="CoreGoalClosure"/> ∪ <see cref="CapabilityGoalClosure"/>.</summary>
    public static bool[] GoalClosure(IReadOnlyWorldState world, SimConfig cfg, PolityId polity)
    {
        ResearchContent content = cfg.Research ?? throw new ArgumentException("the AI research policy needs research content", nameof(cfg));
        bool[] goal = CoreGoalClosure(world, content, cfg.Ages, polity);
        bool[] capability = CapabilityGoalClosure(world, cfg, polity);
        for (int i = 0; i < goal.Length; i++) goal[i] |= capability[i];
        return goal;
    }

    /// <summary>
    /// The prerequisite-ancestors (each node included; union over OR) of the knowledge requirements of the
    /// capability-gated actions the AI itself uses, while each is unmet: the next unknown road class, the tax
    /// edict, and the university founding (see the header). All false when every one is already met.
    /// </summary>
    public static bool[] CapabilityGoalClosure(IReadOnlyWorldState world, SimConfig cfg, PolityId polity)
    {
        ResearchContent content = cfg.Research ?? throw new ArgumentException("the AI research policy needs research content", nameof(cfg));
        var stack = new Stack<int>();
        var expanded = new bool[content.Entities.Count];

        // 1. The NEXT unknown road class: the lowest class above the best the polity knows (RoadDevelopmentPolicy).
        if (cfg.Roads is { } roads)
        {
            int best = RoadDevelopmentQuery.BestKnownClass(world, content, roads, polity);
            RoadClassConfig? next = null;
            foreach (RoadClassConfig c in roads.Classes)
                if (c.Entity is not null && c.EdgeType > best && (next is null || c.EdgeType < next.EdgeType)) next = c;
            if (next?.Entity is { } entity) PushEntity(content, content.EntityIndexOf(entity), stack, expanded);
        }

        // 2. The tax edict's research gate while it is unmet (AiGovernance levies through it).
        if (cfg.Governance is { } governance && !global::Sim.Core.State.Governance.CanLevyTax(world, cfg, polity))
        {
            Predicate requirement = ResearchContentLoader.ParseRequirement(
                content, governance.TaxationRequires, "sim.json governance.taxationRequires");
            foreach (int atom in requirement.AtomIds)
                if (atom >= 0 && atom < content.Nodes.Count) stack.Push(atom);
        }

        // 3. The university requirement while unmet (AiConstructionPolicy founds universities, ADR-033 D6):
        // each founding project's building entity and the institution it founds.
        if (cfg.Institutions is not null && cfg.Goods?.Projects is { } projects)
        {
            foreach (ConstructionProjectEntry project in projects)
            {
                if (project.Founds is not { } founds) continue;
                if (ConstructionQuery.IsKnowledgeEligible(world, content, polity, project)) continue;
                if (project.Entity is { } building) PushEntity(content, content.EntityIndexOf(building), stack, expanded);
                PushEntity(content, content.EntityIndexOf(founds.Entity), stack, expanded);
            }
        }

        var goal = new bool[content.Nodes.Count];
        while (stack.Count > 0)
        {
            int i = stack.Pop();
            if (goal[i]) continue;
            goal[i] = true;
            foreach (int p in content.Nodes[i].PrerequisiteNodes) stack.Push(p);
        }
        return goal;
    }

    /// <summary>Pushes an entity's node atoms, expanding every institution it names to that institution's own
    /// requirement (the knowledge evaluator's reading); each entity once.</summary>
    private static void PushEntity(ResearchContent content, int entity, Stack<int> stack, bool[] expanded)
    {
        if (entity < 0 || expanded[entity]) return;
        expanded[entity] = true;
        ResearchEntity e = content.Entities[entity];
        foreach (int atom in e.NodeAtoms) stack.Push(atom);
        foreach (int institution in e.InstitutionAtoms) PushEntity(content, institution, stack, expanded);
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
