using System.Globalization;
using System.Text;
using Sim.Core.Kernel;
using Sim.Core.Observability.Forensic;

namespace Sim.Ui.Export;

/// <summary>What one export produced. <paramref name="RawTelemetry"/> is the raw log's state as the README states it
/// (<see cref="Export.RawTelemetry.RemovedAfterExport"/> only when the log may be removed: present, rebuilt exactly by
/// its decoded lines, and listed with the same sha256 by the forensic close record); <paramref name="TelemetrySha256"/>
/// is the sha256 of the telemetry bytes the workbook holds (null when it holds none).</summary>
public sealed record SessionExportResult(
    byte[] Bytes,
    string Sha256,
    IReadOnlyList<SheetModel> Sheets,
    int TelemetryLines,
    int ContinuationRows,
    int LongestCell,
    IReadOnlyList<string> Codebooks,
    RawTelemetry RawTelemetry = RawTelemetry.Kept,
    string TelemetryFile = "",
    string? TelemetrySha256 = null);

/// <summary>
/// ADR-059 amendment (Director playtest 2026-10-29) — what becomes of the raw telemetry log, <c>telemetry-*.jsonl</c>
/// (the largest session file by far: 18.5 MB for 150 turns, against a 1.8 MB workbook before compression). The workbook's
/// README says which applies.
/// </summary>
public enum RawTelemetry
{
    /// <summary>The raw log stays beside the workbook: the headless export, and the game with
    /// <c>--keep-raw-telemetry</c>.</summary>
    Kept,
    /// <summary>The game at session close: the raw log is removed once this workbook is written, read back from disk
    /// and verified (<see cref="SessionWorkbook.RemoveRawTelemetry"/>); the Telemetry sheet is then its only copy.</summary>
    RemovedAfterExport,
    /// <summary>The raw log was removed by an earlier verified export; its bytes were rebuilt from that workbook's
    /// Telemetry sheet and equal, by sha256, the bytes the forensic close record lists.</summary>
    RecoveredFromWorkbook,
}

/// <summary>
/// ADR-059 (D-066 A14–A17) — THE ONE-FILE SESSION EXPORT: <c>session-STAMP.xlsx</c>, written beside the six session
/// files, holding all of them in eight sheets — README, Manifest, Orders (decoded), Trace, Chronicle, Telemetry (column A
/// the turn, column B one coded cell per turn), Codebook, Forensic (with every file's sha256) — so a played session is
/// one upload (docs/m6-session-export/README.md).
///
/// SOURCES: THE RECORD FILES ONLY. The manifest (SessionManifest.Read), the order log (OrderLog.Load), the trace
/// (SessionTrace.Parse), the chronicle text, the telemetry JSONL and the forensic record (ForensicRecordFile.Parse). The
/// exporter never replays, never re-founds a world and reads no simulation state: what the workbook says is what the
/// files say. A file the manifest names but the directory does not hold is said to be missing, in its sheet, by the code
/// NP — never invented.
///
/// CODED (ADR-059 amendment 2026-10-29, Director): only the README and the Codebook carry words. Every other sheet holds
/// codes, numbers and identifiers, its column headers included; the Codebook's legend defines every code
/// (<see cref="WorkbookCodes"/>), and a text a file holds that is neither a code nor an identifier is a QT code whose
/// text is in the legend. The chronicle is coded at its source (Sim.Core.Chronicle.ChronicleCode).
///
/// NEVER TRUNCATED, NEVER RAW. A coded turn over Excel's 32,767 characters is split at a <c>|</c> into continuation rows
/// (D-066 A17: column A empty, the cell starting with <c>&gt;</c>); a sheet over 1,048,576 rows is a named refusal. Before
/// a byte is written the exporter reads its own workbook back (ZipArchive + XmlReader), checks every cell is the text it
/// wrote, re-joins the continuation rows and decodes every telemetry cell to its source line byte for byte; any
/// difference is a <see cref="SessionExportException"/> and no workbook is written.
///
/// DETERMINISTIC. The same six files give the same workbook bytes on every machine (no clock, no culture, no dictionary
/// iteration, no float; <see cref="OoxmlWorkbook"/> for the container). Reading the six files changes none of them: the
/// export runs after the forensic close record, which therefore neither lists the workbook nor is altered by it.
/// </summary>
public static class SessionWorkbook
{
    public const string ExporterVersion = "session-export/v1";

    /// <summary>The sheets, in order.</summary>
    public static readonly string[] SheetNames = ["README", "Manifest", "Orders", "Trace", "Chronicle", "Telemetry", "Codebook", "Forensic"];

    /// <summary>The workbook path beside a manifest: <c>session-STAMP.json</c> → <c>session-STAMP.xlsx</c>.</summary>
    public static string WorkbookPathFor(string manifestPath) => Path.ChangeExtension(manifestPath, ".xlsx");

    /// <summary>Exports the session whose manifest is <paramref name="manifestPath"/> to <see cref="WorkbookPathFor"/>.
    /// The file is written only after the self-check passed, in one write.
    ///
    /// ADR-059 amendment 2026-10-29 — A SESSION WHOSE RAW TELEMETRY LOG WAS REMOVED. When the manifest's telemetry file
    /// is not beside it but a workbook is, the telemetry is read back from that workbook's Telemetry sheet (every cell
    /// decoded to its line) and used only if the rebuilt bytes have the sha256 the forensic close record lists for the
    /// file; otherwise the export is the named refusal <see cref="SessionExportException.TelemetryUnrecoverable"/> and
    /// the existing workbook is left untouched — never replaced by one without the telemetry.</summary>
    public static (string Path, SessionExportResult Result) ExportBeside(string manifestPath, RawTelemetry raw = RawTelemetry.Kept)
    {
        string full = Path.GetFullPath(manifestPath);
        string dir = Path.GetDirectoryName(full) ?? "";
        string outPath = WorkbookPathFor(full);
        Func<string, byte[]?> read = name => ReadBeside(dir, name);
        if (RecoverRemovedTelemetry(full, dir, outPath) is ({ } telemetryName, { } recovered))
        {
            read = name => name == telemetryName ? recovered : ReadBeside(dir, name);
            raw = RawTelemetry.RecoveredFromWorkbook;
        }
        SessionExportResult result = Build(Path.GetFileName(full), read, raw: raw);
        File.WriteAllBytes(outPath, result.Bytes);
        return (outPath, result);
    }

