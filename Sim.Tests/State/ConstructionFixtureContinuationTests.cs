using Sim.Cli;
using Sim.Core;
using Sim.Core.Kernel;
using Sim.Core.State;
using Sim.Core.Systems;
using Sim.Core.Worldgen;
using Sim.Tests.Systems;
using Sim.Tests.TestUtil;
using C = Sim.Tests.Systems.IncrementalConstructionTests;

namespace Sim.Tests.State;

/// <summary>
/// D-055 (2026-10-16; the Director's batch-4 instruction, item 4 — "… across save/load, schema-v32 migration …"; batch-4
/// design §4 P6 and X1 "Migration semantics"; recon persistence-audit §7 P6) — THE REAL OLD-SCHEMA FIXTURES, CONTINUED WITH
/// LIFECYCLE ORDERS. Each case loads a fixture an OLDER build wrote and continues it with orders stamped at its save turn K,
/// on several ARMS stepped by the SAME executor over the same log, which must be hash-identical on every turn:
///  — the batch-N TWIN's world at K (X1 (b): the world an in-process run of the fixture's own orders under the older
///    build's rules reaches — for the v32 fixture the PreD054 pair, TestConfigs.PreD054Sim + PreD054Worldgen; for the v33
///    fixture TestConfigs.PreReservations — which publishes no row the migration would lack);
///  — the fixture loaded (Snapshot.Load: the migration);
///  — the fixture re-saved as v34 and reloaded (X1 (d));
///  — the re-save loaded by the CLI's loader (SaveFiles.Load, `sim hash`), which regenerates the terrain from the header.
/// So the migrated world continues exactly as the older build's own world does under THIS build's rules (X1 (c)), with
/// each order's effect pinned turn-exact on the loaded continuation and an anti-vacuity control for each (X1 (e)).
///
/// The fixtures: v32-seed42-turn40.civsnap (batch 2, flat, turn 40: two granaries queued at settlement 0, which has no
/// builders, migrated row-less, EnqueuedTurn −1, p = 0; README-v32-fixture.md) and v33-seed42-turn2.civsnap (batch 3, the
/// cylinder at 256 px, turn 2: the D-054 Q17 shape — s0's granary part-built at 700,000 without builders, s2's at 0, the
/// player's 30/70 split; README-v33-fixture.md). MEASURED (2026-10-16, by the agent writing this line; t4-out/probe-p6.txt,
/// probe-p6b.txt): the PreD054 pair, run 40 turns from the founded world with the v32 fixture's three orders and the AI
/// producer (as `sim run --founded --ai-empires 1` runs), reaches the fixture's world exactly — StateEquals and the hash,
/// and its v32 projection is b000b2ee…, the hash the v32 build printed.
/// </summary>
public class ConstructionFixtureContinuationTests
{
    // M6 Track EX (2026-10-22): the D-055 shipped rules — the newer discovery layer off (TestConfigs.PreDiscovery, R4): its
    // concealment changes the dev purse's stone (MEASURED: "state 3 — expected (700000, 6, 150000), got (700000, 4, 0)").
    private static readonly SimConfig Shipped = TestConfigs.PreDiscovery(TestConfigs.Sim());
    private static readonly PolityId Player = new(1);
    private static readonly SettlementId S0 = new(0), S2 = new(2);
    private static readonly int Timber = Shipped.Goods!.IdOf("timber"), Stone = Shipped.Goods!.IdOf("stone");
    private const int Granary = 1;
    private static readonly TimeSpan Limit = TimeSpan.FromMinutes(4);

    // ======================================================================== shared rig

    private static byte[] Saved(WorldState w)
    {
        using var ms = new MemoryStream();
        Snapshot.Save(w, ms);
        return ms.ToArray();
    }

    /// <summary>The CLI's loader on <paramref name="bytes"/> (written to a temp file): a v34 file prints no note.</summary>
    private static WorldState ViaCli(byte[] bytes, long sizePx)
    {
        string path = Path.Combine(Path.GetTempPath(), "civsim-fixture-cont-" + Guid.NewGuid().ToString("N") + ".civsnap");
        try
        {
            File.WriteAllBytes(path, bytes);
            WorldState w = SaveFiles.Load(path, sizePx, out string? note);
            Assert.Null(note);
            return w;
        }
        finally
        {
            File.Delete(path);
        }
    }

    private static TurnExecutor Production(SimConfig cfg, WorldgenConfig worldgen, OrderLog orders)
    {
        using var era = Sim.Data.DataFiles.OpenEraPacing();
        using var pipe = Sim.Data.DataFiles.OpenPipeline();
        return new TurnExecutor(EraTableLoader.Load(era), PipelineLoader.Load(pipe, SystemCatalog.All(cfg, worldgen)), orders);
    }

    private static OrderLog With(OrderLog? baseLog, params OrderRecord[] extra)
    {
        var log = new OrderLog();
        if (baseLog is not null) for (int i = 0; i < baseLog.Count; i++) log.Append(baseLog[i]);
        foreach (OrderRecord o in extra) log.Append(o);
        return log;
    }

    /// <summary>
    /// Steps every arm <paramref name="steps"/> times with its own executor (<paramref name="executor"/> over its own copy
    /// of <paramref name="log"/>), asserting that all arms are StateEquals and hash-equal at the start and after every step;
    /// returns the FIRST arm's worlds (element j = state K + j) — any arm would do, they are the same worlds.
    /// <paramref name="before"/> is an optional per-step rig change, applied to a clone of each arm's world.
    /// </summary>
    private static WorldState[] Arms(
        (string Arm, WorldState World)[] arms, Func<OrderLog, TurnExecutor> executor, OrderLog log, int steps, Action<int, WorldState>? before = null)
    {
        var all = new WorldState[arms.Length][];
        for (int a = 0; a < arms.Length; a++)
        {
            TurnExecutor exec = executor(With(log));
            var worlds = new WorldState[steps + 1];
            worlds[0] = arms[a].World.Clone();
            for (int j = 1; j <= steps; j++)
            {
                WorldState prev = worlds[j - 1];
                if (before is not null)
                {
                    prev = prev.Clone();
                    before(j - 1, prev);
                }
                worlds[j] = exec.Step(prev);
            }
            all[a] = worlds;
        }
        for (int j = 0; j <= steps; j++)
            for (int a = 1; a < arms.Length; a++)
            {
                Assert.True(WorldStates.StateEquals(all[0][j], all[a][j]), $"the {arms[a].Arm} arm differs from the {arms[0].Arm} arm {j} step(s) after K");
                Assert.True(WorldHash.ComputeHex(all[0][j]) == WorldHash.ComputeHex(all[a][j]), $"the {arms[a].Arm} arm's hash differs {j} step(s) after K");
            }
        return all[0];
    }

    private static long Held(IReadOnlyWorldState w, int good)
    {
        long sum = 0;
        for (int i = 0; i < w.ConstructionHolds.Count; i++)
            if (w.ConstructionHolds[i].Purse == Reservations.PolityPurse(Player) && w.ConstructionHolds[i].Good == good) sum += w.ConstructionHolds[i].Units;
        return sum;
    }

    private static double ShareOf(IReadOnlyWorldState w, SettlementId s)
    {
        for (int i = 0; i < w.SectorAllocations.Count; i++) if (w.SectorAllocations[i].Settlement == s) return w.SectorAllocations[i].Construction;
        return double.NaN;
    }

    private static ConstructionQueueRow Slot(SettlementId s, int slot) => new(s, slot, Granary);

    private static bool Queued(IReadOnlyWorldState w, ConstructionQueueRow e)
    {
        for (int i = 0; i < w.ConstructionQueue.Count; i++) if (w.ConstructionQueue[i] == e) return true;
        return false;
    }

