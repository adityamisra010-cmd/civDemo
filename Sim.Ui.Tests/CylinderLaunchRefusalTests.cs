using Sim.Core.Kernel;
using Sim.Core.Worldgen;
using Xunit;

namespace Sim.Ui.Tests;

/// <summary>
/// Batch 3 WS3 — THE LAUNCHER REFUSES A SIZE A CYLINDER CANNOT TAKE, VISIBLY. With worldgen.json topology.wrapX on,
/// <see cref="Worldgen.Generate"/> refuses a map size the lattice stride (4 px) does not divide (the seam would join a
/// partial lattice block to the first one). Before this fix that refusal ended both launches — a fresh <c>--size 250</c>
/// and the <c>--resume</c> of a session recorded at 250 px — as an UNHANDLED exception (exit 134 on Linux, no dialog),
/// which the Director's "preserve existing user-facing error handling" forbids. Both now go through the D-052 reporter
/// shape: "error: &lt;message&gt;" on stderr, the dialog once, exit code 2 (Cli.Guard's, which `sim run --size 250`
/// already exits with), before any window opens, the session files untouched.
/// </summary>
public class CylinderLaunchRefusalTests
{
    /// <summary>A FLAT session recorded at <paramref name="size"/> px (a size a flat world takes — as the pre-cylinder
    /// build recorded it): manifest, three turns, trace and log.</summary>
    private static string RecordFlatSession(string dir, int size)
    {
        string path = Path.Combine(dir, $"orders-20261010-120000-s{size}-n4.bin");
        UiSession live = UiSession.Start(42, sizeOverridePx: size, settlementsOverride: 4, wrapXOverride: false);
        live.ExportManifest("2026-10-10 12:00:00", path);
        for (int t = 0; t < 3; t++) live.EndTurn();
        live.ExportTrace(UiSession.TracePath(path));
        var log = new OrderLog();
        for (int i = 0; i < live.Orders.Count; i++) log.Append(live.Orders[i]);
        using (FileStream f = File.Create(path)) log.Save(f);
        return UiSession.ManifestPath(path);
    }

    /// <summary>The refusal Generate gives for <paramref name="size"/> under the shipped (wrapped) worldgen.json.</summary>
    private static string ShippedRefusal(int size)
    {
        WorldgenConfigException e = Assert.Throws<WorldgenConfigException>(
            () => UiSession.Start(42, sizeOverridePx: size, settlementsOverride: 4));
        return e.Message;
    }

    [Fact]
    public void TheRefusal_NamesWhatTheUserCanChange_TheNearestSizesACylinderTakes()
    {
        string m250 = ShippedRefusal(250);
        Assert.Contains("size 250 px", m250, StringComparison.Ordinal);
        // Fix round 1 (verifier VR-1): the sizes are named as the nearest THE CYLINDER TAKES (was "248 px or 252 px").
        Assert.Contains("the nearest sizes the cylinder takes are 248 px and 252 px", m250, StringComparison.Ordinal);
        // worldgen.json is an embedded resource: a player cannot set topology.wrapX, so the message must not tell them to.
        Assert.DoesNotContain("set topology.wrapX false", m250, StringComparison.Ordinal);
        string m6 = ShippedRefusal(6);   // below the 3-node floor: the only nearest size is the floor
        Assert.Contains("size 6 px", m6, StringComparison.Ordinal);
        Assert.Contains("the nearest size the cylinder takes is 12 px", m6, StringComparison.Ordinal);

        // Final nits (verifier RC1-4, 2026-10-15): the floor is the CYLINDER's rule, not an instruction — the old second
        // sentence, "Use a size that is a multiple of 4 px and at least 12 px — …", recommended 12 px, where founding
        // then refuses ("settlement siting could only place 1 of 12 sites …", MEASURED with `sim run --size 12`).
        // Final-round fix 3 (verifier RC1-4-FLOOR-NOT-NAMED, 2026-10-15): the founding sentence names no founding size
        // (it said "a map far larger than 12 px is needed", which the records called naming founding's floor) and closes
        // the message ONLY when a size it names is below the size founding was measured to succeed from (124 px at the
        // default settlement count): 6 and 8 px (nearest 12 px) carry it, 250 px (248 and 252 px, both of which found)
        // does not — before this fix it did. Same text as the CLI's (Sim.Tests CliSaveAndSizeMessagesTests, which also
        // pins the threshold from both sides): both report Worldgen.Generate's WorldgenConfigException.
        const string founding = "That rule is the cylinder's alone: founding also needs room to site every settlement, "
            + "and at the default settlement count, settlement siting can refuse maps this small, with its own message.";
        string m8 = ShippedRefusal(8);
        Assert.Contains("size 8 px", m8, StringComparison.Ordinal);
        foreach (string m in new[] { m250, m6, m8 })
            Assert.DoesNotContain("Use a size", m, StringComparison.Ordinal);
        Assert.EndsWith("The cylinder's own rule is a multiple of 4 px, at least 12 px — the nearest sizes the cylinder "
            + "takes are 248 px and 252 px.", m250, StringComparison.Ordinal);
        Assert.DoesNotContain("founding", m250, StringComparison.Ordinal);
        foreach (string m in new[] { m6, m8 })
            Assert.EndsWith("The cylinder's own rule is a multiple of 4 px, at least 12 px — the nearest size the cylinder "
                + "takes is 12 px. " + founding, m, StringComparison.Ordinal);
    }

