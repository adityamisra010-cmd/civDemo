using Sim.Cli;
using Sim.Core;
using Sim.Core.Kernel;
using Sim.Core.State;
using Sim.Core.Systems;
using Sim.Tests.Systems;
using Sim.Tests.TestUtil;

namespace Sim.Tests.Kernel;

/// <summary>
/// D-055 (2026-10-16; the Director's batch-4 instruction, item 4 — "Review partial construction, reservation state,
/// allocation settings, cancellation and re-enqueueing across save/load … and deterministic replay"; batch-4 design §4 P2
/// and P4; recon persistence-audit §7 P2 and P4) — THE BATTERY'S LIFECYCLE VARIANT: the battery's whole script on the full
/// production pipeline (the AI Empire included), plus five lifecycle orders, saved and loaded around each, with behavioural
/// assertions on the LOADED continuations (Snapshot.Load and the CLI's loader SaveFiles.Load) and on a REPLAYED run.
///
/// WHY A VARIANT (Rig.Extra, the battery-variant mechanism T1a added), NOT THE SCRIPTED RUN EXTENDED. MEASURED (2026-10-16,
/// by the agent writing this line; t4-out/probe-bat1.txt): every lifecycle state worth persisting needs a purse that is
/// CONTESTED, and in the scripted run only the capital's university contests the player's purse — it sets aside every unit
/// of timber from world 2 to world 21 and completes in step 21 → 22. After that the player's queue is empty except the
/// turn-36 granaries (each built in one step) and the timber purse grows to 1,545 by world 39, so an order after the last
/// measured save point (the recon's "turn 39 to 48") reaches no binding split and no binding hold. Adding the orders at
/// turns 1–10 of THE scripted run instead would move its measured save points (in this variant the first partial road is
/// world 8, not 23). The variant leaves the scripted run, its save points (1, 26, 23, 24), its part-built save point
/// (world 8 at 306,111) and the road order once the university stands untouched BY CONSTRUCTION, and <see
/// cref="OtherSaveTurns()"/> needs no new world: no test here saves a world of the scripted run except world 8, which the
/// part-built save point already saves (the named stall below).
///
/// THE BINDING SPLIT IS REACHABLE (design §4 P2, review L3-16: "MEASURE first"). The review's premise — the split binds
/// only with track_road + stone_dry, since timber is never scarce — holds for the CI world, not here: the battery cuts the
/// player's timber to 20 at turn 0 and the university holds all of it, so a 30/70 split with a track-road order binds on
/// TIMBER at once (step 7 → 8 below: road works are paid 215 timber that, with no split, the university's set-aside keeps).
/// No stone road or research script is needed.
///
/// THE LIFECYCLE ORDERS (<see cref="Lifecycle"/>, MEASURED on this tree, t4-out/probe-bat1.txt and probe-bat2.txt):
///  turn 1  — a granary G at the player's other holding (settlement 2; EnqueuedTurn 1), younger than the university U
///            (capital slot 1, EnqueuedTurn 0): G waits at p = 0 behind U's set-aside from world 2;
///  turn 4  — the capital's construction share to 0 (delivered by step 4 → 5, so U has NO BUILDERS in step 5 → 6: a VALID
///            wait — labour adults present — that keeps its set-aside; G draws nothing);
///  turn 5  — the share back to 60 (delivered by step 5 → 6; U is a viability wait in 6 → 7 and progresses in 7 → 8);
///  turn 7  — the player's split 30/70 (kind 12) and a road order; turn 8 a second road order (the split BINDS: step 7 → 8
///            pays the road 215 timber, 0 → 0.878 of route 1; without the kind-12 order it pays 0);
///  turn 10 — ONE BATCH: a cancel of U, the capital's HEAD at p = 233,888, and a university enqueued at the capital again
///            (the re-enqueue). Step 10 → 11 removes U before the walk, so G — now the oldest head — draws the released
///            stock and completes in that same step, and the new entry (slot 0, EnqueuedTurn 10) draws the leftover
///            (83,333). With no cancel, G is still at p = 0 in world 11.
/// </summary>
public partial class IntegratedSaveLoadBatteryTests
{
    private const long YoungerTurn = 1, NoBuildersTurn = 4, BuildersBackTurn = 5, SplitTurn = 7, ReEnqueueTurn = 10;
    private const int LifecycleHorizon = 14;
    private const double LifecycleSplit = 30.0, BuildersBack = 60.0;
    private const long CliSizePx = 256;

    private static int TimberId => Cfg.Goods!.IdOf("timber");
    private static int StoneId => Cfg.Goods!.IdOf("stone");

