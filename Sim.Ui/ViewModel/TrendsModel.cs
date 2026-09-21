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
///
/// TURN 0 — THE FOUNDING SAMPLE. The observation history holds one record per
/// STEP (turns 1..N); the T4.18 HistoryBuffer drew a founding sample at turn 0
/// as well, so the series started where the world did. That sample is kept
/// here for the keys whose record carries an OPENING — population
/// (Population.Opening), food (Food.GrainOpening), dwellings
/// (Housing.DwellingsOpening): the first observation's opening IS the founding
/// value, read off the record, no world consulted. It is prepended, so those
/// series have N + 1 values. The other keys have NO turn-0 value and get none:
/// inflow, outflow, births, deaths, harvest, eaten are per-step flows (nothing
/// flowed before the first step), and deficit, happiness, grievance are read
/// from rows the step's systems wrote on next, of which the record keeps no
/// prev-side copy — a founding value for them would have to be computed from
/// the world, which the graph must not do. Their series have N values and
/// start at turn 1. A settlement founded on the first step (Founded record)
/// did not exist at turn 0 and reads NaN there, never its zero opening.
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

    /// <summary>Whether the record carries an OPENING for this key — the keys
    /// that get a turn-0 founding sample.</summary>
    public static bool HasOpening(SeriesKey key) =>
        key is SeriesKey.Population or SeriesKey.Food or SeriesKey.Dwellings;

    /// <summary>The record's opening for a key with one — the value at the
    /// START of the step, which on the first observation is the founding
    /// value. Throws for a key without an opening.</summary>
    public static double Opening(SettlementRecord r, SeriesKey key)
    {
        ArgumentNullException.ThrowIfNull(r);
        return key switch
        {
            SeriesKey.Population => r.Population.Opening,
            SeriesKey.Food => r.Food.GrainOpening,
            SeriesKey.Dwellings => r.Housing.DwellingsOpening,
            _ => throw new ArgumentOutOfRangeException(nameof(key), key, "series key has no opening"),
        };
    }

    /// <summary>The founding (turn-0) value of one settlement: the opening on
    /// its record in the FIRST observation; NaN when it has no record there or
    /// that record is a founding record (it did not exist at turn 0).</summary>
    public static double FoundingValue(IObservationHistory history, int settlement, SeriesKey key)
    {
        ArgumentNullException.ThrowIfNull(history);
        if (!HasOpening(key) || history.Observations.Count == 0) return double.NaN;
        SettlementRecord[] rows = history.Observations[0].Settlements;
        for (int i = 0; i < rows.Length; i++)
            if (rows[i].Settlement == settlement) return rows[i].Founded ? double.NaN : Opening(rows[i], key);
        return double.NaN;
    }

    private static double[] Prepend(double turn0, double[] steps)
    {
        var result = new double[steps.Length + 1];
        result[0] = turn0;
        Array.Copy(steps, 0, result, 1, steps.Length);
        return result;
    }

    /// <summary>The scope label under the graph: what a world-scope value IS.</summary>
    public static string ScopeNote(SeriesKey key, bool worldScope) => !worldScope
        ? "settlement scope: the record's field on each observed turn"
        : IsExtensive(key)
            ? "world scope: sum over every settlement with a record on the turn"
            : "world scope: unweighted mean over settlements with a record - not a simulation quantity";

    /// <summary>One settlement's series, one value per observed turn (NaN where
    /// it has no record), with the turn-0 founding sample in front for a key
    /// that has an opening. Length == Observations.Count, + 1 when
    /// <see cref="HasOpening"/> and at least one turn was observed.</summary>
    public static double[] Settlement(IObservationHistory history, int settlement, SeriesKey key, int classId)
    {
        ArgumentNullException.ThrowIfNull(history);
        var into = new double[history.Observations.Count];
        int n = history.Series(settlement, key, into, classId);
        if (n != into.Length)
            throw new InvalidOperationException($"series wrote {n} of {into.Length} values");
        if (!HasOpening(key) || into.Length == 0) return into;
        return Prepend(FoundingValue(history, settlement, key), into);
    }

    /// <summary>The world series: per turn, the sum (extensive) or mean
    /// (intensive) over the settlements that carry a record on that turn. Built
    /// from the SETTLEMENT series read through the seam — one Series call per
    /// settlement id ever observed, summed column-wise — so there is no second
    /// copy of the key → record-field mapping here to drift from ObservationLog.
    /// For a key with an opening the turn-0 founding sample (the sum of the
    /// first observation's non-founding openings — every settlement that
    /// existed at turn 0) is prepended; the three opening keys are all
    /// extensive, so the founding sample is always a sum.</summary>
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
        if (!HasOpening(key) || turns == 0) return result;

        double founding = 0.0;
        int foundingCount = 0;
        for (int id = 0; id <= maxId; id++)
        {
            if (!present[id]) continue;
            double v = FoundingValue(history, id, key);
            if (double.IsNaN(v)) continue;
            founding += v;
            foundingCount++;
        }
        return Prepend(foundingCount == 0 ? double.NaN : founding, result);
    }

    /// <summary>
    /// The plot boundary: doubles → floats, with NO FABRICATED ZERO.
    ///
    /// This used to map every non-finite value to 0f. A NaN here means "this
    /// settlement has no record on this turn" — not yet founded, or absent from
    /// the observation — and drawing it as 0 asserted a reading that was never
    /// taken. For happiness that was actively dangerous: 0 is the ratified
    /// revolt condition (SettlementHappiness.RevoltThreshold), so a missing
    /// sample was drawn as a settlement in revolt.
    ///
    /// The honest treatment, and the one used here: CARRY FORWARD the last
    /// finite value, which says "no new reading, nothing changed on the chart",
    /// and OMIT the leading run before any finite value exists rather than
    /// inventing a value to start from. The series therefore starts where the
    /// data starts and is shorter than the double series by exactly that
    /// leading run. An all-non-finite series plots as nothing, which the draw
    /// code renders as "no data".
    ///
    /// This changes only what is DRAWN for a sample that was never measured.
    /// The double series returned by <see cref="Settlement"/> and
    /// <see cref="World"/> is untouched, so every value a metric MEASURES, and
    /// every min/max the axis is computed from, is exactly as before.
    /// </summary>
    public static float[] ForPlot(double[] series)
    {
        ArgumentNullException.ThrowIfNull(series);
        int first = 0;
        while (first < series.Length && !double.IsFinite(series[first])) first++;
        if (first == series.Length) return [];
        var plot = new float[series.Length - first];
        float carried = (float)series[first];
        for (int i = first; i < series.Length; i++)
        {
            if (double.IsFinite(series[i])) carried = (float)series[i];
            plot[i - first] = carried;
        }
        return plot;
    }

    public static string LastValueLine(double[] series) => series.Length == 0
        ? "no data"
        : string.Create(CultureInfo.InvariantCulture, $"latest {series[^1]:G6} over {series.Length} turn(s)");
}
