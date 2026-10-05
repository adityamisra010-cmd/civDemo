using System.Globalization;
using System.Text;
using Sim.Core;
using Sim.Core.Kernel;
using Sim.Core.State;
using Sim.Core.Systems;
using Sim.Tests.TestUtil;
using Xunit.Abstractions;

namespace Sim.Tests.Systems;

/// <summary>
/// H2 (Director 2026-10-05 §4–§6, §17) — THE TAX-PRESSURE MEASUREMENT RIG behind docs/d049-taxation-and-revolt-model.md
/// §6. On the founded dev world (seed 42, 4 settlements, one player Empire holding all, the canonical era table — dt 10
/// years per turn at founding, the FASTEST accumulation per turn the game has) the player knows its crafts and the
/// Taxation civic and stands in A3; a levy is declared at turn 0 (in force from turn 1) and the CAPITAL (reach 1.0) is
/// read every turn. Four local conditions:
/// <list type="bullet">
/// <item>WELL-OFF: every stock the capital holds topped up to twice its population each turn, housed with room to
///   spare, and a granary and a workshop standing (public works: the service offset at full reach);</item>
/// <item>SERVED: as founded, plus a granary and a workshop standing (the service offset alone);</item>
/// <item>NATURAL: as founded, nothing touched;</item>
/// <item>POOR: housed for a quarter of its people each turn (no public works).</item>
/// </list>
/// A measurement rig, not a gate (the GovernancePushbackMeasurement precedent): skipped in the suite, run manually by
/// removing the Skip; its tables are transcribed into d049 §6. Nothing is tuned against it.
/// </summary>
public class TaxPressureMeasurement(ITestOutputHelper output)
{
    public enum Condition { WellOff, Served, Natural, Poor }

    /// <summary>One turn's reading of the capital.</summary>
    public readonly record struct Reading(
        int Turn, double MeanT, double MaxT, double Protest, double Risen, double Rebels, double Happiness, double Legitimacy,
        bool Controlled, double Felt, double Output, string Segments, long Population = 0, double FoodDeficit = 0.0);

    /// <summary>Runs the rig: levy <paramref name="percent"/> from turn 0, cut to <paramref name="cutTo"/> at
    /// <paramref name="cutAt"/> (−1 = never), for <paramref name="turns"/> turns. Returns every turn's reading.</summary>
    public static List<Reading> Run(Condition condition, double percent, int turns, int cutAt = -1, double cutTo = 0.0,
        SimConfig? config = null, bool canonical = false, double dtYears = 0.0, int settlements = 4, bool colonize = true)
    {
        SimConfig cfg = config ?? TestConfigs.Sim();
        (WorldState w, PolityId player) = canonical ? Canonical(cfg) : GovernanceRigs.Founded(settlements);
        TestConfigs.KnowRecipes(w, cfg);
        GovernanceRigs.Grant(w, player);
        SettlementId seat = GovernanceRigs.Seat(w, player);
        if (condition is Condition.WellOff or Condition.Served)
        {
            w.Structures.Add(new StructureRow(seat, 1, 1));   // granary
            w.Structures.Add(new StructureRow(seat, 2, 1));   // workshop
        }
        var orders = new OrderLog();
        if (percent > 0.0) orders.Append(Governance.TaxOrder(0, player, percent));
        if (cutAt >= 0) orders.Append(Governance.TaxOrder(cutAt, player, cutTo));
        TurnExecutor ex;
        // F1 (2026-10-05): colonize = false runs the catalog with no worldgen config, so ColonizationSystem founds
        // nothing and a one-settlement Empire's capital stays its FINAL settlement (D-048 ruling 5) for the whole run.
        Sim.Core.Worldgen.WorldgenConfig? worldgen = colonize ? TestConfigs.Worldgen() : null;
        if (dtYears > 0.0)
        {
            using var pipe = Sim.Data.DataFiles.OpenPipeline();
            ex = new TurnExecutor(ResearchRigs.FlatEra(dtYears), PipelineLoader.Load(pipe, SystemCatalog.All(cfg, worldgen)), orders);
        }
        else if (colonize) ex = UniversityRigs.Production(cfg, orders);
        else
        {
            using var era = Sim.Data.DataFiles.OpenEraPacing();
            using var pipe = Sim.Data.DataFiles.OpenPipeline();
            ex = new TurnExecutor(EraTableLoader.Load(era), PipelineLoader.Load(pipe, SystemCatalog.All(cfg, null)), orders);
        }
        var readings = new List<Reading>();
        for (int t = 1; t <= turns; t++)
        {
            long pop = GovernanceRigs.Population(w, seat);
            if (condition == Condition.WellOff)
            {
                for (int i = 0; i < w.GoodStocks.Count; i++)
                    if (w.GoodStocks[i].Settlement == seat && w.GoodStocks[i].Amount.Value < 2 * pop)
                        GovernanceRigs.AddStock(w, seat, w.GoodStocks[i].Good, 2 * pop - w.GoodStocks[i].Amount.Value);
                GovernanceRigs.SetDwellings(w, seat, pop + 100);
            }
            else if (condition == Condition.Poor)
                GovernanceRigs.SetDwellings(w, seat, Math.Max(1, pop / (4 * (long)cfg.Housing!.PersonsPerDwelling)));
            w = ex.Step(w);
            double maxT = 0.0;
            var seg = new StringBuilder();
            for (int g = 0; g < w.Grievances.Count; g++)
            {
                if (w.Grievances[g].Settlement != seat) continue;
                ClassId c = w.Grievances[g].Class;
                if (Unrest.Members(w, seat, c) <= 0) continue;
                double tc = Unrest.SegmentTaxGrievance(w, seat, c);
                maxT = Math.Max(maxT, tc);
                seg.Append(string.Create(CultureInfo.InvariantCulture,
                    $"c{c.Value}:{tc:F1}{(Unrest.IsSegmentRisen(w, seat, c, cfg) ? "R" + Unrest.SegmentRebelFraction(w, seat, c, cfg).ToString("F2", CultureInfo.InvariantCulture) : "")} "));
            }
            readings.Add(new Reading(t, Unrest.TaxGrievance(w, seat, cfg), maxT, Unrest.Protest(w, seat, cfg),
                Unrest.RisenShare(w, seat, cfg), Unrest.RebelShare(w, seat, cfg), SettlementHappiness.Of(w, seat, cfg),
                Governance.Legitimacy(w, player, cfg), EmpireQuery.ControlsSettlement(w, player, seat), 1.0 - DignityAt(w, seat),
                Governance.OutputMultiplier(w, seat, cfg), seg.ToString().TrimEnd(), GovernanceRigs.Population(w, seat), DeficitAt(w, seat)));
        }
        return readings;
    }

