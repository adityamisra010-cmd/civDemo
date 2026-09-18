using System.Text.Json;
using System.Text.Json.Serialization;

namespace Sim.Core.Chronicle;

/// <summary>Name phonology for one (placeholder) culture: syllables are
/// onset + nucleus (+ coda). All TUNE data (chronicle.json).</summary>
public sealed record PhonologyConfig(
    [property: JsonPropertyName("onsets"), JsonRequired] string[] Onsets,
    [property: JsonPropertyName("nuclei"), JsonRequired] string[] Nuclei,
    [property: JsonPropertyName("codas"), JsonRequired] string[] Codas,
    [property: JsonPropertyName("minSyllables"), JsonRequired] int MinSyllables,
    [property: JsonPropertyName("maxSyllables"), JsonRequired] int MaxSyllables);

/// <summary>
/// The data-driven event thresholds (chronicle.json, TUNE).
///
/// T4.21-5 (CR-015) RETIRED <c>famineOnsetDeficit</c> and <c>famineEndDeficit</c>.
/// They were the chronicle's OWN definition of famine — a deficit band — and
/// under "famine is exceptional" the annals do not get to keep a second
/// definition beside the simulation's. FoodState decides; the chronicle reads.
/// What is left is a genuine chronicle threshold: how big an exodus is worth
/// writing down, which is an editorial question about prose and not a claim
/// about the world.
/// </summary>
public sealed record ChronicleThresholds(
    [property: JsonPropertyName("migrationSurgeFraction"), JsonRequired] double MigrationSurgeFraction);

/// <summary>Prose templates, one per event type. Placeholders are substituted
/// from recorded magnitudes only — the renderer throws on any unknown
/// placeholder, so a template cannot invent a fact.</summary>
public sealed record ChronicleTemplates(
    [property: JsonPropertyName("founding"), JsonRequired] string Founding,
    [property: JsonPropertyName("famineOnset"), JsonRequired] string FamineOnset,
    [property: JsonPropertyName("famineEnd"), JsonRequired] string FamineEnd,
    [property: JsonPropertyName("extinction"), JsonRequired] string Extinction,
    [property: JsonPropertyName("firstArtisans"), JsonRequired] string FirstArtisans,
    [property: JsonPropertyName("migrationSurge"), JsonRequired] string MigrationSurge,
    // T4.21-5: the three events CR-015 adds to the annals.
    [property: JsonPropertyName("disaster"), JsonRequired] string Disaster,
    [property: JsonPropertyName("foodShortfallOnset"), JsonRequired] string FoodShortfallOnset,
    [property: JsonPropertyName("foodShortfallEnd"), JsonRequired] string FoodShortfallEnd);

public sealed record ChronicleConfig(
    [property: JsonPropertyName("phonology"), JsonRequired] PhonologyConfig Phonology,
    [property: JsonPropertyName("thresholds"), JsonRequired] ChronicleThresholds Thresholds,
    [property: JsonPropertyName("templates"), JsonRequired] ChronicleTemplates Templates);

public static class ChronicleConfigLoader
{
    private static readonly JsonSerializerOptions Options = new()
    {
        ReadCommentHandling = JsonCommentHandling.Skip,
        AllowTrailingCommas = false,
    };

    public static ChronicleConfig Load(Stream stream)
    {
        ChronicleConfig cfg = JsonSerializer.Deserialize<ChronicleConfig>(stream, Options)
            ?? throw new InvalidDataException("chronicle.json deserialized to null");
        PhonologyConfig p = cfg.Phonology;
        if (p.Onsets.Length == 0 || p.Nuclei.Length == 0 || p.Codas.Length == 0)
            throw new InvalidDataException("chronicle.phonology: onsets/nuclei/codas must be non-empty");
        if (p.MinSyllables < 1 || p.MaxSyllables < p.MinSyllables)
            throw new InvalidDataException(
                $"chronicle.phonology: syllable range [{p.MinSyllables}, {p.MaxSyllables}] invalid");
        ChronicleThresholds t = cfg.Thresholds;
        // The famine hysteresis check went with the two constants it guarded
        // (T4.21-5 / CR-015): there is no chronicle famine band left to be
        // inconsistent.
        if (t.MigrationSurgeFraction <= 0.0)
            throw new InvalidDataException("chronicle.thresholds: migrationSurgeFraction must be positive");
        return cfg;
    }
}
