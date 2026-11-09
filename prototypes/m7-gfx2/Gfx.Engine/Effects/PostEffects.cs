using Gfx.Engine.Mgfx;
using Microsoft.Xna.Framework.Graphics;

namespace Gfx.Engine.Effects;

/// <summary>Effect names of the G2 frame (stage render1).</summary>
public static class G2EffectNames
{
    public const string SkyAnalytic = "SkyAnalytic";
    public const string BloomDown = "PostBloomDown";
    public const string BloomUp = "PostBloomUp";
    public const string Exposure = "PostExposure";
    public const string Composite = "PostComposite";
    public const string Fxaa = "PostFxaa";
    public const string Copy = "PostCopy";
}

/// <summary>
/// G2 sky and post-processing effects (stage render1). All full-screen passes use the engine's one-triangle
/// <c>FullScreen.Triangle</c> and the G1 uv convention (<c>v_uv = (x·0.5+0.5, −y·0.5+0.5)</c>).
/// <para>Tonemappers: PBR Neutral (Khronos), ACES (Stephen Hill's RRT+ODT fit) and AgX (Sobotka; minimal fit by
/// Wrensch), ported to GLSL 1.20 from three.js r180 <c>tonemapping_pars_fragment.glsl.js</c> (MIT, pinned as
/// <c>threejs-ref</c> in assets/manifest.tsv); the G1 ACES fit (Narkowicz) stays. FXAA: an original implementation
/// of the published FXAA idea (luma edge test, edge walk, sub-pixel blend).</para>
/// </summary>
public sealed class PostEffects : IEffectSource
{
    public IEnumerable<EffectBuilder> CreateEffects()
    {
        yield return SkyAnalytic();
        yield return BloomDown();
        yield return BloomUp();
        yield return Exposure();
        yield return Composite();
        yield return Fxaa();
        yield return Copy();
    }

