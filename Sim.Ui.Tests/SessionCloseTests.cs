using System.Globalization;
using Microsoft.Xna.Framework.Input;
using Sim.Core.Observability.Forensic;
using Sim.Ui.Export;
using Sim.Ui.Headless;
using Xunit;

namespace Sim.Ui.Tests;

/// <summary>
/// D-086 DE2 (the Director, 2026-11-11: "I've closed the game and runs excel sheet didn't form. The screen froze") — THE
/// CLOSE, through the real GameUi on the headless harness (the same per-frame Update and Draw the window runs):
/// the five steps in their order, each writing what it always wrote; the saving screen shown while they run, with no game
/// input taken; a second close request that runs nothing twice; an interrupted close — at every step — recovered by
/// <c>sim-ui --export-session</c>; a failed close that keeps the saving screen up with what to do; and a workbook write
/// interrupted half way that never leaves a partial workbook in <c>runs\</c>; and (verification round 2, RR-R2-03) the share
/// of the telemetry the workbook step has read, as it reports it and as the saving screen shows it. In the "ImGui context"
/// collection: ImGui's context is process-wide, so frames of two test classes must never run at once.
/// </summary>
[Collection("ImGui context")]
public class SessionCloseTests
{
    private static string TempDir(string name)
    {
        string dir = Path.Combine(Path.GetTempPath(), name + "-" + Environment.ProcessId.ToString(CultureInfo.InvariantCulture));
        if (Directory.Exists(dir)) Directory.Delete(dir, recursive: true);
        return dir;
    }

    /// <summary>A small session played as the game plays it: the manifest and the forensic run record at launch, then
    /// three End Turns, each followed by the End Turn save (order log, chronicle, trace, telemetry) — so the record files
    /// of the last End Turn are on disk before any close. In the game's layout: the record folder <c>runs/.record</c>.</summary>
    private static UiFrameHarness Played(string root, out UiSession session)
    {
        session = UiSession.Start(42, sizeOverridePx: 256, settlementsOverride: 4);
        string record = Path.Combine(root, "runs", UiSession.RecordFolder);
        UiFrameHarness h = UiFrameHarness.Start(session, record, Path.Combine(AppContext.BaseDirectory, "assets"));
        string log = h.SessionLogPath;
        session.ExportManifest("2026-11-11 12:00:00", log);
        session.ExportForensicRun("2026-11-11 12:00:00+00:00", log);
        for (int t = 0; t < 3; t++)
        {
            session.EndTurn();
            session.Save(log);
            session.ExportChronicle(UiSession.ChroniclePath(log));
            session.ExportTrace(UiSession.TracePath(log));
            session.ExportTelemetry(UiSession.TelemetryPath(log));
        }
        return h;
    }

    private static bool HasCloseRecord(string log) =>
        File.Exists(UiSession.ForensicPath(log)) && ForensicRecordFile.Read(UiSession.ForensicPath(log)).Close is not null;

    private static string Workbook(string log) => SessionWorkbook.WorkbookPathFor(UiSession.ManifestPath(log));

    private static IReadOnlyList<string> ClosingText(UiFrameHarness h)
    {
        var lines = new List<string>();
        foreach (FrameTextEntry e in h.Ui.FrameText.Last)
        {
            Assert.Equal(FrameSurface.Closing, e.Surface);   // the saving screen ALONE: no HUD, no map, no panel
            lines.Add(e.Text);
        }
        return lines;
    }

    // ------------------------------------------------------------------------- order and what is written --

    /// <summary>THE ORDER OF THE STEPS IS UNCHANGED (ADR-059 §2): save, telemetry finalize, forensic close, workbook, raw-log
    /// removal — seen from inside the close at the start of each step: the close record appears only after step 3 started
    /// and before the workbook exists; the workbook only after step 4 started; the raw log is removed last. And what is
    /// written is unchanged: the files a close on the worker writes are byte for byte the files the synchronous
    /// SaveOnExit writes for the same session.</summary>
    [Fact]
    public void TheCloseRunsItsFiveStepsInOrder_OnAWorker_AndWritesExactlyWhatTheSynchronousCloseWrites()
    {
        string a = TempDir("civsim-close-order"), b = TempDir("civsim-close-sync");
        try
        {
            using UiFrameHarness h = Played(a, out UiSession _);
            string log = h.SessionLogPath;
            var seen = new List<(int Step, bool Close, bool Book, bool Raw)>();
            h.Ui.CloseStepHook = k => seen.Add((k, HasCloseRecord(log), File.Exists(Workbook(log)), File.Exists(UiSession.TelemetryPath(log))));
            h.Ui.BeginClose();
            Assert.True(h.Ui.Close!.Wait(TimeSpan.FromMinutes(5)));
            Assert.True(h.Ui.Close.Succeeded);
            Assert.Equal(
            [
                (SessionClose.Save, false, false, true),
                (SessionClose.FinalizeTelemetry, false, false, true),
                (SessionClose.ForensicClose, false, false, true),
                (SessionClose.Workbook, true, false, true),
                (SessionClose.RemoveRawTelemetry, true, true, true),
            ], seen);
            Assert.False(File.Exists(UiSession.TelemetryPath(log)), "the raw log is removed last, once the workbook is verified");
            for (int k = 0; k < SessionClose.StepNames.Length; k++) Assert.True(h.Ui.Close.StepSeconds(k) >= 0.0);

            using UiFrameHarness sync = Played(b, out UiSession _);
            sync.Ui.SaveOnExit();
            string[] mine = Directory.GetFiles(a, "*", SearchOption.AllDirectories), theirs = Directory.GetFiles(b, "*", SearchOption.AllDirectories);
            Array.Sort(mine, StringComparer.Ordinal);
            Array.Sort(theirs, StringComparer.Ordinal);
            Assert.Equal(mine.Select(p => Path.GetRelativePath(a, p)), theirs.Select(p => Path.GetRelativePath(b, p)));
            for (int i = 0; i < mine.Length; i++) Assert.Equal(File.ReadAllBytes(theirs[i]), File.ReadAllBytes(mine[i]));
        }
        finally
        {
            if (Directory.Exists(a)) Directory.Delete(a, recursive: true);
            if (Directory.Exists(b)) Directory.Delete(b, recursive: true);
        }
    }

