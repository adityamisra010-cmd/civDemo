using Sim.Core.Pathing;
using Sim.Core.State;
using Sim.Core.Worldgen;
using Sim.Ui.Render;
using Sim.Ui.Theme;
using Sim.Ui.ViewModel;
using Sim.Ui.World;
using Xunit;

namespace Sim.Ui.Tests;

/// <summary>
/// BATCH 3 WS3 — THE LENS ON A CYLINDER (<see cref="WorldLens.Paint(DrawList, ITextMeasure, WorldProjection, MapDetail, Func{double, double, ValueTuple{double, double}}, double, RectD, int, bool, MapInk?, EraTheme?, double, LegendAnchor?, int, OverlayPaint?, bool)"/>
/// with wrapX), pure: at the camera's floor the window is one circumference wide, so an element is drawn twice only when
/// the window's edges cut it — and then the two copies are exactly one window apart (no world point painted at two screen
/// x), contiguous across the cut, hit-testable at either half, and counted ONCE per row (<see cref="LensFrame.Copies"/>
/// counts the second draw). An element that no edge cuts is drawn exactly as the flat paint draws it. A pan of one pixel
/// across the flip point (the camera's x normalised from 0 to W) moves every drawn element by one pixel. The seam-crossing
/// primitives — a route, a dirt-path step, a river segment, the placement route — are unwrapped (the short way, never
/// map-wide), and corridor ownership is judged across the seam.
/// </summary>
public class CylinderLensTests
{
    private const int Size = 256;
    private const double Km = 4.0;
    private static readonly RectD Map = new(0, 52, 1280, 688);
    private static readonly RectD Window = new(0, 0, 1280, 800);

    private static SettlementLensView S(int id, double x, double y) =>
        new(id, "S" + id.ToString(System.Globalization.CultureInfo.InvariantCulture), x, y, 0, id == 1, true, 900, 40, 1, 1,
            [], null, 0, 0, 0, []);

    private static WorldProjection Proj(IReadOnlyList<SettlementLensView> settlements, IReadOnlyList<RoadSegment>? roads = null,
        IReadOnlyList<RouteLensView>? routes = null, bool wrap = true, IReadOnlyList<TerritoryBlock>? territory = null) => new()
    {
        WorldSize = Size, WrapX = wrap, KmPerPx = Km, Settlements = settlements, Units = [], Roads = roads ?? [], Routes = routes ?? [],
        Territory = territory ?? [], PlayerPolity = 0, PlayerAge = 1, PlayerAgeName = "", Absent = [],
    };

    private static RouteLensView Route(int id, double x0, double y0, double x1, double y1) =>
        new(id, 3, 1, x0, y0, x1, y1, EdgeTypes.BuiltRoad, EdgeTypes.BuiltRoad, 0.0, 24, "Built road", "Built road");

    /// <summary>The game's wrapped camera at its floor (5 screen px per world px), centred at world x <paramref name="x"/>.</summary>
    private static Camera FloorCamera(double x = Size / 2.0)
    {
        var cam = new Camera(Size, wrapX: true) { WindowWidth = Window.W };
        cam.Clamp(Map);
        cam.Pan(-cam.DeltaX(cam.CenterX, x) * cam.Zoom, 0, Map);
        return cam;
    }

    private static (DrawList D, LensFrame F) Paint(WorldProjection p, Camera cam, bool wrapX = true)
    {
        var d = new DrawList();
        LensFrame f = WorldLens.Paint(d, ApproxTextMeasure.Instance, p, MapDetail.Across(cam.Zoom, Window.W, Km),
            (x, y) => cam.WorldToScreen(x, y, Map), cam.Zoom, Window, wrapX: wrapX);
        return (d, f);
    }

    /// <summary>The game's wrapped camera zoomed to <paramref name="zoom"/> (screen px per world px) about the map rect's centre,
    /// then centred at world x <paramref name="x"/> and y <paramref name="y"/>.</summary>
    private static Camera ZoomedCamera(double zoom, double x, double y)
    {
        Camera cam = FloorCamera(x);
        cam.ZoomAt(Map.CenterX, Map.CenterY, zoom / cam.Zoom, Map);
        cam.Pan(-cam.DeltaX(cam.CenterX, x) * cam.Zoom, -(y - cam.CenterY) * cam.Zoom, Map);
        Assert.Equal(zoom, cam.Zoom, 9);
        return cam;
    }

