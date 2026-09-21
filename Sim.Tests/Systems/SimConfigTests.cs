using Sim.Core.Systems;
using Sim.Tests.TestUtil;

namespace Sim.Tests.Systems;

// T1.5: sim.json loader validation — loud, actionable errors (T0.4 template).
// The [JsonRequired] leaves are an adversarial-pass hardening: a missing or
// typo'd key must fail the load, never silently bind as 0.0.
public class SimConfigTests
{
    /// <summary>
    /// The shipped literal the disaster-arming substitutions anchor on, WITH ITS
    /// TRAILING COMMA. The comma is load-bearing: without it "hazardPerYear": 0.0
    /// is a PREFIX of "hazardPerYear": 0.01, so on a tree where the director has
    /// ruled the rate and re-armed sim.json the same Replace would rewrite 0.01
    /// into 0.011 and DisasterHazard_Armed_Loads would silently stop testing what
    /// it names. Pinned by HazardAnchor_IsValueExact_NotAPrefix below.
    /// </summary>
    private const string HazardAnchor = "\"hazardPerYear\": 0.0,";
    private const string ArmedHazard = "\"hazardPerYear\": 0.01,";

    private static string CanonicalJson()
    {
        using var stream = global::Sim.Data.DataFiles.OpenSim();
        using var reader = new StreamReader(stream);
        return reader.ReadToEnd();
    }

    [Fact]
    public void CanonicalFile_Loads()
    {
        SimConfig cfg = TestConfigs.Sim();
        Assert.True(System.Linq.Enumerable.Sum(cfg.Demographics.FertilityPerPersonPerYear) > 0);
        long adults = 0;
        for (int c = Sim.Core.State.Cohorts.FirstAdult; c < Sim.Core.State.Cohorts.FirstElder; c++)
            adults += cfg.Founding.CohortCounts[c];
        Assert.True(adults > 0);
        // T4.11 added Merchants alongside Peasants and Artisans (D-027 delivers
        // the registry incrementally, one class per milestone that earns one).
        Assert.Equal(3, cfg.Registries.Classes.Length);
    }

    [Fact]
    public void MissingLeafKey_FailsNamingTheProperty()
    {
        // The typo scenario from the adversarial pass: a missing rate must not
        // silently load as 0.0 and produce a radically different simulation.
        string json = CanonicalJson().Replace("\"fertilityPerPersonPerYear\"", "\"fertilityPerPersonPerYr\"");
        var e = Assert.Throws<SimConfigException>(() => SimConfigLoader.Load(json));
        Assert.Contains("fertilityPerPersonPerYear", e.Message);
    }

    [Fact]
    public void MissingFoundingLeaf_Fails()
    {
        string json = CanonicalJson().Replace("\"foodStore\"", "\"foodStores\"");
        var e = Assert.Throws<SimConfigException>(() => SimConfigLoader.Load(json));
        Assert.Contains("foodStore", e.Message);
    }

    [Fact]
    public void NegativeRate_FailsActionably()
    {
        string json = CanonicalJson().Replace(
            "\"starvationMortalityMaxPerYear\": 0.12", "\"starvationMortalityMaxPerYear\": -0.12");
        var e = Assert.Throws<SimConfigException>(() => SimConfigLoader.Load(json));
        Assert.Contains("starvationMortalityMaxPerYear", e.Message);
        Assert.Contains(">= 0", e.Message);
    }

    [Fact]
    public void NaNRate_FailsActionably()
    {
        string json = CanonicalJson().Replace(
            "\"starvationChildMultiplier\": 1.5", "\"starvationChildMultiplier\": \"NaN\"");
        // String-typed NaN is a JSON binding error; either failure path must
        // surface as the loader's typed exception, never a silent 0/NaN.
        Assert.Throws<SimConfigException>(() => SimConfigLoader.Load(json));
    }

    [Fact]
    public void DirtPathSpeedFactorOutOfRange_Fails()
    {
        string json = CanonicalJson().Replace(
            "\"dirtPathSpeedFactor\": 0.5", "\"dirtPathSpeedFactor\": 1.5");
        var e = Assert.Throws<SimConfigException>(() => SimConfigLoader.Load(json));
        Assert.Contains("dirtPathSpeedFactor", e.Message);
        Assert.Contains("(0,1]", e.Message);
    }

