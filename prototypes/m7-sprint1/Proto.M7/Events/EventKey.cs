namespace Proto.M7.Events;

/// <summary>
/// Execution classes (spec §2.3), ascending execution order within ONE instant. Why this order:
///  - System (0): pulse/tick bookkeeping and interrupt checks run first so every later phase at the
///    instant sees a consistent frame (pulse budgets refreshed before anyone acts).
///  - Arrival/Movement (10) before Detection (20): you can only see what has arrived.
///  - Detection (20) before Fire (30): you can only shoot at what you have detected this instant.
///  - Fire (30) before Damage (40) before Resolve (50): ALL shots at an instant are taken against the
///    pre-damage world, ALL damage lands, and only then are deaths/morale resolved. That is the simultaneity
///    rule: two units that shoot each other at the same instant both get their shot (mutual kill).
///  - CampaignProcess (60): campaign-layer processes (construction completion) see the settled tactical frame.
///  - Significant (70): notification-type events that must observe every process result at the instant.
///  - PlayerOrder (250): ALWAYS LAST at an instant. An order issued while the runner is stopped at T is
///    delivered at T after everything the world did at T, which is exactly what the player saw when deciding;
///    and because nothing but orders lives in this class, an order can never reorder world events (replay proof).
/// The numeric gaps leave room for new classes without renumbering.
/// </summary>
public enum EventClass : byte
{
    System = 0,
    Arrival = 10,
    Detection = 20,
    Fire = 30,
    Damage = 40,
    Resolve = 50,
    CampaignProcess = 60,
    Significant = 70,
    PlayerOrder = 250,
}

/// <summary>
/// Total order over scheduled events: lexicographic (TimeMs, Class, Priority, SourceId, Seq).
/// Seq is a scheduler-global counter assigned at scheduling time, strictly increasing, so no two keys are
/// equal and heap extraction order is fully determined (O-6 tie-break mechanism; CLAUDE.md composite key
/// with a stable integer tie-break).
/// </summary>
public readonly record struct EventKey(long TimeMs, byte Class, int Priority, long SourceId, long Seq) : IComparable<EventKey>
{
    public int CompareTo(EventKey o)
    {
        int c = TimeMs.CompareTo(o.TimeMs);
        if (c != 0) return c;
        c = Class.CompareTo(o.Class);
        if (c != 0) return c;
        c = Priority.CompareTo(o.Priority);
        if (c != 0) return c;
        c = SourceId.CompareTo(o.SourceId);
        if (c != 0) return c;
        return Seq.CompareTo(o.Seq);
    }

    public EventClass EventClass => (EventClass)Class;
}

/// <summary>A generation-stamped reference to an entity. Id &lt; 0 means "no entity" (no liveness check).</summary>
public readonly record struct EntityRef(long Id, int Generation)
{
    public static readonly EntityRef None = new(-1, 0);
    public bool IsNone => Id < 0;
}

/// <summary>An event in the queue. Kind and payload words are interpreted by the world; the scheduler only orders.</summary>
public readonly record struct ScheduledEvent(EventKey Key, int Kind, EntityRef Target, EntityRef Source, long A, long B, long C);

/// <summary>The world the scheduler drives. IsLive implements generation invalidation (spec §2.3).</summary>
public interface IEventWorld
{
    /// <summary>True when the referenced entity still exists with exactly that generation.</summary>
    bool IsLive(EntityRef entity);

    /// <summary>Apply the event. May schedule further events (strictly later in key order).</summary>
    void Execute(in ScheduledEvent ev, EventScheduler scheduler);
}