    /// <summary>The telemetry file's bytes rebuilt from its decoded lines: each line followed by '\n' (the writer's
    /// form, TelemetryWriter.WriteTurn); no line, no byte.</summary>
    public static byte[] TelemetryBytesOf(IReadOnlyList<string> lines)
    {
        if (lines.Count == 0) return [];
        var sb = new StringBuilder();
        foreach (string line in lines) sb.Append(line).Append('\n');
        return new UTF8Encoding(false).GetBytes(sb.ToString());
    }

    /// <summary>The raw telemetry log a verified export removed, rebuilt from the workbook beside the manifest — or null
    /// when there is nothing to recover (the manifest names no telemetry file, the file is present, or there is no
    /// workbook). Throws <see cref="SessionExportException.TelemetryUnrecoverable"/> when a workbook is there but its
    /// Telemetry sheet does not give back the bytes the forensic close record lists.</summary>
    private static (string Name, byte[] Bytes)? RecoverRemovedTelemetry(string manifestPath, string dir, string workbookPath)
    {
        SessionManifest manifest;
        try
        {
            using FileStream f = File.OpenRead(manifestPath);
            manifest = SessionManifest.Read(f, manifestPath);
        }
        catch (Exception e) when (e is not SessionExportException)
        {
            return null;   // Build names the refusal (manifest-unreadable)
        }
        string name = manifest.TelemetryFile;
        if (name.Length == 0 || ReadBeside(dir, name) is not null || !File.Exists(workbookPath)) return null;

        string unrecoverable = name + " is not beside the manifest (a verified export removes it) and ";
        string keep = " The existing workbook " + Path.GetFileName(workbookPath) + " is left as it is; to export without the "
            + "telemetry, move it away first.";
        string? listed = null;
        if (manifest.ForensicFile.Length > 0 && ReadBeside(dir, manifest.ForensicFile) is { } fBytes)
        {
            try
            {
                ForensicRecordFile fr = ForensicRecordFile.Parse(
                    Lines(new FileEntry("forensic", manifest.ForensicFile, fBytes)), manifest.ForensicFile);
                if (fr.Close is { } c)
                    foreach (ArtifactRef a in c.Artifacts)
                        if (a.Role == "telemetry" && a.File == name) listed = a.Sha256;
            }
            catch (Exception e) when (e is not SessionExportException)
            {
                listed = null;
            }
        }
        if (listed is null)
            throw new SessionExportException(SessionExportException.TelemetryUnrecoverable, unrecoverable
                + "no forensic close record lists its sha256, so the workbook's Telemetry sheet cannot be checked against it." + keep);
        byte[] bytes;
        try
        {
            OoxmlWorkbook.Content back = OoxmlWorkbook.Read(File.ReadAllBytes(workbookPath));
            int t = -1;
            for (int i = 0; i < back.SheetNames.Count; i++)
                if (back.SheetNames[i] == "Telemetry") t = i;
            if (t < 0) throw new InvalidDataException("the workbook has no Telemetry sheet");
            IReadOnlyList<string> cells = JoinTelemetry(back.Sheets[t]);
            var lines = new List<string>(cells.Count);
            foreach (string cell in cells) lines.Add(TelemetryCellCodec.Decode(cell));
            bytes = TelemetryBytesOf(lines);
        }
        catch (Exception e) when (e is not OutOfMemoryException)
        {
            throw new SessionExportException(SessionExportException.TelemetryUnrecoverable, unrecoverable
                + "the workbook's Telemetry sheet does not decode (" + e.Message + ")." + keep, e);
        }
        string sha = ForensicIdentity.Sha256Hex(bytes);
        if (sha != listed)
            throw new SessionExportException(SessionExportException.TelemetryUnrecoverable, unrecoverable
                + "the workbook's Telemetry sheet decodes to bytes with sha256 " + sha + ", not the " + listed
                + " the forensic close record lists." + keep);
        return (name, bytes);
    }

    /// <summary>
    /// ADR-059 amendment 2026-10-29 — DROP THE RAW TELEMETRY LOG AFTER A VERIFIED EXPORT (the game, at session close).
    /// Removes <c>telemetry-*.jsonl</c> only when every one of these holds, and returns null; otherwise keeps it and
    /// returns why:
    /// <list type="number">
    /// <item>the export decided the log may go (<see cref="RawTelemetry.RemovedAfterExport"/>: present, rebuilt exactly
    /// by its decoded lines, and listed with the same sha256 by the forensic close record) — and the export's self-check
    /// passed, since <see cref="Build"/> returns no result otherwise;</item>
    /// <item>the workbook on disk is, byte for byte, the workbook that was self-checked;</item>
    /// <item>the log on disk is, by sha256, the log the workbook holds.</item>
    /// </list>
    /// The manifest, orders, trace, chronicle and forensic files are never touched (resume reads them).
    /// </summary>
    public static string? RemoveRawTelemetry(string workbookPath, SessionExportResult result)
    {
        if (result.RawTelemetry != RawTelemetry.RemovedAfterExport)
            return "the export did not verify it for removal (" + result.RawTelemetry.ToString() + ")";
        string telemetryPath = Path.Combine(Path.GetDirectoryName(Path.GetFullPath(workbookPath)) ?? "", result.TelemetryFile);
        try
        {
            if (!File.ReadAllBytes(workbookPath).AsSpan().SequenceEqual(result.Bytes))
                return "the workbook on disk is not the workbook that was verified";
            if (ForensicIdentity.Sha256HexOfFile(telemetryPath) != result.TelemetrySha256)
                return "the raw log on disk is not the log the workbook holds";
            File.Delete(telemetryPath);
            return null;
        }
        catch (Exception e) when (e is IOException or UnauthorizedAccessException)
        {
            return e.Message;
        }
    }

