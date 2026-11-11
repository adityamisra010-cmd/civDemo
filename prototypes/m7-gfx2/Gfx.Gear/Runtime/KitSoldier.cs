using Gfx.Chars.Animation;
using Gfx.Chars.Rendering;
using Gfx.Gear.Items;
using Gfx.Gear.Rig;
using Microsoft.Xna.Framework;

namespace Gfx.Gear.Runtime;

/// <summary>What one soldier carries: helmet (worn), right-hand item, left-hand item or shield and how it is held, carried items.</summary>
public sealed class Loadout
{
    public KitItem? Helmet;
    public KitItem? Right;
    /// <summary>Right-hand frame: "grip" (thumb towards the business end) or "grip_over" (overhand: thumb towards the butt).</summary>
    public string RightFrame = "grip";
    public KitItem? Left;
    public LeftHold Mode = LeftHold.None;
    /// <summary>Thickness (m) of the sleeves and padding over the shield forearm (mail and gambeson about 0.025, a bare arm 0).</summary>
    public float SleeveThickness;
    /// <summary>Items hanging from sockets (a sheathed sword on the hip, a quiver): (item, frame, socket).</summary>
    public List<(KitItem item, string frame, string socket)> Carried { get; } = new();
}

/// <summary>
/// A kitted soldier: a character instance, its worn kit (helmet with the hair under it hidden, carried items), the
/// props in its hands, the hold constraint and the driver that animates it. Each update advances the driver, solves
/// the pose, then places the held props from the sockets; released props fall and come to rest on the ground
/// (a fixed, deterministic drop: no physics, no randomness). PRESENTATION ONLY.
/// </summary>
public sealed class KitSoldier
{
    public CharInstance Body { get; }
    public KitHold Hold { get; }
    public Loadout Kit { get; }
    public CharInstance? RightProp { get; }
    public CharInstance? LeftProp { get; }
    public AnimDriver Driver { get; private set; } = null!;
    /// <summary>Central-grip shields: which way the face turns (world direction); null = hanging by gravity.</summary>
    public Func<float, Vector3?> FaceDir = _ => null;
    /// <summary>Scene time at which each hand lets go of its prop (null = never).</summary>
    public float? ReleaseRight, ReleaseLeft;
    /// <summary>Ground height under the soldier (for dropped props).</summary>
    public Func<Vector3, float> Ground = _ => 0f;
    Matrix _relR, _relL; bool _haveRelR, _haveRelL;

    public KitSoldier(KitWorld world, CharInstance body, Loadout kit, string wornId)
    {
        Body = body; Kit = kit;
        var sockets = KitSockets.For(body.Asset);
        Hold = new KitHold(sockets) { Right = kit.Right, RightFrame = kit.RightFrame, Left = kit.Left, Mode = kit.Mode };
        if (kit.Mode == LeftHold.Strap && kit.Left != null)
        {
            var (w, slide, _) = KitHold.FitStrap(body.Asset.Skeleton, sockets, kit.Left, kit.SleeveThickness);
            Hold.StrapWrist = w; Hold.StrapSlide = slide; Hold.StrapStandoff = kit.SleeveThickness;
        }
        var worn = new List<(KitItem, string, KitSocket)>();
        if (kit.Helmet != null) worn.Add((kit.Helmet, "head", sockets["head"]));
        foreach (var (item, frame, socket) in kit.Carried) worn.Add((item, frame, sockets[socket]));
        if (worn.Count > 0)
            world.Wear(body, wornId, worn, kit.Helmet != null ? (it, p) => it == kit.Helmet && Helmets.Covers(it, p) : null);
        if (kit.Right != null) RightProp = world.AddProp(kit.Right, Matrix.Identity);
        if (kit.Left != null) LeftProp = world.AddProp(kit.Left, Matrix.Identity);
    }

    /// <summary>Binds the driver (the soldier's script); the hold constraint is added to every input it produces.</summary>
    public KitSoldier Drive(Func<float, AnimInput> script, Func<float, Matrix> place, float phase = 0f)
    {
        Driver = new AnimDriver(Body, t => { var i = script(t); i.Constraint = Hold; return i; }, place, phase);
        return this;
    }

