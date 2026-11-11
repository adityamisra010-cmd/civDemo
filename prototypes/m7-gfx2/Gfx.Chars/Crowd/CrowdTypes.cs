using System.Runtime.InteropServices;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;

namespace Gfx.Chars.Crowd;

/// <summary>
/// Per-instance data of the instanced crowd (stage render2; 160 bytes, TEXCOORD1..10). PRESENTATION ONLY.
/// <list type="bullet">
/// <item><c>W0..W2</c>: the rows of the affine world transform (<c>wp = (W0·p, W1·p, W2·p)</c>, p in character space).</item>
/// <item><c>AnimA</c>: bank rows of clip A's two frames around the sample time (x, y), the frame fraction (z), the weight
/// of clip B (w; 0 = clip A only).</item>
/// <item><c>AnimB</c>: the same for clip B (x, y, z); w unused.</item>
/// <item><c>Look</c>: x = dye hue shift (turns), y = dye value scale, z = skin tone (−1 darker … +1 lighter),
/// w = mud on the lower legs (0..1).</item>
/// <item><c>Lod</c>: x = dithered cross-fade amount (0..1), y = side of the cross-fade (+1 outgoing level, −1 incoming,
/// 0 none), z = metal tarnish (0..1), w = per-soldier seed (0..1).</item>
/// <item><c>PrevAnimA</c>, <c>PrevAnimB</c>, <c>PrevDelta</c>: the previous frame's bank rows and the move of the feet
/// since then (xyz), for the object motion that temporal anti-aliasing reads (equal to this frame's without history).</item>
/// </list>
/// </summary>
[StructLayout(LayoutKind.Sequential, Pack = 1)]
public struct CrowdInstance : IVertexType
{
    public Vector4 W0, W1, W2, AnimA, AnimB, Look, Lod, PrevAnimA, PrevAnimB, PrevDelta;
    public static readonly VertexDeclaration Decl = new(
        new VertexElement(0, VertexElementFormat.Vector4, VertexElementUsage.TextureCoordinate, 1),
        new VertexElement(16, VertexElementFormat.Vector4, VertexElementUsage.TextureCoordinate, 2),
        new VertexElement(32, VertexElementFormat.Vector4, VertexElementUsage.TextureCoordinate, 3),
        new VertexElement(48, VertexElementFormat.Vector4, VertexElementUsage.TextureCoordinate, 4),
        new VertexElement(64, VertexElementFormat.Vector4, VertexElementUsage.TextureCoordinate, 5),
        new VertexElement(80, VertexElementFormat.Vector4, VertexElementUsage.TextureCoordinate, 6),
        new VertexElement(96, VertexElementFormat.Vector4, VertexElementUsage.TextureCoordinate, 7),
        new VertexElement(112, VertexElementFormat.Vector4, VertexElementUsage.TextureCoordinate, 8),
        new VertexElement(128, VertexElementFormat.Vector4, VertexElementUsage.TextureCoordinate, 9),
        new VertexElement(144, VertexElementFormat.Vector4, VertexElementUsage.TextureCoordinate, 10));
    readonly VertexDeclaration IVertexType.VertexDeclaration => Decl;

    public void SetWorld(in Matrix m)
    {
        W0 = new Vector4(m.M11, m.M21, m.M31, m.M41);
        W1 = new Vector4(m.M12, m.M22, m.M32, m.M42);
        W2 = new Vector4(m.M13, m.M23, m.M33, m.M43);
    }
}

/// <summary>
/// Cosmetic per-soldier appearance, drawn from the soldier's seed within controlled bounds (QM6: variation multiplies
/// quality; every soldier stays inside the outfit's look).
/// </summary>
public readonly record struct CrowdLook(float DyeHue, float DyeValue, float SkinTone, float Mud, float Tarnish, float Height, float Width, float Seed)
{
    /// <summary>Bounds of each term (inclusive).</summary>
    public const float MaxDyeHue = 0.035f, MinDyeValue = 0.80f, MaxDyeValue = 1.12f, MaxSkinTone = 0.12f, MinHeight = 0.95f, MaxHeight = 1.05f,
        MinWidth = 0.96f, MaxWidth = 1.04f;

    /// <summary>A deterministic look from a soldier key (integer hash; no RNG state).</summary>
    public static CrowdLook FromKey(uint key)
    {
        static float U(uint h) => (h >> 8) * (1f / 16777216f);
        uint h0 = Hash(key), h1 = Hash(h0 ^ 0x9E3779B9u), h2 = Hash(h1 ^ 0x85EBCA6Bu), h3 = Hash(h2 ^ 0xC2B2AE35u);
        uint h4 = Hash(h3 ^ 0x27D4EB2Fu), h5 = Hash(h4 ^ 0x165667B1u), h6 = Hash(h5 ^ 0xD3A2646Cu), h7 = Hash(h6 ^ 0xFD7046C5u);
        return new CrowdLook(
            (U(h0) * 2 - 1) * MaxDyeHue,
            MinDyeValue + U(h1) * (MaxDyeValue - MinDyeValue),
            (U(h2) * 2 - 1) * MaxSkinTone / 0.12f,
            0.1f + 0.75f * U(h3),
            U(h4),
            MinHeight + U(h5) * (MaxHeight - MinHeight),
            MinWidth + U(h6) * (MaxWidth - MinWidth),
            U(h7));
    }

    /// <summary>Murmur3 finaliser (presentation hashing only).</summary>
    public static uint Hash(uint x)
    {
        x ^= x >> 16; x *= 0x85EBCA6Bu; x ^= x >> 13; x *= 0xC2B2AE35u; x ^= x >> 16;
        return x;
    }

    public Vector4 Packed => new(DyeHue, DyeValue, SkinTone, Mud);

    /// <summary>The look without variation: no dye shift, value 1, base skin tone, no mud or tarnish, unit size, and the
    /// seed at which the emblem tone factor is 1.</summary>
    public static readonly CrowdLook Neutral = new(0f, 1f, 0f, 0f, 0f, 1f, 1f, 2f / 3f);
}

/// <summary>
/// One soldier of the crowd as the presentation sees it: where it stands, which baked look it wears, which two clips it
/// plays and how they blend. Filled by a scene (scripted) or, later, from published battle state; never written back.
/// </summary>
public struct CrowdAgent
{
    public Vector3 Position;
    public float Yaw;
    /// <summary>Index into <see cref="CrowdRenderer.Variants"/> (identity × outfit).</summary>
    public int Variant;
    public int ClipA; public float TimeA;
    public int ClipB; public float TimeB;
    /// <summary>Weight of clip B (0 = clip A only).</summary>
    public float Blend;
    public CrowdLook Look;
    public bool Hidden;
}
