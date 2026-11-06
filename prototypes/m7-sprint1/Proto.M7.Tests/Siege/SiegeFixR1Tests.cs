using System.Numerics;
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
/// Lane S verification round 1 (fix r1). One semantic test per reproduced finding (S-V1 … S-V12) and per mechanism the verifier
/// found pinned only by goldens or not at all (S-V3: disease with crowding and starvation, relief containment, sally effects,
/// mining and counter-mining, rationing and morale, breach, repair labour, water minimum and a cut water supply, failed-relief hope,
/// assault losses by arm, evacuation through a corridor). Each test names the finding or the mutant it answers. Some probes are the
/// verifier's (verify-S-r1/VerifySProbeTests.cs), turned into tests.
/// </summary>
public class SiegeFixR1Tests
{
    private readonly ITestOutputHelper _out;
    public SiegeFixR1Tests(ITestOutputHelper output) => _out = output;

    // Early Modern band: 720-day turns, 7-day proposed War Pulses; war start = start − 30 d, so theatre pulses fall on start − 30 + 7k days
    // and the siege's first (truncated) pulse is [start, start + 5 d); B1 = start + 5 d, B2 = start + 12 d.
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

    /// <summary>A resolver stand-in with fixed results per purpose (1 assault, 2 relief, 3 sally, 4 raid); records every call.</summary>
    private sealed class FixedResolver : IAssaultResolver
    {
        public readonly List<AssaultInputs> Calls = new();
        public Func<int, AssaultResult> Result = _ => new AssaultResult(false, Q32.One, Q32.One, Q32.Zero, Q32.Zero);
        public AssaultResult Resolve(in AssaultInputs input, SiegeCoefficients k) { Calls.Add(input); return Result(input.Purpose); }
    }

    private static long[] Arms(SiegeWorld w, int siege = 0)
    {
        var row = SiegeCodec.Sieges(w)[siege];
        var a = new long[4];
        for (int k = 0; k < 4; k++) a[k] = w.Ledger.Balance(row.Accounts[k]);
        return a;
    }

    private static double D(Q32 q) => q.Raw / (double)Q32.OneRaw;

    // ================================================================== S-V1: head losses do not depend on the pulse length

    /// <summary>S-V1 (equation level, two-sided): for constant heads, 1,440 one-minute pulses lose what one one-day pulse loses, to
    /// within the survival product's floor (one 10⁻¹⁸ unit per step, × heads), and 60 days of one-minute pulses lose the 20 heads that 2,000 men at 60 ‰/yr must lose
    /// (lane E's LossMilli floored this to 0 per pulse: 0 heads).</summary>
    [Fact]
    public void LossAtto_IsIndependentOfThePulseLength_ForConstantHeads_TwoSided()
    {
        const long heads = 2_000, rate = 60;
        Int128 minuteLoss = SiegeEquations.LossAtto(heads, rate, rate, HeadcountLoss.Survival(rate, 60_000));
        Int128 dayLoss = SiegeEquations.LossAtto(heads, rate, rate, HeadcountLoss.Survival(rate, Day));
        Int128 dayOfMinutes = minuteLoss * 1_440;
        Int128 tol = (Int128)heads * 1_441;                            // ≤ 10⁻¹⁴ head
        Assert.True(dayOfMinutes <= dayLoss + tol && dayOfMinutes >= dayLoss - tol, $"{dayOfMinutes} vs {dayLoss}");
        Int128 sixtyDays = minuteLoss * 1_440 * 60;
        Assert.Equal(20, (long)((sixtyDays + HeadcountLoss.One / 2) / HeadcountLoss.One));      // 2,000 × 0.06 × 60/360 = 20 heads
        Assert.Equal(0, HeadcountLoss.LossMilli(heads, rate, rate, HeadcountLoss.Survival(rate, 60_000)));   // the old unit: nothing
        // competing flows: the shares of one survival product sum to the whole loss, from below, within one unit per flow
        long[] rates = { 2_000, 30_000, 1_500, 5_000, 1_000 };
        long total = rates.Sum();
        foreach (long dt in new[] { 60_000L, Day, 21 * Day })
        {
            var s = HeadcountLoss.Survival(total, dt);
            Int128 sum = 0;
            foreach (long r in rates) sum += SiegeEquations.LossAtto(1_000, r, total, s);
            Int128 whole = (Int128)1_000 * (HeadcountLoss.One - s);
            Assert.True(sum <= whole && sum >= whole - rates.Length, $"dt={dt}");
        }
    }

    /// <summary>S-V1 (world level; the verifier's probe P5): the rig siege's disease deaths, fire deaths and survivors over 60 days are
    /// the same on War Pulses, 1-hour and 60-second battle pulses (measured equal; asserted within 2 %). Before the fix the 60-second
    /// clock lost nobody (0 disease deaths, 0 killed).</summary>
    [Fact]
    public void HeadLosses_AreTheSameOnEveryClock_InARunningSiege()
    {
        var res = new List<(string Clock, long Disease, long Killed, long Left)>();
        foreach (var clock in new[] { SiegeClock.WarPulse, SiegeClock.Battle(3_600_000), SiegeClock.Battle(60_000) })
        {
            var b = Build(Start, clock: clock, rates: NoDesertion, inst: i => i with { Food = FoodDays(400) });
            RunTo(b, Start + 61 * Day);
            res.Add((clock.Kind == SiegeClockKind.WarPulse ? "war" : $"{clock.BattlePulseMs / 1000} s",
                     b.W.Ledger.Balance(b.W.DiseaseSink), b.W.Ledger.Balance(b.W.KilledSink), b.W.GarrisonOf(0) + b.W.Civilians(0)));
        }
        foreach (var r in res) _out.WriteLine($"{r.Clock}: disease deaths {r.Disease}, killed {r.Killed}, garrison + civilians {r.Left}");
        var war = res[0];
        Assert.True(war.Disease > 100 && war.Killed > 10, "the rig loses people at all");
        foreach (var r in res)
        {
            Assert.InRange(r.Disease, war.Disease * 98 / 100, war.Disease * 102 / 100);
            Assert.InRange(r.Killed, war.Killed - Math.Max(2, war.Killed / 50), war.Killed + Math.Max(2, war.Killed / 50));
            Assert.InRange(r.Left, war.Left - 5, war.Left + 5);
        }
    }

