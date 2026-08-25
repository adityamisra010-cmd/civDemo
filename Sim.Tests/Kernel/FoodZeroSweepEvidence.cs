using System.Collections.Concurrent;
using System.Globalization;
using System.Text;
using Sim.Core;
using Sim.Core.Kernel;
using Sim.Core.State;
using Sim.Core.Systems;

namespace Sim.Tests.Kernel;

/// <summary>
/// EVIDENCE HARNESS B — the 40-seed EXACT-ZERO sweep and the propagation traces.
///
/// Question 1: can end-of-turn AGGREGATE grain be EXACTLY 0 while total
/// population is &gt; 0? Grain stock is a `long`; "zero" means 0, no epsilon.
///
/// Question 2: where in the thirteen-phase pipeline does a low/zero outcome
/// become inevitable? Answered with an <see cref="ITurnObserver"/> reading
/// grain after every phase, never by re-deriving a system's formula.
///
/// CAPACITY IS AN ESTIMATE AND IS LABELLED ONE. The granary ceiling lives inside
/// BoundStore and is deliberately not re-implemented here. It is inferred from
/// the demand the consumption system itself publishes,
/// <c>GoodStockRow.LastConsumptionDemandUnits</c> (post-substitution), as
/// <c>GranaryYearsOfDemand * Σdemand / dt</c>. Accurate to the sub-unit
/// remainder bank, i.e. of order one unit per settlement.
///
/// Read-only throughout: nothing under Sim.Core / Sim.Data / Sim.Cli is touched.
/// </summary>
public sealed class FoodZeroSweepEvidence
{
    private const int Seeds = 40;
    private const int Turns = 120;

    private static string Inv(FormattableString s) => s.ToString(CultureInfo.InvariantCulture);

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

    private static long Population(IReadOnlyWorldState w)
    {
        long p = 0;
        for (int i = 0; i < w.Buckets.Count; i++) p += w.Buckets[i].Count.Value;
        return p;
    }

    /// <summary>Grain demand PUBLISHED by the consumption system, summed over
    /// settlements. Read, never recomputed.</summary>
    private static long GrainDemanded(IReadOnlyWorldState w, int grain)
    {
        long d = 0;
        for (int i = 0; i < w.GoodStocks.Count; i++)
            if (w.GoodStocks[i].Good.Value == grain) d += w.GoodStocks[i].LastConsumptionDemandUnits;
        return d;
    }

    /// <summary>How many settlements hold EXACTLY zero grain, and how many have
    /// no grain row at all (a different state).</summary>
    private static (int Zero, int NoRow) SettlementZeroes(IReadOnlyWorldState w, int grain)
    {
        int zero = 0, noRow = 0;
        for (int s = 0; s < w.Settlements.Count; s++)
        {
            SettlementId id = w.Settlements[s].Id;
            bool has = FoodAudit.HasGrainRow(w, grain, id);
            if (!has) noRow++;
            if (FoodAudit.GrainStockOf(w, grain, id) == 0) zero++;
        }
        return (zero, noRow);
    }

    internal readonly record struct Row(
        long Turn, double Dt, long Pop, int Settlements,
        long PrevEnd, long Start, long Harvest, long Eaten, long Spoilage, long Granary, long End,
        long Demanded, double CapacityEstimate, int SettlementsAtZero, int SettlementsNoRow)
    {
        public double StoreOverCapacity => CapacityEstimate > 0.0 ? End / CapacityEstimate : double.NaN;
        public double HarvestOverEaten => Eaten > 0 ? Harvest / (double)Eaten : double.NaN;
    }

