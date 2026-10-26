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
/// none (none was ever set, or the last one completed). It then works its goals SEQUENTIALLY (ADR-033 D5
/// follow-up; the union of all goals, measured in docs/institutions-universities.md §12.3, delayed every Age):
/// <list type="number">
/// <item>it picks ONE goal — the goal whose REMAINING closure (Σ EffectiveCost over the closure's not-yet-completed
///   nodes) is cheapest, among the goals with an available node in their closure; composite key
///   (remainingCost, goal ordinal), ordinals core 0, road 1, tax 2, university 3 — strictly-lower replacement in
///   ordinal order, so bit-equal remaining costs go to the lower ordinal;</item>
/// <item>within that goal it targets the cheapest AVAILABLE node of the goal's closure, composite key
///   (EffectiveCost, node key) — an ascending-key scan replacing only on a strictly lower cost;</item>
/// <item>with no goal left, the cheapest available node anywhere (same key).</item>
/// </list>
/// ADR-049 (research.json tuning.repeatableLevels): a levelled repeatable stays AVAILABLE after each completion, priced at its
/// NEXT level (BaseCost × g^level, <see cref="ResearchQuery.EffectiveCost"/>), so once the goal closures are exhausted the
/// fallback above reaches the repeatable levels by the same composite key (EffectiveCost, node key) — the AI researches
/// the cheapest next level instead of idling when nothing finite is left. No new rule: the levels enter the available set
/// (and the city-state's <see cref="Cheapest"/> pick) the way any node does. Off, a completed repeatable is never
/// available again — the complete-once behaviour exactly.
/// Both argmins are the house rule for any argmin over doubles (tie-dense tests: AiPolicyTests,
/// AiResearchGoalTests).
///
/// THE GOALS (ADR-033 B; generalized from S2's core-only goal, which in 320 measured turns never
/// reached a road class, so roads, taxation and universities stayed dead for AI polities) are the
/// prerequisite-ancestor closures (each node included; union over OR) of:
/// <list type="bullet">
/// <item>its NEXT Age's unmet CORE research milestones (ages.json; e.g. cereal_cultivation for the Neolithic);</item>
/// <item>the requirements of the capability-gated actions the AI itself uses, while unmet: the NEXT unknown
///   road class (the lowest sim.json <c>roads.classes[]</c> above the best known, its <c>entity</c>'s
///   requirement — RoadDevelopmentPolicy), the taxation requirement (<c>governance.taxationRequires</c> —
///   AiGovernance), and the university requirement (each founding project's <c>entity</c> and
///   <c>founds.entity</c> — AiConstructionPolicy, ADR-033 D6). An institution named inside a requirement is
///   expanded to its own knowledge requirement, the evaluator's own reading.</item>
/// </list>
/// <item>ADR-046 §6 (research.json tuning.aiSupportingMilestones, with aiCheapestOrRoute): the cheapest unmet RESEARCH
///   SUPPORTING milestones its NEXT Age still needs to meet supportingRequired and minCategories
///   (<see cref="SupportingGoalClosure"/>; goal ordinal 4).</item>
/// No node id appears in code: every goal comes from content. Strategic research planning is later AI work.
/// Player-commanded polities are never touched.
///
/// ADR-043 (D-062) — TWO LANES. When research.json tuning.civicsPoints is enabled, <see cref="DecideAll"/> returns up to
/// TWO orders, one per lane (Technology, then Civics), each built by the rule above with every goal closure and the
/// fallback RESTRICTED TO THE LANE'S TREE (<see cref="Choose(IReadOnlyWorldState, SimConfig, PolityId, bool[], bool[], ResearchTree)"/>):
/// a lane orders only when IT has no live target. So the taxation goal's civic (taxation) is worked on the Civics lane,
/// funded by Civics Points, while its technology prerequisites stay goal work on the Technology lane — the civic never
/// waits on a lane that cannot fund its prerequisites. With civicsPoints off the policy is the one-pool rule above.
///
/// ADR-048 — DESTINATION RESEARCH (research.json tuning.destinationRouting). A lane's row is a destination, live while its
/// node is incomplete; the AI orders only when the lane holds none. The goal choice is unchanged; the goal's NODE
/// (<see cref="GoalDestination"/>) becomes the destination and ResearchRoutePlanner — the player's planner — picks what is
/// funded on the way (<see cref="ChooseDestination"/>). Off: the rule above exactly.
///
/// ADR-052 (PROPOSED) — GATE-AWARE TECHNOLOGY GOALS (research.json tuning.aiGateAwareGoals; acts only with the two lanes and
/// destination routing, on the Technology lane). A technology destination whose route the lane cannot fund now — the
/// engine's planner (<see cref="ResearchRoutePlanner.Fund"/>) gives the lane no current node, typically because the Age
/// sweep needs the current Age's civics cohort first — is not live: the AI re-decides (<see cref="DecideLane"/>), and the
/// choice (<see cref="ChooseDestination"/> → <see cref="FundableDestination"/>) takes the first goal sink, in the existing
/// priority order, the lane can fund now; else the cheapest available technology; else nothing (the lane funds nothing —
/// the empty-lane rule's case). It re-decides ONLY while the held destination is unfundable, so a fundable destination is
/// never switched (no churn), and it never forces a return to the gated goal: once the gate opens, the next decision
/// (when the lane's destination completes) finds the gated goal fundable and takes it by the existing order. RP never
/// pays a civic; a player's research is never chosen. Off: the rule above exactly.
/// </summary>
public static class AiResearchPolicy
{
    /// <summary>
    /// The orders the AI issues for <paramref name="polity"/> this turn — ADR-043: in the two-lane model up to two, the
    /// Technology lane's first, then the Civics lane's; in the one-pool model at most one (<see cref="Decide"/>).
    /// </summary>
    public static OrderRecord[] DecideAll(IReadOnlyWorldState world, SimConfig cfg, PolityId polity)
    {
        if (cfg.Research is not { } content) return [];
        if (!content.Tuning.TwoLanes) return Decide(world, cfg, polity) is { } one ? [one] : [];
        var orders = new List<OrderRecord>(2);
        if (DecideLane(world, cfg, polity, ResearchTree.Technology) is { } tech) orders.Add(tech);
        if (DecideLane(world, cfg, polity, ResearchTree.Civics) is { } civic) orders.Add(civic);
        return [.. orders];
    }

