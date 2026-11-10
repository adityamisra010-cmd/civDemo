using Gfx.Engine.Assets;
using Gfx.Engine.Effects;
using Gfx.Engine.Mgfx;
using Gfx.Engine.Rendering;
using Gfx.Proc.Authoritative;
using Gfx.Proc.Environment.Trees;
using Gfx.Proc.Presentation;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;

namespace Gfx.Proc.Environment.Ground;

/// <summary>
/// Rock and twig meshes (stage env; PRESENTATION ONLY). A boulder is an icosphere stretched into an ellipsoid,
/// displaced by value-noise octaves and cut by several random fracture planes (flat facets with crisp edges, the
/// shape of broken rock rather than a smooth noise blob). Twigs are bent, tapering bark tubes. Vertex colour carries
/// nothing but white; the material comes from the photographic rock (Rock023, CC0) or bark textures, mapped
/// triplanar in the shader.
/// </summary>
public static class RockMeshes
{
    public static MeshData<VertexPNC> Boulder(uint seed, int subdivisions)
    {
        var (pos, tri) = Icosphere(subdivisions);
        float R(uint k) => PNoise.U01(PNoise.Hash(seed * 7919u + k));
        var radii = new Vector3(1f, 0.55f + 0.3f * R(1), 0.7f + 0.35f * R(2));
        int cuts = 3 + (int)(R(3) * 3);
        var planes = new (Vector3 n, float d)[cuts];
        for (int c = 0; c < cuts; c++)
        {
            float a = R(10u + (uint)c) * MathHelper.TwoPi, e = (R(20u + (uint)c) - 0.35f) * 1.4f;
            var n = Vector3.Normalize(new Vector3(MathF.Cos(a) * MathF.Cos(e), MathF.Sin(e), MathF.Sin(a) * MathF.Cos(e)));
            planes[c] = (n, 0.62f + 0.25f * R(30u + (uint)c));
        }
        var v = new Vector3[pos.Length];
        for (int i = 0; i < pos.Length; i++)
        {
            var d = pos[i];
            float disp = 1f + 0.22f * (PNoise.Fbm(d.X * 1.7f + 5f, d.Z * 1.7f + d.Y * 1.3f, seed, 4) - 0.5f) * 2f
                         + 0.05f * (PNoise.Fbm(d.X * 6f, d.Y * 6f + d.Z * 3f, seed ^ 0x77u, 2) - 0.5f) * 2f;
            var p = d * disp;
            foreach (var (n, dd) in planes)
            {
                float over = Vector3.Dot(p, n) - dd;
                if (over > 0) p -= n * over * 0.92f;
            }
            v[i] = p * radii;
        }
        // smooth normals from the faces; flat cuts keep nearly flat normals across their facets
        var nrm = new Vector3[v.Length];
        for (int t = 0; t < tri.Length; t += 3)
        {
            var fn = Vector3.Cross(v[tri[t + 1]] - v[tri[t]], v[tri[t + 2]] - v[tri[t]]);
            nrm[tri[t]] += fn; nrm[tri[t + 1]] += fn; nrm[tri[t + 2]] += fn;
        }
        var verts = new VertexPNC[v.Length];
        for (int i = 0; i < v.Length; i++) verts[i] = new VertexPNC(v[i], Vector3.Normalize(nrm[i]), Color.White);
        return new MeshData<VertexPNC>(verts, tri);
    }