    private static byte[]? ReadBeside(string dir, string name)
    {
        if (name.Length == 0 || name.IndexOfAny(['/', '\\']) >= 0) return null;   // a bare file name beside the manifest
        string p = Path.Combine(dir, name);
        return File.Exists(p) ? File.ReadAllBytes(p) : null;
    }

    private sealed record FileEntry(string Role, string Name, byte[]? Bytes, string? Recovered = null)
    {
        public bool Present => Bytes is not null;
        public string Sha256 => Bytes is null ? "" : ForensicIdentity.Sha256Hex(Bytes);
    }

    /// <summary>Builds and self-checks the workbook from the files <paramref name="read"/> returns by name (null =
    /// not present). <paramref name="cellLimit"/> is Excel's, lowered only by tests of the continuation rule.
    ///
    /// <paramref name="raw"/> (ADR-059 amendment 2026-10-29) is what the README says becomes of the raw telemetry log;
    /// <see cref="RawTelemetry.Kept"/> (the default) writes exactly the README of before the amendment.
    /// <see cref="RawTelemetry.RemovedAfterExport"/> falls back to <see cref="RawTelemetry.Kept"/> unless the log is
    /// present, its decoded lines rebuild it byte for byte and the forensic close record lists it with the same sha256;
    /// <see cref="RawTelemetry.RecoveredFromWorkbook"/> is refused unless that last check holds.</summary>
    public static SessionExportResult Build(string manifestName, Func<string, byte[]?> read, int cellLimit = OoxmlWorkbook.MaxCellChars,
        RawTelemetry raw = RawTelemetry.Kept)
    {
        if (cellLimit is < 16 or > OoxmlWorkbook.MaxCellChars) throw new ArgumentOutOfRangeException(nameof(cellLimit));

        // ---- the manifest: required (it names every other file)
        byte[] mBytes = read(manifestName)
            ?? throw new SessionExportException(SessionExportException.ManifestUnreadable, manifestName + " is not present");
        SessionManifest manifest;
        JVal mJson;
        try
        {
            using (var ms = new MemoryStream(mBytes, writable: false)) manifest = SessionManifest.Read(ms, manifestName);
            mJson = JsonText.Parse(Utf8(mBytes, manifestName));
        }
        catch (Exception e) when (e is not SessionExportException)
        {
            throw new SessionExportException(SessionExportException.ManifestUnreadable, manifestName + ": " + e.Message, e);
        }

        FileEntry[] files =
        [
            new("manifest", manifestName, mBytes),
            Entry("orders", manifest.OrdersFile, read),
            Entry("trace", manifest.TraceFile, read),
            Entry("chronicle", manifest.ChronicleFile, read),
            Entry("telemetry", manifest.TelemetryFile, read, raw == RawTelemetry.RecoveredFromWorkbook
                ? "recovered from the earlier workbook's Telemetry sheet" : null),
            Entry("forensic", manifest.ForensicFile, read),
        ];
        FileEntry orders = files[1], trace = files[2], chronicle = files[3], telemetry = files[4], forensic = files[5];

        // ---- read every present companion with its own reader (a file that does not parse is a named refusal)
        OrderLog? log = null;
        if (orders.Present)
            log = Guard(orders, () =>
            {
                using var ms = new MemoryStream(orders.Bytes!, writable: false);
                return OrderLog.Load(ms);
            });
        string[]? traceLines = trace.Present ? Lines(trace) : null;
        if (traceLines is not null) Guard(trace, () => SessionTrace.Parse(traceLines, trace.Name));
        string[]? chronicleLines = chronicle.Present ? Lines(chronicle) : null;
        string[]? telemetryLines = telemetry.Present ? Lines(telemetry) : null;
        string[]? forensicLines = forensic.Present ? Lines(forensic) : null;
        ForensicRecordFile? forensicFile = forensicLines is null ? null : Guard(forensic, () => ForensicRecordFile.Parse(forensicLines, forensic.Name));

        // ---- Telemetry: one coded cell per line, self-checked, split under A17
        var telemetrySheet = new SheetModel("Telemetry", [8, 200]);
        telemetrySheet.Rows.Add(["T", "TC"]);
        var codebooks = new List<string>();
        var schemas = new List<string>();
        int continuation = 0, longest = 0;
        string firstTurn = "", lastTurn = "";
        if (telemetryLines is not null)
        {
            for (int i = 0; i < telemetryLines.Length; i++)
            {
                string line = telemetryLines[i];
                string where = telemetry.Name + " line " + (i + 1).ToString(CultureInfo.InvariantCulture);
                if (line.Length == 0)
                    throw new SessionExportException(SessionExportException.RecordUnreadable, where + " is empty");
                TelemetryCodebook cb = Wrap(where, () => TelemetryCellCodec.CodebookOf(line));
                if (!codebooks.Contains(cb.Version)) codebooks.Add(cb.Version);
                string turn = Wrap(where, () => TurnOf(line, schemas));
                string cell = Wrap(where, () => TelemetryCellCodec.EncodeChecked(line, where));
                IReadOnlyList<string> parts = TelemetryCellCodec.Split(cell, cellLimit, cb.ContinuationMark);
                if (i == 0) firstTurn = turn;
                lastTurn = turn;
                telemetrySheet.Rows.Add([turn, parts[0]]);
                for (int k = 1; k < parts.Count; k++) telemetrySheet.Rows.Add(["", parts[k]]);
                continuation += parts.Count - 1;
                foreach (string p in parts) longest = Math.Max(longest, p.Length);
            }
        }

        // ---- the raw log's fate (ADR-059 amendment 2026-10-29): removable only when the workbook can stand in for it
        string? listedTelemetrySha = null;
        if (forensicFile?.Close is { } close)
            foreach (ArtifactRef a in close.Artifacts)
                if (a.Role == "telemetry" && a.File == telemetry.Name) listedTelemetrySha = a.Sha256;
        if (raw == RawTelemetry.RecoveredFromWorkbook && (!telemetry.Present || listedTelemetrySha != telemetry.Sha256))
            throw new SessionExportException(SessionExportException.TelemetryUnrecoverable,
                telemetry.Name + ": the recovered telemetry is not the file the forensic close record lists");
        if (raw == RawTelemetry.RemovedAfterExport
            && !(telemetry.Present && listedTelemetrySha == telemetry.Sha256
                 && TelemetryBytesOf(telemetryLines!).AsSpan().SequenceEqual(telemetry.Bytes)))
            raw = RawTelemetry.Kept;

        // ---- the coded sheets, built in sheet order so the QT codes number by first use; the Codebook (the legend) last
        var texts = new WorkbookCodes.Texts();
        var names = new List<(string Code, string Name)>();
        SheetModel manifestSheet = KeyValueSheet("Manifest", [("", mJson)], cellLimit, texts);
        SheetModel ordersSheet = OrdersSheet(orders, log, forensicFile, texts);
        SheetModel traceSheet = TraceSheet(trace, traceLines, forensicFile, texts);
        SheetModel chronicleSheet = ChronicleSheet(chronicle, chronicleLines, forensicFile, cellLimit, texts, names);
        if (telemetryLines is null) telemetrySheet.Rows.Add(Missing(telemetry, forensicFile, texts));
        SheetModel forensicSheet = ForensicSheet(forensic, forensicLines, forensicFile, files, cellLimit, texts);
        SheetModel codebookSheet = CodebookSheet(codebooks, names, texts, cellLimit);
        var sheets = new List<SheetModel>
        {
            Readme(manifest, manifestName, files, schemas, codebooks, telemetryLines?.Length ?? 0, firstTurn, lastTurn, continuation, raw,
                chronicleLines),
            manifestSheet, ordersSheet, traceSheet, chronicleSheet, telemetrySheet, codebookSheet, forensicSheet,
        };

        byte[] bytes = OoxmlWorkbook.Write(sheets);
        SelfCheck(bytes, sheets, telemetryLines);
        return new SessionExportResult(bytes, ForensicIdentity.Sha256Hex(bytes), sheets, telemetryLines?.Length ?? 0,
            continuation, longest, codebooks, raw, telemetry.Name, telemetry.Present ? telemetry.Sha256 : null);
    }

