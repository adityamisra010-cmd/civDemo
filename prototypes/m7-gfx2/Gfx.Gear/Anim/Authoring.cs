using Gfx.Chars.Animation;
using Gfx.Chars.Format;
using Gfx.Gear.Rig;
using Microsoft.Xna.Framework;

namespace Gfx.Gear.Anim;

/// <summary>Easing of the segment that ends at a key.</summary>
public enum Ease { Linear, In, Out, InOut, Snap, Settle }

/// <summary>
/// A hand target in character space (x = the character's left, y up, z forward, origin on the ground under the rest
/// pelvis). <c>Dir</c> is the grip axis (the fist's little-finger to thumb direction, the weapon's axis), <c>Palm</c> the
/// direction the palm faces (completes the hand's frame).
/// </summary>
public sealed record HandKey(Vector3 Pos, Vector3 Dir, Vector3 Palm, float Weight = 1f, Vector3? Pole = null);

/// <summary>A foot target in character space (the ankle's position), its yaw (degrees) and heel lift (degrees of toe-down pitch).</summary>
public sealed record FootKey(Vector3 Pos, float Yaw = 0f, float Heel = 0f, bool Planted = true, float Weight = 1f);

/// <summary>
/// One key of an authored clip. Angles in degrees. Pelvis: offset from the rest pelvis (character space) and yaw, pitch
/// (forward +), roll (to the right +). Spine: twist (yaw), bend (pitch, forward +), side bend (roll) shared by
/// spine_01..03 (25/35/40 %). Head: yaw and pitch shared by neck and head. Hands and feet: targets (null = free).
/// </summary>
public sealed class Key
{
    public float T;
    public Ease Ease = Ease.InOut;
    public Vector3 Pelvis;
    public float PelvisYaw, PelvisPitch, PelvisRoll;
    public float SpineYaw, SpinePitch, SpineRoll;
    public float HeadYaw, HeadPitch;
    public HandKey? R, L;
    public FootKey? FL, FR;
    /// <summary>Elbow and knee helpers: extra elbow drop, knee bend for free legs (degrees).</summary>
    public float KneeL, KneeR;
    /// <summary>Upper-arm swing (degrees, about the shoulder's x axis) when a hand is free.</summary>
    public float ArmL, ArmR;
    public string? Expression;
    public Key Clone() => (Key)MemberwiseClone();
}

/// <summary>An authored clip: named keys, looping or one-shot, baked per skeleton by <see cref="ClipBaker"/>.</summary>
public sealed class Authored
{
    public required string Name { get; init; }
    public bool Loop { get; init; }
    public List<Key> Keys { get; } = new();
    /// <summary>Notes for the report (what the clip shows).</summary>
    public string Note { get; init; } = "";
    /// <summary>Scene-time events (seconds into the clip): "release_r" (the right hand lets go of its weapon), "loose" (an arrow leaves the bow), "impact".</summary>
    public List<(float t, string evt)> Events { get; } = new();
    public float Duration => Keys.Count == 0 ? 0 : Keys[^1].T;

    public Authored Add(Key k) { Keys.Add(k); return this; }
    /// <summary>Copies the previous key with changes applied (time absolute).</summary>
    public Authored Then(float t, Ease e, Action<Key> change)
    {
        var k = Keys[^1].Clone();
        k.T = t; k.Ease = e;
        change(k);
        Keys.Add(k);
        return this;
    }
}

/// <summary>Evaluates authored keys at a time (eased per segment; hand paths through Catmull-Rom so swings arc).</summary>
public static class KeyEval
{
    public static float Apply(Ease e, float s)
    {
        s = Math.Clamp(s, 0f, 1f);
        return e switch
        {
            Ease.Linear => s,
            Ease.In => s * s,
            Ease.Out => 1 - (1 - s) * (1 - s),
            Ease.Snap => 1 - MathF.Pow(1 - s, 3.2f),                 // fast start: strikes
            Ease.Settle => 1 - MathF.Pow(1 - s, 2) * MathF.Cos(s * 5.0f) * (1 - s * 0.3f) * 1f, // a slight overshoot settling
            _ => s * s * (3 - 2 * s),
        };
    }

    public static (int i, float s) Segment(Authored a, float t)
    {
        var k = a.Keys;
        if (t <= k[0].T) return (0, 0);
        for (int i = 0; i < k.Count - 1; i++)
            if (t < k[i + 1].T) return (i, Apply(k[i + 1].Ease, (t - k[i].T) / Math.Max(1e-5f, k[i + 1].T - k[i].T)));
        return (k.Count - 2, 1);
    }

