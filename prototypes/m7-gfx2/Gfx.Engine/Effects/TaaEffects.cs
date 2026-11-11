using Gfx.Engine.Mgfx;
using Microsoft.Xna.Framework.Graphics;

namespace Gfx.Engine.Effects;

/// <summary>Effect name of the temporal anti-aliasing resolve (stage render2).</summary>
public static class TaaEffectNames
{
    public const string Resolve = "PostTaaResolve";
}

/// <summary>
/// Temporal anti-aliasing resolve (stage render2; PRESENTATION ONLY), for interactive frames in motion; deliverable
/// stills keep the jittered accumulation of <c>ForwardRenderer.RenderStill</c>. Each frame renders with a Halton(2,3)
/// sub-pixel jitter; the resolve reprojects the previous resolved frame through this pixel's linear depth (the
/// G-buffer-lite) and the previous unjittered view-projection (or follows the object motion a skinned effect wrote),
/// samples it with a 9-tap Catmull-Rom filter, clips it to the variance box of the current 13-tap neighbourhood (the
/// 3×3 block plus the four texels two away) in YCoCg (on Karis-tonemapped colours, so bright pixels do not dominate),
/// and blends: 10 % current, up to 30 % when the history moved 12 pixels or more. History that reprojects off
/// screen is dropped per pixel; the renderer drops all of it after a camera cut (<c>TaaHistory</c>). Output is linear
/// HDR, read by the bloom, metering and composite passes.
/// </summary>
public sealed class TaaEffects : IEffectSource
{
    public IEnumerable<EffectBuilder> CreateEffects() { yield return Resolve(); }

