using Sim.Core.Kernel;
using Sim.Core.Systems;
using Sim.Core.Systems.Research;

namespace Sim.Core.State;

/// <summary>ADR-032 — what developing one route does.</summary>
public enum RoadDevelopmentKind
{
    None = 0,
    /// <summary>Rebuild an existing physical edge to a better class of the SAME tier, in place.</summary>
    Upgrade = 1,
    /// <summary>Lay a new physical edge beside whatever already joins the pair (a parallel route).</summary>
    NewRoute = 2,
}

/// <summary>ADR-032 — why a candidate route is not eligible for development.</summary>
public enum RouteIneligibility
{
    None = 0,
    /// <summary>The issuer knows no buildable class (research gates every class but the baseline).</summary>
    NoKnownClass = 1,
    /// <summary>The pair already carries the best class the issuer knows.</summary>
    AtBestKnownClass = 2,
}

/// <summary>
/// ADR-032 — ONE CANDIDATE INTER-CITY ROUTE as the issuing polity sees it: the settlement pair
/// (A &lt; B), its baseline length, its USAGE (the ranking proxy), the best class already built
/// on it, the class development would build, which edge an in-place upgrade would rebuild (-1
/// for a new parallel route) and why it is ineligible, if it is. Everything the first road UI
/// needs per route (ruling 22) — and nothing a UI decides.
/// </summary>
public readonly record struct RouteStatus(
    SettlementId A, SettlementId B, double LengthKm, long Usage, int BestExistingClass,
    int TargetClass, int UpgradeEdge, RoadDevelopmentKind Kind, RouteIneligibility Ineligible)
{
    public bool Eligible => Ineligible == RouteIneligibility.None;
}

/// <summary>ADR-032 — <c>Units</c> of good <c>Good</c> one development step consumes.</summary>
public readonly record struct RoadMaterialCost(GoodId Good, long Units);

/// <summary>ADR-032 — one selected development: the route, its cost and the settlement that pays.</summary>
public readonly record struct RoadDevelopmentStep(RouteStatus Route, RoadMaterialCost[] Cost, SettlementId Payer);

/// <summary>
/// ADR-032 — THE ONE ROAD-DEVELOPMENT SELECTION, shared by RoadDevelopmentSystem (which applies
/// it), the AI policy and any UI preview (which only read it). There is no second selection
/// anywhere: player and AI orders go through <see cref="Plan"/> unchanged (ruling 9).
///
/// <list type="number">
/// <item><b>Candidate routes.</b> Every settlement pair (A &lt; B) with a finite cached baseline
///   travel cost (SettlementDistances — the D-016 derived table, recomputed only on network or
///   settlement-set change, so nothing here scans the lattice), a baseline length within
///   roads.maxRouteKm, and at least one endpoint the issuer CONTROLS (a polity develops routes
///   touching its own settlements; the road itself has no owner and may end in a foreign or
///   unruled settlement). A world with no Controls at all (a hand-built toy) treats every pair as
///   the issuer's — the ConstructionSystem precedent.</item>
/// <item><b>Usage proxy.</b> The units of every good that crossed the pair, both directions, in
///   the PREV turn's realised trade flows (TradeFlows — the only measured inter-city transport
///   in the simulation). No traffic model (ruling 11).</item>
/// <item><b>Target class.</b> The best class whose research entity the issuer is
///   knowledge-eligible for (roads.classes[].entity; ResearchQuery.IsKnowledgeEligible). An
///   existing edge of the target's TIER below the target is upgraded IN PLACE; otherwise a NEW
///   PARALLEL edge is laid (a better TIER is a new alignment — path → road → highway — and the
///   old route stays). A pair already at the target class is ineligible.</item>
/// <item><b>Ranking.</b> Eligible routes by (usage DESC, A ASC, B ASC) — a total order on
///   integers, no ties left.</item>
/// <item><b>Percentage.</b> With total usage U &gt; 0, the shortest ranked prefix whose usage
///   reaches pct% of U (pct &gt; 0 always takes at least the first route). With U = 0 (no trade
///   yet) usage cannot weigh anything, so the share is of the route COUNT: ceil(pct% × n).</item>
/// <item><b>Cost.</b> Per good, ceil(LengthKm × (qty/km of the target class − qty/km of the
///   class it replaces, floored at 0)); a new route replaces nothing. Paid by the lowest-id
///   endpoint the issuer controls (endpoint A on a toy world).</item>
/// </list>
/// Affordability is NOT decided here: the system applies the plan in order against the live
/// stocks and stops at the first step it cannot pay in full (a prefix, never a skip, no debt).
/// </summary>
public static class RoadDevelopmentQuery
{
    /// <summary>The best class the polity may build; DirtPath when research opens none.</summary>
    public static int BestKnownClass(IReadOnlyWorldState world, ResearchContent research, RoadsConfig roads, PolityId polity)
    {
        bool[] completed = ResearchQuery.CompletedMask(world, research, polity);
        int best = EdgeTypes.DirtPath;
        for (int i = 0; i < roads.Classes.Length; i++)
        {
            RoadClassConfig c = roads.Classes[i];
            if (c.Entity is null || c.EdgeType <= best) continue;
            int entity = research.EntityIndexOf(c.Entity);
            if (entity >= 0 && ResearchQuery.IsKnowledgeEligible(research, entity, completed)) best = c.EdgeType;
        }
        return best;
    }

