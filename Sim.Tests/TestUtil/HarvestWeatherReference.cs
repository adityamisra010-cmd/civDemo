using Sim.Core;
using Sim.Core.Kernel;
using Sim.Core.State;
using Sim.Core.Systems;
using Sim.Core.Systems.Harvest;

namespace Sim.Tests.TestUtil;

/// <summary>
/// W1 F7 (batch 4; D-055, 2026-10-16; T2 fix round, findings T2-V2 and T2-V3) — A TEST-SIDE RECOMPUTATION of one weather
/// step, written from <see cref="HarvestWeatherSystem"/>'s DOCUMENTED model (its class header), so a rig can assert that the
/// step the shipped pipeline took is that model bit for bit, instead of only reading a hash, a draw count or a road
/// perturbation. It reads the weather system's own RNG streams (replayed on a clone of PREV, never PREV itself) and
/// recomputes, per settlement i in table row order:
/// <list type="bullet">
/// <item>the GEOGRAPHIC kernel (R2b): w_ij = exp(−d_ij / range), d_ij the straight-line distance between the two site cells
///   in ideal-ground cost units (<see cref="GeographicDistance.TryIdealGroundCostUnits"/> — on the cylinder the short way
///   round), self-weight 1;</item>
/// <item>the variance-corrected blend of the kernel-smoothed regional field and the local draw (T3.4b/T3.4c);</item>
/// <item>the stationary start of a settlement with no weather row, then s = max(1, round(dt)) yearly AR(1) sub-steps, and the
///   turn's multiplier as the MEAN of the sub-steps' exp(x − σ²/2) (D-051 ruling 1(b)); with the time average off, x = 0
///   start, one sub-step and the point value.</item>
/// </list>
/// The road-aware travel-cost kernel (the PreWeatherGeography control) is not modelled: the reference refuses it.
/// Pure test code: no RNG of its own, no floats, table-order loops.
/// </summary>
public static class HarvestWeatherReference
{
    /// <summary>The weather rows the step from <paramref name="prev"/> under <paramref name="cfg"/> must publish, one per
    /// settlement in PREV's row order: (settlement, LogDeviation, Multiplier).</summary>
    public static HarvestWeatherRow[] Step(WorldState prev, SimConfig cfg, double dtYears)
    {
        HarvestVarianceConfig h = cfg.HarvestVariance;
        Assert.False(h.LegacyTravelCostKernel, "the reference models the geographic kernel only");
        WorldState scratch = prev.Clone();   // the stream draws advance the clone, never PREV
        var rng = new RngRegistry(scratch);
        int n = prev.Settlements.Count;
        var result = new HarvestWeatherRow[n];
        if (n == 0) return result;

        double range = h.SpatialRangeCostUnits, kmPerCost = TransportQuery.KmPerCostUnit(prev);
        var w = new double[n * n];
        var sumSq = new double[n];
        for (int i = 0; i < n; i++)
        {
            double s = 0.0;
            for (int j = 0; j < n; j++)
            {
                double wij = 1.0;
                if (i != j)
                {
                    wij = 0.0;
                    if (range > 0.0)
                    {
                        Assert.True(GeographicDistance.TryIdealGroundCostUnits(
                            prev, prev.Settlements[i].SiteCell, prev.Settlements[j].SiteCell, kmPerCost, out double d));
                        wij = Math.Exp(-d / range);
                    }
                    if (wij <= 0.0) wij = 0.0;
                }
                w[i * n + j] = wij;
                s += wij * wij;
            }
            sumSq[i] = s;
        }

        double k = Math.Clamp(h.SpatialSharedFraction, 0.0, 1.0);
        double wRegional = Math.Sqrt(k), wLocal = Math.Sqrt(1.0 - k);
        double meanCorrection = 0.5 * h.SigmaLogYield * h.SigmaLogYield;
        var x = new double[n];
        var innovation = new double[n];
        var sumMult = new double[n];
        bool timeAveraged = h.TimeAveragedHarvest, anyNew = false;
        for (int i = 0; i < n; i++)
        {
            int row = RowOf(prev, prev.Settlements[i].Id);
            if (row >= 0) x[i] = prev.HarvestWeather[row].LogDeviation;
            else { x[i] = timeAveraged ? double.NaN : 0.0; anyNew |= timeAveraged; }
        }
        if (anyNew)
        {
            Innovations(prev, rng, n, w, sumSq, wRegional, wLocal, innovation);
            for (int i = 0; i < n; i++) if (double.IsNaN(x[i])) x[i] = h.SigmaLogYield * innovation[i];
        }

        int subSteps = timeAveraged ? Math.Max(1, (int)Math.Round(dtYears, MidpointRounding.AwayFromZero)) : 1;
        double rho = Math.Exp(-(dtYears / subSteps) / h.CorrelationTimeYears);
        double scale = h.SigmaLogYield * Math.Sqrt(Math.Max(0.0, 1.0 - rho * rho));
        for (int step = 0; step < subSteps; step++)
        {
            Innovations(prev, rng, n, w, sumSq, wRegional, wLocal, innovation);
            for (int i = 0; i < n; i++)
            {
                x[i] = rho * x[i] + scale * innovation[i];
                sumMult[i] += Math.Exp(x[i] - meanCorrection);
            }
        }
        for (int i = 0; i < n; i++) result[i] = new HarvestWeatherRow(prev.Settlements[i].Id, x[i], sumMult[i] / subSteps);
        return result;
    }

