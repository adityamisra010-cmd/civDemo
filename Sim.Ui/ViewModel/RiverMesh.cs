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

    // Alias kept for the T1.7 test surface; geometry shared via LineGeometry (T1.8).
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
        var vertices = new List<LineGeometry.Vertex>();
        int count = terrain.RiverPolylineCount;
        for (int rank = 0; rank < count; rank++)
        {
            ReadOnlySpan<int> line = terrain.RiverPolyline(rank);
            double halfWidth = WidthForRank(rank, count) / 2.0;
            for (int i = 0; i + 1 < line.Length; i++)
            {
                Vertex a = CellCenter(line[i], terrain.Size);
                Vertex b = CellCenter(line[i + 1], terrain.Size);
                LineGeometry.AppendSegmentQuad(vertices,
                    new LineGeometry.Vertex(a.X, a.Y), new LineGeometry.Vertex(b.X, b.Y), halfWidth);
            }
        }
        var result = new Vertex[vertices.Count];
        for (int i = 0; i < vertices.Count; i++) result[i] = new Vertex(vertices[i].X, vertices[i].Y);
        return result;
    }
}
