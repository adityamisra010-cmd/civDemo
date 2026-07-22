using Sim.Core.State;

namespace Sim.Core.Pathing;

/// <summary>
/// Pure pathfinding and isochrone queries over (lattice, network tables).
///
/// PURITY MANDATE (T1.3): these are pure functions over their inputs — NO
/// persistent caches, NO memoization. A cache is state, and state is determinism
/// surface (it would need cloning, hashing, and invalidation discipline); defer
/// any caching until a profiling gate demands it, and then only via ADR.
///
/// DETERMINISM MANDATE (m1 spec §3): the priority queue orders by the composite
/// key (f, node id) — a total order; never insertion order, never a bare fractional
/// priority. All adjacency enumeration is in fixed offset order; the network
/// overlay is built in edge-table row order.
///
/// COMPLEXITY: A* / Dijkstra are O(E log V) with V = lattice nodes (65,536 at
/// the 256² default), E ≈ 8V lattice steps + 2·edges overlay; the binary heap
/// gives the log factor. Isochrone is Dijkstra truncated at the budget.
/// </summary>
public static class Pathfinder
{
    public readonly record struct PathResult(bool Found, int[] Nodes, double TotalCost);

    public sealed class IsochroneResult
    {
        /// <summary>Reached lattice nodes in ascending id order.</summary>
        public required int[] Reached { get; init; }

        /// <summary>Travel cost per reached node (parallel to Reached).</summary>
        public required double[] Costs { get; init; }

        /// <summary>Reached nodes with at least one passable unreached neighbor, ascending id.</summary>
        public required int[] Boundary { get; init; }
    }

    // Fixed neighbor offsets: W, E, N, S, NW, NE, SW, SE (cardinals first).
    private static readonly int[] Dx = [-1, 1, 0, 0, -1, 1, -1, 1];
    private static readonly int[] Dy = [0, 0, -1, 1, -1, -1, 1, 1];

    /// <summary>
    /// A* over the lattice + network fast lanes.
    ///
    /// HEURISTIC ADMISSIBILITY: h(n) = straightLineDistance(n, goal) in stride
    /// units × effMinCostPerUnit, where effMinCostPerUnit is the minimum of
    /// (a) the cheapest passable lattice node cost (a lattice step a→b costs
    /// mean(cost_a, cost_b) × length ≥ MinNodeCost × length), and (b) over every
    /// network edge, Cost / straightLineDistance(A,B) (so traversing an edge
    /// also costs ≥ its endpoints' straight-line distance × effMin). Any path's
    /// total cost is therefore ≥ its total geometric length × effMin, and by the
    /// triangle inequality that length ≥ straight-line distance to the goal —
    /// h never overestimates, so A* returns optimal costs (the Dijkstra
    /// equivalence test binds this empirically).
    /// </summary>
    public static PathResult FindPath(
        TraversalLattice lattice, IReadOnlyWorldState world, int fromNode, int toNode)
    {
        int n = lattice.NodeCount;
        if (!lattice.IsPassable(fromNode) || !lattice.IsPassable(toNode))
            return new PathResult(false, [], 0.0);

        (int[][] overlayTargets, double[][] overlayCosts, double minEdgePerUnit) =
            BuildOverlay(lattice, world);
        double effMin = Math.Min(lattice.MinNodeCost, minEdgePerUnit);

        var g = new double[n];
        var cameFrom = new int[n];
        var closed = new bool[n];
        Array.Fill(g, double.MaxValue);
        Array.Fill(cameFrom, -1);

        (int gx, int gy) = lattice.Coords(toNode);
        var open = new MinHeap(256);
        g[fromNode] = 0.0;
        open.Push(Heuristic(lattice, fromNode, gx, gy, effMin), fromNode);

        while (open.Count > 0)
        {
            (_, int current) = open.Pop();
            if (closed[current]) continue; // stale entry (lazy deletion)
            closed[current] = true;
            if (current == toNode) break;

            Expand(lattice, overlayTargets, overlayCosts, current, g, cameFrom, closed,
                (node, tentative) => open.Push(
                    tentative + Heuristic(lattice, node, gx, gy, effMin), node));
        }

        if (g[toNode] == double.MaxValue) return new PathResult(false, [], 0.0);

        var path = new List<int>();
        for (int c = toNode; c != -1; c = cameFrom[c]) path.Add(c);
        path.Reverse();
        return new PathResult(true, [.. path], g[toNode]);
    }

