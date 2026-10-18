using System.Globalization;
using Sim.Core.Kernel;

namespace Sim.Ui;

/// <summary>
/// M5 playtest blocker (D-052): the trace check of <see cref="UiSession.ReplayTo"/> failed — the replayed world's hash
/// at <see cref="Turn"/> is not the one the saved trace recorded for that turn. The message is EXACTLY the text the
/// check has always thrown ("resume diverged at turn N: replay hash X, saved trace Y"); the type is what is new, so a
/// caller can tell a divergence from the other refusals of a replay (the non-turn-0 guard, which stays a plain
/// <see cref="InvalidOperationException"/>; the ADR-034 order checks' <see cref="OrderValidationException"/>).
/// Derives from <see cref="InvalidOperationException"/>, what the check threw before, so no existing catch changes.
/// </summary>
public sealed class ResumeDivergedException(long turn, string replayHash, string savedHash)
    : InvalidOperationException(
        "resume diverged at turn " + turn.ToString(CultureInfo.InvariantCulture)
        + ": replay hash " + replayHash + ", saved trace " + savedHash)
{
    /// <summary>The first replayed turn whose hash is not the saved trace's.</summary>
    public long Turn { get; } = turn;

    /// <summary>The hash this build's replay computed for <see cref="Turn"/>.</summary>
    public string ReplayHash { get; } = replayHash;

    /// <summary>The hash the saved trace recorded for <see cref="Turn"/>.</summary>
    public string SavedHash { get; } = savedHash;
}

/// <summary>
/// M5 playtest blocker (D-052) — A SESSION THIS BUILD CANNOT REBUILD. The session was recorded under THIS build's
/// canonical schema (a session recorded under another schema is resumed by the D-008 recovery path instead, with its
/// notice — <see cref="UiSession.SchemaMismatchNotice"/>), but replaying its orders here does not reproduce its saved
/// trace: the simulation changed between the build that recorded it and this one without a schema bump (the audit's
/// case: a session from playtest build 2dc6af2 resumed by f0427d2, both v32, diverging at turn 1). Its world cannot
/// be rebuilt, so <see cref="UiSession.Resume"/> refuses it with this exception instead of the bare
/// <see cref="ResumeDivergedException"/> (kept as <see cref="Exception.InnerException"/>), which used to end the
/// launch as an unhandled exception.
///
/// The <see cref="Exception.Message"/> is for the PLAYER: <see cref="Lines"/>, short and actionable — which build
/// recorded the session and which build this is, from which turn the worlds differ, what to do (run the recording
/// build, or start a new game), and that the session files were not changed (a refused resume only ever READS them).
/// Only when the two builds call themselves the same does it add a line on what that means: a determinism defect to
/// report, or — when the session was recorded on another platform — the expected platform divergence (ADR-022).
/// </summary>
public sealed class SessionIncompatibleException : InvalidOperationException
{
    public SessionIncompatibleException(
        string manifestPath,
        string recordedBuildSha, string recordedBuildDate, string recordedPlatform,
        string currentBuildSha, string currentBuildDate, string currentPlatform,
        ResumeDivergedException diverged)
        : this(manifestPath, recordedBuildSha, recordedBuildDate, recordedPlatform,
            currentBuildSha, currentBuildDate, currentPlatform, diverged,
            Describe(recordedBuildSha, recordedBuildDate, recordedPlatform,
                currentBuildSha, currentBuildDate, currentPlatform, (diverged ?? throw new ArgumentNullException(nameof(diverged))).Turn))
    {
    }

    private SessionIncompatibleException(
        string manifestPath,
        string recordedBuildSha, string recordedBuildDate, string recordedPlatform,
        string currentBuildSha, string currentBuildDate, string currentPlatform,
        ResumeDivergedException diverged, IReadOnlyList<string> lines)
        : base(string.Join("\n", lines), diverged)
    {
        ManifestPath = manifestPath;
        RecordedBuildSha = recordedBuildSha;
        RecordedBuildDate = recordedBuildDate;
        RecordedPlatform = recordedPlatform;
        CurrentBuildSha = currentBuildSha;
        CurrentBuildDate = currentBuildDate;
        CurrentPlatform = currentPlatform;
        Turn = diverged.Turn;
        Lines = lines;
    }

