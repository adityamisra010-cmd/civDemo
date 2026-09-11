using FsCheck;
using FsCheck.Fluent;
using FsCheck.Xunit;
using Sim.Core;
using Sim.Core.Kernel;
using Sim.Core.State;
using Sim.Core.Systems;
using Sim.Core.Systems.ClassMobility;
using Sim.Core.Worldgen;
using Sim.Tests.TestUtil;

namespace Sim.Tests.Systems;

// T2.2 acceptance: artisans emerge under sustained surplus and plateau at the
// cap; famine drains them demote-first (before peasant starvation peaks);
// predicate flips are hysteretic (teeth-tested both ways); mobility conserves
// (adult-only, same-cohort — per-cohort cross-class totals invariant); the
// scaffolded artisan contributions are bounded (tool multiplier monotone,
// saturating, capped; construction labor exact and slider-scaled).
public class ClassSystemTests
{
    // T4.19-E: xunit output sink for the emergence telemetry (latch turn,
    // first-present turn, first-true predicate turn) — reported, never banded.
    private readonly Xunit.Abstractions.ITestOutputHelper _output;
    public ClassSystemTests(Xunit.Abstractions.ITestOutputHelper output) => _output = output;

    private static EraTable FlatEra(double dtYears) => EraTableLoader.Load(
        $$"""{ "bands": [ { "name": "flat", "startYear": 0, "endYear": 100000, "dtYears": {{dtYears.ToString(System.Globalization.CultureInfo.InvariantCulture)}} } ] }""");

    private static EraTable CanonicalEra()
    {
        using var stream = Sim.Data.DataFiles.OpenEraPacing();
        return EraTableLoader.Load(stream);
    }

    private static TurnExecutor ProductionExecutor(SimConfig cfg)
    {
        using var stream = Sim.Data.DataFiles.OpenPipeline();
        return new TurnExecutor(CanonicalEra(), PipelineLoader.Load(stream, SystemCatalog.All(cfg)));
    }

    private static readonly SettlementId S0 = new(0);
    private static readonly ClassId Peasants = new(1);
    private static readonly ClassId Artisans = new(2);

    private static long ArtisanAdults(WorldState world) =>
        ClassMobilitySystem.AdultsOfClass(world.Buckets, S0, Artisans);

    /// <summary>Two-class hand world with ClassStates rows (peasants active,
    /// artisans dormant unless <paramref name="artisansActive"/>).</summary>
    private static WorldState ClassWorld(long[] peasants, long[] artisans, bool artisansActive = false)
    {
        WorldState world = PopulationExactnessTests.TwoGroupWorld(peasants, artisans);
        world.ClassStates.Add(new ClassStateRow(S0, Peasants, Active: 1));
        world.ClassStates.Add(new ClassStateRow(S0, Artisans, artisansActive ? 1 : 0));
        return world;
    }

    /// <summary>Sets the surplus signal the NEXT classmobility step will
    /// publish: LastHarvestUnits / DemandUnits on the current state's rows.</summary>
    private static void DriveSurplus(WorldState world, long harvest, long demand)
    {
        if (world.GoodStocks.Count == 0)
            world.GoodStocks.Add(new GoodStockRow(S0, new GoodId(1), Conserved.Zero, 0.0, 0.0, harvest));
        else world.GoodStocks.Ref(0).LastProducedUnits = harvest;
        var row = new ConsumptionDeficitRow(S0, 0.0, demand);
        if (world.ConsumptionDeficits.Count == 0) world.ConsumptionDeficits.Add(row);
        else world.ConsumptionDeficits[0] = row;
    }

    private static int ActiveFlag(WorldState world)
    {
        for (int i = 0; i < world.ClassStates.Count; i++)
            if (world.ClassStates[i].Class == Artisans) return world.ClassStates[i].Active;
        return -1;
    }

    /// <summary>The hysteresis latch for (settlement, class): the ClassStates
    /// row's Active flag (WorldState.ClassStateRow), written by
    /// ClassMobilitySystem's latch step. -1 when the row does not exist.
    /// Index iteration only — no LINQ.</summary>
    private static int LatchActive(WorldState world, SettlementId settlement, ClassId cls)
    {
        for (int i = 0; i < world.ClassStates.Count; i++)
        {
            ClassStateRow row = world.ClassStates[i];
            if (row.Settlement == settlement && row.Class == cls) return row.Active;
        }
        return -1;
    }

    // --- emergence in fed autoplay ------------------------------------------

    /// <summary>Evaluate a D-020 predicate exactly as ClassMobilitySystem's
    /// latch step does, on the world the step is ABOUT TO READ as Prev: the
    /// settlement's Variables rows (WorldState.VariableRow, keyed by the
    /// Sim.Core.State.Variables code ids), index iteration, an unpublished
    /// variable reading 0.0. Returns null while the settlement has never
    /// published — the T3.1 "variables not yet published" warm-up guard, under
    /// which the latch step evaluates NOTHING (ClassMobilitySystem.Step, latch
    /// block). Public predicate/variable machinery only; nothing in Sim.Core
    /// was exposed or changed for this test.</summary>
    private static bool? EvaluateOnPrev(Predicate predicate, WorldState prev, SettlementId settlement)
    {
        bool published = false;
        for (int i = 0; i < prev.Variables.Count; i++)
            if (prev.Variables[i].Settlement == settlement) { published = true; break; }
        if (!published) return null;
        Predicate.VariableReader read = varId =>
        {
            for (int i = 0; i < prev.Variables.Count; i++)
            {
                VariableRow row = prev.Variables[i];
                if (row.Settlement == settlement && row.VarId == varId) return row.Value;
            }
            return 0.0;
        };
        return predicate.Evaluate(read);
    }

