using Sim.Core.Kernel;

namespace Sim.Core.Observability;

/// <summary>
/// T4.19 — THE TURN RECORD (docs/observability-architecture.md §2): one per
/// completed step, a pure function of (prev, next, cfg, ordersAppliedThisStep).
///
/// EVERY FIELD IS ONE OF THE FIVE §0 KINDS, and the kind is stated on the
/// field. Nothing here is a second implementation of a simulation formula: the
/// world-level flows are FIRST DIFFERENCES of the cumulative ledger rows the
/// Ledger itself wrote (<see cref="Sim.Core.State.LedgerFlowRow"/> is cumulative
/// for the whole run, so next − prev is this step's flow to the unit), the
/// stocks are plain SUMS over the conserved carriers, and the accounts below are
/// the conservation identity Σ stocks + Σ sunk − Σ sourced = 0 restated per step.
///
/// RECONCILIATION IS A TEST, NOT A HOPE. <c>Reconciles</c> is computed as an
/// EXACT long equality; when it fails <c>Discrepancy</c> carries the integer
/// remainder, never rounded away. A turn that does not reconcile is a
/// simulation defect — a conserved stock moved without a Ledger row — and the
/// record says so rather than smoothing it (§2).
///
/// These are plain records outside the determinism surface: never serialized
/// into WorldState, never read by a system, held by no pipeline object. They
/// are built AFTER the step from two worlds neither of which they retain.
/// </summary>
public sealed record TurnRecord(
    long Turn,                        // READ  next.Clock.Turn
    double Year,                      // READ  next.Clock.WorldDateYears
    double DtYears,                   // READ  next.Clock.DtYears (the dt the step integrated)
    StockRecord[] Stocks,             // one per conserved quantity present, ascending quantity id
    PopulationAccount Population,
    GrainAccount Grain,
    DwellingsAccount Dwellings,
    GoodAccount[] Goods,              // every NON-GRAIN good, registry order
    FlowsSummary Flows,
    OrderApplied[] Orders,            // READ  the log rows whose Turn == prev.Clock.Turn
    PolicyInForce[] Policy,           // RECOMPUTED Sectors.Share on prev SectorAllocations (Default when no row)
    CausesRecord Causes);

/// <summary>One (reason, units) leg of a world-level flow — DIFFERENCED from the
/// cumulative ledger row. <see cref="Name"/> is display only (ReasonIds carries no
/// names; <see cref="ReasonNames"/> is the observer's own table).</summary>
public readonly record struct FlowLeg(int Reason, string Name, long Units);

/// <summary>
/// The generic per-quantity account: Opening + ΣSources − ΣSinks == Closing.
/// Covers EVERY reason the ledger recorded for the quantity, so a reason the
/// named accounts below do not know about (a future sink) still reconciles here
/// — and the named account's discrepancy then points straight at it.
/// </summary>
public sealed record StockRecord(
    int Quantity,                     // ConservedQuantityId.Value
    string Name,                      // display: "population", "dwellings", or the good's registry name
    long Opening,                     // SUMMED prev carriers
    long Closing,                     // SUMMED next carriers
    FlowLeg[] Sources,                // DIFFERENCED ledger TotalSourced, ascending reason id
    FlowLeg[] Sinks,                  // DIFFERENCED ledger TotalSunk, ascending reason id
    bool Reconciles,                  // Opening + ΣSources − ΣSinks == Closing, EXACTLY
    long Discrepancy);                // Closing − (Opening + ΣSources − ΣSinks); 0 when it reconciles

/// <summary>Population: carriers are Buckets AND Notables (both hold
/// ConservedQuantityIds.Population). Nothing in the shipped pipeline calls
/// NotableLifecycle, so the Notables term is summed and stays zero — stated so a
/// reader does not mistake the zero for an omission.</summary>
public sealed record PopulationAccount(
    long Opening, long Births, long NaturalDeaths, long Starvation, long Closing,
    bool Reconciles, long Discrepancy);

/// <summary>Grain: Opening + Endowment + Harvest − Eaten − Spoilage − Overflow == Closing.
/// Endowment (InitialEndowment, reason 2) is sourced only at world founding,
/// before any step, so it is 0 on every step in the shipped pipeline; it is
/// carried so a founding-time source can never make a step "not reconcile".</summary>
public sealed record GrainAccount(
    long Opening, long Endowment, long Harvest, long Eaten, long Spoilage, long Overflow, long Closing,
    bool Reconciles, long Discrepancy);

/// <summary>Dwellings (ConservedQuantityIds.Dwellings, carried by HousingRow):
/// Opening + Built − Decayed == Closing.</summary>
public sealed record DwellingsAccount(
    long Opening, long Built, long Decayed, long Closing, bool Reconciles, long Discrepancy);

