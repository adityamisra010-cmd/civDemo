using Sim.Core.State;

namespace Sim.Ui.ViewModel;

/// <summary>
/// T4.18 — THE LABOUR SPLIT AS A REAL ALLOCATION.
///
/// WHAT WAS WRONG, stated as the interaction it produced. The five sector
/// sliders were five INDEPENDENT 0..100 values whose sum was unconstrained, and
/// the sim normalized whatever arrived. So the director could leave the panel
/// reading 20/20/20/20/20 — an apparent 100% — or 97, or 104, with nothing on
/// screen distinguishing the three, and a caption underneath predicting what his
/// numbers would MEAN once divided through: "applies as 19% / 19% / …". That is
/// a calculator with a preview, not a control: the number he set was never the
/// number that ran, and he had to read a second line to find out what he had
/// actually ordered.
///
/// WHAT IT IS NOW. The five weights are a FIXED-SUM allocation of exactly 100.
/// Move one and the others absorb the difference in proportion to their current
/// size, so the total is 100 before the move, during it and after it. The
/// prediction line disappears because the prediction became an identity: with
/// the weights summing to 100, the normalized share the sim runs IS the number
/// on the slider.
///
/// THE ORDER PAYLOAD IS UNCHANGED, and that is deliberate. D-032 weights are
/// still submitted AS TYPED and still normalized by the consumer
/// (<see cref="Sectors.Share"/>); this file constrains what the director can
/// type, not what the order means. No system, equation, schema or constant
/// moved — a sum-100 allocation was always expressible, the UI simply could not
/// hold itself to one.
///
/// DETERMINISM. Every redistribution is integer arithmetic with largest-
/// remainder rounding and a stable tie-break on the LOWEST SECTOR INDEX, so an
/// identical drag produces an identical allocation on any machine. Ties are
/// dense here rather than exotic — an even 20/20/20/20/20 split is the shape a
/// director reaches for first — so the tie rule is pinned by its own test.
/// </summary>
public static class SectorAllocationModel
{
    /// <summary>The invariant: the five weights sum to exactly this, always.</summary>
    public const int Total = 100;

    /// <summary>
    /// The weights a settlement is CURRENTLY running, as integers summing to
    /// exactly <see cref="Total"/>.
    ///
    /// Naive per-sector rounding does not sum to 100 — five shares of 0.153…
    /// each round down and land on 99 — so a panel that snapped that way could
    /// show the director a 99% world he never typed, before he touched
    /// anything. Largest remainder gives the leftover units to the sectors with
    /// the largest fractional parts, lowest index first.
    /// </summary>
    public static void FromShares(in SectorAllocationRow row, Span<int> into)
    {
        RequireShape(into);

        Span<double> exact = stackalloc double[Sectors.Count];
        for (int s = 0; s < Sectors.Count; s++) exact[s] = Sectors.Share(row, s) * Total;
        LargestRemainder(exact, Total, into);
    }

