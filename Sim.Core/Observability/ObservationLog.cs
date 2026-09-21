using Sim.Core.State;
using Sim.Core.Systems;

namespace Sim.Core.Observability;

/// <summary>
/// The numeric per-settlement series the UI graphs, addressed by key so the
/// window/disk/replay split behind <see cref="IObservationHistory"/> (§7) never
/// leaks into a graph. <see cref="Grievance"/> takes a class id; every other key
/// ignores it.
/// </summary>
public enum SeriesKey
{
    Population,       // Population.Closing
    Food,             // Food.GrainClosing
    Deficit,          // Food.DeficitRatio
    Happiness,        // Social.Happiness
    Grievance,        // Social.Grievance[class].Value  (NaN when the class has no row)
    Inflow,           // Population.Inflow
    Outflow,          // Population.Outflow
    Births,           // Population.Births
    Deaths,           // Population.Deaths
    Harvest,          // Food.Harvest
    Eaten,            // Food.Eaten
    Dwellings,        // Housing.DwellingsClosing
}

/// <summary>
/// §7 — THE ONE READ SEAM. The UI and the CLI read history through this and
/// nothing else, so the in-memory window, the JSONL on disk and reconstruction
/// by replay can be split behind it later without a graph noticing. T4.19
/// ships the in-memory implementation only; the seam is what ships now.
/// </summary>
public interface IObservationHistory
{
    /// <summary>Observed steps, oldest first. Empty until the first step.</summary>
    IReadOnlyList<TurnObservation> Observations { get; }

    /// <summary>Turn of the first / last observation; −1 when empty.</summary>
    long FirstTurn { get; }
    long LastTurn { get; }

    /// <summary>The observation whose record is for <paramref name="turn"/>, or null.</summary>
    TurnObservation? At(long turn);

    /// <summary>One settlement's record on one turn, or null (not yet founded, or no such turn).</summary>
    SettlementRecord? Settlement(long turn, int settlement);

    /// <summary>
    /// Fills <paramref name="into"/> with one value per observed turn, oldest
    /// first, for one settlement; NaN where the settlement has no record on that
    /// turn (it was not yet founded). Returns the number of values written
    /// (min of Observations.Count and into.Length).
    /// </summary>
    int Series(int settlement, SeriesKey key, Span<double> into, int classId = -1);

    IReadOnlyList<PolicyChange> PolicyChanges { get; }
    IReadOnlyList<PolicyState> PolicyStates { get; }
}

/// <summary>
/// §7 — THE IN-MEMORY OBSERVATION LOG: append-only, one <see cref="TurnObservation"/>
/// per step, built by calling <see cref="Observe"/> once per step with the two
/// worlds. It RETAINS NEITHER WORLD — only the records — so nothing a system
/// could read is held here, and it is referenced by no pipeline object; the
/// executor and the systems cannot reach it. Determinism of the world is
/// therefore structural, and it is also measured: the world hash after N steps
/// is bit-identical with and without a log attached (Sim.Tests/Observability).
///
/// The bounded window (last N turns + JSONL + replay) is NOT implemented here
/// (§7): nothing in M4 needs it, and the seam it would sit behind exists.
/// </summary>
public sealed class ObservationLog : IObservationHistory
{
    private readonly List<TurnObservation> _observations = [];
    private readonly List<PolicyChange> _changes = [];
    private readonly List<PolicyState> _states = [];

    public IReadOnlyList<TurnObservation> Observations => _observations;
    public IReadOnlyList<PolicyChange> PolicyChanges => _changes;
    public IReadOnlyList<PolicyState> PolicyStates => _states;

    public long FirstTurn => _observations.Count == 0 ? -1 : _observations[0].Turn.Turn;
    public long LastTurn => _observations.Count == 0 ? -1 : _observations[^1].Turn.Turn;

    /// <summary>
    /// Observe one completed step. <paramref name="ordersApplied"/> are the log
    /// rows delivered to this step — <see cref="OrderApplied.For"/> on the order
    /// log with <c>prev.Clock.Turn</c>, the executor's own delivery rule.
    /// Observations must arrive in turn order; a gap or a repeat is an error,
    /// because a log with holes would make every series silently misaligned.
    /// </summary>
    public TurnObservation Observe(
        IReadOnlyWorldState prev, IReadOnlyWorldState next, SimConfig cfg, OrderApplied[] ordersApplied)
    {
        if (_observations.Count > 0 && next.Clock.Turn != LastTurn + 1)
        {
            throw new InvalidOperationException(
                $"observation log is contiguous: last observed turn {LastTurn}, offered turn {next.Clock.Turn}.");
        }
        TurnObservation observation = Observer.Observe(prev, next, cfg, ordersApplied);
        _observations.Add(observation);
        PolicyHistory.Observe(prev, next, ordersApplied, _changes, _states);
        return observation;
    }

    /// <summary>As above for callers holding a <see cref="WorldState"/> pair.</summary>
    public TurnObservation Observe(WorldState prev, WorldState next, SimConfig cfg, OrderApplied[] ordersApplied)
        => Observe((IReadOnlyWorldState)prev, next, cfg, ordersApplied);

    public TurnObservation? At(long turn)
    {
        if (_observations.Count == 0) return null;
        long index = turn - FirstTurn;
        if (index < 0 || index >= _observations.Count) return null;
        return _observations[(int)index];
    }

    public SettlementRecord? Settlement(long turn, int settlement)
    {
        TurnObservation? o = At(turn);
        if (o is null) return null;
        for (int i = 0; i < o.Settlements.Length; i++)
            if (o.Settlements[i].Settlement == settlement) return o.Settlements[i];
        return null;
    }

    public int Series(int settlement, SeriesKey key, Span<double> into, int classId = -1)
    {
        int n = Math.Min(_observations.Count, into.Length);
        for (int t = 0; t < n; t++)
        {
            SettlementRecord? r = null;
            SettlementRecord[] rows = _observations[t].Settlements;
            for (int i = 0; i < rows.Length; i++) if (rows[i].Settlement == settlement) { r = rows[i]; break; }
            into[t] = r is null ? double.NaN : Value(r, key, classId);
        }
        return n;
    }

    private static double Value(SettlementRecord r, SeriesKey key, int classId)
    {
        switch (key)
        {
            case SeriesKey.Population: return r.Population.Closing;
            case SeriesKey.Food: return r.Food.GrainClosing;
            case SeriesKey.Deficit: return r.Food.DeficitRatio;
            case SeriesKey.Happiness: return r.Social.Happiness;
            case SeriesKey.Grievance:
                for (int i = 0; i < r.Social.Grievance.Length; i++)
                    if (r.Social.Grievance[i].Class == classId) return r.Social.Grievance[i].Value;
                return double.NaN;
            case SeriesKey.Inflow: return r.Population.Inflow;
            case SeriesKey.Outflow: return r.Population.Outflow;
            case SeriesKey.Births: return r.Population.Births;
            case SeriesKey.Deaths: return r.Population.Deaths;
            case SeriesKey.Harvest: return r.Food.Harvest;
            case SeriesKey.Eaten: return r.Food.Eaten;
            case SeriesKey.Dwellings: return r.Housing.DwellingsClosing;
            default: throw new ArgumentOutOfRangeException(nameof(key), key, "unknown series key");
        }
    }
}
