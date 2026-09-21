using Sim.Core;
using Sim.Core.Kernel;
using Sim.Core.State;
using Sim.Core.Systems;
using Sim.Core.Systems.Consumption;
using Sim.Core.Systems.Migration;
using Sim.Core.Worldgen;
using Sim.Tests.TestUtil;

namespace Sim.Tests.Systems;

/// <summary>
/// T4.21-2 — BOUNDED MIGRATION (docs/t4.21-architecture.md §3.4, §3.4.4, §3.5,
/// §6.3; ADR-025). Every mandated case of §6.3 as a named M_* test: the exact
/// flight hazard on the best exit (HOW MANY), shares (WHERE), the basin caps on
/// the gap channel at both ends, the vacancy bound on total inflow, the
/// destination-free readout value, small-population flooring, dt composition,
/// recovery, and the planner-equals-step contract.
///
/// Hand worlds, MigrationSystem ALONE (nothing else moves a person or a grain),
/// so every number here is the migration mechanism's and nothing else's. Where
/// a test pins a turn-exact value it multiplies the SAME public expression the
/// system does (<see cref="MigrationSystem.FlightFractionOf"/>) — no
/// re-implementation of the arithmetic.
/// </summary>
public class MigrationBoundedTests
{
    private const int YoungAdult = 4;   // CohortProfile[4] = 1.0, cohortWeights[4] = 1.0 (adult)

    private static EraTable FlatEra(double dtYears) => EraTableLoader.Load(
        $$"""{ "bands": [ { "name": "flat", "startYear": 0, "endYear": 100000, "dtYears": {{dtYears.ToString(System.Globalization.CultureInfo.InvariantCulture)}} } ] }""");

    private static TurnExecutor MigrationOnly(SimConfig cfg, double dt = 10.0) =>
        new(FlatEra(dt), [SystemCatalog.Migration(cfg)]);

    /// <summary>Happiness weight 0 so a fed, food-holding destination has viability
    /// EXACTLY 1.0 (the T4.13 "w = 0 recovers the pre-T4.13 behaviour" arm) — the
    /// turn-exact pins below need ω = damping × 1.</summary>
    private static SimConfig Cfg() => TestConfigs.Sim() with
    { Migration = TestConfigs.Sim().Migration with { AttractivenessHappinessWeight = 0.0 } };

    private static double KFlight(SimConfig cfg) => cfg.Migration.BaseRatePerYear * cfg.Migration.FamineFlightFactor;

    /// <summary>K-settlement hand world: one (culture 1, religion 1, class 1) group of
    /// 16 cohort buckets per settlement, a zero grain row and a zero deficit row with
    /// DemandUnits 0 (⇒ FoodHeadroom +∞ — every destination is unbounded unless a
    /// test gives it a demand row via <see cref="Vacant"/>).</summary>
    private static WorldState World(params long[][] countsPerSettlement)
    {
        var world = new WorldState(7);
        var ledger = new Ledger(world.LedgerFlows);
        for (int s = 0; s < countsPerSettlement.Length; s++)
        {
            var id = new SettlementId(s);
            world.Settlements.Add(new SettlementRow(id, SiteCell: s, FoundedTurn: 0));
            for (int c = 0; c < Cohorts.Count; c++)
            {
                int row = world.Buckets.Add(new BucketRow(
                    id, new CultureId(1), new ReligionId(1), new ClassId(1),
                    c, Conserved.Zero, 0.0, 0.0, 0.0, 0.0));
                if (countsPerSettlement[s][c] > 0)
                {
                    ledger.Flow(ref world.Buckets.Ref(row).Count, ConservedQuantityIds.Population,
                        ReasonIds.InitialEndowment, countsPerSettlement[s][c],
                        FlowDirection.Source, OverdrawPolicy.Throw);
                }
            }
            world.GoodStocks.Add(new GoodStockRow(id, new GoodId(1), Conserved.Zero, 0.0, 0.0));
            world.ConsumptionDeficits.Add(new ConsumptionDeficitRow(id, 0.0, 0));
        }
        return world;
    }

    private static long[] Uniform(long perCohort)
    {
        var counts = new long[Cohorts.Count];
        for (int c = 0; c < Cohorts.Count; c++) counts[c] = perCohort;
        return counts;
    }

    private static long[] Single(int cohort, long count)
    {
        var counts = new long[Cohorts.Count];
        counts[cohort] = count;
        return counts;
    }

    private static void Link(WorldState w, int a, int b, double cost)
    {
        w.SettlementDistances.Add(new SettlementDistanceRow(new SettlementId(a), new SettlementId(b), cost));
        w.SettlementDistances.Add(new SettlementDistanceRow(new SettlementId(b), new SettlementId(a), cost));
    }

    private static void Endow(WorldState w, int settlement, long food)
    {
        new Ledger(w.LedgerFlows).Flow(ref w.GoodStocks.Ref(settlement).Amount,
            ConservedQuantityIds.OfGood(new GoodId(1)), ReasonIds.InitialEndowment, food,
            FlowDirection.Source, OverdrawPolicy.Throw);
    }

    private static void Land(WorldState w, int settlement, double arableKm2)
    {
        w.CatchmentSummaries.Add(new CatchmentSummaryRow(
            new SettlementId(settlement), NodeCount: 1, EffectiveArableKm2: arableKm2,
            NetworkRevision: 0, LastRecomputeTurn: 0));
    }

