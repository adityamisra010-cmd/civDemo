using Gfx.Proc.Authoritative;
using Gfx.Proc.Presentation;
using Microsoft.Xna.Framework;

namespace Gfx.Proc.Environment.Trees;

/// <summary>A disc where nothing grows (settlements, structures, set pieces).</summary>
public readonly record struct Exclusion(Vector2 Centre, float Radius);

/// <summary>One placed tree before it is bound to a GPU variant.</summary>
public readonly record struct PlacedTree(TreeSpecies Species, int Variant, Vector3 Position, float Yaw, float Scale, Vector3 Tint, float Phase, byte Zone);

/// <summary>
/// Forest placement (stage env; PRESENTATION ONLY: the authoritative Forest/Jungle classes carry movement and cover;
/// these positions are decoration and are never read back). It reads the grid's classes, heights and water.
/// <list type="bullet">
/// <item>A smooth forest field: the signed distance (m) to the edge of the Forest/Jungle cells, domain-warped by noise
/// so the edge does not follow the 4 m cells.</item>
/// <item>Canopy trees (Poisson discs, 6.2 m) where the field is inside, thinned by a clearing noise (open glades of
/// 20–60 m) and a density noise; species in patches (low-frequency noise), conifers favoured on steep and high ground,
/// aspen and ash in the edge band; heights vary per tree, with young trees in the edge band and a few veterans.</item>
/// <item>An edge band (mantle) of shrubs and young pioneers from 6 m outside to 9 m inside the edge, and a sparse
/// shrub understory under the canopy.</item>
/// <item>Solitary trees and shrubs in the open, rare.</item>
/// <item>Arid biomes: maquis scrub and juniper in noise patches on desert and hills, and a few pines.</item>
/// </list>
/// Nothing grows in or within 2.5 m of water, on roads (± 2.5 m) or in an <see cref="Exclusion"/>.
/// Deterministic: every random number is a hash of the position and the map seed.
/// </summary>
public static class ForestLayout
{
    public sealed class Result
    {
        public List<PlacedTree> Trees { get; } = new();
        /// <summary>Signed distance (m) to the forest edge per grid sample (positive inside), after the warp.</summary>
        public required float[] ForestField { get; init; }
        /// <summary>Canopy cover 0..1 per grid sample (from the placed crowns).</summary>
        public required float[] Canopy { get; init; }
        public int Count(TreeSpecies sp) { int n = 0; foreach (var t in Trees) if (t.Species == sp) n++; return n; }
    }

    public const byte ZoneInterior = 0, ZoneEdge = 1, ZoneOpen = 2, ZoneUnder = 3;

    static float H01(uint seed, int a, int b) => PNoise.U01(PNoise.Hash(a, b, seed));

    /// <summary>Signed distance in metres to the Forest/Jungle boundary per sample (positive inside), exact within 60 m.</summary>
    public static float[] SignedDistance(BattleTerrainGrid g)
    {
        int W = g.Width, H = g.Height; float cell = g.CellMm / 1000f;
        var inside = new bool[W * H];
        for (int i = 0; i < inside.Length; i++) inside[i] = g.Class[i] is TerrainClass.Forest or TerrainClass.Jungle;
        int R = (int)MathF.Ceiling(60f / cell);
        var d = new float[W * H];
        // boundary cells: inside cells with an outside 4-neighbour (and vice versa); distance = to the nearest cell of the other kind
        for (int y = 0; y < H; y++)
            for (int x = 0; x < W; x++)
            {
                bool me = inside[y * W + x];
                float best = float.MaxValue;
                for (int r = 1; r <= R && best > (r - 1) * cell; r++)
                    for (int dy = -r; dy <= r; dy++)
                        for (int dx = -r; dx <= r; dx++)
                        {
                            if (Math.Max(Math.Abs(dx), Math.Abs(dy)) != r) continue;
                            int xx = x + dx, yy = y + dy;
                            if (xx < 0 || yy < 0 || xx >= W || yy >= H) continue;
                            if (inside[yy * W + xx] == me) continue;
                            float dist = MathF.Sqrt(dx * dx + dy * dy) * cell;
                            if (dist < best) best = dist;
                        }
                if (best == float.MaxValue) best = R * cell;
                // the edge lies half a cell between the two kinds
                d[y * W + x] = me ? best - cell * 0.5f : -(best - cell * 0.5f);
            }
        return d;
    }

