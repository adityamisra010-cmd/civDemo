using Sim.Core;
using Sim.Core.Kernel;
using Sim.Core.State;
using Sim.Core.Systems;
using Sim.Core.Systems.Consumption;
using Sim.Core.Systems.Demographics;
using Sim.Tests.TestUtil;

namespace Sim.Tests.Systems;

/// <summary>
/// T4.21-3 (CR-015 "famine is exceptional", ADR-026, spec §6.4 D-*): the
/// demographic shock integration — the exceptional channels read the
/// EFFECTIVE deficit, and growth is capped by the food-influx headroom.
/// Every rig is DemographicsSystem-only unless stated, checked against the
/// ADR-011 replica fed the same per-turn scalars the kernel derives.
/// </summary>
public class DemographicShockTests
{
    private static readonly SettlementId S0 = new(0);

    private static EraTable FlatEra(double dtYears) => EraTableLoader.Load(
        $$"""{ "bands": [ { "name": "flat", "startYear": 0, "endYear": 100000, "dtYears": {{dtYears.ToString(System.Globalization.CultureInfo.InvariantCulture)}} } ] }""");

    private static long Floor(double v) => (long)Math.Floor(v);

    private static long FlowTotal(WorldState world, ReasonId reason, bool sunk)
    {
        for (int i = 0; i < world.LedgerFlows.Count; i++)
        {
            LedgerFlowRow row = world.LedgerFlows[i];
            if (row.Quantity == ConservedQuantityIds.Population && row.Reason == reason)
                return sunk ? row.TotalSunk : row.TotalSourced;
        }
        return 0;
    }

    /// <summary>The legacy 0 %-farm LaborAllocation order's row — exactly what
    /// PathBuildSystem writes for pct = 0 (Farming 0, Herding 0, Construction 1.0):
    /// food labour deliberately abandoned, FAMINE whenever d &gt; 0.</summary>
    private static SectorAllocationRow Abandoned(SettlementId s) =>
        new(s, Farming: 0.0, Herding: 0.0, Extraction: 0.0, Crafting: 0.0, Construction: 1.0);

    /// <summary>Starvation-only config (fertility and base mortality zeroed):
    /// the starvation flow is the isolated observable.</summary>
    private static SimConfig StarvationOnly(SimConfig cfg) => cfg with
    {
        Demographics = cfg.Demographics with
        {
            FertilityPerPersonPerYear = new double[Cohorts.Count],
            MortalityPerYear = new double[Cohorts.Count],
        },
    };

    /// <summary>Fertility-only config (mortality and starvation zeroed; the
    /// fertile pool parked in the absorbing 75+ cohort so it never ages away).</summary>
    private static SimConfig FertilityOnly(SimConfig cfg)
    {
        var fertility = new double[Cohorts.Count];
        fertility[15] = 0.1;
        return cfg with
        {
            Demographics = cfg.Demographics with
            {
                FertilityPerPersonPerYear = fertility,
                MortalityPerYear = new double[Cohorts.Count],
                StarvationMortalityMaxPerYear = 0.0,
            },
        };
    }

    private static WorldState AbsorbingRig(long count, double deficit, bool abandoned)
    {
        var counts = new long[Cohorts.Count];
        counts[15] = count;
        WorldState w = PopulationExactnessTests.BucketWorld(counts);
        w.ConsumptionDeficits.Add(new ConsumptionDeficitRow(S0, deficit));
        if (abandoned) w.SectorAllocations.Add(Abandoned(S0));
        return w;
    }

    // --- adaptation: the exceptional channels read the EFFECTIVE deficit ----

    [Fact]
    public void D_Starvation_ReadsEffectiveDeficit()
    {
        // KILLS M-DEM-ADAPT (StarvationRate reads the nominal deficit): the
        // STRESS rig would starve 0.12 × 0.15 × 10 ≈ 16 % of the bucket.
        // One absorbing cohort (one row, no aging, no carried remainder), so
        // the starvation flow is EXACTLY the floor of the replica's exact
        // amount — exact long equality, no per-row flooring band.
        SimConfig cfg = StarvationOnly(TestConfigs.Sim());
        const double dt = 10.0;
        const long n = 100_000;
        var exec = new TurnExecutor(FlatEra(dt), [SystemCatalog.Demographics(cfg)]);
        var counts = new long[Cohorts.Count];
        counts[15] = n;

        // (a) STRESS: d = 0.15 ≤ a — adaptation absorbs the whole cut; the
        //     starvation hazard is identically zero. Semantic, not a tolerance.
        WorldState stress = AbsorbingRig(n, 0.15, abandoned: false);
        Assert.Equal(FoodStateKind.Stress, FoodState.Of(stress, S0, cfg, out _));
        WorldState stressNext = exec.Step(stress);
        Assert.Equal(0, FlowTotal(stressNext, ReasonIds.Starvation, sunk: true));
        Assert.Equal(n, stressNext.Buckets[15].Count.Value);
        Assert.Equal(0.0, stressNext.Buckets[15].StarvationRemainder);
        // ... and NOT the nominal response (teeth against M-DEM-ADAPT).
        DemographicsReplica.Result nominalStress = DemographicsReplica.Turn(cfg.Demographics, counts, 0.15, dt);
        Assert.True(Floor(nominalStress.Starved) > 10_000, "nominal-deficit replica starves nobody — teeth vacuous");

        // (b) SEVERE: d = 0.4 > a — the unabsorbed remainder (0.4 − 0.2)/0.8 =
        //     0.25 starves; flow == replica at d_eff = 0.25, != replica at 0.4.
        WorldState severe = AbsorbingRig(n, 0.4, abandoned: false);
        Assert.Equal(FoodStateKind.Severe, FoodState.Of(severe, S0, cfg, out _));
        double dEff = PopulationExactnessTests.EffectiveDeficit(severe, cfg, 0.4);
        Assert.Equal(0.25, dEff);
        WorldState severeNext = exec.Step(severe);
        DemographicsReplica.Result adapted = DemographicsReplica.Turn(cfg.Demographics, counts, 0.4, dt, dEff: 0.25);
        DemographicsReplica.Result nominalSevere = DemographicsReplica.Turn(cfg.Demographics, counts, 0.4, dt);
        long severeFlow = FlowTotal(severeNext, ReasonIds.Starvation, sunk: true);
        Assert.Equal(Floor(adapted.Starved), severeFlow);
        Assert.NotEqual(Floor(nominalSevere.Starved), severeFlow);
        Assert.True(severeFlow > 0, "SEVERE starved nobody — adapted channel inert");

        // (c) FAMINE by abandonment at the SAME d = 0.4: no adaptation — the
        //     whole deficit starves; flow == replica at 0.4 exactly.
        WorldState famine = AbsorbingRig(n, 0.4, abandoned: true);
        Assert.Equal(FoodStateKind.Famine, FoodState.Of(famine, S0, cfg, out FamineReason reason));
        Assert.Equal(FamineReason.Abandonment, reason);
        Assert.Equal(0.4, PopulationExactnessTests.EffectiveDeficit(famine, cfg, 0.4));
        WorldState famineNext = exec.Step(famine);
        long famineFlow = FlowTotal(famineNext, ReasonIds.Starvation, sunk: true);
        Assert.Equal(Floor(nominalSevere.Starved), famineFlow);
        Assert.True(famineFlow > severeFlow, "FAMINE did not starve more than SEVERE at the same deficit");
    }

