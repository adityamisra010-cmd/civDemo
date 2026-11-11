using Gfx.Chars.Animation;
using Gfx.Chars.Rendering;
using Gfx.Gear.Items;
using Microsoft.Xna.Framework;

namespace Gfx.Gear.Rig;

/// <summary>How the left hand holds the shield or the weapon.</summary>
public enum LeftHold
{
    /// <summary>Nothing: the hand keeps the animation's pose (relaxed or by the stance).</summary>
    None,
    /// <summary>Strap shield (aspis, kite, heater): fixed to the forearm by the arm band, the fist round the hand grip.</summary>
    Strap,
    /// <summary>Central-grip shield (thureos, round): held by a bar behind the boss; the board turns about the bar.</summary>
    Central,
    /// <summary>The off hand on the right-hand weapon's second grip (spear held two-handed, bow string, musket).</summary>
    TwoHanded,
    /// <summary>An item in the left fist by its grip frame (a bow).</summary>
    Hand,
}

/// <summary>
/// The pose constraint that makes characters hold their kit (DR42: cosmetic only): wraps the right fist round the
/// weapon's grip, and for the left hand either fixes the wrist so the fist closes on a strap shield's hand grip (the
/// shield is fixed to the forearm), closes it on a central-grip shield's bar, or reaches the weapon's second grip by
/// two-bone arm IK with the hand turned to the grip's frame. Runs last in the animator step, in character space.
/// </summary>
public sealed class KitHold : IPoseConstraint
{
    public readonly IReadOnlyDictionary<string, KitSocket> Sockets;
    public KitItem? Right;
    public string RightFrame = "grip";
    public KitItem? Left;
    public LeftHold Mode;
    /// <summary>Strap shields: the wrist rotation (hand local) and the slide of the shield along the forearm (m), from <see cref="FitStrap"/>.</summary>
    public Quaternion StrapWrist = Quaternion.Identity;
    public float StrapSlide;
    /// <summary>Strap shields: extra distance (m) between the forearm's skin and the board for sleeves and padding (mail, gambeson).</summary>
    public float StrapStandoff;
    /// <summary>Weights 0..1 of each hand's hold (fade a hold in or out, e.g. when a weapon is dropped).</summary>
    public float RightWeight = 1f, LeftWeight = 1f;
    /// <summary>Time-based weights (scene time), used instead of the fixed ones when set: deterministic under rewinds.</summary>
    public Func<float, float>? RightWeightAt, LeftWeightAt;
    /// <summary>Close the fists round the grips (false = the animation's own finger pose).</summary>
    public bool WrapRight = true, WrapLeft = true;
    /// <summary>Last wrap error per hand (m), for tests and the costs report.</summary>
    public float LastRightError { get; private set; }
    public float LastLeftError { get; private set; }
    /// <summary>Last two-handed reach error (m) of the left hand's IK.</summary>
    public float LastReachError { get; private set; }

    public KitHold(IReadOnlyDictionary<string, KitSocket> sockets) { Sockets = sockets; }

