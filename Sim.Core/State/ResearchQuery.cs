using Sim.Core.Systems.ClassMobility;
using Sim.Core.Systems.Research;

namespace Sim.Core.State;

/// <summary>
/// ADR-029 §12 — THE research read seam (D-044 R17). These are pure, read-only
/// queries over <see cref="IReadOnlyWorldState"/> and the loaded
/// <see cref="ResearchContent"/>. <b>ResearchSystem computes with these same
/// statics</b>: availability, subtree opening, effective cost, Research Points and
/// the acceleration-credit pool are each defined ONCE, here. So every Glass Box value is
/// RECOMPUTED by the function the simulation itself calls, never by a private
/// re-implementation (observability-architecture §0; the FoodState precedent).
///
/// Nothing here mutates state, draws RNG or reads a clock. Iteration is over
/// table indices and content indices in order — never a dictionary or a set —
/// and there is no LINQ (law 5). Every list returned is in ascending node-key
/// order unless it says otherwise, so enumeration is deterministic (D-044 R19).
/// The UI reads through here and never writes. Selecting a target goes through
/// the SetResearchTarget order (D-044 R17).
/// </summary>
public static class ResearchQuery
{
    // ------------------------------------------------------------------ knowledge

    /// <summary>The polity's completed-knowledge set as a mask over content node
    /// indices (D-044 R1: the knowledge base IS the completed nodes). A row whose key
    /// is unknown to this content is ignored; that happens only when content changed
    /// under a save (the ConstructionSystem precedent).</summary>
    public static bool[] CompletedMask(IReadOnlyWorldState world, ResearchContent content, PolityId polity)
    {
        var mask = new bool[content.Nodes.Count];
        for (int i = 0; i < world.ResearchCompleted.Count; i++)
        {
            ResearchCompletedRow row = world.ResearchCompleted[i];
            if (row.Polity.Value != polity.Value) continue;
            int index = content.IndexOf(row.Node);
            if (index >= 0) mask[index] = true;
        }
        return mask;
    }

    public static bool IsCompleted(IReadOnlyWorldState world, PolityId polity, ResearchNodeId node)
    {
        for (int i = 0; i < world.ResearchCompleted.Count; i++)
        {
            ResearchCompletedRow row = world.ResearchCompleted[i];
            if (row.Polity.Value == polity.Value && row.Node.Value == node.Value) return true;
        }
        return false;
    }

    /// <summary>Completed nodes, key-ascending; optionally one tree only.</summary>
    public static ResearchNodeId[] CompletedNodes(
        IReadOnlyWorldState world, ResearchContent content, PolityId polity, ResearchTree? tree = null)
    {
        bool[] mask = CompletedMask(world, content, polity);
        var result = new List<ResearchNodeId>();
        for (int i = 0; i < mask.Length; i++)
            if (mask[i] && (tree is null || content.Nodes[i].Tree == tree)) result.Add(content.Nodes[i].Key);
        return [.. result];
    }

    /// <summary>DIFFERENCED: the nodes <paramref name="polity"/> completed in the step
    /// prev → next, in completion order. This is the observable completion event
    /// (D-044 R11 item 7); the system emits nothing else.</summary>
    public static ResearchNodeId[] CompletedBetween(IReadOnlyWorldState prev, IReadOnlyWorldState next, PolityId polity)
    {
        var result = new List<ResearchNodeId>();
        for (int i = 0; i < next.ResearchCompleted.Count; i++)
        {
            ResearchCompletedRow row = next.ResearchCompleted[i];
            if (row.Polity.Value == polity.Value && !IsCompleted(prev, polity, row.Node)) result.Add(row.Node);
        }
        return [.. result];
    }

    // ------------------------------------------------------------------ stage, subtrees, availability

    /// <summary>The university / research institutional stage (D-044 R4) over a completed mask.</summary>
    public static bool StageReached(ResearchContent content, bool[] completed) =>
        content.Stage.Evaluate(null, a => completed[a], null);

    public static bool IsStageReached(IReadOnlyWorldState world, ResearchContent content, PolityId polity) =>
        StageReached(content, CompletedMask(world, content, polity));

    /// <summary>All five subtrees open together when the stage is reached (D-044 R4);
    /// the Main tree and Civics are always open.</summary>
    public static bool IsBranchOpen(IReadOnlyWorldState world, ResearchContent content, PolityId polity, int branch)
    {
        if (branch < 0) return true;
        if (branch >= content.Branches.Count) throw new ArgumentOutOfRangeException(nameof(branch), branch, "no such subtree");
        return IsStageReached(world, content, polity);
    }

    public static bool PrerequisitesMet(ResearchContent content, int node, bool[] completed) =>
        content.Nodes[node].Prerequisite is not { } p || p.Evaluate(null, a => completed[a], null);

    /// <summary>AVAILABLE (= ELIGIBLE) = not completed, its subtree open, its prerequisite expression
    /// (AND / OR / nested) satisfied by completed knowledge (D-044 R8), — for a RECURSIVE
    /// node only — every finite node of its own subtree complete (finalization ruling 4:
    /// per-subtree exhaustion; repeat mechanics are deferred, so it still completes once), and — ADR-048, with
    /// tuning.ageSweep — the Age sweep (<see cref="SweepSatisfied"/>). This is THE eligibility predicate: the player's
    /// orders, the AI, city states, the UI and the route planner all read it (through <see cref="AvailableMask"/> or here).</summary>
    public static bool IsAvailable(ResearchContent content, int node, bool[] completed, bool stageReached) =>
        IsAvailable(content, node, completed, stageReached, null);

    /// <summary><see cref="IsAvailable(ResearchContent, int, bool[], bool)"/> with the Age sweep read from a precomputed
    /// <see cref="SweptAges"/> (null: computed for this node alone) — the same predicate, for callers that test many nodes.</summary>
    public static bool IsAvailable(ResearchContent content, int node, bool[] completed, bool stageReached, bool[]? sweptAges)
    {
        ResearchNode n = content.Nodes[node];
        if (completed[node] || (n.Branch >= 0 && !stageReached) || !PrerequisitesMet(content, node, completed)) return false;
        if (n.IsRecursive && !SubtreeExhausted(content, n.Branch, completed)) return false;
        return sweptAges is null ? SweepSatisfied(content, node, completed) : SweepOpen(content, node, sweptAges);
    }

