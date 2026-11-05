using Gfx.Engine.Rendering;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics.PackedVector;

namespace Gfx.Units;

/// <summary>
/// Material region carried in vertex-colour ALPHA, read by the <c>UnitSkinnedLit</c> shader:
/// Cloth → albedo = per-instance cloth colour × vertex rgb (a shade); Skin → per-instance skin tone;
/// Fixed → vertex rgb as authored (metal, leather, wood).
/// </summary>
public static class Region
{
    public const byte Cloth = 0, Skin = 128, Fixed = 255;
}

/// <summary>Equipment parts: separate rigid meshes, each skinned 100 % to one joint (so they follow the bone texture).</summary>
public enum Part { HelmetCone, HelmetCrested, HelmetKettle, ShieldRound, ShieldRect, Spear, Sword, Bow, Quiver, Musket }

/// <summary>Procedural low-poly soldier meshes (placeholder-grade; flat-shaded, ~0.4k triangles at LOD0).</summary>
public static class SoldierMeshes
{
    public static readonly int PartCount = Enum.GetValues<Part>().Length;

    sealed class Builder
    {
        public readonly List<VertexSkinned> V = new();
        public readonly List<int> I = new();
        public MeshData<VertexSkinned> Mesh() => new(V.ToArray(), I.ToArray());

        static (Byte4 j, Vector4 w) W((int j, float w)[] inf)
        {
            Span<int> js = stackalloc int[4]; Span<float> ws = stackalloc float[4];
            for (int i = 0; i < inf.Length && i < 4; i++) { js[i] = inf[i].j; ws[i] = inf[i].w; }
            return (new Byte4(js[0], js[1], js[2], js[3]), new Vector4(ws[0], ws[1], ws[2], ws[3]));
        }

        /// <summary>a,b carry influences ia; c,d carry ib. Flat normal, oriented away from <paramref name="inside"/>.</summary>
        public void Quad(Vector3 a, Vector3 b, Vector3 c, Vector3 d, Color col, (int, float)[] ia, (int, float)[] ib, Vector3 inside)
        {
            var n = Vector3.Cross(b - a, d - a);
            if (n.LengthSquared() < 1e-12f) n = Vector3.Cross(c - b, d - b);
            n.Normalize();
            if (Vector3.Dot(n, (a + b + c + d) * 0.25f - inside) < 0) n = -n;
            var (ja, wa) = W(ia); var (jb, wb) = W(ib);
            int o = V.Count;
            V.Add(new VertexSkinned(a, n, col, ja, wa)); V.Add(new VertexSkinned(b, n, col, ja, wa));
            V.Add(new VertexSkinned(c, n, col, jb, wb)); V.Add(new VertexSkinned(d, n, col, jb, wb));
            I.Add(o); I.Add(o + 1); I.Add(o + 2); I.Add(o); I.Add(o + 2); I.Add(o + 3);
        }

        public void Cap(Vector3[] ring, Vector3 center, Color col, (int, float)[] inf, bool flip, Vector3 n)
        {
            var (j, w) = W(inf);
            int o = V.Count;
            V.Add(new VertexSkinned(center, n, col, j, w));
            foreach (var p in ring) V.Add(new VertexSkinned(p, n, col, j, w));
            for (int i = 0; i < ring.Length; i++)
            {
                int a = o + 1 + i, b = o + 1 + (i + 1) % ring.Length;
                I.Add(o); I.Add(flip ? b : a); I.Add(flip ? a : b);
            }
        }

        /// <summary>Elliptic prism (frustum) from a to b; rx along X, rz along the perpendicular in XZ/YZ.</summary>
        public void Prism(Vector3 a, Vector3 b, Vector2 ra, Vector2 rb, int sides, Color col, (int, float)[] ia, (int, float)[] ib, bool caps = true)
        {
            var axis = Vector3.Normalize(b - a);
            var u = MathF.Abs(axis.Y) > 0.9f ? Vector3.UnitX : Vector3.Normalize(Vector3.Cross(Vector3.UnitY, axis));
            var v = Vector3.Normalize(Vector3.Cross(u, axis));
            var A = new Vector3[sides]; var B = new Vector3[sides];
            for (int i = 0; i < sides; i++)
            {
                float t = (i + 0.5f) / sides * MathF.Tau;
                A[i] = a + u * (MathF.Cos(t) * ra.X) + v * (MathF.Sin(t) * ra.Y);
                B[i] = b + u * (MathF.Cos(t) * rb.X) + v * (MathF.Sin(t) * rb.Y);
            }
            for (int i = 0; i < sides; i++)
            {
                int k = (i + 1) % sides;
                var mid = (A[i] + A[k] + B[k] + B[i]) * 0.25f;
                Quad(A[i], A[k], B[k], B[i], col, ia, ib, a + axis * Vector3.Dot(mid - a, axis));
            }
            if (caps)
            {
                if (ra.X > 0.002f) Cap(A, a, col, ia, false, -axis);
                if (rb.X > 0.002f) Cap(B, b, col, ib, true, axis);
            }
        }