    /// <summary>The screen length of the part of a horizontal span [a, b] inside the window's half-open [0, W).</summary>
    private static double Visible(double a, double b) =>
        Math.Max(0.0, Math.Min(Math.Max(a, b), Window.Right) - Math.Max(Math.Min(a, b), Window.X));

    // ------------------------------------------------------------------ a settlement and a route across the cut

    [Fact]
    public void ASettlementAndARouteStraddlingTheWindowsEdges_AreDrawnContiguouslyAcrossTheCut_NoPointPaintedTwice()
    {
        Camera cam = FloorCamera();   // centre W/2: the window's two edges are the seam meridian
        Assert.Equal(5.0, cam.Zoom);
        double rowY = 100.5;
        var p = Proj([S(1, 0.5, 128.5), S(2, 128.5, 128.5), S(3, 230.5, rowY)],
            roads: [new RoadSegment(254.0, 60.0, 258.0, 60.0, EdgeTypes.DirtPath, Edge: 7)],
            routes: [Route(5, 250.5, rowY, 256.5, rowY)]);
        // A formation in the field at the seam (its token drawn at u.X + 0.5 = 0.5): cut by the window's edge too.
        p = new WorldProjection
        {
            WorldSize = p.WorldSize, WrapX = true, KmPerPx = p.KmPerPx, Settlements = p.Settlements, Roads = p.Roads, Routes = p.Routes,
            Territory = p.Territory, PlayerPolity = 0, PlayerAge = 1, PlayerAgeName = "", Absent = [],
            Units = [new UnitLensView(9, 0, 0.0, 160.0, 1, "Band", "Band", -1)],
        };
        (DrawList d, LensFrame f) = Paint(p, cam);
        Assert.Equal([9], f.UnitTokens);
        UnitPlacement[] tokens = f.UnitPlacements.Where(u => u.Id == 9).OrderBy(u => u.X).ToArray();
        Assert.Equal(2, tokens.Length);
        Assert.Equal(Window.W, tokens[1].X - tokens[0].X, 9);

        // The straddling settlement: drawn (counted) once, hit at both halves, exactly one window apart.
        Assert.Equal(1, f.SettlementsDrawn.Count(id => id == 1));
        SettlementHit[] halves = f.SettlementHits.Where(h => h.Id == 1).OrderBy(h => h.X).ToArray();
        Assert.Equal(2, halves.Length);
        Assert.Equal(Window.W, halves[1].X - halves[0].X, 9);
        Assert.Equal(2.5, halves[0].X, 9);   // copy 0, at the left edge
        // Every other settlement: drawn once, hit once (no edge cuts it).
        Assert.Single(f.SettlementHits, h => h.Id == 2);
        Assert.Single(f.SettlementHits, h => h.Id == 3);

        // The route across the cut: one piece, both copies' strokes, each pair one window apart, and the visible parts sum to
        // the route's whole length on screen — nothing missing at the cut, nothing doubled.
        PathPiece piece = Assert.Single(f.Paths, x => x.Route == 5);
        IReadOnlyList<RoadStroke> styles = RoadLens.StyleAt(EdgeTypes.BuiltRoad, 0.0, MapInk.Default);
        Assert.Equal(2 * styles.Count, piece.Commands);
        double sy = cam.WorldToScreen(0, rowY, Map).Y;
        LineCmd[] beds = d.Commands.OfType<LineCmd>().Where(l => l.Y0 == sy && l.Width == styles[^1].Width).OrderBy(l => l.X0).ToArray();
        Assert.Equal(2, beds.Length);
        Assert.Equal(Window.W, beds[1].X0 - beds[0].X0, 9);
        Assert.Equal(6.0 * cam.Zoom, Visible(beds[0].X0, beds[0].X1) + Visible(beds[1].X0, beds[1].X1), 9);

        // The dirt step across the cut: likewise.
        PathPiece step = Assert.Single(f.Paths, x => x.NetworkEdge == 7);
        IReadOnlyList<RoadStroke> dirt = RoadLens.StyleAt(EdgeTypes.DirtPath, 0.0, MapInk.Default);
        Assert.Equal(2 * dirt.Count, step.Commands);
        double stepY = cam.WorldToScreen(0, 60.0, Map).Y;
        LineCmd[] path = d.Commands.OfType<LineCmd>().Where(l => l.Y0 == stepY && l.Width == dirt[^1].Width).OrderBy(l => l.X0).ToArray();
        Assert.Equal(2, path.Length);
        Assert.Equal(4.0 * cam.Zoom, Visible(path[0].X0, path[0].X1) + Visible(path[1].X0, path[1].X1), 9);
        // Exactly four elements are cut — the settlement, the route, the step, the token — each drawn once more.
        Assert.Equal(4, f.Copies);
    }