    /// <summary>Found while verifying S-V1's fix (same class, Law 3): the besiegers' reinforcements are split over their arms in the
    /// opening proportions with an exact carry per arm, so the split is the same on War Pulses and on 60-second pulses. Before, the
    /// remainder of every pulse went to the largest arm, so on 60-second pulses (one person per pulse) every reinforcement joined the
    /// melee. Losses are switched off so the arms show the pure inflow. Replaces the tie-dense remainder test (no argmax is left).</summary>
    [Fact]
    public void AttackerReinforcements_AreSplitInTheOpeningProportions_OnEveryClock()
    {
        var k = SiegeCoefficients.Default with
        {
            FireLethalityPermillePerYear = new long[] { 0, 0, 0, 0 }, EngineLethalityPermillePerYear = new long[] { 0, 0, 0, 0 },
            DefenderGunLethalityPermillePerYear = 0, StarvationMaxPermillePerYear = 0, ThirstMaxPermillePerYear = 0,
        };
        long[] open = { 8_000, 2_000, 1_000, 500 };
        const long rate = 36_500;                                     // people per sim-year: ~0.07 per one-minute pulse
        long[] Run(SiegeClock clock)
        {
            var b = Build(Start, clock: clock, homeTroops: 1_000_000, k: k, rates: NoDesertion with { DiseaseBasePermilleYear = 0 },
                          siege: s => s with { Force = AttackerForce.Of(open[0], open[1], open[2], open[3], RangedKind.Bow), AttackerReinforcementPerYear = rate });
            RunTo(b, B2);                                            // pulses end at B2 on every clock here
            return Arms(b.W);
        }
        var war = Run(SiegeClock.WarPulse);
        var minute = Run(SiegeClock.Battle(60_000));
        long n = rate * 12 / 360;                                     // 12 days of inflow (exact: the integrator's carry starts at zero)
        long P = open.Sum();
        _out.WriteLine($"war [{string.Join(",", war)}] 60 s [{string.Join(",", minute)}] inflow {n}");
        // the per-arm carries are exact, so after N people in any number of pulses arm a holds floor(N × p_a / P) of them
        for (int a = 0; a < 4; a++)
        {
            Assert.Equal(open[a] + n * open[a] / P, war[a]);
            Assert.Equal(open[a] + n * open[a] / P, minute[a]);
        }
        Assert.InRange(war.Sum() - P, n - 3, n);                      // what the floors leave (< 4) waits at home
    }

    // ================================================================== S-V2: garrison pay is the integral of the head count

    private sealed class HalfTheGarrison : IAssaultResolver
    {
        public AssaultResult Resolve(in AssaultInputs input, SiegeCoefficients k)
            => new(false, Q32.One, Q32.One, Q.Permille(100), Q.Permille(500));
    }

    /// <summary>S-V2 (the verifier's probes P1b and P1c): pay over the theatre pulse [B1, B2) equals the exact integral of the garrison's
    /// head count × PayFoot when the garrison changes inside the theatre pulse — on daily battle pulses (head count changes at every
    /// siege pulse end; a failed assault at B1 + 2 d kills half the garrison), and on War Pulses split at a corridor's opening instant.
    /// Before the fix the whole interval was paid at the head count at settle time (350 or 349 against 449).</summary>
    [Theory]
    [InlineData(true)]
    [InlineData(false)]
    public void GarrisonPay_IsTheIntegralOfTheHeadCount_WhenTheGarrisonChangesInsideATheatrePulse(bool battleClock)
    {
        var probes = new long[8];
        for (int k = 0; k < 8; k++) probes[k] = B1 + k * Day + 1;
        var b = battleClock
            ? Build(Start, clock: SiegeClock.Battle(Day), stops: true, probes: probes, resolver: new HalfTheGarrison())
            : Build(Start, stops: true, probes: probes, resolver: new HalfTheGarrison(),
                    siege: s => s with { CorridorFromMs = B1 + 2 * Day, CorridorFoodPerYear = 1_000 });
        var g = new long[8]; var sink = new long[8];
        while (true)
        {
            var r = b.R.RunUntil(B2 + 2);
            if (r.Status == RunStatus.ReachedLimit) break;
            int k = Array.IndexOf(probes, r.AtMs);
            if (k >= 0) { g[k] = b.W.GarrisonOf(0); sink[k] = b.W.Ledger.Balance(b.W.UpkeepSink); }
            if (r.AtMs == B1 + Day + 1) b.R.IssueOrder(SiegeKinds.OrderAssault, Pack(1, 0), 800);
        }
        BigInteger num = 0;
        for (int k = 0; k < 7; k++) num += (BigInteger)g[k] * b.W.Rates.PayFoot * Day;
        long exact = (long)(num / SimInstant.MsPerYear);
        long paid = sink[7] - sink[0];
        _out.WriteLine($"battle clock {battleClock}: heads by day [{string.Join(",", g)}]; paid over [B1,B2) {paid}; exact integral {exact}");
        Assert.True(g[1] > 2 * g[2] - 10, "the assault halved the garrison inside the theatre pulse");
        Assert.InRange(paid, exact - 1, exact + 1);
    }

