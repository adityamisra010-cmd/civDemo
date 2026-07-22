using System.Text.Json;
using System.Text.Json.Serialization;

namespace Sim.Core.Systems;

/// <summary>
/// The D-018 needs registry + grievance tuning (T2.6), loaded from needs.json
/// on the T0.4 loader template (string/Stream in, loud actionable errors out).
/// The EIGHT-need ladder is frozen design (D-018 §3); which needs are BOUND —
/// actually computed from real supply — grows by milestone: M2 binds
/// Sustenance only. An UNBOUND need contributes exactly nothing to any
/// resolution equation (its weight is dormant data) and renders as "not yet
/// simulated" in the HUD. D-018's fuller per-need schema (satisfiers,
/// expectation params) arrives when goods/markets make them real (M3).
/// </summary>
public sealed record NeedsConfig(
    [property: JsonPropertyName("needs"), JsonRequired] NeedEntry[] Needs,
    [property: JsonPropertyName("grievance"), JsonRequired] GrievanceTuning Grievance);

/// <summary>One registry entry: Bound gates participation entirely; Weight is
/// the wₙ of the D-018 grievance accrual (TUNE, meaningful only once bound).</summary>
public sealed record NeedEntry(
    [property: JsonPropertyName("id"), JsonRequired] int Id,
    [property: JsonPropertyName("name"), JsonRequired] string Name,
    [property: JsonPropertyName("bound"), JsonRequired] bool Bound,
    [property: JsonPropertyName("weight"), JsonRequired] double Weight);

/// <summary>
/// D-021 grievance decay tuning (all TUNE, all per-sim-year where rates):
/// decayRate(t) = BaseDecayPerYear + (1 − InheritFraction) × turnoverRate(t),
/// turnover = (Prev births + deaths) / Prev population per settlement — the
/// generational-decay doctrine: children inherit InheritFraction of their
/// parents' grudges, so cohort replacement drains the stock faster than quiet
/// alone ("memory is long but not immortal").
/// </summary>
public sealed record GrievanceTuning(
    [property: JsonPropertyName("baseDecayPerYear"), JsonRequired] double BaseDecayPerYear,
    [property: JsonPropertyName("inheritFraction"), JsonRequired] double InheritFraction);

public sealed class NeedsConfigException(string message, Exception? inner = null)
    : Exception(message, inner);

public static class NeedsConfigLoader
{
    public static NeedsConfig Load(Stream stream)
    {
        using var reader = new StreamReader(stream);
        return Load(reader.ReadToEnd());
    }

    public static NeedsConfig Load(string json)
    {
        NeedsConfig? cfg;
        try
        {
            cfg = JsonSerializer.Deserialize<NeedsConfig>(json);
        }
        catch (JsonException e)
        {
            throw new NeedsConfigException(
                $"needs config is not valid JSON or is missing required values: {e.Message}", e);
        }
        if (cfg is null) throw new NeedsConfigException("needs config is empty.");

        if (cfg.Needs is null || cfg.Needs.Length == 0)
            throw new NeedsConfigException("needs must have at least one entry.");
        for (int i = 0; i < cfg.Needs.Length; i++)
        {
            NeedEntry n = cfg.Needs[i];
            if (string.IsNullOrWhiteSpace(n.Name))
                throw new NeedsConfigException($"needs[{i}].name must be non-empty.");
            if (double.IsNaN(n.Weight) || double.IsInfinity(n.Weight) || n.Weight < 0.0)
                throw new NeedsConfigException(
                    $"needs[{i}].weight must be a finite value >= 0, got {Inv(n.Weight)}.");
            // Strictly ascending ids: uniqueness AND a stable deterministic
            // iteration order in one check (entry order is THE order everywhere).
            if (i > 0 && n.Id <= cfg.Needs[i - 1].Id)
                throw new NeedsConfigException(
                    $"needs ids must be strictly ascending: [{i - 1}].id {cfg.Needs[i - 1].Id} >= [{i}].id {n.Id}.");
        }

        if (cfg.Grievance is null) throw new NeedsConfigException("grievance is missing.");
        double d = cfg.Grievance.BaseDecayPerYear;
        if (double.IsNaN(d) || double.IsInfinity(d) || d < 0.0)
            throw new NeedsConfigException(
                $"grievance.baseDecayPerYear must be a finite value >= 0, got {Inv(d)}.");
        if (!(cfg.Grievance.InheritFraction >= 0.0 && cfg.Grievance.InheritFraction <= 1.0))
            throw new NeedsConfigException(
                $"grievance.inheritFraction must be in [0,1] (a fraction of inherited grudges), " +
                $"got {Inv(cfg.Grievance.InheritFraction)}.");

        return cfg;
    }

    private static string Inv(double v) => v.ToString(System.Globalization.CultureInfo.InvariantCulture);
}
