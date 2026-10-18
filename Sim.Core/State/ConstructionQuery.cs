using System.Globalization;
using Sim.Core.Kernel;
using Sim.Core.Systems;
using Sim.Core.Systems.Research;

namespace Sim.Core.State;

/// <summary>Why a construction project is, or is not, AVAILABLE to an issuer in a settlement (ADR-033 D3).
/// Availability is legality (ADR-028's LOCKED / AVAILABLE); affordability is reported separately.</summary>
public enum ProjectAvailability
{
    Available = 0,
    /// <summary>No settlement has this id.</summary>
    UnknownSettlement = 1,
    /// <summary>The issuer does not control the settlement (M4-D §12: an Empire builds only where it rules).</summary>
    NotControlled = 2,
    /// <summary>goods.json defines no project with this id (or no goods content is loaded).</summary>
    UnknownProject = 3,
    /// <summary>The project's research entity is not knowledge-eligible for the issuer — ADR-028 LOCKED.</summary>
    NotKnowledgeEligible = 4,
    /// <summary>D-051 §4 / Director 2026-10-10: a CRAFTED input of the project (a good no deposit yields, made only
    /// by recipes — tools) is neither held in the required amount nor producible in the settlement now through any
    /// available recipe chain, so the project would sit at the head of the queue blocking it indefinitely.</summary>
    InputUnobtainable = 5,
}

/// <summary>One input a project lacks in a settlement now: <c>Required</c> units of <c>Good</c>, <c>Held</c> present in the
/// settlement's purse (<see cref="ConstructionQuery.Holders"/>: its Empire's settlements together, or itself alone).</summary>
public readonly record struct MaterialShortfall(GoodId Good, string Name, long Required, long Held);

/// <summary>
/// ADR-033 D3 — THE CONSTRUCTION READ SEAM (the ResearchQuery / AgeQuery precedent): pure, read-only, no
/// state, no cache. <b>ConstructionSystem computes with these same statics</b>: it accepts an
/// EnqueueConstruction order iff <see cref="IsProjectAvailable"/> holds on PREV, gates the queue head on
/// <see cref="CapacityAdultYears"/> and <see cref="MaterialsAvailable"/>, and AvailableActionsQuery lists a
/// project iff the same predicate holds on the world asked about — one predicate, two callers (ADR-033 D2),
/// so the UI never offers a project the simulation would refuse and an order the UI never offered is
/// refused on the same rule.
///
/// AVAILABLE = the settlement exists, the issuer controls it, goods.json defines the project, and the
/// project's research entity (goods.json <c>projects[].entity</c>, data — no id in code) is
/// knowledge-eligible for the issuer (ResearchQuery.IsKnowledgeEligible; a null requirement is always met,
/// ADR-028 §3). A project with no entity link has no knowledge requirement. A config with no research
/// content has no knowledge to gate on: every linked project is knowledge-eligible there (the baseline
/// construction capability predates research). Materials and capacity are AFFORDABILITY — transient
/// blockers the query reports, never reasons to hide the project.
///
/// THE PURSE (D-051 §6 and D-053 §1 — the Director's 2026-10-13 instruction, item 1: construction and roads draw on the
/// empire stockpile; sim.json <c>construction.empireStockpile</c>): every reading of what a settlement HOLDS for a project —
/// <see cref="MaterialsAvailable"/>, <see cref="Shortfalls"/>, <see cref="Blocker"/>, the obtainability guard below — reads
/// <see cref="Holders"/>: with the switch on, a settlement an Empire controls holds what its Empire's settlements hold
/// together (EmpireStockpile — the donor order is stated there), and a settlement no Empire controls holds its own stock;
/// with it off (or absent), every settlement holds its own stock only, the pre-change rule bit-exact. ConstructionSystem
/// draws the materials from the same holders in the same order. Capacity, deposits and recipe availability stay the
/// settlement's OWN — the purse pools goods, not labour, land or workshops. A DISPLAY for a settlement the viewer does not
/// control reads that settlement's own stock only (<see cref="HoldersSeenBy"/>, P7): the pooled figure would sum the
/// foreign Empire's other settlements, which the viewer may not know.
///
/// ONE EXCEPTION, Director 2026-10-10 (D-051 §4; dated note in ADR-033 D3): a CRAFTED input with NO production
/// path is not a transient blocker. A good no deposit yields and only recipes make (tools) is OBTAINABLE when the
/// settlement's purse holds the required amount, or when some recipe available there NOW (CraftingQuery.IsRecipeAvailable —
/// the predicate ProductionSystem applies) makes it from inputs that are themselves obtainable (raw inputs: a
/// deposit with abundance &gt; 0, or any stock in the purse). Otherwise the project is <see cref="ProjectAvailability.InputUnobtainable"/>:
/// before it, the Age I Workshop (10 tools; tools come only from bronze, tin_bronze, Age III) was a legal order that
/// froze its settlement's queue for hundreds of turns (there was no cancel order before WS1). This is evaluated at
/// ENQUEUE (ConstructionSystem's order intake). At resolution a head whose crafted input becomes unobtainable later just
/// WAITS — it keeps its progress — and is reported as STRUCTURALLY STALLED (<see cref="HeadLimitKind.Unobtainable"/>), which
/// the player sees and may cancel and the AI cancels for its own heads (WS1, D-054). Raw-material shortfalls (stone) stay
/// affordability.
///
/// THE GUARD READS THE UNCLAIMED PURSE FOR CRAFTED GOODS ONLY (switch on; D-053 §1). One purse serves many queues, so for
/// a CRAFTED good — the only kind the guard exists for: one the empire cannot dig up and may be unable to make — "the
/// purse holds it" is read as "the purse holds it UNCLAIMED": the purse's holding of the good less the CLAIMS on it
/// (<see cref="Claimed(IReadOnlyTable{ConstructionQueueRow}, IReadOnlyTable{ConstructionProgressRow}, GoodsConfig, SettlementId[], GoodId)"/>)
/// — the REMAINING input of that good (what has not yet gone in, WS1) of every project queued, at any slot (a completed
/// project leaves the table), in the purse's settlements. Without it, ten tools held once would admit a
/// workshop order in every settlement of the Empire, the first to resolve would spend them, and every other would freeze
/// its queue for good — the trap above, re-opened by the purse. A stateless settlement's purse is itself, so it is checked
/// against its own stock and its own queue. The claims touch exactly two readings: the direct test (the project's crafted
/// input — the unclaimed holding covers the required amount) and, in the recipe chain, a CRAFTED intermediate's "any
/// stock" (bronze: the unclaimed holding is above zero). A RAW input of the chain (timber, copper ore; any good that is
/// not crafted) keeps the POOLED reading — any stock in the purse, claimed or not, or a deposit at the settlement: a raw
/// good is dug, felled or gathered again, so a transient raw shortage stays a transient blocker (the paragraph above),
/// never a refusal; claiming it would refuse a workshop because a granary elsewhere waits for the same timber. The queue
/// is the world's own for every display and for the AI (with the world's own progress); ConstructionSystem's order intake
/// passes its NEXT queue and NEXT progress, which hold PREV's rows less this batch's cancels plus every order it accepted
/// earlier in the same batch, so two orders of one batch cannot both count the same unclaimed units, and the action surface
/// passes the world's queue with the player's pending cancels and the pending orders the intake would accept applied
/// (<see cref="QueueWithPending(IReadOnlyWorldState, SimConfig, IReadOnlyList{OrderRecord}?, out IReadOnlyTable{ConstructionProgressRow})"/>).
/// CLAIMS ARE COUNTS (WS1, D-054), READ BY THE GUARD, THE AI AND THE DISPLAY. They set nothing aside themselves. D-055
/// (the Director's batch-4 instruction, item 1): each queue HEAD that is not released HOLDS its remaining need, per purse
/// and good, in the building order — later heads and road works may not use those units (<see cref="HoldsWalk"/>; the
/// rule, the release predicate, the purse key and the release table are ConstructionSystem's header); a NON-head entry's
/// remaining need stays a claim only. Switch off: no claims, the pre-change reading bit-exact. THE GUARD IS
/// STILL A REACHABILITY TEST, not an affordability test: an intermediate (bronze) held unclaimed in ANY amount above zero
/// makes its chain producible, as before — a partial holding that cannot make the whole input remains the pre-existing
/// gap, and raw inputs and capacity stay transient blockers.
/// </summary>
public static partial class ConstructionQuery
{
    /// <summary>THE EnqueueConstruction predicate (see the header), the purse's claims read from the world's own queue.</summary>
    public static bool IsProjectAvailable(
        IReadOnlyWorldState world, SimConfig cfg, PolityId issuer, SettlementId settlement, int projectId) =>
        Availability(world, cfg, issuer, settlement, projectId, world.ConstructionQueue) == ProjectAvailability.Available;

