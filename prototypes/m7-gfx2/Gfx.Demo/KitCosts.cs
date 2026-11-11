using System.Diagnostics;
using System.Globalization;
using Gfx.Chars.Animation;
using Gfx.Chars.Format;
using Gfx.Chars.Rendering;
using Gfx.Demo.Scenes;
using Gfx.Engine.Assets;
using Gfx.Engine.Diagnostics;
using Gfx.Engine.Hosting;
using Gfx.Engine.Rendering;
using Gfx.Engine.Scene;
using Gfx.Gear.Anim;
using Gfx.Gear.Items;
using Gfx.Gear.Maps;
using Gfx.Gear.Rig;
using Gfx.Gear.Runtime;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;

namespace Gfx.Demo;

/// <summary>
/// <c>Gfx.Demo --kit-costs</c>: costs of the stage-kit layer on this machine. Generators: geometry alone and geometry
/// plus PBR maps for every item kind (the map painter runs one parallel loop per texel row, so these scale with the
/// core count, printed). Clip bake: every authored combat clip of one skeleton through the IK baker. Animation: one
/// animator step with an authored action and the hold constraint, per soldier per frame at 60 Hz, and the share of
/// the constraint; the full soldier update (step, pose solve, item transforms). Draws: the renderer's draw calls and
/// triangles per kitted soldier (body, garments, worn kit, two held props; main and shadow passes) from one frame of
/// kit-forces with and without its nine soldiers. Every number carries the machine's timing label (llvmpipe here:
/// not Victus evidence).
/// </summary>
public static class KitCosts
{
    static string F(double v, string f = "F2") => v.ToString(f, CultureInfo.InvariantCulture);
    static double Ms(long ticks) => ticks * 1000.0 / Stopwatch.Frequency;
    static double P(List<double> v, double p) { var s = v.OrderBy(x => x).ToList(); return s[Math.Min(s.Count - 1, (int)(s.Count * p))]; }

    sealed class TimedConstraint : IPoseConstraint
    {
        public required IPoseConstraint Inner;
        public long Ticks;
        public void Apply(CharRig rig, Pose pose, Quaternion[] global, Vector3[] heads, in Matrix world, float t)
        {
            long t0 = Stopwatch.GetTimestamp();
            Inner.Apply(rig, pose, global, heads, world, t);
            Ticks += Stopwatch.GetTimestamp() - t0;
        }
    }

