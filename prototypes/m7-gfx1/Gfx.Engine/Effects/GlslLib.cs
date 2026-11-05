using Gfx.Engine.Mgfx;
using Microsoft.Xna.Framework.Graphics;

namespace Gfx.Engine.Effects;

/// <summary>
/// Shared GLSL chunks and the STANDARD parameter set. Any effect that declares the standard parameters
/// (via <see cref="AddStandardLit"/>) is filled by <c>RenderContext.Apply(effect)</c>: lanes writing new
/// effects (terrain splat, water, units) should use these names so lighting/fog/shadows stay consistent.
/// <para>Standard names — VS: <c>ViewProj</c>, <c>LightViewProj</c> (matrices), <c>ShadowOffset</c> (x = normal offset in
/// metres for the shadow lookup position: use <c>shadowLookup(wp, n)</c>). PS: <c>CameraPos</c>,
/// <c>SunDir</c> (xyz = unit vector TOWARD the light), <c>SunColor</c> (linear rgb × intensity),
/// <c>AmbientSky</c>, <c>AmbientGround</c>, <c>FogColor</c>, <c>FogParams</c> (x density /m, y height falloff /m,
/// z base height m, w max opacity), <c>ShadowParams</c> (x 1/size, y depth bias, z enabled 0/1, w strength).
/// Textures: <c>ShadowMap</c> slot 0 (point-clamp). Slot 1 is reserved for bone textures, slots 2+ for materials.</para>
/// All lighting is in LINEAR light; scene targets hold linear HDR; the post pass tonemaps (ACES) and gamma-encodes.
/// </summary>
public static class GlslLib
{
    public const int ShadowSlot = 0, BoneSlot = 1, FirstMaterialSlot = 2;

    /// <summary>Adds the standard lit parameter set, the shadow texture and the pixel lighting library.</summary>
    public static EffectBuilder AddStandardLit(this EffectBuilder b) => b
        .Matrix("ViewProj", Stage.Vertex).Matrix("LightViewProj", Stage.Vertex).Vec4("ShadowOffset", Stage.Vertex)
        .Vec4("CameraPos", Stage.Pixel).Vec4("SunDir", Stage.Pixel).Vec4("SunColor", Stage.Pixel)
        .Vec4("AmbientSky", Stage.Pixel).Vec4("AmbientGround", Stage.Pixel)
        .Vec4("FogColor", Stage.Pixel).Vec4("FogParams", Stage.Pixel).Vec4("ShadowParams", Stage.Pixel)
        .Texture("ShadowMap", ShadowSlot)
        .PixelLib(Lighting);

    /// <summary>Instance attributes: affine world matrix columns (TEXCOORD1..3) and tint (TEXCOORD4).</summary>
    public static EffectBuilder AddInstanceTransform(this EffectBuilder b) => b
        .Attribute("vec4 i_w0", VertexElementUsage.TextureCoordinate, 1)
        .Attribute("vec4 i_w1", VertexElementUsage.TextureCoordinate, 2)
        .Attribute("vec4 i_w2", VertexElementUsage.TextureCoordinate, 3)
        .Attribute("vec4 i_tint", VertexElementUsage.TextureCoordinate, 4)
        .VertexLib("vec3 instPos(vec4 p) { return vec3(dot(i_w0, p), dot(i_w1, p), dot(i_w2, p)); }\n" +
                   "vec3 instDir(vec3 n) { return vec3(dot(i_w0.xyz, n), dot(i_w1.xyz, n), dot(i_w2.xyz, n)); }");

    /// <summary>
    /// Vertex-stage helper for effects using <see cref="AddStandardLit"/>: light-space position with normal-offset
    /// (kills acne on grazing faces). Add with <c>.VertexLib(GlslLib.ShadowLookup)</c>.
    /// </summary>
    public const string ShadowLookup =
        "vec4 shadowLookup(vec3 wp, vec3 wn) { return LightViewProj_mul(vec4(wp + normalize(wn) * ShadowOffset.x, 1.0)); }";

    public const string Lighting = @"
vec3 srgbToLinear(vec3 c) { return pow(max(c, vec3(0.0)), vec3(2.2)); }
float shadowPcf(vec4 lpos) {
    if (ShadowParams.z < 0.5) return 1.0;
    vec3 lc = lpos.xyz / lpos.w;
    vec2 uv = vec2(lc.x * 0.5 + 0.5, -lc.y * 0.5 + 0.5);
    if (uv.x < 0.0 || uv.x > 1.0 || uv.y < 0.0 || uv.y > 1.0 || lc.z > 1.0) return 1.0;
    float ts = ShadowParams.x; float lit = 0.0;
    for (int y = -1; y <= 1; y++) for (int x = -1; x <= 1; x++) {
        float d = texture2D(ShadowMap, uv + vec2(float(x), float(y)) * ts).r;
        lit += (lc.z - ShadowParams.y) <= d ? 1.0 : 0.0;
    }
    return mix(1.0, lit / 9.0, ShadowParams.w);
}
// Lambert sun + hemispheric ambient, linear light.
vec3 shadeLit(vec3 albedoLin, vec3 n, float shadow) {
    float ndl = max(dot(n, SunDir.xyz), 0.0);
    vec3 amb = mix(AmbientGround.rgb, AmbientSky.rgb, n.y * 0.5 + 0.5);
    return albedoLin * (amb + SunColor.rgb * (ndl * shadow));
}
// Exponential height fog (analytic integral along the view ray).
vec3 applyFog(vec3 c, vec3 wpos) {
    vec3 d = wpos - CameraPos.xyz;
    float dist = length(d);
    float k = FogParams.y;
    float camH = CameraPos.y - FogParams.z;
    float dy = k * d.y;
    float line = abs(dy) > 0.0001 ? (1.0 - exp(-dy)) / dy : 1.0;
    float t = FogParams.x * exp(-k * camH) * dist * line;
    float f = min(1.0 - exp(-max(t, 0.0)), FogParams.w);
    return mix(c, FogColor.rgb, f);
}";

    /// <summary>Linear HDR → display: exposure, ACES (Narkowicz 2015 fit), gamma 2.2.</summary>
    public const string Tonemap = @"
vec3 acesFit(vec3 x) { return clamp((x * (2.51 * x + 0.03)) / (x * (2.43 * x + 0.59) + 0.14), 0.0, 1.0); }
vec3 linearToSrgb(vec3 c) { return pow(max(c, vec3(0.0)), vec3(1.0 / 2.2)); }";
}
