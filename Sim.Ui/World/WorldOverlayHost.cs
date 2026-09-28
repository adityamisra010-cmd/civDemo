using Sim.Ui.Render;
using Sim.Ui.World.Content;
using Sim.Ui.World.Demo;
using Sim.Ui.World.Scene;
using Sim.Ui.World.View;

namespace Sim.Ui.World;

/// <summary>Which world layer the game shows (key V cycles Off → Live → Demo → Off).</summary>
public enum WorldLayerMode { Off = 0, Live = 1, Demo = 2 }

/// <summary>
/// THE WORLD OVERLAY HOST — the composition root the game drives, and deliberately small:
/// it holds the loaded morphology, the two bundles (the live adapters the game hands it, the
/// demo source it loads), ONE UI state per bundle (a selection never crosses from live to demo
/// or back), the demo overlay's own camera, and a view cache. It paints nothing itself
/// (WorldScene does), reads no simulation type (the live bundle arrives as interfaces), and
/// has no command that reaches the simulation: its only mutable state is UI state and the
/// demo's chosen step.
/// </summary>
public sealed class WorldOverlayHost
{
    private readonly string _contentDir;
    private readonly WorldSources? _live;
    private readonly ViewCache _liveCache = new();
    private readonly ViewCache _demoCache = new();

    public WorldOverlayHost(string contentDirectory, WorldSources? live)
    {
        _contentDir = contentDirectory;
        _live = live;
        Reload();
    }

    public WorldLayerMode Mode { get; private set; } = WorldLayerMode.Off;
    public WorldMorphology? Morphology { get; private set; }
    public DemoWorldSource? Demo { get; private set; }
    public IReadOnlyList<WorldDiagnostic> Diagnostics { get; private set; } = [];
    public WorldUiState LiveUi { get; } = new();
    public WorldUiState DemoUi { get; } = new();

    /// <summary>The demo overlay's camera: world centre and px per world unit (null = fit on first frame).</summary>
    public (double X, double Y, double Zoom)? DemoCamera { get; set; }

    public void CycleMode() => Mode = Mode switch
    {
        WorldLayerMode.Off => WorldLayerMode.Live,
        WorldLayerMode.Live => Demo is not null ? WorldLayerMode.Demo : WorldLayerMode.Off,
        _ => WorldLayerMode.Off,
    };

    public void Close() => Mode = WorldLayerMode.Off;

    public void Reload()
    {
        var diags = new List<WorldDiagnostic>();
        (WorldMorphology? morph, IReadOnlyList<WorldDiagnostic> md) =
            WorldContentLoader.LoadMorphologyFile(Path.Combine(_contentDir, WorldContentLoader.MorphologyFile));
        diags.AddRange(md);
        DemoWorldSource? demo = null;
        if (morph is not null)
        {
            int step = Demo?.Step ?? 0;
            (demo, IReadOnlyList<WorldDiagnostic> dd) = DemoWorldSource.LoadFile(Path.Combine(_contentDir, WorldContentLoader.DemoFile), morph, step);
            diags.AddRange(dd);
        }
        Morphology = morph;
        Demo = demo;
        Diagnostics = diags;
        _liveCache.Clear();
        _demoCache.Clear();
    }

    /// <summary>The live view (null when content did not load or no live bundle was given).</summary>
    public WorldView? LiveView() => _live is null || Morphology is null ? null : _liveCache.Get(_live, Morphology);

    public WorldView? DemoView() => Demo is null || Morphology is null ? null : _demoCache.Get(Demo.Sources(), Morphology);

    /// <summary>The world layer over the live map, projected through the game's camera. The
    /// game's selected settlement is passed IN to be highlighted; nothing flows back out.</summary>
    public WorldFrame? LiveFrame(WorldProjection proj, string? mirrorSettlementKey, ITextMeasure measure)
    {
        WorldView? view = LiveView();
        if (view is null) return null;
        LiveUi.MirrorSettlementKey = mirrorSettlementKey;
        ForgetMissing(LiveUi, view);
        return WorldScene.Paint(view, Morphology!, proj, LiveUi, WorldSceneOptions.LiveOverlay, measure);
    }

