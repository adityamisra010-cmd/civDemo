using System.Globalization;
using System.Numerics;
using System.Text;

namespace Sim.Ui.Export;

/// <summary>
/// ADR-059 — THE TELEMETRY CELL CODEC: one telemetry JSONL line ⇄ one coded cell of positional tuples, the C# port of
/// <c>docs/m6-session-export/prototype/codec.py</c> (cell grammar v4 of the export README §3, plus codebook v5's
/// optional keys). The golden test holds it to the prototype's cells byte for byte.
///
/// LOSSLESS BY CONSTRUCTION, CHECKED ANYWAY. Every number is the writer's own token text (never re-formatted), a
/// default is compared AS TEXT (so −0 stays distinct from 0), and anything the codebook cannot express is written as a
/// <c>!</c> JSON-literal escape. <see cref="Decode"/> rebuilds the writer's line byte for byte, and the exporter
/// decodes every cell it writes (<see cref="EncodeChecked"/>) and refuses on any difference — there is no raw fallback.
/// No dictionary is iterated and no float is used: objects are ordered key lists, ids are parsed as BigInteger.
/// </summary>
public static class TelemetryCellCodec
{
    // ---------------------------------------------------------------------------------------- public --

    /// <summary>The codebook a line will be encoded with (by its schema tag), or a named refusal.</summary>
    public static TelemetryCodebook CodebookOf(string line)
    {
        JObj doc = ParseLine(line);
        return CodebookOfDoc(doc);
    }

    /// <summary>One telemetry line → one coded cell (not yet split for the workbook).</summary>
    public static string Encode(string line)
    {
        JObj doc = ParseLine(line);
        TelemetryCodebook cb = CodebookOfDoc(doc);
        try
        {
            return new Encoder(cb).EncodeLine(doc);
        }
        catch (Exception e) when (e is not SessionExportException)
        {
            throw new SessionExportException(SessionExportException.TelemetryShape, e.Message, e);
        }
    }

    /// <summary>One coded cell → the writer's telemetry line, byte for byte.</summary>
    public static string Decode(string cell)
    {
        try
        {
            return JsonText.Serialize(new Decoder().DecodeCell(cell));
        }
        catch (Exception e) when (e is not SessionExportException)
        {
            throw new SessionExportException(SessionExportException.CellUndecodable, e.Message, e);
        }
    }

    /// <summary>The exporter's self-checked encode: the cell, decoded, must give back <paramref name="line"/> exactly.</summary>
    public static string EncodeChecked(string line, string describedAs)
    {
        string cell = Encode(line);
        string back;
        try
        {
            back = Decode(cell);
        }
        catch (SessionExportException e)
        {
            throw new SessionExportException(SessionExportException.RoundTripMismatch,
                $"{describedAs}: the coded cell does not decode ({e.Message})", e);
        }
        if (!string.Equals(back, line, StringComparison.Ordinal))
            throw new SessionExportException(SessionExportException.RoundTripMismatch,
                $"{describedAs}: the coded cell decodes to a different line (first difference at character {FirstDifference(back, line)})");
        return cell;
    }

    /// <summary>
    /// D-066 A17 — split a coded cell into workbook cells of at most <paramref name="limit"/> characters, at a <c>|</c>
    /// only. The first part is the cell's head; every further part is a CONTINUATION: the reserved marker <c>&gt;</c>
    /// followed by the <c>|</c> that was cut. Greedy: each part takes as many whole segments as fit. A single segment
    /// over the limit cannot be split and is a named refusal — never truncated.
    /// </summary>
    public static IReadOnlyList<string> Split(string cell, int limit, string mark = ">")
    {
        string[] segs = cell.Split('|');
        var parts = new List<string>();
        var cur = new StringBuilder(segs[0]);
        if (cur.Length > limit)
            throw new SessionExportException(SessionExportException.CellTooLarge,
                $"the turn segment is {cur.Length} characters, over the cell limit {limit}");
        for (int i = 1; i < segs.Length; i++)
        {
            if (cur.Length + 1 + segs[i].Length <= limit) cur.Append('|').Append(segs[i]);
            else
            {
                parts.Add(cur.ToString());
                cur.Clear().Append(mark).Append('|').Append(segs[i]);
                if (cur.Length > limit)
                    throw new SessionExportException(SessionExportException.CellTooLarge,
                        $"settlement segment {i} is {segs[i].Length} characters, over the cell limit {limit}");
            }
        }
        parts.Add(cur.ToString());
        return parts;
    }

    /// <summary>The inverse of <see cref="Split"/>: drop each continuation's marker and append it to the cell above.</summary>
    public static string Join(IReadOnlyList<string> parts, string mark = ">")
    {
        var sb = new StringBuilder(parts[0]);
        for (int i = 1; i < parts.Count; i++)
        {
            if (!parts[i].StartsWith(mark, StringComparison.Ordinal))
                throw new SessionExportException(SessionExportException.CellUndecodable, $"part {i} is not a continuation cell");
            sb.Append(parts[i], mark.Length, parts[i].Length - mark.Length);
        }
        return sb.ToString();
    }