    private static void Eq<T>(T expected, T actual, string what) =>
        Assert.True(EqualityComparer<T>.Default.Equals(expected, actual), $"{what} — expected {expected}, got {actual}");

    /// <summary>A settlement's queue, head first, as (slot, EnqueuedTurn key, progress, has a progress row) — the key of a
    /// row-less (migrated v32) entry reads −1.</summary>
    private static (int Slot, long Key, long P, bool Row)[] Entries(IReadOnlyWorldState w, SettlementId s)
    {
        ConstructionQueueRow[] q = ConstructionQuery.Queue(w, s);
        var entries = new (int, long, long, bool)[q.Length];
        for (int i = 0; i < q.Length; i++)
            entries[i] = (q[i].Slot, ConstructionQuery.EnqueuedTurnOf(w.ConstructionProgress, q[i]), ConstructionQuery.ProgressOf(w, q[i]),
                ConstructionQuery.ProgressIndex(w.ConstructionProgress, q[i]) >= 0);
        return entries;
    }

    // ------------------------------------------------------------------------ a binding split on a migrated world (fix round)

    /// <summary>The timber at s0 before every step of the split rig (the whole supply: every other holder has none).</summary>
    private const long Supply = 100;

    private static readonly Sim.Core.Systems.Research.ResearchContent Research = TestConfigs.Research();

    private static TurnExecutor BuildAndRoads(OrderLog log) =>
        new(ResearchRigs.FlatEra(10.0), [SystemCatalog.Construction(Shipped), SystemCatalog.RoadDevelopment(Shipped)], log);

    /// <summary>ConstructionLifecyclePersistenceTests' road rig (P1 L7/L8), restated on a MIGRATED world: one 600-km dirt
    /// path from s0 to s2 (two holders of the player's purse; sim.json roads.maxRouteKm, so road works can always use all
    /// that building work leaves), the trackway known, every holder of the purse all builders, timber 0 at every holder
    /// and 1,000 stone at s0 (stone is never short, so a granary's draw that matters is its timber).</summary>
    private static void RoadRig(WorldState w)
    {
        w.TransportEdges.Clear();
        w.TransportEdges.Add(new TransportEdgeRow(1, S0, S2, EdgeTypes.DirtPath, TransportModes.Road, TransportEdgeStates.Complete,
            100_000, 600.0, 0, 0, 0, EdgeTypes.DirtPath, 0.0, 1.0));
        w.SettlementDistances.Clear();
        w.SettlementDistances.Add(new SettlementDistanceRow(S0, S2, 100.0));
        w.SettlementDistances.Add(new SettlementDistanceRow(S2, S0, 100.0));
        w.TradeFlows.Clear();
        var track = new ResearchCompletedRow(Player, Research.Nodes[Research.IndexOfId("track_road")].Key);
        bool known = false;
        for (int i = 0; i < w.ResearchCompleted.Count; i++) if (w.ResearchCompleted[i] == track) known = true;
        if (!known) w.ResearchCompleted.Add(track);
        foreach (SettlementId h in ConstructionQuery.Holders(w, Shipped, S0))
        {
            C.AllBuilders(w, h);
            C.Set(w, h, Timber, 0);
            C.Set(w, h, Stone, h == S0 ? 1000 : 0);
        }
    }

    private static long RoadUnits(IReadOnlyWorldState w, long turn)
    {
        long units = 0;
        for (int i = 0; i < w.RoadDevelopments.Count; i++) if (w.RoadDevelopments[i].Turn == turn) units += w.RoadDevelopments[i].MaterialUnits;
        return units;
    }

    /// <summary>
    /// The split rig on <paramref name="arms"/> at K = <paramref name="k"/>: rigged (RoadRig) before the first step, the timber
    /// at s0 refilled to <see cref="Supply"/> through the Ledger before each step, stepped twice by construction + road
    /// development with a road order stamped K and K + 1 and — when <paramref name="bZero"/> — a kind 12 to b = 0 stamped K.
    /// Returns the first arm's worlds (every arm is asserted equal at every step) and, per step, the timber road works and
    /// building work drew (both sink under ConstructionMaterials; the rig's own Set flows are InitialEndowment).
    /// </summary>
    private static (WorldState[] Worlds, (long Road, long Build)[] Steps) SplitRun((string, WorldState)[] arms, long k, bool bZero)
    {
        var log = new OrderLog();
        if (bZero) log.Append(new OrderRecord(k, Player.Value, OrderKind.SetResourceAllocation, Player.Value, 0));
        log.Append(OrderRecord.From(k, Player, OrderKind.DevelopRoads, 0, 100.0));
        log.Append(OrderRecord.From(k + 1, Player, OrderKind.DevelopRoads, 0, 100.0));
        WorldState[] c = Arms(arms, BuildAndRoads, log, 2, (j, x) =>
        {
            if (j == 0) RoadRig(x);
            C.Set(x, S0, Timber, Supply);
        });
        var steps = new (long Road, long Build)[2];
        for (int j = 1; j <= 2; j++)
        {
            long road = RoadUnits(c[j], k + j - 1);
            steps[j - 1] = (road, C.Sunk(c[j], Timber) - C.Sunk(c[j - 1], Timber) - road);
        }
        return (c, steps);
    }

    // ======================================================================== the v32 fixture (batch 2, flat, turn 40)

    private const long K32 = 40;
    private static WorldgenConfig Flat => TestConfigs.PreD054Worldgen();
    private static string V32Path() => Path.Combine(AppContext.BaseDirectory, "Fixtures", "v32-seed42-turn40.civsnap");
    private static WorldState LoadV32() => Snapshot.Load(new MemoryStream(File.ReadAllBytes(V32Path())), Sim.Core.Worldgen.Worldgen.Generate(Flat, 42));

    /// <summary>X1 (b) for v32: the batch-2 twin's world at K — the PreD054 pair run from the founded world (`--founded
    /// --seed 42 --ai-empires 1`, flat) with the fixture's three orders and the AI producer each turn, as `sim run` ran.</summary>
    private static readonly Lazy<WorldState> V32Twin = new(() =>
    {
        SimConfig off = TestConfigs.PreD054Sim(Shipped);
        WorldState w = WorldFounding.Found(Flat with { AiEmpires = 1 }, off, 42);
        OrderLog loaded;
        using (FileStream f = File.OpenRead(Path.Combine(AppContext.BaseDirectory, "Fixtures", "v32-fixture-orders.bin"))) loaded = OrderLog.Load(f);
        var run = new OrderLog();
        TurnExecutor exec = Production(off, Flat, run);
        for (long t = 0; t < K32; t++)
        {
            OrderBatch b = loaded.BatchFor(w.Clock.Turn);
            for (int i = 0; i < b.Count; i++) run.Append(b[i]);
            AiOrders.Append(run, w, off, Flat);
            w = exec.Step(w);
        }
        return w;
    }, LazyThreadSafetyMode.ExecutionAndPublication);

    /// <summary>The four v32 arms at K: the twin's world, the migrated load, the v34 re-save reloaded, and the re-save
    /// through the CLI's loader (no --size: the fixture is the default size).</summary>
    private static (string, WorldState)[] V32Arms()
    {
        WorldState loaded = LoadV32();
        byte[] v34 = Saved(loaded);
        return
        [
            ("batch-2 twin at K", V32Twin.Value),
            ("v32 load", loaded),
            ("v34 re-save", Snapshot.Load(new MemoryStream(v34), loaded.Terrain)),
            ("v34 re-save, SaveFiles.Load", ViaCli(v34, -1)),
        ];
    }

