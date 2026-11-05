namespace Proto.M7.Time;

/// <summary>A War Pulse on the global ms timeline: [StartMs, EndMs).</summary>
public readonly record struct WarPulse(long Index, long StartMs, long EndMs);

/// <summary>
/// War Pulses scheduled on the global ms timeline, INDEPENDENT of campaign-turn boundaries (spec §2.5; O-2
/// era-varying duration; O-3 = CR-006 §1.4 Option A with crossing). A pulse's length is taken from a per-band
/// INPUT table, indexed by the band containing the pulse's START instant — computed state (the date), the same
/// mechanism the kernel uses for dt (EraTable.DtDaysAt), never an era label (Law 4). A pulse that starts in one
/// band and ends in the next keeps its start-band length.
///
/// VALUES ARE NOT DECIDED (O-2 decided only "era-varying"; REV:463 "weeks in earlier eras and days in modern
/// eras, subject to a dimensional audit"). There is NO shipped default here: callers must pass a table; tests use
/// clearly labelled PLACEHOLDER inputs.
/// </summary>
public sealed class WarPulseSchedule
{
    private readonly CampaignCalendar _calendar;
    private readonly long[] _lengthMsByBand;

    public WarPulseSchedule(CampaignCalendar calendar, long[] pulseLengthMsByBand)
    {
        if (pulseLengthMsByBand.Length != calendar.Bands.Count)
            throw new ArgumentException("need exactly one pulse length per era band");
        foreach (var l in pulseLengthMsByBand)
            if (l <= 0) throw new ArgumentOutOfRangeException(nameof(pulseLengthMsByBand), "pulse length must be positive");
        _calendar = calendar;
        _lengthMsByBand = (long[])pulseLengthMsByBand.Clone();
    }

    public long PulseLengthAt(long startMs) => _lengthMsByBand[_calendar.BandIndexAt(new SimInstant(startMs))];

    /// <summary>
    /// The schedule is defined only for pulses that START inside the campaign, [campaign start, campaign end):
    /// a pulse may run past the campaign end, but no pulse starts at or after it. True iff a pulse follows p.
    /// </summary>
    public bool HasNext(WarPulse p) => p.EndMs < _calendar.CampaignEnd.Ms;

    /// <summary>The pulse following one that ends at endMs (pulses are contiguous once a war is active).
    /// Throws when !HasNext(p) (no pulse starts at or after the campaign end).</summary>
    public WarPulse Next(WarPulse p) => Make(p.Index + 1, p.EndMs);

    public WarPulse First(long startMs) => Make(0, startMs);

    private WarPulse Make(long index, long startMs) => new(index, startMs, startMs + PulseLengthAt(startMs));
}