    /// <summary>S-V2, pulse-start actions: an assault or a sally ordered at the siege's first pulse start (Start, inside the theatre
    /// pulse [Start − 2 d, B1)) changes the garrison where no siege pulse end has settled the pay; the pay of [Start − 2 d, Start) is
    /// still at the full 3,000 men. Assault (resolver: half of each formation) → 3,000 × 2 d + 1,500 × 5 d; sally of half the melee
    /// and cavalry (party loss share 1/2 → each sallying formation loses a quarter) → 3,000 × 2 d + 2,500 × 5 d; × PayFoot / year.</summary>
    [Theory]
    [InlineData(SiegeKinds.OrderAssault, 1_500)]
    [InlineData(SiegeKinds.OrderSally, 2_500)]
    public void GarrisonPay_IsSettledBeforeAnAssaultOrASallyChangesTheGarrison(int kind, long after)
    {
        var spy = new FixedResolver { Result = p => new AssaultResult(false, Q32.One, Q32.One, p == 3 ? Q.Permille(500) : Q32.Zero, p == 1 ? Q.Permille(500) : Q32.Zero) };
        var b = Build(Start, stops: true, probes: new[] { Start - 2 * Day + 1, Start, B1 + 1 }, resolver: spy);
        long s0 = -1, s1 = -1;
        Drive(b, B1 + 2, at =>
        {
            if (at == Start - 2 * Day + 1) s0 = b.W.Ledger.Balance(b.W.UpkeepSink);
            if (at == B1 + 1) s1 = b.W.Ledger.Balance(b.W.UpkeepSink);
            return at == Start ? new[] { (kind, Pack(kind == SiegeKinds.OrderAssault ? 1 : 0, 0), 500L) } : None;
        });
        Assert.Single(spy.Calls);
        long pay = b.W.Rates.PayFoot;
        long exact = (long)(((BigInteger)3_000 * 2 * Day + (BigInteger)after * 5 * Day) * pay / SimInstant.MsPerYear);
        _out.WriteLine($"kind {kind}: paid {s1 - s0}, exact {exact}");
        Assert.InRange(s1 - s0, exact - 1, exact + 1);
    }

    /// <summary>S-V2, siege pulse ends: on daily battle pulses an open corridor reinforces the garrison by 200 men at every pulse end
    /// (Evaluate). Pay over the theatre pulse [B1, B2) is the integral of the day-by-day head count, not 7 days at the head count when
    /// the theatre pulse is settled (which was the case before the siege pulse end settled the pay first; mutant F3).</summary>
    [Fact]
    public void GarrisonPay_FollowsTheCorridorsReinforcementsPulseByPulse()
    {
        var probes = new long[8];
        for (int k = 0; k < 8; k++) probes[k] = B1 + k * Day + 1;
        var b = Build(Start, clock: SiegeClock.Battle(Day), stops: true, probes: probes, homeTroops: 1_000_000, rates: NoDesertion,
                      siege: s => s with { CorridorFromMs = Start, ReinforcementPerYear = 200 * 360 });
        var g = new long[8]; var sink = new long[8];
        Drive(b, B2 + 2, at =>
        {
            int k = Array.IndexOf(probes, at);
            if (k >= 0) { g[k] = b.W.GarrisonOf(0); sink[k] = b.W.Ledger.Balance(b.W.UpkeepSink); }
            return None;
        });
        BigInteger num = 0;
        for (int k = 0; k < 7; k++) num += (BigInteger)g[k] * b.W.Rates.PayFoot * Day;
        long exact = (long)(num / SimInstant.MsPerYear);
        long paid = sink[7] - sink[0];
        _out.WriteLine($"heads by day [{string.Join(",", g)}]; paid {paid}; exact {exact}");
        Assert.True(g[6] - g[0] >= 1_100, "the corridor reinforced the garrison inside the theatre pulse");
        Assert.InRange(paid, exact - 1, exact + 1);
    }

    /// <summary>S-V1 on the theatre clock (mutant F10): a garrison's base desertion (lane E: 50 ‰/yr) is not floored away when the
    /// per-pulse loss is below a thousandth of a head. Two men owe 2 × 0.05 = 0.1 head a year (the loss is on whole heads present, the
    /// fraction carried), so the first deserter leaves after 10 years (measured: between days 3,590 and 3,610 after the war start) on
    /// 7-day and on 1-day theatre pulses alike. With the thousandth floor the 7-day clock would lose half the rate (1.94 → 1 per pulse)
    /// and the 1-day clock all of it (0.28 → 0).</summary>
    [Fact]
    public void TheatreDesertion_OfASmallGarrison_IsNotFlooredAway_OnAnyPulseLength()
    {
        foreach (var days in new[] { 7L, 1L })
        {
            var b = Build(Start, noSiege: true, rates: LogisticsRates.Proposal(), schedule: Schedule(new[] { days, days, days, days, days, days, days }),
                          formations: new[] { new FormationSpec(0, 0, FormationFamily.Infantry, 2, 10, 10) { GarrisonedAt = 0 } });
            RunTo(b, WarStart + 3_500 * Day);
            Assert.Equal(2, b.W.GarrisonOf(0));
            RunTo(b, WarStart + 3_700 * Day);
            _out.WriteLine($"{days}-day theatre pulses: garrison {b.W.GarrisonOf(0)}, deserted {b.W.Ledger.Balance(b.W.DesertedSink)}");
            Assert.Equal(1, b.W.Ledger.Balance(b.W.DesertedSink));
        }
    }

    // ================================================================== S-V5 (mutant V8): the arrears clock is interpolated

    /// <summary>S-V5: the treasury covers exactly half of the first settled pay (700 soldier-months over the first 7-day theatre pulse,
    /// 3,000 foot at PayFoot 12): arrears begin at the interpolated instant the treasury ran dry, WarStart + 3.5 days, not at the
    /// settle start. The existing test's treasury was empty from the first instant, so the interpolation was never exercised.</summary>
    [Fact]
    public void Arrears_StartAtTheInterpolatedInstantThePaySourceRanDry()
    {
        var b = Build(Start, noSiege: true, money: 350);
        RunTo(b, WarStart + 7 * Day + 1);
        long due = 3_000L * b.W.Rates.PayFoot * 7 / 360;
        Assert.Equal(700, due);
        Assert.Equal(350, b.W.UpkeepArrearsOf(0));
        Assert.Equal(WarStart + 7 * Day * 350 / 700, b.W.ArrearsSinceOf(0));
        Assert.Equal(WarStart + 3 * Day + Day / 2, b.W.ArrearsSinceOf(0));
    }

