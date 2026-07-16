namespace Sim.Core.Kernel;

/// <summary>
/// The validated era-pacing table (D-006): contiguous, chronologically ordered
/// bands, each with an integer-day dt that divides the band's span exactly —
/// validated at load, so band-boundary ambiguity is structurally impossible
/// (ADR-002). Day 0 is the first band's start (campaign epoch, 4000 BCE). Static
/// config data, not sim state: it lives outside WorldState and is never mutated.
/// </summary>
public sealed class EraTable
{
    public readonly record struct Band(string Name, long StartDay, long EndDay, long DtDays);

    private readonly Band[] _bands;

    internal EraTable(Band[] bands) => _bands = bands;

    public ReadOnlySpan<Band> Bands => _bands;

    /// <summary>First day of the campaign (epoch, always 0).</summary>
    public long CampaignStartDay => _bands[0].StartDay;

    /// <summary>One day past the last band — the tick-through landing day.</summary>
    public long CampaignEndDay => _bands[^1].EndDay;

    /// <summary>
    /// dt for the band containing <paramref name="simDay"/> (kernel rule: dt is
    /// selected by the date at turn start). Bands are half-open [StartDay, EndDay).
    /// Linear scan — the table has a handful of rows and this is not a hot path.
    /// </summary>
    public long DtDaysAt(long simDay)
    {
        for (int i = 0; i < _bands.Length; i++)
        {
            if (simDay >= _bands[i].StartDay && simDay < _bands[i].EndDay)
                return _bands[i].DtDays;
        }
        throw new ArgumentOutOfRangeException(nameof(simDay),
            $"sim day {simDay} is outside the campaign [{CampaignStartDay}, {CampaignEndDay}).");
    }

    /// <summary>
    /// Advances the clock by one turn: dt is the band's at the current date; the
    /// returned clock records the dt that was applied.
    /// </summary>
    public SimClock AdvanceTurn(SimClock clock)
    {
        long dt = DtDaysAt(clock.SimDays);
        return new SimClock(clock.Turn + 1, clock.SimDays + dt, dt);
    }
}
