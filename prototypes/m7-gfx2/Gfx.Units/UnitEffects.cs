using Gfx.Engine.Effects;
using Gfx.Engine.Mgfx;
using Microsoft.Xna.Framework.Graphics;

namespace Gfx.Units;

/// <summary>
/// Lane U effect: <c>UnitSkinnedLit</c> — the engine's instanced GPU skinning (bone texture via the VTF workaround;
/// skinning GLSL copied from Gfx.Engine BuiltinEffects.Skinned so the engine stays untouched) plus material regions:
/// vertex-colour alpha selects cloth (per-instance <c>i_tint.rgb</c> × vertex shade), skin (<c>i_tint.a</c> tone
/// 0 light → 1 dark) or fixed albedo. Vertex/instance layouts = <c>VertexSkinned</c> + <c>SkinnedInstance</c>, so the
/// engine's <c>SkinnedDepth</c> serves as the shadow-pass effect.
/// </summary>
public sealed class UnitEffects : IEffectSource
{
    public const string UnitSkinnedLit = "UnitSkinnedLit";

    const string SkinLib = @"
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
}";

    public IEnumerable<EffectBuilder> CreateEffects()
    {
        yield return new EffectBuilder(UnitSkinnedLit)
            .Attribute("vec4 a_pos", VertexElementUsage.Position, 0)
            .Attribute("vec3 a_nrm", VertexElementUsage.Normal, 0)
            .Attribute("vec4 a_col", VertexElementUsage.Color, 0)
            .Attribute("vec4 a_bi", VertexElementUsage.BlendIndices, 0)
            .Attribute("vec4 a_bw", VertexElementUsage.BlendWeight, 0)
            .AddInstanceTransform()
            .Attribute("vec4 i_anim", VertexElementUsage.TextureCoordinate, 5)
            .Vec4("BoneTexSize", Stage.Vertex)
            .Texture("Bones", GlslLib.BoneSlot)
            .VertexLib(SkinLib)
            .AddStandardLit()
            .VertexLib(GlslLib.ShadowLookup)
            .Varyings("vec3 v_wpos; vec3 v_nrm; vec4 v_col; vec4 v_tint; vec4 v_lpos;")
            .VertexMain(@"
    vec4 p = vec4(a_pos.xyz, 1.0); vec3 sp = vec3(0.0); vec3 sn = vec3(0.0);
    skinJoint(a_bi.x, a_bw.x, p, a_nrm, sp, sn);
    skinJoint(a_bi.y, a_bw.y, p, a_nrm, sp, sn);
    skinJoint(a_bi.z, a_bw.z, p, a_nrm, sp, sn);
    skinJoint(a_bi.w, a_bw.w, p, a_nrm, sp, sn);
    vec3 wp = instPos(vec4(sp, 1.0));
    v_wpos = wp; v_nrm = instDir(sn); v_col = a_col; v_tint = i_tint;
    v_lpos = shadowLookup(wp, v_nrm);
    gl_Position = ViewProj_mul(vec4(wp, 1.0));")
            .PixelMain(@"
    vec3 n = normalize(v_nrm);
    vec3 albedo;
    if (v_col.a > 0.75) albedo = srgbToLinear(v_col.rgb);
    else if (v_col.a > 0.25) albedo = srgbToLinear(mix(vec3(0.93, 0.76, 0.62), vec3(0.33, 0.21, 0.13), v_tint.a)) * v_col.rgb;
    else albedo = srgbToLinear(v_tint.rgb) * v_col.rgb;
    vec3 c = shadeLit(albedo, n, shadowPcf(v_lpos));
    gl_FragColor = vec4(applyFog(c, v_wpos), 1.0);");
    }
}
