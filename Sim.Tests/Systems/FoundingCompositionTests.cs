using Sim.Core;
using Sim.Core.State;
using Sim.Core.Systems;
using Sim.Core.Worldgen;
using Sim.Tests.TestUtil;

namespace Sim.Tests.Systems;

/// <summary>
/// ADR-035 §7 (P-F2) — FOUNDING AGE COMPOSITION AT DEMOGRAPHIC SCALE. A settlement's founding cohort counts are the
/// stable vector (sim.json founding.cohortCounts, p_c = base_c / Σ base) scaled by the settlement-common RC-1 size
/// factor, with per-cohort noise at the scale of a founder group drawn from a stable population: CV_c ≈ 1/√n_c
/// (multinomial / Poisson limit), realised as a uniform factor of amplitude min(1, √(3/n_c)).
///
/// THE DERIVED TOLERANCE. For a settlement of realised total T, write e_c = p_c·T for the stable expectation and
/// X² = Σ_c (count_c − e_c)² / e_c, Pearson's statistic against the stable vector. A multinomial founder group has
/// E[X²] = K − 1 (K the cohorts with p_c &gt; 0). The uniform realisation has per-cohort variance n_c (amplitude
/// √(3/n_c) ⇒ variance n_c·a²/3 = 1) when uncapped and less when capped (small cohorts), plus the rounding's 1/12,
/// and conditioning on the realised total removes one degree of freedom: so the mean of X² over many settlements
/// must lie in [0.5, 1.5]·(K − 1). The superseded per-cohort factor (RC-1's ±0.69, CV 0.4 for every cohort) gives
/// X² ≈ Σ 0.16·n_c ≈ 0.16·T ≈ 64 per 400-person settlement, 4× (K − 1): far outside the band.
///
/// A per-settlement HARD bound comes from the bounded uniform: |count_c − n_c| ≤ min(n_c, √(3·n_c)) + ½ for the
/// settlement-common expectation n_c, so no single cohort can stray by more than about √3 of its own Poisson scale.
/// </summary>
public class FoundingCompositionTests
{
    [Fact]
    public void FoundingCohortShares_LieWithinTheDemographicTolerance_OfTheStableVector()
    {
        SimConfig cfg = TestConfigs.Sim();
        long[] baseCounts = cfg.Founding.CohortCounts;
        double baseTotal = 0;
        int k = 0;
        foreach (long c in baseCounts) { baseTotal += c; if (c > 0) k++; }

        double sumX2 = 0.0;
        int settlements = 0;
        double maxZ = 0.0;
        for (ulong seed = 1; seed <= 10; seed++)
        {
            WorldState w = WorldFounding.Found(TestConfigs.Worldgen(), cfg, seed);
            for (int s = 0; s < w.Settlements.Count; s++)
            {
                SettlementId id = w.Settlements[s].Id;
                var counts = new long[Cohorts.Count];
                long total = 0;
                for (int i = 0; i < w.Buckets.Count; i++)
                {
                    if (w.Buckets[i].Settlement != id) continue;
                    counts[w.Buckets[i].CohortIdx] += w.Buckets[i].Count.Value;
                    total += w.Buckets[i].Count.Value;
                }
                Assert.True(total > 0);
                double x2 = 0.0;
                for (int c = 0; c < Cohorts.Count; c++)
                {
                    double p = baseCounts[c] / baseTotal;
                    if (p <= 0.0) { Assert.Equal(0L, counts[c]); continue; }
                    double e = p * total;
                    double dev = counts[c] - e;
                    x2 += dev * dev / e;
                    // Hard, per cohort: within √3 of the Poisson scale of the expectation, plus the common
                    // factor's share of the total's own deviation (bounded the same way) and rounding.
                    double z = Math.Abs(dev) / Math.Sqrt(e);
                    if (z > maxZ) maxZ = z;
                }
                sumX2 += x2;
                settlements++;
            }
        }

        double meanX2 = sumX2 / settlements;
        Assert.True(settlements >= 100, $"only {settlements} settlements — rig too small");
        Assert.InRange(meanX2, 0.5 * (k - 1), 1.5 * (k - 1));
        // |dev| ≤ √(3 n_c) + p_c·Σ_j √(3 n_j) + 1 ⇒ z ≤ √3 + √3·Σ_j √(p_c p_j) + 1/√e ≤ √3·(1 + Σ_j √p_j) + 1:
        // a deterministic ceiling no realisation of the noise can cross.
        double sumSqrtP = 0.0;
        foreach (long c in baseCounts) sumSqrtP += Math.Sqrt(c / baseTotal);
        Assert.True(maxZ <= Math.Sqrt(3.0) * (1.0 + sumSqrtP) + 1.0, $"max z {maxZ}");
    }
}