    /// <summary>THE EnqueueConstruction predicate with the queue whose projects CLAIM the purse given explicitly —
    /// ConstructionSystem's order intake passes its NEXT queue (PREV's rows plus the orders it accepted earlier in the
    /// batch; see the header).</summary>
    public static bool IsProjectAvailable(
        IReadOnlyWorldState world, SimConfig cfg, PolityId issuer, SettlementId settlement, int projectId,
        IReadOnlyTable<ConstructionQueueRow> queue) =>
        Availability(world, cfg, issuer, settlement, projectId, queue) == ProjectAvailability.Available;

    /// <summary>The predicate's answer with its reason (claims from the world's own queue).</summary>
    public static ProjectAvailability Availability(
        IReadOnlyWorldState world, SimConfig cfg, PolityId issuer, SettlementId settlement, int projectId) =>
        Availability(world, cfg, issuer, settlement, projectId, world.ConstructionQueue);

    /// <summary>THE EnqueueConstruction predicate with the queue AND its progress given explicitly — ConstructionSystem's
    /// intake passes its NEXT tables (the only caller that must: in-step reads never see PREV progress), the action surface
    /// its pending book (<see cref="QueueWithPending(IReadOnlyWorldState, SimConfig, IReadOnlyList{OrderRecord}?, out IReadOnlyTable{ConstructionProgressRow})"/>).</summary>
    public static bool IsProjectAvailable(
        IReadOnlyWorldState world, SimConfig cfg, PolityId issuer, SettlementId settlement, int projectId,
        IReadOnlyTable<ConstructionQueueRow> queue, IReadOnlyTable<ConstructionProgressRow> progress) =>
        Availability(world, cfg, issuer, settlement, projectId, queue, progress) == ProjectAvailability.Available;

    /// <summary>The predicate's answer with its reason, the purse's claims read from <paramref name="queue"/> (its
    /// entries' progress from the world's own table).</summary>
    public static ProjectAvailability Availability(
        IReadOnlyWorldState world, SimConfig cfg, PolityId issuer, SettlementId settlement, int projectId,
        IReadOnlyTable<ConstructionQueueRow> queue) =>
        Availability(world, cfg, issuer, settlement, projectId, queue, world.ConstructionProgress);

    /// <summary>The predicate's answer with its reason, the purse's claims read from <paramref name="queue"/> and
    /// <paramref name="progress"/> (each entry's REMAINING inputs).</summary>
    public static ProjectAvailability Availability(
        IReadOnlyWorldState world, SimConfig cfg, PolityId issuer, SettlementId settlement, int projectId,
        IReadOnlyTable<ConstructionQueueRow> queue, IReadOnlyTable<ConstructionProgressRow> progress)
    {
        if (!SettlementExists(world, settlement)) return ProjectAvailability.UnknownSettlement;
        // Guarded on a non-empty relation: a world with no Controls at all (a hand-built toy) has
        // nothing to check against. In a FOUNDED world the relation is never empty (M4-C).
        if (world.Controls.Count > 0 && !EmpireQuery.ControlsSettlement(world, issuer, settlement))
            return ProjectAvailability.NotControlled;
        if (cfg.Goods?.ProjectById(projectId) is not { } project) return ProjectAvailability.UnknownProject;
        if (!IsKnowledgeEligible(world, cfg.Research, issuer, project)) return ProjectAvailability.NotKnowledgeEligible;
        if (UnobtainableInput(world, cfg, settlement, project, queue, progress) is not null) return ProjectAvailability.InputUnobtainable;
        return ProjectAvailability.Available;
    }

    /// <summary>The first CRAFTED input of <paramref name="project"/> (project input order) that the settlement's purse
    /// (<see cref="Holders"/>) neither holds UNCLAIMED in the required amount nor can come to hold through a recipe chain
    /// available in the settlement now — or null when every crafted input is obtainable (see the header's exception and
    /// its claims). Claims from the world's own queue. Raw and farmed inputs are never checked here.</summary>
    public static GoodEntry? UnobtainableInput(
        IReadOnlyWorldState world, SimConfig cfg, SettlementId settlement, ConstructionProjectEntry project) =>
        UnobtainableInput(world, cfg, settlement, project, world.ConstructionQueue);

