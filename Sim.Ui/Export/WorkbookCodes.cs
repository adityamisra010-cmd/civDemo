using System.Globalization;
using Sim.Core.Kernel;

namespace Sim.Ui.Export;

/// <summary>
/// ADR-059 amendment 2026-10-29 (Director: "the workbook should have 0 real words in anything that is not a key or a
/// legend") — THE WORKBOOK'S CODES AND THEIR LEGEND. Only the README and the Codebook sheet carry words; every other
/// sheet (Manifest, Orders, Trace, Chronicle, Telemetry, Forensic) carries codes, numbers and identifiers (file names,
/// hashes, version tags, JSON keys). Every code is defined here, once, and written into the Codebook sheet as the legend
/// (<see cref="Legend"/>): a row per code, column A the section (<c>Sheet:part</c>, or <c>all</c> / <c>names</c> /
/// <c>texts</c>), column B the code, column D its meaning.
///
/// A string VALUE a session file holds is kept verbatim when it carries no ASCII letter or is an identifier
/// (<see cref="IsIdentifier"/>); any other is a TEXT CODE <c>QT&lt;n&gt;</c>, numbered by first
/// use in sheet order, its exact text in the legend's <c>texts</c> section (<see cref="Texts"/>). Lossless and
/// deterministic: equal texts share a code, no dictionary is iterated, ordering is ordinal.
/// </summary>
public static class WorkbookCodes
{
    /// <summary>The order-kind code of each <see cref="OrderKind"/> (the telemetry codebooks' <c>enums kind</c> codes),
    /// <c>MF</c> for the reserved kind 10 (MoveFormation, M7), <c>UK</c> for a kind this build does not know.</summary>
    public static string KindCode(int kind) => kind switch
    {
        1 => "RB", 2 => "LA", 3 => "SA", 4 => "EC", 5 => "TX", 6 => "RT", 7 => "AA", 8 => "DR", 9 => "FS",
        10 => "MF", 11 => "CC", 12 => "RA", 13 => "CE", 14 => "EX", 15 => "PR", 16 => "RF", 17 => "RE", 18 => "DF",
        _ => "UK",
    };

    /// <summary>The trace header (SessionTrace.Header) coded, column by column.</summary>
    public static string TraceColumn(string name) => name switch
    {
        "turn" => "T", "year" => "Y", "population" => "PO", "settlements" => "SN", "food" => "FD", "hash" => "H",
        _ => "",
    };

    /// <summary>One legend row.</summary>
    public readonly record struct Entry(string Section, string Code, string Meaning);

    private static readonly string[] KindNames =
    [
        "", "SetRainBias", "LaborAllocation", "SectorAllocation", "EnqueueConstruction", "SetTaxRate", "SetResearchTarget",
        "AdvanceAge", "DevelopRoads", "FoundSettlement", "MoveFormation (reserved, M7; never issued)", "CancelConstruction",
        "SetResourceAllocation", "CancelConstructionEntry", "Explore", "Prospect", "RecruitFormation", "ReequipFormation",
        "DisbandFormation",
    ];

    /// <summary>The legend: every code any non-legend sheet uses, by section, in a fixed order.</summary>
    public static IReadOnlyList<Entry> Legend { get; } = BuildLegend();

