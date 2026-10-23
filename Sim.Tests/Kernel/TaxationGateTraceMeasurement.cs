using Sim.Core;
using Sim.Core.Kernel;
using Sim.Core.State;
using Sim.Core.Systems;
using Sim.Core.Systems.Ages;
using Sim.Core.Systems.Research;
using Sim.Core.Worldgen;
using Sim.Tests.TestUtil;
using Xunit;
using Xunit.Abstractions;

namespace Sim.Tests.Kernel;

/// <summary>
/// MEASUREMENT (2026-10-23, Director review item 2): WHICH M6 layer moves the canonical AI's first tax levy (pre-M6 turn
/// 422, M6 turn 579)? The levy needs sim.json governance.taxationRequires ("taxation" civic) AND taxationMinAge 3
/// (Governance.CanLevyTax), so the trace records, per configuration: the turn the Taxation civic completes, the turn the
/// AI enters Age III, the first levy, and the first turn each Age III milestone holds (core a3_bronze; supporting a3_law,
/// a3_writing, a3_wheel, a3_bronze_stock, a3_artisans, a3_exchange, a3_population). Configurations isolate each M6
/// layer with the existing switches: Track EX (PreDiscovery), Track CP (PreTrackCp), TREE (PreTreeTranche), and pairs.
/// A second measurement lists, CP on vs off (discovery off in both), every node the AI completes before its bronze node
/// and the goal (core / road / tax / university) its Technology lane works (ADR-043 two lanes; AiResearchPolicy).
/// Not a pin — the numbers are written to the test output and recorded in
/// docs/m6-plan/knowledge-redesign/audit/taxation-gate-trace.md. Skipped by default (runs ≈ 9 × 600 AI turns).
/// </summary>
public class TaxationGateTraceMeasurement(ITestOutputHelper output)
{
    private static readonly PolityId Rival = new(2);
    private const int Horizon = 700;

    [Fact(Skip = "measurement — run explicitly; results in docs/m6-plan/knowledge-redesign/audit/taxation-gate-trace.md")]
    public void LevyAndAgeIII_ByM6Layer()
    {
        SimConfig full = TestConfigs.Sim();
        (string Name, SimConfig Cfg)[] configs =
        [
            ("M6 (shipped)", full),
            ("discovery off", TestConfigs.PreDiscovery(full)),
            ("CP off (stage restored)", TestConfigs.PreTrackCp(full)),
            ("tree tranche off", TestConfigs.PreTreeTranche(full)),
            ("discovery+CP off", TestConfigs.PreDiscovery(TestConfigs.PreTrackCp(full))),
            ("discovery+tree off", TestConfigs.PreDiscovery(TestConfigs.PreTreeTranche(full))),
            ("pre-M6 (all off)", TestConfigs.PreM6(full)),
        ];
        output.WriteLine("config | taxation civic | law_code | bronze node | A3 eligible | A3 entered | first levy | first turn each A3 milestone holds");
        foreach (var (name, cfg) in configs) output.WriteLine(name + " | " + Trace(cfg));
    }

