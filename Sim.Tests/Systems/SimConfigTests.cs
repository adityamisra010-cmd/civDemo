using Sim.Core.Systems;
using Sim.Tests.TestUtil;

namespace Sim.Tests.Systems;

// T1.5: sim.json loader validation — loud, actionable errors (T0.4 template).
// The [JsonRequired] leaves are an adversarial-pass hardening: a missing or
// typo'd key must fail the load, never silently bind as 0.0.
public class SimConfigTests
{
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
}
