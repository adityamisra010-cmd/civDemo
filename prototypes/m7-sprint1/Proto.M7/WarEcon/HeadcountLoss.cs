using Proto.M7.Time;

namespace Proto.M7.WarEcon;

/// <summary>
/// Law 3 for per-head loss flows (desertion, disease and starvation deaths, animal deaths; lane E fix round 2, finding
/// E2-1). The rates are NOMINAL per-sim-year rates in ‰ of the heads still there, applied once per elapsed day on the
/// declining strength (a part-day pro rata), the same daily round as <see cref="StockDraw"/>. Before this, a pulse took
/// heads × rate × pulse length from the strength at the pulse start (explicit Euler over the whole pulse), so a starving
/// army lost about 7 % more men under 21-day pulses than under 1-day pulses. Now n days in one pulse lose what n one-day
/// pulses lose at the same rates (up to the thousandth-of-a-head floor below). Competing flows (desertion and disease
/// draw on the same men) share one survival product and split the loss pro rata to their rates. Pure integer arithmetic.
/// </summary>
public static class HeadcountLoss
{
    /// <summary>Fixed-point scale of the survival fraction (1e18 = all survive).</summary>
    public static readonly Int128 One = (Int128)1_000_000_000_000_000_000L;
    private const long Day = SimInstant.MsPerDay;
    private static readonly long Y = SimInstant.MsPerYear;

    /// <summary>The fraction of heads that survive <paramref name="dtMs"/> at a total rate of
    /// <paramref name="totalPermilleYear"/>, in units of 1/<see cref="One"/>: each whole day multiplies it by
    /// (1 − rate / (1000 × YearDays)) (floor), the remaining part-day by (1 − rate × remMs / (1000 × MsPerYear)) (floor).</summary>
    public static Int128 Survival(long totalPermilleYear, long dtMs)
    {
        if (totalPermilleYear < 0 || dtMs < 0) throw new ArgumentOutOfRangeException(nameof(dtMs), "a headcount loss needs non-negative inputs");
        Int128 s = One;
        long days = dtMs / Day, rem = dtMs % Day;
        Int128 perDay = (Int128)1000 * SimInstant.YearDays;
        for (long d = 0; d < days && s > 0; d++)
            s = totalPermilleYear >= perDay ? 0 : s * (perDay - totalPermilleYear) / perDay;
        if (rem > 0 && s > 0)
        {
            Int128 den = (Int128)1000 * Y, cut = (Int128)totalPermilleYear * rem;
            s = cut >= den ? 0 : s * (den - cut) / den;
        }
        return s;
    }

    /// <summary>One flow's share of the loss, in thousandths of a head: heads × (1 − survival) × rate / totalRate (floor).
    /// The flows of one survival product never sum to more than heads × 1000.</summary>
    public static long LossMilli(long heads, long permilleYear, long totalPermilleYear, Int128 survival)
    {
        if (heads < 0 || permilleYear < 0 || permilleYear > totalPermilleYear) throw new ArgumentOutOfRangeException(nameof(heads), "invalid loss input");
        if (heads == 0 || permilleYear == 0) return 0;
        return (long)((Int128)heads * (One - survival) * 1000 * permilleYear / (One * totalPermilleYear));
    }
}
