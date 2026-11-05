using Proto.M7.Demo;
using Proto.M7.Events;
using static Proto.M7.Tests.TestSupport;

namespace Proto.M7.Tests;

/// <summary>Spec §2.3 — event key order, explicit heap, classes, simultaneity, invalidation, cancellation, trace.</summary>
public class SchedulerTests
{
    private sealed class RecordingWorld : IEventWorld
    {
        public readonly List<ScheduledEvent> Executed = new();
        public Action<ScheduledEvent, EventScheduler>? OnExecute;
        public bool IsLive(EntityRef entity) => true;
        public void Execute(in ScheduledEvent ev, EventScheduler s) { Executed.Add(ev); OnExecute?.Invoke(ev, s); }
    }

    [Fact]
    public void EventKey_is_lexicographic_time_class_priority_source_seq()
    {
        var k = new EventKey(10, 30, 0, 5, 100);
        Assert.True(k.CompareTo(k with { TimeMs = 11, Class = 0 }) < 0);
        Assert.True(k.CompareTo(k with { Class = 40, Priority = -9 }) < 0);
        Assert.True(k.CompareTo(k with { Priority = 1, SourceId = -1 }) < 0);
        Assert.True(k.CompareTo(k with { SourceId = 6, Seq = 0 }) < 0);
        Assert.True(k.CompareTo(k with { Seq = 101 }) < 0);
        Assert.Equal(0, k.CompareTo(k));
    }

    [Fact]
    public void Heap_extraction_is_the_total_key_order_for_tie_dense_keys_and_any_insertion_order()
    {
        // Tie-dense: 3 times x 2 classes x 2 priorities x 3 sources = 36 distinct prefixes over 6000 events,
        // so ~167 events share every (Time, Class, Priority, SourceId) prefix and only Seq separates them.
        const int n = 6000;
        var events = new ScheduledEvent[n];
        for (int i = 0; i < n; i++)
        {
            long t = (long)Rng.CounterRng.NextBelow(1, 1, i, 0, 0, 0, 3);
            byte c = Rng.CounterRng.NextBelow(1, 2, i, 0, 0, 0, 2) == 0 ? (byte)0 : (byte)10;
            int p = (int)Rng.CounterRng.NextBelow(1, 3, i, 0, 0, 0, 2);
            long s = (long)Rng.CounterRng.NextBelow(1, 4, i, 0, 0, 0, 3);
            events[i] = new ScheduledEvent(new EventKey(t, c, p, s, i), i, EntityRef.None, EntityRef.None, 0, 0, 0);
        }
        // Independent reference: LINQ multi-key sort (allowed in tests).
        var expected = events.OrderBy(e => e.Key.TimeMs).ThenBy(e => e.Key.Class).ThenBy(e => e.Key.Priority)
                             .ThenBy(e => e.Key.SourceId).ThenBy(e => e.Key.Seq).Select(e => e.Key).ToArray();
        for (ulong seed = 0; seed < 5; seed++)
        {
            var heap = new EventHeap(4);
            foreach (int i in Permutation(n, seed)) heap.Push(events[i]);
            var got = new EventKey[n];
            for (int i = 0; i < n; i++) got[i] = heap.Pop().Key;
            Assert.Equal(expected, got);
            Assert.Equal(0, heap.Count);
        }
    }

    [Fact]
    public void Equal_prefix_events_execute_in_scheduling_order()
    {
        var s = new EventScheduler();
        var w = new RecordingWorld();
        for (int i = 0; i < 50; i++) s.Schedule(5, EventClass.Fire, 0, 7, kind: i);
        while (s.PendingCount > 0) s.ExecuteNext(w, out _);
        Assert.Equal(Enumerable.Range(0, 50).ToArray(), w.Executed.Select(e => e.Kind).ToArray());
    }

