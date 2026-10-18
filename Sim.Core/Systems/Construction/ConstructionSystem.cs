using Sim.Core.Kernel;
using Sim.Core.State;

namespace Sim.Core.Systems.Construction;

/// <summary>Tables owned by <see cref="ConstructionSystem"/> (M4-D). GoodStocks
/// is the SANCTIONED SHARED STOCK (see SystemCatalog — construction is its fifth
/// holder); the queue, the structure counts and (ADR-033 D10) the published
/// construction labour are this system's own.</summary>
public sealed record ConstructionTables(
    Table<ConstructionQueueRow> Queue, Table<StructureRow> Structures,
    Table<GoodStockRow> GoodStocks, Table<ConstructionLaborRow> Labor);

/// <summary>
/// M4-D — THE SETTLEMENT CONSTRUCTION QUEUE, RESOLVED WHOLE OR NOT AT ALL.
///
/// THE STEP, per settlement, in table row order, all signals from PREV:
///  1. ENQUEUE. Each EnqueueConstruction order in this turn's batch appends one
///     row to the target settlement's queue at the next free SLOT. Log order,
///     so two orders in one turn queue in the order they were issued. The
///     settlement must exist, the issuing Empire must CONTROL it, and (ADR-033
///     D3) the project's research entity must be knowledge-eligible for it; all
///     are checked HERE, at the point of consumption, by
///     ConstructionQuery.IsProjectAvailable — the predicate the action surface
///     lists projects by (one predicate, two callers).
///
///     WHY HERE AND NOT ONLY IN OrderValidation, which also checks both. That
///     pass runs ONCE, before turn 1, against the turn-0 world (Sim.Cli calls
///     it right after world construction). It therefore cannot see any order
///     whose target did not exist at turn 0 — every colonised settlement — and
///     it never runs at all on orders that do not arrive through a loaded log.
///     An earlier revision of this comment asserted control "was rejected
///     up-front by OrderValidation" and skipped the check on that basis, which
///     left M4-D §12's rule with no enforcement at the only place orders are
///     actually consumed. The load-time pass is a fast, actionable rejection
///     for a bad log; it is not the rule's implementation.
///  2. RESOLVE. Only the HEAD of each queue — its lowest slot — is eligible.
///     A blocked head blocks the queue; the system never reaches past it to
///     build something cheaper, because a queue whose order is advisory is not
///     a queue.
///
/// RESOLUTION IS A GATE, NOT A RATE. The head is built this turn iff EVERY
/// material is present in full AND the settlement's construction capacity meets
/// the project's requirement. Otherwise nothing happens: no partial draw, no
/// banked progress, no percentage. Multiplying a build rate by a shortfall
/// fraction is exactly what this design rejects — it makes every project
/// eventually complete and turns material scarcity into a speed dial.
///
/// SO THERE IS NO TIMER, AND THAT IS THE POINT. A cathedral is not "50 turns";
/// it is a project a village cannot marshal and a city can. Duration is
/// emergent from labour allocation, material stocks and the era's turn length,
/// and nothing in the state records how long anything has been waiting.
///
/// CAPACITY comes from the EXISTING sector allocation, in the EXISTING unit:
/// construction share × adult population × dtYears = adult-years, the same
/// quantity PathBuild and Housing draw from. Housing's published draw is
/// subtracted at the standard one-turn lag (a table read, never a system
/// reference — law 6), so a settlement that spends its builders on houses has
/// fewer for monuments. That is the opportunity cost, and it is a real one.
///
/// CONSERVATION: materials leave their good stocks through Ledger.Flow under
/// ReasonIds.ConstructionMaterials, all of them or none. The all-or-nothing
/// check runs BEFORE the first draw, so a project can never consume timber and
/// then discover the stone is missing.
///
/// THE PURSE (authority: D-051 §6 and D-053 §1 — the Director's 2026-10-13
/// instruction, item 1: construction and roads draw on the empire stockpile; sim.json
/// construction.empireStockpile, ON in the shipped content). "Present in full" and the draw read
/// ConstructionQuery.Holders, computed on PREV — turn-start control — for the
/// building settlement: the settlement first, then every other settlement its
/// controller commands, NEAREST-FIRST by exact integer squared site-cell distance,
/// ties to the LOWER settlement id (EmpireStockpile — ADR-032's road payment order
/// with both route ends at the builder); a settlement no Empire controls is its own
/// stockpile. The check sums the holders' rows of each input on this system's NEXT
/// GoodStocks table; the draw takes each input from the holders in that order, each
/// giving min(remaining, held) through the Ledger, instantly and losslessly. Switch
/// off or absent: the building settlement's own stock only, bit-exact. The M4-D gate
/// is unchanged: the WHOLE cost in the purse or the head waits — no partial draw —
/// and the capacity is the building settlement's own labour, never the Empire's.
///
/// COMPETING PROJECTS (D-051 §6, D-053 §1). Heads are tried one per settlement in
/// PREV's settlement-table row order, which in every founded or loaded world is
/// ascending SettlementId (rows are append-only: founding writes ids 0..N-1 in order,
/// colonization appends max+1, nothing removes a row, and the snapshot keeps row
/// order) — each against what the earlier heads LEFT: its gates read the NEXT table
/// after every earlier head's draw. A lower-id head that passes its gates (capacity,
/// materials in full, founding viability) draws FIRST; a lower-id head that fails a
/// gate draws nothing and blocks no one, so a higher-id head may then build from the
/// purse it would have used. Two heads whose combined cost exceeds the purse cannot
/// both build — no unit is allocated twice and no stock goes negative. Road
/// development (ADR-032) runs LATER in the pipeline (pipeline.json: construction
/// before roaddevelopment) and draws from the same NEXT table, so a road order sees
/// only what construction left.
///
/// THE CLAIMS AT INTAKE (D-053 §1; ConstructionQuery's header). The obtainability
/// guard reads the purse UNCLAIMED — less the whole inputs of every project already
/// queued in the purse's settlements — and the intake passes it THIS step's queue:
/// PREV's rows plus every order accepted earlier in the same batch, so two orders of
/// one batch cannot both count ten tools the purse holds once. Switch off: no claims.
///
/// ADR-033 D10 — CAPACITY IS SPENT ONCE. The adult-years a resolved head consumed
/// are PUBLISHED in the ConstructionLabor table (rebuilt every step: cleared, one
/// row per settlement that built), and PathBuild subtracts them at the §3.2
/// one-turn lag exactly as it subtracts housing's draw — before this, the labour a
/// project used was also banked toward dirt paths in the same turn.
///
/// ADR-033 D6 — A PROJECT THAT FOUNDS AN INSTITUTION (goods.json <c>founds</c>, the
/// five universities) resolves only where the host can FOUND one more
/// (InstitutionViability.ToFound on PREV: the market and the food surplus) — a third
/// transient gate beside materials and capacity, so the head waits exactly as it
/// waits for stone. Its completion is counted in Structures like any project;
/// InstitutionsSystem founds the institution from that count the next step.
/// Capacity reads the labour left after institutional staff
/// (InstitutionStaffing.LabourAdults, inside ConstructionQuery.CapacityAdultYears).
/// </summary>
public sealed class ConstructionSystem(SimConfig cfg) : ISimSystem<ConstructionTables>
{
    public static readonly SystemId WellKnownId = new(20);
    public const string Name = "construction";

