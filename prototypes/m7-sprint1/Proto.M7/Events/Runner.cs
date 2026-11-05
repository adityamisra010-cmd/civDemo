namespace Proto.M7.Events;

public enum RunStatus
{
    /// <summary>All events with Time &lt;= limit were processed; Now = limit.</summary>
    ReachedLimit = 1,
    /// <summary>A significant event executed; the runner finished that instant (classes &lt; PlayerOrder) and stopped.</summary>
    Interrupted = 2,
}

public readonly record struct RunResult(RunStatus Status, long AtMs);

/// <summary>
/// Drives the scheduler with turn interruption (spec §2.4; Taj Mahal rule; CR-006 §1.4 Option A semantics).
///
/// RunUntil(limit) processes events in key order with Time &lt;= limit. When an event whose KIND is in the
/// SignificantKinds data set executes at instant T, the runner keeps processing only events at T with class
/// &lt; PlayerOrder, then stops and returns Interrupted(T). While stopped, IssueOrder appends to the
/// SubTurnOrderLog and schedules the order at key (T, PlayerOrder, 0, SourceId = log index, Seq). The next
/// RunUntil resumes the same turn; the orders execute first (they are the minimum keys left) and take effect at T.
///
/// Replay (CreateReplay): a fresh scheduler + world, all logged orders pre-scheduled, stops disabled.
/// Why seq differences cannot reorder anything:
///  (1) orders are the only PlayerOrder-class events (EventScheduler.Schedule rejects that class), so between an
///      order and a world event the Class field decides before Seq is reached;
///  (2) between two orders, SourceId = log index decides (unique), so Seq is never reached;
///  (3) between two world events, pre-scheduling k orders shifts every world event's Seq by the same monotone
///      insertion, preserving their relative Seq order, and world events are scheduled by the same handlers in the
///      same execution order in both runs (induction on the executed prefix).
/// Hence the execution order is identical; the trace (which excludes raw Seq) and the world state hash match.
/// Interrupts are recorded on the runner (Interrupts), never in the trace, because a replay does not stop.
/// </summary>
public sealed class Runner
{
    private readonly EventScheduler _scheduler;
    private readonly IEventWorld _world;
    private readonly SignificantKinds _significant;
    private readonly List<long> _interrupts = new();

    public Runner(EventScheduler scheduler, IEventWorld world, SignificantKinds significant, bool stopOnSignificant = true)
    {
        _scheduler = scheduler;
        _world = world;
        _significant = significant;
        StopOnSignificant = stopOnSignificant;
    }

    public bool StopOnSignificant { get; }
    public EventScheduler Scheduler => _scheduler;
    public SubTurnOrderLog Orders { get; } = new();
    public bool IsStopped { get; private set; }
    public long StoppedAtMs { get; private set; } = -1;
    public IReadOnlyList<long> Interrupts => _interrupts;

    public RunResult RunUntil(long limitMs)
    {
        if (limitMs < _scheduler.NowMs) throw new ArgumentOutOfRangeException(nameof(limitMs), "limit is in the past");
        IsStopped = false;
        bool pending = false;
        long stopAt = 0;
        while (_scheduler.TryPeekKey(out var key) && key.TimeMs <= limitMs)
        {
            if (pending && (key.TimeMs != stopAt || key.Class >= (byte)EventClass.PlayerOrder)) break;
            var outcome = _scheduler.ExecuteNext(_world, out var ev);
            if (!pending && StopOnSignificant && outcome == TraceOutcome.Executed && _significant.Contains(ev.Kind))
            {
                pending = true;
                stopAt = ev.Key.TimeMs;
            }
        }
        if (pending)
        {
            IsStopped = true;
            StoppedAtMs = stopAt;
            _interrupts.Add(stopAt);
            return new RunResult(RunStatus.Interrupted, stopAt);
        }
        _scheduler.AdvanceTo(limitMs);
        return new RunResult(RunStatus.ReachedLimit, limitMs);
    }

    /// <summary>Issue a player order while stopped at an interrupt instant T; it is delivered AT T.</summary>
    public SubTurnOrder IssueOrder(int kind, long a, long b)
    {
        if (!IsStopped) throw new InvalidOperationException("orders can be issued only while the runner is stopped at an interrupt");
        var o = Orders.Append(StoppedAtMs, kind, a, b);
        _scheduler.ScheduleOrder(o.InstantMs, o.Seq, o.Kind, o.A, o.B);
        return o;
    }

    /// <summary>
    /// Build a replay runner: fresh scheduler/world (caller-provided, seeded identically), every logged order
    /// pre-scheduled at its recorded instant, stops disabled.
    /// </summary>
    public static Runner CreateReplay(EventScheduler freshScheduler, IEventWorld freshWorld, SignificantKinds significant, SubTurnOrderLog log)
    {
        var r = new Runner(freshScheduler, freshWorld, significant, stopOnSignificant: false);
        foreach (var o in log.Orders)
        {
            var copy = r.Orders.Append(o.InstantMs, o.Kind, o.A, o.B);
            if (copy.Seq != o.Seq) throw new InvalidOperationException("log index drift");
            freshScheduler.ScheduleOrder(o.InstantMs, o.Seq, o.Kind, o.A, o.B);
        }
        return r;
    }
}
