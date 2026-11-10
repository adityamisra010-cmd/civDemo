using System.Runtime.InteropServices;
using Gfx.Engine.Effects;
using Gfx.Engine.Mgfx;
using Gfx.Engine.Rendering;
using Gfx.Proc.Authoritative;
using Gfx.Proc.Environment.Trees;
using Gfx.Proc.Presentation;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;

namespace Gfx.Proc.Environment.Ground;

/// <summary>Cells of the ground-cover atlas (scripts/bake-env.py, section cover; drawn in code).</summary>
public enum CoverCard : byte
{
    GrassA, GrassB, GrassC, GrassD, DryA, DryB, Sedge, FernA, FernB, Daisy, Buttercup, Knapweed, Poppy, Dock, Thistle, Cushion,
}

/// <summary>Ground-cover card vertex: unit-space position (x across, y up, z depth), normal, card uv (0..1).</summary>
[StructLayout(LayoutKind.Sequential, Pack = 1)]
public struct VertexCover : IVertexType
{
    public Vector3 Position; public Vector3 Normal; public Vector2 Uv;
    public static readonly VertexDeclaration Decl = new(
        new VertexElement(0, VertexElementFormat.Vector3, VertexElementUsage.Position, 0),
        new VertexElement(12, VertexElementFormat.Vector3, VertexElementUsage.Normal, 0),
        new VertexElement(24, VertexElementFormat.Vector2, VertexElementUsage.TextureCoordinate, 0));
    readonly VertexDeclaration IVertexType.VertexDeclaration => Decl;
    public VertexCover(Vector3 p, Vector3 n, Vector2 uv) { Position = p; Normal = n; Uv = uv; }
}

/// <summary>Per tuft: x, y, z, yaw; width (m), height (m), atlas cell, rank (0..1, the thinning order); tint rgb, wind phase.</summary>
[StructLayout(LayoutKind.Sequential, Pack = 1)]
public struct CoverInstance : IVertexType
{
    public Vector4 PosYaw, Size, Tint;
    public static readonly VertexDeclaration Decl = new(
        new VertexElement(0, VertexElementFormat.Vector4, VertexElementUsage.TextureCoordinate, 1),
        new VertexElement(16, VertexElementFormat.Vector4, VertexElementUsage.TextureCoordinate, 2),
        new VertexElement(32, VertexElementFormat.Vector4, VertexElementUsage.TextureCoordinate, 3));
    readonly VertexDeclaration IVertexType.VertexDeclaration => Decl;
}

/// <summary>Distance thinning: all tufts within <see cref="Full"/> m, then the kept share falls to <see cref="MinKeep"/>
/// at <see cref="Thin"/> m (survivors grow to keep the coverage) and everything fades out by <see cref="Cull"/> m.</summary>
public sealed record CoverThinning(float Full = 14f, float Thin = 55f, float Cull = 85f, float MinKeep = 0.14f)
{
    public float Keep(float d) => d >= Cull ? 0f : d <= Full ? 1f : MathHelper.Lerp(1f, MinKeep, Math.Clamp((d - Full) / (Thin - Full), 0f, 1f));
}

/// <summary>
/// Ground cover (stage env; PRESENTATION ONLY): grass, flower, fern and weed cards from a drawn atlas, chosen by the
/// terrain material under each tuft (the same splat weights the terrain is drawn with) and thinned with distance.
/// The map is cut into 16 m tiles generated lazily around the camera; each tile's tufts are sorted by a random rank so
/// a distance-dependent share is a prefix of the list, and the vertex shader drops a tuft whose rank exceeds the share
/// kept at its own distance (no tile seams). Nothing grows in water or within 0.25 m above it (sedges grow only in
/// the band just above the waterline), on roads, or in an exclusion.
/// </summary>
public sealed class GroundCover : IDisposable
{
    public const float TileSize = 16f;
    sealed class Tile
    {
        public CoverInstance[] Tufts = Array.Empty<CoverInstance>(), Ferns = Array.Empty<CoverInstance>();
        public InstanceBuffer<CoverInstance>? TuftBuf, FernBuf;
        public InstanceTransform[] Pebbles = Array.Empty<InstanceTransform>(), Twigs = Array.Empty<InstanceTransform>();
        public InstanceBuffer<InstanceTransform>? PebbleBuf, TwigBuf;
        public Vector2 Min;
    }
    GpuMesh? _pebble, _twig;
    /// <summary>Pebbles and twigs are drawn within this distance (m).</summary>
    public float LitterDistance { get; set; } = 26f;
    public int LastLitter { get; private set; }