    // ------------------------------------------------------------------------------------- the self-check --

    private static void SelfCheck(byte[] bytes, IReadOnlyList<SheetModel> sheets, string[]? telemetryLines)
    {
        OoxmlWorkbook.Content back;
        try
        {
            back = OoxmlWorkbook.Read(bytes);
        }
        catch (Exception e) when (e is not SessionExportException)
        {
            throw new SessionExportException(SessionExportException.WorkbookReadBack, "the workbook does not read back: " + e.Message, e);
        }
        if (back.SheetNames.Count != sheets.Count)
            throw new SessionExportException(SessionExportException.WorkbookReadBack, "sheet count differs");
        for (int s = 0; s < sheets.Count; s++)
        {
            if (back.SheetNames[s] != sheets[s].Name)
                throw new SessionExportException(SessionExportException.WorkbookReadBack, "sheet " + s.ToString(CultureInfo.InvariantCulture) + " name differs");
            IReadOnlyList<string[]> rows = back.Sheets[s];
            if (rows.Count != sheets[s].Rows.Count)
                throw new SessionExportException(SessionExportException.WorkbookReadBack, sheets[s].Name + ": row count differs");
            for (int r = 0; r < rows.Count; r++)
                if (!SameRow(rows[r], sheets[s].Rows[r]))
                    throw new SessionExportException(SessionExportException.WorkbookReadBack,
                        $"{sheets[s].Name} row {(r + 1).ToString(CultureInfo.InvariantCulture)} reads back differently");
        }
        if (telemetryLines is null || telemetryLines.Length == 0) return;
        int t = Array.IndexOf(SheetNames, "Telemetry");
        IReadOnlyList<string> cells = JoinTelemetry(back.Sheets[t]);
        if (cells.Count != telemetryLines.Length)
            throw new SessionExportException(SessionExportException.RoundTripMismatch,
                $"the Telemetry sheet holds {cells.Count} turns, the file {telemetryLines.Length}");
        for (int i = 0; i < cells.Count; i++)
        {
            string decoded = TelemetryCellCodec.Decode(cells[i]);
            if (!string.Equals(decoded, telemetryLines[i], StringComparison.Ordinal))
                throw new SessionExportException(SessionExportException.RoundTripMismatch,
                    $"telemetry line {(i + 1).ToString(CultureInfo.InvariantCulture)}: the workbook's cell decodes to a different line");
        }
    }

    /// <summary>The Telemetry sheet's coded cells, continuation rows joined (D-066 A17): a row whose column A is empty
    /// and whose cell starts with the marker is appended, marker dropped, to the cell above. Row 1 is the header.</summary>
    public static IReadOnlyList<string> JoinTelemetry(IReadOnlyList<string[]> rows, string mark = ">")
    {
        var cells = new List<string>();
        for (int r = 1; r < rows.Count; r++)
        {
            string a = rows[r].Length > 0 ? rows[r][0] : "";
            string b = rows[r].Length > 1 ? rows[r][1] : "";
            if (a.Length == 0 && b.StartsWith(mark, StringComparison.Ordinal))
            {
                if (cells.Count == 0) throw new SessionExportException(SessionExportException.CellUndecodable, "a continuation row has no cell above it");
                cells[^1] += b[mark.Length..];
            }
            else cells.Add(b);
        }
        return cells;
    }

    private static bool SameRow(string[] read, string[] model)
    {
        int n = model.Length;
        while (n > 0 && string.IsNullOrEmpty(model[n - 1])) n--;
        if (read.Length != n) return false;
        for (int i = 0; i < n; i++)
            if (!string.Equals(read[i], model[i] ?? "", StringComparison.Ordinal)) return false;
        return true;
    }

