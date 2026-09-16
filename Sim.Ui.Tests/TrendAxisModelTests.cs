using Sim.Core.Observability;
using Sim.Core.State;
using Sim.Ui.ViewModel;
using Xunit;

namespace Sim.Ui.Tests;

/// <summary>
/// The trends axis: per-metric domain, axis label, colour band and the
/// no-fabricated-zero plot boundary. Every test here is a pure-function pin —
/// no Game, no window, no simulation.
/// </summary>
public class TrendAxisModelTests
{
    // ---- THE DIRECTOR'S COMPLAINT, PINNED DIRECTLY -------------------------

    [Fact]
    public void Happiness_In_90_To_100_Draws_Against_0_To_100_Not_90_To_100()
    {
        double[] series = [99.0, 97.5, 95.25, 92.0, 90.0, 93.75];
        PlotDomain d = TrendAxisModel.Domain(SeriesKey.Happiness, series);

        Assert.Equal(0.0, d.Floor);
        Assert.Equal(100.0, d.Ceiling);
        Assert.True(d.Bounded);
        // and specifically NOT the auto-scale the sentinels used to give
        Assert.NotEqual(90.0, d.Floor);
    }

    [Fact]
    public void HappinessCeiling_Is_The_Declared_Scale_Maximum()
        => Assert.Equal(SettlementHappiness.Max, TrendAxisModel.DeclaredDomain(SeriesKey.Happiness).Ceiling);

    [Fact]
    public void HappinessFloor_Is_The_Ratified_RevoltThreshold()
        => Assert.Equal(SettlementHappiness.RevoltThreshold,
            TrendAxisModel.DeclaredDomain(SeriesKey.Happiness).Floor);

    // ---- BOUNDED METRICS NEVER LEAVE THEIR DECLARED DOMAIN -----------------

    public static TheoryData<SeriesKey, double, double> BoundedDomains() => new()
    {
        { SeriesKey.Happiness, 0.0, 100.0 },
        { SeriesKey.Deficit, 0.0, 1.0 },
    };

    [Theory]
    [MemberData(nameof(BoundedDomains))]
    public void Bounded_Domain_Is_Fixed_For_Every_Shape_Of_Series(
        SeriesKey key, double floor, double ceiling)
    {
        double mid = (floor + ceiling) / 2.0;
        double[][] shapes =
        [
            [],                                   // empty
            [mid],                                // single sample
            [mid, mid, mid, mid],                 // all equal
            [floor, ceiling],                     // the full domain
            [ceiling, ceiling],                   // pressed against the top
            [double.NaN, mid, double.NaN],        // holes
            [double.NaN],                         // nothing finite at all
        ];
        foreach (double[] s in shapes)
        {
            PlotDomain d = TrendAxisModel.Domain(key, s);
            Assert.Equal(floor, d.Floor);
            Assert.Equal(ceiling, d.Ceiling);
            Assert.True(d.Bounded);
            Assert.True(d.Ceiling > d.Floor);
        }
    }

    // ---- UNBOUNDED NON-NEGATIVE METRICS FLOOR AT ZERO ---------------------

    [Theory]
    [InlineData(SeriesKey.Population)]
    [InlineData(SeriesKey.Food)]
    [InlineData(SeriesKey.Grievance)]
    [InlineData(SeriesKey.Births)]
    [InlineData(SeriesKey.Deaths)]
    [InlineData(SeriesKey.Inflow)]
    [InlineData(SeriesKey.Outflow)]
    [InlineData(SeriesKey.Harvest)]
    [InlineData(SeriesKey.Eaten)]
    [InlineData(SeriesKey.Dwellings)]
    public void Unbounded_NonNegative_Metric_Floors_At_Zero_Not_The_Data_Minimum(SeriesKey key)
    {
        double[] series = [4000.0, 4200.0, 4100.0, 4550.0];
        PlotDomain d = TrendAxisModel.Domain(key, series);

        Assert.Equal(0.0, d.Floor);          // NOT 4000
        Assert.Equal(4550.0, d.Ceiling);     // the ceiling does follow the data
        Assert.False(d.Bounded);
    }