    [Fact]
    public void InvalidJson_FailsActionably()
    {
        Assert.Contains("not valid JSON",
            Assert.Throws<SimConfigException>(() => SimConfigLoader.Load("{ nope")).Message);
    }

    // ---- T3.6 (D-034): the f < 1 mandate is a LOAD guard, not a comment. ----
    // Proven RED by deleting the trade validation block in SimConfigLoader:
    // both (0,1) tests then fail (the config loads clean), while CanonicalFile
    // stays green — the guard, not the shipped value, is under test (§7.5).

    [Theory]
    [InlineData("1.0")]   // f = 1: overshoot no longer structurally impossible
    [InlineData("1.5")]   // f > 1: overshoot guaranteed on a large gap
    [InlineData("0.0")]   // f = 0: trade silently inert
    [InlineData("-0.25")] // negative: direction inverts
    public void TradeGapClosingFraction_OutsideOpenUnitInterval_RefusesLoad(string bad)
    {
        // Anchor on the adjacent trade-only key: migration ALSO has a
        // gapClosingFraction (same shape, same shipped value — the T2.8
        // ancestor of this cap), and a bare replace would trip ITS guard
        // instead and pass for the wrong reason.
        string json = CanonicalJson().Replace(
            "\"gapClosingFraction\": 0.25,\n    \"costPerBulkCostUnit\"",
            $"\"gapClosingFraction\": {bad},\n    \"costPerBulkCostUnit\"");
        var e = Assert.Throws<SimConfigException>(() => SimConfigLoader.Load(json));
        Assert.Contains("trade.gapClosingFraction", e.Message);
        Assert.Contains("(0,1)", e.Message);
    }

    [Theory]
    [InlineData("0.0")]  // deadband vanishes — every gap trades at any distance
    [InlineData("-0.16")]
    public void TradeCostPerBulkCostUnit_NotPositive_RefusesLoad(string bad)
    {
        string json = CanonicalJson().Replace("\"costPerBulkCostUnit\": 0.16", $"\"costPerBulkCostUnit\": {bad}");
        var e = Assert.Throws<SimConfigException>(() => SimConfigLoader.Load(json));
        Assert.Contains("trade.costPerBulkCostUnit", e.Message);
    }

    [Fact]
    public void TradeSection_Missing_RefusesLoad()
    {
        // A silently absent trade section must not bind null and NRE at step
        // time — the config-fails-quietly class (T3.5b item 4 idiom).
        string json = CanonicalJson().Replace(
            "\"gapClosingFraction\": 0.25,\n    \"costPerBulkCostUnit\"",
            "\"gapClosingFractionX\": 0.25,\n    \"costPerBulkCostUnit\"");
        var e = Assert.Throws<SimConfigException>(() => SimConfigLoader.Load(json));
        Assert.Contains("gapClosingFraction", e.Message);
    }

    // ======================================================================
    // T4.21-1 (CR-015): foodState and disaster sections
    // ======================================================================

    [Theory]
    [InlineData("1.0")]    // the remainder (d − a)/(1 − a) divides by zero
    [InlineData("1.5")]
    [InlineData("-0.1")]   // a negative dead-zone would starve a fed settlement
    public void FoodStateAdaptation_OutsideHalfOpenUnitInterval_RefusesLoad(string bad)
    {
        string json = CanonicalJson().Replace(
            "\"adaptationAbsorbableShortfall\": 0.20", $"\"adaptationAbsorbableShortfall\": {bad}");
        Assert.NotEqual(CanonicalJson(), json);
        var e = Assert.Throws<SimConfigException>(() => SimConfigLoader.Load(json));
        Assert.Contains("foodState.adaptationAbsorbableShortfall", e.Message);
        Assert.Contains("[0,1)", e.Message);
    }

