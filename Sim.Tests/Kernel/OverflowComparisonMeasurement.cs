using System.Globalization;
using System.Numerics;
using System.Text;
using Sim.Core;
using Sim.Core.Kernel;
using Sim.Core.State;
using Sim.Core.Systems;
using Sim.Core.Systems.Research;
using Sim.Core.Worldgen;
using Sim.Tests.TestUtil;
using Xunit.Abstractions;

namespace Sim.Tests.Kernel;

/// <summary>
/// ADR-050 (D-064 evidence) — MEASUREMENT, not a gate (skipped): the shipped content with tuning.researchOverflow OFF and ON,
/// the same canonical world, seeds and turn limit (the PacingReferenceMeasurement setup: 1,024 px, one AI Empire, seeds 1, 2,
/// 42, 7, 1,630 turns). Nothing else differs — no cost or pacing change. Per run it reports: the polity's Age entry turns,
/// the turn each (tree, Age) of finite content is complete, the first ten completions per tree, RP generated / spent / lost
/// per polity Age (lost = the per-turn pool minus what throughput added to technology progress), the CP account, and at the
/// end infrastructure (structures in controlled settlements, road developments, institutions) and discovery (surveys,
/// explorations, known cells). Results: docs/m6-plan/knowledge-redesign/research-cost-pass/overflow-comparison.md.
/// </summary>
public class OverflowComparisonMeasurement(ITestOutputHelper output)
{
    private static readonly PolityId Ai = new(2);
    private static readonly CultureInfo Inv = CultureInfo.InvariantCulture;

    [Fact(Skip = "measurement — run explicitly; results in research-cost-pass/overflow-comparison.md")]
    public void OverflowOffVsOn_FourSeeds()
    {
        ulong[] seeds = [1, 2, 42, 7];
        var jobs = new (ulong Seed, bool On)[seeds.Length * 2];
        for (int i = 0; i < seeds.Length; i++) { jobs[2 * i] = (seeds[i], false); jobs[2 * i + 1] = (seeds[i], true); }
        string[] lines = new string[jobs.Length];
        System.Threading.Tasks.Parallel.For(0, jobs.Length, new System.Threading.Tasks.ParallelOptions { MaxDegreeOfParallelism = 4 },
            i => lines[i] = Run(jobs[i].Seed, jobs[i].On));
        foreach (string l in lines) output.WriteLine(l);
    }

    /// <summary>The shipped research.json with tuning.researchOverflow set to <paramref name="on"/> — nothing else changed.</summary>
    [Fact(Skip = "measurement — run explicitly; results in research-cost-pass/overflow-comparison.md")]
    public void OverflowLaneVariant_FourSeeds()
    {
        ulong[] seeds = [1, 2, 42, 7];
        string[] lines = new string[seeds.Length];
        System.Threading.Tasks.Parallel.For(0, seeds.Length, i => lines[i] = Run(seeds[i], true, lane: true));
        foreach (string l in lines) output.WriteLine(l);
    }

    internal static SimConfig WithOverflow(SimConfig cfg, bool on, bool lane = false)
    {
        System.Text.Json.Nodes.JsonObject doc;
        using (var s = Sim.Data.DataFiles.OpenResearch()) doc = System.Text.Json.Nodes.JsonNode.Parse(s)!.AsObject();
        doc["tuning"]!.AsObject()["researchOverflow"] = on;
        if (lane) doc["tuning"]!.AsObject()["researchOverflowLane"] = true;
        using var goods = Sim.Data.DataFiles.OpenGoods();
        return cfg with { Research = ResearchContentLoader.Load(doc.ToJsonString(), GoodsConfigLoader.Load(goods)) };
    }