    // ================================================================== S-V6: a human ration order is a standing order

    /// <summary>S-V6 (the verifier's probe P2): a human ration order to a defender with an AI doctrine takes effect at the next pulse
    /// start and stays: the doctrine (which would keep full rations with 400 days of food) no longer overwrites it. Control: without the
    /// order the doctrine's ration is 1,000 ‰.</summary>
    [Fact]
    public void AHumanRationOrder_IsAStandingOrder_TheDoctrineNoLongerOverridesIt()
    {
        Built Run(bool order)
        {
            var b = Build(Start, stops: true, probes: new[] { B1 + 1 }, siege: s => s with { DefenderAi = SiegeDoctrine.Default },
                          inst: i => i with { Food = FoodDays(400) });
            Drive(b, B2 + 30 * Day, at => order && at == B1 + 1 ? new[] { (SiegeKinds.OrderRation, Pack(0, 0), 500L) } : None);
            return b;
        }
        var held = Run(true);
        var free = Run(false);
        var l = Assert.Single(held.W.Latches);
        Assert.Equal((B2, 500L), (l.EffectiveMs, l.Value));
        Assert.Equal(Q.Permille(500), held.W.RationOf(0));                                       // still in force 30 days later
        Assert.All(held.W.PulseRecords.Where(r => r.StartMs >= B2), r => Assert.Equal(Q.Permille(500).Raw, r.RationRaw));
        Assert.All(held.W.PulseRecords.Where(r => r.StartMs < B2), r => Assert.Equal(Q32.OneRaw, r.RationRaw));
        Assert.Equal(Q32.One, free.W.RationOf(0));
        Assert.True(SiegeCodec.Sieges(held.W)[0].RationHeld && !SiegeCodec.Sieges(free.W)[0].RationHeld);
    }

    // ================================================================== S-V7: occupation depends on being occupied

    /// <summary>S-V7: the same grievance input on a city held by its own population's polity raises no unrest and no revolt.</summary>
    [Fact]
    public void AnOwnCity_IsNeverRestive_WhateverItsGrievanceInput()
    {
        var forms = new[] { new FormationSpec(0, 0, FormationFamily.Infantry, 100, 40, 40) };
        var b = Build(Start, noSiege: true, formations: forms, inst: i => i with { GrievancePermillePerYear = 2_000 });
        RunTo(b, Start + 300 * Day);
        Assert.False(b.W.IsOccupied(0));
        Assert.Equal(Q32.Zero, b.W.UnrestOf(0));
        Assert.Empty(b.W.Revolts);
    }

    /// <summary>S-V7: capture makes the population occupied. Before the capitulation the city is its population's own and its unrest
    /// stays zero; once control passes to the besieger (no garrison of its own there) the grievance accrues and the city revolts, the
    /// revolt instant after the capture by the same 26 theatre pulses as an unpoliced occupied city.</summary>
    [Fact]
    public void Capture_MakesThePopulationOccupied_AndItsUnrestGrowsFromTheCapture()
    {
        var b = Build(Start, inst: i => i with { Food = FoodDays(5), GrievancePermillePerYear = 2_000 });
        RunTo(b, Start + 600 * Day);
        var cap = Assert.Single(b.W.Captures);
        Assert.Equal(1, b.W.ControllerOf(0));
        Assert.True(b.W.IsOccupied(0));
        var rev = Assert.Single(b.W.Revolts);
        Assert.True(rev.AtMs > cap.AtMs);
        // the first theatre pulse end after the capture starts the clock; the 26th pulse end after it is the revolt (as in the rig test)
        long firstEnd = WarStart + ((cap.AtMs - WarStart) / (7 * Day) + 1) * 7 * Day;
        _out.WriteLine($"captured day {(cap.AtMs - Start) / (double)Day:F1}; revolt day {(rev.AtMs - Start) / (double)Day:F1}");
        Assert.Equal(firstEnd + 25 * 7 * Day, rev.AtMs);
    }

    // ================================================================== S-V9: Invested means any active siege, corridor or not

    /// <summary>S-V9: garrison orders are refused as Invested while a siege is active even with an open corridor (the corridor's own
    /// reinforcement and evacuation are the siege's flows).</summary>
    [Fact]
    public void Invested_RefusesGarrisonOrders_EvenWithAnOpenCorridor()
    {
        var b = Build(Start, stops: true, probes: new[] { B1 }, siege: s => s with { CorridorFromMs = Start, CorridorFoodPerYear = 1_000_000 });
        Drive(b, B1 + Day, at => at == B1 ? new[]
        {
            (SiegeKinds.OrderGarrisonAssign, Pack(0, 2), 0L),
            (SiegeKinds.OrderGarrisonWithdraw, Pack(0, 0), 0L),
        } : None);
        Assert.Equal(new[] { SiegeRefusal.Invested, SiegeRefusal.Invested }, b.W.Refusals.Select(r => r.Reason).ToArray());
    }

    // ================================================================== S-V11: sally delivery pinned at B − 1, B, B + 1

