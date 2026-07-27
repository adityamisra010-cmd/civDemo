using Sim.Core;
using Sim.Core.Kernel;
using Sim.Core.State;
using Sim.Core.Systems;
using Sim.Core.Systems.Consumption;
using Sim.Core.Systems.NeedsGrievance;
using Sim.Core.Worldgen;
using Sim.Tests.TestUtil;

namespace Sim.Tests.Systems;

// T3.5 acceptance (m3 spec §4; D-035 A/B/C, d018:46). The D-035-B mandatory
// acceptance test and the director's named scenario; D-035-A's variety term
// measured rather than asserted; the Tier A gate override; class baskets that
// provably differ; a housing shortage lowering Shelter and raising grievance;
// and the config guards that make sigma < 1 non-negotiable.
//
// The aggregation tests attack NeedsAggregation DIRECTLY rather than through a
// founded world. D-035-B is a claim about an aggregation function — "with one
// need pinned at 1.0 and another at 0.0, aggregate grievance must remain above
// a stated floor for ALL values of the first" — and a claim about arithmetic is
// tested as arithmetic. Routed through a world fixture it would be testable
// only at whatever satisfactions that fixture happened to produce, and fixtures
// are where vacuity hides.
public class NeedsBasketTests
{
    private const ulong Seed = 42;

    private static NeedsConfig Needs() => TestConfigs.Sim().Needs!;

    // ======================================================================
    // D-035-B — NON-COMPENSATORY AGGREGATION
    // ======================================================================

    [Fact]
    public void MandatoryAcceptance_OneNeedAtZero_CannotBeBoughtOff_ForALLValuesOfTheOther()
    {
        // THE D-035-B MANDATORY ACCEPTANCE TEST, verbatim: one need pinned at
        // 0.0, the other swept across its ENTIRE range, and aggregate grievance
        // must stay above a stated floor throughout. "A rigged 'maximise one
        // cheap need, ignore the rest' configuration must FAIL to suppress
        // grievance."
        //
        // THE FLOOR IS DERIVED, NOT CHOSEN. CeilingWhenOneNeedIsZero computes
        // the highest aggregate reachable from sigma and the satisfaction floor
        // alone. A literal here would be a number fitted to whatever the code
        // does today and would keep passing after the mechanism changed
        // underneath it — which is the failure mode this bar exists to catch.
        AggregationTuning t = Needs().Aggregation;
        double[] weights = [0.5, 0.5];
        double ceiling = NeedsAggregation.CeilingWhenOneNeedIsZero(t.Sigma, t.SatisfactionFloor, 0.5);

        double worst = 0.0;
        for (int i = 0; i <= 100; i++)
        {
            double lavish = i / 100.0;
            double[] sat = [0.0, lavish];
            double s = NeedsAggregation.Aggregate(sat, weights, t.Sigma, t.SatisfactionFloor);
            worst = Math.Max(worst, s);
            Assert.True(s <= ceiling + 1e-12,
                $"a need at zero was bought off: other need {lavish:F2} -> aggregate {s:F4} > {ceiling:F4}");
        }

        // Non-vacuity: the sweep must actually have exercised the compensating
        // need, or "it never rose" would pass against a function that ignores
        // its inputs entirely.
        Assert.True(worst > 0.0, "sweep vacuous: the aggregate never moved off zero");
        Assert.True(ceiling < 0.15,
            $"the derived ceiling {ceiling:F4} is too loose to constrain anything — sigma or the floor has drifted");
    }

    [Fact]
    public void MandatoryAcceptance_IsRED_AgainstAWeightedSum()
    {
        // ADR-015 §7.4: prove the red, do not assume it. The bar above is
        // meaningless unless the form D-035-B REPLACED actually fails it. This
        // is d018:52's weighted sum — the exact aggregation the ruling forbids —
        // measured against the identical inputs and the identical ceiling.
        AggregationTuning t = Needs().Aggregation;
        double[] weights = [0.5, 0.5];
        double ceiling = NeedsAggregation.CeilingWhenOneNeedIsZero(t.Sigma, t.SatisfactionFloor, 0.5);

        double sum = WeightedSum([0.0, 1.0], weights);
        Assert.True(sum > ceiling,
            $"the weighted sum scored {sum:F4}, inside the ceiling {ceiling:F4} — the bar has no teeth, "
            + "because the form D-035-B exists to forbid passes it");

        // And the CES form, on the same inputs, does not.
        double ces = NeedsAggregation.Aggregate([0.0, 1.0], weights, t.Sigma, t.SatisfactionFloor);
        Assert.True(ces <= ceiling);
        Assert.True(sum > ces * 4.0,
            $"weighted sum {sum:F4} vs CES {ces:F4}: the two forms are not meaningfully different here, "
            + "so this rig cannot tell them apart");
    }

