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

/// <summary>ADR-032 / D-054 — what one development step charges (<see cref="RoadDevelopmentQuery.ChargeStep"/>):
/// <c>Charges</c> per cost entry (aligned with the step's cost), whether anything is <c>Paid</c> at all (false: the order
/// ends with no row and no flow), whether the route is <c>Complete</c> after it, and the fraction <c>After</c>.</summary>
public readonly record struct RoadStepCharge(long[] Charges, bool Paid, bool Complete, double After);

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
///   NEVER creates a second row (ruling 6/11). Length is the row's fixed LengthKm, or for a bare
///   baseline path the GEOGRAPHIC straight-line distance between the site cells
///   (<see cref="GeographicKm"/>) — never the cached travel cost, which the road overlay shortens.</item>
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
///   settlements it controls, drawn NEAREST-FIRST to the route being developed (squared
///   site-cell distance to the nearer endpoint, ties by settlement id ascending —
///   <see cref="PayingSettlements(IReadOnlyWorldState, PolityId, SettlementId, SettlementId)"/>)
///   — never the capital by default. No second treasury.</item>
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
            RouteStatus st = Status(roads, edges, d.From, d.To, GeographicKm(world, d.From, d.To, d.TravelCost * kmPerCost),
                usage[Math.Min(a, b) * n + Math.Max(a, b)], target);
            if (st.LengthKm > roads.MaxRouteKm) continue;
            result.Add(st);
        }
        result.Sort(static (x, y) => x.A.Value != y.A.Value ? x.A.Value.CompareTo(y.A.Value) : x.B.Value.CompareTo(y.B.Value));
        return [.. result];
    }

    /// <summary>
    /// ADR-032 §Length invariant — THE PHYSICAL LENGTH of a new route row, independent of network
    /// performance. On a world with terrain it is the straight-line (flat-raster Euclidean)
    /// distance between the two settlements' site cells × km per pixel: pure geography, so no
    /// road's class, modernization or cost factor can change it. The cached pairwise travel cost
    /// (SettlementDistances) is NOT used there, because it is a Pathfinder result over the road
    /// overlay and shrinks as other roads get faster. A terrain-less world (a hand-built toy, in
    /// which no pathfinder ever runs and the cached rows are supplied by hand) has no positions;
    /// it falls back to <paramref name="toyFallbackKm"/>, its hand-written baseline km.
    ///
    /// ADR-032 FR3 AMENDED (batch 3 WS3, the Director's 2026-10-14 instruction revising Final Ruling 3's
    /// "flat square raster (no wrap)"): on a wrapped (cylindrical) world the straight line is the one ON THE
    /// CYLINDER — x the short way round the seam (<see cref="Pathing.MapTopology.SquaredPx"/>). That is the same
    /// "as the crow flies" abstraction FR3 already applies to a bay between two land-connected settlements of
    /// the flat world (the length never followed the ground); `maxRouteKm` candidacy reads it, and the renderer
    /// draws the road the short way, consistent with its length. Flat worlds are unchanged bit for bit.
    /// </summary>
    public static double GeographicKm(IReadOnlyWorldState world, SettlementId a, SettlementId b, double toyFallbackKm)
    {
        Worldgen.TerrainSet? terrain = world.Terrain;
        if (terrain is null) return toyFallbackKm;
        if (!TrySiteCell(world, a, out int ca) || !TrySiteCell(world, b, out int cb)) return toyFallbackKm;
        long sq = SquaredPx(terrain, ca, cb);
        return Math.Sqrt(sq) * terrain.KmPerPx;
    }

    private static bool TrySiteCell(IReadOnlyWorldState world, SettlementId id, out int cell)
    {
        for (int s = 0; s < world.Settlements.Count; s++)
            if (world.Settlements[s].Id.Value == id.Value) { cell = world.Settlements[s].SiteCell; return true; }
        cell = -1;
        return false;
    }

    /// <summary>Exact squared pixel distance between two row-major cells of the world's raster (integer, tie-exact) —
    /// WS3: on the cylinder when the terrain wraps (<see cref="Pathing.MapTopology.SquaredPx"/>), so the donor order of
    /// roads, the construction purse and grain relief ranks a settlement across the seam by its true distance.</summary>
    private static long SquaredPx(Worldgen.TerrainSet terrain, int c1, int c2) =>
        Pathing.MapTopology.SquaredPx(c1, c2, terrain.Size, terrain.WrapX);

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
    /// it controls (ControlRow, D-042 §5's single source of truth), ascending settlement id. This
    /// is the SET (its sums are order-free); the DRAW ORDER for a given route is the nearest-first
    /// overload. Resources stay physically at settlements (D-042 §10); the
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

    /// <summary>
    /// ADR-032 §Payment order (the Director's final ruling 1): the issuer's paying settlements in
    /// DRAW ORDER for a development of route (<paramref name="a"/>, <paramref name="b"/>) — ranked by
    /// physical distance to the route, nearest first, ties by settlement id ascending. The distance
    /// is GEOGRAPHIC and performance-independent (on the cylinder when the world wraps, WS3): the exact integer squared pixel distance from the
    /// settlement's site cell to the NEARER endpoint's site cell (an endpoint the issuer controls is
    /// at 0). A terrain-less world has no positions: every distance is 0 and the order is ascending id.
    /// </summary>
    public static SettlementId[] PayingSettlements(IReadOnlyWorldState world, PolityId polity, SettlementId a, SettlementId b)
    {
        SettlementId[] ids = PayingSettlements(world, polity);
        Worldgen.TerrainSet? terrain = world.Terrain;
        if (terrain is null || ids.Length < 2) return ids;
        bool hasA = TrySiteCell(world, a, out int ca), hasB = TrySiteCell(world, b, out int cb);
        var keys = new (long Dist, int Id)[ids.Length];
        for (int i = 0; i < ids.Length; i++)
        {
            TrySiteCell(world, ids[i], out int c);
            long d = long.MaxValue;
            if (hasA) d = Math.Min(d, SquaredPx(terrain, c, ca));
            if (hasB) d = Math.Min(d, SquaredPx(terrain, c, cb));
            keys[i] = (d, ids[i].Value);
        }
        Array.Sort(keys, static (x, y) => x.Dist != y.Dist ? x.Dist.CompareTo(y.Dist) : x.Id.CompareTo(y.Id));
        var result = new SettlementId[keys.Length];
        for (int i = 0; i < keys.Length; i++) result[i] = new SettlementId(keys[i].Id);
        return result;
    }

    /// <summary>
    /// D-055 — WHAT ROAD WORKS MAY NOT USE (the batch-4 design §1.5): the units of <paramref name="good"/> construction SET
    /// ASIDE (<paramref name="holds"/>: the NEXT ConstructionHolds table, handed read-only — SystemCatalog's ownership record;
    /// in the shipped pipeline order it holds THIS step's set-asides, construction having run first) in every purse whose
    /// holder set lies within <paramref name="payers"/>: an Empire purse (key ≥ 0) whose own places on <paramref name="prev"/>
    /// are all payers (the issuer's own purse), and the single-settlement purse (key −1 − id) of every payer — purse off, a
    /// stateless payer, a world with no Controls, where road works pay from every settlement. An Empire purse's places are
    /// the places its polity controls less those another Empire's purse owns (<see cref="ConstructionQuery.EmpirePursePlaces"/>,
    /// the disjoint-holder rule — they differ only in a world with two control rows for one place; T1a round 2). The sum is
    /// subtracted from the charge-time holding. A single-settlement purse's units are tied to its settlement, so they are
    /// also added to <paramref name="keep"/> (aligned with the payers; allocated on the first such row): that donor gives at
    /// most its stock less them. An Empire purse's set-aside is fungible across its settlements, so it caps the aggregate
    /// only. 0 (and no floors) for an empty table — and, whatever the table holds, with the hold rule off
    /// (<see cref="Reservations.Enabled"/> false: batch 3 in any pipeline order).
    /// </summary>
    public static long SetAsideWithin(
        IReadOnlyWorldState prev, SimConfig cfg, IReadOnlyTable<ConstructionHoldRow> holds, SettlementId[] payers, GoodId good,
        ref long[]? keep)
    {
        if (!Reservations.Enabled(cfg)) return 0;
        long total = 0;
        for (int i = 0; i < holds.Count; i++)
        {
            ConstructionHoldRow row = holds[i];
            if (row.Good != good.Value) continue;
            if (row.Purse < 0)
            {
                int p = IndexOf(payers, -1 - row.Purse);
                if (p < 0) continue;
                keep ??= new long[payers.Length];
                keep[p] = checked(keep[p] + row.Units);
                total = checked(total + row.Units);
            }
            else if (PurseWithin(prev, cfg, row.Purse, payers)) total = checked(total + row.Units);
        }
        return total;
    }

    /// <summary>Whether every place of Empire purse <paramref name="purse"/> — the settlements that polity controls on
    /// <paramref name="world"/>, less those another Empire's purse owns (<see cref="ConstructionQuery.EmpirePursePlaces"/>) —
    /// is one of <paramref name="payers"/>.</summary>
    private static bool PurseWithin(IReadOnlyWorldState world, SimConfig cfg, int purse, SettlementId[] payers)
    {
        if (world.Controls.Count == 0) return false;
        var polity = new PolityId(purse);
        SettlementId[] members = ConstructionQuery.EmpirePursePlaces(world, cfg, polity, PayingSettlements(world, polity));
        if (members.Length == 0) return false;
        for (int i = 0; i < members.Length; i++) if (IndexOf(payers, members[i].Value) < 0) return false;
        return true;
    }

    private static int IndexOf(SettlementId[] ids, int id)
    {
        for (int i = 0; i < ids.Length; i++) if (ids[i].Value == id) return i;
        return -1;
    }

    /// <summary>The civilization-level holding of <paramref name="good"/>: the sum over its paying settlements
    /// (EmpireStockpile.Available — the one purse sum construction reads too; the same checked loop, moved).</summary>
    public static long CivilizationStock(IReadOnlyTable<GoodStockRow> stocks, SettlementId[] payers, GoodId good) =>
        EmpireStockpile.Available(stocks, payers, good);

    /// <summary>
    /// The proportion of a step's cost the civilization can pay, in [0, 1]: the minimum over its
    /// goods of holding / units (1 when the step costs nothing). The system turns it into whole
    /// units charged (floored) and performs exactly the modernization those units buy.
    /// </summary>
    public static double AffordableFraction(IReadOnlyTable<GoodStockRow> stocks, SettlementId[] payers, RoadMaterialCost[] cost) =>
        AffordableFraction(cost, Holdings(stocks, payers, cost));

    /// <summary><see cref="AffordableFraction(IReadOnlyTable{GoodStockRow}, SettlementId[], RoadMaterialCost[])"/> on
    /// the holdings <paramref name="have"/> of each cost entry's good (aligned with <paramref name="cost"/>).</summary>
    public static double AffordableFraction(RoadMaterialCost[] cost, long[] have)
    {
        double p = 1.0;
        for (int m = 0; m < cost.Length; m++)
        {
            if (have[m] >= cost[m].Units) continue;
            double q = have[m] <= 0 ? 0.0 : (double)have[m] / cost[m].Units;
            if (q < p) p = q;
        }
        return p;
    }

    /// <summary>The civilization's holding of each cost entry's good (aligned with <paramref name="cost"/>), read
    /// before anything of the step is drawn.</summary>
    public static long[] Holdings(IReadOnlyTable<GoodStockRow> stocks, SettlementId[] payers, RoadMaterialCost[] cost)
    {
        var have = new long[cost.Length];
        for (int m = 0; m < cost.Length; m++) have[m] = CivilizationStock(stocks, payers, cost[m].Good);
        return have;
    }

    /// <summary>
    /// THE CHARGE ARITHMETIC OF ONE DEVELOPMENT STEP (ADR-032 rulings 13-14; D-054 WS2 shares it): given what the
    /// civilization holds of each cost entry's good (<paramref name="have"/>, aligned with <paramref name="cost"/>), the
    /// units charged and the modernization performed. Fully affordable: every good in full, the route complete. Partly:
    /// floor(full × affordable) of each good, capped at the holding, and the modernization those units buy — the minimum
    /// charged/full ratio — so a partial step is never free and never more than paid for; a fraction that reaches 1 is
    /// snapped to complete. Nothing affordable: <see cref="RoadStepCharge.Paid"/> is false — the order ends with no row
    /// and no flow. RoadDevelopmentSystem charges exactly this (it draws what <see cref="RoadStepCharge.Charges"/> says)
    /// and <see cref="SimulateCharges"/> sums it without drawing, so the two can never disagree.
    /// </summary>
    public static RoadStepCharge ChargeStep(RoadMaterialCost[] cost, long[] have, double startFraction)
    {
        double affordable = AffordableFraction(cost, have);
        bool complete = affordable >= 1.0;
        var charge = new long[cost.Length];
        double bought = 1.0;
        for (int m = 0; m < cost.Length; m++)
        {
            long full = cost[m].Units;
            charge[m] = complete ? full : (long)Math.Floor(full * affordable);
            if (charge[m] > have[m]) charge[m] = have[m];                       // defensive: never overspend
            if (!complete)
            {
                double ratio = (double)charge[m] / full;
                if (ratio < bought) bought = ratio;
            }
        }
        if (!complete && !(bought > 0.0)) return new RoadStepCharge(new long[cost.Length], false, false, startFraction);

        double after = complete ? 1.0 : startFraction + (1.0 - startFraction) * bought;
        if (after >= 1.0) { after = 1.0; complete = true; }
        return new RoadStepCharge(charge, true, complete, after);
    }

    /// <summary>
    /// THE DevelopRoads ORDERS ROAD DEVELOPMENT APPLIES THIS STEP (ADR-032; D-054 WS2 — one rule, two callers:
    /// RoadDevelopmentSystem acts on exactly these, and ConstructionSystem's building/roads split reads the one of an
    /// Empire): the indices into <paramref name="orders"/>, in log order, of the FIRST DevelopRoads order of each actor
    /// that passes. The actor's one slot per turn is taken by its first DevelopRoads order BEFORE any check, so a first
    /// order that is then rejected (an actor not on the roster — every actor counts in a world with no roster, a toy —
    /// or a percentage outside (0, 100], NaN included) leaves that actor with no road works this turn. Empty when road
    /// development is inert (no roads, research or goods content).
    /// </summary>
    public static int[] DeliveredOrderIndices(IReadOnlyWorldState prev, SimConfig cfg, IReadOnlyList<OrderRecord> orders)
    {
        if (cfg.Roads is null || cfg.Research is null || cfg.Goods is null) return [];
        List<int>? served = null;
        List<int>? result = null;
        for (int o = 0; o < orders.Count; o++)
        {
            OrderRecord order = orders[o];
            if (order.Kind != OrderKind.DevelopRoads) continue;
            served ??= [];
            if (served.Contains(order.ActorId)) continue;   // one action per Empire per turn
            served.Add(order.ActorId);
            if (!IsRosterPolity(prev, order.Actor)) continue;
            if (!(order.Amount > 0.0 && order.Amount <= 100.0)) continue; // load-validated; defensive
            (result ??= []).Add(o);
        }
        return result is null ? [] : [.. result];
    }

    /// <summary><see cref="DeliveredOrderIndices(IReadOnlyWorldState, SimConfig, IReadOnlyList{OrderRecord})"/> on a
    /// step's batch.</summary>
    public static int[] DeliveredOrderIndices(IReadOnlyWorldState prev, SimConfig cfg, OrderBatch batch) =>
        DeliveredOrderIndices(prev, cfg, Orders(batch));

    /// <summary>The DevelopRoads order road development applies for <paramref name="polity"/> this step (see
    /// <see cref="DeliveredOrderIndices(IReadOnlyWorldState, SimConfig, IReadOnlyList{OrderRecord})"/>), or null.</summary>
    public static OrderRecord? DeliveredOrderFor(
        IReadOnlyWorldState prev, SimConfig cfg, IReadOnlyList<OrderRecord> orders, PolityId polity)
    {
        int[] delivered = DeliveredOrderIndices(prev, cfg, orders);
        for (int i = 0; i < delivered.Length; i++)
            if (orders[delivered[i]].ActorId == polity.Value) return orders[delivered[i]];
        return null;
    }

    /// <summary><see cref="DeliveredOrderFor(IReadOnlyWorldState, SimConfig, IReadOnlyList{OrderRecord}, PolityId)"/>
    /// on a step's batch.</summary>
    public static OrderRecord? DeliveredOrderFor(IReadOnlyWorldState prev, SimConfig cfg, OrderBatch batch, PolityId polity) =>
        DeliveredOrderFor(prev, cfg, Orders(batch), polity);

    /// <summary>
    /// WHAT A DevelopRoads ORDER OF <paramref name="percent"/>% WOULD BE CHARGED (D-054 WS2's D_r), per good id, against
    /// the civilization holding <paramref name="available"/> (indexed by good id; a good beyond it holds 0), WITHOUT
    /// DRAWING: road development's own plan (<see cref="Plan(IReadOnlyWorldState, IReadOnlyTable{TransportEdgeRow}, ResearchContent, RoadsConfig, GoodsConfig, PolityId, double)"/>
    /// on <paramref name="prev"/>'s route rows) and its own charge arithmetic (<see cref="ChargeStep"/>) step by step, the
    /// holding reduced by each step's charges, ending where the system ends — after a step it could pay only in part, or
    /// at one it can pay nothing of. A pure function: it reads and allocates, and changes nothing. The route rows are
    /// PREV's: within one step a second Empire's order would see the first's modernization (the system plans on its NEXT
    /// rows), which this does not — an approximation of the forecast only, never of what is drawn. All zeros when road
    /// development is inert or <paramref name="percent"/> is not in (0, 100].
    /// </summary>
    public static long[] SimulateCharges(IReadOnlyWorldState prev, SimConfig cfg, PolityId polity, double percent, long[] available)
    {
        var charged = new long[available.Length];
        if (cfg.Roads is not { } roads || cfg.Research is not { } research || cfg.Goods is not { } goods) return charged;
        if (!(percent > 0.0 && percent <= 100.0)) return charged;
        RoadDevelopmentStep[] plan = Plan(prev, prev.TransportEdges, research, roads, goods, polity, percent);
        var holding = (long[])available.Clone();
        for (int p = 0; p < plan.Length; p++)
        {
            RoadMaterialCost[] cost = plan[p].Cost;
            var have = new long[cost.Length];
            for (int m = 0; m < cost.Length; m++)
            {
                int g = cost[m].Good.Value;
                have[m] = g >= 0 && g < holding.Length ? holding[g] : 0;
            }
            RoadStepCharge step = ChargeStep(cost, have, plan[p].Route.StartFraction);
            if (!step.Paid) break;                                   // nothing affordable: the order ends here
            for (int m = 0; m < cost.Length; m++)
            {
                int g = cost[m].Good.Value;
                if (g < 0 || g >= holding.Length) continue;
                holding[g] -= step.Charges[m];
                charged[g] += step.Charges[m];
            }
            if (!step.Complete) break;                               // paid in part: the order ends after it
        }
        return charged;
    }

    /// <summary>The roster rule road development applies to an order's actor: on the roster, or any actor in a world
    /// with no roster (a toy has none to check against).</summary>
    public static bool IsRosterPolity(IReadOnlyWorldState prev, PolityId polity)
    {
        if (prev.Polities.Count == 0) return true;
        for (int i = 0; i < prev.Polities.Count; i++)
            if (prev.Polities[i].Id.Value == polity.Value) return true;
        return false;
    }

    /// <summary>A step's batch as a list (its orders in log order).</summary>
    public static OrderRecord[] Orders(OrderBatch batch)
    {
        var orders = new OrderRecord[batch.Count];
        for (int i = 0; i < orders.Length; i++) orders[i] = batch[i];
        return orders;
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
