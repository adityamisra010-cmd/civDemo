using Sim.Core.Systems.Research;

namespace Sim.Core.State;

/// <summary>
/// ADR-047 (tranche-2 contract §1) — WHAT COMPLETED RESEARCH DOES TO A LIVE EQUATION. Pure, read-only, no state, no
/// cache: the one function every owning system reads its operands through.
///
/// <para><b>The rule.</b> For a knowledge holder with completed set S and an operand op (<see cref="KnowledgeOperands"/>):
/// Σ = Σ delta over the nodes of S that declare a realized effect on op (research.json <c>effects.realized</c>), plus
/// the repeatable levels (<see cref="LevelContribution"/> — 0 until the levels track lands); Σ is clamped to [0, cap];
/// a <c>higher</c> operand reads base × (1 + Σ), a <c>lower</c> one base × (1 − Σ) (cap &lt; 1); an integer operand is
/// rounded DOWN after scaling; where the registry states a result bound the value is clamped to it (never below the
/// base). Σ = 0 returns the base EXACTLY — the same double, bit for bit.</para>
///
/// <para><b>The holder.</b> A polity reads its own rows; a SETTLEMENT reads the holder that knows for it
/// (<see cref="SettlementKnowledge.HolderOf"/>: its controlling Empire, else its own local key). Effects never leak to
/// another holder.</para>
///
/// <para><b>The switch.</b> research.json <c>tuning.knowledgeEffects</c>. Off — or no research content — and every
/// operand is its base exactly, so the world is byte-identical to the pre-ADR-047 build (the attribution twin
/// TestConfigs.PreKnowledgeEffects).</para>
///
/// <para><b>Order and cost.</b> Σ is summed over nodes in ascending dense index, so it is a function of the completed SET,
/// never of row order (law 5). Systems that read operands in per-settlement loops take a
/// <see cref="KnowledgeEffectsSnapshot"/> once per step (<see cref="Snapshot"/>): one pass over the completed rows, then
/// O(log holders) per lookup. The point functions below rescan the world per call — for queries outside the hot loops.</para>
/// </summary>
public static class KnowledgeEffects
{
    /// <summary>Whether realized effects are switched on for <paramref name="content"/> (false without content).</summary>
    public static bool IsOn(ResearchContent? content) => content is { Tuning.KnowledgeEffects: true };

    /// <summary>THE QUERY (contract §1): <paramref name="op"/>'s value for <paramref name="holder"/>, from
    /// <paramref name="baseValue"/>. Exactly <paramref name="baseValue"/> when the switch is off, there is no content, or
    /// the holder's Σ is 0.</summary>
    public static double Apply(
        IReadOnlyWorldState world, ResearchContent? content, PolityId holder, KnowledgeOperand op, double baseValue) =>
        Value(op, baseValue, Improvement(world, content, holder, op));

    /// <summary><see cref="Apply"/> for a SETTLEMENT: the holder is <see cref="SettlementKnowledge.HolderOf"/>.</summary>
    public static double ApplyAt(
        IReadOnlyWorldState world, ResearchContent? content, SettlementId settlement, KnowledgeOperand op, double baseValue)
    {
        // No node declares this operand (and no level can): the base, without resolving the holder.
        if (!IsOn(content) || (content!.OperandNodes[(int)op].Length == 0 && !LevelsPresent(world, content))) return baseValue;
        return Apply(world, content, SettlementKnowledge.HolderOf(world, settlement), op, baseValue);
    }

    /// <summary>The holder's CLAMPED improvement Σ ∈ [0, cap] on <paramref name="op"/> — 0 with the switch off.</summary>
    public static double Improvement(IReadOnlyWorldState world, ResearchContent? content, PolityId holder, KnowledgeOperand op)
    {
        if (!IsOn(content)) return 0.0;
        double raw = RealizedSum(world, content!, holder, op) + LevelContribution(world, content!, holder, op);
        return Clamp(op, raw);
    }

    /// <summary>The settlement's holder's clamped improvement on <paramref name="op"/>.</summary>
    public static double ImprovementAt(IReadOnlyWorldState world, ResearchContent? content, SettlementId settlement, KnowledgeOperand op)
    {
        if (!IsOn(content) || (content!.OperandNodes[(int)op].Length == 0 && !LevelsPresent(world, content))) return 0.0;
        return Improvement(world, content, SettlementKnowledge.HolderOf(world, settlement), op);
    }

