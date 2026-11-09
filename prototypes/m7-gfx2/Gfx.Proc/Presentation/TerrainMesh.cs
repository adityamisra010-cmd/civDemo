using Gfx.Engine.Rendering;
using Gfx.Proc.Authoritative;
using Microsoft.Xna.Framework;

namespace Gfx.Proc.Presentation;

/// <summary>One 64×64-cell chunk: shared vertex set (grid + skirt ring) and one index list per LOD.</summary>
public sealed class TerrainChunkData
{
    public required int ChunkX { get; init; }
    public required int ChunkY { get; init; }
    public required VertexTerrain[] Vertices { get; init; }
    /// <summary>Indices per LOD (step 1, 2, 4 samples), each including skirt triangles.</summary>
    public required int[][] Indices { get; init; }
    public required BoundingBox Bounds { get; init; }
}

/// <summary>
/// CPU terrain mesh built from the AUTHORITATIVE grid (read only). World layout: sample (ix, iy) sits at
/// x = (ix − W/2)·cell, z = (iy − H/2)·cell (grid y = 0 is north, −Z), height = HeightMm/1000 m.
/// Chunks of 64×64 cells, 3 LODs sharing one vertex buffer, skirts hide LOD T-junction cracks.
/// </summary>
public sealed class TerrainMeshData
{
    public const int ChunkCells = 64;
    public static readonly int[] LodSteps = { 1, 2, 4 };
    public const float SkirtDepthM = 6f;

    public BattleTerrainGrid Grid { get; }
    public float CellM { get; }
    public float MinHeightM { get; }
    public float MaxHeightM { get; }
    public TerrainChunkData[] Chunks { get; }
    public MeshData<VertexWater> Water { get; }
    /// <summary>Per-sample splat weights (same order as <see cref="Material"/>); exposed for tests.</summary>
    public float[] SampleSplats { get; }

    readonly float[] _h; readonly Vector3[] _n;

    TerrainMeshData(BattleTerrainGrid g)
    {
        Grid = g; CellM = g.CellMm / 1000f;
        int W = g.Width, H = g.Height;
        _h = new float[W * H]; _n = new Vector3[W * H];
        float mn = float.MaxValue, mx = float.MinValue;
        for (int i = 0; i < _h.Length; i++) { _h[i] = g.HeightMm[i] / 1000f; mn = MathF.Min(mn, _h[i]); mx = MathF.Max(mx, _h[i]); }
        MinHeightM = mn; MaxHeightM = mx;
        for (int y = 0; y < H; y++)
            for (int x = 0; x < W; x++)
                _n[y * W + x] = Vector3.Normalize(new Vector3(Hs(x - 1, y) - Hs(x + 1, y), 2 * CellM, Hs(x, y - 1) - Hs(x, y + 1)));
        SampleSplats = ComputeSplats();
        Chunks = BuildChunks();
        Water = BuildWater();
    }

    public static TerrainMeshData Build(BattleTerrainGrid grid) => new(grid);

    float Hs(int x, int y) => _h[Math.Clamp(y, 0, Grid.Height - 1) * Grid.Width + Math.Clamp(x, 0, Grid.Width - 1)];

    public Vector3 SamplePosition(int ix, int iy) => new((ix - Grid.Width / 2) * CellM, Hs(ix, iy), (iy - Grid.Height / 2) * CellM);
    public float HalfExtentX => Grid.Width / 2 * CellM;
    public float HalfExtentZ => Grid.Height / 2 * CellM;

    /// <summary>Bilinear terrain height (m) at world (x, z), clamped to the map.</summary>
    public float HeightAt(float x, float z)
    {
        float fx = x / CellM + Grid.Width / 2, fz = z / CellM + Grid.Height / 2;
        int ix = (int)MathF.Floor(fx), iz = (int)MathF.Floor(fz);
        float tx = Math.Clamp(fx - ix, 0, 1), tz = Math.Clamp(fz - iz, 0, 1);
        float a = Hs(ix, iz), b = Hs(ix + 1, iz), c = Hs(ix, iz + 1), d = Hs(ix + 1, iz + 1);
        return MathHelper.Lerp(MathHelper.Lerp(a, b, tx), MathHelper.Lerp(c, d, tx), tz);
    }

