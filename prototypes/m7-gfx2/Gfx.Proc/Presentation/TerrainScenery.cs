using Gfx.Engine.Rendering;
using Gfx.Engine.Scene;
using Gfx.Proc.Authoritative;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;

namespace Gfx.Proc.Presentation;

/// <summary>
/// GPU terrain set for one authoritative grid: chunked splat terrain (3 LODs + skirts, frustum + distance LOD),
/// water surface, instanced vegetation/rocks (frustum culled, 2 LODs, distance cull), biome palette, weather.
/// Reads the grid; never writes it. Use from a <see cref="DemoScene"/> or any <see cref="IRenderContent"/>.
/// </summary>
public sealed class TerrainScenery : IDisposable
{
    sealed class Chunk { public VertexBuffer Vb = null!; public IndexBuffer[] Ib = null!; public int[] Tris = null!; }
    sealed class GpuGroup
    {
        public PropGroup Data = null!; public GpuMesh[] Meshes = null!;
        public InstanceTransform[] All = null!; public Vector4[] Spheres = null!; public int[] Visible = null!;
        public InstanceTransform[] Near = null!, Far = null!; public InstanceBuffer<InstanceTransform> NearBuf = null!, FarBuf = null!;
    }

    readonly GraphicsDevice _gd;
    readonly Chunk[] _chunks; readonly BoundingBox[] _bounds; readonly (int chunk, int lod)[] _visible;
    readonly VertexBuffer? _waterVb; readonly IndexBuffer? _waterIb; readonly int _waterTris;
    readonly Texture2D _detailA, _detailB;
    readonly Vector3[] _palette;
    readonly List<GpuGroup> _groups = new();
    // G2 (stage render1): photographic materials and the far field, built on first use
    TerrainMaterials? _materials; bool _materialsTried;
    VertexBuffer? _farVb, _seaVb; IndexBuffer? _farIb, _seaIb; int _farTris, _seaTris; bool _farBuilt;
    readonly MaterialLook.Entry[] _look;
    /// <summary>Why the photographic materials are not in use (null when they are, or before the first G2 frame).</summary>
    public string? MaterialsFailure { get; private set; }
    public bool UsingPhotoMaterials => _materials != null;
    public FarFieldMesh? FarField { get; private set; }

    public BattleTerrainGrid Grid { get; }
    public TerrainMeshData Mesh { get; }
    public WeatherParams Weather { get; }
    public ChunkLod Lod { get; } = new(new[] { 300f, 700f, 6000f });
    const int LastLod = 2;
    public float TimeSeconds { get; set; }
    public bool DrawProps { get; set; } = true;
    // ---- stage env hooks (defaults keep the G1/render1 behaviour)
    /// <summary>When set, only prop groups whose name it accepts are drawn (e.g. "rock-0"; groups are tree-*, bush-*, rock-*).</summary>
    public Func<string, bool>? PropFilter { get; set; }
    /// <summary>False hides this scenery's own water surface (the environment stage draws its own).</summary>
    public bool DrawWaterSurface { get; set; } = true;
    /// <summary>True draws the terrain with the alternative splat weights given to <see cref="SetAltSplats"/>.</summary>
    public bool UseAltSplats { get; set; }
    /// <summary>Stage env: wet banks. Float texture of the nearby water surface height per grid sample (−10000 where none).</summary>
    public Texture2D? WetMap { get; set; }
    public bool UseWetMap { get; set; }
    Texture2D? _wetDummy;
    /// <summary>Material looks used with the alternative splats (null keeps the biome's own).</summary>
    public MaterialLook.Entry[]? AltLook { get; set; }
    VertexBuffer[]? _altVbs;
    public float ShadowCastDistance { get; set; } = 320f;
    public int LastVisibleChunks { get; private set; }
    public int LastPropInstances { get; private set; }
    public int TotalPropInstances { get; }

