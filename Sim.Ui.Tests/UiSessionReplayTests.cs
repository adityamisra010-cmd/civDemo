using Sim.Core.Kernel;
using Sim.Core.State;
using Xunit;

namespace Sim.Ui.Tests;

// T1.9 adversarial hardening: these tests drive the REAL UI session code —
// UiSession.Start (UiFounding + the UI's executor recipe), EmitSectorOrders (the
// labour control's actual stamping), EndTurn, Save — and replay the produced log
// headlessly. Order-timing drift (Turn+1 stamping), pipeline/era drift in the
// UI executor, and stamped-filename drift all break HERE now, not in a played
// session. ADR-033 D9 / audit E38: the M1 single-slider path these tests used to
// drive (EmitLaborOrder, OrderKind 2) left Sim.Ui, so they drive the LIVE labour
// path — the five-sector batch the action surface emits — and the replay guarantee
// still covers what a player can actually do.
public class UiSessionReplayTests
{
    [Fact]
    public void UiSession_PlayedAndSaved_ReplaysHashForHash()
    {
        // Play 20 turns through the UI seam, emitting orders the way the labour control does.
        var session = Sim.Ui.UiSession.Start(42);
        int capital = session.World.Settlements[0].Id.Value;
        var hashes = new List<string>(20);
        for (int t = 1; t <= 20; t++)
        {
            if (t == 3) Assert.True(session.EmitSectorOrders([40, 20, 15, 15, 10], capital));
            if (t == 11) Assert.True(session.EmitSectorOrders([20, 30, 20, 20, 10], capital));
            session.EndTurn();
            hashes.Add(WorldHash.ComputeHex(session.World));

            // THE DELIVERY SEMANTIC, pinned (adversarial pass follow-up: a
            // Turn+1 stamping mutant survived the pure replay comparison
            // because a shifted stamp shifts live and replay IDENTICALLY —
            // replay fidelity alone cannot see it. What it breaks is the
            // control's promise that an applied split takes effect on the very
            // next End Turn; that promise is asserted here, turn-exactly).
            if (t == 3) Assert.Equal((0.40, 0.20), (session.World.SectorAllocations[0].Farming, session.World.SectorAllocations[0].Herding));
            if (t == 11) Assert.Equal((0.20, 0.30), (session.World.SectorAllocations[0].Farming, session.World.SectorAllocations[0].Herding));
        }

        string logPath = Path.Combine(Path.GetTempPath(), $"orders-ui-replay-{Guid.NewGuid():N}.bin");
        session.Save(logPath);
        try
        {
            OrderLog loaded;
            using (var stream = File.OpenRead(logPath)) loaded = OrderLog.Load(stream);
            Assert.Equal(2 * Sectors.Count, loaded.Count); // both applied splits, nothing else
            for (int i = 0; i < loaded.Count; i++) Assert.Equal(OrderKind.SectorAllocation, loaded[i].Kind);

            // Headless replay through the SAME UI recipes (founding + executor).
            TurnExecutor exec = Sim.Ui.UiSession.BuildProductionExecutor(loaded);
            WorldState world = Sim.Ui.UiFounding.Found(42);
            OrderValidation.ValidateAgainstWorld(loaded, world);
            for (int t = 1; t <= 20; t++)
            {
                world = exec.Step(world);
                Assert.Equal(hashes[t - 1], WorldHash.ComputeHex(world));
            }
            // The orders really steered the sim (anti-vacuity).
            Assert.Equal(0.20, world.SectorAllocations[0].Farming);
            Assert.Equal(0.30, world.SectorAllocations[0].Herding);
        }
        finally
        {
            File.Delete(logPath);
        }
    }

