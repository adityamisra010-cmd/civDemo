using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics.PackedVector;

namespace Gfx.Engine.Rendering;

/// <summary>Placeholder-grade procedural primitives (box/plane after the recon probe's Geometry.Box).</summary>
public static class Primitives
{
    public static MeshData<VertexPNC> Box(Vector3 center, Vector3 halfExtents, Color color)
    {
        var v = new List<VertexPNC>(24); var ix = new List<int>(36);
        AddBox(v, ix, center, halfExtents, color);
        return new MeshData<VertexPNC>(v.ToArray(), ix.ToArray());
    }

    public static void AddBox(List<VertexPNC> v, List<int> ix, Vector3 c, Vector3 e, Color col)
    {
        Vector3[] faces = { Vector3.Right, Vector3.Left, Vector3.Up, Vector3.Down, Vector3.Backward, Vector3.Forward };
        foreach (var f in faces)
        {
            Vector3 s1 = new(f.Y, f.Z, f.X), s2 = Vector3.Cross(f, s1);
            int b = v.Count;
            v.Add(new VertexPNC(c + (f - s1 - s2) * e, f, col)); v.Add(new VertexPNC(c + (f - s1 + s2) * e, f, col));
            v.Add(new VertexPNC(c + (f + s1 + s2) * e, f, col)); v.Add(new VertexPNC(c + (f + s1 - s2) * e, f, col));
            ix.Add(b); ix.Add(b + 1); ix.Add(b + 2); ix.Add(b); ix.Add(b + 2); ix.Add(b + 3);
        }
    }

    /// <summary>Flat XZ grid centred on the origin, normal +Y.</summary>
    public static MeshData<VertexPNC> Plane(float size, int segments, Color color)
    {
        int n = segments + 1; var v = new VertexPNC[n * n];
        for (int z = 0; z < n; z++) for (int x = 0; x < n; x++)
            v[z * n + x] = new VertexPNC(new Vector3((x / (float)segments - 0.5f) * size, 0, (z / (float)segments - 0.5f) * size), Vector3.Up, color);
        var ix = new int[segments * segments * 6]; int k = 0;
        for (int z = 0; z < segments; z++) for (int x = 0; x < segments; x++)
        {
            int a = z * n + x, b = a + 1, c = a + n, d = c + 1;
            ix[k++] = a; ix[k++] = c; ix[k++] = b; ix[k++] = b; ix[k++] = c; ix[k++] = d;
        }
        return new MeshData<VertexPNC>(v, ix);
    }

    /// <summary>
    /// Two-joint test bar (VTF/skinning smoke): an 8-sided tube from y=0 to y=2. Joint 0 owns the lower half,
    /// joint 1 the upper half, blended linearly across y∈[0.75,1.25]. Bind pose = identity skin matrices.
    /// </summary>
    public static MeshData<VertexSkinned> SkinnedBar(Color color, float radius = 0.2f)
    {
        var v = new List<VertexSkinned>(); var ix = new List<int>();
        const int rings = 9, sides = 8;
        for (int r = 0; r < rings; r++)
        {
            float y = r * 0.25f; float w1 = MathHelper.Clamp((y - 0.75f) / 0.5f, 0f, 1f);
            for (int k = 0; k < sides; k++)
            {
                float a = k * MathHelper.TwoPi / sides; var n = new Vector3(MathF.Cos(a), 0, MathF.Sin(a));
                v.Add(new VertexSkinned(new Vector3(n.X * radius, y, n.Z * radius), n, color, new Byte4(0, 1, 0, 0), new Vector4(1 - w1, w1, 0, 0)));
            }
        }
        for (int r = 0; r < rings - 1; r++) for (int k = 0; k < sides; k++)
        {
            int a = r * sides + k, b = r * sides + (k + 1) % sides, c = a + sides, d = b + sides;
            ix.AddRange(new[] { a, c, b, b, c, d });
        }
        return new MeshData<VertexSkinned>(v.ToArray(), ix.ToArray());
    }
}
