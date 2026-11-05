using System.Buffers.Binary;
using System.Security.Cryptography;
using Proto.M7.Events;
using Proto.M7.Time;

namespace Proto.M7.Demo;

/// <summary>
/// A synthetic campaign construction process (spec §2.4). NOT game content: no wonders exist in the repo
/// (recon-critic A12). Progress is a LINEAR per-turn rate integrated exactly (RateIntegrator) from an anchor
/// instant; the completion instant inside a turn is computed exactly with an integer ceiling.
/// </summary>
public sealed class ConstructionProcess
{
    public long Id { get; init; }
    public int Generation { get; set; } = 1;
    public long RemainingUnits { get; set; }
    public long UnitsPerTurn { get; set; }
    public long CurrentTurn { get; set; }
    public long AnchorMs { get; set; }
    /// <summary>Sub-unit progress as an EXACT fraction of one unit in [0, 1) (DIR5 carry; independent of turn length).</summary>
    public ExactFraction Carry { get; set; }
    public bool Completed { get; set; }
    public long CompletedAtMs { get; set; } = -1;
    /// <summary>Id (Seq) of the pending TurnStart/Complete event. Scheduler-internal, deliberately NOT in the state hash.</summary>
    public long PendingSeq { get; set; } = -1;
}

public sealed class DemoUnit
{
    public long Id { get; init; }
    public int Generation { get; set; } = 1;
    public long Hp { get; set; }
    public bool Alive { get; set; } = true;
    public long DiedAtMs { get; set; } = -1;
    public EntityRef Ref => new(Id, Generation);
}

/// <summary>What an order did and at which instant it took effect (the instant-exact delivery pin reads this).</summary>
public readonly record struct OrderEffect(long LogIndex, int Kind, long TargetId, bool Accepted, long AppliedAtMs);

/// <summary>
/// Synthetic world for the Slice B demonstrations: construction processes with significant completion
/// (Taj Mahal rule), units for simultaneity (mutual kill) and generation invalidation, and player orders.
/// </summary>
public sealed class CampaignDemoWorld : IEventWorld
{
    public const long ProcessIdBase = 1_000_000;
    private readonly CampaignCalendar _cal;
    private readonly List<ConstructionProcess> _processes = new();
    private readonly List<DemoUnit> _units = new();
    private readonly List<OrderEffect> _effects = new();
    private long _resolveScheduledAtMs = -1;

    public CampaignDemoWorld(CampaignCalendar calendar) => _cal = calendar;

    public IReadOnlyList<ConstructionProcess> Processes => _processes;
    public IReadOnlyList<DemoUnit> Units => _units;
    public IReadOnlyList<OrderEffect> Effects => _effects;

    // ---------------- construction ----------------

    public ConstructionProcess Process(long id)
    {
        long i = id - ProcessIdBase;
        if (i < 0 || i >= _processes.Count) throw new ArgumentOutOfRangeException(nameof(id));
        return _processes[(int)i];
    }

    /// <summary>Start a process at startMs (must be &gt;= scheduler now). Returns its id.</summary>
    public long AddProcess(EventScheduler s, long remainingUnits, long unitsPerTurn, long startMs)
    {
        if (remainingUnits <= 0 || unitsPerTurn < 0) throw new ArgumentOutOfRangeException(nameof(remainingUnits));
        var p = new ConstructionProcess
        {
            Id = ProcessIdBase + _processes.Count,
            RemainingUnits = remainingUnits,
            UnitsPerTurn = unitsPerTurn,
            CurrentTurn = _cal.TurnOf(new SimInstant(startMs)),
            AnchorMs = startMs,
        };
        _processes.Add(p);
        Plan(s, p);
        return p.Id;
    }