    // ---------------------------------------------------------------------------------------------------------
    // S0 (2026-10-10) — ADR-034 §4 ADOPTED BY THE UI'S RESUME, following the OrderDeliveryValidationTests pattern.
    // UiSession.ReplayTo used to call neither OrderValidation.ValidateAgainstWorld nor ValidateAtDelivery, so an
    // order for a deferred id that never came to exist (an actor no revolt founded, a settlement no colony took)
    // was SILENTLY IGNORED by its consumer on resume while `sim replay` of the same log rejected it. Now the resume
    // rejects it with the very diagnostic the CLI's replay loop produces (Sim.Cli/Program.cs Replay: the up-front
    // pass on the founded world, then the delivery check of each turn's batch against PREV before the step), and
    // the UI launch prints it the way Cli.Guard does ("error: <message>", exit 2). A forged record changes no
    // hash (its consumer ignores it), so without the checks every case below resumes "fine" — measured as the
    // mutants in the ADR-034 S0 note.
    // ---------------------------------------------------------------------------------------------------------

    private const int ForgedSessionTurns = 4;

    private static int MaxSettlement(IReadOnlyWorldState w)
    {
        int max = -1;
        for (int i = 0; i < w.Settlements.Count; i++) max = Math.Max(max, w.Settlements[i].Id.Value);
        return max;
    }

    /// <summary>The forged record of each case, built against the founded (turn-0) world.</summary>
    private static OrderRecord Forge(string kind, IReadOnlyWorldState turnZero)
    {
        // A settlement id above every turn-0 id (so its check is deferred) that no colony can have taken by
        // turn 3: colonization allocates ids one at a time from the maximum.
        int ghost = MaxSettlement(turnZero) + 1000;
        return kind switch
        {
            // a polity no revolt founded, delivered at turn 2 (the OrderDeliveryValidationTests case)
            "forged actor, delivered" => OrderRecord.From(2, new PolityId(999), OrderKind.SetResearchTarget, 2, 0.0),
            // a settlement no colony took, delivered at turn 3
            "ghost colony, delivered" => OrderRecord.From(3, Sim.Ui.ViewModel.UiPlayer.Empire, OrderKind.LaborAllocation, ghost, 40.0),
            // a turn-0 order for a settlement that does not exist: not deferred, the up-front pass rejects it
            "ghost settlement, turn 0" => OrderRecord.From(0, Sim.Ui.ViewModel.UiPlayer.Empire, OrderKind.LaborAllocation, ghost, 40.0),
            // stamped with the last played turn: re-queued by the resume and delivered by the next End Turn
            "forged actor, re-queued" => OrderRecord.From(ForgedSessionTurns, new PolityId(999), OrderKind.SetResearchTarget, 2, 0.0),
            _ => throw new ArgumentOutOfRangeException(nameof(kind), kind, null),
        };
    }

    /// <summary>A real UI session (seed 42, 256 px, 4 settlements; a five-sector split applied at turn 1) played
    /// <see cref="ForgedSessionTurns"/> turns and written as the game writes it — manifest, trace, order log — with
    /// <paramref name="bad"/> merged into the log in turn order. Returns the manifest path.</summary>
    private static string WriteSession(string dir, Func<IReadOnlyWorldState, OrderRecord?> bad, out OrderLog log, out UiSession live)
    {
        string path = Path.Combine(dir, "orders-20261010-120000-s256-n4.bin");
        live = UiSession.Start(42, sizeOverridePx: 256, settlementsOverride: 4);
        live.ExportManifest("2026-10-10 12:00:00", path);
        WorldState turnZero = live.World;
        int capital = turnZero.Settlements[0].Id.Value;
        for (int t = 0; t < ForgedSessionTurns; t++)
        {
            if (t == 1) Assert.True(live.EmitSectorOrders([40, 20, 15, 15, 10], capital));
            live.EndTurn();
        }
        live.ExportTrace(UiSession.TracePath(path));
        OrderRecord? forged = bad(turnZero);
        log = new OrderLog();
        bool placed = forged is null;
        for (int i = 0; i < live.Orders.Count; i++)
        {
            if (!placed && live.Orders[i].Turn > forged!.Value.Turn) { log.Append(forged.Value); placed = true; }
            log.Append(live.Orders[i]);
        }
        if (!placed) log.Append(forged!.Value);
        using (FileStream f = File.Create(path)) log.Save(f);
        return UiSession.ManifestPath(path);
    }

