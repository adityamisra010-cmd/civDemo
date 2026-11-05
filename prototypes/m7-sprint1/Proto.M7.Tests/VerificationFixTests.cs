using Proto.M7.Demo;
using Proto.M7.Events;
using Proto.M7.Fixed;
using Proto.M7.Rng;
using Proto.M7.Time;
using static Proto.M7.Tests.TestSupport;

namespace Proto.M7.Tests;

/// <summary>
/// Regression tests for the verify-B-foundation findings F1-F10 and the surviving mutants MC, MD, MH, MK
/// (reports/B-foundation.md "Verification and fixes"). Every expectation is derived independently of the code
/// under test (Int128 first-principles arithmetic), never read back from it.
/// All pulse lengths / budgets here are PLACEHOLDER test inputs (O-2 values and the 2-vs-3 budget are OPEN).
/// </summary>
public class VerificationFixTests
{
    // ======================= F1 / mutant MD: completion instant is the exact ceiling =======================

    /// <summary>Units accrued from a start instant within ONE turn length L (band 0 only: no rescale): floor(U*dt/L).</summary>
    private static Int128 Accrued(long u, long dtMs, long l) => (Int128)u * dtMs / l;

    /// <summary>Smallest dt with floor(U*dt/L) &gt;= R, computed independently: ceil(R*L/U).</summary>
    private static long CeilDt(long r, long u, long l) => (long)(((Int128)r * l + u - 1) / u);

    [Theory]
    // R, U, start offset (ms) from campaign day 0. Every row has R*L mod U != 0 (asserted), so floor != ceil.
    [InlineData(1L, 7L, 0L)]
    [InlineData(1000L, 7L, 0L)]
    [InlineData(10L, 7L, 1L)]          // crosses the turn 0 -> 1 boundary with a non-zero carry
    [InlineData(5000L, 4099L, 12345L)] // crosses a boundary, odd offset
    [InlineData(3L, 1_000_003L, 0L)]
    public void Completion_instant_is_the_least_instant_whose_integer_accrual_reaches_R(long r, long u, long start)
    {
        var cal = ShippedCalendar();
        long l = cal.TurnLengthMs(0);
        Assert.NotEqual(0, (Int128)r * l % u);          // premise: floor and ceil differ for this row
        var s = new EventScheduler(start);
        var w = new CampaignDemoWorld(cal);
        long id = w.AddProcess(s, r, u, start);
        new Runner(s, w, SignificantKinds.None).RunUntil(cal.Bands[0].EndMs - 1);
        var p = w.Process(id);
        Assert.True(p.Completed);
        long expected = start + CeilDt(r, u, l);
        Assert.True(expected < cal.Bands[0].EndMs);     // premise: stays in band 0 (one turn length)
        Assert.Equal(expected, p.CompletedAtMs);
        Assert.True(Accrued(u, p.CompletedAtMs - start, l) >= r);       // reached at the completion instant
        Assert.True(Accrued(u, p.CompletedAtMs - 1 - start, l) < r);    // and not one ms earlier (minimal)
    }

    [Fact]
    public void Completion_instant_literal_pin_for_a_non_dividing_rate()
    {
        // L = 3600 d = 311_040_000_000 ms; L/7 = 44_434_285_714 rem 2 => ceil = 44_434_285_715 (floor would be 1 ms early).
        var cal = ShippedCalendar();
        var s = new EventScheduler();
        var w = new CampaignDemoWorld(cal);
        long id = w.AddProcess(s, 1, 7, 0);
        Assert.Equal(44_434_285_715L, w.CompletionInstantInCurrentTurn(w.Process(id)));
        var r = new Runner(s, w, new SignificantKinds(DemoKinds.ConstructionComplete));
        var res = r.RunUntil(cal.TurnEnd(0).Ms - 1);
        Assert.Equal(RunStatus.Interrupted, res.Status);
        Assert.Equal(44_434_285_715L, res.AtMs);
    }

    // ======================= F2 / mutant MH: carry rescale at an era-band boundary =======================

