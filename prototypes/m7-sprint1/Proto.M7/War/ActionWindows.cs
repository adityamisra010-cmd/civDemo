using System.Buffers.Binary;
using System.Security.Cryptography;
using Proto.M7.Fixed;
using Proto.M7.Rng;
using Proto.M7.Spatial;

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
    /// <summary>Withdraw to cell B: needs contact with an enemy, moves at most RetreatMaxCells, away from EVERY contact. [AP round 2,
    /// AP-R2-01] A contact is an agent of another polity within the attack range that the issuer's polity DETECTS: an unseen agent
    /// neither enables a retreat nor constrains its direction (with T3's default unlimited sight every agent is detected).</summary>
    Retreat = 2,
    /// <summary>Raise levies for this agent: pays B money from the issuer's treasury to the recruit sink (Ledger transfer).</summary>
    Recruit = 3,
    // ---- [AP] lane AP (D-082 DA8 "Units can spend action points to hide/entrench/camouflage etc."; DA8-R: the action points are
    // this book's Action Capacity). docs/m7-plan/concealment-actions-proposal.md §3, §4. Appended: codes 0-3 keep their meaning.
    /// <summary>[AP] Hide in the cover of the agent's cell: its detection distance is multiplied by the cell's terrain concealment
    /// (ConcealmentTable). Needs a concealing cell, no enemy in contact, not observed. Ends on move, retreat, attack, or being
    /// spotted. B unused (0). [AP round 2] "In contact" and "observed" are judged on the enemy agents the issuer's polity detects;
    /// an enemy polity that detects the concealed agent at the order's instant spots it at once (the cost is spent).</summary>
    Hide = 4,
    /// <summary>[AP] Camouflage: the detection distance is multiplied by ConcealmentRules.CamouflageFactor, in any terrain. Needs
    /// no enemy in contact and not observed. Ends like a hide. B unused (0). [AP round 2] Judged as a hide is.</summary>
    Camouflage = 5,
    /// <summary>[AP] Entrench: dig field works at the agent's cell, accruing at ConcealmentRules.WorksRatePpmPerYear up to complete
    /// works; the works are lost when the agent moves or retreats. Needs diggable ground. B unused (0).</summary>
    Entrench = 6,
}

public static class ActionKinds
{
    /// <summary>[AP] 4 -> 7 (Hide, Camouflage, Entrench appended).</summary>
    public const int Count = 7;
    /// <summary>Order kinds 150..153 carry agent actions: A = WarKinds.Pack(issuer, agentId), B = the argument. [AP] 154..156: Hide,
    /// Camouflage, Entrench.</summary>
    public const int OrderBase = 150;
    public static bool IsAction(int orderKind) => orderKind >= OrderBase && orderKind < OrderBase + Count;
    public static int Bit(ActionKind k) => 1 << (int)k;
}

/// <summary>
/// Agent-specific action set and Action Capacity (ledger §17.2 "Every MobileAgent has Action Capacity."; D-043 B2). All
/// costs are integer Action Capacity units. Capacity is the full budget granted at each refresh (a refresh RESETS to it).
/// </summary>
public sealed record AgentProfile(int Id, int ActionMask, long Capacity, long MoveCostPerCell, long AttackCost, long RetreatCost,
                                  long RecruitCost, long AttackRange, long RetreatMaxCells)
{
    /// <summary>[AP] Action Capacity cost of Hide / Camouflage / Entrench (PROPOSAL; profile data, 0 by default).</summary>
    public long HideCost { get; init; }
    public long CamouflageCost { get; init; }
    public long EntrenchCost { get; init; }
    /// <summary>[AP] Cells within which this agent detects an UNCONCEALED enemy agent on the line (a concealed one only within
    /// SightCells × its concealment factor). long.MaxValue (default): detection is not modelled for this observer — it sees every
    /// agent, as T3 did, so a world without sight values behaves exactly as before.</summary>
    public long SightCells { get; init; } = long.MaxValue;
}

/// <summary>A MobileAgent of a polity. WarId &gt;= 0: a military agent assigned to that war (the polity must take part);
/// -1: a campaign-layer agent. X is the starting cell.</summary>
public sealed record WarAgentSpec(int Id, int Polity, int ProfileId, int WarId, long X);

