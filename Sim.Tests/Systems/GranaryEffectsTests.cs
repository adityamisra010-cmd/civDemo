using System.Text;
using Sim.Core;
using Sim.Core.Kernel;
using Sim.Core.Observability;
using Sim.Core.State;
using Sim.Core.Systems;
using Sim.Core.Worldgen;
using Sim.Tests.TestUtil;

namespace Sim.Tests.Systems;

/// <summary>
/// M5 item 5 — GRANARIES (sim.json <c>granaries</c>, State.GranaryEffects). Two distinct, bounded, saturating effects:
/// the LOCAL farm-output factor ProductionSystem.Farm applies to the settlement's realised grain rate, and the
/// EMPIRE-WIDE famine-intensity factor the effective deficit carries into DemographicsSystem. Pinned here: the closed
/// forms at 0, 1, 2, 5 and 50 granaries; diminishing increments and their measured strict horizon; saturation and the
/// cap never exceeded; the counts (local, empire-wide, stateless); local-only farm output on CULTIVATED grain only (a
/// wild-food settlement is bit-identical to the switch-off world); empire-wide famine reduction (a sibling gains, a rival
/// and a stateless neighbour do not); the effective deficit never negative and never raised; births past the
/// suppression clamp unrestored; the causal chain's "not applied" reading; the systems' once-resolved binding equal to
/// the config seams; zero granaries and the switch off leave Production and Demographics bit-identical; the loader's
/// refusals; deterministic replay of an AI world.
/// </summary>
public class GranaryEffectsTests
{
    private static readonly SimConfig On = TestConfigs.Sim();
    private static readonly SimConfig Off = TestConfigs.PreGranaryEffects(On);
    private static GranariesConfig G => On.Granaries!;

    // The dev world (256², four settlements) with one AI empire: round-robin control — s0 and s2 the player's
    // (PolityId 1), s1 and s3 the AI's (PolityId 2). Run three production turns so catchments, harvests and the
    // weather rows exist.
    private static readonly Lazy<WorldState> DevAt3 = new(() =>
    {
        SimConfig cfg = On;
        WorldState w = WorldFounding.Found(TestConfigs.DevWorldgen() with { AiEmpires = 1 }, cfg, 42);
        return UniversityRigs.Production(cfg).Run(w, 3);
    });

    private static int GranaryId => GranaryEffects.ProjectId(On);

    private static int WorkshopId
    {
        get
        {
            foreach (ConstructionProjectEntry p in On.Goods!.Projects!) if (p.Name == "workshop") return p.Id;
            throw new InvalidOperationException("no workshop project");
        }
    }

    private static SettlementId S(int i) => new(i);

    private static WorldState Dev()
    {
        WorldState w = DevAt3.Value.Clone();
        Assert.Equal(4, w.Settlements.Count);
        for (int i = 0; i < 4; i++)
        {
            Assert.True(EmpireQuery.TryGetController(w, w.Settlements[i].Id, out PolityId p));
            Assert.Equal(i % 2 == 0 ? 1 : 2, p.Value);
        }
        for (int i = 0; i < w.Structures.Count; i++) Assert.NotEqual(GranaryId, w.Structures[i].ProjectId);   // none built yet
        return w;
    }

    private static void Build(WorldState w, SettlementId s, int project, long count) =>
        w.Structures.Add(new StructureRow(s, project, count));

    private static void MakeStateless(WorldState w, SettlementId s)
    {
        var keep = new List<ControlRow>();
        for (int i = 0; i < w.Controls.Count; i++)
            if (w.Controls[i].Place != s) keep.Add(w.Controls[i]);
        w.Controls.Clear();
        foreach (ControlRow row in keep) w.Controls.Add(row);
        Assert.False(EmpireQuery.TryGetController(w, s, out _));
    }

    private static long Population(IReadOnlyWorldState w, SettlementId s)
    {
        long n = 0;
        for (int i = 0; i < w.Buckets.Count; i++) if (w.Buckets[i].Settlement == s) n += w.Buckets[i].Count.Value;
        return n;
    }

    private static GoodStockRow Grain(IReadOnlyWorldState w, SettlementId s)
    {
        int i = GoodStockIndex.IndexOf(w.GoodStocks, s, new GoodId(On.Goods!.GrainId));
        Assert.True(i >= 0);
        return w.GoodStocks[i];
    }

    private static bool SameRowsAt(IReadOnlyWorldState a, IReadOnlyWorldState b, SettlementId s)
    {
        var x = new List<BucketRow>();
        var y = new List<BucketRow>();
        for (int i = 0; i < a.Buckets.Count; i++) if (a.Buckets[i].Settlement == s) x.Add(a.Buckets[i]);
        for (int i = 0; i < b.Buckets.Count; i++) if (b.Buckets[i].Settlement == s) y.Add(b.Buckets[i]);
        return x.SequenceEqual(y);
    }

    private static WorldState StepOnly(WorldState w, SimConfig cfg, params SystemRegistration[] systems) =>
        UniversityRigs.Only(new OrderLog(), 10.0, systems).Step(w);

    // ================================================================== the closed forms

    private static double FarmClosedForm(long m) =>
        m > 0 ? 1.0 + G.FarmYieldBonusMax * (1.0 - Math.Exp(-(m / G.FarmYieldScale))) : 1.0;

    private static double FamineClosedForm(long n) =>
        n > 0 ? 1.0 - G.FamineReductionMax * (1.0 - Math.Exp(-(n / G.FamineReductionScale))) : 1.0;

