using Microsoft.Xna.Framework.Input;
using Sim.Core.Kernel;
using Sim.Core.State;
using Sim.Ui.Headless;
using Sim.Ui.Render;
using Sim.Ui.Theme;
using Sim.Ui.ViewModel;
using Sim.Ui.World;
using Xunit;

namespace Sim.Ui.Tests;

/// <summary>The canonical seed-42 world, its projection and its config — built once for the pure lens tests.</summary>
public sealed class CanonicalLensFixture
{
    public UiSession Session { get; } = UiSession.Start(42);
    public WorldProjection Projection { get; }

    /// <summary>Two End Turns: the catchments have claimed their territory (turn 0 has none to draw).</summary>
    public CanonicalLensFixture()
    {
        Session.EndTurn();
        Session.EndTurn();
        Projection = WorldProjection.Build(Session.World, Session.Config, id => Session.Names.Name(id), UiPlayer.Empire);
        Assert.NotEmpty(Projection.Territory);
        Assert.NotEmpty(Projection.Units);
    }
}

/// <summary>
/// P3 (M5 map workstream) — THE HIT GEOMETRY IS WHAT THE LENS DREW. At sampled zooms across all three levels of detail,
/// centred on every settlement of the canonical world and in three eras' type and hand, every settlement the lens drew
/// has one <see cref="SettlementHit"/> whose name plate contains the name's run exactly as it was set (the TextCmd's
/// measured box) and whose footprint is the mark's outermost circle; the click target contains both.
/// </summary>
public class LensHitGeometryTests(CanonicalLensFixture fx) : IClassFixture<CanonicalLensFixture>
{
    private const int W = 1280, H = 800;

    /// <summary>The measured box of a text run as the DrawList set it: its width as set (face, case, tracking) about its
    /// alignment point, one em tall from its top.</summary>
    internal static RectD TextBox(TextCmd t, ITextMeasure m)
    {
        double w = m.Width(t.Text, t.Size, t.Role, t.Style);
        double x = t.Align switch { TextAlign.Center => t.X - w / 2, TextAlign.Right => t.X - w, _ => t.X };
        return new RectD(x, t.Y, w, t.Size);
    }

    internal static bool Holds(RectD outer, RectD inner) =>
        inner.X >= outer.X && inner.Right <= outer.Right && inner.Y >= outer.Y && inner.Bottom <= outer.Bottom;

    [Fact]
    public void AtSampledZoomsInEveryBand_EachHitRectContainsTheDrawnNameAndTheFootprint()
    {
        WorldProjection p = fx.Projection;
        int size = (int)p.WorldSize;
        var bands = new HashSet<WorldZoom>();
        int names = 0;
        EraTheme?[] themes = [null, EraThemes.For(UiEra.Prehistoric), EraThemes.For(UiEra.Modern)];
        foreach (EraTheme? theme in themes)
        {
            MapInk ink = theme is null ? MapInk.Default : MapInk.For(theme);
            var probe = new Camera(size);
            double min = probe.MinZoom(W, H);
            for (int z = 0; z <= 14; z++)
            {
                double zoom = min * Math.Pow(Camera.MaxZoom / min, z / 14.0);
                foreach (SettlementLensView centre in p.Settlements)
                {
                    var cam = new Camera(size);
                    cam.ZoomAt(W / 2.0, H / 2.0, zoom / cam.Zoom, W, H);
                    cam.Pan((cam.CenterX - centre.X) * cam.Zoom, (cam.CenterY - centre.Y) * cam.Zoom, W, H);
                    MapDetail detail = MapDetail.At(cam.Zoom, W, H, p.KmPerPx);   // P2: the game's continuous detail
                    WorldZoom level = detail.Band;
                    bands.Add(level);
                    var d = new DrawList();
                    LensFrame f = WorldLens.Paint(d, ApproxTextMeasure.Instance, p, detail, (x, y) => cam.WorldToScreen(x, y, W, H),
                        cam.Zoom, new RectD(0, 0, W, H), centre.Id, ink: ink, theme: theme);
                    Assert.Equal(f.SettlementsDrawn, f.SettlementHits.Select(h => h.Id));
                    foreach (SettlementHit hit in f.SettlementHits)
                    {
                        string name = p.Settlements.Single(s => s.Id == hit.Id).Name;
                        TextCmd run = Assert.Single(d.Commands.OfType<TextCmd>(), t => t.Text == name);
                        RectD box = TextBox(run, ApproxTextMeasure.Instance);
                        Assert.True(Holds(hit.NamePlate, box), $"{theme?.Era} zoom {zoom:0.###} {level}: {name}'s run {box} outside its plate {hit.NamePlate}");
                        // The footprint: the outermost filled circle centred on the mark (the halo at World zoom, the built
                        // footprint closer) — the hit's footprint is exactly its box.
                        CircleCmd mark = d.Commands.OfType<CircleCmd>().Where(c => c.Fill is not null && c.Cx == hit.X && c.Cy == hit.Y).MaxBy(c => c.R)!;
                        Assert.Equal(new RectD(mark.Cx - mark.R, mark.Cy - mark.R, 2 * mark.R, 2 * mark.R), hit.Footprint);
                        RectD target = SettlementSelection.Target(hit.Footprint);
                        Assert.True(Holds(target, hit.Footprint));
                        Assert.True(target.W >= SettlementSelection.MinTargetDiameterPx && target.H >= SettlementSelection.MinTargetDiameterPx);
                        // A click on the drawn name, or on the mark's centre, admits this settlement (among several, the
                        // ranking may still prefer a nearer mark: SettlementSelection.HitTest's stated rule).
                        Assert.Equal(hit.Id, SettlementSelection.HitTest([hit], box.CenterX, box.CenterY));
                        Assert.Equal(hit.Id, SettlementSelection.HitTest([hit], hit.X, hit.Y));
                        names++;
                    }
                }
            }
        }
        Assert.Equal(3, bands.Count);   // World, Regional and Settlement were all sampled
        Assert.True(names > 3 * 15 * p.Settlements.Count, $"only {names} names checked");
    }

