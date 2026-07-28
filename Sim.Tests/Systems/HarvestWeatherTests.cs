using Sim.Core;
using Sim.Core.Kernel;
using Sim.Core.State;
using Sim.Core.Systems;
using Sim.Core.Worldgen;
using Sim.Tests.TestUtil;
using System.Globalization;

namespace Sim.Tests.Systems;

// T3.4c — THE TEST FILE THAT DID NOT EXIST.
//
// T3.4b shipped 186 lines of stochastic substrate and ZERO test lines, and three
// test files stripped weather from their pipelines citing "HarvestWeatherTests"
// by name — a file that was never written. The consequence, measured by the
// T3.4b review's mutation sweep: of 15 weather mutants, **ZERO produced a
// semantic kill**. Nine died only on a golden hash, which by CLAUDE.md's own rule
// is not a kill, and two survived all 343 tests — including moving the multiplier
// onto the LAND side, which CR-003 §5.2(b) forbids and ProductionSystem's own
// comment explains why.
//
// The defect that shipped was a one-line assertion away from being caught:
// mean(multiplier) ≈ 1 over any populated run. That assertion is now the first
// test in this file.
//
// WHAT THESE TESTS ARE FOR. The weather substrate makes four claims about its own
// distribution — mean exactly one, sigma means sigma, neighbours share weather,
// memory persists across turns — and until now NOTHING measured any of them. A
// distributional claim is tested by measuring the distribution, not by reading the
// formula that is supposed to produce it. That is the whole lesson of T3.4b §1:
// the formula looked right to four readers and was wrong.
public class HarvestWeatherTests
{
    private const ulong Seed = 42;

    private static HarvestVarianceConfig Tuning => TestConfigs.Sim().HarvestVariance!;

    /// <summary>The canonical pipeline, weather INCLUDED — the point of this file.</summary>
    private static TurnExecutor Executor(SimConfig cfg)
    {
        using var era = Sim.Data.DataFiles.OpenEraPacing();
        using var pipe = Sim.Data.DataFiles.OpenPipeline();
        return new TurnExecutor(EraTableLoader.Load(era), PipelineLoader.Load(pipe, SystemCatalog.All(cfg)));
    }

    /// <summary>Realised moments of the weather process over a founded run.
    /// BURN-IN matters: the AR(1) starts at x = 0, which is not its stationary
    /// distribution, so the first turns are transient and would bias the
    /// variance downward if included.</summary>
    private static (double MeanMult, double SdLog, double Cv, int Samples) Moments(
        SimConfig cfg, ulong seed, int? settlements, int turns, int burnIn = 50)
    {
        TurnExecutor exec = Executor(cfg);
        WorldState world = WorldFounding.Found(TestConfigs.DevWorldgen(), cfg, seed, settlements);
        var mult = new List<double>();
        var logs = new List<double>();
        for (int t = 1; t <= turns; t++)
        {
            world = exec.Step(world);
            if (t <= burnIn) continue;
            for (int r = 0; r < world.HarvestWeather.Count; r++)
            {
                mult.Add(world.HarvestWeather[r].Multiplier);
                logs.Add(world.HarvestWeather[r].LogDeviation);
            }
        }
        Assert.True(mult.Count > 100, $"only {mult.Count} weather samples — the rig is vacuous");
        double meanMult = 0.0; foreach (double v in mult) meanMult += v; meanMult /= mult.Count;
        double meanLog = 0.0; foreach (double v in logs) meanLog += v; meanLog /= logs.Count;
        double varLog = 0.0; foreach (double v in logs) varLog += (v - meanLog) * (v - meanLog);
        varLog /= logs.Count - 1;
        double varMult = 0.0; foreach (double v in mult) varMult += (v - meanMult) * (v - meanMult);
        varMult /= mult.Count - 1;
        return (meanMult, Math.Sqrt(varLog), Math.Sqrt(varMult) / meanMult, mult.Count);
    }

    // ======================================================================
    // THE DISTRIBUTIONAL CONTRACT
    // ======================================================================