    internal static int FirstDifference(string a, string b)
    {
        int n = Math.Min(a.Length, b.Length);
        for (int i = 0; i < n; i++)
            if (a[i] != b[i]) return i;
        return n;
    }

    // ------------------------------------------------------------------------------------- shared --

    private static JObj ParseLine(string line)
    {
        JVal doc;
        try
        {
            doc = JsonText.Parse(line);
        }
        catch (Exception e) when (e is System.Text.Json.JsonException or InvalidOperationException or ArgumentException)
        {
            throw new SessionExportException(SessionExportException.TelemetryShape, "not one JSON object: " + e.Message, e);
        }
        return CheckKeys(doc, ["schema", "turn", "settlements"], "line");
    }

    private static TelemetryCodebook CodebookOfDoc(JObj doc)
    {
        JVal schema = doc.Values[0];
        TelemetryCodebook? cb = schema is JStr s ? TelemetryCodebook.ForSchema(s.Value) : null;
        if (cb is null)
            throw new SessionExportException(SessionExportException.TelemetrySchemaUnknown,
                "no codebook for telemetry schema " + JsonText.Serialize(schema)
                + " (one codebook per schema: a writer change needs a new codebook before export works)");
        return cb;
    }

    private static JObj CheckKeys(JVal obj, IReadOnlyList<string> keys, string where)
    {
        if (obj is not JObj o || o.Keys.Count != keys.Count) throw Shape(obj, where);
        for (int i = 0; i < keys.Count; i++)
            if (!string.Equals(o.Keys[i], keys[i], StringComparison.Ordinal)) throw Shape(obj, where);
        return o;
    }

    private static SessionExportException Shape(JVal obj, string where) =>
        new(SessionExportException.TelemetryShape, "key order/shape differs at " + where + ": "
            + (obj is JObj o ? "[" + string.Join(", ", o.Keys) + "]" : obj.GetType().Name));

    private static readonly string[] SepEscIn = [",", ";", "/", "|", " ", "~", "*", "_"];
    private static readonly string[] SepEscOut = ["\\u002c", "\\u003b", "\\u002f", "\\u007c", "\\u0020", "\\u007e", "\\u002a", "\\u005f"];

    /// <summary>The '!' escape: a JSON literal. Inside strings the cell separators are \u-escaped; outside strings the
    /// structural ',' is written as ';' (JSON never has ';' outside strings). The prototype's <c>bang</c>/<c>_scan</c>.</summary>
    internal static string Bang(JVal v)
    {
        string s = JsonText.Serialize(v);
        var sb = new StringBuilder("!");
        bool inStr = false, prevBs = false;
        foreach (char ch in s)
        {
            if (inStr)
            {
                int k = Array.IndexOf(SepEscIn, ch.ToString());
                sb.Append(k >= 0 ? SepEscOut[k] : ch.ToString());
            }
            else sb.Append(ch == ',' ? ';' : ch);
            if (ch == '"' && !prevBs) inStr = !inStr;
            prevBs = inStr && ch == '\\' && !prevBs;
        }
        return sb.ToString();
    }

    internal static JVal Unbang(string text)
    {
        var sb = new StringBuilder();
        bool inStr = false, prevBs = false;
        for (int i = 1; i < text.Length; i++)
        {
            char ch = text[i];
            sb.Append(ch == ';' && !inStr ? ',' : ch);
            if (ch == '"' && !prevBs) inStr = !inStr;
            prevBs = inStr && ch == '\\' && !prevBs;
        }
        return JsonText.Parse(sb.ToString());
    }

    internal static bool IsInt(string t)
    {
        int i = 0;
        if (t.Length > 0 && t[0] == '-') i = 1;
        if (i >= t.Length) return false;
        for (; i < t.Length; i++)
            if (t[i] < '0' || t[i] > '9') return false;
        return true;
    }

    internal static string? EncDouble(TelemetryCodebook cb, JVal leaf)
    {
        if (leaf is JNum n)
        {
            string x = n.Text;
            if (x.StartsWith("0.", StringComparison.Ordinal)) return x[1..];
            if (x.StartsWith("-0.", StringComparison.Ordinal)) return "-" + x[2..];
            return x;
        }
        if (leaf is JStr s)
            foreach ((string name, string code) in cb.NonFinite)
                if (string.Equals(name, s.Value, StringComparison.Ordinal)) return code;
        return null;
    }

    internal static JVal DecDouble(TelemetryCodebook cb, string t)
    {
        foreach ((string name, string code) in cb.NonFinite)
            if (string.Equals(code, t, StringComparison.Ordinal)) return new JStr(name);
        if (t.StartsWith('.')) return new JNum("0" + t);
        if (t.StartsWith("-.", StringComparison.Ordinal)) return new JNum("-0" + t[1..]);
        return new JNum(t);
    }

