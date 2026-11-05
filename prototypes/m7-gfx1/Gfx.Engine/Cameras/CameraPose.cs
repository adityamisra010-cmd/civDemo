using Microsoft.Xna.Framework;

namespace Gfx.Engine.Cameras;

/// <summary>A camera at an instant. Presentation-only state: nothing in the simulation may read it.</summary>
public readonly record struct CameraPose(Vector3 Position, Vector3 Target, float FovYRadians = 0.8f, float Near = 0.5f, float Far = 4000f)
{
    public Vector3 Up => MathF.Abs(Vector3.Normalize(Target - Position).Y) > 0.999f ? Vector3.Forward : Vector3.Up;
    public Matrix View => Matrix.CreateLookAt(Position, Target, Up);
    public Matrix Projection(float aspect) => Matrix.CreatePerspectiveFieldOfView(FovYRadians, aspect, Near, Far);
    public Matrix ViewProjection(float aspect) => View * Projection(aspect);
    public Vector3 Forward => Vector3.Normalize(Target - Position);
}

/// <summary>Easing curves for camera moves (t in [0,1]).</summary>
public static class Ease
{
    public static float Linear(float t) => Math.Clamp(t, 0f, 1f);
    public static float InOutCubic(float t) { t = Math.Clamp(t, 0f, 1f); return t < 0.5f ? 4 * t * t * t : 1 - MathF.Pow(-2 * t + 2, 3) / 2; }
    public static float InOutSine(float t) => -(MathF.Cos(MathF.PI * Math.Clamp(t, 0f, 1f)) - 1) / 2;
    public static float OutQuad(float t) { t = Math.Clamp(t, 0f, 1f); return 1 - (1 - t) * (1 - t); }
}
