using Gfx.Chars.Format;
using Microsoft.Xna.Framework;

namespace Gfx.Chars.Animation;

/// <summary>
/// Cosmetic animation inputs for one character at one instant. PRESENTATION ONLY (DR42): these come from published
/// simulation state or traces (speed, facing, events) or a demo script; nothing here is fed back.
/// </summary>
public sealed class AnimInput
{
    public float Speed;                    // m/s along the facing direction (0 = standing)
    public string? Action;                 // full-body one-shot clip name (thrust, swing_over, block, death_fwd, kneel ...)
    public float ActionStart;              // scene time the action began
    public bool HoldLastFrame = true;      // one-shots hold their last frame (death, kneel)
    public string Stance = "none";         // upper-body arm stance (masked layer): none, spear, shield_up, sword, hands_up
    public float StanceWeight = 1f;
    public Vector3? LookAt;                // world point for head and eyes
    public float HitTime = -1f;            // additive hit reaction start (scene time)
    public float HitYaw;                   // direction the blow comes from (radians, character space)
    public float HitStrength = 1f;
    public string Grip = "relaxed";        // relaxed, fist, open
    public bool FootLock = true;
    public bool StrideSync = true;
    public string Idle = "idle";
    /// <summary>Upper-body layer: a clip (sampled at <see cref="UpperClipTime"/>, or running when negative) blended over
    /// the base pose through the upper-body mask (spine_02 0.35, spine_03 0.7, arms, hands and fingers 1), e.g. arms held
    /// still while the legs walk. The stance IK then places the hands on the weapon.</summary>
    public string? UpperClip;
    public float UpperClipTime = -1f;
    public float UpperWeight = 1f;
}

/// <summary>Per-skeleton constants: bone indices, masks, rest geometry used by the procedural layers.</summary>
public sealed class CharRig
{
    public readonly Skeleton S;
    public readonly int Pelvis, Spine1, Spine2, Spine3, Neck, Head, EyeL, EyeR;
    public readonly int ThighL, CalfL, FootL, BallL, ThighR, CalfR, FootR, BallR;
    public readonly int ClavL, UpperL, LowerL, HandL, ClavR, UpperR, LowerR, HandR;
    public readonly float[] UpperBody;
    public readonly (int bone, Vector3 axis, float scale)[] FingersL, FingersR;
    public readonly Vector3 PalmL, PalmR, FingerDirL, FingerDirR;

    public CharRig(Skeleton s)
    {
        S = s;
        Pelvis = s["pelvis"]; Spine1 = s["spine_01"]; Spine2 = s["spine_02"]; Spine3 = s["spine_03"]; Neck = s["neck_01"]; Head = s["head"];
        EyeL = s["eye_l"]; EyeR = s["eye_r"];
        ThighL = s["thigh_l"]; CalfL = s["calf_l"]; FootL = s["foot_l"]; BallL = s["ball_l"];
        ThighR = s["thigh_r"]; CalfR = s["calf_r"]; FootR = s["foot_r"]; BallR = s["ball_r"];
        ClavL = s["clavicle_l"]; UpperL = s["upperarm_l"]; LowerL = s["lowerarm_l"]; HandL = s["hand_l"];
        ClavR = s["clavicle_r"]; UpperR = s["upperarm_r"]; LowerR = s["lowerarm_r"]; HandR = s["hand_r"];
        UpperBody = new float[s.Count];
        for (int i = 0; i < s.Count; i++)
        {
            string n = s.Names[i];
            float w = n switch
            {
                "spine_02" => 0.35f, "spine_03" => 0.7f,
                _ when n.StartsWith("clavicle", StringComparison.Ordinal) || n.StartsWith("upperarm", StringComparison.Ordinal) || n.StartsWith("lowerarm", StringComparison.Ordinal)
                       || n.StartsWith("hand", StringComparison.Ordinal) || n.Contains("_0", StringComparison.Ordinal) => 1f,
                _ => 0f,
            };
            UpperBody[i] = w;
        }
        (PalmL, FingerDirL) = PalmFrame(s, "l");
        (PalmR, FingerDirR) = PalmFrame(s, "r");
        FingersL = Fingers(s, "l", PalmL);
        FingersR = Fingers(s, "r", PalmR);
    }