    // ------------------------------------------------------------------------------------------- sheets --

    private static SheetModel Readme(SessionManifest m, string manifestName, FileEntry[] files, List<string> schemas,
        List<string> codebooks, int turns, string firstTurn, string lastTurn, int continuation, RawTelemetry raw, string[]? chronicleLines)
    {
        var s = new SheetModel("README", [22, 140]);
        void R(string a, string b) => s.Rows.Add([a, b]);
        R("Session workbook", "One played session in one file: the six session files as eight sheets (D-066 A14-A17; ADR-059). "
            + (raw == RawTelemetry.Kept
                ? "The six files stay the record (resume reads them); this workbook is a reading copy written beside them, from them alone."
                : "The manifest, orders, trace, chronicle and forensic files stay the record (resume reads them, and rebuilds the "
                  + "telemetry by replay); the raw telemetry log is removed once a workbook holding it is verified, so the Telemetry "
                  + "sheet of this workbook is its copy (see 'file telemetry' below)."));
        R("exporter", ExporterVersion);
        R("session manifest", manifestName);
        R("seed", m.Seed.ToString(CultureInfo.InvariantCulture));
        R("build", m.BuildSha + " (" + m.BuildDate + ")");
        R("platform", m.Platform);
        R("canonical schema", "v" + m.SchemaVersion.ToString(CultureInfo.InvariantCulture));
        R("started at", m.StartedAt);
        R("telemetry schema", schemas.Count == 0 ? "(no telemetry)" : string.Join(", ", schemas));
        R("codebook", codebooks.Count == 0 ? "(none: no telemetry)" : string.Join(", ", codebooks));
        R("turns in telemetry", turns == 0 ? "0" : turns.ToString(CultureInfo.InvariantCulture) + " (turn " + firstTurn + " to turn " + lastTurn + ")");
        R("codes", "Only this sheet and the Codebook carry words (ADR-059 amendment 2026-10-29). Every other sheet holds codes, numbers "
            + "and identifiers (file names, hashes, version tags, JSON keys), its column headers included. The Codebook defines "
            + "every code: first the telemetry codebook, then the legend (column A the section - all, type, Manifest:col, "
            + "Orders:col, Orders:kind, Orders:read, Trace:col, Chronicle:col, Chronicle:event, Chronicle:field, Telemetry:col, "
            + "Forensic:col, Forensic:file - column B the code, column D its meaning), then 'Chronicle:names' (what the chronicle's named codes "
            + "name, e.g. ST12 = a settlement's name) and 'QT:texts' (the exact text of each QT code: a text a session file holds that "
            + "is neither a code nor an identifier).");
        R("EMPTY", "EMPTY never means 0 - it means the codebook default (the Codebook sheet, column 'default when the field is EMPTY').");
        R("cells", "Every cell is text. Numbers are the writer's exact text; a leading 0. is written . and -0. is written -.; "
            + "N, I and -I are NaN, Infinity and -Infinity. Order amounts (formatted here, from the binary log) follow the same rule.");
        R("continuation", "A cell holds at most 32767 characters. A longer coded turn is split at a | into further rows: a continuation "
            + "row has an EMPTY column A and its cell starts with > followed by the | that was cut. Join: drop the > and append the rest "
            + "to the cell above. This workbook has " + continuation.ToString(CultureInfo.InvariantCulture) + " continuation row(s).");
        R("sheet README", "This sheet.");
        R("sheet Manifest", "session-*.json as KY key / VL value / TY JSON type rows (nested keys joined with '.', array items as [i]).");
        R("sheet Orders", "orders-*.bin decoded: one row per order in log order - I index, T turn, PL issuing polity, K kind number, "
            + "KC kind code, TG target id, TD target decoded, AM amount, AD amount decoded, AB the amount's exact 64-bit pattern.");
        R("sheet Trace", "trace-*.csv as is, its header coded: T turn, Y year, PO population, SN settlements, FD food, H world hash.");
        R("sheet Chronicle", "chronicle-*.txt: one coded event per row (L its number, EV the event line); the file's name lines are the "
            + "Codebook's 'Chronicle:names' rows. "
            + (chronicleLines is null ? "This session's chronicle file is not present."
                : chronicleLines.Length > 0 && chronicleLines[0] == Sim.Core.Chronicle.ChronicleCode.FormatTag
                    ? "This file is coded (" + Sim.Core.Chronicle.ChronicleCode.FormatTag + ")."
                    : "This file was written before the coded format (prose): each line is shown as its QT code, its text in 'QT:texts'."));
        R("sheet Telemetry", "telemetry-*.jsonl: column T the turn, column TC the whole turn as one coded cell (README of the export: "
            + "docs/m6-session-export/README.md). Each cell decodes back to its line byte for byte (checked before this file was written).");
        R("sheet Codebook", "The legend: the encoding table every coded telemetry cell is read with (one codebook per telemetry schema), "
            + "then every code the other sheets use, the names of the chronicle's named codes and the texts of the QT codes.");
        R("sheet Forensic", "forensic-*.jsonl: the run and close records as KY / VL / TY rows, then the XF table: every session file "
            + "with its sha256 now and as the close record lists it.");
        foreach (FileEntry f in files)
            R("file " + f.Role, f.Name.Length == 0 ? "not named by the manifest"
                : f.Role == "telemetry" && f.Present && raw != RawTelemetry.Kept ? TelemetryFileLine(f, raw)
                : f.Present ? f.Name + " - present, " + f.Bytes!.Length.ToString(CultureInfo.InvariantCulture) + " bytes, sha256 " + f.Sha256
                : f.Name + " - NOT PRESENT beside the manifest; its sheet says so (NP) and nothing is invented");
        return s;
    }