    /// <summary>A bent twig, 1 m long before scaling, tapering from 1.6 cm to 0.4 cm radius, lying on the ground.</summary>
    public static MeshData<VertexPNC> Twig(uint seed)
    {
        var v = new List<VertexPNC>(); var ix = new List<int>();
        float R(uint k) => PNoise.U01(PNoise.Hash(seed * 7919u + k));
        int rings = 7, seg = 5;
        var pts = new Vector3[rings];
        for (int i = 0; i < rings; i++)
        {
            float t = i / (float)(rings - 1);
            pts[i] = new Vector3(t - 0.5f, 0.02f + 0.03f * MathF.Sin(t * 3f + R(1) * 3f), 0.08f * MathF.Sin(t * 4f + R(2) * 6f));
        }
        for (int i = 0; i < rings; i++)
        {
            var dir = Vector3.Normalize(pts[Math.Min(rings - 1, i + 1)] - pts[Math.Max(0, i - 1)]);
            var side = Vector3.Normalize(Vector3.Cross(dir, Vector3.Up)); var up = Vector3.Cross(side, dir);
            float r = MathHelper.Lerp(0.016f, 0.004f, i / (float)(rings - 1));
            for (int s = 0; s < seg; s++)
            {
                float a = s * MathHelper.TwoPi / seg;
                var n = side * MathF.Cos(a) + up * MathF.Sin(a);
                v.Add(new VertexPNC(pts[i] + n * r, n, Color.White));
            }
        }
        for (int i = 0; i < rings - 1; i++)
            for (int s = 0; s < seg; s++)
            {
                int a = i * seg + s, b = i * seg + (s + 1) % seg, c = a + seg, d = b + seg;
                ix.AddRange(new[] { a, c, b, b, c, d });
            }
        return new MeshData<VertexPNC>(v.ToArray(), ix.ToArray());
    }

    static (Vector3[] pos, int[] tri) Icosphere(int sub)
    {
        float t = (1f + MathF.Sqrt(5f)) / 2f;
        var p = new List<Vector3>
        {
            new(-1, t, 0), new(1, t, 0), new(-1, -t, 0), new(1, -t, 0), new(0, -1, t), new(0, 1, t),
            new(0, -1, -t), new(0, 1, -t), new(t, 0, -1), new(t, 0, 1), new(-t, 0, -1), new(-t, 0, 1),
        };
        for (int i = 0; i < p.Count; i++) p[i] = Vector3.Normalize(p[i]);
        var f = new List<int> { 0, 11, 5, 0, 5, 1, 0, 1, 7, 0, 7, 10, 0, 10, 11, 1, 5, 9, 5, 11, 4, 11, 10, 2, 10, 7, 6, 7, 1, 8,
            3, 9, 4, 3, 4, 2, 3, 2, 6, 3, 6, 8, 3, 8, 9, 4, 9, 5, 2, 4, 11, 6, 2, 10, 8, 6, 7, 9, 8, 1 };
        for (int s = 0; s < sub; s++)
        {
            var mid = new Dictionary<long, int>();
            int Mid(int a, int b)
            {
                long key = a < b ? ((long)a << 32) | (uint)b : ((long)b << 32) | (uint)a;
                if (mid.TryGetValue(key, out int m)) return m;
                p.Add(Vector3.Normalize((p[a] + p[b]) * 0.5f));
                mid[key] = p.Count - 1;
                return p.Count - 1;
            }
            var nf = new List<int>(f.Count * 4);
            for (int i = 0; i < f.Count; i += 3)
            {
                int a = f[i], b = f[i + 1], c = f[i + 2];
                int ab = Mid(a, b), bc = Mid(b, c), ca = Mid(c, a);
                nf.AddRange(new[] { a, ab, ca, b, bc, ab, c, ca, bc, ab, bc, ca });
            }
            f = nf;
        }
        return (p.ToArray(), f.ToArray());
    }
}

/// <summary>A placed rock or twig.</summary>
public readonly record struct PlacedRock(int Variant, Vector3 Position, float Yaw, Vector3 Scale, Vector3 Tint, bool Twig);

/// <summary>
/// Rock placement (stage env; PRESENTATION ONLY): boulders on hills and mountains, a few in forests and along river
/// banks, half buried (sunk by 25–45 % of their height); pebbles and twigs near the camera are in
/// <see cref="GroundCover"/>'s tiles. Deterministic per map seed.
/// </summary>
public static class RockLayout
{
    public const int BoulderVariants = 6;

