namespace Sim.Core.Systems.NeedsGrievance;

/// <summary>
/// T3.5 — D-035 aggregation. PURE FUNCTIONS, no state, no world access: the
/// whole of D-035-A (variety) and D-035-B (non-compensatory CES + the Tier A
/// gate) lives here so it can be attacked directly rather than through a
/// settlement fixture.
///
/// WHY THIS IS A SEPARATE UNIT. D-035-B amends a frozen decision (d018:52's
/// weighted sum), and its mandatory acceptance test is a claim about the
/// AGGREGATION FUNCTION, not about the simulation: "with one need pinned at 1.0
/// and another at 0.0, aggregate grievance must remain above a stated floor for
/// ALL values of the first." That is a property of arithmetic and is tested as
/// one. A version reachable only through a full world would be testable only by
/// fixtures, and fixtures are where vacuity hides.
/// </summary>
public static class NeedsAggregation
{
    /// <summary>
    /// D-035-A — VARIETY AS SATISFACTION. Returns the multiplier applied to the
    /// quantity-satisfaction of a basket, given the quantities of the goods in
    /// it.
    ///
    /// THE MECHANISM, not a modifier (law 2). This multiplies INSIDE the
    /// satisfaction equation; it is not a bonus added afterwards. The
    /// distinction is the whole of D-035-A: a variety bonus is a free-floating
    /// buff, whereas a concentration term changes what satisfaction IS. The
    /// same calories from grain alone satisfy Sustenance LESS than the same
    /// calories spread across grain, livestock and fish.
    ///
    /// HERFINDAHL. H = Σ shareᵢ² over basket shares, running from 1/n (perfectly
    /// even across n goods) to 1 (everything from one good). The normalised
    /// concentration is (H − 1/n) / (1 − 1/n), which is 0 for an even basket and
    /// 1 for a monoculture regardless of n — so the penalty means the same thing
    /// whether a basket has two goods or ten.
    ///
    ///   factor = 1 − varietyWeight × normalisedConcentration
    ///
    /// DECREASING IN CONCENTRATION AT FIXED QUANTITY, which is the property the
    /// ruling actually asks for and the one the tests measure.
    ///
    /// n IS THE BASKET'S DECLARED LENGTH, NOT THE COUNT OF GOODS ACTUALLY
    /// OBTAINED. This is the load-bearing detail and it is easy to get backwards.
    /// Monotony is measured against what the basket COULD have been: a household
    /// whose declared diet is grain/livestock/fish but which ate only grain has
    /// n = 3 and a concentration of 1, and takes the full penalty. Counting only
    /// the goods present would give n = 1, no diversity dimension, and no penalty
    /// at all — the grain-only diet would score as though monotony were
    /// impossible, deleting D-035-A precisely in the case it exists for.
    ///
    /// A genuinely single-good basket (Length ≤ 1) has no diversity dimension and
    /// returns 1.0; so does an empty basket, where the quantity term is already
    /// zero and a variety penalty would be double-counting nothing.
    /// </summary>
    public static double VarietyFactor(ReadOnlySpan<double> quantities, double varietyWeight)
    {
        int n = quantities.Length;
        if (n <= 1) return 1.0;

        double total = 0.0;
        for (int i = 0; i < n; i++) total += Math.Max(0.0, quantities[i]);
        if (!(total > 0.0)) return 1.0;

        double herfindahl = 0.0;
        for (int i = 0; i < n; i++)
        {
            double share = Math.Max(0.0, quantities[i]) / total;
            herfindahl += share * share;
        }

        double even = 1.0 / n;                                    // H at perfect evenness
        double normalised = (herfindahl - even) / (1.0 - even);   // 0 even … 1 monoculture
        normalised = Math.Clamp(normalised, 0.0, 1.0);
        return Math.Clamp(1.0 - varietyWeight * normalised, 0.0, 1.0);
    }

