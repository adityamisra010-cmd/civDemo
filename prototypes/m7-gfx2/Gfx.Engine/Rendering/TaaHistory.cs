using Gfx.Engine.Cameras;
using Microsoft.Xna.Framework;

namespace Gfx.Engine.Rendering;

/// <summary>
/// When the temporal anti-aliasing history may be reused (stage render2; PRESENTATION ONLY). A frame keeps the previous
/// frame's history when the camera moved continuously: less than max(<see cref="MinJumpMetres"/>,
/// <see cref="JumpFraction"/> × the previous camera's distance to its target) of translation, less than
/// <see cref="JumpDegrees"/> of rotation and less than <see cref="FovChangeRadians"/> of field-of-view change. Anything
/// larger is a cut: the history is dropped and the jitter sequence restarts, so the frame after a cut is exactly the
/// first frame of a fresh renderer. Per pixel, the resolve also drops history that reprojects off screen.
/// </summary>
public static class TaaHistory
{
    public const float MinJumpMetres = 4f, JumpFraction = 0.15f, JumpDegrees = 20f, FovChangeRadians = 0.02f;
    /// <summary>Length of the Halton(2,3) jitter cycle of interactive frames.</summary>
    public const int Sequence = 8;
    /// <summary>Weight of the current frame in the resolve (still camera), and at large motion (12 px or more).</summary>
    public const float CurrentWeight = 0.1f, CurrentWeightMoving = 0.3f;
    /// <summary>Variance-clip box half-size in standard deviations.</summary>
    public const float ClipGamma = 1.0f;

    /// <summary>True when the history of a frame seen from <paramref name="prev"/> may be reprojected into <paramref name="cur"/>.</summary>
    public static bool Continuous(in CameraPose prev, in CameraPose cur)
    {
        float moved = Vector3.Distance(prev.Position, cur.Position);
        float focus = Vector3.Distance(prev.Position, prev.Target);
        if (moved > MathF.Max(MinJumpMetres, JumpFraction * focus)) return false;
        if (Vector3.Dot(prev.Forward, cur.Forward) < MathF.Cos(MathHelper.ToRadians(JumpDegrees))) return false;
        if (MathF.Abs(prev.FovYRadians - cur.FovYRadians) > FovChangeRadians) return false;
        return true;
    }
}
