using Sim.Core.Kernel;
using Sim.Core.Systems;

namespace Sim.Core.State;

/// <summary>What stops (or paces) a queue head this step (<see cref="ConstructionQuery.ResolveHead"/>). The order of
/// the values is the order the resolver checks them in.</summary>
public enum HeadLimitKind
{
    /// <summary>Nothing short: the head advances at its builders' pace (or completes).</summary>
    None = 0,
    /// <summary>goods.json no longer defines the entry's project (data changed under a saved queue). STRUCTURAL.</summary>
    UnknownProject = 1,
    /// <summary>A project that founds an institution, at a host that cannot found one more now (checked every step,
    /// before any progress). STRUCTURAL.</summary>
    Viability = 2,
    /// <summary>The settlement's construction labour this step is 0 (or too small to move one progress unit). STRUCTURAL
    /// only when the settlement has no labour adults at all (<see cref="HeadResolution.NoLabourAdults"/>).</summary>
    Labour = 3,
    /// <summary>The next units of an input (<see cref="HeadResolution.LimitGood"/>) are not available to the head: it
    /// advances less than its builders could, or not at all.</summary>
    Materials = 4,
    /// <summary>The head cannot advance because a CRAFTED input (a good no deposit yields, made only by recipes) is short
    /// and no recipe chain available in the settlement now can make it. STRUCTURAL.</summary>
    Unobtainable = 5,
}

/// <summary>Why a materials-limited head lacks its good (<see cref="ConstructionQuery.HeadStatus"/>, display only).</summary>
public enum MaterialCause
{
    None = 0,
    /// <summary>The head's purse held none of the good at turn start.</summary>
    EmpireHasNone = 1,
    /// <summary>Heads earlier in the building order take the purse's units first (<see cref="HeadStatusReport.TakenBy"/>).</summary>
    TakenByEarlier = 2,
    /// <summary>WS2: road works hold the rest of the good this turn under the building/roads split.</summary>
    HeldForRoads = 3,
    /// <summary>The purse holds some, but fewer than the head's builders could put in this step.</summary>
    Short = 4,
}

/// <summary>One queue entry, named by its settlement, slot and project.</summary>
public readonly record struct QueueEntryRef(SettlementId Settlement, int Slot, int ProjectId);

/// <summary>
/// THE ONE RESOLUTION OF A QUEUE HEAD (WS1, D-054; ADR-033 D2's one function, two callers): what the head of
/// <c>Entry.Settlement</c>'s queue does this step against the stocks it was given. <see cref="ProgressAfter"/> is p' in
/// [p, N]; <see cref="Draws"/> are the units of each project input (project input order) that leave the purse for it —
/// exactly Delivered(cost, p') − Delivered(cost, p); <see cref="LaborUsed"/> the adult-years its progress used (0 when it
/// does not advance). <see cref="Limit"/> names what stops or paces it; for a materials limit, <see cref="HeldForRoads"/>
/// says the head's BUILDING GRANT (D-054 WS2 — the building/roads split's budget) held the binding good below what the
/// purse still had, i.e. the rest of it is held for road works this step.
/// </summary>
public sealed record HeadResolution(
    ConstructionQueueRow Entry, ConstructionProjectEntry? Project, long ProgressBefore, long ProgressAfter, long[] Draws,
    double LaborUsed, HeadLimitKind Limit, int LimitGood, long Short, bool NoLabourAdults, bool HeldForRoads = false)
{
    /// <summary>The head advances this step.</summary>
    public bool Progresses => ProgressAfter > ProgressBefore;

    /// <summary>The head completes this step.</summary>
    public bool Completes => ProgressAfter == ConstructionQuery.ProgressUnits;

    /// <summary>The head cannot advance for a STRUCTURAL reason (D-054 "abandoned"): its project left the content, its host
    /// cannot sustain the institution it founds, the settlement has no labour adults, or a crafted input cannot be made.
    /// It keeps its progress; the player sees why and may cancel; the AI cancels its own (AiConstructionPolicy).</summary>
    public bool StructurallyStalled => !Progresses && ((Limit is (HeadLimitKind.UnknownProject or HeadLimitKind.Viability
        or HeadLimitKind.Unobtainable)) || (Limit == HeadLimitKind.Labour && NoLabourAdults));
}

