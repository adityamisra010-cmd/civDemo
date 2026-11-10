using System.Buffers.Binary;
using System.Security.Cryptography;
using Proto.M7.Events;
using Proto.M7.Time;

namespace Proto.M7.War;

/// <summary>Event and order kinds of the T2 war world (disjoint from DemoKinds).</summary>
public static class WarKinds
{
    public const int PulseEnd = 40;
    public const int TurnStart = 41;
    public const int PulseStart = 42;
    /// <summary>Test device: a significant event visible to exactly one polity (Target.Id), used to open a window at a chosen instant.</summary>
    public const int Probe = 43;

    /// <summary>War Pulse order: set the issuer's supply consumption rate (goods per sim-year) in war W. A = Pack(issuer, W), B = rate.</summary>
    public const int OrderSupplyRate = 140;
    /// <summary>Campaign-turn order: set the issuer's war upkeep rate (money per sim-year per war). A = Pack(issuer, 0), B = rate.</summary>
    public const int OrderUpkeepRate = 141;

    /// <summary>T3 agent actions (ActionKinds.OrderBase + ActionKind): A = Pack(issuer, agentId), B = cell / target agent / amount.</summary>
    public const int ActMove = ActionKinds.OrderBase + (int)ActionKind.Move;
    public const int ActAttack = ActionKinds.OrderBase + (int)ActionKind.Attack;
    public const int ActRetreat = ActionKinds.OrderBase + (int)ActionKind.Retreat;
    public const int ActRecruit = ActionKinds.OrderBase + (int)ActionKind.Recruit;
    /// <summary>[AP] Lane AP concealment and preparation actions (D-082 DA8): A = Pack(issuer, agentId), B = 0.</summary>
    public const int ActHide = ActionKinds.OrderBase + (int)ActionKind.Hide;
    public const int ActCamouflage = ActionKinds.OrderBase + (int)ActionKind.Camouflage;
    public const int ActEntrench = ActionKinds.OrderBase + (int)ActionKind.Entrench;

    /// <summary>Order operand packing (prototype-local; the kernel OrderRecord is untouched): A = issuer * 65536 + warId.</summary>
    public const long PolityStride = 65_536;
    public static long Pack(int issuer, int warId) => issuer * PolityStride + warId;
}

/// <summary>Reasons an order is executed as a recorded refusal (EventRules R3(f): no state change).</summary>
public static class WarRefusal
{
    public const int NotARecipient = 1;
    public const int BadTarget = 2;
    public const int WarEnded = 3;
    public const int BadValue = 4;
    public const int UnknownKind = 5;
    // T3 action economy (ActionRules): a refused action changes no state.
    public const int SlotsExhausted = 10;
    public const int CapacityExhausted = 11;
    public const int NotInActionSet = 12;
    public const int KindRepeated = 13;
    public const int CombinationForbidden = 14;
    public const int HitAndRun = 15;
    public const int NotOwnAgent = 16;
    public const int OutOfReach = 17;
    // [AP] lane AP concealment and preparation (ActionRules; docs/m7-plan/concealment-actions-proposal.md §3): a refused action
    // changes no state.
    /// <summary>An Attack on an agent the issuer's polity does not detect (it cannot be targeted).</summary>
    public const int TargetUndetected = 18;
    /// <summary>A Hide in a cell whose terrain does not conceal (multiplier 1).</summary>
    public const int NoCover = 19;
    /// <summary>A Hide or Camouflage with an enemy agent in contact (within the agent's attack range). [AP round 2] Only an enemy agent
    /// the issuer's polity detects counts: an unseen one never refuses the order.</summary>
    public const int EnemyInContact = 20;
    /// <summary>A Hide or Camouflage while an enemy polity detects the agent: one cannot vanish under observation. [AP round 2] Only a
    /// watcher the issuer's polity detects refuses; an unseen watcher spots the new concealment at the order's instant instead.</summary>
    public const int Observed = 21;
    /// <summary>The agent already holds the state (hidden, camouflaged, entrenched): ordering it again would only spend capacity.</summary>
    public const int AlreadyInState = 22;
    /// <summary>An Entrench where the ground cannot be dug (FieldWorks.Diggable).</summary>
    public const int CannotDig = 23;
}