    readonly GraphicsDevice _gd;
    readonly TerrainMeshData _m;
    readonly float[] _splats;
    readonly WaterField _water;
    readonly IReadOnlyList<Exclusion> _ex;
    readonly uint _seed;
    readonly bool _arid;
    readonly Dictionary<(int, int), Tile> _tiles = new();
    readonly EnvTextures _tex;
    GpuMeshPlain? _tuft, _fern;
    public CoverThinning Thinning { get; set; } = new();
    public int LastTufts { get; private set; }
    public int LastTiles { get; private set; }
    public float WindStrength { get; set; } = 0.35f;
    public Vector3 WindDirection { get; set; } = Vector3.Normalize(new Vector3(0.8f, 0, 0.6f));

    public GroundCover(GraphicsDevice gd, TerrainMeshData mesh, float[] splats, WaterField water, IReadOnlyList<Exclusion> exclusions, uint seed, EnvTextures tex)
    {
        _gd = gd; _m = mesh; _splats = splats; _water = water; _ex = exclusions; _seed = seed; _tex = tex;
        _arid = mesh.Grid.Biome == Biome.Arid;
    }

    /// <summary>Splat weights at world (x, z), bilinear over the grid samples.</summary>
    public static void SplatAt(TerrainMeshData m, float[] splats, float x, float z, Span<float> w)
    {
        int W = m.Grid.Width, H = m.Grid.Height, n = SplatModel.Count;
        float fx = x / m.CellM + W / 2, fz = z / m.CellM + H / 2;
        int ix = (int)MathF.Floor(fx), iz = (int)MathF.Floor(fz);
        float tx = fx - ix, tz = fz - iz;
        w.Clear();
        for (int k = 0; k < 4; k++)
        {
            int sx = Math.Clamp(ix + (k & 1), 0, W - 1), sz = Math.Clamp(iz + (k >> 1), 0, H - 1);
            float wt = ((k & 1) == 1 ? tx : 1 - tx) * ((k >> 1) == 1 ? tz : 1 - tz);
            int o = (sz * W + sx) * n;
            for (int c = 0; c < n; c++) w[c] += splats[o + c] * wt;
        }
    }

    static float H01(uint seed, int a, int b) => PNoise.U01(PNoise.Hash(a, b, seed));

