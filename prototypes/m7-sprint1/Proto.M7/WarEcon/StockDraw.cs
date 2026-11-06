using Proto.M7.Time;

namespace Proto.M7.WarEcon;

/// <summary>
/// Law 3 for draws that take a share of what is left in a stock (foraging, forced requisition, plunder, contributions;
/// lane E fix round 1, finding E-V1). The rate is a NOMINAL per-sim-year rate in ‰ of what is left, applied once per
/// elapsed day (the foraging and requisition parties' daily round: out in the morning, back by night); a part-day is
/// applied pro rata. ONE draw therefore depends only on the time drawn and the stock, never on how that time is cut into
/// pulses: n days drawn in one pulse take exactly what n one-day pulses take (the same integer steps in the same order),
/// so era bands with shorter pulses no longer strip a town faster. Pure integer arithmetic.
/// LIMIT (fix round 2, finding E2-5, measured): this holds per draw, not per stock. Where the world applies two draws on
/// one stock in one pulse one after the other (orderly foraging over the whole pulse, then forced requisition over the
/// whole pulse), or several armies draw on one town in (Priority, Id) order, the TOTAL taken is invariant but the split
/// between the draws still depends on the pulse length (see reports/H-E.md, verification round 2).
/// </summary>
public static class StockDraw
{
    private const long Day = SimInstant.MsPerDay;
    private static readonly long Y = SimInstant.MsPerYear;

    /// <summary>What a draw at <paramref name="permilleYear"/> takes from <paramref name="stock"/> over
    /// <paramref name="dtMs"/>: each whole day takes floor(left × rate / (1000 × YearDays)) of what is left (at most all of
    /// it); the remaining part-day takes floor(left × rate × remMs / (1000 × MsPerYear)).</summary>
    public static long Share(long stock, long permilleYear, long dtMs)
    {
        if (stock < 0 || permilleYear < 0 || dtMs < 0) throw new ArgumentOutOfRangeException(nameof(stock), "a stock draw needs non-negative inputs");
        long left = stock;
        long days = dtMs / Day, rem = dtMs % Day;
        for (long d = 0; d < days && left > 0; d++)
            left -= Math.Min(left, (long)((Int128)left * permilleYear / ((Int128)1000 * SimInstant.YearDays)));
        if (rem > 0 && left > 0)
            left -= Math.Min(left, (long)((Int128)left * permilleYear * rem / ((Int128)1000 * Y)));
        return stock - left;
    }
}