    /// <summary>The order the AI issues for <paramref name="polity"/> this turn, or null — in the one-pool model its one
    /// order; in the two-lane model (ADR-043) the TECHNOLOGY lane's (<see cref="DecideLane"/>).</summary>
    public static OrderRecord? Decide(IReadOnlyWorldState world, SimConfig cfg, PolityId polity)
    {
        if (cfg.Research is not { } content) return null;
        if (content.Tuning.TwoLanes) return DecideLane(world, cfg, polity, ResearchTree.Technology);
        if (!EmpireQuery.TryGetCommandSource(world, polity, out CommandSource source) || source != CommandSource.Ai) return null;
        bool[] completed = ResearchQuery.CompletedMask(world, content, polity);
        bool[] available = ResearchQuery.AvailableMask(content, completed, ResearchQuery.LevelMask(world, content, polity, completed));
        // A live target is kept: switching would only scatter progress (it is retained, never lost). ADR-048: with routing
        // the row is a DESTINATION, live while its node is incomplete (available or not).
        bool routing = content.Tuning.DestinationRouting;
        if (ResearchQuery.TryGetTarget(world, polity, out ResearchNodeId current)
            && content.IndexOf(current) is int held and >= 0 && (routing ? ResearchQuery.IsOpenTarget(content, held, completed) : available[held])) return null;

        int choice = routing ? ChooseDestination(world, cfg, polity, completed, available, null)
            : Choose(world, cfg, polity, completed, available);
        return choice < 0 ? null : ResearchQuery.TargetOrder(world, content, polity, content.Nodes[choice].Key);
    }

    /// <summary>ADR-043: the order the AI issues on ONE lane (the lane's tree), or null — only when that lane holds no
    /// live target; the choice is <see cref="Choose(IReadOnlyWorldState, SimConfig, PolityId, bool[], bool[], ResearchTree)"/>.</summary>
    public static OrderRecord? DecideLane(IReadOnlyWorldState world, SimConfig cfg, PolityId polity, ResearchTree lane)
    {
        if (cfg.Research is not { } content) return null;
        if (!EmpireQuery.TryGetCommandSource(world, polity, out CommandSource source) || source != CommandSource.Ai) return null;
        bool[] completed = ResearchQuery.CompletedMask(world, content, polity);
        bool[] available = ResearchQuery.AvailableMask(content, completed, ResearchQuery.LevelMask(world, content, polity, completed));
        bool routing = content.Tuning.DestinationRouting;
        if (ResearchQuery.TryGetTarget(world, content, polity, lane, out ResearchNodeId current)
            && content.IndexOf(current) is int held and >= 0 && (routing ? ResearchQuery.IsOpenTarget(content, held, completed) : available[held]))
        {
            // ADR-052 (tuning.aiGateAwareGoals): a held technology destination the lane cannot fund now is not live — the AI
            // re-decides (its progress stays on its nodes). Off, or fundable: kept, as before.
            if (!GateAware(content, lane) || TechnologyFundable(world, content, polity, completed, available, held)) return null;
            int other = ChooseDestination(world, cfg, polity, completed, available, lane);
            return other < 0 || other == held ? null : ResearchQuery.TargetOrder(world, content, polity, content.Nodes[other].Key);
        }

        int choice = routing ? ChooseDestination(world, cfg, polity, completed, available, lane)
            : Choose(world, cfg, polity, completed, available, lane);
        return choice < 0 ? null : ResearchQuery.TargetOrder(world, content, polity, content.Nodes[choice].Key);
    }

