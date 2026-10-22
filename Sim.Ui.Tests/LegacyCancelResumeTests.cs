using Sim.Core;
using Sim.Core.Kernel;
using Sim.Core.State;
using Sim.Core.Systems.Research;
using Xunit;

namespace Sim.Ui.Tests;

/// <summary>
/// D-056 (2026-10-18; the Director's mandate "Fix the game", §4) — THE LEGACY-CANCEL GUARD ON THE UI RESUME
/// (<see cref="UiSession.Resume"/> → <see cref="UiSession.ReplayTo"/> → ReplayGuard). The shifted-slot case itself — a
/// recorded kind 11 refused, the same game recorded with kind 13 applied to the right entry — is
/// ConstructionLifecycleResumeTests' ARecordedCancel_WhoseSlotShiftsInTheReplay_…; the CLI paths are Sim.Tests'
/// LegacyCancelGuardTests. Pinned here, on the dev-size one-AI world (seed 42, 256 px, four settlements; the player holds
/// s0 and s2): the emitter issues kind 13; COMPATIBLE historical sessions with kind 11 keep resuming (this schema's,
/// checked every turn; another schema's, its trace in the v33 form); a session with kind 11 and no trace is refused, one
/// without kind 11 and no trace resumes with the missing-trace notice; the RE-QUEUED batch (the orders stamped with the
/// last played turn) is checked too; and a resume the trace did not check turn by turn keeps the recorded files aside
/// before the first save overwrites them.
/// </summary>
public class LegacyCancelResumeTests
{
    private const int Granary = 1;
    private static readonly PolityId Me = new(1);
    private static readonly SettlementId S0 = new(0), S2 = new(2);
    private const string StartedAt = "2026-10-18 12:00:00";

