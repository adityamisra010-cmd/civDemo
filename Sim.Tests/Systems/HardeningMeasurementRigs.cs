using System.Diagnostics;
using System.Globalization;
using System.Text;
using Sim.Core;
using Sim.Core.Kernel;
using Sim.Core.State;
using Sim.Core.Systems;
using Sim.Core.Systems.Migration;
using Sim.Core.Worldgen;
using Sim.Tests.TestUtil;
using Xunit.Abstractions;

namespace Sim.Tests.Systems;

/// <summary>
/// A fact that runs only when <c>SIM_MEASURE=1</c> is set in the environment, and is SKIPPED otherwise — the
/// repository's manual measurement-rig pattern (`[Fact(Skip = …)]`) made re-runnable from a command line
/// without editing the source. A measurement rig is not a gate: nothing is tuned against it.
/// </summary>
public sealed class MeasurementFactAttribute : FactAttribute
{
    public const string Variable = "SIM_MEASURE";

    public MeasurementFactAttribute(string what)
    {
        if (Environment.GetEnvironmentVariable(Variable) != "1")
            Skip = $"measurement rig ({what}) — run with {Variable}=1; docs/m5-hardening-measurements.md gives the commands";
    }
}

/// <summary>
/// M5 HARDENING STREAM H4 (Director 2026-10-05 §11, §14) — COLONY CALIBRATION AND LEDGER STRESS. A diagnostic
/// rig, not a gate. Each run founds a world, steps it through the production pipeline with the shipped content
/// (forager layer ON at 4.3 / 2.0) and records, per seed:
/// <list type="bullet">
/// <item>LEDGER: any exception thrown by a step (type, message, turn — the run stops there), the number of
/// turns on which any stock was negative, every violation of the consumption-remainder invariants the
/// substitution-credit fix guarantees (only the staple carries a credit, credit &gt; −3, every remainder &lt; 1),
/// and the exact conservation audit (`ConservationAuditor`, every good by name) at the snapshot turn and at the end.</item>
/// <item>COLONIES (D-037 B1 / T4.4): foundings, the turns they happen on, the most in one turn, the settlement-turns
/// with migration's UNPLACED departure demand (computed with migration's own planner, `MigrationSystem.Plan`, on
/// the PREV world — the exact readout ColonizationSystem consumes), sources with demand that were emptied, colony
/// survival (alive at the end, died out, death turn), colony size at founding and at the end, and the colony's
/// controller against its founders' controllers (control inheritance).</item>
/// </list>
/// Colonization is never scripted. Every stressed arm creates a CAUSAL condition and lets the pipeline decide:
/// <c>allgather</c> — the player orders 100 % of labour to gathering at turn 0 (an ordinary SectorAllocation
/// order), so the settlements grow to the forager land ceiling and meet it Malthusian (the all-food rig of
/// R4a §1); <c>lone</c> — a one-settlement world, where being alone in the world IS B1's "no viable destination";
/// <c>famine</c> — at turn 100 the player orders gathering down to 10 % in every settlement it holds (a policy
/// famine, the FoundedHarness 30 %-farm precedent made world-wide); <c>disaster</c> — COUNTERFACTUAL, not shipped:
/// the famine-class disaster armed at CR-016's derived λ = 0.01 (shipped 0.0, rate OPEN for the Director);
/// <c>preforager</c> — ATTRIBUTION CONTROL, not shipped: the forager layer switched OFF (`TestConfigs.PreForager`,
/// the R3 world), no orders, so a reading can be attributed to the forager calibration or not.
///
/// Parameters come from the environment so several arms run as separate processes (the machine is shared):
/// H4_WORLD (canonical | dev), H4_ARM (none | allgather | lone | famine | disaster | preforager), H4_AI (AI empires, default 0),
/// H4_FOUNDERS (per settlement, default 400 = shipped; others re-apportioned by largest remainder over the shipped
/// stable vector, foodStore 15 per founder — the T4.19 / R3 derivation), H4_SEEDS ("a-b"), H4_TURNS, H4_SNAPSHOT
/// (the turn whose colony/ledger columns are also reported, default 650), H4_OUT (TSV path; colony detail goes to
/// H4_OUT + ".colonies.tsv"; exception stacks to H4_OUT + ".errors.txt").
/// Record: docs/m5-hardening-measurements.md.
/// </summary>
public class HardeningMeasurementRigs(ITestOutputHelper output)
{
    private static string Env(string name, string fallback) =>
        Environment.GetEnvironmentVariable(name) is { Length: > 0 } v ? v : fallback;

