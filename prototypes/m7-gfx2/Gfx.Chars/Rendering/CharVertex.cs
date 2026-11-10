using System.Runtime.InteropServices;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using Microsoft.Xna.Framework.Graphics.PackedVector;

namespace Gfx.Chars.Rendering;

/// <summary>Character vertex (60 bytes): position, normal, tangent (w = handedness), uv, 4 joints, 4 weights (unorm8), colour.</summary>
[StructLayout(LayoutKind.Sequential, Pack = 1)]
public struct CharVertex : IVertexType
{
    public Vector3 Position; public Vector3 Normal; public Vector4 Tangent; public Vector2 Uv;
    public Byte4 Joints; public Color Weights; public Color Color;
    public static readonly VertexDeclaration Decl = new(
        new VertexElement(0, VertexElementFormat.Vector3, VertexElementUsage.Position, 0),
        new VertexElement(12, VertexElementFormat.Vector3, VertexElementUsage.Normal, 0),
        new VertexElement(24, VertexElementFormat.Vector4, VertexElementUsage.Tangent, 0),
        new VertexElement(40, VertexElementFormat.Vector2, VertexElementUsage.TextureCoordinate, 0),
        new VertexElement(48, VertexElementFormat.Byte4, VertexElementUsage.BlendIndices, 0),
        new VertexElement(52, VertexElementFormat.Color, VertexElementUsage.BlendWeight, 0),
        new VertexElement(56, VertexElementFormat.Color, VertexElementUsage.Color, 0));
    readonly VertexDeclaration IVertexType.VertexDeclaration => Decl;
}

/// <summary>The morph stream (stream 1): blended blend-shape deltas of position and normal (bind space).</summary>
[StructLayout(LayoutKind.Sequential, Pack = 1)]
public struct MorphVertex : IVertexType
{
    public Vector3 DeltaPosition; public Vector3 DeltaNormal;
    public static readonly VertexDeclaration Decl = new(
        new VertexElement(0, VertexElementFormat.Vector3, VertexElementUsage.TextureCoordinate, 5),
        new VertexElement(12, VertexElementFormat.Vector3, VertexElementUsage.TextureCoordinate, 6));
    readonly VertexDeclaration IVertexType.VertexDeclaration => Decl;
}