    /// <summary>`sim replay`'s loop (Sim.Cli/Program.cs Replay) over the same founding and executor recipes, run
    /// <paramref name="turns"/> turns: the message of the OrderValidationException it rejects the log with, or
    /// null when it accepts it.</summary>
    private static string? CliReplayDiagnostic(OrderLog log, long turns)
    {
        WorldState world = UiFounding.Found(42, sizeOverridePx: 256, settlementsOverride: 4);
        TurnExecutor executor = UiSession.BuildProductionExecutor(log);
        try
        {
            OrderValidation.ValidateAgainstWorld(log, world);
            WorldState turnZero = world;
            for (long t = 1; t <= turns; t++)
            {
                WorldState previous = world;
                OrderValidation.ValidateAtDelivery(log.BatchFor(previous.Clock.Turn), turnZero, previous);
                world = executor.Step(previous);
            }
        }
        catch (OrderValidationException e)
        {
            return e.Message;
        }
        return null;
    }

    [Theory]
    [InlineData("forged actor, delivered", "is issued by polity 999, which is not a registered Empire when the order is delivered (turn 2,")]
    [InlineData("ghost colony, delivered", "which does not exist when the order is delivered (turn 3,")]
    [InlineData("ghost settlement, turn 0", "which does not exist in this world")]
    [InlineData("forged actor, re-queued", "is issued by polity 999, which is not a registered Empire when the order is delivered (turn 4,")]
    public void Resume_RejectsALogWithAnIllegalDeferredId_WithTheDiagnosticSimReplayGives(string kind, string fragment)
    {
        string dir = Path.Combine(Path.GetTempPath(), $"s0-resume-validation-{Environment.ProcessId}-{Guid.NewGuid():N}");
        Directory.CreateDirectory(dir);
        try
        {
            string manifest = WriteSession(dir, w => Forge(kind, w), out OrderLog forged, out _);

            // What `sim replay` says about this log. The re-queued order is delivered by the turn after the last
            // played one, so the CLI meets it when it replays one turn further (the turn the UI's next End Turn plays).
            long cliTurns = kind == "forged actor, re-queued" ? ForgedSessionTurns + 1 : ForgedSessionTurns;
            string? cli = CliReplayDiagnostic(forged, cliTurns);
            Assert.NotNull(cli);   // non-vacuous: the CLI rejects this log
            Assert.Contains(fragment, cli, StringComparison.Ordinal);
            if (kind != "ghost settlement, turn 0") Assert.Contains("(ADR-034)", cli, StringComparison.Ordinal);

            // The resume rejects it with exactly that diagnostic, instead of ignoring the order.
            OrderValidationException e = Assert.Throws<OrderValidationException>(() => UiSession.Resume(manifest, out _));
            Assert.Equal(cli, e.Message);

            // Control: the same session without the forged record resumes, hash for hash with the live one.
            string clean = WriteSession(dir, _ => null, out _, out UiSession cleanLive);
            UiSession resumed = UiSession.Resume(clean, out _);
            Assert.Equal(WorldHash.ComputeHex(cleanLive.World), WorldHash.ComputeHex(resumed.World));
            Assert.Empty(resumed.ResumeNotice);
        }
        finally
        {
            Directory.Delete(dir, recursive: true);
        }
    }

    [Fact]
    public void ReplayTo_RunsOnlyFromTheFoundedWorld_WhereItsUpFrontCheckIsMeaningful()
    {
        // The up-front pass validates against the world the replay starts from; anything but the founded world
        // with an empty log would validate against the wrong turn-0 world, so it is refused.
        UiSession played = UiSession.Start(42, sizeOverridePx: 256, settlementsOverride: 4);
        played.EndTurn();
        InvalidOperationException e = Assert.Throws<InvalidOperationException>(() => played.ReplayTo(new OrderLog(), 3));
        Assert.Contains("at turn 1", e.Message, StringComparison.Ordinal);
    }

    /// <summary>Runs <c>dotnet Sim.Ui.dll args…</c> from the test's output directory (the PreviewToolProcessTests
    /// recipe), bounded by <paramref name="limit"/>: a hang is reported as non-termination (ADR-015 §7.1). D-052: with
    /// <see cref="ResumeLaunch.NoDialogVariable"/> set, so a refused resume never opens the native (modal) dialog.</summary>
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

