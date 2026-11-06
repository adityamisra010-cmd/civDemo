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

/// <summary>Lane S world tests: order delivery pins (±1 ms), the maximum-duration pin across a band edge, exact conservation,
/// the rationing trade-off in a running siege, replay goldens, tie-dense ordering, the clock parameter, the shortfall policy
/// and the land-module seam.</summary>
public class SiegeWorldTests
{
    private readonly ITestOutputHelper _out;
    public SiegeWorldTests(ITestOutputHelper output) => _out = output;

    // Early Modern band: 720-day turns, 7-day proposed War Pulses. War start = start − 30 d, so theatre pulses fall on start − 30 + 7k
    // days and the siege's first (truncated) pulse is [start, start + 5 d); B1 = start + 5 d, B2 = start + 12 d.
    private static readonly long Start = At(1500, 100);
    private static readonly long B1 = Start + 5 * Day, B2 = Start + 12 * Day;

    private sealed class Spy : IAssaultResolver
    {
        public readonly List<AssaultInputs> Calls = new();
        public bool Success;
        public AssaultResult Resolve(in AssaultInputs input, SiegeCoefficients k)
        {
            Calls.Add(input);
            return new AssaultResult(Success, Q32.One, Q32.One, Q.Permille(100), Q.Permille(50));
        }
    }

    /// <summary>Drive a stopping runner to limit, issuing orders at each window it stops at.</summary>
    private static void Drive(Built b, long limit, Func<long, (int Kind, long A, long B)[]> ordersAt)
    {
        while (true)
        {
            var r = b.R.RunUntil(limit);
            if (r.Status == RunStatus.ReachedLimit) return;
            foreach (var o in ordersAt(r.AtMs)) b.R.IssueOrder(o.Kind, o.A, o.B);
        }
    }

    // ------------------------------------------------------------------ 1. order delivery pins (WA1)

    [Theory]
    [InlineData(-1, 1, 0)]   // issued 1 ms before B1 → takes effect at B1 (pulse 1)
    [InlineData(0, 1, 0)]    // issued at B1 → applies to the pulse starting at B1 (WA1: no waiting)
    [InlineData(1, 2, 7)]    // issued 1 ms after B1 → waits for B2
    public void RationOrder_TakesEffectAtTheFirstSiegePulseStartAtOrAfterIssue(long offset, long pulse, long extraDays)
    {
        long t = B1 + offset;
        var b = Build(Start, stops: true, probes: new[] { t });
        Drive(b, B2 + 7 * Day, at => at == t ? new[] { (SiegeKinds.OrderRation, Pack(0, 0), 500L) } : Array.Empty<(int, long, long)>());
        var l = Assert.Single(b.W.Latches);
        Assert.Equal((t, B1 + extraDays * Day, pulse, 500L), (l.IssuedMs, l.EffectiveMs, l.EffectiveIndex, l.Value));
        Assert.Equal(Q.Permille(500), b.W.RationOf(0));
        // Consumption agrees with the pin: B1−1 and B1 eat identically; B1+1 eats a full-ration pulse first (less food left).
        var ref0 = Build(Start, stops: true, probes: new[] { B1 });
        Drive(ref0, B2 + 7 * Day, at => at == B1 ? new[] { (SiegeKinds.OrderRation, Pack(0, 0), 500L) } : Array.Empty<(int, long, long)>());
        if (offset <= 0) Assert.Equal(ref0.W.FoodOf(0), b.W.FoodOf(0));
        else Assert.True(b.W.FoodOf(0) < ref0.W.FoodOf(0));
    }

    [Theory]
    [InlineData(-1, 0)]
    [InlineData(0, 0)]
    [InlineData(1, 7)]
    public void AssaultOrder_IsResolvedAtTheFirstSiegePulseStartAtOrAfterIssue_ThroughTheLandModuleSeam(long offset, long extraDays)
    {
        long t = B1 + offset;
        var spy = new Spy { Success = false };
        var b = Build(Start, stops: true, probes: new[] { t }, resolver: spy);
        Drive(b, B2 + 7 * Day, at => at == t ? new[] { (SiegeKinds.OrderAssault, Pack(1, 0), 800L) } : Array.Empty<(int, long, long)>());
        var call = Assert.Single(spy.Calls);
        Assert.Equal((B1 + extraDays * Day, 1), (call.AtMs, call.Purpose));
        var battle = Assert.Single(b.W.Battles);
        Assert.Equal(B1 + extraDays * Day, battle.AtMs);
        Assert.True(b.W.IsActive(0));
    }

