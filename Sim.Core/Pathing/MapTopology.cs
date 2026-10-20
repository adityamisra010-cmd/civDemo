namespace Sim.Core.Pathing;

/// <summary>
/// THE EAST-WEST TOPOLOGY CHOKEPOINT (batch 3, WS3 — the Director's 2026-10-14 instruction: "genuine east-west
/// cylindrical world topology, not merely visual camera wrapping"). The world is a CYLINDER when
/// <see cref="Worldgen.TerrainSet.WrapX"/> is true: x is periodic with period Size (moving east past the eastern
/// edge returns to the western edge, and the reverse), and y keeps its hard edges (the poles — vertical boundaries
/// are preserved, Director requirement 7). When it is false the world is the flat square raster it always was, and
/// every function here returns exactly the flat answer the restated arithmetic it replaces returned.
///
/// EVERY x delta, x neighbour and x normalisation between two positions on the map goes through this file (or through
/// <see cref="TraversalLattice"/>, which copies the flag at Build, or through <see cref="Worldgen.Worldgen"/>, which
/// generates the terrain before any TerrainSet exists). A source guard (MapTopologyGuardTests) fails the build of a
/// test run when a Sim.Core or Sim.Ui file outside the allowlist computes an x delta from raw cell arithmetic, so a
/// future movement system (M7, order kind 10 reserved) cannot bypass the topology.
///
/// Integer first: every sim distance is an exact integer (squared pixels, lattice steps) so ordering keys stay
/// tie-exact; the double helpers <see cref="WrapCoord"/> and <see cref="NearestCopy"/> are for continuous
/// positions (a formation's X, the camera).
///
/// A WRAPPED AXIS NEEDS AT LEAST 3 COLUMNS: on 1 or 2 columns an x neighbour is the cell itself or the same column on
/// both sides (self-loops, duplicate neighbours). <see cref="RequireWrapSize"/> refuses that; Worldgen.Generate
/// refuses a wrapped world whose lattice would be narrower than 3 nodes.
/// </summary>
public static class MapTopology
{
    /// <summary>The smallest axis that may wrap (see the class remarks).</summary>
    public const int MinWrapSize = 3;

    /// <summary>Throws when <paramref name="wrap"/> is set on an axis narrower than <see cref="MinWrapSize"/>.</summary>
    public static void RequireWrapSize(int size, bool wrap)
    {
        if (wrap && size < MinWrapSize)
            throw new ArgumentOutOfRangeException(nameof(size), size,
                "an east-west wrapped axis needs at least " + MinWrapSize.ToString(System.Globalization.CultureInfo.InvariantCulture)
                + " columns (a narrower one makes a column its own neighbour).");
    }

    /// <summary>
    /// The column <paramref name="dx"/> steps east of <paramref name="x"/> (negative = west): x + dx when that is on the
    /// map; on a wrapped axis the column it lands on modulo <paramref name="size"/>; on a flat axis −1 (off the map).
    /// <paramref name="x"/> must be a column in [0, size).
    /// </summary>
    public static int NeighbourX(int x, int dx, int size, bool wrap)
    {
        int nx = x + dx;
        if ((uint)nx < (uint)size) return nx;
        if (!wrap) return -1;
        nx %= size;
        return nx < 0 ? nx + size : nx;
    }

    /// <summary>Integer column normalisation: <paramref name="x"/> modulo <paramref name="size"/>, in [0, size).</summary>
    public static int WrapX(int x, int size)
    {
        int r = x % size;
        return r < 0 ? r + size : r;
    }

    /// <summary>As <see cref="WrapX(int, int)"/> for a column held in a long (a floored continuous position).</summary>
    public static int WrapX(long x, int size)
    {
        long r = x % size;
        return (int)(r < 0 ? r + size : r);
    }

