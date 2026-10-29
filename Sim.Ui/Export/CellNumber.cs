using System.Globalization;

namespace Sim.Ui.Export;

/// <summary>
/// ADR-059 — the cell text of a double the exporter formats ITSELF (an order's amount, read from the binary log; every
/// telemetry number is instead the writer's own token text, kept verbatim). The same convention as the cell grammar
/// (README §3): the shortest round-trip text, InvariantCulture, a leading <c>0.</c> written <c>.</c> and <c>-0.</c>
/// written <c>-.</c>; −0 stays <c>-0</c>; NaN and ±∞ are <c>N</c>, <c>I</c>, <c>-I</c>. <see cref="Parse"/> inverts it
/// exactly (every finite double and both zeros round-trip bit for bit).
/// </summary>
public static class CellNumber
{
    public static string Format(double d)
    {
        if (double.IsNaN(d)) return "N";
        if (double.IsPositiveInfinity(d)) return "I";
        if (double.IsNegativeInfinity(d)) return "-I";
        string s = d.ToString("R", CultureInfo.InvariantCulture);
        if (s.StartsWith("0.", StringComparison.Ordinal)) return s[1..];
        if (s.StartsWith("-0.", StringComparison.Ordinal)) return "-" + s[2..];
        return s;
    }

    public static double Parse(string t)
    {
        switch (t)
        {
            case "N": return double.NaN;
            case "I": return double.PositiveInfinity;
            case "-I": return double.NegativeInfinity;
        }
        if (t.StartsWith('.')) t = "0" + t;
        else if (t.StartsWith("-.", StringComparison.Ordinal)) t = "-0" + t[1..];
        return double.Parse(t, NumberStyles.Float, CultureInfo.InvariantCulture);
    }
}
