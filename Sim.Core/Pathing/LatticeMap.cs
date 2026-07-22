using Sim.Core.Worldgen;

namespace Sim.Core.Pathing;

/// <summary>
/// Pure terrain↔lattice mapping helpers (extracted from CatchmentSystem at
/// T1.6 so that PathBuild can share them without a system-to-system reference
/// — law 6). Stateless, deterministic, no caches (T1.3 purity mandate).
/// </summary>
public static class LatticeMap
{
    /// <summary>
    /// The lattice node whose stride-block contains the site cell — or, when that
    /// block averaged out impassable (a shoreline site in a majority-water block),
    /// the nearest passable node by growing Chebyshev rings scanned in fixed
    /// row-major order: the composite key (ring radius ASC, node id ASC), a total
    /// order. Public and pure so tests can reproduce the mapping exactly.
    /// </summary>
    public static int OriginLatticeNode(TraversalLattice lattice, int terrainSize, int siteCell)
    {
        int stride = terrainSize / lattice.Size;
        int cx = Math.Min(siteCell % terrainSize / stride, lattice.Size - 1);
        int cy = Math.Min(siteCell / terrainSize / stride, lattice.Size - 1);
        if (lattice.IsPassable(lattice.NodeId(cx, cy))) return lattice.NodeId(cx, cy);

        for (int r = 1; r < lattice.Size; r++)
        {
            for (int dy = -r; dy <= r; dy++)
            {
                int y = cy + dy;
                if (y < 0 || y >= lattice.Size) continue;
                for (int dx = -r; dx <= r; dx++)
                {
                    if (Math.Max(Math.Abs(dx), Math.Abs(dy)) != r) continue; // ring only
                    int x = cx + dx;
                    if (x < 0 || x >= lattice.Size) continue;
                    int node = lattice.NodeId(x, y);
                    if (lattice.IsPassable(node)) return node;
                }
            }
        }
        throw new InvalidOperationException(
            "no passable lattice node exists — terrain is all water at lattice scale.");
    }

    /// <summary>
    /// Mean fertility over the node's stride×stride terrain block — the same
    /// block averaging as TraversalLattice.Build uses for movement cost, so a
    /// node's farmland is representative at the lattice's own resolution.
    /// Public and pure so tests can recompute the aggregate independently.
    /// </summary>
    public static double BlockFertility(TerrainSet terrain, TraversalLattice lattice, int node)
    {
        int stride = terrain.Size / lattice.Size;
        (int x, int y) = lattice.Coords(node);
        ReadOnlySpan<double> fertility = terrain.Fertility;

        double sum = 0.0;
        for (int by = 0; by < stride; by++)
        {
            int row = (y * stride + by) * terrain.Size + x * stride;
            for (int bx = 0; bx < stride; bx++) sum += fertility[row + bx];
        }
        return sum / (stride * stride);
    }
}
