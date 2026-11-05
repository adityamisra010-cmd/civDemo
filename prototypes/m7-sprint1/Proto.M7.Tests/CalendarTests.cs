using Proto.M7.Time;
using static Proto.M7.Tests.TestSupport;

namespace Proto.M7.Tests;

/// <summary>Spec §2.2 — integer time and the campaign calendar from the era-pacing fixture.</summary>
public class CalendarTests
{
    [Fact]
    public void Fixture_is_a_byte_copy_of_the_repository_file_when_present()
    {
        string? repo = RepoEraPacingPath();
        if (repo is null) return; // prototype copied outside the repository: nothing to compare against
        Assert.Equal(File.ReadAllBytes(repo), File.ReadAllBytes(FixturePath));
    }

    [Fact]
    public void Shipped_calendar_has_1630_turns_and_the_kernel_dt_set()
    {
        var cal = ShippedCalendar();
        Assert.Equal(1630, cal.TotalTurns);
        var dts = new List<long>();
        foreach (var b in cal.Bands) if (!dts.Contains(b.DtDays)) dts.Add(b.DtDays);
        Assert.Equal(new long[] { 3600, 1800, 1080, 720, 360, 180 }, dts.ToArray());
        Assert.Equal(new long[] { 250, 400, 300, 200, 120, 160, 200 }, cal.Bands.Select(b => b.TurnCount).ToArray());
        Assert.Equal(0, cal.TurnStart(0).Ms);
        Assert.Equal(6100L * 360 * Day, cal.CampaignEnd.Ms);          // -4000 .. 2100
        Assert.Equal(cal.CampaignEnd, cal.TurnStart(1630));
        // Turns tile the timeline exactly: start(t+1) = start(t) + length(t), for all 1630 turns.
        long sum = 0;
        for (long t = 0; t < cal.TotalTurns; t++)
        {
            Assert.Equal(sum, cal.TurnStart(t).Ms);
            Assert.Equal(cal.TurnLengthDays(t) * Day, cal.TurnLengthMs(t));
            sum += cal.TurnLengthMs(t);
        }
        Assert.Equal(cal.CampaignEnd.Ms, sum);
    }

    [Fact]
    public void Kernel_dt_rule_matches_dt_at_turn_start_date()
    {
        // EraTable.DtDaysAt(prev.Clock.SimDays): dt is the band at the turn-START date. Band boundaries:
        var cal = ShippedCalendar();
        Assert.Equal(3600, cal.TurnLengthDays(249));   // last Neolithic turn
        Assert.Equal(1800, cal.TurnLengthDays(250));   // first Bronze/Iron turn starts exactly at -1500
        Assert.Equal(new SimInstant(2500L * 360 * Day), cal.TurnStart(250));
        Assert.Equal(180, cal.TurnLengthDays(1629));
    }

    [Fact]
    public void One_third_of_every_shipped_turn_length_is_exact_in_days()
    {
        var cal = ShippedCalendar();
        var expectedThirdDays = new Dictionary<long, long> { [3600] = 1200, [1800] = 600, [1080] = 360, [720] = 240, [360] = 120, [180] = 60 };
        foreach (var b in cal.Bands)
        {
            long turn = b.FirstTurn;
            var at = cal.At(turn, 1, 3);
            long offset = at.Ms - cal.TurnStart(turn).Ms;
            Assert.Equal(0, offset % Day);
            Assert.Equal(expectedThirdDays[b.DtDays], offset / Day);
            Assert.Equal(cal.TurnLengthMs(turn), 3 * offset);   // exactly a third, no remainder
        }
        // The Director's example: a 10-year turn, one third = 1200 days.
        Assert.Equal(1200 * Day, cal.At(0, 1, 3).Ms);
    }

    [Fact]
    public void TurnOf_and_PositionInTurn_round_trip_at_every_turn()
    {
        var cal = ShippedCalendar();
        for (long t = 0; t < cal.TotalTurns; t++)
        {
            long start = cal.TurnStart(t).Ms, len = cal.TurnLengthMs(t);
            Assert.Equal(t, cal.TurnOf(new SimInstant(start)));
            Assert.Equal(t, cal.TurnOf(new SimInstant(start + len - 1)));
            var mid = new SimInstant(start + len / 3 + 17);
            var pos = cal.PositionInTurn(mid);
            Assert.Equal(new TurnPosition(t, len / 3 + 17, len), pos);
            Assert.Equal(mid, new SimInstant(cal.TurnStart(pos.Turn).Ms + pos.Num));
            Assert.Equal(start + (len * 2) / 7, cal.At(t, 2, 7).Ms);   // floor(len*2/7) == (len*2)/7 for positive values
        }
        Assert.Throws<ArgumentOutOfRangeException>(() => cal.TurnOf(cal.CampaignEnd));
        Assert.Throws<ArgumentOutOfRangeException>(() => cal.At(0, 4, 3));
    }

