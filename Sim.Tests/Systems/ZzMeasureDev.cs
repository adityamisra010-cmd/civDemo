using System.Globalization;
using System.Text;
using Sim.Core;
using Sim.Core.Kernel;
using Sim.Core.State;
using Sim.Core.Systems;
using Sim.Core.Worldgen;
using Sim.Tests.TestUtil;

namespace Sim.Tests.Systems;

// TEMPORARY measurement harness for T4.21-4 step 2(e) + RULE B/C. Deleted before
// the packet's final commit.
public class ZzMeasureDev
{
    private static string Inv(double v) => v.ToString("G9", CultureInfo.InvariantCulture);

    [Fact]
    public void MeasureDev()
    {
        string outPath = Environment.GetEnvironmentVariable("T4214_OUT") ?? "/tmp/t4214dev.txt";
        double hazard = double.Parse(Environment.GetEnvironmentVariable("T4214_HAZARD") ?? "0.01",
            CultureInfo.InvariantCulture);
        int turns = int.Parse(Environment.GetEnvironmentVariable("T4214_TURNS") ?? "300",
            CultureInfo.InvariantCulture);
        string seedSpec = Environment.GetEnvironmentVariable("T4214_SEEDS") ?? "42,7";

        var sb = new StringBuilder();
        sb.AppendLine($"# dev preset, hazard={Inv(hazard)} turns={turns}");
        sb.AppendLine("seed,starvTotal,crashes020,peak,final,monotone,migrationGrossPerDecade," +
                      "famineSettlementTurns,severeSettlementTurns,stressSettlementTurns," +
                      "onsets,settlementYears,famineChronicleLines,density,artisanShare");
        foreach (string part in seedSpec.Split(','))
        {
            ulong seed = ulong.Parse(part);
            SimConfig baseCfg = TestConfigs.Sim();
            SimConfig cfg = baseCfg with { Disaster = baseCfg.Disaster with { HazardPerYear = hazard } };
            WorldgenConfig wg = TestConfigs.DevWorldgen();
            using var eraStream = Sim.Data.DataFiles.OpenEraPacing();
            using var pipeStream = Sim.Data.DataFiles.OpenPipeline();
            var exec = new TurnExecutor(EraTableLoader.Load(eraStream),
                PipelineLoader.Load(pipeStream, SystemCatalog.All(cfg, wg)));
            WorldState world = WorldFounding.Found(wg, cfg, seed, null);
            var col = new AutoplayCollector(seed);
            int famine = 0, severe = 0, stress = 0, onsets = 0;
            double settlementYears = 0;
            for (int t = 1; t <= turns; t++)
            {
                WorldState next = exec.Step(world);
                for (int r = 0; r < next.Disasters.Count; r++)
                {
                    DisasterRow row = next.Disasters[r];
                    if (row.Kind != 1) continue;
                    bool wasActive = false;
                    for (int q = 0; q < world.Disasters.Count; q++)
                        if (world.Disasters[q].Settlement == row.Settlement)
                        { wasActive = world.Disasters[q].RemainingYears > 0.0; break; }
                    if (!wasActive) onsets++;
                }
                for (int i = 0; i < world.Settlements.Count; i++)
                {
                    FoodStateKind k = FoodState.Of(world, world.Settlements[i].Id, cfg, out _);
                    if (k == FoodStateKind.Famine) famine++;
                    else if (k == FoodStateKind.Severe) severe++;
                    else if (k == FoodStateKind.Stress) stress++;
                }
                settlementYears += world.Settlements.Count * next.Clock.DtYears;
                world = next;
                col.Observe(world);
            }
            AutoplayMetrics m = col.Finish(world);
            long starved = 0;
            for (int i = 0; i < m.StarvationDeaths.Count; i++) starved += m.StarvationDeaths[i];
            long peak = 0;
            for (int i = 0; i < m.Population.Count; i++) peak = Math.Max(peak, m.Population[i]);
            int crashes = CalibrationAnalysis.Crashes(m, 0.20).Count;
            double gross = CalibrationAnalysis.MigrationGrossPerDecade(m);
            double density = CalibrationAnalysis.DensityPerArableKm2(m);
            sb.AppendLine(string.Join(",", seed, starved, crashes, peak, m.FinalPopulation,
                peak == m.FinalPopulation, Inv(gross), famine, severe, stress, onsets,
                Inv(settlementYears), "-", Inv(density), "-"));
            Console.WriteLine($"dev seed {seed} starved={starved} crashes={crashes} gross={Inv(gross)}");
        }
        File.WriteAllText(outPath, sb.ToString());
        Console.WriteLine(sb.ToString());
    }
}
