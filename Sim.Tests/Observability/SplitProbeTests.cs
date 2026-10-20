using Sim.Core;
using Sim.Core.Kernel;
using Sim.Core.Observability;
using Sim.Core.State;
using Sim.Core.Systems;
using Sim.Core.Systems.Research;
using Sim.Tests.Systems;
using Sim.Tests.TestUtil;
using static Sim.Tests.Systems.IncrementalConstructionTests;

namespace Sim.Tests.Observability;

/// <summary>
/// D-055 / ADR-040 (batch 4, item 3) — THE SPLIT REPORT'S OBSERVER, in process (<see cref="SplitProbe"/>). On a rig whose
/// every figure is worked by hand it reports the split, the set-asides and the heads exactly as the step resolved them (the
/// per-row properties P1–P3 hold on its LIVE output); it changes no state (per-turn hashes and the save are the same with
/// and without it); and it refuses a toy world and a configuration without the split, and throws when the pipeline lacks
/// the construction or the road development phase.
///
/// THE RIG (ResourceAllocationTests' rig, by hand): the founded dev world (four settlements, all the player's; stone and
/// timber 0, housing's draw 0, a flat 10-year turn); one candidate route S0–S1 whose dirt-path row is 60 km (the trackway
/// costs exactly 60 timber); 100 timber at S2 and 1,000 stone at S3; two granary heads (40 timber, 20 stone each),
/// S0's first in the building order — and S0 has NO BUILDERS, so it cannot advance and SETS ASIDE what it needs. The player
/// sets b = 30 at turn 0 and orders roads (100 %) at turns 0 and 1. Only construction and road development run.
/// </summary>
public class SplitProbeTests
{
    private static readonly SimConfig Shipped = Cfg;
    private static readonly ResearchContent Research = TestConfigs.Research();

    private static WorldState Rig()
    {
        WorldState w = Solo();
        w.TransportEdges.Clear();
        w.TransportEdges.Add(new TransportEdgeRow(1, S0, S1, EdgeTypes.DirtPath, TransportModes.Road, TransportEdgeStates.Complete,
            100_000, 60, 0, 0, 0, EdgeTypes.DirtPath, 0.0, 1.0));
        w.SettlementDistances.Clear();
        w.SettlementDistances.Add(new SettlementDistanceRow(S0, S1, 100.0));
        w.SettlementDistances.Add(new SettlementDistanceRow(S1, S0, 100.0));
        w.TradeFlows.Clear();
        w.ResearchCompleted.Add(new ResearchCompletedRow(Player, Research.Nodes[Research.IndexOfId("track_road")].Key));
        for (int s = 0; s < w.Settlements.Count; s++) AllBuilders(w, w.Settlements[s].Id);
        NoBuilders(w, S0);
        Set(w, S2, Timber, 100);
        Set(w, S3, Stone, 1000);
        foreach (SettlementId s in new[] { S0, S1 })
        {
            w.ConstructionQueue.Add(new ConstructionQueueRow(s, 0, Granary));
            w.ConstructionProgress.Add(new ConstructionProgressRow(s, 0, Granary, 0, 0));
        }
        return w;
    }

    private static OrderLog Orders() => Log(
        new OrderRecord(0, Player.Value, OrderKind.SetResourceAllocation, Player.Value, 30),
        OrderRecord.From(0, Player, OrderKind.DevelopRoads, 0, 100.0),
        OrderRecord.From(1, Player, OrderKind.DevelopRoads, 0, 100.0));

    private static TurnExecutor Executor(SimConfig cfg, OrderLog log, params Func<SimConfig, SystemRegistration>[] systems)
    {
        var pipeline = systems.Length == 0
            ? new[] { SystemCatalog.Construction(cfg), SystemCatalog.RoadDevelopment(cfg) }
            : systems.Select(f => f(cfg)).ToArray();
        return new TurnExecutor(ResearchRigs.FlatEra(10.0), pipeline, log);
    }

    /// <summary>Steps <paramref name="turns"/> times through the probe; returns the worlds and the report's rows.</summary>
    private static (WorldState[] Worlds, SplitReportRow[] Rows) Observed(int turns)
    {
        OrderLog log = Orders();
        TurnExecutor exec = Executor(Shipped, log);
        using var report = new MemoryStream();
        var probe = new SplitProbe(report, Shipped);
        var worlds = new WorldState[turns + 1];
        worlds[0] = Rig();
        for (int t = 1; t <= turns; t++) worlds[t] = probe.Step(exec, worlds[t - 1], log.BatchFor(worlds[t - 1].Clock.Turn));
        Assert.Equal(turns, probe.RowsWritten);
        return (worlds, SplitReport.Parse(System.Text.Encoding.UTF8.GetString(report.ToArray())));
    }

