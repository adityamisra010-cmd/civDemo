using System.Diagnostics;
using System.Globalization;
using System.Text;
using ImGuiNET;
using Sim.Core.Kernel;
using Sim.Core.State;
using Sim.Ui.ImGuiIntegration;
using Sim.Ui.Progression;
using Sim.Ui.Render;
using Sim.Ui.Theme;
using Sim.Ui.ViewModel;

namespace Sim.Ui.Headless;

/// <summary>
/// <c>Sim.Ui --frame-cost</c> (M5 hardening H1): the CPU cost of one Research-screen frame through the real path —
/// ProgressionScreen.Paint → DrawListImGuiBackend replay into ImGui's background list → ImGui.Render — with the
/// off-screen cull OFF (before) and ON (after), at A1 and A9, both trees, at the game's window and a maximised one.
/// Each sample is a sweep of frames (the tree as it opens, scrolled top to bottom at zoom 1, fitted); OFF and ON
/// alternate sweep by sweep after a warm-up so neither gets the JIT or the cache. Wall-clock: the machine's load
/// moves it, so the table reports the median of the per-sweep means.
/// </summary>
public static class FrameCost
{
    public static string Run(string assetsRoot, int rounds = 5)
    {
        UiSession s = UiSession.Start(42);
        var sb = new StringBuilder();
        sb.Append("| window | Age | tree | cull | frames/sweep | median ms/frame | mean vertices | max vertices | commands/frame | culled/frame |\n");
        sb.Append("|---|---|---|---|---|---|---|---|---|---|\n");
        foreach ((int w, int h) in new[] { (1280, 800), (1920, 1009) })
        {
            using var gui = new HeadlessImGui(assetsRoot, w, h);
            var backend = new DrawListImGuiBackend(gui.Fonts);
            foreach (int age in new[] { 1, 9 })
                foreach (TreeTab tab in new[] { TreeTab.Technology, TreeTab.Civics })
                {
                    WorldState world = EraPreview.WorldAt(s.World, s.Config.Ages!, UiPlayer.Empire, age);
                    var screen = new ProgressionScreen(s.Config.Research!, UiPlayer.Empire)
                    {
                        Theme = EraThemes.For(UiEras.Of(world, s.Config.Ages, UiPlayer.Empire)),
                    };
                    screen.Refresh(world);
                    screen.Tab = tab;
                    Sweep(gui, backend, screen, w, h, cull: true);   // warm-up
                    Sweep(gui, backend, screen, w, h, cull: false);
                    var off = new List<Sample>();
                    var on = new List<Sample>();
                    for (int r = 0; r < rounds; r++)
                    {
                        off.Add(Sweep(gui, backend, screen, w, h, cull: false));
                        on.Add(Sweep(gui, backend, screen, w, h, cull: true));
                    }
                    Row(sb, w, h, age, tab, "off (before)", off);
                    Row(sb, w, h, age, tab, "on (after)", on);
                }
        }
        return sb.ToString();
    }

    private sealed record Sample(int Frames, double MeanMs, double MeanVertices, int MaxVertices, double Commands, double Culled);

    private static Sample Sweep(HeadlessImGui gui, DrawListImGuiBackend backend, ProgressionScreen screen, int w, int h, bool cull)
    {
        backend.Cull = cull;
        screen.ResetView();
        screen.Camera.Set(1.0, 0, 0);
        int frames = 0, maxV = 0;
        long vertices = 0, commands = 0, culled = 0;
        double ms = 0;
        void Frame(Action? arrange)
        {
            arrange?.Invoke();
            screen.Camera.Set(screen.Camera.TargetZoom, screen.Camera.TargetPanX, screen.Camera.TargetPanY);
            long t0 = Stopwatch.GetTimestamp();
            gui.BeginFrame(default, default, 1.0 / 60);
            DrawList list = screen.Paint(w, h, backend);
            backend.Render(ImGui.GetBackgroundDrawList(), list);
            ImGuiDrawReport r = gui.EndFrame();
            ms += Stopwatch.GetElapsedTime(t0).TotalMilliseconds;
            frames++;
            vertices += r.TotalVertices;
            maxV = Math.Max(maxV, r.MaxListVertices);
            commands += list.Commands.Count;
            culled += backend.LastCulled;
            if (!r.Ok) throw new InvalidOperationException("frame-cost: draw data violation: " + string.Join("; ", r.Violations));
        }
        Frame(null);
        double bottom = Math.Max(0, screen.Layout.Height - screen.Canvas.H);
        for (int k = 0; k < 60 && screen.Camera.TargetPanY < bottom - 1; k++) Frame(() => screen.ScrollBy(0, 300));
        Frame(() => screen.FitAll());
        return new Sample(frames, ms / frames, vertices / (double)frames, maxV, commands / (double)frames, culled / (double)frames);
    }

    private static void Row(StringBuilder sb, int w, int h, int age, TreeTab tab, string cull, List<Sample> samples)
    {
        var means = new List<double>();
        foreach (Sample x in samples) means.Add(x.MeanMs);
        means.Sort();
        double median = means[means.Count / 2];
        Sample a = samples[0];
        int maxV = 0;
        foreach (Sample x in samples) maxV = Math.Max(maxV, x.MaxVertices);
        sb.Append(string.Create(CultureInfo.InvariantCulture,
            $"| {w}x{h} | A{age} | {tab} | {cull} | {a.Frames} | {median:0.00} | {a.MeanVertices:0} | {maxV} | {a.Commands:0} | {a.Culled:0.0} |\n"));
    }
}