    [Fact]
    public void TheUiResumeLaunch_PrintsTheCliDiagnostic_AndExitsLikeSimReplay_BeforeAnyWindowOpens()
    {
        string dir = Path.Combine(Path.GetTempPath(), $"s0-resume-launch-{Environment.ProcessId}-{Guid.NewGuid():N}");
        Directory.CreateDirectory(dir);
        try
        {
            WriteSession(dir, w => Forge("forged actor, delivered", w), out OrderLog forged, out _);
            string? cli = CliReplayDiagnostic(forged, ForgedSessionTurns);
            Assert.NotNull(cli);

            (int exit, string stdout, string stderr) = RunSimUi(TimeSpan.FromMinutes(3), "--resume", dir);
            // Cli.Guard: any exception is "error: <message>" on stderr and exit code 2.
            Assert.Equal(2, exit);
            Assert.Equal("error: " + cli, stderr.TrimEnd('\r', '\n'));
            Assert.DoesNotContain("resumed ", stdout, StringComparison.Ordinal);
        }
        finally
        {
            Directory.Delete(dir, recursive: true);
        }
    }

    // ---------------------------------------------------------------------------------------------------------
    // M5 playtest blocker (D-052) — THE --resume LAUNCH REPORTS EVERY REFUSAL, VISIBLY. ResumeLaunch.Attempt is what
    // Program.cs runs for --resume: the stderr report and exit code (2 for the ADR-034 order checks, exactly as
    // before; 3 for a session another build recorded), and the dialog callback (the native message box in the game,
    // a recorder here — never the native dialog in a test).
    // ---------------------------------------------------------------------------------------------------------

    /// <summary><see cref="WriteSession"/>'s clean session, rewritten as build 2dc6af2 on win-x64 recorded it: the
    /// manifest names that build and the trace's hashes are not this build's (the stand-in fixture,
    /// <see cref="ChronicleAndResumeTests.RecordTraceUnderAnotherSchema"/>, under the SAME schema).</summary>
    private static string WriteSessionOfAnotherBuild(string dir)
    {
        string manifest = WriteSession(dir, _ => null, out _, out _);
        ChronicleAndResumeTests.RecordBuild(manifest, "2dc6af2", "2026-10-11", "win-x64");
        string[] traces = Directory.GetFiles(dir, "trace-*.csv");
        Assert.Single(traces);
        ChronicleAndResumeTests.RecordTraceUnderAnotherSchema(traces[0]);
        return manifest;
    }

