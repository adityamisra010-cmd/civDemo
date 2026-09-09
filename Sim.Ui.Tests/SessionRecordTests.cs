using Sim.Core.Kernel;
using Xunit;

namespace Sim.Ui.Tests;

/// <summary>
/// T4.17 at the SESSION seam: what a played session actually writes to disk, and
/// the one property that makes it worth writing — that the trace the director's
/// machine produced can be reproduced turn for turn by replaying his order log.
/// </summary>
public class SessionRecordTests
{
    private static Sim.Ui.UiSession Played(int turns, out string logPath, string tag)
    {
        var session = Sim.Ui.UiSession.Start(42, sizeOverridePx: 256, settlementsOverride: 4);
        logPath = Path.Combine(Path.GetTempPath(), $"civsim-{tag}", "orders-20260909-120000-s256-n4.bin");
        for (int t = 0; t < turns; t++) session.EndTurn();
        return session;
    }

    [Fact]
    public void TheManifestRecordsTheSEEDAndOverridesTheWorldWasBuiltFrom()
    {
        // THE DEFECT THIS CLOSES. Before T4.17 the seed lived only in the argv
        // of a process that has since exited, so a session log could not be
        // replayed at all — the world it belonged to was unrecoverable.
        var session = Sim.Ui.UiSession.Start(7, sizeOverridePx: 256, settlementsOverride: 4);
        SessionManifest m = session.Manifest("2026-09-09 12:00:00", "runs/orders-x.bin");

        Assert.Equal(7UL, m.Seed);
        Assert.Equal(256, m.SizePx);
        Assert.Equal(4, m.Settlements);
        Assert.Equal(CanonicalSchema.Version, m.SchemaVersion);
        Assert.Equal("orders-x.bin", m.OrdersFile);
    }

    [Fact]
    public void TheThreeCompanionFilesShareTheOrderLogsStamp()
    {
        // Same stamp, different prefix: the four files of one session sort
        // together and cannot be mixed up with another session's.
        const string log = "runs/orders-20260909-120000-s256-n4.bin";
        Assert.Equal(
            Path.Combine("runs", "chronicle-20260909-120000-s256-n4.txt"),
            Sim.Ui.UiSession.ChroniclePath(log));
        Assert.Equal(
            Path.Combine("runs", "trace-20260909-120000-s256-n4.csv"),
            Sim.Ui.UiSession.TracePath(log));
        Assert.Equal(
            Path.Combine("runs", "session-20260909-120000-s256-n4.json"),
            Sim.Ui.UiSession.ManifestPath(log));
    }

    [Fact]
    public void TheTraceCarriesTheHeaderAndONELinePerTurnIncludingTurnZero()
    {
        Sim.Ui.UiSession session = Played(5, out _, "trace-shape");

        // 1 header + turn 0 (the world before any order) + 5 played turns.
        Assert.Equal(7, session.TraceLines.Count);
        Assert.Equal(SessionTrace.Header, session.TraceLines[0]);

        IReadOnlyList<SessionTrace.Row> rows = SessionTrace.Parse(session.TraceLines, "trace");
        Assert.Equal(6, rows.Count);
        for (int i = 0; i < rows.Count; i++) Assert.Equal(i, rows[i].Turn);
        Assert.Equal(5, session.TurnsPlayed);
    }

    [Fact]
    public void ThePlayedSessionREPRODUCESTurnForTurnFromItsOwnLog()
    {
        // THE PROPERTY THE WHOLE RECORD EXISTS FOR. The trace holds the hashes
        // the playing session computed; rebuilding the world from the manifest
        // and replaying the log must recompute every one of them. If this ever
        // fails, `sim inspect` reports the first disagreeing turn — and a
        // divergence here is a determinism finding, not a reporting one.
        Sim.Ui.UiSession session = Played(12, out _, "reproduce");
        IReadOnlyList<SessionTrace.Row> played = SessionTrace.Parse(session.TraceLines, "trace");

        SessionManifest m = session.Manifest("t", "runs/orders-x.bin");
        var orders = new OrderLog();
        for (int i = 0; i < session.Orders.Count; i++) orders.Append(session.Orders[i]);

        Sim.Core.State.WorldState world = Sim.Ui.UiFounding.Found(m.Seed, m.SizePx, m.Settlements);
        TurnExecutor executor = Sim.Ui.UiSession.BuildProductionExecutor(orders);

        Assert.Equal(played[0].Hash, WorldHash.ComputeHex(world));
        for (int t = 1; t < played.Count; t++)
        {
            world = executor.Step(world);
            Assert.Equal(played[t].Hash, WorldHash.ComputeHex(world));
        }
    }

    [Fact]
    public void ExportWritesTheManifestAndTraceWhereTheOrderLogLives()
    {
        Sim.Ui.UiSession session = Played(3, out string log, "export");
        try
        {
            session.ExportManifest("2026-09-09 12:00:00", log);
            session.Save(log);
            session.ExportTrace(Sim.Ui.UiSession.TracePath(log));

            string manifestPath = Sim.Ui.UiSession.ManifestPath(log);
            Assert.True(File.Exists(manifestPath));
            Assert.True(File.Exists(Sim.Ui.UiSession.TracePath(log)));

            using FileStream file = File.OpenRead(manifestPath);
            SessionManifest read = SessionManifest.Read(file, manifestPath);
            Assert.Equal(42UL, read.Seed);
        }
        finally
        {
            string? dir = Path.GetDirectoryName(log);
            if (dir is not null && Directory.Exists(dir)) Directory.Delete(dir, recursive: true);
        }
    }
}
