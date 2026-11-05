namespace Proto.M7.Time;

/// <summary>
/// The prototype's global instant: integer milliseconds since campaign day 0 (spec §2.2).
/// Campaign day 0 is the first era band's startYear (-4000 in the shipped table). A 360-day year
/// (kernel SimClock.YearDays = 360, ADR-002) gives 31,104,000,000 ms/year; the 6,100-year campaign is
/// ~1.9e14 ms, far inside long (max ~9.2e18). There is NO fractional time anywhere: a fractional
/// campaign-turn position is an exact integer instant (see CampaignCalendar.At).
/// </summary>
public readonly record struct SimInstant(long Ms) : IComparable<SimInstant>
{
    public const long MsPerSecond = 1_000;
    public const long MsPerDay = 86_400_000;
    /// <summary>Kernel year length in days (Sim.Core/Kernel/SimClock.cs YearDays = 360; ADR-002). Copied, not referenced.</summary>
    public const long YearDays = 360;
    public const long MsPerYear = YearDays * MsPerDay;

    public static SimInstant FromDays(long days) => new(days * MsPerDay);

    /// <summary>Whole days since day 0 (floor). Integral only when <see cref="IsWholeDay"/>.</summary>
    public long WholeDays => Fixed.FixedMath.DivFloor(Ms, MsPerDay);
    public bool IsWholeDay => Fixed.FixedMath.ModFloor(Ms, MsPerDay) == 0;

    public int CompareTo(SimInstant other) => Ms.CompareTo(other.Ms);
    public static bool operator <(SimInstant a, SimInstant b) => a.Ms < b.Ms;
    public static bool operator >(SimInstant a, SimInstant b) => a.Ms > b.Ms;
    public static bool operator <=(SimInstant a, SimInstant b) => a.Ms <= b.Ms;
    public static bool operator >=(SimInstant a, SimInstant b) => a.Ms >= b.Ms;
}