    // ------------------------------------------------------------------ the Age sweep (ADR-048)

    /// <summary>ADR-048 (tranche-2 contract §2): whether a node is a member of its Age's SWEEP COHORT — every node except one
    /// carrying a repeatable descriptor (repeatables are exempt: their own Age's finite set already counts).</summary>
    public static bool IsSweepCohortMember(ResearchNode node) => !node.HasRepeatableDescriptor;

    /// <summary>
    /// ADR-048 — THE AGE SWEEP PREDICATE (tranche-2 contract §2; D-063 supersedes D-044 R13 for this predicate only). With
    /// research.json tuning.ageSweep, a node of content Age a ≥ 3 is eligible only if EVERY sweep-cohort node of Age a − 2,
    /// in BOTH trees, is complete. It reads the node's content Age label — never the polity's Age, never a date (law 4) —
    /// and it is kept apart from the authored prerequisites (<see cref="SweepBlock(ResearchContent, bool[], int)"/> reports
    /// it on its own; no prerequisite edge is generated). Ages 1 and 2 are exempt. Switch off: always true (the twin).
    /// </summary>
    public static bool SweepSatisfied(ResearchContent content, int node, bool[] completed)
    {
        if (!content.Tuning.AgeSweep) return true;
        int cohort = content.Nodes[node].AgeNumber - 2;
        if (cohort < 1) return true;
        for (int i = 0; i < content.Nodes.Count; i++)
        {
            ResearchNode c = content.Nodes[i];
            if (!completed[i] && c.AgeNumber == cohort && IsSweepCohortMember(c)) return false;
        }
        return true;
    }

    /// <summary>ADR-048: per content Age 1..9 (index 0 unused), whether every sweep-cohort node of that Age is complete —
    /// the sweep of many nodes at once (<see cref="IsAvailable(ResearchContent, int, bool[], bool, bool[])"/>). Equal to
    /// <see cref="SweepSatisfied"/> node by node.</summary>
    public static bool[] SweptAges(ResearchContent content, bool[] completed)
    {
        var swept = new bool[10];
        Array.Fill(swept, true);
        for (int i = 0; i < content.Nodes.Count; i++)
        {
            ResearchNode c = content.Nodes[i];
            if (!completed[i] && IsSweepCohortMember(c)) swept[c.AgeNumber] = false;
        }
        return swept;
    }

    private static bool SweepOpen(ResearchContent content, int node, bool[] sweptAges)
    {
        if (!content.Tuning.AgeSweep) return true;
        int cohort = content.Nodes[node].AgeNumber - 2;
        return cohort < 1 || sweptAges[cohort];
    }

    /// <summary>ADR-048: the Age sweep as it bears on one node — whether it applies (switch on and Age ≥ 3), whether it
    /// BLOCKS the node now, the cohort Age (a − 2) and the cohort nodes still outstanding (key-ascending; both trees).</summary>
    public sealed record SweepState(bool Applies, bool Blocked, int CohortAge, ResearchNodeId[] Outstanding);

    public static SweepState SweepBlock(ResearchContent content, bool[] completed, int node)
    {
        int cohort = content.Nodes[node].AgeNumber - 2;
        if (!content.Tuning.AgeSweep || cohort < 1) return new SweepState(false, false, Math.Max(0, cohort), []);
        var outstanding = new List<ResearchNodeId>();
        for (int i = 0; i < content.Nodes.Count; i++)   // ascending index = ascending key
        {
            ResearchNode c = content.Nodes[i];
            if (!completed[i] && c.AgeNumber == cohort && IsSweepCohortMember(c)) outstanding.Add(c.Key);
        }
        return new SweepState(true, outstanding.Count > 0, cohort, [.. outstanding]);
    }

    /// <summary>ADR-048: <see cref="SweepBlock(ResearchContent, bool[], int)"/> for a polity's completed knowledge.</summary>
    public static SweepState SweepBlock(IReadOnlyWorldState world, ResearchContent content, PolityId polity, ResearchNodeId node) =>
        SweepBlock(content, CompletedMask(world, content, polity), RequireIndex(content, node));

    /// <summary>Whether every FINITE node of the subtree is complete — what a recursive node
    /// of that subtree waits for.</summary>
    public static bool SubtreeExhausted(ResearchContent content, int branch, bool[] completed)
    {
        foreach (int i in content.FiniteNodesBySubtree[branch]) if (!completed[i]) return false;
        return true;
    }

    public static bool[] AvailableMask(ResearchContent content, bool[] completed)
    {
        bool stage = StageReached(content, completed);
        bool[]? swept = content.Tuning.AgeSweep ? SweptAges(content, completed) : null;
        var mask = new bool[completed.Length];
        for (int i = 0; i < mask.Length; i++) mask[i] = IsAvailable(content, i, completed, stage, swept);
        return mask;
    }

    /// <summary>Available nodes, key-ascending. <paramref name="tree"/> filters by tree;
    /// <paramref name="branch"/> filters to one subtree (-1 = the Main Technology Tree).</summary>
    public static ResearchNodeId[] AvailableNodes(
        IReadOnlyWorldState world, ResearchContent content, PolityId polity,
        ResearchTree? tree = null, int? branch = null)
    {
        bool[] available = AvailableMask(content, CompletedMask(world, content, polity));
        var result = new List<ResearchNodeId>();
        for (int i = 0; i < available.Length; i++)
        {
            ResearchNode n = content.Nodes[i];
            if (!available[i]) continue;
            if (tree is not null && n.Tree != tree) continue;
            if (branch is not null && (n.Tree != ResearchTree.Technology || n.Branch != branch)) continue;
            result.Add(n.Key);
        }
        return [.. result];
    }