    public TerrainScenery(GraphicsDevice gd, BattleTerrainGrid grid)
    {
        _gd = gd; Grid = grid;
        Mesh = TerrainMeshData.Build(grid);
        uint seed = (uint)(grid.Seed ^ (grid.Seed >> 32));
        Weather = WeatherParams.For(grid.Biome, Mesh.MinHeightM, seed);
        _palette = SplatModel.Palette(grid.Biome);
        _look = MaterialLook.For(grid.Biome);
        (_detailA, _detailB) = MaterialTextures.Create(gd, 1234u);

        _chunks = new Chunk[Mesh.Chunks.Length]; _bounds = new BoundingBox[_chunks.Length]; _visible = new (int, int)[_chunks.Length];
        for (int i = 0; i < _chunks.Length; i++)
        {
            var c = Mesh.Chunks[i];
            var vb = new VertexBuffer(gd, VertexTerrain.Decl, c.Vertices.Length, BufferUsage.WriteOnly); vb.SetData(c.Vertices);
            var ibs = new IndexBuffer[c.Indices.Length]; var tris = new int[c.Indices.Length];
            for (int l = 0; l < ibs.Length; l++)
            {
                ibs[l] = new IndexBuffer(gd, IndexElementSize.ThirtyTwoBits, c.Indices[l].Length, BufferUsage.WriteOnly); ibs[l].SetData(c.Indices[l]);
                tris[l] = c.Indices[l].Length / 3;
            }
            _chunks[i] = new Chunk { Vb = vb, Ib = ibs, Tris = tris }; _bounds[i] = c.Bounds;
        }
        var w = Mesh.Water;
        if (w.Indices.Length > 0)
        {
            _waterVb = new VertexBuffer(gd, VertexWater.Decl, w.Vertices.Length, BufferUsage.WriteOnly); _waterVb.SetData(w.Vertices);
            _waterIb = new IndexBuffer(gd, IndexElementSize.ThirtyTwoBits, w.Indices.Length, BufferUsage.WriteOnly); _waterIb.SetData(w.Indices);
            _waterTris = w.TriangleCount;
        }
        foreach (var g in PropScatter.Build(Mesh, seed))
        {
            int n = g.Instances.Count;
            _groups.Add(new GpuGroup
            {
                Data = g, Meshes = new[] { GpuMesh.Create(gd, g.Lods[0]), GpuMesh.Create(gd, g.Lods[1]) },
                All = g.Instances.ToArray(), Spheres = g.Spheres.ToArray(), Visible = new int[n],
                Near = new InstanceTransform[n], Far = new InstanceTransform[n],
                NearBuf = new InstanceBuffer<InstanceTransform>(gd, Math.Max(1, n)), FarBuf = new InstanceBuffer<InstanceTransform>(gd, Math.Max(1, n)),
            });
            TotalPropInstances += n;
        }
    }

    public float HeightAt(float x, float z) => Mesh.HeightAt(x, z);
    /// <summary>
    /// G2 aerosol haze for this biome (multiplier on the clear-sky Mie density): humid and dusty biomes are hazier.
    /// Presentation only; scenes pass it to <see cref="RenderFeatures.Haze"/> in Configure.
    /// </summary>
    public float Haze => Grid.Biome switch { Biome.Arid => 3.5f, Biome.Tropical => 4.0f, Biome.Cold => 2.0f, _ => 3.0f };

    /// <summary>World box holding every shadow caster of this scenery (terrain and props, 40 m headroom for trees).</summary>
    public BoundingBox CasterBounds => new(new Vector3(-Mesh.HalfExtentX, Mesh.MinHeightM - 10f, -Mesh.HalfExtentZ),
                                           new Vector3(Mesh.HalfExtentX, Mesh.MaxHeightM + 40f, Mesh.HalfExtentZ));
    public IReadOnlyList<PropGroup> PropGroups => _groups.Select(g => g.Data).ToArray();

    public void DrawShadowCasters(RenderContext rc)
    {
        var depth = rc.Effects.Get(TerrainEffectNames.TerrainDepth);
        DrawChunks(rc, depth);
        // cascaded shadows: every cascade culls by its own light frustum, so trees cast up to the shadow distance
        float reach = rc.ShadowFrustum.HasValue ? MathF.Max(ShadowCastDistance, rc.Features.ShadowDistance) : ShadowCastDistance;
        if (DrawProps) DrawPropGroups(rc, rc.Effects.Get(Gfx.Engine.Effects.EffectNames.InstancedDepth), reach);
    }

