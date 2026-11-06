using System.Numerics;
using Proto.M7.Spatial;
using Proto.M7.Time;
using Proto.M7.War;
using Proto.M7.WarEcon;
using Xunit.Abstractions;
using static Proto.M7.Tests.TestSupport;
using static Proto.M7.Tests.WarEconE.WarEconRigs;

namespace Proto.M7.Tests.WarEconE;

/// <summary>
/// Lane E: per-pulse war economics through the WarEcon layer on top of T2's War Pulse world. Conservation is checked by
/// exact equality of every commodity total; closed forms are computed independently with BigInteger.
/// </summary>
public class WarEconomyWorldTests
{
    private readonly ITestOutputHelper _out;
    public WarEconomyWorldTests(ITestOutputHelper output) => _out = output;

    private static readonly long Y = SimInstant.MsPerYear;
    internal const WarTech EarlyModern = WarTech.Coinage | WarTech.Gunpowder | WarTech.Magazines;

    /// <summary>The standard early-modern campaign: polity 0 invades polity 1 along a 380 km route (own path, then hills on
    /// a track, then forest without a road), with an early-modern field army that starts 50 km out.</summary>
    /// <summary>Route shapes: Invasion = own path 60 km, foreign hills on a track 120 km, foreign forest without a road
    /// 200 km (the last stretch is beyond pack-animal range: the army must live off the land there); Supplied = the first two
    /// only (it ends abroad, within supply range); Home = the own 60 km only.</summary>
    internal enum RouteShape { Invasion, Supplied, Home }

    internal static WarEconSetup EarlyModernSetup(FoodOrdering ordering, long treasury = 5_000_000, long granary = 2_000_000_000,
        bool localities = true, LogisticsRates? rates = null, long locMoney = 20_000, long baseTax = 0, WarTech extra = WarTech.None,
        long lender = 0, (long, long)[]? combat = null, bool march = true, RouteShape shape = RouteShape.Invasion, long homeLocMoney = 10_000)
    {
        var segs = new List<RouteSegment> { Seg(60, TerrainClass.Open, RoadClass.DirtPath, localities ? 0 : -1) };
        if (shape != RouteShape.Home) segs.Add(Seg(120, TerrainClass.Hills, RoadClass.Track, localities ? 1 : -1));
        if (shape == RouteShape.Invasion) segs.Add(Seg(200, TerrainClass.Forest, RoadClass.None, localities ? 1 : -1));
        var route = new Route(segs.ToArray());
        return new WarEconSetup(ordering, rates ?? LogisticsRates.Proposal(), TerrainLogistics.Proposal(),
            new[]
            {
                Polity(0, EarlyModern | extra, 300_000, granary, treasury, baseTax: baseTax, munitions: 200_000, kit: 20_000_000, lender: lender),
                Polity(1, EarlyModern, 150_000, 500_000_000, 1_000_000, munitions: 50_000, kit: 1_000_000),
            },
            new[] { new LocalitySetup(0, 0, 6_000, 4_000_000, homeLocMoney), new LocalitySetup(1, 1, 9_000, 6_000_000, locMoney) },
            new[] { new ArmySetup(0, 0, 0, 0, EarlyModernArmy(), route, 50_000, march, 100_000, 0, 5_000, combat ?? new (long, long)[] { (3, 2) }) });
    }

    private sealed record TierRow(FoodStage Stage, long GrowthDemand, long GrowthLeft, MilitaryFoodGrant[] Grants, MilitaryFoodRequest[] RequestsById)
    {
        public IReadOnlyDictionary<int, MilitaryFoodRequest> Requests => RequestsById.ToDictionary(r => r.ArmyId);
    }

    internal static long EarlyModernStart(int turnInBand, long offsetDays)
    {
        var cal = ShippedCalendar();
        return cal.TurnStart(cal.Bands[3].FirstTurn + turnInBand).Ms + offsetDays * Day;
    }

    // ------------------------------------------------------------------------------------------------ 1. conservation

    /// <summary>Every commodity total is constant (exact equality) at the boundary instant of a straddling pulse, one ms
    /// either side, mid-pulse and at the end, and the pulse straddling the turn boundary is settled in two parts.</summary>
    [Fact]
    public void Conservation_ExactAtEveryInstant_AcrossAStraddlingPulse()
    {
        long s1 = EarlyModernStart(1, 0), start = s1 - 3 * Day, stop = start + 40 * 7 * Day;
        var rig = Build(EarlyModernSetup(FoodOrdering.CivilianMilitaryGrowth), new[] { WarOf(0, start, stop, 0, 1) }, 2, start);
        var t0 = Totals(rig.W.Ledger);
        foreach (long at in new[] { start, s1 - 1, s1, s1 + 1, start + 7 * Day, start + 7 * Day + 1, start + 100 * Day + 12345, stop + 30 * Day })
        {
            rig.R.RunUntil(at);
            Assert.Equal(t0, Totals(rig.W.Ledger));
        }
        var split = rig.Inner.SubSteps.Single(x => x.EndsAtSplit);
        Assert.Equal(s1, split.EndMs);
        Assert.Equal(0, split.PulseIndex);
        // The straddling pulse's consumption, settled in two parts, totals exactly its planned amount.
        var row0 = rig.W.Rows.Single(r => r.PulseIndex == 0);
        Assert.True(row0.FoodEaten > 0);
        long eaten = rig.W.Rows.Sum(r => r.FoodEaten);
        Assert.Equal(eaten, rig.W.Ledger.Balance(rig.W.GrainEatenAccount));
        Assert.Equal(40, rig.W.Rows.Count);
    }

