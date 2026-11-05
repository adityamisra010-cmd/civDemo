using Proto.M7.Demo;
using Proto.M7.Events;
using Proto.M7.Fixed;
using Proto.M7.Rng;
using Proto.M7.Time;
using static Proto.M7.Tests.TestSupport;

namespace Proto.M7.Tests;

/// <summary>
/// Phase C lane T1 (DIR5 §2.5, ADR-061 §3.4–§3.10): exact proportional fractional carry, decision windows as objects,
/// the order-delivery gate, window-independent RNG, and the T1 golden. Report: reports/G-time-events-t1.md.
/// </summary>
public class TimeEventsT1Tests
{
    private static readonly SignificantKinds Sig = new(DemoKinds.ConstructionComplete);
    private const long P1 = 4_294_967_291L, P2 = 4_294_967_279L;   // coprime; P1 * P2 > long.MaxValue

    // ============================== exact fractions ==============================

    [Fact]
    public void Exact_fraction_is_canonical_and_default_is_zero()
    {
        var f = ExactFraction.Of(6, -4);
        Assert.Equal(-3, f.Num);
        Assert.Equal(2, f.Den);
        Assert.Equal(ExactFraction.Zero, default(ExactFraction));
        Assert.Equal(1, default(ExactFraction).Den);
        Assert.Equal(ExactFraction.Zero, ExactFraction.Of(0, 5));
        Assert.Equal(ExactFraction.Of(2, 4), ExactFraction.Of(-3, -6));
        Assert.Equal(-4, ExactFraction.Of(-7, 2).Floor());
        Assert.Equal(-3, ExactFraction.Of(-7, 2).Ceiling());
        Assert.Equal(ExactFraction.Of(1, 2), ExactFraction.Of(-7, 2).FractionalPart());
        Assert.Equal(3, ExactFraction.Of(7, 2).Floor());
        Assert.Equal(4, ExactFraction.Of(7, 2).Ceiling());
        Assert.Equal("-3/2", f.ToString());
        Assert.Throws<DivideByZeroException>(() => ExactFraction.Of(1, 0));
    }

    [Fact]
    public void Exact_fraction_arithmetic_is_exact_and_ordering_matches_cross_multiplication()
    {
        for (int i = 0; i < 500; i++)
        {
            long an = (long)CounterRng.NextBelow(5, 1, i, 0, 0, 0, 2_000_000) - 1_000_000;
            long ad = 1 + (long)CounterRng.NextBelow(5, 2, i, 0, 0, 0, 1_000_000);
            long bn = (long)CounterRng.NextBelow(5, 3, i, 0, 0, 0, 2_000_000) - 1_000_000;
            long bd = 1 + (long)CounterRng.NextBelow(5, 4, i, 0, 0, 0, 1_000_000);
            var a = ExactFraction.Of(an, ad);
            var b = ExactFraction.Of(bn, bd);
            Assert.Equal(a, (a + b) - b);
            Assert.Equal(ExactFraction.Ratio((Int128)an * bd + (Int128)bn * ad, (Int128)ad * bd), a + b);
            Assert.Equal(ExactFraction.Ratio((Int128)an * bn, (Int128)ad * bd), a * b);
            Assert.Equal(((Int128)an * bd).CompareTo((Int128)bn * ad), a.CompareTo(b));
            Assert.Equal(a, a.Scale(bd, 7).Scale(7, bd));
        }
    }

    [Fact]
    public void Overflow_is_refused_and_changes_nothing()
    {
        var a = ExactFraction.Of(1, P1);
        var b = ExactFraction.Of(1, P2);
        Assert.Throws<OverflowException>(() => a + b);                 // reduced denominator P1*P2 does not fit long
        Assert.Throws<OverflowException>(() => a * b);
        Assert.Throws<OverflowException>(() => ProportionalCarry.Carry(a, P2, 1));
        // The integrator is atomic: on refusal the carry is unchanged.
        var carry = a;
        Assert.Throws<OverflowException>(() => RateIntegrator.IntegrateExact(1, 1, P2, ref carry));
        Assert.Equal(ExactFraction.Of(1, P1), carry);
        // Out-of-range inputs are rejected, not clamped.
        var one = ExactFraction.One;
        Assert.Throws<ArgumentOutOfRangeException>(() => RateIntegrator.IntegrateExact(1, 1, 10, ref one));
        Assert.Throws<ArgumentOutOfRangeException>(() => ProportionalCarry.ElapsedFraction(10, 10));
    }

