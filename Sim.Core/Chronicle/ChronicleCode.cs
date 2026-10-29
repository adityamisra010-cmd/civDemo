using System.Globalization;
using System.Text;

namespace Sim.Core.Chronicle;

/// <summary>
/// ADR-059 amendment 2026-10-29 (Director: "the workbook should have 0 real words in anything that is not a key or a
/// legend … the chronicle should come out shortened") — THE CODED CHRONICLE RECORD. The session record
/// (<c>chronicle-*.txt</c>) holds one CODED line per event, built at the source from the event's type and the very
/// values <see cref="ChronicleProse"/> binds into its template (same rounding, same "fewer than 1" / "more than 99"
/// bounds, written <c>&lt;1</c> / <c>&gt;99</c>), never by reading prose back. The in-game ANNALS stay prose.
///
/// LINE GRAMMAR: <c>Y&lt;year&gt; &lt;EV&gt; &lt;field&gt; &lt;field&gt; …</c>, single spaces; every field is a
/// letter prefix and its value (<c>ST12</c> settlement 12, <c>SL438</c> 438 people). The codes are defined in the
/// workbook's Codebook (legend) and in docs/adr/adr-059-one-file-session-export.md. A settlement is named by its id,
/// never by its name; a name appears only in a NAME LINE <c>ST12=Bigen</c> (a legend line), and only for a code an
/// event line uses.
///
/// FILE: the format tag <see cref="FormatTag"/> on line 1, then the name lines in ordinal order of their code, then the
/// event lines in event order. Deterministic: no dictionary iteration, invariant formatting, ordinal sort.
/// </summary>
public static class ChronicleCode
{
    /// <summary>Line 1 of a coded chronicle file. A file without it is a pre-amendment PROSE chronicle.</summary>
    public const string FormatTag = "chronicle-codes/v1";

    /// <summary>The event code of each core chronicle event type, in <see cref="ChronicleEventType"/> order.</summary>
    public static string EventCode(ChronicleEventType t) => t switch
    {
        ChronicleEventType.Founding => "FN",
        ChronicleEventType.FamineOnset => "FA",
        ChronicleEventType.FamineEnd => "FE",
        ChronicleEventType.Extinction => "XT",
        ChronicleEventType.FirstArtisans => "AR",
        ChronicleEventType.MigrationSurge => "MG",
        ChronicleEventType.Disaster => "DS",
        ChronicleEventType.FoodShortfallOnset => "SO",
        ChronicleEventType.FoodShortfallEnd => "SE",
        _ => throw new InvalidOperationException($"unknown event type {t}"),
    };

    /// <summary>The year field: <c>Y</c> and the sim-year exactly as the prose prints it.</summary>
    public static string Year(double year) => "Y" + ChronicleProse.FormatYear(year);

    /// <summary>A settlement's code: <c>ST</c> and its id.</summary>
    public static string Settlement(int id) => "ST" + id.ToString(CultureInfo.InvariantCulture);

    /// <summary>A share field value as the prose prints it, with its two bounds coded: <c>&lt;1</c> for a positive share
    /// that rounds to 0 ("fewer than 1"), <c>&gt;99</c> for a share short of the whole that rounds to 100.</summary>
    public static string Share(double fraction) => ChronicleProse.ShareText(fraction, "<1", ">99");

    private static string Whole(double v) => ((long)Math.Round(v)).ToString(CultureInfo.InvariantCulture);