    [Fact]
    public void TheResumeLaunch_OfASessionAnotherBuildRecorded_ReportsTheIncompatibility_ToStderrAndTheDialog_WithExitCode3()
    {
        string dir = Path.Combine(Path.GetTempPath(), $"d052-launch-incompatible-{Environment.ProcessId}-{Guid.NewGuid():N}");
        Directory.CreateDirectory(dir);
        try
        {
            string manifest = WriteSessionOfAnotherBuild(dir);
            string[] before = ChronicleAndResumeTests.Fingerprint(dir);
            SessionIncompatibleException expected = Assert.Throws<SessionIncompatibleException>(() => UiSession.Resume(manifest, out _));

            var stderr = new StringWriter();
            var dialogs = new List<(string Title, string Message)>();
            UiSession? resumed = ResumeLaunch.Attempt(manifest, stderr, (t, m) => dialogs.Add((t, m)), out string log, out int exit);

            Assert.Null(resumed);
            Assert.Equal(ResumeLaunch.SessionIncompatibleExitCode, exit);
            Assert.Equal(3, exit);
            Assert.Equal("", log);
            string err = stderr.ToString();
            Assert.StartsWith("error: This saved session cannot be resumed with this build." + Environment.NewLine, err, StringComparison.Ordinal);
            var want = new System.Text.StringBuilder();
            want.Append("error: ").Append(expected.Lines[0]).Append(Environment.NewLine);
            for (int i = 1; i < expected.Lines.Count; i++) want.Append("  ").Append(expected.Lines[i]).Append(Environment.NewLine);
            Assert.Equal(want.ToString(), err);
            Assert.Contains("2dc6af2", err, StringComparison.Ordinal);
            Assert.Contains("start a new game", err, StringComparison.Ordinal);

            Assert.Single(dialogs);   // exactly once, with the same text
            Assert.Equal(ResumeLaunch.DialogTitle, dialogs[0].Title);
            Assert.Equal(expected.Message, dialogs[0].Message);
            Assert.Equal(before, ChronicleAndResumeTests.Fingerprint(dir));

            // A dialog that cannot be shown changes nothing: the stderr report and the exit code stand.
            var stderr2 = new StringWriter();
            Assert.Null(ResumeLaunch.Attempt(manifest, stderr2, (_, _) => throw new DllNotFoundException("SDL2"), out _, out int exit2));
            Assert.Equal(3, exit2);
            Assert.Equal(err, stderr2.ToString());
            // ...and no dialog callback at all (CIV_UI_NO_DIALOG) is the same report.
            var stderr3 = new StringWriter();
            Assert.Null(ResumeLaunch.Attempt(manifest, stderr3, null, out _, out int exit3));
            Assert.Equal(3, exit3);
            Assert.Equal(err, stderr3.ToString());
            Assert.Equal(before, ChronicleAndResumeTests.Fingerprint(dir));
        }
        finally
        {
            Directory.Delete(dir, recursive: true);
        }
    }

    [Fact]
    public void TheResumeLaunch_OfALogTheOrderChecksReject_StillPrintsTheCliDiagnosticWithExitCode2_AndNowShowsItToo()
    {
        string dir = Path.Combine(Path.GetTempPath(), $"d052-launch-rejected-{Environment.ProcessId}-{Guid.NewGuid():N}");
        Directory.CreateDirectory(dir);
        try
        {
            string manifest = WriteSession(dir, w => Forge("forged actor, delivered", w), out OrderLog forged, out _);
            string? cli = CliReplayDiagnostic(forged, ForgedSessionTurns);
            Assert.NotNull(cli);

            var stderr = new StringWriter();
            var dialogs = new List<(string Title, string Message)>();
            UiSession? resumed = ResumeLaunch.Attempt(manifest, stderr, (t, m) => dialogs.Add((t, m)), out _, out int exit);

            Assert.Null(resumed);
            Assert.Equal(2, exit);   // Cli.Guard's code, unchanged
            Assert.Equal("error: " + cli + Environment.NewLine, stderr.ToString());   // Cli.Guard's line, unchanged
            Assert.Single(dialogs);
            Assert.Equal(ResumeLaunch.DialogTitle, dialogs[0].Title);
            Assert.Equal(ResumeLaunch.OrderLogRejectedDialog(cli!), dialogs[0].Message);
            Assert.Contains(cli!, dialogs[0].Message, StringComparison.Ordinal);
        }
        finally
        {
            Directory.Delete(dir, recursive: true);
        }
    }

    [Fact]
    public void TheResumeLaunch_OfThisBuildsOwnSession_ResumesIt_WithNoReportAndNoDialog()
    {
        string dir = Path.Combine(Path.GetTempPath(), $"d052-launch-own-{Environment.ProcessId}-{Guid.NewGuid():N}");
        Directory.CreateDirectory(dir);
        try
        {
            string manifest = WriteSession(dir, _ => null, out _, out UiSession live);
            var stderr = new StringWriter();
            var dialogs = new List<(string Title, string Message)>();
            UiSession? resumed = ResumeLaunch.Attempt(manifest, stderr, (t, m) => dialogs.Add((t, m)), out string log, out int exit);

            Assert.NotNull(resumed);
            Assert.Equal(0, exit);
            Assert.Equal("", stderr.ToString());
            Assert.Empty(dialogs);
            Assert.Equal(Path.Combine(dir, "orders-20261010-120000-s256-n4.bin"), log);
            Assert.Equal(live.World.Clock.Turn, resumed.World.Clock.Turn);
            Assert.Equal(WorldHash.ComputeHex(live.World), WorldHash.ComputeHex(resumed.World));
            Assert.Equal(live.TraceLines.ToArray(), resumed.TraceLines.ToArray());
            Assert.Empty(resumed.ResumeNotice);
        }
        finally
        {
            Directory.Delete(dir, recursive: true);
        }
    }