    /// <summary>As <see cref="UnobtainableInput(IReadOnlyWorldState, SimConfig, SettlementId, ConstructionProjectEntry)"/>,
    /// the purse's claims read from <paramref name="queue"/> (switch on; with it off there are no claims), its entries'
    /// progress from the world's own table.</summary>
    public static GoodEntry? UnobtainableInput(
        IReadOnlyWorldState world, SimConfig cfg, SettlementId settlement, ConstructionProjectEntry project,
        IReadOnlyTable<ConstructionQueueRow> queue) =>
        UnobtainableInput(world, cfg, settlement, project, queue, world.ConstructionProgress);

    /// <summary>As above, the claims read from <paramref name="queue"/> and <paramref name="progress"/>.</summary>
    public static GoodEntry? UnobtainableInput(
        IReadOnlyWorldState world, SimConfig cfg, SettlementId settlement, ConstructionProjectEntry project,
        IReadOnlyTable<ConstructionQueueRow> queue, IReadOnlyTable<ConstructionProgressRow> progress)
    {
        if (cfg.Goods is not { } goods) return null;
        bool[]? knowledge = null;
        bool knowledgeRead = false;
        Purse? purse = null;
        foreach (ProjectInput input in project.Inputs)
        {
            int id = goods.IdOf(input.Good);
            if (id < 0 || input.Qty <= 0 || !IsCrafted(goods, id)) continue;
            purse ??= new Purse(world, goods, Holders(world, cfg, settlement), cfg.Construction is { EmpireStockpile: true } ? queue : null, progress);
            if (purse.Free(id) >= input.Qty) continue;
            if (!knowledgeRead) { knowledge = CraftingQuery.KnowledgeOf(world, cfg.Research, settlement); knowledgeRead = true; }
            // The direct input is short of the required amount, so a partial stock does not make it obtainable:
            // only a recipe that can make more does (nine tools and no way to make a tenth is the permanent block).
            var visiting = new bool[MaxGoodId(goods) + 1];
            if (!MadeByAvailableRecipe(world, cfg, goods, settlement, purse, id, knowledge, visiting)) return goods.ById(id);
        }
        return null;
    }

    /// <summary>The guard's reading of the purse. <see cref="Free"/> (a CRAFTED good): the holders' holding of it, less —
    /// when <c>Claims</c> is given (the switch on) — the claims on it of the projects queued there (<see cref="Unclaimed"/>).
    /// <see cref="Pooled"/> (a RAW good): the holders' whole holding, claimed or not (the header).</summary>
    private sealed class Purse(
        IReadOnlyWorldState world, GoodsConfig goods, SettlementId[] holders, IReadOnlyTable<ConstructionQueueRow>? claims,
        IReadOnlyTable<ConstructionProgressRow> progress)
    {
        public long Free(int id) => claims is null
            ? Pooled(id)
            : Unclaimed(world.GoodStocks, claims, progress, goods, holders, new GoodId(id));

        public long Pooled(int id) => EmpireStockpile.Available(world.GoodStocks, holders, new GoodId(id));
    }

    /// <summary>A CRAFTED good — no deposit yields it and at least one recipe outputs it (tools, bronze): the only kind the
    /// obtainability guard checks and the only kind its claims touch (the header). The project card reads its crafted
    /// inputs by the same rule.</summary>
    public static bool IsCraftedGood(GoodsConfig goods, int id) => IsCrafted(goods, id);

    /// <summary>A good no deposit yields and at least one recipe outputs.</summary>
    private static bool IsCrafted(GoodsConfig goods, int id)
    {
        if (goods.ById(id).DepositChannel is not null) return false;
        foreach (RecipeEntry r in goods.Recipes)
            if (goods.IdOf(r.Output.Good) == id) return true;
        return false;
    }

    /// <summary>Whether the settlement can come to hold good <paramref name="id"/>: any stock of it in its purse
    /// (<paramref name="purse"/>; above zero — a reachability test, see the header) — UNCLAIMED stock for a crafted good,
    /// the POOLED holding for a raw one; a deposit of it at the settlement (raw goods); or a recipe available in the
    /// settlement that outputs it from producible inputs. <paramref name="visiting"/> breaks recipe cycles (a good already
    /// on the path is not a source of itself).</summary>
    private static bool Producible(
        IReadOnlyWorldState world, SimConfig cfg, GoodsConfig goods, SettlementId settlement, Purse purse, int id,
        bool[]? knowledge, bool[] visiting)
    {
        if ((IsCrafted(goods, id) ? purse.Free(id) : purse.Pooled(id)) > 0) return true;
        if (goods.ById(id).DepositChannel is not null) return DepositOf(world, settlement, id) > 0.0;
        return MadeByAvailableRecipe(world, cfg, goods, settlement, purse, id, knowledge, visiting);
    }

    /// <summary>Whether some recipe available in the settlement now outputs good <paramref name="id"/> from inputs
    /// that are all <see cref="Producible"/>.</summary>
    private static bool MadeByAvailableRecipe(
        IReadOnlyWorldState world, SimConfig cfg, GoodsConfig goods, SettlementId settlement, Purse purse, int id,
        bool[]? knowledge, bool[] visiting)
    {
        if (visiting[id]) return false;
        visiting[id] = true;
        bool ok = false;
        foreach (RecipeEntry r in goods.Recipes)
        {
            if (goods.IdOf(r.Output.Good) != id) continue;
            if (!CraftingQuery.IsRecipeAvailable(world, cfg.Research, settlement, r, knowledge)) continue;
            bool inputs = true;
            foreach (RecipeInput ri in r.Inputs)
            {
                int inId = goods.IdOf(ri.Good);
                if (inId < 0 || !Producible(world, cfg, goods, settlement, purse, inId, knowledge, visiting)) { inputs = false; break; }
            }
            if (inputs) { ok = true; break; }
        }
        visiting[id] = false;
        return ok;
    }

    /// <summary>
    /// THE CLAIMS on the purse <paramref name="holders"/> for <paramref name="good"/>: over every project in
    /// <paramref name="queue"/> queued at one of the holders — ANY slot, head or waiting; a completed project leaves the
    /// table — that project's REMAINING input of the good (WS1, D-054): its whole input (<see cref="InputOf"/>) less what has
    /// already gone in at the entry's progress in <paramref name="progress"/> (<see cref="Delivered"/>; an entry with no
    /// matching progress row has delivered nothing). A row whose project the content no longer defines claims nothing. The
    /// exact long sum. Claims are counts (the header); a HEAD's set-aside is <see cref="HoldsWalk"/>'s (D-055).
    /// </summary>
    public static long Claimed(
        IReadOnlyTable<ConstructionQueueRow> queue, IReadOnlyTable<ConstructionProgressRow> progress, GoodsConfig goods,
        SettlementId[] holders, GoodId good)
    {
        long total = 0;
        checked
        {
            for (int i = 0; i < queue.Count; i++)
            {
                if (!IsHolder(holders, queue[i].Settlement)) continue;
                if (goods.ProjectById(queue[i].ProjectId) is not { } queued) continue;
                total += Remaining(InputOf(goods, queued, good), ProgressOf(progress, queue[i]));
            }
        }
        return total;
    }

