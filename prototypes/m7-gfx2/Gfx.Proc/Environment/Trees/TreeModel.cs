using System.Runtime.InteropServices;
using Gfx.Proc.Environment.EzTree;
using Gfx.Proc.Presentation;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;

namespace Gfx.Proc.Environment.Trees;

/// <summary>Bark vertex: position, normal, ring tangent, uv (u around, v along, in texture repeats), wind data
/// (x = branch flexibility 0..1, y = branch phase 0..1, z = canopy occlusion 0..1, w = height in the tree 0..1).</summary>
[StructLayout(LayoutKind.Sequential, Pack = 1)]
public struct VertexBark : IVertexType
{
    public Vector3 Position; public Vector3 Normal; public Vector3 Tangent; public Vector2 Uv; public Vector4 Wind;
    public static readonly VertexDeclaration Decl = new(
        new VertexElement(0, VertexElementFormat.Vector3, VertexElementUsage.Position, 0),
        new VertexElement(12, VertexElementFormat.Vector3, VertexElementUsage.Normal, 0),
        new VertexElement(24, VertexElementFormat.Vector3, VertexElementUsage.Tangent, 0),
        new VertexElement(36, VertexElementFormat.Vector2, VertexElementUsage.TextureCoordinate, 0),
        new VertexElement(44, VertexElementFormat.Vector4, VertexElementUsage.TextureCoordinate, 6));
    readonly VertexDeclaration IVertexType.VertexDeclaration => Decl;
}

/// <summary>Leaf-card vertex: position, rounded canopy normal, uv, wind data (x = flexibility, y = phase,
/// z = canopy occlusion, w = 1 at the card's far end, 0 at its stem).</summary>
[StructLayout(LayoutKind.Sequential, Pack = 1)]
public struct VertexLeaf : IVertexType
{
    public Vector3 Position; public Vector3 Normal; public Vector2 Uv; public Vector4 Wind;
    public static readonly VertexDeclaration Decl = new(
        new VertexElement(0, VertexElementFormat.Vector3, VertexElementUsage.Position, 0),
        new VertexElement(12, VertexElementFormat.Vector3, VertexElementUsage.Normal, 0),
        new VertexElement(24, VertexElementFormat.Vector2, VertexElementUsage.TextureCoordinate, 0),
        new VertexElement(32, VertexElementFormat.Vector4, VertexElementUsage.TextureCoordinate, 6));
    readonly VertexDeclaration IVertexType.VertexDeclaration => Decl;
}

/// <summary>Per-instance tree data: affine transform columns (TEXCOORD1..3), tint (rgb linear multiplier, a = wind
/// phase), Lod (x = dither fade 0..1, y = +1 keep where dither ≥ fade / −1 keep where dither &lt; fade / 0 no dither,
/// z = instance scale, w = yaw in radians).</summary>
[StructLayout(LayoutKind.Sequential, Pack = 1)]
public struct TreeInstance : IVertexType
{
    public Vector4 W0, W1, W2, Tint, Lod;
    public static readonly VertexDeclaration Decl = new(
        new VertexElement(0, VertexElementFormat.Vector4, VertexElementUsage.TextureCoordinate, 1),
        new VertexElement(16, VertexElementFormat.Vector4, VertexElementUsage.TextureCoordinate, 2),
        new VertexElement(32, VertexElementFormat.Vector4, VertexElementUsage.TextureCoordinate, 3),
        new VertexElement(48, VertexElementFormat.Vector4, VertexElementUsage.TextureCoordinate, 4),
        new VertexElement(64, VertexElementFormat.Vector4, VertexElementUsage.TextureCoordinate, 5));
    readonly VertexDeclaration IVertexType.VertexDeclaration => Decl;

    public static TreeInstance From(Vector3 pos, float yaw, float scale, Vector3 tint, float phase)
    {
        var m = Matrix.CreateScale(scale) * Matrix.CreateRotationY(yaw) * Matrix.CreateTranslation(pos);
        return new TreeInstance
        {
            W0 = new Vector4(m.M11, m.M21, m.M31, m.M41), W1 = new Vector4(m.M12, m.M22, m.M32, m.M42), W2 = new Vector4(m.M13, m.M23, m.M33, m.M43),
            Tint = new Vector4(tint, phase), Lod = new Vector4(0, 0, scale, yaw),
        };
    }

    public readonly Vector3 Position => new(W0.W, W1.W, W2.W);
}

/// <summary>One detail level of a tree variant (CPU side).</summary>
public sealed class TreeLodMesh
{
    public required VertexBark[] BarkVertices { get; init; }
    public required int[] BarkIndices { get; init; }
    public required VertexLeaf[] LeafVertices { get; init; }
    public required int[] LeafIndices { get; init; }
    public int Triangles => (BarkIndices.Length + LeafIndices.Length) / 3;
}

