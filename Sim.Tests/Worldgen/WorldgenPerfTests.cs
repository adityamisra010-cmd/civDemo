using Sim.Core.Worldgen;

namespace Sim.Tests.Worldgen;

/// <summary>
/// Serial collection for wall-clock-bounded tests: xUnit runs collections in
/// parallel by default, and a timing bound measured under an N-way test storm
/// measures CONTENTION, not the code (the T1.6 one-off flake, finally named by
/// scripts/test.sh at T1.7, was exactly this test at 6.4s under load vs ~2s
/// alone). DisableParallelization gives the measurement the machine to itself;
/// the 5s acceptance bound (T1.1) stays exactly as ratified.
/// </summary>
[CollectionDefinition("perf-serial", DisableParallelization = true)]
public sealed class PerfSerialCollection;

[Collection("perf-serial")]
public class WorldgenPerfTests
{
    [Fact]
    public void FullSize1024_GeneratesUnderFiveSeconds()
    {
        using var stream = Sim.Data.DataFiles.OpenWorldgen();
        WorldgenConfig cfg = WorldgenConfigLoader.Load(stream);
        Assert.Equal(1024, cfg.SizePx); // the acceptance size, from data
        long t0 = System.Diagnostics.Stopwatch.GetTimestamp();
        TerrainSet t = Sim.Core.Worldgen.Worldgen.Generate(cfg, seed: 42);
        double seconds = (System.Diagnostics.Stopwatch.GetTimestamp() - t0)
                         / (double)System.Diagnostics.Stopwatch.Frequency;
        Assert.True(seconds < 5.0, $"1024² worldgen took {seconds:F2}s (budget 5s)");
        Assert.Equal(1024 * 1024, t.Elevation.Length);
    }

    [Fact]
    public void FullSize1024_WithRivers_StillUnderFiveSeconds()
    {
        // T1.2 acceptance bound, moved here from HydrologyTests at T1.7 for the
        // same reason as its sibling above (5.15s under parallel load, ~2s alone).
        using var stream = Sim.Data.DataFiles.OpenWorldgen();
        WorldgenConfig cfg = WorldgenConfigLoader.Load(stream);
        long t0 = System.Diagnostics.Stopwatch.GetTimestamp();
        TerrainSet t = Sim.Core.Worldgen.Worldgen.Generate(cfg, seed: 42);
        double seconds = (System.Diagnostics.Stopwatch.GetTimestamp() - t0)
                         / (double)System.Diagnostics.Stopwatch.Frequency;
        Assert.True(seconds < 5.0, $"1024² worldgen with rivers took {seconds:F2}s (budget 5s)");
        Assert.True(t.RiverPolylineCount > 0);
    }
}