    /// <summary>
    /// THE SIGNED x DELTA from column <paramref name="x1"/> to column <paramref name="x2"/>. Flat: x2 − x1. Wrapped: the
    /// shortest way round, d = ((x2 − x1) mod size); if d &gt; size / 2 (INTEGER division) then d −= size — so d lies in
    /// (−size/2, size/2] on an even size and in [−(size−1)/2, (size−1)/2] on an odd one.
    ///
    /// THE TIE. On an even size, two columns exactly half the world apart are equally far both ways; the delta resolves
    /// to +size/2 (east) from BOTH ends, so at the tie Dx(a, b) = Dx(b, a) = +size/2 and Dx(a, b) ≠ −Dx(b, a). Every
    /// distance built from it uses |d| (or d²), which is symmetric; a caller that needs a direction gets east at the tie.
    /// </summary>
    public static int Dx(int x1, int x2, int size, bool wrap)
    {
        int d = x2 - x1;
        if (!wrap) return d;
        d %= size;
        if (d < 0) d += size;
        if (d > size / 2) d -= size;
        return d;
    }

    /// <summary>
    /// Batch 4 (D-055, O4): <see cref="Dx"/> for two columns ALREADY ON THE MAP — <paramref name="x1"/> and
    /// <paramref name="x2"/> in [0, <paramref name="size"/>) — with branches only, no modulo. For such columns
    /// x2 − x1 lies in (−size, size), where Dx's <c>d %= size</c> is the identity, so the two remaining steps are Dx's
    /// own: + size when negative, then − size when past size / 2 (INTEGER division — the +size/2 east tie of an even
    /// size included). It equals Dx exactly on that domain (MapTopologyBranchDxTests: every (x1, x2) at sizes 2..12, 63,
    /// 64 and 256, both flags); outside it the caller must use Dx. The A* heuristic calls it once per push.
    /// </summary>
    public static int DxOnMap(int x1, int x2, int size, bool wrap)
    {
        int d = x2 - x1;
        if (!wrap) return d;
        if (d < 0) d += size;
        if (d > size / 2) d -= size;
        return d;
    }

    /// <summary>|<see cref="Dx"/>| — the x distance in columns, the short way round when wrapped.</summary>
    public static int AbsDx(int x1, int x2, int size, bool wrap)
    {
        int d = Dx(x1, x2, size, wrap);
        return d < 0 ? -d : d;
    }

    /// <summary>
    /// The exact squared pixel distance between two row-major cells of a Size×Size raster — integer, so a key built on it
    /// is tie-exact. Wrapped: the x term is the short way round (<see cref="Dx"/>); y is never wrapped.
    /// </summary>
    public static long SquaredPx(int cellA, int cellB, int size, bool wrap)
    {
        long dx = Dx(cellA % size, cellB % size, size, wrap);
        long dy = cellA / size - cellB / size;
        return dx * dx + dy * dy;
    }

    /// <summary>The straight-line km between two row-major cells (<see cref="SquaredPx"/> × km per pixel) — on the
    /// cylinder when wrapped, "as the crow flies".</summary>
    public static double Km(int cellA, int cellB, int size, double kmPerPx, bool wrap) =>
        Math.Sqrt(SquaredPx(cellA, cellB, size, wrap)) * kmPerPx;

    /// <summary>
    /// A continuous x normalised into [0, <paramref name="size"/>): x − floor(x / size)·size, with the explicit
    /// <c>if (r &gt;= size) r -= size</c> a tiny negative x needs (−1e-17 + size rounds to size itself). A NaN or infinite x
    /// is returned unchanged — normalising it would invent a position.
    /// </summary>
    public static double WrapCoord(double x, double size)
    {
        if (!double.IsFinite(x) || !(size > 0.0)) return x;
        if (x >= 0.0 && x < size) return x;
        double r = x - Math.Floor(x / size) * size;
        if (r >= size) r -= size;
        if (r < 0.0) r = 0.0;
        return r;
    }