/// <summary>
/// A war: its own pulse clock starts at StartMs (its phase is its own, ADR-061 §3.11 Schedule); no pulse starts at or
/// after StopAfterMs. Participants strictly ascending; SupplyRatePerYear[i] is participant i's opening supply rate.
/// The theatre cell (TheaterX, TheaterY) is what observers must see (WarFog).
/// </summary>
public sealed record WarSpec(int Id, long StartMs, long StopAfterMs, int[] Participants, long TheaterX, long TheaterY, long[] SupplyRatePerYear);

/// <summary>
/// One sub-step of a war: the part of pulse PulseIndex that lies in campaign turn Turn, [StartMs, EndMs). EndsAtSplit is
/// true when the sub-step was closed by the straddle split at a turn boundary, false when it was closed by the pulse end.
/// PulseElapsedAfter is the exact fraction of the pulse completed at EndMs (1 at the pulse end).
/// </summary>
public readonly record struct SubStepRecord(int WarId, long PulseIndex, int Part, long Turn, long StartMs, long EndMs, bool EndsAtSplit,
                                            ExactFraction PulseElapsedAfter, long UpkeepDue, long UpkeepPaid, long SupplyDue, long SupplyPaid);

public readonly record struct WarPulseRecord(int WarId, long Index, long StartMs, long EndMs, int Parts);

/// <summary>An order taking effect: EffectiveIndex is the pulse index (supply order) or the campaign turn (upkeep order).</summary>
public readonly record struct LatchRecord(int LogIndex, int Issuer, int Kind, int WarId, long IssuedMs, long EffectiveMs, long EffectiveIndex, long Value);

public readonly record struct OrderRefusal(int LogIndex, int Issuer, int Kind, long AtMs, int Reason);

/// <summary>What a polity learned: a pulse of war WarId started or ended at AtMs (only if it could see the war at AtMs).</summary>
public readonly record struct KnowledgeEntry(long AtMs, int WarId, long PulseIndex, int Kind);

public readonly record struct TurnSnapshot(long Turn, long AtMs, long[] Balances);

/// <summary>A decision window as one polity receives it: only the hits it can see under fog.</summary>
public sealed record PolityWindow(long InstantMs, SignificantHit[] Hits);

/// <summary>
/// Phase C lane T2 world: War Pulses sub-stepped across campaign-turn boundaries, per-pulse economics through a conserved
/// ledger, War Pulse locality under fog, per-polity window recipients, and the two boundary order-delivery semantics.
///
/// EVENTS at one instant X (all class System; ties between wars broken by SourceId = war id, R2):
///   priority 0  PulseEnd(w)   settles the last sub-step [LastSettled, X) of w's pulse, checks the parts telescope to 1;
///   priority 1  TurnStart     the STRADDLE SPLIT: for every war whose pulse began before X and ends after X, settles
///                             [LastSettled, X); then latches pending turn orders and snapshots the ledger;
///   priority 2  PulseStart(w) latches pending pulse orders and schedules the pulse end.
/// So every sub-step lies inside one pulse and one turn, consecutive sub-steps are contiguous, and a pulse's parts sum to
/// its length W exactly (integer ms; the elapsed fraction is an ExactFraction and reaches exactly 1).
///
/// ECONOMICS per sub-step of length dt (DIR5 §2.4 "per operational pulse"; ADR-061 §3.10 T3): for each participant, in
/// ascending polity id, due = RateIntegrator.IntegrateExact(ratePerYear, dt, MsPerYear, ref carry) — the rate is per
/// sim-year and dtYears = dt / MsPerYear is exact; the carry is an exact fraction of one unit, so it is unchanged by any
/// turn-length or era change and the integer total over any partition equals floor(sum of rate*dt / MsPerYear).
/// Paid = min(due, balance) moves through WarLedger.Transfer to a sink account; any shortfall is recorded as arrears.
///
/// ORDER DELIVERY (orders arrive only at a window instant T, class PlayerOrder, after every world event at T):
///   War Pulse order: takes effect at the first pulse start B of that war with B &gt;= T. If T is the current pulse's
///     start (zero simulated time has passed in it) it applies to the current pulse; otherwise from the next pulse.
///   Turn order: takes effect at the first turn start S with S &gt;= T, by the same rule.
///   An issuer that is not a recipient of the window at T (fog) gets a recorded refusal.
///   T3 agent action: applies AT T and spends a slot of the enclosing window that contains T (ActionRules): at a pulse
///     start X the agent is already in pulse n+1's fresh window; at X - 1 it is still in pulse n's. Same for turn starts.
///     The fog gate above applies first, so a polity that cannot see the triggering event gets no action from it.
/// </summary>
public sealed class WarTheaterWorld : IEventWorld
{
    private sealed class WarState
    {
        public WarState(WarSpec spec)
        {
            Spec = spec;
            int n = spec.Participants.Length;
            SupplyRate = (long[])spec.SupplyRatePerYear.Clone();
            SupplyPending = new long[n];
            SupplyPendingLog = new int[n];
            SupplyPendingIssued = new long[n];
            for (int i = 0; i < n; i++) SupplyPendingLog[i] = -1;
            UpkeepCarry = new ExactFraction[n];
            SupplyCarry = new ExactFraction[n];
            UpkeepArrears = new long[n];
            SupplyArrears = new long[n];
        }