    private static List<Entry> BuildLegend()
    {
        var l = new List<Entry>();
        void E(string section, string code, string meaning) => l.Add(new Entry(section, code, meaning));

        E("all", "NP", "a file the manifest names is NOT PRESENT beside it; the row reads NP, file name, then what the forensic close "
            + "record lists of it (bytes, sha256, state). Nothing is invented.");
        E("all", "NN", "the manifest names no such file");
        E("all", "EM", "the file is present and holds no line (no order, for Orders)");
        E("all", "QT", "QT<n>: a text a session file holds that is not a code or an identifier; its exact text is the 'QT:texts' row QT<n> below");
        E("all", ">", "continuation: a row whose column A is empty and whose cell starts with > continues the cell above (drop the >, append)");

        E("Manifest:col", "KY", "key: the JSON key path (nested keys joined with '.', array items as [i]; $ = the whole document)");
        E("Manifest:col", "VL", "value: the JSON value - a string without letters, or an identifier, as is (any other string is its QT code), a number as written, a bool 1/0, null ~, {} and [] empty");
        E("Manifest:col", "TY", "JSON type of the value: S string, N number, B bool, O object, A array, Z null");
        E("Manifest:col", "$", "the key of the whole document when it is not an object with keys");
        E("type", "S", "string");
        E("type", "N", "number");
        E("type", "B", "bool (value 1 true, 0 false)");
        E("type", "O", "object (value {} when it is empty)");
        E("type", "A", "array (value [] when it is empty)");
        E("type", "Z", "null (value ~)");
        E("type", "X", "a note of the exporter, not a value of the file");
        E("type", "~", "the value null");

        E("Orders:col", "I", "index of the order in the order log (0-based, log order)");
        E("Orders:col", "T", "turn the order is stamped for");
        E("Orders:col", "PL", "issuing polity id (actor; 1 = the player's empire)");
        E("Orders:col", "K", "order kind number (OrderKind)");
        E("Orders:col", "KC", "order kind code (rows 'Orders:kind')");
        E("Orders:col", "TG", "target id as recorded");
        E("Orders:col", "TD", "target id decoded (rows 'Orders:read'; empty = no reading for this kind)");
        E("Orders:col", "AM", "amount as recorded (the writer's shortest text; see README 'cells')");
        E("Orders:col", "AD", "amount decoded (rows 'Orders:read')");
        E("Orders:col", "AB", "amount's exact IEEE-754 64-bit pattern, hex: the exact value");
        for (int k = 1; k < KindNames.Length; k++)
            E("Orders:kind", KindCode(k), k.ToString(CultureInfo.InvariantCulture) + " " + KindNames[k]);
        E("Orders:kind", "UK", "a kind number this build does not know");
        E("Orders:read", "ST", "ST<id>: settlement id (TD of LA, SA, EC, FS (source), CC, CE, EX (origin), PR, RF)");
        E("Orders:read", "S", "ST<id>/S<k>: sector k of the settlement (SA; TG = id x 8 + k): S0 farming, S1 herding, S2 extraction, "
            + "S3 crafting, S4 construction");
        E("Orders:read", "RG", "RG<id>: region id (RB)");
        E("Orders:read", "PL", "PL<id>: polity id (TX, RA)");
        E("Orders:read", "RN", "RN<key>: research node key (RT)");
        E("Orders:read", "LT", "LT-1: clear the technology lane (RT)");
        E("Orders:read", "LC", "LC-2: clear the civics lane (RT)");
        E("Orders:read", "AG", "AG<n>: Age n (AA)");
        E("Orders:read", "SK", "SK<key>: surge key (AA amount)");
        E("Orders:read", "FM", "FM<id>: formation id (RE, DF)");
        E("Orders:read", "ZR", "ZR<value>: a reserved field, its value as recorded (RT amount, DR target, EX amount, DF amount)");
        E("Orders:read", "PC", "PC<n>: a percentage (LA farming share; SA labour weight; TX tax rate; DR share of eligible "
            + "transport demand to develop; RA buildings share, roads the rest)");
        E("Orders:read", "MM", "MM<n>: rainfall bias, mm per year (RB)");
        E("Orders:read", "PJ", "PJ<id>: construction project id (EC amount; CE slot entry)");
        E("Orders:read", "CI", "CI<n>: site cell index (FS amount)");
        E("Orders:read", "QS", "QS<n>: queue slot (CC; CE as QS<slot>/PJ<project>/T<enqueued turn>)");
        E("Orders:read", "T", "T<n>: the turn the cancelled entry was enqueued (CE)");
        E("Orders:read", "NV", "NV<value>: the amount is not a packed queue-entry identity (CE)");
        E("Orders:read", "SV", "prospect method 0: survey (PR)");
        E("Orders:read", "AS", "prospect method 1: assay (PR)");
        E("Orders:read", "MT", "MT<n>: another prospect method n (PR)");
        E("Orders:read", "ID", "ID<key>: unit identity key (RF; RE target identity)");
        E("Orders:read", "CN", "cancel the pending re-equipment (RE amount 0)");

        E("Trace:col", "T", "turn");
        E("Trace:col", "Y", "sim year");
        E("Trace:col", "PO", "population");
        E("Trace:col", "SN", "settlements");
        E("Trace:col", "FD", "food");
        E("Trace:col", "H", "world hash (sha256)");

        E("Chronicle:col", "L", "event number (1-based, record order)");
        E("Chronicle:col", "EV", "the coded event line: Y<year> <event> <fields>, single spaces; the file is "
            + Sim.Core.Chronicle.ChronicleCode.FormatTag + " (a pre-amendment prose chronicle shows each line as its QT code)");
        E("Chronicle:event", "FN", "a settlement was founded: ST SL (founding people)");
        E("Chronicle:event", "FA", "famine began: ST PC (share of need unmet) RS (reason)");
        E("Chronicle:event", "FE", "famine ended: ST YR (years it lasted) DD (dead of it)");
        E("Chronicle:event", "XT", "a settlement died out: ST SL (the last people)");
        E("Chronicle:event", "AR", "first artisans: ST AT (artisans)");
        E("Chronicle:event", "MG", "migration surge: ST SL (people who left this season) PC (share of those who lived there)");
        E("Chronicle:event", "DS", "harvest failed (disaster): ST PC (share of the season's yield lost)");
        E("Chronicle:event", "SO", "food shortfall began: ST PC (share of need unmet)");
        E("Chronicle:event", "SE", "food shortfall ended: ST YR (lean years)");
        E("Chronicle:event", "RM", "research completed (the player's): PL RN");
        E("Chronicle:event", "AE", "a polity entered an Age: PL AG");
        E("Chronicle:event", "TL", "tax levy changed: PL TB (rate before) TN (rate now); TB0 = levied, TN0 = abolished");
        E("Chronicle:event", "RD", "road built: ST ST (its two ends) RC (road class)");
        E("Chronicle:event", "RU", "road upgraded: ST ST RC (the new class)");
        E("Chronicle:event", "SC", "structure completed: ST PJ");
        E("Chronicle:event", "IF", "institution founded: IN ST");
        E("Chronicle:event", "IM", "institution came to maturity: IN ST");
        E("Chronicle:event", "CP", "settlement changed hands: ST PF (from) PT (to)");
        E("Chronicle:event", "RV", "settlement revolted and threw off its ruler: ST PF");
        E("Chronicle:event", "ME", "a merchant class emerged: ST");
        E("Chronicle:event", "SR", "a class rose against the levy: CL ST");
        E("Chronicle:event", "SS", "a class's rising subsided: CL ST");
        E("Chronicle:event", "RQ", "formations re-equipped for a new Age: PL FC (count) ID (the identity they became)");
        E("Chronicle:field", "Y", "Y<year>: sim year (campaign years)");
        E("Chronicle:field", "ST", "ST<id>: settlement id; its name is the 'Chronicle:names' row ST<id>");
        E("Chronicle:field", "SL", "SL<n>: souls, a number of people");
        E("Chronicle:field", "PC", "PC<n>: n of every hundred; PC<1 = fewer than 1, PC>99 = more than 99");
        E("Chronicle:field", "RS", "RS<n>: famine reason: 0 no recorded cause, 1 a ruined harvest, 2 fields left untilled, 3 both");
        E("Chronicle:field", "YR", "YR<n>: years");
        E("Chronicle:field", "DD", "DD<n>: deaths");
        E("Chronicle:field", "AT", "AT<n>: artisans (masters and hands)");
        E("Chronicle:field", "PL", "PL<id>: polity id (PL1 = the player's empire)");
        E("Chronicle:field", "PF", "PF<id>: the polity that lost the settlement");
        E("Chronicle:field", "PT", "PT<id>: the polity that took it");
        E("Chronicle:field", "TB", "TB<n>: tax rate before, percent");
        E("Chronicle:field", "TN", "TN<n>: tax rate now, percent");
        E("Chronicle:field", "RN", "RN<key>: research node key ('Chronicle:names' row)");
        E("Chronicle:field", "AG", "AG<key>: Age key ('Chronicle:names' row)");
        E("Chronicle:field", "RC", "RC<n>: road class, transport edge type ('Chronicle:names' row)");
        E("Chronicle:field", "PJ", "PJ<id>: construction project id ('Chronicle:names' row)");
        E("Chronicle:field", "IN", "IN<n>: institution type ('Chronicle:names' row)");
        E("Chronicle:field", "CL", "CL<id>: class id ('Chronicle:names' row)");
        E("Chronicle:field", "FC", "FC<n>: number of formations");
        E("Chronicle:field", "ID", "ID<key>: unit identity key ('Chronicle:names' row)");

        E("Telemetry:col", "T", "turn");
        E("Telemetry:col", "TC", "the whole turn as one coded cell, read with the telemetry codebook above");

        E("Forensic:col", "KY", "key: the record's key path, prefixed by its record kind (run, close; a second record of a kind #2 ...)");
        E("Forensic:col", "VL", "value, as on Manifest");
        E("Forensic:col", "TY", "JSON type, as on Manifest (X = a note of the exporter)");
        E("Forensic:col", "NC", "VL of key close, type X: the file holds NO CLOSE RECORD (the session did not close cleanly, or is still open)");
        E("Forensic:file", "XF", "the session files table: one row XF.<role> per file the manifest names");
        E("Forensic:file", "FN", "file name");
        E("Forensic:file", "FS", "file state now: PS present, NP not present, RW rebuilt from an earlier workbook's Telemetry sheet");
        E("Forensic:file", "BY", "bytes now");
        E("Forensic:file", "HN", "sha256 now");
        E("Forensic:file", "HC", "sha256 the forensic close record lists");
        E("Forensic:file", "AG", "agreement: EQ same, NE differs, NC no close record, NL not listed by it, LN listed but not present "
            + "now, SF the forensic file itself (it holds the close record and lists no hash of itself)");
        E("Forensic:file", "MN", "role manifest (session-*.json)");
        E("Forensic:file", "OR", "role orders (orders-*.bin)");
        E("Forensic:file", "TR", "role trace (trace-*.csv)");
        E("Forensic:file", "CH", "role chronicle (chronicle-*.txt)");
        E("Forensic:file", "TE", "role telemetry (telemetry-*.jsonl)");
        E("Forensic:file", "FR", "role forensic (forensic-*.jsonl)");
        E("Forensic:file", "PS", "present");
        E("Forensic:file", "RW", "rebuilt from the earlier workbook's Telemetry sheet");
        E("Forensic:file", "EQ", "same sha256");
        E("Forensic:file", "NE", "the sha256 DIFFERS");
        E("Forensic:file", "NC", "no close record");
        E("Forensic:file", "NL", "not listed by the close record");
        E("Forensic:file", "LN", "listed, but the file is NOT PRESENT now");
        E("Forensic:file", "SF", "the forensic file holds the close record; it lists no hash of itself");
        return l;
    }

