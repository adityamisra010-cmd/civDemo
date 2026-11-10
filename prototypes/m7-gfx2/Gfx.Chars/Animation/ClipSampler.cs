using Gfx.Chars.Format;
using Microsoft.Xna.Framework;

namespace Gfx.Chars.Animation;

/// <summary>Samples baked clips into a <see cref="Pose"/> (linear in time between frames, nlerp per bone).</summary>
public static class ClipSampler
{
    /// <summary>Frame position for time t: looping clips wrap, one-shots clamp at the last frame.</summary>
    public static (int f0, int f1, float a) Frames(ClipDef c, float t)
    {
        float f = t * c.Fps;
        if (c.Loop)
        {
            f %= c.Frames; if (f < 0) f += c.Frames;
            int i0 = (int)MathF.Floor(f); float a = f - i0;
            return (i0 % c.Frames, (i0 + 1) % c.Frames, a);
        }
        if (f <= 0) return (0, 0, 0);
        if (f >= c.Frames - 1) return (c.Frames - 1, c.Frames - 1, 0);
        int j0 = (int)MathF.Floor(f);
        return (j0, j0 + 1, f - j0);
    }

    public static void Sample(ClipDef c, float t, Pose into)
    {
        var (f0, f1, a) = Frames(c, t);
        int nb = Math.Min(c.Bones, into.Local.Length);
        for (int b = 0; b < nb; b++) into.Local[b] = Q.Nlerp(c.Rotations[f0 * c.Bones + b], c.Rotations[f1 * c.Bones + b], a);
        into.Pelvis = Vector3.Lerp(c.Pelvis[f0], c.Pelvis[f1], a);
    }

    /// <summary>Planted-foot bits at time t (nearest frame).</summary>
    public static byte Contacts(ClipDef c, float t)
    {
        var (f0, f1, a) = Frames(c, t);
        return c.Contacts[a < 0.5f ? f0 : f1];
    }
}
