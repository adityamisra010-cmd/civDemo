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
/// TOPOLOGY (batch 3 WS3): on a wrapped lattice (<see cref="TraversalLattice.WrapX"/>) every neighbour enumeration
/// here — Expand, the Isochrone boundary scan, RelaxCappedFrom, LegCosts — steps across the east-west seam through
/// <see cref="MapTopology.NeighbourX"/>, and the A* heuristic and the lane bound measure the short way round
/// (<see cref="MapTopology.Dx"/>). On a flat lattice each is the pre-batch-3 arithmetic exactly.
///
/// COMPLEXITY: A* / Dijkstra are O(E log V) with V = lattice nodes (65,536 at
/// the 256² default), E ≈ 8V lattice steps + 2·edges overlay; the binary heap
/// gives the log factor. Isochrone is Dijkstra truncated at the budget.
///
/// BATCH 4 (D-055, ADR-040 — the cylinder pathfinding profile, item 5): four byte-identical speed-ups, NO state added
/// (the purity mandate above holds unchanged; every array is a local of one call):
/// O3 the lane overlay is built per call in COMPRESSED SPARSE ROW form (<see cref="LaneOverlay"/>: one offsets array
///    and two flat lane arrays) instead of one jagged array pair per lattice node, each node's lanes in the batch-3
///    order — NetworkEdges in row order then travelled roads in row order, each lane's A endpoint before its B;
/// O4 FindPath expands inline: the step cost comes from the direction (<see cref="TraversalLattice.StepCostKnown"/>),
///    the heuristic from the neighbour's known coordinates with the branch-only wrapped dx
///    (<see cref="MapTopology.DxOnMap"/>), and no delegate is invoked per relaxation;
/// O5 <see cref="DistancesTo"/>: the uncapped Dijkstra stopped once every passable target is settled (the
///    catchment's pairwise travel costs);
/// O6 Expand (Isochrone, Partition, DistancesTo) and RelaxCappedFrom take the step cost from the direction too.
/// Each computes the same doubles from the same operands in the same order and relaxes and pushes in the same
/// sequence, so every path, cost, reached set and tie-break is the batch-3 one bit for bit: the batch-3 code is kept
/// verbatim as Sim.Tests' LegacyPathfinder and the differential tests (LegacyPathfinderDifferentialTests) compare the
/// two on every pair of the small tie-dense flat and cylindrical lattices and on a strided sample of the three 64×64
/// SeamWorld lattices (FindPath: every passable origin with every 7th or 11th target; Isochrone, DistancesTo and
/// RelaxCappedFrom: every 7th or 11th passable origin).
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
    /// WS3: on a wrapped lattice "straight-line distance" is the distance ON THE
    /// CYLINDER (x the short way round, <see cref="MapTopology.Dx"/>) in both h and
    /// the lane bound. That is a metric (the triangle inequality holds on the
    /// cylinder), a seam step has the same length 1 or √2 it has inside the map,
    /// and every lane's cost ≥ its wrapped endpoint distance × effMin — so the
    /// argument above carries over unchanged and h stays admissible and
    /// consistent. (The raw flat dx would OVERESTIMATE across the seam.)
    /// </summary>
    public static PathResult FindPath(
        TraversalLattice lattice, IReadOnlyWorldState world, int fromNode, int toNode)
    {
        int n = lattice.NodeCount;
        if (!lattice.IsPassable(fromNode) || !lattice.IsPassable(toNode))
            return new PathResult(false, [], 0.0);

        LaneOverlay overlay = BuildOverlay(lattice, world);
        double effMin = Math.Min(lattice.MinNodeCost, overlay.MinEdgePerUnit);

        var g = new double[n];
        var cameFrom = new int[n];
        var closed = new bool[n];
        Array.Fill(g, double.MaxValue);
        Array.Fill(cameFrom, -1);

        int size = lattice.Size;
        bool wrap = lattice.WrapX;
        (int gx, int gy) = lattice.Coords(toNode);
        (int fx, int fy) = lattice.Coords(fromNode);
        var open = new MinHeap(256);
        g[fromNode] = 0.0;
        open.Push(Heuristic(fx, fy, gx, gy, size, wrap, effMin), fromNode);

        int[] laneOffsets = overlay.Offsets, laneTargets = overlay.Targets;
        double[] laneCosts = overlay.Costs;
        while (open.Count > 0)
        {
            (_, int current) = open.Pop();
            if (closed[current]) continue; // stale entry (lazy deletion)
            closed[current] = true;
            if (current == toNode) break;

            // Batch 4 O4: Expand + Relax inlined — the same neighbours in the same order (the 8 lattice steps W, E, N, S,
            // NW, NE, SW, SE, then the node's lanes in overlay order), the same strictly-better relaxation, and the same
            // push key (tentative + h). g[current] is read once: current is closed, and Relax never writes a closed node.
            (int x, int y) = lattice.Coords(current);
            double gCurrent = g[current];
            for (int d = 0; d < 8; d++)
            {
                int nx = MapTopology.NeighbourX(x, Dx[d], size, wrap), ny = y + Dy[d];
                if (nx < 0 || ny < 0 || ny >= size) continue;
                int nb = ny * size + nx;
                if (closed[nb] || !lattice.IsPassable(nb)) continue;
                double tentative = gCurrent + lattice.StepCostKnown(current, nb, d >= 4);
                if (tentative < g[nb])
                {
                    g[nb] = tentative;
                    cameFrom[nb] = current;
                    open.Push(tentative + Heuristic(nx, ny, gx, gy, size, wrap, effMin), nb);
                }
            }
            for (int e = laneOffsets[current], end = laneOffsets[current + 1]; e < end; e++)
            {
                int nb = laneTargets[e];
                if (closed[nb] || !lattice.IsPassable(nb)) continue;
                double tentative = gCurrent + laneCosts[e];
                if (tentative < g[nb])
                {
                    g[nb] = tentative;
                    cameFrom[nb] = current;
                    (int lx, int ly) = lattice.Coords(nb);
                    open.Push(tentative + Heuristic(lx, ly, gx, gy, size, wrap, effMin), nb);
                }
            }
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
    /// Callers (batch 4): AiFoundingPolicy.ReachableNodes (budget MaxValue: the nodes FindPath can reach from a source)
    /// and the UI's ResourceOverlay reach (budget +∞). The D-016 catchment recompute no longer calls it: catchment
    /// membership comes from <see cref="Partition"/> (T2.3) and the pairwise settlement travel costs from
    /// <see cref="DistancesTo"/> (batch 4, O5). An uncapped call floods the origin's whole component.
    /// </summary>
    public static IsochroneResult Isochrone(
        TraversalLattice lattice, IReadOnlyWorldState world, int origin, double budget)
    {
        int n = lattice.NodeCount;
        LaneOverlay overlay = BuildOverlay(lattice, world);

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

            Expand(lattice, overlay, current, g, null, closed,
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
                int nx = MapTopology.NeighbourX(x, Dx[d], lattice.Size, lattice.WrapX), ny = y + Dy[d];
                if (nx < 0 || ny < 0 || ny >= lattice.Size) continue;
                int nb = ny * lattice.Size + nx;
                if (!closed[nb] && lattice.IsPassable(nb)) { boundary.Add(i); break; }
            }
        }

        var costs = new double[reached.Count];
        for (int i = 0; i < reached.Count; i++) costs[i] = g[reached[i]];
        return new IsochroneResult { Reached = [.. reached], Costs = costs, Boundary = [.. boundary] };
    }

    /// <summary>
    /// The catchment PARTITION (T2.3, m2 spec §3): every settlement origin
    /// claims lattice nodes by the composite key (travel cost, settlement
    /// id) — one owner array cell, so no node is ever claimed twice BY
    /// CONSTRUCTION. Ties (two sources reaching a node at bit-equal cost) go
    /// to the lower settlement INDEX (== lower SettlementId in founding pick
    /// order). Uses the same lattice + network overlay expansion as Isochrone
    /// (fast lanes shape the borders too). Owner is −1 for unreached/
    /// over-budget nodes. Since T3.8 the algorithm is one capped Dijkstra per
    /// source merged at settle time (the per-budget overload explains why);
    /// with all budgets equal that computes exactly what T2.3's single
    /// multi-source pass computed.
    /// </summary>
    public readonly struct PartitionResult
    {
        public required int[] Owner { get; init; }   // settlement INDEX per node; −1 unclaimed
        public required double[] Cost { get; init; } // travel cost per node (MaxValue unclaimed)
    }

    public static PartitionResult Partition(
        TraversalLattice lattice, IReadOnlyWorldState world, ReadOnlySpan<int> origins, double budget)
    {
        Span<double> budgets = origins.Length <= 64
            ? stackalloc double[origins.Length] : new double[origins.Length];
        budgets.Fill(budget);
        return Partition(lattice, world, origins, budgets);
    }

    /// <summary>
    /// T3.8: as above with a PER-SOURCE budget (the settlement-size catchment
    /// bonus). SEMANTICS — the T2.3 witness is the spec: each source's reach
    /// is its OWN capped Dijkstra field, independent of every rival, and a
    /// node goes to the composite (travel cost, settlement index) key among
    /// the sources whose field reaches it within their own budget. Produce may
    /// therefore TRANSIT territory another settlement claims — M3 has no
    /// polity borders (walls are D-037's business, not the hinterland's).
    ///
    /// Implemented as one capped Dijkstra PER SOURCE merged at settle time,
    /// deliberately NOT one multi-source pass: a single-label multi-source
    /// formulation is exact only under EQUAL budgets (a refused tentative is
    /// refused for every source alike), and under UNEQUAL budgets it truncates
    /// a larger-budget source's field behind a nearer smaller-budget rival —
    /// the rival holds the node's only label, then exhausts at its budget, and
    /// the larger-budget source never propagates through (label-stealing).
    /// Measured: PartitionUnequalBudgetTests runs the single-label formulation
    /// inline as a mutant and shows the divergence this implementation exists
    /// to avoid, then pins Partition == per-source witness node-for-node.
    /// </summary>
    public static PartitionResult Partition(
        TraversalLattice lattice, IReadOnlyWorldState world, ReadOnlySpan<int> origins,
        ReadOnlySpan<double> budgets)
    {
        int n = lattice.NodeCount;
        LaneOverlay overlay = BuildOverlay(lattice, world);

        var g = new double[n];      // best composite cost per node (MaxValue unclaimed)
        var owner = new int[n];     // winning settlement INDEX per node (−1 unclaimed)
        Array.Fill(g, double.MaxValue);
        Array.Fill(owner, -1);

        // Scratch reused across sources. Total relax work is the sum of the
        // per-source balls — what the old union pass did plus the overlap
        // between balls (small at D-025 spacing) — plus one O(n) clear pair
        // per source. Recompute is D-016-gated, never per-turn.
        var dist = new double[n];
        var closed = new bool[n];

        for (int s = 0; s < origins.Length; s++)
        {
            int origin = origins[s];
            if (!lattice.IsPassable(origin)) continue;
            double budget = budgets[s];

            Array.Fill(dist, double.MaxValue);
            Array.Clear(closed);
            var open = new MinHeap(256);
            dist[origin] = 0.0;
            open.Push(0.0, origin);

            while (open.Count > 0)
            {
                (double cost, int current) = open.Pop();
                if (closed[current]) continue;
                if (cost > budget) break; // heap is monotone: everything further is over THIS budget
                closed[current] = true;

                // Claim at settle time by the composite key (cost, settlement
                // index): strictly better cost wins; bit-equal cost keeps the
                // lower index (s ascends, so the first writer holds ties —
                // including two settlements forced onto one origin node, which
                // D-025 spacing forbids but the key resolves anyway).
                if (cost < g[current] || (cost == g[current] && s < owner[current]))
                {
                    g[current] = cost;
                    owner[current] = s;
                }

                Expand(lattice, overlay, current, dist, null, closed,
                    (node, tentative) => { if (tentative <= budget) open.Push(tentative, node); });
            }
        }

        return new PartitionResult { Owner = owner, Cost = g };
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
                int nx = MapTopology.NeighbourX(x, Dx[d], lattice.Size, lattice.WrapX), ny = y + Dy[d];
                if (nx < 0 || ny < 0 || ny >= lattice.Size) continue;
                int nb = ny * lattice.Size + nx;
                if (closed[nb] || !lattice.IsPassable(nb)) continue;
                double tentative = g[current] + lattice.StepCostKnown(current, nb, d >= 4); // O6: diagonal iff d ≥ 4
                if (tentative < g[nb])
                {
                    g[nb] = tentative;
                    if (tentative <= cap) open.Push(tentative, nb);
                }
            }
        }
    }

    /// <summary>
    /// M5 S1a — THE COST OF EACH LEG of a lattice path, under exactly the edge
    /// set <see cref="FindPath"/> expands (the shared <c>Expand</c>; batch 4: FindPath's inlined copy of it): a
    /// lattice STEP between 8-neighbours costs <see cref="TraversalLattice.StepCost"/>,
    /// and a LANE costs its overlay cost (<c>BuildOverlay</c>: PathBuild's
    /// network fast lanes and ADR-032's travelled road routes, one definition).
    /// Where several edges join the same two nodes the leg costs the CHEAPEST,
    /// because that is the value A*'s strictly-better relaxation leaves: from
    /// one parent, g[child] = min over its edges of (g[parent] + cost), and
    /// IEEE rounding is monotone, so that equals g[parent] + min(cost) bit for
    /// bit. Summing the legs from 0 along a FindPath result therefore
    /// reproduces A*'s own g at every node of the path — and its TotalCost —
    /// exactly (RouteQueryTests measures it). This is not a second movement
    /// model: there is no cost here that Expand does not charge.
    ///
    /// Leg k joins nodes[k] and nodes[k+1]. Throws when a node is out of range
    /// or impassable, or when two consecutive nodes are joined by no edge —
    /// such a sequence is not a path this pathfinder could return.
    /// </summary>
    public static double[] LegCosts(
        TraversalLattice lattice, IReadOnlyWorldState world, ReadOnlySpan<int> nodes)
    {
        var legs = new double[nodes.Length > 1 ? nodes.Length - 1 : 0];
        for (int k = 0; k < nodes.Length; k++)
            if ((uint)nodes[k] >= (uint)lattice.NodeCount || !lattice.IsPassable(nodes[k]))
                throw new ArgumentException(
                    $"node {nodes[k]} at position {k} is not a passable lattice node", nameof(nodes));
        if (legs.Length == 0) return legs;

        LaneOverlay overlay = BuildOverlay(lattice, world);
        for (int k = 0; k < legs.Length; k++)
        {
            int a = nodes[k], b = nodes[k + 1];
            bool joined = false;
            double best = 0.0;
            // The order Expand relaxes in: the lattice step first, then each lane
            // in adjacency order, a lane replacing only a STRICTLY cheaper cost.
            (int ax, int ay) = lattice.Coords(a);
            (int bx, int by) = lattice.Coords(b);
            // WS3: the lattice step exactly as Expand enumerates it — x adjacency the short way round on a wrapped
            // lattice (columns 0 and Size − 1 are neighbours), so RouteQuery.Find's exact-sum check holds across the seam.
            if (a != b && MapTopology.AbsDx(ax, bx, lattice.Size, lattice.WrapX) <= 1 && Math.Abs(ay - by) <= 1)
            {
                best = lattice.StepCost(a, b);
                joined = true;
            }
            for (int e = overlay.Offsets[a], end = overlay.Offsets[a + 1]; e < end; e++)
            {
                if (overlay.Targets[e] != b) continue;
                if (!joined || overlay.Costs[e] < best) best = overlay.Costs[e];
                joined = true;
            }
            if (!joined)
                throw new ArgumentException(
                    $"nodes {a} and {b} (positions {k}, {k + 1}) are joined by no lattice step and no lane",
                    nameof(nodes));
            legs[k] = best;
        }
        return legs;
    }

    /// <summary>
    /// Batch 4 (D-055, O5; ADR-040) — THE TRAVEL COST FROM <paramref name="origin"/> TO EACH OF
    /// <paramref name="targets"/>: the uncapped Dijkstra of <see cref="Isochrone"/> (budget MaxValue) over the same
    /// lattice steps and lanes, stopped as soon as every passable target is settled instead of flooding the whole
    /// component. Entry i is <c>Isochrone(origin, MaxValue)</c>'s cost at targets[i] BIT FOR BIT, and
    /// <see cref="double.PositiveInfinity"/> where that isochrone does not reach it (an impassable or unreachable
    /// target, or an impassable origin) — so a reader that wrote +∞ for an unreached node reads the same table:
    /// - the pops up to the stop are Isochrone's own, because the heap's (key, id) total order fixes them and nothing
    ///   after the stop can come before it;
    /// - a settled node's g never changes again (a relaxation never writes a closed node);
    /// - an impassable target is never awaited (it is never pushed, so never settled) and reads +∞; a passable target
    ///   in another component leaves the count above zero, so the search runs to exhaustion exactly as the full
    ///   isochrone did and returns +∞ for it.
    /// Duplicate targets are awaited once and each copy reads the same cost; a target equal to the origin costs 0.
    /// The early exit changes no result (the points above), so no result-level test can see it: it is a speed-up
    /// guarded only by <c>sim bench</c>, through the catchment phase's TIME (D-055; its allocation column does not move).
    /// Awaiting a duplicate once matters for speed alone too (awaited twice, the count never reaches zero and the search
    /// floods the component), and only where two settlements share an origin node, which D-025 spacing forbids.
    /// Pure, no state (the mandate above): every array is a local of this call. LegacyPathfinderDifferentialTests
    /// compares it with the batch-3 isochrone on tie-dense lattices; CatchmentSystem's pairwise distances use it, and
    /// LegacyCatchmentDistancesTests compares the table they write with the batch-3 one row for row.
    /// </summary>
    public static double[] DistancesTo(
        TraversalLattice lattice, IReadOnlyWorldState world, int origin, ReadOnlySpan<int> targets)
    {
        int n = lattice.NodeCount;
        for (int i = 0; i < targets.Length; i++)
            if ((uint)targets[i] >= (uint)n)
                throw new ArgumentOutOfRangeException(nameof(targets), targets[i],
                    "a target is not a node of the lattice");
        var result = new double[targets.Length];

        var isTarget = new bool[n];
        int remaining = 0;
        for (int i = 0; i < targets.Length; i++)
        {
            int t = targets[i];
            // A duplicate counts once; an impassable node is never pushed, so it is never settled and never awaited.
            if (isTarget[t] || !lattice.IsPassable(t)) continue;
            isTarget[t] = true;
            remaining++;
        }

        var g = new double[n];
        var closed = new bool[n];
        Array.Fill(g, double.MaxValue);
        if (remaining > 0 && lattice.IsPassable(origin))
        {
            LaneOverlay overlay = BuildOverlay(lattice, world);
            var open = new MinHeap(256);
            g[origin] = 0.0;
            open.Push(0.0, origin);
            while (open.Count > 0)
            {
                (_, int current) = open.Pop();
                if (closed[current]) continue;
                closed[current] = true;
                if (isTarget[current] && --remaining == 0) break; // every target settled: nothing later can change them

                Expand(lattice, overlay, current, g, null, closed, (node, tentative) => open.Push(tentative, node));
            }
        }

        for (int i = 0; i < targets.Length; i++)
            result[i] = closed[targets[i]] ? g[targets[i]] : double.PositiveInfinity;
        return result;
    }

    // --- shared expansion ------------------------------------------------------

    private static void Expand(
        TraversalLattice lattice, LaneOverlay overlay,
        int current, double[] g, int[]? cameFrom, bool[] closed, Action<int, double> push)
    {
        (int x, int y) = lattice.Coords(current);
        for (int d = 0; d < 8; d++)
        {
            int nx = MapTopology.NeighbourX(x, Dx[d], lattice.Size, lattice.WrapX), ny = y + Dy[d];
            if (nx < 0 || ny < 0 || ny >= lattice.Size) continue;
            int nb = ny * lattice.Size + nx;
            if (closed[nb] || !lattice.IsPassable(nb)) continue;
            // Batch 4 O6: the direction says whether the step is diagonal (d ≥ 4) — StepCost's own double, no Coords.
            Relax(current, nb, g[current] + lattice.StepCostKnown(current, nb, d >= 4), g, cameFrom, push);
        }

        int[] targets = overlay.Targets;
        double[] costs = overlay.Costs;
        for (int e = overlay.Offsets[current], end = overlay.Offsets[current + 1]; e < end; e++)
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

    /// <summary>
    /// The A* heuristic at node (<paramref name="x"/>, <paramref name="y"/>) toward the goal (<paramref name="gx"/>,
    /// <paramref name="gy"/>) on a <paramref name="size"/>-wide lattice: the straight-line distance in stride units —
    /// x the short way round when <paramref name="wrap"/> (WS3; flat: x − gx's magnitude) — times
    /// <paramref name="effMin"/>. Batch 4 (O4): computed from coordinates the caller already holds, with the
    /// branch-only <see cref="MapTopology.DxOnMap"/> (both columns are on the map), so it is the batch-3
    /// <c>Heuristic(lattice, node, …)</c>'s double bit for bit without its Coords division and modulo. Public so
    /// MapTopologyBranchDxTests compares it with the batch-3 formula exhaustively.
    /// </summary>
    public static double Heuristic(int x, int y, int gx, int gy, int size, bool wrap, double effMin)
    {
        double dx = MapTopology.DxOnMap(gx, x, size, wrap), dy = y - gy;
        return Math.Sqrt(dx * dx + dy * dy) * effMin;
    }

    /// <summary>
    /// Batch 4 (D-055, O3): the per-call network lane overlay in COMPRESSED SPARSE ROW form. Node v's lanes are
    /// entries <c>Offsets[v] .. Offsets[v + 1] − 1</c> of <see cref="Targets"/> (the lattice node the lane leads to)
    /// and <see cref="Costs"/> (its cost); <see cref="Offsets"/> has NodeCount + 1 entries. <see cref="MinEdgePerUnit"/>
    /// is the minimum lane cost per straight-line stride unit (double.MaxValue with no lane) — the heuristic's
    /// admissibility bound. A value of one call: built from the tables each query, never kept (the purity mandate).
    /// </summary>
    public readonly record struct LaneOverlay(int[] Offsets, int[] Targets, double[] Costs, double MinEdgePerUnit);

    /// <summary>
    /// Per-call network overlay (pure — rebuilt from the tables each query):
    /// adjacency per lattice node, both directions — first PathBuild's lattice
    /// fast lanes (NetworkEdges, row order), then (ADR-032) every travelled
    /// inter-city ROUTE in TransportEdges (row order) as a lane between its two
    /// settlements' origin lattice nodes, costed by the ONE road-performance
    /// definition: <see cref="RoadPerformance.TravelCostUnits"/> — the route's
    /// LengthKm × its stored effective (interpolated) cost factor, in cost units.
    /// A better or further-modernized road is a cheaper lane, so road class shapes
    /// catchments, pairwise travel costs and PathBuild routing; with no route rows
    /// the overlay is exactly the pre-ADR-032 one.
    /// Also returns the minimum lane cost per straight-line stride unit for the
    /// heuristic's admissibility bound (road lanes included).
    ///
    /// Batch 4 (D-055, O3): built in CSR form (<see cref="LaneOverlay"/>) — three flat arrays instead of a jagged
    /// pair of NodeCount arrays. The LANE ORDER of every node is the batch-3 order: lanes are appended in the global
    /// sequence (each network edge in row order, its A endpoint's entry then its B's; then each travelled road the
    /// same way), and a node's lanes keep that sequence. The fill needs no per-node cursor: after the counting pass
    /// the offsets hold each node's slice END, and the sequence is walked BACKWARDS, each entry taking its node's last
    /// free slot (<c>--Offsets[node]</c>) — so the last entry of a node lands last, and when the walk ends every
    /// offset has stepped down to its slice's start. MinPerUnit runs in the batch-3 sequence (the counting pass).
    /// Public so LegacyPathfinderDifferentialTests compares it node by node with the batch-3 jagged form.
    /// </summary>
    public static LaneOverlay BuildOverlay(TraversalLattice lattice, IReadOnlyWorldState world)
    {
        int n = lattice.NodeCount;
        int edgeCount = world.NetworkEdges.Count;

        // Resolve network node id → lattice anchor (id-indexed lookup array).
        var anchor = new int[world.NetworkNodes.Count == 0 ? 0 : MaxNodeId(world) + 1];
        for (int i = 0; i < world.NetworkNodes.Count; i++)
        {
            NetworkNodeRow row = world.NetworkNodes[i];
            anchor[row.Id.Value] = row.LatticeNode;
        }

        // ADR-032 road lanes: settlement endpoints → origin lattice nodes, and the cost.
        int roadCount = world.TransportEdges.Count;
        int[] roadA = roadCount == 0 ? [] : new int[roadCount];
        int[] roadB = roadCount == 0 ? [] : new int[roadCount];
        double[] roadCost = roadCount == 0 ? [] : new double[roadCount];
        if (roadCount > 0)
        {
            // W1 fix F9: the TERRAIN's size — the raster the site cells index. Size × Stride restated it, and on a flat
            // world the stride does not divide (250 px: 62 × 4 = 248) read every site as a 248-px cell, anchoring each
            // travelled road lane to the wrong lattice nodes (NonStrideLatticeGeometryTests). A terrain-less (toy)
            // world's lattice is its own raster.
            int terrainSize = world.Terrain?.Size ?? lattice.Size * lattice.Stride;
            for (int i = 0; i < roadCount; i++)
            {
                TransportEdgeRow r = world.TransportEdges[i];
                roadA[i] = -1;
                if (!RoadPerformance.IsTravelled(r)) continue;
                int sa = SiteOf(world, r.A), sb = SiteOf(world, r.B);
                if (sa < 0 || sb < 0) continue;
                int a = LatticeMap.OriginLatticeNode(lattice, terrainSize, sa);
                int b = LatticeMap.OriginLatticeNode(lattice, terrainSize, sb);
                if (a == b) continue;
                roadA[i] = a;
                roadB[i] = b;
                roadCost[i] = RoadPerformance.TravelCostUnits(r, LatticeGeometry.KmPerCostUnitOnIdealGround(lattice));
            }
        }

        // Counting pass (and the admissibility bound, in the batch-3 sequence): offsets[v] counts v's lanes.
        var offsets = new int[n + 1];
        int lanes = 0;
        double minEdgePerUnit = double.MaxValue;
        for (int i = 0; i < edgeCount; i++)
        {
            NetworkEdgeRow e = world.NetworkEdges[i];
            offsets[anchor[e.A.Value]]++;
            offsets[anchor[e.B.Value]]++;
            lanes += 2;
            MinPerUnit(lattice, anchor[e.A.Value], anchor[e.B.Value], e.Cost, ref minEdgePerUnit);
        }
        for (int i = 0; i < roadCount; i++)
        {
            if (roadA[i] < 0) continue;
            offsets[roadA[i]]++;
            offsets[roadB[i]]++;
            lanes += 2;
            MinPerUnit(lattice, roadA[i], roadB[i], roadCost[i], ref minEdgePerUnit);
        }

        // Inclusive prefix sums: offsets[v] = the END of v's slice; offsets[n] = the lane count.
        for (int v = 1; v < n; v++) offsets[v] += offsets[v - 1];
        offsets[n] = lanes;

        int[] targets = lanes == 0 ? [] : new int[lanes];
        double[] costs = lanes == 0 ? [] : new double[lanes];
        // The batch-3 sequence walked BACKWARDS (roads last to first, then network edges last to first; within one
        // lane its B entry before its A entry), each entry into its node's last free slot.
        for (int i = roadCount - 1; i >= 0; i--)
        {
            if (roadA[i] < 0) continue;
            int a = roadA[i], b = roadB[i];
            int k = --offsets[b];
            targets[k] = a; costs[k] = roadCost[i];
            k = --offsets[a];
            targets[k] = b; costs[k] = roadCost[i];
        }
        for (int i = edgeCount - 1; i >= 0; i--)
        {
            NetworkEdgeRow e = world.NetworkEdges[i];
            int a = anchor[e.A.Value], b = anchor[e.B.Value];
            int k = --offsets[b];
            targets[k] = a; costs[k] = e.Cost;
            k = --offsets[a];
            targets[k] = b; costs[k] = e.Cost;
        }
        return new LaneOverlay(offsets, targets, costs, minEdgePerUnit);
    }

    private static void MinPerUnit(TraversalLattice lattice, int a, int b, double cost, ref double min)
    {
        (int ax, int ay) = lattice.Coords(a);
        (int bx, int by) = lattice.Coords(b);
        // WS3: the endpoints' distance on the cylinder — consistent with Heuristic (flat: ax − bx's magnitude, as before).
        double dx = MapTopology.Dx(bx, ax, lattice.Size, lattice.WrapX), dy = ay - by;
        double dist = Math.Sqrt(dx * dx + dy * dy);
        if (dist > 0.0)
        {
            double perUnit = cost / dist;
            if (perUnit < min) min = perUnit;
        }
    }

    private static int SiteOf(IReadOnlyWorldState world, SettlementId id)
    {
        for (int i = 0; i < world.Settlements.Count; i++)
            if (world.Settlements[i].Id.Value == id.Value) return world.Settlements[i].SiteCell;
        return -1;
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
