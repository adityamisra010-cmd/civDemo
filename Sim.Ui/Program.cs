using Sim.Core;
using Sim.Core.Kernel;
using Sim.Core.Systems;
using Sim.Core.Worldgen;

// sim-ui (T1.7/T1.8): found the canonical world, build the production executor
// and a fresh session order log, open the window. Worldgen runs before the
// window so the first frame already has terrain (~2 s at 1024²).
// Args: [--seed N] [--size PX] [--settlements N] [--ai-empires N] (size is the D-015 dev-preview escape
// hatch; --ai-empires founds N AI-commanded Empires to play against, ADR-033 D5). --help prints the usage.
(ulong seed, int? sizeOverride, int? settlementsOverride, int? aiEmpiresOverride) = Sim.Ui.UiArgs.Parse(args);
if (Array.IndexOf(args, "--help") >= 0 || Array.IndexOf(args, "-h") >= 0)
{
    Console.WriteLine(Sim.Ui.UiArgs.Usage);
    return;
}

// --export-session DIR|MANIFEST (ADR-059, D-066 A16): the one-file session export, headless — reads a saved session's six
// files and writes session-STAMP.xlsx beside them (the workbook the game writes at session close). No window, no world.
if (Sim.Ui.Export.SessionExportCommand.Wants(args))
{
    Environment.ExitCode = Sim.Ui.Export.SessionExportCommand.Run(args, Console.Out, Console.Error);
    return;
}

// --smoke [--ai-empires N] [--monkey N] [--report PATH] (M5 hardening H1): THE PLAYABILITY GATE, headless — a real
// native ImGui context and the game's real per-frame UI driven by scripted clicks, keys, wheel and drags over every
// visible control in every meaningful state, then a seeded monkey. No window, no GPU. Exit 0 = passed.
if (Sim.Ui.Headless.SmokeCommand.Wants(args))
{
    Environment.ExitCode = Sim.Ui.Headless.SmokeCommand.Run(args, aiEmpiresOverride);
    return;
}

// --audit-assets [root]: report which manifest keys resolve to REAL art,
// which are still stand-ins, and which files are orphaned. Headless, no window.
if (Array.IndexOf(args, "--audit-assets") >= 0)
{
    int at = Array.IndexOf(args, "--audit-assets");
    string auditRoot = at + 1 < args.Length && !args[at + 1].StartsWith("--")
        ? args[at + 1]
        : Sim.Ui.Art.AssetManifest.DefaultRoot();
    Console.Write(Sim.Ui.Art.AssetAudit.Run(auditRoot).Render());
    return;
}

// --generate-placeholder-assets (art substrate packet): writes any MISSING
// manifest asset as a programmatic stand-in and exits WITHOUT opening a
// window — the headless path that keeps assets/ populated in CI and in the
// repo. Existing files are never overwritten: the director's real art wins.
if (Array.IndexOf(args, "--generate-placeholder-assets") >= 0)
{
    int flag = Array.IndexOf(args, "--generate-placeholder-assets");
    string root = flag + 1 < args.Length && !args[flag + 1].StartsWith("--")
        ? args[flag + 1]
        : Sim.Ui.Art.AssetManifest.DefaultRoot();
    IReadOnlyList<string> written = Sim.Ui.Art.PlaceholderArt.GenerateMissing(root);
    Console.WriteLine($"assets root: {root}");
    foreach (string w in written) Console.WriteLine($"  generated {w}");
    Console.WriteLine(written.Count == 0
        ? "all manifest assets already present — nothing generated"
        : $"{written.Count} placeholder asset(s) generated");
    return;
}