    // ============================== proportional carry: round trip, telescoping, legacy loss ==============================

    private static long[] DistinctTurnLengths(CampaignCalendar cal)
        => cal.Bands.Select(b => b.DtMs).Distinct().ToArray();   // 3600, 1800, 1080, 720, 360, 180 days

    [Fact]
    public void Proportional_carry_round_trip_is_exact_for_every_shipped_turn_length_pair_and_the_legacy_floor_lost_time()
    {
        var lengths = DistinctTurnLengths(ShippedCalendar());
        Assert.Equal(6, lengths.Length);
        int cases = 0, lossy = 0;
        long lostMs = 0, maxLostMs = 0;
        foreach (long a in lengths)
        foreach (long b in lengths)
        foreach (long e in new[] { 1, 7, 123_456_789, a / 3, a / 7, a / 2 + 1, a - 1 })
        {
            cases++;
            var there = ProportionalCarry.Carry(ExactFraction.FromInteger(e), a, b);
            Assert.Equal(ProportionalCarry.ElapsedFraction(e, a), there.Scale(1, b));      // same proportion of the interval
            Assert.Equal(ExactFraction.FromInteger(e), ProportionalCarry.Carry(there, b, a)); // lossless round trip
            long legacyThere = FixedMath.MulDivFloor(e, b, a);                              // superseded Slice B rule
            long legacyBack = FixedMath.MulDivFloor(legacyThere, a, b);
            if (legacyBack != e) { lossy++; lostMs += e - legacyBack; maxLostMs = Math.Max(maxLostMs, e - legacyBack); }
        }
        Assert.Equal(252, cases);
        Assert.Equal(106, lossy);        // the old round-down lost time in 106 of 252 round trips ...
        Assert.Equal(245, lostMs);       // ... 245 ms in total ...
        Assert.Equal(19, maxLostMs);     // ... up to 19 ms in one round trip (3600 d -> 180 d -> 3600 d)
    }

    [Fact]
    public void Proportional_carry_telescopes_over_all_1630_turns_and_back()
    {
        var cal = ShippedCalendar();
        Assert.Equal(1630, cal.TotalTurns);
        long first = cal.TurnLengthMs(0), lastLen = cal.TurnLengthMs(cal.TotalTurns - 1);
        long e = first - 1;
        var x = ExactFraction.FromInteger(e);
        long legacy = e;
        for (long t = 0; t + 1 < cal.TotalTurns; t++)
        {
            x = ProportionalCarry.Carry(x, cal.TurnLengthMs(t), cal.TurnLengthMs(t + 1));
            legacy = FixedMath.MulDivFloor(legacy, cal.TurnLengthMs(t + 1), cal.TurnLengthMs(t));
        }
        Assert.Equal(ExactFraction.FromInteger(e).Scale(lastLen, first), x);    // the product of ratios telescopes
        Assert.Equal(ExactFraction.Of(311_039_999_999L, 20), x);                // = 15_551_999_999 + 19/20 ms
        Assert.Equal(15_551_999_999L, legacy);                                   // legacy dropped the 19/20 ms
        for (long t = cal.TotalTurns - 1; t > 0; t--)
        {
            x = ProportionalCarry.Carry(x, cal.TurnLengthMs(t), cal.TurnLengthMs(t - 1));
            legacy = FixedMath.MulDivFloor(legacy, cal.TurnLengthMs(t - 1), cal.TurnLengthMs(t));
        }
        Assert.Equal(ExactFraction.FromInteger(e), x);                          // exact: back to 311_039_999_999 ms
        Assert.Equal(e - 19, legacy);                                            // legacy: 19 ms of elapsed time lost
    }