    public void DrawOpaque(RenderContext rc)
    {
        bool photo = rc.IsG2Frame && rc.Features.PhotoTerrain && EnsureMaterials(rc.Device);
        Effect fx;
        if (photo) fx = PhotoTerrainEffect(rc);
        else
        {
            fx = rc.Effects.Get(TerrainEffectNames.TerrainSplat);
            for (int i = 0; i < _palette.Length; i++) fx.Parameters["MatColor" + i].SetValue(new Vector4(_palette[i], 1));
            fx.Parameters["TerrainParams"].SetValue(new Vector4(1f / 6f, 1f / 310f, 3f, 0));
            fx.Parameters["DetailA"].SetValue(_detailA); fx.Parameters["DetailB"].SetValue(_detailB);
        }
        LastVisibleChunks = DrawChunks(rc, fx);
        LastPropInstances = DrawProps ? DrawPropGroups(rc, rc.Effects.Get(Gfx.Engine.Effects.EffectNames.InstancedLit), float.MaxValue) : 0;
        if (rc.IsG2Frame && rc.Features.FarField) DrawFarField(rc);
        if (!DrawWaterSurface) return;
        if (rc.IsG2Frame) DrawWaterG2(rc); else DrawWater(rc);
    }

    bool EnsureMaterials(GraphicsDevice gd)
    {
        if (!_materialsTried)
        {
            _materialsTried = true;
            _materials = TerrainMaterials.TryLoad(gd, out var why);
            MaterialsFailure = why;
            // texture creation mid-frame re-binds GL units behind MonoGame's cache: force every slot to re-bind
            for (int i = 0; i < 16; i++) gd.Textures[i] = null;
        }
        return _materials != null;
    }

    Effect PhotoTerrainEffect(RenderContext rc)
    {
        var fx = rc.Effects.Get(TerrainEffectNames.TerrainPbr);
        var m = _materials!;
        var p = fx.Parameters;
        var look = UseAltSplats && AltLook != null ? AltLook : _look;
        for (int i = 0; i < 8; i++) p["MatTint" + i].SetValue(new Vector4(look[i].Tint, look[i].Saturation));
        p["MatRoughA"].SetValue(new Vector4(look[0].RoughMul, look[1].RoughMul, look[2].RoughMul, look[3].RoughMul));
        p["MatRoughB"].SetValue(new Vector4(look[4].RoughMul, look[5].RoughMul, look[6].RoughMul, look[7].RoughMul));
        var ts = TerrainMaterials.TileMetres;
        p["TileScaleA"].SetValue(new Vector4(1f / ts[0], 1f / ts[1], 1f / ts[2], 1f / ts[3]));
        p["TileScaleB"].SetValue(new Vector4(1f / ts[4], 1f / 190f, 1f / 53f, 0));
        bool wet = UseWetMap && WetMap != null;
        p["TerrainOpts"].SetValue(new Vector4(rc.Features.HexTiling ? 1 : 0, rc.Features.MacroVariation ? 1 : 0, 0.22f, wet ? 1 : 0));
        if (_wetDummy == null)
        {
            _wetDummy = new Texture2D(_gd, 1, 1, false, SurfaceFormat.Single);
            _wetDummy.SetData(new[] { -10000f });
            for (int i = 0; i < 16; i++) _gd.Textures[i] = null;
        }
        p["WetMap"].SetValue(wet ? WetMap : _wetDummy);
        float W = Grid.Width, H = Grid.Height, cell = Mesh.CellM;
        p["WetParams"].SetValue(new Vector4(1f / (cell * W), (W / 2 + 0.5f) / W, 1f / (cell * H), (H / 2 + 0.5f) / H));
        for (int i = 0; i < 5; i++) { p["Alb" + i].SetValue(m.AlbHt[i]); p["Nrm" + i].SetValue(m.NrmRgh[i]); }
        p["Macro"].SetValue(m.Macro);
        return fx;
    }

