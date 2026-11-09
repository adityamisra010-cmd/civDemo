using System.Numerics;
using System.Security.Cryptography;
using Proto.M7.Events;
using Proto.M7.Spatial;
using Proto.M7.Support;
using Proto.M7.Tactical;
using Proto.M7.Time;
using Proto.M7.War;
using Proto.M7.War.Outcomes;
using Proto.M7.WarEcon;
using Proto.M7.Tests.Tactical;
using Proto.M7.Tests.WarEconE;
using static Proto.M7.Tests.TestSupport;
using static Proto.M7.Tests.WarEconE.WarEconRigs;

namespace Proto.M7.Tests.LaneD;

internal sealed record SupRig(EventScheduler S, WarTheaterWorld Inner, SupportWorld W, Runner R);

/// <summary>Rigs for the lane D support tests (lane E's calendar, pulse table and setups; every number is a TEST INPUT).</summary>
internal static class SupportRig
{
    public static readonly SignificantKinds Sig = new(WarKinds.Probe);

    public static SupRig Build(WarEconSetup setup, WarSpec[] wars, int polities, long start, SupportPool[] pools, SupportRates? rates = null,
                               long[]? probes = null, SubTurnOrderLog? replay = null, long? beginMs = null)
    {
        var cal = ShippedCalendar();
        var table = new long[cal.Bands.Count];
        for (int i = 0; i < table.Length; i++) table[i] = ProposedPulseDays[i] * Day;
        var s = new EventScheduler(beginMs ?? start - Day);   // a day before the war: room for an allocation window before it
        var inner = new WarTheaterWorld(cal, new WarPulseSchedule(cal, table), polities, wars, WarFog.None, SignificantKinds.None, new long[polities], 0, 0);
        var w = new SupportWorld(inner, cal, setup, pools, rates ?? SupportRates.Proposal());
        w.Begin(s);
        foreach (long p in probes ?? Array.Empty<long>()) WarTheaterWorld.ScheduleProbe(s, p, 0);
        var r = replay is null ? new Runner(s, w, Sig, stopOnSignificant: true) : Runner.CreateReplay(s, w, Sig, replay);
        return new SupRig(s, inner, w, r);
    }

    /// <summary>Run to the limit, issuing the given orders at each window.</summary>
    public static void Drive(SupRig rig, long limit, Func<long, (int Kind, long A, long B)[]> ordersAt)
    {
        while (true)
        {
            var res = rig.R.RunUntil(limit);
            if (res.Status == RunStatus.ReachedLimit) return;
            foreach (var o in ordersAt(res.AtMs)) rig.R.IssueOrder(o.Kind, o.A, o.B);
        }
    }

    public static (int, long, long) Allocate(int polity, int army, SupportKind kind, long target)
        => (SupportKinds.OrderAllocate, SupportKinds.Pack(polity, army), SupportKinds.PackTarget(kind, target));

    public static readonly Commodity[] Conserved =
        { Commodity.Money, Commodity.People, Commodity.Grain, Commodity.Animals, Commodity.Munitions, Commodity.Kit, Commodity.Vehicles };

    public static Int128[] Totals(WarLedger l)
    {
        var t = new Int128[Conserved.Length];
        for (int i = 0; i < t.Length; i++) t[i] = l.Total(Conserved[i]);
        return t;
    }

    /// <summary>A foot-only army (no wagons, no animals) of polity 0, halted or marching on a 400 km track, no locality to
    /// forage from: its food comes only from its own train and the supply line its carriers can run.</summary>
    public static WarEconSetup FootArmy(bool march, long position = 60_000, long men = 2_000, long trainGrain = 0)
    {
        var route = new Route(new[] { Seg(400, TerrainClass.Open, RoadClass.Track, -1) });
        return new WarEconSetup(FoodOrdering.CivilianMilitaryGrowth, LogisticsRates.Proposal(), TerrainLogistics.Proposal(),
            new[] { Polity(0, WarTech.Coinage, 300_000, 2_000_000_000, treasury: 50_000_000), Polity(1, WarTech.Coinage, 100_000, 500_000_000) },
            Array.Empty<LocalitySetup>(),
            new[] { new ArmySetup(0, 0, 0, 0, new ArmyComposition(men, 0, 0, 0, 0, 0, 0, 0, false), route, position, march, trainGrain, 0, 0, Array.Empty<(long, long)>()) });
    }

    public static long WarStart() => WarEconomyWorldTests.EarlyModernStart(1, 0);   // band 3: 7-day proposed pulses

    /// <summary>A collapse input: 50 battle entities (fit 30, wounded 5, routed 3, dead 10, surrendered 2), end morale 600/1000.</summary>
    public static CollapseInput Collapse50(long moraleEnd = 600) => new(0, 0, new long[] { 30, 5, 3, 10, 2 }, moraleEnd, 1000);
}

/// <summary>
/// Lane D item 2: panel-managed support capabilities (Proto.M7/Support) over lane E's war economy. All coefficients are the
/// labelled PROPOSAL table (SupportRates.Proposal); pools, armies and battles are TEST INPUTS.
/// </summary>
public class SupportCapabilityTests
{
    private static readonly long Week = 7 * Day;

    // ------------------------------------------------------------------ the hook is default-off

    /// <summary>A support world with empty pools and no order runs lane E's economy byte for byte: the same pulse rows, the same
    /// cascade events and the same balance in every account lane E opened.</summary>
    [Fact]
    public void WithoutSupport_LaneEsEconomyIsByteIdentical()
    {
        long s1 = SupportRig.WarStart(), start = s1 - 3 * Day, stop = start + 20 * Week;
        var setup = WarEconomyWorldTests.EarlyModernSetup(FoodOrdering.CivilianMilitaryGrowth);
        var wars = new[] { WarOf(0, start, stop, 0, 1) };
        var plain = WarEconRigs.Build(setup, wars, 2, start);
        plain.R.RunUntil(stop + 30 * Day);
        var sup = SupportRig.Build(setup, wars, 2, start, Array.Empty<SupportPool>(), beginMs: start);   // lane E's rig begins at the war start
        SupportRig.Drive(sup, stop + 30 * Day, _ => Array.Empty<(int, long, long)>());
        Assert.Equal(plain.W.Rows.ToArray(), sup.W.Economy.Rows.ToArray());
        Assert.Equal(plain.W.Events.ToArray(), sup.W.Economy.Events.ToArray());
        Assert.Equal(plain.W.TurnRows.ToArray(), sup.W.Economy.TurnRows.ToArray());
        for (int i = 0; i < plain.W.Ledger.AccountCount; i++) Assert.Equal(plain.W.Ledger.Balance(i), sup.W.Ledger.Balance(i));
        Assert.All(sup.W.AllocationRows, r => Assert.Equal(0, r.Granted));
    }

    // ------------------------------------------------------------------ supply train: inside lane E's logistics path

    /// <summary>A halted foot army with no carriers has no supply line: nothing is delivered and it goes hungry. Support wagons
    /// join its train: lane E's own fleet arithmetic now runs a supply line (delivered &gt; 0) and its own train capacity grows,
    /// so hunger falls. No new supply model: the row values come from lane E's PulseStart plan.</summary>
    [Fact]
    public void SupplyTrain_RunsASupplyLine_ForAnArmyWithoutCarriers()
    {
        long start = SupportRig.WarStart();
        var wars = new[] { WarOf(0, start, start + 12 * Week, 0, 1) };
        var pools = new[] { new SupportPool(0, 0, 200, 400, 200) };
        SupRig Run(long wagons)
        {
            var rig = SupportRig.Build(SupportRig.FootArmy(march: false), wars, 2, start, pools, probes: new[] { start - 1 });
            SupportRig.Drive(rig, start + 12 * Week, t => t == start - 1 ? new[] { SupportRig.Allocate(0, 0, SupportKind.SupplyTrain, wagons) } : Array.Empty<(int, long, long)>());
            return rig;
        }
        var none = Run(0);
        var with = Run(150);
        var r0 = none.W.Economy.Rows.Where(r => r.ArmyId == 0).ToArray();
        var r1 = with.W.Economy.Rows.Where(r => r.ArmyId == 0).ToArray();
        Assert.All(r0, r => Assert.Equal(0, r.Delivered));
        Assert.True(r1.Sum(r => r.Delivered) > 0);
        Assert.True(r1.Sum(r => r.HungerPpm) < r0.Sum(r => r.HungerPpm));
        Assert.Equal(150, with.W.Economy.SupportOf(0).Wagons);
        Assert.Equal(300, with.W.Economy.SupportOf(0).DraftAnimals);                 // lane E's hitch rule: two per wagon
        Assert.Equal(150, with.W.Economy.SupportOf(0).Staff);                        // one driver per wagon
    }

    /// <summary>Support wagons are lane E's ox-drawn train: a foot army that takes them on marches at the ox-train pace (15 km a
    /// day against 20 on open ground) — the real cost of carrying more (lane E's BaseSpeed, slowest element).</summary>
    [Fact]
    public void SupplyTrain_SlowsAFootArmyToTheOxTrainPace()
    {
        long start = SupportRig.WarStart();
        var wars = new[] { WarOf(0, start, start + 2 * Week, 0, 1) };
        var pools = new[] { new SupportPool(0, 0, 50, 100, 50) };
        long FirstPulseDistance(long wagons)
        {
            var rig = SupportRig.Build(SupportRig.FootArmy(march: true, position: 0, trainGrain: 50_000_000), wars, 2, start, pools, probes: new[] { start - 1 });
            SupportRig.Drive(rig, start + 2 * Week, t => t == start - 1 ? new[] { SupportRig.Allocate(0, 0, SupportKind.SupplyTrain, wagons) } : Array.Empty<(int, long, long)>());
            return rig.W.Economy.Rows.First(r => r.ArmyId == 0).DistanceM;
        }
        var lr = LogisticsRates.Proposal();
        long foot = FirstPulseDistance(0), ox = FirstPulseDistance(50);
        Assert.True(ox < foot);
        // lane E's march arithmetic on one track segment over open ground: floor(W x speed x 1000 / (ground cost x day))
        long cost = ArmyLogistics.GroundCost(Seg(400, TerrainClass.Open, RoadClass.Track, -1), TerrainLogistics.Proposal());
        long w = 7 * Day;
        Assert.Equal((long)((Int128)w * lr.MarchFootMPerDay * 1000 / ((Int128)cost * Day)), foot);
        Assert.Equal((long)((Int128)w * lr.MarchOxTrainMPerDay * 1000 / ((Int128)cost * Day)), ox);
    }