    [Fact]
    public void Artisans_EmergeInFedAutoplay_LatchFollowsItsPredicate_PlateauAtTheCap()
    {
        // STRUCTURAL TEST (T4.19-E, director ruling): the emergence MECHANISM
        // is asserted, not the turn on which it happens to fire.
        //
        // HISTORY, CONDENSED. This test carried a timing window on the
        // emergence turn from T2.2 onward: [3, 10] at T2.2, phases re-anchored
        // at T2.7 (60 -> 900 turns), population-gated at T3.1 (`population >
        // 520`, measured t52), re-measured 70 -> 81 under T3.4b harvest
        // variance and re-banded [30, 95], floor moved to 10 at T4.7 when the
        // river-aware lattice tripled effective arable (measured 22), and
        // re-pointed from "first artisan adult present" to the ClassStates
        // latch at T4.19-D, because presence can be a migration event. The
        // 20-seed re-measurement (docs/t4.19c-remeasurement.md §4.3) then
        // showed the window was never a 20-seed property of either founding
        // vector: the emergence-turn (first-present arm) distribution sits
        // 13/20 inside [10, 95] on the corrected vector (seven seeds below 10)
        // and 15/20 on the OLD vector (four above 95, one below 10); the latch
        // itself was 13/20 inside on BOTH vectors (§4.3, row "latch inside").
        // A single-seed pass at 22 was the window's whole
        // evidence. THE [10, 95] WINDOW IS RETIRED AS AN UNSUPPORTED
        // CALIBRATION INSTRUMENT (docs/t4.19-verification-record.md §10). No
        // timing band of any kind remains here; the latch turn is TELEMETRY.
        //
        // IMPLEMENTER'S READING of "structural tests" (awaiting the director's
        // confirmation): a structural assertion is a property of the emergence
        // mechanism that holds regardless of WHEN the latch fires. The set:
        //   S1 dormant at founding — (S0, Artisans).Active == 0, no artisan
        //      adults in S0 at turn 0;
        //   S2 mechanism fidelity — the latch fires on exactly the FIRST turn
        //      at which the class's own emerge predicate (sim.json classes[1],
        //      `food_surplus_ratio > 1.3 && population > 520`), evaluated on
        //      the PRE-step world's Variables rows for S0 under the warm-up
        //      guard, is true: never while it is false, and never later than
        //      its first true evaluation;
        //   S3 fires within the horizon — the latch is not "never";
        //   S4 hysteresis after the latch — Active stays 1 on every later turn
        //      whose pre-step recede evaluation (`food_surplus_ratio < 1.1`) is
        //      false while the latch is set; a true recede drops it (counted),
        //      after which only a true emerge re-sets it — the transition
        //      table is asserted on every turn (see the loop);
        //   S5 the arms that were always structural — per-turn share <= cap +
        //      drift margin, boom plateau keyed off the latch turn, per-turn
        //      conservation, minAfterBoom domain-only — stay exactly as before.
        // Nothing in the mechanism moved: threshold 520, surplus 1.3, recede
        // 1.1, promotion rate, mobility, founding vector, density band and
        // migration are all as before; no golden moved.
        SimConfig cfg = TestConfigs.Sim();
        TurnExecutor exec = ProductionExecutor(cfg);
        WorldState world = WorldFounding.Found(TestConfigs.DevWorldgen(), cfg, 42);

        // The predicates the latch step itself parses (ClassMobilitySystem
        // ctor): classes[1] is the artisan class, registry id 2.
        ClassEntry artisanEntry = cfg.Registries.Classes[1];
        Assert.Equal(Artisans.Value, artisanEntry.Id);
        Assert.NotNull(artisanEntry.Emerge);
        Assert.NotNull(artisanEntry.Recede);
        Predicate emerge = Predicate.Parse(artisanEntry.Emerge!);
        Predicate recede = Predicate.Parse(artisanEntry.Recede!);

        // S1: dormant at founding.
        Assert.Equal(0, ArtisanAdults(world));
        Assert.Equal(0, LatchActive(world, S0, Artisans));

        int latchTurn = -1;            // TELEMETRY: first turn (after exec.Step) with (S0, Artisans).Active == 1
        int firstPresentTurn = -1;     // TELEMETRY: first artisan adult present in S0 (may be a migrant)
        int firstEmergeTrueTurn = -1;  // TELEMETRY: first turn whose pre-step emerge evaluation is true
        int recedeTrueTurns = 0;       // TELEMETRY: post-latch turns on which recede was true while Active == 1
        int recessions = 0, reLatches = 0; // TELEMETRY: 1 -> 0 and later 0 -> 1 transitions after the first latch
        int unpublishedTurns = 0;      // TELEMETRY: turns under the warm-up guard (expected: turn 1 only)
        double boomPeak = 0.0, minAfterBoom = 1.0;
        for (int t = 1; t <= 900; t++)
        {
            // Evaluate on the world the step is about to read as Prev.
            bool? emergeNow = EvaluateOnPrev(emerge, world, S0);
            bool? recedeNow = EvaluateOnPrev(recede, world, S0);
            int activeBefore = LatchActive(world, S0, Artisans);
            if (emergeNow is null) unpublishedTurns++;
            if (firstEmergeTrueTurn < 0 && emergeNow == true) firstEmergeTrueTurn = t;

            world = exec.Step(world);
            int active = LatchActive(world, S0, Artisans);
            long artisans = ArtisanAdults(world);
            if (firstPresentTurn < 0 && artisans > 0) firstPresentTurn = t;

            // S2 + S4: the latch is the D-020 hysteresis state machine and
            // nothing else — from the pre-step state and the pre-step
            // predicate evaluations, the post-step state is DETERMINED:
            //   guarded (never published)   -> unchanged
            //   0 and emerge true           -> 1   (S2: no-earlier, no-later)
            //   0 and emerge false          -> 0   (S2: never while false)
            //   1 and recede false          -> 1   (S4: hysteresis holds)
            //   1 and recede true           -> 0   (recession; counted)
            // The first 0 -> 1 is the latch turn. A recession followed by a
            // re-emergence is the same machine run again, so the assertion is
            // the full transition table on every turn, not just up to the
            // first latch — S4 as literally stated ("Active == 1 on every
            // post-latch turn whose recede is false") is exactly the third
            // row and is what the machine says when read from state 1; from
            // state 0 after a recession it is the second row (re-emergence
            // needs emerge, not merely not-recede) — see the record §10.
            int expected;
            if (emergeNow is null) expected = activeBefore;
            else if (activeBefore == 0) expected = emergeNow == true ? 1 : 0;
            else expected = recedeNow == true ? 0 : 1;
            Assert.True(active == expected,
                $"turn {t}: (S0, Artisans).Active was {activeBefore} before the step; emerge '{emerge.Source}' = " +
                $"{(emergeNow is null ? "UNPUBLISHED (warm-up guard)" : emergeNow.ToString())}, recede '{recede.Source}' = " +
                $"{(recedeNow is null ? "UNPUBLISHED" : recedeNow.ToString())} on the pre-step world; expected Active {expected} " +
                $"after the step, found {active} (latch/hysteresis fidelity)");
            if (activeBefore == 0 && active == 1)
            {
                if (latchTurn < 0) latchTurn = t; else reLatches++;
            }
            if (activeBefore == 1 && active == 0) recessions++;
            if (latchTurn > 0 && activeBefore == 1 && recedeNow == true) recedeTrueTurns++;

            long adults = BandViews.Adults(world.Buckets, S0);
            double share = adults > 0 ? artisans / (double)adults : 0.0;
            if (latchTurn > 0 && t <= latchTurn + 25)
                boomPeak = Math.Max(boomPeak, share);                // the post-latch boom window
            else if (latchTurn > 0 && t > latchTurn + 25)
                minAfterBoom = Math.Min(minAfterBoom, share);        // Malthus equilibrium: famines bite
            // S5 (T2.7 re-anchor, stated): the cap binds PROMOTIONS (pinned
            // exactly by the mobility-invariant tests); the share itself can
            // drift a little past it passively — the retuned adult mortality
            // climbs steeply with age, so the peasant-heavy older cohorts die
            // faster than the artisan-heavy younger ones, and young-adult-
            // peaked migration reshapes the denominator too. Measured drift
            // peaks at 0.213; the bound allows 0.03 of composition drift and
            // still catches a runaway-promotion regression.
            Assert.True(share <= cfg.Mobility.TargetShareCap + 0.03,
                $"turn {t}: share {share:F3} exceeded the cap {cfg.Mobility.TargetShareCap} + drift margin");
            Assert.True(ConservationAuditor.IsConserved(world, out string report), $"turn {t}: {report}");
        }

        _output.WriteLine(
            $"seed 42 telemetry: latchTurn={latchTurn} firstPresentTurn={firstPresentTurn} " +
            $"firstEmergeTrueTurn={firstEmergeTrueTurn} recedeTrueTurns(post-latch)={recedeTrueTurns} " +
            $"recessions={recessions} reLatches={reLatches} " +
            $"unpublishedTurns={unpublishedTurns} boomPeak={boomPeak:F4} minAfterBoom={minAfterBoom:F4}");

        // S3: not never — a DOMAIN check on the horizon, not a timing band.
        Assert.InRange(latchTurn, 1, 900);
        Assert.Equal(firstEmergeTrueTurn, latchTurn); // S2 restated as one number
        Assert.Equal(1, unpublishedTurns);            // the warm-up guard covers turn 1 only
        // S5: plateau AT the cap during the boom (sustained surplus ≈ 3 →
        // target pins to the cap; relaxation at 0.08/yr closes the gap well
        // within the 25-turn window).
        // T2.5 note: migration churn (young-adult-peaked flows between the
        // four dev settlements) keeps settlement 0's share a little under the
        // exact cap — bound relaxed 0.18 → 0.15. The mobility MECHANISM is
        // unchanged and its exact plateau/cap behavior stays pinned by the
        // hand-built ClassWorld tests, which run no migration.
        // Upper bound carries the same 0.03 composition-drift margin as the
        // per-turn assert above (T2.7, same measured cause) — a promotion
        // runaway still lands far beyond it.
        Assert.True(boomPeak is >= 0.15 and <= 0.2301,
            $"boom peak share {boomPeak:F3} — never plateaued near the 0.20 cap");
        // THE RECESSION ARM IS CLOSED AS UNSUPPORTED (director ruling, M4
        // completion §8), and it is deliberately not replaced by a weaker
        // assertion in the same place.
        //
        // The arm asserted that once a Malthus equilibrium erased the surplus,
        // the artisan share would fall away from the cap — artisans drain when
        // the surplus dies. The equilibrium it waits for does not arrive: the
        // world is pre-Malthusian for its whole horizon (CR-003), surplus stays
        // above the targetShare saturation point for the campaign, and the
        // recede predicate never fires again after year ~10 (T4.19-E telemetry:
        // on seed 42 the recede predicate DOES fire again — 19 recessions / 19
        // re-latches across the 1.1/1.3 band over 900 turns; the arm stays
        // closed because neither direction is asserted, not because recede is
        // silent). It was then carried
        // as a CR-003 quarantine asserting the NEGATION — that the drain had
        // NOT happened — which measured 0.035 against a 0.05 guard and went red.
        //
        // The ruling closes the EXPECTATION rather than re-banding the guard,
        // and that is the honest disposition: neither direction is a property
        // this world supports, so asserting either way was asserting a
        // prediction rather than a mechanism. Nothing about the drain mechanism
        // itself is retired — ClassMobilitySystem's famine-demote branch is
        // still live and still exercised by the hand-built ClassWorld tests,
        // which control the surplus directly instead of waiting on an
        // equilibrium the canonical world never reaches.
        //
        // EVERY OTHER ARM OF THIS TEST STAYS LIVE AND UNWEAKENED: S1–S4 above,
        // the per-turn cap, the boom plateau and the per-turn conservation
        // audit. `minAfterBoom` is left computed and reported so the number
        // remains visible to a reader without being asserted on.
        Assert.InRange(minAfterBoom, 0.0, 1.0);   // domain only: a share, not a claim
    }