    internal static string? NameOf(TelemetryCodebook cb, string map, BigInteger id)
    {
        CbNameMap m = cb.NameMap(map);
        foreach ((BigInteger i, string name) in m.Ids)
            if (i == id) return name;
        return m.Fallback?.Replace("%d", id.ToString(CultureInfo.InvariantCulture), StringComparison.Ordinal);
    }

    // -------------------------------------------------------------------------------------- context --

    private sealed class Ctx(JObj? t, JObj? s, JObj? r, int? row)
    {
        public JObj? T { get; } = t;
        public JObj? S { get; } = s;
        public JObj? R { get; } = r;
        public int? Row { get; } = row;
    }

    /// <summary>A field left EMPTY in a cell: resolved to its default on demand (the prototype's Pending).</summary>
    private sealed class JPending(CbDefault spec, Ctx ctx) : JVal
    {
        public CbDefault Spec { get; } = spec;
        public Ctx Ctx { get; } = ctx;
        public bool Busy { get; set; }
        public JVal? Value { get; set; }
    }

    /// <summary>An OPTIONAL key that is not in the line (codebook v5).</summary>
    private sealed class JAbsent : JVal
    {
        public static readonly JAbsent Instance = new();
    }

    private static JVal? Ident(JVal? x) => x;

    private static JVal? Resolve(JVal? x)
    {
        if (x is not JPending p) return x;
        if (p.Value is not null) return p.Value;
        if (p.Busy) throw new InvalidOperationException("cyclic default");
        p.Busy = true;
        JVal v = DefaultValue(p.Spec, p.Ctx, Resolve, CurrentDecodeCodebook!)
                 ?? throw new InvalidOperationException("default unavailable: " + p.Spec);
        p.Value = v;
        return v;
    }

    [ThreadStatic] private static TelemetryCodebook? CurrentDecodeCodebook;

    private static JVal? GetPath(Ctx ctx, string path, Func<JVal?, JVal?> resolve)
    {
        // {r} / {r+N}: the row index (+N)
        var sb = new StringBuilder();
        for (int i = 0; i < path.Length; i++)
        {
            if (path[i] == '{')
            {
                int close = path.IndexOf('}', i);
                string e = path[(i + 1)..close];
                int add = e.StartsWith("r+", StringComparison.Ordinal) ? int.Parse(e[2..], CultureInfo.InvariantCulture) : 0;
                sb.Append((ctx.Row!.Value + add).ToString(CultureInfo.InvariantCulture));
                i = close;
            }
            else sb.Append(path[i]);
        }
        string[] parts = sb.ToString().Split('.');
        JVal? cur = parts[0] switch { "T" => ctx.T, "S" => ctx.S, "R" => ctx.R, _ => throw new InvalidOperationException(path) };
        for (int k = 1; k < parts.Length; k++)
        {
            if (cur is null) return null;
            cur = resolve(cur);
            if (cur is JObj o) cur = o.Get(parts[k]);
            else if (cur is JArr a)
            {
                int idx = int.Parse(parts[k], CultureInfo.InvariantCulture);
                cur = idx >= 0 && idx < a.Items.Count ? a.Items[idx] : null;
            }
            else return null;
        }
        return cur is null ? null : resolve(cur);
    }

    private static JVal? DefaultValue(CbDefault? spec, Ctx ctx, Func<JVal?, JVal?> resolve, TelemetryCodebook cb)
    {
        switch (spec)
        {
            case null: return null;
            case CbConst k: return k.Leaf;
            case CbRef r: return GetPath(ctx, r.Path, resolve);
            case CbPos p: return ctx.Row is int at && at < p.Leaves.Length ? p.Leaves[at] : null;
            case CbIdx: return new JNum(ctx.Row!.Value.ToString(CultureInfo.InvariantCulture));
            case CbName nm:
            {
                JVal? idv = GetPath(ctx, "R." + nm.IdKey, resolve);
                if (idv is not JNum n || !IsInt(n.Text)) return null;
                string? name = NameOf(cb, nm.Map, BigInteger.Parse(n.Text, NumberStyles.AllowLeadingSign, CultureInfo.InvariantCulture));
                return name is null ? null : new JStr(name);
            }
            case CbRowDefault rd:
            {
                foreach ((int rowAt, CbDefault sub) in rd.Rows)
                    if (ctx.Row == rowAt) return DefaultValue(sub, ctx, resolve, cb);
                return DefaultValue(rd.Fallback, ctx, resolve, cb);
            }
            case CbElems el:
            {
                var outList = new List<JVal>();
                foreach (CbDefault e in el.Elements)
                {
                    JVal? v = DefaultValue(e, ctx, resolve, cb);
                    if (v is null) return null;
                    outList.Add(v);
                }
                return new JArr(outList);
            }
            case CbAbsent: return JAbsent.Instance;
            case CbOwnAttr:
            {
                JVal? sid = GetPath(ctx, "S.settlement", resolve);
                JVal? lst = GetPath(ctx, "S.migration.allAttractiveness", resolve);
                if (lst is not JArr arr) return null;
                foreach (JVal rowv in arr.Items)
                {
                    if (resolve(rowv) is not JObj row) return null;
                    if (JsonText.Same(resolve(row.Get("settlement")), sid)) return resolve(row.Get("value"));
                }
                return null;
            }
            default: throw new InvalidOperationException("unknown default " + spec);
        }
    }

