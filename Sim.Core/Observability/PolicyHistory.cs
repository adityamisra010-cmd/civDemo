using Sim.Core.Kernel;
using Sim.Core.State;

namespace Sim.Core.Observability;

/// <summary>One sector's declared weight changing on one settlement — "declared
/// 10% → 15%, changed by Empire 1, order #104" verbatim (§4). Weights are the
/// RAW row values (order.Amount / 100, PathBuildSystem.cs:83-103), not the
/// normalized shares, because what the player declared is the raw weight.</summary>
public sealed record PolicyChange(
    long Turn,                        // the turn on whose state the new weight is first visible (next.Clock.Turn)
    double Year,                      // READ next.Clock.WorldDateYears
    int Settlement,
    int Sector,                       // Sectors.Farming..Construction
    double OldWeight,                 // prev row raw weight, or Sectors.Default raw when never ordered
    double NewWeight,                 // next row raw weight
    int Actor,                        // READ OrderRecord.ActorId of the attributed order; −1 when no order explains the change
    int OrderIndex);                  // the attributed order's position in the OrderLog; −1 when none

/// <summary>What was in force on a turn, for every settlement, every turn —
/// including turns with no change, so the player reads state, never infers it
/// from the last change (§4).</summary>
public sealed record PolicyState(
    long Turn,
    int Settlement,
    double[] Declared,                // READ next SectorAllocationRow raw weights (Sectors.Default raw when no row)
    double[] Effective);              // RECOMPUTED Sectors.Share on that same row

/// <summary>ADR-033 D4 — THE SECOND POLICY: one Empire's declared tax rate changing — "declared
/// 0% → 20%, changed by Empire 1, order #104" (§4). Keyed by POLITY, because the tax is an
/// Empire-level decision (<see cref="TaxPolicyRow"/>); the EFFECTIVE rate each settlement bears
/// (declared × the stored reach) is on its settlement record (<see cref="TaxBurdenReading"/>).
/// Rates are the RAW row fractions GovernanceSystem wrote (Amount / 100).</summary>
public sealed record TaxPolicyChange(
    long Turn,                        // the turn on whose state the new rate is first visible (next.Clock.Turn)
    double Year,                      // READ next.Clock.WorldDateYears
    int Polity,
    double OldRate,                   // READ prev TaxPolicyRow.Rate, 0 when the Empire never legislated
    bool OldRowPresent,
    double NewRate,                   // READ next TaxPolicyRow.Rate
    int Actor,                        // READ OrderRecord.ActorId of the attributed SetTaxRate; −1 when no order explains it
    int OrderIndex);                  // the attributed order's position in the OrderLog; −1 when none

/// <summary>ADR-033 D4: the declared tax rate in force for every roster Empire, every turn —
/// including turns with no change and Empires that never legislated (Declared 0, RowPresent
/// false), so the rate in force is read, never inferred.</summary>
public sealed record TaxPolicyState(
    long Turn,
    int Polity,
    double Declared,                  // READ next TaxPolicyRow.Rate (0 when no row — the never-legislated default)
    bool RowPresent);

