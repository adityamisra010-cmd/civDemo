using Sim.Core.Kernel;
using Sim.Core.Observability;
using Sim.Core.State;
using Sim.Tests.TestUtil;
using static Sim.Tests.Kernel.AllocationScenario;

namespace Sim.Tests.Kernel;

/// <summary>
/// D-055 / ADR-040 (batch 4, item 3) — the ci.yml step "Allocation split, binding, cross-process (D-055)" is only as good
/// as its scripted log and its save point: two processes, a replay and a split-check agreeing on a run in which the split
/// never bound, no head was held, or save_K held nothing would prove nothing about the split or the set-asides (finding
/// V4-N3: no other leg ever publishes a hold row). The FoundSettlementCiLegTests pattern: this class pins the literals
/// `scripts/gen-sample-orders.py --allocation` writes (and the sha256 of the logs the step checks the generated files
/// against) AND the step's command literals, and RE-MEASURES every premise the step relies on, on the same founded world
/// the CLI runs, through the same <see cref="SplitProbe"/> `--split-report` uses (<see cref="AllocationScenarioRun"/>):
/// window 1 (the built road researched), the first binding step X, surplus both ways (Y, Z), competing heads served in the
/// building order, the first HeldForEarlier at W (an OLDER part-built head without builders, its set-aside inside the 30 %
/// share), the cancel and re-enqueue at V, what world K holds, the control's contrast at X, kind 12's b = 70, and no AI
/// polity, revolt or colony by H (`sim replay` produces no AI orders). A drifted premise fails here BY NAME.
/// <para>T3 ROUND 2 (2026-10-17; D-055, findings F1–F3): the b = 70 order shares its batch with three granaries, so it is
/// delivered in a step where heads draw stone and the new share changes their grant (re-measured below: the generator
/// literal, the four logs' sha256, the 52-record shape, the binding steps, kind 12's step); the twin without it runs with
/// `--split-report` and `split-check --twin --order-at` (PT) judges the pair; split-check re-derives every row's split (P11)
/// and walk (P12).</para>
/// </summary>
[Collection(AllocationScenarioCollection.Name)]
public class AllocationCiLegTests(AllocationScenarioRun run)
{
    private const string StepName = "Allocation split, binding, cross-process (D-055)";

    // ================================================================== the literals

    [Fact]
    public void TheScriptLiterals_AreTheOrdersPinnedHere()
    {
        string script = File.ReadAllText(Path.Combine(RepoRoot(), "scripts", "gen-sample-orders.py"));
        foreach (string literal in new[]
        {
            "records = [(0, 1, 6, 12, 0.0), (32, 1, 6, 17, 0.0), (39, 1, 6, 2, 0.0), (53, 1, 6, 8, 0.0),",
            "(66, 1, 6, 52, 0.0), (86, 1, 6, 74, 0.0), (99, 1, 6, 53, 0.0), (116, 1, 12, 1, 30.0)]",
            "records += [(117, 1, 4, 0, 1.0)] * 3 + [(117, 1, 4, 2, 1.0)] * 3",
            "records += [(125, 1, 3, (4 << 3) | 4, 0.01), (126, 1, 4, 4, 1.0), (127, 1, 3, (4 << 3) | 4, 0.0)]",
            "records += [(128, 1, 4, 0, 1.0)] * 2 + [(128, 1, 4, 2, 1.0)] * 2",
            "records += [(132, 1, 13, 4, cancel_target(0, 1, 126)), (132, 1, 4, 4, 1.0), (134, 1, 3, (4 << 3) | 4, 10.0), (136, 1, 12, 1, 70.0)]",
            "return slot + (1 << 18) * (project + (1 << 16) * (enqueued_turn + 1))",   // D-056: OrderLog.PackCancelTarget
            "records += [(136, 1, 4, 0, 1.0), (136, 1, 4, 2, 1.0), (136, 1, 4, 4, 1.0)]",   // T3 round 2: heads in kind 12's step
            "records += [(turn, 1, 8, 0, 100.0 if turn < 126 else 1.0) for turn in range(116, max_turn)]",
            "records = [r for r in records if r[0] < max_turn]",
            "if \"--no-split\" in sys.argv:\n    records = [r for r in records if r[2] != 12]",
            "if drop is not None:\n    records = [r for r in records if (r[0], r[2]) != drop]",
        })
            Assert.Contains(literal, script.Replace("\r\n", "\n", StringComparison.Ordinal));

        // The table here IS the script's: the four logs the step generates are checked (sha256sum -c) against the sha256
        // of this table serialized as an order log, so a drift on either side fails CI or this test.
        string ci = CiStep();
        Assert.Contains($"{Sha256(Orders())}  alloc.bin", ci);
        Assert.Contains($"{Sha256(Orders(split: false))}  alloc-ctl.bin", ci);
        Assert.Contains($"{Sha256(Orders(drop: (V, OrderKind.CancelConstructionEntry)))}  alloc-no13.bin", ci);
        Assert.Contains($"{Sha256(Orders(drop: (Seventy, OrderKind.SetResourceAllocation)))}  alloc-no12.bin", ci);
        Assert.Contains("sha256sum -c - <<'SHA'", ci);

        // The table's shape (52 records — 49 until T3 round 2 added the three granaries stamped 136 —; every one by the player;
        // the control and the twins differ by what they drop).
        Assert.Equal(52, Orders().Length);
        Assert.Equal(50, Orders(split: false).Length);
        Assert.Equal(new[] { OrderKind.SetResourceAllocation, OrderKind.EnqueueConstruction, OrderKind.EnqueueConstruction,
            OrderKind.EnqueueConstruction, OrderKind.DevelopRoads }, All(Log(Orders()).BatchFor(Seventy)).Select(o => o.Kind).ToArray());
        Assert.All(Orders(), o => Assert.Equal(Player, o.ActorId));
        Assert.Single(Orders(), o => o.Turn == V && o.Kind == OrderKind.CancelConstructionEntry);
        Assert.DoesNotContain(Orders(), o => o.Kind == OrderKind.CancelConstruction);   // D-056: kind 11 is no longer written
        Assert.Single(Orders(), o => o.Turn == Seventy && o.Kind == OrderKind.SetResourceAllocation);
    }

