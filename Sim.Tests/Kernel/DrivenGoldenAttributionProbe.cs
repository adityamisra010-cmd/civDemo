using System.Globalization;
using System.Text;
using Sim.Core.Kernel;
using Sim.Core.State;
using Sim.Core.Systems;

namespace Sim.Tests.Kernel;

/// <summary>
/// ATTRIBUTION PROBE for the DrivenGolden hash movement.
///
/// NOT a golden and NOT an assertion about intended behaviour. It runs the SAME
/// driven world the golden runs and records, per turn, the world hash and the
/// per-settlement grain state, so the first divergent turn can be named and the
/// settlement responsible identified. Run once with the capacity fix in place
/// and once without; diff the two reports.
///
/// The capacity figure is inferred from the demand the consumption system
/// publishes (`LastConsumptionDemandUnits`, post-substitution) as
/// `GranaryYearsOfDemand × demand / dt` — the same expression on the same
/// inputs, exact to the sub-unit remainder bank. `BoundStore` remains the sole
/// owner of the formula.
/// </summary>
public sealed class DrivenGoldenAttributionProbe
{
    [Fact]
    public void Probe_DrivenWorld_PerTurnGrainAndCapacity()
    {
        // Rebuilt here rather than calling RunDriven(300), so the world can be
        // stepped one turn at a time. Same config, same seed, same order log,
        // same era table and pipeline — DrivenGoldenTests.RunDriven's own recipe.
        SimConfig cfg = Sim.Tests.TestUtil.TestConfigs.Sim();
        WorldState w = Sim.Core.Worldgen.WorldFounding.Found(
            Sim.Tests.TestUtil.TestConfigs.Worldgen(), cfg, 42);
        OrderLog orders = DrivenGoldenTests.DrivingOrders(w.Settlements.Count);
        OrderValidation.ValidateAgainstWorld(orders, w);
        using var eraStream = Sim.Data.DataFiles.OpenEraPacing();
        using var pipeStream = Sim.Data.DataFiles.OpenPipeline();
        var exec = new TurnExecutor(
            EraTableLoader.Load(eraStream),
            PipelineLoader.Load(pipeStream, Sim.Core.SystemCatalog.All(cfg)), orders);

        int grain = cfg.Goods!.GrainId;
        double years = cfg.Consumption.GranaryYearsOfDemand;

        var report = new StringBuilder();
        report.AppendLine("turn|hash|totalGrain|minCapacity|settlementsAtCapZero|settlementsAtCapZeroWithStock|minPop");

        for (int t = 0; t < 300; t++)
        {
            double dt = w.Clock.DtYears;
            w = exec.Step(w);

            long total = 0, minCap = long.MaxValue;
            int capZero = 0, capZeroWithStock = 0;
            for (int i = 0; i < w.GoodStocks.Count; i++)
            {
                GoodStockRow row = w.GoodStocks[i];
                if (row.Good.Value != grain) continue;
                total += row.Amount.Value;
                long demand = row.LastConsumptionDemandUnits;
                if (demand <= 0) continue;
                long cap = (long)Math.Floor(years * demand / dt);
                if (cap < minCap) minCap = cap;
                if (cap == 0) { capZero++; if (row.Amount.Value > 0) capZeroWithStock++; }
            }

            long minPop = long.MaxValue;
            for (int s = 0; s < w.Settlements.Count; s++)
            {
                long pop = 0;
                SettlementId id = w.Settlements[s].Id;
                for (int i = 0; i < w.Buckets.Count; i++)
                    if (w.Buckets[i].Settlement == id) pop += w.Buckets[i].Count.Value;
                if (pop < minPop) minPop = pop;
            }

            report.AppendLine(string.Create(CultureInfo.InvariantCulture,
                $"{w.Clock.Turn}|{WorldHash.ComputeHex(w)[..12]}|{total}|{(minCap == long.MaxValue ? -1 : minCap)}|{capZero}|{capZeroWithStock}|{minPop}"));
        }

        string tag = Environment.GetEnvironmentVariable("CIV_PROBE_TAG") ?? "probe";
        File.WriteAllText(Path.Combine(Path.GetTempPath(), $"driven-attrib-{tag}.psv"), report.ToString());
    }
}
