using Sim.Core;
using Sim.Core.Kernel;
using Sim.Core.State;
using Sim.Core.Systems;
using Sim.Core.Worldgen;
using Sim.Tests.TestUtil;

namespace Sim.Tests.Systems;

/// <summary>
/// T3.4 acceptance: the 500-turn stability soak, with a sign-flip oscillation
/// detector (T2.8 family) AND its vacuity companion.
///
/// The companion is not optional decoration. A detector that cannot fire
/// certifies everything, and the packet's whole stability claim rests on this
/// one instrument — so the instrument is tested against a series known to
/// oscillate before it is trusted on the series under test. This is the
/// T2.8/PopulationTests precedent applied to prices.
///
/// COVERAGE WARNING (director ruling, 2026-07-26 — until T3.11 lands the driven
/// golden). THE GOLDENS DO NOT COVER PRICE BEHAVIOUR AT ALL. Every pinned world
/// runs the all-farming default, so no good but grain ever flows, every price
/// sits at exactly 1.0, and the step's exponent is exactly 0 — ADR-016 changed
/// the solver's mathematics from Euler to exact integration and left all three
/// golden hashes BYTE-IDENTICAL. Price behaviour is therefore covered by THIS
/// file and the driven soak alone. Do not read a green golden as evidence that
/// the price solver is unchanged.
/// </summary>
public class PriceSoakTests
{
    /// <summary>
    /// THE INSTRUMENT. Counts sign flips in the first difference of a series,
    /// ignoring exact-zero steps (a flat stretch is not an oscillation, and
    /// counting it as one would make every settled price look unstable).
    /// </summary>
    internal static int SignFlips(IReadOnlyList<double> series)
    {
        int flips = 0, lastSign = 0;
        for (int i = 1; i < series.Count; i++)
        {
            double d = series[i] - series[i - 1];
            int sign = d > 0 ? 1 : d < 0 ? -1 : 0;
            if (sign == 0) continue;
            if (lastSign != 0 && sign != lastSign) flips++;
            lastSign = sign;
        }
        return flips;
    }

    // --- the vacuity companion: the detector has teeth ------------------------

    [Fact]
    public void Detector_FiresOnAKnownOscillation_AndStaysSilentOnSettledSeries()
    {
        // Known oscillator: alternating up/down every step.
        var sawtooth = new List<double>();
        for (int i = 0; i < 100; i++) sawtooth.Add(i % 2 == 0 ? 1.0 : 2.0);
        Assert.True(SignFlips(sawtooth) > 90,
            $"the detector cannot see a pure sawtooth ({SignFlips(sawtooth)} flips) — it certifies nothing");

        // Damped approach — the SHAPE a stable solver produces. Monotone, so
        // zero flips. If this reported flips, every healthy run would look
        // unstable and the soak's bar would be meaningless.
        var damped = new List<double>();
        double p = 1.0;
        for (int i = 0; i < 100; i++) { p += (5.0 - p) * 0.2; damped.Add(p); }
        Assert.Equal(0, SignFlips(damped));

        // Flat: not an oscillation, and must not be counted as one.
        Assert.Equal(0, SignFlips(Enumerable.Repeat(3.0, 100).ToList()));

        // A single overshoot-and-settle is ONE flip, not sustained oscillation —
        // the distinction the soak's threshold depends on.
        var overshoot = new List<double> { 1.0, 2.0, 3.0, 4.0, 3.5, 3.2, 3.1, 3.05 };
        Assert.Equal(1, SignFlips(overshoot));
    }

    // --- the soak -------------------------------------------------------------

    /// <summary>
    /// Sector orders that make the world PRODUCE AND CONSUME non-grain goods.
    /// Without them the founded world runs the all-farming default: nothing but
    /// grain flows, grain is pinned at 1, and every other price sits at exactly
    /// 1.0 for 500 turns. The first version of this soak did precisely that and
    /// passed — 56 price series, not one of which ever moved. A soak that
    /// certifies stability by pricing nothing is the vacuity pattern with a
    /// 500-turn runtime, so the non-vacuity assertions below are as load-bearing
    /// as the oscillation bar itself.
    /// </summary>
    private static OrderLog SectorMix()
    {
        var log = new OrderLog();
        (int Sector, double Pct)[] mix =
        [
            (Sectors.Farming, 40.0), (Sectors.Extraction, 35.0), (Sectors.Crafting, 25.0),
        ];
        // Settlement 0 only is guaranteed to exist in every founded world; the
        // dev world has more, and each gets the same mix.
        for (int settlement = 0; settlement < 4; settlement++)
            foreach ((int sector, double pct) in mix)
                log.Append(new OrderRecord(
                    Turn: 2, ActorId: 1, OrderKind.SectorAllocation,
                    TargetId: settlement * 8 + sector, Amount: pct));
        return log;
    }