    /// <summary>Bilinear sample of a per-grid-sample field at world (x, z).</summary>
    public static float Sample(float[] f, TerrainMeshData m, float x, float z)
    {
        int W = m.Grid.Width, H = m.Grid.Height;
        float fx = x / m.CellM + W / 2, fz = z / m.CellM + H / 2;
        int ix = (int)MathF.Floor(fx), iz = (int)MathF.Floor(fz);
        float tx = fx - ix, tz = fz - iz;
        float F(int a, int b) => f[Math.Clamp(b, 0, H - 1) * W + Math.Clamp(a, 0, W - 1)];
        return MathHelper.Lerp(MathHelper.Lerp(F(ix, iz), F(ix + 1, iz), tx), MathHelper.Lerp(F(ix, iz + 1), F(ix + 1, iz + 1), tx), tz);
    }

    /// <summary>The warped forest field at world (x, z): signed distance plus a 0..±7 m noise displacement of the edge.</summary>
    public static float Field(float[] sd, TerrainMeshData m, uint seed, float x, float z)
    {
        float wx = (PNoise.Fbm(x / 70f, z / 70f, seed ^ 0xA11u, 3) - 0.5f) * 16f;
        float wz = (PNoise.Fbm(x / 70f + 17.3f, z / 70f - 9.1f, seed ^ 0xA12u, 3) - 0.5f) * 16f;
        float edge = (PNoise.Fbm(x / 23f, z / 23f, seed ^ 0xA13u, 2) - 0.5f) * 7f;
        return Sample(sd, m, x + wx, z + wz) + edge;
    }

    /// <summary>True where nothing may grow: water (with a margin), roads (with a margin) or an exclusion.</summary>
    public static bool Blocked(TerrainMeshData m, float x, float z, float waterMargin, float roadMargin, IReadOnlyList<Exclusion> ex)
    {
        foreach (var e in ex) if (Vector2.DistanceSquared(e.Centre, new Vector2(x, z)) < e.Radius * e.Radius) return true;
        if (m.IsWet(x, z)) return true;
        float hw = MathF.Max(waterMargin, roadMargin);
        int steps = (int)MathF.Ceiling(hw / (m.CellM * 0.5f));
        for (int j = -steps; j <= steps; j++)
            for (int i = -steps; i <= steps; i++)
            {
                float px = x + i * m.CellM * 0.5f, pz = z + j * m.CellM * 0.5f;
                float dd = MathF.Sqrt((px - x) * (px - x) + (pz - z) * (pz - z));
                if (dd <= waterMargin && m.IsWet(px, pz)) return true;
                if (dd <= roadMargin && m.ClassAt(px, pz) == TerrainClass.Road) return true;
            }
        return false;
    }

