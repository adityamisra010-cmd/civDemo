using Microsoft.Xna.Framework;

namespace Gfx.Engine.Cameras;

/// <summary>One cinematic shot: a Catmull-Rom position path and look-at path over a duration, eased.</summary>
public sealed class CameraShot
{
    public required Vector3[] Path { get; init; }      // ≥ 1 control point (the curve passes through all)
    public required Vector3[] LookAt { get; init; }    // ≥ 1 control point
    public float Duration { get; init; } = 4f;
    public float FovYRadians { get; init; } = 0.75f;
    public float FovYEndRadians { get; init; } = float.NaN; // NaN = constant fov (else zoom over the shot)
    public Func<float, float> Easing { get; init; } = Ease.InOutSine;
    public string Name { get; init; } = "";

    public CameraPose Evaluate(float t)
    {
        float u = Easing(Duration <= 0 ? 1 : t / Duration);
        float fov = float.IsNaN(FovYEndRadians) ? FovYRadians : MathHelper.Lerp(FovYRadians, FovYEndRadians, u);
        return new CameraPose(Spline.CatmullRom(Path, u), Spline.CatmullRom(LookAt, u), fov);
    }

    /// <summary>Straight dolly from → to while looking at a fixed point.</summary>
    public static CameraShot Dolly(Vector3 from, Vector3 to, Vector3 lookAt, float duration, string name = "dolly") =>
        new() { Path = new[] { from, to }, LookAt = new[] { lookAt }, Duration = duration, Name = name };

    /// <summary>Orbit around <paramref name="center"/> from angle a0 to a1 (radians), sampled into a spline.</summary>
    public static CameraShot Orbit(Vector3 center, float radius, float height, float a0, float a1, float duration, string name = "orbit")
    {
        const int n = 9; var pts = new Vector3[n];
        for (int i = 0; i < n; i++) { float a = MathHelper.Lerp(a0, a1, i / (float)(n - 1)); pts[i] = center + new Vector3(MathF.Sin(a) * radius, height, MathF.Cos(a) * radius); }
        return new CameraShot { Path = pts, LookAt = new[] { center }, Duration = duration, Easing = Ease.Linear, Name = name };
    }
}

/// <summary>Plays a sequence of shots with hard cuts (or a blend window) between them.</summary>
public sealed class CinematicCamera
{
    public List<CameraShot> Shots { get; } = new();
    public float BlendSeconds = 0f; // 0 = hard cut
    public float TotalDuration { get { float s = 0; foreach (var x in Shots) s += x.Duration; return s; } }

    /// <summary>Pose at absolute time t (clamped to the sequence) and the index of the active shot.</summary>
    public (CameraPose pose, int shot) Evaluate(float t)
    {
        if (Shots.Count == 0) throw new InvalidOperationException("no shots");
        float start = 0;
        for (int i = 0; i < Shots.Count; i++)
        {
            var s = Shots[i];
            if (t < start + s.Duration || i == Shots.Count - 1)
            {
                float local = Math.Clamp(t - start, 0, s.Duration);
                var pose = s.Evaluate(local);
                if (BlendSeconds > 0 && i > 0 && local < BlendSeconds)
                {
                    var prev = Shots[i - 1].Evaluate(Shots[i - 1].Duration);
                    float a = Ease.InOutSine(local / BlendSeconds);
                    pose = new CameraPose(Vector3.Lerp(prev.Position, pose.Position, a), Vector3.Lerp(prev.Target, pose.Target, a),
                        MathHelper.Lerp(prev.FovYRadians, pose.FovYRadians, a));
                }
                return (pose, i);
            }
            start += s.Duration;
        }
        throw new InvalidOperationException("unreachable");
    }
}

public static class Spline
{
    /// <summary>Uniform Catmull-Rom through all points, u ∈ [0,1] over the whole curve (end points clamped).</summary>
    public static Vector3 CatmullRom(Vector3[] p, float u)
    {
        if (p.Length == 1) return p[0];
        u = Math.Clamp(u, 0f, 1f);
        float f = u * (p.Length - 1); int i = Math.Min((int)f, p.Length - 2); float t = f - i;
        Vector3 p0 = p[Math.Max(i - 1, 0)], p1 = p[i], p2 = p[i + 1], p3 = p[Math.Min(i + 2, p.Length - 1)];
        return Vector3.CatmullRom(p0, p1, p2, p3, t);
    }
}