    [Theory]
    [InlineData(-1, 0)]
    [InlineData(0, 0)]
    [InlineData(1, 7)]
    public void LiftOrder_EndsTheSiegeAtTheFirstSiegePulseStartAtOrAfterIssue(long offset, long extraDays)
    {
        long t = B1 + offset;
        var b = Build(Start, stops: true, probes: new[] { t });
        Drive(b, B2 + 14 * Day, at => at == t ? new[] { (SiegeKinds.OrderLift, Pack(1, 0), 0L) } : Array.Empty<(int, long, long)>());
        Assert.Equal((SiegeOutcome.Lifted, B1 + extraDays * Day), (b.W.OutcomeOf(0), b.W.EndOf(0)));
        Assert.Equal(0, b.W.ControllerOf(0));
    }

    [Fact]
    public void SiegeOrders_FromTheWrongSide_OrWithBadValues_AreRecordedRefusals()
    {
        var b = Build(Start, stops: true, probes: new[] { B1 });
        Drive(b, B2, at => at == B1 ? new[]
        {
            (SiegeKinds.OrderRation, Pack(1, 0), 500L),     // the attacker cannot set the defenders' ration
            (SiegeKinds.OrderAssault, Pack(0, 0), 500L),    // the defender cannot assault itself
            (SiegeKinds.OrderRation, Pack(0, 0), 100L),     // below RationMin (150)
            (SiegeKinds.OrderRation, Pack(0, 9), 500L),     // no such siege
        } : Array.Empty<(int, long, long)>());
        Assert.Equal(new[] { SiegeRefusal.WrongSide, SiegeRefusal.WrongSide, SiegeRefusal.BadValue, SiegeRefusal.BadTarget },
                     b.W.Refusals.Select(r => r.Reason).ToArray());
        Assert.Empty(b.W.Latches);
        Assert.Equal(Q32.One, b.W.RationOf(0));
    }

    // ------------------------------------------------------------------ 2. maximum duration (DIR5 §2.2; RDG-2)

    /// <summary>A siege that cannot resolve itself: ample corridor food, no assault (human attacker), no doctrine.</summary>
    private static Built Stalemate(long start, SiegeClock? clock = null, Func<InstallationSpec, InstallationSpec>? inst = null, SiegeCoefficients? k = null,
                                   LogisticsRates? rates = null)
        => Build(start, clock: clock, inst: inst, k: k, rates: rates,
                 siege: s => s with { CorridorFoodPerYear = SiegeScenarios.RationYear(3_000, 6_000), CorridorMissilesPerYear = 1_000L * 360, Mineable = false });

    [Theory]
    [InlineData(-1)]
    [InlineData(0)]
    [InlineData(1)]
    public void MaximumDuration_IsThreeTimesTheStartTurnLength_PinnedAcrossTheIndustrialModernBandEdge(long offset)
    {
        long edge = At(1920, 0);                       // Industrial (360-day turns) → Modern (180-day turns)
        long start = edge + offset;
        long l = offset < 0 ? 360 * Day : 180 * Day;    // the turn containing the start instant fixes L
        long cap = start + 3 * l;
        var b = Stalemate(start);
        RunTo(b, cap - 1);
        Assert.True(b.W.IsActive(0), "active at cap − 1 ms");
        Assert.Equal(cap, b.W.CapOf(0));
        RunTo(b, cap);
        Assert.False(b.W.IsActive(0));
        Assert.Equal((SiegeOutcome.CapLifted, cap), (b.W.OutcomeOf(0), b.W.EndOf(0)));
        Assert.Equal(Cal.TurnOf(new SimInstant(cap)), b.W.Ends[0].Turn);   // the turn in which the siege ends, pinned
        // the pulse in progress at the cap is truncated at the cap
        var last = b.W.PulseRecords[^1];
        Assert.Equal(cap, last.EndMs);
        Assert.True(last.EndMs - last.StartMs <= 3 * Day);
        Assert.Equal(0, b.W.ControllerOf(0));
    }

    [Fact]
    public void MaximumDuration_HoldsOnTheBattlePulseClock_Too()
    {
        long start = At(1950, 10);
        var b = Stalemate(start, SiegeClock.Battle(6 * 3_600_000L));
        long cap = start + 3 * 180 * Day;
        RunTo(b, cap - 1);
        Assert.True(b.W.IsActive(0));
        RunTo(b, cap);
        Assert.Equal((SiegeOutcome.CapLifted, cap), (b.W.OutcomeOf(0), b.W.EndOf(0)));
        Assert.Equal(cap, b.W.PulseRecords[^1].EndMs);
    }

