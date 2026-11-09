using Gfx.Engine.Mgfx;
using Microsoft.Xna.Framework.Graphics;

namespace Gfx.Engine.Effects;

/// <summary>Effect names shipped by Gfx.Engine (and the names other lanes are expected to register).</summary>
public static class EffectNames
{
    public const string InstancedLit = "InstancedLit";     // VertexPNC + InstanceTransform, lit, shadowed, fogged
    public const string InstancedDepth = "InstancedDepth"; // shadow-depth variant
    public const string SkinnedLit = "SkinnedLit";         // VertexSkinned + SkinnedInstance, bone texture via VTF
    public const string SkinnedDepth = "SkinnedDepth";
    public const string Sky = "Sky";
    public const string Post = "Post";                     // exposure + ACES + gamma
    /// <summary>NOT shipped by the engine: lane T registers it through an <see cref="IEffectSource"/>.</summary>
    public const string TerrainSplat = "TerrainSplat";
}

/// <summary>The engine's own effect set (discovered like any other <see cref="IEffectSource"/>).</summary>
public sealed class BuiltinEffects : IEffectSource
{
    public IEnumerable<EffectBuilder> CreateEffects()
    {
        yield return Instanced(depth: false);
        yield return Instanced(depth: true);
        yield return Skinned(depth: false);
        yield return Skinned(depth: true);
        yield return Sky();
        yield return Post();
    }

    static EffectBuilder MeshAttrs(EffectBuilder b) => b
        .Attribute("vec4 a_pos", VertexElementUsage.Position, 0)
        .Attribute("vec3 a_nrm", VertexElementUsage.Normal, 0)
        .Attribute("vec4 a_col", VertexElementUsage.Color, 0)
        .AddInstanceTransform();

    const string LitPixel = @"
    vec3 n = normalize(v_nrm);
    vec3 albedo = srgbToLinear(v_col.rgb) * v_tint.rgb;
    vec3 c = shadeLit(albedo, n, shadowPcf(v_lpos));
    gl_FragColor = vec4(applyFog(c, v_wpos), 1.0);";

    const string DepthPixel = "    gl_FragColor = vec4(v_depth, 0.0, 0.0, 1.0);";

