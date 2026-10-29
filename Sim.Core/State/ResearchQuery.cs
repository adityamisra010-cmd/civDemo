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

    /// <summary>AVAILABLE (= ELIGIBLE) = not completed (a LEVELLED repeatable stays available after it completes,
    /// ADR-049), its subtree open, its prerequisite expression (AND / OR / nested) satisfied by completed knowledge
    /// (D-044 R8), — for a RECURSIVE node only — its exhaustion rule (a subtree technology waits for every finite node of
    /// its own subtree, finalization ruling 4; a trunk technology for nothing more; a civic for every finite civic of Age
    /// A9, ADR-049), and — ADR-048, with tuning.ageSweep — the Age sweep (<see cref="SweepSatisfied"/>). This is THE
    /// eligibility predicate: the player's orders, the AI, city states, the UI and the route planner all read it (through
    /// <see cref="AvailableMask"/> or here). The level cap (<see cref="MaxRepeatableLevel"/>) is the overload taking the
    /// node's level.</summary>
    public static bool IsAvailable(ResearchContent content, int node, bool[] completed, bool stageReached) =>
        IsAvailable(content, node, completed, stageReached, null);

    /// <summary><see cref="IsAvailable(ResearchContent, int, bool[], bool)"/> with the Age sweep read from a precomputed
    /// <see cref="SweptAges"/> (null: computed for this node alone) — the same predicate, for callers that test many nodes.</summary>
    public static bool IsAvailable(ResearchContent content, int node, bool[] completed, bool stageReached, bool[]? sweptAges)
    {
        ResearchNode n = content.Nodes[node];
        if ((completed[node] && !IsLevelled(content, node)) || (n.Branch >= 0 && !stageReached) || !PrerequisitesMet(content, node, completed)) return false;
        if (n.IsRecursive && !RepeatableOpen(content, n, completed)) return false;
        return sweptAges is null ? SweepSatisfied(content, node, completed) : SweepOpen(content, node, sweptAges);
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
            : IsAvailable(content, index, completed, StageReached(content, completed), LevelMask(world, content, issuer, completed)[index]);
        if (!admissible) return null;
        return Kernel.OrderRecord.From(world.Clock.Turn, issuer, Kernel.OrderKind.SetResearchTarget, node.Value, 0.0);
    }

    // ------------------------------------------------------------------ destination research (ADR-048)

    /// <summary>ADR-048 (tranche-2 contract §3): with tuning.destinationRouting, a SetResearchTarget order may name ANY
    /// INCOMPLETE node as the lane's destination, available or not. The one admission rule ResearchSystem, the order
    /// builder and the UI apply.</summary>
    public static bool IsDestinationAdmissible(ResearchContent content, int node, bool[] completed) =>
        node >= 0 && node < completed.Length && IsOpenTarget(content, node, completed);

    /// <summary>ADR-048 × ADR-049: a node a destination may still name — incomplete, or a LEVELLED repeatable (its next
    /// level; the level cap is the availability's).</summary>
    public static bool IsOpenTarget(ResearchContent content, int node, bool[] completed) =>
        !completed[node] || IsLevelled(content, node);

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
                if (index >= 0 && IsOpenTarget(content, index, completed)) destination[0] = index;
                break;   // the one-pool model reads the first row only (TryGetTarget)
            }
            int lane = ResearchRoutePlanner.LaneOf(content, index < 0 ? 0 : index, twoLanes);
            if (index >= 0 && IsOpenTarget(content, index, completed) && destination[lane] < 0) destination[lane] = index;
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

    /// <summary>
    /// What a lane's points do on the next step (no new order), read from its <see cref="Route"/> — display only, the
    /// research screens' one classification (rp-cp-lifecycle k2–k4). <see cref="LaneActivity.Researching"/>: the lane funds
    /// <see cref="ResearchRoute.Current"/>, its destination or a route node toward it. <see cref="LaneActivity.Assisting"/>:
    /// it funds a node of its own tree on the OTHER lane's route (<see cref="ResearchRoute.AssistedDestination"/>).
    /// <see cref="LaneActivity.Waiting"/>: it holds a destination but no node of its route can be funded (the route waits on
    /// the other tree) — the step funds nothing on this lane. <see cref="LaneActivity.Idle"/>: no destination, no assist.
    /// </summary>
    public static LaneActivity Activity(ResearchRoute route) =>
        route.Current is not null ? (route.Assisting ? LaneActivity.Assisting : LaneActivity.Researching)
        : route.Destination is not null ? LaneActivity.Waiting
        : LaneActivity.Idle;

    /// <summary>
    /// Display only: the outstanding nodes of the OTHER tree on a lane's route — what a <see cref="LaneActivity.Waiting"/>
    /// lane waits for — in the route's blocker order (<see cref="ResearchRoute.Blockers"/>). Empty in the one-pool model
    /// (its one lane funds both trees) and when the route holds no node of the other tree.
    /// </summary>
    public static List<ResearchNodeId> WaitingOn(ResearchContent content, ResearchRoute route)
    {
        var result = new List<ResearchNodeId>();
        if (!content.Tuning.TwoLanes) return result;
        foreach (RouteBlocker b in route.Blockers)
            if (content.Nodes[b.Index].Tree != route.Lane) result.Add(b.Key);
        return result;
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

    // ------------------------------------------------------------------ the knowledge population and base (ADR-054)

    /// <summary>
    /// ADR-054 — THE AVERAGE POPULATION every Research Point and Civics Point calculation reads (the Director's binding rule
    /// of 2026-10-27: "every RP and CP calculation is per turn and based on average population"). STATED ASSUMPTION (ADR-054
    /// §3): the holder's population averaged over the most recent turn interval — the trapezoid mean of the population at the
    /// start of its previous research step (its ResearchPopulations row) and at the start of this one
    /// (<see cref="Population"/>): (prev + now) / 2. A holder with no stored row (its first step; a city state just released;
    /// a save from before v38) reads its current population. With tuning.populationAverageTurns 1 (or absent — the
    /// attribution switch) it is <see cref="Population"/> exactly, so every reader computes what it computed before ADR-054.
    /// The ONE input of <see cref="ResearchPointPool"/>, <see cref="CityStateResearchPoints"/> and
    /// <see cref="CivicsPointsMilli"/> — the same function for both lanes.
    /// </summary>
    public static double KnowledgePopulation(IReadOnlyWorldState world, ResearchContent content, PolityId holder)
    {
        long now = Population(world, holder);
        if (content.Tuning.PopulationAverageTurns < 2 || !TryGetStoredPopulation(world, holder, out long previous)) return now;
        return ((double)previous + (double)now) * 0.5;
    }

    /// <summary>ADR-054: the holder's population at the start of its last research step (its ResearchPopulations row), or
    /// false without a row.</summary>
    public static bool TryGetStoredPopulation(IReadOnlyWorldState world, PolityId holder, out long population)
    {
        for (int i = 0; i < world.ResearchPopulations.Count; i++)
            if (world.ResearchPopulations[i].Polity.Value == holder.Value)
            {
                population = world.ResearchPopulations[i].Population;
                return true;
            }
        population = 0L;
        return false;
    }

    /// <summary>
    /// ADR-054 — the ResearchPopulations table as only ResearchSystem writes it, or null: rows in strictly ascending Polity
    /// (so no holder twice) and every population ≥ 0. Snapshot.Load refuses a save that breaks it.
    /// </summary>
    public static string? PopulationsInvariantViolation(IReadOnlyTable<ResearchPopulationRow> rows)
    {
        for (int i = 0; i < rows.Count; i++)
        {
            string at = "ResearchPopulations[" + i.ToString(System.Globalization.CultureInfo.InvariantCulture) + "]";
            if (i > 0 && rows[i - 1].Polity.Value >= rows[i].Polity.Value)
                return at + ": rows are not in strictly ascending Polity order";
            if (rows[i].Population < 0) return at + ": a population is negative";
        }
        return null;
    }

    /// <summary>
    /// ADR-054 — THE SHARED BASE: knowledge points per turn for a population, rpPerTurn.coefficient ×
    /// population^rpPerTurn.exponent, with no benefit of any kind (no realized research effect, no university, building,
    /// buff, penalty or espionage). Research Points AND per-turn Civics Points are this function of the same
    /// <see cref="KnowledgePopulation"/>, so base RP = base CP at every turn length by construction (ADR-054 §2). Each lane's
    /// holder-specific benefit (research.researchPointsCoefficient / research.civicsPointsCoefficient, ADR-047) multiplies
    /// the coefficient on top; at no benefit the lane equals this bit for bit.
    /// </summary>
    public static double BaseKnowledgePointsPerTurn(ResearchTuning tuning, double population) =>
        ResearchPoints(tuning.RpCoefficient, tuning.RpExponent, population);

    /// <summary>
    /// Research Points per strategic turn for a population: rpPerTurn.coefficient ×
    /// population^rpPerTurn.exponent (0.08 × P^0.699; anchors 100 → 2, 1,000 → 10). Sublinear by
    /// validation (exponent &lt; 1). Calibration values, not ratified constants; the PER-TURN
    /// reading is the ruling (ADR-030). Population is the only input — literacy, education,
    /// universities and the rest are FUTURE modifiers, not part of this base curve.
    /// </summary>
    public static double ResearchPoints(ResearchTuning tuning, double population) => BaseKnowledgePointsPerTurn(tuning, population);

    /// <summary>
    /// The polity's ONE shared Research Point pool for this turn (D-044 R2; finalization
    /// terminology ruling): the RP the active target receives, whichever tree it is in. Per turn,
    /// NOT multiplied by dtYears — ADR-030's scoped exception to law 3; the system's throughput
    /// line is the only place it is spent. ADR-054: of the polity's <see cref="KnowledgePopulation"/> (the average population).
    /// </summary>
    public static double ResearchPointPool(IReadOnlyWorldState world, ResearchContent content, PolityId polity) =>
        ResearchPoints(ResearchPointsCoefficient(world, content, polity), content.Tuning.RpExponent, KnowledgePopulation(world, content, polity));

    /// <summary>ADR-047 — research.researchPointsCoefficient for <paramref name="holder"/>: rpPerTurn.coefficient improved by
    /// the holder's realized research (KnowledgeEffects; the base exactly without an effect).</summary>
    public static double ResearchPointsCoefficient(IReadOnlyWorldState world, ResearchContent content, PolityId holder) =>
        KnowledgeEffects.Apply(world, content, holder, KnowledgeOperand.ResearchResearchPointsCoefficient, content.Tuning.RpCoefficient);

    /// <summary>The knowledge-point curve with an explicit coefficient (ADR-047: the holder's) — <see
    /// cref="BaseKnowledgePointsPerTurn"/>'s arithmetic, which it equals bit for bit at the content coefficient. Both lanes
    /// call it (ADR-054).</summary>
    private static double ResearchPoints(double coefficient, double exponent, double population) =>
        population <= 0.0 ? 0.0 : coefficient * Math.Pow(population, exponent);

    /// <summary>R2a: the RP an UNCONTROLLED settlement (a city-state) generates this turn —
    /// tuning.cityStatePaceFraction × the content's RP curve of the settlement's own population. Per turn,
    /// never × dtYears (ADR-030 §2, as <see cref="ResearchPointPool"/>). ADR-054: of its <see cref="KnowledgePopulation"/>.</summary>
    public static double CityStateResearchPoints(IReadOnlyWorldState world, ResearchContent content, SettlementId settlement) =>
        content.Tuning.CityStatePaceFraction * ResearchPoints(
            ResearchPointsCoefficient(world, content, SettlementKnowledge.LocalHolder(settlement)), content.Tuning.RpExponent,
            KnowledgePopulation(world, content, SettlementKnowledge.LocalHolder(settlement)));

    // ------------------------------------------------------------------ Civics Points (ADR-043)

    /// <summary>
    /// ADR-043 — Civics Points per SIM-YEAR for a population: civicsPoints.coefficientPerYear × population^exponent
    /// (0.008 × P^0.699). 0 when the two-lane model is off, when CP are per turn (ADR-054: there is no per-year rate — see
    /// <see cref="CivicsPointsPerTurn"/>) or the population is empty. The ADR-043 §3 form, kept as the attribution twin.
    /// </summary>
    public static double CivicsPointsPerYear(ResearchTuning tuning, double population) =>
        tuning.CivicsPoints is not { Enabled: true, PerTurn: false } cp || population <= 0.0
            ? 0.0 : cp.CoefficientPerYear * Math.Pow(population, cp.Exponent);

    /// <summary>
    /// ADR-054 — BASE Civics Points per turn for a population, with no benefit: <see cref="BaseKnowledgePointsPerTurn"/>
    /// itself when tuning.civicsPoints.perTurn (base CP = base RP by construction), 0 when the two-lane model is off or CP are
    /// per sim-year (the ADR-043 twin: its per-turn amount depends on the step's dtYears — <see cref="CivicsPointsMilli"/>).
    /// </summary>
    public static double CivicsPointsPerTurn(ResearchTuning tuning, double population) =>
        tuning.CivicsPoints is { Enabled: true, PerTurn: true } ? BaseKnowledgePointsPerTurn(tuning, population) : 0.0;

    /// <summary>
    /// THE CP ACCRUAL OF ONE STEP, in integer milli-CP — the one function ResearchSystem accrues with and the Glass Box / UI
    /// show. pace is 1 for an Empire and tuning.cityStatePaceFraction for a city-state's reserved local key (the same factor
    /// its RP carries); P is <see cref="KnowledgePopulation"/> — the input RP reads.
    /// <para>ADR-054 (tuning.civicsPoints.perTurn, shipped): floor(1000 × pace × c' × P^rpPerTurn.exponent), PER TURN — NOT
    /// multiplied by <paramref name="dtYears"/> (ADR-054's scoped per-turn exception for Civics Points, beside ADR-030's for
    /// RP; this is its one code site). c' is research.civicsPointsCoefficient, the holder's realized benefit on
    /// rpPerTurn.coefficient (ADR-047), so with no benefit the accrual is floor(1000 × pace × base RP) at every turn length.
    /// <paramref name="dtYears"/> only gates a non-positive step.</para>
    /// <para>ADR-043 §3 (perTurn absent — the attribution twin): floor(1000 × pace × c' × P^β × dtYears), c' the holder's
    /// civicsPoints.coefficientPerYear.</para>
    /// </summary>
    public static long CivicsPointsMilli(IReadOnlyWorldState world, ResearchContent content, PolityId holder, double dtYears)
    {
        if (!content.Tuning.TwoLanes || !(dtYears > 0.0)) return 0L;
        double pace = SettlementKnowledge.IsLocalHolder(holder) ? content.Tuning.CityStatePaceFraction : 1.0;
        CivicsPointsTuning cp = content.Tuning.CivicsPoints!;
        double population = KnowledgePopulation(world, content, holder);
        double milli;
        if (cp.PerTurn)
        {
            // ADR-054 §2: per turn, the shared base function — never × dtYears.
            double coefficient = KnowledgeEffects.Apply(world, content, holder, KnowledgeOperand.ResearchCivicsPointsCoefficient, content.Tuning.RpCoefficient);
            milli = Math.Floor(1000.0 * pace * ResearchPoints(coefficient, content.Tuning.RpExponent, population));
        }
        else
        {
            // ADR-047: research.civicsPointsCoefficient, the holder's (the content coefficient exactly without an effect).
            double coefficient = KnowledgeEffects.Apply(world, content, holder, KnowledgeOperand.ResearchCivicsPointsCoefficient, cp.CoefficientPerYear);
            double perYear = population <= 0.0 ? 0.0 : coefficient * Math.Pow(population, cp.Exponent);
            milli = Math.Floor(1000.0 * pace * perYear * dtYears);
        }
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

    // ------------------------------------------------------------------ the technology-lane RP reserve (ADR-051)

    /// <summary>
    /// The RP a knowledge holder generates this turn: <see cref="CityStateResearchPoints"/> for a city-state's reserved local
    /// key, else <see cref="ResearchPointPool"/> — exactly the amount ResearchSystem's throughput spends (per turn, never ×
    /// dtYears, ADR-030 §2).
    /// </summary>
    public static double HolderResearchPoints(IReadOnlyWorldState world, ResearchContent content, PolityId holder) =>
        SettlementKnowledge.IsLocalHolder(holder)
            ? CityStateResearchPoints(world, content, SettlementKnowledge.SettlementOf(holder))
            : ResearchPointPool(world, content, holder);

    /// <summary>ADR-051: RP → integer milli-RP, rounded DOWN (never more than the RP it stands for); 0 for a non-positive or
    /// NaN amount, saturating at long.MaxValue (+∞ included).</summary>
    public static long ToMilliRp(double rp)
    {
        double milli = Math.Floor(rp * 1000.0);
        if (!(milli > 0.0)) return 0L;
        return milli >= 9.2e18 ? long.MaxValue : (long)milli;
    }

    /// <summary>ADR-051: the holder's research reserve in milli-RP (0 without a row).</summary>
    public static long ReserveMilli(IReadOnlyWorldState world, PolityId holder)
    {
        for (int i = 0; i < world.ResearchReserves.Count; i++)
            if (world.ResearchReserves[i].Polity.Value == holder.Value) return world.ResearchReserves[i].ReserveMilli;
        return 0L;
    }

    /// <summary>ADR-051: the reserve's cap for the holder's CURRENT pool, milli-RP — floor(1000 × capTurns × pool); 0 with
    /// tuning.researchReserve absent.</summary>
    public static long ReserveCapMilli(IReadOnlyWorldState world, ResearchContent content, PolityId holder) =>
        content.Tuning.Reserve is { } rule ? ToMilliRp(rule.CapTurns * HolderResearchPoints(world, content, holder)) : 0L;

    /// <summary>
    /// ADR-051 — THE DEPOSIT of a step whose technology lane funds nothing, milli-RP: the step's pool (floor, milli-RP) up to
    /// the room left under <see cref="ReserveCapMilli"/>; 0 when the reserve is at or above the cap, and with the switch
    /// off. What is not deposited is lost (D-044 R20-D). The one function ResearchSystem deposits with and the UI shows.
    /// </summary>
    public static long ReserveDepositMilli(IReadOnlyWorldState world, ResearchContent content, PolityId holder)
    {
        if (content.Tuning.Reserve is not { } rule) return 0L;
        double pool = HolderResearchPoints(world, content, holder);
        long room = ToMilliRp(rule.CapTurns * pool) - ReserveMilli(world, holder);
        return room > 0L ? Math.Min(ToMilliRp(pool), room) : 0L;
    }

    /// <summary>
    /// ADR-051 — THE DRAW of a step whose technology lane funds its own technology node, milli-RP: min(reserve,
    /// floor(1000 × drawTurns × pool)); 0 with the switch off. The whole draw leaves the reserve; it is spent by the pool's
    /// own rules and whatever the funding cannot absorb is lost (LOSSY — never refunded). The one function ResearchSystem
    /// draws with and the UI shows.
    /// </summary>
    public static long ReserveDrawMilli(IReadOnlyWorldState world, ResearchContent content, PolityId holder)
    {
        if (content.Tuning.Reserve is not { } rule) return 0L;
        long reserve = ReserveMilli(world, holder);
        return reserve > 0L ? Math.Min(reserve, ToMilliRp(rule.DrawTurns * HolderResearchPoints(world, content, holder))) : 0L;
    }

    /// <summary>
    /// ADR-051 — DISPLAY ONLY: the holder's reserve as the research surfaces show it, or null with tuning.researchReserve
    /// absent. What the NEXT step does with it (no new order) follows the step's own rule: the technology lane funds nothing
    /// → <see cref="ReserveAction.Deposit"/> (<see cref="ReserveDepositMilli"/>); it funds its own technology node →
    /// <see cref="ReserveAction.Draw"/> (<see cref="ReserveDrawMilli"/>); it assists the other tree or funds a civic (one-pool
    /// model) → <see cref="ReserveAction.None"/>. RP are per turn (ADR-030), so no figure here depends on the step's dt.
    /// </summary>
    public static ReserveStatus? Reserve(IReadOnlyWorldState world, ResearchContent content, PolityId holder)
    {
        if (content.Tuning.Reserve is null) return null;
        double pool = HolderResearchPoints(world, content, holder);
        long reserve = ReserveMilli(world, holder), cap = ReserveCapMilli(world, content, holder);
        int funded = TechnologyLaneFunds(world, content, holder, out bool assisting);
        if (funded < 0)
            return new ReserveStatus(pool, reserve, cap, ReserveAction.Deposit, ReserveDepositMilli(world, content, holder));
        if (assisting || content.Nodes[funded].Tree != ResearchTree.Technology)
            return new ReserveStatus(pool, reserve, cap, ReserveAction.None, 0L);
        return new ReserveStatus(pool, reserve, cap, ReserveAction.Draw, ReserveDrawMilli(world, content, holder));
    }

    /// <summary>Display only: the node the technology lane (the one-pool model's one lane) funds on the next step with no new
    /// order, or −1 — <see cref="Route"/>'s current node with destination routing, else the available target.</summary>
    private static int TechnologyLaneFunds(IReadOnlyWorldState world, ResearchContent content, PolityId holder, out bool assisting)
    {
        assisting = false;
        bool twoLanes = content.Tuning.TwoLanes;
        if (content.Tuning.DestinationRouting)
        {
            foreach (ResearchTree lane in twoLanes ? [ResearchTree.Technology] : new[] { ResearchTree.Technology, ResearchTree.Civics })
            {
                ResearchRoute route = Route(world, content, holder, lane);
                if (route.Current is not { } current) continue;
                assisting = route.Assisting;
                return content.IndexOf(current);
            }
            return -1;
        }
        bool found = twoLanes
            ? TryGetTarget(world, content, holder, ResearchTree.Technology, out ResearchNodeId t)
            : TryGetTarget(world, holder, out t);
        int index = found ? content.IndexOf(t) : -1;
        return index >= 0 && AvailableMask(world, content, holder)[index] ? index : -1;
    }

    /// <summary>ADR-051: the ResearchReserves table as only ResearchSystem writes it, or null: rows in strictly ascending
    /// Polity (so no holder twice) and ReserveMilli ≥ 0. Snapshot.Load refuses a save that breaks it.</summary>
    public static string? ReservesInvariantViolation(IReadOnlyTable<ResearchReserveRow> rows)
    {
        for (int i = 0; i < rows.Count; i++)
        {
            string at = "ResearchReserves[" + i.ToString(System.Globalization.CultureInfo.InvariantCulture) + "]";
            if (i > 0 && rows[i - 1].Polity.Value >= rows[i].Polity.Value)
                return at + ": rows are not in strictly ascending Polity order";
            if (rows[i].ReserveMilli < 0) return at + ": ReserveMilli is negative";
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

/// <summary>What a research lane's points do on the next step (<see cref="ResearchQuery.Activity"/>; display only).</summary>
public enum LaneActivity
{
    /// <summary>No destination and no assist: the lane funds nothing (RP lost; CP banked).</summary>
    Idle = 0,
    /// <summary>The lane funds its destination, or a node of its route toward it.</summary>
    Researching = 1,
    /// <summary>The lane funds a node of its own tree on the other lane's route (free-lane assist).</summary>
    Assisting = 2,
    /// <summary>The lane holds a destination but no node of its route is fundable now: it funds nothing (RP lost; CP banked).</summary>
    Waiting = 3,
}

/// <summary>ADR-051: what the next step does with a holder's research reserve (display only).</summary>
public enum ReserveAction
{
    /// <summary>The technology lane funds nothing: the step's RP go to the reserve, up to the cap.</summary>
    Deposit = 0,
    /// <summary>The technology lane funds its own technology node: up to drawTurns × the pool is drawn.</summary>
    Draw = 1,
    /// <summary>The lane assists the other tree, or funds a civic: the reserve is neither drawn nor filled.</summary>
    None = 2,
}

/// <summary>
/// ADR-051 — a holder's research reserve as the surfaces show it (<see cref="ResearchQuery.Reserve"/>): the RP pool a turn,
/// the reserve and its cap (milli-RP), and what the next step does (<see cref="Action"/>, <see cref="AmountMilli"/> milli-RP
/// deposited or drawn). The phrases below are the one wording every research surface uses.
/// </summary>
public sealed record ReserveStatus(double Pool, long ReserveMilli, long CapMilli, ReserveAction Action, long AmountMilli)
{
    private static string N1(double v) => v.ToString("#,0.#", System.Globalization.CultureInfo.InvariantCulture);

    /// <summary>"reserve Y of Z RP".</summary>
    public string Holding => "reserve " + N1(ReserveMilli / 1000.0) + " of " + N1(CapMilli / 1000.0) + " RP";

    /// <summary>For a lane funding nothing: "X RP go to the reserve (Y of Z)", "reserve full - X RP unused this turn", or for a
    /// partial room "D RP go to the reserve (Y of Z), U RP unused this turn".</summary>
    public string Unused
    {
        get
        {
            double deposit = AmountMilli / 1000.0, unused = Math.Max(0.0, Pool - deposit);
            string room = " (" + N1(ReserveMilli / 1000.0) + " of " + N1(CapMilli / 1000.0) + ")";
            if (AmountMilli <= 0L) return "reserve full - " + N1(Pool) + " RP unused this turn";
            return N1(deposit) + " RP go to the reserve" + room + (unused >= 0.05 ? ", " + N1(unused) + " RP unused this turn" : "");
        }
    }

    /// <summary>For a funding lane: "+W RP from the reserve this turn · reserve Y of Z RP", or the holding alone; an assisting
    /// lane says the reserve is not drawn.</summary>
    public string Funding => Action switch
    {
        ReserveAction.Draw when AmountMilli > 0L => "+" + N1(AmountMilli / 1000.0) + " RP from the reserve this turn · " + Holding,
        ReserveAction.None => Holding + " (not drawn for the other tree)",
        _ => Holding,
    };
}
