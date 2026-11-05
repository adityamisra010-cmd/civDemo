using System.Numerics;
using Proto.M7.Events;
using Proto.M7.Time;
using Proto.M7.War;
using Xunit.Abstractions;
using static Proto.M7.Tests.TestSupport;

namespace Proto.M7.Tests;

/// <summary>
/// Phase C lane T2 (DIR5 §2.4; ADR-061 §3.11): War Pulse sub-stepping across campaign-turn boundaries, locality under fog,
/// per-pulse economics with exact conservation, and the boundary order-delivery semantics.
/// Pulse lengths are the PROPOSED per-band durations (for Director review, NOT decided); rates are test inputs.
/// Closed forms are computed independently with BigInteger (not with ExactFraction or Int128).
/// </summary>
public class WarPulseT2Tests
{
    private readonly ITestOutputHelper _out;
    public WarPulseT2Tests(ITestOutputHelper output) => _out = output;

    private static readonly long Y = SimInstant.MsPerYear;
    private const long Big = 1_000_000_000_000_000;

    /// <summary>PROPOSED War Pulse length in days per shipped band (Neolithic … Information+). For Director review; NOT decided.</summary>
    internal static readonly long[] ProposedPulseDays = { 21, 14, 10, 7, 3, 2, 1 };

    private static long[] ProposedTableMs(CampaignCalendar cal)
    {
        Assert.Equal(ProposedPulseDays.Length, cal.Bands.Count);
        var t = new long[cal.Bands.Count];
        for (int i = 0; i < t.Length; i++) t[i] = ProposedPulseDays[i] * Day;
        return t;
    }

    private sealed record Rig(CampaignCalendar Cal, EventScheduler S, WarTheaterWorld W, Runner R, WarSession Session);

    private static Rig Build(WarSpec[] wars, WarFog? fog = null, SignificantKinds? sig = null, long[]? upkeep = null, int polities = 6,
                             long startMs = 0, long money = Big, long goods = Big, bool stops = true, CampaignCalendar? turnCal = null,
                             (long at, int polity)[]? probes = null, SubTurnOrderLog? replayLog = null)
    {
        var bandCal = ShippedCalendar();
        var cal = turnCal ?? bandCal;
        var sched = new WarPulseSchedule(bandCal, ProposedTableMs(bandCal));
        var s = new EventScheduler(startMs);
        var sk = sig ?? SignificantKinds.None;
        var w = new WarTheaterWorld(cal, sched, polities, wars, fog ?? WarFog.None, sk, upkeep ?? new long[polities], money, goods);
        w.Begin(s);
        foreach (var (at, p) in probes ?? Array.Empty<(long, int)>()) WarTheaterWorld.ScheduleProbe(s, at, p);
        var r = replayLog is null ? new Runner(s, w, sk, stops) : Runner.CreateReplay(s, w, sk, replayLog);
        return new Rig(cal, s, w, r, new WarSession(r, w));
    }

    /// <summary>floor(sum(rate_i * dt_i) / MsPerYear), independently, in BigInteger.</summary>
    private static long Closed(params (long rate, long dtMs)[] parts)
    {
        BigInteger acc = 0;
        foreach (var (rate, dt) in parts) acc += (BigInteger)rate * dt;
        return (long)(acc / Y);
    }

    private static WarSpec War(int id, long start, long stop, int[] parts, long[] supply, long x = 0, long y = 0)
        => new(id, start, stop, parts, x, y, supply);

    private static void AssertConserved(WarTheaterWorld w, int polities, long money, long goods)
    {
        Assert.Equal((Int128)money * polities, w.Ledger.Total(Commodity.Money));
        Assert.Equal((Int128)goods * polities, w.Ledger.Total(Commodity.Goods));
    }

    // ---------------------------------------------------------------- 1. sub-stepping across turn boundaries

