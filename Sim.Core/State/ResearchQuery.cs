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

    /// <summary>AVAILABLE = not completed (a LEVELLED repeatable stays available after it completes, ADR-049), its subtree
    /// open, its prerequisite expression (AND / OR / nested) satisfied by completed knowledge (D-044 R8), and — for a
    /// RECURSIVE node only — its exhaustion rule: a subtree technology waits for every finite node of its own subtree
    /// (finalization ruling 4: per-subtree exhaustion), a trunk technology for nothing more, and a civic for every finite
    /// civic of Age A9 (ADR-049; contract §4). The level cap (<see cref="MaxRepeatableLevel"/>) is the overload taking the
    /// node's level.</summary>
    public static bool IsAvailable(ResearchContent content, int node, bool[] completed, bool stageReached)
    {
        ResearchNode n = content.Nodes[node];
        if ((completed[node] && !IsLevelled(content, node)) || (n.Branch >= 0 && !stageReached) || !PrerequisitesMet(content, node, completed)) return false;
        return !n.IsRecursive || RepeatableOpen(content, n, completed);
    }

    /// <summary>ADR-049: <see cref="IsAvailable(ResearchContent, int, bool[], bool)"/> with the level cap — a levelled
    /// repeatable at <see cref="MaxRepeatableLevel"/> (<paramref name="level"/> = its <see cref="Level"/>) is no longer
    /// available. The form ResearchSystem, the AI and the order constructor use.</summary>
    public static bool IsAvailable(ResearchContent content, int node, bool[] completed, bool stageReached, int level) =>
        level < MaxRepeatableLevel && IsAvailable(content, node, completed, stageReached);

    /// <summary>The exhaustion rule of a RECURSIVE node (see <see cref="IsAvailable(ResearchContent, int, bool[], bool)"/>).</summary>
    private static bool RepeatableOpen(ResearchContent content, ResearchNode n, bool[] completed)
    {
        if (n.Tree == ResearchTree.Civics) return FinalAgeCivicsExhausted(content, completed);
        return n.Branch < 0 || SubtreeExhausted(content, n.Branch, completed);
    }

    /// <summary>ADR-049: whether every FINITE civic of Age A9 is complete — what a civics repeatable waits for.</summary>
    public static bool FinalAgeCivicsExhausted(ResearchContent content, bool[] completed)
    {
        foreach (int i in content.FiniteFinalAgeCivics) if (!completed[i]) return false;
        return true;
    }

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
        var mask = new bool[completed.Length];
        for (int i = 0; i < mask.Length; i++) mask[i] = IsAvailable(content, i, completed, stage);
        return mask;
    }

    /// <summary>ADR-049: <see cref="AvailableMask(ResearchContent, bool[])"/> with the level cap applied from
    /// <paramref name="levels"/> (<see cref="LevelMask"/>).</summary>
    public static bool[] AvailableMask(ResearchContent content, bool[] completed, int[] levels)
    {
        bool[] mask = AvailableMask(content, completed);
        for (int i = 0; i < mask.Length; i++) if (levels[i] >= MaxRepeatableLevel) mask[i] = false;
        return mask;
    }

    /// <summary>ADR-049: the holder's availability from the world — completed knowledge AND repeatable levels.</summary>
    public static bool[] AvailableMask(IReadOnlyWorldState world, ResearchContent content, PolityId polity)
    {
        bool[] completed = CompletedMask(world, content, polity);
        return AvailableMask(content, completed, LevelMask(world, content, polity, completed));
    }

    // ------------------------------------------------------------------ repeatable levels (ADR-049)

    /// <summary>
    /// ADR-049 — THE LEVEL CAP: a levelled repeatable stops being available at this level. 1,000 keeps every level cost
    /// finite (BaseCost × g^(n−1) with g ≤ 1.30: 1.30^999 ≈ 2.6 × 10^113, far below double's 1.8 × 10^308 — 10,000 would
    /// overflow to +∞ from about level 2,700) and is unreachable in play (the cost of level 100 is already g^99 ≥ 1.3 × 10^4
    /// times level 1's). The level is an int, so it never overflows either.
    /// </summary>
    public const int MaxRepeatableLevel = 1000;

    /// <summary>ADR-049: whether the node completes in LEVELS — research.json tuning.repeatableLevels is on and its
    /// repeatable descriptor carries <c>"levels": true</c>. False for every node with the switch off (complete once).</summary>
    public static bool IsLevelled(ResearchContent content, int node) =>
        content.Tuning.RepeatableLevels && content.Nodes[node].Levels is not null;

    /// <summary>
    /// ADR-049 — THE NUMBER OF TIMES the holder has completed <paramref name="node"/>: its ResearchLevels row's level; else 1
    /// when the node is completed (a node completed once, or a repeatable completed before levels existed — a v35 save, or
    /// the switch off), else 0. Never reads content, so it answers for any node.
    /// </summary>
    public static int Level(IReadOnlyWorldState world, PolityId polity, ResearchNodeId node)
    {
        for (int i = 0; i < world.ResearchLevels.Count; i++)
        {
            ResearchLevelRow row = world.ResearchLevels[i];
            if (row.Polity.Value == polity.Value && row.Node.Value == node.Value) return row.Level;
        }
        return IsCompleted(world, polity, node) ? 1 : 0;
    }

    /// <summary>ADR-049: <see cref="Level"/> of every LEVELLED node as a mask over node indices (0 for every other node — a
    /// complete-once node is gated by completion, never by the cap). <paramref name="completed"/> is the holder's
    /// <see cref="CompletedMask"/>.</summary>
    public static int[] LevelMask(IReadOnlyWorldState world, ResearchContent content, PolityId polity, bool[] completed)
    {
        var levels = new int[content.Nodes.Count];
        if (!content.Tuning.RepeatableLevels) return levels;
        for (int i = 0; i < levels.Length; i++)
            if (completed[i] && content.Nodes[i].Levels is not null) levels[i] = 1;
        for (int r = 0; r < world.ResearchLevels.Count; r++)
        {
            ResearchLevelRow row = world.ResearchLevels[r];
            if (row.Polity.Value != polity.Value) continue;
            int index = content.IndexOf(row.Node);
            if (index >= 0 && IsLevelled(content, index)) levels[index] = row.Level;
        }
        return levels;
    }

    /// <summary>
    /// ADR-049 — THE BASE COST OF LEVEL <paramref name="level"/> (≥ 1) of a levelled node: BaseCost × g^(level−1), g its
    /// costGrowth (contract §4). Level 1 is BaseCost exactly. The modifier stack and the floor of
    /// <see cref="EffectiveCost"/> apply to this base as they do to BaseCost.
    /// </summary>
    public static double LevelBaseCost(ResearchContent content, int node, int level)
    {
        ResearchNode n = content.Nodes[node];
        if (n.Levels is not { } rule) throw new ArgumentException($"'{n.Id}' has no level rule", nameof(node));
        if (level < 1) throw new ArgumentOutOfRangeException(nameof(level), level, "levels start at 1");
        return level == 1 ? n.BaseCost : n.BaseCost * Math.Pow(rule.CostGrowth, level - 1);
    }

    /// <summary>The base cost the holder's NEXT completion of <paramref name="node"/> is priced from: BaseCost for any node
    /// that is not levelled; for a levelled one, <see cref="LevelBaseCost"/> of level <see cref="Level"/> + 1.</summary>
    private static double NextBaseCost(IReadOnlyWorldState world, ResearchContent content, PolityId polity, int node)
    {
        if (!IsLevelled(content, node)) return content.Nodes[node].BaseCost;
        int level = Level(world, polity, content.Nodes[node].Key);
        return LevelBaseCost(content, node, Math.Min(level, MaxRepeatableLevel - 1) + 1);
    }

    /// <summary>
    /// ADR-049 — THE CUMULATIVE CONTRIBUTION of <paramref name="levels"/> completed levels of one operand effect to Σ of its
    /// operand: cap × (1 − (1 − k)^n), which is exactly cap × Σ_{i=1..n} k(1 − k)^(i−1) (a geometric sum) — level i adds
    /// cap × k × (1 − k)^(i−1), each level closing the fraction k of the gap still left to the cap. 0 for n ≤ 0; never above
    /// cap (0 &lt; k ≤ 0.5): in exact arithmetic strictly below it for every finite n, in double precision equal to it once
    /// (1 − k)^n falls below half an ulp of 1 (n ≈ 350 for k = 0.1), so levels can never run away (DD-14). Pure. The effects
    /// engine adds it to the operand's Σ before the clamp to [0, cap] (contract §1, step 1).
    /// </summary>
    public static double RepeatableContribution(int levels, double gapFraction, double cap) =>
        levels <= 0 ? 0.0 : cap * (1.0 - Math.Pow(1.0 - gapFraction, levels));

    /// <summary>ADR-049: what level <paramref name="level"/> (≥ 1) alone adds: cap × k × (1 − k)^(level−1).</summary>
    public static double RepeatableLevelBenefit(int level, double gapFraction, double cap) =>
        level <= 0 ? 0.0 : cap * gapFraction * Math.Pow(1.0 - gapFraction, level - 1);

    /// <summary>
    /// ADR-049 — THE LEVEL SEAM for the effects engine: Σ over the holder's levelled repeatables (index order) that name
    /// <paramref name="operand"/> of <see cref="RepeatableContribution"/>(level, k, cap). 0 with the switch off. The caller
    /// passes the operand's registry cap and clamps the total Σ itself.
    /// </summary>
    public static double LevelContribution(IReadOnlyWorldState world, ResearchContent content, PolityId polity, string operand, double cap)
    {
        if (!content.Tuning.RepeatableLevels) return 0.0;
        double sum = 0.0;
        for (int i = 0; i < content.Nodes.Count; i++)
        {
            if (content.Nodes[i].Levels is not { } rule) continue;
            foreach (RepeatableLevelEffect e in rule.Effects)
            {
                if (!string.Equals(e.Operand, operand, StringComparison.Ordinal)) continue;
                int level = Level(world, polity, content.Nodes[i].Key);
                if (level > 0) sum += RepeatableContribution(level, e.GapFraction, cap);
            }
        }
        return sum;
    }

    /// <summary>ADR-049: a ResearchLevels table no rule could have written, described — a level outside
    /// [1, <see cref="MaxRepeatableLevel"/>] or a (Polity, Node) pair twice — or null when it is well formed.</summary>
    public static string? LevelsInvariantViolation(IReadOnlyTable<ResearchLevelRow> rows)
    {
        for (int i = 0; i < rows.Count; i++)
        {
            ResearchLevelRow row = rows[i];
            string at = "ResearchLevels[" + i.ToString(System.Globalization.CultureInfo.InvariantCulture) + "]";
            if (row.Level < 1 || row.Level > MaxRepeatableLevel)
                return at + ": level " + row.Level.ToString(System.Globalization.CultureInfo.InvariantCulture) + " is outside [1, " +
                       MaxRepeatableLevel.ToString(System.Globalization.CultureInfo.InvariantCulture) + "]";
            for (int j = 0; j < i; j++)
                if (rows[j].Polity.Value == row.Polity.Value && rows[j].Node.Value == row.Node.Value)
                    return at + ": a second row for polity " + row.Polity.Value.ToString(System.Globalization.CultureInfo.InvariantCulture)
                           + ", node " + row.Node.Value.ToString(System.Globalization.CultureInfo.InvariantCulture);
        }
        return null;
    }

    /// <summary>Available nodes, key-ascending. <paramref name="tree"/> filters by tree;
    /// <paramref name="branch"/> filters to one subtree (-1 = the Main Technology Tree).</summary>
    public static ResearchNodeId[] AvailableNodes(
        IReadOnlyWorldState world, ResearchContent content, PolityId polity,
        ResearchTree? tree = null, int? branch = null)
    {
        bool[] available = AvailableMask(world, content, polity);
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
        bool[] available = AvailableMask(world, content, polity);
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
            n.Branch < 0 || stage, completed[index],
            IsAvailable(content, index, completed, stage, LevelMask(world, content, polity, completed)[index]));
    }

    // ------------------------------------------------------------------ target and progress

    /// <summary>
    /// THE research directive, built the same way for the player and the AI (ADR-033 D5; the
    /// AgeQuery.AdvanceOrder / RoadDevelopmentQuery.DevelopOrder precedent): the SetResearchTarget order
    /// that makes <paramref name="node"/> the issuer's one active target, stamped with the CURRENT turn (the
    /// step executing from this state already spends its RP on it), or null when the node is not AVAILABLE
    /// to the issuer now — refused by <see cref="IsAvailable"/>, the availability ResearchSystem applies, so
    /// no caller ever logs an order the simulation would ignore.
    /// </summary>
    public static Kernel.OrderRecord? TargetOrder(
        IReadOnlyWorldState world, ResearchContent content, PolityId issuer, ResearchNodeId node)
    {
        int index = content.IndexOf(node);
        if (index < 0) return null;
        bool[] completed = CompletedMask(world, content, issuer);
        if (!IsAvailable(content, index, completed, StageReached(content, completed), LevelMask(world, content, issuer, completed)[index])) return null;
        return Kernel.OrderRecord.From(world.Clock.Turn, issuer, Kernel.OrderKind.SetResearchTarget, node.Value, 0.0);
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
    /// is BaseCost × Π Factor; <paramref name="Floor"/> the effective-cost floor; EffectiveCost the larger. ADR-049: for a
    /// levelled repeatable <paramref name="BaseCost"/> is the NEXT level's base, BaseCost × g^level.</summary>
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
    ///
    /// ADR-049: for a LEVELLED repeatable the cost is that of the holder's NEXT level — BaseCost is replaced by
    /// BaseCost × g^level (<see cref="LevelBaseCost"/> of level <see cref="Level"/> + 1), under the same modifiers and floor.
    public static double EffectiveCost(IReadOnlyWorldState world, ResearchContent content, PolityId polity, int node)
    {
        double baseCost = NextBaseCost(world, content, polity, node);
        return Math.Max(content.Tuning.EffectiveCostFloorFraction * baseCost, Modified(world, content, polity, node, baseCost, null));
    }

    public static CostBreakdown EffectiveCostBreakdown(
        IReadOnlyWorldState world, ResearchContent content, PolityId polity, ResearchNodeId node)
    {
        int index = RequireIndex(content, node);
        var terms = new List<CostTerm>();
        double baseCost = NextBaseCost(world, content, polity, index);   // ADR-049: the next level's base for a levelled node
        double modified = Modified(world, content, polity, index, baseCost, terms);
        double floor = content.Tuning.EffectiveCostFloorFraction * baseCost;
        return new CostBreakdown(node, baseCost, [.. terms], modified, floor, Math.Max(floor, modified));
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

    private static double Modified(IReadOnlyWorldState world, ResearchContent content, PolityId polity, int node, double baseCost, List<CostTerm>? terms)
    {
        ResearchNode n = content.Nodes[node];
        double cost = baseCost;
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
        ResearchPoints(content.Tuning, Population(world, polity));

    /// <summary>R2a: the RP an UNCONTROLLED settlement (a city-state) generates this turn —
    /// tuning.cityStatePaceFraction × the content's RP curve of the settlement's own population. Per turn,
    /// never × dtYears (ADR-030 §2, as <see cref="ResearchPointPool"/>).</summary>
    public static double CityStateResearchPoints(IReadOnlyWorldState world, ResearchContent content, SettlementId settlement) =>
        content.Tuning.CityStatePaceFraction * ResearchPoints(content.Tuning, Population(world, SettlementKnowledge.LocalHolder(settlement)));

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
        double milli = Math.Floor(1000.0 * pace * CivicsPointsPerYear(content.Tuning, Population(world, holder)) * dtYears);
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
