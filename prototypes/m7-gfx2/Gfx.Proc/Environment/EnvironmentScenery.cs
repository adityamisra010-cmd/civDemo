using Gfx.Engine.Effects;
using Gfx.Engine.Rendering;
using Gfx.Proc.Authoritative;
using Gfx.Proc.Environment.Ground;
using Gfx.Proc.Environment.Structures;
using Gfx.Proc.Environment.Trees;
using Gfx.Proc.Environment.Water;
using Gfx.Proc.Presentation;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;

namespace Gfx.Proc.Environment;

/// <summary>Build options of an <see cref="EnvironmentScenery"/> (tests use smaller impostors and fewer variants).</summary>
public sealed record EnvironmentOptions(int ImpostorFrames = 8, int ImpostorFrameSize = 128, int MaxVariantsPerSpecies = 99, bool BakeImpostors = true)
{
    public static readonly EnvironmentOptions Default = new();
}

/// <summary>
/// The detailed battlefield environment (stage env; DR45/DR49 item 4) on one authoritative grid, which it only READS:
/// render1's terrain (<see cref="TerrainScenery"/>), plus ez-tree forests, ground cover, rocks, water and structures.
/// Every feature has a render switch (<see cref="RenderFeatures"/> <c>trees</c>, <c>groundcover</c>, <c>rocks</c>,
/// <c>water</c>, <c>structures</c>, …); a switch that is off draws the G1 content for that feature (G1's procedural
/// trees, bushes and rock blobs, render1's water, no structures). PRESENTATION ONLY.
/// </summary>
public sealed class EnvironmentScenery : IDisposable
{
    readonly GraphicsDevice _gd;
    public BattleTerrainGrid Grid { get; }
    public TerrainScenery Terrain { get; }
    public EnvTextures Textures { get; }
    public TreeMaterials TreeMaterials { get; }
    public ForestLayout.Result Forest { get; }
    public ForestRenderer Trees { get; }
    public WaterField Water { get; }
    public GroundCover Cover { get; }
    public RockRenderer Rocks { get; }
    public EnvWater WaterSurface { get; }
    public Vector2[] Flow { get; }
    public SettlementPlan? Settlement { get; }
    public StructureRenderer? Structures { get; }
    public double StructureBuildMs { get; }
    /// <summary>The terrain splat weights with the canopy-driven forest floor (what the ground cover grows on).</summary>
    public float[] EnvSplats { get; }
    public bool Arid { get; }
    public List<Exclusion> Exclusions { get; }
    /// <summary>Load-time costs (ms, this machine): tree models, impostor bakes, layout.</summary>
    public double TreeBuildMs { get; }
    public double ImpostorBakeMs { get; }
    public double LayoutMs { get; }

    static readonly Dictionary<(string, int), TreeModel> ModelCache = new();
    static readonly object CacheLock = new();

    /// <summary>The tree model of a species variant (CPU, deterministic; cached per process).</summary>
    public static TreeModel Model(TreeSpecies sp, int variant)
    {
        lock (CacheLock)
        {
            if (!ModelCache.TryGetValue((sp.Name, variant), out var m)) ModelCache[(sp.Name, variant)] = m = TreeModel.Build(sp, variant);
            return m;
        }
    }

