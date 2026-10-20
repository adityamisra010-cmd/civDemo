// ======================================================================================================================
// LEGACY ORACLE — the batch-3 PathBuildSystem.ChooseTarget (both overloads) and PassableComponent, copied VERBATIM
// (batch 4, D-055, T5) from the base-b3 Sim.Core/Systems/PathBuild/PathBuildSystem.cs (sha256
// e8aae031b633c658e6dea10a1f21fb055075f875fab39128eed50eb8e1271c22, the three members exactly as they stood, their
// doc comments included). The batch-4 PathBuildSystem.Targets (O1 fertility once per step, O2 component reuse) must
// equal the per-settlement composition of these, entry for entry (LegacyPathBuildTargetsTests).
// NEVER EDIT THE BODY. The only departures: this comment block, the usings, the namespace and the enclosing class.
// ======================================================================================================================
using Sim.Core.Pathing;
using Sim.Core.Worldgen;

namespace Sim.Tests.Pathing.Legacy;

public static class LegacyPathBuild
{
    /// <summary>
    /// The build target: best passable lattice node NOT in the settlement's
    /// catchment and inside the frontier's component, by the composite key
    /// (block fertility DESC, node id ASC) — an ascending-id scan with
    /// strictly-greater comparison implements exactly that total order
    /// (constitution rule; tie-dense test shipped). Returns −1 when every
    /// eligible node is already reached.
    /// Public and pure so the tie-dense test can drive it directly.
    /// </summary>
    public static int ChooseTarget(
        TraversalLattice lattice, TerrainSet terrain, bool[] excluded, bool[] eligible)
    {
        var nodeFertility = new double[lattice.NodeCount];
        for (int node = 0; node < lattice.NodeCount; node++)
            nodeFertility[node] = LatticeMap.BlockMeanFertility(terrain, lattice, node);
        return ChooseTarget(lattice, nodeFertility, excluded, eligible);
    }

    /// <summary>Primitive overload (the tie-dense test surface): fertility per lattice node.</summary>
    public static int ChooseTarget(
        TraversalLattice lattice, ReadOnlySpan<double> nodeFertility, bool[] excluded, bool[] eligible)
    {
        int best = -1;
        double bestFertility = double.NegativeInfinity;
        for (int node = 0; node < lattice.NodeCount; node++)
        {
            if (excluded[node] || !eligible[node] || !lattice.IsPassable(node)) continue;
            // Strictly-greater on an ascending-id scan = (fertility DESC, id ASC).
            if (nodeFertility[node] > bestFertility)
            {
                bestFertility = nodeFertility[node];
                best = node;
            }
        }
        return best;
    }

    /// <summary>
    /// Passable 8-neighbor flood from <paramref name="origin"/> — fixed
    /// expansion order (ascending queue, W/E/N/S then diagonals), deterministic
    /// by construction. Pure; recomputed per build call (not state, not a cache).
    /// WS3: x neighbours wrap on a wrapped lattice (<see cref="MapTopology.NeighbourX"/>),
    /// so the component reaches across the east-west seam exactly as Expand does.
    /// </summary>
    public static bool[] PassableComponent(TraversalLattice lattice, int origin)
    {
        var inComponent = new bool[lattice.NodeCount];
        if (!lattice.IsPassable(origin)) return inComponent;
        var queue = new int[lattice.NodeCount];
        int head = 0, tail = 0;
        inComponent[origin] = true;
        queue[tail++] = origin;
        Span<int> dx = [-1, 1, 0, 0, -1, 1, -1, 1];
        Span<int> dy = [0, 0, -1, 1, -1, -1, 1, 1];
        while (head < tail)
        {
            int node = queue[head++];
            (int x, int y) = lattice.Coords(node);
            for (int d = 0; d < 8; d++)
            {
                int nx = MapTopology.NeighbourX(x, dx[d], lattice.Size, lattice.WrapX), ny = y + dy[d];
                if (nx < 0 || ny < 0 || ny >= lattice.Size) continue;
                int nb = lattice.NodeId(nx, ny);
                if (inComponent[nb] || !lattice.IsPassable(nb)) continue;
                inComponent[nb] = true;
                queue[tail++] = nb;
            }
        }
        return inComponent;
    }
}