/// <summary>A head's resolution as the UI ESTIMATES it on the current world and the player's pending orders
/// (<see cref="ConstructionQuery.HeadStatus"/>): the same resolver, plus who took a missing good first.</summary>
public sealed record HeadStatusReport(
    HeadResolution Resolution, MaterialCause Cause, QueueEntryRef[] TakenBy, long HeldAtStart, int PlaceInOrder,
    QueueEntryRef[] Order);

public static partial class ConstructionQuery
{
    /// <summary>N — the integer progress units of one project (WS1). Progress p in [0, N]; p = N is complete.</summary>
    public const long ProgressUnits = 1_000_000;

    /// <summary>The largest input quantity whose lockstep delivery ceil(qty × p / N) stays inside a long (the GoodsConfig
    /// loader refuses larger).</summary>
    public const long MaxInputQty = (long.MaxValue - ProgressUnits) / ProgressUnits;

    private static readonly Table<ConstructionProgressRow> EmptyProgress = new();

    /// <summary>A progress table with no rows (every entry at p = 0).</summary>
    public static IReadOnlyTable<ConstructionProgressRow> NoProgress => EmptyProgress;

    /// <summary>Whether construction is incremental (sim.json construction.incremental, WS1). Off: the M4-D gate.</summary>
    public static bool Incremental(SimConfig cfg) => cfg.Construction is { Incremental: true };

    /// <summary>THE LOCKSTEP (materials first): the units of an input of cost <paramref name="cost"/> delivered at progress
    /// <paramref name="progress"/> — ceil(cost × p / N) in exact long arithmetic (the loader bounds cost by
    /// <see cref="MaxInputQty"/>). 0 at p = 0, exactly <paramref name="cost"/> at p = N, monotone in p.</summary>
    public static long Delivered(long cost, long progress) => (cost * progress + ProgressUnits - 1) / ProgressUnits;

    /// <summary>What an input of cost <paramref name="cost"/> still needs at <paramref name="progress"/>.</summary>
    public static long Remaining(long cost, long progress) => cost - Delivered(cost, progress);

    /// <summary>The labour (adult-years) the work from <paramref name="from"/> to <paramref name="to"/> uses for a project
    /// of <paramref name="laborRequired"/>: L × (to − from) / N.</summary>
    public static double LabourBetween(double laborRequired, long from, long to) =>
        laborRequired * (double)(to - from) / ProgressUnits;

    /// <summary>THE LABOUR BOUND: how many progress units capacity <paramref name="capacity"/> (adult-years this step) can
    /// add to a project of <paramref name="laborRequired"/> at <paramref name="progress"/> — min(N − p, floor(C / L × N)),
    /// clamped in double BEFORE the cast; 0 when C is not positive (NaN included). It rounds down, so a step can lose up to
    /// one unit (1 ppm of the project): completion comes at the first turn whose summed bounds reach N.</summary>
    public static long LabourBound(double capacity, double laborRequired, long progress) =>
        capacity > 0.0 ? (long)Math.Min(ProgressUnits - progress, Math.Floor(capacity / laborRequired * ProgressUnits)) : 0;

    /// <summary>The index of <paramref name="entry"/>'s progress row in <paramref name="progress"/>, matched on (Settlement,
    /// Slot, ProjectId); −1 when there is none (a row for the same slot but another project does not match — a query
    /// reads it as p = 0).</summary>
    public static int ProgressIndex(IReadOnlyTable<ConstructionProgressRow> progress, ConstructionQueueRow entry)
    {
        for (int i = 0; i < progress.Count; i++)
        {
            ConstructionProgressRow row = progress[i];
            if (row.Settlement == entry.Settlement && row.Slot == entry.Slot && row.ProjectId == entry.ProjectId) return i;
        }
        return -1;
    }

    /// <summary>The entry's progress p (0 when it has no matching row).</summary>
    public static long ProgressOf(IReadOnlyTable<ConstructionProgressRow> progress, ConstructionQueueRow entry)
    {
        int i = ProgressIndex(progress, entry);
        return i < 0 ? 0 : progress[i].Progress;
    }

    /// <summary>The entry's progress in the world's own table.</summary>
    public static long ProgressOf(IReadOnlyWorldState world, ConstructionQueueRow entry) => ProgressOf(world.ConstructionProgress, entry);

