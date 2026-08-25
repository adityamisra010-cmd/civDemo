using System.Globalization;
using System.Text;
using Sim.Core;
using Sim.Core.Kernel;
using Sim.Core.State;
using Sim.Core.Systems;

namespace Sim.Tests.Kernel;

/// <summary>
/// EVIDENCE HARNESS B, PART 2 — PROPAGATION TRACES.
///
/// For each traced case (an ordinary seed, a seed at its own lowest-food event,
/// and — if the sweep found one — an exact-zero event) this prints the turn
/// before, the turn of, and the turn after, with the whole-world grain account,
/// and INSIDE each of those turns the grain stock after every one of the
/// thirteen pipeline phases, read through <see cref="ITurnObserver.OnPhaseState"/>.
/// Grain is written only by production (Harvest), appropriation (an internal
/// transfer, so it must leave the world total unchanged) and consumption
/// (Eaten, then Spoilage, then GranaryOverflow). The per-phase column therefore
/// localises "the outcome became inevitable HERE" to a phase, not a turn.
///
/// It also measures — never assumes — the procyclical chain:
/// population falls -&gt; demand falls -&gt; capacity falls -&gt; more grain overflows.
/// </summary>
public sealed class FoodPropagationTraceEvidence
{
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

    /// <summary>Grain after every pipeline phase of one turn. Read-only: the
    /// executor hands it an <see cref="IReadOnlyWorldState"/>.</summary>
    private sealed class PhaseTrace(int grainId) : ITurnObserver
    {
        public readonly List<(string Phase, long Stock, long Demand)> Phases = [];
        public void OnPhase(string phase, long ticks, long bytes) { }
        public void OnPhaseState(string phase, IReadOnlyWorldState next)
        {
            long demand = 0;
            for (int i = 0; i < next.GoodStocks.Count; i++)
                if (next.GoodStocks[i].Good.Value == grainId)
                    demand += next.GoodStocks[i].LastConsumptionDemandUnits;
            Phases.Add((phase, FoodAudit.GrainStock(next, grainId), demand));
        }
    }

    /// <summary>Runs one seed, tracing every turn's phases, asserting conservation
    /// on every turn.</summary>
    private static (List<FoodZeroSweepEvidence.Row> Rows, List<List<(string Phase, long Stock, long Demand)>> Traces)
        RunTraced(SimConfig cfg, ulong seed, int turns)
    {
        int grain = cfg.Goods!.GrainId;
        double granaryYears = cfg.Consumption.GranaryYearsOfDemand;
        TurnExecutor exec = Executor(cfg);
        WorldState world = Sim.Cli.HeadlessFounding.Found(seed, null, null);
        var rows = new List<FoodZeroSweepEvidence.Row>(turns);
        var traces = new List<List<(string, long, long)>>(turns);
        long prevEnd = FoodAudit.GrainStock(world, grain);

        for (int t = 0; t < turns; t++)
        {
            FoodAudit.FoodSnapshot before = FoodAudit.Snapshot(world, grain, "turn-start");
            double dt = world.Clock.DtYears;
            var rec = new PhaseTrace(grain);
            world = exec.Step(world, rec);
            FoodAudit.FoodSnapshot after = FoodAudit.Snapshot(world, grain, "turn-end");
            FoodAudit.FoodTurnAccount a = FoodAudit.Account(before, after);
            Assert.True(a.Reconciles, Inv($"seed {seed} turn {after.Turn}: ") + a.Line());

            long pop = 0;
            for (int i = 0; i < world.Buckets.Count; i++) pop += world.Buckets[i].Count.Value;
            long demanded = 0;
            for (int i = 0; i < world.GoodStocks.Count; i++)
                if (world.GoodStocks[i].Good.Value == grain) demanded += world.GoodStocks[i].LastConsumptionDemandUnits;
            int zero = 0, noRow = 0;
            for (int s = 0; s < world.Settlements.Count; s++)
            {
                SettlementId id = world.Settlements[s].Id;
                if (!FoodAudit.HasGrainRow(world, grain, id)) noRow++;
                if (FoodAudit.GrainStockOf(world, grain, id) == 0) zero++;
            }

            rows.Add(new FoodZeroSweepEvidence.Row(
                after.Turn, dt, pop, world.Settlements.Count,
                prevEnd, a.StockStart, a.Harvest, a.Eaten, a.Spoilage, a.GranaryOverflow, a.StockEnd,
                demanded, granaryYears * demanded / dt, zero, noRow));
            traces.Add(rec.Phases);
            prevEnd = a.StockEnd;
        }
        return (rows, traces);
    }