    private static void Deficit(WorldState w, int settlement, double ratio)
    {
        ConsumptionDeficitRow row = w.ConsumptionDeficits[settlement];
        w.ConsumptionDeficits[settlement] = row with { DeficitRatio = ratio };
    }

    /// <summary>Give a destination a FINITE food-influx limit: a demand row with
    /// <paramref name="demand"/> units, a vitals row with dt_prev
    /// <paramref name="dtPrev"/>, and a last grain harvest of <paramref name="harvest"/>
    /// (which also satisfies the absolute food gate). N_lim = harvest / dtPrev
    /// adult-equivalents per year (spec §3.6a; every non-staple reads 0 here).
    /// T4.21-4 (RULE 2): also ensures the CATCHMENT SUMMARY ROW the finite arm now
    /// requires — a settlement whose catchment has never been computed reads +∞
    /// (FoodHeadroom's null arm), which is the opposite of what this helper means.
    /// Added only when <see cref="Land"/> has not already added one, so the two
    /// helpers compose without duplicating a row, and added with arable 0 so it is
    /// ATTRACTIVENESS-NEUTRAL: MigrationSystem.cs:296-299 leaves arableKm2 at 0.0
    /// when no row matches, so an absent row and a zero-arable row are the same
    /// number to attractiveness, and only the FoodHeadroom arm moves.</summary>
    private static void Vacant(WorldState w, int settlement, long demand, double dtPrev, long harvest)
    {
        var id = new SettlementId(settlement);
        bool haveCatchment = false;
        for (int i = 0; i < w.CatchmentSummaries.Count; i++)
            if (w.CatchmentSummaries[i].Settlement == id) { haveCatchment = true; break; }
        if (!haveCatchment) Land(w, settlement, 0.0);
        w.ConsumptionDeficits[settlement] = w.ConsumptionDeficits[settlement] with { DemandUnits = demand };
        w.SettlementVitals.Add(new SettlementVitalsRow(id, 0, 0, dtPrev));
        w.GoodStocks.Ref(settlement).LastProducedUnits = harvest;
    }

    private static long Pop(WorldState w, int settlement)
    {
        long total = 0;
        for (int i = 0; i < w.Buckets.Count; i++)
            if (w.Buckets[i].Settlement.Value == settlement) total += w.Buckets[i].Count.Value;
        return total;
    }

    private static long PopAll(WorldState w)
    {
        long total = 0;
        for (int i = 0; i < w.Buckets.Count; i++) total += w.Buckets[i].Count.Value;
        return total;
    }

    private static int BucketRow(WorldState w, int settlement, int cohort)
    {
        for (int i = 0; i < w.Buckets.Count; i++)
            if (w.Buckets[i].Settlement.Value == settlement && w.Buckets[i].CohortIdx == cohort) return i;
        throw new InvalidOperationException("no such bucket");
    }

    private static MigrationPlan PlanOf(WorldState w, SimConfig cfg, double dt = 10.0) =>
        MigrationSystem.Plan(w, cfg, dt);

    // === bounded source outflow (kills M-MIG-CAP) ==============================

    [Fact]
    public void M_Flight_BoundedPerTurn()
    {
        // d = 1, ω = 1 (cost 0 ⇒ damping exp(−0) = 1; viability 1 at w = 0), profile 1
        // (young adult), dt 10: the whole source is ONE bucket of 1000, so the moved
        // count is floor(1000 × φ) with φ = 1 − e^{−2.4} ≈ 0.909 — 909 people, never
        // 1000, and the overdraw scaler is NOT what stopped it. Under the Euler form
        // (K·d·dt = 2.4 ⇒ desire 2400) only the scaler could, and it would move 1000.
        SimConfig cfg = Cfg();
        WorldState w = World(Single(YoungAdult, 1000), Uniform(10));
        Endow(w, 1, 50_000);
        Deficit(w, 0, 1.0);
        Link(w, 0, 1, 0.0);

        MigrationPlan plan = PlanOf(w, cfg);
        Assert.Equal(1.0, plan.ExitOpenness[0]);
        int row = BucketRow(w, 0, YoungAdult);
        double phi = MigrationSystem.FlightFractionOf(cfg.Migration.CohortProfile[YoungAdult], KFlight(cfg), 1.0, 1.0, 10.0);
        Assert.Equal(phi, plan.FlightFraction[row]);
        Assert.True(phi < 1.0 && phi > 0.9, $"φ = {phi}");
        Assert.Equal(1.0, plan.OverdrawScale[row]);            // bounded WITHOUT the scaler

        WorldState next = MigrationOnly(cfg).Step(w);
        long moved = next.MigrationFlows[0].Outflow;
        Assert.Equal((long)Math.Floor(phi * 1000.0), moved);   // 909
        Assert.True(moved < 1000, "the bucket was emptied — flight is not bounded");
        Assert.Equal(1000 - moved, Pop(next, 0));
        Assert.Equal(moved, next.MigrationFlows[1].Inflow);
    }

    // === one vs many destinations (kills M-MIG-SUMNOTMAX) =======================