    [Fact]
    public void TheShippedCoefficients_AreTheDocumentedConservativeOnes()
    {
        Assert.True(G.Enabled);
        Assert.Equal("granary", G.Project);
        Assert.Equal(0.05, G.FarmYieldBonusMax);   // C2: half of ADR-013's in-chain x0.90 loss, rounded down (sim.json _doc)
        Assert.Equal(1.0, G.FarmYieldScale);
        Assert.Equal(0.25, G.FamineReductionMax);
        Assert.Equal(2.0, G.FamineReductionScale);
        // The project resolves FROM CONTENT by name (goods.json), never a code literal.
        Assert.Equal("granary", On.Goods!.ProjectById(GranaryId)!.Name);
        // The famine-class disaster hazard and the store's physical bounds are untouched by this layer.
        Assert.Equal(0.0, On.Disaster.HazardPerYear);
        Assert.Equal(0.08, On.Consumption.GrainSpoilagePerYear);
        Assert.Equal(1.5, On.Consumption.GranaryYearsOfDemand);
    }

    [Theory]
    [InlineData(0L)]
    [InlineData(1L)]
    [InlineData(2L)]
    [InlineData(5L)]
    [InlineData(50L)]
    public void BothFactors_AreTheExactClosedForm_AtZeroOneTwoFiveAndFifty(long count)
    {
        Assert.Equal(FarmClosedForm(count), GranaryEffects.FarmYieldFactor(G, count));
        Assert.Equal(FamineClosedForm(count), GranaryEffects.FamineIntensityFactor(G, count));
        if (count == 0)
        {
            // Zero granaries: the literal identity, so x * 1.0 is never even computed (the systems skip it).
            Assert.Equal(1.0, GranaryEffects.FarmYieldFactor(G, 0));
            Assert.Equal(1.0, GranaryEffects.FamineIntensityFactor(G, 0));
        }
    }

    [Fact]
    public void TheReadings_AtOneTwoFiveAndFifty_AreTheDocumentedPercentages()
    {
        // sim.json granaries._doc states these; rounded to the displayed precision only.
        Assert.Equal("3.2", GranaryEffects.FarmBonusPercent(G, 1));
        Assert.Equal("4.3", GranaryEffects.FarmBonusPercent(G, 2));
        Assert.Equal("4.8", GranaryEffects.FarmBonusPercent(G, 3));
        Assert.Equal("5.0", GranaryEffects.FarmBonusPercent(G, 5));
        Assert.Equal("5.0", GranaryEffects.FarmBonusPercent(G, 50));
        Assert.Equal("9.8", GranaryEffects.FamineReductionPercent(G, 1));
        Assert.Equal("15.8", GranaryEffects.FamineReductionPercent(G, 2));
        Assert.Equal("22.9", GranaryEffects.FamineReductionPercent(G, 5));
        Assert.Equal("23.8", GranaryEffects.FamineReductionPercent(G, 6));
        Assert.Equal("25.0", GranaryEffects.FamineReductionPercent(G, 50));
    }

    [Fact]
    public void EachGranary_AddsLessThanTheOneBefore_AndNeitherFactorEverPassesItsCap()
    {
        double farmCap = 1.0 + G.FarmYieldBonusMax, famineFloor = 1.0 - G.FamineReductionMax;
        double prevFarmStep = double.PositiveInfinity, prevFamineStep = double.PositiveInfinity;
        for (long k = 0; k < 30; k++)
        {
            double farmStep = GranaryEffects.FarmYieldFactor(G, k + 1) - GranaryEffects.FarmYieldFactor(G, k);
            double famineStep = GranaryEffects.FamineIntensityFactor(G, k) - GranaryEffects.FamineIntensityFactor(G, k + 1);
            Assert.True(farmStep > 0.0, $"farm step {k}->{k + 1} must be positive");
            Assert.True(famineStep > 0.0, $"famine step {k}->{k + 1} must be positive");
            Assert.True(farmStep < prevFarmStep, $"farm: granary {k + 1} must add less than granary {k}");
            Assert.True(famineStep < prevFamineStep, $"famine: granary {k + 1} must add less than granary {k}");
            prevFarmStep = farmStep;
            prevFamineStep = famineStep;
        }
        // Saturation BELOW the cap at the counts a civilization plausibly builds...
        foreach (long k in new long[] { 1, 2, 5, 10 })
        {
            Assert.True(GranaryEffects.FarmYieldFactor(G, k) < farmCap);
            Assert.True(GranaryEffects.FamineIntensityFactor(G, k) > famineFloor);
        }
        Assert.True(GranaryEffects.FarmYieldFactor(G, 3) - 1.0 >= 0.95 * G.FarmYieldBonusMax);        // three granaries: SATURATED
        Assert.True(GranaryEffects.FarmYieldFactor(G, 2) - 1.0 < 0.95 * G.FarmYieldBonusMax);
        // ...and the cap is NEVER exceeded, however many are built (at very large counts e^-x vanishes below the factor's
        // precision and the factor reaches the cap in double precision — equal, never beyond).
        foreach (long k in new long[] { 50, 100, 1_000, 1_000_000, long.MaxValue / 2 })
        {
            Assert.True(GranaryEffects.FarmYieldFactor(G, k) <= farmCap);
            Assert.True(GranaryEffects.FamineIntensityFactor(G, k) >= famineFloor);
            Assert.True(GranaryEffects.FamineIntensityFactor(G, k) > 0.0);
        }
        Assert.True(GranaryEffects.FamineIntensityFactor(G, 50) > famineFloor);   // still strictly above the floor at 50
    }

