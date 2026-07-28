using Sim.Core;
using Sim.Core.Kernel;
using Sim.Core.State;
using Sim.Core.Systems;
using Sim.Core.Worldgen;
using Sim.Tests.TestUtil;

namespace Sim.Tests.Systems;

// T2.8: THE CALIBRATION BATTERY. Every corridor is a TWO-SIDED band from
// corridors.json (TUNE data) and every metric must produce signal —
// no-output-is-failure is asserted explicitly before any band check (a
// flat-lined, extinct, or migration-dead world must FAIL the battery, not
// vacuously pass it). The CI members below are time-boxed (2 canonical seeds,
// 2 dev seeds); the ≥20-seed sweep is the nightly `sim autoplay` command
// documented in README.md §Autoplay metrics and .github/workflows/ci.yml.
public class CalibrationBatteryTests
{
    private static AutoplayMetrics RunCanonical(ulong seed, int turns)
    {
        SimConfig cfg = TestConfigs.Sim();
        using var eraStream = Sim.Data.DataFiles.OpenEraPacing();
        using var pipeStream = Sim.Data.DataFiles.OpenPipeline();
        var exec = new TurnExecutor(EraTableLoader.Load(eraStream),
            PipelineLoader.Load(pipeStream, SystemCatalog.All(cfg)));
        WorldState world = WorldFounding.Found(TestConfigs.Worldgen(), cfg, seed, null);
        var col = new AutoplayCollector(seed);
        for (int t = 1; t <= turns; t++) { world = exec.Step(world); col.Observe(world); }
        return col.Finish(world);
    }

    private static AutoplayMetrics RunDev(ulong seed, int turns)
    {
        SimConfig cfg = TestConfigs.Sim();
        using var eraStream = Sim.Data.DataFiles.OpenEraPacing();
        using var pipeStream = Sim.Data.DataFiles.OpenPipeline();
        var exec = new TurnExecutor(EraTableLoader.Load(eraStream),
            PipelineLoader.Load(pipeStream, SystemCatalog.All(cfg)));
        WorldState world = WorldFounding.Found(TestConfigs.DevWorldgen(), cfg, seed, null);
        var col = new AutoplayCollector(seed);
        for (int t = 1; t <= turns; t++) { world = exec.Step(world); col.Observe(world); }
        return col.Finish(world);
    }