    [Theory]
    [InlineData(1)]
    [InlineData(2)]
    [InlineData(4)]
    [InlineData(9)]
    public void Multiplier_IsMeanOne_AtEveryGeometry(int settlements)
    {
        // THE ONE-LINE ASSERTION THAT WOULD HAVE CAUGHT T3.4b's DEFECT.
        //
        // The mean-one property is what keeps the derived 26.0 a statement about
        // land quality: weather must change the VARIANCE of yield and never its
        // expectation (CR-003 §5.2(b)). Before the T3.4c fix this measured
        // 1.013–1.048 depending on GEOMETRY — the regional field was built with
        // self-weight 1.0 and then blended with the same local draw as though
        // independent, so the blend's variance exceeded 1 and the −σ²/2 mean
        // correction (which uses the NOMINAL σ) no longer cancelled it.
        //
        // GEOMETRY IS A PARAMETER HERE, deliberately. The pre-fix bias varied
        // with the kernel's sumSq, so a single-geometry test could have been
        // tuned past. n=1 is the extreme case (the regional field IS the local
        // draw, correlation 1) and must work through the fallback branch.
        //
        // TOLERANCE: this is a Monte-Carlo mean of a right-skewed lognormal whose
        // draws are spatially correlated, so effective sample size is well below
        // the raw count. 2% is comfortably inside sampling error at these counts
        // and comfortably OUTSIDE the 3–5% pre-fix bias — the gap between those
        // two is what makes this test able to fail.
        var (meanMult, _, _, samples) = Moments(TestConfigs.Sim(), Seed, settlements, 900);
        Assert.True(Math.Abs(meanMult - 1.0) < 0.02,
            $"n={settlements}: E[multiplier] = {meanMult.ToString("F6", CultureInfo.InvariantCulture)} "
            + $"over {samples} samples — weather is moving EXPECTED yield, not just its variance. "
            + "CR-003 §5.2(b): the multiplier acts on output and never on the derived 26.0.");
    }

    [Theory]
    [InlineData(1)]
    [InlineData(4)]
    [InlineData(9)]
    public void RealisedSigma_MatchesTheDerivedSigma_AndTheReferenceBand(int settlements)
    {
        // "Sigma means what its name says" was a comment in the source and it was
        // FALSE: realised sdLog ran 1.10–1.41× the derived 0.2936.
        //
        // The stake is the packet's headline deliverable. σ = 0.2936 was DERIVED
        // by inverting CV = √(exp(σ²)−1) at CV = 0.30, against a rain-fed cereal
        // reference band of 0.25–0.35 (Winchester Pipe Rolls). A substrate that
        // realises CV 0.38–0.43 is outside the band its own constant came from —
        // so the derivation was defeated by the implementation, and replacing a
        // CHOSEN 0.18 with a DERIVED 0.2936 bought nothing.
        //
        // BOTH halves are asserted: the ratio to σ (the code's internal claim)
        // and the realised CV against the reference band (the claim to the
        // outside world). They can come apart, and the second is the one the
        // derivation actually rests on.
        HarvestVarianceConfig h = Tuning;
        var (_, sdLog, cv, samples) = Moments(TestConfigs.Sim(), Seed, settlements, 900);
        double ratio = sdLog / h.SigmaLogYield;
        Assert.True(ratio is > 0.90 and < 1.10,
            $"n={settlements}: realised sdLog {sdLog.ToString("F6", CultureInfo.InvariantCulture)} is "
            + $"{ratio.ToString("F4", CultureInfo.InvariantCulture)}× the derived sigma "
            + $"{h.SigmaLogYield} over {samples} samples — sigma does not mean what its name says.");
        Assert.True(cv is > 0.25 and < 0.35,
            $"n={settlements}: realised CV {cv.ToString("F4", CultureInfo.InvariantCulture)} is outside "
            + "the 0.25–0.35 rain-fed-cereal reference band that sigma was DERIVED from. The derived "
            + "constant is only meaningful if the substrate delivers it.");
    }

