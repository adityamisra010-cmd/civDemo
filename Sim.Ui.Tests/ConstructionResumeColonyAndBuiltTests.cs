using Sim.Core;
using Sim.Core.Kernel;
using Sim.Core.State;
using Sim.Core.Systems.Colonization;
using Sim.Core.Worldgen;
using Sim.Ui.ViewModel;
using Xunit;

namespace Sim.Ui.Tests;

/// <summary>
/// D-055 (2026-10-16; the Director's batch-4 instruction, item 4 — "… cancellation and re-enqueueing across … resumed UI
/// sessions and deterministic replay"; batch-4 design §4 P7; recon persistence-audit §7 P7) — TWO GAPS THE AUDIT LEFT:
///  (1) A cancel (kind 11 until D-056, now kind 13) NAMING A COLONY — an id the turn-0 world does not hold, whose checks ADR-034 DEFERS to
///      delivery — across a UI resume and a fresh replay: re-queued by the resume, validated against the world it is
///      delivered to, and removing the colony's entry turn-exact on both sessions; and the other side of ADR-034 — a v33
///      session (the trace not checked) whose replay never founds the colony a later order names is REFUSED at resume with
///      the delivery-time diagnostic (the D-055 / m5-playtest-known-issues limitation for v33 sessions, now pinned);
///  (2) EXACTLY ONCE: a construction completed after a resume, and one completed before the save, is counted once
///      (Structures 1) in the resumed session and stays 1 — ICU:347 only ever asserted the live and resumed counts EQUAL.
/// The colony literals are the determinism-xproc Found Settlement leg's (FoundSettlementResumeTests): turn 2, from
/// settlement 0 to cell 349391 on UiSession.StartWith(TestFog.PreDiscovery, 42) — it founds settlement 12, above the turn-0 maximum (11), in state 3.
/// MEASURED (2026-10-16, by the agent writing this line; the same orders through `sim run`, t4-out/probe-colony.txt): the
/// colony's first granary is built in the step that enqueues it (the Empire purse is rich), so its SECOND granary — slot 1
/// — is the entry that waits at p = 0 in world 4.
/// </summary>
public class ConstructionResumeColonyAndBuiltTests
{
    private const int Granary = 1;
    private const int Granaries = 7;                                               // D-058: the colony's order (was 2)
    private static readonly ConstructionQueueRow Cancelled = new(new SettlementId(Colony), Granaries - 1, Granary);
    private const int FoundTurn = 2, FoundSource = 0, FoundSite = 349391, Colony = 12;
    private const string StartedAt = "2026-10-16 12:00:00";
    private static readonly SettlementId S0 = new(0), S2 = new(2), ColonyId = new(Colony);
    private static readonly int[] NoBuilders = [55, 15, 10, 12, 0];

