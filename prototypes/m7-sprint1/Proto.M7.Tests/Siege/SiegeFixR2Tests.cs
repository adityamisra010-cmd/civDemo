using Proto.M7.Events;
using Proto.M7.Fixed;
using Proto.M7.Time;
using Proto.M7.War;
using Proto.M7.War.Siege;
using Proto.M7.WarEcon;
using Xunit.Abstractions;
using static Proto.M7.Tests.Siege.SiegeRig;
using static Proto.M7.Tests.TestSupport;

namespace Proto.M7.Tests.Siege;

/// <summary>
/// Verification round 2 (fix r2, 2026-11-06; reports/H-S.md §13). Semantic tests for the behaviours the round-2 verifier found pinned
/// only by a golden or not at all (S2-V1: R2-M1 … R2-M10), and for the defects reproduced and fixed in this round: theatre desertion and
/// unrest split at a siege's start and end (S2-V2), a state hash independent of the pulse-recording flag (S2-V5), and pay in kind fed
/// before a siege's flows at every settlement on every clock (S2-V6). Each test names the finding or mutant it answers.
/// </summary>
public class SiegeFixR2Tests
{
    private readonly ITestOutputHelper _out;
    public SiegeFixR2Tests(ITestOutputHelper output) => _out = output;

    // Early Modern band: 720-day turns, 7-day proposed War Pulses; war start = start − 30 d, so theatre pulses end at start − 30 + 7k days
    // (… start − 2 d, B1 = start + 5 d, B2 = start + 12 d …) and the siege's first (truncated) War Pulse is [start, B1).
    private static readonly long Start = At(1500, 100);
    private static readonly long WarStart = Start - 30 * Day;
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

    // ================================================================== S2-V2: theatre desertion and unrest split at a siege's boundaries

    /// <summary>TEST INPUT: desertion is the only loss — lane E's base rate at 1,000 ‰/yr, every other desertion term and disease off.</summary>
    private static readonly LogisticsRates DesertOnly = NoDesertion with { DesertionBasePermilleYear = 1_000, DiseaseBasePermilleYear = 0 };

    /// <summary>TEST INPUT: no fire, starvation or thirst; lines that contain no deserter, so a besieged garrison deserts at exactly the
    /// rate of an unbesieged one.</summary>
    private static readonly SiegeCoefficients DesertionOnlySiege = SiegeCoefficients.Default with
    {
        LinesContainDesertionPermille = 0, FireLethalityPermillePerYear = new long[] { 0, 0, 0, 0 }, EngineLethalityPermillePerYear = new long[] { 0, 0, 0, 0 },
        DefenderGunLethalityPermillePerYear = 0, StarvationMaxPermillePerYear = 0, ThirstMaxPermillePerYear = 0,
    };

    /// <summary>
    /// S2-V2 (and mutant R2-M5): with desertion as the only loss and the same rate inside and outside the siege, a garrison besieged
    /// from start (inside the theatre pulse [start − 2 d, B1)) until a lift must hold exactly the head count of the same garrison never
    /// besieged, at every theatre pulse end: each part of each theatre pulse is charged once, by the clock that held it. On the 1-day
    /// battle clock the lift (ordered at B1 + 2 d + 1 ms) acts at B1 + 3 d, inside the theatre pulse [B1, B2); on War Pulses at B2.
    /// Before fix r2: War Pulses 2,740 / 2,687 against 2,723 / 2,670 at B1 / B2 (the part before the siege start never deserted);
    /// 1-day clock 2,740 / 2,664 (and the besieged part of [B1, B2) charged twice).
    /// </summary>
    [Theory]
    [InlineData(0)]
    [InlineData(1)]
    public void TheatreDesertion_IsChargedOnceOverTime_AcrossASiegesStartAndEnd_OnEveryClock(int battle)
    {
        var clock = battle == 1 ? SiegeClock.Battle(Day) : SiegeClock.WarPulse;
        long liftAt = B1 + 2 * Day + 1;
        long[] ends = { Start - 2 * Day, B1, B2, B2 + 7 * Day, B2 + 14 * Day };
        var probes = ends.Append(liftAt).ToArray();
        long[] Run(bool siege)
        {
            var b = Build(Start, clock: clock, noSiege: !siege, stops: true, rates: DesertOnly, k: DesertionOnlySiege, probes: probes,
                          inst: i => i with { Food = FoodDays(400) });
            var g = new long[ends.Length];
            Drive(b, ends[^1] + 1, at =>
            {
                int ix = Array.IndexOf(ends, at);
                if (ix >= 0) g[ix] = b.W.GarrisonOf(0);
                return siege && at == liftAt ? new[] { (SiegeKinds.OrderLift, Pack(1, 0), 0L) } : None;
            });
            if (siege)
            {
                var e = Assert.Single(b.W.Ends);
                Assert.Equal((SiegeOutcome.Lifted, battle == 1 ? B1 + 3 * Day : B2), (e.Outcome, e.AtMs));
            }
            return g;
        }
        var besieged = Run(true);
        var never = Run(false);
        _out.WriteLine($"{(battle == 1 ? "1-day" : "War Pulse")}: besieged [{string.Join(", ", besieged)}], never besieged [{string.Join(", ", never)}]");
        Assert.Equal(never, besieged);
        Assert.True(never[^1] < never[0] - 100, "desertion happened");
    }