    [Fact]
    public void TheCiStep_RunsExactlyTheseCommands_AtThePinnedTurns()
    {
        string ci = CiStep();
        // Every command of the step, in order (comments and checks aside): the turns are this class's constants.
        string[] commands = ci.Split('\n').Select(l => l.Trim())
            .Where(l => l.StartsWith("$CLI ", StringComparison.Ordinal) || l.StartsWith("python3 scripts/gen-sample-orders.py", StringComparison.Ordinal))
            .Select(l => l.Split(" | ")[0].Split("   #")[0].Trim())
            .ToArray();
        // M6 Track EX (ADR-044 §12; re-checked 2026-10-23): every run and replay takes --without-discovery — the scenario's
        // D-055 premises are a pre-M6 measurement (under the shipped rules split-check FAILS P5/P7 on alloc-b.rep);
        // AllocationScenarioRun runs the same rules in process.
        string[] expected =
        [
            $"python3 scripts/gen-sample-orders.py alloc.bin {H} --allocation",
            $"python3 scripts/gen-sample-orders.py alloc-ctl.bin {H} --allocation --no-split",
            $"python3 scripts/gen-sample-orders.py alloc-no13.bin {H} --allocation --drop {V}:13",
            $"python3 scripts/gen-sample-orders.py alloc-no12.bin {H} --allocation --drop {Seventy}:12",
            $"$CLI run --without-discovery --founded --seed 42 --turns {P} --orders alloc.bin --hash-log alloc-a0.log --save-at {P} --save alloc-{P}.civsnap",
            $"$CLI run --without-discovery --from-save alloc-{P}.civsnap --turns {H - P} --orders alloc.bin --hash-log alloc-a.log --split-report alloc-a.rep --save-at {K} --save alloc-{K}.civsnap",
            $"$CLI run --without-discovery --from-save alloc-{P}.civsnap --turns {H - P} --orders alloc.bin --hash-log alloc-a1.log --save-at {K} --save alloc-{K}-a1.civsnap",
            $"$CLI run --without-discovery --from-save alloc-{K}.civsnap --turns {H - K} --orders alloc.bin --hash-log alloc-b.log --split-report alloc-b.rep --save-at {Seventy} --save alloc-{Seventy}.civsnap",
            $"$CLI run --without-discovery --from-save alloc-{P}.civsnap --turns {H - P} --orders alloc-ctl.bin --hash-log alloc-c.log --split-report alloc-c.rep",
            $"$CLI replay --without-discovery --founded --seed 42 --turns {H} --orders alloc.bin --hash-log alloc-replayed.log",
            $"$CLI diff alloc-{K}.civsnap alloc-{K}-a1.civsnap",
            $"$CLI run --without-discovery --from-save alloc-{K}.civsnap --turns {V - K} --orders alloc.bin --hash-log alloc-v.log --save-at {V} --save alloc-{V}.civsnap",
            $"$CLI run --without-discovery --from-save alloc-{K}.civsnap --turns {V + 1 - K} --orders alloc-no13.bin --hash-log alloc-t13.log",
            $"$CLI run --without-discovery --from-save alloc-{K}.civsnap --turns {Seventy + 1 - K} --orders alloc-no12.bin --hash-log alloc-t12.log --split-report alloc-t12.rep",
            $"$CLI split-check alloc-a.rep --expect-hold-at {K}",
            "$CLI split-check alloc-a.rep --control alloc-c.rep",
            $"$CLI split-check alloc-b.rep --expect-hold-at {K + 1}",
            $"$CLI split-check alloc-a.rep --twin alloc-t12.rep --order-at {Seventy}:70",
        ];
        Assert.Equal(expected, commands);

        // The checks that read the logs at the pinned worlds (line n of alloc-full.log = world n).
        foreach (string check in new[]
        {
            "cat alloc-a0.log alloc-a.log > alloc-full.log",
            "if ! cmp alloc-a.log alloc-a1.log || ! cmp alloc-130.civsnap alloc-130-a1.civsnap; then",
            "diff alloc-b.log <(sed -n 131,140p alloc-full.log)",
            "diff alloc-b.rep <(tail -n 10 alloc-a.rep)",
            "if ! diff alloc-full.log alloc-replayed.log > alloc-replay.diff; then",
            "if [ \"$($CLI hash alloc-130.civsnap)\" != \"$(sed -n 130p alloc-full.log)\" ]; then",
            "holds=$(awk '$1 == \"ConstructionHolds\" { print $2 }' alloc-130.diff)",
            "if [ \"${holds:-0}\" -le 0 ]; then",
            "if [ \"$(cat alloc-v.log)\" != \"$(sed -n 131,132p alloc-full.log)\" ] \\",
            "|| [ \"$($CLI hash alloc-132.civsnap)\" != \"$(sed -n 132p alloc-full.log)\" ] \\",
            "|| [ \"$($CLI hash alloc-136.civsnap)\" != \"$(sed -n 136p alloc-full.log)\" ]; then",
            "if [ \"$(head -n 2 alloc-t13.log)\" != \"$(sed -n 131,132p alloc-full.log)\" ] || [ \"$(sed -n 3p alloc-t13.log)\" = \"$(sed -n 133p alloc-full.log)\" ]; then",
            "if [ \"$(head -n 6 alloc-t12.log)\" != \"$(sed -n 131,136p alloc-full.log)\" ] || [ \"$(sed -n 7p alloc-t12.log)\" = \"$(sed -n 137p alloc-full.log)\" ]; then",
        })
            Assert.Contains(check, ci);
        // The literal turns in those checks are this class's constants (and B's report is A's last H − K rows).
        Assert.Equal((130L, 131L, 132L, 133L, 136L, 137L, 140L, 10L), (K, K + 1, V, V + 1, Seventy, Seventy + 1, H, H - K));
    }