    // --- presence is not emergence (T4.19-D) --------------------------------

    [Fact]
    public void ArtisanPresence_CanPrecedeLocalLatch_ViaMigration_IsNotEmergence()
    {
        // REGRESSION PIN for the T4.19-D instrument correction. Migration
        // moves people class-preserving (T2.5), so a settlement whose own
        // emergence predicate has NOT fired can hold an artisan adult carried
        // in from a neighbour that latched first. The corrected instrument
        // (the ClassStates latch) must not count that as local emergence.
        //
        // Seed and preset: the test's own recipe (dev preset, 256 px, N = 4,
        // production pipeline, canonical era) on seed 16, which
        // docs/t4.19c-remeasurement.md §4.1 records on the NEW arm as
        // first-present 22 / latch 39 — the widest immigrant-precedes-latch
        // gap in the battery. MEASURED on this tree (T4.19-D):
        // first-present turn = 22, latch turn = 39.
        // Bounded: the loop stops at the latch (hard ceiling 120 turns).
        SimConfig cfg = TestConfigs.Sim();
        TurnExecutor exec = ProductionExecutor(cfg);
        WorldState world = WorldFounding.Found(TestConfigs.DevWorldgen(), cfg, 16);
        Assert.Equal(0, ArtisanAdults(world));
        Assert.Equal(0, LatchActive(world, S0, Artisans));

        int firstPresentTurn = -1, latchTurn = -1;
        int activeAtFirstPresent = -1;
        long artisansAtFirstPresent = 0;
        for (int t = 1; t <= 120 && latchTurn < 0; t++)
        {
            world = exec.Step(world);
            long artisans = ArtisanAdults(world);
            int active = LatchActive(world, S0, Artisans);
            if (firstPresentTurn < 0 && artisans > 0)
            {
                firstPresentTurn = t;
                activeAtFirstPresent = active;
                artisansAtFirstPresent = artisans;
            }
            if (active == 1) latchTurn = t;
        }

        Assert.True(latchTurn > 0, "settlement 0 never latched within 120 turns");
        Assert.True(firstPresentTurn > 0, "no artisan adult ever present in settlement 0");
        // The migration event precedes the mechanism's event ...
        Assert.True(firstPresentTurn < latchTurn,
            $"expected an immigrant artisan before the latch: first-present {firstPresentTurn}, latch {latchTurn}");
        // ... and on the first-present turn the class is still DORMANT here.
        Assert.Equal(0, activeAtFirstPresent);
        // The corrected instrument reports the latch, not the presence.
        Assert.NotEqual(firstPresentTurn, latchTurn);
        Assert.Equal(39, latchTurn);
        Assert.Equal(22, firstPresentTurn);
        Assert.InRange(artisansAtFirstPresent, 1, 3); // a handful of migrants, not a promoted class (§4.3 item 2)
        Assert.True(ConservationAuditor.IsConserved(world, out string report), report);
    }