    private static (WorldState, PolityId) Canonical(SimConfig cfg)
    {
        WorldState w = Sim.Core.Worldgen.WorldFounding.Found(TestConfigs.Worldgen(), cfg, 42);
        for (int i = 0; i < w.Polities.Count; i++)
            if (w.Polities[i].Source == CommandSource.Player) return (w, w.Polities[i].Id);
        throw new InvalidOperationException("no player");
    }

    private static double DeficitAt(IReadOnlyWorldState w, SettlementId s)
    {
        for (int i = 0; i < w.ConsumptionDeficits.Count; i++)
            if (w.ConsumptionDeficits[i].Settlement == s) return w.ConsumptionDeficits[i].DeficitRatio;
        return 0.0;
    }

    private static double DignityAt(IReadOnlyWorldState w, SettlementId s)
    {
        for (int i = 0; i < w.NeedSatisfactions.Count; i++)
            if (w.NeedSatisfactions[i].Settlement == s && w.NeedSatisfactions[i].NeedId == 7) return w.NeedSatisfactions[i].Value;
        return 1.0;
    }

    private static readonly int[] Checkpoints = [1, 2, 3, 5, 10, 20, 40, 80];

    [Fact(Skip = "H2 tax-pressure measurement rig (~1-3 min: 30 founded dev worlds x 80 turns + recovery arms) — run manually; docs/d049-taxation-and-revolt-model.md §6 records the tables")]
    public void TaxPressure_ByCondition_AndRate_AndRecovery()
    {
        var text = new StringBuilder();
        foreach (double dt in new[] { 0.0, 5.0 })
        {
            text.AppendLine(dt > 0.0 ? "=== flat era, dt 5 years per turn (the Bronze/Iron band, where A3 taxation begins) ===" : "=== canonical era table, dt 10 years per turn at founding ===");
            foreach (Condition c in new[] { Condition.WellOff, Condition.Served, Condition.Natural, Condition.Poor })
                foreach (double rate in new[] { 0.0, 40.0, 70.0, 99.0, 100.0 })
                {
                    List<Reading> r = Run(c, rate, 80, dtYears: dt);
                    int firstProtest = r.FindIndex(x => x.Protest > 0.0);
                    int firstRisen = r.FindIndex(x => x.Risen > 0.0);
                    int revolt = r.FindIndex(x => !x.Controlled);
                    text.AppendLine(string.Create(CultureInfo.InvariantCulture,
                        $"{c} {rate}%: felt(t2) {r[1].Felt:F3}; first protest {Turn(firstProtest)}; first segment risen {Turn(firstRisen)}; settlement revolt {Turn(revolt)}; peak T {r.Max(x => x.MaxT):F1}; peak rebels {r.Max(x => x.Rebels):F3}; min output x{r.Min(x => x.Output):F3}"));
                    foreach (int cp in Checkpoints)
                    {
                        Reading x = r[cp - 1];
                        text.AppendLine(string.Create(CultureInfo.InvariantCulture,
                            $"  t{cp,-3} T {x.MeanT,6:F1} maxT {x.MaxT,6:F1} p {x.Protest:F3} risen {x.Risen:F3} rebels {x.Rebels:F3} out x{x.Output:F3} happy {x.Happiness,5:F1} legit {x.Legitimacy,5:F1} ctrl {(x.Controlled ? "Y" : "N")} [{x.Segments}]"));
                    }
                }
            foreach ((Condition c, double rate, int cut) in new[] { (Condition.WellOff, 99.0, 20), (Condition.Natural, 99.0, 10), (Condition.Natural, 100.0, 40), (Condition.Poor, 70.0, 10) })
            {
                List<Reading> r = Run(c, rate, 80, cutAt: cut, cutTo: 0.0, dtYears: dt);
                text.AppendLine(string.Create(CultureInfo.InvariantCulture, $"RECOVERY {c}: {rate}% turns 1-{cut}, cut to 0% at turn {cut}"));
                foreach (int cp in new[] { cut / 2, cut, cut + 1, cut + 2, cut + 5, cut + 10, cut + 20, cut + 40 })
                {
                    Reading x = r[cp - 1];
                    text.AppendLine(string.Create(CultureInfo.InvariantCulture,
                        $"  t{cp,-3} T {x.MeanT,6:F1} maxT {x.MaxT,6:F1} p {x.Protest:F3} rebels {x.Rebels:F3} out x{x.Output:F3} happy {x.Happiness,5:F1} legit {x.Legitimacy,5:F1} ctrl {(x.Controlled ? "Y" : "N")}"));
                }
            }
        }
        output.WriteLine(text.ToString());
        File.WriteAllText(Path.Combine(Path.GetTempPath(), "h2-tax-pressure.txt"), text.ToString());
    }