    [Theory]
    [InlineData("0.0")]
    [InlineData("0.3333333333333333")]   // the recorded a = 1/3 alternative loads
    [InlineData("0.9999")]
    public void FoodStateAdaptation_InsideHalfOpenUnitInterval_Loads(string ok)
    {
        string json = CanonicalJson().Replace(
            "\"adaptationAbsorbableShortfall\": 0.20", $"\"adaptationAbsorbableShortfall\": {ok}");
        SimConfig cfg = SimConfigLoader.Load(json);
        Assert.Equal(double.Parse(ok, System.Globalization.CultureInfo.InvariantCulture),
            cfg.FoodState.AdaptationAbsorbableShortfall);
    }

    [Fact]
    public void FoodStateSection_Missing_RefusesLoad()
    {
        string json = CanonicalJson().Replace(
            "\"adaptationAbsorbableShortfall\": 0.20", "\"adaptationAbsorbableShortfallX\": 0.20");
        var e = Assert.Throws<SimConfigException>(() => SimConfigLoader.Load(json));
        Assert.Contains("adaptationAbsorbableShortfall", e.Message);
    }

    [Theory]
    [InlineData("-0.01")]
    [InlineData("NaN")]
    public void DisasterHazard_Negative_RefusesLoad(string bad)
    {
        string json = CanonicalJson().Replace(HazardAnchor, $"\"hazardPerYear\": {bad},");
        AssertAnchorMatched(json);
        var e = Assert.Throws<SimConfigException>(() => SimConfigLoader.Load(json));
        Assert.Contains("disaster.hazardPerYear", e.Message);
    }

    [Fact]
    public void DisasterHazard_Armed_Loads()
    {
        // THE PIN THAT MAKES CR-016 REVERSIBLE BY ONE DATA EDIT. T4.21-4 armed
        // the shipped value (0.0 -> 0.01); T4.21-7 returns it to 0.0 because
        // the mechanism ships complete and tested but INERT and the RATE is the
        // director's ruling (docs/adr/cr-016-armed-disaster-fallout.md). This
        // test is what proves the claim that arming is a DATA change and
        // nothing else: the derived value still loads today, through the same
        // loader, with no code path of its own. If the director rules a rate,
        // that ruling is this one number in sim.json.
        string json = CanonicalJson().Replace(HazardAnchor, ArmedHazard);
        AssertAnchorMatched(json);
        Assert.Equal(0.01, SimConfigLoader.Load(json).Disaster.HazardPerYear);
    }

    [Fact]
    public void HazardAnchor_IsValueExact_NotAPrefix()
    {
        // THE TEST THAT WOULD HAVE CAUGHT IT (T4.21-8 finding 2). CR-016 §D.3
        // prices the disarm as ONE data edit; the edit's reversibility is
        // asserted by DisasterHazard_Armed_Loads, which SUBSTITUTES the armed
        // value into the shipped JSON by string replacement. With the old
        // anchor ("hazardPerYear": 0.0, no comma) that substitution is
        // PREFIX-SENSITIVE: applied to an already-armed sim.json it matches
        // inside "hazardPerYear": 0.01 and yields 0.011. Measured on an armed
        // tree before this fix, DisasterHazard_Armed_Loads failed with
        // "Expected: 0.01 / Actual: 0.010999999999999999" — loud, but the trap
        // is that a future agent re-arming the tree repairs it by moving the
        // EXPECTED value instead of the search string, after which the test
        // named "the derived 0.01 still loads" asserts 0.011 forever.
        //
        // This test makes the defect visible on the SHIPPED 0.0 tree, where it
        // otherwise cannot be: apply the substitution to its OWN OUTPUT. A
        // value-exact anchor is idempotent under re-application (the armed JSON
        // no longer contains the shipped literal, so the second pass is a
        // no-op); a prefix anchor compounds. It is the re-armed tree, simulated
        // without needing one.
        string canonical = CanonicalJson();
        Assert.True(CountOccurrences(canonical, HazardAnchor) == 1,
            "HazardAnchor must match the shipped sim.json disaster.hazardPerYear literal EXACTLY " +
            "once. If the director has ruled a rate and sim.json is re-armed, move the SEARCH " +
            "STRINGS (HazardAnchor / ArmedHazard, trailing comma included) — NEVER the expected " +
            "VALUE. See docs/adr/cr-016-armed-disaster-fallout.md §D.3.");

        string armed = canonical.Replace(HazardAnchor, ArmedHazard);
        Assert.NotEqual(canonical, armed);
        Assert.Equal(0.01, SimConfigLoader.Load(armed).Disaster.HazardPerYear);

        // The re-armed tree: the SAME substitution, run again on the armed JSON.
        string rearmed = armed.Replace(HazardAnchor, ArmedHazard);
        Assert.Equal(armed, rearmed);
        Assert.Equal(0.01, SimConfigLoader.Load(rearmed).Disaster.HazardPerYear);
    }