    /// <summary>S2-V2 with S-V2's rule (found by mutant N11 of this round): the theatre desertion of the part of a theatre pulse before a
    /// siege's start changes the garrison at the siege start, so the garrison's pay is settled up to that instant first. Pay in kind (no
    /// Coinage), desertion only at 1,000 ‰/yr: the wages paid by B1 are exactly ⌊wage × Σ heads × span / year⌋ over the spans between the
    /// theatre pulse ends and the siege start, each at the head count that held over it.</summary>
    [Fact]
    public void GarrisonPay_IsSettledAtTheSiegeStart_BeforeThatInstantsTheatreDesertion()
    {
        var caps = new[] { new PolityCapability(WarTech.Gunpowder), Powder, Powder };
        long[] at = { WarStart, WarStart + 7 * Day, WarStart + 14 * Day, WarStart + 21 * Day, Start - 2 * Day, Start, B1 };
        var b = Build(Start, stops: true, caps: caps, rates: DesertOnly, k: DesertionOnlySiege, probes: at[1..], inst: i => i with { Food = FoodDays(400) });
        var heads = new long[at.Length];
        heads[0] = b.W.GarrisonOf(0);
        long paid = -1;
        Drive(b, B1 + 1, t =>
        {
            int ix = Array.IndexOf(at, t);
            if (ix > 0) heads[ix] = b.W.GarrisonOf(0);
            if (t == B1) paid = b.W.Ledger.Balance(b.W.WagesInKindSink);
            return None;
        });
        Int128 sum = 0;
        for (int j = 0; j + 1 < at.Length; j++) sum += (Int128)heads[j] * Rates.WageInKindFoot * (at[j + 1] - at[j]);
        Assert.True(heads[5] < heads[4], "the theatre desertion of [start − 2 d, start) is charged at the siege start");
        Assert.Equal((long)(sum / SimInstant.MsPerYear), paid);
    }

    /// <summary>
    /// S2-V2 (unrest): a capture inside a theatre pulse makes the population occupied from the capture instant, not from that pulse's
    /// start. 1-day battle clock (so the capitulation falls inside a theatre pulse); no garrison of the captor (suppression 0), so the
    /// unrest at the first theatre pulse end after the capture is exactly the grievance over the occupied part of the pulse, and the
    /// revolt comes at the first pulse end at which the occupied time adds up to 1. Before fix r2 the first increment was a whole 7 days.
    /// </summary>
    [Fact]
    public void ACapture_InsideATheatrePulse_CountsOnlyTheOccupiedPartOfThatPulse()
    {
        Built Make() => Build(Start, clock: SiegeClock.Battle(Day), inst: i => i with { Food = FoodDays(5), GrievancePermillePerYear = 2_000 });
        var probe = Make();
        RunTo(probe, Start + 200 * Day);
        long c = Assert.Single(probe.W.Captures).AtMs;
        long into = (c - WarStart) % (7 * Day);
        Assert.NotEqual(0, into);                                                          // the capture is inside a theatre pulse
        long firstEnd = c - into + 7 * Day;
        var b = Make();
        RunTo(b, firstEnd);
        Assert.Equal(1, b.W.ControllerOf(0));
        Assert.Equal(Q.OverDt(2_000, firstEnd - c), b.W.UnrestOf(0));                      // the occupied part only
        Assert.NotEqual(Q.OverDt(2_000, 7 * Day), b.W.UnrestOf(0));
        RunTo(b, Start + 700 * Day);
        long u = Q.OverDt(2_000, firstEnd - c).Raw, step = Q.OverDt(2_000, 7 * Day).Raw, k = 0;
        while (u < Q32.OneRaw) { u += step; k++; }
        _out.WriteLine($"captured day {(c - Start) / (double)Day:F1}; first pulse end {(firstEnd - c) / (double)Day:F1} d later; revolt after {k} more pulses");
        Assert.Equal(firstEnd + k * 7 * Day, Assert.Single(b.W.Revolts).AtMs);
    }

