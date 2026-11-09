using System.Runtime.InteropServices;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;

namespace Gfx.Proc.Presentation;

/// <summary>Terrain vertex: position, normal, 8 splat weights (A = grass, dirt, rock, sand; B = snow, forest floor, mud, dry grass).</summary>
[StructLayout(LayoutKind.Sequential, Pack = 1)]
public struct VertexTerrain : IVertexType
{
    public Vector3 Position; public Vector3 Normal; public Vector4 SplatA; public Vector4 SplatB;
    public static readonly VertexDeclaration Decl = new(
        new VertexElement(0, VertexElementFormat.Vector3, VertexElementUsage.Position, 0),
        new VertexElement(12, VertexElementFormat.Vector3, VertexElementUsage.Normal, 0),
        new VertexElement(24, VertexElementFormat.Vector4, VertexElementUsage.TextureCoordinate, 0),
        new VertexElement(40, VertexElementFormat.Vector4, VertexElementUsage.TextureCoordinate, 1));
    readonly VertexDeclaration IVertexType.VertexDeclaration => Decl;
}

/// <summary>Water vertex: position (on the water surface) and data (x = water depth m at this vertex).</summary>
[StructLayout(LayoutKind.Sequential, Pack = 1)]
public struct VertexWater : IVertexType
{
    public Vector3 Position; public Vector4 Data;
    public static readonly VertexDeclaration Decl = new(
        new VertexElement(0, VertexElementFormat.Vector3, VertexElementUsage.Position, 0),
        new VertexElement(12, VertexElementFormat.Vector4, VertexElementUsage.TextureCoordinate, 0));
    public VertexWater(Vector3 p, float depth) { Position = p; Data = new Vector4(depth, 0, 0, 0); }
    readonly VertexDeclaration IVertexType.VertexDeclaration => Decl;
}
