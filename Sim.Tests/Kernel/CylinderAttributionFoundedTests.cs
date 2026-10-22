using Sim.Core;
using Sim.Core.Kernel;
using Sim.Core.State;
using Sim.Core.Systems;
using Sim.Core.Systems.Harvest;
using Sim.Tests.TestUtil;
using R = Sim.Tests.TestUtil.CylinderAttributionRig;

namespace Sim.Tests.Kernel;

/// <summary>
/// W1 F7 (batch 4; D-055, 2026-10-16; design §2 rigs #6, #7, #8 (T2 step 1) and #10, #11, #12 (T2 step 2)) — THE OLDER LAYERS
/// THAT ACT FROM THE FIRST STEP, attributed
/// turn-exactly on the SHIPPED CYLINDER: the canonical founded world (seed 42, 1024 px, twelve settlements, no orders),
/// founded and stepped by both arms on worldgen.json as shipped (topology.wrapX). ON = the shipped config; OFF = the
/// layer's NON-chained single-switch twin (<see cref="SingleSwitchTwins"/>), so every newer layer stays on in both arms.
/// Each layer's first action is read off world 0 (or the era table) independently of any hash: it is the step 0 → 1,
/// and the first differing world is world 1 — never world 0 (the switch changes nothing at founding) and never later.
/// The write set at world 1 is MEASURED and pinned: none of the six touches a topology table. Five read no distance (their
/// seam exposure is recorded in D-055 as "no distance read"); R2b's weather geography (#10) reads the straight line between
/// settlements, and this world has no seam-crossing pair (MEASURED: 0 of 66), so its seam half is
/// CylinderAttributionSeamTests' seed-16 rotation rig. The three R2b twins (TestConfigs.PreWeatherGeography, PreUnrest,
/// PreAgeMilitary) are single-layer content twins already (each changes one layer, keeping every newer one on).
/// The flat legs (IntegratedPinAttributionTests' R4 forager, D-051 1(b) and R1 recipe controls) are unchanged: they hold
/// the historical literals.
/// SEMANTIC FIRST (T2 step 3): every rig reads what its layer DOES in the step 0 → 1 off the state — the forager's lower
/// harvests, the weather streams' draw counts (the sub-step count of the time average), the weather kernel's indifference
/// to every road, the gated recipe's output, the Dignity rows, the eligibility row — BEFORE the hash-derived first-different
/// turn, so a disabled or broken layer fails on its behaviour and not only on a hash.
/// T2 FIX ROUND (findings T2-V2, T2-V3): the draw count and the road indifference left the turn's multiplier and the kernel's
/// distance reading unasserted — a point-value multiplier, a sum divided by dt − 1 and a kernel weighting every pair 1 (or
/// reading distance at 1,000 × the range) each passed. #7 now reads D-051 1(b)'s defining equation off the rows (the
/// stripped arm's multiplier is the point value exp(x − σ²/2), the shipped arm's is not) and #7 and #10 assert the step is
/// the documented model's, recomputed test-side (<see cref="HarvestWeatherReference"/>), bit for bit.
/// </summary>
public class CylinderAttributionFoundedTests : IClassFixture<CylinderAttributionFoundedTests.Arms>
{
    // ---------------------------------------------------------------------------------------------------------------
    // MEASURED (2026-10-16, D-055; batch 4 track T2 step 1) on base-b3 + the frozen T1a patch (pinned tree 48e7c13b…,
    // patch sha256 01179819…), Release, by the agent writing this block (probe log t2-out/probe-measure.txt):
    //   world 0: 12 of 12 settlements harvest wild food (ON), 0 under PreForagerOnly; the first step's dt is 3,600 days
    //   (10 sim-years); pottery-firing is gated at all 12 settlements (ON) and available at all 12 ungated; weaving is
    //   baseline (available in both arms); bronze and tools need artisans (available in neither).
    //   forager off: first different world 1, write set {GoodStocks, LedgerFlows};
    //   time-averaged harvest off: first different world 1, write set {HarvestWeather, RngStreams};
    //   recipe knowledge off: first different world 1, write set {GoodStocks, LedgerFlows}.
    // These are the batch-3 recon's values (f7-flat-only §5, P4/P5e) re-measured on this tree: unchanged.
    // ---------------------------------------------------------------------------------------------------------------
    private const int Horizon = 2;
    private const int FirstAction = 1;                      // the step 0 → 1 reads world 0
    private const int WildFoodSettlementsAtFounding = 12;
    private const long FirstStepDtDays = 3_600;
    private static readonly string[] ForagerWrites = ["GoodStocks", "LedgerFlows"];
    private static readonly string[] TimeAveragedWrites = ["HarvestWeather", "RngStreams"];
    private static readonly string[] RecipeWrites = ["GoodStocks", "LedgerFlows"];

