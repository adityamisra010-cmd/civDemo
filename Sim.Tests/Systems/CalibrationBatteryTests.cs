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
            PipelineLoader.Load(pipeStream, SystemCatalog.All(cfg, TestConfigs.Worldgen())));
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
            PipelineLoader.Load(pipeStream, SystemCatalog.All(cfg, TestConfigs.DevWorldgen())));
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
    ///   UP-DRIFT (added T4.21-4, kept T4.21-7) — above the recorded envelope
    ///          but NOT into the band is neither the recorded deviation nor its
    ///          resolution, and gets its own tooth and its own words.
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
    ///
    /// T4.21-7 CORRECTION TO THE PARAGRAPH ABOVE, which is now false and is
    /// left standing only as history. It is `recorded / 0.75` that is nearer,
    /// by an order of magnitude: 1.1115925041234045E-04 and
    /// 1.360081711272263E-04 against `lo` = 0.001, computed from the two
    /// constants below. So an upward tooth CAN bite here, the T4.21-4 split
    /// gave it one, and T4.21-7 keeps it — see the ordering comment in
    /// AssertDevMigrationQuarantine and the regime pin in
    /// T4216_DevMigrationQuarantine_EachToothAnswersItsOwnRegime. The F6
    /// reasoning is not repudiated; the DISTANCES it was reasoning about moved,
    /// twice (T4.21-2 bounded migration, then T4.21-4's arming and T4.21-7's
    /// disarming), which is exactly what a self-verifying envelope is for.
    /// </summary>
    // T4.1b RE-PIN (ADR-018, ONE ruled cause): minSpacingKm 480 -> 95.2.
    // DECOMPOSED before re-pinning (§7.15): the metric is gross / person-years,
    // and seed 42 moved on the NUMERATOR — gross 8,940 -> 6,632 (x0.742) while
    // person-years moved x0.9995. Not the CR-002 denominator family.
    // NOT ABSORBED SILENTLY: the pre-existing drift filed in queue.md (main
    // measured 0.000887533 against the OLD recorded 0.000931705, x0.953, cause
    // unmeasured) is STILL OPEN and is a second cause this pin now also
    // carries. Owner: T3.10's migrated corridor work in M4.
    // T4.2 RE-PIN (VALUE, ONE ruled cause): grainSpoilagePerYear = 0.08 and
    // granaryYearsOfDemand = 1.5 move the dev world's migration signature —
    // itemized in the T4.2 fallout table alongside the other four VALUE pins.
    // T4.21-4 RE-PIN, THEN T4.21-7 RE-PIN BACK — the honest history of this
    // envelope, because both halves were measured and only one of them ships.
    // CR-015 N7's "recorded envelope", never a tolerance widen: the 0.75 factor
    // below is UNCHANGED throughout, and no band moves (RULE A).
    //
    //   ARMED (T4.21-4, λ = 0.01, measured by that agent, dev preset, 1000
    //   turns): seed 42 0.0148409518, seed 7 0.0158496291 — ×205.6 and ×19.8
    //   on the then-recorded 7.21744E-05 / 0.000799951, and 1.5× ABOVE the
    //   corridor CEILING 0.01. Two causes, split by that agent: bounded flight
    //   + basin caps + vacancy bound (T4.21-2, ADR-025) put the λ = 0 twin at
    //   8.33694378E-05 / 0.000102006128, and the arming was the other half and
    //   dominated (docs/t4.21-4-record.md §2.7, §4.2).
    //
    //   DISARMED (T4.21-7, λ = 0 — THE SHIPPING VALUE under CR-016's
    //   orchestrator decision; RE-MEASURED ON THIS TREE by the agent writing
    //   this line, same rig, dev preset, 1000 turns):
    //       seed 42  0.0148409518   ->  8.336943780925534E-05
    //       seed  7  0.0158496291   ->  1.0200612834541973E-04
    //   which is the T4.21-2 bounded-migration level and nothing else — the
    //   arming's whole contribution is gone with the arming. These are the
    //   values pinned below, so the envelope is again what the shipped world
    //   measures. The direction is back to the T3.4c one: BELOW the floor
    //   0.001, not above the ceiling.
    //
    // The seed-7 envelope carries the ONE resolved inherited red: on 8f7f9da
    // seed 7 was the suite's single failure, 0.000102006 against the stale
    // recorded 0.000799951 × 0.75 (the T4.21-2 move, previously masked by the
    // Malthus starvation tooth). Re-pinning to the measured value resolves it
    // deliberately, like a golden, with the cause named. The RATE that would
    // move these numbers again is the director's — docs/adr/cr-016-armed-
    // disaster-fallout.md — and re-arming re-pins this envelope a third time.
    private const double QuarantineRecordedSeed42 = 8.336943780925534E-05;
    private const double QuarantineRecordedSeed7 = 1.0200612834541973E-04;
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

        // ORDER MATTERS, AND IT IS THE WHOLE POINT OF THIS BLOCK — and the
        // reason survives the T4.21-7 re-pin unchanged, only with the drift
        // tooth that would swallow the resolution case swapped for the other
        // one. The recorded envelope and the corridor band DO NOT OVERLAP:
        // recorded / 0.75 is 1.1115925041234045E-04 (seed 42) and
        // 1.360081711272263E-04 (seed 7), both strictly BELOW the floor
        // lo = 0.001 — computed by the agent writing this line from the two
        // constants above. So every value inside [lo, hi] is also ABOVE the
        // upward drift bar. If the drift teeth were evaluated first they would
        // consume the resolution case and print "rose above … a ruled change"
        // for the one outcome this quarantine exists to detect, leaving the
        // resolution tooth with an empty failure set — a dead tooth plus a
        // false diagnosis, the exact T3.4b defect. The band read comes FIRST;
        // the envelope teeth judge only the below-floor regime they were
        // pinned in. The band is still read and never written, and 0.75 is
        // unchanged. (T4.21-4 shipped this ordering for the mirror-image
        // reason — recorded × 0.75 then sat ABOVE the ceiling and the DOWNWARD
        // tooth was the swallowing one; T4.21-6 finding 1 made it reachable.
        // The ordering correction is independent of the arming and is kept.)
        //
        // THE UPWARD TOOTH IS SPLIT FROM RESOLUTION, and stays split. The
        // T3.4c version read `value < lo` and called anything else "back
        // INSIDE the corridor … RESOLVED" — a disjunction that is only sound
        // if the sole way up is into the band. It is not: with the envelope at
        // ~1E-04 and the floor at 1E-03 there is an order of magnitude of
        // upward room that is drift, not resolution, and T4.21-4 measured a
        // world (armed) that went clean past the ceiling. Splitting keeps the
        // failure message TRUE in every regime, which is what this helper
        // exists to have fixed.
        //
        // NO BAND MOVES (RULE A / CR-015 N7): `lo` and `hi` are read, never
        // written, and the immovability assert above still guards both. Teeth
        // added, none removed, and the 0.75 factor unchanged.
        Assert.False(value >= lo && value <= hi,
            $"seed {seed}: {Inv(value)} is back INSIDE the corridor [{lo}, {hi}] — the B-1b dev-preset " +
            "deviation is RESOLVED for this seed. Re-measure the dev seed set; if the distribution has " +
            "returned, delete AssertDevMigrationQuarantine and restore the plain AssertInBand.");

        Assert.True(value >= recorded * QuarantineDriftTolerance,
            $"seed {seed}: {Inv(value)} fell below {QuarantineDriftTolerance:F2}× its recorded value " +
            $"{Inv(recorded)} — the dev world has degraded beyond the quarantined deviation " +
            "(measured disablement signature ×0.536; largest legitimate correction ×0.836). " +
            "A NEW defect, or a ruled substrate change that must re-pin this envelope deliberately.");

        Assert.True(value <= recorded / QuarantineDriftTolerance,
            $"seed {seed}: {Inv(value)} rose above {1 / QuarantineDriftTolerance:F2}× its recorded value " +
            $"{Inv(recorded)} — the dev world's migration has drifted UPWARD inside the quarantined " +
            $"below-floor regime without reaching the corridor floor {lo}, so this is neither the " +
            "recorded deviation nor its resolution. A NEW defect, or a ruled change that must re-pin " +
            "this envelope deliberately, like a golden.");

        string where = value < lo ? $"{1 - value / lo:P1} below the floor {lo}"
            : value > hi ? $"{value / hi - 1:P1} ABOVE the ceiling {hi}"
            : "inside the band";
        Console.WriteLine(
            $"T3.4c CORRIDOR-WIDE QUARANTINE (dev preset, B-1b), T4.21-7 re-pin: {QuarantinedKey} " +
            $"seed {seed} = {Inv(value)}, {where}. This corridor records a dev world 20-60% BELOW its " +
            "floor (T3.4c: 19 of 20 seeds below, median -30.5%), and the shipped world is still that " +
            "world: measured at the SHIPPING hazardPerYear = 0.0, seeds 42 / 7 read 8.34E-05 / " +
            "1.02E-04. T4.21-4 measured the SAME two seeds at 0.01484 / 0.01585 — 1.5x above the " +
            "CEILING — with the famine-class disaster ARMED at hazardPerYear = 0.01; that value does " +
            "NOT ship, the mechanism ships inert, and the RATE is the director's ruling. Band NOT " +
            "moved. docs/adr/cr-016-armed-disaster-fallout.md, docs/t4.21-4-record.md §5.");
    }

    /// <summary>
    /// T4.21-6 — THE QUARANTINE'S OWN TEETH, EXERCISED, RE-AIMED BY T4.21-7 AT
    /// THE DISARMED REGIME. The helper above is reached on a real run only
    /// through two 1000-turn dev runs, so its teeth were never exercised by
    /// the ordering that shipped them; that is how the T4.21-4 split shipped
    /// with its resolution tooth UNREACHABLE (the downward bar then sat ABOVE
    /// the corridor ceiling and swallowed every in-band value). This drives the
    /// helper DIRECTLY with synthetic readings, which costs no world run, and
    /// pins WHICH tooth answers WHICH regime.
    ///
    /// WHAT T4.21-7 CHANGED, and what it did not. The ORDERING is unchanged and
    /// so is its reason — the envelope and the band must not overlap, and the
    /// band read must come first or a drift tooth eats the resolution case.
    /// What flipped is WHICH drift tooth would eat it: with the envelope
    /// re-pinned to the SHIPPED λ = 0 world (8.34E-05 / 1.02E-04, both an order
    /// of magnitude BELOW the floor 0.001, CR-016) the bracket sits below the
    /// band, so an in-band value is above the UPWARD bar, not below the
    /// downward one. The probes move with it: "below the envelope" is now
    /// recorded × 0.5 rather than lo × 0.5, because lo × 0.5 is five times the
    /// whole envelope.
    ///
    /// It also pins the non-overlap itself, because that is the fact the
    /// ordering depends on: if a future re-pin (re-arming the disaster is the
    /// obvious one) brings the envelope back across the floor, the first assert
    /// below fails and whoever re-pins is told to re-read the ordering comment
    /// rather than inheriting it silently.
    /// No band is read from anywhere but Corridors.Load(), and nothing is written.
    /// </summary>
    [Fact]
    public void T4216_DevMigrationQuarantine_EachToothAnswersItsOwnRegime()
    {
        Corridors c = Corridors.Load();
        (double lo, double hi) = c.Band(QuarantinedKey);

        // The premise of the ordering, stated as an assertion: the envelope
        // bracket [recorded × 0.75, recorded / 0.75] and the band [lo, hi] are
        // DISJOINT. Today the envelope lies wholly below the floor.
        Assert.True(QuarantineRecordedSeed42 / QuarantineDriftTolerance < lo
                 && QuarantineRecordedSeed7 / QuarantineDriftTolerance < lo,
            "the recorded envelope has risen into (or across) the corridor band — the envelope " +
            "bracket and the band now OVERLAP, so re-read the ordering comment in " +
            "AssertDevMigrationQuarantine before re-pinning: the band-first ordering was chosen " +
            "precisely because they did not.");

        foreach (ulong seed in new ulong[] { 42ul, 7ul })
        {
            double recorded = seed == 42ul ? QuarantineRecordedSeed42 : QuarantineRecordedSeed7;

            // 1. INSIDE the band — RESOLUTION. This is the case a drift tooth
            //    would consume if the order were wrong; it must name resolution
            //    and nothing else.
            double inBand = 0.5 * (lo + hi);
            Assert.True(inBand > recorded / QuarantineDriftTolerance,
                "the in-band probe must also be above the upward drift bar, or this case proves nothing");
            Exception? resolved = Record.Exception(() => AssertDevMigrationQuarantine(c, seed, inBand));
            Assert.NotNull(resolved);
            Assert.Contains("RESOLVED", resolved!.Message, StringComparison.Ordinal);
            Assert.DoesNotContain("rose above", resolved.Message, StringComparison.Ordinal);
            Assert.DoesNotContain("degraded beyond", resolved.Message, StringComparison.Ordinal);

            // 2. BELOW the envelope — a degradation, not a resolution. (lo × 0.5
            //    no longer probes this regime: it is five times the envelope.)
            Exception? degraded = Record.Exception(
                () => AssertDevMigrationQuarantine(c, seed, recorded * 0.5));
            Assert.NotNull(degraded);
            Assert.Contains("degraded beyond", degraded!.Message, StringComparison.Ordinal);
            Assert.DoesNotContain("RESOLVED", degraded.Message, StringComparison.Ordinal);

            // 3. THE RECORDED REGIME ITSELF — below the floor, inside the
            //    envelope: silence. Without this the helper could pass by
            //    failing everything.
            Assert.Null(Record.Exception(() => AssertDevMigrationQuarantine(c, seed, recorded)));

            // 4. ABOVE the envelope but STILL BELOW the floor — the upward
            //    tooth, in its own words, in the gap that is drift and not
            //    resolution. The order-of-magnitude gap between recorded / 0.75
            //    and lo is exactly what makes this tooth worth having.
            double up = recorded / QuarantineDriftTolerance * 1.01;
            Assert.True(up < lo, "the upward probe must stay below the floor, or it probes resolution");
            Exception? drifted = Record.Exception(() => AssertDevMigrationQuarantine(c, seed, up));
            Assert.NotNull(drifted);
            Assert.Contains("rose above", drifted!.Message, StringComparison.Ordinal);
            Assert.DoesNotContain("RESOLVED", drifted.Message, StringComparison.Ordinal);
        }
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

        // M4 completion (director ruling): THE FED-DENSITY QUARANTINE IS LIFTED.
        // This is now the plain corridor assertion every other metric gets.
        //
        // The evidence, and it is the quarantine's OWN stated lift condition: a
        // 20-seed / 650-turn sweep at e6cf705 measured densityPerArableKm2 IN
        // BAND on 20 of 20 seeds (min 0.2808, mean 0.4129, max 0.5648, corridor
        // [0.15, 0.6]) — met at the >=20-seed standard m4-spec §6 requires,
        // rather than on the two seeds this battery happens to run.
        //
        // The band was NOT re-tuned to absorb anything: it is untouched at
        // [0.15, 0.6] and the world came back to it. The cause of the return was
        // T4.7's river-aware traversal enlarging catchments (mean effective
        // arable 90,886 -> 273,561 km2 at flat population), measured by
        // single-variable control in docs/t4.10-review-record.md.
        //
        // Fed-density health is NOT Malthusian crash emergence, and the ruling
        // keeps them apart: the dev Malthus corridors below stay quarantined.
        //
        // m4-density-review-state — RECLASSIFIED C -> B, NOT FIXED. After the
        // lift above, T4.19 lane C corrected the founding demographic vector and
        // docs/t4.19c-remeasurement.md re-measures 20 seeds: 14/20 in band,
        // min 0.36857, mean 0.54063, max 0.74211 (window pinned at full measured
        // precision 0.3685744951368359 / 0.7421101248166698), six over the ceiling.
        // arableKm2 is BIT-IDENTICAL between arms and the per-seed density ratio
        // EQUALS the population ratio, so the move is 100% NUMERATOR - a level
        // shift with a named, ruled cause. That is an OBSERVED/REVIEW state, not
        // an invariant failure, so the quarantine is re-activated in
        // corridors.json and asserted here against its WINDOW with teeth in both
        // directions. THE BAND IS NOT MOVED and the lift record is not deleted.
        AssertCanonicalDensityQuarantine(c, seed,
            CalibrationAnalysis.DensityPerArableKm2(m));
        // MIGRATION IS NO LONGER AN ACCEPTANCE GATE (director ruling, M4
        // completion §5/§22). It is REPORTED against its corridor, not gated on.
        //
        // Measured at e6cf705, 20 seeds / 650 turns: IN BAND on 1 of 20
        // (min 0.00029, mean 0.00057, max 0.00103) against [0.001, 0.01]. The
        // record had ONE seed 2% under the floor; nineteen are now under it, the
        // worst by 3.4x — a change in kind, not the deviation on file.
        //
        // The recorded cause is REFUTED rather than merely unconfirmed. The
        // standing explanation was T4.2's granary cap, on a pre-T4.10 measurement
        // of a 5.56x recovery when lifted; re-run at e6cf705 as a single-variable
        // arm (cap 1.5 -> 1e6, nothing else, 20 seeds) migration got WORSE:
        // 1/20 -> 0/20 in band, mean 0.00057 -> 0.00036. The cause is unknown.
        //
        // The ruling is to ACCEPT current migration behaviour and NOT tune to
        // this corridor. So the band is NOT re-cut to fit — a band redrawn around
        // whatever the world happens to do measures nothing — and the assertion
        // is removed rather than widened. What stays is the LIVENESS tooth: the
        // metric must still be produced and still be positive, so a migration
        // system that silently stopped moving anyone would still fail loudly.
        // That is the part of this corridor that still tests something.
        double migration = CalibrationAnalysis.MigrationGrossPerDecade(m);
        Assert.False(double.IsNaN(migration),
            "canonical.migrationGrossPerDecade: metric produced NO OUTPUT — battery failure");
        Assert.True(migration > 0.0,
            $"canonical.migrationGrossPerDecade: {Inv(migration)} — migration has stopped "
            + "entirely, which is a NEW defect and not the accepted low-volume regime.");
    }


    /// <summary>
    /// m4-density-review-state — THE CANONICAL FED-DENSITY OBSERVATION, quarantined
    /// with teeth in BOTH directions, read from corridors.json so the nightly and
    /// this battery cannot disagree (T3.12's whole point).
    ///
    /// THIS IS A RECLASSIFICATION, NOT A FIX. No simulation behaviour changed: the
    /// measurement is what it was. What changed is that the project now classifies
    /// it as an accepted, owned, loudly-reported observation (state B) instead of
    /// letting it read as an invariant failure (state C). It does NOT reverse the
    /// 2026-09-04 lift, which was correct on its evidence; this is a SECOND,
    /// sequential deviation with a NEW attributed cause.
    ///
    /// NOT A SKIP. Three teeth:
    ///   BAND IMMOVABILITY - the target band may not move while the quarantine
    ///     stands. Widening the band to swallow the deviation is the exact move
    ///     the director forbade, and the window is LITERAL data, never a fraction
    ///     of the band, so it cannot be widened by widening the band.
    ///   DOWN - below the measured window is a NEW defect, not more of the same.
    ///   UP   - above the measured window is a NEW defect. Unlike dev migration,
    ///     upward motion here is NOT resolution: resolution means coming back
    ///     INSIDE [0.15, 0.6], which is below this window, so the downward tooth
    ///     and the lift condition carry that case and no tooth is dead.
    /// </summary>
    private static void AssertCanonicalDensityQuarantine(Corridors c, ulong seed, double value)
    {
        const string key = "canonical.densityPerArableKm2";
        Assert.False(double.IsNaN(value), $"{key}: metric produced NO OUTPUT — battery failure");

        (double lo, double hi) = c.Band(key);
        Assert.True(lo == 0.15 && hi == 0.6,
            $"{key}: band moved to [{lo}, {hi}] while the m4-density-review-state quarantine " +
            "stands. The band is the TARGET and was ruled NOT re-tuned — take it to a ruling, " +
            "do not widen it to absorb the observation.");

        (double Lo, double Hi)? q = Corridors.Quarantine("canonical", "densityPerArableKm2");
        Assert.True(q is not null,
            $"{key}: the quarantine is inactive, so this corridor must gate against the band " +
            "again — restore the plain AssertInBand call at the call site.");
        (double wlo, double whi) = q!.Value;

        Assert.True(value >= wlo,
            $"seed {seed}: {Inv(value)} is BELOW the measured quarantine window [{wlo}, {whi}] — " +
            "a NEW defect, not the recorded founding-correction level shift. (Returning INSIDE " +
            $"the band [{lo}, {hi}] would also land here: if that is what happened, re-measure " +
            "the 20-seed sweep and lift the quarantine per its stated lift condition.)");
        Assert.True(value <= whi,
            $"seed {seed}: {Inv(value)} is ABOVE the measured quarantine window [{wlo}, {whi}] — " +
            "the observation has drifted beyond the recorded envelope, which is a NEW defect or a " +
            "ruled substrate change that must re-pin this window deliberately.");

        Console.WriteLine(
            $"m4 OBSERVED/REVIEW (canonical fed density): {key} seed {seed} = {Inv(value)}, " +
            $"window [{wlo}, {whi}], band [{lo}, {hi}] UNCHANGED. 14/20 seeds in band; six over " +
            "the ceiling (20, 8, 6, 1, 13, 2). 100% numerator, arable bit-identical between arms, " +
            "attributed to the T4.19 lane C founding demographic correction. Owner: the M4 exit " +
            "packet owning the founding vector. NOT a fix — a reclassification.");
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
            "pre-Malthusian in the strict sense this tooth asserts. DO NOT read this as " +
            "permission to delete the quarantine: cr-003.md §7.5 (written AFTER T4.7, on 20-seed " +
            "evidence) rules that the measured world moved AWAY from Malthusian constraint — more " +
            "land per head, fewer deficits, crashCount 0/20 — and is therefore evidence AGAINST " +
            "lifting. §7.6 anticipates this exact message and records it as NOT ACTIONED. " +
            "Isolated starvation is not the crash cycle CR-003 lifts on. The bands stay, the " +
            "quarantine stays, and only a director ruling on CR-003 changes either.");

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
        // Envelope RE-MEASURED at T3.6b (ADR-017: endowmentJitter 0.25 → 0.69).
        // The move is INITIAL-CONDITION, decomposed before this was touched:
        // seed 7's founding total rose 1,742 → 2,018 (+15.8%) and its final
        // 89,615 → 106,150 (+18.5%) — the start compounding through the
        // pre-Malthusian exponential, no demographic rate change (seed 42:
        // founding −0.9%, final 79,847 → 77,654). New measured finals
        // 77,654 / 106,150; envelope [68k, 119k] keeps ~12% margins on both
        // sides. corridors.json bands are UNTOUCHED — this is the recorded-
        // trajectory pin, re-pinned like a golden with this history line.
        // (v1, T3.2b/CR-003: [70k, 100k] on measured 79,847 / 89,615.)
        // Envelope RE-MEASURED at T4.21-3 (CR-015 / ADR-026). The tooth in (2)
        // no longer fires: on the integration tree (1735d41) both seeds carried
        // 3 STRESS-sized weather starvation deaths (the CR-003 §7.6 "NOT
        // ACTIONED" message), and under the effective deficit a shortfall
        // inside the absorbable band starves nobody — starvation is EXACTLY 0
        // on both seeds, crashes 0, growth monotone: the pre-Malthusian regime
        // this quarantine asserts, restored (CR-015 N7's pre-committed reading
        // for dev.starvationRatePer1000). THE T3.6b FINALS WERE STALE: that
        // tooth had masked this assertion since, and measured on the
        // integration HEAD with the tooth bypassed the finals were ALREADY
        // 100,544 (seed 42) / 139,154 (seed 7) — outside [68k, 119k]. This
        // packet LOWERS them, to 93,965 / 122,532 (the turn-2 headroom hold
        // every founded world takes, then chaotic divergence — SnapshotTests
        // .FoundedGolden has the record); founding totals unchanged (1,581 /
        // 2,058). Envelope [82k, 138k] keeps ~12 % margins on both sides of
        // the values measured on THIS tree. corridors.json bands are UNTOUCHED.
        // T4.21-4 owns the CR-003 message re-read; this is the recorded-
        // trajectory pin, re-pinned like a golden with this history line.
        // KNOWN, NOT MINE: seed 7 then reaches AssertDevMigrationQuarantine and
        // fails it — dev.migrationGrossPerDecade vs the recorded 0.000800 ×
        // 0.75. That drift PRE-DATES both T4.21 packets: measured on the
        // integration HEAD (1735d41) with the starvation tooth bypassed, seed 7
        // read 6.80E-05 (seed 42 6.42E-05), already below the tooth and masked
        // by it. The recorded envelope's re-pin is T4.21-4's deliverable (spec
        // §4, N7) and is left for it.
        // T4.21-2 ∥ T4.21-3 MERGE-FINISH LANE — COMMENT ONLY. No band, no
        // tooth, no envelope and no recorded value is changed here; the two
        // figures above (93,965 / 122,532) were measured on the T4.21-3 branch
        // with bounded migration ABSENT. RE-MEASURED ON THE MERGED TREE by the
        // agent writing this line, 1000 dev turns each:
        //   seed 42: founding 1,581, final 93,910, peak 93,910 (monotone),
        //            starvation 0, crashes 0, migrationGrossPerDecade
        //            8.336943780925534E-05 — above its recorded 7.21744E-05 ×
        //            0.75, so seed 42 passes the drift tooth and is GREEN.
        //   seed  7: founding 2,058, final 123,600, peak 123,600 (monotone),
        //            starvation 0, crashes 0, migrationGrossPerDecade
        //            1.0200612834541973E-04 — 0.128× its recorded 0.000799951,
        //            so the drift tooth fires and seed 7 is RED, for the
        //            PRE-EXISTING, previously masked cause above.
        // The envelope [82k, 138k] asserted below HOLDS on both seeds as
        // measured here, so it is not re-pinned.
        Assert.InRange(m.FinalPopulation, 82_000, 138_000);
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
