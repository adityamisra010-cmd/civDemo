using Sim.Core.Systems;

namespace Sim.Core.State;

/// <summary>
/// ADR-032 — READ-ONLY QUERIES OVER THE TRANSPORT GRAPH (the built inter-settlement
/// multigraph in <see cref="IReadOnlyWorldState.TransportEdges"/> plus the free DirtPath
/// baseline every reachable settlement pair already has through
/// <see cref="IReadOnlyWorldState.SettlementDistances"/>).
///
/// JUNCTIONS ARE DERIVED, NEVER STORED (the Director's ruling 10): a settlement where three or
/// more built edges meet IS a junction. The graph is a multigraph — every query here returns
/// every separate physical route between two endpoints; nothing assumes one edge per pair. Edges carry no
/// owner (ruling 6); nothing here reads territory, claims or controls.
///
/// THE TRAVEL-TIME HOOK (<see cref="EstimateFreight"/>): the deterministic, dt-free answer to
/// "how long does a shipment of T tonnes take from X to Y over the network as built?", using
/// the same stored effective road performance (<see cref="RoadPerformance"/>) as the
/// authoritative Pathfinder. It is the seam the Director's trade note asks for (bad roads slow trade; a standing "buy 100 steel
/// every month" contract is paced by it). No system consumes it yet — see ADR-032 §7 for how
/// trade will.
///
/// Pure functions over their inputs: no cache, no state, no RNG; every enumeration is in table
/// row order or ascending id; Dijkstra orders by the composite key (cost, settlement index).
/// </summary>
public static class TransportQuery
{
    /// <summary>Days per sim-year used to turn a per-year capacity into a duration (365.25).</summary>
    public const double DaysPerYear = 365.25;

    // ------------------------------------------------------------------ topology

    /// <summary>The edge with id <paramref name="edge"/>.</summary>
    public static bool TryGetEdge(IReadOnlyWorldState world, int edge, out TransportEdgeRow row) =>
        TryGetEdge(world.TransportEdges, edge, out row);

    /// <summary>The edge with id <paramref name="edge"/> in an explicit edge table.</summary>
    public static bool TryGetEdge(IReadOnlyTable<TransportEdgeRow> table, int edge, out TransportEdgeRow row)
    {
        for (int i = 0; i < table.Count; i++)
        {
            if (table[i].Id != edge) continue;
            row = table[i];
            return true;
        }
        row = default;
        return false;
    }

    /// <summary>Number of built edges with an endpoint at <paramref name="place"/> (all modes).</summary>
    public static int Degree(IReadOnlyWorldState world, SettlementId place)
    {
        int n = 0;
        for (int i = 0; i < world.TransportEdges.Count; i++)
        {
            TransportEdgeRow e = world.TransportEdges[i];
            if (e.A.Value == place.Value || e.B.Value == place.Value) n++;
        }
        return n;
    }

    /// <summary>Every built edge touching <paramref name="place"/>, table row order.</summary>
    public static TransportEdgeRow[] EdgesAt(IReadOnlyWorldState world, SettlementId place)
    {
        var result = new List<TransportEdgeRow>();
        for (int i = 0; i < world.TransportEdges.Count; i++)
        {
            TransportEdgeRow e = world.TransportEdges[i];
            if (e.A.Value == place.Value || e.B.Value == place.Value) result.Add(e);
        }
        return [.. result];
    }

    /// <summary>Every route row between two settlements (either order), row order — genuinely separate physical routes; modernization never adds one.</summary>
    public static TransportEdgeRow[] EdgesBetween(IReadOnlyWorldState world, SettlementId x, SettlementId y) =>
        EdgesBetween(world.TransportEdges, x, y);

    /// <summary><see cref="EdgesBetween(IReadOnlyWorldState, SettlementId, SettlementId)"/> over an explicit edge table.</summary>
    public static TransportEdgeRow[] EdgesBetween(IReadOnlyTable<TransportEdgeRow> table, SettlementId x, SettlementId y)
    {
        var result = new List<TransportEdgeRow>();
        for (int i = 0; i < table.Count; i++)
        {
            TransportEdgeRow e = table[i];
            if ((e.A.Value == x.Value && e.B.Value == y.Value) || (e.A.Value == y.Value && e.B.Value == x.Value))
                result.Add(e);
        }
        return [.. result];
    }

    /// <summary>Distinct settlements joined to <paramref name="place"/> by a built edge, ascending id.</summary>
    public static SettlementId[] Neighbors(IReadOnlyWorldState world, SettlementId place)
    {
        var ids = new List<int>();
        for (int i = 0; i < world.TransportEdges.Count; i++)
        {
            TransportEdgeRow e = world.TransportEdges[i];
            int other = e.A.Value == place.Value ? e.B.Value : e.B.Value == place.Value ? e.A.Value : -1;
            if (other >= 0 && !ids.Contains(other)) ids.Add(other);
        }
        ids.Sort();
        var result = new SettlementId[ids.Count];
        for (int i = 0; i < ids.Count; i++) result[i] = new SettlementId(ids[i]);
        return result;
    }