    /// <summary>d018:52's weighted sum — the superseded form, kept HERE and only
    /// here as the thing the mandatory test must be red against.</summary>
    private static double WeightedSum(double[] satisfactions, double[] weights)
    {
        double acc = 0.0, w = 0.0;
        for (int i = 0; i < satisfactions.Length; i++) { acc += weights[i] * satisfactions[i]; w += weights[i]; }
        return w > 0.0 ? acc / w : 1.0;
    }

    [Fact]
    public void NamedScenario_ShelterFull_DignityAndComfortZero_ProducesHighGrievance()
    {
        // The director's named scenario, shipped as its own test rather than
        // left implied by the general bar: shelter 100%, heavy taxation, dignity
        // and comfort at zero must produce HIGH grievance.
        //
        // SCOPE NOTE, stated rather than quietly dropped: the TAXATION leg is
        // D-035-D and is M5 — there is no tax instrument at M3, so taxation
        // cannot be applied here. What taxation does under D-035-D is injure
        // Dignity directly, and Dignity at zero is exactly what this rig pins.
        // When M5 lands the tax instrument, this test gains the instrument as
        // its cause; the assertion about the aggregate does not change.
        AggregationTuning t = Needs().Aggregation;
        //            Sustenance Shelter Safety Dignity Comfort
        double[] sat = [1.0, 1.0, 1.0, 0.0, 0.0];
        double[] w = [1.0, 0.9, 0.9, 0.4, 0.3];

        double aggregate = NeedsAggregation.Aggregate(sat, w, t.Sigma, t.SatisfactionFloor);
        double grievance = 1.0 - aggregate;

        // The floor is derived from the two zeroed needs' combined weight share.
        double share = (0.4 + 0.3) / (1.0 + 0.9 + 0.9 + 0.4 + 0.3);
        double ceiling = NeedsAggregation.CeilingWhenOneNeedIsZero(t.Sigma, t.SatisfactionFloor, share);
        Assert.True(aggregate <= ceiling + 1e-12,
            $"a well-housed, well-fed, undignified population aggregated to {aggregate:F4} — "
            + $"above the derived ceiling {ceiling:F4}");
        Assert.True(grievance > 0.5,
            $"grievance {grievance:F4} is not HIGH — perfect shelter and food bought off the rest");

        // Red against the sum: under d018:52 the same population scores well.
        Assert.True(WeightedSum(sat, w) > 0.75,
            "the weighted sum did not score this population comfortably — the contrast is not being made");
        Console.WriteLine(
            $"named scenario: CES aggregate {aggregate:F4} (grievance {grievance:F4}) "
            + $"vs weighted sum {WeightedSum(sat, w):F4}");
    }

    [Fact]
    public void SigmaAtOrAboveOne_IsRefused_ByBothTheLoaderAndTheFunction()
    {
        // sigma < 1 is D-035-B's load-bearing constraint, so it is enforced
        // where it cannot be bypassed: at the config boundary AND at the
        // arithmetic. A config that degenerated to Cobb–Douglas silently would
        // be the ruling deleted by a one-character edit.
        foreach (double bad in new[] { 1.0, 1.5, 0.0, -0.2 })
        {
            Assert.Throws<ArgumentOutOfRangeException>(
                () => NeedsAggregation.Aggregate([0.5, 0.5], [1.0, 1.0], bad, 0.05));
            var ex = Assert.Throws<NeedsConfigException>(() => NeedsConfigLoader.Load(NeedsJson(sigma: bad)));
            Assert.Contains("sigma", ex.Message, StringComparison.OrdinalIgnoreCase);
        }
        // …and the canonical value loads.
        Assert.True(NeedsConfigLoader.Load(NeedsJson(sigma: 0.5)).Aggregation.Sigma < 1.0);
    }

    [Fact]
    public void BoundNeedWithNoBasket_IsRefusedAtLoad()
    {
        // A bound need nothing satisfies would read as permanently satisfied —
        // indistinguishable from an unbound need in the output but not in the
        // weighting. That is the T2.6 zero-effect failure wearing the opposite
        // costume, so the loader refuses it.
        var ex = Assert.Throws<NeedsConfigException>(() => NeedsConfigLoader.Load(NeedsJson(sigma: 0.5, dropBaskets: true)));
        Assert.Contains("no basket entry serves it", ex.Message, StringComparison.Ordinal);
    }

    private static string NeedsJson(double sigma, bool dropBaskets = false)
    {
        string inv(double d) => d.ToString(System.Globalization.CultureInfo.InvariantCulture);
        string baskets = dropBaskets
            ? ""
            : """{ "class": 1, "need": 1, "good": "grain", "perPersonYear": 1.0 }""";
        return $$"""
        {
          "needs": [ { "id": 1, "name": "Sustenance", "bound": true, "weight": 1.0 },
                     { "id": 2, "name": "Shelter", "bound": false, "weight": 0.9 },
                     { "id": 3, "name": "Safety", "bound": false, "weight": 0.9 } ],
          "aggregation": { "sigma": {{inv(sigma)}}, "satisfactionFloor": 0.05,
                           "tierAFloor": 0.5, "tierAGain": 3.0, "tierACollapse": 1.0 },
          "baskets": { "entries": [ {{baskets}} ] },
          "grievance": { "baseDecayPerYear": 0.005, "inheritFraction": 0.85 }
        }
        """;
    }

