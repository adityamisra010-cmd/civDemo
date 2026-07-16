using Sim.Core;
using Sim.Core.Kernel;
using Sim.Core.State;
using Sim.Core.Systems.Growth;
using Sim.Core.Systems.Weather;
using Sim.Tests.TestUtil;

namespace Sim.Tests.Kernel;

// T0.5 acceptance: one-turn lag, dt-halving, order-permutation invariance,
// clone non-contamination under stepping.
public class TurnExecutorTests
{
    // A flat one-band era table keeps dt constant where a test needs simplicity.
    private static EraTable FlatEra(double dtYears, long spanYears = 100_000) =>
        EraTableLoader.Load($$"""
            { "bands": [ { "name": "flat", "startYear": 0, "endYear": {{spanYears}},
                           "dtYears": {{dtYears.ToString(System.Globalization.CultureInfo.InvariantCulture)}} } ] }
            """);

    private static WorldState NewWorld(ulong seed, int regions)
    {
        var world = new WorldState(seed);
        for (int i = 0; i < regions; i++)
            world.Regions.Add(new RegionRow(new RegionId(i)));
        return world;
    }

    private static long ExpectedGrowthDelta(double rainMmPerYear, double dtYears, ref double remainder)
    {
        double exact = GrowthSystem.GrowthPerMmPerYear * rainMmPerYear * dtYears + remainder;
        long delta = (long)Math.Floor(exact);
        remainder = exact - delta;
        return delta;
    }

    [Fact]
    public void OneTurnLag_GrowthAtTurnT_UsesRainFromTurnTMinus1()
    {
        var executor = new TurnExecutor(FlatEra(10), [SystemCatalog.Weather(), SystemCatalog.Growth()]);
        var w0 = NewWorld(seed: 42, regions: 1);

        // Turn 1: weather writes R1 into Next, but growth read Prev (no rainfall
        // rows yet) — biomass must remain 0 even though R1 > 0 exists in w1.
        var w1 = executor.Step(w0);
        double r1 = w1.Rainfall[0].RainfallMmPerYear;
        Assert.True(r1 > 0.0);
        Assert.Equal(0L, w1.Biomass[0].Biomass.Value);

        // Turn 2: growth must integrate EXACTLY from R1 (t−1), not from w2's fresh R2.
        var w2 = executor.Step(w1);
        double r2 = w2.Rainfall[0].RainfallMmPerYear;
        Assert.NotEqual(r1, r2); // distinct draws, so the assertion below discriminates

        double remainder = 0.0;
        long expected = ExpectedGrowthDelta(r1, dtYears: 10.0, ref remainder);
        Assert.Equal(expected, w2.Biomass[0].Biomass.Value);
        Assert.Equal(remainder, w2.Biomass[0].GrowthRemainder);
    }

    [Fact]
    public void DtHalving_BiomassWithinAnalyticTolerance()
    {
        // Growth-only pipeline over a FIXED rainfall field (set directly in state,
        // so both runs integrate the same signal — no RNG path divergence).
        //
        // Tolerance derivation: biomass growth is dB/dt = k·R with k, R constant —
        // the integrand is state-independent, so forward Euler is EXACT for any dt:
        // both runs compute B(T) = floor-accumulate(k·R·dt per turn) toward k·R·T.
        // The only divergence is floating-point: each turn adds k·R·dt + remainder
        // in double and floors; the two runs perform different numbers of additions
        // (N vs 2N), so their cumulative double error differs by at most a few ULPs
        // of k·R·T — far below 1 base unit — and the floor/remainder accumulator
        // confines the visible difference to AT MOST 1 base unit of biomass.
        // (A state-DEPENDENT rate would add O(dt) truncation error on top; that
        // regime arrives with real systems at M2+.)
        const double rain = 123.456;
        const double totalYears = 1000.0;

        WorldState Run(double dtYears, int turns)
        {
            var world = NewWorld(seed: 1, regions: 2);
            world.Rainfall.Add(new RainfallRow(new RegionId(0), rain));
            world.Rainfall.Add(new RainfallRow(new RegionId(1), rain));
            var executor = new TurnExecutor(FlatEra(dtYears), [SystemCatalog.Growth()]);
            return executor.Run(world, turns);
        }

        var full = Run(dtYears: 10.0, turns: (int)(totalYears / 10.0));   // 100 turns
        var half = Run(dtYears: 5.0, turns: (int)(totalYears / 5.0));     // 200 turns

        for (int i = 0; i < 2; i++)
        {
            long bFull = full.Biomass[i].Biomass.Value;
            long bHalf = half.Biomass[i].Biomass.Value;
            Assert.True(Math.Abs(bFull - bHalf) <= 1L,
                $"region {i}: |{bFull} - {bHalf}| exceeds the 1-unit analytic tolerance");
            // And both sit on the analytic value k·R·T within the same 1-unit bound.
            long analytic = (long)Math.Floor(GrowthSystem.GrowthPerMmPerYear * rain * totalYears);
            Assert.True(Math.Abs(bFull - analytic) <= 1L);
        }
    }