    public void Apply(CharRig rig, Pose p, Quaternion[] g, Vector3[] h, in Matrix world, float t)
    {
        var s = rig.S;
        if (RightWeightAt != null) RightWeight = RightWeightAt(t);
        if (LeftWeightAt != null) LeftWeight = LeftWeightAt(t);
        // ---- left hand (first: a two-handed hold depends on the right hand only, which is not changed by the left)
        if (Left != null && Mode == LeftHold.Strap && LeftWeight > 0)
        {
            p.Local[rig.HandL] = Q.Nlerp(p.Local[rig.HandL], StrapWrist, LeftWeight);
            Ik.Refresh(s, p, g, h, rig.HandL);
            if (WrapLeft)
            {
                var a = StrapFrameLocal(s, g, h, "grip");
                LastLeftError = Grip.Wrap(rig, p, g, h, true, a.Translation, a.Up, Left.GripRadius);
            }
        }
        else if (Left != null && (Mode == LeftHold.Central || Mode == LeftHold.Hand) && LeftWeight > 0 && WrapLeft)
        {
            var sk = Sockets["grip_l"].Local(s, g, h);
            LastLeftError = Grip.Wrap(rig, p, g, h, true, sk.Translation, sk.Up, Left.GripRadius);
        }
        // ---- right hand
        if (Right != null && RightWeight > 0 && WrapRight)
        {
            var sk = Sockets["grip_r"].Local(s, g, h);
            LastRightError = Grip.Wrap(rig, p, g, h, false, sk.Translation, sk.Up, Right.GripRadius);
        }
        if (Right != null && Mode == LeftHold.TwoHanded && LeftWeight > 0 && Right.Frames.ContainsKey("grip2"))
        {
            var wr = Matrix.Invert(Right.Frame(RightFrame)) * Sockets["grip_r"].Local(s, g, h);
            var g2 = Right.Frame("grip2") * wr;
            LastReachError = PlaceHand(rig, p, g, h, true, g2, LeftWeight);
            if (WrapLeft) LastLeftError = Grip.Wrap(rig, p, g, h, true, g2.Translation, g2.Up, Right.Grip2Radius);
        }
    }

    /// <summary>A strap shield's frame (e.g. "grip", the hand grip) in character space for the current pose.</summary>
    public Matrix StrapFrameLocal(Skeleton s, Quaternion[] g, Vector3[] h, string frame)
        => Left!.Frame(frame) * StrapWorldLocal(s, g, h);

    /// <summary>The strap shield's item-to-character transform for the current pose.</summary>
    public Matrix StrapWorldLocal(Skeleton s, Quaternion[] g, Vector3[] h)
        => Matrix.Invert(Left!.Frame("forearm")) * Matrix.CreateTranslation(0, StrapSlide, StrapStandoff) * Sockets["forearm_l"].Local(s, g, h);

    /// <summary>
    /// Moves the left (or right) hand so its grip socket lands on a target frame (character space): two-bone arm IK for
    /// the wrist, then the hand turned so the socket's axes match the frame's. Returns the remaining position error (m).
    /// </summary>
    public float PlaceHand(CharRig rig, Pose p, Quaternion[] g, Vector3[] h, bool left, Matrix target, float weight)
    {
        var s = rig.S;
        var sock = Sockets[left ? "grip_l" : "grip_r"];
        int hand = left ? rig.HandL : rig.HandR, upper = left ? rig.UpperL : rig.UpperR, lower = left ? rig.LowerL : rig.LowerR;
        var rs = Rot(sock.Bind); var rt = Rot(target);
        var r = Matrix.Transpose(rs) * rt;                              // hand rotation taking the socket's axes to the target's
        var offset = Vector3.Transform(sock.Origin - s.Head[hand], r);  // hand head -> grip, posed
        var wrist = target.Translation - offset;
        var pole = left ? new Vector3(1f, -0.6f, -0.3f) : new Vector3(-1f, -0.6f, -0.3f);
        Ik.TwoBone(s, p, g, h, upper, lower, hand, Vector3.Lerp(h[hand], wrist, weight), 1f, keepEndRotation: true, pole: Q.Rotate(g[rig.Spine3], pole));
        var want = Quaternion.CreateFromRotationMatrix(r); want.Normalize();
        var cur = g[hand];
        Ik.RotateGlobal(s, p, g, h, hand, Q.Mul(Q.Nlerp(cur, want, weight), Q.Inverse(cur)));
        var sockNow = sock.Local(s, g, h).Translation;
        return (sockNow - target.Translation).Length();
    }

    static Matrix Rot(Matrix m) { var r = m; r.Translation = Vector3.Zero; return r; }

