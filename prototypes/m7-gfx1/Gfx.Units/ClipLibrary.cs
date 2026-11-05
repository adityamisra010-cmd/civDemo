using Gfx.Engine.Rendering;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;

namespace Gfx.Units;

/// <summary>Clip ids (stable; index into <see cref="ClipLibrary.Clips"/>).</summary>
public enum ClipId { Idle = 0, Walk = 1, Run = 2, AttackMelee = 3, FireRanged = 4, HitReact = 5, Death = 6, RoutRun = 7 }

public sealed record ClipDef(ClipId Id, string Name, float Duration, bool Loop);

/// <summary>
/// Procedural keyframe clip library, authored as code (placeholder-grade motion; the production path is authored
/// clips retargeted to <see cref="Skeleton"/>). Each clip is a pure function (phase 0..1) → <see cref="Pose"/>.
/// <para>Bake: every clip is sampled at <see cref="FramesPerClip"/> frames into consecutive bone-texture rows.
/// Looping clips sample phase k/F (row after the last wraps to the first); one-shot clips sample k/(F−1) and clamp.
/// The GPU blends two rows per instance (matrix lerp, see <c>BoneTexture.SkinCpu</c>).</para>
/// </summary>
public static class ClipLibrary
{
    public const int FramesPerClip = 32;

    public static readonly ClipDef[] Clips =
    {
        new(ClipId.Idle, "idle", 2.4f, true),
        new(ClipId.Walk, "walk", 1.1f, true),
        new(ClipId.Run, "run", 0.7f, true),
        new(ClipId.AttackMelee, "attack-melee", 1.0f, true),
        new(ClipId.FireRanged, "fire-ranged", 1.6f, true),
        new(ClipId.HitReact, "hit-react", 0.6f, false),
        new(ClipId.Death, "death", 1.4f, false),
        new(ClipId.RoutRun, "rout-run", 0.6f, true),
    };

    public static int RowCount => Clips.Length * FramesPerClip;
    public static int FirstRow(ClipId c) => (int)c * FramesPerClip;

    /// <summary>Phase (0..1) of a clip at clip-local time t (looping wraps, one-shot clamps).</summary>
    public static float Phase(ClipId c, float t)
    {
        var d = Clips[(int)c];
        float p = t / d.Duration;
        if (d.Loop) { p -= MathF.Floor(p); return p; }
        return Math.Clamp(p, 0f, 1f);
    }

    /// <summary>Bone-texture rows and blend for clip-local time t (what the GPU interpolates).</summary>
    public static (int rowA, int rowB, float blend) Sample(ClipId c, float t)
    {
        var d = Clips[(int)c]; int first = FirstRow(c);
        float p = Phase(c, t);
        if (d.Loop)
        {
            float f = p * FramesPerClip; int k = Math.Min((int)f, FramesPerClip - 1);
            return (first + k, first + (k + 1) % FramesPerClip, f - k);
        }
        float g = p * (FramesPerClip - 1); int i = Math.Min((int)g, FramesPerClip - 2);
        return (first + i, first + i + 1, Math.Clamp(g - i, 0f, 1f));
    }

    /// <summary>Animation-LOD variant: snap to the nearest baked frame (no blending) — used at reduced update rate.</summary>
    public static (int rowA, int rowB, float blend) SampleStepped(ClipId c, float t)
    {
        var (a, b, w) = Sample(c, t);
        int r = w < 0.5f ? a : b;
        return (r, r, 0f);
    }

    /// <summary>Phase of baked frame k (inverse of the bake sampling).</summary>
    public static float FramePhase(ClipId c, int k) => Clips[(int)c].Loop ? k / (float)FramesPerClip : k / (float)(FramesPerClip - 1);

    /// <summary>All rows: poses[row][joint] = skin matrix. Input for <see cref="BoneTexture.Create"/>.</summary>
    public static List<Matrix[]> BakeRows()
    {
        var rows = new List<Matrix[]>(RowCount);
        foreach (var c in Clips)
            for (int k = 0; k < FramesPerClip; k++)
                rows.Add(Skeleton.SkinMatrices(Evaluate(c.Id, FramePhase(c.Id, k))));
        return rows;
    }

    public static BoneTexture Bake(GraphicsDevice gd) => BoneTexture.Create(gd, BakeRows(), Skeleton.JointCount);

    // ------------------------------------------------------------------ authoring helpers

