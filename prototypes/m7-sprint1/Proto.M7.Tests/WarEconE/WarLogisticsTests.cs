using System.Numerics;
using Proto.M7.Spatial;
using Proto.M7.Time;
using Proto.M7.WarEcon;
using static Proto.M7.Tests.WarEconE.WarEconRigs;

namespace Proto.M7.Tests.WarEconE;

/// <summary>Lane E: movement consumption by composition, distance, terrain, roads and state-derived technology (DIR5 §2.11),
/// and the supply-line arithmetic. Pure functions; expected values are independent closed forms.</summary>
public class WarLogisticsTests
{
    private static readonly LogisticsRates R = LogisticsRates.Proposal();
    private static readonly TerrainLogistics T = TerrainLogistics.Proposal();
    private const long Day = SimInstant.MsPerDay;
    private static readonly PolityCapability Ancient = new(WarTech.None);

    private static MarchPlan March(RouteSegment seg, ArmyComposition c, PolityCapability cap, long days = 7)
        => ArmyLogistics.PlanMarch(new Route(new[] { seg }), 0, days * Day, c, cap, R, T);

    [Fact]
    public void March_OpenGroundFootSpeed_IsTheClosedForm()
    {
        var foot = new ArmyComposition(1000, 0, 0, 0, 0, 0, 0, 0, false);
        var p = March(Seg(1000, TerrainClass.Open), foot, Ancient);
        Assert.Equal(7 * R.MarchFootMPerDay, p.DistanceM);
        Assert.Equal(7 * Day, p.MarchMs);
        // Halts at the route's end: 50 km at 20 km/day takes 2.5 days.
        var q = March(Seg(50, TerrainClass.Open), foot, Ancient);
        Assert.Equal(50_000, q.DistanceM);
        Assert.Equal(5 * Day / 2, q.MarchMs);
    }

    /// <summary>"Roads reduce costs. Jungle and mountains increase costs." (DIR5 §2.11): distance per pulse falls from a built
    /// road to open ground to forest, hills, jungle and mountain; any road on rough ground beats the same ground without.</summary>
    [Fact]
    public void TerrainAndRoads_OrderTheMarchExactlyAsDirected()
    {
        var c = AncientHost();
        long D(TerrainClass t, RoadClass r) => March(Seg(5000, t, r), c, Ancient).DistanceM;
        Assert.True(D(TerrainClass.Open, RoadClass.Built) > D(TerrainClass.Open, RoadClass.Track));
        Assert.True(D(TerrainClass.Open, RoadClass.Track) > D(TerrainClass.Open, RoadClass.None));
        Assert.True(D(TerrainClass.Open, RoadClass.None) > D(TerrainClass.Forest, RoadClass.None));
        Assert.True(D(TerrainClass.Forest, RoadClass.None) > D(TerrainClass.Hills, RoadClass.None));
        Assert.True(D(TerrainClass.Hills, RoadClass.None) > D(TerrainClass.Jungle, RoadClass.None));
        Assert.True(D(TerrainClass.Jungle, RoadClass.None) > D(TerrainClass.Mountain, RoadClass.None));
        foreach (var t in new[] { TerrainClass.Forest, TerrainClass.Jungle, TerrainClass.Hills, TerrainClass.Mountain })
            Assert.True(D(t, RoadClass.Built) > D(t, RoadClass.None));
        // An ox-drawn train sets the pace (15 km/day) below foot (20 km/day).
        Assert.Equal(7 * R.MarchOxTrainMPerDay, March(Seg(1000, TerrainClass.Open), c, Ancient).DistanceM);
    }

    /// <summary>Railways are a capability (Law 4): a rail segment carries the army at rail speed only for a polity that holds
    /// Railways; for one that does not, the same segment is ordinary ground.</summary>
    [Fact]
    public void Rail_OnlyWithTheRailwaysCapability()
    {
        var c = new ArmyComposition(10_000, 0, 0, 0, 0, 0, 0, 0, false);
        var seg = Seg(5000, TerrainClass.Hills, RoadClass.Track, rail: true);
        Assert.Equal(7 * R.MarchRailMPerDay, March(seg, c, new PolityCapability(WarTech.Railways)).DistanceM);
        Assert.Equal(March(seg with { Rail = false }, c, Ancient).DistanceM, March(seg, c, Ancient).DistanceM);
    }

