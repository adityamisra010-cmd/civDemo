using System.Diagnostics;
using System.Globalization;
using System.Text;
using ImGuiNET;
using Sim.Core.Kernel;
using Sim.Core.Pathing;
using Sim.Core.State;
using Sim.Ui.ImGuiIntegration;
using Sim.Ui.Render;
using Sim.Ui.Theme;
using Sim.Ui.ViewModel;
using Sim.Ui.World;

namespace Sim.Ui.Headless;

/// <summary>
/// <c>Sim.Ui --frame-cost map</c> (M5 map workstream P4): the CPU cost of the MAP's per-frame lens through the game's
/// real path — <see cref="WorldLens.Paint"/> of the turn's <see cref="WorldProjection"/> → <see cref="DrawListImGuiBackend"/>
/// replay (off-screen cull ON, as in the game) into ImGui's background list → <c>ImGui.Render</c> — exactly what
/// <c>GameUi.DrawWorldLensAndAge</c> does each frame, with the era's ink and type and the window's automatic UI scale.
/// The terrain bake and rivers are drawn by the GPU host and are not in it.
///
/// Each sample is a SWEEP: from the fit zoom to the camera's maximum in wheel notches (×1.25, at the window centre,
/// through the real <see cref="Camera"/> and its clamp), and at every zoom a serpentine pan over a 4×4 grid of the
/// world, so every band (World, Regional, Settlement — the derived band of P2's <see cref="MapDetail"/>, the continuous
/// level of detail the game paints at, keyed on the visible km) is crossed; a sweep that
/// misses a band is an error. Windows: 1280×800 (the design size), 1920×1009 (a maximised 1080p window),
/// 2560×1440 and 3840×2160. Worlds, both seed 42 at the canonical size, founded and stepped by the real session:
/// the CANONICAL world at turn <c>--turns</c> (default <see cref="DefaultCanonicalTurn"/>: by then its settlements
/// have laid a dirt-path network for the Paths layer to draw — none exists at turn 1), and a HIGH-SETTLEMENT-COUNT
/// world founded by the simulation's own founding with <c>--settlements N</c> (the existing launch option; default
/// <see cref="DefaultSettlements"/>, about the siting capacity the worldgen record gives) at turn
/// <see cref="HighCountTurn"/>, the first turn with catchment territory (each of its turns costs tens of seconds).
/// P9: the canonical world is also swept with EVERY resource-overlay layer shown at the default opacity with its key
/// (<see cref="ResourceOverlay"/>; the overlay's worst case) and with the settlement-suitability layer alone (one layer:
/// the common case), and a table reports the overlay's bake and per-turn status. P10: and with the region-fertility layer
/// alone, outlined as a 'now usable' shortcut shows it.
/// Nothing is constructed or persisted: the worlds live in this process only. Looking changes nothing: each world's
/// hash is taken before the sweeps and compared after them; a difference aborts the run.
///
/// Wall-clock: the machine's load moves it, so the table reports the median of the per-sweep means after a run-wide
/// warm-up (<see cref="GlobalWarmUp"/>) and <see cref="WarmUpSweeps"/> warm-up sweeps per configuration; it is a
/// measurement, not a budget (ADR-009 sets none). The second table times the per-turn projection build, and the full-terrain lattice build it no longer runs
/// (P4: the stride is arithmetic now).
/// </summary>
public static class MapFrameCost
{
    /// <summary>The <c>--frame-cost</c> mode word that selects this measurement.</summary>
    public const string Mode = "map";

    /// <summary>The high-count world's founding settlement count when <c>--settlements</c> is absent: about the siting
    /// capacity of a canonical world (median 457 over 5 seeds, worldgen.json <c>_docMinSpacing</c>), i.e. the scale
    /// colonisation can grow a world to.</summary>
    public const int DefaultSettlements = 400;

    /// <summary>The canonical world's turn when <c>--turns</c> is absent.</summary>
    public const int DefaultCanonicalTurn = 150;

    /// <summary>The high-count world's turn: the first with catchment territory (turn 0 has none).</summary>
    public const int HighCountTurn = 1;

    /// <summary>Unmeasured sweeps before each configuration's measured rounds.</summary>
    public const int WarmUpSweeps = 2;

