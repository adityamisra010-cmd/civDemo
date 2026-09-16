using System.Globalization;
using Sim.Core.Observability;
using Sim.Core.State;

namespace Sim.Ui.ViewModel;

/// <summary>How the latest reading of a metric sits inside its declared
/// domain. <see cref="Neutral"/> is not "middling" — it is "this metric has no
/// agreed good/bad direction", which is the honest answer for every unbounded
/// count and flow.</summary>
public enum PlotBand
{
    /// <summary>No agreed direction, or no finite reading. Draw in the plain
    /// plot colour.</summary>
    Neutral = 0,
    Good = 1,
    Caution = 2,
    Alarm = 3,
}

/// <summary>The vertical extent an axis is drawn over, and whether that extent
/// is the metric's TRUE domain (declared at source) or a window fitted to the
/// data.</summary>
/// <param name="Floor">Value drawn at the bottom edge of the plot.</param>
/// <param name="Ceiling">Value drawn at the top edge of the plot.</param>
/// <param name="Bounded">True when Floor/Ceiling are the metric's declared
/// domain and therefore do not move with the data.</param>
public readonly record struct PlotDomain(double Floor, double Ceiling, bool Bounded);

/// <summary>
/// THE TRENDS AXIS — per-metric domain, axis label and colour band.
///
/// WHY THIS EXISTS. Both plot sites passed <c>float.MaxValue, float.MaxValue</c>
/// to <c>ImGui.PlotLines</c>. Those two sentinels mean "auto-scale to the
/// series' own min and max", so a happiness series living in [90, 100] filled
/// the entire plot height and 90 was drawn sitting on the x-axis, with no axis
/// label anywhere to say the floor was 90 rather than 0. Every value was
/// correct and the picture was still misleading.
///
/// WHY NOT A LOGARITHMIC AXIS (asked for, and the wrong instrument):
///   - log compresses the HIGH end, which is exactly where this data lives —
///     it would squash 90..100 harder than a linear axis does, not open it up;
///   - it does nothing to separate 90 from 100, which is the actual complaint;
///   - log(0) is undefined, and a happiness of exactly 0 is the RATIFIED
///     revolt condition (<see cref="SettlementHappiness.RevoltThreshold"/>),
///     so the single most important value on the chart is the one a log axis
///     cannot draw at all.
/// The real defect is an axis that is unlabelled and unpinned. That is what is
/// fixed here: pin it where the domain is declared, print it where it is not.
///
/// EVERYTHING HERE IS PRESENTATION. No simulation constant is invented: the
/// bounded domains are read from the source that declares them
/// (<see cref="SettlementHappiness.Max"/>, the [0,1] declared for
/// <c>ConsumptionDeficitRow.DeficitRatio</c> in WorldState), and the colour
/// thresholds are fractions of the domain, labelled presentation-only below.
/// </summary>
public static class TrendAxisModel
{
    /// <summary>The deficit ratio's declared ceiling: <c>DeficitRatio</c> is
    /// "unmet demand / integer demand, in [0,1]" (Sim.Core WorldState). Stated
    /// here as a presentation copy of a documented bound, not a new rule.</summary>
    public const double DeficitMax = 1.0;

    // ---- COLOUR THRESHOLDS: PRESENTATION-ONLY ------------------------------
    // No declared "unhappy" or "in trouble" constant exists on this tree, and
    // this change is forbidden from inventing one. So the bands are fractions
    // of the metric's OWN declared domain, measured as a health fraction that
    // runs 0 (worst) to 1 (best). For happiness the worst end IS the ratified
    // revolt condition — health 0 is exactly RevoltThreshold = 0 — so the red
    // band is anchored on a ratified quantity and only its WIDTH is a
    // presentation choice. For deficit the direction is inverted (more unmet
    // demand is worse), so health = 1 - fraction.
    /// <summary>Health at or below this fraction of the domain draws red.</summary>
    public const double AlarmFraction = 0.25;