    /// <summary>The real exe: Program.cs routes --resume through <see cref="ResumeLaunch"/>, so a session another build
    /// recorded ends the launch with the incompatibility report on stderr and exit code 3 — not an unhandled exception
    /// (exit 134 on Linux, as measured on f0427d2) — before any window opens, and the session files are unchanged.</summary>
    [Fact]
    public void TheUiResumeLaunch_OfASessionAnotherBuildRecorded_ExitsWith3AndTheReport_BeforeAnyWindowOpens()
    {
        string dir = Path.Combine(Path.GetTempPath(), $"d052-resume-launch-{Environment.ProcessId}-{Guid.NewGuid():N}");
        Directory.CreateDirectory(dir);
        try
        {
            WriteSessionOfAnotherBuild(dir);
            string[] before = ChronicleAndResumeTests.Fingerprint(dir);

            (int exit, string stdout, string stderr) = RunSimUi(TimeSpan.FromMinutes(3), "--resume", dir);
            Assert.Equal(3, exit);
            Assert.StartsWith("error: This saved session cannot be resumed with this build.", stderr, StringComparison.Ordinal);
            Assert.Contains("recorded by build 2dc6af2 (2026-10-11, win-x64)", stderr, StringComparison.Ordinal);
            Assert.Contains("a different world from turn 1,", stderr, StringComparison.Ordinal);
            Assert.DoesNotContain("Unhandled exception", stderr, StringComparison.Ordinal);
            Assert.DoesNotContain("resumed ", stdout, StringComparison.Ordinal);
            Assert.Equal(before, ChronicleAndResumeTests.Fingerprint(dir));
        }
        finally
        {
            Directory.Delete(dir, recursive: true);
        }
    }

    [Fact]
    public void SessionLogPath_StampedFlat_LexicographicIsChronological()
    {
        var early = new DateTime(2026, 7, 22, 9, 5, 0);
        var late = new DateTime(2026, 7, 22, 10, 0, 0);
        string a = Sim.Ui.UiSession.SessionLogPath(early);
        string b = Sim.Ui.UiSession.SessionLogPath(late);

        Assert.Equal(Path.Combine("runs", "orders-20260722-090500.bin"), a);
        Assert.True(string.CompareOrdinal(a, b) < 0, "lexicographic != chronological");

        // Non-canonical sizes are visible IN the name (replay needs --size PX).
        Assert.Equal(Path.Combine("runs", "orders-20260722-090500-s256.bin"),
            Sim.Ui.UiSession.SessionLogPath(early, sizeOverridePx: 256));
    }

    [Fact]
    public void UiArgs_Defaults_AreCanonical()
    {
        // Replay-fidelity surface: a silently-added default size override would
        // make every played session unreplayable at canonical size.
        (ulong seed, int? size, int? settlements, int? aiEmpires) = Sim.Ui.UiArgs.Parse([]);
        Assert.Equal(42UL, seed);
        Assert.Null(size);
        Assert.Null(settlements); // T2.3: canonical count unless explicitly overridden
        Assert.Null(aiEmpires);   // ADR-033 D5: worldgen.json's count (0) unless explicitly overridden

        (seed, size, settlements, aiEmpires) = Sim.Ui.UiArgs.Parse(
            ["--seed", "7", "--size", "256", "--settlements", "4", "--ai-empires", "2"]);
        Assert.Equal(7UL, seed);
        Assert.Equal(256, size);
        Assert.Equal(4, settlements);
        Assert.Equal(2, aiEmpires);

        // An explicit 0 is recorded as an override (the log name says -a0); a negative or non-numeric count is ignored.
        Assert.Equal(0, Sim.Ui.UiArgs.Parse(["--ai-empires", "0"]).AiEmpiresOverride);
        Assert.Null(Sim.Ui.UiArgs.Parse(["--ai-empires", "-1"]).AiEmpiresOverride);
        Assert.Null(Sim.Ui.UiArgs.Parse(["--ai-empires", "two"]).AiEmpiresOverride);
        Assert.Contains("--ai-empires N", Sim.Ui.UiArgs.Usage);
    }