    // -------------------------------------------------------------------------------------- encoder --

    private sealed class Encoder(TelemetryCodebook cb)
    {
        public string EncodeLine(JObj doc)
        {
            JVal sch = doc.Values[0];
            string head = "";
            foreach ((string tag, string code) in cb.SchemaCodes)
                if (sch is JStr s && string.Equals(s.Value, tag, StringComparison.Ordinal)) head = code;
            if (doc.Values[1] is not JObj T) throw Shape(doc.Values[1], "turn");
            if (doc.Values[2] is not JArr setts)
                throw new SessionExportException(SessionExportException.TelemetryShape, "settlements not a list");

            // line-level attractiveness table: the most common per-record list (first maximum wins, first-seen order)
            var candKeys = new List<string>();
            var candCount = new List<int>();
            var candList = new List<JVal>();
            foreach (JVal sv in setts.Items)
            {
                if (sv is not JObj so || so.Get("migration") is not JObj mig || mig.Get("allAttractiveness") is not { } lst)
                    continue;
                string key = JsonText.Serialize(lst);
                int at = candKeys.IndexOf(key);
                if (at < 0)
                {
                    candKeys.Add(key);
                    candCount.Add(1);
                    candList.Add(lst);
                }
                else candCount[at]++;
            }
            JVal lineA = new JArr();
            int best = -1;
            for (int i = 0; i < candCount.Count; i++)
                if (best < 0 || candCount[i] > candCount[best]) best = i;
            if (best >= 0) lineA = candList[best];

            var tctx = new Ctx(T, null, null, null);
            var parts = new List<string> { head };
            parts.AddRange(Segment(cb.TurnTemplate, cb.TurnSections, cb.TurnSectionOrder, T, tctx, "turn", [], []));
            bool lineANonEmpty = lineA is not JArr la || la.Items.Count > 0;
            if (lineANonEmpty)
            {
                string? a = Table(cb.AttrRow with { Code = "A" }, lineA, tctx, new JArr());
                if (a is not null) parts.Add(a);
            }
            var segs = new List<string> { string.Join(" ", parts) };

            JVal? tOrders = T.Get("orders");
            for (int i = 0; i < setts.Items.Count; i++)
            {
                JVal s = setts.Items[i];
                JVal? sid = s is JObj so ? so.Get("settlement") : null;
                var mine = new JArr();
                if (tOrders is JArr ta)
                    foreach (JVal o in ta.Items)
                        if (o is JObj oo && JsonText.Same(oo.Get("settlement"), sid)) mine.Items.Add(o);
                if (s is not JObj sObj) throw Shape(s, "settlements[" + i.ToString(CultureInfo.InvariantCulture) + "]");
                var ctx = new Ctx(T, sObj, null, null);
                segs.Add(string.Join(" ", Segment(cb.SettlementTemplate, cb.SettlementSections, cb.SettlementSectionOrder,
                    sObj, ctx, "settlements[" + i.ToString(CultureInfo.InvariantCulture) + "]", ["Ma", "O"], [lineA, mine])));
            }
            return string.Join("|", segs);
        }

        // leaves: per section code, the (key, value) pairs collected from the template walk
        private sealed class Collected
        {
            public readonly List<string> LeafCodes = [];
            public readonly List<JObj> LeafVals = [];
            public readonly List<string> TableCodes = [];
            public readonly List<JVal> TableVals = [];
            public readonly List<string> OptCodes = [];
            public readonly List<bool> OptPresent = [];

            public JObj Leaves(string code)
            {
                int i = LeafCodes.IndexOf(code);
                if (i >= 0) return LeafVals[i];
                var o = new JObj();
                LeafCodes.Add(code);
                LeafVals.Add(o);
                return o;
            }
        }

        private static void Collect(CbEntry[] template, JObj obj, string where, Collected c)
        {
            var present = new List<string>();
            foreach (CbEntry e in template)
            {
                bool optional = e is CbOptObj or CbOptLeaf;
                if (!(optional && obj.IndexOf(e.Key) < 0)) present.Add(e.Key);
            }
            CheckKeys(obj, present, where);
            foreach (CbEntry e in template)
            {
                switch (e)
                {
                    case CbLeaf l:
                        c.Leaves(l.Code).Set(l.Key, obj.Get(l.Key)!);
                        break;
                    case CbOptLeaf ol:
                        c.Leaves(ol.Code).Set(ol.Key, obj.Get(ol.Key) ?? JAbsent.Instance);
                        break;
                    case CbObj ob:
                        if (obj.Get(ob.Key) is not JObj sub) throw Shape(obj.Get(ob.Key)!, where + "." + ob.Key);
                        Collect(ob.Entries, sub, where + "." + ob.Key, c);
                        break;
                    case CbTable t:
                        c.TableCodes.Add(t.Code);
                        c.TableVals.Add(obj.Get(t.Key)!);
                        break;
                    case CbOptObj oo:
                        c.OptCodes.Add(oo.Code);
                        if (obj.Get(oo.Key) is { } v)
                        {
                            c.OptPresent.Add(true);
                            if (v is not JObj vo) throw Shape(v, where + "." + oo.Key);
                            Collect(oo.Entries, vo, where + "." + oo.Key, c);
                        }
                        else c.OptPresent.Add(false);
                        break;
                }
            }
        }

