namespace Proto.M7.Time;

/// <summary>
/// Exact piecewise integration of a LINEAR per-turn rate over sub-intervals of a turn (spec §2.4).
/// Units accrued over dt ms at RatePerTurn over a turn of LengthMs ms = floor((rate*dt + carry) / length), with the
/// remainder carried. Because floor((X+Y)/L) == floor(X/L) + floor((X mod L + Y)/L), splitting a turn at ANY
/// points (e.g. at an interrupt) yields exactly the same integer total and final carry as one integration,
/// and a full turn yields exactly RatePerTurn with zero carry.
///
/// NOT generalisable (documented, not implemented): nonlinear campaign integrations (demography Exp, logistic
/// growth, compounding) are NOT piecewise-invariant — f(dt1)+f(dt2) != f(dt1+dt2) — so sub-stepping the campaign
/// pipeline at interrupts would move results and re-pin goldens (CR-006: "every golden re-pins"). That is a
/// kernel dependency; this prototype keeps the campaign pipeline turn-atomic and integrates only linear processes.
/// </summary>
public static class RateIntegrator
{
    public static long Integrate(long ratePerTurn, long dtMs, long turnLengthMs, ref long carry)
    {
        if (ratePerTurn < 0 || dtMs < 0 || turnLengthMs <= 0 || carry < 0 || carry >= turnLengthMs)
            throw new ArgumentOutOfRangeException(nameof(dtMs), "invalid integration input");
        Int128 total = (Int128)ratePerTurn * dtMs + carry;
        Int128 q = total / turnLengthMs;
        carry = (long)(total - q * turnLengthMs);
        return (long)q;
    }
}