    private static TurnExecutor V32Shipped(OrderLog log) => Production(Shipped, Flat, log);

    /// <summary>X1 (a) and (b) for the v32 fixture: its v32 projection is the batch-2 build's hash, and the migrated world IS
    /// the batch-2 twin's world at K — every table and the v34 hash (the three tables v32 lacks are empty in both).</summary>
    [Fact]
    public void V32_TheMigratedWorld_IsTheBatch2TwinsWorldAtK_EveryTableAndTheHash()
    {
        WorldState loaded = LoadV32(), twin = V32Twin.Value;
        Assert.Equal(ConstructionSchemaV33Tests.V32FixtureHash, SchemaProjection.V32(loaded));
        Assert.Equal(ConstructionSchemaV33Tests.V32FixtureHash, SchemaProjection.V32(twin));
        Assert.True(WorldStates.StateEquals(twin, loaded), "the migrated v32 world differs from the batch-2 twin's world at K");
        Assert.Equal(WorldHash.ComputeHex(twin), WorldHash.ComputeHex(loaded));
        ConstructionQueueRow[] q = ConstructionQuery.Queue(loaded, S0);
        Assert.Equal([Slot(S0, 0), Slot(S0, 1)], q);
        Assert.Equal((-1L, -1L, 0, 0.0), (ConstructionQuery.EnqueuedTurnOf(loaded.ConstructionProgress, q[0]),
            ConstructionQuery.EnqueuedTurnOf(loaded.ConstructionProgress, q[1]), loaded.ConstructionProgress.Count, ShareOf(loaded, S0)));
    }

    /// <summary>
    /// P6 (a) — PART OF THE STONE: the migrated head's FIRST PROGRESS APPENDS ITS ROW KEEPING −1 (ConstructionSystem's
    /// migrated-entry path). Construction alone, stocks set exactly: builders at s0 and 7 stone in the player's whole purse.
    /// Step 40 → 41 takes the row-less slot 0 to floor(7 × N / 20) = 350,000 and appends (s0, slot 0, granary, −1, 350,000).
    /// Saved there (v34) and continued on three arms (never saved, Snapshot.Load, SaveFiles.Load): with 13 more stone it
    /// completes in step 41 → 42 drawing exactly Remaining(20, 350,000) = 13 stone and Remaining(40, 350,000) = 26 timber,
    /// keeping −1 until its row is removed; slot 1 (row-less, −1) is then the head, and sets aside its timber from the step
    /// after (the promoted slot of a completion holds from the NEXT step). D-058 (2026-10-19): with the completion's leftover
    /// capacity carried to slot 1 in the same step, it sets aside its timber already in the completing step.
    /// </summary>
    [Fact]
    public void V32_A_PartOfTheStone_TheMigratedHeadsFirstProgressAppendsItsRowKeepingMinusOne_AndTheKeySurvivesTheSaveToCompletion()
    {
        WorldState w = LoadV32();
        C.AllBuilders(w, S0);
        foreach (SettlementId h in ConstructionQuery.Holders(w, Shipped, S0)) C.Set(w, h, Stone, h == S0 ? 7 : 0);
        TurnExecutor exec = C.ConstructionOnly(Shipped, null);
        WorldState t41 = exec.Step(w);
        Assert.Equal(new ConstructionProgressRow(S0, 0, Granary, -1, 350_000), Assert.Single(TableRows.Rows(t41.ConstructionProgress)));
        Assert.Equal((7L, 14L), (C.Sunk(t41, Stone) - C.Sunk(w, Stone), C.Sunk(t41, Timber) - C.Sunk(w, Timber)));

        byte[] bytes = Saved(t41);
        (string, WorldState)[] arms = [("never saved", t41), ("Snapshot.Load", Snapshot.Load(new MemoryStream(bytes), t41.Terrain)), ("SaveFiles.Load", ViaCli(bytes, -1))];
        WorldState[] c = Arms(arms, l => C.ConstructionOnly(Shipped, l), new OrderLog(), 2, (j, x) => { if (j == 0) C.Set(x, S0, Stone, 13); });
        Eq(new ConstructionProgressRow(S0, 0, Granary, -1, 350_000), c[0].ConstructionProgress[0], "the migrated row after the load");
        Eq((1L, 13L, 26L), (ConstructionQuery.Built(c[1], S0, Granary) - ConstructionQuery.Built(t41, S0, Granary),
            C.Sunk(c[1], Stone) - C.Sunk(t41, Stone), C.Sunk(c[1], Timber) - C.Sunk(t41, Timber)), "completed with exactly the remainder");
        Eq((0, true), (c[1].ConstructionProgress.Count, Queued(c[1], Slot(S0, 1))), "slot 1 heads, row-less");
        Eq(-1L, ConstructionQuery.EnqueuedTurnOf(c[1].ConstructionProgress, Slot(S0, 1)), "slot 1's key");
        // D-058 (2026-10-19) CAUSE BLOCK: was (0, 0), "the promoted slot holds nothing in the completing step". Under the
        // Director's "build one at a time" ruling slot 0's completion passes its leftover capacity to slot 1 IN THE SAME
        // STEP: slot 1 is walked as a head there, finds no stone (it draws nothing and stays row-less) and sets aside its 40
        // timber already in the completing step — MEASURED on this tree: (40, 0). (0, 0) stands under PreContinueWithinTurn.
        Eq((40L, 0L), (Held(c[1], Timber), Held(c[1], Stone)), "the promoted slot holds its timber in the completing step (D-058)");
        Eq((40L, 0L), (Held(c[2], Timber), Held(c[2], Stone)), "from the next step it sets aside its timber (the purse has no stone)");
        Eq((1L, 0), (ConstructionQuery.Built(c[2], S0, Granary) - ConstructionQuery.Built(t41, S0, Granary), c[2].ConstructionProgress.Count), "slot 1 waits for stone, row-less");
    }

    /// <summary>
    /// P6 (b) — A CANCEL OF THE ROW-LESS MIGRATED HEAD, stamped K = 40, on the full pipeline and the four arms. State 40 holds
    /// slots 0 and 1; state 41 holds slot 1 alone — still row-less (no builders: it cannot progress, so no row is created)
    /// with its key −1 — and nothing was refunded (the sinks of timber and stone are unchanged by s0, which drew nothing).
    /// The purse publishes a set-aside of 40 timber and 20 stone at 41. That is PUBLICATION ONLY: the two migrated granaries'
    /// needs are equal, so this case cannot tell the promoted slot's set-aside from the cancelled head's, and it does not
    /// show that the promoted slot sets aside in the SAME step (a cancel applied after the walk passes it). V32_G does
    /// (D-055 fix round, T4-L1-T4V1-F7).
    /// </summary>
    [Fact]
    public void V32_B_ACancelOfTheRowlessMigratedHead_StampedAtK_PromotesSlotOneRowless_NoRefund_AndItSetsAsideInThatStep()
    {
        OrderLog log = With(null, C.Cancel(K32, Player, S0, 0));
        WorldState[] c = Arms(V32Arms(), V32Shipped, log, 2);
        Eq((true, true), (Queued(c[0], Slot(S0, 0)), Queued(c[0], Slot(S0, 1))), "state 40");
        Eq((false, true), (Queued(c[1], Slot(S0, 0)), Queued(c[1], Slot(S0, 1))), "state 41: slot 0 cancelled");
        Eq((0, -1L, 0L), (c[1].ConstructionProgress.Count, ConstructionQuery.EnqueuedTurnOf(c[1].ConstructionProgress, Slot(S0, 1)), ConstructionQuery.ProgressOf(c[1], Slot(S0, 1))), "slot 1 heads, row-less");
        Eq((40L, 20L), (Held(c[1], Timber), Held(c[1], Stone)), "slot 1 sets aside its need at 41");
        Eq((ConstructionQuery.Built(c[0], S0, Granary), C.Sunk(c[0], Stone), C.Sunk(c[0], Timber)),
            (ConstructionQuery.Built(c[2], S0, Granary), C.Sunk(c[2], Stone), C.Sunk(c[2], Timber)), "nothing built, drawn or refunded");
        // Control: without the cancel both slots stay queued.
        WorldState[] k = Arms([("v32 load", LoadV32())], V32Shipped, new OrderLog(), 1);
        Eq((true, true), (Queued(k[1], Slot(S0, 0)), Queued(k[1], Slot(S0, 1))), "control: no cancel");
    }

