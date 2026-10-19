using Sim.Core.Kernel;
using Sim.Core.Systems;

namespace Sim.Core.State;

/// <summary>
/// One reading of THE holds walk for the player-facing text (<see cref="SetAsideQuery.Read"/>): the walk itself
/// (<see cref="Walk"/>) and the queue and progress it walked — the pending book (<see cref="Queue"/>, <see cref="Progress"/>),
/// whose progress rows give each entry's EnqueuedTurn (the building order's first key).
/// </summary>
public sealed record SetAsideReading(
    HoldsWalkResult Walk, IReadOnlyTable<ConstructionQueueRow> Queue, IReadOnlyTable<ConstructionProgressRow> Progress);

/// <summary>
/// D-055 (2026-10-16; batch-4 design §1.6, T1b) — WHAT THE HOLD RULE SETS ASIDE, AS THE PLAYER'S DISPLAYS READ IT. Read-only:
/// every reading here calls THE walk (<see cref="ConstructionQuery.HoldsWalk"/>) — never a second rule — on the CURRENT world
/// and the player's pending orders, exactly as <see cref="ConstructionQuery.HeadStatus"/> sets it up (the pending book, the
/// split each Empire's pending orders put in force), so a card, a blocker or a preview can never say something the head's
/// status contradicts. An ESTIMATE, like every queue reading: the step re-reads the stocks production, trade and housing
/// leave. The P7 rule (the information class) is the CALLER's: a display reads the viewer's OWN purse only
/// (<see cref="ViewerTotals"/> filters the published table itself). Arrays are indexed by good id; no dictionary, no LINQ.
/// </summary>
public static class SetAsideQuery
{
    /// <summary>
    /// THE WALK the coming step would run, as the UI estimates it: <paramref name="world"/>'s queue with
    /// <paramref name="pending"/> applied as the intake will (ConstructionQuery.PendingBook), each Empire's split as the
    /// pending kind-12 orders and the pending road order put it in force (ConstructionQuery.StepAllocations), on the world's
    /// stocks — the very setup of <see cref="ConstructionQuery.HeadStatus"/>, so its heads agree with every head's status.
    /// </summary>
    public static SetAsideReading Read(IReadOnlyWorldState world, SimConfig cfg, IReadOnlyList<OrderRecord>? pending, double dtYears)
    {
        IReadOnlyTable<ConstructionQueueRow> queue = ConstructionQuery.PendingBook(world, cfg, pending, out IReadOnlyTable<ConstructionProgressRow> progress);
        long[]?[] grants = ConstructionQuery.GrantsLeft(ConstructionQuery.StepAllocations(world, cfg, queue, progress, world.GoodStocks,
            ConstructionQuery.AllocationsWith(world, cfg, world.ResourceAllocations, pending), pending ?? [], dtYears));
        return new SetAsideReading(ConstructionQuery.HoldsWalk(world, cfg, queue, progress, world.GoodStocks, dtYears, grants), queue, progress);
    }

    /// <summary>
    /// THE HEADS OF <paramref name="polity"/>'S PURSE under the building grants <paramref name="grants"/> (by good id; null:
    /// no split) — the walk the split's forecast runs (ConstructionQuery.AllocationFor: its BuildingDraws and BuildingHolds
    /// are these heads' sums), for naming the projects a road order's preview finds set aside. In the building order.
    /// </summary>
    public static HoldsWalkHead[] HeadsUnder(
        IReadOnlyWorldState world, SimConfig cfg, PolityId polity, IReadOnlyList<OrderRecord>? pending, double dtYears, long[]? grants)
    {
        IReadOnlyTable<ConstructionQueueRow> queue = ConstructionQuery.PendingBook(world, cfg, pending, out IReadOnlyTable<ConstructionProgressRow> progress);
        var aligned = new long[]?[world.Polities.Count];
        for (int k = 0; k < aligned.Length; k++)
            if (world.Polities[k].Id.Value == polity.Value && grants is not null) aligned[k] = (long[])grants.Clone();
        HoldsWalkResult walk = ConstructionQuery.HoldsWalk(world, cfg, queue, progress, world.GoodStocks, dtYears, aligned);
        int purse = Reservations.PolityPurse(polity);
        var heads = new List<HoldsWalkHead>();
        for (int h = 0; h < walk.Heads.Length; h++) if (walk.Heads[h].Purse == purse) heads.Add(walk.Heads[h]);
        return [.. heads];
    }