    /// <summary>ADR-052: whether the gate-aware rule acts on <paramref name="lane"/> — research.json tuning.aiGateAwareGoals,
    /// with the two-lane model and destination routing, on the TECHNOLOGY lane only.</summary>
    private static bool GateAware(ResearchContent content, ResearchTree? lane) =>
        content.Tuning.AiGateAwareGoals && content.Tuning.TwoLanes && content.Tuning.DestinationRouting
        && lane == ResearchTree.Technology;

    /// <summary>
    /// ADR-052: whether the technology lane, holding <paramref name="destination"/>, funds a node this step — the engine's
    /// own rule: <see cref="ResearchRoutePlanner.Fund"/> over the polity's completed mask, available mask and remaining
    /// costs, with the Civics lane's destination as it stands (<see cref="ResearchQuery.Destinations"/>), exactly as
    /// ResearchSystem and <see cref="ResearchQuery.Route"/> compute it. True when the lane's CURRENT node is set (its
    /// destination, a route node, or a free-lane assist); false when it would wait.
    /// </summary>
    public static bool TechnologyFundable(IReadOnlyWorldState world, ResearchContent content, PolityId polity, bool[] completed,
        bool[] available, int destination)
    {
        if (available[destination]) return true;   // Fund: an available destination is funded itself
        int[] destinations = ResearchQuery.Destinations(world, content, polity, completed);
        destinations[0] = destination;
        double[] remaining = ResearchQuery.RemainingCosts(world, content, polity);
        return ResearchRoutePlanner.Fund(content, completed, available, remaining, destinations, twoLanes: true)[0].Current >= 0;
    }

    /// <summary>
    /// ADR-048 (tuning.destinationRouting) — the AI's DESTINATION, the same goal choice as <see cref="Choose(IReadOnlyWorldState, SimConfig, PolityId, bool[], bool[])"/>
    /// (on <paramref name="lane"/>'s tree when given): the goal worked now is <see cref="ChooseGoal"/>'s, and its GOAL NODE —
    /// <see cref="GoalDestination"/> — becomes the destination, so the route planner (the player's, ResearchRoutePlanner)
    /// chooses what to fund on the way. With no goal (or a goal without an incomplete node), the cheapest available node of
    /// the lane, as before. -1 when nothing is left.
    /// </summary>
    public static int ChooseDestination(IReadOnlyWorldState world, SimConfig cfg, PolityId polity, bool[] completed, bool[] available,
        ResearchTree? lane)
    {
        ResearchContent content = cfg.Research ?? throw new ArgumentException("the AI research policy needs research content", nameof(cfg));
        bool[]? tree = null;
        bool[][] goals = Goals(world, cfg, polity);
        if (lane is { } l)
        {
            tree = new bool[content.Nodes.Count];
            for (int i = 0; i < tree.Length; i++) tree[i] = content.Nodes[i].Tree == l;
            for (int g = 0; g < goals.Length; g++)
                for (int i = 0; i < tree.Length; i++) goals[g][i] &= tree[i];
        }
        if (GateAware(content, lane)) return FundableDestination(world, content, polity, goals, completed, available, tree);
        int goal = ChooseGoal(world, content, polity, goals, completed, available);
        if (goal >= 0 && GoalDestination(world, content, polity, goals[goal], completed, available) is int d and >= 0) return d;
        return Cheapest(world, content, polity, available, tree);
    }

    /// <summary>
    /// ADR-052 (tuning.aiGateAwareGoals) — THE TECHNOLOGY LANE'S GATE-AWARE DESTINATION. The goals in the existing priority
    /// order — the goals <see cref="ChooseGoal"/> admits (a closure node AVAILABLE), by its composite key (remaining closure
    /// cost, ordinal) — and within each goal its SINKS in <see cref="GoalDestination"/>'s order (route cost, key); the first
    /// sink the lane can fund now (<see cref="TechnologyFundable"/>) is the destination. So when the existing choice is
    /// fundable it is the answer, unchanged. Else the cheapest AVAILABLE node of the lane's tree (<see cref="Cheapest"/> —
    /// fundable by definition), else -1: the lane holds no new destination and funds nothing (the empty-lane rule's case).
    /// <paramref name="goals"/> and <paramref name="tree"/> are already restricted to the Technology tree, so a civic is
    /// never chosen.
    /// </summary>
    private static int FundableDestination(IReadOnlyWorldState world, ResearchContent content, PolityId polity, bool[][] goals,
        bool[] completed, bool[] available, bool[]? tree)
    {
        var goalCost = new double[goals.Length];
        var eligible = new bool[goals.Length];
        for (int g = 0; g < goals.Length; g++)   // ChooseGoal's key, computed the same way
        {
            bool[] closure = goals[g];
            for (int i = 0; i < closure.Length; i++)
            {
                if (!closure[i] || completed[i]) continue;
                goalCost[g] += ResearchQuery.EffectiveCost(world, content, polity, i);
                eligible[g] |= available[i];
            }
        }
        double[]? remaining = null;
        foreach (int g in OrderByCost(goalCost, eligible))
        {
            remaining ??= ResearchQuery.RemainingCosts(world, content, polity);
            bool[] closure = goals[g];
            var sink = new bool[closure.Length];
            var sinkCost = new double[closure.Length];
            for (int i = 0; i < closure.Length; i++)   // GoalDestination's sinks and costs
            {
                if (!closure[i] || completed[i]) continue;
                bool isSink = true;
                foreach (int dep in content.Nodes[i].Dependents)
                    if (closure[dep] && !completed[dep]) { isSink = false; break; }
                if (!isSink) continue;
                sink[i] = true;
                sinkCost[i] = available[i] ? remaining[i] : ResearchRoutePlanner.Plan(content, completed, remaining, i).RemainingCost;
            }
            foreach (int d in OrderByCost(sinkCost, sink))
                if (TechnologyFundable(world, content, polity, completed, available, d)) return d;
        }
        return Cheapest(world, content, polity, available, tree);
    }

