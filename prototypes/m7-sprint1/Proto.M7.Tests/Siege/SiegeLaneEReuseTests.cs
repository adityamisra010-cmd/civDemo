using Proto.M7.Events;
using Proto.M7.Spatial;
using Proto.M7.Time;
using Proto.M7.War;
using Proto.M7.War.Siege;
using Proto.M7.WarEcon;
using Xunit.Abstractions;
using static Proto.M7.Tests.Siege.SiegeRig;
using static Proto.M7.Tests.TestSupport;

namespace Proto.M7.Tests.Siege;

/// <summary>
/// Lane S resume (2026-11-06): the siege and garrison world uses lane E's mechanisms instead of parallel ones. Each test pins one
/// reuse semantically in a running world: pay in coin or in kind (Coinage), the arrears clock driving desertion, the base desertion
/// of a garrison, Gunpowder as the capability that allows guns and firearms, Sanitation in disease deaths, gunpowder fire drawing
/// lane E munitions, and the besiegers' line of communication evaluated by lane E's supply line.
/// </summary>
public class SiegeLaneEReuseTests
{
    private readonly ITestOutputHelper _out;
    public SiegeLaneEReuseTests(ITestOutputHelper output) => _out = output;

    private static readonly long Start = At(1500, 100);
    private static readonly long WarStart = Start - 30 * Day;

    // ------------------------------------------------------------------ pay: lane E's pay table, coin with Coinage, grain without

    [Fact]
    public void Pay_IsCoinWithCoinage_AndGrainWithout_AtLaneE_Rates()
    {
        long t = WarStart + 18 * 7 * Day;                                  // a theatre pulse end: pay is settled up to it
        var coin = Build(Start, noSiege: true);
        var kind = Build(Start, noSiege: true, caps: new[] { new PolityCapability(WarTech.None), Powder, Powder },
                         formations: new[]
                         {
                             new FormationSpec(0, 0, FormationFamily.Infantry, 2_000, 10, 10) { GarrisonedAt = 0 },
                             new FormationSpec(1, 0, FormationFamily.Cavalry, 1_000, 10, 10) { GarrisonedAt = 0 },
                         });
        long grain0 = kind.W.Ledger.Balance(kind.W.HomeFoodOf(0));
        RunTo(coin, t);
        RunTo(kind, t);
        var r = coin.W.Rates;
        // coin: 3,000 foot at PayFoot soldier-months per man-year; in kind: 2,000 foot and 1,000 mounted at their gm wages
        long dt = t - WarStart;
        Assert.Equal((long)((Int128)3_000 * r.PayFoot * dt / SimInstant.MsPerYear), coin.W.Ledger.Balance(coin.W.UpkeepSink));
        Assert.Equal(0, coin.W.Ledger.Balance(coin.W.WagesInKindSink));
        long inKind = (long)((Int128)(2_000 * r.WageInKindFoot + 1_000 * r.WageInKindMounted) * dt / SimInstant.MsPerYear);
        Assert.Equal(inKind, kind.W.Ledger.Balance(kind.W.WagesInKindSink));
        Assert.Equal(grain0 - inKind, kind.W.Ledger.Balance(kind.W.HomeFoodOf(0)));     // drawn from the controller's home granary
        Assert.Equal(0, kind.W.Ledger.Balance(kind.W.UpkeepSink));                     // no coin moved
        Assert.Equal(0, kind.W.UpkeepArrearsOf(0));
    }

    // ------------------------------------------------------------------ the arrears clock drives desertion (lane E)