        public void Box(Vector3 c, Vector3 e, Color col, int joint)
        {
            var inf = new[] { (joint, 1f) };
            Vector3[] f = { Vector3.Right, Vector3.Left, Vector3.Up, Vector3.Down, Vector3.Backward, Vector3.Forward };
            foreach (var n in f)
            {
                Vector3 s1 = new(n.Y, n.Z, n.X), s2 = Vector3.Cross(n, s1);
                Quad(c + (n - s1 - s2) * e, c + (n - s1 + s2) * e, c + (n + s1 + s2) * e, c + (n + s1 - s2) * e, col, inf, inf, c);
            }
        }

        public void Sphere(Vector3 c, Vector3 r, int seg, int rings, Color col, int joint)
        {
            var inf = new[] { (joint, 1f) };
            Vector3 P(int i, int k)
            {
                float th = MathF.PI * k / rings, ph = MathF.Tau * i / seg;
                return c + new Vector3(MathF.Sin(th) * MathF.Cos(ph) * r.X, MathF.Cos(th) * r.Y, MathF.Sin(th) * MathF.Sin(ph) * r.Z);
            }
            for (int k = 0; k < rings; k++)
                for (int i = 0; i < seg; i++)
                {
                    var a = P(i, k); var b = P(i + 1, k); var d = P(i, k + 1); var e = P(i + 1, k + 1);
                    Quad(d, e, b, a, col, inf, inf, c); // top row degenerates to a triangle (a == b)
                }
        }
    }

    static Color Rgb(int r, int g, int b, byte region) => new((byte)r, (byte)g, (byte)b, region);
    static (int, float)[] J(int j) => new[] { (j, 1f) };
    static (int, float)[] J(int j1, float w1, int j2) => new[] { (j1, w1), (j2, 1f - w1) };

    /// <summary>Skinned body. <paramref name="sides"/> = 6 (LOD0) or 4 (LOD1).</summary>
    public static MeshData<VertexSkinned> Body(int sides = 6)
    {
        var b = new Builder(); var B = Skeleton.Bind;
        var cloth = Rgb(255, 255, 255, Region.Cloth); var clothDark = Rgb(170, 170, 170, Region.Cloth);
        var skin = Rgb(255, 255, 255, Region.Skin); var leather = Rgb(92, 62, 38, Region.Fixed); var belt = Rgb(60, 42, 28, Region.Fixed);
        var hair = Rgb(48, 36, 26, Region.Fixed);
        // torso (tunic): pelvis → spine → chest → shoulders
        b.Prism(new Vector3(0, 0.80f, 0), new Vector3(0, 1.00f, 0), new Vector2(0.17f, 0.12f), new Vector2(0.16f, 0.11f), sides, clothDark, J(Skeleton.Pelvis), J(Skeleton.Spine, 0.5f, Skeleton.Pelvis));
        b.Prism(new Vector3(0, 1.00f, 0), new Vector3(0, 1.25f, 0), new Vector2(0.16f, 0.11f), new Vector2(0.19f, 0.12f), sides, cloth, J(Skeleton.Spine, 0.5f, Skeleton.Pelvis), J(Skeleton.Chest, 0.6f, Skeleton.Spine));
        b.Prism(new Vector3(0, 1.25f, 0), new Vector3(0, 1.49f, 0), new Vector2(0.19f, 0.12f), new Vector2(0.22f, 0.11f), sides, cloth, J(Skeleton.Chest, 0.6f, Skeleton.Spine), J(Skeleton.Chest));
        b.Box(new Vector3(0, 0.98f, 0), new Vector3(0.175f, 0.03f, 0.125f), belt, Skeleton.Pelvis);
        // neck + head
        b.Prism(new Vector3(0, 1.47f, 0), new Vector3(0, 1.60f, 0.01f), new Vector2(0.05f, 0.05f), new Vector2(0.045f, 0.045f), sides, skin, J(Skeleton.Neck, 0.5f, Skeleton.Chest), J(Skeleton.Neck, 0.5f, Skeleton.Head));
        b.Sphere(new Vector3(0, 1.70f, 0.01f), new Vector3(0.095f, 0.115f, 0.105f), sides + 2, sides == 4 ? 3 : 4, skin, Skeleton.Head);
        b.Box(new Vector3(0, 1.69f, 0.105f), new Vector3(0.012f, 0.02f, 0.02f), skin, Skeleton.Head); // nose
        b.Sphere(new Vector3(0, 1.74f, -0.005f), new Vector3(0.1f, 0.085f, 0.105f), sides + 2, 2, hair, Skeleton.Head);
        foreach (bool left in new[] { true, false })
        {
            int ua = left ? Skeleton.LUpperArm : Skeleton.RUpperArm, fa = left ? Skeleton.LForearm : Skeleton.RForearm;
            int hd = left ? Skeleton.LHand : Skeleton.RHand, th = left ? Skeleton.LThigh : Skeleton.RThigh;
            int sh = left ? Skeleton.LShin : Skeleton.RShin, ft = left ? Skeleton.LFoot : Skeleton.RFoot;
            b.Prism(B[ua] + new Vector3(0, 0.03f, 0), B[fa], new Vector2(0.06f, 0.06f), new Vector2(0.048f, 0.048f), sides, cloth, J(ua, 0.85f, Skeleton.Chest), J(ua, 0.5f, fa));
            b.Prism(B[fa], B[hd] + new Vector3(0, 0.03f, 0), new Vector2(0.046f, 0.046f), new Vector2(0.036f, 0.036f), sides, skin, J(ua, 0.5f, fa), J(fa));
            b.Box(B[hd] + new Vector3(0, -0.04f, 0.01f), new Vector3(0.03f, 0.05f, 0.04f), skin, hd);
            b.Prism(B[th] + new Vector3(0, 0.02f, 0), B[sh], new Vector2(0.085f, 0.085f), new Vector2(0.06f, 0.06f), sides, clothDark, J(th, 0.75f, Skeleton.Pelvis), J(th, 0.5f, sh));
            b.Prism(B[sh], B[ft] + new Vector3(0, 0.03f, 0), new Vector2(0.058f, 0.058f), new Vector2(0.045f, 0.045f), sides, leather, J(th, 0.5f, sh), J(sh));
            b.Box(B[ft] + new Vector3(0, -0.04f, 0.06f), new Vector3(0.05f, 0.04f, 0.12f), leather, ft);
        }
        return b.Mesh();
    }