    /// <summary>The code of a session file's role (Forensic:file).</summary>
    public static string RoleCode(string role) => role switch
    {
        "manifest" => "MN", "orders" => "OR", "trace" => "TR", "chronicle" => "CH", "telemetry" => "TE", "forensic" => "FR",
        _ => throw new ArgumentOutOfRangeException(nameof(role), role, "not a session file role"),
    };

    /// <summary>Whether <paramref name="s"/> holds an ASCII letter.</summary>
    public static bool HasLetter(string s)
    {
        foreach (char c in s)
            if (c is (>= 'a' and <= 'z') or (>= 'A' and <= 'Z')) return true;
        return false;
    }

    /// <summary>Whether <paramref name="s"/> holds a WORD: a run of three or more ASCII letters.</summary>
    public static bool HasWord(string s)
    {
        int run = 0;
        foreach (char c in s)
        {
            run = c is (>= 'a' and <= 'z') or (>= 'A' and <= 'Z') ? run + 1 : 0;
            if (run >= 3) return true;
        }
        return false;
    }

    /// <summary>
    /// An IDENTIFIER kept verbatim although it has letters: a hash or hex value (seven or more hex digits, one case), a
    /// file name (<c>name.ext</c> of lower-case letters, digits, '.' and '-', with a session-file or JSON extension), or a
    /// version tag (<c>a/b/vN</c>, e.g. <c>session-export/v1</c>, <c>telemetry/v5</c>).
    /// </summary>
    public static bool IsIdentifier(string s) => IsHex(s) || IsFileName(s) || IsVersionTag(s);

