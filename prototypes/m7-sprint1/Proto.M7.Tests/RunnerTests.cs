using Proto.M7.Demo;
using Proto.M7.Events;
using Proto.M7.Time;
using static Proto.M7.Tests.TestSupport;

namespace Proto.M7.Tests;

/// <summary>Spec §2.4 — interruption, sub-turn orders, instant-exact delivery, replay, piecewise integration.</summary>
public class RunnerTests
{
    private static readonly SignificantKinds Sig = new(DemoKinds.ConstructionComplete);

    private sealed record Setup(CampaignCalendar Cal, EventScheduler S, CampaignDemoWorld W, long Wonder, long Slow);

    /// <summary>Wonder: 1000 of 3000 units/turn left at turn 0 start (completes 1/3 through the 10-year turn).
    /// Slow: 1000 units at 100/turn (would take 10 turns).</summary>
    private static Setup NewSetup()
    {
        var cal = ShippedCalendar();
        var s = new EventScheduler();
        var w = new CampaignDemoWorld(cal);
        long wonder = w.AddProcess(s, remainingUnits: 1000, unitsPerTurn: 3000, startMs: cal.TurnStart(0).Ms);
        long slow = w.AddProcess(s, remainingUnits: 1000, unitsPerTurn: 100, startMs: cal.TurnStart(0).Ms);
        return new Setup(cal, s, w, wonder, slow);
    }

    [Fact]
    public void Taj_Mahal_wonder_completing_one_third_through_a_ten_year_turn_stops_the_runner_exactly_there()
    {
        var x = NewSetup();
        var runner = new Runner(x.S, x.W, Sig);
        Assert.Equal(3600, x.Cal.TurnLengthDays(0));                    // a 10-year turn
        long turnStart = x.Cal.TurnStart(0).Ms, len = x.Cal.TurnLengthMs(0), turnEndExclusive = x.Cal.TurnEnd(0).Ms - 1;

        var r = runner.RunUntil(turnEndExclusive);

        Assert.Equal(RunStatus.Interrupted, r.Status);
        Assert.Equal(turnStart + len / 3, r.AtMs);                      // exactly one third ...
        Assert.Equal(turnStart + 1200 * Day, r.AtMs);                   // ... = exactly 1200 days
        Assert.Equal(0, len % 3);
        Assert.Equal(new TurnPosition(0, len / 3, len), x.Cal.PositionInTurn(new SimInstant(r.AtMs)));
        var wonder = x.W.Process(x.Wonder);
        Assert.True(wonder.Completed);
        Assert.Equal(r.AtMs, wonder.CompletedAtMs);
        Assert.Equal(0, wonder.RemainingUnits);
        Assert.Equal(r.AtMs, x.S.NowMs);                                // the world is AT the completion instant, not the turn end
        Assert.True(runner.IsStopped);

        // The player can react at that point: the order is accepted and delivered at that instant.
        var order = runner.IssueOrder(DemoKinds.OrderRush, x.Slow, 1500);
        Assert.Equal(r.AtMs, order.InstantMs);
        var r2 = runner.RunUntil(turnEndExclusive);
        Assert.Equal(r.AtMs, x.W.Effects.Single().AppliedAtMs);
        Assert.Equal(RunStatus.Interrupted, r2.Status);                // the rushed slow process now also completes inside turn 0
        Assert.Equal(RunStatus.ReachedLimit, runner.RunUntil(turnEndExclusive).Status);
    }

    [Fact]
    public void Every_shipped_turn_length_interrupts_at_an_exact_whole_day_third()
    {
        var cal = ShippedCalendar();
        foreach (var band in cal.Bands)
        {
            var s = new EventScheduler(cal.TurnStart(band.FirstTurn).Ms);
            var w = new CampaignDemoWorld(cal);
            w.AddProcess(s, 1000, 3000, cal.TurnStart(band.FirstTurn).Ms);
            var r = new Runner(s, w, Sig).RunUntil(cal.TurnEnd(band.FirstTurn).Ms - 1);
            Assert.Equal(RunStatus.Interrupted, r.Status);
            long offset = r.AtMs - cal.TurnStart(band.FirstTurn).Ms;
            Assert.Equal(band.DtDays / 3 * Day, offset);
            Assert.Equal(cal.At(band.FirstTurn, 1, 3).Ms, r.AtMs);
        }
    }

