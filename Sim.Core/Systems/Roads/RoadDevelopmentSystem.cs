using Sim.Core.Kernel;
using Sim.Core.State;

namespace Sim.Core.Systems.Roads;

/// <summary>Tables owned by <see cref="RoadDevelopmentSystem"/> (ADR-003 ownership by construction).
/// GoodStocks is the SANCTIONED SHARED STOCK — this system is its SIXTH holder (SystemCatalog
/// records the field-level split: Amount via Ledger SINK only, reason ConstructionMaterials).</summary>
public sealed record RoadDevelopmentTables(
    Table<TransportEdgeRow> Edges, Table<RoadDevelopmentRow> Log, Table<GoodStockRow> GoodStocks);

/// <summary>
/// ADR-032 — THE ONE AUTHORITATIVE ROAD-DEVELOPMENT OPERATION (the Director's transport rulings
/// 1, 2, 9, 12–15). Roads are built by a civilization-level ACTION, never by a builder unit, and
/// the action is the same for the player and for the AI: a DevelopRoads order (OrderKind 8)
/// asking for X% of eligible inter-city transport demand.
///
/// THE STEP, in order-log order, first DevelopRoads per Empire per turn:
///  1. PLAN with <see cref="RoadDevelopmentQuery.Plan(IReadOnlyWorldState, IReadOnlyTable{TransportEdgeRow}, Research.ResearchContent, RoadsConfig, GoodsConfig, PolityId, double)"/>
///     — the single selection the UI preview and the AI policy also read. Inputs are PREV
///     (research, controls, the cached pairwise baseline, last turn's realised trade) plus this
///     system's NEXT edge table, so a second Empire's order in the same step sees the first's
///     roads and never double-builds a pair.
///  2. PAY AND BUILD, step by step in plan order: the payer's live (NEXT) stock must hold EVERY
///     material in full; then each material leaves through Ledger.Flow (sink,
///     ReasonIds.ConstructionMaterials — a road is a structure, the M4-D argument) and the edge is
///     upgraded in place or a new parallel edge is appended. THE FIRST STEP THAT CANNOT BE PAID
///     ENDS THE ORDER — the built set is the longest affordable PREFIX of the ranking; nothing
///     cheaper further down is substituted, nothing is overspent, no debt exists.
///  3. LOG one RoadDevelopmentRow per built step.
///
/// Construction is atomic within the step (no timer, no partial state — the ConstructionSystem
/// precedent), so an order stamped turn t shows its roads in the state of turn t+1.
///
/// WHAT IT NEVER DOES. It never runs without an order (no per-turn scan of anything: a turn with
/// no DevelopRoads returns at the first loop). It never builds DirtPath — the free baseline is
/// PathBuild's and costs nothing (ruling 3). Research completion builds nothing (ruling 15): it
/// only widens what the next order may choose. It writes no owner (ruling 6) and never reads or
/// changes Condition (ruling 8). No RNG; no dt (a discrete action — law 3 does not apply).
/// Inert without roads tuning, research content or goods content.
/// </summary>
public sealed class RoadDevelopmentSystem(SimConfig cfg) : ISimSystem<RoadDevelopmentTables>
{
    /// <summary>SystemId 27. 17, 19 and 22 stay reserved for unmerged branches (ADR-029 §3).</summary>
    public static readonly SystemId WellKnownId = new(27);
    public const string Name = "roaddevelopment";

    /// <summary>RoadDevelopmentRow.Kind values.</summary>
    public const int KindUpgrade = (int)RoadDevelopmentKind.Upgrade;
    public const int KindNewRoute = (int)RoadDevelopmentKind.NewRoute;

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
        RoadDevelopmentStep[] plan = RoadDevelopmentQuery.Plan(
            prev, owned.Edges, _cfg.Research!, _cfg.Roads!, _cfg.Goods!, polity, percent);
        long effectiveTurn = prev.Clock.Turn + 1;

        for (int p = 0; p < plan.Length; p++)
        {
            RoadDevelopmentStep step = plan[p];
            if (!Affordable(owned.GoodStocks, step)) return;   // the affordable PREFIX ends here

            long units = 0;
            for (int m = 0; m < step.Cost.Length; m++)
            {
                RoadMaterialCost c = step.Cost[m];
                int idx = FindStock(owned.GoodStocks, step.Payer, c.Good);
                ctx.Ledger.Flow(ref owned.GoodStocks.Ref(idx).Amount, ConservedQuantityIds.OfGood(c.Good),
                    ReasonIds.ConstructionMaterials, c.Units, FlowDirection.Sink, OverdrawPolicy.Throw);
                units += c.Units;
            }

            RouteStatus route = step.Route;
            RoadClassConfig cls = _cfg.Roads!.ClassOf(route.TargetClass)!;
            int edgeId;
            int fromClass;
            if (route.Kind == RoadDevelopmentKind.Upgrade)
            {
                int at = IndexOfEdge(owned.Edges, route.UpgradeEdge);
                TransportEdgeRow e = owned.Edges[at];
                fromClass = e.EdgeType;
                edgeId = e.Id;
                owned.Edges[at] = e with
                {
                    EdgeType = route.TargetClass, CapacityTonnesPerYear = cls.CapacityTonnesPerYear,
                    UpgradedTurn = effectiveTurn,
                };
            }
            else
            {
                fromClass = route.BestExistingClass;
                edgeId = NextEdgeId(owned.Edges);
                owned.Edges.Add(new TransportEdgeRow(
                    edgeId, route.A, route.B, route.TargetClass, TransportModes.Road, TransportEdgeStates.Complete,
                    cls.CapacityTonnesPerYear, route.LengthKm, Condition: 0, BuiltTurn: effectiveTurn,
                    UpgradedTurn: effectiveTurn));
            }

            owned.Log.Add(new RoadDevelopmentRow(
                prev.Clock.Turn, polity, edgeId, route.A, route.B, fromClass, route.TargetClass,
                (int)route.Kind, route.Usage, units));
        }
    }

    /// <summary>Every material of the step present IN FULL at the payer — checked before any draw.</summary>
    private static bool Affordable(Table<GoodStockRow> stocks, RoadDevelopmentStep step)
    {
        for (int m = 0; m < step.Cost.Length; m++)
        {
            int idx = FindStock(stocks, step.Payer, step.Cost[m].Good);
            if (idx < 0 || stocks[idx].Amount.Value < step.Cost[m].Units) return false;
        }
        return true;
    }

    private static bool IsRosterPolity(IReadOnlyWorldState prev, PolityId polity)
    {
        if (prev.Polities.Count == 0) return true;   // a toy world has no roster to check against
        for (int i = 0; i < prev.Polities.Count; i++)
            if (prev.Polities[i].Id.Value == polity.Value) return true;
        return false;
    }

    private static int FindStock(Table<GoodStockRow> stocks, SettlementId s, GoodId good)
    {
        for (int i = 0; i < stocks.Count; i++)
            if (stocks[i].Settlement == s && stocks[i].Good == good) return i;
        return -1;
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
