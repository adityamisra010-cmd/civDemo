using Gfx.Chars.Animation;
using Microsoft.Xna.Framework;

namespace Gfx.Gear.Rig;

/// <summary>
/// Finger wrap: curls each finger joint (index to little finger: three joints; the thumb: three joints with the rig's
/// thumb axis) about its rest curl axis until that phalanx lies on a cylinder of the grip's radius plus the finger's
/// thickness, so a fist closes round a spear shaft, a sword grip, a shield bar or a cord instead of a fixed curl.
/// Works on a solved pose (character space globals and heads) and keeps it consistent. PRESENTATION ONLY.
/// </summary>
public static class Grip
{
    /// <summary>Half the thickness of a finger (m): the pad touches the grip at radius + this.</summary>
    public const float FingerHalf = 0.0085f;
    public const float MaxCurl = 1.75f;     // radians per joint (about 100 degrees)

    /// <summary>
    /// Wraps the fingers of one hand round the cylinder (point, unit direction, radius) given in character space.
    /// Returns the mean |distance - (radius + finger half thickness)| of the distal segments' ends after wrapping (m).
    /// </summary>
    public static float Wrap(CharRig rig, Pose p, Quaternion[] g, Vector3[] h, bool left, Vector3 point, Vector3 dir, float radius, float curlScale = 1f)
    {
        var s = rig.S;
        var fingers = left ? rig.FingersL : rig.FingersR;
        dir = Vector3.Normalize(dir);
        float target = radius + FingerHalf;
        float err = 0; int n = 0;
        // fingers come in chains of three (index, middle, ring, little, thumb), proximal first
        for (int c = 0; c + 2 < fingers.Length; c += 3)
        {
            for (int k = 0; k < 3; k++)
            {
                var (b, axis, scale) = fingers[c + k];
                int par = s.Parent[b];
                // the end of this phalanx: the next joint's head, or for the last one its tail
                Vector3 End(Quaternion local)
                {
                    var gb = Q.Mul(g[par], local);
                    if (k < 2) { int nb = fingers[c + k + 1].bone; return h[b] + Q.Rotate(gb, s.Head[nb] - s.Head[b]); }
                    return h[b] + Q.Rotate(gb, s.Tail[b] - s.Head[b]);
                }
                float Dist(Vector3 q) { var d = q - point; return (d - dir * Vector3.Dot(d, dir)).Length(); }
                bool thumb = scale < 0.99f;
                float max = MaxCurl * (thumb ? 0.6f : 1f) * curlScale;
                // the smallest curl at which the segment's end reaches the cylinder (coarse steps, then bisection)
                float lo = 0f, hi = max;
                float dPrev = Dist(End(Quaternion.CreateFromAxisAngle(axis, 0f)));
                if (dPrev <= target) hi = 0f;
                else
                {
                    const int steps = 18;
                    bool found = false;
                    for (int i = 1; i <= steps; i++)
                    {
                        float a = max * i / steps;
                        float d = Dist(End(Quaternion.CreateFromAxisAngle(axis, a)));
                        if (d <= target) { lo = max * (i - 1) / steps; hi = a; found = true; break; }
                    }
                    if (!found) { lo = hi = max; }
                    for (int it = 0; it < 12 && hi - lo > 1e-4f; it++)
                    {
                        float m = 0.5f * (lo + hi);
                        if (Dist(End(Quaternion.CreateFromAxisAngle(axis, m))) <= target) hi = m; else lo = m;
                    }
                }
                p.Local[b] = Quaternion.CreateFromAxisAngle(axis, hi);
                Ik.Refresh(s, p, g, h, b);
                if (k == 2) { err += MathF.Abs(Dist(End(p.Local[b])) - target); n++; }
            }
        }
        return n > 0 ? err / n : 0f;
    }

    /// <summary>Open hand (fingers nearly straight) or a loose relaxed curl, by a uniform angle per joint.</summary>
    public static void Uniform(CharRig rig, Pose p, Quaternion[] g, Vector3[] h, bool left, float angle)
    {
        var fingers = left ? rig.FingersL : rig.FingersR;
        foreach (var (b, axis, scale) in fingers)
        {
            p.Local[b] = Quaternion.CreateFromAxisAngle(axis, angle * scale);
            Ik.Refresh(rig.S, p, g, h, b);
        }
    }
}
