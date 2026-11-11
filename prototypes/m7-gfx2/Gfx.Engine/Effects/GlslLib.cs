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
/// Textures: <c>ShadowMap</c> slot 0 (point-clamp). Slot 1 is reserved for bone textures, slots 2–12 for materials.</para>
/// <para>G2 (stage render1) adds, PS: <c>LightModel</c> (x PBR, y IBL, z aerial perspective, w cascaded shadows),
/// <c>IblParams</c> (x specular mip count − 1, y diffuse scale, z specular scale, w equirect rotation in turns),
/// <c>Sh0</c>…<c>Sh8</c> (irradiance spherical harmonics), <c>SkySun</c> (sun direction of the sky table),
/// <c>Aerial</c> (rgb extinction /m, w height-fog scale), <c>AerialR</c>/<c>AerialM</c>/<c>AerialMs</c> (in-scatter source
/// terms), <c>FogSun</c>/<c>FogAmb</c> (height-fog lighting), <c>CsmAtlas</c>, <c>CsmScale0..3</c>, <c>CsmOffset0..3</c>;
/// textures <c>IblSpec</c> (13), <c>BrdfLut</c> (14), <c>SkyView</c> (15). The G1 functions keep their names and
/// signatures and run their G1 code when the switches are 0. CONVENTION: <c>shadeLit</c> reads the varying
/// <c>v_wpos</c> (world position) for the view vector, so every effect using <see cref="AddStandardLit"/> declares
/// <c>vec3 v_wpos</c> (all G1 effects do; the effect-load test catches one that does not).</para>
/// All lighting is in LINEAR light; scene targets hold linear HDR; the post pass tonemaps and gamma-encodes.
/// </summary>
public static class GlslLib
{
    public const int ShadowSlot = 0, BoneSlot = 1, FirstMaterialSlot = 2, LastMaterialSlot = 12;
    public const int IblSpecSlot = 13, BrdfLutSlot = 14, SkyViewSlot = 15;
    /// <summary>GLSL extension for explicit-LOD and gradient sampling in fragment shaders (guarded, with fallbacks).</summary>
    public const string TextureLodExt = "GL_ARB_shader_texture_lod";

    /// <summary>Adds the standard lit parameter set, the shadow texture and the pixel lighting library.</summary>
    public static EffectBuilder AddStandardLit(this EffectBuilder b) => b
        .Matrix("ViewProj", Stage.Vertex).Matrix("LightViewProj", Stage.Vertex).Vec4("ShadowOffset", Stage.Vertex)
        .Vec4("CameraPos", Stage.Pixel).Vec4("SunDir", Stage.Pixel).Vec4("SunColor", Stage.Pixel)
        .Vec4("AmbientSky", Stage.Pixel).Vec4("AmbientGround", Stage.Pixel)
        .Vec4("FogColor", Stage.Pixel).Vec4("FogParams", Stage.Pixel).Vec4("ShadowParams", Stage.Pixel)
        .AddG2Standard()
        .Texture("ShadowMap", ShadowSlot)
        .PixelLib(Lighting);

    /// <summary>The G2 standard uniforms and textures (see the class summary).</summary>
    static EffectBuilder AddG2Standard(this EffectBuilder b)
    {
        b.Extension(TextureLodExt)
         .Vec4("LightModel", Stage.Pixel).Vec4("IblParams", Stage.Pixel).Vec4("SkySun", Stage.Pixel).Vec4("Aerial", Stage.Pixel)
         .Vec4("CsmAtlas", Stage.Pixel)
         .Vec4("AerialR", Stage.Pixel).Vec4("AerialM", Stage.Pixel).Vec4("AerialMs", Stage.Pixel).Vec4("FogSun", Stage.Pixel).Vec4("FogAmb", Stage.Pixel);
        for (int i = 0; i < 9; i++) b.Vec4("Sh" + i, Stage.Pixel);
        for (int i = 0; i < 4; i++) b.Vec4("CsmScale" + i, Stage.Pixel).Vec4("CsmOffset" + i, Stage.Pixel);
        return b.Texture("IblSpec", IblSpecSlot).Texture("BrdfLut", BrdfLutSlot).Texture("SkyView", SkyViewSlot);
    }

