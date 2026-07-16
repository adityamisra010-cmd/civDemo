using Sim.Core.Kernel;

namespace Sim.Tests.Kernel;

// T0.4 acceptance, part 2: every malformed-JSON case fails loudly with an
// actionable message — naming the field, the offending value, and what was allowed.
public class EraTableLoaderTests
{
    private static string Bands(string rows) => $$"""{ "bands": [ {{rows}} ] }""";

    private static EraTableFormatException LoadFails(string json)
        => Assert.Throws<EraTableFormatException>(() => EraTableLoader.Load(json));

    [Fact]
    public void InvalidJson_FailsActionably()
    {
        var e = LoadFails("{ this is not json");
        Assert.Contains("not valid JSON", e.Message);
    }

    [Fact]
    public void MissingBandsArray_FailsActionably()
    {
        Assert.Contains("non-empty 'bands' array", LoadFails("""{ }""").Message);
        Assert.Contains("non-empty 'bands' array", LoadFails("""{ "bands": [] }""").Message);
    }

    [Fact]
    public void MissingName_FailsActionably()
    {
        var e = LoadFails(Bands("""{ "startYear": -4000, "endYear": -1500, "dtYears": 10 }"""));
        Assert.Contains("bands[0].name", e.Message);
        Assert.Contains("non-empty name", e.Message);
    }

    [Theory]
    [InlineData("""{ "name": "A", "endYear": -1500, "dtYears": 10 }""", "startYear")]
    [InlineData("""{ "name": "A", "startYear": -4000, "dtYears": 10 }""", "endYear")]
    [InlineData("""{ "name": "A", "startYear": -4000, "endYear": -1500 }""", "dtYears")]
    public void MissingField_FailsNamingTheField(string row, string field)
    {
        var e = LoadFails(Bands(row));
        Assert.Contains($"bands[0] ('A').{field} is missing", e.Message);
    }

    [Theory]
    [InlineData(0.0)]
    [InlineData(-5.0)]
    public void NonPositiveDt_FailsActionably(double dt)
    {
        var e = LoadFails(Bands($$"""{ "name": "A", "startYear": 0, "endYear": 100, "dtYears": {{dt}} }"""));
        Assert.Contains("dtYears must be > 0", e.Message);
    }

    [Fact]
    public void EndNotAfterStart_FailsActionably()
    {
        var e = LoadFails(Bands("""{ "name": "A", "startYear": 100, "endYear": 100, "dtYears": 1 }"""));
        Assert.Contains("endYear 100 must be greater than startYear 100", e.Message);
    }

    [Fact]
    public void GapBetweenBands_FailsActionably()
    {
        var e = LoadFails(Bands(
            """{ "name": "A", "startYear": 0, "endYear": 100, "dtYears": 1 },""" +
            """{ "name": "B", "startYear": 150, "endYear": 200, "dtYears": 1 }"""));
        Assert.Contains("startYear 150 does not equal", e.Message);
        Assert.Contains("contiguous", e.Message);
    }

    [Fact]
    public void OverlappingBands_FailActionably()
    {
        var e = LoadFails(Bands(
            """{ "name": "A", "startYear": 0, "endYear": 100, "dtYears": 1 },""" +
            """{ "name": "B", "startYear": 50, "endYear": 200, "dtYears": 1 }"""));
        Assert.Contains("startYear 50 does not equal", e.Message);
        Assert.Contains("no gaps, no overlaps", e.Message);
    }

    [Fact]
    public void BandsOutOfOrder_FailActionably()
    {
        var e = LoadFails(Bands(
            """{ "name": "B", "startYear": 100, "endYear": 200, "dtYears": 1 },""" +
            """{ "name": "A", "startYear": 0, "endYear": 100, "dtYears": 1 }"""));
        Assert.Contains("does not equal", e.Message);
        Assert.Contains("chronological order", e.Message);
    }

    [Fact]
    public void DtNotWholeDays_FailsActionably()
    {
        // 0.3 years = 108 days — fine; 0.001 years = 0.36 days — not integral.
        var e = LoadFails(Bands("""{ "name": "A", "startYear": 0, "endYear": 100, "dtYears": 0.001 }"""));
        Assert.Contains("not a whole number of days", e.Message);
    }

    [Fact]
    public void SpanNotMultipleOfDt_FailsActionably()
    {
        // 100 years = 36,000 days; dt 7y = 2,520 days; 36000 % 2520 != 0.
        var e = LoadFails(Bands("""{ "name": "A", "startYear": 0, "endYear": 100, "dtYears": 7 }"""));
        Assert.Contains("not an exact multiple of its dt", e.Message);
        Assert.Contains("36000 days", e.Message);
        Assert.Contains("2520 days", e.Message);
    }

    [Fact]
    public void ValidMinimalTable_Loads()
    {
        var table = EraTableLoader.Load(Bands(
            """{ "name": "A", "startYear": -10, "endYear": 0, "dtYears": 2 },""" +
            """{ "name": "B", "startYear": 0, "endYear": 10, "dtYears": 0.5 }"""));
        Assert.Equal(2, table.Bands.Length);
        Assert.Equal(0, table.CampaignStartDay);          // epoch = first band start
        Assert.Equal(20L * 360L, table.CampaignEndDay);
        Assert.Equal(720L, table.Bands[0].DtDays);
        Assert.Equal(180L, table.Bands[1].DtDays);
    }
}