    [Fact]
    public void ACutElement_IsHitAtEitherHalf_AndTheHitTestPicksTheRightSettlement()
    {
        Camera cam = FloorCamera();
        var p = Proj([S(1, 0.5, 128.5), S(2, 128.5, 128.5), S(3, 250.5, 128.5)]);
        (_, LensFrame f) = Paint(p, cam);
        double sy = cam.WorldToScreen(0, 128.5, Map).Y;
        Assert.Equal(1, SettlementSelection.HitTest(f, 2.5, sy));      // the half at the left edge
        Assert.Equal(1, SettlementSelection.HitTest(f, 1279.0, sy));   // the half at the right edge (copy 1, 1282.5)
        Assert.Equal(2, SettlementSelection.HitTest(f, 640.0, sy));
        Assert.Equal(3, SettlementSelection.HitTest(f, 1252.5, sy));
        // The flat paint of the same world draws only copy 0: the right edge picks the neighbour or nothing, never 1.
        (_, LensFrame flat) = Paint(p, cam, wrapX: false);
        Assert.NotEqual(1, SettlementSelection.HitTest(flat, 1279.0, sy));
    }

    [Fact]
    public void WithNoElementAtTheCut_TheWrappedPaintIsTheFlatPaint_CommandForCommand()
    {
        Camera cam = FloorCamera();
        var p = Proj([S(1, 60.5, 128.5), S(2, 128.5, 140.5), S(3, 190.5, 100.5)],
            roads: [new RoadSegment(100.0, 60.0, 104.0, 60.0, EdgeTypes.DirtPath, Edge: 7)],
            routes: [Route(5, 60.5, 128.5, 128.5, 140.5)]);
        (DrawList a, LensFrame fa) = Paint(p, cam, wrapX: true);
        (DrawList b, LensFrame fb) = Paint(p, cam, wrapX: false);
        Assert.Equal(0, fa.Copies);
        Assert.Equal(SvgWriter.Write(b, 1280, 800), SvgWriter.Write(a, 1280, 800));
        Assert.Equal(fb.SettlementHits, fa.SettlementHits);
        Assert.Equal(fb.Paths, fa.Paths);
        Assert.Equal(fb.LayerDraws, fa.LayerDraws);
    }

    [Fact]
    public void OnePixelOfPanAcrossTheFlipPoint_MovesEveryDrawnElementByOnePixel()
    {
        var p = Proj([S(1, 2.5, 128.5), S(2, 253.5, 120.5), S(3, 60.5, 140.5), S(4, 190.5, 110.5)]);
        Camera cam = FloorCamera(0.3);   // the seam just left of the window's centre
        (_, LensFrame before) = Paint(p, cam);
        bool flipped = false;
        for (int step = 0; step < 3; step++)
        {
            double c0 = cam.CenterX;
            cam.Pan(1, 0, Map);
            flipped |= cam.CenterX > c0;   // 0.1 → 255.9: x normalised across the seam
            (_, LensFrame after) = Paint(p, cam);
            int compared = 0;
            foreach (SettlementHit h in before.SettlementHits)
            {
                if (h.X < 40 || h.X > Window.Right - 40) continue;   // an element entering or leaving at an edge
                SettlementHit moved = Assert.Single(after.SettlementHits, x => x.Id == h.Id && Math.Abs(x.X - (h.X + 1.0)) < 1e-6);
                Assert.Equal(h.Y, moved.Y, 9);
                compared++;
            }
            Assert.Equal(4, compared);
            before = after;
        }
        Assert.True(flipped, "the pan never crossed the flip point");
    }

    // ------------------------------------------------------------------ every other element at its copies (W2 review F1)