    // ================================================================== what it reports

    [Fact]
    public void OnARigWorkedByHand_TheReportIsTheStepsOwnSplitSetAsidesAndHeads()
    {
        (WorldState[] w, SplitReportRow[] rows) = Observed(2);
        Assert.Equal(2, rows.Length);
        SplitReportRow r0 = rows[0];
        Assert.Equal((0L, 1L, 1, 30, 100.0, false), (r0.Turn, r0.To, r0.Polity, r0.BuildingsPercent, r0.RoadPercent, r0.Guarded));

        // Timber, step 0->1: S 100, E_b 30, E_r 70; the claim D_b = 80 (S0 waits and sets aside 40, S1 would draw 40); the road
        // order's D_r = 60; cap = 30 + (70 - 60) = 40 = G_b < D_b. S0 sets the whole grant aside, S1 draws nothing, road works
        // find 100 - 0 - 40 = 60 = min(D_r, E_r) and pay 60.
        SplitGoodFigures timber = r0.GoodOf(Timber)!;
        Assert.Equal((100L, (long?)40L, 30L, 70L, 80L, 60L, 40L, 40L, true),
            (timber.S, timber.Start, timber.Eb, timber.Er, timber.Db, timber.Dr, timber.Cap, timber.Gb, timber.HoldsBack));
        Assert.Equal((0L, 0L, 40L, 40L, 60L, 60L),
            (timber.ForecastBuild, timber.Build, timber.ForecastHolds, timber.Holds, timber.ForecastRoad, timber.Road));
        // The published row is the report's holds (section 61, purse 1).
        Assert.Contains(new ConstructionHoldRow(1, Timber, 40), w[1].ConstructionHolds.Rows());
        Assert.Equal(60L, TotalOf(w[0], Timber) - TotalOf(w[1], Timber));    // the state itself: building 0 + roads 60

        // The heads, in the building order: S0 (no builders: Labour) sets aside its need; S1 is held for it.
        Assert.Equal(2, r0.Heads.Length);
        SplitHeadFigures s0 = r0.Heads[0], s1 = r0.Heads[1];
        Assert.Equal((0, 0, Granary, 0L, 0L, 0L, "None", "Labour"), (s0.Settlement, s0.Slot, s0.Project, s0.Enqueued, s0.Before, s0.After, s0.Release, s0.Limit));
        Assert.Equal((40L, 0L, 40L), (s0.GoodOf(Timber)!.Need, s0.GoodOf(Timber)!.Draw, s0.GoodOf(Timber)!.Hold));
        Assert.Equal((1, 0L, 0L), (s1.Settlement, s1.Before, s1.After));
        SplitHeadGood held = s1.GoodOf(Timber)!;
        Assert.Equal(("HeldForEarlier", new SplitHeadRef(0, 0, Granary), 40L, 40L, 0L, 0L, (long?)0L),
            (held.Cause, held.Holder, held.SetAsideAhead, held.Need, held.Draw, held.Hold, held.Budget));
        Assert.True(SplitReportCheck.HeldBecauseOfAHold(r0, 1, held), "S1's draw fell because of S0's set-aside");
        // The cause and holder belong to the BINDING good only: S1's stone, which it sets aside, names none.
        SplitHeadGood stone = s1.GoodOf(Stone)!;
        Assert.Equal(("None", (SplitHeadRef?)null, 0L, 20L, 20L, 0L), (stone.Cause, stone.Holder, stone.SetAsideAhead, stone.Need, stone.Hold, stone.Draw));

        // Every row of the live report satisfies the per-row properties.
        SplitCheckResult check = SplitReportCheck.Check(rows);
        foreach (string p in new[] { "P1", "P2", "P3", "P11", "P12" }) Assert.False(check.Failed(p), string.Join(" | ", check.Failures));
        Assert.False(check.Failed("P10"));
    }

    [Fact]
    public void TheReportIsTheSameWhenWrittenTwice()
    {
        (_, SplitReportRow[] a) = Observed(2);
        (_, SplitReportRow[] b) = Observed(2);
        using var x = new MemoryStream();
        using var y = new MemoryStream();
        foreach (SplitReportRow r in a) SplitReport.Write(x, r);
        foreach (SplitReportRow r in b) SplitReport.Write(y, r);
        Assert.Equal(x.ToArray(), y.ToArray());
    }

    // ================================================================== the actuals are measured