    /// <summary><see cref="Claimed(IReadOnlyTable{ConstructionQueueRow}, IReadOnlyTable{ConstructionProgressRow}, GoodsConfig, SettlementId[], GoodId)"/>
    /// of a queue read WITHOUT progress — every entry's WHOLE input (a hand-built queue with no progress table; a v32 world).
    /// Product code passes the progress table that belongs to the queue.</summary>
    public static long Claimed(IReadOnlyTable<ConstructionQueueRow> queue, GoodsConfig goods, SettlementId[] holders, GoodId good) =>
        Claimed(queue, NoProgress, goods, holders, good);

    /// <summary>The purse's holding of <paramref name="good"/> less its claims (<see cref="Claimed(IReadOnlyTable{ConstructionQueueRow}, IReadOnlyTable{ConstructionProgressRow}, GoodsConfig, SettlementId[], GoodId)"/>):
    /// what no queued project still counts on. Negative when the queued projects already claim more than the purse holds.</summary>
    public static long Unclaimed(
        IReadOnlyTable<GoodStockRow> stocks, IReadOnlyTable<ConstructionQueueRow> queue, IReadOnlyTable<ConstructionProgressRow> progress,
        GoodsConfig goods, SettlementId[] holders, GoodId good) =>
        checked(EmpireStockpile.Available(stocks, holders, good) - Claimed(queue, progress, goods, holders, good));

    /// <summary><see cref="Unclaimed(IReadOnlyTable{GoodStockRow}, IReadOnlyTable{ConstructionQueueRow}, IReadOnlyTable{ConstructionProgressRow}, GoodsConfig, SettlementId[], GoodId)"/>
    /// of a queue read without progress (every entry's whole input).</summary>
    public static long Unclaimed(
        IReadOnlyTable<GoodStockRow> stocks, IReadOnlyTable<ConstructionQueueRow> queue, GoodsConfig goods, SettlementId[] holders, GoodId good) =>
        Unclaimed(stocks, queue, NoProgress, goods, holders, good);

    /// <summary>The holders whose queued projects still CLAIM <paramref name="good"/> (a queued project at one of the
    /// holders with a remaining need of it), each once, in ascending SettlementId — the places a display names when the
    /// claims are what refuses an order.</summary>
    public static SettlementId[] ClaimedAt(
        IReadOnlyTable<ConstructionQueueRow> queue, IReadOnlyTable<ConstructionProgressRow> progress, GoodsConfig goods,
        SettlementId[] holders, GoodId good)
    {
        var ids = new List<int>();
        for (int i = 0; i < queue.Count; i++)
        {
            if (!IsHolder(holders, queue[i].Settlement)) continue;
            if (goods.ProjectById(queue[i].ProjectId) is not { } queued
                || Remaining(InputOf(goods, queued, good), ProgressOf(progress, queue[i])) <= 0) continue;
            if (!ids.Contains(queue[i].Settlement.Value)) ids.Add(queue[i].Settlement.Value);
        }
        ids.Sort();
        var result = new SettlementId[ids.Count];
        for (int i = 0; i < result.Length; i++) result[i] = new SettlementId(ids[i]);
        return result;
    }

    /// <summary><see cref="ClaimedAt(IReadOnlyTable{ConstructionQueueRow}, IReadOnlyTable{ConstructionProgressRow}, GoodsConfig, SettlementId[], GoodId)"/>
    /// of a queue read without progress.</summary>
    public static SettlementId[] ClaimedAt(IReadOnlyTable<ConstructionQueueRow> queue, GoodsConfig goods, SettlementId[] holders, GoodId good) =>
        ClaimedAt(queue, NoProgress, goods, holders, good);

    /// <summary>
    /// WHY THE GUARD REFUSED, WHEN THE CLAIMS ARE THE REASON (display only): where <see cref="UnobtainableInput(IReadOnlyWorldState, SimConfig, SettlementId, ConstructionProjectEntry, IReadOnlyTable{ConstructionQueueRow})"/>
    /// names a crafted input the settlement's purse HOLDS in the required amount but not UNCLAIMED (switch on: the
    /// projects queued in the purse's settlements already claim it), the sentence that says so — "needs 10 tools; the
    /// empire's 10 are already claimed by projects queued at Bigen" (or "4 of the empire's 12 are …"; a stateless
    /// settlement's purse is its own: "Bigen's 10 …"), the places named by <paramref name="name"/> in ascending id.
    /// Null when the project is not refused for an input, when the purse is genuinely short of it (the older "cannot make
    /// yet" reason stands), or with the switch off (no claims). Claims from <paramref name="queue"/>.
    /// </summary>
    public static string? ClaimedReason(
        IReadOnlyWorldState world, SimConfig cfg, SettlementId settlement, ConstructionProjectEntry project,
        IReadOnlyTable<ConstructionQueueRow> queue, Func<int, string> name) =>
        ClaimedReason(world, cfg, settlement, project, queue, world.ConstructionProgress, name);

    /// <summary>As above, the claims read from <paramref name="queue"/> and <paramref name="progress"/>.</summary>
    public static string? ClaimedReason(
        IReadOnlyWorldState world, SimConfig cfg, SettlementId settlement, ConstructionProjectEntry project,
        IReadOnlyTable<ConstructionQueueRow> queue, IReadOnlyTable<ConstructionProgressRow> progress, Func<int, string> name)
    {
        if (cfg.Goods is not { } goods || cfg.Construction is not { EmpireStockpile: true }) return null;
        if (UnobtainableInput(world, cfg, settlement, project, queue, progress) is not { } input) return null;
        var good = new GoodId(input.Id);
        long required = InputOf(goods, project, good);
        SettlementId[] holders = Holders(world, cfg, settlement);
        long held = EmpireStockpile.Available(world.GoodStocks, holders, good);
        if (held < required) return null;                       // genuinely short: not the claims' doing
        long claimed = Claimed(queue, progress, goods, holders, good);
        SettlementId[] at = ClaimedAt(queue, progress, goods, holders, good);
        var places = new string[at.Length];
        for (int i = 0; i < at.Length; i++) places[i] = name(at[i].Value);
        string whose = DrawsOnEmpire(world, cfg, settlement) ? "the empire's" : name(settlement.Value) + "'s";
        string subject = claimed >= held ? whose + " " + Num(held) : Num(claimed) + " of " + whose + " " + Num(held);
        bool one = claimed >= held ? held == 1 : claimed == 1;
        return "needs " + Num(required) + " " + input.Name + "; " + subject + (one ? " is" : " are")
            + " already claimed by projects queued at " + string.Join(", ", places);
    }