    [Theory]
    [InlineData(250)]
    [InlineData(254)]
    [InlineData(8)]     // final-round fix 3: the variant that carries the founding sentence takes the same path
    public void TheNewGameLaunch_AtASizeACylinderRefuses_ReportsIt_ToStderrAndTheDialog_WithExitCode2(int size)
    {
        string refusal = ShippedRefusal(size);
        var stderr = new StringWriter();
        var dialogs = new List<(string Title, string Message)>();
        UiSession? session = NewGameLaunch.Attempt(42, size, 4, null, stderr, (t, m) => dialogs.Add((t, m)), out int exit);

        Assert.Null(session);
        Assert.Equal(NewGameLaunch.WorldRefusedExitCode, exit);
        Assert.Equal(2, exit);   // Cli.Guard's code: `sim run --founded --size 250` exits with it too
        Assert.Equal("error: " + refusal + Environment.NewLine, stderr.ToString());
        Assert.Single(dialogs);
        Assert.Equal(NewGameLaunch.DialogTitle, dialogs[0].Title);
        Assert.Equal(NewGameLaunch.WorldRefusedDialog(refusal), dialogs[0].Message);

        // A dialog that cannot be shown, or none at all (CIV_UI_NO_DIALOG), changes nothing.
        var stderr2 = new StringWriter();
        Assert.Null(NewGameLaunch.Attempt(42, size, 4, null, stderr2, (_, _) => throw new DllNotFoundException("SDL2"), out int exit2));
        Assert.Equal(2, exit2);
        Assert.Equal(stderr.ToString(), stderr2.ToString());
        var stderr3 = new StringWriter();
        Assert.Null(NewGameLaunch.Attempt(42, size, 4, null, stderr3, null, out int exit3));
        Assert.Equal(2, exit3);
        Assert.Equal(stderr.ToString(), stderr3.ToString());
    }

    [Fact]
    public void TheNewGameLaunch_AtASizeACylinderTakes_StartsTheSameWorldAsUiSessionStart_WithNoReport()
    {
        var stderr = new StringWriter();
        var dialogs = new List<(string Title, string Message)>();
        UiSession? session = NewGameLaunch.Attempt(42, 256, 4, null, stderr, (t, m) => dialogs.Add((t, m)), out int exit);

        Assert.NotNull(session);
        Assert.Equal(0, exit);
        Assert.Equal("", stderr.ToString());
        Assert.Empty(dialogs);
        Assert.True(session.World.Terrain!.WrapX);   // the shipped world: a cylinder
        Assert.Equal(WorldHash.ComputeHex(UiSession.Start(42, sizeOverridePx: 256, settlementsOverride: 4).World),
            WorldHash.ComputeHex(session.World));
    }

