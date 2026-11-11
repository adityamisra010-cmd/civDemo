using Gfx.Engine.Mgfx;
using Microsoft.Xna.Framework.Graphics;

namespace Gfx.Engine.Effects;

/// <summary>Effect names of the screen-space passes (stage render2).</summary>
public static class SsEffectNames
{
    public const string Gtao = "SsGtao";
    public const string AoBlur = "SsAoBlur";
    public const string AoApply = "SsAoApply";
    public const string Debug = "SsDebug";
}

/// <summary>
/// Screen-space ambient occlusion and contact shadows (stage render2; PRESENTATION ONLY), working from the G-buffer-lite
/// of the G2 scene pass (<see cref="GBufferLayout"/>):
/// <list type="number">
/// <item><b>GTAO at half resolution</b> (<c>SsGtao</c>): view-space positions from the linear depth;
/// normals from depth differences (the smaller one-sided difference on each axis, so edges do not smear);
/// ground-truth ambient occlusion (Jimenez et al. 2016, "Practical Real-Time Strategies for Accurate Indirect
/// Occlusion"): per pixel 3 slices × 2 sides × 4 steps over a world-space radius, the horizon angles clamped to the
/// normal's hemisphere and the cosine-weighted visible arc integrated analytically; a 4×4 interleaved rotation and
/// step pattern that the 4×4 blur averages out, shifted per still sample or TAA frame. Output RGBA16F: r = AO, g = 1,
/// b = the linear depth used, a = 1.</item>
/// <item><b>4×4 depth-aware blur</b> (<c>SsAoBlur</c>) at half resolution.</item>
/// <item><b>Apply</b> (<c>SsAoApply</c>): a depth-aware 2×2 upsample of the AO to full resolution; contact shadows at
/// full resolution: a short ray (default 0.3 m, 8 steps with per-pixel interleaved-gradient jitter, shifted per still
/// sample or TAA frame) from the pixel towards the sun, marched in screen space against the depth, where a sample that
/// lies behind the depth by more than a bias and less than a thickness occludes; then the multiplier
/// 1 − a·(1 − AO) − d·(1 − contact) from the ambient share a and the direct share d of target 2, written with a
/// multiplicative blend onto the HDR scene before the transparent pass. AO never touches direct sunlight; contact
/// shadows never touch sky light.</item>
/// </list>
/// All full-screen passes use the engine's one-triangle <c>FullScreen.Triangle</c> and the G1 uv convention.
/// </summary>
public sealed class ScreenSpaceEffects : IEffectSource
{
    public IEnumerable<EffectBuilder> CreateEffects()
    {
        yield return Gtao();
        yield return Blur();
        yield return Apply();
        yield return Debug();
    }