    /// <summary>The turn the entry's order was accepted, its FIFO key; −1 when it has no matching row (a v32 entry).</summary>
    public static long EnqueuedTurnOf(IReadOnlyTable<ConstructionProgressRow> progress, ConstructionQueueRow entry)
    {
        int i = ProgressIndex(progress, entry);
        return i < 0 ? -1 : progress[i].EnqueuedTurn;
    }

    /// <summary>
    /// THE v33 PROGRESS INVARIANT (ConstructionProgressRow; schema v33), ONE statement for its two checkers: ConstructionSystem
    /// at the end of every step (broken only by a bug: it throws InvalidOperationException) and Snapshot.Load (a save no rule
    /// produces: it throws SnapshotFormatException). It holds iff
    /// <list type="number">
    /// <item>no two queue rows share (Settlement, Slot) — NextSlot gives one past the highest slot queued, and the head, a
    ///   cancel and the progress match would otherwise each pick one of them by table position;</item>
    /// <item>every progress row's Progress is in [0, N − 1] — an entry reaching N is completed and removed in that same
    ///   step, so no state holds N (a head at N could never complete: its labour bound is 0), and a negative progress makes
    ///   <see cref="Delivered"/> negative, so the head would draw more than its cost;</item>
    /// <item>no two progress rows share (Settlement, Slot) — a removal takes one row and would orphan the other;</item>
    /// <item>every progress row has a queue row with the same (Settlement, Slot, ProjectId) (the converse is optional: a
    ///   queue row with no progress row is at p = 0 with EnqueuedTurn −1).</item>
    /// </list>
    /// EnqueuedTurn is a FIFO key only and is not range-checked (−1 is the key of an entry with no row). Null when the
    /// invariant holds; otherwise what breaks it, as a clause the caller prefixes.
    /// </summary>
    public static string? ProgressInvariantViolation(
        IReadOnlyTable<ConstructionQueueRow> queue, IReadOnlyTable<ConstructionProgressRow> progress)
    {
        for (int i = 0; i < queue.Count; i++)
            for (int j = i + 1; j < queue.Count; j++)
                if (queue[i].Settlement == queue[j].Settlement && queue[i].Slot == queue[j].Slot)
                    return $"two construction queue entries of settlement {Num(queue[i].Settlement.Value)}, slot {Num(queue[i].Slot)}";
        for (int i = 0; i < progress.Count; i++)
        {
            ConstructionProgressRow p = progress[i];
            if (p.Progress < 0 || p.Progress >= ProgressUnits)
                return $"construction progress {Num(p.Progress)} for settlement {Num(p.Settlement.Value)}, slot {Num(p.Slot)}, " +
                    $"project {Num(p.ProjectId)} is outside [0, {Num(ProgressUnits - 1)}]";
            for (int j = i + 1; j < progress.Count; j++)
                if (progress[j].Settlement == p.Settlement && progress[j].Slot == p.Slot)
                    return $"two construction progress rows for settlement {Num(p.Settlement.Value)}, slot {Num(p.Slot)}";
            int q = IndexOfSlot(queue, p.Settlement, p.Slot);
            if (q < 0 || queue[q].ProjectId != p.ProjectId)
                return $"construction progress for settlement {Num(p.Settlement.Value)}, slot {Num(p.Slot)}, project " +
                    $"{Num(p.ProjectId)} has no matching queue entry";
        }
        return null;
    }

    /// <summary>The index of <paramref name="settlement"/>'s HEAD in <paramref name="queue"/> (its lowest slot); −1 when its
    /// queue is empty.</summary>
    public static int HeadIndex(IReadOnlyTable<ConstructionQueueRow> queue, SettlementId settlement)
    {
        int best = -1;
        for (int i = 0; i < queue.Count; i++)
        {
            if (queue[i].Settlement != settlement) continue;
            if (best < 0 || queue[i].Slot < queue[best].Slot) best = i;
        }
        return best;
    }

    /// <summary>The slot a new entry of <paramref name="settlement"/> takes: one past its highest slot CURRENTLY queued —
    /// 0 for an empty queue (slots are reused once a queue empties; ConstructionQueueRow's doc).</summary>
    public static int NextSlot(IReadOnlyTable<ConstructionQueueRow> queue, SettlementId settlement)
    {
        int next = 0;
        for (int i = 0; i < queue.Count; i++)
        {
            if (queue[i].Settlement != settlement) continue;
            if (queue[i].Slot >= next) next = queue[i].Slot + 1;
        }
        return next;
    }