    /// <summary>The cost terms are exact: in the first pulse (no carry yet) the support staff's pay is the foot rate and the standing
    /// need adds the staff's field ration and the animals' rest feed and roughage — lane E's own arithmetic, recomputed here.</summary>
    [Fact]
    public void SupportCosts_AreLaneEsRationPayAndFodderTerms_Exactly()
    {
        long start = SupportRig.WarStart();
        var wars = new[] { WarOf(0, start, start + 2 * Week, 0, 1) };
        var pools = new[] { new SupportPool(0, 40, 30, 60, 30) };
        var rig = SupportRig.Build(SupportRig.FootArmy(march: false, trainGrain: 50_000_000), wars, 2, start, pools, probes: new[] { start - 1 });
        SupportRig.Drive(rig, start + 2 * Week, t => t == start - 1
            ? new[] { SupportRig.Allocate(0, 0, SupportKind.SupplyTrain, 30), SupportRig.Allocate(0, 0, SupportKind.FieldMedical, 40) }
            : Array.Empty<(int, long, long)>());
        var row = rig.W.Economy.Rows.First(r => r.ArmyId == 0);
        var lr = LogisticsRates.Proposal();
        long w = row.EndMs - row.StartMs, men = 2_000, staff = 40 + 30, draft = 60;
        long graze = TerrainLogistics.Proposal().GrazePermille(TerrainClass.Open);
        BigInteger standScaled = (BigInteger)men * (lr.RationBase + lr.RationFieldExtra) * 1000
            + (BigInteger)staff * (lr.RationBase + lr.RationFieldExtra) * 1000
            + (BigInteger)draft * lr.DraftHardRest * 1000 + (BigInteger)draft * lr.DraftRoughageAsGrain * (1000 - graze);
        Assert.Equal((long)(standScaled * w / ((BigInteger)SimInstant.MsPerYear * 1000)), row.StandingNeed);
        BigInteger payScaled = (BigInteger)men * (men * lr.PayFoot) + (BigInteger)staff * lr.PayFoot * men;   // composition = all foot
        Assert.Equal((long)(payScaled * w / ((BigInteger)SimInstant.MsPerYear * men)), row.PayDue);
    }

    /// <summary>[verification round 1, D-R1-02] The march terms (support proposal §5): a marching army's movement need adds the
    /// support staff's march exertion (RationMarchExtra per staff-year) and the support animals' working feed (DraftHardWorkExtra
    /// per animal-year). First pulse, no carry; the army marches the whole pulse at the ox-train pace (its distance is the
    /// full-pulse formula), so lane E integrates the extras over the whole pulse. Recomputed by hand.</summary>
    [Fact]
    public void SupportCosts_MarchExtrasOfStaffAndAnimals_Exactly()
    {
        long start = SupportRig.WarStart();
        var wars = new[] { WarOf(0, start, start + 2 * Week, 0, 1) };
        var pools = new[] { new SupportPool(0, 30, 20, 40, 20) };              // 30 medical staff; 20 wagons, 40 draft animals, 20 drivers
        var rig = SupportRig.Build(SupportRig.FootArmy(march: true, position: 0, trainGrain: 50_000_000), wars, 2, start, pools, probes: new[] { start - 1 });
        SupportRig.Drive(rig, start + Week + 1, t => t == start - 1
            ? new[] { SupportRig.Allocate(0, 0, SupportKind.SupplyTrain, 20), SupportRig.Allocate(0, 0, SupportKind.FieldMedical, 30) }
            : Array.Empty<(int, long, long)>());
        var row = rig.W.Economy.Rows.Single(r => r.ArmyId == 0 && r.PulseIndex == 0);
        var lr = LogisticsRates.Proposal();
        long w = row.EndMs - row.StartMs, men = 2_000, staff = 30 + 20, draft = 40;
        Assert.Equal(new SupportComposition(staff, 20, draft, 0), rig.W.Economy.SupportOf(0));
        long cost = ArmyLogistics.GroundCost(Seg(400, TerrainClass.Open, RoadClass.Track, -1), TerrainLogistics.Proposal());
        Assert.Equal((long)((Int128)w * lr.MarchOxTrainMPerDay * 1000 / ((Int128)cost * Day)), row.DistanceM);   // marched all of W
        BigInteger moveRate = (BigInteger)men * lr.RationMarchExtra + (BigInteger)staff * lr.RationMarchExtra + (BigInteger)draft * lr.DraftHardWorkExtra;
        Assert.Equal((long)(moveRate * w / SimInstant.MsPerYear), row.MovementNeed);
        Assert.True(staff * lr.RationMarchExtra * w / SimInstant.MsPerYear > 0 && draft * lr.DraftHardWorkExtra * w / SimInstant.MsPerYear > 0);
    }

    /// <summary>[verification round 1, D-R1-02] The patients' ration (support proposal §5: RationBase per patient-year): the pulse
    /// after a battle plans the wounded in care into the army's standing need. Pulse 1's need is recomputed by hand INCLUDING lane
    /// E's integration carry from pulse 0 (the exact fraction left over: (s0 × w0) mod (year × 1000)); the patients are the
    /// battle's wounded less those the care settlement at pulse 0's end returned or lost.</summary>
    [Fact]
    public void SupportCosts_PatientsEatTheBaseRation_Exactly()
    {
        long start = SupportRig.WarStart();
        var wars = new[] { WarOf(0, start, start + 4 * Week, 0, 1) };
        var rig = SupportRig.Build(SupportRig.FootArmy(march: false, trainGrain: 50_000_000), wars, 2, start, new[] { new SupportPool(0, 40, 0, 0, 0) },
                                   probes: new[] { start - 1 });
        rig.W.ScheduleBattle(rig.S, new SupportBattle(0, start + 3 * Day, SupportRig.Collapse50(), false, 800, 2_000, 0));
        Func<long, (int, long, long)[]> orders = t => t == start - 1 ? new[] { SupportRig.Allocate(0, 0, SupportKind.FieldMedical, 40) } : Array.Empty<(int, long, long)>();
        SupportRig.Drive(rig, start + Week + 1, orders);                                   // just into pulse 1: its plan is latched
        var r0 = rig.W.Economy.Rows.Single(r => r.ArmyId == 0 && r.PulseIndex == 0);
        var sup1 = rig.W.Economy.SupportOf(0);
        long men1 = rig.W.Economy.MenLatchedOf(0);
        var b = rig.W.BattleRows.Single();
        long cared = rig.W.CareRows.Where(c => c.ToMs <= r0.EndMs).Sum(c => c.Recovered + c.Died);
        Assert.Equal(b.Wounded - cared, sup1.Patients);
        Assert.True(sup1.Patients > 0);
        Assert.Equal(40, sup1.Staff);
        SupportRig.Drive(rig, start + 2 * Week + 1, orders);
        var r1 = rig.W.Economy.Rows.Single(r => r.ArmyId == 0 && r.PulseIndex == 1);
        Assert.Equal(men1, r1.MenStart);
        var lr = LogisticsRates.Proposal();
        long ration = lr.RationBase + lr.RationFieldExtra;
        BigInteger d = (BigInteger)SimInstant.MsPerYear * 1000;
        BigInteger s0 = (BigInteger)2_000 * ration * 1000 + (BigInteger)40 * ration * 1000;            // pulse 0: no patients yet
        BigInteger s1 = (BigInteger)men1 * ration * 1000 + (BigInteger)40 * ration * 1000 + (BigInteger)sup1.Patients * lr.RationBase * 1000;
        long w0 = r0.EndMs - r0.StartMs, w1 = r1.EndMs - r1.StartMs;
        Assert.Equal((long)(s0 * w0 / d), r0.StandingNeed);
        Assert.Equal((long)((s0 * w0 % d + s1 * w1) / d), r1.StandingNeed);
    }