    /// <summary>Exact completion instant for the process under its current rate, or -1 if not within the current turn.</summary>
    public long CompletionInstantInCurrentTurn(ConstructionProcess p)
    {
        if (p.UnitsPerTurn == 0) return -1;
        long len = _cal.TurnLengthMs(p.CurrentTurn);
        // Smallest t with floor((U*(t-anchor) + carry)/L) >= R  <=>  t - anchor >= ceil((R*L - carry)/U).
        // Exact carry n/d: t - anchor >= ceil((R*d - n) * L / (d*U)). need > 0 whenever R > 0 (carry < 1).
        Int128 need = ((Int128)p.RemainingUnits * p.Carry.Den - p.Carry.Num) * len;
        Int128 per = (Int128)p.Carry.Den * p.UnitsPerTurn;
        Int128 dt = (need + per - 1) / per;
        Int128 t = p.AnchorMs + dt;
        long turnEnd = _cal.TurnEnd(p.CurrentTurn).Ms;
        return t <= turnEnd ? (long)t : -1;
    }

    private void Plan(EventScheduler s, ConstructionProcess p)
    {
        var self = new EntityRef(p.Id, p.Generation);
        long done = CompletionInstantInCurrentTurn(p);
        if (done >= 0)
        {
            p.PendingSeq = s.Schedule(done, EventClass.CampaignProcess, 1, p.Id, DemoKinds.ConstructionComplete, self, EntityRef.None);
            return;
        }
        long next = _cal.TurnEnd(p.CurrentTurn).Ms;
        p.PendingSeq = next >= _cal.CampaignEnd.Ms
            ? -1
            : s.Schedule(next, EventClass.CampaignProcess, 0, p.Id, DemoKinds.ConstructionTurnStart, self, EntityRef.None);
    }

    private void Integrate(ConstructionProcess p, long t)
    {
        if (t == p.AnchorMs) return;
        var carry = p.Carry;
        long accrued = RateIntegrator.IntegrateExact(p.UnitsPerTurn, t - p.AnchorMs, _cal.TurnLengthMs(p.CurrentTurn), ref carry);
        p.Carry = carry;
        p.RemainingUnits -= Math.Min(p.RemainingUnits, accrued);
        p.AnchorMs = t;
    }

    private void OnTurnStart(EventScheduler s, ConstructionProcess p, long now)
    {
        Integrate(p, now);
        p.CurrentTurn += 1;
        // DIR5 §2.5 / ADR-061 §3.10: the sub-unit residue is an exact fraction of a unit, so a change of turn length
        // leaves it unchanged; nothing is rescaled and nothing is discarded. (Superseded Slice B rule, removed:
        // carry = floor(carry * newLen / oldLen), which dropped up to one ms-numerator unit at every band change.)
        Plan(s, p);
    }

    private void OnComplete(ConstructionProcess p, long now)
    {
        Integrate(p, now);
        p.RemainingUnits = 0;
        p.Completed = true;
        p.CompletedAtMs = now;
        p.PendingSeq = -1;
    }

    // ---------------- units (simultaneity, invalidation) ----------------

    public long AddUnit(long hp)
    {
        var u = new DemoUnit { Id = _units.Count + 1, Hp = hp };
        _units.Add(u);
        return u.Id;
    }

    public DemoUnit Unit(long id) => _units[(int)(id - 1)];

    /// <summary>The id is reused by a NEW entity (generation bump): events aimed at the old one must be dropped.</summary>
    public void Reincarnate(long id, long hp)
    {
        var u = Unit(id);
        u.Generation += 1;
        u.Alive = true;
        u.Hp = hp;
        u.DiedAtMs = -1;
    }

    /// <summary>Schedule a shot (class Fire) from shooter at target with the CURRENT generations.</summary>
    public long ScheduleFire(EventScheduler s, long timeMs, long shooterId, long targetId, long damage)
        => s.Schedule(timeMs, EventClass.Fire, 0, shooterId, DemoKinds.Fire, Unit(targetId).Ref, Unit(shooterId).Ref, damage);

    // ---------------- IEventWorld ----------------

    public bool IsLive(EntityRef e)
    {
        if (e.Id >= ProcessIdBase)
        {
            long i = e.Id - ProcessIdBase;
            if (i >= _processes.Count) return false;
            var p = _processes[(int)i];
            return !p.Completed && p.Generation == e.Generation;
        }
        if (e.Id < 1 || e.Id > _units.Count) return false;
        var u = _units[(int)(e.Id - 1)];
        return u.Alive && u.Generation == e.Generation;
    }