        private List<string> Segment(CbEntry[] template, CbSection[] secs, string[] order, JObj obj, Ctx ctx, string where,
            string[] defaultCodes, JVal[] defaultTables)
        {
            var c = new Collected();
            Collect(template, obj, where, c);
            var outList = new List<string>();
            foreach (string code in order)
            {
                CbSection? sec = null;
                foreach (CbSection s in secs)
                    if (string.Equals(s.Code, code, StringComparison.Ordinal)) sec = s;
                if (sec is null) continue;
                if (!sec.IsTable)
                {
                    int oi = c.OptCodes.IndexOf(code);
                    if (oi >= 0 && !c.OptPresent[oi])
                    {
                        outList.Add(code + "~");
                        continue;
                    }
                    int li = c.LeafCodes.IndexOf(code);
                    JObj vals = li >= 0 ? c.LeafVals[li] : new JObj();
                    string txt = FieldsText(sec.Fields, key => vals.Get(key)
                        ?? throw new SessionExportException(SessionExportException.TelemetryShape, $"{where}: no field {key} for section {code}"), ctx);
                    if (code == "#") outList.Add("#" + txt);
                    else if (txt.Length > 0) outList.Add(code + ":" + txt);
                }
                else
                {
                    int ti = c.TableCodes.IndexOf(code);
                    if (ti < 0) throw new SessionExportException(SessionExportException.TelemetryShape, $"{where}: no table {code}");
                    int di = Array.IndexOf(defaultCodes, code);
                    JVal tdef = di >= 0 ? defaultTables[di] : new JArr();
                    string? t = Table(sec, c.TableVals[ti], ctx, tdef);
                    if (t is not null) outList.Add(t);
                }
            }
            return outList;
        }

        private string? Table(CbSection sec, JVal arr, Ctx ctx, JVal tableDefault)
        {
            if (arr is not JArr a)
                throw new SessionExportException(SessionExportException.TelemetryShape, "not an array: " + sec.Code);
            if (JsonText.Same(a, tableDefault)) return null;
            if (a.Items.Count == 0) return sec.Code + "~";   // empty, but the default was not empty
            var rows = new List<string>();
            bool allEmpty = true;
            for (int r = 0; r < a.Items.Count; r++)
            {
                JObj row = CheckKeys(a.Items[r], sec.RowKeys, sec.Code + "[" + r.ToString(CultureInfo.InvariantCulture) + "]");
                string txt = FieldsText(sec.Fields, key => row.Get(key)!, new Ctx(ctx.T, ctx.S, row, r));
                rows.Add(txt);
                if (txt.Length > 0) allEmpty = false;
            }
            if (allEmpty) return sec.Code + ":*" + rows.Count.ToString(CultureInfo.InvariantCulture);
            return sec.Code + ":" + string.Join("/", rows);
        }

        private string FieldsText(CbField[] fields, Func<string, JVal> get, Ctx ctx)
        {
            var texts = new List<string>();
            foreach (CbField f in fields)
            {
                JVal v = f.Key2 is null ? get(f.Key) : new JArr([get(f.Key), get(f.Key2)]);
                JVal? d = f.Key2 is null ? DefaultValue(f.Default, ctx, Ident, cb) : null;
                if (d is not null && JsonText.Same(v, d)) texts.Add("");
                else texts.Add(EncVal(f, v));
            }
            while (texts.Count > 0 && texts[^1].Length == 0) texts.RemoveAt(texts.Count - 1);
            return string.Join(",", texts);
        }

        private string EncVal(CbField f, JVal v)
        {
            if (v is JAbsent)
                throw new SessionExportException(SessionExportException.TelemetryShape, "optional key " + f.Key + " has no absent default");
            string? t = null;
            switch (f.Type)
            {
                case CbType.Int:
                    if (v is JNum n && IsInt(n.Text)) t = n.Text;
                    break;
                case CbType.Dbl:
                    t = EncDouble(cb, v);
                    break;
                case CbType.Bool:
                    if (v is JBool b) t = b.Value ? "1" : "0";
                    break;
                case CbType.Enum:
                    if (v is JStr es)
                        foreach ((string name, string code) in cb.EnumCodes(f.Sub!))
                            if (string.Equals(name, es.Value, StringComparison.Ordinal)) t = code;
                    break;
                case CbType.DblArr:
                    if (v is JArr da)
                    {
                        var ps = new List<string>();
                        bool ok = true;
                        foreach (JVal x in da.Items)
                        {
                            string? p = EncDouble(cb, x);
                            if (p is null) ok = false;
                            else ps.Add(p);
                        }
                        if (ok) t = ps.Count > 0 ? string.Join(";", ps) : "~";
                    }
                    break;
                case CbType.Legs:
                {
                    var pair = (JArr)v;
                    return EncLegs(pair.Items[0], pair.Items[1]) ?? Bang(pair);
                }
            }
            return t ?? Bang(v);   // Const and Name have no short form: default or escape
        }

