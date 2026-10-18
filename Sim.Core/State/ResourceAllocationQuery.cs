using Sim.Core.Kernel;
using Sim.Core.Systems;

namespace Sim.Core.State;

/// <summary>
/// D-054 WS2 — ONE EMPIRE'S BUILDING/ROADS SPLIT AS THE UI SHOWS IT (<see cref="ResourceAllocationQuery.ForEmpire"/>):
/// <c>BuildingsPercent</c> b in force for THIS turn's work (the last valid kind-12 order pending this turn, else the
/// world's row, else 100); <c>CommittedPercent</c> what the world holds (its row, or 100) and whether it HAS a row;
/// <c>QueuedPercent</c> the pending order's share (null: none); the road order the split was computed against
/// (<c>RoadOrder</c>: the queued one, or the road DRAFT when <c>FromDraft</c>); and <c>Split</c>, every eligible good's
/// supply, demands, shares and building grant — an ESTIMATE on the current stocks (the step re-reads them after
/// production, trade and housing).
/// </summary>
public sealed record AllocationView(
    PolityId Polity, int BuildingsPercent, int CommittedPercent, bool HasRow, int? QueuedPercent,
    OrderRecord? RoadOrder, bool FromDraft, EmpireAllocation Split)
{
    /// <summary>Whether the split in force is the default, building first (b = 100).</summary>
    public bool IsDefault => BuildingsPercent == ResourceAllocation.DefaultBuildingsPercent;
}

/// <summary>
/// D-054 WS2 — THE SPLIT, READ FOR THE UI: the same functions ConstructionSystem applies
/// (<see cref="ConstructionQuery.AllocationsWith"/>, <see cref="ConstructionQuery.AllocationFor"/>,
/// <see cref="RoadDevelopmentQuery.DeliveredOrderFor(IReadOnlyWorldState, SimConfig, IReadOnlyList{OrderRecord}, PolityId)"/>)
/// on the CURRENT world and the player's pending orders. The construction block's "may use up to" line reads ONLY the
/// queued road order; the roads preview passes the road DRAFT. Pure and read-only; it reads only the Empire's own row and
/// its own pending orders, plus its own purse (the P7 information class).
/// </summary>
public static class ResourceAllocationQuery
{
    /// <summary>
    /// <paramref name="polity"/>'s split for the turn the next End Turn plays: null when the split does not apply (the
    /// switch off, no Controls, no settlement of its own). <paramref name="pending"/> is the turn's not-yet-delivered
    /// batch: its kind-12 orders set b, its cancels and enqueues shape the heads (ConstructionQuery.PendingBook), and its
    /// delivered DevelopRoads order is the road demand — unless <paramref name="roadDraftPercent"/> is given, which stands
    /// for a road order of that percentage (the preview of the road control's draft). D_b and D_r are computed even at
    /// b = 100, so the UI can say what building work will take first.
    /// </summary>
    public static AllocationView? ForEmpire(
        IReadOnlyWorldState world, SimConfig cfg, PolityId polity, IReadOnlyList<OrderRecord>? pending, double dtYears,
        double? roadDraftPercent = null)
    {
        if (!ResourceAllocation.Enabled(cfg) || world.Controls.Count == 0) return null;
        Table<ResourceAllocationRow> rows = ConstructionQuery.AllocationsWith(world, cfg, world.ResourceAllocations, pending);
        int b = ResourceAllocation.EffectivePercent(rows, polity);
        int committed = ResourceAllocation.EffectivePercent(world, polity);
        bool hasRow = ResourceAllocation.IndexOf(world.ResourceAllocations, polity) >= 0;
        int? queued = null;
        if (pending is not null)
            for (int o = 0; o < pending.Count; o++)
                if (pending[o].ActorId == polity.Value && ConstructionQuery.AllocationOrderValid(world, cfg, pending[o]))
                    queued = (int)pending[o].Amount;

        OrderRecord? road;
        bool fromDraft = roadDraftPercent is { } d && d > 0.0 && d <= 100.0;
        road = fromDraft
            ? RoadDevelopmentQuery.DevelopOrder(world, polity, roadDraftPercent!.Value)
            : RoadDevelopmentQuery.DeliveredOrderFor(world, cfg, pending ?? [], polity);

        IReadOnlyTable<ConstructionQueueRow> queue = ConstructionQuery.PendingBook(world, cfg, pending, out IReadOnlyTable<ConstructionProgressRow> progress);
        EmpireAllocation? split = ConstructionQuery.AllocationFor(world, cfg, polity, queue, progress, world.GoodStocks, b, road, dtYears);
        return split is null ? null : new AllocationView(polity, b, committed, hasRow, queued, road, fromDraft, split);
    }
}