    public static List<PlacedRock> Boulders(TerrainMeshData m, WaterField water, uint seed, IReadOnlyList<Exclusion> ex, bool arid)
    {
        var list = new List<PlacedRock>();
        var min = new Vector2(-m.HalfExtentX, -m.HalfExtentZ); var size = new Vector2(2 * m.HalfExtentX, 2 * m.HalfExtentZ);
        foreach (var p in PoissonDisk.Sample(min, size, 5.5f, seed ^ 0xB01u))
        {
            var cls = m.ClassAt(p.X, p.Y);
            int hx = (int)(p.X * 8), hz = (int)(p.Y * 8);
            float u = PNoise.U01(PNoise.Hash(hx, hz, seed ^ 0xB02u));
            float free = water.Freeboard(p.X, p.Y);
            float dens = cls switch
            {
                TerrainClass.Mountain => 0.30f, TerrainClass.Hills => 0.07f, TerrainClass.Forest or TerrainClass.Jungle => 0.03f,
                TerrainClass.Desert => 0.03f, TerrainClass.Beach => 0.02f, TerrainClass.Open => 0.006f, _ => 0f,
            };
            if (free < 2.5f && free > 0.15f) dens += 0.12f;   // river banks
            float clump = PNoise.Fbm(p.X / 40f, p.Y / 40f, seed ^ 0xB03u, 3);
            dens *= 0.4f + 1.4f * SplatModel.SmoothStep(0.4f, 0.75f, clump);
            if (u > dens || m.IsWet(p.X, p.Y) || free < 0.15f) continue;
            if (ForestLayout.Blocked(m, p.X, p.Y, 0.5f, 2.5f, ex)) continue;
            float r1 = PNoise.U01(PNoise.Hash(hx, hz, seed ^ 0xB04u)), r2 = PNoise.U01(PNoise.Hash(hx, hz, seed ^ 0xB05u));
            float s = 0.35f + 1.9f * r1 * r1 * (cls == TerrainClass.Mountain ? 1.4f : 1f);
            var scale = new Vector3(s, s * (0.8f + 0.4f * r2), s);
            float sink = scale.Y * (0.25f + 0.2f * r2) * 0.7f;
            float br = 0.85f + 0.3f * PNoise.U01(PNoise.Hash(hx, hz, seed ^ 0xB06u));
            var tint = arid ? new Vector3(1.12f, 1.0f, 0.86f) * br : new Vector3(br);
            list.Add(new PlacedRock((int)(PNoise.Hash(hx, hz, seed ^ 0xB07u) % BoulderVariants), new Vector3(p.X, m.HeightAt(p.X, p.Y) - sink, p.Y),
                PNoise.U01(PNoise.Hash(hx, hz, seed ^ 0xB08u)) * MathHelper.TwoPi, scale, tint, false));
        }
        return list;
    }
}

/// <summary>
/// Draws boulders (2 LODs, shadow casting) with the EnvRock effect: Rock023 photographic albedo/normal/roughness
/// (render1's bake, CC0) mapped triplanar in world space, moss on upward faces in temperate biomes, a darker damp
/// band where the rock meets the ground.
/// </summary>
public sealed class RockRenderer : IDisposable
{
    readonly GraphicsDevice _gd;
    readonly GpuMesh[,] _meshes;   // [variant, lod]
    readonly List<InstanceTransform>[] _inst;
    readonly List<Vector4>[] _spheres;
    readonly InstanceBuffer<InstanceTransform>[,] _bufs;
    readonly bool _arid;
    Texture2D? _alb, _nrm; bool _tried;
    public int LastInstances { get; private set; }
    public int Total { get; }
    public Texture2D? Albedo { get { EnsureTextures(); return _alb; } }
    public Texture2D? Normal { get { EnsureTextures(); return _nrm; } }

