using System.Numerics;
using Proto.M7.Fixed;
using Proto.M7.Spatial;
using Proto.M7.Tactical;
using Proto.M7.Time;
using Proto.M7.War;
using Proto.M7.WarEcon;
using Xunit.Abstractions;
using static Proto.M7.Tests.TestSupport;
using static Proto.M7.Tests.WarEconE.WarEconRigs;
using static Proto.M7.Tests.WarEconE.WarEconomyWorldTests;

namespace Proto.M7.Tests.WarEconE;

/// <summary>
/// Lane E fix round 1: one semantic test per reproduced verifier finding (E-V1..E-V8) and per surviving verifier mutant
/// (VM1-VM6, VM8, VM10, VM11). Expected values are independent closed forms (BigInteger or a plain loop in the test).
/// </summary>
public class WarEconFixR1Tests
{
    private readonly ITestOutputHelper _out;
    public WarEconFixR1Tests(ITestOutputHelper output) => _out = output;

    private static readonly LogisticsRates R = LogisticsRates.Proposal();
    private static readonly long Y = SimInstant.MsPerYear;
    private const long Micro = 1_000_000;

    private static long[] Days(long d) => new[] { d, d, d, d, d, d, d };

    /// <summary>One 100,000-man foot army (out of supply: no road, 150 km out, pack loss total) halted in a town of polity
    /// <paramref name="owner"/>; polity 2 is outside the war. The town holds 2,000,000 gm and 1,000,000 money.</summary>
    private static WarEconSetup TownSetup(int owner, long men = 100_000, LogisticsRates? rates = null, long tradeShare = 0)
    {
        var foot = new ArmyComposition(men, 0, 0, 0, 0, 0, 0, 0, false);
        var route = new Route(new[] { Seg(100, TerrainClass.Open), Seg(100, TerrainClass.Open, locality: 0), Seg(300, TerrainClass.Open) });
        return new WarEconSetup(FoodOrdering.CivilianMilitaryGrowth, rates ?? R, TerrainLogistics.Proposal(),
            new[]
            {
                Polity(0, WarTech.Coinage, 2 * men, 2_000_000_000, 100_000_000, trade: 1_000),
                Polity(1, WarTech.Coinage, 100_000, 2_000_000_000, 0, trade: 1_000),
                Polity(2, WarTech.Coinage, 100_000, 2_000_000_000, 0, trade: 1_000),
            },
            new[] { new LocalitySetup(0, owner, 5_000, 2_000_000, 1_000_000) { TradeSharePermille = tradeShare } },
            new[] { new ArmySetup(0, 0, 0, 0, foot, route, 150_000, false, 0, 0, 0, Array.Empty<(long, long)>()) });
    }

    private static long BandThreeStart(long offsetDays)
    {
        var cal = ShippedCalendar();
        return cal.TurnStart(cal.Bands[3].FirstTurn).Ms + offsetDays * Day;
    }

    private static long DailyDraws(long stock, long permilleYear, int days)
    {
        long left = stock;
        for (int d = 0; d < days; d++) left -= Math.Min(left, (long)((BigInteger)left * permilleYear / (1000 * 360)));
        return stock - left;
    }

    // ------------------------------------------------------------------------------------------------ E-V1 (Law 3)

    /// <summary>E-V1: the same halted army in the same enemy town over the same 42 days (a multiple of every cadence, no
    /// settlement inside) takes exactly the same contributions and the same foraged grain whether the pulses are 21, 7, 3 or
    /// 1 days long, and both equal the independent 42-day loop of daily draws. Before the fix the levy was a share per
    /// pulse (1-day pulses levied 947,662 of 1,000,000 over 28 days, 7-day pulses 343,900). Forced requisition is set to 0
    /// so the only grain draw is orderly foraging, and the army (100,000 men, no supply) always wants more than the draw.</summary>
    [Fact]
    public void StockDraws_TakeTheSameOverTheSameDays_WhateverThePulseLength()
    {
        long start = BandThreeStart(10), span = 42 * Day;
        var rates = R with { ForageForcedPermilleYear = 0 };
        var results = new List<(long d, long contrib, long money, long foraged, long grain)>();
        foreach (long d in new long[] { 21, 7, 3, 1 })
        {
            var rig = Build(TownSetup(1, rates: rates), new[] { WarOf(0, start, start + span, 0, 1) }, 3, start, pulseDays: Days(d));
            rig.R.RunUntil(start + span);
            long contrib = rig.W.Events.Where(e => e.Kind == CascadeKind.Contribution).Sum(e => e.Value);
            results.Add((d, contrib, rig.W.LocalityMoneyOf(0), rig.W.Rows.Sum(r => r.Foraged), rig.W.LocalityGrainOf(0)));
            Assert.All(rig.W.Rows, r => Assert.True(r.HungerPpm > 0));   // the draw, not the want, bounds every pulse
            Assert.DoesNotContain(rig.W.Events, e => e.Kind == CascadeKind.ForcedRequisition);
        }
        foreach (var x in results) _out.WriteLine($"{x.d,2}-day pulses: contributions {x.contrib}, money left {x.money}; foraged {x.foraged}, grain left {x.grain}");
        long expContrib = DailyDraws(1_000_000, R.ContributionPermilleYear, 42), expForage = DailyDraws(2_000_000, R.ForageForeignPermilleYear, 42);
        Assert.All(results, x =>
        {
            Assert.Equal(expContrib, x.contrib);
            Assert.Equal(1_000_000 - expContrib, x.money);
            Assert.Equal(expForage, x.foraged);
            Assert.Equal(2_000_000 - expForage, x.grain);
        });
    }