    [Fact]
    public void Carry_is_rescaled_by_floor_at_the_3600_to_1800_day_band_boundary_and_completion_follows_it()
    {
        var cal = ShippedCalendar();
        long last = cal.Bands[1].FirstTurn - 1;              // last 3600-day turn
        long oldLen = cal.TurnLengthMs(last), newLen = cal.TurnLengthMs(last + 1);
        Assert.Equal(3600 * Day, oldLen);
        Assert.Equal(1800 * Day, newLen);
        long start = cal.TurnStart(last).Ms + 1;
        const long R = 10, U = 7;
        // Independent derivation. Turn `last`: dt = oldLen-1, total = 7*(oldLen-1) = 6*oldLen + (oldLen-7).
        long accrued1 = (long)((Int128)U * (oldLen - 1) / oldLen);           // 6
        long carry1 = (long)((Int128)U * (oldLen - 1) - (Int128)accrued1 * oldLen); // oldLen - 7
        Assert.Equal(6, accrued1);
        Assert.True(carry1 >= newLen);                         // premise: an unrescaled carry would break carry < L
        long carry2 = (long)((Int128)carry1 * newLen / oldLen); // documented rule: floor(carry*Lnew/Lold)
        long remaining = R - accrued1;
        long expectedDone = cal.TurnStart(last + 1).Ms + (long)(((Int128)remaining * newLen - carry2 + U - 1) / U);

        var s = new EventScheduler(start);
        var w = new CampaignDemoWorld(cal);
        long id = w.AddProcess(s, R, U, start);
        var runner = new Runner(s, w, SignificantKinds.None);
        runner.RunUntil(cal.TurnStart(last + 1).Ms);          // executes the band-boundary TurnStart
        var p = w.Process(id);
        Assert.Equal(last + 1, p.CurrentTurn);
        Assert.Equal(remaining, p.RemainingUnits);
        Assert.Equal(carry2, p.Carry);
        Assert.True(p.Carry < newLen);
        runner.RunUntil(cal.TurnEnd(last + 1).Ms - 1);
        Assert.True(p.Completed);
        Assert.Equal(expectedDone, p.CompletedAtMs);
    }

    [Fact]
    public void Processes_crossing_every_band_boundary_keep_carry_below_the_turn_length()
    {
        var cal = ShippedCalendar();
        for (int b = 1; b < cal.Bands.Count; b++)
        {
            long t0 = cal.Bands[b].FirstTurn - 1;
            for (int i = 0; i < 40; i++)
            {
                long r = 1 + (long)CounterRng.NextBelow(21, 1, b, i, 0, 0, 9000);
                long u = 1 + (long)CounterRng.NextBelow(21, 2, b, i, 0, 0, 3000);
                long start = cal.TurnStart(t0).Ms + 1 + (long)CounterRng.NextBelow(21, 3, b, i, 0, 0, 1000);
                var s = new EventScheduler(start);
                var w = new CampaignDemoWorld(cal);
                long id = w.AddProcess(s, r, u, start);
                var runner = new Runner(s, w, SignificantKinds.None);
                for (long t = t0; t < t0 + 3 && t + 1 < cal.TotalTurns; t++)
                {
                    runner.RunUntil(cal.TurnEnd(t).Ms);   // includes the next turn's TurnStart (rescale) event
                    var p = w.Process(id);
                    Assert.True(p.Carry >= 0 && p.Carry < cal.TurnLengthMs(p.CurrentTurn), $"band {b} r={r} u={u} carry={p.Carry}");
                }
            }
        }
    }

    // ======================= F3 / mutant MC: NextBelow unbiased where x % n is biased =======================

    [Fact]
    public void NextBelow_is_unbiased_at_a_three_quarter_range_modulus()
    {
        // n = 3*2^62: 2^64 mod n = 2^62, so plain x % n would give P(v < 2^62) = 1/2; the unbiased value is 1/3.
        ulong n = 3UL << 62;
        const int N = 6000;
        int low = 0;
        for (int i = 0; i < N; i++)
        {
            ulong v = CounterRng.NextBelow(5, 1, i, 0, 0, 0, n);
            Assert.True(v < n);
            if (v < (1UL << 62)) low++;
        }
        Assert.InRange(low, 1800, 2200);   // 1/3 of 6000 = 2000, sd ~ 37; the biased value 3000 is ~27 sd away
    }