    /// <summary>
    /// The AVAILABLE node with the lowest EffectiveCost, ties broken by the LOWER KEY.
    /// This is the composite key (EffectiveCost, key) the house rule requires for any
    /// ordering over doubles. The scan runs in ascending key order and replaces the
    /// best only on a strictly lower cost, so among bit-equal costs the first key
    /// scanned wins. Null when nothing is available. A read-only suggestion for UIs
    /// and measurement drivers; the simulation never calls it, and no system picks
    /// targets (D-042 §6: targets are chosen by orders).
    /// </summary>
    public static ResearchNodeId? CheapestAvailable(
        IReadOnlyWorldState world, ResearchContent content, PolityId polity, ResearchTree? tree = null)
    {
        bool[] available = AvailableMask(content, CompletedMask(world, content, polity));
        int best = -1;
        double bestCost = 0.0;
        for (int i = 0; i < available.Length; i++)
        {
            if (!available[i] || (tree is not null && content.Nodes[i].Tree != tree)) continue;
            double cost = EffectiveCost(world, content, polity, i);
            if (best < 0 || cost < bestCost) { best = i; bestCost = cost; }
        }
        return best < 0 ? null : content.Nodes[best].Key;
    }

    /// <summary>Why a node is or is not researchable: its expression, each
    /// prerequisite node with its completion, and the subtree gate.</summary>
    public sealed record PrerequisiteState(
        string? Expression, PrerequisiteAtom[] Atoms, bool Satisfied, bool SubtreeOpen, bool Completed, bool Available);

    public readonly record struct PrerequisiteAtom(ResearchNodeId Node, string Id, bool Completed);

    public static PrerequisiteState Prerequisites(
        IReadOnlyWorldState world, ResearchContent content, PolityId polity, ResearchNodeId node)
    {
        int index = RequireIndex(content, node);
        bool[] completed = CompletedMask(world, content, polity);
        bool stage = StageReached(content, completed);
        ResearchNode n = content.Nodes[index];
        var atoms = new PrerequisiteAtom[n.PrerequisiteNodes.Count];
        for (int k = 0; k < atoms.Length; k++)
        {
            ResearchNode a = content.Nodes[n.PrerequisiteNodes[k]];
            atoms[k] = new PrerequisiteAtom(a.Key, a.Id, completed[a.Index]);
        }
        return new PrerequisiteState(
            n.Prerequisite?.Source, atoms, PrerequisitesMet(content, index, completed),
            n.Branch < 0 || stage, completed[index], IsAvailable(content, index, completed, stage));
    }

    // ------------------------------------------------------------------ target and progress

    /// <summary>
    /// THE research directive, built the same way for the player and the AI (ADR-033 D5; the
    /// AgeQuery.AdvanceOrder / RoadDevelopmentQuery.DevelopOrder precedent): the SetResearchTarget order
    /// that makes <paramref name="node"/> the issuer's one active target, stamped with the CURRENT turn (the
    /// step executing from this state already spends its RP on it), or null when the node is not AVAILABLE
    /// to the issuer now — refused by <see cref="IsAvailable(ResearchContent, int, bool[], bool)"/>, the availability
    /// ResearchSystem applies, so no caller ever logs an order the simulation would ignore.
    /// ADR-048: with tuning.destinationRouting the order names a DESTINATION and any INCOMPLETE node is accepted
    /// (<see cref="IsDestinationAdmissible"/>, the rule ResearchSystem applies in that mode).
    /// </summary>
    public static Kernel.OrderRecord? TargetOrder(
        IReadOnlyWorldState world, ResearchContent content, PolityId issuer, ResearchNodeId node)
    {
        int index = content.IndexOf(node);
        if (index < 0) return null;
        bool[] completed = CompletedMask(world, content, issuer);
        bool admissible = content.Tuning.DestinationRouting
            ? IsDestinationAdmissible(content, index, completed)
            : IsAvailable(content, index, completed, StageReached(content, completed));
        if (!admissible) return null;
        return Kernel.OrderRecord.From(world.Clock.Turn, issuer, Kernel.OrderKind.SetResearchTarget, node.Value, 0.0);
    }

    // ------------------------------------------------------------------ destination research (ADR-048)

    /// <summary>ADR-048 (tranche-2 contract §3): with tuning.destinationRouting, a SetResearchTarget order may name ANY
    /// INCOMPLETE node as the lane's destination, available or not. The one admission rule ResearchSystem, the order
    /// builder and the UI apply.</summary>
    public static bool IsDestinationAdmissible(ResearchContent content, int node, bool[] completed) =>
        node >= 0 && node < completed.Length && !completed[node];

    /// <summary>ADR-048: what each node still costs the polity — EffectiveCost minus the progress it holds, floored at 0
    /// (0 for a completed node). The route planner's cost vector.</summary>
    public static double[] RemainingCosts(IReadOnlyWorldState world, ResearchContent content, PolityId polity)
    {
        bool[] completed = CompletedMask(world, content, polity);
        var remaining = new double[content.Nodes.Count];
        for (int i = 0; i < remaining.Length; i++) remaining[i] = completed[i] ? 0.0 : EffectiveCost(world, content, polity, i);
        for (int r = 0; r < world.ResearchProgress.Count; r++)
        {
            ResearchProgressRow row = world.ResearchProgress[r];
            if (row.Polity.Value != polity.Value) continue;
            int index = content.IndexOf(row.Node);
            if (index >= 0 && !completed[index]) remaining[index] = Math.Max(0.0, remaining[index] - row.Progress);
        }
        return remaining;
    }

    /// <summary>
    /// ADR-048: the polity's DESTINATION per lane slot (0 = Technology, or the one target in the one-pool model; 1 =
    /// Civics, two-lane model only) as dense indices, -1 for none — read from the target rows exactly as ResearchSystem
    /// reads them with tuning.destinationRouting: in the one-pool model the polity's first row, in the two-lane model the
    /// first row on each lane; a row is a destination while its node is INCOMPLETE.
    /// </summary>
    public static int[] Destinations(IReadOnlyWorldState world, ResearchContent content, PolityId polity, bool[] completed)
    {
        var destination = new[] { -1, -1 };
        bool twoLanes = content.Tuning.TwoLanes;
        for (int r = 0; r < world.ResearchTargets.Count; r++)
        {
            ResearchTargetRow row = world.ResearchTargets[r];
            if (row.Polity.Value != polity.Value) continue;
            int index = content.IndexOf(row.Node);
            if (!twoLanes)
            {
                if (index >= 0 && !completed[index]) destination[0] = index;
                break;   // the one-pool model reads the first row only (TryGetTarget)
            }
            int lane = ResearchRoutePlanner.LaneOf(content, index < 0 ? 0 : index, twoLanes);
            if (index >= 0 && !completed[index] && destination[lane] < 0) destination[lane] = index;
        }
        return destination;
    }