    [Fact]
    public void Exact_integration_telescopes_across_every_band_change_and_the_legacy_floor_rescale_lost_units()
    {
        var cal = ShippedCalendar();
        int trials = 0, lossyTrials = 0, integerMismatch = 0;
        var legacyDeficit = ExactFraction.Zero;
        long maxDen = 0;
        for (int b = 1; b < cal.Bands.Count; b++)
        for (int trial = 0; trial < 30; trial++)
        {
            trials++;
            long t0 = cal.Bands[b].FirstTurn - 2;               // four turns straddling band edge b
            var carry = ExactFraction.Zero;
            var exactSum = ExactFraction.Zero;
            long q = 0, lq = 0, lc = 0;
            for (long t = t0; t < t0 + 4; t++)
            {
                long len = cal.TurnLengthMs(t);
                int pieces = 1 + (int)CounterRng.NextBelow(41, 1, b, trial, t, 0, 5);
                var cuts = new long[pieces + 1];
                cuts[pieces] = len;
                for (int i = 1; i < pieces; i++) cuts[i] = (long)CounterRng.NextBelow(41, 2, b, trial, t, (uint)i, (ulong)len);
                Array.Sort(cuts);
                for (int i = 0; i < pieces; i++)
                {
                    long rate = 1 + (long)CounterRng.NextBelow(41, 3, b, trial, t, (uint)i, 5000);
                    long dt = cuts[i + 1] - cuts[i];
                    q += RateIntegrator.IntegrateExact(rate, dt, len, ref carry);
                    exactSum += ExactFraction.Ratio((Int128)rate * dt, len);
                    lq += RateIntegrator.Integrate(rate, dt, len, ref lc);
                    maxDen = Math.Max(maxDen, carry.Den);
                }
                lc = FixedMath.MulDivFloor(lc, cal.TurnLengthMs(t + 1), len);   // the superseded Slice B rescale
            }
            Assert.Equal(exactSum, ExactFraction.FromInteger(q) + carry);       // exact: integers + carry == the exact integral
            var deficit = exactSum - (ExactFraction.FromInteger(lq) + ExactFraction.Of(lc, cal.TurnLengthMs(t0 + 4)));
            Assert.False(deficit.IsNegative);
            if (!deficit.IsZero) lossyTrials++;
            if (lq != q) integerMismatch++;
            legacyDeficit += deficit;
        }
        Assert.True(maxDen <= 933_120_000_000L);   // bounded by lcm of the turn lengths (10,800 days), ADR-061 T4
        Assert.Equal(180, trials);
        Assert.Equal(81, lossyTrials);                                           // legacy carry lost value in 81 of 180 trials
        Assert.Equal(0, integerMismatch);                                        // ... below one unit: integer totals still agree
        Assert.Equal(ExactFraction.Of(11, 11_520_000_000L), legacyDeficit);     // total discarded: 11/11,520,000,000 unit
        Assert.Equal(466_560_000_000L, maxDen);
    }

    // ============================== decision windows: tie-dense, one window per instant ==============================

    private sealed class NullWorld : IEventWorld
    {
        public bool IsLive(EntityRef e) => true;
        public void Execute(in ScheduledEvent ev, EventScheduler s) { }
    }

    private static (Runner r, int[] perm) TieDense(ulong permSeed, bool stops)
    {
        const long T = 1_000;
        var s = new EventScheduler();
        var r = new Runner(s, new NullWorld(), new SignificantKinds(7), stops);
        var perm = Permutation(72, permSeed);
        foreach (int i in perm)
        {
            var cls = (i & 1) == 0 ? EventClass.CampaignProcess : EventClass.Significant;
            int prio = (i >> 1) & 1;
            long src = (i >> 2) % 3;
            int kind = i % 5 == 0 ? 8 : 7;                         // non-significant events share the instant
            s.Schedule(T, cls, prio, src, kind, new EntityRef(i, 1), EntityRef.None);
        }
        s.Schedule(T + 1, EventClass.Significant, 0, 0, 7, new EntityRef(999, 1), EntityRef.None);
        return (r, perm);
    }

