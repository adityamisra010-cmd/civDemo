using System.Globalization;
using System.Text;
using System.Text.Json;

namespace Sim.Ui.Export;

/// <summary>
/// A JSON value as the telemetry codec sees it (ADR-059): NUMBER TOKENS ARE KEPT AS THEIR TEXT, exactly as the writer
/// printed them, so no number is ever re-formatted (README §5.3 — the writer's exact double text, −0 kept distinct); an
/// object keeps its keys in document order in two parallel lists (no dictionary, so nothing iterates one).
/// </summary>
public abstract class JVal
{
}

/// <summary>A JSON number, as its token text.</summary>
public sealed class JNum(string text) : JVal
{
    public string Text { get; } = text;
}

/// <summary>A JSON string (unescaped).</summary>
public sealed class JStr(string value) : JVal
{
    public string Value { get; } = value;
}

/// <summary>A JSON boolean.</summary>
public sealed class JBool : JVal
{
    public static readonly JBool True = new(true), False = new(false);
    public bool Value { get; }
    private JBool(bool value) => Value = value;
    public static JBool Of(bool value) => value ? True : False;
}

/// <summary>JSON null.</summary>
public sealed class JNull : JVal
{
    public static readonly JNull Instance = new();
    private JNull() { }
}

/// <summary>A JSON array.</summary>
public sealed class JArr : JVal
{
    public List<JVal> Items { get; }
    public JArr() => Items = [];
    public JArr(List<JVal> items) => Items = items;
}

/// <summary>A JSON object: keys in document order. Lookup is a linear scan (a telemetry object has at most ~25 keys).
/// A repeated key replaces the earlier value in place, as Python's json.loads does (the prototype's reading).</summary>
public sealed class JObj : JVal
{
    public List<string> Keys { get; } = [];
    public List<JVal> Values { get; } = [];

    public int IndexOf(string key)
    {
        for (int i = 0; i < Keys.Count; i++)
            if (string.Equals(Keys[i], key, StringComparison.Ordinal)) return i;
        return -1;
    }

    public JVal? Get(string key)
    {
        int i = IndexOf(key);
        return i < 0 ? null : Values[i];
    }

    public void Set(string key, JVal value)
    {
        int i = IndexOf(key);
        if (i >= 0) Values[i] = value;
        else
        {
            Keys.Add(key);
            Values.Add(value);
        }
    }

    public void RemoveAt(int index)
    {
        Keys.RemoveAt(index);
        Values.RemoveAt(index);
    }
}

/// <summary>Parse and print JSON text the way the telemetry writer (System.Text.Json's Utf8JsonWriter, compact, default
/// encoder) prints it — the C# twin of the prototype's <c>parse_json</c> / <c>to_json</c>.</summary>
public static class JsonText
{
    /// <summary>Parses one JSON text. Number tokens keep their text; NaN/Infinity tokens are refused (JSON has none).</summary>
    public static JVal Parse(string text)
    {
        byte[] bytes = Encoding.UTF8.GetBytes(text);
        var reader = new Utf8JsonReader(bytes, new JsonReaderOptions { MaxDepth = 256 });
        if (!reader.Read()) throw new JsonException("empty JSON text");
        JVal v = ReadValue(ref reader);
        if (reader.Read()) throw new JsonException("trailing content after the JSON value");
        return v;
    }

    private static JVal ReadValue(ref Utf8JsonReader reader)
    {
        switch (reader.TokenType)
        {
            case JsonTokenType.StartObject:
            {
                var obj = new JObj();
                while (reader.Read() && reader.TokenType != JsonTokenType.EndObject)
                {
                    string key = reader.GetString()!;
                    reader.Read();
                    obj.Set(key, ReadValue(ref reader));
                }
                return obj;
            }
            case JsonTokenType.StartArray:
            {
                var arr = new JArr();
                while (reader.Read() && reader.TokenType != JsonTokenType.EndArray)
                    arr.Items.Add(ReadValue(ref reader));
                return arr;
            }
            case JsonTokenType.String:
                return new JStr(reader.GetString()!);
            case JsonTokenType.Number:
                return new JNum(Encoding.UTF8.GetString(reader.ValueSpan));
            case JsonTokenType.True:
                return JBool.True;
            case JsonTokenType.False:
                return JBool.False;
            case JsonTokenType.Null:
                return JNull.Instance;
            default:
                throw new JsonException("unexpected JSON token " + reader.TokenType.ToString());
        }
    }

