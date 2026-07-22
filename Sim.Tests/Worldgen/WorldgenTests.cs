using Sim.Core.Kernel;
using Sim.Core.State;
using Sim.Core.Worldgen;

namespace Sim.Tests.Worldgen;

// T1.1 acceptance: worldgen twin byte-identity, land fraction bounds across
// 10 seeds, 1024² under 5 s, field sanity, WorldHash terrain folding.
public class WorldgenTests
{
    private static WorldgenConfig Canonical()
    {
        using var stream = Sim.Data.DataFiles.OpenWorldgen();
        return WorldgenConfigLoader.Load(stream);
    }

    // D-015 dev preset: canonical tuning at 256² for fast tests.
    private static WorldgenConfig Dev() => Canonical() with { SizePx = 256 };

    [Fact]
    public void TwinGeneration_SameSeed_ByteIdentical_AllLayers()
    {
        var cfg = Dev();
        TerrainSet a = Sim.Core.Worldgen.Worldgen.Generate(cfg, seed: 42);
        TerrainSet b = Sim.Core.Worldgen.Worldgen.Generate(cfg, seed: 42);

        Assert.Equal(a.SeaLevel, b.SeaLevel);
        AssertLayerBitIdentical(a.Elevation, b.Elevation);
        AssertLayerBitIdentical(a.Water, b.Water);
        AssertLayerBitIdentical(a.Temperature, b.Temperature);
        AssertLayerBitIdentical(a.Moisture, b.Moisture);
        AssertLayerBitIdentical(a.Fertility, b.Fertility);
        AssertLayerBitIdentical(a.MovementCost, b.MovementCost);
        Assert.Equal(a.ContentHash, b.ContentHash);
    }

    private static void AssertLayerBitIdentical(ReadOnlySpan<double> a, ReadOnlySpan<double> b)
    {
        Assert.Equal(a.Length, b.Length);
        for (int i = 0; i < a.Length; i++)
        {
            if (BitConverter.DoubleToInt64Bits(a[i]) != BitConverter.DoubleToInt64Bits(b[i]))
                Assert.Fail($"layers differ at index {i}: {a[i]} vs {b[i]}");
        }
    }

    [Fact]
    public void LandFraction_WithinTuneBounds_AcrossTenSeeds()
    {
        var cfg = Dev();
        for (ulong seed = 1; seed <= 10; seed++)
        {
            TerrainSet t = Sim.Core.Worldgen.Worldgen.Generate(cfg, seed);
            long land = 0;
            ReadOnlySpan<double> water = t.Water;
            for (int i = 0; i < water.Length; i++) if (water[i] < 0.5) land++;
            double fraction = land / (double)water.Length;
            Assert.True(cfg.LandFractionMin <= fraction && fraction <= cfg.LandFractionMax,
                $"seed {seed}: land fraction {fraction} outside [{cfg.LandFractionMin}, {cfg.LandFractionMax}]");
        }
    }

    [Fact]
    public void Fields_AreSane()
    {
        TerrainSet t = Sim.Core.Worldgen.Worldgen.Generate(Dev(), seed: 7);
        var cfg = Dev();
        ReadOnlySpan<double> water = t.Water, fert = t.Fertility, move = t.MovementCost,
            moist = t.Moisture, temp = t.Temperature, elev = t.Elevation;

        for (int i = 0; i < water.Length; i++)
        {
            Assert.False(double.IsNaN(elev[i]) || double.IsNaN(temp[i]) || double.IsNaN(moist[i])
                || double.IsNaN(fert[i]) || double.IsNaN(move[i]), $"NaN at {i}");
            Assert.True(water[i] is 0.0 or 1.0, $"water not binary at {i}");
            // Water consistent with sea level; fertility zero on water.
            Assert.Equal(elev[i] < t.SeaLevel, water[i] >= 0.5);
            if (water[i] >= 0.5) Assert.Equal(0.0, fert[i]);
            else Assert.True(move[i] >= cfg.Movement.BaseCost);
            Assert.InRange(moist[i], 0.0, 1.0);
            Assert.InRange(fert[i], 0.0, 1.0);
        }

        // Water cells have full moisture (distance 0).
        for (int i = 0; i < water.Length; i++)
            if (water[i] >= 0.5) { Assert.Equal(1.0, moist[i]); break; }
    }

