using Sim.Core.Kernel;
using Sim.Core.Systems;
using Sim.Core.Systems.Research;

namespace Sim.Core.State;

/// <summary>ADR-032 — what developing one route does. Never "a parallel route" (ruling 6).</summary>
public enum RoadDevelopmentKind
{
    None = 0,
    /// <summary>Modernize an existing route row IN PLACE (same id) toward the target class.</summary>
    Modernize = 1,
    /// <summary>The first modernization of a bare baseline path: the SAME physical route gets its
    /// route row (and its stable id) because it is now more than the free baseline.</summary>
    FromBaseline = 2,
}

/// <summary>ADR-032 — why a candidate route is not eligible for development.</summary>
public enum RouteIneligibility
{
    None = 0,
    /// <summary>The issuer knows no buildable class (research gates every class but the baseline).</summary>
    NoKnownClass = 1,
    /// <summary>The route is already fully at the best class the issuer knows.</summary>
    AtBestKnownClass = 2,
    /// <summary>The route is part-way to a class the ISSUER has not researched (another
    /// civilization started it); the issuer cannot continue it and never lowers it.</summary>
    TargetBeyondIssuerKnowledge = 3,
}

/// <summary>
/// ADR-032 — ONE CANDIDATE INTER-CITY ROUTE as the issuing civilization sees it: the settlement
/// pair (A &lt; B), the route row that would be modernized (<c>Edge</c>, -1 for a bare baseline
/// path), its length, its USAGE (the ranking proxy), the class fully achieved
/// (<c>CurrentClass</c>), the route's present modernization target and fraction
/// (<c>RowTarget</c>, <c>RowProgress</c>), the class development would now aim at
/// (<c>TargetClass</c> = the issuer's best known), the fraction toward THAT target the route
/// already represents (<c>StartFraction</c> — equal to RowProgress unless the target was raised,
/// see <see cref="RoadPerformance.RetargetFraction"/>), and why it is ineligible, if it is.
/// </summary>
public readonly record struct RouteStatus(
    SettlementId A, SettlementId B, double LengthKm, long Usage, int Edge, int CurrentClass,
    int RowTarget, double RowProgress, int TargetClass, double StartFraction,
    RoadDevelopmentKind Kind, RouteIneligibility Ineligible)
{
    public bool Eligible => Ineligible == RouteIneligibility.None;
}

/// <summary>ADR-032 — <c>Units</c> of good <c>Good</c>.</summary>
public readonly record struct RoadMaterialCost(GoodId Good, long Units);

/// <summary>ADR-032 — one selected development: the route and the cost of COMPLETING its
/// modernization to the target class (the system applies the affordable proportion of it).</summary>
public readonly record struct RoadDevelopmentStep(RouteStatus Route, RoadMaterialCost[] Cost);

/// <summary>
/// ADR-032 — THE READ-ONLY ROUTE VIEW a UI displays (ruling 29): route, endpoints, class,
/// modernization percentage, effective performance (cost factor and relative speed — the
/// values pathfinding uses), capacity, usage, eligibility and the estimated cost of completing
/// the modernization. Derived; nothing here is decided by a UI.
/// </summary>
public readonly record struct RoadRouteView(
    RouteStatus Route, double ModernizationPercent, double EffectiveCostFactor, double EffectiveSpeed,
    long CapacityTonnesPerYear, RoadMaterialCost[] EstimatedCost);