    /// <summary>
    /// THE QUEUE A LISTING READS THE CLAIMS FROM (D-053 §1, display only): with the construction purse on, the pending book
    /// (<see cref="PendingBook"/>: the world's queue with the turn's not-yet-delivered batch <paramref name="pending"/> applied
    /// exactly as ConstructionSystem's intake will apply it — cancels first, then the enqueues it would accept). So a workshop
    /// ordered this turn claims its ten tools at once, a second one the intake would refuse is neither offered nor shown as
    /// orderable, and a pending cancel frees its entry's remaining claims (WS1). Purse switch off: no claims, so the world's
    /// own queue (no copy) — the pre-change reading bit-exact, as batch 2 shipped it (a pending order changes no availability
    /// answer there). The progress that belongs to the returned queue is the overload with <c>out progress</c>.
    /// </summary>
    public static IReadOnlyTable<ConstructionQueueRow> QueueWithPending(
        IReadOnlyWorldState world, SimConfig cfg, IReadOnlyList<OrderRecord>? pending) =>
        QueueWithPending(world, cfg, pending, out _);

    /// <summary>As <see cref="QueueWithPending(IReadOnlyWorldState, SimConfig, IReadOnlyList{OrderRecord}?)"/>, with the
    /// PROGRESS table that belongs to the returned queue.</summary>
    public static IReadOnlyTable<ConstructionQueueRow> QueueWithPending(
        IReadOnlyWorldState world, SimConfig cfg, IReadOnlyList<OrderRecord>? pending,
        out IReadOnlyTable<ConstructionProgressRow> progress)
    {
        if (cfg.Construction is not { EmpireStockpile: true })
        {
            progress = world.ConstructionProgress;
            return world.ConstructionQueue;
        }
        return PendingBook(world, cfg, pending, out progress);
    }

    /// <summary>
    /// THE QUEUE AS IT WILL STAND AT END TURN (WS1): the world's queue with the turn's not-yet-delivered batch
    /// <paramref name="pending"/> applied exactly as ConstructionSystem's intake will apply it — every CancelConstruction the
    /// intake would honour (<see cref="CancelOrderValid(IReadOnlyWorldState, OrderRecord, IReadOnlyTable{ConstructionQueueRow})"/>)
    /// FIRST, in batch order, then every EnqueueConstruction it would accept, in batch order, each judged against the rows
    /// before it (<see cref="IsProjectAvailable(IReadOnlyWorldState, SimConfig, PolityId, SettlementId, int, IReadOnlyTable{ConstructionQueueRow}, IReadOnlyTable{ConstructionProgressRow})"/>)
    /// at the slot the intake would give it — whatever the purse switch. <paramref name="progress"/> is the progress that
    /// belongs to it: the world's, less the rows of every pending cancel, plus a progress-0 row (EnqueuedTurn = the world's
    /// turn, as the intake writes it) for every accepted pending enqueue when construction is incremental. The world's own
    /// tables (no copy) when nothing is pending. Read by the queue display and <see cref="HeadStatus"/>.
    /// </summary>
    public static IReadOnlyTable<ConstructionQueueRow> PendingBook(
        IReadOnlyWorldState world, SimConfig cfg, IReadOnlyList<OrderRecord>? pending,
        out IReadOnlyTable<ConstructionProgressRow> progress)
    {
        progress = world.ConstructionProgress;
        if (pending is null) return world.ConstructionQueue;
        Table<ConstructionQueueRow>? queue = null;
        Table<ConstructionProgressRow>? book = null;
        // 1. Cancels first, in batch order (ConstructionSystem's fixed order: cancels before enqueues).
        for (int o = 0; o < pending.Count; o++)
        {
            OrderRecord order = pending[o];
            if (!IsCancel(order.Kind)) continue;
            if (!CancelOrderValid(world, order, (IReadOnlyTable<ConstructionQueueRow>?)queue ?? world.ConstructionQueue,
                    (IReadOnlyTable<ConstructionProgressRow>?)book ?? world.ConstructionProgress)) continue;
            queue ??= Copy(world.ConstructionQueue);
            book ??= CopyProgress(world.ConstructionProgress);
            int target = CancelTargetIndex(queue, book, order);
            RemoveEntry(queue, book, new SettlementId(order.TargetId), queue[target].Slot);
        }
        // 2. Then enqueues, in batch order.
        for (int o = 0; o < pending.Count; o++)
        {
            OrderRecord order = pending[o];
            if (order.Kind != OrderKind.EnqueueConstruction) continue;
            queue ??= Copy(world.ConstructionQueue);
            book ??= CopyProgress(world.ConstructionProgress);
            var settlement = new SettlementId(order.TargetId);
            int projectId = (int)order.Amount;
            if (!IsProjectAvailable(world, cfg, order.Actor, settlement, projectId, queue, book)) continue;
            int slot = NextSlot(queue, settlement);
            queue.Add(new ConstructionQueueRow(settlement, slot, projectId));
            if (Incremental(cfg)) book.Add(new ConstructionProgressRow(settlement, slot, projectId, world.Clock.Turn, 0));
        }
        if (book is not null) progress = book;
        return queue ?? world.ConstructionQueue;
    }

    private static Table<ConstructionQueueRow> Copy(IReadOnlyTable<ConstructionQueueRow> rows)
    {
        var copy = new Table<ConstructionQueueRow>(rows.Count);
        for (int i = 0; i < rows.Count; i++) copy.Add(rows[i]);
        return copy;
    }

    /// <summary>How many units of <paramref name="good"/> <paramref name="project"/> takes: the sum of its inputs of the good
    /// (0 when it takes none).</summary>
    public static long InputOf(GoodsConfig goods, ConstructionProjectEntry project, GoodId good)
    {
        long qty = 0;
        checked
        {
            for (int i = 0; i < project.Inputs.Length; i++)
                if (goods.IdOf(project.Inputs[i].Good) == good.Value) qty += project.Inputs[i].Qty;
        }
        return qty;
    }