    /// <summary>The index of the entry at (<paramref name="settlement"/>, <paramref name="slot"/>); −1 when none.</summary>
    public static int IndexOfSlot(IReadOnlyTable<ConstructionQueueRow> queue, SettlementId settlement, int slot)
    {
        for (int i = 0; i < queue.Count; i++)
            if (queue[i].Settlement == settlement && queue[i].Slot == slot) return i;
        return -1;
    }

    /// <summary>
    /// Removes the entry at (<paramref name="settlement"/>, <paramref name="slot"/>) from <paramref name="queue"/> AND its
    /// progress row (matched on the entry's project) from <paramref name="progress"/>, each preserving the relative order
    /// of the rest (a swap-with-last would reorder the serialized stream and make the hash depend on history). No-op when
    /// the slot is not queued. Returns the removed entry, or null.
    /// </summary>
    public static ConstructionQueueRow? RemoveEntry(
        Table<ConstructionQueueRow> queue, Table<ConstructionProgressRow> progress, SettlementId settlement, int slot)
    {
        int index = IndexOfSlot(queue, settlement, slot);
        if (index < 0) return null;
        ConstructionQueueRow entry = queue[index];
        RemoveAt(queue, index);
        int p = ProgressIndex(progress, entry);
        if (p >= 0) RemoveAt(progress, p);
        return entry;
    }

    private static void RemoveAt<T>(Table<T> table, int index) where T : unmanaged
    {
        var kept = new T[table.Count];
        int n = 0;
        for (int i = 0; i < table.Count; i++)
            if (i != index) kept[n++] = table[i];
        table.Clear();
        for (int i = 0; i < n; i++) table.Add(kept[i]);
    }

    private static Table<ConstructionProgressRow> CopyProgress(IReadOnlyTable<ConstructionProgressRow> rows)
    {
        var copy = new Table<ConstructionProgressRow>(rows.Count);
        for (int i = 0; i < rows.Count; i++) copy.Add(rows[i]);
        return copy;
    }

    /// <summary>
    /// THE CancelConstruction PREDICATE (kind 11; ADR-033 D2 — one predicate, two callers: ConstructionSystem at delivery
    /// and the UI's emitter). It holds iff the payload is well-formed (TargetId ≥ 0; the slot finite, whole, in
    /// [0, int.MaxValue] — re-checked here because an in-memory OrderLog.Append never validates), the issuer is a roster
    /// Empire (or the world has no roster), it CONTROLS the settlement on <paramref name="prev"/> (guarded on a non-empty
    /// Controls table, the EnqueueConstruction rule), and the slot is queued in <paramref name="queue"/> — the system's
    /// NEXT queue (after this batch's earlier cancels), the UI's pending book. Otherwise the order changes nothing.
    /// </summary>
    public static bool CancelOrderValid(IReadOnlyWorldState prev, OrderRecord order, IReadOnlyTable<ConstructionQueueRow> queue)
    {
        if (order.Kind != OrderKind.CancelConstruction) return false;
        if (order.TargetId < 0 || !OrderLog.ConstructionSlotValid(order.Amount)) return false;
        if (prev.Polities.Count > 0 && !EmpireQuery.TryGetCommandSource(prev, order.Actor, out _)) return false;
        var settlement = new SettlementId(order.TargetId);
        if (prev.Controls.Count > 0 && !EmpireQuery.ControlsSettlement(prev, order.Actor, settlement)) return false;
        return IndexOfSlot(queue, settlement, (int)order.Amount) >= 0;
    }

    /// <summary><see cref="CancelOrderValid(IReadOnlyWorldState, OrderRecord, IReadOnlyTable{ConstructionQueueRow})"/>
    /// against the world's own queue.</summary>
    public static bool CancelOrderValid(IReadOnlyWorldState world, OrderRecord order) =>
        CancelOrderValid(world, order, world.ConstructionQueue);