/// <summary>
/// ADR-032 — THE ONE ROAD-DEVELOPMENT SELECTION, shared by RoadDevelopmentSystem (which applies
/// it), the AI policy and any UI preview (which only read it). Player and AI orders go through
/// <see cref="Plan(IReadOnlyWorldState, ResearchContent, RoadsConfig, GoodsConfig, PolityId, double)"/> unchanged.
///
/// <list type="number">
/// <item><b>Candidate routes.</b> Every settlement pair (A &lt; B) with a finite cached travel
///   cost (SettlementDistances — the D-016 derived table, so nothing here scans the lattice), a
///   length within roads.maxRouteKm, and at least one endpoint the issuer CONTROLS. A road has no
///   owner and may end in a foreign or unruled settlement. A world with no Controls at all (a
///   hand-built toy) treats every pair as the issuer's — the ConstructionSystem precedent.</item>
/// <item><b>The physical route.</b> A pair with route rows modernizes its LOWEST-ID row that is
///   below the target (stable: repeated orders continue the same row); a pair with none
///   modernizes its baseline path, which gets its route row on the first development. Upgrading
///   NEVER creates a second row (ruling 6/11). Length is the row's fixed LengthKm, or the cached
///   travel cost × km per cost unit for a bare baseline path.</item>
/// <item><b>Usage proxy.</b> Units of every good that crossed the pair, both directions, in the
///   PREV turn's realised trade flows (TradeFlows). Provisional (ruling 9).</item>
/// <item><b>Target class.</b> The best class whose research entity the issuer is
///   knowledge-eligible for. A route already fully there is ineligible.</item>
/// <item><b>Ranking.</b> Eligible routes by (usage DESC, A ASC, B ASC) — integers only.</item>
/// <item><b>Percentage.</b> With total usage U &gt; 0, the shortest ranked prefix whose usage
///   reaches pct% of U (pct &gt; 0 always takes the first). With U = 0, ceil(pct% × n) routes.</item>
/// <item><b>Cost.</b> Per good, ceil(LengthKm × max(0, qty/km(target) − qty/km(current class))
///   × (1 − StartFraction)) — proportional to the modernization still to perform; nothing is
///   charged for infrastructure that does not exist.</item>
/// <item><b>Payer.</b> The ISSUING civilization, from its own goods: the GoodStocks of the
///   settlements it controls, drawn in ascending settlement id (<see cref="PayingSettlements"/>)
///   — never an endpoint as such, never the capital by default. No second treasury.</item>
/// </list>
/// Affordability is applied by the system, in plan order: the affordable PROPORTION of a step
/// is performed, and a step that is not fully affordable ends the order.
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
    /// step sees what the first one modernized; everything else is PREV.
    /// </summary>
    public static RouteStatus[] Routes(
        IReadOnlyWorldState world, IReadOnlyTable<TransportEdgeRow> edges, ResearchContent research, RoadsConfig roads, PolityId polity)
    {
        double kmPerCost = TransportQuery.KmPerCostUnit(world);
        int target = BestKnownClass(world, research, roads, polity);
        int n = world.Settlements.Count;
        bool anyControl = world.Controls.Count > 0;

        // Settlement id → row index, and the usage matrix in ONE pass over the trade flows.
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
            if (anyControl && !EmpireQuery.ControlsSettlement(world, polity, d.From)
                           && !EmpireQuery.ControlsSettlement(world, polity, d.To)) continue;
            RouteStatus st = Status(roads, edges, d.From, d.To, d.TravelCost * kmPerCost,
                usage[Math.Min(a, b) * n + Math.Max(a, b)], target);
            if (st.LengthKm > roads.MaxRouteKm) continue;
            result.Add(st);
        }
        result.Sort(static (x, y) => x.A.Value != y.A.Value ? x.A.Value.CompareTo(y.A.Value) : x.B.Value.CompareTo(y.B.Value));
        return [.. result];
    }

    private static int Lookup(int[] index, int id) => id >= 0 && id < index.Length ? index[id] : -1;

    private static RouteStatus Status(
        RoadsConfig roads, IReadOnlyTable<TransportEdgeRow> table, SettlementId a, SettlementId b, double baselineKm, long usage, int target)
    {
        // The physical route to modernize: the lowest-id travelled road row below the target;
        // failing that (all at or above it) the lowest-id row, for display; none → the bare path.
        int pick = -1, lowest = -1;
        TransportEdgeRow chosen = default, first = default;
        for (int i = 0; i < table.Count; i++)
        {
            TransportEdgeRow e = table[i];
            bool between = (e.A.Value == a.Value && e.B.Value == b.Value) || (e.A.Value == b.Value && e.B.Value == a.Value);
            if (!between || e.Mode != TransportModes.Road) continue;
            if (lowest < 0 || e.Id < lowest) { lowest = e.Id; first = e; }
            if (e.EdgeType < target && (pick < 0 || e.Id < pick)) { pick = e.Id; chosen = e; }
        }

        if (pick < 0 && lowest >= 0)
        {
            RouteIneligibility why = target == EdgeTypes.DirtPath ? RouteIneligibility.NoKnownClass : RouteIneligibility.AtBestKnownClass;
            return new RouteStatus(a, b, first.LengthKm, usage, first.Id, first.EdgeType, first.TargetClass, first.Modernization,
                target, 0.0, RoadDevelopmentKind.None, why);
        }
        if (pick < 0)
        {
            RouteIneligibility why = target == EdgeTypes.DirtPath ? RouteIneligibility.NoKnownClass : RouteIneligibility.None;
            return new RouteStatus(a, b, baselineKm, usage, -1, EdgeTypes.DirtPath, EdgeTypes.DirtPath, 0.0,
                target, 0.0, why == RouteIneligibility.None ? RoadDevelopmentKind.FromBaseline : RoadDevelopmentKind.None, why);
        }

        if (chosen.TargetClass > target)
            return new RouteStatus(a, b, chosen.LengthKm, usage, chosen.Id, chosen.EdgeType, chosen.TargetClass, chosen.Modernization,
                target, 0.0, RoadDevelopmentKind.None, RouteIneligibility.TargetBeyondIssuerKnowledge);

        double start = chosen.Modernization <= 0.0 ? 0.0
            : chosen.TargetClass == target ? chosen.Modernization
            : RoadPerformance.RetargetFraction(roads, chosen.EdgeType, chosen.TargetClass, chosen.Modernization, target);
        return new RouteStatus(a, b, chosen.LengthKm, usage, chosen.Id, chosen.EdgeType, chosen.TargetClass, chosen.Modernization,
            target, start, RoadDevelopmentKind.Modernize, RouteIneligibility.None);
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

    /// <summary>The material cost of COMPLETING <paramref name="route"/>'s modernization to its
    /// target from its StartFraction (see the header). Empty for an ineligible route.</summary>
    public static RoadMaterialCost[] CostOf(RoadsConfig roads, GoodsConfig goods, RouteStatus route)
    {
        if (!route.Eligible) return [];
        RoadClassConfig to = roads.ClassOf(route.TargetClass)!;
        RoadClassConfig from = roads.ClassOf(route.CurrentClass)!;
        double remaining = 1.0 - route.StartFraction;
        var result = new List<RoadMaterialCost>();
        for (int i = 0; i < to.MaterialsPerKm.Length; i++)
        {
            RoadMaterialConfig m = to.MaterialsPerKm[i];
            double already = 0.0;
            for (int j = 0; j < from.MaterialsPerKm.Length; j++)
                if (string.Equals(from.MaterialsPerKm[j].Good, m.Good, StringComparison.Ordinal)) already += from.MaterialsPerKm[j].Qty;
            double perKm = Math.Max(0.0, m.Qty - already);
            long units = (long)Math.Ceiling(route.LengthKm * perKm * remaining);
            if (units > 0) result.Add(new RoadMaterialCost(new GoodId(goods.IdOf(m.Good)), units));
        }
        return [.. result];
    }

    // ------------------------------------------------------------------ the issuer's purse

    /// <summary>
    /// THE ISSUING CIVILIZATION'S RESOURCE BASE (the Director's rulings 12–13): the settlements
    /// it controls (ControlRow, D-042 §5's single source of truth), ascending settlement id — the
    /// deterministic draw order. Resources stay physically at settlements (D-042 §10); the
    /// civilization's economy is their sum, and a development is paid by drawing on them in this
    /// order through the Ledger. No treasury exists. A world with no Controls at all (a toy)
    /// treats every settlement as the issuer's, matching candidate selection.
    /// </summary>
    public static SettlementId[] PayingSettlements(IReadOnlyWorldState world, PolityId polity)
    {
        var ids = new List<int>();
        bool anyControl = world.Controls.Count > 0;
        for (int s = 0; s < world.Settlements.Count; s++)
        {
            SettlementId id = world.Settlements[s].Id;
            if (!anyControl || EmpireQuery.ControlsSettlement(world, polity, id)) ids.Add(id.Value);
        }
        ids.Sort();
        var result = new SettlementId[ids.Count];
        for (int i = 0; i < ids.Count; i++) result[i] = new SettlementId(ids[i]);
        return result;
    }

    /// <summary>The civilization-level holding of <paramref name="good"/>: the sum over its paying settlements.</summary>
    public static long CivilizationStock(IReadOnlyTable<GoodStockRow> stocks, SettlementId[] payers, GoodId good)
    {
        long total = 0;
        checked
        {
            for (int p = 0; p < payers.Length; p++)
            {
                int idx = GoodStockIndex.IndexOf(stocks, payers[p], good);
                if (idx >= 0) total += stocks[idx].Amount.Value;
            }
        }
        return total;
    }

    /// <summary>
    /// The proportion of a step's cost the civilization can pay, in [0, 1]: the minimum over its
    /// goods of holding / units (1 when the step costs nothing). The system turns it into whole
    /// units charged (floored) and performs exactly the modernization those units buy.
    /// </summary>
    public static double AffordableFraction(IReadOnlyTable<GoodStockRow> stocks, SettlementId[] payers, RoadMaterialCost[] cost)
    {
        double p = 1.0;
        for (int m = 0; m < cost.Length; m++)
        {
            long have = CivilizationStock(stocks, payers, cost[m].Good);
            if (have >= cost[m].Units) continue;
            double q = have <= 0 ? 0.0 : (double)have / cost[m].Units;
            if (q < p) p = q;
        }
        return p;
    }

    /// <summary>
    /// THE PLAN: the selected development steps for <paramref name="percent"/>% in application
    /// order, each with the cost of completing it. Affordability is applied by the system, in this order.
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
            steps[i] = new RoadDevelopmentStep(selected[i], CostOf(roads, goods, selected[i]));
        return steps;
    }

    /// <summary>
    /// THE UI SURFACE (ruling 29): every candidate route of the issuer with its present effective
    /// performance (exactly what pathfinding and freight use), modernization percentage, capacity,
    /// usage, eligibility and the estimated cost of completing it. (A, B) ascending.
    /// </summary>
    public static RoadRouteView[] Describe(
        IReadOnlyWorldState world, ResearchContent research, RoadsConfig roads, GoodsConfig goods, PolityId polity)
    {
        RouteStatus[] routes = Routes(world, research, roads, polity);
        var result = new RoadRouteView[routes.Length];
        for (int i = 0; i < routes.Length; i++)
        {
            RouteStatus r = routes[i];
            double factor;
            long capacity;
            if (r.Edge >= 0 && TransportQuery.TryGetEdge(world, r.Edge, out TransportEdgeRow e))
            {
                factor = e.CostFactor;
                capacity = e.CapacityTonnesPerYear;
            }
            else
            {
                factor = roads.ClassOf(EdgeTypes.DirtPath)!.SpeedFactor;
                capacity = roads.ClassOf(EdgeTypes.DirtPath)!.CapacityTonnesPerYear;
            }
            result[i] = new RoadRouteView(r, r.RowProgress * 100.0, factor, 1.0 / factor, capacity, CostOf(roads, goods, r));
        }
        return result;
    }

    /// <summary>The order that asks for <paramref name="percent"/>% — identical for player and AI.</summary>
    public static OrderRecord DevelopOrder(IReadOnlyWorldState world, PolityId polity, double percent) =>
        OrderRecord.From(world.Clock.Turn, polity, OrderKind.DevelopRoads, 0, percent);
}
