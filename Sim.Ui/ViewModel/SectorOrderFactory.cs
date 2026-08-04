using System.Globalization;
using Sim.Core.Kernel;
using Sim.Core.State;

namespace Sim.Ui.ViewModel;

/// <summary>
/// T3.9b: builds the five-sector allocation BATCH the HUD emits, replacing the
/// farm-% slider's single blunt order (which zeroed herding, extraction,
/// crafting AND construction together — the gate session's 100/0/0/0/0).
///
/// THE ORDER CARRIES RAW WEIGHTS; THE SIM READS NORMALIZED SHARES. D-032's
/// order payload is a weight percentage per sector and every consumer divides
/// by the row's sum (<see cref="Sectors.Share"/>), so the five shares total
/// 1.0 BY CONSTRUCTION IN THE CONSUMER — not by any constraint on the order.
/// This is why the weights are submitted AS TYPED rather than normalized on
/// submit: normalizing here would make the order log record something other
/// than what the director ordered, and the log is replay evidence. What the
/// panel shows instead is <see cref="Preview"/> — the shares that WILL be
/// applied, computed by this same file through the sim's own Sectors.Share, so
/// "as applied" cannot drift from "as sent".
///
/// PURE view-model: ints, doubles, strings and OrderRecords — no MonoGame or
/// ImGui types, headless-testable like LaborOrderFactory.
/// </summary>
public static class SectorOrderFactory
{
    /// <summary>The UI's actor id in order logs — the same single human
    /// director as the legacy labor order (LaborOrderFactory.UiActorId).</summary>
    public const int UiActorId = LaborOrderFactory.UiActorId;

    /// <summary>
    /// D-032 target packing, shared by the encoder and the tests so the
    /// shift width lives in exactly one place: settlementId × 8 + sectorId,
    /// decoded by PathBuildSystem as <c>&gt;&gt; 3</c> / <c>&amp; 7</c>.
    /// </summary>
    public static int PackTarget(SettlementId settlement, int sector) => settlement.Value * 8 + sector;

    /// <summary>
    /// The allocation as it WILL BE APPLIED: the sim's own normalization over
    /// the typed weights. Weights are divided by 100 exactly as
    /// PathBuildSystem stores them, then normalized by Sectors.Share — the
    /// same operations in the same order, so the preview matches the row the
    /// sim will hold. An all-zero vector previews as five zeroes (Sectors.Share
    /// returns 0.0 for a zero-sum row rather than NaN); it is refused by
    /// <see cref="Create"/> rather than silently sent.
    /// </summary>
    public static IReadOnlyList<SectorBarRow> Preview(SettlementId settlement, ReadOnlySpan<int> weights)
    {
        RequireShape(weights);
        var row = new SectorAllocationRow(settlement, 0.0, 0.0, 0.0, 0.0, 0.0);
        for (int s = 0; s < Sectors.Count; s++) row = Sectors.With(row, s, weights[s] / 100.0);
        return SectorBarModel.Rows(row);
    }

    /// <summary>True when the weights can be submitted (shape legal, sum positive).</summary>
    public static bool CanSubmit(ReadOnlySpan<int> weights)
    {
        if (weights.Length != Sectors.Count) return false;
        long sum = 0;
        for (int s = 0; s < weights.Length; s++)
        {
            if (weights[s] is < 0 or > 100) return false;
            sum += weights[s];
        }
        return sum > 0;
    }

    /// <summary>
    /// The five orders, one per sector, all stamped with the CURRENT turn.
    /// Turn semantics (§3.9, unchanged from the labor order): an order with
    /// Turn = t is delivered to the step executing FROM turn-t state — the very
    /// next End Turn press. Issued in ascending sector id, which is also the
    /// order PathBuildSystem applies them in (log order, last write wins).
    /// </summary>
    public static IReadOnlyList<OrderRecord> Create(
        long currentTurn, SettlementId settlement, ReadOnlySpan<int> weights)
    {
        RequireShape(weights);

        long sum = 0;
        for (int s = 0; s < Sectors.Count; s++)
        {
            if (weights[s] is < 0 or > 100)
                throw new ArgumentOutOfRangeException(nameof(weights), weights[s],
                    $"sector weight for {SectorBarModel.SectorNames[s]} is 0..100");
            sum += weights[s];
        }
        // THE Σ = 0 GUARD. An all-zero allocation normalizes to five zero
        // shares: every sector pool empty, the settlement silently does
        // nothing, and no test or log line says so. The sim survives it
        // (Sectors.Share returns 0.0, never NaN) which is exactly what makes it
        // dangerous — it is the config-fails-quietly class as an ORDER. Refused
        // at the source, actionably, rather than recorded and obeyed.
        if (sum <= 0)
            throw new ArgumentException(
                "an all-zero sector allocation would put every sector pool at zero — the settlement " +
                "would work at nothing and the order log would not say so. Give at least one sector " +
                "a positive weight (the derived subsistence default is 55/15/10/12/8).",
                nameof(weights));

        var orders = new OrderRecord[Sectors.Count];
        for (int s = 0; s < Sectors.Count; s++)
        {
            orders[s] = new OrderRecord(
                currentTurn, UiActorId, OrderKind.SectorAllocation,
                PackTarget(settlement, s), weights[s]);
        }
        return orders;
    }

    /// <summary>One line stating the applied split, for the panel's preview
    /// text — invariant-culture, preformatted like every other HUD string.</summary>
    public static string PreviewLine(SettlementId settlement, ReadOnlySpan<int> weights)
    {
        IReadOnlyList<SectorBarRow> preview = Preview(settlement, weights);
        var parts = new string[preview.Count];
        for (int i = 0; i < preview.Count; i++)
            parts[i] = string.Create(CultureInfo.InvariantCulture, $"{preview[i].Fraction * 100.0:F0}%");
        return "applies as " + string.Join(" / ", parts);
    }

    private static void RequireShape(ReadOnlySpan<int> weights)
    {
        if (weights.Length != Sectors.Count)
            throw new ArgumentException(
                $"a sector allocation is exactly {Sectors.Count} weights (farming..construction), " +
                $"got {weights.Length}.", nameof(weights));
    }
}
