using Sim.Core;
using Sim.Core.Kernel;
using Sim.Core.State;
using Sim.Core.Systems;
using Sim.Core.Systems.Consumption;
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
}