    /// <summary>Runs one seed for `turns` turns, asserting conservation on EVERY
    /// turn (law 1 is never optional), and returns the per-turn record.</summary>
    internal static List<Row> RunSeed(SimConfig cfg, ulong seed, int turns, List<string> conservationFailures)
    {
        int grain = cfg.Goods!.GrainId;
        double granaryYears = cfg.Consumption.GranaryYearsOfDemand;
        TurnExecutor exec = Executor(cfg);
        WorldState world = Sim.Cli.HeadlessFounding.Found(seed, null, null);
        var rows = new List<Row>(turns);
        long prevEnd = FoodAudit.GrainStock(world, grain);

        for (int t = 0; t < turns; t++)
        {
            FoodAudit.FoodSnapshot before = FoodAudit.Snapshot(world, grain, "turn-start");
            double dt = world.Clock.DtYears;                     // dt IN FORCE for this step
            world = exec.Step(world);
            FoodAudit.FoodSnapshot after = FoodAudit.Snapshot(world, grain, "turn-end");
            FoodAudit.FoodTurnAccount a = FoodAudit.Account(before, after);
            if (!a.Reconciles) conservationFailures.Add(Inv($"seed {seed}: ") + a.Line());

            long demanded = GrainDemanded(world, grain);
            (int zero, int noRow) = SettlementZeroes(world, grain);
            rows.Add(new Row(
                after.Turn, dt, Population(world), world.Settlements.Count,
                prevEnd, a.StockStart, a.Harvest, a.Eaten, a.Spoilage, a.GranaryOverflow, a.StockEnd,
                demanded, granaryYears * demanded / dt, zero, noRow));
            prevEnd = a.StockEnd;
        }
        return rows;
    }

