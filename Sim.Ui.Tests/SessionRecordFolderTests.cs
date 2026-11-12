using System.Globalization;
using Sim.Core.Observability.Forensic;
using Sim.Ui.Export;
using Xunit;

namespace Sim.Ui.Tests;

/// <summary>
/// Director 2026-11-02 — "Next run onwards, I want only one excel sheet [in the runs folder], not these multiple files."
/// A played session records into <c>runs/.record/</c> (<see cref="UiSession.RecordFolder"/>, hidden on Windows) under the
/// file names of before; only <c>session-STAMP.xlsx</c> is written into <c>runs/</c>. Resume reads the record folder and
/// still reads the older flat layout. The raw telemetry log's removal after a verified export is retried, and a log it
/// could not remove is reported in <c>.record/export-STAMP.log</c> as well as on stderr.
/// </summary>
// D-087 DF1 (Director, 2026-11-12): this class drives ImGui frames through the headless harness, and ImGui's context is
// process-wide, so it joins the "ImGui context" collection and never runs beside another class that draws frames.
[Collection("ImGui context")]
public class SessionRecordFolderTests
{
    private static string TempDir(string name)
    {
        string dir = Path.Combine(Path.GetTempPath(), name + "-" + Environment.ProcessId.ToString(CultureInfo.InvariantCulture));
        if (Directory.Exists(dir)) Directory.Delete(dir, recursive: true);
        return dir;
    }

    private static void Remove(string dir)
    {
        if (!Directory.Exists(dir)) return;
        if (OperatingSystem.IsWindows()) File.SetAttributes(dir, FileAttributes.Directory);
        Directory.Delete(dir, recursive: true);
    }

    private static string[] Names(string[] paths)
    {
        var names = new string[paths.Length];
        for (int i = 0; i < paths.Length; i++) names[i] = Path.GetFileName(paths[i]);
        Array.Sort(names, StringComparer.Ordinal);
        return names;
    }

    private static string? ListedTelemetrySha(string forensicPath)
    {
        foreach (ArtifactRef a in ForensicRecordFile.Read(forensicPath).Close!.Artifacts)
            if (a.Role == "telemetry") return a.Sha256;
        return null;
    }

    /// <summary>The game's own launch path names the log in the record folder, so every twinned companion is there.</summary>
    [Fact]
    public void TheLaunchPath_RecordsIntoRunsDotRecord_UnderTheSameFileNames()
    {
        string log = UiSession.SessionLogPath(new DateTime(2026, 11, 2, 14, 5, 58), aiEmpiresOverride: 1);
        string record = Path.Combine("runs", ".record");
        Assert.Equal(Path.Combine(record, "orders-20261102-140558-a1.bin"), log);
        Assert.Equal(Path.Combine(record, "session-20261102-140558-a1.json"), UiSession.ManifestPath(log));
        Assert.Equal(Path.Combine(record, "trace-20261102-140558-a1.csv"), UiSession.TracePath(log));
        Assert.Equal(Path.Combine(record, "chronicle-20261102-140558-a1.txt"), UiSession.ChroniclePath(log));
        Assert.Equal(Path.Combine(record, "telemetry-20261102-140558-a1.jsonl"), UiSession.TelemetryPath(log));
        Assert.Equal(Path.Combine(record, "forensic-20261102-140558-a1.jsonl"), UiSession.ForensicPath(log));
        Assert.Equal(Path.Combine(record, "export-20261102-140558-a1.log"), UiSession.ExportLogPath(log));
        Assert.Equal(Path.Combine(record, "orders-20261102-140558-a1.as-recorded.bin"), UiSession.RecordedCopyPath(log));
        // ...and the one visible file is in runs/ itself
        Assert.Equal(Path.Combine("runs", "session-20261102-140558-a1.xlsx"), SessionWorkbook.WorkbookPathFor(UiSession.ManifestPath(log)));
    }