    /// <summary>Sub-step settlement is exact pro rata: the grain eaten in the part of pulse 0 before the boundary is
    /// floor(total × part / W) (independent BigInteger closed form), the rest after it.</summary>
    [Fact]
    public void StraddlingPulse_PartBeforeTheBoundary_IsTheExactProRataShare()
    {
        long s1 = EarlyModernStart(1, 0), start = s1 - 3 * Day;
        var rig = Build(EarlyModernSetup(FoodOrdering.CivilianMilitaryGrowth), new[] { WarOf(0, start, start + 7 * Day, 0, 1) }, 2, start);
        rig.R.RunUntil(s1);
        long before = rig.W.Ledger.Balance(rig.W.GrainEatenAccount);
        rig.R.RunUntil(start + 8 * Day);
        var row = rig.W.Rows.Single();
        Assert.Equal((long)((BigInteger)row.FoodEaten * (3 * Day) / (7 * Day)), before);
        Assert.Equal(row.FoodEaten, rig.W.Ledger.Balance(rig.W.GrainEatenAccount));
    }

    /// <summary>Crossing invariance of the war side: the same war under the shipped calendar (720-day turns) and under
    /// one-year turns gives the identical army-side state (stores, condition, rows, events) although the splits differ.
    /// No locality on the route and an ample granary: foraging and the allocation would otherwise read turn-keyed stocks.</summary>
    [Fact]
    public void CrossingInvariance_ArmySideIndependentOfTurnBoundaries()
    {
        long start = EarlyModernStart(0, 300), stop = start + 150 * 7 * Day;
        var a = Build(EarlyModernSetup(FoodOrdering.CivilianMilitaryGrowth, treasury: 30_000, localities: false), new[] { WarOf(0, start, stop, 0, 1) }, 2, start);
        var b = Build(EarlyModernSetup(FoodOrdering.CivilianMilitaryGrowth, treasury: 30_000, localities: false), new[] { WarOf(0, start, stop, 0, 1) }, 2, start, turnCal: OneYearTurns());
        a.R.RunUntil(stop + Day);
        b.R.RunUntil(stop + Day);
        Assert.NotEqual(a.Inner.SubSteps.Count(x => x.EndsAtSplit), b.Inner.SubSteps.Count(x => x.EndsAtSplit));
        Assert.True(a.W.Rows.Any(r => r.ArrearsEnd > 0), "the run must exercise arrears (treasury runs dry)");
        Assert.Equal(a.W.ArmySideHashHex(), b.W.ArmySideHashHex());
    }

    // ------------------------------------------------------------------------------------------------ 2. the tiers in the world

