using Gfx.Engine.Cameras;
using Gfx.Engine.Diagnostics;
using Gfx.Engine.Rendering;
using Gfx.Engine.Scene;
using Gfx.Proc.Authoritative;
using Gfx.Proc.Environment;
using Gfx.Proc.Environment.Ground;
using Gfx.Proc.Environment.Structures;
using Gfx.Proc.Environment.Trees;
using Gfx.Proc.Environment.Water;
using Gfx.Proc.Presentation;
using Gfx.Proc.Scenes;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;

namespace Gfx.Tests;

/// <summary>
/// The stage-env test classes build forests, tree models and full environments (CPU heavy). They run in a collection of
/// their own, after the parallel phase, so they never compete with the perf stage's wall-clock tests
/// (`PerfDeviceTests.WarmupLastsAtLeastItsMinimumTime` failed once in a Debug run while they ran beside it).
/// </summary>
[CollectionDefinition(Name, DisableParallelization = true)]
public sealed class EnvSerialCollection { public const string Name = "Env (serial)"; }

/// <summary>Stage env (CPU): forest placement, ground cover, water flow, structures, LOD and impostor maths, switches.</summary>
[Collection(EnvSerialCollection.Name)]
public class EnvLayoutTests
{
    static readonly Lazy<(BattleTerrainGrid g, GenerationTrace t, TerrainMeshData m, WaterField w)> Temperate = new(() =>
    {
        var g = BattleTerrainGenerator.Generate(TerrainTemperateScene.FixtureInputs, out var t);
        var m = TerrainMeshData.Build(g);
        return (g, t, m, new WaterField(m));
    });
    static readonly Lazy<(BattleTerrainGrid g, TerrainMeshData m, WaterField w)> Arid = new(() =>
    {
        var g = BattleTerrainGenerator.Generate(TerrainAridScene.FixtureInputs);
        var m = TerrainMeshData.Build(g);
        return (g, m, new WaterField(m));
    });
    static uint Seed(BattleTerrainGrid g) => (uint)(g.Seed ^ (g.Seed >> 32));

    [Fact]
    public void ForestLayoutIsDeterministicAndKeepsOutOfWaterRoadsAndExclusions()
    {
        var (g, _, m, _) = Temperate.Value;
        var ex = new[] { new Exclusion(new Vector2(-60, 40), 50) };
        var a = ForestLayout.Build(m, Seed(g), ex, arid: false);
        var b = ForestLayout.Build(m, Seed(g), ex, arid: false);
        Assert.Equal(a.Trees.Count, b.Trees.Count);
        for (int i = 0; i < a.Trees.Count; i++) Assert.Equal(a.Trees[i], b.Trees[i]);
        Assert.True(a.Trees.Count > 2000, $"only {a.Trees.Count} trees");
        foreach (var t in a.Trees)
        {
            var p = new Vector2(t.Position.X, t.Position.Z);
            Assert.False(m.IsWet(p.X, p.Y), $"tree in water at {p}");
            Assert.NotEqual(TerrainClass.Road, m.ClassAt(p.X, p.Y));
            Assert.True(Vector2.Distance(p, ex[0].Centre) >= ex[0].Radius, $"tree inside the exclusion at {p}");
        }
    }

