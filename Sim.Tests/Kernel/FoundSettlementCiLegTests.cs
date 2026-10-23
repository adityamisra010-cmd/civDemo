using Sim.Core;
using Sim.Core.Kernel;
using Sim.Core.State;
using Sim.Core.Systems;
using Sim.Core.Worldgen;
using Sim.Tests.TestUtil;
using Xunit;

namespace Sim.Tests.Kernel;

/// <summary>
/// M5 S1b (ADR-036) — the ci.yml determinism-xproc FOUND SETTLEMENT leg is only as good as its scripted log: two
/// processes and a replay agreeing on a run whose orders were refused would prove nothing about kind 9. This test
/// pins the literals `scripts/gen-sample-orders.py --found` writes, and proves on the same founded world the CLI
/// runs (production worldgen, seed 42, the production pipeline with the worldgen siting attached) that each order is
/// LEGAL when it is delivered and founds in the next state — including the second, whose source is the colony the
/// first founded (above the turn-0 maximum: the ADR-034 deferral).
/// </summary>
public class FoundSettlementCiLegTests
{
    /// <remarks>Batch 3 WS3 (D-054, W1 fix F5): re-derived on the CYLINDRICAL seed-42 world — the nearest Legal site to
    /// each source settlement (squared px on the cylinder, then cell id) at its turn. The pre-cylinder literals 330985 and
    /// 322801 are PoorSite on the cylinder (MEASURED: "Expected: Legal, Actual: PoorSite"), so the CI leg's orders would
    /// be refused and its two processes would agree on a run that founds nothing.</remarks>
    private static readonly OrderRecord[] Scripted =
    [
        new(2, 1, OrderKind.FoundSettlement, 0, 335063.0),
        new(5, 1, OrderKind.FoundSettlement, 12, 326873.0),
    ];

    // M6 Track EX (ADR-044, 2026-10-22; re-derived on the merged M6 tree): under the fog a site must be KNOWN to the issuer
    // (MEASURED: the old turn-2 site is UnknownSite), so the log explores from settlement 0 at turn 0 (OrderKind 14; radius
    // 3 -> 6 nodes) and the sites are the nearest legal KNOWN sites at their turns: 335063 from settlement 0 at turn 2, 326873
    // from colony 12 at turn 5 (its seed disc and the explored disc hold it).
    private static readonly OrderRecord[] Explore = [new(0, 1, OrderKind.Explore, 0, 0.0)];

    [Fact]
    public void TheScriptLiterals_AreTheOrdersPinnedHere()
    {
        string root = RepoRoot();
        string script = File.ReadAllText(Path.Combine(root, "scripts", "gen-sample-orders.py"));
        Assert.Contains("records = [(0, 1, 14, 0, 0.0), (2, 1, 9, 0, 335063.0), (5, 1, 9, 12, 326873.0)]", script);
        string ci = File.ReadAllText(Path.Combine(root, ".github", "workflows", "ci.yml"));
        Assert.Contains("gen-sample-orders.py found-orders.bin 300 --found", ci);
    }

    [Fact]
    public void EachScriptedOrder_IsLegalWhenDelivered_AndFoundsInTheNextState()
    {
        SimConfig cfg = TestConfigs.Sim();   // M6 EX: the CLI's shipped rules, the fog on (not the founding rigs' PreDiscovery)
        var log = new OrderLog();
        foreach (OrderRecord r in Explore) log.Append(r);
        foreach (OrderRecord r in Scripted) log.Append(r);
        WorldState w = WorldFounding.Found(TestConfigs.Worldgen(), cfg, 42UL);
        OrderValidation.ValidateAgainstWorld(log, w);                    // what `sim run --orders` does first
        WorldState turnZero = w.Clone();
        SettlementSiting.FrontierSiting f = FoundingRigs.Frontier(w, cfg);
        TurnExecutor exec = FoundingRigs.Pipeline(cfg, log);
        int colonies = 0;
        for (long t = 0; t <= Scripted[^1].Turn; t++)
        {
            OrderValidation.ValidateAtDelivery(log.BatchFor(t), turnZero, w);
            foreach (OrderRecord r in Scripted)
            {
                if (r.Turn != t) continue;
                Assert.Equal(FoundingVerdict.Legal,
                    FoundingQuery.Check(w, cfg, f, r.Actor, new SettlementId(r.TargetId), FoundingQuery.SiteCellOf(r)));
            }
            int before = w.Settlements.Count;
            w = exec.Step(w);
            foreach (OrderRecord r in Scripted)
            {
                if (r.Turn != t) continue;
                Assert.Equal(before + 1, w.Settlements.Count);
                Assert.Equal((int)r.Amount, w.Settlements[^1].SiteCell);
                Assert.Equal(t + 1, w.Settlements[^1].FoundedTurn);
                colonies++;
            }
        }
        Assert.Equal(2, colonies);
        Assert.Equal(12, Scripted[1].TargetId);
        Assert.True(Scripted[1].TargetId > FoundingRigs.MaxSettlementId(turnZero));   // the ADR-034 case
    }

    private static string RepoRoot()
    {
        var dir = new DirectoryInfo(AppContext.BaseDirectory);
        while (dir is not null && !Directory.Exists(Path.Combine(dir.FullName, ".github")))
            dir = dir.Parent;
        Assert.NotNull(dir);
        return dir!.FullName;
    }
}