    /// <summary>Rest-pose palm normal (pointing out of the palm) and finger direction of one hand.</summary>
    static (Vector3 palm, Vector3 fingers) PalmFrame(Skeleton s, string side)
    {
        var h = s.Head[s["hand_" + side]];
        var idx = s.Head[s["index_01_" + side]]; var pin = s.Head[s["pinky_01_" + side]]; var mid = s.Head[s["middle_01_" + side]];
        var f = Vector3.Normalize(mid - h);
        var across = Vector3.Normalize(idx - pin);
        var n = Vector3.Normalize(Vector3.Cross(f, across));
        // the palm faces the thumb's side of the fist: the thumb tip lies on the palm side of the index base
        var thumb = s.Head[s["thumb_02_" + side]];
        if (Vector3.Dot(thumb - idx, n) < 0) n = -n;
        return (n, f);
    }

    static (int, Vector3, float)[] Fingers(Skeleton s, string side, Vector3 palm)
    {
        var list = new List<(int, Vector3, float)>();
        foreach (var f in new[] { "index", "middle", "ring", "pinky", "thumb" })
            for (int k = 1; k <= 3; k++)
            {
                int b = s[$"{f}_0{k}_{side}"];
                var d = s.Tail[b] - s.Head[b];
                if (d.LengthSquared() < 1e-10f) d = s.Head[b] - s.Head[s.Parent[b]];
                d.Normalize();
                var axis = Vector3.Cross(d, palm);
                if (axis.LengthSquared() < 1e-8f) continue;
                axis.Normalize();
                list.Add((b, axis, f == "thumb" ? 0.45f : 1f));
            }
        return list.ToArray();
    }
}

/// <summary>
/// The character animation layer (cosmetic, presentation only): stride-synchronised locomotion blending, 0.2 s
/// crossfades between states, full-body one-shots, an upper-body arm stance layer (masked: spine_02 up, solved by
/// two-bone IK to hand targets), additive procedural hit reactions, head and eye look-at, hand poses, and two-bone
/// foot IK with foot locking on planted feet. Deterministic: callers step it with a fixed dt from scene time 0.
/// </summary>
public sealed class CharAnimator
{
    public const float CrossfadeSeconds = 0.2f;
    readonly Format.CharacterFile _file;
    readonly CharRig _rig;
    readonly Skeleton _s;
    readonly Pose _out, _from, _a, _b, _tmp;
    readonly Quaternion[] _g; readonly Vector3[] _h;
    string _state = ""; float _fade = 1f;
    float _phase; float _idleTime;
    readonly Vector3[] _lock = new Vector3[2]; readonly bool[] _locked = new bool[2]; readonly float[] _lockW = new float[2];
    readonly Vector3[] _prevAnkle = new Vector3[2], _prevBall = new Vector3[2]; bool _havePrev;
    public Pose Pose => _out;
    public CharRig Rig => _rig;
    /// <summary>Planted-foot slide (m/s, world, horizontal) before and after foot locking, accumulated over planted steps.</summary>
    public SlideStats Slide { get; } = new();
    public float LastBlendWeight { get; private set; }
    /// <summary>Weight of the current state against the previous one (1 once the 0.2 s crossfade has finished).</summary>
    public float LastCrossfadeWeight { get; private set; } = 1f;
    /// <summary>Offset (s) added to the idle clock and the locomotion phase at the start (desynchronises a crowd).</summary>
    public float TimeOffset { get => _timeOffset; init { _timeOffset = value; _idleTime = value; } }
    readonly float _timeOffset;
    public string State => _state;

    public CharAnimator(Format.CharacterFile file, Skeleton s)
    {
        _file = file; _s = s; _rig = new CharRig(s);
        _out = new Pose(s.Count); _from = new Pose(s.Count); _a = new Pose(s.Count); _b = new Pose(s.Count); _tmp = new Pose(s.Count);
        _g = new Quaternion[s.Count]; _h = new Vector3[s.Count];
    }

    public ClipDef Clip(string name) => _file.Clip(name) ?? throw new KeyNotFoundException("clip " + name);

