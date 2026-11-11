using Gfx.Engine.Effects;
using Gfx.Engine.Mgfx;
using Microsoft.Xna.Framework.Graphics;

namespace Gfx.Proc.Presentation;

/// <summary>
/// G2 terrain, far-field and water effects (stage render1; PRESENTATION ONLY). Registered by
/// <see cref="TerrainEffects"/>; the G1 effects stay as they were for the G1 frame.
/// <para><b>TerrainPbr</b>: the eight vertex splat weights map onto five photographic sets; per set an albedo+height and
/// a normal+roughness texture. Anti-tiling: two detail scales mixed by a macro mask (grass, gravel, sand, soil) and
/// hex-tiling for rock (Mikkelsen 2022, "Practical Real-Time Hex-Tiling": three randomly offset and rotated taps per
/// triangle-grid cell, weights sharpened and luminance-weighted; an original implementation from the paper), with
/// rock biplanar on steep faces. Height-blended transitions, macro brightness and hue variation, height-derived
/// micro occlusion; shaded with the engine's PBR + IBL, cascaded shadows and aerial perspective. Explicit-gradient
/// sampling (<c>texture2DGradARB</c>) keeps mip selection seam-free across hex cells; anisotropic filtering comes from
/// the engine's material sampler.</para>
/// </summary>
public static class G2TerrainEffects
{
    public const int AlbSlot = GlslLib.FirstMaterialSlot;          // 2..6
    public const int NrmSlot = GlslLib.FirstMaterialSlot + 5;      // 7..11
    public const int MacroSlot = GlslLib.FirstMaterialSlot + 10;   // 12
    /// <summary>Stage env: the wet-bank map uses slot 1 (bone slot; terrain has no bones).</summary>
    public const int WetSlot = GlslLib.BoneSlot;