    [Fact]
    public void TheAiEmpiresOption_FoundsThatManyAiEmpires_AndNamesTheLog()
    {
        (ulong seed, int? size, int? settlements, int? aiEmpires) = Sim.Ui.UiArgs.Parse(
            ["--size", "256", "--settlements", "4", "--ai-empires", "1"]);
        var session = Sim.Ui.UiSession.Start(seed, size, settlements, aiEmpires);
        int ai = 0;
        for (int i = 0; i < session.World.Polities.Count; i++)
            if (session.World.Polities[i].Source == CommandSource.Ai) ai++;
        Assert.Equal(1, ai);
        Assert.Equal(1, session.AiEmpiresOverride);
        Assert.EndsWith("-s256-n4-a1.bin", Sim.Ui.UiSession.SessionLogPath(new DateTime(2026, 10, 2, 12, 0, 0), size, settlements, aiEmpires));
    }

    [Fact]
    public void UiFounding_SizeOverrideBranch_EqualsCanonicalAtThatSize()
    {
        WorldState ui = Sim.Ui.UiFounding.Found(42, sizeOverridePx: 256, settlementsOverride: 4);
        Sim.Core.Worldgen.WorldgenConfig wg;
        using (var s = global::Sim.Data.DataFiles.OpenWorldgen())
            wg = Sim.Core.Worldgen.WorldgenConfigLoader.Load(s);
        // ADR-031: the canonical recipe is the CLI's six-stream load (CliRecipes), which founds
        // the warband formations; the UI now founds with the same recipe.
        Sim.Core.Systems.SimConfig sim;
        using (var s = global::Sim.Data.DataFiles.OpenSim())
        using (var n = global::Sim.Data.DataFiles.OpenNeeds())
        using (var g = global::Sim.Data.DataFiles.OpenGoods())
        using (var r = global::Sim.Data.DataFiles.OpenResearch())
        using (var a = global::Sim.Data.DataFiles.OpenAges())
        using (var f = global::Sim.Data.DataFiles.OpenUnitFamilies())
            sim = Sim.Core.Systems.SimConfigLoader.Load(s, n, g, r, a, f);
        WorldState canonical = Sim.Core.Worldgen.WorldFounding.Found(
            wg with { SizePx = 256 }, sim, 42, settlementsOverride: 4);
        Assert.Equal(WorldHash.ComputeHex(canonical), WorldHash.ComputeHex(ui));
    }
}

// T1.10: build identity — the string the director sees in title AND panel.
public class BuildInfoTests
{
    [Fact]
    public void Describe_LocalBuild_FallsBackToDevLocal()
    {
        // Test builds pass no -p:BuildSha/-p:BuildDate → the documented fallback.
        // MILESTONE LABEL: bumped M2 → M3 at T3.12 (the M3 exit artifact), M3 → M4 at the M4 exit paperwork (b40fad6), and M4 → M5 on
        // 2026-10-05 (M5 hardening, Director §13 — the M5 playtest builds still showed "civ-sim M4"). This
        // assert is deliberately EXACT rather than a prefix/contains match — the
        // director reads this string at every gate to know which build he is
        // holding, so a stale milestone label must fail the suite, not pass it
        // quietly. The label is the CURRENT milestone (docs/milestones.md, the roadmap rebase table): bump it
        // here and in BuildInfo.Milestone together whenever the current milestone changes.
        Assert.Equal("civ-sim M5 (dev, local)", Sim.Ui.BuildInfo.Describe());
        Assert.Equal("M5", Sim.Ui.BuildInfo.Milestone);
        Assert.Equal("dev", Sim.Ui.BuildInfo.Sha);
        Assert.Equal("local", Sim.Ui.BuildInfo.Date);
    }
}