    // ---------------------------------------------------------------------------------------------------------------
    // MEASURED (2026-10-16, D-055; batch 4 track T2 step 2) on base-b3 + the frozen T1a patch (pinned tree 48e7c13b…,
    // patch sha256 01179819…), Release, by the agent writing this block (probe log t2-out/s2/probe-a.txt, A3):
    //   world 0 carries no SettlementDistances row (the catchment writes the first ones into world 1: 132), so the
    //   travel-cost kernel (PreWeatherGeography) couples no pair in the step 0 → 1, while the geographic kernel couples
    //   every pair (66 of 66, exp(−d/range) > 0); weather geography off: first different world 1, write set
    //   {HarvestWeather}; 0 seam-crossing pairs among the 12 settlements.
    //   unrest off (PreUnrest): Dignity (the need bound to the tax burden, id 7) publishes 12 NeedSatisfaction rows in
    //   world 1 ON and none OFF (48 against 36 rows); first different world 1, write set {Grievance, NeedSatisfaction}.
    //   Age-military off (PreAgeMilitary): the founding warband (identity 301) is not among the identities Age 2's
    //   Formations milestone counts, so its AgeEligibility row in world 1 reads SupportingMet 0 ON and 1 OFF (CategoryMask
    //   0 against 16); first different world 1, write set {AgeEligibility}.
    // These equal the batch-3 recon's cylinder values (f7-flat-only §5, P4/P5e): unchanged by T1a.
    // ---------------------------------------------------------------------------------------------------------------
    private const int CoupledPairsAtFounding = 66;
    private const int DignityRowsOn = 12;
    private const int FoundingWarbandIdentity = 301;
    private static readonly string[] WeatherWrites = ["HarvestWeather"];
    private static readonly string[] UnrestWrites = ["Grievance", "NeedSatisfaction"];
    private static readonly string[] AgeMilitaryWrites = ["AgeEligibility"];

    // ---------------------------------------------------------------------------------------------------------------
    // MEASURED (2026-10-16, D-055; batch 4 track T2 step 3) on base-b3 + the frozen T1a patch (pinned tree 48e7c13b…,
    // patch sha256 01179819…), Release, by the agent writing this block (probe log t2-out/s3/probes/probe3-run1.txt,
    // P3b/P3c, and probe3-run3.txt, P3g) — the SEMANTIC readings the step-3 mutant campaign showed the rigs needed (a kill on
    // the hash-derived first-different turn alone is not a semantic kill):
    //   forager: in the step 0 → 1 each of the 12 wild-food settlements harvests LESS grain ON (the forager rates) than with
    //     the layer stripped (the cultivated rates) — e.g. s0 5,888 against 6,847.
    //   time-averaged harvest: the weather system (SystemId 13) draws from 12 regional streams; in the step 0 → 1 each takes
    //     44 draws ON and 4 with the layer stripped: one innovation pass is 4 draws, and ON makes 1 stationary-start pass
    //     (every settlement is new on world 0) plus round(dt = 10 years) = 10 yearly sub-steps — (1 + 10) × 4 = 44.
    //   weather geography: world 1 carries 132 SettlementDistances rows; stepping the weather system alone on world 1 with
    //     every travel cost doubled, halved or made unreachable changes 0 of the 12 weather rows ON (the geographic kernel
    //     reads no road) and 12 of 12 under the travel-cost kernel (PreWeatherGeography) — on seed 42 and on seed 16.
    // ---------------------------------------------------------------------------------------------------------------
    private const int ForagerHarvestSettlements = 12;
    private const int WeatherStreams = 12;
    private const long DrawsPerInnovationPass = 4;
    private const long YearlySubStepsAtFirstStep = 10;
    private const int DistanceRowsAtWorld1 = 132;
    private const int WeatherRows = 12;

