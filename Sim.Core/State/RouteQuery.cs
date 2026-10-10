using Sim.Core.Pathing;

namespace Sim.Core.State;

/// <summary>
/// M5 S1a — THE READ-ONLY ROUTE QUERY: one route between two lattice nodes, with the
/// travel cost from the origin to EVERY node along it, and the part of it a cost budget
/// reaches. It is the seam a "found settlement" preview (route and cost from the source to
/// a candidate site) and any later movement preview share, so the route a player is shown
/// is the route the pathfinder takes.
///
/// NOT A SECOND MOVEMENT MODEL. The route is <see cref="Pathfinder.FindPath"/>'s, unchanged:
/// the same traversal lattice, the same network overlay (PathBuild fast lanes and ADR-032 road
/// routes), the same A* with its (f, node id) composite heap key — so among equal-cost routes
/// the one returned is the one FindPath returns, decided by that key and never by insertion
/// order. The per-node costs are <see cref="Pathfinder.LegCosts"/> summed from zero: each leg
/// is a <see cref="TraversalLattice.StepCost"/> or a lane cost, the edge set FindPath expands.
/// The final cumulative cost is checked here against FindPath's TotalCost with EXACT equality,
/// and a mismatch throws: the two derivations are the same arithmetic, so a difference would be
/// a defect, never rounding to tolerate.
///
/// THE BUDGET PREFIX (<see cref="Route.ReachablePrefixLength"/>) is inclusive, as
/// <see cref="Pathfinder.Isochrone"/>'s is: a node whose cumulative cost EQUALS the budget is
/// reached. The prefix stops at the first node over budget.
///
/// Pure function of (lattice, world, from, to): no cache, no state, no RNG, no writes.
/// </summary>
public static class RouteQuery
{
    /// <summary>A route, or its absence. <see cref="Nodes"/> runs origin → destination
    /// inclusive and <see cref="Cumulative"/> is parallel to it (Cumulative[0] = 0).
    /// Both are empty when no route exists.</summary>
    public sealed class Route
    {
        public required bool Found { get; init; }
        public required int[] Nodes { get; init; }
        public required double[] Cumulative { get; init; }

        /// <summary>The cost of the whole route — FindPath's TotalCost, bit for bit; 0 when
        /// no route exists (FindPath's convention).</summary>
        public double TotalCost => Cumulative.Length == 0 ? 0.0 : Cumulative[^1];

        /// <summary>How many leading nodes of the route lie within <paramref name="budget"/>
        /// (cumulative cost ≤ budget). The origin costs 0, so any budget ≥ 0 reaches at least
        /// it; a negative or NaN budget reaches nothing. The prefix is Nodes[..length].</summary>
        public int ReachablePrefixLength(double budget)
        {
            int k = 0;
            while (k < Cumulative.Length && Cumulative[k] <= budget) k++;
            return k;
        }
    }

    /// <summary>The route from <paramref name="fromNode"/> to <paramref name="toNode"/> on the
    /// lattice + network overlay of <paramref name="world"/>, with per-node cumulative cost.</summary>
    public static Route Find(TraversalLattice lattice, IReadOnlyWorldState world, int fromNode, int toNode)
    {
        Pathfinder.PathResult path = Pathfinder.FindPath(lattice, world, fromNode, toNode);
        if (!path.Found) return new Route { Found = false, Nodes = [], Cumulative = [] };

        double[] legs = Pathfinder.LegCosts(lattice, world, path.Nodes);
        var cumulative = new double[path.Nodes.Length];
        for (int k = 1; k < cumulative.Length; k++) cumulative[k] = cumulative[k - 1] + legs[k - 1];

        // Exact, never an epsilon: the legs are the costs A* added, in the order it added them.
        if (cumulative[^1] != path.TotalCost)
            throw new InvalidOperationException(
                $"route {fromNode}->{toNode}: summed leg cost {cumulative[^1].ToString("R", System.Globalization.CultureInfo.InvariantCulture)} "
                + $"!= FindPath TotalCost {path.TotalCost.ToString("R", System.Globalization.CultureInfo.InvariantCulture)}");
        return new Route { Found = true, Nodes = path.Nodes, Cumulative = cumulative };
    }
}