    private static string Run(ulong seed, bool on, bool lane = false)
    {
        SimConfig cfg = WithOverflow(TestConfigs.Sim(), on, lane);
        WorldgenConfig shipped = TestConfigs.Worldgen();
        var log = new OrderLog();
        TurnExecutor exec;
        using (var era = Sim.Data.DataFiles.OpenEraPacing())
        using (var pipe = Sim.Data.DataFiles.OpenPipeline())
            exec = new TurnExecutor(EraTableLoader.Load(era), PipelineLoader.Load(pipe, SystemCatalog.All(cfg, shipped)), log);
        WorldState w = WorldFounding.Found(shipped with { AiEmpires = 1 }, cfg, seed);
        ResearchContent research = cfg.Research!;
        int n = research.Nodes.Count;
        var sb = new StringBuilder();
        sb.AppendLine(Inv, $"seed {seed} overflow {(on ? (lane ? "ON-LANE" : "ON") : "OFF")}");
        var doneTurn = new long[2, 10];
        var finite = new int[2, 10];
        for (int a = 0; a < 10; a++) { doneTurn[0, a] = -1; doneTurn[1, a] = -1; }
        foreach (ResearchNode node in research.Nodes)
            if (!node.HasRepeatableDescriptor) finite[node.Tree == ResearchTree.Technology ? 0 : 1, node.AgeNumber]++;
        var first = new List<string>[] { new(), new() };
        var rpGen = new double[10];
        var rpSpent = new double[10];
        var turnsAt = new int[10];
        for (int t = 0; t < 1630; t++)
        {
            AiOrders.Append(log, w, cfg, shipped);
            WorldState prev = w;
            int age = AgeQuery.CurrentAge(prev, cfg.Ages!, Ai);
            double pool = ResearchQuery.ResearchPointPool(prev, research, Ai);
            bool[] before = ResearchQuery.CompletedMask(prev, research, Ai);
            double[] progressBefore = ProgressOf(prev, research, n);
            double[] cost = new double[n];
            for (int i = 0; i < n; i++) cost[i] = ResearchQuery.EffectiveCost(prev, research, Ai, i);
            double creditsBefore = TechCredits(prev, research);
            w = exec.Step(w);
            bool[] after = ResearchQuery.CompletedMask(w, research, Ai);
            double[] progressAfter = ProgressOf(w, research, n);
            double added = 0.0;
            for (int i = 0; i < n; i++)
            {
                if (research.Nodes[i].Tree != ResearchTree.Technology) continue;
                bool levelled = ResearchQuery.Level(w, Ai, research.Nodes[i].Key) > ResearchQuery.Level(prev, Ai, research.Nodes[i].Key);
                double reached = (after[i] && !before[i]) || levelled ? cost[i] : progressAfter[i];
                added += reached - progressBefore[i];
            }
            double spent = added - (TechCredits(w, research) - creditsBefore);
            rpGen[age] += pool;
            rpSpent[age] += Math.Min(pool, Math.Max(0.0, spent));
            turnsAt[age]++;
            var count = new int[2, 10];
            for (int i = 0; i < n; i++)
            {
                if (!after[i] || research.Nodes[i].HasRepeatableDescriptor) continue;
                int tr = research.Nodes[i].Tree == ResearchTree.Technology ? 0 : 1;
                count[tr, research.Nodes[i].AgeNumber]++;
                if (!before[i] && first[tr].Count < 10) first[tr].Add(string.Create(Inv, $"{research.Nodes[i].Id}@{w.Clock.Turn}"));
            }
            for (int tr = 0; tr < 2; tr++)
                for (int a = 1; a < 10; a++)
                    if (doneTurn[tr, a] < 0 && finite[tr, a] > 0 && count[tr, a] == finite[tr, a]) doneTurn[tr, a] = w.Clock.Turn;
        }
        sb.Append("  Age entered (effective turn):");
        for (int r = 0; r < w.AgeTransitions.Count; r++)
            if (w.AgeTransitions[r].Polity.Value == Ai.Value) sb.Append(Inv, $" A{w.AgeTransitions[r].ToAge}={w.AgeTransitions[r].EffectiveTurn}");
        sb.AppendLine();
        sb.Append("  Age complete (turn) technology:");
        for (int a = 1; a < 10; a++) sb.Append(Inv, $" A{a}={doneTurn[0, a]}");
        sb.AppendLine();
        sb.Append("  Age complete (turn) civics:    ");
        for (int a = 1; a < 10; a++) sb.Append(Inv, $" A{a}={doneTurn[1, a]}");
        sb.AppendLine();
        sb.AppendLine("  first technologies: " + string.Join(" ", first[0]));
        sb.AppendLine("  first civics:       " + string.Join(" ", first[1]));
        sb.Append("  RP by polity Age (turns, generated, spent, lost %):");
        for (int a = 1; a < 10; a++)
            if (turnsAt[a] > 0)
                sb.Append(Inv, $" A{a}[{turnsAt[a]}t {rpGen[a]:0} {rpSpent[a]:0} {(rpGen[a] > 0 ? 100.0 * (rpGen[a] - rpSpent[a]) / rpGen[a] : 0):0.0}%]");
        sb.AppendLine();
        for (int r = 0; r < w.CivicsPoints.Count; r++)
            if (w.CivicsPoints[r].Polity.Value == Ai.Value)
            {
                CivicsPointsRow cp = w.CivicsPoints[r];
                sb.AppendLine(Inv, $"  CP generated {cp.GeneratedMilli / 1000.0:0} spent {cp.SpentMilli / 1000.0:0} banked {cp.BalanceMilli / 1000.0:0}");
            }
        int tech = ResearchQuery.CompletedNodes(w, research, Ai, ResearchTree.Technology).Length;
        int civ = ResearchQuery.CompletedNodes(w, research, Ai, ResearchTree.Civics).Length;
        long levels = 0;
        for (int r = 0; r < w.ResearchLevels.Count; r++) if (w.ResearchLevels[r].Polity.Value == Ai.Value) levels += w.ResearchLevels[r].Level;
        var mine = new HashSet<int>();   // test bookkeeping, not sim logic
        for (int r = 0; r < w.Controls.Count; r++) if (w.Controls[r].Polity.Value == Ai.Value) mine.Add(w.Controls[r].Place.Value);
        long structures = 0;
        for (int r = 0; r < w.Structures.Count; r++) if (mine.Contains(w.Structures[r].Settlement.Value)) structures += w.Structures[r].Count;
        int roads = 0, surveys = 0, explorations = 0, institutions = 0;
        long known = 0;
        for (int r = 0; r < w.RoadDevelopments.Count; r++) if (w.RoadDevelopments[r].Polity.Value == Ai.Value) roads++;
        for (int r = 0; r < w.SettlementSurveys.Count; r++) if (w.SettlementSurveys[r].Polity.Value == Ai.Value) surveys++;
        for (int r = 0; r < w.Explorations.Count; r++) if (w.Explorations[r].Polity.Value == Ai.Value) explorations++;
        for (int r = 0; r < w.KnownExtents.Count; r++) if (w.KnownExtents[r].Polity.Value == Ai.Value) known += BitOperations.PopCount(w.KnownExtents[r].Bits);
        for (int r = 0; r < w.Institutions.Count; r++) if (mine.Contains(w.Institutions[r].Settlement.Value)) institutions++;
        sb.AppendLine(Inv, $"  end: Age {AgeQuery.CurrentAge(w, cfg.Ages!, Ai)} tech {tech} civics {civ} levels {levels} population {ResearchQuery.Population(w, Ai):0} settlements {mine.Count}");
        sb.AppendLine(Inv, $"  infrastructure: structures {structures} road developments {roads} institutions {institutions}; discovery: surveys {surveys} explorations {explorations} known cells {known}");
        return sb.ToString();
    }

    private static double[] ProgressOf(IReadOnlyWorldState w, ResearchContent research, int n)
    {
        var p = new double[n];
        for (int r = 0; r < w.ResearchProgress.Count; r++)
        {
            ResearchProgressRow row = w.ResearchProgress[r];
            if (row.Polity.Value != Ai.Value) continue;
            int i = research.IndexOf(row.Node);
            if (i >= 0) p[i] = row.Progress;
        }
        return p;
    }

    private static double TechCredits(IReadOnlyWorldState w, ResearchContent research)
    {
        double sum = 0.0;
        for (int r = 0; r < w.ResearchCredits.Count; r++)
        {
            ResearchCreditRow row = w.ResearchCredits[r];
            if (row.Polity.Value != Ai.Value) continue;
            int i = research.IndexOf(row.Node);
            if (i >= 0 && research.Nodes[i].Tree == ResearchTree.Technology) sum += row.Amount;
        }
        return sum;
    }
}