    /// <summary>The run-wide warm-up before anything is measured: every world is swept at the first window, in turn,
    /// for at least this long. MEASURED (P4): with only the per-configuration warm-up, the first configuration measured
    /// read 1.00 ms/frame against 0.25-0.27 at every other window for the same world and band, and 0.15 with
    /// <c>DOTNET_TieredCompilation=0</c> — the .NET tiered JIT had not yet promoted the paint path; tier-up needs wall
    /// time (a quiet period after the call counts), not more calls.</summary>
    public static readonly TimeSpan GlobalWarmUp = TimeSpan.FromSeconds(3);

    /// <summary>The window sizes every world is swept at.</summary>
    public static readonly (int W, int H)[] Windows = [(1280, 800), (1920, 1009), (2560, 1440), (3840, 2160)];

    /// <summary>The pan grid per zoom stop (fractions of the world's side).</summary>
    private static readonly double[] PanGrid = [0.125, 0.375, 0.625, 0.875];

    private const double Notch = 1.25;

    /// <summary>A world swept, and (P9) the resource overlay it is swept with — null for the plain lens.</summary>
    private sealed record MapWorld(string Name, UiSession Session, WorldProjection Projection, string Note,
        ResourceOverlay? Overlay = null, OverlayStatus? OverlayStatus = null, OverlayKind? OnlyLayer = null, bool Outline = false);