    /// <summary>
    /// ADR-052: the included indices ordered by the composite key (cost, index) — ascending cost, bit-equal costs to the
    /// lower index. A stable insertion over an ascending-index scan that moves an entry only past a STRICTLY higher cost, so
    /// its first element is exactly the argmin the ascending scans above take (strictly-lower replacement). Pure; no LINQ.
    /// </summary>
    public static int[] OrderByCost(double[] cost, bool[] include)
    {
        var order = new List<int>();
        for (int i = 0; i < cost.Length; i++)
        {
            if (!include[i]) continue;
            int at = order.Count;
            while (at > 0 && cost[i] < cost[order[at - 1]]) at--;
            order.Insert(at, i);
        }
        return [.. order];
    }

    /// <summary>
    /// ADR-048: a goal closure's GOAL NODE — among its SINKS (incomplete closure nodes no other incomplete closure node
    /// depends on: the milestone, requirement or entity nodes the closure exists for), the one whose route is cheapest:
    /// composite key (remaining route cost incl. the node — ResearchRoutePlanner, the player's planner — , key), an
    /// ascending-key scan replacing only on a strictly lower cost. -1 when the closure has no incomplete node.
    /// </summary>
    public static int GoalDestination(IReadOnlyWorldState world, ResearchContent content, PolityId polity, bool[] closure,
        bool[] completed, bool[] available)
    {
        double[] remaining = ResearchQuery.RemainingCosts(world, content, polity);
        int best = -1;
        double bestCost = 0.0;
        for (int i = 0; i < closure.Length; i++)
        {
            if (!closure[i] || completed[i]) continue;
            bool sink = true;
            foreach (int dep in content.Nodes[i].Dependents)
                if (closure[dep] && !completed[dep]) { sink = false; break; }
            if (!sink) continue;
            double cost = available[i] ? remaining[i] : ResearchRoutePlanner.Plan(content, completed, remaining, i).RemainingCost;
            if (best < 0 || cost < bestCost) { best = i; bestCost = cost; }
        }
        return best;
    }

    /// <summary>
    /// ADR-043: the node the rule picks on ONE lane (dense index), or -1 — <see cref="Choose(IReadOnlyWorldState, SimConfig, PolityId, bool[], bool[])"/>
    /// with every goal closure intersected with the lane's tree and the fallback restricted to it. The two argmins keep
    /// their composite keys (remaining closure cost, goal ordinal) and (EffectiveCost, node key).
    /// </summary>
    public static int Choose(IReadOnlyWorldState world, SimConfig cfg, PolityId polity, bool[] completed, bool[] available, ResearchTree lane)
    {
        ResearchContent content = cfg.Research ?? throw new ArgumentException("the AI research policy needs research content", nameof(cfg));
        var tree = new bool[content.Nodes.Count];
        for (int i = 0; i < tree.Length; i++) tree[i] = content.Nodes[i].Tree == lane;
        bool[][] goals = Goals(world, cfg, polity);
        for (int g = 0; g < goals.Length; g++)
            for (int i = 0; i < tree.Length; i++) goals[g][i] &= tree[i];
        int goal = ChooseGoal(world, content, polity, goals, completed, available);
        int best = goal >= 0 ? Cheapest(world, content, polity, available, goals[goal]) : -1;
        return best >= 0 ? best : Cheapest(world, content, polity, available, tree);
    }

    /// <summary>The goal ordinals (the first component of the goal composite key).</summary>
    public const int CoreGoal = 0, RoadGoal = 1, TaxGoal = 2, UniversityGoal = 3, SupportingGoal = 4, GoalCount = 5;

    /// <summary>The node the rule picks (dense index), or -1 when nothing is available.</summary>
    public static int Choose(IReadOnlyWorldState world, SimConfig cfg, PolityId polity, bool[] completed, bool[] available)
    {
        ResearchContent content = cfg.Research ?? throw new ArgumentException("the AI research policy needs research content", nameof(cfg));
        bool[][] goals = Goals(world, cfg, polity);
        int goal = ChooseGoal(world, content, polity, goals, completed, available);
        int best = goal >= 0 ? Cheapest(world, content, polity, available, goals[goal]) : -1;
        return best >= 0 ? best : Cheapest(world, content, polity, available, null);
    }