    [Fact]
    public void TheResumeLaunch_OfASessionRecordedAtASizeACylinderRefuses_ReportsIt_WithExitCode2_AndTouchesNoFile()
    {
        string dir = Path.Combine(Path.GetTempPath(), $"ws3-resume-refused-{Environment.ProcessId}-{Guid.NewGuid():N}");
        Directory.CreateDirectory(dir);
        try
        {
            string manifest = RecordFlatSession(dir, 250);
            string[] before = ChronicleAndResumeTests.Fingerprint(dir);
            string refusal = ShippedRefusal(250);

            var stderr = new StringWriter();
            var dialogs = new List<(string Title, string Message)>();
            UiSession? resumed = ResumeLaunch.Attempt(manifest, stderr, (t, m) => dialogs.Add((t, m)), out string log, out int exit);

            Assert.Null(resumed);
            Assert.Equal(ResumeLaunch.WorldRefusedExitCode, exit);
            Assert.Equal(2, exit);
            Assert.Equal("", log);
            Assert.Equal("error: " + refusal + Environment.NewLine, stderr.ToString());
            Assert.Single(dialogs);
            Assert.Equal(ResumeLaunch.DialogTitle, dialogs[0].Title);
            Assert.Equal(ResumeLaunch.WorldRefusedDialog(refusal), dialogs[0].Message);
            Assert.Equal(before, ChronicleAndResumeTests.Fingerprint(dir));
        }
        finally
        {
            Directory.Delete(dir, recursive: true);
        }
    }

    /// <summary>Runs the real exe (the UiSessionReplayTests recipe) bounded by <paramref name="limit"/>; a hang is
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

    /// <summary>The real exe: Program.cs routes the fresh start through <see cref="NewGameLaunch"/>. Measured before
    /// the fix: exit 134 and "Unhandled exception. Sim.Core.Worldgen.WorldgenConfigException: …".</summary>
    [Fact]
    public void TheUiLaunch_AtASizeACylinderRefuses_ExitsWith2AndTheReport_BeforeAnyWindowOpens()
    {
        string refusal = ShippedRefusal(250);
        (int exit, string stdout, string stderr) = RunSimUi(TimeSpan.FromMinutes(3), "--size", "250");
        Assert.Equal(2, exit);
        Assert.Equal("error: " + refusal, stderr.TrimEnd('\r', '\n'));
        Assert.DoesNotContain("Unhandled exception", stderr, StringComparison.Ordinal);
        Assert.DoesNotContain("session manifest:", stdout, StringComparison.Ordinal);
    }

    /// <summary>The real exe, --resume of a session recorded at 250 px (flat, as the pre-cylinder build recorded it).</summary>
    [Fact]
    public void TheUiResumeLaunch_OfASessionAtASizeACylinderRefuses_ExitsWith2AndTheReport_BeforeAnyWindowOpens()
    {
        string dir = Path.Combine(Path.GetTempPath(), $"ws3-resume-launch-{Environment.ProcessId}-{Guid.NewGuid():N}");
        Directory.CreateDirectory(dir);
        try
        {
            RecordFlatSession(dir, 250);
            string[] before = ChronicleAndResumeTests.Fingerprint(dir);
            string refusal = ShippedRefusal(250);
            (int exit, string stdout, string stderr) = RunSimUi(TimeSpan.FromMinutes(3), "--resume", dir);
            Assert.Equal(2, exit);
            Assert.Equal("error: " + refusal, stderr.TrimEnd('\r', '\n'));
            Assert.DoesNotContain("Unhandled exception", stderr, StringComparison.Ordinal);
            Assert.DoesNotContain("resumed ", stdout, StringComparison.Ordinal);
            Assert.Equal(before, ChronicleAndResumeTests.Fingerprint(dir));
        }
        finally
        {
            Directory.Delete(dir, recursive: true);
        }
    }
}