    /// <summary>
    /// Steps 3–5 of the rule for an already-clamped improvement: higher → base × (1 + Σ), lower → base × (1 − Σ), the
    /// registry's result bound (never below the base), integer operands floored. Σ = 0 returns the base unchanged.
    /// </summary>
    public static double Value(KnowledgeOperand op, double baseValue, double improvement)
    {
        if (improvement == 0.0) return baseValue;
        KnowledgeOperandInfo info = KnowledgeOperands.Of(op);
        double v = info.Direction == OperandDirection.Higher ? baseValue * (1.0 + improvement) : baseValue * (1.0 - improvement);
        if (v > info.ResultMax) v = Math.Max(baseValue, info.ResultMax);
        if (info.Integer) v = Math.Floor(v);
        return v;
    }

    /// <summary>Step 2: Σ clamped to [0, cap] (NaN reads 0).</summary>
    public static double Clamp(KnowledgeOperand op, double raw)
    {
        if (!(raw > 0.0)) return 0.0;
        double cap = KnowledgeOperands.Of(op).Cap;
        return raw > cap ? cap : raw;
    }

    /// <summary>Σ delta over the holder's completed nodes with an effect on <paramref name="op"/>, ascending node index
    /// (unclamped). 0 when no node declares one — the common case, which reads no row.</summary>
    public static double RealizedSum(IReadOnlyWorldState world, ResearchContent content, PolityId holder, KnowledgeOperand op)
    {
        int[] nodes = content.OperandNodes[(int)op];
        if (nodes.Length == 0) return 0.0;
        double[] deltas = content.OperandDeltas[(int)op];
        bool[] completed = ResearchQuery.CompletedMask(world, content, holder);
        double sum = 0.0;
        for (int k = 0; k < nodes.Length; k++)
            if (completed[nodes[k]]) sum += deltas[k];
        return sum;
    }

    /// <summary>The number of completed rows (any holder) naming a node with a realized effect on <paramref name="op"/> — a
    /// monotone counter (completion is append-only) a derived-state system folds into its staleness key. 0 with the switch
    /// off or when no node declares such an effect (no row is read then).</summary>
    public static int CompletionsWith(IReadOnlyWorldState world, ResearchContent? content, KnowledgeOperand op)
    {
        if (!IsOn(content)) return 0;
        int[] nodes = content!.OperandNodes[(int)op];
        if (nodes.Length == 0) return 0;
        int count = 0;
        for (int i = 0; i < world.ResearchCompleted.Count; i++)
        {
            int index = content.IndexOf(world.ResearchCompleted[i].Node);
            if (index >= 0 && Array.BinarySearch(nodes, index) >= 0) count++;
        }
        return count;
    }

    // ------------------------------------------------------------------ the repeatable-levels seam (contract §4)

    /// <summary>
    /// THE REPEATABLE-LEVELS SEAM (contract §4): the improvement the holder's repeatable LEVELS add to Σ of
    /// <paramref name="op"/> — level n of a node adds cap × k × (1 − k)^(n−1). Tranche 2's levels track implements it
    /// here, and only here (with <see cref="LevelsPresent"/>, its fast-path companion); until then no level exists and it
    /// is 0. Both <see cref="Improvement"/> and <see cref="KnowledgeEffectsSnapshot"/> add it before the clamp.
    /// </summary>
    public static double LevelContribution(IReadOnlyWorldState world, ResearchContent content, PolityId holder, KnowledgeOperand op) => 0.0;

    /// <summary>Part of the levels seam: whether ANY holder may have a level contribution in <paramref name="world"/>
    /// (false until the levels track lands). Lets a snapshot with no realized rows skip holder resolution entirely.</summary>
    public static bool LevelsPresent(IReadOnlyWorldState world, ResearchContent content) => false;

    // ------------------------------------------------------------------ the per-step snapshot