    /// <summary>
    /// The goal worked now: among goals whose closure holds an AVAILABLE node, the lowest composite key
    /// (remaining closure cost, ordinal) — remaining cost = Σ EffectiveCost over the closure's uncompleted nodes,
    /// summed in ascending index order. Ordinal-order scan replacing only on a strictly lower cost, so bit-equal
    /// costs go to the lower ordinal. -1 when no goal has an available node.
    /// </summary>
    public static int ChooseGoal(IReadOnlyWorldState world, ResearchContent content, PolityId polity,
        bool[][] goals, bool[] completed, bool[] available)
    {
        int best = -1;
        double bestCost = 0.0;
        for (int g = 0; g < goals.Length; g++)
        {
            bool[] closure = goals[g];
            bool reachable = false;
            double remaining = 0.0;
            for (int i = 0; i < closure.Length; i++)
            {
                if (!closure[i] || completed[i]) continue;
                remaining += ResearchQuery.EffectiveCost(world, content, polity, i);
                reachable |= available[i];
            }
            if (!reachable) continue;
            if (best < 0 || remaining < bestCost) { best = g; bestCost = remaining; }
        }
        return best;
    }

    /// <summary>The goal closures in ordinal order (<see cref="CoreGoal"/> … <see cref="SupportingGoal"/>); a met
    /// or unconfigured goal is all false.</summary>
    public static bool[][] Goals(IReadOnlyWorldState world, SimConfig cfg, PolityId polity)
    {
        ResearchContent content = cfg.Research ?? throw new ArgumentException("the AI research policy needs research content", nameof(cfg));
        bool[][] capability = CapabilityGoals(world, cfg, polity);
        return [CoreGoalClosure(world, content, cfg.Ages, polity), capability[0], capability[1], capability[2],
            SupportingGoalClosure(world, content, cfg.Ages, polity)];
    }

    /// <summary>THE UNION of every goal (a reader; the choice works one goal at a time, see the header): <see cref="CoreGoalClosure"/> ∪ <see cref="CapabilityGoalClosure"/> ∪ <see cref="SupportingGoalClosure"/>.</summary>
    public static bool[] GoalClosure(IReadOnlyWorldState world, SimConfig cfg, PolityId polity)
    {
        ResearchContent content = cfg.Research ?? throw new ArgumentException("the AI research policy needs research content", nameof(cfg));
        bool[] goal = CoreGoalClosure(world, content, cfg.Ages, polity);
        bool[] capability = CapabilityGoalClosure(world, cfg, polity);
        bool[] supporting = SupportingGoalClosure(world, content, cfg.Ages, polity);
        for (int i = 0; i < goal.Length; i++) goal[i] |= capability[i] | supporting[i];
        return goal;
    }

    /// <summary>
    /// The prerequisite-ancestors (each node included; union over OR) of the knowledge requirements of the
    /// capability-gated actions the AI itself uses, while each is unmet: the next unknown road class, the tax
    /// edict, and the university founding (see the header). All false when every one is already met.
    /// </summary>
    public static bool[] CapabilityGoalClosure(IReadOnlyWorldState world, SimConfig cfg, PolityId polity)
    {
        bool[][] goals = CapabilityGoals(world, cfg, polity);
        bool[] union = goals[0];
        for (int g = 1; g < goals.Length; g++)
            for (int i = 0; i < union.Length; i++) union[i] |= goals[g][i];
        return union;
    }