    /// <summary>WorkbookPathFor: the parent of a record folder; beside any other manifest (the older flat layout, a copy).</summary>
    [Fact]
    public void TheWorkbookPath_IsTheRecordFoldersParent_OrBesideAnyOtherManifest()
    {
        Assert.Equal(Path.Combine("a", "runs", "session-X.xlsx"),
            SessionWorkbook.WorkbookPathFor(Path.Combine("a", "runs", ".record", "session-X.json")));
        Assert.Equal(Path.Combine("a", "runs", "session-X.xlsx"),
            SessionWorkbook.WorkbookPathFor(Path.Combine("a", "runs", "session-X.json")));
        Assert.Equal("session-X.xlsx", SessionWorkbook.WorkbookPathFor(Path.Combine(".record", "session-X.json")));
        Assert.Equal(Path.Combine("a", "x.record", "session-X.xlsx"),
            SessionWorkbook.WorkbookPathFor(Path.Combine("a", "x.record", "session-X.json")));
        Assert.Equal(Path.Combine("a", ".records", "session-X.xlsx"),
            SessionWorkbook.WorkbookPathFor(Path.Combine("a", ".records", "session-X.json")));
        Assert.True(UiSession.IsRecordFolder(Path.Combine("runs", ".record")));
        Assert.True(UiSession.IsRecordFolder(Path.Combine("runs", ".record") + Path.DirectorySeparatorChar));
        Assert.False(UiSession.IsRecordFolder("runs"));
        Assert.False(UiSession.IsRecordFolder(""));
        Assert.False(UiSession.IsRecordFolder(null));
    }

    /// <summary>
    /// THE DIRECTOR'S INSTRUCTION, end to end through the real GameUi close (the headless harness, SaveOnExit): after the
    /// session closes, runs/ holds exactly one file — session-STAMP.xlsx — and one folder, .record, which holds exactly the
    /// manifest, orders, trace, chronicle and forensic record: no telemetry-*.jsonl (removed after the verified export) and
    /// no export log (nothing went wrong). The workbook's Telemetry sheet rebuilds the removed log to the close record's
    /// sha256, so nothing was lost; on Windows the record folder is Hidden. The headless re-export of <c>runs</c> finds the
    /// session in .record, reads the removed log back from the workbook in runs/ and leaves the layout as it was.
    /// </summary>
    [Fact]
    public void AtSessionClose_RunsHoldsOnlyTheWorkbook_AndTheRecordFolderHoldsNoTelemetry()
    {
        string root = TempDir("civsim-record-close");
        string runs = Path.Combine(root, "runs");
        string record = Path.Combine(runs, UiSession.RecordFolder);
        try
        {
            UiSession session = UiSession.Start(42, sizeOverridePx: 256, settlementsOverride: 4);
            string log;
            using (Sim.Ui.Headless.UiFrameHarness h = Sim.Ui.Headless.UiFrameHarness.Start(session, record, Path.Combine(AppContext.BaseDirectory, "assets")))
            {
                log = h.SessionLogPath;
                session.ExportManifest("2026-11-02 14:05:58", log);
                session.ExportForensicRun("2026-11-02 14:05:58+00:00", log);
                for (int t = 0; t < 3; t++) session.EndTurn();
                h.Ui.SaveOnExit();
            }
            Assert.Equal(Path.Combine(record, "orders-harness.bin"), log);

            Assert.Equal(["session-harness.xlsx"], Names(Directory.GetFiles(runs)));
            Assert.Equal([record], Directory.GetDirectories(runs));
            Assert.Equal(["chronicle-harness.txt", "forensic-harness.jsonl", "orders-harness.bin", "session-harness.json", "trace-harness.csv"],
                Names(Directory.GetFiles(record)));
            Assert.Empty(Directory.GetFiles(record, "telemetry-*.jsonl"));
            Assert.False(File.Exists(UiSession.ExportLogPath(log)));
            if (OperatingSystem.IsWindows())
                Assert.Equal(FileAttributes.Hidden, File.GetAttributes(record) & FileAttributes.Hidden);

            // nothing lost: the removed log is the workbook's Telemetry sheet, to the close record's sha256
            string xlsx = Path.Combine(runs, "session-harness.xlsx");
            OoxmlWorkbook.Content back = OoxmlWorkbook.Read(File.ReadAllBytes(xlsx));
            var lines = new List<string>();
            foreach (string c in SessionWorkbook.JoinTelemetry(back.Sheets[5])) lines.Add(TelemetryCellCodec.Decode(c));
            Assert.Equal(3, lines.Count);
            string? listed = ListedTelemetrySha(UiSession.ForensicPath(log));
            Assert.NotNull(listed);
            Assert.Equal(listed, ForensicIdentity.Sha256Hex(SessionWorkbook.TelemetryBytesOf(lines)));

            // the headless re-export of `runs` finds the session in .record and writes the workbook in runs/ again
            var o = new StringWriter();
            Assert.Equal(0, SessionExportCommand.Run(["--export-session", runs], o, new StringWriter()));
            Assert.Contains("session workbook: " + Path.GetFullPath(xlsx), o.ToString(), StringComparison.Ordinal);
            Assert.Contains("were read back from the existing workbook", o.ToString(), StringComparison.Ordinal);
            Assert.Equal(["session-harness.xlsx"], Names(Directory.GetFiles(runs)));
            Assert.Equal(5, Directory.GetFiles(record).Length);
        }
        finally
        {
            Remove(record);
            if (Directory.Exists(root)) Directory.Delete(root, recursive: true);
        }
    }

