using Gfx.Engine.Effects;
using Gfx.Engine.Mgfx;
using Microsoft.Xna.Framework.Graphics;

namespace Gfx.Proc.Environment.Trees;

/// <summary>
/// Tree effects (stage env; PRESENTATION ONLY).
/// <para><b>EnvBark</b>: photographic bark (albedo + roughness, OpenGL normal + cavity) on the ez-tree rings, PBR with the
/// engine's IBL, cascaded shadows and aerial perspective; canopy occlusion from the vertex.</para>
/// <para><b>EnvLeaf</b>: alpha-tested leaf cards (stochastic threshold in jittered stills), the rounded canopy normal
/// blended with the card's own normal-mapped face, canopy occlusion, a waxy specular lobe and transmitted sunlight
/// (diffuse back-lighting through the leaf, tinted by the species' transmission colour and the card's thickness map).</para>
/// Both cross-fade between detail levels with complementary screen-space dithering and sway in a deterministic wind.
/// </summary>
public static class TreeEffects
{
    public const int AlbSlot = GlslLib.FirstMaterialSlot, NrmSlot = GlslLib.FirstMaterialSlot + 1;

    public static IEnumerable<EffectBuilder> Create()
    {
        yield return Bark(depth: false);
        yield return Bark(depth: true);
        yield return Leaf(depth: false);
        yield return Leaf(depth: true);
        foreach (var e in ImpostorEffects.Create()) yield return e;
    }

    /// <summary>Impostor bake outputs (BakeMode.x: 0 lit, 1 albedo (gamma 1/2.2) + coverage, 2 octahedral normal + depth + occlusion).
    /// BakeFrame: xyz = the frame's view direction (towards the viewer), w = model radius; BakeCenter: xyz = centre.</summary>
    const string BakeLib = @"
// albedo pass: albedo (gamma 1/2.2) + coverage; normal pass: octahedral normal, depth along the frame direction, occlusion
vec4 bakeOut(vec3 albedo, vec3 n, vec3 p, float ao) {
    if (BakeMode.x < 1.5) return vec4(pow(max(albedo, vec3(0.0)), vec3(1.0 / 2.2)), 1.0);
    float d = 0.5 + dot(p - BakeCenter.xyz, BakeFrame.xyz) / (2.0 * BakeFrame.w);
    return vec4(octEnc(normalize(n)), clamp(d, 0.0, 1.0), clamp(ao, 0.0, 1.0));
}";

    static EffectBuilder AddBake(this EffectBuilder b) => b.Vec4("BakeMode", Stage.Pixel).Vec4("BakeFrame", Stage.Pixel).Vec4("BakeCenter", Stage.Pixel);

    static EffectBuilder BarkAttrs(EffectBuilder b) => b
        .Attribute("vec4 a_pos", VertexElementUsage.Position, 0)
        .Attribute("vec3 a_nrm", VertexElementUsage.Normal, 0)
        .Attribute("vec3 a_tan", VertexElementUsage.Tangent, 0)
        .Attribute("vec2 a_uv", VertexElementUsage.TextureCoordinate, 0)
        .Attribute("vec4 a_wind", VertexElementUsage.TextureCoordinate, 6)
        .AddTreeInstance().AddEnvParams().VertexLib(EnvGlsl.Wind);

    const string BarkVsBody = @"
    vec3 wp = instPos(vec4(a_pos.xyz, 1.0)) + windOffset(a_wind.w, a_wind.x, a_wind.y, 0.0, i_tint.a, i_lod.z);";