    /// <summary>Is <paramref name="edgeType"/> buildable by the polity (its entity is knowledge-eligible)?</summary>
    public static bool IsClassKnown(IReadOnlyWorldState world, ResearchContent research, RoadsConfig roads, PolityId polity, int edgeType)
    {
        RoadClassConfig? c = roads.ClassOf(edgeType);
        if (c?.Entity is null) return false;
        int entity = research.EntityIndexOf(c.Entity);
        return entity >= 0 && ResearchQuery.IsKnowledgeEligible(research, entity, ResearchQuery.CompletedMask(world, research, polity));
    }

    /// <summary>The usage proxy of one pair: realised trade units both directions in the world's TradeFlows.</summary>
    public static long Usage(IReadOnlyWorldState world, SettlementId x, SettlementId y)
    {
        long sum = 0;
        for (int i = 0; i < world.TradeFlows.Count; i++)
        {
            TradeFlowRow f = world.TradeFlows[i];
            if ((f.From.Value == x.Value && f.To.Value == y.Value) || (f.From.Value == y.Value && f.To.Value == x.Value))
                sum += f.Quantity;
        }
        return sum;
    }

    /// <summary>Every candidate route of the issuer, eligible or not, in (A, B) ascending order.</summary>
    public static RouteStatus[] Routes(IReadOnlyWorldState world, ResearchContent research, RoadsConfig roads, PolityId polity) =>
        Routes(world, world.TransportEdges, research, roads, polity);

    /// <summary>
    /// <see cref="Routes(IReadOnlyWorldState, ResearchContent, RoadsConfig, PolityId)"/> against an
    /// explicit edge table: RoadDevelopmentSystem passes its NEXT table so a second order in the same
    /// step sees the edges the first one built (no double-built pair); everything else is PREV.
    /// </summary>
    public static RouteStatus[] Routes(
        IReadOnlyWorldState world, IReadOnlyTable<TransportEdgeRow> edges, ResearchContent research, RoadsConfig roads, PolityId polity)
    {
        double kmPerCost = TransportQuery.KmPerCostUnit(world);
        int target = BestKnownClass(world, research, roads, polity);
        int n = world.Settlements.Count;
        bool anyControl = world.Controls.Count > 0;

        // Settlement id → row index, and the usage matrix in ONE pass over the trade flows
        // (never per pair): O(settlements² + flows), paid only when somebody asks.
        int maxId = 0;
        for (int s = 0; s < n; s++) if (world.Settlements[s].Id.Value > maxId) maxId = world.Settlements[s].Id.Value;
        var index = new int[maxId + 1];
        Array.Fill(index, -1);
        for (int s = 0; s < n; s++) index[world.Settlements[s].Id.Value] = s;
        var usage = new long[n * n];
        for (int i = 0; i < world.TradeFlows.Count; i++)
        {
            TradeFlowRow f = world.TradeFlows[i];
            int a = Lookup(index, f.From.Value), b = Lookup(index, f.To.Value);
            if (a < 0 || b < 0 || a == b) continue;
            usage[Math.Min(a, b) * n + Math.Max(a, b)] += f.Quantity;
        }

        var result = new List<RouteStatus>();
        for (int i = 0; i < world.SettlementDistances.Count; i++)
        {
            SettlementDistanceRow d = world.SettlementDistances[i];
            if (d.From.Value >= d.To.Value || !double.IsFinite(d.TravelCost)) continue;
            int a = Lookup(index, d.From.Value), b = Lookup(index, d.To.Value);
            if (a < 0 || b < 0) continue;
            double km = d.TravelCost * kmPerCost;
            if (km > roads.MaxRouteKm) continue;
            if (anyControl && !EmpireQuery.ControlsSettlement(world, polity, d.From)
                           && !EmpireQuery.ControlsSettlement(world, polity, d.To)) continue;
            result.Add(Status(edges, d.From, d.To, km, usage[Math.Min(a, b) * n + Math.Max(a, b)], target));
        }
        result.Sort(static (x, y) => x.A.Value != y.A.Value ? x.A.Value.CompareTo(y.A.Value) : x.B.Value.CompareTo(y.B.Value));
        return [.. result];
    }