    [Fact]
    public void D_Suppression_ReadsEffectiveDeficit()
    {
        // G3(b): fertility suppression reads the SAME effective deficit as
        // starvation outside FAMINE, the nominal one inside it (where they are
        // equal). Fertility-only absorbing rig, dt 2.5: births are the floor
        // of the replica's exact amount and the reservoir is BIT-exact.
        SimConfig cfg = FertilityOnly(TestConfigs.Sim());
        const double dt = 2.5;
        const long n = 1000;
        var exec = new TurnExecutor(FlatEra(dt), [SystemCatalog.Demographics(cfg)]);
        var counts = new long[Cohorts.Count];
        counts[15] = n;

        // STRESS d = 0.15: births unsuppressed (0.1 × 1000 × 2.5 = 250 exactly:
        // λ = 0 so W = 1), NOTHING banked — no suppressed conception to defer.
        WorldState stress = exec.Step(AbsorbingRig(n, 0.15, abandoned: false));
        DemographicsReplica.Result rStress = DemographicsReplica.Turn(cfg.Demographics, counts, 0.15, dt, dEff: 0.0, suppressionArg: 0.0);
        Assert.Equal(250, FlowTotal(stress, ReasonIds.Births, sunk: false));
        Assert.Equal(Floor(rStress.Births), FlowTotal(stress, ReasonIds.Births, sunk: false));
        Assert.Equal(0.0, stress.Buckets[0].ReboundReservoir);
        Assert.Equal(rStress.Reservoir, stress.Buckets[0].ReboundReservoir);

        // SEVERE d = 0.4: suppression 1 − 3.0 × 0.25 = 0.25 (not 1 − 1.2 → 0).
        WorldState severe = exec.Step(AbsorbingRig(n, 0.4, abandoned: false));
        DemographicsReplica.Result rSevere = DemographicsReplica.Turn(cfg.Demographics, counts, 0.4, dt, dEff: 0.25, suppressionArg: 0.25);
        DemographicsReplica.Result rNominal = DemographicsReplica.Turn(cfg.Demographics, counts, 0.4, dt);
        Assert.Equal(Floor(rSevere.Births), FlowTotal(severe, ReasonIds.Births, sunk: false));
        Assert.Equal(rSevere.Reservoir, severe.Buckets[0].ReboundReservoir);
        Assert.True(FlowTotal(severe, ReasonIds.Births, sunk: false) > Floor(rNominal.Births),
            "SEVERE births not above the nominal (full-stop) response");
        Assert.True(FlowTotal(severe, ReasonIds.Births, sunk: false) < 250, "SEVERE did not suppress at all");

        // FAMINE d = 0.4 (abandonment): the whole deficit — the 1/3 full stop.
        WorldState famine = exec.Step(AbsorbingRig(n, 0.4, abandoned: true));
        Assert.Equal(0, FlowTotal(famine, ReasonIds.Births, sunk: false));
        Assert.Equal(rNominal.Reservoir, famine.Buckets[0].ReboundReservoir);
        Assert.True(famine.Buckets[0].ReboundReservoir > severe.Buckets[0].ReboundReservoir,
            "FAMINE banked no more than SEVERE at the same deficit");
    }

    [Fact]
    public void DtInvariance_StarvationFlow_ExactAcrossDts()
    {
        // The existing dt pin runs StarvationMax = 0 and cannot see a dt leak
        // in the effective-deficit path. d = 0.5 (SEVERE, d_eff = 0.375) on
        // one absorbing cohort over the same 20 sim-years at dt 10 / 5 / 2.5:
        // the per-turn scalar composes through e^(−s·h), so the cumulative
        // starvation flow agrees within the per-turn integer floor (±1) and
        // the survivors match the closed form N·e^(−s·20) within it.
        SimConfig cfg = StarvationOnly(TestConfigs.Sim());
        const long n = 1_000_000;
        const int horizonYears = 20;
        double s = cfg.Demographics.StarvationMortalityMaxPerYear * 0.375 * cfg.Demographics.StarvationElderMultiplier;
        double[] dts = [10.0, 5.0, 2.5];
        var flows = new long[dts.Length];
        for (int i = 0; i < dts.Length; i++)
        {
            var exec = new TurnExecutor(FlatEra(dts[i]), [SystemCatalog.Demographics(cfg)]);
            WorldState w = exec.Run(AbsorbingRig(n, 0.5, abandoned: false), (int)(horizonYears / dts[i]));
            flows[i] = FlowTotal(w, ReasonIds.Starvation, sunk: true);
            Assert.Equal(n - flows[i], w.Buckets[15].Count.Value);
        }
        Assert.True(flows[0] > 100_000, "starvation inert — invariance vacuous");
        Assert.True(Math.Abs(flows[0] - flows[1]) <= 1, $"dt10 {flows[0]} vs dt5 {flows[1]}");
        Assert.True(Math.Abs(flows[1] - flows[2]) <= 1, $"dt5 {flows[1]} vs dt2.5 {flows[2]}");
        long closedForm = Floor(n * (1.0 - Math.Exp(-s * horizonYears)));
        Assert.True(Math.Abs(flows[0] - closedForm) <= 2, $"dt10 {flows[0]} vs closed form {closedForm}");
    }

    // =====================================================================
    // The headroom growth cap (spec §3.6b (ii), ADR-026 §2.2(ii), §6.4)
    // =====================================================================

    private static BasketBook Book(SimConfig cfg) => new(cfg.Needs!, cfg.Goods!);

    private static double Limit(WorldState prev, SimConfig cfg) =>
        FoodHeadroom.Limit(prev, S0, cfg.Consumption.CohortWeights, Book(cfg));

    private static double Nutrition(WorldState world, SimConfig cfg)
    {
        double n = 0.0;
        for (int i = 0; i < world.Buckets.Count; i++)
        {
            if (world.Buckets[i].Settlement != S0) continue;
            n += cfg.Consumption.CohortWeights[world.Buckets[i].CohortIdx] * world.Buckets[i].Count.Value;
        }
        return n;
    }

    private static long[] Counts(WorldState world)
    {
        var counts = new long[Cohorts.Count];
        for (int i = 0; i < world.Buckets.Count; i++)
            if (world.Buckets[i].Settlement == S0) counts[world.Buckets[i].CohortIdx] += world.Buckets[i].Count.Value;
        return counts;
    }

    private static long[] Scaled(SimConfig cfg, long factor)
    {
        var counts = new long[Cohorts.Count];
        for (int c = 0; c < Cohorts.Count; c++) counts[c] = cfg.Founding.CohortCounts[c] * factor;
        return counts;
    }

    private static double MaxWeight(SimConfig cfg)
    {
        double m = 0.0;
        foreach (double w in cfg.Consumption.CohortWeights) m = Math.Max(m, w);
        return m;
    }

    /// <summary>The integer-reconciliation band around the kernel's exact
    /// double state: births floor with a carried remainder (one flow), deaths
    /// and starvation floor per row (two sink flows), so an integer total sits
    /// within (1 + 2 × rows) heads of the exact trajectory — the cap is a
    /// contract on the double state, and this is what integers can add.</summary>
    private static double FlooringBand(WorldState world, SimConfig cfg) =>
        (1 + 2 * Counts(world).Length) * MaxWeight(cfg);

    /// <summary>One demographics-only replica turn fed the kernel's own
    /// scalars: the effective deficit from the world's classification, N_lim
    /// from FoodHeadroom on the same prev, the canonical weights and k.</summary>
    private static DemographicsReplica.Result ReplicaTurn(WorldState prev, SimConfig cfg, double dt, double reservoir)
    {
        double deficit = FoodState.DeficitRatio(prev, S0);
        double dEff = PopulationExactnessTests.EffectiveDeficit(prev, cfg, deficit);
        return DemographicsReplica.Turn(cfg.Demographics, Counts(prev), deficit, dt, reservoir,
            dEff: dEff, suppressionArg: dEff, nLim: Limit(prev, cfg),
            cohortWeights: cfg.Consumption.CohortWeights,
            headroomRelaxationPerYear: cfg.Demographics.HeadroomRelaxationPerYear);
    }

