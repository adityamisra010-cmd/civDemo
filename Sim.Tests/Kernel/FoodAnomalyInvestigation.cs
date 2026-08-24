using System.Globalization;
using System.Text;
using Sim.Core;
using Sim.Core.Kernel;
using Sim.Core.State;
using Sim.Core.Systems;

namespace Sim.Tests.Kernel;

/// <summary>
/// THE FOOD-ANOMALY INVESTIGATION HARNESS.
///
/// The reported anomaly: aggregate food &gt; 0 at turn 47, EXACTLY 0 at turn 48,
/// and &gt; 0 again at turn 49, while population and farming output are positive
/// throughout. This runs the canonical founded world and produces the complete
/// accounting the packet asks for, at two resolutions:
///
///   1. PER TURN, whole world — the closed identity
///      start + harvest - eaten - spoilage - granary (+/- other) = end,
///      with the residual printed. A non-zero residual is a law-1 defect.
///   2. PER PHASE, inside the turns around the anomaly — grain stock after each
///      of the thirteen systems, so "food = 0" can be attributed to a system
///      rather than to a turn.
///   3. PER SETTLEMENT, in the same window — because a world total of zero and
///      every settlement at zero are different facts.
///
/// These are DIAGNOSTICS, not assertions about intended behaviour. The one thing
/// they do assert is conservation, which is law 1 and is not a matter of taste.
/// </summary>
public sealed class FoodAnomalyInvestigation
{
    private const ulong Seed = 42UL;
    private const int Turns = 60;
    private const int WindowFrom = 45;
    private const int WindowTo = 51;

    private static SimConfig Cfg()
    {
        using var sim = Sim.Data.DataFiles.OpenSim();
        using var needs = Sim.Data.DataFiles.OpenNeeds();
        using var goods = Sim.Data.DataFiles.OpenGoods();
        return SimConfigLoader.Load(sim, needs, goods);
    }

    private static TurnExecutor Executor(SimConfig cfg)
    {
        using var era = Sim.Data.DataFiles.OpenEraPacing();
        using var pipe = Sim.Data.DataFiles.OpenPipeline();
        return new TurnExecutor(EraTableLoader.Load(era), PipelineLoader.Load(pipe, SystemCatalog.All(cfg)));
    }

    /// <summary>Records grain stock after every phase of one turn, plus the
    /// per-settlement split. Reads only; the executor hands it an
    /// <see cref="IReadOnlyWorldState"/> so it cannot write even by accident.</summary>
    private sealed class PhaseGrainRecorder(int grainId) : ITurnObserver
    {
        public readonly List<(string Phase, long Stock)> Phases = [];
        public readonly List<(string Phase, List<(int Settlement, long Stock, bool HasRow)> Rows)> Detail = [];

        public void OnPhase(string phase, long ticks, long bytes) { }

        public void OnPhaseState(string phase, IReadOnlyWorldState next)
        {
            Phases.Add((phase, FoodAudit.GrainStock(next, grainId)));
            var rows = new List<(int, long, bool)>();
            for (int s = 0; s < next.Settlements.Count; s++)
            {
                SettlementId id = next.Settlements[s].Id;
                rows.Add((id.Value,
                    FoodAudit.GrainStockOf(next, grainId, id),
                    FoodAudit.HasGrainRow(next, grainId, id)));
            }
            Detail.Add((phase, rows));
        }
    }