    /// <summary>F1 (2026-10-05, d049 §15): the rebels' subsistence measurement — population, famine and control over
    /// 300 turns at 0/40/70/99/100 % on the well-off, natural and poor capital (4 settlements) and on a FINAL settlement
    /// (one settlement, no colonization).</summary>
    [Fact(Skip = "F1 subsistence measurement rig (~5-10 min: 20 founded dev worlds x 300 turns) — run manually; d049 §15 records the table")]
    public void RebelSubsistence_300Turns()
    {
        var text = new StringBuilder();
        foreach ((string name, Condition c, int n, bool col) in new[]
                 { ("well-off", Condition.WellOff, 4, true), ("natural", Condition.Natural, 4, true), ("poor", Condition.Poor, 4, true), ("final", Condition.Natural, 1, false) })
            foreach (double rate in new[] { 0.0, 40.0, 70.0, 99.0, 100.0 })
            {
                List<Reading> r = Run(c, rate, 300, settlements: n, colonize: col);
                int lost = r.FindIndex(x => !x.Controlled);
                long peak = r.Max(x => x.Population);
                long minAfterPeak = r.Skip(r.FindIndex(x => x.Population == peak)).Min(x => x.Population);
                text.AppendLine(string.Create(CultureInfo.InvariantCulture,
                    $"{name} {rate}%: pop t1 {r[0].Population} peak {peak} min-after-peak {minAfterPeak} t300 {r[^1].Population}; max deficit {r.Max(x => x.FoodDeficit):F3}; peak rebels {r.Max(x => x.Rebels):F3}; min out x{r.Min(x => x.Output):F3}; lost {(lost < 0 ? "never" : (lost + 1).ToString(CultureInfo.InvariantCulture))}{(lost > 0 ? string.Create(CultureInfo.InvariantCulture, $" (pop {r[lost - 1].Population})") : "")}; extinct {(r.Any(x => x.Population == 0) ? "YES" : "no")}"));
            }
        output.WriteLine(text.ToString());
        File.WriteAllText(Path.Combine(Path.GetTempPath(), "f1-subsistence.txt"), text.ToString());
    }

    private static string Turn(int index) => index < 0 ? "none" : (index + 1).ToString(CultureInfo.InvariantCulture);
}

/// <summary>H2 calibration probe (manual): the natural capital's levy-grievance plateau on the canonical world and
/// the dev world at 40/70/99/100 %, for the threshold derivation in d049 §5.</summary>
public class TaxPressureCalibrationProbe(ITestOutputHelper output)
{
    [Fact(Skip = "H2 calibration probe (~1 min: 8 founded worlds x 60 turns) — run manually; d049 §5.1 records the plateaus the thresholds are derived from")]
    public void NaturalPlateaus()
    {
        var text = new StringBuilder();
        foreach (bool canonical in new[] { false, true })
            foreach (double rate in new[] { 40.0, 70.0, 99.0, 100.0 })
            {
                List<TaxPressureMeasurement.Reading> r = TaxPressureMeasurement.Run(TaxPressureMeasurement.Condition.Natural, rate, 60, canonical: canonical);
                text.AppendLine(string.Create(CultureInfo.InvariantCulture,
                    $"{(canonical ? "canonical" : "dev")} {rate}%: T t10 {r[9].MeanT:F2} t20 {r[19].MeanT:F2} t40 {r[39].MeanT:F2} t60 {r[59].MeanT:F2} maxT60 {r[59].MaxT:F2} felt {r[5].Felt:F3}"));
            }
        output.WriteLine(text.ToString());
        File.WriteAllText(Path.Combine(Path.GetTempPath(), "h2-probe.txt"), text.ToString());
    }
}
