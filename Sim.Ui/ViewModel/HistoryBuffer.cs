using Sim.Core.State;

namespace Sim.Ui.ViewModel;

/// <summary>
/// T2.10, D-028: the time-series ring buffer behind the graphs — UI-SIDE
/// history, never sim state. One sample per OBSERVED turn (End Turn drives
/// Capture; the founding state is captured at session start), for the world
/// totals and EVERY settlement (capture is selection-independent, so
/// switching the selected settlement never loses history). Documented D-028
/// limitations: REPLAY REBUILDS the buffer (the same UiSession path steps the
/// same turns), while loading a MID-GAME SAVE starts it fresh — sim-side
/// history tables would bloat saves for no determinism gain.
///
/// CAPACITY: 2048 samples (a full 6,000-year campaign is ~1,730 turns on the
/// canonical era table — the whole game fits; the ring exists so a longer
/// free-play session degrades by forgetting the OLDEST turns, never by
/// crashing). Values are stored as float for ImGui.PlotLines — presentation
/// precision, not sim arithmetic (law 5 bans float in SIM code; this is the
/// view layer). Non-finite inputs clamp to 0 (an extinct settlement plots a
/// flat zero line, never NaN).
/// </summary>
public sealed class HistoryBuffer
{
    public const int Capacity = 2048;

    public enum Metric { Population = 0, Food = 1, Grievance = 2 }

    private sealed class Series
    {
        private readonly float[] _ring = new float[Capacity];
        private int _count, _next;

        public void Add(float value)
        {
            _ring[_next] = float.IsFinite(value) ? value : 0f;
            _next = (_next + 1) % Capacity;
            if (_count < Capacity) _count++;
        }

        public int Count => _count;

        /// <summary>Chronological copy (oldest first) — the shape PlotLines
        /// consumes; rebuilt on demand, sized to the samples present.</summary>
        public float[] Snapshot()
        {
            var result = new float[_count];
            int start = (_next - _count + Capacity) % Capacity;
            for (int i = 0; i < _count; i++) result[i] = _ring[(start + i) % Capacity];
            return result;
        }
    }

    private readonly Series[] _world = [new(), new(), new()];
    private readonly Dictionary<int, Series[]> _settlements = [];
    private long _lastTurn = long.MinValue;

    public int SampleCount => _world[0].Count;

    private Series[] For(int settlementId)
    {
        if (!_settlements.TryGetValue(settlementId, out Series[]? s))
        {
            s = [new(), new(), new()];
            _settlements[settlementId] = s;
        }
        return s;
    }

    /// <summary>One sample per observed turn: idempotent against a repeated
    /// Capture of the same turn (a redraw must not double-sample).</summary>
    public void Capture(IReadOnlyWorldState world)
    {
        if (world.Clock.Turn == _lastTurn) return;
        _lastTurn = world.Clock.Turn;

        long worldPop = 0, worldFood = 0;
        double worldGrievance = 0.0;

        for (int i = 0; i < world.Settlements.Count; i++)
        {
            SettlementId id = world.Settlements[i].Id;
            long pop = 0;
            for (int b = 0; b < world.Buckets.Count; b++)
                if (world.Buckets[b].Settlement == id) pop += world.Buckets[b].Count.Value;
            long food = 0;
            for (int f = 0; f < world.FoodStores.Count; f++)
                if (world.FoodStores[f].Settlement == id) { food = world.FoodStores[f].Store.Value; break; }
            double grievance = 0.0;
            for (int g = 0; g < world.Grievances.Count; g++)
                if (world.Grievances[g].Settlement == id) { grievance = world.Grievances[g].Value; break; }

            Series[] s = For(id.Value);
            s[(int)Metric.Population].Add(pop);
            s[(int)Metric.Food].Add(food);
            s[(int)Metric.Grievance].Add((float)grievance);

            worldPop += pop;
            worldFood += food;
            worldGrievance += grievance;
        }

        _world[(int)Metric.Population].Add(worldPop);
        _world[(int)Metric.Food].Add(worldFood);
        _world[(int)Metric.Grievance].Add((float)worldGrievance);
    }

    public float[] World(Metric metric) => _world[(int)metric].Snapshot();

    /// <summary>A settlement never captured (unknown id) yields an empty
    /// series — the graph renders "no data", never crashes.</summary>
    public float[] Settlement(int settlementId, Metric metric) =>
        _settlements.TryGetValue(settlementId, out Series[]? s)
            ? s[(int)metric].Snapshot()
            : [];
}
