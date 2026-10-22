using System.Security.Cryptography;
using Sim.Core;
using Sim.Core.Kernel;
using Sim.Core.State;
using Sim.Core.Systems;
using Sim.Ui.ViewModel;
using Xunit;

namespace Sim.Ui.Tests;

/// <summary>
/// D-055 (2026-10-16; the Director's batch-4 instruction, item 4 — persistence and lifecycle across "resumed UI sessions";
/// batch-4 design §4 P3; recon persistence-audit §7 P3) — THE CONSTRUCTION LIFECYCLE ACROSS A UI RESUME
/// (<see cref="UiSession.Resume"/>: re-found from the manifest, replay the order log through the same executor, re-queue
/// the orders stamped with the last played turn). Every state comes from the session's own emitters on the dev-size
/// one-AI world (seed 42, 256 px, four settlements; the player holds s0 and s2) — the v33 fixture's Q17 recipe: granaries
/// at s0 and s2 at turn 0 with the player's 30/70 split, and s0's builders taken away at turn 1, so from step 2 → 3 s0
/// waits part-built (700,000) and SETS ASIDE its remaining 6 stone while s2, younger in the building order, gets the rest.
///
/// The cases: kinds 11 and 12 PENDING across the save (kills M-C, a re-queue that drops them); a binding hold resumed; a
/// part-built head WAITING for materials resumed; a re-enqueue pending across the save; a flat session relabelled v32 with
/// construction; the REAL v33 session fixture under v34 with X1's premises (i)–(iii) on the UI path ((iv), `sim inspect`,
/// is Sim.Tests' ConstructionHoldsV33FixtureTests.PremiseIV — this project does not reference Sim.Cli); and a recorded
/// cancel whose replay under this build's rules hits a SHIFTED SLOT — refused as a legacy kind 11, applied to the right
/// entry as a kind 13 (D-056).
///
/// EVERY HOLD ASSERTION HAS A BINDING PREMISE (design §4, review L4-16): the same step from the resumed or replayed world
/// under the PreReservations twin of the production pipeline (sim.json construction.reservations false — the batch-3
/// rule) differs, and the younger head's exact progress is pinned on the resumed or replayed world.
///
/// A BINDING SPLIT CANNOT BE REACHED IN A UI SESSION by the emitters (MEASURED, T4 step 1 probe): the player knows no road
/// class in the first turns, so <see cref="UiSession.EmitRoadOrder"/> appends nothing and D_r = 0 — the split never
/// binds. The binding split across a save is Sim.Tests' ConstructionLifecyclePersistenceTests L7/L8 (and the allocation CI
/// leg); here the kind-12 order is pinned for its delivery across the resume (its row), which is what M-C breaks.
/// </summary>
public class ConstructionLifecycleResumeTests
{
    private const int Granary = 1;
    private const long N = ConstructionQuery.ProgressUnits;
    private static readonly PolityId Me = new(1);
    private static readonly SettlementId S0 = new(0), S2 = new(2);
    /// <summary>The default sector mix with no construction share (Sectors.Default's other four in their proportions).</summary>
    private static readonly int[] NoBuilders = [55, 15, 10, 12, 0];
    private const string StartedAt = "2026-10-16 12:00:00";

    // ================================================================== rig