    // ------------------------------------------------------------------------------ the saving screen --

    /// <summary>
    /// THE SAVING STATE IS SHOWN: while the close works (held at its workbook step here), every frame draws the saving screen
    /// alone — the heading, the three steps done with their times, the workbook step running, and that the window closes by
    /// itself — and nothing else (no status band, no map, no panel). Game input is not taken: End Turn (Space) and Escape
    /// change nothing. A SECOND CLOSE REQUEST while it runs only adds the "still saving" line and runs no step twice; the
    /// forensic file holds exactly one close record. When the close is done the screen says so and the window may close.
    /// </summary>
    [Fact]
    public void WhileTheCloseRuns_TheWindowShowsTheSavingScreenAlone_TakesNoInput_AndASecondRequestRunsNothingTwice()
    {
        string dir = TempDir("civsim-close-screen");
        using var hold = new ManualResetEventSlim(false);
        using var held = new ManualResetEventSlim(false);
        try
        {
            using UiFrameHarness h = Played(dir, out UiSession session);
            int[] runs = new int[SessionClose.StepNames.Length];
            h.Ui.CloseStepHook = k =>
            {
                Interlocked.Increment(ref runs[k]);
                if (k != SessionClose.Workbook) return;
                held.Set();
                Assert.True(hold.Wait(TimeSpan.FromMinutes(2)), "the test released the workbook step");
            };
            long turn = session.World.Clock.Turn;
            h.Ui.BeginClose();
            Assert.True(held.Wait(TimeSpan.FromMinutes(2)));
            Assert.True(h.Ui.Closing);
            Assert.False(h.Ui.ExitAllowed);

            h.Frame();
            IReadOnlyList<string> lines = ClosingText(h);
            Assert.StartsWith(GameUi.ClosingHeading, lines[0], StringComparison.Ordinal);
            Assert.StartsWith("[done] " + SessionClose.StepNames[0] + " (", lines[1], StringComparison.Ordinal);
            Assert.StartsWith("[done] " + SessionClose.StepNames[1] + " (", lines[2], StringComparison.Ordinal);
            Assert.StartsWith("[done] " + SessionClose.StepNames[2] + " (", lines[3], StringComparison.Ordinal);
            Assert.StartsWith("[now] " + SessionClose.StepNames[3], lines[4], StringComparison.Ordinal);
            Assert.Equal("[ ] " + SessionClose.StepNames[4], lines[5]);
            Assert.Contains(GameUi.ClosingNote, lines);
            Assert.DoesNotContain(GameUi.ClosingAgainNote, lines);
            Assert.Empty(h.Ui.Controls.Last);   // nothing to click: no End Turn, no section, no panel

            h.Key(Keys.Space);
            h.Key(Keys.Escape);
            h.Idle(3);
            Assert.Equal(turn, session.World.Clock.Turn);   // End Turn was not taken
            Assert.Equal(0, h.ExitRequests);

            h.Ui.BeginClose();   // the player closes again while the run is saved
            h.Frame();
            Assert.Contains(GameUi.ClosingAgainNote, ClosingText(h));
            Assert.Equal(2, h.Ui.CloseRequests);

            hold.Set();
            Assert.True(h.Ui.Close!.Wait(TimeSpan.FromMinutes(5)));
            Assert.True(h.Ui.ExitAllowed);
            h.Frame();
            IReadOnlyList<string> done = ClosingText(h);
            Assert.Equal("The run is saved", done[0]);
            for (int k = 0; k < SessionClose.StepNames.Length; k++) Assert.StartsWith("[done] " + SessionClose.StepNames[k], done[1 + k], StringComparison.Ordinal);
            Assert.Equal("The workbook: " + Workbook(h.SessionLogPath), done[^1]);
            Assert.Equal([1, 1, 1, 1, 1], runs);
            int closes = 0;
            foreach (string line in File.ReadAllLines(UiSession.ForensicPath(h.SessionLogPath)))
                if (line.Contains("\"rec\":\"close\"", StringComparison.Ordinal)) closes++;
            Assert.Equal(1, closes);
        }
        finally
        {
            hold.Set();
            if (Directory.Exists(dir)) Directory.Delete(dir, recursive: true);
        }
    }

