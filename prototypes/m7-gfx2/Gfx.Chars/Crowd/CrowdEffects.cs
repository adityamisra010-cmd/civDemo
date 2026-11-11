using Gfx.Chars.Rendering;
using Gfx.Engine.Effects;
using Gfx.Engine.Mgfx;
using Microsoft.Xna.Framework.Graphics;

namespace Gfx.Chars.Crowd;

/// <summary>Effect names of the instanced crowd (stage render2).</summary>
public static class CrowdEffectNames
{
    public const string Lit = "CrowdLit", Depth = "CrowdDepth";
}

/// <summary>
/// Instanced crowd effects (stage render2; PRESENTATION ONLY). Vertex stage: GPU skinning from the
/// <see cref="CrowdAnimationBank"/> texture (slot 1): per joint influence the skin matrix is lerped between two frames
/// of clip A and, when clip B has weight, two frames of clip B, then the clips are lerped by the weight; the instance's
/// affine world transform follows. Pixel stage: the stage-chars LOD material (vertex albedo with a roughness/metal code,
/// the identity's skin texture, the outfit's painted emblem) with per-instance variation — dye hue and value on
/// saturated cloth, skin tone, mud on the lower legs, metal tarnish — then the shared surface shading (sun GGX with
/// cascaded shadows, IBL, aerial perspective). LOD transitions are a screen-space dithered cross-fade with exactly
/// complementary masks (<c>Lod.y</c> = +1 outgoing, −1 incoming), varied per still sample so accumulation averages it.
/// </summary>
public sealed class CrowdEffects : IEffectSource
{
    public const int SlotSkin = 2, SlotEmblem = 3;

    public IEnumerable<EffectBuilder> CreateEffects()
    {
        yield return Lit();
        yield return Depth();
    }

    static EffectBuilder Attributes(EffectBuilder b) => b
        .Attribute("vec4 a_pos", VertexElementUsage.Position, 0)
        .Attribute("vec3 a_nrm", VertexElementUsage.Normal, 0)
        .Attribute("vec2 a_uv", VertexElementUsage.TextureCoordinate, 0)
        .Attribute("vec4 a_bi", VertexElementUsage.BlendIndices, 0)
        .Attribute("vec4 a_bw", VertexElementUsage.BlendWeight, 0)
        .Attribute("vec4 a_col", VertexElementUsage.Color, 0)
        .Attribute("vec4 i_w0", VertexElementUsage.TextureCoordinate, 1)
        .Attribute("vec4 i_w1", VertexElementUsage.TextureCoordinate, 2)
        .Attribute("vec4 i_w2", VertexElementUsage.TextureCoordinate, 3)
        .Attribute("vec4 i_animA", VertexElementUsage.TextureCoordinate, 4)
        .Attribute("vec4 i_animB", VertexElementUsage.TextureCoordinate, 5)
        .Attribute("vec4 i_look", VertexElementUsage.TextureCoordinate, 6)
        .Attribute("vec4 i_lod", VertexElementUsage.TextureCoordinate, 7)
        .Attribute("vec4 i_panimA", VertexElementUsage.TextureCoordinate, 8)
        .Attribute("vec4 i_panimB", VertexElementUsage.TextureCoordinate, 9)
        .Attribute("vec4 i_pdelta", VertexElementUsage.TextureCoordinate, 10)
        .Vec4("BankInfo", Stage.Vertex).Vec4("BankSize", Stage.Vertex)
        .Texture("CrowdBank", Gfx.Engine.Effects.GlslLib.BoneSlot)
        .VertexLib(SkinLib);