/// <summary>
/// A tree variant built from ez-tree (C# port): the skeleton is grown once, meshed at full detail (LOD0) and at ez-tree's
/// LOD1 detail, scaled to metres, given bark UVs in metres (no stretched bark), wind weights and a canopy occlusion
/// term (a voxelised leaf-area density, marched towards the sky), so inner and lower foliage is darker.
/// PRESENTATION ONLY.
/// </summary>
public sealed class TreeModel
{
    public required TreeSpecies Species { get; init; }
    public required int Variant { get; init; }
    public required float Height { get; init; }
    public required float Scale { get; init; }
    public required TreeLodMesh Lod0 { get; init; }
    public required TreeLodMesh Lod1 { get; init; }
    public required Vector3 BoundsMin { get; init; }
    public required Vector3 BoundsMax { get; init; }
    /// <summary>Bounding sphere of the model (local space, before the instance scale).</summary>
    public required BoundingSphere Sphere { get; init; }
    /// <summary>Horizontal radius of the crown (m) and the crown's lowest leaf height (m).</summary>
    public required float CrownRadius { get; init; }
    public required float CrownBase { get; init; }
    public required int LeafCount { get; init; }

    /// <summary>Texel aspect (height / width) of each baked bark set: Bark001 (oak) is 512×1024, the others square.</summary>
    public static float BarkAspect(BarkSet b) => b == BarkSet.Oak ? 2f : 1f;

    public static TreeModel Build(TreeSpecies sp, int variant)
    {
        var o = sp.VariantOptions(variant);
        var sk = EzTreeGenerator.GenerateSkeleton(o);
        var (b0, l0) = EzTreeGenerator.Mesh(sk, o, EzDetail.Full);
        var (b1, l1) = EzTreeGenerator.Mesh(sk, o, EzDetail.Lod1);

        // model height (ez-tree units) → metres
        double top = 0;
        for (int i = 1; i < b0.Verts.Count; i += 3) top = Math.Max(top, b0.Verts[i]);
        for (int i = 1; i < l0.Verts.Count; i += 3) top = Math.Max(top, l0.Verts[i]);
        float height = sp.VariantHeight(variant);
        float scale = (float)(height / Math.Max(top, 1e-3));

        var occ = Occlusion.Build(l0, scale, top * scale);
        int levels = (int)o.Levels;
        float aspect = BarkAspect(sp.Bark);
        uint seed = (uint)(o.Seed * 2654435761L);
        var lod0 = Convert(b0, l0, sk, o, scale, height, levels, aspect, occ, seed);
        var lod1 = Convert(b1, l1, sk, o, scale, height, levels, aspect, occ, seed);

        var min = new Vector3(float.MaxValue); var max = new Vector3(float.MinValue);
        foreach (var v in lod0.BarkVertices) { min = Vector3.Min(min, v.Position); max = Vector3.Max(max, v.Position); }
        float crownR = 0, crownBase = float.MaxValue;
        foreach (var v in lod0.LeafVertices)
        {
            min = Vector3.Min(min, v.Position); max = Vector3.Max(max, v.Position);
            crownR = MathF.Max(crownR, MathF.Sqrt(v.Position.X * v.Position.X + v.Position.Z * v.Position.Z));
            crownBase = MathF.Min(crownBase, v.Position.Y);
        }
        var centre = (min + max) * 0.5f; float r = 0;
        foreach (var v in lod0.BarkVertices) r = MathF.Max(r, Vector3.Distance(centre, v.Position));
        foreach (var v in lod0.LeafVertices) r = MathF.Max(r, Vector3.Distance(centre, v.Position));
        return new TreeModel
        {
            Species = sp, Variant = variant, Height = height, Scale = scale, Lod0 = lod0, Lod1 = lod1, BoundsMin = min, BoundsMax = max,
            Sphere = new BoundingSphere(centre, r), CrownRadius = crownR, CrownBase = crownBase == float.MaxValue ? 0 : crownBase,
            LeafCount = sk.Leaves.Count,
        };
    }

    static float Hash01(uint a, uint b) => PNoise.U01(PNoise.Hash(a * 0x9E3779B9u ^ (b + 0x7F4A7C15u)));

