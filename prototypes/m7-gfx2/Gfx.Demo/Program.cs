using System.Globalization;
using Gfx.Engine.Diagnostics;
using Gfx.Engine.Hosting;
using Gfx.Engine.Rendering;
using Gfx.Engine.Scene;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;

namespace Gfx.Demo;

public static class Program
{
    const string Usage = @"Gfx.Demo — M7 graphics prototype G1
  --sysinfo                         GL vendor/renderer/version, CPU, RAM, OS, warnings
  --list                            registered demo scenes
  --capture <scene|all> [--out DIR] [--size WxH] [--no-shadows]
  --bench <scene> [--frames N] [--size WxH] [--no-shadows]
  --interactive [scene]             windowed; WASD pan, Q/E rotate, wheel zoom, F1..F12 switch scene, Esc quit
Headless on Linux: SDL_VIDEODRIVER=offscreen LIBGL_ALWAYS_SOFTWARE=1 (set automatically when no display).";

    public static int Main(string[] args)
    {
        CultureInfo.DefaultThreadCurrentCulture = CultureInfo.InvariantCulture;
        CultureInfo.CurrentCulture = CultureInfo.InvariantCulture;
        if (args.Length == 0 || args[0] is "-h" or "--help") { Console.WriteLine(Usage); return 0; }
        string Opt(string name, string def) { int i = Array.IndexOf(args, name); return i >= 0 && i + 1 < args.Length ? args[i + 1] : def; }
        bool shadows = Array.IndexOf(args, "--no-shadows") < 0;
        var size = Opt("--size", "1280x720").Split('x');
        int w = int.Parse(size[0], CultureInfo.InvariantCulture), h = int.Parse(size[1], CultureInfo.InvariantCulture);
        var scenes = SceneRegistry.Discover(typeof(Program).Assembly);

        switch (args[0])
        {
            case "--list":
                foreach (var s in scenes) Console.WriteLine($"{s.Name,-24} {s.Description}");
                return 0;
            case "--interactive":
                using (var g = new InteractiveGame(scenes, args.Length > 1 ? args[1] : null)) g.Run();
                return 0;
        }

        if (!GfxHost.TryGetShared(out var host, out var reason, typeof(Program).Assembly))
        {
            Console.Error.WriteLine("No graphics device: " + reason);
            return 2;
        }
        return host!.Invoke(gd =>
        {
            var info = SysInfo.Collect(gd);
            switch (args[0])
            {
                case "--sysinfo":
                    Console.Write(info.ToText());
                    foreach (var (name, err) in host.Effects.WarmupAll())
                        Console.WriteLine($"effect {name,-16} {(err == null ? "OK" : "FAILED " + err.Message)}");
                    return 0;
                case "--capture":
                {
                    string which = args.Length > 1 ? args[1] : "all";
                    string outDir = Opt("--out", Path.Combine(Directory.GetCurrentDirectory(), "captures"));
                    int rc = 0;
                    foreach (var e in scenes)
                        if (which == "all" || which == e.Name) rc |= CaptureScene(gd, host, e, outDir, w, h, shadows, info);
                    return rc;
                }
                case "--bench":
                {
                    var e = scenes.FirstOrDefault(s => s.Name == (args.Length > 1 ? args[1] : ""));
                    if (e == null) { Console.Error.WriteLine("unknown scene; --list"); return 1; }
                    return Bench(gd, host, e, int.Parse(Opt("--frames", "30"), CultureInfo.InvariantCulture), w, h, shadows, info);
                }
                default:
                    Console.Error.WriteLine(Usage); return 1;
            }
        });
    }

    static int CaptureScene(GraphicsDevice gd, GfxHost host, SceneRegistry.Entry e, string outDir, int w, int h, bool shadows, SysInfo info)
    {
        using var scene = e.Create();
        scene.Load(gd, host.Effects);
        using var renderer = new ForwardRenderer(gd, host.Effects);
        scene.Configure(renderer.Settings); renderer.Settings.Shadows = shadows;
        using var rt = new RenderTarget2D(gd, w, h, false, SurfaceFormat.Color, DepthFormat.None);
        foreach (var shot in scene.Shots)
        {
            scene.Update(shot.SceneTime);
            renderer.Stats.BeginFrame();
            renderer.Render(scene, shot.Camera, SunModel.At(shot.HourOfDay), rt, shot.SceneTime);
            var px = Capture.ReadPixels(rt); // sync
            double ms = renderer.Stats.EndFrame();
            string path = Path.Combine(outDir, $"{e.Name}-{shot.Name}.png");
            Capture.SavePng(rt, path);
            Console.WriteLine(string.Format(CultureInfo.InvariantCulture,
                "[capture] {0} {1}x{2} shadows={3} hdr={4} distinctColors={5} draws={6} tris={7} frame={8:F1} ms [{9}]",
                path, w, h, shadows, renderer.HdrFormat, Capture.DistinctColors(px), renderer.Stats.LastDrawCalls,
                renderer.Stats.LastTriangles, ms, info.TimingLabel));
        }
        return 0;
    }

    static int Bench(GraphicsDevice gd, GfxHost host, SceneRegistry.Entry e, int frames, int w, int h, bool shadows, SysInfo info)
    {
        using var scene = e.Create();
        scene.Load(gd, host.Effects);
        using var renderer = new ForwardRenderer(gd, host.Effects);
        scene.Configure(renderer.Settings); renderer.Settings.Shadows = shadows;
        using var rt = new RenderTarget2D(gd, w, h, false, SurfaceFormat.Color, DepthFormat.None);
        var shot = scene.Shots[0]; var sun = SunModel.At(shot.HourOfDay); var one = new Color[1];
        for (int f = -3; f < frames; f++)
        {
            float t = shot.SceneTime + Math.Max(f, 0) / 30f;
            if (f == 0) renderer.Stats.Reset();
            renderer.Stats.BeginFrame();
            scene.Update(t);
            renderer.Render(scene, shot.Camera, sun, rt, t);
            rt.GetData(0, new Rectangle(0, 0, 1, 1), one, 0, 1); // wait for the GPU (llvmpipe) to finish
            renderer.Stats.EndFrame();
        }
        Console.WriteLine($"[bench] scene={e.Name} {w}x{h} shadows={shadows} hdr={renderer.HdrFormat}");
        Console.WriteLine(renderer.Stats.Format(info.TimingLabel));
        return 0;
    }
}