    private static string Trace(SimConfig cfg)
    {
        WorldgenConfig worldgen = TestConfigs.Worldgen();
        WorldgenConfig wg = worldgen with { AiEmpires = 1 };
        ResearchContent research = cfg.Research!;
        AgeContent ages = TestConfigs.Ages();
        var log = new OrderLog();
        using var eraStream = Sim.Data.DataFiles.OpenEraPacing();
        using var pipeStream = Sim.Data.DataFiles.OpenPipeline();
        var executor = new TurnExecutor(EraTableLoader.Load(eraStream),
            PipelineLoader.Load(pipeStream, SystemCatalog.All(cfg, worldgen)), log);
        WorldState w = WorldFounding.Found(wg, cfg, 42);
        ResearchNodeId taxKey = research.Nodes[research.IndexOfId("taxation")].Key;
        ResearchNodeId lawKey = research.Nodes[research.IndexOfId("law_code")].Key;
        ResearchNodeId tinKey = research.Nodes[research.IndexOfId("tin_bronze")].Key;
        ResearchNodeId arsKey = research.Nodes[research.IndexOfId("arsenical_bronze")].Key;
        long tax = -1, law = -1, bronze = -1, eligible = -1, a3 = -1, levy = -1;
        var firstMet = new SortedDictionary<string, long>(StringComparer.Ordinal);
        for (int t = 0; t < Horizon && levy < 0; t++)
        {
            AiOrders.Append(log, w, cfg, worldgen);
            w = executor.Step(w);
            long turn = w.Clock.Turn;
            if (tax < 0 && ResearchQuery.IsCompleted(w, Rival, taxKey)) tax = turn;
            if (law < 0 && ResearchQuery.IsCompleted(w, Rival, lawKey)) law = turn;
            if (bronze < 0 && (ResearchQuery.IsCompleted(w, Rival, tinKey) || ResearchQuery.IsCompleted(w, Rival, arsKey))) bronze = turn;
            int age = AgeQuery.CurrentAge(w, ages, Rival);
            if (age == 2)
            {
                AgeEligibilityReport? r = AgeQuery.EvaluateFor(w, ages, Rival, 2);
                if (r is not null)
                {
                    foreach (MilestoneStatus m in r.Core) Note(firstMet, m, turn);
                    foreach (MilestoneStatus m in r.Supporting) Note(firstMet, m, turn);
                    if (eligible < 0 && r.Eligible) eligible = turn;
                }
            }
            if (a3 < 0 && age >= 3) a3 = turn;
            if (levy < 0)
                for (int i = 0; i < w.TaxPolicies.Count; i++)
                    if (w.TaxPolicies[i].Polity == Rival && w.TaxPolicies[i].Rate > 0.0) { levy = turn; break; }
        }
        string milestones = string.Join(", ", firstMet.Select(kv => kv.Key + "=" + kv.Value));
        return $"{tax} | {law} | {bronze} | {eligible} | {a3} | {levy} | {milestones}";
    }

    private static void Note(SortedDictionary<string, long> firstMet, MilestoneStatus m, long turn)
    {
        if (m.Met && !firstMet.ContainsKey(m.Milestone.Id)) firstMet[m.Milestone.Id] = turn;
    }

    [Fact(Skip = "measurement — run explicitly; results in docs/m6-plan/knowledge-redesign/audit/taxation-gate-trace.md")]
    public void TechOrderBeforeBronze_CpOnVsOff()
    {
        SimConfig full = TestConfigs.PreDiscovery(TestConfigs.Sim());
        string[] on = TechOrder(full), off = TechOrder(TestConfigs.PreTrackCp(full));
        output.WriteLine("CP on  (" + on.Length + "): " + string.Join(" ", on));
        output.WriteLine("CP off (" + off.Length + "): " + string.Join(" ", off));
        output.WriteLine("only with CP on: " + string.Join(" ", on.Except(off)));
        output.WriteLine("only with CP off: " + string.Join(" ", off.Except(on)));
    }