    [Fact]
    public void StraddlingPulse_IsSplitAtTheTurnBoundary_AndPartsTelescopeToOne()
    {
        var cal = ShippedCalendar();
        long s1 = cal.TurnStart(1).Ms, w = 21 * Day, start = s1 - 2 * Day;
        var rig = Build(new[] { War(0, start, start + 3 * w, new[] { 0, 1 }, new long[] { 3600, 1234 }) }, upkeep: new long[] { 7, 7, 0, 0, 0, 0 });
        rig.R.RunUntil(start + 3 * w);

        var x = rig.W.SubSteps;
        Assert.Equal(4, x.Count);
        Assert.Equal((0L, 0, 0L, start, s1, true), (x[0].PulseIndex, x[0].Part, x[0].Turn, x[0].StartMs, x[0].EndMs, x[0].EndsAtSplit));
        Assert.Equal(ExactFraction.Of(2, 21), x[0].PulseElapsedAfter);
        Assert.Equal((0L, 1, 1L, s1, start + w, false), (x[1].PulseIndex, x[1].Part, x[1].Turn, x[1].StartMs, x[1].EndMs, x[1].EndsAtSplit));
        Assert.Equal(ExactFraction.One, x[1].PulseElapsedAfter);
        Assert.Equal(ExactFraction.One, x[0].PulseElapsedAfter + ExactFraction.Of(x[1].EndMs - x[1].StartMs, w)); // telescopes
        Assert.Equal(w, (x[0].EndMs - x[0].StartMs) + (x[1].EndMs - x[1].StartMs));                             // no ms lost or doubled
        Assert.Equal(new[] { 2, 1, 1 }, rig.W.PulseRecords.Select(p => p.Parts).ToArray());

        // The boundary snapshot at S1 holds exactly the economics of the part before S1 (the split settles it there).
        var snap = rig.W.TurnSnapshots.Single(t => t.Turn == 1);
        Assert.Equal(s1, snap.AtMs);
        Assert.Equal(Closed((3600, 2 * Day)) + Closed((1234, 2 * Day)), snap.Balances[rig.W.SupplySink]);
        Assert.Equal(20 + 6, snap.Balances[rig.W.SupplySink]);
        // Part 2 carries the exact fractional remainder: the pulse total equals one integration over the whole pulse.
        Assert.Equal(Closed((1234, w)), x[0].SupplyDue - Closed((3600, 2 * Day)) + x[1].SupplyDue - Closed((3600, 19 * Day)));
        Assert.Equal(Closed((3600, w)) + Closed((1234, w)), x[0].SupplyDue + x[1].SupplyDue);
    }

    [Fact]
    public void SubSteps_TileTheWarSpan_EachInsideOneTurn_PerTurnSumsEqualTurnLength()
    {
        var cal = ShippedCalendar();
        // Industrial band (1-year turns, 3-day pulses): 4 turns of war starting mid-turn; 3 does not divide the phase offset.
        long bandStart = cal.Bands[4].StartMs, start = bandStart + 100 * Day + 7 * Day / 3, stop = start + 4 * 360 * Day;
        var rig = Build(new[] { War(0, start, stop, new[] { 0, 1 }, new long[] { 999, 1 }) }, upkeep: new long[] { 13, 17, 0, 0, 0, 0 });
        rig.R.RunUntil(stop + 10 * Day);

        var x = rig.W.SubSteps;
        long prev = start, splits = 0;
        var perTurn = new SortedDictionary<long, long>();
        foreach (var r in x)
        {
            Assert.Equal(prev, r.StartMs);                                  // contiguous: nothing lost, nothing doubled
            Assert.True(r.EndMs <= cal.TurnEnd(r.Turn).Ms && r.StartMs >= cal.TurnStart(r.Turn).Ms);
            perTurn[r.Turn] = (perTurn.TryGetValue(r.Turn, out var v) ? v : 0) + (r.EndMs - r.StartMs);
            if (r.EndsAtSplit) { splits++; Assert.Equal(cal.TurnStart(r.Turn + 1).Ms, r.EndMs); }
            prev = r.EndMs;
        }
        long end = rig.W.PulseRecords[^1].EndMs;
        Assert.Equal(end, prev);
        foreach (var (turn, sum) in perTurn)
            if (cal.TurnStart(turn).Ms >= start && cal.TurnEnd(turn).Ms <= end) Assert.Equal(cal.TurnLengthMs(turn), sum);
        Assert.Equal(4, splits);                                            // one straddle at each of the 4 boundaries crossed
        foreach (var p in rig.W.PulseRecords)
            Assert.Equal(p.EndMs - p.StartMs, x.Where(r => r.PulseIndex == p.Index).Sum(r => r.EndMs - r.StartMs));
    }