    static float Rad(float deg) => deg * (MathF.PI / 180f);
    /// <summary>Hanging limb swings forward (+Z) by deg.</summary>
    static Matrix Fwd(float deg) => Matrix.CreateRotationX(-Rad(deg));
    /// <summary>Upright segment leans forward (+Z) by deg.</summary>
    static Matrix Lean(float deg) => Matrix.CreateRotationX(Rad(deg));
    static Matrix Twist(float deg) => Matrix.CreateRotationY(Rad(deg));
    /// <summary>Limb raised sideways (away from the body) by deg.</summary>
    static Matrix Out(float deg, bool left) => Matrix.CreateRotationZ(Rad(left ? deg : -deg));
    static float S(float p) => MathF.Sin(p * MathF.Tau);
    static float C(float p) => MathF.Cos(p * MathF.Tau);
    static float Smooth(float x) { x = Math.Clamp(x, 0f, 1f); return x * x * (3 - 2 * x); }

    /// <summary>Piecewise keys (phase, value) with smoothstep easing between keys.</summary>
    static float Key(float p, params (float t, float v)[] k)
    {
        if (p <= k[0].t) return k[0].v;
        for (int i = 1; i < k.Length; i++)
            if (p <= k[i].t) return k[i - 1].v + (k[i].v - k[i - 1].v) * Smooth((p - k[i - 1].t) / (k[i].t - k[i - 1].t));
        return k[^1].v;
    }

    static void Guard(Matrix[] l, float swing = 0)
    {
        l[Skeleton.LUpperArm] = Fwd(20 - swing) * Out(10, true);
        l[Skeleton.LForearm] = Fwd(70);
        l[Skeleton.RUpperArm] = Fwd(15 + swing) * Out(8, false);
        l[Skeleton.RForearm] = Fwd(60);
    }

    static void Legs(Matrix[] l, float p, float thigh, float knee)
    {
        float s = S(p);
        l[Skeleton.LThigh] = Fwd(thigh * s);
        l[Skeleton.RThigh] = Fwd(-thigh * s);
        l[Skeleton.LShin] = Fwd(-5 - knee * MathF.Max(0, -C(p + 0.1f)));
        l[Skeleton.RShin] = Fwd(-5 - knee * MathF.Max(0, C(p + 0.1f)));
        l[Skeleton.LFoot] = Fwd(-0.3f * thigh * s);
        l[Skeleton.RFoot] = Fwd(0.3f * thigh * s);
    }

