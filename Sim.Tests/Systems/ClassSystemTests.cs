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
        if (world.FoodStores.Count == 0)
            world.FoodStores.Add(new FoodStoreRow(S0, Conserved.Zero, 0.0, 0.0, harvest));
        else world.FoodStores.Ref(0).LastHarvestUnits = harvest;
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

    // --- emergence in fed autoplay ------------------------------------------

    [Fact]
    public void Artisans_EmergeInFedAutoplay_PlateauAtTheCap_DocumentedWindow()
    {
        // The founding boom is labor-limited with surplus ratio ≈ 3 (harvest
        // 1000/yr vs demand ≈ 327/yr), far above the 1.3 emergence threshold.
        // DOCUMENTED WINDOW: variables first publish at turn 1 (zeros), carry
        // real surplus from turn 2, and the latch reads Prev — emergence lands
        // within turns [3, 10]; the share then relaxes toward the cap at
        // 0.08/yr and must sit within 10% of the 0.20 cap by turn 40.
        SimConfig cfg = TestConfigs.Sim();
        TurnExecutor exec = ProductionExecutor(cfg);
        WorldState world = WorldFounding.Found(TestConfigs.DevWorldgen(), cfg, 42);
        Assert.Equal(0, ArtisanAdults(world)); // founding: no artisans

        int emergenceTurn = -1;
        double boomPeak = 0.0, minAfterBoom = 1.0;
        for (int t = 1; t <= 60; t++)
        {
            world = exec.Step(world);
            long artisans = ArtisanAdults(world);
            if (emergenceTurn < 0 && artisans > 0) emergenceTurn = t;
            long adults = BandViews.Adults(world.Buckets, S0);
            double share = adults > 0 ? artisans / (double)adults : 0.0;
            if (t <= 25) boomPeak = Math.Max(boomPeak, share);       // the founding boom window
            else minAfterBoom = Math.Min(minAfterBoom, share);       // Malthus equilibrium: famines bite
            Assert.True(share <= cfg.Mobility.TargetShareCap + 1e-9,
                $"turn {t}: share {share:F3} exceeded the cap {cfg.Mobility.TargetShareCap}");
            Assert.True(ConservationAuditor.IsConserved(world, out string report), $"turn {t}: {report}");
        }

        Assert.True(emergenceTurn is >= 3 and <= 10,
            $"artisans emerged at turn {emergenceTurn} — outside the documented [3,10] window");
        // Plateau AT the cap during the boom (sustained surplus ≈ 3 → target
        // pins to the cap; relaxation at 0.08/yr closes the gap well within
        // the 25-turn window).
        // T2.5 note: migration churn (young-adult-peaked flows between the
        // four dev settlements) keeps settlement 0's share a little under the
        // exact cap — bound relaxed 0.18 → 0.15. The mobility MECHANISM is
        // unchanged and its exact plateau/cap behavior stays pinned by the
        // hand-built ClassWorld tests, which run no migration.
        Assert.True(boomPeak is >= 0.15 and <= 0.2001,
            $"boom peak share {boomPeak:F3} — never plateaued near the 0.20 cap");
        // And the other acceptance arm in the SAME run: once the Malthus
        // equilibrium erases the surplus (recession + famine demotions), the
        // share falls away from the cap — artisans drain when the surplus dies.
        Assert.True(minAfterBoom < 0.05,
            $"share never drained post-boom (min {minAfterBoom:F3}) — recession/famine valve inert");
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
        // Store ≈ 2.85 turns of demand (~317k/turn at dt = 10): two fully-fed
        // turns, then a SMALL partial deficit (~0.15), then famine — the
        // deficit ramps, so the ordering is observable: the famine valve
        // (2.0/yr × deficit) clears the artisans on the small deficit while
        // peak starvation arrives with the full deficit a turn later (an
        // instant deficit of 1.0 collapses both into one turn).
        int storeRow = 0;
        new Ledger(world.LedgerFlows).Flow(ref world.FoodStores.Ref(storeRow).Store,
            ConservedQuantityIds.Food, ReasonIds.InitialEndowment, 903_000,
            FlowDirection.Source, OverdrawPolicy.Throw);

        var exec = new TurnExecutor(FlatEra(10.0),
            [SystemCatalog.Consumption(cfg),
             SystemCatalog.ClassMobility(cfg), SystemCatalog.Demographics(cfg)]);

        // DRAIN METRIC: the famine valve's signature is a massive single-turn
        // demotion (≥ 80% of a substantial artisan class) — "gone to exactly
        // zero" is the wrong observable, because artisan CHILDREN keep
        // maturing into the adult band for a turn or two after the drain
        // (births are group-local by T2.1 design). The ordering under test:
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
            if (drainTurn < 0 && prevArtisans >= 1000 && artisansNow <= prevArtisans / 5)
                drainTurn = t;
            prevArtisans = artisansNow;
            if (drainTurn > 0 && t > drainTurn && delta > 0) starvationAfterDrain = true;
            Assert.True(ConservationAuditor.IsConserved(world, out string report), $"turn {t}: {report}");
        }

        Assert.True(drainTurn > 0, "the famine valve never drained the artisans");
        Assert.True(drainTurn < peakStarvationTurn,
            $"demote-first violated: drain at turn {drainTurn}, " +
            $"starvation peaked at turn {peakStarvationTurn}");
        Assert.True(starvationAfterDrain, "no starvation after the drain — ordering assertion vacuous");
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
            world.FoodStores.Add(new FoodStoreRow(S0, Conserved.Zero, 0.0, 0.0, surplusPct * 10));
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
    public void ToolMultiplier_MonotoneSaturating_NeverExceedsCap()
    {
        // Farming with FIXED peasants and growing artisans stacked on top: the
        // harvest rate is peasants × output × multiplier (labor-limited), so
        // the multiplier is directly observable. It must be monotone in the
        // artisan share, saturate once slope × share reaches the cap
        // (shares 0.2 and 0.5 → EXACTLY equal harvest), and never exceed
        // 1 + cap (assert against the exact capped product).
        SimConfig cfg = TestConfigs.Sim();
        const double dt = 10.0;
        long[] artisanCounts = [0, 100, 250, 1000, 4000]; // shares 0, .024, .059, .2, .5
        var harvests = new long[artisanCounts.Length];
        for (int i = 0; i < artisanCounts.Length; i++)
        {
            var peasants = new long[Cohorts.Count];
            var artisans = new long[Cohorts.Count];
            peasants[5] = 4000;
            artisans[6] = artisanCounts[i];
            WorldState world = ClassWorld(peasants, artisans);
            world.CatchmentSummaries.Add(new CatchmentSummaryRow(
                S0, NodeCount: 1, EffectiveFarmland: 1e9, // land never binds
                NetworkRevision: 0, LastRecomputeTurn: 0));
            world.FoodStores.Add(new FoodStoreRow(S0, Conserved.Zero, 0.0, 0.0));
            var exec = new TurnExecutor(FlatEra(dt), [SystemCatalog.Farming(cfg)]);
            WorldState next = exec.Step(world);
            harvests[i] = next.FoodStores[0].LastHarvestUnits;
        }

        for (int i = 1; i < harvests.Length; i++)
            Assert.True(harvests[i] >= harvests[i - 1],
                $"multiplier not monotone: harvest {harvests[i - 1]} → {harvests[i]}");
        Assert.True(harvests[1] > harvests[0], "multiplier flat — scaffolding inert");
        Assert.Equal(harvests[^1], harvests[^2]); // saturated: cap binds at share 0.1+
        long capExact = (long)Math.Floor(
            4000 * 1.0 * cfg.Farming.OutputPerFarmerPerYear
            * (1.0 + cfg.Mobility.ToolYieldBonusCap) * dt);
        Assert.Equal(capExact, harvests[^1]); // never exceeds 1 + cap — exactly at it
    }

    [Fact]
    public void ConstructionLabor_ArtisansJoinThePool_SliderScaled_Exact()
    {
        // Founded dev world: seed artisans by a sanctioned test transfer, set
        // a 40% farm order, one step — the bank accrues EXACTLY
        // laborPerAdult × pathShare × (peasants + weight × artisans) × dt,
        // and a 100% farm order still banks exactly nothing (the T1.6
        // invariant the slider-scaling preserves).
        SimConfig cfg = TestConfigs.Sim();
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

        // Same association order as the system (builders first) — bit-exact.
        double builders = 0.6 * (peasants + cfg.Mobility.ConstructionLaborWeight * 60);
        double expected = cfg.PathBuild.LaborPerAdultPerYear * builders * 10.0;
        Assert.Equal(1, next.PathProgress.Count);
        // Exact pin: the bank (~24) is far below a segment's cost
        // (StepCost × 50), so nothing was laid and the bank IS the accrual —
        // bit-exact. An unweighted pool (peasants only) or a slider-ignoring
        // artisan term shifts this product and fails.
        Assert.Equal(expected, next.PathProgress[0].Banked);
        // The exact pin: run a twin with zero artisans and compare deltas.
        WorldState twin = WorldFounding.Found(TestConfigs.DevWorldgen(), cfg, 42);
        var exec2 = new TurnExecutor(CanonicalEra(), [SystemCatalog.PathBuild(cfg)], new OrderLog());
        // (twin uses the never-ordered default 1.0 → banks exactly nothing)
        WorldState t2 = exec2.Run(twin, 3);
        Assert.True(t2.PathProgress.Count == 0 || t2.PathProgress[0].Banked == 0.0,
            "100% farm banked labor — the T1.6 invariant broke");
    }
}