    // --------------------------------------------------------------------------------- the window's host --

    /// <summary>
    /// THE WINDOW'S EXIT, AS ITS HOST TAKES IT (SimUiGame's decision: <see cref="GameUi.HostShouldExit"/> after Update,
    /// <see cref="GameUi.HostExiting"/> in OnExiting — the harness in Host mode). Escape with nothing open asks to quit; the
    /// window's exit then STARTS the close and is CANCELLED, so the loop keeps running and draws the saving screen. Every
    /// later exit request while the run is saved (the close button, Alt+F4) is cancelled too and starts no step twice. Once
    /// the workbook is written the host exits by itself, with nothing more from the player.
    /// </summary>
    [Fact]
    public void TheHostCancelsTheExitWhileTheRunIsSaved_AndExitsByItselfOnceTheWorkbookIsWritten()
    {
        string dir = TempDir("civsim-close-host");
        using var hold = new ManualResetEventSlim(false);
        using var held = new ManualResetEventSlim(false);
        try
        {
            using UiFrameHarness h = Played(dir, out UiSession _);
            h.Host = true;
            int[] runs = new int[SessionClose.StepNames.Length];
            h.Ui.CloseStepHook = k =>
            {
                Interlocked.Increment(ref runs[k]);
                if (k != SessionClose.Workbook) return;
                held.Set();
                Assert.True(hold.Wait(TimeSpan.FromMinutes(2)), "the test released the workbook step");
            };
            Assert.False(h.Ui.Closing);
            h.Key(Keys.Escape);   // nothing open: the player asks to quit
            Assert.Equal(1, h.ExitRequests);
            Assert.True(h.Ui.Closing, "the window's exit started the close");
            Assert.False(h.Exited, "the exit was cancelled while the run is saved");
            Assert.True(held.Wait(TimeSpan.FromMinutes(2)));
            h.Idle(3);
            Assert.False(h.Exited);
            Assert.StartsWith(GameUi.ClosingHeading, ClosingText(h)[0], StringComparison.Ordinal);

            h.CloseWindow();   // the close button while the run is saved
            h.CloseWindow();   // and Alt+F4
            Assert.False(h.Exited);
            Assert.Equal(3, h.Ui.CloseRequests);
            h.Frame();
            Assert.Contains(GameUi.ClosingAgainNote, ClosingText(h));

            hold.Set();
            Assert.True(h.Ui.Close!.Wait(TimeSpan.FromMinutes(5)));
            Assert.False(h.Exited, "the exit comes from the host's next tick");
            h.Frame();
            Assert.True(h.Exited, "the host exits by itself once the workbook is written");
            Assert.Equal([1, 1, 1, 1, 1], runs);
            Assert.True(File.Exists(Workbook(h.SessionLogPath)));
        }
        finally
        {
            hold.Set();
            if (Directory.Exists(dir)) Directory.Delete(dir, recursive: true);
        }
    }

    /// <summary>A CLOSE THAT ENDS WITHOUT A WORKBOOK, AS THE HOST TAKES IT: the host does NOT exit by itself (the saving screen
    /// stays up with the reason and what to do, however many frames pass); the player's next exit request — the close button
    /// here — lets the window close at once.</summary>
    [Fact]
    public void AfterACloseWithoutAWorkbook_TheHostWaitsForThePlayer_AndTheNextExitRequestClosesTheWindow()
    {
        string dir = TempDir("civsim-close-host-failed");
        try
        {
            using UiFrameHarness h = Played(dir, out UiSession _);
            h.Host = true;
            h.Ui.CloseErrors = new StringWriter();
            h.Ui.CloseStepHook = k => { if (k == SessionClose.Workbook) throw new IOException("the disk is full (test)"); };
            h.CloseWindow();
            Assert.False(h.Exited);
            Assert.True(h.Ui.Close!.Wait(TimeSpan.FromMinutes(5)));
            Assert.True(h.Ui.Close.HasFailed);
            h.Idle(5);
            Assert.False(h.Exited, "no workbook: the host waits for the player");
            IReadOnlyList<string> lines = ClosingText(h);
            Assert.Equal(GameUi.ClosingProblemHeading, lines[0]);
            Assert.Contains(lines, l => l.Contains("the disk is full (test)", StringComparison.Ordinal));
            Assert.Equal(GameUi.ClosingRecoveryNote, lines[^1]);
            h.CloseWindow();
            Assert.True(h.Exited, "the player's next exit request closes the window");
        }
        finally
        {
            if (Directory.Exists(dir)) Directory.Delete(dir, recursive: true);
        }
    }

    // ---------------------------------------------------------------------------- interrupted and failed --