    /// <summary>The five lifecycle orders, a pure function of (turn, the world the player sees). <paramref name="split"/> and
    /// <paramref name="cancel"/> switch the kind-12 order and the cancel/re-enqueue batch off for the anti-vacuity controls.</summary>
    private static OrderRecord[] Lifecycle(IReadOnlyWorldState w, bool split, bool cancel)
    {
        long t = w.Clock.Turn;
        var o = new List<OrderRecord>();
        SettlementId cap = Capital(w);
        if (t == YoungerTurn) o.Add(ConstructionQuery.EnqueueOrder(w, Player, OtherHolding(w), Granary));
        if (t == NoBuildersTurn) o.Add(OrderRecord.From(t, Player, OrderKind.SectorAllocation, (cap.Value << 3) | Sectors.Construction, 0.0));
        if (t == BuildersBackTurn) o.Add(OrderRecord.From(t, Player, OrderKind.SectorAllocation, (cap.Value << 3) | Sectors.Construction, BuildersBack));
        if (split && t == SplitTurn) o.Add(OrderRecord.From(t, Player, OrderKind.SetResourceAllocation, Player.Value, LifecycleSplit));
        if (t is SplitTurn or SplitTurn + 1) o.Add(RoadDevelopmentQuery.DevelopOrder(w, Player, 100.0));
        if (cancel && t == ReEnqueueTurn && ConstructionQuery.Queue(w, cap) is { Length: > 0 } q)
        {
            o.Add(ConstructionQuery.CancelOrder(w, Player, cap, q[0].Slot));
            o.Add(ConstructionQuery.EnqueueOrder(w, Player, cap, UniversityProject));
        }
        return [.. o];
    }

    /// <summary>The variant's rig (a property: the battery's static rig lives in the other part of this partial class, and
    /// static initialisation order across partial files is not specified).</summary>
    private static Rig LifecycleRig => Shipped with { Extra = w => Lifecycle(w, split: true, cancel: true) };

    internal static readonly Lazy<Uninterrupted> LifecycleRun =
        new(() => RecordTo(LifecycleRig, LifecycleHorizon), LazyThreadSafetyMode.ExecutionAndPublication);

    /// <summary><see cref="Record"/> to <paramref name="turns"/> instead of the battery's horizon.</summary>
    internal static Uninterrupted RecordTo(Rig rig, int turns)
    {
        var log = new OrderLog();
        TurnExecutor exec = Production(log, rig);
        WorldState w = Start(rig);
        var worlds = new List<WorldState> { w.Clone() };
        var hashes = new List<string> { WorldHash.ComputeHex(w) };
        for (int t = 0; t < turns; t++)
        {
            OneTurnOfOrders(log, w, rig);
            w = exec.Step(w);
            worlds.Add(w.Clone());
            hashes.Add(WorldHash.ComputeHex(w));
        }
        return new Uninterrupted(worlds, hashes, log);
    }

    /// <summary>The save of <paramref name="w"/> loaded by the CLI's loader (`sim hash --size 256`): the bytes in a temp file,
    /// the terrain regenerated from the header and the topology trailer (the battery world is the shipped worldgen at 256
    /// px). A v34 save prints no migration note.</summary>
    private static WorldState ViaCliLoader(byte[] bytes)
    {
        string path = Path.Combine(Path.GetTempPath(), "civsim-battery-" + Guid.NewGuid().ToString("N") + ".civsnap");
        try
        {
            File.WriteAllBytes(path, bytes);
            WorldState w = SaveFiles.Load(path, CliSizePx, out string? note);
            Assert.Null(note);
            return w;
        }
        finally
        {
            File.Delete(path);
        }
    }

    /// <summary>
    /// <see cref="SaveLoadContinue"/> for any recorded run of any rig, on TWO loaded arms — Snapshot.Load and the CLI's
    /// SaveFiles.Load — and RETURNING each arm's worlds (element j = state k + j), so a test asserts behaviour on the loaded
    /// continuation itself, not only its hash. Each arm: the save of world k, the log as it stood at k (with the player's
    /// turn-k orders when <paramref name="issued"/>), a fresh executor, the player script (and the rig's Extra orders) and the
    /// AI producer re-run on the loaded state each turn; every turn's hash and the final log must equal the uninterrupted
    /// run's.
    /// </summary>
    private static (string Arm, List<WorldState> Worlds)[] SaveLoadContinueOn(
        Uninterrupted run, Rig rig, int horizon, int k, string label, bool issued = false)
    {
        WorldState atK = run.Worlds[k];
        var partial = new OrderLog();
        for (int i = 0; i < run.Log.Count; i++)
            if (run.Log[i].Turn < k || (issued && run.Log[i].Turn == k && run.Log[i].ActorId == Player.Value))
                partial.Append(run.Log[i]);
        byte[] bytes;
        using (var ms = new MemoryStream())
        {
            Snapshot.Save(atK, ms);
            bytes = ms.ToArray();
        }
        (string, WorldState)[] loaded =
        [
            ("Snapshot.Load", Snapshot.Load(new MemoryStream(bytes), atK.Terrain)),
            ("SaveFiles.Load", ViaCliLoader(bytes)),
        ];
        var result = new (string, List<WorldState>)[loaded.Length];
        for (int a = 0; a < loaded.Length; a++)
        {
            (string arm, WorldState w) = loaded[a];
            Assert.True(WorldStates.StateEquals(atK, w), $"{label} ({arm}): the loaded world differs from the saved one");
            Assert.Equal(run.Hashes[k], WorldHash.ComputeHex(w));
            OrderLog log = Copy(partial);
            TurnExecutor exec = Production(log, rig);
            var worlds = new List<WorldState> { w.Clone() };
            for (int t = k; t < horizon; t++)
            {
                if (issued && t == k) AiOrders.Append(log, w, rig.Cfg, rig.Production);
                else OneTurnOfOrders(log, w, rig);
                w = exec.Step(w);
                worlds.Add(w.Clone());
                Assert.True(run.Hashes[t + 1] == WorldHash.ComputeHex(w), $"{label} ({arm}, saved at {k}): diverged at turn {t + 1}");
            }
            // The log the continuation wrote equals the uninterrupted run's, up to the continuation's horizon.
            int expected = 0;
            for (int i = 0; i < run.Log.Count; i++) if (run.Log[i].Turn < horizon) expected++;
            Assert.Equal(expected, log.Count);
            for (int i = 0; i < log.Count; i++) Assert.Equal(run.Log[i], log[i]);
            result[a] = (arm, worlds);
        }
        return result;
    }