    /// <summary>(1) THE 40-SEED EXACT-ZERO SWEEP.</summary>
    [Fact]
    public void Sweep40Seeds_ExactZeroAggregateGrainWithPositivePopulation()
    {
        SimConfig cfg = Cfg();
        double granaryYears = cfg.Consumption.GranaryYearsOfDemand;

        // Seeds are fully independent worlds sharing no mutable state. Results are
        // collected into an array INDEXED BY SEED and printed in seed order, so the
        // output is identical whatever the thread schedule does.
        var perSeed = new List<Row>[Seeds + 1];
        var failures = new ConcurrentBag<string>();
        var crashes = new ConcurrentBag<string>();

        Parallel.For(1, Seeds + 1, new ParallelOptions { MaxDegreeOfParallelism = Environment.ProcessorCount }, i =>
        {
            var local = new List<string>();
            try { perSeed[i] = RunSeed(cfg, (ulong)i, Turns, local); }
            catch (Exception ex) { crashes.Add(Inv($"seed {i}: {ex.GetType().Name}: {ex.Message}")); }
            foreach (string f in local) failures.Add(f);
        });

        var r = new StringBuilder();
        r.AppendLine("# (1) 40-SEED EXACT-ZERO SWEEP — seeds 1..40, 120 turns, canonical founded world");
        r.AppendLine(Inv($"# granaryYearsOfDemand = {granaryYears}; grain good id = {cfg.Goods!.GrainId}"));
        r.AppendLine("# capacity is an ESTIMATE: granaryYearsOfDemand * Σ LastConsumptionDemandUnits / dt,");
        r.AppendLine("# accurate to the sub-unit remainder bank (order one unit per settlement).");
        r.AppendLine("# grain stock is a long: ZERO MEANS EXACTLY 0. No epsilon anywhere below.");
        r.AppendLine();

        int zeroEvents = 0, zeroSeeds = 0, zeroPopTurns = 0;
        var zeroRows = new List<string>();
        var perSeedLines = new List<string>();
        long globalMin = long.MaxValue; ulong globalMinSeed = 0; long globalMinTurn = 0;
        double minRatio = double.MaxValue, maxRatio = double.MinValue;

        for (int s = 1; s <= Seeds; s++)
        {
            List<Row>? rows = perSeed[s];
            if (rows is null) { perSeedLines.Add(Inv($"{s} | RUN FAILED")); continue; }

            Row min = rows[0];
            foreach (Row x in rows) if (x.End < min.End || (x.End == min.End && x.Turn < min.Turn)) min = x;
            long minPos = long.MaxValue; long minPosTurn = -1;
            foreach (Row x in rows) if (x.End > 0 && x.End < minPos) { minPos = x.End; minPosTurn = x.Turn; }
            int settlementZeroTurns = 0, lowHarvestTurns = 0, capBoundTurns = 0;
            double loR = double.MaxValue, hiR = double.MinValue;
            bool seedHasZero = false;
            foreach (Row x in rows)
            {
                if (x.SettlementsAtZero > 0) settlementZeroTurns++;
                if (x.Eaten > 0 && x.Harvest < x.Eaten) lowHarvestTurns++;
                if (x.Granary > 0) capBoundTurns++;
                double q = x.StoreOverCapacity;
                if (!double.IsNaN(q)) { if (q < loR) loR = q; if (q > hiR) hiR = q; }
                if (x.Pop == 0) zeroPopTurns++;
                if (x.End == 0 && x.Pop > 0)
                {
                    seedHasZero = true; zeroEvents++;
                    zeroRows.Add(Inv($"seed {s} | turn {x.Turn} | dt {x.Dt} | pop {x.Pop} | settlements {x.Settlements}")
                        + Inv($" | harvest {x.Harvest} | eaten {x.Eaten} | spoilage {x.Spoilage} | overflow {x.Granary}")
                        + Inv($" | prevEnd {x.PrevEnd} | capEst {x.CapacityEstimate:F1} | demanded {x.Demanded}"));
                }
                if (x.End < globalMin || (x.End == globalMin && (ulong)s < globalMinSeed))
                { globalMin = x.End; globalMinSeed = (ulong)s; globalMinTurn = x.Turn; }
            }
            if (seedHasZero) zeroSeeds++;
            if (loR < minRatio) minRatio = loR;
            if (hiR > maxRatio) maxRatio = hiR;

            string minPosText = minPosTurn < 0
                ? "none"
                : Inv($"{minPos}@{minPosTurn}");
            perSeedLines.Add(Inv($"{s} | {min.End}@{min.Turn} | {minPosText} | {settlementZeroTurns}")
                + Inv($" | {lowHarvestTurns} | {capBoundTurns} | {min.Pop} | {rows[^1].Pop}")
                + Inv($" | {min.Settlements} | [{loR:F4}, {hiR:F4}]"));
        }

        r.AppendLine("## VERDICT");
        r.AppendLine(Inv($"EXACT-ZERO AGGREGATE GRAIN WITH POSITIVE POPULATION: {(zeroEvents > 0 ? "YES" : "NO")}"));
        r.AppendLine(Inv($"  seeds exhibiting it        : {zeroSeeds} / {Seeds}"));
        r.AppendLine(Inv($"  turn-events exhibiting it  : {zeroEvents} / {Seeds * Turns}"));
        r.AppendLine(Inv($"  turns with ZERO POPULATION : {zeroPopTurns} (a different fact, counted apart)"));
        r.AppendLine(Inv($"  global minimum aggregate grain: {globalMin} at seed {globalMinSeed} turn {globalMinTurn}"));
        r.AppendLine(Inv($"  store/capacity range over all seeds/turns: [{minRatio:F4}, {maxRatio:F4}]"));
        r.AppendLine(Inv($"  conservation failures: {failures.Count}"));
        r.AppendLine(Inv($"  seeds that crashed   : {crashes.Count}"));
        r.AppendLine();
        if (zeroRows.Count > 0)
        {
            r.AppendLine("## EVERY EXACT-ZERO EVENT, IN FULL");
            foreach (string line in zeroRows) r.AppendLine("  " + line);
            r.AppendLine();
        }
        r.AppendLine("## PER SEED");
        r.AppendLine("seed | minGrain@turn | minPositiveGrain@turn | turnsWithASettlementAtExactly0 | lowHarvestTurns(h<eaten) | capacityBoundTurns(overflow>0) | popAtMin | endPop | settlements | store/cap range");
        foreach (string line in perSeedLines) r.AppendLine(line);

        if (!failures.IsEmpty)
        {
            r.AppendLine();
            r.AppendLine("## CONSERVATION FAILURES");
            foreach (string f in failures.OrderBy(x => x, StringComparer.Ordinal)) r.AppendLine("  " + f);
        }
        if (!crashes.IsEmpty)
        {
            r.AppendLine();
            r.AppendLine("## CRASHES");
            foreach (string c in crashes.OrderBy(x => x, StringComparer.Ordinal)) r.AppendLine("  " + c);
        }

        File.WriteAllText("/tmp/agentB-sweep.md", r.ToString());
        Assert.True(failures.IsEmpty, "grain conservation failed:\n" + string.Join("\n", failures));
        Assert.True(crashes.IsEmpty, "seed runs crashed:\n" + string.Join("\n", crashes));
    }
}