    private readonly SimConfig _cfg = cfg;

    public SystemId Id => WellKnownId;

    public void Step(SimContext<ConstructionTables> ctx)
    {
        IReadOnlyWorldState prev = ctx.Prev;
        Table<ConstructionQueueRow> queue = ctx.Owned.Queue;

        // 1. ENQUEUE, in log order.
        for (int o = 0; o < ctx.Orders.Count; o++)
        {
            OrderRecord order = ctx.Orders[o];
            if (order.Kind != OrderKind.EnqueueConstruction) continue;

            var settlement = new SettlementId(order.TargetId);
            int projectId = (int)order.Amount;   // load-validated as a whole number

            // ADR-033 D3 — THE availability predicate, the very function the action
            // surface lists projects by (one predicate, two callers): the settlement
            // exists; the issuing Empire CONTROLS it (M4-D §12, from the D-037 control
            // relation, never the actor id on trust — guarded on a non-empty relation,
            // because a world with no Controls at all has nothing to check against and
            // hand-built test worlds are legitimately in that state; in a FOUNDED world
            // the relation is never empty, M4-C); goods.json defines the project; and
            // its research entity is knowledge-eligible for the issuer (ADR-028 LOCKED
            // → AVAILABLE). An order failing it changes nothing.
            // The purse's claims (switch on) are read from THIS step's queue — PREV's
            // rows plus the orders accepted above — never PREV's alone (see the header).
            if (!ConstructionQuery.IsProjectAvailable(prev, _cfg, order.Actor, settlement, projectId, queue)) continue;

            queue.Add(new ConstructionQueueRow(settlement, NextSlot(queue, settlement), projectId));
        }

        // 2. RESOLVE one head per settlement. The published labour is rebuilt from
        // this step's resolutions (ADR-033 D10): cleared first, so a settlement that
        // builds nothing this step reads 0 next step.
        ctx.Owned.Labor.Clear();
        if (_cfg.Goods is null) return;
        for (int s = 0; s < prev.Settlements.Count; s++)
        {
            SettlementId settlement = prev.Settlements[s].Id;
            int head = HeadIndex(queue, settlement);
            if (head < 0) continue;

            ConstructionProjectEntry? project = _cfg.Goods.ProjectById(queue[head].ProjectId);
            if (project is null) continue;   // data changed under a saved queue

            // The settlement's construction labour this turn, in adult-years, less
            // housing's published draw (§3.2 one-turn lag), floored at zero — and every
            // material present IN FULL in the settlement's purse (its holders on PREV,
            // their stocks on NEXT after every earlier head's draw), checked before any
            // draw — and, for a project that founds an institution, the host's founding
            // viability. All are the shared State statics, so the blocker the action
            // surface reports is computed by the function that gates the build.
            if (!(ConstructionQuery.CapacityAdultYears(prev, _cfg, settlement, ctx.DtYears) >= project.LaborRequired)) continue;
            SettlementId[] holders = ConstructionQuery.Holders(prev, _cfg, settlement);
            if (!ConstructionQuery.MaterialsAvailable(ctx.Owned.GoodStocks, _cfg.Goods, holders, project)) continue;
            if (InstitutionContent.FoundsInstitution(project) && !InstitutionViability.ToFound(prev, _cfg, settlement).Viable) continue;

            Consume(ctx, holders, project);
            Complete(ctx.Owned.Structures, settlement, project.Id);
            RemoveAt(queue, head);
            ctx.Owned.Labor.Add(new ConstructionLaborRow(settlement, project.LaborRequired));
        }
    }