    private static string[] TechOrder(SimConfig cfg)
    {
        WorldgenConfig worldgen = TestConfigs.Worldgen();
        WorldgenConfig wg = worldgen with { AiEmpires = 1 };
        ResearchContent research = cfg.Research!;
        var log = new OrderLog();
        using var eraStream = Sim.Data.DataFiles.OpenEraPacing();
        using var pipeStream = Sim.Data.DataFiles.OpenPipeline();
        var executor = new TurnExecutor(EraTableLoader.Load(eraStream),
            PipelineLoader.Load(pipeStream, SystemCatalog.All(cfg, worldgen)), log);
        WorldState w = WorldFounding.Found(wg, cfg, 42);
        ResearchNodeId tin = research.Nodes[research.IndexOfId("tin_bronze")].Key, ars = research.Nodes[research.IndexOfId("arsenical_bronze")].Key;
        var done = new List<string>();
        string lastGoal = "";
        for (int t = 0; t < Horizon; t++)
        {
            AiOrders.Append(log, w, cfg, worldgen);
            WorldState prev = w;
            w = executor.Step(prev);
            foreach (ResearchNodeId k in ResearchQuery.CompletedBetween(prev, w, Rival))
            {
                var n = research.Nodes[research.IndexOf(k)];
                done.Add(n.Id + "@" + w.Clock.Turn + (n.Tree == ResearchTree.Civics ? "(c)" : ""));
            }
            if (ResearchQuery.IsCompleted(w, Rival, tin) || ResearchQuery.IsCompleted(w, Rival, ars)) break;
            if (research.Tuning.TwoLanes)
            {
                bool[] completed = ResearchQuery.CompletedMask(w, research, Rival);
                bool[] available = ResearchQuery.AvailableMask(research, completed);
                bool[][] goals = AiResearchPolicy.Goals(w, cfg, Rival);
                for (int g = 0; g < goals.Length; g++)
                    for (int i = 0; i < goals[g].Length; i++) goals[g][i] &= research.Nodes[i].Tree == ResearchTree.Technology;
                int goal = AiResearchPolicy.ChooseGoal(w, research, Rival, goals, completed, available);
                string label = goal switch { 0 => "core", 1 => "road", 2 => "tax", 3 => "university", _ => "none" };
                if (lastGoal != label) { done.Add("[tech goal " + label + " from " + w.Clock.Turn + "]"); lastGoal = label; }
            }
        }
        return done.ToArray();
    }

    /// <summary>ADR-046 measurement (2026-10-23): pre-M6, M6 before the cheapest-route fix (<see cref="TestConfigs.PreAiRoutes"/>)
    /// and M6 with it, seeds 1, 2, 42, 7, 900 AI turns: Taxation civic, paved-road node, bronze node, Age III entry, first
    /// levy, Age IV entry, and the nodes completed by turns 300 / 600 / 900 — the last as a check that other goals still
    /// progress.</summary>
    [Fact(Skip = "measurement — run explicitly; results in docs/m6-plan/knowledge-redesign/audit/taxation-gate-trace.md")]
    public void CheapestRouteFix_FourSeeds()
    {
        SimConfig full = TestConfigs.Sim();
        (string Name, SimConfig Cfg)[] configs =
        [
            ("pre-M6", TestConfigs.PreM6(full)),
            ("M6 before fix", TestConfigs.PreAiRoutes(full)),
            ("M6 with fix", full),
        ];
        output.WriteLine("seed | config | taxation | road_paved | bronze | A3 | first levy | A4 | nodes@300 | nodes@600 | nodes@900");
        foreach (ulong seed in new ulong[] { 1, 2, 42, 7 })
            foreach (var (name, cfg) in configs)
                output.WriteLine(seed + " | " + name + " | " + Timeline(cfg, seed));
    }