    // ======================================================================
    // d018:46 — THE TIER A GATE OVERRIDE
    // ======================================================================

    [Fact]
    public void TierAGate_BelowFloor_RaisesTheGateSuperlinearly_AndCollapsesUpperNeeds()
    {
        // "A starving intelligentsia riots over bread, not press freedom."
        // Measured on three counts: the gate's weight RISES, the upper need's
        // weight FALLS, and the rise is SUPERLINEAR — twice the severity must
        // cost more than twice the weight, which is what distinguishes an
        // override from a gentle tilt.
        AggregationTuning t = Needs().Aggregation;
        double[] w = [1.0, 0.3];
        bool[] gate = [true, false];
        var adjusted = new double[2];

        NeedsAggregation.ApplyTierAGate([t.TierAFloor, 1.0], gate, w, t.TierAFloor, t.TierAGain, t.TierACollapse, adjusted);
        Assert.Equal(w[0], adjusted[0]);   // AT the floor: severity 0, untouched
        Assert.Equal(w[1], adjusted[1]);

        double half = t.TierAFloor * 0.5;      // severity 0.5
        double quarter = t.TierAFloor * 0.25;  // severity 0.75
        NeedsAggregation.ApplyTierAGate([half, 1.0], gate, w, t.TierAFloor, t.TierAGain, t.TierACollapse, adjusted);
        double gateAtHalf = adjusted[0], upperAtHalf = adjusted[1];
        NeedsAggregation.ApplyTierAGate([quarter, 1.0], gate, w, t.TierAFloor, t.TierAGain, t.TierACollapse, adjusted);
        double gateAtQuarter = adjusted[0], upperAtQuarter = adjusted[1];

        Assert.True(gateAtHalf > w[0], "the gate need's weight did not rise below the floor");
        Assert.True(upperAtHalf < w[1], "the upper need's weight did not collapse below the floor");
        Assert.True(gateAtQuarter > gateAtHalf && upperAtQuarter < upperAtHalf, "the gate is not monotone in severity");

        // SUPERLINEARITY, measured: severity 0.5 -> 0.75 is a 1.5x increase in
        // severity, and the EXCESS weight must grow faster than that.
        double excessHalf = gateAtHalf - w[0], excessQuarter = gateAtQuarter - w[0];
        Assert.True(excessQuarter / excessHalf > 1.5 + 1e-9,
            $"gate response is not superlinear: severity x1.5 produced excess weight x{excessQuarter / excessHalf:F3}");

        // Fully unmet: the upper need is weighted OUT, not merely down.
        NeedsAggregation.ApplyTierAGate([0.0, 1.0], gate, w, t.TierAFloor, t.TierAGain, t.TierACollapse, adjusted);
        Assert.Equal(0.0, adjusted[1]);
        Assert.Equal(w[0] * (1.0 + t.TierAGain), adjusted[0]);
    }

    // ======================================================================
    // D-035-A — VARIETY AS SATISFACTION
    // ======================================================================

    [Fact]
    public void Variety_SatisfactionStrictlyDecreasesInConcentration_AtFIXEDQuantity()
    {
        // The property the ruling actually asks for, MEASURED rather than
        // asserted: hold total quantity constant and slide the basket from even
        // to monoculture. Satisfaction must fall the whole way. Fixed quantity
        // is the whole point — a test that let the total move would be measuring
        // quantity and calling it variety.
        const double weight = 0.15, total = 3.0;
        double previous = double.MaxValue;
        var trace = new List<double>();
        for (int i = 0; i <= 10; i++)
        {
            double concentrated = total * (1.0 / 3.0 + (2.0 / 3.0) * (i / 10.0));
            double rest = (total - concentrated) / 2.0;
            double[] basket = [concentrated, rest, rest];
            Assert.Equal(total, basket[0] + basket[1] + basket[2], 12);   // quantity held fixed
            double f = NeedsAggregation.VarietyFactor(basket, weight);
            Assert.True(f < previous, $"variety factor did not fall at step {i}: {f:F6} !< {previous:F6}");
            previous = f;
            trace.Add(f);
        }
        Assert.Equal(1.0, trace[0], 12);                    // perfectly even: no penalty
        Assert.Equal(1.0 - weight, trace[^1], 12);          // monoculture: the full penalty
        Console.WriteLine($"variety factor, even -> monoculture: {trace[0]:F4} -> {trace[^1]:F4} at weight {weight}");
    }

