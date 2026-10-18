using Sim.Core.Kernel;
using Xunit;

namespace Sim.Ui.Tests;

/// <summary>
/// D-057 (2026-10-18) — A SAVED SESSION WITH A FILE THAT IS MISSING OR CANNOT BE READ. Before, <see cref="UiSession.Resume"/>
/// let the reader's exception escape (InvalidDataException for a malformed trace line or a manifest that is not one,
/// SnapshotFormatException for an order log that is not one or is cut short, FileNotFoundException for a missing file),
/// and ResumeLaunch caught none of them: the launch ended as an unhandled exception, which a Windows player never sees.
/// Now each is a <see cref="SessionUnreadableException"/> naming the file, reported like every refusal — "error: " +
/// the message on stderr, the dialog once, exit 2 — and the session's files are only read. A `--resume` argument naming
/// no manifest is reported the same way by the real exe.
/// </summary>
public class SessionUnreadableResumeTests
{
    private static string Played(out string dir)
    {
        dir = Path.Combine(Path.GetTempPath(), "d057-unreadable-" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(dir);
        string log = Path.Combine(dir, "orders-20261018-120000.bin");
        UiSession live = UiSession.Start(42, sizeOverridePx: 256, settlementsOverride: 4);
        live.ExportManifest("2026-10-18 12:00:00", log);
        Assert.True(live.EmitSectorOrders([40, 20, 10, 10, 20], live.World.Settlements[0].Id.Value));
        for (int t = 0; t < 3; t++) live.EndTurn();
        live.Save(log);
        live.ExportTrace(UiSession.TracePath(log));
        return log;
    }

    private static (string[] Files, byte[][] Bytes) Contents(string dir)
    {
        string[] files = Directory.GetFiles(dir).Order(StringComparer.Ordinal).ToArray();
        return (files, files.Select(File.ReadAllBytes).ToArray());
    }

    public static TheoryData<string> Corruptions() =>
    [
        "manifest not json", "manifest of another schema tag", "order log cut short", "order log not an order log",
        "order log missing", "trace line malformed",
    ];

    [Theory]
    [MemberData(nameof(Corruptions))]
    public void AnUnreadableSessionFile_IsRefusedWithItsNameAndTheDialog_AndEveryFileIsOnlyRead(string corruption)
    {
        string log = Played(out string dir);
        try
        {
            string manifest = UiSession.ManifestPath(log), trace = UiSession.TracePath(log);
            (string file, string what) = corruption switch
            {
                "manifest not json" => (manifest, "manifest"),
                "manifest of another schema tag" => (manifest, "manifest"),
                "trace line malformed" => (trace, "trace"),
                _ => (log, "order log"),
            };
            switch (corruption)
            {
                case "manifest not json": File.WriteAllText(manifest, "{ not json"); break;
                case "manifest of another schema tag": File.WriteAllText(manifest, "{\"schema\":\"something-else/v1\"}"); break;
                case "order log cut short": File.WriteAllBytes(log, File.ReadAllBytes(log)[..^7]); break;
                case "order log not an order log": File.WriteAllBytes(log, "NOTANORDERLOG"u8.ToArray()); break;
                case "order log missing": File.Delete(log); break;
                case "trace line malformed":
                    File.WriteAllLines(trace, [.. File.ReadAllLines(trace), "3,thirty,x,4,y,zz"]);
                    break;
            }
            var before = Contents(dir);

            var e = Assert.Throws<SessionUnreadableException>(() => UiSession.Resume(manifest, out _));
            Assert.Equal(Path.GetFullPath(file), Path.GetFullPath(e.FilePath));
            Assert.StartsWith("the session's " + what + " ", e.Message, StringComparison.Ordinal);
            Assert.Contains(" cannot be read: ", e.Message, StringComparison.Ordinal);
            Assert.NotNull(e.InnerException);

            var stderr = new StringWriter();
            var shown = new List<string>();
            Assert.Null(ResumeLaunch.Attempt(manifest, stderr, (_, text) => shown.Add(text), out _, out int code));
            Assert.Equal(ResumeLaunch.SessionUnreadableExitCode, code);
            Assert.Equal(2, code);
            Assert.StartsWith("error: the session's " + what + " ", stderr.ToString(), StringComparison.Ordinal);
            string dialog = Assert.Single(shown);
            Assert.StartsWith("This saved session cannot be resumed: one of its files cannot be read.", dialog, StringComparison.Ordinal);
            Assert.EndsWith("The session files were not changed.", dialog, StringComparison.Ordinal);

            var after = Contents(dir);
            Assert.Equal(before.Files, after.Files);
            for (int i = 0; i < after.Files.Length; i++) Assert.Equal(before.Bytes[i], after.Bytes[i]);
        }
        finally
        {
            Directory.Delete(dir, recursive: true);
        }
    }

    /// <summary>The control: the same session, unharmed, resumes (the refusals above are about the damage).</summary>
    [Fact]
    public void TheSameSessionUnharmed_Resumes()
    {
        string log = Played(out string dir);
        try
        {
            UiSession resumed = UiSession.Resume(UiSession.ManifestPath(log), out _);
            Assert.Equal(3, resumed.World.Clock.Turn);
            Assert.Empty(resumed.ResumeNotice);
        }
        finally
        {
            Directory.Delete(dir, recursive: true);
        }
    }

    /// <summary>The real exe, `--resume` naming a directory with no session manifest and a malformed trace: each is
    /// reported (exit 2, "error: the session's …"), not an unhandled exception (measured before: exit 134,
    /// "Unhandled exception. System.IO.FileNotFoundException" / "System.IO.InvalidDataException").</summary>
    [Fact]
    public void TheRealExe_ReportsAnUnreadableSession_AndExitsWith2()
    {
        string empty = Path.Combine(Path.GetTempPath(), "d057-no-manifest-" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(empty);
        string log = Played(out string dir);
        try
        {
            (int exit, _, string stderr) = RunSimUi(TimeSpan.FromMinutes(3), "--resume", empty);
            Assert.Equal(2, exit);
            Assert.StartsWith("error: the session's manifest " + empty + " cannot be read: no session-*.json manifest in", stderr, StringComparison.Ordinal);
            Assert.DoesNotContain("Unhandled exception", stderr, StringComparison.Ordinal);

            File.WriteAllLines(UiSession.TracePath(log), [.. File.ReadAllLines(UiSession.TracePath(log)), "x"]);
            (exit, _, stderr) = RunSimUi(TimeSpan.FromMinutes(3), "--resume", dir);
            Assert.Equal(2, exit);
            Assert.StartsWith("error: the session's trace ", stderr, StringComparison.Ordinal);
            Assert.DoesNotContain("Unhandled exception", stderr, StringComparison.Ordinal);
        }
        finally
        {
            Directory.Delete(empty, recursive: true);
            Directory.Delete(dir, recursive: true);
        }
    }

    /// <summary>Runs the real exe (CylinderLaunchRefusalTests' recipe) bounded by <paramref name="limit"/>; a hang is
    /// non-termination (ADR-015 §7.1). CIV_UI_NO_DIALOG is set so a refusal never opens the modal dialog.</summary>
    private static (int Exit, string Out, string Err) RunSimUi(TimeSpan limit, params string[] args)
    {
        var psi = new System.Diagnostics.ProcessStartInfo("dotnet")
        {
            RedirectStandardOutput = true,
            RedirectStandardError = true,
            UseShellExecute = false,
            WorkingDirectory = AppContext.BaseDirectory,
        };
        psi.Environment[ResumeLaunch.NoDialogVariable] = "1";
        psi.ArgumentList.Add(Path.Combine(AppContext.BaseDirectory, "Sim.Ui.dll"));
        foreach (string a in args) psi.ArgumentList.Add(a);
        using System.Diagnostics.Process p = System.Diagnostics.Process.Start(psi)!;
        Task<string> o = p.StandardOutput.ReadToEndAsync(), e = p.StandardError.ReadToEndAsync();
        if (!p.WaitForExit(limit))
        {
            p.Kill(entireProcessTree: true);
            throw new TimeoutException("Sim.Ui " + string.Join(' ', args) + " did not finish in " + limit + " (non-termination)");
        }
        return (p.ExitCode, o.Result, e.Result);
    }
}
