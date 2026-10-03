using System.Globalization;
using System.Text;
using Sim.Core;
using Sim.Core.Kernel;
using Sim.Core.State;
using Sim.Core.Systems;
using Sim.Core.Worldgen;
using Sim.Tests.TestUtil;
using Xunit.Abstractions;

namespace Sim.Tests.Systems;

/// <summary>
/// M5 integration R2b — SCARCITY / CONFLICT REACHABILITY (Director decision 10). Not a gate and nothing is tuned
/// against it: a diagnostic that runs the canonical founded world (1024², N = 12) on several seeds and horizons
/// under plausible play — no orders, one AI rival, a player levy at 40 % and at 99 % (the capability forced by
/// a completed taxation node, never by bypassing the gate) — and counts what the M4 conflict loop needs:
/// colonies founded, control rows lost (revolt / uprising), settlement-turns with a positive consumption deficit
/// (starvation pressure), and APPROPRIATION-ARMED turns (a stateless settlement with a positive PREV deficit —
/// exactly AppropriationSystem's trigger; it owns no table, so the trigger is counted, not the raid).
/// The table is transcribed into docs/m5-integration-coherence-matrix.md §6.
/// </summary>
public class ScarcityConflictReachabilityMeasurement(ITestOutputHelper output)
{
    [Fact(Skip = "M5 R2b scarcity/conflict diagnostic (~15-25 min) — run manually; docs/m5-integration-coherence-matrix.md §6 records the table")]
    public void Reachability_AcrossSeedsHorizonsAndPlay()
    {
        var text = new StringBuilder();
        text.AppendLine("| seed | arm | turns | settlements end | colonies founded | control rows lost | stateless at end | deficit settlement-turns | appropriation-armed turns | first loss turn |");
        text.AppendLine("|---|---|---|---|---|---|---|---|---|---|");
        foreach (ulong seed in new ulong[] { 42, 7, 1234 })
            foreach ((string arm, int ai, double tax) in new[] { ("no orders", 0, 0.0), ("aiEmpires=1", 1, 0.0), ("tax 40 %", 0, 40.0), ("tax 99 % + aiEmpires=1", 1, 99.0) })
                text.AppendLine(Run(seed, arm, ai, tax, seed == 42 ? 500 : 300));
        output.WriteLine(text.ToString());
        File.WriteAllText(Path.Combine(Path.GetTempPath(), "r2b-scarcity.md"), text.ToString());
    }

    private static string Run(ulong seed, string arm, int ai, double tax, int turns)
    {
        SimConfig cfg = TestConfigs.Sim();
        WorldgenConfig wg = TestConfigs.Worldgen() with { AiEmpires = ai };
        WorldState w = WorldFounding.Found(wg, cfg, seed);
        PolityId player = default;
        for (int i = 0; i < w.Polities.Count; i++) if (w.Polities[i].Source == CommandSource.Player) player = w.Polities[i].Id;
        var orders = new OrderLog();
        if (tax > 0.0) { GovernanceRigs.Grant(w, player); orders.Append(Governance.TaxOrder(0, player, tax)); }
        TurnExecutor ex;
        using (var era = Sim.Data.DataFiles.OpenEraPacing())
        using (var pipe = Sim.Data.DataFiles.OpenPipeline())
            ex = new TurnExecutor(EraTableLoader.Load(era), PipelineLoader.Load(pipe, SystemCatalog.All(cfg, wg)), orders);

        int initial = w.Settlements.Count, lost = 0, firstLoss = -1;
        long deficitTurns = 0, armed = 0;
        for (int t = 1; t <= turns; t++)
        {
            WorldState prev = w;
            // AI rivals act through the ONE producer the session and CLI use (AiOrders.Append — tax valve, Age
            // advance, roads, construction); nothing else is scripted here.
            AiOrders.Append(orders, w, cfg);
            w = ex.Step(w);
            for (int i = 0; i < prev.Controls.Count; i++)
            {
                bool still = false;
                for (int j = 0; j < w.Controls.Count; j++)
                    if (w.Controls[j].Polity == prev.Controls[i].Polity && w.Controls[j].Place == prev.Controls[i].Place) { still = true; break; }
                if (!still) { lost++; if (firstLoss < 0) firstLoss = t; }
            }
            for (int i = 0; i < w.ConsumptionDeficits.Count; i++)
            {
                if (!(w.ConsumptionDeficits[i].DeficitRatio > 0.0)) continue;
                deficitTurns++;
                if (!EmpireQuery.TryGetController(w, w.ConsumptionDeficits[i].Settlement, out _)) armed++;
            }
        }
        int stateless = 0;
        for (int s = 0; s < w.Settlements.Count; s++) if (!EmpireQuery.TryGetController(w, w.Settlements[s].Id, out _)) stateless++;
        return string.Create(CultureInfo.InvariantCulture,
            $"| {seed} | {arm} | {turns} | {w.Settlements.Count} | {w.Settlements.Count - initial} | {lost} | {stateless} | {deficitTurns} | {armed} | {firstLoss} |");
    }
}
