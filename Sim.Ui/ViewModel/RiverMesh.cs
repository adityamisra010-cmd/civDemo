using Sim.Core.Worldgen;

namespace Sim.Ui.ViewModel;

/// <summary>
/// Builds world-space river geometry from TerrainSet's discharge-ranked
/// polylines (T1.2 data) — the T1.7 re-gate fix: rivers were baked into the
/// raster texture and staircased at zoom; as VECTOR quad-strips they stay
/// smooth at any zoom. PURE view-model (doubles, no MonoGame types, fully
/// testable headless); built once at load — the polylines are immutable
/// terrain data (ADR-008). The raster river layer in TerrainSet is untouched:
/// it is sim/fertility data and hash-bound; this is a render path only.
///
/// Geometry: each polyline segment becomes a quad (two triangles) around the
/// segment axis, extended by half a width at both ends so consecutive quads
/// overlap and cover the joints. Width falls linearly with discharge rank
/// (rank 0 = highest discharge = widest). Anti-aliasing is the renderer's job
/// (MSAA — see SimUiGame); the mesh is plain solid geometry.
/// </summary>
public static class RiverMesh
{
    /// <summary>TUNE: rank-0 river width, in world (terrain px) units.</summary>
    public const double MaxWidthWorldPx = 2.4;

    /// <summary>TUNE: minimum width for the lowest-discharge polyline.</summary>
    public const double MinWidthWorldPx = 0.9;

    public readonly record struct Vertex(double X, double Y);

    /// <summary>Width for a polyline at <paramref name="rank"/> of <paramref name="count"/> (linear falloff).</summary>
    public static double WidthForRank(int rank, int count)
    {
        if (count <= 1) return MaxWidthWorldPx;
        double t = rank / (double)(count - 1);
        // Endpoint-exact lerp form: t=0 → Max exactly, t=1 → Min exactly.
        return MaxWidthWorldPx * (1.0 - t) + MinWidthWorldPx * t;
    }

    /// <summary>World-space center of a terrain cell index.</summary>
    public static Vertex CellCenter(int cell, int terrainSize) =>
        new(cell % terrainSize + 0.5, cell / terrainSize + 0.5);

    /// <summary>
    /// Triangle-list vertices (three per triangle, six per segment quad) for
    /// every river polyline, in discharge-rank order. Deterministic: pure
    /// function of the terrain's polyline data.
    /// </summary>
    public static Vertex[] Build(TerrainSet terrain)
    {
        var vertices = new List<Vertex>();
        int count = terrain.RiverPolylineCount;
        for (int rank = 0; rank < count; rank++)
        {
            ReadOnlySpan<int> line = terrain.RiverPolyline(rank);
            double halfWidth = WidthForRank(rank, count) / 2.0;
            for (int i = 0; i + 1 < line.Length; i++)
            {
                Vertex a = CellCenter(line[i], terrain.Size);
                Vertex b = CellCenter(line[i + 1], terrain.Size);
                AppendSegmentQuad(vertices, a, b, halfWidth);
            }
        }
        return [.. vertices];
    }

    private static void AppendSegmentQuad(List<Vertex> vertices, Vertex a, Vertex b, double halfWidth)
    {
        double dx = b.X - a.X, dy = b.Y - a.Y;
        double length = Math.Sqrt(dx * dx + dy * dy);
        if (length <= 0.0) return;
        double ux = dx / length, uy = dy / length; // unit axis
        double px = -uy, py = ux;                  // unit perpendicular

        // Extend both ends by halfWidth: consecutive quads overlap → joints covered.
        double ax = a.X - ux * halfWidth, ay = a.Y - uy * halfWidth;
        double bx = b.X + ux * halfWidth, by = b.Y + uy * halfWidth;

        var a1 = new Vertex(ax + px * halfWidth, ay + py * halfWidth);
        var a2 = new Vertex(ax - px * halfWidth, ay - py * halfWidth);
        var b1 = new Vertex(bx + px * halfWidth, by + py * halfWidth);
        var b2 = new Vertex(bx - px * halfWidth, by - py * halfWidth);

        vertices.Add(a1); vertices.Add(b1); vertices.Add(a2); // triangle 1
        vertices.Add(a2); vertices.Add(b1); vertices.Add(b2); // triangle 2
    }
}
