using Sim.Core.State;
using Sim.Core.Systems;
using Sim.Core.Systems.Demographics;

namespace Sim.Tests.TestUtil;

/// <summary>
/// An INDEPENDENT test-side replica of the ADR-011 demographic micro-kernel
/// for a single-settlement, single-group 16-cohort world: same closed forms,
/// same pinned op order, bit-identical arithmetic — the "hand-computed
/// expectation" instrument of the exactness suite (a system-side deviation
/// from the documented math breaks equality with this replica). Re-implemented
/// from the PINNED KERNEL CONTRACT (the DemographicsSystem header, which the
/// T2.7b adversarial pass verified against ADR-011), not shared with
/// production code. Scope honesty: the replica pins the closed forms and the
/// composition ORDER as documented — it would catch a formula or order edit,
/// but a change made to BOTH documents and code in lockstep is by definition
/// a new pinned contract, not a drift this instrument can see.
/// </summary>
public static class DemographicsReplica
{
    /// <summary>DriftBound (T4.21-3): Σ_steps max(0, A_step − D_pre) while the
    /// cap is wired — the aging-drift term of the §28 bound
    /// N_nutr(end) ≤ N_lim + DriftBound; 0 when N_lim = +∞. HeadroomRemaining:
    /// H_rem at the end of the turn (H_0 e^(−k dt) when binding throughout).
    /// MinScale: the smallest birth multiplier m over the turn's micro-steps
    /// (1.0 when the cap never bound — a turn on which the population fell
    /// with MinScale == 1.0 fell by nature, not by the cap).</summary>
    public sealed record Result(
        double[] Pop, double Births, double Deaths, double Starved,
        double[] AgingOut, double Reservoir, double DriftBound = 0.0,
        double HeadroomRemaining = double.PositiveInfinity, double MinScale = 1.0);

    /// <summary>The newborn aging weight term of the cap's denominator in its
    /// GENERAL form: newborns credited to cohort 0 age h/width into cohort 1
    /// within the step, so their net adult-equivalent contribution is
    /// W(λ_0 h) e^(−λ_0 h) × (w_0 + advance × (w_1 − w_0)). The kernel uses w_0
    /// alone, exact ONLY because cohortWeights[0] == cohortWeights[1]
    /// (D_CohortWeights_NewbornAgingWeightNeutral asserts the data fact); the
    /// replica carries the general term so a future weight change degrades
    /// to a bounded, visible replica mismatch rather than a silent error.</summary>
    public static double NewbornAgingWeightTerm(double[] cohortWeights, double advance) =>
        advance * (cohortWeights[1] - cohortWeights[0]);

    public static double W(double x) => x < 1e-12 ? 1.0 : (1.0 - Math.Exp(-x)) / x;

