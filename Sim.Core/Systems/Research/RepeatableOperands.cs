using Sim.Core.State;

namespace Sim.Core.Systems.Research;

/// <summary>One row of the tranche-2 operand registry as the repeatable-levels track reads it (ADR-049): the operand id, its
/// direction (<see cref="Higher"/>: value = base × (1 + Σ); otherwise value = base × (1 − Σ)), the cap on Σ, and the
/// player-facing meaning "when improved".</summary>
public sealed record RepeatableOperand(string Id, bool Higher, double Cap, string Meaning);

/// <summary>
/// ADR-049 — THE LEGAL OPERAND IDS OF A REPEATABLE LEVEL EFFECT: a view of the one operand registry
/// (State.KnowledgeOperands, ADR-047 — the tranche-2 contract §1.1 table, row for row) with each row's direction, cap and
/// meaning. The loader validates every <c>repeatable.effects[].operand</c> against <see cref="IndexOf"/>; the UI reads the
/// cap and meaning to show a level's benefit. Pure data: no system reads a value from here.
/// </summary>
public static class RepeatableOperands
{
    public static readonly IReadOnlyList<RepeatableOperand> All = Derive();

    private static RepeatableOperand[] Derive()
    {
        IReadOnlyList<KnowledgeOperandInfo> rows = KnowledgeOperands.All;
        var all = new RepeatableOperand[rows.Count];
        for (int i = 0; i < all.Length; i++)
            all[i] = new RepeatableOperand(rows[i].Id, rows[i].Direction == OperandDirection.Higher, rows[i].Cap, rows[i].Meaning);
        return all;
    }

    /// <summary>The registry row index of <paramref name="id"/> (ordinal), or -1. A linear scan over a fixed array — no
    /// dictionary (law 5).</summary>
    public static int IndexOf(string id)
    {
        for (int i = 0; i < All.Count; i++)
            if (string.Equals(All[i].Id, id, StringComparison.Ordinal)) return i;
        return -1;
    }

    /// <summary>The registry row of <paramref name="id"/>; throws for an id outside the registry.</summary>
    public static RepeatableOperand Get(string id) =>
        IndexOf(id) is int i and >= 0 ? All[i] : throw new ArgumentException($"'{id}' is not a tranche-2 operand id.", nameof(id));
}
