using Sim.Core;
using Sim.Core.Kernel;
using Sim.Core.State;
using Sim.Core.Systems;
using Sim.Core.Worldgen;
using Sim.Tests.TestUtil;

namespace Sim.Tests.Kernel;

// TEMPORARY golden probe for T4.21-4 step 5. Deleted before the final commit.
public class ZzGoldenProbe
{
    [Fact]
    public void Probe()
    {
        SimConfig cfg = TestConfigs.Sim();
        using var eraStream = Sim.Data.DataFiles.OpenEraPacing();
        using var pipeStream = Sim.Data.DataFiles.OpenPipeline();
        var exec = new TurnExecutor(EraTableLoader.Load(eraStream),
            PipelineLoader.Load(pipeStream, SystemCatalog.All(cfg, TestConfigs.Worldgen())));
        WorldState founded = exec.Run(WorldFounding.Found(TestConfigs.Worldgen(), cfg, 42), 300);
        string f = WorldHash.ComputeHex(founded);
        (WorldState driven, _) = DrivenGoldenTests.RunDriven(300);
        string d = WorldHash.ComputeHex(driven);
        File.WriteAllText(Environment.GetEnvironmentVariable("T4214_OUT") ?? "/tmp/goldens.txt",
            $"FOUNDED {f}\nDRIVEN {d}\n");
        Console.WriteLine($"FOUNDED {f}");
        Console.WriteLine($"DRIVEN {d}");
    }
}