    /// <summary>A granary with no harvest drained by a marching army: under CivilianMilitaryGrowth the recorded stages of
    /// the automatic allocation escalate in DIR5's order — growth reduced, then movement restricted, then standing short —
    /// never skipping back; under CivilianGrowthMilitary growth is never the cut tier (it is protected).</summary>
    [Fact]
    public void World_TierOrder_GrowthThenMovementThenStarvation_UnderBothOrderings()
    {
        // No harvest (so the granary only drains) and a long river route (so the supply line never fails): the only constraint
        // is the granary. Civilians 24,200 over a 720-day turn reserve 48.4 M gm; growth 400 gm/head-year asks 19.36 M gm;
        // the granary holds both plus 8.15 M gm for the army. Re-rigged in fix round 2 (E2-1, lost premise, measured): with
        // head losses compounded per day the growth tier ran out with only 904 gm left, less than the 10,000 gm the
        // civilian reserve rose by in one pulse as deserters came home, so the granary went from growth-reduced straight to
        // a civilian shortfall and no pulse cut the military; 150,000 gm more (half a pulse's grant) restores the window.
        var rates = LogisticsRates.Proposal() with { HarvestPerCapitaYear = 0, LostHarvestPerSoldierYear = 0, WaterRadiusKm = 10_000_000 };
        long start = EarlyModernStart(0, 0), stop = start + 110 * 7 * Day;
        TierRow[] Stages(FoodOrdering o)
        {
            var setup = EarlyModernSetup(o, granary: 48_400_000 + 19_360_000 + 8_150_000, localities: false, rates: rates);
            var river = new Route(new[] { Seg(20_000, TerrainClass.Open, RoadClass.Track, water: true) });
            setup = setup with
            {
                Polities = new[] { setup.Polities[0] with { Population = 30_000, GrowthPerCapitaYear = 400 }, setup.Polities[1] },
                Armies = new[] { setup.Armies[0] with { Route = river, Position = 0 } },
            };
            var rig = Build(setup, new[] { WarOf(0, start, stop, 0, 1) }, 2, start);
            rig.R.RunUntil(stop);
            return rig.W.Allocations.Where(x => x.Polity == 0).Select(x => new TierRow(x.Allocation.Stage, x.Allocation.GrowthDemand, x.Allocation.GrowthLeft,
                x.Allocation.Grants, x.Requests)).ToArray();
        }
        var b = Stages(FoodOrdering.CivilianMilitaryGrowth);
        _out.WriteLine("CivilianMilitaryGrowth stages: " + string.Join(",", b.Select(x => (int)x.Stage)));
        // Every allocation obeys the tiers (movement cut => growth already got nothing; standing cut => no movement at all),
        // and over the run growth is the first tier to be cut: it is reduced in pulses before any military cut.
        foreach (var x in b)
        {
            if (x.Grants.Any(g => g.Movement < x.Requests[g.ArmyId].Movement)) Assert.Equal(0, x.GrowthLeft);
            if (x.Grants.Any(g => g.Standing < x.Requests[g.ArmyId].Standing)) Assert.All(x.Grants, g => Assert.Equal(0, g.Movement));
        }
        int firstGrowth = Array.FindIndex(b, x => x.Stage == FoodStage.GrowthReduced);
        int firstMilitary = Array.FindIndex(b, x => x.Stage is FoodStage.MovementRestricted or FoodStage.StandingShort);
        Assert.True(firstGrowth >= 0 && firstMilitary > firstGrowth, $"growth reduced at {firstGrowth}, military first cut at {firstMilitary}");
        Assert.True(b.Skip(firstGrowth).Take(firstMilitary - firstGrowth).All(x => x.Stage == FoodStage.GrowthReduced));

        var a = Stages(FoodOrdering.CivilianGrowthMilitary);
        _out.WriteLine("CivilianGrowthMilitary stages: " + string.Join(",", a.Select(x => (int)x.Stage)));
        Assert.DoesNotContain(a, x => x.Stage == FoodStage.GrowthReduced);
        int aMilitary = Array.FindIndex(a, x => x.Stage is FoodStage.MovementRestricted or FoodStage.StandingShort);
        Assert.True(aMilitary >= 0 && aMilitary < firstMilitary, "with growth protected, the military is cut earlier");
        // At that pulse the other ordering cut growth instead and met the military in full.
        Assert.Equal(FoodStage.GrowthReduced, b[aMilitary].Stage);
        Assert.True(a[aMilitary].GrowthLeft > b[aMilitary].GrowthLeft);
    }

    /// <summary>At the army: whenever soldiers go short (hunger), the army did not move in that pulse — movement is
    /// restricted before starvation; and the hungry army's disease and desertion rates exceed the fed baseline.</summary>
    [Fact]
    public void Army_HungerImpliesNoMovement_AndRaisesDiseaseAndDesertion()
    {
        var rig = AncientDeepRaid(FoodOrdering.CivilianMilitaryGrowth, out _);
        var rows = rig.W.Rows.Where(r => r.ArmyId == 0).ToList();
        Assert.Contains(rows, r => r.HungerPpm > 0);
        Assert.Contains(rows, r => r.HungerPpm == 0 && r.DistanceM > 0);
        foreach (var r in rows.Where(r => r.HungerPpm > 0))
        {
            Assert.Equal(0, r.DistanceM);
            Assert.Equal(0, r.MovementPpm);
        }
        var fed = rows.First(r => r.HungerPpm == 0);
        var hungry = rows.First(r => r.HungerPpm > 0);
        Assert.True(hungry.DiseasePermilleYear > fed.DiseasePermilleYear);
        Assert.True(hungry.DesertionPermilleYear > fed.DesertionPermilleYear);
        Assert.True(hungry.SupplyQ32Raw < fed.SupplyQ32Raw);
    }

    // ------------------------------------------------------------------------------------------------ 3. composition, Law 4

    [Fact]
    public void AncientArmy_ConsumesNoOilAndNoMunitions_AndTrucksOrFirearmsAreRefusedWithoutTheirTechnology()
    {
        var rig = AncientDeepRaid(FoodOrdering.CivilianMilitaryGrowth, out _);
        Assert.Equal(0, rig.W.Ledger.Total(Commodity.Fuel));
        Assert.Equal(0, rig.W.Ledger.Total(Commodity.Munitions));
        Assert.All(rig.W.Rows, r => Assert.Equal(0, r.FuelUsed));
        Assert.All(rig.W.Rows, r => Assert.Equal(0, r.MunitionsNeed));
        var ancient = new PolityCapability(WarTech.None);
        Assert.NotNull((AncientHost() with { Trucks = 1 }).Validate(ancient));
        Assert.NotNull((AncientHost() with { Firearms = true }).Validate(ancient));
        Assert.NotNull((AncientHost() with { Guns = 1 }).Validate(ancient));
        Assert.Null(AncientHost().Validate(ancient));
        Assert.Null((AncientHost() with { Trucks = 1 }).Validate(new PolityCapability(WarTech.MotorTransport)));
    }