    [Fact]
    public void ForestHasAnEdgeMantleClearingsAndAMixOfSpecies()
    {
        var (g, _, m, _) = Temperate.Value;
        var r = ForestLayout.Build(m, Seed(g), Array.Empty<Exclusion>(), arid: false);
        var edge = r.Trees.Where(t => t.Zone == ForestLayout.ZoneEdge && !t.Species.IsShrub).ToArray();
        var inner = r.Trees.Where(t => t.Zone == ForestLayout.ZoneInterior).ToArray();
        Assert.NotEmpty(edge); Assert.NotEmpty(inner);
        // the mantle is lower: young pioneers and shrubs at the edge
        Assert.True(edge.Average(t => t.Scale) < inner.Average(t => t.Scale) - 0.1f, $"edge {edge.Average(t => t.Scale):F2} vs interior {inner.Average(t => t.Scale):F2}");
        Assert.True(r.Trees.Count(t => t.Species == TreeSpecies.Shrub && t.Zone == ForestLayout.ZoneEdge) > 200);
        // at least four species, none above 60 % of the canopy trees
        var canopy = r.Trees.Where(t => !t.Species.IsShrub).ToArray();
        var bySpecies = canopy.GroupBy(t => t.Species.Name).ToDictionary(x => x.Key, x => x.Count());
        Assert.True(bySpecies.Count >= 4, string.Join(",", bySpecies.Keys));
        Assert.True(bySpecies.Values.Max() < canopy.Length * 0.6, $"one species dominates: {bySpecies.Values.Max()} of {canopy.Length}");
        // clearings: deep inside the forest (field > 20 m) some samples have no canopy tree within 9 m
        var deep = new List<Vector2>();
        for (int y = 0; y < g.Height; y += 3) for (int x = 0; x < g.Width; x += 3) if (r.ForestField[y * g.Width + x] > 20) { var p = m.SamplePosition(x, y); deep.Add(new Vector2(p.X, p.Z)); }
        int open = deep.Count(p => !canopy.Any(t => Vector2.DistanceSquared(p, new Vector2(t.Position.X, t.Position.Z)) < 81));
        Assert.True(open > deep.Count * 0.04, $"no clearings: {open} of {deep.Count} deep samples open");
    }

    [Fact]
    public void ForestFloorFollowsTheCanopyNotTheCells()
    {
        var (g, _, m, _) = Temperate.Value;
        var r = ForestLayout.Build(m, Seed(g), Array.Empty<Exclusion>(), arid: false);
        var env = EnvironmentScenery.ForestFloorSplats(m, r.Canopy);
        int ff = (int)Material.ForestFloor, n = SplatModel.Count;
        // under full canopy: forest floor; forest cells without any crown (clearings): little forest floor
        int under = 0, underOk = 0, glade = 0, gladeOk = 0;
        for (int i = 0; i < r.Canopy.Length; i++)
        {
            if (r.Canopy[i] > 0.95f) { under++; if (env[i * n + ff] > 0.6f) underOk++; }
            if (g.Class[i] == TerrainClass.Forest && r.Canopy[i] < 0.05f) { glade++; if (env[i * n + ff] < 0.1f) gladeOk++; }
            float sum = 0; for (int k = 0; k < n; k++) sum += env[i * n + k];
            Assert.InRange(sum, 0.999f, 1.001f);
        }
        Assert.True(under > 100 && underOk > under * 0.95, $"{underOk}/{under}");
        Assert.True(glade > 20 && gladeOk == glade, $"{gladeOk}/{glade}");
        // it follows the trees, not the cells: under crowns that stand outside the Forest cells (the edge mantle and
        // solitary trees) the ground is forest floor too, where the cell-based splat had none
        int outside = 0, outsideOk = 0, cellHad = 0;
        for (int i = 0; i < r.Canopy.Length; i++)
        {
            if (g.Class[i] is TerrainClass.Forest or TerrainClass.Jungle || r.Canopy[i] < 0.8f) continue;
            outside++;
            if (env[i * n + ff] > 0.5f) outsideOk++;
            if (m.SampleSplats[i * n + ff] > 0.5f) cellHad++;
        }
        Assert.True(outside > 10 && outsideOk == outside, $"{outsideOk}/{outside} crowns outside the cells have forest floor");
        Assert.True(cellHad < outside / 2, $"the cell-based splat already had forest floor at {cellHad}/{outside}");
    }

    [Fact]
    public void LodLevelsCrossFadeWithExactComplements()
    {
        var lod = new TreeLodSettings();
        Span<(int level, float fade, float sign)> a = stackalloc (int, float, float)[2];
        float prev = -1;
        for (float d = 0; d < 300; d += 0.25f)
        {
            int n = ForestRenderer.Assign(d, 1f, lod, fade: true, impostors: true, a);
            if (n == 2)
            {
                Assert.Equal(a[0].fade, a[1].fade);
                Assert.Equal(1f, a[0].sign); Assert.Equal(-1f, a[1].sign);
                Assert.Equal(a[0].level + 1, a[1].level);
                // the dither test keeps a pixel in exactly one level for every dither value
                for (float dv = 0; dv < 1; dv += 0.01f) Assert.NotEqual(dv >= a[0].fade, dv < a[1].fade);
                if (a[0].level == 0) { Assert.True(a[0].fade >= prev - 1e-6f); prev = a[0].fade; }
            }
            else Assert.Equal(0f, a[0].sign);
        }
        // without the cross-fade one level, hard switches; without impostors the far level is LOD1
        Assert.Equal(1, ForestRenderer.Assign(lod.Lod1At, 1f, lod, fade: false, impostors: true, a));
        ForestRenderer.Assign(1000f, 1f, lod, fade: true, impostors: false, a);
        Assert.Equal(1, a[0].level);
        ForestRenderer.Assign(1000f, 1f, lod, fade: true, impostors: true, a);
        Assert.Equal(2, a[0].level);
    }