    [Fact]
    public void WorldHash_FoldsTerrainHash_DifferentSeedDiffers_SameSeedStable()
    {
        var cfg = Dev();
        var world = new WorldState(seed: 42);
        world.Regions.Add(new RegionRow(new RegionId(0)));

        string h0 = WorldHash.ComputeHex(world);                 // no terrain

        world.Terrain = Sim.Core.Worldgen.Worldgen.Generate(cfg, seed: 1);
        string h1 = WorldHash.ComputeHex(world);
        Assert.NotEqual(h0, h1);                                  // terrain participates

        world.Terrain = Sim.Core.Worldgen.Worldgen.Generate(cfg, seed: 2);
        string h2 = WorldHash.ComputeHex(world);
        Assert.NotEqual(h1, h2);                                  // different seed → different hash

        world.Terrain = Sim.Core.Worldgen.Worldgen.Generate(cfg, seed: 1);
        Assert.Equal(h1, WorldHash.ComputeHex(world));            // same seed → stable
    }

    [Fact]
    public void Clone_SharesTerrainByReference_AndHashesEqual()
    {
        var world = new WorldState(seed: 5)
        {
            Terrain = Sim.Core.Worldgen.Worldgen.Generate(Dev(), seed: 5),
        };
        WorldState clone = world.Clone();
        Assert.Same(world.Terrain, clone.Terrain);                // ADR-008: reference, not copy
        Assert.Equal(WorldHash.ComputeHex(world), WorldHash.ComputeHex(clone));
    }

    [Fact]
    public void Snapshot_TerrainWorld_RoundTripsWithRegeneratedTerrain_RejectsMismatch()
    {
        var cfg = Dev();
        var world = new WorldState(seed: 9)
        {
            Terrain = Sim.Core.Worldgen.Worldgen.Generate(cfg, seed: 9),
        };
        world.Regions.Add(new RegionRow(new RegionId(0)));

        using var buffer = new MemoryStream();
        Snapshot.Save(world, buffer);

        // Load with the regenerated (same seed) terrain: succeeds, hash-identical.
        buffer.Position = 0;
        WorldState loaded = Snapshot.Load(buffer, Sim.Core.Worldgen.Worldgen.Generate(cfg, seed: 9));
        Assert.Equal(WorldHash.ComputeHex(world), WorldHash.ComputeHex(loaded));

        // Missing terrain: actionable failure.
        buffer.Position = 0;
        var e1 = Assert.Throws<SnapshotFormatException>(() => Snapshot.Load(buffer));
        Assert.Contains("regenerate the TerrainSet", e1.Message);

        // Wrong-seed terrain: actionable failure.
        buffer.Position = 0;
        var e2 = Assert.Throws<SnapshotFormatException>(() =>
            Snapshot.Load(buffer, Sim.Core.Worldgen.Worldgen.Generate(cfg, seed: 10)));
        Assert.Contains("terrain mismatch", e2.Message);

        // Inverse direction: a terrain-less save refuses an offered TerrainSet.
        var plain = new WorldState(seed: 9);
        using var plainBuffer = new MemoryStream();
        Snapshot.Save(plain, plainBuffer);
        plainBuffer.Position = 0;
        var e3 = Assert.Throws<SnapshotFormatException>(() =>
            Snapshot.Load(plainBuffer, Sim.Core.Worldgen.Worldgen.Generate(cfg, seed: 9)));
        Assert.Contains("refusing to attach", e3.Message);
    }

    [Fact]
    public void CanonicalStream_TerrainPresentBranch_LengthEqualsSchemaWidthSum()
    {
        // The anti-padding guard for schema v2's terrain branch (flag + 32 hash
        // bytes): without this, a wrong width in ExpectedLength's terrain term
        // would never fail any test (the M0 length proof runs terrain-less).
        var world = new WorldState(seed: 3)
        {
            Terrain = Sim.Core.Worldgen.Worldgen.Generate(Dev(), seed: 3),
        };
        world.Regions.Add(new RegionRow(new RegionId(0)));

        using var buffer = new MemoryStream();
        using (var writer = new BinaryWriter(buffer, System.Text.Encoding.UTF8, leaveOpen: true))
        {
            CanonicalSchema.Write(world, writer);
        }
        Assert.Equal(CanonicalSchema.ExpectedLength(world), buffer.Length);
    }

    [Fact]
    public void ConfigLoader_RejectsBadConfigs_Actionably()
    {
        Assert.Contains("not valid JSON",
            Assert.Throws<WorldgenConfigException>(() => WorldgenConfigLoader.Load("{ nope")).Message);
        string valid;
        using (var s = Sim.Data.DataFiles.OpenWorldgen())
        using (var r = new StreamReader(s)) valid = r.ReadToEnd();

        Assert.Contains("sizePx",
            Assert.Throws<WorldgenConfigException>(() =>
                WorldgenConfigLoader.Load(valid.Replace("\"sizePx\": 1024", "\"sizePx\": 4"))).Message);
        Assert.Contains("land fraction bounds",
            Assert.Throws<WorldgenConfigException>(() =>
                WorldgenConfigLoader.Load(valid.Replace("\"landFractionMin\": 0.25", "\"landFractionMin\": 0.6"))).Message);
    }
}
