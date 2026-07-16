using System.Text.Json;
using System.Text.Json.Serialization;

namespace Sim.Core.Kernel;

/// <summary>Raised on any era-table schema violation, with an actionable message
/// naming the field, the offending value, and what was allowed.</summary>
public sealed class EraTableFormatException(string message, Exception? inner = null)
    : Exception(message, inner);

/// <summary>
/// Loader for the era-pacing data file (D-006) — the project's FIRST data file,
/// and the template for every future loader: Sim.Core takes a string/Stream and
/// stays filesystem-free (the JSON itself ships in Sim.Data); rows deserialize
/// into strongly-typed records via System.Text.Json (in-box, culture-invariant);
/// every schema violation fails loudly with an actionable message.
/// Years in the file are signed calendar years (negative = BCE, positive = CE, a
/// continuous integer axis — BCE/CE labeling is presentation-only per ADR-002);
/// the first band's start is the campaign epoch, day 0.
/// </summary>
public static class EraTableLoader
{
    // JSON rows are nullable so MISSING fields are detected, not defaulted (loud
    // errors beat silent zeros).
    private sealed record BandJson(
        [property: JsonPropertyName("name")] string? Name,
        [property: JsonPropertyName("startYear")] long? StartYear,
        [property: JsonPropertyName("endYear")] long? EndYear,
        [property: JsonPropertyName("dtYears")] double? DtYears);

    private sealed record TableJson(
        [property: JsonPropertyName("bands")] List<BandJson>? Bands);

    public static EraTable Load(Stream json)
    {
        using var reader = new StreamReader(json);
        return Load(reader.ReadToEnd());
    }

    public static EraTable Load(string json)
    {
        TableJson? parsed;
        try
        {
            parsed = JsonSerializer.Deserialize<TableJson>(json);
        }
        catch (JsonException e)
        {
            throw new EraTableFormatException(
                $"era table is not valid JSON: {e.Message}", e);
        }

        if (parsed?.Bands is null || parsed.Bands.Count == 0)
            throw new EraTableFormatException(
                "era table must contain a non-empty 'bands' array of " +
                "{ name, startYear, endYear, dtYears } objects.");

        var bands = new EraTable.Band[parsed.Bands.Count];
        long epochYear = 0;

        for (int i = 0; i < parsed.Bands.Count; i++)
        {
            BandJson b = parsed.Bands[i];

            if (string.IsNullOrWhiteSpace(b.Name))
                throw new EraTableFormatException(
                    $"bands[{i}].name is missing or blank; every band requires a non-empty name.");
            if (b.StartYear is null)
                throw new EraTableFormatException(
                    $"bands[{i}] ('{b.Name}').startYear is missing; give the band's first calendar year (negative = BCE).");
            if (b.EndYear is null)
                throw new EraTableFormatException(
                    $"bands[{i}] ('{b.Name}').endYear is missing; give the year the band ends (exclusive).");
            if (b.DtYears is null)
                throw new EraTableFormatException(
                    $"bands[{i}] ('{b.Name}').dtYears is missing; give the turn length in sim years (e.g. 10 or 0.5).");

            long startYear = b.StartYear.Value, endYear = b.EndYear.Value;
            double dtYears = b.DtYears.Value;

            if (dtYears <= 0)
                throw new EraTableFormatException(
                    $"bands[{i}] ('{b.Name}').dtYears must be > 0, got {dtYears.ToString(System.Globalization.CultureInfo.InvariantCulture)}.");
            if (endYear <= startYear)
                throw new EraTableFormatException(
                    $"bands[{i}] ('{b.Name}') endYear {endYear} must be greater than startYear {startYear}.");

            if (i == 0) epochYear = startYear;
            else if (startYear != parsed.Bands[i - 1].EndYear)
                throw new EraTableFormatException(
                    $"bands[{i}] ('{b.Name}') startYear {startYear} does not equal bands[{i - 1}] " +
                    $"('{parsed.Bands[i - 1].Name}') endYear {parsed.Bands[i - 1].EndYear}; bands must be " +
                    "contiguous and in chronological order (no gaps, no overlaps).");

            double dtDaysExact = dtYears * SimClock.YearDays;
            long dtDays = (long)dtDaysExact;
            if (dtDays != dtDaysExact)
                throw new EraTableFormatException(
                    $"bands[{i}] ('{b.Name}').dtYears {dtYears.ToString(System.Globalization.CultureInfo.InvariantCulture)} " +
                    $"is {dtDaysExact.ToString(System.Globalization.CultureInfo.InvariantCulture)} days, which is not a " +
                    $"whole number of days ({SimClock.YearDays}-day year, ADR-002); use a dt that converts to integer days.");

            long startDay = (startYear - epochYear) * SimClock.YearDays;
            long endDay = (endYear - epochYear) * SimClock.YearDays;
            long spanDays = endDay - startDay;
            if (spanDays % dtDays != 0)
                throw new EraTableFormatException(
                    $"bands[{i}] ('{b.Name}') spans {spanDays} days ({startYear} to {endYear}) which is not an exact " +
                    $"multiple of its dt of {dtDays} days; band edges must land exactly on turn boundaries (ADR-002).");

            bands[i] = new EraTable.Band(b.Name!, startDay, endDay, dtDays);
        }

        return new EraTable(bands);
    }
}