    [Fact]
    public void Grievance_Has_No_Declared_Domain_And_Is_Not_Given_An_Invented_One()
    {
        Assert.False(TrendAxisModel.IsBounded(SeriesKey.Grievance));
        Assert.Throws<ArgumentOutOfRangeException>(
            () => TrendAxisModel.DeclaredDomain(SeriesKey.Grievance));
    }

    [Fact]
    public void Unbounded_AllEqual_And_SingleSample_Series_Keep_A_Drawable_Height()
    {
        PlotDomain flat = TrendAxisModel.Domain(SeriesKey.Population, [7.0, 7.0, 7.0]);
        Assert.Equal(0.0, flat.Floor);
        Assert.Equal(7.0, flat.Ceiling);

        PlotDomain one = TrendAxisModel.Domain(SeriesKey.Population, [12.0]);
        Assert.Equal(0.0, one.Floor);
        Assert.Equal(12.0, one.Ceiling);

        // an all-zero series still has a non-zero height
        PlotDomain zeros = TrendAxisModel.Domain(SeriesKey.Population, [0.0, 0.0]);
        Assert.Equal(0.0, zeros.Floor);
        Assert.Equal(1.0, zeros.Ceiling);
        Assert.True(zeros.Ceiling > zeros.Floor);

        PlotDomain empty = TrendAxisModel.Domain(SeriesKey.Population, []);
        Assert.Equal(0.0, empty.Floor);
        Assert.Equal(1.0, empty.Ceiling);
    }

    [Fact]
    public void PriceDomain_Floors_At_Zero_And_Fits_The_Ceiling()
    {
        PlotDomain d = TrendAxisModel.PriceDomain([3.5f, 4.25f, 3.75f]);
        Assert.Equal(0.0, d.Floor);
        Assert.Equal(4.25, d.Ceiling);
        Assert.False(d.Bounded);
    }

    // ---- THE AXIS LABEL, EXACT AND INVARIANT ------------------------------

    [Fact]
    public void AxisLabel_States_Floor_Ceiling_And_Observed_Range_Exactly()
    {
        double[] series = [99.0, 97.5, 95.25, 92.0, 90.0, 93.75];
        PlotDomain d = TrendAxisModel.Domain(SeriesKey.Happiness, series);
        Assert.Equal(
            "axis pinned 0 to 100 | observed 90 to 99",
            TrendAxisModel.AxisLabel(d, series));
    }

    [Fact]
    public void AxisLabel_For_A_Fitted_Axis_Says_So()
    {
        double[] series = [4000.0, 4200.0, 4100.0, 4550.5];
        PlotDomain d = TrendAxisModel.Domain(SeriesKey.Population, series);
        Assert.Equal(
            "axis fitted 0 to 4550.5 | observed 4000 to 4550.5",
            TrendAxisModel.AxisLabel(d, series));
    }

    [Fact]
    public void AxisLabel_With_No_Finite_Sample_Says_Observed_None()
    {
        double[] series = [double.NaN, double.NaN];
        PlotDomain d = TrendAxisModel.Domain(SeriesKey.Happiness, series);
        Assert.Equal("axis pinned 0 to 100 | observed none", TrendAxisModel.AxisLabel(d, series));
        Assert.Equal("axis fitted 0 to 1 | observed none",
            TrendAxisModel.AxisLabel(TrendAxisModel.Domain(SeriesKey.Population, series), series));
    }