    [Fact]
    public void Order_issued_at_the_interrupt_takes_effect_at_that_instant_not_turn_end_not_next_turn()
    {
        var x = NewSetup();
        var runner = new Runner(x.S, x.W, Sig);
        long T = runner.RunUntil(x.Cal.TurnEnd(0).Ms - 1).AtMs;
        Assert.Equal(1200 * Day, T);
        runner.IssueOrder(DemoKinds.OrderRush, x.Slow, 1500);
        runner.RunUntil(x.Cal.TurnEnd(0).Ms - 1);

        // (1) the effect is stamped at T exactly
        var eff = x.W.Effects.Single();
        Assert.True(eff.Accepted);
        Assert.Equal(T, eff.AppliedAtMs);
        Assert.NotEqual(x.Cal.TurnEnd(0).Ms, eff.AppliedAtMs);
        // (2) the order event itself executed at T in the PlayerOrder class (trace)
        var orderEntry = x.S.Trace.Entries.Single(e => e.Key.Class == (byte)EventClass.PlayerOrder);
        Assert.Equal(T, orderEntry.Key.TimeMs);
        Assert.Equal(TraceOutcome.Executed, orderEntry.Outcome);
        // (3) semantic consequence pinned as literals: the slow process ran at 100/turn for exactly [0, T) = 33 units
        //     + carry L/3, then at 1600/turn: completion = T + ceil((967*L - L/3)/1600).
        long L = x.Cal.TurnLengthMs(0);
        Assert.Equal(311_040_000_000L, L);
        long expected = T + (967 * L - L / 3 + 1599) / 1600;
        Assert.Equal(291_600_000_000L, expected);
        var slow = x.W.Process(x.Slow);
        Assert.True(slow.Completed);
        Assert.Equal(expected, slow.CompletedAtMs);
        Assert.Equal(0, x.Cal.TurnOf(new SimInstant(slow.CompletedAtMs)));   // inside turn 0: impossible if delivered at turn end
    }

    [Fact]
    public void Orders_can_only_be_issued_while_stopped_and_resume_runs_the_rest_of_the_same_turn()
    {
        var x = NewSetup();
        var runner = new Runner(x.S, x.W, Sig);
        Assert.Throws<InvalidOperationException>(() => runner.IssueOrder(DemoKinds.OrderRush, x.Slow, 1));
        runner.RunUntil(x.Cal.TurnEnd(0).Ms - 1);
        runner.IssueOrder(DemoKinds.OrderStartProcess, 300, 3000);   // a new process started AT the interrupt
        var r = runner.RunUntil(x.Cal.TurnEnd(0).Ms - 1);
        Assert.Equal(RunStatus.Interrupted, r.Status);                 // the new process completes 1/10 turn later
        Assert.Equal(1200 * Day + 360 * Day, r.AtMs);
        Assert.Equal(RunStatus.ReachedLimit, runner.RunUntil(x.Cal.TurnEnd(0).Ms - 1).Status);
        Assert.Equal(x.Cal.TurnEnd(0).Ms - 1, x.S.NowMs);
        Assert.Equal(new[] { 1200 * Day, 1560 * Day }, runner.Interrupts.ToArray());
    }

