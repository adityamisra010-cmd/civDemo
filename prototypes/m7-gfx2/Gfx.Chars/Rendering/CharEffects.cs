using Gfx.Engine.Effects;
using Gfx.Engine.Mgfx;
using Microsoft.Xna.Framework.Graphics;

namespace Gfx.Chars.Rendering;

/// <summary>Effect names of stage chars.</summary>
public static class CharEffectNames
{
    public const string Skin = "CharSkin", Eye = "CharEye", Mouth = "CharMouth", Cloth = "CharCloth", Metal = "CharMetal",
        Hair = "CharHair", Fur = "CharFur", Lod = "CharLod", Cornea = "CharCornea", Paint = "CharPaint", Depth = "CharDepth", DepthAlpha = "CharDepthAlpha", Rigid = "CharRigid", RigidDepth = "CharRigidDepth";
}

/// <summary>
/// Character effects (GLSL 1.20 through the engine's MGFX builder). Every skinned effect reads a bone palette texture
/// (<c>CharBones</c>, slot 1, RGBA32F, row = one character: 3 texels per bone = the columns of its skin matrix, world
/// transform folded in) and adds the blended morph stream (stream 1) before skinning. Lighting uses the engine's
/// standard library (sun GGX, IBL, cascaded shadows, aerial perspective), so characters sit in the same light as the
/// terrain. PRESENTATION ONLY.
/// </summary>
public sealed class CharEffects : IEffectSource
{
    public const int SlotAlbedo = 2, SlotDetail = 3, SlotMask = 4, SlotExtra = 5;

    public IEnumerable<EffectBuilder> CreateEffects()
    {
        yield return Skinned(CharEffectNames.Skin, SkinPixel, b => b.Texture("SkinTex", SlotAlbedo).Texture("SkinDetail", SlotDetail).Texture("SkinMask", SlotMask)
            .Vec4("SkinParams", Stage.Pixel).Vec4("SkinParams2", Stage.Pixel).PixelLib(SkinLib));
        yield return Skinned(CharEffectNames.Eye, EyePixel, b => b.Texture("EyeTex", SlotAlbedo).Vec4("EyeParams", Stage.Pixel)
            .Vec4("EyeCentreL", Stage.Vertex).Vec4("EyeCentreR", Stage.Vertex), eye: true);
        yield return Skinned(CharEffectNames.Mouth, MouthPixel, b => b.Vec4("MouthParams", Stage.Both));
        yield return Skinned(CharEffectNames.Cornea, CorneaPixel, b => b.Vec4("EyeParams", Stage.Pixel));
        yield return Skinned(CharEffectNames.Hair, HairPixel, b => b.Texture("HairTex", SlotAlbedo).Vec4("HairColour", Stage.Pixel).Vec4("HairParams", Stage.Pixel));
        yield return Skinned(CharEffectNames.Lod, LodPixel, b => b.Texture("SkinTex", SlotAlbedo).Texture("Emblem", SlotDetail).PixelLib(SurfaceLib));
        yield return Skinned(CharEffectNames.Fur, FurPixel, b => b.Texture("FurTex", SlotAlbedo).Vec4("HairColour", Stage.Pixel).Vec4("FurParams", Stage.Pixel));
        yield return Skinned(CharEffectNames.Cloth, ClothPixel, b => b.Texture("Emblem", SlotAlbedo).Texture("Fabric", SlotDetail).Texture("Trim", SlotMask)
            .Vec4("ClothColour", Stage.Pixel).Vec4("ClothColour2", Stage.Pixel).Vec4("ClothColour3", Stage.Pixel).Vec4("ClothParams", Stage.Pixel)
            .Vec4("ClothParams2", Stage.Pixel).Vec4("EmblemParams", Stage.Pixel).Vec4("ChannelMask", Stage.Pixel).PixelLib(SurfaceLib), capsules: true);
        yield return Skinned(CharEffectNames.Metal, MetalPixel, b => b.Texture("Fabric", SlotDetail)
            .Vec4("MetalColour", Stage.Pixel).Vec4("MetalParams", Stage.Pixel).PixelLib(SurfaceLib), capsules: true);
        yield return Skinned(CharEffectNames.Paint, PaintPixel, b => b.Texture("Emblem", SlotAlbedo).Texture("Wood", SlotMask)
            .Vec4("PaintColour", Stage.Pixel).Vec4("PaintParams", Stage.Pixel).PixelLib(SurfaceLib));
        yield return SkinnedDepth(CharEffectNames.Depth, alpha: false);
        yield return SkinnedDepth(CharEffectNames.DepthAlpha, alpha: true);
    }

    internal static EffectBuilder Attributes(EffectBuilder b) => b
        .Attribute("vec4 a_pos", VertexElementUsage.Position, 0)
        .Attribute("vec3 a_nrm", VertexElementUsage.Normal, 0)
        .Attribute("vec4 a_tan", VertexElementUsage.Tangent, 0)
        .Attribute("vec2 a_uv", VertexElementUsage.TextureCoordinate, 0)
        .Attribute("vec4 a_bi", VertexElementUsage.BlendIndices, 0)
        .Attribute("vec4 a_bw", VertexElementUsage.BlendWeight, 0)
        .Attribute("vec4 a_col", VertexElementUsage.Color, 0)
        .Attribute("vec3 m_dpos", VertexElementUsage.TextureCoordinate, 5)
        .Attribute("vec3 m_dnrm", VertexElementUsage.TextureCoordinate, 6)
        .Vec4("CharBoneSize", Stage.Vertex).Vec4("BoneRow", Stage.Vertex)
        .Texture("CharBones", GlslLib.BoneSlot)
        .VertexLib(SkinningLib);