    /// <summary>The README's telemetry file line when the raw log does not stay (ADR-059 amendment 2026-10-29).</summary>
    private static string TelemetryFileLine(FileEntry f, RawTelemetry raw) =>
        f.Name + (raw == RawTelemetry.RemovedAfterExport ? " - present at export, " : " - " + f.Recovered + ", ")
        + f.Bytes!.Length.ToString(CultureInfo.InvariantCulture) + " bytes, sha256 " + f.Sha256
        + " (the sha256 the forensic close record lists). Its whole content is the Telemetry sheet: every cell decodes to its "
        + "line byte for byte, and the lines, each followed by a line feed, are the file. "
        + (raw == RawTelemetry.RemovedAfterExport
            ? "The raw log was REMOVED after this workbook was written and verified (session close; sim-ui --keep-raw-telemetry keeps it). "
            : "The raw log had been REMOVED after an earlier workbook was verified; this one was exported from that workbook's copy. ")
        + "With the build that recorded the session, sim inspect --manifest <manifest> --telemetry <file> also rebuilds it by replay.";

    private static SheetModel KeyValueSheet(string name, (string Prefix, JVal Value)[] roots, int cellLimit, WorkbookCodes.Texts texts)
    {
        var s = new SheetModel(name, [44, 120, 10]);
        s.Rows.Add(["KY", "VL", "TY"]);
        foreach ((string prefix, JVal value) in roots) Flatten(s, prefix, value, cellLimit, texts);
        return s;
    }

    /// <summary>A JSON value as KY / VL / TY rows: the key path, the value (a string through
    /// <see cref="WorkbookCodes.Texts.Value"/>, a bool 1/0, null ~) and the type code S N B O A Z.</summary>
    private static void Flatten(SheetModel s, string key, JVal v, int cellLimit, WorkbookCodes.Texts texts)
    {
        switch (v)
        {
            case JObj o when o.Keys.Count > 0:
                for (int i = 0; i < o.Keys.Count; i++)
                    Flatten(s, key.Length == 0 ? o.Keys[i] : key + "." + o.Keys[i], o.Values[i], cellLimit, texts);
                break;
            case JArr a when a.Items.Count > 0:
                for (int i = 0; i < a.Items.Count; i++)
                    Flatten(s, key + "[" + i.ToString(CultureInfo.InvariantCulture) + "]", a.Items[i], cellLimit, texts);
                break;
            case JObj:
                AddValueRow(s, [key, "{}", "O"], cellLimit);
                break;
            case JArr:
                AddValueRow(s, [key, "[]", "A"], cellLimit);
                break;
            case JStr str:
                AddValueRow(s, [key, texts.Value(str.Value), "S"], cellLimit);
                break;
            case JNum n:
                AddValueRow(s, [key, n.Text, "N"], cellLimit);
                break;
            case JBool b:
                AddValueRow(s, [key, b.Value ? "1" : "0", "B"], cellLimit);
                break;
            default:
                AddValueRow(s, [key, "~", "Z"], cellLimit);
                break;
        }
    }

    /// <summary>A row whose column-B text may exceed the cell limit: the excess goes to A17 continuation rows (column A
    /// empty, the cell starting with the marker), split between characters (never inside a surrogate pair). An empty
    /// key is the whole document, <c>$</c>.</summary>
    private static void AddValueRow(SheetModel s, string[] row, int cellLimit)
    {
        if (row[0].Length == 0) row[0] = "$";
        AddSplitRow(s, row, 1, cellLimit);
    }

    /// <summary>A row whose column <paramref name="col"/> may exceed the cell limit: the excess goes to continuation rows
    /// whose column A is empty and whose column <paramref name="col"/> starts with the marker &gt;.</summary>
    private static void AddSplitRow(SheetModel s, string[] row, int col, int cellLimit)
    {
        string b = row[col];
        if (b.Length <= cellLimit)
        {
            s.Rows.Add(row);
            return;
        }
        int cut = SafeCut(b, 0, cellLimit);
        row[col] = b[..cut];
        s.Rows.Add(row);
        while (cut < b.Length)
        {
            int next = SafeCut(b, cut, cellLimit - 1);
            var more = new string[col + 1];
            for (int i = 0; i < col; i++) more[i] = "";
            more[col] = ">" + b[cut..next];
            s.Rows.Add(more);
            cut = next;
        }
    }

    private static int SafeCut(string s, int from, int max)
    {
        int end = Math.Min(s.Length, from + max);
        if (end < s.Length && end > from && char.IsHighSurrogate(s[end - 1])) end--;
        return end;
    }

    private static string Inv(long v) => v.ToString(CultureInfo.InvariantCulture);

    private static SheetModel OrdersSheet(FileEntry f, OrderLog? log, ForensicRecordFile? fr, WorkbookCodes.Texts texts)
    {
        var s = new SheetModel("Orders", [7, 7, 6, 5, 5, 9, 16, 12, 22, 18]);
        s.Rows.Add(["I", "T", "PL", "K", "KC", "TG", "TD", "AM", "AD", "AB"]);
        if (log is null)
        {
            s.Rows.Add(Missing(f, fr, texts));
            return s;
        }
        for (int i = 0; i < log.Count; i++)
        {
            OrderRecord r = log[i];
            (string kind, string target, string amount) = Code(r);
            s.Rows.Add([
                Inv(i), Inv(r.Turn), Inv(r.ActorId), Inv((int)r.Kind), kind, Inv(r.TargetId), target, CellNumber.Format(r.Amount), amount,
                BitConverter.DoubleToInt64Bits(r.Amount).ToString("X16", CultureInfo.InvariantCulture),
            ]);
        }
        if (log.Count == 0) s.Rows.Add(["EM"]);
        return s;
    }