    [Fact]
    public void Classes_execute_in_ascending_order_within_one_instant_and_player_order_is_reserved()
    {
        var s = new EventScheduler();
        var w = new RecordingWorld();
        var classes = new[] { EventClass.Significant, EventClass.Damage, EventClass.System, EventClass.CampaignProcess,
                              EventClass.Fire, EventClass.Resolve, EventClass.Detection, EventClass.Arrival };
        foreach (var c in classes) s.Schedule(100, c, 0, 0, kind: (int)c);
        while (s.PendingCount > 0) s.ExecuteNext(w, out _);
        Assert.Equal(new[] { 0, 10, 20, 30, 40, 50, 60, 70 }, w.Executed.Select(e => e.Kind).ToArray());
        Assert.Throws<InvalidOperationException>(() => s.Schedule(200, EventClass.PlayerOrder, 0, 0, 1));
    }

    [Fact]
    public void Causality_no_event_may_be_scheduled_behind_the_executing_key_or_in_the_past()
    {
        var s = new EventScheduler();
        var w = new RecordingWorld();
        Exception? caught = null;
        w.OnExecute = (ev, sch) =>
        {
            if (ev.Kind == 1)
                caught = Record.Exception(() => sch.Schedule(ev.Key.TimeMs, EventClass.Fire, 0, 0, 2)); // Fire behind Damage
        };
        s.Schedule(10, EventClass.Damage, 0, 0, 1);
        s.ExecuteNext(w, out _);
        Assert.IsType<InvalidOperationException>(caught);
        Assert.Throws<InvalidOperationException>(() => s.Schedule(9, EventClass.Resolve, 0, 0, 3));
        s.Schedule(10, EventClass.Resolve, 0, 0, 3); // a later class at the same instant is fine
    }

    [Fact]
    public void Mutual_kill_at_the_same_instant_both_die()
    {
        var cal = ShippedCalendar();
        var s = new EventScheduler();
        var w = new CampaignDemoWorld(cal);
        long a = w.AddUnit(10), b = w.AddUnit(10);
        long t = 5 * Day + 123;
        w.ScheduleFire(s, t, a, b, 10);
        w.ScheduleFire(s, t, b, a, 10);
        var r = new Runner(s, w, SignificantKinds.None);
        r.RunUntil(10 * Day);
        Assert.False(w.Unit(a).Alive);
        Assert.False(w.Unit(b).Alive);
        Assert.Equal(t, w.Unit(a).DiedAtMs);
        Assert.Equal(t, w.Unit(b).DiedAtMs);
        // Phase order recorded in the trace: Fire, Fire, Damage, Damage, Resolve — all at t.
        Assert.Equal(new[] { DemoKinds.Fire, DemoKinds.Fire, DemoKinds.Damage, DemoKinds.Damage, DemoKinds.ResolveDeaths },
                     s.Trace.Entries.Select(e => e.Kind).ToArray());
        Assert.All(s.Trace.Entries, e => Assert.Equal(t, e.Key.TimeMs));
        Assert.All(s.Trace.Entries, e => Assert.Equal(TraceOutcome.Executed, e.Outcome));
    }

    [Fact]
    public void Sequential_shots_one_ms_apart_only_the_first_shooter_survives_and_the_dead_shooters_shot_is_dropped()
    {
        var s = new EventScheduler();
        var w = new CampaignDemoWorld(ShippedCalendar());
        long a = w.AddUnit(10), b = w.AddUnit(10);
        w.ScheduleFire(s, 1000, a, b, 10);
        w.ScheduleFire(s, 1001, b, a, 10);
        new Runner(s, w, SignificantKinds.None).RunUntil(2000);
        Assert.True(w.Unit(a).Alive);
        Assert.False(w.Unit(b).Alive);
        var last = s.Trace.Entries[^1];
        Assert.Equal(DemoKinds.Fire, last.Kind);
        Assert.Equal(TraceOutcome.Dropped, last.Outcome);   // stale SOURCE generation
    }