        private string? EncLegs(JVal srcV, JVal snkV)
        {
            if (srcV is not JArr src || snkV is not JArr snk || src.Items.Count != snk.Items.Count) return null;
            if (src.Items.Count == 0) return "~";
            var legs = new List<string>();
            for (int i = 0; i < src.Items.Count; i++)
            {
                JObj? a = LegRow(src.Items[i]), b = LegRow(snk.Items[i]);
                if (a is null || b is null) return null;
                if (!JsonText.Same(a.Values[0], b.Values[0])) return null;
                string t = ((JNum)a.Values[0]).Text;
                string au = ((JNum)a.Values[2]).Text, bu = ((JNum)b.Values[2]).Text;
                if (au != "0") t += "+" + au;
                if (bu != "0") t += "-" + bu;
                legs.Add(t);
            }
            return string.Join(";", legs);
        }

        private JObj? LegRow(JVal row)
        {
            if (row is not JObj o || o.Keys.Count != 3 || o.Keys[0] != "reason" || o.Keys[1] != "name" || o.Keys[2] != "units")
                return null;
            if (o.Values[0] is not JNum r || !IsInt(r.Text)) return null;
            if (o.Values[2] is not JNum u || !IsInt(u.Text)) return null;
            string? name = NameOf(cb, "reason", BigInteger.Parse(r.Text, NumberStyles.AllowLeadingSign, CultureInfo.InvariantCulture));
            if (name is null || o.Values[1] is not JStr s || !string.Equals(s.Value, name, StringComparison.Ordinal)) return null;
            return o;
        }
    }

    // -------------------------------------------------------------------------------------- decoder --

    private sealed class Decoder
    {
        private TelemetryCodebook _cb = TelemetryCodebook.V4;

        public JObj DecodeCell(string cell)
        {
            string[] segs = cell.Split('|');
            int sp = segs[0].IndexOf(' ');
            string head = sp < 0 ? segs[0] : segs[0][..sp];
            string? rest = sp < 0 ? null : segs[0][(sp + 1)..];
            TelemetryCodebook? cb = TelemetryCodebook.ForHead(head);
            if (cb is null)
                throw new SessionExportException(SessionExportException.CellUndecodable, "unknown cell head " + Trunc(head));
            _cb = cb;
            CurrentDecodeCodebook = cb;
            JVal schema = JNull.Instance;
            foreach ((string tag, string code) in cb.SchemaCodes)
                if (string.Equals(code, head, StringComparison.Ordinal)) schema = new JStr(tag);

            Parsed tparsed = rest is null ? new Parsed() : ParseSegment(rest);
            var T = new JObj();
            var tctx = new Ctx(T, null, null, null);
            var tp = new List<(JObj, string, string)>();
            Build(cb.TurnTemplate, cb.TurnSections, tparsed, tctx, tp, new Holder(), T);
            DeepResolve(T);
            JVal lineA = new JArr();
            if (tparsed.Find("A") is { Absent: false } a)
            {
                lineA = DecTable(cb.AttrRow, a.Payload, tctx);
                DeepResolve(lineA);
            }
            var setts = new JArr();
            for (int i = 1; i < segs.Length; i++)
            {
                Parsed parsed = ParseSegment(segs[i]);
                var S = new JObj();
                var ctx = new Ctx(T, S, null, null);
                var spend = new List<(JObj, string, string)>();
                Build(cb.SettlementTemplate, cb.SettlementSections, parsed, ctx, spend, new Holder(), S);
                JVal? sid = Resolve(S.Get("settlement"));
                foreach ((JObj obj, string key, string code) in spend)
                {
                    if (code == "Ma") obj.Set(key, DeepCopy(lineA));
                    else if (code == "O")
                    {
                        var mine = new JArr();
                        if (T.Get("orders") is JArr ta)
                            foreach (JVal o in ta.Items)
                                if (o is JObj oo && JsonText.Same(oo.Get("settlement"), sid)) mine.Items.Add(DeepCopy(o));
                        obj.Set(key, mine);
                    }
                }
                DeepResolve(S);
                setts.Items.Add(S);
            }
            StripAbsent(T);
            foreach (JVal s in setts.Items) StripAbsent(s);
            var doc = new JObj();
            doc.Set("schema", schema);
            doc.Set("turn", T);
            doc.Set("settlements", setts);
            return doc;
        }

        private static string Trunc(string s) => s.Length <= 20 ? s : s[..20];

        private sealed class Parsed
        {
            public readonly List<(string Code, bool Absent, string Payload)> Items = [];