    /// <summary>
    /// T3.4b QUARANTINE, CR-003 pattern — dev.migrationGrossPerDecade, seed 7.
    ///
    /// Measured 0.000956939 against the RE-DERIVED floor of 0.001: out of band
    /// by 4%. Reported out-of-corridor honestly rather than widened to fit
    /// (director ruling: "Do NOT widen to fit"). The floor was derived as "below
    /// this a 26x land-quality differential is never exploited and settlements
    /// are isolated islands" — a reasoned bound carrying about ONE significant
    /// figure, so a 4% miss is inside the derivation's own precision and the
    /// honest description is "at the floor", not "below it". Restating a
    /// one-sig-fig argument as a three-decimal corridor edge would be false
    /// precision; MOVING that edge now, having seen the failure, would be
    /// fitting. Neither is taken.
    ///
    /// DIRECTOR RULING, 2026-07-26 — QUARANTINE STANDS, THE FLOOR DOES NOT MOVE,
    /// and the reasoning is ratified as the general rule:
    ///   "Restating a one-significant-figure bound to three decimals to
    ///    accommodate a 4% miss is FALSE PRECISION, and moving it after seeing
    ///    the failure is FITTING. Both make the corridor mean less than leaving
    ///    one seed honestly outside it."
    /// The observation that the miss lies inside the derivation's own precision
    /// is recorded as CONTEXT FOR A FUTURE READER — explicitly NOT grounds for a
    /// change. A reader who later re-derives this floor at higher precision may
    /// resolve the quarantine; nobody may resolve it by widening.
    ///
    /// RE-FRAMED 2026-07-27 (director ruling, T3.4b review). THE ORIGINAL
    /// DESCRIPTION WAS FALSE and is corrected here rather than left to be
    /// discovered again. It said: "The quarantine is SEED-SCOPED and loud: every
    /// other seed and every other corridor is asserted normally." Measured, it is
    /// none of that:
    ///
    ///   - NOT SEED-SCOPED. AssertInBand never receives the seed; the bypass is a
    ///     pure function of (key, value), so it fires for ANY dev seed landing in
    ///     [0.9*lo, lo). Ten of seeds 1-60 do.
    ///   - NOT LOUD. The banner names no seed, so a seed-41 bypass is
    ///     indistinguishable from seed 7's.
    ///   - NOT ONE SEED. Seed 7 is not an outlier. It is the 44th smallest of 60.
    ///
    /// THE MEASURED DISTRIBUTION, dev worldgen, 1000 turns, seeds 1-60, floor
    /// 0.001 (measured by the implementing agent for this re-frame, not restated
    /// from a review):
    ///
    ///   below the floor      52 / 60          in band            8 / 60
    ///   in the silent window 10 / 60          worst   seed 26    0.00044986 (-55.0%)
    ///   median  (seed 54)    0.000858572 (-14.1%)
    ///   best    (seed 14)    0.00115079  (+15.1%)
    ///   seed 7               0.000956939 (-4.3%)  <- among the SMALLEST misses
    ///   seed 42              0.00109699  (+9.7%)  <- in band, but by 9.7%
    ///
    /// So this is not one seed sitting just outside a good corridor. THE WHOLE DEV
    /// SEED SET STRADDLES THE FLOOR, with the bulk below it and the median missing
    /// by 14%. Describing that as a single-seed quarantine made the instrument
    /// MISLEADING rather than merely narrow — it hid the majority of its own
    /// failures behind a bypass that announces one of them.
    ///
    /// THE FLOOR DOES NOT MOVE. This is a DESCRIPTION fix, not a band fix; the
    /// T3.4b ruling against widening stands untouched, and nothing here re-tunes
    /// anything. What is corrected is the claim, which was written into a commit
    /// message and this docstring WITHOUT BEING MEASURED (ADR-015 §7.12).
    ///
    /// T3.4c RE-MEASUREMENT IS JUDGED AGAINST THIS BASELINE. The weather variance
    /// defect (T3.4b review §1) inflated realised sigma ~1.33x, and gross migration
    /// with it, so T3.4c's corrected-world numbers must be compared against the
    /// honest distribution above. A quarantine that hid the majority of its own
    /// failures would have made those before/after deltas meaningless.
    ///
    /// THE CODE IS NOT FIXED HERE — T3.4c item 4 threads the seed, adds teeth in
    /// both directions, adds a band-immovability guard and retires the dead
    /// QuarantinedSeedValue. This commit corrects only what the artifact CLAIMS,
    /// so the claim and the code stop disagreeing while the fix is scheduled.
    /// </summary>
    // The T3.4b declarations that sat here are GONE. QuarantinedSeedValue was
    // dead code — declared, never read — so drift inside the bypass window was
    // invisible. Superseded by AssertDevMigrationQuarantine above, whose envelope
    // is measured, literal, and actually read.

    private static void AssertInBand(Corridors c, string key, double value)
    {
        // T3.4c: the value-shaped bypass that used to live here is GONE. It was
        // ratified as "seed-scoped and loud" and was neither — it fired for ANY
        // dev seed in [0.9*lo, lo), which was ten of seeds 1-60, and named none of
        // them. The dev migration corridor now goes through
        // AssertDevMigrationQuarantine below, which takes the seed.
        Assert.False(double.IsNaN(value), $"{key}: metric produced NO OUTPUT — battery failure");
        (double lo, double hi) = c.Band(key);
        Assert.True(value >= lo && value <= hi,
            $"{key}: {value.ToString("G6", System.Globalization.CultureInfo.InvariantCulture)} " +
            $"outside [{lo}, {hi}]");
    }

