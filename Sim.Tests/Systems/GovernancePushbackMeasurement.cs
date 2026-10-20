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
/// ADR-033 D4 — THE TAX'S PUSHBACK, MEASURED (the evidence behind the D-021 unrest-lite escalation:
/// "taxation without pushback isn't governing", and no branch implements unrest). On the founded
/// seed-42 world (canonical 1024², N = 12) the player is made able to tax by opening the WHOLE gate
/// (F1 2026-10-05: the R5 Taxation civic, a ResearchCompleted row at turn 0, AND entry into the edict's
/// Age, sim.json governance.taxationMinAge = A3 — <see cref="GovernanceRigs.Grant"/>; the rig forces the
/// capability through real state, it does not bypass the gate; the pre-R5 text named
/// arithmetic_babylonian, which no longer opens it), and the arms levy 0 / 40 / 70 / 99 % from turn 0 (the edict lands in the
/// state of turn 1) for 300 turns. Recorded per arm, for the capital and the whole empire: population,
/// happiness, legitimacy, effective rate, migration and output, plus control (revolts).
///
/// A measurement rig, not a gate (the FoundingVariationItem0 / HousingBeforeColumn precedent): it runs
/// three canonical 300-turn worlds, so it is skipped in the suite and run manually; its table is
/// transcribed into docs/m5-governing-loop-port.md. Nothing is tuned against it.
/// </summary>
public class GovernancePushbackMeasurement(ITestOutputHelper output)
{
    private const int Horizon = 300;
    private static readonly int[] Checkpoints = [20, 50, 100, 200, 300];

    private sealed class Arm
    {
        public required double Percent;
        public readonly Dictionary<int, string> Rows = []; // checkpoint → row (test-side reporting only)
        public long CapitalHarvest, EmpireHarvest, CapitalOutput, EmpireOutput, Migrants, CapitalIn, CapitalOut;
    }

    [Fact(Skip = "ADR-033 D4 / M5 R2b pushback measurement rig (~2-3 min: 4 canonical founded worlds x 300 turns) — run manually; docs/m5-integration-coherence-matrix.md §6 records the table")]
    public void TaxPushback_FoundedSeed42_Levies0_40_99()
    {
        SimConfig cfg = TestConfigs.Sim();
        WorldgenConfig wg = TestConfigs.Worldgen();
        var player = new PolityId(1);
        var arms = new[] { new Arm { Percent = 0.0 }, new Arm { Percent = 40.0 }, new Arm { Percent = 70.0 }, new Arm { Percent = 99.0 } };
        var text = new StringBuilder();

        foreach (Arm arm in arms)
        {
            WorldState w = WorldFounding.Found(wg, cfg, 42);
            GovernanceRigs.Grant(w, player);
            Assert.True(Governance.CanLevyTax(w, cfg, player));
            var orders = new OrderLog();
            if (arm.Percent > 0.0) orders.Append(Governance.TaxOrder(0, player, arm.Percent));
            TurnExecutor exec;
            using (var era = Sim.Data.DataFiles.OpenEraPacing())
            using (var pipe = Sim.Data.DataFiles.OpenPipeline())
                exec = new TurnExecutor(EraTableLoader.Load(era), PipelineLoader.Load(pipe, SystemCatalog.All(cfg, wg)), orders);
            SettlementId seat = GovernanceRigs.Seat(w, player);

            for (int t = 1; t <= Horizon; t++)
            {
                w = exec.Step(w);
                for (int i = 0; i < w.GoodStocks.Count; i++)
                {
                    GoodStockRow g = w.GoodStocks[i];
                    bool atSeat = g.Settlement == seat;
                    if (g.Good.Value == cfg.Goods!.GrainId)
                    {
                        arm.EmpireHarvest += g.LastProducedUnits;
                        if (atSeat) arm.CapitalHarvest += g.LastProducedUnits;
                    }
                    arm.EmpireOutput += g.LastProducedUnits;
                    if (atSeat) arm.CapitalOutput += g.LastProducedUnits;
                }
                for (int i = 0; i < w.MigrationFlows.Count; i++)
                {
                    arm.Migrants += w.MigrationFlows[i].Inflow;
                    if (w.MigrationFlows[i].Settlement == seat)
                    {
                        arm.CapitalIn += w.MigrationFlows[i].Inflow;
                        arm.CapitalOut += w.MigrationFlows[i].Outflow;
                    }
                }
                if (Array.IndexOf(Checkpoints, t) >= 0) arm.Rows[t] = Row(w, cfg, player, seat, arm);
            }
        }

        text.AppendLine("| arm | turn | capital pop | empire pop | settlements held / all | capital happiness | capital eff. rate | empire mean eff. rate | legitimacy | capital grain (cum.) | empire grain (cum.) | capital output (cum., all goods) | empire output (cum.) | migrants moved (cum.) | capital in / out (cum.) | capital grievance | max settlement grievance | capital protest | settlements protesting | capital tax grievance |");
        text.AppendLine("|---|---|---|---|---|---|---|---|---|---|---|---|---|---|---|---|---|---|---|---|");
        foreach (int cp in Checkpoints)
            foreach (Arm arm in arms)
                text.AppendLine(arm.Rows[cp]);
        output.WriteLine(text.ToString());
        File.WriteAllText(Path.Combine(Path.GetTempPath(), "governance-pushback.md"), text.ToString());
    }

