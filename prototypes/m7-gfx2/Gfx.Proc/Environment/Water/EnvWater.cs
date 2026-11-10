using System.Runtime.InteropServices;
using Gfx.Engine.Effects;
using Gfx.Engine.Mgfx;
using Gfx.Engine.Rendering;
using Gfx.Proc.Authoritative;
using Gfx.Proc.Presentation;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;

namespace Gfx.Proc.Environment.Water;

/// <summary>Water vertex: position on the surface; flow (x, z velocity m/s, w = 1 river / 0 still or sea).</summary>
[StructLayout(LayoutKind.Sequential, Pack = 1)]
public struct VertexEnvWater : IVertexType
{
    public Vector3 Position; public Vector4 Flow;
    public static readonly VertexDeclaration Decl = new(
        new VertexElement(0, VertexElementFormat.Vector3, VertexElementUsage.Position, 0),
        new VertexElement(12, VertexElementFormat.Vector4, VertexElementUsage.TextureCoordinate, 0));
    readonly VertexDeclaration IVertexType.VertexDeclaration => Decl;
}

/// <summary>
/// The river's flow field (PRESENTATION; derived from the grid and the generation trace, read only): every wet sample
/// takes the downstream tangent of the nearest river centre-line cell and a speed that falls from mid-channel to the
/// banks (parabolic profile); sea and other still water get no flow.
/// </summary>
public static class FlowField
{
    public static Vector2[] Build(BattleTerrainGrid g, GenerationTrace? trace, float maxSpeed = 1.1f)
    {
        int W = g.Width, H = g.Height;
        var flow = new Vector2[W * H];
        if (trace == null || trace.RiverCells.Length < 3) return flow;
        var path = trace.RiverCells;
        int R = 9;
        for (int y = 0; y < H; y++)
            for (int x = 0; x < W; x++)
            {
                int i = y * W + x;
                if (g.WaterDepthMm[i] <= 0) continue;
                int best = -1; int bestD = int.MaxValue;
                for (int k = 0; k < path.Length; k++)
                {
                    int px = path[k] % W, py = path[k] / W;
                    int dx = px - x, dy = py - y;
                    if (Math.Abs(dx) > R || Math.Abs(dy) > R) continue;
                    int d = dx * dx + dy * dy;
                    if (d < bestD) { bestD = d; best = k; }
                }
                if (best < 0) continue;
                int a = path[Math.Max(0, best - 3)], b = path[Math.Min(path.Length - 1, best + 3)];
                var t = new Vector2(b % W - a % W, b / W - a / W);
                if (t.LengthSquared() < 1e-6f) continue;
                t = Vector2.Normalize(t);
                int half = 1 + best * 2 / Math.Max(1, path.Length) + 1;
                float rr = MathF.Sqrt(bestD) / (half + 0.5f);
                float speed = maxSpeed * MathF.Max(0.12f, 1f - rr * rr);
                // grid y grows towards +z
                flow[i] = t * speed;
            }
        return flow;
    }
}

/// <summary>
/// Environment water (stage env; DR45/DR46; PRESENTATION ONLY). One mesh over the wet cells plus a one-cell skirt
/// (the shoreline is cut per pixel by the terrain, not by cells). The pixel shader computes the water depth per pixel
/// from a height texture of the terrain, so absorption, refraction strength, shore foam and the soft shore edge follow
/// the true waterline. Normals: a drawn wave normal map advected along the flow with two phases (Vlachos 2010) at two
/// scales, plus render1's wind-ripple octaves on still water. Fresnel (water F0 0.02); planar reflection from the
/// renderer (clipped at the water plane) or the sky table; refraction of the opaque scene with Beer–Lambert absorption
/// over the refracted path; foam in a broken band at the waterline and in fast shallow water.
/// </summary>
public sealed class EnvWater : IDisposable
{
    readonly GraphicsDevice _gd;
    readonly TerrainMeshData _m;
    readonly VertexBuffer? _vb; readonly IndexBuffer? _ib; readonly int _tris;
    readonly Texture2D _height, _level;
    readonly EnvTextures _tex;
    public bool Arid { get; }
    public int Triangles => _tris;
    public Texture2D LevelMap => _level;