    [Fact]
    public void FoodAccountingReconcilesEveryTurn_AndTheAnomalyWindowIsDumped()
    {
        SimConfig cfg = Cfg();
        int grain = cfg.Goods!.GrainId;
        TurnExecutor exec = Executor(cfg);

        WorldState world = Sim.Cli.HeadlessFounding.Found(Seed, null, null);
        var report = new StringBuilder();
        report.AppendLine("# FOOD ANOMALY — CANONICAL FOUNDED WORLD, SEED 42");
        report.AppendLine(Inv($"# grain good id = {grain}; quantity id = {FoodAudit.QuantityOf(grain).Value}"));
        report.AppendLine();
        report.AppendLine("## PER-TURN CLOSED ACCOUNTING (whole world)");
        report.AppendLine("turn | pop | start | +harvest | -eaten | -spoilage | -granary | +otherSrc | -otherSink | end | residual");

        var failures = new List<string>();

        for (int t = 0; t < Turns; t++)
        {
            FoodAudit.FoodSnapshot before = FoodAudit.Snapshot(world, grain, "turn-start");
            bool inWindow = t >= WindowFrom && t <= WindowTo;
            var rec = inWindow ? new PhaseGrainRecorder(grain) : null;

            world = exec.Step(world, rec);

            FoodAudit.FoodSnapshot after = FoodAudit.Snapshot(world, grain, "turn-end");
            FoodAudit.FoodTurnAccount acct = FoodAudit.Account(before, after);

            long pop = 0;
            for (int i = 0; i < world.Buckets.Count; i++) pop += world.Buckets[i].Count.Value;

            report.AppendLine(Inv($"{after.Turn} | {pop} | {acct.StockStart} | {acct.Harvest} | {acct.Eaten} | {acct.Spoilage} | {acct.GranaryOverflow} | {acct.OtherSourced} | {acct.OtherSunk} | {acct.StockEnd} | {acct.Residual}"));

            if (!acct.Reconciles) failures.Add(acct.Line());

            if (rec is not null)
            {
                report.AppendLine();
                report.AppendLine(Inv($"### TURN {after.Turn} — grain stock after each phase"));
                for (int p = 0; p < rec.Phases.Count; p++)
                    report.AppendLine(Inv($"    {rec.Phases[p].Phase,-16} {rec.Phases[p].Stock}"));

                report.AppendLine(Inv($"### TURN {after.Turn} — per-settlement grain by phase (stock, hasRow)"));
                for (int p = 0; p < rec.Detail.Count; p++)
                {
                    var sb = new StringBuilder();
                    sb.Append(Inv($"    {rec.Detail[p].Phase,-16}"));
                    foreach ((int id, long stock, bool has) in rec.Detail[p].Rows)
                        sb.Append(Inv($" s{id}={stock}{(has ? "" : "(NOROW)")}"));
                    report.AppendLine(sb.ToString());
                }
                report.AppendLine();
            }
        }

        if (failures.Count > 0)
        {
            report.AppendLine();
            report.AppendLine("## RESIDUALS — GRAIN MOVED OUTSIDE THE LEDGER");
            foreach (string f in failures) report.AppendLine("    " + f);
        }

        string path = Path.Combine(Path.GetTempPath(), "food-anomaly-report.md");
        File.WriteAllText(path, report.ToString());

        Assert.True(failures.Count == 0,
            $"grain accounting failed to reconcile on {failures.Count} turn(s); report at {path}\n"
            + string.Join("\n", failures));
    }

    /// <summary>One turn's whole-world grain figures, all read from published
    /// state. `CapacityEstimate` is the ONE derived number here and is labelled
    /// as such: the granary capacity is local to BoundStore and is deliberately
    /// NOT re-implemented, so it is inferred from the demand signal the
    /// consumption system itself publishes
    /// (<c>LastConsumptionDemandUnits</c>, which is post-substitution and
    /// includes the sub-unit remainder bank) as
    /// <c>GranaryYearsOfDemand * demanded / dt</c>. Exact to the remainder
    /// bank, i.e. within about one unit per settlement.</summary>
    private readonly record struct TurnFood(
        long Turn, double Dt, long Pop, int Settlements,
        long Start, long Harvest, long Eaten, long Spoilage, long Granary, long End,
        double CapacityEstimate)
    {
        public double StoreOverCapacity => CapacityEstimate > 0.0 ? End / CapacityEstimate : double.NaN;
        /// <summary>How many TURNS of consumption the store at end-of-turn covers.</summary>
        public double StoreTurns => Eaten > 0 ? End / (double)Eaten : double.NaN;
        /// <summary>How many YEARS of consumption it covers.</summary>
        public double StoreYears => Eaten > 0 ? End / (Eaten / Dt) : double.NaN;
        public double DestroyedFractionOfHarvest =>
            Harvest > 0 ? (Spoilage + Granary) / (double)Harvest : double.NaN;
    }

    /// <summary>Grain demand published by the consumption system this turn,
    /// summed over settlements. Read, never recomputed.</summary>
    private static long GrainDemanded(IReadOnlyWorldState w, int grain)
    {
        long d = 0;
        for (int i = 0; i < w.GoodStocks.Count; i++)
            if (w.GoodStocks[i].Good.Value == grain) d += w.GoodStocks[i].LastConsumptionDemandUnits;
        return d;
    }

