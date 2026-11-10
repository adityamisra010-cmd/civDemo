using Gfx.Engine.Effects;
using Gfx.Engine.Mgfx;
using Gfx.Engine.Rendering;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;

namespace Gfx.Proc.Environment.Structures;

/// <summary>
/// Draws built structures: one static mesh per material (one draw call each), shadow casting, PBR with the material's
/// albedo/roughness and normal/occlusion maps, vertex tint and occlusion, and a world-space weathering term (darker,
/// damp, dirtier towards the ground and in streaks). PRESENTATION ONLY.
/// </summary>
public sealed class StructureRenderer : IDisposable
{
    readonly GraphicsDevice _gd;
    readonly VertexBuffer?[] _vb = new VertexBuffer?[(int)StMat.Count];
    readonly IndexBuffer?[] _ib = new IndexBuffer?[(int)StMat.Count];
    readonly int[] _tris = new int[(int)StMat.Count];
    readonly EnvTextures _tex;
    public int Triangles { get; }
    public BoundingBox Bounds { get; }

    public StructureRenderer(GraphicsDevice gd, StructureBuilder b, EnvTextures tex)
    {
        _gd = gd; _tex = tex;
        var min = new Vector3(float.MaxValue); var max = new Vector3(float.MinValue);
        for (int m = 0; m < (int)StMat.Count; m++)
        {
            var v = b.Verts[m]; var i = b.Idx[m];
            if (i.Count == 0) continue;
            _vb[m] = new VertexBuffer(gd, VertexStruct.Decl, v.Count, BufferUsage.WriteOnly); _vb[m]!.SetData(v.ToArray());
            _ib[m] = new IndexBuffer(gd, IndexElementSize.ThirtyTwoBits, i.Count, BufferUsage.WriteOnly); _ib[m]!.SetData(i.ToArray());
            _tris[m] = i.Count / 3; Triangles += _tris[m];
            foreach (var x in v) { min = Vector3.Min(min, x.Position); max = Vector3.Max(max, x.Position); }
        }
        Bounds = Triangles > 0 ? new BoundingBox(min, max) : new BoundingBox(Vector3.Zero, Vector3.Zero);
    }

    static string Name(StMat m) => m.ToString().ToLowerInvariant();

    (Texture2D alb, Texture2D nrm) Textures(StMat m) => m == StMat.Logs
        ? (_tex.Get("bark_oak_alb_rgh.r1tex"), _tex.Get("bark_oak_nrm_ao.r1tex", normalMap: true))
        : (_tex.Get($"st_{Name(m)}_alb_rgh.r1tex"), _tex.Get($"st_{Name(m)}_nrm_ao.r1tex", normalMap: true));

    public void Draw(RenderContext rc, bool depth)
    {
        if (Triangles == 0) return;
        var cull = depth && rc.ShadowFrustum is { } sf ? sf : rc.Frustum;
        if (!cull.IntersectsBox(Bounds.Min, Bounds.Max)) return;
        var e = rc.Effects.Get(depth ? EnvEffectNames.StructureDepth : EnvEffectNames.Structure);
        for (int m = 0; m < (int)StMat.Count; m++)
        {
            if (_vb[m] == null) continue;
            if (!depth)
            {
                var (alb, nrm) = Textures((StMat)m);
                e.Parameters["StAlb"].SetValue(alb); e.Parameters["StNrm"].SetValue(nrm);
                e.Parameters["StParams"].SetValue(new Vector4(m == (int)StMat.Thatch ? 1 : 0, m == (int)StMat.Tiles ? 1 : 0, 0, 0));
                e.Parameters["StEye"].SetValue(new Vector4(rc.Camera.Position, 1));
            }
            _gd.SetVertexBuffer(_vb[m]); _gd.Indices = _ib[m];
            rc.Apply(e);
            _gd.DrawIndexedPrimitives(PrimitiveType.TriangleList, 0, 0, _tris[m]);
            rc.Stats.RecordDraw(_tris[m], 1);
        }
    }

    public void Dispose()
    {
        foreach (var v in _vb) v?.Dispose();
        foreach (var i in _ib) i?.Dispose();
    }
}

public static class StructureEffects
{
    public const int AlbSlot = GlslLib.FirstMaterialSlot, NrmSlot = GlslLib.FirstMaterialSlot + 1;

