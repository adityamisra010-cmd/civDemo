using Sim.Core.Kernel;
using Sim.Core.Systems;

namespace Sim.Core.State;

/// <summary>
/// D-054 WS2 — ONE EMPIRE'S BUILDING/ROADS SPLIT FOR ONE STEP (<see cref="ConstructionQuery.AllocationFor"/>): its
/// effective building share <c>BuildingsPercent</c>; the DevelopRoads order road development will apply for it this step
/// (<c>RoadOrder</c>, null when none); the eligible goods (<c>Goods</c>, ascending ids) and each one's
/// <see cref="AllocationSplit"/> (<c>Splits</c>, aligned with Goods); and the GRANTS heads draw within
/// (<c>BuildingGrants</c>, indexed by good id — G_b for an eligible good, long.MaxValue for any other — or null when the
/// split cannot bind: b = 100). The FORECAST of the step, indexed by good id: <c>BuildingDraws</c> what the Empire's heads
/// draw within those grants (D_b where nothing binds), and <c>RoadCharges</c> what road development would charge the road
/// order on what building work leaves (S − BuildingDraws; all zeros without a road order). <c>RoadPaysNothingFromItsShare</c>
/// — THE STARVATION GUARD fired: the split would have held materials back from building work for a road order that, on
/// what building work would leave it, pays nothing at all, so it withholds nothing (its RoadDemand is 0 and every grant is
/// D_b).
/// </summary>
public sealed record EmpireAllocation(
    PolityId Polity, int BuildingsPercent, OrderRecord? RoadOrder, int[] Goods, AllocationSplit[] Splits, long[]? BuildingGrants,
    long[] BuildingDraws, long[] RoadCharges, bool RoadPaysNothingFromItsShare = false)
{
    /// <summary>The split of good <paramref name="good"/>, or null when it is not an eligible good.</summary>
    public AllocationSplit? SplitOf(int good)
    {
        for (int i = 0; i < Goods.Length; i++) if (Goods[i] == good) return Splits[i];
        return null;
    }

    /// <summary>What building work is forecast to draw of good <paramref name="good"/> this step (0 for an unknown id).</summary>
    public long BuildingDrawOf(int good) => good >= 0 && good < BuildingDraws.Length ? BuildingDraws[good] : 0;

    /// <summary>What road development is forecast to charge of good <paramref name="good"/> this step, on what building work
    /// leaves (0 for an unknown id).</summary>
    public long RoadChargeOf(int good) => good >= 0 && good < RoadCharges.Length ? RoadCharges[good] : 0;
}

public static partial class ConstructionQuery
{
    /// <summary>
    /// THE SetResourceAllocation PREDICATE (kind 12; ADR-033 D2 — one predicate, two callers: ConstructionSystem at
    /// delivery and the UI's emitter). It holds iff the building/roads split is on (<see cref="ResourceAllocation.Enabled"/>
    /// — with it off a delivered kind-12 order changes nothing and the UI never emits one), the payload is well-formed
    /// (the building share a whole number in [0, 100] — re-checked here because an in-memory OrderLog.Append never
    /// validates), the order targets its issuer (TargetId = ActorId: an Empire sets its own split, the SetTaxRate rule),
    /// and the issuer is a ROSTER Empire on <paramref name="prev"/>. Otherwise the order changes nothing.
    /// </summary>
    public static bool AllocationOrderValid(IReadOnlyWorldState prev, SimConfig cfg, OrderRecord order)
    {
        if (order.Kind != OrderKind.SetResourceAllocation) return false;
        if (!ResourceAllocation.Enabled(cfg)) return false;
        if (order.TargetId < 0 || order.TargetId != order.ActorId) return false;
        if (!OrderLog.AllocationPercentValid(order.Amount)) return false;
        return EmpireQuery.TryGetCommandSource(prev, order.Actor, out _);
    }

