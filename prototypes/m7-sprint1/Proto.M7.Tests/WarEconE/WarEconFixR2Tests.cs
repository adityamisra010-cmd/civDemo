using System.Numerics;
using System.Reflection;
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

/// <summary>Lane E fix round 2: semantic tests for the reproduced verifier findings E2-1 .. E2-10.</summary>
public class WarEconFixR2Tests
{
    private readonly ITestOutputHelper _out;
    public WarEconFixR2Tests(ITestOutputHelper output) => _out = output;

    private static readonly LogisticsRates R = LogisticsRates.Proposal();
    private static readonly long Y = SimInstant.MsPerYear;
    private const long Micro = 1_000_000;

    private static long[] Days(long d) => new[] { d, d, d, d, d, d, d };

    private static long BandThreeStart(long offsetDays)
    {
        var cal = ShippedCalendar();
        return cal.TurnStart(cal.Bands[3].FirstTurn).Ms + offsetDays * Day;
    }

    /// <summary>A foot army of <paramref name="men"/> halted in a town of polity <paramref name="owner"/> (out of supply).</summary>
    private static WarEconSetup TownSetup(int owner, WarTech tech, long men, LogisticsRates? rates = null, long townMoney = 1_000_000,
        long townGrain = 2_000_000, int armies = 1)
    {
        var foot = new ArmyComposition(men, 0, 0, 0, 0, 0, 0, 0, false);
        var route = new Route(new[] { Seg(100, TerrainClass.Open), Seg(100, TerrainClass.Open, locality: 0), Seg(300, TerrainClass.Open) });
        var arms = new ArmySetup[armies];
        for (int i = 0; i < armies; i++) arms[i] = new ArmySetup(i, 0, 0, 0, foot, route, 150_000, false, 0, 0, 0, Array.Empty<(long, long)>());
        return new WarEconSetup(FoodOrdering.CivilianMilitaryGrowth, rates ?? R, TerrainLogistics.Proposal(),
            new[]
            {
                Polity(0, tech, 2 * men * armies + 10, 2_000_000_000, 0, trade: 1_000),
                Polity(1, tech, 100_000, 2_000_000_000, 0, trade: 1_000),
            },
            new[] { new LocalitySetup(0, owner, 5_000, townGrain, townMoney) },
            arms);
    }

    // ------------------------------------------------------------------------------------------------ E2-1

    /// <summary>Exact daily-compounded share of a head loss, in thousandths of a head, with exact rationals (no fixed point):
    /// heads × (1 − ((360,000 − total) / 360,000)^days) × 1000 × rate / total, floor.</summary>
    private static long CompoundedMilli(long heads, long pm, long totalPm, long days)
    {
        BigInteger num = BigInteger.Pow(360_000 - totalPm, (int)days), den = BigInteger.Pow(360_000, (int)days);
        return (long)((BigInteger)heads * (den - num) * 1000 * pm / (den * totalPm));
    }

