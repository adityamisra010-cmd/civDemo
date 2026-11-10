using System.Runtime.InteropServices;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;

namespace Gfx.Proc.Environment.Structures;

/// <summary>Structure materials (baked by scripts/bake-env.py, section struct; logs use the CC0 bark sets).</summary>
public enum StMat { Logs, Wood, Planks, Daub, Thatch, Ashlar, Rubble, Plaster, Tiles, Paving, Count }

/// <summary>Structure vertex: position, normal, tangent (xyz = direction of +u, w = handedness of +v), uv in texture
/// repeats, colour (rgb tint, a = ambient occlusion).</summary>
[StructLayout(LayoutKind.Sequential, Pack = 1)]
public struct VertexStruct : IVertexType
{
    public Vector3 Position; public Vector3 Normal; public Vector4 Tangent; public Vector2 Uv; public Color Color;
    public static readonly VertexDeclaration Decl = new(
        new VertexElement(0, VertexElementFormat.Vector3, VertexElementUsage.Position, 0),
        new VertexElement(12, VertexElementFormat.Vector3, VertexElementUsage.Normal, 0),
        new VertexElement(24, VertexElementFormat.Vector4, VertexElementUsage.Tangent, 0),
        new VertexElement(40, VertexElementFormat.Vector2, VertexElementUsage.TextureCoordinate, 0),
        new VertexElement(48, VertexElementFormat.Color, VertexElementUsage.Color, 0));
    readonly VertexDeclaration IVertexType.VertexDeclaration => Decl;
}

/// <summary>
/// Accumulates structure geometry per material. Every helper takes world-space points and derives uv in metres
/// divided by the material's repeat size (<see cref="WorldSize"/>), with a per-call offset so neighbouring pieces do
/// not repeat the same texture area. PRESENTATION ONLY.
/// </summary>
public sealed class StructureBuilder
{
    public readonly List<VertexStruct>[] Verts = new List<VertexStruct>[(int)StMat.Count];
    public readonly List<int>[] Idx = new List<int>[(int)StMat.Count];
    public int Triangles { get { int n = 0; foreach (var l in Idx) n += l.Count / 3; return n; } }

    public StructureBuilder()
    {
        for (int i = 0; i < Verts.Length; i++) { Verts[i] = new(); Idx[i] = new(); }
    }

    /// <summary>Metres per texture repeat (matches calibration.json of the bake).</summary>
    public static float WorldSize(StMat m) => m switch
    {
        StMat.Logs => 1.2f, StMat.Wood => 1.0f, StMat.Planks => 2.0f, StMat.Daub => 2.5f, StMat.Thatch => 2.0f,
        StMat.Ashlar => 6.0f, StMat.Rubble => 2.0f, StMat.Plaster => 3.0f, StMat.Tiles => 2.0f, StMat.Paving => 4.0f, _ => 1f,
    };

    static Color C(Vector3 tint, float ao) => new(Math.Clamp(tint.X, 0, 1), Math.Clamp(tint.Y, 0, 1), Math.Clamp(tint.Z, 0, 1), Math.Clamp(ao, 0, 1));

    public int Vertex(StMat m, Vector3 p, Vector3 n, Vector3 t, float sign, Vector2 uv, Vector3 tint, float ao)
    {
        var l = Verts[(int)m];
        l.Add(new VertexStruct { Position = p, Normal = n, Tangent = new Vector4(t, sign), Uv = uv, Color = C(tint, ao) });
        return l.Count - 1;
    }

    public void Tri(StMat m, int a, int b, int c) { var l = Idx[(int)m]; l.Add(a); l.Add(b); l.Add(c); }