    /// <summary>The COMMITTED entries (the world's own queue) that the turn's pending batch <paramref name="pending"/>
    /// cancels, as the intake will apply it: each CancelConstruction that <see cref="CancelOrderValid(IReadOnlyWorldState, OrderRecord, IReadOnlyTable{ConstructionQueueRow})"/>
    /// admits against the queue as the earlier pending cancels leave it, in batch order. Empty when nothing is pending.</summary>
    public static QueueEntryRef[] PendingCancels(IReadOnlyWorldState world, IReadOnlyList<OrderRecord>? pending)
    {
        if (pending is null) return [];
        Table<ConstructionQueueRow>? queue = null;
        var result = new List<QueueEntryRef>();
        for (int o = 0; o < pending.Count; o++)
        {
            OrderRecord order = pending[o];
            if (order.Kind != OrderKind.CancelConstruction) continue;
            if (!CancelOrderValid(world, order, (IReadOnlyTable<ConstructionQueueRow>?)queue ?? world.ConstructionQueue)) continue;
            queue ??= Copy(world.ConstructionQueue);
            int i = IndexOfSlot(queue, new SettlementId(order.TargetId), (int)order.Amount);
            result.Add(new QueueEntryRef(queue[i].Settlement, queue[i].Slot, queue[i].ProjectId));
            RemoveAt(queue, i);
        }
        return [.. result];
    }

    /// <summary>What has gone into an entry of <paramref name="project"/> at <paramref name="progress"/>, in figures and
    /// with no percentage (any era): "16 of 40 timber, 8 of 20 stone and 0.8 of 2 adult-years have gone in".</summary>
    public static string GoneIn(GoodsConfig goods, ConstructionProjectEntry project, long progress)
    {
        var parts = new List<string>(project.Inputs.Length + 1);
        foreach (ProjectInput i in project.Inputs)
            parts.Add(Num(Delivered(i.Qty, progress)) + " of " + Num(i.Qty) + " " + i.Good);
        string labour = Num(LabourBetween(project.LaborRequired, 0, progress)) + " of " + Num(project.LaborRequired) + " adult-years";
        return string.Join(", ", parts) + " and " + labour + " have gone in";
    }

    /// <summary>What a cancel of an entry of <paramref name="project"/> at <paramref name="progress"/> forfeits: "16 timber,
    /// 8 stone, 0.8 adult-years" (no refund — D-054).</summary>
    public static string Forfeit(ConstructionProjectEntry project, long progress)
    {
        var parts = new List<string>(project.Inputs.Length + 1);
        foreach (ProjectInput i in project.Inputs) parts.Add(Num(Delivered(i.Qty, progress)) + " " + i.Good);
        parts.Add(Num(LabourBetween(project.LaborRequired, 0, progress)) + " adult-years");
        return string.Join(", ", parts);
    }

    /// <summary>The CancelConstruction order the player's Cancel and the AI's structural-stall cancel issue — identical
    /// for both: stamped with the CURRENT turn, TargetId = the settlement, Amount = the slot.</summary>
    public static OrderRecord CancelOrder(IReadOnlyWorldState world, PolityId issuer, SettlementId settlement, int slot) =>
        OrderRecord.From(world.Clock.Turn, issuer, OrderKind.CancelConstruction, settlement.Value, slot);

