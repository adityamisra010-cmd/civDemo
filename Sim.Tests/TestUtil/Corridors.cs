using System.Text.Json;

namespace Sim.Tests.TestUtil;

/// <summary>
/// Loader for the T2.8 calibration corridors (Sim.Data corridors.json). Bands
/// are TUNE data; this reader is deliberately dumb — a band is [lo, hi] and a
/// missing corridor is a loud failure (a battery that silently skips a
/// corridor is the no-output-is-failure violation).
/// </summary>
public sealed class Corridors
{
    private readonly Dictionary<string, (double Lo, double Hi)> _bands = [];

    public static Corridors Load()
    {
        using var stream = Sim.Data.DataFiles.OpenCorridors();
        using var doc = JsonDocument.Parse(stream);
        var c = new Corridors();
        foreach (JsonProperty group in doc.RootElement.EnumerateObject())
        {
            if (group.Value.ValueKind != JsonValueKind.Object) continue;
            foreach (JsonProperty corridor in group.Value.EnumerateObject())
            {
                if (corridor.Value.ValueKind != JsonValueKind.Object) continue;
                if (!corridor.Value.TryGetProperty("band", out JsonElement band)) continue;
                c._bands[$"{group.Name}.{corridor.Name}"] =
                    (band[0].GetDouble(), band[1].GetDouble());
            }
        }
        return c;
    }

    /// <summary>Every corridor key present in the file ("group.corridor") —
    /// lets tests sweep ALL bands so an added corridor can't silently escape
    /// the interval-sanity check.</summary>
    public IReadOnlyCollection<string> Keys => _bands.Keys;

    public (double Lo, double Hi) Band(string key) =>
        _bands.TryGetValue(key, out var b)
            ? b
            : throw new InvalidOperationException($"corridor '{key}' missing from corridors.json");

    /// <summary>
    /// T3.12 — THE QUARANTINE, READ FROM DATA SO TWO INSTRUMENTS CANNOT DISAGREE.
    ///
    /// The defect this closes: corridors.json carried the TARGET band, the
    /// battery hard-coded the RECORDED DEVIATION window, and the nightly `jq`
    /// step read only the band — so the nightly gated on a corridor the project
    /// had explicitly quarantined, and was red for ELEVEN CONSECUTIVE RUNS
    /// while nobody noticed. Both instruments now read this one field.
    ///
    /// Returns null when the corridor declares no quarantine, or declares one
    /// with active=false. Callers must treat null as "gate normally".
    /// </summary>
    public static (double Lo, double Hi)? Quarantine(string group, string corridor)
    {
        using var stream = Sim.Data.DataFiles.OpenCorridors();
        using var doc = JsonDocument.Parse(stream);
        if (!doc.RootElement.GetProperty(group).GetProperty(corridor)
                .TryGetProperty("quarantine", out JsonElement q)) return null;
        if (!q.TryGetProperty("active", out JsonElement active) || !active.GetBoolean()) return null;
        JsonElement w = q.GetProperty("window");
        return (w[0].GetDouble(), w[1].GetDouble());
    }

    /// <summary>Window bounds for a corridor that declares one (e.g.
    /// canonical.fedGrowthPerYear.windowYears).</summary>
    public static (double From, double To) WindowYears(string group, string corridor)
    {
        using var stream = Sim.Data.DataFiles.OpenCorridors();
        using var doc = JsonDocument.Parse(stream);
        JsonElement w = doc.RootElement.GetProperty(group).GetProperty(corridor).GetProperty("windowYears");
        return (w[0].GetDouble(), w[1].GetDouble());
    }
}