    /// <summary>The rows <see cref="SplitProbe.Rows"/> writes for the rig's step 0 -> 1 from the step's own inputs (PREV, its
    /// batch, its dt, the stocks before construction — so the forecast is the live one) and the given tables after
    /// construction (also road development's "before": it runs next), the published holds and the stocks after road
    /// development.</summary>
    private static SplitReportRow RowOf(WorldState prev, WorldState next, IReadOnlyTable<GoodStockRow> afterBuild,
        IReadOnlyTable<ConstructionHoldRow> holds, IReadOnlyTable<GoodStockRow> afterRoad) =>
        Assert.Single(SplitProbe.Rows(prev, Shipped, RoadDevelopmentQuery.Orders(Orders().BatchFor(0)), next.Clock.DtYears,
            prev.GoodStocks, afterBuild, holds, afterBuild, afterRoad));

    private static byte[] Bytes(SplitReportRow row)
    {
        using var ms = new MemoryStream();
        SplitReport.Write(ms, row);
        return ms.ToArray();
    }

    private static WorldState WithTimberAtS2(WorldState w, long fall)
    {
        WorldState copy = w.Clone();
        Set(copy, S2, Timber, Of(w, S2, Timber) - fall);
        return copy;
    }

    /// <summary>
    /// THE ACTUALS ARE MEASURED, NOT COPIED FROM THE FORECAST (fixer round, finding T3-L1-T3-V1-F4). In a correct run the step
    /// does what the rule forecast, so no live report can tell a measured column from the forecast (mutant CM13 — Build = the
    /// forecast — survived every test). Here <see cref="SplitProbe.Rows"/> gets the step's own inputs but ONE table that
    /// differs from what the walk forecast: building work's purse fell by 7 timber where the rule drew none; construction
    /// published 33 timber set aside where it forecast 40; road works' purse fell by 50 where they were forecast 60. That
    /// column reports its table, every forecast column is unchanged, and split-check's P1 names the divergence.
    /// </summary>
    [Theory]
    [InlineData("build")]
    [InlineData("holds")]
    [InlineData("road")]
    public void TheActualsAreMeasuredFromTheStockAndHoldTables_NotTheForecast_AndP1NamesADivergence(string column)
    {
        (WorldState[] w, SplitReportRow[] live) = Observed(1);
        WorldState prev = w[0], next = w[1];
        // Premise: in step 0 -> 1 building work draws nothing (S0 has no builders, S1 is held for it) and road works pay 60
        // timber from S2's 100, so the honest tables are PREV's stocks after construction, the published holds and NEXT's
        // stocks after road development — and fed them, Rows writes the live row byte for byte.
        SplitGoodFigures honest = live[0].GoodOf(Timber)!;
        Assert.Equal((0L, 0L, 40L, 40L, 60L, 60L), (honest.ForecastBuild, honest.Build, honest.ForecastHolds, honest.Holds, honest.ForecastRoad, honest.Road));
        Assert.Equal((0L, 100L, 40L), (live[0].GoodOf(Stone)!.Build, Of(prev, S2, Timber), Of(next, S2, Timber)));
        Assert.Equal(Bytes(live[0]), Bytes(RowOf(prev, next, prev.GoodStocks, next.ConstructionHolds, next.GoodStocks)));

        var fewerHeld = new Table<ConstructionHoldRow>();
        foreach (ConstructionHoldRow h in next.ConstructionHolds.Rows()) fewerHeld.Add(h.Good == Timber ? h with { Units = h.Units - 7 } : h);
        WorldState built = WithTimberAtS2(prev, 7);
        (SplitReportRow row, long build, long holds, long road, string p1) = column switch
        {
            "build" => (RowOf(prev, next, built.GoodStocks, next.ConstructionHolds, WithTimberAtS2(built, 60).GoodStocks), 7L, 40L, 60L,
                "building work drew 7 but the split forecast 0"),
            "holds" => (RowOf(prev, next, prev.GoodStocks, fewerHeld, next.GoodStocks), 0L, 33L, 60L,
                "33 were set aside but the split forecast 40"),
            "road" => (RowOf(prev, next, prev.GoodStocks, next.ConstructionHolds, WithTimberAtS2(prev, 50).GoodStocks), 0L, 40L, 50L,
                "road works drew 50 but the split forecast 60"),
            _ => throw new ArgumentOutOfRangeException(nameof(column)),
        };
        SplitGoodFigures timber = row.GoodOf(Timber)!;
        Assert.Equal((build, holds, road), (timber.Build, timber.Holds, timber.Road));
        Assert.Equal((0L, 40L, 60L), (timber.ForecastBuild, timber.ForecastHolds, timber.ForecastRoad));
        // Only the measured column moved: the rest of the timber figures, the other goods and the heads are the live row's.
        Assert.Equal(honest, timber with { Build = honest.Build, Holds = honest.Holds, Road = honest.Road });
        Assert.Equal(Bytes(live[0]), Bytes(row with { Goods = row.Goods.Select(g => g.Id == Timber ? honest : g).ToArray() }));
        Assert.Equal($"P1: step 0->1 (polity 1) timber: {p1}",
            SplitReportCheck.Check([row]).Failures.Single(f => f.StartsWith("P1:", StringComparison.Ordinal)));
        Assert.False(SplitReportCheck.Check([live[0]]).Failed("P1"));
    }