    [Fact]
    public void M_DestinationCount_DoesNotMultiplyQuantity()
    {
        // HOW MANY is separated from WHERE: a source at d = 0.5 facing ONE viable
        // destination loses the same number of people as the same source facing
        // THREE identical ones (equal cost, equal food, no land gap). φ reads the
        // BEST exit (a max), so a Σ over exits would triple the exponent here.
        SimConfig cfg = Cfg();
        WorldState One()
        {
            WorldState w = World(Uniform(1000), Uniform(10));
            Endow(w, 1, 50_000); Deficit(w, 0, 0.5); Link(w, 0, 1, 10.0);
            return w;
        }
        WorldState Three()
        {
            WorldState w = World(Uniform(1000), Uniform(10), Uniform(10), Uniform(10));
            for (int d = 1; d <= 3; d++) { Endow(w, d, 50_000); Link(w, 0, d, 10.0); }
            Deficit(w, 0, 0.5);
            return w;
        }

        MigrationPlan pOne = PlanOf(One(), cfg), pThree = PlanOf(Three(), cfg);
        Assert.Equal(pOne.ExitOpenness[0], pThree.ExitOpenness[0]);          // ω: the best exit, bit-exact
        for (int c = 0; c < Cohorts.Count; c++)
            Assert.Equal(pOne.FlightFraction[BucketRow(One(), 0, c)], pThree.FlightFraction[BucketRow(Three(), 0, c)]);
        Assert.Equal(1.0, pOne.Share[0, 1]);
        for (int d = 1; d <= 3; d++) Assert.Equal(1.0 / 3.0, pThree.Share[0, d], 12);

        WorldState nOne = MigrationOnly(cfg).Step(One());
        WorldState nThree = MigrationOnly(cfg).Step(Three());
        long outOne = nOne.MigrationFlows[0].Outflow, outThree = nThree.MigrationFlows[0].Outflow;
        Assert.True(outOne > 0, "vacuous: nobody left");
        Assert.True(Math.Abs(outOne - outThree) <= Cohorts.Count,
            $"three destinations moved {outThree} vs one destination {outOne} — quantity multiplies with destination count");
        for (int d = 1; d <= 3; d++)
        {
            long share = nThree.MigrationFlows[d].Inflow;
            Assert.True(Math.Abs(share - outThree / 3.0) <= Cohorts.Count,
                $"destination {d} received {share} of {outThree} — shares are not ⅓ each");
        }
    }

    // === empty ruin =============================================================

    [Fact]
    public void M_EmptyRuin_StillRefused()
    {
        // A destination with zero store AND zero harvest on enormous land accepts
        // nobody (the T2.13 absolute food gate, unchanged) — and with no other exit
        // the source's ω is 0, φ is 0 and its people stay (die at home, ADR-012).
        SimConfig cfg = Cfg();
        WorldState w = World(Uniform(1000), Uniform(10));
        Land(w, 1, 1_000_000.0);                 // astronomically attractive land…
        Deficit(w, 0, 1.0);                      // …a source in full famine…
        Link(w, 0, 1, 1.0);                      // …next door. No food there.

        MigrationPlan plan = PlanOf(w, cfg);
        Assert.Equal(0.0, plan.Viability[1]);
        Assert.Equal(0.0, plan.ExitOpenness[0]);
        foreach (int row in plan.BucketRows[0]) Assert.Equal(0.0, plan.FlightFraction[row]);

        WorldState next = MigrationOnly(cfg).Step(w);
        Assert.Equal(0, next.MigrationFlows[1].Inflow);
        Assert.Equal(0, next.MigrationFlows[0].Outflow);
        Assert.Equal(16_000, Pop(next, 0));
    }

    // === empty high-capacity destination: bounded acceptance (kills M-MIG-VACANCY)

    [Fact]
    public void M_VacantViableDestination_AcceptsUpToCap()
    {
        // The spec's numbers (§6.3): destination j has 10 adult heads, a demand row
        // D = 100, dt_prev = 10 and a last harvest S = 800 (labour-bound, ρ = 8) ⇒
        // N_lim = 80, V = 70, cap = (1 − e^{−k·10}) × 70 = 35 at k = ln2/10. A
        // 1000-person famine source next door desires ≈ 909; j accepts 35 (within
        // flooring), the rest STAY at the source. A second turn accepts what the
        // shrunken vacancy allows (V = 35 ⇒ cap 17.5). At S = 130 (ρ = 1.3, V = 3,
        // cap = 1.5 ae) the inflow is at most 2 heads however attractive j is.
        SimConfig cfg = Cfg();
        double k = cfg.Demographics.HeadroomRelaxationPerYear;
        WorldState Rig(long harvest)
        {
            WorldState w = World(Single(YoungAdult, 1000), Single(YoungAdult, 10));
            Vacant(w, 1, demand: 100, dtPrev: 10.0, harvest: harvest);
            Deficit(w, 0, 1.0);
            Link(w, 0, 1, 0.0);
            return w;
        }

        WorldState w800 = Rig(800);
        MigrationPlan plan = PlanOf(w800, cfg);
        Assert.Equal(70.0, plan.Vacancy[1]);
        double cap = (1.0 - Math.Exp(-(k * 10.0))) * 70.0;
        Assert.Equal(cap, plan.VacancyCap[1]);
        Assert.True(Math.Abs(cap - 35.0) < 1e-9, $"cap = {cap}");
        Assert.True(plan.DesiredInflowAe[1] > 900.0, $"desired inflow {plan.DesiredInflowAe[1]} — the rig does not press the cap");
        Assert.True(plan.VacancyScale[1] < 1.0);

        WorldState turn1 = MigrationOnly(cfg).Step(w800);
        long in1 = turn1.MigrationFlows[1].Inflow;
        Assert.True(Math.Abs(in1 - 35) <= 1, $"turn 1 accepted {in1}, cap 35 (± flooring)");
        Assert.Equal(in1, turn1.MigrationFlows[0].Outflow);            // refused people stayed home
        Assert.Equal(1000 - in1, Pop(turn1, 0));

        // Turn 2 on the HELD N_lim (migration alone rewrites no harvest): V = 80 − (10 + in1).
        MigrationPlan plan2 = PlanOf(turn1, cfg);
        Assert.Equal(80.0 - (10 + in1), plan2.Vacancy[1]);
        WorldState turn2 = MigrationOnly(cfg).Step(turn1);
        long in2 = turn2.MigrationFlows[1].Inflow;
        Assert.True(in2 > 0 && Math.Abs(in2 - plan2.VacancyCap[1]) <= 1,
            $"turn 2 accepted {in2} against cap {plan2.VacancyCap[1]}");

        WorldState turn1Tiny = MigrationOnly(cfg).Step(Rig(130));
        Assert.Equal(3.0, PlanOf(Rig(130), cfg).Vacancy[1]);
        Assert.True(turn1Tiny.MigrationFlows[1].Inflow <= 2,
            $"tiny-land destination accepted {turn1Tiny.MigrationFlows[1].Inflow} > 2");
    }