    /// <summary>The hit geometry is a read of the frame only: painting twice gives the same hits, and painting never
    /// changes the world.</summary>
    [Fact]
    public void HitGeometry_IsDeterministic_AndPaintingIsReadOnly()
    {
        string hash = WorldHash.ComputeHex(fx.Session.World);
        var cam = new Camera((int)fx.Projection.WorldSize);
        cam.Clamp(W, H);
        cam.ZoomAt(W / 2.0, H / 2.0, 3, W, H);
        LensFrame Once() => WorldLens.Paint(new DrawList(), ApproxTextMeasure.Instance, fx.Projection,
            MapDetail.At(cam.Zoom, W, H, fx.Projection.KmPerPx), (x, y) => cam.WorldToScreen(x, y, W, H), cam.Zoom,
            new RectD(0, 0, W, H), legend: new LegendAnchor(W - 12, H - 12, 340));
        LensFrame a = Once(), b = Once();
        Assert.Equal(a.SettlementHits, b.SettlementHits);
        Assert.Equal(a.InfoDraws, b.InfoDraws);
        Assert.Equal(a.Painted, b.Painted);
        Assert.Equal(a.Legend!.Rect, b.Legend!.Rect);
        Assert.Equal(hash, WorldHash.ComputeHex(fx.Session.World));
    }
}

/// <summary>
/// P3 — THE MAP LEGEND, ACCOUNTED THROUGH THE CHARGE. The legend is a DATA legend painted by the lens through the
/// DrawList: one entry per information layer the frame actually drew (<see cref="LensFrame.Painted"/>), then the
/// projection's absent lines verbatim; every one of its commands is charged to <see cref="MapLayer.Legend"/>, and every
/// other command to exactly one information layer of its map layer.
/// </summary>
public class MapLegendTests(CanonicalLensFixture fx) : IClassFixture<CanonicalLensFixture>
{
    private const int W = 1280, H = 800;
    private static readonly LegendAnchor Anchor = new(W - 12, H - 60, 340);

    private (DrawList D, LensFrame F) Paint(WorldProjection p, double zoomTimesFit, int centreRow, bool territory = true,
        LegendAnchor? legend = null, RectD? viewport = null)
    {
        int size = (int)p.WorldSize;
        var cam = new Camera(size);
        cam.Clamp(W, H);
        cam.ZoomAt(W / 2.0, H / 2.0, zoomTimesFit, W, H);
        if (centreRow >= 0)
        {
            SettlementLensView s = p.Settlements[centreRow];
            cam.Pan((cam.CenterX - s.X) * cam.Zoom, (cam.CenterY - s.Y) * cam.Zoom, W, H);
        }
        var d = new DrawList();
        LensFrame f = WorldLens.Paint(d, ApproxTextMeasure.Instance, p, MapDetail.At(cam.Zoom, W, H, p.KmPerPx),
            (x, y) => cam.WorldToScreen(x, y, W, H), cam.Zoom, viewport ?? new RectD(0, 0, W, H), showTerritory: territory,
            legend: legend ?? Anchor);
        return (d, f);
    }

