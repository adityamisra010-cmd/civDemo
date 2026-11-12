using System.Globalization;
using Sim.Core.Observability.Forensic;

namespace Sim.Ui.Export;

public static partial class SessionWorkbook
{
    /// <summary>
    /// D-086 DE1 (the Director, 2026-11-11: "it should record which node was researched and how much and which turn also";
    /// readings DE1-R1..R3 (a)) — THE RESEARCH SHEET: the research rows the telemetry lines carry (telemetry/v6:
    /// <c>turn.research</c> and <c>turn.civicsBank</c>, written by Sim.Core.Observability.ResearchObservation), one sheet
    /// row per line row, in line order. CODED like every non-legend sheet (ADR-059 amendment 2026-10-29): codes, numbers and
    /// identifiers only, every code in the Codebook legend (<c>Research:col</c>, <c>Research:kind</c>,
    /// <c>Research:read</c>). Numbers are the telemetry writer's own text under the workbook's cell convention (a leading
    /// <c>0.</c> written <c>.</c>); the Civics Points figures are the record's integer milli-CP written as exact decimals.
    /// Nothing is invented: a telemetry line without research rows (telemetry/v2–v5) is counted in an <c>NR</c> row, a
    /// telemetry file that is not present is the <c>NP</c> row every sheet uses.
    /// </summary>
    private sealed class ResearchSheet
    {
        public static readonly string[] Header = ["T", "HD", "KD", "RN", "LN", "PR", "AC", "PG", "CS", "LV", "GN", "DR", "BL"];

        private readonly List<string[]> _rows = [];

        /// <summary>Telemetry lines that carry research rows (telemetry/v6 and later).</summary>
        public int RecordedTurns { get; private set; }

        /// <summary>Telemetry lines that carry none (written before telemetry/v6).</summary>
        public int UnrecordedTurns { get; private set; }

        public int NodeRows { get; private set; }
        public int Completions { get; private set; }
        public int BankRows { get; private set; }

        /// <summary>The rows of one telemetry line (its document, as the exporter parsed it once).</summary>
        public void Add(string turn, JObj doc)
        {
            if (doc.Get("turn") is not JObj t || t.Get("research") is not JArr research)
            {
                UnrecordedTurns++;
                return;
            }
            RecordedTurns++;
            foreach (JVal v in research.Items)
            {
                if (v is not JObj r) throw new SessionExportException(SessionExportException.TelemetryShape, "a research row is not an object");
                bool completed = r.Get("completed") is JBool { Value: true };
                if (completed) Completions++;
                NodeRows++;
                _rows.Add([
                    turn, Holder(r.Get("holder")), completed ? "RC" : "RP", "RN" + Int(r.Get("node")), Lane(r.Get("tree")),
                    Number(r.Get("received")), Number(r.Get("credit")), Number(r.Get("progress")), Number(r.Get("cost")),
                    Int(r.Get("level")),
                ]);
            }
            if (t.Get("civicsBank") is JArr bank)
                foreach (JVal v in bank.Items)
                {
                    if (v is not JObj b) throw new SessionExportException(SessionExportException.TelemetryShape, "a civicsBank row is not an object");
                    BankRows++;
                    _rows.Add([
                        turn, Holder(b.Get("holder")), "BK", "", "", "", "", "", "", "",
                        Milli(b.Get("generatedMilli")), Milli(b.Get("drawnMilli")), Milli(b.Get("balanceMilli")),
                    ]);
                }
        }

        /// <summary>What the README says of the sheet.</summary>
        public string Summary() =>
            RecordedTurns == 0 && UnrecordedTurns == 0
                ? "This session has no telemetry lines, so no research rows."
                : RecordedTurns == 0
                    ? "This session's telemetry was written before research rows were recorded (telemetry/v6): the sheet holds one NR "
                      + "row and nothing is invented."
                    : "This workbook has " + Inv(NodeRows) + " node row(s), " + Inv(Completions) + " of them completions, and "
                      + Inv(BankRows) + " bank row(s), from " + Inv(RecordedTurns) + " turn(s)"
                      + (UnrecordedTurns > 0 ? "; " + Inv(UnrecordedTurns) + " turn(s) carry no research rows (the NR row)." : ".");

