using Sim.Tests.TestUtil;

namespace Sim.Tests.Systems;

// T3.6 acceptance criterion 4, discharged BEFORE the detector watches trade
// (the directed prompt's explicit order): the detector FIRES on a known
// oscillation and stays SILENT on constant, monotone and one-flip series.
// These are proofs of the instrument, not of trade — trade's stability
// measurements are only admissible because these exist first.
public class OscillationDetectorTests
{
    private const double Tol = 1e-9;

    [Fact]
    public void Fires_OnKnownOscillation()
    {
        // A sustained zig-zag — the exact shape a positive trade→price loop
        // without a binding damper would produce.
        double[] series = [1.0, 1.4, 0.9, 1.5, 0.8, 1.6, 0.7];
        Assert.True(OscillationDetector.Oscillates(series, Tol));
        Assert.Equal(5, OscillationDetector.CountFlips(series, Tol));
    }

    [Fact]
    public void Silent_OnConstantSeries()
    {
        double[] series = [2.0, 2.0, 2.0, 2.0, 2.0, 2.0];
        Assert.False(OscillationDetector.Oscillates(series, Tol));
        Assert.Equal(0, OscillationDetector.CountFlips(series, Tol));
    }

    [Fact]
    public void Silent_OnMonotoneSeries_BothDirections()
    {
        double[] rising = [1.0, 1.1, 1.3, 1.6, 2.0, 2.5];
        double[] falling = [2.5, 2.0, 1.6, 1.3, 1.1, 1.0];
        Assert.False(OscillationDetector.Oscillates(rising, Tol));
        Assert.False(OscillationDetector.Oscillates(falling, Tol));
    }

    [Fact]
    public void Silent_OnOneFlip_ShockThenRelaxation()
    {
        // A shock followed by monotone relaxation is HEALTHY damped behaviour
        // and has exactly one flip — the floor of 3 exists to keep this out.
        double[] series = [1.0, 1.8, 1.5, 1.3, 1.2, 1.1, 1.05];
        Assert.False(OscillationDetector.Oscillates(series, Tol));
        Assert.Equal(1, OscillationDetector.CountFlips(series, Tol));
    }

    [Fact]
    public void ToleranceDeadband_NumericDustNeverManufacturesAFlip()
    {
        // ±1e-12 wiggle on a constant is measurement dust, not oscillation.
        double[] series = [1.0, 1.0 + 1e-12, 1.0 - 1e-12, 1.0 + 1e-12, 1.0];
        Assert.False(OscillationDetector.Oscillates(series, Tol));
        Assert.Equal(0, OscillationDetector.CountFlips(series, Tol));
    }

    [Fact]
    public void PlateauInsideZigZag_StillCountsTheReversals()
    {
        // A zero-delta plateau must not reset direction memory — otherwise a
        // sampled oscillation with occasional repeats would read as calm.
        double[] series = [1.0, 1.5, 1.5, 1.0, 1.0, 1.5, 1.0];
        Assert.Equal(3, OscillationDetector.CountFlips(series, Tol));
        Assert.True(OscillationDetector.Oscillates(series, Tol));
    }
}