    /// <summary>[verification round 1, D-R1-02] The support staff carry their own rations (support proposal §3.2): lane E's train
    /// capacity = wagons × payload + floor((soldiers + support staff) × RationBase × CarryDaysPerMan / year days). Observed as the
    /// restock request of the first pulse of an army at its depot (position 0: no haul, so no carriage loss and no capacity cap)
    /// with an empty train: its request is exactly the train capacity; its standing request exactly its standing need.</summary>
    [Fact]
    public void SupportCosts_StaffCarryRationsInTheTrainCapacity_Exactly()
    {
        long start = SupportRig.WarStart();
        var wars = new[] { WarOf(0, start, start + 2 * Week, 0, 1) };
        var pools = new[] { new SupportPool(0, 30, 20, 40, 20) };
        var rig = SupportRig.Build(SupportRig.FootArmy(march: false, position: 0, trainGrain: 0), wars, 2, start, pools, probes: new[] { start - 1 });
        SupportRig.Drive(rig, start + Week + 1, t => t == start - 1
            ? new[] { SupportRig.Allocate(0, 0, SupportKind.SupplyTrain, 20), SupportRig.Allocate(0, 0, SupportKind.FieldMedical, 30) }
            : Array.Empty<(int, long, long)>());
        var row = rig.W.Economy.Rows.Single(r => r.ArmyId == 0 && r.PulseIndex == 0);
        var req = rig.W.Economy.Allocations.First(a => a.WarId == 0 && a.PulseIndex == 0).Requests.Single(q => q.ArmyId == 0);
        var lr = LogisticsRates.Proposal();
        long men = 2_000, staff = 30 + 20, wagons = 20;
        long capacity = wagons * lr.WagonPayloadGm + (long)((BigInteger)(men + staff) * lr.RationBase * lr.CarryDaysPerMan / SimInstant.YearDays);
        Assert.Equal(row.StandingNeed, req.Standing);
        Assert.Equal(0, req.Movement);
        Assert.Equal(capacity, req.Restock);
        Assert.NotEqual(wagons * lr.WagonPayloadGm + (long)((BigInteger)men * lr.RationBase * lr.CarryDaysPerMan / SimInstant.YearDays), req.Restock);
    }

    /// <summary>[verification round 1, D-R1-06] Where the battle sends people: the captured to the captured sink (nothing else feeds
    /// it), the deserters home to the owner's civilians at the battle instant (measured between two probes 1 ms either side).
    /// Captured (80) and deserted (48) differ, so a swap of the two destinations shows.</summary>
    [Fact]
    public void SupportBattle_SendsPrisonersToTheCapturedSink_AndDesertersHome()
    {
        long start = SupportRig.WarStart(), at = start + 3 * Day;
        var wars = new[] { WarOf(0, start, start + 4 * Week, 0, 1) };
        var rig = SupportRig.Build(SupportRig.FootArmy(march: false, trainGrain: 50_000_000), wars, 2, start, new[] { new SupportPool(0, 20, 0, 0, 0) },
                                   probes: new[] { start - 1, at - 1, at + 1 });
        rig.W.ScheduleBattle(rig.S, new SupportBattle(0, at, SupportRig.Collapse50(), false, 800, 2_000, 0));
        int civ = rig.W.Economy.CivilianAccountOf(0);
        long civBefore = -1, civAfter = -1, capturedBefore = -1;
        SupportRig.Drive(rig, start + 4 * Week, t =>
        {
            if (t == at - 1) { civBefore = rig.W.Ledger.Balance(civ); capturedBefore = rig.W.Ledger.Balance(rig.W.CapturedAccount); }
            if (t == at + 1) civAfter = rig.W.Ledger.Balance(civ);
            return t == start - 1 ? new[] { SupportRig.Allocate(0, 0, SupportKind.FieldMedical, 20) } : Array.Empty<(int, long, long)>();
        });
        var b = rig.W.BattleRows.Single();
        Assert.Equal((80L, 48L), (b.Captured, b.Deserted));
        Assert.Equal(0, capturedBefore);
        Assert.Equal(b.Captured, rig.W.Ledger.Balance(rig.W.CapturedAccount));
        Assert.Equal(b.Deserted, civAfter - civBefore);
    }

    // ------------------------------------------------------------------ field medical: casualty split and care

    /// <summary>Without staff the collapse is lane W's split exactly; with coverage c the save is floor(dead × SaveMax × c/(c+c0)),
    /// moved from Dead to Wounded; people are conserved through the ledger.</summary>
    [Fact]
    public void Medical_BattleSave_IsLaneWsSplitPlusAnExactDeadToWoundedMove()
    {
        long start = SupportRig.WarStart();
        var wars = new[] { WarOf(0, start, start + 4 * Week, 0, 1) };
        SupportBattleRow Battle(long medics)
        {
            var pools = new[] { new SupportPool(0, medics, 0, 0, 0) };
            var rig = SupportRig.Build(SupportRig.FootArmy(march: false, trainGrain: 50_000_000), wars, 2, start, pools, probes: new[] { start - 1 });
            rig.W.ScheduleBattle(rig.S, new SupportBattle(0, start + 3 * Day, SupportRig.Collapse50(), false, 800, 2_000, 0));
            var t0 = SupportRig.Totals(rig.W.Ledger);
            SupportRig.Drive(rig, start + 4 * Week, t => t == start - 1 ? new[] { SupportRig.Allocate(0, 0, SupportKind.FieldMedical, medics) } : Array.Empty<(int, long, long)>());
            Assert.Equal(t0, SupportRig.Totals(rig.W.Ledger));
            return rig.W.BattleRows.Single();
        }
        var none = Battle(0);
        var lw = BattleCollapse.Split(none.Persons, SupportRig.Collapse50(), false);
        Assert.Equal(0, none.Saved);
        Assert.Equal((lw.Dead, lw.Wounded, lw.Captured, lw.Deserted), (none.Dead, none.Wounded, none.Captured, none.Deserted));
        var with = Battle(20);                                     // 20 per 2,000 men: c = 10 = c0, so u = 1/2
        Assert.Equal(10, with.Coverage);
        long expected = with.DeadBefore * 400_000 * 10 / (1_000_000L * (10 + 10));
        Assert.Equal(expected, with.Saved);
        Assert.Equal(with.DeadBefore - with.Saved, with.Dead);
        Assert.Equal(lw.Wounded + with.Saved, with.Wounded);
        Assert.True(with.Saved > 0);
    }

    /// <summary>The care of the wounded is lane E's per-day compounding of per-sim-year rates (Law 3): with coverage more return
    /// to duty and fewer die of wounds than without; the settlement equals SupportMath.CareMilli exactly.</summary>
    [Fact]
    public void Medical_Care_MoreRecoverFewerDie_WithCoverage_AtPerYearRates()
    {
        var r = SupportRates.Proposal();
        var (rec0, die0) = SupportMath.WoundRates(0, r);
        var (rec1, die1) = SupportMath.WoundRates(10, r);
        Assert.Equal((3_000, 1_500), (rec0, die0));
        Assert.Equal((3_000 + 3_000, 1_500 - 600), (rec1, die1));            // u = 1/2: +6,000/2; −1,500 × 0.8 / 2
        var (rm0, dm0) = SupportMath.CareMilli(1_000, 0, 28 * Day, r);
        var (rm1, dm1) = SupportMath.CareMilli(1_000, 10, 28 * Day, r);
        Assert.True(rm1 > rm0 && dm1 < dm0);
        // the same rates over 28 days: one settlement equals HeadcountLoss directly
        var s = HeadcountLoss.Survival(rec1 + die1, 28 * Day);
        Assert.Equal(HeadcountLoss.LossMilli(1_000, rec1, rec1 + die1, s), rm1);
        Assert.Equal(HeadcountLoss.LossMilli(1_000, die1, rec1 + die1, s), dm1);
        // dt-correct: the shares depend on elapsed time, not on a per-call amount
        var (rmLong, _) = SupportMath.CareMilli(1_000, 10, 56 * Day, r);
        Assert.True(rmLong > rm1);
    }

    /// <summary>In the world: the wounded of a battle are settled at every pulse end at the latched coverage; recovered men rejoin
    /// the army's ranks (lane E's men account), the dead of wounds go to lane E's dead account; carries persist.</summary>
    [Fact]
    public void Medical_CareInTheWorld_ReturnsMenToTheRanks_AndConservesPeople()
    {
        long start = SupportRig.WarStart();
        var wars = new[] { WarOf(0, start, start + 30 * Week, 0, 1) };
        (long Recovered, long Died, long Men) Run(long medics)
        {
            var rig = SupportRig.Build(SupportRig.FootArmy(march: false, trainGrain: 900_000_000), wars, 2, start, new[] { new SupportPool(0, medics, 0, 0, 0) },
                                       probes: new[] { start - 1 });
            rig.W.ScheduleBattle(rig.S, new SupportBattle(0, start + 3 * Day, SupportRig.Collapse50(), true, 800, 2_000, 0));
            var t0 = SupportRig.Totals(rig.W.Ledger);
            SupportRig.Drive(rig, start + 30 * Week, t => t == start - 1 ? new[] { SupportRig.Allocate(0, 0, SupportKind.FieldMedical, medics) } : Array.Empty<(int, long, long)>());
            Assert.Equal(t0, SupportRig.Totals(rig.W.Ledger));
            Assert.All(rig.W.CareRows, c => Assert.True(c.Recovered + c.Died <= c.WoundedBefore));
            for (int i = 1; i < rig.W.CareRows.Count; i++) Assert.Equal(rig.W.CareRows[i - 1].ToMs, rig.W.CareRows[i].FromMs);   // contiguous
            // EXACT strength identity for every pulse after the battle's (pulse 0): lane E's end strength = start strength −
            // deserters − disease deaths + the men the care settlement returned at that pulse end (before lane E's row).
            int checkedPulses = 0;
            foreach (var row in rig.W.Economy.Rows.Where(r => r.ArmyId == 0 && r.PulseIndex >= 1))
            {
                long recovered = rig.W.CareRows.Where(c => c.ToMs == row.EndMs).Sum(c => c.Recovered);
                Assert.Equal(row.MenStart - row.Deserted - row.DiseaseDeaths + recovered, row.MenEnd);
                checkedPulses++;
            }
            Assert.True(checkedPulses >= 20);
            if (medics > 0) Assert.True(rig.W.CareRows.Sum(c => c.Recovered) > 0);
            return (rig.W.CareRows.Sum(c => c.Recovered), rig.W.CareRows.Sum(c => c.Died), rig.W.Economy.MenOf(0));
        }
        var none = Run(0);
        var with = Run(50);
        Assert.True(with.Recovered > none.Recovered);
        Assert.True(with.Died < none.Died);
        Assert.True(with.Men > none.Men);
    }

