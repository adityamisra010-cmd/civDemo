using Gfx.Chars.Rendering;
using Microsoft.Xna.Framework;

namespace Gfx.Chars.Animation;

/// <summary>
/// Deterministic driver for scenes and captures: a script gives the inputs and the world transform at any scene
/// time; <see cref="Advance"/> steps the animator from the last time (or from 0 after a rewind) with a fixed dt, so a
/// still at time t is identical however it was reached.
/// </summary>
public sealed class AnimDriver
{
    public const float Dt = 1f / 60f;
    public CharInstance Instance { get; }
    public CharAnimator Animator { get; private set; }
    readonly Func<float, AnimInput> _script;
    readonly Func<float, Matrix> _world;
    float _t = -1f;
    /// <summary>CPU time of the animation evaluation per step (ms), accumulated since the last rewind.</summary>
    public double CpuMs { get; private set; }
    public int Steps { get; private set; }

    /// <summary>Clip phase offset (s): idles and cycles of several characters do not run in unison.</summary>
    public float Phase { get; }

    public AnimDriver(CharInstance inst, Func<float, AnimInput> script, Func<float, Matrix> world, float phase = 0f)
    {
        Instance = inst; _script = script; _world = world; Phase = phase;
        Animator = new CharAnimator(inst.Asset.ClipFile, inst.Asset.Skeleton) { TimeOffset = phase };
    }

    public void Advance(float t)
    {
        if (t < _t || _t < 0)
        {
            Animator = new CharAnimator(Instance.Asset.ClipFile, Instance.Asset.Skeleton) { TimeOffset = Phase };
            _t = 0f; CpuMs = 0; Steps = 0;
            Animator.Step(0f, 0f, _script(0f), _world(0f));
        }
        var sw = System.Diagnostics.Stopwatch.StartNew();
        while (_t + Dt * 0.5f < t)
        {
            float step = MathF.Min(Dt, t - _t);
            _t += step;
            Animator.Step(_t, step, _script(_t), _world(_t));
            Steps++;
        }
        CpuMs += sw.Elapsed.TotalMilliseconds;
        Instance.World = _world(t);
        Instance.Pose.CopyFrom(Animator.Pose);
    }
}