    // ================================================================== S2-V1: behaviours that had no semantic test

    /// <summary>R2-M2: a liberated population calms down even with no garrison to police it (§3, §10.10: "under its own polity the unrest
    /// decays", "a liberation calms it"). Installation 0 is held by polity 1 over a population of polity 0 with a 30-man garrison (a quarter
    /// of the police ratio), so unrest grows; polity 0 besieges and takes it back; from then on the unrest decays at the full decay rate at
    /// every theatre pulse end: u' = u / (1 + decay × 7 d), exactly (the grievance no longer accrues).</summary>
    [Fact]
    public void ALiberation_CalmsTheUnrest_EvenWithNoGarrisonToPoliceIt()
    {
        Built Make() => Build(Start, formations: new[] { new FormationSpec(0, 1, FormationFamily.Infantry, 30, 10, 10) { GarrisonedAt = 0 } },
                              siege: s => s with { Attacker = 0 },
                              inst: i => i with { Controller = 1, PopulationPolity = 0, Food = FoodDays(5), GrievancePermillePerYear = 2_000 });
        var probe = Make();
        Assert.True(probe.W.IsOccupied(0));
        RunTo(probe, Start + 120 * Day);
        var cap = Assert.Single(probe.W.Captures);
        Assert.Equal((1, 0), (cap.From, cap.To));
        long firstEnd = WarStart + ((cap.AtMs - WarStart) / (7 * Day) + 1) * 7 * Day;
        var d = Q.OverDt(SiegeCoefficients.Default.UnrestDecayPermillePerYear, 7 * Day);
        var b = Make();
        RunTo(b, firstEnd);
        Assert.False(b.W.IsOccupied(0));
        Assert.Equal(0, b.W.GarrisonOf(0));                                                  // nobody polices the city now
        var u = b.W.UnrestOf(0);
        Assert.True(u.Raw > 0, "the occupation left unrest behind");
        for (int j = 1; j <= 4; j++)
        {
            RunTo(b, firstEnd + j * 7 * Day);
            var expected = new Q32(FixedMath.MulDivFloor(u.Raw, Q32.OneRaw, Q32.OneRaw + d.Raw));
            Assert.Equal(expected, b.W.UnrestOf(0));
            Assert.True(expected.Raw < u.Raw);
            u = expected;
        }
        Assert.Empty(b.W.Revolts);
    }

    /// <summary>R2-M3: the installation's capacity binds the corridor's reinforcement too (§3): with 1,000 men a day offered through the
    /// corridor and room for 100, the garrison never exceeds the capacity, ends each pulse full, and the men who could not enter stay in
    /// the home stock (people conserved exactly).</summary>
    [Fact]
    public void CorridorReinforcement_StopsAtTheInstallationsCapacity_TheRestStaysAtHome()
    {
        var b = Build(Start, homeTroops: 50_000, inst: i => i with { Capacity = 3_100 },
                      siege: s => s with { CorridorFromMs = Start, ReinforcementPerYear = 360_000 });
        var people = b.W.Ledger.Total(Commodity.People);
        RunTo(b, Start + 40 * Day);
        Assert.True(b.W.IsActive(0));
        Assert.True(b.W.PulseRecords.Count >= 6);
        Assert.All(b.W.PulseRecords, r => Assert.True(r.Garrison <= 3_100, $"garrison {r.Garrison} over capacity at {r.EndMs}"));
        Assert.All(b.W.PulseRecords, r => Assert.Equal(3_100, r.Garrison));                // the offer always exceeds the room: full each pulse
        long entered = 50_000 - b.W.Ledger.Balance(b.W.HomeTroopsOf(0));
        Assert.True(entered < 40 * 1_000 / 10, $"only what fitted entered ({entered})");
        Assert.Equal(people, b.W.Ledger.Total(Commodity.People));
    }