    /// <summary>One production step of <paramref name="w"/> (a clone) under <paramref name="rig"/>, delivering
    /// <paramref name="orders"/> (the batch stamped with w's turn).</summary>
    private static WorldState StepWith(Rig rig, WorldState w, IEnumerable<OrderRecord> orders)
    {
        var log = new OrderLog();
        foreach (OrderRecord o in orders) log.Append(o);
        return Production(log, rig).Step(w.Clone());
    }

    /// <summary>The orders of <paramref name="run"/>'s log stamped <paramref name="turn"/>, in log order, minus those
    /// <paramref name="drop"/> names.</summary>
    private static List<OrderRecord> BatchOf(Uninterrupted run, long turn, Func<OrderRecord, bool>? drop = null)
    {
        var batch = new List<OrderRecord>();
        for (int i = 0; i < run.Log.Count; i++)
            if (run.Log[i].Turn == turn && (drop is null || !drop(run.Log[i]))) batch.Add(run.Log[i]);
        return batch;
    }

    private static long Held(IReadOnlyWorldState w, int good)
    {
        long sum = 0;
        for (int i = 0; i < w.ConstructionHolds.Count; i++)
            if (w.ConstructionHolds[i].Purse == Reservations.PolityPurse(Player) && w.ConstructionHolds[i].Good == good)
                sum += w.ConstructionHolds[i].Units;
        return sum;
    }

    private static long PurseOf(IReadOnlyWorldState w, int good) =>
        EmpireStockpile.Available(w.GoodStocks, ConstructionQuery.Holders(w, Cfg, Capital(w)), new GoodId(good));

    /// <summary>The road units the player's road order stamped <paramref name="turn"/> paid (one row per route).</summary>
    private static long RoadUnits(IReadOnlyWorldState w, long turn)
    {
        long sum = 0;
        for (int i = 0; i < w.RoadDevelopments.Count; i++)
            if (w.RoadDevelopments[i].Turn == turn && w.RoadDevelopments[i].Polity == Player) sum += w.RoadDevelopments[i].MaterialUnits;
        return sum;
    }

    private static double CapitalShare(IReadOnlyWorldState w)
    {
        for (int i = 0; i < w.SectorAllocations.Count; i++)
            if (w.SectorAllocations[i].Settlement == Capital(w)) return w.SectorAllocations[i].Construction;
        return double.NaN;
    }

    private static ConstructionQueueRow UniversityEntry(IReadOnlyWorldState w) => new(Capital(w), 1, UniversityProject);

    private static ConstructionQueueRow GranaryEntry(IReadOnlyWorldState w) => new(OtherHolding(w), 0, Granary);

    private static ConstructionQueueRow ReEnqueued(IReadOnlyWorldState w) => new(Capital(w), 0, UniversityProject);

    private static bool Queued(IReadOnlyWorldState w, ConstructionQueueRow e) => ConstructionQuery.ProgressIndex(w.ConstructionProgress, e) >= 0;

    private static void Eq<T>(T expected, T actual, string where) =>
        Assert.True(EqualityComparer<T>.Default.Equals(expected, actual), $"{where} — expected {expected}, got {actual}");

    // ======================================================================== the variant's own states (premises)