    /// <summary>The manifest of the refused session.</summary>
    public string ManifestPath { get; }

    /// <summary>The build that recorded the session, as its manifest says.</summary>
    public string RecordedBuildSha { get; }
    public string RecordedBuildDate { get; }
    public string RecordedPlatform { get; }

    /// <summary>This build (<see cref="BuildInfo"/>) and the platform it runs on.</summary>
    public string CurrentBuildSha { get; }
    public string CurrentBuildDate { get; }
    public string CurrentPlatform { get; }

    /// <summary>The first turn whose replayed world differs from the saved one.</summary>
    public long Turn { get; }

    /// <summary>The player-facing explanation, one short line each; <see cref="Exception.Message"/> is these joined
    /// by '\n'.</summary>
    public IReadOnlyList<string> Lines { get; }

    /// <summary>The player-facing lines for a session recorded by (<paramref name="recordedSha"/>,
    /// <paramref name="recordedDate"/>, <paramref name="recordedPlatform"/>) whose replay by this build
    /// (<paramref name="currentSha"/>, <paramref name="currentDate"/>, <paramref name="currentPlatform"/>) differs
    /// from turn <paramref name="turn"/>. Culture-invariant.</summary>
    public static IReadOnlyList<string> Describe(
        string recordedSha, string recordedDate, string recordedPlatform,
        string currentSha, string currentDate, string currentPlatform, long turn)
    {
        var lines = new List<string>
        {
            "This saved session cannot be resumed with this build.",
            "It was recorded by build " + recordedSha + " (" + recordedDate + ", " + recordedPlatform + "); this is build "
                + currentSha + " (" + currentDate + ", " + currentPlatform + ").",
            "Replaying its orders here produces a different world from turn " + turn.ToString(CultureInfo.InvariantCulture)
                + ", so the saved game cannot be rebuilt.",
            "To continue it, run the build that recorded it. To play this build, start a new game.",
            "The session files were not changed.",
        };
        // The same build id on both sides: either this build recorded it — a determinism defect, unless it was
        // recorded on another platform, where a replay is expected to diverge (ADR-022, CR-013 §8) — or another build
        // that calls itself the same: every unstamped local build (and every CLI build) is "dev", which identifies
        // nothing, so the line says so rather than letting "dev" pass for an identity.
        if (string.Equals(recordedSha, currentSha, StringComparison.Ordinal))
            lines.Add(SessionManifest.IsPlatformRecorded(recordedPlatform) && !SamePlatform(recordedPlatform, currentPlatform)
                ? "If this build recorded it, the likely cause is the platform: a session replays exactly only on the platform that recorded it (ADR-022)."
                : string.Equals(recordedSha, LocalBuildSha, StringComparison.Ordinal)
                    ? "Both are unstamped local builds (\"dev\"), so which build recorded it is not known; if this one did, that is a determinism defect: please report it with the session folder."
                    : "If this build recorded it, that is a determinism defect: please report it with the session folder.");
        return lines;
    }

    /// <summary>The build id of an unstamped local build — <see cref="BuildInfo.Sha"/>'s fallback, and the CLI's.</summary>
    private const string LocalBuildSha = "dev";

    /// <summary>The same platform for the replay promise: the same RID, or the two RIDs CR-013 §8.2 measured equal to
    /// the reference (<see cref="SessionManifest.IsReferencePlatform"/>: "linux-x64" and "ubuntu.24.04-x64").</summary>
    private static bool SamePlatform(string a, string b) =>
        string.Equals(a, b, StringComparison.Ordinal)
        || (SessionManifest.IsReferencePlatform(a) && SessionManifest.IsReferencePlatform(b));
}