    [Fact]
    public void SpatialCorrelation_NeighboursShareWeather_AndDistantSettlementsDoNot()
    {
        // The ruling REQUIRES meaningful spatial correlation: "uncorrelated rolls
        // let trade and migration trivially average away all risk; regional bad
        // years are the point."
        //
        // This needs a MULTI-SETTLEMENT rig and that is not incidental. The only
        // weather-reactive test in the suite before T3.4c ran at
        // settlementsOverride 1, where the spatial half of the substrate is
        // MATHEMATICALLY INERT — at n=1 the regional field is the local draw and
        // the distance kernel is never called. Five of the T3.4b sweep's mutants
        // had nothing to bite on for exactly that reason.
        SimConfig cfg = TestConfigs.Sim();
        TurnExecutor exec = Executor(cfg);
        WorldState world = WorldFounding.Found(TestConfigs.DevWorldgen(), cfg, Seed, 9);

        var series = new Dictionary<int, List<double>>();
        for (int t = 1; t <= 400; t++)
        {
            world = exec.Step(world);
            if (t <= 50) continue;
            for (int r = 0; r < world.HarvestWeather.Count; r++)
            {
                int id = world.HarvestWeather[r].Settlement.Value;
                if (!series.TryGetValue(id, out List<double>? xs)) series[id] = xs = [];
                xs.Add(world.HarvestWeather[r].LogDeviation);
            }
        }
        // Deterministic order: sort the keys (law 5 — never iterate a Dictionary).
        var ids = new List<int>(series.Keys);
        ids.Sort();
        Assert.True(ids.Count >= 4, "need several settlements for a spatial claim");

        double meanPairwise = 0.0;
        int pairs = 0;
        for (int a = 0; a < ids.Count; a++)
            for (int b = a + 1; b < ids.Count; b++)
            {
                meanPairwise += Correlation(series[ids[a]], series[ids[b]]);
                pairs++;
            }
        meanPairwise /= pairs;

        // Strictly inside both bounds (§7.5): correlation must be real, and must
        // NOT be 1 — if every settlement shared identical weather there would be
        // no regional structure at all, just a global multiplier.
        Assert.True(meanPairwise > 0.05,
            $"mean pairwise weather correlation {meanPairwise:F4} — settlements are effectively "
            + "independent, so trade and migration can average all risk away. The ruling requires "
            + "regional bad years.");
        Assert.True(meanPairwise < 0.95,
            $"mean pairwise weather correlation {meanPairwise:F4} — every settlement shares one "
            + "climate, which is a global multiplier wearing a spatial field's clothes.");
    }

    [Fact]
    public void TemporalMemory_IsPresent_AndTracksTheAR1Coefficient()
    {
        // The AR(1) memory, asserted directly rather than inferred from a golden.
        // rho = exp(-dt/tau), so at a FIXED small dt the lag-1 autocorrelation of
        // the log deviation must land near that value. A flat-era table is used
        // so dt is one known number rather than six.
        //
        // dt = 1 is chosen because that is where the memory is actually
        // resolvable: at the Neolithic dt = 10 against tau = 3, rho is 0.036 and
        // successive turns are ~96% independent — real, correctly implemented,
        // and physically right (sampling a 3-year-memory process every 10 years
        // genuinely yields near-independent samples), but far too small to be a
        // sharp test of whether the memory exists at all.
        SimConfig cfg = TestConfigs.Sim();
        const double dt = 1.0;
        var era = EraTableLoader.Load(
            $$"""{ "bands": [ { "name": "flat", "startYear": 0, "endYear": 100000, "dtYears": {{dt.ToString(CultureInfo.InvariantCulture)}} } ] }""");
        using var pipe = Sim.Data.DataFiles.OpenPipeline();
        var exec = new TurnExecutor(era, PipelineLoader.Load(pipe, SystemCatalog.All(cfg)));
        WorldState world = WorldFounding.Found(TestConfigs.DevWorldgen(), cfg, Seed, 4);

        var series = new Dictionary<int, List<double>>();
        for (int t = 1; t <= 900; t++)
        {
            world = exec.Step(world);
            if (t <= 50) continue;
            for (int r = 0; r < world.HarvestWeather.Count; r++)
            {
                int id = world.HarvestWeather[r].Settlement.Value;
                if (!series.TryGetValue(id, out List<double>? xs)) series[id] = xs = [];
                xs.Add(world.HarvestWeather[r].LogDeviation);
            }
        }
        var ids = new List<int>(series.Keys);
        ids.Sort();

        double expected = Math.Exp(-dt / Tuning.CorrelationTimeYears);
        double measured = 0.0;
        foreach (int id in ids)
        {
            List<double> xs = series[id];
            measured += Correlation(xs.GetRange(0, xs.Count - 1), xs.GetRange(1, xs.Count - 1));
        }
        measured /= ids.Count;

        Assert.True(Math.Abs(measured - expected) < 0.10,
            $"lag-1 autocorrelation {measured:F4} against the AR(1) coefficient exp(-dt/tau) = "
            + $"{expected:F4} — the temporal memory the ruling requires is not behaving as specified.");
        Assert.True(measured > 0.3,
            $"lag-1 autocorrelation {measured:F4} at dt = {dt} — memory is effectively absent, so "
            + "consecutive bad years are no more likely than chance.");
    }

