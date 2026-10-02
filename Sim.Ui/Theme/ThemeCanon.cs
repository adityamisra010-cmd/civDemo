using System.Globalization;
using System.Reflection;
using System.Text;
using Sim.Ui.Render;
using Rgba = Sim.Ui.Art.ParchmentPalette.Rgba;

namespace Sim.Ui.Theme;

/// <summary>
/// THE CANONICAL TEXT OF A THEME — every token as one "path=value" line, in declaration order, with
/// invariant formatting (colours "#RRGGBB[AA]", doubles round-trip "R", enums by name). Two themes
/// are the same presentation iff their canonical texts are byte-identical; the determinism and
/// save/load tests compare these bytes, and <see cref="Hash"/> is their SHA-256.
/// </summary>
public static class ThemeCanon
{
    public static string Describe(EraTheme theme)
    {
        var sb = new StringBuilder();
        Write(sb, "", theme);
        return sb.ToString();
    }

    /// <summary>SHA-256 of the canonical text, lower-case hex.</summary>
    public static string Hash(EraTheme theme) =>
        Convert.ToHexStringLower(System.Security.Cryptography.SHA256.HashData(Encoding.UTF8.GetBytes(Describe(theme))));

    private static void Write(StringBuilder sb, string path, object? value)
    {
        switch (value)
        {
            case null:
                sb.Append(path).Append("=null\n");
                return;
            case Rgba c:
                sb.Append(path).Append('=').Append(ThemeColor.Code(c)).Append('\n');
                return;
            case TextStyle t:
                sb.Append(path).Append('=').Append(t.Face).Append('/')
                  .Append(t.Weight.ToString(CultureInfo.InvariantCulture)).Append('/')
                  .Append(t.TrackingEm.ToString("R", CultureInfo.InvariantCulture)).Append('/')
                  .Append(t.Case).Append('\n');
                return;
            case double d:
                sb.Append(path).Append('=').Append(d.ToString("R", CultureInfo.InvariantCulture)).Append('\n');
                return;
            case int i:
                sb.Append(path).Append('=').Append(i.ToString(CultureInfo.InvariantCulture)).Append('\n');
                return;
            case bool b:
                sb.Append(path).Append('=').Append(b ? "true" : "false").Append('\n');
                return;
            case string s:
                sb.Append(path).Append('=').Append(s).Append('\n');
                return;
            case Enum e:
                sb.Append(path).Append('=').Append(e.ToString()).Append('\n');
                return;
        }

        // A token record: its public instance properties in declaration (metadata) order.
        Type type = value.GetType();
        PropertyInfo[] props = type.GetProperties(BindingFlags.Public | BindingFlags.Instance);
        Array.Sort(props, (a, b) => a.MetadataToken.CompareTo(b.MetadataToken));
        foreach (PropertyInfo p in props)
        {
            if (p.GetIndexParameters().Length > 0) continue;
            string child = path.Length == 0 ? p.Name : path + "." + p.Name;
            Write(sb, child, p.GetValue(value));
        }
    }
}
