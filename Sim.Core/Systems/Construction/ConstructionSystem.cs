using Sim.Core.Kernel;
using Sim.Core.State;

namespace Sim.Core.Systems.Construction;

/// <summary>Tables owned by <see cref="ConstructionSystem"/> (M4-D; WS1/WS2 v33). GoodStocks is the SANCTIONED SHARED
/// STOCK (see SystemCatalog — construction is one of its holders); the queue, the structure counts, (ADR-033 D10) the
/// published construction labour, (WS1) the per-entry progress and (WS2) the per-Empire material split are this
/// system's own.</summary>
public sealed record ConstructionTables(
    Table<ConstructionQueueRow> Queue, Table<StructureRow> Structures,
    Table<GoodStockRow> GoodStocks, Table<ConstructionLaborRow> Labor,
    Table<ConstructionProgressRow> Progress, Table<ResourceAllocationRow> Allocations);

/// <summary>
/// M4-D — THE SETTLEMENT CONSTRUCTION QUEUE; WS1 (D-054 — the Director's 2026-10-14 instruction, workstream 1) — BUILT
/// INCREMENTALLY. D-054 SUPERSEDES M4-D's "built whole or waits": with sim.json construction.incremental on (shipped), a
/// queue head progresses a little each turn as far as its builders and the next units of its materials allow, and what
/// has gone in is never lost while it waits. With the switch off, the M4-D gate below ("THE SWITCH OFF") runs exactly as
/// in batch 2 — the attribution control only.
///
/// THE STEP, in this FIXED order (all signals from PREV except the tables this system owns, which it reads NEXT):
///  1. ctx.Owned.Labor.Clear() — the published labour is rebuilt from this step's work (ADR-033 D10).
///  2. SetResourceAllocation (kind 12) upserts, in log order — WS2 (sim.json construction.allocationRatios): each order
///     ConstructionQuery.AllocationOrderValid admits (the switch on; a roster Empire setting its OWN split; a whole share
///     in [0, 100]) upserts its Empire's ResourceAllocationRow in the NEXT table — the last valid order wins, and an order
///     stamped turn t governs the split of THIS step (t → t+1), the SetResearchTarget precedent. Switch off: ignored.
///  3. CancelConstruction (kind 11), in log order. Each order the predicate admits
///     (ConstructionQuery.CancelOrderValid on PREV and the NEXT queue: a roster actor, CONTROL of the settlement, the
///     payload re-checked, the slot queued — DELIVERY-TIME validation, see WHY HERE below) removes the entry AND its
///     progress row from the NEXT tables. NO REFUND: what went in was sunk as the work progressed and is forfeited (a
///     salvage refund is an open Director choice, D-054). The next slot becomes the head with its own progress (0).
///  4. EnqueueConstruction (kind 4), in log order: an order the availability predicate admits against the NEXT queue and
///     NEXT progress (so this batch's cancels and earlier accepts count, and a re-enqueue into a freed slot never
///     inherits forfeited progress) appends an entry at the next free SLOT and, incremental on, a progress row
///     (EnqueuedTurn = PREV's turn, Progress = 0). The settlement must exist, the issuer must CONTROL it, and (ADR-033 D3)
///     the project's research entity must be knowledge-eligible — ConstructionQuery.IsProjectAvailable, the predicate the
///     action surface lists projects by (one predicate, two callers). Affordability never gates an order: a project
///     begins when it is head and has builders plus the first unit of every input.
///  5. WS2: per Empire, the BUILDING/ROADS SPLIT over the POST-INTAKE heads (ConstructionQuery.StepAllocations; the rule
///     in State.ResourceAllocation): on the NEXT stocks before any head draws, S = the Empire purse's holding of each
///     eligible good, D_b = what its heads would draw with no split, D_r = what the road order road development will
///     apply for it this step would be charged (RoadDevelopmentQuery.DeliveredOrderFor + SimulateCharges — the road
///     system's own rules, read from State, never from that system); its heads may then draw at most the GRANT
///     G_b = min(D_b, E_b + max(0, E_r − D_r)) of each good in all, spent down as they draw, served in the building
///     order. An Empire at b = 100 (no row: building first) is skipped — G_b = min(D_b, S) never binds — so its heads
///     draw exactly as with the split off. A road order that road development would charge nothing on what building work
///     leaves it withholds nothing (the starvation guard: G_b = min(D_b, S)). Housing is not under the split (it draws its
///     own settlement's timber, earlier).
///  6. RESOLVE THE HEADS IN THE BUILDING ORDER (ConstructionQuery.HeadsInBuildingOrder): only each settlement's head —
///     its lowest slot — progresses, OLDEST ORDER FIRST (EnqueuedTurn ascending, −1 for an entry with no progress row;
///     then SettlementId; then Slot). Each head is resolved by ConstructionQuery.ResolveHead — THE resolver the UI's
///     estimate also calls — against the NEXT stocks after every earlier head's draw, and draws exactly
///     Delivered(c, p') − Delivered(c, p) of each input through EmpireStockpile.Draw (Ledger.Flow sinks under
///     ReasonIds.ConstructionMaterials, the batch-2 donor order).
///  7. COMPLETION: a head reaching p' = N is completed in the same step — Structures + 1 (StructureRow), its queue row and
///     progress row removed exactly once. Its effects begin as any completed structure's always did: institution
///     founding the next step (InstitutionsSystem), the granary factors from the next production step.
///  8. The progress/queue invariant is asserted (ConstructionQuery.ProgressInvariantViolation, the statement Snapshot.Load
///     also applies: unique slots, progress in [0, N − 1], one progress row per entry, each matched by its queue row), and
///     the split invariant (ResourceAllocation.InvariantViolation: shares in [0, 100], one row per Empire).
/// All in-step reads — the intake guard, the claims, the resolution — use the NEXT/owned queue and progress tables;
/// ProjectId is matched on every read, and in resolution a mismatch throws (a bug: forfeited progress survived).
///
/// WHY CANCELS AND ENQUEUES ARE CHECKED HERE AND NOT ONLY IN OrderValidation, which also checks control. That pass runs
/// ONCE, before turn 1, against the turn-0 world (Sim.Cli calls it right after world construction). It therefore cannot
/// see any order whose target did not exist at turn 0 — every colonised settlement — nor a change of control after it
/// (a revolt), and it never runs at all on orders that do not arrive through a loaded log. An earlier revision of this
/// comment asserted control "was rejected up-front by OrderValidation" and skipped the check on that basis, which left
/// M4-D §12's rule with no enforcement at the only place orders are actually consumed. The load-time pass is a fast,
/// actionable rejection for a bad log; it is not the rule's implementation.
///
/// PROGRESS AND THE LOCKSTEP (materials first). Each entry has integer progress p in [0, N], N = 1,000,000. After
/// progress p exactly Delivered(c, p) = ceil(c × p / N) of each input of cost c has gone in — so nothing advances from
/// a bucket edge without the next unit of EVERY input, a shortage of one material stops the work without losing any of
/// it, and at p = N exactly c has been delivered in total. The labour bound is floor(C / L × N) units a step for capacity
/// C adult-years and a project of L; labour is a PACE, not a gate, so a project needing more than one turn's capacity
/// simply takes more turns (the M4-D gate made such projects unbuildable at short turn lengths). DT: the floor loses up
/// to one unit (1 ppm) per step, so the completion turn is the first k with Σ dp ≥ N; a project built at two turn lengths
/// completes within one turn's elapsed years of the same date — not exactly dt-invariant, and nothing claims it is.
///
/// THE HEAD WAITS, keeping p (no decay, no timer), while materials are short, it has no builders (C = 0), an institution
/// host cannot sustain it (viability, checked every step), a crafted input cannot be made, or its project has left the
/// content. A head that cannot advance for a STRUCTURAL reason (no labour adults at all, viability, an unobtainable crafted
/// input, an unknown project) is reported as such; the player sees why and may cancel, and the AI cancels its own
/// (AiConstructionPolicy).
///
/// RESERVATION (Director WS1-5, D-054). A project never consumes (a) materials already delivered to another project —
/// they left the purse through the Ledger and are sunk — nor (b) what a project EARLIER in the building order takes this
/// step: this step's material is served oldest order first, and a later project uses only what earlier ones left. The
/// CLAIMS (ConstructionQuery.Claimed: each queued entry's remaining needs) stay advisory — the crafted-input guard, the
/// AI's ordering and the display read them; they set nothing aside.
///
/// CAPACITY comes from the EXISTING sector allocation, in the EXISTING unit: construction share × labour adults × dtYears
/// = adult-years (ConstructionQuery.CapacityAdultYears), the same quantity PathBuild and Housing draw from. Housing's
/// published draw is subtracted at the standard one-turn lag (a table read, never a system reference — law 6).
///
/// THE PURSE (D-051 §6 and D-053 §1; sim.json construction.empireStockpile): ConstructionQuery.Holders, computed on PREV
/// — turn-start control — for the building settlement: the settlement first, then every other settlement its controller
/// commands, nearest-first, ties to the lower id (EmpireStockpile); a settlement no Empire controls is its own stockpile.
/// The resolver reads the holders' rows on this system's NEXT GoodStocks; the draw takes each input from them in that
/// order. The capacity is always the building settlement's own labour.
///
/// CONTROLLER CHANGE (the existing convention, explicit): the queue and its progress belong to the SETTLEMENT and stay
/// with it when control changes — the new controller's purse pays from then on (the holders are read on PREV), and the
/// new controller may cancel; the old one no longer can.
///
/// ROAD DEVELOPMENT (ADR-032) runs LATER in the pipeline (pipeline.json: construction before roaddevelopment) and draws
/// from the same NEXT table, so a road order sees only what building work left; no unit is spent twice. Under an
/// Empire's split (WS2) building work left at least min(D_r, E_r) of every eligible good for it; at b = 100 (the default)
/// building work goes first and road works get what it leaves.
///
/// THE CLAIMS AT INTAKE (D-053 §1; ConstructionQuery's header). The obtainability guard reads the purse UNCLAIMED — less
/// the REMAINING inputs of every entry queued in the purse's settlements — and the intake passes it THIS step's queue and
/// progress: PREV's rows less this batch's cancels plus every order accepted earlier in the same batch.
///
/// ADR-033 D10 — CAPACITY IS SPENT ONCE. The adult-years the head's progress used, L × (p' − p) / N, are PUBLISHED in the
/// ConstructionLabor table (rebuilt every step: cleared, one row per settlement whose head advanced), and PathBuild
/// subtracts them at the §3.2 one-turn lag exactly as it subtracts housing's draw. Construction capacity a head does NOT
/// use is not lost: PathBuild banks it toward dirt paths, as before. The per-step publications of one project sum to L
/// only up to double rounding.
///
/// ADR-033 D6 — A PROJECT THAT FOUNDS AN INSTITUTION (goods.json <c>founds</c>, the five universities) progresses only
/// while its host can FOUND one more (InstitutionViability.ToFound on PREV — the market and the food surplus), checked
/// every step before any progress, so the head waits exactly as it waits for stone. Its completion is counted in
/// Structures like any project; InstitutionsSystem founds the institution from that count the next step. A part-built
/// project is not a structure: it is not counted, drawn, or felt by any effect.
///
/// THE SWITCH OFF (construction.incremental false or absent — TestConfigs.PreIncrementalConstruction): steps 1, 3 and 4
/// as above (no progress rows are written), then M4-D's gate exactly as in batch 2: heads tried one per settlement in
/// PREV's settlement-table order; a head is built this turn iff the settlement's capacity meets the project's
/// laborRequired, EVERY material is present in full in the purse, and (a founding project) the host is viable — then its
/// whole cost is drawn, Structures + 1, the entry leaves the queue and its whole laborRequired is published; otherwise
/// nothing happens.
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
        Table<ConstructionProgressRow> progress = ctx.Owned.Progress;
        bool incremental = ConstructionQuery.Incremental(_cfg);

        // 1. The published labour is rebuilt from this step's work (ADR-033 D10).
        ctx.Owned.Labor.Clear();

        // 2. SetResourceAllocation (kind 12, WS2), in log order: each order the predicate admits (the switch on, a roster
        //    Empire setting its OWN split, a whole share in [0, 100]) upserts its Empire's row; the last valid one wins.
        if (ResourceAllocation.Enabled(_cfg))
        {
            for (int o = 0; o < ctx.Orders.Count; o++)
            {
                OrderRecord order = ctx.Orders[o];
                if (!ConstructionQuery.AllocationOrderValid(prev, _cfg, order)) continue;
                ResourceAllocation.Upsert(ctx.Owned.Allocations, order.Actor, (int)order.Amount);
            }
        }

        // 3. CANCEL, in log order, against the NEXT queue (delivery-time validation; the header).
        for (int o = 0; o < ctx.Orders.Count; o++)
        {
            OrderRecord order = ctx.Orders[o];
            if (order.Kind != OrderKind.CancelConstruction) continue;
            if (!ConstructionQuery.CancelOrderValid(prev, order, queue)) continue;
            ConstructionQuery.RemoveEntry(queue, progress, new SettlementId(order.TargetId), (int)order.Amount);
        }

        // 4. ENQUEUE, in log order, against the NEXT queue and progress.
        for (int o = 0; o < ctx.Orders.Count; o++)
        {
            OrderRecord order = ctx.Orders[o];
            if (order.Kind != OrderKind.EnqueueConstruction) continue;

            var settlement = new SettlementId(order.TargetId);
            int projectId = (int)order.Amount;   // load-validated as a whole number

            // ADR-033 D3 — THE availability predicate, the very function the action surface lists projects by (one
            // predicate, two callers): the settlement exists; the issuing Empire CONTROLS it (M4-D §12, from the D-037
            // control relation, never the actor id on trust — guarded on a non-empty relation, because a world with no
            // Controls at all has nothing to check against and hand-built test worlds are legitimately in that state; in a
            // FOUNDED world the relation is never empty, M4-C); goods.json defines the project; and its research entity is
            // knowledge-eligible for the issuer (ADR-028 LOCKED → AVAILABLE). An order failing it changes nothing. The
            // purse's claims (switch on) are read from THIS step's queue and progress — never PREV's alone.
            if (!ConstructionQuery.IsProjectAvailable(prev, _cfg, order.Actor, settlement, projectId, queue, progress)) continue;

            int slot = ConstructionQuery.NextSlot(queue, settlement);
            queue.Add(new ConstructionQueueRow(settlement, slot, projectId));
            if (incremental) progress.Add(new ConstructionProgressRow(settlement, slot, projectId, prev.Clock.Turn, 0));
        }

        if (_cfg.Goods is not null)
        {
            if (incremental) ResolveIncremental(ctx);
            else ResolveWhole(ctx);
        }

        AssertInvariant(queue, progress);
        if (ResourceAllocation.InvariantViolation(ctx.Owned.Allocations) is { } split)
            throw new InvalidOperationException("construction: " + split + " (the v33 invariant).");
    }

    /// <summary>Steps 5–7 with incremental construction on (the header).</summary>
    private void ResolveIncremental(SimContext<ConstructionTables> ctx)
    {
        IReadOnlyWorldState prev = ctx.Prev;
        Table<ConstructionQueueRow> queue = ctx.Owned.Queue;
        Table<ConstructionProgressRow> progress = ctx.Owned.Progress;
        Table<GoodStockRow> stocks = ctx.Owned.GoodStocks;
        GoodsConfig goods = _cfg.Goods!;

        // 5. WS2: each Empire's building/roads split over the POST-INTAKE heads, on the NEXT stocks before any head draws
        //    and the split rows as step 2 left them (null where it cannot bind: b = 100, or the switch off).
        long[]?[] grants = ConstructionQuery.GrantsLeft(ConstructionQuery.StepAllocations(
            prev, _cfg, queue, progress, stocks, ctx.Owned.Allocations, RoadDevelopmentQuery.Orders(ctx.Orders), ctx.DtYears));

        ConstructionQueueRow[] heads = ConstructionQuery.HeadsInBuildingOrder(prev, queue, progress);
        for (int h = 0; h < heads.Length; h++)
        {
            SettlementId settlement = heads[h].Settlement;
            long[]? grant = ConstructionQuery.GrantsFor(prev, grants, settlement);
            HeadResolution? r = ConstructionQuery.ResolveHead(
                prev, _cfg, settlement, queue, progress, stocks, ctx.DtYears, budget: grant, strict: true);
            if (r is null || !r.Progresses || r.Project is not { } project) continue;
            ConstructionQuery.SpendGrants(grant, goods, project, r.Draws);

            SettlementId[] holders = ConstructionQuery.Holders(prev, _cfg, settlement);
            for (int i = 0; i < project.Inputs.Length; i++)
            {
                if (r.Draws[i] == 0) continue;
                // Throw, not clamp: the resolver bounded every draw by the units available, so a short draw here means
                // the state moved under us and silence would hide it.
                EmpireStockpile.Draw(ctx.Ledger, stocks, holders, new GoodId(goods.IdOf(project.Inputs[i].Good)),
                    r.Draws[i], ReasonIds.ConstructionMaterials);
            }
            ctx.Owned.Labor.Add(new ConstructionLaborRow(settlement, r.LaborUsed));

            if (r.Completes)
            {
                Complete(ctx.Owned.Structures, settlement, project.Id);
                ConstructionQuery.RemoveEntry(queue, progress, settlement, r.Entry.Slot);
                continue;
            }

            int row = ConstructionQuery.ProgressIndex(progress, r.Entry);
            if (row >= 0)
                progress[row] = progress[row] with { Progress = r.ProgressAfter };
            else   // an entry with no row (a v32 entry): its first progress appends one, keeping its order key −1
                progress.Add(new ConstructionProgressRow(settlement, r.Entry.Slot, r.Entry.ProjectId, -1, r.ProgressAfter));
        }
    }

    /// <summary>THE SWITCH OFF: M4-D's whole-or-nothing gate, exactly as in batch 2 (the header).</summary>
    private void ResolveWhole(SimContext<ConstructionTables> ctx)
    {
        IReadOnlyWorldState prev = ctx.Prev;
        Table<ConstructionQueueRow> queue = ctx.Owned.Queue;
        GoodsConfig goods = _cfg.Goods!;
        for (int s = 0; s < prev.Settlements.Count; s++)
        {
            SettlementId settlement = prev.Settlements[s].Id;
            int head = ConstructionQuery.HeadIndex(queue, settlement);
            if (head < 0) continue;

            ConstructionProjectEntry? project = goods.ProjectById(queue[head].ProjectId);
            if (project is null) continue;   // data changed under a saved queue

            // The settlement's construction labour this turn, in adult-years, less housing's published draw (§3.2 one-turn
            // lag), floored at zero — and every material present IN FULL in the settlement's purse (its holders on PREV,
            // their stocks on NEXT after every earlier head's draw), checked before any draw — and, for a project that
            // founds an institution, the host's founding viability.
            if (!(ConstructionQuery.CapacityAdultYears(prev, _cfg, settlement, ctx.DtYears) >= project.LaborRequired)) continue;
            SettlementId[] holders = ConstructionQuery.Holders(prev, _cfg, settlement);
            if (!ConstructionQuery.MaterialsAvailable(ctx.Owned.GoodStocks, goods, holders, project)) continue;
            if (InstitutionContent.FoundsInstitution(project) && !InstitutionViability.ToFound(prev, _cfg, settlement).Viable) continue;

            for (int i = 0; i < project.Inputs.Length; i++)
            {
                ProjectInput input = project.Inputs[i];
                EmpireStockpile.Draw(ctx.Ledger, ctx.Owned.GoodStocks, holders, new GoodId(goods.IdOf(input.Good)), input.Qty,
                    ReasonIds.ConstructionMaterials);
            }
            Complete(ctx.Owned.Structures, settlement, project.Id);
            ConstructionQuery.RemoveEntry(queue, ctx.Owned.Progress, settlement, queue[head].Slot);
            ctx.Owned.Labor.Add(new ConstructionLaborRow(settlement, project.LaborRequired));
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

    /// <summary>THE v33 INVARIANT (ConstructionQuery.ProgressInvariantViolation — the one statement Snapshot.Load also
    /// applies): queue slots unique per settlement; every progress row in [0, N − 1], unique per (Settlement, Slot) and matched
    /// by a queue row with the same (Settlement, Slot, ProjectId). Broken only by a bug, so it throws.</summary>
    private static void AssertInvariant(Table<ConstructionQueueRow> queue, Table<ConstructionProgressRow> progress)
    {
        if (ConstructionQuery.ProgressInvariantViolation(queue, progress) is { } broken)
            throw new InvalidOperationException("construction: " + broken + " (the v33 invariant).");
    }
}