    /// <summary>Continue last game (<c>--resume runs</c>): the newest manifest by stamp, whichever layout holds it — the
    /// record folder's or the older flat folder's; a stamp in both resolves to the record folder's; a record folder named
    /// directly is an ordinary directory; a directory with neither is the named "no manifest" refusal.</summary>
    [Fact]
    public void ResolveManifest_FindsTheNewestSession_InTheRecordFolderOrTheFlatLayout()
    {
        string runs = TempDir("civsim-record-resolve");
        string record = Path.Combine(runs, UiSession.RecordFolder);
        try
        {
            Directory.CreateDirectory(record);
            File.WriteAllText(Path.Combine(runs, "session-20261020-090000.json"), "");
            File.WriteAllText(Path.Combine(record, "session-20261101-090000.json"), "");
            File.WriteAllText(Path.Combine(record, "session-20261102-140558-a1.json"), "");
            File.WriteAllText(Path.Combine(record, "session-20261102-140558-a1.xlsx"), "");   // not a manifest
            Assert.Equal(Path.Combine(record, "session-20261102-140558-a1.json"), UiArgs.ResolveManifest(runs));

            File.WriteAllText(Path.Combine(runs, "session-20261103-080000.json"), "");       // a newer flat session
            Assert.Equal(Path.Combine(runs, "session-20261103-080000.json"), UiArgs.ResolveManifest(runs));

            File.WriteAllText(Path.Combine(record, "session-20261103-080000.json"), "");     // the same stamp in both
            Assert.Equal(Path.Combine(record, "session-20261103-080000.json"), UiArgs.ResolveManifest(runs));

            Assert.Equal(Path.Combine(record, "session-20261103-080000.json"), UiArgs.ResolveManifest(record));
            string explicitManifest = Path.Combine(runs, "session-20261020-090000.json");
            Assert.Equal(explicitManifest, UiArgs.ResolveManifest(explicitManifest));

            string empty = Path.Combine(runs, "empty");
            Directory.CreateDirectory(Path.Combine(empty, UiSession.RecordFolder));
            var e = Assert.Throws<FileNotFoundException>(() => UiArgs.ResolveManifest(empty));
            Assert.Equal("no session-*.json manifest in " + empty + " (nor in its .record folder)", e.Message);
            Assert.ThrowsAny<IOException>(() => UiArgs.ResolveManifest(Path.Combine(runs, "missing")));
        }
        finally
        {
            Remove(runs);
        }
    }