/// <summary>The action layer of a war world: profiles, agents (ids 0..n-1) and the seed of identity-keyed engagement rolls.</summary>
public sealed record ActionLayerSpec(AgentProfile[] Profiles, WarAgentSpec[] Agents, ulong Seed)
{
    /// <summary>[AP] The terrain class of each cell of the line (index = cell X; cells beyond the array are Open). Null: all Open.
    /// The stand-in for the campaign map's cover at an agent's location (lane E's war-economy routes use the same classes).</summary>
    public TerrainClass[]? LineTerrain { get; init; }
    /// <summary>[AP] The terrain concealment multipliers a hide uses (lane TR's / Slice C's table). Null: the placeholder table.</summary>
    public ConcealmentTable? Concealment { get; init; }
    /// <summary>[AP] Camouflage factor, works rate and works thresholds. Null: <see cref="War.ConcealmentRules.Proposal"/>.</summary>
    public ConcealmentRules? ConcealmentRules { get; init; }
}

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
///   6. target/argument legality (OutOfReach, BadValue); [AP] for an Attack, the target must be DETECTED by the issuer's polity
///      (TargetUndetected, checked before the reach); for Hide / Camouflage / Entrench, the state is not already held
///      (AlreadyInState), the cell conceals (Hide: NoCover) or can be dug (Entrench: CannotDig), no enemy agent is in contact
///      (Hide, Camouflage: EnemyInContact), and no enemy polity detects the agent now (Hide, Camouflage: Observed);
///      [AP round 2, AP-R2-01: an order's legality depends only on what the issuer's polity knows — its own agents and the agents it
///      detects. TargetUndetected comes before the hit-and-run check (rule 5 is then read only for a detected target);
///      EnemyInContact and Observed count only enemy agents the issuer's polity detects; Retreat's contacts likewise. AP-R2-04:
///      the concealment mechanics' "enemy" is a polity that shares a war with the agent's polity (AreEnemies).]
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
    ///                 Move   Attack Retreat Recruit [AP] Hide Camo Entrench
    ///   Move           -      yes    no      yes          yes  yes  yes      advance-and-strike; advance then conceal / dig in
    ///   Attack         no     -      no      no           no   no   no       no strike-then-anything (hit-and-run, strike-and-vanish)
    ///   Retreat        no     no     -       no           no   no   no       a retreat ends the agent's window
    ///   Recruit        yes    no     yes     -            yes  yes  yes      no reinforcement in a window with an attack (rule 4)
    ///   [AP] Hide      no     no     no      yes          -    yes  yes      concealment holds still; no hide-and-strike in one window
    ///   [AP] Camo      no     no     no      yes          yes  -    yes      as Hide
    ///   [AP] Entrench  no     yes    no      yes          yes  yes  -        dig in then strike from the works; no dig-then-leave
    /// The 4 x 4 block of T3's kinds is unchanged. An ambush (an attack from concealment) therefore needs the concealment ordered in
    /// an EARLIER window than the attack (docs/m7-plan/concealment-actions-proposal.md §3.4).
    /// </summary>
    private static readonly bool[] SameAgent =
    {
        false, true,  false, true,  true,  true,  true,
        false, false, false, false, false, false, false,
        false, false, false, false, false, false, false,
        true,  false, true,  false, true,  true,  true,
        false, false, false, true,  false, true,  true,
        false, false, false, true,  true,  false, true,
        false, true,  false, true,  true,  true,  false,
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
    // ---- [AP] concealment and preparation (lane AP, D-082 DA8): per-agent states as the instant each began (-1 = not held), the
    // change log and the engagement log. Hashed only when some profile enables Hide, Camouflage or Entrench (_concealOn), so a T3
    // world keeps its hashes byte for byte.
    private readonly bool _concealOn;
    private readonly TerrainClass[] _terrain;
    private readonly ConcealmentTable _conceal;
    private readonly ConcealmentRules _cr;
    private readonly long[] _hiddenSince, _camoSince, _entrenchSince;
    private readonly List<ConcealmentChange> _changes = new();
    private readonly List<EngagementRecord> _engagements = new();
    // [AP round 2, AP-R2-04] The ENEMY relation of the concealment mechanics (who can spot, watch or be in contact with a concealed
    // agent): polities p != q are enemies iff some war of the world lists both as participants. Row-major polityCount x polityCount,
    // fixed at construction (a war's span is not consulted). T3's Attack and Retreat keep T3's own convention (any other polity).
    private readonly bool[] _enemies;

    public ActionWindowBook(ActionLayerSpec spec, int polityCount, WarSpec[] warsById, Func<int, long> operationalWindowOf,
                            WarLedger ledger, int[] treasury, int recruitSink)
    {
        _profiles = (AgentProfile[])spec.Profiles.Clone();
        for (int i = 0; i < _profiles.Length; i++)
        {
            var p = _profiles[i];
            if (p.Id != i || p.Capacity < 0 || p.MoveCostPerCell < 0 || p.AttackCost < 0 || p.RetreatCost < 0 || p.RecruitCost < 0
                || p.AttackRange < 0 || p.RetreatMaxCells < 1 || (p.ActionMask & ~((1 << ActionKinds.Count) - 1)) != 0
                || p.HideCost < 0 || p.CamouflageCost < 0 || p.EntrenchCost < 0 || p.SightCells < 0)          // [AP]
                throw new ArgumentException($"bad profile {i}");
        }
        const int concealBits = (1 << (int)ActionKind.Hide) | (1 << (int)ActionKind.Camouflage) | (1 << (int)ActionKind.Entrench);
        foreach (var p in _profiles) if ((p.ActionMask & concealBits) != 0) _concealOn = true;              // [AP]
        _terrain = spec.LineTerrain is null ? Array.Empty<TerrainClass>() : (TerrainClass[])spec.LineTerrain.Clone();
        foreach (var c in _terrain) if ((int)c >= TerrainCostTable.ClassCount) throw new ArgumentException("line terrain class");
        _conceal = spec.Concealment ?? ConcealmentTable.PlaceholderTestTable();
        _cr = spec.ConcealmentRules ?? ConcealmentRules.Proposal();
        _cr.Validate();
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
        _enemies = new bool[polityCount * polityCount];                                                    // [AP round 2]
        foreach (var parts in _participants)
            foreach (int p in parts)
                foreach (int q in parts)
                    if (p != q) _enemies[p * polityCount + q] = true;
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
        _hiddenSince = new long[n]; _camoSince = new long[n]; _entrenchSince = new long[n];                  // [AP]
        Array.Fill(_hiddenSince, -1L); Array.Fill(_camoSince, -1L); Array.Fill(_entrenchSince, -1L);
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

    // ---- [AP] concealment and preparation: read-only queries (pure; a query never changes state or a hash)
    public bool IsHidden(int agent) => _hiddenSince[agent] >= 0;
    public bool IsCamouflaged(int agent) => _camoSince[agent] >= 0;
    public bool IsEntrenched(int agent) => _entrenchSince[agent] >= 0;
    public long EntrenchSince(int agent) => _entrenchSince[agent];
    public ConcealKind StatesOf(int agent)
        => (IsHidden(agent) ? ConcealKind.Hidden : 0) | (IsCamouflaged(agent) ? ConcealKind.Camouflaged : 0) | (IsEntrenched(agent) ? ConcealKind.Entrenched : 0);
    /// <summary>The agent's field works at <paramref name="nowMs"/> (ppm of complete works; 0 when it is not entrenched).</summary>
    public long WorksOf(int agent, long nowMs) => FieldWorks.WorksAt(_entrenchSince[agent], nowMs, _cr.WorksRatePpmPerYear);
    public IReadOnlyList<ConcealmentChange> ConcealmentChanges => _changes;
    public IReadOnlyList<EngagementRecord> Engagements => _engagements;
    public ConcealmentRules ConcealmentRules => _cr;
    public TerrainClass TerrainAt(long x) => x >= 0 && x < _terrain.Length ? _terrain[x] : TerrainClass.Open;

    /// <summary>The factor on the distance at which an observer detects the agent: the terrain concealment of its cell while it is
    /// hidden, times the camouflage factor while it is camouflaged (Q32, floor); One with neither.</summary>
    public Q32 ConcealmentFactor(int agent)
    {
        Q32 c = Q32.One;
        if (IsHidden(agent)) c = c.Mul(_conceal.Multiplier(TerrainAt(_x[agent])));
        if (IsCamouflaged(agent)) c = c.Mul(_cr.CamouflageFactor);
        return c;
    }

    /// <summary>
    /// [AP] The war-pulse DETECTION rule (docs/m7-plan/concealment-actions-proposal.md §2): polity <paramref name="polity"/> detects
    /// agent <paramref name="agent"/> now iff the agent is its own, or some agent o of the polity has |x_o − x_agent| ≤ SightCells(o) ×
    /// ConcealmentFactor(agent), compared exactly (|dx| × 2^32 ≤ sight × factor.Raw, Int128). An observer of unlimited sight
    /// (long.MaxValue, the default) detects every agent. A pure function of the positions and states, so it is identical in a live
    /// run, a run without stops and a replay. The knowledge-seam counterpart (PolityKnowledge.KnowsUnit, ADR-037) is §2.3.
    /// </summary>
    public bool Detects(int polity, int agent)
    {
        if (_agents[agent].Polity == polity) return true;
        Q32 f = ConcealmentFactor(agent);
        for (int o = 0; o < _agents.Length; o++)
            if (_agents[o].Polity == polity && Sees(o, agent, f)) return true;
        return false;
    }

    /// <summary>[AP] Whether observer agent <paramref name="o"/> detects agent <paramref name="agent"/> whose concealment factor is
    /// <paramref name="f"/>: |x_o − x_agent| × 2^32 ≤ SightCells(o) × f.Raw (Int128); an observer of unlimited sight sees every agent.
    /// [AP round 2: factored out of <see cref="Detects"/> unchanged, for the watcher test of Hide and Camouflage.]</summary>
    private bool Sees(int o, int agent, Q32 f)
    {
        long sight = _profiles[_agents[o].ProfileId].SightCells;
        if (sight == long.MaxValue) return true;
        Int128 lhs = (Int128)Abs(_x[o] - _x[agent]) * Q32.OneRaw, rhs = (Int128)sight * f.Raw;
        return lhs <= rhs;
    }

    /// <summary>[AP round 2, AP-R2-04] Whether polities <paramref name="p"/> and <paramref name="q"/> are ENEMIES for the concealment
    /// mechanics: p ≠ q and some war of the world lists both as participants (PROPOSAL; docs/m7-plan/concealment-actions-proposal.md
    /// §3.7). Only an enemy polity spots a concealed agent, watches it (Observed) or is in contact with it (EnemyInContact).</summary>
    public bool AreEnemies(int p, int q) => _enemies[p * _polityCount + q];

    /// <summary>Whether an ENEMY polity of the agent's polity (<see cref="AreEnemies"/>; [AP round 2], before: any other polity)
    /// detects it now. The world's truth, which the agent's own polity may not know: used to spot a concealment at the instant it
    /// begins, never to refuse an order.</summary>
    private bool DetectedByAnEnemy(int agent)
    {
        int own = _agents[agent].Polity;
        for (int p = 0; p < _polityCount; p++) if (AreEnemies(own, p) && Detects(p, agent)) return true;
        return false;
    }

    /// <summary>[AP round 2, AP-R2-01] An agent of an enemy polity that the agent's own polity DETECTS stands within
    /// <paramref name="range"/> cells: what the issuer can know of a contact. An enemy it does not detect never refuses an order, so
    /// the order's outcome reveals nothing about it (proposal §3.6).</summary>
    private bool KnownEnemyInContact(int agent, long range)
    {
        int own = _agents[agent].Polity;
        for (int e = 0; e < _agents.Length; e++)
            if (AreEnemies(own, _agents[e].Polity) && Abs(_x[e] - _x[agent]) <= range && Detects(own, e)) return true;
        return false;
    }

    /// <summary>[AP round 2, AP-R2-01] An agent of an enemy polity that the agent's own polity DETECTS detects the agent now (with its
    /// current concealment factor): what the issuer can know of being watched. An unseen watcher is judged after the order instead —
    /// the new concealment is spotted at once if an enemy polity detects it (proposal §3.6).</summary>
    private bool WatchedByAKnownEnemy(int agent)
    {
        int own = _agents[agent].Polity;
        Q32 f = ConcealmentFactor(agent);
        for (int o = 0; o < _agents.Length; o++)
            if (AreEnemies(own, _agents[o].Polity) && Detects(own, o) && Sees(o, agent, f)) return true;
        return false;
    }

    private void Change(int log, long now, int agent, ConcealKind kind, bool begin, ConcealEnd cause)
        => _changes.Add(new ConcealmentChange(_changes.Count, log, now, agent, kind, begin, cause));

    /// <summary>End the agent's hide and camouflage (and, when <paramref name="works"/>, its entrenchment: the works are lost).</summary>
    private void EndStates(int log, long now, int agent, ConcealEnd cause, bool works)
    {
        if (_hiddenSince[agent] >= 0) { _hiddenSince[agent] = -1; Change(log, now, agent, ConcealKind.Hidden, false, cause); }
        if (_camoSince[agent] >= 0) { _camoSince[agent] = -1; Change(log, now, agent, ConcealKind.Camouflaged, false, cause); }
        if (works && _entrenchSince[agent] >= 0) { _entrenchSince[agent] = -1; Change(log, now, agent, ConcealKind.Entrenched, false, cause); }
    }

    /// <summary>After an agent of <paramref name="moverPolity"/> moved: every concealed agent of another polity that this polity now
    /// detects is SPOTTED (its hide and camouflage end), in ascending agent id. [AP round 2, AP-R2-04] Only a polity at war with the
    /// concealed agent's polity spots it (<see cref="AreEnemies"/>; a polity is never its own enemy).</summary>
    private void Spot(int log, long now, int moverPolity)
    {
        for (int j = 0; j < _agents.Length; j++)
        {
            if (!AreEnemies(moverPolity, _agents[j].Polity) || (!IsHidden(j) && !IsCamouflaged(j))) continue;
            if (Detects(moverPolity, j)) EndStates(log, now, j, ConcealEnd.Spotted, works: false);
        }
    }

    /// <summary>The persisted state of every agent (codec rows), ascending id.</summary>
    public ConcealmentStateRow[] StateRows()
    {
        var r = new ConcealmentStateRow[_agents.Length];
        for (int i = 0; i < r.Length; i++) r[i] = new ConcealmentStateRow(i, StatesOf(i), _hiddenSince[i], _camoSince[i], _entrenchSince[i]);
        return r;
    }

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
                // [AP] (round 2, AP-R2-01: before the hit-and-run check, whose record is lifted by the target's own approach, so an
                // order on an unseen agent says nothing of its distance or its movements)
                if (_agents[arg].Polity != issuer && !Detects(issuer, (int)arg)) return WarRefusal.TargetUndetected;
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
                    if (!InContact(agent, e, prof.AttackRange) || !Detects(issuer, e)) continue;   // [AP round 2] a contact it can see
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
            // ---- [AP] concealment and preparation (docs/m7-plan/concealment-actions-proposal.md §3)
            case ActionKind.Hide:
                if (arg != 0) return WarRefusal.BadValue;
                if (IsHidden(agent)) return WarRefusal.AlreadyInState;
                if (_conceal.Multiplier(TerrainAt(x)) == Q32.One) return WarRefusal.NoCover;
                if (KnownEnemyInContact(agent, prof.AttackRange)) return WarRefusal.EnemyInContact;   // [AP round 2] seen enemies only
                if (WatchedByAKnownEnemy(agent)) return WarRefusal.Observed;                         // [AP round 2] seen watchers only
                cost = prof.HideCost;
                break;
            case ActionKind.Camouflage:
                if (arg != 0) return WarRefusal.BadValue;
                if (IsCamouflaged(agent)) return WarRefusal.AlreadyInState;
                if (KnownEnemyInContact(agent, prof.AttackRange)) return WarRefusal.EnemyInContact;   // [AP round 2]
                if (WatchedByAKnownEnemy(agent)) return WarRefusal.Observed;                         // [AP round 2]
                cost = prof.CamouflageCost;
                break;
            case ActionKind.Entrench:
                if (arg != 0) return WarRefusal.BadValue;
                if (IsEntrenched(agent)) return WarRefusal.AlreadyInState;
                if (!FieldWorks.Diggable(TerrainAt(x))) return WarRefusal.CannotDig;
                cost = prof.EntrenchCost;
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
                EndStates(log, now, agent, ConcealEnd.Moved, works: true);   // [AP] it left its cover, camouflage and works
                Spot(log, now, issuer);                                        // [AP] its new position may spot hidden enemies
                break;
            case ActionKind.Attack:
            {
                for (int i = 0; i < _disengaged.Count; i++)
                {
                    var d = _disengaged[i];
                    if (d.AgentId == arg && d.EnemyId == agent && !d.Lifted) _disengaged[i] = d with { Lifted = true };
                }
                roll = (long)CounterRng.NextBelow(_seed, EngagementStream, agent, arg, now, 0, 1000);
                // [AP] An AMBUSH: the attacker held a hide or camouflage and the target's polity did not detect it at this instant.
                int target = (int)arg;
                var states = StatesOf(agent);
                bool ambush = (states & (ConcealKind.Hidden | ConcealKind.Camouflaged)) != 0 && !Detects(_agents[target].Polity, agent);
                _engagements.Add(new EngagementRecord(log, now, agent, target, issuer, _agents[target].Polity, ambush, states, WorksOf(target, now), _x[target]));
                EndStates(log, now, agent, ConcealEnd.Attacked, works: false);   // an attack reveals the attacker; its works stay
                break;
            }
            case ActionKind.Retreat:
                for (int e = 0; e < _agents.Length; e++)                     // [AP round 2] from the contacts it can see only
                    if (InContact(agent, e, prof.AttackRange) && Detects(issuer, e)) _disengaged.Add(new Disengagement(agent, e, _serial[agent], now, false));
                _x[agent] = arg;
                EndStates(log, now, agent, ConcealEnd.Moved, works: true);   // [AP]
                Spot(log, now, issuer);                                        // [AP]
                break;
            case ActionKind.Recruit:
                _ledger.Transfer(_treasury[issuer], _recruitSink, arg);
                _levies[agent] = checked(_levies[agent] + arg);
                break;
            case ActionKind.Hide:                                              // [AP] from this instant (log order at T)
                _hiddenSince[agent] = now; Change(log, now, agent, ConcealKind.Hidden, true, ConcealEnd.None);
                // [AP round 2, AP-R2-01] an enemy polity that detects the concealed agent at this instant (an unseen watcher: a seen one
                // refused the order above) spots it at once, with this order's log index; the cost is spent (proposal §3.6)
                if (DetectedByAnEnemy(agent)) EndStates(log, now, agent, ConcealEnd.Spotted, works: false);
                break;
            case ActionKind.Camouflage:
                _camoSince[agent] = now; Change(log, now, agent, ConcealKind.Camouflaged, true, ConcealEnd.None);
                if (DetectedByAnEnemy(agent)) EndStates(log, now, agent, ConcealEnd.Spotted, works: false);   // [AP round 2]
                break;
            case ActionKind.Entrench:
                _entrenchSince[agent] = now; Change(log, now, agent, ConcealKind.Entrenched, true, ConcealEnd.None);
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
        if (!_concealOn) return;                 // [AP] appended only when some profile enables the lane-AP actions (T3 hashes unchanged)
        for (int i = 0; i < _agents.Length; i++) { L(_hiddenSince[i]); L(_camoSince[i]); L(_entrenchSince[i]); }
        L(_changes.Count);
        foreach (var c in _changes) { L(c.Seq); L(c.LogIndex); L(c.AtMs); L(c.AgentId); L((long)c.Kind); L(c.Begin ? 1 : 0); L((long)c.Cause); }
        L(_engagements.Count);
        foreach (var e in _engagements)
        {
            L(e.LogIndex); L(e.AtMs); L(e.Attacker); L(e.Target); L(e.AttackerPolity); L(e.TargetPolity); L(e.Ambush ? 1 : 0);
            L((long)e.AttackerStates); L(e.TargetWorksPpm); L(e.TargetCell);
        }
    }

    public string HashHex()
    {
        using var h = IncrementalHash.CreateHash(HashAlgorithmName.SHA256);
        AppendTo(h);
        return Convert.ToHexString(h.GetHashAndReset());
    }
}