    [Fact]
    public void EraChangeMidWar_StraddlingPulseKeepsItsStartBandLength_NextPulsesUseTheNewBand_ConservationExact()
    {
        var cal = ShippedCalendar();
        long edge = cal.Bands[1].StartMs, start = edge - 5 * Day, stop = edge + 60 * Day;
        const long money = 50_000, goods = 1_000_000;
        var rig = Build(new[] { War(0, start, stop, new[] { 0, 1 }, new long[] { 1000, 1234 }) },
                        upkeep: new long[] { 7, 123_456_789, 0, 0, 0, 0 }, money: money, goods: goods);
        rig.R.RunUntil(stop + 30 * Day);

        var p = rig.W.PulseRecords;
        Assert.Equal((start, start + 21 * Day, 2), (p[0].StartMs, p[0].EndMs, p[0].Parts)); // Neolithic length, split at the edge
        for (int i = 1; i < p.Count; i++) Assert.Equal(14 * Day, p[i].EndMs - p[i].StartMs); // Bronze/Iron length afterwards
        Assert.Equal(edge, rig.W.SubSteps[0].EndMs);
        Assert.Equal(ExactFraction.Of(5, 21), rig.W.SubSteps[0].PulseElapsedAfter);

        long end = p[^1].EndMs, T = end - start;
        // Snapshot at the band edge: exactly the economics of the 5 days before it.
        var snap = rig.W.TurnSnapshots.Single(t => t.AtMs == edge);
        Assert.Equal(Closed((1000, 5 * Day)) + Closed((1234, 5 * Day)), snap.Balances[rig.W.SupplySink]);
        // Totals: one integration over the whole span, despite the split and the era change (exact carry, T3).
        Assert.Equal(Closed((1000, T)) + Closed((1234, T)), rig.W.Ledger.Balance(rig.W.SupplySink));
        // Polity 1's upkeep exceeds its treasury: paid is capped, arrears record the rest, money is still conserved.
        long due0 = Closed((7, T)), due1 = Closed((123_456_789, T));
        Assert.True(due1 > money);
        Assert.Equal(due0 + money, rig.W.Ledger.Balance(rig.W.UpkeepSink));
        Assert.Equal(0, rig.W.Ledger.Balance(rig.W.TreasuryOf(1)));
        Assert.Equal(due1 - money, rig.W.UpkeepArrearsOf(0, 1));
        AssertConserved(rig.W, 6, money, goods);
    }

    [Fact]
    public void Conservation_HoldsExactlyAtEverySubStepBoundary()
    {
        var cal = ShippedCalendar();
        long edge = cal.Bands[1].StartMs, start = edge - 40 * Day;
        var rig = Build(new[] { War(0, start, edge + 40 * Day, new[] { 0, 2 }, new long[] { 777, 5 }) }, upkeep: new long[] { 11, 0, 3, 0, 0, 0 },
                        money: 1_000_000, goods: 1_000_000);
        long t = start;
        while (t < edge + 80 * Day)
        {
            t += Day / 2;                                         // step through the run, observing the ledger between events
            rig.R.RunUntil(t);
            AssertConserved(rig.W, 6, 1_000_000, 1_000_000);
            long paid = rig.W.Ledger.Balance(rig.W.SupplySink);
            long settled = rig.W.SubSteps.Count == 0 ? start : rig.W.SubSteps[^1].EndMs;
            Assert.Equal(Closed((777, settled - start)) + Closed((5, settled - start)), paid);
        }
    }

    // ---------------------------------------------------------------- 2. per-pulse economics

    [Fact]
    public void Economics_SettleAtEachPulseEnd_ObservableMidTurn()
    {
        long start = 10 * Day, w = 21 * Day;
        // A window for polity 0 at the end of pulse 1, mid-turn (turn 0 is 3600 days long).
        var rig = Build(new[] { War(0, start, start + 3 * w, new[] { 0, 1 }, new long[] { 3600, 100 }) }, upkeep: new long[] { 360, 0, 0, 0, 0, 0 },
                        sig: new SignificantKinds(WarKinds.Probe), probes: new[] { (start + 2 * w, 0) });
        var r = rig.Session.RunUntilWindowFor(0, start + 10 * w);
        Assert.Equal(RunStatus.Interrupted, r.Status);
        Assert.Equal(start + 2 * w, r.AtMs);
        Assert.Equal(Closed((3600, 2 * w)) + Closed((100, 2 * w)), rig.W.Ledger.Balance(rig.W.SupplySink));
        Assert.Equal(420 + 11, rig.W.Ledger.Balance(rig.W.SupplySink));
        Assert.Equal(Closed((360, 2 * w)), rig.W.Ledger.Balance(rig.W.UpkeepSink));
        Assert.Equal(new long[] { 210 + 5, 210 + 6 }, rig.W.SubSteps.Select(x => x.SupplyDue).ToArray());
        rig.Session.RunUntilWindowFor(0, start + 10 * w);
        Assert.Equal(Closed((3600, 3 * w)) + Closed((100, 3 * w)), rig.W.Ledger.Balance(rig.W.SupplySink));
    }