    /// <summary>E2-1: desertion and disease compound per day on the declining strength, so a pulse loses what that many
    /// one-day pulses lose. Constant rates (fed, paid, no morale or arrears terms; desertion 3,000 and disease 2,000 ‰/yr),
    /// 5,800 men, 42 days: the first pulse of each cadence loses exactly the closed form (21 days: 3,000/5,000 and
    /// 2,000/5,000 of 5,800 × (1 − (355,000/360,000)^21)), and the strength after 42 days is the same within 2 men under
    /// 21-, 7-, 3- and 1-day pulses. Explicit Euler over the pulse (the former rule) would lose 5,800 × 5,000 × 21 /
    /// 360,000 = 1,691 men in the first 21-day pulse, more than the compounded loss asserted here.</summary>
    [Fact]
    public void HeadLosses_CompoundPerDay_SameWhateverThePulseCut()
    {
        var rates = R with
        {
            DesertionBasePermilleYear = 3_000, DesertionLowMoralePermilleYear = 0, DesertionPerArrearsMonthPermilleYear = 0,
            DiseaseBasePermilleYear = 2_000, DiseaseExposureFactorPermille = 0,
        };
        long start = EarlyModernStart(0, 30), stop = start + 42 * Day;
        var left = new List<long>();
        foreach (long d in new long[] { 21, 7, 3, 1 })
        {
            var setup = EarlyModernSetup(FoodOrdering.CivilianMilitaryGrowth, treasury: 50_000_000, localities: false, march: false, rates: rates, combat: Array.Empty<(long, long)>());
            var rig = Build(setup, new[] { WarOf(0, start, stop + 30 * Day, 0, 1) }, 2, start, pulseDays: Days(d));
            rig.R.RunUntil(stop);
            var rows = rig.W.Rows.Where(r => r.EndMs <= stop).ToList();
            // Premise: constant rates throughout (fed, paid, loyal), whole pulses that end exactly at day 42.
            Assert.Equal((int)(42 / d), rows.Count);
            Assert.All(rows, r =>
            {
                Assert.Equal(0, r.HungerPpm);
                Assert.Equal(0, r.ArrearsEnd);
                Assert.Equal(ArmyCondition.Loyal, r.ConditionEnd);
                Assert.Equal(3_000, r.DesertionPermilleYear);
                Assert.Equal(rows[0].DiseasePermilleYear, r.DiseasePermilleYear);
            });
            long dis = rows[0].DiseasePermilleYear, tot = 3_000 + dis;
            var first = rows[0];
            Assert.Equal(CompoundedMilli(first.MenStart, 3_000, tot, d) / 1000, first.Deserted);
            Assert.Equal(CompoundedMilli(first.MenStart, dis, tot, d) / 1000, first.DiseaseDeaths);
            left.Add(rig.W.MenOf(0));
            _out.WriteLine($"{d,2}-day pulses: disease {dis} ‰/yr, first pulse deserted {first.Deserted} died {first.DiseaseDeaths}, men left at day 42: {rig.W.MenOf(0)}");
        }
        Assert.True(left.Max() - left.Min() <= 2, $"men left differ by cadence: {string.Join(", ", left)}");
    }

    /// <summary>E2-1 (the verifier's probe A): a 100,000-man foot army starving in an enemy town, default rates, 42 days.
    /// The men left now agree within 0.25 % across 21-, 7-, 3- and 1-day pulses (measured 60,202 / 60,107 / 60,095 /
    /// 60,089; before the fix 55,785 / 58,832 / 59,687 / 60,117, a 7.2 % spread). The residual is the rates latched at
    /// pulse starts (hunger is read at each pulse start), not the integration; grain eaten stays 1,999,997.</summary>
    [Fact]
    public void StarvingArmy_MenLeft_NearlyIndependentOfThePulseCut()
    {
        long start = BandThreeStart(10), span = 42 * Day;
        var left = new List<long>();
        foreach (long d in new long[] { 21, 7, 3, 1 })
        {
            var rig = Build(TownSetup(1, WarTech.Coinage, 100_000), new[] { WarOf(0, start, start + span, 0, 1) }, 2, start, pulseDays: Days(d));
            rig.R.RunUntil(start + span);
            Assert.Equal(1_999_997, rig.W.Ledger.Balance(rig.W.GrainEatenAccount));
            Assert.Contains(rig.W.Rows, r => r.HungerPpm > 0);   // premise: it starves
            left.Add(rig.W.MenOf(0));
            _out.WriteLine($"{d,2}-day pulses: men left {rig.W.MenOf(0)}");
        }
        Assert.True((left.Max() - left.Min()) * 1000 <= left.Max() * 25 / 10, $"spread over 0.25 %: {string.Join(", ", left)}");
    }

    // ------------------------------------------------------------------------------------------------ E2-2