    public static string Run(string assetsRoot, int rounds = 5, int settlements = DefaultSettlements,
        int canonicalTurn = DefaultCanonicalTurn, Action<string>? log = null)
    {
        rounds = Math.Max(1, rounds);
        log ??= _ => { };
        MapWorld canonical = Found("canonical", UiSession.Start(42), Math.Max(HighCountTurn, canonicalTurn), log);
        var worlds = new List<MapWorld>
        {
            canonical,
            // P9: the same world with EVERY resource-overlay layer shown at the default opacity, with its key — the overlay's
            // worst case (the game draws only the layers the player shows).
            canonical with
            {
                Name = "canonical + overlay", Overlay = canonical.Session.Overlay, OverlayStatus = canonical.Session.OverlayStatus,
                Note = canonical.Note + "; every resource-overlay layer shown (" + canonical.Session.OverlayLayers.Count.ToString(CultureInfo.InvariantCulture)
                    + ") at opacity " + (GameUi.OverlayOpacityDefault / 10.0).ToString("0.0", CultureInfo.InvariantCulture) + ", with its key",
            },
            // ... and with ONE layer shown (the settlement-suitability score), the case the game draws most.
            canonical with
            {
                Name = "canonical + one overlay layer", Overlay = canonical.Session.Overlay, OverlayStatus = canonical.Session.OverlayStatus,
                OnlyLayer = OverlayKind.Suitability,
                Note = canonical.Note + "; the settlement-suitability overlay layer alone at opacity "
                    + (GameUi.OverlayOpacityDefault / 10.0).ToString("0.0", CultureInfo.InvariantCulture) + ", with its key",
            },
            // P10: the region-fertility layer alone with a 'now usable' shortcut's outline of its qualifying places and the
            // shortcut's rows opening the key (the farming shortcut's view).
            canonical with
            {
                Name = "canonical + one overlay layer + now-usable outline", Overlay = canonical.Session.Overlay,
                OverlayStatus = canonical.Session.OverlayStatus, OnlyLayer = OverlayKind.RegionFertility, Outline = true,
                Note = canonical.Note + "; the region-fertility overlay layer alone at opacity "
                    + (GameUi.OverlayOpacityDefault / 10.0).ToString("0.0", CultureInfo.InvariantCulture)
                    + ", outlined as a 'now usable' shortcut shows it, with its key",
            },
            Found("high-count", UiSession.Start(42, settlementsOverride: Math.Max(1, settlements)), HighCountTurn, log),
        };

        // Looking changes nothing: every world's hash before the sweeps must equal its hash after them.
        var hashes = new List<string>();
        foreach (MapWorld m in worlds) hashes.Add(WorldHash.ComputeHex(m.Session.World));

        var sb = new StringBuilder();
        sb.Append(string.Create(CultureInfo.InvariantCulture,
            $"Map frame cost ({BuildInfo.Describe()}): WorldLens.Paint + ImGui replay (cull on) + ImGui.Render per frame; median of {rounds} sweep(s) after a {GlobalWarmUp.TotalSeconds:0} s run-wide warm-up and {WarmUpSweeps} warm-up sweep(s) per configuration.\n\n"));
        sb.Append("Worlds (seed 42, canonical size; founded and stepped by the real session, never persisted):\n\n");
        foreach (MapWorld m in worlds) sb.Append("- ").Append(m.Name).Append(": ").Append(m.Note).Append('\n');
        sb.Append('\n');
        sb.Append("| window | world | band | frames/sweep | median ms/frame | mean vertices | max list vertices | commands/frame | max commands | culled/frame | cross-fade frames/sweep |\n");
        sb.Append("|---|---|---|---|---|---|---|---|---|---|---|\n");
        WarmUp(assetsRoot, worlds, log);
        foreach ((int w, int h) in Windows)
        {
            log(string.Create(CultureInfo.InvariantCulture, $"map frame cost: {w}x{h}"));
            using var gui = new HeadlessImGui(assetsRoot, w, h, uiScale: UiScale.Auto(h));
            var backend = new DrawListImGuiBackend(gui.Fonts) { Cull = true };
            foreach (MapWorld m in worlds)
            {
                Painter paint = PainterFor(m, backend, gui.Fonts.Scale);
                for (int k = 0; k < WarmUpSweeps; k++) Sweep(gui, backend, paint, m.Projection.WorldSize, m.Projection.KmPerPx, w, h);
                var samples = new List<Band[]>();
                for (int r = 0; r < rounds; r++) samples.Add(Sweep(gui, backend, paint, m.Projection.WorldSize, m.Projection.KmPerPx, w, h));
                for (int z = 0; z < 3; z++) Row(sb, w, h, m.Name, (WorldZoom)z, samples, z);
            }
        }

        sb.Append('\n');
        sb.Append("| world | turn | settlements | territory blocks | path steps | built routes | formations | projection build ms (median) | lattice build ms (median; no longer run by the projection) |\n");
        sb.Append("|---|---|---|---|---|---|---|---|---|\n");
        foreach (MapWorld m in worlds)
        {
            IReadOnlyWorldState world = m.Session.World;
            var build = new List<double>();
            var lattice = new List<double>();
            for (int r = 0; r < rounds; r++)
            {
                long t0 = Stopwatch.GetTimestamp();
                WorldProjection.Build(world, m.Session.Config, m.Session.Names.Name, UiPlayer.Empire);
                build.Add(Stopwatch.GetElapsedTime(t0).TotalMilliseconds);
                t0 = Stopwatch.GetTimestamp();
                TraversalLattice.Build(world.Terrain!, m.Session.Config.Transport.RiverCostFactor);
                lattice.Add(Stopwatch.GetElapsedTime(t0).TotalMilliseconds);
            }
            WorldProjection p = m.Projection;
            sb.Append(string.Create(CultureInfo.InvariantCulture,
                $"| {m.Name} | {world.Clock.Turn} | {p.Settlements.Count} | {p.Territory.Count} | {p.Roads.Count} | {p.Routes.Count} | {p.Units.Count} | {Median(build):0.00} | {Median(lattice):0.00} |\n"));
        }
        // P9: the overlay's bake (once per terrain and knowledge mask) and its per-turn status, per world.
        sb.Append('\n');
        sb.Append("| world | overlay bake ms (median; once per session while the knowledge is the same) | overlay status ms (median; once per turn while shown) | nodes | known land nodes | layers |\n");
        sb.Append("|---|---|---|---|---|---|\n");
        foreach (MapWorld m in worlds)
        {
            if (m.Overlay is not null || m.Session.Sites is not { } site) continue;
            var bake = new List<double>();
            var status = new List<double>();
            ResourceOverlay? last = null;
            for (int r = 0; r < rounds; r++)
            {
                long t0 = Stopwatch.GetTimestamp();
                last = ResourceOverlay.Bake(m.Session.Config, site, m.Session.Knowledge.Core);
                bake.Add(Stopwatch.GetElapsedTime(t0).TotalMilliseconds);
                t0 = Stopwatch.GetTimestamp();
                last.Status(m.Session.World, m.Session.Config, m.Session.Knowledge);
                status.Add(Stopwatch.GetElapsedTime(t0).TotalMilliseconds);
            }
            sb.Append(string.Create(CultureInfo.InvariantCulture,
                $"| {m.Name} | {Median(bake):0.0} | {Median(status):0.0} | {last!.NodeCount} | {last.KnownLandNodes} | {last.Layers.Count} |\n"));
        }
        sb.Append('\n');
        for (int i = 0; i < worlds.Count; i++)
        {
            string after = WorldHash.ComputeHex(worlds[i].Session.World);
            if (after != hashes[i])
                throw new InvalidOperationException("frame-cost map: painting the " + worlds[i].Name + " world changed its hash " + hashes[i] + " -> " + after);
            sb.Append("World hash unchanged by every sweep: ").Append(worlds[i].Name).Append(' ').Append(after).Append('\n');
        }
        return sb.ToString();
    }