    [Fact]
    public void ATerritoryBlockCutByTheWindowsEdges_IsTintedAtBothHalves_OneWindowApart_AndCountedOnce()
    {
        Camera cam = FloorCamera(130.0);   // the window shows world x [2, 258): the block over x 0..4 is cut by both edges
        Assert.Equal(5.0, cam.Zoom);
        TerritoryBlock[] blocks = [new(0.0, 120.0, 4.0, 1), new(128.0, 120.0, 4.0, 1)];
        WorldProjection p = Proj([], territory: blocks);
        double side = 4.0 * cam.Zoom + 0.6, ty = cam.WorldToScreen(0, 120.0, Map).Y;
        RectCmd[] Tints(DrawList d) =>
            d.Commands.OfType<RectCmd>().Where(r => r.Rect.W == side && r.Rect.Y == ty).OrderBy(r => r.Rect.X).ToArray();

        (DrawList d, LensFrame f) = Paint(p, cam);
        RectCmd[] tints = Tints(d);
        Assert.Equal(3, tints.Length);
        Assert.Equal(-10.0, tints[0].Rect.X, 9);    // the cut block's copy 0, its east half at the left edge
        Assert.Equal(630.0, tints[1].Rect.X, 9);    // the uncut block, once
        Assert.Equal(1270.0, tints[2].Rect.X, 9);   // the cut block's copy 1, its west half at the right edge
        Assert.Equal(Window.W, tints[2].Rect.X - tints[0].Rect.X, 9);
        Assert.Equal(tints[0].Fill, tints[2].Fill);
        Assert.Equal(1, f.Copies);                  // one element cut, drawn once more
        Assert.Contains(WorldLayer.Territories, f.Painted);

        // The flat paint of the same projection tints only copy 0: the right edge shows no territory (control).
        (DrawList fd, LensFrame ff) = Paint(p, cam, wrapX: false);
        Assert.Equal(2, Tints(fd).Length);
        Assert.Equal(0, ff.Copies);
    }

    [Fact]
    public void ACutSettlementsInstitutionMarkers_AreCountedOnce_WhileItsPartsArePlacedAtBothHalves()
    {
        SettlementLensView held = S(1, 0.5, 128.5) with
        {
            Structures = [new StructureView(7, "Granary", StructureKind.Storage, 2)],
            Institutions = [new InstitutionView(3, "Academy", 1)],
        };
        WorldProjection p = Proj([held, S(2, 128.5, 128.5)]);
        InstitutionMarker[] want = [new(1, "structure:7", 2), new(1, "institution:3", 1)];

        (_, LensFrame f) = Paint(p, FloorCamera());   // the seam meridian at both window edges: settlement 1 is cut
        // Batch 3 R3 (W2 review F7): the 256-px floor is the REGIONAL plateau — the band at which parts are drawn.
        Assert.Equal(WorldZoom.Regional, f.Zoom);
        Assert.True(f.Detail.IsPlateau);
        Assert.Equal(2, f.SettlementHits.Count(h => h.Id == 1));
        Assert.Equal(want, f.Institutions);           // its markers once, as one settlement
        PartPlacement[] parts = f.Parts.Where(x => x.Settlement == 1).ToArray();
        Assert.Equal(4, parts.Length);                // each of its two parts at both halves
        foreach (IGrouping<string, PartPlacement> part in parts.GroupBy(x => x.Key))
        {
            PartPlacement[] two = part.OrderBy(x => x.Rect.X).ToArray();
            Assert.Equal(2, two.Length);
            Assert.Equal(Window.W, two[1].Rect.X - two[0].Rect.X, 9);
            Assert.Equal(two[0].Rect.Y, two[1].Rect.Y, 9);
        }
        // W2 review F7 — the Parts contract on a wrapped paint: ONE ENTRY PER DRAWN COPY (Institutions holds the markers once,
        // Parts one placement per part per copy), and each copy's parts lie inside THAT copy's hit footprint (the click target
        // of the half they are drawn at holds them), never the other half's.
        SettlementHit[] halves = [.. f.SettlementHits.Where(h => h.Id == 1).OrderBy(h => h.X)];
        Assert.Equal(want.Length * halves.Length, parts.Length);
        foreach (SettlementHit half in halves)
        {
            PartPlacement[] mine = [.. parts.Where(x => half.Footprint.Contains(x.Rect.X, x.Rect.Y) && half.Footprint.Contains(x.Rect.Right, x.Rect.Bottom))];
            Assert.Equal(want.Select(m => m.Key).Order(StringComparer.Ordinal), mine.Select(x => x.Key).Order(StringComparer.Ordinal));
            Assert.Equal(want.Length, mine.Length);
        }
        Assert.True(halves[0].Footprint.Right < halves[1].Footprint.X, "the two halves' footprints are one window apart, disjoint");

        // Uncut (in the window's middle), the same settlement holds the same markers, its parts placed once.
        (_, LensFrame g) = Paint(p, FloorCamera(0.5));
        Assert.Single(g.SettlementHits, h => h.Id == 1);
        Assert.Equal(want, g.Institutions);
        Assert.Equal(2, g.Parts.Count(x => x.Settlement == 1));
    }

