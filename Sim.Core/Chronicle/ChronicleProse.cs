using System.Globalization;
using Sim.Core.State;

namespace Sim.Core.Chronicle;

/// <summary>
/// Renders events into annal lines from the data templates. NO INVENTED
/// FACTS by construction: the only placeholders a template may use are the
/// ones this renderer binds from the event's recorded magnitudes (plus the
/// registry name and sim-year), and an unbound placeholder left in the
/// output throws — a template cannot smuggle a fact detection never recorded.
/// All formatting is culture-invariant.
/// </summary>
public static class ChronicleProse
{
    public static string Render(ChronicleEvent e, ChronicleConfig cfg, NameRegistry names)
    {
        string template = e.Type switch
        {
            ChronicleEventType.Founding => cfg.Templates.Founding,
            ChronicleEventType.FamineOnset => cfg.Templates.FamineOnset,
            ChronicleEventType.FamineEnd => cfg.Templates.FamineEnd,
            ChronicleEventType.Extinction => cfg.Templates.Extinction,
            ChronicleEventType.FirstArtisans => cfg.Templates.FirstArtisans,
            ChronicleEventType.MigrationSurge => cfg.Templates.MigrationSurge,
            ChronicleEventType.Disaster => cfg.Templates.Disaster,
            ChronicleEventType.FoodShortfallOnset => cfg.Templates.FoodShortfallOnset,
            ChronicleEventType.FoodShortfallEnd => cfg.Templates.FoodShortfallEnd,
            _ => throw new InvalidOperationException($"unknown event type {e.Type}"),
        };

        string line = template
            .Replace("{year}", FormatYear(e.Year))
            .Replace("{name}", names.Name(e.SettlementId));
        line = e.Type switch
        {
            ChronicleEventType.Founding => Count(line, "population", e.Magnitude1),
            // {reason} binds the RECORDED FamineReason ordinal (M2), so the
            // annals name the cause CR-015 made the classifier carry and cannot
            // name one detection never recorded.
            ChronicleEventType.FamineOnset => line
                .Replace("{deficitPct}", Share(e.Magnitude1))
                .Replace("{reason}", Reason(e.Magnitude2)),
            ChronicleEventType.FamineEnd =>
                Count(Count(line, "years", e.Magnitude1), "deaths", e.Magnitude2),
            ChronicleEventType.Extinction => Count(line, "lastPopulation", e.Magnitude1),
            ChronicleEventType.FirstArtisans => Count(line, "count", e.Magnitude1),
            ChronicleEventType.MigrationSurge => Count(line, "count", e.Magnitude1)
                .Replace("{sharePct}", Share(e.Magnitude2)),
            // T4.21-6: {severityPct} is still SUBSTITUTABLE, but the shipped
            // template no longer uses it. The Disaster event fires on the rising
            // edge of FoodState.IsStruck, and at canonical dt 10 with
            // durationYears 5 the row that is struck is the TAIL row, whose
            // Severity is 0 by construction (DisasterSystem writes
            // new DisasterRow(id, 0, 0.0, 0.0, 1.0, applied)) — measured on the
            // armed canonical session, 55 of 60 rising edges carry severity 0, so
            // the clause read "0 in a hundred of the fields still" on essentially
            // every canonical-era disaster. {lossPct}, from AppliedMultiplier,
            // carries the magnitude and is the one the reader can act on.
            ChronicleEventType.Disaster => line
                .Replace("{severityPct}", Share(e.Magnitude1))
                .Replace("{lossPct}", Share(1.0 - e.Magnitude2)),
            ChronicleEventType.FoodShortfallOnset => line
                .Replace("{deficitPct}", Share(e.Magnitude1)),
            ChronicleEventType.FoodShortfallEnd => Count(line, "years", e.Magnitude1),
            _ => line,
        };

        int open = line.IndexOf('{');
        if (open >= 0 && line.IndexOf('}', open) > open)
            throw new InvalidDataException(
                $"chronicle template for {e.Type} uses a placeholder detection never recorded: '{line}'");
        return line;
    }