    /// <summary>Uniforms and texture an effect needs to call <c>skyView()</c> without the full lighting set.</summary>
    public static EffectBuilder AddSkyView(this EffectBuilder b) => b
        .Vec4("SkySun", Stage.Pixel).Vec4("IblParams", Stage.Pixel).Texture("SkyView", SkyViewSlot).PixelLib(SkyLib);

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
    /// (kills acne on grazing faces). Add with <c>.VertexLib(GlslLib.ShadowLookup)</c>. With cascaded shadows the
    /// result is cascade 0's clip position; the pixel stage derives the other cascades from it.
    /// </summary>
    public const string ShadowLookup =
        "vec4 shadowLookup(vec3 wp, vec3 wn) { return LightViewProj_mul(vec4(wp + normalize(wn) * ShadowOffset.x, 1.0)); }";

    /// <summary>Sky-view table lookup and equirectangular mapping, shared by the sky, lit and water shaders.</summary>
    public const string SkyLib = @"
const float PI = 3.14159265;
// Sky-view table (AtmosphereModel.SkyViewTable): u = azimuth from the sun / pi, v = 0.5 +- 0.5*sqrt(|elevation| / (pi/2)).
vec3 skyView(vec3 d) {
    float el = asin(clamp(d.y, -1.0, 1.0));
    float v = 0.5 + 0.5 * sign(el) * sqrt(min(abs(el), 1.5707963) / 1.5707963);
    vec2 h = d.xz; vec2 s = SkySun.xz;
    float hl = length(h), sl = length(s);
    float ca = (hl > 1e-5 && sl > 1e-5) ? dot(h, s) / (hl * sl) : 1.0;
    float u = acos(clamp(ca, -1.0, 1.0)) / PI;
    return texture2D(SkyView, vec2(u, v)).rgb;
}
// Equirectangular direction -> uv (u = 0.5 looks along -Z, v = 0 is straight up); IblParams.w rotates (turns).
vec2 equirectUv(vec3 d) {
    return vec2(atan(d.x, -d.z) / (2.0 * PI) + 0.5 + IblParams.w, acos(clamp(d.y, -1.0, 1.0)) / PI);
}";

