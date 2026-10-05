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
/// the famine-class disaster armed at CR-016's derived λ = 0.01 (shipped 0.0, rate OPEN for the Director).
///
/// Parameters come from the environment so several arms run as separate processes (the machine is shared):
/// H4_WORLD (canonical | dev), H4_ARM (none | allgather | lone | famine | disaster), H4_AI (AI empires, default 0),
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
        "\tstarvationSnap\tstarvationEnd\tpopSnap\tpopEnd\tmaxSettlements\tsecondsWall";

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

        for (int seed = seedLo; seed <= seedHi; seed++)
        {
            string line = RunOne((ulong)seed, world, arm, ai, founders, turns, snapshot, colonyPath, outPath + ".errors.txt");
            File.AppendAllText(outPath, line + "\n");
            output.WriteLine(line);
        }
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
        string colonyPath, string errorPath)
    {
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

        WorldgenConfig wg = (worldName == "dev" ? TestConfigs.DevWorldgen() : TestConfigs.Worldgen()) with { AiEmpires = ai };
        int? settlementsOverride = arm == "lone" ? 1 : null;
        WorldState w = WorldFounding.Found(wg, cfg, seed, settlementsOverride);

        var orders = new OrderLog();
        var player = new PolityId(1);
        if (arm == "allgather") AppendGatherShare(orders, w, player, 0, 100.0);
        if (arm == "famine") AppendGatherShare(orders, w, player, 100, 10.0);

        TurnExecutor ex;
        using (var era = Sim.Data.DataFiles.OpenEraPacing())
        using (var pipe = Sim.Data.DataFiles.OpenPipeline())
            ex = new TurnExecutor(EraTableLoader.Load(era), PipelineLoader.Load(pipe, SystemCatalog.All(cfg, wg)), orders);
        bool hasAi = AiOrders.HasAiPolity(w);
        int grain = cfg.Goods!.GrainId;

        int initial = w.Settlements.Count, maxSettlements = initial;
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
                if (hasAi) AiOrders.Append(orders, prev, cfg);

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
            $"\t{starvSnap}\t{Starved(w)}\t{popSnap}\t{TotalPop(w)}\t{maxSettlements}\t{clock.Elapsed.TotalSeconds:F0}");
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