    // ------------------------------------------------------------------ morale: the TacticalResolver's end morale carries over

    /// <summary>"Morale decreases when troops participate in combat" (DIR5 §2.12), reusing the TacticalResolver's morale: a real
    /// tactical battle (lane D runs one here) collapses through lane W's FromTactical; the army's campaign morale becomes the
    /// battle's end morale (micro units). With medical staff the saved men give back part of the casualty-driven loss.
    /// [verification round 1, D-R1-04] The expected restore was computed with SupportMath.MoraleRestore itself, so a wrong restore
    /// formula passed (verifier mutants V7, V8). It is now recomputed by hand from the TacticalResolver's loss formula.
    /// [verification round 2, D-R2-01] Since round 2 the campaign morale falls by the battle's net loss (start − end, less the
    /// restore) and never rises. This battle starts at the campaign morale (800 = 800,000 micro units) and ends below it, restore
    /// included, so "becomes the end morale" and "falls by the net loss" give the same numbers here and the assertions are unchanged.
    /// The cases where the two rules differ are pinned by Morale_ABattleNeverRaisesCampaignMorale_… and
    /// Morale_ABattleLowersCampaignMoraleByItsNetLossOnly.</summary>
    [Fact]
    public void Morale_ACampaignArmyLeavesABattleAtTheTacticalEndMorale_MedicsRestorePart()
    {
        var setup = TacticalScenarios.MeetingAsymmetric(UnitProfiles.HeavyInfantry, 50, UnitProfiles.HeavyInfantry, 40, 7) with { };
        setup = setup with { Formations = new[] { setup.Formations[0] with { Morale = 800 }, setup.Formations[1] } };
        var battle = new TacticalBattle(setup, new KnowledgeAi(), new KnowledgeAi());
        var res = TT.RunGuarded(battle);
        var inputs = BattleCollapse.FromTactical(battle, setup, new[] { (1L, 0), (2L, 1) });
        var mine = inputs[0];
        Assert.True(mine.EntityCounts[(int)CollapseCategory.Dead] > 0);
        long start = SupportRig.WarStart();
        var wars = new[] { WarOf(0, start, start + 4 * Week, 0, 1) };
        (long Before, long After, long Saved, long Dead) Run(long medics)
        {
            var rig = SupportRig.Build(SupportRig.FootArmy(march: false, trainGrain: 50_000_000), wars, 2, start, new[] { new SupportPool(0, medics, 0, 0, 0) },
                                       probes: new[] { start - 1 });
            rig.W.ScheduleBattle(rig.S, new SupportBattle(0, start + 3 * Day, mine, res.HoldsField == 0, 800, setup.Rules.MoraleLossFullStrength, 0));
            SupportRig.Drive(rig, start + 3 * Day, t => t == start - 1 ? new[] { SupportRig.Allocate(0, 0, SupportKind.FieldMedical, medics) } : Array.Empty<(int, long, long)>());
            var b = rig.W.BattleRows.Single();
            Assert.Equal(b.MoraleAfter, rig.W.Economy.MoraleOf(0));
            return (b.MoraleBefore, b.MoraleAfter, b.Saved, b.DeadBefore);
        }
        var none = Run(0);
        Assert.Equal(800_000, none.Before);                                   // lane E's opening morale = the battle's start morale
        Assert.Equal(mine.MoraleEnd * 1_000_000 / mine.MoraleMax, none.After);
        Assert.True(none.After < none.Before);                                // combat cost morale
        var with = Run(100);
        // By hand, no call to SupportMath or ConditionMath: loss = min(MoraleMax, floor(dead entities × F × 100 / (entities ×
        // (100 + L)))) at leadership L = 0; restore = floor(loss × saved × ρ / (dead persons × 1000)) with ρ = 500 ‰ (PROPOSAL).
        long deadEntities = mine.EntityCounts[3], entities = 0;
        foreach (long n in mine.EntityCounts) entities += n;
        BigInteger loss = BigInteger.Min(1000, (BigInteger)deadEntities * setup.Rules.MoraleLossFullStrength * 100 / ((BigInteger)entities * 100));
        long restore = (long)(loss * with.Saved * 500 / ((BigInteger)with.Dead * 1000));
        Assert.True(restore > 0);
        Assert.Equal(Math.Min(1000, mine.MoraleEnd + restore) * 1_000_000 / 1000, with.After);
        Assert.True(with.After > none.After);
    }

    /// <summary>[verification round 1, D-R1-04] The medical morale restore with literal numbers (support proposal §3.1): 2,000 men,
    /// 20 medical staff (coverage c = 10 = c0, u = 1/2), lane W's collapse of 50 entities with 10 dead and 5 wounded, end morale 600
    /// of 1,000. Dead persons 10/50 × 2,000 = 400; saved floor(400 × 400,000 × 10 / (1,000,000 × 20)) = 80. Casualty-driven loss of
    /// the DEAD entities only: floor(10 × 2,000 × 100 / (50 × 100)) = 400; restore floor(400 × 80 × 500 / (400 × 1,000)) = 40; the
    /// campaign morale after the battle = (600 + 40) / 1,000 = 640,000 micro units. A full restore (ρ ignored) would give 680,000;
    /// counting the wounded entities' loss too, 660,000.</summary>
    [Fact]
    public void Morale_MedicalRestore_ByHand_TenDeadOfFifty()
    {
        long start = SupportRig.WarStart();
        var wars = new[] { WarOf(0, start, start + 4 * Week, 0, 1) };
        var rig = SupportRig.Build(SupportRig.FootArmy(march: false, trainGrain: 50_000_000), wars, 2, start, new[] { new SupportPool(0, 20, 0, 0, 0) },
                                   probes: new[] { start - 1 });
        rig.W.ScheduleBattle(rig.S, new SupportBattle(0, start + 3 * Day, SupportRig.Collapse50(), false, 800, 2_000, 0));
        SupportRig.Drive(rig, start + 3 * Day + 1, t => t == start - 1 ? new[] { SupportRig.Allocate(0, 0, SupportKind.FieldMedical, 20) } : Array.Empty<(int, long, long)>());
        var b = rig.W.BattleRows.Single();
        Assert.Equal((2_000L, 10L, 400L, 80L), (b.Persons, b.Coverage, b.DeadBefore, b.Saved));
        Assert.Equal(800_000, b.MoraleBefore);
        Assert.Equal(640_000, b.MoraleAfter);
        Assert.Equal(640_000, rig.W.Economy.MoraleOf(0));
    }

    // ------------------------------------------------------------------ allocation: delivery, contention, limits, refusals

    /// <summary>TURN-EXACT DELIVERY PIN. An allocation order is delivered at its window instant after every world event of that
    /// instant, and applied at the army's next pulse start: issued 1 ms before pulse k starts, it is in force for pulse k; issued
    /// AT pulse k's start instant (after k was planned), it is in force only from pulse k + 1.</summary>
    [Fact]
    public void Allocation_DeliveryPin_ToThePulse()
    {
        long start = SupportRig.WarStart();
        var wars = new[] { WarOf(0, start, start + 6 * Week, 0, 1) };
        var pools = new[] { new SupportPool(0, 100, 0, 0, 0) };
        long FirstPulseWith(long issueAt)
        {
            var rig = SupportRig.Build(SupportRig.FootArmy(march: false, trainGrain: 50_000_000), wars, 2, start, pools, probes: new[] { issueAt });
            SupportRig.Drive(rig, start + 6 * Week, t => t == issueAt ? new[] { SupportRig.Allocate(0, 0, SupportKind.FieldMedical, 30) } : Array.Empty<(int, long, long)>());
            var latch = rig.W.AllocationRows.First(r => r.Kind == (byte)SupportKind.FieldMedical && r.Granted == 30);
            Assert.Equal(issueAt, rig.W.OrderRows.Single().AtMs);
            return latch.PulseIndex;
        }
        var pulseStarts = PulseStarts(start, wars, pools);
        Assert.Equal(1, FirstPulseWith(pulseStarts[1] - 1));
        Assert.Equal(2, FirstPulseWith(pulseStarts[1]));
        Assert.Equal(2, FirstPulseWith(pulseStarts[1] + 1));
        Assert.Equal(0, FirstPulseWith(start - 1));
        Assert.Equal(1, FirstPulseWith(start));
    }

    private static long[] PulseStarts(long start, WarSpec[] wars, SupportPool[] pools)
    {
        var rig = SupportRig.Build(SupportRig.FootArmy(march: false, trainGrain: 50_000_000), wars, 2, start, pools);
        SupportRig.Drive(rig, start + 6 * Week, _ => Array.Empty<(int, long, long)>());
        return rig.W.Economy.Rows.Where(r => r.ArmyId == 0).Select(r => r.StartMs).ToArray();
    }

