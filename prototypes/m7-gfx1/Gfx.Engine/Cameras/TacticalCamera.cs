using Microsoft.Xna.Framework;

namespace Gfx.Engine.Cameras;

/// <summary>
/// Total War-style tactical camera: orbit around a ground focus point. Pan (camera-relative), rotate (yaw),
/// zoom (distance; pitch flattens as you zoom in), edge clamp of the focus to map bounds, follow a target.
/// Smoothing is exponential toward the goal state (frame-rate independent). Presentation-only.
/// </summary>
public sealed class TacticalCamera
{
    public Vector3 Focus;          // current (smoothed)
    public float Yaw, Distance;    // current
    Vector3 _goalFocus; float _goalYaw, _goalDistance;

    public float MinDistance = 8f, MaxDistance = 600f;
    public float PitchNear = MathHelper.ToRadians(18f), PitchFar = MathHelper.ToRadians(62f);
    public BoundingBox? Bounds;                 // focus clamp (xz used)
    public float Smoothing = 12f;               // 1/s; 0 = snap
    public Func<Vector3>? FollowTarget;         // when set, the focus goal tracks this point
    public Func<float, float, float>? GroundHeight; // optional terrain height lookup for the focus

    public TacticalCamera(Vector3 focus, float yaw = 0f, float distance = 120f)
    {
        Focus = _goalFocus = focus; Yaw = _goalYaw = yaw; Distance = _goalDistance = distance;
    }

    public Vector3 GoalFocus => _goalFocus;
    public float Pitch => MathHelper.Lerp(PitchNear, PitchFar, Zoom01(Distance));
    float Zoom01(float d) => Math.Clamp((MathF.Log(d) - MathF.Log(MinDistance)) / (MathF.Log(MaxDistance) - MathF.Log(MinDistance)), 0f, 1f);

    /// <summary>Pan in camera-relative ground axes (right, forward), metres. Speed scales with zoom.</summary>
    public void Pan(float right, float forward)
    {
        FollowTarget = null;
        var fwd = new Vector3(-MathF.Sin(_goalYaw), 0, -MathF.Cos(_goalYaw));
        var rt = new Vector3(-fwd.Z, 0, fwd.X);
        _goalFocus += rt * right + fwd * forward;
        Clamp();
    }
    public void Rotate(float deltaYaw) => _goalYaw += deltaYaw;
    /// <summary>Multiplicative zoom: factor &lt; 1 moves closer.</summary>
    public void Zoom(float factor) => _goalDistance = Math.Clamp(_goalDistance * factor, MinDistance, MaxDistance);
    public void JumpTo(Vector3 focus) { _goalFocus = focus; Clamp(); }

    void Clamp()
    {
        if (Bounds is { } b)
            _goalFocus = new Vector3(Math.Clamp(_goalFocus.X, b.Min.X, b.Max.X), _goalFocus.Y, Math.Clamp(_goalFocus.Z, b.Min.Z, b.Max.Z));
    }

    public void Update(float dtSeconds)
    {
        if (FollowTarget != null) { _goalFocus = FollowTarget(); Clamp(); }
        if (GroundHeight != null) _goalFocus.Y = GroundHeight(_goalFocus.X, _goalFocus.Z);
        float a = Smoothing <= 0 ? 1f : 1f - MathF.Exp(-Smoothing * dtSeconds);
        Focus = Vector3.Lerp(Focus, _goalFocus, a);
        Yaw = MathHelper.Lerp(Yaw, _goalYaw, a);
        Distance = MathHelper.Lerp(Distance, _goalDistance, a);
    }

    public CameraPose Pose
    {
        get
        {
            float p = Pitch;
            var offset = new Vector3(MathF.Sin(Yaw) * MathF.Cos(p), MathF.Sin(p), MathF.Cos(Yaw) * MathF.Cos(p)) * Distance;
            return new CameraPose(Focus + offset, Focus);
        }
    }
}