    /// <summary>
    /// ADR-048 — THE ROUTE of one lane (tranche-2 contract §3), the ONLY route calculator: the UI, the AI and the Glass Box
    /// read it, and ResearchSystem funds exactly its <see cref="ResearchRoute.Current"/> (both call
    /// <see cref="ResearchRoutePlanner.Fund"/> over the same inputs). It reports the lane's destination, the CURRENT node the
    /// next step funds, the NEXT node after it, the ordered route, every blocker with its reason (prerequisite, sweep(Age k),
    /// stage, otherLane) and the remaining cost. A lane with no destination of its own reports the node it ASSISTS the other
    /// lane with (free-lane assist), if any. In the one-pool model the one destination is reported on its own tree's lane.
    /// </summary>
    public static ResearchRoute Route(IReadOnlyWorldState world, ResearchContent content, PolityId polity, ResearchTree lane)
    {
        bool[] completed = CompletedMask(world, content, polity);
        bool[] available = AvailableMask(content, completed);
        int[] destinations = Destinations(world, content, polity, completed);
        bool twoLanes = content.Tuning.TwoLanes;
        if (!twoLanes && destinations[0] >= 0 && content.Nodes[destinations[0]].Tree != lane) destinations[0] = -1;
        double[] remaining = RemainingCosts(world, content, polity);
        return ResearchRoutePlanner.Describe(content, completed, available, remaining, destinations, twoLanes, lane);
    }


    /// <summary>ADR-043: the SetResearchTarget TargetId that clears the TECHNOLOGY lane (in the one-pool model, the one
    /// target — the v34 meaning of −1).</summary>
    public const int ClearTechnologyTargetId = -1;

    /// <summary>ADR-043: the SetResearchTarget TargetId that clears the CIVICS lane (new in v35).</summary>
    public const int ClearCivicsTargetId = -2;

    /// <summary>The order that clears the issuer's Technology target — in the one-pool model its one target (progress
    /// everywhere is kept, D-044 R9).</summary>
    public static Kernel.OrderRecord ClearTargetOrder(IReadOnlyWorldState world, PolityId issuer) =>
        Kernel.OrderRecord.From(world.Clock.Turn, issuer, Kernel.OrderKind.SetResearchTarget, ClearTechnologyTargetId, 0.0);

    /// <summary>ADR-043: the order that clears the issuer's target on one LANE — −1 for Technology, −2 for Civics.</summary>
    public static Kernel.OrderRecord ClearTargetOrder(IReadOnlyWorldState world, PolityId issuer, ResearchTree lane) =>
        Kernel.OrderRecord.From(world.Clock.Turn, issuer, Kernel.OrderKind.SetResearchTarget,
            lane == ResearchTree.Civics ? ClearCivicsTargetId : ClearTechnologyTargetId, 0.0);

    /// <summary>The polity's FIRST target row, whichever lane (in the one-pool model, its one active target — D-044
    /// R9). Lane-aware readers use <see cref="TryGetTarget(IReadOnlyWorldState, ResearchContent, PolityId, ResearchTree, out ResearchNodeId)"/>.</summary>
    public static bool TryGetTarget(IReadOnlyWorldState world, PolityId polity, out ResearchNodeId node)
    {
        for (int i = 0; i < world.ResearchTargets.Count; i++)
        {
            ResearchTargetRow row = world.ResearchTargets[i];
            if (row.Polity.Value == polity.Value) { node = row.Node; return true; }
        }
        node = default;
        return false;
    }

    /// <summary>
    /// ADR-043 — the polity's target on one LANE (the node's tree): Technology targets are funded by Research Points,
    /// Civics targets by Civics Points. A row whose key this content does not know is skipped. In the one-pool model
    /// (<see cref="ResearchTuning.TwoLanes"/> off) a polity holds at most one row, reported on its node's lane.
    /// </summary>
    public static bool TryGetTarget(IReadOnlyWorldState world, ResearchContent content, PolityId polity, ResearchTree lane, out ResearchNodeId node)
    {
        for (int i = 0; i < world.ResearchTargets.Count; i++)
        {
            ResearchTargetRow row = world.ResearchTargets[i];
            if (row.Polity.Value != polity.Value) continue;
            int index = content.IndexOf(row.Node);
            if (index >= 0 && content.Nodes[index].Tree == lane) { node = row.Node; return true; }
        }
        node = default;
        return false;
    }

    /// <summary>ADR-043: whether the polity holds no target that a new order on <paramref name="lane"/> would leave
    /// untouched — in the two-lane model, no target on that lane; in the one-pool model, no target at all (a civic order
    /// displaced the tech target there).</summary>
    public static bool LaneIsFree(IReadOnlyWorldState world, ResearchContent content, PolityId polity, ResearchTree lane) =>
        content.Tuning.TwoLanes ? !TryGetTarget(world, content, polity, lane, out _) : !TryGetTarget(world, polity, out _);

    /// <summary>READ: RP invested in a node the polity has not completed (0 if none).
    /// A completed node has no progress row — completion is the fact.</summary>
    public static double Progress(IReadOnlyWorldState world, PolityId polity, ResearchNodeId node)
    {
        for (int i = 0; i < world.ResearchProgress.Count; i++)
        {
            ResearchProgressRow row = world.ResearchProgress[i];
            if (row.Polity.Value == polity.Value && row.Node.Value == node.Value) return row.Progress;
        }
        return 0.0;
    }

    /// <summary>Every node holding partial progress for the polity, key-ascending
    /// (D-044 R9: many nodes may retain progress).</summary>
    public static ResearchProgressRow[] PartialProgress(IReadOnlyWorldState world, PolityId polity)
    {
        var rows = new List<ResearchProgressRow>();
        for (int i = 0; i < world.ResearchProgress.Count; i++)
            if (world.ResearchProgress[i].Polity.Value == polity.Value) rows.Add(world.ResearchProgress[i]);
        ResearchProgressRow[] result = [.. rows];
        Array.Sort(result, (a, b) => a.Node.Value.CompareTo(b.Node.Value)); // keys are unique per polity
        return result;
    }

