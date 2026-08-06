using System.Text.Json;
using Sim.Core;
using Sim.Core.Kernel;
using Sim.Core.State;
using Sim.Core.Systems;
using Sim.Core.Worldgen;
using Sim.Tests.TestUtil;

namespace Sim.Tests.Kernel;

/// <summary>
/// T3.12a — the reporter is an OBSERVER, and these tests are what makes that a
/// measured claim rather than a design intention.
///
/// The packet's fence: "It must not change determinism. Same log, same world
/// hash, with and without reporting enabled — assert that, do not assume it."
/// </summary>
public class ReplayReportTests
{
    private static (WorldState World, SimConfig Cfg) Run(int turns, Stream? report, int every = 1)
    {
        SimConfig cfg = TestConfigs.Sim();
        WorldState world = WorldFounding.Found(TestConfigs.Worldgen(), cfg, 42);
        OrderLog orders = DrivenGoldenTests.DrivingOrders(world.Settlements.Count);
        OrderValidation.ValidateAgainstWorld(orders, world);

        using var eraStream = Sim.Data.DataFiles.OpenEraPacing();
        using var pipeStream = Sim.Data.DataFiles.OpenPipeline();
        var exec = new TurnExecutor(
            EraTableLoader.Load(eraStream),
            PipelineLoader.Load(pipeStream, SystemCatalog.All(cfg)), orders);

        for (int t = 1; t <= turns; t++)
        {
            world = exec.Step(world);
            if (report is not null && t % every == 0) ReplayReport.WriteTurn(report, world, cfg);
        }
        return (world, cfg);
    }

    [Fact]
    public void Reporting_DoesNotChangeTheWorldHash()
    {
        // THE FENCE ASSERTION. A reporter that touched state — even by reading
        // through something that lazily materializes, or by advancing an RNG —
        // would show here as a divergent hash. Run the SAME driven log twice,
        // once observed and once not, and compare bit-exactly.
        (WorldState quiet, _) = Run(40, null);

        using var ms = new MemoryStream();
        (WorldState observed, _) = Run(40, ms);

        Assert.Equal(WorldHash.ComputeHex(quiet), WorldHash.ComputeHex(observed));
        Assert.True(ms.Length > 0, "no report written — the assertion would be vacuous");
    }

    [Fact]
    public void TheReportItself_IsByteIdenticalAcrossRuns()
    {
        // Determinism of the ARTIFACT, not just of the world: a diagnostic that
        // differs run to run cannot be diffed, which is the whole point of
        // capturing it. Guards culture-sensitive formatting and any accidental
        // Dictionary iteration order in the writer.
        using var a = new MemoryStream();
        using var b = new MemoryStream();
        Run(20, a);
        Run(20, b);
        Assert.Equal(a.ToArray(), b.ToArray());
    }

    [Fact]
    public void EveryReportedTurn_IsOneParseableLine_CarryingTheStateTheDirectorCannotOtherwiseSee()
    {
        using var ms = new MemoryStream();
        (WorldState world, SimConfig cfg) = Run(12, ms);

        string[] lines = System.Text.Encoding.UTF8.GetString(ms.ToArray())
            .Split('\n', StringSplitOptions.RemoveEmptyEntries);
        Assert.Equal(12, lines.Length);   // one line per turn at the default interval

        using JsonDocument last = JsonDocument.Parse(lines[^1]);
        JsonElement root = last.RootElement;
        Assert.Equal(ReplayReport.Schema, root.GetProperty("schema").GetString());
        Assert.Equal(12, root.GetProperty("turn").GetInt64());

        // The world row agrees with the world it was read from — the reporter is
        // reading state, not recomputing it.
        Assert.Equal(WorldHash.ComputeHex(world), root.GetProperty("hash").GetString());
        long pop = 0;
        for (int i = 0; i < world.Buckets.Count; i++) pop += world.Buckets[i].Count.Value;
        Assert.Equal(pop, root.GetProperty("totalPopulation").GetInt64());

        JsonElement s0 = root.GetProperty("settlements")[0];
        Assert.Equal(Cohorts.Count, s0.GetProperty("cohorts").GetArrayLength());
        Assert.Equal(cfg.Goods!.Goods.Length, s0.GetProperty("goods").GetArrayLength());
        Assert.True(s0.GetProperty("classes").GetArrayLength() > 0);

        // The four readings that were previously invisible outside the running
        // UI, spot-checked against the tables they came from.
        JsonElement cls = s0.GetProperty("classes")[0];
        Assert.True(cls.TryGetProperty("needs", out JsonElement needs));
        Assert.True(needs.TryGetProperty("Sustenance", out _), "bound needs must be reported by name");
        Assert.True(cls.TryGetProperty("grievance", out _));
        Assert.True(s0.GetProperty("sectors").TryGetProperty("farming", out _));
        Assert.True(s0.GetProperty("housing").TryGetProperty("dwellings", out _));
    }

    [Fact]
    public void ReportEvery_ThinsTheFile_WithoutChangingWhatEachLineSays()
    {
        // The volume flag. Every 4th turn of 12 = 3 lines, and the turn numbers
        // are exactly the reported ones — a thinned report must stay honest
        // about WHICH turns it holds, or a reader will interpolate over gaps.
        using var ms = new MemoryStream();
        Run(12, ms, every: 4);
        string[] lines = System.Text.Encoding.UTF8.GetString(ms.ToArray())
            .Split('\n', StringSplitOptions.RemoveEmptyEntries);
        Assert.Equal(3, lines.Length);
        long[] turns = lines.Select(l =>
        {
            using JsonDocument d = JsonDocument.Parse(l);
            return d.RootElement.GetProperty("turn").GetInt64();
        }).ToArray();
        Assert.Equal([4L, 8L, 12L], turns);
    }
}