    /// <summary>E-V1 at the unit level: n days drawn at once equal n one-day draws in sequence (for every rate the proposal
    /// uses, many stocks and n up to 21); a part-day draws pro rata; the 7-day calibration is the former per-pulse share
    /// (10 %, 40 %, 90 % within 0.01 %); a draw never exceeds the stock.</summary>
    [Fact]
    public void StockDraw_IsPartitionInvariant_AndCalibratedToTheFormerSevenDayShares()
    {
        foreach (long rate in new[] { R.ContributionPermilleYear, R.ForageForeignPermilleYear, R.ForageForcedPermilleYear })
            foreach (long stock in new long[] { 0, 1, 7, 999, 1_000_000, 123_456_789, 4_000_000_000_000 })
                for (int n = 1; n <= 21; n++)
                {
                    long seq = 0, left = stock;
                    for (int i = 0; i < n; i++) { long t = StockDraw.Share(left, rate, Day); seq += t; left -= t; }
                    Assert.Equal(seq, StockDraw.Share(stock, rate, n * Day));
                    Assert.Equal(DailyDraws(stock, rate, n), seq);
                }
        Assert.Equal((long)((BigInteger)1_000_000 * R.ForageForeignPermilleYear * (Day / 2) / ((BigInteger)1000 * Y)), StockDraw.Share(1_000_000, R.ForageForeignPermilleYear, Day / 2));
        Assert.InRange(StockDraw.Share(1_000_000, R.ContributionPermilleYear, 7 * Day), 99_900, 100_100);
        Assert.InRange(StockDraw.Share(1_000_000, R.ForageForeignPermilleYear, 7 * Day), 399_900, 400_100);
        Assert.InRange(StockDraw.Share(1_000_000, R.ForageForcedPermilleYear, 7 * Day), 899_900, 900_100);
        Assert.Equal(1_000, StockDraw.Share(1_000, 10_000_000, Day));   // a rate above 100 % a day takes all, never more
    }

    /// <summary>E-V1: disbandment waits a DURATION at zero cohesion (7 days), not a number of pulses. The fiscal cascade to
    /// disbandment under 7-day and 1-day pulses: the army disbands at least 7 days after the start of the first pulse that
    /// ended at zero cohesion and at most 7 days plus one pulse after its end; it is mutinous and still above the strength
    /// threshold (it dissolved, it was not destroyed). The former rule (two consecutive pulse ends at zero) disbanded 1-day
    /// pulse armies 2 days after the first zero.</summary>
    [Fact]
    public void Disbandment_WaitsADurationAtZeroCohesion_NotAPulseCount()
    {
        long start = EarlyModernStart(0, 30), stop = start + 200 * 7 * Day;
        foreach (long d in new long[] { 7, 1 })
        {
            var setup = EarlyModernSetup(FoodOrdering.CivilianMilitaryGrowth, treasury: 12_000, locMoney: 0, homeLocMoney: 0, shape: RouteShape.Supplied, march: false);
            setup = setup with { Armies = new[] { setup.Armies[0] with { Position = 70_000 } } };
            var rig = Build(setup, new[] { WarOf(0, start, stop, 0, 1) }, 2, start, pulseDays: Days(d));
            rig.R.RunUntil(stop);
            var disband = rig.W.Events.Single(e => e.Kind == CascadeKind.Disbanded);
            var firstZero = rig.W.Rows.First(r => r.CohesionEnd == 0);
            _out.WriteLine($"{d}-day pulses: first pulse ending at zero cohesion [{(firstZero.StartMs - start) / Day} d, {(firstZero.EndMs - start) / Day} d), disbanded at {(disband.AtMs - start) / (double)Day:F2} d");
            Assert.True(disband.AtMs - firstZero.StartMs >= R.DisbandZeroCohesionDays * Day);
            Assert.True(disband.AtMs - firstZero.EndMs < R.DisbandZeroCohesionDays * Day + d * Day);
            var last = rig.W.Rows.Last();
            Assert.Equal(ArmyCondition.Mutinous, last.ConditionAtStart);
            Assert.True(last.MenEnd * 1000 > EarlyModernArmy().Men * R.DisbandStrengthPermille);
        }
    }

