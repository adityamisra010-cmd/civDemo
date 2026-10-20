using Sim.Core.Kernel;
using Sim.Core.State;

namespace Sim.Core.Systems.Roads;

/// <summary>Tables owned by <see cref="RoadDevelopmentSystem"/> (ADR-003 ownership by construction).
/// GoodStocks is the SANCTIONED SHARED STOCK — this system is its SIXTH holder (SystemCatalog
/// records the field-level split: Amount via Ledger SINK only, reason ConstructionMaterials).
/// <c>Holds</c> (D-055, schema v34) is NOT owned: it is construction's NEXT ConstructionHolds table, handed READ-ONLY
/// (SystemCatalog's ownership record; construction is its sole writer). D-056: the read is a declared same-step edge
/// (SystemRegistration.ReadsThisStepFrom) — TurnExecutor refuses a pipeline that runs this system before construction while
/// the hold rule applies, so it always reads THIS step's set-asides (a pipeline without construction, a test composition,
/// reads the table as the world carries it).</summary>
public sealed record RoadDevelopmentTables(
    Table<TransportEdgeRow> Edges, Table<RoadDevelopmentRow> Log, Table<GoodStockRow> GoodStocks,
    IReadOnlyTable<ConstructionHoldRow> Holds);

/// <summary>
/// ADR-032 — THE ONE AUTHORITATIVE ROAD-DEVELOPMENT OPERATION (the Director's final transport
/// rulings 5–17). Roads are developed by a civilization-level ACTION, never by a builder unit,
/// and the action is the same for the player and for the AI: a DevelopRoads order (OrderKind 8)
/// asking for X% of eligible inter-city transport demand.
///
/// THE STEP, in order-log order, first DevelopRoads per Empire per turn (RoadDevelopmentQuery.DeliveredOrderIndices — the
/// slot is taken by an Empire's first DevelopRoads order before the roster and range checks):
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
/// turn t shows its effect in the state of turn t+1. (Building projects are no longer atomic — D-054
/// WS1 builds them a little each turn — but road works are unchanged by it.)
///
/// WHAT IT PAYS FROM (D-054 WS2): the NEXT GoodStocks after building work, which ran earlier in the step. Under an
/// Empire's building/roads split (sim.json construction.allocationRatios; ResourceAllocationRow) building work took at
/// most its grant G_b of each good — its guaranteed share plus the road share this order did not need — so what this
/// order finds is at least min(its charge, the road share) of every good; with no split row (b = 100) building work
/// went first and this order pays from what it left, as before. This system itself is unchanged by the split: it
/// charges exactly as it always did, against what it finds. D-055: what it finds EXCLUDES what construction set aside
/// (its published ConstructionHolds, read-only; construction ran earlier in the step — D-056's enforced edge — so these
/// are this step's set-asides): the charge-time holding is the payers' stock less the set-asides of every purse
/// whose holder set lies within the payers (RoadDevelopmentQuery.SetAsideWithin — the issuer's Empire purse, plus each
/// payer's own purse in a purse-off or Controls-less world), and a donor in a single-settlement purse gives at most its
/// stock less its own set-aside (an Empire purse's set-aside is fungible: it caps the aggregate). THE PAYERS are the
/// issuer's places less any place ANOTHER Empire's purse owns (ConstructionQuery.EmpirePursePlaces — construction's
/// disjoint-holder rule: a place with two control rows belongs to its LOWEST controller's purse alone, so a higher
/// controller's road works neither count nor draw it; T1a round 2). ADR-032's min-ratio charge and donor order are
/// otherwise unchanged. With the hold rule off (reservations off) the table is ignored and the payers are every place the
/// issuer controls: batch 3, in any pipeline order.
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

        // THE orders this step applies — the first DevelopRoads per Empire per turn, its slot taken before the roster
        // and range checks — decided by the one State rule (RoadDevelopmentQuery.DeliveredOrderIndices) that the
        // construction system's building/roads split (D-054 WS2) reads too, so the two can never disagree.
        int[] delivered = RoadDevelopmentQuery.DeliveredOrderIndices(ctx.Prev, _cfg, ctx.Orders);
        for (int i = 0; i < delivered.Length; i++)
        {
            OrderRecord order = ctx.Orders[delivered[i]];
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
            // Ruling 1 (final): draw nearest-first to THIS route, ties by settlement id. D-055 (T1a round 2): with the hold
            // rule on, less any place another Empire's purse owns (a doubly-controlled place is its lowest controller's).
            SettlementId[] payers = ConstructionQuery.EmpirePursePlaces(
                prev, _cfg, polity, RoadDevelopmentQuery.PayingSettlements(prev, polity, route.A, route.B));

            // Units charged: the full cost, or floor(p × units) per good, and the modernization those units buy — the
            // minimum charged/full ratio — so a partial step is never free and never more than paid for. THE one charge
            // arithmetic (RoadDevelopmentQuery.ChargeStep), on what the payers hold before anything of the step is drawn;
            // the construction system's split forecast (RoadDevelopmentQuery.SimulateCharges, D-054 WS2) runs the same.
            // D-055: what construction SET ASIDE in the payers' purses is not road works' to use — subtracted from the
            // holding (RoadDevelopmentQuery.SetAsideWithin), and a single-settlement purse's units also floor that donor's
            // draw. The rule off, or an empty holds table: batch 3.
            long[] have = RoadDevelopmentQuery.Holdings(owned.GoodStocks, payers, step.Cost);
            var keeps = new long[]?[step.Cost.Length];
            for (int m = 0; m < step.Cost.Length; m++)
                have[m] = Math.Max(0, have[m] - RoadDevelopmentQuery.SetAsideWithin(prev, _cfg, owned.Holds, payers, step.Cost[m].Good, ref keeps[m]));
            RoadStepCharge charged = RoadDevelopmentQuery.ChargeStep(step.Cost, have, route.StartFraction);
            if (!charged.Paid) return;   // nothing affordable: the order ends here
            bool complete = charged.Complete;

            long units = 0;
            for (int m = 0; m < step.Cost.Length; m++)
            {
                units += charged.Charges[m];
                Draw(ctx, owned.GoodStocks, payers, step.Cost[m].Good, charged.Charges[m], keeps[m]);
            }

            double before = route.StartFraction;
            double after = charged.After;
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
    /// the given (nearest-first) order through the Ledger (a sink per row touched) — EmpireStockpile.Draw, the one
    /// purse draw construction uses too (the same loop that lived here, moved unchanged: each payer in order gives
    /// min(remaining, held)). The caller has checked the total; a short purse throws before any flow.</summary>
    private static void Draw(SimContext<RoadDevelopmentTables> ctx, Table<GoodStockRow> stocks, SettlementId[] payers, GoodId good, long units, long[]? keep) =>
        EmpireStockpile.Draw(ctx.Ledger, stocks, payers, good, units, ReasonIds.ConstructionMaterials, keep);

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
