namespace Proto.M7.Tactical;

/// <summary>
/// Domain tag carried by formations, entities and results (spec §4 "Naval": O-1 decided full tactical control for naval
/// battles, auto-resolve optional and never the default). The order / entity / event interfaces of this slice carry NO
/// domain-specific field: the tag is the only place the domain appears. Only <see cref="Land"/> has a tactical module in
/// Sprint 1; the naval and air modules are NOT built (brief §4).
/// </summary>
public enum Domain : byte
{
    Land = 0,
    Naval = 1,
    Air = 2,
}

/// <summary>Which resolver settles a battle. The choice is ALWAYS an explicit parameter whose default is Tactical (O-1).</summary>
public enum ResolverKind : byte
{
    Tactical = 0,
    /// <summary>Formation-level AutoResolver (Slice E). Never chosen implicitly.</summary>
    Auto = 1,
}

/// <summary>
/// Battle-transient entity state (O-10 a). Transitions are monotone: Active -&gt; Routing -&gt; {Escaped, Surrendered, Dead};
/// Active -&gt; {Escaped, Surrendered, Dead}. There is no Routing -&gt; Active rally in this prototype (PLACEHOLDER: not modelled).
/// </summary>
public enum EntityState : byte
{
    Active = 0,
    Routing = 1,
    Surrendered = 2,
    Dead = 3,
    Escaped = 4,
}

/// <summary>
/// Formation-level orders (spec §4). Every order is ONE action for the <see cref="ActionBudget"/>.
/// Rally is the spec's "Rally/Heal (placeholder)" kind: it is the Heal action of the budget rules.
/// Values are stable codes (hashed in the trace) — never renumber.
/// </summary>
public enum OrderKind : byte
{
    Hold = 0,
    MoveTo = 1,
    Attack = 2,
    Retreat = 3,
    /// <summary>Rally/Heal (placeholder): stand, recover morale faster, regenerate HP from a bounded pool.</summary>
    Rally = 4,
    /// <summary>Resume an attack after a Retreat issued in an EARLIER interval.</summary>
    Reengage = 5,
}

/// <summary>Map edge a side withdraws / routs toward.</summary>
public enum ExitEdge : byte
{
    West = 0,
    East = 1,
    North = 2,
    South = 3,
}

/// <summary>AI posture of a formation (scenario input).</summary>
public enum Posture : byte
{
    Normal = 0,
    /// <summary>Hold (concealed) until a known enemy comes within the AI's ambush trigger distance, then Attack.</summary>
    Ambush = 1,
}

public enum BattleEndReason : byte
{
    None = 0,
    /// <summary>One side has no Active entity left (all Dead / Escaped / Surrendered / Routing); the other still has some.</summary>
    SideEliminated = 1,
    /// <summary>Neither side has an Active entity left (e.g. a simultaneous mutual kill).</summary>
    BothEliminated = 2,
    /// <summary>One side is entirely disengaged by the five-pulse inactivity rule.</summary>
    SideDisengaged = 3,
    /// <summary>Both sides disengaged at the same pulse end.</summary>
    BothDisengaged = 4,
}

/// <summary>Event kinds of the tactical world (stable codes; part of the trace hash).</summary>
public static class TacticalKinds
{
    public const int PulseStart = 101;
    public const int PulseEnd = 102;
    public const int Order = 103;
    public const int Step = 104;
    public const int Detect = 105;
    public const int Fire = 106;
    public const int Damage = 107;
    public const int Resolve = 108;
}

/// <summary>Counter-RNG stream ids used by the tactical world (CounterRng stream argument).</summary>
public static class TacticalStreams
{
    public const uint Detection = 0x7AC1_0001;
    public const uint Hit = 0x7AC1_0002;
    public const uint MovePriority = 0x7AC1_0003;
}
