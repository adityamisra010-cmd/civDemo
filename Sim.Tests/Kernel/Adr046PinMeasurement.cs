using Sim.Core;
using Sim.Core.Kernel;
using Sim.Core.State;
using Sim.Core.Systems;
using Sim.Core.Worldgen;
using Sim.Tests.TestUtil;

namespace Sim.Tests.Kernel;

/// <summary>ADR-046 (2026-10-23) — MEASUREMENT, not a gate (skipped): the AI pins the review batch moved, on the shipped config
/// and on the single-layer twin with the cheapest-route closure off (TestConfigs.PreAiRoutes — so the remainder is the
/// Prospect bill). Written to $ADR046_OUT; the asserted values live in the tests that pin them.</summary>
public class Adr046PinMeasurement
{
    private const string Why = "measurement (ADR-046); run by hand: set ADR046_OUT and remove Skip";

    private static void Write(string line) =>
        File.AppendAllText(Environment.GetEnvironmentVariable("ADR046_OUT") ?? Path.Combine(Path.GetTempPath(), "adr046.txt"), line + "\n");

    private static string Pins(AiEmpireIntegrationTests.CanonicalAiPins p) =>
        $"targets [{string.Join(",", p.FirstTargets!)}] target1 {p.Target1} build {p.FirstBuild} advance {p.Advance}/{p.AdvancedAt} " +
        $"road {p.Road}/{p.FirstRoad} tax {p.Tax}/{p.FirstTax} a3 {p.A3At} founds [{string.Join(",", p.Founds)}] controlled {p.Controlled}";

    /// <summary>Batch 3 (ADR-046 §6–§7): the shipped config and each single-layer twin, and both off (= e91d10b).</summary>
    private static (string Name, SimConfig Cfg)[] Configs()
    {
        SimConfig full = TestConfigs.Sim();
        return
        [
            ("shipped", full),
            ("supporting-off", TestConfigs.PreAiSupporting(full)),
            ("build-first-off", TestConfigs.PreAiBuildFirst(full)),
            ("both-off", TestConfigs.PreAiSupporting(TestConfigs.PreAiBuildFirst(full))),
            ("routes-off", TestConfigs.PreAiRoutes(full)),
        ];
    }

    [Fact(Skip = Why)]
    public void CanonicalAiPins_ShippedAndTwins()
    {
        (string Name, SimConfig Cfg)[] configs = Configs();
        string[] lines = new string[configs.Length];
        System.Threading.Tasks.Parallel.For(0, configs.Length, i =>
            lines[i] = configs[i].Name + "  " + Pins(AiEmpireIntegrationTests.RunCanonicalAi(TestConfigs.Worldgen(), configs[i].Cfg)));
        foreach (string l in lines) Write(l);
    }

    [Fact(Skip = Why)]
    public void AiWorldTurn300_ShippedAndTwins()
    {
        (string Name, SimConfig Cfg)[] configs = Configs();
        string[] lines = new string[configs.Length];
        System.Threading.Tasks.Parallel.For(0, configs.Length, i =>
            lines[i] = "t300 " + configs[i].Name + "  " + WorldHash.ComputeHex(CivicsPointsAttributionTests.RunAiWorld(configs[i].Cfg)));
        foreach (string l in lines) Write(l);
    }

    [Fact(Skip = Why)]
    public void DevWorld_FirstRoadOrder_ShippedAndTwins()
    {
        (string Name, SimConfig Cfg)[] configs = Configs();
        string[] lines = new string[configs.Length];
        System.Threading.Tasks.Parallel.For(0, configs.Length, i =>
        {
            WorldgenConfig wg = TestConfigs.DevWorldgen() with { AiEmpires = 1 };
            var log = new OrderLog();
            TurnExecutor exec;
            using (var era = Sim.Data.DataFiles.OpenEraPacing())
            using (var pipe = Sim.Data.DataFiles.OpenPipeline())
                exec = new TurnExecutor(EraTableLoader.Load(era), PipelineLoader.Load(pipe, SystemCatalog.All(configs[i].Cfg, wg)), log);
            WorldState w = WorldFounding.Found(wg, configs[i].Cfg, 42);
            long road = -1;
            for (int t = 1; t <= 900 && road < 0; t++)
            {
                AiOrders.Append(log, w, configs[i].Cfg, wg);
                w = exec.Step(w);
                for (int k = 0; k < log.Count; k++) if (log[k].Kind == OrderKind.DevelopRoads) { road = log[k].Turn; break; }
            }
            lines[i] = "dev first road " + configs[i].Name + "  " + road;
        });
        foreach (string l in lines) Write(l);
    }

    [Fact(Skip = Why)]
    public void FromSaveWorld_AiOrders()
    {
        using var runs = new FromSaveRuns();
        for (int i = 0; i < runs.RunLog.Count; i++)
            if (runs.RunLog[i].ActorId == 2) Write("fromsave ai " + runs.RunLog[i]);
    }
}