    /// <summary>E2-2: the instant cohesion reaches zero is interpolated on the pulse's linear path — exactly
    /// start + w × cohesion-before / −Δcohesion, with Δcohesion recomputed here from the published rates, the pulse's
    /// hunger and the age of its arrears — and the army disbands at the first pulse end at least 7 days after that
    /// instant, under 7-day and 21-day pulses alike (dating it at the pulse start or the pulse end changes both).</summary>
    [Fact]
    public void ZeroCohesion_InstantInterpolated_DisbandAWeekAfterIt()
    {
        long start = EarlyModernStart(0, 30), stop = start + 200 * 7 * Day;
        foreach (long d in new long[] { 7, 21 })
        {
            var setup = EarlyModernSetup(FoodOrdering.CivilianMilitaryGrowth, treasury: 12_000, locMoney: 0, homeLocMoney: 0, shape: RouteShape.Supplied, march: false);
            setup = setup with { Armies = new[] { setup.Armies[0] with { Position = 70_000 } } };
            var rig = Build(setup, new[] { WarOf(0, start, stop, 0, 1) }, 2, start, pulseDays: Days(d));
            long since = -1, prevCoh = -1, zeroAt = -1;
            ArmyPulseRow zeroRow = default;
            for (long t = start + d * Day; t <= stop && zeroAt < 0; t += d * Day)
            {
                long sinceBefore = rig.W.ArrearsSinceOf(0);
                rig.R.RunUntil(t);
                var row = rig.W.Rows.Last();
                if (rig.W.ZeroCohesionSinceOf(0) >= 0) { zeroAt = rig.W.ZeroCohesionSinceOf(0); zeroRow = row; since = sinceBefore >= 0 ? sinceBefore : rig.W.ArrearsSinceOf(0); break; }
                prevCoh = row.CohesionEnd;
            }
            Assert.True(zeroAt >= 0 && prevCoh > 0, "premise: cohesion reaches zero from above inside a pulse");
            long w = zeroRow.EndMs - zeroRow.StartMs;
            long ageMonths = Math.Max(0, zeroRow.StartMs - since) / (30 * Day);
            long dc = (long)((BigInteger)R.CohesionHungerPenaltyPerYear * 1000 * zeroRow.HungerPpm * w / ((BigInteger)Micro * Y))
                      + (long)((BigInteger)R.CohesionArrearsPenaltyPerMonthPerYear * 1000 * ageMonths * w / Y);
            long exp = zeroRow.StartMs + (long)((BigInteger)w * prevCoh / dc);
            _out.WriteLine($"{d}-day pulses: zero-cohesion pulse [{(zeroRow.StartMs - start) / (double)Day:F2}, {(zeroRow.EndMs - start) / (double)Day:F2}) d, instant {(zeroAt - start) / (double)Day:F4} d (closed form {(exp - start) / (double)Day:F4} d)");
            Assert.True(zeroAt > zeroRow.StartMs && zeroAt < zeroRow.EndMs, "premise: the zero instant is strictly inside the pulse");
            Assert.Equal(exp, zeroAt);
            rig.R.RunUntil(stop);
            var disband = rig.W.Events.Single(e => e.Kind == CascadeKind.Disbanded);
            long expectedEnd = rig.W.Rows.Select(r => r.EndMs).Where(e => e >= zeroAt + R.DisbandZeroCohesionDays * Day).Min();
            Assert.Equal(expectedEnd, disband.AtMs);
        }
    }

    // ------------------------------------------------------------------------------------------------ E2-3

    /// <summary>E2-3 (a): morale falls with the DURATION of arrears. An unpaid but fed army (7-day pulses): every pulse end
    /// moves morale by exactly −60 × 1000 × months-in-arrears × w / year (and the hunger term, here zero), clamped at 0;
    /// pulses with 1, 2 and 3 months of arrears are all seen, so the penalty grows month by month.</summary>
    [Fact]
    public void Morale_FallsWithTheMonthsInArrears_ClosedForm()
    {
        long start = EarlyModernStart(0, 30), stop = start + 30 * 7 * Day;
        var setup = EarlyModernSetup(FoodOrdering.CivilianMilitaryGrowth, treasury: 12_000, locMoney: 0, homeLocMoney: 0, shape: RouteShape.Supplied, march: false);
        setup = setup with { Armies = new[] { setup.Armies[0] with { Position = 70_000 } } };
        var rig = Build(setup, new[] { WarOf(0, start, stop + 30 * Day, 0, 1) }, 2, start, pulseDays: Days(7));
        var monthsSeen = new SortedSet<long>();
        long prevMorale = -1;
        for (long t = start + 7 * Day; t <= stop; t += 7 * Day)
        {
            rig.R.RunUntil(t);
            var row = rig.W.Rows.Last();
            long since = rig.W.ArrearsSinceOf(0);
            if (prevMorale >= 0 && row.ArrearsEnd > 0 && since >= 0 && row.ConditionAtStart == ArmyCondition.Loyal)
            {
                Assert.Equal(0, row.HungerPpm);   // premise: fed
                long w = row.EndMs - row.StartMs;
                long months = Math.Max(0, row.StartMs - since) / (30 * Day);
                long dm = -(long)((BigInteger)R.MoraleArrearsPenaltyPerMonthPerYear * 1000 * months * w / Y);
                Assert.Equal(Math.Clamp(prevMorale + dm, 0, Micro), row.MoraleEnd);
                monthsSeen.Add(months);
            }
            prevMorale = row.MoraleEnd;
        }
        _out.WriteLine("months in arrears seen: " + string.Join(",", monthsSeen));
        Assert.Contains(1L, monthsSeen);
        Assert.Contains(2L, monthsSeen);
        Assert.Contains(3L, monthsSeen);
    }