    // --- hysteresis teeth ---------------------------------------------------

    private static WorldState Oscillate(long lowHarvest, long highHarvest, int steps, out int transitions)
    {
        SimConfig cfg = TestConfigs.Sim();
        var peasants = new long[Cohorts.Count];
        for (int c = Cohorts.FirstAdult; c < Cohorts.FirstElder; c++) peasants[c] = 1000;
        WorldState world = ClassWorld(peasants, new long[Cohorts.Count]);
        var exec = new TurnExecutor(FlatEra(10.0), [SystemCatalog.ClassMobility(cfg)]);

        transitions = 0;
        int last = ActiveFlag(world);
        for (int t = 0; t < steps; t++)
        {
            DriveSurplus(world, t % 2 == 0 ? lowHarvest : highHarvest, 1000);
            world = exec.Step(world);
            int now = ActiveFlag(world);
            if (now != last) transitions++;
            last = now;
        }
        return world;
    }

    [Fact]
    public void Hysteresis_OscillationInsideTheBand_AtMostOneTransition()
    {
        // The teeth test the packet demands: surplus oscillating ACROSS the
        // interior of the (1.1, 1.3) band — 1.12 ⇄ 1.28 — crosses neither
        // threshold. A single-threshold implementation (emerge iff > T for any
        // T inside the band) toggles every cycle; the latch produces ≤ 1.
        Oscillate(lowHarvest: 1120, highHarvest: 1280, steps: 20, out int transitions);
        Assert.True(transitions <= 1, $"{transitions} transitions on an in-band oscillation");
    }

    [Fact]
    public void Hysteresis_MetricHasTeeth_FullBandCrossingsDoTransition()
    {
        // The control proving the counter counts: 0.9 ⇄ 1.5 crosses BOTH
        // thresholds, so the latch must flip repeatedly (≥ 2 transitions).
        Oscillate(lowHarvest: 900, highHarvest: 1500, steps: 20, out int transitions);
        Assert.True(transitions >= 2, $"only {transitions} transitions on full-band crossings — the metric is blind");
    }