    /// <summary>
    /// T3.4c — THE DEV MIGRATION CORRIDOR, quarantined CORRIDOR-WIDE and with
    /// teeth in every direction. Replaces the T3.4b "seed 7" quarantine, which
    /// described an outlier where the truth is a corridor-wide deviation.
    ///
    /// WHY CORRIDOR-WIDE. Measured after the T3.4c variance fix, dev worldgen,
    /// 1000 turns, floor 0.001: 19 of 20 seeds BELOW, median −30.5%, worst seed 6
    /// at −64.1%, best seed 11 at +7.5%. Seed 7 sits at −20.0%. Before the fix the
    /// median was −14.1% — the excess variance had been INFLATING gross migration
    /// and masking how far this world sits from the corridor. A defect can mask
    /// the distance to a corridor as easily as it can create one (ADR-015 §7.13).
    ///
    /// WHY IT IS NOT A BAND PROBLEM — the discriminator the director ordered, with
    /// its reading fixed BEFORE it ran. The SAME corridor on the CANONICAL 1024 px
    /// world: 5 of 5 seeds IN BAND, +15.0% to +63.6% above floor. Canonical in
    /// band with dev below is the pre-committed signature of M4 blocking material
    /// B-1b: the dev preset is not a scale model of the shipped world (¼ the
    /// linear extent at the same kmPerPx, hence +37% land movement cost and 3.1×
    /// site packing), so a corridor calibrated on it measures the preset.
    ///
    /// THEREFORE the floor does NOT move and nothing is retuned. Canonical is
    /// asserted normally by AssertInBand and is untouched by any of this.
    ///
    /// TEETH, which the T3.4b quarantine had in NO direction:
    ///   BAND — the band may not move while the quarantine stands.
    ///   DOWN — below the recorded envelope is a NEW defect, not more of the same.
    ///   UP   — back inside the corridor RESOLVES it and must fail loudly, so it
    ///          cannot rot into silence.
    ///
    /// The envelope is LITERAL and deliberately NOT derived from `lo`: a
    /// quarantine expressible as a fraction of the band it is quarantined against
    /// can be widened by widening the band, which is the move the director forbade.
    /// </summary>
    private const string QuarantinedKey = "dev.migrationGrossPerDecade";

    /// <summary>
    /// PER-SEED recorded values, measured at the T3.4c pin and self-verifying:
    /// the drift tooth below fails the moment either recorded value stops
    /// matching what the battery measures, so a stale pin cannot rot silently.
    ///
    /// T3.4c REVIEW FIX (H3). The first version of this envelope used the
    /// 20-seed distribution's WORST value (0.000359) as the floor for every
    /// seed, which gave the two call-site seeds 62–68% of silent headroom —
    /// measured: cutting baseRatePerYear 300× and disabling famine flight
    /// entirely still landed inside the silent window (seed 7 → 0.000429).
    /// A quarantine whose tooth cannot see near-total disablement of the
    /// mechanism it watches is the T3.4b bypass one level up.
    ///
    /// DriftTolerance 0.75 is MEASURED, not chosen to feel right: the largest
    /// legitimate substrate correction observed moved seed 7 by ×0.836 (the
    /// variance fix, 0.000957 → 0.000800 — must NOT fire), and the measured
    /// mechanism-disablement signature is ×0.536 (300× rate cut + famine
    /// flight off — MUST fire). 0.75 sits between them: gross mechanism loss
    /// is caught, honest re-measurement after a ruled substrate change re-pins
    /// the recorded values deliberately, like a golden.
    ///
    /// The former ceiling constant (0.00115) is DELETED: it sat above `lo`,
    /// so its assert ran after `value &lt; lo` with an empty failure set — a
    /// tooth that could never bite (review F6, verified). Upward motion is the
    /// resolution tooth's job, and for both call-site seeds `lo` is nearer
    /// than any envelope ceiling could be.
    /// </summary>
    private const double QuarantineRecordedSeed42 = 0.000931705;
    private const double QuarantineRecordedSeed7 = 0.000799951;
    private const double QuarantineDriftTolerance = 0.75;