    // ======================= F4 / mutant MK: pulses are contiguous =======================

    [Fact]
    public void War_pulses_are_contiguous_and_each_takes_the_length_of_its_start_band()
    {
        var cal = ShippedCalendar();
        var table = new long[cal.Bands.Count];
        for (int i = 0; i < table.Length; i++) table[i] = (7 - i) * Day;   // PLACEHOLDER distinct lengths per band
        var sched = new WarPulseSchedule(cal, table);
        long warStart = cal.Bands[1].StartMs - 30 * Day - 5;                 // crosses the band 0 -> 1 boundary
        var s = new EventScheduler(warStart);
        var w = new PulseDemoWorld(sched, Day, 2, stopAfterMs: warStart + 60 * Day);
        w.Begin(s, warStart);
        new Runner(s, w, SignificantKinds.None).RunUntil(warStart + 80 * Day);
        Assert.True(w.Pulses.Count >= 8);
        Assert.Equal(warStart, w.Pulses[0].StartMs);
        for (int i = 0; i < w.Pulses.Count; i++)
        {
            var p = w.Pulses[i];
            Assert.Equal(i, p.Index);
            if (i > 0) Assert.Equal(w.Pulses[i - 1].EndMs, p.StartMs);
            Assert.Equal(table[p.StartMs < cal.Bands[1].StartMs ? 0 : 1], p.EndMs - p.StartMs);
        }
    }

    // ======================= F6: invalid player orders are refused, not thrown =======================

    private static (CampaignDemoWorld w, Runner r, long slow, CampaignCalendar cal) StoppedWorld()
    {
        var cal = ShippedCalendar();
        var s = new EventScheduler();
        var w = new CampaignDemoWorld(cal);
        w.AddProcess(s, 1000, 3000, 0);
        long slow = w.AddProcess(s, 1000, 100, 0);
        var r = new Runner(s, w, new SignificantKinds(DemoKinds.ConstructionComplete));
        Assert.Equal(RunStatus.Interrupted, r.RunUntil(cal.TurnEnd(0).Ms - 1).Status);
        return (w, r, slow, cal);
    }

    [Theory]
    [InlineData(0L, 5L)]      // zero remaining units
    [InlineData(-3L, 5L)]     // negative remaining units
    [InlineData(10L, -1L)]    // negative rate
    public void Invalid_start_process_order_is_recorded_as_refused(long a, long b)
    {
        var (w, r, _, cal) = StoppedWorld();
        int before = w.Processes.Count;
        r.IssueOrder(DemoKinds.OrderStartProcess, a, b);
        Assert.Null(Record.Exception(() => r.RunUntil(cal.TurnEnd(0).Ms - 1)));
        Assert.Equal(before, w.Processes.Count);
        var e = w.Effects.Single();
        Assert.False(e.Accepted);
        Assert.Equal(-1, e.TargetId);
        Assert.Equal(r.Interrupts[0], e.AppliedAtMs);
    }

    [Fact]
    public void Rush_order_whose_rate_would_overflow_is_recorded_as_refused()
    {
        var (w, r, slow, cal) = StoppedWorld();
        long rateBefore = w.Process(slow).UnitsPerTurn;
        r.IssueOrder(DemoKinds.OrderRush, slow, long.MaxValue);
        Assert.Null(Record.Exception(() => r.RunUntil(cal.TurnEnd(0).Ms - 1)));
        Assert.Equal(rateBefore, w.Process(slow).UnitsPerTurn);
        var e = w.Effects.Single();
        Assert.False(e.Accepted);
        Assert.Equal(slow, e.TargetId);
    }

    [Fact]
    public void Valid_start_process_order_is_still_accepted()
    {
        var (w, r, _, cal) = StoppedWorld();
        r.IssueOrder(DemoKinds.OrderStartProcess, 10, 5);
        r.RunUntil(cal.TurnEnd(0).Ms - 1);
        var e = w.Effects.Single();
        Assert.True(e.Accepted);
        Assert.Equal(CampaignDemoWorld.ProcessIdBase + 2, e.TargetId);
    }

    // ======================= F7: a war running to (or past) the campaign end =======================

