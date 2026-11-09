using System.Diagnostics;
using Gfx.Engine.Diagnostics;
using Gfx.Engine.Rendering;
using Gfx.Engine.Scene;
using Gfx.Proc.Authoritative;
using Gfx.Proc.Presentation;
using Gfx.Proc.Scenes;
using Microsoft.Xna.Framework.Graphics;

namespace Gfx.Tests;

/// <summary>Lane T: authoritative battle-terrain grid (integer, hashed) and its presentation (mesh, splat, render).</summary>
public class TerrainTests
{
    static readonly BattleTerrainInputs Temperate = TerrainTemperateScene.FixtureInputs;
    static readonly BattleTerrainInputs Arid = TerrainAridScene.FixtureInputs;

    // Pinned SHA-256 of the canonical bytes. A change here is a change to the AUTHORITATIVE generator: re-pin only with an
    // attributed reason (the grid is gameplay-relevant state).
    // Re-pinned 2026-11-04 (verification fix round): river surface drop capped per step + talus slope limit after carving
    // (findings 1 and 2). Previous pins: temperate 19da38cd…f6ea6, arid 42216ba2…e4f4.
    const string TemperateHash = "be39da870fc8f3f52b42385c9cd506ac70d16f01178104d654a4739827a6fe89";
    const string AridHash = "88b1094a17418b16157f97dec3cdc863945e49bc60f5300fa3c7f35b746c9156";

    [Fact]
    public void GridHashesArePinned()
    {
        Assert.Equal(TemperateHash, BattleTerrainGenerator.Generate(Temperate).HashHex());
        Assert.Equal(AridHash, BattleTerrainGenerator.Generate(Arid).HashHex());
    }

    [Fact]
    public void RepeatedGenerationIsByteIdentical()
    {
        var a = BattleTerrainGenerator.Generate(Temperate).ToCanonicalBytes();
        var b = BattleTerrainGenerator.Generate(Temperate).ToCanonicalBytes();
        Assert.Equal(a, b);
    }

    [Fact]
    public void DifferentSeedOrCellGivesDifferentTerrain()
    {
        var a = BattleTerrainGenerator.Generate(Temperate);
        var bySeed = BattleTerrainGenerator.Generate(Temperate with { WorldSeed = 43 });
        var byCell = BattleTerrainGenerator.Generate(Temperate with { CampaignCell = 1235 });
        foreach (var other in new[] { bySeed, byCell })
        {
            Assert.NotEqual(a.HashHex(), other.HashHex());
            int diff = 0;
            for (int i = 0; i < a.HeightMm.Length; i++) if (a.HeightMm[i] != other.HeightMm[i]) diff++;
            Assert.True(diff > a.HeightMm.Length / 2, $"only {diff} heights differ");
        }
    }

    [Fact]
    public void CanonicalBytesRoundTripPopulatedGrid()
    {
        var g = BattleTerrainGenerator.Generate(Arid);
        Assert.True(g.WaterDepthMm.Any(d => d > 0) && g.HeightMm.Any(h => h != 0));
        var bytes = g.ToCanonicalBytes();
        Assert.Equal(BattleTerrainGrid.HeaderBytes + 256 * 256 * 9, bytes.Length);
        Assert.Equal(g.ExpectedLength, bytes.Length);
        var back = BattleTerrainGrid.FromCanonicalBytes(bytes);
        Assert.Equal(bytes, back.ToCanonicalBytes());
        Assert.Equal(g.HashHex(), back.HashHex());
        Assert.Equal((g.Width, g.Height, g.CellMm, g.DatumMm, g.WaterLevelMm, g.Biome, g.Seed),
            (back.Width, back.Height, back.CellMm, back.DatumMm, back.WaterLevelMm, back.Biome, back.Seed));
        back.HeightMm[12345]++;
        Assert.NotEqual(g.HashHex(), back.HashHex());
    }