    [Fact]
    public void ImpostorFrameDirectionsRoundTripAndCoverTheHorizon()
    {
        const int n = 8;
        for (int j = 0; j < n; j++)
            for (int i = 0; i < n; i++)
            {
                var d = ImpostorMath.FrameDirection(i, j, n);
                Assert.InRange(d.Length(), 0.999f, 1.001f);
                Assert.True(d.Y >= -1e-5f);
                var g = ImpostorMath.Encode(d) * (n - 1);
                Assert.InRange(g.X, i - 1e-3f, i + 1e-3f); Assert.InRange(g.Y, j - 1e-3f, j + 1e-3f);
                var (r, u) = ImpostorMath.Basis(d);
                Assert.InRange(Vector3.Dot(r, u), -1e-4f, 1e-4f); Assert.InRange(Vector3.Dot(r, d), -1e-4f, 1e-4f);
                bool ring = i == 0 || j == 0 || i == n - 1 || j == n - 1;
                if (ring) Assert.InRange(d.Y, -1e-4f, 1e-4f);   // the outer ring of frames looks horizontally
            }
        // octahedral normal packing of the impostor atlas
        foreach (var v in new[] { Vector3.Up, Vector3.Down, Vector3.UnitX, Vector3.Normalize(new Vector3(0.3f, -0.8f, -0.5f)) })
            Assert.True(Vector3.Distance(v, ImpostorAtlas.OctDecode(ImpostorAtlas.OctEncode(v))) < 1e-4f);
    }

    [Fact]
    public void GroundCoverFollowsTheMaterialStaysOutOfWaterAndThinsByRank()
    {
        var (g, t, m, w) = Temperate.Value;
        var r = ForestLayout.Build(m, Seed(g), Array.Empty<Exclusion>(), arid: false);
        var splats = EnvironmentScenery.ForestFloorSplats(m, r.Canopy);
        var gc = new GroundCover(null!, m, splats, w, Array.Empty<Exclusion>(), Seed(g), null!);
        // tiles along the river and in a forest
        var mid = t.RiverCells[t.RiverCells.Length / 2];
        var rp = m.SamplePosition(mid % g.Width, mid / g.Width);
        int tx0 = (int)MathF.Floor(rp.X / GroundCover.TileSize), tz0 = (int)MathF.Floor(rp.Z / GroundCover.TileSize);
        int tufts = 0, sedges = 0, ferns = 0;
        Span<float> wts = stackalloc float[SplatModel.Count];
        for (int dz = -3; dz <= 3; dz++)
            for (int dx = -3; dx <= 3; dx++)
            {
                var (tu, fe) = gc.Generate(tx0 + dx, tz0 + dz);
                var again = gc.Generate(tx0 + dx, tz0 + dz);
                Assert.Equal(tu, again.tufts);
                for (int i = 1; i < tu.Count; i++) Assert.True(tu[i - 1].Size.W <= tu[i].Size.W, "not sorted by rank");
                foreach (var c in tu.Concat(fe))
                {
                    float fb = w.Freeboard(c.PosYaw.X, c.PosYaw.Z);
                    Assert.True(fb >= 0.25f, $"tuft {c.Size.Z} only {fb:F2} m above the water");
                    Assert.False(m.IsWet(c.PosYaw.X, c.PosYaw.Z));
                    if ((CoverCard)(int)c.Size.Z == CoverCard.Sedge) { sedges++; Assert.True(fb < 1.15f, $"sedge {fb:F2} m above water"); }
                }
                foreach (var c in fe)
                {
                    ferns++;
                    GroundCover.SplatAt(m, splats, c.PosYaw.X, c.PosYaw.Z, wts);
                    Assert.True(wts[(int)Material.ForestFloor] > 0.45f, "fern off the forest floor");
                }
                tufts += tu.Count;
            }
        Assert.True(tufts > 1000 && sedges > 20, $"tufts {tufts}, sedges {sedges}");
        // the kept share falls with distance and is zero past the cull distance
        var th = new CoverThinning();
        float last = 2;
        for (float d = 0; d < th.Cull + 5; d += 1) { float k = th.Keep(d); Assert.True(k <= last + 1e-6f); last = k; }
        Assert.Equal(0f, th.Keep(th.Cull));
        _ = ferns;
    }