    [Fact]
    public void CrossingInvariance_PulsesAndTotalsIndependentOfTurnBoundaries()
    {
        var shipped = ShippedCalendar();
        var parts = shipped.Bands.Select(b => $"{{ \"name\": \"{b.Name}\", \"startYear\": {b.StartYear}, \"endYear\": {b.EndYear}, \"dtYears\": 1 }}");
        var oneYear = CampaignCalendar.FromJson(System.Text.Encoding.UTF8.GetBytes("{ \"bands\": [ " + string.Join(", ", parts) + " ] }"));
        long edge = shipped.Bands[1].StartMs, start = edge - 700 * Day, stop = edge + 500 * Day;
        WarSpec[] Wars() => new[] { War(0, start, stop, new[] { 1, 3 }, new long[] { 4321, 17 }) };
        var a = Build(Wars(), upkeep: new long[] { 0, 29, 0, 31, 0, 0 });
        var b = Build(Wars(), upkeep: new long[] { 0, 29, 0, 31, 0, 0 }, turnCal: oneYear);
        a.R.RunUntil(stop + 30 * Day);
        b.R.RunUntil(stop + 30 * Day);
        Assert.Equal(a.W.PulseRecords.Select(p => (p.Index, p.StartMs, p.EndMs)), b.W.PulseRecords.Select(p => (p.Index, p.StartMs, p.EndMs)));
        Assert.Equal(a.W.Ledger.Snapshot(), b.W.Ledger.Snapshot());
        Assert.NotEqual(a.W.SubSteps.Count, b.W.SubSteps.Count);         // the splits differ; the economics do not
    }

    // ---------------------------------------------------------------- 3. locality and fog

    [Fact]
    public void TwoWars_HaveIndependentPulseClocks_AndNoCrossTalk()
    {
        long s0 = 100 * Day, s1 = 103 * Day + 5;
        WarSpec W0() => War(0, s0, s0 + 300 * Day, new[] { 0, 1 }, new long[] { 50, 60 });
        var both = Build(new[] { W0(), War(1, s1, s1 + 200 * Day, new[] { 2, 3 }, new long[] { 70, 80 }) }, upkeep: new long[] { 1, 2, 3, 4, 0, 0 });
        var alone = Build(new[] { W0() }, upkeep: new long[] { 1, 2, 3, 4, 0, 0 });
        both.R.RunUntil(400 * Day);
        alone.R.RunUntil(400 * Day);
        Assert.Equal(alone.W.PulseRecords, both.W.PulseRecords.Where(p => p.WarId == 0));
        Assert.Equal(alone.W.SubSteps, both.W.SubSteps.Where(p => p.WarId == 0));
        Assert.All(both.W.PulseRecords.Where(p => p.WarId == 1), p => Assert.Equal(s1 % (21 * Day), p.StartMs % (21 * Day))); // own phase
        for (int pol = 0; pol < 2; pol++)
        {
            Assert.Equal(alone.W.Ledger.Balance(alone.W.TreasuryOf(pol)), both.W.Ledger.Balance(both.W.TreasuryOf(pol)));
            Assert.Equal(alone.W.Ledger.Balance(alone.W.GranaryOf(pol)), both.W.Ledger.Balance(both.W.GranaryOf(pol)));
        }
    }

    /// <summary>Polities 0,1 fight war 0 at (0,0); 2,3 fight war 1 at (1000,1000); 4 has a city 30 cells from war 0; 5 is isolated.</summary>
    private static Rig FogRig(bool withWar1, bool stops = false)
    {
        var wars = new List<WarSpec> { War(0, 100 * Day, 300 * Day, new[] { 0, 1 }, new long[] { 40, 40 }, 0, 0) };
        if (withWar1) wars.Add(War(1, 150 * Day + 3, 400 * Day, new[] { 2, 3 }, new long[] { 40, 40 }, 1000, 1000));
        var fog = new WarFog(new[] { new WarObserver(4, 18, 24, 30, 0, long.MaxValue), new WarObserver(5, -5000, -5000, 10, 0, long.MaxValue) });
        return Build(wars.ToArray(), fog: fog, sig: new SignificantKinds(WarKinds.PulseStart), upkeep: new long[] { 5, 5, 5, 5, 0, 0 }, stops: stops);
    }

