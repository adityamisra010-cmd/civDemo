// Vertex layouts adapted from the recon probe (scratchpad/m7gfx/probe-mono-probe/src/Geometry.cs: VPNC,
// InstanceData, VSkin), extended with per-instance tint and animation channels.
using System.Runtime.InteropServices;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using Microsoft.Xna.Framework.Graphics.PackedVector;

namespace Gfx.Engine.Rendering;

/// <summary>Position, normal, sRGB vertex colour (converted to linear in the shader).</summary>
[StructLayout(LayoutKind.Sequential, Pack = 1)]
public struct VertexPNC : IVertexType
{
    public Vector3 Position; public Vector3 Normal; public Color Color;
    public static readonly VertexDeclaration Decl = new(
        new VertexElement(0, VertexElementFormat.Vector3, VertexElementUsage.Position, 0),
        new VertexElement(12, VertexElementFormat.Vector3, VertexElementUsage.Normal, 0),
        new VertexElement(24, VertexElementFormat.Color, VertexElementUsage.Color, 0));
    public VertexPNC(Vector3 p, Vector3 n, Color c) { Position = p; Normal = n; Color = c; }
    readonly VertexDeclaration IVertexType.VertexDeclaration => Decl;
}

/// <summary>Skinned vertex: up to 4 joint influences (indices as bytes, weights summing to 1).</summary>
[StructLayout(LayoutKind.Sequential, Pack = 1)]
public struct VertexSkinned : IVertexType
{
    public Vector3 Position; public Vector3 Normal; public Color Color; public Byte4 Joints; public Vector4 Weights;
    public static readonly VertexDeclaration Decl = new(
        new VertexElement(0, VertexElementFormat.Vector3, VertexElementUsage.Position, 0),
        new VertexElement(12, VertexElementFormat.Vector3, VertexElementUsage.Normal, 0),
        new VertexElement(24, VertexElementFormat.Color, VertexElementUsage.Color, 0),
        new VertexElement(28, VertexElementFormat.Byte4, VertexElementUsage.BlendIndices, 0),
        new VertexElement(32, VertexElementFormat.Vector4, VertexElementUsage.BlendWeight, 0));
    public VertexSkinned(Vector3 p, Vector3 n, Color c, Byte4 joints, Vector4 weights) { Position = p; Normal = n; Color = c; Joints = joints; Weights = weights; }
    readonly VertexDeclaration IVertexType.VertexDeclaration => Decl;
}

/// <summary>Per-instance affine world transform (matrix columns 1..3, XNA row-vector convention) + linear tint.</summary>
[StructLayout(LayoutKind.Sequential, Pack = 1)]
public struct InstanceTransform : IVertexType
{
    public Vector4 W0, W1, W2, Tint;
    public static readonly VertexDeclaration Decl = new(
        new VertexElement(0, VertexElementFormat.Vector4, VertexElementUsage.TextureCoordinate, 1),
        new VertexElement(16, VertexElementFormat.Vector4, VertexElementUsage.TextureCoordinate, 2),
        new VertexElement(32, VertexElementFormat.Vector4, VertexElementUsage.TextureCoordinate, 3),
        new VertexElement(48, VertexElementFormat.Vector4, VertexElementUsage.TextureCoordinate, 4));
    public static InstanceTransform From(in Matrix m, Vector4 tint) => new()
    {
        W0 = new Vector4(m.M11, m.M21, m.M31, m.M41),
        W1 = new Vector4(m.M12, m.M22, m.M32, m.M42),
        W2 = new Vector4(m.M13, m.M23, m.M33, m.M43),
        Tint = tint,
    };
    public static InstanceTransform From(in Matrix m) => From(m, Vector4.One);
    readonly VertexDeclaration IVertexType.VertexDeclaration => Decl;
}

/// <summary>Per-instance data for <c>SkinnedLit</c>: transform, tint, and Anim (x pose row A, y pose row B, z blend).</summary>
[StructLayout(LayoutKind.Sequential, Pack = 1)]
public struct SkinnedInstance : IVertexType
{
    public Vector4 W0, W1, W2, Tint, Anim;
    public static readonly VertexDeclaration Decl = new(
        new VertexElement(0, VertexElementFormat.Vector4, VertexElementUsage.TextureCoordinate, 1),
        new VertexElement(16, VertexElementFormat.Vector4, VertexElementUsage.TextureCoordinate, 2),
        new VertexElement(32, VertexElementFormat.Vector4, VertexElementUsage.TextureCoordinate, 3),
        new VertexElement(48, VertexElementFormat.Vector4, VertexElementUsage.TextureCoordinate, 4),
        new VertexElement(64, VertexElementFormat.Vector4, VertexElementUsage.TextureCoordinate, 5));
    public static SkinnedInstance From(in Matrix m, Vector4 tint, int rowA, int rowB, float blend)
    {
        var t = InstanceTransform.From(m, tint);
        return new SkinnedInstance { W0 = t.W0, W1 = t.W1, W2 = t.W2, Tint = tint, Anim = new Vector4(rowA, rowB, blend, 0) };
    }
    readonly VertexDeclaration IVertexType.VertexDeclaration => Decl;
}

/// <summary>Position-only vertex for full-screen passes.</summary>
public static class FullScreen
{
    /// <summary>One triangle covering the viewport (NDC).</summary>
    public static readonly VertexPosition[] Triangle =
    {
        new(new Vector3(-1, -1, 0)), new(new Vector3(-1, 3, 0)), new(new Vector3(3, -1, 0)),
    };
}
