using System.Globalization;
using System.Text;
using Sim.Core;
using Sim.Core.Kernel;
using Sim.Core.State;
using Sim.Core.Systems;

namespace Sim.Tests.Kernel;

/// <summary>
/// THE CONTROLLED dt EXPERIMENT (agent C).
///
/// Hypothesis under test: "coarse temporal resolution alone changes the
/// qualitative food-storage behaviour."
///
/// Six configurations differ in EXACTLY ONE thing: a constant dtYears of
/// 10 / 5 / 3 / 2 / 1 / 0.5, supplied by an era table built here in test code
/// (production era-pacing.json is untouched). Everything else — pipeline,
/// SimConfig, founding, seeds — is the production article.
///
/// The control that makes the comparison meaningful: every case runs the SAME
/// NUMBER OF SIMULATED YEARS, not the same number of turns.
///
/// DIAGNOSTIC, not an acceptance test. The one hard assertion is conservation
/// (law 1), which is not a matter of taste.
/// </summary>
public sealed class DtControlledFoodExperiment
{
    private static readonly double[] DtCases = [10.0, 5.0, 3.0, 2.0, 1.0, 0.5];
    private static readonly ulong[] Seeds = [42UL, 7UL, 13UL];

    /// <summary>Sim-years every case runs. Override with CIV_DT_YEARS.</summary>
    private static int Horizon =>
        int.TryParse(Environment.GetEnvironmentVariable("CIV_DT_YEARS"),
            NumberStyles.Integer, CultureInfo.InvariantCulture, out int h) && h > 0 ? h : 300;

    private static string Inv(FormattableString s) => s.ToString(CultureInfo.InvariantCulture);

    private static SimConfig Cfg()
    {
        using var sim = Sim.Data.DataFiles.OpenSim();
        using var needs = Sim.Data.DataFiles.OpenNeeds();
        using var goods = Sim.Data.DataFiles.OpenGoods();
        return SimConfigLoader.Load(sim, needs, goods);
    }

    /// <summary>A one-band era table spanning the whole campaign at a CONSTANT
    /// dt. Built through the production loader from an in-memory JSON string —
    /// no data file is read or written.</summary>
    private static EraTable ConstantDt(double dtYears)
    {
        // -4000 .. 500 = 4500 years, an exact multiple of every dt tested.
        string json = Inv($@"{{ ""bands"": [ {{ ""name"": ""constant"", ""startYear"": -4000, ""endYear"": 500, ""dtYears"": {dtYears} }} ] }}");
        return EraTableLoader.Load(json);
    }

    private static TurnExecutor Executor(SimConfig cfg, double dtYears)
    {
        using var pipe = Sim.Data.DataFiles.OpenPipeline();
        return new TurnExecutor(ConstantDt(dtYears), PipelineLoader.Load(pipe, SystemCatalog.All(cfg)));
    }

    private sealed class Result
    {
        public double Dt;
        public ulong Seed;
        public long Turns;
        public long EndStock, MinStock, MaxStock;
        public long TotalHarvest, TotalEaten, TotalSpoilage, TotalOverflow;
        public long ZeroStockTurnsWithPop;
        public double MinStoreOverCapacity = double.PositiveInfinity;
        public long DeficitSettlementTurns, TotalSettlementTurns;
        public long EndPop;
        public readonly List<(int Year, long Pop, long Stock)> Checkpoints = [];
        public readonly List<string> Residuals = [];
    }

