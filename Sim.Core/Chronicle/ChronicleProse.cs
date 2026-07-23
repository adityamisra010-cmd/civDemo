using System.Globalization;

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
            _ => throw new InvalidOperationException($"unknown event type {e.Type}"),
        };

        string line = template
            .Replace("{year}", FormatYear(e.Year))
            .Replace("{name}", names.Name(e.SettlementId));
        line = e.Type switch
        {
            ChronicleEventType.Founding => line
                .Replace("{population}", Whole(e.Magnitude1)),
            ChronicleEventType.FamineOnset => line
                .Replace("{deficitPct}", Whole(e.Magnitude1 * 100.0)),
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
            _ => line,
        };

        int open = line.IndexOf('{');
        if (open >= 0 && line.IndexOf('}', open) > open)
            throw new InvalidDataException(
                $"chronicle template for {e.Type} uses a placeholder detection never recorded: '{line}'");
        return line;
    }

    /// <summary>Sim-years since epoch, whole years (presentation may later map
    /// to BCE/CE; the annals speak in campaign years).</summary>
    private static string FormatYear(double year) =>
        ((long)Math.Round(year)).ToString(CultureInfo.InvariantCulture);

    private static string Whole(double v) =>
        ((long)Math.Round(v)).ToString(CultureInfo.InvariantCulture);
}