    private static bool IsHex(string s)
    {
        if (s.Length < 7) return false;
        bool lower = false, upper = false;
        foreach (char c in s)
        {
            if (c is >= 'a' and <= 'f') lower = true;
            else if (c is >= 'A' and <= 'F') upper = true;
            else if (c is < '0' or > '9') return false;
        }
        return !(lower && upper);
    }

    private static readonly string[] FileExtensions = [".json", ".jsonl", ".bin", ".csv", ".txt", ".xlsx"];

    private static bool IsFileName(string s)
    {
        string? ext = null;
        foreach (string e in FileExtensions)
            if (s.EndsWith(e, StringComparison.Ordinal) && s.Length > e.Length) ext = e;
        if (ext is null) return false;
        return IsLowerWord(s[..^ext.Length], allowDot: true);
    }

    private static bool IsVersionTag(string s)
    {
        int slash = s.LastIndexOf('/');
        if (slash <= 0 || slash + 2 > s.Length || s[slash + 1] != 'v') return false;
        for (int i = slash + 2; i < s.Length; i++)
            if (s[i] is < '0' or > '9') return false;
        if (slash + 2 == s.Length) return false;
        foreach (string part in s[..slash].Split('/'))
            if (!IsLowerWord(part, allowDot: false)) return false;
        return true;
    }