    /// <summary>Generates the tufts of tile (tx, tz) (deterministic: a hash of the tile, the cell and the map seed).</summary>
    public (List<CoverInstance> tufts, List<CoverInstance> ferns) Generate(int tx, int tz)
    {
        var tufts = new List<CoverInstance>(); var ferns = new List<CoverInstance>();
        const float step = 0.33f;
        int n = (int)(TileSize / step);
        Span<float> w = stackalloc float[SplatModel.Count];
        float x0 = tx * TileSize, z0 = tz * TileSize;
        for (int j = 0; j < n; j++)
            for (int i = 0; i < n; i++)
            {
                int gx = tx * n + i, gz = tz * n + j;
                float x = x0 + (i + H01(_seed ^ 0xC01u, gx, gz)) * step, z = z0 + (j + H01(_seed ^ 0xC02u, gx, gz)) * step;
                if (MathF.Abs(x) > _m.HalfExtentX - 2 || MathF.Abs(z) > _m.HalfExtentZ - 2) continue;
                float u = H01(_seed ^ 0xC03u, gx, gz);
                SplatAt(_m, _splats, x, z, w);
                float grass = w[(int)Material.Grass], dry = w[(int)Material.DryGrass], ff = w[(int)Material.ForestFloor];
                float mud = w[(int)Material.Mud], sand = w[(int)Material.Sand], dirt = w[(int)Material.Dirt], rock = w[(int)Material.Rock];
                float patch = PNoise.Fbm(x / 9f, z / 9f, _seed ^ 0xC04u, 3);
                float density = _arid
                    ? dry * (0.55f + 0.5f * patch) + grass * 0.45f + sand * 0.05f + dirt * 0.10f + rock * 0.03f
                    : grass * (0.85f + 0.3f * patch) + dry * 0.8f + ff * 0.38f * SplatModel.SmoothStep(0.35f, 0.65f, patch) + mud * 0.55f + sand * 0.06f + dirt * 0.05f + rock * 0.02f;
                // sedges and rushes grow in clumps along the waterline (a noise field picks the clumps, so the bank
                // is not lined with one continuous belt)
                float fb = _water.Freeboard(x, z);
                bool bank = !_arid && fb > 0.25f && fb < 1.1f;
                float reed = bank ? SplatModel.SmoothStep(0.42f, 0.68f, PNoise.Fbm(x / 5f + 1.7f, z / 5f - 4.2f, _seed ^ 0xC13u, 2)) : 0f;
                if (bank) density = MathF.Max(density, 0.45f + 0.45f * reed);
                // tufts per candidate cell: density is the probability that this cell holds one
                if (u > density) continue;
                float free = _water.Freeboard(x, z);
                if (free < 0.25f || _m.IsWet(x, z)) continue;
                var cls = _m.ClassAt(x, z);
                if (cls == TerrainClass.Road && H01(_seed ^ 0xC05u, gx, gz) > 0.08f) continue;
                if (Blocked(x, z)) continue;
                float pick = H01(_seed ^ 0xC06u, gx, gz);
                float sz = 0.8f + 0.45f * H01(_seed ^ 0xC07u, gx, gz);
                CoverCard card; float width, height; bool fern = false;
                if (bank && pick < 0.12f + 0.78f * reed) { card = CoverCard.Sedge; width = 0.8f; height = 0.95f; }
                else if (_arid)
                {
                    float r = H01(_seed ^ 0xC08u, gx, gz);
                    if (grass > dry && r < 0.4f) { card = CoverCard.GrassC; width = 0.5f; height = 0.32f; }
                    else if (r < 0.08f) { card = CoverCard.Cushion; width = 0.7f; height = 0.4f; }
                    else if (r < 0.12f) { card = CoverCard.Thistle; width = 0.55f; height = 0.7f; }
                    else { card = r < 0.6f ? CoverCard.DryA : CoverCard.DryB; width = 0.65f; height = 0.55f; }
                }
                else if (ff > 0.45f && pick < ff)
                {
                    float fpatch = PNoise.Fbm(x / 6f + 3.1f, z / 6f, _seed ^ 0xC09u, 2);
                    if (fpatch > 0.42f) { card = H01(_seed ^ 0xC0Au, gx, gz) < 0.55f ? CoverCard.FernA : CoverCard.FernB; width = 0.42f; height = 1.0f; fern = true; }
                    else { card = CoverCard.GrassC; width = 0.45f; height = 0.28f; }
                }
                else if (dry > grass) { card = pick < 0.5f ? CoverCard.DryA : CoverCard.DryB; width = 0.6f; height = 0.55f; }
                else
                {
                    // meadow: grasses, with flowers in patches (one species per patch) and a few weeds
                    float fl = PNoise.Fbm(x / 14f - 7.3f, z / 14f, _seed ^ 0xC0Bu, 3);
                    float r = H01(_seed ^ 0xC0Cu, gx, gz);
                    if (fl > 0.6f && r < 0.30f)
                    {
                        int sp = (int)(PNoise.Fbm(x / 30f, z / 30f + 11f, _seed ^ 0xC0Du, 2) * 4.0f) & 3;
                        card = (CoverCard)((int)CoverCard.Daisy + sp); width = 0.55f; height = 0.62f;
                    }
                    else if (r > 0.988f) { card = r > 0.995f ? CoverCard.Thistle : CoverCard.Dock; width = 0.6f; height = 0.7f; }
                    else
                    {
                        float g = H01(_seed ^ 0xC0Eu, gx, gz);
                        card = g < 0.34f ? CoverCard.GrassA : g < 0.44f ? CoverCard.GrassB : g < 0.80f ? CoverCard.GrassC : CoverCard.GrassD;
                        width = card == CoverCard.GrassC ? 0.45f : 0.55f; height = card == CoverCard.GrassB ? 0.62f : card == CoverCard.GrassC ? 0.30f : 0.45f;
                    }
                }
                float br = 0.82f + 0.3f * H01(_seed ^ 0xC0Fu, gx, gz);
                var tint = new Vector3(br * (1.0f + 0.08f * (patch - 0.5f)), br, br * (1.0f - 0.1f * (patch - 0.5f)));
                // sedge blades are paler and yellower than the drawn card in summer light
                if (card == CoverCard.Sedge) tint *= new Vector3(1.12f, 1.06f, 0.82f);
                var inst = new CoverInstance
                {
                    PosYaw = new Vector4(x, _m.HeightAt(x, z) - 0.03f, z, H01(_seed ^ 0xC10u, gx, gz) * MathHelper.TwoPi),
                    Size = new Vector4(width * sz, height * sz, (float)card, H01(_seed ^ 0xC11u, gx, gz)),
                    Tint = new Vector4(tint, H01(_seed ^ 0xC12u, gx, gz)),
                };
                (fern ? ferns : tufts).Add(inst);
            }
        // thinning order: a prefix of the rank-sorted list is the share kept at a distance
        tufts.Sort((a, b) => a.Size.W.CompareTo(b.Size.W));
        ferns.Sort((a, b) => a.Size.W.CompareTo(b.Size.W));
        return (tufts, ferns);
    }