    /// <summary>A demographics-only world whose PREV rows make the cap READ:
    /// a deficit row with d = 0 and a positive DemandUnits, a vitals row at dt,
    /// and (unless <paramref name="stapleProduced"/> is null) a grain stock row
    /// whose LastProducedUnits is the influx S — N_lim = S / dt. With no grain
    /// row S = 0 and N_lim = 0 (an abandoned settlement on its granary).
    /// T4.21-4 (RULE 2): the rig carries a CATCHMENT SUMMARY ROW with arable > 0.
    /// That is the whole point of the new null arm's key — row ABSENCE means the
    /// influx was never measured (+∞), while a settlement that HAS a catchment and
    /// still produced no staple is the abandoned settlement whose zero influx is
    /// genuine and must keep N_lim = 0. CatchmentSystem emits one row per settlement
    /// in `prev.Settlements` regardless of labour or sectors, so a real abandoned
    /// settlement always has one; this rig now matches that world.</summary>
    private static WorldState CappedRig(SimConfig cfg, long[] counts, double dt, long? stapleProduced, double deficit = 0.0)
    {
        WorldState w = PopulationExactnessTests.BucketWorld(counts);
        w.CatchmentSummaries.Add(new CatchmentSummaryRow(
            S0, NodeCount: 1, EffectiveArableKm2: 5000.0, NetworkRevision: 0, LastRecomputeTurn: 0));
        w.ConsumptionDeficits.Add(new ConsumptionDeficitRow(S0, deficit, 1_000_000));
        w.SettlementVitals.Add(new SettlementVitalsRow(S0, 0, 0, dt));
        if (stapleProduced is long s)
        {
            w.GoodStocks.Add(new GoodStockRow(S0, new GoodId(cfg.Goods!.GrainId), Conserved.Zero, 0.0, 0.0,
                lastProducedUnits: s, lastConsumptionDemandUnits: 0));
        }
        return w;
    }

    /// <summary>The LAND-BOUND food-loop rig: one settlement, a fixed catchment
    /// (arable km² × yieldPerArableKm2PerYear is the harvest, labour never
    /// binds), an all-farming sector row unless <paramref name="row"/> is given,
    /// a grain stock row, no weather, no disaster — run through
    /// [Production, Consumption, Demographics]. With <paramref name="livestockDeposit"/>
    /// a livestock deposit and stock row exist so the herding share of a
    /// Default-mix row produces a NON-STAPLE surplus.</summary>
    private static WorldState LandBoundWorld(
        SimConfig cfg, long[] counts, double arableKm2, long store = 0,
        SectorAllocationRow? row = null, double? livestockDeposit = null)
    {
        WorldState w = PopulationExactnessTests.BucketWorld(counts);
        w.CatchmentSummaries.Add(new CatchmentSummaryRow(
            S0, NodeCount: 1, EffectiveArableKm2: arableKm2, NetworkRevision: 0, LastRecomputeTurn: 0));
        int grain = w.GoodStocks.Add(new GoodStockRow(S0, new GoodId(cfg.Goods!.GrainId), Conserved.Zero, 0.0, 0.0));
        if (store > 0)
        {
            new Ledger(w.LedgerFlows).Flow(ref w.GoodStocks.Ref(grain).Amount,
                ConservedQuantityIds.OfGood(new GoodId(cfg.Goods.GrainId)), ReasonIds.InitialEndowment, store,
                FlowDirection.Source, OverdrawPolicy.Throw);
        }
        w.SectorAllocations.Add(row ?? new SectorAllocationRow(
            S0, Farming: 1.0, Herding: 0.0, Extraction: 0.0, Crafting: 0.0, Construction: 0.0));
        if (livestockDeposit is double abundance)
        {
            var livestock = new GoodId(cfg.Goods.IdOf("livestock"));
            w.GoodStocks.Add(new GoodStockRow(S0, livestock, Conserved.Zero, 0.0, 0.0));
            w.Deposits.Add(new DepositRow(S0, livestock, abundance));
        }
        return w;
    }

    private static TurnExecutor FoodLoop(SimConfig cfg, double dt) => new(FlatEra(dt),
        [SystemCatalog.Production(cfg), SystemCatalog.Consumption(cfg), SystemCatalog.Demographics(cfg)]);

    private static void AssertBucketsBitIdentical(WorldState a, WorldState b)
    {
        Assert.Equal(a.Buckets.Count, b.Buckets.Count);
        for (int i = 0; i < a.Buckets.Count; i++)
        {
            BucketRow x = a.Buckets[i], y = b.Buckets[i];
            Assert.Equal(x.Count.Value, y.Count.Value);
            Assert.Equal(BitConverter.DoubleToInt64Bits(x.BirthRemainder), BitConverter.DoubleToInt64Bits(y.BirthRemainder));
            Assert.Equal(BitConverter.DoubleToInt64Bits(x.DeathRemainder), BitConverter.DoubleToInt64Bits(y.DeathRemainder));
            Assert.Equal(BitConverter.DoubleToInt64Bits(x.StarvationRemainder), BitConverter.DoubleToInt64Bits(y.StarvationRemainder));
            Assert.Equal(BitConverter.DoubleToInt64Bits(x.AgingRemainder), BitConverter.DoubleToInt64Bits(y.AgingRemainder));
            Assert.Equal(BitConverter.DoubleToInt64Bits(x.ReboundReservoir), BitConverter.DoubleToInt64Bits(y.ReboundReservoir));
        }
    }

    [Fact]
    public void D_CohortWeights_NewbornAgingWeightNeutral()
    {
        // The cap's bornMax converts newborn survivors to adult-equivalents
        // with w_0 ALONE. Newborns age h/width into cohort 1 within the same
        // micro-step, so that is exact ONLY because cohortWeights[0] ==
        // cohortWeights[1] — a DATA fact this test pins. The replica carries
        // the general term advance × (w_1 − w_0), which evaluates to exactly 0
        // here; a future weight change breaks replica equality (bounded,
        // visible), never the kernel silently.
        SimConfig cfg = TestConfigs.Sim();
        double[] w = cfg.Consumption.CohortWeights;
        Assert.Equal(w[0], w[1]);
        Assert.Equal(0.0, DemographicsReplica.NewbornAgingWeightTerm(w, DemographicsSystem.MicroStepYears / Cohorts.WidthYears));
        Assert.True(w[0] > 0.0, "a zero newborn weight would make bornMax undefined");
    }

    [Fact]
    public void D_Cap_Identity_LargeHeadroom()
    {
        // With H_0 ≫ a step's growth, m = min(1.0, big) is the LITERAL 1.0 and
        // the guarded scaling is never entered: the wired cap executes the
        // pre-T4.21 instruction sequence. Twin worlds, identical people and
        // config, differing only in whether the cap READS — the skipped arm
        // has no deficit row (N_lim = +∞, the cap compiled out of the path);
        // the wired arm carries a demand row, a vitals row and an absurd
        // staple influx (N_lim = 10⁸ ae) — must agree BIT FOR BIT on every
        // bucket field and every vitals row over 30 canonical turns.
        SimConfig cfg = TestConfigs.Sim();
        const double dt = 10.0;
        long[] counts = Scaled(cfg, 10);
        WorldState skipped = PopulationExactnessTests.BucketWorld(counts);
        WorldState wired = CappedRig(cfg, counts, dt, stapleProduced: 1_000_000_000);
        Assert.Equal(double.PositiveInfinity, Limit(skipped, cfg));
        Assert.Equal(1e8, Limit(wired, cfg));
        var exec = new TurnExecutor(FlatEra(dt), [SystemCatalog.Demographics(cfg)]);
        for (int t = 1; t <= 30; t++)
        {
            skipped = exec.Step(skipped);
            wired = exec.Step(wired);
            AssertBucketsBitIdentical(skipped, wired);
            Assert.Equal(skipped.SettlementVitals[0], wired.SettlementVitals[0]);
            Assert.True(double.IsFinite(Limit(wired, cfg)), "the wired arm stopped reading a finite limit");
        }
        Assert.True(Nutrition(wired, cfg) > 1.1 * Nutrition(PopulationExactnessTests.BucketWorld(counts), cfg),
            "no growth in 300 years — identity vacuous");
    }