    /// <summary>ADR-046 follow-up (2026-10-23): what stands between the AI and Age IV at turn 900 — the next Age's unmet
    /// entry requirements (AgeQuery.Evaluate) on seeds 1 and 42, with the cheapest-route closure and with its twin off.</summary>
    [Fact(Skip = "measurement — run explicitly; results in docs/m6-plan/knowledge-redesign/audit/taxation-gate-trace.md §7")]
    public void AgeIV_UnmetRequirementsAtTurn900()
    {
        SimConfig full = TestConfigs.Sim();
        foreach (ulong seed in new ulong[] { 1, 42 })
            foreach ((string name, SimConfig cfg) in new[] { ("with fix", full), ("before fix", TestConfigs.PreAiRoutes(full)) })
            {
                WorldgenConfig worldgen = TestConfigs.Worldgen();
                AgeContent ages = TestConfigs.Ages();
                var log = new OrderLog();
                using var eraStream = Sim.Data.DataFiles.OpenEraPacing();
                using var pipeStream = Sim.Data.DataFiles.OpenPipeline();
                var executor = new TurnExecutor(EraTableLoader.Load(eraStream), PipelineLoader.Load(pipeStream, SystemCatalog.All(cfg, worldgen)), log);
                WorldState w = WorldFounding.Found(worldgen with { AiEmpires = 1 }, cfg, seed);
                long a4 = -1;
                var at = new List<string>();
                for (int t = 0; t < 900; t++)
                {
                    AiOrders.Append(log, w, cfg, worldgen);
                    w = executor.Step(w);
                    if (a4 < 0 && AgeQuery.CurrentAge(w, ages, Rival) >= 4) a4 = w.Clock.Turn;
                    if (w.Clock.Turn is 700 or 800 or 900 && AgeQuery.CurrentAge(w, ages, Rival) == 3
                        && AgeQuery.Evaluate(w, ages, Rival) is { } r)
                        at.Add($"t{w.Clock.Turn}: core {r.CoreMet}/{r.CoreTotal}, supporting {r.SupportingMet}/{r.SupportingRequired}, "
                            + $"categories {r.CategoriesCovered.Count}/{r.CategoriesRequired} — {string.Join("; ", r.Remaining)}"
                            + (w.Clock.Turn == 900 ? " — supporting: " + string.Join(", ", r.Supporting.Select(m =>
                                m.Milestone.Name + " [" + m.Milestone.Category + "] " + (m.Met ? "MET" : "unmet"))) : ""));
                }
                output.WriteLine($"seed {seed} | {name} | A4 {a4} | " + string.Join(" || ", at));
            }
    }

    /// <summary>ADR-046 §6 (M6 review batch 3): the supporting-milestone goal, with and without, seeds 1, 2, 42, 7, 900 turns —
    /// Alphabetic script (abjad), Census, Ironworking (iron_bloomery), Age II/III/IV and the first levy.</summary>
    [Fact(Skip = "measurement — run explicitly; results in docs/m6-plan/knowledge-redesign/audit/taxation-gate-trace.md §8")]
    public void SupportingMilestones_FourSeeds()
    {
        SimConfig full = TestConfigs.Sim();
        var runs = new List<(ulong Seed, string Name, SimConfig Cfg)>();
        foreach (ulong seed in new ulong[] { 1, 2, 42, 7 })
        {
            runs.Add((seed, "baseline (e91d10b: supporting goal off)", TestConfigs.PreAiSupporting(full)));
            runs.Add((seed, "supporting goal on", full));
        }
        string[] lines = new string[runs.Count];
        System.Threading.Tasks.Parallel.For(0, runs.Count, i => lines[i] = runs[i].Seed + " | " + runs[i].Name + " | " + Milestones(runs[i].Cfg, runs[i].Seed));
        output.WriteLine("seed | config | abjad | census | iron_bloomery | A2 | A3 | A4 | first levy | nodes@900");
        foreach (string line in lines) output.WriteLine(line);
    }