    /// <summary>
    /// Pebbles (on gravel, tracks, river banks and rocky ground, some in grass) and twigs (on the forest floor) of a tile:
    /// small instanced meshes, aligned to the ground normal, partly sunk.
    /// </summary>
    public (List<InstanceTransform> pebbles, List<InstanceTransform> twigs) GenerateLitter(int tx, int tz)
    {
        var pebbles = new List<InstanceTransform>(); var twigs = new List<InstanceTransform>();
        const float step = 0.55f;
        int n = (int)(TileSize / step);
        Span<float> w = stackalloc float[SplatModel.Count];
        float x0 = tx * TileSize, z0 = tz * TileSize;
        for (int j = 0; j < n; j++)
            for (int i = 0; i < n; i++)
            {
                int gx = tx * n + i, gz = tz * n + j;
                float x = x0 + (i + H01(_seed ^ 0xD01u, gx, gz)) * step, z = z0 + (j + H01(_seed ^ 0xD02u, gx, gz)) * step;
                if (MathF.Abs(x) > _m.HalfExtentX - 2 || MathF.Abs(z) > _m.HalfExtentZ - 2) continue;
                SplatAt(_m, _splats, x, z, w);
                float free = _water.Freeboard(x, z);
                if (free < 0.02f || _m.IsWet(x, z) || Blocked(x, z)) continue;
                float bank = free < 1.2f ? 0.55f : 0f;
                float pDens = w[(int)Material.Dirt] * 0.30f + w[(int)Material.Rock] * 0.35f + w[(int)Material.Sand] * 0.10f + bank
                              + w[(int)Material.Grass] * 0.012f + w[(int)Material.DryGrass] * 0.04f + w[(int)Material.Mud] * 0.12f;
                float tDens = w[(int)Material.ForestFloor] * 0.22f;
                float u = H01(_seed ^ 0xD03u, gx, gz);
                var nrm = _m.NormalAt(x, z);
                var align = Matrix.Identity;
                var axis = Vector3.Cross(Vector3.Up, nrm);
                if (axis.LengthSquared() > 1e-6f) align = Matrix.CreateFromAxisAngle(Vector3.Normalize(axis), MathF.Acos(Math.Clamp(nrm.Y, -1f, 1f)));
                float yaw = H01(_seed ^ 0xD04u, gx, gz) * MathHelper.TwoPi;
                if (u < pDens)
                {
                    float s = 0.035f + 0.12f * MathF.Pow(H01(_seed ^ 0xD05u, gx, gz), 2.2f);
                    var sc = new Vector3(s, s * (0.55f + 0.4f * H01(_seed ^ 0xD06u, gx, gz)), s * (0.7f + 0.5f * H01(_seed ^ 0xD07u, gx, gz)));
                    var m = Matrix.CreateScale(sc) * Matrix.CreateRotationY(yaw) * align * Matrix.CreateTranslation(x, _m.HeightAt(x, z) - sc.Y * 0.35f, z);
                    float br = 0.75f + 0.45f * H01(_seed ^ 0xD08u, gx, gz);
                    pebbles.Add(InstanceTransform.From(m, new Vector4(br, br * 0.97f, br * 0.92f, 1)));
                }
                else if (u < pDens + tDens)
                {
                    float s = 0.25f + 0.55f * H01(_seed ^ 0xD09u, gx, gz);
                    var m = Matrix.CreateScale(s) * Matrix.CreateRotationY(yaw) * align * Matrix.CreateTranslation(x, _m.HeightAt(x, z) - 0.01f, z);
                    float br = 0.8f + 0.4f * H01(_seed ^ 0xD0Au, gx, gz);
                    twigs.Add(InstanceTransform.From(m, new Vector4(br, br, br, 2)));
                }
            }
        return (pebbles, twigs);
    }

    bool Blocked(float x, float z)
    {
        foreach (var e in _ex) if (Vector2.DistanceSquared(e.Centre, new Vector2(x, z)) < e.Radius * e.Radius) return true;
        return false;
    }

    Tile GetTile(int tx, int tz)
    {
        if (_tiles.TryGetValue((tx, tz), out var t)) return t;
        var (tufts, ferns) = Generate(tx, tz);
        var (pebbles, twigs) = GenerateLitter(tx, tz);
        t = new Tile { Tufts = tufts.ToArray(), Ferns = ferns.ToArray(), Pebbles = pebbles.ToArray(), Twigs = twigs.ToArray(), Min = new Vector2(tx * TileSize, tz * TileSize) };
        if (t.Pebbles.Length > 0) { t.PebbleBuf = new InstanceBuffer<InstanceTransform>(_gd, t.Pebbles.Length); t.PebbleBuf.Set(t.Pebbles); }
        if (t.Twigs.Length > 0) { t.TwigBuf = new InstanceBuffer<InstanceTransform>(_gd, t.Twigs.Length); t.TwigBuf.Set(t.Twigs); }
        if (t.Tufts.Length > 0) { t.TuftBuf = new InstanceBuffer<CoverInstance>(_gd, t.Tufts.Length); t.TuftBuf.Set(t.Tufts); }
        if (t.Ferns.Length > 0) { t.FernBuf = new InstanceBuffer<CoverInstance>(_gd, t.Ferns.Length); t.FernBuf.Set(t.Ferns); }
        _tiles[(tx, tz)] = t;
        return t;
    }