    static EffectBuilder Instanced(bool depth)
    {
        var b = MeshAttrs(new EffectBuilder(depth ? EffectNames.InstancedDepth : EffectNames.InstancedLit));
        string body = @"
    vec3 wp = instPos(vec4(a_pos.xyz, 1.0));";
        if (depth)
            return b.Matrix("LightViewProj", Stage.Vertex).Varyings("float v_depth;")
                .VertexMain(body + @"
    gl_Position = LightViewProj_mul(vec4(wp, 1.0)); v_depth = gl_Position.z / gl_Position.w;")
                .PixelMain(DepthPixel);
        return b.AddStandardLit().Varyings("vec3 v_wpos; vec3 v_nrm; vec4 v_col; vec3 v_tint; vec4 v_lpos;")
            .VertexMain(body + @"
    v_wpos = wp; v_nrm = instDir(a_nrm); v_col = a_col; v_tint = i_tint.rgb;
    v_lpos = shadowLookup(wp, v_nrm);
    gl_Position = ViewProj_mul(vec4(wp, 1.0));")
            .VertexLib(GlslLib.ShadowLookup)
            .PixelMain(LitPixel);
    }

    /// <summary>
    /// Instanced GPU skinning. Bone texture layout (see <see cref="Rendering.BoneTexture"/>): RGBA32F,
    /// width = joints × 3, one row per baked pose; texel (3j+k, row) = column k of joint j's skin matrix
    /// (same affine convention as <see cref="Rendering.InstanceTransform"/>). Per instance <c>i_anim</c>:
    /// x = pose row A, y = pose row B, z = blend A→B. Up to 4 influences (BlendIndices/BlendWeight).
    /// Vertex texture fetch uses the critic's workaround (sampler listed on the pixel stage, texture2DLod in VS).
    /// </summary>
    static EffectBuilder Skinned(bool depth)
    {
        var b = new EffectBuilder(depth ? EffectNames.SkinnedDepth : EffectNames.SkinnedLit)
            .Attribute("vec4 a_pos", VertexElementUsage.Position, 0)
            .Attribute("vec3 a_nrm", VertexElementUsage.Normal, 0)
            .Attribute("vec4 a_col", VertexElementUsage.Color, 0)
            .Attribute("vec4 a_bi", VertexElementUsage.BlendIndices, 0)
            .Attribute("vec4 a_bw", VertexElementUsage.BlendWeight, 0)
            .AddInstanceTransform()
            .Attribute("vec4 i_anim", VertexElementUsage.TextureCoordinate, 5)
            .Vec4("BoneTexSize", Stage.Vertex) // x width, y height, z 1/width, w 1/height
            .Texture("Bones", GlslLib.BoneSlot)
            .VertexLib(@"
vec4 boneTexel(float joint, float k, float row) {
    return texture2DLod(Bones, vec2((joint * 3.0 + k + 0.5) * BoneTexSize.z, (row + 0.5) * BoneTexSize.w), 0.0);
}
void skinJoint(float joint, float w, vec4 p, vec3 n, inout vec3 sp, inout vec3 sn) {
    if (w <= 0.0) return;
    vec4 c0 = mix(boneTexel(joint, 0.0, i_anim.x), boneTexel(joint, 0.0, i_anim.y), i_anim.z);
    vec4 c1 = mix(boneTexel(joint, 1.0, i_anim.x), boneTexel(joint, 1.0, i_anim.y), i_anim.z);
    vec4 c2 = mix(boneTexel(joint, 2.0, i_anim.x), boneTexel(joint, 2.0, i_anim.y), i_anim.z);
    sp += w * vec3(dot(c0, p), dot(c1, p), dot(c2, p));
    sn += w * vec3(dot(c0.xyz, n), dot(c1.xyz, n), dot(c2.xyz, n));
}");
        string body = @"
    vec4 p = vec4(a_pos.xyz, 1.0); vec3 sp = vec3(0.0); vec3 sn = vec3(0.0);
    skinJoint(a_bi.x, a_bw.x, p, a_nrm, sp, sn);
    skinJoint(a_bi.y, a_bw.y, p, a_nrm, sp, sn);
    skinJoint(a_bi.z, a_bw.z, p, a_nrm, sp, sn);
    skinJoint(a_bi.w, a_bw.w, p, a_nrm, sp, sn);
    vec3 wp = instPos(vec4(sp, 1.0));";
        if (depth)
            return b.Matrix("LightViewProj", Stage.Vertex).Varyings("float v_depth;")
                .VertexMain(body + @"
    gl_Position = LightViewProj_mul(vec4(wp, 1.0)); v_depth = gl_Position.z / gl_Position.w;")
                .PixelMain(DepthPixel);
        return b.AddStandardLit().Varyings("vec3 v_wpos; vec3 v_nrm; vec4 v_col; vec3 v_tint; vec4 v_lpos;")
            .VertexMain(body + @"
    v_wpos = wp; v_nrm = instDir(sn); v_col = a_col; v_tint = i_tint.rgb;
    v_lpos = shadowLookup(wp, v_nrm);
    gl_Position = ViewProj_mul(vec4(wp, 1.0));")
            .VertexLib(GlslLib.ShadowLookup)
            .PixelMain(LitPixel);
    }

    /// <summary>Full-screen sky: gradient + sun disc/glow + horizon haze, linear HDR. Draw first with depth off.</summary>
    static EffectBuilder Sky() => new EffectBuilder(EffectNames.Sky)
        .Attribute("vec4 a_pos", VertexElementUsage.Position, 0)
        .Matrix("InvViewProj", Stage.Vertex).Vec4("CameraPos", Stage.Vertex)
        .Vec4("SunDir", Stage.Pixel).Vec4("SunColor", Stage.Pixel)
        .Vec4("SkyZenith", Stage.Pixel).Vec4("SkyHorizon", Stage.Pixel).Vec4("AmbientGround", Stage.Pixel)
        .Varyings("vec3 v_dir;")
        .VertexMain(@"
    vec4 f = InvViewProj_mul(vec4(a_pos.xy, 1.0, 1.0));
    v_dir = f.xyz / f.w - CameraPos.xyz;
    gl_Position = vec4(a_pos.xy, 0.0, 1.0);")
        .PixelMain(@"
    vec3 d = normalize(v_dir);
    float up = clamp(d.y, 0.0, 1.0);
    vec3 sky = mix(SkyHorizon.rgb, SkyZenith.rgb, pow(up, 0.5));
    if (d.y < 0.0) sky = mix(SkyHorizon.rgb, AmbientGround.rgb, clamp(-d.y * 4.0, 0.0, 1.0));
    float s = max(dot(d, SunDir.xyz), 0.0);
    sky += SunColor.rgb * (pow(s, 2000.0) * 30.0 + pow(s, 12.0) * 0.12);
    gl_FragColor = vec4(sky, 1.0);");

    /// <summary>HDR scene texture (slot 0) → display. PostParams: x exposure, y tonemap on (1)/off (0).</summary>
    static EffectBuilder Post() => new EffectBuilder(EffectNames.Post)
        .Attribute("vec4 a_pos", VertexElementUsage.Position, 0)
        .Vec4("PostParams", Stage.Pixel)
        .Texture("SceneTex", 0)
        .Varyings("vec2 v_uv;")
        .PixelLib(GlslLib.Tonemap)
        .VertexMain(@"
    v_uv = vec2(a_pos.x * 0.5 + 0.5, -a_pos.y * 0.5 + 0.5);
    gl_Position = vec4(a_pos.xy, 0.0, 1.0);")
        .PixelMain(@"
    vec3 c = texture2D(SceneTex, v_uv).rgb * PostParams.x;
    if (PostParams.y > 0.5) c = acesFit(c);
    gl_FragColor = vec4(linearToSrgb(c), 1.0);");
}