// --research-preview [dir] (docs/architecture/research-tree-ui.md): paint the KNOWLEDGE &
// TECHNOLOGY progression screen from a REAL founded and stepped world to SVG and exit,
// without opening a window. docs/architecture/research-tree-ui/render-previews.sh turns the SVGs into PNGs.
if (Array.IndexOf(args, "--research-preview") >= 0)
{
    int previewAt = Array.IndexOf(args, "--research-preview");
    string previewDir = previewAt + 1 < args.Length && !args[previewAt + 1].StartsWith("--")
        ? args[previewAt + 1]
        : "research-preview";
    string? fontDir = Sim.Ui.Art.AssetManifest.PreviewFontDirectory();
    foreach (string p in Sim.Ui.Progression.ProgressionPreview.Run(previewDir, fontDir))
        Console.WriteLine($"research preview: {Path.GetFullPath(p)}");
    return;
}

// --age-preview [dir] (docs/architecture/age-and-world-ui.md): paint the capital Age panel, the
// ADVANCE AGE flow and the world lens at three zooms from a REAL seed-42 world played to Age
// eligibility through the real order pathway, to SVG, and exit.
if (Array.IndexOf(args, "--age-preview") >= 0)
{
    int at = Array.IndexOf(args, "--age-preview");
    string dir = at + 1 < args.Length && !args[at + 1].StartsWith("--") ? args[at + 1] : "age-preview";
    string? fonts = Sim.Ui.Art.AssetManifest.PreviewFontDirectory();
    foreach (string p in Sim.Ui.Ages.AgePreview.Run(dir, fonts))
        Console.WriteLine($"age preview: {Path.GetFullPath(p)}");
    return;
}

// --era-preview [dir] (docs/architecture/era-ui.md, ADR-033 D8): for each of the nine Ages, on the SAME
// stepped world with only the player's Age differing, paint the Technology tree, the capital Age panel
// and a chrome sample in that era's derived theme, to SVG, and exit.
if (Array.IndexOf(args, "--era-preview") >= 0)
{
    int at = Array.IndexOf(args, "--era-preview");
    string dir = at + 1 < args.Length && !args[at + 1].StartsWith("--") ? args[at + 1] : "era-ui-preview";
    string? fonts = Sim.Ui.Art.AssetManifest.PreviewFontDirectory();
    foreach (string p in Sim.Ui.Theme.EraPreview.Run(dir, fonts))
        Console.WriteLine($"era preview: {Path.GetFullPath(p)}");
    return;
}

// --e1-preview [dir] (M6 E1.6; docs/m6-plan/e1-visual-acceptance-checklist.md): the object layer at three zooms in three
// eras on the canonical world and the 300- and 800-settlement worlds, over each world's parchment bake, plus the parts sheet.
if (Array.IndexOf(args, "--e1-preview") >= 0)
{
    int at = Array.IndexOf(args, "--e1-preview");
    string dir = at + 1 < args.Length && !args[at + 1].StartsWith("--") ? args[at + 1] : "e1-preview";
    string? fonts = Sim.Ui.Art.AssetManifest.PreviewFontDirectory();
    foreach (string p in Sim.Ui.Headless.ObjectLayerPreview.Run(dir, fonts, line => Console.Error.WriteLine(line), quick: Array.IndexOf(args, "--quick") >= 0))
        Console.WriteLine($"e1 preview: {Path.GetFullPath(p)}");
    return;
}

// --action-preview [dir] (docs/architecture/action-surface.md, ADR-033 D1/D2): paint the REAL action surface
// — turn 1 on the canonical founded world, a later state (a crop, the Taxation civic and a road class known) at
// Age III and Age VIII, and a researched state at Age IV — as the game screen with the POLICY panel open and as the
// panel alone, to SVG. (Its handler follows --r2a-preview below.)
// --r2a-preview [dir] (docs/architecture/r2-previews/): turn 1, researched pre-Trade, post-Trade and a city-state.
if (Array.IndexOf(args, "--r2a-preview") >= 0)
{
    int at = Array.IndexOf(args, "--r2a-preview");
    string dir = at + 1 < args.Length && !args[at + 1].StartsWith("--") ? args[at + 1] : "r2-previews";
    string? fonts = Sim.Ui.Art.AssetManifest.PreviewFontDirectory();
    foreach (string p in Sim.Ui.Actions.R2aPreview.Run(dir, fonts))
        Console.WriteLine($"r2a preview: {Path.GetFullPath(p)}");
    return;
}
if (Array.IndexOf(args, "--action-preview") >= 0)
{
    int at = Array.IndexOf(args, "--action-preview");
    string dir = at + 1 < args.Length && !args[at + 1].StartsWith("--") ? args[at + 1] : "action-surface-preview";
    string? fonts = Sim.Ui.Art.AssetManifest.PreviewFontDirectory();
    foreach (string p in Sim.Ui.Actions.ActionSurfacePreview.Run(dir, fonts))
        Console.WriteLine($"action preview: {Path.GetFullPath(p)}");
    return;
}