    /// <summary>
    /// THE ONE RESOLVER (WS1, D-054; ADR-033 D2). What the HEAD of <paramref name="settlement"/>'s queue in
    /// <paramref name="queue"/> does this step, its progress read from <paramref name="progress"/>, against the units
    /// <paramref name="stocks"/> hold in its purse (<see cref="Holders"/> on <paramref name="prev"/>), capped per good by
    /// <paramref name="budget"/> (WS2: what is left of the Empire's BUILDING GRANT this step, indexed by good id; null = no
    /// cap). Null when the queue is empty.
    /// ConstructionSystem calls it with its NEXT tables and the real budget, after every earlier head's draw; the UI's
    /// <see cref="HeadStatus"/> calls it on the current world (an ESTIMATE). The rule, in its checking order:
    /// <list type="number">
    /// <item>A project the content no longer defines waits (<see cref="HeadLimitKind.UnknownProject"/>).</item>
    /// <item>A project that founds an institution waits while its host cannot found one more (InstitutionViability.ToFound
    ///   on PREV — every step, before any progress) (<see cref="HeadLimitKind.Viability"/>).</item>
    /// <item>LABOUR BOUND: C = <see cref="CapacityAdultYears"/> (unchanged; NaN or ≤ 0 = 0), dp = <see cref="LabourBound"/>
    ///   (min(N − p, floor(C / L × N))). dp = 0 waits (<see cref="HeadLimitKind.Labour"/>).</item>
    /// <item>MATERIAL BOUND per input g of cost c (materials first): with a_g the units this head may take now and
    ///   d = <see cref="Delivered"/>(c, p), p'_g = N if a_g ≥ c − d, else floor((d + a_g) × N / c) — the furthest progress
    ///   whose lockstep delivery the units cover. So from a bucket edge nothing advances without the next unit of every
    ///   input, and no draw exceeds what is available.</item>
    /// <item>p' = min(p + dp, min_g p'_g). The draws are Delivered(c, p') − Delivered(c, p) per input; the labour used is
    ///   L × (p' − p) / N. A materials-bound head names the FIRST input (project order) with the lowest bound; one that
    ///   cannot advance at all for a crafted input no available recipe chain can make is
    ///   <see cref="HeadLimitKind.Unobtainable"/>.</item>
    /// </list>
    /// <paramref name="strict"/> (the system): a progress row for the head's slot that names ANOTHER project is a bug and
    /// throws; queries read it as p = 0.
    /// </summary>
    public static HeadResolution? ResolveHead(
        IReadOnlyWorldState prev, SimConfig cfg, SettlementId settlement,
        IReadOnlyTable<ConstructionQueueRow> queue, IReadOnlyTable<ConstructionProgressRow> progress,
        IReadOnlyTable<GoodStockRow> stocks, double dtYears, long[]? budget, bool strict = false)
    {
        int head = HeadIndex(queue, settlement);
        if (head < 0) return null;
        ConstructionQueueRow entry = queue[head];
        if (strict)
        {
            for (int i = 0; i < progress.Count; i++)
                if (progress[i].Settlement == entry.Settlement && progress[i].Slot == entry.Slot
                    && progress[i].ProjectId != entry.ProjectId)
                    throw new InvalidOperationException(
                        $"construction: the progress row of settlement {entry.Settlement.Value}, slot {entry.Slot} names project " +
                        $"{progress[i].ProjectId} but the queue entry is project {entry.ProjectId} — a cancelled or completed " +
                        "entry's progress survived its removal.");
        }
        long p = ProgressOf(progress, entry);
        GoodsConfig? goods = cfg.Goods;
        if (goods?.ProjectById(entry.ProjectId) is not { } project)
            return new HeadResolution(entry, null, p, p, [], 0.0, HeadLimitKind.UnknownProject, -1, 0, false);

        var none = new long[project.Inputs.Length];
        if (InstitutionContent.FoundsInstitution(project) && !InstitutionViability.ToFound(prev, cfg, settlement).Viable)
            return new HeadResolution(entry, project, p, p, none, 0.0, HeadLimitKind.Viability, -1, 0, false);

        double capacity = CapacityAdultYears(prev, cfg, settlement, dtYears);
        long dp = LabourBound(capacity, project.LaborRequired, p);
        if (dp <= 0)
            return new HeadResolution(entry, project, p, p, none, 0.0, HeadLimitKind.Labour, -1, 0,
                InstitutionStaffing.LabourAdults(prev, cfg, settlement) <= 0.0);

        SettlementId[] holders = Holders(prev, cfg, settlement);
        long target = p + dp;
        long pMat = ProgressUnits;
        int binding = -1;
        long bindingAvailable = 0;
        bool bindingHeld = false;
        for (int i = 0; i < project.Inputs.Length; i++)
        {
            ProjectInput input = project.Inputs[i];
            int good = goods.IdOf(input.Good);
            long cost = input.Qty;
            long delivered = Delivered(cost, p);
            long held = EmpireStockpile.Available(stocks, holders, new GoodId(good));
            long available = held;
            if (budget is not null && good >= 0 && good < budget.Length) available = Math.Min(available, Math.Max(0, budget[good]));
            long bound = available >= cost - delivered ? ProgressUnits : (delivered + available) * ProgressUnits / cost;
            if (bound < pMat) { pMat = bound; binding = i; bindingAvailable = available; bindingHeld = available < held; }
        }

        long after = Math.Min(target, pMat);
        var draws = new long[project.Inputs.Length];
        for (int i = 0; i < project.Inputs.Length; i++)
            draws[i] = Delivered(project.Inputs[i].Qty, after) - Delivered(project.Inputs[i].Qty, p);
        double labour = after > p ? LabourBetween(project.LaborRequired, p, after) : 0.0;

        if (pMat >= target)
            return new HeadResolution(entry, project, p, after, draws, labour, HeadLimitKind.None, -1, 0, false);

        ProjectInput limiting = project.Inputs[binding];
        int limitGood = goods.IdOf(limiting.Good);
        long shortBy = Delivered(limiting.Qty, target) - Delivered(limiting.Qty, p) - bindingAvailable;
        HeadLimitKind kind = after == p && CraftedUnobtainable(prev, cfg, settlement, limitGood)
            ? HeadLimitKind.Unobtainable : HeadLimitKind.Materials;
        return new HeadResolution(entry, project, p, after, draws, labour, kind, limitGood, shortBy, false, bindingHeld);
    }