    // ---------------------------------------------------------------------------------------------------------------
    // MEASURED (D-055, 2026-10-16; batch 4 track T2 FIX ROUND, run 2026-10-17) on base-b3 + T1a + T2, Release, by the agent
    // writing this block (this class's run, t2-out/fix): on world 1 the time-averaged-off arm's 12 weather rows each carry
    // Multiplier == exp(LogDeviation − σ²/2) bit for bit (one point draw) and the shipped arm's 0 of 12 (the mean of 10
    // sub-steps' multipliers); both arms' world-1 weather equals HarvestWeatherReference's recomputation from world 0.
    // (Equal to the T2-V3 verdict's probe: 12/12 OFF, 0/12 ON; s0's ON multiplier 0.7131537156451905 against its point
    // value 0.7710211829017805.)
    // ---------------------------------------------------------------------------------------------------------------
    private const int PointValueRowsStripped = 12, PointValueRowsShipped = 0;

    /// <summary>The shared ON arm (the shipped config, 2 turns) and a second, independent ON run for determinism.</summary>
    public sealed class Arms
    {
        public SimConfig On { get; } = TestConfigs.PreM6(TestConfigs.Sim());
        public R.Arm Live { get; }
        public R.Arm Again { get; }

        public Arms()
        {
            Live = R.Run(R.Founded1024(), "on", On, Horizon, keep: [0, 1, 2]);
            Again = R.Run(R.Founded1024(), "on-again", TestConfigs.PreM6(TestConfigs.Sim()), Horizon);
        }
    }

    private readonly Arms _arms;

    public CylinderAttributionFoundedTests(Arms arms) => _arms = arms;

    /// <summary>The OFF arm of a rig: the same world under the layer's twin, to the horizon, worlds 0 and 1 kept.</summary>
    private static R.Arm Twin(SimConfig off, string label) => R.Run(R.Founded1024(), label, off, Horizon, keep: [0, FirstAction]);

    /// <summary>The template's common half, asserted AFTER each rig's own semantic reading of the first action (T2 step 3: the
    /// semantic assertions come first, so a disabled or broken layer fails on what it does, not only on a hash): world 0
    /// identical, the first difference at the first action, the measured write set there with no topology table, and the ON
    /// series reproduced by a second run.</summary>
    private void AttributeAtTheFirstStep(R.Arm twin, string[] writes)
    {
        R.Arm on = _arms.Live;
        R.AssertWorld0Identical(on, twin);
        Assert.Equal(FirstAction, R.FirstDifferent(on, twin));
        string[] written = R.WriteSet(on.At(FirstAction), twin.At(FirstAction));
        Assert.Equal(writes, written);
        R.AssertNoTopologyTable(written);
        Assert.NotEqual(on.Hashes[Horizon], twin.Hashes[Horizon]);   // it stays different (the layer acts every step)
        R.AssertSameSeries(on, _arms.Again);
    }

    /// <summary>#6 — R4's FORAGER layer: wild-food gathering (sim.json farming.preCultivation). First action: a settlement
    /// harvests wild food on world 0 (<see cref="LabourActivities.HarvestsWildFood"/>, read with each arm's config), so the
    /// step 0 → 1 is the first to read the forager rates.</summary>
    [Fact]
    public void Forager_OnTheShippedCylinder_DivergesAtTheFirstStepThatReadsAWildFoodSettlement()
    {
        SimConfig off = SingleSwitchTwins.PreForagerOnly(_arms.On);
        WorldState w0 = _arms.Live.At(0);
        int wildOn = 0, wildOff = 0;
        for (int i = 0; i < w0.Settlements.Count; i++)
        {
            if (LabourActivities.HarvestsWildFood(w0, _arms.On, w0.Settlements[i].Id)) wildOn++;
            if (LabourActivities.HarvestsWildFood(w0, off, w0.Settlements[i].Id)) wildOff++;
        }
        Assert.Equal((WildFoodSettlementsAtFounding, 0), (wildOn, wildOff));   // vacuity: the layer has a settlement to act on

        // What the layer does in the step 0 -> 1: every wild-food settlement harvests at the forager rates, below the
        // cultivated harvest the stripped arm reaps on the same land and labour.
        R.Arm twin = Twin(off, "forager-off");
        int grain = _arms.On.Goods!.GrainId, lower = 0;
        for (int i = 0; i < w0.Settlements.Count; i++)
        {
            SettlementId s = w0.Settlements[i].Id;
            if (Harvested(_arms.Live.At(FirstAction), s, grain) < Harvested(twin.At(FirstAction), s, grain)) lower++;
        }
        Assert.Equal(ForagerHarvestSettlements, lower);
        AttributeAtTheFirstStep(twin, ForagerWrites);
    }