    [Fact]
    public void ARouteInViewOnlyAcrossTheSeam_IsNamedAtTheCopyInView()
    {
        Camera cam = ZoomedCamera(16.0, 2.0, 100.5);   // Settlement zoom, centred just east of the seam
        Assert.Equal(1.0, MapDetail.Across(cam.Zoom, Window.W, Km).Settlement);
        WorldProjection p = Proj([], routes: [Route(5, 240.5, 100.5, 250.5, 100.5)]);   // just west of it
        double a = cam.WorldToScreen(240.5 - Size, 100.5, Map).X, b = cam.WorldToScreen(250.5 - Size, 100.5, Map).X;
        Assert.Equal(360.0, a, 9);   // copy −1: wholly in view, left of the centre
        Assert.Equal(520.0, b, 9);
        (DrawList d, LensFrame f) = Paint(p, cam);
        TextCmd label = Assert.Single(d.Commands.OfType<TextCmd>(), t => t.Text == "Built road");
        Assert.Equal((a + b) / 2.0, label.X, 9);       // at the midpoint of the line in view
        Assert.True(label.Y < cam.WorldToScreen(0, 100.5, Map).Y, "the name is not above its road");
        PathPiece piece = Assert.Single(f.Paths, x => x.Route == 5);
        Assert.Equal(RoadLens.StyleAt(EdgeTypes.BuiltRoad, 1.0, MapInk.Default).Count + 1, piece.Commands);   // strokes + the name, once
        Assert.Equal(0, f.Copies);

        // The flat paint draws the route at copy 0, a circumference east and off the window: no name (control).
        (DrawList fd, _) = Paint(p, cam, wrapX: false);
        Assert.DoesNotContain(fd.Commands.OfType<TextCmd>(), t => t.Text == "Built road");
    }

    [Fact]
    public void ThePlacementMarks_CutByTheWindowsEdges_AreDrawnAtBothHalves_OneWindowApart()
    {
        const int n = 8;
        TraversalLattice lattice = TraversalLattice.FromCosts(n, Enumerable.Repeat(1.0, n * n).ToArray(),
            Enumerable.Repeat(true, n * n).ToArray(), wrapX: true);
        var mode = new PlacementMode();
        mode.Begin(0, "Alpha", "a party of 40");
        void Set(string name, object value) => typeof(PlacementMode).GetProperty(name)!.SetValue(mode, value);
        Set(nameof(PlacementMode.Hovered), 3 * n + 0);   // column 0: cut by the window's left edge
        Set(nameof(PlacementMode.HoveredVerdict), FoundingVerdict.Legal);
        Set(nameof(PlacementMode.Selected), 5 * n + 0);
        Set(nameof(PlacementMode.SelectedVerdict), FoundingVerdict.Legal);
        EraTheme theme = EraThemes.For(UiEra.Prehistoric);
        var cam = new Camera(n, wrapX: true) { WindowWidth = 400 };
        var view = new RectD(0, 0, 400, 300);
        cam.Clamp(view);
        cam.Pan(-cam.DeltaX(cam.CenterX, 4.5) * cam.Zoom, 0, view);   // the window shows world x [0.5, 8.5)
        Assert.Equal(50.0, cam.Zoom);
        DrawList Draw(bool wrap)
        {
            var d = new DrawList();
            PlacementOverlay.PaintMap(d, theme, MapInk.Default, (x, yy) => cam.WorldToScreen(x, yy, view), n, lattice, mode, 1.0,
                wrapX: wrap, viewport: view);
            return d;
        }

        DrawList w = Draw(true);
        RectCmd[] footprint = w.Commands.OfType<RectCmd>().OrderBy(r => r.Rect.X).ToArray();   // the hovered place
        Assert.Equal(2, footprint.Length);
        Assert.Equal(-25.0, footprint[0].Rect.X, 9);
        Assert.Equal(view.W, footprint[1].Rect.X - footprint[0].Rect.X, 9);
        CircleCmd[] rings = w.Commands.OfType<CircleCmd>().Where(c => c.Stroke is not null).OrderBy(c => c.Cx).ToArray();   // the chosen one
        Assert.Equal(2, rings.Length);
        Assert.Equal(0.0, rings[0].Cx, 9);
        Assert.Equal(view.W, rings[1].Cx - rings[0].Cx, 9);

        // The flat painter marks only copy 0 (control).
        DrawList flat = Draw(false);
        Assert.Single(flat.Commands.OfType<RectCmd>());
        Assert.Single(flat.Commands.OfType<CircleCmd>(), c => c.Stroke is not null);
    }