    /// <summary>
    /// An order CODED (ADR-059 amendment 2026-10-29; the Codebook's Orders:kind and Orders:read rows): its kind code, its
    /// target decoded and its amount decoded, each from the payload meanings of <see cref="OrderKind"/>'s documentation
    /// (kinds 1–18; 10 is reserved for M7's MoveFormation and unused). An unknown kind, the reserved kind and a non-finite
    /// amount have no reading (empty); the exact values are the TG, AM and AB columns.
    /// </summary>
    public static (string Kind, string Target, string Amount) Code(OrderRecord r)
    {
        string id = Inv(r.TargetId);
        string amt = CellNumber.Format(r.Amount);
        string st = "ST" + id;
        string kind = WorkbookCodes.KindCode((int)r.Kind);
        (string target, string amount) = r.Kind switch
        {
            OrderKind.SetRainBias => ("RG" + id, "MM" + amt),
            OrderKind.LaborAllocation => (st, "PC" + amt),
            OrderKind.SectorAllocation => ("ST" + Inv(r.TargetId >> 3) + "/S" + Inv(r.TargetId & 7), "PC" + amt),
            OrderKind.EnqueueConstruction => (st, "PJ" + amt),
            OrderKind.SetTaxRate => ("PL" + id, "PC" + amt),
            OrderKind.SetResearchTarget => (r.TargetId switch { -1 => "LT-1", -2 => "LC-2", _ => "RN" + id }, "ZR" + amt),
            OrderKind.AdvanceAge => ("AG" + id, "SK" + amt),
            OrderKind.DevelopRoads => ("ZR" + id, "PC" + amt),
            OrderKind.FoundSettlement => (st, "CI" + amt),
            OrderKind.CancelConstruction => (st, "QS" + amt),
            OrderKind.SetResourceAllocation => ("PL" + id, "PC" + amt),
            OrderKind.CancelConstructionEntry => (st, OrderLog.TryUnpackCancelTarget(r.Amount, out CancelTarget c)
                ? "QS" + Inv(c.Slot) + "/PJ" + Inv(c.ProjectId) + "/T" + Inv(c.EnqueuedTurn)
                : "NV" + amt),
            OrderKind.Explore => (st, "ZR" + amt),
            OrderKind.Prospect => (st, r.Amount == 0.0 && !double.IsNegative(r.Amount) ? "SV" : r.Amount == 1.0 ? "AS" : "MT" + amt),
            OrderKind.RecruitFormation => (st, "ID" + amt),
            OrderKind.ReequipFormation => ("FM" + id, r.Amount == 0.0 ? "CN" : "ID" + amt),
            OrderKind.DisbandFormation => ("FM" + id, "ZR" + amt),
            _ => ("", ""),
        };
        if (!double.IsFinite(r.Amount)) amount = "";
        return (kind, target, amount);
    }

    private static SheetModel TraceSheet(FileEntry f, string[]? lines, ForensicRecordFile? fr, WorkbookCodes.Texts texts)
    {
        var s = new SheetModel("Trace", [7, 8, 11, 11, 11, 66]);
        string[] head = SessionTrace.Header.Split(',');
        for (int i = 0; i < head.Length; i++)
            head[i] = WorkbookCodes.TraceColumn(head[i]) is { Length: > 0 } code ? code : texts.Value(head[i]);
        s.Rows.Add(head);
        if (lines is null)
        {
            s.Rows.Add(Missing(f, fr, texts));
            return s;
        }
        for (int i = 0; i < lines.Length; i++)
            if (i > 0 || lines[i] != SessionTrace.Header) s.Rows.Add(lines[i].Split(','));
        if (lines.Length == 0) s.Rows.Add(["EM"]);
        return s;
    }

    /// <summary>The Chronicle sheet: one row per event line, numbered. A coded file (<see cref="Sim.Core.Chronicle.ChronicleCode"/>)
    /// gives its event lines as they are and its name lines to <paramref name="names"/> (the legend's 'Chronicle:names' rows); a
    /// pre-amendment prose file is NOT parsed: each line is its QT code (its text in the legend's 'QT:texts' rows).</summary>
    private static SheetModel ChronicleSheet(FileEntry f, string[]? lines, ForensicRecordFile? fr, int cellLimit,
        WorkbookCodes.Texts texts, List<(string Code, string Name)> names)
    {
        var s = new SheetModel("Chronicle", [7, 120]);
        s.Rows.Add(["L", "EV"]);
        if (lines is null)
        {
            s.Rows.Add(Missing(f, fr, texts));
            return s;
        }
        bool coded = lines.Length > 0 && lines[0] == Sim.Core.Chronicle.ChronicleCode.FormatTag;
        int n = 0;
        for (int i = coded ? 1 : 0; i < lines.Length; i++)
        {
            if (coded && Sim.Core.Chronicle.ChronicleCode.TryParseNameLine(lines[i], out string code, out string name))
            {
                names.Add((code, name));
                continue;
            }
            n++;
            // a coded event line is shown as it is (codes and numbers only); any other line - a prose line of a
            // pre-amendment file, or a line of a coded file that carries a word - is its QT code
            string line = lines[i];
            AddSplitRow(s, [Inv(n), coded && !WorkbookCodes.HasWord(line) ? line : texts.Value(line)], 1, cellLimit);
        }
        if (n == 0) s.Rows.Add(["EM"]);
        return s;
    }

    private static SheetModel CodebookSheet(List<string> codebooks, List<(string Code, string Name)> names, WorkbookCodes.Texts texts, int cellLimit)
    {
        var s = new SheetModel("Codebook", [14, 8, 6, 60, 18, 60, 60]);
        if (codebooks.Count == 0)
            s.Rows.Add(["codebook", "(none)", "", "the session has no telemetry, so no cell needs a codebook"]);
        foreach (string v in codebooks)
        {
            TelemetryCodebook cb = v == "v5" ? TelemetryCodebook.V5 : TelemetryCodebook.V4;
            s.Rows.Add(["codebook", v, "", "cells whose first token is " + string.Join(", ", Codes(cb)) + " are read with the rows below"]);
            foreach (string line in cb.Tsv.Split('\n'))
                if (line.Length > 0) s.Rows.Add(line.Split('\t'));
        }
        // ADR-059 amendment 2026-10-29: the legend of every other sheet's codes, then the session's names and texts.
        s.Rows.Add(["legend", "code", "", "what the code means: every sheet but README and Codebook holds only these codes, numbers and "
            + "identifiers (file names, hashes, version tags, JSON keys)"]);
        foreach (WorkbookCodes.Entry e in WorkbookCodes.Legend) AddSplitRow(s, [e.Section, e.Code, "", e.Meaning], 3, cellLimit);
        var sorted = new List<(string Code, string Name)>(names);
        sorted.Sort((a, b) => string.CompareOrdinal(a.Code, b.Code) is var c and not 0 ? c : string.CompareOrdinal(a.Name, b.Name));
        foreach ((string code, string name) in sorted) AddSplitRow(s, ["Chronicle:names", code, "", name], 3, cellLimit);
        for (int i = 0; i < texts.All.Count; i++)
            AddSplitRow(s, ["QT:texts", "QT" + Inv(i + 1), "", texts.All[i]], 3, cellLimit);
        return s;
    }