    public RockRenderer(GraphicsDevice gd, IReadOnlyList<PlacedRock> rocks, bool arid)
    {
        _gd = gd; _arid = arid;
        int n = RockLayout.BoulderVariants;
        _meshes = new GpuMesh[n, 2]; _inst = new List<InstanceTransform>[n]; _spheres = new List<Vector4>[n];
        _bufs = new InstanceBuffer<InstanceTransform>[n, 2];
        for (int v = 0; v < n; v++)
        {
            _meshes[v, 0] = GpuMesh.Create(gd, RockMeshes.Boulder(0x5EED0u + (uint)v * 31u, 3));
            _meshes[v, 1] = GpuMesh.Create(gd, RockMeshes.Boulder(0x5EED0u + (uint)v * 31u, 1));
            _inst[v] = new(); _spheres[v] = new();
            _bufs[v, 0] = new InstanceBuffer<InstanceTransform>(gd, 64); _bufs[v, 1] = new InstanceBuffer<InstanceTransform>(gd, 64);
        }
        foreach (var r in rocks)
        {
            var m = Matrix.CreateScale(r.Scale) * Matrix.CreateRotationY(r.Yaw) * Matrix.CreateTranslation(r.Position);
            _inst[r.Variant].Add(InstanceTransform.From(m, new Vector4(r.Tint, 1)));
            _spheres[r.Variant].Add(new Vector4(r.Position, 1.3f * MathF.Max(r.Scale.X, MathF.Max(r.Scale.Y, r.Scale.Z))));
        }
        Total = rocks.Count;
    }

    void EnsureTextures()
    {
        if (_tried) return;
        _tried = true;
        try
        {
            _alb = BakedTexture.Load(_gd, AssetRoot.Baked("render1/terrain/rock_alb_ht.r1tex"));
            _nrm = BakedTexture.Load(_gd, AssetRoot.Baked("render1/terrain/rock_nrm_rgh.r1tex"));
        }
        catch (Exception e) when (e is IOException or InvalidDataException) { _alb = null; _nrm = null; }
        for (int i = 0; i < 16; i++) _gd.Textures[i] = null;
    }

    public void Draw(RenderContext rc, bool depth)
    {
        if (rc.IsReflectionPass && depth) return;
        EnsureTextures();
        var e = rc.Effects.Get(depth ? EffectNames.InstancedDepth : EnvEffectNames.Rock);
        if (!depth)
        {
            var p = e.Parameters;
            p["RockAlb"].SetValue(_alb); p["RockNrm"].SetValue(_nrm);
            p["RockParams"].SetValue(new Vector4(_alb != null ? 1 : 0, _arid ? 0 : 1, 0, 0));
        }
        var cam = rc.Camera.Position; int total = 0;
        for (int v = 0; v < _inst.Length; v++)
        {
            var all = _inst[v]; if (all.Count == 0) continue;
            var sph = _spheres[v];
            int nn = 0, nf = 0;
            var near = new InstanceTransform[all.Count]; var far = new InstanceTransform[all.Count];
            for (int i = 0; i < all.Count; i++)
            {
                var s = sph[i];
                var frustum = depth && rc.ShadowFrustum is { } sf ? sf : rc.Frustum;
                if (!frustum.IntersectsSphere(new Vector3(s.X, s.Y, s.Z), s.W)) continue;
                float d = Vector3.Distance(cam, new Vector3(s.X, s.Y, s.Z));
                if (d > 1500f) continue;
                if (d < 35f + 18f * s.W) near[nn++] = all[i]; else far[nf++] = all[i];
            }
            if (nn > 0) { _bufs[v, 0].Set(near, nn); _meshes[v, 0].DrawInstanced(rc, e, _bufs[v, 0], nn); }
            if (nf > 0) { _bufs[v, 1].Set(far, nf); _meshes[v, 1].DrawInstanced(rc, e, _bufs[v, 1], nf); }
            total += nn + nf;
        }
        if (!depth) LastInstances = total;
    }

    public void Dispose()
    {
        foreach (var m in _meshes) m.Dispose();
        foreach (var b in _bufs) b.Dispose();
        _alb?.Dispose(); _nrm?.Dispose();
    }
}

/// <summary>The rock effect (see <see cref="RockRenderer"/>).</summary>
public static class RockEffects
{
    public const int AlbSlot = GlslLib.FirstMaterialSlot, NrmSlot = GlslLib.FirstMaterialSlot + 1;

