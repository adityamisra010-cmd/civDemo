using Gfx.Chars.Animation;
using Gfx.Chars.Format;
using Gfx.Chars.Rendering;
using Microsoft.Xna.Framework;

namespace Gfx.Gear.Rig;

/// <summary>An attachment frame on the character rig: a bone and a frame in bind space (world-aligned rest frames).</summary>
public sealed record KitSocket(string Name, int Bone, Matrix Bind)
{
    /// <summary>World transform of the socket for a solved instance (call inst.SolvePose() first).</summary>
    public Matrix World(CharInstance inst)
    {
        var s = inst.Asset.Skeleton;
        var skin = Matrix.CreateTranslation(-s.Head[Bone]) * Matrix.CreateFromQuaternion(inst.Global[Bone]) * Matrix.CreateTranslation(inst.Heads[Bone]);
        return Bind * skin * inst.World;
    }

    /// <summary>Character-space transform of the socket from an animator's solved arrays (no instance needed).</summary>
    public Matrix Local(Skeleton s, Quaternion[] global, Vector3[] heads)
    {
        var skin = Matrix.CreateTranslation(-s.Head[Bone]) * Matrix.CreateFromQuaternion(global[Bone]) * Matrix.CreateTranslation(heads[Bone]);
        return Bind * skin;
    }

    public Vector3 Origin => Bind.Translation;
    public Vector3 AxisX => new(Bind.M11, Bind.M12, Bind.M13);
    public Vector3 AxisY => new(Bind.M21, Bind.M22, Bind.M23);
    public Vector3 AxisZ => new(Bind.M31, Bind.M32, Bind.M33);
}

/// <summary>Body measurements of an identity in bind space, taken from its skinned body mesh (never from hand positions).</summary>
public sealed record BodyMeasure(
    float EyeY, Vector3 HeadCentre, float CrownY, float SkullBackZ, float BrowZ, float TempleHalfWidth, float ChinY,
    float HipY, float HipHalfWidth, float HipFrontZ, float HipBackZ, float BackZAtChest, float ChestY, float ForearmRadius,
    float CalfTopY, float AnkleY, float CalfRadius);

/// <summary>
/// Runtime sockets of stage kit, computed from an identity's skeleton and body (bind space, A-pose):
/// <list type="bullet">
/// <item><c>grip_r</c>, <c>grip_l</c>: the baked hand sockets (SOCK chunk), unchanged.</item>
/// <item><c>forearm_l</c>, <c>forearm_r</c>: on the back of the forearm, <see cref="PorpaxFromElbow"/> metres from the
/// elbow (the arm band of a strap shield); +Y along the forearm, +Z out of its back (the dorsal side, away from the palm).</item>
/// <item><c>hip_l</c>, <c>hip_r</c>: on the pelvis bone, at the side of the hips just below the waist, measured from the
/// body vertices weighted to the pelvis and thighs (the lost attempt's hip socket sat on the A-pose hand, 0.6 m out);
/// +Y up and slightly forward, +Z outward (away from the body's midline).</item>
/// <item><c>back</c>: on spine_03, on the back surface between the shoulder blades; +Y up, +Z out of the back.</item>
/// <item><c>head</c>: on the head bone at the skull centre (eye height + 30 mm, mid-depth of the skull at that height); +Y up, +Z forward.</item>
/// </list>
/// </summary>
public static class KitSockets
{
    public const float PorpaxFromElbow = 0.06f;

    static readonly System.Runtime.CompilerServices.ConditionalWeakTable<CharAsset, Dictionary<string, KitSocket>> Cache = new();
    static readonly System.Runtime.CompilerServices.ConditionalWeakTable<CharAsset, BodyMeasure> MeasureCache = new();

    public static IReadOnlyDictionary<string, KitSocket> For(CharAsset identity) => Cache.GetValue(identity, a => Compute(a.File, a.Skeleton));

    public static BodyMeasure Measure(CharAsset identity) => MeasureCache.GetValue(identity, a => Measure(a.File, a.Skeleton));

    public static Dictionary<string, KitSocket> Compute(CharacterFile f, Skeleton s)
    {
        var res = new Dictionary<string, KitSocket>(StringComparer.Ordinal);
        foreach (var sk in f.Sockets)
        {
            if (!sk.Name.StartsWith("grip_", StringComparison.Ordinal)) continue;
            var bind = Matrix.CreateFromQuaternion(sk.Rotation) * Matrix.CreateTranslation(sk.Position + s.Head[sk.Bone]);
            res[sk.Name] = new KitSocket(sk.Name, sk.Bone, bind);
        }
        var m = Measure(f, s);
        foreach (var side in new[] { "l", "r" })
        {
            int lower = s["lowerarm_" + side], hand = s["hand_" + side];
            var el = s.Head[lower]; var wr = s.Head[hand];
            var axis = Vector3.Normalize(wr - el);
            // dorsal direction: away from the palm (the grip socket's +Z is the palm side)
            var gz = res.TryGetValue("grip_" + side, out var g) ? g.AxisZ : -Vector3.UnitY;
            var back = -(gz - Vector3.Dot(gz, axis) * axis);
            back.Normalize();
            var origin = el + axis * PorpaxFromElbow + back * m.ForearmRadius;
            res["forearm_" + side] = new KitSocket("forearm_" + side, lower, Frame(origin, axis, back));
        }
        int pelvis = s["pelvis"];
        foreach (var (side, sign) in new[] { ("l", 1f), ("r", -1f) })
        {
            // at the hip line, at the side of the body (just outside the measured half width), slightly forward
            var origin = new Vector3(sign * (m.HipHalfWidth + 0.025f), m.HipY, 0.5f * (m.HipFrontZ + m.HipBackZ) + 0.02f);
            var up = Vector3.Normalize(new Vector3(0, 1, 0.12f));
            res["hip_" + side] = new KitSocket("hip_" + side, pelvis, Frame(origin, up, new Vector3(sign, 0, 0)));
        }
        int chest = s["spine_03"];
        res["back"] = new KitSocket("back", chest, Frame(new Vector3(0, m.ChestY, m.BackZAtChest - 0.02f), Vector3.UnitY, -Vector3.UnitZ));
        int head = s["head"];
        res["head"] = new KitSocket("head", head, Frame(m.HeadCentre, Vector3.UnitY, Vector3.UnitZ));
        return res;
    }