    [Theory]
    [InlineData(-1, 0)]
    [InlineData(0, 0)]
    [InlineData(1, 7)]
    public void SallyOrder_IsResolvedAtTheFirstSiegePulseStartAtOrAfterIssue_ThroughTheSeam(long offset, long extraDays)
    {
        long t = B1 + offset;
        var spy = new FixedResolver();
        var b = Build(Start, stops: true, probes: new[] { t }, resolver: spy);
        Drive(b, B2 + 7 * Day, at => at == t ? new[] { (SiegeKinds.OrderSally, Pack(0, 0), 300L) } : None);
        var call = Assert.Single(spy.Calls);
        Assert.Equal((B1 + extraDays * Day, 3), (call.AtMs, call.Purpose));
        var l = Assert.Single(b.W.Latches);
        Assert.Equal((t, B1 + extraDays * Day, SiegeKinds.OrderSally, 300L), (l.IssuedMs, l.EffectiveMs, l.Kind, l.Value));
        Assert.Equal(B1 + extraDays * Day, Assert.Single(b.W.Battles).AtMs);
    }

    // ================================================================== S-V12: raid state is in the state hash

    [Fact]
    public void AnArrivedRaid_IsInTheStateHash_BeforeItIsResolved()
    {
        // a raid arriving at B1 − 2 d waits for the theatre pulse start at B1; the other arrives after the probe instant B1 − 1 d
        var arrived = Build(Start, noSiege: true, raids: new[] { new RaidSpec(0, 1, 0, B1 - 2 * Day, 500, RangedKind.Bow) });
        var later = Build(Start, noSiege: true, raids: new[] { new RaidSpec(0, 1, 0, B1 + 3 * Day, 500, RangedKind.Bow) });
        RunTo(arrived, B1 - Day);
        RunTo(later, B1 - Day);
        Assert.Empty(arrived.W.RaidRecords);
        Assert.NotEqual(arrived.W.StateHashHex(), later.W.StateHashHex());
        // and two worlds that agree on the raid's state hash equal
        var again = Build(Start, noSiege: true, raids: new[] { new RaidSpec(0, 1, 0, B1 - 2 * Day, 500, RangedKind.Bow) });
        RunTo(again, B1 - Day);
        Assert.Equal(arrived.W.StateHashHex(), again.W.StateHashHex());
    }

    // ================================================================== S-V3 coverage: disease with crowding and starvation

    /// <summary>S-V3 / mutant V2: crowding drives the city's disease. The same garrison and civilians in a city with a quarter of the
    /// housing are sicker after 30 days and lose more to disease.</summary>
    [Fact]
    public void Crowding_RaisesTheCitysDisease_AndItsDeaths()
    {
        Built Run(long housing)
        {
            var b = Build(Start, rates: NoDesertion, inst: i => i with { Housing = housing, Food = FoodDays(400) });
            RunTo(b, Start + 30 * Day + 1);
            return b;
        }
        var roomy = Run(12_000);
        var packed = Run(3_000);
        _out.WriteLine($"prevalence roomy {D(roomy.W.DiseaseOf(0)):F4} packed {D(packed.W.DiseaseOf(0)):F4}; deaths {roomy.W.Ledger.Balance(roomy.W.DiseaseSink)} vs {packed.W.Ledger.Balance(packed.W.DiseaseSink)}");
        Assert.True(packed.W.DiseaseOf(0).Raw > roomy.W.DiseaseOf(0).Raw);
        Assert.True(packed.W.Ledger.Balance(packed.W.DiseaseSink) > roomy.W.Ledger.Balance(roomy.W.DiseaseSink));
    }

    /// <summary>S-V3 / mutant V3: malnutrition raises disease transmission (equation), and a starving city is sicker than a fed one.</summary>
    [Fact]
    public void Starvation_RaisesDiseaseTransmission()
    {
        var k = SiegeCoefficients.Default;
        var fed = SiegeEquations.DiseaseStep(k, Q.Permille(100), Q.Permille(1_500), Q32.One, 7 * Day);
        var starving = SiegeEquations.DiseaseStep(k, Q.Permille(100), Q.Permille(1_500), Q.Permille(400), 7 * Day);
        Assert.True(starving.Raw > fed.Raw);
        Built Run(long ration)
        {
            var b = Build(Start, stops: true, probes: new[] { Start }, rates: NoDesertion, inst: i => i with { Food = FoodDays(400) });
            Drive(b, Start + 40 * Day + 1, at => at == Start ? new[] { (SiegeKinds.OrderRation, Pack(0, 0), ration) } : None);
            return b;
        }
        var full = Run(1_000);
        var hungry = Run(200);
        Assert.True(hungry.W.DiseaseOf(0).Raw > full.W.DiseaseOf(0).Raw);
    }

    // ================================================================== S-V3: rationing lowers morale; a breach lowers morale

    /// <summary>S-V3 / mutant V1: hunger lowers the defenders' morale target (equation, strictly), and in a running siege half rations
    /// leave the garrison's morale lower at day 26 than full rations (the verifier's probe P3).</summary>
    [Fact]
    public void Rationing_LowersTheDefendersMorale()
    {
        var k = SiegeCoefficients.Default;
        Q32 T(long nut) => SiegeEquations.DefenderMoraleTarget(k, Q32.One, Q.Permille(nut), Q.Permille(50), false, Q32.One, Q.Permille(300), Q32.Zero);
        Assert.True(T(500).Raw < T(800).Raw && T(800).Raw < T(1_000).Raw);
        SiegePulseRecord Day26(long ration)
        {
            var b = Build(Start, stops: true, probes: new[] { Start }, inst: i => i with { Food = FoodDays(40) });
            Drive(b, Start + 30 * Day, at => at == Start ? new[] { (SiegeKinds.OrderRation, Pack(0, 0), ration) } : None);
            return b.W.PulseRecords.First(r => r.EndMs >= Start + 26 * Day);
        }
        Assert.True(Day26(500).MoraleRaw < Day26(1_000).MoraleRaw);
    }