    void EnsureMeshes()
    {
        _tuft ??= GpuMeshPlain.Create(_gd, CoverMeshes.Tuft());
        _fern ??= GpuMeshPlain.Create(_gd, CoverMeshes.Fern());
    }

    public void DrawOpaque(RenderContext rc)
    {
        if (rc.IsReflectionPass || rc.IsShadowPass) return;
        EnsureMeshes();
        var cam = rc.Camera.Position;
        var th = Thinning;
        int r = (int)MathF.Ceiling(th.Cull / TileSize) + 1;
        int ctx = (int)MathF.Floor(cam.X / TileSize), ctz = (int)MathF.Floor(cam.Z / TileSize);
        var e = rc.Effects.Get(EnvEffectNames.Cover);
        var p = e.Parameters;
        p["CoverAlb"].SetValue(_tex.Get("cover_alb.r1tex")); p["CoverNrm"].SetValue(_tex.Get("cover_nrm_tr.r1tex", normalMap: true));
        p["CoverParams"].SetValue(new Vector4(th.Full, th.Thin, th.Cull, th.MinKeep));
        p["CoverEye"].SetValue(new Vector4(cam, 1));
        p["EnvParams"].SetValue(new Vector4(rc.SampleIndex, rc.SampleCount, rc.SampleCount > 1 ? 1 : 0, 0.5f));
        p["WindParams"].SetValue(new Vector4(rc.TimeSeconds, WindStrength, rc.Features.EnvWind ? 1 : 0, 0.3f));
        p["WindDir"].SetValue(new Vector4(WindDirection, 0));
        int tufts = 0, tiles = 0;
        for (int dz = -r; dz <= r; dz++)
            for (int dx = -r; dx <= r; dx++)
            {
                int tx = ctx + dx, tz = ctz + dz;
                var min = new Vector2(tx * TileSize, tz * TileSize);
                if (MathF.Abs(min.X + TileSize / 2) > _m.HalfExtentX + TileSize || MathF.Abs(min.Y + TileSize / 2) > _m.HalfExtentZ + TileSize) continue;
                // nearest point of the tile to the camera (horizontal) decides the kept prefix
                float nx = Math.Clamp(cam.X, min.X, min.X + TileSize), nz = Math.Clamp(cam.Z, min.Y, min.Y + TileSize);
                float d = MathF.Sqrt((nx - cam.X) * (nx - cam.X) + (nz - cam.Z) * (nz - cam.Z));
                if (d > th.Cull) continue;
                float h = _m.HeightAt(min.X + TileSize / 2, min.Y + TileSize / 2);
                var bmin = new Vector3(min.X, h - 12f, min.Y); var bmax = new Vector3(min.X + TileSize, h + 14f, min.Y + TileSize);
                if (!rc.Frustum.IntersectsBox(bmin, bmax)) continue;
                var t = GetTile(tx, tz);
                float keep = th.Keep(d);
                tufts += DrawList(rc, e, _tuft!, t.TuftBuf, t.Tufts, keep);
                tufts += DrawList(rc, e, _fern!, t.FernBuf, t.Ferns, keep);
                tiles++;
            }
        LastTufts = tufts; LastTiles = tiles;
        DrawLitter(rc, ctx, ctz);
    }