    [Fact]
    public void FogMetamorphic_NonParticipantViewIsIdenticalWithTheDistantWarPresentOrAbsent()
    {
        var with = FogRig(withWar1: true);
        var without = FogRig(withWar1: false);
        with.R.RunUntil(500 * Day);
        without.R.RunUntil(500 * Day);
        Assert.NotEqual(with.R.WindowsHashHex(), without.R.WindowsHashHex());       // the world differs …
        foreach (int pol in new[] { 0, 1, 4, 5 })                                   // … no non-participant of war 1 can tell
            Assert.Equal(without.W.PolityViewHashHex(pol, without.R.Windows), with.W.PolityViewHashHex(pol, with.R.Windows));
        Assert.NotEqual(without.W.PolityViewHashHex(2, without.R.Windows), with.W.PolityViewHashHex(2, with.R.Windows));
        // The observer city sees war 0 (and only war 0); the isolated polity learns nothing and receives no window.
        Assert.NotEmpty(with.W.KnowledgeOf(4));
        Assert.All(with.W.KnowledgeOf(4), k => Assert.Equal(0, k.WarId));
        Assert.Empty(with.W.KnowledgeOf(5));
        Assert.Empty(with.W.WindowsFor(5, with.R.Windows));
        Assert.All(with.W.WindowsFor(4, with.R.Windows), w => Assert.All(w.Hits, h => Assert.Equal(0, h.SourceId)));
        Assert.Equal(with.W.WindowsFor(2, with.R.Windows).Count, with.W.PulseRecords.Count(p => p.WarId == 1));
    }

    [Theory]
    [InlineData(-1, true)]
    [InlineData(0, true)]
    [InlineData(1, false)]
    public void ObserverPresence_IsInstantExact(long offsetMs, bool seesPulse2Start)
    {
        long start = 100 * Day, b2 = start + 2 * 21 * Day;
        var fog = new WarFog(new[] { new WarObserver(4, 3, 4, 5, b2 + offsetMs, b2 + 21 * Day) }); // distance exactly 5: in sight
        var rig = Build(new[] { War(0, start, start + 5 * 21 * Day, new[] { 0, 1 }, new long[] { 1, 1 }) }, fog: fog);
        rig.R.RunUntil(start + 6 * 21 * Day);
        var k = rig.W.KnowledgeOf(4);
        Assert.Equal(seesPulse2Start, k.Any(e => e.AtMs == b2 && e.PulseIndex == 2 && e.Kind == WarKinds.PulseStart));
        Assert.All(k, e => Assert.True(e.AtMs >= b2 + offsetMs && e.AtMs < b2 + 21 * Day)); // half-open presence [from, until)
        Assert.Equal(seesPulse2Start ? 2 : 0, k.Count);                                      // pulse 1 end + pulse 2 start, both at b2

        var far = new WarFog(new[] { new WarObserver(4, 3, 4, 4, 0, long.MaxValue) });      // distance 5 > radius 4: never in sight
        var rig2 = Build(new[] { War(0, start, start + 5 * 21 * Day, new[] { 0, 1 }, new long[] { 1, 1 }) }, fog: far);
        rig2.R.RunUntil(start + 6 * 21 * Day);
        Assert.Empty(rig2.W.KnowledgeOf(4));
    }

    [Fact]
    public void WindowRecipients_ArePerPolity_AndANonRecipientOrderIsRefused()
    {
        var rig = FogRig(withWar1: true, stops: true);
        // Polity 4 (observer of war 0) is stopped only at war-0 pulse starts, never at war-1 ones.
        var r = rig.Session.RunUntilWindowFor(4, 500 * Day);
        Assert.Equal(RunStatus.Interrupted, r.Status);
        Assert.Equal(100 * Day, r.AtMs);
        // Run on to the first war-1 window: polity 4 is not a recipient there; an order it issues is a recorded refusal.
        var r2 = rig.Session.RunUntilWindowFor(2, 500 * Day);
        Assert.Equal(150 * Day + 3, r2.AtMs);
        Assert.Equal(0UL, rig.W.LatestWindowRecipients & (1UL << 4));
        long[] before = rig.W.Ledger.Snapshot();
        rig.R.IssueOrder(WarKinds.OrderUpkeepRate, WarKinds.Pack(4, 0), 1_000_000);
        rig.R.IssueOrder(WarKinds.OrderSupplyRate, WarKinds.Pack(2, 1), 999);     // a recipient: accepted
        rig.R.RunUntil(rig.S.NowMs);
        Assert.Equal(new OrderRefusal(0, 4, WarKinds.OrderUpkeepRate, 150 * Day + 3, WarRefusal.NotARecipient), rig.W.Refusals.Single());
        Assert.Equal(0, rig.W.UpkeepRateOf(4));
        Assert.Equal(999, rig.W.SupplyRateOf(1, 0));
        Assert.Equal(before, rig.W.Ledger.Snapshot());
    }

