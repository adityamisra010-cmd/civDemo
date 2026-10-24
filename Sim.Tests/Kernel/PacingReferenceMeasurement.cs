using Sim.Core;
using Sim.Core.Kernel;
using Sim.Core.State;
using Sim.Core.Systems;
using Sim.Core.Worldgen;
using Sim.Tests.TestUtil;
using Xunit.Abstractions;

namespace Sim.Tests.Kernel;

/// <summary>
/// D-063 R6 (tranche 2, 2026-10-24) — MEASUREMENT, not a gate (skipped): the reference empire trajectory used to
/// calibrate per-(tree, Age) research costs. The canonical world (1,024 px) with one AI Empire on seeds 1, 2, 42 and 7,
/// 1,630 turns. Every 10 turns it prints: turn, world year, dtYears, the AI's population, RP per turn, CP per turn
/// (CivicsPointsPerYear × dtYears), its Age, and completed technology and civics counts.
/// Results: docs/m6-plan/knowledge-redesign/tranche2-balance-report.md.
/// </summary>
public class PacingReferenceMeasurement(ITestOutputHelper output)
{
    private static readonly PolityId Ai = new(2);

    [Fact(Skip = "measurement — run explicitly; results in docs/m6-plan/knowledge-redesign/tranche2-balance-report.md")]
    public void ReferenceTrajectory_FourSeeds()
    {
        ulong[] seeds = [1, 2, 42, 7];
        string[] lines = new string[seeds.Length];
        System.Threading.Tasks.Parallel.For(0, seeds.Length, i => lines[i] = Run(seeds[i]));
        foreach (string l in lines) output.WriteLine(l);
    }

    private static string Run(ulong seed)
    {
        SimConfig cfg = TestConfigs.Sim();
        WorldgenConfig shipped = TestConfigs.Worldgen();
        var log = new OrderLog();
        TurnExecutor exec;
        using (var era = Sim.Data.DataFiles.OpenEraPacing())
        using (var pipe = Sim.Data.DataFiles.OpenPipeline())
            exec = new TurnExecutor(EraTableLoader.Load(era), PipelineLoader.Load(pipe, SystemCatalog.All(cfg, shipped)), log);
        WorldState w = WorldFounding.Found(shipped with { AiEmpires = 1 }, cfg, seed);
        var research = cfg.Research!;
        var sb = new System.Text.StringBuilder();
        sb.AppendLine($"seed {seed}: turn | year | dt | population | RP/turn | CP/turn | Age | tech done | civics done");
        for (int t = 0; t < 1630; t++)
        {
            AiOrders.Append(log, w, cfg, shipped);
            w = exec.Step(w);
            if (w.Clock.Turn % 10 != 0) continue;
            double pop = ResearchQuery.Population(w, Ai);
            double rp = ResearchQuery.ResearchPointPool(w, research, Ai);
            double cp = ResearchQuery.CivicsPointsPerYear(research.Tuning, pop) * w.Clock.DtYears;
            int tech = ResearchQuery.CompletedNodes(w, research, Ai, Sim.Core.Systems.Research.ResearchTree.Technology).Length;
            int civ = ResearchQuery.CompletedNodes(w, research, Ai, Sim.Core.Systems.Research.ResearchTree.Civics).Length;
            sb.AppendLine(string.Create(System.Globalization.CultureInfo.InvariantCulture,
                $"{w.Clock.Turn} | {w.Clock.WorldDateYears:0} | {w.Clock.DtYears:0.##} | {pop:0} | {rp:0.0} | {cp:0.00} | {AgeQuery.CurrentAge(w, cfg.Ages!, Ai)} | {tech} | {civ}"));
        }
        return sb.ToString();
    }
}
