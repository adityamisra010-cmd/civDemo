namespace Sim.Core.Kernel;

/// <summary>
/// A computed flow amount that cannot be represented as whole Int64 units —
/// thrown, never wrapped (the M3 overflow discipline, director-mandated).
/// Distinct from <see cref="LedgerOverflowException"/>: that guards the Ledger
/// chokepoint's own arithmetic; this guards the INTERMEDIATE arithmetic inside
/// systems before a value ever reaches the Ledger.
/// </summary>
public sealed class FlowOverflowException(string message) : Exception(message);

/// <summary>
/// THE INTERMEDIATE-ARITHMETIC GUARD (M3 overflow discipline). Systems compute
/// flows in doubles (rate × dt + remainder) and convert to whole conserved
/// units at exactly one kind of site: the double→long floor before a
/// Ledger call. An UNCHECKED cast there is the wrap risk the Ledger cannot
/// see — in .NET an out-of-range double→long conversion saturates to
/// long.MinValue, which then reads as a negative "amount" with a misleading
/// error, or worse, as a plausible wrong number after further arithmetic.
///
/// HEADROOM (documented so future readers know overflow means BUG, not
/// growth): long.MaxValue ≈ 9.22e18. Realistic civ-sim maxima — world
/// population &lt; 1e10 people; a goods stock bounded by population ×
/// centuries of production &lt; 1e14 units; a single turn's flow
/// (quantity × yield × dt) &lt; 1e12. Every legitimate value sits at least
/// FOUR orders of magnitude under the ceiling; a flow that reaches
/// <see cref="MaxWholeUnits"/> is a broken equation or an absurd config,
/// and the only correct response is a loud, named throw.
/// </summary>
public static class ConservedMath
{
    /// <summary>Inclusive guard bound for double→long flow conversion. Chosen
    /// just under 2^63 so every double at or below it converts exactly
    /// (doubles near 2^63 are spaced 1024 apart; 9.2e18 &lt; 2^63 − 1024).</summary>
    public const double MaxWholeUnits = 9.2e18;

    /// <summary>
    /// Converts an exact (double) flow into whole conserved units by floor —
    /// the D-004 remainder convention — throwing
    /// <see cref="FlowOverflowException"/> when the value is not finite or
    /// exceeds the Int64 headroom. <paramref name="what"/> names the flow in
    /// the failure ("farming harvest for settlement 3"), so the throw is
    /// actionable at the site that computed the bad number.
    /// </summary>
    public static long WholeUnits(double exact, string what)
    {
        if (!double.IsFinite(exact) || exact > MaxWholeUnits || exact < -MaxWholeUnits)
            throw new FlowOverflowException(
                $"{what}: computed flow {exact.ToString("G17", System.Globalization.CultureInfo.InvariantCulture)} " +
                $"units cannot be represented as whole Int64 units (headroom {MaxWholeUnits:E1}; " +
                "long.MaxValue ~9.22e18). Realistic stocks sit orders of magnitude below this — " +
                "an overflowing flow is a BUG or an absurd config, never growth.");
        return (long)Math.Floor(exact);
    }
}