            /// <summary>The LAST section with this code (a dict's last-wins, as the prototype parses).</summary>
            public (string Code, bool Absent, string Payload)? Find(string code)
            {
                for (int i = Items.Count - 1; i >= 0; i--)
                    if (string.Equals(Items[i].Code, code, StringComparison.Ordinal)) return Items[i];
                return null;
            }
        }

        private sealed class Holder
        {
            public readonly List<string> Codes = [];
            public readonly List<JObj> Vals = [];
        }

        private static Parsed ParseSegment(string seg)
        {
            var p = new Parsed();
            foreach (string tok in seg.Split(' '))
            {
                // ^(#|[A-Za-z]+)(:|~)?(.*)$
                int i;
                string code;
                if (tok.StartsWith('#'))
                {
                    code = "#";
                    i = 1;
                }
                else
                {
                    i = 0;
                    while (i < tok.Length && char.IsAsciiLetter(tok[i])) i++;
                    if (i == 0) throw new FormatException("bad section " + Trunc(tok));
                    code = tok[..i];
                }
                char? mark = i < tok.Length && (tok[i] == ':' || tok[i] == '~') ? tok[i] : null;
                string payload = tok[(i + (mark is null ? 0 : 1))..];
                if (code == "#") p.Items.Add(("#", false, (mark?.ToString() ?? "") + payload));
                else if (mark == '~') p.Items.Add((code, true, ""));
                else p.Items.Add((code, false, payload));
            }
            return p;
        }

        private void Build(CbEntry[] template, CbSection[] secs, Parsed parsed, Ctx ctx, List<(JObj, string, string)> tablePending,
            Holder holder, JObj obj)
        {
            foreach (CbEntry e in template)
            {
                switch (e)
                {
                    case CbLeaf or CbOptLeaf:
                    {
                        string code = e is CbLeaf l ? l.Code : ((CbOptLeaf)e).Code;
                        int hi = holder.Codes.IndexOf(code);
                        JObj vals;
                        if (hi >= 0) vals = holder.Vals[hi];
                        else
                        {
                            vals = new JObj();
                            holder.Codes.Add(code);
                            holder.Vals.Add(vals);
                            var p = parsed.Find(code);
                            string payload = p is { Absent: false } pv ? pv.Payload : "";
                            DecTupleFields(Sec(secs, code).Fields, payload, ctx, vals);
                        }
                        obj.Set(e.Key, vals.Get(e.Key) ?? throw new FormatException("no field " + e.Key));
                        break;
                    }
                    case CbObj ob:
                    {
                        var sub = new JObj();
                        Build(ob.Entries, secs, parsed, ctx, tablePending, holder, sub);
                        obj.Set(ob.Key, sub);
                        break;
                    }
                    case CbTable t:
                    {
                        var p = parsed.Find(t.Code);
                        if (p is null)
                        {
                            obj.Set(t.Key, new JArr());
                            tablePending.Add((obj, t.Key, t.Code));
                        }
                        else if (p.Value.Absent) obj.Set(t.Key, new JArr());
                        else obj.Set(t.Key, DecTable(Sec(secs, t.Code), p.Value.Payload, ctx));
                        break;
                    }
                    case CbOptObj oo:
                    {
                        var p = parsed.Find(oo.Code);
                        if (p is { Absent: true }) continue;
                        var sub = new JObj();
                        Build(oo.Entries, secs, parsed, ctx, tablePending, holder, sub);
                        obj.Set(oo.Key, sub);
                        break;
                    }
                }
            }
        }

        private static CbSection Sec(CbSection[] secs, string code)
        {
            foreach (CbSection s in secs)
                if (string.Equals(s.Code, code, StringComparison.Ordinal)) return s;
            throw new FormatException("no section " + code);
        }

        private void DecTupleFields(CbField[] fields, string payload, Ctx ctx, JObj target)
        {
            string[] texts = payload.Length > 0 ? payload.Split(',') : [];
            if (texts.Length > fields.Length) throw new FormatException("too many fields");
            for (int i = 0; i < fields.Length; i++)
            {
                CbField f = fields[i];
                string t = i < texts.Length ? texts[i] : "";
                if (t.Length == 0)
                {
                    if (f.Default is null) throw new FormatException("empty field without default: " + f.Key);
                    target.Set(f.Key, new JPending(f.Default, ctx));
                }
                else
                {
                    JVal v = DecVal(f, t);
                    if (f.Key2 is not null)
                    {
                        var pair = (JArr)v;
                        target.Set(f.Key, pair.Items[0]);
                        target.Set(f.Key2, pair.Items[1]);
                    }
                    else target.Set(f.Key, v);
                }
            }
        }

        private JVal DecTable(CbSection sec, string payload, Ctx ctx)
        {
            string[] rowsTxt;
            if (payload.StartsWith('*'))
            {
                int n = int.Parse(payload[1..], NumberStyles.None, CultureInfo.InvariantCulture);
                rowsTxt = new string[n];
                Array.Fill(rowsTxt, "");
            }
            else rowsTxt = payload.Split('/');
            var rows = new JArr();
            for (int r = 0; r < rowsTxt.Length; r++)
            {
                var vals = new JObj();
                var row = new JObj();
                var rctx = new Ctx(ctx.T, ctx.S, row, r);
                DecTupleFields(sec.Fields, rowsTxt[r], rctx, vals);
                foreach (string k in sec.RowKeys) row.Set(k, vals.Get(k) ?? throw new FormatException("no row field " + k));
                rows.Items.Add(row);
            }
            return rows;
        }

