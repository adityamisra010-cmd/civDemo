using System.Numerics;
using Proto.M7.Events;
using Proto.M7.Fixed;
using Proto.M7.Time;
using Proto.M7.War;
using Proto.M7.War.Siege;
using Proto.M7.WarEcon;
using static Proto.M7.Tests.Siege.SiegeRig;
using static Proto.M7.Tests.TestSupport;

namespace Proto.M7.Tests.Siege;

/// <summary>Lane S garrisons (DIR5 §2.3; C30): assign, withdraw, replace, reinforce with delivery pins; capacity and eligibility
/// refusals; upkeep through the ledger and the realistic consequence of non-payment (WA4); defence against sieges and raids;
/// garrison as occupation force; capture; air defence counted once; personnel conservation.</summary>
public class GarrisonTests
{
    private static readonly long Start = At(1500, 100);
    // Theatre pulses (7 d, Early Modern) fall on Start − 30 + 7k days: B1 = Start + 5 d, B2 = Start + 12 d.
    private static readonly long B1 = Start + 5 * Day, B2 = Start + 12 * Day;
    private static readonly (int, long, long)[] None = Array.Empty<(int, long, long)>();

    private static void Drive(Built b, long limit, Func<long, (int Kind, long A, long B)[]> ordersAt)
    {
        while (true)
        {
            var r = b.R.RunUntil(limit);
            if (r.Status == RunStatus.ReachedLimit) return;
            foreach (var o in ordersAt(r.AtMs)) b.R.IssueOrder(o.Kind, o.A, o.B);
        }
    }

    private static Built Peace(long[]? probes = null, FormationSpec[]? formations = null, Func<InstallationSpec, InstallationSpec>? inst = null,
                               RaidSpec[]? raids = null, long money = 1_000_000_000)
        => Build(Start, stops: probes is not null, probes: probes, noSiege: true, formations: formations, inst: inst, raids: raids, money: money);

    // ------------------------------------------------------------------ delivery pins (WA1, theatre pulse starts)

    [Theory]
    [InlineData(-1, 0)]
    [InlineData(0, 0)]
    [InlineData(1, 7)]
    public void Assign_TakesEffectAtTheFirstTheatrePulseStartAtOrAfterIssue(long offset, long extraDays)
    {
        long t = B1 + offset;
        var b = Peace(new[] { t });
        var people = b.W.Ledger.Total(Commodity.People);
        Drive(b, B2 + Day, at => at == t ? new[] { (SiegeKinds.OrderGarrisonAssign, Pack(0, 2), 0L) } : None);
        var l = Assert.Single(b.W.Latches);
        Assert.Equal((t, B1 + extraDays * Day, SiegeKinds.OrderGarrisonAssign, 2), (l.IssuedMs, l.EffectiveMs, l.Kind, l.TargetId));
        Assert.Equal((FormationState.Garrisoned, 0), (b.W.StateOf(2), b.W.LocationOf(2)));
        Assert.Equal(3_500, b.W.GarrisonOf(0));
        Assert.Equal(people, b.W.Ledger.Total(Commodity.People));
    }

    [Theory]
    [InlineData(-1, 0)]
    [InlineData(0, 0)]
    [InlineData(1, 7)]
    public void Withdraw_TakesEffectAtTheFirstTheatrePulseStartAtOrAfterIssue(long offset, long extraDays)
    {
        long t = B1 + offset;
        var b = Peace(new[] { t });
        Drive(b, B2 + Day, at => at == t ? new[] { (SiegeKinds.OrderGarrisonWithdraw, Pack(0, 1), 0L) } : None);
        var l = Assert.Single(b.W.Latches);
        Assert.Equal(B1 + extraDays * Day, l.EffectiveMs);
        Assert.Equal((FormationState.Mobile, -1, (10L, 10L)), (b.W.StateOf(1), b.W.LocationOf(1), b.W.PositionOf(1)));
        Assert.Equal(2_000, b.W.GarrisonOf(0));
    }