    [Fact]
    public void Variety_IsInsideTheEquation_NotABonus_MonotonousFullBasketScoresBelowOne()
    {
        // Law 2, made testable. A variety BONUS is added after the fact and
        // leaves a fully-supplied basket at 1.0 (or above); a concentration term
        // INSIDE the equation makes a fully-supplied MONOTONOUS basket score
        // strictly below a fully-supplied VARIED one, at identical quantity.
        // The distinguishing case is the fully-supplied one — under a bonus
        // there is nothing left to add.
        const double weight = 0.15;
        double varied = NeedsAggregation.VarietyFactor([1.0, 1.0, 1.0], weight);
        double monotonous = NeedsAggregation.VarietyFactor([3.0, 0.0, 0.0], weight);
        Assert.Equal(1.0, varied, 12);
        Assert.True(monotonous < 1.0, "a monotonous full basket scored 1.0 — the term is a bonus, not a mechanism");
        Assert.True(monotonous <= 1.0, "variety exceeded 1.0 — that is a buff, which law 2 forbids");
        Assert.Equal(1.0 - weight, monotonous, 12);
    }

    [Fact]
    public void Variety_ConcentrationIsMeasuredAgainstTheDECLAREDBasket_NotWhatArrived()
    {
        // The easy-to-invert detail, pinned. A three-good basket that received
        // only grain must take the FULL monotony penalty. Counting only the
        // goods present would make n = 1, give it no diversity dimension, and
        // score it as an unimprovable single-good basket — deleting D-035-A
        // precisely in the case it exists for.
        const double weight = 0.15;
        Assert.Equal(1.0 - weight, NeedsAggregation.VarietyFactor([1.0, 0.0, 0.0], weight), 12);
        // A genuinely one-good basket has no diversity to lose, and is not penalised.
        Assert.Equal(1.0, NeedsAggregation.VarietyFactor([1.0], weight), 12);
    }

    // ======================================================================
    // THE BASKETS IN A LIVE WORLD
    // ======================================================================

    /// <summary>The canonical pipeline minus harvest weather — these are
    /// controlled experiments on basket supply, and background weather noise is
    /// a confound on both arms (the doctrine the director ratified at
    /// T3.4b).</summary>
    private static TurnExecutor Executor(SimConfig cfg)
    {
        EraTable era;
        using (var s = Sim.Data.DataFiles.OpenEraPacing()) era = EraTableLoader.Load(s);
        using var p = Sim.Data.DataFiles.OpenPipeline();
        var kept = new List<SystemRegistration>();
        foreach (SystemRegistration r in PipelineLoader.Load(p, SystemCatalog.All(cfg)))
            if (r.Name != Sim.Core.Systems.Harvest.HarvestWeatherSystem.Name) kept.Add(r);
        return new TurnExecutor(era, [.. kept]);
    }

    [Fact]
    public void ClassBaskets_PeasantAndArtisan_ProvablyDiffer_AndTheDifferenceIsTheWeights()
    {
        // The packet's class-differentiation criterion. Two claims, and the
        // second is the one that matters: the baskets differ, AND the
        // difference comes from the D-018 Tier B class signatures rather than
        // from a coincidence of stocks.
        NeedsConfig needs = Needs();
        var book = new BasketBook(needs, TestConfigs.Sim().Goods!);
        ReadOnlySpan<BasketLine> peasant = book.Basket(new ClassId(1), 1);
        ReadOnlySpan<BasketLine> artisan = book.Basket(new ClassId(2), 1);
        Assert.Equal(peasant.Length, artisan.Length);

        bool differs = false;
        double peasantStaple = 0.0, artisanStaple = 0.0, peasantTotal = 0.0, artisanTotal = 0.0;
        GoodId grain = new(TestConfigs.Sim().Goods!.GrainId);
        for (int i = 0; i < peasant.Length; i++)
        {
            Assert.Equal(peasant[i].Good, artisan[i].Good);   // same goods, different shares
            if (peasant[i].PerPersonYear != artisan[i].PerPersonYear) differs = true;
            peasantTotal += peasant[i].PerPersonYear;
            artisanTotal += artisan[i].PerPersonYear;
            if (peasant[i].Good == grain) { peasantStaple = peasant[i].PerPersonYear; artisanStaple = artisan[i].PerPersonYear; }
        }
        Assert.True(differs, "peasant and artisan food baskets are identical — there is no class differentiation to test");
        Assert.True(artisanStaple < peasantStaple,
            "the artisan diet is not less staple-heavy than the peasant diet — the signature is backwards");
        // Same nutrition, different composition: the difference is WHAT is
        // eaten, never how much — otherwise "artisans are better off" would be
        // a calorie subsidy wearing a basket's clothes.
        Assert.Equal(peasantTotal, artisanTotal, 12);
        Assert.Equal(1.0, peasantTotal, 12);
    }