    [Fact]
    public void AxisLabel_Formats_Through_InvariantCulture()
    {
        // The test host runs in globalization-invariant mode (a non-invariant
        // CultureInfo cannot even be constructed here), so this pins the
        // property directly rather than by switching culture: the label is
        // character-for-character what InvariantCulture produces, and carries a
        // '.' decimal point and no ',' decimal comma.
        double[] series = [1.5, 2.25];
        PlotDomain d = TrendAxisModel.Domain(SeriesKey.Deficit, series);
        string label = TrendAxisModel.AxisLabel(d, series);
        Assert.Equal(
            string.Create(System.Globalization.CultureInfo.InvariantCulture,
                $"axis pinned {0.0:G6} to {1.0:G6} | observed {1.5:G6} to {2.25:G6}"),
            label);
        Assert.Equal("axis pinned 0 to 1 | observed 1.5 to 2.25", label);
        Assert.DoesNotContain(",", label, StringComparison.Ordinal);
    }

    [Fact]
    public void Observed_Ignores_NonFinite_Samples()
    {
        (double min, double max, bool any) = TrendAxisModel.Observed(
            [double.NaN, 5.0, double.PositiveInfinity, 2.0, double.NegativeInfinity]);
        Assert.True(any);
        Assert.Equal(2.0, min);
        Assert.Equal(5.0, max);

        Assert.False(TrendAxisModel.Observed([double.NaN]).Any);
        Assert.False(TrendAxisModel.Observed([]).Any);
    }

    // ---- THE COLOUR BAND, BOUNDARIES PINNED -------------------------------

    [Theory]
    // happiness: low is bad, red anchored on the ratified revolt condition (0)
    [InlineData(0.0, PlotBand.Alarm)]            // RevoltThreshold itself
    [InlineData(24.999999, PlotBand.Alarm)]      // just below the 25% threshold
    [InlineData(25.0, PlotBand.Alarm)]           // AT the threshold: the worse band wins
    [InlineData(25.000001, PlotBand.Caution)]    // just above
    [InlineData(49.999999, PlotBand.Caution)]    // just below the 50% threshold
    [InlineData(50.0, PlotBand.Caution)]         // AT the threshold
    [InlineData(50.000001, PlotBand.Good)]       // just above
    [InlineData(90.0, PlotBand.Good)]            // the director's own data
    [InlineData(100.0, PlotBand.Good)]
    public void Happiness_Band_Boundaries_Are_Exact(double latest, PlotBand expected)
        => Assert.Equal(expected, TrendAxisModel.Band(SeriesKey.Happiness, latest));

    [Theory]
    // deficit: HIGH is bad, so the direction inverts
    [InlineData(0.0, PlotBand.Good)]
    [InlineData(0.499999, PlotBand.Good)]
    [InlineData(0.5, PlotBand.Caution)]          // health = 0.5, AT the threshold
    [InlineData(0.749999, PlotBand.Caution)]
    [InlineData(0.75, PlotBand.Alarm)]           // health = 0.25, AT the threshold
    [InlineData(1.0, PlotBand.Alarm)]
    public void Deficit_Band_Boundaries_Are_Exact(double latest, PlotBand expected)
        => Assert.Equal(expected, TrendAxisModel.Band(SeriesKey.Deficit, latest));

    [Fact]
    public void Band_Clamps_Values_Outside_The_Declared_Domain()
    {
        Assert.Equal(PlotBand.Alarm, TrendAxisModel.Band(SeriesKey.Happiness, -5.0));
        Assert.Equal(PlotBand.Good, TrendAxisModel.Band(SeriesKey.Happiness, 1000.0));
    }

    [Theory]
    [InlineData(SeriesKey.Population)]
    [InlineData(SeriesKey.Grievance)]
    [InlineData(SeriesKey.Food)]
    [InlineData(SeriesKey.Harvest)]
    public void Unbounded_Metrics_Are_Never_Coloured(SeriesKey key)
    {
        Assert.Equal(PlotBand.Neutral, TrendAxisModel.Band(key, 0.0));
        Assert.Equal(PlotBand.Neutral, TrendAxisModel.Band(key, 1e9));
        Assert.Null(TrendAxisModel.BandColour(PlotBand.Neutral));
        Assert.Equal("band: none - no agreed good/bad direction for this metric",
            TrendAxisModel.BandNote(PlotBand.Neutral));
    }

