using System.Buffers.Binary;
using System.Security.Cryptography;
using Proto.M7.Rng;

namespace Proto.M7.War;

/// <summary>
/// The T3 prototype's agent action kinds. Each AgentProfile enables a SUBSET (D-043 B2 "The action set is agent-specific",
/// "Do not create a giant universal action list"). Order kind = ActionKinds.OrderBase + (int)ActionKind.
/// </summary>
public enum ActionKind : byte
{
    /// <summary>Move to cell B (1-D line of cells; uniform cost, so the least-AC route is the straight one, ledger §18).</summary>
    Move = 0,
    /// <summary>Attack enemy agent B, which must be within the attacker's AttackRange.</summary>
    Attack = 1,
    /// <summary>Withdraw to cell B: needs contact with an enemy, moves at most RetreatMaxCells, away from EVERY contact.</summary>
    Retreat = 2,
    /// <summary>Raise levies for this agent: pays B money from the issuer's treasury to the recruit sink (Ledger transfer).</summary>
    Recruit = 3,
}

public static class ActionKinds
{
    public const int Count = 4;
    /// <summary>Order kinds 150..153 carry agent actions: A = WarKinds.Pack(issuer, agentId), B = the argument.</summary>
    public const int OrderBase = 150;
    public static bool IsAction(int orderKind) => orderKind >= OrderBase && orderKind < OrderBase + Count;
    public static int Bit(ActionKind k) => 1 << (int)k;
}

/// <summary>
/// Agent-specific action set and Action Capacity (ledger §17.2 "Every MobileAgent has Action Capacity."; D-043 B2). All
/// costs are integer Action Capacity units. Capacity is the full budget granted at each refresh (a refresh RESETS to it).
/// </summary>
public sealed record AgentProfile(int Id, int ActionMask, long Capacity, long MoveCostPerCell, long AttackCost, long RetreatCost,
                                  long RecruitCost, long AttackRange, long RetreatMaxCells);

/// <summary>A MobileAgent of a polity. WarId &gt;= 0: a military agent assigned to that war (the polity must take part);
/// -1: a campaign-layer agent. X is the starting cell.</summary>
public sealed record WarAgentSpec(int Id, int Polity, int ProfileId, int WarId, long X);

/// <summary>The action layer of a war world: profiles, agents (ids 0..n-1) and the seed of identity-keyed engagement rolls.</summary>
public sealed record ActionLayerSpec(AgentProfile[] Profiles, WarAgentSpec[] Agents, ulong Seed);

/// <summary>
/// The action-economy rules (Phase C lane T3). The count is a DIRECTOR DECISION; everything else is a PROPOSAL under DIR5
/// §2.8's delegation ("You own the detailed legal-action rules and implementation"), for Director review.
///
/// AN ACTION IS LEGAL iff (checked in this order; the first failure is the recorded refusal reason):
///   1. the agent exists and belongs to the issuer (NotOwnAgent);
///   2. its kind is in the agent's own action set (NotInActionSet);
///   3. the issuer has used fewer than MaxActionsPerWindow slots in the agent's enclosing window (SlotsExhausted);
///   4. same-agent combination: the agent has not done this kind in the window (KindRepeated) and every earlier action of
///      the SAME agent in the window may be followed by it (SameAgentAllowed; CombinationForbidden). Actions of
///      DIFFERENT agents always combine;
///   5. cross-window hit-and-run: an agent that retreated from enemy E may not attack E in the same or the next
///      HitAndRunCooldownWindows window(s) of its own, unless E has since attacked it or closed on it (HitAndRun);
///   6. target/argument legality (OutOfReach, BadValue);
///   7. its Action Capacity cost does not exceed the agent's remaining capacity (CapacityExhausted).
/// ENCLOSING WINDOW (C23 item 1–2; reading RDG-10/R8): an agent of an active war is in the OPERATIONAL window = the
/// current War Pulse of that war; any other agent is in the CAMPAIGN window = the current strategic turn. Event-triggered
/// (interrupt) windows open access to the enclosing window but refresh neither slots nor Action Capacity.
/// A refused action changes no state: no slot, no capacity, no ledger transfer, no draw.
/// </summary>
public static class ActionRules
{
    /// <summary>DIR5 §2.8 "Maximum two actions per decision window." (D-079 DR22). Director decision: a constant, not data.</summary>
    public const int MaxActionsPerWindow = 2;