    [Fact]
    public void MaximumDuration_WeakerDefender_SurrendersOnTerms_GarrisonMarchesOut_ControlPasses()
    {
        long start = At(1950, 10);
        // A disloyal city (will 0.35) that neither starves nor breaks: at the cap its health is below the besiegers'. (TEST INPUT: no
        // desertion, so its low morale does not empty it before the cap.)
        var b = Stalemate(start, inst: i => i with { LoyaltyPermille = 350 }, rates: NoDesertion);
        long cap = start + 3 * 180 * Day;
        RunTo(b, cap - 1);
        Assert.True(b.W.IsActive(0));
        RunTo(b, cap);
        Assert.Equal((SiegeOutcome.CapCapitulation, cap), (b.W.OutcomeOf(0), b.W.EndOf(0)));
        Assert.True(b.W.HealthOf(0) < b.W.AttackerHealthOf(0));
        Assert.Equal(1, b.W.ControllerOf(0));
        Assert.Equal((FormationState.Mobile, -1, (11L, 10L)), (b.W.StateOf(0), b.W.LocationOf(0), b.W.PositionOf(0)));
        var c = Assert.Single(b.W.Captures);
        Assert.Equal(0L, c.Prisoners);
        Assert.True(c.MarchedOut > 2_500);
        Assert.Equal(c.MarchedOut, b.W.Personnel(0) + b.W.Personnel(1));   // they keep their people
        Assert.Equal(0, b.W.Ledger.Balance(b.W.PrisonersOf(1)));
    }

    [Fact]
    public void CapRule_LowerHealthYields_EqualHealthDefenderHolds()
    {
        Assert.Equal(SiegeOutcome.CapCapitulation, SiegeWorld.CapOutcome(new Q32(10), new Q32(11)));
        Assert.Equal(SiegeOutcome.CapLifted, SiegeWorld.CapOutcome(new Q32(11), new Q32(11)));
        Assert.Equal(SiegeOutcome.CapLifted, SiegeWorld.CapOutcome(new Q32(12), new Q32(11)));
    }

    // ------------------------------------------------------------------ Law 4: the gunpowder transition keys on capability, not date

    private static (SiegeRegime Regime, long[] Trajectory, SiegeOutcome Outcome, long Days) Trajectory(long start, long bombards, long throwers)
    {
        var b = Build(start, schedule: FlatSchedule(), inst: i => i with { Fort = FortKind.Castle },
                      siege: s => s with { Force = AttackerForce.Of(8_000, 2_000, 1_000, 500, RangedKind.Bow, throwers: throwers, bombards: bombards) });
        RunTo(b, start + 150 * Day);
        var t = b.W.PulseRecords.Select(r => r.WallRaw ^ (r.HealthRaw << 1) ^ (r.EndMs - start)).ToArray();
        return (b.W.RegimeOf(0), t, b.W.OutcomeOf(0), b.W.EndOf(0) < 0 ? -1 : (b.W.EndOf(0) - start) / Day);
    }

    [Fact]
    public void GunpowderTransition_SameCapabilityDifferentDate_SameRegimeAndTrajectory_DifferentCapabilitySameDate_Differs()
    {
        // Bronze/Iron (5-year turns) against Early Modern (2-year turns), flat 7-day pulses, each start 100 days into a turn
        var ancientGuns = Trajectory(At(-1000, 100), bombards: 20, throwers: 0);
        var modernGuns = Trajectory(At(1700, 100), bombards: 20, throwers: 0);
        var modernEngines = Trajectory(At(1700, 100), bombards: 0, throwers: 20);
        Assert.Equal(SiegeRegime.Gunpowder, ancientGuns.Regime);
        Assert.Equal(ancientGuns.Regime, modernGuns.Regime);
        Assert.Equal(ancientGuns.Trajectory, modernGuns.Trajectory);     // a date shift changes nothing
        Assert.Equal((ancientGuns.Outcome, ancientGuns.Days), (modernGuns.Outcome, modernGuns.Days));
        Assert.Equal(SiegeRegime.Classical, modernEngines.Regime);
        Assert.NotEqual(modernGuns.Trajectory, modernEngines.Trajectory); // a capability change changes the siege
    }

    // ------------------------------------------------------------------ 3. conservation (Law 1, exact)