/// <summary>
/// §4 — THE POLICY HISTORY, derived from the two authoritative sources and
/// nothing else: the ORDER LOG (what was declared, by whom, when; an order's
/// index in the log is its order number) and the per-turn
/// <see cref="SectorAllocationRow"/> (what was in force).
///
/// A change is DETECTED on the row (prev raw weight ≠ next raw weight, exact
/// double compare — the row holds exactly what Upsert wrote) and ATTRIBUTED to
/// the order applied this step that targets that settlement and that sector.
/// When several do, the LAST in log order wins, because PathBuildSystem applies
/// the batch in order and the last Upsert is the one that stands. A change with
/// no explaining order (impossible through the pipeline — only Upsert writes the
/// row — but reachable in a hand-built world) is recorded with Actor −1 and
/// OrderIndex −1 rather than dropped: a silent change is the defect this
/// history exists to make visible.
///
/// M4 has exactly one policy (D-032); M5's taxation (ADR-033 D4) is the second, in
/// the same two shapes — a change record and a per-turn state record — keyed by the
/// Empire rather than the settlement, because the tax is an Empire-level decision
/// (<see cref="ObserveTax"/>).
/// </summary>
public static class PolicyHistory
{
    /// <summary>
    /// ADR-033 D4: appends this step's TAX policy changes and states, one state per roster Empire
    /// in next.Polities order (an id listed twice is one Empire). A change is DETECTED on the row
    /// (prev raw rate or presence ≠ next, exact compare — the row holds exactly what
    /// GovernanceSystem wrote) and ATTRIBUTED to the LAST SetTaxRate of this step issued by the
    /// Empire for itself, because GovernanceSystem applies the batch in log order and the last
    /// valid order stands (one Empire's orders share one research gate on PREV, so the last one is
    /// the last valid one). A change no order explains — reachable only in a hand-built world — is
    /// recorded with Actor −1 and OrderIndex −1 rather than dropped.
    /// </summary>
    public static void ObserveTax(
        IReadOnlyWorldState prev, IReadOnlyWorldState next, OrderApplied[] orders,
        List<TaxPolicyChange> changes, List<TaxPolicyState> states)
    {
        var seen = new List<int>();
        for (int p = 0; p < next.Polities.Count; p++)
        {
            PolityId polity = next.Polities[p].Id;
            if (seen.Contains(polity.Value)) continue;
            seen.Add(polity.Value);

            double before = RateOrZero(prev.TaxPolicies, polity, out bool beforePresent);
            double after = RateOrZero(next.TaxPolicies, polity, out bool afterPresent);
            if (beforePresent != afterPresent
                || BitConverter.DoubleToInt64Bits(before) != BitConverter.DoubleToInt64Bits(after))
            {
                int actor = -1, index = -1;
                for (int o = 0; o < orders.Length; o++)
                {
                    OrderApplied order = orders[o];
                    if (order.Kind != OrderKind.SetTaxRate) continue;
                    if (order.Actor != polity.Value || order.TargetId != polity.Value) continue;
                    actor = order.Actor;     // last match in log order stands
                    index = order.Index;
                }
                changes.Add(new TaxPolicyChange(
                    next.Clock.Turn, next.Clock.WorldDateYears, polity.Value,
                    before, beforePresent, after, actor, index));
            }
            states.Add(new TaxPolicyState(next.Clock.Turn, polity.Value, after, afterPresent));
        }
    }

    /// <summary>The Empire's raw declared rate, or 0 when it never legislated.</summary>
    private static double RateOrZero(IReadOnlyTable<TaxPolicyRow> rows, PolityId polity, out bool present)
    {
        for (int i = 0; i < rows.Count; i++)
        {
            if (rows[i].Polity.Value != polity.Value) continue;
            present = true;
            return rows[i].Rate;
        }
        present = false;
        return 0.0;
    }

    /// <summary>Appends this step's changes and states. Settlements are walked in
    /// next.Settlements order (founded ones included: their state is Default until
    /// ordered), sectors ascending — a fixed integer order, no sort over doubles.</summary>
    public static void Observe(
        IReadOnlyWorldState prev, IReadOnlyWorldState next, OrderApplied[] orders,
        List<PolicyChange> changes, List<PolicyState> states)
    {
        for (int s = 0; s < next.Settlements.Count; s++)
        {
            SettlementId id = next.Settlements[s].Id;
            SectorAllocationRow before = RowOrDefault(prev.SectorAllocations, id, out _);
            SectorAllocationRow after = RowOrDefault(next.SectorAllocations, id, out _);

            var declared = new double[Sectors.Count];
            var effective = new double[Sectors.Count];
            for (int sector = 0; sector < Sectors.Count; sector++)
            {
                declared[sector] = Sectors.Raw(after, sector);
                effective[sector] = Sectors.Share(after, sector);

                double oldW = Sectors.Raw(before, sector);
                double newW = declared[sector];
                if (oldW == newW) continue;

                int actor = -1, index = -1;
                for (int o = 0; o < orders.Length; o++)
                {
                    OrderApplied order = orders[o];
                    if (order.Settlement != id.Value) continue;
                    bool setsThisSector = order.Kind == OrderKind.LaborAllocation
                        || (order.Kind == OrderKind.SectorAllocation && order.Sector == sector);
                    if (!setsThisSector) continue;
                    actor = order.Actor;     // last match in log order stands
                    index = order.Index;
                }
                changes.Add(new PolicyChange(
                    next.Clock.Turn, next.Clock.WorldDateYears, id.Value, sector,
                    oldW, newW, actor, index));
            }
            states.Add(new PolicyState(next.Clock.Turn, id.Value, declared, effective));
        }
    }

    /// <summary>The settlement's row, or <see cref="Sectors.Default"/> when it has
    /// never been ordered — the fallback every consumer applies.</summary>
    public static SectorAllocationRow RowOrDefault(
        IReadOnlyTable<SectorAllocationRow> rows, SettlementId id, out bool present)
    {
        for (int i = 0; i < rows.Count; i++)
        {
            if (rows[i].Settlement != id) continue;
            present = true;
            return rows[i];
        }
        present = false;
        return Sectors.Default(id);
    }
}