    /// <summary>TIE-DENSE pool contention: three armies of one war at equal priority latch at the same instant from a pool too
    /// small for all; lane E's (Priority, Id) key serves the lowest id first. The result does not depend on the order of the
    /// armies in the setup; a lower priority value is served first whatever the id.</summary>
    [Fact]
    public void PoolContention_AtOneInstant_IsServedByPriorityThenId_IndependentOfSetupOrder()
    {
        long start = SupportRig.WarStart();
        var wars = new[] { WarOf(0, start, start + 2 * Week, 0, 1) };
        var route = new Route(new[] { Seg(100, TerrainClass.Open, RoadClass.Track, -1) });
        ArmySetup Army(int id, int priority) => new(id, 0, 0, priority, new ArmyComposition(1_000, 0, 0, 0, 0, 0, 0, 0, false), route, 50_000, false,
                                                    50_000_000, 0, 0, Array.Empty<(long, long)>());
        long[] Granted(int[] priorities, int[] order)
        {
            var armies = new ArmySetup[3];
            for (int k = 0; k < 3; k++) armies[k] = Army(order[k], priorities[order[k]]);
            var setup = SupportRig.FootArmy(false) with { Armies = armies };
            var rig = SupportRig.Build(setup, wars, 2, start, new[] { new SupportPool(0, 50, 0, 0, 0) }, probes: new[] { start - 1 });
            SupportRig.Drive(rig, start + Week, t => t == start - 1
                ? new[] { SupportRig.Allocate(0, 2, SupportKind.FieldMedical, 30), SupportRig.Allocate(0, 0, SupportKind.FieldMedical, 30), SupportRig.Allocate(0, 1, SupportKind.FieldMedical, 30) }
                : Array.Empty<(int, long, long)>());
            return new[] { rig.W.MedicsOf(0), rig.W.MedicsOf(1), rig.W.MedicsOf(2) };
        }
        foreach (var order in new[] { new[] { 0, 1, 2 }, new[] { 2, 1, 0 }, new[] { 1, 2, 0 } })
        {
            Assert.Equal(new long[] { 30, 20, 0 }, Granted(new[] { 0, 0, 0 }, order));    // equal priority: id order
            Assert.Equal(new long[] { 20, 0, 30 }, Granted(new[] { 1, 1, 0 }, order));    // army 2 (priority 0) first, then the 1-1 tie by id
        }
    }

    /// <summary>Limits and refusals: a target above the army's limit (per 1000 men at the latch) is granted to the limit; refused
    /// orders are recorded with their code and change nothing.</summary>
    [Fact]
    public void Limits_AndRefusalCodes()
    {
        long start = SupportRig.WarStart();
        var wars = new[] { WarOf(0, start, start + 2 * Week, 0, 1) };
        var rig = SupportRig.Build(SupportRig.FootArmy(false, trainGrain: 50_000_000), wars, 2, start, new[] { new SupportPool(0, 500, 500, 1_000, 500) },
                                   probes: new[] { start - 1 });
        SupportRig.Drive(rig, start + Week, t => t == start - 1
            ? new[]
            {
                SupportRig.Allocate(0, 0, SupportKind.FieldMedical, 400),              // limit 50 per 1000 x 2,000 = 100
                SupportRig.Allocate(0, 0, SupportKind.SupplyTrain, 400),               // limit 100 per 1000 x 2,000 = 200
                SupportRig.Allocate(1, 0, SupportKind.FieldMedical, 5),                // not the owner
                SupportRig.Allocate(0, 7, SupportKind.FieldMedical, 5),                // no such army
                (SupportKinds.OrderAllocate, SupportKinds.Pack(0, 0), 9 * SupportKinds.TargetStride + 5),   // no such kind
            }
            : Array.Empty<(int, long, long)>());
        var codes = rig.W.OrderRows.Select(o => (SupportRefusal)o.Refusal).ToArray();
        Assert.Equal(new[] { SupportRefusal.None, SupportRefusal.None, SupportRefusal.NotOwner, SupportRefusal.UnknownArmy, SupportRefusal.UnknownKind }, codes);
        var med = rig.W.AllocationRows.First(r => r.Kind == (byte)SupportKind.FieldMedical);
        Assert.Equal((400L, 100L, 100L, 400L), (med.Requested, med.Limit, med.Granted, med.PoolAfter));   // 2,000 men at pulse 0
        var wag = rig.W.AllocationRows.First(r => r.Kind == (byte)SupportKind.SupplyTrain);
        Assert.Equal((400L, 200L, 200L, 300L), (wag.Requested, wag.Limit, wag.Granted, wag.PoolAfter));
        // The limit follows the army's strength at EVERY latch: after a pulse of lane E's disease deaths the army is below 2,000
        // and the excess staff go back to the pool at the next pulse start.
        int checkedRows = 0;
        foreach (var row in rig.W.AllocationRows)
        {
            long men = row.PulseIndex == rig.W.Economy.Rows.Max(r => r.PulseIndex) + 1
                ? rig.W.Economy.MenLatchedOf(row.ArmyId)                                           // the pulse still running
                : rig.W.Economy.Rows.First(r => r.ArmyId == row.ArmyId && r.PulseIndex == row.PulseIndex).MenStart;
            long perThousand = row.Kind == (byte)SupportKind.FieldMedical ? 50 : 100;
            Assert.Equal(men * perThousand / 1000, row.Limit);
            Assert.Equal(Math.Min(row.Requested, row.Limit), row.Granted);
            checkedRows++;
        }
        Assert.True(checkedRows >= 4);
        Assert.Contains(rig.W.AllocationRows, r => r.Kind == (byte)SupportKind.FieldMedical && r.Granted < 100);
        Assert.Equal(500 - rig.W.MedicsOf(0), rig.W.PoolMedicsOf(0));
    }

    /// <summary>Stacking is diminishing, never multiplicative: the save share u(c) = c/(c+c0) rises with coverage at a falling
    /// rate and stays below SaveMax; two allocations to one army are one coverage (a standing target is replaced, not added).</summary>
    [Fact]
    public void Stacking_IsSaturating_AndATargetReplacesTheLastOne()
    {
        var r = SupportRates.Proposal();
        long prev = 0, prevGain = long.MaxValue;
        for (long c = 5; c <= 80; c += 5)
        {
            long s = SupportMath.SavedFromDead(1_000_000, c, r);
            Assert.True(s > prev && s < 400_000);
            Assert.True(s - prev <= prevGain);
            prevGain = s - prev; prev = s;
        }
        long start = SupportRig.WarStart();
        var wars = new[] { WarOf(0, start, start + 2 * Week, 0, 1) };
        var rig = SupportRig.Build(SupportRig.FootArmy(false, trainGrain: 50_000_000), wars, 2, start, new[] { new SupportPool(0, 500, 0, 0, 0) }, probes: new[] { start - 1 });
        SupportRig.Drive(rig, start + Week, t => t == start - 1
            ? new[] { SupportRig.Allocate(0, 0, SupportKind.FieldMedical, 30), SupportRig.Allocate(0, 0, SupportKind.FieldMedical, 20) }
            : Array.Empty<(int, long, long)>());
        Assert.Equal(20, rig.W.MedicsOf(0));
        Assert.Equal(10, rig.W.CoverageOf(0));
    }

    /// <summary>Returning assets: lowering a target returns the difference to the pool at the next latch; a disbanded army returns
    /// everything (its wounded go home). People, animals and wagons are conserved exactly at every observed instant.</summary>
    [Fact]
    public void ReturnsAndDisbandment_ConserveEveryCommodity()
    {
        long start = SupportRig.WarStart();
        var wars = new[] { WarOf(0, start, start + 10 * Week, 0, 1) };
        var rig = SupportRig.Build(SupportRig.FootArmy(false, trainGrain: 50_000_000), wars, 2, start, new[] { new SupportPool(0, 200, 100, 200, 100) },
                                   probes: new[] { start - 1, start + Week - 1 });
        rig.W.ScheduleBattle(rig.S, new SupportBattle(0, start + 10 * Day, new CollapseInput(0, 0, new long[] { 2, 1, 1, 40, 6 }, 100, 1000), false, 800, 2_000, 0));
        var t0 = SupportRig.Totals(rig.W.Ledger);
        var seen = new List<Int128[]>();
        SupportRig.Drive(rig, start + 10 * Week, t =>
        {
            seen.Add(SupportRig.Totals(rig.W.Ledger));
            return t == start - 1
                ? new[] { SupportRig.Allocate(0, 0, SupportKind.FieldMedical, 90), SupportRig.Allocate(0, 0, SupportKind.SupplyTrain, 60) }
                : new[] { SupportRig.Allocate(0, 0, SupportKind.SupplyTrain, 10) };
        });
        foreach (var t in seen) Assert.Equal(t0, t);
        Assert.Equal(t0, SupportRig.Totals(rig.W.Ledger));
        Assert.Equal(ArmyCondition.Disbanded, rig.W.Economy.ConditionOf(0));       // 80 % lost: below lane E's disband strength
        Assert.Equal(0, rig.W.MedicsOf(0));
        Assert.Equal(0, rig.W.WagonsOf(0));
        Assert.Equal(0, rig.W.WoundedOf(0));
        Assert.Equal(200, rig.W.PoolMedicsOf(0));
        Assert.Equal(100, rig.W.PoolWagonsOf(0));
        Assert.Contains(rig.W.AllocationRows, a => a.Kind == (byte)SupportKind.SupplyTrain && a.Granted == 10);   // lowered before
    }

    // ------------------------------------------------------------------ serialized rows and replay