    [Theory]
    [InlineData(false)]   // the war is stopped exactly at the campaign end
    [InlineData(true)]    // the war is never stopped: the schedule itself ends at the campaign end
    public void War_running_to_the_campaign_end_ends_cleanly(bool unbounded)
    {
        var cal = ShippedCalendar();
        var table = new long[cal.Bands.Count];
        for (int i = 0; i < table.Length; i++) table[i] = Day;     // PLACEHOLDER 1-day pulses
        long end = cal.CampaignEnd.Ms;
        var s = new EventScheduler(end - 5 * Day);
        var w = new PulseDemoWorld(new WarPulseSchedule(cal, table), Day, 2, stopAfterMs: unbounded ? long.MaxValue : end);
        w.Begin(s, end - 5 * Day);
        Assert.Null(Record.Exception(() => new Runner(s, w, SignificantKinds.None).RunUntil(end + 10 * Day)));
        Assert.Equal(5, w.Pulses.Count);
        Assert.Equal(end, w.Pulses[^1].EndMs);
        Assert.Equal(0, s.PendingCount);
    }

    [Fact]
    public void A_pulse_started_before_the_campaign_end_runs_to_its_own_end_and_no_pulse_starts_after()
    {
        var cal = ShippedCalendar();
        var table = new long[cal.Bands.Count];
        for (int i = 0; i < table.Length; i++) table[i] = 7 * Day; // PLACEHOLDER 7-day pulses
        long end = cal.CampaignEnd.Ms;
        var s = new EventScheduler(end - 10 * Day);
        var w = new PulseDemoWorld(new WarPulseSchedule(cal, table), Day, 2, stopAfterMs: long.MaxValue);
        w.Begin(s, end - 10 * Day);
        new Runner(s, w, SignificantKinds.None).RunUntil(end + 30 * Day);
        Assert.Equal(2, w.Pulses.Count);
        Assert.Equal(end + 4 * Day, w.Pulses[1].EndMs);
        Assert.Equal(1, w.Pulses[1].EndCount);
    }

    // ======================= F8: ModFloor at the extremes of long =======================

    [Fact]
    public void ModFloor_is_exact_over_extreme_operands()
    {
        Assert.Equal(-1L, FixedMath.ModFloor(long.MaxValue, -2));
        Assert.Equal(1L, FixedMath.ModFloor(long.MinValue, 3));
        Assert.Equal(0L, FixedMath.ModFloor(long.MinValue, -1));
        Assert.False(new SimInstant(long.MinValue).IsWholeDay);
        long[] vals = { long.MinValue, long.MinValue + 1, -86_400_000, -3, -2, -1, 0, 1, 2, 3, 86_400_000, long.MaxValue - 1, long.MaxValue };
        foreach (long a in vals)
            foreach (long b in vals)
            {
                if (b == 0) continue;
                Int128 r = (Int128)a % b;                         // independent reference: sign of the divisor
                if (r != 0 && (r < 0) != (b < 0)) r += b;
                Assert.Equal((long)r, FixedMath.ModFloor(a, b));
            }
    }

    // ======================= F9: nothing but orders at or after the PlayerOrder class =======================

    [Theory]
    [InlineData(250)]
    [InlineData(251)]
    [InlineData(255)]
    public void World_events_cannot_use_a_class_at_or_after_PlayerOrder(int cls)
    {
        var s = new EventScheduler();
        Assert.Throws<InvalidOperationException>(() => s.Schedule(10, (EventClass)(byte)cls, 0, 0, 1));
        Assert.Equal(0, s.PendingCount);
    }

    [Fact]
    public void World_event_classes_below_PlayerOrder_are_still_accepted()
    {
        var s = new EventScheduler();
        s.Schedule(10, EventClass.Significant, 0, 0, 1);
        s.Schedule(10, (EventClass)249, 0, 0, 1);
        Assert.Equal(2, s.PendingCount);
    }

    // ======================= F10(a): empty band list =======================

    [Fact]
    public void Empty_band_list_is_a_format_error()
        => Assert.Throws<FormatException>(() => CampaignCalendar.FromJson("""{ "bands": [] }"""u8.ToArray()));
}