    /// <summary>Draws every input, input order, from the purse <paramref name="holders"/> in its draw order (the
    /// building settlement first — with one holder, exactly the single sink of the pre-purse rule).</summary>
    private void Consume(
        SimContext<ConstructionTables> ctx, SettlementId[] holders, ConstructionProjectEntry project)
    {
        Table<GoodStockRow> stocks = ctx.Owned.GoodStocks;
        for (int i = 0; i < project.Inputs.Length; i++)
        {
            ProjectInput input = project.Inputs[i];
            var good = new GoodId(_cfg.Goods!.IdOf(input.Good));

            // Throw, not clamp: availability was established above, so a short
            // draw here means the state moved under us and silence would hide it.
            EmpireStockpile.Draw(ctx.Ledger, stocks, holders, good, input.Qty, ReasonIds.ConstructionMaterials);
        }
    }

    private static void Complete(Table<StructureRow> structures, SettlementId settlement, int projectId)
    {
        for (int i = 0; i < structures.Count; i++)
        {
            if (structures[i].Settlement != settlement || structures[i].ProjectId != projectId) continue;
            structures[i] = structures[i] with { Count = structures[i].Count + 1 };
            return;
        }

        structures.Add(new StructureRow(settlement, projectId, 1));
    }

    /// <summary>Lowest slot for the settlement — the head. -1 when it has none.</summary>
    private static int HeadIndex(Table<ConstructionQueueRow> queue, SettlementId settlement)
    {
        int best = -1;
        for (int i = 0; i < queue.Count; i++)
        {
            if (queue[i].Settlement != settlement) continue;
            if (best < 0 || queue[i].Slot < queue[best].Slot) best = i;
        }

        return best;
    }

    /// <summary>
    /// One past the settlement's highest slot ever seen. Slots are never reused,
    /// so a completed head cannot let a later project inherit its position.
    /// </summary>
    private static int NextSlot(Table<ConstructionQueueRow> queue, SettlementId settlement)
    {
        int next = 0;
        for (int i = 0; i < queue.Count; i++)
        {
            if (queue[i].Settlement != settlement) continue;
            if (queue[i].Slot >= next) next = queue[i].Slot + 1;
        }

        return next;
    }

    /// <summary>
    /// Removes one row, preserving the relative order of the rest — Table has no
    /// RemoveAt, and a swap-with-last would reorder the serialized stream and
    /// make the canonical hash depend on completion history.
    /// </summary>
    private static void RemoveAt(Table<ConstructionQueueRow> queue, int index)
    {
        var kept = new ConstructionQueueRow[queue.Count];
        int n = 0;
        for (int i = 0; i < queue.Count; i++)
        {
            if (i != index) kept[n++] = queue[i];
        }

        queue.Clear();
        for (int i = 0; i < n; i++) queue.Add(kept[i]);
    }

}