    private static List<string> Codes(TelemetryCodebook cb)
    {
        var o = new List<string>();
        foreach ((string _, string code) in cb.SchemaCodes)
            if (!o.Contains(code)) o.Add(code);
        o.Sort(StringComparer.Ordinal);
        return o;
    }

    private static SheetModel ForensicSheet(FileEntry f, string[]? lines, ForensicRecordFile? fr, FileEntry[] files, int cellLimit,
        WorkbookCodes.Texts texts)
    {
        var s = new SheetModel("Forensic", [44, 90, 12, 10, 66, 66, 22]);
        s.Rows.Add(["KY", "VL", "TY"]);
        if (lines is null) s.Rows.Add(f.Name.Length == 0 ? ["NN"] : ["NP", texts.Value(f.Name)]);
        else
        {
            int run = 0, close = 0, other = 0;
            foreach (string line in lines)
            {
                if (line.Trim().Length == 0) continue;
                JVal rec = Guard(f, () => JsonText.Parse(line));
                string kind = rec is JObj o && o.Get("rec") is JStr k ? k.Value : "record";
                int n = kind == "run" ? run++ : kind == "close" ? close++ : other++;
                string prefix = kind + (n == 0 ? "" : "#" + (n + 1).ToString(CultureInfo.InvariantCulture));
                Flatten(s, prefix, rec, cellLimit, texts);
            }
            if (fr?.Close is null)
                s.Rows.Add(["close", "NC", "X"]);
        }
        s.Rows.Add(["XF", "FN", "FS", "BY", "HN", "HC", "AG"]);
        foreach (FileEntry e in files)
        {
            ArtifactRef? listed = null;
            if (fr?.Close is { } c)
                foreach (ArtifactRef a in c.Artifacts)
                    if (a.Role == e.Role) listed = a;
            string agreement = e.Role == "forensic" ? "SF"
                : fr?.Close is null ? "NC"
                : listed is null ? "NL"
                : !e.Present ? "LN"
                : listed.Sha256 == e.Sha256 ? "EQ" : "NE";
            s.Rows.Add([
                "XF." + WorkbookCodes.RoleCode(e.Role), e.Name.Length == 0 ? "NN" : texts.Value(e.Name),
                e.Recovered is not null && e.Present ? "RW" : e.Present ? "PS" : "NP",
                e.Present ? e.Bytes!.Length.ToString(CultureInfo.InvariantCulture) : "", e.Sha256, listed?.Sha256 ?? "", agreement,
            ]);
        }
        return s;
    }

    // ------------------------------------------------------------------------------------------ helpers --

    private static FileEntry Entry(string role, string name, Func<string, byte[]?> read, string? recovered = null) =>
        new(role, name, name.Length == 0 ? null : read(name), recovered);

    /// <summary>The row a sheet holds for a file the manifest names but the directory does not hold: NP, the file name,
    /// then what the forensic close record lists of it — bytes, sha256, state — or NN when the manifest names no such
    /// file. Nothing is invented.</summary>
    private static string[] Missing(FileEntry f, ForensicRecordFile? fr, WorkbookCodes.Texts texts)
    {
        if (f.Name.Length == 0) return ["NN"];
        var row = new List<string> { "NP", texts.Value(f.Name) };
        if (fr?.Close is { } c)
            foreach (ArtifactRef a in c.Artifacts)
                if (a.Role == f.Role)
                {
                    row.Add(a.Bytes?.ToString(CultureInfo.InvariantCulture) ?? "");
                    row.Add(a.Sha256 ?? "");
                    row.Add(texts.Value(a.State ?? ""));
                }
        return row.ToArray();
    }

    private static string TurnOf(string line, List<string> schemas)
    {
        var doc = (JObj)JsonText.Parse(line);
        string tag = doc.Get("schema") is JStr s ? s.Value : "";
        if (!schemas.Contains(tag)) schemas.Add(tag);
        return doc.Get("turn") is JObj t && t.Get("turn") is JNum n ? n.Text : "";
    }

    private static T Guard<T>(FileEntry f, Func<T> read)
    {
        try
        {
            return read();
        }
        catch (Exception e) when (e is not SessionExportException)
        {
            throw new SessionExportException(SessionExportException.RecordUnreadable, f.Name + ": " + e.Message, e);
        }
    }

    private static T Wrap<T>(string where, Func<T> f)
    {
        try
        {
            return f();
        }
        catch (SessionExportException e)
        {
            throw new SessionExportException(e.Code, where + ": " + e.Message[(e.Code.Length + 2)..], e);
        }
    }

    private static string Utf8(byte[] bytes, string name)
    {
        try
        {
            return new UTF8Encoding(false, throwOnInvalidBytes: true).GetString(bytes);
        }
        catch (DecoderFallbackException e)
        {
            throw new SessionExportException(SessionExportException.RecordUnreadable, name + " is not UTF-8 text", e);
        }
    }

    /// <summary>A text file's lines: split at '\n' only (a '\r' stays in its line, so nothing is silently dropped);
    /// the empty piece after a final newline is not a line.</summary>
    private static string[] Lines(FileEntry f)
    {
        string text = Utf8(f.Bytes!, f.Name);
        if (text.Length == 0) return [];
        string[] parts = text.Split('\n');
        return parts[^1].Length == 0 ? parts[..^1] : parts;
    }
}