    [Theory]
    [InlineData(-1, 0)]
    [InlineData(0, 0)]
    [InlineData(1, 7)]
    public void Raid_IsResolvedAtTheFirstTheatrePulseStartAtOrAfterArrival(long offset, long extraDays)
    {
        var b = Peace(raids: new[] { new RaidSpec(0, 1, 0, B1 + offset, 500, RangedKind.Bow) });
        RunTo(b, B2 + Day);
        var r = Assert.Single(b.W.RaidRecords);
        Assert.Equal(B1 + extraDays * Day, r.AtMs);
    }

    // ------------------------------------------------------------------ refusals: capacity, eligibility, ownership, presence, state

    [Fact]
    public void IllegalGarrisonOrders_AreRefused_AndChangeNothing()
    {
        var forms = new[]
        {
            new FormationSpec(0, 0, FormationFamily.Infantry, 2_000, 10, 10) { GarrisonedAt = 0 },
            new FormationSpec(1, 0, FormationFamily.Ranged, 1_000, 10, 10) { GarrisonedAt = 0, Ranged = RangedKind.Bow },
            new FormationSpec(2, 0, FormationFamily.Infantry, 7_500, 10, 10),         // too big for the remaining capacity
            new FormationSpec(3, 0, FormationFamily.Naval, 100, 10, 10),              // a city cannot host ships
            new FormationSpec(4, 1, FormationFamily.Infantry, 300, 10, 10),           // owned by polity 1
            new FormationSpec(5, 0, FormationFamily.Infantry, 300, 50, 50),           // not at the city
            new FormationSpec(6, 0, FormationFamily.AirDefence, 200, 10, 10) { Ranged = RangedKind.Rifle },
        };
        var b = Peace(new[] { B1 }, forms);
        string before = b.W.StateHashHex();
        Drive(b, B1 + Day, at => at == B1 ? new[]
        {
            (SiegeKinds.OrderGarrisonAssign, Pack(0, 2), 0L),     // Capacity
            (SiegeKinds.OrderGarrisonAssign, Pack(0, 3), 0L),     // Eligibility
            (SiegeKinds.OrderGarrisonAssign, Pack(0, 4), 0L),     // NotOwner
            (SiegeKinds.OrderGarrisonAssign, Pack(1, 4), 0L),     // NotController (polity 1 does not control the city)
            (SiegeKinds.OrderGarrisonAssign, Pack(0, 5), 0L),     // NotPresent
            (SiegeKinds.OrderGarrisonAssign, Pack(0, 0), 0L),     // AlreadyGarrisoned
            (SiegeKinds.OrderGarrisonWithdraw, Pack(0, 6), 0L),   // NotGarrisoned
            (SiegeKinds.OrderGarrisonAssign, Pack(0, 99), 0L),    // BadTarget
            (SiegeKinds.OrderGarrisonReplace, Pack(0, 1), 2L),    // Capacity on the net: 3,000 − 1,000 + 7,500 > 10,000? no: 9,500 fits → legal
        } : None);
        Assert.Equal(new[] { SiegeRefusal.Capacity, SiegeRefusal.Eligibility, SiegeRefusal.NotOwner, SiegeRefusal.NotController,
                             SiegeRefusal.NotPresent, SiegeRefusal.AlreadyGarrisoned, SiegeRefusal.NotGarrisoned, SiegeRefusal.BadTarget },
                     b.W.Refusals.Select(r => r.Reason).ToArray());
        // the one legal order (the replace) took effect atomically
        Assert.Equal((FormationState.Mobile, FormationState.Garrisoned), (b.W.StateOf(1), b.W.StateOf(2)));
        Assert.Equal(9_500, b.W.GarrisonOf(0));
        Assert.NotEqual(before, b.W.StateHashHex());
    }