    [Fact]
    public void D_Cap_NoGrowthOnAGranary()
    {
        // KILLS M-DEM-CAP (m := 1): a stockpile is not an influx. An abandoned
        // settlement living on its granary — demand row with d = 0, NO food
        // produced last turn (S = 0) — has N_lim = 0 ⇒ H_0 = 0 ⇒ births
        // replace deaths in adult-equivalents, step by step: over 20 turns the
        // nutritional population holds (up to the aging-drift term the replica
        // computes and the integer band), while the uncapped twin grows. Every
        // turn is replica-exact: births to the person (one anchor row), the
        // reservoir bit for bit.
        SimConfig cfg = TestConfigs.Sim();
        const double dt = 10.0;
        long[] counts = Scaled(cfg, 100);
        WorldState world = CappedRig(cfg, counts, dt, stapleProduced: null);
        WorldState twin = PopulationExactnessTests.BucketWorld(counts);
        Assert.Equal(0.0, Limit(world, cfg));
        double n0 = Nutrition(world, cfg);
        double band = FlooringBand(world, cfg);
        var exec = new TurnExecutor(FlatEra(dt), [SystemCatalog.Demographics(cfg)]);
        double driftSum = 0.0;
        long ledgerBirths = 0;
        for (int t = 1; t <= 20; t++)
        {
            double reservoir = world.Buckets[0].ReboundReservoir;
            double birthRemainder = world.Buckets[0].BirthRemainder;
            DemographicsReplica.Result r = ReplicaTurn(world, cfg, dt, reservoir);
            double nStart = Nutrition(world, cfg);
            world = exec.Step(world);
            twin = exec.Step(twin);

            // Replica-exact: births == floor(exact + carried remainder); the
            // reservoir bit-exact; and the per-step identity births == deaths
            // in ae — the replica's double state moves ONLY by the drift term.
            long born = FlowTotal(world, ReasonIds.Births, sunk: false) - ledgerBirths;
            ledgerBirths += born;
            Assert.Equal(Floor(r.Births + birthRemainder), born);
            Assert.Equal(r.Reservoir, world.Buckets[0].ReboundReservoir);
            double nReplica = 0.0;
            for (int c = 0; c < Cohorts.Count; c++) nReplica += cfg.Consumption.CohortWeights[c] * r.Pop[c];
            Assert.InRange(nReplica - nStart - r.DriftBound, -1e-6 * nStart, 1e-6 * nStart);
            Assert.True(born > 0, $"turn {t}: no births at all — the cap removed the replacement births");

            driftSum += r.DriftBound;
            double n = Nutrition(world, cfg);
            Assert.True(n <= n0 + driftSum + band, $"turn {t}: N_nutr {n:F1} > N_0 {n0:F1} + drift {driftSum:F1} + band {band:F1}");
            Assert.True(n >= n0 - band, $"turn {t}: the cap removed people: {n:F1} < {n0:F1} − band");
        }
        // Teeth: the uncapped twin grew at the natural tempo (+0.76 %/decade × 20).
        Assert.True(Nutrition(twin, cfg) > 1.10 * n0, $"twin did not grow: {Nutrition(twin, cfg):F0} vs {n0:F0}");
        Assert.True(Nutrition(world, cfg) < 1.02 * n0, $"capped rig grew: {Nutrition(world, cfg):F0} vs {n0:F0}");
    }

    [Fact]
    public void D_SmallStockpile_DeficitChannelOwnsDecline()
    {
        // Abandoned rig (the pct-0 row), a store covering three quarters of
        // one decade's demand: turn 1 eats it (d = 0.25, DemandUnits and the
        // vitals row land on prev), turn 2 reads FAMINE/Abandonment at d = 0.25
        // — mortality on the WHOLE deficit (== the replica at d_eff = d, not
        // the adapted 0.0625) — and N_lim = 0 (S = 0) ⇒ H_0 = 0 ⇒ births ≤
        // deaths in ae on top of suppression. Decline comes from the deficit
        // channel only: the cap scales a birth candidate and never removes a
        // person — the standing population's sinks equal the uncapped replica's.
        SimConfig cfg = TestConfigs.Sim();
        const double dt = 10.0;
        long[] counts = Scaled(cfg, 10);
        double demandPerTurn = 0.0;
        for (int c = 0; c < Cohorts.Count; c++) demandPerTurn += cfg.Consumption.CohortWeights[c] * counts[c] * dt;
        WorldState world = LandBoundWorld(cfg, counts, arableKm2: 100.0, store: (long)(0.75 * demandPerTurn), row: Abandoned(S0));
        TurnExecutor exec = FoodLoop(cfg, dt);

        world = exec.Step(world);
        double d = world.ConsumptionDeficits[0].DeficitRatio;
        Assert.InRange(d, 0.2, 0.3);
        Assert.Equal(FoodStateKind.Famine, FoodState.Of(world, S0, cfg, out FamineReason reason));
        Assert.Equal(FamineReason.Abandonment, reason);
        Assert.Equal(0.0, Limit(world, cfg));                     // S = 0: a granary is not an influx

        long births1 = FlowTotal(world, ReasonIds.Births, sunk: false);
        long deaths1 = FlowTotal(world, ReasonIds.Deaths, sunk: true);
        long starved1 = FlowTotal(world, ReasonIds.Starvation, sunk: true);
        double reservoir = world.Buckets[0].ReboundReservoir;
        double birthRemainder = world.Buckets[0].BirthRemainder;
        long[] prevCounts = Counts(world);
        double nBefore = Nutrition(world, cfg);
        DemographicsReplica.Result famine = ReplicaTurn(world, cfg, dt, reservoir);           // d_eff = d, N_lim = 0
        DemographicsReplica.Result adapted = DemographicsReplica.Turn(cfg.Demographics, prevCounts, d, dt, reservoir,
            dEff: FoodState.EffectiveDeficit(d, FoodStateKind.Severe, cfg), suppressionArg: FoodState.EffectiveDeficit(d, FoodStateKind.Severe, cfg),
            nLim: 0.0, cohortWeights: cfg.Consumption.CohortWeights, headroomRelaxationPerYear: cfg.Demographics.HeadroomRelaxationPerYear);
        DemographicsReplica.Result uncapped = DemographicsReplica.Turn(cfg.Demographics, prevCounts, d, dt, reservoir, dEff: d, suppressionArg: d);

        world = exec.Step(world);
        long births = FlowTotal(world, ReasonIds.Births, sunk: false) - births1;
        long deaths = FlowTotal(world, ReasonIds.Deaths, sunk: true) - deaths1;
        long starved = FlowTotal(world, ReasonIds.Starvation, sunk: true) - starved1;

        Assert.Equal(Floor(famine.Births + birthRemainder), births);
        Assert.True(Math.Abs(starved - famine.Starved) < Cohorts.Count, $"starvation {starved} vs famine replica {famine.Starved:F1}");
        Assert.True(Math.Abs(deaths - famine.Deaths) < Cohorts.Count, $"deaths {deaths} vs famine replica {famine.Deaths:F1}");
        Assert.True(starved > adapted.Starved + Cohorts.Count, $"starvation {starved} is the ADAPTED response {adapted.Starved:F1}, not the famine one");
        Assert.True(births > 0, "suppression alone stopped every birth");
        // The cap is a GUARANTEE here, not a shaping force: at any d > 0 the
        // famine channels already hold births below replacement (measured:
        // the uncapped replica bears the same births), so H_0 = 0 binds
        // nothing and changes nothing — consistent with "reduce growth, never
        // cause decline". D_Cap_NoGrowthOnAGranary is where it binds.
        Assert.Equal(Floor(uncapped.Births + birthRemainder), births);
        // The cap never removes a person: its only lever is the birth
        // candidate, so the sinks it leaves are at most the uncapped ones
        // (fewer newborns, fewer newborn deaths) — and strictly the standing
        // population's.
        Assert.True(famine.Deaths + famine.Starved <= uncapped.Deaths + uncapped.Starved);
        Assert.True(Nutrition(world, cfg) < nBefore, "no decline");
        Assert.True(births * cfg.Consumption.CohortWeights[0] <= (deaths + starved) * MaxWeight(cfg),
            "births exceeded deaths in adult-equivalents under a zero limit");
    }