    static Matrix Frame(Vector3 origin, Vector3 y, Vector3 zApprox)
    {
        y = Vector3.Normalize(y);
        var z = Vector3.Normalize(zApprox - y * Vector3.Dot(zApprox, y));
        var x = Vector3.Cross(y, z);
        return new Matrix(x.X, x.Y, x.Z, 0, y.X, y.Y, y.Z, 0, z.X, z.Y, z.Z, 0, origin.X, origin.Y, origin.Z, 1);
    }

    /// <summary>Measurements from the body mesh: vertices are assigned to their heaviest bone.</summary>
    public static BodyMeasure Measure(CharacterFile f, Skeleton s)
    {
        var body = f.Mesh("body") ?? throw new InvalidDataException("identity without a body mesh");
        int head = s["head"], pelvis = s["pelvis"], thighL = s["thigh_l"], thighR = s["thigh_r"], spine3 = s["spine_03"], spine2 = s["spine_02"];
        int lowerL = s["lowerarm_l"], calfL = s["calf_l"];
        float eyeY = 0.5f * (s.Head[s["eye_l"]].Y + s.Head[s["eye_r"]].Y);
        var headV = new List<Vector3>(); var hipV = new List<Vector3>(); var chestV = new List<Vector3>(); var foreV = new List<Vector3>(); var calfV = new List<Vector3>();
        for (int i = 0; i < body.VertexCount; i++)
        {
            int j = body.Joints[4 * i]; int w = body.Weights[4 * i];
            var p = body.Positions[i];
            if (j == head && w > 128) headV.Add(p);
            if ((j == pelvis || j == thighL || j == thighR) && w > 100) hipV.Add(p);
            if ((j == spine3 || j == spine2) && w > 100) chestV.Add(p);
            if (j == lowerL && w > 150) foreV.Add(p);
            if (j == calfL && w > 150) calfV.Add(p);
        }
        float crown = headV.Max(p => p.Y);
        var band = headV.Where(p => p.Y > eyeY - 0.005f && p.Y < eyeY + 0.06f).ToList();
        float back = band.Min(p => p.Z), front = band.Max(p => p.Z);
        float temple = band.Max(p => MathF.Abs(p.X));
        float brow = headV.Where(p => MathF.Abs(p.X) < 0.02f && p.Y > eyeY + 0.01f && p.Y < eyeY + 0.04f).Select(p => p.Z).DefaultIfEmpty(front).Max();
        float chin = headV.Where(p => MathF.Abs(p.X) < 0.02f && p.Z > s.Head[head].Z).Select(p => p.Y).DefaultIfEmpty(eyeY - 0.12f).Min();
        var centre = new Vector3(0, eyeY + 0.03f, 0.5f * (back + front));
        // hips: just below the waist, at the level of the hip joints' heads plus 6 cm
        float hipY = s.Head[thighL].Y + 0.06f;
        var hb = hipV.Where(p => MathF.Abs(p.Y - hipY) < 0.03f).ToList();
        float hipHalf = hb.Count > 0 ? hb.Max(p => MathF.Abs(p.X)) : 0.17f;
        float hipFront = hb.Count > 0 ? hb.Max(p => p.Z) : 0.1f, hipBack = hb.Count > 0 ? hb.Min(p => p.Z) : -0.12f;
        // chest: 0.12 m below the base of the neck, the back surface there
        float chestY = s.Head[s["neck_01"]].Y - 0.14f;
        var cb = chestV.Where(p => MathF.Abs(p.Y - chestY) < 0.03f && MathF.Abs(p.X) < 0.08f).ToList();
        float backZ = cb.Count > 0 ? cb.Min(p => p.Z) : -0.12f;
        // forearm radius: median distance from the forearm axis
        var el = s.Head[lowerL]; var wr = s.Head[s["hand_l"]]; var ax = Vector3.Normalize(wr - el);
        var d = foreV.Select(p => { var q = p - el; return (q - ax * Vector3.Dot(q, ax)).Length(); }).OrderBy(x => x).ToList();
        float foreR = d.Count > 0 ? d[(int)(d.Count * 0.85)] : 0.035f;
        var ca = s.Head[calfL]; var ft = s.Head[s["foot_l"]]; var cax = Vector3.Normalize(ft - ca);
        var dc = calfV.Select(p => { var q = p - ca; return (q - cax * Vector3.Dot(q, cax)).Length(); }).OrderBy(x => x).ToList();
        float calfR = dc.Count > 0 ? dc[(int)(dc.Count * 0.9)] : 0.05f;
        return new BodyMeasure(eyeY, centre, crown, back, brow, temple, chin, hipY, hipHalf, hipFront, hipBack, backZ, chestY, foreR,
            s.Head[calfL].Y, s.Head[s["foot_l"]].Y, calfR);
    }
}