    public static EffectBuilder Create() => new EffectBuilder(EnvEffectNames.Rock)
        .Attribute("vec4 a_pos", VertexElementUsage.Position, 0)
        .Attribute("vec3 a_nrm", VertexElementUsage.Normal, 0)
        .Attribute("vec4 a_col", VertexElementUsage.Color, 0)
        .AddInstanceTransform().AddEnvParams()
        .AddStandardLit().VertexLib(GlslLib.ShadowLookup)
        .Vec4("RockParams", Stage.Pixel) // x = textures present, y = moss on (temperate), z = 1 twig (dead wood)
        .Texture("RockAlb", AlbSlot).Texture("RockNrm", NrmSlot)
        .Varyings("vec3 v_wpos; vec3 v_nrm; vec3 v_tint; vec4 v_lpos; float v_h;")
        .VertexMain(@"
    vec3 wp = instPos(vec4(a_pos.xyz, 1.0));
    v_wpos = wp; v_nrm = normalize(instDir(a_nrm)); v_tint = i_tint.rgb; v_h = a_pos.y;
    v_lpos = shadowLookup(wp, v_nrm);
    gl_Position = ViewProj_mul(vec4(wp, 1.0));")
        .PixelLib(EnvGlsl.Common)
        .PixelMain(@"
    vec3 N = normalize(v_nrm);
    vec3 albedo; float rough; vec3 n = N;
    if (RockParams.x > 0.5) {
        // triplanar: weights from the normal, sharpened
        vec3 w = pow(abs(N), vec3(4.0)); w /= dot(w, vec3(1.0));
        float sc = 1.0 / 2.6;
        vec4 ax = texture2D(RockAlb, v_wpos.zy * sc), ay = texture2D(RockAlb, v_wpos.xz * sc), az = texture2D(RockAlb, v_wpos.xy * sc);
        vec4 nx = texture2D(RockNrm, v_wpos.zy * sc), ny = texture2D(RockNrm, v_wpos.xz * sc), nz = texture2D(RockNrm, v_wpos.xy * sc);
        albedo = decodeSrgb(ax.rgb) * w.x + decodeSrgb(ay.rgb) * w.y + decodeSrgb(az.rgb) * w.z;
        rough = nx.a * w.x + ny.a * w.y + nz.a * w.z;
        // whiteout-style blend of the three tangent normals into world space
        vec3 tx = decodeNormal(nx), ty = decodeNormal(ny), tz = decodeNormal(nz);
        vec3 bx = vec3(0.0, tx.y, tx.x) * sign(N.x), by = vec3(ty.x, 0.0, ty.y) * sign(N.y), bz = vec3(tz.x, tz.y, 0.0) * sign(N.z);
        n = normalize(N + bx * w.x + by * w.y + bz * w.z);
        float hgt = ax.a * w.x + ay.a * w.y + az.a * w.z;
        albedo *= mix(0.75, 1.05, hgt);
    } else { albedo = vec3(0.2, 0.2, 0.19); rough = 0.85; }
    if (RockParams.z > 0.5) {
        // twig: grey-brown dead wood with a little streaking along its length
        float st = 0.85 + 0.15 * sin(v_wpos.x * 37.0 + v_wpos.z * 29.0);
        albedo = vec3(0.075, 0.058, 0.042) * st; rough = 0.85; n = N;
    }
    albedo *= v_tint;
    // moss and lichen on upward faces (temperate), noise-broken
    if (RockParams.y > 0.5) {
        float up = smoothstep(0.55, 0.9, N.y);
        float mn = envHash(floor(v_wpos.xz * 3.0));
        float moss = up * smoothstep(0.25, 0.75, mn * 0.5 + 0.5 * sin(v_wpos.x * 1.7 + v_wpos.z * 2.3));
        albedo = mix(albedo, vec3(0.045, 0.065, 0.02), moss * 0.75);
        rough = mix(rough, 0.95, moss);
    }
    // damp, darker band where the rock enters the ground
    float base = 1.0 - smoothstep(-0.05, 0.25, v_h);
    albedo *= 1.0 - 0.35 * base;
    float ao = mix(0.6, 1.0, smoothstep(-0.2, 0.5, v_h));
    vec3 c = shadePbrFull(albedo, clamp(rough, 0.5, 1.0), 0.0, ao, n, N, v_wpos, shadowPcf(v_lpos));
    gl_FragColor = vec4(applyFog(c, v_wpos), 1.0);");
}