    /// <summary>
    /// One core event as its coded line. The fields are the template's placeholders, in a fixed order:
    /// FN ST SL (population) · FA ST PC (deficit share) RS (famine reason ordinal) · FE ST YR (years) DD (deaths) ·
    /// XT ST SL (last population) · AR ST AT (artisans) · MG ST SL (emigrants) PC (share of the population) ·
    /// DS ST PC (share of the season's yield lost) · SO ST PC (deficit share) · SE ST YR (years).
    /// The disaster's severity is not coded, as the shipped prose does not print it (T4.21-6: 0 by construction).
    /// </summary>
    public static string Render(ChronicleEvent e)
    {
        string head = Year(e.Year) + " " + EventCode(e.Type) + " " + Settlement(e.SettlementId);
        return e.Type switch
        {
            ChronicleEventType.Founding => head + " SL" + Whole(e.Magnitude1),
            ChronicleEventType.FamineOnset => head + " PC" + Share(e.Magnitude1)
                + " RS" + ((int)e.Magnitude2).ToString(CultureInfo.InvariantCulture),
            ChronicleEventType.FamineEnd => head + " YR" + Whole(e.Magnitude1) + " DD" + Whole(e.Magnitude2),
            ChronicleEventType.Extinction => head + " SL" + Whole(e.Magnitude1),
            ChronicleEventType.FirstArtisans => head + " AT" + Whole(e.Magnitude1),
            ChronicleEventType.MigrationSurge => head + " SL" + Whole(e.Magnitude1) + " PC" + Share(e.Magnitude2),
            ChronicleEventType.Disaster => head + " PC" + Share(1.0 - e.Magnitude2),
            ChronicleEventType.FoodShortfallOnset => head + " PC" + Share(e.Magnitude1),
            ChronicleEventType.FoodShortfallEnd => head + " YR" + Whole(e.Magnitude1),
            _ => throw new InvalidOperationException($"unknown event type {e.Type}"),
        };
    }

    /// <summary>A name line <c>CODE=name</c>: the code is one to three capital letters and an integer.</summary>
    public static bool TryParseNameLine(string line, out string code, out string name)
    {
        code = "";
        name = "";
        int eq = line.IndexOf('=');
        if (eq < 2) return false;
        int i = 0;
        while (i < eq && line[i] is >= 'A' and <= 'Z') i++;
        if (i is 0 or > 3) return false;
        int digits = i;
        if (digits < eq && line[digits] == '-') digits++;
        if (digits == eq) return false;
        for (int k = digits; k < eq; k++)
            if (line[k] is < '0' or > '9') return false;
        code = line[..eq];
        name = line[(eq + 1)..];
        return true;
    }

    /// <summary>
    /// The coded session record: the event lines in event order and, for every code an event line uses that stands for a
    /// named thing (a settlement, a research node, an Age …), its name as of the moment it was first used. Names are
    /// noted only through <see cref="Note"/>, by the writer of the event, so a hidden settlement (P7) — whose events are
    /// never appended — is never named.
    /// </summary>
    public sealed class Record
    {
        private readonly List<string> _lines = [];
        private readonly List<(string Code, string Name)> _names = [];

        public IReadOnlyList<string> Lines => _lines;

        /// <summary>The name table, ordinal by code.</summary>
        public IReadOnlyList<(string Code, string Name)> Names
        {
            get
            {
                var sorted = new List<(string Code, string Name)>(_names);
                sorted.Sort((a, b) => string.CompareOrdinal(a.Code, b.Code));
                return sorted;
            }
        }

        public void Add(string line) => _lines.Add(line);

        /// <summary>Notes the name of <paramref name="code"/> (first note wins) and returns the code.</summary>
        public string Note(string code, string name)
        {
            for (int i = 0; i < _names.Count; i++)
                if (string.Equals(_names[i].Code, code, StringComparison.Ordinal)) return code;
            _names.Add((code, name));
            return code;
        }

        /// <summary>The file: the format tag, the name lines, the event lines; each line ends with \n.</summary>
        public string FileText()
        {
            var sb = new StringBuilder();
            sb.Append(FormatTag).Append('\n');
            foreach ((string code, string name) in Names) sb.Append(code).Append('=').Append(name).Append('\n');
            foreach (string line in _lines) sb.Append(line).Append('\n');
            return sb.ToString();
        }
    }
}
