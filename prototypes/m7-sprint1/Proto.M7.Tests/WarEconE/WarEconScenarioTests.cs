using System.Globalization;
using System.Numerics;
using Proto.M7.Spatial;
using Proto.M7.Time;
using Proto.M7.War;
using Proto.M7.WarEcon;
using Xunit.Abstractions;
using static Proto.M7.Tests.WarEconE.WarEconRigs;

namespace Proto.M7.Tests.WarEconE;

/// <summary>Lane E scenario runs and the calibration table (reports/H-E.md §3, §4). The printed tables are the report's
/// measured evidence; the assertions pin the properties the tables are cited for.</summary>
public class WarEconScenarioTests
{
    private readonly ITestOutputHelper _out;
    public WarEconScenarioTests(ITestOutputHelper output) => _out = output;
    private const long Day = SimInstant.MsPerDay;

    /// <summary>
    /// The starvation path (no coin, wages in kind): a Bronze/Iron-age host marches 400 km into foreign hills and mountains
    /// without roads. Pack-animal carriage cannot reach it beyond the logistic radius, so it lives off the localities it
    /// crosses: orderly foraging, then forced requisition (with famine risk for the locals), then hunger. Hungry, it stops
    /// (movement restricted before starvation), disease and desertion rise, wages in kind cannot be delivered, arrears
    /// grow. Every commodity is conserved exactly throughout.
    /// </summary>
    [Fact]
    public void AncientDeepRaid_StarvationCascade_PulseByPulse()
    {
        var rig = WarEconomyWorldTests.AncientDeepRaid(FoodOrdering.CivilianMilitaryGrowth, out long start);
        _out.WriteLine(Table(rig.W.Rows, 0, start));
        foreach (var e in rig.W.Events) _out.WriteLine($"{(e.AtMs - start) / Day,5} d  {e.Kind,-20} {e.Value}");
        var rows = rig.W.Rows.ToList();
        Assert.Contains(rig.W.Events, e => e.Kind == CascadeKind.ForcedRequisition && e.Polity == 0);   // seized abroad
        Assert.Contains(rig.W.Events, e => e.Kind == CascadeKind.LocalFamine && e.Polity == 1);         // locals' famine risk
        Assert.Contains(rows, r => r.HungerPpm > 0);
        Assert.Contains(rows, r => r.ArrearsEnd > 0);                                                   // wages in kind undelivered
        Assert.All(rows, r => Assert.Equal(0, r.FuelUsed));
        var firstHungry = rows.First(r => r.HungerPpm > 0);
        Assert.True(rows.Last().MenEnd < rows.First().MenStart);
        Assert.True(firstHungry.Delivered == 0 || firstHungry.CarriageLoss > 0);
        // Nothing is delivered beyond the pack-animal radius: the whole shipment would be eaten on the way.
        Assert.All(rows.Where(r => r.PositionEnd > 230_000 && r.StartMs > firstHungry.StartMs), r => Assert.Equal(0, r.Delivered));
    }