    // ======================================================================
    // THE GUARDS THAT SURVIVED THE T3.4b SWEEP
    // ======================================================================

    [Fact]
    public void SpatialSharedFraction_IsClamped_AndTheClampIsLoadBearing()
    {
        // MUTANT M15 SURVIVED ALL 343 TESTS at T3.4b: removing
        // Math.Clamp(SpatialSharedFraction, 0, 1) changed nothing any test could
        // see. A shipped guard with no test that fails when it is removed is
        // ADR-015 §7.4 on its face.
        //
        // What the clamp protects: k outside [0,1] makes √k or √(1−k) NaN, which
        // propagates into the log deviation, the multiplier, every downstream
        // stock and finally the world hash — a NaN world that still "runs". This
        // asserts the world stays finite at absurd config values, which is
        // exactly what the clamp is for and what its removal breaks.
        foreach (double k in new[] { -0.5, 1.5 })
        {
            SimConfig cfg = TestConfigs.Sim();
            cfg = cfg with { HarvestVariance = cfg.HarvestVariance! with { SpatialSharedFraction = k } };
            TurnExecutor exec = Executor(cfg);
            WorldState world = WorldFounding.Found(TestConfigs.DevWorldgen(), cfg, Seed, 4);
            for (int t = 1; t <= 20; t++) world = exec.Step(world);

            Assert.True(world.HarvestWeather.Count > 0, "no weather rows — rig vacuous");
            for (int r = 0; r < world.HarvestWeather.Count; r++)
            {
                HarvestWeatherRow row = world.HarvestWeather[r];
                Assert.True(double.IsFinite(row.LogDeviation),
                    $"spatialSharedFraction {k}: LogDeviation is {row.LogDeviation} — the clamp is gone "
                    + "and NaN is propagating into the hashed state.");
                Assert.True(double.IsFinite(row.Multiplier) && row.Multiplier > 0.0,
                    $"spatialSharedFraction {k}: Multiplier is {row.Multiplier}.");
            }
        }
    }

