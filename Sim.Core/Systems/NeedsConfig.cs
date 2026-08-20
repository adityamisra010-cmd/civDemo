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
    [property: JsonPropertyName("baskets"), JsonRequired] BasketsConfig Baskets,
    [property: JsonPropertyName("varietyStandard"), JsonRequired] VarietyStandardConfig VarietyStandard,
    [property: JsonPropertyName("householdGoods")] HouseholdGoodsConfig? HouseholdGoods = null);

/// <summary>
/// T4.13 — the household-goods stock that satisfies Comfort.
///
/// ServiceLifeYears is the ONE constant this packet introduces, DERIVED against a
/// reference class stated in needs.json and docs/t4.13-design-record.md before any
/// number was chosen. It does NOT set steady-state material consumption: the
/// holding standard is derived from PerClass so that, at the standard, the annual
/// draw equals exactly what the basket drew before T4.13 for ANY service life. The
/// constant sets only how deep the buffer is — how many years of neglect Comfort
/// survives — which is the property that makes this a stock and not a flow.
///
/// PerClass carries the FORMER Comfort basket lines verbatim. They are both the
/// material mix (one material unit makes one household-good unit) and the
/// derivation input for the standard, so they had to move rather than be deleted.
/// </summary>
public sealed record HouseholdGoodsConfig(
    [property: JsonPropertyName("serviceLifeYears"), JsonRequired] double ServiceLifeYears,
    [property: JsonPropertyName("perClass"), JsonRequired] HouseholdGoodsClass[] PerClass)
{
    /// <summary>Fraction of the goods IN USE that wear out per sim-year, as an
    /// e-fold over the service life — the same shape housing uses for decay, so
    /// it is bounded below 1 at any dt and cannot wear more than exists.</summary>
    public double WornFraction(double dtYears) =>
        1.0 - System.Math.Exp(-dtYears / ServiceLifeYears);

    /// <summary>Units a person of this class holds when Comfort is exactly met.
    /// DERIVED: standard × WornFraction(1) = Σ PerPersonYear, i.e. at the standard
    /// the annual replacement equals the ratified annual consumption. Evaluated AT
    /// the standard, where every held unit is in use — so it never assumes goods
    /// above the standard wear.</summary>
    public double StandardPerPerson(int classId)
    {
        double annual = 0.0;
        for (int i = 0; i < PerClass.Length; i++)
        {
            if (PerClass[i].Class != classId) continue;
            for (int j = 0; j < PerClass[i].Materials.Length; j++)
                annual += PerClass[i].Materials[j].PerPersonYear;
            break;
        }
        return annual <= 0.0 ? 0.0 : annual / WornFraction(1.0);
    }
}

/// <summary>One class's household-goods material mix (T4.13).</summary>
public sealed record HouseholdGoodsClass(
    [property: JsonPropertyName("class"), JsonRequired] int Class,
    [property: JsonPropertyName("materials"), JsonRequired] HouseholdGoodsMaterial[] Materials);

/// <summary>One material line: PerPersonYear is a RATE (law 3), the same
/// denomination the basket line it came from used.</summary>
public sealed record HouseholdGoodsMaterial(
    [property: JsonPropertyName("good"), JsonRequired] string Good,
    [property: JsonPropertyName("perPersonYear"), JsonRequired] double PerPersonYear);

/// <summary>
/// T3.5b item 2 — the FIXED NUTRITIONAL DIVERSITY STANDARD (director ruling:
/// neither perfect-evenness nor declared-basket normalisation; a standard in
/// DATA, independent of what the class asked for and what it received).
/// Shares are the reference diet's composition (derivation:
/// docs/t3.5b-derivations.md §2 — staple 0.70 / animal 0.20 / other 0.10 from
/// the varied pre-modern agrarian diet, anchored to ADR-013's own "cereals
/// 75% of calories" chain). The loader computes the standard's Herfindahl
/// concentration H* = Σ shareᵢ²; the variety factor penalises only EXCESS
/// concentration beyond it, so a diet at or below H* takes no penalty and
/// exact saturation is expressible.
/// </summary>
public sealed record VarietyStandardConfig(
    [property: JsonPropertyName("shares"), JsonRequired] double[] Shares)
{
    /// <summary>H* — computed at load, never stored, so shares and standard
    /// cannot drift apart. NORMALISED exactly as VarietyFactor normalises the
    /// obtained diet (share = value / Σ values, same operations, same order):
    /// 0.70+0.20+0.10 is 1−1ulp in binary, and an un-normalised H* left a diet
    /// EXACTLY at the standard one ulp above it — satisfaction 1−1.1e-16, the
    /// exact-saturation branch dead by a rounding error. Identical shapes must
    /// give H == H* bitwise, and with matching normalisation they do.</summary>
    public double Concentration
    {
        get
        {
            double total = 0.0;
            for (int i = 0; i < Shares.Length; i++) total += Math.Max(0.0, Shares[i]);
            if (!(total > 0.0)) return 1.0;
            double h = 0.0;
            for (int i = 0; i < Shares.Length; i++)
            {
                double share = Math.Max(0.0, Shares[i]) / total;
                h += share * share;
            }
            return h;
        }
    }
}