    [Fact]
    public void ClassBaskets_AFisheryFailure_CostsArtisansMoreThanPeasants()
    {
        // The differentiation made live: identical settlement, identical
        // shortage, different classes.
        //
        // THE CLAIM IS ABOUT THE SIZE OF THE LOSS, NOT THE LEVEL — and that
        // correction came from the measurement, which contradicted the obvious
        // guess. Artisans weight fish more heavily, so a failed fishery costs
        // them roughly twice the variety it costs peasants. They nonetheless
        // remain BETTER fed in absolute terms, because their surviving diet is
        // still more varied than the peasant's ever was. Both facts are the
        // class signature working; asserting "artisans end up worse" would have
        // been asserting an inversion the mechanism does not produce, and
        // pinning it would have frozen a wrong belief into the suite.
        SimConfig cfg = TestConfigs.Sim();
        double peasantBefore = 0, artisanBefore = 0, peasantAfter = 0, artisanAfter = 0;
        foreach (double fishFill in new[] { 1.0, 0.0 })
        {
            WorldState world = HandWorld(cfg);
            foreach (string good in new[] { "grain", "livestock", "timber", "stone", "pottery", "cloth" })
                SetFill(world, cfg, good, 1.0);
            SetFill(world, cfg, "fish", fishFill);

            WorldState next = new TurnExecutor(FlatEra(10.0), [SystemCatalog.NeedsGrievance(cfg)]).Step(world);
            if (fishFill == 1.0)
            {
                peasantBefore = Satisfaction(next, 0, 1, needId: 1);
                artisanBefore = Satisfaction(next, 0, 2, needId: 1);
            }
            else
            {
                peasantAfter = Satisfaction(next, 0, 1, needId: 1);
                artisanAfter = Satisfaction(next, 0, 2, needId: 1);
            }
        }

        double peasantLoss = peasantBefore - peasantAfter, artisanLoss = artisanBefore - artisanAfter;
        Assert.True(peasantLoss > 0.0 && artisanLoss > 0.0, "nobody was hurt by the shortage — the rig is vacuous");
        // Strictly inside both bounds on all four readings: a saturated
        // comparison proves nothing (ADR-015 §7.5).
        foreach (double v in new[] { peasantBefore, artisanBefore, peasantAfter, artisanAfter })
            Assert.True(v > 0.5 && v < 1.0, $"satisfaction {v:F4} is resting against a bound");
        Assert.True(artisanLoss > peasantLoss * 1.5,
            $"the fishery failure did not cost artisans meaningfully more: artisan −{artisanLoss:F4} "
            + $"vs peasant −{peasantLoss:F4}");
        Assert.True(artisanBefore > peasantBefore,
            "the artisan diet was not more varied to begin with — the class signature is inverted");
        Console.WriteLine(
            $"fishery failure: peasant {peasantBefore:F4} -> {peasantAfter:F4} (−{peasantLoss:F4}), "
            + $"artisan {artisanBefore:F4} -> {artisanAfter:F4} (−{artisanLoss:F4})");
    }

    [Fact]
    public void HousingShortage_LowersShelterSatisfaction_AndRaisesGrievance()
    {
        // The packet's Shelter criterion, as a controlled contrast: two
        // settlements identical in every respect except the supply of dwelling
        // materials. The short one must read LOWER Shelter and MORE grievance.
        SimConfig cfg = TestConfigs.Sim();
        WorldState world = HandWorld(cfg, settlements: 2);
        foreach (string good in new[] { "grain", "livestock", "fish", "pottery", "cloth" })
        {
            SetFill(world, cfg, good, 1.0, settlement: 0);
            SetFill(world, cfg, good, 1.0, settlement: 1);
        }
        // Settlement 0 keeps its roofs up; settlement 1 gets a third of what it needs.
        SetFill(world, cfg, "timber", 1.0, settlement: 0);
        SetFill(world, cfg, "stone", 1.0, settlement: 0);
        SetFill(world, cfg, "timber", 0.33, settlement: 1);
        SetFill(world, cfg, "stone", 0.33, settlement: 1);

        WorldState next = new TurnExecutor(FlatEra(10.0), [SystemCatalog.NeedsGrievance(cfg)]).Step(world);

        double housed = Satisfaction(next, 0, 1, needId: 2);
        double short_ = Satisfaction(next, 1, 1, needId: 2);
        Assert.Equal(1.0, housed);
        Assert.True(short_ < housed, $"the housing shortage did not lower Shelter: {short_:F4} !< {housed:F4}");
        Assert.True(short_ > 0.0, "Shelter bottomed out — assert strictly inside the bound (ADR-015 §7.5)");
        Assert.True(Grievance(next, 1) > Grievance(next, 0),
            $"the housing shortage did not raise grievance: {Grievance(next, 1):F4} !> {Grievance(next, 0):F4}");
        Console.WriteLine(
            $"housing: shelter {housed:F2} -> {short_:F2}, grievance {Grievance(next, 0):F3} -> {Grievance(next, 1):F3}");
    }