    static Vector3 CatmullRom(Vector3 p0, Vector3 p1, Vector3 p2, Vector3 p3, float t)
    {
        float t2 = t * t, t3 = t2 * t;
        return 0.5f * (2 * p1 + (p2 - p0) * t + (2 * p0 - 5 * p1 + 4 * p2 - p3) * t2 + (3 * p1 - p0 - 3 * p2 + p3) * t3);
    }

    /// <summary>The interpolated key at time t.</summary>
    public static Key At(Authored a, float t)
    {
        var (i, s) = Segment(a, t);
        var k0 = a.Keys[i]; var k1 = a.Keys[Math.Min(i + 1, a.Keys.Count - 1)];
        var km = a.Keys[Math.Max(i - 1, 0)]; var kp = a.Keys[Math.Min(i + 2, a.Keys.Count - 1)];
        float L(float x, float y) => x + (y - x) * s;
        var o = new Key
        {
            T = t, Pelvis = Vector3.Lerp(k0.Pelvis, k1.Pelvis, s),
            PelvisYaw = L(k0.PelvisYaw, k1.PelvisYaw), PelvisPitch = L(k0.PelvisPitch, k1.PelvisPitch), PelvisRoll = L(k0.PelvisRoll, k1.PelvisRoll),
            SpineYaw = L(k0.SpineYaw, k1.SpineYaw), SpinePitch = L(k0.SpinePitch, k1.SpinePitch), SpineRoll = L(k0.SpineRoll, k1.SpineRoll),
            HeadYaw = L(k0.HeadYaw, k1.HeadYaw), HeadPitch = L(k0.HeadPitch, k1.HeadPitch),
            KneeL = L(k0.KneeL, k1.KneeL), KneeR = L(k0.KneeR, k1.KneeR), ArmL = L(k0.ArmL, k1.ArmL), ArmR = L(k0.ArmR, k1.ArmR),
            Expression = s < 0.5f ? k0.Expression : k1.Expression,
        };
        o.R = Hand(km.R, k0.R, k1.R, kp.R, s);
        o.L = Hand(km.L, k0.L, k1.L, kp.L, s);
        o.FL = Foot(k0.FL, k1.FL, s);
        o.FR = Foot(k0.FR, k1.FR, s);
        return o;
    }

    static HandKey? Hand(HandKey? m, HandKey? a, HandKey? b, HandKey? p, float s)
    {
        if (a == null && b == null) return null;
        if (a == null) return b! with { Weight = b.Weight * s };
        if (b == null) return a with { Weight = a.Weight * (1 - s) };
        var pos = CatmullRom((m ?? a).Pos, a.Pos, b.Pos, (p ?? b).Pos, s);
        var dir = Slerp(a.Dir, b.Dir, s); var palm = Slerp(a.Palm, b.Palm, s);
        Vector3? pole = a.Pole is { } pa && b.Pole is { } pb ? Vector3.Lerp(pa, pb, s) : a.Pole ?? b.Pole;
        return new HandKey(pos, dir, palm, a.Weight + (b.Weight - a.Weight) * s, pole);
    }

    static FootKey? Foot(FootKey? a, FootKey? b, float s)
    {
        if (a == null || b == null) return a ?? b;
        // a stepping foot lifts on the way (a 6 cm arc)
        var pos = Vector3.Lerp(a.Pos, b.Pos, s);
        float stepLen = new Vector2(b.Pos.X - a.Pos.X, b.Pos.Z - a.Pos.Z).Length();
        if (stepLen > 0.05f) pos.Y += 0.06f * MathF.Sin(MathF.PI * s) * Math.Min(1f, stepLen / 0.2f);
        return new FootKey(pos, a.Yaw + (b.Yaw - a.Yaw) * s, a.Heel + (b.Heel - a.Heel) * s, s < 0.5f ? a.Planted : b.Planted, a.Weight + (b.Weight - a.Weight) * s);
    }