    /// <summary>
    /// Fits a strap shield to an identity in the bind pose: the wrist rotation that turns the fist's grip frame onto the
    /// shield's hand grip, and the slide along the forearm (the arm band holds the forearm; the forearm slides in it)
    /// that brings the hand grip to the fist. Returns (wrist, slide, remaining distance between fist and hand grip).
    /// </summary>
    public static (Quaternion wrist, float slide, float residual) FitStrap(Skeleton s, IReadOnlyDictionary<string, KitSocket> sockets, KitItem shield, float standoff = 0f)
    {
        int hand = s["hand_l"];
        var fore = sockets["forearm_l"]; var grip = sockets["grip_l"];
        var axis = fore.AxisY;
        float slide = 0f;
        Matrix r = Matrix.Identity; float residual = 0;
        for (int it = 0; it < 4; it++)
        {
            var w = Matrix.Invert(shield.Frame("forearm")) * Matrix.CreateTranslation(0, slide, standoff) * fore.Bind;
            var a = shield.Frame("grip") * w;
            r = Matrix.Transpose(Rot(grip.Bind)) * Rot(a);
            var fist = s.Head[hand] + Vector3.Transform(grip.Origin - s.Head[hand], r);
            var e = a.Translation - fist;
            slide -= Vector3.Dot(e, axis);
            residual = (e - axis * Vector3.Dot(e, axis)).Length();
        }
        var q = Quaternion.CreateFromRotationMatrix(r); q.Normalize();
        return (q, slide, residual);
    }

    /// <summary>
    /// World transforms of the held items for a solved instance (call after the animator step and inst.SolvePose()):
    /// right-hand item at its grip, strap shield on the forearm, central-grip shield turned about its bar so its face
    /// points along <paramref name="faceDir"/> (world; null = hanging by gravity).
    /// </summary>
    public (Matrix? right, Matrix? left) ItemWorlds(CharInstance inst, Vector3? faceDir = null)
    {
        Matrix? r = null, l = null;
        if (Right != null) r = Matrix.Invert(Right.Frame(RightFrame)) * Sockets["grip_r"].World(inst);
        if (Left != null)
        {
            if (Mode == LeftHold.Strap)
                l = Matrix.Invert(Left.Frame("forearm")) * Matrix.CreateTranslation(0, StrapSlide, StrapStandoff) * Sockets["forearm_l"].World(inst);
            else if (Mode == LeftHold.Hand)
                l = Matrix.Invert(Left.Frame("grip")) * Sockets["grip_l"].World(inst);
            else if (Mode == LeftHold.Central)
            {
                var sw = Sockets["grip_l"].World(inst);
                var f = Left.Frame("grip");
                // the face normal (item +Z) in the grip frame, turned about the bar (grip +Y) to face the wanted way
                var nItem = Vector3.TransformNormal(Vector3.UnitZ, Matrix.Invert(f));
                var bar = Vector3.Normalize(sw.Up);
                var want = faceDir ?? -Vector3.UnitY; // by gravity: the board hangs with its centre below the bar
                if (faceDir == null)
                {
                    // the centre of mass below the bar: turn so the item's centre (from the grip) points down
                    var cm = Vector3.TransformNormal(Left.CentreOfMass - f.Translation, Matrix.Invert(f with { Translation = Vector3.Zero }));
                    nItem = cm.LengthSquared() > 1e-8f ? cm : nItem;
                }
                float spin = SpinAbout(Vector3.TransformNormal(nItem, Rot(sw)), bar, want);
                l = Matrix.Invert(f) * Matrix.CreateFromAxisAngle(Vector3.UnitY, spin) * sw;
            }
        }
        return (r, l);
    }

    /// <summary>Angle about unit axis a that turns vector v (world) closest to direction d.</summary>
    static float SpinAbout(Vector3 v, Vector3 a, Vector3 d)
    {
        var vp = v - a * Vector3.Dot(v, a); var dp = d - a * Vector3.Dot(d, a);
        if (vp.LengthSquared() < 1e-10f || dp.LengthSquared() < 1e-10f) return 0f;
        vp.Normalize(); dp.Normalize();
        return MathF.Atan2(Vector3.Dot(Vector3.Cross(vp, dp), a), Vector3.Dot(vp, dp));
    }
}