    [Fact]
    public void ConstantDtSweep_FoodStorageBehaviour()
    {
        int horizon = Horizon;
        SimConfig cfg = Cfg();
        int grain = cfg.Goods!.GrainId;
        double granaryYears = cfg.Consumption.GranaryYearsOfDemand;
        double spoilPerYear = cfg.Consumption.GrainSpoilagePerYear;

        var report = new StringBuilder();
        report.AppendLine("# CONTROLLED dt EXPERIMENT — food storage vs temporal resolution");
        report.AppendLine();
        report.AppendLine(Inv($"horizon = {horizon} sim-years (identical for every dt); seeds = 42, 7, 13"));
        report.AppendLine(Inv($"grain good id = {grain}; GranaryYearsOfDemand = {granaryYears}; GrainSpoilagePerYear = {spoilPerYear}"));
        report.AppendLine();

        // --- arithmetic check: dt-invariance of the survival exponential -----
        report.AppendLine("## SPOILAGE dt-INVARIANCE ARITHMETIC");
        report.AppendLine("dt | perTurnLoss = 1-exp(-0.08*dt) | survival = 1-loss | survival^(1/dt) | exp(-0.08)");
        foreach (double dt in DtCases)
        {
            double loss = 1.0 - Math.Exp(-spoilPerYear * dt);
            double surv = 1.0 - loss;
            report.AppendLine(Inv($"{dt} | {loss:F10} | {surv:F10} | {Math.Pow(surv, 1.0 / dt):F12} | {Math.Exp(-spoilPerYear):F12}"));
        }
        report.AppendLine();

        var all = new List<Result>();
        var failures = new List<string>();

        foreach (double dt in DtCases)
        {
            long turns = (long)Math.Round(horizon / dt);
            foreach (ulong seed in Seeds)
            {
                var r = new Result { Dt = dt, Seed = seed, Turns = turns };
                TurnExecutor exec = Executor(cfg, dt);
                WorldState world = Sim.Cli.HeadlessFounding.Found(seed, null, null);
                r.MinStock = FoodAudit.GrainStock(world, grain);
                r.MaxStock = r.MinStock;
                int nextCheckpoint = 50;

                for (long t = 0; t < turns; t++)
                {
                    FoodAudit.FoodSnapshot before = FoodAudit.Snapshot(world, grain, "turn-start");
                    world = exec.Step(world);
                    FoodAudit.FoodSnapshot after = FoodAudit.Snapshot(world, grain, "turn-end");
                    FoodAudit.FoodTurnAccount a = FoodAudit.Account(before, after);
                    if (!a.Reconciles)
                    {
                        string line = Inv($"dt={dt} seed={seed} ") + a.Line();
                        r.Residuals.Add(line);
                        failures.Add(line);
                    }

                    r.TotalHarvest += a.Harvest;
                    r.TotalEaten += a.Eaten;
                    r.TotalSpoilage += a.Spoilage;
                    r.TotalOverflow += a.GranaryOverflow;

                    long stock = a.StockEnd;
                    if (stock < r.MinStock) r.MinStock = stock;
                    if (stock > r.MaxStock) r.MaxStock = stock;

                    long pop = 0;
                    for (int i = 0; i < world.Buckets.Count; i++) pop += world.Buckets[i].Count.Value;
                    if (stock == 0 && pop > 0) r.ZeroStockTurnsWithPop++;

                    // deficits: one row per settlement, rewritten each turn
                    for (int i = 0; i < world.ConsumptionDeficits.Count; i++)
                    {
                        r.TotalSettlementTurns++;
                        if (world.ConsumptionDeficits[i].DeficitRatio > 0.0) r.DeficitSettlementTurns++;
                    }

                    // store / capacity, aggregated over settlements. Capacity is
                    // reconstructed from the row the consumption system itself
                    // wrote: LastConsumptionDemandUnits is the per-turn grain
                    // demand (base + substitution) that BoundStore divided by dt.
                    long aggStock = 0, aggCap = 0;
                    for (int i = 0; i < world.GoodStocks.Count; i++)
                    {
                        GoodStockRow row = world.GoodStocks[i];
                        if (row.Good.Value != grain) continue;
                        aggStock += row.Amount.Value;
                        double annual = row.LastConsumptionDemandUnits / dt;
                        aggCap += (long)Math.Round(granaryYears * annual, MidpointRounding.AwayFromZero);
                    }
                    if (aggCap > 0)
                    {
                        double ratio = aggStock / (double)aggCap;
                        if (ratio < r.MinStoreOverCapacity) r.MinStoreOverCapacity = ratio;
                    }

                    double years = world.Clock.SimDays / (double)SimClock.YearDays;   // elapsed sim-years since campaign start
                    while (nextCheckpoint <= horizon && years >= nextCheckpoint - 1e-9)
                    {
                        r.Checkpoints.Add((nextCheckpoint, pop, stock));
                        nextCheckpoint += 50;
                    }

                    if (t == turns - 1) { r.EndStock = stock; r.EndPop = pop; }
                }

                all.Add(r);
                Console.WriteLine(Inv($"done dt={dt} seed={seed} turns={turns} endStock={r.EndStock} endPop={r.EndPop}"));
            }
        }

        // ---- reporting -------------------------------------------------------
        report.AppendLine("## PER-RUN RESULTS");
        report.AppendLine("dt | seed | turns | endStock | minStock | maxStock | harvest | eaten | spoilage | overflow | zeroStockTurns | minStore/Cap | deficitSettTurns/total");
        foreach (Result r in all)
            report.AppendLine(Inv($"{r.Dt} | {r.Seed} | {r.Turns} | {r.EndStock} | {r.MinStock} | {r.MaxStock} | {r.TotalHarvest} | {r.TotalEaten} | {r.TotalSpoilage} | {r.TotalOverflow} | {r.ZeroStockTurnsWithPop} | {r.MinStoreOverCapacity:F4} | {r.DeficitSettlementTurns}/{r.TotalSettlementTurns}"));
        report.AppendLine();

        report.AppendLine("## NORMALISED — PER-YEAR vs PER-TURN (mean over seeds)");
        // destroyed/harvest is a DIMENSIONLESS ratio of two per-turn quantities; there is
        // no "per year" form of it and dividing by dt would be meaningless. What DOES
        // change with dt is its COMPOSITION, so spoilage- and overflow-shares are reported.
        report.AppendLine("dt | store in YEARS of consumption | store in TURNS of consumption | spoilage/store per turn (proxy) | annualised | destroyed/harvest (dimensionless) | spoilage share of destroyed | overflow share of destroyed | deficit frac of settlement-turns | deficit events per settlement-year | zero-stock turns per 100 yr | endPop");
        foreach (double dt in DtCases)
        {
            var rs = all.Where(x => x.Dt == dt).ToList();
            double storeYears = rs.Average(x => x.TotalEaten > 0 ? x.EndStock / (x.TotalEaten / (double)x.Turns / dt) : double.NaN);
            double storeTurns = rs.Average(x => x.TotalEaten > 0 ? x.EndStock / (x.TotalEaten / (double)x.Turns) : double.NaN);
            // spoilage as a fraction of the store it acted on: spoilage/(spoilage+surviving) is not
            // recoverable post hoc, so use spoilage per turn over mean store as the observable.
            double meanStore = rs.Average(x => (x.MinStock + x.MaxStock) / 2.0);
            double spoilPerTurn = rs.Average(x => x.TotalSpoilage / (double)x.Turns);
            double spoilFrac = spoilPerTurn / Math.Max(meanStore, 1.0);
            double annualisedSpoilFrac = 1.0 - Math.Pow(1.0 - Math.Min(spoilFrac, 0.999999), 1.0 / dt);
            double destroyedFracTurn = rs.Average(x => x.TotalHarvest > 0 ? (x.TotalSpoilage + x.TotalOverflow) / (double)x.TotalHarvest : double.NaN);
            double defFrac = rs.Average(x => x.TotalSettlementTurns > 0 ? x.DeficitSettlementTurns / (double)x.TotalSettlementTurns : double.NaN);
            double defPerSettYear = defFrac / dt;
            double zeroPer100 = rs.Average(x => x.ZeroStockTurnsWithPop / (double)horizon * 100.0);
            double endPop = rs.Average(x => (double)x.EndPop);
            double spoilShare = rs.Average(x => (x.TotalSpoilage + x.TotalOverflow) > 0 ? x.TotalSpoilage / (double)(x.TotalSpoilage + x.TotalOverflow) : double.NaN);
            report.AppendLine(Inv($"{dt} | {storeYears:F4} | {storeTurns:F4} | {spoilFrac:F6} | {annualisedSpoilFrac:F6} | {destroyedFracTurn:F6} | {spoilShare:F4} | {1.0 - spoilShare:F4} | {defFrac:F6} | {defPerSettYear:F6} | {zeroPer100:F3} | {endPop:F0}"));
        }
        report.AppendLine();

        report.AppendLine("## POPULATION AT EQUAL SIM-YEAR CHECKPOINTS (mean over seeds)");
        var years0 = all.SelectMany(x => x.Checkpoints.Select(c => c.Year)).Distinct().OrderBy(y => y).ToList();
        report.Append("dt");
        foreach (int y in years0) report.Append(Inv($" | y{y}"));
        report.AppendLine();
        foreach (double dt in DtCases)
        {
            var rs = all.Where(x => x.Dt == dt).ToList();
            report.Append(Inv($"{dt}"));
            foreach (int y in years0)
            {
                var vals = rs.Select(x => x.Checkpoints.FirstOrDefault(c => c.Year == y)).Where(c => c.Year == y).ToList();
                report.Append(vals.Count > 0 ? Inv($" | {vals.Average(v => (double)v.Pop):F0}") : " | -");
            }
            report.AppendLine();
        }
        report.AppendLine();

        report.AppendLine("## GRAIN STOCK AT EQUAL SIM-YEAR CHECKPOINTS (mean over seeds)");
        report.Append("dt");
        foreach (int y in years0) report.Append(Inv($" | y{y}"));
        report.AppendLine();
        foreach (double dt in DtCases)
        {
            var rs = all.Where(x => x.Dt == dt).ToList();
            report.Append(Inv($"{dt}"));
            foreach (int y in years0)
            {
                var vals = rs.Select(x => x.Checkpoints.FirstOrDefault(c => c.Year == y)).Where(c => c.Year == y).ToList();
                report.Append(vals.Count > 0 ? Inv($" | {vals.Average(v => (double)v.Stock):F0}") : " | -");
            }
            report.AppendLine();
        }
        report.AppendLine();

        report.AppendLine("## CONSERVATION");
        report.AppendLine(failures.Count == 0
            ? "FoodAudit residual was EXACTLY 0 on every turn of every run, at every dt."
            : Inv($"{failures.Count} NON-ZERO RESIDUALS — law-1 defect:"));
        foreach (string f in failures.Take(40)) report.AppendLine("    " + f);

        File.WriteAllText("/tmp/agentC-findings.md", report.ToString());
        Console.WriteLine(report.ToString());

        Assert.Empty(failures);
    }
}