    /// <summary>PROPOSAL (C23 item 4, window count a proposal): windows after the retreat window during which the
    /// retreating agent may not attack the enemy it left, unless that enemy attacked it or closed on it.</summary>
    public const int HitAndRunCooldownWindows = 1;

    /// <summary>
    /// PROPOSAL — same-agent combination matrix, row = an earlier action of the agent in this window, column = the next.
    /// The diagonal is refused as KindRepeated before this table is read (no splitting one action into two).
    ///                 Move   Attack Retreat Recruit
    ///   Move           -      yes    no      yes      advance-and-strike; no advance-then-withdraw loop
    ///   Attack         no     -      no      no       no strike-then-reposition, no strike-then-retreat (hit-and-run)
    ///   Retreat        no     no     -       no       a retreat ends the agent's window
    ///   Recruit        yes    no     yes     -        no reinforcement in a window with an attack (prototype rule 4)
    /// </summary>
    private static readonly bool[] SameAgent =
    {
        false, true,  false, true,
        false, false, false, false,
        false, false, false, false,
        true,  false, true,  false,
    };

    public static bool SameAgentAllowed(ActionKind first, ActionKind second) => SameAgent[(int)first * ActionKinds.Count + (int)second];
}

public enum RefreshCause : byte
{
    Begin = 0,
    TurnStart = 1,
    PulseStart = 2,
}

/// <summary>An Action Capacity refresh of one agent: WarId -1 = campaign layer (WindowIndex = turn), else the pulse index.</summary>
public readonly record struct ActionRefresh(long AtMs, int AgentId, RefreshCause Cause, int WarId, long WindowIndex, long Capacity);

/// <summary>An accepted action. WarId/WindowIndex name the enclosing window whose slot it used; Roll is the identity-keyed
/// engagement draw (Attack only, else -1).</summary>
public readonly record struct ActionRecord(int LogIndex, int Polity, int AgentId, ActionKind Kind, long AtMs, int WarId, long WindowIndex,
                                           long Arg, long Cost, long Roll);

/// <summary>Agent retreated from contact with Enemy in its refresh window Serial. Lifted once Enemy attacked it or closed on it.</summary>
public readonly record struct Disengagement(int AgentId, int EnemyId, long Serial, long AtMs, bool Lifted);

/// <summary>
/// The universal budget mechanism (D-043 B2 "The budget mechanism is universal.") for every agent of one war world:
/// per-agent Action Capacity, per-polity window slots, the combination and hit-and-run rules. Driven by WarTheaterWorld:
/// Begin (initial grant), OnTurnStart (campaign agents), OnPulseStart (that war's agents), TryAct (orders).
/// Owns no presentation input: nothing here can see whether a window was shown, auto-skipped or replayed.
/// </summary>
public sealed class ActionWindowBook
{
    private sealed class WindowUse
    {
        public WindowUse(int warId, long index) { WarId = warId; Index = index; }
        public readonly int WarId;
        public readonly long Index;
        public int Used;
        public readonly int[] Agent = new int[ActionRules.MaxActionsPerWindow];
        public readonly ActionKind[] Kind = new ActionKind[ActionRules.MaxActionsPerWindow];
    }

    /// <summary>Counter-RNG stream of engagement rolls (keyed by attacker, defender, instant; never by a cursor).</summary>
    public const uint EngagementStream = 31;

    private readonly AgentProfile[] _profiles;
    private readonly WarAgentSpec[] _agents;
    private readonly ulong _seed;
    private readonly int _polityCount;
    private readonly int[][] _participants;
    private readonly Func<int, long> _operationalWindowOf;
    private readonly WarLedger _ledger;
    private readonly int[] _treasury;
    private readonly int _recruitSink;
    private readonly long[] _x, _remaining, _serial, _levies;
    private readonly WindowUse[] _campaign;
    private readonly WindowUse?[][] _operational;
    private readonly List<Disengagement> _disengaged = new();
    private readonly List<ActionRecord> _actions = new();
    private readonly List<ActionRefresh> _refreshes = new();