    public static Vector3 Slerp(Vector3 a, Vector3 b, float s)
    {
        a.Normalize(); b.Normalize();
        float d = Math.Clamp(Vector3.Dot(a, b), -1f, 1f);
        if (d > 0.9995f) return Vector3.Normalize(Vector3.Lerp(a, b, s));
        float th = MathF.Acos(d);
        if (th > 3.1f) { var perp = Vector3.Cross(a, Vector3.UnitY); if (perp.LengthSquared() < 1e-6f) perp = Vector3.UnitX; a = Vector3.Normalize(a + perp * 0.01f); d = Vector3.Dot(a, b); th = MathF.Acos(Math.Clamp(d, -1f, 1f)); }
        float sn = MathF.Sin(th);
        return (MathF.Sin((1 - s) * th) / sn) * a + (MathF.Sin(s * th) / sn) * b;
    }
}

/// <summary>
/// Bakes authored clips into the character format's clips for one skeleton (30 frames per second): per frame the key
/// is evaluated, the body is posed (pelvis, spine and head rotations), the hands are placed by two-bone arm IK with the
/// grip socket turned to the key's frame, the feet by two-bone leg IK with heel lift, and the body is kept above the
/// ground (the lowest of the head, chest, pelvis, hands, knees and feet, with their radii). The result plays through
/// the stage-chars animator like any clip (crossfades, upper-body layers, hit additives). PRESENTATION ONLY.
/// </summary>
public static class ClipBaker
{
    public const float Fps = 30f;

    public static ClipDef Bake(Authored a, Skeleton s, IReadOnlyDictionary<string, KitSocket> sockets)
    {
        int frames = Math.Max(2, (int)MathF.Round(a.Duration * Fps) + 1);
        var rig = new CharRig(s);
        var pose = new Pose(s.Count);
        var g = new Quaternion[s.Count]; var h = new Vector3[s.Count];
        var rot = new Quaternion[frames * s.Count]; var pel = new Vector3[frames]; var con = new byte[frames];
        var hold = new KitHold(sockets);
        for (int f = 0; f < frames; f++)
        {
            float t = Math.Min(a.Duration, f / Fps);
            var k = KeyEval.At(a, t);
            PoseFrame(k, s, rig, pose, g, h, hold);
            for (int b = 0; b < s.Count; b++) rot[f * s.Count + b] = pose.Local[b];
            pel[f] = pose.Pelvis;
            byte c = 0;
            // a foot is planted (and may be locked by the animator) only while its target holds it
            if (k.FL is { Planted: true, Weight: > 0.5f }) c |= 1;
            if (k.FR is { Planted: true, Weight: > 0.5f }) c |= 2;
            con[f] = c;
        }
        return new ClipDef
        {
            Name = a.Name, Loop = a.Loop, Fps = Fps, Frames = frames, Bones = s.Count, Speed = 0f, Rotations = rot, Pelvis = pel, Contacts = con,
            Source = "authored in code (Gfx.Gear/Anim): " + a.Note,
        };
    }

    static Quaternion Euler(float yawDeg, float pitchDeg, float rollDeg)
    {
        // yaw about +Y, then pitch about +X (forward bend), then roll about +Z (to the right): character axes
        var y = Quaternion.CreateFromAxisAngle(Vector3.UnitY, MathHelper.ToRadians(yawDeg));
        var x = Quaternion.CreateFromAxisAngle(Vector3.UnitX, MathHelper.ToRadians(pitchDeg));
        var z = Quaternion.CreateFromAxisAngle(Vector3.UnitZ, -MathHelper.ToRadians(rollDeg));
        return Q.Mul(y, Q.Mul(x, z));
    }

