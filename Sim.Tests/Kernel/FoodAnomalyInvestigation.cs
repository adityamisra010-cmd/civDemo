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

    /// <summary>
    /// DOES THE REPORTED STATE EXIST AT ALL? Seed 42 never reaches zero, so this
    /// sweeps worlds looking for a turn whose aggregate grain is EXACTLY 0 while
    /// population is positive — the reported condition — and, for every world,
    /// records the minimum ever reached and the per-settlement zero count. A
    /// settlement at zero inside a positive world total is the same mechanism at
    /// smaller scale and is counted separately.
    /// </summary>
    [Fact]
    public void SweepSeeds_ForAnAggregateFoodZeroWithPositivePopulation()
    {
        SimConfig cfg = Cfg();
        int grain = cfg.Goods!.GrainId;
        var report = new StringBuilder();
        report.AppendLine("# SEED SWEEP — does aggregate grain ever reach EXACTLY 0 with pop > 0?");
        report.AppendLine("seed | turns | minTotalFood@turn | zeroTurns(pop>0) | settlementZeroEvents | endPop");

        for (ulong seed = 1; seed <= 40; seed++)
        {
            TurnExecutor exec = Executor(cfg);
            WorldState world = Sim.Cli.HeadlessFounding.Found(seed, null, null);
            long min = long.MaxValue, minTurn = -1, pop = 0;
            var zeroTurns = new List<long>();
            long settlementZeros = 0;

            for (int t = 0; t < 120; t++)
            {
                world = exec.Step(world);
                long food = FoodAudit.GrainStock(world, grain);
                pop = 0;
                for (int i = 0; i < world.Buckets.Count; i++) pop += world.Buckets[i].Count.Value;
                if (food < min) { min = food; minTurn = world.Clock.Turn; }
                if (food == 0 && pop > 0) zeroTurns.Add(world.Clock.Turn);
                for (int s = 0; s < world.Settlements.Count; s++)
                    if (FoodAudit.GrainStockOf(world, grain, world.Settlements[s].Id) == 0) settlementZeros++;
            }

            string zt = zeroTurns.Count == 0
                ? "none"
                : string.Join(",", zeroTurns.Take(10).Select(z => z.ToString(CultureInfo.InvariantCulture)));
            report.AppendLine(Inv($"{seed} | 120 | {min}@{minTurn} | {zt} | {settlementZeros} | {pop}"));
        }

        File.WriteAllText(Path.Combine(Path.GetTempPath(), "food-seed-sweep.md"), report.ToString());
    }

    private static string Inv(FormattableString s) => s.ToString(CultureInfo.InvariantCulture);
}
