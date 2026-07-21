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
        Assert.True(cfg.Demographics.BirthsPerAdultPerYear > 0);
        Assert.True(cfg.Founding.Adults > 0);
    }

    [Fact]
    public void MissingLeafKey_FailsNamingTheProperty()
    {
        // The typo scenario from the adversarial pass: a missing rate must not
        // silently load as 0.0 and produce a radically different simulation.
        string json = CanonicalJson().Replace("\"birthsPerAdultPerYear\"", "\"birthsPerAdultPerYr\"");
        var e = Assert.Throws<SimConfigException>(() => SimConfigLoader.Load(json));
        Assert.Contains("birthsPerAdultPerYear", e.Message);
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
            "\"adultMortalityPerYear\": 0.006", "\"adultMortalityPerYear\": -0.006");
        var e = Assert.Throws<SimConfigException>(() => SimConfigLoader.Load(json));
        Assert.Contains("adultMortalityPerYear", e.Message);
        Assert.Contains(">= 0", e.Message);
    }

    [Fact]
    public void NaNRate_FailsActionably()
    {
        string json = CanonicalJson().Replace(
            "\"childWeight\": 0.6", "\"childWeight\": \"NaN\"");
        // String-typed NaN is a JSON binding error; either failure path must
        // surface as the loader's typed exception, never a silent 0/NaN.
        Assert.Throws<SimConfigException>(() => SimConfigLoader.Load(json));
    }

    [Fact]
    public void LaborShareAboveOne_Fails()
    {
        string json = CanonicalJson().Replace(
            "\"farmLaborShareDefault\": 0.7", "\"farmLaborShareDefault\": 1.5");
        var e = Assert.Throws<SimConfigException>(() => SimConfigLoader.Load(json));
        Assert.Contains("farmLaborShareDefault", e.Message);
    }

    [Fact]
    public void InvalidJson_FailsActionably()
    {
        Assert.Contains("not valid JSON",
            Assert.Throws<SimConfigException>(() => SimConfigLoader.Load("{ nope")).Message);
    }
}