    /// <summary>
    /// Isochrone(origin, budget): Dijkstra truncated at the budget. The reached
    /// set is contiguous by construction (a flood from the origin) and monotone
    /// in the budget (a larger budget settles a superset).
    /// This is the D-016 catchment recompute entry point: call it ONLY on the
    /// catchment-invalidating events (network changed — T1.6 PathBuild;
    /// settlement changed — T1.4; terrain changed — never in M1), never per-turn.
    /// </summary>
    public static IsochroneResult Isochrone(
        TraversalLattice lattice, IReadOnlyWorldState world, int origin, double budget)
    {
        int n = lattice.NodeCount;
        (int[][] overlayTargets, double[][] overlayCosts, _) = BuildOverlay(lattice, world);

        var g = new double[n];
        var closed = new bool[n];
        Array.Fill(g, double.MaxValue);

        var open = new MinHeap(256);
        if (lattice.IsPassable(origin))
        {
            g[origin] = 0.0;
            open.Push(0.0, origin);
        }

        while (open.Count > 0)
        {
            (double cost, int current) = open.Pop();
            if (closed[current]) continue;
            if (cost > budget) break; // heap is monotone: everything further is over budget
            closed[current] = true;

            Expand(lattice, overlayTargets, overlayCosts, current, g, null, closed,
                (node, tentative) => { if (tentative <= budget) open.Push(tentative, node); });
        }

        var reached = new List<int>();
        for (int i = 0; i < n; i++) if (closed[i]) reached.Add(i);

        var boundary = new List<int>();
        foreach (int i in reached)
        {
            (int x, int y) = lattice.Coords(i);
            for (int d = 0; d < 8; d++)
            {
                int nx = x + Dx[d], ny = y + Dy[d];
                if (nx < 0 || ny < 0 || nx >= lattice.Size || ny >= lattice.Size) continue;
                int nb = ny * lattice.Size + nx;
                if (!closed[nb] && lattice.IsPassable(nb)) { boundary.Add(i); break; }
            }
        }

        var costs = new double[reached.Count];
        for (int i = 0; i < reached.Count; i++) costs[i] = g[reached[i]];
        return new IsochroneResult { Reached = [.. reached], Costs = costs, Boundary = [.. boundary] };
    }

    /// <summary>
    /// The catchment PARTITION (T2.3, m2 spec §3): ONE multi-source Dijkstra
    /// with every settlement origin as a source, truncated at the budget. Each
    /// lattice node is CLAIMED by the composite key (travel cost, settlement
    /// id) — the first source to settle it under that total order owns it, so
    /// no node is ever claimed twice BY CONSTRUCTION (one owner array cell).
    /// Ties (two sources reaching a node at bit-equal cost) go to the lower
    /// settlement INDEX (== lower SettlementId in founding pick order): the
    /// relax rule below propagates ownership on strictly-better cost, or on
    /// equal cost from a lower-indexed owner. Uses the same lattice + network
    /// overlay expansion as Isochrone (fast lanes shape the borders too).
    /// Owner is −1 for unreached/over-budget nodes.
    /// </summary>
    public readonly struct PartitionResult
    {
        public required int[] Owner { get; init; }   // settlement INDEX per node; −1 unclaimed
        public required double[] Cost { get; init; } // travel cost per node (MaxValue unclaimed)
    }

    public static PartitionResult Partition(
        TraversalLattice lattice, IReadOnlyWorldState world, ReadOnlySpan<int> origins, double budget)
    {
        int n = lattice.NodeCount;
        (int[][] overlayTargets, double[][] overlayCosts, _) = BuildOverlay(lattice, world);

        var g = new double[n];
        var owner = new int[n];
        var closed = new bool[n];
        Array.Fill(g, double.MaxValue);
        Array.Fill(owner, -1);

        var open = new MinHeap(256);
        for (int s = 0; s < origins.Length; s++)
        {
            int origin = origins[s];
            if (!lattice.IsPassable(origin)) continue;
            // Two settlements sharing an origin node cannot happen under D-025
            // spacing; if forced, the composite tie rule (lower index) holds.
            if (g[origin] == 0.0 && owner[origin] >= 0) continue;
            g[origin] = 0.0;
            owner[origin] = s;
            open.Push(0.0, origin);
        }

        while (open.Count > 0)
        {
            (double cost, int current) = open.Pop();
            if (closed[current]) continue;
            if (cost > budget) break; // heap is monotone: everything further is over budget
            closed[current] = true;

            (int x, int y) = lattice.Coords(current);
            for (int d = 0; d < 8; d++)
            {
                int nx = x + Dx[d], ny = y + Dy[d];
                if (nx < 0 || ny < 0 || nx >= lattice.Size || ny >= lattice.Size) continue;
                int nb = ny * lattice.Size + nx;
                if (closed[nb] || !lattice.IsPassable(nb)) continue;
                RelaxClaim(current, nb, g[current] + lattice.StepCost(current, nb), g, owner, open, budget);
            }
            int[] targets = overlayTargets[current];
            double[] costs = overlayCosts[current];
            for (int e = 0; e < targets.Length; e++)
            {
                int nb = targets[e];
                if (closed[nb] || !lattice.IsPassable(nb)) continue;
                RelaxClaim(current, nb, g[current] + costs[e], g, owner, open, budget);
            }
        }

        // Nodes never settled within budget are unclaimed (their tentative g /
        // owner may hold over-budget speculation — normalize them out).
        for (int i = 0; i < n; i++)
        {
            if (!closed[i]) { owner[i] = -1; g[i] = double.MaxValue; }
        }
        return new PartitionResult { Owner = owner, Cost = g };
    }