    [Fact]
    public void StapleSubstitution_AFisheryFailure_CostsVarietyNotCalories()
    {
        // The substitution mechanism, isolated. A household short of fish eats
        // more bread: nutrition is made whole out of the staple, and the loss
        // shows up in the variety term instead. Both halves are asserted —
        // asserting only that satisfaction fell would not distinguish
        // substitution from plain hunger.
        SimConfig cfg = TestConfigs.Sim();
        SimConfig noVariety = cfg with
        {
            Needs = cfg.Needs! with { Needs = ZeroVariety(cfg.Needs!.Needs) },
        };

        WorldState world = HandWorld(cfg);
        foreach (string good in new[] { "grain", "livestock", "timber", "stone", "pottery", "cloth" })
            SetFill(world, cfg, good, 1.0);
        SetFill(world, cfg, "fish", 0.0);

        // With variety switched off, the nutrition is entirely made good.
        WorldState plain = new TurnExecutor(FlatEra(10.0), [SystemCatalog.NeedsGrievance(noVariety)]).Step(world.Clone());
        Assert.Equal(1.0, Satisfaction(plain, 0, 1, needId: 1));

        // With it on, the same world scores strictly lower — and the whole gap
        // is the monotony, not a calorie shortfall.
        WorldState real = new TurnExecutor(FlatEra(10.0), [SystemCatalog.NeedsGrievance(cfg)]).Step(world.Clone());
        double withVariety = Satisfaction(real, 0, 1, needId: 1);
        Assert.True(withVariety < 1.0 && withVariety > 0.75,
            $"substituted diet scored {withVariety:F4} — expected a monotony penalty, not a famine");
        Console.WriteLine($"fish failure: calories made whole (1.00), variety-scored {withVariety:F4}");
    }

    [Fact]
    public void UnboundNeed_AtHugeWeight_DoesNotLeakThroughTheCESAggregation()
    {
        // The T2.6 zero-effect gate, re-asserted against the NEW aggregation.
        // CES is a fresh way for an unbound need to leak: a zero term in a power
        // mean with rho < 0 does not merely contribute little, it drives the
        // whole aggregate to zero. So "the weight is small" is no defence and
        // was never the mechanism — unbound needs are skipped before any weight
        // is read, and this pins that at a weight where any leak is unmissable.
        SimConfig cfg = TestConfigs.Sim();
        var riggedNeeds = (NeedEntry[])cfg.Needs!.Needs.Clone();
        int safety = Array.FindIndex(riggedNeeds, n => n.Id == 3);
        Assert.False(riggedNeeds[safety].Bound, "rig targets a BOUND need — the inertness claim is void");
        riggedNeeds[safety] = riggedNeeds[safety] with { Weight = 1e9 };
        SimConfig rigged = cfg with { Needs = cfg.Needs with { Needs = riggedNeeds } };

        WorldState world = HandWorld(cfg);
        foreach (string good in new[] { "grain", "livestock", "fish", "timber", "stone", "pottery", "cloth" })
            SetFill(world, cfg, good, 0.6);   // strictly inside both bounds

        WorldState a = new TurnExecutor(FlatEra(10.0), [SystemCatalog.NeedsGrievance(cfg)]).Step(world.Clone());
        WorldState b = new TurnExecutor(FlatEra(10.0), [SystemCatalog.NeedsGrievance(rigged)]).Step(world.Clone());
        Assert.True(Grievance(a, 0) > 0.0, "rig vacuous: nothing accrued, so nothing could have leaked");
        Assert.Equal(WorldHash.ComputeHex(a), WorldHash.ComputeHex(b));
    }

    [Fact]
    public void Aggregate_IsScaleInvariantInItsWeights_AndMatchesTheHarmonicMeanAtSigmaHalf()
    {
        // MUTANT M3 SURVIVED THE T3.5 SWEEP: dropping `/ weightSum` in Aggregate
        // left all 52 tests green. It is not an equivalent mutant — with the
        // shipped weights (1.0/0.9/0.3, sum 2.2) and every need at 0.5, correct
        // S = 0.5 and the mutant gives 1/4.4 = 0.227, so grievance accrual
        // roughly doubles everywhere, silently.
        //
        // WHY NOTHING CAUGHT IT: both mandatory-acceptance rigs use weights
        // [0.5, 0.5], which sum to exactly 1.0 — normalisation is a no-op there,
        // so they are STRUCTURALLY blind to it. The one rig with unnormalised
        // weights asserts only upper bounds, and M3 lowers the aggregate, so the
        // mutant satisfies them more comfortably than the correct code. That is
        // ADR-015 §7.5 one level up: an assertion disarmed by pointing the same
        // way as the defect. This test exists to be the thing that is not blind.
        AggregationTuning t = Needs().Aggregation;
        double[] sat = [0.5, 0.5, 0.5];
        double[] shipped = [1.0, 0.9, 0.3];           // sum 2.2 — NOT 1.0, deliberately
        Assert.NotEqual(1.0, shipped[0] + shipped[1] + shipped[2]);

        // Scale invariance: the weights are shares, so multiplying them all by k
        // must change nothing. The mutant fails this at every k != 1.
        double baseline = NeedsAggregation.Aggregate(sat, shipped, t.Sigma, t.SatisfactionFloor);
        foreach (double k in new[] { 0.5, 2.0, 7.0 })
        {
            double[] scaled = [shipped[0] * k, shipped[1] * k, shipped[2] * k];
            Assert.Equal(baseline, NeedsAggregation.Aggregate(sat, scaled, t.Sigma, t.SatisfactionFloor), 12);
        }

        // INDEPENDENT ORACLE, not a second copy of the implementation. At the
        // shipped sigma = 0.5, rho = -1 and CES IS the weighted HARMONIC mean —
        // a closed form with no Math.Pow and no rho in it at all. If the
        // implementation and this expression agree, the implementation is doing
        // the arithmetic it claims; if someone changes rho's sign or magnitude,
        // this expression does not follow them.
        Assert.Equal(0.5, t.Sigma);   // the oracle below is only valid at sigma = 0.5
        double[] uneven = [0.9, 0.4, 0.2];
        double wSum = shipped[0] + shipped[1] + shipped[2];
        double harmonic = 1.0 / (shipped[0] / wSum / uneven[0]
                               + shipped[1] / wSum / uneven[1]
                               + shipped[2] / wSum / uneven[2]);
        Assert.Equal(harmonic, NeedsAggregation.Aggregate(uneven, shipped, t.Sigma, t.SatisfactionFloor), 12);

        // And the sigma-GENERAL property, a theorem rather than a computation:
        // for rho < 0 the power mean is STRICTLY below the weighted arithmetic
        // mean whenever the inputs differ. This holds for every sigma in (0,1)
        // and kills a sign flip without evaluating the CES independently.
        foreach (double sigma in new[] { 0.2, 0.5, 0.9 })
        {
            double ces = NeedsAggregation.Aggregate(uneven, shipped, sigma, t.SatisfactionFloor);
            double arithmetic = (shipped[0] * uneven[0] + shipped[1] * uneven[1] + shipped[2] * uneven[2]) / wSum;
            Assert.True(ces < arithmetic,
                $"at sigma {sigma} the CES {ces:F6} is not strictly below the arithmetic mean "
                + $"{arithmetic:F6} — the aggregation is not acting as a complement");
        }
    }