    // === many famine sources → one destination (kills M-MIG-VACANCY) ===========

    [Fact]
    public void M_Basin_FanIn_Flight_BoundedByVacancy()
    {
        // Four famine sources of 1000 (d = 1) all next to one viable destination
        // with V = 70: total inflow ≤ cap (35) + flooring, not 4 × 909; each source
        // contributes in proportion to its desired flow (equal here); every refused
        // person is still at home; the chronicle reconciles.
        SimConfig cfg = Cfg();
        WorldState w = World(Single(YoungAdult, 1000), Single(YoungAdult, 1000),
            Single(YoungAdult, 1000), Single(YoungAdult, 1000), Single(YoungAdult, 10));
        Vacant(w, 4, demand: 100, dtPrev: 10.0, harvest: 800);
        for (int s = 0; s < 4; s++) { Deficit(w, s, 1.0); Link(w, s, 4, 0.0); }

        MigrationPlan plan = PlanOf(w, cfg);
        Assert.True(plan.DesiredInflowAe[4] > 3600.0, $"desired {plan.DesiredInflowAe[4]}");
        Assert.True(plan.VacancyScale[4] < 0.02, $"vacScale {plan.VacancyScale[4]}");

        WorldState next = MigrationOnly(cfg).Step(w);
        long inflow = next.MigrationFlows[4].Inflow;
        Assert.True(inflow > 0, "vacuous");
        Assert.True(inflow <= (long)Math.Ceiling(plan.VacancyCap[4]) + 4,
            $"inflow {inflow} exceeds the vacancy cap {plan.VacancyCap[4]} (+ flooring)");
        long outSum = 0;
        for (int s = 0; s < 4; s++)
        {
            long outS = next.MigrationFlows[s].Outflow;
            outSum += outS;
            Assert.True(Math.Abs(outS - inflow / 4.0) <= 1, $"source {s} moved {outS} of {inflow} — not proportional");
            Assert.Equal(1000 - outS, Pop(next, s));                 // refused people remain at home
        }
        Assert.Equal(inflow, outSum);
        Assert.Equal(4010, PopAll(next));
    }

    // === many sources → one destination, GAP channel (kills M-MIG-FANIN) =======

    private static WorldState FanIn(int sources, double sourceLand, long sourceCounts = 2000,
        long destCounts = 10, long destFood = 500_000, double destLand = 128_000.0)
    {
        var counts = new long[sources + 1][];
        for (int s = 0; s < sources; s++) counts[s] = Uniform(sourceCounts);
        counts[sources] = Uniform(destCounts);
        WorldState w = World(counts);
        for (int s = 0; s < sources; s++) { Land(w, s, sourceLand); Link(w, s, sources, 20.0); }
        Land(w, sources, destLand);
        Endow(w, sources, destFood);
        return w;
    }

    [Fact]
    public void M_Basin_FanIn_AggregateInflowBounded()
    {
        // Four equal land-holding sources (R = 10 000, P = 32 000 each) and one rich
        // destination (R = 10 000, P = 160), gap channel only, destinations row-less
        // (vacancy +∞). Pair caps: f × m*_ij = 0.25 × 15 920 = 3 980 EACH — 15 920 in
        // sum. The basin: P_pool = 128 160, R_pool = 50 000, M*_j^in = 10 000 × 128 160 /
        // 50 000 − 160 = 25 472 ⇒ f × M* = 6 368. Inflow ≤ 6 368 + flooring.
        SimConfig cfg = Cfg();
        double land = 10_000.0 / cfg.Migration.AttractivenessLandWeight;    // R = 10 000 exactly (5/64 × 128 000)
        WorldState w = FanIn(4, land);

        MigrationPlan plan = PlanOf(w, cfg);
        double rPool = 5 * 10_000.0, pPool = 160 + 4 * 32_000.0;
        double expectedCap = cfg.Migration.GapClosingFraction * (10_000.0 * pPool / rPool - 160);
        Assert.Equal(expectedCap, plan.GapInflowCap[4], 6);
        Assert.True(plan.GapInflowPairCapped[4] > 2.0 * expectedCap,
            $"pair-capped inflow {plan.GapInflowPairCapped[4]} does not exceed the basin cap {expectedCap} — the rig is vacuous");
        Assert.True(plan.DestScale[4] < 1.0);
        Assert.Equal(double.PositiveInfinity, plan.Vacancy[4]);

        WorldState next = MigrationOnly(cfg).Step(w);
        long inflow = next.MigrationFlows[4].Inflow;
        Assert.True(inflow > 0, "vacuous");
        Assert.True(inflow <= (long)expectedCap + 4 * Cohorts.Count,
            $"basin inflow {inflow} > f × M*_j^in = {expectedCap:F0} (+ flooring) — fan-in is unbounded");
    }