    [Fact]
    public void Runner_finishes_the_interrupt_instant_below_player_order_class_before_stopping()
    {
        var x = NewSetup();
        long T = 1200 * Day;
        // A second wonder completing at the same instant T: same class, higher SourceId => it executes AFTER the
        // first significant event at T, so it is part of the "rest of the instant" the runner must finish.
        long twin = x.W.AddProcess(x.S, 1000, 3000, 0);
        long u1 = x.W.AddUnit(5), u2 = x.W.AddUnit(5);
        x.W.ScheduleFire(x.S, T, u1, u2, 5);          // Fire(30) < CampaignProcess(60): runs before the completion
        x.S.Schedule(T, EventClass.Significant, 0, 0, 999);   // class 70 at T: after both completions, before orders
        var world = new KindRecorder(x.W);
        var runner = new Runner(x.S, world, Sig);
        var r = runner.RunUntil(x.Cal.TurnEnd(0).Ms - 1);
        Assert.Equal(RunStatus.Interrupted, r.Status);
        Assert.Equal(T, r.AtMs);
        Assert.True(x.W.Process(x.Wonder).Completed);
        Assert.True(x.W.Process(twin).Completed);     // finished at the same instant before the stop
        Assert.Equal(T, x.W.Process(twin).CompletedAtMs);
        Assert.Contains(999, world.Kinds);            // the class-70 event at T ran before the stop
        Assert.False(x.W.Unit(u2).Alive);
        Assert.Single(runner.Interrupts);              // one stop for the instant, not one per significant event
        Assert.True(x.S.TryPeekKey(out var next));
        Assert.True(next.TimeMs > T);
    }

    private sealed class KindRecorder(CampaignDemoWorld inner) : IEventWorld
    {
        public readonly List<int> Kinds = new();
        public bool IsLive(EntityRef e) => inner.IsLive(e);
        public void Execute(in ScheduledEvent ev, EventScheduler s)
        {
            Kinds.Add(ev.Kind);
            if (ev.Kind != 999) inner.Execute(ev, s);
        }
    }

    private static (string trace, string state, SubTurnOrderLog log, long liveOrderSeq, CampaignDemoWorld world, CampaignCalendar cal) LiveRun()
    {
        var x = NewSetup();
        var runner = new Runner(x.S, x.W, Sig);
        long u1 = x.W.AddUnit(20), u2 = x.W.AddUnit(20);
        x.W.ScheduleFire(x.S, 1200 * Day, u1, u2, 7);
        for (long turn = 0; turn < 4; turn++)
        {
            long limit = x.Cal.TurnEnd(turn).Ms - 1;
            int stops = 0;
            while (runner.RunUntil(limit).Status == RunStatus.Interrupted)
            {
                stops++;
                if (stops == 1 && turn == 0) runner.IssueOrder(DemoKinds.OrderRush, x.Slow, 400);
                if (stops == 1 && turn == 0) runner.IssueOrder(DemoKinds.OrderStartProcess, 500, 2000);
                if (stops == 2 && turn == 0) runner.IssueOrder(DemoKinds.OrderRush, 1_000_000 + 99, 1); // refused: unknown process
            }
        }
        long seq = x.S.Trace.Entries.First(e => e.Key.Class == (byte)EventClass.PlayerOrder).Key.Seq;
        return (x.S.Trace.HashHex(), x.W.StateHashHex(), runner.Orders, seq, x.W, x.Cal);
    }

    private static (string trace, string state, long orderSeq) Replay(SubTurnOrderLog log)
    {
        var x = NewSetup();
        long u1 = x.W.AddUnit(20), u2 = x.W.AddUnit(20);
        x.W.ScheduleFire(x.S, 1200 * Day, u1, u2, 7);
        var runner = Runner.CreateReplay(x.S, x.W, Sig, log);
        var r = runner.RunUntil(x.Cal.TurnEnd(3).Ms - 1);
        Assert.Equal(RunStatus.ReachedLimit, r.Status);                 // replay never stops
        long seq = x.S.Trace.Entries.First(e => e.Key.Class == (byte)EventClass.PlayerOrder).Key.Seq;
        return (x.S.Trace.HashHex(), x.W.StateHashHex(), seq);
    }

    [Fact]
    public void Replay_with_pre_scheduled_orders_reproduces_state_and_trace_hash_exactly()
    {
        var live = LiveRun();
        Assert.Equal(3, live.log.Count);
        var replay = Replay(live.log);
        Assert.Equal(live.state, replay.state);
        Assert.Equal(live.trace, replay.trace);
        // The proof's premise is real: raw Seq values DO differ between the runs, yet nothing reorders.
        Assert.NotEqual(live.liveOrderSeq, replay.orderSeq);
        // Determinism of the live path itself.
        var live2 = LiveRun();
        Assert.Equal(live.trace, live2.trace);
        Assert.Equal(live.state, live2.state);
        Assert.Equal(live.log.HashHex(), live2.log.HashHex());
    }