    [Fact]
    public void TheStrictDiminishingHorizon_IsMeasuredOnTheFactorItself_AndBeyondItNothingShows()
    {
        // The cards say "strictly less below about N, nothing that shows after" with N computed by these two functions
        // (C4(c)): pinned at the shipped coefficients, and checked against the definition step by step.
        long nFarm = GranaryEffects.FarmStrictlyLessBelow(G), nFamine = GranaryEffects.FamineStrictlyLessBelow(G);
        Assert.Equal(35, nFarm);
        Assert.Equal(69, nFamine);
        double FarmStep(long k) => GranaryEffects.FarmYieldFactor(G, k) - GranaryEffects.FarmYieldFactor(G, k - 1);
        double FamineStep(long k) => GranaryEffects.FamineIntensityFactor(G, k - 1) - GranaryEffects.FamineIntensityFactor(G, k);
        for (long k = 2; k < nFarm; k++) Assert.True(FarmStep(k) < FarmStep(k - 1), $"farm granary #{k} must add strictly less");
        for (long k = 2; k < nFamine; k++) Assert.True(FamineStep(k) < FamineStep(k - 1), $"famine granary #{k} must reduce strictly less");
        Assert.False(FarmStep(nFarm) < FarmStep(nFarm - 1));
        Assert.False(FamineStep(nFamine) < FamineStep(nFamine - 1));
        // "Nothing that shows": from N on, no granary moves either factor by as much as a millionth of a percent, and the
        // displayed percentage is the ceiling's.
        for (long k = nFarm; k < nFarm + 200; k++) Assert.True(Math.Abs(FarmStep(k)) < 1e-8, $"farm granary #{k}");
        for (long k = nFamine; k < nFamine + 200; k++) Assert.True(Math.Abs(FamineStep(k)) < 1e-8, $"famine granary #{k}");
        Assert.Equal(GranaryEffects.Percent(G.FarmYieldBonusMax), GranaryEffects.FarmBonusPercent(G, nFarm));
        Assert.Equal(GranaryEffects.Percent(G.FamineReductionMax), GranaryEffects.FamineReductionPercent(G, nFamine));
        // A flat factor (no bonus) has no strictly-diminishing range at all: the second granary already adds no less.
        Assert.Equal(2, GranaryEffects.FarmStrictlyLessBelow(G with { FarmYieldBonusMax = 0.0 }));
        Assert.Equal(2, GranaryEffects.FamineStrictlyLessBelow(G with { FamineReductionMax = 0.0 }));
    }

    // ================================================================== the counts

    [Fact]
    public void LocalCounts_TheSettlement_EmpireCounts_EveryControlledSettlement_AndAStatelessOne_OnlyItsOwn()
    {
        WorldState w = Dev();
        Build(w, S(0), GranaryId, 2);
        Build(w, S(2), GranaryId, 1);
        Build(w, S(1), GranaryId, 1);
        Build(w, S(2), WorkshopId, 4);   // not a granary: counts toward neither
        Assert.Equal(2, GranaryEffects.LocalGranaries(w, On, S(0)));
        Assert.Equal(1, GranaryEffects.LocalGranaries(w, On, S(2)));
        Assert.Equal(0, GranaryEffects.LocalGranaries(w, On, S(3)));
        Assert.Equal(3, GranaryEffects.EmpireGranaries(w, On, S(0)));   // player: s0 (2) + s2 (1)
        Assert.Equal(3, GranaryEffects.EmpireGranaries(w, On, S(2)));
        Assert.Equal(1, GranaryEffects.EmpireGranaries(w, On, S(1)));   // AI: s1 (1) + s3 (0)
        Assert.Equal(1, GranaryEffects.EmpireGranaries(w, On, S(3)));

        // s3 loses its controller: it is its own civilization, and the AI's count no longer includes it.
        MakeStateless(w, S(3));
        Assert.Equal(0, GranaryEffects.EmpireGranaries(w, On, S(3)));
        Build(w, S(3), GranaryId, 1);
        Assert.Equal(1, GranaryEffects.EmpireGranaries(w, On, S(3)));
        Assert.Equal(1, GranaryEffects.EmpireGranaries(w, On, S(1)));   // s3's granary is not the AI's

        // The counts are a function of the row SET: the same rows in reverse order give the same answers.
        WorldState reversed = w.Clone();
        reversed.Structures.Clear();
        for (int i = w.Structures.Count - 1; i >= 0; i--) reversed.Structures.Add(w.Structures[i]);
        for (int s = 0; s < 4; s++)
        {
            Assert.Equal(GranaryEffects.LocalGranaries(w, On, S(s)), GranaryEffects.LocalGranaries(reversed, On, S(s)));
            Assert.Equal(GranaryEffects.EmpireGranaries(w, On, S(s)), GranaryEffects.EmpireGranaries(reversed, On, S(s)));
        }
    }

    [Fact]
    public void TheSwitchOff_TheAbsentSection_AndAnUnresolvedProject_AreAllInert()
    {
        WorldState w = KnowFarming(Dev(), Player);   // cultivating: the farm factor is live where a granary stands (C1)
        Build(w, S(0), GranaryId, 3);
        Assert.True(GranaryEffects.FarmYieldFactor(w, On, S(0)) > 1.0);
        Assert.True(GranaryEffects.FamineIntensityFactor(w, On, S(2)) < 1.0);

        SimConfig absent = On with { Granaries = null };
        SimConfig unresolved = On with { Granaries = G with { Project = "no_such_project" } };
        foreach (SimConfig cfg in new[] { Off, absent, unresolved })
            for (int s = 0; s < 4; s++)
            {
                Assert.Equal(1.0, GranaryEffects.FarmYieldFactor(w, cfg, S(s)));
                Assert.Equal(1.0, GranaryEffects.FamineIntensityFactor(w, cfg, S(s)));
            }
        Assert.Null(GranaryEffects.Active(Off));
        Assert.Equal(-1, GranaryEffects.ProjectId(absent));
        Assert.Equal(-1, GranaryEffects.ProjectId(unresolved));
    }