    /// <summary>S-V3 / mutant V15: a practicable breach lowers the defenders' morale target by exactly its coefficient.</summary>
    [Fact]
    public void ABreach_LowersTheDefendersMoraleTarget()
    {
        var k = SiegeCoefficients.Default;
        Q32 T(bool breach) => SiegeEquations.DefenderMoraleTarget(k, Q32.One, Q32.One, Q.Permille(50), breach, Q32.One, Q.Permille(300), Q32.Zero);
        Assert.True(T(true).Raw < T(false).Raw);
        Assert.InRange(T(true).Raw, T(false).Mul(Q.Sub(Q32.One, Q.Permille(k.MoraleBreachPermille))).Raw - 4,
                       T(false).Mul(Q.Sub(Q32.One, Q.Permille(k.MoraleBreachPermille))).Raw + 4);
    }

    // ================================================================== S-V3: relief, sally

    /// <summary>S-V3 / mutant V4: the relief army fights only the part of the besiegers' lines that is not kept facing the city: with
    /// the default containment the lines' power in the relief battle is (1 − containment) of what it is with containment 0.</summary>
    [Fact]
    public void ReliefBattle_FacesOnlyThePartOfTheLinesNotKeptFacingTheCity()
    {
        long Defence(long containment)
        {
            var spy = new FixedResolver();
            var b = Build(Start, resolver: spy, k: SiegeCoefficients.Default with { ReliefContainmentPermille = containment },
                          siege: s => s with { Relief = new ReliefSpec(Start + 10 * Day, new long[] { 3_000, 1_000, 1_000, 0 }, RangedKind.Bow) });
            RunTo(b, Start + 30 * Day);
            return Assert.Single(spy.Calls, c => c.Purpose == 2).DefencePowerMilli;
        }
        long none = Defence(0), part = Defence(SiegeCoefficients.Default.ReliefContainmentPermille);
        long expected = none * (1_000 - SiegeCoefficients.Default.ReliefContainmentPermille) / 1_000;
        Assert.InRange(part, expected - 2, expected + 2);
        Assert.True(part < none);
    }

    /// <summary>S-V3 / mutant V5: a SUCCESSFUL sally burns a share of the siege engines (min(1/2, odds × fortune / 4) = 1/4 here),
    /// destroys the mine galleries (mining progress to zero) and sets the approach works back by a quarter; a failed sally does none
    /// of this. Same world up to the sally; the resolver returns no losses either way.</summary>
    [Fact]
    public void ASuccessfulSally_BurnsEngines_DestroysTheMines_AndSetsBackTheApproach()
    {
        Built Run(bool success)
        {
            var spy = new FixedResolver { Result = p => new AssaultResult(p == 3 && success, Q32.One, Q32.One, Q32.Zero, Q32.Zero) };
            var b = Build(Start, stops: true, probes: new[] { B2 }, resolver: spy,
                          siege: s => s with { ApproachWorkEngineerYears = 200, Force = AttackerForce.Of(8_000, 2_000, 1_000, 2_000, RangedKind.Bow, throwers: 40) });
            Drive(b, B2 + 1, at => at == B2 ? new[] { (SiegeKinds.OrderSally, Pack(0, 0), 500L) } : None);
            return b;
        }
        var failed = Run(false);
        var won = Run(true);
        long engines = failed.W.AttackerEngines(0, EngineClass.Thrower);
        _out.WriteLine($"engines {engines} → {won.W.AttackerEngines(0, EngineClass.Thrower)}; mining {D(failed.W.MiningOf(0)):F4} → {D(won.W.MiningOf(0)):F4}; approach {D(failed.W.ApproachOf(0)):F4} → {D(won.W.ApproachOf(0)):F4}");
        Assert.True(failed.W.MiningOf(0).Raw > 0 && failed.W.ApproachOf(0).Raw > Q32.OneRaw / 4, "the galleries and the approach had progressed");
        Assert.Equal(engines - engines / 4, won.W.AttackerEngines(0, EngineClass.Thrower));
        Assert.Equal(engines / 4, won.W.Ledger.Balance(won.W.EquipmentLostSink) - failed.W.Ledger.Balance(failed.W.EquipmentLostSink));
        Assert.Equal(Q32.Zero, won.W.MiningOf(0));
        Assert.Equal(Q.Sub(failed.W.ApproachOf(0), new Q32(Q32.OneRaw / 4)), won.W.ApproachOf(0));
    }

    // ================================================================== S-V3: mining and counter-mining

    /// <summary>S-V3 / mutant V6: the besiegers' miners make progress; the defenders' engineers counter-mine it (×(1 − counter)); a
    /// completed mine drops the wall by its kind's mine damage. Same siege with and without 600 defending engineers (who do not shoot,
    /// so nothing else on the besiegers' side differs).</summary>
    [Fact]
    public void Miners_BreachTheWall_AndCounterMinersSlowThem()
    {
        Built Run(long counterMiners, bool mineable)
        {
            var forms = new List<FormationSpec>
            {
                new(0, 0, FormationFamily.Infantry, 2_000, 10, 10) { GarrisonedAt = 0 },
                new(1, 0, FormationFamily.Ranged, 1_000, 10, 10) { GarrisonedAt = 0, Ranged = RangedKind.Bow },
            };
            if (counterMiners > 0) forms.Add(new(2, 0, FormationFamily.Engineers, counterMiners, 10, 10) { GarrisonedAt = 0 });
            var b = Build(Start, formations: forms.ToArray(), rates: NoDesertion, inst: i => i with { Food = FoodDays(400) },
                          siege: s => s with { Mineable = mineable, Force = AttackerForce.Of(8_000, 2_000, 1_000, 3_000, RangedKind.Bow) });
            RunTo(b, Start + 120 * Day + 1);
            return b;
        }
        var open = Run(0, true);
        var countered = Run(600, true);
        var none = Run(0, false);
        _out.WriteLine($"mines {open.W.MinesOf(0)} (+progress {D(open.W.MiningOf(0)):F3}) vs countered {countered.W.MinesOf(0)} (+{D(countered.W.MiningOf(0)):F3}); wall {D(open.W.WallOf(0)):F3} vs unmined {D(none.W.WallOf(0)):F3}");
        Assert.True(open.W.MinesOf(0) >= 1, "unopposed miners complete mines");
        Assert.True(open.W.MinesOf(0) + D(open.W.MiningOf(0)) > countered.W.MinesOf(0) + D(countered.W.MiningOf(0)), "counter-mining slows them");
        Assert.Equal(0, none.W.MinesOf(0));
        Assert.True(open.W.WallOf(0).Raw < none.W.WallOf(0).Raw, "the mines breached the wall");
    }