    /// <summary>Golden pin of the live interrupted scenario (LINUX-MEASURED, x64, .NET 10; integer-only code and a
    /// canonical little-endian encoding, so it is expected — not yet proven — to match on Windows).</summary>
    [Fact]
    public void Live_interrupted_scenario_hashes_are_pinned()
    {
        var live = LiveRun();
        Assert.Equal("10BBF4EE2184E81428B387FBEDFB6C00791FFEA6BD118B427B187953441B6E48", live.trace);   // unchanged by lane T1
        // MOVED by lane T1 (DIR5 exact carry): the state encoding now writes the carry as (Num, Den), not as a remainder
        // over the turn length. Old golden 41191E35…C0F2. The move is the encoding ONLY: the Slice B encoding of the
        // same final state still reproduces the old golden exactly (no band change in turns 0-3, so carry*L is integral).
        Assert.Equal("41191E3543CE36CD9C4CF36BD7FA912E09711C201AA9AD17DD4252CD7B7BC0F2", LegacyEncodedStateHash(live.world, live.cal));
        Assert.Equal("2E3D9E5BED96B0FE1353E592B0B899EDCFD9559217676CB09A062745FB7EE4B7", live.state);
        Assert.Equal("8617BDA73A213B5E6A2FBC75DE62F0A6FDB0BFF835845C692B49A372C2B35864", live.log.HashHex());
    }

    /// <summary>The Slice B state encoding (carry as an integer remainder over the current turn length), for golden attribution.</summary>
    private static string LegacyEncodedStateHash(CampaignDemoWorld w, CampaignCalendar cal)
    {
        using var h = System.Security.Cryptography.IncrementalHash.CreateHash(System.Security.Cryptography.HashAlgorithmName.SHA256);
        var b = new byte[8];
        void L(long v) { System.Buffers.Binary.BinaryPrimitives.WriteInt64LittleEndian(b, v); h.AppendData(b); }
        L(w.Processes.Count);
        foreach (var p in w.Processes)
        {
            Int128 scaled = (Int128)p.Carry.Num * cal.TurnLengthMs(p.CurrentTurn);
            Assert.Equal(0, scaled % p.Carry.Den);
            L(p.Id); L(p.Generation); L(p.RemainingUnits); L(p.UnitsPerTurn); L(p.CurrentTurn);
            L(p.AnchorMs); L((long)(scaled / p.Carry.Den)); L(p.Completed ? 1 : 0); L(p.CompletedAtMs);
        }
        L(w.Units.Count);
        foreach (var u in w.Units) { L(u.Id); L(u.Generation); L(u.Hp); L(u.Alive ? 1 : 0); L(u.DiedAtMs); }
        L(w.Effects.Count);
        foreach (var e in w.Effects) { L(e.LogIndex); L(e.Kind); L(e.TargetId); L(e.Accepted ? 1 : 0); L(e.AppliedAtMs); }
        return Convert.ToHexString(h.GetHashAndReset());
    }

    [Fact]
    public void Replay_comparison_has_teeth_an_order_moved_to_the_turn_end_changes_the_hashes()
    {
        var live = LiveRun();
        var shifted = new SubTurnOrderLog();
        var cal = ShippedCalendar();
        foreach (var o in live.log.Orders) shifted.Append(cal.TurnEnd(0).Ms - 1, o.Kind, o.A, o.B);
        var replay = Replay(shifted);
        Assert.NotEqual(live.state, replay.state);
        Assert.NotEqual(live.trace, replay.trace);
    }

    [Fact]
    public void Only_orders_occupy_the_player_order_class_so_seq_never_decides_between_an_order_and_a_world_event()
    {
        var x = NewSetup();
        var runner = new Runner(x.S, x.W, Sig);
        while (runner.RunUntil(x.Cal.TurnEnd(1).Ms - 1).Status == RunStatus.Interrupted)
            runner.IssueOrder(DemoKinds.OrderRush, x.Slow, 50);
        foreach (var e in x.S.Trace.Entries)
            if (e.Key.Class == (byte)EventClass.PlayerOrder) Assert.True(e.Kind >= DemoKinds.OrderRush);
            else Assert.True(e.Kind < DemoKinds.OrderRush);
        // Between orders the SourceId (log index) is unique, so their relative order is the issue order.
        var orderSources = x.S.Trace.Entries.Where(e => e.Key.Class == (byte)EventClass.PlayerOrder).Select(e => e.Key.SourceId).ToArray();
        Assert.Equal(Enumerable.Range(0, orderSources.Length).Select(i => (long)i).ToArray(), orderSources);
    }