    // ------------------------------------------------------------------ cost

    /// <summary>One specialized-university term in a node's cost (ADR-029 §9).</summary>
    public readonly record struct CostTerm(int ModifierRow, int UniversityType, string UniversityId, int Branch, double Factor);

    /// <summary>BaseCost → relevant modifiers → EffectiveCost (D-044 R5), decomposed. <paramref name="Modified"/>
    /// is BaseCost × Π Factor; <paramref name="Floor"/> the effective-cost floor; EffectiveCost the larger.</summary>
    public sealed record CostBreakdown(
        ResearchNodeId Node, double BaseCost, CostTerm[] Terms, double Modified, double Floor, double EffectiveCost)
    {
        /// <summary>Whether the floor, not the modifiers, sets the effective cost.</summary>
        public bool FloorBinds => Floor > Modified;
    }

    /// <summary>
    /// EffectiveCost = max(effectiveCostFloorFraction × BaseCost, BaseCost × Π Factor) — the
    /// floor (0.20) is the finalization ruling: no modifier stack takes a node below 20 % of its
    /// base. The product runs over the polity's ResearchCostModifier rows whose university type
    /// serves the node's subtree, multiplied in TABLE ORDER (a fixed order, so the product is
    /// bit-reproducible). Main-tree and Civics nodes have no relevant university and cost BaseCost. The seam is the whole of the
    /// mechanism. The Factor values (maturity, viability, diminishing returns) are the
    /// future writer's, and no formula is invented here (D-044 R5). A malformed row
    /// — an unknown type, or a Factor outside (0, 1] — breaks the input contract and
    /// throws.
    /// </summary>
    public static double EffectiveCost(IReadOnlyWorldState world, ResearchContent content, PolityId polity, int node) =>
        Math.Max(content.Tuning.EffectiveCostFloorFraction * content.Nodes[node].BaseCost, Modified(world, content, polity, node, null));

    public static CostBreakdown EffectiveCostBreakdown(
        IReadOnlyWorldState world, ResearchContent content, PolityId polity, ResearchNodeId node)
    {
        int index = RequireIndex(content, node);
        var terms = new List<CostTerm>();
        double modified = Modified(world, content, polity, index, terms);
        double floor = content.Tuning.EffectiveCostFloorFraction * content.Nodes[index].BaseCost;
        return new CostBreakdown(node, content.Nodes[index].BaseCost, [.. terms], modified, floor, Math.Max(floor, modified));
    }

    /// <summary>The polity's specialized-university modifiers, table order — every
    /// row, whichever subtree it serves.</summary>
    public static CostTerm[] CostModifiers(IReadOnlyWorldState world, ResearchContent content, PolityId polity)
    {
        var terms = new List<CostTerm>();
        for (int i = 0; i < world.ResearchCostModifiers.Count; i++)
        {
            ResearchCostModifierRow row = world.ResearchCostModifiers[i];
            if (row.Polity.Value != polity.Value) continue;
            UniversityType type = ValidModifier(content, row, i);
            terms.Add(new CostTerm(i, type.Key, type.Id, type.Branch, row.Factor));
        }
        return [.. terms];
    }

    private static double Modified(IReadOnlyWorldState world, ResearchContent content, PolityId polity, int node, List<CostTerm>? terms)
    {
        ResearchNode n = content.Nodes[node];
        double cost = n.BaseCost;
        for (int i = 0; i < world.ResearchCostModifiers.Count; i++)
        {
            ResearchCostModifierRow row = world.ResearchCostModifiers[i];
            if (row.Polity.Value != polity.Value) continue;
            UniversityType type = ValidModifier(content, row, i);
            if (n.Tree != ResearchTree.Technology || type.Branch != n.Branch) continue;
            cost *= row.Factor;
            terms?.Add(new CostTerm(i, type.Key, type.Id, type.Branch, row.Factor));
        }
        return cost;
    }

    private static UniversityType ValidModifier(ResearchContent content, ResearchCostModifierRow row, int rowIndex)
    {
        int t = content.UniversityTypeIndexOf(row.UniversityType);
        if (t < 0)
            throw new InvalidOperationException(
                $"ResearchCostModifiers[{rowIndex}]: university type {row.UniversityType} is not in research.json — " +
                "the writer broke the ADR-029 §9 input contract.");
        if (!(double.IsFinite(row.Factor) && row.Factor > 0.0 && row.Factor <= 1.0))
            throw new InvalidOperationException(
                $"ResearchCostModifiers[{rowIndex}]: factor {row.Factor.ToString("R", System.Globalization.CultureInfo.InvariantCulture)} " +
                "is outside (0, 1] — a specialized university REDUCES effective cost (D-044 R5); the writer broke the input contract.");
        return content.UniversityTypes[t];
    }

    // ------------------------------------------------------------------ Research Points

    /// <summary>Adults (cohorts 3..11, every class) in the settlements the polity
    /// controls — each settlement once, credited to its lowest-id controller
    /// (EmpireQuery.TryGetController), so a doubled control row never double-counts.</summary>
    public static long Adults(IReadOnlyWorldState world, PolityId polity)
    {
        long adults = 0;
        checked
        {
            for (int s = 0; s < world.Settlements.Count; s++)
            {
                SettlementId id = world.Settlements[s].Id;
                if (SettlementKnowledge.HolderOf(world, id).Value == polity.Value)
                    adults += BandViews.Adults(world.Buckets, id);
            }
        }
        return adults;
    }

    /// <summary>Total population (every cohort and class) of the settlements the polity
    /// controls, each settlement once (lowest-id controller, as <see cref="Adults"/>). The
    /// military-population weight of the RP input is a calibration parameter that stays inert
    /// until soldier accounting exists (ADR-030 §6), so every person counts once.</summary>
    public static long Population(IReadOnlyWorldState world, PolityId polity)
    {
        long population = 0;
        checked
        {
            for (int s = 0; s < world.Settlements.Count; s++)
            {
                SettlementId id = world.Settlements[s].Id;
                if (SettlementKnowledge.HolderOf(world, id).Value != polity.Value) continue;
                for (int b = 0; b < world.Buckets.Count; b++)
                    if (world.Buckets[b].Settlement.Value == id.Value) population += world.Buckets[b].Count.Value;
            }
        }
        return population;
    }