    /// <summary>
    /// A planar quad a-b-c-d (counter-clockwise seen from the front). uv: u along a→b, v along a→d (metres from a,
    /// divided by the repeat), plus <paramref name="uvOff"/>. AO per corner.
    /// </summary>
    public void Quad(StMat m, Vector3 a, Vector3 b, Vector3 c, Vector3 d, Vector3 tint, Vector2 uvOff, float aoA = 1, float aoB = 1, float aoC = 1, float aoD = 1, Vector2? uvScale = null)
    {
        float ws = WorldSize(m);
        var sc = uvScale ?? Vector2.One;
        var eu = b - a; float lu = eu.Length(); var tu = lu > 1e-6f ? eu / lu : Vector3.UnitX;
        var ev = d - a; var n = Vector3.Cross(eu, ev);
        n = n.LengthSquared() > 1e-12f ? Vector3.Normalize(n) : Vector3.Up;
        // +v direction in the plane, orthogonal to +u
        var tv = ev - tu * Vector3.Dot(ev, tu);
        tv = tv.LengthSquared() > 1e-12f ? Vector3.Normalize(tv) : Vector3.Cross(n, tu);
        // a negative uv scale flips that texture axis: the tangent frame follows the texture
        var T = sc.X < 0 ? -tu : tu; var Bv = sc.Y < 0 ? -tv : tv;
        float sign = Vector3.Dot(Vector3.Cross(n, T), Bv) >= 0 ? 1f : -1f;
        Vector2 UV(Vector3 p) { var q = p - a; return new Vector2(Vector3.Dot(q, tu) * sc.X, Vector3.Dot(q, tv) * sc.Y) / ws + uvOff; }
        int ia = Vertex(m, a, n, T, sign, UV(a), tint, aoA), ib = Vertex(m, b, n, T, sign, UV(b), tint, aoB);
        int ic = Vertex(m, c, n, T, sign, UV(c), tint, aoC), id = Vertex(m, d, n, T, sign, UV(d), tint, aoD);
        Tri(m, ia, ib, ic); Tri(m, ia, ic, id);
    }

    /// <summary>A planar quad with explicit uv per corner (for pieces that share one texture frame, e.g. a wall's piers).</summary>
    public void QuadUv(StMat m, Vector3 a, Vector3 b, Vector3 c, Vector3 d, Vector2 ua, Vector2 ub, Vector2 uc, Vector2 ud, Vector3 tint, float aoA = 1, float aoB = 1, float aoC = 1, float aoD = 1)
    {
        var n = Vector3.Cross(b - a, d - a);
        n = n.LengthSquared() > 1e-12f ? Vector3.Normalize(n) : Vector3.Up;
        // tangent: the world direction of +u, from the uv gradient over the quad
        var e1 = b - a; var e2 = d - a; var du1 = ub - ua; var du2 = ud - ua;
        float det = du1.X * du2.Y - du2.X * du1.Y;
        var T = MathF.Abs(det) > 1e-9f ? (e1 * du2.Y - e2 * du1.Y) / det : Vector3.Normalize(e1);
        var Bv = MathF.Abs(det) > 1e-9f ? (e2 * du1.X - e1 * du2.X) / det : Vector3.Normalize(e2);
        T = T.LengthSquared() > 1e-12f ? Vector3.Normalize(T) : Vector3.UnitX;
        float sign = Vector3.Dot(Vector3.Cross(n, T), Bv) >= 0 ? 1f : -1f;
        int ia = Vertex(m, a, n, T, sign, ua, tint, aoA), ib = Vertex(m, b, n, T, sign, ub, tint, aoB);
        int ic = Vertex(m, c, n, T, sign, uc, tint, aoC), id = Vertex(m, d, n, T, sign, ud, tint, aoD);
        Tri(m, ia, ib, ic); Tri(m, ia, ic, id);
    }