/// <summary>
/// M5 playtest blocker (D-052) — THE --resume LAUNCH, as a testable seam (the UiSession precedent: Program.cs only
/// calls it). A refused resume used to be visible only as console text, or not at all: the ADR-034 refusal printed
/// "error: &lt;message&gt;" to stderr, and a session another build recorded ended the launch with an UNHANDLED exception.
/// Sim.Ui is a GUI-subsystem exe, so on Windows — launched from Explorer, the zip's launcher or cmd.exe — neither
/// reaches the player. Both refusals now go through one reporter, before any window opens: the stderr report (the
/// first line prefixed "error: ", like Cli.Guard), the exit code, and the injected <c>dialog</c> callback
/// (<see cref="NativeErrorDialog.Show"/> from Program.cs; a recorder in the tests; never the native dialog in a test
/// or in --smoke). The stderr report is written FIRST, so a dialog that cannot be shown leaves it standing.
/// </summary>
public static class ResumeLaunch
{
    /// <summary>The ADR-034 order checks rejected the saved log: the code `sim replay` exits with (Cli.Guard).</summary>
    public const int OrderLogRejectedExitCode = 2;

    /// <summary>The session was recorded by a build whose world this build does not reproduce.</summary>
    public const int SessionIncompatibleExitCode = 3;

    /// <summary>Batch 3 WS3 (W1 fix F1): this build cannot create the session's world at all — worldgen refused its
    /// configuration (a cylinder at a size the lattice stride does not divide: a session recorded at <c>--size 250</c>
    /// by the pre-cylinder build). Cli.Guard's code, which `sim replay --founded --size 250` exits with too.</summary>
    public const int WorldRefusedExitCode = 2;

    /// <summary>The title of the refusal dialog.</summary>
    public const string DialogTitle = "civ-sim: this saved session cannot be resumed";

    /// <summary>Set (to anything non-empty) in the environment of a Sim.Ui process that must not open the native
    /// refusal dialog: the tests that launch the real exe, which would otherwise block on a modal dialog wherever a
    /// display exists.</summary>
    public const string NoDialogVariable = "CIV_UI_NO_DIALOG";

    /// <summary>The dialog callback Program.cs hands to <see cref="Attempt"/>: the native message box, or null when
    /// <see cref="NoDialogVariable"/> is set.</summary>
    public static Action<string, string>? NativeDialog() =>
        string.IsNullOrEmpty(Environment.GetEnvironmentVariable(NoDialogVariable)) ? NativeErrorDialog.Show : null;

    /// <summary>The dialog text for a log the ADR-034 order checks rejected (stderr keeps the bare diagnostic).</summary>
    public static string OrderLogRejectedDialog(string diagnostic) =>
        "This saved session cannot be resumed: its order log was rejected by this build's order checks.\n"
        + diagnostic + "\n"
        + "The session files were not changed.";

    /// <summary>The dialog text for a session whose world this build's worldgen refuses (stderr keeps the bare
    /// diagnostic).</summary>
    public static string WorldRefusedDialog(string diagnostic) =>
        "This saved session cannot be resumed: this build cannot create its world.\n"
        + diagnostic + "\n"
        + "The session files were not changed.";

    /// <summary>
    /// Resumes the session <paramref name="manifestPath"/> names (<see cref="UiSession.Resume"/>). On success: the
    /// session, exit code 0, nothing written to <paramref name="stderr"/>, no dialog. On a refusal: null, the
    /// refusal reported to <paramref name="stderr"/> and to <paramref name="dialog"/> (once), and its exit code —
    /// <see cref="OrderLogRejectedExitCode"/> with "error: &lt;message&gt;" exactly as before for an
    /// <see cref="OrderValidationException"/>, <see cref="SessionIncompatibleExitCode"/> with "error: " + the first
    /// of <see cref="SessionIncompatibleException.Lines"/> and the rest indented for a
    /// <see cref="SessionIncompatibleException"/>; <see cref="WorldRefusedExitCode"/> with "error: &lt;message&gt;" for
    /// a <see cref="Sim.Core.Worldgen.WorldgenConfigException"/> (batch 3 WS3: the session's size is one a cylinder
    /// refuses). Any other exception propagates unchanged. A dialog callback that throws is ignored: the stderr report
    /// and the exit code stand.
    /// </summary>
    public static UiSession? Attempt(
        string manifestPath, TextWriter stderr, Action<string, string>? dialog, out string sessionLogPath, out int exitCode)
    {
        ArgumentNullException.ThrowIfNull(stderr);
        try
        {
            UiSession session = UiSession.Resume(manifestPath, out sessionLogPath);
            exitCode = 0;
            return session;
        }
        catch (OrderValidationException e)
        {
            // S0 (ADR-034 §4): Cli.Guard's line and code, unchanged.
            stderr.WriteLine($"error: {e.Message}");
            ShowDialog(dialog, OrderLogRejectedDialog(e.Message));
            exitCode = OrderLogRejectedExitCode;
        }
        catch (SessionIncompatibleException e)
        {
            stderr.WriteLine("error: " + e.Lines[0]);
            for (int i = 1; i < e.Lines.Count; i++) stderr.WriteLine("  " + e.Lines[i]);
            ShowDialog(dialog, e.Message);
            exitCode = SessionIncompatibleExitCode;
        }
        catch (Sim.Core.Worldgen.WorldgenConfigException e)
        {
            // Batch 3 WS3 (W1 fix F1): before this catch, a session recorded at a size a cylinder refuses ended the
            // launch as an unhandled exception. Reported like Cli.Guard: the line, then the code.
            stderr.WriteLine($"error: {e.Message}");
            ShowDialog(dialog, WorldRefusedDialog(e.Message));
            exitCode = WorldRefusedExitCode;
        }
        sessionLogPath = "";
        return null;
    }