    /// <summary>
    /// Every disaster test in this file SUBSTITUTES into the shipped JSON by string
    /// replacement anchored on HazardAnchor. If the anchor stops matching — the
    /// director rules a rate and sim.json is re-armed — the substitution becomes
    /// a silent no-op and the test would assert against the UNMODIFIED shipped
    /// config. Fail loudly and say what to move, because the tempting repair is
    /// the expected VALUE and that is exactly what makes the test stop testing
    /// what it names (T4.21-8 finding 2).
    /// </summary>
    private static void AssertAnchorMatched(string substituted) => Assert.True(
        substituted != CanonicalJson(),
        "the disaster-arming substitution found nothing: SimConfigTests.HazardAnchor no longer " +
        "matches the shipped sim.json disaster.hazardPerYear literal. If the director has ruled a " +
        "rate and sim.json is re-armed, move the SEARCH STRINGS (HazardAnchor / ArmedHazard, " +
        "trailing comma included) — NEVER the expected VALUE. See " +
        "docs/adr/cr-016-armed-disaster-fallout.md §D.3.");

    private static int CountOccurrences(string haystack, string needle)
    {
        int n = 0;
        for (int i = haystack.IndexOf(needle, System.StringComparison.Ordinal); i >= 0;
             i = haystack.IndexOf(needle, i + needle.Length, System.StringComparison.Ordinal))
        {
            n++;
        }
        return n;
    }

    [Theory]
    [InlineData("0.0")]
    [InlineData("-5.0")]
    public void DisasterDuration_NotPositive_RefusesLoad(string bad)
    {
        string json = CanonicalJson().Replace("\"durationYears\": 5.0", $"\"durationYears\": {bad}");
        Assert.NotEqual(CanonicalJson(), json);
        var e = Assert.Throws<SimConfigException>(() => SimConfigLoader.Load(json));
        Assert.Contains("disaster.durationYears", e.Message);
    }

    [Theory]
    [InlineData("0.75", "1.5")]    // max > 1: the multiplier goes negative, Ledger.Flow throws
    [InlineData("0.9", "0.8")]     // min > max
    [InlineData("-0.1", "1.0")]    // min < 0
    public void DisasterSeverityBand_Invalid_RefusesLoad(string min, string max)
    {
        string json = CanonicalJson()
            .Replace("\"severityMin\": 0.75", $"\"severityMin\": {min}")
            .Replace("\"severityMax\": 1.0", $"\"severityMax\": {max}");
        Assert.NotEqual(CanonicalJson(), json);
        var e = Assert.Throws<SimConfigException>(() => SimConfigLoader.Load(json));
        Assert.Contains("disaster.severityMin/severityMax", e.Message);
    }

    [Fact]
    public void DisasterSection_Missing_RefusesLoad()
    {
        string json = CanonicalJson().Replace(HazardAnchor, "\"hazardPerYearX\": 0.0,");
        AssertAnchorMatched(json);
        var e = Assert.Throws<SimConfigException>(() => SimConfigLoader.Load(json));
        Assert.Contains("hazardPerYear", e.Message);
    }

    // ======================================================================
    // T4.21-1b (ADR-025 §2.4 / ADR-026): demographics.headroomRelaxationPerYear
    // ======================================================================

    [Theory]
    [InlineData("-0.01")]      // a negative rate would OPEN headroom instead of closing it
    [InlineData("NaN")]
    [InlineData("Infinity")]
    public void HeadroomRelaxation_NotFiniteNonNegative_RefusesLoad(string bad)
    {
        string json = CanonicalJson().Replace(
            "\"headroomRelaxationPerYear\": 0.0693147180559945",
            $"\"headroomRelaxationPerYear\": {bad}");
        Assert.NotEqual(CanonicalJson(), json);
        var e = Assert.Throws<SimConfigException>(() => SimConfigLoader.Load(json));
        Assert.Contains("demographics.headroomRelaxationPerYear", e.Message);
    }