    public EnvWater(GraphicsDevice gd, TerrainMeshData m, WaterField water, Vector2[] flow, EnvTextures tex)
    {
        _gd = gd; _m = m; _tex = tex;
        Arid = m.Grid.Biome is Biome.Arid or Biome.Tropical;
        var g = m.Grid; int W = g.Width, H = g.Height;
        // terrain heights and the nearby water level as float textures (one texel per grid sample)
        var hs = new float[W * H]; var lv = new float[W * H];
        for (int i = 0; i < hs.Length; i++) hs[i] = g.HeightMm[i] / 1000f;
        var ws = water.Samples;
        for (int i = 0; i < lv.Length; i++) lv[i] = float.IsNaN(ws[i]) ? -10000f : ws[i];
        _height = new Texture2D(gd, W, H, false, SurfaceFormat.Single); _height.SetData(hs);
        _level = new Texture2D(gd, W, H, false, SurfaceFormat.Single); _level.SetData(lv);
        for (int i = 0; i < 16; i++) gd.Textures[i] = null;

        // mesh: corners of wet cells (+ one-cell skirt); corner surface = mean of the wet cells around it
        var src = m.Water;
        if (src.Indices.Length == 0) return;
        var v = new VertexEnvWater[src.Vertices.Length];
        for (int i = 0; i < v.Length; i++)
        {
            var p = src.Vertices[i].Position;
            // flow at the corner: mean of the four cells around it
            float fx = p.X / m.CellM + W / 2f, fz = p.Z / m.CellM + H / 2f;
            int cx = (int)MathF.Round(fx), cz = (int)MathF.Round(fz);
            var f = Vector2.Zero; int n = 0;
            for (int dz = -1; dz <= 0; dz++)
                for (int dx = -1; dx <= 0; dx++)
                {
                    int x = cx + dx, z = cz + dz;
                    if (x < 0 || z < 0 || x >= W || z >= H) continue;
                    var c = flow[z * W + x];
                    if (c != Vector2.Zero) { f += c; n++; }
                }
            if (n > 0) f /= n;
            v[i] = new VertexEnvWater { Position = p, Flow = new Vector4(f.X, f.Y, n > 0 ? 1 : 0, 0) };
        }
        _vb = new VertexBuffer(gd, VertexEnvWater.Decl, v.Length, BufferUsage.WriteOnly); _vb.SetData(v);
        _ib = new IndexBuffer(gd, IndexElementSize.ThirtyTwoBits, src.Indices.Length, BufferUsage.WriteOnly); _ib.SetData(src.Indices);
        _tris = src.TriangleCount;
    }

    public void Draw(RenderContext rc)
    {
        if (_vb == null || _ib == null) return;
        var e = rc.Effects.Get(EnvEffectNames.Water);
        var p = e.Parameters;
        var g = _m.Grid; float W = g.Width, H = g.Height, cell = _m.CellM;
        p["WaterGrid"].SetValue(new Vector4(1f / (cell * W), (W / 2 + 0.5f) / W, 1f / (cell * H), (H / 2 + 0.5f) / H));
        p["WaterGeom"].SetValue(new Vector4(cell, W / 2, H / 2, W));
        p["WaterTime"].SetValue(new Vector4(rc.TimeSeconds, rc.Features.WaterRippleFade ? 1 : 0, rc.SceneColor != null ? 1 : 0, rc.Reflection != null ? 1 : 0));
        p["WaterPlane"].SetValue(rc.ReflectionPlane);
        p["WaterScatter"].SetValue(Arid ? new Vector4(0.032f, 0.070f, 0.066f, 0) : new Vector4(0.030f, 0.050f, 0.036f, 0));
        // attenuation per metre: a lowland river carries silt and algae (green-brown, the bed fades within a metre or two);
        // the arid river and sea are clearer
        p["WaterAbsorb"].SetValue(Arid ? new Vector4(0.45f, 0.14f, 0.12f, 0) : new Vector4(1.10f, 0.62f, 0.85f, 0));
        p["Viewport"].SetValue(rc.ViewportSize);
        p["TerrainHeight"].SetValue(_height);
        p["WaterNrm"].SetValue(_tex.Get("water_nrm.r1tex", normalMap: true));
        p["SceneColor"].SetValue(rc.SceneColor);
        p["ReflectionTex"].SetValue(rc.Reflection);
        _gd.BlendState = BlendState.AlphaBlend; _gd.DepthStencilState = DepthStencilState.DepthRead;
        _gd.SetVertexBuffer(_vb); _gd.Indices = _ib;
        rc.Apply(e);
        var ss = _gd.SamplerStates;
        ss[WaterEffects.HeightSlot] = SamplerState.PointClamp; ss[WaterEffects.NrmSlot] = SamplerState.LinearWrap;
        ss[WaterEffects.SceneSlot] = SamplerState.LinearClamp; ss[WaterEffects.ReflSlot] = SamplerState.LinearClamp;
        _gd.DrawIndexedPrimitives(PrimitiveType.TriangleList, 0, 0, _tris);
        rc.Stats.RecordDraw(_tris, 1);
        _gd.BlendState = BlendState.Opaque; _gd.DepthStencilState = DepthStencilState.Default;
        for (int i = 2; i < 7; i++) _gd.Textures[i] = null;
    }