    [Theory]
    [InlineData("vienna")]
    [InlineData("leningrad")]
    [InlineData("sevastopol")]
    [InlineData("constantinople")]
    public void Conservation_EveryCommodity_IsExactAtEveryTurnSnapshotAndAtTheEnd(string key)
    {
        var h = SiegeScenarios.Get(key);
        var sched = ProposedSchedule();
        var w = SiegeHarness.Build(h, Cal, sched, SiegeClock.WarPulse, ShortfallPolicy.ProRata, null, out var s, out long start);
        var opening = Totals(w.Ledger);
        var r = new Runner(s, w, SignificantKinds.None, stopOnSignificant: false);
        r.RunUntil(start + 3 * Cal.TurnLengthMs(Cal.TurnOf(new SimInstant(start))));
        Assert.False(w.IsActive(0));
        Assert.Equal(opening, Totals(w.Ledger));
        Assert.NotEmpty(w.TurnSnapshots);
        foreach (var snap in w.TurnSnapshots) Assert.Equal(opening, Totals(w.Ledger, snap.Balances));
        // people really moved (the test has teeth): deaths, prisoners or evacuees exist
        long moved = w.Ledger.Balance(w.KilledSink) + w.Ledger.Balance(w.StarvedSink) + w.Ledger.Balance(w.DiseaseSink);
        Assert.True(moved > 0);
    }

    [Fact]
    public void TurnStart_SplitsTheSiegePulse_TheSnapshotHoldsExactlyTheConsumptionUpToTheBoundary()
    {
        long s = At(1502, 0);                          // a campaign-turn start; the siege's first pulse [S − 2 d, S + 5 d) straddles it
        var b = Build(s - 2 * Day);
        long opening = b.W.FoodOf(0);
        RunTo(b, s + Day);
        var snap = b.W.TurnSnapshots.First(x => x.AtMs == s);
        // 3,000 garrison (lane E soldier ration, gm/yr) and 6,000 civilians (base ration) over exactly two days, integrated separately
        // from zero carries
        long two = 2 * Day;
        long eaten = (long)((Int128)3_000 * b.W.SoldierRation * two / SimInstant.MsPerYear) + (long)((Int128)6_000 * b.W.Rates.RationBase * two / SimInstant.MsPerYear);
        Assert.Equal(opening - eaten, snap.Balances[b.W.GranaryAccount(0)]);
        Assert.True(b.W.IsActive(0));
        Assert.Empty(b.W.PulseRecords);                // the pulse itself has not ended: only its linear flows were split
    }

    [Fact]
    public void Starvation_DrawsOnTheSettlementsOwnStore_NoSiegeOnlyStock()
    {
        var b = Build(Start, inst: i => i with { Food = FoodDays(10), Civilians = 6_000 }, k: SiegeCoefficients.Default with { SurrenderHealthPermille = 0, AttackerCollapsePermille = 0 });
        int granary = b.W.GranaryAccount(0);
        long before = b.W.Ledger.Balance(granary);
        RunTo(b, Start + 40 * Day);
        Assert.True(b.W.Ledger.Balance(granary) <= 1);                     // the settlement's granary is what ran out (≤ 1 pro-rata leftover)
        Assert.True(b.W.Ledger.Balance(b.W.StarvedSink) > 0);              // and people starved on the food system stand-in
        Assert.True(before > 0);
        // every Grain account is a polity home store, the settlement granary, the besiegers' field granary or a sink (eaten, wages in
        // kind, carriage loss): the siege opened no stock of its own for the defenders.
        int foodAccounts = 0;
        for (int a = 0; a < b.W.Ledger.AccountCount; a++) if (b.W.Ledger.CommodityOf(a) == Commodity.Grain) foodAccounts++;
        Assert.Equal(3 /*homes*/ + 3 /*sinks*/ + 1 /*granary*/ + 1 /*field granary*/, foodAccounts);
    }

    // ------------------------------------------------------------------ 4. rationing trade-off in a running siege (C15)

    private static Built StarvationSiege(long ration)
    {
        var b = Build(Start, stops: true, probes: new[] { Start }, inst: i => i with { Food = FoodDays(40) });
        Drive(b, Start + 400 * Day, at => at == Start ? new[] { (SiegeKinds.OrderRation, Pack(0, 0), ration) } : Array.Empty<(int, long, long)>());
        return b;
    }

