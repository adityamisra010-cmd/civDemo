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
    [property: JsonPropertyName("grievance"), JsonRequired] GrievanceTuning Grievance,
    [property: JsonPropertyName("aggregation"), JsonRequired] AggregationTuning Aggregation,
    [property: JsonPropertyName("baskets"), JsonRequired] BasketsConfig Baskets);

/// <summary>One registry entry: Bound gates participation entirely; Weight is
/// the wₙ of the D-018 grievance accrual (TUNE, meaningful only once bound).
/// VarietyWeight is D-035-A's concentration coefficient — 0 for a need with no
/// diversity dimension.</summary>
public sealed record NeedEntry(
    [property: JsonPropertyName("id"), JsonRequired] int Id,
    [property: JsonPropertyName("name"), JsonRequired] string Name,
    [property: JsonPropertyName("bound"), JsonRequired] bool Bound,
    [property: JsonPropertyName("weight"), JsonRequired] double Weight,
    [property: JsonPropertyName("varietyWeight")] double VarietyWeight = 0.0);

/// <summary>
/// D-035-B aggregation tuning (all TUNE). Sigma is the CES substitution
/// elasticity and MUST be in (0,1) — that is the ruling's load-bearing
/// constraint, not a preference, so the loader refuses anything else rather
/// than silently degenerating to the weighted sum D-035-B exists to forbid.
/// SatisfactionFloor bounds how far a single zeroed need may drag the
/// aggregate (see needs.json's doc; the D-035-B acceptance ceiling is derived
/// from it). The TierA* trio is d018:46's gate override, retained unchanged.
/// </summary>
public sealed record AggregationTuning(
    [property: JsonPropertyName("sigma"), JsonRequired] double Sigma,
    [property: JsonPropertyName("satisfactionFloor"), JsonRequired] double SatisfactionFloor,
    [property: JsonPropertyName("tierAFloor"), JsonRequired] double TierAFloor,
    [property: JsonPropertyName("tierAGain"), JsonRequired] double TierAGain,
    [property: JsonPropertyName("tierACollapse"), JsonRequired] double TierACollapse);

/// <summary>The D-035-C consumption baskets: one entry per (class, need, good).</summary>
public sealed record BasketsConfig(
    [property: JsonPropertyName("entries"), JsonRequired] BasketEntry[] Entries);

/// <summary>One basket line. PerPersonYear is a RATE (law 3) — units of the
/// good demanded per person per sim-year, integrated with dtYears at the point
/// of use. Food lines are denominated in person-year-equivalents of nutrition
/// (the D-015 grain convention).</summary>
public sealed record BasketEntry(
    [property: JsonPropertyName("class"), JsonRequired] int Class,
    [property: JsonPropertyName("need"), JsonRequired] int Need,
    [property: JsonPropertyName("good"), JsonRequired] string Good,
    [property: JsonPropertyName("perPersonYear"), JsonRequired] double PerPersonYear);

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

        ValidateAggregation(cfg.Aggregation);
        ValidateBaskets(cfg);
        return cfg;
    }

    private static void ValidateAggregation(AggregationTuning? a)
    {
        if (a is null) throw new NeedsConfigException("aggregation is missing.");
        // sigma in (0,1) is D-035-B's load-bearing constraint: at sigma >= 1 the
        // aggregation stops being non-compensatory, which is the whole point of
        // the ruling. Refuse loudly rather than degenerate silently.
        if (!(a.Sigma > 0.0) || !(a.Sigma < 1.0))
            throw new NeedsConfigException(
                $"aggregation.sigma must be in the open interval (0,1) — D-035-B's "
                + $"non-compensatory requirement; got {Inv(a.Sigma)}.");
        if (!(a.SatisfactionFloor >= 0.0) || !(a.SatisfactionFloor < 1.0))
            throw new NeedsConfigException(
                $"aggregation.satisfactionFloor must be in [0,1), got {Inv(a.SatisfactionFloor)}.");
        if (!(a.TierAFloor >= 0.0) || !(a.TierAFloor <= 1.0))
            throw new NeedsConfigException(
                $"aggregation.tierAFloor must be in [0,1] (a satisfaction level), got {Inv(a.TierAFloor)}.");
        if (!(a.TierAGain >= 0.0) || !double.IsFinite(a.TierAGain))
            throw new NeedsConfigException(
                $"aggregation.tierAGain must be a finite value >= 0, got {Inv(a.TierAGain)}.");
        if (!(a.TierACollapse >= 0.0) || !double.IsFinite(a.TierACollapse))
            throw new NeedsConfigException(
                $"aggregation.tierACollapse must be a finite value >= 0, got {Inv(a.TierACollapse)}.");
    }

    private static void ValidateBaskets(NeedsConfig cfg)
    {
        BasketsConfig? b = cfg.Baskets;
        if (b is null || b.Entries is null) throw new NeedsConfigException("baskets.entries is missing.");
        for (int i = 0; i < b.Entries.Length; i++)
        {
            BasketEntry e = b.Entries[i];
            if (string.IsNullOrWhiteSpace(e.Good))
                throw new NeedsConfigException($"baskets.entries[{i}].good must be non-empty.");
            if (!(e.PerPersonYear > 0.0) || !double.IsFinite(e.PerPersonYear))
                throw new NeedsConfigException(
                    $"baskets.entries[{i}] ({e.Good}).perPersonYear must be a finite value > 0 "
                    + $"— an entry that demands nothing is a deleted line, not data; got {Inv(e.PerPersonYear)}.");
            bool known = false;
            for (int n = 0; n < cfg.Needs.Length; n++) if (cfg.Needs[n].Id == e.Need) { known = true; break; }
            if (!known)
                throw new NeedsConfigException(
                    $"baskets.entries[{i}] names need id {e.Need}, which is not in the needs registry.");
            for (int j = 0; j < i; j++)
                if (b.Entries[j].Class == e.Class && b.Entries[j].Need == e.Need
                    && string.Equals(b.Entries[j].Good, e.Good, StringComparison.Ordinal))
                    throw new NeedsConfigException(
                        $"baskets.entries[{i}] repeats (class {e.Class}, need {e.Need}, {e.Good}) "
                        + $"already declared at [{j}] — combine them into one perPersonYear rate.");
        }

        // A BOUND need with no basket line anywhere would satisfy silently at
        // 1.0 forever — indistinguishable from an unbound need in the output
        // but not in the weighting. That is exactly the failure the T2.6
        // zero-effect gate exists to catch, so catch it at load.
        for (int n = 0; n < cfg.Needs.Length; n++)
        {
            if (!cfg.Needs[n].Bound) continue;
            bool served = false;
            for (int i = 0; i < b.Entries.Length; i++) if (b.Entries[i].Need == cfg.Needs[n].Id) { served = true; break; }
            if (!served)
                throw new NeedsConfigException(
                    $"need {cfg.Needs[n].Id} ({cfg.Needs[n].Name}) is bound but no basket entry serves it "
                    + "— a bound need with no satisfier would read as permanently satisfied.");
        }
    }

    private static string Inv(double v) => v.ToString(System.Globalization.CultureInfo.InvariantCulture);
}
