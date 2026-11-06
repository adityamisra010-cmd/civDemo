using Proto.M7.Events;
using Proto.M7.Time;
using Proto.M7.WarEcon;

namespace Proto.M7.War.Siege;

/// <summary>
/// One historical siege as calibration input (defender = polity 0, attacker = polity 1, installation 0, siege 0). Every number is a
/// TEST INPUT taken from the historical record as summarised in reports/H-S.md §6 (with the uncertainty stated there); none is a
/// gameplay value. HistoryDaysMin/Max bracket the documented duration; HistoryOutcomes lists the model outcomes that count as a
/// match for the historical result. Fit = used to set the FITTED coefficients; holdout scenarios were not used for fitting.
/// </summary>
public sealed record HistoricalSiege(string Key, string Name, long Year, long DayOfYear, bool Fit,
    InstallationSpec Installation, FormationSpec[] Garrison, SiegeSpec Siege,
    long HistoryDaysMin, long HistoryDaysMax, SiegeOutcome[] HistoryOutcomes, string History)
{
    public long HomeTroops { get; init; }
    /// <summary>The defender's (polity 0) and the attacker's (polity 1) war capability as of the siege (lane E flags; TEST INPUT
    /// standing for each polity's computed state): Coinage, Gunpowder, Sanitation.</summary>
    public PolityCapability DefenderCapability { get; init; }
    public PolityCapability AttackerCapability { get; init; }
}

public readonly record struct SiegeRunResult(string Key, SiegeOutcome Outcome, long EndMs, long DurationDays, long DurationMs, long Pulses,
    long Evaluations, long EventsExecuted, int Assaults, int FailedAssaults, int Mines, long GarrisonEnd, long CiviliansEnd, long AttackersEnd,
    long CapMs, string StateHash, long StarvedOrThirst, long DiedOfDisease, long Killed, long Deserted);

/// <summary>Runs a calibration scenario under a given clock and shortfall policy (stops off: an AI-vs-AI siege).</summary>
public static class SiegeHarness
{
    public const ulong Seed = 0x5E6E_2026_1105UL;

    public static long InstantOf(CampaignCalendar cal, long year, long dayOfYear)
        => checked(((year - cal.Bands[0].StartYear) * SimInstant.YearDays + dayOfYear) * SimInstant.MsPerDay);

    public static SiegeWorld Build(HistoricalSiege h, CampaignCalendar cal, WarPulseSchedule schedule, SiegeClock clock, ShortfallPolicy policy,
                                   SiegeCoefficients? k, out EventScheduler scheduler, out long startMs, ulong seed = Seed, LogisticsRates? rates = null)
    {
        startMs = InstantOf(cal, h.Year, h.DayOfYear);
        long warStart = startMs - 45 * SimInstant.MsPerDay;
        long warEnd = startMs + 4 * cal.TurnLengthMs(cal.TurnOf(new SimInstant(startMs))) + SimInstant.MsPerYear;
        var siege = h.Siege with
        {
            StartMs = startMs, CorridorFromMs = startMs + h.Siege.CorridorFromMs,
            Relief = h.Siege.Relief is null ? null : h.Siege.Relief with { ArrivalMs = startMs + h.Siege.Relief.ArrivalMs },
        };
        scheduler = new EventScheduler(warStart);
        var w = new SiegeWorld(cal, schedule, warStart, warEnd, clock, k ?? SiegeCoefficients.Default, rates,
            new[] { h.DefenderCapability, h.AttackerCapability }, policy, seed, new[] { h.Installation }, h.Garrison, new[] { siege }, null,
            openingMoney: 1_000_000_000_000, homeFood: 1_000_000_000_000_000, homeMunitions: 1_000_000_000_000, homeMissiles: 1_000_000_000_000,
            homeTroops: h.HomeTroops);
        w.Begin(scheduler);
        return w;
    }