    /// <summary>The two cycles blended at speed v and the blend weight (walk below its natural speed, run above its own).</summary>
    (ClipDef c1, ClipDef c2, float w) Cycles(float v)
    {
        var walk = Clip("walk"); var jog = Clip("jog"); var run = Clip("run");
        if (v <= walk.Speed) return (walk, walk, 0);
        if (v <= jog.Speed) return (walk, jog, (v - walk.Speed) / (jog.Speed - walk.Speed));
        if (v <= run.Speed) return (jog, run, (v - jog.Speed) / (run.Speed - jog.Speed));
        return (run, run, 0);
    }

    /// <summary>Duration (s) of one stride-synchronised locomotion cycle at speed v (m/s).</summary>
    public float CycleSeconds(float v)
    {
        var (c1, c2, w) = Cycles(v);
        return MathHelper.Lerp(c1.Speed * c1.Duration, c2.Speed * c2.Duration, w) / MathF.Max(v, 1e-3f);
    }

    /// <summary>Locomotion pose for a speed (stride-synchronised blend of idle, walk, jog and run).</summary>
    void Locomotion(AnimInput inp, float dt, Pose into, out byte contacts)
    {
        var walk = Clip("walk"); var idle = Clip(inp.Idle);
        float v = MathF.Max(0, inp.Speed);
        _idleTime += dt;
        contacts = 3;
        if (v < 0.05f) { ClipSampler.Sample(idle, _idleTime, into); contacts = ClipSampler.Contacts(idle, _idleTime); LastBlendWeight = 0; return; }
        var (c1, c2, w) = Cycles(v);
        LastBlendWeight = w;
        // stride sync: the blended pose covers the blended stride length (speed x duration of each cycle) per cycle, so
        // the cycle takes stride / v and the planted foot moves back at exactly v; without sync the blend plays at the
        // blended natural duration
        float stride = MathHelper.Lerp(c1.Speed * c1.Duration, c2.Speed * c2.Duration, w);
        float cycle = inp.StrideSync ? stride / MathF.Max(v, 1e-3f) : MathHelper.Lerp(c1.Duration, c2.Duration, w);
        _phase = (_phase + dt / MathF.Max(cycle, 1e-3f)) % 1f;
        ClipSampler.Sample(c1, _phase * c1.Duration, _a);
        ClipSampler.Sample(c2, _phase * c2.Duration, _b);
        into.Blend(_a, _b, w);
        contacts = ClipSampler.Contacts(w < 0.5f ? c1 : c2, _phase * (w < 0.5f ? c1 : c2).Duration);
        // a slow walk eases into the idle stance
        if (v < walk.Speed * 0.5f)
        {
            ClipSampler.Sample(idle, _idleTime, _tmp);
            float k = 1 - v / (walk.Speed * 0.5f);
            into.Blend(into, _tmp, k * 0.6f);
        }
    }

    /// <summary>Advances by dt (s) to scene time t and evaluates the pose (character space: World is applied later).</summary>
    public void Step(float t, float dt, AnimInput inp, in Matrix world)
    {
        byte contacts;
        string state = inp.Action != null && t >= inp.ActionStart ? "act:" + inp.Action : "loco";
        if (state != _state)
        {
            if (_state.Length > 0) { _from.CopyFrom(_out); _fade = 0f; }
            _state = state;
        }
        if (state == "loco") Locomotion(inp, dt, _tmp, out contacts);
        else
        {
            var c = Clip(inp.Action!);
            float lt = t - inp.ActionStart;
            if (!c.Loop && !inp.HoldLastFrame && lt > c.Duration) lt = c.Duration;
            ClipSampler.Sample(c, lt, _tmp);
            contacts = ClipSampler.Contacts(c, lt);
        }
        _fade = MathF.Min(1f, _fade + dt / CrossfadeSeconds);
        float fw = _fade * _fade * (3 - 2 * _fade);
        if (fw < 1f) _out.Blend(_from, _tmp, fw); else _out.CopyFrom(_tmp);
        LastCrossfadeWeight = fw;
        if (inp.UpperClip != null && inp.UpperWeight > 0)
        {
            var uc = Clip(inp.UpperClip);
            ClipSampler.Sample(uc, inp.UpperClipTime >= 0 ? inp.UpperClipTime : t, _b);
            _out.BlendMasked(_b, _rig.UpperBody, inp.UpperWeight);
        }

        HandPose(inp.Grip, _rig.FingersR, inp.Stance == "none" ? inp.Grip : "fist");
        HandPose(inp.Grip, _rig.FingersL, inp.Stance is "shield_up" or "spear" or "sword" or "rest" ? "fist" : inp.Grip);
        Additive(t, inp);
        // FK, then the procedural layers on the solved pose
        Fk.Solve(_s, _out, _g, _h, _rig.Pelvis);
        Stance(inp);
        if (inp.LookAt is { } lw) Look(Vector3.Transform(lw, Matrix.Invert(world)));
        Feet(t, dt, inp, world, contacts, state == "loco" && inp.Speed > 0.05f);
    }