/// <summary>One registry entry: Bound gates participation entirely; Weight is
/// the wₙ of the D-018 grievance accrual (TUNE, meaningful only once bound).
/// VarietyWeight is D-035-A's concentration coefficient — 0 for a need with no
/// diversity dimension.</summary>
public sealed record NeedEntry(
    [property: JsonPropertyName("id"), JsonRequired] int Id,
    [property: JsonPropertyName("name"), JsonRequired] string Name,
    [property: JsonPropertyName("bound"), JsonRequired] bool Bound,
    [property: JsonPropertyName("weight"), JsonRequired] double Weight,
    [property: JsonPropertyName("varietyWeight")] double VarietyWeight = 0.0,
    // T3.8: WHERE a bound need's satisfaction comes from. "basket" (default)
    // = the D-035-C consumption-basket fill; "housingStock" = the T3.8
    // dwelling stock (capacity/population), declared in DATA so the binding
    // is reviewable and the loader can refuse ambiguity (a housingStock need
    // with basket entries would have two satisfaction sources and the code
    // would silently pick one).
    [property: JsonPropertyName("source")] string? Source = null)
{
    [JsonIgnore] public bool FromHousingStock =>
        string.Equals(Source, "housingStock", StringComparison.Ordinal);

    /// <summary>T4.13: satisfaction comes from the HouseholdGoods stock
    /// (Comfort). Same contract as <see cref="FromHousingStock"/> — declared in
    /// data, refused by the loader if it also carries basket lines.</summary>
    [JsonIgnore] public bool FromHouseholdGoods =>
        string.Equals(Source, "householdGoods", StringComparison.Ordinal);

    /// <summary>Any stock source — the property the ambiguity guard and the
    /// bound-need bookkeeping actually care about.</summary>
    [JsonIgnore] public bool FromStock => FromHousingStock || FromHouseholdGoods;
}

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
            // T3.5b review fix (lens 3, V3): varietyWeight was entirely
            // unvalidated — NaN, -1 and 5.0 all loaded clean, and a negative
            // value under a clamp-less factor is a smuggled variety BONUS,
            // which law 2 via D-035-A forbids.
            if (double.IsNaN(n.VarietyWeight) || double.IsInfinity(n.VarietyWeight)
                || n.VarietyWeight < 0.0 || n.VarietyWeight > 1.0)
                throw new NeedsConfigException(
                    $"needs[{i}].varietyWeight must be in [0, 1], got {Inv(n.VarietyWeight)} — "
                    + "a negative value is a variety BONUS (law 2 forbids free-floating buffs), "
                    + "and above 1 the factor can go negative.");
            // Strictly ascending ids: uniqueness AND a stable deterministic
            // iteration order in one check (entry order is THE order everywhere).
            if (i > 0 && n.Id <= cfg.Needs[i - 1].Id)
                throw new NeedsConfigException(
                    $"needs ids must be strictly ascending: [{i - 1}].id {cfg.Needs[i - 1].Id} >= [{i}].id {n.Id}.");
            // T3.8: the source field is a closed vocabulary — a typo'd source
            // must not silently fall back to basket (the config-fails-quietly
            // class).
            if (n.Source is not null && n.Source != "basket" && n.Source != "housingStock"
                && n.Source != "householdGoods")
                throw new NeedsConfigException(
                    $"needs[{i}] ({n.Name}).source must be \"basket\" or \"housingStock\", got "
                    + $"\"{n.Source}\".");
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
        // T3.8 AMBIGUITY GUARD: a housingStock-sourced need must have NO
        // basket entries — two satisfaction sources for one need means the
        // code silently picks one, which is how mechanisms rot. Proven RED by
        // deleting this block.
        for (int n = 0; n < cfg.Needs.Length; n++)
        {
            if (!cfg.Needs[n].FromStock) continue;
            for (int i = 0; i < cfg.Baskets!.Entries.Length; i++)
                if (cfg.Baskets.Entries[i].Need == cfg.Needs[n].Id)
                    throw new NeedsConfigException(
                        $"need {cfg.Needs[n].Id} ({cfg.Needs[n].Name}) declares source \"{cfg.Needs[n].Source}\" "
                        + $"but baskets.entries[{i}] still baskets it ({cfg.Baskets.Entries[i].Good}) — "
                        + "a need cannot have two satisfaction sources. Delete the basket lines or the "
                        + "source declaration.");
        }
        // T3.5b item 2: the variety standard — validated so a malformed
        // standard cannot silently disable the diversity mechanism.
        if (cfg.VarietyStandard is null || cfg.VarietyStandard.Shares is null
            || cfg.VarietyStandard.Shares.Length < 2)
            throw new NeedsConfigException(
                "varietyStandard.shares must list at least two reference-diet shares "
                + "(docs/t3.5b-derivations.md §2); a standard with fewer has no diversity dimension.");
        double shareSum = 0.0;
        for (int i = 0; i < cfg.VarietyStandard.Shares.Length; i++)
        {
            double v = cfg.VarietyStandard.Shares[i];
            if (double.IsNaN(v) || double.IsInfinity(v) || v <= 0.0)
                throw new NeedsConfigException(
                    $"varietyStandard.shares[{i}] must be a finite value > 0, got {Inv(v)}.");
            shareSum += v;
        }
        if (Math.Abs(shareSum - 1.0) > 1e-9)
            throw new NeedsConfigException(
                $"varietyStandard.shares must sum to 1.0 (a diet composition), got {Inv(shareSum)}.");

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

    /// <summary>The Sustenance need id — mirrors BasketBook.SustenanceNeedId,
    /// which the loader cannot reference (goods are not resolved yet here).</summary>
    private const int SustenanceNeedId = 1;

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

        // FOOD LINES MUST SUM TO EXACTLY 1.0 PER CLASS. needs.json calls this
        // "by construction"; construction is not a mechanism, so it is checked.
        // The sum IS the settlement's nutritional requirement per person-year
        // (ConsumptionDeficitRow.DemandUnits), which is the denominator of
        // food_surplus_ratio, which gates artisan emergence — so a tuner who
        // nudged grain from 0.90 to 0.95 while "just adjusting the diet" would
        // silently move the class-mobility bar. Tuning data is always allowed;
        // that is exactly why the invariant a tuner could break must be a check
        // rather than a comment. Tolerance is 1e-9: authored decimal data, not
        // an accumulated computation.
        for (int c = 0; c < b.Entries.Length; c++)
        {
            int cls = b.Entries[c].Class;
            bool seen = false;
            for (int j = 0; j < c; j++) if (b.Entries[j].Class == cls) { seen = true; break; }
            if (seen) continue;

            double foodSum = 0.0;
            bool anyFood = false;
            for (int i = 0; i < b.Entries.Length; i++)
            {
                if (b.Entries[i].Class != cls || b.Entries[i].Need != SustenanceNeedId) continue;
                foodSum += b.Entries[i].PerPersonYear;
                anyFood = true;
            }
            if (anyFood && Math.Abs(foodSum - 1.0) > 1e-9)
                throw new NeedsConfigException(
                    $"class {cls}'s food basket sums to {Inv(foodSum)}, not 1.0. Food lines are "
                    + "denominated in person-year-equivalents of nutrition, so the sum IS how much "
                    + "one person needs per year — it must be 1.0. A basket changes WHAT is eaten, "
                    + "never how much nutrition a person requires; the sum is also the denominator "
                    + "of food_surplus_ratio, so moving it silently retunes class mobility.");
        }

        // A BOUND need with no basket line anywhere would satisfy silently at
        // 1.0 forever — indistinguishable from an unbound need in the output
        // but not in the weighting. That is exactly the failure the T2.6
        // zero-effect gate exists to catch, so catch it at load.
        for (int n = 0; n < cfg.Needs.Length; n++)
        {
            if (!cfg.Needs[n].Bound) continue;
            // T3.8: a housingStock-sourced need's satisfier IS the dwelling
            // stock — the guard's purpose (no bound need without a satisfier)
            // is met by the declared source, and the ambiguity guard above
            // separately forbids it ALSO having basket lines.
            if (cfg.Needs[n].FromStock) continue;
            bool served = false;
            for (int i = 0; i < b.Entries.Length; i++) if (b.Entries[i].Need == cfg.Needs[n].Id) { served = true; break; }
            if (!served)
                throw new NeedsConfigException(
                    $"need {cfg.Needs[n].Id} ({cfg.Needs[n].Name}) is bound but no basket entry serves it "
                    + "— a bound need with no satisfier would read as permanently satisfied (declare "
                    + "source \"housingStock\" or \"householdGoods\" if a stock is meant to serve it).");
        }
    }

    private static string Inv(double v) => v.ToString(System.Globalization.CultureInfo.InvariantCulture);
}