    /// <summary>Law 4: capability, not the date, decides the economics. The same polity, army and route at two instants in
    /// different era bands (pulse length held equal) produce identical rows except for the instants.</summary>
    [Fact]
    public void Capability_IsStateDerived_NotDateDerived()
    {
        var cal = ShippedCalendar();
        var sevenEverywhere = new long[] { 7, 7, 7, 7, 7, 7, 7 };
        string[] Run(long start)
        {
            var rig = Build(EarlyModernSetup(FoodOrdering.CivilianMilitaryGrowth, localities: false), new[] { WarOf(0, start, start + 30 * 7 * Day, 0, 1) }, 2, start, pulseDays: sevenEverywhere);
            rig.R.RunUntil(start + 31 * 7 * Day);
            return rig.W.Rows.Select(r => (r with { StartMs = 0, EndMs = 0 }).ToString()).ToArray();
        }
        long bronze = cal.TurnStart(cal.Bands[1].FirstTurn + 3).Ms + 10 * Day;     // -1500..500 band
        long industrial = cal.TurnStart(cal.Bands[4].FirstTurn + 3).Ms + 10 * Day; // 1800..1920 band
        Assert.Equal(Run(bronze), Run(industrial));
    }

    /// <summary>Sanitation is a capability (Law 4) that scales disease mortality by 300‰; it does not touch the rest of the
    /// economics. A fed, equipped army: 60 ‰/yr without it, exactly 18 ‰/yr with it.</summary>
    [Fact]
    public void Sanitation_ScalesDiseaseMortality()
    {
        long start = EarlyModernStart(0, 30), stop = start + 7 * Day;
        ArmyPulseRow One(WarTech extra)
        {
            var rig = Build(EarlyModernSetup(FoodOrdering.CivilianMilitaryGrowth, localities: false, extra: extra), new[] { WarOf(0, start, stop, 0, 1) }, 2, start);
            rig.R.RunUntil(stop);
            return rig.W.Rows.Single();
        }
        var without = One(WarTech.None);
        var with = One(WarTech.Sanitation);
        Assert.Equal(0, without.HungerPpm);
        Assert.Equal(LogisticsRates.Proposal().DiseaseBasePermilleYear, without.DiseasePermilleYear);
        Assert.Equal(LogisticsRates.Proposal().DiseaseBasePermilleYear * 300 / 1000, with.DiseasePermilleYear);
        Assert.True(with.DiseaseDeaths < without.DiseaseDeaths);
        Assert.Equal(without.FoodEaten, with.FoodEaten);
    }

    // ------------------------------------------------------------------------------------------------ 4. people and labour

    /// <summary>Deserters return to the population through the ledger: the owner's civilians grow by exactly the men who
    /// deserted (no famine, no disbandment in this run), and the dead sink by exactly the disease deaths.</summary>
    [Fact]
    public void Deserters_ReturnToPopulation_Exactly()
    {
        long start = EarlyModernStart(0, 30), stop = start + 60 * 7 * Day;
        var rig = Build(EarlyModernSetup(FoodOrdering.CivilianMilitaryGrowth, treasury: 20_000, localities: false), new[] { WarOf(0, start, stop, 0, 1) }, 2, start);
        long civ0 = rig.W.CiviliansOf(0);
        rig.R.RunUntil(stop);
        long deserted = rig.W.Rows.Sum(r => r.Deserted), died = rig.W.Rows.Sum(r => r.DiseaseDeaths);
        Assert.True(deserted > 0 && died > 0);
        Assert.DoesNotContain(rig.W.Events, e => e.Kind is CascadeKind.CivilianFamine or CascadeKind.Disbanded);
        Assert.Equal(civ0 + deserted, rig.W.CiviliansOf(0));
        Assert.Equal(died, rig.W.Ledger.Balance(rig.W.DeadAccount));
        Assert.Equal(EarlyModernArmy().Men - deserted - died, rig.W.MenOf(0));
    }