    public void Dispose() { _vb?.Dispose(); _ib?.Dispose(); _height.Dispose(); _level.Dispose(); }
}

public static class WaterEffects
{
    public const int HeightSlot = GlslLib.FirstMaterialSlot, NrmSlot = GlslLib.FirstMaterialSlot + 1,
        SceneSlot = GlslLib.FirstMaterialSlot + 2, ReflSlot = GlslLib.FirstMaterialSlot + 3;

    public static EffectBuilder Create() => new EffectBuilder(EnvEffectNames.Water)
        .Attribute("vec4 a_pos", VertexElementUsage.Position, 0)
        .Attribute("vec4 a_flow", VertexElementUsage.TextureCoordinate, 0)
        .AddEnvParams()
        .AddStandardLit().VertexLib(GlslLib.ShadowLookup)
        .Vec4("WaterGrid", Stage.Pixel)   // world -> terrain-texture uv (x scale, x offset, z scale, z offset)
        .Vec4("WaterTime", Stage.Pixel)   // x time, y ripple fade, z refraction on, w planar reflection on
        .Vec4("WaterPlane", Stage.Pixel)  // reflection plane (xyz normal, w = -n.p)
        .Vec4("WaterScatter", Stage.Pixel).Vec4("WaterAbsorb", Stage.Pixel).Vec4("Viewport", Stage.Pixel)
        .Vec4("WaterGeom", Stage.Pixel)   // x = cell size m, y = W/2, z = H/2 (sample offsets), w = grid size (texels)
        .Texture("TerrainHeight", HeightSlot).Texture("WaterNrm", NrmSlot).Texture("SceneColor", SceneSlot).Texture("ReflectionTex", ReflSlot)
        .Varyings("vec3 v_wpos; vec4 v_flow; vec4 v_lpos; vec4 v_clip;")
        .VertexMain(@"
    vec3 wp = a_pos.xyz;
    v_wpos = wp; v_flow = a_flow;
    v_lpos = shadowLookup(wp, vec3(0.0, 1.0, 0.0));
    gl_Position = ViewProj_mul(vec4(wp, 1.0));
    v_clip = gl_Position;")
        .PixelLib(EnvGlsl.Common)
        .PixelLib(@"
vec3 sampleWaves(vec2 uv) { return decodeNormal(texture2D(WaterNrm, uv)); }
// flow-mapped normal: two phases half a period apart, blended by a triangle wave (no visible reset)
vec3 flowNormal(vec2 p, vec2 flow, float t, float scale, float period) {
    float ph0 = fract(t / period), ph1 = fract(t / period + 0.5);
    float w = abs(ph0 - 0.5) * 2.0;
    vec2 off = flow * period;
    vec3 a = sampleWaves(p * scale - off * ph0 * scale + vec2(0.0));
    vec3 b = sampleWaves(p * scale - off * ph1 * scale + vec2(0.37, 0.21));
    return normalize(mix(a, b, w));
}
// terrain height under xz with the terrain mesh's own triangulation (each cell split along its (x+1, z)-(x, z+1)
// diagonal), so the per-pixel depth matches where the terrain really cuts the water surface
float terrainAt(vec2 xz) {
    vec2 f = xz / WaterGeom.x + WaterGeom.yz;   // grid coordinates (sample units)
    vec2 i = floor(f), t = f - i;
    vec2 inv = WaterGeom.w > 0.0 ? vec2(1.0 / WaterGeom.w) : vec2(1.0 / 256.0);
    float a = texture2D(TerrainHeight, (i + vec2(0.5, 0.5)) * inv).r;
    float b = texture2D(TerrainHeight, (i + vec2(1.5, 0.5)) * inv).r;
    float c = texture2D(TerrainHeight, (i + vec2(0.5, 1.5)) * inv).r;
    float d = texture2D(TerrainHeight, (i + vec2(1.5, 1.5)) * inv).r;
    return t.x + t.y <= 1.0 ? a + (b - a) * t.x + (c - a) * t.y : d + (c - d) * (1.0 - t.x) + (b - d) * (1.0 - t.y);
}
float hash21(vec2 p) { return envHash(p); }
float vnoise(vec2 p) {
    vec2 i = floor(p), f = fract(p); f = f * f * (3.0 - 2.0 * f);
    return mix(mix(hash21(i), hash21(i + vec2(1.0, 0.0)), f.x), mix(hash21(i + vec2(0.0, 1.0)), hash21(i + vec2(1.0, 1.0)), f.x), f.y);
}")
        .PixelMain(@"
    float t = WaterTime.x; vec3 p = v_wpos;
    vec3 toCam = CameraPos.xyz - p; float dist = length(toCam); vec3 V = toCam / max(dist, 1e-3);
    vec2 suv = vec2(v_clip.x / v_clip.w * 0.5 + 0.5, -v_clip.y / v_clip.w * 0.5 + 0.5);
    // water depth at this pixel from the terrain heights (continuous, so the shoreline is not cell-shaped)
    float bed = terrainAt(p.xz);
    float depth = p.y - bed;
    if (depth < -0.05) discard;
    vec2 flow = v_flow.xy;
    float speed = length(flow);
    // normals: flow-advected waves at two scales, calmer at the banks and in still water
    float fp = max(length(dFdx(p.xz)), length(dFdy(p.xz)));
    vec3 n1 = flowNormal(p.xz, flow, t, 1.0 / 3.1, 2.0);
    vec3 n2 = flowNormal(p.xz + vec2(1.7, 5.3), flow * 1.3, t, 1.0 / 0.9, 1.3);
    float fine = 1.0 - smoothstep(0.02, 0.12, fp / 0.9);
    vec2 slope = n1.xy * (0.45 + 0.6 * min(speed, 1.2)) + n2.xy * (0.35 * fine) * (0.5 + 0.5 * min(speed, 1.0));
    if (v_flow.z < 0.5) {
        // still water and sea: wind ripples drifting downwind
        vec3 n3 = sampleWaves(p.xz / 4.3 + vec2(t * 0.03, t * 0.017));
        vec3 n4 = sampleWaves(p.xz / 1.3 - vec2(t * 0.05, -t * 0.04));
        slope = n3.xy * 0.55 + n4.xy * 0.3 * fine;
    }
    // shallow water near the bank is calmer
    slope *= smoothstep(0.0, 0.5, depth) * 0.85 + 0.15;
    // the grazing view filters fine slopes out: keep the mean, roughen the glint instead
    float lost = clamp(fp * 0.6, 0.0, 0.5);
    vec3 n = normalize(vec3(-slope.x, 1.0, slope.y) * vec3(1.0 - lost, 1.0, 1.0 - lost));
    float nov = max(dot(n, V), 1e-3);
    float F = 0.02 + 0.98 * pow(1.0 - nov, 5.0);
    // reflection: planar (clipped at the water plane, distorted by the waves) or the sky table
    vec3 R = reflect(-V, n); R.y = abs(R.y);
    vec3 sky = LightModel.z > 0.5 ? skyView(R) : mix(AmbientGround.rgb, AmbientSky.rgb, R.y * 0.5 + 0.5);
    vec3 refl = sky;
    if (WaterTime.w > 0.5) {
        vec2 ruv = suv + n.xz * vec2(0.035, -0.035) * (0.5 + 0.5 * min(depth, 1.0));
        // the reflection is rendered for one plane (the water level near the camera); a reach at another level is
        // offset from that plane, which matters only in proportion to its distance (the image error is an angle), so
        // far reaches of a stepping river keep the planar reflection instead of falling back to the bright sky
        float planeDist = abs(dot(WaterPlane.xyz, p) + WaterPlane.w);
        float tol = max(1.2, 0.035 * dist);
        float wplanar = (1.0 - smoothstep(tol * 0.33, tol, planeDist)) * step(0.0, ruv.x) * step(ruv.x, 1.0) * step(0.0, ruv.y) * step(ruv.y, 1.0);
        refl = mix(sky, texture2D(ReflectionTex, ruv).rgb, wplanar);
    }
    // refraction: the bed seen through the water column, absorbed along the refracted path
    float cosT = sqrt(max(1.0 - (1.0 - nov * nov) / (1.33 * 1.33), 0.04));
    float path = max(depth, 0.0) / cosT;
    vec3 trans = exp(-WaterAbsorb.rgb * path);
    float sh = shadowPcf(v_lpos);
    vec3 irr = shIrradiance(vec3(0.0, 1.0, 0.0)) * (LightModel.y > 0.5 ? 1.0 : 0.0) + AmbientSky.rgb * PI * (LightModel.y > 0.5 ? 0.0 : 1.0);
    vec3 inscatter = WaterScatter.rgb * (irr / PI + SunColor.rgb * max(SunDir.y, 0.0) * sh / PI);
    vec3 refr;
    if (WaterTime.z > 0.5) {
        vec2 off = n.xz * vec2(0.025, -0.025) * min(depth, 1.5);
        vec3 bedCol = texture2D(SceneColor, suv + off).rgb;
        refr = bedCol * trans + inscatter * (vec3(1.0) - trans);
    } else refr = inscatter * (vec3(1.0) - trans * 0.6);
    // sun glint (GGX), roughened where waves were filtered out
    float rough = clamp(0.06 + lost, 0.05, 0.5); float a = rough * rough;
    vec3 L = SunDir.xyz; vec3 Hh = normalize(V + L);
    float nol = max(dot(n, L), 0.0), noh = max(dot(n, Hh), 0.0);
    vec3 spec = vec3(dGgx(noh, a) * vSmithGgx(nov, nol, a) * F) * SunColor.rgb * (nol * sh);
    vec3 c = mix(refr, refl, F) + spec;
    // foam: a broken band at the waterline, plus streaks in fast shallow water; advected with the flow
    vec2 fuv = p.xz - flow * t * 0.9;
    float fnoise = vnoise(fuv * 2.3) * 0.6 + vnoise(fuv * 6.1 + 3.7) * 0.4;
    // foam collects in patches where the current presses on the bank (never a continuous line along it)
    float shore = 1.0 - smoothstep(0.02, 0.22, depth);
    float rapid = smoothstep(0.85, 1.15, speed) * (1.0 - smoothstep(0.25, 0.8, depth));
    float patches = smoothstep(0.55, 0.8, vnoise(fuv * 0.35 + 9.1));
    float foam = smoothstep(0.66, 0.92, fnoise) * max(shore * patches, rapid * 0.6) * smoothstep(0.15, 0.7, speed + 0.25 * patches);
    vec3 foamCol = vec3(0.62, 0.64, 0.62) * (irr / PI + SunColor.rgb * max(SunDir.y, 0.0) * sh / PI);
    c = mix(c, foamCol, foam * 0.85);
    // soft shore edge: the water thins out over the last 20 cm, so the waterline is a gradient, not a cut
    float alpha = smoothstep(-0.02, 0.2, depth);
    if (WaterTime.z < 0.5) alpha *= clamp(1.0 - trans.g * (1.0 - F), 0.35, 0.97);
    gl_FragColor = vec4(applyFog(c, p), alpha);");
}