    // ================================================================== LOCAL farm output

    [Fact]
    public void AGranaryInA_RaisesAsHarvestByExactlyItsFactor_AndLeavesEverySiblingAndRivalBitIdentical()
    {
        // Both polities CULTIVATE (the factor is a cultivation effect, C1), so the siblings' and rivals' bit-identity
        // below is locality, not wild food.
        WorldState w = KnowFarming(KnowFarming(Dev(), Player), Rival);
        for (int s = 0; s < 4; s++) Assert.False(LabourActivities.HarvestsWildFood(w, On, S(s)));
        foreach (long m in new long[] { 1, 2, 5 })
        {
            WorldState rig = w.Clone();
            Build(rig, S(0), GranaryId, m);
            WorldState on = StepOnly(rig, On, SystemCatalog.Production(On));
            WorldState off = StepOnly(rig, Off, SystemCatalog.Production(Off));

            // A's realised rate is the switched-off rate times the factor: exact = harvested + remainder (D-004), and
            // the rate part is exact - the prior remainder. A double ratio, not a stock: compared to the closed form.
            GoodStockRow before = Grain(rig, S(0)), a = Grain(on, S(0)), b = Grain(off, S(0));
            double rateOn = a.Amount.Value - before.Amount.Value + a.ProduceRemainder - before.ProduceRemainder;
            double rateOff = b.Amount.Value - before.Amount.Value + b.ProduceRemainder - before.ProduceRemainder;
            Assert.Equal(a.LastProducedUnits, a.Amount.Value - before.Amount.Value);
            Assert.True(b.LastProducedUnits > 100, $"the rig must harvest something ({b.LastProducedUnits})");
            Assert.True(a.LastProducedUnits > b.LastProducedUnits);
            Assert.Equal(GranaryEffects.FarmYieldFactor(G, m), rateOn / rateOff, 12);

            // LOCAL: the sibling s2 (same empire), and the rivals s1, s3: every good stock row bit-identical.
            for (int i = 0; i < on.GoodStocks.Count; i++)
            {
                if (on.GoodStocks[i].Settlement == S(0) && on.GoodStocks[i].Good.Value == On.Goods!.GrainId) continue;
                Assert.Equal(off.GoodStocks[i], on.GoodStocks[i]);
            }
        }
    }

    [Fact]
    public void AGranaryInASibling_DoesNotRaiseAsHarvest()
    {
        WorldState w = KnowFarming(Dev(), Player);   // cultivating, so the factor applies where a granary stands
        Build(w, S(2), GranaryId, 3);    // the player's OTHER settlement
        WorldState on = StepOnly(w, On, SystemCatalog.Production(On));
        WorldState off = StepOnly(w, Off, SystemCatalog.Production(Off));
        Assert.Equal(Grain(off, S(0)), Grain(on, S(0)));
        Assert.True(Grain(on, S(2)).LastProducedUnits > Grain(off, S(2)).LastProducedUnits);
    }

    // ================================================================== a CULTIVATION effect: never wild food (C1)

    private static readonly PolityId Player = new(1), Rival = new(2);

    /// <summary>The farming capability for one polity (root_crop: a depth-0 node that makes activity.farming
    /// knowledge-eligible) — its settlements then grow CULTIVATED grain (LabourActivities.HarvestsWildFood false).</summary>
    private static WorldState KnowFarming(WorldState w, PolityId polity)
    {
        UniversityRigs.Grant(w, On.Research!, polity, "root_crop");
        return w;
    }

    [Fact]
    public void APreCultivationSettlement_WithGranaries_HarvestsBitIdenticallyToTheSwitchOffWorld()
    {
        // At turn 3 no polity knows farming: every settlement's Farming sector gathers WILD FOOD at the forager rates
        // (the same predicate ProductionSystem.Farm reads for the yield choice). A granary saves part of a CROP between
        // the threshing floor and the store; gathering is not that crop, so the factor must not touch it.
        WorldState w = Dev();
        for (int s = 0; s < 4; s++)
            Assert.True(LabourActivities.HarvestsWildFood(w, On, S(s)), $"settlement {s} must still gather wild food at turn 3");
        Build(w, S(0), GranaryId, 3);
        Build(w, S(1), GranaryId, 1);
        WorldState on = StepOnly(w, On, SystemCatalog.Production(On));
        WorldState off = StepOnly(w, Off, SystemCatalog.Production(Off));
        Assert.True(Grain(on, S(0)).LastProducedUnits > 100, $"the rig must gather something ({Grain(on, S(0)).LastProducedUnits})");
        Assert.True(WorldStates.StateEquals(on, off));
        Assert.Equal(WorldHash.ComputeHex(off), WorldHash.ComputeHex(on));
        // The seam the causal chain prints agrees: 1, not applied.
        Assert.Equal(1.0, GranaryEffects.FarmYieldFactor(w, On, S(0)));
        Assert.Equal(1.0, GranaryEffects.FarmYieldFactor(w, On, S(1)));
    }