    [Fact]
    public void Rationing_LowersSiegeHealthEarly_ButTheCityHoldsStrictlyLonger()
    {
        var full = StarvationSiege(1000);
        var half = StarvationSiege(500);
        // early (day 26): the rationed garrison is weaker and its health lower
        SiegePulseRecord At26(Built b) => b.W.PulseRecords.First(r => r.EndMs >= Start + 26 * Day);
        Assert.True(At26(half).HealthRaw < At26(full).HealthRaw, "rationing lowers siege health while food lasts");
        Assert.True(At26(half).CapabilityMilli < At26(full).CapabilityMilli, "rationing lowers the defence capability");
        // but the full-ration city exhausts its store first and falls first
        Assert.Equal(SiegeOutcome.Capitulated, full.W.OutcomeOf(0));
        Assert.True(half.W.EndOf(0) > full.W.EndOf(0), $"rationed {half.W.EndOf(0) / Day} vs full {full.W.EndOf(0) / Day}");
    }

    // ------------------------------------------------------------------ 5. the shortfall policy is a parameter, never chosen

    [Fact]
    public void ShortfallPolicy_DecidesWhoStarvesWhenTheStoreIsShort_AndIsNotChosenHere()
    {
        var k = SiegeCoefficients.Default with { SurrenderHealthPermille = 0, AttackerCollapsePermille = 0 };
        (long G, long C) Run(ShortfallPolicy p)
        {
            var b = Build(Start, policy: p, k: k, inst: i => i with { Food = FoodDays(10) },
                          siege: s => s with { CorridorFoodPerYear = SiegeScenarios.RationYear(3_000, 6_000) * 4 / 10 });
            var opening = Totals(b.W.Ledger);
            RunTo(b, Start + 150 * Day);
            Assert.Equal(opening, Totals(b.W.Ledger));
            return (b.W.GarrisonOf(0), b.W.Civilians(0));
        }
        var civFirst = Run(ShortfallPolicy.CiviliansFirst);
        var proRata = Run(ShortfallPolicy.ProRata);
        var garFirst = Run(ShortfallPolicy.GarrisonFirst);
        _out.WriteLine($"garrison/civilians after 150 d: civilians-first {civFirst}, pro-rata {proRata}, garrison-first {garFirst}");
        Assert.True(civFirst.G < proRata.G && proRata.G < garFirst.G);
        Assert.True(civFirst.C > proRata.C && proRata.C > garFirst.C);
    }

    // ------------------------------------------------------------------ 6. replay goldens (both clocks)

    // Re-pinned at the merge of dev 9503d2b (commodity codes renumbered; measured: the old codes reproduce the old goldens), then at
    // the lane S resume's lane E reuse (food in gm at lane E's rations, lane E desertion and disease rates, HeadcountLoss, munitions
    // and missiles, the pay table, row version 2): every flow and loss changed by design. Live = replay is asserted first.
    // Re-pinned at fix r1 (2026-11-06; measured in steps): the exact head-loss carry alone (S-V1: carries in 10⁻¹⁸ head; every
    // per-pulse loss below a whole head now accumulates) moved them to War 724764749D8E… / Battle 797CE664B16B…; then the siege row
    // version 3 (S-V6 standing-ration flag, per-arm reinforcement carries), the raid state in the hash (S-V12, a count of zero here)
    // and, on the battle clock, pay settled before every change of the garrison (S-V2) gave War 2CC6A546… / Battle 92E2E46C….
    // Re-pinned at fix r2 (2026-11-06) by the hash FORMAT only, measured: the fix r1 hash format recomputed on the fixed tree (installation
    // rows cut to their old 215 bytes, the kept pulse-record list instead of the digest) still gives 2CC6A546… / 92E2E46C… exactly, so
    // the golden runs behave identically (base desertion off, no grievance, no pay in kind); the move is the installation row version 4
    // (+ the theatre settle instant, S2-V2) and the pulse digest that replaced the kept list in the hash (S2-V5). (Measured before the
    // digest's mixing was switched to CounterRng.Mix for the determinism scan, a change to the digest value only.)
    private const string GoldenWar = "8EF1425A5582F2F104489B3879B8EE12023E898637D4B45B7944F863D3B1B960";
    private const string GoldenBattle = "71DB1CDD16B3F59CF0B39E1BA818260CEFC1BE5D6E9E33F217E4C55F181597CD";