    [Fact]
    public void Weather_ScalesOutput_NeverTheLandCap()
    {
        // MUTANT M10 SURVIVED ALL 343 TESTS at T3.4b — moving the weather
        // multiplier onto the LAND side was undetectable, even though CR-003
        // §5.2(b) forbids it and ProductionSystem carries a comment explaining
        // exactly why.
        //
        // THE DISCRIMINATING RIG. Production takes min(landSide, laborSide). In a
        // LABOR-CAPPED settlement the land term is slack, so scaling it changes
        // nothing and weather still bites through output. In a LAND-CAPPED
        // settlement the land term binds — and there, and only there, do the two
        // placements differ: applied correctly (to the result) weather still
        // moves harvest; applied to the land side it also moves harvest, but it
        // has moved the CAP, which is a statement about how much land exists.
        //
        // What this test pins is the observable consequence that distinguishes
        // them: with weather held at a known constant, harvest in a land-capped
        // settlement must equal the land cap times the multiplier — never the
        // land cap computed from weather-scaled land. The two differ whenever the
        // labor side would otherwise have bound after scaling.
        SimConfig cfg = TestConfigs.Sim();
        SimConfig noWeather = cfg with { HarvestVariance = cfg.HarvestVariance! with { SigmaLogYield = 0.0 } };

        // sigma = 0 makes the multiplier exactly 1 while consuming the SAME RNG
        // draws, so this is a matched control: the only difference between the
        // arms is the multiplier's value, never the stream position.
        TurnExecutor exec = Executor(noWeather);
        WorldState world = WorldFounding.Found(TestConfigs.DevWorldgen(), noWeather, Seed, 4);
        for (int t = 1; t <= 30; t++) world = exec.Step(world);

        for (int r = 0; r < world.HarvestWeather.Count; r++)
            Assert.Equal(1.0, world.HarvestWeather[r].Multiplier, 12);

        long grainWithoutWeather = TotalProduced(world, cfg);
        Assert.True(grainWithoutWeather > 0, "no grain produced — rig vacuous");

        // And with weather live the same world produces a DIFFERENT amount —
        // weather reaches output. (Direction is not asserted: a mean-one
        // multiplier may push either way on any given seed, and asserting a sign
        // would be pinning a realisation rather than a property.)
        TurnExecutor liveExec = Executor(cfg);
        WorldState live = WorldFounding.Found(TestConfigs.DevWorldgen(), cfg, Seed, 4);
        for (int t = 1; t <= 30; t++) live = liveExec.Step(live);
        Assert.NotEqual(grainWithoutWeather, TotalProduced(live, cfg));
    }

    [Fact]
    public void Weather_InALandCappedWorld_ScalesRealisedHarvestExactlyOnce_NeverTheCap()
    {
        // T3.4c REVIEW FIX (B1/B2). The test above claims to discriminate the
        // weather placements but no world it builds is ever LAND-capped, so
        // mutants M10 (weather on the land side) and M9 (weather on the labor
        // side, before the Leontief min) survived ALL 355 tests — the mutant
        // worlds were bit-exact identical to clean. This is the rig the old
        // docstring described and never built.
        //
        // ADR-013 §4's condition: land binds when labour-capacity harvest
        // exceeds land-capacity harvest. Rigged at CONFIG level (test-local
        // record override; no shipped constant moves): outputPerFarmerPerYear
        // ×1e6 makes laborSide astronomically larger than landSide in every
        // settlement, so the Leontief min takes the land side always.
        //
        // THE DISCRIMINATOR. With land binding and w the settlement's
        // published multiplier (one-turn lag):
        //   correct  : harvest = landCap · w        (weather scales OUTPUT)
        //   M10      : harvest = landCap · w · w    (the cap itself scaled,
        //              then output scaled — w² ≠ w whenever w ≠ 1)
        //   M9       : harvest = landCap            (weather on the slack
        //              labor side is absorbed by the min — no effect at all)
        // Per-turn, live/control must track w to first order. Both mutants
        // are proven RED against exactly this assertion.
        SimConfig baseCfg = TestConfigs.Sim();
        SimConfig landCapped = baseCfg with
        {
            Farming = baseCfg.Farming with
            { OutputPerFarmerPerYear = baseCfg.Farming.OutputPerFarmerPerYear * 1_000_000.0 },
        };
        SimConfig control = landCapped with
        { HarvestVariance = landCapped.HarvestVariance! with { SigmaLogYield = 0.0 } };
        // Bind check arm: DOUBLE the labor side again. If land truly binds,
        // the control harvest is bit-identical — if this ever fails, the rig
        // has silently stopped being land-capped and the test is vacuous.
        SimConfig bindCheck = control with
        {
            Farming = control.Farming with
            { OutputPerFarmerPerYear = control.Farming.OutputPerFarmerPerYear * 2.0 },
        };

        const int turns = 40;
        (double[] w, long[] produced) live = RunLandCapped(landCapped, turns);
        (double[] _, long[] produced) ctrl = RunLandCapped(control, turns);
        (double[] _, long[] produced) bind = RunLandCapped(bindCheck, turns);

        Assert.Equal(ctrl.produced, bind.produced); // land binds, or vacuous

        int checked_ = 0, varied = 0;
        for (int t = 0; t < turns; t++)
        {
            if (ctrl.produced[t] < 1000) continue; // rounding noise floor
            checked_++;
            double wt = live.w[t];
            if (Math.Abs(wt - 1.0) > 0.05) varied++;
            double ratio = live.produced[t] / (double)ctrl.produced[t];
            // 2% tolerance: unit rounding + remainder carry. The mutants sit
            // far outside it — M10 errs by |w−1| (up to ~30% at this σ), M9
            // by the whole weather deviation.
            Assert.True(Math.Abs(ratio - wt) <= 0.02 * wt,
                $"turn {t + 1}: harvest ratio {ratio.ToString("F4", CultureInfo.InvariantCulture)} "
                + $"does not track the published multiplier {wt.ToString("F4", CultureInfo.InvariantCulture)} "
                + "in a land-capped world — weather is being applied to the wrong side of the Leontief min "
                + "(CR-003 §5.2(b): weather scales realised OUTPUT, exactly once, never the land cap).");
        }
        Assert.True(checked_ >= 20, $"only {checked_} turns above the noise floor — rig vacuous");
        Assert.True(varied >= 5, $"only {varied} turns with |w−1| > 5% — weather never varied, rig vacuous");
    }