/// <summary>One non-grain good: Opening + Produced − InputsConsumed − ToolWear −
/// Eaten − HousingMaterials − ConstructionMaterials == Closing. Eaten is non-zero
/// only for the food goods (livestock, fish); the others sit at zero.</summary>
public sealed record GoodAccount(
    int Good, string Name,
    long Opening, long Produced, long InputsConsumed, long ToolWear, long Eaten,
    long HousingMaterials, long ConstructionMaterials, long Closing,
    bool Reconciles, long Discrepancy);

/// <summary>
/// The step's cross-settlement movements. <see cref="UnattributedGrainTransfer"/>
/// is the HONEST appropriation detector (§3): a settlement whose store-loss
/// residual is NEGATIVE on a turn it was not founded received grain no row
/// records — the only shipped mechanism that does that is an appropriation raid
/// (AppropriationSystem owns no table, §8 gap 4). It is detected by the identity
/// failing per settlement, never asserted absent by re-deriving the raid rule.
/// </summary>
public sealed record FlowsSummary(
    long MigrantsMoved,               // SUMMED next.MigrationFlows Inflow
    int SettlementsFounded,           // DIFFERENCED next.Settlements.Count − prev.Settlements.Count
    int ControlLost,                  // ControlRows present in prev and absent in next (same Polity, Place)
    long TradeUnits,                  // SUMMED next.TradeFlows Quantity
    int TradeFlowCount,               // READ next.TradeFlows.Count
    bool UnattributedGrainTransfer);  // any non-founded settlement with StoreLosses < 0

/// <summary>Effective sector shares IN FORCE for one settlement during the step:
/// Sectors.Share on the PREV row, which is the row every consumer (Production,
/// Housing, PathBuild) read this step; Sectors.Default when the settlement has
/// never been ordered — the same fallback those consumers apply
/// (ProductionSystem.cs:148, HousingSystem.cs:146, PathBuildSystem.cs:125).</summary>
public sealed record PolicyInForce(int Settlement, double[] Shares);

/// <summary>
/// The exact-by-construction causes: with the accounts reconciled, the change in
/// a stock IS its named flows and nothing else. Both deltas are DIFFERENCED
/// totals; both "explained" terms are sums of the account's flow legs. When the
/// account reconciles they are equal — that is the identity, not a finding.
/// </summary>
public sealed record CausesRecord(
    long PopulationDelta,             // Closing − Opening
    long PopulationExplained,         // Births − NaturalDeaths − Starvation
    long GrainDelta,                  // Closing − Opening
    long GrainExplained);             // Endowment + Harvest − Eaten − Spoilage − Overflow

/// <summary>
/// One order APPLIED this step, with its position in the order log. The delivery
/// rule is TurnExecutor.Step: <c>_orders?.BatchFor(prev.Clock.Turn)</c> — an
/// order with Turn == t is delivered to the step that transforms turn-t state
/// into turn-(t+1) state (OrderLog.cs, OrderRecord doc; TurnExecutor.cs).
/// <see cref="Index"/> is the record's index in the OrderLog: its order number.
/// </summary>
public readonly record struct OrderApplied(
    int Index, long Turn, int Actor, OrderKind Kind, int TargetId, double Amount)
{
    /// <summary>The settlement the order targets, decoded per kind (OrderKind
    /// doc): SectorAllocation packs settlement × 8 + sector; the others carry
    /// the settlement id directly. SetRainBias targets a region: −1.</summary>
    public int Settlement => Kind switch
    {
        OrderKind.SectorAllocation => TargetId >> 3,
        OrderKind.LaborAllocation => TargetId,
        OrderKind.EnqueueConstruction => TargetId,
        _ => -1,
    };

    /// <summary>The sector a SectorAllocation order sets; −1 for every other kind.</summary>
    public int Sector => Kind == OrderKind.SectorAllocation ? TargetId & 7 : -1;

    /// <summary>
    /// The rows of <paramref name="log"/> delivered to the step FROM turn
    /// <paramref name="prevTurn"/> — the same selection <see cref="OrderLog.BatchFor"/>
    /// makes, walked in log order so Index is the log position.
    /// </summary>
    public static OrderApplied[] For(OrderLog log, long prevTurn)
    {
        int count = 0;
        for (int i = 0; i < log.Count; i++) if (log[i].Turn == prevTurn) count++;
        var applied = new OrderApplied[count];
        int j = 0;
        for (int i = 0; i < log.Count; i++)
        {
            OrderRecord r = log[i];
            if (r.Turn != prevTurn) continue;
            applied[j++] = new OrderApplied(i, r.Turn, r.ActorId, r.Kind, r.TargetId, r.Amount);
        }
        return applied;
    }
}