    [Fact]
    public void Replace_OverCapacity_IsRefused_AndNeitherFormationMoves()
    {
        var forms = new[]
        {
            new FormationSpec(0, 0, FormationFamily.Infantry, 2_000, 10, 10) { GarrisonedAt = 0 },
            new FormationSpec(1, 0, FormationFamily.Ranged, 1_000, 10, 10) { GarrisonedAt = 0, Ranged = RangedKind.Bow },
            new FormationSpec(2, 0, FormationFamily.Infantry, 8_500, 10, 10),
        };
        var b = Peace(new[] { B1 }, forms);
        Drive(b, B1 + Day, at => at == B1 ? new[] { (SiegeKinds.OrderGarrisonReplace, Pack(0, 1), 2L) } : None);
        Assert.Equal(SiegeRefusal.Capacity, Assert.Single(b.W.Refusals).Reason);
        Assert.Equal((FormationState.Garrisoned, FormationState.Mobile), (b.W.StateOf(1), b.W.StateOf(2)));
        Assert.Equal(3_000, b.W.GarrisonOf(0));
    }

    [Fact]
    public void Reinforce_MovesPeopleThroughTheLedger_SameFamilyOnly_WithinCapacity()
    {
        var b = Peace(new[] { B1 });
        var people = b.W.Ledger.Total(Commodity.People);
        Drive(b, B1 + Day, at => at == B1 ? new[]
        {
            (SiegeKinds.OrderGarrisonReinforce, Pack(0, 0), 2L * SiegeKinds.ReinforceStride + 200),   // infantry 2 → infantry 0: legal
            (SiegeKinds.OrderGarrisonReinforce, Pack(0, 0), 5L * SiegeKinds.ReinforceStride + 50),    // air defence → infantry: refused
            (SiegeKinds.OrderGarrisonReinforce, Pack(0, 0), 2L * SiegeKinds.ReinforceStride + 999),   // more than formation 2 has left
        } : None);
        Assert.Equal((2_200L, 300L), (b.W.Personnel(0), b.W.Personnel(2)));
        Assert.Equal(new[] { SiegeRefusal.Eligibility, SiegeRefusal.BadValue }, b.W.Refusals.Select(r => r.Reason).ToArray());
        Assert.Equal(people, b.W.Ledger.Total(Commodity.People));
    }

    [Fact]
    public void AnInvestedInstallation_AdmitsNoEntryAndNoWithdrawal()
    {
        var b = Build(Start, stops: true, probes: new[] { B1 });
        Drive(b, B1 + Day, at => at == B1 ? new[]
        {
            (SiegeKinds.OrderGarrisonAssign, Pack(0, 2), 0L),
            (SiegeKinds.OrderGarrisonWithdraw, Pack(0, 0), 0L),
            (SiegeKinds.OrderGarrisonReinforce, Pack(0, 0), 2L * SiegeKinds.ReinforceStride + 10),
        } : None);
        Assert.Equal(new[] { SiegeRefusal.Invested, SiegeRefusal.Invested, SiegeRefusal.Invested }, b.W.Refusals.Select(r => r.Reason).ToArray());
    }

    // ------------------------------------------------------------------ air defence: one capability, one state (D-065 R11)

    [Fact]
    public void AirDefence_IsCountedOnce_GarrisonedOrMobile_NeverBoth()
    {
        var b = Peace(new[] { B1 });
        var mobile = b.W.DefenderOf(0);
        Assert.Equal(1_000, mobile.Ranged);                           // formation 5 (air defence, mobile at the city) does not count
        Drive(b, B1 + Day, at => at == B1 ? new[]
        {
            (SiegeKinds.OrderGarrisonAssign, Pack(0, 5), 0L),
            (SiegeKinds.OrderGarrisonAssign, Pack(0, 5), 0L),          // twice: the second is refused
        } : None);
        var g = b.W.DefenderOf(0);
        Assert.Equal(1_200, g.Ranged);                                // counted exactly once
        Assert.Equal(1_000L * 1_000 + 200L * 3_000, g.BowValueMilli + g.GunmenValueMilli);
        Assert.Equal((1_000L, 200L), (g.Bowmen, g.Gunmen));                     // the rifles shoot munitions, the bows missiles
        Assert.Equal(SiegeRefusal.AlreadyGarrisoned, Assert.Single(b.W.Refusals).Reason);
    }