    private static long Population(IReadOnlyWorldState w)
    {
        long p = 0;
        for (int i = 0; i < w.Buckets.Count; i++) p += w.Buckets[i].Count.Value;
        return p;
    }

    /// <summary>Runs one world and returns the per-turn grain record. Asserts
    /// conservation on every turn — the audit is not optional anywhere.</summary>
    private static List<TurnFood> RunWorld(SimConfig cfg, ulong seed, int turns, double granaryYears)
    {
        int grain = cfg.Goods!.GrainId;
        TurnExecutor exec = Executor(cfg);
        WorldState world = Sim.Cli.HeadlessFounding.Found(seed, null, null);
        var rows = new List<TurnFood>(turns);

        for (int t = 0; t < turns; t++)
        {
            FoodAudit.FoodSnapshot before = FoodAudit.Snapshot(world, grain, "turn-start");
            double dt = world.Clock.DtYears;                 // dt IN FORCE for this step
            world = exec.Step(world);
            FoodAudit.FoodSnapshot after = FoodAudit.Snapshot(world, grain, "turn-end");
            FoodAudit.FoodTurnAccount a = FoodAudit.Account(before, after);
            Assert.True(a.Reconciles, $"seed {seed} turn {after.Turn}: {a.Line()}");

            rows.Add(new TurnFood(
                after.Turn, dt, Population(world), world.Settlements.Count,
                a.StockStart, a.Harvest, a.Eaten, a.Spoilage, a.GranaryOverflow, a.StockEnd,
                granaryYears * GrainDemanded(world, grain) / dt));
        }
        return rows;
    }

    /// <summary>
    /// (B) DOES THE REPORTED STATE EXIST AT ALL? Sweeps 40 worlds asking whether
    /// aggregate grain reaches EXACTLY 0 — no epsilon, because the model defines
    /// none and the stock is a `long` — while population is positive. Records the
    /// full per-turn detail at the minimum so "zero grain with people",
    /// "zero population", "merely low", "low harvest" and "capacity-limited"
    /// stay distinguishable.
    /// </summary>
    [Fact]
    public void SweepSeeds_ForAnAggregateFoodZeroWithPositivePopulation()
    {
        SimConfig cfg = Cfg();
        double granaryYears = cfg.Consumption.GranaryYearsOfDemand;
        var report = new StringBuilder();
        report.AppendLine("# (B) 40-SEED SWEEP — can grain reach EXACTLY 0 with population > 0?");
        report.AppendLine(Inv($"# 120 turns/seed. granaryYearsOfDemand={granaryYears}. No epsilon: stock is a long."));
        report.AppendLine();
        report.AppendLine("seed | sett | minGrain@turn | zeroWithPop | firstZeroTurn | popAtMin | dtAtMin | harvest@min | eaten@min | spoil@min | overflow@min | capEst@min | store/cap@min | settlementZeroTurns | minGrain(first60) | endPop");

        int seedsWithZero = 0;
        long globalMin = long.MaxValue;
        double minRatio = double.MaxValue, maxRatio = 0.0;

        for (ulong seed = 1; seed <= 40; seed++)
        {
            List<TurnFood> rows = RunWorld(cfg, seed, 120, granaryYears);

            TurnFood min = rows[0];
            foreach (TurnFood r in rows) if (r.End < min.End) min = r;
            long min60 = long.MaxValue;
            foreach (TurnFood r in rows) if (r.Turn <= 60 && r.End < min60) min60 = r.End;

            long firstZero = -1;
            bool zeroWithPop = false;
            foreach (TurnFood r in rows)
                if (r.End == 0 && r.Pop > 0) { zeroWithPop = true; if (firstZero < 0) firstZero = r.Turn; }

            // Per-settlement zeroes are the same mechanism without cross-settlement
            // averaging, counted separately from the world total.
            int settlementZeroTurns = 0;
            {
                TurnExecutor exec2 = Executor(cfg);
                WorldState w = Sim.Cli.HeadlessFounding.Found(seed, null, null);
                for (int t = 0; t < 120; t++)
                {
                    w = exec2.Step(w);
                    for (int s = 0; s < w.Settlements.Count; s++)
                        if (FoodAudit.GrainStockOf(w, cfg.Goods!.GrainId, w.Settlements[s].Id) == 0)
                        { settlementZeroTurns++; break; }
                }
            }

            if (zeroWithPop) seedsWithZero++;
            if (min.End < globalMin) globalMin = min.End;
            foreach (TurnFood r in rows)
            {
                double q = r.StoreOverCapacity;
                if (double.IsNaN(q)) continue;
                if (q < minRatio) minRatio = q;
                if (q > maxRatio) maxRatio = q;
            }

            report.AppendLine(Inv(
                $"{seed} | {min.Settlements} | {min.End}@{min.Turn} | {zeroWithPop} | {firstZero} | {min.Pop} | {min.Dt} | {min.Harvest} | {min.Eaten} | {min.Spoilage} | {min.Granary} | {min.CapacityEstimate:F1} | {min.StoreOverCapacity:F4} | {settlementZeroTurns} | {min60} | {rows[^1].Pop}"));
        }

        report.AppendLine();
        report.AppendLine(Inv($"SEEDS REACHING EXACTLY ZERO WITH POSITIVE POPULATION: {seedsWithZero} / 40"));
        report.AppendLine(Inv($"GLOBAL MINIMUM AGGREGATE GRAIN OVER ALL SEEDS/TURNS: {globalMin}"));
        report.AppendLine(Inv($"store/capacity RANGE over all seeds/turns: [{minRatio:F4}, {maxRatio:F4}]"));
        File.WriteAllText(Path.Combine(Path.GetTempPath(), "food-seed-sweep.md"), report.ToString());
    }

