using System.Buffers.Binary;
using System.Security.Cryptography;

namespace Proto.M7.Events;

public enum RunStatus
{
    /// <summary>All events with Time &lt;= limit were processed; Now = limit.</summary>
    ReachedLimit = 1,
    /// <summary>A significant event executed; the runner finished that instant (classes &lt; PlayerOrder) and stopped.</summary>
    Interrupted = 2,
}

public readonly record struct RunResult(RunStatus Status, long AtMs);

/// <summary>One significant event listed in a decision window, in execution (key) order.</summary>
public readonly record struct SignificantHit(byte Class, int Priority, long SourceId, int Kind, long TargetId);

/// <summary>
/// A decision window (ADR-061 §3.5/§3.6): ONE per instant at which at least one significant event executed, listing ALL
/// of them in key order. Its identity is (Index, InstantMs); both are functions of world state only, so a live run, a
/// run with stops disabled and a replay produce the same windows.
/// </summary>
public sealed class DecisionWindow
{
    private readonly SignificantHit[] _hits;

    public DecisionWindow(int index, long instantMs, SignificantHit[] hits)
    {
        Index = index;
        InstantMs = instantMs;
        _hits = hits;
    }

    public int Index { get; }
    public long InstantMs { get; }
    public IReadOnlyList<SignificantHit> Hits => _hits;
}

/// <summary>
/// Drives the scheduler with turn interruption (spec §2.4; Taj Mahal rule; ADR-061 §3.6–§3.8). Rules: EventRules.
///
/// RunUntil(limit) processes events in key order with Time &lt;= limit. A window opens at instant T when an EXECUTED event
/// of class &lt; PlayerOrder has a KIND in SignificantKinds; it collects every such event at T and closes when the next key
/// has time &gt; T or class &gt;= PlayerOrder, or the run ends. Windows are recorded in EVERY mode. With stops enabled the
/// runner then stops and returns Interrupted(T). While stopped, IssueOrder appends to the SubTurnOrderLog and schedules
/// the order at key (T, PlayerOrder, 0, SourceId = log index, Seq). The next RunUntil resumes the same turn; the orders
/// execute first (they are the minimum keys left) and take effect at T.
///
/// Order gate: an order is delivered only if its instant is the instant of the latest closed window; otherwise it is
/// traced Refused and changes nothing. A hand-made or edited log therefore cannot force a split that state did not produce.
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
    private readonly List<DecisionWindow> _windows = new();
    private readonly List<SignificantHit> _hits = new();
    private long _collectAt = -1;

    public Runner(EventScheduler scheduler, IEventWorld world, SignificantKinds significant, bool stopOnSignificant = true)
    {
        _scheduler = scheduler;
        _world = world;
        _significant = significant;
        StopOnSignificant = stopOnSignificant;
        _scheduler.OrderGate = IsDeliverable;
    }

    public bool StopOnSignificant { get; }
    public EventScheduler Scheduler => _scheduler;
    public SubTurnOrderLog Orders { get; } = new();
    public bool IsStopped { get; private set; }
    public long StoppedAtMs { get; private set; } = -1;
    public IReadOnlyList<long> Interrupts => _interrupts;
    public IReadOnlyList<DecisionWindow> Windows => _windows;

    public RunResult RunUntil(long limitMs)
    {
        if (limitMs < _scheduler.NowMs) throw new ArgumentOutOfRangeException(nameof(limitMs), "limit is in the past");
        IsStopped = false;
        while (_scheduler.TryPeekKey(out var key) && key.TimeMs <= limitMs)
        {
            if (_collectAt >= 0 && (key.TimeMs != _collectAt || key.Class >= (byte)EventClass.PlayerOrder))
            {
                long at = CloseWindow();
                if (StopOnSignificant) return Stop(at);
            }
            var outcome = _scheduler.ExecuteNext(_world, out var ev);
            if (outcome == TraceOutcome.Executed && ev.Key.Class < (byte)EventClass.PlayerOrder && _significant.Contains(ev.Kind))
            {
                if (_collectAt < 0) _collectAt = ev.Key.TimeMs;
                _hits.Add(new SignificantHit(ev.Key.Class, ev.Key.Priority, ev.Key.SourceId, ev.Kind, ev.Target.Id));
            }
        }
        if (_collectAt >= 0)
        {
            long at = CloseWindow();
            if (StopOnSignificant) return Stop(at);
        }
        _scheduler.AdvanceTo(limitMs);
        return new RunResult(RunStatus.ReachedLimit, limitMs);
    }

    private long CloseWindow()
    {
        long at = _collectAt;
        _windows.Add(new DecisionWindow(_windows.Count, at, _hits.ToArray()));
        _hits.Clear();
        _collectAt = -1;
        return at;
    }

    private RunResult Stop(long at)
    {
        IsStopped = true;
        StoppedAtMs = at;
        _interrupts.Add(at);
        return new RunResult(RunStatus.Interrupted, at);
    }

    /// <summary>An order is deliverable only at the instant of the latest closed decision window (ADR-061 §3.7).</summary>
    private bool IsDeliverable(ScheduledEvent order)
        => _collectAt < 0 && _windows.Count > 0 && _windows[^1].InstantMs == order.Key.TimeMs;

    /// <summary>Issue a player order while stopped at an interrupt instant T; it is delivered AT T.</summary>
    public SubTurnOrder IssueOrder(int kind, long a, long b)
    {
        if (!IsStopped) throw new InvalidOperationException("orders can be issued only while the runner is stopped at an interrupt");
        var o = Orders.Append(StoppedAtMs, kind, a, b);
        _scheduler.ScheduleOrder(o.InstantMs, o.Seq, o.Kind, o.A, o.B);
        return o;
    }

    /// <summary>SHA-256 over a canonical little-endian encoding of every decision window (index, instant, hits in order).</summary>
    public string WindowsHashHex()
    {
        using var h = IncrementalHash.CreateHash(HashAlgorithmName.SHA256);
        var b = new byte[8];
        void L(long v) { BinaryPrimitives.WriteInt64LittleEndian(b, v); h.AppendData(b); }
        L(_windows.Count);
        foreach (var w in _windows)
        {
            L(w.Index); L(w.InstantMs); L(w.Hits.Count);
            foreach (var x in w.Hits) { L(x.Class); L(x.Priority); L(x.SourceId); L(x.Kind); L(x.TargetId); }
        }
        return Convert.ToHexString(h.GetHashAndReset());
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