    [Fact]
    public void M_Basin_FanIn_SingleSource_BitIdenticalToPairCap()
    {
        // |B_j| = 1 ⇒ the basin pass SKIPS by branch: the one-source instance of the
        // fan-in builder with no source land IS MigrationStabilityTests'
        // GapCap_PairGrossFlow rig, and the step result is the same to the bit.
        SimConfig cfg = TestConfigs.Sim();
        WorldState twin = FanIn(1, 0.0);
        WorldState reference = MigrationTestWorld.TwoSettlements(
            sourceCounts: 2000, destCounts: 10, destFood: 500_000, destLand: 128_000.0);

        MigrationPlan plan = PlanOf(twin, cfg);
        Assert.Equal(double.PositiveInfinity, plan.GapInflowCap[1]);
        Assert.Equal(1.0, plan.DestScale[1]);
        Assert.Equal(1.0, plan.SrcScale[0]);

        WorldState a = MigrationOnly(cfg).Step(twin);
        WorldState b = MigrationOnly(cfg).Step(reference);
        Assert.Equal(b.MigrationFlows[1].Inflow, a.MigrationFlows[1].Inflow);
        Assert.Equal(b.Buckets.Count, a.Buckets.Count);
        for (int i = 0; i < a.Buckets.Count; i++)
        {
            Assert.Equal(b.Buckets[i].Count.Value, a.Buckets[i].Count.Value);
            Assert.Equal(b.Buckets[i].MigrationRemainder, a.Buckets[i].MigrationRemainder);
        }
    }

    // === one source → many destinations, GAP channel (kills M-MIG-FANOUT) ======

    [Fact]
    public void M_Basin_FanOut_AggregateOutflowBounded()
    {
        // The mirror: one land-holding source (R = 10 000, P = 32 000) feeding four
        // identical rich destinations (R = 10 000, P = 160 each). Pair caps sum to
        // 15 920; M*_i^out = 32 000 − 10 000 × 32 640 / 50 000 = 25 472 ⇒ f × M* = 6 368.
        SimConfig cfg = Cfg();
        double land = 10_000.0 / cfg.Migration.AttractivenessLandWeight;
        WorldState w = World(Uniform(2000), Uniform(10), Uniform(10), Uniform(10), Uniform(10));
        Land(w, 0, land);
        for (int d = 1; d <= 4; d++) { Land(w, d, land); Endow(w, d, 500_000); Link(w, 0, d, 20.0); }

        MigrationPlan plan = PlanOf(w, cfg);
        double rPool = 5 * 10_000.0, pPool = 32_000 + 4 * 160.0;
        double expectedCap = cfg.Migration.GapClosingFraction * (32_000 - 10_000.0 * pPool / rPool);
        Assert.Equal(expectedCap, plan.GapOutflowCap[0], 6);
        Assert.True(plan.GapOutflowDestScaled[0] > 2.0 * expectedCap, "rig vacuous: pair caps do not exceed the basin cap");
        Assert.True(plan.SrcScale[0] < 1.0);
        for (int d = 1; d <= 4; d++) Assert.Equal(1.0, plan.DestScale[d]);   // single-source basins skip

        WorldState next = MigrationOnly(cfg).Step(w);
        long outflow = next.MigrationFlows[0].Outflow;
        Assert.True(outflow > 0, "vacuous");
        Assert.True(outflow <= (long)expectedCap + Cohorts.Count,
            $"basin outflow {outflow} > f × M*_i^out = {expectedCap:F0} (+ flooring) — fan-out is unbounded");
        for (int d = 1; d <= 4; d++)
            Assert.True(Math.Abs(next.MigrationFlows[d].Inflow - outflow / 4.0) <= Cohorts.Count, "shares not equal");
    }

    // === die at home ==============================================================

