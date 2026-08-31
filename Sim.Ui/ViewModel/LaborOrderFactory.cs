using Sim.Core.Kernel;
using Sim.Core.State;

namespace Sim.Ui.ViewModel;

/// <summary>
/// Builds the LaborAllocationOrder the HUD slider emits (T1.8, pure —
/// payload-exactness tested). Emitted ON RELEASE only, never per drag pixel:
/// order logs are the replay record and stay human-readable. The UI is the
/// order SOURCE; the sim only ever sees the log (m1 spec §3).
/// </summary>
public static class LaborOrderFactory
{
    /// <summary>
    /// The EMPIRE the human director commands — not a "player" marker (M4-B §3
    /// forbids that reading, and D-042 makes command source a property of the
    /// Empire row rather than of the actor id).
    ///
    /// The numeric value is unchanged from the id this UI has always written, so
    /// every existing order log and replay fixture keeps its meaning exactly. It
    /// is a STANDING DEFAULT, not a decision: nothing seeds a polity roster yet,
    /// so there is no registered Empire to read the human's from. When worldgen
    /// seeds one, this becomes a lookup and the constant goes.
    /// </summary>
    public static readonly PolityId PlayerEmpire = new(1);

    /// <summary>Back-compat alias for the raw id. Prefer <see cref="PlayerEmpire"/>.</summary>
    public const int UiActorId = 1;

    public static OrderRecord Create(long currentTurn, SettlementId settlement, int farmPct)
        => Create(currentTurn, PlayerEmpire, settlement, farmPct);

    /// <summary>As above, with the issuing Empire named explicitly.</summary>
    public static OrderRecord Create(
        long currentTurn, PolityId issuer, SettlementId settlement, int farmPct)
    {
        if (farmPct is < 0 or > 100)
            throw new ArgumentOutOfRangeException(nameof(farmPct), farmPct, "farm percentage is 0..100");
        // Turn semantics (§3.9): an order with Turn = t is delivered to the step
        // executing FROM turn-t state — i.e. the very next End Turn press.
        return OrderRecord.From(currentTurn, issuer, OrderKind.LaborAllocation,
            settlement.Value, farmPct);
    }
}