    // ------------------------------------------------------------------ unwrapped primitives

    [Fact]
    public void RiverMesh_UnwrapsASegmentAcrossTheSeam_TheFlatMeshSpansTheMap()
    {
        const int n = 16;
        double[] Fill(double v) => Enumerable.Repeat(v, n * n).ToArray();
        int[] line = [8 * n + 14, 8 * n + 15, 8 * n + 0, 8 * n + 1];   // a river stepping east across the seam
        TerrainSet Make(bool wrap) => TerrainSet.FromLayers(n, 4.0, 0.5, Fill(0.6), Fill(0.0), Fill(0.5), Fill(0.5), Fill(0.5), Fill(1.0),
            Fill(0.0), [line], wrap);
        RiverMesh.Vertex[] wrapped = RiverMesh.BuildBand(Make(true), 0), flat = RiverMesh.BuildBand(Make(false), 0);
        Assert.Equal(flat.Length, wrapped.Length);
        Assert.Equal(18, wrapped.Length);   // three segments, six vertices each
        double Span(RiverMesh.Vertex[] v, int seg) => v.Skip(seg * 6).Take(6).Max(x => x.X) - v.Skip(seg * 6).Take(6).Min(x => x.X);
        // The seam segment: a short quad past the edge on the cylinder (15.5 → 16.5), map-wide when flat (15.5 → 0.5).
        Assert.True(Span(wrapped, 1) < 4, $"wrapped seam segment spans {Span(wrapped, 1)} px");
        Assert.True(Span(flat, 1) > 14, $"flat seam segment spans {Span(flat, 1)} px");
        Assert.True(wrapped.Skip(6).Take(6).All(v => v.X > 14), "the seam segment is drawn from its first point, east of it");
        // Every segment that does not cross is bit-identical.
        Assert.Equal(flat.Take(6), wrapped.Take(6));
        Assert.Equal(flat.Skip(12), wrapped.Skip(12));
    }

    [Fact]
    public void CorridorOwnership_IsJudgedAcrossTheSeam()
    {
        RouteLensView route = Route(5, 250.5, 100.5, 262.5, 100.5);   // unwrapped: crosses the seam eastward
        var step = new RoadSegment(2.0, 100.5, 6.0, 100.5, EdgeTypes.DirtPath, Edge: 7);   // on it, one circumference west
        Assert.True(RoadLens.InCorridor(step, route, 2.0, Size));
        Assert.False(RoadLens.InCorridor(step, route, 2.0));   // the flat rule: a circumference apart
        Assert.False(RoadLens.InCorridor(step, route, 2.0, 0.0));
        Assert.False(RoadLens.InCorridor(step with { Y0 = 110.5, Y1 = 110.5 }, route, 2.0, Size));   // beside it: not owned
    }