    /// <summary>POPULATED tables: every support row type, from a real run, encodes to its exact length, decodes bit-exactly and
    /// re-encodes to the same bytes (hash equality). [verification round 1, D-R1-07] The run had ArmyId = WarId = 0 in every
    /// allocation row, so an encoder swapping the two round-tripped (verifier mutant V13). It now has a second army and a second
    /// battle, and every pair of same-typed fields of every row type must be told apart by some row.</summary>
    [Fact]
    public void Codec_PopulatedTables_RoundTripBitExact_WithExactLengths()
    {
        var rig = PopulatedRun(out _);
        var w = rig.W;
        Assert.True(w.OrderRows.Count >= 2 && w.AllocationRows.Count >= 4 && w.CareRows.Count >= 2 && w.BattleRows.Count >= 2);
        RowFieldCoverage.AssertEverySameTypedFieldPairDiffers(w.OrderRows);
        RowFieldCoverage.AssertEverySameTypedFieldPairDiffers(w.AllocationRows);
        RowFieldCoverage.AssertEverySameTypedFieldPairDiffers(w.CareRows);
        RowFieldCoverage.AssertEverySameTypedFieldPairDiffers(w.BattleRows);
        void Check<T>(IReadOnlyList<T> rows, Func<IReadOnlyList<T>, byte[]> enc, Func<byte[], T[]> dec, int rowBytes)
        {
            byte[] b = enc(rows);
            Assert.Equal(SupportCodec.ExpectedLength(rows.Count, rowBytes), b.Length);
            var back = dec(b);
            Assert.Equal(rows.ToArray(), back);
            Assert.Equal(SHA256.HashData(b), SHA256.HashData(enc(back)));
        }
        Check(w.OrderRows, SupportCodec.EncodeOrders, SupportCodec.DecodeOrders, SupportCodec.OrderRowBytes);
        Check(w.AllocationRows, SupportCodec.EncodeAllocations, SupportCodec.DecodeAllocations, SupportCodec.AllocationRowBytes);
        Check(w.CareRows, SupportCodec.EncodeCare, SupportCodec.DecodeCare, SupportCodec.CareRowBytes);
        Check(w.BattleRows, SupportCodec.EncodeBattles, SupportCodec.DecodeBattles, SupportCodec.BattleRowBytes);
        Assert.Equal((30, 57, 52, 96), (SupportCodec.OrderRowBytes, SupportCodec.AllocationRowBytes, SupportCodec.CareRowBytes, SupportCodec.BattleRowBytes));
        Assert.Throws<FormatException>(() => SupportCodec.DecodeOrders(new byte[] { 1, 1, 0, 0, 0 }));
    }

    /// <summary>Replay: the logged panel orders re-run on a fresh world give the same state hash (orders, latches, care and the
    /// battle collapse are functions of state and the log only).</summary>
    [Fact]
    public void Replay_FromTheOrderLog_GivesTheSameStateHash()
    {
        var live = PopulatedRun(out long end);
        var replay = PopulatedRun(out _, live.R.Orders);
        Assert.Equal(live.W.StateHashHex(), replay.W.StateHashHex());
        Assert.Equal(live.W.Economy.Rows.ToArray(), replay.W.Economy.Rows.ToArray());
        Assert.True(end > 0);
    }

    /// <summary>The populated run: army 0 (2,000 foot, marching) and, since verification round 1 (D-R1-07), army 1 (1,000 foot,
    /// halted, priority 1) of the same war; a medical pool too small for both targets; army 1's battle (day 5) before army 0's
    /// (day 10), so neither battle row has Seq = ArmyId; later orders that are partly granted and refused.</summary>
    private static SupRig PopulatedRun(out long end, SubTurnOrderLog? replay = null)
    {
        long start = SupportRig.WarStart();
        end = start + 8 * Week;
        var wars = new[] { WarOf(0, start, end, 0, 1) };
        var setup = SupportRig.FootArmy(march: true, position: 0, trainGrain: 20_000_000);
        var army1 = new ArmySetup(1, 0, 0, 1, new ArmyComposition(1_000, 0, 0, 0, 0, 0, 0, 0, false), setup.Armies[0].Route, 50_000, false,
                                  20_000_000, 0, 0, Array.Empty<(long, long)>());
        setup = setup with { Armies = new[] { setup.Armies[0], army1 } };
        var rig = SupportRig.Build(setup, wars, 2, start,
            new[] { new SupportPool(0, 60, 80, 160, 80) }, probes: new[] { start - 1, start + 2 * Week + 5 }, replay: replay);
        rig.W.ScheduleBattle(rig.S, new SupportBattle(1, start + 5 * Day, new CollapseInput(0, 0, new long[] { 35, 4, 6, 3, 2 }, 700, 1000), false, 800, 2_000, 0));
        rig.W.ScheduleBattle(rig.S, new SupportBattle(0, start + 10 * Day, SupportRig.Collapse50(500), true, 800, 2_000, 10));
        if (replay is null)
            SupportRig.Drive(rig, end + Week, t => t == start - 1
                ? new[] { SupportRig.Allocate(0, 0, SupportKind.FieldMedical, 40), SupportRig.Allocate(0, 0, SupportKind.SupplyTrain, 50),
                          SupportRig.Allocate(0, 1, SupportKind.FieldMedical, 40) }
                : new[] { SupportRig.Allocate(0, 0, SupportKind.FieldMedical, 60), SupportRig.Allocate(1, 0, SupportKind.SupplyTrain, 3) });
        else rig.R.RunUntil(end + Week);
        return rig;
    }

    // ------------------------------------------------------------------ verification round 2 (D-R2-01, D-R2-02, D-R2-04)

    /// <summary>[verification round 2, D-R2-01] DR29: "Morale decreases when troops participate in combat". The TacticalResolver's
    /// in-battle recovery (HoldRecovery, +20 per casualty-free pulse, capped at MoraleMax and not at the start) lifts archers who
    /// hold and fight without loss ABOVE their start morale (800 → 880 here). The battle starts at the army's own campaign morale
    /// (800,000 micro units = 800 on the tactical scale), so the round-1 rule ("set the campaign morale to the battle's end
    /// morale") RAISED it to 880,000 through combat (verifier probe P_M: 24 of 24 runs). A battle now changes the campaign morale
    /// by its net loss only and never raises it: unchanged here, with and without medical staff.</summary>
    [Theory]
    [InlineData(1UL, 10)]
    [InlineData(7UL, 20)]
    public void Morale_ABattleNeverRaisesCampaignMorale_ArchersHoldingWithoutLossEndAboveTheirStart(ulong seed, int enemies)
    {
        var setup = TacticalScenarios.MeetingAsymmetric(UnitProfiles.Archers, 60, UnitProfiles.HeavyInfantry, enemies, seed);
        setup = setup with { Formations = new[] { setup.Formations[0] with { Morale = 800 }, setup.Formations[1] } };
        var battle = new TacticalBattle(setup, PassiveController.Instance, new KnowledgeAi());
        var res = TT.RunGuarded(battle);
        var mine = BattleCollapse.FromTactical(battle, setup, new[] { (1L, 0), (2L, 1) })[0];
        Assert.Equal(0, res.Formations.Single(x => x.Side == 0).Killed);                   // none of the archers fell ...
        Assert.True(res.Formations.Single(x => x.Side == 1).Killed > 0);                 // ... and they did fight: enemies fell
        Assert.True(mine.MoraleEnd > 800, "the in-battle recovery lifts the holding archers above their start");
        long start = SupportRig.WarStart();
        var wars = new[] { WarOf(0, start, start + 4 * Week, 0, 1) };
        foreach (long medics in new long[] { 0, 100 })
        {
            var rig = SupportRig.Build(SupportRig.FootArmy(march: false, trainGrain: 50_000_000), wars, 2, start, new[] { new SupportPool(0, medics, 0, 0, 0) },
                                       probes: new[] { start - 1 });
            rig.W.ScheduleBattle(rig.S, new SupportBattle(0, start + 3 * Day, mine, res.HoldsField == 0, 800, setup.Rules.MoraleLossFullStrength, 0));
            SupportRig.Drive(rig, start + 3 * Day + 1, t => t == start - 1 ? new[] { SupportRig.Allocate(0, 0, SupportKind.FieldMedical, medics) } : Array.Empty<(int, long, long)>());
            var b = rig.W.BattleRows.Single();
            Assert.Equal((800_000L, 800_000L), (b.MoraleBefore, b.MoraleAfter));          // not 880,000: combat never raises it
            Assert.Equal(800_000, rig.W.Economy.MoraleOf(0));
        }
    }

    /// <summary>[verification round 2, D-R2-01] The after-battle rule on stand-in battles, exactly: the campaign morale falls by the
    /// battle's net loss on the TacticalResolver's scale (start − end), less the medical restore, never below its value before the
    /// battle is raised, and floored at 0. 2,000 men, campaign morale 800,000 before; lane W's collapse of 50 entities (10 dead). The
    /// round-1 rule gave (end + restore) × 1,000: 900,000; 900,000; 450,000; 830,000; 640,000 and 0 for the six cases; a rule clamped
    /// at the campaign morale ("never above before", the start ignored) gives 800,000; 800,000; 450,000; 800,000; 640,000 and 0.</summary>
    [Theory]
    [InlineData(800L, 900L, 0L, 800_000L)]      // the battle ended above its start: no rise (round 1: 900,000)
    [InlineData(1_000L, 900L, 0L, 700_000L)]    // it began above the campaign morale: its loss of 100 still applies
    [InlineData(500L, 450L, 0L, 750_000L)]      // it began below: only its own loss of 50 applies (not a fall to 450,000)
    [InlineData(800L, 790L, 20L, 800_000L)]     // the restore (40) exceeds the net loss (10): no rise (round 1: 830,000)
    [InlineData(800L, 600L, 20L, 640_000L)]     // the by-hand case: 800 − 600 − 40 = 160 → 640,000
    [InlineData(1_000L, 0L, 0L, 0L)]            // a loss of the whole scale from a higher start: floored at 0, not −200,000
    public void Morale_ABattleLowersCampaignMoraleByItsNetLossOnly(long moraleStart, long moraleEnd, long medics, long expectedAfter)
    {
        long start = SupportRig.WarStart();
        var wars = new[] { WarOf(0, start, start + 4 * Week, 0, 1) };
        var rig = SupportRig.Build(SupportRig.FootArmy(march: false, trainGrain: 50_000_000), wars, 2, start, new[] { new SupportPool(0, medics, 0, 0, 0) },
                                   probes: new[] { start - 1 });
        rig.W.ScheduleBattle(rig.S, new SupportBattle(0, start + 3 * Day, SupportRig.Collapse50(moraleEnd), false, moraleStart, 2_000, 0));
        SupportRig.Drive(rig, start + 3 * Day + 1, t => t == start - 1 ? new[] { SupportRig.Allocate(0, 0, SupportKind.FieldMedical, medics) } : Array.Empty<(int, long, long)>());
        var b = rig.W.BattleRows.Single();
        Assert.Equal(medics == 0 ? 0L : 80L, b.Saved);                                      // coverage 10: restore 40 (Morale_MedicalRestore_ByHand_…)
        Assert.Equal((800_000L, expectedAfter), (b.MoraleBefore, b.MoraleAfter));
        Assert.Equal(expectedAfter, rig.W.Economy.MoraleOf(0));
        Assert.True(b.MoraleAfter <= b.MoraleBefore);
    }

