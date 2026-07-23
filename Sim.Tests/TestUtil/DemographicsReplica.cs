using Sim.Core.State;
using Sim.Core.Systems;
using Sim.Core.Systems.Demographics;

namespace Sim.Tests.TestUtil;

/// <summary>
/// An INDEPENDENT test-side replica of the ADR-011 demographic micro-kernel
/// for a single-settlement, single-group 16-cohort world: same closed forms,
/// same pinned op order, bit-identical arithmetic — the "hand-computed
/// expectation" instrument of the exactness suite (a system-side deviation
/// from the documented math breaks equality with this replica). Deliberately
/// re-implemented from the ADR, not shared with production code.
/// </summary>
public static class DemographicsReplica
{
    public sealed record Result(
        double[] Pop, double Births, double Deaths, double Starved,
        double[] AgingOut, double Reservoir);

    public static double W(double x) => x < 1e-12 ? 1.0 : (1.0 - Math.Exp(-x)) / x;

    /// <summary>One turn of dt years over initial integer counts at the given
    /// PREV deficit; returns exact (unfloored) flow totals and the final
    /// double cohort vector. reservoir0 seeds the group's rebound bank.</summary>
    public static Result Turn(
        DemographicsConfig d, long[] counts, double deficit, double dt, double reservoir0 = 0.0)
    {
        int n = Cohorts.Count;
        var pop = new double[n];
        for (int c = 0; c < n; c++) pop[c] = counts[c];
        var starveRate = new double[n];
        var totalRate = new double[n];
        for (int c = 0; c < n; c++)
        {
            double mult = BandViews.IsChild(c) ? d.StarvationChildMultiplier
                : BandViews.IsElder(c) ? d.StarvationElderMultiplier : 1.0;
            starveRate[c] = d.StarvationMortalityMaxPerYear * deficit * mult;
            totalRate[c] = d.MortalityPerYear[c] + starveRate[c];
        }
        double suppression = Math.Max(0.0, 1.0 - d.FamineFertilitySuppressionSlope * deficit);

        double births = 0.0, deaths = 0.0, starved = 0.0, reservoir = reservoir0;
        var agingOut = new double[n];
        double remaining = dt;
        while (remaining > 1e-9)
        {
            double h = Math.Min(DemographicsSystem.MicroStepYears, remaining);
            remaining -= h;
            double advance = h / Cohorts.WidthYears;

            // Births (pre-sink populations), suppression, bank, release.
            double unsuppressed = 0.0;
            for (int c = 0; c < n; c++)
            {
                double f = d.FertilityPerPersonPerYear[c];
                if (f <= 0.0) continue;
                unsuppressed += f * pop[c] * W(totalRate[c] * h) * h;
            }
            double born = unsuppressed * suppression;
            reservoir += d.ReboundRecoverableFraction * (unsuppressed - born);
            if (deficit == 0.0 && unsuppressed > 0.0)
            {
                double release = reservoir * Math.Min(1.0, d.ReboundReleaseRatePerYear * h);
                reservoir -= release;
                born += release;
            }
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
        }
        return new Result(pop, births, deaths, starved, agingOut, reservoir);
    }
}
