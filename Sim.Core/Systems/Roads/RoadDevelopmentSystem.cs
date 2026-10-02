using Sim.Core.Kernel;
using Sim.Core.State;

namespace Sim.Core.Systems.Roads;

/// <summary>Tables owned by <see cref="RoadDevelopmentSystem"/> (ADR-003 ownership by construction).
/// GoodStocks is the SANCTIONED SHARED STOCK — this system is its SIXTH holder (SystemCatalog
/// records the field-level split: Amount via Ledger SINK only, reason ConstructionMaterials).</summary>
public sealed record RoadDevelopmentTables(
    Table<TransportEdgeRow> Edges, Table<RoadDevelopmentRow> Log, Table<GoodStockRow> GoodStocks);

/// <summary>
/// ADR-032 — THE ONE AUTHORITATIVE ROAD-DEVELOPMENT OPERATION (the Director's final transport
/// rulings 5–17). Roads are developed by a civilization-level ACTION, never by a builder unit,
/// and the action is the same for the player and for the AI: a DevelopRoads order (OrderKind 8)
/// asking for X% of eligible inter-city transport demand.
///
/// THE STEP, in order-log order, first DevelopRoads per Empire per turn:
///  1. PLAN with <see cref="RoadDevelopmentQuery.Plan(IReadOnlyWorldState, IReadOnlyTable{TransportEdgeRow}, Research.ResearchContent, RoadsConfig, GoodsConfig, PolityId, double)"/>
///     — the single selection the UI preview and the AI policy also read. Inputs are PREV
///     (research, controls, the cached pairwise travel costs, last turn's realised trade) plus
///     this system's NEXT route table, so a second Empire's order in the same step sees the
///     first's modernization.
///  2. PAY, step by step in plan order, from the ISSUING CIVILIZATION's goods (the settlements
///     it controls, NEAREST-FIRST to the route, ties by id — <see cref="RoadDevelopmentQuery.PayingSettlements(IReadOnlyWorldState, PolityId, SettlementId, SettlementId)"/>), each
///     unit leaving through Ledger.Flow (sink, ReasonIds.ConstructionMaterials). The step's cost
///     is that of completing the modernization; if only a proportion p &lt; 1 is affordable,
///     floor(p × units) of each good is charged and exactly the modernization those units buy
///     is performed, AND THE ORDER ENDS (its purse is exhausted in at least one good). Nothing is
///     overspent, no debt exists, no cost moves to another civilization.
///  3. MODERNIZE IN PLACE: the route row keeps its id; its fraction toward the target grows; at
///     1 its class becomes the target. A bare baseline path gets its route row (its stable id) on
///     its first development — the same physical route, never a parallel one. The row's
///     effective cost factor and capacity are re-derived by <see cref="RoadPerformance"/>.
///  4. LOG one RoadDevelopmentRow per modernized route (which also moves the combined network
///     revision, so catchments and pairwise travel costs are recomputed next turn — D-016).
///
/// Modernization is atomic within the step (no multi-turn project, no builder): an order stamped
/// turn t shows its effect in the state of turn t+1.
///
/// WHAT IT NEVER DOES. It never runs without an order. It never charges for the free baseline
/// (DirtPath costs nothing; ruling 2). Research completion builds nothing: it only widens what
/// the next order may choose. It writes no owner and never reads or changes Condition. No RNG;
/// no dt (a discrete action). Inert without roads tuning, research content or goods content.
/// </summary>
public sealed class RoadDevelopmentSystem(SimConfig cfg) : ISimSystem<RoadDevelopmentTables>
{
    /// <summary>SystemId 27. 17, 19 and 22 stay reserved for unmerged branches (ADR-029 §3).</summary>
    public static readonly SystemId WellKnownId = new(27);
    public const string Name = "roaddevelopment";

    /// <summary>RoadDevelopmentRow.Kind values.</summary>
    public const int KindModernize = (int)RoadDevelopmentKind.Modernize;
    public const int KindFromBaseline = (int)RoadDevelopmentKind.FromBaseline;

    private readonly SimConfig _cfg = cfg;

    public SystemId Id => WellKnownId;

    public void Step(SimContext<RoadDevelopmentTables> ctx)
    {
        if (_cfg.Roads is null || _cfg.Research is null || _cfg.Goods is null) return;
        IReadOnlyWorldState prev = ctx.Prev;
        List<int>? served = null;

        for (int o = 0; o < ctx.Orders.Count; o++)
        {
            OrderRecord order = ctx.Orders[o];
            if (order.Kind != OrderKind.DevelopRoads) continue;
            served ??= [];
            if (served.Contains(order.ActorId)) continue;   // one action per Empire per turn
            served.Add(order.ActorId);
            if (!IsRosterPolity(prev, order.Actor)) continue;
            if (!(order.Amount > 0.0 && order.Amount <= 100.0)) continue; // load-validated; defensive

            Apply(ctx, order.Actor, order.Amount);
        }
    }