    public static Result Build(TerrainMeshData m, uint seed, IReadOnlyList<Exclusion> exclusions, bool arid, bool place = true)
    {
        var g = m.Grid;
        var sd = SignedDistance(g);
        var field = new float[sd.Length];
        for (int y = 0; y < g.Height; y++)
            for (int x = 0; x < g.Width; x++)
            {
                var p = m.SamplePosition(x, y);
                field[y * g.Width + x] = Field(sd, m, seed, p.X, p.Z);
            }
        var res = new Result { ForestField = field, Canopy = new float[sd.Length] };
        var min = new Vector2(-m.HalfExtentX, -m.HalfExtentZ); var size = new Vector2(2 * m.HalfExtentX, 2 * m.HalfExtentZ);
        float range = MathF.Max(1f, m.MaxHeightM - m.MinHeightM);
        if (place && arid) BuildArid(m, seed, exclusions, res, min, size);
        else if (place) BuildTemperate(m, seed, exclusions, res, sd, min, size, range);
        // canopy cover per grid sample from the crowns (smooth falloff past the crown edge)
        foreach (var t in res.Trees)
        {
            if (t.Species.IsShrub) continue;
            float crown = t.Species == TreeSpecies.Pine || t.Species == TreeSpecies.AridPine ? 3.8f : 5.2f;
            float r = crown * t.Scale * (t.Species.VariantHeight(t.Variant) / 20f);
            int cx = (int)MathF.Round(t.Position.X / m.CellM + g.Width / 2), cz = (int)MathF.Round(t.Position.Z / m.CellM + g.Height / 2);
            int rr = (int)MathF.Ceiling((r + 2f) / m.CellM);
            for (int dz = -rr; dz <= rr; dz++)
                for (int dx = -rr; dx <= rr; dx++)
                {
                    int x = cx + dx, z = cz + dz;
                    if (x < 0 || z < 0 || x >= g.Width || z >= g.Height) continue;
                    var p = m.SamplePosition(x, z);
                    float dist = MathF.Sqrt((p.X - t.Position.X) * (p.X - t.Position.X) + (p.Z - t.Position.Z) * (p.Z - t.Position.Z));
                    float w = SplatModel.SmoothStep(r + 1f, r - 3f, dist);
                    ref float c = ref res.Canopy[z * g.Width + x];
                    c = 1f - (1f - c) * (1f - w * 0.85f);
                }
        }
        // stable order: by species name, variant, then position (z, x)
        res.Trees.Sort((a, b) =>
        {
            int c = string.CompareOrdinal(a.Species.Name, b.Species.Name);
            if (c != 0) return c;
            c = a.Variant.CompareTo(b.Variant); if (c != 0) return c;
            c = a.Position.Z.CompareTo(b.Position.Z); if (c != 0) return c;
            return a.Position.X.CompareTo(b.Position.X);
        });
        return res;
    }

