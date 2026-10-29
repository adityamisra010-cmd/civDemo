using System.Globalization;

namespace Sim.Ui.Export;

/// <summary>
/// ADR-059 / D-066 A16 — <c>sim-ui --export-session DIR|MANIFEST</c>: the one-file session export, headless (no window, no
/// GPU, no world). It reads a saved session's six files and writes <c>session-STAMP.xlsx</c> beside them — the same
/// <see cref="SessionWorkbook"/> the game writes at session close, so a session closed by a crash (or played before this
/// build) is exported the same way. Exit 0 = written (and self-checked); 2 = refused, with the named reason on stderr.
/// </summary>
public static class SessionExportCommand
{
    public const string Flag = "--export-session";

    public static bool Wants(string[] args) => Array.IndexOf(args, Flag) >= 0;

    public static int Run(string[] args, TextWriter output, TextWriter error)
    {
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
            (string path, SessionExportResult r) = SessionWorkbook.ExportBeside(manifest);
            output.WriteLine("session workbook: " + Path.GetFullPath(path));
            output.WriteLine("  " + r.Bytes.Length.ToString(CultureInfo.InvariantCulture) + " bytes, sha256 " + r.Sha256);
            output.WriteLine("  telemetry: " + r.TelemetryLines.ToString(CultureInfo.InvariantCulture) + " turn(s), codebook "
                + (r.Codebooks.Count == 0 ? "(none)" : string.Join(", ", r.Codebooks)) + ", continuation rows "
                + r.ContinuationRows.ToString(CultureInfo.InvariantCulture) + ", longest cell "
                + r.LongestCell.ToString(CultureInfo.InvariantCulture) + " characters");
            output.WriteLine("  self-check: every cell read back, every telemetry cell decoded to its line byte for byte");
            return 0;
        }
        catch (SessionExportException e)
        {
            error.WriteLine("error: session export refused - " + e.Message);
            return 2;
        }
        catch (IOException e)
        {
            error.WriteLine("error: " + e.Message);
            return 2;
        }
    }
}