    private static int Lookup(int[] index, int id) => id >= 0 && id < index.Length ? index[id] : -1;

    private static RouteStatus Status(IReadOnlyTable<TransportEdgeRow> table, SettlementId a, SettlementId b, double km, long usage, int target)
    {
        TransportEdgeRow[] edges = TransportQuery.EdgesBetween(table, a, b);
        int bestExisting = EdgeTypes.DirtPath;
        for (int i = 0; i < edges.Length; i++)
            if (edges[i].Mode == TransportModes.Road && edges[i].EdgeType > bestExisting) bestExisting = edges[i].EdgeType;

        if (target == EdgeTypes.DirtPath)
            return new RouteStatus(a, b, km, usage, bestExisting, target, -1, RoadDevelopmentKind.None, RouteIneligibility.NoKnownClass);
        if (bestExisting >= target)
            return new RouteStatus(a, b, km, usage, bestExisting, target, -1, RoadDevelopmentKind.None, RouteIneligibility.AtBestKnownClass);

        // In-place upgrade: the best edge of the target's tier below the target (lowest id on a tie).
        int upgrade = -1, upgradeClass = 0;
        int tier = EdgeTypes.TierOf(target);
        for (int i = 0; i < edges.Length; i++)
        {
            TransportEdgeRow e = edges[i];
            if (e.Mode != TransportModes.Road || EdgeTypes.TierOf(e.EdgeType) != tier || e.EdgeType >= target) continue;
            if (upgrade < 0 || e.EdgeType > upgradeClass || (e.EdgeType == upgradeClass && e.Id < upgrade))
            {
                upgrade = e.Id;
                upgradeClass = e.EdgeType;
            }
        }
        return upgrade >= 0
            ? new RouteStatus(a, b, km, usage, bestExisting, target, upgrade, RoadDevelopmentKind.Upgrade, RouteIneligibility.None)
            : new RouteStatus(a, b, km, usage, bestExisting, target, -1, RoadDevelopmentKind.NewRoute, RouteIneligibility.None);
    }

    /// <summary>The eligible routes ranked by (usage DESC, A ASC, B ASC).</summary>
    public static RouteStatus[] Ranked(RouteStatus[] routes)
    {
        var eligible = new List<RouteStatus>();
        for (int i = 0; i < routes.Length; i++) if (routes[i].Eligible) eligible.Add(routes[i]);
        eligible.Sort(CompareRank);
        return [.. eligible];
    }

    /// <summary>The ranking key: usage descending, then the stable endpoint ids ascending.</summary>
    public static int CompareRank(RouteStatus x, RouteStatus y)
    {
        if (x.Usage != y.Usage) return y.Usage.CompareTo(x.Usage);
        if (x.A.Value != y.A.Value) return x.A.Value.CompareTo(y.A.Value);
        return x.B.Value.CompareTo(y.B.Value);
    }