    /// <summary>The per-step snapshot of every holder's realized Σ on <paramref name="world"/> (systems pass PREV). The
    /// inert snapshot (every lookup returns the base) when the switch is off or there is no content.</summary>
    public static KnowledgeEffectsSnapshot Snapshot(IReadOnlyWorldState world, ResearchContent? content)
    {
        if (!IsOn(content)) return KnowledgeEffectsSnapshot.Inert;
        ResearchContent c = content!;
        if (!c.HasRealizedEffects)
            return LevelsPresent(world, c) ? new KnowledgeEffectsSnapshot(world, c, [], []) : KnowledgeEffectsSnapshot.Inert;

        // (holder, node) pairs of completed rows whose node declares an effect, as one composite integer key
        // holder × 2^32 + node index — sorted (holder ascending, then node ascending), duplicates dropped.
        var keys = new List<long>();
        for (int i = 0; i < world.ResearchCompleted.Count; i++)
        {
            ResearchCompletedRow row = world.ResearchCompleted[i];
            int index = c.IndexOf(row.Node);
            if (index < 0 || c.Nodes[index].RealizedEffects.Count == 0) continue;
            keys.Add(((long)row.Polity.Value << 32) + index);
        }
        long[] sorted = [.. keys];
        Array.Sort(sorted);

        int ops = KnowledgeOperands.Count;
        var holders = new List<int>();
        var sums = new List<double>();
        long last = long.MinValue;
        foreach (long key in sorted)
        {
            if (key == last) continue;
            last = key;
            int holder = (int)(key >> 32);
            int node = (int)(key - ((long)holder << 32));
            if (holders.Count == 0 || holders[^1] != holder)
            {
                holders.Add(holder);
                for (int op = 0; op < ops; op++) sums.Add(0.0);
            }
            int at = (holders.Count - 1) * ops;
            foreach (RealizedEffect e in c.Nodes[node].RealizedEffects) sums[at + (int)e.Operand] += e.Delta;
        }
        if (holders.Count == 0 && !LevelsPresent(world, c)) return KnowledgeEffectsSnapshot.Inert;
        return new KnowledgeEffectsSnapshot(world, c, [.. holders], [.. sums]);
    }
}

/// <summary>
/// ADR-047 — every holder's realized Σ on one world, computed once (<see cref="KnowledgeEffects.Snapshot"/>) for the
/// per-settlement loops of a step. Immutable. Lookups binary-search a sorted holder array (no dictionary, law 5) and
/// return exactly what <see cref="KnowledgeEffects.Apply"/> returns on the same world — the same sums, added in the same
/// ascending node order.
/// </summary>
public sealed class KnowledgeEffectsSnapshot
{
    /// <summary>The inert snapshot: every lookup returns the base value; no holder is resolved.</summary>
    public static readonly KnowledgeEffectsSnapshot Inert = new(null, null, [], []);

    private readonly IReadOnlyWorldState? _world;
    private readonly ResearchContent? _content;
    private readonly int[] _holders;     // ascending holder ids with a non-empty realized set
    private readonly double[] _sums;     // [holderSlot × operandCount + operand], unclamped

    internal KnowledgeEffectsSnapshot(IReadOnlyWorldState? world, ResearchContent? content, int[] holders, double[] sums)
    {
        _world = world;
        _content = content;
        _holders = holders;
        _sums = sums;
    }

    /// <summary>Whether any lookup can differ from its base (false: every Apply returns the base).</summary>
    public bool IsActive => _content is not null;

    /// <summary>The holder's clamped improvement on <paramref name="op"/>.</summary>
    public double Improvement(PolityId holder, KnowledgeOperand op)
    {
        if (_content is null) return 0.0;
        int slot = Array.BinarySearch(_holders, holder.Value);
        double raw = slot >= 0 ? _sums[slot * KnowledgeOperands.Count + (int)op] : 0.0;
        raw += KnowledgeEffects.LevelContribution(_world!, _content, holder, op);
        return KnowledgeEffects.Clamp(op, raw);
    }

    /// <summary><paramref name="op"/>'s value for <paramref name="holder"/>.</summary>
    public double Apply(PolityId holder, KnowledgeOperand op, double baseValue) =>
        _content is null ? baseValue : KnowledgeEffects.Value(op, baseValue, Improvement(holder, op));

    /// <summary>The holder that knows for <paramref name="settlement"/> on the snapshot's world
    /// (<see cref="SettlementKnowledge.HolderOf"/>); its local key when inert (no controller scan).</summary>
    public PolityId HolderOf(SettlementId settlement) =>
        _content is null ? SettlementKnowledge.LocalHolder(settlement) : SettlementKnowledge.HolderOf(_world!, settlement);

    /// <summary><paramref name="op"/>'s value at <paramref name="settlement"/> (its holder's).</summary>
    public double ApplyAt(SettlementId settlement, KnowledgeOperand op, double baseValue) =>
        _content is null ? baseValue : Apply(HolderOf(settlement), op, baseValue);

    /// <summary>The settlement's holder's clamped improvement on <paramref name="op"/>.</summary>
    public double ImprovementAt(SettlementId settlement, KnowledgeOperand op) =>
        _content is null ? 0.0 : Improvement(HolderOf(settlement), op);
}