    void DrawLitter(RenderContext rc, int ctx, int ctz)
    {
        _pebble ??= GpuMesh.Create(_gd, RockMeshes.Boulder(0xBEB0u, 1));
        _twig ??= GpuMesh.Create(_gd, RockMeshes.Twig(0x7A1Bu));
        var cam = rc.Camera.Position;
        int r = (int)MathF.Ceiling(LitterDistance / TileSize) + 1, count = 0;
        var e = rc.Effects.Get(EnvEffectNames.Rock);
        e.Parameters["RockAlb"].SetValue(RockAlbedo); e.Parameters["RockNrm"].SetValue(RockNormal);
        for (int dz = -r; dz <= r; dz++)
            for (int dx = -r; dx <= r; dx++)
            {
                int tx = ctx + dx, tz = ctz + dz;
                var min = new Vector2(tx * TileSize, tz * TileSize);
                float nx = Math.Clamp(cam.X, min.X, min.X + TileSize), nz = Math.Clamp(cam.Z, min.Y, min.Y + TileSize);
                if ((nx - cam.X) * (nx - cam.X) + (nz - cam.Z) * (nz - cam.Z) > LitterDistance * LitterDistance) continue;
                float h = _m.HeightAt(min.X + TileSize / 2, min.Y + TileSize / 2);
                if (!rc.Frustum.IntersectsBox(new Vector3(min.X, h - 12f, min.Y), new Vector3(min.X + TileSize, h + 12f, min.Y + TileSize))) continue;
                if (!_tiles.TryGetValue((tx, tz), out var t)) continue;
                if (t.PebbleBuf != null)
                {
                    e.Parameters["RockParams"].SetValue(new Vector4(RockAlbedo != null ? 1 : 0, 0, 0, 0));
                    _pebble.DrawInstanced(rc, e, t.PebbleBuf, t.Pebbles.Length); count += t.Pebbles.Length;
                }
                if (t.TwigBuf != null)
                {
                    e.Parameters["RockParams"].SetValue(new Vector4(0, 0, 1, 0));
                    _twig.DrawInstanced(rc, e, t.TwigBuf, t.Twigs.Length); count += t.Twigs.Length;
                }
            }
        LastLitter = count;
    }

    /// <summary>Rock textures for the pebbles (set by the scenery; null draws flat grey stone).</summary>
    public Texture2D? RockAlbedo { get; set; }
    public Texture2D? RockNormal { get; set; }

    int DrawList(RenderContext rc, Effect e, GpuMeshPlain mesh, InstanceBuffer<CoverInstance>? buf, CoverInstance[] items, float keep)
    {
        if (buf == null || items.Length == 0 || keep <= 0) return 0;
        // prefix of the rank-sorted list: every instance whose rank is below the kept share
        int lo = 0, hi = items.Length;
        while (lo < hi) { int mid = (lo + hi) >> 1; if (items[mid].Size.W < keep) lo = mid + 1; else hi = mid; }
        int count = lo;
        if (count == 0) return 0;
        _gd.SetVertexBuffers(new VertexBufferBinding(mesh.Vertices, 0, 0), new VertexBufferBinding(buf.Buffer, 0, 1));
        _gd.Indices = mesh.Indices;
        rc.Apply(e);
        _gd.DrawInstancedPrimitives(PrimitiveType.TriangleList, 0, 0, mesh.Triangles, count);
        rc.Stats.RecordDraw(mesh.Triangles * count, count);
        return count;
    }

    public void Dispose()
    {
        foreach (var t in _tiles.Values) { t.TuftBuf?.Dispose(); t.FernBuf?.Dispose(); t.PebbleBuf?.Dispose(); t.TwigBuf?.Dispose(); }
        _tiles.Clear();
        _tuft?.Dispose(); _fern?.Dispose(); _pebble?.Dispose(); _twig?.Dispose();
    }
}

/// <summary>A small static mesh of any vertex type (vertex + index buffer).</summary>
public sealed class GpuMeshPlain : IDisposable
{
    public required VertexBuffer Vertices { get; init; }
    public required IndexBuffer Indices { get; init; }
    public required int Triangles { get; init; }
    public static GpuMeshPlain Create<T>(GraphicsDevice gd, MeshData<T> m) where T : struct, IVertexType
    {
        var vb = new VertexBuffer(gd, typeof(T), m.Vertices.Length, BufferUsage.WriteOnly); vb.SetData(m.Vertices);
        var ib = new IndexBuffer(gd, IndexElementSize.ThirtyTwoBits, m.Indices.Length, BufferUsage.WriteOnly); ib.SetData(m.Indices);
        return new GpuMeshPlain { Vertices = vb, Indices = ib, Triangles = m.TriangleCount };
    }
    public void Dispose() { Vertices.Dispose(); Indices.Dispose(); }
}