    /// <summary>
    /// Research Points per strategic turn for a population: rpPerTurn.coefficient ×
    /// population^rpPerTurn.exponent (0.08 × P^0.699; anchors 100 → 2, 1,000 → 10). Sublinear by
    /// validation (exponent &lt; 1). Calibration values, not ratified constants; the PER-TURN
    /// reading is the ruling (ADR-030). Population is the only input — literacy, education,
    /// universities and the rest are FUTURE modifiers, not part of this base curve.
    /// </summary>
    public static double ResearchPoints(ResearchTuning tuning, double population) =>
        population <= 0.0 ? 0.0 : tuning.RpCoefficient * Math.Pow(population, tuning.RpExponent);

    /// <summary>
    /// The polity's ONE shared Research Point pool for this turn (D-044 R2; finalization
    /// terminology ruling): the RP the active target receives, whichever tree it is in. Per turn,
    /// NOT multiplied by dtYears — ADR-030's scoped exception to law 3; the system's throughput
    /// line is the only place it is spent.
    /// </summary>
    public static double ResearchPointPool(IReadOnlyWorldState world, ResearchContent content, PolityId polity) =>
        ResearchPoints(ResearchPointsCoefficient(world, content, polity), content.Tuning.RpExponent, Population(world, polity));

    /// <summary>ADR-047 — research.researchPointsCoefficient for <paramref name="holder"/>: rpPerTurn.coefficient improved by
    /// the holder's realized research (KnowledgeEffects; the base exactly without an effect).</summary>
    public static double ResearchPointsCoefficient(IReadOnlyWorldState world, ResearchContent content, PolityId holder) =>
        KnowledgeEffects.Apply(world, content, holder, KnowledgeOperand.ResearchResearchPointsCoefficient, content.Tuning.RpCoefficient);

    /// <summary>The RP curve with an explicit coefficient (ADR-047: the holder's) — <see cref="ResearchPoints(ResearchTuning,
    /// double)"/>'s arithmetic, which it equals bit for bit at the content coefficient.</summary>
    private static double ResearchPoints(double coefficient, double exponent, double population) =>
        population <= 0.0 ? 0.0 : coefficient * Math.Pow(population, exponent);

    /// <summary>R2a: the RP an UNCONTROLLED settlement (a city-state) generates this turn —
    /// tuning.cityStatePaceFraction × the content's RP curve of the settlement's own population. Per turn,
    /// never × dtYears (ADR-030 §2, as <see cref="ResearchPointPool"/>).</summary>
    public static double CityStateResearchPoints(IReadOnlyWorldState world, ResearchContent content, SettlementId settlement) =>
        content.Tuning.CityStatePaceFraction * ResearchPoints(
            ResearchPointsCoefficient(world, content, SettlementKnowledge.LocalHolder(settlement)), content.Tuning.RpExponent,
            Population(world, SettlementKnowledge.LocalHolder(settlement)));

    // ------------------------------------------------------------------ Civics Points (ADR-043)

    /// <summary>
    /// ADR-043 — Civics Points per SIM-YEAR for a population: civicsPoints.coefficientPerYear × population^exponent
    /// (0.008 × P^0.699, PROPOSED). 0 when the two-lane model is off or the population is empty. Unlike RP this is a
    /// rate per sim-year (law 3 as written): the step integrates it over dtYears (<see cref="CivicsPointsMilli"/>).
    /// </summary>
    public static double CivicsPointsPerYear(ResearchTuning tuning, double population) =>
        tuning.CivicsPoints is not { Enabled: true } cp || population <= 0.0
            ? 0.0 : cp.CoefficientPerYear * Math.Pow(population, cp.Exponent);

    /// <summary>
    /// ADR-043 — THE CP ACCRUAL OF ONE STEP, in integer milli-CP: floor(1000 × pace × CivicsPointsPerYear(P) × dtYears),
    /// where pace is 1 for an Empire and tuning.cityStatePaceFraction for a city-state's reserved local key (the same
    /// factor its RP carries). P is <see cref="Population"/> — the input RP reads. The one function ResearchSystem
    /// accrues with and the Glass Box / UI show.
    /// </summary>
    public static long CivicsPointsMilli(IReadOnlyWorldState world, ResearchContent content, PolityId holder, double dtYears)
    {
        if (!content.Tuning.TwoLanes || !(dtYears > 0.0)) return 0L;
        double pace = SettlementKnowledge.IsLocalHolder(holder) ? content.Tuning.CityStatePaceFraction : 1.0;
        // ADR-047: research.civicsPointsCoefficient, the holder's (the content coefficient exactly without an effect).
        CivicsPointsTuning cp = content.Tuning.CivicsPoints!;
        double coefficient = KnowledgeEffects.Apply(world, content, holder, KnowledgeOperand.ResearchCivicsPointsCoefficient, cp.CoefficientPerYear);
        double population = Population(world, holder);
        double perYear = population <= 0.0 ? 0.0 : coefficient * Math.Pow(population, cp.Exponent);
        double milli = Math.Floor(1000.0 * pace * perYear * dtYears);
        return milli > 0.0 ? (long)milli : 0L;
    }

    /// <summary>ADR-043: the holder's Civics Points account (all zero when it has no row).</summary>
    public static CivicsPointsRow CivicsPointsAccount(IReadOnlyWorldState world, PolityId holder)
    {
        for (int i = 0; i < world.CivicsPoints.Count; i++)
            if (world.CivicsPoints[i].Polity.Value == holder.Value) return world.CivicsPoints[i];
        return new CivicsPointsRow(holder, 0L, 0L, 0L);
    }

    /// <summary>ADR-043: the holder's banked Civics Points (whole CP, for display).</summary>
    public static double CivicsPointsBalance(IReadOnlyWorldState world, PolityId holder) =>
        CivicsPointsAccount(world, holder).BalanceMilli / 1000.0;