    private static (string Trace, string State, string Windows, SubTurnOrderLog Log) LiveRun(SiegeClock clock, SubTurnOrderLog? replay = null)
    {
        long t1 = Start + 3 * Day, t2 = B1, t3 = B1 + 2 * Day;
        var b = Build(Start, clock: clock, stops: replay is null, probes: replay is null ? new[] { t1, t2, t3 } : null, replayLog: replay,
                      siege: s => s with { Relief = new ReliefSpec(Start + 30 * Day, new long[] { 3_000, 1_000, 2_000, 0 }, RangedKind.Bow) });
        if (replay is not null)
        {
            // a replay schedules the same probes so the windows (world state) are identical; stops are off
            foreach (var t in new[] { t1, t2, t3 }) SiegeWorld.ScheduleProbe(b.S, t);
        }
        long end = Start + 120 * Day;
        if (replay is null)
            Drive(b, end, at => at == t1 ? new[] { (SiegeKinds.OrderRation, Pack(0, 0), 700L), (SiegeKinds.OrderGarrisonAssign, Pack(0, 2), 0L) }
                              : at == t2 ? new[] { (SiegeKinds.OrderSally, Pack(0, 0), 300L) }
                              : at == t3 ? new[] { (SiegeKinds.OrderAssault, Pack(1, 0), 900L) }
                              : Array.Empty<(int, long, long)>());
        else b.R.RunUntil(end);
        return (b.S.Trace.HashHex(), b.W.StateHashHex(), b.R.WindowsHashHex(), b.R.Orders);
    }

    [Theory]
    [InlineData(SiegeClockKind.WarPulse)]
    [InlineData(SiegeClockKind.BattlePulse)]
    public void Replay_ReproducesTheLiveRun_AndMatchesTheGolden(SiegeClockKind kind)
    {
        var clock = kind == SiegeClockKind.WarPulse ? SiegeClock.WarPulse : SiegeClock.Battle(3_600_000);
        var live = LiveRun(clock);
        var rep = LiveRun(clock, live.Log);
        Assert.Equal(live.Trace, rep.Trace);
        Assert.Equal(live.State, rep.State);
        Assert.Equal(live.Windows, rep.Windows);
        _out.WriteLine($"{kind}: state {live.State}");
        Assert.Equal(kind == SiegeClockKind.WarPulse ? GoldenWar : GoldenBattle, live.State);
        // teeth: without the orders the state differs
        var none = LiveRun(clock, new SubTurnOrderLog());
        Assert.NotEqual(live.State, none.State);
    }

    // ------------------------------------------------------------------ 7. tie-dense ordering

    private static SiegeWorld TieWorld(int[] order, out EventScheduler s)
    {
        long start = Start;
        const int n = 6;
        var inst = new InstallationSpec[n];
        var forms = new FormationSpec[n];
        var sieges = new SiegeSpec[n];
        for (int i = 0; i < n; i++)
        {
            inst[i] = new InstallationSpec(i, InstallationKind.City, 0, 10 * i, 0, 5_000)
            { Civilians = 2_000, Food = FoodDays(90, 1_000, 2_000), Water = 3_000L * 90, WaterInflowPerYear = 3_000L * 360, Missiles = 1_000L * 90,
              Fort = FortKind.StoneWall, Housing = 4_000 };
            forms[i] = new FormationSpec(i, 0, FormationFamily.Infantry, 1_000, 10 * i, 0) { GarrisonedAt = i };
            // identical besiegers, coincident pulses, a shared supply line from polity 1's home store that runs short
            sieges[i] = new SiegeSpec(i, 1, i, start, AttackerForce.Of(3_000, 1_000, 0, 0, RangedKind.Bow, throwers: 4))
            { AttackerFood = 0, AttackerSupplyPerYear = SiegeScenarios.RationYear(4_000, 0), CampCapacity = 10_000, AttackerAi = null, DefenderAi = null };
        }
        s = new EventScheduler(start - 30 * Day);
        var w = new SiegeWorld(Cal, ProposedSchedule(), start - 30 * Day, start + 10 * SimInstant.MsPerYear, SiegeClock.WarPulse, SiegeCoefficients.Default,
            Rates, new[] { Powder, Powder }, ShortfallPolicy.ProRata, 5, order.Select(i => inst[i]).ToArray(), order.Select(i => forms[i]).ToArray(),
            order.Select(i => sieges[i]).ToArray(), openingMoney: 1_000_000_000, homeFood: SiegeScenarios.Food(4_000, 0, 60) * 3, homeMunitions: 0, homeMissiles: 0);
        w.Begin(s);
        return w;
    }

    [Fact]
    public void TieDense_CoincidentSieges_SettleInSiegeIdOrder_IndependentOfDeclarationOrder()
    {
        string? first = null;
        foreach (ulong seed in new ulong[] { 1, 2, 3, 4 })
        {
            var perm = seed == 1 ? new[] { 0, 1, 2, 3, 4, 5 } : Permutation(6, seed);
            var w = TieWorld(perm, out var s);
            new Runner(s, w, SignificantKinds.None, false).RunUntil(Start + 40 * Day);
            string h = w.StateHashHex();
            first ??= h;
            Assert.Equal(first, h);
            // the shared store ran short: lower siege ids were supplied first at every coincident instant, so the besiegers'
            // health is non-increasing in siege id (a suffix pattern, never an interleaving), and the shortage really bit
            Assert.Equal(0, w.Ledger.Balance(w.HomeFoodOf(1)));
            for (int i = 1; i < 6; i++) Assert.True(w.AttackerHealthOf(i) <= w.AttackerHealthOf(i - 1), $"siege {i}");
            Assert.True(w.AttackerHealthOf(5) < w.AttackerHealthOf(0), "the shortage actually bit");
        }
    }