    static EffectBuilder Bark(bool depth)
    {
        var b = BarkAttrs(new EffectBuilder(depth ? EnvEffectNames.BarkDepth : EnvEffectNames.Bark));
        if (depth)
            return b.Matrix("LightViewProj", Stage.Vertex).Varyings("float v_depth;")
                .VertexMain(BarkVsBody + @"
    gl_Position = LightViewProj_mul(vec4(wp, 1.0)); v_depth = gl_Position.z / gl_Position.w;")
                .PixelMain("    gl_FragColor = vec4(v_depth, 0.0, 0.0, 1.0);");
        return b.AddStandardLit().VertexLib(GlslLib.ShadowLookup)
            .Vec4("SpeciesTint", Stage.Pixel).AddBake()
            .Texture("BarkAlb", AlbSlot).Texture("BarkNrm", NrmSlot)
            .Varyings("vec3 v_wpos; vec3 v_nrm; vec3 v_tan; vec2 v_uv; vec4 v_wind; vec3 v_tint; vec2 v_lod; vec4 v_lpos;")
            .VertexMain(BarkVsBody + @"
    v_wpos = wp; v_nrm = normalize(instDir(a_nrm)); v_tan = normalize(instDir(a_tan)); v_uv = a_uv; v_wind = a_wind;
    v_tint = i_tint.rgb; v_lod = i_lod.xy;
    v_lpos = shadowLookup(wp, v_nrm);
    gl_Position = ViewProj_mul(vec4(wp, 1.0));")
            .PixelLib(EnvGlsl.Common).PixelLib(EnvGlsl.Foliage).PixelLib(BakeLib)
            .PixelMain(@"
    lodDither(v_lod);
    vec4 ar = texture2D(BarkAlb, v_uv);
    vec4 nr = texture2D(BarkNrm, v_uv);
    vec3 N = normalize(v_nrm);
    vec3 T = normalize(v_tan - N * dot(N, v_tan));
    vec3 m = decodeNormal(nr);
    // OpenGL normal maps: +Y points up the image (towards v = 0), which on the ring is cross(N, T)
    vec3 n = normalize(T * m.x + cross(N, T) * m.y + N * m.z);
    vec3 albedo = decodeSrgb(ar.rgb) * SpeciesTint.rgb * v_tint;
    float ao = nr.b * v_wind.z;
    if (BakeMode.x > 0.5) { gl_FragColor = bakeOut(albedo, n, v_wpos, ao); return; }
    vec3 c = shadePbrFull(albedo, clamp(ar.a, 0.3, 1.0), 0.0, ao, n, N, v_wpos, shadowPcf(v_lpos));
    gl_FragColor = vec4(applyFog(c, v_wpos), 1.0);");
    }

    static EffectBuilder LeafAttrs(EffectBuilder b) => b
        .Attribute("vec4 a_pos", VertexElementUsage.Position, 0)
        .Attribute("vec3 a_nrm", VertexElementUsage.Normal, 0)
        .Attribute("vec2 a_uv", VertexElementUsage.TextureCoordinate, 0)
        .Attribute("vec4 a_wind", VertexElementUsage.TextureCoordinate, 6)
        .AddTreeInstance().AddEnvParams().VertexLib(EnvGlsl.Wind);

    const string LeafVsBody = @"
    float h = clamp(a_pos.y / max(TreeParams.x, 0.1), 0.0, 1.0);
    vec3 wp = instPos(vec4(a_pos.xyz, 1.0)) + windOffset(h, a_wind.x, a_wind.y, a_wind.w, i_tint.a, i_lod.z);";

    static EffectBuilder Leaf(bool depth)
    {
        var b = LeafAttrs(new EffectBuilder(depth ? EnvEffectNames.LeafDepth : EnvEffectNames.Leaf))
            .Vec4("TreeParams", Stage.Vertex) // x = model height (m)
            .Texture("LeafAlb", AlbSlot);
        if (depth)
            return b.Matrix("LightViewProj", Stage.Vertex).Varyings("vec2 v_uv; float v_depth;")
                .VertexMain(LeafVsBody + @"
    v_uv = a_uv;
    gl_Position = LightViewProj_mul(vec4(wp, 1.0)); v_depth = gl_Position.z / gl_Position.w;")
                .PixelLib(EnvGlsl.Common)
                .PixelMain(@"
    if (texture2D(LeafAlb, v_uv).a < EnvParams.w) discard;
    gl_FragColor = vec4(v_depth, 0.0, 0.0, 1.0);");
        return b.AddStandardLit().VertexLib(GlslLib.ShadowLookup)
            .Vec4("SpeciesTint", Stage.Pixel).Vec4("Transmission", Stage.Pixel).AddBake()
            .Texture("LeafNrm", NrmSlot)
            .Varyings("vec3 v_wpos; vec3 v_nrm; vec2 v_uv; vec4 v_wind; vec3 v_tint; vec2 v_lod; vec4 v_lpos;")
            .VertexMain(LeafVsBody + @"
    v_wpos = wp; v_nrm = normalize(instDir(a_nrm)); v_uv = a_uv; v_wind = a_wind; v_tint = i_tint.rgb; v_lod = i_lod.xy;
    v_lpos = shadowLookup(wp, v_nrm);
    gl_Position = ViewProj_mul(vec4(wp, 1.0));")
            .PixelLib(EnvGlsl.Common).PixelLib(EnvGlsl.Foliage).PixelLib(BakeLib)
            .PixelMain(@"
    vec4 a = texture2D(LeafAlb, v_uv);
    if (a.a < alphaCut(vec2(17.0, 59.0))) discard;
    lodDither(v_lod);
    vec4 nt = texture2D(LeafNrm, v_uv);
    vec3 V = normalize(CameraPos.xyz - v_wpos);
    // the card's own face (from derivatives), turned towards the viewer, carries the leaf normal map
    vec3 dpx = dFdx(v_wpos), dpy = dFdy(v_wpos);
    vec3 F = normalize(cross(dpx, dpy));
    if (dot(F, V) < 0.0) F = -F;
    vec2 dux = dFdx(v_uv), duy = dFdy(v_uv);
    vec3 dp2perp = cross(dpy, F), dp1perp = cross(F, dpx);
    vec3 Tt = dp2perp * dux.x + dp1perp * duy.x, Bt = dp2perp * dux.y + dp1perp * duy.y;
    float inv = inversesqrt(max(max(dot(Tt, Tt), dot(Bt, Bt)), 1e-12));
    vec3 m = decodeNormal(nt);
    vec3 Fd = normalize(Tt * inv * m.x - Bt * inv * m.y + F * m.z);
    vec3 Nr = normalize(v_nrm);
    vec3 N = normalize(mix(Nr, Fd, 0.35));
    vec3 albedo = decodeSrgb(a.rgb) * SpeciesTint.rgb * v_tint;
    float ao = v_wind.z;
    if (BakeMode.x > 0.5) { gl_FragColor = bakeOut(albedo, N, v_wpos, ao); return; }
    float sh = shadowPcf(v_lpos);
    vec3 c = shadeFoliage(albedo, N, v_wpos, sh, ao, 0.25);
    // transmitted sunlight: the far side of a sunlit leaf glows (diffuse transmission), stronger looking into the sun
    vec3 L = SunDir.xyz;
    float back = max(dot(-Fd, L), 0.0) * 0.6 + max(dot(-Nr, L), 0.0) * 0.4;
    float fwd = pow(max(dot(-V, L), 0.0), 6.0);
    vec3 trans = albedo / max(dot(albedo, vec3(0.2126, 0.7152, 0.0722)), 1e-4) * Transmission.rgb * (0.06 + 0.10 * nt.b);
    c += trans * SunColor.rgb * sh * (back * (1.0 + 2.0 * fwd)) / PI * mix(0.6, 1.0, ao);
    gl_FragColor = vec4(applyFog(c, v_wpos), 1.0);");
    }
}