    [Fact]
    public void IntegerOnlyScanPassesOnAuthoritativeFolderAndCatchesFloat()
    {
        string root = AppContext.BaseDirectory;
        while (!File.Exists(Path.Combine(root, "scripts", "check-gfx-determinism.sh"))) root = Path.GetDirectoryName(root) ?? throw new FileNotFoundException("scan script");
        string script = Path.Combine(root, "scripts", "check-gfx-determinism.sh");
        Assert.True(Directory.Exists(Path.Combine(root, "Gfx.Proc", "Authoritative")));
        var (code, output) = Run(script);
        Assert.True(code == 0, output);
        // planted files live inside the prototype folder (not bin/obj, which the scan skips), never the system temp dir (finding 6)
        string tmp = Path.Combine(root, ".gfx-scan-" + Environment.ProcessId);
        Directory.CreateDirectory(tmp);
        try
        {
            // finding 4: exponent, leading-dot and trailing-dot literals and Math.FusedMultiplyAdd must be caught too
            string[] planted = { "(int)(x * 0.5)", "(int)(x * 1e3)", "(int)(x * .5)", "(int)(x / 2.)", "(int)System.Math.FusedMultiplyAdd(x, x, x)", "(int)MathF.Abs(x)" };
            foreach (string expr in planted)
            {
                File.WriteAllText(Path.Combine(tmp, "Bad.cs"), "class Bad { int F(int x) => " + expr + "; }\n");
                var (bad, badOut) = Run(script, tmp);
                Assert.True(bad == 1, expr + ": " + badOut);
            }
            // integer look-alikes must stay clean: ranges, member access on literals, hex with E digits, indexers
            File.WriteAllText(Path.Combine(tmp, "Bad.cs"), "class Ok { int F(int[] a) => a[0..2].Length + a[1].GetType().Name.Length + 0x1E5 + 2.CompareTo(1) + a[^1]; }\n");
            var (ok, okOut) = Run(script, tmp);
            Assert.True(ok == 0, okOut);
        }
        finally { Directory.Delete(tmp, true); }
    }

    static (int code, string output) Run(string script, params string[] args)
    {
        var psi = new ProcessStartInfo("bash") { RedirectStandardOutput = true, RedirectStandardError = true };
        psi.ArgumentList.Add(script); foreach (var a in args) psi.ArgumentList.Add(a);
        using var p = Process.Start(psi)!;
        string o = p.StandardOutput.ReadToEnd() + p.StandardError.ReadToEnd();
        p.WaitForExit();
        return (p.ExitCode, o);
    }

    [Fact]
    public void ThermalErosionConservesMassExactlyAndReducesSteepness()
    {
        int W = 64, H = 64; var h = new int[W * H];
        for (int i = 0; i < h.Length; i++) h[i] = (int)(IntNoise.Hash(7, i, 0) % 100_000);
        long before = h.Sum(x => (long)x);
        int MaxStep(int[] a) { int m = 0; for (int y = 0; y < H; y++) for (int x = 0; x + 1 < W; x++) m = Math.Max(m, Math.Abs(a[y * W + x] - a[y * W + x + 1])); return m; }
        int steep = MaxStep(h);
        BattleTerrainGenerator.ThermalErode(h, W, H, 2400, 20);
        Assert.Equal(before, h.Sum(x => (long)x));
        Assert.True(MaxStep(h) < steep / 2, $"{MaxStep(h)} vs {steep}");
    }

    [Fact]
    public void RiverRunsDownhillToItsOutletAndIsWater()
    {
        foreach (var inp in new[] { Temperate, Arid })
        {
            var g = BattleTerrainGenerator.Generate(inp, out var tr);
            Assert.True(tr.RiverCells.Length > 100, $"{inp.Biome}: river {tr.RiverCells.Length} cells");
            for (int i = 1; i < tr.RiverSurfaceMm.Length; i++)
                Assert.InRange(tr.RiverSurfaceMm[i - 1] - tr.RiverSurfaceMm[i], 0, BattleTerrainGenerator.RiverMaxDropMm);
            foreach (int c in tr.RiverCells) Assert.Equal(TerrainClass.Water, g.Class[c]);
            int last = tr.RiverCells[^1], lx = last % g.Width, ly = last / g.Width;
            bool atEdge = ly == g.Height - 1 || (inp.CoastEdge >= 0 && g.HeightMm[last] < g.WaterLevelMm);
            Assert.True(atEdge, $"{inp.Biome}: river ends at ({lx},{ly})");
        }
    }

    /// <summary>Verification finding 1: the river carve must not leave one-cell cliffs; every 4-neighbour step is held to the talus.</summary>
    [Fact]
    public void NoAdjacentCellStepExceedsTheTalus()
    {
        foreach (var inp in new[] { Temperate, Arid })
        {
            var g = BattleTerrainGenerator.Generate(inp);
            int talus = BattleTerrainGenerator.TalusMm(g.CellMm), worst = 0, wx = 0, wy = 0;
            for (int y = 0; y < g.Height; y++)
                for (int x = 0; x < g.Width; x++)
                {
                    int h = g.HeightMm[g.Index(x, y)];
                    foreach (var (nx, ny) in new[] { (x + 1, y), (x, y + 1) })
                    {
                        if (nx >= g.Width || ny >= g.Height) continue;
                        int d = Math.Abs(h - g.HeightMm[g.Index(nx, ny)]);
                        if (d > worst) { worst = d; wx = x; wy = y; }
                    }
                }
            Assert.True(worst <= talus, $"{inp.Biome}: step {worst} mm at ({wx},{wy}) > talus {talus} mm");
        }
    }