    /// <summary>
    /// The split rows as a batch of kind-12 orders leaves them: a copy of <paramref name="rows"/> with every order of
    /// <paramref name="orders"/> that <see cref="AllocationOrderValid"/> admits on <paramref name="prev"/> upserted in log
    /// order (<see cref="ResourceAllocation.Upsert"/> — the last valid order of an Empire wins). The UI's view of the
    /// pending book; ConstructionSystem applies the same upserts to its NEXT table at step 2.
    /// </summary>
    public static Table<ResourceAllocationRow> AllocationsWith(
        IReadOnlyWorldState prev, SimConfig cfg, IReadOnlyTable<ResourceAllocationRow> rows, IReadOnlyList<OrderRecord>? orders)
    {
        var copy = new Table<ResourceAllocationRow>(rows.Count + 1);
        for (int i = 0; i < rows.Count; i++) copy.Add(rows[i]);
        if (orders is null) return copy;
        for (int o = 0; o < orders.Count; o++)
            if (AllocationOrderValid(prev, cfg, orders[o]))
                ResourceAllocation.Upsert(copy, orders[o].Actor, (int)orders[o].Amount);
        return copy;
    }

    /// <summary>
    /// THE SPLIT OF ONE EMPIRE FOR ONE STEP (D-054 WS2; <see cref="ResourceAllocation"/>'s header), at building share
    /// <paramref name="buildingsPercent"/> and with the road order <paramref name="roadOrder"/> (null: none). Null when the
    /// split does not apply — the switch is off, the world has no Controls (no Empire purse to split), or the Empire
    /// controls no settlement. Per good of the Empire's purse (every settlement it controls on <paramref name="prev"/>):
    /// <list type="bullet">
    /// <item>S — the purse's holding in <paramref name="stocks"/> (ConstructionSystem: its NEXT stocks before any head
    ///   draws);</item>
    /// <item>D_b — what the Empire's OWN queue heads in <paramref name="queue"/>/<paramref name="progress"/> would draw with
    ///   no split: each resolved by <see cref="ResolveHead"/> in THE BUILDING ORDER (<see cref="HeadsInBuildingOrder"/>,
    ///   gates applied) against what the earlier ones left of S — the Empire's purse is no other head's, so this is exactly
    ///   what they would draw;</item>
    /// <item>D_r — <see cref="RoadDevelopmentQuery.SimulateCharges"/> of the road order against S (0 without one);</item>
    /// <item>the split, <see cref="ResourceAllocation.Split"/>, of each ELIGIBLE good.</item>
    /// </list>
    /// THE FORECAST: the heads are resolved again within the grants (each against the least of what is left of S and of
    /// its grant — exactly the step's own resolution, since no other head draws on this purse), giving
    /// <see cref="EmpireAllocation.BuildingDraws"/>, and the road order's charges on what they leave,
    /// <see cref="EmpireAllocation.RoadCharges"/>.
    /// THE STARVATION GUARD: a road order that can pay nothing withholds nothing. It holds at S by construction (D_r = 0
    /// there gives G_b = min(D_b, S)); and when the split holds anything back from building work but road development
    /// would charge NOTHING on what building work then leaves it — ADR-032's min-ratio can floor a step to nothing, e.g. a
    /// two-tool charge on a quarter of its timber — the split is recomputed with D_r = 0, so building work takes D_b and
    /// road works, finding less, still pay nothing (the charge never rises as the holding falls).
    /// The grants are null at b = 100 (G_b = min(D_b, S) never binds). A pure function: it reads and allocates only.
    /// </summary>
    public static EmpireAllocation? AllocationFor(
        IReadOnlyWorldState prev, SimConfig cfg, PolityId polity,
        IReadOnlyTable<ConstructionQueueRow> queue, IReadOnlyTable<ConstructionProgressRow> progress,
        IReadOnlyTable<GoodStockRow> stocks, int buildingsPercent, OrderRecord? roadOrder, double dtYears)
    {
        if (!ResourceAllocation.Enabled(cfg) || cfg.Goods is not { } goods || prev.Controls.Count == 0) return null;
        SettlementId[] purse = RoadDevelopmentQuery.PayingSettlements(prev, polity);
        if (purse.Length == 0) return null;

        int width = MaxGoodId(goods) + 1;
        var supply = new long[width];
        for (int i = 0; i < goods.Goods.Length; i++)
        {
            int id = goods.Goods[i].Id;
            if (id >= 0) supply[id] = EmpireStockpile.Available(stocks, purse, new GoodId(id));
        }

        // D_b: the Empire's own heads, oldest order first, each against what the earlier ones left (no split).
        ConstructionQueueRow[] heads = HeadsInBuildingOrder(prev, queue, progress);
        long[] demand = OwnHeadDraws(prev, cfg, goods, polity, heads, queue, progress, stocks, dtYears, supply, grants: null);

        // D_r: road development's own charges for the delivered order, against S.
        long[] roads = roadOrder is { } order
            ? RoadDevelopmentQuery.SimulateCharges(prev, cfg, polity, order.Amount, supply)
            : new long[width];

        int[] ids = ResourceAllocation.EligibleGoodIds(cfg);
        (AllocationSplit[] splits, long[]? grants) = SplitAll(ids, width, supply, demand, roads, buildingsPercent);

        // The forecast: what the heads draw within the grants, and what the road order is charged on what they leave.
        long[] drawn = grants is null || !AnyHoldsBack(splits)
            ? demand
            : OwnHeadDraws(prev, cfg, goods, polity, heads, queue, progress, stocks, dtYears, supply, grants);
        long[] charges = RoadChargesOnRemainder(prev, cfg, polity, roadOrder, supply, drawn);

        // The starvation guard on what the split leaves road works.
        bool guarded = false;
        if (grants is not null && roadOrder is not null && AnyHoldsBack(splits) && AllZero(charges))
        {
            (splits, grants) = SplitAll(ids, width, supply, demand, new long[width], buildingsPercent);
            drawn = demand;
            charges = RoadChargesOnRemainder(prev, cfg, polity, roadOrder, supply, drawn);
            guarded = true;
        }
        return new EmpireAllocation(polity, buildingsPercent, roadOrder, ids, splits, grants, drawn, charges, guarded);
    }