    /// <summary>P6 (c) — A KIND 12 ON THE MIGRATED WORLD, stamped K: no split row in state 40, (1, 50) from state 41 — the
    /// migrated world's first ResourceAllocations row, on all four arms. ROW AND TIMING ONLY: no road works are ordered, so
    /// the split binds nothing here; V32_S pins a BINDING split on the migrated world.</summary>
    [Fact]
    public void V32_C_AKind12StampedAtK_IsTheMigratedWorldsFirstSplitRow_FromKPlusOne()
    {
        OrderLog log = With(null, OrderRecord.From(K32, Player, OrderKind.SetResourceAllocation, Player.Value, 50.0));
        WorldState[] c = Arms(V32Arms(), V32Shipped, log, 2);
        Eq(0, c[0].ResourceAllocations.Count, "no row at 40");
        Eq(new ResourceAllocationRow(Player, 50), Assert.Single(TableRows.Rows(c[1].ResourceAllocations)), "the row at 41");
        Eq(new ResourceAllocationRow(Player, 50), Assert.Single(TableRows.Rows(c[2].ResourceAllocations)), "the row at 42");
    }

    /// <summary>
    /// P6 (e) — NO BUILDERS: THE MIGRATED HEADS KEEP WAITING AND THE HEAD SETS ASIDE ITS NEED. On the full pipeline and the four
    /// arms, three steps with no order: both slots stay queued at p = 0 with no row, and the head (slot 0, key −1) sets aside
    /// 40 timber and 20 stone every step (a valid wait: labour adults present, construction share 0). BINDING (construction
    /// alone, stocks set exactly — the fixture's purse holds 348 stone, so a younger head is never short on it): with 30 stone
    /// in the purse, a granary ordered at s2 after the load (EnqueuedTurn 40) is served BEHIND the migrated head (−1) and
    /// gets the 10 stone it does not set aside — p = 500,000 — on the three arms of the load, where the PreReservations step
    /// builds it.
    /// </summary>
    [Fact]
    public void V32_E_WithNoBuilders_TheMigratedHeadsKeepWaiting_TheHeadSetsAsideItsNeed_AndBindsAYoungerHeadOrderedAfterTheLoad()
    {
        WorldState[] c = Arms(V32Arms(), V32Shipped, new OrderLog(), 3);
        for (int j = 1; j <= 3; j++)
        {
            Eq((true, true, 0), (Queued(c[j], Slot(S0, 0)), Queued(c[j], Slot(S0, 1)), c[j].ConstructionProgress.Count), $"state {40 + j}: both waiting, row-less");
            Eq((40L, 20L), (Held(c[j], Timber), Held(c[j], Stone)), $"state {40 + j}: the head's set-aside");
            Eq(0.0, ShareOf(c[j], S0), $"state {40 + j}: no builders");
        }
        Eq(HoldRelease.None, ConstructionQuery.ReleaseOf(c[0], Shipped, Slot(S0, 0)), "a no-builders wait is not released");

        WorldState w = LoadV32();
        foreach (SettlementId h in ConstructionQuery.Holders(w, Shipped, S0)) C.Set(w, h, Stone, h == S0 ? 30 : 0);
        byte[] bytes = Saved(w);
        OrderLog younger = With(null, C.Enqueue(K32, Player, S2));
        WorldState[] b = Arms([("never saved", w), ("Snapshot.Load", Snapshot.Load(new MemoryStream(bytes), w.Terrain)), ("SaveFiles.Load", ViaCli(bytes, -1))],
            l => C.ConstructionOnly(Shipped, l), younger, 1);
        ConstructionQueueRow s2 = new(S2, 0, Granary);
        Eq((40L, 500_000L), (ConstructionQuery.EnqueuedTurnOf(b[1].ConstructionProgress, s2), ConstructionQuery.ProgressOf(b[1], s2)), "the younger head gets what is not set aside");
        Eq((20L, 0L), (Held(b[1], Stone), ConstructionQuery.ProgressOf(b[1], Slot(S0, 0))), "the migrated head keeps waiting and sets aside 20 stone");
        WorldState batch3 = C.ConstructionOnly(TestConfigs.PreReservations(Shipped), With(younger)).Step(w.Clone());
        Eq(1L, ConstructionQuery.Built(batch3, S2, Granary) - ConstructionQuery.Built(w, S2, Granary), "PreReservations builds the younger head");
    }

    /// <summary>P6 (a), on the full pipeline — BUILDERS ORDERED AT K (kind 3, s0's construction share to 60): the row is 60 in
    /// state 41 (delivered by step 40 → 41), slot 0 completes in step 41 → 42 and slot 1 in 42 → 43, each exactly once (the
    /// purse is rich: no partial state), the second one holding nothing in the step it was promoted.</summary>
    [Fact]
    public void V32_A_BuildersOrderedAtK_TheMigratedHeadsCompleteInOrder_EachExactlyOnce()
    {
        OrderLog log = With(null, OrderRecord.From(K32, Player, OrderKind.SectorAllocation, (S0.Value << 3) | Sectors.Construction, 60.0));
        WorldState[] c = Arms(V32Arms(), V32Shipped, log, 4);
        long built0 = ConstructionQuery.Built(c[0], S0, Granary);
        Eq((0.0, 0.6), (ShareOf(c[0], S0), ShareOf(c[1], S0)), "the share row, turn-exact");
        // D-058 (2026-10-19) CAUSE BLOCK — was (0, 1, 2, 2): slot 0 in 41 → 42, slot 1 in 42 → 43. Under the Director's
        // "build one at a time … 7 built, 1 under construction, 1 not started" ruling slot 0's leftover capacity carries slot
        // 1 forward in the same step and the rich purse completes it: MEASURED on this tree (0, 2, 2, 2) — both in 41 → 42,
        // still one after the other, each exactly once. (0, 1, 2, 2) stands under TestConfigs.PreContinueWithinTurn.
        Eq((0L, 2L, 2L, 2L), (ConstructionQuery.Built(c[1], S0, Granary) - built0, ConstructionQuery.Built(c[2], S0, Granary) - built0,
            ConstructionQuery.Built(c[3], S0, Granary) - built0, ConstructionQuery.Built(c[4], S0, Granary) - built0), "built 41..44");
        Eq((40L, 0L, 0L), (Held(c[1], Timber), Held(c[2], Timber), Held(c[3], Timber)), "set aside at 41, then none");
        Eq((false, false), (Queued(c[3], Slot(S0, 0)), Queued(c[3], Slot(S0, 1))), "the queue is empty at 43");
    }

