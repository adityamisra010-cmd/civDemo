using Sim.Core.Kernel;
using Sim.Core.Observability;
using Sim.Core.State;
using Sim.Tests.TestUtil;
using static Sim.Tests.Kernel.AllocationScenario;

namespace Sim.Tests.Kernel;

/// <summary>
/// D-055 / ADR-040 (batch 4, item 3) — THE SCENARIO S42-ALLOC-H IN PROCESS, TURN-EXACT: the orders of the ci.yml
/// allocation step through the CLI's own founding, configuration and executor (<see cref="AllocationScenarioRun"/>), and
/// what each step DID — the split's figures and the set-asides, read through the report's observer AND from the worlds'
/// own tables — pinned step by step; the twins without the kind-11 and the kind-12 order (each order delivered in the step
/// from its stamp turn, not before and not after); a mid-run Snapshot round trip at the save point K that resumes the same
/// hashes and the same report; and the CLI continuing the in-process world K to the same hashes and report (one world, two
/// runners). Every figure is MEASURED on the batch-4 tree (base-b3 + T1a v4 + T3), 2026-10-17.
/// <para>T3 ROUND 2 (2026-10-17; D-055, the orchestrator's finding F1 — a kind-12 order delivered where no head drew let a split
/// applied to spending one step LATE, mutant CM04b, pass the leg byte for byte): the b = 70 order now shares its batch (136)
/// with three granaries at settlements 0, 2 and 4, so in its delivery step building work draws what the NEW share grants —
/// pinned turn-exact here against the twin without the order
/// (<see cref="Kind12_ChangesBuildingWorksDraw_InItsOwnDeliveryStep_TheTwinDrawsTheOldShare"/>).</para>
/// </summary>
[Collection(AllocationScenarioCollection.Name)]
public class AllocationScenarioTests(AllocationScenarioRun run)
{
    private static readonly TimeSpan Limit = TimeSpan.FromMinutes(4);

    /// <summary>Stone, every step P..H−1: (b, S, E_b, E_r, D_b, D_r, cap, G_b, build, holds, road). Road works were ordered
    /// every step (100 % to 125, 1 % from 126); the starvation guard never fired.</summary>
    private static readonly (long Turn, int B, long S, long Eb, long Er, long Db, long Dr, long Cap, long Gb, long Build, long Holds, long Road)[] Stone_ =
    [
        (116, 30, 4913, 1473, 3440, 0, 4913, 1473, 0, 0, 0, 4913),   // no head: road works use building's share too
        (117, 30, 64, 19, 45, 40, 63, 19, 19, 19, 0, 45),            // X: the first binding step
        (118, 30, 61, 18, 43, 21, 61, 18, 18, 18, 0, 43),
        (119, 30, 66, 19, 47, 23, 66, 19, 19, 19, 0, 47),
        (120, 30, 63, 18, 45, 4, 63, 18, 4, 4, 0, 58),               // Y: building's unused share to road works
        (121, 30, 68, 20, 48, 40, 68, 20, 20, 20, 0, 48),
        (122, 30, 66, 19, 47, 20, 66, 19, 19, 19, 0, 47),
        (123, 30, 67, 20, 47, 1, 67, 20, 1, 1, 0, 66),
        (124, 30, 68, 20, 48, 20, 68, 20, 20, 20, 0, 48),
        (125, 30, 67, 20, 47, 0, 67, 20, 0, 0, 0, 67),
        (126, 30, 70, 21, 49, 20, 70, 21, 20, 9, 11, 50),            // settlement 4's paced head draws 9, sets aside 11
        (127, 30, 81, 24, 57, 11, 81, 24, 11, 0, 11, 70),
        (128, 30, 81, 24, 57, 51, 81, 24, 24, 13, 11, 57),           // W: the set-aside binds the younger heads
        (129, 30, 81, 24, 57, 38, 81, 24, 24, 13, 11, 57),
        (130, 30, 84, 25, 59, 45, 68, 25, 25, 14, 11, 59),           // from K
        (131, 30, 84, 25, 59, 31, 12, 72, 31, 20, 11, 12),           // Z: road works' unused share to building work
        (132, 30, 127, 38, 89, 40, 127, 38, 38, 20, 18, 89),         // V: the cancel releases the 11; the new head sets aside 18
        (133, 30, 89, 26, 63, 20, 89, 26, 20, 0, 20, 68),
        (134, 30, 98, 29, 69, 20, 98, 29, 20, 0, 20, 78),
        (135, 30, 95, 28, 67, 20, 95, 28, 20, 20, 0, 75),            // settlement 4's builders are back: built
        // Re-pinned 2026-10-17 (T3 round 2; D-055, the 2026-10-16 decision — finding F1). Cause: the scenario's batch 136 now
        // queues three granaries beside the b = 70 order. Old rows, REPRODUCED on this build from the round-1 log (the control:
        // the same log without the three 136 enqueues, t3-out/t3-v2.patch's generator; t3-out/r2/repin/): 136 (136, 70, 73, 51,
        // 22, 0, 73, 51, 0, 0, 0, 73) and 137 (137, 70, 76, 53, 23, 0, 76, 53, 0, 0, 0, 76) — no head, so b = 70 drew nothing.
        (136, 70, 73, 51, 22, 60, 73, 51, 51, 51, 0, 22),            // b = 70 in its delivery step: building work draws E_b = 51
        (137, 70, 76, 53, 23, 9, 76, 53, 9, 9, 0, 67),               // settlement 4's granary takes the 9 it still needs
        (138, 70, 79, 55, 24, 0, 79, 55, 0, 0, 0, 79),
        (139, 70, 75, 52, 23, 0, 75, 52, 0, 0, 0, 75),
    ];