    /// <summary>#7 — D-051 1(b), TIME-AVERAGED DECADE WEATHER (sim.json harvestVariance.timeAveragedHarvest). First action: the
    /// first weather step whose dt exceeds one sim-year (the era table's dt at world 0's day), the first step that has more
    /// than one yearly sub-step to average.</summary>
    [Fact]
    public void TimeAveragedHarvest_OnTheShippedCylinder_DivergesAtTheFirstMultiYearWeatherStep()
    {
        SimConfig off = SingleSwitchTwins.PreTimeAveragedHarvestOnly(_arms.On);
        WorldState w0 = _arms.Live.At(0);
        long dtDays = R.Eras().DtDaysAt(w0.Clock.SimDays);
        Assert.Equal(FirstStepDtDays, dtDays);
        Assert.True(dtDays > SimClock.YearDays, "vacuity: the first step must span more than one yearly weather sub-step");
        Assert.Equal(FirstStepDtDays, _arms.Live.At(FirstAction).Clock.DtDays);

        // What the layer does in the step 0 -> 1, read off the weather system's own RNG streams: ON averages
        // round(dt) = 10 yearly sub-steps after one stationary-start pass, the stripped arm takes one point draw — so every
        // regional stream advanced (1 + 10) times as far ON as OFF.
        R.Arm twin = Twin(off, "time-averaged-off");
        long subSteps = (long)Math.Round(dtDays / (double)SimClock.YearDays, MidpointRounding.AwayFromZero);
        Assert.Equal(YearlySubStepsAtFirstStep, subSteps);
        (int Region, long Draws)[] on = WeatherDraws(_arms.Live.At(0), _arms.Live.At(FirstAction));
        (int Region, long Draws)[] stripped = WeatherDraws(twin.At(0), twin.At(FirstAction));
        Assert.Equal(WeatherStreams, on.Length);
        Assert.Equal(WeatherStreams, stripped.Length);
        for (int i = 0; i < on.Length; i++)
        {
            Assert.Equal(stripped[i].Region, on[i].Region);
            Assert.Equal(DrawsPerInnovationPass, stripped[i].Draws);                          // one point draw
            Assert.Equal((1 + subSteps) * DrawsPerInnovationPass, on[i].Draws);              // the start + 10 yearly sub-steps
        }

        // What the layer computes (T2 fix round, finding T2-V3) — D-051 1(b)'s defining equation, read off the rows: the
        // stripped arm publishes the point value exp(x − σ²/2) of its one draw on every row, the shipped arm the MEAN of
        // its sub-steps' multipliers, which is the point value of none ...
        double sigma = _arms.On.HarvestVariance.SigmaLogYield;
        Assert.Equal((PointValueRowsShipped, PointValueRowsStripped),
            (PointValueRows(_arms.Live.At(FirstAction), sigma), PointValueRows(twin.At(FirstAction), sigma)));
        // ... and both steps are the documented model's, recomputed from the streams (the mean over exactly round(dt)
        // sub-steps ON, the single point draw OFF), bit for bit.
        double dtYears = dtDays / (double)SimClock.YearDays;
        HarvestWeatherReference.AssertStepIs(_arms.Live.At(0), _arms.Live.At(FirstAction), _arms.On, dtYears, "ON (time-averaged)");
        HarvestWeatherReference.AssertStepIs(twin.At(0), twin.At(FirstAction), off, dtYears, "time-averaged-off");
        AttributeAtTheFirstStep(twin, TimeAveragedWrites);
    }