    /// <summary>
    /// AN INTERRUPTED CLOSE IS RECOVERABLE, AT EVERY STEP: the close stops before step <paramref name="step"/> (the test
    /// seam's throw stands in for the process ending there). The record files of the last End Turn are complete, the failure
    /// is in the export log, and <c>sim-ui --export-session runs</c> then writes and self-checks the workbook. Its README says
    /// plainly whether the close record was written; from step 3 on (the close record written), every sheet but the README
    /// is the workbook of an uninterrupted close of the same session.
    /// </summary>
    [Theory]
    [InlineData(SessionClose.Save)]
    [InlineData(SessionClose.FinalizeTelemetry)]
    [InlineData(SessionClose.ForensicClose)]
    [InlineData(SessionClose.Workbook)]
    [InlineData(SessionClose.RemoveRawTelemetry)]
    public void AnInterruptedClose_AtAnyStep_IsRecoveredByTheHeadlessExport(int step)
    {
        string dir = TempDir("civsim-close-cut-" + step.ToString(CultureInfo.InvariantCulture)), reference = TempDir("civsim-close-ref-" + step.ToString(CultureInfo.InvariantCulture));
        try
        {
            using UiFrameHarness h = Played(dir, out UiSession _);
            string log = h.SessionLogPath;
            var errors = new StringWriter();
            h.Ui.CloseErrors = errors;
            h.Ui.CloseStepHook = k => { if (k == step) throw new IOException("the process ended here (test)"); };
            h.Ui.SaveOnExit();
            Assert.True(h.Ui.Close!.HasFailed);
            Assert.Contains("stopped at step " + (step + 1).ToString(CultureInfo.InvariantCulture), h.Ui.Close.Failure, StringComparison.Ordinal);
            Assert.Contains("stopped at step", File.ReadAllText(UiSession.ExportLogPath(log)), StringComparison.Ordinal);
            Assert.False(h.Ui.ExitAllowed);   // the saving screen stays up with the failure until the player closes again
            h.Frame();
            Assert.Contains(GameUi.ClosingRecoveryNote, ClosingText(h));
            h.Ui.BeginClose();
            Assert.True(h.Ui.ExitAllowed);

            var o = new StringWriter();
            var e = new StringWriter();
            Assert.Equal(0, SessionExportCommand.Run(["--export-session", Path.Combine(dir, "runs")], o, e));
            Assert.True(File.Exists(Workbook(log)), e.ToString());
            OoxmlWorkbook.Content back = OoxmlWorkbook.Read(File.ReadAllBytes(Workbook(log)));
            string close = ReadmeValue(back, "close record");
            Assert.StartsWith(step <= SessionClose.ForensicClose ? "The forensic file holds NO close record" : "The forensic file holds the close record",
                close, StringComparison.Ordinal);
            if (step <= SessionClose.ForensicClose) return;   // no close record: the record of the last End Turn, exported as it is

            using UiFrameHarness whole = Played(reference, out UiSession _);
            whole.Ui.SaveOnExit();
            Assert.True(whole.Ui.Close!.Succeeded);
            OoxmlWorkbook.Content full = OoxmlWorkbook.Read(File.ReadAllBytes(Workbook(whole.SessionLogPath)));
            for (int s = 1; s < SessionWorkbook.SheetNames.Length; s++)   // every sheet but the README (which says the log was kept)
                Assert.Equal(full.Sheets[s], back.Sheets[s]);
        }
        finally
        {
            if (Directory.Exists(dir)) Directory.Delete(dir, recursive: true);
            if (Directory.Exists(reference)) Directory.Delete(reference, recursive: true);
        }
    }

    /// <summary>A WORKBOOK WRITE INTERRUPTED HALF WAY never leaves a partial workbook in runs\: the export writes the whole
    /// workbook to <c>.record/session-STAMP.xlsx.partial</c> first and moves it into place in one rename. A partial file
    /// left by an interrupted write is overwritten by the next export, which leaves none behind.</summary>
    [Fact]
    public void APartialWorkbookLeftByAnInterruptedWrite_IsReplaced_AndNoPartialIsLeftInRuns()
    {
        string dir = TempDir("civsim-close-partial");
        try
        {
            using UiFrameHarness h = Played(dir, out UiSession session);
            string log = h.SessionLogPath;
            session.FinalizeTelemetry(UiSession.TelemetryPath(log));
            session.ExportForensicClose(log);
            string manifest = UiSession.ManifestPath(log);
            string partial = SessionWorkbook.PartialPathFor(manifest);
            Assert.Equal(Path.GetDirectoryName(Path.GetFullPath(manifest)), Path.GetDirectoryName(partial));   // in the record folder
            File.WriteAllBytes(partial, [0x50, 0x4B, 0x03]);   // what a write cut short would leave
            Assert.False(File.Exists(Workbook(log)));
            (string path, SessionExportResult r) = SessionWorkbook.ExportBeside(manifest);
            Assert.False(File.Exists(partial));
            Assert.Equal(r.Bytes, File.ReadAllBytes(path));
            Assert.Equal(SessionWorkbook.SheetNames, OoxmlWorkbook.Read(File.ReadAllBytes(path)).SheetNames);
            Assert.Single(Directory.GetFiles(Path.Combine(dir, "runs")));   // runs\ holds the one workbook
        }
        finally
        {
            if (Directory.Exists(dir)) Directory.Delete(dir, recursive: true);
        }
    }