    [Theory]
    [InlineData("0.5", 5L, 1)]
    [InlineData("10", 10L, 0)]
    [InlineData("-1500", -1500L, 0)]
    [InlineData("0.50", 5L, 1)]
    [InlineData("2.5", 25L, 1)]
    [InlineData("0.001", 1L, 3)]
    public void Exact_number_text_parses_without_ieee(string text, long mantissa, int scale)
        => Assert.Equal(new ExactNumber(mantissa, scale), ExactNumberText.Parse(text));

    [Theory]
    [InlineData("1e3")]
    [InlineData("1.2.3")]
    [InlineData("-")]
    [InlineData("")]
    [InlineData("1,5")]
    public void Exact_number_text_rejects_unsupported_forms(string text)
        => Assert.Throws<FormatException>(() => ExactNumberText.Parse(text));

    [Fact]
    public void Exact_scaling_rejects_non_integral_products_instead_of_truncating()
    {
        Assert.Equal(180, ExactNumberText.Parse("0.5").TimesExactInteger(360));
        Assert.Equal(900, ExactNumberText.Parse("2.5").TimesExactInteger(360));
        Assert.Throws<FormatException>(() => ExactNumberText.Parse("1.001").TimesExactInteger(360)); // 360.36 days
        Assert.Throws<FormatException>(() => ExactNumberText.Parse("-1500.5").AsExactInteger());
        // A dt of 1.001 years would TRUNCATE to 360 days if the check were lost; the calendar must refuse it.
        var nearlyOne = """{ "bands": [ { "name": "x", "startYear": 0, "endYear": 10, "dtYears": 1.001 } ] }"""u8.ToArray();
        Assert.Throws<FormatException>(() => CampaignCalendar.FromJson(nearlyOne));
    }

    [Fact]
    public void Calendar_rejects_non_whole_day_dt_and_misaligned_bands()
    {
        // 0.001 years = 0.36 days: the kernel loader rejects it (EraTableLoaderTests precedent); so do we.
        var bad = """{ "bands": [ { "name": "x", "startYear": 0, "endYear": 10, "dtYears": 0.001 } ] }"""u8.ToArray();
        Assert.Throws<FormatException>(() => CampaignCalendar.FromJson(bad));
        var misaligned = """{ "bands": [ { "name": "x", "startYear": 0, "endYear": 10, "dtYears": 3 } ] }"""u8.ToArray();
        Assert.Throws<FormatException>(() => CampaignCalendar.FromJson(misaligned));
        var gap = """{ "bands": [ { "name": "a", "startYear": 0, "endYear": 10, "dtYears": 1 }, { "name": "b", "startYear": 11, "endYear": 20, "dtYears": 1 } ] }"""u8.ToArray();
        Assert.Throws<FormatException>(() => CampaignCalendar.FromJson(gap));
        // 2.5 years = 900 days and 0.1 years = 36 days are whole days: accepted (test-only kernel tables use them).
        var ok = """{ "bands": [ { "name": "a", "startYear": 0, "endYear": 5, "dtYears": 2.5 }, { "name": "b", "startYear": 5, "endYear": 6, "dtYears": 0.1 } ] }"""u8.ToArray();
        var cal = CampaignCalendar.FromJson(ok);
        Assert.Equal(900, cal.TurnLengthDays(0));
        Assert.Equal(36, cal.TurnLengthDays(2));
        Assert.Equal(12, cal.TotalTurns);
    }

    [Fact]
    public void Instant_range_is_far_inside_long()
    {
        var cal = ShippedCalendar();
        Assert.True(cal.CampaignEnd.Ms < long.MaxValue / 40_000);    // ~1.9e14 vs 9.2e18
        Assert.Equal(31_104_000_000L, SimInstant.MsPerYear);
    }
}