    /// <summary>An oriented box: centre, half extents along the frame axes (x right, y up, z forward).</summary>
    public void Box(StMat m, Vector3 centre, Vector3 half, Vector3 xAxis, Vector3 tint, Vector2 uvOff, float aoBottom = 0.75f, StMat? top = null)
    {
        var x = Vector3.Normalize(new Vector3(xAxis.X, 0, xAxis.Z)); var y = Vector3.Up; var z = Vector3.Cross(x, y);
        Vector3 P(float sx, float sy, float sz) => centre + x * (sx * half.X) + y * (sy * half.Y) + z * (sz * half.Z);
        // four sides (front +z, back -z, right +x, left -x), then top and bottom
        Quad(m, P(-1, -1, 1), P(1, -1, 1), P(1, 1, 1), P(-1, 1, 1), tint, uvOff, aoBottom, aoBottom, 1, 1);
        Quad(m, P(1, -1, -1), P(-1, -1, -1), P(-1, 1, -1), P(1, 1, -1), tint, uvOff + new Vector2(0.37f, 0), aoBottom, aoBottom, 1, 1);
        Quad(m, P(1, -1, 1), P(1, -1, -1), P(1, 1, -1), P(1, 1, 1), tint, uvOff + new Vector2(0.61f, 0), aoBottom, aoBottom, 1, 1);
        Quad(m, P(-1, -1, -1), P(-1, -1, 1), P(-1, 1, 1), P(-1, 1, -1), tint, uvOff + new Vector2(0.13f, 0), aoBottom, aoBottom, 1, 1);
        Quad(top ?? m, P(-1, 1, 1), P(1, 1, 1), P(1, 1, -1), P(-1, 1, -1), tint, uvOff);
        Quad(m, P(-1, -1, -1), P(1, -1, -1), P(1, -1, 1), P(-1, -1, 1), tint, uvOff, 0.5f, 0.5f, 0.5f, 0.5f);
    }

    /// <summary>A tapered tube from a to b (radii r0, r1), u around (wraps times), v along (metres / repeat).</summary>
    public void Tube(StMat m, Vector3 a, Vector3 b, float r0, float r1, int seg, Vector3 tint, Vector2 uvOff, float wraps = 1, float aoA = 0.8f, float aoB = 1f, bool capTop = false)
    {
        float ws = WorldSize(m);
        var axis = b - a; float len = axis.Length(); var ax = axis / len;
        var side = MathF.Abs(ax.Y) > 0.9f ? Vector3.UnitX : Vector3.Up;
        var u = Vector3.Normalize(Vector3.Cross(ax, side)); var w = Vector3.Cross(ax, u);
        float slope = (r0 - r1) / len;
        int b0 = Verts[(int)m].Count;
        for (int i = 0; i <= seg; i++)
        {
            float t = i / (float)seg * MathHelper.TwoPi;
            var dir = u * MathF.Cos(t) + w * MathF.Sin(t);
            var n = Vector3.Normalize(dir + ax * slope);
            var tan = Vector3.Normalize(-u * MathF.Sin(t) + w * MathF.Cos(t));
            float uu = i / (float)seg * wraps + uvOff.X;
            Vertex(m, a + dir * r0, n, tan, 1, new Vector2(uu, uvOff.Y), tint, aoA);
            Vertex(m, b + dir * r1, n, tan, 1, new Vector2(uu, uvOff.Y + len / ws), tint, aoB);
        }
        for (int i = 0; i < seg; i++)
        {
            int p = b0 + 2 * i;
            Tri(m, p, p + 1, p + 2); Tri(m, p + 2, p + 1, p + 3);
        }
        if (capTop && r1 > 0)
        {
            int c = Vertex(m, b, ax, u, 1, uvOff, tint, aoB);
            for (int i = 0; i < seg; i++) Tri(m, c, b0 + 2 * i + 3, b0 + 2 * i + 1);
        }
    }

    /// <summary>A cone (r0 at a, tip at b).</summary>
    public void Cone(StMat m, Vector3 a, Vector3 b, float r0, int seg, Vector3 tint, Vector2 uvOff, float ao = 1f) =>
        Tube(m, a, b, r0, 0.0005f, seg, tint, uvOff, 1, ao, ao);
}