        public readonly WarSpec Spec;
        public WarPulse Pulse;
        public bool Active, Ended;
        public long LastSettledMs;
        public ExactFraction Elapsed;
        public int Part;
        public readonly long[] SupplyRate, SupplyPending, SupplyPendingIssued, UpkeepArrears, SupplyArrears;
        public readonly int[] SupplyPendingLog;
        public readonly ExactFraction[] UpkeepCarry, SupplyCarry;
    }

    private readonly CampaignCalendar _cal;
    private readonly WarPulseSchedule _schedule;
    private readonly int _polityCount;
    private readonly WarState[] _wars;
    private readonly WarFog _fog;
    private readonly SignificantKinds _significant;
    private readonly WarLedger _ledger = new();
    private readonly int[] _treasury, _granary;
    private readonly int _upkeepSink, _supplySink;
    private readonly long[] _upkeepRate, _upkeepPending, _upkeepPendingIssued;
    private readonly int[] _upkeepPendingLog;
    private readonly List<SubStepRecord> _subSteps = new();
    private readonly List<WarPulseRecord> _pulses = new();
    private readonly List<LatchRecord> _latches = new();
    private readonly List<OrderRefusal> _refusals = new();
    private readonly List<KnowledgeEntry>[] _knowledge;
    private readonly List<TurnSnapshot> _snapshots = new();
    private long _sigInstant = -1;
    private ulong _sigMask;
    private readonly ActionWindowBook? _book;
    private readonly int _recruitSink = -1;

    public WarTheaterWorld(CampaignCalendar calendar, WarPulseSchedule schedule, int polityCount, WarSpec[] wars, WarFog fog,
                           SignificantKinds significant, long[] upkeepRatePerYear, long openingMoney, long openingGoods,
                           ActionLayerSpec? actions = null)
    {
        if (polityCount <= 0 || polityCount > 64) throw new ArgumentOutOfRangeException(nameof(polityCount), "1..64 polities (recipient mask)");
        if (upkeepRatePerYear.Length != polityCount) throw new ArgumentException("one upkeep rate per polity");
        _cal = calendar;
        _schedule = schedule;
        _polityCount = polityCount;
        _fog = fog;
        _significant = significant;
        _wars = new WarState[wars.Length];
        foreach (var w in wars)
        {
            if (w.Id < 0 || w.Id >= wars.Length || _wars[w.Id] is not null) throw new ArgumentException("war ids must be 0..n-1, each once");
            if (w.Participants.Length == 0 || w.Participants.Length != w.SupplyRatePerYear.Length) throw new ArgumentException("participants/rates mismatch");
            for (int i = 0; i < w.Participants.Length; i++)
            {
                if (w.Participants[i] < 0 || w.Participants[i] >= polityCount) throw new ArgumentOutOfRangeException(nameof(wars));
                if (i > 0 && w.Participants[i] <= w.Participants[i - 1]) throw new ArgumentException("participants must be strictly ascending");
                if (w.SupplyRatePerYear[i] < 0) throw new ArgumentOutOfRangeException(nameof(wars));
            }
            _wars[w.Id] = new WarState(w);
        }
        _upkeepRate = (long[])upkeepRatePerYear.Clone();
        _upkeepPending = new long[polityCount];
        _upkeepPendingIssued = new long[polityCount];
        _upkeepPendingLog = new int[polityCount];
        _treasury = new int[polityCount];
        _granary = new int[polityCount];
        _knowledge = new List<KnowledgeEntry>[polityCount];
        for (int p = 0; p < polityCount; p++)
        {
            if (_upkeepRate[p] < 0) throw new ArgumentOutOfRangeException(nameof(upkeepRatePerYear));
            _upkeepPendingLog[p] = -1;
            _treasury[p] = _ledger.Open(Commodity.Money, openingMoney);
            _granary[p] = _ledger.Open(Commodity.Goods, openingGoods);
            _knowledge[p] = new List<KnowledgeEntry>();
        }
        _upkeepSink = _ledger.Open(Commodity.Money, 0);
        _supplySink = _ledger.Open(Commodity.Goods, 0);
        if (actions is not null && actions.Agents.Length > 0)
        {
            // T3: the sink opens only with an action layer, so a world without agents keeps T2's ledger and hashes.
            _recruitSink = _ledger.Open(Commodity.Money, 0);
            var specs = new WarSpec[_wars.Length];
            for (int i = 0; i < specs.Length; i++) specs[i] = _wars[i].Spec;
            _book = new ActionWindowBook(actions, polityCount, specs, OperationalWindow, _ledger, _treasury, _recruitSink);
        }
    }