    /// <summary>R2-M4: when control passes by capitulation the old controller's pay arrears are written off (§2.5): the captor's own
    /// garrison, paid in kind, does not desert for the defender's unpaid wages. Polity 0 (Coinage, empty treasury) runs up arrears from the
    /// war start; polity 1 (no Coinage, pays in grain from a full home granary) takes the city and garrisons it with formation 4.</summary>
    [Fact]
    public void ACapture_WritesOffTheOldControllersArrears_TheCaptorsGarrisonDoesNotDesertForThem()
    {
        var caps = new[] { new PolityCapability(WarTech.Coinage | WarTech.Gunpowder), new PolityCapability(WarTech.Gunpowder), Powder };
        var b = Build(Start, stops: true, money: 0, caps: caps, probes: new[] { Start + 3 * Day }, inst: i => i with { Food = FoodDays(5) });
        bool issued = false;
        long arrearsBefore = -1, capAt = -1;
        Drive(b, Start + 300 * Day, at =>
        {
            if (at == Start + 3 * Day) arrearsBefore = b.W.UpkeepArrearsOf(0);
            if (!issued && b.W.Captures.Count == 1)
            {
                issued = true;
                capAt = at;
                return new[] { (SiegeKinds.OrderGarrisonAssign, Pack(1, 4), 0L) };
            }
            return None;
        });
        Assert.True(arrearsBefore > 0, "the defender was in arrears");
        var cap = Assert.Single(b.W.Captures);
        Assert.Equal((SiegeOutcome.Capitulated, 1), (cap.Cause, cap.To));
        var l = Assert.Single(b.W.Latches, x => x.Kind == SiegeKinds.OrderGarrisonAssign);
        Assert.Equal(WarStart + (capAt - WarStart + 7 * Day - 1) / (7 * Day) * 7 * Day, l.EffectiveMs);   // the first theatre pulse start ≥ the order
        Assert.Equal((FormationState.Garrisoned, 0), (b.W.StateOf(4), b.W.LocationOf(4)));
        Assert.Equal((0L, -1L), (b.W.UpkeepArrearsOf(0), b.W.ArrearsSinceOf(0)));
        Assert.Equal(300, b.W.Personnel(4));                                                 // base desertion is off in the rig; no arrears
        Assert.True(b.W.Ledger.Balance(b.W.WagesInKindSink) > 0, "the captor pays its garrison in grain");
    }

    /// <summary>R2-M8: a raid on an installation under siege is not fought (the besiegers' lines are in the way): it is recorded as
    /// unsuccessful with no losses and no pillage, and the besieged city is exactly as it would be without the raid. Control: the same raid
    /// on the same city in peace is fought.</summary>
    [Fact]
    public void ARaid_OnABesiegedInstallation_IsNotFought_AndChangesNothing()
    {
        var raid = new[] { new RaidSpec(0, 2, 0, Start + 10 * Day, 5_000, RangedKind.Bow) };
        var with = Build(Start, raids: raid);
        var without = Build(Start);
        var peace = Build(Start, noSiege: true, raids: raid);
        foreach (var x in new[] { with, without, peace }) RunTo(x, Start + 30 * Day);
        var r = Assert.Single(with.W.RaidRecords);
        Assert.Equal((B2, false, 0L, 0L, 0L), (r.AtMs, r.Success, r.Pillaged, r.RaiderLosses, r.GarrisonLosses));
        Assert.DoesNotContain(with.W.Battles, x => x.Purpose == 4);
        Assert.Equal(5_000, with.W.Raiders(0));
        Assert.Equal((without.W.GarrisonOf(0), without.W.FoodOf(0), without.W.Civilians(0)), (with.W.GarrisonOf(0), with.W.FoodOf(0), with.W.Civilians(0)));
        Assert.Single(peace.W.Battles, x => x.Purpose == 4);
    }

    /// <summary>R2-M9: unpaid pay lowers a besieged garrison's morale (§3: "the unpaid share also lowers siege morale"). Two identical
    /// sieges, one garrison paid and one not; every desertion term is off, so the head counts stay identical and the only difference is the
    /// unpaid share in the morale target.</summary>
    [Fact]
    public void UnpaidPay_LowersTheBesiegedGarrisonsMorale_AndNothingElseDiffers()
    {
        Built Run(long money) { var b = Build(Start, money: money, rates: NoDesertion); RunTo(b, Start + 30 * Day); return b; }
        var paid = Run(1_000_000_000);
        var unpaid = Run(0);
        Assert.True(unpaid.W.UpkeepArrearsOf(0) > 0 && paid.W.UpkeepArrearsOf(0) == 0);
        Assert.True(paid.W.IsActive(0) && unpaid.W.IsActive(0));
        Assert.Equal(paid.W.GarrisonOf(0), unpaid.W.GarrisonOf(0));
        Assert.Equal(paid.W.Civilians(0), unpaid.W.Civilians(0));
        _out.WriteLine($"morale paid {paid.W.MoraleOf(0).Raw / 4294967296.0:F4}, unpaid {unpaid.W.MoraleOf(0).Raw / 4294967296.0:F4}");
        Assert.True(unpaid.W.MoraleOf(0).Raw < paid.W.MoraleOf(0).Raw);
        Assert.All(unpaid.W.PulseRecords.Zip(paid.W.PulseRecords), p => Assert.True(p.First.MoraleRaw <= p.Second.MoraleRaw));
    }