    // TieDense_AttackerReinforcementRemainder_GoesToTheLargestArm_LowestIndexOnATie was removed in fix r1: the per-pulse remainder rule
    // (an integer argmax with an index tie-break) is gone; the split is now exact per arm with carries and has no ordering left to tie.
    // Its replacement is SiegeFixR1Tests.AttackerReinforcements_AreSplitInTheOpeningProportions_OnEveryClock.

    // ------------------------------------------------------------------ the AI doctrine and the approach gate (semantic, not pinned)

    private static (SiegeWorld W, long Start) RunScenario(string key)
    {
        var h = SiegeScenarios.Get(key);
        var w = SiegeHarness.Build(h, Cal, ProposedSchedule(), SiegeClock.WarPulse, ShortfallPolicy.ProRata, null, out var s, out long start);
        new Runner(s, w, SignificantKinds.None, stopOnSignificant: false).RunUntil(start + 3 * Cal.TurnLengthMs(Cal.TurnOf(new SimInstant(start))));
        return (w, start);
    }

    [Fact]
    public void ApproachWorks_GateTheAssault_NoAssaultBeforeTheRampIsComplete()
    {
        // Masada: 600 engineer-years of ramp for 2,000 engineers + 5,000 infantry at one tenth ≈ 0.24 sim-years at full capability
        var (w, start) = RunScenario("masada");
        var assaults = w.Battles.Where(b => b.Purpose == 1).ToArray();
        Assert.NotEmpty(assaults);
        Assert.True(assaults[0].AtMs >= start + 60 * Day, $"first assault on day {(assaults[0].AtMs - start) / Day}");
    }

    [Fact]
    public void Doctrine_AssaultsOnlyAtItsOddsThreshold_AndLiftsAtItsOwnHealthThreshold()
    {
        var (c, _) = RunScenario("constantinople");
        var assaults = c.Battles.Where(b => b.Purpose == 1).ToArray();
        Assert.NotEmpty(assaults);
        long min = Q.Permille(SiegeDoctrine.Default.AssaultMinOddsPermille).Raw;
        foreach (var a in assaults) Assert.True(a.OddsRaw >= min, "the doctrine never assaults below its odds threshold");
        // Besiegers with no supply at all (no field granary, no resupply) and an AI attacker: their health falls with hunger and
        // they give up by doctrine (health at most the lift threshold) before they collapse (≤ 0.1)
        var b = Build(Start, siege: s => s with { AttackerFood = 0, AttackerSupplyPerYear = 0, AttackerAi = SiegeDoctrine.Default with { AssaultMinOddsPermille = 1_000_000 } });
        RunTo(b, Start + 360 * Day);
        Assert.Equal(SiegeOutcome.Lifted, b.W.OutcomeOf(0));
        long ha = b.W.AttackerHealthOf(0).Raw;
        Assert.True(ha <= Q.Permille(SiegeDoctrine.Default.LiftHealthPermille).Raw && ha > Q.Permille(SiegeCoefficients.Default.AttackerCollapsePermille).Raw,
                    $"attacker health {ha / (double)Q32.OneRaw:F3}");
        Assert.Empty(b.W.Latches);                                     // a doctrine decision is not an order
    }

    [Fact]
    public void Doctrine_RationsAgainstTheNetDraw_ACorridorThatCoversTheNeed_KeepsFullRations()
    {
        // 10 days of food: without a corridor the AI defender cuts the ration at once; a corridor that brings the full need from
        // the start leaves the net draw at zero, so it keeps full rations (the store never runs down)
        Built Run(long corridor) => Build(Start, inst: i => i with { Food = FoodDays(10) },
            siege: s => s with { DefenderAi = SiegeDoctrine.Default, CorridorFoodPerYear = corridor });
        var shut = Run(0);
        var open = Run(SiegeScenarios.RationYear(3_000, 6_000));
        RunTo(shut, B2);
        RunTo(open, B2);
        Assert.Equal(Q.Permille(SiegeDoctrine.Default.RationLowPermille), shut.W.RationOf(0));
        Assert.Equal(Q32.One, open.W.RationOf(0));
        Assert.True(open.W.FoodOf(0) >= FoodDays(10) - 2, "the inflow kept the store full");
    }

