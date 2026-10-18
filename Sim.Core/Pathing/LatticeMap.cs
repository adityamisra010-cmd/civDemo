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
    /// the nearest passable node by growing Chebyshev rings: the composite key
    /// (ring radius ASC, node id ASC), a total order. Public and pure so tests can
    /// reproduce the mapping exactly.
    ///
    /// WS3 (batch 3): the key is now applied EXPLICITLY. On a flat lattice the
    /// ring scan (dy ascending, then dx ascending) visits a ring's nodes in
    /// ascending id, so the first passable node found IS the minimum id — the
    /// pre-batch-3 early return, kept. On a wrapped lattice the ring is measured
    /// with the WRAPPED Chebyshev radius max(|Dx|, |dy|) (<see cref="MapTopology.Dx"/>),
    /// each column appears once (at its shortest offset), and because a wrapped
    /// column x = Size − 1 can sit in the same ring as x = 0 the scan order is no
    /// longer the id order — so the ring's passable nodes are compared by id.
    /// </summary>
    /// <remarks>W1 fix F9: <paramref name="terrainSize"/> is the size of the TERRAIN the lattice was built from (its
    /// cells are what <paramref name="siteCell"/> indexes) — never a restatement such as Size × Stride, which on a size
    /// the stride does not divide is a different raster. The block size is <see cref="MapTopology.LatticeGrid"/>'s, and
    /// a terrain size that grid does not map onto this lattice is refused rather than read as another raster.</remarks>
    public static int OriginLatticeNode(TraversalLattice lattice, int terrainSize, int siteCell)
    {
        (int latticeSize, int block) = MapTopology.LatticeGrid(terrainSize, lattice.Stride);
        if (latticeSize != lattice.Size)
            throw new ArgumentException(
                "terrain size " + terrainSize.ToString(System.Globalization.CultureInfo.InvariantCulture)
                + " px is not the terrain this lattice was built from (" + lattice.Size.ToString(System.Globalization.CultureInfo.InvariantCulture)
                + " nodes at stride " + lattice.Stride.ToString(System.Globalization.CultureInfo.InvariantCulture) + ")",
                nameof(terrainSize));
        int origin = MapTopology.NodeOfCell(siteCell, terrainSize, lattice.Size, block);
        if (lattice.IsPassable(origin)) return origin;
        (int cx, int cy) = lattice.Coords(origin);
        bool wrap = lattice.WrapX;

        for (int r = 1; r < lattice.Size; r++)
        {
            int best = -1;
            for (int dy = -r; dy <= r; dy++)
            {
                int y = cy + dy;
                if (y < 0 || y >= lattice.Size) continue;
                for (int dx = -r; dx <= r; dx++)
                {
                    if (Math.Max(Math.Abs(dx), Math.Abs(dy)) != r) continue; // ring only
                    int x = MapTopology.NeighbourX(cx, dx, lattice.Size, wrap);
                    if (x < 0) continue;
                    // Wrapped: a column reached by an offset that is not its shortest one belongs to a nearer
                    // ring (or is a duplicate of a column already scanned here) — skip it.
                    if (wrap && MapTopology.Dx(cx, x, lattice.Size, wrap) != dx) continue;
                    int node = lattice.NodeId(x, y);
                    if (!lattice.IsPassable(node)) continue;
                    if (!wrap) return node;               // flat: scan order is id order
                    if (best < 0 || node < best) best = node;
                }
            }
            if (best >= 0) return best;
        }
        throw new InvalidOperationException(
            "no passable lattice node exists — terrain is all water at lattice scale.");
    }

    /// <summary>
    /// MEAN fertility over the node's stride×stride terrain block — the same
    /// block averaging as TraversalLattice.Build uses for movement cost, so a
    /// node's suitability is representative at the lattice's own resolution.
    /// Public and pure so tests can recompute the aggregate independently.
    ///
    /// DIMENSIONLESS (T3.2b): an agronomic-suitability index in [0,1], NOT an
    /// area. Summing this over nodes counts fertility-weighted NODES — the
    /// CR-002 denomination bug. To get land, convert through
    /// <see cref="LatticeGeometry.ArableKm2"/>, or call
    /// <see cref="BlockArableKm2"/>. The name says "mean" so that a future
    /// caller cannot accumulate it into an area by accident.
    /// </summary>
    public static double BlockMeanFertility(TerrainSet terrain, TraversalLattice lattice, int node)
    {
        int stride = MapTopology.LatticeGrid(terrain.Size, lattice.Stride).BlockPx;   // W1 fix F9: the one grid
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

    /// <summary>
    /// The node's arable land in FERTILITY-WEIGHTED km² — mean suitability ×
    /// block area, through the single conversion in <see cref="LatticeGeometry"/>.
    /// This is what a catchment sums (T3.2b); the yield constant is denominated
    /// against it.
    /// </summary>
    public static double BlockArableKm2(TerrainSet terrain, TraversalLattice lattice, int node) =>
        LatticeGeometry.ArableKm2(BlockMeanFertility(terrain, lattice, node), lattice);
}