    void HandPose(string _, (int bone, Vector3 axis, float scale)[] fingers, string grip)
    {
        float a = grip switch { "fist" => 1.35f, "open" => 0.05f, _ => 0.32f };
        foreach (var (b, axis, sc) in fingers)
            _out.Local[b] = Quaternion.CreateFromAxisAngle(axis, a * sc * (_s.Names[b].EndsWith("1_l", StringComparison.Ordinal) || _s.Names[b].EndsWith("1_r", StringComparison.Ordinal) ? 0.8f : 1f));
    }

    /// <summary>Additive hit reaction: a quick backward flinch of spine, neck and head away from the blow, decaying.</summary>
    void Additive(float t, AnimInput inp)
    {
        if (inp.HitTime < 0 || t < inp.HitTime) return;
        float u = t - inp.HitTime;
        float env = u < 0.07f ? u / 0.07f : MathF.Exp(-(u - 0.07f) / 0.18f);
        if (env < 0.01f) return;
        env *= inp.HitStrength;
        var away = new Vector3(-MathF.Sin(inp.HitYaw), 0, -MathF.Cos(inp.HitYaw));
        var axis = Vector3.Cross(Vector3.UnitY, away);
        if (axis.LengthSquared() < 1e-6f) axis = Vector3.UnitX;
        axis.Normalize();
        void Add(int b, float deg, Vector3 ax) => _out.Local[b] = Q.Mul(_out.Local[b], Quaternion.CreateFromAxisAngle(ax, MathHelper.ToRadians(deg) * env));
        Add(_rig.Spine1, 4f, axis); Add(_rig.Spine2, 9f, axis); Add(_rig.Spine3, 11f, axis); Add(_rig.Neck, 12f, axis); Add(_rig.Head, 18f, axis);
        Add(_rig.Spine3, 9f, Vector3.UnitY);
        Add(_rig.ClavL, -8f, Vector3.UnitZ); Add(_rig.ClavR, 8f, Vector3.UnitZ);
    }

    /// <summary>Arm stances as hand targets in chest space, blended over the base pose by the upper-body weight.</summary>
    void Stance(AnimInput inp)
    {
        if (inp.Stance == "none" || inp.StanceWeight <= 0) return;
        var chest = _h[_rig.Spine3];
        var cr = _g[_rig.Spine3];
        Vector3 C(float x, float y, float z) => chest + Q.Rotate(cr, new Vector3(x, y, z));
        Vector3 handR, handL; Vector3 poleR = Q.Rotate(cr, new Vector3(-1f, -0.4f, -0.5f)), poleL = Q.Rotate(cr, new Vector3(1f, -0.6f, -0.2f));
        switch (inp.Stance)
        {
            case "spear": handR = C(-0.21f, -0.08f, 0.22f); handL = C(0.14f, 0.0f, 0.30f); break;
            case "shield_up": handR = C(-0.24f, 0.12f, 0.05f); handL = C(0.06f, 0.10f, 0.40f); poleL = Q.Rotate(cr, new Vector3(1f, -0.2f, 0.3f)); break;
            case "sword": handR = C(-0.18f, -0.12f, 0.32f); handL = C(0.16f, 0.02f, 0.30f); break;
            case "hands_up": handR = C(-0.28f, 0.42f, 0.08f); handL = C(0.28f, 0.42f, 0.08f); poleR = Q.Rotate(cr, new Vector3(-1f, -0.5f, 0f)); poleL = Q.Rotate(cr, new Vector3(1f, -0.5f, 0f)); break;
            // at rest: the weapon stands upright in the right hand beside the hip; the shield arm keeps the clip's pose
            case "rest": handR = C(-0.27f, -0.42f, 0.12f); handL = Vector3.Zero; poleR = Q.Rotate(cr, new Vector3(-0.6f, 0f, -1f)); break;
            default: return;
        }
        float w = inp.StanceWeight * _rig.UpperBody[_rig.HandR];
        Ik.TwoBone(_s, _out, _g, _h, _rig.UpperR, _rig.LowerR, _rig.HandR, handR, w, keepEndRotation: true, pole: poleR);
        if (inp.Stance == "rest")
        {
            OrientHand(_rig.HandR, _rig.FingerDirR, _rig.PalmR, Q.Rotate(cr, new Vector3(0.1f, -0.15f, 1f)), Q.Rotate(cr, new Vector3(1f, 0f, 0f)), w);
            return;
        }
        Ik.TwoBone(_s, _out, _g, _h, _rig.UpperL, _rig.LowerL, _rig.HandL, handL, w, keepEndRotation: true, pole: poleL);
        if (inp.Stance == "hands_up")
        {
            // open palms facing forward
            OrientHand(_rig.HandR, _rig.FingerDirR, _rig.PalmR, Vector3.UnitY, Vector3.UnitZ, w);
            OrientHand(_rig.HandL, _rig.FingerDirL, _rig.PalmL, Vector3.UnitY, Vector3.UnitZ, w);
            return;
        }
        // right hand: fist around a vertical shaft (fingers forward, palm facing in); left: shield grip
        OrientHand(_rig.HandR, _rig.FingerDirR, _rig.PalmR, Q.Rotate(cr, new Vector3(0.2f, 0f, 1f)), Q.Rotate(cr, new Vector3(1f, 0f, 0f)), w);
        OrientHand(_rig.HandL, _rig.FingerDirL, _rig.PalmL, Q.Rotate(cr, new Vector3(-0.3f, 0f, 1f)), Q.Rotate(cr, new Vector3(-1f, 0f, 0f)), w);
    }