    public const string SkinningLib = @"
vec4 cbTexel(float joint, float k) {
    return texture2DLod(CharBones, vec2((joint * 3.0 + k + 0.5) * CharBoneSize.z, (BoneRow.x + 0.5) * CharBoneSize.w), 0.0);
}
void skinOne(float j, float w, vec4 p, vec3 n, vec3 t, inout vec3 sp, inout vec3 sn, inout vec3 st) {
    if (w <= 0.0) return;
    vec4 c0 = cbTexel(j, 0.0); vec4 c1 = cbTexel(j, 1.0); vec4 c2 = cbTexel(j, 2.0);
    sp += w * vec3(dot(c0, p), dot(c1, p), dot(c2, p));
    sn += w * vec3(dot(c0.xyz, n), dot(c1.xyz, n), dot(c2.xyz, n));
    st += w * vec3(dot(c0.xyz, t), dot(c1.xyz, t), dot(c2.xyz, t));
}
// MonoGame's GL backend normalises unsigned bytes only for the Color usage: the weights arrive as 0..255.
void skinVertex(out vec3 sp, out vec3 sn, out vec3 st) {
    vec4 p = vec4(a_pos.xyz + m_dpos, 1.0); vec3 n = a_nrm + m_dnrm; vec3 t = a_tan.xyz;
    vec4 bw = a_bw * (1.0 / 255.0);
    sp = vec3(0.0); sn = vec3(0.0); st = vec3(0.0);
    skinOne(a_bi.x, bw.x, p, n, t, sp, sn, st);
    skinOne(a_bi.y, bw.y, p, n, t, sp, sn, st);
    skinOne(a_bi.z, bw.z, p, n, t, sp, sn, st);
    skinOne(a_bi.w, bw.w, p, n, t, sp, sn, st);
}";

    /// <summary>
    /// Skirts (mesh flag 1) are pushed out of the four leg capsules (thigh and calf segments of both legs, radius in w,
    /// world space, from the posed skeleton each frame), so a striding or running leg never shows through the cloth.
    /// </summary>
    public const string CapsuleLib = @"
vec3 pushOut(vec3 p, vec4 a, vec4 b) {
    vec3 ab = b.xyz - a.xyz;
    float t = clamp(dot(p - a.xyz, ab) / max(dot(ab, ab), 1e-6), 0.0, 1.0);
    vec3 q = a.xyz + ab * t; vec3 d = p - q; float r = mix(a.w, b.w, t); float l = length(d);
    if (l < r && l > 1e-5) p = q + d * (r / l);
    return p;
}";

    static EffectBuilder Skinned(string name, string pixel, Func<EffectBuilder, EffectBuilder> extra, bool eye = false, bool capsules = false)
    {
        var b = Attributes(new EffectBuilder(name)).AddStandardLit()
            .Varyings("vec3 v_wpos; vec3 v_nrm; vec4 v_tan; vec2 v_uv; vec4 v_col; vec4 v_lpos; vec3 v_bind; vec3 v_bnrm;" + (eye ? " vec3 v_fwd; vec3 v_local;" : ""))
            .VertexLib(GlslLib.ShadowLookup);
        if (capsules)
        {
            b = b.Vec4("CapsuleOn", Stage.Vertex).VertexLib(CapsuleLib);
            for (int i = 0; i < 4; i++) b = b.Vec4("LegA" + i, Stage.Vertex).Vec4("LegB" + i, Stage.Vertex);
        }
        b = extra(b);
        string vs = @"
    vec3 sp; vec3 sn; vec3 st;
    skinVertex(sp, sn, st);" + (capsules ? @"
    if (CapsuleOn.x > 0.5) { sp = pushOut(sp, LegA0, LegB0); sp = pushOut(sp, LegA1, LegB1); sp = pushOut(sp, LegA2, LegB2); sp = pushOut(sp, LegA3, LegB3); }" : "") + @"
    v_wpos = sp; v_nrm = sn; v_tan = vec4(st, a_tan.w); v_uv = a_uv; v_col = a_col; v_bind = a_pos.xyz + m_dpos; v_bnrm = a_nrm;
    v_lpos = shadowLookup(sp, sn);
    gl_Position = ViewProj_mul(vec4(sp, 1.0));";
        if (eye) vs += @"
    float isL = step(abs(a_bi.x - EyeCentreL.w), 0.5);
    vec3 c = mix(EyeCentreR.xyz, EyeCentreL.xyz, isL);
    v_local = (a_pos.xyz - c);
    vec3 fp; vec3 fn; vec3 ft;
    fp = vec3(0.0); fn = vec3(0.0); ft = vec3(0.0);
    skinOne(a_bi.x, 1.0, vec4(0.0, 0.0, 1.0, 0.0), vec3(0.0, 0.0, 1.0), vec3(0.0), fp, fn, ft);
    v_fwd = fn;";
        return b.VertexMain(vs).PixelMain(pixel);
    }