    [Fact]
    public void ASettlementThatKnowsFarming_GetsExactlyTheFactorOnItsCultivatedGrain_WhileAWildFoodRivalGetsNone()
    {
        WorldState w = KnowFarming(Dev(), Player);
        Assert.False(LabourActivities.HarvestsWildFood(w, On, S(0)));   // the player's settlements cultivate...
        Assert.True(LabourActivities.HarvestsWildFood(w, On, S(1)));    // ...the rival's still gather
        Build(w, S(0), GranaryId, 2);
        Build(w, S(1), GranaryId, 2);
        WorldState on = StepOnly(w, On, SystemCatalog.Production(On));
        WorldState off = StepOnly(w, Off, SystemCatalog.Production(Off));

        GoodStockRow before = Grain(w, S(0)), a = Grain(on, S(0)), b = Grain(off, S(0));
        double rateOn = a.Amount.Value - before.Amount.Value + a.ProduceRemainder - before.ProduceRemainder;
        double rateOff = b.Amount.Value - before.Amount.Value + b.ProduceRemainder - before.ProduceRemainder;
        Assert.True(b.LastProducedUnits > 100, $"the rig must harvest something ({b.LastProducedUnits})");
        Assert.True(a.LastProducedUnits > b.LastProducedUnits);
        Assert.Equal(GranaryEffects.FarmYieldFactor(G, 2), rateOn / rateOff, 12);
        Assert.Equal(GranaryEffects.FarmYieldFactor(G, 2), GranaryEffects.FarmYieldFactor(w, On, S(0)));

        // The rival's wild-food harvest with the same two granaries: bit-identical, factor 1.
        Assert.Equal(Grain(off, S(1)), Grain(on, S(1)));
        Assert.Equal(1.0, GranaryEffects.FarmYieldFactor(w, On, S(1)));
    }

    // ================================================================== EMPIRE-WIDE famine

    /// <summary>Every settlement under the SAME SEVERE deficit (d = 0.6 with a = 0.2: dEff = 0.5), no disaster, food
    /// labour not abandoned.</summary>
    private static WorldState Severe(WorldState w)
    {
        w.ConsumptionDeficits.Clear();
        for (int s = 0; s < w.Settlements.Count; s++)
            w.ConsumptionDeficits.Add(new ConsumptionDeficitRow(w.Settlements[s].Id, 0.6, 1_000_000));
        for (int s = 0; s < w.Settlements.Count; s++)
            Assert.Equal(FoodStateKind.Severe, FoodState.Of(w, w.Settlements[s].Id, On, out _));
        return w;
    }

    [Fact]
    public void AGranaryInA_ReducesStarvationInSiblingB_UnderTheSameDeficit_ButNotInAnotherEmpire()
    {
        WorldState w = Severe(Dev());
        Build(w, S(0), GranaryId, 1);
        double pure = FoodState.EffectiveDeficit(0.6, FoodStateKind.Severe, On);
        Assert.Equal(0.5, pure, 12);

        // The effective deficit the kernel reads: reduced by the EMPIRE's factor in A and in sibling B (s2, no granary
        // of its own); untouched in the rival empire's settlements.
        double f = GranaryEffects.FamineIntensityFactor(G, 1);
        Assert.Equal(pure * f, FoodState.EffectiveDeficit(w, S(0), 0.6, FoodStateKind.Severe, On));
        Assert.Equal(pure * f, FoodState.EffectiveDeficit(w, S(2), 0.6, FoodStateKind.Severe, On));
        Assert.Equal(pure, FoodState.EffectiveDeficit(w, S(1), 0.6, FoodStateKind.Severe, On));
        Assert.Equal(pure, FoodState.EffectiveDeficit(w, S(3), 0.6, FoodStateKind.Severe, On));

        WorldState on = StepOnly(w, On, SystemCatalog.Demographics(On));
        WorldState off = StepOnly(w, Off, SystemCatalog.Demographics(Off));
        Assert.True(Population(on, S(2)) > Population(off, S(2)),
            $"sibling B must starve less ({Population(on, S(2))} vs {Population(off, S(2))})");
        Assert.True(Population(on, S(0)) > Population(off, S(0)));
        Assert.True(SameRowsAt(on, off, S(1)), "a rival empire's settlement must be bit-identical");
        Assert.True(SameRowsAt(on, off, S(3)), "a rival empire's settlement must be bit-identical");
        // The nominal deficit and the classification are not this layer's: Demographics wrote neither.
        for (int s = 0; s < 4; s++) Assert.Equal(FoodState.Of(off, S(s), On, out _), FoodState.Of(on, S(s), On, out _));
    }

    [Fact]
    public void AStatelessSettlement_CountsOnlyItsOwnGranaries_AndNoEmpireCountsIt()
    {
        // (a) A granary at stateless s3 relieves s3 alone — not s1, the AI settlement that used to share its empire.
        WorldState w = Severe(Dev());
        MakeStateless(w, S(3));
        Build(w, S(3), GranaryId, 1);
        WorldState on = StepOnly(w, On, SystemCatalog.Demographics(On));
        WorldState off = StepOnly(w, Off, SystemCatalog.Demographics(Off));
        Assert.True(Population(on, S(3)) > Population(off, S(3)));
        for (int s = 0; s < 3; s++) Assert.True(SameRowsAt(on, off, S(s)), $"settlement {s} must be bit-identical");

        // (b) A granary at s1 (the AI's) does not relieve the stateless s3.
        WorldState v = Severe(Dev());
        MakeStateless(v, S(3));
        Build(v, S(1), GranaryId, 1);
        WorldState on2 = StepOnly(v, On, SystemCatalog.Demographics(On));
        WorldState off2 = StepOnly(v, Off, SystemCatalog.Demographics(Off));
        Assert.True(Population(on2, S(1)) > Population(off2, S(1)));
        Assert.True(SameRowsAt(on2, off2, S(3)), "the stateless settlement must be bit-identical");
    }

