using Sim.Core.Systems;

namespace Sim.Core.State;

/// <summary>
/// D-055 — MATERIAL RESERVATIONS ACROSS TURNS ("holds"; the Director's batch-4 instruction, item 1: projects must not
/// consume resources reserved by other projects, which also resolves D-054 Q17). This file holds the switch predicate,
/// the purse-key encoding and the structural invariant of the published table (<see cref="ConstructionHoldRow"/>, schema
/// v34 section 61). The hold rule itself — which heads hold, in what order, and how much — is ConstructionQuery's.
///
/// THE SWITCH is sim.json construction.reservations (shipped true). The loader refuses it unless construction.incremental
/// is true (a hold is the remaining need of a part-built head; under M4-D's whole-or-wait gate there is no partial state
/// to hold for). <see cref="Enabled"/> is false when the switch is off, absent, or incremental construction is off — the
/// attribution control is TestConfigs.PreReservations, never a gameplay setting. Construction CLEARS the table at the
/// start of every step whatever the switch says, so a world construction steps with the switch off never carries a hold
/// row. A loaded or carried table is never read before construction rebuilds it: road development's read is a declared
/// same-step edge (D-056, SystemRegistration.ReadsThisStepFrom) and TurnExecutor refuses a pipeline that runs road
/// development before construction while this rule applies (the shipped order is pinned by
/// ConstructionHoldsSchemaV34Tests.TheShippedPipeline_RunsConstructionBeforeRoadDevelopment); with the switch off road
/// development ignores the table in any order.
///
/// THE PURSE KEY (an int, so the table's key compares as integers): a polity id ≥ 0 names an Empire purse; −1 −
/// settlementId (always &lt; 0) names a single-settlement purse (purse-off, a stateless settlement, a world with no control
/// rows). The two ranges cannot meet.
/// </summary>
public static class Reservations
{
    /// <summary>Whether the hold rule applies: sim.json construction.reservations, with incremental construction on (any
    /// other combination built in code leaves it inert; the loader refuses reservations without incremental).</summary>
    public static bool Enabled(SimConfig cfg) => cfg.Construction is { Reservations: true, Incremental: true };

    /// <summary>The purse key of the Empire purse of <paramref name="polity"/>: its id (≥ 0).</summary>
    public static int PolityPurse(PolityId polity) =>
        polity.Value >= 0 ? polity.Value
            : throw new ArgumentOutOfRangeException(nameof(polity), polity.Value, "a polity id is never negative.");

    /// <summary>The purse key of <paramref name="settlement"/>'s single-settlement purse: −1 − its id (&lt; 0).</summary>
    public static int SettlementPurse(SettlementId settlement) =>
        settlement.Value >= 0 ? -1 - settlement.Value
            : throw new ArgumentOutOfRangeException(nameof(settlement), settlement.Value, "a settlement id is never negative.");

    /// <summary>The (Purse, Good) key order of the table: ascending Purse, then ascending Good — explicit integer
    /// comparisons, never a hash or a culture.</summary>
    public static int CompareKeys(ConstructionHoldRow a, ConstructionHoldRow b)
    {
        int c = a.Purse.CompareTo(b.Purse);
        return c != 0 ? c : a.Good.CompareTo(b.Good);
    }

    /// <summary>
    /// The structural invariant of a published ConstructionHolds table — what Snapshot.Load refuses (SnapshotFormatException)
    /// and ConstructionSystem asserts at the end of every step: rows in STRICTLY ascending (Purse, Good) — so unsorted rows
    /// and a duplicate key are both refused; every Units &gt; 0; every Good ≥ 0. Content-free: the goods-registry bound is
    /// checked where content is available (construction's end-of-step invariant). Null when it holds.
    /// </summary>
    public static string? InvariantViolation(IReadOnlyTable<ConstructionHoldRow> holds)
    {
        for (int i = 0; i < holds.Count; i++)
        {
            ConstructionHoldRow row = holds[i];
            if (row.Good < 0)
                return string.Create(System.Globalization.CultureInfo.InvariantCulture,
                    $"construction hold row {i} (purse {row.Purse}) names good {row.Good}; a good id is never negative");
            if (row.Units <= 0)
                return string.Create(System.Globalization.CultureInfo.InvariantCulture,
                    $"construction hold row {i} (purse {row.Purse}, good {row.Good}) sets aside {row.Units} units; a hold row exists only for a positive amount");
            if (i > 0)
            {
                int order = CompareKeys(holds[i - 1], row);
                if (order == 0)
                    return string.Create(System.Globalization.CultureInfo.InvariantCulture,
                        $"two construction hold rows for purse {row.Purse}, good {row.Good}");
                if (order > 0)
                    return string.Create(System.Globalization.CultureInfo.InvariantCulture,
                        $"construction hold rows out of order at row {i}: (purse {holds[i - 1].Purse}, good {holds[i - 1].Good}) before (purse {row.Purse}, good {row.Good}); rows are in ascending (purse, good)");
            }
        }
        return null;
    }
}
