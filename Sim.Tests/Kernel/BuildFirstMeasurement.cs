using Sim.Core;
using Sim.Core.Kernel;
using Sim.Core.State;
using Sim.Core.Systems;
using Sim.Core.Worldgen;
using Sim.Tests.TestUtil;
using Xunit.Abstractions;

namespace Sim.Tests.Kernel;

/// <summary>
/// ADR-046 §6–§7 (M6 review batch 3, 2026-10-23) — MEASUREMENT, not a gate (skipped): the AI's first granary, its purse,
/// its surveys and assays, the concealed copper and tin it finds, and its Ages and first levy, on the canonical world
/// (seed 42, 1,024 px, one AI Empire, 900 turns) and the 256 px CLI world (seed 42, 4 settlements, one AI Empire, the
/// RunFromSaveCliTests player log), under four configs: e91d10b (both batch-3 switches off), the supporting goal alone,
/// build-first alone, and both (shipped). Results: docs/m6-plan/knowledge-redesign/audit/taxation-gate-trace.md §8–§9.
/// </summary>
public class BuildFirstMeasurement(ITestOutputHelper output)
{
    private static readonly PolityId Ai = new(2);

    [Fact(Skip = "measurement — run explicitly; results in docs/m6-plan/knowledge-redesign/audit/taxation-gate-trace.md §9")]
    public void FirstGranary_Purse_Surveys_TwoWorlds()
    {
        SimConfig full = TestConfigs.Sim();
        (string Name, SimConfig Cfg)[] configs =
        [
            ("e91d10b", TestConfigs.PreAiSupporting(TestConfigs.PreAiBuildFirst(full))),
            ("supporting only", TestConfigs.PreAiBuildFirst(full)),
            ("build-first only", TestConfigs.PreAiSupporting(full)),
            ("both (shipped)", full),
        ];
        var runs = new List<(string World, string Name, SimConfig Cfg)>();
        foreach ((string n, SimConfig c) in configs) { runs.Add(("canonical", n, c)); runs.Add(("256px", n, c)); }
        string[] lines = new string[runs.Count];
        System.Threading.Tasks.Parallel.For(0, runs.Count, i => lines[i] = runs[i].World + " | " + runs[i].Name + " | " + Run(runs[i].World, runs[i].Cfg));
        output.WriteLine("world | config | first granary order / built | first prospect | stone,timber,grain @t1 / @t10 / @t20 / @first-granary | surveyed @100/@300/@600 | assays | Cu/Sn found @600 (of hidden) | A2 | A3 | levy | A4");
        foreach (string l in lines) output.WriteLine(l);
    }