    /// <summary>A mutiny of a fed army at home (threshold raised to 890 so it comes within weeks): the mutineers plunder
    /// their own moneyed town, which clears their arrears; a week of new pay then falls due, at most a month of pay, so the
    /// mutiny is settled at that pulse end.</summary>
    private static EconRig MutinyAtHome(long start, long stop, LogisticsRates rates)
    {
        var route = new Route(new[] { Seg(60, TerrainClass.Open, RoadClass.DirtPath, 0) });
        var setup = new WarEconSetup(FoodOrdering.CivilianMilitaryGrowth, rates, TerrainLogistics.Proposal(),
            new[] { Polity(0, EarlyModern, 300_000, 2_000_000_000, 0, munitions: 200_000, kit: 20_000_000), Polity(1, EarlyModern, 150_000, 0, 0) },
            new[] { new LocalitySetup(0, 0, 6_000, 50_000_000, 50_000_000) },
            new[] { new ArmySetup(0, 0, 0, 0, EarlyModernArmy(), route, 55_000, false, 1_200_000, 0, 5_000, Array.Empty<(long, long)>()) });
        return Build(setup, new[] { WarOf(0, start, stop, 0, 1) }, 2, start, pulseDays: Days(7));
    }

    /// <summary>E2-3 (b): settling a mutiny restarts the arrears clock on what is still owed: right after the MutinyEnded
    /// pulse end the arrears (a week's pay, still owed) are dated from that instant, not from when the chest ran dry.</summary>
    [Fact]
    public void MutinySettled_RestartsTheArrearsClock()
    {
        long start = EarlyModernStart(0, 30), stop = start + 30 * 7 * Day;
        var rig = MutinyAtHome(start, stop, R with { MutinyCohesionThreshold = 890 });
        for (long t = start + 7 * Day; t <= stop; t += 7 * Day)
        {
            rig.R.RunUntil(t);
            var ended = rig.W.Events.Where(e => e.Kind == CascadeKind.MutinyEnded).ToList();
            if (ended.Count == 0) continue;
            Assert.Equal(t, ended[0].AtMs);
            Assert.Contains(rig.W.Events, e => e.Kind == CascadeKind.MutinyBegan && e.AtMs < t);
            var row = rig.W.Rows.Last();
            Assert.True(row.ArrearsEnd > 0, "premise: something is still owed when the mutiny is settled");
            Assert.Equal(0, row.HungerPpm);
            Assert.Equal(t, rig.W.ArrearsSinceOf(0));
            _out.WriteLine($"mutiny settled at day {(t - start) / Day} with {row.ArrearsEnd} still owed; arrears dated from day {(rig.W.ArrearsSinceOf(0) - start) / Day}");
            return;
        }
        Assert.Fail("premise: the mutiny must be settled within the run");
    }

    // ------------------------------------------------------------------------------------------------ E2-4

