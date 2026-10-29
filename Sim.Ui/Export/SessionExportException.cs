namespace Sim.Ui.Export;

/// <summary>
/// ADR-059 (D-066 A14–A17) — the session export's NAMED refusal. The exporter never writes a workbook it cannot read
/// back exactly: a telemetry line it cannot encode, a cell that does not decode to its source line, a cell or a sheet
/// over Excel's limits that cannot be split under the A17 grammar, or a record file it cannot read ends the export
/// with one of these, and no workbook is written. There is no raw fallback.
/// </summary>
public sealed class SessionExportException : Exception
{
    /// <summary>The stable name of the refusal (see the constants below).</summary>
    public string Code { get; }

    public SessionExportException(string code, string message, Exception? inner = null)
        : base(code + ": " + message, inner) => Code = code;

    /// <summary>The session manifest is missing or is not a session manifest.</summary>
    public const string ManifestUnreadable = "manifest-unreadable";
    /// <summary>A companion file the manifest names is present but cannot be read by its own reader.</summary>
    public const string RecordUnreadable = "record-unreadable";
    /// <summary>A telemetry line carries a schema tag no codebook covers (README §5.4: one codebook per schema).</summary>
    public const string TelemetrySchemaUnknown = "telemetry-schema-unknown";
    /// <summary>A telemetry line's shape differs from its codebook (key order, a missing key, a non-array table).</summary>
    public const string TelemetryShape = "telemetry-shape";
    /// <summary>A coded cell does not follow the cell grammar.</summary>
    public const string CellUndecodable = "cell-undecodable";
    /// <summary>The self-check: a coded cell does not decode to its source line byte for byte.</summary>
    public const string RoundTripMismatch = "telemetry-roundtrip-mismatch";
    /// <summary>A cell over 32,767 characters that cannot be split (a single segment over the limit, or a column
    /// other than B).</summary>
    public const string CellTooLarge = "cell-too-large";
    /// <summary>A sheet over 1,048,576 rows.</summary>
    public const string RowLimit = "row-limit";
    /// <summary>The self-check: the workbook read back does not hold exactly the cells that were written.</summary>
    public const string WorkbookReadBack = "workbook-readback-mismatch";
    /// <summary>ADR-059 amendment 2026-10-29: the raw telemetry log is not beside the manifest (the game removes it after
    /// a verified export) and the existing workbook's Telemetry sheet does not give back the bytes the forensic close
    /// record lists for it (or there is no close record to check them against). Nothing is written: the existing workbook
    /// is left as it is, never replaced by one without the telemetry.</summary>
    public const string TelemetryUnrecoverable = "telemetry-unrecoverable";
}