    /// <summary>Whether good <paramref name="id"/> is CRAFTED and no recipe chain available in the settlement now can make
    /// it (the obtainability guard's recipe reading, with no claims: what the purse holds, not what is spoken for).</summary>
    private static bool CraftedUnobtainable(IReadOnlyWorldState world, SimConfig cfg, SettlementId settlement, int id)
    {
        if (cfg.Goods is not { } goods || id < 0 || !IsCrafted(goods, id)) return false;
        var purse = new Purse(world, goods, Holders(world, cfg, settlement), null, NoProgress);
        bool[]? knowledge = CraftingQuery.KnowledgeOf(world, cfg.Research, settlement);
        var visiting = new bool[MaxGoodId(goods) + 1];
        return !MadeByAvailableRecipe(world, cfg, goods, settlement, purse, id, knowledge, visiting);
    }

    /// <summary>
    /// THE BUILDING ORDER (WS1, D-054 — the reservation rule's priority): the queue HEADS of <paramref name="queue"/>, one
    /// per settlement of <paramref name="world"/> with a non-empty queue, OLDEST ORDER FIRST — composite key (EnqueuedTurn
    /// ascending, −1 for an entry with no progress row; SettlementId ascending; Slot ascending), all integers, unique per
    /// head (one head per settlement), so the order is total and deterministic.
    /// </summary>
    public static ConstructionQueueRow[] HeadsInBuildingOrder(
        IReadOnlyWorldState world, IReadOnlyTable<ConstructionQueueRow> queue, IReadOnlyTable<ConstructionProgressRow> progress)
    {
        var heads = new List<(long Turn, ConstructionQueueRow Entry)>();
        for (int s = 0; s < world.Settlements.Count; s++)
        {
            int head = HeadIndex(queue, world.Settlements[s].Id);
            if (head < 0) continue;
            heads.Add((EnqueuedTurnOf(progress, queue[head]), queue[head]));
        }
        heads.Sort(static (a, b) =>
        {
            int c = a.Turn.CompareTo(b.Turn);
            if (c != 0) return c;
            c = a.Entry.Settlement.Value.CompareTo(b.Entry.Settlement.Value);
            return c != 0 ? c : a.Entry.Slot.CompareTo(b.Entry.Slot);
        });
        var result = new ConstructionQueueRow[heads.Count];
        for (int i = 0; i < result.Length; i++) result[i] = heads[i].Entry;
        return result;
    }