    private static int EnvInt(string name, int fallback) =>
        int.Parse(Env(name, fallback.ToString(CultureInfo.InvariantCulture)), CultureInfo.InvariantCulture);

    public const string Header =
        "seed\tworld\tarm\tai\tfounders\tturns\tstatus\texcTurn\texception\tconservedSnap\tconservedEnd\tnegStockTurns\tremainderViolations\tcreditTurns" +
        "\tsettlementsStart\tfoundingsSnap\tfoundingsEnd\tfirstFound\tlastFound\tmaxFoundPerTurn\tfoundingTurns\tunplacedSettlementTurns" +
        "\tdeficitSettlementTurns\tsourcesEmptied\tcoloniesAliveEnd\tcoloniesDiedOut\tcolonyPopEnd\tcolonyMaxPopEnd\tcontrolMismatches" +
        "\tstarvationSnap\tstarvationEnd\tpopSnap\tpopEnd\tmaxSettlements\tsecondsWall\tpolitiesStart\tpolitiesEnd\tcontrolLosses\tfirstLossTurn\taiOrders";

    [MeasurementFact("H4 colonies + ledger stress, parameters from H4_* environment variables")]
    public void ColoniesAndLedger()
    {
        string world = Env("H4_WORLD", "canonical");
        string arm = Env("H4_ARM", "none");
        int ai = EnvInt("H4_AI", 0);
        int founders = EnvInt("H4_FOUNDERS", 400);
        int turns = EnvInt("H4_TURNS", 650);
        int snapshot = EnvInt("H4_SNAPSHOT", 650);
        string[] range = Env("H4_SEEDS", "1-20").Split('-');
        int seedLo = int.Parse(range[0], CultureInfo.InvariantCulture);
        int seedHi = range.Length > 1 ? int.Parse(range[1], CultureInfo.InvariantCulture) : seedLo;
        string outPath = Env("H4_OUT", Path.Combine(Path.GetTempPath(), $"h4-{world}-{arm}-ai{ai}-n{founders}.tsv"));

        if (!File.Exists(outPath)) File.WriteAllText(outPath, Header + "\n");
        string colonyPath = outPath + ".colonies.tsv";
        if (!File.Exists(colonyPath))
            File.WriteAllText(colonyPath,
                "seed\tworld\tarm\tai\tfounders\tcolonyId\tfoundedTurn\tsourcesWithDemand\tdemandSourceIds\tcontroller\tsourceControllers\tpopAtFounding\tmaxPop\tpopEnd\tdeathTurn\tgrainAtFounding\n");

        // H4_METRICS_OUT: also write the `sim autoplay` metrics shape (autoplay-metrics/v1) observed over the
        // first H4_SNAPSHOT turns, so an arm's corridors can be scored by NightlyCorridors exactly like the nightly.
        string metricsOut = Env("H4_METRICS_OUT", "");
        List<AutoplayMetrics>? metrics = metricsOut.Length > 0 ? [] : null;
        for (int seed = seedLo; seed <= seedHi; seed++)
        {
            string line = RunOne((ulong)seed, world, arm, ai, founders, turns, snapshot, colonyPath, outPath + ".errors.txt", metrics);
            File.AppendAllText(outPath, line + "\n");
            output.WriteLine(line);
        }
        if (metrics is not null)
        {
            var doc = new
            {
                schema = "autoplay-metrics/v1",
                turns = snapshot,
                seeds = metrics.Select(m => new
                {
                    seed = m.Seed,
                    worldHash = m.WorldHash,
                    finalPopulation = m.FinalPopulation,
                    finalYear = m.FinalYear,
                    settlementCount = m.SettlementCount,
                    arableKm2 = m.ArableKm2,
                    finalCohortTotals = m.FinalCohortTotals,
                    series = new
                    {
                        year = m.Year, dtYears = m.DtYears, population = m.Population, births = m.Births,
                        deaths = m.Deaths, starvationDeaths = m.StarvationDeaths, migrationGross = m.MigrationGross,
                    },
                }).ToArray(),
            };
            File.WriteAllText(metricsOut, System.Text.Json.JsonSerializer.Serialize(doc) + "\n");
        }
    }