    private static void AssertDevMigrationQuarantine(Corridors c, ulong seed, double value)
    {
        Assert.False(double.IsNaN(value), $"{QuarantinedKey}: metric produced NO OUTPUT — battery failure");
        (double lo, double hi) = c.Band(QuarantinedKey);

        Assert.True(lo == 0.001 && hi == 0.01,
            $"{QuarantinedKey}: band moved to [{lo}, {hi}] while the T3.4c corridor-wide quarantine " +
            "stands. The floor was ruled IMMOVABLE — take it to a ruling, do not re-tune.");

        double recorded = seed == 42ul ? QuarantineRecordedSeed42 : QuarantineRecordedSeed7;
        Assert.True(seed is 42ul or 7ul,
            $"seed {seed} reached the quarantine helper without a recorded envelope value — " +
            "record it here before asserting against it.");

        Assert.True(value >= recorded * QuarantineDriftTolerance,
            $"seed {seed}: {Inv(value)} fell below {QuarantineDriftTolerance:F2}× its recorded value " +
            $"{Inv(recorded)} — the dev world has degraded beyond the quarantined deviation " +
            "(measured disablement signature ×0.536; largest legitimate correction ×0.836). " +
            "A NEW defect, or a ruled substrate change that must re-pin this envelope deliberately.");
        // No per-seed UPPER tooth, deliberately: recorded / 0.75 exceeds `lo`
        // for both call-site seeds, so any such assert would sit behind the
        // resolution tooth with an empty failure set — the same dead-ceiling
        // defect (review F6) this fix deletes. Upward motion has exactly one
        // meaning here, resolution, and exactly one tooth:
        Assert.True(value < lo,
            $"seed {seed}: {Inv(value)} is back INSIDE the corridor [{lo}, {hi}] — the B-1b dev-preset " +
            "deviation is RESOLVED for this seed. Re-measure the dev seed set; if the distribution has " +
            "returned, delete AssertDevMigrationQuarantine and restore the plain AssertInBand.");

        Console.WriteLine(
            $"T3.4c CORRIDOR-WIDE QUARANTINE (dev preset, B-1b): {QuarantinedKey} seed {seed} = " +
            $"{Inv(value)}, {(1 - value / lo):P1} below the floor {lo}. NOT one seed: 19 of dev seeds " +
            "1-20 sit below this floor, median -30.5%, worst -64.1%. The SAME corridor on the CANONICAL " +
            "world is 5/5 IN BAND (+15.0% to +63.6%), which is why this is the preset and not the band. " +
            "Floor NOT moved; escalated as M4 blocking material B-1b.");
    }

    // --- canonical corridors (fed era, 650 turns to year 4500) ---------------