/// <summary>Unit card meshes: a tuft is three vertical crossed cards (0°, 60°, 120°); a fern is seven arching fronds.</summary>
public static class CoverMeshes
{
    public static MeshData<VertexCover> Tuft()
    {
        var v = new List<VertexCover>(); var ix = new List<int>();
        for (int k = 0; k < 3; k++)
        {
            float a = k * MathF.PI / 3;
            var d = new Vector3(MathF.Cos(a), 0, MathF.Sin(a)); var nrm = new Vector3(-d.Z, 0, d.X);
            var up = Vector3.Normalize(nrm * 0.35f + Vector3.Up);
            int b = v.Count;
            // two rows: base and top (slightly splayed outwards along the card normal sign is irrelevant: two-sided)
            v.Add(new VertexCover(-d * 0.5f, up, new Vector2(0, 1)));
            v.Add(new VertexCover(d * 0.5f, up, new Vector2(1, 1)));
            v.Add(new VertexCover(d * 0.55f + Vector3.Up, up, new Vector2(1, 0)));
            v.Add(new VertexCover(-d * 0.55f + Vector3.Up, up, new Vector2(0, 0)));
            ix.AddRange(new[] { b, b + 1, b + 2, b, b + 2, b + 3 });
        }
        return new MeshData<VertexCover>(v.ToArray(), ix.ToArray());
    }

    /// <summary>Seven fronds around the crown: each a quad from the centre out and up, bent in two segments (arching).</summary>
    public static MeshData<VertexCover> Fern()
    {
        var v = new List<VertexCover>(); var ix = new List<int>();
        for (int k = 0; k < 7; k++)
        {
            float a = k * MathHelper.TwoPi / 7 + 0.37f * (k % 2);
            var d = new Vector3(MathF.Cos(a), 0, MathF.Sin(a)); var side = new Vector3(-d.Z, 0, d.X);
            // spine: base, mid (rising steeply), tip (bending out and down a little); height 1, reach about 0.9
            float lean = 0.75f + 0.25f * (k % 3) / 2f;
            var p0 = Vector3.Zero;
            var p1 = d * 0.30f * lean + Vector3.Up * 0.62f;
            var p2 = d * 0.85f * lean + Vector3.Up * 0.78f;
            var n0 = Vector3.Normalize(-d * 0.6f + Vector3.Up);
            int b = v.Count;
            float w0 = 0.12f, w1 = 0.5f, w2 = 0.42f;
            v.Add(new VertexCover(p0 - side * w0, n0, new Vector2(0, 1)));
            v.Add(new VertexCover(p0 + side * w0, n0, new Vector2(1, 1)));
            v.Add(new VertexCover(p1 - side * w1, n0, new Vector2(0, 0.45f)));
            v.Add(new VertexCover(p1 + side * w1, n0, new Vector2(1, 0.45f)));
            v.Add(new VertexCover(p2 - side * w2, n0, new Vector2(0, 0)));
            v.Add(new VertexCover(p2 + side * w2, n0, new Vector2(1, 0)));
            ix.AddRange(new[] { b, b + 1, b + 3, b, b + 3, b + 2, b + 2, b + 3, b + 5, b + 2, b + 5, b + 4 });
        }
        return new MeshData<VertexCover>(v.ToArray(), ix.ToArray());
    }
}

/// <summary>
/// The ground-cover effect (stage env): cards placed and sized per instance in the vertex stage, thinned by rank and
/// distance (survivors grow so coverage holds, the last 15 % before the cull distance shrink to nothing), swayed by
/// travelling wind gusts; alpha tested (stochastic in jittered stills), lit like foliage with a normal bent towards the
/// sky, darker at the base (occlusion inside the tuft) and with transmitted sunlight.
/// </summary>
public static class CoverEffects
{
    public const int AlbSlot = GlslLib.FirstMaterialSlot, NrmSlot = GlslLib.FirstMaterialSlot + 1;

