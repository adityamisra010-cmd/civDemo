namespace Proto.M7.Events;

/// <summary>
/// The delegated time/event rules of DIR5 §2.5 ("event priority, tie-breaking, collision handling, interruption rules and
/// RNG-consumption rules"), stated once, ADR-061 §3.4–§3.9. Each rule names the code that enforces it.
///
/// R1 PRIORITY. Within one instant, events execute by EventClass ascending (EventKey.cs: System 0 &lt; Arrival 10 &lt;
///    Detection 20 &lt; Fire 30 &lt; Damage 40 &lt; Resolve 50 &lt; CampaignProcess 60 &lt; Significant 70 &lt; PlayerOrder 250),
///    then by the integer Priority set by the scheduling rule (never a fractional score; a score enters as (score, id)).
/// R2 TIE-BREAK. Then SourceId (a stable integer identity, never a hash), then Seq (scheduler-global, strictly
///    increasing at scheduling time). The key (TimeMs, Class, Priority, SourceId, Seq) is unique: a total order.
///    Causality: a new key must be greater than the executing key (EventScheduler.ScheduleCore).
/// R3 COLLISIONS. (a) Same instant: phases above, so every Fire at T sees the pre-damage world (mutual kill).
///    (b) Invalidation: a stale target/source generation at pop is traced Dropped, never silently skipped.
///    (c) Cancellation: a tombstone keyed by Seq (TombstoneSet); the event is traced Cancelled when it comes due.
///    (d) Several significant events at one instant: ONE stop and ONE DecisionWindow listing all of them in key order.
///    (e) An order whose instant is not the latest window instant is traced Refused (EventScheduler.OrderGate).
///    (f) An order the world rejects (unknown target, bad amount) executes as a recorded refusal and changes no state.
/// R4 INTERRUPTION. A window opens at T when an executed non-order event's KIND is in SignificantKinds (data; the list
///    is a Director decision, OPEN). It closes before the first key with time &gt; T or class &gt;= PlayerOrder. Orders
///    never open windows (no chained windows, no action multiplication). The split is a function of world state only:
///    windows are recorded with stops on or off and in replay; a stop consumes zero simulated time. Orders issued at a
///    window are stamped T and execute at T in class PlayerOrder, after every world event at T. Replay never stops.
/// R5 RNG. Event-scheduled randomness is counter-based (Rng.CounterRng.Draw(seed, stream, a, b, c, index)) keyed by
///    stable event identity (source id, instant, kind, index), NEVER by Seq, window index or a sequential cursor that an
///    order or a stop could advance. So a draw does not depend on whether a window was shown, whether the runner stopped,
///    or whether an order was issued, unless that order changed the drawing event itself. Presentation never draws.
/// </summary>
public static class EventRules
{
    /// <summary>First class reserved for orders issued through the Runner; Schedule rejects it and everything above (R1, R4).</summary>
    public const byte FirstReservedClass = (byte)EventClass.PlayerOrder;
}