    [Fact]
    public void Tie_dense_significant_events_at_one_instant_open_one_window_listing_all_in_composite_key_order()
    {
        var projections = new List<string>();
        var targets = new List<string>();
        foreach (ulong seed in new ulong[] { 11, 12 })
        {
            var (r, perm) = TieDense(seed, stops: true);
            var pos = new int[72];
            for (int k = 0; k < perm.Length; k++) pos[perm[k]] = k;   // scheduling position = Seq order
            var res = r.RunUntil(10_000);
            Assert.Equal(new RunResult(RunStatus.Interrupted, 1_000), res);
            var w = Assert.Single(r.Windows);
            Assert.Equal(57, w.Hits.Count);                             // 72 events, 15 of kind 8: ONE window lists all 57
            int exactTies = 0;
            for (int k = 1; k < w.Hits.Count; k++)
            {
                var p = w.Hits[k - 1]; var c = w.Hits[k];
                int cmp = (p.Class, p.Priority, p.SourceId).CompareTo((c.Class, c.Priority, c.SourceId));
                Assert.True(cmp <= 0);
                if (cmp == 0) { exactTies++; Assert.True(pos[p.TargetId] < pos[c.TargetId]); }  // stable integer tie-break: Seq
            }
            Assert.True(exactTies >= 40);
            projections.Add(string.Join(",", w.Hits.Select(h => $"{h.Class}/{h.Priority}/{h.SourceId}")));
            targets.Add(string.Join(",", w.Hits.Select(h => h.TargetId)));
            Assert.Equal(new RunResult(RunStatus.Interrupted, 1_001), r.RunUntil(10_000));
            Assert.Equal(2, r.Windows.Count);
            Assert.Equal(RunStatus.ReachedLimit, r.RunUntil(10_000).Status);
            var (r2, _) = TieDense(seed, stops: false);                  // windows are recorded with stops off too
            Assert.Equal(RunStatus.ReachedLimit, r2.RunUntil(10_000).Status);
            Assert.Equal(r.WindowsHashHex(), r2.WindowsHashHex());
            Assert.Equal(r.Scheduler.Trace.HashHex(), r2.Scheduler.Trace.HashHex());
        }
        Assert.Equal(projections[0], projections[1]);   // the composite key decides independently of scheduling order
        Assert.NotEqual(targets[0], targets[1]);        // only exact ties fall to Seq, and they did
    }

    // ============================== order delivery pins relative to windows ==============================

    private sealed record Twin(CampaignCalendar Cal, EventScheduler S, CampaignDemoWorld W, long A, long B, long Slow);

    /// <summary>A and B complete at the same instant (1200 days, one third of turn 0); Slow does not.</summary>
    private static Twin NewTwin()
    {
        var cal = ShippedCalendar();
        var s = new EventScheduler();
        var w = new CampaignDemoWorld(cal);
        long a = w.AddProcess(s, 1000, 3000, 0);
        long b = w.AddProcess(s, 1000, 3000, 0);
        long slow = w.AddProcess(s, 1000, 100, 0);
        return new Twin(cal, s, w, a, b, slow);
    }

    [Fact]
    public void Window_order_is_stamped_at_the_window_instant_and_runs_after_every_world_event_there_and_before_the_next_instant()
    {
        const long T = 103_680_000_000L;                                 // 1200 days
        var x = NewTwin();
        var r = new Runner(x.S, x.W, Sig);
        Assert.Equal(new RunResult(RunStatus.Interrupted, T), r.RunUntil(x.Cal.TurnEnd(0).Ms - 1));
        var w = Assert.Single(r.Windows);
        Assert.Equal(new[] { x.A, x.B }, w.Hits.Select(h => h.SourceId).ToArray());   // both coincident completions, one window
        var o = r.IssueOrder(DemoKinds.OrderRush, x.Slow, 1500);
        Assert.Equal(T, o.InstantMs);
        Assert.Equal(new RunResult(RunStatus.Interrupted, 291_600_000_000L), r.RunUntil(x.Cal.TurnEnd(0).Ms - 1));
        Assert.Equal(T, x.W.Effects.Single().AppliedAtMs);
        Assert.Equal(291_600_000_000L, x.W.Process(x.Slow).CompletedAtMs);  // only delivery AT T yields this instant
        var tr = x.S.Trace.Entries;
        int oi = tr.ToList().FindIndex(e => e.Key.Class == (byte)EventClass.PlayerOrder);
        Assert.All(tr.Take(oi), e => Assert.True(e.Key.TimeMs <= T));
        Assert.True(tr[oi + 1].Key.TimeMs > T);
        Assert.Equal(TraceOutcome.Executed, tr[oi].Outcome);
    }