    /// <summary>Runs the land-bound food loop for <paramref name="turns"/> and
    /// asserts, every turn from the second: never INTO deficit (below), N_nutr
    /// non-decreasing within the flooring band, and the §28 bound
    /// N_nutr ≤ N_lim + Σ max(0, A_step − D_pre) + band, the drift term
    /// accumulated by the replica fed the kernel's own integer state.
    /// THE DEFICIT ASSERTION, honestly: in the kernel's exact double state d
    /// is 0 on every turn (the population approaches N_lim from below). The
    /// INTEGER population sits within the reconciliation band of that state,
    /// and at the limit the store equilibrates at zero (spoilage drains a
    /// store with no net inflow), so a flooring surplus of a fraction of one
    /// adult-equivalent reads as a deficit of a few units in a decade's demand
    /// (measured: d ≤ 7.2 × 10⁻³ over 200 turns on this rig — a surplus of
    /// ≈ 5 ae against the 33-ae band). The pin is therefore
    /// d ≤ band / N_lim — any shortfall lies inside the integer band expressed
    /// as a ratio (5 % here) — with the measured maximum reported; the uncapped
    /// mutant (M-DEM-CAP) overshoots by a factor and reads d ≈ 0.3–0.5.</summary>
    private static (double Final, double Limit, double DriftSum, double MaxDeficit) RunAsymptote(
        SimConfig cfg, WorldState world, double dt, int turns, out double peak)
    {
        TurnExecutor exec = FoodLoop(cfg, dt);
        double driftSum = 0.0, nPrev = Nutrition(world, cfg), limit = double.NaN, maxDeficit = 0.0;
        peak = nPrev;
        world = exec.Step(world);                                  // turn 1: no prev deficit row — uncapped
        Assert.Equal(0.0, world.ConsumptionDeficits[0].DeficitRatio);
        double band = FlooringBand(world, cfg);
        for (int t = 2; t <= turns; t++)
        {
            limit = Limit(world, cfg);
            Assert.True(double.IsFinite(limit) && limit > 0.0, $"turn {t}: N_lim {limit}");
            DemographicsReplica.Result r = ReplicaTurn(world, cfg, dt, world.Buckets[0].ReboundReservoir);
            driftSum += r.DriftBound;
            // "Non-decreasing" on the double state the kernel integrates (the
            // replica, fed the kernel's integer seed), stated exactly. The cap
            // holds births at replacement when H_rem = 0, so a step never
            // declines BY the cap; a step declines only when NATURAL births sit
            // below replacement (m = 1) — in a 400-person rig the three 75+
            // elders dying at 0.3/yr make the first steps of a turn
            // sub-replacement before the fertile cohorts' maturation carries
            // it — and the headroom those deaths open is refilled at the
            // relaxation rate k (3.4 %/step, "the gap halves per decade"), not
            // at once, so such a turn nets a tiny decline (traced: −0.015 ae
            // of 653 at turn 183 with nature at +1.7). THE PIN: a turn's
            // decline never exceeds 10⁻³ of N (measured 2 × 10⁻⁵), the
            // population converges (below), and there is no drawdown after
            // convergence (final within the integer band of the peak).
            double nSeed = Nutrition(world, cfg), nExact = 0.0;
            for (int c = 0; c < Cohorts.Count; c++) nExact += cfg.Consumption.CohortWeights[c] * r.Pop[c];
            Assert.True(nExact >= nSeed * (1.0 - 1e-3), $"turn {t}: exact state fell {nSeed:F3} → {nExact:F3}");
            world = exec.Step(world);
            double n = Nutrition(world, cfg);
            peak = Math.Max(peak, n);
            double d = world.ConsumptionDeficits[0].DeficitRatio;
            maxDeficit = Math.Max(maxDeficit, d);
            Assert.True(d <= band / limit, $"turn {t}: overshot INTO deficit d = {d:E2} beyond the integer band {band / limit:E2}");
            // The INTEGER state follows within one turn's remainder rollover:
            // each sink row's carried remainder can wrap (0.99 → 0.0x) and
            // cost one extra head that turn — bounded by rows + 1, mean zero.
            Assert.True(n >= nPrev - (Cohorts.Count + 1) * MaxWeight(cfg), $"turn {t}: N_nutr fell {nPrev:F1} → {n:F1}");
            Assert.True(n <= limit + driftSum + band,
                $"turn {t}: N_nutr {n:F1} > N_lim {limit:F1} + drift {driftSum:F2} + band {band:F0}");
            nPrev = n;
        }
        return (nPrev, limit, driftSum, maxDeficit);
    }

    [Fact]
    public void D_Asymptote_NoOvershootUnderConstantConditions()
    {
        // THE §28 PIN (kills M-DEM-CAP: without the cap the population passes
        // the limit and a deficit turn appears). Weather off, a constant
        // land-bound harvest (25 km² × 26/yr = 650 ae/yr against a founding
        // 400 people ≈ 330 ae; labour never binds), 200 canonical turns:
        // N_nutr rises monotonically toward N_lim, d == 0.0 on EVERY turn, and
        // N_nutr ≤ N_lim + Σ max(0, A_step − D_pre) + the integer band.
        SimConfig cfg = TestConfigs.Sim();
        const double dt = 10.0, arable = 25.0;
        WorldState world = LandBoundWorld(cfg, Scaled(cfg, 1), arable);
        double n0 = Nutrition(world, cfg);
        (double final, double limit, double drift, double maxDeficit) = RunAsymptote(cfg, world, dt, 200, out double peak);
        Assert.Equal(arable * cfg.Farming.YieldPerArableKm2PerYear, limit, 6);   // land-bound: N_lim = the harvest rate
        Assert.True(limit > 1.5 * n0, "rig starts too close to its limit — the asymptote is vacuous");
        Assert.True(final >= 0.95 * limit, $"did not converge: {final:F0} vs N_lim {limit:F0}");
        Assert.True(peak <= limit + drift + FlooringBand(world, cfg));
        Assert.True(final >= peak - FlooringBand(world, cfg), $"drawdown after convergence: peak {peak:F1} → final {final:F1}");
        Assert.True(maxDeficit <= FlooringBand(world, cfg) / limit, $"max deficit {maxDeficit:E2} is not a flooring artefact");
        Console.WriteLine($"asymptote: N_0 {n0:F0} → {final:F1} against N_lim {limit:F1}, drift term {drift:F2}, max d {maxDeficit:E2}");

        // TWIN with every cohort weight 1.0: no aging drift exists (w_{c+1} −
        // w_c = 0 for every c) — N_nutr ≤ N_lim within the integer band alone.
        var ones = new double[Cohorts.Count];
        Array.Fill(ones, 1.0);
        SimConfig flat = cfg with { Consumption = cfg.Consumption with { CohortWeights = ones } };
        WorldState twin = LandBoundWorld(flat, Scaled(flat, 1), arable);
        (double finalFlat, double limitFlat, double driftFlat, double maxDeficitFlat) = RunAsymptote(flat, twin, dt, 200, out double peakFlat);
        Assert.Equal(0.0, driftFlat);
        Assert.True(peakFlat <= limitFlat + FlooringBand(twin, flat));
        Assert.True(finalFlat >= 0.95 * limitFlat, $"twin did not converge: {finalFlat:F0} vs {limitFlat:F0}");
        Assert.True(finalFlat >= peakFlat - FlooringBand(twin, flat), $"twin drawdown: peak {peakFlat:F1} → final {finalFlat:F1}");
        Assert.True(maxDeficitFlat <= FlooringBand(twin, flat) / limitFlat, $"twin max deficit {maxDeficitFlat:E2} is not a flooring artefact");
    }