    public void Execute(in ScheduledEvent ev, EventScheduler s)
    {
        long now = ev.Key.TimeMs;
        if (ev.Key.Class == (byte)EventClass.PlayerOrder) { ExecuteOrder(ev, s, now); return; }
        switch (ev.Kind)
        {
            case DemoKinds.ConstructionTurnStart: OnTurnStart(s, Process(ev.Target.Id), now); break;
            case DemoKinds.ConstructionComplete: OnComplete(Process(ev.Target.Id), now); break;
            case DemoKinds.Fire:
                // The shot lands in the Damage phase of the SAME instant; it is not cancelled if the shooter dies at this
                // instant (deaths resolve after all damage) — simultaneity.
                s.Schedule(now, EventClass.Damage, 0, ev.Source.Id, DemoKinds.Damage, ev.Target, EntityRef.None, ev.A);
                break;
            case DemoKinds.Damage:
                Unit(ev.Target.Id).Hp -= ev.A;
                if (_resolveScheduledAtMs != now)
                {
                    _resolveScheduledAtMs = now;
                    s.Schedule(now, EventClass.Resolve, 0, 0, DemoKinds.ResolveDeaths);
                }
                break;
            case DemoKinds.ResolveDeaths:
                foreach (var u in _units)
                {
                    if (u.Alive && u.Hp <= 0)
                    {
                        u.Alive = false;
                        u.Generation += 1;
                        u.DiedAtMs = now;
                    }
                }
                break;
            default: throw new InvalidOperationException($"unknown event kind {ev.Kind}");
        }
    }

    private void ExecuteOrder(in ScheduledEvent ev, EventScheduler s, long now)
    {
        long logIndex = ev.Key.SourceId;
        switch (ev.Kind)
        {
            case DemoKinds.OrderRush:
            {
                long id = ev.A;
                // Validated, never thrown (verify-B F6): unknown/completed process, non-positive or overflowing rush.
                bool ok = id >= ProcessIdBase && id - ProcessIdBase < _processes.Count && !Process(id).Completed
                          && ev.B > 0 && ev.B <= long.MaxValue - Process(id).UnitsPerTurn;
                if (ok)
                {
                    var p = Process(id);
                    Integrate(p, now);              // the old rate applies up to the order instant exactly
                    p.UnitsPerTurn += ev.B;
                    if (p.PendingSeq >= 0) s.Cancel(p.PendingSeq);
                    Plan(s, p);
                }
                _effects.Add(new OrderEffect(logIndex, ev.Kind, id, ok, now));
                break;
            }
            case DemoKinds.OrderStartProcess:
            {
                // Same preconditions as AddProcess, checked so an invalid order is recorded as refused (verify-B F6).
                bool ok = ev.A > 0 && ev.B >= 0;
                long id = ok ? AddProcess(s, ev.A, ev.B, now) : -1;
                _effects.Add(new OrderEffect(logIndex, ev.Kind, id, ok, now));
                break;
            }
            default:
                _effects.Add(new OrderEffect(logIndex, ev.Kind, -1, false, now));
                break;
        }
    }

    /// <summary>SHA-256 over a canonical little-endian encoding of the world state (excludes scheduler-internal ids).</summary>
    public string StateHashHex()
    {
        using var h = IncrementalHash.CreateHash(HashAlgorithmName.SHA256);
        var b = new byte[8];
        void L(long v) { BinaryPrimitives.WriteInt64LittleEndian(b, v); h.AppendData(b); }
        L(_processes.Count);
        foreach (var p in _processes)
        {
            L(p.Id); L(p.Generation); L(p.RemainingUnits); L(p.UnitsPerTurn); L(p.CurrentTurn);
            L(p.AnchorMs); L(p.Carry.Num); L(p.Carry.Den); L(p.Completed ? 1 : 0); L(p.CompletedAtMs);
        }
        L(_units.Count);
        foreach (var u in _units) { L(u.Id); L(u.Generation); L(u.Hp); L(u.Alive ? 1 : 0); L(u.DiedAtMs); }
        L(_effects.Count);
        foreach (var e in _effects) { L(e.LogIndex); L(e.Kind); L(e.TargetId); L(e.Accepted ? 1 : 0); L(e.AppliedAtMs); }
        return Convert.ToHexString(h.GetHashAndReset());
    }
}