    [Fact]
    public void Significant_kinds_are_data_with_an_empty_set_nothing_interrupts()
    {
        var x = NewSetup();
        var r = new Runner(x.S, x.W, SignificantKinds.None).RunUntil(x.Cal.TurnEnd(0).Ms - 1);
        Assert.Equal(RunStatus.ReachedLimit, r.Status);
        Assert.True(x.W.Process(x.Wonder).Completed);                  // it still completed, at the same instant
        Assert.Equal(1200 * Day, x.W.Process(x.Wonder).CompletedAtMs);
        var k = new SignificantKinds(5, 3, 5, 1);
        Assert.Equal(new[] { 1, 3, 5 }, k.Kinds.ToArray());
        Assert.True(k.Contains(3) && !k.Contains(2));
    }

    [Fact]
    public void Linear_rate_integration_is_split_invariant_with_remainder_carry()
    {
        long L = 311_040_000_000L, U = 3000;
        long whole = 0, carryWhole = 0;
        whole = RateIntegrator.Integrate(U, L, L, ref carryWhole);
        Assert.Equal(U, whole);
        Assert.Equal(0, carryWhole);
        // Split at the interrupt (one third) and at many deterministic arbitrary points.
        for (int trial = 0; trial < 200; trial++)
        {
            int pieces = 1 + (int)Rng.CounterRng.NextBelow(3, 1, trial, 0, 0, 0, 12);
            var cuts = new long[pieces + 1];
            cuts[0] = 0; cuts[pieces] = L;
            for (int i = 1; i < pieces; i++) cuts[i] = (long)Rng.CounterRng.NextBelow(3, 2, trial, i, 0, 0, (ulong)L);
            if (trial == 0 && pieces > 1) cuts[1] = L / 3;
            Array.Sort(cuts);
            long rate = 1 + (long)Rng.CounterRng.NextBelow(3, 3, trial, 0, 0, 0, 1_000_000);
            long sum = 0, carry = 0;
            for (int i = 0; i < pieces; i++) sum += RateIntegrator.Integrate(rate, cuts[i + 1] - cuts[i], L, ref carry);
            Assert.Equal(rate, sum);
            Assert.Equal(0, carry);
            // Partial prefix also matches a one-shot integration of the same span.
            long c1 = 0, c2 = 0;
            long a = RateIntegrator.Integrate(rate, cuts[1], L, ref c1);
            long b1 = RateIntegrator.Integrate(rate, cuts[1] / 2, L, ref c2);
            long b2 = RateIntegrator.Integrate(rate, cuts[1] - cuts[1] / 2, L, ref c2);
            Assert.Equal(a, b1 + b2);
            Assert.Equal(c1, c2);
        }
    }

    [Fact]
    public void Construction_state_at_turn_end_is_identical_whether_or_not_the_turn_was_interrupted()
    {
        // The slow process integrates [0, T) and [T, end) when interrupted, [0, end) when not: same integers.
        var a = NewSetup();
        var ra = new Runner(a.S, a.W, Sig);
        while (ra.RunUntil(a.Cal.TurnEnd(2).Ms).Status == RunStatus.Interrupted) { }
        var b = NewSetup();
        new Runner(b.S, b.W, SignificantKinds.None).RunUntil(b.Cal.TurnEnd(2).Ms);   // inclusive: the turn-3 boundary event integrates turn 2
        Assert.Equal(a.W.StateHashHex(), b.W.StateHashHex());
        Assert.Equal(a.S.Trace.HashHex(), b.S.Trace.HashHex());
        Assert.Equal(1000 - 300, b.W.Process(b.Slow).RemainingUnits);     // 3 whole turns x 100, carry 0
        Assert.Equal(ExactFraction.Zero, b.W.Process(b.Slow).Carry);
    }
}