    // ================================================================== S-V3: repair needs labour

    /// <summary>S-V3 / mutant V7: the wall is repaired by the defenders' labour. A failed assault at B1 that costs the garrison half
    /// its men leaves less labour and a lower wall at day 40 than the same assault without losses. The besiegers' camp is roomy and
    /// fed, so their machines do the same damage in both runs.</summary>
    [Fact]
    public void WallRepair_NeedsTheDefendersLabour()
    {
        Built Run(long lossPermille)
        {
            var spy = new FixedResolver { Result = _ => new AssaultResult(false, Q32.One, Q32.One, Q32.Zero, Q.Permille(lossPermille)) };
            var b = Build(Start, stops: true, probes: new[] { B1 }, resolver: spy, rates: NoDesertion, inst: i => i with { Food = FoodDays(400) },
                          siege: s => s with { CampCapacity = 1_000_000, Mineable = false });
            Drive(b, Start + 40 * Day + 1, at => at == B1 ? new[] { (SiegeKinds.OrderAssault, Pack(1, 0), 500L) } : None);
            return b;
        }
        var whole = Run(0);
        var halved = Run(500);
        _out.WriteLine($"wall at day 40: full labour {D(whole.W.WallOf(0)):F4}, half the garrison {D(halved.W.WallOf(0)):F4}");
        Assert.True(halved.W.GarrisonOf(0) < whole.W.GarrisonOf(0) * 6 / 10);
        Assert.True(halved.W.WallOf(0).Raw < whole.W.WallOf(0).Raw);
    }

    // ================================================================== S-V3: water

    /// <summary>S-V3 / mutant V11: water is drawn at max(ration, the physiological minimum): at the lowest food ration (150 ‰) the city
    /// still drinks the minimum (600 ‰). Exact over the first pulse [Start, B1) with no inflow (the head count changes only at B1).</summary>
    [Fact]
    public void Water_IsNeverRationedBelowThePhysiologicalMinimum()
    {
        var k = SiegeCoefficients.Default;
        var b = Build(Start, stops: true, probes: new[] { Start, B1 }, inst: i => i with { WaterInflowPerYear = 0 });
        long water0 = b.W.WaterOf(0), atB1 = -1;
        Drive(b, B1 + 1, at =>
        {
            if (at == B1) atB1 = b.W.WaterOf(0);
            return at == Start ? new[] { (SiegeKinds.OrderRation, Pack(0, 0), (long)k.RationMinPermille) } : None;
        });
        Q32 rw = Q.Permille(k.WaterMinRationPermille);
        ExactFraction cg = ExactFraction.Zero, cc = ExactFraction.Zero;
        long drunk = RateIntegrator.IntegrateExact(rw.Scale(3_000 * k.WaterPerPersonYear), 5 * Day, SimInstant.MsPerYear, ref cg)
                   + RateIntegrator.IntegrateExact(rw.Scale(6_000 * k.WaterPerPersonYear), 5 * Day, SimInstant.MsPerYear, ref cc);
        Assert.Equal(water0 - drunk, atB1);
        Assert.True(k.RationMinPermille < k.WaterMinRationPermille);
    }

    /// <summary>S-V3 / mutant V12: a cut water supply stops the cistern's inflow (exact over the first pulse); with a small cistern the
    /// city then dies of thirst.</summary>
    [Fact]
    public void ACutWaterSupply_StopsTheInflow_AndTheCityDiesOfThirst()
    {
        long At(bool cut)
        {
            var b = Build(Start, stops: true, probes: new[] { B1 }, siege: s => s with { WaterCut = cut });
            long v = -1;
            Drive(b, B1 + 1, at => { if (at == B1) v = b.W.WaterOf(0); return None; });
            return v;
        }
        ExactFraction c = ExactFraction.Zero;
        long inflow = RateIntegrator.IntegrateExact(9_000L * 360, 5 * Day, SimInstant.MsPerYear, ref c);
        Assert.Equal(At(false) - inflow, At(true));
        var dry = Build(Start, rates: NoDesertion, inst: i => i with { Water = 9_000L * 10 }, siege: s => s with { WaterCut = true });
        var wet = Build(Start, rates: NoDesertion, inst: i => i with { Water = 9_000L * 10 });
        RunTo(dry, Start + 60 * Day);
        RunTo(wet, Start + 60 * Day);
        Assert.True(dry.W.Ledger.Balance(dry.W.ThirstSink) > 0);
        Assert.Equal(0, wet.W.Ledger.Balance(wet.W.ThirstSink));
    }

    // ================================================================== S-V3 / S-V8: a failed relief