    /// <summary>
    /// The nightly sweep's metrics file (`sim autoplay --seeds 20 --turns 650 --metrics F`, ci.yml
    /// calibration-nightly) scored against EVERY canonical corridor in corridors.json, per seed, with the
    /// battery's own definitions (`CalibrationAnalysis`, `Corridors.WindowYears`) — `sim corridors` reads only
    /// the two quarantined keys, so the fed corridors the CI battery checks on seeds 1–2 are scored here on all
    /// twenty. Also the era-boundary continuity pin (|r(1600–2500) − r(2500–3400)| ≤ 0.0001/yr). H4_METRICS is
    /// the metrics path, H4_OUT the TSV written.
    /// </summary>
    [MeasurementFact("H4 nightly corridor scoring, parameters H4_METRICS / H4_OUT")]
    public void NightlyCorridors()
    {
        string metricsPath = Env("H4_METRICS", "nightly-metrics.json");
        string outPath = Env("H4_OUT", Path.Combine(Path.GetTempPath(), "h4-nightly-corridors.tsv"));
        Corridors c = Corridors.Load();
        using var doc = System.Text.Json.JsonDocument.Parse(File.ReadAllText(metricsPath));
        string[] keys =
        [
            "fedGrowthPerYear", "crudeBirthRatePer1000", "crudeDeathRatePer1000", "pyramidChildShare",
            "pyramidAdultShare", "pyramidElderShare", "densityPerArableKm2", "migrationGrossPerDecade",
        ];
        var text = new StringBuilder("seed\t" + string.Join('\t', keys) + "\teraContinuityDelta\tfinalPopulation\tstarvationDeaths\tcrashes\n");
        var values = new List<double[]>();
        foreach (System.Text.Json.JsonElement s in doc.RootElement.GetProperty("seeds").EnumerateArray())
        {
            var m = new AutoplayMetrics
            {
                Seed = s.GetProperty("seed").GetUInt64(),
                FinalPopulation = s.GetProperty("finalPopulation").GetInt64(),
                ArableKm2 = s.GetProperty("arableKm2").GetDouble(),
            };
            int ci = 0;
            foreach (System.Text.Json.JsonElement v in s.GetProperty("finalCohortTotals").EnumerateArray())
                m.FinalCohortTotals[ci++] = v.GetInt64();
            System.Text.Json.JsonElement series = s.GetProperty("series");
            foreach (System.Text.Json.JsonElement v in series.GetProperty("year").EnumerateArray()) m.Year.Add(v.GetDouble());
            foreach (System.Text.Json.JsonElement v in series.GetProperty("dtYears").EnumerateArray()) m.DtYears.Add(v.GetDouble());
            foreach (System.Text.Json.JsonElement v in series.GetProperty("population").EnumerateArray()) m.Population.Add(v.GetInt64());
            foreach (System.Text.Json.JsonElement v in series.GetProperty("births").EnumerateArray()) m.Births.Add(v.GetInt64());
            foreach (System.Text.Json.JsonElement v in series.GetProperty("deaths").EnumerateArray()) m.Deaths.Add(v.GetInt64());
            foreach (System.Text.Json.JsonElement v in series.GetProperty("starvationDeaths").EnumerateArray()) m.StarvationDeaths.Add(v.GetInt64());
            foreach (System.Text.Json.JsonElement v in series.GetProperty("migrationGross").EnumerateArray()) m.MigrationGross.Add(v.GetInt64());

            (double gFrom, double gTo) = Corridors.WindowYears("canonical", "fedGrowthPerYear");
            (double bFrom, double bTo) = Corridors.WindowYears("canonical", "crudeBirthRatePer1000");
            (double dFrom, double dTo) = Corridors.WindowYears("canonical", "crudeDeathRatePer1000");
            (double child, double adult, double elder) = CalibrationAnalysis.PyramidShares(m);
            double[] row =
            [
                CalibrationAnalysis.WindowGrowthPerYear(m, gFrom, gTo),
                CalibrationAnalysis.CrudeRatePerPersonYear(m, m.Births, bFrom, bTo) * 1000.0,
                CalibrationAnalysis.CrudeRatePerPersonYear(m, m.Deaths, dFrom, dTo) * 1000.0,
                child, adult, elder,
                CalibrationAnalysis.DensityPerArableKm2(m),
                CalibrationAnalysis.MigrationGrossPerDecade(m),
            ];
            values.Add(row);
            double continuity = Math.Abs(CalibrationAnalysis.WindowGrowthPerYear(m, 1600.0, 2500.0)
                - CalibrationAnalysis.WindowGrowthPerYear(m, 2500.0, 3400.0));
            long starved = 0;
            foreach (long x in m.StarvationDeaths) starved += x;
            text.Append(m.Seed.ToString(CultureInfo.InvariantCulture));
            foreach (double v in row) text.Append('\t').Append(v.ToString("R", CultureInfo.InvariantCulture));
            text.Append(string.Create(CultureInfo.InvariantCulture,
                $"\t{continuity:R}\t{m.FinalPopulation}\t{starved}\t{CalibrationAnalysis.Crashes(m, 0.20).Count}\n"));
        }
        text.Append("# key\tband\tquarantineWindow\tmin\tmax\tinBand\tinWindow\n");
        for (int k = 0; k < keys.Length; k++)
        {
            (double lo, double hi) = c.Band("canonical." + keys[k]);
            (double Lo, double Hi)? q = Corridors.Quarantine("canonical", keys[k]);
            double min = double.PositiveInfinity, max = double.NegativeInfinity;
            int inBand = 0, inWindow = 0;
            foreach (double[] row in values)
            {
                double v = row[k];
                min = Math.Min(min, v); max = Math.Max(max, v);
                if (v >= lo && v <= hi) inBand++;
                if (q is { } w && v >= w.Lo && v <= w.Hi) inWindow++;
            }
            text.Append(string.Create(CultureInfo.InvariantCulture,
                $"# {keys[k]}\t[{lo:R}, {hi:R}]\t{(q is { } qw ? $"[{qw.Lo:R}, {qw.Hi:R}]" : "-")}\t{min:R}\t{max:R}\t{inBand}/{values.Count}\t{(q is null ? "-" : $"{inWindow}/{values.Count}")}\n"));
        }
        File.WriteAllText(outPath, text.ToString());
        output.WriteLine(text.ToString());
    }