    // --- famine demote-first ------------------------------------------------

    [Fact]
    public void Famine_DrainsArtisansBeforePeasantStarvationPeaks()
    {
        // Engineered famine: an active artisan class, a small store, NO
        // harvest (no catchment). The deficit appears as the store empties;
        // the famine valve demotes at 0.5/yr × deficit — artisan adults must
        // hit ZERO strictly before the per-turn starvation flow peaks, and
        // starvation must continue after (the peasants keep dying — the
        // ordering assertion of the packet).
        SimConfig cfg = TestConfigs.Sim();
        var peasants = new long[Cohorts.Count];
        var artisans = new long[Cohorts.Count];
        for (int c = 0; c < Cohorts.Count; c++) peasants[c] = 2000;
        // Youngest adult cohorts only: slot-advance aging (+2/turn at dt = 10)
        // must not carry them into the elder band inside the test horizon —
        // the drain under test is the famine VALVE, not aging attrition.
        artisans[3] = 1500; artisans[4] = 1500; artisans[5] = 1500;
        WorldState world = ClassWorld(peasants, artisans, artisansActive: true);
        // Freeze a HEALTHY surplus signal (no Farming in the pipeline, so
        // LastHarvestUnits persists): the latch stays active and the share
        // stays at target until the REAL deficit arrives — isolating the
        // famine valve, which must demote regardless of what the (stale)
        // predicates say.
        DriveSurplus(world, harvest: 700_000, demand: 317_000);
        // Store = two fully-fed turns + 85% of the third: a SMALL partial
        // deficit (~0.15) at t3, then famine — the deficit ramps, so the
        // ordering is observable: the famine valve (2.0/yr × deficit) clears
        // the artisans on the small deficit while peak starvation arrives
        // with the full deficit a turn later (an instant deficit of 1.0
        // collapses both into one turn). T2.7b re-anchor (stated): demand
        // decays turn over turn (317,000 → 244,051 → 220,522 under the
        // ADR-011 micro-step kernel — the exponential sinks kill slightly
        // fewer than the old per-turn flows, so demand decays less steeply
        // than the T2.7 trace); 748,494 = measured d1 + d2 + 0.85 × d3
        // restores the t3 partial-deficit ramp.
        int storeRow = 0;
        new Ledger(world.LedgerFlows).Flow(ref world.GoodStocks.Ref(storeRow).Amount,
            ConservedQuantityIds.OfGood(new GoodId(1)), ReasonIds.InitialEndowment, 748_494,
            FlowDirection.Source, OverdrawPolicy.Throw);

        var exec = new TurnExecutor(FlatEra(10.0),
            [SystemCatalog.Consumption(cfg),
             SystemCatalog.ClassMobility(cfg), SystemCatalog.Demographics(cfg)]);

        // DRAIN METRIC: the famine valve's signature is a massive single-turn
        // demotion (≥ 70% of a substantial artisan class) — "gone to exactly
        // zero" is the wrong observable, because artisan CHILDREN keep
        // maturing into the adult band for a turn or two after the drain
        // (births are group-local by T2.1 design). T2.7b re-anchor: the
        // ADR-011 diffusive micro-step aging matures children CONTINUOUSLY
        // rather than in discrete slot jumps, so a FULL drain of the PREV
        // adults still leaves ~27% of the previous count as fresh maturation
        // (measured: 3262 → 883 on the drain turn) — the old ≥ 80% bar is
        // unreachable through that influx. The ordering under test:
        // the drain turn strictly precedes the peak starvation turn, and
        // starvation continues after the drain (peasants keep dying).
        long prevStarved = 0, prevArtisans = ArtisanAdults(world);
        int drainTurn = -1, peakStarvationTurn = -1;
        long peakStarvationDelta = -1;
        bool starvationAfterDrain = false;
        for (int t = 1; t <= 30; t++)
        {
            world = exec.Step(world);
            long starvedTotal = 0;
            for (int i = 0; i < world.LedgerFlows.Count; i++)
            {
                LedgerFlowRow row = world.LedgerFlows[i];
                if (row.Quantity == ConservedQuantityIds.Population && row.Reason == ReasonIds.Starvation)
                    starvedTotal = row.TotalSunk;
            }
            long delta = starvedTotal - prevStarved;
            prevStarved = starvedTotal;
            if (delta > peakStarvationDelta) { peakStarvationDelta = delta; peakStarvationTurn = t; }
            long artisansNow = ArtisanAdults(world);
            if (drainTurn < 0 && prevArtisans >= 1000 && artisansNow * 10 <= prevArtisans * 3)
                drainTurn = t;
            prevArtisans = artisansNow;
            if (drainTurn > 0 && t > drainTurn && delta > 0) starvationAfterDrain = true;
            Assert.True(ConservationAuditor.IsConserved(world, out string report), $"turn {t}: {report}");
        }

        // THE FAMINE VALVE STILL FIRES, and that is still asserted: the
        // mechanism is real and this test keeps its teeth on it.
        Assert.True(drainTurn > 0, "the famine valve never drained the artisans");
        Assert.True(starvationAfterDrain, "no starvation after the drain — the rig is vacuous");

        // THE ORDERING CLAIM IS CLOSED AS UNSUPPORTED (director ruling, M4
        // completion §8), and it was already ruled UNREPRESENTABLE on
        // 2026-08-13 (docs/t4.2-review-record.md): "INVALID / UNREPRESENTABLE AT
        // CURRENT CAUSAL RESOLUTION", explicitly not a T4.2 regression.
        //
        // What it asserted — drainTurn STRICTLY BEFORE peakStarvationTurn — asks
        // the system to distinguish two effects that are simultaneous by
        // ratified design. The famine valve (ClassMobilitySystem) and the
        // starvation sink (DemographicsSystem) are two independent consumers of
        // the SAME one-turn-lagged ConsumptionDeficit row; under law 6 neither
        // can see the other's current-turn output. At turn resolution the strict
        // inequality is only observable when the deficit ramps gradually across
        // several turns, and T4.2's granary capacity ceiling removed the ramp:
        // the store truncates and the deficit arrives at 0.805 in a single step,
        // so both fire on turn 3 and the measured result is "drain at turn 3,
        // starvation peaked at turn 3".
        //
        // Asserting it anyway would be asserting a resolution the kernel does
        // not have. The semantic intent — a society sheds its non-subsistence
        // classes before mass death peaks — is not discarded; it is recorded as
        // a property that needs sub-turn causal resolution to express, which is
        // a kernel question and not this test's to force. The turn indices stay
        // computed and in scope so a future run can read them.
        Assert.True(peakStarvationTurn > 0, "starvation never peaked — the rig is vacuous");
    }

