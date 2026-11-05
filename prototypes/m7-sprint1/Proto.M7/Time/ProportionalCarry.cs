namespace Proto.M7.Time;

/// <summary>
/// Proportional carry of an in-progress interval when its length changes (DIR5 §2.5: "Preserve fractional time
/// proportionally when campaign-turn duration changes. Do not discard leftover time."; ADR-061 §3.10, C20).
///
/// An interval of length L_old of which e ms have elapsed is e / L_old complete. Re-denominated to length L_new it keeps
/// exactly that proportion: e' = e * L_new / L_old, held as an EXACT fraction of a millisecond (never floored). Hence
///  - round trip: Carry(Carry(e, a, b), b, a) == e exactly, for every a, b;
///  - telescoping: carrying through L_0 → L_1 → … → L_k equals one carry L_0 → L_k (the product of ratios telescopes);
///  - refusal: a result whose reduced form does not fit long throws OverflowException and changes nothing.
/// The superseded Slice B rule e' = floor(e * L_new / L_old) lost up to 1 ms (one numerator unit) per change.
/// </summary>
public static class ProportionalCarry
{
    /// <summary>Elapsed fraction e / L of an in-progress interval (0 &lt;= e &lt; L).</summary>
    public static ExactFraction ElapsedFraction(long elapsedMs, long lengthMs)
    {
        if (lengthMs <= 0 || elapsedMs < 0 || elapsedMs >= lengthMs)
            throw new ArgumentOutOfRangeException(nameof(elapsedMs), "elapsed time must lie in [0, length)");
        return ExactFraction.Of(elapsedMs, lengthMs);
    }

    /// <summary>The elapsed milliseconds of an interval re-denominated from oldLengthMs to newLengthMs, same proportion, exact.</summary>
    public static ExactFraction Carry(ExactFraction elapsedMs, long oldLengthMs, long newLengthMs)
    {
        if (oldLengthMs <= 0 || newLengthMs <= 0) throw new ArgumentOutOfRangeException(nameof(oldLengthMs), "lengths must be positive");
        return elapsedMs.Scale(newLengthMs, oldLengthMs);
    }
}