    /// <summary>The shipped 400-founder stable vector re-apportioned to <paramref name="n"/> by largest remainder,
    /// ties to the lower cohort index — reproduces the R3 / SubstitutionCredit 100-founder vector exactly.</summary>
    public static long[] Apportion(long[] shipped, int n)
    {
        long total = 0;
        foreach (long c in shipped) total += c;
        var counts = new long[shipped.Length];
        var rem = new double[shipped.Length];
        long assigned = 0;
        for (int i = 0; i < shipped.Length; i++)
        {
            double exact = shipped[i] * (double)n / total;
            counts[i] = (long)Math.Floor(exact);
            rem[i] = exact - counts[i];
            assigned += counts[i];
        }
        for (long k = assigned; k < n; k++)
        {
            int best = -1;
            for (int i = 0; i < rem.Length; i++)
                if (best < 0 || rem[i] > rem[best]) best = i;   // strict >: ties keep the lower index
            counts[best]++;
            rem[best] = -1.0;
        }
        return counts;
    }

    private static string RunOne(ulong seed, string worldName, string arm, int ai, int founders, int turns, int snapshot,
        string colonyPath, string errorPath, List<AutoplayMetrics>? metrics)
    {
        AutoplayCollector? collector = metrics is not null ? new AutoplayCollector(seed) : null;
        var clock = Stopwatch.StartNew();
        SimConfig cfg = TestConfigs.Sim();
        if (founders != 400)
        {
            long[] shipped = cfg.Founding.CohortCounts;
            long sum = 0;
            foreach (long c in shipped) sum += c;
            if (sum != 400) throw new InvalidOperationException($"shipped founding vector sums to {sum}, not 400");
            cfg = cfg with { Founding = cfg.Founding with { CohortCounts = Apportion(shipped, founders), FoodStore = 15L * founders } };
        }
        if (arm == "disaster")
            cfg = cfg with { Disaster = cfg.Disaster with { HazardPerYear = 0.01 } };
        if (arm == "preforager")
            cfg = TestConfigs.PreForager(cfg);   // ATTRIBUTION CONTROL: the R3 world (forager layer OFF), no orders
        // COUNTERFACTUAL forager rates (H4_GATHER per gatherer, H4_KM2 per fertility-weighted km²) — a measurement
        // of the Director's accepted lever (§10), never the shipped content. Unset = shipped sim.json values.
        if (Environment.GetEnvironmentVariable("H4_GATHER") is { Length: > 0 } gatherText && cfg.Farming.PreCultivation is { } preG)
            cfg = cfg with { Farming = cfg.Farming with { PreCultivation = preG with { OutputPerGathererPerYear = double.Parse(gatherText, CultureInfo.InvariantCulture) } } };
        if (Environment.GetEnvironmentVariable("H4_KM2") is { Length: > 0 } km2Text && cfg.Farming.PreCultivation is { } preK)
            cfg = cfg with { Farming = cfg.Farming with { PreCultivation = preK with { YieldPerArableKm2PerYear = double.Parse(km2Text, CultureInfo.InvariantCulture) } } };

        WorldgenConfig wg = (worldName == "dev" ? TestConfigs.DevWorldgen() : TestConfigs.Worldgen()) with { AiEmpires = ai };
        int? settlementsOverride = arm == "lone" ? 1 : null;
        WorldState w = WorldFounding.Found(wg, cfg, seed, settlementsOverride);

        var orders = new OrderLog();
        var player = new PolityId(1);
        if (arm == "allgather") AppendGatherShare(orders, w, player, 0, 100.0);
        if (arm == "famine") AppendGatherShare(orders, w, player, 100, 10.0);
        if (arm is "tax99" or "tax100")
        {
            // The capability is forced by a COMPLETED taxation node (constructed knowledge, the R2b rig's method),
            // never by bypassing the gate; the levy is an ordinary player order at turn 0. Its purpose here is the
            // REVOLT path: every revolt founds a capital-less AI polity that the AI producer then drives.
            GovernanceRigs.Grant(w, player);
            orders.Append(Governance.TaxOrder(0, player, arm == "tax99" ? 99.0 : 100.0));
        }

        TurnExecutor ex;
        using (var era = Sim.Data.DataFiles.OpenEraPacing())
        using (var pipe = Sim.Data.DataFiles.OpenPipeline())
            ex = new TurnExecutor(EraTableLoader.Load(era), PipelineLoader.Load(pipe, SystemCatalog.All(cfg, wg)), orders);
        bool hasAi = AiOrders.HasAiPolity(w);
        int grain = cfg.Goods!.GrainId;

        bool trace = Environment.GetEnvironmentVariable("H4_TRACE") == "1";
        string tracePath = errorPath.Replace(".errors.txt", ".starvation.tsv", StringComparison.Ordinal);
        int initial = w.Settlements.Count, maxSettlements = initial;
        int politiesStart = w.Polities.Count, controlLosses = 0, firstLossTurn = -1;
        long aiOrders = 0;
        string status = "ok", exception = "", excTurn = "";
        long negStockTurns = 0, remainderViolations = 0, creditTurns = 0;
        long unplacedTurns = 0, deficitTurns = 0, sourcesEmptied = 0;
        int foundingsSnap = -1, firstFound = -1, lastFound = -1, maxPerTurn = 0, foundingTurnCount = 0;
        string conservedSnap = "n/a", conservedEnd = "n/a";
        long starvSnap = -1, popSnap = -1;
        var colonies = new List<ColonyTrace>();

        int t = 0;
        try
        {
            for (t = 1; t <= turns; t++)
            {
                WorldState prev = w;
                // The CLI session loop's rule: run the producer whenever an AI polity exists (a revolt founds one).
                if (hasAi || AiOrders.HasAiPolity(prev)) aiOrders += AiOrders.Append(orders, prev, cfg);

                // Migration's own B1 readout on PREV (the planner Step itself consumes): which sources have
                // departure demand with no reachable, viable destination this turn. Only computed when some
                // settlement is in deficit — the readout is zero without one (MigrationSystem: deficit gate).
                var demandSources = new List<int>();
                bool anyDeficit = false;
                for (int i = 0; i < prev.ConsumptionDeficits.Count; i++)
                    if (prev.ConsumptionDeficits[i].DeficitRatio > 0.0) { anyDeficit = true; deficitTurns++; }
                if (anyDeficit)
                {
                    MigrationPlan plan = MigrationSystem.Plan(prev, cfg, prev.Clock.DtYears > 0.0 ? prev.Clock.DtYears : 10.0);
                    for (int s = 0; s < prev.Settlements.Count; s++)
                    {
                        SettlementId id = prev.Settlements[s].Id;
                        double demand = 0.0;
                        for (int b = 0; b < prev.Buckets.Count && b < plan.Unplaced.Length; b++)
                            if (prev.Buckets[b].Settlement == id) demand += plan.Unplaced[b];
                        if (demand > 0.0) demandSources.Add(s);
                    }
                    unplacedTurns += demandSources.Count;
                }

                w = ex.Step(prev);
                for (int i = 0; i < prev.Controls.Count; i++)
                {
                    bool still = false;
                    for (int j = 0; j < w.Controls.Count; j++)
                        if (w.Controls[j].Polity == prev.Controls[i].Polity && w.Controls[j].Place == prev.Controls[i].Place) { still = true; break; }
                    if (!still) { controlLosses++; if (firstLossTurn < 0) firstLossTurn = t; }
                }
                if (collector is not null && t <= snapshot) collector.Observe(w);
                if (collector is not null && t == snapshot) metrics!.Add(collector.Finish(w));

                // --- ledger invariants -------------------------------------------------------------------
                bool neg = false;
                for (int i = 0; i < w.GoodStocks.Count; i++)
                {
                    GoodStockRow row = w.GoodStocks[i];
                    if (row.Amount.Value < 0) neg = true;
                    if (row.ConsumeRemainder < 0.0)
                    {
                        creditTurns++;
                        if (row.Good.Value != grain || !(row.ConsumeRemainder > -3.0)) remainderViolations++;
                    }
                    else if (!(row.ConsumeRemainder < 1.0)) remainderViolations++;
                }
                if (neg) negStockTurns++;

                // --- colonies --------------------------------------------------------------------------------
                int founded = w.Settlements.Count - prev.Settlements.Count;
                if (founded > 0)
                {
                    foundingTurnCount++;
                    if (firstFound < 0) firstFound = t;
                    lastFound = t;
                    if (founded > maxPerTurn) maxPerTurn = founded;
                    var demandIds = new StringBuilder();
                    var demandCtrl = new StringBuilder();
                    foreach (int s in demandSources)
                    {
                        SettlementId sid = prev.Settlements[s].Id;
                        if (demandIds.Length > 0) { demandIds.Append(','); demandCtrl.Append(','); }
                        demandIds.Append(sid.Value.ToString(CultureInfo.InvariantCulture));
                        demandCtrl.Append(EmpireQuery.TryGetController(prev, sid, out PolityId pc)
                            ? pc.Value.ToString(CultureInfo.InvariantCulture) : "-");
                    }
                    for (int k = prev.Settlements.Count; k < w.Settlements.Count; k++)
                    {
                        SettlementId cid = w.Settlements[k].Id;
                        string ctrl = EmpireQuery.TryGetController(w, cid, out PolityId cp)
                            ? cp.Value.ToString(CultureInfo.InvariantCulture) : "-";
                        int gi = GoodStockIndex.IndexOf(w.GoodStocks, cid, new GoodId(grain));
                        colonies.Add(new ColonyTrace(cid, t, demandSources.Count, demandIds.ToString(), ctrl,
                            demandCtrl.ToString(), Pop(w, cid), gi >= 0 ? w.GoodStocks[gi].Amount.Value : -1));
                    }
                }
                foreach (int s in demandSources)
                    if (Pop(w, prev.Settlements[s].Id) <= 0) sourcesEmptied++;
                for (int c = 0; c < colonies.Count; c++)
                {
                    ColonyTrace ct = colonies[c];
                    long p = Pop(w, ct.Id);
                    if (p > ct.MaxPop) ct.MaxPop = p;
                    if (p <= 0 && ct.DeathTurn < 0) ct.DeathTurn = t;
                    ct.PopEnd = p;
                    colonies[c] = ct;
                }
                if (w.Settlements.Count > maxSettlements) maxSettlements = w.Settlements.Count;
                if (trace && Starved(w) > Starved(prev)) TraceStarvation(tracePath, seed, worldName, arm, t, prev, w, cfg);

                if (t == snapshot)
                {
                    foundingsSnap = w.Settlements.Count - initial;
                    conservedSnap = ConservationAuditor.IsConserved(w, cfg.Goods, out _) ? "yes" : "NO";
                    starvSnap = Starved(w);
                    popSnap = TotalPop(w);
                }
            }
            t = turns;
        }
        catch (Exception e)
        {
            status = "EXCEPTION";
            excTurn = t.ToString(CultureInfo.InvariantCulture);
            exception = (e.GetType().Name + ": " + e.Message).Replace('\t', ' ').Replace('\n', ' ');
            File.AppendAllText(errorPath, string.Create(CultureInfo.InvariantCulture,
                $"=== seed {seed} world {worldName} arm {arm} ai {ai} founders {founders} turn {t}\n{e}\n\n"));
        }

        conservedEnd = ConservationAuditor.IsConserved(w, cfg.Goods, out string report) ? "yes" : "NO";
        if (conservedEnd == "NO")
            File.AppendAllText(errorPath, string.Create(CultureInfo.InvariantCulture,
                $"=== AUDIT seed {seed} world {worldName} arm {arm} ai {ai} founders {founders}\n{report}\n\n"));

        int alive = 0, died = 0;
        long colonyPop = 0, colonyMax = 0;
        var detail = new StringBuilder();
        foreach (ColonyTrace ct in colonies)
        {
            if (ct.PopEnd > 0) alive++; else died++;
            colonyPop += ct.PopEnd;
            if (ct.PopEnd > colonyMax) colonyMax = ct.PopEnd;
            detail.Append(string.Create(CultureInfo.InvariantCulture,
                $"{seed}\t{worldName}\t{arm}\t{ai}\t{founders}\t{ct.Id.Value}\t{ct.FoundedTurn}\t{ct.SourcesWithDemand}\t{ct.DemandIds}\t{ct.Controller}\t{ct.SourceControllers}\t{ct.PopAtFounding}\t{ct.MaxPop}\t{ct.PopEnd}\t{ct.DeathTurn}\t{ct.GrainAtFounding}\n"));
        }
        if (detail.Length > 0) File.AppendAllText(colonyPath, detail.ToString());

        int controlMismatches = 0;
        foreach (ColonyTrace ct in colonies)
        {
            // Inheritance: the colony's controller must be the controller of one of the turn's demand sources
            // (a stateless parent founds a stateless colony, "-").
            bool ok = false;
            foreach (string sc in ct.SourceControllers.Split(','))
                if (sc == ct.Controller) { ok = true; break; }
            if (!ok) controlMismatches++;
        }
        if (foundingsSnap < 0 && t >= snapshot) foundingsSnap = w.Settlements.Count - initial;

        return string.Create(CultureInfo.InvariantCulture,
            $"{seed}\t{worldName}\t{arm}\t{ai}\t{founders}\t{turns}\t{status}\t{excTurn}\t{exception}\t{conservedSnap}\t{conservedEnd}\t{negStockTurns}\t{remainderViolations}\t{creditTurns}" +
            $"\t{initial}\t{foundingsSnap}\t{w.Settlements.Count - initial}\t{firstFound}\t{lastFound}\t{maxPerTurn}\t{foundingTurnCount}\t{unplacedTurns}" +
            $"\t{deficitTurns}\t{sourcesEmptied}\t{alive}\t{died}\t{colonyPop}\t{colonyMax}\t{controlMismatches}" +
            $"\t{starvSnap}\t{Starved(w)}\t{popSnap}\t{TotalPop(w)}\t{maxSettlements}\t{clock.Elapsed.TotalSeconds:F0}" +
            $"\t{politiesStart}\t{w.Polities.Count}\t{controlLosses}\t{firstLossTurn}\t{aiOrders}");
    }