    /// <summary>P6 (d) — `sim hash` (a real process) of the v32 fixture's v34 re-save prints the in-process hash of the
    /// migrated world on stdout and NOTHING on stderr: the re-save is a current-schema file of the flat world (trailer bit 0
    /// = 0), so the CLI regenerates the flat terrain and no migration note is due.</summary>
    [Fact]
    public void V32_D_SimHash_OfTheV34ReSave_IsTheInProcessHashOfTheMigratedWorld_FlatAndWithoutANote()
    {
        WorldState loaded = LoadV32();
        byte[] v34 = Saved(loaded);
        Assert.Equal((CanonicalSchema.Version, 0), (BitConverter.ToInt32(v34, 8), BitConverter.ToInt32(v34, v34.Length - 4)));
        string path = Path.Combine(Path.GetTempPath(), "civsim-v32resave-" + Guid.NewGuid().ToString("N") + ".civsnap");
        try
        {
            File.WriteAllBytes(path, v34);
            (int exit, string stdout, string stderr) = CliProcess.Run(Limit, "hash", path);
            Assert.Equal((0, ""), (exit, stderr));
            Assert.Equal(new[] { WorldHash.ComputeHex(loaded) }, CliProcess.Lines(stdout));
            Assert.False(SaveFiles.Load(path, -1).Terrain!.WrapX);
        }
        finally
        {
            File.Delete(path);
        }
    }

    /// <summary>
    /// D-055 fix round (2026-10-17, T4-L1-T4V1-F1) — A RE-ENQUEUE ON THE MIGRATED v32 WORLD. One batch stamped K = 40 cancels
    /// s0's row-less migrated head (slot 0, key −1), orders a new granary at s0 and gives s0 builders (kind 3, 60 — a share
    /// governs the NEXT step's capacity). On the four arms, state 41 holds s0's queue EXACTLY as [(slot 1, key −1, p 0,
    /// row-less), (slot 2, key 40, p 0, WITH its progress row)]: the re-enqueue is a NEW entry at the next free slot, keyed by
    /// its stamp turn (D-055 §1.2) and given its own row although its settlement still holds a row-less migrated entry, and
    /// the purse sets aside slot 1's need alone (40 timber, 20 stone). With builders slot 1 (the head) is built in 41 → 42,
    /// the new entry still keyed 40 at 42, and the new entry is built in 42 → 43. Control (the same kind 3 without the batch):
    /// state 41 keeps the two migrated slots. Kills M-RL (an enqueue at a settlement holding a row-less entry given no row —
    /// its key would read −1, ordering it ahead of every head ordered after the migration).
    /// </summary>
    [Fact]
    public void V32_F_ACancelAndAReEnqueueInOneBatchAtK_TheNewEntryIsKeyedByItsStampTurnWithItsOwnRow_BehindTheMigratedSlot()
    {
        OrderRecord builders = OrderRecord.From(K32, Player, OrderKind.SectorAllocation, (S0.Value << 3) | Sectors.Construction, 60.0);
        WorldState[] c = Arms(V32Arms(), V32Shipped, With(null, C.Cancel(K32, Player, S0, 0), C.Enqueue(K32, Player, S0), builders), 3);
        long built0 = ConstructionQuery.Built(c[0], S0, Granary);
        Assert.Equal(new[] { (0, -1L, 0L, false), (1, -1L, 0L, false) }, Entries(c[0], S0));
        Assert.Equal(new[] { (1, -1L, 0L, false), (2, 40L, 0L, true) }, Entries(c[1], S0));
        Eq((40L, 20L, 0L), (Held(c[1], Timber), Held(c[1], Stone), ConstructionQuery.Built(c[1], S0, Granary) - built0), "state 41: slot 1's need set aside, nothing built");
        // D-058 (2026-10-19) CAUSE BLOCK — was: state 42 holds the new entry (key 40) with slot 1 built first, and the new
        // entry is built in 42 → 43. Under the Director's "build one at a time" ruling slot 1's leftover capacity carries the
        // new entry into the same step 41 → 42 — still after slot 1, and still keyed 40 (its own row) — and it completes
        // there: MEASURED on this tree, state 42 holds no entry and two granaries. The old sequence stands under
        // TestConfigs.PreContinueWithinTurn.
        Assert.Empty(Entries(c[2], S0));
        Eq(2L, ConstructionQuery.Built(c[2], S0, Granary) - built0, "state 42: slot 1 built first, then the re-enqueued granary in the same step");
        Assert.Empty(Entries(c[3], S0));
        Eq(2L, ConstructionQuery.Built(c[3], S0, Granary) - built0, "state 43: the re-enqueued granary built");
        // Control: the kind 3 alone keeps both migrated slots at 41.
        WorldState[] k = Arms([("v32 load", LoadV32())], V32Shipped, With(null, builders), 1);
        Assert.Equal(new[] { (0, -1L, 0L, false), (1, -1L, 0L, false) }, Entries(k[1], S0));
    }

    /// <summary>
    /// D-055 fix round (2026-10-17, T4-L1-T4V1-F7) — A CANCEL OF THE MIGRATED HEAD AFTER ITS FIRST PROGRESS: THE PROMOTED SLOT'S
    /// SET-ASIDE BINDS IN THE DELIVERY STEP. Construction alone, stocks set exactly. Step 40 → 41 (builders, 7 stone in the
    /// purse — V32_A's first step) takes slot 0 to 350,000 with its row keyed −1; saved there, three arms (never saved,
    /// Snapshot.Load, SaveFiles.Load). One batch stamped 41 cancels slot 0 and orders a granary at s2 (key 41, the younger
    /// head); s0 has no builders from 41 (a valid wait) and the purse holds exactly 30 stone. The cancel is applied BEFORE
    /// the walk, so in step 41 → 42 the promoted row-less slot 1 (key −1, p 0) is s0's head and sets aside its WHOLE need,
    /// 20 stone, and s2 draws the 10 left: 500,000. The step's sinks are s2's 20 timber and 10 stone only (slot 0 drew and
    /// is refunded nothing); the purse publishes slot 1's 40 timber and 20 stone plus s2's remaining 20 timber. Were the
    /// cancelled head still walked it would set aside only Remaining(20, 350,000) = 13 stone and s2 would reach 850,000 —
    /// exactly the no-cancel control; the PreReservations step builds s2. Kills M-B (the cancel moved after building work),
    /// which V32_B cannot: there the two migrated granaries' needs are equal.
    /// </summary>
    [Fact]
    public void V32_G_ACancelOfTheMigratedHeadAfterItsFirstProgress_ThePromotedRowlessSlotSetsAsideItsWholeNeedInTheDeliveryStep()
    {
        WorldState w = LoadV32();
        C.AllBuilders(w, S0);
        foreach (SettlementId h in ConstructionQuery.Holders(w, Shipped, S0)) C.Set(w, h, Stone, h == S0 ? 7 : 0);
        WorldState t41 = C.ConstructionOnly(Shipped, null).Step(w);
        Assert.Equal(new[] { (0, -1L, 350_000L, true), (1, -1L, 0L, false) }, Entries(t41, S0));
        Action<int, WorldState> rig = (j, x) =>
        {
            if (j != 0) return;
            C.NoBuilders(x, S0);
            C.Set(x, S0, Stone, 30);
        };
        OrderLog batch = With(null, C.Cancel(41, Player, S0, 0), C.Enqueue(41, Player, S2));
        byte[] bytes = Saved(t41);
        WorldState[] c = Arms([("never saved", t41), ("Snapshot.Load", Snapshot.Load(new MemoryStream(bytes), t41.Terrain)), ("SaveFiles.Load", ViaCli(bytes, -1))],
            l => C.ConstructionOnly(Shipped, l), batch, 1, rig);
        Assert.Equal(new[] { (1, -1L, 0L, false) }, Entries(c[1], S0));
        Assert.Equal(new[] { (0, 41L, 500_000L, true) }, Entries(c[1], S2));
        Eq((20L, 10L), (C.Sunk(c[1], Timber) - C.Sunk(c[0], Timber), C.Sunk(c[1], Stone) - C.Sunk(c[0], Stone)), "step 41 -> 42 sinks: s2's draw only");
        Eq((60L, 20L), (Held(c[1], Timber), Held(c[1], Stone)), "state 42: slot 1's whole need (40, 20) and s2's remaining 20 timber");
        Eq(ConstructionQuery.Built(t41, S0, Granary), ConstructionQuery.Built(c[1], S0, Granary), "nothing built at s0");
        // Controls: no cancel — the walked head is slot 0, setting aside 13 stone; and the PreReservations step.
        WorldState[] kept = Arms([("never saved", t41)], l => C.ConstructionOnly(Shipped, l), With(null, C.Enqueue(41, Player, S2)), 1, rig);
        Eq((850_000L, 13L), (ConstructionQuery.ProgressOf(kept[1], new ConstructionQueueRow(S2, 0, Granary)), Held(kept[1], Stone)), "control: no cancel");
        WorldState[] pre = Arms([("never saved", t41)], l => C.ConstructionOnly(TestConfigs.PreReservations(Shipped), l), batch, 1, rig);
        Eq(1L, ConstructionQuery.Built(pre[1], S2, Granary) - ConstructionQuery.Built(t41, S2, Granary), "PreReservations builds s2");
    }

