using Sim.Core;
using Sim.Core.Kernel;
using Sim.Core.Systems;
using Sim.Core.Worldgen;

// sim-ui (T1.7/T1.8): found the canonical world, build the production executor
// and a fresh session order log, open the window. Worldgen runs before the
// window so the first frame already has terrain (~2 s at 1024²).
// Args: [--seed N] [--size PX] (size is the D-015 dev-preview escape hatch).
(ulong seed, int? sizeOverride, int? settlementsOverride) = Sim.Ui.UiArgs.Parse(args);

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
    string fontDir = Path.Combine(Sim.Ui.Art.AssetManifest.DefaultRoot(), "fonts");
    foreach (string p in Sim.Ui.Progression.ProgressionPreview.Run(previewDir, Directory.Exists(fontDir) ? fontDir : null))
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
    string fonts = Path.Combine(Sim.Ui.Art.AssetManifest.DefaultRoot(), "fonts");
    foreach (string p in Sim.Ui.Ages.AgePreview.Run(dir, Directory.Exists(fonts) ? fonts : null))
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
    string fonts = Path.Combine(Sim.Ui.Art.AssetManifest.DefaultRoot(), "fonts");
    foreach (string p in Sim.Ui.Theme.EraPreview.Run(dir, Directory.Exists(fonts) ? fonts : null))
        Console.WriteLine($"era preview: {Path.GetFullPath(p)}");
    return;
}

// Founding, executor recipe, order stamping and log persistence all live in
// UiSession/UiFounding (T1.9) — pinned by the founding- and replay-equivalence
// tests. Wall-clock stamps are legal here (outside the determinism surface);
// the log CONTENT records sim turns only.
var session = Sim.Ui.UiSession.Start(seed, sizeOverride, settlementsOverride);
string sessionLogPath = Sim.Ui.UiSession.SessionLogPath(DateTime.Now, sizeOverride, settlementsOverride);

// T4.17: the manifest is written HERE, before the window opens and before a
// single turn is played. A session that ends in a crash, a force-quit or a
// power cut is still reproducible, because the one fact that cannot be
// recovered afterwards — the seed — is already on disk. Everything else the
// session writes is appended as it goes; this is written once and never
// rewritten.
session.ExportManifest(
    DateTime.Now.ToString("yyyy-MM-dd HH:mm:ss", System.Globalization.CultureInfo.InvariantCulture),
    sessionLogPath);
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

using var game = new Sim.Ui.SimUiGame(session, sessionLogPath);
game.Run();