    [Fact]
    public void TheSplit_IsTurnExact_EveryStep_AndTheStepDidWhatTheRuleForecast()
    {
        Assert.Equal(Stone_.Length, run.Rows.Length);
        for (int i = 0; i < Stone_.Length; i++)
        {
            var e = Stone_[i];
            SplitReportRow r = run.Rows[i];
            SplitGoodFigures s = r.GoodOf(Stone)!;
            Assert.Equal((e.Turn, Player, e.B, e.Turn < 126 ? 100.0 : 1.0, false), (r.Turn, r.Polity, r.BuildingsPercent, r.RoadPercent!.Value, r.Guarded));
            Assert.Equal((e.Turn, e.S, e.Eb, e.Er, e.Db, e.Dr, e.Cap, e.Gb, e.Build, e.Holds, e.Road),
                (r.Turn, s.S, s.Eb, s.Er, s.Db, s.Dr, s.Cap, s.Gb, s.Build, s.Holds, s.Road));
            // The measured actuals are the step's own forecast (one Empire with a road order: the road forecast is exact).
            Assert.Equal((s.ForecastBuild, s.ForecastHolds, s.ForecastRoad), (s.Build, s.Holds, s.Road));
        }
    }

    [Fact]
    public void TheHeads_TheirDrawsSetAsidesAndCauses_AreTurnExact_InTheHoldWindow()
    {
        // (step, [settlement:slot enqueued before->after limit | stone draw, hold, cause, holder])
        (long Turn, string[] Heads)[] expected =
        [
            (126, ["4:0 e126 0->418976 None | 9 11 None -"]),
            (127, ["4:0 e126 418976->422236 None | 0 11 None -"]),
            (128, ["4:0 e126 422236->422236 Labour | 0 11 None -",
                   "0:0 e128 0->650000 Materials | 13 0 HeldForEarlier 4:0",
                   "2:0 e128 0->0 Materials | 0 0 HeldForEarlier 4:0"]),
            (129, ["4:0 e126 422236->422236 Labour | 0 11 None -",
                   "0:0 e128 650000->1000000 None | 7 0 None -",
                   "2:0 e128 0->300000 Materials | 6 0 HeldForEarlier 4:0"]),
            (130, ["4:0 e126 422236->422236 Labour | 0 11 None -",
                   "0:1 e128 0->700000 Materials | 14 0 HeldForEarlier 4:0",
                   "2:0 e128 300000->300000 Materials | 0 0 HeldForEarlier 4:0"]),
            (131, ["4:0 e126 422236->422236 Labour | 0 11 None -",
                   "0:1 e128 700000->1000000 None | 6 0 None -",
                   "2:0 e128 300000->1000000 None | 14 0 None -"]),
            (132, ["2:1 e128 0->1000000 None | 20 0 None -",
                   "4:0 e132 0->0 Labour | 0 18 None -"]),
            (133, ["4:0 e132 0->0 Labour | 0 20 None -"]),
            (134, ["4:0 e132 0->0 Labour | 0 20 None -"]),
            (135, ["4:0 e132 0->1000000 None | 20 0 None -"]),
            // T3 round 2 (2026-10-17): the granaries queued with the b = 70 order, walked in the building order (all stamped
            // 136: settlement id decides) within the NEW grant 51 — 20, 20, then 11 of settlement 4's 20 (HeldForRoads: the
            // split's grant, not a set-aside, holds the rest back).
            (136, ["0:0 e136 0->1000000 None | 20 0 None -",
                   "2:0 e136 0->1000000 None | 20 0 None -",
                   "4:0 e136 0->550000 Materials | 11 0 HeldForRoads -"]),
            (137, ["4:0 e136 550000->1000000 None | 9 0 None -"]),
        ];
        foreach ((long turn, string[] heads) in expected)
            Assert.Equal(heads, run.Row(turn).Heads.Select(Text).ToArray());
        // Outside the windows no head is walked (136 and 137 have heads from T3 round 2 on).
        Assert.All(new long[] { 116, 125, 138, 139 }, t => Assert.Empty(run.Row(t).Heads));
    }