    /// <summary>
    /// (C/D) THE DECISIVE TIMESCALE EXPERIMENT. The era table steps dt down
    /// 10 → 5 → 3 → 2 → 1 → 0.5 years. Granary capacity is denominated in YEARS
    /// and spoilage is an annual rate, so BOTH are dt-invariant per year while
    /// the TURN is not. If the anomaly is a timescale artefact rather than a
    /// calibration error, then the destroyed fraction and the store's coverage
    /// measured IN TURNS must move sharply at the era boundaries while the same
    /// quantities measured IN YEARS stay put. That is a falsifiable prediction
    /// and this measures it.
    /// </summary>
    [Fact]
    public void EraTimescaleSensitivity_DtStepsDownAtTurn250()
    {
        SimConfig cfg = Cfg();
        double granaryYears = cfg.Consumption.GranaryYearsOfDemand;
        double spoilRate = cfg.Consumption.GrainSpoilagePerYear;
        List<TurnFood> rows = RunWorld(cfg, 42UL, 320, granaryYears);

        var report = new StringBuilder();
        report.AppendLine("# (C) GRANARY AND SPOILAGE TIMESCALES ACROSS AN ERA BOUNDARY — seed 42, 320 turns");
        report.AppendLine(Inv($"# granaryYearsOfDemand={granaryYears}; grainSpoilagePerYear={spoilRate}"));
        report.AppendLine();
        report.AppendLine("dt | turns | meanDestroyedFractionOfHarvest | predictedSpoilFraction=1-exp(-rate*dt) | meanStoreYears | meanStoreTURNS | meanStore/cap | minStore | zeroTurns");

        foreach (double dt in new[] { 10.0, 5.0, 3.0, 2.0, 1.0, 0.5 })
        {
            var band = rows.Where(r => r.Dt == dt).ToList();
            if (band.Count == 0) continue;
            double destroyed = band.Where(r => r.Harvest > 0).Average(r => r.DestroyedFractionOfHarvest);
            double storeYears = band.Where(r => r.Eaten > 0).Average(r => r.StoreYears);
            double storeTurns = band.Where(r => r.Eaten > 0).Average(r => r.StoreTurns);
            double ratio = band.Where(r => !double.IsNaN(r.StoreOverCapacity)).Average(r => r.StoreOverCapacity);
            long minStore = band.Min(r => r.End);
            int zeros = band.Count(r => r.End == 0 && r.Pop > 0);
            report.AppendLine(Inv(
                $"{dt} | {band.Count} | {destroyed:F4} | {1.0 - Math.Exp(-spoilRate * dt):F4} | {storeYears:F3} | {storeTurns:F3} | {ratio:F4} | {minStore} | {zeros}"));
        }

        report.AppendLine();
        report.AppendLine("## SPOILAGE dt-INVARIANCE, checked arithmetically against the source formula");
        report.AppendLine("dt | 1-exp(-0.08*dt) | survival^(1/dt) (must be constant if dt-invariant)");
        foreach (double dt in new[] { 10.0, 5.0, 3.0, 2.0, 1.0, 0.5 })
        {
            double lost = 1.0 - Math.Exp(-spoilRate * dt);
            report.AppendLine(Inv($"{dt} | {lost:F6} | {Math.Pow(1.0 - lost, 1.0 / dt):F9}"));
        }

        File.WriteAllText(Path.Combine(Path.GetTempPath(), "food-era-timescale.md"), report.ToString());
    }