    /// <summary>#8 — R1's RECIPE KNOWLEDGE gate (goods.json recipes[].entity). First action: on world 0 a research-gated
    /// recipe is unavailable at a settlement under the gate and available there ungated
    /// (<see cref="CraftingQuery.IsRecipeAvailable(IReadOnlyWorldState, SimConfig, SettlementId, RecipeEntry)"/>, each arm's
    /// config), so the step 0 → 1 runs it in the ungated arm only — world 1 carries its output there and none of it ON.</summary>
    [Fact]
    public void RecipeKnowledge_OnTheShippedCylinder_DivergesAtTheFirstStepAGatedRecipeRunsUngated()
    {
        SimConfig on = _arms.On, off = SingleSwitchTwins.PreRecipeKnowledgeOnly(on);
        WorldState w0 = _arms.Live.At(0);
        int gatedRecipe = -1, sites = 0;
        for (int r = 0; r < on.Goods!.Recipes.Length && gatedRecipe < 0; r++)
        {
            if (on.Goods.Recipes[r].Entity is null) continue;
            for (int i = 0; i < w0.Settlements.Count; i++)
            {
                SettlementId s = w0.Settlements[i].Id;
                if (!CraftingQuery.IsRecipeAvailable(w0, on, s, on.Goods.Recipes[r])
                    && CraftingQuery.IsRecipeAvailable(w0, off, s, off.Goods!.Recipes[r])) sites++;
            }
            if (sites > 0) gatedRecipe = r;
        }
        Assert.True(gatedRecipe >= 0, "vacuity: no research-gated recipe is runnable ungated on world 0");
        Assert.Equal("pottery-firing", on.Goods.Recipes[gatedRecipe].Name);
        Assert.Equal(12, sites);

        R.Arm twin = Twin(off, "recipe-knowledge-off");
        var output = new GoodId(on.Goods.IdOf(on.Goods.Recipes[gatedRecipe].Output.Good));
        Assert.Equal(0L, Produced(_arms.Live.At(FirstAction), output));          // gated: nothing of it ON
        Assert.True(Produced(twin.At(FirstAction), output) > 0, "the ungated arm must run the gated recipe in the step 0 -> 1");
        AttributeAtTheFirstStep(twin, RecipeWrites);
    }

    /// <summary>#10 — R2b's WEATHER GEOGRAPHY (sim.json harvestVariance.spatialDistance): the weather kernel reads the
    /// straight-line distance between two settlements' sites (GeographicDistance — on the cylinder, the short way round)
    /// instead of the road-aware travel costs. First action: on world 0 the geographic kernel couples two or more
    /// settlements (exp(−d / range) &gt; 0 between distinct sites), while the travel-cost kernel has no distance row to read
    /// (the catchment publishes the first SettlementDistances into world 1) — so the step 0 → 1, the first weather step, is the
    /// first that differs, and only in the weather table.</summary>
    [Fact]
    public void WeatherGeography_OnTheShippedCylinder_DivergesAtTheFirstWeatherStep_InTheWeatherTableAlone()
    {
        SimConfig on = _arms.On, off = TestConfigs.PreWeatherGeography(on);
        Assert.False(on.HarvestVariance.LegacyTravelCostKernel);
        Assert.True(off.HarvestVariance.LegacyTravelCostKernel);
        WorldState w0 = _arms.Live.At(0);
        double range = on.HarvestVariance.SpatialRangeCostUnits, kmPerCost = TransportQuery.KmPerCostUnit(w0);
        int coupled = 0;
        for (int i = 0; i < w0.Settlements.Count; i++)
            for (int j = i + 1; j < w0.Settlements.Count; j++)
            {
                Assert.True(GeographicDistance.TryIdealGroundCostUnits(w0, w0.Settlements[i].SiteCell, w0.Settlements[j].SiteCell, kmPerCost, out double d));
                if (Math.Exp(-d / range) > 0.0) coupled++;
            }
        Assert.Equal(CoupledPairsAtFounding, coupled);                      // vacuity: ≥ 2 settlements share weather ON
        Assert.Equal(0, w0.SettlementDistances.Count);                      // and the travel-cost kernel has nothing to read
        Assert.Equal(0, R.SeamCrossingPairsAll(w0));                        // seam exposure: none here (the seam rig has it)

        // What the layer reads (T2 fix round, finding T2-V2): the step 0 -> 1 the pipeline took is the documented model's
        // with the kernel exp(−d / range) over the straight-line distance, recomputed test-side bit for bit — a kernel that
        // ignores distance, or reads it at another scale, publishes other weather.
        double dtYears = R.Eras().DtDaysAt(w0.Clock.SimDays) / (double)SimClock.YearDays;
        HarvestWeatherReference.AssertStepIs(w0, _arms.Live.At(FirstAction), on, dtYears, "ON (geographic kernel)");

        // What the layer does, read off the weather step itself: the geographic kernel reads no road. On world 1 (the first
        // world with travel costs) every travel cost doubled, halved or made unreachable leaves ON's next weather unchanged,
        // while the travel-cost kernel's weather moves at every settlement.
        WorldState w1 = _arms.Live.At(FirstAction);
        Assert.Equal(DistanceRowsAtWorld1, w1.SettlementDistances.Count);
        Assert.Equal((0, 0, 0), RoadPerturbedWeatherRows(w1, on));
        Assert.Equal((WeatherRows, WeatherRows, WeatherRows), RoadPerturbedWeatherRows(w1, off));
        AttributeAtTheFirstStep(Twin(off, "weather-geography-off"), WeatherWrites);
    }

