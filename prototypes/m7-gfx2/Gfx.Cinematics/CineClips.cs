using Gfx.Units;
using Microsoft.Xna.Framework;

namespace Gfx.Cinematics;

/// <summary>
/// Cinematic clip library on the canonical <see cref="Skeleton"/> (faces +Z, left = +X), authored as code —
/// PLACEHOLDER motion. The production path is authored/mocap clips retargeted to the same skeleton; selection,
/// blending and staging (this lane's subject) stay the same. Clips are in place: the player moves the root.
/// Every clip is a pure function (clip-local seconds, beat duration) → <see cref="Pose"/>.
/// </summary>
public static class CineClips
{
    static float Rad(float d) => d * (MathF.PI / 180f);
    static Matrix Fwd(float d) => Matrix.CreateRotationX(-Rad(d));   // hanging limb swings forward
    static Matrix Lean(float d) => Matrix.CreateRotationX(Rad(d));   // upright segment leans forward
    static Matrix Twist(float d) => Matrix.CreateRotationY(Rad(d));
    static Matrix Out(float d, bool left) => Matrix.CreateRotationZ(Rad(left ? d : -d)); // limb away from body
    static float Smooth(float x) { x = Math.Clamp(x, 0f, 1f); return x * x * (3 - 2 * x); }
    static float Key(float p, params (float t, float v)[] k)
    {
        if (p <= k[0].t) return k[0].v;
        for (int i = 1; i < k.Length; i++)
            if (p <= k[i].t) return k[i - 1].v + (k[i].v - k[i - 1].v) * Smooth((p - k[i - 1].t) / (k[i].t - k[i - 1].t));
        return k[^1].v;
    }

    static void Arms(Matrix[] l, float outDeg = 7, float fwd = 3, float elbow = 8)
    {
        l[Skeleton.LUpperArm] = Fwd(fwd) * Out(outDeg, true); l[Skeleton.RUpperArm] = Fwd(fwd) * Out(outDeg, false);
        l[Skeleton.LForearm] = Fwd(elbow); l[Skeleton.RForearm] = Fwd(elbow);
    }

    static void Walk(Matrix[] l, float t, float period, float stride = 26, float armSwing = 20)
    {
        float p = t / period; p -= MathF.Floor(p); float s = MathF.Sin(p * MathF.Tau), c = MathF.Cos(p * MathF.Tau);
        l[Skeleton.LThigh] = Fwd(stride * s); l[Skeleton.RThigh] = Fwd(-stride * s);
        l[Skeleton.LShin] = Fwd(-6 - 30 * MathF.Max(0, -c)); l[Skeleton.RShin] = Fwd(-6 - 30 * MathF.Max(0, c));
        l[Skeleton.LUpperArm] = Fwd(-armSwing * s) * Out(6, true); l[Skeleton.RUpperArm] = Fwd(armSwing * s) * Out(6, false);
        l[Skeleton.LForearm] = Fwd(12); l[Skeleton.RForearm] = Fwd(12);
    }

    /// <summary>Root drop at a full kneel: knee joint 0.50 m minus the 0.07 m knee probe radius (matches <see cref="GroundRootY"/>).</summary>
    const float KneelDrop = 0.43f;

    // bind-space contact probes on the lower body (joint, point): knee fronts and the boot box corners (AvatarMesh boots)
    static readonly (int j, Vector3 p)[] Probes = BuildProbes();
    static (int, Vector3)[] BuildProbes()
    {
        var l = new List<(int, Vector3)>();
        foreach (var (shin, foot, x) in new[] { (Skeleton.LShin, Skeleton.LFoot, 0.10f), (Skeleton.RShin, Skeleton.RFoot, -0.10f) })
        {
            for (int a = 0; a < 8; a++) l.Add((shin, new Vector3(x, 0.50f, 0) + 0.07f * new Vector3(0, MathF.Cos(a * MathF.PI / 4), MathF.Sin(a * MathF.PI / 4))));
            foreach (float y in new[] { 0f, 0.09f }) foreach (float z in new[] { -0.065f, 0.155f }) l.Add((foot, new Vector3(x, y, z)));
        }
        return l.ToArray();
    }

    /// <summary>Root height that puts the lowest lower-body contact probe on the ground (≥ the given floor).</summary>
    static float GroundRootY(Matrix[] l, Vector3 root)
    {
        Span<Matrix> model = stackalloc Matrix[Skeleton.JointCount];
        Skeleton.ModelTransforms(new Pose(l, new Vector3(root.X, 0, root.Z)), model);
        float lo = float.MaxValue;
        foreach (var (j, p) in Probes) lo = MathF.Min(lo, Vector3.Transform(p - Skeleton.Bind[j], model[j]).Y);
        return -lo;
    }