    /// <summary>The three capability goal closures, separately: [road, tax, university].</summary>
    public static bool[][] CapabilityGoals(IReadOnlyWorldState world, SimConfig cfg, PolityId polity)
    {
        ResearchContent content = cfg.Research ?? throw new ArgumentException("the AI research policy needs research content", nameof(cfg));
        var expanded = new bool[content.Entities.Count];

        // 1. The NEXT unknown road class: the lowest class above the best the polity knows (RoadDevelopmentPolicy).
        var road = new Stack<int>();
        int roadEntity = -1;
        if (cfg.Roads is { } roads)
        {
            int best = RoadDevelopmentQuery.BestKnownClass(world, content, roads, polity);
            RoadClassConfig? next = null;
            foreach (RoadClassConfig c in roads.Classes)
                if (c.Entity is not null && c.EdgeType > best && (next is null || c.EdgeType < next.EdgeType)) next = c;
            if (next?.Entity is { } entity)
            {
                roadEntity = content.EntityIndexOf(entity);
                PushEntity(content, roadEntity, road, expanded);
            }
        }

        // 2. The tax edict's research gate while it is unmet (AiGovernance levies through it). H2: only the KNOWLEDGE
        // half — the Age half (governance.taxationMinAge) is not researchable, so a polity that knows Taxation and
        // waits for its Age has no tax research goal (else it would chase the requirement's other OR-branches).
        var tax = new Stack<int>();
        Predicate? taxRequirement = null;
        if (cfg.Governance is { } governance && !global::Sim.Core.State.Governance.KnowsTaxation(world, cfg, polity))
        {
            taxRequirement = ResearchContentLoader.ParseRequirement(
                content, governance.TaxationRequires, "sim.json governance.taxationRequires");
            foreach (int atom in taxRequirement.AtomIds)
                if (atom >= 0 && atom < content.Nodes.Count) tax.Push(atom);
        }

        // 3. The university requirement while unmet (AiConstructionPolicy founds universities, ADR-033 D6):
        // each founding project's building entity and the institution it founds.
        var university = new Stack<int>();
        var universityEntities = new List<int>();
        Array.Clear(expanded);
        if (cfg.Institutions is not null && cfg.Goods?.Projects is { } projects)
        {
            foreach (ConstructionProjectEntry project in projects)
            {
                if (project.Founds is not { } founds) continue;
                if (ConstructionQuery.IsKnowledgeEligible(world, content, polity, project)) continue;
                if (project.Entity is { } building)
                {
                    universityEntities.Add(content.EntityIndexOf(building));
                    PushEntity(content, content.EntityIndexOf(building), university, expanded);
                }
                universityEntities.Add(content.EntityIndexOf(founds.Entity));
                PushEntity(content, content.EntityIndexOf(founds.Entity), university, expanded);
            }
        }

        if (!content.Tuning.AiCheapestOrRoute)
            return [Ancestors(content, road), Ancestors(content, tax), Ancestors(content, university)];

        // ADR-046: the cheapest remaining route through each requirement instead of the union over OR. Every goal
        // keeps its roots (an entity's whole requirement, the edict's requirement, every university entity: AND).
        var routes = new Routes(world, content, polity);
        bool[] roadGoal = new bool[content.Nodes.Count], taxGoal = new bool[content.Nodes.Count],
            universityGoal = new bool[content.Nodes.Count];
        if (roadEntity >= 0) routes.Mark(routes.Entity(roadEntity), roadGoal);
        if (taxRequirement is not null) routes.Mark(routes.Expr(taxRequirement), taxGoal);
        int[] all = Routes.Empty;
        foreach (int e in universityEntities)
            if (e >= 0) all = Routes.And(all, routes.Entity(e));
        routes.Mark(all, universityGoal);
        return [roadGoal, taxGoal, universityGoal];
    }

    /// <summary>
    /// ADR-046 (M6 review, 2026-10-23) — THE CHEAPEST REMAINING ROUTE through a requirement, the replacement for the
    /// union-over-OR closure (switch: research.json tuning.aiCheapestOrRoute). A route is the ascending, distinct set of
    /// UNCOMPLETED nodes a polity must still research to satisfy an expression; its cost is Σ EffectiveCost over that set,
    /// summed in ascending index order:
    /// <list type="bullet">
    /// <item>a completed node: the empty route (so an alternative already satisfied costs 0 and stops every other
    ///   alternative of its OR from being pursued); an uncompleted node: itself plus the route of its prerequisite;</item>
    /// <item>AND: the union of both sides (shared ancestors counted once) — every required side is kept;</item>
    /// <item>OR: the side with the lower route cost; bit-equal costs go to the side WRITTEN FIRST (left), a fixed walk of
    ///   the parsed expression — composite key (route cost, source position);</item>
    /// <item>an institution atom: the route of that institution's own requirement (the evaluator's reading); a declared
    ///   unresolved name, which reads false forever: NO route (an OR takes the other side; an AND has no route, and a
    ///   goal without a route is empty — nothing research can do satisfies it);</item>
    /// <item>a comparison or a negation: the empty route (nothing to research toward).</item>
    /// </list>
    /// Routes are memoized per node for one decision; nested AND/OR and partly completed trees fall out of the recursion.
    /// </summary>
    public sealed class Routes
    {
        /// <summary>The empty route.</summary>
        public static readonly int[] Empty = [];
        /// <summary>The "no route" sentinel (compared by reference).</summary>
        public static readonly int[] None = [-1];

        private readonly IReadOnlyWorldState _world;
        private readonly ResearchContent _content;
        private readonly PolityId _polity;
        private readonly bool[] _completed;
        private readonly int[]?[] _node;
        private readonly int[]?[] _entity;
        private readonly bool[] _entityOpen;

        public Routes(IReadOnlyWorldState world, ResearchContent content, PolityId polity)
        {
            _world = world;
            _content = content;
            _polity = polity;
            _completed = ResearchQuery.CompletedMask(world, content, polity);
            _node = new int[]?[content.Nodes.Count];
            _entity = new int[]?[content.Entities.Count];
            _entityOpen = new bool[content.Entities.Count];
        }

        /// <summary>The route that completes node <paramref name="i"/> (empty when it is complete).</summary>
        public int[] Node(int i)
        {
            if (_completed[i]) return Empty;
            if (_node[i] is { } memo) return memo;
            Predicate? pre = _content.Nodes[i].Prerequisite;
            int[] before = pre is null ? Empty : Expr(pre);
            int[] route = ReferenceEquals(before, None) ? None : Union(before, [i]);
            _node[i] = route;
            return route;
        }