    /// <summary>The frames every accounting test sweeps: three zooms (one per band) on the fit centre and on every
    /// settlement, territory on and off.</summary>
    private IEnumerable<(DrawList D, LensFrame F, string What)> Sweep(WorldProjection p)
    {
        foreach (double zoom in new[] { 1.0, 3.0, 12.0 })
            for (int row = -1; row < p.Settlements.Count; row++)
                foreach (bool territory in new[] { true, false })
                {
                    (DrawList d, LensFrame f) = Paint(p, zoom, row, territory);
                    yield return (d, f, $"zoom x{zoom} row {row} territory {territory}");
                }
    }

    /// <summary>The legend's commands: the last <c>LayerDraws[Legend]</c> commands of the list (it is painted last).</summary>
    internal static List<DrawCmd> LegendCommands(DrawList d, LensFrame f) =>
        d.Commands.Skip(d.Commands.Count - f.LayerDraws[(int)MapLayer.Legend]).ToList();

    /// <summary>Re-assembles the legend's wrapped runs into its texts: the title, each entry, the heading, each absent
    /// line — greedily joining runs (with one space) until the expected text is matched exactly.</summary>
    internal static void AssertRunsSpell(IReadOnlyList<string> runs, IReadOnlyList<string> texts)
    {
        int r = 0;
        foreach (string want in texts)
        {
            Assert.True(r < runs.Count, $"the legend ran out of runs before '{want}'");
            string got = runs[r++];
            while (got.Length < want.Length && r < runs.Count && want.StartsWith(got + " ", StringComparison.Ordinal)) got += " " + runs[r++];
            Assert.Equal(want, got);
        }
        Assert.Equal(runs.Count, r);   // nothing else is written in the legend
    }

    internal static List<string> Expected(MapLegend legend)
    {
        var texts = new List<string> { legend.Title };
        texts.AddRange(legend.Entries);
        if (legend.Absent.Count > 0) texts.Add(WorldLens.LegendAbsentHeading);
        texts.AddRange(legend.Absent);
        return texts;
    }

    [Fact]
    public void TheLegend_ListsExactlyTheLayersDrawn_PlusTheAbsentLines_AndEveryCommandIsCharged()
    {
        WorldProjection p = fx.Projection;
        Assert.NotEmpty(p.Absent);
        int frames = 0;
        foreach ((DrawList d, LensFrame f, string what) in Sweep(p))
        {
            frames++;
            // The Charge: every command charged to one map layer; every lens command but the legend's to one information
            // layer OF THAT map layer (the two accountings agree layer by layer); nothing charged to a layer that is off.
            Assert.Equal(d.Commands.Count, f.LayerDraws.Sum());
            Assert.Equal(WorldLens.InfoLayerCount, f.InfoDraws.Count);
            foreach (MapLayer m in MapLayerOwnership.All)
            {
                if (m == MapLayer.Legend || MapLayerOwnership.OwnerOf(m) != LayerOwner.WorldLens) continue;
                int sum = 0;
                foreach (WorldLayer w in Enum.GetValues<WorldLayer>()) if (MapLayerOwnership.MapLayerOf(w) == m) sum += f.InfoDraws[(int)w];
                Assert.True(sum == f.LayerDraws[(int)m], $"{what}: {m} {f.LayerDraws[(int)m]} commands, its information layers {sum}");
            }
            foreach (WorldLayer w in Enum.GetValues<WorldLayer>())
                if (f.InfoDraws[(int)w] > 0) Assert.Contains(w, f.Layers);
            // Drawn: a subsequence of the zoom's layers (paint order), each with commands.
            Assert.Equal(f.Layers.Where(f.Painted.Contains), f.Painted);
            foreach (WorldLayer w in f.Painted) Assert.True(f.InfoDraws[(int)w] > 0, what + ": " + w);

            // The legend lists exactly those layers, by their labels, then the absent lines verbatim.
            MapLegend legend = f.Legend!;
            Assert.Equal(f.Painted, legend.Layers);
            Assert.Equal(f.Painted.Select(WorldLens.LayerLabel), legend.Entries);
            Assert.Equal(p.Absent, legend.Absent);
            Assert.Equal(WorldLens.LegendTitle(f.Zoom), legend.Title);

            // And its commands — charged to the Legend layer — are its plate and exactly those runs, inside the plate.
            List<DrawCmd> cmds = LegendCommands(d, f);
            Assert.IsType<RectCmd>(cmds[0]);
            Assert.Equal(legend.Rect, ((RectCmd)cmds[0]).Rect);
            List<TextCmd> runs = cmds.Skip(1).Cast<TextCmd>().ToList();
            Assert.Equal(cmds.Count - 1, runs.Count);
            AssertRunsSpell(runs.Select(t => t.Text).ToList(), Expected(legend));
            foreach (TextCmd t in runs)
                Assert.True(LensHitGeometryTests.Holds(legend.Rect, LensHitGeometryTests.TextBox(t, ApproxTextMeasure.Instance)), $"{what}: '{t.Text}' outside the legend");
            Assert.True(legend.Rect.W <= Anchor.MaxWidth);
            Assert.Equal(Anchor.Right, legend.Rect.Right, 9);
            Assert.Equal(Anchor.Bottom, legend.Rect.Bottom, 9);
        }
        Assert.True(frames > 20);
    }