    /// <summary>Labour withdrawn by mobilisation: inside one turn and with no draw on the granary (a halted army with a full
    /// train, no deaths), the granary grows by exactly floor(((civ + mob)·h − mob·lost)·dt / Y) (BigInteger closed form);
    /// without the army it grows by floor((civ + mob)·h·dt / Y).</summary>
    [Fact]
    public void LabourWithdrawn_HarvestIsExactlyTheClosedForm()
    {
        var rates = LogisticsRates.Proposal() with { DesertionBasePermilleYear = 0, DesertionLowMoralePermilleYear = 0, DiseaseBasePermilleYear = 0, AnimalDeathBasePermilleYear = 0 };
        long start = EarlyModernStart(0, 10), stop = start + 50 * 7 * Day;
        var setup = EarlyModernSetup(FoodOrdering.CivilianMilitaryGrowth, localities: false, rates: rates, march: false);
        setup = setup with { Armies = new[] { setup.Armies[0] with { TrainGrain = 5_000_000_000 } } };
        var rig = Build(setup, new[] { WarOf(0, start, stop, 0, 1) }, 2, start);
        long g0 = rig.W.GranaryOf(0);
        rig.R.RunUntil(start + 49 * 7 * Day);   // touched at that pulse start; still inside turn 0 of the band (720 days)
        long mob = EarlyModernArmy().Men, civ = 300_000 - mob;
        long dt = 49 * 7 * Day;
        long expected = (long)(((BigInteger)(civ + mob) * rates.HarvestPerCapitaYear - (BigInteger)mob * rates.LostHarvestPerSoldierYear) * dt / Y);
        Assert.Equal(g0 + expected, rig.W.GranaryOf(0));
        Assert.True(expected < (long)((BigInteger)(civ + mob) * rates.HarvestPerCapitaYear * dt / Y));
    }

    /// <summary>DIR5 §2.11 at home: a LOYAL army that its polity cannot feed does not take its own civilians' reserve. Halted
    /// in its own locality with an empty home granary, it requisitions only the locality's surplus above the locals' need
    /// until the next settlement; it seizes nothing; it goes hungry (starvation is the last resort and falls on the army).</summary>
    [Fact]
    public void LoyalArmyAtHome_NeverSeizesTheLocalCiviliansReserve()
    {
        long start = EarlyModernStart(0, 30), stop = start + 30 * 7 * Day;
        var noHarvest = LogisticsRates.Proposal() with { HarvestPerCapitaYear = 0, LostHarvestPerSoldierYear = 0 };
        var setup = EarlyModernSetup(FoodOrdering.CivilianMilitaryGrowth, granary: 0, shape: RouteShape.Home, march: false, rates: noHarvest);
        setup = setup with
        {
            Localities = new[] { new LocalitySetup(0, 0, 6_000, 12_000_000, 0), setup.Localities[1] },
            Armies = new[] { setup.Armies[0] with { Position = 55_000, TrainGrain = 0 } },
        };
        var rig = Build(setup, new[] { WarOf(0, start, stop, 0, 1) }, 2, start);
        var cal = ShippedCalendar();
        long turnEnd = cal.TurnStart(cal.Bands[3].FirstTurn + 1).Ms;
        for (long t = start; t < stop; t += 7 * Day)
        {
            rig.R.RunUntil(t);
            // The locality never falls below its civilians' food until the next settlement because of the army.
            long reserve = (long)(((BigInteger)6_000 * 1000 * (turnEnd - t) + Y - 1) / Y);
            Assert.True(rig.W.LocalityGrainOf(0) >= Math.Min(12_000_000, reserve) || rig.W.Rows.All(r => r.Foraged == 0));
        }
        rig.R.RunUntil(stop);
        Assert.All(rig.W.Rows, r => Assert.Equal(0, r.Seized));
        Assert.DoesNotContain(rig.W.Events, e => e.Kind is CascadeKind.ForcedRequisition or CascadeKind.Plunder);
        Assert.Contains(rig.W.Rows, r => r.HungerPpm > 0 && r.ConditionAtStart == ArmyCondition.Loyal);
    }

    /// <summary>Revenue is a pure inflow at the latched rate: taxes (base + war tax) on civilians and trade income less the
    /// blockaded share. With a pre-funded pay chest (no draw on the treasury) and no losses, the treasury grows by exactly
    /// floor((civ·(base + war) + trade·(1000 − blockade))·dt / (1000·Y)) (BigInteger closed form); the blockade costs exactly
    /// its share.</summary>
    [Fact]
    public void Revenue_WarTaxAndBlockade_AreExactClosedForms()
    {
        var rates = LogisticsRates.Proposal() with { DesertionBasePermilleYear = 0, DesertionLowMoralePermilleYear = 0, DiseaseBasePermilleYear = 0, AnimalDeathBasePermilleYear = 0 };
        long start = EarlyModernStart(0, 10), at = start + 40 * 7 * Day;
        long Gain(long blockade)
        {
            var setup = EarlyModernSetup(FoodOrdering.CivilianMilitaryGrowth, treasury: 1000, localities: false, rates: rates, march: false, baseTax: 30);
            setup = setup with
            {
                Polities = new[] { setup.Polities[0] with { WarTaxPerMilleCivYear = 25, TradeIncomePerYear = 7_000, BlockadePermille = blockade }, setup.Polities[1] },
                Armies = new[] { setup.Armies[0] with { PayChest = 1_000_000_000 } },
            };
            var rig = Build(setup, new[] { WarOf(0, start, start + 60 * 7 * Day, 0, 1) }, 2, start);
            long t0 = rig.W.TreasuryOf(0);
            rig.R.RunUntil(at);
            return rig.W.TreasuryOf(0) - t0;
        }
        long civ = 300_000 - EarlyModernArmy().Men, dt = at - start;
        long Closed(long blockade) => (long)(((BigInteger)civ * (30 + 25) + (BigInteger)7_000 * (1000 - blockade)) * dt / ((BigInteger)1000 * Y));
        Assert.Equal(Closed(0), Gain(0));
        Assert.Equal(Closed(600), Gain(600));
        Assert.True(Gain(600) < Gain(0));
    }