    private static string Row(WorldState w, SimConfig cfg, PolityId player, SettlementId seat, Arm arm)
    {
        long capitalPop = GovernanceRigs.Population(w, seat), empirePop = 0;
        int held = 0;
        double rateSum = 0.0;
        for (int s = 0; s < w.Settlements.Count; s++)
        {
            SettlementId id = w.Settlements[s].Id;
            if (!EmpireQuery.ControlsSettlement(w, player, id)) continue;
            held++;
            empirePop += GovernanceRigs.Population(w, id);
            rateSum += Governance.EffectiveTaxRate(w, id, cfg);
        }
        double capG = MeanGrievance(w, seat), maxG = 0.0;
        int protesting = 0;
        for (int s = 0; s < w.Settlements.Count; s++)
        {
            maxG = Math.Max(maxG, MeanGrievance(w, w.Settlements[s].Id));
            if (Unrest.Protest(w, w.Settlements[s].Id, cfg) > 0.0) protesting++;
        }
        return string.Create(CultureInfo.InvariantCulture,
            $"| {arm.Percent:F0} % | {w.Clock.Turn} | {capitalPop} | {empirePop} | {held} / {w.Settlements.Count} | " +
            $"{SettlementHappiness.Of(w, seat, cfg):F2} | {Governance.EffectiveTaxRate(w, seat, cfg):F3} | " +
            $"{(held > 0 ? rateSum / held : 0.0):F3} | {Governance.Legitimacy(w, player, cfg):F2} | " +
            $"{arm.CapitalHarvest} | {arm.EmpireHarvest} | {arm.CapitalOutput} | {arm.EmpireOutput} | {arm.Migrants} | " +
            $"{arm.CapitalIn} / {arm.CapitalOut} | {capG:F2} | {maxG:F2} | {Unrest.Protest(w, seat, cfg):F3} | {protesting} | {Unrest.TaxGrievance(w, seat, cfg):F2} |");
    }

    /// <summary>Population-weighted mean grievance of one settlement (test-side reporting).</summary>
    private static double MeanGrievance(WorldState w, SettlementId s)
    {
        double acc = 0.0; long pop = 0;
        for (int g = 0; g < w.Grievances.Count; g++)
        {
            if (w.Grievances[g].Settlement != s) continue;
            long n = 0;
            for (int b = 0; b < w.Buckets.Count; b++)
                if (w.Buckets[b].Settlement == s && w.Buckets[b].Class == w.Grievances[g].Class) n += w.Buckets[b].Count.Value;
            acc += w.Grievances[g].Value * n; pop += n;
        }
        return pop > 0 ? acc / pop : 0.0;
    }
}