    /// <summary>Distinct transport modes present at <paramref name="place"/>, ascending.</summary>
    public static int[] ModesAt(IReadOnlyWorldState world, SettlementId place)
    {
        var modes = new List<int>();
        for (int i = 0; i < world.TransportEdges.Count; i++)
        {
            TransportEdgeRow e = world.TransportEdges[i];
            if ((e.A.Value == place.Value || e.B.Value == place.Value) && !modes.Contains(e.Mode)) modes.Add(e.Mode);
        }
        modes.Sort();
        return [.. modes];
    }

    /// <summary>The implicit junction rule: three or more built edges meet here.</summary>
    public static bool IsJunction(IReadOnlyWorldState world, SettlementId place) => Degree(world, place) >= 3;

    /// <summary>Every derived junction, ascending settlement id.</summary>
    public static SettlementId[] Junctions(IReadOnlyWorldState world)
    {
        var result = new List<SettlementId>();
        for (int s = 0; s < world.Settlements.Count; s++)
            if (IsJunction(world, world.Settlements[s].Id)) result.Add(world.Settlements[s].Id);
        result.Sort();
        return [.. result];
    }

    /// <summary>Route intersections between two settlements' edge sets: settlements both reach by
    /// one built edge (shared neighbours), ascending id.</summary>
    public static SettlementId[] SharedNeighbors(IReadOnlyWorldState world, SettlementId x, SettlementId y)
    {
        SettlementId[] a = Neighbors(world, x), b = Neighbors(world, y);
        var result = new List<SettlementId>();
        for (int i = 0; i < a.Length; i++)
            for (int j = 0; j < b.Length; j++)
                if (a[i].Value == b[j].Value) result.Add(a[i]);
        return [.. result];
    }

    /// <summary>The best (highest) built road class between two settlements; DirtPath when none
    /// is built — the baseline is always there.</summary>
    public static int BestClassBetween(IReadOnlyWorldState world, SettlementId x, SettlementId y)
    {
        int best = EdgeTypes.DirtPath;
        TransportEdgeRow[] edges = EdgesBetween(world, x, y);
        for (int i = 0; i < edges.Length; i++)
            if (edges[i].Mode == TransportModes.Road && edges[i].EdgeType > best) best = edges[i].EdgeType;
        return best;
    }

    // ------------------------------------------------------------------ the travel-time hook

    /// <summary>
    /// A freight estimate. <c>CostKm</c> is the route's ideal-ground-equivalent length after
    /// class speed factors (a built road shortens it; the baseline does not). <c>TransitDays</c> =
    /// CostKm / roads.baselineKmPerDay. <c>BottleneckCapacityTonnesPerYear</c> is the lowest
    /// per-edge capacity along the chosen route, and <c>ThroughputDays</c> = tonnes / that capacity
    /// × <see cref="DaysPerYear"/> (a per-year rate turned into a duration — never a per-turn
    /// amount, law 3). <c>TotalDays</c> = transit + throughput. <c>Path</c> lists the settlements
    /// visited; <c>Links</c> the edge id taken at each hop (-1 = the free baseline).
    /// </summary>
    public readonly record struct FreightEstimate(
        bool Reachable, double CostKm, double TransitDays, long BottleneckCapacityTonnesPerYear,
        double ThroughputDays, double TotalDays, SettlementId[] Path, int[] Links);

    /// <summary>
    /// Ideal-ground km per pathfinding cost unit for this world (ADR-032): the terrain scale at
    /// the lattice stride every system uses; 1.0 on a terrain-less toy world.
    /// </summary>
    public static double KmPerCostUnit(IReadOnlyWorldState world) =>
        world.Terrain is null ? 1.0 : Pathing.LatticeGeometry.KmPerCostUnitOnIdealGround(world.Terrain);

    /// <summary>The free-baseline length of a settlement pair (ideal-ground-equivalent km) from the
    /// cached pairwise travel cost; +∞ when unreachable or not yet computed.</summary>
    public static double BaselineKm(IReadOnlyWorldState world, SettlementId from, SettlementId to, double kmPerCostUnit)
    {
        for (int i = 0; i < world.SettlementDistances.Count; i++)
        {
            SettlementDistanceRow d = world.SettlementDistances[i];
            if (d.From.Value == from.Value && d.To.Value == to.Value) return d.TravelCost * kmPerCostUnit;
        }
        return double.PositiveInfinity;
    }