    static TreeLodMesh Convert(EzBuffers b, EzBuffers l, EzSkeleton sk, EzTreeOptions o, float scale, float height, int levels,
        float barkAspect, Occlusion occ, uint seed)
    {
        var bv = new VertexBark[b.VertexCount];
        for (int i = 0; i < bv.Length; i++)
        {
            var p = new Vector3((float)b.Verts[3 * i], (float)b.Verts[3 * i + 1], (float)b.Verts[3 * i + 2]) * scale;
            var n = new Vector3((float)b.Normals[3 * i], (float)b.Normals[3 * i + 1], (float)b.Normals[3 * i + 2]);
            var t = new Vector3((float)b.Tangents[3 * i], (float)b.Tangents[3 * i + 1], (float)b.Tangents[3 * i + 2]);
            var br = sk.Branches[b.Owner[i]];
            double wraps = Math.Max(1, EzTreeGenerator.JsRound(br.BaseRadius * o.BarkTextureScaleX));
            double repeatW = 2 * Math.PI * br.BaseRadius * scale / wraps;
            float v = (float)(b.AlongDistance[i] * scale / (repeatW * barkAspect));
            int level = b.Level[i];
            float along = (float)b.Along[i];
            float flex = level == 0 ? 0f : Math.Clamp(0.3f + 0.7f * ((level - 1) + along) / Math.Max(1, levels), 0f, 1f);
            bv[i] = new VertexBark
            {
                Position = p, Normal = n, Tangent = t, Uv = new Vector2((float)b.Uvs[2 * i], v),
                Wind = new Vector4(flex, Hash01(seed, (uint)b.Owner[i]), occ.At(p), Math.Clamp(p.Y / height, 0f, 1f)),
            };
        }
        var lv = new VertexLeaf[l.VertexCount];
        for (int i = 0; i < lv.Length; i++)
        {
            var p = new Vector3((float)l.Verts[3 * i], (float)l.Verts[3 * i + 1], (float)l.Verts[3 * i + 2]) * scale;
            var n = new Vector3((float)l.Normals[3 * i], (float)l.Normals[3 * i + 1], (float)l.Normals[3 * i + 2]);
            lv[i] = new VertexLeaf
            {
                // three.js flips images on upload (v = 0 is the bottom row); MonoGame does not: flip v so the stem stays at the card's base
                Position = p, Normal = n, Uv = new Vector2((float)l.Uvs[2 * i], 1f - (float)l.Uvs[2 * i + 1]),
                Wind = new Vector4(1f, Hash01(seed + 1, (uint)l.Owner[i]), occ.At(p), (float)l.Along[i]),
            };
        }
        return new TreeLodMesh { BarkVertices = bv, BarkIndices = b.Indices.ToArray(), LeafVertices = lv, LeafIndices = l.Indices.ToArray() };
    }

    /// <summary>
    /// Canopy occlusion: leaf area per voxel (each card counts its area × typical coverage), then for a point the
    /// transmittance towards seven sky directions (zenith and six at 35°), cosine-weighted. Returned as an ambient
    /// factor in [0.18, 1].
    /// </summary>
    sealed class Occlusion
    {
        const int N = 20;
        readonly float[] _d = new float[N * N * N];
        Vector3 _min, _cell;

        public static Occlusion Build(EzBuffers leaves, float scale, double topM)
        {
            var o = new Occlusion();
            var min = new Vector3(float.MaxValue); var max = new Vector3(float.MinValue);
            for (int i = 0; i < leaves.VertexCount; i++)
            {
                var p = new Vector3((float)leaves.Verts[3 * i], (float)leaves.Verts[3 * i + 1], (float)leaves.Verts[3 * i + 2]) * scale;
                min = Vector3.Min(min, p); max = Vector3.Max(max, p);
            }
            if (leaves.VertexCount == 0) { min = Vector3.Zero; max = new Vector3(1, (float)topM, 1); }
            var pad = new Vector3(0.5f);
            o._min = min - pad; o._cell = (max - min + 2 * pad) / N;
            float cellVol = o._cell.X * o._cell.Y * o._cell.Z;
            // one quad = 4 vertices; card area × 0.22 (mean alpha coverage of the baked cards)
            for (int q = 0; q + 3 < leaves.VertexCount; q += 4)
            {
                Vector3 V(int k) => new Vector3((float)leaves.Verts[3 * k], (float)leaves.Verts[3 * k + 1], (float)leaves.Verts[3 * k + 2]) * scale;
                var a = V(q); var b = V(q + 1); var c = V(q + 2);
                float area = Vector3.Cross(b - a, c - b).Length() * 0.22f;
                var centre = (a + c) * 0.5f;
                if (o.Cell(centre, out int ci)) o._d[ci] += area / cellVol;
            }
            return o;
        }

        bool Cell(Vector3 p, out int i)
        {
            var f = (p - _min) / _cell;
            int x = (int)MathF.Floor(f.X), y = (int)MathF.Floor(f.Y), z = (int)MathF.Floor(f.Z);
            i = (z * N + y) * N + x;
            return x >= 0 && y >= 0 && z >= 0 && x < N && y < N && z < N;
        }

        static readonly Vector3[] Dirs = MakeDirs();
        static Vector3[] MakeDirs()
        {
            var d = new Vector3[7]; d[0] = Vector3.Up;
            for (int k = 0; k < 6; k++)
            {
                float a = k * MathHelper.TwoPi / 6, e = MathHelper.ToRadians(35);
                d[k + 1] = new Vector3(MathF.Cos(a) * MathF.Cos(e), MathF.Sin(e), MathF.Sin(a) * MathF.Cos(e));
            }
            return d;
        }

        public float At(Vector3 p)
        {
            float step = MathF.Min(_cell.X, MathF.Min(_cell.Y, _cell.Z));
            float sum = 0, wsum = 0;
            foreach (var d in Dirs)
            {
                float tau = 0; var q = p + d * (step * 0.5f);
                for (int s = 0; s < 3 * N; s++)
                {
                    if (!Cell(q, out int ci)) break;
                    tau += _d[ci] * 0.5f * step; // G-function 0.5 (random leaf orientation)
                    q += d * step;
                }
                float w = MathF.Max(d.Y, 0.2f);
                sum += w * MathF.Exp(-tau); wsum += w;
            }
            float t = sum / wsum;
            return 0.18f + 0.82f * t;
        }
    }
}