    public static SiegeRunResult Run(HistoricalSiege h, CampaignCalendar cal, WarPulseSchedule schedule, SiegeClock clock, ShortfallPolicy policy,
                                     SiegeCoefficients? k = null, bool recordPulses = false, ulong seed = Seed, LogisticsRates? rates = null)
    {
        var w = Build(h, cal, schedule, clock, policy, k, out var s, out long start, seed, rates);
        w.RecordPulses = recordPulses;
        s.Trace.Enabled = false;
        var runner = new Runner(s, w, SignificantKinds.None, stopOnSignificant: false);
        long cap = start + 3 * cal.TurnLengthMs(cal.TurnOf(new SimInstant(start)));
        // run in slices until the siege has ended (the cap bounds it)
        long t = start;
        while (w.IsActive(0) || w.EndOf(0) < 0)
        {
            t = Math.Min(cap, t + 30 * SimInstant.MsPerDay);
            runner.RunUntil(t);
            if (t >= cap) break;
        }
        if (w.IsActive(0)) throw new InvalidOperationException("siege still active after its maximum duration");
        var end = w.Ends[0];
        long starved = w.Ledger.Balance(w.StarvedSink);
        return new SiegeRunResult(h.Key, end.Outcome, end.AtMs, end.DurationMs / SimInstant.MsPerDay, end.DurationMs, end.Pulses, w.Evaluations,
            s.ExecutedCount, end.Assaults, end.FailedAssaults, end.Mines, end.Garrison, end.Civilians, end.Attackers, w.CapOf(0), w.StateHashHex(),
            starved, w.Ledger.Balance(w.DiseaseSink), w.Ledger.Balance(w.KilledSink), w.Ledger.Balance(w.DesertedSink));
    }

    public static bool Matches(HistoricalSiege h, SiegeOutcome o)
    {
        foreach (var x in h.HistoryOutcomes) if (x == o) return true;
        return false;
    }
}

/// <summary>The calibration set: five fit sieges and four holdout sieges (see the report for sources and uncertainty).</summary>
public static class SiegeScenarios
{
    private const long D = SimInstant.MsPerDay;
    private static readonly SiegeDoctrine Doctrine = SiegeDoctrine.Default;

    private static FormationSpec F(int id, FormationFamily fam, long n, RangedKind r = RangedKind.None)
        => new(id, 0, fam, n, 0, 0) { Ranged = r, GarrisonedAt = 0 };

    private static InstallationSpec City(long civ, long food, FortKind fort, long works, long housing, long cap, long guns = 0, long crew = 10,
                                         long water = 0, long waterIn = 0, long munitions = 0, long missiles = 0)
        => new(0, InstallationKind.City, 0, 0, 0, cap)
        {
            Civilians = civ, Food = food, Fort = fort, WallPermille = 1000, FieldWorksPermille = works, Housing = housing, Guns = guns, GunCrew = crew,
            Water = water, WaterInflowPerYear = waterIn, Munitions = munitions, Missiles = missiles, ExitX = 1, ExitY = 0,
        };

    // ---- unit helpers: the historical inputs are "days of X for N people"; the stocks are in lane E's units (Grain gm, Munitions kg)
    private static readonly LogisticsRates R = LogisticsRates.Proposal();
    private static readonly long SoldierRation = R.RationBase + R.RationFieldExtra, CivilianRation = R.RationBase;

    /// <summary>Full rations of soldiers and civilians per sim-year, gm.</summary>
    public static long RationYear(long soldiers, long civilians) => checked(soldiers * SoldierRation + civilians * CivilianRation);

    /// <summary>Food for <paramref name="days"/> days at full ration, gm.</summary>
    public static long Food(long soldiers, long civilians, long days) => checked(RationYear(soldiers, civilians) * days / SimInstant.YearDays);

    /// <summary>Munitions (kg) for <paramref name="days"/> days of siege fire of gunmen and guns (lane E kg per combat day × the siege share).</summary>
    public static long MunitionsKg(long gunmen, long guns, long days)
        => checked((gunmen * R.MunitionsPerFirearmCombatDay + guns * R.MunitionsPerGunCombatDay) * days * SiegeCoefficients.Default.SiegeFireCombatDayPermille / 1000);

    /// <summary>Munitions (kg) for <paramref name="days"/> days of bombardment by gunpowder engines.</summary>
    public static long BombardKg(long engines, long days)
        => checked(engines * R.MunitionsPerGunCombatDay * days * SiegeCoefficients.Default.BombardmentCombatDayPermille / 1000);