    static void Kneel(Matrix[] l, ref Vector3 root, float k, bool both)
    {
        // the shin folds back to lie on the ground; the foot points back with its top on the ground (not toes-down into it)
        l[Skeleton.LShin] = Fwd(-90 * k); l[Skeleton.LFoot] = Fwd(-90 * k);
        if (both) { l[Skeleton.RShin] = Fwd(-90 * k); l[Skeleton.RFoot] = Fwd(-90 * k); }
        else { l[Skeleton.RThigh] = Fwd(90 * k); l[Skeleton.RShin] = Fwd(-90 * k); }
        root.Y = GroundRootY(l, root); // the lowest knee/boot point rests on the ground through the whole transition
    }

    /// <summary>Pose of <paramref name="clip"/> at clip-local time <paramref name="t"/> (s) within a beat of <paramref name="duration"/> (s).</summary>
    public static Pose Sample(string clip, float t, float duration)
    {
        var pose = Pose.Rest(); var l = pose.Local; var root = Vector3.Zero;
        float u = duration > 0 ? Math.Clamp(t / duration, 0f, 1f) : 1f;
        float breathe = MathF.Sin(t * 1.7f);
        Arms(l);
        switch (clip)
        {
            case "stand-proud":
                l[Skeleton.Chest] = Lean(-4 + breathe); l[Skeleton.Head] = Lean(-5) * Twist(4 * MathF.Sin(t * 0.6f));
                break;
            case "stand-wary":
                root.Y = -0.02f; l[Skeleton.Chest] = Lean(6 + breathe); Arms(l, 10, 12, 25);
                l[Skeleton.LThigh] = Fwd(8); l[Skeleton.LShin] = Fwd(-10);
                break;
            case "walk-in":
                Walk(l, t, 1.05f); l[Skeleton.Chest] = Lean(3);
                break;
            case "escort-walk":
                Walk(l, t, 1.3f, 20, 8); l[Skeleton.RUpperArm] = Fwd(35) * Out(5, false); l[Skeleton.RForearm] = Fwd(30);
                break;
            case "bound-walk":
            case "led-captive":
            {
                bool led = clip == "led-captive";
                Walk(l, t, led ? 1.5f : 1.3f, led ? 16 : 20, 0);
                l[Skeleton.LUpperArm] = Fwd(-30) * Out(4, true); l[Skeleton.RUpperArm] = Fwd(-30) * Out(4, false);
                l[Skeleton.LForearm] = Out(-75, true); l[Skeleton.RForearm] = Out(-75, false);
                l[Skeleton.Neck] = Lean(led ? 30 : 18); l[Skeleton.Spine] = Lean(led ? 10 : 4);
                break;
            }
            case "kneel-one":
            case "kneel-both":
            {
                bool both = clip == "kneel-both";
                float k = Key(u, (0.05f, 0), (0.4f, 1));
                Kneel(l, ref root, k, both);
                l[Skeleton.Spine] = Lean((both ? 18 : 8) * k); l[Skeleton.Neck] = Lean(30 * Key(u, (0.3f, 0), (0.6f, 1)));
                if (!both) { l[Skeleton.RUpperArm] = Fwd(35 * k) * Out(6, false); l[Skeleton.RForearm] = Fwd(30 * k); }
                else Arms(l, 6, 25 * k, 20 * k);
                break;
            }
            case "kneel-fall":
            {
                float k = Key(u, (0.0f, 0), (0.25f, 1));
                Kneel(l, ref root, k, both: true);
                l[Skeleton.Spine] = Lean(15 * k); l[Skeleton.Neck] = Lean(35 * k);
                float f = Key(u, (0.6f, 0), (0.85f, 1));
                if (f > 0)
                {
                    // fall forward pivoting about the knee line on the ground: the root tips, the shins counter-rotate so
                    // they stay flat, and the root shifts so the knees stay where they were (no sinking, no levitation)
                    var tip = Lean(80 * f);
                    l[Skeleton.Root] = tip;
                    l[Skeleton.LShin] = Fwd(-90 + 80 * f); l[Skeleton.RShin] = Fwd(-90 + 80 * f);
                    l[Skeleton.Spine] = Lean(15 * k * (1 - f)); l[Skeleton.Neck] = Lean(35 * k * (1 - f)) * Twist(-50 * f) * Out(25 * f, false);
                    var knee = new Vector3(0, Skeleton.Bind[Skeleton.LShin].Y, 0);
                    root = new Vector3(0, Skeleton.Bind[Skeleton.LShin].Y - KneelDrop, 0) - Vector3.Transform(knee, tip) + new Vector3(0, 0.12f * f, 0);
                    Arms(l, 20 * f + 6, -10 * f, 10);
                }
                break;
            }
            case "handshake":
            {
                float r = Key(u, (0.1f, 0), (0.35f, 1), (0.85f, 1), (0.98f, 0));
                float shake = u > 0.4f && u < 0.75f ? 6 * MathF.Sin(t * 18f) : 0;
                l[Skeleton.RUpperArm] = Fwd(42 * r) * Out(4, false); l[Skeleton.RForearm] = Fwd(30 * r + shake);
                l[Skeleton.Spine] = Lean(6 * r);
                break;
            }
            case "clasp-bow":
            {
                float b = Key(u, (0.15f, 0), (0.45f, 1), (0.8f, 1), (1f, 0.3f));
                l[Skeleton.Spine] = Lean(22 * b); l[Skeleton.Chest] = Lean(8 * b); l[Skeleton.Neck] = Lean(15 * b);
                l[Skeleton.LUpperArm] = Fwd(30 * b) * Out(-6 * b, true); l[Skeleton.RUpperArm] = Fwd(30 * b) * Out(-6 * b, false);
                l[Skeleton.LForearm] = Fwd(70 * b) * Out(-20 * b, true); l[Skeleton.RForearm] = Fwd(70 * b) * Out(-20 * b, false);
                break;
            }
            case "strike-overhead":
            {
                float raise = Key(u, (0.05f, 0), (0.35f, 170), (0.52f, 170), (0.6f, 55), (1f, 40));
                l[Skeleton.RUpperArm] = Fwd(raise) * Out(8, false); l[Skeleton.RForearm] = Fwd(Key(u, (0.35f, 20), (0.6f, 5)));
                l[Skeleton.LUpperArm] = Fwd(raise * 0.9f) * Out(10, true); l[Skeleton.LForearm] = Fwd(25);
                l[Skeleton.Spine] = Lean(Key(u, (0.35f, -8), (0.6f, 18), (1f, 10)));
                l[Skeleton.LThigh] = Fwd(Key(u, (0.45f, 0), (0.6f, 22))); l[Skeleton.LShin] = Fwd(-Key(u, (0.45f, 0), (0.6f, 20)));
                break;
            }
            case "strike-side":
            {
                float raise = Key(u, (0.05f, 0), (0.35f, 95), (0.52f, 95), (0.62f, 70));
                float twist = Key(u, (0.05f, 0), (0.35f, -50), (0.52f, -55), (0.62f, 45), (1f, 35));
                l[Skeleton.Chest] = Twist(twist); l[Skeleton.RUpperArm] = Out(raise, false) * Fwd(Key(u, (0.52f, 0), (0.62f, 40)));
                l[Skeleton.RForearm] = Fwd(15); l[Skeleton.Spine] = Lean(Key(u, (0.5f, 0), (0.62f, 12)));
                break;
            }
            case "victory-raise-arm":
            {
                float r = Key(u, (0.08f, 0), (0.35f, 1));
                l[Skeleton.RUpperArm] = Fwd(165 * r) * Out(12, false); l[Skeleton.RForearm] = Fwd(10);
                l[Skeleton.Chest] = Lean(-6 * r + breathe); l[Skeleton.Head] = Lean(-12 * r);
                break;
            }
            case "victory-raise-both":
            {
                float r = Key(u, (0.08f, 0), (0.35f, 1));
                l[Skeleton.LUpperArm] = Out(150 * r + 7, true); l[Skeleton.RUpperArm] = Out(150 * r + 7, false);
                l[Skeleton.LForearm] = Fwd(10); l[Skeleton.RForearm] = Fwd(10);
                l[Skeleton.Chest] = Lean(-8 * r); l[Skeleton.Head] = Lean(-15 * r);
                break;
            }
            case "bow-deep":
            {
                float b = Key(u, (0.1f, 0), (0.45f, 1));
                l[Skeleton.Spine] = Lean(38 * b); l[Skeleton.Chest] = Lean(10 * b); l[Skeleton.Neck] = Lean(18 * b);
                Arms(l, 5, 36 * b, 10);
                break;
            }
            case "bow-head":
            {
                float b = Key(u, (0.1f, 0), (0.4f, 1));
                l[Skeleton.Neck] = Lean(32 * b); l[Skeleton.Chest] = Lean(10 * b); Arms(l, 5, 6 * b, 10);
                break;
            }
            case "standoff-arms-crossed":
                l[Skeleton.LUpperArm] = Fwd(35) * Out(6, true); l[Skeleton.RUpperArm] = Fwd(35) * Out(6, false);
                l[Skeleton.LForearm] = Out(-82, true) * Fwd(15); l[Skeleton.RForearm] = Out(-78, false) * Fwd(25);
                l[Skeleton.Chest] = Lean(-3 + breathe); l[Skeleton.Head] = Twist(8 * MathF.Sin(t * 0.5f));
                break;
            case "standoff-hold":
                l[Skeleton.LUpperArm] = Out(28, true); l[Skeleton.RUpperArm] = Out(28, false);
                l[Skeleton.LForearm] = Out(-70, true); l[Skeleton.RForearm] = Out(-70, false);
                l[Skeleton.LThigh] = Out(6, true); l[Skeleton.RThigh] = Out(6, false); l[Skeleton.Chest] = Lean(breathe);
                break;
            case "":
                break;
            default:
                throw new ArgumentException($"unknown clip '{clip}'");
        }
        return new Pose(l, root);
    }
}