    public EnvironmentScenery(GraphicsDevice gd, EffectLibrary fx, BattleTerrainGrid grid, IReadOnlyList<Exclusion>? exclusions = null,
        EnvironmentOptions? options = null, IReadOnlyList<PlacedTree>? extraTrees = null, bool forest = true, GenerationTrace? trace = null,
        Func<TerrainMeshData, WaterField, StructureKit?, SettlementPlan>? settlement = null)
    {
        options ??= EnvironmentOptions.Default;
        _gd = gd; Grid = grid;
        Terrain = new TerrainScenery(gd, grid);
        Textures = new EnvTextures(gd);
        TreeMaterials = new TreeMaterials(Textures);
        Arid = grid.Biome == Biome.Arid;
        Exclusions = exclusions?.ToList() ?? new List<Exclusion>();
        uint seed = (uint)(grid.Seed ^ (grid.Seed >> 32));

        Water = new WaterField(Terrain.Mesh);
        var sw = System.Diagnostics.Stopwatch.StartNew();
        if (settlement != null)
        {
            // plan first (exclusions for trees, rocks and ground cover), then build the geometry
            var kit = new StructureKit(Terrain.Mesh.HeightAt, seed ^ 0x5E77u);
            Settlement = settlement(Terrain.Mesh, Water, kit);
            Exclusions.AddRange(Settlement.Exclusions);
            Structures = new StructureRenderer(gd, kit.B, Textures);
        }
        StructureBuildMs = sw.Elapsed.TotalMilliseconds;
        sw.Restart();
        Forest = ForestLayout.Build(Terrain.Mesh, seed, Exclusions, Arid, forest);
        if (extraTrees != null) Forest.Trees.AddRange(extraTrees);
        LayoutMs = sw.Elapsed.TotalMilliseconds;

        // canopy-driven forest floor: replace the cell-based forest-floor weight by the canopy cover
        EnvSplats = ForestFloorSplats(Terrain.Mesh, Forest.Canopy);
        if (Settlement != null) TrampledGround(Terrain.Mesh, EnvSplats, Settlement, seed);
        Terrain.SetAltSplats(EnvSplats);
        Cover = new GroundCover(gd, Terrain.Mesh, EnvSplats, Water, Exclusions, seed, Textures);
        Rocks = new RockRenderer(gd, RockLayout.Boulders(Terrain.Mesh, Water, seed, Exclusions, Arid), Arid);
        Flow = FlowField.Build(grid, trace);
        WaterSurface = new EnvWater(gd, Terrain.Mesh, Water, Flow, Textures);
        Terrain.WetMap = WaterSurface.LevelMap;
        // forest floor under the canopy: dark leaf litter with moss, less red than the open-ground soil
        var look = MaterialLook.For(grid.Biome);
        look[(int)Material.ForestFloor] = look[(int)Material.ForestFloor] with { Tint = new Vector3(0.50f, 0.52f, 0.40f), Saturation = 0.62f };
        Terrain.AltLook = look;

        sw.Restart();
        Trees = new ForestRenderer(gd, TreeMaterials);
        var groups = new SortedDictionary<string, (TreeSpecies sp, int variant, List<TreeInstance> list)>(StringComparer.Ordinal);
        foreach (var t in Forest.Trees)
        {
            int v = Math.Min(t.Variant, options.MaxVariantsPerSpecies - 1);
            string key = t.Species.Name + "/" + v.ToString("D2", System.Globalization.CultureInfo.InvariantCulture);
            if (!groups.TryGetValue(key, out var g)) groups[key] = g = (t.Species, v, new List<TreeInstance>());
            g.list.Add(TreeInstance.From(t.Position, t.Yaw, t.Scale, t.Tint, t.Phase));
        }
        double bake = 0;
        foreach (var (_, g) in groups)
        {
            var model = Model(g.sp, g.variant);
            var gpu = new TreeVariantGpu(gd, model, options.ImpostorFrames, g.sp.IsShrub ? Math.Max(32, options.ImpostorFrameSize * 3 / 4) : options.ImpostorFrameSize);
            if (options.BakeImpostors) { gpu.BakeImpostors(gd, fx, TreeMaterials); bake += gpu.ImpostorBakeMs; }
            Trees.Add(gpu, g.list);
        }
        TreeBuildMs = sw.Elapsed.TotalMilliseconds - bake;
        ImpostorBakeMs = bake;
        if (Arid) { Trees.WindStrength = 0.45f; Cover.WindStrength = 0.5f; }
        // review aid: G2_ENV_LOD="lod1At,impostorAt" moves the switch distances (e.g. "1,2" draws every tree as an impostor)
        var lodEnv = System.Environment.GetEnvironmentVariable("G2_ENV_LOD");
        if (!string.IsNullOrWhiteSpace(lodEnv))
        {
            var parts = lodEnv.Split(',');
            Trees.Lod = Trees.Lod with
            {
                Lod1At = float.Parse(parts[0], System.Globalization.CultureInfo.InvariantCulture),
                ImpostorAt = float.Parse(parts[1], System.Globalization.CultureInfo.InvariantCulture),
            };
        }
    }

