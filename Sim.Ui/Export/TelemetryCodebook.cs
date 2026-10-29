using System.Numerics;
using System.Text.Json;

namespace Sim.Ui.Export;

/// <summary>The value type of one codebook field (the prototype's type letters).</summary>
public enum CbType
{
    /// <summary><c>i</c>: an integer literal.</summary>
    Int,
    /// <summary><c>d</c>: a double (number text, or the NaN/Infinity strings).</summary>
    Dbl,
    /// <summary><c>b</c>: a boolean, 1/0.</summary>
    Bool,
    /// <summary><c>e:NAME</c>: a string from a closed set, written as its code.</summary>
    Enum,
    /// <summary><c>c</c>: constant text (default or the ! escape only).</summary>
    Const,
    /// <summary><c>n:MAP</c>: a name derived from an id (default or the ! escape only).</summary>
    Name,
    /// <summary><c>da</c>: an array of doubles joined by ;.</summary>
    DblArr,
    /// <summary><c>legs</c>: a stock's sources and sinks.</summary>
    Legs,
}

/// <summary>A default spec (the prototype's <c>('k', …)</c>, <c>('r', …)</c>, …): what an EMPTY field means.</summary>
public abstract record CbDefault;
/// <summary>A constant leaf.</summary>
public sealed record CbConst(JVal Leaf) : CbDefault;
/// <summary>The value of another field: <c>T.</c> the turn, <c>S.</c> this settlement, <c>R.</c> this row;
/// <c>{r}</c> / <c>{r+N}</c> = the row index (+N).</summary>
public sealed record CbRef(string Path) : CbDefault;
/// <summary>A constant chosen by row position.</summary>
public sealed record CbPos(JVal[] Leaves) : CbDefault;
/// <summary>The row position itself.</summary>
public sealed record CbIdx : CbDefault;
/// <summary>The codebook name of the id in field <see cref="IdKey"/> of the same row.</summary>
public sealed record CbName(string IdKey, string Map) : CbDefault;
/// <summary>A per-row override of a fallback default.</summary>
public sealed record CbRowDefault((int Row, CbDefault Spec)[] Rows, CbDefault Fallback) : CbDefault;
/// <summary>Element-wise defaults of a double array.</summary>
public sealed record CbElems(CbDefault[] Elements) : CbDefault;
/// <summary>This settlement's value in its attractiveness list.</summary>
public sealed record CbOwnAttr : CbDefault;
/// <summary>Codebook v5: the key is not in the line (an OPTIONAL key).</summary>
public sealed record CbAbsent : CbDefault;

/// <summary>One field of a section: its JSON key (two keys for legs), its type and its default (null = always written).</summary>
public sealed record CbField(string Key, string? Key2, CbType Type, string? Sub, CbDefault? Default);

/// <summary>A section: a tuple, or a table with its row key order and table default.</summary>
public sealed record CbSection(string Code, bool IsTable, CbField[] Fields, string[] RowKeys, string TableDefault);

/// <summary>A JSON template entry.</summary>
public abstract record CbEntry(string Key);
/// <summary>A leaf of a tuple section.</summary>
public sealed record CbLeaf(string Key, string Code) : CbEntry(Key);
/// <summary>An OPTIONAL leaf (codebook v5): the key may be missing.</summary>
public sealed record CbOptLeaf(string Key, string Code) : CbEntry(Key);
/// <summary>A nested object.</summary>
public sealed record CbObj(string Key, CbEntry[] Entries) : CbEntry(Key);
/// <summary>An array (a table section).</summary>
public sealed record CbTable(string Key, string Code) : CbEntry(Key);
/// <summary>An OPTIONAL object (presence is meaningful).</summary>
public sealed record CbOptObj(string Key, string Code, CbEntry[] Entries) : CbEntry(Key);

/// <summary>A name map: ids to names, with an optional fallback pattern (<c>reason-%d</c>).</summary>
public sealed record CbNameMap(string Map, string? Fallback, (BigInteger Id, string Name)[] Ids);

/// <summary>
/// ADR-059 — ONE CODEBOOK PER TELEMETRY SCHEMA (README §5.4; D-066 A15). The codebook is not transcribed here: it is
/// the machine-readable twin (<c>docs/m6-session-export/codebook-telemetry-vN.json</c>) that
/// <c>prototype/make_codebook.py</c> generates from <c>prototype/spec.py</c> — the single source of the table — embedded
/// in Sim.Ui with the TSV the Codebook sheet shows. v4 reads telemetry/v2, v3 and v4 lines (cell heads v2/v3/v4);
/// v5 reads telemetry/v5 (head v5).
/// </summary>
public sealed class TelemetryCodebook
{
    public string Version { get; }
    public (string Tag, string Code)[] SchemaCodes { get; }
    public (string Name, string Code)[] NonFinite { get; }
    public CbNameMap[] Names { get; }
    public (string Enum, (string Name, string Code)[] Codes)[] Enums { get; }
    public CbSection[] TurnSections { get; }
    public CbSection AttrRow { get; }
    public CbSection[] SettlementSections { get; }
    public CbEntry[] TurnTemplate { get; }
    public CbEntry[] SettlementTemplate { get; }
    public string[] TurnSectionOrder { get; }
    public string[] SettlementSectionOrder { get; }
    public int CellLimit { get; }
    public string ContinuationMark { get; }