    [Fact]
    public void ALayerOnButNotDrawn_IsNotListed()
    {
        WorldProjection p = fx.Projection;
        // Territory toggled off: on at World zoom, drawn nothing, not listed (and listed with the toggle on).
        (_, LensFrame on) = Paint(p, 1.0, -1, territory: true);
        (_, LensFrame off) = Paint(p, 1.0, -1, territory: false);
        Assert.Equal(WorldZoom.World, off.Zoom);
        Assert.Contains(WorldLayer.Territories, off.Layers);
        Assert.Contains(WorldLayer.Territories, on.Legend!.Layers);
        Assert.DoesNotContain(WorldLayer.Territories, off.Legend!.Layers);
        Assert.Equal(0, off.InfoDraws[(int)WorldLayer.Territories]);

        // No formation in the projection: the formations layer is on, and not listed.
        Assert.NotEmpty(p.Units);
        WorldProjection noUnits = new()
        {
            WorldSize = p.WorldSize, KmPerPx = p.KmPerPx, Settlements = p.Settlements, Units = [], Roads = p.Roads, Routes = p.Routes,
            Territory = p.Territory, PlayerPolity = p.PlayerPolity, PlayerAge = p.PlayerAge, PlayerAgeName = p.PlayerAgeName, Absent = p.Absent,
        };
        Assert.Contains(WorldLayer.MilitaryFormations, on.Legend!.Layers);
        (_, LensFrame bare) = Paint(noUnits, 1.0, -1);
        Assert.Contains(WorldLayer.MilitaryFormations, bare.Layers);
        Assert.DoesNotContain(WorldLayer.MilitaryFormations, bare.Legend!.Layers);

        // A viewport that sees none of it: every command lies outside, so nothing is listed — but the absent lines are.
        (DrawList dn, LensFrame none) = Paint(p, 1.0, -1, viewport: new RectD(-100000, -100000, 10, 10));
        Assert.True(dn.Commands.Count > none.LayerDraws[(int)MapLayer.Legend], "the lens still emitted its commands");
        Assert.Empty(none.Painted);
        Assert.Empty(none.Legend!.Layers);
        Assert.Equal(p.Absent, none.Legend.Absent);
    }

    /// <summary>The independent identity: with a viewport that sees every command, a layer is drawn exactly when it
    /// emitted any command — so the legend is exactly the layers that drew something.</summary>
    [Fact]
    public void WithEverythingInView_DrawnIsExactlyTheLayersThatEmittedACommand()
    {
        WorldProjection p = fx.Projection;
        var all = new RectD(-1e7, -1e7, 2e7, 2e7);
        foreach (double zoom in new[] { 1.0, 3.0, 12.0 })
            foreach (bool territory in new[] { true, false })
            {
                (_, LensFrame f) = Paint(p, zoom, 0, territory, viewport: all);
                var emitted = new List<WorldLayer>();
                foreach (WorldLayer w in f.Layers) if (f.InfoDraws[(int)w] > 0) emitted.Add(w);
                Assert.Equal(emitted, f.Painted);
                Assert.Equal(emitted, f.Legend!.Layers);
            }
    }

