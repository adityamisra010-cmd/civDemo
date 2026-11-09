using Gfx.Cinematics.Demo;
using Gfx.Engine.Cameras;
using Gfx.Engine.Scene;
using Gfx.Units.Demo;
using Microsoft.Xna.Framework;

namespace Gfx.Perf.Scenes;

/// <summary>Camera, scene time and time of day at one point of a path.</summary>
public readonly record struct PathFrame(CameraPose Camera, float SceneTime, float Hour);

/// <summary>
/// A fixed camera path parameterised by u ∈ [0, 1) = measured-frame index / measured-frame count, so every run of
/// a configuration renders the same frames whatever the machine's speed (frame-based, never wall-clock-based).
/// </summary>
public interface ICameraPath
{
    string Description { get; }
    PathFrame At(float u);
}

/// <summary>Orbit of a capture shot's camera around its target: yaw offset = amplitude · sin(2πu); scene time advances linearly.</summary>
public sealed class OrbitPath : ICameraPath
{
    readonly CameraPose _base; readonly Vector3 _target; readonly float _yaw0, _horiz, _height, _amp, _t0, _seconds, _hour;
    public string Description { get; }

    public OrbitPath(CameraPose shot, float sceneTime0, float hour, float amplitudeRad, float seconds, string name)
    {
        _base = shot; _target = shot.Target;
        var off = shot.Position - shot.Target;
        _yaw0 = MathF.Atan2(off.X, off.Z); _horiz = new Vector2(off.X, off.Z).Length(); _height = off.Y;
        _amp = amplitudeRad; _t0 = sceneTime0; _seconds = seconds; _hour = hour;
        Description = string.Create(System.Globalization.CultureInfo.InvariantCulture,
            $"{name}: orbit {_horiz:F1} m out, {_height:F1} m up, yaw ±{MathHelper.ToDegrees(amplitudeRad):F0}° (one sine period), scene time {sceneTime0:F2}+{seconds:F0} s, hour {hour:F1}");
    }

    public PathFrame At(float u)
    {
        float yaw = _yaw0 + _amp * MathF.Sin(MathHelper.TwoPi * u);
        var pos = _target + new Vector3(MathF.Sin(yaw) * _horiz, _height, MathF.Cos(yaw) * _horiz);
        return new PathFrame(_base with { Position = pos }, _t0 + u * _seconds, _hour);
    }
}

/// <summary>The cinematic's own planned camera (shot sequence), time-scaled so the whole plan plays over the run.</summary>
public sealed class CinematicPath : ICameraPath
{
    readonly CinematicSceneBase _scene; readonly Vector3 _origin; readonly float _hour;
    public string Description { get; }

    public CinematicPath(CinematicSceneBase scene)
    {
        _scene = scene; _origin = scene.Focus - scene.Cine.Mid; _hour = scene.Plan.HourTenths / 10f;
        Description = string.Create(System.Globalization.CultureInfo.InvariantCulture,
            $"cinematic planner camera: {scene.Plan.Shots.Count} shots, {scene.Cine.Duration:F1} s played over the measured frames, hour {_hour:F1}");
    }

    public PathFrame At(float u)
    {
        float t = u * _scene.Cine.Duration;
        var c = _scene.Cine.Evaluate(t).Camera;
        return new PathFrame(c with { Position = c.Position + _origin, Target = c.Target + _origin }, t, _hour);
    }
}

public static class CameraPaths
{
    /// <summary>
    /// Path keys: A, C, E orbit the scene's first capture shot; B orbits the battle line's front at ~65 m (crowd
    /// scenes; otherwise the first shot); B-wide orbits the crowd scene's whole-army "wide" shot; D plays the
    /// cinematic planner's camera when the scene is a cinematic, else orbits the first shot.
    /// </summary>
    public static ICameraPath For(string key, DemoScene scene)
    {
        var shot = scene.Shots.Count > 0 ? scene.Shots[0] : new CaptureShot("default", new CameraPose(new Vector3(0, 20, 40), Vector3.Zero));
        switch (key)
        {
            case "A": return new OrbitPath(shot.Camera, shot.SceneTime, shot.HourOfDay, 0.45f, 10f, "A " + shot.Name);
            case "B" when scene is UnitsCrowdScene:
            {
                // Formation view: ~65 m from the centre of the front line, from the side-0 rear flank (LOD0/1/2 mix at x1).
                var target = new Vector3(0, 1, 0);
                var dir = Vector3.Normalize(new Vector3(-16, 0, 20));
                var pose = new CameraPose(target + dir * 60f + new Vector3(0, 26, 0), target, MathHelper.ToRadians(55), 0.5f, 4000f);
                return new OrbitPath(pose, 0.5f, 10f, 0.6f, 10f, "B formation");
            }
            case "B": return new OrbitPath(shot.Camera, shot.SceneTime, shot.HourOfDay, 0.5f, 10f, "B " + shot.Name);
            case "B-wide": return new OrbitPath(shot.Camera, shot.SceneTime, shot.HourOfDay, 0.15f, 10f, "B-wide " + shot.Name);
            case "C": return new OrbitPath(shot.Camera, shot.SceneTime, shot.HourOfDay, 0.35f, 10f, "C " + shot.Name);
            case "D" when scene is CinematicSceneBase cine: return new CinematicPath(cine);
            case "D": return new OrbitPath(shot.Camera, shot.SceneTime, shot.HourOfDay, 0.3f, 10f, "D " + shot.Name);
            case "E": return new OrbitPath(shot.Camera, shot.SceneTime, shot.HourOfDay, 0.6f, 10f, "E " + shot.Name);
            default: throw new ArgumentException($"unknown camera path '{key}'");
        }
    }
}