    public void Update(float t)
    {
        Hold.RightWeightAt = tt => ReleaseRight is { } rr0 && tt >= rr0 ? 0f : 1f;
        Hold.LeftWeightAt = tt => ReleaseLeft is { } rl0 && tt >= rl0 ? 0f : 1f;
        // a released prop starts its fall from where it was at the release time (computed at exactly that time)
        if (ReleaseRight is { } rr1 && t >= rr1 && !_haveRelR) { Driver.Advance(rr1); Body.SolvePose(); _relR = Hold.ItemWorlds(Body, FaceDir(rr1)).right ?? Matrix.Identity; _haveRelR = true; }
        if (ReleaseLeft is { } rl1 && t >= rl1 && !_haveRelL) { Driver.Advance(rl1); Body.SolvePose(); _relL = Hold.ItemWorlds(Body, FaceDir(rl1)).left ?? Matrix.Identity; _haveRelL = true; }
        Driver.Advance(t);
        bool relR = ReleaseRight is { } rr && t >= rr, relL = ReleaseLeft is { } rl && t >= rl;
        Body.SolvePose();
        var (r, l) = Hold.ItemWorlds(Body, FaceDir(t));
        if (RightProp != null && r is { } wr)
        {
            if (!relR) { RightProp.World = wr; }
            else RightProp.World = Drop(Kit.Right!, ref _relR, ref _haveRelR, wr, t - ReleaseRight!.Value);
        }
        if (LeftProp != null && l is { } wl)
        {
            if (!relL) { LeftProp.World = wl; }
            else LeftProp.World = Drop(Kit.Left!, ref _relL, ref _haveRelL, wl, t - ReleaseLeft!.Value);
        }
    }

    /// <summary>
    /// A dropped item: from the transform it had when released, it falls under gravity for up to 0.45 s while turning
    /// to lie flat (its long axis level, its widest face down) at the ground. Deterministic in the elapsed time.
    /// </summary>
    Matrix Drop(KitItem item, ref Matrix released, ref bool have, Matrix current, float dt)
    {
        if (!have) { released = current; have = true; }
        float fall = Math.Clamp(dt / 0.45f, 0f, 1f);
        float g = fall * fall;
        var p0 = released.Translation;
        // lying: keep the yaw of the item's long axis, lay it flat, the centre of mass at the ground plus its half thickness
        var axis = Vector3.Normalize(Vector3.TransformNormal(Vector3.UnitY, released));
        var flatAxis = new Vector3(axis.X, 0, axis.Z);
        if (flatAxis.LengthSquared() < 1e-4f) flatAxis = Vector3.UnitX;
        flatAxis.Normalize();
        var bb = item.Parts[0].Mesh.Bounds();
        float thick = MathF.Min(bb.Max.X - bb.Min.X, bb.Max.Z - bb.Min.Z) * 0.5f;
        bool shield = item.Kind.StartsWith("shield", StringComparison.Ordinal);
        Matrix lie;
        if (shield)
        {
            // shields land face up
            var faceUp = Matrix.CreateRotationX(-MathHelper.PiOver2);
            float yaw = MathF.Atan2(flatAxis.X, flatAxis.Z);
            lie = faceUp * Matrix.CreateRotationY(yaw);
            float minY = item.Parts[0].Mesh.Positions.Min(q => Vector3.Transform(q, lie).Y);
            lie *= Matrix.CreateTranslation(p0.X, Ground(p0) - minY + 0.004f, p0.Z);
        }
        else
        {
            var right = Vector3.Normalize(Vector3.Cross(Vector3.UnitY, flatAxis));
            lie = new Matrix(right.X, right.Y, right.Z, 0, flatAxis.X, flatAxis.Y, flatAxis.Z, 0, 0, 1, 0, 0, 0, 0, 0, 1);
            float minY = item.Parts[0].Mesh.Positions.Min(q => Vector3.Transform(q, lie).Y);
            lie *= Matrix.CreateTranslation(p0.X, Ground(p0) - minY + 0.003f, p0.Z);
        }
        _ = thick;
        if (fall >= 1f) return lie;
        // blend rotation (nlerp) and position (gravity on the height, linear across)
        released.Decompose(out var s0, out var q0, out var t0);
        lie.Decompose(out var s1, out var q1, out var t1);
        var q = Quaternion.Lerp(q0, q1, g); q.Normalize();
        var pos = new Vector3(MathHelper.Lerp(t0.X, t1.X, fall), MathHelper.Lerp(t0.Y, t1.Y, g), MathHelper.Lerp(t0.Z, t1.Z, fall));
        return Matrix.CreateFromQuaternion(q) * Matrix.CreateTranslation(pos);
    }
}