    // ================================================================== it changes nothing

    [Fact]
    public void TheProbeChangesNoState_PerTurnHashesAndTheSaveAreTheSameWithAndWithoutIt()
    {
        (WorldState[] observed, _) = Observed(3);
        OrderLog log = Orders();
        TurnExecutor plain = Executor(Shipped, log);
        WorldState w = Rig();
        for (int t = 1; t <= 3; t++)
        {
            w = plain.Step(w);
            Assert.Equal(WorldHash.ComputeHex(w), WorldHash.ComputeHex(observed[t]));
        }
        using var a = new MemoryStream();
        using var b = new MemoryStream();
        Snapshot.Save(w, a);
        Snapshot.Save(observed[3], b);
        Assert.Equal(a.ToArray(), b.ToArray());
        Assert.NotEqual(WorldHash.ComputeHex(observed[0]), WorldHash.ComputeHex(observed[1]));   // anti-vacuity: the steps did work
    }

    // ================================================================== refusals

    [Fact]
    public void ItRefusesAWorldThatWasNotFounded_AndAConfigurationWithoutTheSplit()
    {
        var probe = new SplitProbe(new MemoryStream(), Shipped);
        var toy = new WorldState(7);
        InvalidOperationException e = Assert.Throws<InvalidOperationException>(() => probe.Begin(toy, OrderBatch.Empty));
        Assert.Contains("split report: the world was not founded (it has no terrain", e.Message);

        InvalidOperationException off = Assert.Throws<InvalidOperationException>(
            () => new SplitProbe(new MemoryStream(), TestConfigs.PreAllocationRatios(Shipped)));
        Assert.Contains("the building/roads split is off in this configuration", off.Message);
    }

    [Fact]
    public void ItRefusesAConfigurationWithTheHoldRuleOff()
    {
        // Fixer round (finding T3-L2-T3-V2-F2): split-check's P2 (build + holds = G_b) is a property of the D-055 walk; a run
        // without set-asides is not one, so the probe writes no report of it. The split itself is on here.
        SimConfig off = TestConfigs.PreReservations(Shipped);
        Assert.True(ResourceAllocation.Enabled(off), "premise: only the hold rule is off");
        Assert.False(Reservations.Enabled(off));
        InvalidOperationException e = Assert.Throws<InvalidOperationException>(() => new SplitProbe(new MemoryStream(), off));
        Assert.Contains("split report: the D-055 hold rule is off in this configuration (construction.reservations)", e.Message);
    }

    [Theory]
    [InlineData("roaddevelopment")]
    [InlineData("construction")]
    public void ItThrows_WhenTheStepRanNoConstructionOrNoRoadDevelopmentPhase(string missing)
    {
        OrderLog log = Orders();
        TurnExecutor exec = missing == "roaddevelopment"
            ? Executor(Shipped, log, SystemCatalog.Construction)
            : Executor(Shipped, log, SystemCatalog.RoadDevelopment);
        var probe = new SplitProbe(new MemoryStream(), Shipped);
        InvalidOperationException e = Assert.Throws<InvalidOperationException>(() => probe.Step(exec, Rig(), log.BatchFor(0)));
        Assert.Contains($"split report: the step from turn 0 ran no '{missing}' phase", e.Message);
    }

    [Fact]
    public void ItThrows_WhenThePipelineRunsAPhaseTwice()
    {
        OrderLog log = Orders();
        TurnExecutor exec = Executor(Shipped, log, SystemCatalog.Construction, SystemCatalog.Construction, SystemCatalog.RoadDevelopment);
        var probe = new SplitProbe(new MemoryStream(), Shipped);
        InvalidOperationException e = Assert.Throws<InvalidOperationException>(() => probe.Step(exec, Rig(), log.BatchFor(0)));
        Assert.Contains("split report: the pipeline runs a 'construction' phase twice", e.Message);
    }
}
