using Gfx.Engine.Effects;
using Gfx.Engine.Mgfx;
using Microsoft.Xna.Framework.Graphics;

namespace Gfx.Proc.Environment;

/// <summary>Effect names of the environment stage (registered by <see cref="EnvEffects"/>).</summary>
public static class EnvEffectNames
{
    public const string Bark = "EnvBark", BarkDepth = "EnvBarkDepth", Leaf = "EnvLeaf", LeafDepth = "EnvLeafDepth";
    public const string Impostor = "EnvImpostor", ImpostorDepth = "EnvImpostorDepth";
    public const string Cover = "EnvCover", Rock = "EnvRock", RockDepth = "EnvRockDepth";
    public const string Water = "EnvWater", Structure = "EnvStructure", StructureDepth = "EnvStructureDepth";
}

/// <summary>
/// Shared GLSL for the environment effects: sRGB decode, interleaved-gradient-noise dithering (per still sample),
/// the LOD cross-fade test, instance transforms and the deterministic wind model. PRESENTATION ONLY.
/// <para>Uniforms used by these helpers (declared by each effect): <c>EnvParams</c> (x = sample index, y = sample count,
/// z = 1 for stochastic alpha in jittered stills, w = alpha cut-off), <c>WindParams</c> (x = scene time s, y = wind
/// strength 0..1, z = 1 wind on, w = gust phase), <c>WindDir</c> (xyz = horizontal unit wind direction).</para>
/// </summary>
public static class EnvGlsl
{
    public const string Common = @"
vec3 decodeSrgb(vec3 c) {
    vec3 lo = c / 12.92, hi = pow((c + 0.055) / 1.055, vec3(2.4));
    return vec3(c.r <= 0.04045 ? lo.r : hi.r, c.g <= 0.04045 ? lo.g : hi.g, c.b <= 0.04045 ? lo.b : hi.b);
}
vec3 decodeNormal(vec4 t) { vec2 xy = t.rg * 2.0 - 1.0; return vec3(xy, sqrt(max(1.0 - dot(xy, xy), 0.0))); }
// interleaved gradient noise (Jimenez 2014); each still sample shifts the pattern so accumulation averages it
float ign(vec2 p) { return fract(52.9829189 * fract(dot(p, vec2(0.06711056, 0.00583715)))); }
// sine-free value hash (Dave Hoskins' hash12, MIT): fract(sin(x)) loses the fractional bits of its argument at world
// coordinates of hundreds of metres in float32; the input is wrapped first so the products keep theirs
float envHash(vec2 p) {
    vec3 p3 = fract(vec3(mod(p.xyx, 4096.0)) * 0.1031);
    p3 += dot(p3, p3.yzx + 33.33);
    return fract((p3.x + p3.y) * p3.z);
}
float envDither(vec2 salt) {
    vec2 o = vec2(EnvParams.x * 5.588238, EnvParams.x * 3.233591) + salt;
    return ign(gl_FragCoord.xy + o);
}
// LOD cross-fade: lod.y = +1 keeps the pixel where the dither >= fade (outgoing level), -1 where it is < fade
// (incoming level), 0 never discards. The two levels of a tree in transition are exact complements.
void lodDither(vec2 lod) {
    if (abs(lod.y) < 0.5) return;
    float d = envDither(vec2(0.0));
    if (lod.y > 0.0 ? (d < lod.x) : (d >= lod.x)) discard;
}
float alphaCut(vec2 salt) { return EnvParams.z > 0.5 ? 0.3 + 0.4 * envDither(salt) : EnvParams.w; }";