    [Theory]
    [InlineData(-1)]
    [InlineData(1)]
    public void A_replayed_order_one_ms_off_the_window_instant_is_refused_and_changes_nothing(long offsetMs)
    {
        const long T = 103_680_000_000L;
        var none = NewTwin();
        new Runner(none.S, none.W, Sig, stopOnSignificant: false).RunUntil(none.Cal.TurnEnd(0).Ms - 1);
        var log = new SubTurnOrderLog();
        log.Append(T + offsetMs, DemoKinds.OrderRush, none.Slow, 1500);
        var x = NewTwin();
        var r = Runner.CreateReplay(x.S, x.W, Sig, log);
        Assert.Equal(RunStatus.ReachedLimit, r.RunUntil(x.Cal.TurnEnd(0).Ms - 1).Status);
        var refused = Assert.Single(x.S.Trace.Entries, e => e.Key.Class == (byte)EventClass.PlayerOrder);
        Assert.Equal(TraceOutcome.Refused, refused.Outcome);
        Assert.Equal(1, x.S.RefusedCount);
        Assert.Empty(x.W.Effects);
        Assert.False(x.W.Process(x.Slow).Completed);
        Assert.Equal(none.W.StateHashHex(), x.W.StateHashHex());
        // The same order AT the window instant is delivered.
        var ok = new SubTurnOrderLog();
        ok.Append(T, DemoKinds.OrderRush, none.Slow, 1500);
        var y = NewTwin();
        Runner.CreateReplay(y.S, y.W, Sig, ok).RunUntil(y.Cal.TurnEnd(0).Ms - 1);
        Assert.Equal(T, y.W.Effects.Single().AppliedAtMs);
        Assert.Equal(291_600_000_000L, y.W.Process(y.Slow).CompletedAtMs);
    }

    // ============================== RNG consumption independent of windows, stops and unrelated orders ==============================

    private sealed class DrawingWorld(CampaignDemoWorld inner) : IEventWorld
    {
        public readonly List<(long Target, long Time, int Kind, ulong ByIdentity, ulong BySeq)> Draws = new();
        public bool IsLive(EntityRef e) => inner.IsLive(e);
        public void Execute(in ScheduledEvent ev, EventScheduler s)
        {
            inner.Execute(ev, s);
            if (ev.Key.Class == (byte)EventClass.CampaignProcess)
                Draws.Add((ev.Target.Id, ev.Key.TimeMs, ev.Kind,
                    CounterRng.Draw(0xC0FFEE, 7, ev.Target.Id, ev.Key.TimeMs, ev.Kind, 0),   // R5: keyed by event identity
                    CounterRng.Draw(0xC0FFEE, 7, ev.Key.Seq, 0, 0, 0)));                    // banned key (negative control)
        }
    }

    private static (DrawingWorld d, Runner r, long other, string state) RngRun(bool stops, bool order, SubTurnOrderLog? replay = null)
    {
        var cal = ShippedCalendar();
        var s = new EventScheduler();
        var w = new CampaignDemoWorld(cal);
        w.AddProcess(s, 1000, 3000, 0);
        long slow = w.AddProcess(s, 1000, 100, 0);
        long other = w.AddProcess(s, 5000, 700, 0);
        w.AddProcess(s, 500, 200, 0);                             // completes mid turn 2: a second window
        var d = new DrawingWorld(w);
        var r = replay is null ? new Runner(s, d, Sig, stops) : Runner.CreateReplay(s, d, Sig, replay);
        for (long t = 0; t < 6; t++)
            while (r.RunUntil(cal.TurnEnd(t).Ms - 1).Status == RunStatus.Interrupted)
                if (order && r.Interrupts.Count == 2 && r.Orders.Count == 0) r.IssueOrder(DemoKinds.OrderRush, slow, 900); // 2nd window
        return (d, r, other, w.StateHashHex());
    }

    [Fact]
    public void Rng_draws_do_not_depend_on_window_display_stops_replay_or_an_unrelated_order()
    {
        var shown = RngRun(stops: true, order: false);
        var hidden = RngRun(stops: false, order: false);
        var replayed = RngRun(stops: false, order: false, replay: new SubTurnOrderLog());
        Assert.True(shown.r.Windows.Count >= 2);
        Assert.Equal(shown.r.WindowsHashHex(), hidden.r.WindowsHashHex());
        Assert.Equal(shown.d.Draws, hidden.d.Draws);
        Assert.Equal(shown.d.Draws, replayed.d.Draws);
        Assert.Equal(shown.state, hidden.state);

        var ordered = RngRun(stops: true, order: true);
        Assert.Single(ordered.r.Orders.Orders);
        var otherShown = shown.d.Draws.Where(x => x.Target == shown.other).ToArray();
        var otherOrdered = ordered.d.Draws.Where(x => x.Target == ordered.other).ToArray();
        Assert.Equal(otherShown.Select(x => (x.Time, x.Kind, x.ByIdentity)), otherOrdered.Select(x => (x.Time, x.Kind, x.ByIdentity)));
        Assert.NotEqual(otherShown.Select(x => x.BySeq), otherOrdered.Select(x => x.BySeq));   // why Seq is banned as a key

        var replayOfOrdered = RngRun(stops: false, order: false, replay: ordered.r.Orders);
        Assert.Equal(ordered.d.Draws.Select(x => (x.Target, x.Time, x.Kind, x.ByIdentity)), replayOfOrdered.d.Draws.Select(x => (x.Target, x.Time, x.Kind, x.ByIdentity)));
        Assert.NotEqual(ordered.d.Draws.Select(x => x.BySeq), replayOfOrdered.d.Draws.Select(x => x.BySeq));
        Assert.Equal(ordered.state, replayOfOrdered.state);
        Assert.Equal(ordered.r.WindowsHashHex(), replayOfOrdered.r.WindowsHashHex());
    }

