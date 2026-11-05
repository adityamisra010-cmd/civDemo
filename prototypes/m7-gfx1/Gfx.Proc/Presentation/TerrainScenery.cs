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

    public BattleTerrainGrid Grid { get; }
    public TerrainMeshData Mesh { get; }
    public WeatherParams Weather { get; }
    public ChunkLod Lod { get; } = new(new[] { 300f, 700f, 6000f });
    const int LastLod = 2;
    public float TimeSeconds { get; set; }
    public bool DrawProps { get; set; } = true;
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
    public IReadOnlyList<PropGroup> PropGroups => _groups.Select(g => g.Data).ToArray();

    public void DrawShadowCasters(RenderContext rc)
    {
        var depth = rc.Effects.Get(TerrainEffectNames.TerrainDepth);
        DrawChunks(rc, depth);
        if (DrawProps) DrawPropGroups(rc, rc.Effects.Get(Gfx.Engine.Effects.EffectNames.InstancedDepth), ShadowCastDistance);
    }

    public void DrawOpaque(RenderContext rc)
    {
        var fx = rc.Effects.Get(TerrainEffectNames.TerrainSplat);
        for (int i = 0; i < _palette.Length; i++) fx.Parameters["MatColor" + i].SetValue(new Vector4(_palette[i], 1));
        fx.Parameters["TerrainParams"].SetValue(new Vector4(1f / 6f, 1f / 310f, 3f, 0));
        fx.Parameters["DetailA"].SetValue(_detailA); fx.Parameters["DetailB"].SetValue(_detailB);
        LastVisibleChunks = DrawChunks(rc, fx);
        LastPropInstances = DrawProps ? DrawPropGroups(rc, rc.Effects.Get(Gfx.Engine.Effects.EffectNames.InstancedLit), float.MaxValue) : 0;
        DrawWater(rc);
    }

    int DrawChunks(RenderContext rc, Effect fx)
    {
        int n = Lod.Visible(rc.Frustum, rc.Camera.Position, _bounds, _visible);
        if (rc.IsShadowPass)
        {
            // every chunk casts, at the LOD the camera will see (a coarser caster mesh self-shadows the finer one)
            n = _chunks.Length;
            for (int i = 0; i < n; i++)
            {
                var b = _bounds[i]; var cp = Vector3.Clamp(rc.Camera.Position, b.Min, b.Max);
                int l = Lod.Select(Vector3.Distance(rc.Camera.Position, cp));
                _visible[i] = (i, l < 0 ? LastLod : l);
            }
        }
        for (int k = 0; k < n; k++)
        {
            var (ci, lod) = _visible[k]; var c = _chunks[ci];
            _gd.SetVertexBuffer(c.Vb); _gd.Indices = c.Ib[lod];
            rc.Apply(fx);
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
            int vis = rc.IsShadowPass ? FillAll(g) : rc.Frustum.CullSpheres(g.Spheres, g.Visible);
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

    public void Dispose()
    {
        foreach (var c in _chunks) { c.Vb.Dispose(); foreach (var ib in c.Ib) ib.Dispose(); }
        _waterVb?.Dispose(); _waterIb?.Dispose(); _detailA.Dispose(); _detailB.Dispose();
        foreach (var g in _groups) { g.Meshes[0].Dispose(); g.Meshes[1].Dispose(); g.NearBuf.Dispose(); g.FarBuf.Dispose(); }
    }
}