    /// <summary>Supply-line loss is exact: pack animals over 100 km of open ground lose 100/150 of the cargo; over hills the
    /// loss scales with the ground's cost (1800/1000) and is capped at all of it; magazines cut it to 600‰; water loses
    /// distance / 6000 km; delivered(gross(x)) &gt;= x with gross minimal.</summary>
    [Fact]
    public void SupplyLine_LossFractions_AreExact()
    {
        var c = AncientHost() with { Wagons = 0 };
        SupplyLine L(RouteSegment s, PolityCapability cap, long p) => ArmyLogistics.Evaluate(new Route(new[] { s }), p, 7 * Day, c, cap, R, T);
        Assert.Equal(ExactFraction.Of(100, 150), L(Seg(200, TerrainClass.Open), Ancient, 100_000).Loss);
        Assert.Equal(ExactFraction.Of(50 * 1800, 150 * 1000), L(Seg(200, TerrainClass.Hills), Ancient, 50_000).Loss);
        Assert.Equal(ExactFraction.One, L(Seg(400, TerrainClass.Hills), Ancient, 400_000).Loss);
        Assert.Equal(ExactFraction.Of(100 * 600, 150 * 1000), L(Seg(200, TerrainClass.Open), new PolityCapability(WarTech.Magazines), 100_000).Loss);
        Assert.Equal(ExactFraction.Of(300, 6000), L(Seg(500, TerrainClass.Open, water: true), Ancient, 300_000).Loss);
        // Wagons on a road beat pack animals off it. On a Track the haul is faster (speedFactor 0.85 CITED), so the carriers
        // eat less of the cargo (fix round 1, E-V5; before the fix the road did not enter the loss: 100/600).
        var wag = AncientHost();
        var road = ArmyLogistics.Evaluate(new Route(new[] { Seg(200, TerrainClass.Open, RoadClass.Track) }), 100_000, 7 * Day, wag, Ancient, R, T);
        Assert.Equal(ExactFraction.Of(100 * 850, 600 * 1000), road.Loss);
        for (long x = 1; x < 5000; x += 37)
        {
            var loss = ExactFraction.Of(100, 150);
            long g = ArmyLogistics.Gross(x, loss);
            Assert.True(ArmyLogistics.Delivered(g, loss) >= x);
            Assert.True(ArmyLogistics.Delivered(g - 1, loss) < x);
        }
        Assert.Equal(-1, ArmyLogistics.Gross(10, ExactFraction.One));
    }

    /// <summary>Throughput: the fleet cap is carriers × payload × round trips in the pulse (closed form), and the infrastructure
    /// cap is the sim.json road capacity (CITED) pro rata over the pulse.</summary>
    [Fact]
    public void SupplyLine_Throughput_FleetAndInfrastructure()
    {
        var c = new ArmyComposition(1000, 0, 0, 0, 40, 20, 0, 0, false);   // 20 wagons, 40 oxen, none spare
        var line = ArmyLogistics.Evaluate(new Route(new[] { Seg(300, TerrainClass.Open, RoadClass.Built) }), 150_000, 7 * Day, c, Ancient, R, T);
        long fleet = (long)((BigInteger)20 * R.WagonPayloadGm * (7 * Day) * R.MarchOxTrainMPerDay / ((BigInteger)2 * 150_000 * Day));
        long infra = (long)((BigInteger)R.CapacityBuiltGmPerYear * (7 * Day) / SimInstant.MsPerYear);
        Assert.Equal(Math.Min(fleet, infra), line.CapacityPerPulse);
        Assert.Equal(fleet, line.CapacityPerPulse);
        // Twice the distance halves the round trips.
        var far = ArmyLogistics.Evaluate(new Route(new[] { Seg(300, TerrainClass.Open, RoadClass.Built) }), 300_000, 7 * Day, c, Ancient, R, T);
        Assert.Equal((long)((BigInteger)20 * R.WagonPayloadGm * (7 * Day) * R.MarchOxTrainMPerDay / ((BigInteger)2 * 300_000 * Day)), far.CapacityPerPulse);
    }

    /// <summary>Motor carriage loses no cargo but burns fuel: litres = ceil(gm × 2 × km × 400 L/1000 km / payload).</summary>
    [Fact]
    public void Motor_CarriageBurnsFuel_NotCargo()
    {
        var c = new ArmyComposition(2000, 0, 0, 0, 0, 0, 100, 0, false);
        var cap = new PolityCapability(WarTech.MotorTransport);
        var line = ArmyLogistics.Evaluate(new Route(new[] { Seg(500, TerrainClass.Open, RoadClass.Built) }), 200_000, 2 * Day, c, cap, R, T);
        Assert.Equal(ExactFraction.Zero, line.Loss);
        Assert.Equal(200_000, line.MotorHaulM);
        long gm = 1_000_000;
        long expect = (long)(((BigInteger)gm * 2 * 200_000 * R.FuelPerTruck1000Km + (BigInteger)R.TruckPayloadGm * 1_000_000 - 1) / ((BigInteger)R.TruckPayloadGm * 1_000_000));
        Assert.Equal(expect, ArmyLogistics.CarriageFuel(gm, line, R));
        Assert.Equal(CarriageMode.Wagon, ArmyLogistics.ModeOf(Seg(1, TerrainClass.Open, RoadClass.Built), Ancient, AncientHost()));
        Assert.Equal(CarriageMode.Motor, ArmyLogistics.ModeOf(Seg(1, TerrainClass.Open, RoadClass.Built), cap, c));
    }