    /// <summary>
    /// The terrain's splat weights with the forest floor taken from the canopy cover (smooth, following the crowns)
    /// instead of the 3×3-blurred Forest/Jungle cells, which drew brown cell-aligned bands.
    /// </summary>
    public static float[] ForestFloorSplats(TerrainMeshData m, float[] canopy)
    {
        var src = (float[])m.SampleSplats.Clone(); var dst = new float[src.Length];
        int n = SplatModel.Count, ff = (int)Material.ForestFloor, grass = (int)Material.Grass;
        int rock = (int)Material.Rock, dirt = (int)Material.Dirt, dryGrass = (int)Material.DryGrass;
        var g = m.Grid;
        bool arid = g.Biome == Biome.Arid;
        // talus slopes (the generator's slope limit keeps them under about 31°) are Mountain cells: as bare rock they read
        // as a concrete channel. Below 38° keep a third of the rock as outcrops (noise-placed) and grow grass or scree on
        // the rest. Presentation only: the cells stay Mountain for movement and cover.
        for (int y = 0; y < g.Height; y++)
            for (int x = 0; x < g.Width; x++)
            {
                int i = y * g.Width + x, o = i * n;
                var p = m.SamplePosition(x, y);
                float slope = 1 - m.NormalAt(p.X, p.Z).Y;
                if (src[o + rock] < 0.05f || slope > 0.21f) continue;
                float outcrop = PNoise.Fbm(p.X / 18f, p.Z / 18f, (uint)g.Seed ^ 0xE5u, 3);
                float keep = SplatModel.SmoothStep(0.5f, 0.68f, outcrop) * 0.85f + 0.12f + SplatModel.SmoothStep(0.15f, 0.21f, slope) * 0.6f;
                float moved = src[o + rock] * (1f - Math.Clamp(keep, 0f, 1f));
                src[o + rock] -= moved;
                if (arid) { src[o + dryGrass] += moved * 0.55f; src[o + dirt] += moved * 0.45f; }
                else { src[o + grass] += moved * 0.75f; src[o + dirt] += moved * 0.25f; }
            }
        for (int i = 0; i < canopy.Length; i++)
        {
            int o = i * n;
            float old = src[o + ff];
            float rest = 1f - old;
            float c = Math.Clamp(canopy[i], 0f, 1f);
            float target = 0.88f * SplatModel.SmoothStep(0.35f, 0.92f, c);
            // other materials keep their proportions; where the cells said forest but no crown stands, grass returns
            for (int k = 0; k < n; k++) dst[o + k] = k == ff ? 0 : (rest > 1e-4f ? src[o + k] / rest : 0f) * (1f - target);
            if (rest <= 1e-4f) dst[o + grass] = 1f - target;
            dst[o + ff] = target;
        }
        return dst;
    }

    /// <summary>Inside a settlement the ground is trodden: bare soil and gravel near the buildings and along the ways.</summary>
    static void TrampledGround(TerrainMeshData m, float[] splats, SettlementPlan s, uint seed)
    {
        var g = m.Grid; int n = SplatModel.Count;
        for (int y = 0; y < g.Height; y++)
            for (int x = 0; x < g.Width; x++)
            {
                var p = m.SamplePosition(x, y);
                float d = Vector2.Distance(new Vector2(p.X, p.Z), s.Centre);
                if (d > s.Radius + 6) continue;
                float inside = SplatModel.SmoothStep(s.Radius + 4, s.Radius - 6, d);
                float noise = PNoise.Fbm(p.X / 7f, p.Z / 7f, seed ^ 0xD1u, 3);
                float dirt = inside * (0.45f + 0.5f * noise);
                int o = (y * g.Width + x) * n;
                for (int k = 0; k < n; k++) splats[o + k] *= 1 - dirt;
                splats[o + (int)Material.Dirt] += dirt * 0.35f;
                splats[o + (int)Material.Mud] += dirt * 0.45f;
                splats[o + (int)Material.Grass] += dirt * 0.2f;
            }
    }

    void Configure(RenderContext rc)
    {
        var f = rc.Features;
        bool trees = f.EnvTrees, rocks = f.EnvRocks;
        Terrain.PropFilter = name => name.StartsWith("rock", StringComparison.Ordinal) ? !rocks : !trees;
        Terrain.UseAltSplats = trees && f.EnvForestFloor;
        // water is drawn after every opaque layer (DrawTransparent), so trees and structures behind it are covered
        Terrain.DrawWaterSurface = false;
        Terrain.UseWetMap = f.EnvWater && rc.IsG2Frame;
    }

    bool EnvWaterOn(RenderContext rc) => rc.Features.EnvWater && rc.IsG2Frame && WaterSurface.Triangles > 0;

    public bool NeedsSceneColor(RenderContext rc) => EnvWaterOn(rc);