    /// <summary>The grants the split's forecast walked within (ConstructionQuery.AllocationFor): the building grants when the
    /// split holds anything back and the starvation guard did not fire, otherwise none — so the heads of
    /// <see cref="HeadsUnder"/> sum to <paramref name="split"/>'s BuildingDraws and BuildingHolds.</summary>
    public static long[]? ForecastGrants(EmpireAllocation split)
    {
        if (split.BuildingGrants is not { } grants || split.RoadPaysNothingFromItsShare) return null;
        for (int i = 0; i < split.Splits.Length; i++) if (split.Splits[i].HoldsBack) return grants;
        return null;
    }

    /// <summary>The index in <paramref name="walk"/>.Heads of <paramref name="settlement"/>'s head, or −1.</summary>
    public static int HeadIndexOf(HoldsWalkResult walk, SettlementId settlement)
    {
        for (int h = 0; h < walk.Heads.Length; h++) if (walk.Heads[h].Entry.Settlement == settlement) return h;
        return -1;
    }

    /// <summary>D-058: the index in <paramref name="walk"/>.Heads of the walked entry <paramref name="entry"/> — its settlement's
    /// head, or an entry a completion promotes within the step — or −1 when the walk does not reach it.</summary>
    public static int EntryIndexOf(HoldsWalkResult walk, ConstructionQueueRow entry)
    {
        for (int h = 0; h < walk.Heads.Length; h++) if (walk.Heads[h].Entry == entry) return h;
        return -1;
    }

    /// <summary>
    /// What the heads AHEAD of head <paramref name="head"/> (an index in <paramref name="walk"/>.Heads) in its own purse take
    /// of each good, by good id: their set-asides, plus their draws when <paramref name="withDraws"/> — the card's reading of
    /// a queued head ("the purse minus earlier heads' draws and holds").
    /// </summary>
    public static long[] Ahead(HoldsWalkResult walk, int head, bool withDraws)
    {
        int purse = walk.Heads[head].Purse;
        var sum = new long[walk.Heads[head].Holds.Length];
        for (int e = 0; e < head; e++)
        {
            HoldsWalkHead x = walk.Heads[e];
            if (x.Purse != purse) continue;
            for (int g = 0; g < sum.Length && g < x.Holds.Length; g++)
                sum[g] = checked(sum[g] + x.Holds[g] + (withDraws ? x.Draws[g] : 0));
        }
        return sum;
    }

    /// <summary>
    /// What the heads AHEAD of head <paramref name="head"/> in its own purse DRAW this step, by good id (their draws only — T1b
    /// fix round, 2026-10-17, findings F4 and F7): the units the step uses for earlier projects before this head is reached, so
    /// a card's blocker and a waiting line can name them apart from what is set aside.
    /// </summary>
    public static long[] DrawnAhead(HoldsWalkResult walk, int head)
    {
        int purse = walk.Heads[head].Purse;
        var sum = new long[walk.Heads[head].Draws.Length];
        for (int e = 0; e < head; e++)
        {
            HoldsWalkHead x = walk.Heads[e];
            if (x.Purse != purse) continue;
            for (int g = 0; g < sum.Length && g < x.Draws.Length; g++) sum[g] = checked(sum[g] + x.Draws[g]);
        }
        return sum;
    }

    /// <summary>The heads of purse <paramref name="purse"/> that DRAW good <paramref name="good"/> this step, in the building
    /// order, among the first <paramref name="before"/> heads of the walk, each with its units (T1b fix round, finding F4: the
    /// card's note names them).</summary>
    public static (QueueEntryRef Head, long Units)[] Drawers(HoldsWalkResult walk, int purse, int good, int before)
    {
        var result = new List<(QueueEntryRef, long)>();
        int end = Math.Min(Math.Max(0, before), walk.Heads.Length);
        for (int h = 0; h < end; h++)
        {
            HoldsWalkHead x = walk.Heads[h];
            if (x.Purse != purse || x.DrawOf(good) <= 0) continue;
            result.Add((new QueueEntryRef(x.Entry.Settlement, x.Entry.Slot, x.Entry.ProjectId), x.DrawOf(good)));
        }
        return [.. result];
    }

    /// <summary>
    /// THE TABLE THE COMING STEP WOULD PUBLISH (T1b fix round, 2026-10-17, finding F2): the rows of <see cref="Read"/>'s walk —
    /// the queue with the pending cancels and enqueues applied, each Empire's split as the pending orders put it in force — as
    /// a table, for the readers that ask what road development will find set aside (RoadDevelopmentQuery.SetAsideWithin): the
    /// step runs construction first, so road development reads THIS step's set-asides, not the last step's published ones.
    /// Empty with the hold rule off (<see cref="Reservations.Enabled"/>): batch 3, which never subtracts a set-aside.
    /// </summary>
    public static IReadOnlyTable<ConstructionHoldRow> StepTable(IReadOnlyWorldState world, SimConfig cfg, IReadOnlyList<OrderRecord>? pending, double dtYears)
    {
        var table = new Table<ConstructionHoldRow>();
        if (!Reservations.Enabled(cfg)) return table;
        foreach (ConstructionHoldRow row in Read(world, cfg, pending, dtYears).Walk.Rows()) table.Add(row);
        return table;
    }