    /// <summary>Sets a hand's character-space rotation so its rest finger direction and palm normal map to the given ones.</summary>
    void OrientHand(int hand, Vector3 f0, Vector3 n0, Vector3 f1, Vector3 n1, float w)
    {
        f1.Normalize(); n1 = Vector3.Normalize(n1 - Vector3.Dot(n1, f1) * f1);
        var m0 = Basis(f0, n0); var m1 = Basis(f1, n1);
        var target = Quaternion.CreateFromRotationMatrix(Matrix.Transpose(m0) * m1);
        target.Normalize();
        var cur = _g[hand];
        var want = Q.Nlerp(cur, target, w);
        Ik.RotateGlobal(_s, _out, _g, _h, hand, Q.Mul(want, Q.Inverse(cur)));
    }

    static Matrix Basis(Vector3 f, Vector3 n)
    {
        f.Normalize(); n = Vector3.Normalize(n - Vector3.Dot(n, f) * f);
        var x = Vector3.Cross(n, f);
        return new Matrix(x.X, x.Y, x.Z, 0, n.X, n.Y, n.Z, 0, f.X, f.Y, f.Z, 0, 0, 0, 0, 1);
    }

    void Look(Vector3 target)
    {
        var chain = new[] { _rig.Spine3, _rig.Neck, _rig.Head };
        var w = new[] { 0.15f, 0.35f, 0.5f };
        var eyes = 0.5f * (_h[_rig.EyeL] + _h[_rig.EyeR]);
        Ik.LookAt(_s, _out, _g, _h, chain, w, _rig.Head, Vector3.UnitZ, target + (_h[_rig.Head] - eyes), MathHelper.ToRadians(75), MathHelper.ToRadians(40), 1f);
        foreach (int e in new[] { _rig.EyeL, _rig.EyeR })
        {
            var fwd = Q.Rotate(_g[e], Vector3.UnitZ);
            var want = Vector3.Normalize(target - _h[e]);
            float ang = MathF.Acos(Math.Clamp(Vector3.Dot(fwd, want), -1f, 1f));
            float lim = MathHelper.ToRadians(28);
            var r = Q.Between(fwd, want);
            if (ang > lim) r = Q.Scale(r, lim / ang);
            Ik.RotateGlobal(_s, _out, _g, _h, e, r);
        }
    }