        /// <summary>The route that satisfies entity <paramref name="e"/>'s knowledge requirement.</summary>
        public int[] Entity(int e)
        {
            if (_entity[e] is { } memo) return memo;
            if (_entityOpen[e]) return Empty;   // an institution cycle contributes nothing (the loader forbids self-reference)
            _entityOpen[e] = true;
            int[] route = _content.Entities[e].Requirement is { } req ? Expr(req) : Empty;
            _entityOpen[e] = false;
            _entity[e] = route;
            return route;
        }

        /// <summary>The route that satisfies a research-dialect expression.</summary>
        public int[] Expr(Predicate p) => p.Fold(Atom, And, Or, Empty);

        private int[] Atom(int a)
        {
            int nodes = _content.Nodes.Count;
            if (a >= 0 && a < nodes) return Node(a);
            if (a >= nodes && a < nodes + _content.Entities.Count) return Entity(a - nodes);
            return None;   // the declared-unresolved sentinel: reads false forever
        }

        /// <summary>AND: both sides, shared nodes once; no route when either side has none.</summary>
        public static int[] And(int[] l, int[] r) =>
            ReferenceEquals(l, None) || ReferenceEquals(r, None) ? None : Union(l, r);

        private int[] Or(int[] l, int[] r)
        {
            if (ReferenceEquals(l, None)) return r;
            if (ReferenceEquals(r, None)) return l;
            return Cost(r) < Cost(l) ? r : l;   // strictly lower replaces: bit-equal costs keep the side written first
        }

        /// <summary>Σ EffectiveCost over the route, in ascending index order.</summary>
        public double Cost(int[] route)
        {
            double sum = 0.0;
            if (ReferenceEquals(route, None)) return double.PositiveInfinity;
            for (int k = 0; k < route.Length; k++) sum += ResearchQuery.EffectiveCost(_world, _content, _polity, route[k]);
            return sum;
        }

        /// <summary>Marks the route's nodes in <paramref name="goal"/> (nothing for no route).</summary>
        public void Mark(int[] route, bool[] goal)
        {
            if (ReferenceEquals(route, None)) return;
            foreach (int i in route) goal[i] = true;
        }

        private static int[] Union(int[] a, int[] b)
        {
            if (a.Length == 0) return b;
            if (b.Length == 0) return a;
            var merged = new int[a.Length + b.Length];
            int i = 0, j = 0, n = 0;
            while (i < a.Length || j < b.Length)
            {
                int next = j >= b.Length || (i < a.Length && a[i] <= b[j]) ? a[i] : b[j];
                if (i < a.Length && a[i] == next) i++;
                if (j < b.Length && b[j] == next) j++;
                merged[n++] = next;
            }
            return n == merged.Length ? merged : merged[..n];
        }
    }

