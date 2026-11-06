using System.Globalization;
using System.Text;
using Proto.M7.Events;
using Proto.M7.Spatial;
using Proto.M7.Time;
using Proto.M7.War;
using Proto.M7.WarEcon;
using static Proto.M7.Tests.TestSupport;

namespace Proto.M7.Tests.WarEconE;

internal sealed record EconRig(CampaignCalendar Cal, EventScheduler S, WarTheaterWorld Inner, WarEconomyWorld W, Runner R);

/// <summary>Rigs for the lane E war-economy tests. Pulse lengths are T2's PROPOSED per-band durations (not decided).</summary>
internal static class WarEconRigs
{
    public static readonly long[] ProposedPulseDays = { 21, 14, 10, 7, 3, 2, 1 };
    public static readonly Commodity[] AllCommodities =
        { Commodity.Money, Commodity.Goods, Commodity.People, Commodity.Grain, Commodity.Animals, Commodity.Munitions, Commodity.Kit, Commodity.Fuel,
          Commodity.Coal };

    public static EconRig Build(WarEconSetup setup, WarSpec[] wars, int polities, long startMs, CampaignCalendar? turnCal = null, long[]? pulseDays = null)
    {
        var bandCal = ShippedCalendar();
        var cal = turnCal ?? bandCal;
        var days = pulseDays ?? ProposedPulseDays;
        var table = new long[bandCal.Bands.Count];
        for (int i = 0; i < table.Length; i++) table[i] = days[i] * Day;
        var sched = new WarPulseSchedule(bandCal, table);
        var s = new EventScheduler(startMs);
        var inner = new WarTheaterWorld(cal, sched, polities, wars, WarFog.None, SignificantKinds.None, new long[polities], 0, 0);
        var w = new WarEconomyWorld(inner, cal, setup);
        w.Begin(s);
        var r = new Runner(s, w, SignificantKinds.None, stopOnSignificant: false);
        return new EconRig(cal, s, inner, w, r);
    }

    public static WarSpec WarOf(int id, long start, long stop, params int[] participants)
        => new(id, start, stop, participants, 0, 0, new long[participants.Length]);

    public static CampaignCalendar OneYearTurns()
    {
        var shipped = ShippedCalendar();
        var parts = shipped.Bands.Select(b => $"{{ \"name\": \"{b.Name}\", \"startYear\": {b.StartYear}, \"endYear\": {b.EndYear}, \"dtYears\": 1 }}");
        return CampaignCalendar.FromJson(Encoding.UTF8.GetBytes("{ \"bands\": [ " + string.Join(", ", parts) + " ] }"));
    }

    public static Int128[] Totals(WarLedger l)
    {
        var t = new Int128[AllCommodities.Length];
        for (int i = 0; i < t.Length; i++) t[i] = l.Total(AllCommodities[i]);
        return t;
    }

    public static PolitySetup Polity(int id, WarTech tech, long population, long granary, long treasury = 0, long baseTax = 0,
                                     long warTax = 0, long trade = 0, long blockade = 0, long lender = 0, long munitions = 0,
                                     long kit = 0, long fuel = 0, long growth = 50)
        => new(id, new PolityCapability(tech), population, granary, treasury, 1_000_000_000_000, 100_000, munitions, kit, fuel,
               lender, baseTax, warTax, trade, blockade, growth);

    public static RouteSegment Seg(long km, TerrainClass t, RoadClass road = RoadClass.None, int locality = -1, bool water = false, bool rail = false)
        => new(km * 1000, t, road, water, rail, locality);

    /// <summary>An early-modern field army: musketeers, horse, a gun train and an ox-drawn wagon train.</summary>
    public static ArmyComposition EarlyModernArmy() => new(5000, 600, 200, 700, 600, 150, 0, 20, true);

    /// <summary>A Bronze/Iron-age host: spearmen, chariot horses, pack and draft animals; no gunpowder, no coin.</summary>
    public static ArmyComposition AncientHost() => new(4000, 200, 0, 400, 800, 60, 0, 0, false);

    public static string Table(IReadOnlyList<ArmyPulseRow> rows, int army, long startMs = 0)
    {
        var sb = new StringBuilder();
        sb.AppendLine("| pulse | day | cond | men | need S/M (gm) | forage+seized | delivered | hunger ppm | move ppm | km | pay due/paid | arrears | morale | cohesion | deserted | died | des ‰/yr | dis ‰/yr | supply |");
        sb.AppendLine("|---|---|---|---|---|---|---|---|---|---|---|---|---|---|---|---|---|---|---|");
        foreach (var r in rows)
        {
            if (r.ArmyId != army) continue;
            sb.Append(CultureInfo.InvariantCulture, $"| {r.PulseIndex} | {(r.StartMs - startMs) / Day} | {r.ConditionAtStart}->{r.ConditionEnd} | {r.MenStart}->{r.MenEnd} | {r.StandingNeed}/{r.MovementNeed} | ");
            sb.Append(CultureInfo.InvariantCulture, $"{r.Foraged}+{r.Seized} | {r.Delivered} | {r.HungerPpm} | {r.MovementPpm} | {r.DistanceM / 1000} | {r.PayDue}/{r.PayPaid} | {r.ArrearsEnd} | ");
            sb.Append(CultureInfo.InvariantCulture, $"{r.MoraleEnd / 1000} | {r.CohesionEnd / 1000} | {r.Deserted} | {r.DiseaseDeaths} | {r.DesertionPermilleYear} | {r.DiseasePermilleYear} | {r.SupplyQ32Raw * 1000 / Fixed.Q32.OneRaw}‰ |");
            sb.AppendLine();
        }
        return sb.ToString();
    }
}