    /// <summary>The T3 action layer (null when the world has no agents).</summary>
    public ActionWindowBook? Actions => _book;
    public int RecruitSink => _recruitSink;

    /// <summary>The current pulse index of war warId if it is in progress (started, not ended), else -1 (campaign layer).</summary>
    private long OperationalWindow(int warId)
    {
        if (warId < 0) return -1;
        var w = _wars[warId];
        return w.Ended || w.Pulse.EndMs == 0 ? -1 : w.Pulse.Index;
    }

    public WarLedger Ledger => _ledger;
    public int TreasuryOf(int polity) => _treasury[polity];
    public int GranaryOf(int polity) => _granary[polity];
    public int UpkeepSink => _upkeepSink;
    public int SupplySink => _supplySink;
    public IReadOnlyList<SubStepRecord> SubSteps => _subSteps;
    public IReadOnlyList<WarPulseRecord> PulseRecords => _pulses;
    public IReadOnlyList<LatchRecord> Latches => _latches;
    public IReadOnlyList<OrderRefusal> Refusals => _refusals;
    public IReadOnlyList<TurnSnapshot> TurnSnapshots => _snapshots;
    public IReadOnlyList<KnowledgeEntry> KnowledgeOf(int polity) => _knowledge[polity];
    public long UpkeepRateOf(int polity) => _upkeepRate[polity];
    public long SupplyRateOf(int warId, int participantIndex) => _wars[warId].SupplyRate[participantIndex];
    public long UpkeepArrearsOf(int warId, int participantIndex) => _wars[warId].UpkeepArrears[participantIndex];
    public bool IsEnded(int warId) => _wars[warId].Ended;
    /// <summary>Recipient mask of the latest window (the instant of the latest executed significant event).</summary>
    public ulong LatestWindowRecipients => _sigMask;
    public long LatestWindowInstant => _sigInstant;

    /// <summary>Schedule every war's first pulse and the first turn start at or after the scheduler's clock.</summary>
    public void Begin(EventScheduler s)
    {
        foreach (var w in _wars)
        {
            if (w.Spec.StartMs >= w.Spec.StopAfterMs) { w.Ended = true; continue; }
            var p = _schedule.First(w.Spec.StartMs);
            s.Schedule(p.StartMs, EventClass.System, 2, w.Spec.Id, WarKinds.PulseStart, p.Index, p.StartMs, p.EndMs);
        }
        long now = s.NowMs;
        if (now >= _cal.CampaignEnd.Ms) { _book?.Begin(now, _cal.TotalTurns); return; }
        long turn = _cal.TurnOf(new SimInstant(now));
        _book?.Begin(now, turn);
        if (_cal.TurnStart(turn).Ms < now) turn++;
        if (turn < _cal.TotalTurns) s.Schedule(_cal.TurnStart(turn).Ms, EventClass.System, 1, 0, WarKinds.TurnStart, turn);
    }

    /// <summary>Test device: open a window at atMs that only the given polity receives.</summary>
    public static void ScheduleProbe(EventScheduler s, long atMs, int polity)
        => s.Schedule(atMs, EventClass.Significant, 0, 0, WarKinds.Probe, new EntityRef(polity, 0), EntityRef.None);

    public bool IsLive(EntityRef entity) => true;

    // ---- lane E (WarEcon) addition, fix round 1: a read-only participant query; nothing else in this file changed ----
    /// <summary>True when the polity is a participant of the war (WarSpec.Participants).</summary>
    public bool IsParticipant(int warId, int polity) => Array.BinarySearch(_wars[warId].Spec.Participants, polity) >= 0;