    // ------------------------------------------------------------------------------------------------ E-V2

    /// <summary>E-V2: a LOYAL army halted in its own town (no harvest, empty home granary) takes only the surplus over what
    /// the next settlement will charge the locals — the consumption of the whole interval since their last settlement
    /// (6,000 civilians × 1000 gm × 690 / 360 days = 11,500,000 gm) — so it forages exactly 500,000 gm of the 12,000,000, the
    /// locals eat in full at the settlement (no famine deaths, as with the army outside the town) and the stock is exactly
    /// spent. Before the fix the reserve counted only from "now": the army took 11,816,666 gm and 2,286 locals died.</summary>
    [Fact]
    public void LoyalArmyAtHome_LocalsEatInFullAtTheSettlement()
    {
        var cal = ShippedCalendar();
        long start = cal.TurnStart(cal.Bands[3].FirstTurn).Ms + 30 * Day, turn1 = cal.TurnStart(cal.Bands[3].FirstTurn + 1).Ms;
        var noHarvest = R with { HarvestPerCapitaYear = 0, LostHarvestPerSoldierYear = 0 };
        (long deaths, long foraged, long seized, long before, long after) Run(bool inTown)
        {
            var route = new Route(new[] { Seg(60, TerrainClass.Open, RoadClass.DirtPath, inTown ? 0 : -1) });
            var setup = new WarEconSetup(FoodOrdering.CivilianMilitaryGrowth, noHarvest, TerrainLogistics.Proposal(),
                new[] { Polity(0, EarlyModern, 300_000, 0, 5_000_000, munitions: 200_000, kit: 20_000_000),
                        Polity(1, EarlyModern, 150_000, 500_000_000, 1_000_000, munitions: 50_000, kit: 1_000_000) },
                new[] { new LocalitySetup(0, 0, 6_000, 12_000_000, 0), new LocalitySetup(1, 1, 9_000, 6_000_000, 20_000) },
                new[] { new ArmySetup(0, 0, 0, 0, EarlyModernArmy(), route, 55_000, false, 0, 0, 5_000, new (long, long)[] { (3, 2) }) });
            var rig = Build(setup, new[] { WarOf(0, start, turn1 + 30 * Day, 0, 1) }, 2, start);
            rig.R.RunUntil(turn1 - 1);
            long before = rig.W.LocalityGrainOf(0);
            rig.R.RunUntil(turn1 + Day);
            long deaths = rig.W.Events.Where(e => e.Kind == CascadeKind.LocalityFamineDeaths && e.Polity == 0).Sum(e => e.Value);
            long foraged = rig.W.Rows.Where(r => r.EndMs <= turn1).Sum(r => r.Foraged);
            return (deaths, foraged, rig.W.Rows.Sum(r => r.Seized), before, rig.W.LocalityGrainOf(0));
        }
        long due = (long)((BigInteger)6_000 * 1000 * (turn1 - start) / Y);
        Assert.Equal(11_500_000, due);
        var with = Run(true);
        var without = Run(false);
        _out.WriteLine($"in town: foraged {with.foraged}, grain before settlement {with.before}, after {with.after}, deaths {with.deaths}");
        Assert.Equal(0, without.deaths);
        Assert.Equal(0, with.deaths);
        Assert.Equal(0, with.seized);
        Assert.Equal(12_000_000 - due, with.foraged);
        Assert.Equal(due, with.before);
        Assert.Equal(0, with.after);
    }

    // ------------------------------------------------------------------------------------------------ E-V5

    /// <summary>E-V5: a road cuts the supply wagons' carriage loss over rough ground as it cuts the army's own march cost
    /// (DIR5 §2.11 "roads reduce costs"): wagons over 100 km of Mountain lose exactly 100 × cost / (600 × 1000) with the
    /// road-adjusted cost 3400 (DirtPath), 2125 (Track), 1225 (Built): 17/30 > 17/48 > 49/240. Before the fix all three
    /// were 2/3 (the raw terrain cost 4000).</summary>
    [Fact]
    public void SupplyLine_RoadCutsWagonLoss_OverRoughGround()
    {
        var t = TerrainLogistics.Proposal();
        var c = AncientHost();
        var cap = new PolityCapability(WarTech.None);
        ExactFraction L(RoadClass road) => ArmyLogistics.Evaluate(new Route(new[] { Seg(200, TerrainClass.Mountain, road) }), 100_000, 7 * Day, c, cap, R, t).Loss;
        Assert.Equal(CarriageMode.Wagon, ArmyLogistics.ModeOf(Seg(200, TerrainClass.Mountain, RoadClass.DirtPath), cap, c));
        Assert.Equal(ExactFraction.Of(17, 30), L(RoadClass.DirtPath));
        Assert.Equal(ExactFraction.Of(17, 48), L(RoadClass.Track));
        Assert.Equal(ExactFraction.Of(49, 240), L(RoadClass.Built));
        foreach (var road in new[] { RoadClass.DirtPath, RoadClass.Track, RoadClass.Built })
            Assert.Equal(ExactFraction.Of(100 * ArmyLogistics.SegmentCost(Seg(200, TerrainClass.Mountain, road), c, t), 600 * 1000), L(road));
    }