    /// <summary>Resume from <c>runs</c> of a REAL session recorded in runs/.record: the manifest resolved there, the replay
    /// checked against the trace turn by turn (no notice), and the session continues into the SAME record files.</summary>
    [Fact]
    public void ResumeFromRuns_FindsTheSessionInTheRecordFolder_AndContinuesItsFiles()
    {
        string runs = TempDir("civsim-record-resume");
        string record = Path.Combine(runs, UiSession.RecordFolder);
        try
        {
            string log = ExportFixtures.PlayToDisk(UiSession.Start(42, sizeOverridePx: 256, settlementsOverride: 4), record,
                "20261102-140558", turns: 2);
            Assert.Equal(Path.Combine(record, "orders-20261102-140558.bin"), log);
            Assert.Empty(Directory.GetFiles(runs));   // nothing of the record outside .record

            string manifest = UiArgs.ResolveManifest(runs);
            Assert.Equal(Path.Combine(record, "session-20261102-140558.json"), manifest);
            UiSession resumed = UiSession.Resume(manifest, out string resumedLog);
            Assert.Equal(Path.GetFullPath(log), resumedLog);
            Assert.Equal(2, resumed.World.Clock.Turn);
            Assert.Empty(resumed.ResumeNotice);
        }
        finally
        {
            Remove(runs);
        }
    }

    /// <summary>An OLDER, FLAT runs folder (every file in runs/ itself, as builds before 2026-11-02 wrote it) still
    /// resumes from <c>runs</c>, and its close-time export keeps the old layout: the workbook beside the manifest, the
    /// raw log removed there, nothing written into a .record folder.</summary>
    [Fact]
    public void AnOlderFlatRunsFolder_StillResumes_AndExportsBesideItsManifest()
    {
        string runs = TempDir("civsim-record-flat");
        try
        {
            UiSession session = UiSession.Start(42, sizeOverridePx: 256, settlementsOverride: 4);
            string log = ExportFixtures.PlayToDisk(session, runs, "20261020-090000", turns: 2);
            string manifest = UiArgs.ResolveManifest(runs);
            Assert.Equal(Path.Combine(runs, "session-20261020-090000.json"), manifest);
            UiSession resumed = UiSession.Resume(manifest, out string resumedLog);
            Assert.Equal(Path.GetFullPath(log), resumedLog);
            Assert.Equal(2, resumed.World.Clock.Turn);
            Assert.Empty(resumed.ResumeNotice);

            var errors = new StringWriter();
            Assert.Equal(Path.Combine(Path.GetFullPath(runs), "session-20261020-090000.xlsx"), session.ExportWorkbook(log, errors));
            Assert.Equal("", errors.ToString());
            Assert.Equal(["chronicle-20261020-090000.txt", "forensic-20261020-090000.jsonl", "orders-20261020-090000.bin",
                "session-20261020-090000.json", "session-20261020-090000.xlsx", "trace-20261020-090000.csv"], Names(Directory.GetFiles(runs)));
            Assert.Empty(Directory.GetDirectories(runs));
        }
        finally
        {
            Remove(runs);
        }
    }

    /// <summary>A session played to disk in runs/.record (as the game records it), not yet exported.</summary>
    private static (UiSession Session, string Runs, string Log) Recorded(string name, string stamp)
    {
        string runs = Path.Combine(TempDir(name), "runs");
        UiSession session = UiSession.Start(42, sizeOverridePx: 256, settlementsOverride: 4);
        string log = ExportFixtures.PlayToDisk(session, Path.Combine(runs, UiSession.RecordFolder), stamp, turns: 2);
        return (session, runs, log);
    }