    static EffectBuilder SkinnedDepth(string name, bool alpha)
    {
        var b = Attributes(new EffectBuilder(name)).Matrix("LightViewProj", Stage.Vertex).Varyings("float v_depth; vec2 v_uv;");
        if (alpha) b = b.Texture("HairTex", SlotAlbedo);
        return b.VertexMain(@"
    vec3 sp; vec3 sn; vec3 st;
    skinVertex(sp, sn, st);
    v_uv = a_uv;
    gl_Position = LightViewProj_mul(vec4(sp, 1.0)); v_depth = gl_Position.z / gl_Position.w;")
            .PixelMain(alpha ? @"
    if (texture2D(HairTex, v_uv).a < 0.5) discard;
    gl_FragColor = vec4(v_depth, 0.0, 0.0, 1.0);" : "    gl_FragColor = vec4(v_depth, 0.0, 0.0, 1.0);");
    }

    /// <summary>
    /// Skin: dual-lobe GGX specular (F0 0.028; second lobe at 0.55 x roughness, weight 0.15) on a micro-detail normal;
    /// subsurface approximation by per-channel wrapped diffuse on progressively smoother normals (red scatters
    /// furthest), red-shifted shadow edges, and back-light transmission through thin parts (ears, nostrils).
    /// </summary>
    public const string SkinLib = @"
vec3 shadeSkin(vec3 albedo, vec3 N, vec3 Ng, float rough, float sss, float ao, float shadow, vec3 wpos, float thin, float specMul) {
    vec3 V = normalize(CameraPos.xyz - wpos);
    vec3 L = SunDir.xyz;
    vec3 Nr = normalize(mix(N, Ng, 0.65)); vec3 Ngr = normalize(mix(N, Ng, 0.3));
    vec3 ndl = vec3(dot(Nr, L), dot(Ngr, L), dot(N, L));
    vec3 w = sss * vec3(0.42, 0.16, 0.10);
    vec3 diff = clamp((ndl + w) / (1.0 + w), 0.0, 1.0);
    vec3 sh = vec3(pow(shadow, 0.45), pow(shadow, 0.8), shadow);
    vec3 direct = albedo / PI * SunColor.rgb * diff * sh;
    // specular, two lobes
    float nol = max(dot(N, L), 0.0); float nov = max(dot(N, V), 1e-4);
    vec3 H = normalize(V + L); float noh = max(dot(N, H), 0.0); float voh = max(dot(V, H), 0.0);
    float r1 = clamp(rough, 0.08, 1.0); float r2 = clamp(rough * 0.55, 0.06, 1.0);
    float a1 = r1 * r1; float a2 = r2 * r2;
    vec3 F = fSchlick(vec3(0.028), voh);
    float lobes = 0.85 * dGgx(noh, a1) * vSmithGgx(nov, nol, a1) + 0.15 * dGgx(noh, a2) * vSmithGgx(nov, nol, a2);
    vec3 spec = F * lobes * SunColor.rgb * (nol * shadow) * specMul;
    // ambient
    vec3 irr; vec3 pre; vec3 R = reflect(-V, N);
    if (LightModel.y > 0.5) { irr = shIrradiance(Nr) * IblParams.y; pre = iblSpecular(R, r1) * IblParams.z; }
    else { irr = mix(AmbientGround.rgb, AmbientSky.rgb, Nr.y * 0.5 + 0.5) * PI; pre = mix(AmbientGround.rgb, AmbientSky.rgb, R.y * 0.5 + 0.5); }
    vec2 dfg = envBrdf(nov, r1);
    float so = clamp(pow(nov + ao, exp2(-16.0 * a1 - 1.0)) - 1.0 + ao, 0.0, 1.0);
    float skyVis = mix(0.8, 1.0, shadow);
    vec3 amb = albedo * irr / PI * ao * skyVis + pre * (0.028 * dfg.x + dfg.y) * so * specMul * skyVis;
    // transmission through thin parts, lit from behind
    float back = pow(clamp(dot(V, -L), 0.0, 1.0), 4.0) * clamp(1.0 - ndl.r, 0.0, 1.0);
    vec3 trans = thin * back * albedo * vec3(1.0, 0.32, 0.18) * SunColor.rgb * 0.35;
    return direct + spec + amb + trans;
}";

    const string SkinPixel = @"
    vec4 alb = texture2D(SkinTex, v_uv);
    vec4 mk = texture2D(SkinMask, v_uv);
    vec3 albedo = srgbToLinear(alb.rgb);
    vec3 Ng = normalize(v_nrm);
    vec3 T = normalize(v_tan.xyz - Ng * dot(Ng, v_tan.xyz)); vec3 B = cross(Ng, T) * v_tan.w;
    vec3 dn = texture2D(SkinDetail, v_uv * SkinParams2.x).xyz * 2.0 - 1.0;
    dn.xy *= SkinParams.w * (1.0 - 0.6 * mk.r);
    vec3 N = normalize(T * dn.x + B * dn.y + Ng * max(dn.z, 0.2));
    // mouth interior: a deep, occluded cavity (warm red-brown, never lit by the blue sky)
    float mouth = mk.b;
    albedo = mix(albedo, vec3(0.30, 0.075, 0.06), mouth * 0.85);
    float ao = mix(1.0, 0.06, mouth) * mix(1.0, alb.a, SkinParams2.y);
    // roughness: lips smoother, a little oilier at nose and forehead (mask.g holds the oily zone)
    float rough = SkinParams.y - 0.12 * mk.r - 0.06 * mk.g;
    // trust the smooth wrapped terminator where the surface turns from the sun; the shadow map only where N.L > 0
    float shadow = mix(1.0, shadowPcf(v_lpos), smoothstep(0.0, 0.3, dot(Ng, SunDir.xyz)));
    vec3 c = shadeSkin(albedo, N, Ng, rough, SkinParams.x, ao, shadow, v_wpos, mk.a, 1.0 - 0.7 * mouth);
    gl_FragColor = vec4(applyFog(c, v_wpos), 1.0);";

    /// <summary>
    /// Eye: iris parallax under the cornea (refraction approximated by a view-dependent shift of the iris texture),
    /// limbal darkening, socket occlusion (the eyeball darkens away from the optical axis and under the upper lid), a
    /// rough-ish sclera and a wet cornea layer (GGX 0.04 plus a sharp environment reflection).
    /// </summary>
    const string EyePixel = @"
    vec3 Ng = normalize(v_nrm);
    vec3 V = normalize(CameraPos.xyz - v_wpos);
    vec3 lp = v_local / EyeParams.x;                     // unit eyeball coordinates, +Z = optical axis (rest)
    float r = length(lp.xy);
    float corneaZone = smoothstep(0.62, 0.48, r) * step(0.0, lp.z);
    vec3 T = normalize(v_tan.xyz - Ng * dot(Ng, v_tan.xyz)); vec3 B = cross(Ng, T) * v_tan.w;
    vec2 off = vec2(dot(V, T), dot(V, B)) / max(dot(V, Ng), 0.25);
    vec2 uv = v_uv - off * EyeParams.y * corneaZone;
    vec3 base = srgbToLinear(texture2D(EyeTex, uv).rgb);
    float limbal = 1.0 - 0.45 * smoothstep(0.30, 0.50, r) * smoothstep(0.62, 0.50, r) * step(0.0, lp.z);
    base *= limbal;
    float socket = smoothstep(-0.35, 0.75, lp.z) * (1.0 - 0.45 * smoothstep(0.05, 0.75, lp.y)) * (1.0 - 0.2 * smoothstep(0.2, 0.9, -lp.y));
    float shadow = shadowPcf(v_lpos);
    vec3 c = shadePbrFull(base, EyeParams.w, 0.0, socket, Ng, Ng, v_wpos, shadow * mix(0.55, 1.0, socket));
    gl_FragColor = vec4(applyFog(c, v_wpos), 1.0);";

    /// <summary>
    /// Cornea and tear film over the iris (the CC0 proxy's clear shell), drawn after the opaque parts with additive
    /// blending: specular only (sun GGX at the cornea roughness plus a sharp environment reflection, Schlick F0 0.025).
    /// </summary>
    const string CorneaPixel = @"
    vec3 Ng = normalize(v_nrm);
    vec3 V = normalize(CameraPos.xyz - v_wpos);
    vec3 L = SunDir.xyz; vec3 H = normalize(V + L);
    float nol = max(dot(Ng, L), 0.0); float nov = max(dot(Ng, V), 1e-4); float noh = max(dot(Ng, H), 0.0);
    float a = EyeParams.z * EyeParams.z;
    float shadow = shadowPcf(v_lpos);
    vec3 Fc = fSchlick(vec3(0.025), max(dot(V, H), 0.0));
    vec3 c = Fc * dGgx(noh, a) * vSmithGgx(nov, nol, a) * SunColor.rgb * nol * shadow;
    vec3 Rr = reflect(-V, Ng);
    vec3 env = (LightModel.y > 0.5) ? iblSpecular(Rr, EyeParams.z) * IblParams.z : mix(AmbientGround.rgb, AmbientSky.rgb, Rr.y * 0.5 + 0.5);
    vec2 dfg = envBrdf(nov, EyeParams.z);
    // the brow and lids shade the upper part of the reflection
    float lid = 1.0 - 0.6 * clamp(Rr.y, 0.0, 1.0);
    c += env * (0.025 * dfg.x + dfg.y) * lid * mix(0.5, 1.0, shadow);
    gl_FragColor = vec4(max(applyFog(c, v_wpos) - applyFog(vec3(0.0), v_wpos), vec3(0.0)), 1.0);";

    /// <summary>
    /// Hair cards: hashed alpha test (a per-pixel random threshold that changes every accumulated still sample, so
    /// jittered stills resolve soft strand edges; the shadow pass uses a fixed 0.5 test), per-strand colour variation,
    /// root-to-tip shading and layer occlusion, Kajiya-Kay style primary (shifted towards the root, white) and secondary
    /// (shifted towards the tip, tinted) highlights along the strand direction, sky light from the SH irradiance.
    /// </summary>
    const string HairPixel = @"
    vec4 t = texture2D(HairTex, v_uv);
    float thr = HairParams.x > 0.5 ? 0.5 : fract(sin(dot(gl_FragCoord.xy + HairColour.a * vec2(37.0, 17.0), vec2(12.9898, 78.233))) * 43758.5453);
    thr = mix(0.08, 0.92, thr);
    if (t.a * HairParams.w < thr) discard;
    vec3 Ng = normalize(v_nrm);
    vec3 Tt = normalize(v_tan.xyz - Ng * dot(Ng, v_tan.xyz));
    vec3 S = normalize(cross(Ng, Tt) * v_tan.w);   // along the strand (root -> tip)
    vec3 V = normalize(CameraPos.xyz - v_wpos);
    if (dot(Ng, V) < 0.0) Ng = -Ng;
    vec3 L = SunDir.xyz;
    float layer = v_col.a;
    vec3 albedo = HairColour.rgb * (0.86 + 0.28 * t.r) * (0.78 + 0.22 * t.g) * mix(0.7, 1.0, layer);
    float shadow = shadowPcf(v_lpos);
    float tl = dot(S, L);
    float diff = sqrt(max(1.0 - tl * tl, 0.0)) * clamp(dot(Ng, L) * 0.5 + 0.6, 0.0, 1.0);
    vec3 H = normalize(L + V);
    float sh = (t.b - 0.5) * 0.25;
    vec3 T1 = normalize(S + Ng * (HairParams.y + sh));
    vec3 T2 = normalize(S + Ng * (HairParams.y - 0.3 + sh));
    float th1 = dot(T1, H); float th2 = dot(T2, H);
    float s1 = pow(sqrt(max(1.0 - th1 * th1, 0.0)), HairParams.z);
    float s2 = pow(sqrt(max(1.0 - th2 * th2, 0.0)), HairParams.z * 0.35) * (0.6 + 0.8 * t.r);
    float vis = smoothstep(-0.1, 0.25, dot(Ng, L) + 0.3);
    vec3 spec = (vec3(0.022) * s1 + albedo * 0.18 * s2) * vis;
    vec3 c = SunColor.rgb * shadow * (albedo / PI * diff + spec);
    vec3 irr = (LightModel.y > 0.5) ? shIrradiance(Ng) * IblParams.y : mix(AmbientGround.rgb, AmbientSky.rgb, Ng.y * 0.5 + 0.5) * PI;
    c += albedo * irr / PI * mix(0.35, 1.0, layer) * mix(0.75, 1.0, shadow);
    gl_FragColor = vec4(applyFog(c, v_wpos), 1.0);";

    /// <summary>
    /// Merged LOD meshes (one material): vertex colour = albedo, alpha = surface code (0..99 dielectric roughness,
    /// 128..227 metal roughness, 255 skin from the identity's skin texture through the UVs, with a light wrap for the
    /// subsurface look at distance).
    /// </summary>
    const string LodPixel = @"
    vec3 Ng = normalize(v_nrm);
    float code = floor(v_col.a * 255.0 + 0.5);
    vec3 albedo; float rough; float metal = 0.0;
    if (code > 254.5) { albedo = srgbToLinear(texture2D(SkinTex, v_uv).rgb); rough = 0.5; }
    else if (code > 253.5) { albedo = srgbToLinear(texture2D(Emblem, v_uv).rgb); rough = 0.62; }
    else {
        albedo = srgbToLinear(v_col.rgb);
        if (code > 127.5) { metal = 1.0; rough = (code - 128.0) / 99.0; } else rough = code / 99.0;
    }
    float shadow = shadowPcf(v_lpos);
    vec3 c = shadeSurface(albedo, rough, metal, 1.0, Ng, Ng, v_wpos, shadow, code > 254.5 ? 0.0 : 0.3);
    if (code > 254.5) c += albedo * vec3(0.10, 0.03, 0.02) * SunColor.rgb * clamp(1.0 - abs(dot(Ng, SunDir.xyz)), 0.0, 1.0) * shadow;
    gl_FragColor = vec4(applyFog(c, v_wpos), 1.0);";

    /// <summary>
    /// Fur shells (beards, stubble): layer fraction in vertex colour A, region density in R, strand direction in the
    /// tangent. A strand of the tiling cross-section texture shows on a shell while its length exceeds the shell's
    /// fraction (shorter and sparser where the density falls off, so a beard edge thins out strand by strand); hashed
    /// alpha resolves soft edges over the jittered still samples. Kajiya-Kay shading along the strand, inner shells
    /// darker (self-shadowing towards the skin), sky light from the SH irradiance.
    /// </summary>
    const string FurPixel = @"
    vec4 t = texture2D(FurTex, v_uv * FurParams.x);
    float frac = v_col.a;
    float dens = v_col.r;
    float len = t.r * smoothstep(0.02, 0.55, dens);
    float a = t.b * smoothstep(frac - 0.12, frac, len);
    float thr = fract(sin(dot(gl_FragCoord.xy + HairColour.a * vec2(37.0, 17.0), vec2(12.9898, 78.233))) * 43758.5453);
    if (a < mix(0.1, 0.9, thr)) discard;
    vec3 Ng = normalize(v_nrm);
    vec3 S = normalize(v_tan.xyz);
    vec3 V = normalize(CameraPos.xyz - v_wpos);
    vec3 L = SunDir.xyz;
    vec3 albedo = HairColour.rgb * FurParams.y * (0.80 + 0.40 * t.g) * mix(0.40, 1.0, frac);
    float shadow = shadowPcf(v_lpos);
    float tl = dot(S, L);
    float diff = sqrt(max(1.0 - tl * tl, 0.0)) * clamp(dot(Ng, L) * 0.5 + 0.55, 0.0, 1.0);
    vec3 H = normalize(L + V);
    float th = dot(normalize(S + Ng * 0.1), H);
    float s1 = pow(sqrt(max(1.0 - th * th, 0.0)), 60.0);
    float th2 = dot(normalize(S - Ng * 0.2), H);
    float s2 = pow(sqrt(max(1.0 - th2 * th2, 0.0)), 20.0);
    vec3 spec = (vec3(0.012) * s1 + albedo * 0.12 * s2) * smoothstep(-0.1, 0.25, dot(Ng, L) + 0.3);
    vec3 c = SunColor.rgb * shadow * (albedo / PI * diff + spec);
    vec3 irr = (LightModel.y > 0.5) ? shIrradiance(Ng) * IblParams.y : mix(AmbientGround.rgb, AmbientSky.rgb, Ng.y * 0.5 + 0.5) * PI;
    c += albedo * irr / PI * mix(0.35, 1.0, frac) * mix(0.75, 1.0, shadow);
    gl_FragColor = vec4(applyFog(c, v_wpos), 1.0);";

    /// <summary>
    /// Shared surface helpers: triplanar sampling in bind space (the pattern sticks to the cloth as it moves),
    /// derivative bump mapping (Mikkelsen 2010: no UVs or tangents needed on generated garments), cloth sheen and a
    /// ground-bounce reflection for metals (the sky IBL alone mirrors only blue sky and dark ground into armour).
    /// </summary>
    public const string SurfaceLib = @"
vec4 triplanar(sampler2D t, vec3 p, vec3 n) {
    vec3 w = pow(abs(n), vec3(4.0)); w /= (w.x + w.y + w.z + 1e-5);
    return texture2D(t, p.zy) * w.x + texture2D(t, p.xz) * w.y + texture2D(t, p.xy) * w.z;
}
vec3 bumpNormal(vec3 N, vec3 wp, float h, float strength) {
    vec3 dpdx = dFdx(wp); vec3 dpdy = dFdy(wp);
    float dhdx = dFdx(h); float dhdy = dFdy(h);
    vec3 r1 = cross(dpdy, N); vec3 r2 = cross(N, dpdx);
    float det = dot(dpdx, r1);
    vec3 grad = sign(det) * (dhdx * r1 + dhdy * r2);
    return normalize(abs(det) * N - strength * grad);
}
float quilt(vec3 p, vec3 n, float k) {
    vec3 w = pow(abs(n), vec3(4.0)); w /= (w.x + w.y + w.z + 1e-5);
    vec2 a = p.zy * k; vec2 b = p.xz * k; vec2 c = p.xy * k;
    float qa = min(abs(fract(a.x + a.y) - 0.5), abs(fract(a.x - a.y) - 0.5));
    float qb = min(abs(fract(b.x + b.y) - 0.5), abs(fract(b.x - b.y) - 0.5));
    float qc = min(abs(fract(c.x + c.y) - 0.5), abs(fract(c.x - c.y) - 0.5));
    float q = qa * w.x + qb * w.y + qc * w.z;
    return smoothstep(0.0, 0.12, q);
}
// environment term with a lit ground below the horizon (lit soil/grass, albedo about 0.15)
vec3 envReflect(vec3 R, float rough) {
    vec3 sky = (LightModel.y > 0.5) ? iblSpecular(R, rough) * IblParams.z : mix(AmbientGround.rgb, AmbientSky.rgb, R.y * 0.5 + 0.5);
    // ARTISTIC (labelled): the clear analytic sky is a saturated blue dome with no clouds or surroundings; mirrored
    // in curved armour it reads as blue paint. Take 35 % of the colour out of reflected sky light.
    // A clear analytic sky is far bluer than the cloud- and haze-filled skies armour usually mirrors: keep 40 % of the
    // reflected sky's chroma, and lift the horizon band towards the pale haze colour.
    float sl = dot(sky, vec3(0.2126, 0.7152, 0.0722));
    sky = mix(sky, vec3(sl), 0.6);
    sky = mix(sky, vec3(sl) * vec3(1.04, 1.0, 0.95), 0.5 * (1.0 - smoothstep(0.0, 0.35, R.y)));
    vec3 ground = AmbientGround.rgb * PI * 0.9;
    float below = smoothstep(0.05, -0.25, R.y);
    return mix(sky, ground, below);
}
vec3 shadeSurface(vec3 albedo, float rough, float metal, float ao, vec3 N, vec3 Ng, vec3 wpos, float shadow, float sheen) {
    rough = clamp(rough, 0.05, 1.0);
    vec3 V = normalize(CameraPos.xyz - wpos);
    if (dot(N, V) < 0.0) { N = -N; Ng = -Ng; }
    float nov = max(dot(N, V), 1e-4);
    vec3 L = SunDir.xyz; vec3 H = normalize(V + L);
    float nol = max(dot(N, L), 0.0); float noh = max(dot(N, H), 0.0); float voh = max(dot(V, H), 0.0);
    float a = rough * rough;
    vec3 f0 = mix(vec3(0.04), albedo, metal);
    vec3 dc = albedo * (1.0 - metal);
    vec3 F = fSchlick(f0, voh);
    vec3 direct = (dc / PI * (vec3(1.0) - F) + F * (dGgx(noh, a) * vSmithGgx(nov, nol, a))) * SunColor.rgb * (nol * shadow);
    // cloth sheen: grazing retro-reflection of fibres
    direct += sheen * dc * pow(1.0 - nov, 4.0) * SunColor.rgb * nol * shadow * 0.6;
    vec3 irr = (LightModel.y > 0.5) ? shIrradiance(N) * IblParams.y : mix(AmbientGround.rgb, AmbientSky.rgb, N.y * 0.5 + 0.5) * PI;
    vec3 R = reflect(-V, N);
    vec2 dfg = envBrdf(nov, rough);
    float so = clamp(pow(nov + ao, exp2(-16.0 * a - 1.0)) - 1.0 + ao, 0.0, 1.0);
    float skyVis = mix(0.8, 1.0, shadow);
    vec3 amb = dc * irr / PI * ao * skyVis + envReflect(R, rough) * (f0 * dfg.x + vec3(dfg.y)) * so * skyVis;
    return direct + amb;
}";

    /// <summary>
    /// Garment cloth: linen, wool, leather, felt or quilted padding; the weave or grain is sampled triplanar in bind
    /// space and turned into a bump; trims (vertex colour R near each hem) take the second colour; a painted emblem
    /// can be projected on the chest. Sheen at grazing angles.
    /// </summary>
    const string ClothPixel = @"
    vec3 Ng = normalize(v_nrm);
    vec3 bn = normalize(v_bnrm);
    vec3 p = v_bind * ClothParams.y;
    float fid = ClothParams.x;
    vec4 tx = triplanar(Fabric, p, bn);
    float h = dot(tx, ChannelMask);
    if (fid > 3.5) h = 0.65 * quilt(v_bind, bn, 16.0) + 0.35 * tx.r;
    // fade the weave bump once a thread is smaller than a pixel (no shimmer, no speckle at distance)
    float fp = length(fwidth(p)) * 32.0;
    float bumpFade = 1.0 - smoothstep(0.35, 0.9, fp);
    if (fid > 3.5) bumpFade = 1.0 - smoothstep(0.6, 1.4, length(fwidth(v_bind)) * 16.0);
    h = mix(0.5, h, bumpFade);
    float bump = ClothParams.z * bumpFade;
    vec4 low = triplanar(Fabric, v_bind * 1.3, bn);      // broad dye unevenness and fading
    vec3 albedo = ClothColour.rgb * (0.82 + 0.36 * h) * (0.90 + 0.20 * tx.a) * (0.92 + 0.16 * low.a);
    // woven border (vertex colour R): tablet-woven pattern in the second colour on a darker ground, edge cords
    if (v_col.r > 0.5 && ClothColour2.a > 0.5) {
        vec2 tuv = vec2(v_uv.x, (ClothParams2.x + clamp(v_uv.y, 0.03, 0.97)) * 0.25);
        vec4 tp = texture2D(Trim, tuv);
        vec3 c2 = ClothColour2.rgb;
        vec3 ground = mix(ClothColour.rgb * 0.6, c2 * 0.35, 0.4);
        albedo = mix(ground, c2 * 0.85, tp.r) * (0.80 + 0.35 * tp.g);
        albedo = mix(albedo, c2 * 0.55, tp.b * 0.7);
        h = tp.g;
        bump = 0.0006 * (1.0 - smoothstep(0.6, 1.4, length(fwidth(v_uv)) * 48.0));
    }
    // soles (vertex colour B on footwear)
    if (ClothColour3.a > 0.5) albedo = mix(albedo, ClothColour3.rgb, step(0.5, v_col.b));
    if (EmblemParams.w > 0.5 && bn.z > 0.25) {
        vec2 euv = vec2((v_bind.x - EmblemParams.x) / EmblemParams.z + 0.5, (EmblemParams.y - v_bind.y) / EmblemParams.z + 0.5);
        if (euv.x > 0.0 && euv.x < 1.0 && euv.y > 0.0 && euv.y < 1.0) {
            vec4 e = texture2D(Emblem, euv);
            albedo = mix(albedo, srgbToLinear(e.rgb) * (0.88 + 0.24 * h), e.a * smoothstep(0.25, 0.45, bn.z));
        }
    }
    // field wear: mud and dust on the lower garments (rising with ClothParams2.y), sun-faded upper surfaces
    // spatter rather than streaks: fine noise, below the knee, weaker on pale cloth (where a dark stain would dominate)
    float spat = smoothstep(0.55, 0.85, tx.a * 0.5 + low.a * 0.2 + triplanar(Fabric, v_bind * 9.0, bn).a * 0.3);
    float mud = ClothParams2.y * smoothstep(0.42, 0.06, v_bind.y) * spat;
    float pale = smoothstep(0.25, 0.6, dot(ClothColour.rgb, vec3(0.2126, 0.7152, 0.0722)));
    albedo = mix(albedo, vec3(0.085, 0.070, 0.052), mud * mix(0.55, 0.25, pale));
    albedo *= 1.0 + 0.07 * smoothstep(0.4, 0.9, bn.y) * smoothstep(1.1, 1.5, v_bind.y);
    vec3 N = bumpNormal(Ng, v_wpos, h, bump);
    float ao = mix(0.7, 1.0, h) * (v_col.g);
    float rough = clamp(ClothColour.a + 0.06 * mud, 0.05, 1.0);
    float sheen = ClothParams.w;
    vec3 c = shadeSurface(albedo, rough, 0.0, ao, N, Ng, v_wpos, shadowPcf(v_lpos), sheen);
    gl_FragColor = vec4(applyFog(c, v_wpos), 1.0);";

    /// <summary>
    /// Armour metal (iron, steel, bronze, gilt, riveted mail): noise-driven roughness and dirt in the low spots, rust
    /// on iron or patina on bronze by the wear amount, hammer-mark bump, mail rings from the fabric texture with dark
    /// gaps (the padding below). Reflections take a lit ground below the horizon, so iron does not read blue.
    /// </summary>
    const string MetalPixel = @"
    vec3 Ng = normalize(v_nrm);
    vec3 bn = normalize(v_bnrm);
    vec4 nz = triplanar(Fabric, v_bind * 3.0, bn);
    vec4 fine = triplanar(Fabric, v_bind * 11.0, bn);
    vec4 micro = triplanar(Fabric, v_bind * 37.0, bn);
    float mail = MetalParams.z;
    float rings = triplanar(Fabric, v_bind * MetalParams.w, bn).b;
    // rings smaller than about a pixel turn into their average (a darker, rougher grey) instead of noise
    float ringFade = 1.0 - smoothstep(0.3, 0.8, length(fwidth(v_bind)) * MetalParams.w * 16.0);
    rings = mix(0.45, rings, ringFade);
    float hammer = (nz.a - 0.5) * 0.7 + (fine.a - 0.5) * 0.3;
    float hammerFade = 1.0 - smoothstep(0.3, 0.8, length(fwidth(v_bind)) * 11.0 * 64.0);
    float hh = mix(hammer * MetalParams.y, rings, mail);
    vec3 N = bumpNormal(Ng, v_wpos, hh, mix(0.0010 * hammerFade, 0.0016 * ringFade, mail));
    float wear = MetalParams.x;
    vec3 f0 = MetalColour.rgb;
    // tarnish: broad, low-contrast darkening; oxide specks (rust on iron and steel, verdigris on bronze) only where
    // the wear is high. No blotchy camouflage: armour in the field is oiled and scoured.
    float tarnish = wear * smoothstep(0.40, 0.80, nz.a) * 0.55;
    float specks = wear * smoothstep(0.80, 0.90, fine.a * 0.65 + micro.a * 0.35);
    vec3 oxide = (f0.r > f0.b * 1.4) ? vec3(0.10, 0.16, 0.12) : vec3(0.19, 0.085, 0.04);
    vec3 albedo = mix(f0, f0 * 0.6, tarnish);
    albedo = mix(albedo, oxide, specks);
    float metal = 1.0 - specks * 0.9;
    float rough = clamp(MetalColour.a + 0.10 * (fine.a - 0.5) + 0.06 * (micro.a - 0.5) + 0.22 * tarnish + 0.45 * specks, 0.08, 1.0);
    float gap = mail * mix(0.45, smoothstep(0.42, 0.08, rings), ringFade);
    albedo *= 1.0 - 0.25 * mail;
    albedo = mix(albedo, vec3(0.03, 0.025, 0.02), gap);
    metal *= 1.0 - gap;
    rough = mix(rough, 0.9, gap);
    float ao = 1.0 - 0.5 * gap;
    vec3 c = shadeSurface(albedo, rough, metal, ao, N, Ng, v_wpos, shadowPcf(v_lpos), 0.0);
    gl_FragColor = vec4(applyFog(c, v_wpos), 1.0);";

    /// <summary>Painted shield faces (emblem texture with planar UVs, chipped to the wood under it) and plain wood.</summary>
    const string PaintPixel = @"
    vec3 Ng = normalize(v_nrm);
    vec4 wd = texture2D(Wood, v_uv * vec2(1.0, PaintParams.w));
    vec3 woodCol = PaintColour.rgb * (0.65 + 0.6 * wd.g);
    float h = wd.r;
    vec3 albedo = woodCol; float rough = PaintColour.a;
    // planks: the shield is boards glued edge to edge (four across the face); seams show as fine grooves
    float seam = smoothstep(0.475, 0.497, abs(fract(v_uv.x * 4.0) - 0.5));
    if (PaintParams.x < 0.5) {
        vec4 e = texture2D(Emblem, v_uv);
        float rim = v_col.r;
        // paint chips: few in the field, many towards the rim where the shield takes blows and scrapes
        float chip = smoothstep(0.72, 0.80, wd.b + 0.22 * smoothstep(0.80, 1.0, rim)) * PaintParams.z;
        vec3 paint = srgbToLinear(e.rgb) * (0.90 + 0.14 * wd.g) * (1.0 - 0.18 * smoothstep(0.85, 1.0, rim));
        albedo = mix(paint, woodCol, chip);
        rough = mix(0.62, PaintColour.a, chip);
        h = mix(h * 0.12, h, chip);
    }
    albedo *= 1.0 - 0.35 * seam;
    h -= 0.6 * seam;
    vec3 N = bumpNormal(Ng, v_wpos, h, PaintParams.y);
    vec3 c = shadeSurface(albedo, rough, 0.0, 1.0 - 0.3 * seam, N, Ng, v_wpos, shadowPcf(v_lpos), 0.0);
    gl_FragColor = vec4(applyFog(c, v_wpos), 1.0);";

    /// <summary>Teeth, gums and tongue: vertex colour, wet roughness, occlusion growing towards the back of the mouth.</summary>
    const string MouthPixel = @"
    vec3 Ng = normalize(v_nrm);
    vec3 albedo = srgbToLinear(v_col.rgb);
    float depthIn = clamp((MouthParams.x - v_bind.z) / max(MouthParams.x - MouthParams.y, 1e-3), 0.0, 1.0);
    // the mouth cavity sees almost no sky: strong occlusion growing towards the throat, warm inter-reflection
    float ao = mix(0.55, 0.03, sqrt(depthIn)) * MouthParams.z;
    vec3 c = shadePbrFull(albedo, 0.35, 0.0, ao, Ng, Ng, v_wpos, shadowPcf(v_lpos) * ao * ao);
    c += albedo * vec3(0.9, 0.35, 0.3) * ao * 0.02 * (SunColor.r + SunColor.g + SunColor.b);
    gl_FragColor = vec4(applyFog(c, v_wpos), 1.0);";
}