    // ================================================================== the premises, re-measured

    [Fact]
    public void Window1_TheBuiltRoad_IsResearchedBeforeThePreSplitWorld()
    {
        // MEASURED: track_road completes in step 98 -> 99 and stone_dry in step 115 -> 116 (road class 3 needs both and
        // costs stone). The fixture records the first world that knows each.
        Assert.Equal(99, run.FirstWorldKnowing("track_road"));
        Assert.Equal(116, run.FirstWorldKnowing("stone_dry"));
        Assert.True(run.FirstWorldKnowing("stone_dry") <= P);
        // World P holds no split row: every step before P ran at the default b = 100 (the kind 12 is stamped P).
        Assert.Empty(All(run.World(P).ResourceAllocations));
        Assert.Equal(new ResourceAllocationRow(new PolityId(Player), 30), Assert.Single(All(run.World(P + 1).ResourceAllocations)));
    }

    [Fact]
    public void X_TheFirstBindingStep_HasCompetingHeads_ServedInTheBuildingOrder()
    {
        // No step before X binds (and before P there is no split at all: the premise above).
        long first = run.Rows.First(r => Binding(r) is not null).Turn;
        Assert.Equal(X, first);
        SplitGoodFigures s = run.Row(X).GoodOf(Stone)!;
        Assert.Equal("stone", s.Good);
        Assert.True(s.Build + s.Holds == s.Gb && s.Gb < s.Db, $"step {X}: build {s.Build} + holds {s.Holds} = G_b {s.Gb} < D_b {s.Db}");
        // D-058 (2026-10-19) RE-PIN: D_b 40 -> 64. Cause: the Director's "build one at a time" ruling — the claim's walk on S
        // now carries settlement 0's leftover capacity into its slots 1 and 2 in the same step (AllocationScenarioTests'
        // Stone_ summary; the batch-4 value 40 is reproduced on the PreContinueWithinTurn twin). The step's own spending is
        // unchanged: G_b = build = 19.
        Assert.Equal((64L, 19L, 64L, 19L, 0L), (s.S, s.Gb, s.Db, s.Build, s.Holds));

        // Two settlements' heads, both ordered at 117, walked in the building order (EnqueuedTurn, SettlementId, Slot):
        // settlement 0's is served, settlement 2's waits although it needs stone.
        SplitHeadFigures[] heads = run.Row(X).Heads;
        Assert.Equal(new[] { (0, 0, 117L), (2, 0, 117L) }, heads.Select(h => (h.Settlement, h.Slot, h.Enqueued)).ToArray());
        Assert.Equal((0L, 950_000L, 19L), (heads[0].Before, heads[0].After, heads[0].GoodOf(Stone)!.Draw));
        Assert.Equal((0L, 0L, 0L, 20L), (heads[1].Before, heads[1].After, heads[1].GoodOf(Stone)!.Draw, heads[1].GoodOf(Stone)!.Need));
        // Every row's heads are in the building order (the walk's order).
        foreach (SplitReportRow r in run.Rows)
            for (int i = 1; i < r.Heads.Length; i++)
                Assert.True(Key(r.Heads[i - 1]).CompareTo(Key(r.Heads[i])) < 0, $"step {r.Turn}: heads out of the building order");
        // The binding steps of window 2 all lie in 117..125, those of the hold window from W on: the windows are separate.
        // Re-pinned 2026-10-17 (T3 round 2; cause: the granaries queued with the b = 70 order bind under the NEW share at 136).
        // Old list 117 118 119 121 122 128 129 130 132 — reproduced on this build from the round-1 log (t3-out/r2/repin/).
        long[] binding = run.Rows.Where(r => Binding(r) is not null).Select(r => r.Turn).ToArray();
        // D-058 (2026-10-19) RE-PIN: 120 joins (settlement 2's head, no longer served at 119, claims more than the grant) and
        // 132 leaves (settlement 2's granaries ordered at 128 completed together at 131). Batch-4 list, reproduced on the
        // twin: 117 118 119 121 122 128 129 130 132 136.
        Assert.Equal(new long[] { 117, 118, 119, 120, 121, 122, 128, 129, 130, 136 }, binding);
    }