    [Theory]
    [InlineData(1ul)]
    [InlineData(2ul)]
    public void Canonical_FedCorridors_AllInBand(ulong seed)
    {
        Corridors c = Corridors.Load();
        AutoplayMetrics m = RunCanonical(seed, 650);

        // NO-OUTPUT-IS-FAILURE: the world must be alive, breeding, and moving.
        Assert.True(m.FinalPopulation > 0, "extinct world — battery vacuous");
        long totalBirths = 0, totalMoves = 0;
        for (int i = 0; i < m.Births.Count; i++) { totalBirths += m.Births[i]; totalMoves += m.MigrationGross[i]; }
        Assert.True(totalBirths > 0, "no births in 650 turns — vitals dead");
        Assert.True(totalMoves > 0, "no migration in 650 turns — corridor vacuous");
        Assert.True(m.ArableKm2 > 0.0, "no arable area — density corridor vacuous");

        (double from, double to) = Corridors.WindowYears("canonical", "fedGrowthPerYear");
        AssertInBand(c, "canonical.fedGrowthPerYear",
            CalibrationAnalysis.WindowGrowthPerYear(m, from, to));
        (double bFrom, double bTo) = Corridors.WindowYears("canonical", "crudeBirthRatePer1000");
        AssertInBand(c, "canonical.crudeBirthRatePer1000",
            CalibrationAnalysis.CrudeRatePerPersonYear(m, m.Births, bFrom, bTo) * 1000.0);
        (double dFrom, double dTo) = Corridors.WindowYears("canonical", "crudeDeathRatePer1000");
        AssertInBand(c, "canonical.crudeDeathRatePer1000",
            CalibrationAnalysis.CrudeRatePerPersonYear(m, m.Deaths, dFrom, dTo) * 1000.0);

        (double child, double adult, double elder) = CalibrationAnalysis.PyramidShares(m);
        AssertInBand(c, "canonical.pyramidChildShare", child);
        AssertInBand(c, "canonical.pyramidAdultShare", adult);
        AssertInBand(c, "canonical.pyramidElderShare", elder);

        AssertDensityKnownDeviation(c, CalibrationAnalysis.DensityPerArableKm2(m));
        AssertInBand(c, "canonical.migrationGrossPerDecade", CalibrationAnalysis.MigrationGrossPerDecade(m));
    }