    /// <summary>Poses one frame from an evaluated key into (pose, globals, heads).</summary>
    public static void PoseFrame(Key k, Skeleton s, CharRig rig, Pose pose, Quaternion[] g, Vector3[] h, KitHold hold)
    {
        pose.Reset();
        // all targets are in character space; the pelvis yaw turns the body above the feet
        var stance = Quaternion.CreateFromAxisAngle(Vector3.UnitY, MathHelper.ToRadians(k.PelvisYaw));
        pose.Pelvis = k.Pelvis;
        // the pelvis carries the stance yaw and its own pitch and roll
        pose.Local[rig.Pelvis] = Q.Mul(stance, Euler(0, k.PelvisPitch, k.PelvisRoll));
        // spine: shared out; rotations are local to the parent's frame, which already carries the pelvis
        float[] share = { 0.25f, 0.35f, 0.40f };
        int[] spine = { rig.Spine1, rig.Spine2, rig.Spine3 };
        for (int i = 0; i < 3; i++) pose.Local[spine[i]] = Euler(k.SpineYaw * share[i], k.SpinePitch * share[i], k.SpineRoll * share[i]);
        pose.Local[rig.Neck] = Euler(k.HeadYaw * 0.4f, k.HeadPitch * 0.4f, 0);
        pose.Local[rig.Head] = Euler(k.HeadYaw * 0.6f, k.HeadPitch * 0.6f, 0);
        // free arms hang (A-pose rest lowered towards the sides) with a swing
        pose.Local[rig.UpperL] = Q.Mul(Quaternion.CreateFromAxisAngle(Vector3.UnitZ, MathHelper.ToRadians(-28f)), Quaternion.CreateFromAxisAngle(Vector3.UnitX, -MathHelper.ToRadians(k.ArmL)));
        pose.Local[rig.UpperR] = Q.Mul(Quaternion.CreateFromAxisAngle(Vector3.UnitZ, MathHelper.ToRadians(28f)), Quaternion.CreateFromAxisAngle(Vector3.UnitX, -MathHelper.ToRadians(k.ArmR)));
        // knees for free legs (a positive bend takes the foot backwards)
        pose.Local[rig.CalfL] = Quaternion.CreateFromAxisAngle(Vector3.UnitX, MathHelper.ToRadians(k.KneeL));
        pose.Local[rig.CalfR] = Quaternion.CreateFromAxisAngle(Vector3.UnitX, MathHelper.ToRadians(k.KneeR));
        Fk.Solve(s, pose, g, h, rig.Pelvis);
        // legs: ankles to their targets (stance space -> character space), knees forward
        foreach (var (fk, thigh, calf, foot, ball) in new[] { (k.FL, rig.ThighL, rig.CalfL, rig.FootL, rig.BallL), (k.FR, rig.ThighR, rig.CalfR, rig.FootR, rig.BallR) })
        {
            if (fk == null || fk.Weight <= 0) continue;
            var target = fk.Pos;
            var yawQ = Quaternion.CreateFromAxisAngle(Vector3.UnitY, MathHelper.ToRadians(fk.Yaw));
            var pole = Vector3.Transform(new Vector3(0, 0, 1), yawQ);
            Ik.TwoBone(s, pose, g, h, thigh, calf, foot, target, fk.Weight, keepEndRotation: false, pole: pole);
            // the foot level with the ground, turned by its yaw, toes down by the heel lift
            var want = Q.Mul(yawQ, Quaternion.CreateFromAxisAngle(Vector3.UnitX, MathHelper.ToRadians(fk.Heel)));
            var cur = g[foot];
            Ik.RotateGlobal(s, pose, g, h, foot, Q.Mul(Q.Nlerp(cur, want, fk.Weight), Q.Inverse(cur)));
            _ = ball;
        }
        // hands: grip frames (stance space) placed by arm IK
        foreach (var (hk, left) in new[] { (k.R, false), (k.L, true) })
        {
            if (hk == null || hk.Weight <= 0) continue;
            var frame = Items.KitItem.MakeFrame(hk.Pos, hk.Dir, hk.Palm);
            hold.PlaceHand(rig, pose, g, h, left, frame, hk.Weight);
        }
        GroundClamp(s, rig, pose, g, h);
    }

    /// <summary>Lifts the body when any of its parts (with a radius) would go below the ground (y = 0 in character space).</summary>
    static void GroundClamp(Skeleton s, CharRig rig, Pose pose, Quaternion[] g, Vector3[] h)
    {
        (int bone, float r)[] parts =
        {
            (rig.Head, 0.11f), (rig.Neck, 0.07f), (rig.Spine3, 0.12f), (rig.Spine1, 0.11f), (rig.Pelvis, 0.10f),
            (rig.HandL, 0.035f), (rig.HandR, 0.035f), (rig.LowerL, 0.05f), (rig.LowerR, 0.05f), (rig.CalfL, 0.06f), (rig.CalfR, 0.06f),
            (rig.FootL, 0.05f), (rig.FootR, 0.05f), (rig.BallL, 0.02f), (rig.BallR, 0.02f),
        };
        float lift = 0;
        foreach (var (b, r) in parts) lift = Math.Max(lift, r - h[b].Y);
        // every other joint (fingers, toes, eyes) at least 1 cm above the ground
        for (int b = 0; b < s.Count; b++) lift = Math.Max(lift, 0.01f - h[b].Y);
        if (lift > 0)
        {
            pose.Pelvis += new Vector3(0, lift, 0);
            Fk.Solve(s, pose, g, h, rig.Pelvis);
        }
    }
}