    [Fact]
    public void Y_And_Z_EachShareUnusedByOneUse_IsUsedByTheOther()
    {
        // Y: building work, with heads in the queue, claims less than its 30 % share and road works draw more than theirs.
        SplitGoodFigures y = run.Row(Y).GoodOf(Stone)!;
        Assert.NotEmpty(run.Row(Y).Heads);
        Assert.True(y.Db < y.Eb && y.Road > y.Er, $"step {Y}: D_b {y.Db} < E_b {y.Eb} and road {y.Road} > E_r {y.Er}");
        // D-058 (2026-10-19) RE-PIN: Y moved 120 -> 123 (AllocationScenario.Y). Batch-4 values at 120, reproduced on the twin:
        // (63, 18, 45, 4, 58).
        Assert.Equal((67L, 20L, 47L, 7L, 60L), (y.S, y.Eb, y.Er, y.Db, y.Road));
        // Z: road works need less than their share and building work's claim above its own share uses the rest.
        SplitGoodFigures z = run.Row(Z).GoodOf(Stone)!;
        Assert.True(z.Db > z.Eb && z.Dr < z.Er && z.Build + z.Holds > z.Eb,
            $"step {Z}: D_b {z.Db} > E_b {z.Eb}, D_r {z.Dr} < E_r {z.Er}, build + holds {z.Build + z.Holds} > E_b");
        // D-058 (2026-10-19) RE-PIN: D_b and build + holds 31 -> 51 (settlement 2's two granaries ordered at 128 complete
        // together in this step). Batch-4 values, reproduced on the twin: (84, 25, 59, 31, 12, 72, 31).
        Assert.Equal((84L, 25L, 59L, 51L, 12L, 72L, 51L), (z.S, z.Eb, z.Er, z.Db, z.Dr, z.Cap, z.Build + z.Holds));
        // Both are the FIRST such steps with a head (P5 has no witness before Z).
        Assert.Equal(Z, run.Rows.First(r => r.GoodOf(Stone) is { } g && g.Db > g.Eb && g.Dr < g.Er && g.Build + g.Holds > g.Eb).Turn);
        Assert.Equal(Y, run.Rows.First(r => r.Heads.Length > 0 && r.GoodOf(Stone) is { } g && g.Db < g.Eb && g.Road > g.Er).Turn);
    }