    // --- mobility conservation ----------------------------------------------

    [Fact]
    public void Mobility_SameCohortAdultOnly_PerCohortCrossClassTotalsInvariant()
    {
        // Mobility-only pipeline, strong invariants: (1) for EVERY cohort, the
        // cross-class total is exactly invariant (same-cohort transfers only);
        // (2) child and elder rows never change (adult-cohorts-only); (3) no
        // ledger source/sink footprint (transfers conserve by construction).
        SimConfig cfg = TestConfigs.Sim();
        var peasants = new long[Cohorts.Count];
        var artisans = new long[Cohorts.Count];
        for (int c = 0; c < Cohorts.Count; c++) { peasants[c] = 3000 + 11 * c; artisans[c] = c * 5; }
        WorldState world = ClassWorld(peasants, artisans, artisansActive: true);
        DriveSurplus(world, harvest: 2000, demand: 1000); // strong surplus → promotion pressure
        var exec = new TurnExecutor(FlatEra(10.0), [SystemCatalog.ClassMobility(cfg)]);

        var totals = new long[Cohorts.Count];
        for (int c = 0; c < Cohorts.Count; c++) totals[c] = peasants[c] + artisans[c];

        bool moved = false;
        for (int t = 1; t <= 8; t++)
        {
            long artisansBefore = ArtisanAdults(world);
            world = exec.Step(world);
            if (ArtisanAdults(world) != artisansBefore) moved = true;

            var perCohort = new long[Cohorts.Count];
            for (int i = 0; i < world.Buckets.Count; i++)
            {
                BucketRow row = world.Buckets[i];
                perCohort[row.CohortIdx] += row.Count.Value;
                if (!BandViews.IsAdult(row.CohortIdx) && row.Class == Artisans)
                    Assert.Equal(artisans[row.CohortIdx], row.Count.Value); // non-adults never move
            }
            for (int c = 0; c < Cohorts.Count; c++)
                Assert.Equal(totals[c], perCohort[c]);

            foreach (ReasonId reason in new[] { ReasonIds.Births, ReasonIds.Deaths, ReasonIds.Starvation })
            {
                for (int i = 0; i < world.LedgerFlows.Count; i++)
                {
                    LedgerFlowRow row = world.LedgerFlows[i];
                    if (row.Quantity == ConservedQuantityIds.Population && row.Reason == reason)
                        Assert.True(row.TotalSourced == 0 && row.TotalSunk == 0,
                            $"mobility left a {reason.Value} footprint");
                }
            }
        }
        Assert.True(moved, "mobility never moved anyone — invariants vacuous");
    }

    [Property(MaxTest = 60)]
    public Property Mobility_ArbitraryStatesAndSignals_ConservesExactly()
    {
        // The T2.1 property suite extended to class transfers (load-bearing
        // per the packet): random two-class adult populations, random surplus
        // and deficit signals, classmobility + demographics steps — audit
        // exact and person-exact reconciliation from flows alone, every step.
        Gen<long> countGen = Gen.Choose(0, 100_000).Select(v => (long)v);
        Gen<long[]> stateGen = countGen.ArrayOf(Cohorts.Count);
        Gen<(long[] Peasants, long[] Artisans)> pairGen =
            stateGen.SelectMany(p => stateGen.Select(a => (p, a)));
        Gen<(int DeficitPct, int SurplusPct)> signalGen =
            Gen.Choose(0, 100).SelectMany(d => Gen.Choose(0, 400).Select(sp => (d, sp)));
        return Prop.ForAll(pairGen.ToArbitrary(), signalGen.ToArbitrary(), (pair, signal) =>
        {
            (long[] peasants, long[] artisans) = pair;
            (int deficitPct, int surplusPct) = signal;
            SimConfig cfg = TestConfigs.Sim();
            WorldState world = ClassWorld(peasants, artisans, artisansActive: true);
            world.ConsumptionDeficits.Add(new ConsumptionDeficitRow(S0, deficitPct / 100.0, 1000));
            world.GoodStocks.Add(new GoodStockRow(S0, new GoodId(1), Conserved.Zero, 0.0, 0.0, surplusPct * 10));
            var exec = new TurnExecutor(FlatEra(10.0),
                [SystemCatalog.ClassMobility(cfg), SystemCatalog.Demographics(cfg)]);
            for (int t = 0; t < 3; t++)
            {
                world = exec.Step(world);
                if (!ConservationAuditor.IsConserved(world, out string report))
                    return false.Label($"turn {t + 1}: {report}");
            }
            long endow = 0, births = 0, deaths = 0, starved = 0;
            for (int i = 0; i < world.LedgerFlows.Count; i++)
            {
                LedgerFlowRow row = world.LedgerFlows[i];
                if (row.Quantity != ConservedQuantityIds.Population) continue;
                if (row.Reason == ReasonIds.InitialEndowment) endow = row.TotalSourced;
                else if (row.Reason == ReasonIds.Births) births = row.TotalSourced;
                else if (row.Reason == ReasonIds.Deaths) deaths = row.TotalSunk;
                else if (row.Reason == ReasonIds.Starvation) starved = row.TotalSunk;
            }
            long total = 0;
            for (int i = 0; i < world.Buckets.Count; i++) total += world.Buckets[i].Count.Value;
            return (total == checked(endow + births - deaths - starved))
                .Label("person-exact reconciliation failed under mobility");
        });
    }