    // ------------------------------------------------------------------ upkeep through the ledger; non-payment (WA4)

    [Fact]
    public void Upkeep_IsExactPerTheatrePulse_AcrossATurnBoundary()
    {
        var b = Peace();
        long warStart = Start - 30 * Day;
        long s = At(1502, 0);                                         // a campaign-turn start (720-day turns from 1400)
        RunTo(b, s + 10 * Day);
        var snap = b.W.TurnSnapshots.First(x => x.AtMs == s);
        // 3,000 garrisoned foot (infantry and bowmen) × lane E's PayFoot (12 soldier-months per man-year, coin: Coinage held), over
        // [warStart, s): an independent BigInteger closed form
        long expected = (long)((BigInteger)3_000 * b.W.Rates.PayFoot * (s - warStart) / SimInstant.MsPerYear);
        Assert.Equal(expected, snap.Balances[b.W.UpkeepSink]);
        Assert.Equal(0, b.W.UpkeepArrearsOf(0));
    }

    // Non-payment (WA4): lane E's arrears clock drives desertion — SiegeLaneEReuseTests.UnpaidPay_RunsUpArrears_...; pay in coin
    // or in kind — SiegeLaneEReuseTests.Pay_IsCoinWithCoinage_...

    // ------------------------------------------------------------------ defence in sieges and against raids

    [Fact]
    public void AGarrison_DefendsInTheSiege_MoreGarrisonMeansMoreDefenceAtTheAssault()
    {
        long Defence(bool reinforced)
        {
            var spy = new CaptureResolver();
            var forms = new[]
            {
                new FormationSpec(0, 0, FormationFamily.Infantry, reinforced ? 2_500 : 2_000, 10, 10) { GarrisonedAt = 0 },
                new FormationSpec(1, 0, FormationFamily.Ranged, 1_000, 10, 10) { GarrisonedAt = 0, Ranged = RangedKind.Bow },
            };
            var b = Build(Start, stops: true, probes: new[] { B1 }, formations: forms, resolver: spy);
            Drive(b, B1 + Day, at => at == B1 ? new[] { (SiegeKinds.OrderAssault, Pack(1, 0), 800L) } : None);
            return Assert.Single(spy.Calls).DefencePowerMilli;
        }
        Assert.True(Defence(true) > Defence(false));
    }

    private sealed class CaptureResolver : IAssaultResolver
    {
        public readonly List<AssaultInputs> Calls = new();
        public AssaultResult Resolve(in AssaultInputs input, SiegeCoefficients k)
        {
            Calls.Add(input);
            return new AssaultResult(false, Q32.One, Q32.One, Q32.Zero, Q32.Zero);
        }
    }

    [Fact]
    public void Raid_IsRepelledByAGarrison_ButPillagesAnUndefendedTown_Conserved()
    {
        // a garrison of 3,000 behind a stone wall repels 5,000 raiders; an unwalled town with its garrison away is pillaged
        var raid = new[] { new RaidSpec(0, 1, 0, B1 - Day, 5_000, RangedKind.Bow) };
        var defended = Peace(raids: raid);
        var undefended = Peace(raids: raid, inst: i => i with { Fort = FortKind.None }, formations: new[]
        {
            new FormationSpec(0, 0, FormationFamily.Infantry, 2_000, 30, 30),
            new FormationSpec(1, 0, FormationFamily.Ranged, 1_000, 30, 30) { Ranged = RangedKind.Bow },
        });
        var food = defended.W.Ledger.Total(Commodity.Grain);
        RunTo(defended, B2);
        RunTo(undefended, B2);
        var d = Assert.Single(defended.W.RaidRecords);
        var u = Assert.Single(undefended.W.RaidRecords);
        Assert.False(d.Success);
        Assert.True(u.Success);
        Assert.Equal(0, d.Pillaged);
        Assert.Equal(FoodDays(120) * 3 / 10, u.Pillaged);                // 30% of the town's food
        Assert.Equal(u.Pillaged, undefended.W.RaiderFood(0));
        Assert.Equal(food, undefended.W.Ledger.Total(Commodity.Grain));
        Assert.True(d.RaiderLosses > 0);
    }

