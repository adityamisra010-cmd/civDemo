namespace Sim.Tests.TestUtil;

/// <summary>
/// T3.6 acceptance criterion 4 / R3 rig instrument: detects sustained
/// oscillation in a scalar time series (a price, a stock, a flow). Built and
/// PROVEN before it watches anything (the directed prompt: a detector that
/// cannot fire measures nothing — the T3.3 "teeth without aim" family).
///
/// DEFINITION (stated so the proof tests are checking a contract, not vibes):
/// a series OSCILLATES iff its successive differences change sign at least
/// <see cref="MinFlips"/> times, where differences with |d| ≤ tolerance count
/// as zero (a deadband so numeric dust never manufactures a flip — tolerance
/// is the caller's, stated per measurement). Constant series have no nonzero
/// differences → no flips. Monotone series have same-sign differences → no
/// flips. A single peak/trough has exactly one flip → below the floor.
/// MinFlips = 3 requires at least two full reversals plus one more — a real
/// back-and-forth, not a shock followed by relaxation.
///
/// Pure and deterministic: no state, no RNG, no ordering over doubles beyond
/// sign comparisons.
/// </summary>
public static class OscillationDetector
{
    public const int MinFlips = 3;

    /// <summary>Counts sign flips of successive differences, treating
    /// |difference| ≤ tolerance as zero. Zero-differences neither flip nor
    /// reset the last seen direction (a plateau inside a zig-zag still counts
    /// the zig-zag's reversals).</summary>
    public static int CountFlips(IReadOnlyList<double> series, double tolerance)
    {
        int flips = 0;
        int lastSign = 0;
        for (int i = 1; i < series.Count; i++)
        {
            double d = series[i] - series[i - 1];
            int sign = d > tolerance ? 1 : d < -tolerance ? -1 : 0;
            if (sign == 0) continue;
            if (lastSign != 0 && sign != lastSign) flips++;
            lastSign = sign;
        }
        return flips;
    }

    public static bool Oscillates(IReadOnlyList<double> series, double tolerance) =>
        CountFlips(series, tolerance) >= MinFlips;
}
