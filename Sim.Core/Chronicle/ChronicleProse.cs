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
            ChronicleEventType.Founding => line
                .Replace("{population}", Whole(e.Magnitude1)),
            // {reason} binds the RECORDED FamineReason ordinal (M2), so the
            // annals name the cause CR-015 made the classifier carry and cannot
            // name one detection never recorded.
            ChronicleEventType.FamineOnset => line
                .Replace("{deficitPct}", Whole(e.Magnitude1 * 100.0))
                .Replace("{reason}", Reason(e.Magnitude2)),
            ChronicleEventType.FamineEnd => line
                .Replace("{years}", Whole(e.Magnitude1))
                .Replace("{deaths}", Whole(e.Magnitude2)),
            ChronicleEventType.Extinction => line
                .Replace("{lastPopulation}", Whole(e.Magnitude1)),
            ChronicleEventType.FirstArtisans => line
                .Replace("{count}", Whole(e.Magnitude1)),
            ChronicleEventType.MigrationSurge => line
                .Replace("{count}", Whole(e.Magnitude1))
                .Replace("{sharePct}", Whole(e.Magnitude2 * 100.0)),
            ChronicleEventType.Disaster => line
                .Replace("{severityPct}", Whole(e.Magnitude1 * 100.0))
                .Replace("{lossPct}", Whole((1.0 - e.Magnitude2) * 100.0)),
            ChronicleEventType.FoodShortfallOnset => line
                .Replace("{deficitPct}", Whole(e.Magnitude1 * 100.0)),
            ChronicleEventType.FoodShortfallEnd => line
                .Replace("{years}", Whole(e.Magnitude1)),
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
    private static string FormatYear(double year) =>
        ((long)Math.Round(year)).ToString(CultureInfo.InvariantCulture);

    private static string Whole(double v) =>
        ((long)Math.Round(v)).ToString(CultureInfo.InvariantCulture);
}