    /// <summary>Bank lookup and two-clip skinning (the GLSL twin of <see cref="CrowdAnimationBank.SkinCpu"/>).</summary>
    public const string SkinLib = @"
vec3 instPos(vec4 p) { return vec3(dot(i_w0, p), dot(i_w1, p), dot(i_w2, p)); }
vec3 instDir(vec3 n) { return vec3(dot(i_w0.xyz, n), dot(i_w1.xyz, n), dot(i_w2.xyz, n)); }
vec4 bankTexel(float row, float joint, float k) {
    float col = floor(row / BankInfo.x);
    float r = row - col * BankInfo.x;
    float x = col * BankInfo.y + joint * 3.0 + k;
    return texture2DLod(CrowdBank, vec2((x + 0.5) * BankSize.z, (r + 0.5) * BankSize.w), 0.0);
}
vec4 bankCol(vec4 animA, vec4 animB, float joint, float k) {
    vec4 a = mix(bankTexel(animA.x, joint, k), bankTexel(animA.y, joint, k), animA.z);
    if (animA.w > 0.0) a = mix(a, mix(bankTexel(animB.x, joint, k), bankTexel(animB.y, joint, k), animB.z), animA.w);
    return a;
}
void skinJoint(vec4 animA, vec4 animB, float j, float w, vec4 p, vec3 n, inout vec3 sp, inout vec3 sn) {
    if (w <= 0.0) return;
    vec4 c0 = bankCol(animA, animB, j, 0.0); vec4 c1 = bankCol(animA, animB, j, 1.0); vec4 c2 = bankCol(animA, animB, j, 2.0);
    sp += w * vec3(dot(c0, p), dot(c1, p), dot(c2, p));
    sn += w * vec3(dot(c0.xyz, n), dot(c1.xyz, n), dot(c2.xyz, n));
}
// MonoGame's GL backend normalises unsigned bytes only for the Color usage: the weights arrive as 0..255.
void skinWith(vec4 animA, vec4 animB, out vec3 sp, out vec3 sn) {
    vec4 p = vec4(a_pos.xyz, 1.0);
    vec4 bw = a_bw * (1.0 / 255.0);
    sp = vec3(0.0); sn = vec3(0.0);
    skinJoint(animA, animB, a_bi.x, bw.x, p, a_nrm, sp, sn);
    skinJoint(animA, animB, a_bi.y, bw.y, p, a_nrm, sp, sn);
    skinJoint(animA, animB, a_bi.z, bw.z, p, a_nrm, sp, sn);
    skinJoint(animA, animB, a_bi.w, bw.w, p, a_nrm, sp, sn);
}
void crowdSkin(out vec3 wp, out vec3 wn) {
    vec3 sp; vec3 sn;
    skinWith(i_animA, i_animB, sp, sn);
    wp = instPos(vec4(sp, 1.0)); wn = instDir(sn);
}
// the previous frame's world position (object motion for TAA): previous bank rows, feet moved back by the delta
vec3 crowdPrevPos() {
    vec3 sp; vec3 sn;
    skinWith(i_panimA, i_panimB, sp, sn);
    return instPos(vec4(sp, 1.0)) - i_pdelta.xyz;
}";