    /// <summary>Verification finding 2: water depth is authoritative, so no dry cell may sit below the surface of an adjacent (8-neighbour) wet cell.</summary>
    [Fact]
    public void NoDryCellLiesBelowAnAdjacentWaterSurface()
    {
        foreach (var inp in new[] { Temperate, Arid })
        {
            var g = BattleTerrainGenerator.Generate(inp);
            int bad = 0;
            for (int y = 0; y < g.Height; y++)
                for (int x = 0; x < g.Width; x++)
                {
                    int i = g.Index(x, y);
                    if (g.WaterDepthMm[i] != 0) continue;
                    for (int dy = -1; dy <= 1; dy++)
                        for (int dx = -1; dx <= 1; dx++)
                        {
                            int nx = x + dx, ny = y + dy;
                            if ((dx | dy) == 0 || nx < 0 || ny < 0 || nx >= g.Width || ny >= g.Height) continue;
                            int n = g.Index(nx, ny);
                            if (g.WaterDepthMm[n] > 0 && g.HeightMm[i] < g.HeightMm[n] + g.WaterDepthMm[n]) { bad++; dx = dy = 2; }
                        }
                }
            Assert.True(bad == 0, $"{inp.Biome}: {bad} dry cells below an adjacent water surface");
        }
    }

    /// <summary>
    /// Verification finding 3: the road must not run into the sea. Synthetic south-coast grid: land north of the road
    /// line is flat at +3 m; on and south of it the land falls 50 mm per column eastward into the sea. Without the
    /// near-sea penalty the least-climb walk follows the falling ground and the road is cut off by water.
    /// </summary>
    [Fact]
    public void RoadStepsAwayFromTheSeaAndStaysContinuous()
    {
        var inp = Arid;
        var g = new BattleTerrainGrid(256, 256, 4000, 0, 0, Biome.Arid, 1);
        int c = g.Height / 3;
        for (int y = 0; y < g.Height; y++)
            for (int x = 0; x < g.Width; x++)
            {
                int i = g.Index(x, y);
                g.HeightMm[i] = y < c ? 3000 : 3000 - 50 * x;
                g.WaterDepthMm[i] = Math.Max(0, g.WaterLevelMm - g.HeightMm[i]);
            }
        var road = new bool[g.HeightMm.Length];
        BattleTerrainGenerator.LayRoad(g, inp, g.Seed, coastal: true, road);
        for (int x = 0; x < g.Width; x++)
        {
            bool any = false;
            for (int y = 0; y < g.Height; y++) any |= road[g.Index(x, y)];
            Assert.True(any, $"road broken at column {x}");
        }
        for (int i = 0; i < road.Length; i++) if (road[i]) Assert.Equal(0, g.WaterDepthMm[i]);
    }

    [Fact]
    public void BiomesProduceTheirClasses()
    {
        var t = BattleTerrainGenerator.Generate(Temperate).ClassHistogram();
        foreach (var c in new[] { TerrainClass.Open, TerrainClass.Forest, TerrainClass.Hills, TerrainClass.Water, TerrainClass.Road })
            Assert.True(t[(int)c] > 0, $"temperate lacks {c}");
        Assert.Equal(0, t[(int)TerrainClass.Jungle]); Assert.Equal(0, t[(int)TerrainClass.Desert]);
        var a = BattleTerrainGenerator.Generate(Arid).ClassHistogram();
        foreach (var c in new[] { TerrainClass.Desert, TerrainClass.Beach, TerrainClass.Water, TerrainClass.Road })
            Assert.True(a[(int)c] > 0, $"arid lacks {c}");
        Assert.Equal(0, a[(int)TerrainClass.Forest]);
        var trop = Temperate with { TemperatureDeciC = 260, MoisturePermille = 800 };
        Assert.Equal(Biome.Tropical, trop.Biome);
        Assert.True(BattleTerrainGenerator.Generate(trop).ClassHistogram()[(int)TerrainClass.Jungle] > 0);
        Assert.Equal(Biome.Cold, (Temperate with { TemperatureDeciC = 0 }).Biome);
        Assert.Equal(0, t[(int)TerrainClass.Wall]);
    }

