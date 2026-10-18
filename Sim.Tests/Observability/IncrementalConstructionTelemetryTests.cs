using System.Text.Json;
using Sim.Core;
using Sim.Core.Kernel;
using Sim.Core.Observability;
using Sim.Core.State;
using Sim.Core.Systems;
using Sim.Core.Worldgen;
using Sim.Tests.TestUtil;

namespace Sim.Tests.Observability;

/// <summary>
/// D-054 WS1 — THE TELEMETRY AUDIT RUN: incremental construction sinks part of a project's materials every step it
/// progresses (ReasonIds.ConstructionMaterials, through EmpireStockpile.Draw), so the per-good audit identity — Opening +
/// Produced − InputsConsumed − ToolWear − Eaten − HousingMaterials − ConstructionMaterials == Closing (Observer → TurnRecord
/// → TelemetryWriter; TurnAuditModel renders the same flags) — must close EXACTLY on every turn of a 300-turn AI world with
/// every layer ON, including the turns where a head is part-built. Non-vacuity is pinned: part-built steps occur, and a
/// part-built step sinks construction materials.
///
/// MEASURED (Debug, this tree): the order-less AI world alone never part-builds a head in 300 turns — the AI orders only
/// when its purse covers the remaining inputs (the documented prudence) and a turn's builders finish every project it
/// orders in one step — so the audit would be vacuous for WS1 there. The PLAYER therefore orders a granary at each of its
/// settlements every third turn (through the order constructor the UI uses, only where the predicate the intake applies
/// holds): the queue outruns the empire's stone and the heads are built a little at a time, oldest order first.
/// </summary>
public class IncrementalConstructionTelemetryTests
{
    [Fact]
    public void TheAiWorld_WithIncrementalConstruction_EveryGoodReconcilesOnEveryTurn_InTheRecordAndInTheTelemetry()
    {
        SimConfig cfg = TestConfigs.Sim();
        Assert.True(ConstructionQuery.Incremental(cfg));
        WorldgenConfig wg = TestConfigs.Worldgen() with { AiEmpires = 1 };
        var log = new OrderLog();
        TurnExecutor exec = ObservedWorlds.Executor(cfg, log);
        WorldState w = WorldFounding.Found(wg, cfg, 42);
        var observations = new ObservationLog();
        int partBuiltSteps = 0;
        long partBuiltSunk = 0;
        var player = new PolityId(1);
        int granary = cfg.Goods!.ProjectById(1) is { Name: "granary" } ? 1 : throw new InvalidOperationException("goods.json project 1 is the granary");
        for (int t = 1; t <= 300; t++)
        {
            if (t % 3 == 1)
                foreach (SettlementId s in LabourActivities.ControlledSettlements(w, player))
                    if (ConstructionQuery.IsProjectAvailable(w, cfg, player, s, granary))
                        log.Append(ConstructionQuery.EnqueueOrder(w, player, s, granary));
            AiOrders.Append(log, w, cfg, TestConfigs.Worldgen());
            WorldState prev = w;
            w = exec.Step(prev);
            TurnRecord turn = observations.Observe(prev, w, cfg, OrderApplied.For(log, prev.Clock.Turn)).Turn;

            for (int s = 0; s < turn.Stocks.Length; s++)
                Assert.True(turn.Stocks[s].Reconciles && turn.Stocks[s].Discrepancy == 0,
                    $"turn {turn.Turn}: {turn.Stocks[s].Name} discrepancy {turn.Stocks[s].Discrepancy}");
            long constructionSunk = 0;
            for (int g = 0; g < turn.Goods.Length; g++)
            {
                GoodAccount a = turn.Goods[g];
                Assert.True(a.Reconciles && a.Discrepancy == 0, $"turn {turn.Turn}: {a.Name} discrepancy {a.Discrepancy}");
                Assert.Equal(a.Closing, a.Opening + a.Produced - a.InputsConsumed - a.ToolWear - a.Eaten - a.HousingMaterials - a.ConstructionMaterials);
                constructionSunk += a.ConstructionMaterials;
            }

            // A PART-BUILT STEP: some head advanced this step and is still short of N (the state whole-or-nothing never had).
            bool partBuilt = false;
            for (int i = 0; i < w.ConstructionProgress.Count; i++)
            {
                ConstructionProgressRow row = w.ConstructionProgress[i];
                long before = ConstructionQuery.ProgressOf(prev.ConstructionProgress,
                    new ConstructionQueueRow(row.Settlement, row.Slot, row.ProjectId));
                if (row.Progress > before && row.Progress < ConstructionQuery.ProgressUnits) partBuilt = true;
            }
            if (partBuilt)
            {
                partBuiltSteps++;
                partBuiltSunk += constructionSunk;
                Assert.True(constructionSunk > 0, $"turn {turn.Turn}: a head advanced but no construction material was sunk");
            }
        }
        Assert.True(partBuiltSteps > 0, "no head was ever part-built in 300 turns - the audit run is vacuous for WS1");
        Assert.True(partBuiltSunk > 0);

        // The same identity through the telemetry writer: every "discrepancy" the JSONL carries is 0.
        using var jsonl = new MemoryStream();
        TelemetryWriter.WriteAll(jsonl, observations);
        string[] lines = System.Text.Encoding.UTF8.GetString(jsonl.ToArray()).Split('\n', StringSplitOptions.RemoveEmptyEntries);
        Assert.Equal(300, lines.Length);
        long discrepancies = 0;
        foreach (string line in lines)
        {
            using JsonDocument doc = JsonDocument.Parse(line);
            discrepancies += CountDiscrepancies(doc.RootElement);
        }
        Assert.True(discrepancies >= 300 * 17L, $"only {discrepancies} discrepancy fields were written");
    }

    /// <summary>Counts every "discrepancy" property under <paramref name="e"/>, asserting each is exactly 0.</summary>
    private static long CountDiscrepancies(JsonElement e)
    {
        long n = 0;
        switch (e.ValueKind)
        {
            case JsonValueKind.Object:
                foreach (JsonProperty p in e.EnumerateObject())
                {
                    if (p.NameEquals("discrepancy"))
                    {
                        Assert.Equal(0L, p.Value.GetInt64());
                        n++;
                    }
                    else n += CountDiscrepancies(p.Value);
                }
                break;
            case JsonValueKind.Array:
                foreach (JsonElement item in e.EnumerateArray()) n += CountDiscrepancies(item);
                break;
        }
        return n;
    }
}