    /// <summary>The ranked prefix covering <paramref name="percent"/>% of demand (see the header).</summary>
    public static RouteStatus[] Select(RouteStatus[] ranked, double percent)
    {
        if (ranked.Length == 0 || !(percent > 0.0)) return [];
        double pct = Math.Min(percent, 100.0);
        long total = 0;
        for (int i = 0; i < ranked.Length; i++) total += ranked[i].Usage;

        int take;
        if (total > 0)
        {
            double needed = pct / 100.0 * total;
            long cumulative = 0;
            take = 0;
            while (take < ranked.Length && (take == 0 || cumulative < needed))
            {
                cumulative += ranked[take].Usage;
                take++;
            }
        }
        else
        {
            take = (int)Math.Ceiling(pct / 100.0 * ranked.Length);
            if (take < 1) take = 1;
            if (take > ranked.Length) take = ranked.Length;
        }
        var result = new RouteStatus[take];
        Array.Copy(ranked, result, take);
        return result;
    }

    /// <summary>The material cost of developing <paramref name="route"/> (see the header).</summary>
    public static RoadMaterialCost[] CostOf(RoadsConfig roads, GoodsConfig goods, IReadOnlyTable<TransportEdgeRow> edges, RouteStatus route)
    {
        RoadClassConfig to = roads.ClassOf(route.TargetClass)!;
        RoadClassConfig? from = null;
        if (route.Kind == RoadDevelopmentKind.Upgrade && TransportQuery.TryGetEdge(edges, route.UpgradeEdge, out TransportEdgeRow edge))
            from = roads.ClassOf(edge.EdgeType);

        var result = new List<RoadMaterialCost>();
        for (int i = 0; i < to.MaterialsPerKm.Length; i++)
        {
            RoadMaterialConfig m = to.MaterialsPerKm[i];
            double already = 0.0;
            if (from is not null)
                for (int j = 0; j < from.MaterialsPerKm.Length; j++)
                    if (string.Equals(from.MaterialsPerKm[j].Good, m.Good, StringComparison.Ordinal)) already += from.MaterialsPerKm[j].Qty;
            double perKm = Math.Max(0.0, m.Qty - already);
            long units = (long)Math.Ceiling(route.LengthKm * perKm);
            if (units > 0) result.Add(new RoadMaterialCost(new GoodId(goods.IdOf(m.Good)), units));
        }
        return [.. result];
    }

    /// <summary>Who pays: the lowest-id endpoint the issuer controls; endpoint A on a world with no Controls.</summary>
    public static SettlementId Payer(IReadOnlyWorldState world, PolityId polity, RouteStatus route)
    {
        if (world.Controls.Count == 0) return route.A;
        if (EmpireQuery.ControlsSettlement(world, polity, route.A)) return route.A;
        return route.B;
    }

    /// <summary>
    /// THE PLAN: the selected development steps for <paramref name="percent"/>% in application
    /// order, each with its cost and payer. Affordability is applied by the system, in this order.
    /// </summary>
    public static RoadDevelopmentStep[] Plan(
        IReadOnlyWorldState world, ResearchContent research, RoadsConfig roads, GoodsConfig goods,
        PolityId polity, double percent) =>
        Plan(world, world.TransportEdges, research, roads, goods, polity, percent);

    /// <summary><see cref="Plan(IReadOnlyWorldState, ResearchContent, RoadsConfig, GoodsConfig, PolityId, double)"/>
    /// against an explicit edge table (the system's NEXT table).</summary>
    public static RoadDevelopmentStep[] Plan(
        IReadOnlyWorldState world, IReadOnlyTable<TransportEdgeRow> edges, ResearchContent research, RoadsConfig roads,
        GoodsConfig goods, PolityId polity, double percent)
    {
        RouteStatus[] selected = Select(Ranked(Routes(world, edges, research, roads, polity)), percent);
        var steps = new RoadDevelopmentStep[selected.Length];
        for (int i = 0; i < selected.Length; i++)
            steps[i] = new RoadDevelopmentStep(selected[i], CostOf(roads, goods, edges, selected[i]), Payer(world, polity, selected[i]));
        return steps;
    }

    /// <summary>The order that asks for <paramref name="percent"/>% — identical for player and AI.</summary>
    public static OrderRecord DevelopOrder(IReadOnlyWorldState world, PolityId polity, double percent) =>
        OrderRecord.From(world.Clock.Turn, polity, OrderKind.DevelopRoads, 0, percent);
}