    [Fact]
    public void RiverFlowRunsDownstream()
    {
        var (g, t, _, _) = Temperate.Value;
        var flow = FlowField.Build(g, t);
        int checkedCells = 0;
        for (int k = 3; k + 3 < t.RiverCells.Length; k++)
        {
            int c = t.RiverCells[k], nx = t.RiverCells[k + 3], pv = t.RiverCells[k - 3];
            var down = new Vector2(nx % g.Width - pv % g.Width, nx / g.Width - pv / g.Width);
            var f = flow[c];
            Assert.True(f.Length() > 0.1f, $"no flow at path cell {k}");
            Assert.True(Vector2.Dot(Vector2.Normalize(f), Vector2.Normalize(down)) > 0.7f, $"flow against the river at {k}");
            checkedCells++;
        }
        Assert.True(checkedCells > 100);
        // dry cells carry no flow
        for (int i = 0; i < g.Width * g.Height; i++) if (g.WaterDepthMm[i] == 0) Assert.Equal(Vector2.Zero, flow[i]);
    }

    [Fact]
    public void ObliqueProjectionClipsAtTheWaterPlane()
    {
        var cam = new CameraPose(new Vector3(0, 8, 20), new Vector3(0, 0, -10), 0.9f, 0.3f, 4000f);
        float h = 1.5f;
        var mirror = Matrix.CreateReflection(new Plane(Vector3.Up, -h));
        var view = mirror * cam.View;
        var proj = cam.Projection(16f / 9f);
        var pv = Vector3.Transform(new Vector3(0, h, 0), view); var nv = Vector3.Normalize(Vector3.TransformNormal(Vector3.Up, view));
        var obl = ForwardRenderer.ObliqueNearPlane(proj, new Vector4(nv, -Vector3.Dot(nv, pv)));
        var vp = view * obl;
        foreach (var (p, above) in new[] { (new Vector3(0, h + 0.5f, -10), true), (new Vector3(3, h + 4f, -30), true), (new Vector3(0, h - 0.5f, -10), false), (new Vector3(-2, h - 2f, -25), false) })
        {
            var c = Vector4.Transform(new Vector4(p, 1), vp);
            float z = c.Z / c.W;
            if (above) Assert.True(z >= 0 && z <= 1, $"{p} above the plane but clipped (z {z})");
            else Assert.True(z < 0, $"{p} below the plane but kept (z {z})");
        }
    }