    /// <summary>
    /// D-055 fix round (2026-10-17, T4-L1-T4V1-F2) — A BINDING SPLIT ON THE MIGRATED v32 WORLD (ADR-039 §3 after the migration).
    /// The four arms rigged at K by RoadRig (a 600-km trackway s0–s2, all builders, stone plenty, the timber at s0 refilled to
    /// 100 before each step) and stepped by construction + road development with a road order each turn. A kind 12 to b = 0
    /// stamped K governs the step that delivers it: in step 40 → 41 road works take all 100 timber and building work none
    /// (the migrated heads wait), and the migrated world's first split row (1, 0) holds at 41 and governs 41 → 42 alike.
    /// Control (no kind 12, no row: building first): the migrated head draws its 40 timber and the road gets 60, in each step
    /// (slot 0 is built at 41, slot 1 at 42). Kills M-A (the kind-12 block moved after building work) on the migration path.
    /// </summary>
    [Fact]
    public void V32_S_ABindingSplitStampedAtK_GovernsTheDeliveringStepOnTheMigratedWorld()
    {
        (WorldState[] c, (long Road, long Build)[] split) = SplitRun(V32Arms(), K32, bZero: true);
        Eq((0, new ResourceAllocationRow(Player, 0), new ResourceAllocationRow(Player, 0)),
            (c[0].ResourceAllocations.Count, Assert.Single(TableRows.Rows(c[1].ResourceAllocations)), Assert.Single(TableRows.Rows(c[2].ResourceAllocations))), "rows at 40, 41, 42");
        Eq(((100L, 0L), (100L, 0L)), (split[0], split[1]), "b = 0: (road, building) timber in 40 -> 41 and 41 -> 42");
        Eq((0L, 0L), (ConstructionQuery.ProgressOf(c[2], Slot(S0, 0)), ConstructionQuery.Built(c[2], S0, Granary) - ConstructionQuery.Built(c[0], S0, Granary)), "the migrated heads wait");
        (WorldState[] k, (long Road, long Build)[] control) = SplitRun(V32Arms(), K32, bZero: false);
        // D-058 (2026-10-19) CAUSE BLOCK — was ((60, 40), (60, 40)): slot 0 built in 40 → 41 and slot 1 in 41 → 42, 40 timber
        // each. Under the Director's "build one at a time" ruling slot 0's leftover capacity carries slot 1 into the same
        // step, so building first takes 80 of the 100 timber in 40 → 41 (both built) and the road the other 20, and in
        // 41 → 42 nothing is queued and the road takes all 100 — MEASURED on this tree. The b = 0 arm above is unchanged.
        Eq(((20L, 80L), (100L, 0L)), (control[0], control[1]), "control (no row): building first");
        Eq((0, 2L), (k[2].ResourceAllocations.Count, ConstructionQuery.Built(k[2], S0, Granary) - ConstructionQuery.Built(k[0], S0, Granary)), "control: both migrated granaries built");
    }

    // ======================================================================== the v33 fixture (batch 3, cylinder, turn 2)

    private const long K33 = 2;
    private static string V33Path() => ConstructionHoldsV33FixtureTests.FixturePath();
    private static TerrainSet V33Terrain() => Sim.Core.Worldgen.Worldgen.Generate(TestConfigs.Worldgen() with { SizePx = 256 }, 42);
    private static WorldState LoadV33() => Snapshot.Load(new MemoryStream(File.ReadAllBytes(V33Path())), V33Terrain());

    private static OrderLog SessionOrders()
    {
        using FileStream f = File.OpenRead(Path.Combine(ConstructionHoldsV33FixtureTests.SessionDir(), "orders-" + ConstructionHoldsV33FixtureTests.Stamp + ".bin"));
        return OrderLog.Load(f);
    }

    private static TurnExecutor V33Exec(SimConfig cfg, OrderLog log) => Production(cfg, TestConfigs.Worldgen(), log);

    /// <summary>X1 (b) for v33: the PreReservations run of the session's orders at K (it publishes no hold row).</summary>
    private static WorldState V33TwinAtK()
    {
        SimConfig off = TestConfigs.PreReservations(Shipped);
        return V33Exec(off, SessionOrders()).Run(WorldFounding.Found(TestConfigs.Worldgen() with { SizePx = 256, AiEmpires = 1 }, off, 42, 4), (int)K33);
    }

    /// <summary>The four v33 arms at K: the PreReservations twin's world, the migrated load, the v34 re-save reloaded, and
    /// the re-save through the CLI's loader at 256 px.</summary>
    private static (string, WorldState)[] V33Arms()
    {
        WorldState loaded = LoadV33();
        byte[] v34 = Saved(loaded);
        return
        [
            ("PreReservations twin at K", V33TwinAtK()),
            ("v33 load", loaded),
            ("v34 re-save", Snapshot.Load(new MemoryStream(v34), loaded.Terrain)),
            ("v34 re-save, SaveFiles.Load", ViaCli(v34, 256)),
        ];
    }

    private static readonly ConstructionQueueRow Q0 = new(S0, 0, Granary), Q2 = new(S2, 0, Granary);

    /// <summary>The loaded v33 world stepped <paramref name="steps"/> times by the ALL-PreReservations executor (the batch-3
    /// rule) with the same orders: X1 (e)'s anti-vacuity arm.</summary>
    private static WorldState[] AllBatch3(OrderLog log, int steps)
    {
        TurnExecutor exec = V33Exec(TestConfigs.PreReservations(Shipped), With(log));
        var worlds = new WorldState[steps + 1];
        worlds[0] = LoadV33();
        for (int j = 1; j <= steps; j++) worlds[j] = exec.Step(worlds[j - 1]);
        return worlds;
    }