    private static string TempDir(out string log)
    {
        string dir = Path.Combine(Path.GetTempPath(), "d056-resume-" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(dir);
        log = Path.Combine(dir, "orders-20261018-120000.bin");
        return dir;
    }

    private static UiSession Start() => UiSession.Start(42, sizeOverridePx: 256, settlementsOverride: 4, aiEmpiresOverride: 1);

    /// <summary>The default sector mix with no construction share: s0 builds nothing, so its two granaries wait at p = 0.</summary>
    private static readonly int[] NoBuilders = [55, 15, 10, 12, 0];

    /// <summary>A session played to turn <paramref name="turns"/> by this build: two granaries at s0 (its builders taken away
    /// in the same batch, so both wait) and one at s2 ordered at turn 0, and at turn <paramref name="cancelAt"/> a cancel of
    /// s0's slot 1 — the legacy kind 11 (appended as an older build wrote it) or, <paramref name="legacy"/> false, the
    /// emitter's kind 13; a cancel stamped with the last turn stays pending. Saved: the log, the trace, the manifest
    /// (written at launch, as the game does). <paramref name="worlds"/>: the world at every turn, as played.
    /// M6 Track EX (2026-10-22): <paramref name="older"/> — a session to be relabelled under an OLDER schema is played with the
    /// discovery layer off, as every pre-v35 build played it (a pre-v35 session is replayed without it,
    /// DiscoveryQuery.RecordedWithoutDiscovery).</summary>
    private static UiSession Played(string log, bool legacy, out List<WorldState> worlds, long cancelAt = 2, int turns = 4, bool older = false)
    {
        UiSession live = older ? UiSession.StartWith(TestFog.PreDiscovery, 42, sizeOverridePx: 256, settlementsOverride: 4, aiEmpiresOverride: 1) : Start();
        live.ExportManifest(StartedAt, log);
        Assert.True(live.EmitConstructionOrder(S0.Value, Granary));
        Assert.True(live.EmitConstructionOrder(S0.Value, Granary));
        Assert.True(live.EmitConstructionOrder(S2.Value, Granary));
        Assert.True(live.EmitSectorOrders(NoBuilders, S0.Value));
        worlds = [live.World];
        for (int t = 0; t <= turns; t++)
        {
            if (t == cancelAt)
            {
                Assert.Equal(2, ConstructionQuery.Queue(live.World, S0).Length);
                if (legacy) live.Orders.Append(new OrderRecord(t, Me.Value, OrderKind.CancelConstruction, S0.Value, 1.0));
                else Assert.True(live.EmitCancelConstruction(S0.Value, 1));
            }
            if (t == turns) break;
            live.EndTurn();
            worlds.Add(live.World);
        }
        live.Save(log);
        live.ExportTrace(UiSession.TracePath(log));
        return live;
    }

    private static UiSession Played(string log, bool legacy, long cancelAt = 2, int turns = 4, bool older = false) =>
        Played(log, legacy, out _, cancelAt, turns, older);

    /// <summary>Relabels the session <paramref name="log"/> names as another schema's, its trace hashes rewritten into
    /// that schema's form by <paramref name="form"/> (null: left as they are).</summary>
    private static void Relabel(string log, int schema, Func<string, int, string>? form = null)
    {
        string manifestPath = UiSession.ManifestPath(log);
        SessionManifest m;
        using (FileStream f = File.OpenRead(manifestPath)) m = SessionManifest.Read(f, manifestPath);
        using (FileStream f = File.Create(manifestPath)) (m with { SchemaVersion = schema }).Write(f);
        if (form is null) return;
        string[] lines = File.ReadAllLines(UiSession.TracePath(log));
        for (int i = 1; i < lines.Length; i++)
        {
            string[] cols = lines[i].Split(',');
            cols[^1] = form(cols[^1], i - 1);
            lines[i] = string.Join(',', cols);
        }
        File.WriteAllText(UiSession.TracePath(log), string.Join("\n", lines) + "\n");
    }

    private static (string[] Files, byte[][] Bytes) Snapshot(string dir)
    {
        string[] files = Directory.GetFiles(dir).Order(StringComparer.Ordinal).ToArray();
        return (files, files.Select(File.ReadAllBytes).ToArray());
    }

    private static void AssertUnchanged(string dir, (string[] Files, byte[][] Bytes) before)
    {
        Assert.Equal(before.Files, Directory.GetFiles(dir).Order(StringComparer.Ordinal).ToArray());
        for (int i = 0; i < before.Files.Length; i++) Assert.Equal(before.Bytes[i], File.ReadAllBytes(before.Files[i]));
    }

    // ================================================================== the emitter

    [Fact]
    public void TheCancelEmitter_IssuesKind13_CarryingTheEntrysIdentity()
    {
        UiSession live = Start();
        Assert.True(live.EmitConstructionOrder(S0.Value, Granary));
        Assert.True(live.EmitConstructionOrder(S0.Value, Granary));
        Assert.True(live.EmitSectorOrders(NoBuilders, S0.Value));                                // both wait at p = 0
        live.EndTurn();
        live.EndTurn();
        Assert.Equal(new[] { 0, 1 }, ConstructionQuery.Queue(live.World, S0).Select(r => r.Slot).ToArray());
        Assert.True(live.EmitCancelConstruction(S0.Value, 1));
        OrderRecord cancel = live.QueuedOrders()[^1];
        Assert.Equal((OrderKind.CancelConstructionEntry, S0.Value, 2L), (cancel.Kind, cancel.TargetId, cancel.Turn));
        Assert.True(OrderLog.TryUnpackCancelTarget(cancel.Amount, out CancelTarget t));
        Assert.Equal(new CancelTarget(1, Granary, 0), t);                                         // slot 1, the granary of turn 0
        Assert.False(live.EmitCancelConstruction(S0.Value, 1));                                     // pending: not twice
        live.EndTurn();
        Assert.Equal(new[] { 0 }, ConstructionQuery.Queue(live.World, S0).Select(r => r.Slot).ToArray());
    }

    // ================================================================== compatible historical sessions keep resuming

    [Fact]
    public void AKind11Session_OfThisSchema_TheTraceCheckedEveryTurn_Resumes_ToTheRecordedWorld()
    {
        string dir = TempDir(out string log);
        try
        {
            UiSession live = Played(log, legacy: true);
            Assert.Single(ConstructionQuery.Queue(live.World, S0));                                // the cancel was applied as played
            UiSession resumed = UiSession.Resume(UiSession.ManifestPath(log), out _);
            Assert.Empty(resumed.ResumeNotice);
            Assert.Equal(WorldHash.ComputeHex(live.World), WorldHash.ComputeHex(resumed.World));
        }
        finally
        {
            Directory.Delete(dir, recursive: true);
        }
    }

    [Fact]
    public void AKind11Session_OfAnotherSchema_WhoseTraceMatchesInTheV33Form_Resumes_ToTheRecordedWorld()
    {
        string dir = TempDir(out string log);
        try
        {
            UiSession live = Played(log, legacy: true, out List<WorldState> worlds, older: true);
            // The trace as a v33 build hashed the same tables: the played worlds, in the v33 form.
            Relabel(log, 33, (_, turn) => WorldHash.ComputeHexAsSchema(worlds[turn], 33));
            UiSession resumed = UiSession.Resume(UiSession.ManifestPath(log), out _);
            Assert.Equal(UiSession.SchemaMismatchNotice(33, CanonicalSchema.Version), resumed.ResumeNotice);
            Assert.Equal(WorldHash.ComputeHex(live.World), WorldHash.ComputeHex(resumed.World));
            Assert.Single(ConstructionQuery.Queue(resumed.World, S0));
        }
        finally
        {
            Directory.Delete(dir, recursive: true);
        }
    }

    // ================================================================== refusals, before any write

    /// <summary>
    /// A SESSION WITHOUT ITS TRACE. D-056 refused one carrying a kind-11 cancel and resumed the rest with the missing-trace
    /// notice. D-057 (the identity proof) is stricter, by design: with no trace nothing shows that any settlement id names the
    /// recorded settlement — not even the founding's — so a session whose log names a settlement is refused at the first
    /// batch that does (here turn 0's granaries, kind 11 or not), with a remedy naming the missing file; only a log that names
    /// no settlement (a research order alone, no AI) resumes, with the notice. Every arm only reads.
    /// </summary>
    [Fact]
    public void ASessionWithNoTrace_IsRefusedAtItsFirstSettlementOrder_AndOneNamingNoSettlementResumesWithTheMissingTraceNotice()
    {
        foreach (bool legacy in new[] { true, false })
        {
            string dir = TempDir(out string log);
            try
            {
                Played(log, legacy);
                File.Delete(UiSession.TracePath(log));
                var before = Snapshot(dir);
                var e = Assert.Throws<OrderValidationException>(() => UiSession.Resume(UiSession.ManifestPath(log), out _));
                Assert.Contains("the orders delivered on turn 0 include EnqueueConstruction (issued by polity 1) for settlement 0", e.Message);
                Assert.Contains("The session has no trace to show that with", e.Message);
                Assert.Contains("put its trace file (" + Path.GetFileName(UiSession.TracePath(log)) + ") back beside the manifest", e.Message);
                AssertUnchanged(dir, before);                                                        // Resume only reads
            }
            finally
            {
                Directory.Delete(dir, recursive: true);
            }
        }

        string only = TempDir(out string researchLog);
        try
        {
            UiSession live = UiSession.Start(42, sizeOverridePx: 256, settlementsOverride: 4, aiEmpiresOverride: 0);
            live.ExportManifest(StartedAt, researchLog);
            Assert.True(ResearchQuery.CheapestAvailable(live.World, live.Config.Research!, Me) is { } node && live.EmitResearchOrder(node));
            for (int t = 0; t < 3; t++) live.EndTurn();
            live.Save(researchLog);
            var before = Snapshot(only);
            // With no trace the replay runs to the last turn the log names (UiSession.Resume's target rule).
            UiSession resumed = UiSession.Resume(UiSession.ManifestPath(researchLog), out _);
            Assert.Equal(UiSession.MissingTraceNotice, resumed.ResumeNotice);
            UiSession replay = UiSession.Start(42, sizeOverridePx: 256, settlementsOverride: 4, aiEmpiresOverride: 0);
            replay.ReplayTo(live.Orders, resumed.World.Clock.Turn);
            Assert.Equal(WorldHash.ComputeHex(replay.World), WorldHash.ComputeHex(resumed.World));
            AssertUnchanged(only, before);
        }
        finally
        {
            Directory.Delete(only, recursive: true);
        }
    }

    [Theory]
    [InlineData(true)]
    [InlineData(false)]
    public void TheReQueuedBatch_IsCheckedToo_AKind11StampedWithTheLastTurn_IsRefusedWhenThatTurnIsNotTheRecordedWorld(bool diverged)
    {
        string dir = TempDir(out string log);
        try
        {
            // The cancel is stamped with the last played turn (4): it is re-queued, not replayed. The session is labelled
            // v33 (no every-turn check); in the diverged arm its last trace row is another world's, so only the re-queued
            // batch's check can refuse it.
            Played(log, legacy: true, cancelAt: 4, turns: 4, older: true);
            OrderLog saved;
            using (FileStream f = File.OpenRead(log)) saved = OrderLog.Load(f);
            Assert.Equal((4L, OrderKind.CancelConstruction), (saved[saved.Count - 1].Turn, saved[saved.Count - 1].Kind));
            Relabel(log, 33, (hash, turn) => diverged && turn == 4 ? new string('0', 64) : hash);
            var before = Snapshot(dir);
            if (diverged)
            {
                var e = Assert.Throws<OrderValidationException>(() => UiSession.Resume(UiSession.ManifestPath(log), out _));
                Assert.Contains("the orders delivered on turn 4 include 1 legacy slot-only cancel order", e.Message);
                AssertUnchanged(dir, before);
                return;
            }
            UiSession resumed = UiSession.Resume(UiSession.ManifestPath(log), out _);
            Assert.Equal(4L, resumed.World.Clock.Turn);
            Assert.Equal(OrderKind.CancelConstruction, resumed.QueuedOrders()[^1].Kind);           // re-queued for End Turn
            resumed.EndTurn();
            Assert.Single(ConstructionQuery.Queue(resumed.World, S0));
        }
        finally
        {
            Directory.Delete(dir, recursive: true);
        }
    }

    // ================================================================== the recorded files, kept before the first overwrite

    [Fact]
    public void AnUncheckedResume_KeepsTheRecordedFilesAside_AtTheFirstSave_AndNeverReplacesTheCopies()
    {
        string dir = TempDir(out string log);
        try
        {
            Played(log, legacy: false, older: true);
            Relabel(log, 33);                                                  // another schema: its trace is not checked
            byte[] orders = File.ReadAllBytes(log), trace = File.ReadAllBytes(UiSession.TracePath(log));
            UiSession resumed = UiSession.Resume(UiSession.ManifestPath(log), out string sessionLog);
            Assert.Equal(log, sessionLog);
            Assert.False(File.Exists(UiSession.RecordedCopyPath(log)), "a resume only reads");
            resumed.EndTurn();
            resumed.Save(log);
            resumed.ExportTrace(UiSession.TracePath(log));
            Assert.Equal(Path.Combine(dir, "orders-20261018-120000.as-recorded.bin"), UiSession.RecordedCopyPath(log));
            Assert.Equal(orders, File.ReadAllBytes(UiSession.RecordedCopyPath(log)));
            Assert.Equal(trace, File.ReadAllBytes(UiSession.RecordedCopyPath(UiSession.TracePath(log))));
            Assert.NotEqual(trace, File.ReadAllBytes(UiSession.TracePath(log)));   // the session itself moved on

            resumed.EndTurn();
            resumed.Save(log);                                                 // a later save keeps the first copies
            Assert.Equal(orders, File.ReadAllBytes(UiSession.RecordedCopyPath(log)));
            UiSession again = UiSession.Resume(UiSession.ManifestPath(log), out _);
            again.Save(log);
            Assert.Equal(orders, File.ReadAllBytes(UiSession.RecordedCopyPath(log)));   // and so does another resume's
        }
        finally
        {
            Directory.Delete(dir, recursive: true);
        }
    }

    [Fact]
    public void ACheckedResume_MakesNoCopy()
    {
        string dir = TempDir(out string log);
        try
        {
            Played(log, legacy: false);
            UiSession resumed = UiSession.Resume(UiSession.ManifestPath(log), out _);
            resumed.EndTurn();
            resumed.Save(log);
            Assert.Empty(Directory.GetFiles(dir, "*.as-recorded.*"));
        }
        finally
        {
            Directory.Delete(dir, recursive: true);
        }
    }
}