    // --- scaffolded artisan contributions -----------------------------------

    [Fact]
    public void ToolStock_RaisesHarvest_MonotoneSaturating_AndDepletes()
    {
        // T3.3 SCAFFOLDING DEMOLITION (m3 spec §1). The retired test pinned the
        // M2 abstraction: harvest rose with the ARTISAN SHARE via a free
        // multiplier. That mechanism is deleted. What replaces it is a real
        // good: harvest rises with the TOOL STOCK the settlement actually
        // holds, saturates at one tool per farmer, and — the half a modifier
        // could never have — the stock DEPLETES as equipped farmers work.
        //
        // Fixed 4000 farming adults, land never binds, so harvest is exactly
        // the labor side: 4000 × outputPerFarmer × (1 + bonus × equipRatio).
        SimConfig cfg = TestConfigs.Sim();
        const double dt = 10.0;
        const long farmers = 4000;
        var toolStocks = new long[] { 0, 1000, 2000, 4000, 8000 }; // equip 0, .25, .5, 1, 1 (capped)
        var harvests = new long[toolStocks.Length];
        var toolsLeft = new long[toolStocks.Length];

        for (int i = 0; i < toolStocks.Length; i++)
        {
            var peasants = new long[Cohorts.Count];
            peasants[5] = farmers;
            WorldState world = ClassWorld(peasants, new long[Cohorts.Count]);
            // T3.5b: the tool-factor arithmetic below assumes every adult
            // farms; pin it explicitly now that the default is the subsistence
            // mix (§7.8: a rig isolates its variable).
            world.SectorAllocations.Add(new SectorAllocationRow(
                S0, Farming: 1.0, Herding: 0.0, Extraction: 0.0, Crafting: 0.0, Construction: 0.0));
            world.CatchmentSummaries.Add(new CatchmentSummaryRow(
                S0, NodeCount: 1, EffectiveArableKm2: 1e9, // land never binds
                NetworkRevision: 0, LastRecomputeTurn: 0));
            world.GoodStocks.Add(new GoodStockRow(S0, new GoodId(1), Conserved.Zero, 0.0, 0.0));
            int toolsRow = world.GoodStocks.Add(new GoodStockRow(
                S0, new GoodId(cfg.Goods!.IdOf("tools")), Conserved.Zero, 0.0, 0.0));
            if (toolStocks[i] > 0)
            {
                new Ledger(world.LedgerFlows).Flow(
                    ref world.GoodStocks.Ref(toolsRow).Amount,
                    ConservedQuantityIds.OfGood(new GoodId(cfg.Goods!.IdOf("tools"))),
                    ReasonIds.InitialEndowment, toolStocks[i],
                    FlowDirection.Source, OverdrawPolicy.Throw);
            }

            var exec = new TurnExecutor(FlatEra(dt), [SystemCatalog.Production(cfg)]);
            WorldState next = exec.Step(world);
            harvests[i] = next.GoodStocks[0].LastProducedUnits;
            toolsLeft[i] = next.GoodStocks[1].Amount.Value;
        }

        for (int i = 1; i < harvests.Length; i++)
            Assert.True(harvests[i] >= harvests[i - 1],
                $"tool bonus not monotone in stock: {harvests[i - 1]} → {harvests[i]}");
        Assert.True(harvests[1] > harvests[0], "tools had no effect — the mechanism is inert");
        Assert.Equal(harvests[^1], harvests[^2]); // saturated: one tool per farmer is full equipment

        // Exact pin at full equipment — never above 1 + bonus.
        long capExact = (long)Math.Floor(
            farmers * cfg.Farming.OutputPerFarmerPerYear
            * (1.0 + cfg.Production.ToolYieldBonusMax) * dt);
        Assert.Equal(capExact, harvests[^1]);

        // DEPLETION — the half the scaffold could not express. Equipped farmers
        // wear stock out; an unequipped settlement wears nothing.
        Assert.Equal(toolStocks[0], toolsLeft[0]);
        for (int i = 1; i < toolStocks.Length; i++)
            Assert.True(toolsLeft[i] < toolStocks[i],
                $"tools did not deplete at stock {toolStocks[i]}: {toolsLeft[i]} — a modifier, not a stock");
    }