    [Fact]
    public void M_NoViableExit_NobodyLeaves()
    {
        SimConfig cfg = Cfg();

        // (a) Every reachable destination is food-less: ω = 0 ⇒ φ = 0 EXACTLY, nobody
        //     moves — and B1's condition holds, so the readout is written (they may
        //     found; that is colonization's business, not migration's).
        WorldState ruins = World(Uniform(1000), Uniform(10), Uniform(10));
        Deficit(ruins, 0, 1.0);
        Link(ruins, 0, 1, 1.0); Link(ruins, 0, 2, 1.0);
        MigrationPlan pa = PlanOf(ruins, cfg);
        Assert.Equal(0.0, pa.ExitOpenness[0]);
        foreach (int row in pa.BucketRows[0]) Assert.Equal(0.0, pa.FlightFraction[row]);
        WorldState na = MigrationOnly(cfg).Step(ruins);
        Assert.Equal(0, na.MigrationFlows[0].Outflow);
        Assert.Equal(16_000, Pop(na, 0));
        double unplaced = 0.0;
        for (int i = 0; i < na.Buckets.Count; i++) if (na.Buckets[i].Settlement.Value == 0) unplaced += na.Buckets[i].UnplacedDeparture;
        Assert.True(unplaced > 0.0, "a stranded famine source wrote no readout");

        // (b) Every destination is viable but FULL (V = 0: N_lim = 100/10 = 10 < 20
        //     heads): vacScale = 0 ⇒ nobody moves, AND nobody founds — B1's condition
        //     is about viability, not capacity (CR-015 G7(a)); refused people stay.
        WorldState full = World(Uniform(1000), Single(YoungAdult, 20), Single(YoungAdult, 20));
        Deficit(full, 0, 1.0);
        for (int d = 1; d <= 2; d++) { Vacant(full, d, demand: 100, dtPrev: 10.0, harvest: 100); Link(full, 0, d, 1.0); }
        MigrationPlan pb = PlanOf(full, cfg);
        Assert.True(pb.ExitOpenness[0] > 0.0);
        Assert.Equal(0.0, pb.Vacancy[1]); Assert.Equal(0.0, pb.Vacancy[2]);
        Assert.Equal(0.0, pb.VacancyScale[1]); Assert.Equal(0.0, pb.VacancyScale[2]);
        WorldState nb = MigrationOnly(cfg).Step(full);
        Assert.Equal(0, nb.MigrationFlows[0].Outflow);
        Assert.Equal(16_000, Pop(nb, 0));
        for (int i = 0; i < nb.Buckets.Count; i++) Assert.Equal(0.0, nb.Buckets[i].UnplacedDeparture);
    }

    // === small population: flooring and remainders ==============================

    [Theory]
    [InlineData(3)]
    [InlineData(7)]
    [InlineData(20)]
    public void M_SmallSettlement_FlooringAndRemainders(long persons)
    {
        // A FAMINE source of 3, 7 or 20 (one adult bucket, d = 1) next to one viable
        // destination at damping ½ (cost = D·ln 2 ⇒ ω ≈ 0.5). Per turn the moved
        // count is EXACTLY floor(φ × count + remainder) with the remainder carried in
        // [0,1); people are conserved; at least one leaves within ⌈1/φ⌉ turns; the
        // bucket never goes negative and the 3-person source never moves more than 3.
        SimConfig cfg = Cfg();
        WorldState w = World(Single(YoungAdult, persons), Single(YoungAdult, 10));
        Endow(w, 1, 50_000);
        Deficit(w, 0, 1.0);
        Link(w, 0, 1, cfg.Migration.DampingDecayCostUnits * Math.Log(2.0));
        int row = BucketRow(w, 0, YoungAdult);
        TurnExecutor exec = MigrationOnly(cfg);

        long cumulative = 0;
        int firstLeaverTurn = -1;
        double firstPhi = 0.0;
        for (int turn = 1; turn <= 6; turn++)
        {
            MigrationPlan plan = PlanOf(w, cfg);
            Assert.True(Math.Abs(plan.ExitOpenness[0] - 0.5) < 1e-12, $"ω = {plan.ExitOpenness[0]}");
            double phi = plan.FlightFraction[row];
            if (turn == 1) firstPhi = phi;
            long before = w.Buckets[row].Count.Value;
            double remBefore = w.Buckets[row].MigrationRemainder;
            long expected = (long)Math.Floor(phi * before * plan.FlightWeight[0, 1] + remBefore);

            WorldState next = exec.Step(w);
            long moved = next.MigrationFlows[0].Outflow;
            Assert.Equal(expected, moved);
            Assert.True(next.Buckets[row].MigrationRemainder >= 0.0 && next.Buckets[row].MigrationRemainder < 1.0,
                $"remainder {next.Buckets[row].MigrationRemainder} outside [0,1)");
            Assert.True(next.Buckets[row].Count.Value >= 0, "negative bucket");
            Assert.Equal(persons + 10, PopAll(next));
            cumulative += moved;
            if (moved > 0 && firstLeaverTurn < 0) firstLeaverTurn = turn;
            w = next;
        }
        Assert.True(cumulative <= persons);
        int within = (int)Math.Ceiling(1.0 / firstPhi);
        Assert.True(firstLeaverTurn > 0 && firstLeaverTurn <= within,
            $"first leaver at turn {firstLeaverTurn}, expected within ⌈1/φ⌉ = {within}");
    }

    // === readout value, turn-exact ==============================================

