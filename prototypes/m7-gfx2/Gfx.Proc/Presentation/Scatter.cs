using Gfx.Engine.Rendering;
using Gfx.Proc.Authoritative;
using Microsoft.Xna.Framework;

namespace Gfx.Proc.Presentation;

/// <summary>Bridson Poisson-disk sampling (PRESENTATION; seeded hash RNG, deterministic per seed).</summary>
public static class PoissonDisk
{
    public static List<Vector2> Sample(Vector2 min, Vector2 size, float r, uint seed, int k = 20)
    {
        float cs = r / MathF.Sqrt(2);
        int gw = (int)MathF.Ceiling(size.X / cs), gh = (int)MathF.Ceiling(size.Y / cs);
        var grid = new int[gw * gh]; Array.Fill(grid, -1);
        var pts = new List<Vector2>(); var active = new List<int>();
        uint ctr = 0;
        float Rnd() => PNoise.U01(PNoise.Hash(seed * 0x9E3779B9u + ctr++));
        void Add(Vector2 p) { int gi = (int)(p.Y / cs) * gw + (int)(p.X / cs); grid[gi] = pts.Count; active.Add(pts.Count); pts.Add(p); }
        Add(new Vector2(Rnd() * size.X, Rnd() * size.Y));
        while (active.Count > 0)
        {
            int ai = (int)(Rnd() * active.Count); if (ai >= active.Count) ai = active.Count - 1;
            var p = pts[active[ai]]; bool found = false;
            for (int t = 0; t < k && !found; t++)
            {
                float a = Rnd() * MathHelper.TwoPi, d = r * (1 + Rnd());
                var q = p + new Vector2(MathF.Cos(a), MathF.Sin(a)) * d;
                if (q.X < 0 || q.Y < 0 || q.X >= size.X || q.Y >= size.Y) continue;
                int qx = (int)(q.X / cs), qy = (int)(q.Y / cs); bool ok = true;
                for (int y = Math.Max(0, qy - 2); y <= Math.Min(gh - 1, qy + 2) && ok; y++)
                    for (int x = Math.Max(0, qx - 2); x <= Math.Min(gw - 1, qx + 2) && ok; x++)
                    {
                        int o = grid[y * gw + x];
                        if (o >= 0 && Vector2.DistanceSquared(pts[o], q) < r * r) ok = false;
                    }
                if (ok) { Add(q); found = true; }
            }
            if (!found) { active[ai] = active[^1]; active.RemoveAt(active.Count - 1); }
        }
        for (int i = 0; i < pts.Count; i++) pts[i] += min;
        return pts;
    }
}

/// <summary>One instanced prop variant: two LOD meshes, LOD switch / cull distances, instances and bounding spheres.</summary>
public sealed class PropGroup
{
    public required string Name { get; init; }
    public required MeshData<VertexPNC>[] Lods { get; init; }
    /// <summary>[0] near→far switch distance, [1] cull distance (m).</summary>
    public required float[] Distances { get; init; }
    public List<InstanceTransform> Instances { get; } = new();
    public List<Vector4> Spheres { get; } = new();
}

/// <summary>
/// Vegetation and rock scatter (PRESENTATION ONLY — never gameplay: the authoritative Forest/Jungle classes already
/// carry movement/cover; tree positions are decoration). Poisson-disk candidates thinned by a per-class density.
/// </summary>
public static class PropScatter
{
    // density by TerrainClass byte: Road, Open, Forest, Jungle, Hills, Mountain, Water, Wall, Beach, Desert
    static readonly float[] TreeDensity = { 0, 0.012f, 0.85f, 1f, 0.03f, 0, 0, 0, 0, 0.006f };
    static readonly float[] BushDensity = { 0, 0.10f, 0.25f, 0.45f, 0.12f, 0.03f, 0, 0, 0.03f, 0.05f };
    static readonly float[] RockDensity = { 0, 0.015f, 0.03f, 0.02f, 0.10f, 0.35f, 0, 0, 0.03f, 0.05f };

    public static TreeKind[] TreeKinds(Biome b) => b switch
    {
        Biome.Cold => new[] { TreeKind.Conifer, TreeKind.Conifer, TreeKind.Conifer },
        Biome.Tropical => new[] { TreeKind.Broadleaf, TreeKind.Broadleaf, TreeKind.Palm },
        Biome.Arid => new[] { TreeKind.Acacia, TreeKind.Acacia, TreeKind.Palm },
        _ => new[] { TreeKind.Broadleaf, TreeKind.Broadleaf, TreeKind.Conifer },
    };

    static Vector3 FoliageTint(Biome b) => b switch
    {
        Biome.Cold => new(0.85f, 0.95f, 0.95f), Biome.Tropical => new(0.9f, 1.1f, 0.85f), Biome.Arid => new(1.7f, 0.95f, 0.75f), _ => Vector3.One,
    };