    /// <summary>Whether <paramref name="settlement"/> is one of <paramref name="holders"/>.</summary>
    public static bool IsHolder(SettlementId[] holders, SettlementId settlement)
    {
        for (int i = 0; i < holders.Length; i++) if (holders[i].Value == settlement.Value) return true;
        return false;
    }

    private static double DepositOf(IReadOnlyWorldState world, SettlementId settlement, int id)
    {
        double sum = 0.0;
        for (int i = 0; i < world.Deposits.Count; i++)
            if (world.Deposits[i].Settlement == settlement && world.Deposits[i].Good.Value == id) sum += world.Deposits[i].Abundance;
        return sum;
    }

    private static int MaxGoodId(GoodsConfig goods)
    {
        int max = 0;
        foreach (GoodEntry g in goods.Goods) if (g.Id > max) max = g.Id;
        return max;
    }

    /// <summary>Whether the project's research entity is knowledge-eligible for the issuer (see the header).
    /// An entity id the research content does not define is never eligible (content changed under a save).
    /// ADR-033 D6: a project that FOUNDS an institution (goods.json <c>founds</c>) needs the institution's
    /// entity knowledge-eligible too — a university project builds <c>building.university</c> and founds
    /// <c>inst.university</c>, and BOTH gate (the research stage's own reading, D-044 R4;
    /// docs/institutions-universities.md §3). Still the one availability predicate.</summary>
    public static bool IsKnowledgeEligible(
        IReadOnlyWorldState world, ResearchContent? research, PolityId issuer, ConstructionProjectEntry project)
    {
        if (research is null) return true;
        if (project.Entity is null && project.Founds is null) return true;
        bool[] completed = ResearchQuery.CompletedMask(world, research, issuer);
        if (project.Entity is { } building && !Eligible(research, building, completed)) return false;
        return project.Founds is not { } founds || Eligible(research, founds.Entity, completed);
    }

    private static bool Eligible(ResearchContent research, string entityId, bool[] completed)
    {
        int entity = research.EntityIndexOf(entityId);
        return entity >= 0 && ResearchQuery.IsKnowledgeEligible(research, entity, completed);
    }

    /// <summary>
    /// The settlement's construction capacity this turn, in adult-years: construction share × LABOUR adults ×
    /// dtYears, less housing's published draw (§3.2 one-turn lag), floored at zero (the lag means a shrinking
    /// pool can transiently owe more than it has). The SAME arithmetic, in the same order, that gates
    /// ConstructionSystem's queue head — it is the function the system calls. LABOUR adults are the adults
    /// left after institutional staff (ADR-033 D6, InstitutionStaffing.LabourAdults — the one labour reader;
    /// exactly the adult count when no institution is staffed).
    /// </summary>
    public static double CapacityAdultYears(IReadOnlyWorldState world, SimConfig cfg, SettlementId settlement, double dtYears)
    {
        SectorAllocationRow shares = Sectors.Default(settlement);
        for (int i = 0; i < world.SectorAllocations.Count; i++)
        {
            if (world.SectorAllocations[i].Settlement == settlement)
            { shares = world.SectorAllocations[i]; break; }
        }

        double adults = InstitutionStaffing.LabourAdults(world, cfg, settlement);
        double capacity = Sectors.Share(shares, Sectors.Construction) * adults * dtYears;
        for (int i = 0; i < world.Housing.Count; i++)
        {
            if (world.Housing[i].Settlement != settlement) continue;
            capacity = Math.Max(0.0, capacity - world.Housing[i].LastLaborUsed);
            break;
        }
        return capacity;
    }

    /// <summary>
    /// THE PURSE a project in <paramref name="settlement"/> is checked against and paid from, in DRAW ORDER (see the
    /// header): with sim.json <c>construction.empireStockpile</c> on, <see cref="EmpireStockpile.Holders"/> — the settlement
    /// first, then the other settlements its controller commands, nearest-first, ties by id; the settlement alone when no
    /// Empire controls it. With the switch off or absent, the settlement alone (the pre-change rule). ConstructionSystem
    /// computes it on PREV (turn-start control) and reads and draws the stocks on its NEXT table.
    /// </summary>
    public static SettlementId[] Holders(IReadOnlyWorldState world, SimConfig cfg, SettlementId settlement) =>
        cfg.Construction is { EmpireStockpile: true } ? EmpireStockpile.Holders(world, settlement) : [settlement];

    /// <summary>Whether the purse of <paramref name="settlement"/> is an Empire's (the switch is on and an Empire controls
    /// it) rather than the settlement's own stock — what the blocker says it holds.</summary>
    public static bool DrawsOnEmpire(IReadOnlyWorldState world, SimConfig cfg, SettlementId settlement) =>
        cfg.Construction is { EmpireStockpile: true } && EmpireQuery.TryGetController(world, settlement, out _);

    /// <summary>
    /// The purse of <paramref name="settlement"/> AS <paramref name="viewer"/> MAY READ IT (P7, the information class):
    /// <see cref="Holders"/> where the viewer controls the settlement — its own Empire's books — and the settlement's own
    /// stock alone where it does not. A foreign settlement's pooled figure would sum its Empire's OTHER settlements, which
    /// the viewer may not know; its own stock is what the viewer's settlement view already shows of it. Every display that
    /// may be asked about a settlement the viewer does not rule reads this (PlayerViews' Structures block, the project card).
    /// </summary>
    public static SettlementId[] HoldersSeenBy(IReadOnlyWorldState world, SimConfig cfg, PolityId viewer, SettlementId settlement) =>
        EmpireQuery.ControlsSettlement(world, viewer, settlement) ? Holders(world, cfg, settlement) : [settlement];

    /// <summary>Whether <see cref="HoldersSeenBy"/> is an Empire's purse: the switch is on and the viewer controls the
    /// settlement — what the display says the figure is ("empire has").</summary>
    public static bool DrawsOnEmpireSeenBy(IReadOnlyWorldState world, SimConfig cfg, PolityId viewer, SettlementId settlement) =>
        cfg.Construction is { EmpireStockpile: true } && EmpireQuery.ControlsSettlement(world, viewer, settlement);

    /// <summary>Every input present IN FULL in the purse <paramref name="holders"/> (<see cref="Holders"/>) of
    /// <paramref name="stocks"/> — checked before any draw: M4-D's whole-or-nothing gate, which ConstructionSystem applies to
    /// its NEXT stocks only with construction.incremental OFF (the switch-off rule; D-054 WS1 superseded it — a head now
    /// progresses as far as each unit of material allows, ResolveHead) and the query to the world's. Each input is compared
    /// with the purse's whole holding of its good.</summary>
    public static bool MaterialsAvailable(
        IReadOnlyTable<GoodStockRow> stocks, GoodsConfig goods, SettlementId[] holders, ConstructionProjectEntry project)
    {
        for (int i = 0; i < project.Inputs.Length; i++)
        {
            ProjectInput input = project.Inputs[i];
            if (EmpireStockpile.Available(stocks, holders, new GoodId(goods.IdOf(input.Good))) < input.Qty) return false;
        }
        return true;
    }

