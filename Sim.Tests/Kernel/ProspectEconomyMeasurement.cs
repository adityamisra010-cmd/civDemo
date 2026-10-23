using Sim.Core;
using Sim.Core.Kernel;
using Sim.Core.State;
using Sim.Core.Systems;
using Sim.Core.Worldgen;
using Sim.Tests.TestUtil;
using Xunit;
using Xunit.Abstractions;

namespace Sim.Tests.Kernel;

/// <summary>
/// MEASUREMENT (2026-10-23, M6 review item 2 — the Prospect cost): what an Empire's purse holds of each good the Prospect
/// cost could name (grain, timber, stone, bronze, tools), summed over the settlements it controls, at turns 1, 5, 20, 50,
/// 100, 200 and 400 of the canonical AI world (seeds 1, 2, 42, 7), for the idle player Empire (polity 1) and the AI Empire
/// (polity 2), plus how many founded settlements hold a timber and a stone deposit. Not a pin; the numbers choose the
/// quantities in sim.json discovery and are recorded in docs/adr/adr-046-m6-review-fixes.md. Skipped by default.
/// </summary>
public class ProspectEconomyMeasurement(ITestOutputHelper output)
{
    [Fact(Skip = "measurement — run explicitly; results in docs/adr/adr-046-m6-review-fixes.md")]
    public void PurseByTurn_FourSeeds()
    {
        SimConfig cfg = TestConfigs.Sim();
        string[] names = ["grain", "timber", "stone", "bronze", "tools"];
        output.WriteLine("seed | polity | turn | " + string.Join(" | ", names));
        foreach (ulong seed in new ulong[] { 1, 2, 42, 7 })
        {
            WorldgenConfig worldgen = TestConfigs.Worldgen();
            WorldgenConfig wg = worldgen with { AiEmpires = 1 };
            var log = new OrderLog();
            using var eraStream = Sim.Data.DataFiles.OpenEraPacing();
            using var pipeStream = Sim.Data.DataFiles.OpenPipeline();
            var executor = new TurnExecutor(EraTableLoader.Load(eraStream),
                PipelineLoader.Load(pipeStream, SystemCatalog.All(cfg, worldgen)), log);
            WorldState w = WorldFounding.Found(wg, cfg, seed);
            int timberDeposits = 0, stoneDeposits = 0;
            for (int i = 0; i < w.Deposits.Count; i++)
            {
                if (w.Deposits[i].Abundance <= 0.0) continue;
                string g = cfg.Goods!.ById(w.Deposits[i].Good.Value).Name;
                if (g == "timber") timberDeposits++;
                if (g == "stone") stoneDeposits++;
            }
            output.WriteLine($"seed {seed}: {w.Settlements.Count} settlements; timber deposits {timberDeposits}, stone deposits {stoneDeposits}");
            for (int t = 1; t <= 400; t++)
            {
                AiOrders.Append(log, w, cfg, worldgen);
                w = executor.Step(w);
                if (t is not (1 or 5 or 20 or 50 or 100 or 200 or 400)) continue;
                foreach (int p in new[] { 1, 2 })
                {
                    var sums = new long[names.Length];
                    for (int r = 0; r < w.GoodStocks.Count; r++)
                    {
                        GoodStockRow row = w.GoodStocks[r];
                        if (!EmpireQuery.ControlsSettlement(w, new PolityId(p), row.Settlement)) continue;
                        int k = Array.IndexOf(names, cfg.Goods!.ById(row.Good.Value).Name);
                        if (k >= 0) sums[k] += row.Amount.Value;
                    }
                    output.WriteLine($"{seed} | {p} | {t} | " + string.Join(" | ", sums));
                }
            }
        }
    }
}