    /// <summary>[verification round 2, D-R2-02, verifier mutant S1] Coverage counts the medical staff actually GRANTED, not the
    /// target (support proposal §3.1, "medical staff × 1000 ÷ men"), and the battle save follows it. A pool of 20 against a target of
    /// 60: 20 granted, coverage 10 (not 30); the save of 400 dead is floor(400 × 400,000 × 10 ÷ (1,000,000 × 20)) = 80 (at 30 it would
    /// be 120).</summary>
    [Fact]
    public void Medical_CoverageIsTheStaffGranted_NotTheTarget_AndTheSaveFollowsIt()
    {
        long start = SupportRig.WarStart();
        var wars = new[] { WarOf(0, start, start + 4 * Week, 0, 1) };
        var rig = SupportRig.Build(SupportRig.FootArmy(march: false, trainGrain: 50_000_000), wars, 2, start, new[] { new SupportPool(0, 20, 0, 0, 0) },
                                   probes: new[] { start - 1 });
        rig.W.ScheduleBattle(rig.S, new SupportBattle(0, start + 3 * Day, SupportRig.Collapse50(), false, 800, 2_000, 0));
        SupportRig.Drive(rig, start + 3 * Day + 1, t => t == start - 1 ? new[] { SupportRig.Allocate(0, 0, SupportKind.FieldMedical, 60) } : Array.Empty<(int, long, long)>());
        var latch = rig.W.AllocationRows.Single(r => r.Kind == (byte)SupportKind.FieldMedical);
        Assert.Equal((60L, 100L, 20L, 0L), (latch.Requested, latch.Limit, latch.Granted, latch.PoolAfter));   // the pool binds, not the limit
        Assert.Equal(60, rig.W.TargetOf(0, SupportKind.FieldMedical));
        Assert.Equal(10, rig.W.CoverageOf(0));
        var b = rig.W.BattleRows.Single();
        Assert.Equal((10L, 400L, 80L), (b.Coverage, b.DeadBefore, b.Saved));
    }

    /// <summary>[verification round 2, D-R2-02, verifier mutants S2 and S3] The hitch rule (support proposal §4): a support wagon is
    /// granted only with its two draft animals and its driver. Target 10 wagons (limit 200); whichever of the pool's wagons, animals
    /// ÷ 2 and drivers is smallest binds, and what is granted moves with its animals and drivers (staff = drivers here).</summary>
    [Theory]
    [InlineData(10L, 4L, 10L, 2L)]      // the draft animals bind: 4 ÷ 2
    [InlineData(10L, 20L, 3L, 3L)]      // the drivers bind
    [InlineData(10L, 25L, 12L, 10L)]    // the wagons bind: 20 animals and 10 drivers go with them
    public void SupplyTrain_AWagonIsGrantedOnlyWithItsTwoDraftAnimalsAndItsDriver(long wagons, long draft, long drivers, long expected)
    {
        long start = SupportRig.WarStart();
        var wars = new[] { WarOf(0, start, start + 2 * Week, 0, 1) };
        var rig = SupportRig.Build(SupportRig.FootArmy(march: false, trainGrain: 50_000_000), wars, 2, start, new[] { new SupportPool(0, 0, wagons, draft, drivers) },
                                   probes: new[] { start - 1 });
        var t0 = SupportRig.Totals(rig.W.Ledger);
        SupportRig.Drive(rig, start + 3 * Day, t => t == start - 1 ? new[] { SupportRig.Allocate(0, 0, SupportKind.SupplyTrain, 10) } : Array.Empty<(int, long, long)>());
        var row = rig.W.AllocationRows.Single(r => r.Kind == (byte)SupportKind.SupplyTrain);
        Assert.Equal((10L, 200L, expected, wagons - expected), (row.Requested, row.Limit, row.Granted, row.PoolAfter));
        Assert.Equal(expected, rig.W.WagonsOf(0));
        Assert.Equal(new SupportComposition(expected, expected, 2 * expected, 0), rig.W.Economy.SupportOf(0));
        Assert.Equal(t0, SupportRig.Totals(rig.W.Ledger));
    }

    /// <summary>[verification round 2, D-R2-02, verifier mutants S4 and S5] Law 3 in the world, not only in the pure function: every
    /// care settlement covers exactly its own elapsed interval (the first one runs from the battle instant to the pulse end, four
    /// days), and the thousandth-of-a-head carries persist from one settlement to the next (support proposal §3.1). Each row is
    /// recomputed from SupportMath.CareMilli over ITS interval with the carries chained; the run is checked to contain a row that a
    /// fixed week would settle differently and a row that the carries change, so both rules have teeth here.</summary>
    [Fact]
    public void Medical_CareSettlements_AreExactOverTheirElapsedTime_AndCarryTheThousandths()
    {
        long start = SupportRig.WarStart();
        var wars = new[] { WarOf(0, start, start + 30 * Week, 0, 1) };
        var rig = SupportRig.Build(SupportRig.FootArmy(march: false, trainGrain: 900_000_000), wars, 2, start, new[] { new SupportPool(0, 20, 0, 0, 0) },
                                   probes: new[] { start - 1 });
        rig.W.ScheduleBattle(rig.S, new SupportBattle(0, start + 3 * Day, SupportRig.Collapse50(), true, 800, 2_000, 0));
        SupportRig.Drive(rig, start + 30 * Week, t => t == start - 1 ? new[] { SupportRig.Allocate(0, 0, SupportKind.FieldMedical, 20) } : Array.Empty<(int, long, long)>());
        var rates = SupportRates.Proposal();
        var rows = rig.W.CareRows;
        Assert.True(rows.Count >= 20);
        Assert.Equal(start + 3 * Day, rows[0].FromMs);
        long recCarry = 0, dieCarry = 0;
        bool carryMatters = false, intervalMatters = false;
        for (int i = 0; i < rows.Count; i++)
        {
            var c = rows[i];
            if (i > 0)
            {
                Assert.Equal(rows[i - 1].ToMs, c.FromMs);
                Assert.Equal(rows[i - 1].WoundedBefore - rows[i - 1].Recovered - rows[i - 1].Died, c.WoundedBefore);
            }
            long dt = c.ToMs - c.FromMs;
            var (recM, dieM) = SupportMath.CareMilli(c.WoundedBefore, c.Coverage, dt, rates);
            long rec = recM + recCarry, die = dieM + dieCarry;
            long recHeads = Math.Min(c.WoundedBefore, rec / 1000), dieHeads = Math.Min(c.WoundedBefore - recHeads, die / 1000);
            Assert.Equal((recHeads, dieHeads), (c.Recovered, c.Died));
            long recNo = Math.Min(c.WoundedBefore, recM / 1000);
            if ((recNo, Math.Min(c.WoundedBefore - recNo, dieM / 1000)) != (recHeads, dieHeads)) carryMatters = true;
            var (weekRec, _) = SupportMath.CareMilli(c.WoundedBefore, c.Coverage, 7 * Day, rates);
            if (dt != 7 * Day && Math.Abs(weekRec - recM) >= 1000) intervalMatters = true;
            recCarry = rec % 1000;
            dieCarry = die % 1000;
        }
        Assert.True(carryMatters, "some settlement depends on the carried thousandths");
        Assert.True(intervalMatters, "some settlement is shorter than a week by at least a head");
    }

    /// <summary>[verification round 2, D-R2-02, verifier mutants S6 and S7] Refusal codes 5 and 6 (support proposal §2.3): an order
    /// for a naval force (army 1, ten cogs) is refused NavalForce; an order for an army that has disbanded (army 0, 80 % lost in a
    /// battle on day 10, disbanded at the pulse end of day 14) is refused ArmyDisbanded. Neither changes a target or the pool.</summary>
    [Fact]
    public void Allocation_RefusedForANavalForce_AndForADisbandedArmy()
    {
        long start = SupportRig.WarStart();
        var wars = new[] { WarOf(0, start, start + 6 * Week, 0, 1) };
        var setup = SupportRig.FootArmy(false, trainGrain: 50_000_000);
        var fleet = new ArmyComposition(0, 0, 0, 0, 0, 0, 0, 0, false) { Vessel = DomainProfiles.Cog, Hulls = 10 };
        var p0 = setup.Polities[0] with { Capability = new PolityCapability(WarTech.Coinage) { Naval = NavalCapability.Sails } };
        var army1 = new ArmySetup(1, 0, 0, 1, fleet, new Route(new[] { Seg(5_000, TerrainClass.Water) }), 1_000_000, false, 50_000_000, 0, 0,
                                  Array.Empty<(long, long)>());
        setup = setup with { Polities = new[] { p0, setup.Polities[1] }, Armies = new[] { setup.Armies[0], army1 } };
        var rig = SupportRig.Build(setup, wars, 2, start, new[] { new SupportPool(0, 50, 0, 0, 0) }, probes: new[] { start - 1, start + 3 * Week });
        rig.W.ScheduleBattle(rig.S, new SupportBattle(0, start + 10 * Day, new CollapseInput(0, 0, new long[] { 2, 1, 1, 40, 6 }, 100, 1000), false, 800, 2_000, 0));
        SupportRig.Drive(rig, start + 4 * Week, t => t == start - 1 ? new[] { SupportRig.Allocate(0, 1, SupportKind.FieldMedical, 5) }
            : t == start + 3 * Week ? new[] { SupportRig.Allocate(0, 0, SupportKind.FieldMedical, 5) } : Array.Empty<(int, long, long)>());
        Assert.True(setup.Armies[1].Composition.Naval);
        Assert.Equal(ArmyCondition.Disbanded, rig.W.Economy.ConditionOf(0));
        Assert.Equal(new[] { SupportRefusal.NavalForce, SupportRefusal.ArmyDisbanded }, rig.W.OrderRows.Select(o => (SupportRefusal)o.Refusal).ToArray());
        Assert.Equal((0L, 0L), (rig.W.TargetOf(1, SupportKind.FieldMedical), rig.W.TargetOf(0, SupportKind.FieldMedical)));
        Assert.DoesNotContain(rig.W.AllocationRows, r => r.ArmyId == 1);
        Assert.Equal(50, rig.W.PoolMedicsOf(0));
    }

