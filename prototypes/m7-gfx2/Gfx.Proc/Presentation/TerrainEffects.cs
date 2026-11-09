using Gfx.Engine.Effects;
using Gfx.Engine.Mgfx;
using Microsoft.Xna.Framework.Graphics;

namespace Gfx.Proc.Presentation;

/// <summary>Effect names shipped by lane T.</summary>
public static class TerrainEffectNames
{
    public const string TerrainSplat = EffectNames.TerrainSplat;
    public const string TerrainDepth = "TerrainDepth";
    public const string Water = "Water";
}

/// <summary>
/// Lane T effects (discovered via <see cref="IEffectSource"/>). TerrainSplat: 8-material splat with detail-driven
/// height blending, two-scale rotated sampling against tiling, biplanar rock on steep faces, macro tint; standard
/// lighting/fog/shadows. Water: per-vertex depth tint, Fresnel sky reflection, sun glint, animated ripple normals.
/// </summary>
public sealed class TerrainEffects : IEffectSource
{
    public IEnumerable<EffectBuilder> CreateEffects()
    {
        yield return Splat();
        yield return Depth();
        yield return WaterFx();
    }

    static EffectBuilder Splat()
    {
        var b = new EffectBuilder(TerrainEffectNames.TerrainSplat)
            .Attribute("vec4 a_pos", VertexElementUsage.Position, 0)
            .Attribute("vec3 a_nrm", VertexElementUsage.Normal, 0)
            .Attribute("vec4 a_wa", VertexElementUsage.TextureCoordinate, 0)
            .Attribute("vec4 a_wb", VertexElementUsage.TextureCoordinate, 1)
            .AddStandardLit().VertexLib(GlslLib.ShadowLookup);
        for (int i = 0; i < SplatModel.Count; i++) b.Vec4("MatColor" + i, Stage.Pixel);
        return b.Vec4("TerrainParams", Stage.Pixel) // x detail tiling (1/m), y macro tiling (1/m), z blend sharpness
            .Texture("DetailA", GlslLib.FirstMaterialSlot)
            .Texture("DetailB", GlslLib.FirstMaterialSlot + 1)
            .Varyings("vec3 v_wpos; vec3 v_nrm; vec4 v_wa; vec4 v_wb; vec4 v_lpos;")
            .VertexMain(@"
    vec3 wp = a_pos.xyz;
    v_wpos = wp; v_nrm = a_nrm; v_wa = a_wa; v_wb = a_wb;
    v_lpos = shadowLookup(wp, a_nrm);
    gl_Position = ViewProj_mul(vec4(wp, 1.0));")
            .PixelMain(@"
    vec3 n = normalize(v_nrm);
    vec2 p = v_wpos.xz;
    float macro = texture2D(DetailA, p * TerrainParams.y).x;
    vec2 uv1 = p * TerrainParams.x;
    vec2 uv2 = vec2(0.8 * p.x - 0.6 * p.y, 0.6 * p.x + 0.8 * p.y) * TerrainParams.x * 0.37 + vec2(0.31, 0.77);
    float k = smoothstep(0.3, 0.7, macro);
    vec4 da = mix(texture2D(DetailA, uv1), texture2D(DetailA, uv2), k);
    vec4 db = mix(texture2D(DetailB, uv1), texture2D(DetailB, uv2), k);
    vec2 uvr = (abs(n.x) > abs(n.z) ? v_wpos.zy : v_wpos.xy) * TerrainParams.x * 0.5;
    da.z = mix(da.z, texture2D(DetailA, uvr).z, smoothstep(0.25, 0.6, 1.0 - n.y));
    vec4 wa = v_wa * pow(da + 0.15, vec4(TerrainParams.z));
    vec4 wb = v_wb * pow(db + 0.15, vec4(TerrainParams.z));
    float s = dot(wa, vec4(1.0)) + dot(wb, vec4(1.0)) + 1e-5;
    wa /= s; wb /= s;
    vec3 alb = wa.x * MatColor0.rgb * (0.55 + 0.9 * da.x) + wa.y * MatColor1.rgb * (0.55 + 0.9 * da.y)
             + wa.z * MatColor2.rgb * (0.55 + 0.9 * da.z) + wa.w * MatColor3.rgb * (0.55 + 0.9 * da.w)
             + wb.x * MatColor4.rgb * (0.55 + 0.9 * db.x) + wb.y * MatColor5.rgb * (0.55 + 0.9 * db.y)
             + wb.z * MatColor6.rgb * (0.55 + 0.9 * db.z) + wb.w * MatColor7.rgb * (0.55 + 0.9 * db.w);
    alb *= mix(0.82, 1.15, macro);
    vec3 c = shadeLit(alb, n, shadowPcf(v_lpos));
    gl_FragColor = vec4(applyFog(c, v_wpos), 1.0);");
    }

    static EffectBuilder Depth() => new EffectBuilder(TerrainEffectNames.TerrainDepth)
        .Attribute("vec4 a_pos", VertexElementUsage.Position, 0)
        .Matrix("LightViewProj", Stage.Vertex).Varyings("float v_depth;")
        .VertexMain(@"
    gl_Position = LightViewProj_mul(vec4(a_pos.xyz, 1.0)); v_depth = gl_Position.z / gl_Position.w;")
        .PixelMain("    gl_FragColor = vec4(v_depth, 0.0, 0.0, 1.0);");

    static EffectBuilder WaterFx() => new EffectBuilder(TerrainEffectNames.Water)
        .Attribute("vec4 a_pos", VertexElementUsage.Position, 0)
        .Attribute("vec4 a_dat", VertexElementUsage.TextureCoordinate, 0)
        .AddStandardLit().VertexLib(GlslLib.ShadowLookup)
        .Vec4("WaterParams", Stage.Pixel)   // x time s
        .Vec4("WaterShallow", Stage.Pixel).Vec4("WaterDeep", Stage.Pixel)
        .Vec4("SkyZenith", Stage.Pixel).Vec4("SkyHorizon", Stage.Pixel)
        .Varyings("vec3 v_wpos; float v_depth; vec4 v_lpos;")
        .VertexMain(@"
    vec3 wp = a_pos.xyz;
    v_wpos = wp; v_depth = a_dat.x;
    v_lpos = shadowLookup(wp, vec3(0.0, 1.0, 0.0));
    gl_Position = ViewProj_mul(vec4(wp, 1.0));")
        .PixelMain(@"
    float t = WaterParams.x; vec2 p = v_wpos.xz;
    vec3 n = normalize(vec3(0.05 * sin(p.x * 0.9 + t * 1.3) + 0.04 * sin(p.y * 1.7 - t * 0.9 + p.x * 0.3), 1.0,
                            0.05 * cos(p.y * 1.1 + t * 1.1) + 0.03 * sin((p.x + p.y) * 2.3 + t * 1.7)));
    vec3 V = normalize(CameraPos.xyz - v_wpos);
    float fres = 0.02 + 0.98 * pow(1.0 - max(dot(n, V), 0.0), 5.0);
    vec3 r = reflect(-V, n);
    vec3 sky = mix(SkyHorizon.rgb, SkyZenith.rgb, sqrt(clamp(r.y, 0.0, 1.0)));
    float sh = shadowPcf(v_lpos);
    float spec = pow(max(dot(r, SunDir.xyz), 0.0), 180.0) * 6.0 * sh;
    vec3 body = mix(WaterShallow.rgb, WaterDeep.rgb, clamp(v_depth / 3.0, 0.0, 1.0))
              * (AmbientSky.rgb + SunColor.rgb * max(SunDir.y, 0.0) * sh);
    vec3 c = mix(body, sky, fres) + SunColor.rgb * spec;
    float alpha = clamp(0.5 + v_depth * 0.2 + fres, 0.0, 0.95);
    gl_FragColor = vec4(applyFog(c, v_wpos), alpha);");
}