    private static string Run(string world, SimConfig cfg)
    {
        WorldgenConfig shipped = TestConfigs.Worldgen();
        WorldgenConfig wg = world == "canonical" ? shipped with { AiEmpires = 1 } : shipped with { AiEmpires = 1, SizePx = 256 };
        WorldgenConfig stepping = world == "canonical" ? shipped : shipped with { SizePx = 256 };
        var log = new OrderLog();
        TurnExecutor exec;
        using (var era = Sim.Data.DataFiles.OpenEraPacing())
        using (var pipe = Sim.Data.DataFiles.OpenPipeline())
            exec = new TurnExecutor(EraTableLoader.Load(era), PipelineLoader.Load(pipe, SystemCatalog.All(cfg, stepping)), log);
        WorldState w = world == "canonical"
            ? WorldFounding.Found(wg, cfg, 42)
            : WorldFounding.Found(wg, cfg, 42, 4);
        GoodId stone = new(cfg.Goods!.IdOf("stone")), timber = new(cfg.Goods.IdOf("timber")), grain = new(cfg.Goods.GrainId);
        GoodId copper = new(cfg.Goods.IdOf("copper-ore")), tin = new(cfg.Goods.IdOf("tin-ore"));
        var hidden = new List<DepositRow>();
        for (int i = 0; i < w.Deposits.Count; i++)
        {
            DepositRow d = w.Deposits[i];
            if ((d.Good == copper || d.Good == tin) && d.Abundance > 0.0 && EmpireQuery.ControlsSettlement(w, Ai, d.Settlement)
                && DepositConcealment.KnownAbundance(w, cfg, d.Settlement, d.Good.Value) == 0.0) hidden.Add(d);
        }
        var ages = cfg.Ages!;
        long firstOrder = -1, firstBuilt = -1, firstProspect = -1, a2 = -1, a3 = -1, a4 = -1, levy = -1;
        string purse1 = "", purse10 = "", purse20 = "", purseAtGranary = "";
        int s100 = -1, s300 = -1, s600 = -1;
        int horizon = world == "canonical" ? 900 : 600;
        string Purse(IReadOnlyWorldState x) => $"{Sum(x, stone)},{Sum(x, timber)},{Sum(x, grain)}";
        for (int t = 0; t < horizon; t++)
        {
            if (world != "canonical" && w.Clock.Turn == 6)   // RunFromSaveCliTests' player log L (the split b = 30, two granaries), then the AI
            {
                log.Append(new OrderRecord(6, 1, OrderKind.SetResourceAllocation, 1, 30.0));
                log.Append(new OrderRecord(6, 1, OrderKind.EnqueueConstruction, 0, 1.0));
                log.Append(new OrderRecord(6, 1, OrderKind.EnqueueConstruction, 2, 1.0));
            }
            AiOrders.Append(log, w, cfg, world == "canonical" ? shipped : stepping);
            w = exec.Step(w);
            long turn = w.Clock.Turn;
            for (int i = 0; i < log.Count && (firstOrder < 0 || firstProspect < 0); i++)
            {
                OrderRecord o = log[i];
                if (o.ActorId != Ai.Value) continue;
                if (firstOrder < 0 && o.Kind == OrderKind.EnqueueConstruction) firstOrder = o.Turn;
                if (firstProspect < 0 && o.Kind == OrderKind.Prospect) firstProspect = o.Turn;
            }
            if (firstBuilt < 0)
                for (int i = 0; i < w.Structures.Count; i++)
                    if (w.Structures[i].ProjectId == 1 && EmpireQuery.ControlsSettlement(w, Ai, w.Structures[i].Settlement))
                    { firstBuilt = turn; purseAtGranary = Purse(w); break; }
            if (turn == 1) purse1 = Purse(w);
            if (turn == 10) purse10 = Purse(w);
            if (turn == 20) purse20 = Purse(w);
            if (turn == 100) s100 = Surveyed(w);
            if (turn == 300) s300 = Surveyed(w);
            if (turn == 600) s600 = Surveyed(w);
            int age = AgeQuery.CurrentAge(w, ages, Ai);
            if (a2 < 0 && age >= 2) a2 = turn;
            if (a3 < 0 && age >= 3) a3 = turn;
            if (a4 < 0 && age >= 4) a4 = turn;
            if (levy < 0)
                for (int i = 0; i < w.TaxPolicies.Count; i++)
                    if (w.TaxPolicies[i].Polity == Ai && w.TaxPolicies[i].Rate > 0.0) { levy = turn; break; }
            if (turn == 600)
            {
                int found = 0;
                foreach (DepositRow d in hidden)
                    if (EmpireQuery.ControlsSettlement(w, Ai, d.Settlement) && DepositConcealment.KnownAbundance(w, cfg, d.Settlement, d.Good.Value) > 0.0) found++;
                purseAtGranary += $" | {s100}/{s300}/{s600} | {Assays(log)} | {found} of {hidden.Count}";
            }
        }
        string tail = purseAtGranary.Contains('|') ? purseAtGranary : purseAtGranary + " | ? | ? | ?";
        return $"{firstOrder} / {firstBuilt} | {firstProspect} | {purse1} / {purse10} / {purse20} / {tail} | {a2} | {a3} | {levy} | {a4}";
    }

    private static long Sum(IReadOnlyWorldState w, GoodId g)
    {
        long n = 0;
        for (int i = 0; i < w.GoodStocks.Count; i++)
            if (w.GoodStocks[i].Good == g && EmpireQuery.ControlsSettlement(w, Ai, w.GoodStocks[i].Settlement)) n += w.GoodStocks[i].Amount.Value;
        return n;
    }

    private static int Surveyed(IReadOnlyWorldState w)
    {
        int n = 0;
        for (int i = 0; i < w.SettlementSurveys.Count; i++) if (w.SettlementSurveys[i].Polity == Ai) n++;
        return n;
    }

    private static int Assays(OrderLog log)
    {
        int n = 0;
        for (int i = 0; i < log.Count; i++) if (log[i].ActorId == Ai.Value && log[i].Kind == OrderKind.Prospect && log[i].Amount == 1.0) n++;
        return n;
    }
}