    /// <summary>dt-correctness (Law 3): the standing need summed over the pulses of 84 days is floor(rate × 84 d / Y) with
    /// 7-day pulses and with 3-day pulses alike (exact carry across pulses; no per-pulse rounding drift).</summary>
    [Fact]
    public void StandingNeed_IsDtCorrect_IndependentOfPulseLength()
    {
        var rates = R with { DesertionBasePermilleYear = 0, DesertionLowMoralePermilleYear = 0, DiseaseBasePermilleYear = 0, AnimalDeathBasePermilleYear = 0 };
        var cal = TestSupport.ShippedCalendar();
        long start = cal.TurnStart(cal.Bands[3].FirstTurn).Ms + 10 * Day, span = 84 * Day;
        long Sum(long days)
        {
            var setup = new WarEconSetup(FoodOrdering.CivilianMilitaryGrowth, rates, T,
                new[] { Polity(0, WarTech.Coinage, 50_000, 1_000_000_000, 10_000_000), Polity(1, WarTech.None, 1000, 0) },
                Array.Empty<LocalitySetup>(),
                new[] { new ArmySetup(0, 0, 0, 0, AncientHost(), new Route(new[] { Seg(100, TerrainClass.Hills) }), 0, false, 0, 0, 0, Array.Empty<(long, long)>()) });
            var table = new long[] { days, days, days, days, days, days, days };
            var rig = Build(setup, new[] { WarOf(0, start, start + span, 0, 1) }, 2, start, pulseDays: table);
            rig.R.RunUntil(start + span);
            Assert.Equal(span / (days * Day), rig.W.Rows.Count);
            return rig.W.Rows.Sum(r => r.StandingNeed);
        }
        var c = AncientHost();
        long graze = T.GrazePermille(TerrainClass.Hills);
        BigInteger rateScaled = (BigInteger)c.Men * (rates.RationBase + rates.RationFieldExtra) * 1000
            + (BigInteger)c.Horses * rates.HorseHardRest * 1000 + (BigInteger)c.DraftAnimals * rates.DraftHardRest * 1000
            + ((BigInteger)c.Horses * rates.HorseRoughageAsGrain + (BigInteger)c.DraftAnimals * rates.DraftRoughageAsGrain) * (1000 - graze);
        long closed = (long)(rateScaled * span / ((BigInteger)SimInstant.MsPerYear * 1000));
        Assert.Equal(closed, Sum(7));
        Assert.Equal(closed, Sum(3));
    }

    /// <summary>Capability is derived from known technology ids (the stand-in for the research table): a flag is held exactly
    /// when the id mapped to its bit is known; unknown ids are ignored; the empty set holds nothing.</summary>
    [Fact]
    public void Capability_DerivedFromKnownTechnologies()
    {
        int[] idOfBit = { 101, 102, 103, 104, 105, 106, 107, 108 };   // Coinage, Banking, Gunpowder, Magazines, Railways, Motor, (bit 6 unused), Sanitation
        var c = PolityCapability.FromKnown(new[] { 103, 999, 101, 108 }, idOfBit);
        Assert.Equal(WarTech.Coinage | WarTech.Gunpowder | WarTech.Sanitation, c.Held);
        Assert.True(c.Has(WarTech.Gunpowder));
        Assert.False(c.Has(WarTech.Railways));
        Assert.False(c.Has(WarTech.Coinage | WarTech.Banking));
        Assert.Equal(WarTech.None, PolityCapability.FromKnown(Array.Empty<int>(), idOfBit).Held);
    }

    /// <summary>Composition: horses and draft animals add fodder; the march adds exertion only while marching.</summary>
    [Fact]
    public void Composition_AnimalsAddFodder_MarchAddsExertion()
    {
        var cal = TestSupport.ShippedCalendar();
        long start = cal.TurnStart(cal.Bands[3].FirstTurn).Ms + 10 * Day;
        ArmyPulseRow One(ArmyComposition c, bool march)
        {
            var setup = new WarEconSetup(FoodOrdering.CivilianMilitaryGrowth, R, T,
                new[] { Polity(0, WarTech.Coinage, 50_000, 1_000_000_000, 10_000_000), Polity(1, WarTech.None, 1000, 0) },
                Array.Empty<LocalitySetup>(),
                new[] { new ArmySetup(0, 0, 0, 0, c, new Route(new[] { Seg(500, TerrainClass.Open, RoadClass.Track) }), 0, march, 0, 0, 0, Array.Empty<(long, long)>()) });
            var rig = Build(setup, new[] { WarOf(0, start, start + 7 * Day, 0, 1) }, 2, start);
            rig.R.RunUntil(start + 7 * Day);
            return rig.W.Rows.Single();
        }
        var foot = new ArmyComposition(1000, 0, 0, 0, 0, 0, 0, 0, false);
        var horsed = foot with { Mounted = 0, Horses = 300 };
        Assert.True(One(horsed, false).StandingNeed > One(foot, false).StandingNeed);
        Assert.Equal(0, One(foot, false).MovementNeed);
        long expect = (long)((BigInteger)1000 * R.RationMarchExtra * (7 * Day) / SimInstant.MsPerYear);
        Assert.Equal(expect, One(foot, true).MovementNeed);
        Assert.True(One(horsed, true).MovementNeed > One(foot, true).MovementNeed);
    }
}
