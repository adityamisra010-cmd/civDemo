using Sim.Core;
using Sim.Core.Kernel;
using Sim.Core.Observability;
using Sim.Core.State;
using Sim.Core.Systems;
using Sim.Core.Worldgen;
using Sim.Ui.ViewModel;
using Xunit;

namespace Sim.Ui.Tests;

/// <summary>
/// D-054 WS1 (batch-3 integration, 2026-10-14; verifier 2 F10(a)) — THE TURN AUDIT READS CLEAN ON THE TELEMETRY AUDIT
/// RUN. Sim.Tests' IncrementalConstructionTelemetryTests closes every good's account on every turn of a 300-turn,
/// one-AI-Empire world founded from the shipped configs (seed 42), with the player ordering a granary at each of its
/// settlements every third turn so heads are part-built — through Observer, TurnRecord and TelemetryWriter. The spec
/// names TurnAuditModel too, and Sim.Tests cannot reference Sim.Ui; the C1 fix pinned it on one part-built turn of a
/// 256-px session. This is THE SAME RUN (the same configs, worldgen, executor recipe and order loop, so the same turns
/// by determinism) rendered through TurnAuditModel: every WHAT CHANGED line, every WHY line and every ECONOMY row says
/// the account reconciles — no "DISCREPANCY" on any turn — including the turns where a head is part-built.
/// </summary>
public class IncrementalConstructionTurnAuditRunTests
{
    [Fact]
    public void TheTelemetryAuditRun_RenderedThroughTheTurnAudit_ShowsNoDiscrepancyOnAnyTurn_IncludingPartBuiltTurns()
    {
        SimConfig cfg = UiFounding.ProductionConfig();
        Assert.True(ConstructionQuery.Incremental(cfg));
        WorldgenConfig shipped = UiSession.ProductionWorldgen();
        WorldgenConfig wg = shipped with { AiEmpires = 1 };
        var log = new OrderLog();
        TurnExecutor exec;
        using (var eraStream = Sim.Data.DataFiles.OpenEraPacing())
        using (var pipeStream = Sim.Data.DataFiles.OpenPipeline())
            exec = new TurnExecutor(EraTableLoader.Load(eraStream), PipelineLoader.Load(pipeStream, SystemCatalog.All(cfg, shipped)), log);
        WorldState w = WorldFounding.Found(wg, cfg, 42);
        var observations = new ObservationLog();
        var player = new PolityId(1);
        int granary = cfg.Goods!.ProjectById(1) is { Name: "granary" } ? 1 : throw new InvalidOperationException("goods.json project 1 is the granary");
        int partBuiltTurns = 0, linesRead = 0;
        for (int t = 1; t <= 300; t++)
        {
            if (t % 3 == 1)
                foreach (SettlementId s in LabourActivities.ControlledSettlements(w, player))
                    if (ConstructionQuery.IsProjectAvailable(w, cfg, player, s, granary))
                        log.Append(ConstructionQuery.EnqueueOrder(w, player, s, granary));
            AiOrders.Append(log, w, cfg, shipped);
            WorldState prev = w;
            w = exec.Step(prev);
            TurnObservation o = observations.Observe(prev, w, cfg, OrderApplied.For(log, prev.Clock.Turn));

            var lines = new List<string>();
            foreach (AuditLine l in TurnAuditModel.ChangedLines(o.Turn, o.Settlements, id => "S" + id))
            {
                lines.Add(l.Line);
                lines.AddRange(l.Detail);
            }
            lines.AddRange(TurnAuditModel.WhyLines(o.Turn));
            IReadOnlyList<string> economy = TurnAuditModel.GoodAccountLines(o.Turn);
            Assert.Equal(o.Turn.Goods.Length + 1, economy.Count);
            for (int i = 1; i < economy.Count; i++)
                Assert.True(economy[i].EndsWith("  ok", StringComparison.Ordinal), $"turn {o.Turn.Turn}: {economy[i]}");
            lines.AddRange(economy);
            foreach (string line in lines)
                Assert.False(line.Contains("DISCREPANCY", StringComparison.Ordinal), $"turn {o.Turn.Turn}: {line}");
            linesRead += lines.Count;

            // A part-built step (a head advanced and is still short of N): its construction column carries the draws.
            bool partBuilt = false;
            for (int i = 0; i < w.ConstructionProgress.Count; i++)
            {
                ConstructionProgressRow row = w.ConstructionProgress[i];
                long before = ConstructionQuery.ProgressOf(prev.ConstructionProgress,
                    new ConstructionQueueRow(row.Settlement, row.Slot, row.ProjectId));
                if (row.Progress > before && row.Progress < ConstructionQuery.ProgressUnits) partBuilt = true;
            }
            if (!partBuilt) continue;
            partBuiltTurns++;
            long constr = 0;
            foreach (GoodAccount a in o.Turn.Goods) constr += a.ConstructionMaterials;
            Assert.True(constr > 0, $"turn {o.Turn.Turn}: a head advanced but the audit's construction column is empty");
        }
        Assert.True(partBuiltTurns > 0, "no head was ever part-built in 300 turns - the audit run is vacuous for WS1");
        Assert.True(linesRead > 300 * 20, $"only {linesRead} audit lines were read");
    }
}
