using Gfx.Engine.Effects;
using Gfx.Engine.Mgfx;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;

namespace Gfx.Proc.Environment.Trees;

/// <summary>
/// Hemi-octahedral multi-view impostors (stage env; PRESENTATION ONLY). Each tree variant is rendered from N×N
/// directions over the upper hemisphere (frame (i, j) looks from <c>hemiOctDecode((i, j) / (N − 1))</c>; the outer ring
/// of frames are horizontal views) into an albedo+coverage atlas and a normal+depth atlas. At run time a camera-facing
/// quad picks one of the four frames around the view direction per pixel (dithered by the bilinear weights, so
/// jittered stills blend them), intersects the view ray with that frame's plane, and shades the stored normal with the
/// same foliage model as the 3D trees (sun with cascaded shadows at the reconstructed surface point, sky light from the
/// SH irradiance, transmitted light, aerial perspective). It writes the reconstructed depth, so impostors on slopes
/// meet the ground like the meshes they replace.
/// </summary>
public static class ImpostorEffects
{
    public const int AlbSlot = GlslLib.FirstMaterialSlot, NrmSlot = GlslLib.FirstMaterialSlot + 1;

    /// <summary>Shared by the GLSL and the C# bake (<see cref="ImpostorMath"/>): direction ↔ grid, frame basis.</summary>
    public const string Lib = @"
#ifdef GL_ARB_shader_texture_lod
#define TEXL(s, uv, l) texture2DLod(s, uv, l)
#else
#define TEXL(s, uv, l) texture2D(s, uv, l)
#endif
vec3 hemiOctDecode(vec2 g) {
    vec2 f = g * 2.0 - 1.0;
    vec2 p = vec2(f.x + f.y, f.x - f.y) * 0.5;
    return normalize(vec3(p.x, 1.0 - abs(p.x) - abs(p.y), p.y));
}
vec2 hemiOctEncode(vec3 d) {
    d.y = max(d.y, 0.0);
    vec3 a = d / (abs(d.x) + abs(d.y) + abs(d.z));
    return vec2(a.x + a.z, a.x - a.z) * 0.5 + 0.5;
}
void frameBasis(vec3 d, out vec3 right, out vec3 up) {
    up = abs(d.y) < 0.999 ? normalize(vec3(0.0, 1.0, 0.0) - d * d.y) : vec3(0.0, 0.0, -1.0);
    right = normalize(cross(up, d));
}
vec3 rotY(vec3 v, float c, float s) { return vec3(c * v.x + s * v.z, v.y, -s * v.x + c * v.z); }";

    public static IEnumerable<EffectBuilder> Create()
    {
        yield return Impostor(depth: false);
        yield return Impostor(depth: true);
    }