    /// <summary>S-V3 / S-V8 / mutant V10: a beaten relief costs the defenders hope twice over, by design: an immediate fall of morale
    /// (FailedReliefShock) and a lasting fall of the morale TARGET (ReliefHopeLost) for the rest of the siege. Against the same siege
    /// with no relief, the gap right after the battle is the shock, and 100 days later the morale is still lower by about the
    /// target factor (the shock alone would have decayed). The battle has no losses, so the two sieges differ only in morale.</summary>
    [Fact]
    public void AFailedRelief_LowersTheDefendersMorale_AndTheirHopeForTheRestOfTheSiege()
    {
        var k = SiegeCoefficients.Default;
        long relief = Start + 10 * Day;                               // acts at the pulse start B2 = Start + 12 d
        Built Run(bool withRelief)
        {
            var spy = new FixedResolver();
            var b = Build(Start, stops: true, probes: new[] { B2, B2 + 100 * Day }, resolver: spy, rates: NoDesertion, inst: i => i with { Food = FoodDays(400) },
                          siege: s => withRelief ? s with { Relief = new ReliefSpec(relief, new long[] { 500, 0, 0, 0 }, RangedKind.None) } : s);
            return b;
        }
        var beaten = Run(true);
        var alone = Run(false);
        Q32 b2a = default, b2b = default, lateA = default, lateB = default;
        Drive(beaten, B2 + 100 * Day + 1, at => { if (at == B2) b2a = beaten.W.MoraleOf(0); if (at == B2 + 100 * Day) lateA = beaten.W.MoraleOf(0); return None; });
        Drive(alone, B2 + 100 * Day + 1, at => { if (at == B2) b2b = alone.W.MoraleOf(0); if (at == B2 + 100 * Day) lateB = alone.W.MoraleOf(0); return None; });
        _out.WriteLine($"morale at B2: no relief {D(b2b):F4}, beaten relief {D(b2a):F4}; 100 days later {D(lateB):F4} vs {D(lateA):F4}");
        Assert.Equal(Q.Max(Q32.Zero, Q.Sub(b2b, Q.Permille(k.FailedReliefShockPermille))), b2a);
        double ratio = D(lateA) / D(lateB);
        Assert.InRange(ratio, 1 - k.ReliefHopeLostPermille / 1000.0 - 0.05, 1 - k.ReliefHopeLostPermille / 1000.0 + 0.05);
    }

    // ================================================================== S-V3: assault losses by arm

    /// <summary>S-V3 / mutant V13: a failed assault's attacker loss share L falls on the committed melee (L × committed), on the engineers
    /// at L/2 and on the ranged at L/4 (exact against the same siege without the assault, read right after the battle).</summary>
    [Fact]
    public void AssaultLosses_FallOnTheCommittedMelee_TheEngineersAtHalf_AndTheRangedAtAQuarter()
    {
        Q32 L = Q.Permille(200);
        long[] ArmsAfterB1(bool assault)
        {
            var spy = new FixedResolver { Result = _ => new AssaultResult(false, Q32.One, Q32.One, L, Q32.Zero) };
            var b = Build(Start, stops: true, probes: new[] { B1 - 1, B1 }, resolver: spy);
            long[]? arms = null;
            Drive(b, B1 + 1, at =>
            {
                if (at == B1) arms = Arms(b.W);
                return at == B1 - 1 && assault ? new[] { (SiegeKinds.OrderAssault, Pack(1, 0), 600L) } : None;
            });
            Assert.Equal(assault ? 1 : 0, b.W.Battles.Count);
            return arms!;
        }
        var before = ArmsAfterB1(false);                       // the same siege at the same instant without the assault
        var after = ArmsAfterB1(true);
        long committed = Q.Permille(600).Scale(before[0]);
        Assert.Equal(before[0] - L.Scale(committed), after[0]);
        Assert.Equal(before[3] - (long)(((Int128)before[3] * (L.Raw / 2)) >> Q32.FractionBits), after[3]);
        Assert.Equal(before[1] - (long)(((Int128)before[1] * (L.Raw / 4)) >> Q32.FractionBits), after[1]);
        Assert.Equal(before[2], after[2]);                     // the cavalry are not in the storming party's losses
        Assert.True(before[3] > after[3] && before[1] > after[1]);
    }

    // ================================================================== S-V3: evacuation only through an open corridor

    /// <summary>S-V3 / mutant V16: evacuation runs only once the corridor opens (Start + 20 d, a pulse boundary on War Pulses): nobody
    /// has left by day 19; the 6 days [20, 26) evacuate exactly 36,000/yr × 6/360 = 600.</summary>
    [Fact]
    public void Evacuation_StartsOnlyWhenTheCorridorOpens()
    {
        var b = Build(Start, stops: true, probes: new[] { Start + 19 * Day + 1, Start + 26 * Day + 1 },
                      siege: s => s with { CorridorFromMs = Start + 20 * Day, EvacuationPerYear = 36_000 });
        long day19 = -1, day26 = -1;
        Drive(b, Start + 26 * Day + 2, at =>
        {
            if (at == Start + 19 * Day + 1) day19 = b.W.Ledger.Balance(b.W.ElsewhereOf(0));
            if (at == Start + 26 * Day + 1) day26 = b.W.Ledger.Balance(b.W.ElsewhereOf(0));
            return None;
        });
        Assert.Equal(0, day19);
        Assert.Equal(600, day26);
    }

    // ================================================================== S-V10: the endurance half of the rationing trade-off, in the world

    /// <summary>S-V10: the granary lasts strictly longer at a lower ration in a running siege (the equation-level test exercised only the
    /// closed-form helper, which the world does not call): the first pulse end with an empty granary comes later at half rations.</summary>
    [Fact]
    public void Rationing_TheGranaryLastsStrictlyLonger_InARunningSiege()
    {
        long EmptyDay(long ration)
        {
            var k = SiegeCoefficients.Default with { SurrenderHealthPermille = 0, AttackerCollapsePermille = 0 };
            var b = Build(Start, stops: true, probes: new[] { Start }, k: k, rates: NoDesertion, inst: i => i with { Food = FoodDays(30) });
            Drive(b, Start + 120 * Day, at => at == Start ? new[] { (SiegeKinds.OrderRation, Pack(0, 0), ration) } : None);
            var r = b.W.PulseRecords.First(p => p.Food < FoodDays(1));
            return (r.EndMs - Start) / Day;
        }
        long full = EmptyDay(1_000), half = EmptyDay(500);
        _out.WriteLine($"granary empty by day {full} at full rations, {half} at half rations");
        Assert.True(half > full);
        Assert.True(half >= full * 3 / 2);
    }
}