// --resources-preview [dir] (M5 resources view): REAL frames of the game's UI (the headless harness on the one-AI world,
// three End Turns) rasterized to PNG — the status band's resource strip with a hover card, the band at the smallest window,
// and the RESOURCES view (stores, then the trend graph) — and exit.
if (Array.IndexOf(args, "--resources-preview") >= 0)
{
    int at = Array.IndexOf(args, "--resources-preview");
    string dir = at + 1 < args.Length && !args[at + 1].StartsWith("--") ? args[at + 1] : "resources-preview";
    foreach (string p in Sim.Ui.Headless.ResourcesPreview.Run(dir))
        Console.WriteLine($"resources preview: {Path.GetFullPath(p)}");
    return;
}

// --player-views-preview [dir] (ADR-033 D9): paint the SETTLEMENT, EMPIRE and INSTITUTIONS player views from
// real sessions (turn 1 at Age I; a developed Age III state with a university) to SVG and exit.
if (Array.IndexOf(args, "--player-views-preview") >= 0)
{
    int at = Array.IndexOf(args, "--player-views-preview");
    string dir = at + 1 < args.Length && !args[at + 1].StartsWith("--") ? args[at + 1] : "player-views-preview";
    string? fonts = Sim.Ui.Art.AssetManifest.PreviewFontDirectory();
    foreach (string p in Sim.Ui.ViewModel.PlayerViewsPreview.Run(dir, fonts))
        Console.WriteLine($"player views preview: {Path.GetFullPath(p)}");
    return;
}

// Founding, executor recipe, order stamping and log persistence all live in
// UiSession/UiFounding (T1.9) — pinned by the founding- and replay-equivalence
// tests. Wall-clock stamps are legal here (outside the determinism surface);
// the log CONTENT records sim turns only.
// Audit E32 / D-008: --resume <session-dir | session-*.json> re-founds the world from the manifest and
// replays the saved order log (with every check against the saved trace, schema permitting), then continues play into the
// SAME session files. The manifest and the forensic run record are not rewritten: they describe the
// session's founding, which a resume does not change.
// S0 (ADR-034 §4 adopted): the replay runs the CLI's order checks, and a log they reject ends the launch with the
// diagnostic and exit code `sim replay` gives (Cli.Guard: "error: <message>", 2) — before any window opens. A session
// recorded under another schema is resumed by replay without the trace check (D-008) and says so, here and in-game.
// M5 playtest blocker (D-052): a session another build recorded under THIS schema, whose replay here diverges from its
// saved trace, is refused as incompatible — "error: This saved session cannot be resumed with this build." and the
// reasons on stderr, exit code 3 — instead of ending the launch with an unhandled exception. Sim.Ui is a GUI exe whose
// stderr a Windows player never sees, so both refusals are also shown in a native error dialog (best effort).
if (Sim.Ui.UiArgs.ResumePath(args) is { } resumeArg)
{
    // D-057: a --resume argument naming no session manifest (a missing directory, one without a session-*.json) is
    // reported like every refusal — the line, the dialog, exit 2 — not an unhandled exception.
    string manifestPath;
    try
    {
        manifestPath = Sim.Ui.UiArgs.ResolveManifest(resumeArg);
    }
    catch (IOException e)
    {
        Environment.ExitCode = Sim.Ui.ResumeLaunch.ReportUnreadable(
            new Sim.Ui.SessionUnreadableException(resumeArg, "manifest", e), Console.Error, Sim.Ui.ResumeLaunch.NativeDialog());
        return;
    }
    Sim.Ui.UiSession? resumed = Sim.Ui.ResumeLaunch.Attempt(
        manifestPath, Console.Error, Sim.Ui.ResumeLaunch.NativeDialog(), out string resumedLog, out int refusal);
    if (resumed is null)
    {
        Environment.ExitCode = refusal;
        return;
    }
    resumed.KeepRawTelemetry = Sim.Ui.UiArgs.KeepRawTelemetry(args);   // ADR-059 amendment 2026-10-29
    Console.WriteLine($"resumed {manifestPath} at turn {resumed.TurnsPlayed}");
    foreach (string line in resumed.ResumeNotice) Console.WriteLine($"  {line}");
    using var resumedGame = new Sim.Ui.SimUiGame(resumed, resumedLog, Sim.Ui.UiArgs.Developer(args), Sim.Ui.UiArgs.UserScale(args));
    resumedGame.Run();
    return;
}