    /// <summary>The codebook's TSV (the Codebook sheet), exactly as published.</summary>
    public string Tsv { get; }

    private TelemetryCodebook(string json, string tsv)
    {
        Tsv = tsv;
        using JsonDocument doc = JsonDocument.Parse(json);
        JsonElement r = doc.RootElement;
        Version = r.GetProperty("codebook").GetString()!;
        SchemaCodes = Pairs(r.GetProperty("schemaCodes"));
        NonFinite = Pairs(r.GetProperty("nonFinite"));
        var names = new List<CbNameMap>();
        foreach (JsonElement m in r.GetProperty("names").EnumerateArray())
        {
            var ids = new List<(BigInteger, string)>();
            foreach (JsonElement p in m.GetProperty("ids").EnumerateArray())
                ids.Add((new BigInteger(p[0].GetInt64()), p[1].GetString()!));
            JsonElement fb = m.GetProperty("fallback");
            names.Add(new CbNameMap(m.GetProperty("map").GetString()!,
                fb.ValueKind == JsonValueKind.Null ? null : fb.GetString(), ids.ToArray()));
        }
        Names = names.ToArray();
        var enums = new List<(string, (string, string)[])>();
        foreach (JsonElement e in r.GetProperty("enums").EnumerateArray())
            enums.Add((e.GetProperty("enum").GetString()!, Pairs(e.GetProperty("codes"))));
        Enums = enums.ToArray();
        TurnSections = Sections(r.GetProperty("turnSections"));
        AttrRow = Section(r.GetProperty("attrRow"));
        SettlementSections = Sections(r.GetProperty("settlementSections"));
        TurnTemplate = Template(r.GetProperty("turnTemplate"));
        SettlementTemplate = Template(r.GetProperty("settlementTemplate"));
        TurnSectionOrder = Strings(r.GetProperty("turnSectionOrder"));
        SettlementSectionOrder = Strings(r.GetProperty("settlementSectionOrder"));
        CellLimit = r.GetProperty("cellLimit").GetInt32();
        ContinuationMark = r.GetProperty("continuationMark").GetString()!;
    }

    public static TelemetryCodebook V4 { get; } = Load("v4");
    public static TelemetryCodebook V5 { get; } = Load("v5");

    /// <summary>The codebook of a schema tag (telemetry/v2, v3, v4 → v4; telemetry/v5 → v5), or null.</summary>
    public static TelemetryCodebook? ForSchema(string tag)
    {
        foreach (TelemetryCodebook cb in new[] { V5, V4 })
            foreach ((string t, string _) in cb.SchemaCodes)
                if (string.Equals(t, tag, StringComparison.Ordinal)) return cb;
        return null;
    }

    /// <summary>The codebook of a cell's first token (v2, v3, v4 → v4; v5 → v5), or null.</summary>
    public static TelemetryCodebook? ForHead(string head)
    {
        foreach (TelemetryCodebook cb in new[] { V5, V4 })
            foreach ((string _, string c) in cb.SchemaCodes)
                if (string.Equals(c, head, StringComparison.Ordinal)) return cb;
        return null;
    }

    public CbSection? TurnSection(string code) => Find(TurnSections, code);
    public CbSection? SettlementSection(string code) => Find(SettlementSections, code);

    public CbNameMap NameMap(string map)
    {
        foreach (CbNameMap m in Names)
            if (string.Equals(m.Map, map, StringComparison.Ordinal)) return m;
        throw new InvalidOperationException("no name map " + map);
    }

    public (string Name, string Code)[] EnumCodes(string name)
    {
        foreach ((string e, (string, string)[] codes) in Enums)
            if (string.Equals(e, name, StringComparison.Ordinal)) return codes;
        throw new InvalidOperationException("no enum " + name);
    }

    private static CbSection? Find(CbSection[] sections, string code)
    {
        foreach (CbSection s in sections)
            if (string.Equals(s.Code, code, StringComparison.Ordinal)) return s;
        return null;
    }

    private static TelemetryCodebook Load(string version) =>
        new(Resource("Sim.Ui.codebook-telemetry-" + version + ".json"), Resource("Sim.Ui.codebook-telemetry-" + version + ".tsv"));