    /// <summary>E2-4 (a): in kind too, new arrears date from the instant the WAGE chest runs dry. A polity without coinage,
    /// no granary, in an enemy town with 50,000 gm of grain: the wage chest holds only the contributions in kind levied at
    /// the pulse start (after the orderly foraging), so it
    /// runs dry at start + w × chest / due inside the first 7-day pulse.</summary>
    [Fact]
    public void Arrears_InKind_DateFromTheInstantTheWageChestRanDry()
    {
        long start = BandThreeStart(10);
        var setup = TownSetup(1, WarTech.None, 1_000, townGrain: 50_000);
        setup = setup with { Polities = new[] { setup.Polities[0] with { Granary = 0 }, setup.Polities[1] } };
        var rig = Build(setup, new[] { WarOf(0, start, start + 28 * Day, 0, 1) }, 2, start, pulseDays: Days(7));
        rig.R.RunUntil(start + 7 * Day);
        var row = rig.W.Rows.Single();
        long chest = rig.W.Events.Where(e => e.Kind == CascadeKind.ContributionInKind && e.AtMs == row.StartMs).Sum(e => e.Value);
        Assert.True(chest > 0 && chest < row.PayDue, "premise: the chest covers part of the pulse's wages");
        Assert.Equal(chest, row.PayPaid);
        long exp = row.StartMs + (long)((BigInteger)(row.EndMs - row.StartMs) * chest / row.PayDue);
        _out.WriteLine($"wage chest {chest} of {row.PayDue} due: dry at {(exp - start) / (double)Day:F4} d");
        Assert.Equal(exp, rig.W.ArrearsSinceOf(0));
    }

    /// <summary>E2-4 (b): the polity's civilian reserve nets the harvest still to come before the next settlement (§5): at
    /// the first allocation it is exactly ceil(civilians × ration × (next − last settlement) / year) − floor(harvest rate ×
    /// (next − now) / year), with the harvest rate (civilians + mobilised) × harvest − mobilised × lost harvest (harvest
    /// halved here so the reserve stays positive).</summary>
    [Fact]
    public void CivilianReserve_NetsTheHarvestStillToCome()
    {
        long start = EarlyModernStart(0, 30);
        var rates = R with { HarvestPerCapitaYear = R.HarvestPerCapitaYear / 2 };
        var setup = EarlyModernSetup(FoodOrdering.CivilianMilitaryGrowth, localities: false, march: false, rates: rates);
        var rig = Build(setup, new[] { WarOf(0, start, start + 14 * Day, 0, 1) }, 2, start);
        long civ = rig.W.CiviliansOf(0), mob = rig.W.MenOf(0);
        long next = rig.Cal.TurnStart(rig.Cal.TurnOf(new SimInstant(start)) + 1).Ms;
        rig.R.RunUntil(start + Day);
        var first = rig.W.Allocations.First(a => a.Polity == 0);
        Assert.Equal(start, first.AtMs);
        long harvestYear = (civ + mob) * rates.HarvestPerCapitaYear - mob * rates.LostHarvestPerSoldierYear;
        long charge = (long)(((BigInteger)civ * R.RationBase * (next - start) + Y - 1) / Y);
        long toCome = (long)((BigInteger)harvestYear * (next - start) / Y);
        Assert.True(toCome > 0 && toCome < charge, "premise: a harvest is still to come, smaller than the charge");
        _out.WriteLine($"civilians {civ}, charge {charge}, harvest to come {toCome}, reserve {first.Allocation.CivilianReserve}");
        Assert.Equal(Math.Max(0, charge - toCome), first.Allocation.CivilianReserve);
    }

    // ------------------------------------------------------------------------------------------------ E2-5

    /// <summary>E2-5 (the property that holds): two equal armies in one enemy town levy the same TOTAL contributions and
    /// forage the same total under 21-, 7- and 1-day pulses when their caps do not bind; the split between them still
    /// depends on the pulse length (they draw in (Priority, Id) order once per pulse: measured, reported, open).</summary>
    [Fact]
    public void TwoArmiesOneTown_TotalContributionsInvariant()
    {
        long start = BandThreeStart(10), span = 42 * Day;
        var totals = new List<long>();
        foreach (long d in new long[] { 21, 7, 1 })
        {
            var rig = Build(TownSetup(1, WarTech.Coinage, 1_000, armies: 2), new[] { WarOf(0, start, start + span, 0, 1) }, 2, start, pulseDays: Days(d));
            rig.R.RunUntil(start + span);
            long c0 = rig.W.Events.Where(e => e.Kind == CascadeKind.Contribution && e.ArmyId == 0).Sum(e => e.Value);
            long c1 = rig.W.Events.Where(e => e.Kind == CascadeKind.Contribution && e.ArmyId == 1).Sum(e => e.Value);
            totals.Add(c0 + c1);
            _out.WriteLine($"{d,2}-day pulses: contributions {c0} + {c1} = {c0 + c1}");
        }
        Assert.All(totals, t => Assert.Equal(totals[0], t));
        Assert.Equal(717_548, totals[0]);
    }