    /// <summary>One five-sector SectorAllocation batch per settlement the player controls: gathering (the
    /// Farming sector) at <paramref name="gatherPercent"/>, the remainder to Construction, the other sectors 0.
    /// Ordinary player orders through the ordinary validator (TargetId = settlement × 8 + sector).</summary>
    private static void AppendGatherShare(OrderLog orders, WorldState w, PolityId player, long turn, double gatherPercent)
    {
        for (int s = 0; s < w.Settlements.Count; s++)
        {
            SettlementId id = w.Settlements[s].Id;
            if (!EmpireQuery.TryGetController(w, id, out PolityId c) || c != player) continue;
            double[] weights = [gatherPercent, 0.0, 0.0, 0.0, 100.0 - gatherPercent];
            for (int sector = 0; sector < Sectors.Count; sector++)
                orders.Append(new OrderRecord(turn, player.Value, OrderKind.SectorAllocation, id.Value * 8 + sector, weights[sector]));
        }
    }

    /// <summary>H4_TRACE=1: one line per settlement in deficit on every turn that starvation deaths occurred —
    /// the turn, the deaths that turn, and the settlement's PREV population, PREV deficit ratio (the one demographics reads), PREV grain store, last grain
    /// harvest and its forager LAND CEILING (fertility-weighted arable km² × the forager per-km² yield, person-years
    /// per year) so a reading can be classed as a land-bound (Malthusian) or a labour/weather shortfall.</summary>
    private static void TraceStarvation(string path, ulong seed, string worldName, string arm, int t,
        IReadOnlyWorldState prev, IReadOnlyWorldState w, SimConfig cfg)
    {
        if (!File.Exists(path))
            File.WriteAllText(path, "seed\tworld\tarm\tturn\tyear\tdt\tstarvedThisTurn\tsettlement\tpop\tdeficitRatio\tgrainStore\tlastGrainHarvest\tarableKm2\tforagerLandCeilingPerYear\n");
        long delta = Starved(w) - Starved(prev);
        double perKm2 = cfg.Farming.PreCultivation?.YieldPerArableKm2PerYear ?? double.NaN;
        var sb = new StringBuilder();
        // The deficit demographics reads is PREV's (the turn before the deaths); the store and harvest are PREV's too.
        for (int i = 0; i < prev.ConsumptionDeficits.Count; i++)
        {
            ConsumptionDeficitRow d = prev.ConsumptionDeficits[i];
            if (!(d.DeficitRatio > 0.0)) continue;
            int gi = GoodStockIndex.IndexOf(prev.GoodStocks, d.Settlement, new GoodId(cfg.Goods!.GrainId));
            double arable = 0.0;
            for (int c = 0; c < prev.CatchmentSummaries.Count; c++)
                if (prev.CatchmentSummaries[c].Settlement == d.Settlement) { arable = prev.CatchmentSummaries[c].EffectiveArableKm2; break; }
            sb.Append(string.Create(CultureInfo.InvariantCulture,
                $"{seed}\t{worldName}\t{arm}\t{t}\t{w.Clock.WorldDateYears:F0}\t{w.Clock.DtYears}\t{delta}\t{d.Settlement.Value}\t{Pop(prev, d.Settlement)}\t{d.DeficitRatio:F4}\t{(gi >= 0 ? prev.GoodStocks[gi].Amount.Value : -1)}\t{(gi >= 0 ? prev.GoodStocks[gi].LastProducedUnits : -1)}\t{arable:F0}\t{arable * perKm2:F0}\n"));
        }
        if (sb.Length == 0)
            sb.Append(string.Create(CultureInfo.InvariantCulture,
                $"{seed}\t{worldName}\t{arm}\t{t}\t{w.Clock.WorldDateYears:F0}\t{w.Clock.DtYears}\t{delta}\t-\t-\t-\t-\t-\t-\t-\n"));
        File.AppendAllText(path, sb.ToString());
    }

    private static long Pop(IReadOnlyWorldState w, SettlementId s)
    {
        long pop = 0;
        for (int i = 0; i < w.Buckets.Count; i++)
            if (w.Buckets[i].Settlement == s) pop += w.Buckets[i].Count.Value;
        return pop;
    }

    private static long TotalPop(IReadOnlyWorldState w)
    {
        long pop = 0;
        for (int i = 0; i < w.Buckets.Count; i++) pop += w.Buckets[i].Count.Value;
        return pop;
    }

    private static long Starved(IReadOnlyWorldState w)
    {
        for (int i = 0; i < w.LedgerFlows.Count; i++)
            if (w.LedgerFlows[i].Quantity == ConservedQuantityIds.Population && w.LedgerFlows[i].Reason == ReasonIds.Starvation)
                return w.LedgerFlows[i].TotalSunk;
        return 0;
    }

    private record struct ColonyTrace(SettlementId Id, int FoundedTurn, int SourcesWithDemand, string DemandIds,
        string Controller, string SourceControllers, long PopAtFounding, long GrainAtFounding)
    {
        public long MaxPop { get; set; } = PopAtFounding;
        public long PopEnd { get; set; } = PopAtFounding;
        public int DeathTurn { get; set; } = -1;
    }
}