    public static List<PropGroup> Build(TerrainMeshData t, uint seed)
    {
        var biome = t.Grid.Biome;
        var groups = new List<PropGroup>();
        var kinds = TreeKinds(biome);
        for (int i = 0; i < kinds.Length; i++)
            groups.Add(new PropGroup { Name = $"tree-{kinds[i]}-{i}", Lods = new[] { PropMeshes.Tree(kinds[i], seed + (uint)i * 31u, 0), PropMeshes.Tree(kinds[i], seed + (uint)i * 31u, 1) }, Distances = new[] { 110f, 900f } });
        int firstBush = groups.Count;
        for (int i = 0; i < 3; i++)
            groups.Add(new PropGroup { Name = $"bush-{i}", Lods = new[] { PropMeshes.Bush(seed + 100u + (uint)i, 0), PropMeshes.Bush(seed + 100u + (uint)i, 1) }, Distances = new[] { 60f, 350f } });
        int firstRock = groups.Count;
        for (int i = 0; i < 4; i++)
            groups.Add(new PropGroup { Name = $"rock-{i}", Lods = new[] { PropMeshes.Rock(seed + 200u + (uint)i, 0), PropMeshes.Rock(seed + 200u + (uint)i, 1) }, Distances = new[] { 70f, 500f } });

        var radius = new float[groups.Count]; var centerY = new float[groups.Count];
        for (int g = 0; g < groups.Count; g++)
        {
            var pts = groups[g].Lods[0].Vertices.Select(v => v.Position).ToArray();
            var bs = BoundingSphere.CreateFromPoints(pts); radius[g] = bs.Radius + bs.Center.Length(); centerY[g] = bs.Center.Y;
        }

        var min = new Vector2(-t.HalfExtentX, -t.HalfExtentZ); var size = new Vector2(2 * t.HalfExtentX, 2 * t.HalfExtentZ);
        var leaf = FoliageTint(biome);
        void Scatter(float r, uint salt, float[] density, int first, int count, Func<uint, float> scale, bool alignToGround, Vector3 tintBase)
        {
            foreach (var p in PoissonDisk.Sample(min, size, r, seed ^ salt))
            {
                var cls = t.ClassAt(p.X, p.Y);
                uint h = PNoise.Hash((int)(p.X * 16), (int)(p.Y * 16), seed ^ salt);
                float dens = density[(int)cls];
                if (cls == TerrainClass.Beach && biome == Biome.Tropical && first == 0) dens = 0.06f;
                if (PNoise.U01(h) >= dens || t.IsWet(p.X, p.Y)) continue;
                int g = first + (int)(PNoise.Hash(h) % (uint)count);
                float s = scale(PNoise.Hash(h + 1));
                float rot = PNoise.U01(PNoise.Hash(h + 2)) * MathHelper.TwoPi;
                var pos = new Vector3(p.X, t.HeightAt(p.X, p.Y) - 0.15f * s, p.Y);
                var m = Matrix.CreateScale(s) * Matrix.CreateRotationY(rot);
                if (alignToGround)
                {
                    var n = t.NormalAt(p.X, p.Y); var axis = Vector3.Cross(Vector3.Up, n);
                    if (axis.LengthSquared() > 1e-6f) m *= Matrix.CreateFromAxisAngle(Vector3.Normalize(axis), MathF.Acos(Math.Clamp(n.Y, -1, 1)));
                }
                m *= Matrix.CreateTranslation(pos);
                float br = 0.82f + 0.36f * PNoise.U01(PNoise.Hash(h + 3));
                var tint = tintBase * br * new Vector3(1 + 0.12f * (PNoise.U01(PNoise.Hash(h + 4)) - 0.5f), 1, 1 + 0.12f * (PNoise.U01(PNoise.Hash(h + 5)) - 0.5f));
                groups[g].Instances.Add(InstanceTransform.From(m, new Vector4(tint, 1)));
                groups[g].Spheres.Add(new Vector4(pos + Vector3.Up * centerY[g] * s, radius[g] * s));
            }
        }
        bool jungle = biome == Biome.Tropical;
        Scatter(4f, 0x7EEu, TreeDensity, 0, kinds.Length, h => (jungle ? 1.2f : 0.8f) + 0.45f * PNoise.U01(h), false, leaf);
        Scatter(3f, 0xB05u, BushDensity, firstBush, 3, h => 0.7f + 0.7f * PNoise.U01(h), true, leaf);
        var rockTint = biome == Biome.Arid ? new Vector3(1.25f, 1.0f, 0.8f) : Vector3.One;
        Scatter(5f, 0x50Cu, RockDensity, firstRock, 4, h => 0.5f + 2.2f * PNoise.U01(h) * PNoise.U01(h + 9), true, rockTint);
        return groups;
    }
}

public enum Precipitation { None, Rain, Snow, Dust }

/// <summary>
/// PRESENTATION-ONLY weather parameters (never read by the simulation). Fog is rendered (engine height fog);
/// precipitation is chosen but NOT rendered in G1 (particle quads were cut for budget — see report).
/// </summary>
public sealed record WeatherParams(Vector4 FogParams, float WindMps, Precipitation Precipitation, float Intensity)
{
    public static WeatherParams For(Biome b, float baseHeightM, uint seed)
    {
        float r = PNoise.U01(PNoise.Hash(seed ^ 0xFEEDu));
        return b switch
        {
            Biome.Arid => new(new Vector4(0.0011f + 0.0006f * r, 0.010f, baseHeightM, 0.75f), 4 + 6 * r, r > 0.6f ? Precipitation.Dust : Precipitation.None, r),
            Biome.Cold => new(new Vector4(0.0020f + 0.0010f * r, 0.015f, baseHeightM, 0.85f), 3 + 5 * r, r > 0.5f ? Precipitation.Snow : Precipitation.None, r),
            Biome.Tropical => new(new Vector4(0.0018f + 0.0008f * r, 0.014f, baseHeightM, 0.85f), 2 + 3 * r, r > 0.5f ? Precipitation.Rain : Precipitation.None, r),
            _ => new(new Vector4(0.0008f + 0.0006f * r, 0.012f, baseHeightM, 0.8f), 2 + 4 * r, r > 0.7f ? Precipitation.Rain : Precipitation.None, r),
        };
    }
}