    /// <summary>The fog rule (WarFog): a participant always sees its war; anyone else only through a present observer.</summary>
    public bool CanSee(int polity, int warId, long t)
    {
        var spec = _wars[warId].Spec;
        if (Array.BinarySearch(spec.Participants, polity) >= 0) return true;
        return _fog.Observes(polity, spec.TheaterX, spec.TheaterY, t);
    }

    public ulong VisibleMask(int warId, long t)
    {
        ulong m = 0;
        for (int p = 0; p < _polityCount; p++) if (CanSee(p, warId, t)) m |= 1UL << p;
        return m;
    }

    /// <summary>Which polities receive a significant event of this kind/source/target at instant t.</summary>
    public ulong RecipientsOf(int kind, long sourceId, long targetId, long t) => kind switch
    {
        WarKinds.Probe => 1UL << (int)targetId,
        WarKinds.PulseStart or WarKinds.PulseEnd => VisibleMask((int)sourceId, t),
        WarKinds.TurnStart => _polityCount == 64 ? ulong.MaxValue : (1UL << _polityCount) - 1,
        _ => throw new InvalidOperationException($"no recipient rule for kind {kind}"),
    };

    public ulong RecipientsOf(SignificantHit hit, long instantMs) => RecipientsOf(hit.Kind, hit.SourceId, hit.TargetId, instantMs);

    /// <summary>The windows one polity receives: each world window filtered to the hits it can see; empty ones dropped.</summary>
    public List<PolityWindow> WindowsFor(int polity, IReadOnlyList<DecisionWindow> windows)
    {
        var result = new List<PolityWindow>();
        ulong bit = 1UL << polity;
        foreach (var w in windows)
        {
            var hits = new List<SignificantHit>();
            foreach (var h in w.Hits) if ((RecipientsOf(h, w.InstantMs) & bit) != 0) hits.Add(h);
            if (hits.Count > 0) result.Add(new PolityWindow(w.InstantMs, hits.ToArray()));
        }
        return result;
    }

    public void Execute(in ScheduledEvent ev, EventScheduler s)
    {
        long now = ev.Key.TimeMs;
        if (ev.Key.Class == (byte)EventClass.PlayerOrder) { ExecuteOrder(in ev, now); return; }
        switch (ev.Kind)
        {
            case WarKinds.PulseEnd: OnPulseEnd(in ev, s, now); break;
            case WarKinds.TurnStart: OnTurnStart(in ev, s, now); break;
            case WarKinds.PulseStart: OnPulseStart(in ev, s, now); break;
            case WarKinds.Probe: break;
            default: throw new InvalidOperationException($"unknown kind {ev.Kind}");
        }
        if (_significant.Contains(ev.Kind))
        {
            if (now != _sigInstant) { _sigInstant = now; _sigMask = 0; }
            _sigMask |= RecipientsOf(ev.Kind, ev.Key.SourceId, ev.Target.Id, now);
        }
    }

    private void OnPulseStart(in ScheduledEvent ev, EventScheduler s, long now)
    {
        var w = _wars[(int)ev.Key.SourceId];
        var p = new WarPulse(ev.A, ev.B, ev.C);
        if (w.Active || w.Ended || p.StartMs != now) throw new InvalidOperationException("pulse start out of order");
        w.Pulse = p;
        w.Active = true;
        w.LastSettledMs = p.StartMs;
        w.Elapsed = ExactFraction.Zero;
        w.Part = 0;
        for (int i = 0; i < w.SupplyRate.Length; i++)
        {
            if (w.SupplyPendingLog[i] < 0) continue;
            w.SupplyRate[i] = w.SupplyPending[i];
            _latches.Add(new LatchRecord(w.SupplyPendingLog[i], w.Spec.Participants[i], WarKinds.OrderSupplyRate, w.Spec.Id,
                                         w.SupplyPendingIssued[i], now, p.Index, w.SupplyPending[i]));
            w.SupplyPendingLog[i] = -1;
        }
        Know(w.Spec.Id, p.Index, WarKinds.PulseStart, now);
        _book?.OnPulseStart(now, w.Spec.Id, p.Index);
        s.Schedule(p.EndMs, EventClass.System, 0, w.Spec.Id, WarKinds.PulseEnd, p.Index, p.StartMs, p.EndMs);
    }