        private JVal DecVal(CbField f, string t)
        {
            if (t.StartsWith('!'))
            {
                JVal v = Unbang(t);
                if (f.Type == CbType.Legs && (v is not JArr a || a.Items.Count < 2)) throw new FormatException("legs escape is not a pair");
                return v;
            }
            switch (f.Type)
            {
                case CbType.Int:
                    if (!IsInt(t)) throw new FormatException("bad int " + Trunc(t));
                    return new JNum(t);
                case CbType.Dbl:
                    return DecDouble(_cb, t);
                case CbType.Bool:
                    return JBool.Of(t == "1");
                case CbType.Enum:
                    foreach ((string name, string code) in _cb.EnumCodes(f.Sub!))
                        if (string.Equals(code, t, StringComparison.Ordinal)) return new JStr(name);
                    throw new FormatException("unknown " + f.Sub + " code " + Trunc(t));
                case CbType.DblArr:
                {
                    var arr = new JArr();
                    if (t != "~")
                        foreach (string x in t.Split(';')) arr.Items.Add(DecDouble(_cb, x));
                    return arr;
                }
                case CbType.Legs:
                    return DecLegs(t);
                default:
                    throw new FormatException("no short form for type " + f.Type + ": " + Trunc(t));
            }
        }

        private JArr DecLegs(string t)
        {
            var src = new JArr();
            var snk = new JArr();
            if (t != "~")
                foreach (string leg in t.Split(';'))
                {
                    (string r, string? a, string? b) = ParseLeg(leg);
                    string? name = NameOf(_cb, "reason", BigInteger.Parse(r, NumberStyles.AllowLeadingSign, CultureInfo.InvariantCulture));
                    JVal nm = name is null ? JNull.Instance : new JStr(name);
                    src.Items.Add(LegObj(r, nm, a ?? "0"));
                    snk.Items.Add(LegObj(r, nm, b ?? "0"));
                }
            return new JArr([src, snk]);
        }

        private static JObj LegObj(string reason, JVal name, string units)
        {
            var o = new JObj();
            o.Set("reason", new JNum(reason));
            o.Set("name", name);
            o.Set("units", new JNum(units));
            return o;
        }

        /// <summary>^(-?\d+)(?:\+(-?\d+))?(?:-(-?\d+))?$</summary>
        private static (string, string?, string?) ParseLeg(string leg)
        {
            int i = 0;
            string r = Digits(leg, ref i, allowSign: true) ?? throw new FormatException("bad leg " + Trunc(leg));
            string? a = null, b = null;
            if (i < leg.Length && leg[i] == '+')
            {
                i++;
                a = Digits(leg, ref i, allowSign: true) ?? throw new FormatException("bad leg " + Trunc(leg));
            }
            if (i < leg.Length && leg[i] == '-')
            {
                i++;
                b = Digits(leg, ref i, allowSign: true) ?? throw new FormatException("bad leg " + Trunc(leg));
            }
            if (i != leg.Length) throw new FormatException("bad leg " + Trunc(leg));
            return (r, a, b);
        }

        private static string? Digits(string s, ref int i, bool allowSign)
        {
            int start = i;
            if (allowSign && i < s.Length && s[i] == '-') i++;
            int d = i;
            while (i < s.Length && s[i] >= '0' && s[i] <= '9') i++;
            if (i == d)
            {
                i = start;
                return null;
            }
            return s[start..i];
        }

        private static JVal? DeepResolve(JVal? x)
        {
            x = Resolve(x);
            if (x is JObj o)
                for (int i = 0; i < o.Values.Count; i++) o.Values[i] = DeepResolve(o.Values[i])!;
            else if (x is JArr a)
                for (int i = 0; i < a.Items.Count; i++) a.Items[i] = DeepResolve(a.Items[i])!;
            return x;
        }

        private static JVal DeepCopy(JVal x)
        {
            switch (x)
            {
                case JObj o:
                {
                    var c = new JObj();
                    for (int i = 0; i < o.Keys.Count; i++) c.Set(o.Keys[i], DeepCopy(o.Values[i]));
                    return c;
                }
                case JArr a:
                {
                    var c = new JArr();
                    foreach (JVal v in a.Items) c.Items.Add(DeepCopy(v));
                    return c;
                }
                default:
                    return x;
            }
        }

        private static void StripAbsent(JVal x)
        {
            if (x is JObj o)
            {
                for (int i = o.Values.Count - 1; i >= 0; i--)
                    if (o.Values[i] is JAbsent) o.RemoveAt(i);
                foreach (JVal v in o.Values) StripAbsent(v);
            }
            else if (x is JArr a)
                foreach (JVal v in a.Items) StripAbsent(v);
        }
    }
}