    /// <summary>
    /// The lifecycle variant reaches each state it exists for, on the uninterrupted run, with an anti-vacuity control for
    /// each: G waits behind U's set-aside (PreReservations builds it at once); U has no builders in step 5 → 6 (Δp = 0, a
    /// labour wait with adults present: not released) while G draws nothing; the split binds in step 7 → 8 (215 road units;
    /// 0 without the kind-12 order); the turn-10 batch removes U and its row, G completes in that step, and the re-enqueued
    /// university is a NEW entry keyed by the stamp turn (no cancel: G still waits).
    /// </summary>
    [Fact]
    public void TheLifecycleVariant_ReachesEveryLifecycleState_OnTheFullPipeline_EachAgainstItsControl()
    {
        Uninterrupted run = LifecycleRun.Value;
        List<WorldState> w = run.Worlds;
        ConstructionQueueRow u = UniversityEntry(w[0]), g = GranaryEntry(w[0]);
        Assert.Equal((2, 0), (OtherHolding(w[0]).Value, Capital(w[0]).Value));

        // G waits behind the university's set-aside from world 2 (batch 3 builds it in step 1 → 2).
        for (int k = 2; k <= 10; k++)
        {
            Eq(0L, ConstructionQuery.ProgressOf(w[k], g), $"world {k}: G's progress");
            Assert.True(Queued(w[k], u), $"world {k}: U is queued");
        }
        Assert.Equal(1L, ConstructionQuery.EnqueuedTurnOf(w[2].ConstructionProgress, g));
        Uninterrupted pre = RecordTo(LifecycleRig with { Cfg = TestConfigs.PreReservations(Cfg) }, 2);
        Assert.Equal(1L, ConstructionQuery.Built(pre.Worlds[2], OtherHolding(pre.Worlds[2]), Granary));

        // No builders: the share row is 0 in world 5 only; U waits in step 5 → 6 on labour, not released.
        Assert.Equal((0.6, 0.0, 0.6), (CapitalShare(w[4]), CapitalShare(w[5]), CapitalShare(w[6])));
        HeadStatusReport s5 = ConstructionQuery.HeadStatus(w[5], Cfg, Capital(w[5]), [], 10.0)!;
        Assert.Equal((HeadLimitKind.Labour, false, HoldRelease.None), (s5.Resolution.Limit, s5.Resolution.NoLabourAdults, s5.Release));
        Assert.Equal((182_777L, 182_777L), (ConstructionQuery.ProgressOf(w[5], u), ConstructionQuery.ProgressOf(w[6], u)));
        Assert.Equal((162L, 980L), (Held(w[6], TimberId), Held(w[6], StoneId)));
        Assert.Equal((PurseOf(w[6], TimberId), PurseOf(w[6], StoneId)), (Held(w[6], TimberId), Held(w[6], StoneId)));

        // The split binds in step 7 → 8.
        Assert.Equal(0, w[7].ResourceAllocations.Count);
        Assert.Equal(new ResourceAllocationRow(Player, (int)LifecycleSplit), Assert.Single(TableRows.Rows(w[8].ResourceAllocations)));
        Assert.Equal((215L, 233_888L), (RoadUnits(w[8], SplitTurn), ConstructionQuery.ProgressOf(w[8], u)));
        Uninterrupted noSplit = RecordTo(Shipped with { Extra = x => Lifecycle(x, split: false, cancel: true) }, (int)SplitTurn + 1);
        Assert.Equal(run.Hashes[(int)SplitTurn], noSplit.Hashes[(int)SplitTurn]);
        Assert.Equal((0L, 353_333L), (RoadUnits(noSplit.Worlds[8], SplitTurn), ConstructionQuery.ProgressOf(noSplit.Worlds[8], u)));

        // The cancel and the re-enqueue: U and its row gone, G built, the new entry keyed by the stamp turn.
        Assert.Equal(233_888L, ConstructionQuery.ProgressOf(w[10], u));
        Assert.False(Queued(w[11], u));
        Assert.Equal(1L, ConstructionQuery.Built(w[11], OtherHolding(w[11]), Granary));
        Assert.Equal(0L, ConstructionQuery.Built(w[10], OtherHolding(w[10]), Granary));
        Assert.Equal((10L, 83_333L), (ConstructionQuery.EnqueuedTurnOf(w[11].ConstructionProgress, ReEnqueued(w[11])), ConstructionQuery.ProgressOf(w[11], ReEnqueued(w[11]))));
        Uninterrupted noCancel = RecordTo(Shipped with { Extra = x => Lifecycle(x, split: true, cancel: false) }, (int)ReEnqueueTurn + 1);
        Assert.Equal(run.Hashes[(int)ReEnqueueTurn], noCancel.Hashes[(int)ReEnqueueTurn]);
        Assert.Equal((0L, 0L), (ConstructionQuery.ProgressOf(noCancel.Worlds[11], g), ConstructionQuery.Built(noCancel.Worlds[11], OtherHolding(w[11]), Granary)));
    }

    // ======================================================================== P2: saved and loaded around each order