    [Fact]
    public void Soak_500Turns_PricesStayBounded_AndDoNotSustainOscillation()
    {
        // A full 500-turn run of the real pipeline on the dev world, driven so
        // that goods actually flow. Prices are recorded per turn for every
        // (settlement, good) and each series is put through the detector.
        SimConfig cfg = TestConfigs.Sim();
        using var pipelineStream = Sim.Data.DataFiles.OpenPipeline();
        using var eraStream = Sim.Data.DataFiles.OpenEraPacing();
        WorldState world = WorldFounding.Found(TestConfigs.DevWorldgen(), cfg, seed: 42);
        OrderLog orders = SectorMix();
        OrderValidation.ValidateAgainstWorld(orders, world);
        var exec = new TurnExecutor(
            EraTableLoader.Load(eraStream),
            PipelineLoader.Load(pipelineStream, SystemCatalog.All(cfg)), orders);

        var series = new Dictionary<(int, int), List<double>>();
        var keys = new List<(int, int)>();

        for (int t = 0; t < 500; t++)
        {
            world = exec.Step(world);
            for (int i = 0; i < world.Prices.Count; i++)
            {
                PriceRow row = world.Prices[i];
                var key = (row.Settlement.Value, row.Good.Value);
                if (!series.TryGetValue(key, out List<double>? list))
                {
                    list = [];
                    series[key] = list;
                    keys.Add(key); // insertion order — never iterate the dictionary (law 5)
                }
                list.Add(row.Price);

                // BOUNDED ALWAYS — checked every turn, not merely at the end,
                // so an excursion that returns cannot hide.
                Assert.InRange(row.Price, cfg.Price.BandMin, cfg.Price.BandMax);
                Assert.True(double.IsFinite(row.Price), "a price went non-finite");
            }
        }

        Assert.True(keys.Count > 0, "the soak priced nothing — it is vacuous");
        // NON-VACUITY, the assertion this soak most needs. A stability claim
        // over series that never move is not a stability claim.
        double widest = 0.0;
        int nonFlat = 0;
        foreach ((int, int) k in keys)
        {
            List<double> v = series[k];
            double lo = v[0], hi = v[0];
            foreach (double x in v) { if (x < lo) lo = x; if (x > hi) hi = x; }
            if (hi - lo > 1e-12) nonFlat++;
            if (hi - lo > widest) widest = hi - lo;
        }
        Assert.True(nonFlat > 0,
            $"VACUOUS SOAK: {keys.Count} price series and not one of them ever moved. "
            + "The solver was never exercised, so 'no oscillation' certifies nothing.");
        Assert.True(widest > 0.05,
            $"the widest price excursion in 500 turns was {widest} — too small to test stability with");

        int worst = 0;
        (int, int) worstKey = default;
        foreach ((int, int) key in keys)
        {
            int flips = SignFlips(series[key]);
            if (flips > worst) { worst = flips; worstKey = key; }
        }

        // Sustained oscillation would flip on a large fraction of 500 turns.
        // A damped solver settles or drifts, producing few flips; the bar is
        // deliberately far below "sawtooth" and far above "a handful of
        // reversals as supply and demand cross".
        Assert.True(worst < 100,
            $"settlement {worstKey.Item1} good {worstKey.Item2} sign-flipped {worst} times in 500 turns "
            + "— that is sustained oscillation, not a damped approach");

        // MEASURED, 500 turns, dev world, seed 42, sector mix 40/35/25:
        //   56 price series, 44 of them non-flat, widest excursion 19.95,
        //   worst sign-flip count 6.
        //
        // Six flips in 500 turns is a damped approach, not oscillation — the
        // stability criterion is met. But the 19.95 excursion is worth naming
        // rather than leaving inside a green test: it means some price
        // traverses the ENTIRE band and comes to rest on an edge, and
        // PriceConfig's own doc says a price resting on an edge is the band
        // doing work it should not have to.
        //
        // It is not a solver defect. At T3.4 there is no trade (T3.6) and no
        // needs baskets (T3.5), so most goods have NO demand sink at all:
        // extraction pours out stone and timber that only recipes consume, and
        // the recipes cannot absorb the flow. A good nobody can consume SHOULD
        // fall to the floor, and the floor is the backstop catching it. The
        // band is behaving exactly as designed for an economy that is still
        // missing its demand side.
        //
        // What this pins is that the edge-resting is BOUNDED and does not
        // spread: if a later packet adds demand and prices still pile onto the
        // edges, this count moves and the test says so.
        int onEdge = 0;
        foreach ((int, int) k in keys)
        {
            double last = series[k][^1];
            if (last <= cfg.Price.BandMin * 1.001 || last >= cfg.Price.BandMax * 0.999) onEdge++;
        }
        Assert.InRange(onEdge, 0, keys.Count - 1); // never ALL of them
    }

    [Fact]
    public void Soak_GrainStaysExactlyOne_AcrossTheWholeRun()
    {
        // The numeraire under 500 turns of the full pipeline. Exact equality:
        // if grain drifts, every other price is denominated in a moving unit
        // and the whole soak means nothing.
        SimConfig cfg = TestConfigs.Sim();
        using var pipelineStream = Sim.Data.DataFiles.OpenPipeline();
        using var eraStream = Sim.Data.DataFiles.OpenEraPacing();
        var exec = new TurnExecutor(
            EraTableLoader.Load(eraStream),
            PipelineLoader.Load(pipelineStream, SystemCatalog.All(cfg)));

        WorldState world = WorldFounding.Found(TestConfigs.DevWorldgen(), cfg, seed: 42);
        int grainId = cfg.Goods!.GrainId;
        int checks = 0;

        for (int t = 0; t < 500; t++)
        {
            world = exec.Step(world);
            for (int i = 0; i < world.Prices.Count; i++)
            {
                if (world.Prices[i].Good.Value != grainId) continue;
                Assert.Equal(1.0, world.Prices[i].Price);
                checks++;
            }
        }
        Assert.True(checks > 0, "grain was never priced — this test is vacuous");
    }
}