    public void DrawTransparent(RenderContext rc)
    {
        if (rc.IsReflectionPass) return;
        if (EnvWaterOn(rc)) WaterSurface.Draw(rc);
        else Terrain.DrawWaterOnly(rc);
    }

    /// <summary>
    /// The planar reflection plane: the water surface under the camera's target, or else at the nearest water in front
    /// of the camera (within 600 m). Null when no water is in view (no reflection pass).
    /// </summary>
    public Vector4? ReflectionPlane(RenderContext rc)
    {
        if (!EnvWaterOn(rc)) return null;
        var cam = rc.Camera;
        float s = Water.SurfaceAt(cam.Target.X, cam.Target.Z);
        if (float.IsNaN(s) || !Terrain.Mesh.IsWet(cam.Target.X, cam.Target.Z))
        {
            s = float.NaN;
            var fwd = cam.Forward; var dir = Vector2.Normalize(new Vector2(fwd.X, fwd.Z) + new Vector2(1e-5f, 0));
            for (float d = 2; d < 600; d += 2)
            {
                var q = new Vector2(cam.Position.X, cam.Position.Z) + dir * d;
                if (Terrain.Mesh.IsWet(q.X, q.Y)) { s = Water.SurfaceAt(q.X, q.Y); break; }
            }
        }
        if (float.IsNaN(s)) return null;
        return new Vector4(0, 1, 0, -s);
    }

    public void Update(float t) { Terrain.TimeSeconds = t; }

    /// <summary>Load-time costs (ms, measured on this machine) and content counts, one line for logs.</summary>
    public string LoadReport() => string.Format(System.Globalization.CultureInfo.InvariantCulture,
        "[env] load: forest layout {0:F0} ms, tree models {1:F0} ms, impostor bake {2:F0} ms, structures {3:F0} ms; trees {4} in {5} variants, boulders {6}, structure tris {7}, water tris {8}",
        LayoutMs, TreeBuildMs, ImpostorBakeMs, StructureBuildMs, Trees.TotalInstances, Trees.Variants.Count, Rocks.Total,
        Structures?.Triangles ?? 0, WaterSurface.Triangles);

    bool _drewTrees, _drewCover, _drewRocks;

    /// <summary>What the last main pass drew (instances per tree level, ground cover, litter, boulders; 0 for a part
    /// that was switched off), one line for logs.</summary>
    public string FrameReport() => string.Format(System.Globalization.CultureInfo.InvariantCulture,
        "[env] last frame: trees full {0}, reduced {1}, impostor {2} (tree tris {3}); cover cards {4} in {5} tiles; litter {6}; boulders {7}",
        _drewTrees ? Trees.LastPerLod[0] : 0, _drewTrees ? Trees.LastPerLod[1] : 0, _drewTrees ? Trees.LastPerLod[2] : 0, _drewTrees ? Trees.LastTriangles : 0,
        _drewCover ? Cover.LastTufts : 0, _drewCover ? Cover.LastTiles : 0, _drewCover ? Cover.LastLitter : 0, _drewRocks ? Rocks.LastInstances : 0);

    public void DrawShadowCasters(RenderContext rc)
    {
        Configure(rc);
        Terrain.DrawShadowCasters(rc);
        if (rc.Features.EnvTrees) Trees.DrawShadowCasters(rc);
        if (rc.Features.EnvRocks) Rocks.Draw(rc, depth: true);
        if (rc.Features.EnvStructures) Structures?.Draw(rc, depth: true);
    }

    public void DrawOpaque(RenderContext rc)
    {
        Configure(rc);
        if (!rc.IsReflectionPass) (_drewTrees, _drewCover, _drewRocks) = (rc.Features.EnvTrees, rc.Features.EnvGroundCover, rc.Features.EnvRocks);
        Terrain.DrawOpaque(rc);
        if (rc.Features.EnvTrees) Trees.DrawOpaque(rc);
        if (rc.Features.EnvRocks) Rocks.Draw(rc, depth: false);
        if (rc.Features.EnvStructures) Structures?.Draw(rc, depth: false);
        if (rc.Features.EnvGroundCover)
        {
            Cover.RockAlbedo = Rocks.Albedo; Cover.RockNormal = Rocks.Normal;
            Cover.DrawOpaque(rc);
        }
    }

    public void Dispose()
    {
        Trees.Dispose(); Cover.Dispose(); Rocks.Dispose(); WaterSurface.Dispose(); Structures?.Dispose(); Textures.Dispose(); Terrain.Dispose();
    }
}