    /// <summary>
    /// <c>ProjInfo</c>: x = 1/P.M11, y = 1/P.M22, zw = this frame's NDC jitter. <c>Reproject</c>: view (this frame) →
    /// clip (previous frame, unjittered). <c>TaaParams</c>: x = current weight, y = 1 history valid, z = variance clip
    /// gamma, w = current weight at high motion. <c>TaaSize</c>: xy = size, zw = 1/size.
    /// </summary>
    static EffectBuilder Resolve() => new EffectBuilder(TaaEffectNames.Resolve)
        .Attribute("vec4 a_pos", VertexElementUsage.Position, 0)
        .Varyings("vec2 v_uv;")
        .VertexMain(@"
    v_uv = vec2(a_pos.x * 0.5 + 0.5, -a_pos.y * 0.5 + 0.5);
    gl_Position = vec4(a_pos.xy, 0.0, 1.0);")
        .Vec4("ProjInfo", Stage.Pixel).Matrix("Reproject", Stage.Pixel).Vec4("TaaParams", Stage.Pixel).Vec4("TaaSize", Stage.Pixel)
        .Texture("Cur", 0).Texture("Hist", 1).Texture("DepthTex", 2).Texture("VelTex", 3)
        .PixelLib(@"
vec3 toYCoCg(vec3 c) { return vec3(0.25 * c.r + 0.5 * c.g + 0.25 * c.b, 0.5 * c.r - 0.5 * c.b, -0.25 * c.r + 0.5 * c.g - 0.25 * c.b); }
vec3 fromYCoCg(vec3 y) { return vec3(y.x + y.y - y.z, y.x + y.z, y.x - y.y - y.z); }
vec3 karis(vec3 c) { return c / (1.0 + max(c.r, max(c.g, c.b))); }
vec3 karisInv(vec3 c) { return c / max(1.0 - max(c.r, max(c.g, c.b)), 1e-4); }
// bicubic Catmull-Rom history fetch from 9 bilinear taps (the middle two weights folded into one tap per axis)
vec3 historyCatmullRom(vec2 uv) {
    vec2 sp = uv * TaaSize.xy;
    vec2 t1 = floor(sp - 0.5) + 0.5;
    vec2 f = sp - t1;
    vec2 w0 = f * (-0.5 + f * (1.0 - 0.5 * f));
    vec2 w1 = 1.0 + f * f * (-2.5 + 1.5 * f);
    vec2 w2 = f * (0.5 + f * (2.0 - 1.5 * f));
    vec2 w3 = f * f * (-0.5 + 0.5 * f);
    vec2 w12 = w1 + w2;
    vec2 p0 = (t1 - 1.0) * TaaSize.zw, p3 = (t1 + 2.0) * TaaSize.zw, p12 = (t1 + w2 / w12) * TaaSize.zw;
    vec3 r = texture2D(Hist, vec2(p0.x, p0.y)).rgb * (w0.x * w0.y) + texture2D(Hist, vec2(p12.x, p0.y)).rgb * (w12.x * w0.y)
           + texture2D(Hist, vec2(p3.x, p0.y)).rgb * (w3.x * w0.y) + texture2D(Hist, vec2(p0.x, p12.y)).rgb * (w0.x * w12.y)
           + texture2D(Hist, vec2(p12.x, p12.y)).rgb * (w12.x * w12.y) + texture2D(Hist, vec2(p3.x, p12.y)).rgb * (w3.x * w12.y)
           + texture2D(Hist, vec2(p0.x, p3.y)).rgb * (w0.x * w3.y) + texture2D(Hist, vec2(p12.x, p3.y)).rgb * (w12.x * w3.y)
           + texture2D(Hist, vec2(p3.x, p3.y)).rgb * (w3.x * w3.y);
    return max(r, vec3(0.0));
}
// clip q towards the centre of the box [lo, hi] (Playdead/Salvi variance clipping)
vec3 clipBox(vec3 lo, vec3 hi, vec3 q) {
    vec3 c = 0.5 * (hi + lo), e = 0.5 * (hi - lo) + 1e-5;
    vec3 v = q - c;
    vec3 a = abs(v / e);
    float m = max(a.x, max(a.y, a.z));
    return m > 1.0 ? c + v / m : q;
}")
        .PixelMain(@"
    vec3 cur = texture2D(Cur, v_uv).rgb;
    if (TaaParams.y < 0.5) { gl_FragColor = vec4(cur, 1.0); return; }
    // neighbourhood: the 3x3 block plus the four texels two away (thin, sub-pixel lines rasterise with gaps; the wider
    // cross keeps their history from being clipped away in the gaps)
    vec3 m1 = vec3(0.0), m2 = vec3(0.0);
    for (int j = -1; j <= 1; j++) for (int i = -1; i <= 1; i++) {
        vec3 s = toYCoCg(karis(texture2D(Cur, v_uv + vec2(float(i), float(j)) * TaaSize.zw).rgb));
        m1 += s; m2 += s * s;
    }
    for (int k = 0; k < 4; k++) {
        vec2 o = k == 0 ? vec2(2.0, 0.0) : k == 1 ? vec2(-2.0, 0.0) : k == 2 ? vec2(0.0, 2.0) : vec2(0.0, -2.0);
        vec3 s = toYCoCg(karis(texture2D(Cur, v_uv + o * TaaSize.zw).rgb));
        m1 += s; m2 += s * s;
    }
    m1 /= 13.0; m2 /= 13.0;
    vec3 sigma = sqrt(max(m2 - m1 * m1, vec3(0.0)));
    vec3 lo = m1 - TaaParams.z * sigma, hi = m1 + TaaParams.z * sigma;
    // previous position: the object motion an effect wrote (skinned crowds), else reprojected through the linear depth
    vec2 puv;
    vec4 vel = texture2D(VelTex, v_uv);
    if (vel.z > 0.5) puv = v_uv - vec2(vel.x * 0.5, -vel.y * 0.5);
    else {
        float d = min(texture2D(DepthTex, v_uv).r, 1.0e5);
        vec2 ndc = vec2(v_uv.x * 2.0 - 1.0, 1.0 - v_uv.y * 2.0) - ProjInfo.zw;
        vec3 vp = vec3(ndc.x * d * ProjInfo.x, ndc.y * d * ProjInfo.y, -d);
        vec4 pc = Reproject_mul(vec4(vp, 1.0));
        if (pc.w <= 1e-4) { gl_FragColor = vec4(cur, 1.0); return; }
        puv = vec2(pc.x / pc.w * 0.5 + 0.5, 0.5 - pc.y / pc.w * 0.5);
    }
    if (puv.x < 0.0 || puv.y < 0.0 || puv.x > 1.0 || puv.y > 1.0) { gl_FragColor = vec4(cur, 1.0); return; }
    vec3 h = clipBox(lo, hi, toYCoCg(karis(historyCatmullRom(puv))));
    float motion = length((puv - v_uv) * TaaSize.xy);
    float a = mix(TaaParams.x, TaaParams.w, clamp(motion / 12.0, 0.0, 1.0));
    vec3 r = mix(h, toYCoCg(karis(cur)), a);
    gl_FragColor = vec4(karisInv(max(fromYCoCg(r), vec3(0.0))), 1.0);");
}
