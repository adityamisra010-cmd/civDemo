namespace Proto.M7.War;

/// <summary>
/// A polity's unit or city that can observe a war theatre: position (X, Y) in integer map cells, sight radius R cells,
/// present during [FromMs, UntilMs) on the global timeline.
/// </summary>
public readonly record struct WarObserver(int Polity, long X, long Y, long Radius, long FromMs, long UntilMs);

/// <summary>
/// The prototype's fog-of-war predicate for War Pulse locality (DIR5 §2.4: pulses "are local to wars and their
/// participants", "do not automatically expose distant wars to uninvolved civilizations", "become known through
/// observation by units or cities within explored areas", "must respect fog of war"; ADR-061 §3.11 Locality).
///
/// RULE (prototype simplification, documented in the T2 report): polity P can see war W at instant t iff
///   (a) P is a participant of W, or
///   (b) some observer O of P is present at t (O.FromMs &lt;= t &lt; O.UntilMs) and the theatre cell of W lies inside O's
///       sight disk: (O.X - W.X)^2 + (O.Y - W.Y)^2 &lt;= O.Radius^2 (exact Int128 arithmetic).
/// A cell inside a present observer's sight is explored by construction, so (b) is "observation by units or cities
/// within explored areas". Not modelled: terrain line of sight, explored-set memory, moving observers (an observer that
/// moves is a sequence of observers with adjacent presence intervals), detection probability. The predicate is a pure
/// function of static data and t, so it is identical in a live run, a run without stops and a replay.
/// </summary>
public sealed class WarFog
{
    private readonly WarObserver[] _observers;

    public WarFog(WarObserver[] observers)
    {
        foreach (var o in observers)
            if (o.Radius < 0 || o.UntilMs < o.FromMs || o.Polity < 0) throw new ArgumentOutOfRangeException(nameof(observers));
        _observers = (WarObserver[])observers.Clone();
    }

    public static readonly WarFog None = new(Array.Empty<WarObserver>());

    public IReadOnlyList<WarObserver> Observers => _observers;

    /// <summary>True iff some observer of the polity, present at t, has the cell (x, y) in sight.</summary>
    public bool Observes(int polity, long x, long y, long t)
    {
        foreach (var o in _observers)
        {
            if (o.Polity != polity || t < o.FromMs || t >= o.UntilMs) continue;
            Int128 dx = (Int128)o.X - x, dy = (Int128)o.Y - y, r = o.Radius;
            if (dx * dx + dy * dy <= r * r) return true;
        }
        return false;
    }
}