    public ActionWindowBook(ActionLayerSpec spec, int polityCount, WarSpec[] warsById, Func<int, long> operationalWindowOf,
                            WarLedger ledger, int[] treasury, int recruitSink)
    {
        _profiles = (AgentProfile[])spec.Profiles.Clone();
        for (int i = 0; i < _profiles.Length; i++)
        {
            var p = _profiles[i];
            if (p.Id != i || p.Capacity < 0 || p.MoveCostPerCell < 0 || p.AttackCost < 0 || p.RetreatCost < 0 || p.RecruitCost < 0
                || p.AttackRange < 0 || p.RetreatMaxCells < 1 || (p.ActionMask & ~((1 << ActionKinds.Count) - 1)) != 0)
                throw new ArgumentException($"bad profile {i}");
        }
        _agents = (WarAgentSpec[])spec.Agents.Clone();
        _seed = spec.Seed;
        _polityCount = polityCount;
        _participants = new int[warsById.Length][];
        _operational = new WindowUse?[warsById.Length][];
        for (int w = 0; w < warsById.Length; w++)
        {
            _participants[w] = (int[])warsById[w].Participants.Clone();
            _operational[w] = new WindowUse?[_participants[w].Length];
        }
        for (int i = 0; i < _agents.Length; i++)
        {
            var a = _agents[i];
            if (a.Id != i || a.Polity < 0 || a.Polity >= polityCount || a.ProfileId < 0 || a.ProfileId >= _profiles.Length || a.X < 0)
                throw new ArgumentException($"bad agent {i}");
            if (a.WarId >= warsById.Length || a.WarId < -1) throw new ArgumentException($"agent {i}: unknown war");
            if (a.WarId >= 0 && Array.BinarySearch(_participants[a.WarId], a.Polity) < 0) throw new ArgumentException($"agent {i}: polity not in its war");
        }
        _operationalWindowOf = operationalWindowOf;
        _ledger = ledger;
        _treasury = treasury;
        _recruitSink = recruitSink;
        int n = _agents.Length;
        _x = new long[n];
        _remaining = new long[n];
        _serial = new long[n];
        _levies = new long[n];
        for (int i = 0; i < n; i++) _x[i] = _agents[i].X;
        _campaign = new WindowUse[polityCount];
        for (int p = 0; p < polityCount; p++) _campaign[p] = new WindowUse(-1, -1);
    }

    public int AgentCount => _agents.Length;
    public IReadOnlyList<ActionRecord> Actions => _actions;
    public IReadOnlyList<ActionRefresh> Refreshes => _refreshes;
    public IReadOnlyList<Disengagement> Disengagements => _disengaged;
    public long PositionOf(int agent) => _x[agent];
    public long RemainingOf(int agent) => _remaining[agent];
    public long LeviesOf(int agent) => _levies[agent];
    public long WindowSerialOf(int agent) => _serial[agent];

    /// <summary>Slots the polity has used in the campaign window (warId -1) or in its current window of war warId.</summary>
    public int SlotsUsed(int polity, int warId)
    {
        if (warId < 0) return _campaign[polity].Used;
        int i = Array.BinarySearch(_participants[warId], polity);
        return i < 0 ? 0 : _operational[warId][i]?.Used ?? 0;
    }

    /// <summary>Initial grant at the world's start instant: every agent full capacity; the campaign window is turn `turn`.</summary>
    public void Begin(long now, long turn)
    {
        for (int p = 0; p < _polityCount; p++) _campaign[p] = new WindowUse(-1, turn);
        for (int i = 0; i < _agents.Length; i++) Refresh(i, now, RefreshCause.Begin, -1, turn);
    }

    /// <summary>Strategic-turn refresh (D-043 B2 "Normal world: Action Capacity refreshes according to the strategic turn."):
    /// new campaign window for every polity; agents NOT in an active war get full capacity. War agents are untouched.</summary>
    public void OnTurnStart(long now, long turn)
    {
        for (int p = 0; p < _polityCount; p++) _campaign[p] = new WindowUse(-1, turn);
        for (int i = 0; i < _agents.Length; i++)
            if (_operationalWindowOf(_agents[i].WarId) < 0) Refresh(i, now, RefreshCause.TurnStart, -1, turn);
    }