    /// <summary>Equipment part in BIND-pose model space, 100 % weighted to its attachment joint.</summary>
    public static MeshData<VertexSkinned> Equipment(Part part)
    {
        var b = new Builder(); var B = Skeleton.Bind;
        var bronze = Rgb(176, 128, 66, Region.Fixed); var iron = Rgb(120, 122, 128, Region.Fixed);
        var wood = Rgb(110, 76, 44, Region.Fixed); var leather = Rgb(90, 60, 36, Region.Fixed);
        var paint = Rgb(255, 255, 255, Region.Cloth); var crest = Rgb(170, 28, 24, Region.Fixed);
        int H = Skeleton.Head, RH = Skeleton.RHand, LH = Skeleton.LHand, LF = Skeleton.LForearm;
        Vector3 hand = B[RH] + new Vector3(0, -0.05f, 0.01f), lhand = B[LH] + new Vector3(0, -0.05f, 0.01f);
        switch (part)
        {
            case Part.HelmetCone:
                b.Prism(new Vector3(0, 1.70f, 0.0f), new Vector3(0, 1.90f, 0), new Vector2(0.125f, 0.13f), new Vector2(0.01f, 0.01f), 8, bronze, J(H), J(H));
                b.Box(new Vector3(0, 1.66f, 0.12f), new Vector3(0.012f, 0.05f, 0.01f), bronze, H); // nasal
                break;
            case Part.HelmetCrested:
                b.Prism(new Vector3(0, 1.66f, 0.0f), new Vector3(0, 1.84f, 0), new Vector2(0.125f, 0.13f), new Vector2(0.08f, 0.085f), 8, bronze, J(H), J(H));
                b.Box(new Vector3(0, 1.90f, -0.02f), new Vector3(0.02f, 0.06f, 0.15f), crest, H);
                b.Box(new Vector3(0.11f, 1.64f, 0.04f), new Vector3(0.012f, 0.06f, 0.05f), bronze, H); // cheek guards
                b.Box(new Vector3(-0.11f, 1.64f, 0.04f), new Vector3(0.012f, 0.06f, 0.05f), bronze, H);
                break;
            case Part.HelmetKettle:
                b.Prism(new Vector3(0, 1.735f, 0), new Vector3(0, 1.755f, 0), new Vector2(0.22f, 0.22f), new Vector2(0.22f, 0.22f), 10, iron, J(H), J(H));
                b.Prism(new Vector3(0, 1.75f, 0), new Vector3(0, 1.86f, 0), new Vector2(0.12f, 0.125f), new Vector2(0.07f, 0.075f), 8, iron, J(H), J(H));
                break;
            case Part.ShieldRound: // disc normal along the forearm axis: faces forward in the guard pose
            {
                var c = B[LF] + new Vector3(-0.05f, -0.22f, 0);
                b.Prism(c + new Vector3(0, 0.03f, 0), c - new Vector3(0, 0.02f, 0), new Vector2(0.42f, 0.42f), new Vector2(0.42f, 0.42f), 12, paint, J(LF), J(LF));
                b.Prism(c - new Vector3(0, 0.02f, 0), c - new Vector3(0, 0.06f, 0), new Vector2(0.08f, 0.08f), new Vector2(0.05f, 0.05f), 8, bronze, J(LF), J(LF)); // boss
                break;
            }
            case Part.ShieldRect:
            {
                var c = B[LF] + new Vector3(-0.05f, -0.22f, 0.05f);
                b.Box(c, new Vector3(0.32f, 0.025f, 0.55f), paint, LF);
                b.Box(c - new Vector3(0, 0.035f, 0), new Vector3(0.06f, 0.015f, 0.06f), bronze, LF);
                break;
            }
            case Part.Spear: // along bind +Z through the hand: near-vertical in the guard pose
                b.Prism(hand + new Vector3(0, 0, -0.9f), hand + new Vector3(0, 0, 1.55f), new Vector2(0.018f, 0.018f), new Vector2(0.016f, 0.016f), 5, wood, J(RH), J(RH));
                b.Prism(hand + new Vector3(0, 0, 1.55f), hand + new Vector3(0, 0, 1.82f), new Vector2(0.035f, 0.012f), new Vector2(0.002f, 0.002f), 4, iron, J(RH), J(RH));
                break;
            case Part.Sword:
                b.Box(hand + new Vector3(0, 0, -0.05f), new Vector3(0.018f, 0.018f, 0.07f), leather, RH);
                b.Box(hand + new Vector3(0, 0, 0.03f), new Vector3(0.09f, 0.015f, 0.015f), bronze, RH);
                b.Prism(hand + new Vector3(0, 0, 0.04f), hand + new Vector3(0, 0, 0.78f), new Vector2(0.03f, 0.006f), new Vector2(0.012f, 0.004f), 4, iron, J(RH), J(RH));
                break;
            case Part.Bow: // vertical in the guard/aim pose (bind Z), bulging forward (bind −Y)
            {
                const int n = 6; Vector3 prev = default;
                for (int i = 0; i <= n; i++)
                {
                    float z = -0.75f + 1.5f * i / n; float y = -0.16f * (1 - (z / 0.75f) * (z / 0.75f));
                    var p = lhand + new Vector3(0, y, z);
                    if (i > 0) b.Prism(prev, p, new Vector2(0.018f, 0.018f), new Vector2(0.018f, 0.018f), 4, wood, J(LH), J(LH), caps: false);
                    prev = p;
                }
                b.Prism(lhand + new Vector3(0, 0, -0.75f), lhand + new Vector3(0, 0, 0.75f), new Vector2(0.003f, 0.003f), new Vector2(0.003f, 0.003f), 3, Rgb(220, 210, 190, Region.Fixed), J(LH), J(LH), caps: false);
                break;
            }
            case Part.Quiver:
                b.Prism(new Vector3(0.08f, 1.05f, -0.16f), new Vector3(-0.06f, 1.50f, -0.17f), new Vector2(0.05f, 0.05f), new Vector2(0.055f, 0.055f), 6, leather, J(Skeleton.Chest), J(Skeleton.Chest));
                break;
            case Part.Musket:
                b.Box(hand + new Vector3(0, 0, -0.22f), new Vector3(0.03f, 0.05f, 0.2f), wood, RH);
                b.Prism(hand + new Vector3(0, 0.0f, -0.02f), hand + new Vector3(0, 0.0f, 1.15f), new Vector2(0.014f, 0.014f), new Vector2(0.012f, 0.012f), 5, iron, J(RH), J(RH));
                break;
        }
        return b.Mesh();
    }

    /// <summary>Far-LOD rigid proxy (no skinning): legs, torso, head. White × instance tint (cloth colour).</summary>
    public static MeshData<VertexPNC> Proxy()
    {
        var v = new List<VertexPNC>(); var ix = new List<int>();
        var c = new Color(235, 235, 235); var head = new Color(255, 214, 180);
        Primitives.AddBox(v, ix, new Vector3(0, 0.45f, 0), new Vector3(0.16f, 0.45f, 0.10f), new Color(150, 150, 150));
        Primitives.AddBox(v, ix, new Vector3(0, 1.20f, 0), new Vector3(0.21f, 0.30f, 0.12f), c);
        Primitives.AddBox(v, ix, new Vector3(0, 1.68f, 0), new Vector3(0.10f, 0.12f, 0.10f), head);
        return new MeshData<VertexPNC>(v.ToArray(), ix.ToArray());
    }
}