    /// <summary>
    /// D-035-B — NON-COMPENSATORY CES AGGREGATION, amending d018:52's weighted
    /// sum. Returns aggregate satisfaction in [0,1] from per-need satisfactions
    /// and weights.
    ///
    ///   S = (Σ ŵₙ · sₙ^ρ)^(1/ρ),   ρ = (σ − 1) / σ,   weights ŵ normalised
    ///
    /// σ &lt; 1 IS THE LOAD-BEARING CONSTRAINT, not a tuning preference. At σ = 1
    /// CES degenerates to Cobb–Douglas; as σ → ∞ it becomes the linear sum this
    /// ruling exists to forbid. Only σ &lt; 1 (hence ρ &lt; 0) makes needs genuine
    /// COMPLEMENTS: with ρ negative, a need approaching zero sends sₙ^ρ to
    /// infinity, the sum to infinity, and S to zero — so abundant comfort cannot
    /// buy off absent shelter, however much of it there is. That is the
    /// non-compensatory property, and it is a consequence of the exponent's
    /// sign rather than a special case bolted on.
    ///
    /// ZERO HANDLING — <paramref name="satisfactionFloor"/>. A satisfaction of
    /// exactly 0 gives 0^ρ = +∞ and then ∞^(1/ρ) = 0: correct in the limit, and
    /// the limit is the problem. Every settlement missing any one need would
    /// aggregate to zero, so a world with nothing and a world with almost
    /// everything would be equally, maximally aggrieved and the model would
    /// discriminate between no two states at all. The floor states that total
    /// deprivation of one need is very bad, not infinitely bad.
    ///
    /// It is a MECHANISM PARAMETER, not a guard, and it is honest about which:
    /// raising it weakens non-compensation continuously, and the D-035-B
    /// acceptance ceiling is DERIVED from it by
    /// <see cref="CeilingWhenOneNeedIsZero"/> rather than chosen next to it. A
    /// floor of 0 recovers the exact limit (up to floating point) and makes the
    /// ruling's test pass more easily, not less — which is exactly why the floor
    /// cannot be defended as a guard.
    /// </summary>
    public static double Aggregate(
        ReadOnlySpan<double> satisfactions, ReadOnlySpan<double> weights, double sigma,
        double satisfactionFloor)
    {
        if (satisfactions.Length == 0) return 1.0;
        if (!(sigma > 0.0) || sigma >= 1.0)
        {
            throw new ArgumentOutOfRangeException(nameof(sigma), sigma,
                "D-035-B requires substitution elasticity sigma in (0,1): at sigma >= 1 the "
                + "aggregation stops being non-compensatory, which is the property the ruling "
                + "exists to guarantee.");
        }

        double rho = (sigma - 1.0) / sigma;   // < 0 for sigma < 1

        // EXACT SATURATION. Every need fully met must aggregate to EXACTLY 1.0,
        // not to 1 − 5e-15. The general path normalises weights and then raises
        // a sum-of-normalised-weights to a power, and neither step is exact in
        // binary floating point — so "nothing is unmet" would accrue a whisker
        // of grievance forever, and every test of the form "a supplied
        // settlement accrues EXACTLY nothing" would have to be written with an
        // epsilon. The constitution's exact-equality culture is the reason this
        // short-circuit exists; it changes no value that was not already 1.
        bool allMet = true;
        for (int i = 0; i < satisfactions.Length; i++)
        {
            if (weights.Length > i && !(Math.Max(0.0, weights[i]) > 0.0)) continue;
            if (!(satisfactions[i] >= 1.0)) { allMet = false; break; }
        }
        if (allMet) return 1.0;

        double weightSum = 0.0;
        for (int i = 0; i < weights.Length; i++) weightSum += Math.Max(0.0, weights[i]);
        if (!(weightSum > 0.0)) return 1.0;   // nothing weighted → nothing unmet

        // 1e-12 is the numerical domain guard under the TUNE floor: it exists
        // only so a floor of exactly 0 raises 0 to a negative power instead of
        // producing NaN. It is not the mechanism.
        double floor = Math.Max(1e-12, Math.Clamp(satisfactionFloor, 0.0, 1.0));
        double acc = 0.0;
        for (int i = 0; i < satisfactions.Length; i++)
        {
            double w = Math.Max(0.0, weights[i]) / weightSum;
            if (w <= 0.0) continue;
            double s = Math.Clamp(satisfactions[i], floor, 1.0);
            acc += w * Math.Pow(s, rho);
        }
        if (!(acc > 0.0) || double.IsInfinity(acc)) return 0.0;

        double aggregate = Math.Pow(acc, 1.0 / rho);
        return double.IsFinite(aggregate) ? Math.Clamp(aggregate, 0.0, 1.0) : 0.0;
    }