    // ---------------------------------------------------------------- 4. order delivery at War Pulse and turn boundaries

    [Theory]
    [InlineData(-1, 3)]
    [InlineData(0, 3)]
    [InlineData(1, 4)]
    public void PulseOrder_TakesEffectAtTheFirstPulseStartAtOrAfterIssue(long offsetMs, long effectivePulse)
    {
        long start = 100 * Day, w = 21 * Day, b3 = start + 3 * w, end = start + 8 * w;
        var rig = Build(new[] { War(0, start, end, new[] { 0, 1 }, new long[] { 360, 1234 }) },
                        sig: new SignificantKinds(WarKinds.Probe), probes: new[] { (b3 + offsetMs, 0) });
        var r = rig.Session.RunUntilWindowFor(0, end + w);
        Assert.Equal(b3 + offsetMs, r.AtMs);
        rig.R.IssueOrder(WarKinds.OrderSupplyRate, WarKinds.Pack(0, 0), 36_000);
        Assert.Equal(RunStatus.ReachedLimit, rig.Session.RunUntilWindowFor(0, end + w).Status);

        long eff = start + effectivePulse * w;
        Assert.Equal(new LatchRecord(0, 0, WarKinds.OrderSupplyRate, 0, b3 + offsetMs, eff, effectivePulse, 36_000), rig.W.Latches.Single());
        Assert.Equal(Closed((360, eff - start), (36_000, end - eff)) + Closed((1234, end - start)), rig.W.Ledger.Balance(rig.W.SupplySink));
        // The pulse the order missed (offset +1: pulse 3) settled entirely at the old rate.
        var p3 = rig.W.SubSteps.Single(x => x.PulseIndex == 3);
        Assert.Equal(offsetMs <= 0 ? 2100 + 72 : 21 + 72, p3.SupplyDue);
    }

    [Theory]
    [InlineData(-1, 1)]
    [InlineData(0, 1)]
    [InlineData(1, 2)]
    public void TurnOrder_TakesEffectAtTheFirstTurnStartAtOrAfterIssue(long offsetMs, long effectiveTurn)
    {
        var cal = ShippedCalendar();
        long s1 = cal.TurnStart(1).Ms, s2 = cal.TurnStart(2).Ms, start = s1 - 30 * Day, end = start + 4 * 21 * Day;
        var rig = Build(new[] { War(0, start, end, new[] { 0, 1 }, new long[] { 0, 0 }) }, upkeep: new long[] { 360, 7, 0, 0, 0, 0 },
                        sig: new SignificantKinds(WarKinds.Probe), probes: new[] { (s1 + offsetMs, 0) });
        var r = rig.Session.RunUntilWindowFor(0, s2);
        Assert.Equal(s1 + offsetMs, r.AtMs);
        rig.R.IssueOrder(WarKinds.OrderUpkeepRate, WarKinds.Pack(0, 0), 36_000);
        Assert.Equal(RunStatus.ReachedLimit, rig.Session.RunUntilWindowFor(0, s2).Status);

        long eff = cal.TurnStart(effectiveTurn).Ms;
        Assert.Equal(new LatchRecord(0, 0, WarKinds.OrderUpkeepRate, 0, s1 + offsetMs, eff, effectiveTurn, 36_000), rig.W.Latches.Single());
        // The straddle part before S1 is settled at the old rate in every case (split before latch).
        var snap = rig.W.TurnSnapshots.Single(t => t.Turn == 1);
        Assert.Equal(Closed((360, 30 * Day)) + Closed((7, 30 * Day)), snap.Balances[rig.W.UpkeepSink]);
        long expected0 = effectiveTurn == 1 ? Closed((360, 30 * Day), (36_000, end - s1)) : Closed((360, end - start));
        Assert.Equal(expected0 + Closed((7, end - start)), rig.W.Ledger.Balance(rig.W.UpkeepSink));
        Assert.Equal(effectiveTurn == 1 ? 5430 + 1 : 84 + 1, rig.W.Ledger.Balance(rig.W.UpkeepSink));
    }

    // ---------------------------------------------------------------- 5. ordering, replay, golden