    private static void ShowDialog(Action<string, string>? dialog, string message) => ShowDialog(dialog, DialogTitle, message);

    /// <summary>Best effort: a dialog callback that throws is ignored, the stderr report stands. Shared with
    /// <see cref="NewGameLaunch"/>.</summary>
    internal static void ShowDialog(Action<string, string>? dialog, string title, string message)
    {
        if (dialog is null) return;
        try { dialog(title, message); }
        catch (Exception) { }   // best effort: the stderr report stands
    }
}

/// <summary>
/// Batch 3 WS3 (W1 fix F1) — THE FRESH LAUNCH, as a testable seam (the <see cref="ResumeLaunch"/> precedent:
/// Program.cs only calls it). With worldgen.json topology.wrapX on, <see cref="Sim.Core.Worldgen.Worldgen.Generate"/>
/// refuses a <c>--size</c> the lattice stride does not divide; that refusal used to end the launch as an UNHANDLED
/// exception (exit 134 on Linux, nothing a Windows player sees). It now goes through the same reporter as a refused
/// resume, before any window opens: "error: &lt;message&gt;" on stderr (Cli.Guard's line), the exit code
/// (<see cref="WorldRefusedExitCode"/>, Cli.Guard's 2), and the injected dialog callback once.
/// </summary>
public static class NewGameLaunch
{
    /// <summary>Worldgen refused the world's configuration: Cli.Guard's code (`sim run --founded --size 250` exits with it).</summary>
    public const int WorldRefusedExitCode = 2;

    /// <summary>The title of the refusal dialog.</summary>
    public const string DialogTitle = "civ-sim: this world cannot be created";

    /// <summary>The dialog text for a world this build's worldgen refuses (stderr keeps the bare diagnostic).</summary>
    public static string WorldRefusedDialog(string diagnostic) =>
        "This world cannot be created with these launch options.\n" + diagnostic;

    /// <summary>
    /// Starts a new session (<see cref="UiSession.Start"/>). On success: the session, exit code 0, nothing written to
    /// <paramref name="stderr"/>, no dialog. On a <see cref="Sim.Core.Worldgen.WorldgenConfigException"/>: null,
    /// "error: &lt;message&gt;" on <paramref name="stderr"/>, <see cref="WorldRefusedDialog"/> to
    /// <paramref name="dialog"/> (once) and <see cref="WorldRefusedExitCode"/>. Any other exception propagates unchanged.
    /// </summary>
    public static UiSession? Attempt(
        ulong seed, int? sizeOverridePx, int? settlementsOverride, int? aiEmpiresOverride,
        TextWriter stderr, Action<string, string>? dialog, out int exitCode)
    {
        ArgumentNullException.ThrowIfNull(stderr);
        try
        {
            UiSession session = UiSession.Start(seed, sizeOverridePx, settlementsOverride, aiEmpiresOverride);
            exitCode = 0;
            return session;
        }
        catch (Sim.Core.Worldgen.WorldgenConfigException e)
        {
            stderr.WriteLine($"error: {e.Message}");
            ResumeLaunch.ShowDialog(dialog, DialogTitle, WorldRefusedDialog(e.Message));
            exitCode = WorldRefusedExitCode;
            return null;
        }
    }
}
