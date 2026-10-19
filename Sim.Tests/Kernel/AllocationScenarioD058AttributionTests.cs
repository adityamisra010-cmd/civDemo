using Sim.Core.Observability;
using Sim.Core.State;
using static Sim.Tests.Kernel.AllocationScenario;

namespace Sim.Tests.Kernel;

/// <summary>
/// D-058 (2026-10-19; the Director's ruling "build one at a time … 7 built, 1 under construction, 1 not started") — THE
/// ATTRIBUTION OF EVERY S42-ALLOC-H PIN THE RULING MOVED. The scenario is run twice in process from the same founding,
/// orders and executor: as shipped, and under TestConfigs.PreContinueWithinTurn (sim.json construction.continueWithinTurn
/// off, a single-layer twin — <see cref="AllocationScenarioPreD058Run"/>). On the twin every batch-4 literal that
/// AllocationScenarioTests and AllocationCiLegTests re-pinned holds exactly as D-055 pinned it; the two runs are hash-identical
/// through world 118 (the control through world 117), and the first difference is a construction completion whose leftover
/// capacity the batch-4 rule would have left for the next step.
/// </summary>
[Collection(AllocationScenarioCollection.Name)]
public class AllocationScenarioD058AttributionTests(AllocationScenarioRun run, AllocationScenarioPreD058Run twinHolder)
    : IClassFixture<AllocationScenarioPreD058Run>
{
    private readonly AllocationScenarioRun twin = twinHolder.Run;

    private static string Text(SplitHeadFigures h)
    {
        SplitHeadGood s = h.GoodOf(Stone)!;
        string holder = s.Holder is { } x ? $"{x.Settlement}:{x.Slot}" : "-";
        return $"{h.Settlement}:{h.Slot} e{h.Enqueued} {h.Before}->{h.After} {h.Limit} | {s.Draw} {s.Hold} {s.Cause} {holder}";
    }

    [Fact]
    public void TheRunsAgreeThroughWorld118_AndTheFirstDifferenceIsACompletionsLeftoverCarriedToTheNextEntry()
    {
        Assert.False(Sim.Core.State.ConstructionQuery.ContinuesWithinTurn(twin.Cfg));
        Assert.True(Sim.Core.State.ConstructionQuery.ContinuesWithinTurn(run.Cfg));
        for (int t = 0; t <= 118; t++) Assert.Equal(twin.Hashes[t], run.Hashes[t]);
        Assert.NotEqual(twin.Hashes[119], run.Hashes[119]);
        // The step 118 -> 119: settlement 0's first granary (ordered at 117) completes on its last stone in BOTH runs; in the
        // shipped run its leftover capacity carries settlement 0's slot 1 — key (117, 0, 1), ahead of settlement 2's head
        // (117, 2, 0) — into the same step, where it takes the 17 stone the batch-4 rule gave settlement 2's head.
        Assert.Equal(["0:0 e117 950000->1000000 None | 1 0 None -", "2:0 e117 0->850000 Materials | 17 0 HeldForRoads -"],
            twin.Row(118).Heads.Select(Text).ToArray());
        Assert.Equal(["0:0 e117 950000->1000000 None | 1 0 None -", "0:1 e117 0->850000 Materials | 17 0 HeldForRoads -",
                      "2:0 e117 0->0 Materials | 0 0 HeldForRoads -"], run.Row(118).Heads.Select(Text).ToArray());
        // The control (b = 100 from world P): identical through world 117, different from 118 — in step 117 -> 118 settlement
        // 0's continuations take the whole purse (64), where the batch-4 rule built one granary (40) and left road works 24.
        for (int t = 1; t <= 117 - (int)P; t++) Assert.Equal(twin.ControlHashes[t], run.ControlHashes[t]);
        Assert.NotEqual(twin.ControlHashes[118 - P], run.ControlHashes[118 - P]);
    }

    [Fact]
    public void OnTheTwin_TheBatch4StoneTableAndHeadsHold()
    {
        Assert.Equal(AllocationScenarioTests.StoneBatch4.Length, twin.Rows.Length);
        for (int i = 0; i < twin.Rows.Length; i++)
        {
            var e = AllocationScenarioTests.StoneBatch4[i];
            SplitReportRow r = twin.Rows[i];
            SplitGoodFigures s = r.GoodOf(Stone)!;
            Assert.Equal((e.Turn, e.B, e.S, e.Eb, e.Er, e.Db, e.Dr, e.Cap, e.Gb, e.Build, e.Holds, e.Road),
                (r.Turn, r.BuildingsPercent, s.S, s.Eb, s.Er, s.Db, s.Dr, s.Cap, s.Gb, s.Build, s.Holds, s.Road));
        }
        foreach ((long turn, string[] heads) in AllocationScenarioTests.HeadsBatch4)
            Assert.Equal(heads, twin.Row(turn).Heads.Select(Text).ToArray());
    }

    [Fact]
    public void OnTheTwin_TheBatch4WorldsControlAndLegLiteralsHold()
    {
        int purse = Reservations.PolityPurse(new PolityId(Player));
        Assert.Equal([new ConstructionHoldRow(purse, Timber, 51), new ConstructionHoldRow(purse, Stone, 11)], All(twin.World(130).ConstructionHolds));
        Assert.Equal([new ConstructionHoldRow(purse, Timber, 40), new ConstructionHoldRow(purse, Stone, 18)], All(twin.World(133).ConstructionHolds));
        string[] Progress(WorldState w) => All(w.ConstructionProgress).Select(r => $"{r.Settlement.Value}:{r.Slot} {r.EnqueuedTurn} {r.Progress}").ToArray();
        Assert.Equal(["4:0 126 422236", "0:1 128 0", "2:0 128 300000", "2:1 128 0"], Progress(twin.World(130)));
        Assert.Equal(["4:0 126 422236", "2:1 128 0"], Progress(twin.World(132)));
        Assert.Equal((5L, 4L, 0L), (ConstructionQuery.Built(twin.World(Z + 1), new SettlementId(0), Granary),
            ConstructionQuery.Built(twin.World(Z + 1), new SettlementId(2), Granary), ConstructionQuery.Built(twin.World(Z + 1), new SettlementId(Third), Granary)));

        (long Turn, long S, long Db, long Gb, long Build, long Holds, long Road)[] control =
            [(117, 64, 40, 40, 40, 0, 24), (118, 61, 40, 40, 40, 0, 21), (119, 66, 40, 40, 40, 0, 26), (128, 81, 51, 51, 40, 11, 30)];
        foreach (var e in control)
        {
            SplitGoodFigures s = twin.ControlRow(e.Turn).GoodOf(Stone)!;
            Assert.Equal(e, (e.Turn, s.S, s.Db, s.Gb, s.Build, s.Holds, s.Road));
        }

        // AllocationCiLegTests' batch-4 literals: X, the binding list, Y at 120, Z, W and V.
        SplitGoodFigures x = twin.Row(X).GoodOf(Stone)!;
        Assert.Equal((64L, 19L, 40L, 19L, 0L), (x.S, x.Gb, x.Db, x.Build, x.Holds));
        long[] binding = twin.Rows.Where(r => r.BuildingsPercent is > 0 and < 100 && r.RoadPercent is not null
            && r.Goods.Any(g => g.Build + g.Holds == g.Gb && g.Gb < g.Db)).Select(r => r.Turn).ToArray();
        Assert.Equal(new long[] { 117, 118, 119, 121, 122, 128, 129, 130, 132, 136 }, binding);
        SplitGoodFigures y = twin.Row(120).GoodOf(Stone)!;
        Assert.Equal((63L, 18L, 45L, 4L, 58L), (y.S, y.Eb, y.Er, y.Db, y.Road));
        SplitGoodFigures z = twin.Row(Z).GoodOf(Stone)!;
        Assert.Equal((84L, 25L, 59L, 31L, 12L, 72L, 31L), (z.S, z.Eb, z.Er, z.Db, z.Dr, z.Cap, z.Build + z.Holds));
        SplitGoodFigures w = twin.Row(W).GoodOf(Stone)!;
        Assert.Equal((81L, 24L, 57L, 51L, 13L, 11L, 57L), (w.S, w.Eb, w.Er, w.Db, w.Build, w.Holds, w.Road));
        SplitGoodFigures cw = twin.ControlRow(W).GoodOf(Stone)!;
        Assert.Equal((40L, 11L), (cw.Build, cw.Holds));
        Assert.DoesNotContain(twin.ControlRow(W).Heads, h => h.Goods.Any(g => g.Cause == SplitReportCheck.HeldForEarlier));
        Assert.Equal(new[] { (2, 128L), (Third, V) }, twin.Row(V).Heads.Select(h => (h.Settlement, h.Enqueued)).ToArray());
        Assert.Equal(((20L, 0L), (0L, 18L)), ((twin.Row(V).Heads[0].GoodOf(Stone)!.Draw, twin.Row(V).Heads[0].GoodOf(Stone)!.Hold),
            (twin.Row(V).Heads[1].GoodOf(Stone)!.Draw, twin.Row(V).Heads[1].GoodOf(Stone)!.Hold)));
        SplitGoodFigures c = twin.ControlRow(X).GoodOf(Stone)!;
        Assert.Equal((64L, 40L, 0L, 24L), (c.S, c.Build, c.Holds, c.Road));
    }
}