    // ------------------------------------------------------------------------------------------------ 5. the insolvency cascade

    /// <summary>
    /// The insolvency cascade, pulse by pulse (reports/H-E.md §4): a coin-paying army abroad with a small war chest and no
    /// revenue. Pay arrears begin when the treasury runs dry; morale and cohesion fall with the arrears' duration; the
    /// latched desertion rate rises; cohesion crosses the mutiny threshold; the mutinous army stops marching and plunders
    /// the locality it stands in; deserters go home. Food is ample, so the cascade is purely fiscal.
    /// </summary>
    [Fact]
    public void InsolvencyCascade_ArrearsMoraleDesertionMutinyPlunder_InThatOrder()
    {
        long start = EarlyModernStart(0, 30), stop = start + 140 * 7 * Day;
        var setup = EarlyModernSetup(FoodOrdering.CivilianMilitaryGrowth, treasury: 12_000, locMoney: 3_000, shape: RouteShape.Supplied, march: false);
        setup = setup with { Armies = new[] { setup.Armies[0] with { Position = 70_000 } } };   // 10 km across the border, in reach of its wagons
        var rig = Build(setup, new[] { WarOf(0, start, stop, 0, 1) }, 2, start);
        var t0 = Totals(rig.W.Ledger);
        rig.R.RunUntil(stop);
        Assert.Equal(t0, Totals(rig.W.Ledger));
        _out.WriteLine(Table(rig.W.Rows, 0, start));
        foreach (var e in rig.W.Events.Where(e => e.Kind != CascadeKind.Contribution)) _out.WriteLine($"{e.AtMs / Day - start / Day,5} d  {e.Kind,-20} {e.Value}");

        var ev = rig.W.Events;
        Assert.Contains(ev, e => e.Kind == CascadeKind.ArrearsBegan);
        Assert.Contains(ev, e => e.Kind == CascadeKind.MutinyBegan);
        Assert.Contains(ev, e => e.Kind == CascadeKind.Plunder);
        long At(CascadeKind k) => ev.First(e => e.Kind == k).AtMs;
        Assert.True(At(CascadeKind.ArrearsBegan) < At(CascadeKind.MutinyBegan));
        Assert.True(At(CascadeKind.MutinyBegan) <= At(CascadeKind.Plunder));          // the next pulse start, the same instant
        Assert.All(ev.Where(e => e.Kind == CascadeKind.Plunder), e => Assert.Equal(ArmyCondition.Mutinous, rig.W.Rows.Single(r => r.StartMs == e.AtMs).ConditionAtStart));
        var rows = rig.W.Rows.Where(r => r.ArmyId == 0).ToList();
        var loyalInArrears = rows.Where(r => r.ConditionAtStart == ArmyCondition.Loyal && r.ArrearsEnd > 0).ToList();
        for (int i = 1; i < loyalInArrears.Count; i++)
        {
            Assert.True(loyalInArrears[i].DesertionPermilleYear >= loyalInArrears[i - 1].DesertionPermilleYear, "desertion rises with arrears duration");
            Assert.True(loyalInArrears[i].CohesionEnd <= loyalInArrears[i - 1].CohesionEnd, "cohesion falls with arrears duration");
        }
        Assert.All(rows.Where(r => r.ConditionAtStart == ArmyCondition.Mutinous), r => Assert.Equal(0, r.DistanceM));
        Assert.All(rows, r => Assert.Equal(0, r.HungerPpm));     // purely fiscal
        Assert.True(rows[^1].MenEnd < rows[0].MenStart);
    }