    /// <summary>
    /// canonical.densityPerArableKm2 — KNOWN DEVIATION, OPEN under
    /// docs/adr/cr-002.md (diagnosis) and docs/adr/cr-003.md (the ruling this
    /// now waits on). The band is HELD at [0.15, 0.6] and the deviation is
    /// recorded with teeth in both directions; it is not absorbed.
    ///
    /// THE DEVIATION CHANGED SIDES AT T3.2b. Before: 0.1415–0.1630 across four
    /// seeds, three of them BELOW the floor. Now: 2.252 (seed 1) and 2.385
    /// (seed 2), well ABOVE the ceiling. Both readings were produced by the
    /// same underlying fault, and the flip is the evidence that it was a
    /// DENOMINATION problem rather than a demographic one:
    ///
    ///  - the old numerator was correct and the old denominator was ~17x too
    ///    large, because the catchment was a 205 km isochrone rather than an
    ///    economic hinterland (809,711 fertility-weighted km² world-wide);
    ///  - the corrected 50 km hinterland gives 50,516–54,621, and the SAME
    ///    population divided by a denominator 15x smaller reads 15x denser.
    ///
    /// Population is essentially unchanged and is not what moved: it is set by
    /// the demographic vectors integrated over the era table, not by the food
    /// ceiling (CR-003 §2.3), so the measured density is now almost entirely a
    /// statement about catchment GEOMETRY divided into a demography-driven
    /// numerator. Note the measured value is INSIDE the honestly re-derived
    /// frontier band of [0.25, 2.30] recorded in corridors.json at T3.1 —
    /// barely, at its very top — and outside the standing [0.15, 0.6]. Raising
    /// the band to the re-derived one is also a change to the instrument and
    /// needs the same ruling, so it is not done here.
    ///
    /// The assertion keeps teeth in BOTH directions: it fails if the world
    /// drifts further above the recorded envelope (a new defect on top of the
    /// open one), and it fails if it falls back toward the corridor (which
    /// would mean CR-002/CR-003 resolved and this quarantine must be deleted).
    /// Delete this method and restore AssertInBand the moment the director
    /// rules.
    /// </summary>
    private static void AssertDensityKnownDeviation(Corridors c, double value)
    {
        const string Key = "canonical.densityPerArableKm2";
        Assert.False(double.IsNaN(value), $"{Key}: metric produced NO OUTPUT - battery failure");
        (double lo, double hi) = c.Band(Key);
        Assert.True(lo == 0.15 && hi == 0.6,
            $"{Key}: the corridor band moved to [{lo}, {hi}] while CR-002/CR-003 are OPEN - " +
            "the band may not be re-tuned to absorb this deviation; take the CR to a ruling.");

        // The recorded deviation envelope — an ANCHOR, not a band.
        //
        // PROVENANCE, full row (canonical 1024 px preset, 650 turns, seeds
        // 1/2, measured by the implementing agent, not cited):
        //   T3.2b record:      2.252 / 2.385
        //   main b60e49e:      2.26310 / 2.38654   (re-measured for T3.5b)
        //   T3.5b (this tree): 1.70191 / 1.76610
        // The before column agreeing with T3.2b within 0.5% ACROSS FOUR
        // PACKETS (T3.3, T3.4, T3.4b, T3.4c — including a one-third change in
        // realised sigma) is itself a finding: CR-003 §2.3's population
        // yield-insensitivity, confirmed at head.
        //
        // WHY THE T3.5b VALUE FELL — measured, not assumed (§7.10: the first
        // version of this comment attributed it to the food economy and was
        // WRONG). Population is UNCHANGED (120,811/120,558 → 120,588/120,585,
        // ±0.2%): the −24% is the DENOMINATOR — the default 0.08 construction
        // share builds paths, the network grows, catchments recompute, and
        // effective arable expands 53,385 → 70,851 km² (+33%). A geometry
        // effect of the new default, not a harvest effect. The food economy's
        // own response is item 1b's SECOND pre-committed reading: grain
        // accumulation slowed (303M → 125M at turn 650, −59%) but reserves
        // remain ~1,240 years and starvation stays 0 — the unbounded granary
        // masks the perturbation. DIRECT B-2 EVIDENCE, escalated there.
        //
        // The corridor band [0.15, 0.6] is untouched; CR-002/CR-003 stay OPEN
        // (still ~3x above the ceiling). The window is the measured envelope
        // with the same modest margin.
        const double DeviationFloor = 1.55, DeviationCeiling = 1.95;
        Assert.True(value <= DeviationCeiling,
            $"{Key}: {Inv(value)} has drifted FURTHER above the corridor ceiling {hi} than the " +
            $"recorded deviation window [{DeviationFloor}, {DeviationCeiling}] - the world is " +
            "getting denser, which is a NEW defect on top of the open one.");
        Assert.True(value >= DeviationFloor,
            $"{Key}: {Inv(value)} has fallen below the recorded deviation window - if it is back " +
            $"inside the corridor [{lo}, {hi}], CR-002/CR-003 are resolved: delete this " +
            "quarantine and restore the plain AssertInBand.");
    }

    private static string Inv(double v) =>
        v.ToString("G6", System.Globalization.CultureInfo.InvariantCulture);

    // --- era-boundary continuity: PERMANENT battery member -------------------

    [Fact]
    public void Canonical_EraBoundaryContinuity_PermanentBatteryMember()
    {
        // The ADR-011 acceptance pin, promoted into the battery FOREVER: the
        // canonical autoplay crosses Neolithic (dt 10) -> Bronze (dt 5) at
        // year 2500, and the windowed growth rate on either side of the
        // boundary must be continuous within 0.1/1000-yr — a dt-dependent
        // kernel reads as a step in r at every era gate, which is exactly the
        // CR-001 fragility this test exists to keep dead.
        AutoplayMetrics m = RunCanonical(1, 650);
        double before = CalibrationAnalysis.WindowGrowthPerYear(m, 1600.0, 2500.0);
        double after = CalibrationAnalysis.WindowGrowthPerYear(m, 2500.0, 3400.0);
        Assert.False(double.IsNaN(before) || double.IsNaN(after),
            "era-boundary windows produced no output — battery vacuous");
        Assert.True(Math.Abs(before - after) <= 0.0001,
            $"growth discontinuity at the Neolithic->Bronze gate: " +
            $"{before * 1000:F4}/1000-yr vs {after * 1000:F4}/1000-yr");
    }