    private static readonly PolityCapability Ancient = new(WarTech.Coinage);
    private static readonly PolityCapability EarlyModern = new(WarTech.Coinage | WarTech.Gunpowder);
    private static readonly PolityCapability Modern = new(WarTech.Coinage | WarTech.Gunpowder | WarTech.Sanitation);

    public static IReadOnlyList<HistoricalSiege> All { get; } = Build();

    private static HistoricalSiege[] Build()
    {
        var list = new List<HistoricalSiege>();

        // ---- FIT ---------------------------------------------------------------------------------------------------
        // Alesia, 52 BC. Gauls ~70,000 (Caesar: 80,000), grain for ~30 days (BG 7.71), Mandubii expelled (civilians 0),
        // Roman lines (circum- and contravallation), relief army defeated, Vercingetorix surrenders.
        list.Add(new HistoricalSiege("alesia", "Alesia (52 BC)", -52, 240, true,
            City(0, Food(70_000, 0, 30), FortKind.MudbrickWall, 300, 40_000, 100_000, water: 70_000 * 20, waterIn: 70_000 * 360, missiles: 10_000 * 60),
            new[] { F(0, FormationFamily.Infantry, 60_000), F(1, FormationFamily.Ranged, 10_000, RangedKind.Bow) },
            new SiegeSpec(0, 1, 0, 0, AttackerForce.Of(45_000, 8_000, 6_000, 3_000, RangedKind.Bow, throwers: 100))
            {
                AttackerFood = Food(62_000, 0, 60), AttackerSupplyPerYear = RationYear(62_000, 0), AttackerMissiles = 8_000 * 30,
                CampCapacity = 80_000, AttackerWorksPermille = 900,
                Mineable = false, Relief = new ReliefSpec(38 * D, new long[] { 80_000, 10_000, 8_000, 0 }, RangedKind.Bow),
                AttackerAi = Doctrine with { AssaultMinOddsPermille = 2_000 }, DefenderAi = Doctrine,
            },
            40, 60, new[] { SiegeOutcome.Capitulated }, "about six weeks; relief defeated; surrender")
            { DefenderCapability = Ancient, AttackerCapability = Ancient });

        // Constantinople, 6 Apr - 29 May 1453 (53 days). ~7,000 defenders, ~45,000 civilians, Theodosian walls (inner and outer wall and moat),
        // Ottoman ~70,000 with ~15 heavy bombards (Orban) and trebuchets, Serbian miners countermined by Johannes Grant.
        list.Add(new HistoricalSiege("constantinople", "Constantinople (1453)", 1453, 96, true,
            City(45_000, Food(7_000, 45_000, 150), FortKind.Castle, 100, 200_000, 20_000, guns: 20, crew: 10, water: 52_000 * 120, waterIn: 52_000 * 360,
                 munitions: MunitionsKg(0, 20, 120), missiles: 3_300 * 120),
            new[] { F(0, FormationFamily.Infantry, 3_500), F(1, FormationFamily.Ranged, 3_300, RangedKind.Bow), F(2, FormationFamily.Engineers, 200),
                    F(3, FormationFamily.Artillery, 200) },
            new SiegeSpec(0, 1, 0, 0, AttackerForce.Of(45_000, 15_000, 10_000, 800, RangedKind.Bow, throwers: 10, bombards: 15))
            {
                AttackerFood = Food(73_000, 0, 90), AttackerSupplyPerYear = RationYear(73_000, 0), AttackerMunitions = BombardKg(15, 30),
                AttackerMissiles = 15_000 * 30, CampCapacity = 100_000, AttackerWorksPermille = 300,
                AttackerAi = Doctrine, DefenderAi = Doctrine,
            },
            53, 53, new[] { SiegeOutcome.Stormed }, "53 days; breaches by bombards; final assault")
            { DefenderCapability = EarlyModern, AttackerCapability = EarlyModern });

        // Vienna, 14 Jul - 12 Sep 1683 (60 days). ~16,000 defenders (11,000 regulars + militia), bastioned enceinte, ~300 guns,
        // Ottoman ~90,000 with medium guns only and expert miners; dysentery; relief (Sobieski) on 12 Sep.
        list.Add(new HistoricalSiege("vienna", "Vienna (1683)", 1683, 195, true,
            City(50_000, Food(16_000, 50_000, 80), FortKind.BastionFort, 200, 60_000, 30_000, guns: 300, crew: 5, water: 66_000 * 60, waterIn: 66_000 * 360,
                 munitions: MunitionsKg(8_000, 300, 90)),
            new[] { F(0, FormationFamily.Infantry, 6_000), F(1, FormationFamily.Ranged, 8_000, RangedKind.Firearm), F(2, FormationFamily.Engineers, 500),
                    F(3, FormationFamily.Artillery, 1_500) },
            new SiegeSpec(0, 1, 0, 0, AttackerForce.Of(50_000, 20_000, 20_000, 5_000, RangedKind.Firearm, bombards: 19))
            {
                AttackerFood = Food(95_000, 0, 90), AttackerSupplyPerYear = RationYear(95_000, 0), AttackerMunitions = BombardKg(19, 30) + MunitionsKg(20_000, 0, 30),
                CampCapacity = 60_000, AttackerWorksPermille = 0,
                Relief = new ReliefSpec(59 * D, new long[] { 30_000, 20_000, 25_000, 0 }, RangedKind.Firearm),
                AttackerAi = Doctrine, DefenderAi = Doctrine,
            },
            60, 60, new[] { SiegeOutcome.Relieved }, "60 days; relieved by the Battle of Vienna")
            { DefenderCapability = EarlyModern, AttackerCapability = EarlyModern });

        // Vicksburg, 18 May - 4 Jul 1863 (47 days). ~30,000 Confederates in earthworks, 172 guns, ~4,000 civilians, ~35 days of
        // rations; Grant ~60,000 average (35,000 rising to 77,000); assaults of 19 and 22 May failed; starvation; surrender.
        list.Add(new HistoricalSiege("vicksburg", "Vicksburg (1863)", 1863, 138, true,
            City(4_000, Food(32_000, 4_000, 35), FortKind.None, 800, 25_000, 40_000, guns: 170, crew: 10, water: 34_000 * 60, waterIn: 34_000 * 360,
                 munitions: MunitionsKg(30_000, 170, 120)),
            new[] { F(0, FormationFamily.Ranged, 30_000, RangedKind.Rifle), F(1, FormationFamily.Artillery, 2_000) },
            new SiegeSpec(0, 1, 0, 0, AttackerForce.Of(35_000, 20_000, 3_000, 2_000, RangedKind.Rifle, bombards: 220))
            {
                AttackerFood = Food(60_000, 0, 60), AttackerSupplyPerYear = RationYear(60_000, 0), AttackerMunitions = BombardKg(220, 30) + MunitionsKg(20_000, 0, 30),
                CampCapacity = 80_000, AttackerWorksPermille = 600,
                Mineable = true, AttackerAi = Doctrine, DefenderAi = Doctrine,
            },
            47, 47, new[] { SiegeOutcome.Capitulated }, "47 days; two assaults failed; starvation; surrender")
            { DefenderCapability = EarlyModern, AttackerCapability = Modern });

        // Sevastopol, 17 Oct 1854 - 9 Sep 1855 (327 days from the first bombardment; often quoted as 349 days). Russian garrison
        // ~45,000 behind Totleben's earthworks with naval guns, never invested on the north side (supplies and reinforcements
        // crossed the harbour); Allies ~120,000, cholera in the camps; the French stormed the Malakoff and the Russians evacuated.
        list.Add(new HistoricalSiege("sevastopol", "Sevastopol (1854-55)", 1854, 290, true,
            City(20_000, Food(51_000, 20_000, 60), FortKind.None, 700, 50_000, 60_000, guns: 1_000, crew: 10, water: 85_000 * 60, waterIn: 85_000 * 360,
                 munitions: MunitionsKg(40_000, 1_000, 60)),
            new[] { F(0, FormationFamily.Ranged, 40_000, RangedKind.Firearm), F(1, FormationFamily.Artillery, 10_000), F(2, FormationFamily.Engineers, 1_000) },
            new SiegeSpec(0, 1, 0, 0, AttackerForce.Of(60_000, 40_000, 5_000, 5_000, RangedKind.Rifle, bombards: 600))
            {
                AttackerFood = Food(110_000, 0, 90), AttackerSupplyPerYear = RationYear(110_000, 0), AttackerMunitions = BombardKg(600, 30) + MunitionsKg(40_000, 0, 30),
                CampCapacity = 60_000, AttackerWorksPermille = 700,
                CorridorFoodPerYear = RationYear(65_000, 20_000), CorridorMunitionsPerYear = MunitionsKg(40_000, 1_000, 360), ReinforcementPerYear = 100_000,
                AttackerReinforcementPerYear = 100_000,
                AttackerAi = Doctrine, DefenderAi = Doctrine,
            },
            327, 349, new[] { SiegeOutcome.Stormed }, "327-349 days; storm of the Malakoff; evacuation")
            { HomeTroops = 300_000, DefenderCapability = EarlyModern, AttackerCapability = EarlyModern });

        // ---- HOLDOUT (not used to set any coefficient) --------------------------------------------------------------
        // Masada, AD 73 or 74. ~960 people (a few hundred fighters), stores and cisterns for years; the Romans (~8,000) built a ramp,
        // breached the casemate wall with a ram; the defenders' inner timber wall was burnt; the defenders died. Duration uncertain.
        list.Add(new HistoricalSiege("masada", "Masada (73/74)", 73, 330, false,
            City(600, Food(360, 600, 1800), FortKind.StoneWall, 0, 2_000, 2_000, water: 960 * 1000, waterIn: 100_000, missiles: 110 * 720),
            new[] { F(0, FormationFamily.Infantry, 250), F(1, FormationFamily.Ranged, 110, RangedKind.Bow) },
            new SiegeSpec(0, 1, 0, 0, AttackerForce.Of(5_000, 1_000, 0, 2_000, RangedKind.Bow, rams: 2, throwers: 20))
            {
                AttackerFood = Food(8_000, 0, 90), AttackerSupplyPerYear = RationYear(8_000, 0), AttackerMissiles = 1_000 * 30, CampCapacity = 12_000,
                AttackerWorksPermille = 800,
                Mineable = false, ApproachWorkEngineerYears = 600, AttackerAi = Doctrine, DefenderAi = Doctrine,
            },
            50, 120, new[] { SiegeOutcome.Stormed }, "weeks to a few months; ramp, ram, breach; the defenders died")
            { DefenderCapability = Ancient, AttackerCapability = Ancient });

        // Malta, 18 May - 11 Sep 1565 (116 days). ~6,500 defenders (knights, soldiers, Maltese militia), ~8,000 civilians,
        // Birgu/Senglea and Fort St Angelo; Ottoman ~35,000 with ~60 heavy guns; disease in the camp; the Gran Soccorso (~8,000)
        // landed on 7 Sep and the Ottomans withdrew.
        list.Add(new HistoricalSiege("malta", "Malta (1565)", 1565, 138, false,
            City(8_000, Food(6_500, 8_000, 150), FortKind.Castle, 100, 12_000, 10_000, guns: 80, crew: 6, water: 14_500 * 90, waterIn: 14_500 * 360,
                 munitions: MunitionsKg(3_000, 80, 150)),
            new[] { F(0, FormationFamily.Infantry, 2_800), F(1, FormationFamily.Ranged, 3_000, RangedKind.Firearm), F(2, FormationFamily.Artillery, 500),
                    F(3, FormationFamily.Engineers, 200) },
            new SiegeSpec(0, 1, 0, 0, AttackerForce.Of(20_000, 12_000, 0, 2_000, RangedKind.Firearm, bombards: 60))
            {
                AttackerFood = Food(34_000, 0, 60), AttackerSupplyPerYear = RationYear(30_000, 0), AttackerMunitions = BombardKg(60, 30) + MunitionsKg(12_000, 0, 30),
                CampCapacity = 20_000, AttackerWorksPermille = 200,
                Relief = new ReliefSpec(112 * D, new long[] { 3_000, 5_000, 0, 0 }, RangedKind.Firearm),
                AttackerAi = Doctrine, DefenderAi = Doctrine,
            },
            112, 116, new[] { SiegeOutcome.Relieved, SiegeOutcome.Lifted }, "116 days; relief landed; the Ottomans withdrew")
            { DefenderCapability = EarlyModern, AttackerCapability = EarlyModern });

        // Rhodes, 26 Jun - 22 Dec 1522 (about 145-180 days). ~6,000 defenders behind partly bastioned walls, Tadini's countermines,
        // powder ran short; Ottoman ~100,000 with heavy guns and miners, very heavy losses; surrender on terms, no relief came.
        list.Add(new HistoricalSiege("rhodes", "Rhodes (1522)", 1522, 177, false,
            City(6_000, Food(5_900, 6_000, 240), FortKind.BastionFort, 100, 10_000, 10_000, guns: 60, crew: 5, water: 12_000 * 120, waterIn: 12_000 * 360,
                 munitions: MunitionsKg(3_000, 60, 120)),
            new[] { F(0, FormationFamily.Infantry, 2_500), F(1, FormationFamily.Ranged, 3_000, RangedKind.Firearm), F(2, FormationFamily.Engineers, 100),
                    F(3, FormationFamily.Artillery, 300) },
            new SiegeSpec(0, 1, 0, 0, AttackerForce.Of(60_000, 20_000, 0, 10_000, RangedKind.Firearm, bombards: 100))
            {
                AttackerFood = Food(90_000, 0, 60), AttackerSupplyPerYear = RationYear(90_000, 0), AttackerMunitions = BombardKg(100, 30) + MunitionsKg(20_000, 0, 30),
                CampCapacity = 50_000, AttackerWorksPermille = 300,
                AttackerAi = Doctrine, DefenderAi = Doctrine,
            },
            145, 180, new[] { SiegeOutcome.Capitulated, SiegeOutcome.CapCapitulation }, "about five to six months; surrender on terms")
            { DefenderCapability = EarlyModern, AttackerCapability = EarlyModern });

        // Leningrad, 8 Sep 1941 - 27 Jan 1944 (872 days): the modern-encirclement contrast. ~2.6 million civilians, ~300,000 troops,
        // ~35 days of food at the start, the Ladoga route (≈30% of need on average in the first winter), ~1.3 million evacuated in
        // 1941-42; no assault on the city; lifted by Soviet offensives. The DIR5 three-turn maximum (Modern band, 180-day turns)
        // ends the model's siege at day 540 (adjudication).
        list.Add(new HistoricalSiege("leningrad", "Leningrad (1941-44)", 1941, 251, false,
            City(2_600_000, Food(280_000, 2_600_000, 35), FortKind.None, 700, 3_000_000, 400_000, guns: 2_000, crew: 10, water: 2_900_000L * 365,
                 waterIn: 2_900_000L * 360, munitions: MunitionsKg(250_000, 2_000, 90)),
            new[] { F(0, FormationFamily.Ranged, 250_000, RangedKind.Rifle), F(1, FormationFamily.Engineers, 10_000), F(2, FormationFamily.Artillery, 20_000) },
            new SiegeSpec(0, 1, 0, 0, AttackerForce.Of(0, 280_000, 0, 10_000, RangedKind.Rifle, modernGuns: 400))
            {
                AttackerFood = Food(290_000, 0, 90), AttackerSupplyPerYear = RationYear(290_000, 0), AttackerMunitions = BombardKg(400, 30) + MunitionsKg(280_000, 0, 30),
                CampCapacity = 300_000, AttackerWorksPermille = 700,
                CorridorFoodPerYear = RationYear(280_000, 2_600_000) * 3 / 10, CorridorMunitionsPerYear = MunitionsKg(250_000, 2_000, 360), EvacuationPerYear = 900_000,
                CorridorFromMs = 75 * D, ReinforcementPerYear = 100_000,
                Mineable = false, AttackerAi = Doctrine with { AssaultMinOddsPermille = 5_000 }, DefenderAi = Doctrine,
            },
            872, 872, new[] { SiegeOutcome.CapLifted, SiegeOutcome.Relieved, SiegeOutcome.Lifted }, "872 days; held; lifted (cap at 540 days in the model)")
            { HomeTroops = 1_000_000, DefenderCapability = Modern, AttackerCapability = Modern });

        return list.ToArray();
    }

    public static HistoricalSiege Get(string key)
    {
        foreach (var h in All) if (h.Key == key) return h;
        throw new ArgumentOutOfRangeException(nameof(key));
    }
}