    /// <summary>The selected formation's frame is drawn by the lens (moved from GameUi so the legend stays the topmost
    /// thing the lens draws): one rect in the selection ink about the token, charged to the formations, and the legend
    /// still the last commands of the list.</summary>
    [Fact]
    public void TheSelectedFormationsFrame_IsTheLenss_AndTheLegendStaysOnTop()
    {
        WorldProjection p = fx.Projection;
        int unit = p.Units[0].Id;
        var cam = new Camera((int)p.WorldSize);
        cam.Clamp(W, H);
        cam.ZoomAt(W / 2.0, H / 2.0, 3, W, H);
        MapDetail level = MapDetail.At(cam.Zoom, W, H, p.KmPerPx);
        LensFrame Draw(DrawList d, int selectedUnit) => WorldLens.Paint(d, ApproxTextMeasure.Instance, p, level,
            (x, y) => cam.WorldToScreen(x, y, W, H), cam.Zoom, new RectD(0, 0, W, H), legend: Anchor, selectedUnit: selectedUnit);
        var plain = new DrawList();
        LensFrame a = Draw(plain, -1);
        var framed = new DrawList();
        LensFrame b = Draw(framed, unit);
        Assert.Equal(a.LayerDraws[(int)MapLayer.UnitTokens] + 1, b.LayerDraws[(int)MapLayer.UnitTokens]);
        Assert.Equal(a.InfoDraws[(int)WorldLayer.MilitaryFormations] + 1, b.InfoDraws[(int)WorldLayer.MilitaryFormations]);
        UnitPlacement at = b.UnitPlacements.Single(u => u.Id == unit);
        double ts = UnitSelection.TokenSize(b.Zoom) + 4;
        RectCmd ring = Assert.Single(framed.Commands.OfType<RectCmd>(), r => r.Stroke == MapInk.Default.Selection && r.Fill is null);
        Assert.Equal(new RectD(at.X - ts, at.Y - ts * 0.8, 2 * ts, 1.6 * ts), ring.Rect);
        Assert.DoesNotContain(plain.Commands.OfType<RectCmd>(), r => r.Stroke == MapInk.Default.Selection && r.Fill is null);
        // The legend is still the tail of the list: its plate first, then its runs.
        List<DrawCmd> tail = LegendCommands(framed, b);
        Assert.Equal(b.Legend!.Rect, Assert.IsType<RectCmd>(tail[0]).Rect);
        Assert.All(tail.Skip(1), c => Assert.IsType<TextCmd>(c));
        Assert.True(framed.Commands.ToList().IndexOf(ring) < framed.Commands.Count - tail.Count);
    }

    /// <summary>The era's HAND (P13: stroke, corner, rule — never geometry) does not move or resize the legend or change
    /// a word of it: in every era's ink and hand, the same box, the same layers and the same runs at the same points.</summary>
    [Fact]
    public void TheLegend_IsTheSameBoxAndWords_InEveryErasHand()
    {
        WorldProjection p = fx.Projection;
        (DrawList d0, LensFrame f0) = PaintInk(p, MapInk.Default);
        List<TextCmd> runs0 = LegendCommands(d0, f0).OfType<TextCmd>().ToList();
        foreach (UiEra era in UiEras.All)
        {
            (DrawList d, LensFrame f) = PaintInk(p, MapInk.For(EraThemes.For(era)));
            Assert.Equal(f0.Legend!.Rect, f.Legend!.Rect);
            Assert.Equal(f0.Legend.Layers, f.Legend.Layers);
            Assert.Equal(f0.SettlementHits, f.SettlementHits);
            List<TextCmd> runs = LegendCommands(d, f).OfType<TextCmd>().ToList();
            Assert.Equal(runs0.Select(t => (t.X, t.Y, t.Text, t.Size)), runs.Select(t => (t.X, t.Y, t.Text, t.Size)));
        }
    }

    private static (DrawList D, LensFrame F) PaintInk(WorldProjection p, MapInk ink)
    {
        var cam = new Camera((int)p.WorldSize);
        cam.Clamp(W, H);
        cam.ZoomAt(W / 2.0, H / 2.0, 3, W, H);
        var d = new DrawList();
        LensFrame f = WorldLens.Paint(d, ApproxTextMeasure.Instance, p, MapDetail.At(cam.Zoom, W, H, p.KmPerPx),
            (x, y) => cam.WorldToScreen(x, y, W, H), cam.Zoom, new RectD(0, 0, W, H), ink: ink, legend: Anchor);
        return (d, f);
    }

    /// <summary>Two commands are the same command (polygons and polylines by their points, not their arrays).</summary>
    private static bool Same(DrawCmd a, DrawCmd b) => (a, b) switch
    {
        (PolygonCmd x, PolygonCmd y) => x.Fill == y.Fill && x.Points.SequenceEqual(y.Points),
        (PolylineCmd x, PolylineCmd y) => x with { Points = [] } == y with { Points = [] } && x.Points.SequenceEqual(y.Points),
        _ => a == b,
    };