    [Fact]
    public void TheEffectiveDeficit_IsNeverNegative_NeverRaised_AndExactlyTheDeadZoneFormWithoutAGranary()
    {
        double[] deficits = [0.0, 0.05, 0.2, 0.2000001, 0.35, 0.6, 0.99, 1.0];
        FoodStateKind[] states = [FoodStateKind.Normal, FoodStateKind.Stress, FoodStateKind.Severe, FoodStateKind.Famine];
        foreach (long n in new long[] { 0, 1, 2, 5, 50 })
        {
            WorldState w = Dev();
            if (n > 0) Build(w, S(0), GranaryId, n);
            foreach (double d in deficits)
                foreach (FoodStateKind k in states)
                {
                    double pure = FoodState.EffectiveDeficit(d, k, On);
                    double reduced = FoodState.EffectiveDeficit(w, S(2), d, k, On);   // s2: reached through its empire
                    Assert.True(reduced >= 0.0, $"never negative (n={n}, d={d}, {k})");
                    Assert.True(reduced <= pure, $"never raised (n={n}, d={d}, {k})");
                    if (n == 0 || !(pure > 0.0)) Assert.Equal(pure, reduced);   // bit for bit
                    else Assert.Equal(pure * GranaryEffects.FamineIntensityFactor(G, n), reduced);
                    Assert.Equal(pure, FoodState.EffectiveDeficit(w, S(2), d, k, Off));   // switched off: the dead-zone form
                }
        }
    }

    [Fact]
    public void TheObserverAndTheCausalChain_RecomputeTheEffectiveDeficitTheKernelIntegrated()
    {
        WorldState prev = Severe(Dev());
        Build(prev, S(0), GranaryId, 1);
        WorldState next = StepOnly(prev, On, SystemCatalog.Demographics(On));
        double pure = FoodState.EffectiveDeficit(0.6, FoodStateKind.Severe, On);
        double kernel = FoodState.EffectiveDeficit(prev, S(2), 0.6, FoodStateKind.Severe, On);
        Assert.Equal(pure * GranaryEffects.FamineIntensityFactor(G, 1), kernel);
        Assert.True(kernel < pure);

        // The settlement record's RECOMPUTED effective deficit is the kernel's (granary factor included), not the dead-zone form.
        TurnObservation obs = Sim.Core.Observability.Observer.Observe(prev, next, On, []);
        SettlementRecord rec = Array.Find(obs.Settlements, r => r.Settlement == 2)!;
        Assert.Equal(kernel, rec.FoodState.EffectiveDeficit);
        SettlementRecord rival = Array.Find(obs.Settlements, r => r.Settlement == 1)!;
        Assert.Equal(pure, rival.FoodState.EffectiveDeficit);

        // The causal chain's effective-deficit link is the same value, and says which factor it carries.
        var links = new List<Sim.Core.Observability.Explain.Link>();
        Sim.Core.Observability.Explain.CausalChain.FoodSupply(prev, Sim.Core.Observability.Explain.SourceWorld.Prev, On, S(2), links);
        Sim.Core.Observability.Explain.Link eff = links.Single(l => l.Node == Sim.Core.Observability.Explain.ChainNode.EffectiveDeficit);
        Assert.Equal(kernel, eff.Value);
        Assert.Contains("here n = 1", eff.Note);
    }

    private static SettlementVitalsRow Vitals(IReadOnlyWorldState w, SettlementId s)
    {
        for (int i = 0; i < w.SettlementVitals.Count; i++) if (w.SettlementVitals[i].Settlement == s) return w.SettlementVitals[i];
        throw new InvalidOperationException($"no vitals row for settlement {s.Value}");
    }

    [Fact]
    public void WhereTheShortfallAlreadyHaltsBirths_AGranaryEasesStarvation_ButRestoresNoBirth()
    {
        // Fertility suppression is max(0, 1 - slope x dEff): it CLAMPS at dEff >= 1/slope (1/3 at the shipped 3.0). At
        // d = 0.6 (dEff 0.5) one granary brings the sibling's dEff to 0.5 x 0.9016 = 0.451 - still past the clamp. So the
        // factor eases starvation there but births stay halted in both worlds: lost births do NOT fall by the factor's
        // percentage, and the card must not say they do (GranaryCardTests).
        WorldState w = Severe(Dev());
        Build(w, S(0), GranaryId, 1);
        double dEffOn = FoodState.EffectiveDeficit(w, S(2), 0.6, FoodStateKind.Severe, On);
        Assert.True(dEffOn < FoodState.EffectiveDeficit(0.6, FoodStateKind.Severe, On));
        Assert.True(On.Demographics.FamineFertilitySuppressionSlope * dEffOn >= 1.0, "the rig must sit past the clamp");
        WorldState on = StepOnly(w, On, SystemCatalog.Demographics(On));
        WorldState off = StepOnly(w, Off, SystemCatalog.Demographics(Off));
        Assert.Equal(Vitals(off, S(2)).Births, Vitals(on, S(2)).Births);
        Assert.True(Vitals(on, S(2)).Deaths < Vitals(off, S(2)).Deaths,
            $"starvation must ease ({Vitals(on, S(2)).Deaths} vs {Vitals(off, S(2)).Deaths})");
    }

    private static Sim.Core.Observability.Explain.Link EffectiveDeficitLink(WorldState w, SettlementId s)
    {
        var links = new List<Sim.Core.Observability.Explain.Link>();
        Sim.Core.Observability.Explain.CausalChain.FoodSupply(w, Sim.Core.Observability.Explain.SourceWorld.Prev, On, s, links);
        return links.Single(l => l.Node == Sim.Core.Observability.Explain.ChainNode.EffectiveDeficit);
    }