    [Fact]
    public void StructuresStandOnTheGroundAndReachBelowIt()
    {
        // a sloping ground: 10 % grade along x
        Func<float, float, float> ground = (x, z) => 0.1f * x + 0.03f * z;
        var kit = new StructureKit(ground, 7);
        kit.Roundhouse(new Vector2(10, 5), 5f, Vector2.UnitX, Vector3.One, Vector3.One);
        // footing (rubble) reaches below the lowest ground of the footprint; the daub wall starts at or above the highest
        float lo = float.MaxValue, hi = float.MinValue;
        for (float a = 0; a < MathHelper.TwoPi; a += 0.1f) { float g = ground(10 + MathF.Cos(a) * 5.1f, 5 + MathF.Sin(a) * 5.1f); lo = MathF.Min(lo, g); hi = MathF.Max(hi, g); }
        Assert.True(kit.B.Verts[(int)StMat.Rubble].Min(v => v.Position.Y) < lo - 0.2f);
        Assert.True(kit.B.Verts[(int)StMat.Daub].Min(v => v.Position.Y) >= hi, "wall below the floor");
        // a town house: plinth below the lowest ground, walls from the floor
        var kit2 = new StructureKit(ground, 9);
        kit2.House(new Vector2(0, 0), Vector2.UnitX, new Vector2(5, 4), 2, Vector3.One, Vector3.One, 0, hip: true);
        float lo2 = ground(-5.2f, -4.2f), hi2 = ground(5.2f, 4.2f);
        Assert.True(kit2.B.Verts[(int)StMat.Ashlar].Min(v => v.Position.Y) < lo2);
        Assert.True(kit2.B.Verts[(int)StMat.Plaster].Where(v => v.Color.A > 60).Min(v => v.Position.Y) >= hi2, "plaster below the floor");
        // every palisade log is sunk into the ground where it stands
        var kit3 = new StructureKit(ground, 11);
        var ring = Enumerable.Range(0, 24).Select(i => new Vector2(MathF.Cos(i / 24f * MathHelper.TwoPi), MathF.Sin(i / 24f * MathHelper.TwoPi)) * 20f).ToList();
        kit3.Palisade(ring, 3.4f, ring[0], 3.6f);
        var logs = kit3.B.Verts[(int)StMat.Logs];
        Assert.True(logs.Count > 1000);
        foreach (var v in logs.Where(v => v.Position.Y < ground(v.Position.X, v.Position.Z) - 0.05f)) Assert.True(v.Position.Y > ground(v.Position.X, v.Position.Z) - 1.0f);
        Assert.Contains(logs, v => v.Position.Y < ground(v.Position.X, v.Position.Z) - 0.5f);
    }

    [Fact]
    public void SettlementsArePlannedDeterministicallyOnDryGround()
    {
        var (g, _, m, w) = Temperate.Value;
        var a = Settlements.IronAgeVillage(m, w, Seed(g), null);
        var b = Settlements.IronAgeVillage(m, w, Seed(g), null);
        Assert.Equal(a.Centre, b.Centre); Assert.Equal(a.Gate, b.Gate);
        foreach (var p in a.Outline) Assert.False(m.IsWet(p.X, p.Y));
        var (ga, ma, wa) = Arid.Value;
        var town = Settlements.ClassicalTown(ma, wa, Seed(ga), null);
        foreach (var p in town.Outline) { Assert.False(ma.IsWet(p.X, p.Y)); Assert.True(MathF.Abs(p.X) < ma.HalfExtentX && MathF.Abs(p.Y) < ma.HalfExtentZ); }
        Assert.True(town.WalkY > 0);
    }

    [Fact]
    public void EnvironmentSwitchesParseAndTheG1PresetTurnsThemOff()
    {
        var f = new RenderFeatures();
        Assert.True(f.EnvTrees && f.EnvGroundCover && f.EnvWater && f.EnvStructures && f.EnvRocks && f.EnvImpostors && f.EnvLodFade && f.EnvWind && f.EnvReflections && f.EnvForestFloor);
        f.SetMany("trees=0,groundcover=0,water=0,reflect=0");
        Assert.False(f.EnvTrees || f.EnvGroundCover || f.EnvWater || f.EnvReflections);
        Assert.Contains("trees=0", f.Describe());
        f.Set("env", "0");
        Assert.False(f.EnvStructures || f.EnvRocks || f.EnvImpostors || f.EnvLodFade || f.EnvWind || f.EnvForestFloor);
        f.Set("env", "1");
        Assert.True(f.EnvStructures && f.EnvTrees);
        var g1 = RenderFeatures.G1();
        Assert.False(g1.EnvTrees || g1.EnvGroundCover || g1.EnvWater || g1.EnvStructures || g1.EnvRocks || g1.EnvForestFloor);
        Assert.Throws<ArgumentException>(() => f.Set("trees", "maybe"));
    }

    [Fact]
    public void TreeModelsAreBuiltInMetresWithCanopyOcclusion()
    {
        var m = TreeModel.Build(TreeSpecies.Oak, 1);
        Assert.InRange(m.BoundsMax.Y, m.Height * 0.98f, m.Height * 1.02f);
        Assert.True(m.Lod1.Triangles < m.Lod0.Triangles * 0.75);
        // inner leaves are darker than outer ones: occlusion spans a real range
        var occ = m.Lod0.LeafVertices.Select(v => v.Wind.Z).ToArray();
        Assert.True(occ.Min() < 0.55f && occ.Max() > 0.9f, $"occlusion {occ.Min():F2}..{occ.Max():F2}");
        // the trunk base does not sway, branch tips do
        Assert.Equal(0f, m.Lod0.BarkVertices.Where(v => v.Wind.W < 0.05f).Max(v => v.Wind.X));
        Assert.True(m.Lod0.BarkVertices.Max(v => v.Wind.X) > 0.8f);
    }
}