    const string Sampling = @"
#ifdef GL_ARB_shader_texture_lod
#define TEXG(s, uv, dx, dy) texture2DGradARB(s, uv, dx, dy)
#else
#define TEXG(s, uv, dx, dy) texture2D(s, uv)
#endif
vec3 decodeSrgb(vec3 c) {
    vec3 lo = c / 12.92, hi = pow((c + 0.055) / 1.055, vec3(2.4));
    return vec3(c.r <= 0.04045 ? lo.r : hi.r, c.g <= 0.04045 ? lo.g : hi.g, c.b <= 0.04045 ? lo.b : hi.b);
}
vec3 decodeNormal(vec4 t) { vec2 xy = t.rg * 2.0 - 1.0; return vec3(xy, sqrt(max(1.0 - dot(xy, xy), 0.0))); }
vec2 rot2(vec2 v, float a) { float c = cos(a), s = sin(a); return vec2(c * v.x - s * v.y, s * v.x + c * v.y); }
vec2 hash2(vec2 p) { return fract(sin(vec2(dot(p, vec2(127.1, 311.7)), dot(p, vec2(269.5, 183.3)))) * 43758.5453); }
// Two detail scales (the second rotated, 4.4x larger) mixed by a macro mask: breaks the visible repeat.
void sampleTwoScale(sampler2D A, sampler2D N, vec2 uv, vec2 dx, vec2 dy, float mixFar, out vec4 a, out vec4 n) {
    a = TEXG(A, uv, dx, dy); n = TEXG(N, uv, dx, dy);
    if (mixFar > 0.02) {
        vec2 uv2 = rot2(uv, 0.61) * 0.227 + vec2(0.31, 0.77);
        vec2 dx2 = rot2(dx, 0.61) * 0.227, dy2 = rot2(dy, 0.61) * 0.227;
        vec4 a2 = TEXG(A, uv2, dx2, dy2);
        a = mix(a, a2, mixFar * 0.65);
    }
}
// Hex-tiling (triangle grid): three taps with per-cell random offset and rotation (|angle| <= maxRot).
void sampleHex(sampler2D A, sampler2D N, vec2 uv, vec2 dx, vec2 dy, float maxRot, out vec4 a, out vec4 n) {
    vec2 g = uv * 1.7320508;
    vec2 sk = vec2(g.x - 0.57735027 * g.y, 1.15470054 * g.y);
    vec2 base = floor(sk);
    vec3 t = vec3(fract(sk), 0.0); t.z = 1.0 - t.x - t.y;
    float s = step(0.0, -t.z), s2 = 2.0 * s - 1.0;
    vec3 w = vec3(-t.z * s2, s - t.y * s2, s - t.x * s2);
    vec2 v1 = base + vec2(s, s), v2 = base + vec2(s, 1.0 - s), v3 = base + vec2(1.0 - s, s);
    vec2 h1 = hash2(v1), h2 = hash2(v2), h3 = hash2(v3);
    float r1 = (h1.x * 2.0 - 1.0) * maxRot, r2 = (h2.x * 2.0 - 1.0) * maxRot, r3 = (h3.x * 2.0 - 1.0) * maxRot;
    vec2 u1 = rot2(uv, r1) + h1, u2 = rot2(uv, r2) + h2, u3 = rot2(uv, r3) + h3;
    // sharpened weights; a tap under 3 % is skipped (most pixels need one or two taps, not three)
    vec3 pw = pow(w, vec3(7.0)); pw /= dot(pw, vec3(1.0));
    vec4 a1 = vec4(0.0), a2 = vec4(0.0), a3 = vec4(0.0);
    vec4 n1 = vec4(0.5, 0.5, 1.0, 1.0), n2 = n1, n3 = n1;
    if (pw.x > 0.03) { a1 = TEXG(A, u1, rot2(dx, r1), rot2(dy, r1)); n1 = TEXG(N, u1, rot2(dx, r1), rot2(dy, r1)); } else pw.x = 0.0;
    if (pw.y > 0.03) { a2 = TEXG(A, u2, rot2(dx, r2), rot2(dy, r2)); n2 = TEXG(N, u2, rot2(dx, r2), rot2(dy, r2)); } else pw.y = 0.0;
    if (pw.z > 0.03) { a3 = TEXG(A, u3, rot2(dx, r3), rot2(dy, r3)); n3 = TEXG(N, u3, rot2(dx, r3), rot2(dy, r3)); } else pw.z = 0.0;
    // favour the brighter (higher) tap: keeps contrast where taps blend
    vec3 lw = vec3(dot(a1.rgb, vec3(0.3333)), dot(a2.rgb, vec3(0.3333)), dot(a3.rgb, vec3(0.3333)));
    vec3 ww = pw * mix(vec3(1.0), 1.0 + 2.0 * lw, 0.6);
    ww /= max(dot(ww, vec3(1.0)), 1e-5);
    a = a1 * ww.x + a2 * ww.y + a3 * ww.z;
    // rotate each tap's tangent normal back into the base frame before blending
    vec3 m1 = decodeNormal(n1), m2 = decodeNormal(n2), m3 = decodeNormal(n3);
    vec2 bxy = rot2(m1.xy, -r1) * ww.x + rot2(m2.xy, -r2) * ww.y + rot2(m3.xy, -r3) * ww.z;
    n = vec4(bxy * 0.5 + 0.5, n1.b * ww.x + n2.b * ww.y + n3.b * ww.z, 1.0);
}
vec3 saturateCol(vec3 c, float s) { float l = dot(c, vec3(0.2126, 0.7152, 0.0722)); return max(vec3(l) + (c - vec3(l)) * s, vec3(0.0)); }";