    static void BuildTemperate(TerrainMeshData m, uint seed, IReadOnlyList<Exclusion> ex, Result res, float[] sd, Vector2 min, Vector2 size, float range)
    {
        // canopy trees
        foreach (var p in PoissonDisk.Sample(min, size, 6.2f, seed ^ 0x7A11u))
        {
            float f = Field(sd, m, seed, p.X, p.Y);
            if (f < -3f) continue;
            int hx = (int)(p.X * 8), hz = (int)(p.Y * 8);
            float u = H01(seed ^ 0x51u, hx, hz);
            // clearings: open glades inside the forest
            float glade = PNoise.Fbm(p.X / 85f, p.Y / 85f, seed ^ 0xC1Eu, 3);
            if (f > 14f && glade > 0.66f) continue;
            float dens = f < 9f ? 0.55f + 0.04f * f : 0.93f - 0.25f * SplatModel.SmoothStep(0.55f, 0.66f, glade);
            if (u > dens) continue;
            if (Blocked(m, p.X, p.Y, 2.5f, 2.5f, ex)) continue;
            var cls = m.ClassAt(p.X, p.Y);
            float slope = 1 - m.NormalAt(p.X, p.Y).Y;
            float hrel = (m.HeightAt(p.X, p.Y) - m.MinHeightM) / range;
            bool edge = f < 9f;
            if (cls == TerrainClass.Mountain && H01(seed ^ 0x52u, hx, hz) > 0.3f) continue;
            // species by patches: weights then a hashed pick
            float patch = PNoise.Fbm(p.X / 130f, p.Y / 130f, seed ^ 0x5Eu, 3), patch2 = PNoise.Fbm(p.X / 60f + 40f, p.Y / 60f, seed ^ 0x5Fu, 2);
            float wOak = 1.2f * SplatModel.SmoothStep(0.25f, 0.6f, patch) + 0.2f;
            float wAsh = 0.9f * SplatModel.SmoothStep(0.65f, 0.35f, patch) + 0.15f;
            float wPine = 1.3f * SplatModel.SmoothStep(0.45f, 0.75f, patch2) * (0.5f + hrel + 2f * slope) + (cls == TerrainClass.Mountain ? 2f : 0f);
            float wAspen = edge ? 1.1f : 0.12f * SplatModel.SmoothStep(0.5f, 0.66f, glade);
            if (edge) { wOak *= 0.6f; wPine *= 0.5f; wAsh *= 1.2f; }
            TreeSpecies sp = Pick(H01(seed ^ 0x53u, hx, hz), (TreeSpecies.Oak, wOak), (TreeSpecies.Ash, wAsh), (TreeSpecies.Pine, wPine), (TreeSpecies.Aspen, wAspen));
            float age = H01(seed ^ 0x54u, hx, hz);
            float scale = edge ? 0.62f + 0.3f * age : age > 0.97f ? 1.18f + 0.1f * H01(seed ^ 0x55u, hx, hz) : 0.86f + 0.24f * age;
            Add(res, m, sp, seed, hx, hz, p, scale, edge ? ZoneEdge : ZoneInterior);
        }
        // edge mantle and understory shrubs
        foreach (var p in PoissonDisk.Sample(min, size, 2.9f, seed ^ 0x5B1u))
        {
            float f = Field(sd, m, seed, p.X, p.Y);
            int hx = (int)(p.X * 8), hz = (int)(p.Y * 8);
            float u = H01(seed ^ 0x61u, hx, hz);
            float keep = f > -6f && f < 9f ? 0.62f * SplatModel.SmoothStep(-6f, -1f, f) * SplatModel.SmoothStep(9f, 3f, f) + 0.12f
                : f >= 9f ? 0.045f : 0.006f * (0.5f + PNoise.Fbm(p.X / 50f, p.Y / 50f, seed ^ 0x62u, 2));
            if (u > keep) continue;
            if (Blocked(m, p.X, p.Y, 2.0f, 2.0f, ex)) continue;
            if (m.ClassAt(p.X, p.Y) == TerrainClass.Mountain) continue;
            byte zone = f >= 9f ? ZoneUnder : f > -6f ? ZoneEdge : ZoneOpen;
            float scale = 0.75f + 0.45f * H01(seed ^ 0x63u, hx, hz);
            // a few young pioneer trees in the mantle
            if (zone == ZoneEdge && H01(seed ^ 0x64u, hx, hz) < 0.12f)
                Add(res, m, H01(seed ^ 0x65u, hx, hz) < 0.6f ? TreeSpecies.Aspen : TreeSpecies.Ash, seed, hx, hz, p, 0.42f + 0.25f * H01(seed ^ 0x66u, hx, hz), ZoneEdge);
            else Add(res, m, TreeSpecies.Shrub, seed, hx, hz, p, scale, zone);
        }
        // solitary trees in the open
        foreach (var p in PoissonDisk.Sample(min, size, 9f, seed ^ 0x501u))
        {
            float f = Field(sd, m, seed, p.X, p.Y);
            if (f > -14f) continue;
            int hx = (int)(p.X * 8), hz = (int)(p.Y * 8);
            float meadow = PNoise.Fbm(p.X / 110f, p.Y / 110f, seed ^ 0x502u, 3);
            if (H01(seed ^ 0x503u, hx, hz) > 0.035f * SplatModel.SmoothStep(0.45f, 0.7f, meadow)) continue;
            if (Blocked(m, p.X, p.Y, 3f, 3f, ex) || m.ClassAt(p.X, p.Y) == TerrainClass.Mountain) continue;
            var sp = H01(seed ^ 0x504u, hx, hz) < 0.7f ? TreeSpecies.Oak : TreeSpecies.Ash;
            Add(res, m, sp, seed, hx, hz, p, 1.0f + 0.15f * H01(seed ^ 0x505u, hx, hz), ZoneOpen);
        }
    }