/// <summary>Stage env (GPU, llvmpipe here): the environment reads the grid only, renders repeatably, and its switches
/// fall back to exactly the G1/render1 content.</summary>
[Collection(EnvSerialCollection.Name)]
public class EnvRenderTests
{
    static readonly EnvironmentOptions Small = new(ImpostorFrames: 4, ImpostorFrameSize: 32, MaxVariantsPerSpecies: 1);

    static Color[] Render(GraphicsDevice gd, IRenderContent content, ForwardRenderer r, CameraPose cam, float hour, RenderTarget2D rt, float t = 3f)
    {
        r.ResetAdaptation = true;
        r.Render(content, cam, SunModel.At(hour), rt, t);
        return Capture.ReadPixels(rt);
    }

    sealed class Wrap(EnvironmentScenery env) : IRenderContent, ITransparentContent, IPlanarReflectionContent
    {
        public void DrawShadowCasters(RenderContext rc) => env.DrawShadowCasters(rc);
        public void DrawOpaque(RenderContext rc) => env.DrawOpaque(rc);
        public bool NeedsSceneColor(RenderContext rc) => env.NeedsSceneColor(rc);
        public void DrawTransparent(RenderContext rc) => env.DrawTransparent(rc);
        public Vector4? ReflectionPlane(RenderContext rc) => env.ReflectionPlane(rc);
        public float ReflectionScale => 0.5f;
    }

    sealed class Plain(TerrainScenery t) : IRenderContent
    {
        public void DrawShadowCasters(RenderContext rc) => t.DrawShadowCasters(rc);
        public void DrawOpaque(RenderContext rc) => t.DrawOpaque(rc);
    }

    static CameraPose Cam(TerrainMeshData m, Vector2 at, Vector2 look, float up) =>
        new(new Vector3(at.X, m.HeightAt(at.X, at.Y) + up, at.Y), new Vector3(look.X, m.HeightAt(look.X, look.Y), look.Y), 0.85f, 0.3f, 3000f);

    [GfxFact]
    public void EnvironmentNeverWritesTheGridAndRendersRepeatably()
    {
        TestGfx.Host.Invoke(gd =>
        {
            var grid = BattleTerrainGenerator.Generate(TerrainTemperateScene.FixtureInputs, out var trace);
            string before = grid.HashHex();
            using var env = new EnvironmentScenery(gd, TestGfx.Host.Effects, grid, null, Small, null, true, trace,
                (m, w, kit) => Settlements.IronAgeVillage(m, w, 3, kit));
            var c = new Wrap(env);
            using var rt = new RenderTarget2D(gd, 320, 180, false, SurfaceFormat.Color, DepthFormat.None);
            var s = env.Settlement!;
            var cam = Cam(env.Terrain.Mesh, s.Centre + s.GateOut * 70f, s.Centre, 12f);
            using var r1 = new ForwardRenderer(gd, TestGfx.Host.Effects);
            r1.Settings.FogParams = env.Terrain.Weather.FogParams; r1.Settings.ShadowCasterBounds = env.Terrain.CasterBounds;
            var a = Render(gd, c, r1, cam, 10.5f, rt);
            var b = Render(gd, c, r1, cam, 10.5f, rt);
            Assert.Equal(0, Capture.CountChanged(a, b));
            Assert.True(Capture.DistinctColors(a) > 2000);
            Assert.True(env.Trees.LastInstances > 0 && env.Structures!.Triangles > 10000);
            // a river view with water, reflection and refraction on
            var mid = trace.RiverCells[trace.RiverCells.Length / 2];
            var rp = env.Terrain.Mesh.SamplePosition(mid % grid.Width, mid / grid.Width);
            var rc = Cam(env.Terrain.Mesh, new Vector2(rp.X + 30, rp.Z + 10), new Vector2(rp.X, rp.Z), 8f);
            var w1 = Render(gd, c, r1, rc, 10.5f, rt); var w2 = Render(gd, c, r1, rc, 10.5f, rt);
            Assert.Equal(0, Capture.CountChanged(w1, w2));
            Assert.Equal(before, grid.HashHex());
            Assert.Equal(before, BattleTerrainGenerator.Generate(TerrainTemperateScene.FixtureInputs).HashHex());
        });
    }