    [Fact]
    public void UnboundNeed_WITHABasketLine_IsStillExactlyInert()
    {
        // MUTANT M8 SURVIVED THE T3.5 SWEEP: deleting `if (!need.Bound) continue;`
        // from NeedsGrievanceSystem left all 52 tests green, because on shipped
        // data the basket set is exactly the bound set, so the NEXT line
        // (`basket.Length == 0`) reaches the answer first. Both zero-effect rigs
        // — the T2.6 one and the CES one added by this packet — therefore test
        // the no-basket path while their comments claim they test the Bound
        // gate. ADR-015 §7.4 exactly: a guard disarmed by a different mechanism
        // getting there first.
        //
        // This rig gives an UNBOUND need a real basket line, so the no-basket
        // path cannot fire and the `Bound` flag is the ONLY thing that can
        // produce the asserted inertness. Authoring a basket ahead of binding is
        // a natural way to stage content, and it is precisely when an unbound
        // need would otherwise enter a CES with rho < 0 — where a zero term does
        // not contribute little, it zeroes the whole aggregate.
        SimConfig cfg = TestConfigs.Sim();
        int safety = Array.FindIndex(cfg.Needs!.Needs, n => n.Id == 3);
        Assert.False(cfg.Needs.Needs[safety].Bound, "rig targets a BOUND need — the inertness claim is void");

        // Safety gains a basket line (timber, a good the settlement does hold)
        // while staying UNBOUND.
        var entries = new List<BasketEntry>(cfg.Needs.Baskets.Entries) { new(1, 3, "timber", 0.05) };
        NeedsConfig withLine = cfg.Needs with { Baskets = new BasketsConfig([.. entries]) };
        SimConfig a = cfg with { Needs = withLine };

        var cranked = (NeedEntry[])withLine.Needs.Clone();
        cranked[safety] = cranked[safety] with { Weight = 1e9, VarietyWeight = 0.9 };
        SimConfig b = cfg with { Needs = withLine with { Needs = cranked } };

        WorldState world = HandWorld(cfg);
        foreach (string good in new[] { "grain", "livestock", "fish", "timber", "stone", "pottery", "cloth" })
            SetFill(world, cfg, good, 0.6);   // strictly inside both bounds

        WorldState ra = new TurnExecutor(FlatEra(10.0), [SystemCatalog.NeedsGrievance(a)]).Step(world.Clone());
        WorldState rb = new TurnExecutor(FlatEra(10.0), [SystemCatalog.NeedsGrievance(b)]).Step(world.Clone());

        Assert.True(Grievance(ra, 0) > 0.0, "rig vacuous: nothing accrued, so nothing could have leaked");
        // No satisfaction row for the unbound need, however well-satisfied its basket is.
        for (int i = 0; i < ra.NeedSatisfactions.Count; i++)
            Assert.NotEqual(3, ra.NeedSatisfactions[i].NeedId);
        Assert.Equal(WorldHash.ComputeHex(ra), WorldHash.ComputeHex(rb));
    }