    /// <summary>Health at or below this fraction (and above <see cref="AlarmFraction"/>)
    /// draws amber.</summary>
    public const double CautionFraction = 0.50;

    /// <summary>Whether a metric has a declared domain that pins the axis.</summary>
    public static bool IsBounded(SeriesKey key) =>
        key is SeriesKey.Deficit or SeriesKey.Happiness;

    /// <summary>The declared domain of a bounded metric. Throws for an
    /// unbounded one — grievance has NO declared bound on this tree
    /// (GrievanceRow.Value is an unconserved double stock, manufactured by
    /// deprivation and destroyed by decay, with no stated maximum), so it is
    /// treated as unbounded rather than given an invented ceiling.</summary>
    public static PlotDomain DeclaredDomain(SeriesKey key) => key switch
    {
        SeriesKey.Happiness => new PlotDomain(0.0, SettlementHappiness.Max, true),
        SeriesKey.Deficit => new PlotDomain(0.0, DeficitMax, true),
        _ => throw new ArgumentOutOfRangeException(nameof(key), key, "series key has no declared domain"),
    };

    /// <summary>Whether a metric is a non-negative quantity — a count, a stock
    /// or a per-step flow. Its axis floor is 0 even when its ceiling is fitted
    /// to the data: a population series that never dips below 4000 must not
    /// imply that 4000 is the floor of the world. Every SeriesKey on this tree
    /// is non-negative (populations, grain stocks, migrations, births, deaths,
    /// harvest, eaten, dwellings, deficit ratios, happiness, and grievance,
    /// which is seeded at 0 and decays toward it), so this is true for all of
    /// them; it is a named rule rather than a blanket so that a future signed
    /// series has somewhere to say so.</summary>
    public static bool IsNonNegative(SeriesKey key) => true;

    /// <summary>The axis for a metric over a plotted window.
    /// BOUNDED metrics get their declared domain, which does not move with the
    /// data — this is the director's complaint, fixed: happiness in [90, 100]
    /// draws against 0..100. UNBOUNDED metrics get a fitted ceiling, but the
    /// floor is 0 for a non-negative quantity (never the data minimum), and
    /// the bounds are printed by <see cref="AxisLabel"/> so nothing is guessed.
    /// A degenerate window (no finite samples, or an all-equal series) widens
    /// upward to keep a drawable, non-zero height.</summary>
    public static PlotDomain Domain(SeriesKey key, double[] series)
    {
        ArgumentNullException.ThrowIfNull(series);
        if (IsBounded(key)) return DeclaredDomain(key);
        return Fitted(series, IsNonNegative(key));
    }

    /// <summary>The axis for the price series, which has no SeriesKey (prices
    /// come from the HistoryBuffer, not the observation history). A price is a
    /// non-negative unbounded quantity, so: floor 0, fitted ceiling.</summary>
    public static PlotDomain PriceDomain(float[] series)
    {
        ArgumentNullException.ThrowIfNull(series);
        var wide = new double[series.Length];
        for (int i = 0; i < series.Length; i++) wide[i] = series[i];
        return Fitted(wide, nonNegative: true);
    }

    private static PlotDomain Fitted(double[] series, bool nonNegative)
    {
        (double min, double max, bool any) = Observed(series);
        if (!any) return new PlotDomain(0.0, 1.0, false);
        double floor = nonNegative ? 0.0 : min;
        if (min < floor) floor = min;              // defensive: a negative sample is drawn, never clipped
        double ceiling = max;
        if (!(ceiling > floor)) ceiling = floor + 1.0;   // all-equal / single-sample series
        return new PlotDomain(floor, ceiling, false);
    }

    /// <summary>The observed min and max over the FINITE samples of a window;
    /// <c>any</c> is false when there are none.</summary>
    public static (double Min, double Max, bool Any) Observed(double[] series)
    {
        ArgumentNullException.ThrowIfNull(series);
        double min = 0.0, max = 0.0;
        bool any = false;
        for (int i = 0; i < series.Length; i++)
        {
            double v = series[i];
            if (!double.IsFinite(v)) continue;
            if (!any) { min = v; max = v; any = true; continue; }
            if (v < min) min = v;
            if (v > max) max = v;
        }
        return (min, max, any);
    }