    /// <summary>
    /// P6 (e) on the v33 fixture — NO BUILDERS AT s0 (the fixture's own state), continued four steps on the four arms: s0's
    /// part-built head keeps p = 700,000, its row and its key 0, and sets aside its remaining Remaining(20, 700,000) = 6 stone
    /// in EVERY state 3 to 6 (and its 12 timber; the purse's published timber is s0's 12 plus s2's own remaining need while
    /// s2 is the other head: 46, 30, then 12). s2, younger in the building order, is held to 150,000 at 3, 550,000 at 4 and
    /// built at 5. X1 (e): the all-PreReservations continuation of the same loaded world gives s2 450,000 at 3 and 850,000 at 4.
    /// </summary>
    [Fact]
    public void V33_E_NoBuildersAtS0_ThePartBuiltHeadKeepsWaiting_AndSetsAsideItsStoneEveryStep_HoldingS2Back()
    {
        OrderLog log = SessionOrders();
        WorldState[] c = Arms(V33Arms(), l => V33Exec(Shipped, l), log, 4);
        for (int j = 1; j <= 4; j++)
        {
            Eq((700_000L, 0L), (ConstructionQuery.ProgressOf(c[j], Q0), ConstructionQuery.EnqueuedTurnOf(c[j].ConstructionProgress, Q0)), $"state {2 + j}: s0 waits");
            Eq(ConstructionQuery.Remaining(20, 700_000), Held(c[j], Stone), $"state {2 + j}: s0's stone set aside");
        }
        Eq((46L, 30L, 12L, 12L), (Held(c[1], Timber), Held(c[2], Timber), Held(c[3], Timber), Held(c[4], Timber)), "timber set aside 3..6");
        Eq((150_000L, 550_000L), (ConstructionQuery.ProgressOf(c[1], Q2), ConstructionQuery.ProgressOf(c[2], Q2)), "s2 at 3 and 4");
        Eq(1L, ConstructionQuery.Built(c[3], S2, Granary), "s2 built at 5");
        WorldState[] pre = AllBatch3(log, 2);
        Eq((450_000L, 850_000L), (ConstructionQuery.ProgressOf(pre[1], Q2), ConstructionQuery.ProgressOf(pre[2], Q2)), "the batch-3 rule from the same loaded world");
    }

    /// <summary>
    /// P6 (a) on the v33 fixture — BUILDERS BACK AT s0, ordered at K (kind 3, 60): the share row is 60 in state 3 (delivered
    /// by step 2 → 3); in that step s0 still has no builders (the share governs the next step's capacity), keeps 700,000 and
    /// sets aside its 6 stone, so s2 reaches only 150,000; in step 3 → 4 s0 completes drawing exactly Remaining(40, 700,000)
    /// = 12 timber and Remaining(20, 700,000) = 6 stone, and s2 reaches 450,000 (the construction sinks grow by 24 timber and
    /// 12 stone: s0's 12 and 6, s2's 12 and 6). X1 (e): the all-PreReservations continuation gives s2 450,000 already at 3.
    /// </summary>
    [Fact]
    public void V33_A_BuildersBackAtK_TheHeadKeepsHoldingUntilItsBuildersArrive_ThenCompletesWithExactlyItsRemainder()
    {
        OrderLog log = With(SessionOrders(), OrderRecord.From(K33, Player, OrderKind.SectorAllocation, (S0.Value << 3) | Sectors.Construction, 60.0));
        WorldState[] c = Arms(V33Arms(), l => V33Exec(Shipped, l), log, 4);
        Eq((0.0, 0.6), (ShareOf(c[0], S0), ShareOf(c[1], S0)), "the share row, turn-exact");
        Eq((700_000L, 6L, 150_000L), (ConstructionQuery.ProgressOf(c[1], Q0), Held(c[1], Stone), ConstructionQuery.ProgressOf(c[1], Q2)), "state 3");
        Eq((1L, false, 450_000L), (ConstructionQuery.Built(c[2], S0, Granary), Queued(c[2], Q0), ConstructionQuery.ProgressOf(c[2], Q2)), "state 4");
        Eq((24L, 12L), (C.Sunk(c[2], Timber) - C.Sunk(c[1], Timber), C.Sunk(c[2], Stone) - C.Sunk(c[1], Stone)), "step 3 -> 4's draws");
        Eq(1L, ConstructionQuery.Built(c[4], S2, Granary), "s2 built at 6");
        Eq(1L, ConstructionQuery.Built(c[4], S0, Granary), "s0 built once");
        WorldState[] pre = AllBatch3(log, 1);
        Eq(450_000L, ConstructionQuery.ProgressOf(pre[1], Q2), "the batch-3 rule at 3");
    }

    /// <summary>
    /// P6 (b) on the v33 fixture — A CANCEL OF THE PART-BUILT HEAD, stamped K (kind 11, s0 slot 0 at p = 700,000): in step
    /// 2 → 3 the cancel is applied before the walk, so s0's entry and row are gone in state 3, s0 draws and refunds nothing,
    /// and s2 draws the 6 stone s0 had set aside — 450,000 at 3: the construction sinks grow by exactly s2's
    /// Delivered(40, 450,000) = 18 timber and Delivered(20, 450,000) = 9 stone. Control (the same arms without the cancel):
    /// s2 150,000. X1 (e) inverted: once the only binding hold is released, this build's continuation equals the
    /// all-PreReservations one in every table but the hold table (their v33 projections, holds stripped, are equal at 3..6).
    /// </summary>
    [Fact]
    public void V33_B_ACancelOfThePartBuiltHeadStampedAtK_ReleasesItsSetAsideInTheDeliveryStep_NoRefund()
    {
        OrderLog log = With(SessionOrders(), C.Cancel(K33, Player, S0, 0));
        WorldState[] c = Arms(V33Arms(), l => V33Exec(Shipped, l), log, 4);
        Eq((true, 700_000L), (Queued(c[0], Q0), ConstructionQuery.ProgressOf(c[0], Q0)), "state 2");
        Eq((false, -1, 0L), (Queued(c[1], Q0), ConstructionQuery.ProgressIndex(c[1].ConstructionProgress, Q0), ConstructionQuery.Built(c[1], S0, Granary)), "state 3: s0 cancelled");
        Eq(450_000L, ConstructionQuery.ProgressOf(c[1], Q2), "s2 draws the released stone");
        Eq((18L, 9L), (C.Sunk(c[1], Timber) - C.Sunk(c[0], Timber), C.Sunk(c[1], Stone) - C.Sunk(c[0], Stone)), "step 2 -> 3 draws only s2's units");
        Eq((ConstructionQuery.Remaining(40, 450_000), 0L), (Held(c[1], Timber), Held(c[1], Stone)), "state 3: only s2's own remaining timber is set aside");
        WorldState[] control = Arms([("v33 load", LoadV33())], l => V33Exec(Shipped, l), SessionOrders(), 1);
        Eq(150_000L, ConstructionQuery.ProgressOf(control[1], Q2), "control: no cancel");
        WorldState[] pre = AllBatch3(log, 4);
        for (int j = 1; j <= 4; j++)
            Assert.True(SchemaProjection.HashAtSchemaV33(c[j], out _) == SchemaProjection.HashAtSchemaV33(pre[j], out _), $"state {2 + j}: after the release the hold rule binds nothing");
    }

    /// <summary>P6 (c) on the v33 fixture — A SPLIT CHANGE STAMPED K (30 → 50): the row is (1, 30) in state 2 and (1, 50)
    /// from state 3, on the four arms; the player orders no road works, so the split binds nothing and s2 is still held to
    /// 150,000 by s0's set-aside — X1 (e): the all-PreReservations continuation gives 450,000. ROW AND TIMING ONLY for the
    /// split; V33_S pins a BINDING split on the migrated world.</summary>
    [Fact]
    public void V33_C_ASplitChangeStampedAtK_IsTheRowFromKPlusOne_AndTheHoldStillBinds()
    {
        OrderLog log = With(SessionOrders(), OrderRecord.From(K33, Player, OrderKind.SetResourceAllocation, Player.Value, 50.0));
        WorldState[] c = Arms(V33Arms(), l => V33Exec(Shipped, l), log, 2);
        Eq(new ResourceAllocationRow(Player, 30), Assert.Single(TableRows.Rows(c[0].ResourceAllocations)), "the row at 2");
        Eq(new ResourceAllocationRow(Player, 50), Assert.Single(TableRows.Rows(c[1].ResourceAllocations)), "the row at 3");
        Eq((150_000L, 6L), (ConstructionQuery.ProgressOf(c[1], Q2), Held(c[1], Stone)), "state 3");
        Eq(450_000L, ConstructionQuery.ProgressOf(AllBatch3(log, 1)[1], Q2), "the batch-3 rule at 3");
    }