    private static string TempDir(out string log)
    {
        string dir = Path.Combine(Path.GetTempPath(), "d055-lifecycle-resume-"
            + Environment.ProcessId.ToString(System.Globalization.CultureInfo.InvariantCulture) + "-" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(dir);
        log = Path.Combine(dir, "orders-20261016-120000.bin");
        return dir;
    }

    /// <summary>The Q17 rig at turn 2 (the fixture's recipe through the emitters): the manifest exported at the start.</summary>
    private static UiSession Q17(string log, bool? wrapX = null)
    {
        UiSession live = UiSession.StartWith(TestFog.PreDiscovery, 42, sizeOverridePx: 256, settlementsOverride: 4, aiEmpiresOverride: 1, wrapXOverride: wrapX);
        live.ExportManifest(StartedAt, log);
        Assert.True(live.EmitAllocationOrder(30));
        Assert.True(live.EmitConstructionOrder(S0.Value, Granary));
        Assert.True(live.EmitConstructionOrder(S2.Value, Granary));
        live.EndTurn();                                                                       // turn 1
        Assert.True(live.EmitSectorOrders(NoBuilders, S0.Value));
        live.EndTurn();                                                                       // turn 2
        return live;
    }

    private static UiSession SaveAndResume(UiSession live, string log)
    {
        live.Save(log);
        live.ExportTrace(UiSession.TracePath(log));
        return UiSession.Resume(UiSession.ManifestPath(log), out _, TestFog.PreDiscovery);
    }

    private static TurnExecutor Production(SimConfig cfg, OrderLog orders)
    {
        using var pipe = Sim.Data.DataFiles.OpenPipeline();
        return new TurnExecutor(UiSession.ProductionEra(), PipelineLoader.Load(pipe, SystemCatalog.All(cfg, UiSession.ProductionWorldgen())), orders);
    }

    /// <summary>The production pipeline under <paramref name="reservations"/> (false: TestConfigs.PreReservations restated —
    /// this project does not reference Sim.Tests), delivering <paramref name="orders"/>.</summary>
    private static TurnExecutor Twin(bool reservations, OrderLog orders)
    {
        // M6 EX (2026-10-22): the rig runs the pre-M6 economy (TestFog.PreDiscovery — a hidden stone deposit would move every
        // figure below), so its twins are built on the same config.
        SimConfig c = TestFog.PreDiscovery;
        // D-058 (2026-10-19) CAUSE BLOCK: TestConfigs.PreReservations now also switches off the newer continueWithinTurn
        // layer (the R4 rule: the batch-3 rule had no in-step continuation), so the restatement does too.
        return Production(reservations ? c : c with { Construction = c.Construction! with { Reservations = false, ContinueWithinTurn = false } }, orders);
    }

    private static ConstructionQueueRow[] Queue(IReadOnlyWorldState w, SettlementId s) => ConstructionQuery.Queue(w, s);

    private static long P(IReadOnlyWorldState w, SettlementId s)
    {
        ConstructionQueueRow[] q = Queue(w, s);
        return q.Length == 0 ? -1 : ConstructionQuery.ProgressOf(w, q[0]);
    }

    private static (int Slot, long Key, long P)[] Entries(IReadOnlyWorldState w, SettlementId s)
    {
        ConstructionQueueRow[] q = Queue(w, s);
        var e = new (int, long, long)[q.Length];
        for (int i = 0; i < q.Length; i++)
            e[i] = (q[i].Slot, ConstructionQuery.EnqueuedTurnOf(w.ConstructionProgress, q[i]), ConstructionQuery.ProgressOf(w, q[i]));
        return e;
    }

    private static int StoneId(UiSession s) => s.Config.Goods!.IdOf("stone");

    private static long PurseStone(UiSession s)
    {
        int stone = StoneId(s);
        long sum = 0;
        foreach (SettlementId at in LabourActivities.ControlledSettlements(s.World, Me))
            for (int i = 0; i < s.World.GoodStocks.Count; i++)
                if (s.World.GoodStocks[i].Settlement == at && s.World.GoodStocks[i].Good.Value == stone) sum += s.World.GoodStocks[i].Amount.Value;
        return sum;
    }

    /// <summary>The Ledger's total sunk of <paramref name="good"/> under ConstructionMaterials (no road works here).</summary>
    private static long Sunk(IReadOnlyWorldState w, int good)
    {
        for (int i = 0; i < w.LedgerFlows.Count; i++)
            if (w.LedgerFlows[i].Quantity == ConservedQuantityIds.OfGood(new GoodId(good)) && w.LedgerFlows[i].Reason == ReasonIds.ConstructionMaterials)
                return w.LedgerFlows[i].TotalSunk;
        return 0;
    }

    private static long Held(IReadOnlyWorldState w, int good)
    {
        long sum = 0;
        for (int i = 0; i < w.ConstructionHolds.Count; i++)
            if (w.ConstructionHolds[i].Purse == Reservations.PolityPurse(Me) && w.ConstructionHolds[i].Good == good) sum += w.ConstructionHolds[i].Units;
        return sum;
    }

    private static OrderRecord[] Rows(IReadOnlyList<OrderRecord> orders) => [.. orders];

    private static T[] All<T>(IReadOnlyTable<T> table) where T : unmanaged
    {
        var rows = new T[table.Count];
        for (int i = 0; i < rows.Length; i++) rows[i] = table[i];
        return rows;
    }

    /// <summary>The world's hash at schema v33 (Sim.Tests' SchemaProjection.HashAtSchemaV33, restated): a clone with the v34
    /// table (section 61, ConstructionHolds) cleared, serialized, its empty count prefix (4 zero bytes) dropped, SHA-256.</summary>
    private static string HashAtSchemaV33(WorldState world, out int holdRows)
    {
        WorldState c = world.Clone();
        holdRows = c.ConstructionHolds.Count;
        c.ConstructionHolds.Clear();
        // M6 EX (v35): the four discovery tables follow section 61 — asserted empty (the rig runs without discovery) and dropped.
        Assert.Equal(0, c.KnownExtents.Count + c.Explorations.Count + c.SettlementSightings.Count + c.SettlementSurveys.Count);
        using var buffer = new MemoryStream();
        using (var writer = new BinaryWriter(buffer, System.Text.Encoding.UTF8, leaveOpen: true)) CanonicalSchema.Write(c, writer);
        byte[] full = buffer.ToArray();
        for (int i = full.Length - 20; i < full.Length; i++) Assert.Equal(0, full[i]);
        return Convert.ToHexStringLower(SHA256.HashData(full.AsSpan(0, full.Length - 20)));
    }

    // ================================================================== kinds 11 and 12 pending across the save (M-C)

    /// <summary>
    /// KINDS 11 AND 12 PENDING ACROSS A SAVE — the resume RE-QUEUES them and the first End Turn delivers them, turn-exact.
    /// At turn 2 (s0's granary part-built at 700,000, its builders gone) the player cancels s0's HEAD (kind 11) and moves the
    /// split from 30 to 50 (kind 12); the session is saved with both issued and not delivered, and resumed (same schema:
    /// every replayed hash checked against the trace). At once: no notice, the same world, and the resumed session's queued
    /// batch equals the live one and holds both orders. One End Turn on both: at turn 3 s0's entry and its progress row are
    /// gone (they were there at turn 2), the split row is 50, and — because the cancel is applied BEFORE the walk — s0's
    /// set-aside is released in that very step: s2 draws all 9 stone (p 450,000), where without the cancel it draws 3 (p
    /// 150,000: the binding control). No refund: the 14 stone that went into s0 stay sunk; the step's sinks are exactly s2's
    /// 9 and the purse ends with none. This kills M-C (UiSession.ReplayTo's re-queue dropping kinds 11 and 12).
    /// </summary>
    [Fact]
    public void Kinds11And12PendingAtTheSave_AreReQueuedByTheResume_AndTheFirstEndTurnDeliversThemTurnExact()
    {
        string dir = TempDir(out string log);
        try
        {
            UiSession live = Q17(log);
            int stone = StoneId(live);
            Assert.Equal(700_000, P(live.World, S0));
            Assert.True(live.EmitCancelConstruction(S0.Value, 0));                           // kind 11: s0's part-built HEAD
            Assert.True(live.EmitAllocationOrder(50));                                       // kind 12: 30 → 50
            Assert.False(live.EmitRoadOrder(100.0));                                         // premise: no road class known
            UiSession resumed = SaveAndResume(live, log);

            Assert.Empty(resumed.ResumeNotice);
            Assert.Equal(WorldHash.ComputeHex(live.World), WorldHash.ComputeHex(resumed.World));
            OrderRecord[] queued = Rows(live.QueuedOrders());
            Assert.Equal(queued, Rows(resumed.QueuedOrders()));
            Assert.Contains(ConstructionQuery.CancelOrder(live.World, Me, S0, 0), queued);
            Assert.Contains(ResourceAllocation.Order(live.World, Me, 50), queued);
            Assert.Equal(new ResourceAllocationRow(Me, 30), Assert.Single(All(resumed.World.ResourceAllocations)));
            Assert.Equal(700_000, P(resumed.World, S0));                                     // still queued at the save turn
            long sunk2 = Sunk(resumed.World, stone);

            live.EndTurn();
            resumed.EndTurn();
            Assert.Equal(WorldHash.ComputeHex(live.World), WorldHash.ComputeHex(resumed.World));
            foreach (UiSession s in new[] { live, resumed })
            {
                Assert.Equal(3, s.World.Clock.Turn);
                Assert.Empty(Queue(s.World, S0));
                Assert.DoesNotContain(All(s.World.ConstructionProgress), r => r.Settlement == S0);
                Assert.Equal(new ResourceAllocationRow(Me, 50), Assert.Single(All(s.World.ResourceAllocations)));
                Assert.Equal(450_000, P(s.World, S2));                                       // the released stone, same step
                Assert.Equal(ConstructionQuery.Delivered(20, 450_000), Sunk(s.World, stone) - sunk2);   // 9: s2's draw only
                Assert.Equal(0, PurseStone(s));
                Assert.Equal(0, Held(s.World, stone));
            }
            Assert.Equal(live.TraceLines.ToArray(), resumed.TraceLines.ToArray());

            // The binding control: the same rig without the cancel — s0 keeps its 6 stone set aside and s2 gets 3.
            UiSession kept = Q17(Path.Combine(dir, "orders-20261016-120001.bin"));
            kept.EndTurn();
            Assert.Equal((700_000L, 150_000L, 6L), (P(kept.World, S0), P(kept.World, S2), Held(kept.World, stone)));
        }
        finally
        {
            Directory.Delete(dir, recursive: true);
        }
    }

    // ================================================================== a binding hold, resumed

    /// <summary>
    /// A BINDING HOLD ACROSS A RESUME. Saved at turn 2 (s0 at 700,000 with no builders from this step on; s2 at 0), resumed
    /// with its trace checked, both sessions End Turn: at turn 3 s0 still waits at 700,000 and sets aside its remaining
    /// Remaining(20, 700,000) = 6 stone, and s2 — younger — is at exactly 150,000 (3 stone in), HeldForEarlier naming s0. The
    /// BINDING PREMISE on the resumed world: the same step under the PreReservations twin of the production pipeline puts s2
    /// at 450,000 (the batch-3 figure), while the shipped twin reproduces the session's own step (the harness is the
    /// session's executor). The two sessions stay hash-identical to s2's completion at turn 5.
    /// </summary>
    [Fact]
    public void ABindingHold_Resumed_TheYoungerHeadDrawsWhatTheHoldLeaves_AndThePreReservationsStepDiffers()
    {
        string dir = TempDir(out string log);
        try
        {
            UiSession live = Q17(log);
            UiSession resumed = SaveAndResume(live, log);
            Assert.Empty(resumed.ResumeNotice);
            int stone = StoneId(resumed);
            WorldState at2 = resumed.World;
            long sunk2 = Sunk(at2, stone);

            live.EndTurn();
            resumed.EndTurn();
            Assert.Equal(WorldHash.ComputeHex(live.World), WorldHash.ComputeHex(resumed.World));
            Assert.Equal((700_000L, 150_000L), (P(resumed.World, S0), P(resumed.World, S2)));
            Assert.Equal(ConstructionQuery.Remaining(20, 700_000), Held(resumed.World, stone));
            Assert.Equal(ConstructionQuery.Delivered(20, 150_000), Sunk(resumed.World, stone) - sunk2);
            HeadStatusReport s2 = ConstructionQuery.HeadStatus(resumed.World, resumed.Config, S2, null, 10.0)!;
            Assert.Equal((MaterialCause.HeldForEarlier, (QueueEntryRef?)new QueueEntryRef(S0, 0, Granary)), (s2.Cause, s2.Holder));

            // The binding premise on the resumed world (the batch at turn 2 includes the AI's orders End Turn appended).
            Assert.Equal(WorldHash.ComputeHex(resumed.World), WorldHash.ComputeHex(Twin(true, resumed.Orders).Step(at2)));
            WorldState pre = Twin(false, resumed.Orders).Step(at2);
            Assert.Equal((700_000L, 450_000L, 0), (P(pre, S0), P(pre, S2), pre.ConstructionHolds.Count));

            for (int t = 0; t < 2; t++)
            {
                live.EndTurn();
                resumed.EndTurn();
                Assert.Equal(WorldHash.ComputeHex(live.World), WorldHash.ComputeHex(resumed.World));
            }
            Assert.Equal((1L, 700_000L), (ConstructionQuery.Built(resumed.World, S2, Granary), P(resumed.World, S0)));
        }
        finally
        {
            Directory.Delete(dir, recursive: true);
        }
    }

    // ================================================================== a head waiting for materials, resumed

    /// <summary>
    /// D-058 (2026-10-19): the figures below are batch 4's; under the Director's "build one at a time" ruling s0's second
    /// granary is CONTINUED in step 2 → 3 (to 150,000) and s2 waits at 0, held for it, until turn 6 — the body's cause blocks.
    /// A PART-BUILT HEAD WAITING FOR MATERIALS (Δp = 0) ACROSS A RESUME. s0 orders two granaries at turn 0, s2 one at turn
    /// 1; s0's builders go at turn 2. s0's first granary completes in step 2 → 3 and its second (ordered at turn 0, so OLDER
    /// than s2's) heads from step 3 → 4 with no builders: it sets aside its whole 20 stone as the purse fills, and s2 —
    /// part-built at 150,000 — waits: Δp = 0 in step 3 → 4. Saved at turn 4 and resumed, then End Turn: s2 is STILL at
    /// 150,000 at turn 5 (HeldForEarlier naming s0's slot 1, which sets aside 17 stone); under the PreReservations twin the
    /// same step completes s2's granary (the binding premise). At turn 6, with 20 set aside, s2 moves again: 500,000.
    /// </summary>
    [Fact]
    public void AHeadWaitingForMaterials_Resumed_KeepsWaitingTurnExact_AndThePreReservationsStepWouldCompleteIt()
    {
        string dir = TempDir(out string log);
        try
        {
            UiSession live = UiSession.StartWith(TestFog.PreDiscovery, 42, sizeOverridePx: 256, settlementsOverride: 4, aiEmpiresOverride: 1);
            live.ExportManifest(StartedAt, log);
            Assert.True(live.EmitConstructionOrder(S0.Value, Granary));
            Assert.True(live.EmitConstructionOrder(S0.Value, Granary));
            live.EndTurn();                                                                   // 1
            Assert.True(live.EmitConstructionOrder(S2.Value, Granary));
            live.EndTurn();                                                                   // 2
            Assert.True(live.EmitSectorOrders(NoBuilders, S0.Value));
            live.EndTurn();                                                                   // 3
            // D-058 (2026-10-19) CAUSE BLOCK — was: s2 at 150,000 and s0's second granary heading at p = 0 at turn 3. Under the
            // Director's "build one at a time" ruling s0's first granary completes in step 2 → 3 with builders to spare, so
            // its second (ordered at turn 0, OLDER than s2's) continues in that same step and takes the stone s2 got under
            // the batch-4 rule: MEASURED, s0's slot 1 at 150,000 and s2 still at 0 at turn 3. From turn 3 s0 has no builders:
            // its part-built second granary sets aside what it still needs (17 stone) and s2 waits behind it.
            Assert.Equal(0, P(live.World, S2));
            Assert.Equal(new[] { (1, 0L, 150_000L) }, Entries(live.World, S0));                     // s0's second granary heads, part-built
            live.EndTurn();                                                                   // 4
            Assert.Equal(0, P(live.World, S2));                                               // premise: it waited in 3 → 4

            UiSession resumed = SaveAndResume(live, log);
            Assert.Empty(resumed.ResumeNotice);
            int stone = StoneId(resumed);
            WorldState at4 = resumed.World;
            live.EndTurn();
            resumed.EndTurn();
            Assert.Equal(WorldHash.ComputeHex(live.World), WorldHash.ComputeHex(resumed.World));
            Assert.Equal(0, P(resumed.World, S2));                                            // Δp = 0 again, after the resume
            Assert.Equal(Sunk(at4, stone), Sunk(resumed.World, stone));                        // nothing drawn
            Assert.Equal(17, Held(resumed.World, stone));
            HeadStatusReport s2 = ConstructionQuery.HeadStatus(resumed.World, resumed.Config, S2, null, 10.0)!;
            Assert.Equal((MaterialCause.HeldForEarlier, (QueueEntryRef?)new QueueEntryRef(S0, 1, Granary)), (s2.Cause, s2.Holder));

            WorldState pre = Twin(false, resumed.Orders).Step(at4);                            // the binding premise
            // D-058: the premise was "the PreReservations step completes s2's granary"; with s0's continuation having taken s2's
            // earlier stone, the twin's step takes s2 from 0 to 850,000 (MEASURED) — it moves, where the hold rule keeps it at 0.
            Assert.Equal((0L, 850_000L), (ConstructionQuery.Built(pre, S2, Granary), P(pre, S2)));

            live.EndTurn();
            resumed.EndTurn();
            Assert.Equal(WorldHash.ComputeHex(live.World), WorldHash.ComputeHex(resumed.World));
            Assert.Equal((500_000L, 17L), (P(resumed.World, S2), Held(resumed.World, stone)));   // D-058: s0's slot 1 needs 17 (was 20)
        }
        finally
        {
            Directory.Delete(dir, recursive: true);
        }
    }

    // ================================================================== a re-enqueue pending across the save

    /// <summary>
    /// A RE-ENQUEUE ACROSS A RESUME. At turn 3 of the Q17 rig (s2 part-built at 150,000 behind s0's set-aside) the player
    /// cancels s2's head and orders a granary there again — one batch — and the session is saved with both pending. The
    /// resume re-queues both; the first End Turn makes a NEW entry: slot 0, EnqueuedTurn 3 (the stamp turn), served behind
    /// s0's set-aside: p 400,000 at turn 4 (8 stone), where the PreReservations step gives it 700,000 (the binding premise).
    /// Saved again at turn 4 with the batch delivered: the new key survives the resume row for row and the next turn is
    /// the live one.
    /// </summary>
    [Fact]
    public void AReEnqueuePendingAtTheSave_IsANewEntryKeyedToItsStampTurn_ServedBehindTheOlderHeadsSetAside()
    {
        string dir = TempDir(out string log);
        try
        {
            UiSession live = Q17(log);
            live.EndTurn();                                                                   // 3
            Assert.Equal((0, 0L, 150_000L), Assert.Single(Entries(live.World, S2)));
            Assert.True(live.EmitCancelConstruction(S2.Value, 0));
            Assert.True(live.EmitConstructionOrder(S2.Value, Granary));
            UiSession resumed = SaveAndResume(live, log);
            Assert.Empty(resumed.ResumeNotice);
            Assert.Equal(Rows(live.QueuedOrders()), Rows(resumed.QueuedOrders()));
            int stone = StoneId(resumed);
            WorldState at3 = resumed.World;

            live.EndTurn();
            resumed.EndTurn();
            Assert.Equal(WorldHash.ComputeHex(live.World), WorldHash.ComputeHex(resumed.World));
            Assert.Equal(new[] { (0, 3L, 400_000L) }, Entries(resumed.World, S2));
            Assert.Equal((700_000L, 6L), (P(resumed.World, S0), Held(resumed.World, stone)));
            WorldState pre = Twin(false, resumed.Orders).Step(at3);                            // the binding premise
            Assert.Equal(new[] { (0, 3L, 700_000L) }, Entries(pre, S2));

            // Saved again with the batch delivered.
            UiSession again = SaveAndResume(live, log);
            Assert.Equal(new[] { (0, 3L, 400_000L) }, Entries(again.World, S2));
            live.EndTurn();
            again.EndTurn();
            Assert.Equal(WorldHash.ComputeHex(live.World), WorldHash.ComputeHex(again.World));
            Assert.Equal(850_000, P(again.World, S2));
        }
        finally
        {
            Directory.Delete(dir, recursive: true);
        }
    }

    // ================================================================== a flat session relabelled v32

    /// <summary>
    /// A FLAT SESSION WITH CONSTRUCTION, RELABELLED v32 (ChronicleAndResumeTests.RecordSchema): the Q17 rig on the flat map
    /// (wrapXOverride false), played to turn 3 and saved. Resumed by the D-008 path: rebuilt FLAT (TopologyFlags.RecordedFlat),
    /// the four-line notice with the flat line 2, the trace not checked — and the world is the live one and a fresh flat
    /// replay's. ANTI-VACUITY: it is replayed under THIS build's rules (D-054 Q9's "replays under batch-3 rules" became
    /// "this build's rules"): the replay reaches a part-built row (s0 at 600,000) and a binding set-aside — s0 sets aside
    /// 7 stone and s2 stays at 0, where the PreReservations step from the replayed turn-2 world puts s2 at 350,000.
    /// </summary>
    [Fact]
    public void AFlatSessionRelabelledV32_WithConstruction_ResumesFlat_UnderThisBuildsRules_TheSetAsideBinding()
    {
        string dir = TempDir(out string log);
        try
        {
            UiSession live = Q17(log, wrapX: false);
            live.EndTurn();                                                                   // 3
            live.Save(log);
            live.ExportTrace(UiSession.TracePath(log));
            string manifest = UiSession.ManifestPath(log);
            ChronicleAndResumeTests.RecordSchema(manifest, 32);
            UiSession resumed = UiSession.Resume(manifest, out _);

            Assert.False(resumed.World.Terrain!.WrapX);
            Assert.Equal(UiSession.SchemaMismatchNotice(32, CanonicalSchema.Version), resumed.ResumeNotice);
            Assert.Equal("Replayed from its seed and orders on the flat map v32 used (D-008).", resumed.ResumeNotice[1]);
            Assert.Equal(WorldHash.ComputeHex(live.World), WorldHash.ComputeHex(resumed.World));
            OrderLog saved;
            using (FileStream f = File.OpenRead(log)) saved = OrderLog.Load(f);
            UiSession fresh = UiSession.StartWith(TestFog.PreDiscovery, 42, sizeOverridePx: 256, settlementsOverride: 4, aiEmpiresOverride: 1, wrapXOverride: false);
            fresh.ReplayTo(saved, 3);
            Assert.Equal(WorldHash.ComputeHex(fresh.World), WorldHash.ComputeHex(resumed.World));

            int stone = StoneId(resumed);
            Assert.Equal((600_000L, 0L, 7L), (P(resumed.World, S0), P(resumed.World, S2), Held(resumed.World, stone)));
            Assert.InRange(P(resumed.World, S0), 1, N - 1);                                    // a part-built row
            UiSession at2 = UiSession.StartWith(TestFog.PreDiscovery, 42, sizeOverridePx: 256, settlementsOverride: 4, aiEmpiresOverride: 1, wrapXOverride: false);
            at2.ReplayTo(saved, 2);                                                           // the turn-2 batch re-queued
            Assert.Equal(WorldHash.ComputeHex(resumed.World), WorldHash.ComputeHex(Twin(true, at2.Orders).Step(at2.World)));
            Assert.Equal(350_000, P(Twin(false, at2.Orders).Step(at2.World), S2));             // the binding premise

            for (int t = 0; t < 2; t++)
            {
                live.EndTurn();
                resumed.EndTurn();
                Assert.Equal(WorldHash.ComputeHex(live.World), WorldHash.ComputeHex(resumed.World));
            }
        }
        finally
        {
            Directory.Delete(dir, recursive: true);
        }
    }

    // ================================================================== the real v33 session under v34

    private const string V33Stamp = "f1cc70bce302a9a0";

    /// <summary>
    /// THE REAL v33 SESSION (Sim.Tests/Fixtures/v33-session-q17, recorded by the batch-3 build) UNDER v34, X1's premises on
    /// the UI path. (i) Its order log, replayed from the UI's founding (UiFounding.Found) through the PreReservations twin of
    /// the production pipeline, reproduces EVERY recorded trace hash (turns 0–6) through the v33 projection. (ii) The
    /// session's replay by this build (UiSession.ReplayTo) equals the trace through the projection at turn 2 although it
    /// publishes set-asides there (they bind nothing yet), and first differs at turn 3, where s0 sets aside its remaining 6
    /// stone and s2 is at 150,000 — the (i) replay's s2 is at 450,000. (iii) UiSession.Resume: the four-line v33 notice,
    /// the cylinder, the fresh replay's world. (iv) — `sim inspect` — is Sim.Tests' PremiseIV.
    /// </summary>
    [Fact]
    public void TheRealV33Session_UnderV34_PremisesIToIII_OnTheUiPath()
    {
        string src = Path.Combine(AppContext.BaseDirectory, "Fixtures", "v33-session-q17");
        OrderLog saved;
        using (FileStream f = File.OpenRead(Path.Combine(src, "orders-" + V33Stamp + ".bin"))) saved = OrderLog.Load(f);
        string[] trace = File.ReadAllLines(Path.Combine(src, "trace-" + V33Stamp + ".csv"));
        Assert.Equal(SessionTrace.Header, trace[0]);
        string Hash(int turn) => trace[turn + 1].Split(',')[^1];

        // (i)
        WorldState w = UiFounding.Found(42, 256, 4, 1);
        TurnExecutor pre = Twin(false, saved);
        WorldState? preAt3 = null;
        for (int t = 0; t <= 6; t++)
        {
            if (t > 0) w = pre.Step(w);
            Assert.True(Hash(t) == HashAtSchemaV33(w, out int rows) && rows == 0, $"(i) the PreReservations replay differs from the trace at turn {t}");
            if (t == 3) preAt3 = w;
        }

        // (ii)
        UiSession two = UiSession.StartWith(TestFog.PreDiscovery, 42, sizeOverridePx: 256, settlementsOverride: 4, aiEmpiresOverride: 1);
        two.ReplayTo(saved, 2);
        Assert.Equal(Hash(2), HashAtSchemaV33(two.World, out int heldAt2));
        Assert.True(heldAt2 > 0, "anti-vacuity: set-asides are published at turn 2 (binding nothing yet)");
        UiSession three = UiSession.StartWith(TestFog.PreDiscovery, 42, sizeOverridePx: 256, settlementsOverride: 4, aiEmpiresOverride: 1);
        three.ReplayTo(saved, 3);
        Assert.NotEqual(Hash(3), HashAtSchemaV33(three.World, out _));
        Assert.Equal((700_000L, 150_000L, 6L), (P(three.World, S0), P(three.World, S2), Held(three.World, StoneId(three))));
        Assert.Equal((700_000L, 450_000L), (P(preAt3!, S0), P(preAt3!, S2)));

        // (iii)
        string dir = Path.Combine(Path.GetTempPath(), "d055-v33-ui-" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(dir);
        try
        {
            foreach (string f in Directory.GetFiles(src)) File.Copy(f, Path.Combine(dir, Path.GetFileName(f)));
            UiSession resumed = UiSession.Resume(Path.Combine(dir, "session-" + V33Stamp + ".json"), out _);
            Assert.Equal(UiSession.SchemaMismatchNotice(33, CanonicalSchema.Version), resumed.ResumeNotice);
            Assert.Equal("Resumed by replaying its seed and orders (D-008).", resumed.ResumeNotice[1]);
            Assert.True(resumed.World.Terrain!.WrapX);
            UiSession six = UiSession.StartWith(TestFog.PreDiscovery, 42, sizeOverridePx: 256, settlementsOverride: 4, aiEmpiresOverride: 1);
            six.ReplayTo(saved, 6);
            Assert.Equal(WorldHash.ComputeHex(six.World), WorldHash.ComputeHex(resumed.World));
            Assert.NotEqual(Hash(6), HashAtSchemaV33(resumed.World, out _));                 // the world differs from the one played
        }
        finally
        {
            Directory.Delete(dir, recursive: true);
        }
    }

    // ================================================================== a recorded cancel whose slot shifts in the replay

    /// <summary>
    /// A RECORDED CANCEL WHOSE SLOT SHIFTS IN THE REPLAY (D-055 limitation, documented — design X1, review L4-02; FIXED by
    /// D-056, 2026-10-18: until then this test pinned the hazard as passing). Cancels name their entry; slots are reused.
    /// The session is RECORDED under the batch-3 rule (the PreReservations twin, AI orders appended each turn as End Turn
    /// does) and labelled schema v33 with its trace in the v33 form, as the batch-3 build wrote it: s0 orders two granaries
    /// at turn 0, s2 one at turn 1, s0's builders go at turn 2; s2's granary completes at turn 5; the player orders another
    /// granary at s2 at turn 5 — slot 0, the queue being empty — and cancels it at turn 6: the second granary, then at
    /// 500,000, leaves; s2 has one granary built. Replayed by this build under the hold rule, s0's waiting granary sets aside
    /// the stone, s2's FIRST granary is still at 500,000 in slot 0 at turn 6 and the turn-5 order went to slot 1.
    /// <list type="bullet">
    /// <item>RECORDED AS KIND 11 (what the batch-3 build wrote: settlement and slot only): before D-056 the cancel removed the
    ///   FIRST granary (its 500,000 forfeited), which the player never cancelled, and the resume was accepted. Now the
    ///   resume is REFUSED before the turn-6 batch — the replayed world at turn 6 is not the recorded one in any readable
    ///   form — with the diagnostic on stderr and in the dialog, exit 2, and every session file byte for byte as it was;</item>
    /// <item>RECORDED AS KIND 13 (what every emitter now writes: the entry's identity — slot 0, the granary, ordered at turn
    ///   5): the resume is accepted (the four-line v33 notice) and the cancel removes the turn-5 granary in slot 1 — the one
    ///   the player cancelled; the first granary goes on being built.</item>
    /// </list>
    /// </summary>
    [Theory]
    [InlineData(true)]
    [InlineData(false)]
    public void ARecordedCancel_WhoseSlotShiftsInTheReplay_Kind11IsRefused_Kind13CancelsTheEntryThePlayerCancelled(bool legacy)
    {
        string dir = TempDir(out string log);
        try
        {
            // The batch-3 recording (the PreReservations twin): the player's orders through the very factories the emitters
            // use (the legacy arm writes the slot-only order the batch-3 factory wrote), the AI's appended each turn, a trace
            // line per turn in the v33 form.
            // M6 Track EX (2026-10-22): batch 3 played without the discovery layer (v35), as Resume replays it.
            SimConfig c = TestFog.PreDiscovery;
            // D-058 (2026-10-19) CAUSE BLOCK: the batch-3 rule had no in-step continuation either (ContinueWithinTurn false).
            SimConfig batch3 = c with { Construction = c.Construction! with { Reservations = false, ContinueWithinTurn = false } };
            WorldState w = UiFounding.Found(42, 256, 4, 1);
            var recorded = new OrderLog();
            TurnExecutor exec = Production(batch3, recorded);
            string V33Line(WorldState at)
            {
                string[] cols = SessionTrace.Line(at, c.Goods!.GrainId).Split(',');
                cols[^1] = WorldHash.ComputeHexAsSchema(at, 33);
                return string.Join(',', cols);
            }
            var traceLines = new List<string> { SessionTrace.Header, V33Line(w) };
            var worlds = new List<WorldState> { w };
            for (int t = 0; t < 8; t++)
            {
                if (t == 0)
                {
                    recorded.Append(ConstructionQuery.EnqueueOrder(w, Me, S0, Granary));
                    recorded.Append(ConstructionQuery.EnqueueOrder(w, Me, S0, Granary));
                }
                if (t == 1) recorded.Append(ConstructionQuery.EnqueueOrder(w, Me, S2, Granary));
                if (t == 2) foreach (OrderRecord o in SectorOrderFactory.Create(w.Clock.Turn, Me, S0, NoBuilders)) recorded.Append(o);
                if (t == 5) recorded.Append(ConstructionQuery.EnqueueOrder(w, Me, S2, Granary));
                if (t == 6)
                {
                    OrderRecord cancel = legacy
                        ? new OrderRecord(6, Me.Value, OrderKind.CancelConstruction, S2.Value, 0.0)
                        : ConstructionQuery.CancelOrder(w, Me, S2, 0);
                    Assert.Equal(legacy ? OrderKind.CancelConstruction : OrderKind.CancelConstructionEntry, cancel.Kind);
                    Assert.True(ConstructionQuery.CancelOrderValid(w, cancel));
                    recorded.Append(cancel);
                }
                AiOrders.Append(recorded, w, batch3, UiSession.ProductionWorldgen());
                w = exec.Step(w);
                worlds.Add(w);
                traceLines.Add(V33Line(w));
            }
            // As played (batch 3): at 6 the second granary (key 5) is slot 0 at 500,000; the cancel removes it.
            Assert.Equal(1, ConstructionQuery.Built(worlds[6], S2, Granary));
            Assert.Equal(new[] { (0, 5L, 500_000L) }, Entries(worlds[6], S2));
            Assert.Equal((1L, 0), (ConstructionQuery.Built(worlds[7], S2, Granary), Entries(worlds[7], S2).Length));

            // The session files, as the batch-3 build wrote them (schema v33).
            using (FileStream o = File.Create(log)) recorded.Save(o);
            File.WriteAllText(UiSession.TracePath(log), string.Join("\n", traceLines) + "\n");
            SessionManifest m = UiSession.StartWith(TestFog.PreDiscovery, 42, sizeOverridePx: 256, settlementsOverride: 4, aiEmpiresOverride: 1).Manifest(StartedAt, log);
            using (FileStream o = File.Create(UiSession.ManifestPath(log))) (m with { SchemaVersion = 33, BuildSha = "b3-final" }).Write(o);

            // This build's replay up to the cancel's batch: s2's FIRST granary still in slot 0, the turn-5 one in slot 1.
            var upToTheCancel = new OrderLog();
            for (int i = 0; i < recorded.Count; i++) if (recorded[i].Turn < 6) upToTheCancel.Append(recorded[i]);
            UiSession at6 = UiSession.StartWith(TestFog.PreDiscovery, 42, sizeOverridePx: 256, settlementsOverride: 4, aiEmpiresOverride: 1);
            at6.ReplayTo(upToTheCancel, 6);
            Assert.Equal(new[] { (0, 1L, 500_000L), (1, 5L, 0L) }, Entries(at6.World, S2));
            Assert.Equal(0, ConstructionQuery.Built(at6.World, S2, Granary));

            if (legacy)
            {
                string[] files = Directory.GetFiles(dir).Order(StringComparer.Ordinal).ToArray();
                byte[][] before = files.Select(File.ReadAllBytes).ToArray();
                var e = Assert.Throws<OrderValidationException>(() => UiSession.Resume(UiSession.ManifestPath(log), out _));
                Assert.Contains("the orders delivered on turn 6 include 1 legacy slot-only cancel order", e.Message);
                Assert.Contains("does not reproduce that world at turn 6", e.Message);
                Assert.Contains("open it with the build that recorded it (build b3-final, ", e.Message);

                // The launch: one report, exit 2, the dialog once — and every file as it was, no copy made.
                var stderr = new StringWriter();
                var shown = new List<string>();
                Assert.Null(ResumeLaunch.Attempt(UiSession.ManifestPath(log), stderr, (_, text) => shown.Add(text), out _, out int code));
                Assert.Equal(ResumeLaunch.OrderLogRejectedExitCode, code);
                Assert.StartsWith("error: saved session " + Path.GetFileName(UiSession.ManifestPath(log)) + ": the orders delivered on turn 6", stderr.ToString());
                Assert.Contains("The session files were not changed.", Assert.Single(shown));
                Assert.Equal(files, Directory.GetFiles(dir).Order(StringComparer.Ordinal).ToArray());
                for (int i = 0; i < files.Length; i++) Assert.Equal(before[i], File.ReadAllBytes(files[i]));
                return;
            }

            UiSession resumed = UiSession.Resume(UiSession.ManifestPath(log), out _, TestFog.PreDiscovery);
            Assert.Equal(UiSession.SchemaMismatchNotice(33, CanonicalSchema.Version), resumed.ResumeNotice);
            Assert.Equal(8, resumed.World.Clock.Turn);
            UiSession at7 = UiSession.StartWith(TestFog.PreDiscovery, 42, sizeOverridePx: 256, settlementsOverride: 4, aiEmpiresOverride: 1);
            at7.ReplayTo(recorded, 7);
            (int Slot, long Key, long P)[] after = Entries(at7.World, S2);
            Assert.DoesNotContain(after, e => e.Key == 5);                                          // the cancelled entry is gone
            Assert.Equal(new[] { (0, 1L) }, after.Select(e => (e.Slot, e.Key)).ToArray());         // the first granary stays
            Assert.True(after[0].P > 500_000L, "the first granary goes on being built");
        }
        finally
        {
            Directory.Delete(dir, recursive: true);
        }
    }
}