    [Fact]
    public void W_TheFirstHeldForEarlier_IsAYoungerHeadBehindTheOlderPartBuiltHeadWithoutBuilders()
    {
        // No head is held for an earlier head before W (window 2 has none: its waits are the split's, HeldForRoads).
        long first = run.Rows.First(r => HeldHeads(r).Length > 0).Turn;
        Assert.Equal(W, first);
        SplitReportRow w = run.Row(W);

        // The holder: settlement 4's granary, ordered at 126 (OLDER than the 128 heads), part-built by its few builders to a
        // progress that is not a round number, now with no builders at all (kind 3 weight 0 stamped 127, read on PREV from
        // step 128): it cannot advance (Labour), it is NOT released (labour adults remain: a valid wait), and it sets aside
        // the 11 stone it still needs.
        SplitHeadFigures holder = w.Heads[0];
        Assert.Equal((Third, 0, 126L, 422_236L, 422_236L, "Labour", "None"),
            (holder.Settlement, holder.Slot, holder.Enqueued, holder.Before, holder.After, holder.Limit, holder.Release));
        Assert.NotEqual(0L, holder.Before % 1000);
        Assert.Equal((11L, 0L, 11L), (holder.GoodOf(Stone)!.Need, holder.GoodOf(Stone)!.Draw, holder.GoodOf(Stone)!.Hold));
        Assert.Equal(0.0, All(run.World(W).SectorAllocations).Single(r => r.Settlement.Value == Third).Construction);

        // The younger heads (ordered at 128) are held for it: each drew less than min(its need, the budget less the draws
        // ahead of it), naming the holder.
        (int Settlement, long Draw)[] held = HeldHeads(w).Select(h => (h.Settlement, h.GoodOf(Stone)!.Draw)).ToArray();
        Assert.Equal(new[] { (0, 13L), (2, 0L) }, held);
        foreach (SplitHeadFigures h in HeldHeads(w))
        {
            SplitHeadGood g = h.GoodOf(Stone)!;
            Assert.Equal((W, "HeldForEarlier", new SplitHeadRef(Third, 0, Granary), 11L), (h.Enqueued, g.Cause, g.Holder, g.SetAsideAhead));
            Assert.True(h.Enqueued > holder.Enqueued);
        }

        // The set-aside sits INSIDE the 30 % share: road works need more than their share (D_r ≥ E_r), so the grant is the
        // building share itself, the walk starts at it, and the set-aside and the draws spend it; road works still get E_r.
        SplitGoodFigures s = w.GoodOf(Stone)!;
        Assert.True(s.Dr >= s.Er && s.Gb == s.Eb && s.Start == s.Gb, $"step {W}: D_r {s.Dr} >= E_r {s.Er}, G_b {s.Gb} = E_b {s.Eb} = start {s.Start}");
        Assert.True(s.Holds > 0 && s.Holds + s.Build == s.Gb && s.Road >= s.Er, $"step {W}: holds {s.Holds} + build {s.Build} = G_b {s.Gb}; road {s.Road} >= E_r {s.Er}");
        // D-058 (2026-10-19) RE-PIN: D_b 51 -> 81 (the claim's walk on S continues settlement 0's and 2's queues). Batch-4
        // values, reproduced on the twin: (81, 24, 57, 51, 13, 11, 57). The step's spending is unchanged.
        Assert.Equal((81L, 24L, 57L, 81L, 13L, 11L, 57L), (s.S, s.Eb, s.Er, s.Db, s.Build, s.Holds, s.Road));
        // Without the split (the control, the same step) the purse covers the set-aside AND the younger heads: the hold
        // binds here because the split confines building work to its share.
        SplitGoodFigures c = run.ControlRow(W).GoodOf(Stone)!;
        // D-058 (2026-10-19) RE-PIN: the control's build 40 -> 70 (every younger head continues). Batch-4: (40, 11).
        Assert.Equal((70L, 11L), (c.Build, c.Holds));
        // D-058 (2026-10-19) RE-PIN — was Assert.Empty: every head that STARTED the control's step still fits in the whole
        // purse (the 128 heads at settlements 0 and 2 are served), but the Director's "build one at a time" ruling carries
        // settlement 2's completed head's leftover capacity to its slot 1 in the same step, and THAT continuation — ordered at
        // 128, behind settlement 4's set-aside — is held for it once the purse runs out (MEASURED: s2:1, 0 -> 500,000). The
        // batch-4 control had no held head here (AllocationScenarioD058AttributionTests).
        Assert.Equal(new[] { (2, 1, 128L) }, HeldHeads(run.ControlRow(W)).Select(h => (h.Settlement, h.Slot, h.Enqueued)).ToArray());
    }

