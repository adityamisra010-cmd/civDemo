namespace Sim.Ui.ViewModel;

/// <summary>
/// T4.19 lane B (MORE) — the five files of the played session, by role, so the
/// director can find them without reading UiSession. Paths are the session's
/// own twinning rules (UiSession.*Path), never re-derived here.
/// </summary>
public static class SessionFilesModel
{
    public static IReadOnlyList<string> Lines(string sessionLogPath)
    {
        ArgumentNullException.ThrowIfNull(sessionLogPath);
        return
        [
            "session files (beside the order log, same stamp):",
            "  manifest   " + UiSession.ManifestPath(sessionLogPath),
            "  orders     " + sessionLogPath,
            "  chronicle  " + UiSession.ChroniclePath(sessionLogPath),
            "  trace      " + UiSession.TracePath(sessionLogPath),
            "  telemetry  " + UiSession.TelemetryPath(sessionLogPath),
            "  replay: sim replay / sim inspect --telemetry rebuild every record from the manifest and the orders",
        ];
    }
}
