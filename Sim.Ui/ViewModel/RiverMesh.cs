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
///
/// WS3 (batch 3): on a cylinder (<see cref="TerrainSet.WrapX"/>) a polyline may step across the seam (cell x = size − 1
/// to x = 0). Each segment is built from a to the copy of b NEAREST a (<see cref="Sim.Core.Pathing.MapTopology.UnwrapNear"/>
/// — b itself for every segment that does not cross, bit for bit), so a seam-crossing river is a short quad past the
/// world's edge, never one map-wide; the host draws the buffer at every visible copy of the world, which carries it on.
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

    /// <summary>WS3: <paramref name="b"/> at its copy nearest <paramref name="a"/> on a wrapped terrain (a segment's far end
    /// unwrapped relative to its first point); <paramref name="b"/> itself on a flat one.</summary>
    public static Vertex Unwrapped(Vertex b, Vertex a, TerrainSet terrain) =>
        terrain.WrapX ? b with { X = Sim.Core.Pathing.MapTopology.UnwrapNear(b.X, a.X, terrain.Size) } : b;

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
    /// ranks deviate. P2: the host no longer rebuilds this per zoom change (an eased zoom changes every frame); it
    /// builds <see cref="BuildBand"/> once per zoom band (<see cref="RiverBandCache"/>).
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
                Vertex b = Unwrapped(CellCenter(line[i + 1], terrain.Size), a, terrain);
                LineGeometry.AppendSegmentQuad(vertices,
                    new LineGeometry.Vertex(a.X, a.Y), new LineGeometry.Vertex(b.X, b.Y), halfWidth);
            }
        }
        var result = new Vertex[vertices.Count];
        for (int i = 0; i < vertices.Count; i++) result[i] = new Vertex(vertices[i].X, vertices[i].Y);
        return result;
    }

    // ------------------------------------------------------------------ P2: zoom bands (no rebuild per frame)

    /// <summary>P2: zoom bands per octave — the mesh is rebuilt when the zoom crosses a 2^(1/4) step, never per frame of
    /// an eased zoom (the D-A3 rebuild-on-every-zoom-change held only while the zoom moved per wheel event).</summary>
    public const int BandsPerOctave = 4;

    /// <summary>The band holding <paramref name="zoom"/>: ⌊4 log2 zoom⌋ (band b spans 2^(b/4) … 2^((b+1)/4)).</summary>
    public static int BandOf(double zoom) => (int)Math.Floor(BandsPerOctave * Math.Log2(Math.Max(1e-9, zoom)));

    /// <summary>The zoom range of band <paramref name="band"/>: [2^(b/4), 2^((b+1)/4)].</summary>
    public static (double Low, double High) BandRange(int band) =>
        (Math.Pow(2.0, band / (double)BandsPerOctave), Math.Pow(2.0, (band + 1) / (double)BandsPerOctave));

    /// <summary>
    /// P2: the WORLD width of a rank's strip for a whole zoom band [<paramref name="low"/>, <paramref name="high"/>]:
    /// max(MIN / low, min(MAX / high, world width)). Drawn at ANY zoom z of the band its screen width is
    /// max(MIN·z/low, min(MAX·z/high, w·z)) — never below <see cref="MinScreenWidthPx"/> (z ≥ low), never above
    /// <see cref="MaxScreenWidthPx"/> (z ≤ high; MIN·2^¼ &lt; MAX/2^¼, so the outer max never exceeds it), exactly the
    /// world width × z where neither clamp binds, and weakly monotone in rank (max/min of a monotone width). The cost:
    /// near a clamp a river may read up to 2^¼ (19 %) heavier than the floor, or lighter than the ceiling, than the
    /// per-zoom mesh drew.
    /// </summary>
    public static double BandWidthWorld(int rank, int count, double low, double high) =>
        Math.Max(MinScreenWidthPx / low, Math.Min(MaxScreenWidthPx / high, WidthForRank(rank, count)));

    /// <summary>P2: the river strips for zoom band <paramref name="band"/> (<see cref="BandWidthWorld"/>) — what the host
    /// uploads once per band.</summary>
    public static Vertex[] BuildBand(TerrainSet terrain, int band)
    {
        (double low, double high) = BandRange(band);
        var vertices = new List<LineGeometry.Vertex>();
        int count = terrain.RiverPolylineCount;
        for (int rank = 0; rank < count; rank++)
        {
            ReadOnlySpan<int> line = terrain.RiverPolyline(rank);
            double halfWidth = BandWidthWorld(rank, count, low, high) / 2.0;
            for (int i = 0; i + 1 < line.Length; i++)
            {
                Vertex a = CellCenter(line[i], terrain.Size);
                Vertex b = Unwrapped(CellCenter(line[i + 1], terrain.Size), a, terrain);
                LineGeometry.AppendSegmentQuad(vertices,
                    new LineGeometry.Vertex(a.X, a.Y), new LineGeometry.Vertex(b.X, b.Y), halfWidth);
            }
        }
        var result = new Vertex[vertices.Count];
        for (int i = 0; i < vertices.Count; i++) result[i] = new Vertex(vertices[i].X, vertices[i].Y);
        return result;
    }
}

/// <summary>
/// P2: WHEN THE HOST REBUILDS THE RIVER BUFFER — once per zoom band (<see cref="RiverMesh.BandOf"/>), counted. The host
/// (SimUiGame) asks <see cref="Update"/> with the shown zoom every frame and rebuilds only when it answers true; the
/// tests drive the same object with the eased camera's zoom and read <see cref="Rebuilds"/>. Pure.
/// </summary>
public sealed class RiverBandCache
{
    /// <summary>The band the buffer was last built for (null before the first build).</summary>
    public int? Band { get; private set; }

    /// <summary>How many times the buffer has been (re)built.</summary>
    public int Rebuilds { get; private set; }

    /// <summary>True when <paramref name="zoom"/> lies in another band than the buffer's — the caller rebuilds for
    /// <see cref="Band"/>, which this has moved to it.</summary>
    public bool Update(double zoom)
    {
        int band = RiverMesh.BandOf(zoom);
        if (Band == band) return false;
        Band = band;
        Rebuilds++;
        return true;
    }
}