    /// <summary>One turn of dt years over initial integer counts at the given
    /// PREV deficit; returns exact (unfloored) flow totals and the final
    /// double cohort vector. reservoir0 seeds the group's rebound bank.
    /// T4.21-3 (ADR-026): <paramref name="dEff"/> is the EFFECTIVE deficit the
    /// starvation rate reads and <paramref name="suppressionArg"/> the one the
    /// fertility suppression reads (G3(b) makes them the same scalar in the
    /// kernel; the replica keeps them separate so both arms are testable);
    /// both default to <paramref name="deficit"/> — today's linear response —
    /// and the rebound RELEASE gate always reads the NOMINAL deficit.</summary>
    /// <para>T4.21-3 headroom growth cap (ADR-026 §2.2(ii)): <paramref name="nLim"/>
    /// is the food-influx limit in adult-equivalents (default +∞: the cap is
    /// skipped entirely, today's kernel); <paramref name="cohortWeights"/> the
    /// per-cohort nutritional weights and <paramref name="headroomRelaxationPerYear"/>
    /// the relaxation k. H_0 = max(0, N_lim − Σ w_c count_c); per step PASS A /
    /// CAP / PASS B mirror the kernel's order exactly.</para>
    public static Result Turn(
        DemographicsConfig d, long[] counts, double deficit, double dt, double reservoir0 = 0.0,
        double? dEff = null, double? suppressionArg = null,
        double nLim = double.PositiveInfinity, double[]? cohortWeights = null,
        double headroomRelaxationPerYear = 0.0)
    {
        double effective = dEff ?? deficit;
        double suppressionDeficit = suppressionArg ?? deficit;
        int n = Cohorts.Count;
        bool capped = !double.IsPositiveInfinity(nLim);
        double[] w = cohortWeights ?? new double[n];
        double hRem = 0.0, driftBound = 0.0, minScale = 1.0;
        if (capped)
        {
            double nNow = 0.0;
            for (int c = 0; c < n; c++) nNow += w[c] * counts[c];
            hRem = Math.Max(0.0, nLim - nNow);
        }
        var pop = new double[n];
        for (int c = 0; c < n; c++) pop[c] = counts[c];
        var starveRate = new double[n];
        var totalRate = new double[n];
        for (int c = 0; c < n; c++)
        {
            double mult = BandViews.IsChild(c) ? d.StarvationChildMultiplier
                : BandViews.IsElder(c) ? d.StarvationElderMultiplier : 1.0;
            starveRate[c] = d.StarvationMortalityMaxPerYear * effective * mult;
            totalRate[c] = d.MortalityPerYear[c] + starveRate[c];
        }
        double suppression = Math.Max(0.0, 1.0 - d.FamineFertilitySuppressionSlope * suppressionDeficit);

        double births = 0.0, deaths = 0.0, starved = 0.0, reservoir = reservoir0;
        var agingOut = new double[n];
        double remaining = dt;
        while (remaining > 1e-9)
        {
            double h = Math.Min(DemographicsSystem.MicroStepYears, remaining);
            remaining -= h;
            double advance = h / Cohorts.WidthYears;

            // PASS A — births (pre-sink populations), suppression, the bank
            // from the UNCAPPED pair, the release from the nominal-d gate.
            double unsuppressed = 0.0;
            for (int c = 0; c < n; c++)
            {
                double f = d.FertilityPerPersonPerYear[c];
                if (f <= 0.0) continue;
                unsuppressed += f * pop[c] * W(totalRate[c] * h) * h;
            }
            double born = unsuppressed * suppression;
            double bank = d.ReboundRecoverableFraction * (unsuppressed - born);
            bool gateOpen = deficit == 0.0 && unsuppressed > 0.0;
            double release = gateOpen ? (reservoir + bank) * Math.Min(1.0, d.ReboundReleaseRatePerYear * h) : 0.0;
            double cand = born + release;

            // CAP — D_pre, A_step and bornMax are closed forms of the pre-birth state.
            double m = 1.0;
            double nutritionBefore = 0.0;
            if (capped)
            {
                double deathsPre = 0.0, agingDrift = 0.0;
                for (int c = 0; c < n; c++)
                {
                    double survive = Math.Exp(-totalRate[c] * h);
                    nutritionBefore += w[c] * pop[c];
                    deathsPre += pop[c] * (1.0 - survive) * w[c];
                    if (c >= n - 1) continue;
                    agingDrift += pop[c] * survive * advance * (w[c + 1] - w[c]);
                }
                double allowed = (1.0 - Math.Exp(-headroomRelaxationPerYear * h)) * hRem;
                double newbornNet = W(totalRate[0] * h) * Math.Exp(-totalRate[0] * h)
                                    * (w[0] + NewbornAgingWeightTerm(w, advance));
                double bornMax = (allowed + deathsPre - agingDrift) / newbornNet;
                m = cand > 0.0 ? Math.Min(1.0, Math.Max(0.0, bornMax) / cand) : 1.0;
                driftBound += Math.Max(0.0, agingDrift - deathsPre);
                minScale = Math.Min(minScale, m);
            }

            // PASS B — commit: bank, (scaled) release, births, newborn credit.
            reservoir += bank;
            if (gateOpen)
            {
                if (m < 1.0) { cand *= m; release *= m; }
                reservoir -= release;
            }
            else if (m < 1.0)
            {
                cand *= m;
            }
            born = cand;
            births += born;
            double survivors = born * W(totalRate[0] * h);
            deaths += born - survivors;
            pop[0] += survivors;

            // Sinks then aging, descending (cascade-free).
            for (int c = n - 1; c >= 0; c--)
            {
                double dead = pop[c] * (1.0 - Math.Exp(-d.MortalityPerYear[c] * h));
                pop[c] -= dead;
                deaths += dead;
                double str = pop[c] * (1.0 - Math.Exp(-starveRate[c] * h));
                pop[c] -= str;
                starved += str;

                if (c >= n - 1) continue;
                double moving = pop[c] * advance;
                pop[c] -= moving;
                pop[c + 1] += moving;
                agingOut[c] += moving;
            }

            if (capped)
            {
                double nutritionAfter = 0.0;
                for (int c = 0; c < n; c++) nutritionAfter += w[c] * pop[c];
                hRem -= nutritionAfter - nutritionBefore;
            }
        }
        return new Result(pop, births, deaths, starved, agingOut, reservoir, driftBound,
            capped ? hRem : double.PositiveInfinity, minScale);
    }
}