    [Fact]
    public void HeadroomRelaxation_Missing_RefusesLoad()
    {
        // The typo scenario: a missing key must not bind as 0.0 (which would
        // freeze every settlement at its current headroom — no growth into V).
        string json = CanonicalJson().Replace(
            "\"headroomRelaxationPerYear\":", "\"headroomRelaxationPerYr\":");
        Assert.NotEqual(CanonicalJson(), json);
        var e = Assert.Throws<SimConfigException>(() => SimConfigLoader.Load(json));
        Assert.Contains("headroomRelaxationPerYear", e.Message);
    }

    [Fact]
    public void HeadroomRelaxation_ShippedValue_IsLn2OverTenYears()
    {
        // CHOSEN k = ln 2 / 10: "the gap halves per decade" (ADR-026 §3 table; one
        // constant shared with ADR-025's vacancy bound). Pinned exactly so the
        // value cannot drift from its stated frame silently.
        SimConfig cfg = TestConfigs.Sim();
        Assert.Equal(0.0693147180559945, cfg.Demographics.HeadroomRelaxationPerYear);
        // The literal is ln 2 / 10 rounded to 16 significant digits (the last
        // digit 3 of 0.06931471805599453 dropped): |Δ| ≈ 3e-17, one ulp-scale.
        double k = cfg.Demographics.HeadroomRelaxationPerYear;
        Assert.InRange(Math.Abs(k - Math.Log(2.0) / 10.0), 0.0, 1e-16);
        // Frame check: the gap halves per decade.
        Assert.InRange(Math.Exp(-k * 10.0), 0.5 - 1e-15, 0.5 + 1e-15);
    }

    [Fact]
    public void ShippedDisaster_IsInert_AndTheBandIsTheDerivedOne()
    {
        // THE SHIPPING STATE, asserted where a change to it must be noticed.
        // T4.21-1 shipped λ = 0 (its golden move was layout + RngStreams only).
        // T4.21-4 ARMED it at the derived 0.01 — one famine-class local crop
        // failure per settlement per century (spec §3.3, the disaster._doc's
        // derivation and reference class, CR-015 §3.3) — and MEASURED at that
        // value, canonical founded, 20 seeds × 300 turns: 6,211 onsets over
        // 66,000 settlement-turns = 0.941061 per settlement-century, inside the
        // binomial 99 % band [0.922204, 0.981047] around the truncation-
        // corrected expectation (1 − e^{−λ·dt})/(λ·dt) × λ·100 = 0.9516258
        // (docs/t4.21-4-record.md §2.2, measured AT λ = 0.01). It also measured
        // the fallout, which is why λ IS 0 AGAIN: the mechanism ships COMPLETE
        // AND TESTED BUT INERT and the RATE is the director's ruling on
        // docs/adr/cr-016-armed-disaster-fallout.md. The onset rate above is
        // still measured in the suite — FamineScenarioTests arms λ IN-RIG.
        // The BAND below is untouched by any of that and is the §3.3
        // derivation: s·D ∈ [3.75, 5.0] > 3.46 production-years at ρ_ship = 1.3.
        SimConfig cfg = TestConfigs.Sim();
        Assert.Equal(0.0, cfg.Disaster.HazardPerYear);
        Assert.Equal(5.0, cfg.Disaster.DurationYears);
        Assert.Equal(0.75, cfg.Disaster.SeverityMin);
        Assert.Equal(1.0, cfg.Disaster.SeverityMax);
        const double rhoShip = 1.3, dtMax = 10.0;
        double G = cfg.Consumption.GranaryYearsOfDemand;
        double threshold = (dtMax * (rhoShip - 1.0) + G) / rhoShip;
        Assert.InRange(threshold, 3.45, 3.47);
        Assert.True(cfg.Disaster.SeverityMin * cfg.Disaster.DurationYears > threshold);
        Assert.Equal(0.20, cfg.FoodState.AdaptationAbsorbableShortfall);
    }
}