    /// <summary>A CLOSE THAT WRITES NO WORKBOOK (a named refusal: here a telemetry line of an unknown vintage) keeps the
    /// saving screen up with the refusal and what to do — instead of the window closing on a missing workbook — until the
    /// player closes again; Escape then counts as that request.</summary>
    [Fact]
    public void ARefusedWorkbook_KeepsTheSavingScreenUp_WithTheReasonAndTheRecovery_UntilThePlayerClosesAgain()
    {
        string dir = TempDir("civsim-close-refused");
        try
        {
            using UiFrameHarness h = Played(dir, out UiSession _);
            string log = h.SessionLogPath;
            h.Ui.CloseErrors = new StringWriter();
            h.Ui.CloseStepHook = k =>
            {
                if (k != SessionClose.Workbook) return;
                string path = UiSession.TelemetryPath(log);
                File.WriteAllText(path, File.ReadAllText(path).Replace("\"telemetry/v6\"", "\"telemetry/v9\"", StringComparison.Ordinal));
            };
            h.Ui.SaveOnExit();
            SessionClose close = h.Ui.Close!;
            Assert.False(close.HasFailed);
            Assert.Null(close.WorkbookPath);
            Assert.Contains(SessionExportException.TelemetrySchemaUnknown, close.WorkbookProblem, StringComparison.Ordinal);
            Assert.False(h.Ui.ExitAllowed);
            h.Frame();
            IReadOnlyList<string> lines = ClosingText(h);
            Assert.Equal(GameUi.ClosingProblemHeading, lines[0]);
            Assert.Contains(close.WorkbookProblem!, lines);
            Assert.Equal(GameUi.ClosingRecoveryNote, lines[^1]);
            Assert.Contains(GameUi.RecoveryCommand, GameUi.ClosingRecoveryNote, StringComparison.Ordinal);
            Assert.True(File.Exists(UiSession.TelemetryPath(log)), "nothing was removed");
            // the Windows launcher discards stderr: the reason is also in the session's export log, beside the record files
            string exportLog = UiSession.ExportLogPath(log);
            Assert.Contains("session close: no workbook written: ", File.ReadAllText(exportLog), StringComparison.Ordinal);
            Assert.Contains(SessionExportException.TelemetrySchemaUnknown, File.ReadAllText(exportLog), StringComparison.Ordinal);
            h.Key(Keys.Escape);
            Assert.True(h.Ui.ExitAllowed);

            // the headless recovery command, refused for the same reason, says so in the same log (it shows no stderr on
            // Windows: the game is a window application); the log is the one the manifest's stamp names
            Assert.Equal(exportLog, SessionExportCommand.ExportLogFor(UiSession.ManifestPath(log)));
            int before = File.ReadAllLines(exportLog).Length;
            var e = new StringWriter();
            Assert.Equal(2, SessionExportCommand.Run(["--export-session", Path.Combine(dir, "runs")], new StringWriter(), e));
            string[] after = File.ReadAllLines(exportLog);
            Assert.Equal(before + 1, after.Length);
            Assert.Contains("export-session refused - ", after[^1], StringComparison.Ordinal);
            Assert.Contains(SessionExportException.TelemetrySchemaUnknown, after[^1], StringComparison.Ordinal);
            Assert.False(File.Exists(Workbook(log)));
        }
        finally
        {
            if (Directory.Exists(dir)) Directory.Delete(dir, recursive: true);
        }
    }

    /// <summary>A WORKBOOK WRITTEN ADDS NO EXPORT-LOG LINE: the log is the failure path's only (a successful close, and a
    /// successful headless export, write exactly what they wrote before D-086).</summary>
    [Fact]
    public void ASuccessfulCloseAndASuccessfulHeadlessExport_WriteNoExportLog()
    {
        string dir = TempDir("civsim-close-nolog");
        try
        {
            using UiFrameHarness h = Played(dir, out UiSession _);
            h.Ui.SaveOnExit();
            Assert.True(h.Ui.Close!.Succeeded);
            Assert.False(File.Exists(UiSession.ExportLogPath(h.SessionLogPath)));
            Assert.Equal(0, SessionExportCommand.Run(["--export-session", Path.Combine(dir, "runs")], new StringWriter(), new StringWriter()));
            Assert.False(File.Exists(UiSession.ExportLogPath(h.SessionLogPath)));
        }
        finally
        {
            if (Directory.Exists(dir)) Directory.Delete(dir, recursive: true);
        }
    }