    [Fact]
    public void TieDense_CoincidentWarEvents_SettleInWarIdOrder_NotSchedulingOrder()
    {
        // War i starts one pulse after war i-1, so at each instant the newest war's start was scheduled FIRST (lowest Seq)
        // but must execute LAST among the coincident starts: the key decides by SourceId = war id before Seq.
        var wars = new WarSpec[8];
        for (int i = 0; i < 8; i++) wars[i] = War(i, 50 * Day + i * 21 * Day, 50 * Day + 14 * 21 * Day, new[] { 0, i + 1 }, new long[] { 0, 0 });
        var upkeep = new long[9];
        upkeep[0] = 3600;                                               // polity 0: 210 per war per pulse
        var a = Build(wars, upkeep: upkeep, polities: 9, money: 5000);
        var b = Build(Enumerable.Reverse(wars).ToArray(), upkeep: upkeep, polities: 9, money: 5000);
        a.R.RunUntil(400 * Day);
        b.R.RunUntil(400 * Day);
        Assert.Equal(a.S.Trace.HashHex(), b.S.Trace.HashHex());
        Assert.Equal(a.W.StateHashHex(), b.W.StateHashHex());

        var trace = a.S.Trace.Entries;
        foreach (var g in trace.Where(e => e.Kind == WarKinds.PulseStart || e.Kind == WarKinds.PulseEnd).GroupBy(e => (e.Key.TimeMs, e.Kind)))
        {
            var ids = g.Select(e => e.Key.SourceId).ToArray();
            Assert.Equal(ids.OrderBy(v => v).ToArray(), ids);           // ascending war id at every coincident instant
        }
        Assert.Contains(trace, e => e.Kind == WarKinds.PulseStart && e.Key.TimeMs == 50 * Day + 7 * 21 * Day && e.Key.SourceId == 7);
        // Shortfall order: at every instant, the wars short of upkeep for polity 0 form a SUFFIX in war-id order.
        foreach (var g in a.W.SubSteps.GroupBy(x => x.EndMs))
        {
            var st = g.OrderBy(x => x.WarId).ToArray();
            bool shortSeen = false;
            foreach (var x in st)
            {
                bool isShort = x.UpkeepPaid < x.UpkeepDue;
                Assert.False(shortSeen && !isShort);
                shortSeen |= isShort;
            }
        }
        Assert.Equal(5000, a.W.Ledger.Balance(a.W.UpkeepSink));
        AssertConserved(a.W, 9, 5000, Big);
    }

    private static Rig ReplayScenario(SubTurnOrderLog? log, Action<Rig>? live)
    {
        var cal = ShippedCalendar();
        long edge = cal.Bands[1].StartMs;
        var fog = new WarFog(new[] { new WarObserver(4, 10, 0, 50, edge, edge + 200 * Day) });
        var wars = new[]
        {
            War(0, edge - 50 * Day, edge + 100 * Day, new[] { 0, 1 }, new long[] { 1000, 2000 }),
            War(1, edge - 20 * Day + 7, edge + 150 * Day, new[] { 0, 2 }, new long[] { 300, 400 }, 5000, 5000),
        };
        var rig = Build(wars, fog: fog, sig: new SignificantKinds(WarKinds.PulseStart, WarKinds.Probe), upkeep: new long[] { 9, 8, 7, 0, 0, 0 },
                        startMs: edge - 60 * Day, money: 100_000, goods: 10_000_000, probes: new[] { (edge - 1, 0), (edge + 33 * Day + 1, 0) },
                        stops: log is null, replayLog: log);
        if (live is not null) live(rig);
        rig.R.RunUntil(edge + 200 * Day);
        return rig;
    }

    private static void DriveOrders(Rig rig)
    {
        long limit = rig.Cal.Bands[1].StartMs + 200 * Day;
        int n = 0;
        while (rig.Session.RunUntilWindowFor(0, limit).Status == RunStatus.Interrupted)
        {
            n++;
            if (n == 2) rig.R.IssueOrder(WarKinds.OrderSupplyRate, WarKinds.Pack(0, 1), 5000);   // before war 1 starts: latched at its pulse 0
            if (n == 4) rig.R.IssueOrder(WarKinds.OrderUpkeepRate, WarKinds.Pack(0, 0), 900);    // mid-turn, at the start of pulse 2 of war 0
            if (n == 9) rig.R.IssueOrder(WarKinds.OrderSupplyRate, WarKinds.Pack(0, 0), 0);
        }
    }

    [Fact]
    public void Replay_WithBoundaryOrders_ReproducesTraceStateAndWindows()
    {
        var live = ReplayScenario(null, DriveOrders);
        Assert.Equal(3, live.R.Orders.Count);
        Assert.Equal(3, live.W.Latches.Count);
        var replay = ReplayScenario(live.R.Orders, null);
        Assert.Equal(live.S.Trace.HashHex(), replay.S.Trace.HashHex());
        Assert.Equal(live.W.StateHashHex(), replay.W.StateHashHex());
        Assert.Equal(live.R.WindowsHashHex(), replay.R.WindowsHashHex());
        var none = ReplayScenario(new SubTurnOrderLog(), null);                       // teeth: the orders matter
        Assert.NotEqual(live.W.StateHashHex(), none.W.StateHashHex());
        Assert.Equal(live.R.WindowsHashHex(), none.R.WindowsHashHex());               // windows are world-state only
    }

