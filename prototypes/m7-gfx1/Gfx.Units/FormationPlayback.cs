using Microsoft.Xna.Framework;

namespace Gfx.Units;

/// <summary>One soldier as the renderer draws it this frame (presentation state only).</summary>
public struct SoldierFrame
{
    public Vector3 Position;
    public float Yaw;          // radians; 0 faces +Z
    public ClipId Clip;
    public float ClipTime;     // seconds into the clip
    public bool Visible;
    public int Side;
    public SoldierLook Look;
}

/// <summary>
/// Turns the 2 s battle-trace steps into per-frame soldier state.
/// <para><b>Positions</b>: linear interpolation between consecutive step snapshots (mm → m about the field centre);
/// nothing else moves a soldier. <b>Facing</b>: derived from the step's displacement (the trace has no facing);
/// held when stationary; while a Fire event is live the soldier faces its target (presentation only).
/// <b>Clip selection</b> (priority): Escaped → hidden; Dead → death (from the snapshot that first shows Dead);
/// Damage received within 0.6 s → hit-react; Fire within the clip window → attack-melee (A=1) / fire-ranged (A=2);
/// Routing → rout-run; speed &gt; 2.2 m/s → run; &gt; 0.15 m/s → walk; else idle.</para>
/// <para><b>R24 (D-075) flag</b>: the Animator chooses ONLY pose and timing; it never invents movement, hits, deaths or
/// targets. One tension remains and is FLAGGED, not decided: a melee swing loops for its window inside the 2 s step
/// (cosmetic choreography). It never shows a hit the trace lacks (hit-react plays only on Damage events), but whether
/// such sub-step choreography is permitted needs a Director rule (recon-critic §0.6).</para>
/// </summary>
public sealed class FormationPlayback
{
    readonly BattleTrace _trace;
    readonly int[][] _eventsByEntity;   // indices into Events where the entity is source (Fire) or target (Damage)
    readonly float[][] _yawAtStep;      // [step][entity]
    readonly int[] _deathStep;          // first step showing Dead (−1 never)
    readonly SoldierLook[] _looks;
    public Vector2 CentreMm { get; }
    public int EntityCount => _trace.Entities.Count;
    public float DurationSeconds => _trace.StepTimeMs(_trace.StepCount - 1) / 1000f;

    public const float HitReactSeconds = 0.6f, RunSpeed = 2.2f, WalkSpeed = 0.15f;

    public FormationPlayback(BattleTrace trace, Vector2 centreMm)
    {
        _trace = trace; CentreMm = centreMm;
        int n = trace.Entities.Count, steps = trace.StepCount;
        var lists = new List<int>[n]; for (int i = 0; i < n; i++) lists[i] = new List<int>();
        for (int k = 0; k < trace.Events.Count; k++)
        {
            var ev = trace.Events[k];
            int who = trace.IndexOf(ev.Kind == TraceEventKind.Fire ? ev.Source : ev.Target);
            if (who >= 0) lists[who].Add(k);
        }
        _eventsByEntity = lists.Select(l => l.ToArray()).ToArray(); // events are in time order already
        _yawAtStep = new float[steps][]; _deathStep = new int[n]; _looks = new SoldierLook[n];
        for (int e = 0; e < n; e++)
        {
            _deathStep[e] = -1;
            var ent = trace.Entities[e];
            var f = trace.Formations.First(x => x.Id == ent.Formation);
            _looks[e] = SoldierLook.For(ent.Id, KitOf(f), ent.Side == 0 ? 120 : 90);
        }
        for (int s = 0; s < steps; s++)
        {
            _yawAtStep[s] = new float[n];
            for (int e = 0; e < n; e++)
            {
                float prev = s == 0 ? (trace.Entities[e].Side == 0 ? MathF.PI / 2 : -MathF.PI / 2) : _yawAtStep[s - 1][e];
                if (s > 0)
                {
                    var (x0, y0) = trace.PosMm(s - 1, e); var (x1, y1) = trace.PosMm(s, e);
                    long dx = x1 - x0, dy = y1 - y0;
                    if (dx * dx + dy * dy > 40L * 40L) prev = MathF.Atan2(dx, dy);
                }
                _yawAtStep[s][e] = prev;
                if (_deathStep[e] < 0 && trace.State(s, e) == TraceState.Dead) _deathStep[e] = s;
            }
        }
    }

    /// <summary>Formation profile name → equipment kit (presentation mapping of the unit identity).</summary>
    public static Kit KitOf(TraceFormation f) => f.Ranged ? Kit.Archer : (f.Side == 0 ? Kit.Spearman : Kit.Swordsman);

    public SoldierLook LookOf(int entityIndex) => _looks[entityIndex];

    public Vector3 ToWorld(long xMm, long yMm) => new((xMm - CentreMm.X) / 1000f, 0, (yMm - CentreMm.Y) / 1000f);

