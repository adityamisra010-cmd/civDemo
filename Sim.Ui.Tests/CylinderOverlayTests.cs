using Sim.Core.State;
using Sim.Core.Worldgen;
using Sim.Ui.Render;
using Sim.Ui.ViewModel;
using Sim.Ui.World;
using Xunit;

namespace Sim.Ui.Tests;

/// <summary>A canonical world whose land CROSSES the seam: seed 16 (1024 px, shipped worldgen.json — measured by W1's
/// SeamCrossingShippedWorldTests: 123 rows with land on both edge columns), founded by the real session, turn 0.</summary>
public sealed class SeamCrossingOverlayFixture
{
    public UiSession Session { get; } = UiSession.StartWith(TestFog.PreDiscovery, 16);
}

/// <summary>A 256-px cylinder (seed 42, 4 settlements), founded by the real session, turn 0: at its floor (one circumference,
/// 1024 km, across a 1280-px window) the lens is on its Regional plateau, so the realized-roll markers are drawn there.</summary>
public sealed class SmallCylinderOverlayFixture
{
    public UiSession Session { get; } = UiSession.StartWith(TestFog.PreDiscovery, 42, 256, 4);
}

/// <summary>
/// BATCH 3 WS3 — THE RESOURCE OVERLAY ON A CYLINDER: its mirrors of the simulation's wrapped functions agree with the
/// simulation across the seam (the restricted view's region-fertility window is BoxSum's modular window), its outline has
/// no boundary at the seam where both sides qualify (and none duplicated at the eastern edge) — on seed 16, whose land
/// crosses the seam — and a wrapped paint counts each drawn node ONCE however many copies of its run the window shows (the
/// canonical seed-42 world of <see cref="CanonicalOverlayFixture"/>). The P8/P9 parity tests (ResourceOverlayTests) stay as
/// they are.
/// </summary>
public class CylinderOverlayTests(CanonicalOverlayFixture fx, SeamCrossingOverlayFixture seam, SmallCylinderOverlayFixture small)
    : IClassFixture<CanonicalOverlayFixture>, IClassFixture<SeamCrossingOverlayFixture>, IClassFixture<SmallCylinderOverlayFixture>
{
    private static readonly PolityId Me = UiPlayer.Empire;

    private static ResourceOverlay Bake(UiSession s) =>
        ResourceOverlay.Bake(s.Config, s.Sites!, PlayerKnowledge.Of(s.World, Me).Core);

    /// <summary>A deterministic, non-constant field (no RNG): every cell distinct enough that a wrong window shows.</summary>
    private static double[] Field(int size)
    {
        var f = new double[size * size];
        for (int i = 0; i < f.Length; i++) f[i] = (int)((uint)(i + 1) * 2654435761u % 1000u) / 1000.0;
        return f;
    }

    [Theory]
    [InlineData(64, 3)]
    [InlineData(64, 40)]   // 2r + 1 ≥ size: the window spans every column once
    [InlineData(37, 5)]    // an odd width
    public void TheRestrictedWindowSum_MirrorsTheSimulationsWrappedBoxSum_AtEveryCell(int size, int r)
    {
        double[] f = Field(size);
        double[] sim = SettlementSiting.BoxSum(f, size, r, wrapX: true);
        double[] flat = SettlementSiting.BoxSum(f, size, r);
        int differs = 0;
        for (int y = 0; y < size; y++)
            for (int x = 0; x < size; x++)
            {
                double mine = ResourceOverlay.WindowSum(f, size, x, y, r, wrapX: true), want = sim[y * size + x];
                Assert.True(Math.Abs(mine - want) <= 1e-9 * Math.Max(1.0, Math.Abs(want)), $"({x},{y}): window {mine} vs the simulation's {want}");
                if (Math.Abs(flat[y * size + x] - want) > 1e-9)
                {
                    differs++;
                    Assert.True(Math.Abs(ResourceOverlay.WindowSum(f, size, x, y, r) - want) > 1e-9, "the flat window agreed at a seam cell");
                }
                if (x - r >= 0 && x + r < size)   // an interior window: the same cells in the same order, wrapped or flat
                    Assert.Equal(ResourceOverlay.WindowSum(f, size, x, y, r), mine);
            }
        Assert.True(differs > 0, "no window crossed the seam: the rig is void");
    }

    [Fact]
    public void OnTheShippedSeamWorld_TheWindowSumMirrorsTheSimulation_WhereLandCrossesTheSeam()
    {
        TerrainSet t = seam.Session.World.Terrain!;
        Assert.True(t.WrapX);
        int size = t.Size, r = UiSession.ProductionWorldgen().Siting.FertilityRadiusPx;
        double[] sim = SettlementSiting.BoxSum(t.Fertility, size, r, wrapX: true);
        int crossing = 0;
        for (int y = 0; y < size; y++)
        {
            if (!(t.Fertility[y * size] > 0 && t.Fertility[y * size + size - 1] > 0)) continue;   // fertile on both edge columns
            crossing++;
            foreach (int x in new[] { 0, r - 1, size - r, size - 1 })
            {
                double mine = ResourceOverlay.WindowSum(t.Fertility, size, x, y, r, wrapX: true), want = sim[y * size + x];
                Assert.True(Math.Abs(mine - want) <= 1e-9 * Math.Max(1.0, Math.Abs(want)), $"({x},{y}): window {mine} vs {want}");
            }
        }
        Assert.True(crossing > 0, "seed 16 has no fertile row across the seam: the rig is void");
    }

    /// <summary>
    /// Batch 3 R3 (W2 review F6) — A RESTRICTED BAKE TAKES ITS REGION FERTILITY OVER THE WRAPPED WINDOW. On seed 16 (land
    /// across the seam), a knowledge stub that knows every node but keeps one mid-map node out of sight sends the bake down its
    /// restricted path (no whole-world sight: <see cref="ResourceOverlay.WindowSum"/> over the node's own window instead of
    /// the simulation's integral image). At every drawn node whose window crosses the seam, the region-fertility value is
    /// SiteAssessment's wrapped BoxSum (the simulation's, to the rounding of a different summation order) — and at the
    /// nodes where the clamped flat window differs (the control: such nodes exist), it is not the flat window's.
    /// </summary>
    [Fact]
    public void ARestrictedBake_TakesRegionFertilityOverTheWrappedWindow_AsSiteAssessmentDoes_WhereTheWindowCrossesTheSeam()
    {
        UiSession s = seam.Session;
        TerrainSet t = s.World.Terrain!;
        Assert.True(t.WrapX);
        int size = t.Size, r = s.Sites!.Worldgen.Siting.FertilityRadiusPx;
        (int l, _) = PolityKnowledge.Grid(size);
        var every = new bool[l * l];
        Array.Fill(every, true);
        var visible = (bool[])every.Clone();
        visible[(l / 2) * l + l / 2] = false;                // one node out of sight, far from the seam: the restricted path
        ResourceOverlay b = ResourceOverlay.Bake(s.Config, s.Sites!, PolityKnowledge.Restricted(s.World, Me, every, visible));
        Assert.False(b.SeesAll);
        OverlayLayer region = b.Layers.Single(x => x.Kind == OverlayKind.RegionFertility);
        int crossing = 0, flatDiffers = 0;
        for (int node = 0; node < b.NodeCount; node++)
        {
            double v = b.Value(region.Index, node);
            if (double.IsNaN(v)) continue;
            int cell = b.SampleCell(node), x = cell % size, y = cell / size;
            if (x - r >= 0 && x + r < size) continue;        // only windows that cross the seam
            crossing++;
            double want = s.Sites!.RegionFertility[cell];     // SiteAssessment's (SettlementSiting.BoxSum, wrapped)
            Assert.True(Math.Abs(v - want) <= 1e-9 * Math.Max(1.0, Math.Abs(want)), $"node {node} ({x},{y}): {v} vs SiteAssessment's {want}");
            double flatWindow = ResourceOverlay.WindowSum(t.Fertility, size, x, y, r);
            if (Math.Abs(flatWindow - want) > 1e-6 * Math.Max(1.0, Math.Abs(want))) flatDiffers++;
        }
        Assert.True(crossing > 0, "no drawn node's window crosses the seam: the rig is void");
        Assert.True(flatDiffers > 0, "the flat window agrees at every seam node: the rig cannot tell the windows apart");
    }

    [Fact]
    public void TheOutline_HasNoBoundaryAtTheSeamWhereBothSidesQualify_AndNoneAtTheEasternEdge()
    {
        ResourceOverlay b = Bake(seam.Session);
        int l = b.LatticeSize, block = b.Block, checkedLayers = 0;
        for (int layer = 0; layer < b.Layers.Count; layer++)
        {
            OverlayOutline o = b.Outline(layer);
            int seamRows = 0, bothSides = 0;
            for (int ny = 0; ny < l; ny++)
            {
                bool west = b.Qualifies(layer, ny * l + l - 1), east = b.Qualifies(layer, ny * l);
                if (west != east) seamRows++;
                if (west && east) bothSides++;
            }
            int atSeam = 0;
            for (int i = 0; i + 3 < o.Segments.Length; i += 4)
            {
                int x0 = o.Segments[i], y0 = o.Segments[i + 1], x1 = o.Segments[i + 2], y1 = o.Segments[i + 3];
                Assert.False(x0 == l * block && x1 == l * block, "a boundary drawn at the eastern edge (the flat outline's)");
                if (x0 == 0 && x1 == 0) atSeam += (y1 - y0) / block;
            }
            Assert.Equal(seamRows, atSeam);   // exactly the rows where one side of the seam qualifies and the other does not
            if (bothSides > 0) checkedLayers++;
        }
        Assert.True(checkedLayers >= 1, "no layer qualifies on both sides of the seam: the rig is void");
    }

    /// <summary>A projection of nothing but <paramref name="settlements"/> on <paramref name="t"/>'s cylinder: only what the
    /// overlay draws (and those settlements) is in the paint.</summary>
    private static WorldProjection Bare(TerrainSet t, IReadOnlyList<SettlementLensView> settlements) => new()
    {
        WorldSize = t.Size, WrapX = t.WrapX, KmPerPx = t.KmPerPx, Settlements = settlements, Units = [], Roads = [], Territory = [],
        PlayerPolity = Me.Value, PlayerAge = 1, PlayerAgeName = "", Absent = [],
    };

    /// <summary>W2 review F1: a 'now usable' outline on a cylinder is drawn at EVERY copy in view — the window at the floor is one
    /// circumference, so every outline segment on the window's rows is drawn, and one the window's edge cuts is drawn at both
    /// halves, one window apart. (Drawn at copy 0 only, the half of the world west of the window's left edge — east of the
    /// seam's other side — would carry no outline at all.)</summary>
    [Fact]
    public void TheOutline_IsDrawnAtEveryCopyInView_AndASegmentCutByTheWindowsEdgeAtBothHalves()
    {
        ResourceOverlay b = Bake(fx.Session);
        TerrainSet t = fx.Session.World.Terrain!;
        Assert.True(t.WrapX);
        int size = t.Size;
        var map = new RectD(0, 52, 1280, 688);
        var window = new RectD(0, 0, 1280, 800);
        // The layer and the cut: the first layer with a horizontal outline segment at least two blocks long on the rows the
        // centred camera shows; the camera is centred half a window east of its midpoint, so the window's left edge cuts it.
        int li = -1;
        (int X0, int Y, int X1) cut = default;
        for (int l = 0; l < b.Layers.Count && li < 0; l++)
        {
            int[] s = b.Outline(l).Segments;
            for (int i = 0; i + 3 < s.Length; i += 4)
                if (s[i + 1] == s[i + 3] && s[i + 2] - s[i] >= 2 * b.Block && Math.Abs(s[i + 1] - size / 2) < 200)
                {
                    li = l;
                    cut = (s[i], s[i + 1], s[i + 2]);
                    break;
                }
        }
        Assert.True(li >= 0, "no layer has a horizontal outline segment on the centre rows: the rig is void");
        double edge = (cut.X0 + cut.X1) / 2.0;
        var cam = new Camera(size, wrapX: true) { WindowWidth = window.W };
        cam.Clamp(map);
        cam.Pan(-cam.DeltaX(cam.CenterX, edge + size / 2.0) * cam.Zoom, 0, map);
        Assert.Equal(window.W, size * cam.Zoom, 9);   // the floor: the window is one circumference
        var shown = new bool[b.Layers.Count];
        shown[li] = true;
        var d = new DrawList();
        LensFrame f = WorldLens.Paint(d, ApproxTextMeasure.Instance, Bare(t, []), MapDetail.Across(cam.Zoom, window.W, t.KmPerPx),
            (x, y) => cam.WorldToScreen(x, y, map), cam.Zoom, window,
            overlay: new OverlayPaint(b, shown, 0.5) { Highlight = new OverlayHighlight([li], []) }, wrapX: true);
        LineCmd[] lines = d.Commands.OfType<LineCmd>().ToArray();
        Assert.Equal(f.Overlay!.OutlineSegments, lines.Length);   // the outline is the only line drawn

        // Every drawn line is an outline segment at some copy; every segment on the window's rows is drawn.
        // A drawn line's world segment and the copy k it was drawn at (world x + k·size).
        ((int, int, int, int) Seg, int K) World(LineCmd l)
        {
            (double x0, double y0) = cam.ScreenToWorld(l.X0, l.Y0, map);
            (double x1, double y1) = cam.ScreenToWorld(l.X1, l.Y1, map);
            int k = (int)Math.Floor(Math.Round(Math.Min(x0, x1)) / size);
            return (((int)Math.Round(x0) - k * size, (int)Math.Round(y0), (int)Math.Round(x1) - k * size, (int)Math.Round(y1)), k);
        }
        int[] seg = b.Outline(li).Segments;
        var segments = new HashSet<(int, int, int, int)>();
        for (int i = 0; i + 3 < seg.Length; i += 4) segments.Add((seg[i], seg[i + 1], seg[i + 2], seg[i + 3]));
        var drawn = new Dictionary<(int, int, int, int), int>();
        var copiesSeen = new SortedSet<int>();
        foreach (LineCmd l in lines)
        {
            ((int, int, int, int) w, int k) = World(l);
            Assert.Contains(w, segments);
            drawn[w] = drawn.TryGetValue(w, out int c) ? c + 1 : 1;
            copiesSeen.Add(k);
        }
        double top = cam.ScreenToWorld(0, window.Y, map).Y, bottom = cam.ScreenToWorld(0, window.Bottom, map).Y;
        int onRows = 0;
        for (int i = 0; i + 3 < seg.Length; i += 4)
        {
            if (Math.Max(seg[i + 1], seg[i + 3]) < top + 1 || Math.Min(seg[i + 1], seg[i + 3]) > bottom - 1) continue;
            onRows++;
            Assert.True(drawn.ContainsKey((seg[i], seg[i + 1], seg[i + 2], seg[i + 3])),
                $"outline segment ({seg[i]},{seg[i + 1]})-({seg[i + 2]},{seg[i + 3]}) on the window's rows was not drawn");
        }
        Assert.True(onRows > 0, "no outline segment on the window's rows: the rig is void");
        Assert.Equal(2, copiesSeen.Count);   // the window shows two copies' pieces of the world, both outlined

        // The cut segment: at both halves, exactly one window apart.
        LineCmd[] halves = lines.Where(l => World(l).Seg == (cut.X0, cut.Y, cut.X1, cut.Y)).OrderBy(l => l.X0).ToArray();
        Assert.Equal(2, halves.Length);
        Assert.Equal(window.W, halves[1].X0 - halves[0].X0, 9);
        Assert.True(halves[0].X0 < window.X && halves[0].X1 > window.X, "copy one of the cut segment does not cross the left edge");
        Assert.True(f.Copies >= 1);
    }

    /// <summary>W2 review F1 and F8: a realized-roll marker on a cylinder is drawn at every copy whose plate reaches the window,
    /// and counted once. At the floor (the window one circumference) a marker whose plate the window's left edge cuts is drawn
    /// at both halves, one window apart; a marker wholly inside the window is drawn once, and its copy a window east — wholly
    /// off the window, though inside the flat paint's generous cull margin — is neither drawn nor counted in
    /// <see cref="LensFrame.Copies"/>.</summary>
    [Fact]
    public void ARollMarker_CutByTheWindowsEdge_IsDrawnAtBothHalves_AndItsCopyOffTheWindowIsNeitherDrawnNorCounted()
    {
        UiSession s = small.Session;
        ResourceOverlay b = s.Overlay!;
        OverlayStatus st = s.OverlayStatus!;
        TerrainSet t = s.World.Terrain!;
        Assert.True(t.WrapX);
        var on = new bool[b.Layers.Count];
        Array.Fill(on, true);
        WorldProjection full = WorldProjection.Build(s.World, s.Config, s.Names.Name, Me, knowledge: s.Knowledge);
        // ADR-056: the economy packets' ores roll on the elevation channel too, so a marker can carry more rolls; the rig takes
        // the player settlement with the SHORTEST marker (then the lowest id) so its plate leaves room for the east copy.
        SettlementLensView sv = full.Settlements.Where(x => x.IsPlayer && b.MarkerText(on, st, x.Id).Length > 0)
            .OrderBy(x => b.MarkerText(on, st, x.Id).Length).ThenBy(x => x.Id).First();
        string text = b.MarkerText(on, st, sv.Id);
        var map = new RectD(0, 52, 1280, 688);
        var window = new RectD(0, 0, 1280, 800);
        var cam = new Camera(t.Size, wrapX: true) { WindowWidth = window.W };
        cam.Clamp(map);
        cam.Pan(-cam.DeltaX(cam.CenterX, t.Size / 2.0) * cam.Zoom, 0, map);
        double zoom = cam.Zoom;
        Assert.Equal(window.W, t.Size * zoom, 9);   // the floor
        MapDetail detail = MapDetail.Across(zoom, window.W, t.KmPerPx);
        Assert.True(detail.Regional > 0, "the 256-px floor is not past the World band: no marker is drawn there");
        var paint = new OverlayPaint(b, on, 0.5, st, null, s.Names.Name);
        (DrawList D, LensFrame F) PaintAt(double screenX)
        {
            double wx = cam.CenterX + (screenX - map.CenterX) / zoom;
            var d = new DrawList();
            LensFrame f = WorldLens.Paint(d, ApproxTextMeasure.Instance, Bare(t, [sv with { X = wx, Y = cam.CenterY }]), detail,
                (x, y) => cam.WorldToScreen(x, y, map), zoom, window, overlay: paint, wrapX: true);
            return (d, f);
        }
        static RectD Plate(DrawList d, TextCmd label) => ((RectCmd)d.Commands[d.Commands.ToList().IndexOf(label) - 1]).Rect;

        // In the window's middle: one marker, west of the settlement — its plate's offset from the settlement's x measured.
        (DrawList mid, LensFrame fm) = PaintAt(640.0);
        TextCmd one = Assert.Single(mid.Commands.OfType<TextCmd>(), x => x.Text == text);
        RectD plate = Plate(mid, one);
        double reachWest = 640.0 - plate.X;   // from the settlement's x to its plate's left edge
        Assert.Equal([(sv.Id, text)], fm.Overlay!.Markers);
        Assert.Equal(0, fm.Copies);

        // Cut by the left edge: the settlement just inside it, its plate straddling x = 0 — drawn at both halves.
        double sx = reachWest - plate.W / 2.0;
        (DrawList cut, LensFrame fc) = PaintAt(sx);
        TextCmd[] halves = cut.Commands.OfType<TextCmd>().Where(x => x.Text == text).OrderBy(x => x.X).ToArray();
        Assert.Equal(2, halves.Length);
        Assert.Equal(window.W, halves[1].X - halves[0].X, 9);
        RectD west = Plate(cut, halves[0]), east = Plate(cut, halves[1]);
        Assert.True(west.X < window.X && west.Right > window.X, "copy 0's plate does not cross the left edge");
        Assert.True(east.X < window.Right && east.Right > window.Right, "copy 1's plate does not cross the right edge");
        Assert.Equal([(sv.Id, text)], fc.Overlay!.Markers);   // counted once
        Assert.True(fc.Copies >= 1);

        // Wholly inside, 400 px from the left edge: its copy a window east (1680) is inside the flat cull margin (right + 600)
        // but its plate is wholly off the window — not drawn, not counted.
        Assert.True(400.0 + window.W - reachWest > window.Right + DrawListCull.FringePx, "the rig's east copy would reach the window");
        (DrawList inside, LensFrame fi) = PaintAt(400.0);
        Assert.Single(inside.Commands.OfType<TextCmd>(), x => x.Text == text);
        Assert.Equal([(sv.Id, text)], fi.Overlay!.Markers);
        Assert.Single(fi.SettlementHits);
        Assert.Equal(0, fi.Copies);
    }

    [Fact]
    public void AWrappedPaint_CountsEachDrawnNodeOnce_WhereverTheWindowCutsTheRuns()
    {
        ResourceOverlay b = Bake(fx.Session);
        WorldState w = fx.Session.World;
        WorldProjection p = WorldProjection.Build(w, fx.Session.Config, fx.Session.Names.Name, Me);
        var on = new bool[b.Layers.Count];
        Array.Fill(on, true);
        var map = new RectD(0, 52, 1280, 688);
        var window = new RectD(0, 0, 1280, 800);
        OverlayFrame Paint(double centreX, out LensFrame f)
        {
            var cam = new Camera(w.Terrain!.Size, wrapX: true) { WindowWidth = 1280 };
            cam.Clamp(map);
            cam.Pan(-cam.DeltaX(cam.CenterX, centreX) * cam.Zoom, 0, map);
            f = WorldLens.Paint(new DrawList(), ApproxTextMeasure.Instance, p, MapDetail.Across(cam.Zoom, 1280, w.Terrain.KmPerPx),
                (x, y) => cam.WorldToScreen(x, y, map), cam.Zoom, window, overlay: new OverlayPaint(b, on, 0.5), wrapX: true);
            return f.Overlay!;
        }
        OverlayFrame whole = Paint(w.Terrain!.Size / 2.0, out LensFrame fw);   // the window is the world, edge to edge
        OverlayFrame cut = Paint(0.0, out LensFrame fc);                          // the window's edges cut the world at x = 512
        for (int i = 0; i < b.Layers.Count; i++)
            Assert.Equal(whole.Nodes[i], cut.Nodes[i]);   // the same nodes, each counted once
        Assert.True(cut.Rects.Sum() > whole.Rects.Sum(), "no run was cut by the window's edges: the rig is void");
        Assert.True(fc.Copies >= cut.Rects.Sum() - whole.Rects.Sum());
        Assert.Equal(0, fw.Overlay!.Rects.Sum() - whole.Rects.Sum());
    }
}