    /// <summary>Foot locking: a planted foot keeps its world position (two-bone leg IK), released over 0.12 s.</summary>
    void Feet(float t, float dt, AnimInput inp, in Matrix world, byte contacts, bool moving)
    {
        var inv = Matrix.Invert(world);
        var ankles = new[] { Vector3.Transform(_h[_rig.FootL], world), Vector3.Transform(_h[_rig.FootR], world) };
        // the contact point of a planted foot: the ball while the heel is lifted (toe-off), else the ankle (heel down);
        // its motion is measured frame to frame on the same point (never across a switch between the two)
        int[] ballB = { _rig.BallL, _rig.BallR }, ankB = { _rig.FootL, _rig.FootR };
        var heelDown = new bool[2];
        for (int f = 0; f < 2; f++)
        {
            var ankleW = Vector3.Transform(_h[ankB[f]], world);
            var ballW = Vector3.Transform(_h[ballB[f]], world);
            float ballLift = _h[ballB[f]].Y - _s.Head[ballB[f]].Y, ankleLift = _h[ankB[f]].Y - _s.Head[ankB[f]].Y;
            bool onBall = ankleLift > ballLift + 0.01f;
            heelDown[f] = !onBall;
            if (_havePrev && dt > 0 && (contacts & (1 << f)) != 0 && moving)
            {
                var dv = onBall ? ballW - _prevBall[f] : ankleW - _prevAnkle[f]; dv.Y = 0;
                Slide.AddBefore(dv.Length() / dt);
            }
            _prevAnkle[f] = ankleW; _prevBall[f] = ballW;
        }
        _havePrev = true;
        if (!inp.FootLock) return;
        int[] thigh = { _rig.ThighL, _rig.ThighR }, calf = { _rig.CalfL, _rig.CalfR }, foot = { _rig.FootL, _rig.FootR };
        for (int f = 0; f < 2; f++)
        {
            // the ankle is pinned while the foot is planted with the heel down; when the heel lifts (toe-off) the lock
            // lets go over 0.12 s, so the foot rolls over the ball instead of the leg over-stretching
            bool planted = (contacts & (1 << f)) != 0 && heelDown[f];
            if (planted && !_locked[f]) { _locked[f] = true; _lock[f] = ankles[f]; }
            if (!planted && _locked[f] && _lockW[f] <= 0) _locked[f] = false;
            _lockW[f] = planted ? MathF.Min(1, _lockW[f] + dt / 0.06f) : MathF.Max(0, _lockW[f] - dt / 0.12f);
            if (_lockW[f] > 0 && _locked[f])
            {
                // horizontal pin only: the clip keeps the ankle's height (heel strike and roll)
                var pin = new Vector3(_lock[f].X, ankles[f].Y, _lock[f].Z);
                var target = Vector3.Transform(pin, inv);
                Ik.TwoBone(_s, _out, _g, _h, thigh[f], calf[f], foot[f], target, _lockW[f], keepEndRotation: true);
            }
        }
        // after locking: frame-to-frame horizontal motion of a fully locked planted foot (world space)
        for (int f = 0; f < 2; f++)
        {
            bool fully = moving && (contacts & (1 << f)) != 0 && heelDown[f] && _lockW[f] >= 1f;
            var now = Vector3.Transform(_h[foot[f]], world);
            if (fully && _wasLocked[f] && dt > 0)
            {
                var dv = now - _lockedPrev[f]; dv.Y = 0;
                Slide.AddAfter(dv.Length() / dt);
            }
            _wasLocked[f] = fully; _lockedPrev[f] = now;
        }
    }
    readonly bool[] _wasLocked = new bool[2];
    readonly Vector3[] _lockedPrev = new Vector3[2];
}

/// <summary>Accumulated planted-foot slide speeds (m/s).</summary>
public sealed class SlideStats
{
    readonly List<float> _before = new(), _after = new();
    public void AddBefore(float v) => _before.Add(v);
    public void AddAfter(float v) => _after.Add(v);
    public int Samples => _before.Count;
    public float MeanBefore => _before.Count == 0 ? 0 : _before.Average();
    public float MeanAfter => _after.Count == 0 ? 0 : _after.Average();
    public float P95Before => Pct(_before, 0.95f);
    public float P95After => Pct(_after, 0.95f);
    static float Pct(List<float> l, float p) { if (l.Count == 0) return 0; var a = l.ToArray(); Array.Sort(a); return a[Math.Min(a.Length - 1, (int)(p * a.Length))]; }
    public void Clear() { _before.Clear(); _after.Clear(); }
}