    /// <summary>
    /// ADR-043 — the CP table as only ResearchSystem writes it, or null: rows in strictly ascending Polity (so no holder
    /// twice), every field ≥ 0, and the audit identity BalanceMilli == GeneratedMilli − SpentMilli. Snapshot.Load refuses
    /// a save that breaks it; the tests assert it every step.
    /// </summary>
    public static string? CivicsPointsInvariantViolation(IReadOnlyTable<CivicsPointsRow> rows)
    {
        for (int i = 0; i < rows.Count; i++)
        {
            CivicsPointsRow row = rows[i];
            string at = "CivicsPoints[" + i.ToString(System.Globalization.CultureInfo.InvariantCulture) + "]";
            if (i > 0 && rows[i - 1].Polity.Value >= row.Polity.Value)
                return at + ": rows are not in strictly ascending Polity order";
            if (row.BalanceMilli < 0 || row.GeneratedMilli < 0 || row.SpentMilli < 0)
                return at + ": a Civics Points field is negative";
            if (row.BalanceMilli != row.GeneratedMilli - row.SpentMilli)
                return at + ": BalanceMilli is not GeneratedMilli − SpentMilli";
        }
        return null;
    }

    // ------------------------------------------------------------------ acceleration credit (Eureka + foreign exposure)

    public static bool EurekaFired(IReadOnlyWorldState world, PolityId polity, ResearchNodeId node, int eureka)
    {
        for (int i = 0; i < world.ResearchEurekas.Count; i++)
        {
            ResearchEurekaRow row = world.ResearchEurekas[i];
            if (row.Polity.Value == polity.Value && row.Node.Value == node.Value && row.Eureka == eureka) return true;
        }
        return false;
    }

    /// <summary>
    /// Whether a Eureka condition holds for the polity NOW (ADR-029 §7). Node atoms read the
    /// polity's completed knowledge. A condition that reads any settlement-scoped operand — a
    /// registered variable, or a stock_&lt;good&gt; quantity — holds when it holds in AT LEAST ONE
    /// settlement the polity controls, taken in table order. A condition over node atoms only is
    /// evaluated once, at polity scope. An unpublished variable reads 0.0 (the ProductionSystem
    /// precedent).
    /// </summary>
    public static bool EurekaHolds(
        IReadOnlyWorldState world, ResearchContent content, PolityId polity, Predicate condition, bool[] completed)
    {
        Predicate.AtomReader atoms = a => completed[a];
        if (!condition.ReadsVariables && condition.QuantityIds.Count == 0)
            return condition.Evaluate(null, atoms, null);
        for (int s = 0; s < world.Settlements.Count; s++)
        {
            SettlementId id = world.Settlements[s].Id;
            if (SettlementKnowledge.HolderOf(world, id).Value != polity.Value) continue;
            if (condition.Evaluate(v => Variable(world, id, v), atoms, q => Stock(world, id, content.QuantityGoods[q])))
                return true;
        }
        return false;
    }

    /// <summary>The credit one Eureka is worth before the pool caps it: weight × BaseCost.</summary>
    public static double EurekaCredit(ResearchNode node, ResearchEureka eureka) => eureka.Weight * node.BaseCost;

    /// <summary>The cumulative acceleration credit one source has added to a node (provenance).</summary>
    public static double CreditedBySource(IReadOnlyWorldState world, PolityId polity, ResearchNodeId node, AccelerationSource source)
    {
        double total = 0.0;
        for (int i = 0; i < world.ResearchCredits.Count; i++)
        {
            ResearchCreditRow row = world.ResearchCredits[i];
            if (row.Polity.Value == polity.Value && row.Node.Value == node.Value && row.Source == (int)source) total += row.Amount;
        }
        return total;
    }

    /// <summary>The foreign-exposure credit offered to the polity for the node (input seam; 0 when no row).</summary>
    public static double ExposureOffered(IReadOnlyWorldState world, PolityId polity, ResearchNodeId node)
    {
        double total = 0.0;
        for (int i = 0; i < world.ResearchExposures.Count; i++)
        {
            ResearchExposureRow row = world.ResearchExposures[i];
            if (row.Polity.Value == polity.Value && row.Node.Value == node.Value) total += row.Offered;
        }
        return total;
    }

    /// <summary>A node's shared acceleration pool as the Glass Box shows it: the ceiling
    /// (accelerationCreditCeilingFraction × BaseCost), what each source has credited, and the
    /// headroom left. Eureka and foreign exposure draw on this ONE pool.</summary>
    public sealed record AccelerationPool(
        ResearchNodeId Node, double Ceiling, double Eureka, double ForeignExposure, double ExposureOffered)
    {
        public double Credited => Eureka + ForeignExposure;
        public double Headroom => Math.Max(0.0, Ceiling - Credited);
    }

    public static AccelerationPool AccelerationPoolOf(IReadOnlyWorldState world, ResearchContent content, PolityId polity, ResearchNodeId node)
    {
        ResearchNode n = content.Nodes[RequireIndex(content, node)];
        return new AccelerationPool(node, content.Tuning.AccelerationCreditCeilingFraction * n.BaseCost,
            CreditedBySource(world, polity, node, AccelerationSource.Eureka),
            CreditedBySource(world, polity, node, AccelerationSource.ForeignExposure),
            ExposureOffered(world, polity, node));
    }

    /// <summary>One Eureka of a node as the Glass Box shows it.</summary>
    public sealed record EurekaState(
        int Index, string Text, string Kind, string Justification, string System, string Class, string Source,
        double Weight, double MaxCredit, string? Condition, bool Fired, bool? HoldsNow);

    public static EurekaState[] Eurekas(IReadOnlyWorldState world, ResearchContent content, PolityId polity, ResearchNodeId node)
    {
        int index = RequireIndex(content, node);
        bool[] completed = CompletedMask(world, content, polity);
        ResearchNode n = content.Nodes[index];
        var result = new EurekaState[n.Eurekas.Count];
        for (int e = 0; e < n.Eurekas.Count; e++)
        {
            ResearchEureka eu = n.Eurekas[e];
            result[e] = new EurekaState(e, eu.Text, eu.Kind, eu.Justification, eu.System, eu.Class, eu.Source, eu.Weight,
                EurekaCredit(n, eu), eu.Condition?.Source, EurekaFired(world, polity, node, e),
                eu.Condition is null ? null : EurekaHolds(world, content, polity, eu.Condition, completed));
        }
        return result;
    }