    static EffectBuilder FullScreen(string name) => new EffectBuilder(name)
        .Attribute("vec4 a_pos", VertexElementUsage.Position, 0)
        .Varyings("vec2 v_uv;")
        .VertexMain(@"
    v_uv = vec2(a_pos.x * 0.5 + 0.5, -a_pos.y * 0.5 + 0.5);
    gl_Position = vec4(a_pos.xy, 0.0, 1.0);");

    /// <summary>
    /// Analytic sky: the sky-view table (Rayleigh + Mie + ozone, multiple scattering) plus a limb-darkened sun disc;
    /// optionally an HDRI backdrop instead. Below the horizon it holds the horizon colour (the far field covers it).
    /// </summary>
    static EffectBuilder SkyAnalytic() => new EffectBuilder(G2EffectNames.SkyAnalytic)
        .Attribute("vec4 a_pos", VertexElementUsage.Position, 0)
        .Matrix("InvViewProj", Stage.Vertex).Vec4("CameraPos", Stage.Vertex)
        .Vec4("SunDir", Stage.Pixel).Vec4("SunDisc", Stage.Pixel).Vec4("SunDiscColor", Stage.Pixel).Vec4("SkyMode", Stage.Pixel)
        .AddSkyView()
        .Texture("Backdrop", GlslLib.FirstMaterialSlot)
        .Varyings("vec3 v_dir;")
        .VertexMain(@"
    vec4 f = InvViewProj_mul(vec4(a_pos.xy, 1.0, 1.0));
    v_dir = f.xyz / f.w - CameraPos.xyz;
    gl_Position = vec4(a_pos.xy, 0.0, 1.0);")
        .PixelMain(@"
    vec3 d = normalize(v_dir);
    vec3 c;
    if (SkyMode.x > 0.5) c = texture2D(Backdrop, equirectUv(d)).rgb;
    else c = skyView(normalize(vec3(d.x, max(d.y, 0.0), d.z)));
    // sun disc: SunDisc.x = cos(angular radius), y = 1 - SunDisc.x
    float cd = dot(d, SunDir.xyz);
    if (cd > SunDisc.x && d.y > -0.01) {
        float r = clamp((1.0 - cd) / SunDisc.y, 0.0, 1.0);          // 0 centre .. 1 limb (in 1-cos units)
        float mu = sqrt(1.0 - r);
        float limb = 1.0 - 0.6 * (1.0 - mu);                       // linear limb darkening
        c += SunDiscColor.rgb * limb * smoothstep(1.0, 0.85, r);
    }
    gl_FragColor = vec4(c, 1.0);");

    /// <summary>13-tap downsample (Jimenez 2014). BloomParams.x = 1: Karis-weighted (first level, kills fireflies).</summary>
    static EffectBuilder BloomDown() => FullScreen(G2EffectNames.BloomDown)
        .Vec4("SrcTexel", Stage.Pixel).Vec4("BloomParams", Stage.Pixel)
        .Texture("Src", 0)
        .PixelLib(@"
vec3 karis(vec3 c, inout float wsum, float w) {
    float k = BloomParams.x > 0.5 ? 1.0 / (1.0 + dot(c, vec3(0.2126, 0.7152, 0.0722))) : 1.0;
    wsum += w * k; return c * (w * k);
}
vec3 tap(vec2 o) { return min(texture2D(Src, v_uv + o * SrcTexel.xy).rgb, vec3(60000.0)); }")
        .PixelMain(@"
    float ws = 0.0; vec3 s = vec3(0.0);
    s += karis(tap(vec2(0.0, 0.0)), ws, 0.125);
    s += karis(tap(vec2(-2.0, -2.0)), ws, 0.03125); s += karis(tap(vec2(2.0, -2.0)), ws, 0.03125);
    s += karis(tap(vec2(-2.0, 2.0)), ws, 0.03125);  s += karis(tap(vec2(2.0, 2.0)), ws, 0.03125);
    s += karis(tap(vec2(0.0, -2.0)), ws, 0.0625);   s += karis(tap(vec2(0.0, 2.0)), ws, 0.0625);
    s += karis(tap(vec2(-2.0, 0.0)), ws, 0.0625);   s += karis(tap(vec2(2.0, 0.0)), ws, 0.0625);
    s += karis(tap(vec2(-1.0, -1.0)), ws, 0.125);   s += karis(tap(vec2(1.0, -1.0)), ws, 0.125);
    s += karis(tap(vec2(-1.0, 1.0)), ws, 0.125);    s += karis(tap(vec2(1.0, 1.0)), ws, 0.125);
    gl_FragColor = vec4(s / ws, 1.0);");

    /// <summary>9-tap tent upsample of the lower level, added (blend) onto the current level.</summary>
    static EffectBuilder BloomUp() => FullScreen(G2EffectNames.BloomUp)
        .Vec4("SrcTexel", Stage.Pixel).Vec4("BloomParams", Stage.Pixel)
        .Texture("Src", 0)
        .PixelMain(@"
    vec2 o = SrcTexel.xy * BloomParams.y;
    vec3 s = texture2D(Src, v_uv).rgb * 4.0;
    s += (texture2D(Src, v_uv + vec2(-o.x, 0.0)).rgb + texture2D(Src, v_uv + vec2(o.x, 0.0)).rgb
        + texture2D(Src, v_uv + vec2(0.0, -o.y)).rgb + texture2D(Src, v_uv + vec2(0.0, o.y)).rgb) * 2.0;
    s += texture2D(Src, v_uv + vec2(-o.x, -o.y)).rgb + texture2D(Src, v_uv + vec2(o.x, -o.y)).rgb
       + texture2D(Src, v_uv + vec2(-o.x, o.y)).rgb + texture2D(Src, v_uv + vec2(o.x, o.y)).rgb;
    gl_FragColor = vec4(s / 16.0, 1.0);");

    /// <summary>
    /// Metering + temporal adaptation into a 1×1 target: centre-weighted mean of log2 luminance over a 16×9 grid of a
    /// small bloom level, partial adaptation (target = a·mean + (1−a)·log2(reference)), then exponential smoothing
    /// towards it (separate rates for brightening and darkening). Output r = adapted log2 luminance, g = target.
    /// ExposureParams: x = blend factor when brightening, y = blend factor when darkening, z = a, w = log2(reference).
    /// ExposureRange: x = min log2, y = max log2, z = 1 when a previous value exists.
    /// </summary>
    static EffectBuilder Exposure() => FullScreen(G2EffectNames.Exposure)
        .Vec4("ExposureParams", Stage.Pixel).Vec4("ExposureRange", Stage.Pixel)
        .Texture("Src", 0).Texture("Prev", 1)
        .PixelMain(@"
    float sum = 0.0, wsum = 0.0;
    for (int j = 0; j < 9; j++) for (int i = 0; i < 16; i++) {
        vec2 uv = vec2((float(i) + 0.5) / 16.0, (float(j) + 0.5) / 9.0);
        float l = dot(texture2D(Src, uv).rgb, vec3(0.2126, 0.7152, 0.0722));
        vec2 dc = (uv - 0.5) * vec2(2.0, 2.0);
        float w = 1.0 + 2.0 * clamp(1.0 - dot(dc, dc), 0.0, 1.0);   // centre-weighted
        sum += w * log2(clamp(l, 1e-5, 1e4)); wsum += w;
    }
    float mean = sum / wsum;
    float target = clamp(ExposureParams.z * mean + (1.0 - ExposureParams.z) * ExposureParams.w, ExposureRange.x, ExposureRange.y);
    float prev = texture2D(Prev, vec2(0.5, 0.5)).r;
    float adapted = target;
    if (ExposureRange.z > 0.5) adapted = prev + (target - prev) * (target > prev ? ExposureParams.x : ExposureParams.y);
    gl_FragColor = vec4(adapted, target, mean, 1.0);");

    const string Tonemappers = @"
vec3 acesFit(vec3 x) { return clamp((x * (2.51 * x + 0.03)) / (x * (2.43 * x + 0.59) + 0.14), 0.0, 1.0); }
// --- ported from three.js r180 tonemapping_pars_fragment.glsl.js (MIT) ---
vec3 rrtAndOdtFit(vec3 v) {
    vec3 a = v * (v + 0.0245786) - 0.000090537;
    vec3 b = v * (0.983729 * v + 0.4329510) + 0.238081;
    return a / b;
}
vec3 acesHill(vec3 color) {
    mat3 inM = mat3(vec3(0.59719, 0.07600, 0.02840), vec3(0.35458, 0.90834, 0.13383), vec3(0.04823, 0.01566, 0.83777));
    mat3 outM = mat3(vec3(1.60475, -0.10208, -0.00327), vec3(-0.53108, 1.10813, -0.07276), vec3(-0.07367, -0.00605, 1.07602));
    color = inM * (color / 0.6);
    color = rrtAndOdtFit(color);
    return clamp(outM * color, 0.0, 1.0);
}
vec3 agxContrast(vec3 x) {
    vec3 x2 = x * x; vec3 x4 = x2 * x2;
    return 15.5 * x4 * x2 - 40.14 * x4 * x + 31.96 * x4 - 6.868 * x2 * x + 0.4298 * x2 + 0.1191 * x - 0.00232;
}
vec3 agx(vec3 color) {
    mat3 toRec2020 = mat3(vec3(0.6274, 0.0691, 0.0164), vec3(0.3293, 0.9195, 0.0880), vec3(0.0433, 0.0113, 0.8956));
    mat3 fromRec2020 = mat3(vec3(1.6605, -0.1246, -0.0182), vec3(-0.5876, 1.1329, -0.1006), vec3(-0.0728, -0.0083, 1.1187));
    mat3 inset = mat3(vec3(0.856627153315983, 0.137318972929847, 0.11189821299995),
                      vec3(0.0951212405381588, 0.761241990602591, 0.0767994186031903),
                      vec3(0.0482516061458583, 0.101439036467562, 0.811302368396859));
    mat3 outset = mat3(vec3(1.1271005818144368, -0.1413297634984383, -0.14132976349843826),
                       vec3(-0.11060664309660323, 1.157823702216272, -0.11060664309660294),
                       vec3(-0.016493938717834573, -0.016493938717834257, 1.2519364065950405));
    const float minEv = -12.47393, maxEv = 4.026069;
    color = inset * (toRec2020 * color);
    color = clamp((log2(max(color, vec3(1e-10))) - minEv) / (maxEv - minEv), 0.0, 1.0);
    color = agxContrast(color);
    color = outset * color;
    color = pow(max(vec3(0.0), color), vec3(2.2));
    return clamp(fromRec2020 * color, 0.0, 1.0);
}
vec3 pbrNeutral(vec3 color) {
    const float startCompression = 0.8 - 0.04;
    const float desaturation = 0.15;
    float x = min(color.r, min(color.g, color.b));
    float offset = x < 0.08 ? x - 6.25 * x * x : 0.04;
    color -= offset;
    float peak = max(color.r, max(color.g, color.b));
    if (peak < startCompression) return color;
    float d = 1.0 - startCompression;
    float newPeak = 1.0 - d * d / (peak + d - startCompression);
    color *= newPeak / peak;
    float g = 1.0 - 1.0 / (desaturation * (peak - newPeak) + 1.0);
    return mix(color, vec3(newPeak), g);
}
// --- end of the three.js port ---
vec3 linearToSrgbExact(vec3 c) {
    c = clamp(c, 0.0, 1.0);
    vec3 lo = c * 12.92, hi = 1.055 * pow(c, vec3(1.0 / 2.4)) - 0.055;
    return vec3(c.r <= 0.0031308 ? lo.r : hi.r, c.g <= 0.0031308 ? lo.g : hi.g, c.b <= 0.0031308 ? lo.b : hi.b);
}
vec3 srgbToLinearExact(vec3 c) {
    vec3 lo = c / 12.92, hi = pow((c + 0.055) / 1.055, vec3(2.4));
    return vec3(c.r <= 0.04045 ? lo.r : hi.r, c.g <= 0.04045 ? lo.g : hi.g, c.b <= 0.04045 ? lo.b : hi.b);
}";

    /// <summary>
    /// Exposure → (bloom lerp) → tonemap → 3D LUT grade → output. CompositeParams: x manual exposure, y auto exposure
    /// on, z bloom strength, w tonemapper (0 ACES fit, 1 PBR Neutral, 2 ACES Hill, 3 AgX, 4 linear).
    /// CompositeParams2: x grade on, y LUT size, z output mode (0 = sRGB + luma in alpha, 1 = linear display × weight),
    /// w = exposure multiplier (2^EV × scene compensation). ExposureTex holds the adapted log2 luminance.
    /// </summary>
    static EffectBuilder Composite() => FullScreen(G2EffectNames.Composite)
        .Vec4("CompositeParams", Stage.Pixel).Vec4("CompositeParams2", Stage.Pixel).Vec4("AccumWeight", Stage.Pixel)
        .Texture("SceneTex", 0).Texture("BloomTex", 1).Texture("ExposureTex", 2).Texture("LutTex", 3)
        .PixelLib(Tonemappers + @"
vec3 applyLut(vec3 c) {
    float n = CompositeParams2.y;
    float b = clamp(c.b, 0.0, 1.0) * (n - 1.0);
    float b0 = floor(b); float b1 = min(b0 + 1.0, n - 1.0); float t = b - b0;
    vec2 uv = vec2((clamp(c.r, 0.0, 1.0) * (n - 1.0) + 0.5) / (n * n), (clamp(c.g, 0.0, 1.0) * (n - 1.0) + 0.5) / n);
    vec3 s0 = texture2D(LutTex, uv + vec2(b0 / n, 0.0)).rgb;
    vec3 s1 = texture2D(LutTex, uv + vec2(b1 / n, 0.0)).rgb;
    return mix(s0, s1, t);
}")
        .PixelMain(@"
    vec3 c = texture2D(SceneTex, v_uv).rgb;
    if (CompositeParams.z > 0.0) c = mix(c, texture2D(BloomTex, v_uv).rgb * AccumWeight.y, CompositeParams.z);
    float e = CompositeParams.x;
    if (CompositeParams.y > 0.5) e = 0.18 / exp2(texture2D(ExposureTex, vec2(0.5, 0.5)).r);
    c *= e * CompositeParams2.w;
    int tm = int(CompositeParams.w + 0.5);
    vec3 t;
    if (tm == 0) t = acesFit(c);
    else if (tm == 1) t = pbrNeutral(c);
    else if (tm == 2) t = acesHill(c);
    else if (tm == 3) t = agx(c);
    else t = clamp(c, 0.0, 1.0);
    vec3 s = linearToSrgbExact(t);
    if (CompositeParams2.x > 0.5) s = applyLut(s);
    if (CompositeParams2.z > 0.5) gl_FragColor = vec4(srgbToLinearExact(s) * AccumWeight.x, AccumWeight.x);
    else gl_FragColor = vec4(s, dot(s, vec3(0.299, 0.587, 0.114)));");

    /// <summary>
    /// FXAA-style edge anti-aliasing on the display image (luma in alpha). FxaaParams: xy = 1/size, z = sub-pixel
    /// amount, w = relative edge threshold.
    /// </summary>
    static EffectBuilder Fxaa() => FullScreen(G2EffectNames.Fxaa)
        .Vec4("FxaaParams", Stage.Pixel)
        .Texture("Src", 0)
        .PixelLib("float lumaAt(vec2 uv) { return texture2D(Src, uv).a; }")
        .PixelMain(@"
    vec2 px = FxaaParams.xy;
    vec4 cM = texture2D(Src, v_uv);
    float lM = cM.a;
    float lN = lumaAt(v_uv + vec2(0.0, -px.y)), lS = lumaAt(v_uv + vec2(0.0, px.y));
    float lE = lumaAt(v_uv + vec2(px.x, 0.0)), lW = lumaAt(v_uv + vec2(-px.x, 0.0));
    float lMax = max(max(max(lN, lS), max(lE, lW)), lM), lMin = min(min(min(lN, lS), min(lE, lW)), lM);
    float range = lMax - lMin;
    if (range < max(0.0312, lMax * FxaaParams.w)) { gl_FragColor = vec4(cM.rgb, 1.0); }
    else {
        float lNW = lumaAt(v_uv + vec2(-px.x, -px.y)), lNE = lumaAt(v_uv + vec2(px.x, -px.y));
        float lSW = lumaAt(v_uv + vec2(-px.x, px.y)), lSE = lumaAt(v_uv + vec2(px.x, px.y));
        float lAvg = (2.0 * (lN + lS + lE + lW) + lNW + lNE + lSW + lSE) / 12.0;
        float sub = clamp(abs(lAvg - lM) / range, 0.0, 1.0);
        sub = smoothstep(0.0, 1.0, sub); sub = sub * sub * FxaaParams.z;
        float edgeH = abs(lNW - 2.0 * lW + lSW) + 2.0 * abs(lN - 2.0 * lM + lS) + abs(lNE - 2.0 * lE + lSE);
        float edgeV = abs(lNW - 2.0 * lN + lNE) + 2.0 * abs(lW - 2.0 * lM + lE) + abs(lSW - 2.0 * lS + lSE);
        bool horz = edgeH >= edgeV;
        float l1 = horz ? lN : lW, l2 = horz ? lS : lE;
        float g1 = abs(l1 - lM), g2 = abs(l2 - lM);
        float stepLen = horz ? px.y : px.x;
        float lLocal, grad;
        if (g1 >= g2) { stepLen = -stepLen; lLocal = 0.5 * (l1 + lM); grad = g1; }
        else { lLocal = 0.5 * (l2 + lM); grad = g2; }
        vec2 uvE = v_uv + (horz ? vec2(0.0, stepLen * 0.5) : vec2(stepLen * 0.5, 0.0));
        vec2 dir = horz ? vec2(px.x, 0.0) : vec2(0.0, px.y);
        float gs = grad * 0.25;
        vec2 uvN = uvE - dir, uvP = uvE + dir;
        float eN = lumaAt(uvN) - lLocal, eP = lumaAt(uvP) - lLocal;
        bool dN = abs(eN) >= gs, dP = abs(eP) >= gs;
        for (int i = 0; i < 10; i++) {
            if (dN && dP) break;
            float s = i < 3 ? 1.0 : (i < 6 ? 1.5 : (i < 8 ? 2.0 : 4.0));
            if (!dN) { uvN -= dir * s; eN = lumaAt(uvN) - lLocal; dN = abs(eN) >= gs; }
            if (!dP) { uvP += dir * s; eP = lumaAt(uvP) - lLocal; dP = abs(eP) >= gs; }
        }
        float distN = horz ? v_uv.x - uvN.x : v_uv.y - uvN.y;
        float distP = horz ? uvP.x - v_uv.x : uvP.y - v_uv.y;
        bool nCloser = distN < distP;
        float off = 0.5 - min(distN, distP) / (distN + distP);
        bool mLess = lM - lLocal < 0.0;
        bool good = ((nCloser ? eN : eP) < 0.0) != mLess;
        off = max(good ? off : 0.0, sub);
        vec2 uvF = v_uv + (horz ? vec2(0.0, off * stepLen) : vec2(off * stepLen, 0.0));
        gl_FragColor = vec4(texture2D(Src, uvF).rgb, 1.0);
    }");

    /// <summary>Copy with scale; CopyParams.x = scale, y = 1: encode linear → sRGB (exact curve).</summary>
    static EffectBuilder Copy() => FullScreen(G2EffectNames.Copy)
        .Vec4("CopyParams", Stage.Pixel)
        .Texture("Src", 0)
        .PixelLib(@"
vec3 encSrgb(vec3 c) {
    c = clamp(c, 0.0, 1.0);
    vec3 lo = c * 12.92, hi = 1.055 * pow(c, vec3(1.0 / 2.4)) - 0.055;
    return vec3(c.r <= 0.0031308 ? lo.r : hi.r, c.g <= 0.0031308 ? lo.g : hi.g, c.b <= 0.0031308 ? lo.b : hi.b);
}")
        .PixelMain(@"
    vec4 c = texture2D(Src, v_uv) * CopyParams.x;
    if (CopyParams.y > 0.5) c.rgb = encSrgb(c.rgb / max(c.a, 1e-6));
    gl_FragColor = vec4(c.rgb, 1.0);");
}