    private static void RelaxClaim(
        int from, int to, double tentative, double[] g, int[] owner, MinHeap open, double budget)
    {
        // The claim key (travel cost, settlement index): strictly-better cost
        // wins; bit-equal cost goes to the lower-indexed settlement.
        if (tentative < g[to] || (tentative == g[to] && owner[from] < owner[to]))
        {
            g[to] = tentative;
            owner[to] = owner[from];
            if (tentative <= budget) open.Push(tentative, to);
        }
    }

    /// <summary>
    /// D-025 spacing support (T2.3): capped Dijkstra from one origin over RAW
    /// terrain (no network overlay — spacing is a worldgen property), relaxing
    /// <paramref name="minCost"/> in place: minCost[node] ← min(existing,
    /// travel cost from this origin). Nodes beyond the cap are untouched —
    /// the caller treats them as infinitely far (the prefilter stage).
    /// </summary>
    public static void RelaxCappedFrom(
        TraversalLattice lattice, int origin, double cap, double[] minCost)
    {
        int n = lattice.NodeCount;
        var g = new double[n];
        var closed = new bool[n];
        Array.Fill(g, double.MaxValue);

        var open = new MinHeap(256);
        if (lattice.IsPassable(origin))
        {
            g[origin] = 0.0;
            open.Push(0.0, origin);
        }
        while (open.Count > 0)
        {
            (double cost, int current) = open.Pop();
            if (closed[current]) continue;
            if (cost > cap) break;
            closed[current] = true;
            if (cost < minCost[current]) minCost[current] = cost;

            (int x, int y) = lattice.Coords(current);
            for (int d = 0; d < 8; d++)
            {
                int nx = x + Dx[d], ny = y + Dy[d];
                if (nx < 0 || ny < 0 || nx >= lattice.Size || ny >= lattice.Size) continue;
                int nb = ny * lattice.Size + nx;
                if (closed[nb] || !lattice.IsPassable(nb)) continue;
                double tentative = g[current] + lattice.StepCost(current, nb);
                if (tentative < g[nb])
                {
                    g[nb] = tentative;
                    if (tentative <= cap) open.Push(tentative, nb);
                }
            }
        }
    }

    // --- shared expansion ------------------------------------------------------

    private static void Expand(
        TraversalLattice lattice, int[][] overlayTargets, double[][] overlayCosts,
        int current, double[] g, int[]? cameFrom, bool[] closed, Action<int, double> push)
    {
        (int x, int y) = lattice.Coords(current);
        for (int d = 0; d < 8; d++)
        {
            int nx = x + Dx[d], ny = y + Dy[d];
            if (nx < 0 || ny < 0 || nx >= lattice.Size || ny >= lattice.Size) continue;
            int nb = ny * lattice.Size + nx;
            if (closed[nb] || !lattice.IsPassable(nb)) continue;
            Relax(current, nb, g[current] + lattice.StepCost(current, nb), g, cameFrom, push);
        }

        int[] targets = overlayTargets[current];
        double[] costs = overlayCosts[current];
        for (int e = 0; e < targets.Length; e++)
        {
            int nb = targets[e];
            if (closed[nb] || !lattice.IsPassable(nb)) continue;
            Relax(current, nb, g[current] + costs[e], g, cameFrom, push);
        }
    }

    private static void Relax(
        int from, int to, double tentative, double[] g, int[]? cameFrom, Action<int, double> push)
    {
        // Strictly-better only: with the (key, id) total order on the heap the
        // expansion sequence is deterministic, so the surviving parent is too.
        if (tentative < g[to])
        {
            g[to] = tentative;
            if (cameFrom is not null) cameFrom[to] = from;
            push(to, tentative);
        }
    }

    private static double Heuristic(TraversalLattice lattice, int node, int gx, int gy, double effMin)
    {
        (int x, int y) = lattice.Coords(node);
        double dx = x - gx, dy = y - gy;
        return Math.Sqrt(dx * dx + dy * dy) * effMin;
    }