    /// <summary>Asserts that <paramref name="next"/>'s weather table holds exactly <see cref="Step"/>'s rows (bit for bit,
    /// matched by settlement) for the step <paramref name="prev"/> → <paramref name="next"/> under <paramref name="cfg"/>.</summary>
    public static void AssertStepIs(WorldState prev, IReadOnlyWorldState next, SimConfig cfg, double dtYears, string label)
    {
        HarvestWeatherRow[] expected = Step(prev, cfg, dtYears);
        Assert.Equal(expected.Length, next.HarvestWeather.Count);
        for (int i = 0; i < expected.Length; i++)
        {
            int row = RowOf(next, expected[i].Settlement);
            Assert.True(row >= 0, $"{label}: settlement {expected[i].Settlement.Value} has no weather row");
            HarvestWeatherRow got = next.HarvestWeather[row];
            Assert.True(BitConverter.DoubleToInt64Bits(expected[i].LogDeviation) == BitConverter.DoubleToInt64Bits(got.LogDeviation)
                        && BitConverter.DoubleToInt64Bits(expected[i].Multiplier) == BitConverter.DoubleToInt64Bits(got.Multiplier),
                $"{label}: settlement {expected[i].Settlement.Value}'s weather is ({got.LogDeviation:R}, {got.Multiplier:R}), "
                + $"the documented model's ({expected[i].LogDeviation:R}, {expected[i].Multiplier:R})");
        }
    }

    private static int RowOf(IReadOnlyWorldState w, SettlementId id)
    {
        for (int r = 0; r < w.HarvestWeather.Count; r++) if (w.HarvestWeather[r].Settlement == id) return r;
        return -1;
    }

    private static void Innovations(IReadOnlyWorldState prev, RngRegistry rng, int n, double[] w, double[] sumSq,
        double wRegional, double wLocal, double[] innovation)
    {
        var local = new double[n];
        for (int i = 0; i < n; i++)
            local[i] = StandardNormal(rng.Get(HarvestWeatherSystem.WellKnownId, new RegionId(prev.Settlements[i].Id.Value)));
        for (int i = 0; i < n; i++)
        {
            double acc = 0.0;
            for (int j = 0; j < n; j++)
            {
                double wij = w[i * n + j];
                if (wij <= 0.0) continue;
                acc += wij * local[j];
            }
            double regional = acc / Math.Sqrt(sumSq[i]);
            double variance = 1.0 + 2.0 * wRegional * wLocal / Math.Sqrt(sumSq[i]);
            innovation[i] = (wRegional * regional + wLocal * local[i]) / Math.Sqrt(variance);
        }
    }

    /// <summary>Box–Muller's first output from two uniform draws, the u1 floor keeping log() finite (the system's draw).</summary>
    private static double StandardNormal(RngStream s)
    {
        double u1 = s.NextDouble();
        double u2 = s.NextDouble();
        if (u1 < 1e-300) u1 = 1e-300;
        return Math.Sqrt(-2.0 * Math.Log(u1)) * Math.Cos(2.0 * Math.PI * u2);
    }
}