    /// <summary>
    /// CONSTRUCTION SHARE 0 AND BACK — A HOLD ACROSS THE SAVE. Saved at 4 with the share-0 order issued (pending), at 5 (no
    /// builders in force; the restore order issued) and at 6 (restored). On each loaded continuation: the share row is 0 in
    /// state 5 only; in step 5 → 6 U keeps p = 182,777 (a labour wait with adults present — HeadStatus on the LOADED world 5:
    /// Labour, not NoLabourAdults, not released) and sets aside the purse's whole timber and stone (162 and 980), and G draws
    /// nothing. BINDING: the loaded world 5 stepped by the PreReservations pipeline with the same batch builds G (+40 timber
    /// and +20 stone sunk). Then U progresses again in step 7 → 8 (233,888).
    /// </summary>
    [Fact]
    public void SaveLoad_LifecycleVariant_NoBuildersAndBack_TheValidWaitKeepsItsSetAside_AndBindsTheYoungerHead()
    {
        Uninterrupted run = LifecycleRun.Value;
        foreach ((string arm, List<WorldState> c) in SaveLoadContinueOn(run, LifecycleRig, LifecycleHorizon, (int)NoBuildersTurn, "share 0 issued", issued: true))
        {
            Eq((0.6, 0.0, 0.6), (CapitalShare(c[0]), CapitalShare(c[1]), CapitalShare(c[2])), arm + ": the share rows at 4, 5, 6");
            Eq(182_777L, ConstructionQuery.ProgressOf(c[2], UniversityEntry(c[2])), arm + ": U at 6");
        }
        foreach ((string arm, List<WorldState> c) in SaveLoadContinueOn(run, LifecycleRig, LifecycleHorizon, (int)BuildersBackTurn, "builders back issued", issued: true))
        {
            WorldState loaded = c[0], next = c[1];
            HeadStatusReport s = ConstructionQuery.HeadStatus(loaded, Cfg, Capital(loaded), [], 10.0)!;
            Eq((HeadLimitKind.Labour, false, HoldRelease.None), (s.Resolution.Limit, s.Resolution.NoLabourAdults, s.Release), arm + ": U on the loaded world 5");
            Eq(HoldRelease.None, ConstructionQuery.ReleaseOf(loaded, Cfg, UniversityEntry(loaded)), arm + ": U's release");
            Eq((182_777L, 182_777L), (ConstructionQuery.ProgressOf(loaded, UniversityEntry(loaded)), ConstructionQuery.ProgressOf(next, UniversityEntry(next))), arm + ": U waits in 5 -> 6");
            Eq((162L, 980L), (Held(next, TimberId), Held(next, StoneId)), arm + ": U's set-aside at 6");
            Eq((PurseOf(next, TimberId), PurseOf(next, StoneId)), (Held(next, TimberId), Held(next, StoneId)), arm + ": the whole purse set aside");
            Eq(0L, ConstructionQuery.ProgressOf(next, GranaryEntry(next)), arm + ": G draws nothing in 5 -> 6");
            Eq((Sim.Tests.Systems.IncrementalConstructionTests.Sunk(loaded, TimberId), Sim.Tests.Systems.IncrementalConstructionTests.Sunk(loaded, StoneId)),
                (Sim.Tests.Systems.IncrementalConstructionTests.Sunk(next, TimberId), Sim.Tests.Systems.IncrementalConstructionTests.Sunk(next, StoneId)), arm + ": nothing drawn in 5 -> 6");
            // BINDING: the batch-3 rule on the same loaded world builds G in that step.
            WorldState batch3 = StepWith(LifecycleRig with { Cfg = TestConfigs.PreReservations(Cfg) }, loaded, BatchOf(run, BuildersBackTurn));
            Eq(1L, ConstructionQuery.Built(batch3, OtherHolding(batch3), Granary), arm + ": PreReservations builds G from the loaded world 5");
            Eq((Sim.Tests.Systems.IncrementalConstructionTests.Sunk(loaded, TimberId) + 40, Sim.Tests.Systems.IncrementalConstructionTests.Sunk(loaded, StoneId) + 20),
                (Sim.Tests.Systems.IncrementalConstructionTests.Sunk(batch3, TimberId), Sim.Tests.Systems.IncrementalConstructionTests.Sunk(batch3, StoneId)), arm + ": PreReservations draws G's 40 timber and 20 stone");
            Eq((0.6, 233_888L), (CapitalShare(next), ConstructionQuery.ProgressOf(c[3], UniversityEntry(c[3]))), arm + ": builders back, U progresses in 7 -> 8");
        }
        SaveLoadContinueOn(run, LifecycleRig, LifecycleHorizon, (int)BuildersBackTurn + 1, "builders restored");
    }