    // --- dev-world Malthus corridors (capacity horizon, 1000 turns) ----------

    [Theory]
    [InlineData(42ul)]
    [InlineData(7ul)]
    public void Dev_MalthusCorridors_AllInBand(ulong seed)
    {
        Corridors c = Corridors.Load();
        AutoplayMetrics m = RunDev(seed, 1000);

        // NO-OUTPUT-IS-FAILURE: the world must still be alive and breeding.
        Assert.True(m.FinalPopulation > 0, "extinct dev world — battery vacuous");
        long totalBirths = 0;
        for (int i = 0; i < m.Births.Count; i++) totalBirths += m.Births[i];
        Assert.True(totalBirths > 0, "no births in 1000 turns — vitals dead");

        AssertMalthusKnownDeviation(c, m, seed);
        AssertDevMigrationQuarantine(c, seed, CalibrationAnalysis.MigrationGrossPerDecade(m));
    }

    /// <summary>
    /// dev.crashCount / firstCrashTurn / crashDepth / postCrashPopulation /
    /// starvationRatePer1000 — KNOWN DEVIATION, OPEN under docs/adr/cr-003.md.
    /// Same quarantine pattern the director accepted for the density corridor:
    /// the bands are HELD, the deviation is RECORDED with teeth in both
    /// directions, and the ruling is escalated rather than absorbed.
    ///
    /// WHAT CHANGED. T3.2b corrected two compensating errors: the yield
    /// constant was denominated per 256 km² lattice node (CR-002) and the
    /// catchment radius was ~205 km. Their PRODUCT put carrying capacity at
    /// ~1.08e5 — which the demographic clock happens to reach around turn 800,
    /// producing exactly the crash these bands were measured on. Correct either
    /// error alone and the coincidence breaks. Correct both and the dev world
    /// is PRE-MALTHUSIAN for its whole horizon: capacity is ~8x what 5,500
    /// years of growth at the fed rate can reach, so the land ceiling is never
    /// touched, nobody starves, and there is no cycle to band.
    ///
    /// MEASURED, this tree, 1000 turns, yield 26.0 / hinterland 50 km:
    ///   seed 42: final 79,847, peak 79,847 (monotone), starvation 0, crashes 0
    ///   seed  7: final 89,615, peak 89,615 (monotone), starvation 0, crashes 0
    /// Baseline on main for the same runs: seed 42 final 17,644 / peak 55,178 /
    /// starved 32,527 / 1 crash; seed 7 final 18,434 / peak 59,102 /
    /// starved 35,495 / 1 crash.
    ///
    /// NO AGRONOMICALLY DEFENSIBLE YIELD SATISFIES THESE BANDS. Measured across
    /// every derived value (12.94 / 15.0 / 16.04 / 26.0 / 37.5 / 152.7) the result is
    /// identical: zero starvation, zero crashes. They need <= 6.2 per
    /// fertility-weighted km², and realistically ~1.6 — an order of magnitude
    /// below the lowest reference-class derivation, and a value every deriving
    /// author explicitly disowned as a lower bound. The retired 28.0/node fails
    /// them too once the geometry is corrected (first crash at turn 134 against
    /// a [700, 1000] band). So this is not a tuning miss; it is a missing
    /// MECHANISM — nothing bounds a growing population below agronomic capacity,
    /// because it cannot clear land, cannot colonise, and cannot found daughter
    /// settlements. See cr-003 §3 option 3 and the expansion-opportunity entry
    /// in docs/queue.md.
    ///
    /// Delete this method and restore the five AssertInBand calls the moment
    /// the director rules.
    /// </summary>
    private static void AssertMalthusKnownDeviation(Corridors c, AutoplayMetrics m, ulong seed)
    {
        // 1. THE BANDS MAY NOT MOVE while the CR is open.
        foreach ((string key, double lo, double hi) in new[]
        {
            ("dev.crashCount", 1.0, 3.0),
            ("dev.postCrashPopulation", 4000.0, 20000.0),
            ("dev.starvationRatePer1000", 0.1, 1.5),
            ("dev.firstCrashTurn", 700.0, 1000.0),
        })
        {
            (double actualLo, double actualHi) = c.Band(key);
            Assert.True(actualLo == lo && actualHi == hi,
                $"{key}: the corridor band moved to [{actualLo}, {actualHi}] while CR-003 is OPEN — " +
                "the bands may not be re-tuned to absorb this deviation; take the CR to a ruling.");
        }

        // 2. THE WORLD IS PRE-MALTHUSIAN — exactly, not approximately. Any
        //    starvation at all means the land ceiling started binding inside
        //    the horizon, i.e. CR-003's premise changed and the ruling must be
        //    revisited. This is the tooth that fires when the mechanism lands.
        long starvedTotal = 0;
        for (int i = 0; i < m.StarvationDeaths.Count; i++) starvedTotal += m.StarvationDeaths[i];
        Assert.True(starvedTotal == 0,
            $"seed {seed}: {starvedTotal} starvation deaths — the dev world is no longer " +
            "pre-Malthusian. If a crash cycle is back, CR-003 is resolved: delete this " +
            "quarantine and restore the five AssertInBand calls.");

        var crashes = CalibrationAnalysis.Crashes(m, 0.20);
        Assert.True(crashes.Count == 0,
            $"seed {seed}: {crashes.Count} crash(es) appeared — see the message above; " +
            "CR-003 is resolved and this quarantine must be deleted.");

        // 3. AND THE TRAJECTORY IS THE RECORDED ONE. Monotone growth to a
        //    population inside the measured envelope: this still fails loudly
        //    on any NEW defect stacked on the open one (a collapse, a runaway,
        //    or a drift in the demographic clock), which is the whole point of
        //    quarantining rather than deleting.
        long peak = 0;
        for (int i = 0; i < m.Population.Count; i++) peak = Math.Max(peak, m.Population[i]);
        Assert.True(peak == m.FinalPopulation,
            $"seed {seed}: population peaked at {peak} and ended at {m.FinalPopulation} — " +
            "growth is no longer monotone, which contradicts the recorded pre-Malthusian regime.");
        Assert.InRange(m.FinalPopulation, 70_000, 100_000);
    }