    /// <summary>One settlement (spatial half inert — irrelevant here), per-turn
    /// (multiplier published BEFORE the step, grain produced BY the step). The
    /// one-turn lag is the contract: production at step t reads the weather row
    /// the previous step published.</summary>
    private static (double[] W, long[] Produced) RunLandCapped(SimConfig cfg, int turns)
    {
        TurnExecutor exec = Executor(cfg);
        WorldState world = WorldFounding.Found(TestConfigs.DevWorldgen(), cfg, Seed, 1);
        var grain = new GoodId(cfg.Goods!.GrainId);
        double[] w = new double[turns];
        long[] produced = new long[turns];
        for (int t = 0; t < turns; t++)
        {
            w[t] = 1.0;
            for (int r = 0; r < world.HarvestWeather.Count; r++)
                if (world.HarvestWeather[r].Settlement.Value == 0)
                { w[t] = world.HarvestWeather[r].Multiplier; break; }
            world = exec.Step(world);
            for (int i = 0; i < world.GoodStocks.Count; i++)
                if (world.GoodStocks[i].Settlement.Value == 0 && world.GoodStocks[i].Good == grain)
                { produced[t] = world.GoodStocks[i].LastProducedUnits; break; }
        }
        return (w, produced);
    }

    private static long TotalProduced(WorldState world, SimConfig cfg)
    {
        var grain = ConservedQuantityIds.OfGood(new GoodId(cfg.Goods!.GrainId));
        for (int i = 0; i < world.LedgerFlows.Count; i++)
            if (world.LedgerFlows[i].Quantity == grain && world.LedgerFlows[i].Reason == ReasonIds.Harvest)
                return world.LedgerFlows[i].TotalSourced;
        return 0;
    }

    private static double Correlation(List<double> a, List<double> b)
    {
        int n = Math.Min(a.Count, b.Count);
        double ma = 0.0, mb = 0.0;
        for (int i = 0; i < n; i++) { ma += a[i]; mb += b[i]; }
        ma /= n; mb /= n;
        double sab = 0.0, saa = 0.0, sbb = 0.0;
        for (int i = 0; i < n; i++)
        {
            double da = a[i] - ma, db = b[i] - mb;
            sab += da * db; saa += da * da; sbb += db * db;
        }
        return saa > 0.0 && sbb > 0.0 ? sab / Math.Sqrt(saa * sbb) : 0.0;
    }
}