    [Fact]
    public void BasketBook_OrderIsIndependentOfAuthoringOrder()
    {
        // Law 5: the iteration order over baskets is the book's own (class,
        // need, good), never the order needs.json happened to be written in.
        // Reversing the file must change nothing.
        NeedsConfig needs = Needs();
        GoodsConfig goods = TestConfigs.Sim().Goods!;
        var reversed = (BasketEntry[])needs.Baskets.Entries.Clone();
        Array.Reverse(reversed);

        var a = new BasketBook(needs, goods);
        var b = new BasketBook(needs with { Baskets = new BasketsConfig(reversed) }, goods);
        Assert.Equal(a.Lines.Length, b.Lines.Length);
        for (int i = 0; i < a.Lines.Length; i++) Assert.Equal(a.Lines[i], b.Lines[i]);
        for (int i = 0; i < a.Goods.Length; i++) Assert.Equal(a.Goods[i], b.Goods[i]);
    }

    [Fact]
    public void BasketBook_UnknownGoodName_IsRefusedWithBothFileNames()
    {
        // needs.json and goods.json are separate files that must agree; when
        // they do not, the error has to say so, because the reader is looking
        // at only one of them.
        NeedsConfig needs = Needs();
        var bad = new BasketEntry[] { new(1, 1, "unobtainium", 1.0) };
        var ex = Assert.Throws<NeedsConfigException>(
            () => new BasketBook(needs with { Baskets = new BasketsConfig(bad) }, TestConfigs.Sim().Goods!));
        Assert.Contains("unobtainium", ex.Message, StringComparison.Ordinal);
        Assert.Contains("goods.json", ex.Message, StringComparison.Ordinal);
    }

    // ======================================================================
    // rig plumbing
    // ======================================================================

    private static EraTable FlatEra(double dtYears) => EraTableLoader.Load(
        $$"""{ "bands": [ { "name": "flat", "startYear": 0, "endYear": 100000, "dtYears": {{dtYears.ToString(System.Globalization.CultureInfo.InvariantCulture)}} } ] }""");

    private static NeedEntry[] ZeroVariety(NeedEntry[] needs)
    {
        var copy = (NeedEntry[])needs.Clone();
        for (int i = 0; i < copy.Length; i++) copy[i] = copy[i] with { VarietyWeight = 0.0 };
        return copy;
    }

    /// <summary>K settlements, each with a peasant and an artisan bucket of 500
    /// and a zeroed grievance row per class. Fills are set by the caller.</summary>
    private static WorldState HandWorld(SimConfig cfg, int settlements = 1)
    {
        var world = new WorldState(11);
        var ledger = new Ledger(world.LedgerFlows);
        for (int s = 0; s < settlements; s++)
        {
            var id = new SettlementId(s);
            world.Settlements.Add(new SettlementRow(id, SiteCell: s, FoundedTurn: 0));
            for (int c = 1; c <= 2; c++)
            {
                int row = world.Buckets.Add(new BucketRow(
                    id, new CultureId(1), new ReligionId(1), new ClassId(c),
                    cohortIdx: 5, Conserved.Zero, 0.0, 0.0, 0.0, 0.0));
                ledger.Flow(ref world.Buckets.Ref(row).Count, ConservedQuantityIds.Population,
                    ReasonIds.InitialEndowment, 500, FlowDirection.Source, OverdrawPolicy.Throw);
                world.Grievances.Add(new GrievanceRow(id, new ClassId(c), 0.0));
            }
        }
        return world;
    }

    /// <summary>Publish a settlement's fill ratio for one good — the (pre-clamp
    /// demand, post-clamp obtained) pair NeedsGrievanceSystem reads from Prev.
    /// 1000 is an arbitrary round denominator; only the RATIO is read.</summary>
    private static void SetFill(WorldState world, SimConfig cfg, string good, double ratio, int settlement = 0)
    {
        int id = cfg.Goods!.IdOf(good);
        Assert.True(id > 0, $"unknown good '{good}'");
        const long Demand = 1000;
        world.GoodStocks.Add(new GoodStockRow(
            new SettlementId(settlement), new GoodId(id), Conserved.Zero, 0.0, 0.0,
            lastConsumptionDemandUnits: Demand,
            lastConsumptionEatenUnits: (long)Math.Round(Demand * ratio)));
    }

    private static double Satisfaction(WorldState world, int settlement, int cls, int needId)
    {
        for (int i = 0; i < world.NeedSatisfactions.Count; i++)
        {
            NeedSatisfactionRow r = world.NeedSatisfactions[i];
            if (r.Settlement.Value == settlement && r.Class.Value == cls && r.NeedId == needId) return r.Value;
        }
        Assert.Fail($"no satisfaction row for settlement {settlement}, class {cls}, need {needId}");
        return 0.0;
    }

    private static double Grievance(WorldState world, int settlement)
    {
        for (int i = 0; i < world.Grievances.Count; i++)
            if (world.Grievances[i].Settlement.Value == settlement) return world.Grievances[i].Value;
        Assert.Fail($"no grievance row for settlement {settlement}");
        return 0.0;
    }
}