    /// <summary>
    /// (E) THE PROCYCLICAL-OVERFLOW HYPOTHESIS, MEASURED RATHER THAN ASSUMED:
    /// population falls → demand falls → capacity falls → more grain becomes
    /// overflow → the buffer weakens. The test is a correlation between the
    /// population change and the NEXT turn's capacity, plus a full trace of the
    /// turns following the worst harvests.
    /// </summary>
    [Fact]
    public void PropagationAfterLowHarvest_AndTheProcyclicalCapacityChain()
    {
        SimConfig cfg = Cfg();
        double granaryYears = cfg.Consumption.GranaryYearsOfDemand;
        var report = new StringBuilder();
        report.AppendLine("# (E) PROPAGATION AFTER A LOW-HARVEST TURN — seeds 42, 7, 13, 23");

        foreach (ulong seed in new ulong[] { 42, 7, 13, 23 })
        {
            List<TurnFood> rows = RunWorld(cfg, seed, 120, granaryYears);

            // The five worst harvests relative to that turn's consumption.
            var worst = rows.Where(r => r.Eaten > 0 && r.Turn > 1)
                            .OrderBy(r => r.Harvest / (double)r.Eaten)
                            .ThenBy(r => r.Turn)     // stable integer tie-break
                            .Take(5).OrderBy(r => r.Turn).ToList();

            report.AppendLine();
            report.AppendLine(Inv($"## SEED {seed} — the five lowest harvest:consumption turns, with the turn before and after"));
            report.AppendLine("turn | dt | pop | dPop | preHarvestStore | harvest | h/eaten | eaten | spoil | overflow | end | capEst | store/cap | NEXTcapEst | dCap");

            foreach (TurnFood r in worst)
            {
                int i = rows.FindIndex(x => x.Turn == r.Turn);
                for (int k = Math.Max(0, i - 1); k <= Math.Min(rows.Count - 1, i + 1); k++)
                {
                    TurnFood c = rows[k];
                    long dPop = k > 0 ? c.Pop - rows[k - 1].Pop : 0;
                    double nextCap = k + 1 < rows.Count ? rows[k + 1].CapacityEstimate : double.NaN;
                    double dCap = k + 1 < rows.Count ? nextCap - c.CapacityEstimate : double.NaN;
                    report.AppendLine(Inv(
                        $"{c.Turn}{(c.Turn == r.Turn ? "*" : " ")} | {c.Dt} | {c.Pop} | {dPop} | {c.Start} | {c.Harvest} | {c.Harvest / (double)c.Eaten:F3} | {c.Eaten} | {c.Spoilage} | {c.Granary} | {c.End} | {c.CapacityEstimate:F1} | {c.StoreOverCapacity:F4} | {nextCap:F1} | {dCap:F1}"));
                }
                report.AppendLine("    ---");
            }

            // THE HYPOTHESIS, as a number: over every consecutive pair, does a
            // population fall predict a capacity fall on the following turn?
            int popFell = 0, popFellAndCapFell = 0;
            for (int k = 1; k + 1 < rows.Count; k++)
            {
                if (rows[k].Pop >= rows[k - 1].Pop) continue;
                popFell++;
                if (rows[k + 1].CapacityEstimate < rows[k].CapacityEstimate) popFellAndCapFell++;
            }
            report.AppendLine(Inv(
                $"SEED {seed}: turns where population FELL = {popFell}; of those, next-turn capacity ALSO fell = {popFellAndCapFell}"));
        }

        File.WriteAllText(Path.Combine(Path.GetTempPath(), "food-propagation.md"), report.ToString());
    }

    private static string Inv(FormattableString s) => s.ToString(CultureInfo.InvariantCulture);
}