    /// <summary>Compact JSON text of a value, escaped as Utf8JsonWriter's default encoder escapes it:
    /// <c>"</c> and <c>&lt; &gt; &amp; ' + `</c> as uppercase <c>\uXXXX</c>; backslash as <c>\\</c>; \n \r \t \b \f
    /// short; every other control character and everything outside printable ASCII as <c>\uXXXX</c> (UTF-16 units).</summary>
    public static string Serialize(JVal value)
    {
        var sb = new StringBuilder();
        Write(sb, value);
        return sb.ToString();
    }

    private static void Write(StringBuilder sb, JVal v)
    {
        switch (v)
        {
            case JObj o:
                sb.Append('{');
                for (int i = 0; i < o.Keys.Count; i++)
                {
                    if (i > 0) sb.Append(',');
                    EscapeString(sb, o.Keys[i]);
                    sb.Append(':');
                    Write(sb, o.Values[i]);
                }
                sb.Append('}');
                break;
            case JArr a:
                sb.Append('[');
                for (int i = 0; i < a.Items.Count; i++)
                {
                    if (i > 0) sb.Append(',');
                    Write(sb, a.Items[i]);
                }
                sb.Append(']');
                break;
            case JNum n:
                sb.Append(n.Text);
                break;
            case JStr s:
                EscapeString(sb, s.Value);
                break;
            case JBool b:
                sb.Append(b.Value ? "true" : "false");
                break;
            case JNull:
                sb.Append("null");
                break;
            default:
                throw new InvalidOperationException("not a JSON value: " + v.GetType().Name);
        }
    }

    /// <summary>The string escaping of <see cref="Serialize"/> (the prototype's <c>enc_str</c>).</summary>
    public static void EscapeString(StringBuilder sb, string s)
    {
        sb.Append('"');
        foreach (char ch in s)
        {
            switch (ch)
            {
                case '\\': sb.Append("\\\\"); break;
                case '<' or '>' or '&' or '\'' or '+' or '`' or '"':
                    AppendU(sb, ch);
                    break;
                case '\n': sb.Append("\\n"); break;
                case '\r': sb.Append("\\r"); break;
                case '\t': sb.Append("\\t"); break;
                case '\b': sb.Append("\\b"); break;
                case '\f': sb.Append("\\f"); break;
                default:
                    if (ch < 0x20 || ch > 0x7E) AppendU(sb, ch);
                    else sb.Append(ch);
                    break;
            }
        }
        sb.Append('"');
    }

    private static void AppendU(StringBuilder sb, char ch) =>
        sb.Append("\\u").Append(((int)ch).ToString("X4", CultureInfo.InvariantCulture));

    /// <summary>Ordered, exact equality: numbers by text (−0 ≠ 0), objects key by key in order.</summary>
    public static bool Same(JVal? a, JVal? b)
    {
        if (ReferenceEquals(a, b)) return true;
        if (a is null || b is null) return false;
        if (a is JNum na) return b is JNum nb && string.Equals(na.Text, nb.Text, StringComparison.Ordinal);
        if (a is JStr sa) return b is JStr sb && string.Equals(sa.Value, sb.Value, StringComparison.Ordinal);
        if (a is JBool ba) return b is JBool bb && ba.Value == bb.Value;
        if (a is JNull) return b is JNull;
        if (a is JArr aa)
        {
            if (b is not JArr ab || aa.Items.Count != ab.Items.Count) return false;
            for (int i = 0; i < aa.Items.Count; i++)
                if (!Same(aa.Items[i], ab.Items[i])) return false;
            return true;
        }
        if (a is JObj oa)
        {
            if (b is not JObj ob || oa.Keys.Count != ob.Keys.Count) return false;
            for (int i = 0; i < oa.Keys.Count; i++)
                if (!string.Equals(oa.Keys[i], ob.Keys[i], StringComparison.Ordinal) || !Same(oa.Values[i], ob.Values[i]))
                    return false;
            return true;
        }
        return false;
    }
}