    // ============================== T1 golden: band change + windows + orders + replay ==============================

    private static (string trace, string state, string windows, string log, Runner r, CampaignDemoWorld w) T1Golden(SubTurnOrderLog? replay)
    {
        var cal = ShippedCalendar();
        long last = cal.Bands[1].FirstTurn - 1;                  // last 3600-day turn; the next is 1800 days
        long start = cal.TurnStart(last).Ms;
        var s = new EventScheduler(start);
        var w = new CampaignDemoWorld(cal);
        w.AddProcess(s, 10, 7, start + 1);                        // carries a fraction across the band edge
        w.AddProcess(s, 1000, 3000, start);                       // Taj-style: completes 1/3 through the turn
        long slow = w.AddProcess(s, 1000, 100, start);
        long u1 = w.AddUnit(20), u2 = w.AddUnit(20);
        w.ScheduleFire(s, start + 1500 * Day, u1, u2, 7);
        w.ScheduleFire(s, start + 1500 * Day, u2, u1, 25);
        var r = replay is null ? new Runner(s, w, Sig) : Runner.CreateReplay(s, w, Sig, replay);
        for (long t = last; t < last + 3; t++)
        {
            int stops = 0;
            while (r.RunUntil(cal.TurnEnd(t).Ms - 1).Status == RunStatus.Interrupted)
            {
                stops++;
                if (t == last && stops == 1)
                {
                    r.IssueOrder(DemoKinds.OrderRush, slow, 1500);
                    r.IssueOrder(DemoKinds.OrderStartProcess, 500, 2000);
                }
                if (t == last + 1 && stops == 1) r.IssueOrder(DemoKinds.OrderRush, CampaignDemoWorld.ProcessIdBase + 99, 1); // refused by the world
            }
        }
        return (s.Trace.HashHex(), w.StateHashHex(), r.WindowsHashHex(), r.Orders.HashHex(), r, w);
    }

    /// <summary>LINUX-MEASURED, x64, .NET 10; integer-only code and canonical little-endian encodings.</summary>
    [Fact]
    public void T1_golden_scenario_is_pinned_and_replay_reproduces_it_at_every_window()
    {
        var live = T1Golden(null);
        Assert.Equal(3, live.r.Orders.Count);
        var replay = T1Golden(live.r.Orders);
        Assert.Empty(replay.r.Interrupts);                                     // replay never stops ...
        Assert.Equal(live.r.Interrupts, replay.r.Windows.Select(w => w.InstantMs)); // ... yet opens the same windows
        Assert.Equal(live.windows, replay.windows);
        Assert.Equal(live.trace, replay.trace);
        Assert.Equal(live.state, replay.state);
        Assert.Equal(new[] { 77_552_640_000_000L, 77_630_400_000_000L, 77_740_560_000_000L, 77_826_651_428_572L }, live.r.Interrupts);
        Assert.Equal("E8EA92A08AFD3AC59525387DBFD99C5D00504303F71C6C50CF02C11DCF08E979", live.trace);
        Assert.Equal("A74B4B47E1817E4B2F21D7D6AD748B04F6F8E4119D4666F5BF61A50D9C5C2FD8", live.state);
        Assert.Equal("60428AC0A499E220BD735774A87B7CBD24DB4DE21CB21122B9223BA311ED9DB7", live.windows);
        Assert.Equal("A67810580F8D0B9ECC738A022C8254D809D2862A4388036FDBA909E237D4D2E4", live.log);
    }
}
