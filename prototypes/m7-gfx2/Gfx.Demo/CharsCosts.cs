using System.Diagnostics;
using System.Globalization;
using Gfx.Chars.Animation;
using Gfx.Chars.Format;
using Gfx.Chars.Rendering;
using Gfx.Engine.Assets;
using Gfx.Engine.Diagnostics;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;

namespace Gfx.Demo;

/// <summary>
/// <c>Gfx.Demo --chars-costs</c>: CPU costs of the stage-chars character layer on this machine. Animation: one
/// CharAnimator step (locomotion blend, crossfade, masked upper-body layer, stance IK, additive hit, head look, foot IK
/// with locking) per character per frame at 60 Hz. Skinning prep: forward kinematics and skin matrices for one instance.
/// Expressions: the CPU blend of the five expression shapes into an instance's morph stream plus its upload, when the
/// weights change. Every number is labelled with the machine's timing label (llvmpipe here: not Victus evidence).
/// </summary>
public static class CharsCosts
{
    static string F(double v) => v.ToString("F3", CultureInfo.InvariantCulture);

    public static int Run(GraphicsDevice gd, SysInfo info)
    {
        string dir = AssetRoot.Baked("chars");
        if (!CharStage.BakeExists(dir)) { Console.Error.WriteLine("no character bake (scripts/bake-chars.sh)"); return 2; }
        string[] ids = { "avatar", "commander_a", "a1", "a2", "a3", "a4", "b1", "b2", "b3", "b4" };
        Console.WriteLine($"[chars-costs] {info.TimingLabel}");
        // 1. animation step per character per frame
        var all = new List<double>();
        var lean = new List<double>();
        foreach (var id in ids)
        {
            var idf = CharacterFile.Load(Path.Combine(dir, id + ".g2ch"));
            var f = CharacterFile.Load(Path.Combine(dir, id + "__clips.g2ch"));
            var s = new Skeleton(idf.Bones);
            foreach (bool full in new[] { true, false })
            {
                var a = new CharAnimator(f, s);
                const float dt = 1f / 60f;
                for (int i = 0; i < 1200; i++)
                {
                    float t = i * dt;
                    var inp = full
                        ? new AnimInput { Speed = t < 10f ? 1.45f : 0f, Stance = "spear", UpperClip = "idle", UpperClipTime = 1f, UpperWeight = 0.7f,
                            LookAt = new Vector3(2f, 1.6f, 3f + t), HitTime = 4f, Action = t > 15f ? "thrust" : null, ActionStart = 15f }
                        : new AnimInput { Speed = t < 10f ? 1.45f : 0f, FootLock = false };
                    var world = Matrix.CreateTranslation(0, 0, 1.45f * MathF.Min(t, 10f));
                    long t0 = Stopwatch.GetTimestamp();
                    a.Step(t, dt, inp, world);
                    double us = (Stopwatch.GetTimestamp() - t0) * 1e6 / Stopwatch.Frequency;
                    if (i >= 60) (full ? all : lean).Add(us);
                }
            }
        }
        all.Sort(); lean.Sort();
        // planted-foot slide (m/s, horizontal, world) for walk and run speeds: moving root (the in-place clip plus the
        // root's travel, stride-synchronised), before and after foot locking, and a static root for comparison
        foreach (float v in new[] { 1.45f, 3.6f })
            foreach (bool moving in new[] { true, false })
            {
                var before = new List<float>(); var after = new List<float>();
                foreach (var id in ids)
                {
                    var idf = CharacterFile.Load(Path.Combine(dir, id + ".g2ch"));
                    var f = CharacterFile.Load(Path.Combine(dir, id + "__clips.g2ch"));
                    var a = new CharAnimator(f, new Skeleton(idf.Bones));
                    const float dt = 1f / 60f;
                    for (int i = 0; i < 600; i++)
                    {
                        float t = i * dt;
                        a.Step(t, dt, new AnimInput { Speed = v }, moving ? Matrix.CreateTranslation(0, 0, v * t) : Matrix.Identity);
                    }
                    before.Add(a.Slide.MeanBefore); after.Add(a.Slide.MeanAfter);
                }
                Console.WriteLine($"[chars-costs] planted-foot slide at {v:F2} m/s, {(moving ? "moving root" : "static root (in-place playback)")}: before locking mean {F(before.Average() * 100)} cm/s (worst identity {F(before.Max() * 100)}), after locking mean {F(after.Average() * 100)} cm/s (worst {F(after.Max() * 100)})");
            }
        Console.WriteLine($"[chars-costs] animation step, every layer: mean {F(all.Average())} us, p50 {F(all[all.Count / 2])} us, p95 {F(all[(int)(all.Count * 0.95)])} us per character per frame ({all.Count} steps, 10 identities)");
        Console.WriteLine($"[chars-costs] animation step, locomotion only (no IK, layers or look): mean {F(lean.Average())} us, p95 {F(lean[(int)(lean.Count * 0.95)])} us");
        // 2. FK + skin matrices, 3. expression blend + upload (needs the GPU for the morph stream)
        using var tex = new TextureCache(gd);
        var assets = ids.Select(id => CharAsset.Load(gd, Path.Combine(dir, id + ".g2ch"), tex.Get)).ToList();
        var fk = new List<double>(); var morph = new List<double>(); var verts = new List<int>();
        string[] expr = { "battlecry", "pain", "grim", "calm", "blink" };
        foreach (var asset in assets)
        {
            using var inst = new CharInstance(asset);
            for (int i = 0; i < 200; i++)
            {
                inst.Pose.Local[asset.Skeleton["spine_02"]] = Quaternion.CreateFromAxisAngle(Vector3.UnitY, 0.01f * i);
                long t0 = Stopwatch.GetTimestamp();
                inst.SolvePose();
                fk.Add((Stopwatch.GetTimestamp() - t0) * 1e6 / Stopwatch.Frequency);
                inst.Expression.Clear();
                inst.Expression[expr[i % 5]] = 0.5f + 0.5f * MathF.Sin(i * 0.3f);
                inst.Expression["blink"] = (i % 7) / 7f;
                inst.UpdateMorphs(gd);
                if (i >= 10) { morph.Add(inst.LastMorphMs * 1000); verts.Add(inst.LastMorphVertices); }
            }
        }
        fk.Sort(); morph.Sort();
        Console.WriteLine($"[chars-costs] forward kinematics + 55 skin matrices: mean {F(fk.Average())} us, p95 {F(fk[(int)(fk.Count * 0.95)])} us per instance");
        Console.WriteLine($"[chars-costs] expression blend (5 shapes, CPU) + morph-stream upload: mean {F(morph.Average())} us, p95 {F(morph[(int)(morph.Count * 0.95)])} us per instance per change; {verts.Average():F0} morphed vertices on average");
        foreach (var a in assets) a.Dispose();
        return 0;
    }
}