    private static string TempDir(out string log)
    {
        string dir = Path.Combine(Path.GetTempPath(), "d055-colony-built-"
            + Environment.ProcessId.ToString(System.Globalization.CultureInfo.InvariantCulture) + "-" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(dir);
        log = Path.Combine(dir, "orders-20261016-130000.bin");
        return dir;
    }

    private static UiSession SaveAndResume(UiSession live, string log)
    {
        live.Save(log);
        live.ExportTrace(UiSession.TracePath(log));
        return UiSession.Resume(UiSession.ManifestPath(log), out _, TestFog.PreDiscovery);
    }

    private static OrderLog LoadLog(string path)
    {
        using FileStream f = File.OpenRead(path);
        return OrderLog.Load(f);
    }

    private static int MaxSettlementId(IReadOnlyWorldState w)
    {
        int max = -1;
        for (int i = 0; i < w.Settlements.Count; i++) max = Math.Max(max, w.Settlements[i].Id.Value);
        return max;
    }

    /// <summary>The colony session at turn 4: colony 12 founded (state 3), two granaries ordered there at turn 3 (the first
    /// built in that step, the second — slot 1 — waiting at p = 0), and the cancel of slot 1 issued at turn 4 (pending).
    /// D-058 (2026-10-19): seven granaries now — five built in step 3 → 4, slot 5 at 200,000, slot 6 at p = 0 — and the cancel
    /// names slot 6; after it the colony has six granaries and no entry (the method's cause block).</summary>
    private static UiSession ColonySession(string log)
    {
        UiSession live = UiSession.StartWith(TestFog.PreDiscovery, 42);
        live.ExportManifest(StartedAt, log);
        Assert.Equal(Colony - 1, MaxSettlementId(live.World));
        for (int t = 0; t < FoundTurn; t++) live.EndTurn();
        SettlementSiting.FrontierSiting frontier = FoundingQuery.Frontier(live.World, live.Config, UiSession.ProductionWorldgen())!;
        Assert.Equal(FoundingVerdict.Legal, FoundingQuery.Check(live.World, live.Config, frontier, UiPlayer.Empire, S0, FoundSite));
        live.Orders.Append(FoundingQuery.FoundOrder(live.World, UiPlayer.Empire, S0, FoundSite));
        live.EndTurn();                                                                                   // 3
        Assert.True(EmpireQuery.ControlsSettlement(live.World, UiPlayer.Empire, ColonyId), "the colony stands at 3");
        // D-058 (2026-10-19) CAUSE BLOCK — was two granaries, world 4 holding slot 1 at p = 0 behind the one built, and the
        // cancel of slot 1. Under the Director's "build one at a time … 7 built, 1 under construction, 1 not started" ruling
        // the colony builds about 5.2 granaries' worth in step 3 → 4 (MEASURED, the CLI's identical founding:
        // CliConstructionLifecycleTests), so both were built and world 4 held nothing to cancel. Seven give the Director's
        // shape: five built, slot 5 part-built at 200,000, slot 6 not started — and slot 6 is cancelled.
        for (int i = 0; i < Granaries; i++) Assert.True(live.EmitConstructionOrder(Colony, Granary));
        live.EndTurn();                                                                                   // 4
        Assert.Equal([new ConstructionQueueRow(ColonyId, Granaries - 2, Granary), Cancelled], ConstructionQuery.Queue(live.World, ColonyId));
        Assert.Equal(200_000L, ConstructionQuery.ProgressOf(live.World, new ConstructionQueueRow(ColonyId, Granaries - 2, Granary)));
        Assert.Equal(5L, ConstructionQuery.Built(live.World, ColonyId, Granary));
        Assert.True(live.EmitCancelConstruction(Colony, Granaries - 1));
        return live;
    }

    /// <summary>
    /// (1) A KIND 11 NAMING A DEFERRED COLONY ID, PENDING AT THE SAVE. Saved at turn 4 with the cancel of the colony's slot 1
    /// issued; the resume (same schema, the trace checked) re-queues it — its TargetId 12 is above the turn-0 maximum, so
    /// the up-front check deferred it and the re-queued batch is validated against world 4, where the colony stands — and the
    /// first End Turn of BOTH sessions removes the entry: world 5 holds no colony entry and no row, and the colony has
    /// exactly its one granary. A FRESH replay of the saved log to turn 4 (`ReplayTo`) re-queues the same cancel, and to
    /// turn 5 reproduces the live world.
    /// </summary>
    [Fact]
    public void AKind11NamingADeferredColonyId_PendingAtTheSave_IsReQueuedValidatedAtDelivery_AndRemovesTheEntryTurnExact()
    {
        string dir = TempDir(out string log);
        try
        {
            UiSession live = ColonySession(log);
            // D-056: the emitter's cancel is kind 13, carrying the colony entry's identity (slot 1, the granary, its order turn).
            OrderRecord cancel = ConstructionQuery.CancelOrder(live.World, UiPlayer.Empire, ColonyId, Granaries - 1);
            Assert.Equal((4L, OrderKind.CancelConstructionEntry, Colony), (cancel.Turn, cancel.Kind, cancel.TargetId));
            Assert.Contains(cancel, live.QueuedOrders());
            UiSession resumed = SaveAndResume(live, log);
            Assert.Empty(resumed.ResumeNotice);
            Assert.Equal(live.QueuedOrders().ToArray(), resumed.QueuedOrders().ToArray());
            Assert.Contains(cancel, resumed.QueuedOrders());
            Assert.Equal(WorldHash.ComputeHex(live.World), WorldHash.ComputeHex(resumed.World));

            UiSession fresh4 = UiSession.StartWith(TestFog.PreDiscovery, 42);
            fresh4.ReplayTo(LoadLog(log), 4);
            Assert.Contains(cancel, fresh4.QueuedOrders());

            live.EndTurn();
            resumed.EndTurn();
            fresh4.EndTurn();
            foreach (UiSession s in new[] { live, resumed, fresh4 })
            {
                Assert.Empty(ConstructionQuery.Queue(s.World, ColonyId));
                Assert.Equal(-1, ConstructionQuery.ProgressIndex(s.World.ConstructionProgress, Cancelled));
                Assert.Equal(6L, ConstructionQuery.Built(s.World, ColonyId, Granary));   // D-058: the part-built one completed (was 1)
                Assert.Equal(WorldHash.ComputeHex(live.World), WorldHash.ComputeHex(s.World));
            }
            live.Save(log);
            UiSession fresh5 = UiSession.StartWith(TestFog.PreDiscovery, 42);
            fresh5.ReplayTo(LoadLog(log), 5);
            Assert.Equal(WorldHash.ComputeHex(live.World), WorldHash.ComputeHex(fresh5.World));
        }
        finally
        {
            Directory.Delete(dir, recursive: true);
        }
    }

    /// <summary>
    /// (1), THE OTHER SIDE OF ADR-034 — A v33 SESSION WHOSE REPLAY NEVER FOUNDS THE COLONY A LATER ORDER NAMES IS REFUSED. The
    /// colony session, saved at 4 and relabelled schema v33 (so it takes the D-008 path: replayed under this build's rules,
    /// the trace NOT checked — what every batch-3 session is under v34). With its log intact it resumes (the four-line v33
    /// notice) and the cancel still works. With the Found Settlement order removed from the log — the case of a batch-4
    /// replay in which the colony is never founded — the resume is REFUSED by the delivery-time check (OrderValidationException,
    /// ADR-034): the first order naming settlement 12, the turn-3 enqueue, is delivered to a world that has no settlement 12.
    /// This is the D-055 limitation "a v33 session can be refused under ADR-034" (design X1), pinned.
    /// </summary>
    [Fact]
    public void AV33SessionWhoseReplayNeverFoundsTheColony_ALaterOrderNames_IsRefusedAtResume_ADR034()
    {
        string dir = TempDir(out string log);
        try
        {
            UiSession live = ColonySession(log);
            live.Save(log);
            live.ExportTrace(UiSession.TracePath(log));
            string manifest = UiSession.ManifestPath(log);
            ChronicleAndResumeTests.RecordSchema(manifest, 33);

            UiSession intact = UiSession.Resume(manifest, out _, TestFog.PreDiscovery);
            Assert.Equal(UiSession.SchemaMismatchNotice(33, CanonicalSchema.Version), intact.ResumeNotice);
            Assert.Equal(WorldHash.ComputeHex(live.World), WorldHash.ComputeHex(intact.World));
            intact.EndTurn();
            Assert.Empty(ConstructionQuery.Queue(intact.World, ColonyId));

            OrderLog saved = LoadLog(log), forged = new();
            for (int i = 0; i < saved.Count; i++) if (saved[i].Kind != OrderKind.FoundSettlement || saved[i].ActorId != UiPlayer.Empire.Value) forged.Append(saved[i]);
            Assert.Equal(saved.Count - 1, forged.Count);
            using (FileStream f = File.Create(log)) forged.Save(f);
            OrderValidationException refused = Assert.Throws<OrderValidationException>(() => UiSession.Resume(manifest, out _, TestFog.PreDiscovery));
            Assert.Equal("order (turn 3): EnqueueConstruction targets settlement 12, which does not exist when the order is delivered "
                + "(turn 3, 12 settlement(s)). Its check was deferred because a colony could have been founded with that id by then; "
                + "none was (ADR-034).", refused.Message);
        }
        finally
        {
            Directory.Delete(dir, recursive: true);
        }
    }

    /// <summary>The Q17 rig at turn 2 (ConstructionLifecycleResumeTests' recipe through the emitters): s0's granary waits
    /// part-built without builders and sets aside its stone; s2's, younger, is built in step 4 → 5.</summary>
    private static UiSession Q17(string log)
    {
        UiSession live = UiSession.StartWith(TestFog.PreDiscovery, 42, sizeOverridePx: 256, settlementsOverride: 4, aiEmpiresOverride: 1);
        live.ExportManifest(StartedAt, log);
        Assert.True(live.EmitAllocationOrder(30));
        Assert.True(live.EmitConstructionOrder(S0.Value, Granary));
        Assert.True(live.EmitConstructionOrder(S2.Value, Granary));
        live.EndTurn();
        Assert.True(live.EmitSectorOrders(NoBuilders, S0.Value));
        live.EndTurn();
        return live;
    }

    /// <summary>
    /// (2) EXACTLY ONCE ACROSS A RESUME. The Q17 session's s2 granary is built in step 4 → 5. Saved at 4 (before) and resumed:
    /// the first End Turn completes it in both sessions — Structures for (s2, granary) is exactly 1, its queue entry and row
    /// are gone — and three more End Turns leave it 1. Saved at 5 (after) and resumed: the replay counts it once (1, not 2),
    /// and two more End Turns leave it 1. s0's granary, waiting part-built throughout, is never counted.
    /// </summary>
    [Fact]
    public void ACompletionAfterTheResume_AndOneBeforeTheSave_AreEachCountedExactlyOnce()
    {
        string dir = TempDir(out string log);
        try
        {
            UiSession live = Q17(log);
            live.EndTurn();
            live.EndTurn();                                                                               // 4
            Assert.Equal(0L, ConstructionQuery.Built(live.World, S2, Granary));
            Assert.Single(ConstructionQuery.Queue(live.World, S2));
            UiSession resumed = SaveAndResume(live, log);
            for (int t = 0; t < 4; t++)
            {
                live.EndTurn();
                resumed.EndTurn();
                foreach (UiSession s in new[] { live, resumed })
                {
                    Assert.Equal((1L, 0L), (ConstructionQuery.Built(s.World, S2, Granary), ConstructionQuery.Built(s.World, S0, Granary)));
                    Assert.Empty(ConstructionQuery.Queue(s.World, S2));
                    Assert.Equal(-1, ConstructionQuery.ProgressIndex(s.World.ConstructionProgress, new ConstructionQueueRow(S2, 0, Granary)));
                }
                Assert.Equal(WorldHash.ComputeHex(live.World), WorldHash.ComputeHex(resumed.World));
            }

            string dir2 = TempDir(out string log2);
            try
            {
                UiSession after = Q17(log2);
                for (int t = 0; t < 3; t++) after.EndTurn();                                             // 5
                Assert.Equal(1L, ConstructionQuery.Built(after.World, S2, Granary));
                UiSession back = SaveAndResume(after, log2);
                Assert.Equal(1L, ConstructionQuery.Built(back.World, S2, Granary));
                for (int t = 0; t < 2; t++)
                {
                    back.EndTurn();
                    after.EndTurn();
                    Assert.Equal(1L, ConstructionQuery.Built(back.World, S2, Granary));
                    Assert.Equal(WorldHash.ComputeHex(after.World), WorldHash.ComputeHex(back.World));
                }
            }
            finally
            {
                Directory.Delete(dir2, recursive: true);
            }
        }
        finally
        {
            Directory.Delete(dir, recursive: true);
        }
    }
}