    [Fact]
    public void SplatWeightsAreNonNegativeAndSumToOne()
    {
        foreach (var inp in new[] { Temperate, Arid })
        {
            var m = TerrainMeshData.Build(BattleTerrainGenerator.Generate(inp));
            var w = m.SampleSplats; int n = SplatModel.Count;
            for (int i = 0; i < w.Length; i += n)
            {
                float s = 0; for (int k = 0; k < n; k++) { Assert.True(w[i + k] >= 0); s += w[i + k]; }
                Assert.True(MathF.Abs(s - 1) < 1e-5f, $"sample {i / n}: sum {s}");
            }
        }
        // all biomes, random inputs (including all-zero class fractions → fallback to grass)
        Span<float> o = stackalloc float[SplatModel.Count];
        for (uint i = 0; i < 4000; i++)
        {
            var f = new float[16]; int cls = (int)(PNoise.Hash(i) % 11);
            if (cls < 10) f[cls] = PNoise.U01(PNoise.Hash(i + 1));
            SplatModel.Weights(new SplatInputs { ClassFraction = f, Slope = PNoise.U01(PNoise.Hash(i + 2)), HeightRel = PNoise.U01(PNoise.Hash(i + 3)), Noise = PNoise.U01(PNoise.Hash(i + 4)), Biome = (Biome)(i % 4) }, o);
            float s = 0; foreach (var x in o) { Assert.True(x >= 0); s += x; }
            Assert.True(MathF.Abs(s - 1) < 1e-5f);
        }
    }

    [Fact]
    public void ChunkLodIndicesAreValid()
    {
        var m = TerrainMeshData.Build(BattleTerrainGenerator.Generate(Temperate));
        Assert.Equal(16, m.Chunks.Length);
        int N = TerrainMeshData.ChunkCells + 1;
        foreach (var c in m.Chunks)
        {
            Assert.Equal(N * N + 4 * N, c.Vertices.Length);
            for (int l = 0; l < TerrainMeshData.LodSteps.Length; l++)
            {
                int step = TerrainMeshData.LodSteps[l], q = TerrainMeshData.ChunkCells / step;
                var ix = c.Indices[l];
                Assert.Equal(0, ix.Length % 3);
                Assert.Equal(q * q * 2 + 4 * q * 2, ix.Length / 3);
                var used = new bool[c.Vertices.Length];
                for (int t = 0; t < ix.Length; t += 3)
                {
                    int a = ix[t], b = ix[t + 1], d = ix[t + 2];
                    Assert.True(a >= 0 && b >= 0 && d >= 0 && a < c.Vertices.Length && b < c.Vertices.Length && d < c.Vertices.Length);
                    Assert.True(a != b && b != d && a != d, "degenerate triangle");
                    used[a] = used[b] = used[d] = true;
                }
                // grid vertices used are exactly those on the LOD lattice
                for (int y = 0; y < N; y++) for (int x = 0; x < N; x++)
                    Assert.Equal(x % step == 0 && y % step == 0, used[y * N + x]);
            }
            foreach (var v in c.Vertices) Assert.True(c.Bounds.Contains(v.Position) != Microsoft.Xna.Framework.ContainmentType.Disjoint);
        }
        var water = m.Water;
        Assert.True(water.Indices.Length > 0 && water.Indices.All(i => i >= 0 && i < water.Vertices.Length));
    }

    /// <summary>Renders every terrain shot (shadows on) and proves the renderer never wrote the authoritative grid.</summary>
    [GfxFact]
    public void TerrainScenesRenderAndNeverWriteTheGrid()
    {
        TestGfx.Host.Invoke(gd =>
        {
            foreach (var scene in new TerrainSceneBase[] { new TerrainTemperateScene(), new TerrainAridScene() })
            {
                using (scene)
                {
                    scene.Load(gd, TestGfx.Host.Effects);
                    string fresh = BattleTerrainGenerator.Generate(scene.Inputs).HashHex();
                    string before = scene.Grid.HashHex();
                    Assert.Equal(fresh, before);
                    using var r = new ForwardRenderer(gd, TestGfx.Host.Effects);
                    scene.Configure(r.Settings);
                    using var rt = new RenderTarget2D(gd, 320, 180, false, SurfaceFormat.Color, DepthFormat.None);
                    foreach (var shot in scene.Shots)
                    {
                        scene.Update(shot.SceneTime);
                        r.Render(scene, shot.Camera, SunModel.At(shot.HourOfDay), rt, shot.SceneTime);
                        var px = Capture.ReadPixels(rt);
                        Assert.True(Capture.DistinctColors(px) > 300, $"{shot.Name}: flat image");
                        int ground = Capture.CountDifferentFrom(px.AsSpan(px.Length / 2), px[0], 12);
                        Assert.True(ground > px.Length / 4, $"{shot.Name}: ground px {ground}");
                    }
                    Assert.True(scene.Scenery.LastVisibleChunks > 0 && scene.Scenery.TotalPropInstances > 1000);
                    Assert.Equal(before, scene.Grid.HashHex());
                }
            }
        });
    }

    [GfxFact]
    public void TerrainEffectsAreRegistered()
    {
        foreach (var n in new[] { TerrainEffectNames.TerrainSplat, TerrainEffectNames.TerrainDepth, TerrainEffectNames.Water })
            Assert.Contains(n, TestGfx.Host.Effects.Names);
    }
}