    /// <summary>
    /// A BINDING SPLIT ISSUED AT THE SAVE TURN (R8) AND IN FORCE AT THE SAVE (R7), on the full pipeline. Saved at 7 with the
    /// kind-12 order and the road order issued: on each loaded continuation state 7 has no split row and state 8 has (1, 30);
    /// step 7 → 8 pays the road 215 timber and U reaches 233,888 — the order governs the step that delivers it. ANTI-VACUITY
    /// on the LOADED world 7: the same batch without the kind-12 order pays the road nothing and U reaches 353,333 (kills
    /// "kind-12 upserted after building work" on this path). Saved at 8 (in force): step 8 → 9 sets aside exactly
    /// E_b = floor(S × 30 / 100) of the timber S that building work and road works share, and road works are paid the rest,
    /// E_r (25 and 60 of 85); the loaded world's split forecast (StepAllocations) equals the uninterrupted world's.
    /// </summary>
    [Fact]
    public void SaveLoad_LifecycleVariant_ABindingSplitIssuedAtTheSaveTurn_GovernsTheStepAfterTheLoad_AndBindsWhileInForce()
    {
        Uninterrupted run = LifecycleRun.Value;
        foreach ((string arm, List<WorldState> c) in SaveLoadContinueOn(run, LifecycleRig, LifecycleHorizon, (int)SplitTurn, "split issued", issued: true))
        {
            WorldState loaded = c[0], next = c[1];
            Eq(0, loaded.ResourceAllocations.Count, arm + ": no split row at 7");
            Eq(new ResourceAllocationRow(Player, (int)LifecycleSplit), Assert.Single(TableRows.Rows(next.ResourceAllocations)), arm + ": the row at 8");
            Eq((215L, 233_888L), (RoadUnits(next, SplitTurn), ConstructionQuery.ProgressOf(next, UniversityEntry(next))), arm + ": step 7 -> 8 under the split");
            List<OrderRecord> withoutSplit = BatchOf(run, SplitTurn, o => o.Kind == OrderKind.SetResourceAllocation);
            Eq(BatchOf(run, SplitTurn).Count - 1, withoutSplit.Count, arm + ": the control drops exactly the kind-12 order");
            WorldState control = StepWith(LifecycleRig, loaded, withoutSplit);
            Eq((0L, 353_333L), (RoadUnits(control, SplitTurn), ConstructionQuery.ProgressOf(control, UniversityEntry(control))), arm + ": the same step without the split");
        }
        WorldState never = run.Worlds[(int)SplitTurn + 1];
        foreach ((string arm, List<WorldState> c) in SaveLoadContinueOn(run, LifecycleRig, LifecycleHorizon, (int)SplitTurn + 1, "split in force"))
        {
            WorldState loaded = c[0], next = c[1];
            long held = Held(next, TimberId), road = RoadUnits(next, SplitTurn + 1);
            Eq((25L, 60L), (held, road), arm + ": step 8 -> 9 timber set aside / paid to road works");
            Eq(ResourceAllocation.BuildingShare(held + road, (int)LifecycleSplit), held, arm + ": the set-aside is building work's share E_b");
            Eq(233_888L, ConstructionQuery.ProgressOf(next, UniversityEntry(next)), arm + ": U waits on viability in 8 -> 9");
            Assert.True(held < ConstructionQuery.Remaining(1800, 233_888), $"{arm}: the grant caps the set-aside below U's remaining need");
            EmpireAllocation?[] a = ConstructionQuery.StepAllocations(loaded, Cfg, loaded.ConstructionQueue, loaded.ConstructionProgress, loaded.GoodStocks,
                loaded.ResourceAllocations, BatchOf(run, SplitTurn + 1), 10.0)!;
            EmpireAllocation?[] b = ConstructionQuery.StepAllocations(never, Cfg, never.ConstructionQueue, never.ConstructionProgress, never.GoodStocks,
                never.ResourceAllocations, BatchOf(run, SplitTurn + 1), 10.0)!;
            Eq(b.Length, a.Length, arm + ": allocations");
            for (int i = 0; i < a.Length; i++)
            {
                Eq(b[i] is null, a[i] is null, arm + ": allocation presence");
                if (a[i] is not { } x || b[i] is not { } y) continue;
                Eq((y.Polity, y.BuildingsPercent, y.RoadOrder), (x.Polity, x.BuildingsPercent, x.RoadOrder), arm + ": the split and road order");
                Assert.Equal(y.Splits, x.Splits);
                Assert.Equal(y.BuildingGrants, x.BuildingGrants);
            }
        }
    }

    /// <summary>
    /// A HEAD CANCEL WITH p &gt; 0 AND A RE-ENQUEUE IN ONE BATCH (R4, R5), saved with the batch issued (state 10) and after
    /// delivery (state 11). On each loaded continuation, step 10 → 11: U (capital slot 1, p = 233,888) and its progress row are
    /// gone; the cancel is applied BEFORE the walk, so G — now the oldest head — draws the released stock and completes in
    /// that same step (Built 0 → 1); the re-enqueued university is a NEW entry (slot 0, EnqueuedTurn 10, p 83,333) served
    /// BEHIND G; and nothing is refunded: the construction sinks grow by exactly G's 40 timber and 20 stone plus the new
    /// entry's Delivered(1,800, 83,333) = 150 and Delivered(1,200, 83,333) = 100. ANTI-VACUITY on the LOADED world 10: the
    /// same batch without the cancel and the enqueue leaves G at p = 0 (U still sets the stock aside) — kills "the cancel
    /// applied after building work" on this path. Saved at 11: the new key survives the load row for row.
    /// </summary>
    [Fact]
    public void SaveLoad_LifecycleVariant_AHeadCancelWithProgressAndAReEnqueue_AreAppliedBeforeTheWalk_AfterTheLoad()
    {
        Uninterrupted run = LifecycleRun.Value;
        foreach ((string arm, List<WorldState> c) in SaveLoadContinueOn(run, LifecycleRig, LifecycleHorizon, (int)ReEnqueueTurn, "cancel and re-enqueue issued", issued: true))
        {
            WorldState loaded = c[0], next = c[1];
            ConstructionQueueRow u = UniversityEntry(loaded), g = GranaryEntry(loaded), n = ReEnqueued(next);
            Eq((233_888L, 0L, 0L), (ConstructionQuery.ProgressOf(loaded, u), ConstructionQuery.ProgressOf(loaded, g), ConstructionQuery.Built(loaded, g.Settlement, Granary)), arm + ": state 10");
            Eq(false, Queued(next, u), arm + ": U's entry and row gone at 11");
            Eq(-1, ConstructionQuery.ProgressIndex(next.ConstructionProgress, g), arm + ": G's row gone (completed)");
            Eq(1L, ConstructionQuery.Built(next, g.Settlement, Granary), arm + ": G built in the cancel's step");
            Eq((10L, 83_333L), (ConstructionQuery.EnqueuedTurnOf(next.ConstructionProgress, n), ConstructionQuery.ProgressOf(next, n)), arm + ": the re-enqueued entry");
            Eq(0L, ConstructionQuery.Built(next, Capital(next), UniversityProject), arm + ": no university built");
            Eq((Sim.Tests.Systems.IncrementalConstructionTests.Sunk(loaded, TimberId) + 40 + 150, Sim.Tests.Systems.IncrementalConstructionTests.Sunk(loaded, StoneId) + 20 + 100),
                (Sim.Tests.Systems.IncrementalConstructionTests.Sunk(next, TimberId), Sim.Tests.Systems.IncrementalConstructionTests.Sunk(next, StoneId)), arm + ": sunk = G's draw + the new entry's, no refund");
            List<OrderRecord> without = BatchOf(run, ReEnqueueTurn, o => o.Kind is OrderKind.CancelConstruction || (o.Kind == OrderKind.EnqueueConstruction && o.ActorId == Player.Value));
            Eq(BatchOf(run, ReEnqueueTurn).Count - 2, without.Count, arm + ": the control drops exactly the cancel and the enqueue");
            WorldState control = StepWith(LifecycleRig, loaded, without);
            Eq((0L, 0L, true), (ConstructionQuery.ProgressOf(control, g), ConstructionQuery.Built(control, g.Settlement, Granary), Queued(control, u)), arm + ": without the batch G still waits behind U");
        }
        foreach ((string arm, List<WorldState> c) in SaveLoadContinueOn(run, LifecycleRig, LifecycleHorizon, (int)ReEnqueueTurn + 1, "re-enqueue delivered"))
        {
            ConstructionQueueRow n = ReEnqueued(c[0]);
            Eq(10L, ConstructionQuery.EnqueuedTurnOf(c[0].ConstructionProgress, n), arm + ": the key after the load");
            Eq(run.Worlds[(int)ReEnqueueTurn + 1].ConstructionProgress[ConstructionQuery.ProgressIndex(run.Worlds[(int)ReEnqueueTurn + 1].ConstructionProgress, n)],
                c[0].ConstructionProgress[ConstructionQuery.ProgressIndex(c[0].ConstructionProgress, n)], arm + ": the row, field for field");
            Assert.True(ConstructionQuery.ProgressOf(c[2], n) > 83_333L, $"{arm}: the new entry progresses after the load");
        }
    }