    [Fact]
    public void A_Missing_Reading_Is_Not_A_Verdict()
    {
        Assert.Equal(PlotBand.Neutral, TrendAxisModel.Band(SeriesKey.Happiness, double.NaN));
        Assert.Equal(PlotBand.Neutral, TrendAxisModel.Band(SeriesKey.Deficit, double.NaN));
    }

    [Fact]
    public void Every_NonNeutral_Band_Has_A_Distinct_Colour()
    {
        uint good = TrendAxisModel.BandColour(PlotBand.Good)!.Value;
        uint caution = TrendAxisModel.BandColour(PlotBand.Caution)!.Value;
        uint alarm = TrendAxisModel.BandColour(PlotBand.Alarm)!.Value;
        Assert.NotEqual(good, caution);
        Assert.NotEqual(caution, alarm);
        Assert.NotEqual(good, alarm);
    }

    // ---- NO FABRICATED ZERO -----------------------------------------------

    [Fact]
    public void ForPlot_Does_Not_Draw_A_Zero_Where_A_NaN_Was()
    {
        float[] plot = TrendsModel.ForPlot([95.0, double.NaN, 93.0]);
        Assert.Equal(3, plot.Length);
        Assert.Equal(95f, plot[0]);
        Assert.Equal(95f, plot[1]);          // carried forward, NOT 0
        Assert.NotEqual(0f, plot[1]);
        Assert.Equal(93f, plot[2]);
    }

    [Fact]
    public void ForPlot_Omits_The_Leading_Run_Before_Any_Finite_Value()
    {
        float[] plot = TrendsModel.ForPlot([double.NaN, double.NaN, 88.0, 87.0]);
        Assert.Equal(2, plot.Length);
        Assert.Equal(88f, plot[0]);
        Assert.Equal(87f, plot[1]);
    }

    [Fact]
    public void ForPlot_Of_An_AllNaN_Series_Is_Empty_Rather_Than_A_Row_Of_Zeroes()
        => Assert.Empty(TrendsModel.ForPlot([double.NaN, double.NaN]));

    [Fact]
    public void ForPlot_Of_An_Empty_Series_Is_Empty()
        => Assert.Empty(TrendsModel.ForPlot([]));

    [Fact]
    public void ForPlot_Of_A_Single_Finite_Sample_Keeps_It()
    {
        float[] plot = TrendsModel.ForPlot([42.5]);
        Assert.Equal(42.5f, Assert.Single(plot));
    }

    [Fact]
    public void ForPlot_Carries_A_Genuine_Zero_Through_Untouched()
    {
        // a MEASURED zero is still drawn as zero — only a MISSING sample is
        // treated differently, so the revolt condition remains visible
        float[] plot = TrendsModel.ForPlot([10.0, 0.0, double.NaN, 5.0]);
        Assert.Equal([10f, 0f, 0f, 5f], plot);
    }

    [Fact]
    public void ForPlot_Treats_Infinities_As_Missing_Too()
    {
        float[] plot = TrendsModel.ForPlot([3.0, double.PositiveInfinity, double.NegativeInfinity]);
        Assert.Equal([3f, 3f, 3f], plot);
    }

    // ---- ZERO-LENGTH AND SINGLE-ELEMENT SERIES DO NOT CRASH ---------------

    [Fact]
    public void Empty_And_Single_Element_Series_Render_Sanely_End_To_End()
    {
        foreach (SeriesKey key in new[] { SeriesKey.Happiness, SeriesKey.Deficit, SeriesKey.Population })
        {
            foreach (double[] s in new[] { Array.Empty<double>(), [5.0], new[] { double.NaN } })
            {
                PlotDomain d = TrendAxisModel.Domain(key, s);
                Assert.True(d.Ceiling > d.Floor);
                string label = TrendAxisModel.AxisLabel(d, s);
                Assert.StartsWith("axis ", label);
                double latest = s.Length == 0 ? double.NaN : s[^1];
                _ = TrendAxisModel.BandNote(TrendAxisModel.Band(key, latest));
                _ = TrendsModel.ForPlot(s);
            }
        }
    }
}