    [Fact]
    public void ThePlacementRoute_AcrossTheSeam_IsOneContiguousLine_TheShortWay()
    {
        const int n = 8;
        var cost = Enumerable.Repeat(1.0, n * n).ToArray();
        var pass = Enumerable.Repeat(true, n * n).ToArray();
        TraversalLattice lattice = TraversalLattice.FromCosts(n, cost, pass, wrapX: true);
        int y = 3;
        int[] nodes = [y * n + 6, y * n + 7, y * n + 0, y * n + 1];
        var mode = new PlacementMode();
        mode.Begin(0, "Alpha", "a party of 40");
        typeof(PlacementMode).GetProperty(nameof(PlacementMode.Route))!.SetValue(mode,
            new RouteQuery.Route { Found = true, Nodes = nodes, Cumulative = [0, 1, 2, 3] });
        EraTheme theme = EraThemes.For(UiEra.Prehistoric);
        var cam = new Camera(n, wrapX: true) { WindowWidth = 400 };
        var view = new RectD(0, 0, 400, 300);
        cam.Clamp(view);
        cam.Pan(-cam.DeltaX(cam.CenterX, 7.5) * cam.Zoom, 0, view);   // the seam in view
        var d = new DrawList();
        PlacementOverlay.PaintMap(d, theme, MapInk.Default, (x, yy) => cam.WorldToScreen(x, yy, view), n, lattice, mode, 1.0,
            wrapX: true, viewport: view);
        PolylineCmd[] lines = d.Commands.OfType<PolylineCmd>().ToArray();
        Assert.NotEmpty(lines);
        foreach (PolylineCmd l in lines)
            for (int i = 1; i < l.Points.Length; i++)
                Assert.Equal(cam.Zoom, l.Points[i].X - l.Points[i - 1].X, 9);   // one node east per step — never a jump back
        // The flat painter on the same route jumps a whole map width back at the seam (control).
        var flat = new DrawList();
        PlacementOverlay.PaintMap(flat, theme, MapInk.Default, (x, yy) => cam.WorldToScreen(x, yy, view), n, lattice, mode, 1.0);
        PolylineCmd fl = flat.Commands.OfType<PolylineCmd>().First();
        Assert.Contains(Enumerable.Range(1, fl.Points.Length - 1), i => fl.Points[i].X - fl.Points[i - 1].X < -cam.Zoom * (n - 2));
    }

    [Fact]
    public void TheProjection_UnwrapsASeamCrossingRouteAndStep_TheShortWay_AndTheStepIsTheRoutesGround()
    {
        WorldState w = UiSession.Start(42, 256, 4).World.Clone();
        TerrainSet t = w.Terrain!;
        Assert.True(t.WrapX);
        int size = t.Size, y = 128;
        (int lsize, int stride) = WorldProjection.LatticeGrid(size);
        w.Settlements[0] = w.Settlements[0] with { SiteCell = y * size + 2 };            // x 2.5
        w.Settlements[1] = w.Settlements[1] with { SiteCell = y * size + size - 3 };     // x 253.5
        w.NetworkNodes.Clear();
        w.NetworkEdges.Clear();
        w.TransportEdges.Clear();
        int ly = y / stride;
        w.NetworkNodes.Add(new NetworkNodeRow(new NetworkNodeId(0), ly * lsize + 0));
        w.NetworkNodes.Add(new NetworkNodeRow(new NetworkNodeId(1), ly * lsize + lsize - 1));
        w.NetworkEdges.Add(new NetworkEdgeRow(new NetworkEdgeId(0), new NetworkNodeId(0), new NetworkNodeId(1), EdgeTypes.DirtPath, 1.0));
        w.TransportEdges.Add(new TransportEdgeRow(0, w.Settlements[0].Id, w.Settlements[1].Id, EdgeTypes.BuiltRoad, TransportModes.Road,
            TransportEdgeStates.Complete, 0, 20.0, 0, 0, 0, EdgeTypes.BuiltRoad, 0.0, 1.0));
        var cfg = UiSession.Start(42, 256, 4).Config;
        WorldProjection p = WorldProjection.Build(w, cfg, id => "s" + id, UiPlayer.Empire);
        Assert.True(p.WrapX);
        RouteLensView route = Assert.Single(p.Routes);
        Assert.Equal(2.5, route.X0);
        Assert.Equal(-2.5, route.X1);   // 253.5, one circumference west: the short way, as its LengthKm measures it
        RoadSegment step = Assert.Single(p.Roads);
        Assert.Equal(2.0, step.X0);
        Assert.Equal(-2.0, step.X1);    // lattice column 63's centre (254), unwrapped
        Assert.Equal(0, step.CoveredBy);   // the route draws this ground

        // The same layers, flat: the primitives span the map (control).
        var lines = new int[t.RiverPolylineCount][];
        for (int i = 0; i < lines.Length; i++) lines[i] = t.RiverPolyline(i).ToArray();
        w.Terrain = TerrainSet.FromLayers(t.Size, t.KmPerPx, t.SeaLevel, t.Elevation, t.Water, t.Temperature, t.Moisture, t.Fertility,
            t.MovementCost, t.Rivers, lines, wrapX: false);
        WorldProjection flat = WorldProjection.Build(w, cfg, id => "s" + id, UiPlayer.Empire);
        Assert.False(flat.WrapX);
        Assert.Equal(253.5, Assert.Single(flat.Routes).X1);
        Assert.Equal(254.0, Assert.Single(flat.Roads).X1);
    }
}