    [Fact]
    public void Stale_generation_events_are_dropped_and_traced()
    {
        var s = new EventScheduler();
        var w = new CampaignDemoWorld(ShippedCalendar());
        long a = w.AddUnit(50), c = w.AddUnit(30);
        w.ScheduleFire(s, 2000, a, c, 5);           // aimed at c, generation 1
        var r = new Runner(s, w, SignificantKinds.None);
        r.RunUntil(1000);
        w.Reincarnate(c, 30);                        // id c is now a different entity (generation 2)
        r.RunUntil(3000);
        Assert.Equal(30, w.Unit(c).Hp);              // the new entity was NOT hit
        Assert.Single(s.Trace.Entries);
        Assert.Equal(TraceOutcome.Dropped, s.Trace.Entries[0].Outcome);
        Assert.Equal(new EntityRef(c, 1), s.Trace.Entries[0].Target);
        Assert.Equal(1, s.DroppedCount);
    }

    [Fact]
    public void Cancellation_by_id_prevents_execution_and_is_traced()
    {
        var s = new EventScheduler();
        var w = new RecordingWorld();
        long keep = s.Schedule(10, EventClass.System, 0, 0, 1);
        long drop = s.Schedule(10, EventClass.System, 0, 0, 2);
        s.Cancel(drop);
        s.Cancel(drop); // idempotent
        while (s.PendingCount > 0) s.ExecuteNext(w, out _);
        Assert.Equal(new[] { 1 }, w.Executed.Select(e => e.Kind).ToArray());
        Assert.Equal(TraceOutcome.Cancelled, s.Trace.Entries[1].Outcome);
        Assert.Equal(1, s.CancelledCount);
        Assert.True(s.IsCancelled(drop) && !s.IsCancelled(keep));
        Assert.Throws<ArgumentOutOfRangeException>(() => s.Cancel(999));
    }

    [Fact]
    public void Tombstones_grow_beyond_the_initial_capacity()
    {
        var t = new TombstoneSet();
        t.Add(5); t.Add(100_000); t.Add(64);
        Assert.True(t.Contains(5) && t.Contains(100_000) && t.Contains(64));
        Assert.False(t.Contains(6) || t.Contains(99_999) || t.Contains(1_000_000) || t.Contains(-1));
    }

    [Fact]
    public void Trace_hash_covers_everything_but_seq()
    {
        var baseEntry = new TraceEntry(new EventKey(1, 30, 2, 3, 4), 5, TraceOutcome.Executed, new EntityRef(6, 7), new EntityRef(8, 9), 10, 11, 12);
        string H(params TraceEntry[] es) { var t = new EventTrace(); foreach (var e in es) t.Append(e); return t.HashHex(); }
        string h0 = H(baseEntry);
        Assert.Equal(h0, H(baseEntry with { Key = baseEntry.Key with { Seq = 999 } }));
        var variants = new[]
        {
            baseEntry with { Key = baseEntry.Key with { TimeMs = 2 } },
            baseEntry with { Key = baseEntry.Key with { Class = 40 } },
            baseEntry with { Key = baseEntry.Key with { Priority = 3 } },
            baseEntry with { Key = baseEntry.Key with { SourceId = 4 } },
            baseEntry with { Kind = 6 }, baseEntry with { Outcome = TraceOutcome.Dropped },
            baseEntry with { Target = new EntityRef(6, 8) }, baseEntry with { Source = new EntityRef(9, 9) },
            baseEntry with { A = 0 }, baseEntry with { B = 0 }, baseEntry with { C = 0 },
        };
        foreach (var v in variants) Assert.NotEqual(h0, H(v));
        Assert.NotEqual(H(baseEntry, baseEntry with { A = 1 }), H(baseEntry with { A = 1 }, baseEntry)); // order matters
        Assert.Equal(74, EventTrace.EntryBytes);
    }
}