    static EffectBuilder Lit() => Attributes(new EffectBuilder(CrowdEffectNames.Lit)).AddStandardLit().Velocity()
        .Vec4("CrowdParams", Stage.Pixel)
        .Texture("SkinTex", SlotSkin).Texture("Emblem", SlotEmblem)
        .VertexLib(GlslLib.ShadowLookup)
        .Varyings("vec3 v_wpos; vec3 v_nrm; vec2 v_uv; vec4 v_col; vec4 v_lpos; vec4 v_look; vec4 v_lod; float v_h;")
        .PixelLib(CharEffects.SurfaceLib + @"
// interleaved gradient noise (Jimenez 2014), shifted per still sample so accumulation averages the dither
float crowdDither() {
    vec2 o = vec2(CrowdParams.x * 5.588238, CrowdParams.x * 3.233591);
    vec2 p = gl_FragCoord.xy + o;
    return fract(52.9829189 * fract(dot(p, vec2(0.06711056, 0.00583715))));
}
// a small hue rotation in YIQ (chroma rotated by 2 pi h) and a value scale
vec3 dyeShift(vec3 c, float h, float v) {
    float y = dot(c, vec3(0.299, 0.587, 0.114));
    float i = dot(c, vec3(0.596, -0.274, -0.322));
    float q = dot(c, vec3(0.211, -0.523, 0.312));
    float a = h * 6.2831853; float ca = cos(a), sa = sin(a);
    float i2 = i * ca - q * sa, q2 = i * sa + q * ca;
    vec3 r = vec3(y + 0.956 * i2 + 0.621 * q2, y - 0.272 * i2 - 0.647 * q2, y - 1.106 * i2 + 1.703 * q2);
    return max(r * v, vec3(0.0));
}")
        .VertexMain(@"
    vec3 wp; vec3 wn;
    crowdSkin(wp, wn);
    v_wpos = wp; v_nrm = wn; v_uv = a_uv; v_col = a_col; v_look = i_look; v_lod = i_lod; v_h = a_pos.y;
    v_lpos = shadowLookup(wp, wn);
    gl_Position = ViewProj_mul(vec4(wp, 1.0));
    gb_vcur = VelCur_mul(vec4(wp, 1.0));
    gb_vprev = VelParams.x > 0.5 ? VelPrev_mul(vec4(crowdPrevPos(), 1.0)) : gb_vcur;")
        .PixelMain(@"
    if (abs(v_lod.y) > 0.5) { float d = crowdDither(); if (v_lod.y > 0.0 ? (d < v_lod.x) : (d >= v_lod.x)) discard; }
    vec3 Ng = normalize(v_nrm);
    float code = floor(v_col.a * 255.0 + 0.5);
    vec3 albedo; float rough; float metal = 0.0;
    if (code > 254.5) {
        albedo = srgbToLinear(texture2D(SkinTex, v_uv).rgb) * (1.0 + 0.12 * v_look.z);
        rough = 0.5;
    } else if (code > 253.5) { albedo = srgbToLinear(texture2D(Emblem, v_uv).rgb) * mix(0.92, 1.04, v_lod.w); rough = 0.62; }
    else {
        vec3 srgb = v_col.rgb;
        if (code > 127.5) {
            metal = 1.0; rough = (code - 128.0) / 99.0;
            albedo = srgbToLinear(srgb) * (1.0 - 0.25 * v_lod.z);
            rough = clamp(rough + 0.18 * v_lod.z, 0.05, 1.0);
        } else {
            rough = code / 99.0;
            float mx = max(srgb.r, max(srgb.g, srgb.b)), mn = min(srgb.r, min(srgb.g, srgb.b));
            float sat = (mx - mn) / max(mx, 1e-3);
            srgb = mix(srgb, dyeShift(srgb, v_look.x, v_look.y), smoothstep(0.08, 0.30, sat));
            albedo = srgbToLinear(clamp(srgb, 0.0, 1.0));
        }
    }
    // mud and dust on the lower legs and feet (not on metal)
    float mud = v_look.w * (1.0 - smoothstep(0.06, 0.48, v_h)) * (1.0 - metal);
    albedo = mix(albedo, vec3(0.055, 0.045, 0.035), mud * 0.55);
    rough = mix(rough, 0.95, mud);
    float shadow = shadowPcf(v_lpos);
    vec3 c = shadeSurface(albedo, rough, metal, 1.0, Ng, Ng, v_wpos, shadow, code > 254.5 ? 0.0 : 0.3);
    if (code > 254.5) c += albedo * vec3(0.10, 0.03, 0.02) * SunColor.rgb * clamp(1.0 - abs(dot(Ng, SunDir.xyz)), 0.0, 1.0) * shadow;
    gl_FragColor = vec4(applyFog(c, v_wpos), 1.0);");

    static EffectBuilder Depth() => Attributes(new EffectBuilder(CrowdEffectNames.Depth))
        .Matrix("LightViewProj", Stage.Vertex)
        .Varyings("float v_depth;")
        .VertexMain(@"
    vec3 wp; vec3 wn;
    crowdSkin(wp, wn);
    gl_Position = LightViewProj_mul(vec4(wp, 1.0)); v_depth = gl_Position.z / gl_Position.w;")
        .PixelMain("    gl_FragColor = vec4(v_depth, 0.0, 0.0, 1.0);");
}