    private void OnTurnStart(in ScheduledEvent ev, EventScheduler s, long now)
    {
        long turn = ev.A;
        // The straddle split: every pulse in progress across this boundary is cut here (DIR5 §2.4 "Use sub-stepping").
        foreach (var w in _wars)
            if (w.Active && w.LastSettledMs < now) Settle(w, now, split: true);
        for (int p = 0; p < _polityCount; p++)
        {
            if (_upkeepPendingLog[p] < 0) continue;
            _upkeepRate[p] = _upkeepPending[p];
            _latches.Add(new LatchRecord(_upkeepPendingLog[p], p, WarKinds.OrderUpkeepRate, 0, _upkeepPendingIssued[p], now, turn, _upkeepPending[p]));
            _upkeepPendingLog[p] = -1;
        }
        _snapshots.Add(new TurnSnapshot(turn, now, _ledger.Snapshot()));
        _book?.OnTurnStart(now, turn);
        if (turn + 1 < _cal.TotalTurns)
            s.Schedule(_cal.TurnStart(turn + 1).Ms, EventClass.System, 1, 0, WarKinds.TurnStart, turn + 1);
    }

    private void OnPulseEnd(in ScheduledEvent ev, EventScheduler s, long now)
    {
        var w = _wars[(int)ev.Key.SourceId];
        if (!w.Active || w.Pulse.Index != ev.A || w.Pulse.EndMs != now) throw new InvalidOperationException("pulse end out of order");
        Settle(w, now, split: false);
        if (w.Elapsed != ExactFraction.One) throw new InvalidOperationException($"pulse parts do not telescope to one: {w.Elapsed}");
        _pulses.Add(new WarPulseRecord(w.Spec.Id, w.Pulse.Index, w.Pulse.StartMs, w.Pulse.EndMs, w.Part));
        Know(w.Spec.Id, w.Pulse.Index, WarKinds.PulseEnd, now);
        w.Active = false;
        if (w.Pulse.EndMs < w.Spec.StopAfterMs && _schedule.HasNext(w.Pulse))
        {
            var n = _schedule.Next(w.Pulse);
            s.Schedule(n.StartMs, EventClass.System, 2, w.Spec.Id, WarKinds.PulseStart, n.Index, n.StartMs, n.EndMs);
        }
        else w.Ended = true;
    }

    /// <summary>Close the sub-step [LastSettled, upTo) of w's current pulse: exact progress, then per-pulse economics.</summary>
    private void Settle(WarState w, long upTo, bool split)
    {
        long a = w.LastSettledMs, dt = upTo - a;
        long length = w.Pulse.EndMs - w.Pulse.StartMs;
        if (dt <= 0 || upTo > w.Pulse.EndMs) throw new InvalidOperationException("empty or overlong sub-step");
        long turn = _cal.TurnOf(new SimInstant(a));
        if (turn < _cal.TotalTurns - 1 && upTo > _cal.TurnEnd(turn).Ms) throw new InvalidOperationException("sub-step crosses a turn boundary");
        w.Elapsed += ExactFraction.Of(dt, length);
        // At a split the carried progress must be exactly the elapsed proportion of the pulse (T1 ProportionalCarry).
        if (split && w.Elapsed != ProportionalCarry.ElapsedFraction(upTo - w.Pulse.StartMs, length))
            throw new InvalidOperationException("split progress is not the exact elapsed proportion");

        long uDue = 0, uPaid = 0, sDue = 0, sPaid = 0;
        for (int i = 0; i < w.Spec.Participants.Length; i++)
        {
            int p = w.Spec.Participants[i];
            long du = RateIntegrator.IntegrateExact(_upkeepRate[p], dt, SimInstant.MsPerYear, ref w.UpkeepCarry[i]);
            long pu = Math.Min(du, _ledger.Balance(_treasury[p]));
            _ledger.Transfer(_treasury[p], _upkeepSink, pu);
            w.UpkeepArrears[i] += du - pu;
            long ds = RateIntegrator.IntegrateExact(w.SupplyRate[i], dt, SimInstant.MsPerYear, ref w.SupplyCarry[i]);
            long ps = Math.Min(ds, _ledger.Balance(_granary[p]));
            _ledger.Transfer(_granary[p], _supplySink, ps);
            w.SupplyArrears[i] += ds - ps;
            uDue += du; uPaid += pu; sDue += ds; sPaid += ps;
        }
        _subSteps.Add(new SubStepRecord(w.Spec.Id, w.Pulse.Index, w.Part, turn, a, upTo, split, w.Elapsed, uDue, uPaid, sDue, sPaid));
        w.LastSettledMs = upTo;
        w.Part++;
    }