    public static EffectBuilder Create() => new EffectBuilder(EnvEffectNames.Cover)
        .Attribute("vec4 a_pos", VertexElementUsage.Position, 0)
        .Attribute("vec3 a_nrm", VertexElementUsage.Normal, 0)
        .Attribute("vec2 a_uv", VertexElementUsage.TextureCoordinate, 0)
        .Attribute("vec4 i_pos", VertexElementUsage.TextureCoordinate, 1)
        .Attribute("vec4 i_size", VertexElementUsage.TextureCoordinate, 2)
        .Attribute("vec4 i_tint", VertexElementUsage.TextureCoordinate, 3)
        .AddEnvParams()
        .Vec4("CoverParams", Stage.Vertex).Vec4("CoverEye", Stage.Vertex)
        .AddStandardLit().VertexLib(GlslLib.ShadowLookup)
        .Texture("CoverAlb", AlbSlot).Texture("CoverNrm", NrmSlot)
        .Varyings("vec3 v_wpos; vec3 v_nrm; vec2 v_uv; vec2 v_card; vec3 v_tint; vec4 v_lpos;")
        .VertexMain(@"
    float d = distance(CoverEye.xz, i_pos.xz);
    float keep = d >= CoverParams.z ? 0.0 : (d <= CoverParams.x ? 1.0 : mix(1.0, CoverParams.w, clamp((d - CoverParams.x) / (CoverParams.y - CoverParams.x), 0.0, 1.0)));
    if (i_size.w >= keep) {
        gl_Position = vec4(2.0, 2.0, 2.0, 1.0);
        v_wpos = vec3(0.0); v_nrm = vec3(0.0, 1.0, 0.0); v_uv = vec2(0.0); v_card = vec2(0.0); v_tint = vec3(0.0); v_lpos = vec4(0.0);
    } else {
        float grow = mix(1.0, inversesqrt(max(keep, 0.15)), 0.55);
        float tail = clamp((CoverParams.z - d) / (0.15 * CoverParams.z), 0.0, 1.0);
        vec2 sz = i_size.xy * grow * tail;
        float c = cos(i_pos.w), s = sin(i_pos.w);
        vec3 lp = vec3(a_pos.x * sz.x, a_pos.y * sz.y, a_pos.z * sz.x);
        vec3 wp = i_pos.xyz + vec3(c * lp.x + s * lp.z, lp.y, -s * lp.x + c * lp.z);
        if (WindParams.z > 0.5) {
            float t = WindParams.x;
            // gusts travel downwind across the meadow
            float wave = sin(dot(i_pos.xz, WindDir.xz) * 0.21 - t * 1.6 + i_tint.a * 0.8) * 0.5 + 0.5;
            float bend = a_pos.y * a_pos.y * sz.y * (0.10 + 0.25 * WindParams.y) * (0.35 + 0.65 * wave);
            wp += WindDir.xyz * bend + vec3(sin(t * 3.1 + i_tint.a * 6.28), 0.0, cos(t * 2.7 + i_tint.a * 6.28)) * (0.02 * a_pos.y * sz.y);
            wp.y -= bend * 0.35;
        }
        vec3 n = a_nrm; n = vec3(c * n.x + s * n.z, n.y, -s * n.x + c * n.z);
        float cell = i_size.z;
        vec2 origin = vec2(mod(cell, 4.0), floor(cell / 4.0)) * 0.25;
        v_uv = origin + clamp(a_uv, vec2(0.004), vec2(0.996)) * 0.25;
        v_card = a_uv;
        v_wpos = wp; v_nrm = normalize(n); v_tint = i_tint.rgb;
        v_lpos = shadowLookup(wp, vec3(0.0, 1.0, 0.0));
        gl_Position = ViewProj_mul(vec4(wp, 1.0));
    }")
        .PixelLib(EnvGlsl.Common).PixelLib(EnvGlsl.Foliage)
        .PixelMain(@"
    vec4 a = texture2D(CoverAlb, v_uv);
    if (a.a < alphaCut(vec2(5.0, 31.0))) discard;
    vec4 nt = texture2D(CoverNrm, v_uv);
    vec3 V = normalize(CameraPos.xyz - v_wpos);
    vec3 dpx = dFdx(v_wpos), dpy = dFdy(v_wpos);
    vec3 F = normalize(cross(dpx, dpy));
    if (dot(F, V) < 0.0) F = -F;
    vec2 dux = dFdx(v_uv), duy = dFdy(v_uv);
    vec3 dp2perp = cross(dpy, F), dp1perp = cross(F, dpx);
    vec3 Tt = dp2perp * dux.x + dp1perp * duy.x, Bt = dp2perp * dux.y + dp1perp * duy.y;
    float inv = inversesqrt(max(max(dot(Tt, Tt), dot(Bt, Bt)), 1e-12));
    vec3 m = decodeNormal(nt);
    vec3 Fd = normalize(Tt * inv * m.x - Bt * inv * m.y + F * m.z);
    // grass lies under the open sky: bend the shading normal towards up (it then matches the ground it grows on)
    vec3 N = normalize(mix(Fd, v_nrm, 0.62));
    vec3 albedo = decodeSrgb(a.rgb) * v_tint;
    float ao = mix(0.55, 1.0, smoothstep(0.0, 0.65, 1.0 - v_card.y));
    float sh = shadowPcf(v_lpos);
    vec3 c = shadeFoliage(albedo, N, v_wpos, sh, ao, 0.2);
    vec3 L = SunDir.xyz;
    float back = max(dot(-Fd, L), 0.0);
    float fwd = pow(max(dot(-V, L), 0.0), 5.0);
    vec3 trans = albedo / max(dot(albedo, vec3(0.2126, 0.7152, 0.0722)), 1e-4) * vec3(0.55, 0.75, 0.25) * (0.05 + 0.10 * nt.b);
    c += trans * SunColor.rgb * sh * (back * (1.0 + 2.0 * fwd)) / PI * ao;
    gl_FragColor = vec4(applyFog(c, v_wpos), 1.0);");
}