    public static IEnumerable<EffectBuilder> Create()
    {
        yield return new EffectBuilder(EnvEffectNames.StructureDepth)
            .Attribute("vec4 a_pos", VertexElementUsage.Position, 0)
            .Matrix("LightViewProj", Stage.Vertex).Varyings("float v_depth;")
            .VertexMain(@"
    gl_Position = LightViewProj_mul(vec4(a_pos.xyz, 1.0)); v_depth = gl_Position.z / gl_Position.w;")
            .PixelMain("    gl_FragColor = vec4(v_depth, 0.0, 0.0, 1.0);");

        yield return new EffectBuilder(EnvEffectNames.Structure)
            .Attribute("vec4 a_pos", VertexElementUsage.Position, 0)
            .Attribute("vec3 a_nrm", VertexElementUsage.Normal, 0)
            .Attribute("vec4 a_tan", VertexElementUsage.Tangent, 0)
            .Attribute("vec2 a_uv", VertexElementUsage.TextureCoordinate, 0)
            .Attribute("vec4 a_col", VertexElementUsage.Color, 0)
            .AddEnvParams()
            .AddStandardLit().VertexLib(GlslLib.ShadowLookup)
            .Vec4("StParams", Stage.Pixel) // x = thatch, y = tiles
            .Vec4("StEye", Stage.Vertex)   // the viewer: the shadow lookup offsets along the normal of the visible side
            .Texture("StAlb", AlbSlot).Texture("StNrm", NrmSlot)
            .Varyings("vec3 v_wpos; vec3 v_nrm; vec4 v_tan; vec2 v_uv; vec4 v_col; vec4 v_lpos;")
            .VertexMain(@"
    vec3 wp = a_pos.xyz;
    v_wpos = wp; v_nrm = a_nrm; v_tan = a_tan; v_uv = a_uv; v_col = a_col;
    // pieces are drawn two-sided, so the side the viewer sees may be the back of the quad's winding: offset the shadow
    // lookup out of the visible side (offsetting into the solid shadows the face by itself in bands)
    vec3 sn = dot(a_nrm, StEye.xyz - wp) < 0.0 ? -a_nrm : a_nrm;
    v_lpos = shadowLookup(wp, sn);
    gl_Position = ViewProj_mul(vec4(wp, 1.0));")
            .PixelLib(EnvGlsl.Common)
            .PixelLib(@"
float h21(vec2 p) { return envHash(p); }
float vn(vec2 p) {
    vec2 i = floor(p), f = fract(p); f = f * f * (3.0 - 2.0 * f);
    return mix(mix(h21(i), h21(i + vec2(1.0, 0.0)), f.x), mix(h21(i + vec2(0.0, 1.0)), h21(i + vec2(1.0, 1.0)), f.x), f.y);
}")
            .PixelMain(@"
    vec4 ar = texture2D(StAlb, v_uv);
    vec4 nr = texture2D(StNrm, v_uv);
    vec3 N = normalize(v_nrm);
    vec3 T = v_tan.xyz - N * dot(N, v_tan.xyz);
    T = dot(T, T) > 1e-8 ? normalize(T) : vec3(1.0, 0.0, 0.0);
    vec3 Bv = cross(N, T) * v_tan.w;
    vec3 m = decodeNormal(nr);
    // two-sided: the visible side of a solid piece is its outside, whatever the winding of the quad
    if (dot(N, CameraPos.xyz - v_wpos) < 0.0) N = -N;
    // OpenGL normal maps: +Y points up the image, i.e. towards -v
    vec3 n = normalize(T * m.x - Bv * m.y + N * m.z);
    vec3 albedo = decodeSrgb(ar.rgb) * v_col.rgb;
    float rough = ar.a;
    // weathering: large-scale tone variation and soot/rain streaks (world space, so repeats do not line up)
    float macro = vn(v_wpos.xz * 0.11 + v_wpos.y * 0.05) * 0.6 + vn(v_wpos.xz * 0.43 + vec2(v_wpos.y * 0.3, 0.0)) * 0.4;
    float streak = vn(vec2(dot(v_wpos.xz, vec2(0.7071)) * 2.3, v_wpos.y * 0.15));
    albedo *= mix(0.84, 1.08, macro) * mix(1.0, 0.86, smoothstep(0.55, 0.9, streak) * (1.0 - abs(N.y)));
    // thatch weathers grey-green on top; tiles keep a darker, glossier tone where water runs
    if (StParams.x > 0.5) albedo = mix(albedo, albedo * vec3(0.78, 0.82, 0.74), smoothstep(0.4, 0.9, macro) * 0.6);
    if (StParams.y > 0.5) rough = mix(rough, rough * 0.8, smoothstep(0.5, 0.9, macro));
    float ao = nr.b * v_col.a;
    vec3 c = shadePbrFull(albedo, clamp(rough, 0.3, 1.0), 0.0, ao, n, N, v_wpos, shadowPcf(v_lpos));
    gl_FragColor = vec4(applyFog(c, v_wpos), 1.0);");
    }
}