    // ------------------------------------------------------------------------------------------------ E-V6

    /// <summary>E-V6: a town owned by a polity outside the war (2) is neutral ground: no contributions, no foraging, no
    /// forced requisition even though the army goes hungry, no occupation, and the owner keeps its whole trade income
    /// (1,000 × 1000 scaled). The same town owned by the enemy (1): contributions, foraging, an Occupation event and the
    /// enemy's trade cut by the town's 500 ‰ share.</summary>
    [Fact]
    public void NeutralTown_IsNotEnemyGround()
    {
        long start = BandThreeStart(10), span = 28 * Day;
        var neutral = Build(TownSetup(2, men: 1_000, tradeShare: 500), new[] { WarOf(0, start, start + span, 0, 1) }, 3, start);
        neutral.R.RunUntil(start + span);
        Assert.DoesNotContain(neutral.W.Events, e => e.Kind is CascadeKind.Contribution or CascadeKind.ForcedRequisition or CascadeKind.Occupation or CascadeKind.LocalFamine);
        Assert.All(neutral.W.Rows, r => { Assert.Equal(0, r.Foraged); Assert.Equal(0, r.Seized); });
        Assert.Contains(neutral.W.Rows, r => r.HungerPpm > 0);
        Assert.Equal(-1, neutral.W.OccupiedLocalityOf(0));
        Assert.Equal(1_000 * 1000, neutral.W.RevenueRateScaledOf(2));
        Assert.Equal(1_000_000, neutral.W.LocalityMoneyOf(0));
        Assert.Equal(2_000_000, neutral.W.LocalityGrainOf(0));

        var enemy = Build(TownSetup(1, men: 1_000, tradeShare: 500), new[] { WarOf(0, start, start + span, 0, 1) }, 3, start);
        enemy.R.RunUntil(start + span);
        Assert.Contains(enemy.W.Events, e => e.Kind == CascadeKind.Contribution);
        Assert.Contains(enemy.W.Events, e => e.Kind == CascadeKind.Occupation && e.Polity == 1);
        Assert.True(enemy.W.Rows.Sum(r => r.Foraged) > 0);
        Assert.Equal(0, enemy.W.OccupiedLocalityOf(0));
        Assert.Equal(1_000 * 500, enemy.W.RevenueRateScaledOf(1));
    }

    /// <summary>VM8 (verifier mutant: an army in its OWN town counted as occupying it): its own town is never occupied; no
    /// Occupation event, and its polity's trade income is intact.</summary>
    [Fact]
    public void OwnTown_IsNeverOccupied_OwnTradeIntact()
    {
        long start = BandThreeStart(10), span = 28 * Day;
        var rig = Build(TownSetup(0, men: 1_000, tradeShare: 500), new[] { WarOf(0, start, start + span, 0, 1) }, 3, start);
        rig.R.RunUntil(start + span);
        Assert.DoesNotContain(rig.W.Events, e => e.Kind is CascadeKind.Occupation or CascadeKind.Contribution);
        Assert.Equal(-1, rig.W.OccupiedLocalityOf(0));
        Assert.Equal(1_000 * 1000, rig.W.RevenueRateScaledOf(0));
    }

    // ------------------------------------------------------------------------------------------------ E-V7

    /// <summary>E-V7: engine-driven passage speed comes from lane N's class: MaxSpeedMmPerS × 86.4 × 550 / 1000 m/day, so a
    /// turret warship (15,400 mm/s) makes 731,808 m/day and a steam frigate (5,700 mm/s) 270,864; oared and sailing hulls
    /// keep the propulsion's passage table.</summary>
    [Fact]
    public void SteamPassageSpeed_IsTheLaneNClassSpeed()
    {
        long S(VesselClass v) => NavalLogistics.PassageSpeed(v, R);
        Assert.Equal(731_808, S(DomainProfiles.TurretWarship));
        Assert.Equal(270_864, S(DomainProfiles.SteamFrigate));
        Assert.Equal(365_904, S(DomainProfiles.SteamTransport));
        foreach (var v in new[] { DomainProfiles.TurretWarship, DomainProfiles.SteamFrigate, DomainProfiles.SteamTransport, DomainProfiles.EngineBoardingShip })
            Assert.Equal((long)((BigInteger)v.MaxSpeedMmPerS * 86_400 / 1000 * R.PassageEngineSustainedPermille / 1000), S(v));
        Assert.Equal(R.PassageOarMPerDay, S(DomainProfiles.Galley));
        Assert.Equal(R.PassageSailMPerDay, S(DomainProfiles.ShipOfTheLine));
    }