    /// <summary>The node's Eureka progress (partial Eurekas): fired of total, evaluable today,
    /// and the weight fired (0 … the node's total weight, at most 0.40).</summary>
    public sealed record EurekaProgress(int Fired, int Total, int EvaluableNow, double FiredWeight, double TotalWeight);

    public static EurekaProgress EurekaProgressOf(IReadOnlyWorldState world, ResearchContent content, PolityId polity, ResearchNodeId node)
    {
        ResearchNode n = content.Nodes[RequireIndex(content, node)];
        int fired = 0, evaluable = 0;
        double firedWeight = 0.0, totalWeight = 0.0;
        for (int e = 0; e < n.Eurekas.Count; e++)
        {
            ResearchEureka eu = n.Eurekas[e];
            totalWeight += eu.Weight;
            if (eu.EvaluableNow) evaluable++;
            if (EurekaFired(world, polity, node, e)) { fired++; firedWeight += eu.Weight; }
        }
        return new EurekaProgress(fired, n.Eurekas.Count, evaluable, firedWeight, totalWeight);
    }

    /// <summary>The baseline capabilities (D-045 §1): available to every polity with zero
    /// completed nodes, because they are not research nodes at all.</summary>
    public static IReadOnlyList<ResearchBaselineCapability> BaselineCapabilities(ResearchContent content) => content.Baseline;

    private static double Variable(IReadOnlyWorldState world, SettlementId settlement, int varId)
    {
        for (int i = 0; i < world.Variables.Count; i++)
        {
            VariableRow row = world.Variables[i];
            if (row.Settlement.Value == settlement.Value && row.VarId == varId) return row.Value;
        }
        return 0.0;
    }

    private static double Stock(IReadOnlyWorldState world, SettlementId settlement, GoodId good)
    {
        for (int i = 0; i < world.GoodStocks.Count; i++)
        {
            GoodStockRow row = world.GoodStocks[i];
            if (row.Settlement.Value == settlement.Value && row.Good.Value == good.Value) return row.Amount.Value;
        }
        return 0.0;
    }

    // ------------------------------------------------------------------ unlocks

    /// <summary>
    /// KNOWLEDGE-level eligibility of a registry entity (ADR-029 §10). It asks one
    /// thing: does the completed knowledge satisfy the entity's knowledge requirement?
    /// An institution named inside a requirement counts when ITS OWN knowledge
    /// requirement is met. A declared-unresolved corpus reference reads false. A null
    /// requirement is always met (ADR-028 §3).
    ///
    /// This is NOT availability. Sites, resources, required buildings, institutional
    /// presence and construction belong to the owning system (D-044 R14; ADR-028 §3–§4),
    /// and no system consumes this answer yet (D-040 B3: no node opens sea travel or
    /// any movement mode).
    /// </summary>
    public static bool IsKnowledgeEligible(ResearchContent content, int entity, bool[] completed)
    {
        var memo = new sbyte[content.Entities.Count];
        return Eligible(content, entity, completed, memo);
    }

    private static bool Eligible(ResearchContent content, int entity, bool[] completed, sbyte[] memo)
    {
        if (memo[entity] != 0) return memo[entity] > 0;
        ResearchEntity e = content.Entities[entity];
        bool ok = e.Requirement is not { } req || Holds(content, req, completed, memo);
        memo[entity] = ok ? (sbyte)1 : (sbyte)-1;
        return ok;
    }

    /// <summary>The ONE atom reader of the knowledge evaluator: a node atom reads completed
    /// knowledge, an institution atom reads that institution's own eligibility, and the
    /// declared-unresolved sentinel (and anything past it) reads false.</summary>
    private static bool Holds(ResearchContent content, Predicate requirement, bool[] completed, sbyte[] memo)
    {
        int nodes = content.Nodes.Count;
        return requirement.Evaluate(null, a => a < nodes ? completed[a]
                                         : a < nodes + content.Entities.Count && Eligible(content, a - nodes, completed, memo),
                                    null);
    }

    /// <summary>
    /// ADR-033 D4 — does the completed knowledge satisfy a requirement authored OUTSIDE
    /// research.json (sim.json <c>governance.taxationRequires</c>, parsed by
    /// <see cref="ResearchContentLoader.ParseRequirement"/>)? Evaluated by EXACTLY the reader an
    /// entity requirement uses (<see cref="IsKnowledgeEligible"/>), so a gate authored in sim.json
    /// and one authored on a research.json entity can never disagree about the same knowledge.
    /// </summary>
    public static bool RequirementMet(ResearchContent content, Predicate requirement, bool[] completed)
    {
        ArgumentNullException.ThrowIfNull(content);
        ArgumentNullException.ThrowIfNull(requirement);
        return Holds(content, requirement, completed, new sbyte[content.Entities.Count]);
    }

    /// <summary>Entity ids whose knowledge requirement the polity meets, entity order.</summary>
    public static string[] KnowledgeEligibleEntities(IReadOnlyWorldState world, ResearchContent content, PolityId polity)
    {
        bool[] completed = CompletedMask(world, content, polity);
        var memo = new sbyte[content.Entities.Count];
        var result = new List<string>();
        for (int e = 0; e < content.Entities.Count; e++)
            if (Eligible(content, e, completed, memo)) result.Add(content.Entities[e].Id);
        return [.. result];
    }

    /// <summary>The capabilities declared by the polity's completed nodes, node-key order
    /// (D-044 R11 item 3: unlocked at completion; realized by their owning systems).</summary>
    public static string[] UnlockedCapabilities(IReadOnlyWorldState world, ResearchContent content, PolityId polity)
    {
        bool[] completed = CompletedMask(world, content, polity);
        var result = new List<string>();
        for (int i = 0; i < completed.Length; i++)
            if (completed[i]) result.AddRange(content.Nodes[i].Capabilities);
        return [.. result];
    }

    // ------------------------------------------------------------------ helpers

    private static int RequireIndex(ResearchContent content, ResearchNodeId node)
    {
        int index = content.IndexOf(node);
        if (index < 0) throw new ArgumentOutOfRangeException(nameof(node), node.Value, "no research node has this key");
        return index;
    }
}