    /// <summary>The line printed with the plot, so the bottom of the chart can
    /// never again mean something the reader has to guess. Exact and
    /// InvariantCulture; "pinned" vs "fitted" says whether the axis is the
    /// metric's declared domain or a window over the data.</summary>
    public static string AxisLabel(PlotDomain domain, double[] series)
    {
        ArgumentNullException.ThrowIfNull(series);
        string kind = domain.Bounded ? "pinned" : "fitted";
        (double min, double max, bool any) = Observed(series);
        return any
            ? string.Create(CultureInfo.InvariantCulture,
                $"axis {kind} {domain.Floor:G6} to {domain.Ceiling:G6} | observed {min:G6} to {max:G6}")
            : string.Create(CultureInfo.InvariantCulture,
                $"axis {kind} {domain.Floor:G6} to {domain.Ceiling:G6} | observed none");
    }

    /// <summary>Whether LOW values of a metric are the bad ones. Happiness: yes
    /// (0 is the revolt condition). Deficit: no — a deficit ratio of 0 is no
    /// unmet demand, and 1 is total deprivation, so the direction inverts.</summary>
    public static bool LowIsBad(SeriesKey key) => key switch
    {
        SeriesKey.Happiness => true,
        SeriesKey.Deficit => false,
        _ => throw new ArgumentOutOfRangeException(nameof(key), key, "series key has no agreed direction"),
    };

    /// <summary>
    /// The colour band of the latest reading. A pure function of the value and
    /// the metric's declared domain.
    ///
    /// UNBOUNDED metrics are always <see cref="PlotBand.Neutral"/>: there is no
    /// agreed good/bad direction for a population, a grain stock, a birth count
    /// or a price — a rising population is not "healthy" in any ratified sense,
    /// and colouring one green would be the graph asserting a judgement the
    /// simulation does not make.
    ///
    /// Boundaries, pinned exactly: health is (value - floor)/(ceiling - floor),
    /// inverted for a metric where high is bad, and clamped to the domain.
    /// health &lt;= 0.25 is Alarm, health &lt;= 0.50 is Caution, above that is
    /// Good. AT a threshold the worse band wins — a chart should not call a
    /// settlement fine on the exact boundary.
    /// </summary>
    public static PlotBand Band(SeriesKey key, double latest)
    {
        if (!IsBounded(key)) return PlotBand.Neutral;
        if (!double.IsFinite(latest)) return PlotBand.Neutral;   // no reading is not a verdict
        PlotDomain d = DeclaredDomain(key);
        double span = d.Ceiling - d.Floor;
        double fraction = (latest - d.Floor) / span;
        if (fraction < 0.0) fraction = 0.0;
        if (fraction > 1.0) fraction = 1.0;
        double health = LowIsBad(key) ? fraction : 1.0 - fraction;
        if (health <= AlarmFraction) return PlotBand.Alarm;
        if (health <= CautionFraction) return PlotBand.Caution;
        return PlotBand.Good;
    }

    /// <summary>The plot-line colour of a band, packed ABGR as ImGui takes it.
    /// Neutral returns null — the caller pushes no style and the theme's own
    /// plot colour stands.</summary>
    public static uint? BandColour(PlotBand band) => band switch
    {
        PlotBand.Good => 0xFF4CBB6Au,      // green
        PlotBand.Caution => 0xFF3CC0E0u,   // amber
        PlotBand.Alarm => 0xFF4040E0u,     // red
        _ => null,
    };

    /// <summary>The band named, for the label line. Neutral names itself so the
    /// reader knows the absence of colour is deliberate.</summary>
    public static string BandNote(PlotBand band) => band switch
    {
        PlotBand.Good => "band: green (healthy)",
        PlotBand.Caution => "band: amber (mid)",
        PlotBand.Alarm => "band: red (low)",
        _ => "band: none - no agreed good/bad direction for this metric",
    };
}