    public static EffectBuilder TerrainPbr()
    {
        var b = new EffectBuilder(TerrainEffectNames.TerrainPbr)
            .Attribute("vec4 a_pos", VertexElementUsage.Position, 0)
            .Attribute("vec3 a_nrm", VertexElementUsage.Normal, 0)
            .Attribute("vec4 a_wa", VertexElementUsage.TextureCoordinate, 0)
            .Attribute("vec4 a_wb", VertexElementUsage.TextureCoordinate, 1)
            .AddStandardLit().VertexLib(GlslLib.ShadowLookup);
        for (int i = 0; i < 8; i++) b.Vec4("MatTint" + i, Stage.Pixel);
        b.Vec4("MatRoughA", Stage.Pixel).Vec4("MatRoughB", Stage.Pixel)
         .Vec4("TileScaleA", Stage.Pixel).Vec4("TileScaleB", Stage.Pixel).Vec4("TerrainOpts", Stage.Pixel);
        for (int i = 0; i < 5; i++) b.Texture("Alb" + i, AlbSlot + i);
        for (int i = 0; i < 5; i++) b.Texture("Nrm" + i, NrmSlot + i);
        // stage env: wet banks (TerrainOpts.w = 1): WetMap holds the nearby water surface height per grid sample
        b.Vec4("WetParams", Stage.Pixel).Texture("WetMap", WetSlot);
        return b.Texture("Macro", MacroSlot)
            .Varyings("vec3 v_wpos; vec3 v_nrm; vec4 v_wa; vec4 v_wb; vec4 v_lpos;")
            .VertexMain(@"
    vec3 wp = a_pos.xyz;
    v_wpos = wp; v_nrm = a_nrm; v_wa = a_wa; v_wb = a_wb;
    v_lpos = shadowLookup(wp, a_nrm);
    gl_Position = ViewProj_mul(vec4(wp, 1.0));")
            .PixelLib(Sampling)
            .PixelMain(@"
    vec3 N = normalize(v_nrm);
    vec3 p = v_wpos;
    vec3 dpx = dFdx(p), dpy = dFdy(p);
    // macro variation (two scales of the procedural macro texture)
    vec4 m1 = texture2D(Macro, p.xz * TileScaleB.y);
    vec4 m2 = texture2D(Macro, p.xz * TileScaleB.z + vec2(0.37, 0.71));
    float macroOn = TerrainOpts.y;
    float mixFar = macroOn > 0.5 ? m2.a : 0.0;
    // material weights -> set weights; per-set tint, saturation and roughness = weighted mix of its materials
    float wG = v_wa.x, wD = v_wa.y, wR = v_wa.z, wS = v_wa.w, wSn = v_wb.x, wF = v_wb.y, wM = v_wb.z, wDg = v_wb.w;
    float sw0 = wG + wDg, sw1 = wD, sw2 = wR, sw3 = wS + wSn, sw4 = wF + wM;
    vec4 t0 = (MatTint0 * wG + MatTint7 * wDg) / max(sw0, 1e-4);
    vec4 t3 = (MatTint3 * wS + MatTint4 * wSn) / max(sw3, 1e-4);
    vec4 t4 = (MatTint5 * wF + MatTint6 * wM) / max(sw4, 1e-4);
    float r0 = (MatRoughA.x * wG + MatRoughB.w * wDg) / max(sw0, 1e-4);
    float r3 = (MatRoughA.w * wS + MatRoughB.x * wSn) / max(sw3, 1e-4);
    float r4 = (MatRoughB.y * wF + MatRoughB.z * wM) / max(sw4, 1e-4);
    // sample the sets that matter
    vec4 a0 = vec4(0.0), a1 = vec4(0.0), a2 = vec4(0.0), a3 = vec4(0.0), a4 = vec4(0.0);
    vec4 n0 = vec4(0.5, 0.5, 1.0, 1.0), n1 = n0, n2 = n0, n3 = n0, n4 = n0;
    vec3 nRockW = N;
    bool hex = TerrainOpts.x > 0.5;
    // isotropic ground sets: hex-tiled with free rotation (or two detail scales when hex-tiling is off)
    if (sw0 > 0.004) { vec2 u = p.xz * TileScaleA.x; vec2 ex = dpx.xz * TileScaleA.x, ey = dpy.xz * TileScaleA.x;
        if (hex) sampleHex(Alb0, Nrm0, u, ex, ey, 3.14159, a0, n0); else sampleTwoScale(Alb0, Nrm0, u, ex, ey, mixFar, a0, n0); }
    if (sw1 > 0.004) { vec2 u = p.xz * TileScaleA.y; vec2 ex = dpx.xz * TileScaleA.y, ey = dpy.xz * TileScaleA.y;
        if (hex) sampleHex(Alb1, Nrm1, u, ex, ey, 3.14159, a1, n1); else sampleTwoScale(Alb1, Nrm1, u, ex, ey, mixFar, a1, n1); }
    if (sw3 > 0.004) { vec2 u = p.xz * TileScaleA.w; vec2 ex = dpx.xz * TileScaleA.w, ey = dpy.xz * TileScaleA.w;
        if (hex) sampleHex(Alb3, Nrm3, u, ex, ey, 3.14159, a3, n3); else sampleTwoScale(Alb3, Nrm3, u, ex, ey, mixFar, a3, n3); }
    if (sw4 > 0.004) { vec2 u = p.xz * TileScaleB.x; vec2 ex = dpx.xz * TileScaleB.x, ey = dpy.xz * TileScaleB.x;
        if (hex) sampleHex(Alb4, Nrm4, u, ex, ey, 3.14159, a4, n4); else sampleTwoScale(Alb4, Nrm4, u, ex, ey, mixFar, a4, n4); }
    if (sw2 > 0.004) {
        // rock: biplanar (top + dominant side), hex-tiled when enabled
        float sc = TileScaleA.z;
        float wTop = smoothstep(0.55, 0.85, N.y);
        vec4 at = vec4(0.0), nt = n0, as = vec4(0.0), ns = n0;
        vec3 nTopW = N, nSideW = N;
        if (wTop > 0.01) {
            vec2 uv = p.xz * sc, dx = dpx.xz * sc, dy = dpy.xz * sc;
            if (hex) sampleHex(Alb2, Nrm2, uv, dx, dy, 3.14159, at, nt);
            else { at = TEXG(Alb2, uv, dx, dy); nt = TEXG(Nrm2, uv, dx, dy); }
            vec3 T = normalize(vec3(1.0, 0.0, 0.0) - N * N.x), B = cross(N, T);
            vec3 m = decodeNormal(nt);
            nTopW = normalize(T * m.x + B * m.y + N * m.z);
        }
        if (wTop < 0.99) {
            bool xs = abs(N.x) > abs(N.z);
            vec2 uv = (xs ? vec2(p.z, -p.y) : vec2(p.x, -p.y)) * sc;
            vec2 dx = (xs ? vec2(dpx.z, -dpx.y) : vec2(dpx.x, -dpx.y)) * sc, dy = (xs ? vec2(dpy.z, -dpy.y) : vec2(dpy.x, -dpy.y)) * sc;
            if (hex) sampleHex(Alb2, Nrm2, uv, dx, dy, 0.22, as, ns);
            else { as = TEXG(Alb2, uv, dx, dy); ns = TEXG(Nrm2, uv, dx, dy); }
            vec3 T = xs ? vec3(0.0, 0.0, 1.0) : vec3(1.0, 0.0, 0.0), B = vec3(0.0, 1.0, 0.0);
            vec3 m = decodeNormal(ns);
            nSideW = normalize(N + T * m.x + B * m.y);
        }
        a2 = at * wTop + as * (1.0 - wTop);
        n2 = vec4(0.5, 0.5, nt.b * wTop + ns.b * (1.0 - wTop), 1.0);
        nRockW = normalize(nTopW * wTop + nSideW * (1.0 - wTop));
    }
    // height-blended set weights
    float depth = TerrainOpts.z;
    vec4 hw = vec4(a0.a + sw0, a1.a + sw1, a2.a + sw2, a3.a + sw3);
    float hw4 = a4.a + sw4;
    float mx = max(max(max(hw.x, hw.y), max(hw.z, hw.w)), hw4);
    vec4 bw = max(hw - vec4(mx - depth), vec4(0.0)) * vec4(step(0.004, sw0), step(0.004, sw1), step(0.004, sw2), step(0.004, sw3));
    float bw4 = max(hw4 - (mx - depth), 0.0) * step(0.004, sw4);
    float bs = dot(bw, vec4(1.0)) + bw4 + 1e-5;
    bw /= bs; bw4 /= bs;
    // albedo (linear), tinted per material
    vec3 alb = saturateCol(decodeSrgb(a0.rgb), t0.a) * t0.rgb * bw.x
             + saturateCol(decodeSrgb(a1.rgb), MatTint1.a) * MatTint1.rgb * bw.y
             + saturateCol(decodeSrgb(a2.rgb), MatTint2.a) * MatTint2.rgb * bw.z
             + saturateCol(decodeSrgb(a3.rgb), t3.a) * t3.rgb * bw.w
             + saturateCol(decodeSrgb(a4.rgb), t4.a) * t4.rgb * bw4;
    float rough = n0.b * r0 * bw.x + n1.b * MatRoughA.y * bw.y + n2.b * MatRoughA.z * bw.z + n3.b * r3 * bw.w + n4.b * r4 * bw4;
    float h = a0.a * bw.x + a1.a * bw.y + a2.a * bw.z + a3.a * bw.w + a4.a * bw4;
    // normals: tangent-space sets in the top frame, rock already in world space
    vec3 T = normalize(vec3(1.0, 0.0, 0.0) - N * N.x), B = cross(N, T);
    vec3 tn = decodeNormal(n0) * bw.x + decodeNormal(n1) * bw.y + decodeNormal(n3) * bw.w + decodeNormal(n4) * bw4;
    vec3 n = T * tn.x + B * tn.y + N * tn.z;
    n = normalize(n * (1.0 - bw.z) + nRockW * bw.z);
    if (macroOn > 0.5) {
        alb *= mix(0.86, 1.14, m1.r * 0.65 + m2.r * 0.35);
        float grassy = bw.x;
        alb = mix(alb, alb * vec3(1.12, 1.03, 0.80), clamp(m1.b * 0.7 + m2.g * 0.3, 0.0, 1.0) * grassy * 0.45);
    }
    if (TerrainOpts.w > 0.5) {
        // wet banks: darker and glossier up to about 0.6 m above the nearby water surface, fading with height
        float hw = texture2D(WetMap, vec2(p.x * WetParams.x + WetParams.y, p.z * WetParams.z + WetParams.w)).r;
        float above = p.y - hw;
        float wet = (1.0 - smoothstep(0.0, 0.6, above)) * step(-50.0, above);
        // under water: silt evens out the bed (lower contrast)
        float sub = (1.0 - smoothstep(-0.05, 0.0, above)) * step(-50.0, above);
        alb = mix(alb, vec3(dot(alb, vec3(0.3333))) * vec3(1.0, 0.98, 0.9), sub * 0.55);
        alb *= mix(1.0, 0.55, wet);
        rough = mix(rough, 0.28, wet * 0.85);
    }
    float ao = mix(0.62, 1.0, smoothstep(0.0, 0.55, h));
    vec3 c = shadePbrFull(alb, clamp(rough, 0.05, 1.0), 0.0, ao, n, N, p, shadowPcf(v_lpos));
    gl_FragColor = vec4(applyFog(c, p), 1.0);");
    }

    /// <summary>
    /// Water with ripple octaves that fade out once a wavelength spans only a few pixels (no moiré at distance; the
    /// filtered-out slope variance widens the sun glint instead), sky-table reflections, depth absorption.
    /// WaterParams: x = time s, y = ripple fade on, z = opaque flag, w = 1: vertex fade in Data.y (far sea). WaterAbsorb: rgb absorption /m.
    /// </summary>
    public static EffectBuilder WaterPbr() => new EffectBuilder(TerrainEffectNames.WaterPbr).BlendedGBuffer()
        .Attribute("vec4 a_pos", VertexElementUsage.Position, 0)
        .Attribute("vec4 a_dat", VertexElementUsage.TextureCoordinate, 0)
        .AddStandardLit().VertexLib(GlslLib.ShadowLookup)
        .Vec4("WaterParams", Stage.Pixel).Vec4("WaterScatter", Stage.Pixel).Vec4("WaterAbsorb", Stage.Pixel)
        .Varyings("vec3 v_wpos; float v_depth; float v_fade; vec4 v_lpos;")
        .VertexMain(@"
    vec3 wp = a_pos.xyz;
    v_wpos = wp; v_depth = a_dat.z > 0.0 ? a_dat.z : a_dat.x; v_fade = a_dat.y; // z: in-map depth (render1 fix), else x
    v_lpos = shadowLookup(wp, vec3(0.0, 1.0, 0.0));
    gl_Position = ViewProj_mul(vec4(wp, 1.0));")
        .PixelLib(@"
vec2 wave(vec2 p, vec2 dir, float lambda, float slope, float t, float fp, inout float lost) {
    float k = 6.2831853 / lambda;
    float w = sqrt(9.81 * k);
    float fade = WaterParams.y > 0.5 ? 1.0 - smoothstep(0.08, 0.25, fp / lambda) : 1.0;
    lost += slope * slope * (1.0 - fade);
    return dir * (slope * fade * cos(k * dot(dir, p) - w * t));
}")
        .PixelMain(@"
    float t = WaterParams.x; vec3 p = v_wpos;
    vec3 toCam = CameraPos.xyz - p; float dist = length(toCam);
    vec3 V = toCam / max(dist, 1e-3);
    float fp = max(length(dFdx(p.xz)), length(dFdy(p.xz)));
    float lost = 0.0;
    vec2 g = vec2(0.0);
    g += wave(p.xz, normalize(vec2(0.8, 0.6)), 7.3, 0.050, t, fp, lost);
    g += wave(p.xz, normalize(vec2(-0.4, 0.9)), 3.9, 0.045, t, fp, lost);
    g += wave(p.xz, normalize(vec2(0.95, -0.3)), 2.1, 0.040, t, fp, lost);
    g += wave(p.xz, normalize(vec2(-0.7, -0.7)), 1.1, 0.034, t, fp, lost);
    g += wave(p.xz, normalize(vec2(0.2, -0.98)), 0.55, 0.028, t, fp, lost);
    vec3 n = normalize(vec3(-g.x, 1.0, -g.y));
    float nov = max(dot(n, V), 1e-3);
    float F = 0.02 + 0.98 * pow(1.0 - nov, 5.0);
    vec3 R = reflect(-V, n); R.y = abs(R.y);
    vec3 refl = LightModel.z > 0.5 ? skyView(R) : mix(AmbientGround.rgb, AmbientSky.rgb, R.y * 0.5 + 0.5);
    float sh = shadowPcf(v_lpos);
    // sun glint: GGX, roughened by the ripple slope variance that the distance filter removed
    float rough = clamp(sqrt(0.0025 + lost * 2.0), 0.05, 0.6); float a = rough * rough;
    vec3 L = SunDir.xyz; vec3 H = normalize(V + L);
    float nol = max(dot(n, L), 0.0), noh = max(dot(n, H), 0.0);
    vec3 spec = vec3(dGgx(noh, a) * vSmithGgx(nov, nol, a) * F) * SunColor.rgb * (nol * sh);
    // body: light scattered back out of the water column, absorbed per channel with depth
    vec3 absorb = exp(-WaterAbsorb.rgb * max(v_depth, 0.05) * 2.0);
    vec3 irr = shIrradiance(vec3(0.0, 1.0, 0.0)) * (LightModel.y > 0.5 ? 1.0 : 0.0) + AmbientSky.rgb * PI * (LightModel.y > 0.5 ? 0.0 : 1.0);
    vec3 body = WaterScatter.rgb * (irr / PI + SunColor.rgb * max(SunDir.y, 0.0) * sh / PI) * (1.0 - absorb * 0.6);
    vec3 c = mix(body, refl, F) + spec;
    float alpha = WaterParams.z > 0.5 ? 1.0 : clamp(1.0 - absorb.g * (1.0 - F), 0.25, 0.97);
    if (WaterParams.w > 0.5) alpha *= v_fade; // far sea: fades in across the map edge
    gl_FragColor = vec4(applyFog(c, p), alpha);");

    /// <summary>
    /// The land and sea beyond the battle map: vertex colour (sRGB) and normal, macro brightness variation, rough PBR,
    /// cascaded shadows where they reach, aerial perspective, and a fade into the horizon haze at the outer edge.
    /// FarParams: x = fade start radius, y = fade end radius (m from the map centre).
    /// </summary>
    public static EffectBuilder FarField() => new EffectBuilder(TerrainEffectNames.FarField)
        .Attribute("vec4 a_pos", VertexElementUsage.Position, 0)
        .Attribute("vec3 a_nrm", VertexElementUsage.Normal, 0)
        .Attribute("vec4 a_col", VertexElementUsage.Color, 0)
        .AddStandardLit().VertexLib(GlslLib.ShadowLookup)
        .Vec4("FarParams", Stage.Pixel)
        .Texture("Macro", MacroSlot)
        .Varyings("vec3 v_wpos; vec3 v_nrm; vec4 v_col; vec4 v_lpos;")
        .VertexMain(@"
    vec3 wp = a_pos.xyz;
    v_wpos = wp; v_nrm = a_nrm; v_col = a_col;
    v_lpos = shadowLookup(wp, a_nrm);
    gl_Position = ViewProj_mul(vec4(wp, 1.0));")
        .PixelMain(@"
    vec3 n = normalize(v_nrm);
    vec3 alb = srgbToLinear(v_col.rgb);
    vec4 m1 = texture2D(Macro, v_wpos.xz / 1400.0), m2 = texture2D(Macro, v_wpos.xz / 310.0 + vec2(0.3, 0.6));
    alb *= mix(0.82, 1.16, m1.r * 0.6 + m2.r * 0.4);
    vec3 c = shadePbr(alb, 0.93, 0.0, 1.0, n, v_wpos, shadowPcf(v_lpos));
    float edge = smoothstep(FarParams.x, FarParams.y, length(v_wpos.xz));
    gl_FragColor = vec4(LightModel.z > 0.5 ? aerialPerspective(c, v_wpos, edge) : mix(applyFog(c, v_wpos), FogColor.rgb, edge), 1.0);");
}