    /// <summary>
    /// THE HEAD'S STATUS AS THE UI ESTIMATES IT (WS1; ADR-033 D2): the turn's building work replayed on a scratch copy of
    /// the CURRENT world's stocks — the player's <paramref name="pending"/> cancels and enqueues applied first, exactly as
    /// the intake will (<see cref="PendingBook"/>),
    /// then every head in the building order (<see cref="HeadsInBuildingOrder"/>) resolved by <see cref="ResolveHead"/> and
    /// drawn through <see cref="EmpireStockpile.Draw"/> (into a scratch ledger) until <paramref name="settlement"/>'s head is
    /// reached. An ESTIMATE: the real step resolves on the stocks production, consumption, trade and housing leave earlier
    /// in the same step. WS2: each Empire's building/roads split is applied as the step applies it (the pending kind-12
    /// orders upserted, the road order the pending batch delivers — ConstructionSystem's step 5 on these stocks). A
    /// materials-limited head's <see cref="HeadStatusReport.Cause"/> is EmpireHasNone when its purse held none of the
    /// good at turn start, HeldForRoads when the split's grant held the rest of it for road works, TakenByEarlier (naming
    /// the heads) when earlier heads took units of it from its purse, else Short. Null when the settlement's queue (with
    /// the pending orders) is empty.
    /// </summary>
    public static HeadStatusReport? HeadStatus(
        IReadOnlyWorldState world, SimConfig cfg, SettlementId settlement, IReadOnlyList<OrderRecord>? pending, double dtYears)
    {
        IReadOnlyTable<ConstructionQueueRow> queue = PendingBook(world, cfg, pending, out IReadOnlyTable<ConstructionProgressRow> progress);
        if (HeadIndex(queue, settlement) < 0) return null;
        ConstructionQueueRow[] order = HeadsInBuildingOrder(world, queue, progress);
        var refs = new QueueEntryRef[order.Length];
        for (int i = 0; i < order.Length; i++) refs[i] = new QueueEntryRef(order[i].Settlement, order[i].Slot, order[i].ProjectId);

        var stocks = new Table<GoodStockRow>(world.GoodStocks.Count);
        for (int i = 0; i < world.GoodStocks.Count; i++) stocks.Add(world.GoodStocks[i]);
        var scratch = new Ledger(new Table<LedgerFlowRow>());
        SettlementId[] mine = Holders(world, cfg, settlement);
        var takenBy = new List<(QueueEntryRef Head, int Good, long Units)>();

        // WS2: the building/roads split as the step will apply it — the pending kind-12 orders upserted first, the road
        // order the pending batch delivers, the split computed before any head draws (ConstructionSystem's step 5).
        long[]?[] grants = GrantsLeft(StepAllocations(world, cfg, queue, progress, stocks,
            AllocationsWith(world, cfg, world.ResourceAllocations, pending), pending ?? [], dtYears));

        for (int h = 0; h < order.Length; h++)
        {
            SettlementId at = order[h].Settlement;
            long[]? grant = GrantsFor(world, grants, at);
            HeadResolution? r = ResolveHead(world, cfg, at, queue, progress, stocks, dtYears, budget: grant);
            if (r is null) continue;
            if (at == settlement)
            {
                MaterialCause cause = MaterialCause.None;
                long held = 0;
                var who = new List<QueueEntryRef>();
                if (r.Limit is (HeadLimitKind.Materials or HeadLimitKind.Unobtainable) && r.LimitGood >= 0)
                {
                    held = EmpireStockpile.Available(world.GoodStocks, mine, new GoodId(r.LimitGood));
                    foreach ((QueueEntryRef head, int good, long units) in takenBy)
                        if (good == r.LimitGood && units > 0 && !who.Contains(head)) who.Add(head);
                    cause = held <= 0 ? MaterialCause.EmpireHasNone
                        : r.HeldForRoads ? MaterialCause.HeldForRoads
                        : who.Count > 0 ? MaterialCause.TakenByEarlier : MaterialCause.Short;
                }
                return new HeadStatusReport(r, cause, [.. who], held, h, refs);
            }
            if (!r.Progresses || r.Project is not { } project || cfg.Goods is not { } goods) continue;
            SpendGrants(grant, goods, project, r.Draws);
            SettlementId[] holders = Holders(world, cfg, at);
            for (int i = 0; i < project.Inputs.Length; i++)
            {
                if (r.Draws[i] <= 0) continue;
                var good = new GoodId(goods.IdOf(project.Inputs[i].Good));
                long before = EmpireStockpile.Available(stocks, mine, good);
                EmpireStockpile.Draw(scratch, stocks, holders, good, r.Draws[i], ReasonIds.ConstructionMaterials);
                long taken = before - EmpireStockpile.Available(stocks, mine, good);
                if (taken > 0) takenBy.Add((refs[h], good.Value, taken));
            }
        }
        return null;
    }

    /// <summary>
    /// Whether <paramref name="settlement"/>'s head is STRUCTURALLY STALLED now (<see cref="HeadResolution.StructurallyStalled"/>):
    /// the resolver on the world's own tables and stocks with no competition — the reason the AI cancels its own head
    /// (AiConstructionPolicy) and the UI names. <paramref name="dtYears"/> only paces labour; a structural labour stall is
    /// the absence of labour adults, whatever the turn's length.
    /// </summary>
    public static HeadResolution? StructuralStall(IReadOnlyWorldState world, SimConfig cfg, SettlementId settlement, double dtYears)
    {
        HeadResolution? r = ResolveHead(world, cfg, settlement, world.ConstructionQueue, world.ConstructionProgress,
            world.GoodStocks, dtYears, budget: null);
        return r is { StructurallyStalled: true } ? r : null;
    }
}