    private static string Milestones(SimConfig cfg, ulong seed)
    {
        WorldgenConfig worldgen = TestConfigs.Worldgen();
        ResearchContent research = cfg.Research!;
        AgeContent ages = TestConfigs.Ages();
        var log = new OrderLog();
        using var eraStream = Sim.Data.DataFiles.OpenEraPacing();
        using var pipeStream = Sim.Data.DataFiles.OpenPipeline();
        var executor = new TurnExecutor(EraTableLoader.Load(eraStream), PipelineLoader.Load(pipeStream, SystemCatalog.All(cfg, worldgen)), log);
        WorldState w = WorldFounding.Found(worldgen with { AiEmpires = 1 }, cfg, seed);
        ResearchNodeId Key(string id) => research.Nodes[research.IndexOfId(id)].Key;
        ResearchNodeId abjad = Key("abjad"), census = Key("census"), iron = Key("iron_bloomery");
        long tA = -1, tC = -1, tI = -1, a2 = -1, a3 = -1, a4 = -1, levy = -1;
        for (int t = 0; t < 900; t++)
        {
            AiOrders.Append(log, w, cfg, worldgen);
            w = executor.Step(w);
            long turn = w.Clock.Turn;
            if (tA < 0 && ResearchQuery.IsCompleted(w, Rival, abjad)) tA = turn;
            if (tC < 0 && ResearchQuery.IsCompleted(w, Rival, census)) tC = turn;
            if (tI < 0 && ResearchQuery.IsCompleted(w, Rival, iron)) tI = turn;
            int age = AgeQuery.CurrentAge(w, ages, Rival);
            if (a2 < 0 && age >= 2) a2 = turn;
            if (a3 < 0 && age >= 3) a3 = turn;
            if (a4 < 0 && age >= 4) a4 = turn;
            if (levy < 0)
                for (int i = 0; i < w.TaxPolicies.Count; i++)
                    if (w.TaxPolicies[i].Polity == Rival && w.TaxPolicies[i].Rate > 0.0) { levy = turn; break; }
        }
        long known = 0;
        foreach (bool d in ResearchQuery.CompletedMask(w, research, Rival)) if (d) known++;
        return $"{tA} | {tC} | {tI} | {a2} | {a3} | {a4} | {levy} | {known}";
    }

    private static string Timeline(SimConfig cfg, ulong seed)
    {
        WorldgenConfig worldgen = TestConfigs.Worldgen();
        WorldgenConfig wg = worldgen with { AiEmpires = 1 };
        ResearchContent research = cfg.Research!;
        AgeContent ages = TestConfigs.Ages();
        var log = new OrderLog();
        using var eraStream = Sim.Data.DataFiles.OpenEraPacing();
        using var pipeStream = Sim.Data.DataFiles.OpenPipeline();
        var executor = new TurnExecutor(EraTableLoader.Load(eraStream),
            PipelineLoader.Load(pipeStream, SystemCatalog.All(cfg, worldgen)), log);
        WorldState w = WorldFounding.Found(wg, cfg, seed);
        ResearchNodeId Key(string id) => research.Nodes[research.IndexOfId(id)].Key;
        ResearchNodeId taxKey = Key("taxation"), paved = Key("road_paved"), tin = Key("tin_bronze"), ars = Key("arsenical_bronze");
        long tax = -1, road = -1, bronze = -1, a3 = -1, levy = -1, a4 = -1, n300 = -1, n600 = -1, n900 = -1;
        for (int t = 0; t < 900; t++)
        {
            AiOrders.Append(log, w, cfg, worldgen);
            w = executor.Step(w);
            long turn = w.Clock.Turn;
            if (tax < 0 && ResearchQuery.IsCompleted(w, Rival, taxKey)) tax = turn;
            if (road < 0 && ResearchQuery.IsCompleted(w, Rival, paved)) road = turn;
            if (bronze < 0 && (ResearchQuery.IsCompleted(w, Rival, tin) || ResearchQuery.IsCompleted(w, Rival, ars))) bronze = turn;
            int age = AgeQuery.CurrentAge(w, ages, Rival);
            if (a3 < 0 && age >= 3) a3 = turn;
            if (a4 < 0 && age >= 4) a4 = turn;
            if (levy < 0)
                for (int i = 0; i < w.TaxPolicies.Count; i++)
                    if (w.TaxPolicies[i].Polity == Rival && w.TaxPolicies[i].Rate > 0.0) { levy = turn; break; }
            if (turn == 300 || turn == 600 || turn == 900)
            {
                bool[] done = ResearchQuery.CompletedMask(w, research, Rival);
                long count = 0;
                foreach (bool d in done) if (d) count++;
                if (turn == 300) n300 = count; else if (turn == 600) n600 = count; else n900 = count;
            }
        }
        return $"{tax} | {road} | {bronze} | {a3} | {levy} | {a4} | {n300} | {n600} | {n900}";
    }
}