    [Fact]
    public void TheCausalChain_SaysFactorOneNotApplied_WheneverTheDeadZoneDeficitIsZero_OrNoGranaryCounts()
    {
        WorldState w = Dev();
        Build(w, S(0), GranaryId, 1);
        w.ConsumptionDeficits.Clear();
        w.ConsumptionDeficits.Add(new ConsumptionDeficitRow(S(0), 0.0, 1_000_000));   // NORMAL, n = 1
        w.ConsumptionDeficits.Add(new ConsumptionDeficitRow(S(2), 0.1, 1_000_000));   // STRESS (inside the dead zone), n = 1
        w.ConsumptionDeficits.Add(new ConsumptionDeficitRow(S(1), 0.6, 1_000_000));   // SEVERE, rival empire: n = 0
        w.ConsumptionDeficits.Add(new ConsumptionDeficitRow(S(3), 0.6, 1_000_000));
        Assert.Equal(FoodStateKind.Normal, FoodState.Of(w, S(0), On, out _));
        Assert.Equal(FoodStateKind.Stress, FoodState.Of(w, S(2), On, out _));
        foreach (int s in new[] { 0, 2 })
        {
            Sim.Core.Observability.Explain.Link eff = EffectiveDeficitLink(w, S(s));
            Assert.Equal(0.0, eff.Value);
            Assert.Contains("here n = 1, factor 1 (not applied)", eff.Note);
        }
        Assert.Contains("here n = 0, factor 1 (not applied)", EffectiveDeficitLink(w, S(1)).Note);

        // Applied: a SEVERE settlement in the granary's civilization prints the factor it carries.
        WorldState v = Severe(Dev());
        Build(v, S(0), GranaryId, 1);
        string factor = GranaryEffects.FamineIntensityFactor(G, 1).ToString("0.0000", System.Globalization.CultureInfo.InvariantCulture);
        Sim.Core.Observability.Explain.Link applied = EffectiveDeficitLink(v, S(2));
        Assert.Contains("here n = 1, factor " + factor + ".", applied.Note);
        Assert.DoesNotContain("not applied", applied.Note);
        Assert.Equal(FoodState.EffectiveDeficit(v, S(2), 0.6, FoodStateKind.Severe, On), applied.Value);
    }

    private static string GrainHarvestNote(WorldState w, SettlementId s)
    {
        var links = new List<Sim.Core.Observability.Explain.Link>();
        Sim.Core.Observability.Explain.CausalChain.FoodSupply(w, Sim.Core.Observability.Explain.SourceWorld.Prev, On, s, links);
        return links.Single(l => l.Node == Sim.Core.Observability.Explain.ChainNode.GrainHarvest).Note!;
    }

    [Fact]
    public void TheCausalChain_GrainHarvestNote_PrintsTheFarmFactorOnlyWhereItApplies_AndSaysWhyNotElsewhere()
    {
        WorldState w = KnowFarming(Dev(), Player);   // s0, s2 cultivate; s1, s3 (the rival's) gather wild food
        Build(w, S(0), GranaryId, 1);
        Build(w, S(1), GranaryId, 1);
        string factor = GranaryEffects.FarmYieldFactor(G, 1).ToString("0.0000", System.Globalization.CultureInfo.InvariantCulture);
        Assert.Contains("read from this world: " + factor + ".", GrainHarvestNote(w, S(0)));
        Assert.Contains("read from this world: 1 (not applied: the settlement still gathers wild food).", GrainHarvestNote(w, S(1)));
        Assert.Contains("read from this world: 1 (not applied: no granary stands here).", GrainHarvestNote(w, S(2)));
    }

    [Fact]
    public void TheBindingTheSystemsResolveOnce_GivesTheConfigSeamsValues_AndTheSameEffectiveDeficit()
    {
        // C6: ProductionSystem and DemographicsSystem resolve the project once (GranaryEffects.Bind) and call the
        // binding overloads; the observer, the causal chain and the cards call the config overloads. Same answers.
        GranaryBinding on = GranaryEffects.Bind(On), off = GranaryEffects.Bind(Off);
        Assert.Same(G, on.Active);
        Assert.Equal(GranaryId, on.Project);
        Assert.Null(off.Active);
        Assert.Equal(GranaryId, off.Project);   // the project still resolves; the switch alone turns the effects off
        Assert.Equal(-1, GranaryEffects.Bind(On with { Granaries = null }).Project);

        WorldState w = Severe(KnowFarming(Dev(), Player));
        Build(w, S(0), GranaryId, 2);
        Build(w, S(1), GranaryId, 1);
        for (int s = 0; s < 4; s++)
        {
            bool wild = LabourActivities.HarvestsWildFood(w, On, S(s));
            Assert.Equal(GranaryEffects.FarmYieldFactor(w, On, S(s)), GranaryEffects.FarmYieldFactor(w, on, S(s), wild));
            Assert.Equal(1.0, GranaryEffects.FarmYieldFactor(w, on, S(s), harvestsWildFood: true));
            Assert.Equal(1.0, GranaryEffects.FarmYieldFactor(w, off, S(s), wild));
            Assert.Equal(GranaryEffects.FamineIntensityFactor(w, On, S(s)), GranaryEffects.FamineIntensityFactor(w, on, S(s)));
            Assert.Equal(1.0, GranaryEffects.FamineIntensityFactor(w, off, S(s)));
            Assert.Equal(FoodState.EffectiveDeficit(w, S(s), 0.6, FoodStateKind.Severe, On),
                FoodState.EffectiveDeficit(w, S(s), 0.6, FoodStateKind.Severe, On, on));
        }
        Assert.True(GranaryEffects.FarmYieldFactor(w, on, S(0), false) > 1.0);    // the player's cultivating s0
        Assert.True(GranaryEffects.FamineIntensityFactor(w, on, S(3)) < 1.0);     // the AI's s3 through s1's granary
    }