    /// <summary>#11 — R2b's UNREST / DIGNITY layer (needs.json: Dignity bound to the tax burden, the unrest section). First
    /// action: the Dignity need's NeedSatisfaction rows — published from the first step ON (one per settlement), absent with
    /// the layer stripped — so world 1 is the first to differ, in the needs and grievance tables alone.</summary>
    [Fact]
    public void UnrestDignity_OnTheShippedCylinder_DivergesAtTheFirstStep_ThroughTheDignityRows()
    {
        SimConfig on = _arms.On, off = TestConfigs.PreUnrest(on);
        int dignity = -1;
        foreach (NeedEntry n in on.Needs!.Needs) if (n.FromTaxBurden) { dignity = n.Id; Assert.True(n.Bound); }
        Assert.True(dignity >= 0, "no need is bound to the tax burden");
        foreach (NeedEntry n in off.Needs!.Needs) if (n.Id == dignity) Assert.False(n.Bound);
        Assert.NotNull(on.Needs.Unrest);
        Assert.Null(off.Needs.Unrest);

        R.Arm twin = Twin(off, "unrest-off");
        Assert.Equal(DignityRowsOn, NeedRows(_arms.Live.At(FirstAction), dignity));   // the first action: the Dignity rows
        Assert.Equal(0, NeedRows(twin.At(FirstAction), dignity));
        Assert.Equal(0, NeedRows(_arms.Live.At(0), dignity));                        // world 0 has none in either arm
        AttributeAtTheFirstStep(twin, UnrestWrites);
    }

    /// <summary>#12 — R2b's AGE-MILITARY REALIZATION (ages.json: each Age's Formations milestone counts only its listed unit
    /// identities). First action: the formations identity set — the founding warband's identity is not one Age 2's
    /// Formations milestone counts, so the AgeEligibility row the first step publishes holds that milestone unmet ON and met
    /// with the layer stripped (any identity counts) — world 1 differs in that row alone.</summary>
    [Fact]
    public void AgeMilitary_OnTheShippedCylinder_DivergesAtTheFirstStep_InTheEligibilityRowAlone()
    {
        SimConfig on = _arms.On, off = TestConfigs.PreAgeMilitary(on);
        WorldState w0 = _arms.Live.At(0);
        Assert.Equal(1, w0.MilitaryUnits.Count);
        Assert.Equal(FoundingWarbandIdentity, w0.MilitaryUnits[0].Identity);
        int formations = 0;
        Assert.Equal(2, on.Ages!.Ages[1].Key);
        foreach (var m in on.Ages.Ages[1].Entry!.Supporting)
        {
            if (m.Fact.Kind != Sim.Core.Systems.Ages.MilestoneFactKind.Formations) continue;
            formations++;
            Assert.DoesNotContain(FoundingWarbandIdentity, m.Fact.IdentityKeys!);   // the warband does not count ON
        }
        Assert.Equal(1, formations);
        foreach (var m in off.Ages!.Ages[1].Entry!.Supporting)
            if (m.Fact.Kind == Sim.Core.Systems.Ages.MilestoneFactKind.Formations) Assert.Null(m.Fact.IdentityKeys);

        R.Arm twin = Twin(off, "age-military-off");
        AgeEligibilityRow rowOn = _arms.Live.At(FirstAction).AgeEligibility[0], rowOff = twin.At(FirstAction).AgeEligibility[0];
        Assert.Equal((2, 0L), (rowOn.NextAge, rowOn.EvaluatedTurn));
        Assert.Equal((0, 1), (rowOn.SupportingMet, rowOff.SupportingMet));          // the first action, read off the row
        Assert.Equal(rowOn with { SupportingMet = rowOff.SupportingMet, CategoryMask = rowOff.CategoryMask }, rowOff);
        AttributeAtTheFirstStep(twin, AgeMilitaryWrites);
    }