    [Fact]
    public void T2Golden_TraceAndStateHashes()
    {
        var live = ReplayScenario(null, DriveOrders);
        _out.WriteLine($"trace {live.S.Trace.HashHex()} state {live.W.StateHashHex()} windows {live.R.WindowsHashHex()} " +
                       $"events {live.S.ExecutedCount} substeps {live.W.SubSteps.Count}");
        Assert.Equal("39BFB0B9278DB2EAD5A8610E4729461DB312B7CCA06C9235631F67B720FA3961", live.S.Trace.HashHex());
        Assert.Equal("10F9370F7046BE080BF41096B89441EF1C131035862660045497667D1F2818D2", live.W.StateHashHex());
    }

    // ---------------------------------------------------------------- 6. PROPOSED durations: measured pulses per turn

    [Fact]
    public void ProposedDurations_WholeCampaignWar_PulsesPerTurnMeasured_TimeTiles_ConservationExact()
    {
        var cal = ShippedCalendar();
        long campaignEnd = cal.CampaignEnd.Ms;
        var rig = Build(new[] { War(0, 0, campaignEnd, new[] { 0, 1 }, new long[] { 1000, 1234 }) }, upkeep: new long[] { 7, 11, 0, 0, 0, 0 }, stops: false);
        rig.S.Trace.Enabled = false;
        rig.R.RunUntil(campaignEnd + 30 * Day);

        var startsPerTurn = new long[cal.TotalTurns];
        foreach (var p in rig.W.PulseRecords) startsPerTurn[cal.TurnOf(new SimInstant(p.StartMs))]++;
        var timePerTurn = new long[cal.TotalTurns];
        foreach (var x in rig.W.SubSteps) timePerTurn[x.Turn] += x.EndMs - x.StartMs;
        long lastEnd = rig.W.PulseRecords[^1].EndMs;
        for (long t = 0; t < cal.TotalTurns - 1; t++) Assert.Equal(cal.TurnLengthMs(t), timePerTurn[t]);
        Assert.Equal(lastEnd - cal.TurnStart(cal.TotalTurns - 1).Ms, timePerTurn[^1]);

        var measured = new List<string>();
        for (int bi = 0; bi < cal.Bands.Count; bi++)
        {
            var band = cal.Bands[bi];
            long min = long.MaxValue, max = 0, sum = 0;
            for (long t = band.FirstTurn; t < band.FirstTurn + band.TurnCount; t++)
            {
                min = Math.Min(min, startsPerTurn[t]); max = Math.Max(max, startsPerTurn[t]); sum += startsPerTurn[t];
                if (t > band.FirstTurn)                                     // interior turns: floor or ceil of turn/pulse
                    Assert.InRange(startsPerTurn[t], band.DtDays / ProposedPulseDays[bi], (band.DtDays + ProposedPulseDays[bi] - 1) / ProposedPulseDays[bi]);
            }
            measured.Add($"{band.Name}:{band.DtDays}d/{ProposedPulseDays[bi]}d min {min} max {max} total {sum} turns {band.TurnCount}");
        }
        foreach (var m in measured) _out.WriteLine(m);
        Assert.Equal(new[]
        {
            "Neolithic:3600d/21d min 171 max 172 total 42858 turns 250",
            "Bronze/Iron:1800d/14d min 128 max 129 total 51428 turns 400",
            "Medieval:1080d/10d min 107 max 108 total 32399 turns 300",
            "Early Modern:720d/7d min 102 max 103 total 20572 turns 200",
            "Industrial:360d/3d min 119 max 120 total 14399 turns 120",
            "Modern:180d/2d min 90 max 90 total 14400 turns 160",
            "Information+:180d/1d min 179 max 180 total 35999 turns 200",
        }, measured.ToArray());

        Assert.Equal(Closed((1000, lastEnd)) + Closed((1234, lastEnd)), rig.W.Ledger.Balance(rig.W.SupplySink));
        Assert.Equal(Closed((7, lastEnd)) + Closed((11, lastEnd)), rig.W.Ledger.Balance(rig.W.UpkeepSink));
        AssertConserved(rig.W, 6, Big, Big);
    }
}