    // ------------------------------------------------------------------------------------------------ E-V8, VM11

    /// <summary>E-V8 and VM11: drill munitions and equipment wear by time are carried across pulses, so with no losses (all
    /// attrition rates 0, a halted army, no combat) 28 days cost exactly floor(5000 firearm men × 5 kg × 28 / 360) = 1,944 kg
    /// of powder and floor(5,800 × 250 × 28 / 360) = 112,777 milli-kits whether the pulses are 7 days or 1 day. Before the
    /// fix each pulse rounded up (1-day pulses 1,960 kg).</summary>
    [Fact]
    public void DrillMunitionsAndWear_AreCarried_AndIndependentOfPulseLength()
    {
        long start = BandThreeStart(10), span = 28 * Day;
        var still = R with
        {
            DesertionBasePermilleYear = 0, DesertionLowMoralePermilleYear = 0, DiseaseBasePermilleYear = 0, AnimalDeathBasePermilleYear = 0,
        };
        (long mun, long kit) Run(long d)
        {
            var setup = EarlyModernSetup(FoodOrdering.CivilianMilitaryGrowth, localities: false, rates: still, march: false, combat: Array.Empty<(long, long)>());
            setup = setup with { Armies = new[] { setup.Armies[0] with { TrainGrain = 50_000_000, Munitions = 0 } } };
            var rig = Build(setup, new[] { WarOf(0, start, start + span, 0, 1) }, 2, start, pulseDays: Days(d));
            rig.R.RunUntil(start + span);
            Assert.All(rig.W.Rows, r => Assert.Equal(EarlyModernArmy().Men, r.MenEnd));
            return (rig.W.Rows.Sum(r => r.MunitionsNeed), rig.W.Rows.Sum(r => r.KitReplaced));
        }
        long expMun = (long)((BigInteger)5_000 * R.MunitionsPerFirearmYearDrill * 28 / 360);
        long expKit = (long)((BigInteger)EarlyModernArmy().Men * R.KitWearPerManYear * 28 / 360);
        Assert.Equal(1_944, expMun);
        foreach (long d in new long[] { 7, 1 })
        {
            var x = Run(d);
            Assert.Equal(expMun, x.mun);
            Assert.Equal(expKit, x.kit);
        }
    }

    /// <summary>E-V8: new arrears date from the instant the pay chest runs dry, not from the start of the pulse. An army paid
    /// 81,000 soldier-months a year (5,000 foot × 12 + 600 horse × 30 + 200 gunners × 15) with 787 in its chest and an empty
    /// treasury runs dry 787/81,000 of a year (3.498 days) after the start; under 7-day and 1-day pulses the recorded start
    /// of the arrears is within one hour of that instant (exactly the planned pro-rata share of the pulse in which it
    /// happens). Before the fix it was the pulse start (0 days or 3 days).</summary>
    [Fact]
    public void Arrears_DateFromTheInstantTheChestRanDry()
    {
        long start = BandThreeStart(10);
        long trueDry = start + (long)((BigInteger)787 * Y / 81_000);
        foreach (long d in new long[] { 7, 1 })
        {
            var setup = EarlyModernSetup(FoodOrdering.CivilianMilitaryGrowth, treasury: 0, localities: false, march: false);
            setup = setup with { Armies = new[] { setup.Armies[0] with { PayChest = 787 } } };
            var rig = Build(setup, new[] { WarOf(0, start, start + 14 * Day, 0, 1) }, 2, start, pulseDays: Days(d));
            rig.R.RunUntil(start + 7 * Day);
            long since = rig.W.ArrearsSinceOf(0);
            _out.WriteLine($"{d}-day pulses: arrears since {(since - start) / (double)Day:F4} d (continuous {(trueDry - start) / (double)Day:F4} d)");
            Assert.InRange(since, trueDry - 3_600_000, trueDry + 3_600_000);
            var began = rig.W.Events.Single(e => e.Kind == CascadeKind.ArrearsBegan);
            var row = rig.W.Rows.Single(r => r.EndMs == began.AtMs);
            // Exactly the planned share: the chest at that pulse's start (787 less what earlier pulses paid) over its pay due.
            long chestAtStart = 787 - rig.W.Rows.Where(r => r.EndMs <= row.StartMs).Sum(r => r.PayPaid);
            Assert.Equal(row.StartMs + (long)((BigInteger)(row.EndMs - row.StartMs) * chestAtStart / row.PayDue), since);
        }
    }

    // ------------------------------------------------------------------------------------------------ E-V3: VM1, VM3, VM4