    public const string Lighting = SkyLib + @"
vec3 srgbToLinear(vec3 c) { return pow(max(c, vec3(0.0)), vec3(2.2)); }

// ---------------------------------------------------------------- shadows
// G1: one map, 3x3 point PCF.
float shadowSingle(vec4 lpos) {
    vec3 lc = lpos.xyz / lpos.w;
    vec2 uv = vec2(lc.x * 0.5 + 0.5, -lc.y * 0.5 + 0.5);
    if (uv.x < 0.0 || uv.x > 1.0 || uv.y < 0.0 || uv.y > 1.0 || lc.z > 1.0) return 1.0;
    float ts = ShadowParams.x; float lit = 0.0;
    for (int y = -1; y <= 1; y++) for (int x = -1; x <= 1; x++) {
        float d = texture2D(ShadowMap, uv + vec2(float(x), float(y)) * ts).r;
        lit += (lc.z - ShadowParams.y) <= d ? 1.0 : 0.0;
    }
    return lit / 9.0;
}
// G2: 3x3 box of bilinear comparisons (16 texels, tent-like footprint) inside one 2x2-atlas tile.
float shadowTile(vec3 lc, vec2 tile, float bias) {
    float atlas = CsmAtlas.y;
    vec2 uv = (tile + vec2(lc.x * 0.5 + 0.5, -lc.y * 0.5 + 0.5)) * 0.5;
    vec2 st = uv * atlas - 0.5;
    vec2 f = fract(st);
    vec2 base = (floor(st) + 0.5) / atlas;
    vec2 lo = tile * 0.5 + 0.5 / atlas, hi = tile * 0.5 + 0.5 - 0.5 / atlas;
    float z = lc.z - bias; float sum = 0.0;
    for (int j = -1; j <= 2; j++) {
        float wy = j == -1 ? 1.0 - f.y : (j == 2 ? f.y : 1.0);
        for (int i = -1; i <= 2; i++) {
            float wx = i == -1 ? 1.0 - f.x : (i == 2 ? f.x : 1.0);
            vec2 p = clamp(base + vec2(float(i), float(j)) / atlas, lo, hi);
            sum += wx * wy * (z <= texture2D(ShadowMap, p).r ? 1.0 : 0.0);
        }
    }
    return sum / 9.0;
}
// Cascade selection by containment (the finest cascade that holds the point, with a filter margin); blends into
// the next cascade over the outer 10 % of each one. CsmScaleK.w is cascade K's depth bias.
float shadowCascaded(vec4 lpos) {
    vec3 lc0 = lpos.xyz / lpos.w;
    float margin = 1.0 - 8.0 / CsmAtlas.y;
    vec3 c[4]; float bias[4];
    c[0] = lc0 * CsmScale0.xyz + CsmOffset0.xyz; c[1] = lc0 * CsmScale1.xyz + CsmOffset1.xyz;
    c[2] = lc0 * CsmScale2.xyz + CsmOffset2.xyz; c[3] = lc0 * CsmScale3.xyz + CsmOffset3.xyz;
    bias[0] = CsmScale0.w; bias[1] = CsmScale1.w; bias[2] = CsmScale2.w; bias[3] = CsmScale3.w;
    int count = int(CsmAtlas.x + 0.5);
    for (int k = 0; k < 4; k++) {
        if (k >= count) break;
        vec3 lc = c[k];
        float m = max(abs(lc.x), abs(lc.y));
        if (m < margin && lc.z < 1.0) {
            float s = shadowTile(lc, vec2(mod(float(k), 2.0), floor(float(k) / 2.0)), bias[k]);
            float edge = smoothstep(margin * 0.9, margin, m);
            gb_casc = float(k + 1); // stage render2: cascade debug view
            if (edge > 0.0 && k + 1 < count) {
                vec3 ln = c[k + 1];
                if (max(abs(ln.x), abs(ln.y)) < margin && ln.z < 1.0) {
                    s = mix(s, shadowTile(ln, vec2(mod(float(k + 1), 2.0), floor(float(k + 1) / 2.0)), bias[k + 1]), edge);
                    gb_casc += edge;
                }
            }
            return s;
        }
    }
    return 1.0;
}
float shadowPcf(vec4 lpos) {
    if (ShadowParams.z < 0.5) return 1.0;
    float s = LightModel.w > 0.5 ? shadowCascaded(lpos) : shadowSingle(lpos);
    return mix(1.0, s, ShadowParams.w);
}

// ---------------------------------------------------------------- BRDF (GGX, height-correlated Smith, Schlick)
float dGgx(float noh, float a) { float a2 = a * a; float f = (noh * a2 - noh) * noh + 1.0; return a2 / (PI * f * f); }
float vSmithGgx(float nov, float nol, float a) {
    float a2 = a * a;
    float gv = nol * sqrt(nov * nov * (1.0 - a2) + a2);
    float gl = nov * sqrt(nol * nol * (1.0 - a2) + a2);
    return 0.5 / (gv + gl + 1e-5);
}
vec3 fSchlick(vec3 f0, float voh) { float f = pow(1.0 - voh, 5.0); return f0 + (vec3(1.0) - f0) * f; }

// ---------------------------------------------------------------- image-based lighting
vec3 shIrradiance(vec3 n) {
    return max(Sh0.rgb
        + Sh1.rgb * n.y + Sh2.rgb * n.z + Sh3.rgb * n.x
        + Sh4.rgb * (n.x * n.y) + Sh5.rgb * (n.y * n.z) + Sh6.rgb * (3.0 * n.z * n.z - 1.0)
        + Sh7.rgb * (n.x * n.z) + Sh8.rgb * (n.x * n.x - n.y * n.y), vec3(0.0));
}
vec3 iblSpecular(vec3 r, float rough) {
    float lod = rough * IblParams.x;
#ifdef GL_ARB_shader_texture_lod
    return texture2DLod(IblSpec, equirectUv(r), lod).rgb;
#else
    return texture2D(IblSpec, equirectUv(r), lod).rgb;
#endif
}
vec2 envBrdf(float nov, float rough) { return texture2D(BrdfLut, vec2(nov, rough)).rg; }

// Full PBR (metal/rough): sun (GGX specular + Lambert) plus IBL (SH diffuse, prefiltered specular, split-sum LUT).
// geoN: geometric normal for the reflection horizon fade (pass n when there is no normal map).
vec3 shadePbrFull(vec3 albedo, float rough, float metal, float ao, vec3 n, vec3 geoN, vec3 wpos, float shadow) {
    rough = clamp(rough, 0.045, 1.0);
    vec3 V = normalize(CameraPos.xyz - wpos);
    float nov = max(dot(n, V), 1e-4);
    vec3 L = SunDir.xyz; vec3 H = normalize(V + L);
    float nol = max(dot(n, L), 0.0), noh = max(dot(n, H), 0.0), voh = max(dot(V, H), 0.0);
    float a = rough * rough;
    vec3 f0 = mix(vec3(0.04), albedo, metal);
    vec3 dc = albedo * (1.0 - metal);
    vec3 F = fSchlick(f0, voh);
    vec3 direct = (dc / PI * (vec3(1.0) - F) + F * (dGgx(noh, a) * vSmithGgx(nov, nol, a))) * SunColor.rgb * (nol * shadow);
    vec3 irr; vec3 pre;
    vec3 R = reflect(-V, n);
    if (LightModel.y > 0.5) {
        irr = shIrradiance(n) * IblParams.y;
        pre = iblSpecular(R, rough) * IblParams.z;
    } else {
        irr = mix(AmbientGround.rgb, AmbientSky.rgb, n.y * 0.5 + 0.5) * PI;
        pre = mix(AmbientGround.rgb, AmbientSky.rgb, R.y * 0.5 + 0.5);
    }
    vec2 dfg = envBrdf(nov, rough);
    vec3 specCol = f0 * dfg.x + vec3(dfg.y);
    // no reflections from below the geometric surface (normal-mapped and rough terrain)
    float horizon = clamp(1.0 + 1.2 * dot(R, geoN), 0.0, 1.0); horizon *= horizon;
    // specular occlusion from AO (Lagarde and de Rousiers 2014)
    float so = clamp(pow(nov + ao, exp2(-16.0 * a - 1.0)) - 1.0 + ao, 0.0, 1.0);
    // where the sun is blocked, part of the sky usually is too: dim the sky light a little in shadow
    float skyVis = mix(0.85, 1.0, shadow);
    vec3 ambient = dc * irr / PI * ao * skyVis + pre * specCol * (horizon * so * skyVis);
    gbAmbient(ambient); // stage render2: screen-space AO darkens this share only
    return direct + ambient;
}
vec3 shadePbr(vec3 albedo, float rough, float metal, float ao, vec3 n, vec3 wpos, float shadow) {
    return shadePbrFull(albedo, rough, metal, ao, n, n, wpos, shadow);
}
// G1 signature. G1: Lambert sun + hemispheric ambient. G2 (LightModel.x = 1): PBR dielectric, roughness 0.75.
vec3 shadeLit(vec3 albedoLin, vec3 n, float shadow) {
    if (LightModel.x > 0.5) return shadePbr(albedoLin, 0.75, 0.0, 1.0, n, v_wpos, shadow);
    float ndl = max(dot(n, SunDir.xyz), 0.0);
    vec3 amb = mix(AmbientGround.rgb, AmbientSky.rgb, n.y * 0.5 + 0.5);
    return albedoLin * (amb + SunColor.rgb * (ndl * shadow));
}

// ---------------------------------------------------------------- atmosphere
// Aerial perspective for a homogeneous air layer at the camera's altitude: L = L0*T + J*(1-T), with the in-scatter
// source J(theta) = AerialR*P_R + AerialM*P_M + AerialMs (Rayleigh, Mie and multiple scattering of the key light),
// so haze is bluish by day and glows towards a low sun. Then the exponential height fog (G1's integral, density scaled
// by Aerial.w), lit by the key light (Henyey-Greenstein, g = FogSun.w) and the ambient light (FogAmb).
// extraFog (0..1) blends towards the sky table's horizon colour (used by the far field's outer edge).
float rayleighPhase(float c) { return 0.0596831 * (1.0 + c * c); }
float miePhaseCs(float c) { const float g = 0.8; float g2 = g * g; return 0.1193662 * ((1.0 - g2) * (1.0 + c * c)) / ((2.0 + g2) * pow(1.0 + g2 - 2.0 * g * c, 1.5)); }
float hgPhase(float c, float g) { float g2 = g * g; return 0.0795775 * (1.0 - g2) / pow(1.0 + g2 - 2.0 * g * c, 1.5); }
vec3 aerialPerspective(vec3 c, vec3 wpos, float extraFog) {
    vec3 c0 = c;
    vec3 d = wpos - CameraPos.xyz;
    float dist = max(length(d), 1e-3);
    vec3 dir = d / dist;
    float cosT = dot(dir, SunDir.xyz);
    vec3 T = exp(-Aerial.rgb * dist);
    vec3 J = AerialR.rgb * rayleighPhase(cosT) + AerialM.rgb * miePhaseCs(cosT) + AerialMs.rgb;
    c = c * T + J * (vec3(1.0) - T);
    float k = FogParams.y;
    float camH = CameraPos.y - FogParams.z;
    float dy = k * d.y;
    float line = abs(dy) > 0.0001 ? (1.0 - exp(-dy)) / dy : 1.0;
    float fogT = max(FogParams.x * Aerial.w * exp(-k * camH) * dist * line, 0.0);
    vec3 fogCol = FogSun.rgb * hgPhase(cosT, FogSun.w) + FogAmb.rgb;
    c = mix(fogCol, c, exp(-fogT));
    if (extraFog > 0.0) c = mix(c, skyView(normalize(vec3(dir.x, max(dir.y, 0.0), dir.z))), extraFog);
    gbSurface(c0, T * (exp(-fogT) * (1.0 - clamp(extraFog, 0.0, 1.0)))); // stage render2: surface radiance and its transmittance
    return c;
}
// G1 signature. G1: exponential height fog towards FogColor (analytic integral along the view ray).
vec3 applyFog(vec3 c, vec3 wpos) {
    if (LightModel.z > 0.5) return aerialPerspective(c, wpos, 0.0);
    vec3 d = wpos - CameraPos.xyz;
    float dist = length(d);
    float k = FogParams.y;
    float camH = CameraPos.y - FogParams.z;
    float dy = k * d.y;
    float line = abs(dy) > 0.0001 ? (1.0 - exp(-dy)) / dy : 1.0;
    float t = FogParams.x * exp(-k * camH) * dist * line;
    float f = min(1.0 - exp(-max(t, 0.0)), FogParams.w);
    gbSurface(c, vec3(1.0 - f));
    return mix(c, FogColor.rgb, f);
}";

    /// <summary>Linear HDR → display: exposure, ACES (Narkowicz 2015 fit), gamma 2.2. (G1 post.)</summary>
    public const string Tonemap = @"
vec3 acesFit(vec3 x) { return clamp((x * (2.51 * x + 0.03)) / (x * (2.43 * x + 0.59) + 0.14), 0.0, 1.0); }
vec3 linearToSrgb(vec3 c) { return pow(max(c, vec3(0.0)), vec3(1.0 / 2.2)); }";
}
