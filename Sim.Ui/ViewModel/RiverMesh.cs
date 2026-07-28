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

    /// <summary>
    /// D-A3 (docs/art-gate-defects.md): rivers were the one screen element
    /// whose size scaled with zoom — a 2.4 world-px river drew ~76 screen px
    /// at 32×, while markers and labels hold constant screen size, and the
    /// style bible §2 calls rivers "ink-blue hairlines": inked cartography
    /// does not thicken when you lean toward the page.
    ///
    /// Chosen response, option (iii) of the directed packet: screen width =
    /// clamp(worldWidth × zoom, MIN, MAX). It scales in the middle band — so
    /// approaching the map still reads as approach — and is flat at both
    /// ends: discharge RANK stays legible zoomed out (no river drops below a
    /// visible hairline) and no river becomes a ribbon zoomed in.
    ///
    /// TUNE, display-only. MIN 1.0 px: the hairline visibility floor — one
    /// device pixel; below it a river disappears, which un-ranks the map.
    /// MAX 6.0 px = 2.5 × the rank-0 design width (2.4): the ink-weight
    /// ceiling past which a line stops reading as a line; 6.0/1.0 exceeds
    /// the world ratio 2.4/0.9, so the full rank spread stays expressible
    /// between the clamps.
    /// </summary>
    public const double MinScreenWidthPx = 1.0;
    public const double MaxScreenWidthPx = 6.0;

    /// <summary>Screen-space width for a rank at a zoom — the D-A3 response
    /// curve. Weakly monotone in rank at EVERY zoom by construction (clamp
    /// and WidthForRank are both monotone), pinned by a property test.</summary>
    public static double ScreenWidthForRank(int rank, int count, double zoom)
    {
        double w = WidthForRank(rank, count) * zoom;
        return w < MinScreenWidthPx ? MinScreenWidthPx
             : w > MaxScreenWidthPx ? MaxScreenWidthPx : w;
    }

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
    public static Vertex[] Build(TerrainSet terrain) => Build(terrain, 1.0);

    /// <summary>
    /// D-A3 overload: geometry stays in WORLD units so the existing world
    /// transform draws it unchanged, but the half-width is the CLAMPED screen
    /// width divided back by zoom — after the transform every river lands at
    /// exactly ScreenWidthForRank px on screen. In the unclamped middle band
    /// this is bit-identical to the original world-width mesh; only clamped
    /// ranks deviate. The caller rebuilds when zoom changes (zoom moves on
    /// wheel events, not per frame).
    /// </summary>
    public static Vertex[] Build(TerrainSet terrain, double zoom)
    {
        var vertices = new List<LineGeometry.Vertex>();
        int count = terrain.RiverPolylineCount;
        for (int rank = 0; rank < count; rank++)
        {
            ReadOnlySpan<int> line = terrain.RiverPolyline(rank);
            double halfWidth = ScreenWidthForRank(rank, count, zoom) / zoom / 2.0;
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