    [Fact]
    public void D_Asymptote_DefaultMix_LivestockSurplusIsNotFood()
    {
        // KILLS M-DEM-SUMFOOD semantically: the Default sector mix (farming
        // 0.55 / herding 0.15) on a rich livestock deposit produces a
        // non-staple surplus far above its basket share; the limit the
        // population converges to is the GRAIN-feedable one, X = S /
        // (1 − b_livestock) — not S + NS — and d == 0 every turn. A
        // sum-of-food limit would let the population pass what grain can
        // feed and a deficit turn would appear.
        SimConfig cfg = TestConfigs.Sim();
        cfg = cfg with { Production = cfg.Production with { OutputPerHerderPerYear = 3.0 * cfg.Production.OutputPerHerderPerYear } };
        const double dt = 10.0, arable = 20.0;
        WorldState world = LandBoundWorld(cfg, Scaled(cfg, 1), arable, row: Sectors.Default(S0), livestockDeposit: 1.0);
        double n0 = Nutrition(world, cfg);
        TurnExecutor exec = FoodLoop(cfg, dt);
        world = exec.Step(world);
        world = exec.Step(world);
        // The rig is what it claims: livestock in SURPLUS against its share.
        int livestockRow = -1, grainRow = -1;
        for (int i = 0; i < world.GoodStocks.Count; i++)
        {
            if (world.GoodStocks[i].Good.Value == cfg.Goods!.IdOf("livestock")) livestockRow = i;
            if (world.GoodStocks[i].Good.Value == cfg.Goods.GrainId) grainRow = i;
        }
        long ns = world.GoodStocks[livestockRow].LastProducedUnits;
        long s = world.GoodStocks[grainRow].LastProducedUnits;
        long demand = world.ConsumptionDeficits[0].DemandUnits;
        double bLivestock = world.GoodStocks[livestockRow].LastConsumptionDemandUnits / (double)demand;
        Assert.True(ns > bLivestock * (s / (1.0 - bLivestock)) * 2.0, $"livestock {ns} is not a surplus against share {bLivestock:F3}");
        double sumLimit = (s + ns) / dt;
        double grainLimit = Limit(world, cfg);
        Assert.InRange(grainLimit, s / (1.0 - bLivestock) / dt - 1e-6, s / (1.0 - bLivestock) / dt + 1e-6);
        Assert.True(grainLimit < 0.8 * sumLimit, $"the sum {sumLimit:F0} and the feedable limit {grainLimit:F0} do not separate");

        (double final, double limit, double drift, double maxDeficit) = RunAsymptote(cfg, world, dt, 200, out double peak);
        Assert.True(limit > 1.3 * n0, "rig starts too close to its limit");
        Assert.True(final >= 0.95 * limit, $"did not converge to the grain-feedable limit: {final:F0} vs {limit:F0}");
        Assert.True(peak <= limit + drift + FlooringBand(world, cfg));
        Assert.True(final >= peak - FlooringBand(world, cfg), $"drawdown after convergence: peak {peak:F1} → final {final:F1}");
        Assert.True(peak < 0.85 * sumLimit, $"population {peak:F0} chased the sum-of-food limit {sumLimit:F0}");
        Assert.True(maxDeficit <= FlooringBand(world, cfg) / limit, $"max deficit {maxDeficit:E2} is not a flooring artefact");
        Console.WriteLine($"default mix: N_0 {n0:F0} → {final:F1}; grain-feedable {limit:F1} vs Σ food {sumLimit:F1}, max d {maxDeficit:E2}");
    }

    [Fact]
    public void D_Cap_DtComposes_LandBound()
    {
        // Warm the land-bound rig into the BINDING regime, then from one
        // world: one dt-10 turn vs two dt-5 turns. Within a turn the allowance
        // composes exactly (H_rem(end) = H_0 e^(−k dt)); across the boundary
        // the second dt-5 turn re-reads N_lim from prev, which on a
        // land-bound rig equals the within-turn remainder, so the two agree
        // cohort-for-cohort within reconciliation flooring.
        SimConfig cfg = TestConfigs.Sim();
        WorldState world = LandBoundWorld(cfg, Scaled(cfg, 1), arableKm2: 25.0);
        world = FoodLoop(cfg, 10.0).Run(world, 100);
        Assert.Equal(0.0, world.ConsumptionDeficits[0].DeficitRatio);
        double limit = Limit(world, cfg);
        Assert.True(Nutrition(world, cfg) > 0.9 * limit, "warm-up did not reach the binding regime");
        // The cap binds on the coarse turn: fewer births than the uncapped replica.
        DemographicsReplica.Result free = DemographicsReplica.Turn(cfg.Demographics, Counts(world), 0.0, 10.0, world.Buckets[0].ReboundReservoir);
        WorldState coarse = FoodLoop(cfg, 10.0).Step(world);
        long bornCoarse = coarse.SettlementVitals[0].Births;
        Assert.True(bornCoarse < Floor(free.Births) - 1, $"cap not binding: {bornCoarse} vs uncapped {free.Births:F1}");

        WorldState fine = FoodLoop(cfg, 5.0).Run(world, 2);
        long bornFine = fine.SettlementVitals[0].Births;          // the second dt-5 turn only
        for (int c = 0; c < Cohorts.Count; c++)
        {
            Assert.True(Math.Abs(coarse.Buckets[c].Count.Value - fine.Buckets[c].Count.Value) <= 2,
                $"cohort {c}: dt10 {coarse.Buckets[c].Count.Value} vs 2×dt5 {fine.Buckets[c].Count.Value}");
        }
        Assert.True(Math.Abs(Nutrition(coarse, cfg) - Nutrition(fine, cfg)) <= 3.0,
            $"N_nutr dt10 {Nutrition(coarse, cfg):F1} vs 2×dt5 {Nutrition(fine, cfg):F1}");
        Assert.True(bornFine > 0 && bornCoarse > 0);
    }