    /// <summary>The prerequisite-ancestor closure (each node included) of the stacked nodes.</summary>
    private static bool[] Ancestors(ResearchContent content, Stack<int> stack)
    {
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
        if (content.Tuning.AiCheapestOrRoute)
        {
            // ADR-046: a research fact holds when ANY listed node is complete (AgeContent), so each unmet core
            // milestone is an OR over its nodes — the cheapest route; the milestones together are an AND.
            var routes = new Routes(world, content, polity);
            int[] all = Routes.Empty;
            foreach (AgeMilestone core in entry.Core)
            {
                if (core.Fact.Kind != MilestoneFactKind.Research || AgeQuery.Milestone(world, polity, core).Met) continue;
                if (MilestoneRoute(content, routes, core, out _) is { } cheapest) all = Routes.And(all, cheapest);
            }
            routes.Mark(all, goal);
            return goal;
        }
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

    /// <summary>
    /// ADR-046 §6 (M6 review batch 3, 2026-10-23) — THE SUPPORTING-MILESTONE GOAL (research.json
    /// tuning.aiSupportingMilestones, acting only with aiCheapestOrRoute). An Age is entered on its core milestones AND
    /// <c>supportingRequired</c> supporting milestones from <c>minCategories</c> categories (AgeQuery.Evaluate — the
    /// player's rule, read, never changed). The other goals never aimed at the supporting half; the union-over-OR closure
    /// met some of it by accident (Age IV's Alphabetic script, through the redundant scripts it researched), and the
    /// cheapest route no longer does. So, from the polity's NEXT Age's evaluation:
    /// <list type="number">
    /// <item>the deficits: count = supportingRequired − supportingMet, categories = minCategories − covered; none ⇒ empty;</item>
    /// <item>the candidates: the UNMET supporting milestones whose fact is research (ANY listed node, so each is the
    ///   cheapest route through its nodes, ties to the node listed first — as for the core goal); a milestone of another
    ///   kind (goods, structures, people, classes, formations) is not research work and is never a goal here;</item>
    /// <item>while categories are short: the cheapest candidate in a category not yet covered — composite key
    ///   (route cost, listed position) — counted toward both deficits; then, while the count is short, the cheapest
    ///   remaining candidate by the same key;</item>
    /// <item>the goal is the AND (union) of the chosen routes: every prerequisite of each chosen route is kept.</item>
    /// </list>
    /// Each candidate is costed alone (a node shared by two routes is counted in both when they are compared); only the
    /// deficit the CURRENT state leaves is pursued, re-read every decision, so a milestone met by other means (a
    /// workshop, a tool stock) shrinks it at once and nothing beyond the requirement is researched. No milestone or node id
    /// appears in code. All false without Age content, at the final Age, or with either switch off.
    /// </summary>
    public static bool[] SupportingGoalClosure(IReadOnlyWorldState world, ResearchContent content, AgeContent? ages, PolityId polity)
    {
        var goal = new bool[content.Nodes.Count];
        if (!content.Tuning.AiCheapestOrRoute || !content.Tuning.AiSupportingMilestones || ages is null) return goal;
        if (AgeQuery.Evaluate(world, ages, polity) is not { } report) return goal;
        int needCount = report.SupportingRequired - report.SupportingMet;
        int needCategories = report.CategoriesRequired - report.CategoriesCovered.Count;
        if (needCount <= 0 && needCategories <= 0) return goal;

        var routes = new Routes(world, content, polity);
        int n = report.Supporting.Count;
        var route = new int[]?[n];
        var cost = new double[n];
        for (int k = 0; k < n; k++)
        {
            MilestoneStatus s = report.Supporting[k];
            if (s.Met || s.Milestone.Fact.Kind != MilestoneFactKind.Research) continue;
            if (MilestoneRoute(content, routes, s.Milestone, out double c) is { } r) { route[k] = r; cost[k] = c; }
        }

        var categories = new AgeMilestoneCategory[n];
        for (int k = 0; k < n; k++) categories[k] = report.Supporting[k].Milestone.Category;
        int[] all = Routes.Empty;
        foreach (int k in SelectSupporting(categories, route, cost, report.CategoryMask, needCount, needCategories))
            all = Routes.And(all, route[k]!);
        routes.Mark(all, goal);
        return goal;
    }

    /// <summary>
    /// The supporting milestones chosen (listed positions, in choice order) — <see cref="SupportingGoalClosure"/>'s rule as a
    /// pure function: candidates are the positions with a route (<paramref name="route"/>[k] non-null, costed
    /// <paramref name="cost"/>[k]); while <paramref name="needCategories"/> &gt; 0, the cheapest candidate whose category is
    /// outside <paramref name="mask"/> (its bit then added; it counts toward both deficits); then, while
    /// <paramref name="needCount"/> &gt; 0, the cheapest remaining candidate. Each pick is the composite key (cost, listed
    /// position): an ascending scan replacing only on a strictly lower cost.
    /// </summary>
    public static List<int> SelectSupporting(IReadOnlyList<AgeMilestoneCategory> categories, int[]?[] route, double[] cost,
        int mask, int needCount, int needCategories)
    {
        var taken = new bool[route.Length];
        var chosen = new List<int>();
        while (needCategories > 0 && Pick(route, cost, categories, taken, mask, uncoveredOnly: true) is int k and >= 0)
        {
            taken[k] = true;
            chosen.Add(k);
            mask |= CategoryBit(categories[k]);
            needCategories--;
            needCount--;
        }
        while (needCount > 0 && Pick(route, cost, categories, taken, mask, uncoveredOnly: false) is int k and >= 0)
        {
            taken[k] = true;
            chosen.Add(k);
            needCount--;
        }
        return chosen;
    }

    /// <summary>The untaken candidate with the lowest composite key (route cost, listed position); with
    /// <paramref name="uncoveredOnly"/>, only candidates whose category is outside <paramref name="mask"/>. -1 when none.</summary>
    private static int Pick(int[]?[] route, double[] cost, IReadOnlyList<AgeMilestoneCategory> categories, bool[] taken, int mask,
        bool uncoveredOnly)
    {
        int best = -1;
        for (int k = 0; k < route.Length; k++)
        {
            if (route[k] is null || taken[k]) continue;
            if (uncoveredOnly && (mask & CategoryBit(categories[k])) != 0) continue;
            if (best < 0 || cost[k] < cost[best]) best = k;
        }
        return best;
    }

    private static int CategoryBit(AgeMilestoneCategory category) => 1 << ((int)category - 1);

    /// <summary>A research milestone's cheapest remaining route — ANY listed node satisfies it, so the cheapest node route,
    /// ties to the node listed first; null when no listed node has a route (unknown ids, or no route).</summary>
    private static int[]? MilestoneRoute(ResearchContent content, Routes routes, AgeMilestone milestone, out double cost)
    {
        int[]? cheapest = null;
        cost = 0.0;
        foreach (int key in milestone.Fact.NodeKeys)   // listed order: bit-equal costs keep the node listed first
        {
            int index = content.IndexOf(new ResearchNodeId(key));
            if (index < 0) continue;
            int[] route = routes.Node(index);
            if (ReferenceEquals(route, Routes.None)) continue;
            double c = routes.Cost(route);
            if (cheapest is null || c < cost) { cheapest = route; cost = c; }
        }
        return cheapest;
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