    /// <summary>What the heads of <paramref name="polity"/> among <paramref name="heads"/> (the building order) draw of each
    /// good id from its purse holding <paramref name="supply"/>: each resolved by <see cref="ResolveHead"/> against the least
    /// of what the earlier ones left of the supply and (when <paramref name="grants"/> is given) of the grants.</summary>
    private static long[] OwnHeadDraws(
        IReadOnlyWorldState prev, SimConfig cfg, GoodsConfig goods, PolityId polity, ConstructionQueueRow[] heads,
        IReadOnlyTable<ConstructionQueueRow> queue, IReadOnlyTable<ConstructionProgressRow> progress,
        IReadOnlyTable<GoodStockRow> stocks, double dtYears, long[] supply, long[]? grants)
    {
        int width = supply.Length;
        var left = (long[])supply.Clone();
        var budget = new long[width];
        var drawn = new long[width];
        for (int h = 0; h < heads.Length; h++)
        {
            if (!EmpireQuery.TryGetController(prev, heads[h].Settlement, out PolityId controller)
                || controller.Value != polity.Value) continue;
            for (int g = 0; g < width; g++)
                budget[g] = grants is null || g >= grants.Length ? left[g] : Math.Min(left[g], grants[g] - drawn[g]);
            HeadResolution? r = ResolveHead(prev, cfg, heads[h].Settlement, queue, progress, stocks, dtYears, budget);
            if (r is null || !r.Progresses || r.Project is not { } project) continue;
            for (int i = 0; i < project.Inputs.Length; i++)
            {
                int g = goods.IdOf(project.Inputs[i].Good);
                if (g < 0 || g >= width) continue;
                left[g] -= r.Draws[i];
                drawn[g] += r.Draws[i];
            }
        }
        return drawn;
    }

    /// <summary>Each eligible good's split (aligned with <paramref name="ids"/>) and the grants by good id (long.MaxValue
    /// for any other good), or null grants at the default share (b = 100: the split never binds).</summary>
    private static (AllocationSplit[] Splits, long[]? Grants) SplitAll(
        int[] ids, int width, long[] supply, long[] demand, long[] roads, int buildingsPercent)
    {
        var splits = new AllocationSplit[ids.Length];
        long[]? grants = null;
        if (buildingsPercent != ResourceAllocation.DefaultBuildingsPercent)
        {
            grants = new long[width];
            Array.Fill(grants, long.MaxValue);
        }
        for (int i = 0; i < ids.Length; i++)
        {
            int g = ids[i];
            splits[i] = ResourceAllocation.Split(supply[g], demand[g], g < roads.Length ? roads[g] : 0, buildingsPercent);
            if (grants is not null) grants[g] = splits[i].BuildingGrant;
        }
        return (splits, grants);
    }

    /// <summary>The road order's charges (<see cref="RoadDevelopmentQuery.SimulateCharges"/>) on the purse's holding less
    /// building work's draws; all zeros without a road order.</summary>
    private static long[] RoadChargesOnRemainder(
        IReadOnlyWorldState prev, SimConfig cfg, PolityId polity, OrderRecord? roadOrder, long[] supply, long[] drawn)
    {
        if (roadOrder is not { } order) return new long[supply.Length];
        var remainder = new long[supply.Length];
        for (int g = 0; g < remainder.Length; g++) remainder[g] = supply[g] - drawn[g];
        return RoadDevelopmentQuery.SimulateCharges(prev, cfg, polity, order.Amount, remainder);
    }