    [Fact]
    public void V_ThePartBuiltHeadIsCancelled_AndTheSameProjectReEnqueued_InOneBatch()
    {
        OrderRecord[] batch = All(run.Log.BatchFor(V));
        // D-056: the cancel carries the head's identity — slot 0, the granary, accepted at turn 126 (the progress row below).
        Assert.Contains(new OrderRecord(V, Player, OrderKind.CancelConstructionEntry, Third,
            OrderLog.PackCancelTarget(new CancelTarget(0, Granary, 126))), batch);
        Assert.Contains(new OrderRecord(V, Player, OrderKind.EnqueueConstruction, Third, Granary), batch);
        // When the batch is delivered the cancelled head is part-built (p > 0) and is settlement 4's only entry.
        ConstructionProgressRow before = All(run.World(V).ConstructionProgress).Single(r => r.Settlement.Value == Third);
        Assert.Equal((0, 126L, 422_236L), (before.Slot, before.EnqueuedTurn, before.Progress));
        // After it: one entry at settlement 4, the re-enqueued project, keyed by V's stamp turn, at progress 0 — no refund,
        // no inherited place, and it sets aside only what is left.
        ConstructionProgressRow after = All(run.World(V + 1).ConstructionProgress).Single(r => r.Settlement.Value == Third);
        Assert.Equal((0, Granary, V, 0L), (after.Slot, after.ProjectId, after.EnqueuedTurn, after.Progress));
        // D-058 (2026-10-19) RE-PIN. Batch 4: it was walked AFTER settlement 2's head ordered at 128, which drew 20, and set
        // aside the 18 left (reproduced on the PreContinueWithinTurn twin). Under the Director's "build one at a time" ruling
        // settlement 2's two granaries ordered at 128 completed together in step 131 -> 132, so at V the re-enqueued entry is
        // the only head and sets aside its whole 20.
        Assert.Equal(new[] { (Third, V) }, run.Row(V).Heads.Select(h => (h.Settlement, h.Enqueued)).ToArray());
        Assert.Equal((0L, 20L), (run.Row(V).Heads[0].GoodOf(Stone)!.Draw, run.Row(V).Heads[0].GoodOf(Stone)!.Hold));
    }