    /// <summary>P6 (d) on the v33 fixture — `sim hash --size 256` (a real process) of its v34 re-save prints the in-process
    /// hash of the migrated world and nothing on stderr (a current-schema file: no migration note), and that hash is not the
    /// v33 hash the batch-3 build printed (the layout moved), whose projection it still carries.</summary>
    [Fact]
    public void V33_D_SimHash_OfTheV34ReSave_IsTheInProcessHashOfTheMigratedWorld_WithoutANote()
    {
        WorldState loaded = LoadV33();
        byte[] v34 = Saved(loaded);
        string path = Path.Combine(Path.GetTempPath(), "civsim-v33resave-" + Guid.NewGuid().ToString("N") + ".civsnap");
        try
        {
            File.WriteAllBytes(path, v34);
            (int exit, string stdout, string stderr) = CliProcess.Run(Limit, "hash", path, "--size", "256");
            Assert.Equal((0, ""), (exit, stderr));
            Assert.Equal(new[] { WorldHash.ComputeHex(loaded) }, CliProcess.Lines(stdout));
            Assert.NotEqual(ConstructionHoldsV33FixtureTests.V33SaveHash, CliProcess.Lines(stdout)[0]);
            Assert.Equal(ConstructionHoldsV33FixtureTests.V33SaveHash, SchemaProjection.V33(SaveFiles.Load(path, 256)));
        }
        finally
        {
            File.Delete(path);
        }
    }

    /// <summary>
    /// D-055 fix round (2026-10-17, T4-L1-T4V1-F1) — A RE-ENQUEUE ON THE MIGRATED v33 WORLD. One batch stamped K = 2 cancels s0's
    /// part-built head (slot 0, key 0, p = 700,000) and orders a new granary at s0, on the four arms with the session's log.
    /// State 3 holds s0's queue EXACTLY as [(slot 0, key 2, p 0, with its row)]: a NEW entry in the freed slot, keyed by its
    /// stamp turn, inheriting neither the forfeited progress nor the cancelled entry's priority. So it is served BEHIND s2
    /// (key 0): s2 draws the released stone and reaches 450,000 in step 2 → 3, whose sinks are exactly s2's Delivered(40,
    /// 450,000) = 18 timber and Delivered(20, 450,000) = 9 stone (s0's new entry has no builders and draws nothing); the purse
    /// then publishes s2's remaining 22 timber and the new entry's 40 (no stone is left to set aside). s2 reaches 850,000 at
    /// 4, the new entry still (key 2, p 0). Control (the session's log alone): s2 150,000 at 3. Kills M-INH (a re-enqueue
    /// into a slot this batch freed inheriting the cancelled entry's key 0: it would then order ahead of s2 and take the
    /// stone).
    /// </summary>
    [Fact]
    public void V33_F_ACancelOfThePartBuiltHeadAndAReEnqueueInOneBatchAtK_TheNewEntryIsKeyedK_AndServedBehindS2()
    {
        WorldState[] c = Arms(V33Arms(), l => V33Exec(Shipped, l), With(SessionOrders(), C.Cancel(K33, Player, S0, 0), C.Enqueue(K33, Player, S0)), 2);
        Assert.Equal(new[] { (0, 0L, 700_000L, true) }, Entries(c[0], S0));
        Assert.Equal(new[] { (0, 2L, 0L, true) }, Entries(c[1], S0));
        Assert.Equal(new[] { (0, 0L, 450_000L, true) }, Entries(c[1], S2));
        Eq((18L, 9L), (C.Sunk(c[1], Timber) - C.Sunk(c[0], Timber), C.Sunk(c[1], Stone) - C.Sunk(c[0], Stone)), "step 2 -> 3 draws only s2's units");
        Eq((62L, 0L), (Held(c[1], Timber), Held(c[1], Stone)), "state 3: s2's remaining 22 timber and the new entry's 40");
        Eq((0L, 0L), (ConstructionQuery.Built(c[1], S0, Granary), ConstructionQuery.Built(c[2], S0, Granary)), "s0 never built");
        Assert.Equal(new[] { (0, 2L, 0L, true) }, Entries(c[2], S0));
        Eq(850_000L, ConstructionQuery.ProgressOf(c[2], Q2), "s2 at 4");
        WorldState[] control = Arms([("v33 load", LoadV33())], l => V33Exec(Shipped, l), SessionOrders(), 1);
        Eq(150_000L, ConstructionQuery.ProgressOf(control[1], Q2), "control: no batch");
    }

    /// <summary>
    /// D-055 fix round (2026-10-17, T4-L1-T4V1-F2) — A BINDING SPLIT ON THE MIGRATED v33 WORLD (ADR-039 §3 after the migration).
    /// The four arms rigged at K by RoadRig (a 600-km trackway s0–s2, all builders, stone plenty, the timber at s0 refilled to
    /// 100 before each step) and stepped by construction + road development with a road order each turn. A kind 12 to b = 0
    /// stamped K governs the step that delivers it: in step 2 → 3 road works take all 100 timber and building work none, and
    /// the row (1, 0) holds at 3 and governs 3 → 4 alike. Control (no kind 12): the MIGRATED row (1, 30) binds by itself —
    /// building work is held to E_b = 30 (s0's last 12 and 18 to s2) and roads get 70; then 22 (s2's remainder) and 78. Kills
    /// M-A (the kind-12 block moved after building work) on the migration path.
    /// </summary>
    [Fact]
    public void V33_S_ABindingSplitStampedAtK_GovernsTheDeliveringStep_AndTheMigratedRowBindsWithoutIt()
    {
        (WorldState[] c, (long Road, long Build)[] split) = SplitRun(V33Arms(), K33, bZero: true);
        Eq((new ResourceAllocationRow(Player, 30), new ResourceAllocationRow(Player, 0), new ResourceAllocationRow(Player, 0)),
            (Assert.Single(TableRows.Rows(c[0].ResourceAllocations)), Assert.Single(TableRows.Rows(c[1].ResourceAllocations)), Assert.Single(TableRows.Rows(c[2].ResourceAllocations))), "rows at 2, 3, 4");
        Eq(((100L, 0L), (100L, 0L)), (split[0], split[1]), "b = 0: (road, building) timber in 2 -> 3 and 3 -> 4");
        Eq((700_000L, 0L), (ConstructionQuery.ProgressOf(c[2], Q0), ConstructionQuery.ProgressOf(c[2], Q2)), "the heads wait");
        (WorldState[] k, (long Road, long Build)[] control) = SplitRun(V33Arms(), K33, bZero: false);
        Eq(((70L, 30L), (78L, 22L)), (control[0], control[1]), "control: the migrated (1, 30) row binds");
        Eq((1L, 450_000L), (ConstructionQuery.Built(k[1], S0, Granary), ConstructionQuery.ProgressOf(k[1], Q2)), "control at 3: s0 completed, s2 at 450,000");
    }
}