    /// <summary>The recorded <see cref="FamineReason"/> ordinal as prose. An
    /// ordinal the enum does not cover renders as itself rather than as a
    /// guess — the renderer never invents a cause.</summary>
    private static string Reason(double ordinal) => ((int)ordinal) switch
    {
        (int)FamineReason.Disaster => "a ruined harvest",
        (int)FamineReason.Abandonment => "fields left untilled",
        (int)FamineReason.Both => "a ruined harvest and fields left untilled",
        (int)FamineReason.None => "no recorded cause",
        _ => "cause " + ((int)ordinal).ToString(CultureInfo.InvariantCulture),
    };

    /// <summary>Sim-years since epoch, whole years (presentation may later map
    /// to BCE/CE; the annals speak in campaign years).</summary>
    internal static string FormatYear(double year) =>
        ((long)Math.Round(year)).ToString(CultureInfo.InvariantCulture);

    private static string Whole(double v) =>
        ((long)Math.Round(v)).ToString(CultureInfo.InvariantCulture);

    /// <summary>
    /// A fraction as "N of every hundred" — the {...Pct} placeholders.
    /// M5 playtest (Director, 2026-10-19): the shortfall event fires on the
    /// deficit crossing ZERO (CR-015, no band — deliberately), so a real
    /// deficit of 0.0367 in a hundred rounded to "0 of every hundred measures
    /// of need went unmet" under a line that says the stores ran short. The
    /// rounding may not cross a semantic boundary the event asserts: a share
    /// above 0 that rounds to 0 reads "fewer than 1", and a share below the
    /// whole that rounds to 100 reads "more than 99". The event set is
    /// untouched; only its wording is honest.
    /// </summary>
    private static string Share(double fraction) => ShareText(fraction, "fewer than 1", "more than 99");

    /// <summary>The share rule above with the two bound texts given: the prose's words here, the coded record's
    /// <c>&lt;1</c> / <c>&gt;99</c> in <see cref="ChronicleCode"/> (ADR-059 amendment 2026-10-29) — one rule, two
    /// spellings, so the coded record and the annals can never disagree on a figure.</summary>
    internal static string ShareText(double fraction, string fewer, string more)
    {
        double pct = fraction * 100.0;
        long whole = (long)Math.Round(pct);
        if (whole == 0 && pct > 0.0) return fewer;
        if (whole == 100 && pct < 100.0) return more;
        return whole.ToString(CultureInfo.InvariantCulture);
    }

    /// <summary>
    /// Binds a count placeholder {key} to the whole number, and every
    /// {key:singular|plural} selector in the template to the form that number
    /// takes — singular exactly when the RENDERED number is 1 (so a magnitude
    /// of 0.6 that prints "1" reads singular too). M5 playtest (Director,
    /// 2026-10-19): "1 masters and hands". A selector without exactly one '|'
    /// is a malformed template and throws, like an unbound placeholder.
    /// </summary>
    private static string Count(string line, string key, double magnitude)
    {
        long n = (long)Math.Round(magnitude);
        line = line.Replace("{" + key + "}", n.ToString(CultureInfo.InvariantCulture));
        string open = "{" + key + ":";
        int at = line.IndexOf(open, StringComparison.Ordinal);
        while (at >= 0)
        {
            int close = line.IndexOf('}', at);
            if (close < 0)
                throw new InvalidDataException($"chronicle template: unterminated selector for {{{key}}} in '{line}'");
            string body = line.Substring(at + open.Length, close - at - open.Length);
            int bar = body.IndexOf('|');
            if (bar < 0 || body.IndexOf('|', bar + 1) >= 0)
                throw new InvalidDataException(
                    $"chronicle template: selector for {{{key}}} must read {{{key}:singular|plural}}: '{line}'");
            string form = n == 1 ? body.Substring(0, bar) : body.Substring(bar + 1);
            line = line.Substring(0, at) + form + line.Substring(close + 1);
            at = line.IndexOf(open, at + form.Length, StringComparison.Ordinal);
        }
        return line;
    }
}