    public Vector3 NormalAt(float x, float z)
    {
        int ix = Math.Clamp((int)MathF.Round(x / CellM + Grid.Width / 2), 0, Grid.Width - 1);
        int iz = Math.Clamp((int)MathF.Round(z / CellM + Grid.Height / 2), 0, Grid.Height - 1);
        return _n[iz * Grid.Width + ix];
    }

    /// <summary>Authoritative class of the nearest cell (read only).</summary>
    public TerrainClass ClassAt(float x, float z) =>
        Grid.ClampedClass((int)MathF.Round(x / CellM + Grid.Width / 2), (int)MathF.Round(z / CellM + Grid.Height / 2));

    public bool IsWet(float x, float z)
    {
        int ix = Math.Clamp((int)MathF.Round(x / CellM + Grid.Width / 2), 0, Grid.Width - 1);
        int iz = Math.Clamp((int)MathF.Round(z / CellM + Grid.Height / 2), 0, Grid.Height - 1);
        return Grid.WaterDepthMm[Grid.Index(ix, iz)] > 0;
    }

    float[] ComputeSplats()
    {
        int W = Grid.Width, H = Grid.Height;
        var outW = new float[W * H * SplatModel.Count];
        var frac = new float[16]; Span<float> w = stackalloc float[SplatModel.Count];
        float range = MathF.Max(1f, MaxHeightM - MinHeightM);
        uint seed = (uint)(Grid.Seed & 0xFFFFFFFF);
        for (int y = 0; y < H; y++)
            for (int x = 0; x < W; x++)
            {
                Array.Clear(frac);
                for (int dy = -1; dy <= 1; dy++)
                    for (int dx = -1; dx <= 1; dx++)
                        frac[(int)Grid.ClampedClass(x + dx, y + dy)] += (dx == 0 ? 2 : 1) * (dy == 0 ? 2 : 1) / 16f;
                int i = y * W + x;
                var s = new SplatInputs
                {
                    ClassFraction = frac, Slope = 1 - _n[i].Y, HeightRel = (_h[i] - MinHeightM) / range, Biome = Grid.Biome,
                    Noise = PNoise.Fbm(x * CellM / 40f, y * CellM / 40f, seed ^ 0x5u, 3),
                };
                SplatModel.Weights(s, w);
                w.CopyTo(outW.AsSpan(i * SplatModel.Count, SplatModel.Count));
            }
        return outW;
    }

    TerrainChunkData[] BuildChunks()
    {
        int W = Grid.Width, H = Grid.Height, N = ChunkCells + 1;
        int cxn = (W + ChunkCells - 1) / ChunkCells, cyn = (H + ChunkCells - 1) / ChunkCells;
        var list = new List<TerrainChunkData>();
        for (int cy = 0; cy < cyn; cy++)
            for (int cx = 0; cx < cxn; cx++)
            {
                var v = new VertexTerrain[N * N + 4 * N];
                VertexTerrain Make(int vx, int vy, float drop)
                {
                    int gx = cx * ChunkCells + vx, gy = cy * ChunkCells + vy;
                    int sx = Math.Clamp(gx, 0, W - 1), sy = Math.Clamp(gy, 0, H - 1), si = sy * W + sx;
                    var sp = SampleSplats.AsSpan(si * SplatModel.Count, SplatModel.Count);
                    return new VertexTerrain
                    {
                        Position = new Vector3((gx - W / 2) * CellM, _h[si] - drop, (gy - H / 2) * CellM), Normal = _n[si],
                        SplatA = new Vector4(sp[0], sp[1], sp[2], sp[3]), SplatB = new Vector4(sp[4], sp[5], sp[6], sp[7]),
                    };
                }
                for (int vy = 0; vy < N; vy++) for (int vx = 0; vx < N; vx++) v[vy * N + vx] = Make(vx, vy, 0);
                for (int t = 0; t < N; t++)
                {
                    v[N * N + 0 * N + t] = Make(t, 0, SkirtDepthM);
                    v[N * N + 1 * N + t] = Make(t, ChunkCells, SkirtDepthM);
                    v[N * N + 2 * N + t] = Make(0, t, SkirtDepthM);
                    v[N * N + 3 * N + t] = Make(ChunkCells, t, SkirtDepthM);
                }
                var lods = new int[LodSteps.Length][];
                for (int l = 0; l < LodSteps.Length; l++) lods[l] = ChunkIndices(LodSteps[l]);
                var min = new Vector3(float.MaxValue); var max = new Vector3(float.MinValue);
                foreach (var p in v) { min = Vector3.Min(min, p.Position); max = Vector3.Max(max, p.Position); }
                list.Add(new TerrainChunkData { ChunkX = cx, ChunkY = cy, Vertices = v, Indices = lods, Bounds = new BoundingBox(min, max) });
            }
        return list.ToArray();
    }