    /// <summary>The full-screen demo overlay (its own paper, camera, banner and panels).</summary>
    public WorldFrame? DemoFrame(double width, double height, ITextMeasure measure)
    {
        WorldView? view = DemoView();
        if (view is null || Demo is null) return null;
        WorldProjection proj = DemoProjection(width, height);
        DemoStep step = Demo.CurrentStep;
        var options = WorldSceneOptions.Full with { BannerExtra = "[Left/Right] step  [drag] pan  [wheel] zoom  [click] select  [V/Esc] close" };
        return WorldScene.Paint(view, Morphology!, proj, DemoUi, options, measure,
            $"step {step.Id} ({Demo.Step + 1} of {Demo.StepCount}): {step.Name}", Demo.Backdrop);
    }

    public WorldProjection DemoProjection(double width, double height)
    {
        if (DemoCamera is not (double x, double y, double z))
        {
            WorldProjection fit = WorldProjection.FitInto(Demo!.Width, Demo.Height, 352, 74, width - 364, height - 86, width, height);
            DemoCamera = (fit.CenterX, fit.CenterY, fit.PxPerWorldUnit);
            return fit;
        }
        return new WorldProjection(x, y, z, width, height);
    }

    public void DemoPan(double dxScreen, double dyScreen, double width, double height)
    {
        WorldProjection p = DemoProjection(width, height);
        DemoCamera = (p.CenterX - dxScreen / p.PxPerWorldUnit, p.CenterY - dyScreen / p.PxPerWorldUnit, p.PxPerWorldUnit);
    }

    /// <summary>Zoom about a screen point, keeping the world point under it fixed.</summary>
    public void DemoZoomAt(double sx, double sy, double factor, double width, double height)
    {
        WorldProjection p = DemoProjection(width, height);
        WorldPoint w = p.ToWorld(sx, sy);
        double z = Math.Clamp(p.PxPerWorldUnit * factor, 0.5, 60.0);
        DemoCamera = (w.X - (sx - width / 2.0) / z, w.Y - (sy - height / 2.0) / z, z);
    }

    /// <summary>A preview control: move the demo timeline. Touches nothing but the demo's step.</summary>
    public void DemoStepBy(int delta)
    {
        if (Demo is null) return;
        if (delta > 0) Demo.Next(); else if (delta < 0) Demo.Previous();
    }

    /// <summary>Select what is under a click in a frame just painted with the current camera
    /// (never a cached earlier frame); a miss clears the selection.</summary>
    public static void Click(WorldUiState ui, WorldFrame frame, double sx, double sy) => ui.Selected = frame.Hits.HitTest(sx, sy);

    /// <summary>A selection that the current state no longer reports is kept (so it comes back
    /// if the entity does) but draws nothing; it is never re-targeted by index or proximity.</summary>
    private static void ForgetMissing(WorldUiState ui, WorldView view)
    {
        if (ui.Hovered is WorldEntityId h && !view.Contains(h)) ui.Hovered = null;
    }

    /// <summary>Rebuilds a view only when a source returns a different snapshot object.</summary>
    private sealed class ViewCache
    {
        private object?[] _seen = [];
        private WorldSources? _bundle;
        private WorldView? _view;

        public void Clear() { _seen = []; _bundle = null; _view = null; }

        public WorldView Get(WorldSources sources, WorldMorphology morph)
        {
            object?[] now =
            [
                sources.Settlements.Current, sources.Polities.Current, sources.Structures.Current, sources.Infrastructure.Nodes,
                sources.Infrastructure.Edges, sources.Resources.Current, sources.Agents.Current,
            ];
            bool same = _view is not null && ReferenceEquals(_bundle?.Settlements, sources.Settlements) && _seen.Length == now.Length;
            for (int i = 0; same && i < now.Length; i++) same = ReferenceEquals(_seen[i], now[i]);
            if (!same)
            {
                _view = WorldViewBuilder.Build(sources, morph);
                _seen = now;
                _bundle = sources;
            }
            return _view!;
        }
    }
}
