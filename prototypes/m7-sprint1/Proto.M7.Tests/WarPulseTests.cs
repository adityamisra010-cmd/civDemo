using Proto.M7.Demo;
using Proto.M7.Events;
using Proto.M7.Time;
using static Proto.M7.Tests.TestSupport;

namespace Proto.M7.Tests;

/// <summary>
/// Spec §2.5 — War Pulses crossing campaign turns. ALL pulse lengths, step lengths and budgets here are
/// PLACEHOLDER test inputs (O-2 decided only "era-varying"; REV:463 "weeks in earlier eras and days in modern
/// eras"; budget 2 vs 3 is OPEN). They are not shipped defaults.
/// </summary>
public class WarPulseTests
{
    private const long PlaceholderWeek = 7 * Day;     // PLACEHOLDER: "weeks in earlier eras"
    private const long PlaceholderDay = 1 * Day;      // PLACEHOLDER: "days in modern eras"
    private const long PlaceholderStep = 1 * Day;     // PLACEHOLDER internal step
    private const long PlaceholderBudget = 2;         // PLACEHOLDER (2 or 3 is OPEN)

    /// <summary>PLACEHOLDER per-band table: 7 days for the five pre-1920 bands, 1 day for Modern and Information+.</summary>
    private static long[] PlaceholderTable(CampaignCalendar cal)
    {
        var t = new long[cal.Bands.Count];
        for (int i = 0; i < t.Length; i++) t[i] = cal.Bands[i].StartYear < 1920 ? PlaceholderWeek : PlaceholderDay;
        return t;
    }

    /// <summary>Same band years as the shipped table but every turn 1 year long: different turn boundaries, same bands.</summary>
    private static CampaignCalendar OneYearTurnsCalendar()
    {
        var shipped = ShippedCalendar();
        var parts = shipped.Bands.Select(b => $"{{ \"name\": \"{b.Name}\", \"startYear\": {b.StartYear}, \"endYear\": {b.EndYear}, \"dtYears\": 1 }}");
        return CampaignCalendar.FromJson(System.Text.Encoding.UTF8.GetBytes("{ \"bands\": [ " + string.Join(", ", parts) + " ] }"));
    }

    private static (string trace, string state, PulseDemoWorld w) Run(CampaignCalendar turnCal, CampaignCalendar bandCal, long warStart, long warStop, bool turnByTurn)
    {
        var sched = new WarPulseSchedule(bandCal, PlaceholderTable(bandCal));
        var s = new EventScheduler(warStart);
        var w = new PulseDemoWorld(sched, PlaceholderStep, PlaceholderBudget, warStop);
        w.Begin(s, warStart);
        var runner = new Runner(s, w, SignificantKinds.None);
        long end = warStop + 30 * Day;
        if (turnByTurn)
        {
            for (long t = turnCal.TurnOf(new SimInstant(warStart)); turnCal.TurnStart(t).Ms <= end; t++)
                Assert.Equal(RunStatus.ReachedLimit, runner.RunUntil(turnCal.TurnEnd(t).Ms - 1).Status);
        }
        else runner.RunUntil(end);
        return (s.Trace.HashHex(), w.StateHashHex(), w);
    }

    [Fact]
    public void A_pulse_crossing_a_turn_boundary_is_processed_once_at_exact_instants_regardless_of_turn_boundaries()
    {
        var cal = ShippedCalendar();
        long boundary = cal.TurnEnd(0).Ms;                 // 3600 days
        long warStart = boundary - 2 * Day;                // pulse 0 = [3598d, 3605d): crosses turn 0 -> turn 1
        long warStop = warStart + 20 * Day;

        var single = Run(cal, cal, warStart, warStop, turnByTurn: false);
        var byTurn = Run(cal, cal, warStart, warStop, turnByTurn: true);
        var otherBoundaries = Run(OneYearTurnsCalendar(), cal, warStart, warStop, turnByTurn: true);

        Assert.Equal(single.trace, byTurn.trace);
        Assert.Equal(single.trace, otherBoundaries.trace);
        Assert.Equal(single.state, byTurn.state);
        Assert.Equal(single.state, otherBoundaries.state);

        var w = byTurn.w;
        var p0 = w.Pulses[0];
        Assert.Equal(warStart, p0.StartMs);
        Assert.Equal(warStart + PlaceholderWeek, p0.EndMs);
        Assert.True(p0.StartMs < boundary && p0.EndMs > boundary);   // it really crosses
        Assert.All(w.Pulses, p => { Assert.Equal(1, p.StartCount); Assert.Equal(1, p.EndCount); });
        Assert.Equal(3, w.Pulses.Count);                             // starts at 3598, 3605, 3612 (< stop 3618)
        // Ticks at exact instants start + i*step, 7 per pulse, including across the boundary.
        var ticks0 = w.Actions.Where(a => a.PulseIndex == 0).Select(a => a.AtMs).ToArray();
        Assert.Equal(Enumerable.Range(0, 7).Select(i => warStart + i * PlaceholderStep).ToArray(), ticks0);
        Assert.Contains(boundary, ticks0);
    }

    [Fact]
    public void Action_budget_is_refreshed_at_pulse_start_and_bounds_actions_per_pulse()
    {
        var cal = ShippedCalendar();
        long warStart = cal.TurnEnd(0).Ms - 2 * Day;
        var (_, _, w) = Run(cal, cal, warStart, warStart + 20 * Day, turnByTurn: true);
        Assert.Equal(w.Pulses.Count, w.Refreshes.Count);
        for (int i = 0; i < w.Pulses.Count; i++)
        {
            Assert.Equal(w.Pulses[i].StartMs, w.Refreshes[i].AtMs);  // refresh at pulse START, not at turn start
            Assert.Equal(PlaceholderBudget, w.Refreshes[i].Budget);
            var acts = w.Actions.Where(a => a.PulseIndex == i).ToArray();
            Assert.Equal(PlaceholderBudget, acts.Count(a => a.Accepted));
            Assert.True(acts[0].Accepted && acts[1].Accepted && !acts[2].Accepted);
        }
        // The pulse crossing the turn boundary did NOT get a second refresh at the boundary.
        Assert.DoesNotContain(w.Refreshes, r => r.AtMs == cal.TurnEnd(0).Ms);
    }

    [Fact]
    public void Pulse_length_is_keyed_on_the_band_of_the_pulse_start_date()
    {
        var cal = ShippedCalendar();
        var sched = new WarPulseSchedule(cal, PlaceholderTable(cal));
        int modern = cal.Bands.ToList().FindIndex(b => b.StartYear == 1920);
        long modernStart = cal.Bands[modern].StartMs;
        Assert.Equal(PlaceholderWeek, sched.PulseLengthAt(modernStart - 1));    // starts in Industrial: a week, crossing into Modern
        Assert.Equal(PlaceholderDay, sched.PulseLengthAt(modernStart));
        var p = sched.First(modernStart - 3 * Day);
        Assert.Equal(modernStart + 4 * Day, p.EndMs);
        Assert.Equal(PlaceholderDay, sched.Next(p).EndMs - sched.Next(p).StartMs);
        Assert.Throws<ArgumentException>(() => new WarPulseSchedule(cal, new long[] { 1 }));
        Assert.Throws<ArgumentOutOfRangeException>(() => new WarPulseSchedule(cal, new long[cal.Bands.Count]));
    }
}
