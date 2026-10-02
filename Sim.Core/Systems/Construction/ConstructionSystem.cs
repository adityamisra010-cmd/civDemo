using Sim.Core.Kernel;
using Sim.Core.State;

namespace Sim.Core.Systems.Construction;

/// <summary>Tables owned by <see cref="ConstructionSystem"/> (M4-D). GoodStocks
/// is the SANCTIONED SHARED STOCK (see SystemCatalog — construction is its fifth
/// holder); the queue and the structure counts are this system's own.</summary>
public sealed record ConstructionTables(
    Table<ConstructionQueueRow> Queue, Table<StructureRow> Structures,
    Table<GoodStockRow> GoodStocks);

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
            if (!ConstructionQuery.IsProjectAvailable(prev, _cfg, order.Actor, settlement, projectId)) continue;

            queue.Add(new ConstructionQueueRow(settlement, NextSlot(queue, settlement), projectId));
        }

        // 2. RESOLVE one head per settlement.
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
            // material present IN FULL, checked before any draw. Both are the shared
            // ConstructionQuery statics, so the blocker the action surface reports is
            // computed by the function that gates the build.
            if (!(ConstructionQuery.CapacityAdultYears(prev, settlement, ctx.DtYears) >= project.LaborRequired)) continue;
            if (!ConstructionQuery.MaterialsAvailable(ctx.Owned.GoodStocks, _cfg.Goods, settlement, project)) continue;

            Consume(ctx, settlement, project);
            Complete(ctx.Owned.Structures, settlement, project.Id);
            RemoveAt(queue, head);
        }
    }

    private void Consume(
        SimContext<ConstructionTables> ctx, SettlementId settlement, ConstructionProjectEntry project)
    {
        Table<GoodStockRow> stocks = ctx.Owned.GoodStocks;
        for (int i = 0; i < project.Inputs.Length; i++)
        {
            ProjectInput input = project.Inputs[i];
            var good = new GoodId(_cfg.Goods!.IdOf(input.Good));
            int idx = FindStock(stocks, settlement, good);

            // Throw, not clamp: availability was established above, so a short
            // draw here means the state moved under us and silence would hide it.
            ctx.Ledger.Flow(ref stocks.Ref(idx).Amount, ConservedQuantityIds.OfGood(good),
                ReasonIds.ConstructionMaterials, input.Qty, FlowDirection.Sink, OverdrawPolicy.Throw);
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

    private static int FindStock(Table<GoodStockRow> stocks, SettlementId s, GoodId good)
    {
        for (int i = 0; i < stocks.Count; i++)
            if (stocks[i].Settlement == s && stocks[i].Good == good) return i;
        return -1;
    }
}