    void EnsureFarField(GraphicsDevice gd)
    {
        if (_farBuilt) return;
        _farBuilt = true;
        FarField = FarFieldMesh.Build(Mesh, (uint)(Grid.Seed ^ (Grid.Seed >> 32)));
        var land = FarField.Land;
        _farVb = new VertexBuffer(gd, VertexPNC.Decl, land.Vertices.Length, BufferUsage.WriteOnly); _farVb.SetData(land.Vertices);
        _farIb = new IndexBuffer(gd, IndexElementSize.ThirtyTwoBits, land.Indices.Length, BufferUsage.WriteOnly); _farIb.SetData(land.Indices);
        _farTris = land.TriangleCount;
        if (FarField.Sea is { } sea)
        {
            _seaVb = new VertexBuffer(gd, VertexWater.Decl, sea.Vertices.Length, BufferUsage.WriteOnly); _seaVb.SetData(sea.Vertices);
            _seaIb = new IndexBuffer(gd, IndexElementSize.ThirtyTwoBits, sea.Indices.Length, BufferUsage.WriteOnly); _seaIb.SetData(sea.Indices);
            _seaTris = sea.TriangleCount;
        }
    }

    void DrawFarField(RenderContext rc)
    {
        EnsureFarField(rc.Device);
        if (_farVb == null || _farIb == null) return;
        var fx = rc.Effects.Get(TerrainEffectNames.FarField);
        fx.Parameters["FarParams"].SetValue(new Vector4(FarFieldMesh.OuterHalfSize * 0.35f, FarFieldMesh.OuterHalfSize * 0.95f, 0, 0));
        fx.Parameters["Macro"]?.SetValue(_materials?.Macro ?? _detailA);
        _gd.SetVertexBuffer(_farVb); _gd.Indices = _farIb;
        rc.Apply(fx);
        _gd.DrawIndexedPrimitives(PrimitiveType.TriangleList, 0, 0, _farTris);
        rc.Stats.RecordDraw(_farTris, 1);
        if (_seaVb != null && _seaIb != null)
        {
            var w = rc.Effects.Get(TerrainEffectNames.WaterPbr);
            SetWaterParams(rc, w, farSea: true); // depth-driven opacity like the in-map sea, faded in across the map edge
            _gd.BlendState = BlendState.AlphaBlend; _gd.DepthStencilState = DepthStencilState.DepthRead;
            _gd.SetVertexBuffer(_seaVb); _gd.Indices = _seaIb;
            rc.Apply(w);
            _gd.DrawIndexedPrimitives(PrimitiveType.TriangleList, 0, 0, _seaTris);
            rc.Stats.RecordDraw(_seaTris, 1);
            _gd.BlendState = BlendState.Opaque; _gd.DepthStencilState = DepthStencilState.Default;
        }
    }

    void SetWaterParams(RenderContext rc, Effect fx, bool farSea)
    {
        bool arid = Grid.Biome == Biome.Arid || Grid.Biome == Biome.Tropical;
        fx.Parameters["WaterParams"].SetValue(new Vector4(TimeSeconds, rc.Features.WaterRippleFade ? 1 : 0, 0, farSea ? 1 : 0));
        // single-scattering albedo of the water body and its absorption per metre (red goes first)
        fx.Parameters["WaterScatter"].SetValue(arid ? new Vector4(0.035f, 0.075f, 0.075f, 0) : new Vector4(0.025f, 0.05f, 0.045f, 0));
        fx.Parameters["WaterAbsorb"].SetValue(new Vector4(0.45f, 0.20f, 0.13f, 0));
    }

    /// <summary>Draws this scenery's water surface (G1 or G2 path) on its own, for content that draws water after all opaque
    /// geometry (stage env). Use with <see cref="DrawWaterSurface"/> = false.</summary>
    public void DrawWaterOnly(RenderContext rc)
    {
        if (rc.IsG2Frame) DrawWaterG2(rc); else DrawWater(rc);
    }