    /// <summary>
    /// THE TRAVEL-TIME HOOK. Shortest route by class-weighted length over the free baseline
    /// (every SettlementDistances pair — the authoritative Pathfinder's pairwise cost, which
    /// already routes over road lanes — at the DirtPath speed factor and capacity) plus every
    /// travelled route row (<see cref="RoadPerformance.TravelKm"/>: LengthKm × the row's stored
    /// EFFECTIVE, interpolated cost factor — the very value Pathfinder's overlay uses — at its
    /// effective capacity). One road-performance definition, never a second one.
    /// Deterministic: Dijkstra keyed (cost, settlement row index); links relaxed in table row
    /// order, a strictly-better cost only (an equal cost keeps the first found).
    /// </summary>
    public static FreightEstimate EstimateFreight(
        IReadOnlyWorldState world, RoadsConfig roads, SettlementId from, SettlementId to, double tonnes)
    {
        double kmPerCost = KmPerCostUnit(world);
        int n = world.Settlements.Count;
        int src = IndexOf(world, from), dst = IndexOf(world, to);
        var none = new FreightEstimate(false, double.PositiveInfinity, double.PositiveInfinity, 0,
            double.PositiveInfinity, double.PositiveInfinity, [], []);
        if (src < 0 || dst < 0) return none;
        RoadClassConfig dirt = roads.ClassOf(EdgeTypes.DirtPath)!;

        var dist = new double[n];
        var prevNode = new int[n];
        var prevLink = new int[n];
        var prevCap = new long[n];
        var done = new bool[n];
        Array.Fill(dist, double.PositiveInfinity);
        Array.Fill(prevNode, -1);
        dist[src] = 0.0;

        for (int iter = 0; iter < n; iter++)
        {
            int u = -1;
            for (int i = 0; i < n; i++)
                if (!done[i] && dist[i] < double.PositiveInfinity && (u < 0 || dist[i] < dist[u])) u = i;
            if (u < 0) break;
            done[u] = true;
            if (u == dst) break;
            int uId = world.Settlements[u].Id.Value;

            // Baseline links (the free DirtPath layer), SettlementDistances row order.
            for (int i = 0; i < world.SettlementDistances.Count; i++)
            {
                SettlementDistanceRow d = world.SettlementDistances[i];
                if (d.From.Value != uId || !double.IsFinite(d.TravelCost)) continue;
                int v = IndexOf(world, d.To);
                if (v < 0 || done[v]) continue;
                Relax(u, v, d.TravelCost * kmPerCost * dirt.SpeedFactor, -1, dirt.CapacityTonnesPerYear);
            }
            // Built road edges, table row order, both directions.
            for (int i = 0; i < world.TransportEdges.Count; i++)
            {
                TransportEdgeRow e = world.TransportEdges[i];
                if (!RoadPerformance.IsTravelled(e)) continue;
                int other = e.A.Value == uId ? e.B.Value : e.B.Value == uId ? e.A.Value : -1;
                if (other < 0) continue;
                int v = IndexOf(world, new SettlementId(other));
                if (v < 0 || done[v]) continue;
                Relax(u, v, RoadPerformance.TravelKm(e), e.Id, e.CapacityTonnesPerYear);
            }
        }

        if (!double.IsFinite(dist[dst])) return none;

        var path = new List<SettlementId>();
        var links = new List<int>();
        long bottleneck = long.MaxValue;
        for (int v = dst; v != src; v = prevNode[v])
        {
            path.Add(world.Settlements[v].Id);
            links.Add(prevLink[v]);
            if (prevCap[v] < bottleneck) bottleneck = prevCap[v];
        }
        path.Add(world.Settlements[src].Id);
        path.Reverse();
        links.Reverse();
        if (src == dst) bottleneck = 0;

        double transit = dist[dst] / roads.BaselineKmPerDay;
        double throughput = bottleneck > 0 ? tonnes / bottleneck * DaysPerYear : 0.0;
        return new FreightEstimate(true, dist[dst], transit, bottleneck == long.MaxValue ? 0 : bottleneck,
            throughput, transit + throughput, [.. path], [.. links]);

        void Relax(int u, int v, double w, int link, long cap)
        {
            double cand = dist[u] + w;
            if (cand < dist[v])
            {
                dist[v] = cand;
                prevNode[v] = u;
                prevLink[v] = link;
                prevCap[v] = cap;
            }
        }
    }

    private static int IndexOf(IReadOnlyWorldState world, SettlementId id)
    {
        for (int i = 0; i < world.Settlements.Count; i++)
            if (world.Settlements[i].Id.Value == id.Value) return i;
        return -1;
    }
}