    /// <summary>VM1 (refusal to march dropped): an unpaid army marching up a 20,000 km river route (it never arrives) moves
    /// while loyal and stops dead from the pulse it starts mutinous: no distance, no movement food, its position frozen.</summary>
    [Fact]
    public void MutinousArmy_RefusesToMarch()
    {
        long start = EarlyModernStart(0, 30), stop = start + 140 * 7 * Day;
        var setup = EarlyModernSetup(FoodOrdering.CivilianMilitaryGrowth, treasury: 12_000, localities: false);
        var river = new Route(new[] { Seg(20_000, TerrainClass.Open, RoadClass.Track, water: true) });
        setup = setup with { Armies = new[] { setup.Armies[0] with { Route = river, Position = 0 } } };
        var rig = Build(setup, new[] { WarOf(0, start, stop, 0, 1) }, 2, start);
        rig.R.RunUntil(stop);
        var rows = rig.W.Rows.Where(r => r.ArmyId == 0).ToList();
        var mutinous = rows.Where(r => r.ConditionAtStart == ArmyCondition.Mutinous).ToList();
        Assert.True(mutinous.Count >= 2, "the run must reach a mutiny that lasts");
        Assert.Contains(rows, r => r.ConditionAtStart == ArmyCondition.Loyal && r.DistanceM > 0 && r.ArrearsEnd > 0);
        long frozen = rows.Last(r => r.ConditionAtStart == ArmyCondition.Loyal).PositionEnd;
        Assert.True(frozen < river.Length);
        Assert.All(mutinous, r =>
        {
            Assert.Equal(0, r.DistanceM);
            Assert.Equal(0, r.MovementNeed);
            Assert.Equal(frozen, r.PositionEnd);
        });
    }

    /// <summary>VM3 (arrears-duration term dropped): with the low-morale term switched off and food ample, a loyal army's
    /// desertion rate in a pulse is exactly base + min(cap, 30 ‰/yr × whole months since its arrears began) — 0 extra before
    /// the arrears, rising month by month after (purely fiscal cascade; arrears never cleared before the mutiny).</summary>
    [Fact]
    public void Desertion_RisesWithTheMonthsInArrears_ClosedForm()
    {
        long start = EarlyModernStart(0, 30), stop = start + 140 * 7 * Day;
        var rates = R with { DesertionLowMoralePermilleYear = 0 };
        EconRig Rig()
        {
            var setup = EarlyModernSetup(FoodOrdering.CivilianMilitaryGrowth, treasury: 12_000, locMoney: 3_000, shape: RouteShape.Supplied, march: false, rates: rates);
            setup = setup with { Armies = new[] { setup.Armies[0] with { Position = 70_000 } } };
            return Build(setup, new[] { WarOf(0, start, stop, 0, 1) }, 2, start);
        }
        var full = Rig();
        full.R.RunUntil(stop);
        long mutiny = full.W.Events.First(e => e.Kind == CascadeKind.MutinyBegan).AtMs;
        Assert.DoesNotContain(full.W.Events, e => e.Kind == CascadeKind.ArrearsCleared && e.AtMs <= mutiny);
        var probe = Rig();
        probe.R.RunUntil(mutiny);
        long since = probe.W.ArrearsSinceOf(0);
        Assert.True(since > start);
        var loyal = full.W.Rows.Where(r => r.ConditionAtStart == ArmyCondition.Loyal && r.EndMs <= mutiny).ToList();
        long maxExtra = 0;
        foreach (var r in loyal)
        {
            Assert.Equal(0, r.HungerPpm);
            long months = r.StartMs > since ? (r.StartMs - since) / (30 * Day) : 0;
            long extra = Math.Min(R.DesertionArrearsCapPermilleYear, months * R.DesertionPerArrearsMonthPermilleYear);
            Assert.Equal(R.DesertionBasePermilleYear + extra, r.DesertionPermilleYear);
            maxExtra = Math.Max(maxExtra, extra);
        }
        Assert.True(maxExtra >= 3 * R.DesertionPerArrearsMonthPermilleYear, "the loyal phase must last several months in arrears");
    }