    /// <summary>
    /// D-035-B's STATED FLOOR ON GRIEVANCE, derived rather than chosen: the
    /// highest aggregate satisfaction reachable when one need of normalised
    /// weight <paramref name="weightShare"/> sits at zero and EVERY other need
    /// is at a perfect 1.0. Grievance can never fall below the complement of
    /// this, however lavishly the cheap needs are supplied — which is the exact
    /// claim the ruling's mandatory acceptance test makes.
    ///
    ///   S_max = (ŵ·f^ρ + (1 − ŵ))^(1/ρ)
    ///
    /// Deriving it from σ and the floor is the point: a ceiling written as a
    /// literal would be a number fitted to whatever the code happened to do, and
    /// would keep passing after the mechanism changed underneath it.
    /// </summary>
    public static double CeilingWhenOneNeedIsZero(double sigma, double satisfactionFloor, double weightShare)
    {
        if (!(sigma > 0.0) || sigma >= 1.0)
            throw new ArgumentOutOfRangeException(nameof(sigma), sigma, "sigma must be in (0,1).");
        if (!(weightShare > 0.0) || weightShare > 1.0)
            throw new ArgumentOutOfRangeException(nameof(weightShare), weightShare, "weightShare must be in (0,1].");
        double rho = (sigma - 1.0) / sigma;
        double floor = Math.Max(1e-12, Math.Clamp(satisfactionFloor, 0.0, 1.0));
        double acc = weightShare * Math.Pow(floor, rho) + (1.0 - weightShare);
        return Math.Clamp(Math.Pow(acc, 1.0 / rho), 0.0, 1.0);
    }

    /// <summary>
    /// d018:46 TIER A GATE OVERRIDE, retained unchanged by D-035-B and applied
    /// to the weights BEFORE aggregation. Below a floor, a gate need
    /// (Sustenance / Shelter / Safety) scales SUPERLINEARLY and collapses
    /// upper-need weights toward zero: a starving intelligentsia riots over
    /// bread, not press freedom.
    ///
    /// severity_g = (floor − s_g)⁺ / floor  ∈ [0,1], 0 when the need is at or
    /// above its floor.
    ///   gate need:  w × (1 + gain × severity²)      — superlinear in severity
    ///   upper need: w × Π_gates (1 − severity_g)^collapse  — toward zero
    ///
    /// The square is what makes it superlinear: a need slightly below the floor
    /// barely changes the weighting, while one near zero dominates it. A linear
    /// term would make the gate a gentle tilt rather than an override.
    ///
    /// Writes the adjusted weights into <paramref name="adjusted"/>.
    /// </summary>
    public static void ApplyTierAGate(
        ReadOnlySpan<double> satisfactions, ReadOnlySpan<bool> isGate,
        ReadOnlySpan<double> weights, double floor, double gain, double collapse,
        Span<double> adjusted)
    {
        double collapseFactor = 1.0;
        for (int i = 0; i < satisfactions.Length; i++)
        {
            if (!isGate[i]) continue;
            double severity = SeverityOf(satisfactions[i], floor);
            if (severity > 0.0) collapseFactor *= Math.Pow(1.0 - severity, collapse);
        }

        for (int i = 0; i < weights.Length; i++)
        {
            double w = Math.Max(0.0, weights[i]);
            if (isGate[i])
            {
                double severity = SeverityOf(satisfactions[i], floor);
                adjusted[i] = w * (1.0 + gain * severity * severity);
            }
            else
            {
                adjusted[i] = w * collapseFactor;
            }
        }
    }

    private static double SeverityOf(double satisfaction, double floor)
    {
        if (!(floor > 0.0)) return 0.0;
        double s = Math.Clamp(satisfaction, 0.0, 1.0);
        return s >= floor ? 0.0 : (floor - s) / floor;
    }
}