    private void Know(int warId, long pulseIndex, int kind, long t)
    {
        for (int p = 0; p < _polityCount; p++)
            if (CanSee(p, warId, t)) _knowledge[p].Add(new KnowledgeEntry(t, warId, pulseIndex, kind));
    }

    private void ExecuteOrder(in ScheduledEvent ev, long now)
    {
        int log = (int)ev.Key.SourceId;
        if (ev.A < 0) { Refuse(log, -1, ev.Kind, now, WarRefusal.BadTarget); return; }
        long issuerL = ev.A / WarKinds.PolityStride;
        int warId = (int)(ev.A % WarKinds.PolityStride);
        if (issuerL >= _polityCount) { Refuse(log, -1, ev.Kind, now, WarRefusal.BadTarget); return; }
        int issuer = (int)issuerL;
        // Per-polity recipients (fog): only a polity that received the window at this instant may act at it.
        if (now != _sigInstant || (_sigMask & (1UL << issuer)) == 0) { Refuse(log, issuer, ev.Kind, now, WarRefusal.NotARecipient); return; }
        if (ev.B < 0) { Refuse(log, issuer, ev.Kind, now, WarRefusal.BadValue); return; }
        if (_book is not null && ActionKinds.IsAction(ev.Kind))
        {
            // T3: an agent action spends a slot of the issuer's enclosing window and the agent's Action Capacity.
            int r = _book.TryAct(log, issuer, ev.Kind, warId, ev.B, now);
            if (r != 0) Refuse(log, issuer, ev.Kind, now, r);
            return;
        }
        switch (ev.Kind)
        {
            case WarKinds.OrderSupplyRate:
            {
                if (warId >= _wars.Length) { Refuse(log, issuer, ev.Kind, now, WarRefusal.BadTarget); return; }
                var w = _wars[warId];
                int i = Array.BinarySearch(w.Spec.Participants, issuer);
                if (i < 0) { Refuse(log, issuer, ev.Kind, now, WarRefusal.BadTarget); return; }
                if (w.Ended) { Refuse(log, issuer, ev.Kind, now, WarRefusal.WarEnded); return; }
                if (w.Active && now == w.Pulse.StartMs)
                {
                    // T is the current pulse's start: no simulated time has passed in it, so it applies to this pulse.
                    w.SupplyRate[i] = ev.B;
                    w.SupplyPendingLog[i] = -1;
                    _latches.Add(new LatchRecord(log, issuer, ev.Kind, warId, now, now, w.Pulse.Index, ev.B));
                }
                else
                {
                    w.SupplyPending[i] = ev.B;
                    w.SupplyPendingLog[i] = log;
                    w.SupplyPendingIssued[i] = now;
                }
                return;
            }
            case WarKinds.OrderUpkeepRate:
            {
                if (now >= _cal.CampaignEnd.Ms) { Refuse(log, issuer, ev.Kind, now, WarRefusal.BadValue); return; }
                long turn = _cal.TurnOf(new SimInstant(now));
                if (_cal.TurnStart(turn).Ms == now)
                {
                    _upkeepRate[issuer] = ev.B;
                    _upkeepPendingLog[issuer] = -1;
                    _latches.Add(new LatchRecord(log, issuer, ev.Kind, 0, now, now, turn, ev.B));
                }
                else
                {
                    _upkeepPending[issuer] = ev.B;
                    _upkeepPendingLog[issuer] = log;
                    _upkeepPendingIssued[issuer] = now;
                }
                return;
            }
            default: Refuse(log, issuer, ev.Kind, now, WarRefusal.UnknownKind); return;
        }
    }

    private void Refuse(int log, int issuer, int kind, long now, int reason) => _refusals.Add(new OrderRefusal(log, issuer, kind, now, reason));