    static EffectBuilder Impostor(bool depth)
    {
        var b = new EffectBuilder(depth ? EnvEffectNames.ImpostorDepth : EnvEffectNames.Impostor)
            .Attribute("vec4 a_pos", VertexElementUsage.Position, 0)
            .AddTreeInstance().AddEnvParams()
            // ImpParams: x = frames per side N, y = frame size px, z = atlas size px, w = model radius R (m, before scale)
            .Vec4("ImpParams", Stage.Both)
            .Vec4("ImpCenter", Stage.Both)   // xyz = model sphere centre (local, before scale)
            .Vec4("ImpEye", Stage.Both)      // xyz = the viewer the quad faces: camera position, or (shadow pass) a point far towards the light
            .Matrix("ImpViewProj", Stage.Pixel)
            // ImpLod: x = world size of one screen pixel per metre of distance (2 tan(fov/2) / viewport height), or for the
            // shadow pass y = world size of one shadow texel; mip selection is explicit because the per-pixel frame choice
            // makes the atlas coordinates discontinuous (hardware derivatives would pick the coarsest level)
            .Vec4("ImpLod", Stage.Pixel)
            .Extension(GlslLib.TextureLodExt)
            .Texture("ImpAlb", AlbSlot).Texture("ImpNrm", NrmSlot)
            .VertexLib(Lib).PixelLib(Lib)
            .Varyings("vec3 v_wpos; vec3 v_centre; vec4 v_inst; vec3 v_tint; vec2 v_lod;");
        string vs = @"
    float s = i_lod.z;
    vec3 c = instPos(vec4(ImpCenter.xyz, 1.0));
    float R = ImpParams.w * s;
    vec3 fwd = ImpEye.xyz - c;
    fwd = length(fwd) > 1e-4 ? normalize(fwd) : vec3(0.0, 0.0, 1.0);
    vec3 up = abs(fwd.y) < 0.999 ? normalize(vec3(0.0, 1.0, 0.0) - fwd * fwd.y) : vec3(0.0, 0.0, -1.0);
    vec3 right = normalize(cross(up, fwd));
    vec3 wp = c + (right * a_pos.x + up * a_pos.y) * R;
    v_wpos = wp; v_centre = c; v_inst = vec4(cos(i_lod.w), sin(i_lod.w), s, R); v_tint = i_tint.rgb; v_lod = i_lod.xy;";
        const string pick = @"
    // view direction in the tree's local frame (undo the instance yaw), grid position, one of four frames by dither
    float cy = v_inst.x, sy = v_inst.y, R = v_inst.w;
    vec3 eye = ImpEye.xyz;
    vec3 toEye = normalize(eye - v_centre);
    vec3 dl = rotY(toEye, cy, -sy);
    float N = ImpParams.x;
    vec2 g = clamp(hemiOctEncode(dl), 0.0, 1.0) * (N - 1.0);
    vec2 g0 = min(floor(g), vec2(N - 2.0)); vec2 fr = g - g0;
    float r = envDither(vec2(41.0, 13.0));
    vec2 cell = g0;
    float w00 = (1.0 - fr.x) * (1.0 - fr.y), w10 = fr.x * (1.0 - fr.y), w01 = (1.0 - fr.x) * fr.y;
    if (r < w00) cell = g0; else if (r < w00 + w10) cell = g0 + vec2(1.0, 0.0); else if (r < w00 + w10 + w01) cell = g0 + vec2(0.0, 1.0); else cell = g0 + vec2(1.0, 1.0);
    vec3 df = hemiOctDecode(cell / (N - 1.0));
    vec3 fr0, fu0; frameBasis(df, fr0, fu0);
    vec3 D = rotY(df, cy, sy), Rt = rotY(fr0, cy, sy), Up = rotY(fu0, cy, sy);
    // intersect the ray eye -> pixel with the frame plane through the centre
    vec3 dir = normalize(v_wpos - eye);
    float denom = dot(dir, D);
    if (abs(denom) < 1e-4) discard;
    float t = dot(v_centre - eye, D) / denom;
    vec3 Q = eye + dir * t - v_centre;
    vec2 uvf = vec2(dot(Q, Rt), -dot(Q, Up)) / (2.0 * R) + 0.5;
    if (uvf.x < 0.0 || uvf.y < 0.0 || uvf.x > 1.0 || uvf.y > 1.0) discard;
    float texel = 1.0 / ImpParams.y;
    uvf = clamp(uvf, vec2(0.75 * texel), vec2(1.0 - 0.75 * texel));
    vec2 uv = (cell + uvf) / N;
    float texWorld = 2.0 * R / ImpParams.y;
    float pixWorld = ImpLod.x > 0.0 ? length(eye - v_wpos) * ImpLod.x : ImpLod.y;
    float lod = max(log2(max(pixWorld, 1e-6) / texWorld), 0.0);";
        if (depth)
            return b.Matrix("LightViewProj", Stage.Vertex).Varyings("vec4 v_clip;")
                .VertexMain(vs + @"
    gl_Position = LightViewProj_mul(vec4(wp, 1.0)); v_clip = gl_Position;")
                .PixelLib(EnvGlsl.Common)
                .PixelMain(pick + @"
    if (TEXL(ImpAlb, uv, lod).a < 0.5) discard;
    gl_FragColor = vec4(v_clip.z / v_clip.w, 0.0, 0.0, 1.0);");
        return b.AddStandardLit()
            .Vec4("Transmission", Stage.Pixel)
            .Matrix("ImpLightViewProj", Stage.Pixel)
            .VertexMain(vs + @"
    gl_Position = ViewProj_mul(vec4(wp, 1.0));")
            .PixelLib(EnvGlsl.Common).PixelLib(EnvGlsl.Foliage)
            .PixelMain(pick + @"
    vec4 a = TEXL(ImpAlb, uv, lod);
    if (a.a < alphaCut(vec2(23.0, 7.0))) discard;
    lodDither(v_lod);
    vec4 nd = TEXL(ImpNrm, uv, lod);
    vec3 nl = octDec(nd.xy);
    vec3 n = normalize(rotY(nl, cy, sy));
    float ao = nd.a;
    vec3 P = v_centre + Q + D * ((nd.b - 0.5) * 2.0 * R);
    // reconstructed depth (D3D clip z / w is the window depth after MonoGame's fix-up)
    vec4 pc = ImpViewProj_mul(vec4(P, 1.0));
    gl_FragDepth = clamp(pc.z / pc.w, 0.0, 1.0);
    vec3 albedo = pow(a.rgb, vec3(2.2)) * v_tint;
    float sh = shadowPcf(ImpLightViewProj_mul(vec4(P + n * (0.25 + 0.004 * length(CameraPos.xyz - P)), 1.0)));
    // the meshes are self-shadowed leaf by leaf through the shadow map; the impostor keeps only its outer surface, so
    // its canopy occlusion stands in for the sun blocked inside the crown
    // (shrubs: measured against their meshes, the tree mapping left them near black, so theirs is gentler)
    sh *= mix(smoothstep(0.22, 0.85, ao), smoothstep(0.0, 0.5, ao), Transmission.w);
    vec3 c = shadeFoliage(albedo, n, P, sh, ao, 0.25);
    vec3 L = SunDir.xyz;
    vec3 V = normalize(CameraPos.xyz - P);
    float back = max(dot(-n, L), 0.0);
    float fwdS = pow(max(dot(-V, L), 0.0), 6.0);
    vec3 trans = albedo / max(dot(albedo, vec3(0.2126, 0.7152, 0.0722)), 1e-4) * Transmission.rgb * 0.11;
    c += trans * SunColor.rgb * sh * (back * (1.0 + 2.0 * fwdS)) / PI * mix(0.6, 1.0, ao);
    gl_FragColor = vec4(applyFog(c, P), 1.0);");
    }
}

/// <summary>The C# side of the hemi-octahedral mapping (same formulas as <see cref="ImpostorEffects.Lib"/>).</summary>
public static class ImpostorMath
{
    public static Vector3 Decode(Vector2 g)
    {
        var f = g * 2f - Vector2.One;
        var p = new Vector2(f.X + f.Y, f.X - f.Y) * 0.5f;
        return Vector3.Normalize(new Vector3(p.X, 1f - MathF.Abs(p.X) - MathF.Abs(p.Y), p.Y));
    }

    public static Vector2 Encode(Vector3 d)
    {
        d.Y = MathF.Max(d.Y, 0f);
        var a = d / (MathF.Abs(d.X) + MathF.Abs(d.Y) + MathF.Abs(d.Z));
        return new Vector2(a.X + a.Z, a.X - a.Z) * 0.5f + new Vector2(0.5f);
    }

    public static (Vector3 right, Vector3 up) Basis(Vector3 d)
    {
        var up = MathF.Abs(d.Y) < 0.999f ? Vector3.Normalize(Vector3.Up - d * d.Y) : new Vector3(0, 0, -1);
        return (Vector3.Normalize(Vector3.Cross(up, d)), up);
    }

    /// <summary>Direction of frame (i, j) of an N×N atlas.</summary>
    public static Vector3 FrameDirection(int i, int j, int n) => Decode(new Vector2(i / (float)(n - 1), j / (float)(n - 1)));
}