    /// <summary>War Pulse refresh (ledger §19 "involved military agents receive refreshed Action Capacity"; ADR-061 §3.11
    /// "at pulse start, not at the turn boundary"): new operational window for each participant; that war's agents refresh.</summary>
    public void OnPulseStart(long now, int warId, long pulseIndex)
    {
        var parts = _participants[warId];
        for (int i = 0; i < parts.Length; i++) _operational[warId][i] = new WindowUse(warId, pulseIndex);
        for (int i = 0; i < _agents.Length; i++)
            if (_agents[i].WarId == warId) Refresh(i, now, RefreshCause.PulseStart, warId, pulseIndex);
    }

    private void Refresh(int agent, long now, RefreshCause cause, int warId, long index)
    {
        long cap = _profiles[_agents[agent].ProfileId].Capacity;
        _remaining[agent] = cap;   // a refresh RESETS: unused capacity never carries over
        if (cause != RefreshCause.Begin) _serial[agent]++;
        _refreshes.Add(new ActionRefresh(now, agent, cause, warId, index, cap));
    }

    private static long Abs(long v) => v < 0 ? checked(-v) : v;

    private bool InContact(int agent, int enemy, long range) => _agents[enemy].Polity != _agents[agent].Polity && Abs(_x[enemy] - _x[agent]) <= range;

    private bool HitAndRunBanned(int agent, int target)
    {
        foreach (var d in _disengaged)
            if (d.AgentId == agent && d.EnemyId == target && !d.Lifted && _serial[agent] <= d.Serial + ActionRules.HitAndRunCooldownWindows)
                return true;
        return false;
    }

    /// <summary>Evaluate and, when legal, apply one action order. Returns 0 (accepted) or a WarRefusal code (no state change).</summary>
    public int TryAct(int log, int issuer, int orderKind, long agentL, long arg, long now)
    {
        var kind = (ActionKind)(orderKind - ActionKinds.OrderBase);
        if (agentL < 0 || agentL >= _agents.Length || _agents[agentL].Polity != issuer) return WarRefusal.NotOwnAgent;
        int agent = (int)agentL;
        var spec = _agents[agent];
        var prof = _profiles[spec.ProfileId];
        if ((prof.ActionMask & ActionKinds.Bit(kind)) == 0) return WarRefusal.NotInActionSet;

        long opIndex = _operationalWindowOf(spec.WarId);
        WindowUse use;
        if (opIndex >= 0)
        {
            use = _operational[spec.WarId][Array.BinarySearch(_participants[spec.WarId], issuer)]
                  ?? throw new InvalidOperationException("operational window not opened");
            if (use.Index != opIndex) throw new InvalidOperationException("operational window is stale");
        }
        else use = _campaign[issuer];

        if (use.Used >= ActionRules.MaxActionsPerWindow) return WarRefusal.SlotsExhausted;
        for (int i = 0; i < use.Used; i++)
        {
            if (use.Agent[i] != agent) continue;
            if (use.Kind[i] == kind) return WarRefusal.KindRepeated;
            if (!ActionRules.SameAgentAllowed(use.Kind[i], kind)) return WarRefusal.CombinationForbidden;
        }

        long x = _x[agent], cost;
        switch (kind)
        {
            case ActionKind.Move:
                if (arg == x) return WarRefusal.OutOfReach;
                cost = checked(Abs(arg - x) * prof.MoveCostPerCell);
                break;
            case ActionKind.Attack:
                if (arg >= _agents.Length) return WarRefusal.OutOfReach;
                if (HitAndRunBanned(agent, (int)arg)) return WarRefusal.HitAndRun;
                if (!InContact(agent, (int)arg, prof.AttackRange)) return WarRefusal.OutOfReach;
                cost = prof.AttackCost;
                break;
            case ActionKind.Retreat:
            {
                long d = Abs(arg - x);
                bool contact = false;
                for (int e = 0; e < _agents.Length; e++)
                {
                    if (!InContact(agent, e, prof.AttackRange)) continue;
                    contact = true;
                    if (Abs(arg - _x[e]) <= Abs(x - _x[e])) return WarRefusal.OutOfReach;   // must move away from every contact
                }
                if (!contact || d < 1 || d > prof.RetreatMaxCells) return WarRefusal.OutOfReach;
                cost = prof.RetreatCost;
                break;
            }
            case ActionKind.Recruit:
                if (arg <= 0 || arg > _ledger.Balance(_treasury[issuer])) return WarRefusal.BadValue;
                cost = prof.RecruitCost;
                break;
            default: throw new InvalidOperationException("unreachable");
        }
        if (cost > _remaining[agent]) return WarRefusal.CapacityExhausted;

        // ---- accepted: every state change happens below this line
        long roll = -1;
        switch (kind)
        {
            case ActionKind.Move:
                for (int i = 0; i < _disengaged.Count; i++)
                {
                    var d = _disengaged[i];
                    if (d.EnemyId == agent && !d.Lifted && Abs(arg - _x[d.AgentId]) < Abs(x - _x[d.AgentId]))
                        _disengaged[i] = d with { Lifted = true };   // the enemy closed on the retreated agent
                }
                _x[agent] = arg;
                break;
            case ActionKind.Attack:
                for (int i = 0; i < _disengaged.Count; i++)
                {
                    var d = _disengaged[i];
                    if (d.AgentId == arg && d.EnemyId == agent && !d.Lifted) _disengaged[i] = d with { Lifted = true };
                }
                roll = (long)CounterRng.NextBelow(_seed, EngagementStream, agent, arg, now, 0, 1000);
                break;
            case ActionKind.Retreat:
                for (int e = 0; e < _agents.Length; e++)
                    if (InContact(agent, e, prof.AttackRange)) _disengaged.Add(new Disengagement(agent, e, _serial[agent], now, false));
                _x[agent] = arg;
                break;
            case ActionKind.Recruit:
                _ledger.Transfer(_treasury[issuer], _recruitSink, arg);
                _levies[agent] = checked(_levies[agent] + arg);
                break;
        }
        _remaining[agent] -= cost;
        use.Agent[use.Used] = agent;
        use.Kind[use.Used] = kind;
        use.Used++;
        _actions.Add(new ActionRecord(log, issuer, agent, kind, now, use.WarId, use.Index, arg, cost, roll));
        return 0;
    }