    void DrawWaterG2(RenderContext rc)
    {
        if (_waterVb == null || _waterIb == null) return;
        var fx = rc.Effects.Get(TerrainEffectNames.WaterPbr);
        SetWaterParams(rc, fx, farSea: false);
        _gd.BlendState = BlendState.AlphaBlend; _gd.DepthStencilState = DepthStencilState.DepthRead;
        _gd.SetVertexBuffer(_waterVb); _gd.Indices = _waterIb;
        rc.Apply(fx);
        _gd.DrawIndexedPrimitives(PrimitiveType.TriangleList, 0, 0, _waterTris);
        rc.Stats.RecordDraw(_waterTris, 1);
        _gd.BlendState = BlendState.Opaque; _gd.DepthStencilState = DepthStencilState.Default;
    }

    int DrawChunks(RenderContext rc, Effect fx)
    {
        int n = Lod.Visible(rc.Frustum, rc.Camera.Position, _bounds, _visible);
        if (rc.IsShadowPass)
        {
            // every chunk casts, at the LOD the camera will see (a coarser caster mesh self-shadows the finer one);
            // with cascades, only the chunks inside this cascade's light frustum
            n = 0;
            for (int i = 0; i < _chunks.Length; i++)
            {
                var b = _bounds[i];
                if (rc.ShadowFrustum is { } sf && !sf.IntersectsBox(b.Min, b.Max)) continue;
                var cp = Vector3.Clamp(rc.Camera.Position, b.Min, b.Max);
                int l = Lod.Select(Vector3.Distance(rc.Camera.Position, cp));
                _visible[n++] = (i, l < 0 ? LastLod : l);
            }
        }
        for (int k = 0; k < n; k++)
        {
            var (ci, lod) = _visible[k]; var c = _chunks[ci];
            _gd.SetVertexBuffer(UseAltSplats && _altVbs != null ? _altVbs[ci] : c.Vb); _gd.Indices = c.Ib[lod];
            rc.Apply(fx);
            // the macro-variation texture is low-frequency: plain trilinear, no anisotropic taps
            if (fx.Name == TerrainEffectNames.TerrainPbr)
            {
                _gd.SamplerStates[G2TerrainEffects.MacroSlot] = SamplerState.LinearWrap;
                _gd.SamplerStates[G2TerrainEffects.WetSlot] = SamplerState.LinearClamp;
            }
            _gd.DrawIndexedPrimitives(PrimitiveType.TriangleList, 0, 0, c.Tris[lod]);
            rc.Stats.RecordDraw(c.Tris[lod], 1);
        }
        return n;
    }

    int DrawPropGroups(RenderContext rc, Effect fx, float maxDistance)
    {
        var cam = rc.Camera.Position; int total = 0;
        foreach (var g in _groups)
        {
            if (g.All.Length == 0) continue;
            if (PropFilter != null && !PropFilter(g.Data.Name)) continue;
            int vis = rc.IsShadowPass ? (rc.ShadowFrustum is { } sf ? sf.CullSpheres(g.Spheres, g.Visible) : FillAll(g)) : rc.Frustum.CullSpheres(g.Spheres, g.Visible);
            float near = g.Data.Distances[0], far = MathF.Min(g.Data.Distances[1], maxDistance);
            float near2 = near * near, far2 = far * far; int nn = 0, nf = 0;
            for (int i = 0; i < vis; i++)
            {
                int ix = g.Visible[i]; var s = g.Spheres[ix];
                float d2 = Vector3.DistanceSquared(cam, new Vector3(s.X, s.Y, s.Z));
                if (d2 > far2) continue;
                if (d2 <= near2) g.Near[nn++] = g.All[ix]; else g.Far[nf++] = g.All[ix];
            }
            g.NearBuf.Set(g.Near, nn); g.FarBuf.Set(g.Far, nf);
            if (nn > 0) g.Meshes[0].DrawInstanced(rc, fx, g.NearBuf, nn);
            if (nf > 0) g.Meshes[1].DrawInstanced(rc, fx, g.FarBuf, nf);
            total += nn + nf;
        }
        return total;
    }

    static int FillAll(GpuGroup g) { for (int i = 0; i < g.All.Length; i++) g.Visible[i] = i; return g.All.Length; }

