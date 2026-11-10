using Gfx.Chars.Format;
using Microsoft.Xna.Framework;

namespace Gfx.Chars.Animation;

/// <summary>Runtime skeleton: rest heads (bind space) and parents, bones ordered parents first. Rest frames are world-aligned.</summary>
public sealed class Skeleton
{
    public string[] Names { get; }
    public int[] Parent { get; }
    public Vector3[] Head { get; }
    public Vector3[] Tail { get; }
    public int Count => Names.Length;
    readonly Dictionary<string, int> _index;

    public Skeleton(IReadOnlyList<BoneDef> bones)
    {
        Names = bones.Select(b => b.Name).ToArray();
        Parent = bones.Select(b => b.Parent).ToArray();
        Head = bones.Select(b => b.Head).ToArray();
        Tail = bones.Select(b => b.Tail).ToArray();
        _index = new Dictionary<string, int>(StringComparer.Ordinal);
        for (int i = 0; i < Names.Length; i++)
        {
            _index[Names[i]] = i;
            if (Parent[i] >= i) throw new InvalidDataException($"bone {Names[i]}: parent must come first");
        }
    }

    public int this[string name] => _index.TryGetValue(name, out int i) ? i : throw new KeyNotFoundException(name);
    public bool TryIndex(string name, out int i) => _index.TryGetValue(name, out i);
}

/// <summary>A pose: local rotation per bone (relative to the world-aligned rest frame) and the pelvis offset.</summary>
public sealed class Pose
{
    public Quaternion[] Local { get; }
    public Vector3 Pelvis;
    public Pose(int bones) { Local = new Quaternion[bones]; Reset(); }
    public void Reset() { Array.Fill(Local, Quaternion.Identity); Pelvis = Vector3.Zero; }
    public void CopyFrom(Pose o) { Array.Copy(o.Local, Local, Local.Length); Pelvis = o.Pelvis; }

    /// <summary>Masked layer: each bone moves towards <paramref name="layer"/> by mask[bone] x weight; the pelvis offset stays.</summary>
    public void BlendMasked(Pose layer, float[] mask, float weight)
    {
        for (int i = 0; i < Local.Length; i++)
        {
            float w = mask[i] * weight;
            if (w > 0) Local[i] = Q.Nlerp(Local[i], layer.Local[i], w);
        }
    }

    /// <summary>this = lerp(a, b, t) per bone (shortest-arc normalised lerp), pelvis linear.</summary>
    public void Blend(Pose a, Pose b, float t)
    {
        for (int i = 0; i < Local.Length; i++) Local[i] = Q.Nlerp(a.Local[i], b.Local[i], t);
        Pelvis = Vector3.Lerp(a.Pelvis, b.Pelvis, t);
    }
}

/// <summary>Quaternion helpers with explicit conventions: Mul(a, b) applies b first, then a (Hamilton product).</summary>
public static class Q
{
    public static Quaternion Mul(Quaternion a, Quaternion b) => Quaternion.Multiply(a, b);
    public static Vector3 Rotate(Quaternion q, Vector3 v) => Vector3.Transform(v, q);
    public static Quaternion Inverse(Quaternion q) => Quaternion.Conjugate(q);

    public static Quaternion Nlerp(Quaternion a, Quaternion b, float t)
    {
        if (Quaternion.Dot(a, b) < 0) b = -b;
        var r = new Quaternion(a.X + (b.X - a.X) * t, a.Y + (b.Y - a.Y) * t, a.Z + (b.Z - a.Z) * t, a.W + (b.W - a.W) * t);
        r.Normalize();
        return r;
    }

    /// <summary>Fraction t of rotation q (from identity).</summary>
    public static Quaternion Scale(Quaternion q, float t) => Nlerp(Quaternion.Identity, q, t);

    /// <summary>Minimal rotation taking unit direction a to unit direction b.</summary>
    public static Quaternion Between(Vector3 a, Vector3 b)
    {
        a.Normalize(); b.Normalize();
        float d = Vector3.Dot(a, b);
        if (d > 0.999999f) return Quaternion.Identity;
        if (d < -0.999999f)
        {
            var axis = Vector3.Cross(Vector3.UnitX, a);
            if (axis.LengthSquared() < 1e-6f) axis = Vector3.Cross(Vector3.UnitY, a);
            axis.Normalize();
            return Quaternion.CreateFromAxisAngle(axis, MathF.PI);
        }
        var c = Vector3.Cross(a, b);
        var q = new Quaternion(c.X, c.Y, c.Z, 1 + d);
        q.Normalize();
        return q;
    }
}

/// <summary>Forward kinematics and skin matrices.</summary>
public static class Fk
{
    /// <summary>Global rotations and posed heads (character space) of a pose.</summary>
    public static void Solve(Skeleton s, Pose p, Quaternion[] global, Vector3[] head, int pelvisBone)
    {
        for (int i = 0; i < s.Count; i++)
        {
            int par = s.Parent[i];
            if (par < 0) { global[i] = p.Local[i]; head[i] = s.Head[i]; continue; }
            global[i] = Q.Mul(global[par], p.Local[i]);
            head[i] = head[par] + Q.Rotate(global[par], s.Head[i] - s.Head[par]);
            if (i == pelvisBone) head[i] += p.Pelvis;
        }
    }

    /// <summary>Skin matrix per bone (XNA row-vector convention, world folded in): v_world = v_bind · M.</summary>
    public static void SkinMatrices(Skeleton s, Quaternion[] global, Vector3[] head, in Matrix world, Matrix[] skin)
    {
        for (int i = 0; i < s.Count; i++)
        {
            var m = Matrix.CreateTranslation(-s.Head[i]) * Matrix.CreateFromQuaternion(global[i]) * Matrix.CreateTranslation(head[i]);
            skin[i] = m * world;
        }
    }
}
