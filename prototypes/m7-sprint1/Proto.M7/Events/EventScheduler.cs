namespace Proto.M7.Events;

/// <summary>
/// Deterministic discrete-event scheduler (spec §2.3; O-5 hybrid, O-6 mechanism).
///  - Keys: (TimeMs, Class, Priority, SourceId, Seq), Seq scheduler-global and strictly increasing.
///  - Causality: a newly scheduled key must be strictly greater than the key of the event executing (or last
///    executed) and its time must be &gt;= Now. Hence the executed key sequence is strictly increasing and an
///    event can never be inserted "behind" the phase that is running (e.g. a Fire after Damage at one instant).
///  - Invalidation: Target/Source refs are re-checked against the world at pop; stale =&gt; traced as Dropped.
///  - Cancellation: by Seq through a tombstone bit array; traced as Cancelled when it comes due.
///  - PlayerOrder class is reserved for the Runner's order path; the public Schedule rejects it, so orders
///    are the ONLY events in that class (the replay-ordering proof depends on this).
/// </summary>
public sealed class EventScheduler
{
    private readonly EventHeap _heap = new();
    private readonly TombstoneSet _tombstones = new();
    private long _nextSeq;
    private EventKey _lastKey = new(long.MinValue, 0, int.MinValue, long.MinValue, -1);

    public EventScheduler(long startMs = 0) => NowMs = startMs;

    /// <summary>Current simulated instant: the time of the event executing/last executed, or the last run limit.</summary>
    public long NowMs { get; private set; }
    public EventTrace Trace { get; } = new();
    public int PendingCount => _heap.Count;
    public long NextSeq => _nextSeq;
    public long ExecutedCount { get; private set; }
    public long DroppedCount { get; private set; }
    public long CancelledCount { get; private set; }

    /// <summary>The key of the event currently executing, or the last one executed.</summary>
    public EventKey CurrentKey => _lastKey;

    /// <summary>Schedule a world event. Returns its id (Seq). Class PlayerOrder is rejected here (Runner only).</summary>
    public long Schedule(long timeMs, EventClass cls, int priority, long sourceId, int kind,
                         EntityRef target, EntityRef source, long a = 0, long b = 0, long c = 0)
    {
        // Every class at or after PlayerOrder is reserved: out-of-enum values (e.g. 255) would otherwise sort
        // AFTER orders at an instant and break "PlayerOrder is always last" (verify-B F9).
        if ((byte)cls >= (byte)EventClass.PlayerOrder)
            throw new InvalidOperationException("classes >= PlayerOrder are reserved for orders issued through the Runner");
        return ScheduleCore(timeMs, cls, priority, sourceId, kind, target, source, a, b, c);
    }

    /// <summary>Schedule with no entity references.</summary>
    public long Schedule(long timeMs, EventClass cls, int priority, long sourceId, int kind, long a = 0, long b = 0, long c = 0)
        => Schedule(timeMs, cls, priority, sourceId, kind, EntityRef.None, EntityRef.None, a, b, c);

    internal long ScheduleOrder(long timeMs, long logIndex, int kind, long a, long b)
        => ScheduleCore(timeMs, EventClass.PlayerOrder, 0, logIndex, kind, EntityRef.None, EntityRef.None, a, b, logIndex);

    private long ScheduleCore(long timeMs, EventClass cls, int priority, long sourceId, int kind,
                              EntityRef target, EntityRef source, long a, long b, long c)
    {
        if (timeMs < NowMs)
            throw new InvalidOperationException($"cannot schedule in the past: {timeMs} < now {NowMs}");
        var key = new EventKey(timeMs, (byte)cls, priority, sourceId, _nextSeq);
        if (key.CompareTo(_lastKey) <= 0)
            throw new InvalidOperationException($"causality violation: key {key} is not after the executing key {_lastKey}");
        _nextSeq++;
        _heap.Push(new ScheduledEvent(key, kind, target, source, a, b, c));
        return key.Seq;
    }

    /// <summary>Cancel a pending event by id. Idempotent. The event is traced as Cancelled when it comes due.</summary>
    public void Cancel(long seq)
    {
        if (seq < 0 || seq >= _nextSeq) throw new ArgumentOutOfRangeException(nameof(seq), "unknown event id");
        _tombstones.Add(seq);
    }

    public bool IsCancelled(long seq) => _tombstones.Contains(seq);

    public bool TryPeekKey(out EventKey key)
    {
        if (_heap.TryPeek(out var ev)) { key = ev.Key; return true; }
        key = default;
        return false;
    }

    /// <summary>Pop the minimum event and process it: cancelled / stale-dropped / executed. Returns the outcome.</summary>
    public TraceOutcome ExecuteNext(IEventWorld world, out ScheduledEvent ev)
    {
        ev = _heap.Pop();
        _lastKey = ev.Key;
        NowMs = ev.Key.TimeMs;
        TraceOutcome outcome;
        if (_tombstones.Contains(ev.Key.Seq))
        {
            outcome = TraceOutcome.Cancelled;
            CancelledCount++;
        }
        else if ((!ev.Target.IsNone && !world.IsLive(ev.Target)) || (!ev.Source.IsNone && !world.IsLive(ev.Source)))
        {
            outcome = TraceOutcome.Dropped;
            DroppedCount++;
        }
        else
        {
            outcome = TraceOutcome.Executed;
            ExecutedCount++;
        }
        // Appended BEFORE the handler runs: the trace order is the pop order, independent of what the handler schedules.
        Trace.Append(new TraceEntry(ev.Key, ev.Kind, outcome, ev.Target, ev.Source, ev.A, ev.B, ev.C));
        if (outcome == TraceOutcome.Executed) world.Execute(in ev, this);
        return outcome;
    }

    /// <summary>Advance the clock to a run limit (no event executes). Never moves backwards.</summary>
    public void AdvanceTo(long timeMs)
    {
        if (timeMs < NowMs) throw new InvalidOperationException("time cannot move backwards");
        NowMs = timeMs;
    }
}