    /// <summary>
    /// The copy of <paramref name="x"/> (x + k·size, k an integer) nearest to <paramref name="refX"/> — so the shortest
    /// wrapped path from refX to x is the straight segment refX → result. At an exact half-world tie the eastern copy wins
    /// (the <see cref="Dx"/> convention).
    /// </summary>
    public static double NearestCopy(double x, double refX, double size)
    {
        if (!double.IsFinite(x) || !double.IsFinite(refX) || !(size > 0.0)) return x;
        double d = x - refX;
        double k = Math.Floor(d / size + 0.5);
        double r = d - k * size;
        if (r <= -size / 2.0) r += size;
        else if (r > size / 2.0) r -= size;
        return refX + r;
    }

    /// <summary>
    /// A SEAM-CROSSING PRIMITIVE UNWRAPPED (batch 3 WS3, the rendering stage): <paramref name="x"/> itself when it is already
    /// the copy nearest <paramref name="refX"/> (x − refX in (−size/2, size/2], <see cref="NearestCopy"/>'s convention), else
    /// that nearest copy — so a segment, a polyline step or a route drawn from refX to the result goes the short way round,
    /// and a primitive that does not cross the seam keeps its coordinates BIT FOR BIT (no arithmetic touches them).
    /// </summary>
    public static double UnwrapNear(double x, double refX, double size)
    {
        if (!double.IsFinite(x) || !double.IsFinite(refX) || !(size > 0.0)) return x;
        double d = x - refX;
        if (d > -size / 2.0 && d <= size / 2.0) return x;
        return NearestCopy(x, refX, size);
    }

    /// <summary>The block width in terrain px of a lattice of <paramref name="latticeSize"/> nodes per side over a
    /// <paramref name="terrainSize"/>-px terrain (the trailing columns of a size the lattice does not divide belong to the
    /// last node) — <see cref="LatticeGrid"/>'s second half for a lattice already built.</summary>
    public static int BlockPx(int terrainSize, int latticeSize) => Math.Max(1, terrainSize / Math.Max(1, latticeSize));

    /// <summary>The terrain cell at the CENTRE of lattice node <paramref name="node"/>'s block (block x·b + b/2, y·b + b/2),
    /// kept on the map — the cell a node's values are sampled at (the resource overlay).</summary>
    public static int NodeCentreCell(int node, int terrainSize, int latticeSize, int blockPx)
    {
        int x = Math.Min(terrainSize - 1, node % latticeSize * blockPx + blockPx / 2);
        int y = Math.Min(terrainSize - 1, node / latticeSize * blockPx + blockPx / 2);
        return y * terrainSize + x;
    }

    /// <summary>
    /// The lattice geometry of a terrain of <paramref name="terrainSize"/> px at <paramref name="stride"/>: nodes per side
    /// (floored — the trailing columns of a size the stride does not divide belong to the last node) and the block width
    /// in terrain px. The ONE statement of it every reader of the lattice's geometry shares.
    /// </summary>
    public static (int LatticeSize, int BlockPx) LatticeGrid(int terrainSize, int stride)
    {
        int lsize = Math.Max(1, terrainSize / Math.Max(1, stride));
        return (lsize, Math.Max(1, terrainSize / lsize));
    }

    /// <summary>
    /// The lattice node whose block contains terrain cell <paramref name="cell"/> of a <paramref name="terrainSize"/>-px
    /// raster, on a lattice of <paramref name="latticeSize"/> nodes per side with blocks of <paramref name="blockPx"/> px:
    /// cells past the last whole block belong to the last node (the clamp every restatement used). The cell must be on
    /// the map; a cell index is already a normalised position, so wrap changes nothing here.
    /// </summary>
    public static int NodeOfCell(int cell, int terrainSize, int latticeSize, int blockPx)
    {
        int x = Math.Min(cell % terrainSize / blockPx, latticeSize - 1);
        int y = Math.Min(cell / terrainSize / blockPx, latticeSize - 1);
        return y * latticeSize + x;
    }
}