    /// <summary>
    /// Per-call network overlay (pure — rebuilt from the tables each query):
    /// adjacency per lattice node in edge-table row order, both directions.
    /// Also returns the minimum edge cost per straight-line stride unit for the
    /// heuristic's admissibility bound.
    /// </summary>
    private static (int[][] Targets, double[][] Costs, double MinEdgePerUnit) BuildOverlay(
        TraversalLattice lattice, IReadOnlyWorldState world)
    {
        int n = lattice.NodeCount;
        var counts = new int[n];
        int edgeCount = world.NetworkEdges.Count;

        // Resolve network node id → lattice anchor (id-indexed lookup array).
        var anchor = new int[world.NetworkNodes.Count == 0 ? 0 : MaxNodeId(world) + 1];
        for (int i = 0; i < world.NetworkNodes.Count; i++)
        {
            NetworkNodeRow row = world.NetworkNodes[i];
            anchor[row.Id.Value] = row.LatticeNode;
        }

        double minEdgePerUnit = double.MaxValue;
        for (int i = 0; i < edgeCount; i++)
        {
            NetworkEdgeRow e = world.NetworkEdges[i];
            counts[anchor[e.A.Value]]++;
            counts[anchor[e.B.Value]]++;

            (int ax, int ay) = lattice.Coords(anchor[e.A.Value]);
            (int bx, int by) = lattice.Coords(anchor[e.B.Value]);
            double dx = ax - bx, dy = ay - by;
            double dist = Math.Sqrt(dx * dx + dy * dy);
            if (dist > 0.0)
            {
                double perUnit = e.Cost / dist;
                if (perUnit < minEdgePerUnit) minEdgePerUnit = perUnit;
            }
        }

        var targets = new int[n][];
        var costs = new double[n][];
        var empty = Array.Empty<int>();
        var emptyCosts = Array.Empty<double>();
        for (int i = 0; i < n; i++)
        {
            targets[i] = counts[i] == 0 ? empty : new int[counts[i]];
            costs[i] = counts[i] == 0 ? emptyCosts : new double[counts[i]];
        }
        var fill = new int[n];
        for (int i = 0; i < edgeCount; i++)
        {
            NetworkEdgeRow e = world.NetworkEdges[i];
            int a = anchor[e.A.Value], b = anchor[e.B.Value];
            targets[a][fill[a]] = b; costs[a][fill[a]++] = e.Cost;
            targets[b][fill[b]] = a; costs[b][fill[b]++] = e.Cost;
        }
        return (targets, costs, minEdgePerUnit);
    }

    private static int MaxNodeId(IReadOnlyWorldState world)
    {
        int max = 0;
        for (int i = 0; i < world.NetworkNodes.Count; i++)
            if (world.NetworkNodes[i].Id.Value > max) max = world.NetworkNodes[i].Id.Value;
        return max;
    }

    // --- deterministic binary min-heap on (key, id) — the T1.3 mandate ---------

    private sealed class MinHeap(int capacity)
    {
        private double[] _keys = new double[capacity];
        private int[] _ids = new int[capacity];
        public int Count { get; private set; }

        public void Push(double key, int id)
        {
            if (Count == _keys.Length)
            {
                Array.Resize(ref _keys, _keys.Length * 2);
                Array.Resize(ref _ids, _ids.Length * 2);
            }
            int c = Count++;
            _keys[c] = key; _ids[c] = id;
            while (c > 0)
            {
                int parent = (c - 1) / 2;
                if (!Less(c, parent)) break;
                Swap(c, parent);
                c = parent;
            }
        }

        public (double Key, int Id) Pop()
        {
            (double key, int id) = (_keys[0], _ids[0]);
            Count--;
            _keys[0] = _keys[Count]; _ids[0] = _ids[Count];
            int c = 0;
            while (true)
            {
                int l = 2 * c + 1, r = l + 1, smallest = c;
                if (l < Count && Less(l, smallest)) smallest = l;
                if (r < Count && Less(r, smallest)) smallest = r;
                if (smallest == c) break;
                Swap(c, smallest);
                c = smallest;
            }
            return (key, id);
        }

        // Composite (key, id): a total order — never a bare fractional priority.
        private bool Less(int a, int b) =>
            _keys[a] < _keys[b] || (_keys[a] == _keys[b] && _ids[a] < _ids[b]);

        private void Swap(int a, int b)
        {
            (_keys[a], _keys[b]) = (_keys[b], _keys[a]);
            (_ids[a], _ids[b]) = (_ids[b], _ids[a]);
        }
    }
}