    /// <summary>THE RETRY: a delete that fails twice with the errors a transient Windows lock gives (IOException, then
    /// UnauthorizedAccessException) and then succeeds removes the log on the third attempt, after waits of exactly 250
    /// and 500 ms, every precondition checked again before each attempt; no reason is reported and no export log is written.
    /// (The seam stands in for File.Delete: Linux does not enforce Windows share modes. The Windows twin below uses a real
    /// lock.)</summary>
    [Fact]
    public void ATransientlyLockedLog_IsRemovedOnARetry()
    {
        (UiSession session, string runs, string log) = Recorded("civsim-record-retry", "20261102-140558-a1");
        try
        {
            string telemetry = UiSession.TelemetryPath(log);
            var deletes = new List<string>();
            var pauses = new List<int>();
            void Delete(string path)
            {
                deletes.Add(path);
                if (deletes.Count == 1) throw new IOException("being scanned");
                if (deletes.Count == 2) throw new UnauthorizedAccessException("access denied");
                File.Delete(path);
            }
            var errors = new StringWriter();
            string? xlsx = session.ExportWorkbook(log, errors, Delete, pauses.Add);
            Assert.Equal(Path.Combine(Path.GetFullPath(runs), "session-20261102-140558-a1.xlsx"), xlsx);
            Assert.Equal([Path.GetFullPath(telemetry), Path.GetFullPath(telemetry), Path.GetFullPath(telemetry)], deletes);
            Assert.Equal([250, 500], pauses);
            Assert.False(File.Exists(telemetry));
            Assert.Equal("", errors.ToString());
            Assert.False(File.Exists(UiSession.ExportLogPath(log)));
            Assert.Equal(["session-20261102-140558-a1.xlsx"], Names(Directory.GetFiles(runs)));
        }
        finally
        {
            Remove(Path.GetDirectoryName(runs)!);
        }
    }

    /// <summary>A PERSISTENT failure: five attempts (waits 250, 500, 750, 1000 ms), then the log is KEPT, unchanged, the
    /// workbook still written and returned (a failed delete never blocks the close), and the reason — the attempts and the
    /// last error — is on stderr AND in .record/export-STAMP.log, one line, so the next failure on the Director's machine
    /// is diagnosable although the launcher discards stderr. The runs folder still shows only the workbook.</summary>
    [Fact]
    public void APersistentlyLockedLog_IsKept_AndTheReasonIsLoggedInTheRecordFolder()
    {
        (UiSession session, string runs, string log) = Recorded("civsim-record-locked", "20261102-140558-a1");
        try
        {
            string telemetry = UiSession.TelemetryPath(log);
            string before = ExportFixtures.Sha(telemetry);
            int deletes = 0;
            var pauses = new List<int>();
            var errors = new StringWriter();
            string? xlsx = session.ExportWorkbook(log, errors, _ => { deletes++; throw new IOException("held by test"); }, pauses.Add);

            Assert.Equal(Path.Combine(Path.GetFullPath(runs), "session-20261102-140558-a1.xlsx"), xlsx);
            Assert.True(File.Exists(xlsx));
            Assert.Equal(SessionWorkbook.RemovalAttempts, deletes);
            Assert.Equal(5, deletes);
            Assert.Equal([250, 500, 750, 1000], pauses);
            Assert.Equal(before, ExportFixtures.Sha(telemetry));
            const string reason = "raw telemetry log kept in the session record (telemetry-20261102-140558-a1.jsonl): "
                + "not removed after 5 attempts; last error IOException: held by test";
            Assert.Equal(reason + Environment.NewLine, errors.ToString());
            string exportLog = UiSession.ExportLogPath(log);
            Assert.Equal(Path.Combine(runs, UiSession.RecordFolder, "export-20261102-140558-a1.log"), exportLog);
            string[] logged = File.ReadAllLines(exportLog);
            Assert.Single(logged);
            Assert.EndsWith(" " + reason, logged[0], StringComparison.Ordinal);
            Assert.Equal(["session-20261102-140558-a1.xlsx"], Names(Directory.GetFiles(runs)));

            // a second failing close appends, never truncates
            Assert.NotNull(session.ExportWorkbook(log, new StringWriter(), _ => throw new UnauthorizedAccessException("still held"), _ => { }));
            logged = File.ReadAllLines(exportLog);
            Assert.Equal(2, logged.Length);
            Assert.EndsWith(" raw telemetry log kept in the session record (telemetry-20261102-140558-a1.jsonl): not removed after 5 attempts; "
                + "last error UnauthorizedAccessException: still held", logged[1], StringComparison.Ordinal);
        }
        finally
        {
            Remove(Path.GetDirectoryName(runs)!);
        }
    }