    [Theory]
    [InlineData(0.40)]
    [InlineData(0.15)]
    public void M_Readout_ValueTurnExact(double d)
    {
        // A stranded source (its only neighbour is a food-less ruin): the readout on
        // the very turn the deficit is in force is (1 − e^{−profile·K·d·dt}) × count
        // per bucket — the destination-free hazard, ω := 1 — bit for bit, at d = 0.40
        // and at d = 0.15 (a STRESS settlement founds at ordinary-pressure magnitude);
        // and it is strictly below the bucket, so no founding empties a bucket.
        SimConfig cfg = Cfg();
        WorldState w = World(Uniform(1000), Uniform(10));
        Deficit(w, 0, d);
        Link(w, 0, 1, 5.0);                      // reachable, but settlement 1 has no food

        WorldState next = MigrationOnly(cfg).Step(w);
        Assert.Equal(0, next.MigrationFlows[0].Outflow);
        double total = 0.0;
        for (int c = 0; c < Cohorts.Count; c++)
        {
            int row = BucketRow(w, 0, c);
            double expected = MigrationSystem.FlightFractionOf(cfg.Migration.CohortProfile[c], KFlight(cfg), 1.0, d, 10.0) * 1000.0;
            Assert.Equal(expected, next.Buckets[row].UnplacedDeparture);
            Assert.True(expected > 0.0 && expected < 1000.0);
            total += expected;
        }
        Assert.True(total > 0.0);
        // Not the pre-amendment linear value (0.24 × profile × d × 10 × count).
        double linear = cfg.Migration.BaseRatePerYear * cfg.Migration.CohortProfile[YoungAdult] * 1000.0 * 10.0
                        * cfg.Migration.FamineFlightFactor * d;
        Assert.NotEqual(linear, next.Buckets[BucketRow(w, 0, YoungAdult)].UnplacedDeparture);
    }

    // === exact integration: dt composes =========================================

    [Fact]
    public void M_Flight_DtComposes()
    {
        // At held (ω, d) one dt = 10 step moves what two dt = 5 steps move, within
        // flooring: (1 − φ₅)² = 1 − φ₁₀ exactly for the hazard form (the Euler form
        // gives 2.4 vs 1.2 + 1.2 × (1 − 1.2) — a different number). No land ⇒ no
        // gap channel; migration alone holds the deficit row.
        SimConfig cfg = Cfg();
        WorldState Rig()
        {
            WorldState w = World(Uniform(1000), Uniform(10));
            Endow(w, 1, 50_000); Deficit(w, 0, 1.0); Link(w, 0, 1, 10.0);
            return w;
        }
        MigrationPlan p10 = PlanOf(Rig(), cfg, 10.0), p5 = PlanOf(Rig(), cfg, 5.0);
        int row = BucketRow(Rig(), 0, YoungAdult);
        double phi10 = p10.FlightFraction[row], phi5 = p5.FlightFraction[row];
        Assert.Equal(1.0 - phi10, (1.0 - phi5) * (1.0 - phi5), 12);

        long one = MigrationOnly(cfg, 10.0).Step(Rig()).MigrationFlows[0].Outflow;
        WorldState half = MigrationOnly(cfg, 5.0).Step(Rig());
        long two = half.MigrationFlows[0].Outflow + MigrationOnly(cfg, 5.0).Step(half).MigrationFlows[0].Outflow;
        Assert.True(one > 0 && two > 0);
        Assert.True(Math.Abs(one - two) <= 2 * Cohorts.Count, $"dt-10 moved {one}, two dt-5 steps moved {two}");

        // The vacancy bound composes the same way on a held-N_lim rig: cap₁₀ = (1 −
        // e^{−10k}) V versus cap₅ V then cap₅ (V − accepted) — the same relaxation
        // law births use, integrated exactly.
        WorldState Vac()
        {
            WorldState w = World(Single(YoungAdult, 1000), Single(YoungAdult, 10));
            Vacant(w, 1, demand: 100, dtPrev: 10.0, harvest: 800);
            Deficit(w, 0, 1.0); Link(w, 0, 1, 0.0);
            return w;
        }
        long oneV = MigrationOnly(cfg, 10.0).Step(Vac()).MigrationFlows[1].Inflow;
        WorldState halfV = MigrationOnly(cfg, 5.0).Step(Vac());
        long twoV = halfV.MigrationFlows[1].Inflow + MigrationOnly(cfg, 5.0).Step(halfV).MigrationFlows[1].Inflow;
        Assert.True(oneV > 0 && twoV > 0);
        Assert.True(Math.Abs(oneV - twoV) <= 2, $"vacancy: dt-10 accepted {oneV}, two dt-5 steps accepted {twoV}");
    }

    // === recovery ================================================================

    [Fact]
    public void M_Recovery_FlowsStopWhenDeficitClears()
    {
        // Turn 1: a famine source (d = 1) with a land gap toward a viable, CAPACITY-
        // BOUNDED destination — both channels flow and the vacancy bound refuses
        // most. Turn 2 with d back to 0: flight is 0 (φ = 0 per bucket, FlightOut 0),
        // the gap channel resumes on its own, and nothing refused on turn 1 was
        // banked anywhere (every remainder < 1).
        SimConfig cfg = Cfg();
        WorldState w = World(Uniform(1000), Single(YoungAdult, 10));   // 10 adult heads ⇒ V = 70, cap 35
        Land(w, 1, 128_000.0);
        Vacant(w, 1, demand: 100, dtPrev: 10.0, harvest: 800);
        Deficit(w, 0, 1.0);
        Link(w, 0, 1, 5.0);

        MigrationPlan p1 = PlanOf(w, cfg);
        Assert.True(p1.FlightOut[0] > 0.0 && p1.GapOut[0] > 0.0, "turn 1 does not use both channels");
        Assert.True(p1.VacancyScale[1] < 1.0, "the rig does not refuse anybody");
        WorldState turn1 = MigrationOnly(cfg).Step(w);
        Assert.True(turn1.MigrationFlows[0].Outflow > 0);
        for (int i = 0; i < turn1.Buckets.Count; i++)
            Assert.True(turn1.Buckets[i].MigrationRemainder < 1.0, $"a refused pool was banked in bucket {i}");

        Deficit(turn1, 0, 0.0);
        MigrationPlan p2 = PlanOf(turn1, cfg);
        Assert.Equal(0.0, p2.FlightOut[0]);
        foreach (int row in p2.BucketRows[0]) Assert.Equal(0.0, p2.FlightFraction[row]);
        Assert.True(p2.GapOut[0] > 0.0, "the gap channel did not resume");
        WorldState turn2 = MigrationOnly(cfg).Step(turn1);
        long out2 = turn2.MigrationFlows[0].Outflow;
        Assert.True(out2 > 0);
        Assert.True(Math.Abs(out2 - p2.GapOut[0]) <= Cohorts.Count, $"turn-2 outflow {out2} vs planned gap {p2.GapOut[0]}");
    }

