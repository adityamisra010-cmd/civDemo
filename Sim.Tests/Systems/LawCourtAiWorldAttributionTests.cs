using Sim.Core;
using Sim.Core.Kernel;
using Sim.Core.State;
using Sim.Core.Systems;
using Sim.Core.Worldgen;
using Sim.Tests.TestUtil;
using Xunit;

namespace Sim.Tests.Systems;

/// <summary>
/// ADR-057 §6 — THE LAW COURT'S ATTRIBUTION IN THE SHIPPED AI WORLD (canonical worldgen, one AI Empire, seed 42), against
/// its switch-off twin (TestConfigs.PreLawCourt). MEASURED (2026-10-28, this tree): the AI completes law_code at turn 506,
/// orders a court the same turn (stamp 506, through AiConstructionPolicy — the player's predicate and constructor), the
/// building stands in the delivery step and InstitutionsSystem founds it the step after. The twin and the live world are
/// byte-identical for every state before the first court order's delivery and differ from exactly that state on — the
/// court is the whole of the difference, and no base pin (all at turn 300) can see it. A long test (~510 turns × 2 worlds,
/// about 3.5 minutes): it is this track's golden-attribution control.
/// </summary>
public sealed class LawCourtAiWorldAttributionTests
{
    [Fact]
    public void AiWorldSeed42_IdenticalToTheTwinUntilTheFirstCourtOrdersDelivery_ThenFoundedTheStepAfter()
    {
        SimConfig on = TestConfigs.Sim();
        SimConfig off = TestConfigs.PreLawCourt(on);
        WorldgenConfig wg = TestConfigs.Worldgen();
        var logOn = new OrderLog();
        var logOff = new OrderLog();
        TurnExecutor eOn = Exec(on, wg, logOn), eOff = Exec(off, wg, logOff);
        WorldState a = WorldFounding.Found(wg with { AiEmpires = 1 }, on, 42), b = WorldFounding.Found(wg with { AiEmpires = 1 }, off, 42);
        long stamp = -1;
        var ai = new PolityId(2);
        for (int t = 1; t <= 520 && (stamp < 0 || a.Clock.Turn < stamp + 2); t++)
        {
            AiOrders.Append(logOn, a, on, wg);
            AiOrders.Append(logOff, b, off, wg);
            if (stamp < 0)
                for (int i = 0; i < logOn.Count; i++)
                    if (logOn[i].Kind == OrderKind.EnqueueConstruction && logOn[i].Amount == 16) { stamp = logOn[i].Turn; break; }
            if (stamp < 0 || a.Clock.Turn <= stamp)
                Assert.True(WorldHash.ComputeHex(a) == WorldHash.ComputeHex(b), $"the worlds differ at state {a.Clock.Turn}, before the first court order's delivery");
            a = eOn.Step(a);
            b = eOff.Step(b);
            if (stamp >= 0 && a.Clock.Turn == stamp + 1)
                Assert.NotEqual(WorldHash.ComputeHex(a), WorldHash.ComputeHex(b));
        }
        // M6 INTEGRATION RE-PIN (2026-10-28; MEASURED on the integrated tree): 506 -> 507. CAUSE: D-066 A18 (the AI's known-terrain
        // valuation moves its foundings, and the research trajectory with them); KNOW off still gives 507. CONTROL:
        // FirstCourtOrder_WithTheKnownTerrainValuationOff_IsStampedAt506 returns CIV's measured 506.
        Assert.Equal(507, stamp);
        Assert.True(ResearchQuery.IsCompleted(a, ai, on.Research!.Nodes[on.Research.IndexOfId("law_code")].Key));
        InstitutionRow court = Assert.Single(Rows(a), r => r.Type == 6);
        Assert.Equal((ai, stamp + 2, 0.0), (court.Polity, court.FoundedTurn, court.Maturity));
        Assert.Equal("law-giving chiefdom", RegimeQuery.Label(a, on, ai));
        Assert.DoesNotContain(Rows(b), r => r.Type == 6);
    }

    /// <summary>M6 integration — the attribution control of the 506 -> 507 re-pin above: with ONLY D-066 A18 off
    /// (TestConfigs.PreAiKnownTerrain) the AI's first court order is stamped at 506, as CIV measured on its own tree.</summary>
    [Fact]
    public void FirstCourtOrder_WithTheKnownTerrainValuationOff_IsStampedAt506()
    {
        SimConfig cfg = TestConfigs.PreAiKnownTerrain(TestConfigs.Sim());
        WorldgenConfig wg = TestConfigs.Worldgen();
        var log = new OrderLog();
        TurnExecutor e = Exec(cfg, wg, log);
        WorldState a = WorldFounding.Found(wg with { AiEmpires = 1 }, cfg, 42);
        long stamp = -1;
        for (int t = 1; t <= 520 && stamp < 0; t++)
        {
            AiOrders.Append(log, a, cfg, wg);
            for (int i = 0; i < log.Count && stamp < 0; i++)
                if (log[i].Kind == OrderKind.EnqueueConstruction && log[i].Amount == 16) stamp = log[i].Turn;
            if (stamp < 0) a = e.Step(a);
        }
        Assert.Equal(506, stamp);
    }

    private static InstitutionRow[] Rows(IReadOnlyWorldState w)
    {
        var r = new InstitutionRow[w.Institutions.Count];
        for (int i = 0; i < r.Length; i++) r[i] = w.Institutions[i];
        return r;
    }

    private static TurnExecutor Exec(SimConfig cfg, WorldgenConfig wg, OrderLog log)
    {
        using var era = Sim.Data.DataFiles.OpenEraPacing();
        using var pipe = Sim.Data.DataFiles.OpenPipeline();
        return new TurnExecutor(EraTableLoader.Load(era), PipelineLoader.Load(pipe, SystemCatalog.All(cfg, wg)), log);
    }
}