    // ------------------------------------------------------------------------------------------------ E2-6

    /// <summary>E2-6: when plunder clears a mutinous army's arrears, the clock restarts (ArrearsCleared at that instant;
    /// new arrears are dated from a later instant), exactly as back pay does. Before the fix the clock stayed at day 0 and
    /// no ArrearsCleared was raised.</summary>
    [Fact]
    public void PlunderClearingArrears_RestartsTheClock()
    {
        long start = EarlyModernStart(0, 30), stop = start + 16 * 7 * Day;
        var rates = R with { HarvestPerCapitaYear = 0, LostHarvestPerSoldierYear = 0, MutinyCohesionThreshold = 880 };
        var route = new Route(new[] { Seg(60, TerrainClass.Open, RoadClass.DirtPath, 0) });
        var setup = new WarEconSetup(FoodOrdering.CivilianMilitaryGrowth, rates, TerrainLogistics.Proposal(),
            new[] { Polity(0, EarlyModern, 300_000, 0, 0, munitions: 200_000, kit: 20_000_000), Polity(1, EarlyModern, 150_000, 0, 0) },
            new[] { new LocalitySetup(0, 0, 6_000, 0, 50_000_000) },
            new[] { new ArmySetup(0, 0, 0, 0, EarlyModernArmy(), route, 55_000, false, 1_200_000, 0, 5_000, Array.Empty<(long, long)>()) });
        var rig = Build(setup, new[] { WarOf(0, start, stop, 0, 1) }, 2, start, pulseDays: Days(7));
        long firstBegan = -1;
        for (long t = start + 7 * Day; t <= stop; t += 7 * Day)
        {
            rig.R.RunUntil(t);
            if (firstBegan < 0 && rig.W.ArrearsSinceOf(0) >= 0) firstBegan = rig.W.ArrearsSinceOf(0);
            var plunder = rig.W.Events.FirstOrDefault(e => e.Kind == CascadeKind.Plunder);
            if (plunder.Kind != CascadeKind.Plunder) continue;
            // At the first plunder the arrears were cleared: the event is there and the clock was reset.
            Assert.Contains(rig.W.Events, e => e.Kind == CascadeKind.ArrearsCleared && e.AtMs == plunder.AtMs);
            Assert.Equal(-1, rig.W.ArrearsSinceOf(0));
            Assert.Equal(0, rig.W.ArrearsOf(0));
            rig.R.RunUntil(t + 7 * Day);
            long since = rig.W.ArrearsSinceOf(0);
            Assert.True(since >= plunder.AtMs || since == -1, "new arrears date from after the plunder");
            Assert.True(firstBegan >= 0 && firstBegan < plunder.AtMs, "premise: arrears had begun before the plunder");
            return;
        }
        Assert.Fail("premise: the army must plunder within the run");
    }

    // ------------------------------------------------------------------------------------------------ E2-7

    /// <summary>E2-7: a polity without coinage pays in kind, so it levies contributions in kind (grain into the wage chest)
    /// and no money: the town's money is untouched, the wage chest pays the soldiers, and arrears are lower than with no
    /// contributions at all; every commodity total is conserved exactly. Before the fix it took the town's money into a pay
    /// chest it could not pay from (arrears 38,713 with or without it).</summary>
    [Fact]
    public void NoCoinage_LeviesContributionsInKind_IntoTheWageChest()
    {
        long start = BandThreeStart(10), span = 28 * Day;
        (long arrears, long inKind, long money, bool moneyLevy) Run(long contributionRate)
        {
            var rig = Build(TownSetup(1, WarTech.None, 1_000, R with { ContributionPermilleYear = contributionRate }), new[] { WarOf(0, start, start + span, 0, 1) }, 2, start, pulseDays: Days(7));
            var before = Totals(rig.W.Ledger);
            rig.R.RunUntil(start + span);
            Assert.Equal(before, Totals(rig.W.Ledger));
            return (rig.W.ArrearsOf(0), rig.W.Events.Where(e => e.Kind == CascadeKind.ContributionInKind).Sum(e => e.Value),
                    rig.W.LocalityMoneyOf(0), rig.W.Events.Any(e => e.Kind == CascadeKind.Contribution));
        }
        var with = Run(R.ContributionPermilleYear);
        var without = Run(0);
        _out.WriteLine($"in kind {with.inKind}: arrears {with.arrears} (without contributions {without.arrears}), town money {with.money}");
        Assert.False(with.moneyLevy);
        Assert.Equal(1_000_000, with.money);
        Assert.True(with.inKind > 0);
        Assert.True(with.arrears < without.arrears);
        Assert.Equal(0, without.inKind);
    }