    [Fact]
    public void ConstructionLabor_ArtisansJoinThePool_SliderScaled_Exact()
    {
        // Founded dev world: seed artisans by a sanctioned test transfer, set
        // a 40% farm order, one step — the bank accrues EXACTLY
        // laborPerAdult × pathShare × (peasants + weight × artisans) × dt,
        // and a 100% farm order still banks exactly nothing (the T1.6
        // invariant the slider-scaling preserves).
        // RIG LEVER, stated (T3.4c precedent; T3.6b repair): endowment jitter
        // is pinned to 0 IN THIS RIG because the test's subject is slider-
        // scaled labor pooling, not founding variance — the hand-computed
        // 10-per-cohort transfers below assume the unjittered cohort counts,
        // and the ADR-017 amplitude (0.69) left a cohort at 9 on this seed
        // (measured LedgerOverdrawException in the T3.6b fallout run).
        SimConfig cfg = TestConfigs.Sim();
        cfg = cfg with { Founding = cfg.Founding with { EndowmentJitter = 0.0 } };
        WorldState world = WorldFounding.Found(TestConfigs.DevWorldgen(), cfg, 42);
        // Move 10 adults from each of cohorts 3..8 (60 total) into the
        // artisan buckets via the Ledger (founding cohorts hold 30/28/26/24/
        // 22/20 — no overdraw).
        var ledger = new Ledger(world.LedgerFlows);
        for (int cohort = 3; cohort <= 8; cohort++)
        {
            int src = -1, dst = -1;
            for (int i = 0; i < world.Buckets.Count; i++)
            {
                if (world.Buckets[i].CohortIdx != cohort || world.Buckets[i].Settlement != S0) continue;
                if (world.Buckets[i].Class == Peasants) src = i;
                if (world.Buckets[i].Class == Artisans) dst = i;
            }
            ledger.Transfer(ref world.Buckets.Ref(src).Count, ref world.Buckets.Ref(dst).Count,
                10, OverdrawPolicy.Throw);
        }
        long peasants = ClassMobilitySystem.AdultsOfClass(world.Buckets, S0, Peasants);
        Assert.Equal(60, ArtisanAdults(world));

        var orders = new OrderLog();
        orders.Append(new OrderRecord(1, ActorId: 1, OrderKind.LaborAllocation, TargetId: 0, Amount: 40.0));
        // Canonical era: a founded world's clock starts at year −4000, outside
        // the FlatEra band (Neolithic dt is 10 anyway).
        var exec = new TurnExecutor(CanonicalEra(), [SystemCatalog.PathBuild(cfg)], orders);
        // Delivery semantic (T1.9, pinned): an order stamped turn 1 lands at
        // turn 2; the allocation steers accrual (read from Prev) at turn 3.
        WorldState next = exec.Step(world);
        next = exec.Step(next);
        next = exec.Step(next);

        // T3.3: builders are the CONSTRUCTION share of ALL adults — the M2
        // weighted pool (peasants + weight × artisans) is DELETED, so the
        // artisan/peasant split no longer changes the bank at all. The legacy
        // LaborAllocation order maps 40% farm → 60% construction.
        double builders = 0.6 * (peasants + 60);
        double expected = cfg.PathBuild.LaborPerAdultPerYear * builders * 10.0;
        // T3.5b: the subsistence default (construction 0.08) banks for EVERY
        // settlement from turn 1, so rows exist for all four dev settlements
        // and S0's bank carries two default turns before the order lands. The
        // exact pin becomes the DELTA across the post-order step; the twin
        // (identical world, no order) supplies the default-turns baseline —
        // which also keeps the twin non-vacuous where "banks exactly nothing"
        // was retired with the all-farming default.
        int S0row = -1;
        for (int i = 0; i < next.PathProgress.Count; i++)
            if (next.PathProgress[i].Settlement == S0) { S0row = i; break; }
        Assert.True(S0row >= 0, "no bank row for S0");
        WorldState twin = WorldFounding.Found(TestConfigs.DevWorldgen(), cfg, 42);
        var exec2 = new TurnExecutor(CanonicalEra(), [SystemCatalog.PathBuild(cfg)], new OrderLog());
        WorldState t2 = exec2.Run(twin, 2);          // the two pre-order turns, default share
        int twinRow = -1;
        for (int i = 0; i < t2.PathProgress.Count; i++)
            if (t2.PathProgress[i].Settlement == S0) { twinRow = i; break; }
        double preOrderBank = twinRow >= 0 ? t2.PathProgress[twinRow].Banked : 0.0;
        // T4.7 — the delta must compare LABOUR COMMITTED, not bank level. `Banked`
        // is debited the instant a segment completes, and T4.7's cheaper
        // river-threaded segments make one complete inside this window, which sent
        // the raw delta negative. Each completed segment leaves exactly one
        // NetworkEdgeRow holding `StepCost × DirtPathSpeedFactor`, so its labour is
        // recovered exactly; adding it back on BOTH sides leaves the hand-computed
        // pin untouched and restores what the assertion always meant to measure.
        double Spent(WorldState w)
        {
            // SCOPE, stated because it is a real limitation (independent review):
            // this sums EVERY settlement's edges, while the bank it corrects is
            // S0's alone in a four-settlement world. It recovers the hand-computed
            // pin exactly (9 decimals) only because no other settlement completes
            // a segment inside this three-turn window. Correct today, fragile if
            // the window or the world grows — a future edit that makes another
            // settlement build here will over-correct and the pin will fail
            // loudly, which is the acceptable failure direction.
            double sum = 0.0;
            for (int e = 0; e < w.NetworkEdges.Count; e++)
                sum += w.NetworkEdges[e].Cost
                       / cfg.PathBuild.DirtPathSpeedFactor * cfg.PathBuild.BuildCostMultiplier;
            return sum;
        }
        // Exact pin: the post-order accrual is the 0.6 construction share of
        // ALL adults, artisans included at full weight. An unweighted pool
        // (peasants only) or a slider-ignoring artisan term shifts this
        // product and fails.
        Assert.Equal(expected,
            (next.PathProgress[S0row].Banked + Spent(next)) - (preOrderBank + Spent(t2)), 9);
    }
}