    [Fact]
    public void K_TheSavePoint_HoldsAPartBuiltHead_ASetAsideThatBindsAYoungerHead_TheSplitRow_AndAPartModernizedRoute()
    {
        WorldState k = run.World(K);
        // A head part-built to a progress that is not a multiple of 1,000 (the round trip of an arbitrary progress).
        Assert.Contains(All(k.ConstructionProgress), r => r.Progress > 0 && r.Progress < ConstructionQuery.ProgressUnits && r.Progress % 1000 != 0);
        // A NON-EMPTY ConstructionHolds table (schema v34 section 61) for the player's purse, stone among it ...
        Assert.NotEmpty(All(k.ConstructionHolds));
        Assert.Contains(new ConstructionHoldRow(Reservations.PolityPurse(new PolityId(Player)), Stone, 11), All(k.ConstructionHolds));
        // ... that binds a younger head in the step that produced world K (`split-check --expect-hold-at K`).
        SplitReportRow toK = Assert.Single(run.Rows, r => r.To == K);
        Assert.NotEmpty(HeldHeads(toK));
        Assert.True(SplitReportCheck.Check(run.Rows, expectHoldAt: K).Failures.All(f => !f.StartsWith("PK:", StringComparison.Ordinal)));
        // The split row (not the default) and a road part-way through its modernization.
        Assert.Equal(new ResourceAllocationRow(new PolityId(Player), 30), Assert.Single(All(k.ResourceAllocations)));
        Assert.Contains(All(k.TransportEdges), e => e.Modernization > 0.0 && e.Modernization < 1.0);
        // B's first step (from K) publishes a set-aside binding a younger head as well (`split-check b.rep --expect-hold-at K+1`).
        Assert.NotEmpty(HeldHeads(run.Row(K)));
        Assert.True(run.Row(K).GoodOf(Stone)!.Holds > 0);
    }

    [Fact]
    public void TheControl_BranchesFromThePreSplitWorld_AndDiffersFromAAtX()
    {
        // The same purse at P (the shared pre-split world), b = 100 throughout, and more building work at X.
        Assert.Equal(run.Row(P).GoodOf(Stone)!.S, run.ControlRow(P).GoodOf(Stone)!.S);
        Assert.All(run.ControlRows, r => Assert.Equal(100, r.BuildingsPercent));
        SplitGoodFigures a = run.Row(X).GoodOf(Stone)!, c = run.ControlRow(X).GoodOf(Stone)!;
        // D-058 (2026-10-19) RE-PIN: the control's build 40 -> 64 and road 24 -> 0 (b = 100: settlement 0's continuations take
        // the whole purse). Batch-4 values, reproduced on the twin: (64, 40, 0, 24).
        Assert.Equal((64L, 64L, 0L, 0L), (c.S, c.Build, c.Holds, c.Road));
        Assert.True(c.Build + c.Holds > a.Build + a.Holds, $"step {X}: the control claims {c.Build + c.Holds}, the split run {a.Build + a.Holds}");
        // Its worlds differ from A's from the first step (A's world P+1 holds the b = 30 row).
        Assert.NotEqual(run.Hashes[P + 1], run.ControlHashes[1]);
        Assert.NotEqual(run.Hashes[X + 1], run.ControlHashes[X + 1 - P]);
    }

    [Fact]
    public void Kind12_ChangesBTo70_FromTheStepOfItsStamp_WithHeadsThatNeedWhatItGrants()
    {
        Assert.Equal(30, run.Row(Seventy - 1).BuildingsPercent);
        Assert.Equal(70, run.Row(Seventy).BuildingsPercent);
        Assert.Equal(new ResourceAllocationRow(new PolityId(Player), 30), Assert.Single(All(run.World(Seventy).ResourceAllocations)));
        Assert.Equal(new ResourceAllocationRow(new PolityId(Player), 70), Assert.Single(All(run.World(Seventy + 1).ResourceAllocations)));
        SplitGoodFigures s = run.Row(Seventy).GoodOf(Stone)!;
        Assert.Equal((73L, 51L, 22L), (s.S, s.Eb, s.Er));   // E_b = floor(0.7 S)
        // T3 round 2 (2026-10-17, finding F1), MEASURED: the step that delivers the order HAS heads that need stone (three
        // granaries stamped in the same batch), the new share is binding there (D_r 73 >= E_r: G_b = E_b = 51 < D_b = 60), and
        // building work draws all of it — so spending under the new b is visible in the order's own step, where a split
        // applied a step late (CM04b) would draw the old share's 21. AllocationScenarioTests pins the twin turn-exact.
        Assert.Equal(3, run.Row(Seventy).Heads.Length);
        Assert.True(s.Gb == s.Eb && s.Gb < s.Db && s.Build == s.Gb,
            $"step {Seventy}: G_b {s.Gb} = E_b {s.Eb} < D_b {s.Db}, build {s.Build}");
        Assert.Equal((60L, 73L, 51L, 51L, 0L), (s.Db, s.Dr, s.Gb, s.Build, s.Holds));
        // The step BEFORE delivers nothing of b = 70 (no head there either: the granary at 4 was built in 135 -> 136).
        Assert.Equal(30, run.Row(Seventy - 1).BuildingsPercent);
    }