    // ------------------------------------------------------------------------------------------------ E2-9

    /// <summary>E2-9: the state hash covers the locality carries and the in-pulse plan. Two identical runs stopped mid-pulse
    /// hash equal; editing a locality's consumption carry or the army's planned desertion changes the hash, restoring it
    /// restores the hash; and the planned desertion decides the rest of the pulse (men at the pulse end differ).</summary>
    [Fact]
    public void StateHash_SeesLocalityCarriesAndThePulsePlan()
    {
        long start = EarlyModernStart(0, 30), mid = start + 3 * Day, end = start + 7 * Day;
        EconRig Make()
        {
            var rig = Build(EarlyModernSetup(FoodOrdering.CivilianMilitaryGrowth), new[] { WarOf(0, start, start + 28 * Day, 0, 1) }, 2, start, pulseDays: Days(7));
            rig.R.RunUntil(mid);
            return rig;
        }
        var a = Make();
        var c = Make();
        Assert.Equal(a.W.StateHashHex(), c.W.StateHashHex());
        const BindingFlags F = BindingFlags.NonPublic | BindingFlags.Instance;
        var loc = ((Array)typeof(WarEconomyWorld).GetField("_loc", F)!.GetValue(c.W)!).GetValue(0)!;
        var eat = loc.GetType().GetField("EatCarry")!;
        object oldEat = eat.GetValue(loc)!;
        eat.SetValue(loc, ExactFraction.Of(1, 2));
        Assert.NotEqual(a.W.StateHashHex(), c.W.StateHashHex());
        eat.SetValue(loc, oldEat);
        Assert.Equal(a.W.StateHashHex(), c.W.StateHashHex());
        var army = ((Array)typeof(WarEconomyWorld).GetField("_a", F)!.GetValue(c.W)!).GetValue(0)!;
        var des = army.GetType().GetField("DesMilli")!;
        long oldDes = (long)des.GetValue(army)!;
        des.SetValue(army, oldDes + 500_000);
        Assert.NotEqual(a.W.StateHashHex(), c.W.StateHashHex());
        Assert.NotEqual(a.W.ArmySideHashHex(), c.W.ArmySideHashHex());
        a.R.RunUntil(end);
        c.R.RunUntil(end);
        Assert.True(c.W.MenOf(0) < a.W.MenOf(0));
    }

    // ------------------------------------------------------------------------------------------------ E2-10

    /// <summary>E2-10: the supply factor the economy publishes per pulse (ArmyPulseRow.SupplyQ32Raw) is a Q32 probability
    /// in the form the tactical layer's ConditionMath takes: fed into ConditionMath.Attack it lowers a starving army's
    /// attack to the supply floor (1/2) and leaves a fed army's unchanged. NOTE: nothing in the prototype passes it to a
    /// battle yet (no integration seam; reported as a limitation) — this pins the hand-off contract only.</summary>
    [Fact]
    public void SupplyFactor_HandOff_LowersConditionMathAttack()
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
        Assert.Equal(0, hungry.SupplyQ32Raw);
        Assert.Equal(Q32.OneRaw, fed.SupplyQ32Raw);
        Assert.Equal(50, ConditionMath.Attack(100, 0, new Q32(hungry.SupplyQ32Raw)));
        Assert.Equal(100, ConditionMath.Attack(100, 0, new Q32(fed.SupplyQ32Raw)));
    }
}