    /// <summary>How many of <paramref name="w"/>'s weather rows carry the POINT VALUE of their own log deviation,
    /// Multiplier == exp(LogDeviation − σ²/2), bit for bit.</summary>
    private static int PointValueRows(IReadOnlyWorldState w, double sigma)
    {
        double meanCorrection = 0.5 * sigma * sigma;
        int n = 0;
        for (int i = 0; i < w.HarvestWeather.Count; i++)
        {
            HarvestWeatherRow r = w.HarvestWeather[i];
            if (BitConverter.DoubleToInt64Bits(r.Multiplier) == BitConverter.DoubleToInt64Bits(Math.Exp(r.LogDeviation - meanCorrection))) n++;
        }
        return n;
    }

    private static int NeedRows(IReadOnlyWorldState w, int need)
    {
        int n = 0;
        for (int i = 0; i < w.NeedSatisfactions.Count; i++) if (w.NeedSatisfactions[i].NeedId == need) n++;
        return n;
    }

    private static long Harvested(IReadOnlyWorldState w, SettlementId s, int grain)
    {
        int i = GoodStockIndex.IndexOf(w.GoodStocks, s, new GoodId(grain));
        Assert.True(i >= 0, $"settlement {s.Value} has no grain row");
        return w.GoodStocks[i].LastProducedUnits;
    }

    /// <summary>The draws each of the weather system's RNG streams took in the step <paramref name="w0"/> → <paramref name="w1"/>,
    /// by region ascending: each stream is replayed from its state on <paramref name="w0"/> (or its seeded start, when
    /// <paramref name="w0"/> has no row for it — RngRegistry's own rule) until it reaches its state on <paramref name="w1"/>.</summary>
    private static (int Region, long Draws)[] WeatherDraws(WorldState w0, WorldState w1)
    {
        const long MaxDraws = 1_000_000;
        var draws = new List<(int Region, long Draws)>();
        for (int i = 0; i < w1.RngStreams.Count; i++)
        {
            RngStreamRow target = w1.RngStreams[i];
            if (target.System != HarvestWeatherSystem.WellKnownId) continue;
            WorldState scratch = w0.Clone();
            RngStream stream = new RngRegistry(scratch).Get(target.System, target.Region);
            int row = -1;
            for (int k = 0; k < scratch.RngStreams.Count; k++)
                if (scratch.RngStreams[k].System == target.System && scratch.RngStreams[k].Region == target.Region) row = k;
            long n = 0;
            while (scratch.RngStreams[row].State != target.State && n < MaxDraws)
            {
                stream.NextUInt32();
                n++;
            }
            Assert.True(n < MaxDraws, $"region {target.Region.Value}: the stream never reached its world-1 state");
            draws.Add((target.Region.Value, n));
        }
        draws.Sort(static (a, b) => a.Region.CompareTo(b.Region));
        return draws.ToArray();
    }

    /// <summary>How many of the next weather step's rows change when every travel cost of <paramref name="w1"/> is doubled,
    /// halved, or made unreachable — the weather system stepped alone under <paramref name="cfg"/>.</summary>
    private static (int Doubled, int Halved, int Unreachable) RoadPerturbedWeatherRows(WorldState w1, SimConfig cfg)
    {
        using var era = Sim.Data.DataFiles.OpenEraPacing();
        var weather = new TurnExecutor(EraTableLoader.Load(era), [SystemCatalog.HarvestWeather(cfg)]);
        WorldState plain = weather.Step(w1.Clone());
        int Changed(Func<double, double> f)
        {
            WorldState p = w1.Clone();
            for (int i = 0; i < p.SettlementDistances.Count; i++)
                p.SettlementDistances[i] = p.SettlementDistances[i] with { TravelCost = f(p.SettlementDistances[i].TravelCost) };
            WorldState stepped = weather.Step(p);
            Assert.Equal(plain.HarvestWeather.Count, stepped.HarvestWeather.Count);
            int n = 0;
            for (int i = 0; i < plain.HarvestWeather.Count; i++) if (plain.HarvestWeather[i] != stepped.HarvestWeather[i]) n++;
            return n;
        }
        return (Changed(static d => d * 2.0), Changed(static d => d / 2.0), Changed(static _ => double.PositiveInfinity));
    }

    private static long Produced(IReadOnlyWorldState w, GoodId good)
    {
        long units = 0;
        for (int i = 0; i < w.GoodStocks.Count; i++) if (w.GoodStocks[i].Good == good) units += w.GoodStocks[i].LastProducedUnits;
        return units;
    }
}