    /// <summary>
    /// Foliage shading (after the engine's lighting library): slightly wrapped diffuse (thin scattering leaves), a
    /// weak GGX lobe (roughness 0.65) with a waxy F0 of 0.03 and no sky-reflection term (rough leaves at grazing angles otherwise turn
    /// white), sky light from the SH irradiance with the canopy occlusion. Octahedral normal packing for the impostors.
    /// </summary>
    public const string Foliage = @"
vec3 shadeFoliage(vec3 albedo, vec3 n, vec3 wpos, float sh, float ao, float specScale) {
    vec3 V = normalize(CameraPos.xyz - wpos);
    vec3 L = SunDir.xyz;
    float nol = max(dot(n, L), 0.0);
    float diff = max((dot(n, L) + 0.25) / 1.25, 0.0);
    vec3 c = albedo / PI * SunColor.rgb * (diff * sh);
    vec3 H = normalize(V + L);
    float nov = max(dot(n, V), 1e-4), noh = max(dot(n, H), 0.0), voh = max(dot(V, H), 0.0);
    float a = 0.42;
    float F = 0.03 + 0.97 * pow(1.0 - voh, 5.0);
    c += vec3(dGgx(noh, a) * vSmithGgx(nov, nol, a) * F * specScale) * SunColor.rgb * (nol * sh);
    vec3 irr = LightModel.y > 0.5 ? shIrradiance(n) * IblParams.y : mix(AmbientGround.rgb, AmbientSky.rgb, n.y * 0.5 + 0.5) * PI;
    vec3 amb = albedo * irr / PI * ao * mix(0.85, 1.0, sh);
    gbAmbient(amb); // stage render2: the ambient share for screen-space AO
    c += amb;
    return c;
}
vec2 octEnc(vec3 n) {
    n /= abs(n.x) + abs(n.y) + abs(n.z);
    vec2 p = n.xz;
    if (n.y < 0.0) p = (1.0 - abs(p.yx)) * vec2(p.x >= 0.0 ? 1.0 : -1.0, p.y >= 0.0 ? 1.0 : -1.0);
    return p * 0.5 + 0.5;
}
vec3 octDec(vec2 e) {
    vec2 f = e * 2.0 - 1.0;
    vec3 n = vec3(f.x, 1.0 - abs(f.x) - abs(f.y), f.y);
    if (n.y < 0.0) { vec2 t = n.xz; n.xz = (1.0 - abs(t.yx)) * vec2(t.x >= 0.0 ? 1.0 : -1.0, t.y >= 0.0 ? 1.0 : -1.0); }
    return normalize(n);
}";

    /// <summary>Vertex-stage wind (needs <c>WindParams</c>, <c>WindDir</c> in the vertex stage).</summary>
    public const string Wind = @"
// h: height in the tree 0..1, flex: branch flexibility 0..1, ph: branch phase 0..1, tip: card tip 0..1,
// instPh: instance phase 0..1, s: instance scale (the model is in metres before it)
vec3 windOffset(float h, float flex, float ph, float tip, float instPh, float s) {
    if (WindParams.z < 0.5) return vec3(0.0);
    float t = WindParams.x, k = WindParams.y;
    vec3 wd = WindDir.xyz;
    float gust = 0.55 + 0.45 * sin(t * 0.37 + instPh * 6.2832 + WindParams.w);
    float sway = (0.35 + 0.55 * sin(t * 0.85 + instPh * 6.2832) + 0.2 * sin(t * 2.07 + instPh * 3.7)) * gust;
    vec3 off = wd * (h * h * (0.05 + 0.20 * k) * s * sway);
    float bph = ph * 6.2832 + instPh * 2.9;
    vec3 side = vec3(-wd.z, 0.0, wd.x);
    off += (wd * 0.55 + side * 0.35 + vec3(0.0, 0.25, 0.0)) * (flex * flex * (0.04 + 0.10 * k) * s * sin(t * (2.3 + ph) + bph) * gust);
    off += tip * vec3(sin(t * 7.3 + bph * 2.0), 0.6 * cos(t * 6.1 + bph), sin(t * 8.7 + bph * 1.3)) * (0.012 + 0.03 * k) * s * flex;
    return off;
}";

    /// <summary>Adds the tree instance attributes (TEXCOORD1..5) and the instance transform helpers.</summary>
    public static EffectBuilder AddTreeInstance(this EffectBuilder b) => b
        .Attribute("vec4 i_w0", VertexElementUsage.TextureCoordinate, 1)
        .Attribute("vec4 i_w1", VertexElementUsage.TextureCoordinate, 2)
        .Attribute("vec4 i_w2", VertexElementUsage.TextureCoordinate, 3)
        .Attribute("vec4 i_tint", VertexElementUsage.TextureCoordinate, 4)
        .Attribute("vec4 i_lod", VertexElementUsage.TextureCoordinate, 5)
        .VertexLib("vec3 instPos(vec4 p) { return vec3(dot(i_w0, p), dot(i_w1, p), dot(i_w2, p)); }\n" +
                   "vec3 instDir(vec3 n) { return vec3(dot(i_w0.xyz, n), dot(i_w1.xyz, n), dot(i_w2.xyz, n)); }");

    /// <summary>The uniforms of <see cref="Common"/> and <see cref="Wind"/>.</summary>
    public static EffectBuilder AddEnvParams(this EffectBuilder b) => b
        .Vec4("EnvParams", Stage.Pixel).Vec4("WindParams", Stage.Vertex).Vec4("WindDir", Stage.Vertex);
}

/// <summary>All environment effects (discovered by <see cref="EffectLibrary"/>).</summary>
public sealed class EnvEffects : IEffectSource
{
    public IEnumerable<EffectBuilder> CreateEffects()
    {
        foreach (var e in Trees.TreeEffects.Create()) yield return e;
        yield return Ground.CoverEffects.Create();
        yield return Ground.RockEffects.Create();
        yield return Water.WaterEffects.Create();
        foreach (var e in Structures.StructureEffects.Create()) yield return e;
    }
}
