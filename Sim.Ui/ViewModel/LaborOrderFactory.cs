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
    /// <summary>The UI's actor id in order logs (single human director in M1).</summary>
    public const int UiActorId = 1;

    public static OrderRecord Create(long currentTurn, SettlementId settlement, int farmPct)
    {
        if (farmPct is < 0 or > 100)
            throw new ArgumentOutOfRangeException(nameof(farmPct), farmPct, "farm percentage is 0..100");
        // Turn semantics (§3.9): an order with Turn = t is delivered to the step
        // executing FROM turn-t state — i.e. the very next End Turn press.
        return new OrderRecord(currentTurn, UiActorId, OrderKind.LaborAllocation,
            settlement.Value, farmPct);
    }
}