    private static void WarmUp(string assetsRoot, List<MapWorld> worlds, Action<string> log)
    {
        (int w, int h) = Windows[0];
        using var gui = new HeadlessImGui(assetsRoot, w, h, uiScale: UiScale.Auto(h));
        var backend = new DrawListImGuiBackend(gui.Fonts) { Cull = true };
        long t0 = Stopwatch.GetTimestamp();
        int sweeps = 0;
        while (sweeps < 2 * worlds.Count || Stopwatch.GetElapsedTime(t0) < GlobalWarmUp)
        {
            MapWorld m = worlds[sweeps % worlds.Count];
            Sweep(gui, backend, PainterFor(m, backend, gui.Fonts.Scale), m.Projection.WorldSize, m.Projection.KmPerPx, w, h);
            sweeps++;
        }
        log(string.Create(CultureInfo.InvariantCulture, $"map frame cost: warm-up {sweeps} sweeps"));
    }

    private static MapWorld Found(string name, UiSession s, int turns, Action<string> log)
    {
        log(name + ": founded " + s.World.Settlements.Count.ToString(CultureInfo.InvariantCulture) + " settlements; stepping");
        while (s.World.Clock.Turn < turns) s.EndTurn();
        WorldProjection p = WorldProjection.Build(s.World, s.Config, s.Names.Name, UiPlayer.Empire);
        string note = string.Create(CultureInfo.InvariantCulture,
            $"turn {s.World.Clock.Turn}, {p.Settlements.Count} settlements, {p.Territory.Count} territory blocks, {p.Roads.Count} dirt-path steps, {p.Routes.Count} built routes, {p.Units.Count} formations");
        return new MapWorld(name, s, p, note);
    }

    /// <summary>One frame's lens paint, as <c>GameUi.DrawWorldLensAndAge</c> issues it (P2: at the continuous detail).</summary>
    private delegate LensFrame Painter(DrawList d, Camera cam, MapDetail detail, int w, int h);

    private static Painter PainterFor(MapWorld m, DrawListImGuiBackend backend, double uiScale)
    {
        IReadOnlyWorldState world = m.Session.World;
        EraTheme theme = EraThemes.For(UiEras.Of(world, m.Session.Config.Ages, UiPlayer.Empire));
        MapInk ink = MapInk.For(theme);
        int selected = world.Settlements.Count > 0 ? world.Settlements[0].Id.Value : -1;   // the game's opening selection
        WorldProjection p = m.Projection;
        bool[]? all = null;
        if (m.Overlay is { } bake)
        {
            all = new bool[bake.Layers.Count];
            foreach (OverlayLayer l in bake.Layers) all[l.Index] = m.OnlyLayer is not { } only || l.Kind == only;
        }
        // P10: the shortcut's outline on the only layer shown, with five key rows (a shortcut's usual count).
        OverlayHighlight? highlight = null;
        if (m.Outline && m.Overlay is { } ob)
            foreach (OverlayLayer l in ob.Layers)
                if (m.OnlyLayer == l.Kind)
                    highlight = new OverlayHighlight([l.Index], ["Farming now usable: fertile land yields more food (x13)",
                        "Outlined: the known places where " + l.Name + " is above 0", "Permission to use: newly known (your research)",
                        DiscoveryShortcuts.KnowledgeStatement, "Shown through turn 19"]);
        return (d, cam, detail, w, h) => WorldLens.Paint(d, backend, p, detail, (x, y) => cam.WorldToScreen(x, y, w, h), cam.Zoom,
            new RectD(0, 0, w, h), selected, showTerritory: true, ink: ink, theme: theme, uiScale: uiScale,
            overlay: m.Overlay is { } o ? new OverlayPaint(o, all!, GameUi.OverlayOpacityDefault / 10.0, m.OverlayStatus,
                new OverlayKeyAnchor(12, h - 12, GameUi.OverlayKeyMaxWidth * uiScale), m.Session.Names.Name) { Highlight = highlight } : null);
    }

