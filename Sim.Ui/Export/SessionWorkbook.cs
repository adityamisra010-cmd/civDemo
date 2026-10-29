using System.Globalization;
using System.Text;
using Sim.Core.Kernel;
using Sim.Core.Observability.Forensic;

namespace Sim.Ui.Export;

/// <summary>What one export produced.</summary>
public sealed record SessionExportResult(
    byte[] Bytes,
    string Sha256,
    IReadOnlyList<SheetModel> Sheets,
    int TelemetryLines,
    int ContinuationRows,
    int LongestCell,
    IReadOnlyList<string> Codebooks);

/// <summary>
/// ADR-059 (D-066 A14–A17) — THE ONE-FILE SESSION EXPORT: <c>session-STAMP.xlsx</c>, written beside the six session
/// files, holding all of them in eight sheets — README, Manifest, Orders (decoded), Trace, Chronicle, Telemetry (column A
/// the turn, column B one coded cell per turn), Codebook, Forensic (with every file's sha256) — so a played session is
/// one upload (docs/m6-session-export/README.md).
///
/// SOURCES: THE RECORD FILES ONLY. The manifest (SessionManifest.Read), the order log (OrderLog.Load), the trace
/// (SessionTrace.Parse), the chronicle text, the telemetry JSONL and the forensic record (ForensicRecordFile.Parse). The
/// exporter never replays, never re-founds a world and reads no simulation state: what the workbook says is what the
/// files say. A file the manifest names but the directory does not hold is said to be missing, in its sheet, in plain
/// words — never invented.
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
    /// The file is written only after the self-check passed, in one write.</summary>
    public static (string Path, SessionExportResult Result) ExportBeside(string manifestPath)
    {
        string full = Path.GetFullPath(manifestPath);
        string dir = Path.GetDirectoryName(full) ?? "";
        SessionExportResult result = Build(Path.GetFileName(full), name => ReadBeside(dir, name));
        string outPath = WorkbookPathFor(full);
        File.WriteAllBytes(outPath, result.Bytes);
        return (outPath, result);
    }

    private static byte[]? ReadBeside(string dir, string name)
    {
        if (name.Length == 0 || name.IndexOfAny(['/', '\\']) >= 0) return null;   // a bare file name beside the manifest
        string p = Path.Combine(dir, name);
        return File.Exists(p) ? File.ReadAllBytes(p) : null;
    }

    private sealed record FileEntry(string Role, string Name, byte[]? Bytes)
    {
        public bool Present => Bytes is not null;
        public string Sha256 => Bytes is null ? "" : ForensicIdentity.Sha256Hex(Bytes);
    }

    /// <summary>Builds and self-checks the workbook from the files <paramref name="read"/> returns by name (null =
    /// not present). <paramref name="cellLimit"/> is Excel's, lowered only by tests of the continuation rule.</summary>
    public static SessionExportResult Build(string manifestName, Func<string, byte[]?> read, int cellLimit = OoxmlWorkbook.MaxCellChars)
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
            Entry("telemetry", manifest.TelemetryFile, read),
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
        var codebooks = new List<string>();
        var schemas = new List<string>();
        int continuation = 0, longest = 0;
        string firstTurn = "", lastTurn = "";
        if (telemetryLines is null)
        {
            telemetrySheet.Rows.Add(["turn", "cell"]);
            telemetrySheet.Rows.Add(["FILE NOT PRESENT", Missing(telemetry, forensicFile)]);
        }
        else
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
                if (i == 0)
                {
                    string head = "turn";
                    string col = "cell (one turn; read with the Codebook sheet)";
                    telemetrySheet.Rows.Add([head, col]);
                    firstTurn = turn;
                }
                lastTurn = turn;
                telemetrySheet.Rows.Add([turn, parts[0]]);
                for (int k = 1; k < parts.Count; k++) telemetrySheet.Rows.Add(["", parts[k]]);
                continuation += parts.Count - 1;
                foreach (string p in parts) longest = Math.Max(longest, p.Length);
            }
            if (telemetryLines.Length == 0) telemetrySheet.Rows.Add(["turn", "cell (the telemetry file holds no turn)"]);
        }

        var sheets = new List<SheetModel>
        {
            Readme(manifest, manifestName, files, schemas, codebooks, telemetryLines?.Length ?? 0, firstTurn, lastTurn, continuation),
            KeyValueSheet("Manifest", [("", mJson)], cellLimit),
            OrdersSheet(orders, log, forensicFile),
            TraceSheet(trace, traceLines, forensicFile),
            ChronicleSheet(chronicle, chronicleLines, forensicFile, cellLimit),
            telemetrySheet,
            CodebookSheet(codebooks),
            ForensicSheet(forensic, forensicLines, forensicFile, files, cellLimit),
        };

        byte[] bytes = OoxmlWorkbook.Write(sheets);
        SelfCheck(bytes, sheets, telemetryLines);
        return new SessionExportResult(bytes, ForensicIdentity.Sha256Hex(bytes), sheets, telemetryLines?.Length ?? 0,
            continuation, longest, codebooks);
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
        List<string> codebooks, int turns, string firstTurn, string lastTurn, int continuation)
    {
        var s = new SheetModel("README", [22, 140]);
        void R(string a, string b) => s.Rows.Add([a, b]);
        R("Session workbook", "One played session in one file: the six session files as eight sheets (D-066 A14-A17; ADR-059). "
            + "The six files stay the record (resume reads them); this workbook is a reading copy written beside them, from them alone.");
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
        R("EMPTY", "EMPTY never means 0 - it means the codebook default (the Codebook sheet, column 'default when the field is EMPTY').");
        R("cells", "Every cell is text. Numbers are the writer's exact text; a leading 0. is written . and -0. is written -.; "
            + "N, I and -I are NaN, Infinity and -Infinity. Order amounts (formatted here, from the binary log) follow the same rule.");
        R("continuation", "A cell holds at most 32767 characters. A longer coded turn is split at a | into further rows: a continuation "
            + "row has an EMPTY column A and its cell starts with > followed by the | that was cut. Join: drop the > and append the rest "
            + "to the cell above. This workbook has " + continuation.ToString(CultureInfo.InvariantCulture) + " continuation row(s).");
        R("sheet README", "This sheet.");
        R("sheet Manifest", "session-*.json as key / value / JSON type rows (nested keys joined with '.', array items as [i]).");
        R("sheet Orders", "orders-*.bin decoded: one row per order in log order - turn, issuing polity, kind, target and amount spelled out, "
            + "and the amount's exact 64-bit pattern.");
        R("sheet Trace", "trace-*.csv as is: turn, year, population, settlements, food, world hash.");
        R("sheet Chronicle", "chronicle-*.txt, one line per row.");
        R("sheet Telemetry", "telemetry-*.jsonl: column A the turn, column B the whole turn as one coded cell (README of the export: "
            + "docs/m6-session-export/README.md). Each cell decodes back to its line byte for byte (checked before this file was written).");
        R("sheet Codebook", "The encoding table every coded cell is read with (one codebook per telemetry schema).");
        R("sheet Forensic", "forensic-*.jsonl: the run and close records as key / value rows, then every session file with its sha256 "
            + "now and as the close record lists it.");
        foreach (FileEntry f in files)
            R("file " + f.Role, f.Name.Length == 0 ? "not named by the manifest"
                : f.Present ? f.Name + " - present, " + f.Bytes!.Length.ToString(CultureInfo.InvariantCulture) + " bytes, sha256 " + f.Sha256
                : f.Name + " - NOT PRESENT beside the manifest; its sheet says so and nothing is invented");
        return s;
    }

    private static SheetModel KeyValueSheet(string name, (string Prefix, JVal Value)[] roots, int cellLimit)
    {
        var s = new SheetModel(name, [44, 120, 10]);
        s.Rows.Add(["key", "value", "type"]);
        foreach ((string prefix, JVal value) in roots) Flatten(s, prefix, value, cellLimit);
        return s;
    }

    private static void Flatten(SheetModel s, string key, JVal v, int cellLimit)
    {
        switch (v)
        {
            case JObj o when o.Keys.Count > 0:
                for (int i = 0; i < o.Keys.Count; i++)
                    Flatten(s, key.Length == 0 ? o.Keys[i] : key + "." + o.Keys[i], o.Values[i], cellLimit);
                break;
            case JArr a when a.Items.Count > 0:
                for (int i = 0; i < a.Items.Count; i++)
                    Flatten(s, key + "[" + i.ToString(CultureInfo.InvariantCulture) + "]", a.Items[i], cellLimit);
                break;
            case JObj:
                AddValueRow(s, [key, "{}", "object"], cellLimit);
                break;
            case JArr:
                AddValueRow(s, [key, "[]", "array"], cellLimit);
                break;
            case JStr str:
                AddValueRow(s, [key, str.Value, "string"], cellLimit);
                break;
            case JNum n:
                AddValueRow(s, [key, n.Text, "number"], cellLimit);
                break;
            case JBool b:
                AddValueRow(s, [key, b.Value ? "true" : "false", "bool"], cellLimit);
                break;
            default:
                AddValueRow(s, [key, "null", "null"], cellLimit);
                break;
        }
    }

    /// <summary>A row whose column-B text may exceed the cell limit: the excess goes to A17 continuation rows (column A
    /// empty, the cell starting with the marker), split between characters (never inside a surrogate pair).</summary>
    private static void AddValueRow(SheetModel s, string[] row, int cellLimit)
    {
        if (row[0].Length == 0) row[0] = "(root)";
        string b = row[1];
        if (b.Length <= cellLimit)
        {
            s.Rows.Add(row);
            return;
        }
        int cut = SafeCut(b, 0, cellLimit);
        row[1] = b[..cut];
        s.Rows.Add(row);
        while (cut < b.Length)
        {
            int next = SafeCut(b, cut, cellLimit - 1);
            s.Rows.Add(["", ">" + b[cut..next]]);
            cut = next;
        }
    }

    private static int SafeCut(string s, int from, int max)
    {
        int end = Math.Min(s.Length, from + max);
        if (end < s.Length && end > from && char.IsHighSurrogate(s[end - 1])) end--;
        return end;
    }

    private static SheetModel OrdersSheet(FileEntry f, OrderLog? log, ForensicRecordFile? fr)
    {
        var s = new SheetModel("Orders", [7, 7, 8, 6, 24, 10, 44, 12, 44, 18]);
        s.Rows.Add(["index", "turn", "polity", "kind", "kind name", "target id", "target", "amount", "amount means", "amount bits"]);
        if (log is null)
        {
            s.Rows.Add(["FILE NOT PRESENT", Missing(f, fr)]);
            return s;
        }
        for (int i = 0; i < log.Count; i++)
        {
            OrderRecord r = log[i];
            (string kindName, string target, string amount) = Describe(r);
            s.Rows.Add([
                i.ToString(CultureInfo.InvariantCulture), r.Turn.ToString(CultureInfo.InvariantCulture),
                r.ActorId.ToString(CultureInfo.InvariantCulture), ((int)r.Kind).ToString(CultureInfo.InvariantCulture),
                kindName, r.TargetId.ToString(CultureInfo.InvariantCulture), target, CellNumber.Format(r.Amount), amount,
                BitConverter.DoubleToInt64Bits(r.Amount).ToString("X16", CultureInfo.InvariantCulture),
            ]);
        }
        if (log.Count == 0) s.Rows.Add(["(none)", "the order log holds no order"]);
        return s;
    }

    private static readonly string[] SectorNames = ["farming", "herding", "extraction", "crafting", "construction"];

    /// <summary>An order spelled out (kinds 1–18; 10 is reserved for M7's MoveFormation and unused). Payload meanings are
    /// <see cref="OrderKind"/>'s documentation.</summary>
    public static (string KindName, string Target, string Amount) Describe(OrderRecord r)
    {
        string id = r.TargetId.ToString(CultureInfo.InvariantCulture);
        string amt = CellNumber.Format(r.Amount);
        string settlement = "settlement #" + id;
        string name = Enum.IsDefined(r.Kind) ? r.Kind.ToString() : (int)r.Kind == 10 ? "reserved (MoveFormation, M7)" : "unknown kind";
        return r.Kind switch
        {
            OrderKind.SetRainBias => (name, "region " + id, "rainfall bias " + amt + " mm/year"),
            OrderKind.LaborAllocation => (name, settlement, "farming share " + amt + "%"),
            OrderKind.SectorAllocation => (name, "settlement #" + (r.TargetId >> 3).ToString(CultureInfo.InvariantCulture) + ", sector "
                + (r.TargetId & 7).ToString(CultureInfo.InvariantCulture) + " ("
                + ((r.TargetId & 7) < SectorNames.Length ? SectorNames[r.TargetId & 7] : "unknown") + ")", "labour weight " + amt + "%"),
            OrderKind.EnqueueConstruction => (name, settlement, "project " + amt),
            OrderKind.SetTaxRate => (name, "polity " + id, "tax rate " + amt + "%"),
            OrderKind.SetResearchTarget => (name, r.TargetId switch
            {
                -1 => "clear the technology lane",
                -2 => "clear the civics lane",
                _ => "research node key " + id,
            }, "reserved (" + amt + ")"),
            OrderKind.AdvanceAge => (name, "Age " + id, "surge key " + amt),
            OrderKind.DevelopRoads => (name, "reserved (" + id + ")", "develop " + amt + "% of eligible transport demand"),
            OrderKind.FoundSettlement => (name, "source " + settlement, "site cell " + amt),
            OrderKind.CancelConstruction => (name, settlement, "queue slot " + amt + " (legacy kind 11)"),
            OrderKind.SetResourceAllocation => (name, "polity " + id, "buildings " + amt + "% (roads the rest)"),
            OrderKind.CancelConstructionEntry => (name, settlement, OrderLog.TryUnpackCancelTarget(r.Amount, out CancelTarget c)
                ? "slot " + c.Slot.ToString(CultureInfo.InvariantCulture) + ", project " + c.ProjectId.ToString(CultureInfo.InvariantCulture)
                  + ", enqueued turn " + c.EnqueuedTurn.ToString(CultureInfo.InvariantCulture)
                : "not a packed entry identity (" + amt + ")"),
            OrderKind.Explore => (name, "origin " + settlement, "reserved (" + amt + ")"),
            OrderKind.Prospect => (name, settlement, r.Amount == 0.0 && !double.IsNegative(r.Amount) ? "method 0 (survey)"
                : r.Amount == 1.0 ? "method 1 (assay)" : "method " + amt),
            OrderKind.RecruitFormation => (name, settlement, "identity key " + amt),
            OrderKind.ReequipFormation => (name, "formation #" + id, r.Amount == 0.0 ? "cancel the pending re-equipment" : "target identity key " + amt),
            OrderKind.DisbandFormation => (name, "formation #" + id, "reserved (" + amt + ")"),
            _ => (name, "target " + id, amt),
        };
    }

    private static SheetModel TraceSheet(FileEntry f, string[]? lines, ForensicRecordFile? fr)
    {
        var s = new SheetModel("Trace", [7, 8, 11, 11, 11, 66]);
        if (lines is null)
        {
            s.Rows.Add(["turn", "year", "population", "settlements", "food", "hash"]);
            s.Rows.Add(["FILE NOT PRESENT", Missing(f, fr)]);
            return s;
        }
        foreach (string line in lines) s.Rows.Add(line.Split(','));
        if (lines.Length == 0) s.Rows.Add(["(empty)", "the trace file holds no line"]);
        return s;
    }

    private static SheetModel ChronicleSheet(FileEntry f, string[]? lines, ForensicRecordFile? fr, int cellLimit)
    {
        var s = new SheetModel("Chronicle", [7, 160]);
        s.Rows.Add(["line", "text"]);
        if (lines is null)
        {
            s.Rows.Add(["FILE NOT PRESENT", Missing(f, fr)]);
            return s;
        }
        for (int i = 0; i < lines.Length; i++)
            AddValueRow(s, [(i + 1).ToString(CultureInfo.InvariantCulture), lines[i]], cellLimit);
        if (lines.Length == 0) s.Rows.Add(["(empty)", "the chronicle file holds no line"]);
        return s;
    }

    private static SheetModel CodebookSheet(List<string> codebooks)
    {
        var s = new SheetModel("Codebook", [11, 6, 6, 44, 18, 60, 60]);
        if (codebooks.Count == 0)
        {
            s.Rows.Add(["codebook", "(none)", "", "the session has no telemetry, so no cell needs a codebook"]);
            return s;
        }
        foreach (string v in codebooks)
        {
            TelemetryCodebook cb = v == "v5" ? TelemetryCodebook.V5 : TelemetryCodebook.V4;
            s.Rows.Add(["codebook", v, "", "cells whose first token is " + string.Join(", ", Codes(cb)) + " are read with the rows below"]);
            foreach (string line in cb.Tsv.Split('\n'))
                if (line.Length > 0) s.Rows.Add(line.Split('\t'));
        }
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

    private static SheetModel ForensicSheet(FileEntry f, string[]? lines, ForensicRecordFile? fr, FileEntry[] files, int cellLimit)
    {
        var s = new SheetModel("Forensic", [44, 90, 12, 10, 66, 66, 22]);
        s.Rows.Add(["key", "value", "type"]);
        if (lines is null) s.Rows.Add(["FILE NOT PRESENT", f.Name.Length == 0 ? "the manifest names no forensic file" : f.Name + " is not beside the manifest"]);
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
                Flatten(s, prefix, rec, cellLimit);
            }
            if (fr?.Close is null)
                s.Rows.Add(["close", "NO CLOSE RECORD: the session did not close cleanly (or is still open)", "note"]);
        }
        s.Rows.Add(["export.file", "file", "state", "bytes", "sha256 now", "sha256 in the close record", "agreement"]);
        foreach (FileEntry e in files)
        {
            ArtifactRef? listed = null;
            if (fr?.Close is { } c)
                foreach (ArtifactRef a in c.Artifacts)
                    if (a.Role == e.Role) listed = a;
            string agreement = e.Role == "forensic" ? "the forensic file holds the close record; it lists no hash of itself"
                : fr?.Close is null ? "no close record"
                : listed is null ? "not listed by the close record"
                : !e.Present ? "listed, but the file is NOT PRESENT now"
                : listed.Sha256 == e.Sha256 ? "same" : "DIFFERS";
            s.Rows.Add([
                "export.file." + e.Role, e.Name.Length == 0 ? "(not named)" : e.Name, e.Present ? "present" : "NOT PRESENT",
                e.Present ? e.Bytes!.Length.ToString(CultureInfo.InvariantCulture) : "", e.Sha256, listed?.Sha256 ?? "", agreement,
            ]);
        }
        return s;
    }

    // ------------------------------------------------------------------------------------------ helpers --

    private static FileEntry Entry(string role, string name, Func<string, byte[]?> read) =>
        new(role, name, name.Length == 0 ? null : read(name));

    /// <summary>What a sheet says of a file the manifest names but the directory does not hold.</summary>
    private static string Missing(FileEntry f, ForensicRecordFile? fr)
    {
        if (f.Name.Length == 0) return "the manifest names no " + f.Role + " file";
        string text = f.Name + " is named by the manifest but is not beside it, so this sheet is empty. Nothing is invented.";
        if (fr?.Close is { } c)
            foreach (ArtifactRef a in c.Artifacts)
                if (a.Role == f.Role)
                    text += " The forensic close record lists it: " + (a.Bytes?.ToString(CultureInfo.InvariantCulture) ?? "?")
                        + " bytes, sha256 " + (a.Sha256 ?? "(none)") + " (" + a.State + ").";
        return text;
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