    /// <summary>
    /// A LOG KEPT ON PURPOSE IS NOT A CLOSE PROBLEM (verification round 1, RR-R1-03 (c)): with the developer switch
    /// (<c>--keep-raw-telemetry</c>, <see cref="UiSession.KeepRawTelemetry"/>) the close writes the workbook, keeps the raw
    /// log, and reports nothing — no line on the close's error writer, no export log — exactly as before D-086, when the
    /// removal was skipped under the switch. The removal step skips itself under the switch; without that guard it would ask
    /// the removal, which answers "not verified for removal (Kept)", and log a spurious "raw telemetry log kept" line.
    /// </summary>
    [Fact]
    public void WithKeepRawTelemetry_TheCloseKeepsTheLog_AndReportsNothing()
    {
        string dir = TempDir("civsim-close-keep");
        try
        {
            using UiFrameHarness h = Played(dir, out UiSession session);
            session.KeepRawTelemetry = true;
            var errors = new StringWriter();
            h.Ui.CloseErrors = errors;
            h.Ui.SaveOnExit();
            Assert.True(h.Ui.Close!.Succeeded);
            Assert.True(File.Exists(Workbook(h.SessionLogPath)));
            Assert.True(File.Exists(UiSession.TelemetryPath(h.SessionLogPath)), "the developer switch keeps the raw log");
            Assert.Equal("", errors.ToString());
            Assert.False(File.Exists(UiSession.ExportLogPath(h.SessionLogPath)), "a log kept on purpose is not a close problem");
        }
        finally
        {
            if (Directory.Exists(dir)) Directory.Delete(dir, recursive: true);
        }
    }

    /// <summary>
    /// EVERY HEADLESS EXPORT FAILURE IS THE EXPORT LOG'S LAST LINE (verification round 1, RR-R1-04): the recovery note
    /// (docs/m6-session-export/README.md §8) says that when no workbook appears, the reason is the last line of
    /// <c>runs\.record\export-STAMP.log</c>, because the game is a window application and shows no stderr. A failure that is
    /// neither a named refusal nor an I/O error — a record file this account may not read (UnauthorizedAccessException;
    /// MEASURED before this fix, as an unprivileged user with the trace unreadable: an unhandled exception, exit 134, no log
    /// line) or any other defect — is said on stderr and appended to that log, and the command exits 2 with no workbook. The
    /// test seam stands in for the export, throwing where it would; the command resolved the session's own manifest.
    /// </summary>
    [Theory]
    [InlineData("UnauthorizedAccessException")]
    [InlineData("InvalidOperationException")]
    public void AnyHeadlessExportFailure_IsTheExportLogsLastLine_AndTheCommandExits2(string kind)
    {
        string dir = TempDir("civsim-export-failed-" + kind);
        try
        {
            using UiFrameHarness h = Played(dir, out UiSession _);
            string log = h.SessionLogPath;
            string? asked = null;
            Exception failure = kind == "UnauthorizedAccessException"
                ? new UnauthorizedAccessException("Access to the path '" + UiSession.TracePath(log) + "' is denied.")
                : new InvalidOperationException("a defect in the export (test)");
            var e = new StringWriter();
            int code = SessionExportCommand.Run(["--export-session", Path.Combine(dir, "runs")], new StringWriter(), e,
                export: manifest =>
                {
                    asked = manifest;
                    throw failure;
                });
            Assert.Equal(2, code);
            Assert.Equal(Path.GetFullPath(UiSession.ManifestPath(log)), Path.GetFullPath(asked!));
            Assert.Contains(kind + ": " + failure.Message, e.ToString(), StringComparison.Ordinal);
            string[] lines = File.ReadAllLines(UiSession.ExportLogPath(log));
            Assert.EndsWith(" export-session failed - " + kind + ": " + failure.Message, lines[^1], StringComparison.Ordinal);
            Assert.False(File.Exists(Workbook(log)));
        }
        finally
        {
            if (Directory.Exists(dir)) Directory.Delete(dir, recursive: true);
        }
    }

    // ------------------------------------------------------------- the streamed export keeps the removal's rule --

    /// <summary>
    /// THE RAW LOG GOES ONLY WHEN THE WORKBOOK REBUILDS IT EXACTLY (ADR-059 amendment 2026-10-29, condition 2), under the
    /// streamed reader of D-086: the decoded lines, each followed by a line feed, must be the file byte for byte. A log whose
    /// last line lost its final line feed — with a close record that lists exactly that file, so only this condition can
    /// keep it — is KEPT (its lines rebuild a file one byte longer); the same log with its line feed is removable.
    /// </summary>
    [Fact]
    public void ALogWithoutItsFinalLineFeed_IsKept_EvenWhenTheCloseRecordListsIt()
    {
        List<(string Name, byte[] Bytes)> files = ExportFixtures.Files("current-v6");
        SessionExportResult whole = SessionWorkbook.Build(ExportFixtures.Manifest(files), ExportFixtures.Reader(files),
            raw: RawTelemetry.RemovedAfterExport);
        Assert.Equal(RawTelemetry.RemovedAfterExport, whole.RawTelemetry);   // the control: the file as written is removable

        int t = files.FindIndex(f => f.Name.StartsWith("telemetry-", StringComparison.Ordinal));
        int fr = files.FindIndex(f => f.Name.StartsWith("forensic-", StringComparison.Ordinal));
        byte[] log = files[t].Bytes;
        Assert.Equal((byte)'\n', log[^1]);
        byte[] cut = log[..^1];
        string before = Sim.Core.Observability.Forensic.ForensicIdentity.Sha256Hex(log), after = Sim.Core.Observability.Forensic.ForensicIdentity.Sha256Hex(cut);
        string forensic = System.Text.Encoding.UTF8.GetString(files[fr].Bytes);
        Assert.Contains(before, forensic, StringComparison.Ordinal);
        files[t] = (files[t].Name, cut);
        files[fr] = (files[fr].Name, System.Text.Encoding.UTF8.GetBytes(forensic.Replace(before, after, StringComparison.Ordinal)));
        SessionExportResult r = SessionWorkbook.Build(ExportFixtures.Manifest(files), ExportFixtures.Reader(files),
            raw: RawTelemetry.RemovedAfterExport);
        Assert.Equal(after, r.TelemetrySha256);   // the close record lists this very file
        Assert.Equal(RawTelemetry.Kept, r.RawTelemetry);
        Assert.Equal(whole.TelemetryLines, r.TelemetryLines);   // every line is in the workbook all the same
    }