    [Fact]
    public void UnpaidPay_RunsUpArrears_WhoseAgeInMonths_DrivesDesertion_Conserved()
    {
        var paid = Build(Start, noSiege: true);
        var unpaid = Build(Start, noSiege: true, money: 0);
        var people = unpaid.W.Ledger.Total(Commodity.People);
        long Lost(Built b, long t) { RunTo(b, t); return 3_000 - b.W.GarrisonOf(0); }
        long l1 = Lost(unpaid, WarStart + 60 * Day), l2 = Lost(unpaid, WarStart + 120 * Day), l3 = Lost(unpaid, WarStart + 180 * Day);
        RunTo(paid, WarStart + 180 * Day);
        _out.WriteLine($"unpaid garrison losses by day 60/120/180: {l1}/{l2}/{l3}");
        Assert.Equal(3_000, paid.W.GarrisonOf(0));                                     // paid (and base desertion off in the rig)
        Assert.Equal(WarStart, unpaid.W.ArrearsSinceOf(0));                            // the treasury was dry from the first instant
        Assert.True(unpaid.W.UpkeepArrearsOf(0) > 0);
        // lane E: + DesertionPerArrearsMonth for every whole month of arrears: each 60-day window loses more than the one before
        Assert.True(l1 < l2 - l1 && l2 - l1 < l3 - l2, $"{l1} {l2 - l1} {l3 - l2}");
        Assert.Equal(l3, unpaid.W.Ledger.Balance(unpaid.W.DesertedSink));
        Assert.Equal(people, unpaid.W.Ledger.Total(Commodity.People));
    }

    [Fact]
    public void BaseDesertion_ThinsAPeacetimeGarrison_AtLaneE_BaseRate_CompoundedDaily()
    {
        var withBase = Build(Start, noSiege: true, rates: LogisticsRates.Proposal());
        var without = Build(Start, noSiege: true);
        long t = WarStart + 360 * Day;
        RunTo(withBase, t);
        RunTo(without, t);
        // 50 ‰/yr nominal, compounded per day: survival (1 − 50/360,000)^360 = 0.95123 → about 146 of 3,000 (2,000 + 1,000, each with its carry)
        long lost = 3_000 - withBase.W.GarrisonOf(0);
        _out.WriteLine($"base desertion over 360 days: {lost}");
        Assert.InRange(lost, 144, 148);
        Assert.Equal(lost, withBase.W.Ledger.Balance(withBase.W.DesertedSink));
        Assert.Equal(3_000, without.W.GarrisonOf(0));
    }

    [Fact]
    public void InvestedGarrison_Desertion_IsLaneE_Rate_ContainedByTheBesiegersLines_UnlessACorridorIsOpen()
    {
        // a hungry, demoralised garrison (5 days of food): lane E's desertion is high; the lines contain it in proportion to their works
        long Deserted(long works, bool corridor)
        {
            var b = Build(Start, rates: Rates with { DesertionBasePermilleYear = 50 }, inst: i => i with { Food = FoodDays(5) },
                          k: SiegeCoefficients.Default with { SurrenderHealthPermille = 0, AttackerCollapsePermille = 0 },
                          siege: s => s with { AttackerWorksPermille = works, EvacuationPerYear = corridor ? 1 : 0 });
            RunTo(b, Start + 60 * Day);
            return b.W.Ledger.Balance(b.W.DesertedSink);
        }
        long open = Deserted(1000, true), none = Deserted(0, false), half = Deserted(500, false), full = Deserted(1000, false);
        _out.WriteLine($"deserted in 60 days (both camps): corridor open {open}, lines 0 {none}, lines 0.5 {half}, lines 1 {full}");
        Assert.True(full < half && half < none, "more complete lines let fewer deserters through");
        Assert.True(open > full, "an open corridor lets them walk away");
    }

    // ------------------------------------------------------------------ Gunpowder: the capability that allows guns and firearms (Law 4)