    /// <summary>Back pay ends a mutiny: an unpaid army garrisoned at home (no contributions from its own people) mutinies
    /// and sacks its own side's town (the Spanish Fury at Antwerp, 1576, fell on a city of the same crown); the plunder pays
    /// the mutineers' arrears (they pay themselves), and the army returns to obedience at that pulse end with cohesion
    /// restored to the recovery level. Unrest rises at home.</summary>
    [Fact]
    public void Mutiny_EndsWhenArrearsAreCleared()
    {
        long start = EarlyModernStart(0, 30), stop = start + 140 * 7 * Day;
        var rig = Build(EarlyModernSetup(FoodOrdering.CivilianMilitaryGrowth, treasury: 12_000, shape: RouteShape.Home, homeLocMoney: 5_000_000), new[] { WarOf(0, start, stop, 0, 1) }, 2, start);
        rig.R.RunUntil(stop);
        _out.WriteLine(Table(rig.W.Rows, 0, start));
        // Unrest inputs in their own units (fix round 1, E-V9): the grain plundered from the own town is exactly the sum of
        // the Plunder events against polity 0, and money was taken too.
        Assert.Equal(rig.W.Events.Where(e => e.Kind == CascadeKind.Plunder && e.Polity == 0).Sum(e => e.Value), rig.W.OwnSidePlunderGrainOf(0));
        Assert.True(rig.W.OwnSidePlunderMoneyOf(0) > 0, "a mutiny that sacks its own side's town raises unrest at home");
        Assert.Contains(rig.W.Events, e => e.Kind == CascadeKind.MutinyBegan);
        Assert.Contains(rig.W.Events, e => e.Kind == CascadeKind.MutinyEnded);
        var began = rig.W.Events.First(e => e.Kind == CascadeKind.MutinyBegan);
        var ended = rig.W.Events.First(e => e.Kind == CascadeKind.MutinyEnded);
        Assert.True(began.AtMs < ended.AtMs);
        var endRow = rig.W.Rows.Single(r => r.EndMs == ended.AtMs);
        var beganRow = rig.W.Rows.Single(r => r.EndMs == began.AtMs);
        Assert.True(endRow.ArrearsEnd < beganRow.ArrearsEnd / 10, "the back pay was settled");
        Assert.Contains(rig.W.Events, e => e.Kind == CascadeKind.Plunder && e.Polity == 0 && e.AtMs >= began.AtMs && e.AtMs <= ended.AtMs);
        Assert.Equal(ArmyCondition.Loyal, endRow.ConditionEnd);
        Assert.True(endRow.CohesionEnd >= LogisticsRates.Proposal().MutinyRecoveredCohesion * 1000);
    }

    /// <summary>The cascade to its end: with nothing to plunder and no money, the mutiny persists, cohesion reaches zero and
    /// the army disbands; every remaining man goes home and every store returns to the polity through the ledger.</summary>
    [Fact]
    public void InsolvencyCascade_EndsInDisbandment_EverythingReturnsThroughTheLedger()
    {
        long start = EarlyModernStart(0, 30), stop = start + 200 * 7 * Day;
        var setup = EarlyModernSetup(FoodOrdering.CivilianMilitaryGrowth, treasury: 12_000, locMoney: 0, homeLocMoney: 0, shape: RouteShape.Supplied, march: false);
        setup = setup with { Armies = new[] { setup.Armies[0] with { Position = 70_000 } } };
        var rig = Build(setup, new[] { WarOf(0, start, stop, 0, 1) }, 2, start);
        var t0 = Totals(rig.W.Ledger);
        rig.R.RunUntil(stop);
        Assert.Equal(t0, Totals(rig.W.Ledger));
        Assert.Single(rig.W.Events, e => e.Kind == CascadeKind.Disbanded);
        var d = rig.W.Events.Single(e => e.Kind == CascadeKind.Disbanded);
        Assert.Equal(ArmyCondition.Disbanded, rig.W.ConditionOf(0));
        Assert.Equal(0, rig.W.MenOf(0));
        Assert.Equal(0, rig.W.TrainOf(0));
        Assert.True(rig.W.Events.First(e => e.Kind == CascadeKind.MutinyBegan).AtMs < d.AtMs);
        Assert.DoesNotContain(rig.W.Rows, r => r.StartMs >= d.AtMs);        // a disbanded army plans no further pulse
    }

    /// <summary>War borrowing (Banking): the polity borrows at a campaign settlement to cover pay; interest accrues per
    /// sim-year; when a later settlement cannot pay the interest, the polity defaults, credit closes, and the unpaid army
    /// slides into arrears (the 1575 Spanish default and the unpaid Army of Flanders is the reference pattern).</summary>
    [Fact]
    public void WarBorrowing_ThenDefault_ClosesCredit_AndArrearsFollow()
    {
        var cal = ShippedCalendar();
        long start = cal.TurnStart(cal.Bands[4].FirstTurn + 2).Ms - 20 * Day, stop = start + 4 * 360 * Day;   // 1-year turns, 3-day pulses
        var setup = EarlyModernSetup(FoodOrdering.CivilianMilitaryGrowth, treasury: 1_000, baseTax: 40, extra: WarTech.Banking, lender: 10_000_000, localities: false);
        var rig = Build(setup, new[] { WarOf(0, start, stop, 0, 1) }, 2, start);
        rig.R.RunUntil(stop);
        foreach (var t in rig.W.TurnRows.Where(t => t.Polity == 0)) _out.WriteLine($"turn {t.Turn}: borrowed {t.Borrowed} interest {t.InterestDue}/{t.InterestPaid} debt {t.Debt} closed {t.CreditClosed}");
        var ev = rig.W.Events;
        long borrowed = ev.First(e => e.Kind == CascadeKind.Borrowed).AtMs;
        long dflt = ev.First(e => e.Kind == CascadeKind.CreditDefault).AtMs;
        Assert.True(borrowed < dflt);
        Assert.DoesNotContain(ev, e => e.Kind == CascadeKind.Borrowed && e.AtMs > dflt);
        Assert.True(rig.W.CreditClosedOf(0));
        Assert.Contains(ev, e => e.Kind == CascadeKind.ArrearsBegan && e.AtMs > borrowed);
    }