    /// <summary>Canonical little-endian encoding of the whole action layer (agents, windows, disengagements, actions, refreshes).</summary>
    public void AppendTo(IncrementalHash h)
    {
        var b = new byte[8];
        void L(long v) { BinaryPrimitives.WriteInt64LittleEndian(b, v); h.AppendData(b); }
        void U(WindowUse? u)
        {
            if (u is null) { L(-2); return; }
            L(u.WarId); L(u.Index); L(u.Used);
            for (int i = 0; i < u.Used; i++) { L(u.Agent[i]); L((long)u.Kind[i]); }
        }
        L(_agents.Length);
        for (int i = 0; i < _agents.Length; i++) { L(_x[i]); L(_remaining[i]); L(_serial[i]); L(_levies[i]); }
        for (int p = 0; p < _polityCount; p++) U(_campaign[p]);
        foreach (var w in _operational) foreach (var u in w) U(u);
        L(_disengaged.Count);
        foreach (var d in _disengaged) { L(d.AgentId); L(d.EnemyId); L(d.Serial); L(d.AtMs); L(d.Lifted ? 1 : 0); }
        L(_actions.Count);
        foreach (var a in _actions) { L(a.LogIndex); L(a.Polity); L(a.AgentId); L((long)a.Kind); L(a.AtMs); L(a.WarId); L(a.WindowIndex); L(a.Arg); L(a.Cost); L(a.Roll); }
        L(_refreshes.Count);
        foreach (var r in _refreshes) { L(r.AtMs); L(r.AgentId); L((long)r.Cause); L(r.WarId); L(r.WindowIndex); L(r.Capacity); }
    }

    public string HashHex()
    {
        using var h = IncrementalHash.CreateHash(HashAlgorithmName.SHA256);
        AppendTo(h);
        return Convert.ToHexString(h.GetHashAndReset());
    }
}