    /// <summary>Every environment switch off draws exactly what render1's terrain scenery draws (G1 trees, bushes and rocks,
    /// render1 water, cell-based splats, no structures), bit for bit, in the G2 frame and through the G1 preset.</summary>
    [GfxFact]
    public void EnvironmentOffDrawsExactlyTheG1Content()
    {
        TestGfx.Host.Invoke(gd =>
        {
            var grid = BattleTerrainGenerator.Generate(TerrainTemperateScene.FixtureInputs, out var trace);
            using var env = new EnvironmentScenery(gd, TestGfx.Host.Effects, grid, null, Small, null, true, trace,
                (m, w, kit) => Settlements.IronAgeVillage(m, w, 3, kit));
            using var plain = new TerrainScenery(gd, grid);
            using var rt = new RenderTarget2D(gd, 320, 180, false, SurfaceFormat.Color, DepthFormat.None);
            var mid = trace.RiverCells[trace.RiverCells.Length / 3];
            var rp = env.Terrain.Mesh.SamplePosition(mid % grid.Width, mid / grid.Width);
            var cam = Cam(env.Terrain.Mesh, new Vector2(rp.X - 60, rp.Z + 40), new Vector2(rp.X, rp.Z), 25f);
            foreach (bool g1 in new[] { false, true })
            {
                Color[] With(IRenderContent content, TerrainScenery t)
                {
                    using var r = new ForwardRenderer(gd, TestGfx.Host.Effects);
                    r.Settings.FogParams = t.Weather.FogParams; r.Settings.ShadowCasterBounds = t.CasterBounds; r.Settings.Features.Haze = t.Haze;
                    if (g1) r.Settings.Features = RenderFeatures.G1();
                    r.Settings.Features.Set("env", "0");
                    // stage render2: the screen-space AO and contact-shadow pass runs between the opaque and the
                    // transparent pass. Plain terrain scenery blends its water inside the opaque pass, the environment
                    // draws the same water in its transparent pass, so with AO on the two legitimately differ on water
                    // pixels near the banks. This test compares content, so it compares with those passes off.
                    r.Settings.Features.SetMany("ssao=0,contact=0");
                    return Render(gd, content, r, cam, 10.5f, rt);
                }
                var a = With(new Wrap(env), env.Terrain);
                var b = With(new Plain(plain), plain);
                Assert.Equal(0, Capture.CountChanged(a, b));
            }
            // and switching the environment on changes the picture substantially
            using var r2 = new ForwardRenderer(gd, TestGfx.Host.Effects);
            r2.Settings.FogParams = env.Terrain.Weather.FogParams; r2.Settings.ShadowCasterBounds = env.Terrain.CasterBounds;
            var on = Render(gd, new Wrap(env), r2, cam, 10.5f, rt);
            r2.Settings.Features.Set("env", "0");
            var off = Render(gd, new Wrap(env), r2, cam, 10.5f, rt);
            Assert.True(Capture.CountChanged(on, off, 4) > 320 * 180 / 4);
        });
    }

    [GfxFact]
    public void ImpostorAtlasHasCoverageInEveryFrame()
    {
        TestGfx.Host.Invoke(gd =>
        {
            using var tex = new EnvTextures(gd);
            var mats = new TreeMaterials(tex);
            using var v = new TreeVariantGpu(gd, EnvironmentScenery.Model(TreeSpecies.Pine, 2), 4, 32);
            v.BakeImpostors(gd, TestGfx.Host.Effects, mats);
            var data = new Color[v.ImpostorAlbedo!.Width * v.ImpostorAlbedo.Height];
            v.ImpostorAlbedo.GetData(data);
            int n = 4, f = 32, size = n * f;
            for (int j = 0; j < n; j++)
                for (int i = 0; i < n; i++)
                {
                    int covered = 0;
                    for (int y = 0; y < f; y++) for (int x = 0; x < f; x++) if (data[(j * f + y) * size + i * f + x].A > 127) covered++;
                    Assert.True(covered > f * f / 40, $"frame ({i},{j}) covered {covered}");
                }
        });
    }
}