    /// <summary>The finer accounting charges each run to the layer it SHOWS — pinned where the count is known from state:
    /// with everything in view, one territory block per claimed node at World zoom, and at Settlement zoom exactly one
    /// population line and one infrastructure line per settlement drawn (a label charged to the wrong layer moves
    /// these, though the map layer's total would not change).</summary>
    [Fact]
    public void EachRunIsChargedToTheLayerItShows()
    {
        WorldProjection p = fx.Projection;
        var all = new RectD(-1e7, -1e7, 2e7, 2e7);
        (_, LensFrame world) = Paint(p, 1.0, -1, viewport: all);
        Assert.Equal(WorldZoom.World, world.Zoom);
        Assert.Equal(p.Territory.Count, world.InfoDraws[(int)WorldLayer.Territories]);
        Assert.Equal(0, world.InfoDraws[(int)WorldLayer.PopulationScale]);
        (_, LensFrame town) = Paint(p, 12.0, 0, viewport: all);
        Assert.Equal(WorldZoom.Settlement, town.Zoom);
        Assert.Equal(p.Settlements.Count, town.SettlementsDrawn.Count);
        Assert.Equal(town.SettlementsDrawn.Count, town.InfoDraws[(int)WorldLayer.PopulationScale]);
        Assert.Equal(town.SettlementsDrawn.Count, town.InfoDraws[(int)WorldLayer.InfrastructureDensity]);
        Assert.Equal(0, town.InfoDraws[(int)WorldLayer.Territories]);
        Assert.Equal(0, town.InfoDraws[(int)WorldLayer.MajorSettlements]);
    }

    /// <summary>Without an anchor nothing of the legend is drawn, and the rest of the frame is command for command the
    /// frame drawn with it (the legend only adds; it is painted last).</summary>
    [Fact]
    public void WithoutAnAnchor_NoLegend_AndTheMapIsTheSame()
    {
        WorldProjection p = fx.Projection;
        int size = (int)p.WorldSize;
        var cam = new Camera(size);
        cam.Clamp(W, H);
        cam.ZoomAt(W / 2.0, H / 2.0, 3, W, H);
        MapDetail level = MapDetail.At(cam.Zoom, W, H, p.KmPerPx);
        var plain = new DrawList();
        LensFrame a = WorldLens.Paint(plain, ApproxTextMeasure.Instance, p, level, (x, y) => cam.WorldToScreen(x, y, W, H), cam.Zoom, new RectD(0, 0, W, H));
        var keyed = new DrawList();
        LensFrame b = WorldLens.Paint(keyed, ApproxTextMeasure.Instance, p, level, (x, y) => cam.WorldToScreen(x, y, W, H), cam.Zoom, new RectD(0, 0, W, H), legend: Anchor);
        Assert.Null(a.Legend);
        Assert.Equal(0, a.LayerDraws[(int)MapLayer.Legend]);
        Assert.NotNull(b.Legend);
        Assert.True(keyed.Commands.Count > plain.Commands.Count);
        for (int i = 0; i < plain.Commands.Count; i++)
            Assert.True(Same(plain.Commands[i], keyed.Commands[i]), $"command {i}: {plain.Commands[i]} vs {keyed.Commands[i]}");
        Assert.Equal(keyed.Commands.Count - plain.Commands.Count, b.LayerDraws[(int)MapLayer.Legend]);
        Assert.Equal(a.SettlementHits, b.SettlementHits);
        Assert.Equal(a.Painted, b.Painted);
    }
}

/// <summary>
/// P3 — IN THE LIVE UI: a click on a drawn name selects that settlement (the name plate the lens set, at a level of
/// detail where the legacy 22 px circle about the mark could not reach it); the legend is shown and hidden by its tab and
/// by L, is painted through the lens's DrawList (its text in LastLensCommands and FrameText under the lens surface,
/// never in an ImGui window or tooltip), lists the frame's drawn layers, lies over the map (a click on it selects nothing
/// beneath), and none of it changes the world hash or the order log.
/// </summary>
[Collection("ImGui context")]
public class LensHitGeometryUiTests : IDisposable
{
    private static string Assets() => Path.Combine(AppContext.BaseDirectory, "assets");

