
namespace Sim.Core.State;

/// <summary>
/// R3 (Director R2-final §0A, §2, §4) — KNOWLEDGE NEVER DECAYS; POLITICAL CHANGE MOVES IT, NEVER DESTROYS IT.
///
/// Knowledge is the completed-node relation (<see cref="ResearchCompletedRow"/>, ADR-029: row presence is the
/// fact, rows are never removed). This is the ONE domain operation every political transition uses on it:
/// <see cref="MergeInto"/> appends to <paramref name="to"/>'s knowledge every completed node
/// <paramref name="from"/> holds that <paramref name="to"/> lacks — a set UNION — and removes nothing, from either
/// holder. Two rulings are the same call:
/// <list type="bullet">
/// <item>SEPARATION (revolt, §2): the new polity has no knowledge yet, so the union IS the complete copy of the
/// former polity's knowledge at that instant. Only that instant is copied; afterwards the two holders share no
/// row and research independently (no link is kept, so nothing flows later in either direction).</item>
/// <item>ANNEXATION (§4): new owner = union(own, annexed). No annexation or conquest path exists in the
/// simulation yet (M6 war / later politics — DEFERRED), so this operation is the merge such a path must call; it
/// is tested at this level and invented nowhere else.</item>
/// </list>
/// KNOWLEDGE = COMPLETED NODES ONLY (INFERRED, R3): in-progress RP (<see cref="ResearchProgressRow"/>), fired
/// Eurekas and acceleration-credit provenance are research EFFORT, not knowledge, and are not copied or merged; the
/// donor keeps its own untouched. Appended rows keep the donor's completion order, so the result is a pure
/// function of the table (array walk, no set, no hash — law 5).
/// </summary>
public static class KnowledgeTransfer
{
    /// <summary>Union <paramref name="from"/>'s completed nodes into <paramref name="to"/>. Returns how many rows
    /// were appended. Never removes or reorders a row; a holder merged into itself is a no-op.</summary>
    public static int MergeInto(Table<ResearchCompletedRow> completed, PolityId from, PolityId to)
    {
        ArgumentNullException.ThrowIfNull(completed);
        if (from.Value == to.Value) return 0;
        int count = completed.Count;   // the rows present at the instant of transfer
        int added = 0;
        for (int i = 0; i < count; i++)
        {
            ResearchCompletedRow row = completed[i];
            if (row.Polity.Value != from.Value) continue;
            if (Holds(completed, to, row.Node)) continue;
            completed.Add(new ResearchCompletedRow(to, row.Node));
            added++;
        }
        return added;
    }

    /// <summary>Whether <paramref name="holder"/> has completed <paramref name="node"/>.</summary>
    public static bool Holds(IReadOnlyTable<ResearchCompletedRow> completed, PolityId holder, ResearchNodeId node)
    {
        for (int i = 0; i < completed.Count; i++)
            if (completed[i].Polity.Value == holder.Value && completed[i].Node.Value == node.Value) return true;
        return false;
    }
}