    /// <summary>The ancient deep raid (beyond pack range; it goes hungry) with an ample equipment stock, so no kit deficit:
    /// no gunpowder, so munitions never bind.</summary>
    private static EconRig DeepRaid(long kit, out long start)
    {
        var cal = ShippedCalendar();
        start = cal.TurnStart(cal.Bands[1].FirstTurn + 5).Ms + 40 * Day;
        var route = new Route(new[]
        {
            Seg(80, TerrainClass.Open, RoadClass.DirtPath, 0),
            Seg(150, TerrainClass.Hills, RoadClass.None, 1),
            Seg(250, TerrainClass.Mountain, RoadClass.None, 2),
        });
        var setup = new WarEconSetup(FoodOrdering.CivilianMilitaryGrowth, R, TerrainLogistics.Proposal(),
            new[] { Polity(0, WarTech.None, 120_000, 400_000_000, kit: kit), Polity(1, WarTech.None, 60_000, 100_000_000) },
            new[] { new LocalitySetup(0, 0, 3_000, 2_000_000, 0), new LocalitySetup(1, 1, 2_500, 2_500_000, 0), new LocalitySetup(2, 1, 600, 300_000, 0) },
            new[] { new ArmySetup(0, 0, 0, 0, AncientHost(), route, 70_000, true, 400_000, 0, 0, Array.Empty<(long, long)>()) });
        var rig = Build(setup, new[] { WarOf(0, start, start + 60 * 14 * Day, 0, 1) }, 2, start);
        rig.R.RunUntil(start + 61 * 14 * Day);
        return rig;
    }

    /// <summary>VM4 (supply factor ignores hunger): the battle layer's supply factor is exactly the worst of fed, munitioned
    /// and equipped (Q32 of min(1 − hunger, munitions share, 1 − kit deficit share)) in every pulse; with an ample kit stock
    /// a hungry army's factor is exactly its fed share, below one.</summary>
    [Fact]
    public void SupplyFactor_IsTheWorstOfFedMunitionedEquipped_ClosedForm()
    {
        foreach (long kit in new long[] { 0, 1_000_000_000 })
        {
            var rig = DeepRaid(kit, out _);
            foreach (var r in rig.W.Rows)
            {
                long fed = Micro - r.HungerPpm;
                long equip = r.MenStart == 0 ? Micro : Micro - (long)((BigInteger)Math.Min(r.KitDeficit, r.MenStart * 1000) * Micro / (r.MenStart * 1000));
                Assert.Equal(Q32.FromRatio(Math.Min(fed, equip), Micro).Raw, r.SupplyQ32Raw);
            }
            if (kit > 0)
                Assert.Contains(rig.W.Rows, r => r.HungerPpm > 0 && r.KitDeficit == 0 && r.SupplyQ32Raw == Q32.FromRatio(Micro - r.HungerPpm, Micro).Raw && r.SupplyQ32Raw < Q32.OneRaw);
        }
    }

    // ------------------------------------------------------------------------------------------------ E-V4: VM2, VM5, VM6, VM10

    /// <summary>VM2 (pay keeps being charged for men lost): pay due over the whole run is exactly floor(Σ men present at each
    /// pulse start × 81,000 × W / (Y × 5,800)) — pay falls with desertion and deaths (persistent exact carry).</summary>
    [Fact]
    public void PayDue_FollowsTheMenPresent_ClosedForm()
    {
        long start = EarlyModernStart(0, 30), stop = start + 60 * 7 * Day;
        var rig = Build(EarlyModernSetup(FoodOrdering.CivilianMilitaryGrowth, treasury: 20_000, localities: false), new[] { WarOf(0, start, stop, 0, 1) }, 2, start);
        rig.R.RunUntil(stop);
        var rows = rig.W.Rows.Where(r => r.ArmyId == 0).ToList();
        Assert.Equal(60, rows.Count);
        Assert.True(rows[^1].MenStart < rows[0].MenStart);
        long m0 = EarlyModernArmy().Men, annual = 5_000 * R.PayFoot + 600 * R.PayMounted + 200 * R.PayGunCrew;
        Assert.Equal(81_000, annual);
        BigInteger num = 0;
        foreach (var r in rows) num += (BigInteger)r.MenStart * annual * (r.EndMs - r.StartMs);
        Assert.Equal((long)(num / ((BigInteger)Y * m0)), rows.Sum(r => r.PayDue));
    }

    /// <summary>VM5 (equipment deficit does not raise disease): the latched disease rate of every pulse of the deep raid
    /// (no equipment stock, hunger at times) is exactly base × (1000 + 4000 × hunger‰ + 1000 × kit-deficit‰) / 1000 +
    /// 3000 × hunger‰ (‰ per year), and the equipment term matters in some fed pulse.</summary>
    [Fact]
    public void DiseaseRate_IsTheClosedFormOfHungerAndEquipment()
    {
        var rig = DeepRaid(0, out _);
        bool exposureMatters = false;
        foreach (var r in rig.W.Rows)
        {
            long h = r.HungerPpm / 1000, k = r.MenStart == 0 ? 0 : Math.Min(1000, r.KitDeficit / r.MenStart);
            long exp = R.DiseaseBasePermilleYear * (1000 + R.DiseaseHungerFactorPermille * h / 1000 + R.DiseaseExposureFactorPermille * k / 1000) / 1000
                       + R.StarvationDeathPermilleYear * h / 1000;
            Assert.Equal(exp, r.DiseasePermilleYear);
            long without = R.DiseaseBasePermilleYear * (1000 + R.DiseaseHungerFactorPermille * h / 1000) / 1000 + R.StarvationDeathPermilleYear * h / 1000;
            if (h == 0 && exp > without) exposureMatters = true;
        }
        Assert.True(exposureMatters, "some fed pulse must carry an equipment deficit large enough to raise the rate");
    }