    [Fact]
    public void D_Rebound_StrictRelease_NeverInvents()
    {
        // KILLS M-DEM-BANKCAPPED (bank taken from the capped pair): under a
        // sustained ceiling with no shortfall there is nothing to defer, so the
        // reservoir may only FALL. Fertile pool in the absorbing cohort with a
        // small mortality (deaths exist, so replacement births are allowed
        // under H_0 = 0). Two SEVERE turns (d = 0.4, uncapped) bank suppressed
        // conceptions; then a binding cap (N_lim = 0) at d = 0: every turn is
        // replica-exact (births to the person, reservoir bit for bit), the
        // reservoir is non-increasing, its drop is exactly the REALISED
        // release, the §28 bound holds, and the total extra births over the
        // rebound-off twin never exceed the bank (deferred, not invented).
        SimConfig cfg = FertilityOnly(TestConfigs.Sim());
        var mortality = new double[Cohorts.Count];
        mortality[15] = 0.02;
        cfg = cfg with { Demographics = cfg.Demographics with { MortalityPerYear = mortality } };
        SimConfig off = cfg with { Demographics = cfg.Demographics with { ReboundRecoverableFraction = 0.0 } };
        const double dt = 10.0;
        var counts = new long[Cohorts.Count];
        counts[15] = 100_000;

        WorldState world = CappedRig(cfg, counts, dt, stapleProduced: null, deficit: 0.4);
        world.ConsumptionDeficits[0] = new ConsumptionDeficitRow(S0, 0.4, 0);   // uncapped while SEVERE
        WorldState twin = world.Clone();
        var exec = new TurnExecutor(FlatEra(dt), [SystemCatalog.Demographics(cfg)]);
        var execOff = new TurnExecutor(FlatEra(dt), [SystemCatalog.Demographics(off)]);
        for (int t = 0; t < 2; t++) { world = exec.Step(world); twin = execOff.Step(twin); }
        double bank = world.Buckets[0].ReboundReservoir;
        Assert.True(bank > 100.0, $"famine banked {bank} — rig vacuous");
        Assert.Equal(0.0, twin.Buckets[0].ReboundReservoir);

        // The ceiling: d = 0, DemandUnits > 0, vitals present, S = 0 ⇒ N_lim = 0.
        world.ConsumptionDeficits[0] = new ConsumptionDeficitRow(S0, 0.0, 1_000_000);
        twin.ConsumptionDeficits[0] = new ConsumptionDeficitRow(S0, 0.0, 1_000_000);
        Assert.Equal(0.0, Limit(world, cfg));
        double n0 = Nutrition(world, cfg), band = FlooringBand(world, cfg), driftSum = 0.0;
        long bornBefore = FlowTotal(world, ReasonIds.Births, sunk: false);
        long bornTwinBefore = FlowTotal(twin, ReasonIds.Births, sunk: false);
        double reservoirPrev = bank;
        for (int t = 1; t <= 15; t++)
        {
            double rem = world.Buckets[0].BirthRemainder;
            long ledgerBefore = FlowTotal(world, ReasonIds.Births, sunk: false);
            DemographicsReplica.Result r = ReplicaTurn(world, cfg, dt, reservoirPrev);
            world = exec.Step(world);
            twin = execOff.Step(twin);
            double reservoir = world.Buckets[0].ReboundReservoir;
            Assert.Equal(r.Reservoir, reservoir);                                       // bit-exact
            Assert.Equal(Floor(r.Births + rem), FlowTotal(world, ReasonIds.Births, sunk: false) - ledgerBefore);
            Assert.True(reservoir <= reservoirPrev, $"turn {t}: reservoir grew under a ceiling {reservoirPrev} → {reservoir}");
            driftSum += r.DriftBound;
            Assert.True(Nutrition(world, cfg) <= n0 + driftSum + band, $"turn {t}: §28 bound broken");
            reservoirPrev = reservoir;
        }
        Assert.True(reservoirPrev < bank, "nothing was ever released — strict release inert");
        Assert.True(reservoirPrev > 0.0, "the whole bank released under a zero ceiling — release was not scaled");
        long extra = (FlowTotal(world, ReasonIds.Births, sunk: false) - bornBefore)
                     - (FlowTotal(twin, ReasonIds.Births, sunk: false) - bornTwinBefore);
        Assert.True(extra >= 0, $"the banked rig bore fewer than its twin: {extra}");
        Assert.True(extra <= bank - reservoirPrev + 1.0, $"extra births {extra} exceed the realised release {bank - reservoirPrev:F2}");
    }

    [Fact]
    public void D_PostFamine_RecoversTowardLimit()
    {
        // Land-bound rig warmed to its limit; then food labour is abandoned for
        // two decades (the pct-0 row in force: harvest 0, the store runs dry,
        // FAMINE/Abandonment read, the whole deficit starves); then farming is
        // restored. Over the next 30 turns the population rises monotonically
        // toward N_lim after the last d > 0 turn and never overshoots the §28
        // bound — the banked rebound releases only into headroom.
        SimConfig cfg = TestConfigs.Sim();
        const double dt = 10.0;
        WorldState world = LandBoundWorld(cfg, Scaled(cfg, 1), arableKm2: 25.0);
        TurnExecutor exec = FoodLoop(cfg, dt);
        world = exec.Run(world, 100);
        double limit = Limit(world, cfg);
        double atLimit = Nutrition(world, cfg);
        Assert.True(atLimit > 0.9 * limit, "warm-up did not reach the limit");

        SectorAllocationRow farming = world.SectorAllocations[0];
        world.SectorAllocations[0] = Abandoned(S0);
        world = exec.Step(world);                                   // harvest 0; the store covers part: d > 0
        Assert.True(world.ConsumptionDeficits[0].DeficitRatio > 0.0);
        Assert.Equal(FoodStateKind.Famine, FoodState.Of(world, S0, cfg, out _));
        long starvedBefore = FlowTotal(world, ReasonIds.Starvation, sunk: true);
        world = exec.Step(world);                                   // FAMINE read; harvest 0 again
        Assert.True(FlowTotal(world, ReasonIds.Starvation, sunk: true) > starvedBefore, "the famine starved nobody");
        world.SectorAllocations[0] = farming;
        world = exec.Step(world);                                   // reads d(prev) > 0 as SEVERE (d_eff = d at 1); harvest resumes
        double trough = Nutrition(world, cfg);
        Assert.True(trough < 0.5 * atLimit, $"famine too shallow: {trough:F0} vs {atLimit:F0}");
        Assert.True(world.Buckets[0].ReboundReservoir > 0.0, "nothing banked — the rebound has nothing to release");

        double band = FlooringBand(world, cfg), driftSum = 0.0, nPrev = trough;
        bool recovered = false;
        for (int t = 1; t <= 30; t++)
        {
            if (world.ConsumptionDeficits[0].DeficitRatio == 0.0) recovered = true;
            DemographicsReplica.Result r = ReplicaTurn(world, cfg, dt, world.Buckets[0].ReboundReservoir);
            world = exec.Step(world);
            double n = Nutrition(world, cfg);
            if (recovered)
            {
                driftSum += r.DriftBound;
                Assert.Equal(0.0, world.ConsumptionDeficits[0].DeficitRatio);
                Assert.True(n >= nPrev - 2.0 * MaxWeight(cfg), $"turn {t}: recovery reversed {nPrev:F1} → {n:F1}");
                Assert.True(n <= limit + driftSum + band, $"turn {t}: overshoot {n:F1} > {limit:F1} + {driftSum:F2} + {band:F0}");
            }
            nPrev = n;
        }
        Assert.True(recovered, "d never returned to zero");
        // Recovery runs at the kernel's NATURAL tempo (+0.76 %/decade, plus
        // the one-off rebound release) — the cap only bounds it from above,
        // so 30 turns rise measurably toward the limit without reaching it.
        Assert.True(nPrev > 1.1 * trough, $"did not recover toward the limit: {trough:F0} → {nPrev:F0} (N_lim {limit:F0})");
        Assert.True(nPrev < limit, $"recovered past the limit in 30 turns: {nPrev:F0} vs {limit:F0}");
        Console.WriteLine($"post-famine: {atLimit:F0} → trough {trough:F0} → {nPrev:F0} against N_lim {limit:F0}");
    }