    /// <summary>The preconditions are not retried: a log that is not the one the workbook holds is kept at once — no
    /// delete, no wait.</summary>
    [Fact]
    public void AFailedPrecondition_IsFinal_WithoutADeleteOrAWait()
    {
        (UiSession _, string runs, string log) = Recorded("civsim-record-precondition", "20261102-140558");
        try
        {
            (string xlsx, SessionExportResult r) = SessionWorkbook.ExportBeside(UiSession.ManifestPath(log), RawTelemetry.RemovedAfterExport);
            Assert.Equal(RawTelemetry.RemovedAfterExport, r.RawTelemetry);
            string telemetry = UiSession.TelemetryPath(log);
            File.AppendAllText(telemetry, "\n");
            int deletes = 0, pauses = 0;
            Assert.Equal("the raw log on disk is not the log the workbook holds",
                SessionWorkbook.RemoveRawTelemetry(xlsx, telemetry, r, _ => deletes++, _ => pauses++));
            Assert.Equal(0, deletes);
            Assert.Equal(0, pauses);
            Assert.True(File.Exists(telemetry));
        }
        finally
        {
            Remove(Path.GetDirectoryName(runs)!);
        }
    }

    /// <summary>WINDOWS ONLY (a no-op elsewhere: Linux does not enforce share modes, the seam tests above cover the logic):
    /// the log held open by another handle WITHOUT delete sharing (FileShare.Read) is the real lock a scanner or a
    /// preview holds. Released during the second wait, the third attempt removes it; held throughout, it is kept and the
    /// reason logged.</summary>
    [Fact]
    public void OnWindows_ARealSharingLock_IsRetried_ThenLogged()
    {
        if (!OperatingSystem.IsWindows()) return;
        (UiSession session, string runs, string log) = Recorded("civsim-record-winlock", "20261102-140558");
        try
        {
            string telemetry = UiSession.TelemetryPath(log);
            var holder = new FileStream(telemetry, FileMode.Open, FileAccess.Read, FileShare.Read);
            var pauses = new List<int>();
            Assert.NotNull(session.ExportWorkbook(log, new StringWriter(), null, ms =>
            {
                pauses.Add(ms);
                if (pauses.Count == 2) holder.Dispose();
            }));
            Assert.Equal([250, 500], pauses);
            Assert.False(File.Exists(telemetry));

            (UiSession again, string runs2, string log2) = Recorded("civsim-record-winlock2", "20261102-140558");
            try
            {
                string telemetry2 = UiSession.TelemetryPath(log2);
                using var held = new FileStream(telemetry2, FileMode.Open, FileAccess.Read, FileShare.Read);
                var errors = new StringWriter();
                Assert.NotNull(again.ExportWorkbook(log2, errors, null, _ => { }));
                Assert.True(File.Exists(telemetry2));
                Assert.Contains("not removed after 5 attempts; last error IOException", errors.ToString(), StringComparison.Ordinal);
                Assert.Contains("not removed after 5 attempts", File.ReadAllText(UiSession.ExportLogPath(log2)), StringComparison.Ordinal);
            }
            finally
            {
                Remove(Path.GetDirectoryName(runs2)!);
            }
        }
        finally
        {
            Remove(Path.GetDirectoryName(runs)!);
        }
    }
}