    /// <summary>R2-M10: the corridor's food and munitions flow only from its opening instant (inside a siege pulse, which is split there),
    /// then exactly at their rates: the defender's home stocks are untouched up to the opening and lose exactly ⌊rate × elapsed / year⌋ by
    /// a later pulse end (they feed nothing else: the rig pays in coin).</summary>
    [Fact]
    public void CorridorSupplies_FlowOnlyFromTheOpeningInstant_ThenExactlyAtTheirRates()
    {
        long open = Start + 10 * Day, later = B2 + 7 * Day;
        const long food = 3_600_000, munitions = 36_000;
        var b = Build(Start, stops: true, probes: new[] { open, later },
                      siege: s => s with { CorridorFromMs = open, CorridorFoodPerYear = food, CorridorMunitionsPerYear = munitions });
        long f0 = b.W.Ledger.Balance(b.W.HomeFoodOf(0)), m0 = b.W.Ledger.Balance(b.W.HomeMunitionsOf(0));
        long fOpen = -1, mOpen = -1, fLater = -1, mLater = -1;
        Drive(b, later + 1, at =>
        {
            if (at == open) { fOpen = b.W.Ledger.Balance(b.W.HomeFoodOf(0)); mOpen = b.W.Ledger.Balance(b.W.HomeMunitionsOf(0)); }
            if (at == later) { fLater = b.W.Ledger.Balance(b.W.HomeFoodOf(0)); mLater = b.W.Ledger.Balance(b.W.HomeMunitionsOf(0)); }
            return None;
        });
        Assert.Contains(b.W.PulseRecords, r => r.EndMs == open);                             // the opening is a pulse boundary
        Assert.Equal((f0, m0), (fOpen, mOpen));
        long dt = later - open;
        Assert.Equal(f0 - (long)((Int128)food * dt / SimInstant.MsPerYear), fLater);
        Assert.Equal(m0 - (long)((Int128)munitions * dt / SimInstant.MsPerYear), mLater);
    }

    /// <summary>R2-M1 (and the delivery pins B − 1, B, B + 1 under a doctrine): a human ration order is a standing order whether it latched
    /// at a pulse start (issued at B1 − 1 or B1 + 1) or acted at once (issued exactly at the pulse start B1, the immediate path): the
    /// defenders' doctrine, which rations this city (45 days of food) below full, never sets the ration again. Control: without the order
    /// the doctrine's ration is below full in every pulse from the effective one.</summary>
    [Theory]
    [InlineData(-1L)]
    [InlineData(0L)]
    [InlineData(1L)]
    public void AHumanRationOrder_UnderADoctrine_Stands_WhetherItLatchedOrActedAtOnce(long offset)
    {
        long issue = B1 + offset, effective = offset <= 0 ? B1 : B2;
        Built Run(bool order)
        {
            var b = Build(Start, stops: true, probes: new[] { issue }, siege: s => s with { DefenderAi = SiegeDoctrine.Default },
                          inst: i => i with { Food = FoodDays(45) });
            Drive(b, B2 + 21 * Day, at => order && at == issue ? new[] { (SiegeKinds.OrderRation, Pack(0, 0), 1_000L) } : None);
            return b;
        }
        var held = Run(true);
        var free = Run(false);
        var l = Assert.Single(held.W.Latches);
        Assert.Equal((effective, 1_000L, issue), (l.EffectiveMs, l.Value, l.IssuedMs));
        Assert.True(SiegeCodec.Sieges(held.W)[0].RationHeld);
        var after = held.W.PulseRecords.Where(r => r.StartMs >= effective).ToList();
        Assert.True(after.Count >= 3);
        Assert.All(after, r => Assert.Equal(Q32.OneRaw, r.RationRaw));
        Assert.All(free.W.PulseRecords.Where(r => r.StartMs >= effective), r => Assert.True(r.RationRaw < Q32.OneRaw));
        Assert.All(held.W.PulseRecords.Where(r => r.StartMs < effective), r => Assert.True(r.RationRaw < Q32.OneRaw));
    }