    /// <summary>
    /// THE NAMED STALL (recon P2 "name the stall"): the battery's part-built save point, world 8 of the SCRIPTED run, is a
    /// VIABILITY wait — the capital's university at 306,111 cannot be sustained (its host's food-surplus ratio is below the
    /// threshold: a market failure, transient), which HeadResolution counts as structurally stalled for the AI and the display
    /// but which the hold rule keeps HOLDING (ReleaseOf None: only NoConfig releases). Saved and loaded (both arms), the
    /// loaded world names the same stall, and its next state keeps p = 306,111 and sets aside the purse's whole timber (83)
    /// and the university's remaining stone, Remaining(1,200, 306,111) = 832 — the units world 9 of the scripted run holds.
    /// BINDING (the lifecycle variant, whose university is in a viability wait in step 9 → 10): the younger granary there draws
    /// nothing from the loaded world 9, while the PreReservations pipeline builds it from the same world.
    /// </summary>
    [Fact]
    public void TheScriptedRunsPartBuiltSavePoint_IsANamedViabilityWait_ThatKeepsItsSetAsideAcrossTheSave()
    {
        Uninterrupted main = Run.Value;
        Assert.Equal(8, PartBuiltSavePoint(main.Worlds));
        foreach ((string arm, List<WorldState> c) in SaveLoadContinueOn(main, Shipped, 10, 8, "the named stall"))
        {
            WorldState loaded = c[0], next = c[1];
            ConstructionQueueRow u = UniversityEntry(loaded);
            HeadStatusReport s = ConstructionQuery.HeadStatus(loaded, Cfg, Capital(loaded), [], 10.0)!;
            Eq((HeadLimitKind.Viability, true, false, HoldRelease.None), (s.Resolution.Limit, s.Resolution.StructurallyStalled, s.Resolution.NoLabourAdults, s.Release), arm + ": the stall at 8");
            Eq(ViabilityFailure.FoodSurplus, InstitutionViability.ToFound(loaded, Cfg, Capital(loaded)).Failure, arm + ": why");
            Eq(HoldRelease.None, ConstructionQuery.ReleaseOf(loaded, Cfg, u), arm + ": a viability wait is not released");
            Eq((306_111L, 306_111L), (ConstructionQuery.ProgressOf(loaded, u), ConstructionQuery.ProgressOf(next, u)), arm + ": Δp = 0 in 8 -> 9");
            Eq((PurseOf(next, TimberId), 83L), (Held(next, TimberId), Held(next, TimberId)), arm + ": the whole timber purse set aside at 9");
            Eq(ConstructionQuery.Remaining(1200, 306_111), Held(next, StoneId), arm + ": the remaining stone set aside at 9");
            Eq(832L, Held(next, StoneId), arm + ": 832 stone");
        }
        Uninterrupted variant = LifecycleRun.Value;
        foreach ((string arm, List<WorldState> c) in SaveLoadContinueOn(variant, LifecycleRig, 11, 9, "a viability wait binds"))
        {
            WorldState loaded = c[0], next = c[1];
            HeadStatusReport s = ConstructionQuery.HeadStatus(loaded, Cfg, Capital(loaded), [], 10.0)!;
            Eq((HeadLimitKind.Viability, HoldRelease.None), (s.Resolution.Limit, s.Release), arm + ": the variant's U at 9");
            HeadStatusReport sg = ConstructionQuery.HeadStatus(loaded, Cfg, OtherHolding(loaded), [], 10.0)!;
            Eq((MaterialCause.HeldForEarlier, (QueueEntryRef?)new QueueEntryRef(Capital(loaded), 1, UniversityProject)), (sg.Cause, sg.Holder), arm + ": G is held for U");
            Eq(0L, ConstructionQuery.ProgressOf(next, GranaryEntry(next)), arm + ": G draws nothing in 9 -> 10");
            WorldState batch3 = StepWith(LifecycleRig with { Cfg = TestConfigs.PreReservations(Cfg) }, loaded, BatchOf(variant, 9));
            Eq(1L, ConstructionQuery.Built(batch3, OtherHolding(batch3), Granary), arm + ": PreReservations builds G from the loaded world 9");
        }
    }

