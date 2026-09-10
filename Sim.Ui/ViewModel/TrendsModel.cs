using System.Globalization;
using Sim.Core.Observability;
using Sim.Core.Systems;

namespace Sim.Ui.ViewModel;

/// <summary>One selectable metric of the TRENDS graph. <see cref="IsPrice"/>
/// metrics come from the T3.9a HistoryBuffer (the observation history has no
/// price series); everything else reads IObservationHistory.Series.</summary>
public sealed record TrendMetric(string Label, SeriesKey Key, int ClassId, bool IsPrice)
{
    public string ButtonLabel => Label;
}

/// <summary>
/// T4.19 lane B (B2) — THE TRENDS METRICS over the one read seam
/// (docs/observability-architecture.md §7, IObservationHistory.Series). One
/// graph, as T4.18 left it; the metric list is every SeriesKey (grievance per
/// registry class) plus the price series the old HistoryBuffer alone holds.
///
/// WORLD SCOPE is a sum over every settlement that has a record on the turn
/// for EXTENSIVE keys (population, food, inflow, outflow, births, deaths,
/// harvest, eaten, dwellings) and an unweighted MEAN over those settlements
/// for INTENSIVE keys (deficit, happiness, grievance) — labelled so, because
/// a "world deficit ratio" is not a simulation quantity and the graph must not
/// imply one. The series is float only at this boundary (ImGui.PlotLines);
/// the values are the records' doubles.
/// </summary>
public static class TrendsModel
{
    /// <summary>Every metric, in button order.</summary>
    public static IReadOnlyList<TrendMetric> Metrics(ClassEntry[] classes)
    {
        ArgumentNullException.ThrowIfNull(classes);
        var list = new List<TrendMetric>
        {
            new("population", SeriesKey.Population, -1, false),
            new("food", SeriesKey.Food, -1, false),
            new("deficit", SeriesKey.Deficit, -1, false),
            new("happiness", SeriesKey.Happiness, -1, false),
        };
        for (int i = 0; i < classes.Length; i++)
            list.Add(new TrendMetric("grievance " + classes[i].Name, SeriesKey.Grievance, classes[i].Id, false));
        list.Add(new("inflow", SeriesKey.Inflow, -1, false));
        list.Add(new("outflow", SeriesKey.Outflow, -1, false));
        list.Add(new("births", SeriesKey.Births, -1, false));
        list.Add(new("deaths", SeriesKey.Deaths, -1, false));
        list.Add(new("harvest", SeriesKey.Harvest, -1, false));
        list.Add(new("eaten", SeriesKey.Eaten, -1, false));
        list.Add(new("dwellings", SeriesKey.Dwellings, -1, false));
        list.Add(new("price", SeriesKey.Population, -1, true));   // Key unused for a price metric
        return list;
    }

    public static bool IsExtensive(SeriesKey key) => key switch
    {
        SeriesKey.Deficit or SeriesKey.Happiness or SeriesKey.Grievance => false,
        _ => true,
    };

    /// <summary>The scope label under the graph: what a world-scope value IS.</summary>
    public static string ScopeNote(SeriesKey key, bool worldScope) => !worldScope
        ? "settlement scope: the record's field on each observed turn"
        : IsExtensive(key)
            ? "world scope: sum over every settlement with a record on the turn"
            : "world scope: unweighted mean over settlements with a record - not a simulation quantity";

    /// <summary>One settlement's series, one value per observed turn (NaN where
    /// it has no record). Length == history.Observations.Count.</summary>
    public static double[] Settlement(IObservationHistory history, int settlement, SeriesKey key, int classId)
    {
        ArgumentNullException.ThrowIfNull(history);
        var into = new double[history.Observations.Count];
        int n = history.Series(settlement, key, into, classId);
        if (n != into.Length)
            throw new InvalidOperationException($"series wrote {n} of {into.Length} values");
        return into;
    }

    /// <summary>The world series: per turn, the sum (extensive) or mean
    /// (intensive) over the settlements that carry a record on that turn. Built
    /// from the SETTLEMENT series read through the seam — one Series call per
    /// settlement id ever observed, summed column-wise — so there is no second
    /// copy of the key → record-field mapping here to drift from ObservationLog.</summary>
    public static double[] World(IObservationHistory history, SeriesKey key, int classId)
    {
        ArgumentNullException.ThrowIfNull(history);
        IReadOnlyList<TurnObservation> observations = history.Observations;
        int turns = observations.Count;
        var sum = new double[turns];
        var count = new int[turns];

        // Every settlement id that appears in any observation, ascending, once.
        // Ids are small non-negative ints; a presence array avoids a HashSet.
        int maxId = -1;
        for (int t = 0; t < turns; t++)
        {
            SettlementRecord[] rows = observations[t].Settlements;
            for (int i = 0; i < rows.Length; i++) if (rows[i].Settlement > maxId) maxId = rows[i].Settlement;
        }
        var present = new bool[maxId + 1];
        for (int t = 0; t < turns; t++)
        {
            SettlementRecord[] rows = observations[t].Settlements;
            for (int i = 0; i < rows.Length; i++) present[rows[i].Settlement] = true;
        }

        var one = new double[turns];
        for (int id = 0; id <= maxId; id++)
        {
            if (!present[id]) continue;
            history.Series(id, key, one, classId);
            for (int t = 0; t < turns; t++)
            {
                if (double.IsNaN(one[t])) continue;   // no record on this turn (not yet founded)
                sum[t] += one[t];
                count[t]++;
            }
        }

        bool extensive = IsExtensive(key);
        var result = new double[turns];
        for (int t = 0; t < turns; t++)
            result[t] = count[t] == 0 ? double.NaN : (extensive ? sum[t] : sum[t] / count[t]);
        return result;
    }

    /// <summary>The plot boundary: doubles → floats, NaN → 0 (an absent record
    /// plots flat, never breaks the line — the HistoryBuffer convention).</summary>
    public static float[] ForPlot(double[] series)
    {
        var plot = new float[series.Length];
        for (int i = 0; i < series.Length; i++) plot[i] = double.IsFinite(series[i]) ? (float)series[i] : 0f;
        return plot;
    }

    public static string LastValueLine(double[] series) => series.Length == 0
        ? "no data"
        : string.Create(CultureInfo.InvariantCulture, $"latest {series[^1]:G6} over {series.Length} turn(s)");
}