    /// <summary>Lower-case letters and digits in pieces joined by '-' (and '.' when allowed), no empty piece.</summary>
    private static bool IsLowerWord(string s, bool allowDot)
    {
        if (s.Length == 0) return false;
        char prev = '-';
        foreach (char c in s)
        {
            bool sep = c == '-' || (allowDot && c == '.');
            if (sep && (prev == '-' || prev == '.')) return false;
            if (!sep && c is not ((>= 'a' and <= 'z') or (>= '0' and <= '9'))) return false;
            prev = c;
        }
        return prev != '-' && prev != '.';
    }

    /// <summary>
    /// The session's TEXT CODES: each distinct text gets <c>QT&lt;n&gt;</c> (n from 1, by first use); <see cref="Value"/>
    /// keeps a value without a letter, or an identifier, as it is.
    /// </summary>
    public sealed class Texts
    {
        private readonly List<string> _texts = [];
        private readonly Dictionary<string, int> _index = new(StringComparer.Ordinal);   // lookup only, never iterated

        /// <summary>The texts, in code order: QT1 is [0].</summary>
        public IReadOnlyList<string> All => _texts;

        /// <summary>The cell for a string <paramref name="s"/> a session file holds.</summary>
        public string Value(string s) => !HasLetter(s) || IsIdentifier(s) ? s : Code(s);

        /// <summary>The QT code of <paramref name="s"/>, assigning the next one on first use.</summary>
        public string Code(string s)
        {
            if (_index.TryGetValue(s, out int i)) return CodeOf(i);
            _index.Add(s, _texts.Count);
            _texts.Add(s);
            return CodeOf(_texts.Count - 1);
        }

        private static string CodeOf(int i) => "QT" + (i + 1).ToString(CultureInfo.InvariantCulture);
    }
}