    // ======================================================================== P4: turn-exact pins on the REPLAYED run

    /// <summary>
    /// REPLAY (design §4 P4; CLAUDE.md, the T1.9 precedent: replay equality proves reproducibility, not semantics): the
    /// variant's run log — the player's script, the lifecycle orders and the AI's orders — saved (OrderLog.Save) and loaded
    /// back, validated up front against turn 0 as every replay path does, and replayed from the founded world by a fresh
    /// executor with NO order producer. Besides the hash of every turn, each order's delivery is pinned turn-exact ON THE
    /// REPLAYED RUN: the share-0 order (4) is in the row of state 5 and U waits in 5 → 6; the kind-12 order (7) is in no row
    /// before state 8 and governs step 7 → 8 (215 road units); the cancel (10) leaves U in state 10 and removes it in state 11
    /// having drawn nothing (the sinks grow by exactly G's and the new entry's draws); the re-enqueued entry's key is 10.
    /// </summary>
    [Fact]
    public void TheLifecycleVariant_ReplayedFromItsSavedLog_PinsEveryOrderTurnExact_OnTheReplayedRun()
    {
        Uninterrupted live = LifecycleRun.Value;
        OrderLog log;
        using (var ms = new MemoryStream())
        {
            live.Log.Save(ms);
            ms.Position = 0;
            log = OrderLog.Load(ms);
        }
        Assert.Equal(live.Log.Count, log.Count);
        WorldState start = Start(LifecycleRig);
        OrderValidation.ValidateAgainstWorld(log, start);
        TurnExecutor exec = Production(log, LifecycleRig);
        var r = new List<WorldState> { start.Clone() };
        WorldState w = start;
        for (int t = 0; t < LifecycleHorizon; t++)
        {
            w = exec.Step(w);
            r.Add(w.Clone());
            Assert.True(live.Hashes[t + 1] == WorldHash.ComputeHex(w), $"the replay diverged at turn {t + 1}");
        }
        Eq((0.6, 0.0, 0.6), (CapitalShare(r[4]), CapitalShare(r[5]), CapitalShare(r[6])), "replayed: the share rows at 4, 5, 6");
        Eq((182_777L, 182_777L), (ConstructionQuery.ProgressOf(r[5], UniversityEntry(r[5])), ConstructionQuery.ProgressOf(r[6], UniversityEntry(r[6]))), "replayed: U waits in 5 -> 6");
        Eq((162L, 0L), (Held(r[6], TimberId), ConstructionQuery.ProgressOf(r[6], GranaryEntry(r[6]))), "replayed: U sets aside, G draws nothing");
        Eq((0, 0L), (r[7].ResourceAllocations.Count, RoadUnits(r[7], SplitTurn)), "replayed: no row and no road units at 7");
        Eq((new ResourceAllocationRow(Player, (int)LifecycleSplit), 215L), (Assert.Single(TableRows.Rows(r[8].ResourceAllocations)), RoadUnits(r[8], SplitTurn)), "replayed: the split governs 7 -> 8");
        Eq((true, 233_888L), (Queued(r[10], UniversityEntry(r[10])), ConstructionQuery.ProgressOf(r[10], UniversityEntry(r[10]))), "replayed: U at 10");
        Eq(false, Queued(r[11], UniversityEntry(r[11])), "replayed: U gone at 11");
        Eq((1L, 10L, 83_333L), (ConstructionQuery.Built(r[11], OtherHolding(r[11]), Granary),
            ConstructionQuery.EnqueuedTurnOf(r[11].ConstructionProgress, ReEnqueued(r[11])), ConstructionQuery.ProgressOf(r[11], ReEnqueued(r[11]))), "replayed: G built, the new key");
        Eq((Sim.Tests.Systems.IncrementalConstructionTests.Sunk(r[10], TimberId) + 190, Sim.Tests.Systems.IncrementalConstructionTests.Sunk(r[10], StoneId) + 120),
            (Sim.Tests.Systems.IncrementalConstructionTests.Sunk(r[11], TimberId), Sim.Tests.Systems.IncrementalConstructionTests.Sunk(r[11], StoneId)), "replayed: the cancelled head drew nothing in 10 -> 11");
    }
}