    // ================================================================== S2-V5: the state hash does not depend on the recording flag

    /// <summary>S2-V5: two identical simulations, one keeping its pulse records and one not, have the same state hash; the hash still
    /// covers every pulse (the running digest equals the fold of the kept records), so a different trajectory hashes differently with
    /// recording off.</summary>
    [Fact]
    public void TheStateHash_DoesNotDependOnKeepingPulseRecords_ButCoversEveryPulse()
    {
        var kept = Build(Start);
        var dropped = Build(Start);
        dropped.W.RecordPulses = false;
        var other = Build(Start, inst: i => i with { Food = FoodDays(119) });
        other.W.RecordPulses = false;
        foreach (var x in new[] { kept, dropped, other }) RunTo(x, Start + 60 * Day);
        Assert.NotEmpty(kept.W.PulseRecords);
        Assert.Empty(dropped.W.PulseRecords);
        Assert.Equal(kept.W.StateHashHex(), dropped.W.StateHashHex());
        ulong h = 0;
        foreach (var r in kept.W.PulseRecords) h = SiegeWorld.FoldPulse(h, r);
        Assert.Equal(h, kept.W.PulseDigest);
        Assert.Equal(h, dropped.W.PulseDigest);
        Assert.NotEqual(dropped.W.PulseDigest, other.W.PulseDigest);
        Assert.NotEqual(dropped.W.StateHashHex(), other.W.StateHashHex());
        // the digest is part of the state hash: the same world with only its digest changed hashes differently (mutant N9)
        var field = typeof(SiegeWorld).GetField("_pulseDigest", System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance)!;
        string before = dropped.W.StateHashHex();
        field.SetValue(dropped.W, dropped.W.PulseDigest ^ 1UL);
        Assert.NotEqual(before, dropped.W.StateHashHex());
    }

    // ================================================================== S2-V6: pay in kind before the siege's flows, at every settlement

    /// <summary>
    /// S2-V6: when the polity's home granary feeds both its garrison's wages in kind (no Coinage) and a corridor into the besieged city,
    /// the wages are fed first at every settlement, whichever event settles them: the theatre pulse end (War Pulses), a siege pulse end
    /// (1-day battle pulses) or a campaign-turn start (War Pulses, the turn start inside the first pulse). The granary holds ten days of
    /// wages and the corridor wants a hundred times the wages: the first settlement pays its wages in full and the corridor takes the
    /// rest, so the wages paid are exactly the first settlement's due and the arrears start exactly at that settlement. Before fix r2 the
    /// siege pulse end and the turn start fed the corridor first (1-day clock: wages 0, arrears from the siege start).
    /// </summary>
    [Theory]
    [InlineData(0)]
    [InlineData(1)]
    [InlineData(2)]
    public void PayInKind_IsFedBeforeTheCorridor_AtEverySettlement_OnEveryClock(int site)
    {
        long turn = At(1502, 0);
        long start = site == 2 ? turn - 2 * Day : Start;
        long e1 = site switch { 0 => start + 7 * Day, 1 => start + Day, _ => turn };
        var caps = new[] { new PolityCapability(WarTech.Gunpowder), Powder, Powder };
        long wagesPerYear = 3_000 * Rates.WageInKindFoot;                                  // 2,000 infantry + 1,000 bowmen, lane E's foot wage in kind
        long granary = wagesPerYear * 10 / 360;
        var b = Build(start, warStart: start, clock: site == 1 ? SiegeClock.Battle(Day) : SiegeClock.WarPulse, caps: caps, homeFood: granary,
                      rates: NoDesertion, siege: s => s with { CorridorFromMs = start, CorridorFoodPerYear = wagesPerYear * 100 });
        RunTo(b, start + 20 * Day);
        long due = (long)((Int128)wagesPerYear * (e1 - start) / SimInstant.MsPerYear);
        Assert.Equal(due, b.W.Ledger.Balance(b.W.WagesInKindSink));
        Assert.Equal(e1, b.W.ArrearsSinceOf(0));
        Assert.Equal(0, b.W.Ledger.Balance(b.W.HomeFoodOf(0)));
        Assert.True(b.W.IsActive(0));
    }
}