    [Fact]
    public void TheWorlds_HoldTheSetAsidesAndTheProgress_TurnExact_ReadFromTheirOwnTables()
    {
        // ConstructionHolds (schema v34 section 61) as the worlds hold them — not through the probe: (good, units) of the
        // player's purse, which is the only purse with a head.
        int purse = Reservations.PolityPurse(new PolityId(Player));
        (long World, (int Good, long Units)[] Holds)[] holds =
        [
            (117, []),
            (118, [(Timber, 42)]),
            (126, []),
            (127, [(Timber, 23), (Stone, 11)]),
            (128, [(Timber, 23), (Stone, 11)]),
            (129, [(Timber, 77), (Stone, 11)]),
            (130, [(Timber, 51), (Stone, 11)]),
            (131, [(Timber, 63), (Stone, 11)]),
            (132, [(Timber, 23), (Stone, 11)]),
            (133, [(Timber, 40), (Stone, 18)]),
            (134, [(Timber, 40), (Stone, 20)]),
            (135, [(Timber, 40), (Stone, 20)]),
            (136, []),
            (137, [(Timber, 18)]),                 // T3 round 2: settlement 4's part-built granary sets aside its timber
            (138, []),
        ];
        foreach ((long world, (int Good, long Units)[] rows) in holds)
            Assert.Equal(rows.Select(h => new ConstructionHoldRow(purse, h.Good, h.Units)).ToArray(), All(run.World(world).ConstructionHolds));

        // ConstructionProgress: (settlement, slot, enqueued, progress) of every entry.
        (long World, string[] Rows)[] progress =
        [
            (118, ["0:0 117 950000", "0:1 117 0", "0:2 117 0", "2:0 117 0", "2:1 117 0", "2:2 117 0"]),
            (127, ["4:0 126 418976"]),
            (128, ["4:0 126 422236"]),
            (129, ["4:0 126 422236", "0:0 128 650000", "0:1 128 0", "2:0 128 0", "2:1 128 0"]),
            (130, ["4:0 126 422236", "0:1 128 0", "2:0 128 300000", "2:1 128 0"]),
            (132, ["4:0 126 422236", "2:1 128 0"]),
            (133, ["4:0 132 0"]),
            (136, []),
            (137, ["4:0 136 550000"]),             // T3 round 2: the granaries at 0 and 2 completed in step 136 -> 137
            (138, []),
        ];
        foreach ((long world, string[] rows) in progress)
            Assert.Equal(rows, All(run.World(world).ConstructionProgress)
                .Select(r => $"{r.Settlement.Value}:{r.Slot} {r.EnqueuedTurn} {r.Progress}").ToArray());

        // The building order's servings, from the Structures table: granaries built at settlements 0, 2 and 4.
        Assert.Equal((5L, 4L, 0L), Built(run.World(Z + 1)));
        // Re-pinned 2026-10-17 (T3 round 2; cause: the three granaries queued at 136, all built by H). Old value (5, 5, 1),
        // reproduced on this build from the round-1 log (t3-out/r2/repin/).
        Assert.Equal((6L, 6L, 2L), Built(run.World(H)));
    }