    /// <summary>Per-band totals of one sweep.</summary>
    private sealed class Band
    {
        public int Frames, MaxVertices, MaxCommands, Fading;
        public double Ms;
        public long Vertices, Commands, Culled;
    }

    private static Band[] Sweep(HeadlessImGui gui, DrawListImGuiBackend backend, Painter paint, double worldSize, double kmPerPx, int w, int h)
    {
        var bands = new[] { new Band(), new Band(), new Band() };
        var cam = new Camera(worldSize);
        cam.Clamp(w, h);
        while (true)
        {
            for (int j = 0; j < PanGrid.Length; j++)
                for (int k = 0; k < PanGrid.Length; k++)
                {
                    int i = j % 2 == 0 ? k : PanGrid.Length - 1 - k;   // serpentine: each step a short pan
                    double tx = PanGrid[i] * worldSize, ty = PanGrid[j] * worldSize;
                    cam.Pan((cam.CenterX - tx) * cam.Zoom, (cam.CenterY - ty) * cam.Zoom, w, h);
                    Frame(gui, backend, paint, cam, kmPerPx, w, h, bands);
                }
            if (cam.Zoom >= Camera.MaxZoom) break;
            cam.ZoomAt(w / 2.0, h / 2.0, Notch, w, h);
        }
        for (int z = 0; z < bands.Length; z++)
            if (bands[z].Frames == 0)
                throw new InvalidOperationException(string.Create(CultureInfo.InvariantCulture,
                    $"frame-cost map: the {w}x{h} sweep never reached the {(WorldZoom)z} band"));
        return bands;
    }

    private static void Frame(HeadlessImGui gui, DrawListImGuiBackend backend, Painter paint, Camera cam, double kmPerPx,
        int w, int h, Band[] bands)
    {
        MapDetail detail = MapDetail.At(cam.Zoom, w, h, kmPerPx);
        WorldZoom level = detail.Band;
        long t0 = Stopwatch.GetTimestamp();
        gui.BeginFrame(default, default, 1.0 / 60);
        var list = new DrawList();
        paint(list, cam, detail, w, h);
        backend.Render(ImGui.GetBackgroundDrawList(), list);
        ImGuiDrawReport r = gui.EndFrame();
        double ms = Stopwatch.GetElapsedTime(t0).TotalMilliseconds;
        if (!r.Ok) throw new InvalidOperationException("frame-cost map: draw data violation: " + string.Join("; ", r.Violations));
        Band b = bands[(int)level];
        b.Frames++;
        if (!detail.IsPlateau) b.Fading++;
        b.Ms += ms;
        b.Vertices += r.TotalVertices;
        b.MaxVertices = Math.Max(b.MaxVertices, r.MaxListVertices);
        b.Commands += list.Commands.Count;
        b.MaxCommands = Math.Max(b.MaxCommands, list.Commands.Count);
        b.Culled += backend.LastCulled;
    }

    private static double Median(List<double> xs)
    {
        var s = new List<double>(xs);
        s.Sort();
        return s[s.Count / 2];
    }

    private static void Row(StringBuilder sb, int w, int h, string world, WorldZoom band, List<Band[]> samples, int z)
    {
        var means = new List<double>();
        int maxV = 0, maxC = 0;
        foreach (Band[] s in samples)
        {
            means.Add(s[z].Ms / s[z].Frames);
            maxV = Math.Max(maxV, s[z].MaxVertices);
            maxC = Math.Max(maxC, s[z].MaxCommands);
        }
        Band a = samples[0][z];
        sb.Append(string.Create(CultureInfo.InvariantCulture,
            $"| {w}x{h} | {world} | {band} | {a.Frames} | {Median(means):0.00} | {a.Vertices / (double)a.Frames:0} | {maxV} | {a.Commands / (double)a.Frames:0} | {maxC} | {a.Culled / (double)a.Frames:0.0} | {a.Fading} |\n"));
    }
}
