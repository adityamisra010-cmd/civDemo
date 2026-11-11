using Gfx.Chars.Rendering;
using Gfx.Engine.Effects;
using Gfx.Engine.Mgfx;
using Gfx.Engine.Rendering;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;

namespace Gfx.Gear.Runtime;

/// <summary>Effect names of stage kit.</summary>
public static class KitEffectNames
{
    public const string Kit = "CharKit";
}

/// <summary>
/// The era-kit material: GPU-skinned like every character part (bone palette, morph stream), shaded from three maps
/// (base colour sRGB, tangent-space normal with green up, ORM = occlusion/roughness/metallic as in glTF) with the
/// stage-chars surface model (GGX sun with cascaded shadows, sky or HDRI image-based light, the desaturated-sky metal
/// reflection, cloth sheen) and aerial perspective. Optional hashed alpha test for cut-out cards (fringes, fletching).
/// PRESENTATION ONLY.
/// </summary>
public sealed class KitEffects : IEffectSource
{
    public const int SlotBase = 2, SlotNormal = 3, SlotOrm = 4;

    public IEnumerable<EffectBuilder> CreateEffects()
    {
        yield return CharEffects.Attributes(new EffectBuilder(KitEffectNames.Kit)).AddStandardLit()
            .Varyings("vec3 v_wpos; vec3 v_nrm; vec4 v_tan; vec2 v_uv; vec4 v_col; vec4 v_lpos;")
            .VertexLib(GlslLib.ShadowLookup)
            .Texture("KitBase", SlotBase).Texture("KitNormal", SlotNormal).Texture("KitOrm", SlotOrm)
            .Vec4("KitParams", Stage.Pixel).Vec4("KitTint", Stage.Pixel)
            .PixelLib(CharEffects.SurfaceLib)
            .PixelLib(@"
vec3 kitSrgb(vec3 c) { vec3 lo = c / 12.92; vec3 hi = pow((c + 0.055) / 1.055, vec3(2.4)); return mix(lo, hi, step(vec3(0.04045), c)); }")
            .VertexMain(@"
    vec3 sp; vec3 sn; vec3 st;
    skinVertex(sp, sn, st);
    v_wpos = sp; v_nrm = sn; v_tan = vec4(st, a_tan.w); v_uv = a_uv; v_col = a_col;
    // three times the standard normal offset: large smooth metal faces in close-ups otherwise show acne stripes
    v_lpos = LightViewProj_mul(vec4(sp + normalize(sn) * ShadowOffset.x * 3.0, 1.0));
    gl_Position = ViewProj_mul(vec4(sp, 1.0));")
            .PixelMain(@"
    vec4 bc = texture2D(KitBase, v_uv);
    if (KitParams.y > 0.5) {
        float thr = fract(sin(dot(gl_FragCoord.xy + KitTint.a * vec2(37.0, 17.0), vec2(12.9898, 78.233))) * 43758.5453);
        if (bc.a < mix(0.15, 0.85, thr)) discard;
    }
    vec3 nm = texture2D(KitNormal, v_uv).xyz * 2.0 - 1.0;
    nm.xy *= KitParams.x;
    vec4 orm = texture2D(KitOrm, v_uv);
    vec3 Ng = normalize(v_nrm);
    vec3 T = normalize(v_tan.xyz - Ng * dot(Ng, v_tan.xyz));
    vec3 B = cross(Ng, T) * v_tan.w;
    vec3 N = normalize(T * nm.x + B * nm.y + Ng * max(nm.z, 0.05));
    vec3 albedo = kitSrgb(bc.rgb) * KitTint.rgb;
    float ao = mix(1.0, orm.r, KitParams.w);
    // the shadow map is trusted only where the surface faces the sun; the lit side's terminator comes from N.L
    vec3 Vv = normalize(CameraPos.xyz - v_wpos);
    vec3 Ns = dot(Ng, Vv) < 0.0 ? -Ng : Ng;
    float shadow = mix(1.0, shadowPcf(v_lpos), smoothstep(-0.05, 0.25, dot(Ns, SunDir.xyz)));
    vec3 c = shadeSurface(albedo, orm.g, orm.b, ao, N, Ng, v_wpos, shadow, KitParams.z);
    gl_FragColor = vec4(applyFog(c, v_wpos), 1.0);");
    }
}

/// <summary>
/// Material handler for parts whose material kind is "kit": finds the part's map set (INFO materials[part].maps) in the
/// owning asset's textures and sets the effect. Register once per renderer with <see cref="Register"/>.
/// </summary>
public static class KitMaterials
{
    public static void Register(CharMaterials m) => m.Extension = Setup;

    static Effect? Setup(CharRenderer r, RenderContext rc, CharInstance inst, GpuPart part, CharAsset owner)
    {
        if (part.Material != "kit") return null;
        var mat = owner.File.Info["materials"]?[part.Name] as System.Text.Json.Nodes.JsonObject;
        string maps = mat?["maps"]?.GetValue<string>() ?? part.Name;
        if (!owner.Textures.TryGetValue(maps + ":base", out var bas)) return null;
        var fx = rc.Effects.Get(KitEffectNames.Kit);
        fx.Parameters["KitBase"].SetValue(bas);
        fx.Parameters["KitNormal"].SetValue(owner.Textures.TryGetValue(maps + ":normal", out var n) ? n : r.FlatNormal);
        fx.Parameters["KitOrm"].SetValue(owner.Textures.TryGetValue(maps + ":orm", out var o) ? o : r.White);
        float F(string k, float d) => mat?[k] is System.Text.Json.Nodes.JsonValue v && v.TryGetValue<double>(out double x) ? (float)x : d;
        fx.Parameters["KitParams"].SetValue(new Vector4(F("normal", 1f), F("alpha", 0f), F("sheen", 0f), F("ao", 1f)));
        var tint = inst.Tint;
        fx.Parameters["KitTint"].SetValue(new Vector4(tint, r.FrameSeed));
        return fx;
    }
}