    [Fact]
    public void GunsAndFirearms_NeedTheGunpowderCapability_StateDerived_NeverADate()
    {
        var noPowder = new PolityCapability(WarTech.Coinage);
        // the besiegers' bombards need their polity's Gunpowder
        Assert.Throws<ArgumentException>(() => Build(Start, caps: new[] { Powder, noPowder, Powder },
            siege: s => s with { Force = AttackerForce.Of(8_000, 2_000, 1_000, 500, RangedKind.Bow, bombards: 5) }));
        // a garrison's firearms need the owner's Gunpowder (the rig's air-defence formation 5 carries rifles)
        Assert.Throws<ArgumentException>(() => Build(Start, caps: new[] { noPowder, Powder, Powder }));
        // fortress guns need the controller's Gunpowder
        Assert.Throws<ArgumentException>(() => Build(Start, caps: new[] { noPowder, Powder, Powder }, inst: i => i with { Guns = 5 },
            formations: new[] { new FormationSpec(0, 0, FormationFamily.Infantry, 2_000, 10, 10) { GarrisonedAt = 0 } }));
        // the same forces with the capability build, at any date (Bronze/Iron here)
        var ok = Build(At(-1000, 100), siege: s => s with { Force = AttackerForce.Of(8_000, 2_000, 1_000, 500, RangedKind.Bow, bombards: 5) });
        Assert.Equal(SiegeRegime.Gunpowder, ok.W.RegimeOf(0));
    }

    // ------------------------------------------------------------------ gunpowder fire draws lane E munitions (kg)

    [Fact]
    public void Bombards_BreachOnlyWhileThePowderLasts_Throwers_NeedNone()
    {
        Q32Raw WallAfter(long homeMunitions, long bombards, long throwers)
        {
            var b = Build(Start, homeMunitions: homeMunitions, inst: i => i with { Fort = FortKind.StoneWall },
                          siege: s => s with { Force = AttackerForce.Of(8_000, 2_000, 1_000, 500, RangedKind.Bow, throwers: throwers, bombards: bombards), Mineable = false });
            RunTo(b, Start + 40 * Day);
            Assert.True(b.W.IsActive(0));
            return new Q32Raw(b.W.WallOf(0).Raw);
        }
        var powder = WallAfter(1_000_000_000_000, 10, 0);
        var noPowder = WallAfter(0, 10, 0);
        Assert.True(powder.Raw < noPowder.Raw, "bombards with powder breach faster");
        Assert.Equal(1L << 32, noPowder.Raw);                                         // without munitions (and no throwers) nothing hits the wall
        Assert.Equal(WallAfter(0, 0, 10).Raw, WallAfter(1_000_000_000_000, 0, 10).Raw); // throwers draw no munitions
    }

    private readonly record struct Q32Raw(long Raw);

    [Fact]
    public void GarrisonRifles_FireLaneE_Munitions_AndFightDryWithout_ConservedToTheKilogram()
    {
        var forms = new[]
        {
            new FormationSpec(0, 0, FormationFamily.Infantry, 2_000, 10, 10) { GarrisonedAt = 0 },
            new FormationSpec(1, 0, FormationFamily.Ranged, 1_000, 10, 10) { GarrisonedAt = 0, Ranged = RangedKind.Rifle },
        };
        long kg = 1_000L * 1 * 30 * SiegeCoefficients.Default.SiegeFireCombatDayPermille / 1000;   // 30 days of siege fire for 1,000 rifles
        var armed = Build(Start, formations: forms, inst: i => i with { Munitions = kg });
        var dry = Build(Start, formations: forms, inst: i => i with { Munitions = 0 });
        var muns = armed.W.Ledger.Total(Commodity.Munitions);
        RunTo(armed, Start + 12 * Day);
        RunTo(dry, Start + 12 * Day);
        Assert.True(armed.W.PulseRecords[^1].CapabilityMilli > dry.W.PulseRecords[^1].CapabilityMilli, "rifles with powder outfight rifles without");
        // 12 days of fire: exactly what the magazine lost went to the spent sink (exact, no rounding loss)
        long spent = kg - armed.W.MunitionsOf(0);
        Assert.Equal(1_000L * 1 * 12 * SiegeCoefficients.Default.SiegeFireCombatDayPermille / 1000, spent);
        Assert.Equal(muns, armed.W.Ledger.Total(Commodity.Munitions));
    }

    // ------------------------------------------------------------------ Sanitation in disease deaths (lane E factor)

