namespace Sim.Core.Kernel;

/// <summary>
/// The simulation clock (kernel contract §3.4, storage per ADR-002): time is a
/// `long` count of sim-days since the campaign epoch (4000 BCE = day 0), with a
/// fixed 360-day sim year. <see cref="DtYears"/> — the universal rate basis of
/// law 3 — and <see cref="WorldDateYears"/> are derived doubles, never stored.
/// Unmanaged; lives in WorldState and travels with the double-buffer clone.
/// </summary>
public readonly record struct SimClock(long Turn, long SimDays, long DtDays)
{
    /// <summary>Days per sim year (ADR-002). A clock constant, not data.</summary>
    public const long YearDays = 360;

    /// <summary>Current turn length in years — integrate rates against this (law 3).</summary>
    public double DtYears => DtDays / (double)YearDays;

    /// <summary>Years since the campaign epoch (presentation converts to BCE/CE).</summary>
    public double WorldDateYears => SimDays / (double)YearDays;
}