    static void BuildArid(TerrainMeshData m, uint seed, IReadOnlyList<Exclusion> ex, Result res, Vector2 min, Vector2 size)
    {
        foreach (var p in PoissonDisk.Sample(min, size, 3.4f, seed ^ 0xA21u))
        {
            var cls = m.ClassAt(p.X, p.Y);
            if (cls is not (TerrainClass.Desert or TerrainClass.Hills or TerrainClass.Open or TerrainClass.Mountain)) continue;
            int hx = (int)(p.X * 8), hz = (int)(p.Y * 8);
            // maquis grows in patches: dense on hills and north-facing slopes, sparse on the open desert
            float patch = PNoise.Fbm(p.X / 75f, p.Y / 75f, seed ^ 0xA22u, 3);
            var n = m.NormalAt(p.X, p.Y);
            float north = MathF.Max(0, -n.Z) * 0.6f;
            float dens = (cls == TerrainClass.Hills ? 0.30f : cls == TerrainClass.Mountain ? 0.10f : 0.07f) * SplatModel.SmoothStep(0.38f, 0.68f, patch + north * 0.3f);
            if (H01(seed ^ 0xA23u, hx, hz) > dens) continue;
            if (Blocked(m, p.X, p.Y, 2.5f, 2.5f, ex)) continue;
            float u = H01(seed ^ 0xA24u, hx, hz);
            var sp = u < 0.62f ? TreeSpecies.Scrub : TreeSpecies.Juniper;
            Add(res, m, sp, seed, hx, hz, p, 0.7f + 0.55f * H01(seed ^ 0xA25u, hx, hz), ZoneOpen);
        }
        foreach (var p in PoissonDisk.Sample(min, size, 11f, seed ^ 0xA31u))
        {
            var cls = m.ClassAt(p.X, p.Y);
            if (cls is not (TerrainClass.Hills or TerrainClass.Desert)) continue;
            int hx = (int)(p.X * 8), hz = (int)(p.Y * 8);
            float patch = PNoise.Fbm(p.X / 140f, p.Y / 140f, seed ^ 0xA32u, 3);
            if (H01(seed ^ 0xA33u, hx, hz) > 0.07f * SplatModel.SmoothStep(0.5f, 0.75f, patch)) continue;
            if (Blocked(m, p.X, p.Y, 3f, 3f, ex)) continue;
            Add(res, m, TreeSpecies.AridPine, seed, hx, hz, p, 0.8f + 0.35f * H01(seed ^ 0xA34u, hx, hz), ZoneOpen);
        }
    }

    static TreeSpecies Pick(float u, params (TreeSpecies sp, float w)[] options)
    {
        float sum = 0; foreach (var o in options) sum += MathF.Max(0, o.w);
        float t = u * sum;
        foreach (var o in options) { t -= MathF.Max(0, o.w); if (t < 0) return o.sp; }
        return options[^1].sp;
    }

    static void Add(Result res, TerrainMeshData m, TreeSpecies sp, uint seed, int hx, int hz, Vector2 p, float scale, byte zone)
    {
        int variant = (int)(PNoise.Hash(hx, hz, seed ^ 0x71u) % (uint)sp.VariantCount);
        float yaw = H01(seed ^ 0x72u, hx, hz) * MathHelper.TwoPi;
        // per-tree colour: brightness and a slight hue shift (no cloned canopy)
        float br = 0.84f + 0.30f * H01(seed ^ 0x73u, hx, hz);
        float hue = (H01(seed ^ 0x74u, hx, hz) - 0.5f) * 0.16f;
        var tint = new Vector3(br * (1 + hue), br, br * (1 - hue * 1.2f));
        float h = m.HeightAt(p.X, p.Y);
        // sink the root flare slightly on slopes so the downhill side never floats
        var n = m.NormalAt(p.X, p.Y);
        float sink = 0.08f + 1.2f * (1 - n.Y) * scale;
        res.Trees.Add(new PlacedTree(sp, variant, new Vector3(p.X, h - sink, p.Y), yaw, scale, tint, H01(seed ^ 0x75u, hx, hz), zone));
    }
}