    /// <summary>
    /// The calibration table (reports/H-E.md §3): per-day consumption of three archetypal forces computed by the model's own
    /// arithmetic from LogisticsRates.Proposal, converted to kilograms at 0.252 kg per gm (the PROPOSED mapping of the
    /// shipped grain unit, one adult-year, to about 0.7 kg of grain a day). The assertions pin the derivation, not history.
    /// </summary>
    [Fact]
    public void CalibrationTable_PerDayConsumption_ByComposition()
    {
        var r = LogisticsRates.Proposal();
        string Kg(BigInteger gmPerYearTimes1000) => (gmPerYearTimes1000 * 252 / 360 / 1000 / 1000).ToString(CultureInfo.InvariantCulture) + "."
            + (gmPerYearTimes1000 * 252 / 360 / 1000 % 1000).ToString("000", CultureInfo.InvariantCulture);
        // per man-day, kg: standing (base + field), marching (+ march extra)
        BigInteger stand = (BigInteger)(r.RationBase + r.RationFieldExtra) * 1000, march = (BigInteger)(r.RationBase + r.RationFieldExtra + r.RationMarchExtra) * 1000;
        BigInteger horseWork = (BigInteger)(r.HorseHardRest + r.HorseHardWorkExtra) * 1000, horseHay = (BigInteger)r.HorseRoughageAsGrain * 1000;
        _out.WriteLine("| quantity | model | unit |");
        _out.WriteLine("|---|---|---|");
        _out.WriteLine($"| civilian subsistence (base ration) | {Kg((BigInteger)r.RationBase * 1000)} | kg grain-eq / person-day |");
        _out.WriteLine($"| soldier in camp (base + field) | {Kg(stand)} | kg / man-day |");
        _out.WriteLine($"| soldier marching | {Kg(march)} | kg / man-day |");
        _out.WriteLine($"| horse at work, hard feed | {Kg(horseWork)} | kg grain / horse-day |");
        _out.WriteLine($"| horse roughage, as grain if not grazed | {Kg(horseHay)} | kg grain-eq / horse-day |");
        _out.WriteLine($"| draft animal at work, hard feed | {Kg((BigInteger)(r.DraftHardRest + r.DraftHardWorkExtra) * 1000)} | kg / animal-day |");
        _out.WriteLine($"| pay, infantry / cavalry / gunner | {r.PayFoot} / {r.PayMounted} / {r.PayGunCrew} | soldier-months per man-year |");
        _out.WriteLine($"| truck fuel | {r.FuelPerTruck1000Km / 1000}.{r.FuelPerTruck1000Km % 1000:000} | L / truck-km |");
        _out.WriteLine($"| foot march (open, sustained) | {r.MarchFootMPerDay / 1000} | km / day |");
        Assert.Equal("1.330", Kg(march));                      // 1900 gm/yr x 0.252 kg / 360 d
        Assert.Equal("4.480", Kg(horseWork));                  // 6400 gm/yr
        Assert.Equal("0.700", Kg((BigInteger)r.RationBase * 1000));

        // Per-day food and fodder for three forces, and what 100 km of each carriage mode loses.
        var t = TerrainLogistics.Proposal();
        (string name, ArmyComposition c, PolityCapability cap)[] forces =
        {
            ("Bronze/Iron host (AncientHost)", AncientHost(), new PolityCapability(WarTech.None)),
            ("Early-modern army (EarlyModernArmy)", EarlyModernArmy(), new PolityCapability(WarTech.Coinage | WarTech.Gunpowder | WarTech.Magazines)),
            ("Motorised force, 20,000 men, 1,000 trucks", new ArmyComposition(18_000, 0, 2_000, 0, 0, 0, 1_000, 72, true),
                new PolityCapability(WarTech.Coinage | WarTech.Gunpowder | WarTech.Magazines | WarTech.Railways | WarTech.MotorTransport)),
        };
        _out.WriteLine("");
        _out.WriteLine("| force | men | food+fodder marching, t/day | of which fodder, t/day | march km/7 d (open, no road) | (track) |");
        _out.WriteLine("|---|---|---|---|---|---|");
        foreach (var (name, c, cap) in forces)
        {
            BigInteger men = (BigInteger)c.Men * (r.RationBase + r.RationFieldExtra + r.RationMarchExtra);
            BigInteger fodder = (BigInteger)c.Horses * (r.HorseHardRest + r.HorseHardWorkExtra + r.HorseRoughageAsGrain)
                              + (BigInteger)c.DraftAnimals * (r.DraftHardRest + r.DraftHardWorkExtra + r.DraftRoughageAsGrain);
            BigInteger tPerDay1000 = (men + fodder) * 252 / 360;   // kg x 1000 per day -> tonnes
            var open = ArmyLogistics.PlanMarch(new Route(new[] { Seg(5000, TerrainClass.Open) }), 0, 7 * Day, c, cap, r, t).DistanceM / 1000;
            var track = ArmyLogistics.PlanMarch(new Route(new[] { Seg(5000, TerrainClass.Open, RoadClass.Track) }), 0, 7 * Day, c, cap, r, t).DistanceM / 1000;
            _out.WriteLine($"| {name} | {c.Men} | {tPerDay1000 / 1_000_000}.{tPerDay1000 / 1000 % 1000:000} | {fodder * 252 / 360 / 1_000_000} | {open} | {track} |");
        }
        _out.WriteLine("");
        _out.WriteLine("| carriage mode, 100 km of open ground | share of cargo lost |");
        _out.WriteLine("|---|---|");
        var anc = new PolityCapability(WarTech.None);
        foreach (var (mode, seg, c) in new (string, RouteSegment, ArmyComposition)[]
        {
            ("pack animals, no road", Seg(200, TerrainClass.Open), AncientHost() with { Wagons = 0 }),
            ("ox wagons, track", Seg(200, TerrainClass.Open, RoadClass.Track), AncientHost()),
            ("river boats", Seg(200, TerrainClass.Open, water: true), AncientHost()),
        })
        {
            var line = ArmyLogistics.Evaluate(new Route(new[] { seg }), 100_000, 7 * Day, c, anc, r, t);
            _out.WriteLine($"| {mode} | {line.Loss.Num * 1000 / line.Loss.Den}‰ |");
        }
        var rail = ArmyLogistics.Evaluate(new Route(new[] { Seg(200, TerrainClass.Open, rail: true) }), 100_000, 7 * Day, AncientHost(), new PolityCapability(WarTech.Railways), r, t);
        _out.WriteLine($"| railway (Railways held) | {rail.Loss.Num * 1_000_000 / rail.Loss.Den} ppm |");
        Assert.Equal(ExactFraction.Of(100, 40_000), rail.Loss);
    }
}