    /// <summary>The traced seeds. Seed 42 is the ordinary case; seed 24 holds the
    /// sweep's global minimum aggregate grain (2602 at turn 11); seed 16 has the
    /// most turns with an individual settlement at EXACTLY 0 grain (7). The trace
    /// re-measures each rather than trusting the sweep's summary.</summary>
    private static readonly ulong[] TracedSeeds = [42UL, 24UL, 16UL];

    [Fact]
    public void PropagationTraces_AndTheProcyclicalCapacityChain()
    {
        SimConfig cfg = Cfg();
        var r = new StringBuilder();
        r.AppendLine("# (2) PROPAGATION TRACES — turn before / turn of / turn after, with intra-turn phase detail");
        r.AppendLine(Inv($"# granaryYearsOfDemand = {cfg.Consumption.GranaryYearsOfDemand}"));
        r.AppendLine("# capacityEstimate = granaryYearsOfDemand * Σ LastConsumptionDemandUnits / dt — AN ESTIMATE,");
        r.AppendLine("# exact to the sub-unit remainder bank. Grain stock is a long; zero means exactly 0.");

        int popFellAll = 0, popFellCapFellAll = 0, popRoseAll = 0, popRoseCapFellAll = 0;

        foreach (ulong seed in TracedSeeds)
        {
            (List<FoodZeroSweepEvidence.Row> rows, var traces) = RunTraced(cfg, seed, Turns);

            // The event of interest, in priority order and measured, not assumed:
            //   1. an AGGREGATE exact zero with positive population (earliest),
            //   2. else an individual settlement at EXACTLY 0 grain (earliest),
            //   3. else the lowest aggregate-food turn (earliest on ties).
            int ev = -1;
            string kind = "";
            for (int i = 0; i < rows.Count; i++)
                if (rows[i].End == 0 && rows[i].Pop > 0) { ev = i; kind = "AGGREGATE EXACT-ZERO EVENT"; break; }
            if (ev < 0)
                for (int i = 0; i < rows.Count; i++)
                    if (rows[i].SettlementsAtZero > 0) { ev = i; kind = "PER-SETTLEMENT EXACT-ZERO EVENT"; break; }
            if (ev < 0)
            {
                ev = 0; kind = "LOWEST-FOOD EVENT";
                for (int i = 1; i < rows.Count; i++)
                    if (rows[i].End < rows[ev].End) ev = i;
            }
            // Always ALSO trace the seed's lowest aggregate-food turn.
            int lowest = 0;
            for (int i = 1; i < rows.Count; i++) if (rows[i].End < rows[lowest].End) lowest = i;

            void Window(int centre, string label)
            {
                r.AppendLine();
                r.AppendLine(Inv($"## SEED {seed} — {label} at turn {rows[centre].Turn} (endStock {rows[centre].End}, pop {rows[centre].Pop})"));
                r.AppendLine("turn | dt | pop | dPop | preHarvestStore(=start) | harvest | h/eaten | eaten | spoil | overflow | end | capEstThisTurn | NEXTturnCapEst | store/cap | settlementsAt0 | settlementsNoRow");

                for (int k = Math.Max(0, centre - 1); k <= Math.Min(rows.Count - 1, centre + 1); k++)
                {
                    FoodZeroSweepEvidence.Row c = rows[k];
                    long dPop = k > 0 ? c.Pop - rows[k - 1].Pop : 0;
                    double nextCap = k + 1 < rows.Count ? rows[k + 1].CapacityEstimate : double.NaN;
                    r.AppendLine(Inv($"{c.Turn}{(k == centre ? "*" : " ")} | {c.Dt} | {c.Pop} | {dPop} | {c.Start} | {c.Harvest} | {c.HarvestOverEaten:F3}")
                        + Inv($" | {c.Eaten} | {c.Spoilage} | {c.Granary} | {c.End} | {c.CapacityEstimate:F1} | {nextCap:F1} | {c.StoreOverCapacity:F4} | {c.SettlementsAtZero} | {c.SettlementsNoRow}"));
                }

                r.AppendLine();
                r.AppendLine("### INTRA-TURN: aggregate grain after each pipeline phase (\"clone\" = beginning of turn)");
                for (int k = Math.Max(0, centre - 1); k <= Math.Min(rows.Count - 1, centre + 1); k++)
                {
                    r.AppendLine(Inv($"  turn {rows[k].Turn}{(k == centre ? "  <-- event" : "")}:"));
                    long prev = long.MinValue;
                    foreach ((string phase, long stock, long demand) in traces[k])
                    {
                        string delta = prev == long.MinValue ? "" : Inv($"  delta {stock - prev:+#;-#;0}");
                        r.AppendLine(Inv($"    {phase,-16} stock={stock,12}{delta}  publishedDemand={demand}"));
                        prev = stock;
                    }
                }
            }

            Window(ev, kind);
            if (lowest != ev) Window(lowest, "LOWEST AGGREGATE-FOOD EVENT");

            // THE HYPOTHESIS, AS A NUMBER, per seed and pooled.
            int popFell = 0, popFellCapFell = 0, popRose = 0, popRoseCapFell = 0;
            for (int k = 1; k + 1 < rows.Count; k++)
            {
                bool capFell = rows[k + 1].CapacityEstimate < rows[k].CapacityEstimate;
                if (rows[k].Pop < rows[k - 1].Pop) { popFell++; if (capFell) popFellCapFell++; }
                else if (rows[k].Pop > rows[k - 1].Pop) { popRose++; if (capFell) popRoseCapFell++; }
            }
            popFellAll += popFell; popFellCapFellAll += popFellCapFell;
            popRoseAll += popRose; popRoseCapFellAll += popRoseCapFell;
            r.AppendLine();
            r.AppendLine(Inv($"SEED {seed}: P(next-turn capacity falls | population fell) = {popFellCapFell}/{popFell}")
                + Inv($" = {(popFell > 0 ? popFellCapFell / (double)popFell : double.NaN):F4}; ")
                + Inv($"baseline P(capacity falls | population rose) = {popRoseCapFell}/{popRose}")
                + Inv($" = {(popRose > 0 ? popRoseCapFell / (double)popRose : double.NaN):F4}"));
        }

        r.AppendLine();
        r.AppendLine(Inv($"## POOLED CONDITIONAL RATE (seeds {string.Join(", ", TracedSeeds)})"));
        r.AppendLine(Inv($"P(capacity falls next turn | population fell) = {popFellCapFellAll}/{popFellAll}")
            + Inv($" = {(popFellAll > 0 ? popFellCapFellAll / (double)popFellAll : double.NaN):F4}"));
        r.AppendLine(Inv($"P(capacity falls next turn | population rose) = {popRoseCapFellAll}/{popRoseAll}")
            + Inv($" = {(popRoseAll > 0 ? popRoseCapFellAll / (double)popRoseAll : double.NaN):F4}"));

        File.WriteAllText("/tmp/agentB-traces.md", r.ToString());
    }
}