    [Fact]
    public void Kind11AndKind12_AreDeliveredInTheStepFromTheirStampTurn_TwinsWithoutThemDifferFromTheNextWorldOn()
    {
        // From world K (the save point), as the ci.yml twins run from save_K.
        (string[] main, SplitReportRow[] _) = Continue(run.World(K).Clone(), run.Log, V + 1);
        (string[] no11, SplitReportRow[] _) = Continue(run.World(K).Clone(), Log(Orders(drop: (V, OrderKind.CancelConstructionEntry))), V + 1);
        Assert.Equal(main, run.Hashes[(int)(K + 1)..(int)(V + 2)]);
        Assert.Equal(main[..(int)(V - K)], no11[..(int)(V - K)]);              // equal through world V
        Assert.NotEqual(main[^1], no11[^1]);                                     // different at V + 1
        // ... and WHAT differs: without the cancel the part-built head is still there and the project is queued twice.
        WorldState twin = StepTo(run.World(K).Clone(), Log(Orders(drop: (V, OrderKind.CancelConstructionEntry))), V + 1);
        Assert.Equal(new[] { "4:0 126 422236", "4:1 132 0" },
            All(twin.ConstructionProgress).Where(r => r.Settlement.Value == Third).Select(r => $"{r.Settlement.Value}:{r.Slot} {r.EnqueuedTurn} {r.Progress}").ToArray());

        (string[] no12, SplitReportRow[] rows12) = Continue(run.World(K).Clone(), Log(Orders(drop: (Seventy, OrderKind.SetResourceAllocation))), Seventy + 1);
        Assert.Equal(run.Hashes[(int)(K + 1)..(int)(Seventy + 1)], no12[..(int)(Seventy - K)]);   // equal through world 136
        Assert.NotEqual(run.Hashes[Seventy + 1], no12[^1]);                                     // different at 137
        Assert.Equal(30, rows12[^1].BuildingsPercent);                                           // the twin's step 136 stays at b = 30
        Assert.Equal(70, run.Row(Seventy).BuildingsPercent);
    }

    /// <summary>
    /// T3 ROUND 2 (2026-10-17; D-055, finding F1) — THE KIND-12 ORDER CHANGES SPENDING IN ITS OWN DELIVERY STEP, TURN-EXACT.
    /// The b = 70 order is stamped 136 in the batch that queues granaries at settlements 0, 2 and 4, so the step 136 → 137
    /// both delivers the new share and has heads that need stone. MEASURED: the run's row for that step is the NEW share's —
    /// S 73, E_b = floor(73 × 70 / 100) = 51 binds (D_b 60), and building work draws exactly 51 (20, 20, 11 down the building
    /// order); the twin without the order, from the same world K, has the same purse and claims at 136 but b = 30 —
    /// E_b = 21 binds and building work draws 21 (20, 1, 0). The difference is the share itself, IN THE STEP OF DELIVERY: a
    /// split applied to spending one step late (mutant CM04b: construction reads PREV's split rows) would draw 21 here
    /// under a row that says 70. split-check's PT (`--twin --order-at 136:70`, the ci.yml step) judges the same pair.
    /// </summary>
    [Fact]
    public void Kind12_ChangesBuildingWorksDraw_InItsOwnDeliveryStep_TheTwinDrawsTheOldShare()
    {
        SplitReportRow a = run.Row(Seventy);
        SplitGoodFigures s = a.GoodOf(Stone)!;
        Assert.Equal((70, 73L, 51L, 22L, 60L, 73L, 51L, 51L, 51L, 0L, 22L),
            (a.BuildingsPercent, s.S, s.Eb, s.Er, s.Db, s.Dr, s.Cap, s.Gb, s.Build, s.Holds, s.Road));
        Assert.Equal(new[] { (0, 20L), (2, 20L), (Third, 11L) }, a.Heads.Select(h => (h.Settlement, h.GoodOf(Stone)!.Draw)).ToArray());
        Assert.All(a.Heads, h => Assert.Equal(Seventy, h.Enqueued));

        OrderLog no12 = Log(Orders(drop: (Seventy, OrderKind.SetResourceAllocation)));
        (SplitReportRow[] twinRows, WorldState twin137) = ContinueTo(run.World(K).Clone(), no12, Seventy + 1);
        SplitReportRow t = twinRows[^1];
        SplitGoodFigures ts = t.GoodOf(Stone)!;
        Assert.Equal((Seventy, 30, 73L, 21L, 52L, 60L, 73L, 21L, 21L, 21L, 0L, 52L),
            (t.Turn, t.BuildingsPercent, ts.S, ts.Eb, ts.Er, ts.Db, ts.Dr, ts.Cap, ts.Gb, ts.Build, ts.Holds, ts.Road));
        Assert.Equal(new[] { (0, 20L), (2, 1L), (Third, 0L) }, t.Heads.Select(h => (h.Settlement, h.GoodOf(Stone)!.Draw)).ToArray());
        // The same purse and claim in both at the delivery step; the building draws differ by the shares' difference.
        Assert.Equal((s.S, s.Db, s.Dr), (ts.S, ts.Db, ts.Dr));
        Assert.Equal(s.Eb - ts.Eb, s.Build - ts.Build);

        // What the two draws bought, read from the worlds' own tables (world 137): in the run settlements 0 and 2 completed
        // their granaries and settlement 4's stands at 550,000; in the twin settlement 2's stands at 50,000, 4's at 0.
        string[] Progress(WorldState w) => All(w.ConstructionProgress).Select(r => $"{r.Settlement.Value}:{r.Slot} {r.EnqueuedTurn} {r.Progress}").ToArray();
        Assert.Equal(["4:0 136 550000"], Progress(run.World(Seventy + 1)));
        Assert.Equal(["2:0 136 50000", "4:0 136 0"], Progress(twin137));

        // The judgement the ci.yml step runs on the same pair (`split-check alloc-a.rep --twin alloc-t12.rep --order-at 136:70`).
        SplitCheckResult pt = SplitReportCheck.Check(run.Rows, twin: new SplitTwin(twinRows, Seventy, 70));
        Assert.True(pt.Passed, string.Join("\n", pt.Failures));
        Assert.Equal("the kind-12 order at 136 (PT): b 30 -> 70 in step 136->137; building work drew, against the twin without it: " +
                     "stone 51 vs 21 (E_b 51 vs 21, G_b 51 vs 21), timber 102 vs 42 (E_b 310121 vs 132909, G_b 120 vs 120)", pt.Findings[^1]);
    }