    // ------------------------------------------------------------------------------ a game played to its end --

    /// <summary>
    /// A SESSION PLAYED PAST WORLD YEAR 5920 KEEPS ITS WORKBOOK (found by measurement, D-086 DE2): from 1920 CE a turn is half
    /// a year, and the trace writer (<see cref="Sim.Core.Kernel.SessionTrace.Line"/>) writes the year as it is,
    /// <c>5920.5</c>. The kernel's own reader (<see cref="Sim.Core.Kernel.SessionTrace.Parse"/>, frozen) reads the year as an
    /// integer and throws on it, and the export used that reader as its check: every session past turn 1270 was refused
    /// (<c>record-unreadable</c>) and lost its workbook at close. The export now checks the trace with its own reader, which
    /// reads the year as the number written: the current-v6 session with half-year years exports, its Trace sheet the lines
    /// as written; a line that is not a trace line is still refused, by its number.
    /// </summary>
    [Fact]
    public void ATraceWithHalfYearTurns_Exports_AndAMalformedTraceIsStillRefusedByLine()
    {
        List<(string Name, byte[] Bytes)> files = ExportFixtures.Files("current-v6");
        int t = files.FindIndex(f => f.Name.StartsWith("trace-", StringComparison.Ordinal));
        string[] lines = ExportFixtures.Lines(files[t].Bytes);
        var halves = new List<string> { lines[0] };
        for (int i = 1; i < lines.Length; i++)
        {
            string[] p = lines[i].Split(',');
            p[1] = (5920.0 + 0.5 * i).ToString(CultureInfo.InvariantCulture);   // the writer's text: 5920.5, 5921, 5921.5, …
            halves.Add(string.Join(',', p));
        }
        Assert.Contains("5920.5", halves[1], StringComparison.Ordinal);
        // the frozen kernel reader refuses the writer's own text (the defect, reported to the Director as a proposal)
        Assert.Throws<FormatException>(() => Sim.Core.Kernel.SessionTrace.Parse(halves, "half-year trace"));

        files[t] = (files[t].Name, System.Text.Encoding.UTF8.GetBytes(string.Join('\n', halves) + "\n"));
        SessionExportResult r = SessionWorkbook.Build(ExportFixtures.Manifest(files), ExportFixtures.Reader(files));
        IReadOnlyList<string[]> trace = OoxmlWorkbook.Read(r.Bytes).Sheets[Array.IndexOf(SessionWorkbook.SheetNames, "Trace")];
        Assert.Equal(halves.Count, trace.Count);
        for (int i = 1; i < halves.Count; i++) Assert.Equal(halves[i].Split(','), trace[i]);

        foreach (string bad in new[] { "3,5921.5,1,2,3", "3,a5921,1,2,3,ab12", "3,5921.5,1.5,2,3,ab12", "3,NaN,1,2,3,ab12" })
        {
            var broken = new List<(string Name, byte[] Bytes)>(files);
            broken[t] = (files[t].Name, System.Text.Encoding.UTF8.GetBytes(string.Join('\n', halves) + "\n" + bad + "\n"));
            SessionExportException e = Assert.Throws<SessionExportException>(
                () => SessionWorkbook.Build(ExportFixtures.Manifest(broken), ExportFixtures.Reader(broken)));
            Assert.Equal(SessionExportException.RecordUnreadable, e.Code);
            Assert.Contains("line " + (halves.Count + 1).ToString(CultureInfo.InvariantCulture), e.Message, StringComparison.Ordinal);
        }

        // Verification round 1 (RR-R1-03 (b)): THE COLUMN COUNT IS THE KERNEL READER'S RULE, EXACTLY SIX. A line with a
        // seventh column (the year an integer, so only the count is wrong) is refused by the frozen reader; the export's own
        // check refuses it the same way, by its number and its column count — too many columns as well as too few.
        string seven = "3,5921,1,2,3,ab12,extra";
        Assert.Throws<InvalidDataException>(() => Sim.Core.Kernel.SessionTrace.Parse([lines[0], seven], "seven columns"));
        var wide = new List<(string Name, byte[] Bytes)>(files);
        wide[t] = (files[t].Name, System.Text.Encoding.UTF8.GetBytes(string.Join('\n', halves) + "\n" + seven + "\n"));
        SessionExportException tooWide = Assert.Throws<SessionExportException>(
            () => SessionWorkbook.Build(ExportFixtures.Manifest(wide), ExportFixtures.Reader(wide)));
        Assert.Equal(SessionExportException.RecordUnreadable, tooWide.Code);
        Assert.Contains("line " + (halves.Count + 1).ToString(CultureInfo.InvariantCulture) + ": expected 6 columns", tooWide.Message,
            StringComparison.Ordinal);
        Assert.Contains("found 7", tooWide.Message, StringComparison.Ordinal);
    }