    /// <summary>Step index s with t(s) ≤ T &lt; t(s+1) (clamped).</summary>
    public int StepAt(long tMs)
    {
        int lo = 0, hi = _trace.StepCount - 1;
        if (tMs <= _trace.StepTimeMs(0)) return 0;
        if (tMs >= _trace.StepTimeMs(hi)) return hi;
        while (hi - lo > 1) { int m = (lo + hi) >> 1; if (_trace.StepTimeMs(m) <= tMs) lo = m; else hi = m; }
        return lo;
    }

    static float LerpAngle(float a, float b, float t)
    {
        float d = b - a;
        while (d > MathF.PI) d -= MathF.Tau;
        while (d < -MathF.PI) d += MathF.Tau;
        return a + d * t;
    }

    /// <summary>Fills <paramref name="out"/> (length ≥ EntityCount) for scene time <paramref name="seconds"/> (0 = first snapshot).</summary>
    public void Evaluate(float seconds, Span<SoldierFrame> @out)
    {
        long t0 = _trace.StepTimeMs(0);
        long tMs = t0 + (long)MathF.Floor(seconds * 1000f);
        int s = StepAt(tMs); int s1 = Math.Min(s + 1, _trace.StepCount - 1);
        long ts = _trace.StepTimeMs(s), te = _trace.StepTimeMs(s1);
        float u = te > ts ? Math.Clamp((seconds * 1000f + t0 - ts) / (te - ts), 0f, 1f) : 0f;
        float segSec = te > ts ? (te - ts) / 1000f : 1f;
        float now = seconds + t0 / 1000f;

        for (int e = 0; e < EntityCount; e++)
        {
            ref var f = ref @out[e];
            var ent = _trace.Entities[e];
            var (ax, ay) = _trace.PosMm(s, e); var (bx, by) = _trace.PosMm(s1, e);
            var pa = ToWorld(ax, ay); var pb = ToWorld(bx, by);
            f.Position = Vector3.Lerp(pa, pb, u);
            f.Side = ent.Side; f.Look = _looks[e];
            float speed = Vector3.Distance(pa, pb) / segSec;
            f.Yaw = LerpAngle(_yawAtStep[s][e], _yawAtStep[s1][e], Math.Clamp(u * 4f, 0f, 1f));
            var state = _trace.State(s, e);
            f.Visible = state != TraceState.Escaped;
            float loopOffset = f.Look.AnimOffsetMs / 1000f;

            if (state == TraceState.Dead)
            {
                int ds = Math.Max(_deathStep[e], 0);
                f.Clip = ClipId.Death; f.ClipTime = now - _trace.StepTimeMs(ds) / 1000f;
                continue;
            }
            // latest live event for this entity (events sorted by time; scan back from the last one ≤ now)
            var evs = _eventsByEntity[e];
            ClipId? evClip = null; float evTime = 0; long target = -1;
            int hiIdx = UpperBound(evs, (long)MathF.Floor(now * 1000f));
            for (int k = hiIdx - 1; k >= 0; k--)
            {
                var ev = _trace.Events[evs[k]];
                float age = now - ev.TimeMs / 1000f;
                if (ev.Kind == TraceEventKind.Damage && ev.Target == ent.Id)
                {
                    if (age < HitReactSeconds) { evClip = ClipId.HitReact; evTime = age; target = ev.Source; }
                    else continue;
                    break;
                }
                if (ev.Kind == TraceEventKind.Fire && ev.Source == ent.Id)
                {
                    var c = ev.A == 1 ? ClipId.AttackMelee : ClipId.FireRanged;
                    float window = ev.A == 1 ? _trace.StepMs / 1000f : ClipLibrary.Clips[(int)c].Duration;
                    if (age < window) { evClip = c; evTime = age; target = ev.Target; }
                    break;
                }
            }
            if (evClip is { } ec && state == TraceState.Active)
            {
                f.Clip = ec; f.ClipTime = evTime;
                int ti = _trace.IndexOf(target);
                if (ti >= 0 && ec != ClipId.HitReact)
                {
                    var (tx, ty) = _trace.PosMm(s, ti); var tp = ToWorld(tx, ty) - f.Position;
                    if (tp.LengthSquared() > 0.01f) f.Yaw = MathF.Atan2(tp.X, tp.Z);
                }
                continue;
            }
            f.ClipTime = now + loopOffset;
            if (state == TraceState.Routing) f.Clip = ClipId.RoutRun;
            else if (speed > RunSpeed) f.Clip = ClipId.Run;
            else if (speed > WalkSpeed) f.Clip = ClipId.Walk;
            else f.Clip = ClipId.Idle;
        }
    }

    int UpperBound(int[] evs, long tMs)
    {
        int lo = 0, hi = evs.Length;
        while (lo < hi) { int m = (lo + hi) >> 1; if (_trace.Events[evs[m]].TimeMs <= tMs) lo = m + 1; else hi = m; }
        return lo;
    }
}