// Batch 3 WS3 (W1 fix F1): the fresh start goes through NewGameLaunch, so a --size the cylinder refuses (worldgen.json
// topology.wrapX: a size the lattice stride does not divide) ends the launch with "error: <message>" on stderr, the
// native dialog and exit code 2 — Cli.Guard's — before any window opens, instead of an unhandled exception.
Sim.Ui.UiSession? session = Sim.Ui.NewGameLaunch.Attempt(
    seed, sizeOverride, settlementsOverride, aiEmpiresOverride, Console.Error, Sim.Ui.ResumeLaunch.NativeDialog(), out int startRefusal);
if (session is null)
{
    Environment.ExitCode = startRefusal;
    return;
}
session.KeepRawTelemetry = Sim.Ui.UiArgs.KeepRawTelemetry(args);   // ADR-059 amendment 2026-10-29
string sessionLogPath = Sim.Ui.UiSession.SessionLogPath(DateTime.Now, sizeOverride, settlementsOverride, aiEmpiresOverride);

// UR-7 (M5 polish, 2026-10-06): the game is CONSTRUCTED first — its window exists but is not shown until Run — so the
// manifest can record the display the session opens on (window, display, UI scale, DPI; provenance only).
using var game = new Sim.Ui.SimUiGame(session, sessionLogPath, Sim.Ui.UiArgs.Developer(args), Sim.Ui.UiArgs.UserScale(args));

// T4.17: the manifest is written HERE, before the window is shown and before a
// single turn is played. A session that ends in a crash, a force-quit or a
// power cut is still reproducible, because the one fact that cannot be
// recovered afterwards — the seed — is already on disk. Everything else the
// session writes is appended as it goes; this is written once and never
// rewritten.
session.ExportManifest(
    DateTime.Now.ToString("yyyy-MM-dd HH:mm:ss", System.Globalization.CultureInfo.InvariantCulture),
    sessionLogPath, game.OpeningDisplay);
Console.WriteLine($"session manifest: {Sim.Ui.UiSession.ManifestPath(sessionLogPath)}");

// m4-forensic P1: the run record, written beside the manifest and for the same
// reason — the identity of the run is on disk before a turn is played. The local
// stamp is handed over (this is Sim.Ui; the clock is legal here, ADR-009) but it
// is NOT the identity: the runId is derived from seed + overrides + schema +
// config digest + orders digest, so it is reproducible and checkable.
session.ExportForensicRun(
    DateTime.Now.ToString("yyyy-MM-dd HH:mm:sszzz", System.Globalization.CultureInfo.InvariantCulture),
    sessionLogPath);
Console.WriteLine($"forensic record: {Sim.Ui.UiSession.ForensicPath(sessionLogPath)}"
    + $"  (run {session.ForensicRunId})");

game.Run();