    [Fact]
    public void NoAiPolity_NoRevolt_NoColony_ByH()
    {
        // `sim replay` produces no AI orders, so it can reproduce the run only while no AI polity exists; a revolt founds
        // one, and a colony would change the settlement count. Every world 0..H of the run and of the control.
        Assert.All(run.PolityCounts, n => Assert.Equal(1, n));
        Assert.All(run.SettlementCounts, n => Assert.Equal(12, n));
        Assert.All(run.ControlPolityCounts.Skip(1), n => Assert.Equal(1, n));
        Assert.All(run.ControlSettlementCounts.Skip(1), n => Assert.Equal(12, n));
        Assert.Equal(H + 1, run.Hashes.Length);
        Assert.Equal(new PolityId(Player), Assert.Single(All(run.World(H).Polities)).Id);
    }

    [Fact]
    public void SplitCheck_PassesOnTheInProcessReports_AsTheStepRunsIt()
    {
        SplitCheckResult a = SplitReportCheck.Check(run.Rows, expectHoldAt: K);
        Assert.True(a.Passed, string.Join("\n", a.Failures));
        SplitCheckResult ac = SplitReportCheck.Check(run.Rows, run.ControlRows);
        Assert.True(ac.Passed, string.Join("\n", ac.Failures));
        SplitReportRow[] tail = run.Rows.Where(r => r.Turn >= K).ToArray();
        SplitCheckResult b = SplitReportCheck.Check(tail, expectHoldAt: K + 1);
        Assert.True(b.Passed, string.Join("\n", b.Failures));
        // P11 and P12 (T3 round 2) hold on every row of A, B and the control: the split's arithmetic and the walk re-derived.
        foreach (SplitCheckResult r in new[] { a, ac, b }) Assert.False(r.Failed("P11") || r.Failed("P12"));
        Assert.Contains(run.Rows, r => r.Heads.Length >= 2);   // P12's order clause is exercised, not vacuous
        SplitCheckResult c = SplitReportCheck.Check(run.ControlRows);
        Assert.False(c.Failed("P1") || c.Failed("P2") || c.Failed("P3") || c.Failed("P11") || c.Failed("P12"), string.Join("\n", c.Failures));
        Assert.Contains(run.ControlRows, r => r.Heads.Length > 0);   // P12's b = 100 start (S) is exercised on the control
    }

    // ================================================================== helpers

    private static (long, int, int) Key(SplitHeadFigures h) => (h.Enqueued, h.Settlement, h.Slot);

    /// <summary>The row's first binding good (build + holds = G_b &lt; D_b), or null.</summary>
    private static SplitGoodFigures? Binding(SplitReportRow r) =>
        r.BuildingsPercent is > 0 and < 100 && r.RoadPercent is not null
            ? r.Goods.FirstOrDefault(g => g.Build + g.Holds == g.Gb && g.Gb < g.Db) : null;

    /// <summary>The row's heads held for an earlier head on some good (split-check's P10 condition).</summary>
    private static SplitHeadFigures[] HeldHeads(SplitReportRow r) =>
        r.Heads.Where((h, i) => h.Goods.Any(g => SplitReportCheck.HeldBecauseOfAHold(r, i, g))).ToArray();

    private static string CiStep()
    {
        string ci = File.ReadAllText(Path.Combine(RepoRoot(), ".github", "workflows", "ci.yml")).Replace("\r\n", "\n", StringComparison.Ordinal);
        int start = ci.IndexOf("- name: " + StepName, StringComparison.Ordinal);
        Assert.True(start >= 0, $"ci.yml has no step '{StepName}'");
        int end = ci.IndexOf("\n      - name: ", start + 1, StringComparison.Ordinal);
        int job = ci.IndexOf("\n  calibration:", start, StringComparison.Ordinal);
        if (end < 0 || (job >= 0 && job < end)) end = job;
        Assert.True(end > start);
        string step = ci[start..end];
        Assert.DoesNotContain("--emit-session", step);   // a continued run writes no session; replay reads the generated log
        return step;
    }
}