        public SheetModel Build(FileEntry telemetry, ForensicRecordFile? fr, WorkbookCodes.Texts texts,
            Func<FileEntry, ForensicRecordFile?, WorkbookCodes.Texts, string[]> missing)
        {
            var s = new SheetModel("Research", [7, 8, 5, 9, 5, 14, 14, 14, 14, 5, 12, 12, 14]);
            s.Rows.Add(Header);
            if (!telemetry.Present)
            {
                s.Rows.Add(missing(telemetry, fr, texts));
                return s;
            }
            if (RecordedTurns == 0 && UnrecordedTurns == 0)
            {
                s.Rows.Add(["EM"]);
                return s;
            }
            if (UnrecordedTurns > 0) s.Rows.Add(["NR", Inv(UnrecordedTurns)]);
            if (RecordedTurns > 0 && _rows.Count == 0) s.Rows.Add(["NX", Inv(RecordedTurns)]);
            s.Rows.AddRange(_rows);
            return s;
        }

        private static string Inv(int v) => v.ToString(CultureInfo.InvariantCulture);

        /// <summary>A holder: <c>PL&lt;id&gt;</c> for a polity, <c>ST&lt;id&gt;</c> for a city-state (local key −1 − id).</summary>
        private static string Holder(JVal? v)
        {
            if (v is not JNum n || !long.TryParse(n.Text, NumberStyles.AllowLeadingSign, CultureInfo.InvariantCulture, out long h))
                throw new SessionExportException(SessionExportException.TelemetryShape, "a research row's holder is not an integer");
            return h >= 0 ? "PL" + h.ToString(CultureInfo.InvariantCulture) : "ST" + (-1L - h).ToString(CultureInfo.InvariantCulture);
        }

        private static string Int(JVal? v) =>
            v is JNum n && long.TryParse(n.Text, NumberStyles.AllowLeadingSign, CultureInfo.InvariantCulture, out long x)
                ? x.ToString(CultureInfo.InvariantCulture)
                : throw new SessionExportException(SessionExportException.TelemetryShape, "a research row's integer field is not an integer");

        private static string Lane(JVal? v) => v is JStr { Value: var tree }
            ? tree switch
            {
                "Technology" => "LT",
                "Civics" => "LC",
                _ => throw new SessionExportException(SessionExportException.TelemetryShape, "a research row's tree is not Technology or Civics"),
            }
            : throw new SessionExportException(SessionExportException.TelemetryShape, "a research row has no tree");

        /// <summary>A double as the writer wrote it, under the workbook's cell convention (README 'cells'): a leading <c>0.</c>
        /// written <c>.</c>, <c>-0.</c> written <c>-.</c>; the writer's NaN / Infinity strings as N, I, -I.</summary>
        internal static string Number(JVal? v)
        {
            switch (v)
            {
                case JNum n:
                    string t = n.Text;
                    if (t.StartsWith("0.", StringComparison.Ordinal)) return t[1..];
                    if (t.StartsWith("-0.", StringComparison.Ordinal)) return "-" + t[2..];
                    return t;
                case JStr { Value: "NaN" }: return "N";
                case JStr { Value: "Infinity" }: return "I";
                case JStr { Value: "-Infinity" }: return "-I";
                default: throw new SessionExportException(SessionExportException.TelemetryShape, "a research row's number is not a number");
            }
        }

        /// <summary>Integer milli-CP as exact Civics Points: the thousandths after the point, trailing zeros dropped, and the
        /// cell convention for a leading <c>0.</c> (500 → <c>.5</c>, 2500 → <c>2.5</c>, 12345 → <c>12.345</c>, 0 → <c>0</c>).</summary>
        internal static string Milli(JVal? v)
        {
            if (v is not JNum n || !long.TryParse(n.Text, NumberStyles.AllowLeadingSign, CultureInfo.InvariantCulture, out long m))
                throw new SessionExportException(SessionExportException.TelemetryShape, "a civicsBank figure is not an integer");
            return MilliText(m);
        }

        internal static string MilliText(long m)
        {
            bool negative = m < 0;
            ulong a = negative ? (ulong)(-(m + 1)) + 1UL : (ulong)m;
            ulong whole = a / 1000UL, frac = a % 1000UL;
            string text = frac == 0UL
                ? whole.ToString(CultureInfo.InvariantCulture)
                : (whole == 0UL ? "" : whole.ToString(CultureInfo.InvariantCulture)) + "."
                  + frac.ToString("000", CultureInfo.InvariantCulture).TrimEnd('0');
            return negative ? "-" + text : text;
        }
    }
}