    private readonly string _root = Path.Combine(Path.GetTempPath(),
        "sim-ui-p3-" + Environment.ProcessId.ToString(System.Globalization.CultureInfo.InvariantCulture) + "-" + Guid.NewGuid().ToString("N"));

    private string WorkDir(string name) => Path.Combine(_root, name);

    public void Dispose()
    {
        try { if (Directory.Exists(_root)) Directory.Delete(_root, recursive: true); }
        catch (IOException) { }
        catch (UnauthorizedAccessException) { }
        GC.SuppressFinalize(this);
    }

    /// <summary>Wheels in at the map rect's centre until the lens is at <paramref name="want"/>.</summary>
    private static void ZoomTo(UiFrameHarness h, WorldZoom want)
    {
        for (int k = 0; k < 60 && h.Ui.LensFrame?.Zoom != want; k++) h.Wheel(h.Ui.MapRect.CenterX, h.Ui.MapRect.CenterY, 1);
        Assert.Equal(want, h.Ui.LensFrame?.Zoom);
    }

    private static TextCmd NameRun(UiFrameHarness h, int id) =>
        Assert.Single(h.Ui.LastLensCommands.OfType<TextCmd>(), t => t.Text == h.Ui.Session.Names.Name(id));

    [Theory]
    [InlineData(WorldZoom.Regional)]
    [InlineData(WorldZoom.Settlement)]
    public void ClickingADrawnName_SelectsThatSettlement(WorldZoom level)
    {
        using UiFrameHarness h = UiFrameHarness.Start(UiSession.Start(42), WorkDir("name-" + level), Assets());
        string hash = WorldHash.ComputeHex(h.Ui.Session.World);
        WorldState w = h.Ui.World;
        int target = -1;
        for (int i = w.Settlements.Count - 1; i >= 0 && target < 0; i--)
            if (w.Settlements[i].Id.Value != h.Ui.SelectedSettlement) target = w.Settlements[i].Id.Value;
        Assert.True(target >= 0);
        int before = h.Ui.SelectedSettlement;
        Assert.True(CameraFocus.CenterOn(h.Ui.Camera, w, target, h.Ui.MapRect));
        h.Idle(2);
        ZoomTo(h, level);
        Assert.True(CameraFocus.CenterOn(h.Ui.Camera, w, target, h.Ui.MapRect));
        h.Idle(2);

        TextCmd run = NameRun(h, target);
        double cx = run.X, cy = run.Y + run.Size / 2;   // the middle of the drawn name
        SettlementHit hit = h.Ui.LensFrame!.SettlementHits.Single(x => x.Id == target);
        // The name lies below the footprint's target: only its plate makes it clickable (the legacy 22 px circle about
        // the mark did not reach it either).
        Assert.False(SettlementSelection.Inside(SettlementSelection.Target(hit.Footprint), cx, cy));
        Assert.True(Math.Sqrt((cx - hit.X) * (cx - hit.X) + (cy - hit.Y) * (cy - hit.Y)) > SettlementSelection.HitRadiusPx);
        Assert.Equal(before, h.Ui.SelectedSettlement);

        h.Click(cx, cy);
        Assert.Equal(target, h.Ui.SelectedSettlement);
        Assert.Equal(hash, WorldHash.ComputeHex(h.Ui.Session.World));
        Assert.Empty(h.Problems);
    }