    [Fact]
    public void ASnapshotAtK_RoundTripsTheSetAsides_AndItsContinuationIsTheRunsTail()
    {
        WorldState k = run.World(K);
        WorldState loaded;
        using (var ms = new MemoryStream())
        {
            Snapshot.Save(k, ms);
            ms.Position = 0;
            loaded = Snapshot.Load(ms, k.Terrain);
        }
        Assert.Equal(run.Hashes[K], WorldHash.ComputeHex(loaded));
        Assert.NotEmpty(All(loaded.ConstructionHolds));
        Assert.Equal(All(k.ConstructionHolds), All(loaded.ConstructionHolds));
        Assert.Equal(All(k.ConstructionProgress), All(loaded.ConstructionProgress));
        Assert.Equal(All(k.ResourceAllocations), All(loaded.ResourceAllocations));

        // A fresh executor continues the loaded world to H: the run's hashes and its report, row for row and byte for byte.
        (string[] hashes, SplitReportRow[] rows, string report) = ContinueWithReport(loaded, run.Log, H);
        Assert.Equal(run.Hashes[(int)(K + 1)..], hashes);
        Assert.Equal(run.ReportFrom(K), report);
        Assert.Equal(H - K, rows.Length);
        Assert.True(SplitReportCheck.Check(rows, expectHoldAt: K + 1).Passed);
    }