    /// <summary>Index list for one LOD step: grid triangles plus skirt quads on all four edges.</summary>
    public static int[] ChunkIndices(int step)
    {
        int N = ChunkCells + 1, q = ChunkCells / step;
        var ix = new List<int>(q * q * 6 + 4 * q * 6);
        for (int y = 0; y < ChunkCells; y += step)
            for (int x = 0; x < ChunkCells; x += step)
            {
                int a = y * N + x, b = a + step, c = a + step * N, d = c + step;
                ix.Add(a); ix.Add(c); ix.Add(b); ix.Add(b); ix.Add(c); ix.Add(d);
            }
        for (int e = 0; e < 4; e++)
            for (int t = 0; t < ChunkCells; t += step)
            {
                int Top(int k) => e switch { 0 => k, 1 => ChunkCells * N + k, 2 => k * N, _ => k * N + ChunkCells };
                int Sk(int k) => N * N + e * N + k;
                ix.Add(Top(t)); ix.Add(Sk(t)); ix.Add(Top(t + step));
                ix.Add(Top(t + step)); ix.Add(Sk(t)); ix.Add(Sk(t + step));
            }
        return ix.ToArray();
    }

    MeshData<VertexWater> BuildWater()
    {
        int W = Grid.Width, H = Grid.Height, CW = W + 1;
        var map = new int[(W + 1) * (H + 1)]; Array.Fill(map, -1);
        var verts = new List<VertexWater>(); var idx = new List<int>();
        int Corner(int cx, int cy)
        {
            int ci = cy * CW + cx;
            if (map[ci] >= 0) return map[ci];
            float surf = 0, depth = 0; int wet = 0;
            for (int dy = -1; dy <= 0; dy++)
                for (int dx = -1; dx <= 0; dx++)
                {
                    int x = cx + dx, y = cy + dy;
                    if (x < 0 || y < 0 || x >= W || y >= H) continue;
                    int i = Grid.Index(x, y), d = Grid.WaterDepthMm[i];
                    if (d > 0) { surf += (Grid.HeightMm[i] + d) / 1000f; depth += d / 1000f; wet++; }
                }
            if (wet == 0)
                for (int dy = -2; dy <= 1; dy++)
                    for (int dx = -2; dx <= 1; dx++)
                    {
                        int x = cx + dx, y = cy + dy;
                        if (x < 0 || y < 0 || x >= W || y >= H) continue;
                        int i = Grid.Index(x, y), d = Grid.WaterDepthMm[i];
                        if (d > 0) { surf += (Grid.HeightMm[i] + d) / 1000f; wet++; }
                    }
            surf /= Math.Max(1, wet); depth /= 4f;
            map[ci] = verts.Count;
            verts.Add(new VertexWater(new Vector3((cx - 0.5f - W / 2) * CellM, surf, (cy - 0.5f - H / 2) * CellM), depth));
            return map[ci];
        }
        for (int y = 0; y < H; y++)
            for (int x = 0; x < W; x++)
            {
                // wet cells plus a one-cell skirt: the shoreline is then cut by the terrain depth test, not by cell edges
                bool near = false;
                for (int dy = -1; dy <= 1 && !near; dy++)
                    for (int dx = -1; dx <= 1 && !near; dx++)
                    {
                        int nx = x + dx, ny = y + dy;
                        near = nx >= 0 && ny >= 0 && nx < W && ny < H && Grid.WaterDepthMm[Grid.Index(nx, ny)] > 0;
                    }
                if (!near) continue;
                int a = Corner(x, y), b = Corner(x + 1, y), c = Corner(x, y + 1), d = Corner(x + 1, y + 1);
                idx.Add(a); idx.Add(c); idx.Add(b); idx.Add(b); idx.Add(c); idx.Add(d);
            }
        return new MeshData<VertexWater>(verts.ToArray(), idx.ToArray());
    }
}