    // ------------------------------------------------------------------ garrison as occupation force

    private static Built Occupied(long garrison)
    {
        var forms = garrison > 0
            ? new[] { new FormationSpec(0, 0, FormationFamily.Infantry, garrison, 10, 10) { GarrisonedAt = 0 } }
            : new[] { new FormationSpec(0, 0, FormationFamily.Infantry, 100, 40, 40) };
        // the population belongs to polity 1 and the city is held by polity 0: occupied (fix r1, S-V7: only an occupied city is restive)
        return Peace(formations: forms, inst: i => i with { GrievancePermillePerYear = 2_000, PopulationPolity = 1 });
    }

    [Fact]
    public void Occupation_AGarrisonSuppressesUnrest_ProportionallyToThePoliceRatio()
    {
        var none = Occupied(0);
        var half = Occupied(60);                                       // 60 per 6,000 = 10 per 1,000: half the 20-per-1,000 ratio
        var full = Occupied(3_000);
        foreach (var x in new[] { none, half, full }) RunTo(x, Start + 300 * Day);
        var r = Assert.Single(none.W.Revolts);
        // unrest grows by OverDt(2,000 ‰/yr, 7 d) per theatre pulse with no garrison; it reaches 1 at the 26th pulse end
        Assert.Equal(Start - 30 * Day + 26 * 7 * Day, r.AtMs);
        Assert.Empty(half.W.Revolts);
        Assert.Empty(full.W.Revolts);
        Assert.True(half.W.UnrestOf(0) > full.W.UnrestOf(0));
        Assert.True(half.W.UnrestOf(0).Raw < Q32.OneRaw);
        Assert.Equal(Q32.Zero, full.W.UnrestOf(0));
    }

    // ------------------------------------------------------------------ capture and personnel conservation

    [Fact]
    public void Capitulation_TakesTheGarrisonPrisoner_PassesControl_AndRefusesLaterOrdersForCapturedFormations()
    {
        // a starving town with no corridor: it capitulates; the garrison becomes the captor's prisoners
        var b = Build(Start, stops: true, inst: i => i with { Food = FoodDays(5) }, probes: new[] { Start + 200 * Day });
        var people = b.W.Ledger.Total(Commodity.People);
        Drive(b, Start + 201 * Day, at => at == Start + 200 * Day ? new[] { (SiegeKinds.OrderGarrisonAssign, Pack(0, 0), 0L) } : None);
        Assert.Equal(SiegeOutcome.Capitulated, b.W.OutcomeOf(0));
        var c = Assert.Single(b.W.Captures);
        Assert.Equal((0, 1), (c.From, c.To));
        Assert.Equal(c.Prisoners, b.W.Ledger.Balance(b.W.PrisonersOf(1)));
        Assert.Equal((FormationState.Captured, 0L), (b.W.StateOf(0), b.W.Personnel(0)));
        Assert.Equal(1, b.W.ControllerOf(0));
        Assert.Equal(SiegeRefusal.BadTarget, b.W.Refusals[^1].Reason);   // a captured formation cannot be re-garrisoned
        Assert.Equal(people, b.W.Ledger.Total(Commodity.People));
    }
}