    void DrawWater(RenderContext rc)
    {
        if (_waterVb == null || _waterIb == null) return;
        var fx = rc.Effects.Get(TerrainEffectNames.Water);
        bool arid = Grid.Biome == Biome.Arid || Grid.Biome == Biome.Tropical;
        fx.Parameters["WaterParams"].SetValue(new Vector4(TimeSeconds, 0, 0, 0));
        fx.Parameters["WaterShallow"].SetValue(arid ? new Vector4(0.10f, 0.32f, 0.30f, 1) : new Vector4(0.10f, 0.20f, 0.16f, 1));
        fx.Parameters["WaterDeep"].SetValue(arid ? new Vector4(0.01f, 0.07f, 0.12f, 1) : new Vector4(0.01f, 0.04f, 0.06f, 1));
        fx.Parameters["SkyZenith"].SetValue(new Vector4(rc.Sun.SkyZenith, 1));
        fx.Parameters["SkyHorizon"].SetValue(new Vector4(rc.Sun.SkyHorizon, 1));
        _gd.BlendState = BlendState.AlphaBlend; _gd.DepthStencilState = DepthStencilState.DepthRead;
        _gd.SetVertexBuffer(_waterVb); _gd.Indices = _waterIb;
        rc.Apply(fx);
        _gd.DrawIndexedPrimitives(PrimitiveType.TriangleList, 0, 0, _waterTris);
        rc.Stats.RecordDraw(_waterTris, 1);
        _gd.BlendState = BlendState.Opaque; _gd.DepthStencilState = DepthStencilState.Default;
    }

    /// <summary>
    /// Builds alternative chunk vertex buffers whose splat weights come from <paramref name="sampleSplats"/> (same layout as
    /// <see cref="TerrainMeshData.SampleSplats"/>: 8 weights per grid sample); null removes them. Positions are unchanged.
    /// </summary>
    public void SetAltSplats(float[]? sampleSplats)
    {
        if (_altVbs != null) foreach (var vb in _altVbs) vb.Dispose();
        _altVbs = null;
        if (sampleSplats == null) return;
        int W = Grid.Width, H = Grid.Height; float cell = Mesh.CellM;
        _altVbs = new VertexBuffer[_chunks.Length];
        for (int i = 0; i < _chunks.Length; i++)
        {
            var src = Mesh.Chunks[i].Vertices;
            var v = new VertexTerrain[src.Length];
            for (int k = 0; k < v.Length; k++)
            {
                v[k] = src[k];
                int gx = Math.Clamp((int)MathF.Round(src[k].Position.X / cell + W / 2), 0, W - 1);
                int gy = Math.Clamp((int)MathF.Round(src[k].Position.Z / cell + H / 2), 0, H - 1);
                int o = (gy * W + gx) * SplatModel.Count;
                v[k].SplatA = new Vector4(sampleSplats[o], sampleSplats[o + 1], sampleSplats[o + 2], sampleSplats[o + 3]);
                v[k].SplatB = new Vector4(sampleSplats[o + 4], sampleSplats[o + 5], sampleSplats[o + 6], sampleSplats[o + 7]);
            }
            _altVbs[i] = new VertexBuffer(_gd, VertexTerrain.Decl, v.Length, BufferUsage.WriteOnly);
            _altVbs[i].SetData(v);
        }
    }

    public void Dispose()
    {
        if (_altVbs != null) foreach (var vb in _altVbs) vb.Dispose();
        _wetDummy?.Dispose();
        foreach (var c in _chunks) { c.Vb.Dispose(); foreach (var ib in c.Ib) ib.Dispose(); }
        _waterVb?.Dispose(); _waterIb?.Dispose(); _detailA.Dispose(); _detailB.Dispose();
        _materials?.Dispose(); _farVb?.Dispose(); _farIb?.Dispose(); _seaVb?.Dispose(); _seaIb?.Dispose();
        foreach (var g in _groups) { g.Meshes[0].Dispose(); g.Meshes[1].Dispose(); g.NearBuf.Dispose(); g.FarBuf.Dispose(); }
    }
}