    // ------------------------------------------------------------------------------------------------ 6. determinism, tie-dense

    /// <summary>Tie-dense at the world level: three armies of EQUAL priority foraging the same foreign locality and drawing
    /// on one constrained granary, declared in permuted array orders: identical state hashes, and within each pulse the
    /// lower army id is served first (it forages at least as much as a higher id).</summary>
    [Fact]
    public void TieDense_EqualPriorityArmies_ServedByIdRegardlessOfDeclarationOrder()
    {
        long start = EarlyModernStart(0, 30), stop = start + 20 * 7 * Day;
        var baseSetup = EarlyModernSetup(FoodOrdering.CivilianMilitaryGrowth, granary: 20_000_000);
        var route = new Route(new[] { Seg(40, TerrainClass.Open, RoadClass.Track, 1) });
        var armies = Enumerable.Range(0, 3).Select(i => new ArmySetup(i, 0, 0, 7, EarlyModernArmy(), route, 10_000, false, 0, 0, 0, Array.Empty<(long, long)>())).ToArray();
        string? hash = null;
        for (ulong seed = 1; seed <= 4; seed++)
        {
            var perm = Permutation(3, seed);
            var setup = baseSetup with { Armies = perm.Select(i => armies[i]).ToArray() };
            var rig = Build(setup, new[] { WarOf(0, start, stop, 0, 1) }, 2, start);
            rig.R.RunUntil(stop);
            hash ??= rig.W.StateHashHex();
            Assert.Equal(hash, rig.W.StateHashHex());
            foreach (var g in rig.W.Rows.GroupBy(r => r.PulseIndex))
            {
                var f = g.OrderBy(r => r.ArmyId).Select(r => r.Foraged).ToArray();
                Assert.True(f[0] >= f[1] && f[1] >= f[2]);
            }
            Assert.Contains(rig.W.Rows, r => r.ArmyId == 0 && r.Foraged > rig.W.Rows.Single(x => x.ArmyId == 2 && x.PulseIndex == r.PulseIndex).Foraged);
        }
    }

    [Fact]
    public void SameSetupTwice_IdenticalHashes()
    {
        long start = EarlyModernStart(1, -3), stop = start + 60 * 7 * Day;
        var a = Build(EarlyModernSetup(FoodOrdering.CivilianGrowthMilitary, treasury: 20_000), new[] { WarOf(0, start, stop, 0, 1) }, 2, start);
        var b = Build(EarlyModernSetup(FoodOrdering.CivilianGrowthMilitary, treasury: 20_000), new[] { WarOf(0, start, stop, 0, 1) }, 2, start);
        a.R.RunUntil(stop);
        b.R.RunUntil(stop);
        Assert.Equal(a.W.StateHashHex(), b.W.StateHashHex());
        Assert.Equal(a.S.Trace.HashHex(), b.S.Trace.HashHex());
    }

    // ------------------------------------------------------------------------------------------------ helpers

    /// <summary>An ancient host (no coin: wages in kind) raiding 400 km into foreign hills and mountains with no road:
    /// beyond pack-animal range, so the home granary cannot reach it; it lives off a thin locality.</summary>
    internal static EconRig AncientDeepRaid(FoodOrdering ordering, out long start)
    {
        var cal = ShippedCalendar();
        start = cal.TurnStart(cal.Bands[1].FirstTurn + 5).Ms + 40 * Day;   // Bronze/Iron band: 1800-day turns, 14-day pulses
        var route = new Route(new[]
        {
            Seg(80, TerrainClass.Open, RoadClass.DirtPath, 0),
            Seg(150, TerrainClass.Hills, RoadClass.None, 1),
            Seg(250, TerrainClass.Mountain, RoadClass.None, 2),
        });
        var setup = new WarEconSetup(ordering, LogisticsRates.Proposal(), TerrainLogistics.Proposal(),
            new[] { Polity(0, WarTech.None, 120_000, 400_000_000), Polity(1, WarTech.None, 60_000, 100_000_000) },
            new[]
            {
                new LocalitySetup(0, 0, 3_000, 2_000_000, 0),
                new LocalitySetup(1, 1, 2_500, 2_500_000, 0),
                new LocalitySetup(2, 1, 600, 300_000, 0),
            },
            new[] { new ArmySetup(0, 0, 0, 0, AncientHost(), route, 70_000, true, 400_000, 0, 0, Array.Empty<(long, long)>()) });
        var rig = Build(setup, new[] { WarOf(0, start, start + 60 * 14 * Day, 0, 1) }, 2, start);
        rig.R.RunUntil(start + 61 * 14 * Day);
        return rig;
    }
}