    private static string Resource(string name)
    {
        using Stream? s = typeof(TelemetryCodebook).Assembly.GetManifestResourceStream(name)
            ?? throw new InvalidOperationException("embedded resource missing: " + name);
        using var reader = new StreamReader(s, new System.Text.UTF8Encoding(false));
        return reader.ReadToEnd();
    }

    // ------------------------------------------------------------------ parsing the generated JSON --

    private static (string, string)[] Pairs(JsonElement a)
    {
        var o = new List<(string, string)>();
        foreach (JsonElement p in a.EnumerateArray()) o.Add((p[0].GetString()!, p[1].GetString()!));
        return o.ToArray();
    }

    private static string[] Strings(JsonElement a)
    {
        var o = new List<string>();
        foreach (JsonElement p in a.EnumerateArray()) o.Add(p.GetString()!);
        return o.ToArray();
    }

    private static CbSection[] Sections(JsonElement a)
    {
        var o = new List<CbSection>();
        foreach (JsonElement s in a.EnumerateArray()) o.Add(Section(s));
        return o.ToArray();
    }

    private static CbSection Section(JsonElement s)
    {
        bool table = s.GetProperty("kind").GetString() == "table";
        var fields = new List<CbField>();
        foreach (JsonElement f in s.GetProperty("fields").EnumerateArray())
        {
            string key;
            string? key2 = null;
            if (f[0].ValueKind == JsonValueKind.Array)
            {
                key = f[0][0].GetString()!;
                key2 = f[0][1].GetString()!;
            }
            else key = f[0].GetString()!;
            (CbType type, string? sub) = Type(f[1].GetString()!);
            fields.Add(new CbField(key, key2, type, sub, Default(f[2])));
        }
        return new CbSection(s.GetProperty("code").GetString()!, table, fields.ToArray(),
            table ? Strings(s.GetProperty("rowKeys")) : [],
            table ? s.GetProperty("tableDefault").GetString()! : "");
    }

    private static (CbType, string?) Type(string t) => t switch
    {
        "i" => (CbType.Int, null),
        "d" => (CbType.Dbl, null),
        "b" => (CbType.Bool, null),
        "c" => (CbType.Const, null),
        "da" => (CbType.DblArr, null),
        "legs" => (CbType.Legs, null),
        _ when t.StartsWith("e:", StringComparison.Ordinal) => (CbType.Enum, t[2..]),
        _ when t.StartsWith("n:", StringComparison.Ordinal) => (CbType.Name, t[2..]),
        _ => throw new InvalidOperationException("unknown codebook type " + t),
    };

    private static JVal Leaf(JsonElement l)
    {
        string kind = l[0].GetString()!;
        return kind switch
        {
            "n" => new JNum(l[1].GetString()!),
            "s" => new JStr(l[1].GetString()!),
            "b" => JBool.Of(l[1].GetBoolean()),
            _ => throw new InvalidOperationException("unknown leaf kind " + kind),
        };
    }

    private static CbDefault? Default(JsonElement d)
    {
        if (d.ValueKind == JsonValueKind.Null) return null;
        string k = d[0].GetString()!;
        switch (k)
        {
            case "k": return new CbConst(Leaf(d[1]));
            case "r": return new CbRef(d[1].GetString()!);
            case "pos":
            {
                var leaves = new List<JVal>();
                foreach (JsonElement l in d[1].EnumerateArray()) leaves.Add(Leaf(l));
                return new CbPos(leaves.ToArray());
            }
            case "idx": return new CbIdx();
            case "own_attr": return new CbOwnAttr();
            case "absent": return new CbAbsent();
            case "nm": return new CbName(d[1].GetString()!, d[2].GetString()!);
            case "rowd":
            {
                var rows = new List<(int, CbDefault)>();
                foreach (JsonElement p in d[1].EnumerateArray()) rows.Add((p[0].GetInt32(), Default(p[1])!));
                return new CbRowDefault(rows.ToArray(), Default(d[2])!);
            }
            case "elems":
            {
                var els = new List<CbDefault>();
                foreach (JsonElement e in d[1].EnumerateArray()) els.Add(Default(e)!);
                return new CbElems(els.ToArray());
            }
            default: throw new InvalidOperationException("unknown default spec " + k);
        }
    }

    private static CbEntry[] Template(JsonElement a)
    {
        var o = new List<CbEntry>();
        foreach (JsonElement e in a.EnumerateArray())
        {
            string kind = e[0].GetString()!;
            string key = e[1].GetString()!;
            o.Add(kind switch
            {
                "leaf" => new CbLeaf(key, e[2].GetString()!),
                "optleaf" => new CbOptLeaf(key, e[2].GetString()!),
                "obj" => new CbObj(key, Template(e[2])),
                "table" => new CbTable(key, e[2].GetString()!),
                "opt" => new CbOptObj(key, e[2].GetString()!, Template(e[3])),
                _ => throw new InvalidOperationException("unknown template entry " + kind),
            });
        }
        return o.ToArray();
    }
}