    /// <summary>
    /// Whether <paramref name="settlement"/>'s purse is <paramref name="viewer"/>'s OWN (P7; T1b fix round, 2026-10-17, finding
    /// F8): the viewer rules the place and its purse key is the viewer's Empire purse or the place's own single-settlement
    /// purse. False in a world with two control rows for one place for every controller but the LOWEST, whose purse the place
    /// belongs to (ConstructionQuery.PurseHolders) — so no hold text there names a foreign queue entry.
    /// </summary>
    public static bool ViewersOwnPurse(IReadOnlyWorldState world, SimConfig cfg, PolityId viewer, SettlementId settlement)
    {
        if (!EmpireQuery.ControlsSettlement(world, viewer, settlement)) return false;
        int key = ConstructionQuery.PurseKey(world, cfg, settlement);
        return key < 0 || key == Reservations.PolityPurse(viewer);
    }

    /// <summary>Every set-aside of purse <paramref name="purse"/> in <paramref name="walk"/>, by good id (empty when the purse
    /// has no head) — what a would-be new order finds set aside: every head is ahead of it.</summary>
    public static long[] HeldIn(HoldsWalkResult walk, int purse)
    {
        int k = walk.IndexOfPurse(purse);
        return k < 0 ? [] : (long[])walk.Held[k].Clone();
    }

    /// <summary>The heads of purse <paramref name="purse"/> that set aside good <paramref name="good"/>, in the building order,
    /// among the first <paramref name="before"/> heads of the walk (all of them for −1), each with its units.</summary>
    public static (QueueEntryRef Head, long Units)[] Holders(HoldsWalkResult walk, int purse, int good, int before = -1)
    {
        var result = new List<(QueueEntryRef, long)>();
        int end = before < 0 ? walk.Heads.Length : Math.Min(before, walk.Heads.Length);
        for (int h = 0; h < end; h++)
        {
            HoldsWalkHead x = walk.Heads[h];
            if (x.Purse != purse || x.HoldOf(good) <= 0) continue;
            result.Add((new QueueEntryRef(x.Entry.Settlement, x.Entry.Slot, x.Entry.ProjectId), x.HoldOf(good)));
        }
        return [.. result];
    }

    /// <summary>
    /// THE VIEWER'S OWN SET-ASIDES, from the published table (ConstructionHolds — what the last step set aside): the rows of
    /// <paramref name="viewer"/>'s Empire purse and of the single-settlement purses of the places it controls (construction's
    /// purse switch off), summed per good, ascending good id, each with units &gt; 0. Empty with the hold rule off
    /// (<see cref="Reservations.Enabled"/>), whatever the table holds. P7: no other purse is read.
    /// </summary>
    public static (int Good, long Units)[] ViewerTotals(IReadOnlyWorldState world, SimConfig cfg, PolityId viewer)
    {
        if (!Reservations.Enabled(cfg) || viewer.Value < 0) return [];
        int own = Reservations.PolityPurse(viewer);
        var goods = new List<int>();
        var units = new List<long>();
        IReadOnlyTable<ConstructionHoldRow> holds = world.ConstructionHolds;
        for (int i = 0; i < holds.Count; i++)
        {
            ConstructionHoldRow row = holds[i];
            bool mine = row.Purse >= 0
                ? row.Purse == own
                : EmpireQuery.ControlsSettlement(world, viewer, new SettlementId(-1 - row.Purse));
            if (!mine || row.Units <= 0) continue;
            int at = goods.IndexOf(row.Good);
            if (at < 0) { goods.Add(row.Good); units.Add(row.Units); }
            else units[at] = checked(units[at] + row.Units);
        }
        var result = new (int, long)[goods.Count];
        for (int i = 0; i < result.Length; i++) result[i] = (goods[i], units[i]);
        Array.Sort(result, static (a, b) => a.Item1.CompareTo(b.Item1));
        return result;
    }

    /// <summary>The entry's EnqueuedTurn in <paramref name="reading"/>'s pending book (−1 for an entry with no progress row).</summary>
    public static long EnqueuedTurnOf(SetAsideReading reading, QueueEntryRef entry) =>
        ConstructionQuery.EnqueuedTurnOf(reading.Progress, new ConstructionQueueRow(entry.Settlement, entry.Slot, entry.ProjectId));

    /// <summary>The units at good id <paramref name="good"/> of <paramref name="byGood"/> (0 outside it).</summary>
    public static long At(long[]? byGood, int good) => byGood is not null && good >= 0 && good < byGood.Length ? byGood[good] : 0;
}