    // ------------------------------------------------------------------ 8. the clock parameter (D-079 Q14 evidence)

    [Fact]
    public void ClockParameter_WarPulseAndBattlePulse_TraceTheSameSiege_AtTheSameInstants_AtAFractionOfTheCost()
    {
        // Constantinople without assaults (a battle's single realisation depends on its instant, which the clock moves; the seed
        // battery compares the distributions): the trajectory is a resolution choice, so both clocks agree at common instants.
        var h = SiegeScenarios.Get("constantinople");
        h = h with { Siege = h.Siege with { AttackerAi = null } };
        var sched = ProposedSchedule();
        var war = SiegeHarness.Build(h, Cal, sched, SiegeClock.WarPulse, ShortfallPolicy.ProRata, null, out var s1, out long start);
        var bat = SiegeHarness.Build(h, Cal, sched, SiegeClock.Battle(3_600_000), ShortfallPolicy.ProRata, null, out var s2, out _);
        new Runner(s1, war, SignificantKinds.None, false).RunUntil(start + 120 * Day);
        new Runner(s2, bat, SignificantKinds.None, false).RunUntil(start + 120 * Day);
        int compared = 0;
        foreach (var w in war.PulseRecords)
        {
            var b = bat.PulseRecords.Single(r => r.EndMs == w.EndMs);
            double tol = 0.03 * Q32.OneRaw;
            Assert.True(Math.Abs(w.WallRaw - b.WallRaw) <= tol, $"wall at day {(w.EndMs - start) / Day}");
            Assert.True(Math.Abs(w.HealthRaw - b.HealthRaw) <= tol, $"health at day {(w.EndMs - start) / Day}");
            Assert.True(Math.Abs(w.Garrison - b.Garrison) <= w.Garrison / 50, $"garrison at day {(w.EndMs - start) / Day}");
            compared++;
        }
        _out.WriteLine($"compared {compared} instants; evaluations war {war.Evaluations}, battle(1 h) {bat.Evaluations}");
        Assert.True(compared >= 10);
        Assert.True(bat.Evaluations >= 100 * war.Evaluations);
    }

    // ------------------------------------------------------------------ 9. the land-module seam (C15: an assault invokes the land module)

    [Fact]
    public void AssaultReliefAndSally_AllGoThroughTheBattleSeam_AndItsResultDecides()
    {
        var spy = new Spy { Success = true };
        long relief = Start + 20 * Day;
        var b = Build(Start, stops: true, probes: new[] { B1, B2 }, resolver: spy,
                      siege: s => s with { Relief = new ReliefSpec(relief, new long[] { 100, 0, 0, 0 }, RangedKind.None) });
        spy.Success = false;
        Drive(b, Start + 40 * Day, at => at == B1 ? new[] { (SiegeKinds.OrderSally, Pack(0, 0), 200L) }
                                        : at == B2 ? new[] { (SiegeKinds.OrderAssault, Pack(1, 0), 500L) }
                                        : Array.Empty<(int, long, long)>());
        Assert.Equal(new[] { 3, 1, 2 }, spy.Calls.Select(c => c.Purpose).ToArray());   // sally at B1, assault at B2, relief at its pulse
        Assert.Equal(new[] { B1, B2, Start + 26 * Day }, spy.Calls.Select(c => c.AtMs).ToArray());
        Assert.True(b.W.IsActive(0));
        // a forced success of the assault storms the city and takes the garrison prisoner
        var spy2 = new Spy { Success = true };
        var c = Build(Start, stops: true, probes: new[] { B1 }, resolver: spy2);
        long g = c.W.GarrisonOf(0);
        Drive(c, B2, at => at == B1 ? new[] { (SiegeKinds.OrderAssault, Pack(1, 0), 500L) } : Array.Empty<(int, long, long)>());
        Assert.Equal((SiegeOutcome.Stormed, B1, 1), (c.W.OutcomeOf(0), c.W.EndOf(0), c.W.ControllerOf(0)));
        var cap = Assert.Single(c.W.Captures);
        Assert.Equal(c.W.Ledger.Balance(c.W.PrisonersOf(1)), cap.Prisoners);
        Assert.True(cap.Prisoners > 0 && cap.Prisoners < g);                                  // the assault's losses came first
        Assert.Equal(FormationState.Captured, c.W.StateOf(0));
    }
}