    /// <summary>VM6 (fodder shortfall does not raise animal deaths): one unsplit 7-day pulse, the early-modern army halted
    /// with nothing to eat (empty train and granary, no locality): its 700 horses and 600 draft animals die at
    /// (50 + 1500) ‰/yr compounded per day (fix round 2, E2-1), exactly floor(700 × (1 − (358,450 / 360,000)^7)) +
    /// floor(600 × (1 − (358,450 / 360,000)^7)) = 20 + 17; fed, at 50 ‰/yr, none die in the week.</summary>
    [Fact]
    public void FodderShortfall_RaisesAnimalDeaths_ClosedForm()
    {
        long start = EarlyModernStart(0, 30), stop = start + 7 * Day;
        ArmyPulseRow One(long train, long granary)
        {
            var setup = EarlyModernSetup(FoodOrdering.CivilianMilitaryGrowth, granary: granary, localities: false, march: false);
            setup = setup with { Armies = new[] { setup.Armies[0] with { TrainGrain = train } } };
            var rig = Build(setup, new[] { WarOf(0, start, stop, 0, 1) }, 2, start);
            rig.R.RunUntil(stop);
            return rig.W.Rows.Single();
        }
        var hungry = One(0, 0);
        var fed = One(50_000_000, 2_000_000_000);
        Assert.Equal(Micro, hungry.HungerPpm);
        Assert.Equal(0, fed.HungerPpm);
        // Moved pin (fix round 2, E2-1): the deaths now compound per day on the declining herd, heads × (1 − (1 − rate /
        // 360,000)^7); the explicit-Euler form heads × rate × 7 / 360,000 gave 21 + 18 = 39.
        static long Compounded(long heads, long pm, int days)
        {
            BigInteger num = BigInteger.Pow(360_000 - pm, days), den = BigInteger.Pow(360_000, days);
            return (long)(heads * (den - num) / den);
        }
        long pm = R.AnimalDeathBasePermilleYear + R.AnimalDeathStarvePermilleYear;
        long exp = Compounded(700, pm, 7) + Compounded(600, pm, 7);
        Assert.Equal(37, exp);
        Assert.Equal(exp, hungry.AnimalDeaths);
        Assert.Equal(Compounded(700, R.AnimalDeathBasePermilleYear, 7) + Compounded(600, R.AnimalDeathBasePermilleYear, 7), fed.AnimalDeaths);
        Assert.True(hungry.AnimalDeaths > fed.AnimalDeaths);
    }

    /// <summary>VM10 (mutiny ends while the army starves): an army paid in full (no arrears ever) but with nothing to eat
    /// (no harvest, empty granary, no locality) loses cohesion to hunger and mutinies; while it goes hungry the mutiny never
    /// ends, whatever its pay. Attrition from hunger is switched off so the army survives to show it.</summary>
    [Fact]
    public void Mutiny_PersistsWhileTheArmyStarves_EvenWhenPaid()
    {
        long start = EarlyModernStart(0, 30), stop = start + 60 * 7 * Day;
        var rates = R with
        {
            StarvationDeathPermilleYear = 0, DesertionHungerPermilleYear = 0, DiseaseHungerFactorPermille = 0, DesertionMutinyPermilleYear = 0,
            DesertionBasePermilleYear = 0, DesertionLowMoralePermilleYear = 0, DiseaseBasePermilleYear = 0,
            HarvestPerCapitaYear = 0, LostHarvestPerSoldierYear = 0,   // no harvest: the granary stays empty
        };
        var setup = EarlyModernSetup(FoodOrdering.CivilianMilitaryGrowth, granary: 0, localities: false, march: false, rates: rates);
        setup = setup with { Armies = new[] { setup.Armies[0] with { TrainGrain = 0 } } };
        var rig = Build(setup, new[] { WarOf(0, start, stop, 0, 1) }, 2, start);
        rig.R.RunUntil(stop);
        var began = rig.W.Events.First(e => e.Kind == CascadeKind.MutinyBegan);
        Assert.DoesNotContain(rig.W.Events, e => e.Kind == CascadeKind.MutinyEnded);
        var after = rig.W.Rows.Where(r => r.StartMs >= began.AtMs).ToList();
        Assert.True(after.Count >= 2);
        Assert.All(rig.W.Rows, r => { Assert.Equal(0, r.ArrearsEnd); Assert.True(r.HungerPpm > 0); });
        Assert.All(after, r => Assert.Equal(ArmyCondition.Mutinous, r.ConditionAtStart));
    }
}