    private void Apply(SimContext<RoadDevelopmentTables> ctx, PolityId polity, double percent)
    {
        IReadOnlyWorldState prev = ctx.Prev;
        RoadDevelopmentTables owned = ctx.Owned;
        RoadsConfig roads = _cfg.Roads!;
        RoadDevelopmentStep[] plan = RoadDevelopmentQuery.Plan(
            prev, owned.Edges, _cfg.Research!, roads, _cfg.Goods!, polity, percent);
        if (plan.Length == 0) return;
        long effectiveTurn = prev.Clock.Turn + 1;

        for (int p = 0; p < plan.Length; p++)
        {
            RoadDevelopmentStep step = plan[p];
            RouteStatus route = step.Route;
            // Ruling 1 (final): draw nearest-first to THIS route, ties by settlement id.
            SettlementId[] payers = RoadDevelopmentQuery.PayingSettlements(prev, polity, route.A, route.B);
            double affordable = RoadDevelopmentQuery.AffordableFraction(owned.GoodStocks, payers, step.Cost);
            bool complete = affordable >= 1.0;

            // Units charged: the full cost, or floor(p × units) per good. The modernization
            // performed is what the charged units buy — the minimum charged/full ratio — so a
            // partial step is never free and never more than paid for.
            var charge = new long[step.Cost.Length];
            double bought = 1.0;
            for (int m = 0; m < step.Cost.Length; m++)
            {
                long full = step.Cost[m].Units;
                charge[m] = complete ? full : (long)Math.Floor(full * affordable);
                long have = RoadDevelopmentQuery.CivilizationStock(owned.GoodStocks, payers, step.Cost[m].Good);
                if (charge[m] > have) charge[m] = have;                       // defensive: never overspend
                if (!complete)
                {
                    double ratio = (double)charge[m] / full;
                    if (ratio < bought) bought = ratio;
                }
            }
            if (!complete && !(bought > 0.0)) return;   // nothing affordable: the order ends here

            long units = 0;
            for (int m = 0; m < step.Cost.Length; m++)
            {
                units += charge[m];
                Draw(ctx, owned.GoodStocks, payers, step.Cost[m].Good, charge[m]);
            }

            double before = route.StartFraction;
            double after = complete ? 1.0 : before + (1.0 - before) * bought;
            if (after >= 1.0) { after = 1.0; complete = true; }
            int edgeId = WriteRoute(owned.Edges, roads, route, after, effectiveTurn);

            owned.Log.Add(new RoadDevelopmentRow(
                prev.Clock.Turn, polity, edgeId, route.A, route.B, route.CurrentClass, route.TargetClass,
                (int)route.Kind, route.Usage, units, before, after));

            if (!complete) return;   // the purse ran out part-way: the affordable proportion, then stop
        }
    }

    /// <summary>Writes the route's new modernization state IN PLACE (or the first route row of a
    /// bare baseline path) and returns its stable id.</summary>
    private static int WriteRoute(Table<TransportEdgeRow> edges, RoadsConfig roads, RouteStatus route, double fraction, long turn)
    {
        int cls = route.CurrentClass, target = route.TargetClass;
        bool done = fraction >= 1.0;
        int newClass = done ? target : cls;
        double newFraction = done ? 0.0 : fraction;
        double factor = done ? roads.ClassOf(target)!.SpeedFactor : RoadPerformance.EffectiveCostFactor(roads, cls, target, fraction);
        long capacity = done ? roads.ClassOf(target)!.CapacityTonnesPerYear : RoadPerformance.EffectiveCapacity(roads, cls, target, fraction);

        if (route.Kind == RoadDevelopmentKind.Modernize)
        {
            int at = IndexOfEdge(edges, route.Edge);
            edges[at] = edges[at] with
            {
                EdgeType = newClass, TargetClass = target, Modernization = newFraction,
                CostFactor = factor, CapacityTonnesPerYear = capacity, UpgradedTurn = turn,
            };
            return route.Edge;
        }

        int id = NextEdgeId(edges);
        edges.Add(new TransportEdgeRow(
            id, route.A, route.B, newClass, TransportModes.Road, TransportEdgeStates.Complete,
            capacity, route.LengthKm, Condition: 0, BuiltTurn: turn, UpgradedTurn: turn,
            TargetClass: target, Modernization: newFraction, CostFactor: factor));
        return id;
    }

    /// <summary>Draws <paramref name="units"/> of a good from the civilization's settlements in
    /// the given (nearest-first) order through the Ledger (a sink per row touched). The caller has checked the total.</summary>
    private static void Draw(SimContext<RoadDevelopmentTables> ctx, Table<GoodStockRow> stocks, SettlementId[] payers, GoodId good, long units)
    {
        long left = units;
        for (int p = 0; p < payers.Length && left > 0; p++)
        {
            int idx = GoodStockIndex.IndexOf(stocks, payers[p], good);
            if (idx < 0) continue;
            long take = Math.Min(left, stocks[idx].Amount.Value);
            if (take <= 0) continue;
            ctx.Ledger.Flow(ref stocks.Ref(idx).Amount, ConservedQuantityIds.OfGood(good),
                ReasonIds.ConstructionMaterials, take, FlowDirection.Sink, OverdrawPolicy.Throw);
            left -= take;
        }
        if (left != 0) throw new InvalidOperationException("road development: the civilization's holding was checked but did not cover the charge.");
    }

    private static bool IsRosterPolity(IReadOnlyWorldState prev, PolityId polity)
    {
        if (prev.Polities.Count == 0) return true;   // a toy world has no roster to check against
        for (int i = 0; i < prev.Polities.Count; i++)
            if (prev.Polities[i].Id.Value == polity.Value) return true;
        return false;
    }

    private static int IndexOfEdge(Table<TransportEdgeRow> edges, int id)
    {
        for (int i = 0; i < edges.Count; i++) if (edges[i].Id == id) return i;
        throw new InvalidOperationException($"road development: edge {id} named by the plan is not in the table.");
    }

    /// <summary>Ids are stable and never reused: one past the highest ever written (rows are never deleted).</summary>
    private static int NextEdgeId(Table<TransportEdgeRow> edges)
    {
        int max = 0;
        for (int i = 0; i < edges.Count; i++) if (edges[i].Id > max) max = edges[i].Id;
        return max + 1;
    }
}