    [Fact]
    public void D_Headroom_CountsArrivals()
    {
        // H_0 is read from the OWNED buckets at step start, not from prev:
        // refugees who arrived this turn have used headroom. Two settlements —
        // A in a total deficit (flight), B fed with a small headroom (N_lim =
        // N_B + 300 ae, the cap binding). Run 1: [Migration, Demographics] on
        // W. Run 2: [Demographics] on Migration(W) — the arrivals already in
        // B's PREV. At Demographics' step start the owned buckets are the same
        // in both, so B's demographic outputs must be BIT-IDENTICAL: a kernel
        // reading prev for N_now would give run 1 the larger headroom
        // (Headroom(prev) ignores the arrivals) and more births. The observer's
        // Headroom(prev) is stated to differ from H_0 by the arrivals' ae.
        SimConfig cfg = TestConfigs.Sim();
        const double dt = 10.0;
        var world = new WorldState(7);
        var ledger = new Ledger(world.LedgerFlows);
        SettlementId a = new(0), b = new(1);
        long[] countsB = Scaled(cfg, 10);
        for (int sIdx = 0; sIdx < 2; sIdx++)
        {
            SettlementId id = sIdx == 0 ? a : b;
            world.Settlements.Add(new SettlementRow(id, SiteCell: sIdx, FoundedTurn: 0));
            for (int c = 0; c < Cohorts.Count; c++)
            {
                int row = world.Buckets.Add(new BucketRow(
                    id, new CultureId(1), new ReligionId(1), new ClassId(1), c, Conserved.Zero, 0.0, 0.0, 0.0, 0.0));
                long n = sIdx == 0 ? 2_000 : countsB[c];
                if (n > 0)
                    ledger.Flow(ref world.Buckets.Ref(row).Count, ConservedQuantityIds.Population,
                        ReasonIds.InitialEndowment, n, FlowDirection.Source, OverdrawPolicy.Throw);
            }
        }
        // T4.21-4 (RULE 2): both settlements carry a catchment summary row, because
        // the finite FoodHeadroom arm now requires one (row ABSENCE is "the influx
        // was never measured" ⇒ +∞). Arable 0 keeps it ATTRACTIVENESS-NEUTRAL —
        // MigrationSystem.cs:296-299 leaves arableKm2 at 0.0 when no row matches, so
        // the zero-arable row is the same number to attractiveness as no row, and
        // the measured 142.3 ae arrival below is unmoved by its presence.
        for (int sIdx = 0; sIdx < 2; sIdx++)
        {
            world.CatchmentSummaries.Add(new CatchmentSummaryRow(
                sIdx == 0 ? a : b, NodeCount: 1, EffectiveArableKm2: 0.0,
                NetworkRevision: 0, LastRecomputeTurn: 0));
        }

        double nB = 0.0;
        for (int c = 0; c < Cohorts.Count; c++) nB += cfg.Consumption.CohortWeights[c] * countsB[c];
        long stapleB = (long)((nB + 300.0) * dt);
        var grain = new GoodId(cfg.Goods!.GrainId);
        world.GoodStocks.Add(new GoodStockRow(a, grain, Conserved.Zero, 0.0, 0.0));
        world.GoodStocks.Add(new GoodStockRow(b, grain, Conserved.Zero, 0.0, 0.0, lastProducedUnits: stapleB, lastConsumptionDemandUnits: 0));
        ledger.Flow(ref world.GoodStocks.Ref(1).Amount, ConservedQuantityIds.OfGood(grain), ReasonIds.InitialEndowment, 5_000_000,
            FlowDirection.Source, OverdrawPolicy.Throw);
        world.ConsumptionDeficits.Add(new ConsumptionDeficitRow(a, 1.0, 0));
        world.ConsumptionDeficits.Add(new ConsumptionDeficitRow(b, 0.0, (long)(nB * dt)));
        world.SettlementVitals.Add(new SettlementVitalsRow(b, 0, 0, dt));
        world.SettlementDistances.Add(new SettlementDistanceRow(a, b, 5.0));
        world.SettlementDistances.Add(new SettlementDistanceRow(b, a, 5.0));
        double headroomPrev = DemographicsSystem.Headroom(world, b, cfg);
        Assert.InRange(headroomPrev, 299.0, 301.0);

        WorldState moved = new TurnExecutor(FlatEra(dt), [SystemCatalog.Migration(cfg)]).Step(world);
        double arrivals = 0.0;
        for (int i = 0; i < moved.Buckets.Count; i++)
        {
            if (moved.Buckets[i].Settlement != b) continue;
            arrivals += cfg.Consumption.CohortWeights[moved.Buckets[i].CohortIdx]
                        * (moved.Buckets[i].Count.Value - world.Buckets[i].Count.Value);
        }
        // T4.21-2 ∥ T4.21-3 MERGE RE-RIG, MEASURED ON THE MERGED TREE by the
        // agent writing this line: 142.29999999999998 ae arrive against B's
        // 300-ae headroom, not the > 500 the T4.21-3 branch measured with
        // migration unbounded. What decides it is ADR-025 §3.5c's vacancy bound
        // — B accepts a share of its OWN vacancy instead of the whole of A's
        // flight. The guard's aim is unchanged and still has teeth: the
        // arrivals must eat a real part of B's headroom, or the two runs below
        // could not differ; 142 of 300 is a little under half of it.
        Assert.True(arrivals > 100.0, $"only {arrivals:F1} ae arrived — rig vacuous");
        Assert.InRange(DemographicsSystem.Headroom(moved, b, cfg), 0.0, Math.Max(0.0, headroomPrev - arrivals) + 1e-6);

        WorldState run1 = new TurnExecutor(FlatEra(dt), [SystemCatalog.Migration(cfg), SystemCatalog.Demographics(cfg)]).Step(world);
        WorldState run2 = new TurnExecutor(FlatEra(dt), [SystemCatalog.Demographics(cfg)]).Step(moved);
        AssertBucketsBitIdentical(run1, run2);
        Assert.Equal(run1.SettlementVitals.Count, run2.SettlementVitals.Count);
        for (int i = 0; i < run1.SettlementVitals.Count; i++) Assert.Equal(run1.SettlementVitals[i], run2.SettlementVitals[i]);

        // Semantic: B's growth that turn is bounded by the allowance on the
        // REDUCED headroom, and is below what the prev headroom would allow.
        double nAfterArrivals = 0.0, nEnd = 0.0;
        for (int i = 0; i < moved.Buckets.Count; i++)
        {
            if (moved.Buckets[i].Settlement != b) continue;
            nAfterArrivals += cfg.Consumption.CohortWeights[moved.Buckets[i].CohortIdx] * moved.Buckets[i].Count.Value;
            nEnd += cfg.Consumption.CohortWeights[run1.Buckets[i].CohortIdx] * run1.Buckets[i].Count.Value;
        }
        double k = cfg.Demographics.HeadroomRelaxationPerYear;
        double h0 = Math.Max(0.0, (nB + 300.0) - nAfterArrivals);
        DemographicsReplica.Result rB = DemographicsReplica.Turn(cfg.Demographics, CountsOf(moved, b), 0.0, dt, 0.0,
            nLim: nB + 300.0, cohortWeights: cfg.Consumption.CohortWeights, headroomRelaxationPerYear: k);
        double band = (1 + 2 * Cohorts.Count) * MaxWeight(cfg);
        Assert.True(nEnd - nAfterArrivals <= (1.0 - Math.Exp(-k * dt)) * h0 + rB.DriftBound + band,
            $"growth {nEnd - nAfterArrivals:F1} exceeds the allowance on the reduced headroom {h0:F1}");
        Assert.True(nEnd - nAfterArrivals < (1.0 - Math.Exp(-k * dt)) * headroomPrev,
            "B grew as if the arrivals had not used its headroom");
    }


    private static long[] CountsOf(WorldState world, SettlementId s)
    {
        var counts = new long[Cohorts.Count];
        for (int i = 0; i < world.Buckets.Count; i++)
            if (world.Buckets[i].Settlement == s) counts[world.Buckets[i].CohortIdx] += world.Buckets[i].Count.Value;
        return counts;
    }
}