    private static bool AnyHoldsBack(AllocationSplit[] splits)
    {
        for (int i = 0; i < splits.Length; i++) if (splits[i].HoldsBack) return true;
        return false;
    }

    private static bool AllZero(long[] values)
    {
        for (int i = 0; i < values.Length; i++) if (values[i] != 0) return false;
        return true;
    }

    /// <summary>
    /// STEP 5 OF ConstructionSystem (D-054 WS2) — and the UI's estimate of it: every roster Empire's split for the step,
    /// aligned with <paramref name="prev"/>.Polities. An Empire whose effective building share in <paramref name="rows"/>
    /// is 100 (no row, or an explicit 100) gets null — the split cannot bind, so neither its road forecast nor its heads'
    /// demand is computed and its heads draw exactly as with the switch off. Null when the split does not apply at all
    /// (switch off; no Controls). <paramref name="orders"/> is the step's batch (the UI: the pending orders) — the road
    /// order each Empire's split reads is the one road development will apply
    /// (<see cref="RoadDevelopmentQuery.DeliveredOrderFor(IReadOnlyWorldState, SimConfig, IReadOnlyList{OrderRecord}, PolityId)"/>).
    /// </summary>
    public static EmpireAllocation?[]? StepAllocations(
        IReadOnlyWorldState prev, SimConfig cfg,
        IReadOnlyTable<ConstructionQueueRow> queue, IReadOnlyTable<ConstructionProgressRow> progress,
        IReadOnlyTable<GoodStockRow> stocks, IReadOnlyTable<ResourceAllocationRow> rows,
        IReadOnlyList<OrderRecord> orders, double dtYears)
    {
        if (!ResourceAllocation.Enabled(cfg) || prev.Controls.Count == 0) return null;
        var result = new EmpireAllocation?[prev.Polities.Count];
        for (int k = 0; k < result.Length; k++)
        {
            PolityId polity = prev.Polities[k].Id;
            int b = ResourceAllocation.EffectivePercent(rows, polity);
            if (b == ResourceAllocation.DefaultBuildingsPercent) continue;
            OrderRecord? road = RoadDevelopmentQuery.DeliveredOrderFor(prev, cfg, orders, polity);
            result[k] = AllocationFor(prev, cfg, polity, queue, progress, stocks, b, road, dtYears);
        }
        return result;
    }

    /// <summary>The working grants of a step: a copy of each Empire's <see cref="EmpireAllocation.BuildingGrants"/>
    /// (aligned with the allocations; null where there is no cap), which the step spends down as heads draw.</summary>
    public static long[]?[] GrantsLeft(EmpireAllocation?[]? allocations)
    {
        if (allocations is null) return [];
        var left = new long[]?[allocations.Length];
        for (int k = 0; k < allocations.Length; k++)
            if (allocations[k]?.BuildingGrants is { } g) left[k] = (long[])g.Clone();
        return left;
    }

    /// <summary>The working grants of the Empire that controls <paramref name="settlement"/> on <paramref name="prev"/>
    /// (the head's purse), or null — no cap — for a stateless settlement or an Empire with no split.</summary>
    public static long[]? GrantsFor(IReadOnlyWorldState prev, long[]?[] grants, SettlementId settlement)
    {
        if (grants.Length == 0 || !EmpireQuery.TryGetController(prev, settlement, out PolityId controller)) return null;
        for (int k = 0; k < prev.Polities.Count && k < grants.Length; k++)
            if (prev.Polities[k].Id.Value == controller.Value) return grants[k];
        return null;
    }

    /// <summary>Spends a resolved head's draws from its Empire's working grants (no-op without a cap).</summary>
    public static void SpendGrants(long[]? grants, GoodsConfig goods, ConstructionProjectEntry project, long[] draws)
    {
        if (grants is null) return;
        for (int i = 0; i < project.Inputs.Length; i++)
        {
            int g = goods.IdOf(project.Inputs[i].Good);
            if (g >= 0 && g < grants.Length && grants[g] != long.MaxValue) grants[g] -= draws[i];
        }
    }
}