    /// <summary>Pose of clip <paramref name="c"/> at phase <paramref name="p"/> (0..1).</summary>
    public static Pose Evaluate(ClipId c, float p)
    {
        var pose = Pose.Rest(); var l = pose.Local; var root = Vector3.Zero;
        switch (c)
        {
            case ClipId.Idle:
                Guard(l, 2 * S(p));
                l[Skeleton.Chest] = Lean(2 + 1.5f * S(p));
                l[Skeleton.Head] = Twist(6 * C(p));
                l[Skeleton.LThigh] = Fwd(4); l[Skeleton.RThigh] = Fwd(-3);
                l[Skeleton.LShin] = Fwd(-6); l[Skeleton.RShin] = Fwd(-3);
                root.Y = -0.01f * (1 + S(p));
                break;
            case ClipId.Walk:
                Legs(l, p, 25, 35);
                Guard(l, 6 * S(p));
                l[Skeleton.Spine] = Lean(4) * Twist(4 * S(p));
                root.Y = -0.03f * MathF.Abs(S(p));
                break;
            case ClipId.Run:
                Legs(l, p, 40, 75);
                Guard(l, 12 * S(p));
                l[Skeleton.Spine] = Lean(12) * Twist(7 * S(p));
                root.Y = -0.06f * MathF.Abs(S(p)) - 0.03f;
                break;
            case ClipId.AttackMelee:
            {
                float up = Key(p, (0f, 15), (0.38f, -40), (0.55f, 95), (0.72f, 70), (1f, 15));
                float fore = Key(p, (0f, 60), (0.38f, 85), (0.55f, 10), (0.72f, 25), (1f, 60));
                float tw = Key(p, (0f, 0), (0.38f, 22), (0.55f, -25), (1f, 0));
                float lean = Key(p, (0f, 2), (0.4f, -4), (0.55f, 14), (1f, 2));
                Guard(l);
                l[Skeleton.RUpperArm] = Fwd(up) * Out(20, false);
                l[Skeleton.RForearm] = Fwd(fore);
                l[Skeleton.Chest] = Twist(tw);
                l[Skeleton.Spine] = Lean(lean);
                float step = Key(p, (0f, 0), (0.45f, 22), (0.75f, 22), (1f, 0));
                l[Skeleton.LThigh] = Fwd(step); l[Skeleton.LShin] = Fwd(-step * 0.8f);
                l[Skeleton.RThigh] = Fwd(-step * 0.5f); l[Skeleton.RShin] = Fwd(-8);
                root.Y = -0.04f * step / 22f;
                break;
            }
            case ClipId.FireRanged:
            {
                float raise = Key(p, (0f, 0), (0.18f, 1), (0.62f, 1), (0.7f, 0.85f), (1f, 0));
                float draw = Key(p, (0.12f, 0), (0.5f, 1), (0.6f, 1), (0.7f, 0), (1f, 0));
                l[Skeleton.LUpperArm] = Fwd(20 + 65 * raise) * Out(10, true);
                l[Skeleton.LForearm] = Fwd(70 - 62 * raise);
                l[Skeleton.RUpperArm] = Fwd(15 + 65 * raise) * Out(8 + 22 * draw, false);
                l[Skeleton.RForearm] = Fwd(60 + 70 * draw);
                l[Skeleton.Chest] = Twist(-18 * raise);
                l[Skeleton.Head] = Twist(14 * raise);
                l[Skeleton.LThigh] = Fwd(10); l[Skeleton.RThigh] = Fwd(-10); l[Skeleton.LShin] = Fwd(-5);
                break;
            }
            case ClipId.HitReact:
            {
                float k = Key(p, (0f, 0), (0.2f, 1), (1f, 0));
                Guard(l);
                l[Skeleton.LUpperArm] = Fwd(20 - 15 * k) * Out(10 + 25 * k, true);
                l[Skeleton.RUpperArm] = Fwd(15 - 15 * k) * Out(8 + 25 * k, false);
                l[Skeleton.Spine] = Lean(-14 * k);
                l[Skeleton.Chest] = Lean(-8 * k) * Twist(10 * k);
                l[Skeleton.Head] = Lean(-15 * k);
                l[Skeleton.LShin] = Fwd(-15 * k); l[Skeleton.RShin] = Fwd(-15 * k);
                l[Skeleton.LThigh] = Fwd(8 * k); l[Skeleton.RThigh] = Fwd(8 * k);
                root = new Vector3(0, -0.04f * k, -0.10f * k);
                break;
            }
            case ClipId.Death:
            {
                float buckle = Key(p, (0f, 0), (0.3f, 1), (0.7f, 1), (1f, 0.2f));
                float fall = Key(p, (0.2f, 0), (0.85f, 90));
                float arms = Key(p, (0f, 0), (0.6f, 1));
                l[Skeleton.LThigh] = Fwd(45 * buckle); l[Skeleton.RThigh] = Fwd(38 * buckle);
                l[Skeleton.LShin] = Fwd(-70 * buckle); l[Skeleton.RShin] = Fwd(-60 * buckle);
                l[Skeleton.LUpperArm] = Fwd(20 * (1 - arms)) * Out(10 + 45 * arms, true);
                l[Skeleton.RUpperArm] = Fwd(15 * (1 - arms)) * Out(8 + 35 * arms, false);
                l[Skeleton.LForearm] = Fwd(70 * (1 - arms) + 10); l[Skeleton.RForearm] = Fwd(60 * (1 - arms) + 15);
                l[Skeleton.Head] = Lean(-12 * arms) * Twist(20 * arms);
                l[Skeleton.Root] = Lean(-fall); // pivot at the feet: falls backward onto the back
                root = new Vector3(0, -0.28f * buckle * (1 - fall / 90f) + 0.13f * (fall / 90f), -0.25f * (fall / 90f));
                break;
            }
            case ClipId.RoutRun:
                Legs(l, p, 42, 80);
                l[Skeleton.LUpperArm] = Fwd(-45 * S(p)) * Out(20, true);
                l[Skeleton.RUpperArm] = Fwd(45 * S(p)) * Out(20, false);
                l[Skeleton.LForearm] = Fwd(55); l[Skeleton.RForearm] = Fwd(55);
                l[Skeleton.Spine] = Lean(20) * Twist(8 * S(p));
                l[Skeleton.Head] = Twist(30 * C(p)) * Lean(-10);
                root.Y = -0.07f * MathF.Abs(S(p)) - 0.03f;
                break;
        }
        return new Pose(l, root);
    }
}