    /// <summary>[verification round 2, D-R2-02, verifier mutant S8] A disbanded army's wounded go home to the civilians (support
    /// proposal §2.3), not to the dead: every person in lane E's dead account is accounted for by a recorded death (lane E's disease
    /// deaths, the battle's dead, the deaths of wounds, famine), while wounded were still in care when the army disbanded.</summary>
    [Fact]
    public void Disbandment_SendsTheWoundedHome_TheDeadAccountHoldsOnlyTheDead()
    {
        long start = SupportRig.WarStart();
        var wars = new[] { WarOf(0, start, start + 10 * Week, 0, 1) };
        var rig = SupportRig.Build(SupportRig.FootArmy(false, trainGrain: 50_000_000), wars, 2, start, new[] { new SupportPool(0, 200, 0, 0, 0) },
                                   probes: new[] { start - 1 });
        rig.W.ScheduleBattle(rig.S, new SupportBattle(0, start + 10 * Day, new CollapseInput(0, 0, new long[] { 2, 1, 1, 40, 6 }, 100, 1000), false, 800, 2_000, 0));
        var t0 = SupportRig.Totals(rig.W.Ledger);
        SupportRig.Drive(rig, start + 10 * Week, t => t == start - 1 ? new[] { SupportRig.Allocate(0, 0, SupportKind.FieldMedical, 90) } : Array.Empty<(int, long, long)>());
        Assert.Equal(ArmyCondition.Disbanded, rig.W.Economy.ConditionOf(0));
        var b = rig.W.BattleRows.Single();
        long recovered = rig.W.CareRows.Sum(c => c.Recovered), diedOfWounds = rig.W.CareRows.Sum(c => c.Died);
        Assert.True(b.Wounded - recovered - diedOfWounds > 0, "wounded are still in care when the army disbands");
        Assert.Equal(0, rig.W.WoundedOf(0));
        long disease = rig.W.Economy.Rows.Sum(r => r.DiseaseDeaths);
        long famine = rig.W.Economy.Events.Where(e => e.Kind is CascadeKind.CivilianFamine or CascadeKind.LocalityFamineDeaths).Sum(e => e.Value);
        Assert.Equal(disease + b.Dead + diedOfWounds + famine, rig.W.Ledger.Balance(rig.W.Economy.DeadAccount));
        Assert.Equal(t0, SupportRig.Totals(rig.W.Ledger));
    }

    /// <summary>[verification round 2, D-R2-02, verifier mutant S10] The in-kind pay term of the cost table (support proposal §5,
    /// "WageInKindFoot (in kind)"): support staff of a polity WITHOUT coinage draw a foot soldier's wage in kind. First pulse of a
    /// halted army, recomputed by hand from lane E's rate with the staff term.</summary>
    [Fact]
    public void SupportCosts_StaffArePaidInKind_WhereThePolityHasNoCoinage()
    {
        long start = SupportRig.WarStart();
        var wars = new[] { WarOf(0, start, start + 2 * Week, 0, 1) };
        var setup = SupportRig.FootArmy(march: false, trainGrain: 50_000_000);
        setup = setup with { Polities = new[] { Polity(0, WarTech.None, 300_000, 2_000_000_000, treasury: 50_000_000), setup.Polities[1] } };
        var rig = SupportRig.Build(setup, wars, 2, start, new[] { new SupportPool(0, 40, 0, 0, 0) }, probes: new[] { start - 1 });
        SupportRig.Drive(rig, start + 2 * Week, t => t == start - 1 ? new[] { SupportRig.Allocate(0, 0, SupportKind.FieldMedical, 40) } : Array.Empty<(int, long, long)>());
        var row = rig.W.Economy.Rows.First(r => r.ArmyId == 0);
        var lr = LogisticsRates.Proposal();
        long w = row.EndMs - row.StartMs, men = 2_000, staff = 40;
        BigInteger d = (BigInteger)SimInstant.MsPerYear * men;
        Assert.Equal((long)(((BigInteger)men * (men * lr.WageInKindFoot) + (BigInteger)staff * lr.WageInKindFoot * men) * w / d), row.PayDue);
        Assert.NotEqual((long)((BigInteger)men * (men * lr.WageInKindFoot) * w / d), row.PayDue);       // the staff's wage is in it
    }

    /// <summary>[verification round 2, D-R2-04] An order log is replayed input: every raw allocation order is refused and recorded,
    /// never thrown. B &lt; 0 is NegativeTarget (checked before the kind; kind byte 0, the raw value as the target); otherwise kind =
    /// ⌊B ÷ 2^32⌋ and target = B mod 2^32, and a kind other than 1 or 2 is UnknownKind (its byte recorded when it fits, else 0); an
    /// A outside 0..2^47 − 1 (Pack's range) is UnknownArmy with polity and army −1. Before round 2, B = 256 × 2^32, any B ≤ −2^32
    /// and A = 2^47 threw an OverflowException out of the world, and code 4 could not be produced. A valid order after them is
    /// accepted and latched: the world runs on.</summary>
    [Fact]
    public void AllocationOrders_AnyRawValue_IsRefusedAndRecorded_TheWorldRunsOn()
    {
        long start = SupportRig.WarStart();
        var wars = new[] { WarOf(0, start, start + 2 * Week, 0, 1) };
        long S = SupportKinds.TargetStride, ok = SupportKinds.Pack(0, 0), med5 = SupportKinds.PackTarget(SupportKind.FieldMedical, 5);
        var cases = new (long A, long B, SupportRefusal Code, int Polity, int Army, byte Kind, long Target)[]
        {
            (ok, -5, SupportRefusal.NegativeTarget, 0, 0, 0, -5),
            (ok, -1, SupportRefusal.NegativeTarget, 0, 0, 0, -1),
            (ok, -S, SupportRefusal.NegativeTarget, 0, 0, 0, -S),
            (ok, -S - 5, SupportRefusal.NegativeTarget, 0, 0, 0, -S - 5),
            (ok, long.MinValue, SupportRefusal.NegativeTarget, 0, 0, 0, long.MinValue),
            (ok, S - 1, SupportRefusal.UnknownKind, 0, 0, 0, S - 1),
            (ok, 255 * S, SupportRefusal.UnknownKind, 0, 0, 255, 0),
            (ok, 256 * S + 7, SupportRefusal.UnknownKind, 0, 0, 0, 7),
            (ok, long.MaxValue, SupportRefusal.UnknownKind, 0, 0, 0, S - 1),
            (-1, med5, SupportRefusal.UnknownArmy, -1, -1, 1, 5),
            (long.MinValue, med5, SupportRefusal.UnknownArmy, -1, -1, 1, 5),
            (1L << 47, med5, SupportRefusal.UnknownArmy, -1, -1, 1, 5),
            (long.MaxValue, med5, SupportRefusal.UnknownArmy, -1, -1, 1, 5),
            ((1L << 47) - 1, med5, SupportRefusal.UnknownArmy, int.MaxValue, 65_535, 1, 5),   // the largest decodable A: no such army
            (SupportKinds.Pack(1, 0), -5, SupportRefusal.NotOwner, 1, 0, 0, -5),               // ownership is checked before the value
            (ok, SupportKinds.PackTarget(SupportKind.FieldMedical, 30), SupportRefusal.None, 0, 0, 1, 30),
        };
        var rig = SupportRig.Build(SupportRig.FootArmy(false, trainGrain: 50_000_000), wars, 2, start, new[] { new SupportPool(0, 50, 0, 0, 0) },
                                   probes: new[] { start - 1 });
        SupportRig.Drive(rig, start + Day, t => t == start - 1 ? cases.Select(c => (SupportKinds.OrderAllocate, c.A, c.B)).ToArray() : Array.Empty<(int, long, long)>());
        Assert.Equal(cases.Length, rig.W.OrderRows.Count);
        for (int i = 0; i < cases.Length; i++)
        {
            var o = rig.W.OrderRows[i];
            var c = cases[i];
            Assert.Equal((c.Code, c.Polity, c.Army, c.Kind, c.Target), ((SupportRefusal)o.Refusal, o.Polity, o.ArmyId, o.Kind, o.Target));
        }
        Assert.Equal(30, rig.W.MedicsOf(0));                                              // only the valid order took effect
    }
}