    // --- the corridors file itself -------------------------------------------

    [Fact]
    public void Corridors_AllBandsTwoSided_AndOrdered()
    {
        // A one-sided or inverted band is a silent-vacuity hazard: every band
        // must be a real interval with lo < hi (two-sided teeth by data).
        // Interval sanity sweeps EVERY key in the FILE (adversarial pass: a
        // corridor added to corridors.json must not escape this check), and
        // the battery's assertion coverage is pinned to the exact expected
        // key set — adding a corridor without wiring a battery assert FAILS
        // here instead of silently going unenforced.
        Corridors c = Corridors.Load();
        foreach (string key in c.Keys)
        {
            (double lo, double hi) = c.Band(key);
            Assert.True(lo < hi, $"{key}: band [{lo}, {hi}] is not a real interval");
        }
        var expected = new[]
        {
            "canonical.fedGrowthPerYear", "canonical.crudeBirthRatePer1000",
            "canonical.crudeDeathRatePer1000", "canonical.pyramidChildShare",
            "canonical.pyramidAdultShare", "canonical.pyramidElderShare",
            "canonical.densityPerArableKm2", "canonical.migrationGrossPerDecade",
            "dev.crashCount", "dev.firstCrashTurn", "dev.crashDepth",
            "dev.postCrashPopulation", "dev.starvationRatePer1000",
            "dev.migrationGrossPerDecade",
        };
        var actual = new List<string>(c.Keys);
        actual.Sort(StringComparer.Ordinal);
        Array.Sort(expected, StringComparer.Ordinal);
        Assert.Equal(expected, actual);
    }
}
