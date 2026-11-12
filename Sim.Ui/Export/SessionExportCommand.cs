using System.Globalization;

namespace Sim.Ui.Export;

/// <summary>
/// ADR-059 / D-066 A16 — <c>sim-ui --export-session DIR|MANIFEST</c>: the one-file session export, headless (no window, no
/// GPU, no world). It reads a saved session's six files and writes <c>session-STAMP.xlsx</c> beside them — the same
/// <see cref="SessionWorkbook"/> the game writes at session close, so a session closed by a crash (or played before this
/// build) is exported the same way. Exit 0 = written (and self-checked); 2 = not written — refused, or failed — with the
/// reason on stderr and, once the session's manifest is found, appended to its export log (D-086 DE2).
///
/// ADR-059 amendment 2026-10-29: this command never removes a file (only the game, at session close, removes the raw
/// telemetry log after a verified export). On a session whose raw log was removed, the telemetry is read back from the
/// existing workbook and used only when it matches the forensic close record's sha256; otherwise the export is refused
/// (<see cref="SessionExportException.TelemetryUnrecoverable"/>) and the existing workbook is left as it is.
/// </summary>
public static class SessionExportCommand
{
    public const string Flag = "--export-session";

    public static bool Wants(string[] args) => Array.IndexOf(args, Flag) >= 0;

    /// <summary>Runs the command. <paramref name="export"/> is a TEST SEAM standing in for
    /// <see cref="SessionWorkbook.ExportBeside"/> (null: the real export); the launcher never passes it.</summary>
    public static int Run(string[] args, TextWriter output, TextWriter error,
        Func<string, (string Path, SessionExportResult Result)>? export = null)
    {
        export ??= m => SessionWorkbook.ExportBeside(m);
        int at = Array.IndexOf(args, Flag);
        if (at + 1 >= args.Length || args[at + 1].StartsWith("--", StringComparison.Ordinal))
        {
            error.WriteLine("error: " + Flag + " needs a session directory or a session-*.json manifest");
            return 2;
        }
        string manifest;
        try
        {
            manifest = UiArgs.ResolveManifest(args[at + 1]);
        }
        catch (IOException e)
        {
            error.WriteLine("error: " + e.Message);
            return 2;
        }
        try
        {
            (string path, SessionExportResult r) = export(manifest);
            output.WriteLine("session workbook: " + Path.GetFullPath(path));
            output.WriteLine("  " + r.Bytes.Length.ToString(CultureInfo.InvariantCulture) + " bytes, sha256 " + r.Sha256);
            output.WriteLine("  telemetry: " + r.TelemetryLines.ToString(CultureInfo.InvariantCulture) + " turn(s), codebook "
                + (r.Codebooks.Count == 0 ? "(none)" : string.Join(", ", r.Codebooks)) + ", continuation rows "
                + r.ContinuationRows.ToString(CultureInfo.InvariantCulture) + ", longest cell "
                + r.LongestCell.ToString(CultureInfo.InvariantCulture) + " characters");
            output.WriteLine("  self-check: every cell read back, every telemetry cell decoded to its line byte for byte");
            if (r.RawTelemetry == RawTelemetry.RecoveredFromWorkbook)
                output.WriteLine("  telemetry: " + r.TelemetryFile + " was removed after a verified export; its lines were read back from "
                    + "the existing workbook and match the forensic close record's sha256 " + r.TelemetrySha256);
            return 0;
        }
        catch (SessionExportException e)
        {
            error.WriteLine("error: session export refused - " + e.Message);
            LogRefusal(manifest, "export-session refused - " + e.Message);
            return 2;
        }
        catch (IOException e)
        {
            error.WriteLine("error: " + e.Message);
            LogRefusal(manifest, "export-session failed - " + e.Message);
            return 2;
        }
        catch (Exception e)
        {
            // D-086 verification round 1 (RR-R1-04): ANY other failure — a record file this account may not read
            // (UnauthorizedAccessException), or a defect — is reported like an I/O failure, on stderr and in the session's
            // export log, so the recovery note holds: when no workbook appears, the log's last line says why. Before, it left
            // the command as an unhandled exception, which a window application shows nowhere.
            string why = e.GetType().Name + ": " + e.Message;
            error.WriteLine("error: session export failed - " + why);
            LogRefusal(manifest, "export-session failed - " + why);
            return 2;
        }
    }

    /// <summary>D-086 DE2: the session's export log beside its manifest — <c>session-STAMP.json</c> → <c>export-STAMP.log</c>,
    /// the file the game's close appends to (UiSession.ExportLogPath) — or null for a manifest not named that way.</summary>
    public static string? ExportLogFor(string manifestPath)
    {
        string name = Path.GetFileName(manifestPath);
        if (!name.StartsWith("session-", StringComparison.Ordinal) || !name.EndsWith(".json", StringComparison.Ordinal)) return null;
        return Path.Combine(Path.GetDirectoryName(Path.GetFullPath(manifestPath)) ?? "",
            "export-" + name["session-".Length..^".json".Length] + ".log");
    }

    /// <summary>D-086 DE2: a refused or failed export is also appended to the session's export log (a line: the local time,
    /// then the reason) — on Windows the game is a window application, so <c>start /wait "" "app\Sim.Ui.exe" --export-session
    /// runs</c> shows no stderr, and the record folder then says why no workbook appeared. A written workbook adds no line.
    /// Never throws: a log that cannot be written is skipped (the reason is already on stderr).</summary>
    private static void LogRefusal(string manifestPath, string reason)
    {
        if (ExportLogFor(manifestPath) is not { } log || !Directory.Exists(Path.GetDirectoryName(log))) return;
        try
        {
            File.AppendAllText(log, DateTime.Now.ToString("yyyy-MM-dd HH:mm:sszzz", CultureInfo.InvariantCulture) + " " + reason + "\n");
        }
        catch (Exception e) when (e is IOException or UnauthorizedAccessException)
        {
            // the reason is on stderr; a log that cannot be written changes nothing else
        }
    }
}