    public string StateHashHex()
    {
        using var h = IncrementalHash.CreateHash(HashAlgorithmName.SHA256);
        var b = new byte[8];
        void L(long v) { BinaryPrimitives.WriteInt64LittleEndian(b, v); h.AppendData(b); }
        void F(ExactFraction f) { L(f.Num); L(f.Den); }
        _ledger.AppendTo(h);
        foreach (var w in _wars)
        {
            L(w.Spec.Id); L(w.Active ? 1 : 0); L(w.Ended ? 1 : 0); L(w.Pulse.Index); L(w.Pulse.StartMs); L(w.Pulse.EndMs);
            L(w.LastSettledMs); F(w.Elapsed); L(w.Part);
            for (int i = 0; i < w.SupplyRate.Length; i++)
            {
                L(w.SupplyRate[i]); L(w.SupplyPending[i]); L(w.SupplyPendingLog[i]); L(w.SupplyPendingIssued[i]);
                F(w.UpkeepCarry[i]); F(w.SupplyCarry[i]); L(w.UpkeepArrears[i]); L(w.SupplyArrears[i]);
            }
        }
        for (int p = 0; p < _polityCount; p++) { L(_upkeepRate[p]); L(_upkeepPending[p]); L(_upkeepPendingLog[p]); L(_upkeepPendingIssued[p]); }
        L(_subSteps.Count);
        foreach (var x in _subSteps)
        {
            L(x.WarId); L(x.PulseIndex); L(x.Part); L(x.Turn); L(x.StartMs); L(x.EndMs); L(x.EndsAtSplit ? 1 : 0);
            F(x.PulseElapsedAfter); L(x.UpkeepDue); L(x.UpkeepPaid); L(x.SupplyDue); L(x.SupplyPaid);
        }
        L(_pulses.Count);
        foreach (var x in _pulses) { L(x.WarId); L(x.Index); L(x.StartMs); L(x.EndMs); L(x.Parts); }
        L(_latches.Count);
        foreach (var x in _latches) { L(x.LogIndex); L(x.Issuer); L(x.Kind); L(x.WarId); L(x.IssuedMs); L(x.EffectiveMs); L(x.EffectiveIndex); L(x.Value); }
        L(_refusals.Count);
        foreach (var x in _refusals) { L(x.LogIndex); L(x.Issuer); L(x.Kind); L(x.AtMs); L(x.Reason); }
        for (int p = 0; p < _polityCount; p++)
        {
            L(_knowledge[p].Count);
            foreach (var k in _knowledge[p]) { L(k.AtMs); L(k.WarId); L(k.PulseIndex); L(k.Kind); }
        }
        L(_snapshots.Count);
        foreach (var x in _snapshots) { L(x.Turn); L(x.AtMs); foreach (var v in x.Balances) L(v); }
        L(_sigInstant); L((long)_sigMask);
        _book?.AppendTo(h);   // T3 action layer; absent (no bytes) in a world without agents
        return Convert.ToHexString(h.GetHashAndReset());
    }

    /// <summary>
    /// Everything one polity can know: its knowledge log, the windows it receives (instant and visible hits; NOT the
    /// world window index, which counts windows of wars it cannot see) and its own account balances.
    /// </summary>
    public string PolityViewHashHex(int polity, IReadOnlyList<DecisionWindow> windows)
    {
        using var h = IncrementalHash.CreateHash(HashAlgorithmName.SHA256);
        var b = new byte[8];
        void L(long v) { BinaryPrimitives.WriteInt64LittleEndian(b, v); h.AppendData(b); }
        L(_knowledge[polity].Count);
        foreach (var k in _knowledge[polity]) { L(k.AtMs); L(k.WarId); L(k.PulseIndex); L(k.Kind); }
        var mine = WindowsFor(polity, windows);
        L(mine.Count);
        foreach (var w in mine)
        {
            L(w.InstantMs); L(w.Hits.Length);
            foreach (var x in w.Hits) { L(x.Class); L(x.Priority); L(x.SourceId); L(x.Kind); L(x.TargetId); }
        }
        L(_ledger.Balance(_treasury[polity])); L(_ledger.Balance(_granary[polity]));
        return Convert.ToHexString(h.GetHashAndReset());
    }
}

/// <summary>
/// Drives a Runner for one human polity: stops only at windows that polity receives. A window it does not receive is
/// still recorded (R4: windows are a function of world state) and resumed at once; a stop consumes zero simulated time,
/// so resuming without orders changes nothing.
/// </summary>
public sealed class WarSession
{
    public WarSession(Runner runner, WarTheaterWorld world)
    {
        Runner = runner;
        World = world;
    }

    public Runner Runner { get; }
    public WarTheaterWorld World { get; }

    public RunResult RunUntilWindowFor(int polity, long limitMs)
    {
        while (true)
        {
            var r = Runner.RunUntil(limitMs);
            if (r.Status == RunStatus.ReachedLimit) return r;
            if ((World.LatestWindowRecipients & (1UL << polity)) != 0) return r;
        }
    }
}