    // === the planner IS what the loop executes ====================================

    [Fact]
    public void M_Plan_EqualsStep()
    {
        // Three rigs — gap only, flight only, both with every bound biting — and in
        // each the per-settlement, per-channel totals the planner records are what
        // the transfer loop delivered, within the per-bucket floors (a remainder is
        // carried across a source bucket's destinations, so each destination's
        // executed inflow sits within the number of contributing buckets of the plan).
        SimConfig cfg = Cfg();
        void Check(WorldState w, bool expectGap, bool expectFlight)
        {
            MigrationPlan plan = PlanOf(w, cfg);
            WorldState next = MigrationOnly(cfg).Step(w);
            int n = w.Settlements.Count;
            double gapIn = 0.0, gapOut = 0.0, flightIn = 0.0, flightOut = 0.0;
            long executed = 0;
            for (int s = 0; s < n; s++)
            {
                gapIn += plan.GapIn[s]; gapOut += plan.GapOut[s];
                flightIn += plan.FlightIn[s]; flightOut += plan.FlightOut[s];
                long inflow = next.MigrationFlows[s].Inflow, outflow = next.MigrationFlows[s].Outflow;
                executed += inflow;
                int contributing = w.Buckets.Count;                     // ≤ every bucket that could floor
                Assert.True(Math.Abs(inflow - (plan.GapIn[s] + plan.FlightIn[s])) <= contributing,
                    $"settlement {s}: executed inflow {inflow} vs planned {plan.GapIn[s] + plan.FlightIn[s]}");
                Assert.True(Math.Abs(outflow - (plan.GapOut[s] + plan.FlightOut[s])) <= Cohorts.Count,
                    $"settlement {s}: executed outflow {outflow} vs planned {plan.GapOut[s] + plan.FlightOut[s]}");
                if (!expectGap) { Assert.Equal(0.0, plan.GapIn[s]); Assert.Equal(0.0, plan.GapOut[s]); }
                if (!expectFlight) { Assert.Equal(0.0, plan.FlightIn[s]); Assert.Equal(0.0, plan.FlightOut[s]); }
            }
            Assert.Equal(gapOut, gapIn, 6);
            Assert.Equal(flightOut, flightIn, 6);
            Assert.True(executed > 0, "vacuous rig");
            if (expectGap) Assert.True(gapIn > 0.0);
            if (expectFlight) Assert.True(flightIn > 0.0);
        }

        // Gap only: d = 0 everywhere, a land gap, two rich destinations.
        WorldState gap = World(Uniform(2000), Uniform(10), Uniform(10));
        for (int d = 1; d <= 2; d++) { Land(gap, d, 128_000.0); Endow(gap, d, 500_000); Link(gap, 0, d, 20.0); }
        Check(gap, expectGap: true, expectFlight: false);

        // Flight only: no land, d = 0.5, two viable destinations, one capacity-bounded.
        WorldState flight = World(Uniform(1000), Uniform(10), Single(YoungAdult, 10));
        Endow(flight, 1, 50_000); Vacant(flight, 2, demand: 100, dtPrev: 10.0, harvest: 800);
        Deficit(flight, 0, 0.5); Link(flight, 0, 1, 10.0); Link(flight, 0, 2, 10.0);
        Assert.True(PlanOf(flight, cfg).VacancyScale[2] < 1.0);
        Check(flight, expectGap: false, expectFlight: true);

        // Both: two famine sources with land facing one rich, capacity-bounded
        // destination and one rich unbounded one — pair caps, basin caps, vacancy.
        double land = 10_000.0 / cfg.Migration.AttractivenessLandWeight;
        WorldState both = World(Uniform(2000), Uniform(2000), Uniform(10), Uniform(10));
        for (int s = 0; s <= 1; s++) { Land(both, s, land); Deficit(both, s, 0.6); Link(both, s, 2, 20.0); Link(both, s, 3, 20.0); }
        for (int d = 2; d <= 3; d++) { Land(both, d, land); Endow(both, d, 500_000); }
        Vacant(both, 2, demand: 1000, dtPrev: 10.0, harvest: 8000);
        MigrationPlan pb = PlanOf(both, cfg);
        Assert.True(pb.DestScale[2] < 1.0 || pb.DestScale[3] < 1.0, "no basin cap bites");
        Assert.True(pb.VacancyScale[2] < 1.0, "no vacancy bound bites");
        Check(both, expectGap: true, expectFlight: true);
    }
}
