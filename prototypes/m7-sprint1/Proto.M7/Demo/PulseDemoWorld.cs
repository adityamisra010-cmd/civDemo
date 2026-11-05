using System.Buffers.Binary;
using System.Security.Cryptography;
using Proto.M7.Events;
using Proto.M7.Time;

namespace Proto.M7.Demo;

public readonly record struct PulseRecord(long Index, long StartMs, long EndMs, long StartCount, long EndCount);
public readonly record struct BudgetRefresh(long PulseIndex, long AtMs, long Budget);
public readonly record struct TickAction(long PulseIndex, long AtMs, bool Accepted);

/// <summary>
/// Synthetic war-pulse world (spec §2.5 + the §2.3 hybrid step): pulses run on the global ms timeline from a
/// WarPulseSchedule; inside a pulse a fixed-step Tick event fires every StepMs (integer). The per-pulse action
/// budget is refreshed at PULSE START (ledger §19 "refreshed Action Capacity"); each tick tries to spend one action.
/// Event ordering at a shared instant: PulseEnd (System, priority 0) before the next PulseStart (System, 1)
/// before that pulse's first Tick (System, 2).
/// </summary>
public sealed class PulseDemoWorld : IEventWorld
{
    private readonly WarPulseSchedule _schedule;
    private readonly long _stepMs;
    private readonly long _budgetPerPulse;
    private readonly long _stopAfterMs;
    private readonly List<PulseRecord> _pulses = new();
    private readonly List<BudgetRefresh> _refreshes = new();
    private readonly List<TickAction> _actions = new();
    private long _budget;
    private long _currentPulse = -1;
    private long _currentPulseEnd;

    /// <param name="stepMs">Fixed internal step (PLACEHOLDER input in tests).</param>
    /// <param name="budgetPerPulse">Actions per pulse (PLACEHOLDER input; 2 vs 3 is OPEN).</param>
    /// <param name="stopAfterMs">No new pulse starts at or after this instant (war ends).</param>
    public PulseDemoWorld(WarPulseSchedule schedule, long stepMs, long budgetPerPulse, long stopAfterMs)
    {
        if (stepMs <= 0 || budgetPerPulse < 0) throw new ArgumentOutOfRangeException(nameof(stepMs));
        _schedule = schedule;
        _stepMs = stepMs;
        _budgetPerPulse = budgetPerPulse;
        _stopAfterMs = stopAfterMs;
    }

    public IReadOnlyList<PulseRecord> Pulses => _pulses;
    public IReadOnlyList<BudgetRefresh> Refreshes => _refreshes;
    public IReadOnlyList<TickAction> Actions => _actions;

    public void Begin(EventScheduler s, long warStartMs)
    {
        var p = _schedule.First(warStartMs);
        SchedulePulse(s, p);
    }

    private void SchedulePulse(EventScheduler s, WarPulse p)
    {
        if (p.StartMs >= _stopAfterMs) return;
        s.Schedule(p.StartMs, EventClass.System, 1, 0, DemoKinds.PulseStart, p.Index, p.StartMs, p.EndMs);
    }

    public bool IsLive(EntityRef entity) => true;

    public void Execute(in ScheduledEvent ev, EventScheduler s)
    {
        long now = ev.Key.TimeMs;
        switch (ev.Kind)
        {
            case DemoKinds.PulseStart:
            {
                long index = ev.A, start = ev.B, end = ev.C;
                _pulses.Add(new PulseRecord(index, start, end, 1, 0));
                _currentPulse = index;
                _currentPulseEnd = end;
                _budget = _budgetPerPulse;
                _refreshes.Add(new BudgetRefresh(index, now, _budget));
                s.Schedule(now, EventClass.System, 2, 0, DemoKinds.PulseTick, index);
                s.Schedule(end, EventClass.System, 0, 0, DemoKinds.PulseEnd, index, start, end);
                break;
            }
            case DemoKinds.PulseTick:
            {
                bool ok = _budget > 0;
                if (ok) _budget -= 1;
                _actions.Add(new TickAction(ev.A, now, ok));
                long next = now + _stepMs;
                if (next < _currentPulseEnd) s.Schedule(next, EventClass.System, 2, 0, DemoKinds.PulseTick, ev.A);
                break;
            }
            case DemoKinds.PulseEnd:
            {
                int i = _pulses.Count - 1;
                var r = _pulses[i];
                if (r.Index != ev.A) throw new InvalidOperationException("pulse end out of order");
                _pulses[i] = r with { EndCount = r.EndCount + 1 };
                var ended = new WarPulse(ev.A, ev.B, ev.C);
                // The war stops at stopAfterMs or at the campaign end, whichever comes first (verify-B F7):
                // check BEFORE asking the schedule for a pulse that would start outside the campaign.
                if (ended.EndMs < _stopAfterMs && _schedule.HasNext(ended)) SchedulePulse(s, _schedule.Next(ended));
                break;
            }
            default: throw new InvalidOperationException($"unknown kind {ev.Kind}");
        }
    }

    public string StateHashHex()
    {
        using var h = IncrementalHash.CreateHash(HashAlgorithmName.SHA256);
        var b = new byte[8];
        void L(long v) { BinaryPrimitives.WriteInt64LittleEndian(b, v); h.AppendData(b); }
        L(_pulses.Count);
        foreach (var p in _pulses) { L(p.Index); L(p.StartMs); L(p.EndMs); L(p.StartCount); L(p.EndCount); }
        L(_refreshes.Count);
        foreach (var r in _refreshes) { L(r.PulseIndex); L(r.AtMs); L(r.Budget); }
        L(_actions.Count);
        foreach (var a in _actions) { L(a.PulseIndex); L(a.AtMs); L(a.Accepted ? 1 : 0); }
        L(_currentPulse);
        return Convert.ToHexString(h.GetHashAndReset());
    }
}
