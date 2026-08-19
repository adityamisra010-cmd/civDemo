using Sim.Core.Kernel;

namespace Sim.Core.State;

/// <summary>
/// T4.8 — THE THREE LIFECYCLE EVENTS OF A NOTABLE, EACH A LEDGER FLOW (R-1).
///
/// R-1 RULED Option B: "A NOTABLE IS A PERSON. Extracted from the bucket via
/// `Ledger.Transfer`; a conserved population stock with births, deaths and a
/// law-1 audit." T4.1's escalation then established the exact boundary, and
/// m4-spec §7 records it: "born, dies and defects are clean Ledger flows; IS
/// BOUGHT is not — the person moves, but the consideration is a SECOND flow, and
/// payment is money, which is M5."
///
/// So this file ships EXACTLY THREE operations. Purchase is absent on purpose,
/// not by oversight, and it is not expressible here: there is no money.
///
/// WHY A STATIC HELPER AND NOT A SYSTEM. Nothing in M4 decides WHEN a notable
/// should emerge — that trigger belongs to the aggrieved-bucket mechanism D-010
/// describes, and inventing one here would be a spawner nobody ratified. What M4
/// owes is the CONSERVATION SURFACE ("generals ship with the conservation surface
/// from day one, and the law-1 audit is part of the packet, not a follow-up"), so
/// these are pure functions over (Ledger, tables) that any future caller drives.
/// They register no pipeline slot and therefore change no existing world.
///
/// THE AUDIT PROPERTY, which is the packet's actual acceptance: every operation
/// below moves people through <see cref="Ledger"/> alone, so
/// Σ stocks + Σ sunk − Σ sourced = 0 holds across a notable's entire life. A
/// notable cannot be created from nothing, cannot vanish, and cannot be in two
/// places at once.
///
/// NOT HERE, DELIBERATELY: competence, traits, experience, BattleSetup,
/// BattleOutcome, or any resolver contract. Those are M6.
/// </summary>
public static class NotableLifecycle
{
    /// <summary>
    /// BORN — one person leaves a bucket and becomes a notable.
    ///
    /// The person is EXTRACTED, never copied: `Ledger.Transfer` moves exactly one
    /// unit of <see cref="ConservedQuantityIds.Population"/> out of the bucket and
    /// into the new notable's row, so the world's population is unchanged by the
    /// promotion. The notable remembers the cohort they came from, because they
    /// are still a person of that age and not an age-less token.
    ///
    /// Returns the new notable's row index, or −1 when the bucket is empty — an
    /// empty bucket cannot furnish a person, and refusing is the honest outcome
    /// (<see cref="OverdrawPolicy.Throw"/> would be a crash on a legal question).
    /// </summary>
    public static int Born(
        Ledger ledger, Table<BucketRow> buckets, Table<NotableRow> notables,
        int bucketRow, NotableId id, PolityId allegiance)
    {
        ArgumentNullException.ThrowIfNull(ledger);
        ArgumentNullException.ThrowIfNull(buckets);
        ArgumentNullException.ThrowIfNull(notables);
        if (bucketRow < 0 || bucketRow >= buckets.Count)
            throw new ArgumentOutOfRangeException(nameof(bucketRow), bucketRow, "no such bucket row");
        if (buckets[bucketRow].Count.Value <= 0) return -1; // nobody to promote

        BucketRow source = buckets[bucketRow];
        int row = notables.Add(new NotableRow(
            id, source.Settlement, allegiance, source.CohortIdx, Conserved.Zero));

        long moved = ledger.Transfer(
            ref buckets.Ref(bucketRow).Count, ref notables.Ref(row).Count,
            1, OverdrawPolicy.ClampToAvailable);

        // The bucket was non-empty and exactly one person was asked for, so the
        // clamp cannot bite; asserted rather than assumed because a slot holding
        // nobody would be a notable who is not a person.
        if (moved != 1)
            throw new InvalidOperationException(
                $"notable {id.Value}: promotion moved {moved} people, expected exactly 1");
        return row;
    }

    /// <summary>
    /// DIES — the person leaves the world.
    ///
    /// A SINK, not a transfer: nobody receives them. The reason is
    /// <see cref="ReasonIds.Deaths"/>, the same reason a bucket death records,
    /// because it is the same event — a person died. Inventing a separate
    /// "notable death" reason would split one audit question ("where did the
    /// people go?") across two answers for no gain.
    ///
    /// The row REMAINS at Count 0, vacated rather than deleted, exactly as an
    /// emptied bucket does: the identity is history, and removing rows would
    /// shift every later index under a table that other rows may reference.
    /// </summary>
    public static long Dies(Ledger ledger, Table<NotableRow> notables, int notableRow)
    {
        ArgumentNullException.ThrowIfNull(ledger);
        ArgumentNullException.ThrowIfNull(notables);
        if (notableRow < 0 || notableRow >= notables.Count)
            throw new ArgumentOutOfRangeException(nameof(notableRow), notableRow, "no such notable row");

        return ledger.Flow(
            ref notables.Ref(notableRow).Count, ConservedQuantityIds.Population,
            ReasonIds.Deaths, notables[notableRow].Count.Value,
            FlowDirection.Sink, OverdrawPolicy.ClampToAvailable);
    }

    /// <summary>
    /// DEFECTS — the same person, serving someone else, somewhere else.
    ///
    /// The identity travels and the PERSON MOVES: a new row is opened under the
    /// destination allegiance carrying the SAME <see cref="NotableId"/>, and
    /// `Ledger.Transfer` walks the person into it, leaving the old row vacated at
    /// Count 0. That is what R-1 bought — a defection that moves a real person and
    /// is therefore auditable, instead of a boolean flipping on a label.
    ///
    /// Returns the new row index, or −1 if the row holds nobody to defect.
    /// </summary>
    public static int Defects(
        Ledger ledger, Table<NotableRow> notables,
        int notableRow, SettlementId toSettlement, PolityId toAllegiance)
    {
        ArgumentNullException.ThrowIfNull(ledger);
        ArgumentNullException.ThrowIfNull(notables);
        if (notableRow < 0 || notableRow >= notables.Count)
            throw new ArgumentOutOfRangeException(nameof(notableRow), notableRow, "no such notable row");

        NotableRow from = notables[notableRow];
        if (from.Count.Value <= 0) return -1; // a vacated slot has nobody to defect

        int row = notables.Add(new NotableRow(
            from.Id, toSettlement, toAllegiance, from.CohortIdx, Conserved.Zero));

        ledger.Transfer(
            ref notables.Ref(notableRow).Count, ref notables.Ref(row).Count,
            from.Count.Value, OverdrawPolicy.Throw);
        return row;
    }

    /// <summary>The row currently HOLDING this notable, or −1 if none does — after
    /// a defection the identity appears on more than one row and only one of them
    /// has the person. Ascending scan, first match: deterministic, no dictionary.</summary>
    public static int LivingRowOf(IReadOnlyTable<NotableRow> notables, NotableId id)
    {
        ArgumentNullException.ThrowIfNull(notables);
        for (int i = 0; i < notables.Count; i++)
            if (notables[i].Id == id && notables[i].Count.Value > 0) return i;
        return -1;
    }
}
