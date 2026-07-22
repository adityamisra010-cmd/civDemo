using Sim.Core.Worldgen;

namespace Sim.Tests.Worldgen;

// T1.2 acceptance: monotonic descent to sea/documented sink, twin byte-identity
// including river data, fertility river-correlation, counts within TUNE bounds.
public class HydrologyTests
{
    private static WorldgenConfig Canonical()
    {
        using var stream = Sim.Data.DataFiles.OpenWorldgen();
        return WorldgenConfigLoader.Load(stream);
    }

    private static WorldgenConfig Dev() => Canonical() with { SizePx = 256 };

    [Fact]
    public void Rivers_FlowMonotonicallyDownhill_ToSeaOrDocumentedSink()
    {
        var cfg = Dev();
        TerrainSet t = Sim.Core.Worldgen.Worldgen.Generate(cfg, seed: 42);

        // Recompute hydrology over the SAME inputs (deterministic — twin-proven)
        // to obtain the conditioned routing surface and flow targets.
        var elevation = t.Elevation.ToArray();
        var water = t.Water.ToArray();
        Hydrology.Result h = Hydrology.Compute(elevation, water, t.Size, cfg.Rivers);

        Assert.True(t.RiverPolylineCount > 0, "no rivers generated");
        for (int p = 0; p < t.RiverPolylineCount; p++)
        {
            ReadOnlySpan<int> line = t.RiverPolyline(p);
            Assert.True(line.Length >= 1);

            // Head → mouth: conditioned elevation strictly decreases at every step.
            for (int i = 1; i < line.Length; i++)
            {
                Assert.True(h.FilledElevation[line[i]] < h.FilledElevation[line[i - 1]],
                    $"polyline {p} not strictly descending at step {i}");
                // And each step follows the D8 flow target (a real flow path).
                Assert.Equal(line[i], h.FlowTarget[line[i - 1]]);
            }

            // The mouth terminates in water or the documented border sink.
            int mouth = line[^1];
            int target = h.FlowTarget[mouth];
            Assert.True(target == -1 || water[target] >= 0.5,
                $"polyline {p} mouth {mouth} does not terminate at sea or border sink");
        }
    }

    [Fact]
    public void TwinGeneration_ByteIdentical_IncludingRiverData()
    {
        var cfg = Dev();
        TerrainSet a = Sim.Core.Worldgen.Worldgen.Generate(cfg, seed: 42);
        TerrainSet b = Sim.Core.Worldgen.Worldgen.Generate(cfg, seed: 42);

        Assert.Equal(a.ContentHash, b.ContentHash); // hash now covers rivers

        Assert.Equal(a.RiverPolylineCount, b.RiverPolylineCount);
        for (int p = 0; p < a.RiverPolylineCount; p++)
            Assert.True(a.RiverPolyline(p).SequenceEqual(b.RiverPolyline(p)),
                $"polyline {p} differs between twins");

        ReadOnlySpan<double> ra = a.Rivers, rb = b.Rivers;
        for (int i = 0; i < ra.Length; i++)
            Assert.Equal(BitConverter.DoubleToInt64Bits(ra[i]), BitConverter.DoubleToInt64Bits(rb[i]));
    }

    [Fact]
    public void RiverCounts_WithinTuneBounds_AcrossTenSeeds()
    {
        var cfg = Dev();
        for (ulong seed = 1; seed <= 10; seed++)
        {
            TerrainSet t = Sim.Core.Worldgen.Worldgen.Generate(cfg, seed);
            Assert.InRange(t.RiverPolylineCount, 1, cfg.Rivers.Count);

            long riverCells = 0, landCells = 0;
            ReadOnlySpan<double> rivers = t.Rivers, water = t.Water;
            for (int i = 0; i < rivers.Length; i++)
            {
                if (water[i] < 0.5) landCells++;
                if (rivers[i] >= 0.5) riverCells++;
            }
            double fraction = riverCells / (double)landCells;
            Assert.True(cfg.Rivers.CellFractionMin <= fraction && fraction <= cfg.Rivers.CellFractionMax,
                $"seed {seed}: river cell fraction {fraction} outside " +
                $"[{cfg.Rivers.CellFractionMin}, {cfg.Rivers.CellFractionMax}]");
        }
    }

    [Fact]
    public void Fertility_IsRiverCorrelated()
    {
        var cfg = Dev();
        TerrainSet t = Sim.Core.Worldgen.Worldgen.Generate(cfg, seed: 42);
        double r = WorldgenStats.FertilityRiverCorrelation(t, cfg.Rivers.AdjacencyRadiusPx);
        // The boost multiplies river-adjacent fertility by 1.6 (TUNE); the
        // point-biserial correlation must be clearly positive. 0.05 is a loose
        // floor — the observed value is reported by the acceptance run.
        Assert.True(r > 0.05, $"fertility/river correlation {r} not clearly positive");
    }

    [Fact]
    public void RiverCells_AreOnLand()
    {
        TerrainSet t = Sim.Core.Worldgen.Worldgen.Generate(Dev(), seed: 7);
        ReadOnlySpan<double> rivers = t.Rivers, water = t.Water;
        for (int i = 0; i < rivers.Length; i++)
            if (rivers[i] >= 0.5)
                Assert.True(water[i] < 0.5, $"river cell {i} is on water");
    }

}