    [Fact]
    public void OrderPermutation_WeatherGrowthVsGrowthWeather_IdenticalEndStates()
    {
        // Both systems read only Prev; double buffering makes intra-turn order
        // invisible to them. Identical seeds must therefore yield IDENTICAL worlds
        // — full structural equality, every field, every table.
        var a = new TurnExecutor(FlatEra(10), [SystemCatalog.Weather(), SystemCatalog.Growth()])
            .Run(NewWorld(seed: 42, regions: 3), 10);
        var b = new TurnExecutor(FlatEra(10), [SystemCatalog.Growth(), SystemCatalog.Weather()])
            .Run(NewWorld(seed: 42, regions: 3), 10);

        Assert.True(WorldStates.StateEquals(a, b));
    }

    [Fact]
    public void CloneNonContamination_OriginalUntouchedWhileCloneSteps_FuturesIdentical()
    {
        var executor = new TurnExecutor(FlatEra(10), [SystemCatalog.Weather(), SystemCatalog.Growth()]);

        // Run to turn 3, clone, and keep a control copy of the original.
        var world = executor.Run(NewWorld(seed: 7, regions: 2), 3);
        var clone = world.Clone();
        var control = world.Clone();

        // Step the CLONE five turns with the SAME executor/system instances
        // (systems are stateless — instance sharing must be safe).
        var cloneFuture = executor.Run(clone, 5);

        // The original was never touched by the clone's stepping.
        Assert.True(WorldStates.StateEquals(world, control));

        // Both futures from the same turn-3 state are identical, draw for draw.
        var originalFuture = executor.Run(world, 5);
        Assert.True(WorldStates.StateEquals(originalFuture, cloneFuture));
    }

    [Fact]
    public void Executor_AdvancesClockWithTurnStartDt_AcrossBandBoundary()
    {
        // Two bands: 10y then 5y, boundary at year 20. The turn STARTING at the
        // last day of band A uses band A's dt (pinned executor rule (1)).
        var era = EraTableLoader.Load("""
            { "bands": [ { "name": "A", "startYear": 0,  "endYear": 20, "dtYears": 10 },
                         { "name": "B", "startYear": 20, "endYear": 40, "dtYears": 5 } ] }
            """);
        var executor = new TurnExecutor(era, [SystemCatalog.Weather()]);

        var world = NewWorld(seed: 1, regions: 1);
        world = executor.Step(world);                  // starts day 0 → dt 10y
        Assert.Equal(3600L, world.Clock.DtDays);
        world = executor.Step(world);                  // starts year 10 → dt 10y
        Assert.Equal((long)(2 * 3600), world.Clock.SimDays);
        world = executor.Step(world);                  // starts year 20 → band B, dt 5y
        Assert.Equal(1800L, world.Clock.DtDays);
        Assert.Equal(3L, world.Clock.Turn);
    }
}