    [Fact]
    public void TheLegend_TogglesByItsTabAndL_IsPaintedNotATooltip_AndListsTheDrawnLayers()
    {
        using UiFrameHarness h = UiFrameHarness.Start(UiSession.Start(42), WorkDir("legend"), Assets());
        string hash = WorldHash.ComputeHex(h.Ui.Session.World);
        OrderRecord[] queued = [.. h.Ui.Session.QueuedOrders()];
        // Off until asked for; the tab is drawn over the map's bottom-right corner, inside the map rect.
        Assert.False(h.Ui.ShowLegend);
        Assert.Null(h.Ui.LensFrame!.Legend);
        Assert.Equal(0, h.Ui.LensFrame.LayerDraws[(int)MapLayer.Legend]);
        UiControl tab = h.Ui.Controls.Find("legend") ?? throw new Xunit.Sdk.XunitException("the legend tab is not drawn");
        RectD map = h.Ui.MapRect;
        Assert.True(tab.X0 >= map.X && tab.X1 <= map.Right && tab.Y0 >= map.Y && tab.Y1 <= map.Bottom, $"tab {tab} outside the map rect {map}");

        foreach (bool byKey in new[] { false, true })
        {
            if (byKey) h.Key(Keys.L); else Assert.True(h.ClickControl("legend"));
            h.Idle(1);
            Assert.True(h.Ui.ShowLegend);
            LensFrame f = h.Ui.LensFrame!;
            MapLegend legend = f.Legend ?? throw new Xunit.Sdk.XunitException("no legend painted");
            Assert.Equal(f.Painted, legend.Layers);
            Assert.NotEmpty(legend.Layers);
            Assert.NotEmpty(legend.Absent);
            // Through the DrawList: its runs are in the lens's commands and in FrameText under the lens surface.
            var runs = new List<string>();
            foreach (DrawCmd c in h.Ui.LastLensCommands.Skip(h.Ui.LastLensCommands.Count - f.LayerDraws[(int)MapLayer.Legend]))
                if (c is TextCmd t) runs.Add(t.Text);
            MapLegendTests.AssertRunsSpell(runs, MapLegendTests.Expected(legend));
            IReadOnlyList<string> lensText = h.Ui.FrameText.Texts(FrameSurface.Lens);
            foreach (string r in runs) Assert.Contains(r, lensText);
            // Above the tab, inside the map rect; no tooltip window, hovering over it or not.
            Assert.True(legend.Rect.Bottom <= tab.Y0 && legend.Rect.Right <= map.Right && legend.Rect.Y >= map.Y, $"legend {legend.Rect}");
            h.MoveTo(legend.Rect.CenterX, legend.Rect.CenterY);
            Assert.Empty(h.Gui.LastTooltipWindows);

            if (byKey) h.Key(Keys.L); else Assert.True(h.ClickControl("legend"));
            h.Idle(1);
            Assert.False(h.Ui.ShowLegend);
            Assert.Null(h.Ui.LensFrame!.Legend);
            foreach (string r in runs) Assert.DoesNotContain(r, h.Ui.FrameText.Texts(FrameSurface.Lens));
        }
        Assert.Equal(hash, WorldHash.ComputeHex(h.Ui.Session.World));
        Assert.Equal(queued, h.Ui.Session.QueuedOrders());
        Assert.Empty(h.Problems);
    }

    /// <summary>The legend lies over the map: a click on it does not select the settlement drawn beneath it; with the
    /// legend hidden, the same click does.</summary>
    [Fact]
    public void AClickOnTheLegend_SelectsNothingBeneathIt()
    {
        using UiFrameHarness h = UiFrameHarness.Start(UiSession.Start(42), WorkDir("occlude"), Assets());
        WorldState w = h.Ui.World;
        int target = -1;
        for (int i = w.Settlements.Count - 1; i >= 0 && target < 0; i--)
            if (w.Settlements[i].Id.Value != h.Ui.SelectedSettlement) target = w.Settlements[i].Id.Value;
        int before = h.Ui.SelectedSettlement;
        Assert.True(CameraFocus.CenterOn(h.Ui.Camera, w, target, h.Ui.MapRect));
        h.Idle(2);
        ZoomTo(h, WorldZoom.Settlement);
        Assert.True(CameraFocus.CenterOn(h.Ui.Camera, w, target, h.Ui.MapRect));
        h.Key(Keys.L);
        h.Idle(1);
        RectD legend = h.Ui.LensFrame!.Legend!.Rect;
        // Move the camera so the settlement's mark sits in the middle of the legend.
        SettlementHit at = h.Ui.LensFrame.SettlementHits.Single(x => x.Id == target);
        h.Ui.Camera.Pan(legend.CenterX - at.X, legend.CenterY - at.Y, h.Ui.MapRect);
        h.Idle(2);
        at = h.Ui.LensFrame!.SettlementHits.Single(x => x.Id == target);
        Assert.True(h.Ui.LensFrame.Legend!.Rect.Contains(at.X, at.Y), $"the mark {at.X},{at.Y} is not under the legend {h.Ui.LensFrame.Legend.Rect}");
        Assert.Equal(target, SettlementSelection.HitTest(h.Ui.LensFrame, at.X, at.Y));   // the mark IS drawn there, beneath

        h.Click(at.X, at.Y);
        Assert.Equal(before, h.Ui.SelectedSettlement);   // the legend took the click
        h.Key(Keys.L);
        h.Idle(1);
        Assert.Null(h.Ui.LensFrame!.Legend);
        h.Click(at.X, at.Y);
        Assert.Equal(target, h.Ui.SelectedSettlement);   // with the legend hidden, the mark is clickable
        Assert.Empty(h.Problems);
    }
}