    // ================================================================== null arms

    [Fact]
    public void WithNoGranary_ProductionAndDemographics_AreBitIdenticalToTheSwitchedOffWorld()
    {
        WorldState w = Severe(Dev());
        Build(w, S(1), WorkshopId, 1);   // another structure is not a granary
        WorldState on = StepOnly(w, On, SystemCatalog.Production(On), SystemCatalog.Demographics(On));
        WorldState off = StepOnly(w, Off, SystemCatalog.Production(Off), SystemCatalog.Demographics(Off));
        Assert.True(WorldStates.StateEquals(on, off));
        Assert.Equal(WorldHash.ComputeHex(off), WorldHash.ComputeHex(on));
    }

    [Fact]
    public void WithTheSwitchOff_AGranaryChangesNothing_InProductionOrDemographics()
    {
        WorldState w = Severe(Dev());
        WorldState with = w.Clone();
        Build(with, S(0), GranaryId, 5);
        Build(with, S(1), GranaryId, 2);
        WorldState a = StepOnly(w, Off, SystemCatalog.Production(Off), SystemCatalog.Demographics(Off));
        WorldState b = StepOnly(with, Off, SystemCatalog.Production(Off), SystemCatalog.Demographics(Off));
        Assert.True(WorldStates.TableEquals(a.GoodStocks, b.GoodStocks));
        Assert.True(WorldStates.TableEquals(a.Buckets, b.Buckets));
        Assert.True(WorldStates.TableEquals(a.SettlementVitals, b.SettlementVitals));
        Assert.True(WorldStates.TableEquals(a.LedgerFlows, b.LedgerFlows));
    }

    // ================================================================== determinism

    [Fact]
    public void AnAiWorldThatBuildsGranaries_ReplaysByteForByte_LiveTwice_AndFromItsOrderLog()
    {
        SimConfig cfg = On;
        WorldgenConfig wg = TestConfigs.DevWorldgen() with { AiEmpires = 1 };
        const int turns = 120;

        var logA = new OrderLog();
        var logB = new OrderLog();
        TurnExecutor ea = UniversityRigs.Production(cfg, logA), eb = UniversityRigs.Production(cfg, logB);
        WorldState a = WorldFounding.Found(wg, cfg, 42), b = WorldFounding.Found(wg, cfg, 42);
        WorldState start = a.Clone();
        bool granaryBuilt = false;
        for (int t = 1; t <= turns; t++)
        {
            AiOrders.Append(logA, a, cfg, TestConfigs.Worldgen());
            AiOrders.Append(logB, b, cfg, TestConfigs.Worldgen());
            a = ea.Step(a);
            b = eb.Step(b);
            Assert.Equal(WorldHash.ComputeHex(a), WorldHash.ComputeHex(b));
            for (int i = 0; i < a.Structures.Count && !granaryBuilt; i++) granaryBuilt = a.Structures[i].ProjectId == GranaryId;
        }
        Assert.True(granaryBuilt, "the AI must have built a granary — otherwise this replay pins nothing about it");

        // Replay: a fresh executor fed the recorded log alone reaches the same world.
        var replayLog = new OrderLog();
        for (int i = 0; i < logA.Count; i++) replayLog.Append(logA[i]);
        WorldState r = UniversityRigs.Production(cfg, replayLog).Run(start, turns);
        Assert.Equal(WorldHash.ComputeHex(a), WorldHash.ComputeHex(r));
    }

    // ================================================================== the loader

    private static SimConfig LoadFull(string sim)
    {
        using var simStream = new MemoryStream(Encoding.UTF8.GetBytes(sim));
        using var needs = Sim.Data.DataFiles.OpenNeeds();
        using var goods = Sim.Data.DataFiles.OpenGoods();
        return SimConfigLoader.Load(simStream, needs, goods);
    }

    [Fact]
    public void TheConfigLoader_RefusesANonsensicalGranariesSection_AndAcceptsTheShippedOne()
    {
        string json = TestConfigs.SimJson();
        Assert.True(LoadFull(json).Granaries!.Enabled);
        string[] bad =
        [
            json.Replace("\"farmYieldBonusMax\": 0.05", "\"farmYieldBonusMax\": -0.01"),
            json.Replace("\"farmYieldBonusMax\": 0.05", "\"farmYieldBonusMax\": 1.0"),
            json.Replace("\"farmYieldScale\": 1.0", "\"farmYieldScale\": 0.0"),
            json.Replace("\"famineReductionMax\": 0.25", "\"famineReductionMax\": 1.0"),
            json.Replace("\"famineReductionMax\": 0.25", "\"famineReductionMax\": -0.25"),
            json.Replace("\"famineReductionScale\": 2.0", "\"famineReductionScale\": -2.0"),
            json.Replace("\"project\": \"granary\"", "\"project\": \"\""),
        ];
        foreach (string b in bad)
        {
            Assert.NotEqual(json, b);   // the replacement must have hit
            Assert.Throws<SimConfigException>(() => SimConfigLoader.Load(b));
        }
        // A project goods.json does not define is refused where sim.json and goods.json meet.
        string unknown = json.Replace("\"project\": \"granary\"", "\"project\": \"silo\"");
        Assert.NotEqual(json, unknown);
        Assert.NotNull(SimConfigLoader.Load(unknown).Granaries);           // sim.json alone cannot know
        Assert.Throws<SimConfigException>(() => LoadFull(unknown));
    }
}