    /// <summary>The inputs the purse <paramref name="holders"/> (<see cref="Holders"/>) lacks now, input order (empty when
    /// every input is present in full); <c>Held</c> is the purse's whole holding.</summary>
    public static MaterialShortfall[] Shortfalls(
        IReadOnlyTable<GoodStockRow> stocks, GoodsConfig goods, SettlementId[] holders, ConstructionProjectEntry project) =>
        Shortfalls(stocks, goods, holders, project, progress: 0);

    /// <summary>As above, against the REMAINING need of an entry at <paramref name="progress"/> (WS1: what has gone in is
    /// not needed again): <c>Required</c> is the input less <see cref="Delivered"/> at that progress.</summary>
    public static MaterialShortfall[] Shortfalls(
        IReadOnlyTable<GoodStockRow> stocks, GoodsConfig goods, SettlementId[] holders, ConstructionProjectEntry project,
        long progress)
    {
        var result = new List<MaterialShortfall>();
        for (int i = 0; i < project.Inputs.Length; i++)
        {
            ProjectInput input = project.Inputs[i];
            var good = new GoodId(goods.IdOf(input.Good));
            long need = Remaining(input.Qty, progress);
            long held = EmpireStockpile.Available(stocks, holders, good);
            if (held < need) result.Add(new MaterialShortfall(good, input.Good, need, held));
        }
        return [.. result];
    }

    /// <summary><see cref="Shortfalls(IReadOnlyTable{GoodStockRow}, GoodsConfig, SettlementId[], ConstructionProjectEntry)"/>
    /// for <paramref name="settlement"/>'s purse on <paramref name="world"/> — the reading every display uses.</summary>
    public static MaterialShortfall[] Shortfalls(
        IReadOnlyWorldState world, SimConfig cfg, SettlementId settlement, ConstructionProjectEntry project)
    {
        GoodsConfig goods = cfg.Goods ?? throw new ArgumentException("construction shortfalls need goods content", nameof(cfg));
        return Shortfalls(world.GoodStocks, goods, Holders(world, cfg, settlement), project);
    }

    /// <summary>
    /// The transient blocker the action surface shows for an AVAILABLE project, or null when nothing in the purse or the
    /// host stands in its way: the inputs short and, for a project that founds an institution (ADR-033 D6), the host's
    /// founding viability first ("needs 4000 adults (has 3100)"). An input names what the PURSE holds
    /// (<see cref="Holders"/>): "needs 40 timber (empire has 12)" where the settlement draws on its Empire, "needs 40
    /// timber (has 12)" where it draws on its own stock (no controller, or the switch off). WS1 (incremental construction
    /// on): the inputs are judged against the REMAINING need at <c>progress</c> (0 for a project not yet queued) and the
    /// labour clause is gone — labour is a pace, not a gate, so a settlement with few builders builds slowly rather than
    /// never. With incremental off (the M4-D gate), the per-turn labour clause stands as before when the turn's length is
    /// known. Information, not a disabled future control (ADR-033 D2) — each reading is the function ConstructionSystem
    /// resolves the head with.
    /// </summary>
    public static string? Blocker(
        IReadOnlyWorldState world, SimConfig cfg, SettlementId settlement, ConstructionProjectEntry project, double? dtYears) =>
        Blocker(world, cfg, settlement, project, dtYears, progress: 0);

    /// <summary>As above for an entry at <paramref name="progress"/> (the head's, from <see cref="ProgressOf(IReadOnlyWorldState, ConstructionQueueRow)"/>).</summary>
    public static string? Blocker(
        IReadOnlyWorldState world, SimConfig cfg, SettlementId settlement, ConstructionProjectEntry project, double? dtYears,
        long progress) =>
        Blocker(world, cfg, settlement, project, dtYears, Holders(world, cfg, settlement), DrawsOnEmpire(world, cfg, settlement), progress);

    /// <summary><see cref="Blocker(IReadOnlyWorldState, SimConfig, SettlementId, ConstructionProjectEntry, double?)"/> as
    /// <paramref name="viewer"/> may read it (P7): the materials against <see cref="HoldersSeenBy"/> — the Empire's purse
    /// where the viewer rules the settlement, the settlement's own stock ("has N") where it does not.</summary>
    public static string? BlockerSeenBy(
        IReadOnlyWorldState world, SimConfig cfg, PolityId viewer, SettlementId settlement, ConstructionProjectEntry project, double? dtYears) =>
        BlockerSeenBy(world, cfg, viewer, settlement, project, dtYears, progress: 0);

    /// <summary>As above for an entry at <paramref name="progress"/>.</summary>
    public static string? BlockerSeenBy(
        IReadOnlyWorldState world, SimConfig cfg, PolityId viewer, SettlementId settlement, ConstructionProjectEntry project, double? dtYears,
        long progress) =>
        Blocker(world, cfg, settlement, project, dtYears,
            HoldersSeenBy(world, cfg, viewer, settlement), DrawsOnEmpireSeenBy(world, cfg, viewer, settlement), progress);

    /// <summary>
    /// D-055 (batch-4 design §1.6, T1b): <see cref="Blocker(IReadOnlyWorldState, SimConfig, SettlementId, ConstructionProjectEntry, double?, long)"/>
    /// judged against what the hold rule leaves the entry — <paramref name="setAside"/>, by good id, the units of its purse SET
    /// ASIDE for projects ahead of it in the building order (SetAsideQuery: every set-aside for a would-be new order, the
    /// earlier heads' for a queued head; null or all zeros: the reading above, exactly). An input is short when the purse's
    /// holding less its set-aside is below the need, and its clause says so: "40 timber (empire has 52; 40 set aside for
    /// earlier projects)".
    /// </summary>
    public static string? Blocker(
        IReadOnlyWorldState world, SimConfig cfg, SettlementId settlement, ConstructionProjectEntry project, double? dtYears,
        long progress, long[]? setAside) =>
        Blocker(world, cfg, settlement, project, dtYears, Holders(world, cfg, settlement), DrawsOnEmpire(world, cfg, settlement), progress, setAside);