    [Fact]
    public void TheCli_ContinuesTheInProcessWorldAtK_ToTheSameHashesAndTheSameReport()
    {
        // One world, two runners: the CLI (its own process, --from-save) continues world K written by THIS process with the
        // generated log, and gives the in-process run's hashes and report — so the ci.yml leg and these pins are one run.
        string dir = Path.Combine(Path.GetTempPath(), "civsim-alloc-" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(dir);
        try
        {
            string save = Path.Combine(dir, "k.civsnap"), orders = Path.Combine(dir, "alloc.bin");
            using (FileStream f = File.Create(save)) Snapshot.Save(run.World(K), f);
            using (FileStream f = File.Create(orders)) run.Log.Save(f);
            (int exit, string stdout, string stderr) = CliProcess.Run(Limit, "run", "--from-save", save, "--turns", $"{H - K}",
                "--orders", orders, "--hash-log", Path.Combine(dir, "b.log"), "--split-report", Path.Combine(dir, "b.rep"));
            Assert.True(exit == 0, stdout + stderr);
            Assert.Equal(run.Hashes[(int)(K + 1)..], File.ReadAllLines(Path.Combine(dir, "b.log")));
            Assert.Equal(run.ReportFrom(K), File.ReadAllText(Path.Combine(dir, "b.rep")));
        }
        finally
        {
            Directory.Delete(dir, recursive: true);
        }
    }

    [Fact]
    public void TheControl_IsTurnExact_WhereTheSplitBinds()
    {
        // The b = 100 twin from world P: building work is served first and road works get what is left.
        (long Turn, long S, long Db, long Gb, long Build, long Holds, long Road)[] expected =
        [
            (117, 64, 40, 40, 40, 0, 24),
            (118, 61, 40, 40, 40, 0, 21),
            (119, 66, 40, 40, 40, 0, 26),
            (128, 81, 51, 51, 40, 11, 30),     // the set-aside and both younger heads fit in the whole purse
        ];
        foreach (var e in expected)
        {
            SplitGoodFigures s = run.ControlRow(e.Turn).GoodOf(Stone)!;
            Assert.Equal(e, (e.Turn, s.S, s.Db, s.Gb, s.Build, s.Holds, s.Road));
        }
    }

    // ================================================================== helpers

    private static string Text(SplitHeadFigures h)
    {
        SplitHeadGood s = h.GoodOf(Stone)!;
        string holder = s.Holder is { } x ? $"{x.Settlement}:{x.Slot}" : "-";
        return $"{h.Settlement}:{h.Slot} e{h.Enqueued} {h.Before}->{h.After} {h.Limit} | {s.Draw} {s.Hold} {s.Cause} {holder}";
    }

    private static (long S0, long S2, long S4) Built(WorldState w) =>
        (ConstructionQuery.Built(w, new SettlementId(0), Granary), ConstructionQuery.Built(w, new SettlementId(2), Granary),
         ConstructionQuery.Built(w, new SettlementId(Third), Granary));

    private WorldState StepTo(WorldState w, OrderLog log, long to)
    {
        TurnExecutor exec = Executor(run.Cfg, log);
        while (w.Clock.Turn < to)
        {
            OrderValidation.ValidateAtDelivery(log.BatchFor(w.Clock.Turn), run.TurnZero, w);
            w = exec.Step(w);
        }
        return w;
    }

    /// <summary>The report rows of a continuation to world <paramref name="to"/>, and that world.</summary>
    private (SplitReportRow[] Rows, WorldState World) ContinueTo(WorldState w, OrderLog log, long to)
    {
        TurnExecutor exec = Executor(run.Cfg, log);
        using var report = new MemoryStream();
        var probe = new SplitProbe(report, run.Cfg);
        while (w.Clock.Turn < to)
        {
            OrderBatch batch = log.BatchFor(w.Clock.Turn);
            OrderValidation.ValidateAtDelivery(batch, run.TurnZero, w);
            w = probe.Step(exec, w, batch);
        }
        return (SplitReport.Parse(System.Text.Encoding.UTF8.GetString(report.ToArray())), w);
    }

    private (string[] Hashes, SplitReportRow[] Rows) Continue(WorldState w, OrderLog log, long to)
    {
        (string[] hashes, SplitReportRow[] rows, _) = ContinueWithReport(w, log, to);
        return (hashes, rows);
    }

    private (string[] Hashes, SplitReportRow[] Rows, string Report) ContinueWithReport(WorldState w, OrderLog log, long to)
    {
        TurnExecutor exec = Executor(run.Cfg, log);
        var hashes = new List<string>();
        using var report = new MemoryStream();
        var probe = new SplitProbe(report, run.Cfg);
        while (w.Clock.Turn < to)
        {
            OrderBatch batch = log.BatchFor(w.Clock.Turn);
            OrderValidation.ValidateAtDelivery(batch, run.TurnZero, w);
            w = probe.Step(exec, w, batch);
            hashes.Add(WorldHash.ComputeHex(w));
        }
        string text = System.Text.Encoding.UTF8.GetString(report.ToArray());
        return ([.. hashes], SplitReport.Parse(text), text);
    }
}