    public static int Run(GraphicsDevice gd, GfxHost host, SysInfo info, IReadOnlyList<SceneRegistry.Entry> scenes)
    {
        string label = info.TimingLabel;
        string dir = AssetRoot.Baked("chars");
        if (!CharStage.BakeExists(dir)) { Console.Error.WriteLine("no character bake (scripts/bake-chars.sh)"); return 2; }
        Console.WriteLine($"[kit-costs] {label}; {Environment.ProcessorCount} logical cores (map painting is a parallel loop)");
        var idf = CharacterFile.Load(Path.Combine(dir, "a1.g2ch"));
        var sk = new Skeleton(idf.Bones);
        var m = KitSockets.Measure(idf, sk);

        // ---- 1. generators: geometry alone, then geometry + maps
        var gens = new List<(string name, Func<int, KitItem> build, int size)>
        {
            ("aspis (hero, 2048 maps)", n => Aspis.Build(Gfx.Gear.Bake.HeroBake.Spec with { MapSize = n }), 2048),
            ("aspis (line, 1024)", n => Aspis.Build(new AspisSpec { Seed = 11, MapSize = n, Device = "crescent" }), 1024),
        };
        foreach (var k in new[] { "thureos", "round", "kite", "heater" }) gens.Add(("shield " + k, n => Shields.Build(new ShieldSpec { Kind = k, Seed = 300, MapSize = n }), 1024));
        foreach (var k in new[] { "corinthian", "montefortino", "nasal", "kettle", "greathelm" }) gens.Add(("helmet " + k, n => Helmets.Build(new HelmetSpec { Kind = k, Seed = 100, MapSize = n, Crest = k is "corinthian" or "montefortino" ? "#7E2A20" : null }, m), 1024));
        foreach (var k in new[] { "spear", "xiphos", "longsword", "arming", "axe", "bow", "arrow", "quiver", "scabbard", "sling", "musket" }) gens.Add(("weapon " + k, n => Weapons.Build(new WeaponSpec { Kind = k, Seed = 200, MapSize = n }), 512));
        gens.Add(("banner", n => Banner.Build(new BannerSpec { Seed = 61, MapSize = n }), 1024));
        Console.WriteLine($"[kit-costs] {"item",-26} {"tris",7} {"geometry ms",12} {"+maps ms",10} {"maps",6}");
        MapSet? hero = null, line = null;
        double totalGeo = 0, totalAll = 0;
        foreach (var (name, build, size) in gens)
        {
            build(0); // warm (JIT)
            long t0 = Stopwatch.GetTimestamp(); var geo = build(0); double gms = Ms(Stopwatch.GetTimestamp() - t0);
            t0 = Stopwatch.GetTimestamp(); var full = build(size); double ams = Ms(Stopwatch.GetTimestamp() - t0);
            totalGeo += gms; totalAll += ams;
            if (name.StartsWith("aspis (hero")) hero = full.Maps.Values.First();
            if (name.StartsWith("aspis (line")) line = full.Maps.Values.First();
            Console.WriteLine($"[kit-costs] {name,-26} {geo.TriangleCount,7} {F(gms, "F1"),12} {F(ams, "F1"),10} {size,6}  [{label}]");
        }
        Console.WriteLine($"[kit-costs] all {gens.Count} generators: geometry {F(totalGeo, "F0")} ms, geometry + maps {F(totalAll, "F0")} ms  [{label}]");
        string glb = AssetRoot.Baked("kit/hero/" + Gfx.Gear.Bake.HeroBake.FileName);
        if (File.Exists(glb))
        {
            Gfx.Gear.Bake.HeroBake.Load(glb);
            long t0 = Stopwatch.GetTimestamp(); Gfx.Gear.Bake.HeroBake.Load(glb); double ms = Ms(Stopwatch.GetTimestamp() - t0);
            Console.WriteLine($"[kit-costs] hero glTF load (decode 3 x 2048 PNG + mesh): {F(ms, "F0")} ms, file {new FileInfo(glb).Length / 1024} KiB  [{label}]");
        }
        foreach (var (nm, ms0) in new (string, MapSet?)[] { ("2048", hero), ("1024", line) })
        {
            if (ms0 == null) continue;
            KitGpu.Upload(gd, ms0).Dispose();
            long t0 = Stopwatch.GetTimestamp(); var tx = KitGpu.Upload(gd, ms0); double ms = Ms(Stopwatch.GetTimestamp() - t0); tx.Dispose();
            Console.WriteLine($"[kit-costs] map set upload {nm} (base, normal, ORM with CPU mips): {F(ms, "F1")} ms  [{label}]");
        }

        // ---- 2. clip bake
        {
            var d = Dims.Of(sk);
            var sockets = KitSockets.Compute(idf, sk);
            var all = CombatClips.All(d);
            foreach (var a in all.Take(2)) ClipBaker.Bake(a, sk, sockets);
            long t0 = Stopwatch.GetTimestamp();
            int frames = 0;
            foreach (var a in all) frames += (int)MathF.Round(ClipBaker.Bake(a, sk, sockets).Duration * 30f) + 1;
            double ms = Ms(Stopwatch.GetTimestamp() - t0);
            Console.WriteLine($"[kit-costs] clip bake: {all.Count} authored clips, {frames} frames at 30 fps, {F(ms, "F0")} ms per skeleton ({F(ms * 1000 / frames, "F0")} us per frame)  [{label}]");
        }

        // ---- 3. animation and soldier update (kit-forces' nine soldiers)
        var entry = scenes.FirstOrDefault(e => e.Name == "kit-forces");
        if (entry == null) { Console.Error.WriteLine("kit-forces scene not registered"); return 1; }
        long l0 = Stopwatch.GetTimestamp();
        using var scene = entry.Create();
        scene.Load(gd, host.Effects);
        Console.WriteLine($"[kit-costs] kit-forces load (outfits, nine kits generated with maps, clip bakes, uploads): {F(Ms(Stopwatch.GetTimestamp() - l0), "F0")} ms  [{label}]");
        var forces = (KitForcesScene)scene;
        var step = new List<double>(); var hold = new List<double>(); var walkStep = new List<double>(); var walkHold = new List<double>();
        foreach (var s in forces.Soldiers)
        {
            var asset = s.Body.Asset;
            bool central = s.Kit.Mode == LeftHold.Central;
            bool sword = s.Kit.Right?.Kind == "weapon.longsword";
            string c = central ? ".c" : "";
            string[] acts = sword ? new[] { "kit.sword.cut_over.c", "kit.sword.cut_side.c", "kit.sword.block.c" } : new[] { "kit.hoplite.thrust" + c, "kit.hoplite.block" + c, "kit.hoplite.hit" + c };
            foreach (bool walk in new[] { false, true })
            {
                var tc = new TimedConstraint { Inner = s.Hold };
                var an = new CharAnimator(asset.ClipFile, asset.Skeleton);
                const float dt = 1f / 60f;
                for (int i = 0; i < 900; i++)
                {
                    float t = i * dt;
                    int which = (int)(t / 2.5f) % acts.Length;
                    var inp = walk
                        ? new AnimInput { Speed = 1.45f, UpperClip = sword ? "kit.sword.guard.c" : "kit.hoplite.guard" + c, UpperClipTime = t % 2f, UpperWeight = 1f, FootLock = true, Constraint = tc }
                        : new AnimInput { Speed = 0, Action = acts[which], ActionStart = MathF.Floor(t / 2.5f) * 2.5f, FootLock = false, Constraint = tc };
                    var world = walk ? Matrix.CreateTranslation(0, 0, 1.45f * t) : Matrix.Identity;
                    long tick0 = tc.Ticks, t0 = Stopwatch.GetTimestamp();
                    an.Step(t, dt, inp, world);
                    double us = Ms(Stopwatch.GetTimestamp() - t0) * 1000;
                    if (i < 60) continue;
                    (walk ? walkStep : step).Add(us);
                    (walk ? walkHold : hold).Add(Ms(tc.Ticks - tick0) * 1000);
                }
            }
        }
        Console.WriteLine($"[kit-costs] animator step, authored action + hold constraint: mean {F(step.Average())} us, p95 {F(P(step, 0.95))} us per soldier per frame; of which the constraint mean {F(hold.Average())} us, p95 {F(P(hold, 0.95))} us ({step.Count} steps, {forces.Soldiers.Count} soldiers)  [{label}]");
        Console.WriteLine($"[kit-costs] animator step, walk (motion-capture legs) + authored guard on the upper body + hold: mean {F(walkStep.Average())} us, p95 {F(P(walkStep, 0.95))} us; constraint mean {F(walkHold.Average())} us  [{label}]");
        var upd = new List<double>();
        for (int i = 0; i < 600; i++)
        {
            float t = 2f + i / 60f;
            foreach (var s in forces.Soldiers)
            {
                long t0 = Stopwatch.GetTimestamp();
                s.Update(t);
                if (i >= 30) upd.Add(Ms(Stopwatch.GetTimestamp() - t0) * 1000);
            }
        }
        Console.WriteLine($"[kit-costs] soldier update (guard loop step + hold, pose solve with skin matrices, item transforms): mean {F(upd.Average())} us, p95 {F(P(upd, 0.95))} us per soldier per frame  [{label}]");

        // ---- 4. draws per kitted soldier: one frame with and without the soldiers
        var shot = scene.Shots.First(x => x.Name == "lines-34");
        using var renderer = new ForwardRenderer(gd, host.Effects);
        scene.Configure(renderer.Settings);
        using var rt = new RenderTarget2D(gd, 1280, 720, false, SurfaceFormat.Color, DepthFormat.None);
        (int draws, long tris, double ms) Frame()
        {
            scene.Update(shot.SceneTime);
            for (int w = 0; w < 2; w++) { renderer.Render(scene, shot.Camera, SunModel.At(shot.HourOfDay), rt, shot.SceneTime); Capture.ReadPixels(rt); }
            var times = new List<double>(); int dc = 0; long tr = 0;
            for (int i = 0; i < 4; i++)
            {
                renderer.Stats.BeginFrame();
                renderer.Render(scene, shot.Camera, SunModel.At(shot.HourOfDay), rt, shot.SceneTime);
                Capture.ReadPixels(rt);
                times.Add(renderer.Stats.EndFrame()); dc = renderer.Stats.LastDrawCalls; tr = renderer.Stats.LastTriangles;
            }
            return (dc, tr, times.Average());
        }
        var with = Frame();
        var stage = forces.KitStage;
        var removed = new List<CharInstance>();
        foreach (var s in forces.Soldiers) { removed.Add(s.Body); if (s.RightProp != null) removed.Add(s.RightProp); if (s.LeftProp != null) removed.Add(s.LeftProp); }
        foreach (var r in removed) stage.Instances.Remove(r);
        stage.Invalidate();
        var without = Frame();
        stage.Instances.AddRange(removed); stage.Invalidate();
        int n = forces.Soldiers.Count;
        Console.WriteLine($"[kit-costs] kit-forces lines-34 1280x720 frame: {with.draws} draws, {with.tris} triangles, {F(with.ms, "F0")} ms with {n} kitted soldiers; {without.draws} draws, {without.tris} triangles, {F(without.ms, "F0")} ms without  [{label}]");
        Console.WriteLine($"[kit-costs] per kitted soldier (main + shadow passes): {F((with.draws - without.draws) / (double)n, "F1")} draws, {F((with.tris - without.tris) / (double)n, "F0")} triangles, {F((with.ms - without.ms) / n, "F1")} ms of frame time  [{label}]");
        return 0;
    }
}