    // ------------------------------------- verification round 2 (RR-R2-03): the workbook step's progress --

    /// <summary>
    /// THE WORKBOOK STEP REPORTS HOW MUCH OF THE TELEMETRY LOG IT HAS READ (verification round 2, RR-R2-03): one report per
    /// telemetry line, after that line, each the share of the log's bytes read so far — the bytes through that line's line
    /// feed, of the whole file — so the reports rise line by line and the last is exactly 1. The current-v6 session has 16
    /// lines, so 16 reports, the k-th at the k-th line feed found in the file's own bytes.
    /// </summary>
    [Fact]
    public void TheWorkbookStep_ReportsTheShareOfTheTelemetryRead_AfterEachLine_EndingAtTheWholeLog()
    {
        List<(string Name, byte[] Bytes)> files = ExportFixtures.Files("current-v6");
        byte[] log = ExportFixtures.Bytes(files, "telemetry-");
        var reports = new List<double>();
        SessionWorkbook.Build(ExportFixtures.Manifest(files), ExportFixtures.Reader(files), progress: reports.Add);
        var expected = new List<double>();
        for (int at = Array.IndexOf(log, (byte)'\n'); at >= 0; at = Array.IndexOf(log, (byte)'\n', at + 1))
            expected.Add((double)(at + 1) / log.Length);
        Assert.Equal(16, expected.Count);
        Assert.Equal(expected, reports);
        Assert.Equal(1.0, reports[^1]);
    }

    /// <summary>
    /// THE SAVING SCREEN SAYS HOW MUCH OF THE TELEMETRY THE WORKBOOK STEP HAS READ (verification round 2, RR-R2-03; DE2-R1
    /// (a): the window shows that the run is being saved). The close is held inside its workbook step, just after the second
    /// of the session's three telemetry lines was read (the <see cref="GameUi.CloseProgressHook"/> seam): the step's progress
    /// is then the bytes through the second line feed of the log on disk, of the whole file, and the frame drawn there shows
    /// the running step as "[now] Building and checking the workbook: N% of the telemetry read", N the whole percent of that
    /// share. Released, the close has reported once per line (three reports, the last the whole log) and has written its
    /// workbook.
    /// </summary>
    [Fact]
    public void WhileTheWorkbookIsBuilt_TheSavingScreenShowsTheShareOfTheTelemetryRead()
    {
        string dir = TempDir("civsim-close-progress");
        using var hold = new ManualResetEventSlim(false);
        using var held = new ManualResetEventSlim(false);
        try
        {
            using UiFrameHarness h = Played(dir, out UiSession _);
            var reports = new List<double>();   // written on the worker; read while it is held, and after it has finished
            h.Ui.CloseProgressHook = p =>
            {
                reports.Add(p);
                if (reports.Count != 2) return;
                held.Set();
                Assert.True(hold.Wait(TimeSpan.FromMinutes(2)), "the test released the workbook step");
            };
            h.Ui.BeginClose();
            Assert.True(SpinWait.SpinUntil(() => held.IsSet || h.Ui.Close!.Finished, TimeSpan.FromMinutes(2)));
            Assert.True(held.IsSet, "the workbook step reported its progress after the second telemetry line");
            Assert.Equal(SessionClose.Workbook, h.Ui.Close!.CurrentStep);

            byte[] log = File.ReadAllBytes(UiSession.TelemetryPath(h.SessionLogPath));
            int first = Array.IndexOf(log, (byte)'\n'), second = Array.IndexOf(log, (byte)'\n', first + 1);
            Assert.True(first > 0 && second > first && Array.IndexOf(log, (byte)'\n', second + 1) == log.Length - 1, "three lines");
            double share = (double)(second + 1) / log.Length;
            Assert.Equal(share, h.Ui.Close.WorkbookProgress);
            int percent = (int)(share * 100.0);
            Assert.InRange(percent, 1, 99);
            h.Frame();
            IReadOnlyList<string> lines = ClosingText(h);
            Assert.Equal("[now] " + SessionClose.StepNames[SessionClose.Workbook] + ": "
                + percent.ToString(CultureInfo.InvariantCulture) + "% of the telemetry read", lines[1 + SessionClose.Workbook]);

            hold.Set();
            Assert.True(h.Ui.Close.Wait(TimeSpan.FromMinutes(5)));
            Assert.True(h.Ui.Close.Succeeded);
            Assert.Equal(3, reports.Count);
            Assert.Equal(1.0, reports[^1]);
            Assert.Equal(1.0, h.Ui.Close.WorkbookProgress);
        }
        finally
        {
            hold.Set();
            if (Directory.Exists(dir)) Directory.Delete(dir, recursive: true);
        }
    }

    private static string ReadmeValue(OoxmlWorkbook.Content back, string key)
    {
        foreach (string[] row in back.Sheets[0])
            if (row.Length > 1 && row[0] == key) return row[1];
        throw new InvalidOperationException("no README row " + key);
    }
}