    /// <summary><see cref="BlockerSeenBy(IReadOnlyWorldState, SimConfig, PolityId, SettlementId, ConstructionProjectEntry, double?, long)"/>
    /// with the set-asides ahead of the entry (D-055) — read only on the viewer's OWN books (P7): where the settlement's purse
    /// is not the viewer's own (SetAsideQuery.ViewersOwnPurse — T1b fix round, finding F8: it does not rule the place, or
    /// another Empire's purse owns it), <paramref name="setAside"/> is ignored and the settlement's own stock is the reading,
    /// as above.</summary>
    public static string? BlockerSeenBy(
        IReadOnlyWorldState world, SimConfig cfg, PolityId viewer, SettlementId settlement, ConstructionProjectEntry project, double? dtYears,
        long progress, long[]? setAside) =>
        BlockerSeenBy(world, cfg, viewer, settlement, project, dtYears, progress, setAside, usedAhead: null);

    /// <summary>
    /// The blocker of a QUEUED HEAD (T1b fix round, 2026-10-17, finding F4): judged against the purse less what the heads ahead
    /// of it DRAW this step (<paramref name="usedAhead"/>, by good id) and SET ASIDE (<paramref name="setAside"/>) — the very
    /// deduction its card's Met reads (design §1.6: "the purse minus earlier heads' draws and holds") — each named apart:
    /// "4 stone (empire has 15; 5 used this turn by earlier projects, 10 set aside for earlier projects)". Both null: the
    /// reading above, exactly. On the viewer's OWN purse only (P7), as above.
    /// </summary>
    public static string? BlockerSeenBy(
        IReadOnlyWorldState world, SimConfig cfg, PolityId viewer, SettlementId settlement, ConstructionProjectEntry project, double? dtYears,
        long progress, long[]? setAside, long[]? usedAhead)
    {
        bool own = SetAsideQuery.ViewersOwnPurse(world, cfg, viewer, settlement);
        return Blocker(world, cfg, settlement, project, dtYears,
            HoldersSeenBy(world, cfg, viewer, settlement), DrawsOnEmpireSeenBy(world, cfg, viewer, settlement), progress,
            own ? setAside : null, own ? usedAhead : null);
    }

    private static string? Blocker(
        IReadOnlyWorldState world, SimConfig cfg, SettlementId settlement, ConstructionProjectEntry project, double? dtYears,
        SettlementId[] holders, bool empire, long progress, long[]? setAside = null, long[]? usedAhead = null)
    {
        GoodsConfig goods = cfg.Goods ?? throw new ArgumentException("a construction blocker needs goods content", nameof(cfg));
        bool incremental = Incremental(cfg);
        var parts = new List<string>();
        if (InstitutionContent.FoundsInstitution(project)
            && InstitutionViability.ToFound(world, cfg, settlement) is { Viable: false } viability)
            parts.Add(InstitutionViability.Shortfall(viability));
        string holder = empire ? "empire has" : "has";
        if (setAside is null && usedAhead is null)
            foreach (MaterialShortfall s in Shortfalls(world.GoodStocks, goods, holders, project, incremental ? progress : 0))
                parts.Add($"{Num(s.Required)} {s.Name} ({holder} {Num(s.Held)})");
        else
            foreach (ProjectInput input in project.Inputs)
            {
                // D-055: the holding less what is set aside for projects ahead (the Shortfalls reading when that is 0) — and,
                // for a queued head (T1b fix round, finding F4), less what the heads ahead of it draw this step, named apart.
                int good = goods.IdOf(input.Good);
                long need = Remaining(input.Qty, incremental ? progress : 0);
                long held = EmpireStockpile.Available(world.GoodStocks, holders, new GoodId(good));
                long aside = setAside is not null && good >= 0 && good < setAside.Length ? Math.Max(0, setAside[good]) : 0;
                long used = usedAhead is not null && good >= 0 && good < usedAhead.Length ? Math.Max(0, usedAhead[good]) : 0;
                if (checked(held - used - aside) >= need) continue;
                string why = used > 0 && aside > 0 ? $"; {Num(used)} used this turn by earlier projects, {Num(aside)} set aside for earlier projects"
                    : used > 0 ? $"; {Num(used)} used this turn by earlier projects"
                    : aside > 0 ? $"; {Num(aside)} set aside for earlier projects" : "";
                parts.Add($"{Num(need)} {input.Good} ({holder} {Num(held)}{why})");
            }
        if (!incremental && dtYears is { } dt && dt > 0.0)
        {
            double capacity = CapacityAdultYears(world, cfg, settlement, dt);
            if (capacity < project.LaborRequired)
                parts.Add($"{Num(project.LaborRequired)} adult-years of construction labour (has {Num(capacity)} this turn)");
        }
        return parts.Count == 0 ? null : "needs " + string.Join(", ", parts);
    }

    /// <summary>The order the player's build button and the AI policy issue — identical for both: stamped with
    /// the CURRENT turn (delivered to the next step), TargetId = the settlement, Amount = the project id.</summary>
    public static OrderRecord EnqueueOrder(IReadOnlyWorldState world, PolityId issuer, SettlementId settlement, int projectId) =>
        OrderRecord.From(world.Clock.Turn, issuer, OrderKind.EnqueueConstruction, settlement.Value, projectId);

    /// <summary>The settlement's queued projects, head (lowest slot) first.</summary>
    public static ConstructionQueueRow[] Queue(IReadOnlyWorldState world, SettlementId settlement)
    {
        var rows = new List<ConstructionQueueRow>();
        for (int i = 0; i < world.ConstructionQueue.Count; i++)
            if (world.ConstructionQueue[i].Settlement == settlement) rows.Add(world.ConstructionQueue[i]);
        rows.Sort(static (a, b) => a.Slot.CompareTo(b.Slot));   // slots are unique per settlement
        return [.. rows];
    }

    /// <summary>How many of <paramref name="projectId"/> the settlement has built.</summary>
    public static long Built(IReadOnlyWorldState world, SettlementId settlement, int projectId)
    {
        for (int i = 0; i < world.Structures.Count; i++)
            if (world.Structures[i].Settlement == settlement && world.Structures[i].ProjectId == projectId)
                return world.Structures[i].Count;
        return 0;
    }

    private static bool SettlementExists(IReadOnlyWorldState world, SettlementId settlement)
    {
        for (int i = 0; i < world.Settlements.Count; i++)
            if (world.Settlements[i].Id.Value == settlement.Value) return true;
        return false;
    }

    private static string Num(double v) => v.ToString("0.##", CultureInfo.InvariantCulture);
    private static string Num(long v) => v.ToString(CultureInfo.InvariantCulture);
}