    static EffectBuilder FullScreen(string name) => new EffectBuilder(name)
        .Attribute("vec4 a_pos", VertexElementUsage.Position, 0)
        .Varyings("vec2 v_uv;")
        .VertexMain(@"
    v_uv = vec2(a_pos.x * 0.5 + 0.5, -a_pos.y * 0.5 + 0.5);
    gl_Position = vec4(a_pos.xy, 0.0, 1.0);");

    /// <summary>
    /// Shared view-space reconstruction. <c>ProjInfo</c>: x = 1/P.M11, y = 1/P.M22, z/w = the projection's sub-pixel
    /// jitter in NDC. <c>FullSize</c>: xy = full-resolution size, zw = 1/size.
    /// </summary>
    public const string ViewLib = @"
const float PI_SS = 3.14159265;
vec3 viewPos(vec2 uv, float d) {
    vec2 ndc = vec2(uv.x * 2.0 - 1.0, 1.0 - uv.y * 2.0) - ProjInfo.zw;
    return vec3(ndc.x * d * ProjInfo.x, ndc.y * d * ProjInfo.y, -d);
}
vec2 viewToUv(vec3 p) {
    float d = max(-p.z, 1e-4);
    vec2 ndc = vec2(p.x / (d * ProjInfo.x), p.y / (d * ProjInfo.y)) + ProjInfo.zw;
    return vec2(ndc.x * 0.5 + 0.5, 0.5 - ndc.y * 0.5);
}
float depthAt(vec2 uv) { return texture2D(DepthTex, uv).r; }";

    /// <summary>
    /// GTAO + contact shadows. <c>AoParams</c>: x = radius (m), y = 1 / AO fade-out start distance (m), z = max
    /// screen radius (half-res pixels), w = frame rotation (0..1). <c>ContactParams</c>: x = ray length (m), y =
    /// thickness (m), z = 1 contact shadows on, w = 1 AO on. <c>SunView</c>: xyz = direction to the sun in view space.
    /// <c>HalfSize</c>: xy = half-resolution size, zw = 1/size.
    /// </summary>
    static EffectBuilder Gtao() => FullScreen(SsEffectNames.Gtao)
        .Vec4("ProjInfo", Stage.Pixel).Vec4("FullSize", Stage.Pixel).Vec4("HalfSize", Stage.Pixel)
        .Vec4("AoParams", Stage.Pixel).Vec4("ContactParams", Stage.Pixel).Vec4("SunView", Stage.Pixel)
        .Texture("DepthTex", 0)
        .PixelLib(ViewLib + @"
// position of the full-resolution texel (ix, iy) offset from the centre texel c (in texels)
vec3 tapPos(vec2 c, vec2 o) { vec2 uv = (c + o + 0.5) * FullSize.zw; return viewPos(uv, depthAt(uv)); }
float gtaoSlice(vec2 uvc, vec3 P, vec3 V, vec3 N, vec2 dir, float rPx, float jitter, float radius) {
    // slice plane through V containing the screen direction dir
    vec3 dv = vec3(dir.x, -dir.y, 0.0);                 // screen y runs down, view y up
    vec3 ortho = dv - dot(dv, V) * V;
    vec3 axis = normalize(cross(ortho, V));
    vec3 pn = N - axis * dot(N, axis);
    float pnLen = max(length(pn), 1e-4);
    float sgn = dot(ortho, pn) >= 0.0 ? 1.0 : -1.0;
    float cosN = clamp(dot(pn, V) / pnLen, -1.0, 1.0);
    float n = sgn * acos(cosN);
    float h0c = -1.0, h1c = -1.0;
    float falloff = radius * radius;
    for (int s = 0; s < 4; s++) {
        float t = (float(s) + jitter) / 4.0;
        t = t * t;                                       // denser near the pixel
        vec2 off = dir * max(t * rPx, 1.0 + float(s)) * 2.0;   // half-res pixels -> full-res texels
        vec2 uv1 = uvc + off * FullSize.zw, uv0 = uvc - off * FullSize.zw;
        vec3 S1 = viewPos(uv1, depthAt(uv1)), S0 = viewPos(uv0, depthAt(uv0));
        vec3 d1 = S1 - P, d0 = S0 - P;
        float l1 = dot(d1, d1), l0 = dot(d0, d0);
        float c1 = dot(d1, V) * inversesqrt(max(l1, 1e-8)), c0 = dot(d0, V) * inversesqrt(max(l0, 1e-8));
        // distance falloff: samples beyond the radius fade back to the lowest horizon
        float w1 = clamp(1.0 - l1 / falloff, 0.0, 1.0), w0 = clamp(1.0 - l0 / falloff, 0.0, 1.0);
        h1c = max(h1c, mix(-1.0, c1, w1));
        h0c = max(h0c, mix(-1.0, c0, w0));
    }
    // horizon angles measured from V towards +dir (h1) and -dir (h0), clamped to the normal's hemisphere
    float h1 = n + min(acos(clamp(h1c, -1.0, 1.0)) - n, PI_SS * 0.5);
    float h0 = n + max(-acos(clamp(h0c, -1.0, 1.0)) - n, -PI_SS * 0.5);
    float sinN = sin(n);
    float a1 = cosN + 2.0 * h1 * sinN - cos(2.0 * h1 - n);
    float a0 = cosN + 2.0 * h0 * sinN - cos(2.0 * h0 - n);
    return pnLen * 0.25 * (a0 + a1);
}")
        .PixelMain(@"
    vec2 px = floor(v_uv * HalfSize.xy);                      // half-res pixel
    vec2 c = px * 2.0;                                         // its top-left full-res texel
    vec2 uvc = (c + 0.5) * FullSize.zw;
    float d = depthAt(uvc);
    if (d > 5.0e5) { gl_FragColor = vec4(1.0, 1.0, d, 1.0); return; }
    vec3 P = viewPos(uvc, d);
    // normal from the smaller one-sided differences
    vec3 pr = tapPos(c, vec2(1.0, 0.0)), pl = tapPos(c, vec2(-1.0, 0.0));
    vec3 pd = tapPos(c, vec2(0.0, 1.0)), pu = tapPos(c, vec2(0.0, -1.0));
    vec3 dx = abs(pr.z - P.z) < abs(P.z - pl.z) ? pr - P : P - pl;
    vec3 dy = abs(pd.z - P.z) < abs(P.z - pu.z) ? pd - P : P - pu;
    vec3 N = normalize(cross(dy, dx));
    vec3 V = normalize(-P);
    if (dot(N, V) < 0.0) N = -N;
    // 4x4 interleaved pattern (averaged by the 4x4 blur), shifted per frame
    vec2 m = mod(px, 4.0);
    float idx = m.x + 4.0 * m.y;
    float rot = fract((idx * 7.0 + 0.5) / 16.0 + AoParams.w);
    float jitter = fract(mod(m.x + 2.0 * m.y, 4.0) * 0.25 + 0.125 + AoParams.w * 3.0);
    float ao = 1.0;
    if (ContactParams.w > 0.5) {
        float radius = AoParams.x;
        // screen radius in half-res pixels: radius / (2 d tan(fov/2)) of the half-res height
        float rPx = min(radius * HalfSize.y / (2.0 * d * ProjInfo.y), AoParams.z);
        if (rPx > 1.0) {
            float vis = 0.0;
            for (int k = 0; k < 3; k++) {
                float phi = (float(k) + rot) * (PI_SS / 3.0);
                vis += gtaoSlice(uvc, P, V, N, vec2(cos(phi), sin(phi)), rPx, jitter, radius);
            }
            ao = clamp(vis / 3.0, 0.0, 1.0);
        }
        ao = mix(ao, 1.0, clamp((d * AoParams.y - 1.0) * 2.0, 0.0, 1.0)); // fade out beyond the start distance
    }
    gl_FragColor = vec4(ao, 1.0, d, 1.0);");

    /// <summary>4×4 depth-aware box blur at half resolution (cancels the 4×4 pattern). <c>HalfSize</c>: xy size, zw 1/size.</summary>
    static EffectBuilder Blur() => FullScreen(SsEffectNames.AoBlur)
        .Vec4("HalfSize", Stage.Pixel)
        .Texture("AoTex", 0)
        .PixelMain(@"
    vec4 c0 = texture2D(AoTex, v_uv);
    float d0 = c0.b;
    if (d0 > 5.0e5) { gl_FragColor = c0; return; }
    vec2 sum = vec2(0.0); float ws = 0.0;
    float tol = 0.03 * d0 + 0.05;
    for (int j = -2; j < 2; j++) for (int i = -2; i < 2; i++) {
        vec4 s = texture2D(AoTex, v_uv + vec2(float(i) + 0.5, float(j) + 0.5) * HalfSize.zw);
        float w = 1.0 / (1.0 + abs(s.b - d0) / tol);
        w *= w;
        sum += s.rg * w; ws += w;
    }
    gl_FragColor = vec4(sum / max(ws, 1e-5), d0, 1.0);");

    /// <summary>
    /// Depth-aware upsample and the multiplier for the HDR scene. <c>ApplyParams</c>: x = AO strength (0..1),
    /// y = contact strength (0..1), z = assumed albedo for the multi-bounce lift (0 = off), w = debug output (0 the
    /// multiplier, 2 the AO, 3 the contact visibility). <c>ContactParams</c>: x = ray length (m), y = thickness (m),
    /// z = 1 on, w = jitter offset. <c>HalfSize</c>, <c>FullSize</c>, <c>ProjInfo</c>, <c>SunView</c> as above.
    /// </summary>
    static EffectBuilder Apply() => FullScreen(SsEffectNames.AoApply)
        .Vec4("HalfSize", Stage.Pixel).Vec4("FullSize", Stage.Pixel).Vec4("ApplyParams", Stage.Pixel)
        .Vec4("ProjInfo", Stage.Pixel).Vec4("ContactParams", Stage.Pixel).Vec4("SunView", Stage.Pixel)
        .Texture("AoTex", 0).Texture("DepthTex", 1).Texture("SplitTex", 2)
        .PixelLib(ViewLib + @"
// contact shadows at full resolution: a short ray towards the sun, marched in screen space against the depth; a sample
// behind the depth by more than a bias and less than the thickness occludes (sharper the nearer the occluder)
float contactVisibility(vec2 uv, float d) {
    vec2 fc = uv * FullSize.xy;
    vec3 P = viewPos(uv, d);
    // normal from the smaller one-sided depth differences (no smearing across silhouettes)
    vec2 ox = vec2(FullSize.z, 0.0), oy = vec2(0.0, FullSize.w);
    vec3 pr = viewPos(uv + ox, depthAt(uv + ox)), pl = viewPos(uv - ox, depthAt(uv - ox));
    vec3 pd = viewPos(uv + oy, depthAt(uv + oy)), pu = viewPos(uv - oy, depthAt(uv - oy));
    vec3 dx = abs(pr.z - P.z) < abs(P.z - pl.z) ? pr - P : P - pl;
    vec3 dy = abs(pd.z - P.z) < abs(P.z - pu.z) ? pd - P : P - pu;
    vec3 N = normalize(cross(dy, dx));
    if (dot(N, -P) < 0.0) N = -N;
    if (dot(N, SunView.xyz) <= 0.0) return 1.0;
    float jitter = fract(52.9829189 * fract(dot(fc + ContactParams.w * vec2(5.588238, 3.233591), vec2(0.06711056, 0.00583715))));
    vec3 o = P + N * (0.004 + 0.0015 * d);
    float occ = 0.0;
    for (int s = 0; s < 8; s++) {
        float t = (float(s) + jitter) / 8.0;
        vec3 Q = o + SunView.xyz * (t * ContactParams.x);
        vec2 q = viewToUv(Q);
        if (q.x < 0.0 || q.y < 0.0 || q.x > 1.0 || q.y > 1.0) break;
        float dq = -Q.z;
        float dz = dq - depthAt(q);
        if (dz > 0.002 * dq + 0.01 && dz < ContactParams.y) occ = max(occ, 1.0 - t * 0.8);
    }
    return 1.0 - occ * (1.0 - clamp((d - 80.0) / 80.0, 0.0, 1.0)); // fade out 80-160 m
}")
        .PixelMain(@"
    float d = texture2D(DepthTex, v_uv).r;
    vec4 split = texture2D(SplitTex, v_uv);
    if (d > 5.0e5 || (split.r + split.g < 1e-4 && ApplyParams.w < 0.5)) { gl_FragColor = vec4(1.0); return; }
    // the four half-res texels around this pixel, bilinear weights times depth similarity
    vec2 hp = v_uv * HalfSize.xy - 0.5;
    vec2 b = floor(hp); vec2 f = hp - b;
    vec2 acc = vec2(0.0); float ws = 0.0;
    float tol = 0.02 * d + 0.03;
    for (int j = 0; j < 2; j++) for (int i = 0; i < 2; i++) {
        vec4 s = texture2D(AoTex, (b + vec2(float(i), float(j)) + 0.5) * HalfSize.zw);
        float bw = (i == 0 ? 1.0 - f.x : f.x) * (j == 0 ? 1.0 - f.y : f.y);
        float w = (bw + 1e-3) / (1.0 + abs(s.b - d) / tol);
        acc += s.rg * w; ws += w;
    }
    vec2 v = acc / max(ws, 1e-5);
    float ao = v.x;
    float cs = ContactParams.z > 0.5 && (split.g > 1e-3 || ApplyParams.w > 2.5) ? contactVisibility(v_uv, d) : 1.0;
    if (ApplyParams.z > 0.0) {                          // multi-bounce lift (Jimenez et al. 2016)
        float al = ApplyParams.z;
        float A = 2.0404 * al - 0.3324, B = -4.7951 * al + 0.6417, C = 2.7552 * al + 0.6903;
        ao = max(ao, ((ao * A + B) * ao + C) * ao);
    }
    ao = mix(1.0, ao, ApplyParams.x);
    cs = mix(1.0, cs, ApplyParams.y);
    float m = 1.0 - split.r * (1.0 - ao) - split.g * (1.0 - cs);
    if (ApplyParams.w > 2.5) m = cs; else if (ApplyParams.w > 1.5) m = ao;   // debug views: the term itself
    gl_FragColor = vec4(vec3(max(m, 0.0)), 1.0);");

    /// <summary>
    /// Debug views of the G-buffer-lite and the screen-space terms (display-ready). <c>DebugParams</c>: x = mode (1 depth,
    /// 2 AO, 3 contact, 4 shares: r ambient, g direct, b reported, 5 shadow cascades), y = depth scale (m).
    /// </summary>
    static EffectBuilder Debug() => FullScreen(SsEffectNames.Debug)
        .Vec4("DebugParams", Stage.Pixel)
        .Texture("AoTex", 0).Texture("DepthTex", 1).Texture("SplitTex", 2)
        .PixelMain(@"
    int mode = int(DebugParams.x + 0.5);
    vec3 c;
    if (mode == 1) { float d = texture2D(DepthTex, v_uv).r; c = d > 5.0e5 ? vec3(0.3, 0.5, 1.0) : vec3(1.0 - exp(-d / DebugParams.y)); }
    else if (mode == 2) c = vec3(texture2D(AoTex, v_uv).r);
    else if (mode == 5) {
        // cascade used for the sun shadow: 1 red, 2 green, 3 blue, 4 yellow; blend bands mix towards the next colour
        float k = texture2D(SplitTex, v_uv).a * 5.0;
        vec3 cols[5]; cols[0] = vec3(0.5); cols[1] = vec3(1.0, 0.25, 0.2); cols[2] = vec3(0.25, 0.9, 0.3); cols[3] = vec3(0.25, 0.45, 1.0); cols[4] = vec3(1.0, 0.9, 0.2);
        int i0 = int(floor(k)); float fr = k - floor(k);
        vec3 a = cols[0]; vec3 b = cols[0];
        for (int i = 0; i < 5; i++) { if (i == i0) a = cols[i]; if (i == i0 + 1) b = cols[i]; }
        c = mix(a, i0 >= 4 ? a : b, fr);
        float d = texture2D(DepthTex, v_uv).r;
        if (d > 5.0e5) c = vec3(0.6, 0.75, 1.0);
    }
    else c = texture2D(SplitTex, v_uv).rgb;
    gl_FragColor = vec4(c, 1.0);");
}