    /// <summary>
    /// The director moved sector <paramref name="moved"/> to
    /// <paramref name="requested"/>. Rewrites <paramref name="weights"/> so that
    /// the moved sector holds exactly what he asked for and the other four
    /// absorb the difference, summing to exactly <see cref="Total"/>.
    ///
    /// Absorption is PROPORTIONAL: a sector already carrying half the remaining
    /// labour gives up half of what has to be found. That is the behaviour that
    /// reads as "the others make room" rather than "some other slider I was not
    /// touching jumped". The two degenerate cases are handled explicitly rather
    /// than left to the arithmetic:
    ///
    ///   - the others are ALL ZERO and the moved sector is being reduced, so
    ///     there is nothing to scale up. The freed labour is spread evenly over
    ///     the four, remainder to the lowest indices.
    ///   - the moved sector is pushed to 100, so the others go to zero. Nothing
    ///     special is needed, but it is the boundary the invariant is easiest to
    ///     break at, and it has a test.
    /// </summary>
    public static void Rebalance(Span<int> weights, int moved, int requested)
    {
        RequireShape(weights);
        if (moved < 0 || moved >= Sectors.Count)
            throw new ArgumentOutOfRangeException(nameof(moved), moved, "not a sector index.");

        int target = Math.Clamp(requested, 0, Total);
        int remaining = Total - target;

        int othersNow = 0;
        for (int s = 0; s < Sectors.Count; s++) if (s != moved) othersNow += weights[s];

        Span<double> exact = stackalloc double[Sectors.Count];
        if (othersNow > 0)
        {
            double scale = (double)remaining / othersNow;
            for (int s = 0; s < Sectors.Count; s++) exact[s] = s == moved ? 0.0 : weights[s] * scale;
        }
        else
        {
            // Nothing to scale: share the freed labour out evenly.
            double each = remaining / (double)(Sectors.Count - 1);
            for (int s = 0; s < Sectors.Count; s++) exact[s] = s == moved ? 0.0 : each;
        }

        Span<int> others = stackalloc int[Sectors.Count];
        LargestRemainder(exact, remaining, others);

        for (int s = 0; s < Sectors.Count; s++) weights[s] = s == moved ? target : others[s];
    }

    /// <summary>True when the weights hold the invariant — the assertion a
    /// caller can make after any sequence of moves.</summary>
    public static bool IsBalanced(ReadOnlySpan<int> weights)
    {
        if (weights.Length != Sectors.Count) return false;
        int sum = 0;
        for (int s = 0; s < weights.Length; s++)
        {
            if (weights[s] is < 0 or > Total) return false;
            sum += weights[s];
        }
        return sum == Total;
    }

    /// <summary>
    /// Rounds <paramref name="exact"/> to integers summing to exactly
    /// <paramref name="total"/>: floor everything, then hand the leftover units
    /// out one at a time to the largest fractional remainders.
    ///
    /// THE TIE-BREAK IS THE POINT (CLAUDE.md: any argmax over doubles carries a
    /// stable integer tie-break). Equal remainders are ordered by SECTOR INDEX,
    /// lowest first, so an even split distributes its remainder the same way
    /// every time instead of following whatever order the comparison happened to
    /// visit. Ties are the common case here, not the corner.
    /// </summary>
    private static void LargestRemainder(ReadOnlySpan<double> exact, int total, Span<int> into)
    {
        Span<double> fraction = stackalloc double[exact.Length];
        int assigned = 0;
        for (int s = 0; s < exact.Length; s++)
        {
            double value = Math.Max(0.0, exact[s]);
            int floor = (int)Math.Floor(value);
            into[s] = floor;
            fraction[s] = value - floor;
            assigned += floor;
        }

        // Hand out the leftover units, one per pass, to the largest remaining
        // fraction. `taken` retires a sector so no sector receives two units
        // before every other sector has been considered for one.
        Span<bool> taken = stackalloc bool[exact.Length];
        while (assigned < total)
        {
            int best = -1;
            double bestFraction = -1.0;
            for (int s = 0; s < exact.Length; s++)
            {
                // Strict >: an equal fraction never displaces the LOWER index,
                // which is the stable integer tie-break the constitution asks
                // for on any argmax over doubles.
                if (taken[s] || fraction[s] <= bestFraction) continue;
                bestFraction = fraction[s];
                best = s;
            }

            if (best < 0)
            {
                // Every sector has had a unit and the total is still short —
                // only reachable when total exceeds the sum of the ceilings.
                // Give the rest to sector 0 rather than looping forever.
                into[0] += total - assigned;
                return;
            }

            into[best]++;
            taken[best] = true;
            assigned++;
        }
    }

    private static void RequireShape(Span<int> weights)
    {
        if (weights.Length != Sectors.Count)
        {
            throw new ArgumentException(
                $"a sector allocation is exactly {Sectors.Count} weights, got {weights.Length}.",
                nameof(weights));
        }
    }
}