    [Fact]
    public void Sanitation_CutsDiseaseDeaths_InACrowdedBesiegedCity()
    {
        var san = new PolityCapability(WarTech.Coinage | WarTech.Gunpowder | WarTech.Sanitation);
        long Deaths(PolityCapability defender, PolityCapability attacker)
        {
            var b = Build(Start, caps: new[] { defender, attacker, Powder }, inst: i => i with { Housing = 3_000 });
            RunTo(b, Start + 60 * Day);
            return b.W.Ledger.Balance(b.W.DiseaseSink);                // both camps' disease deaths
        }
        long neither = Deaths(Powder, Powder), defenderOnly = Deaths(san, Powder), both = Deaths(san, san);
        _out.WriteLine($"disease deaths in 60 days (both camps): neither {neither}, defender only {defenderOnly}, both {both}");
        Assert.True(both > 0);
        Assert.True(both * 2 < neither, "lane E's Sanitation factor (300 ‰) cuts disease mortality to well under half");
        Assert.True(both < defenderOnly && defenderOnly < neither, "each side's own capability cuts its own camp's deaths");
    }

    // ------------------------------------------------------------------ the besiegers' line of communication: lane E's supply line

    private static Route Road(long km) => new(new[] { new RouteSegment(km * 1000, TerrainClass.Open, RoadClass.Built, false, false, -1) });
    private static Route Trackless(long km) => new(new[] { new RouteSegment(km * 1000, TerrainClass.Hills, RoadClass.None, false, false, -1) });

    private static Built LineSiege(SiegeSupplyLine line)
        => Build(Start, siege: s => s with { SupplyLine = line, AttackerFood = SiegeScenarios.Food(11_500, 0, 20) });

    [Fact]
    public void SupplyLine_IsLaneE_Evaluate_RoadFeedsTheBesiegers_TracklessHillsStarveThem_Conserved()
    {
        var roadLine = new SiegeSupplyLine(Road(60), 300, 600, 0);
        var hillLine = new SiegeSupplyLine(Trackless(120), 0, 3_000, 0);
        var road = LineSiege(roadLine);
        var hill = LineSiege(hillLine);
        var grain = road.W.Ledger.Total(Commodity.Grain);
        RunTo(road, Start + 80 * Day);
        RunTo(hill, Start + 80 * Day);
        // the line is lane E's: same loss and throughput as ArmyLogistics.Evaluate on the same train
        var train = new ArmyComposition(8_000 + 2_000 + 500, 1_000, 0, 0, 600, 300, 0, 0, false);
        var expected = ArmyLogistics.Evaluate(roadLine.Route, roadLine.Route.Length, SimInstant.MsPerYear, train, Powder, road.W.Rates, TerrainLogistics.Proposal());
        var got = road.W.SupplyLineOf(0);
        Assert.True(got.HasLine);
        Assert.Equal((expected.Loss, expected.CapacityPerPulse), (got.Loss, got.CapacityPerYear));
        // what was shipped but did not arrive was eaten on the road (a sink), to the gm
        Assert.Equal(got.Shipped - got.Delivered, road.W.Ledger.Balance(road.W.CarriageLossSink));
        Assert.True(got.Delivered > 0 && got.Shipped > got.Delivered);
        var h = hill.W.SupplyLineOf(0);
        _out.WriteLine($"road: loss {got.Loss} cap {got.CapacityPerYear} gm/yr; hills: loss {h.Loss} cap {h.CapacityPerYear} gm/yr");
        Assert.True(h.Loss > got.Loss);
        // the road keeps the besiegers fed; across trackless hills the opening 20 days run out and hunger bites
        Assert.True(road.W.AttackerFoodOf(0) > 0);
        Assert.Equal(0, hill.W.AttackerFoodOf(0));
        Assert.True(hill.W.AttackerHealthOf(0) < road.W.AttackerHealthOf(0));
        Assert.Equal(grain, road.W.Ledger.Total(Commodity.Grain));
    }

    [Fact]
    public void SupplyLine_TrainIsValidatedByLaneE_TrucksNeedMotorTransport()
        => Assert.Throws<ArgumentException>(() => LineSiege(new SiegeSupplyLine(Road(60), 0, 0, 50)));
}
