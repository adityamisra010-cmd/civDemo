using Sim.Core;
using Sim.Core.Kernel;
using Sim.Core.State;
using Sim.Core.Systems;
using Sim.Core.Worldgen;
using Sim.Tests.TestUtil;

namespace Sim.Tests.Systems;

// T2.8 commit-1 acceptance (director ruling — the ping-pong attractor was a
// D-021 paired-feedback violation): the gap-closing flow cap and the EMA-
// smoothed driver make two-turn oscillation structurally impossible at the
// configs that used to bifurcate; the old bifurcation rig is now a PERMANENT
// regression test, the oscillation detector carries its own teeth, and
// occupancy concentration stays bounded through canonical autoplay. The cap
// and the filter are also pinned exactly, and the filter is dt-correct.
public class MigrationStabilityTests
{
    private const ulong Seed = 42;

    private static EraTable FlatEra(double dtYears) => EraTableLoader.Load(
        $$"""{ "bands": [ { "name": "flat", "startYear": 0, "endYear": 100000, "dtYears": {{dtYears.ToString(System.Globalization.CultureInfo.InvariantCulture)}} } ] }""");

    private static EraTable CanonicalEra()
    {
        using var stream = Sim.Data.DataFiles.OpenEraPacing();
        return EraTableLoader.Load(stream);
    }

    private static TurnExecutor ProductionExecutor(SimConfig cfg)
    {
        using var stream = Sim.Data.DataFiles.OpenPipeline();
        return new TurnExecutor(CanonicalEra(), PipelineLoader.Load(stream, SystemCatalog.All(cfg)));
    }

    /// <summary>The oscillation detector: sign flips in a net-flow series,
    /// zeros transparent (a zero neither flips nor resets the sign). A pure
    /// function so its own teeth are unit-testable.</summary>
    internal static int SignFlipCount(long[] series)
    {
        int flips = 0, lastSign = 0;
        for (int i = 0; i < series.Length; i++)
        {
            int sign = Math.Sign(series[i]);
            if (sign == 0) continue;
            if (lastSign != 0 && sign != lastSign) flips++;
            lastSign = sign;
        }
        return flips;
    }

    /// <summary>The two-turn-attractor signature: CONSECUTIVE alternations —
    /// a flip whose immediately preceding nonzero step also flipped. Isolated
    /// direction reversals (regime shifts as stores evolve, famine pulses)
    /// score zero; the old ping-pong (flip every turn) scores ~length.
    /// Sharpened at T2.7b: the honest post-ruling dynamics reverse direction
    /// legitimately at Malthus pulses, which the raw flip count cannot tell
    /// from oscillation.</summary>
    internal static int ConsecutiveAlternationCount(long[] series)
    {
        int count = 0, lastSign = 0;
        bool lastWasFlip = false;
        for (int i = 0; i < series.Length; i++)
        {
            int sign = Math.Sign(series[i]);
            if (sign == 0) continue;
            bool flip = lastSign != 0 && sign != lastSign;
            if (flip && lastWasFlip) count++;
            lastWasFlip = flip;
            lastSign = sign;
        }
        return count;
    }

    [Fact]
    public void Detector_Teeth_SyntheticPingPongCaught_SmoothDriftPasses()
    {
        // The detector must SEE a two-turn oscillation (adversarial lens 1
        // demands the instrument is proven, not assumed): a synthetic
        // alternating series flips every step; a monotone drift never flips;
        // a single regime change flips once.
        var pingPong = new long[20];
        for (int i = 0; i < 20; i++) pingPong[i] = i % 2 == 0 ? 500 : -500;
        Assert.Equal(19, SignFlipCount(pingPong));
        Assert.Equal(18, ConsecutiveAlternationCount(pingPong));

        var drift = new long[20];
        for (int i = 0; i < 20; i++) drift[i] = 10 + i;
        Assert.Equal(0, SignFlipCount(drift));
        Assert.Equal(0, ConsecutiveAlternationCount(drift));

        var oneTurn = new long[] { 100, 80, 0, -60, -40, -20 };
        Assert.Equal(1, SignFlipCount(oneTurn));
        Assert.Equal(0, ConsecutiveAlternationCount(oneTurn));

        // Isolated reversals (a famine pulse in, then out, turns later) score
        // zero alternations; only back-to-back flipping registers.
        var pulses = new long[] { 50, 60, -300, -100, 40, 50, 60, -200, -80, 30 };
        Assert.Equal(4, SignFlipCount(pulses));
        Assert.Equal(0, ConsecutiveAlternationCount(pulses));
    }

    [Fact]
    public void BifurcationConfig_NoTwoTurnOscillation_AnySettlement()
    {
        // THE PERMANENT REGRESSION (mandated): the exact config that
        // ping-ponged pre-stabilization — canonical dev autoplay at 3× base
        // rate (T2.7-measured: 30.6 %/decade gross in a two-turn attractor;
        // the bifurcation opened at ~2.2×). Per-settlement NET flows over 40
        // turns must not alternate: the old attractor flipped sign nearly
        // every turn (detector reads ~39); the stabilized system is allowed
        // occasional direction changes (regime shifts as stores evolve) but
        // no oscillation — ≤ 1 flip per 3 turns.
        SimConfig cfg = TestConfigs.Sim();
        cfg = cfg with
        {
            Migration = cfg.Migration with { BaseRatePerYear = cfg.Migration.BaseRatePerYear * 3.0 },
        };
        TurnExecutor exec = ProductionExecutor(cfg);
        WorldState world = WorldFounding.Found(TestConfigs.DevWorldgen(), cfg, Seed, null);

        // 200 turns (not the founding 40): populations grow into the
        // thousands, so a genuine attractor — which moves a FRACTION of the
        // population per turn (the T2.7 one moved ~30 %/decade gross) —
        // dwarfs the integer-dribble floor below instead of hiding under it.
        const int turns = 200;
        var net = new long[4][];
        var popSum = new long[4];
        for (int s = 0; s < 4; s++) net[s] = new long[turns];
        long grossTotal = 0;
        for (int t = 0; t < turns; t++)
        {
            world = exec.Step(world);
            for (int s = 0; s < 4; s++)
            {
                MigrationFlowRow row = world.MigrationFlows[s];
                net[s][t] = row.Inflow - row.Outflow;
                grossTotal += row.Outflow;
            }
            for (int i = 0; i < world.Buckets.Count; i++)
                for (int s = 0; s < 4; s++)
                    if (world.Buckets[i].Settlement == world.Settlements[s].Id)
                    { popSum[s] += world.Buckets[i].Count.Value; break; }
        }
        Assert.True(grossTotal > 0, "no migration at 3x base — regression rig vacuous");
        for (int s = 0; s < 4; s++)
        {
            // The two-turn-attractor signature is CONSECUTIVE alternation
            // (the old ping-pong scored ~38 per 40 turns here); isolated
            // reversals at the honest dynamics' Malthus pulses are
            // legitimate and don't count.
            // NOISE FLOOR (T2.7b re-anchor, adversarially re-verified): net
            // flows of a few PERSONS are integer-remainder dribble (D-004
            // flooring of sub-person desires), not the attractor. The floor
            // is POPULATION-RELATIVE — max(5, mean settlement pop / 200),
            // i.e. 0.5% of the settlement per turn — so it can never mask a
            // flow that matters at the settlement's own scale (the real
            // attractor moved ~30% of a settlement per turn at dt 10).
            // Zeros stay transparent to the alternation count, so a genuine
            // attractor with quiet turns still trips the detector.
            long meanPop = popSum[s] / turns;
            long floor = Math.Max(5, meanPop / 200);
            long zeroedMagnitude = 0;
            for (int t = 0; t < turns; t++)
                if (Math.Abs(net[s][t]) <= floor)
                {
                    zeroedMagnitude += Math.Abs(net[s][t]);
                    net[s][t] = 0;
                }
            // ANTI-VACUITY COMPANION (the floor must only ever discard
            // dribble): D-004 remainder flooring sheds O(1 person) per
            // settlement per turn regardless of population, so the AVERAGE
            // discarded magnitude must stay ≤ 2 people/turn (measured: 0.9).
            // A persistent attractor pinned at the floor amplitude would
            // discard ≥ floor (≥ 5) per active turn — this trips first.
            Assert.True(zeroedMagnitude <= 2 * turns,
                $"settlement {s}: noise floor discarded {zeroedMagnitude} people over {turns} turns " +
                $"(avg > 2/turn) — sub-floor flow is NOT dribble, raise the alarm not the floor");
            int alternations = ConsecutiveAlternationCount(net[s]);
            Assert.True(alternations <= turns / 10,
                $"settlement {s}: {alternations} consecutive net-flow alternations in {turns} turns — oscillation regressed");
        }
    }

    [Fact]
    public void CanonicalAutoplay_OccupancyConcentration_Bounded()
    {
        // The second mandated regression: through 400 canonical turns
        // (founding growth, the era-pacing transition, the decline) no
        // settlement may hold more than the TUNE concentration bound of the
        // world's population — the old attractor parked ~95% of a region's
        // people in whichever settlement was momentarily magnetic. Bound
        // (TUNE, test-side documented): 0.60 of world population in any one
        // of the dev world's four settlements (fair share 0.25); checked
        // whenever world population is above 100 (below that, integer
        // granularity makes shares meaningless).
        const double concentrationBound = 0.60;
        SimConfig cfg = TestConfigs.Sim();
        TurnExecutor exec = ProductionExecutor(cfg);
        WorldState world = WorldFounding.Found(TestConfigs.DevWorldgen(), cfg, Seed, null);

        for (int t = 1; t <= 400; t++)
        {
            world = exec.Step(world);
            long total = 0;
            var perSettlement = new long[world.Settlements.Count];
            for (int i = 0; i < world.Buckets.Count; i++)
            {
                long c = world.Buckets[i].Count.Value;
                total += c;
                for (int s = 0; s < world.Settlements.Count; s++)
                    if (world.Buckets[i].Settlement == world.Settlements[s].Id) { perSettlement[s] += c; break; }
            }
            if (total <= 100) continue;
            for (int s = 0; s < perSettlement.Length; s++)
            {
                double share = perSettlement[s] / (double)total;
                Assert.True(share <= concentrationBound,
                    $"turn {t}: settlement {s} holds {share:P0} of the world — concentration bound broken");
            }
        }
    }

    // --- the cap, pinned exactly --------------------------------------------

    [Fact]
    public void GapCap_PairGrossFlow_NeverExceedsFractionOfEqualizing()
    {
        // Hand world, huge instantaneous gap (empty-ish rich destination —
        // the old magnet scenario): one migration-only step may move at most
        // GapClosingFraction × m*, m* = (R_d×P_s − R_s×P_d)/(R_s + R_d), plus
        // per-bucket floor slack. The EMA starts converged on first sighting,
        // so this is the maximal-desire case, not a smoothed-away one.
        SimConfig cfg = TestConfigs.Sim();
        WorldState world = MigrationTestWorld.TwoSettlements(
            sourceCounts: 2000, destCounts: 10, destFood: 500_000);
        WorldState next = new TurnExecutor(FlatEra(10.0), [SystemCatalog.Migration(cfg)]).Step(world);

        long moved = next.MigrationFlows[1].Inflow;
        double rd = cfg.Migration.AttractivenessFoodWeight * 500_000;
        long ps = 2000 * 16, pd = 10 * 16;
        double equalizing = (rd * ps - 0.0 * pd) / (rd + 0.0);
        double cap = cfg.Migration.GapClosingFraction * equalizing;
        Assert.True(moved > 0, "no flow at a huge gap — cap rig vacuous");
        Assert.True(moved <= (long)cap + Cohorts.Count,
            $"pair moved {moved} > f × m* = {cap:F0} (+ floor slack) — the gap-closing cap is not binding");
    }

    // --- the filter, pinned exactly and dt-correct --------------------------

    [Fact]
    public void Smoothing_FilterUpdate_ExactRecurrence_AndDesireReadsSmoothed()
    {
        // One step on a hand world with a PRE-SEEDED smoothed row far below
        // the instantaneous value (the just-emptied-magnet scenario): the
        // stored filter value must equal prev + (instant − prev) × dt/τ
        // bit-exactly, and the flow must be sized by the SMOOTHED gap — i.e.
        // strictly less than the same world flows with the filter forced
        // converged (a fresh row initializes AT instant, the maximal case).
        SimConfig cfg = TestConfigs.Sim();
        const double dt = 10.0;

        // destFood sized so BOTH desires sit BELOW the gap-closing cap —
        // at the cap the two runs clip identically and the filter's effect
        // on desire is invisible (first version of this rig did exactly that).
        WorldState fresh = MigrationTestWorld.TwoSettlements(2000, 10, destFood: 12_000);
        WorldState seeded = MigrationTestWorld.TwoSettlements(2000, 10, destFood: 12_000);
        // The destination LOOKS freshly emptied: its smoothed history is low.
        seeded.SmoothedAttractiveness.Add(new SmoothedAttractivenessRow(new SettlementId(0), 0.0));
        seeded.SmoothedAttractiveness.Add(new SmoothedAttractivenessRow(new SettlementId(1), 1.0));

        var exec = new TurnExecutor(FlatEra(dt), [SystemCatalog.Migration(cfg)]);
        WorldState freshNext = exec.Step(fresh);
        WorldState seededNext = new TurnExecutor(FlatEra(dt), [SystemCatalog.Migration(cfg)]).Step(seeded);

        double instantDest = cfg.Migration.AttractivenessFoodWeight * 12_000 / (10.0 * 16);
        double alpha = Math.Min(1.0, dt / cfg.Migration.AttractivenessSmoothingWindowYears);
        Assert.Equal(1.0 + (instantDest - 1.0) * alpha, seededNext.SmoothedAttractiveness[1].Value);
        Assert.Equal(instantDest, freshNext.SmoothedAttractiveness[1].Value); // fresh row: converged init

        long freshMoved = freshNext.MigrationFlows[1].Inflow;
        long seededMoved = seededNext.MigrationFlows[1].Inflow;
        Assert.True(freshMoved > 0, "converged-filter flow zero — smoothing rig vacuous");
        Assert.True(seededMoved < freshMoved,
            $"smoothed desire {seededMoved} not below converged desire {freshMoved} — the driver is not reading the filter");
    }

    [Fact]
    public void Smoothing_DtHalving_FirstOrderConvergence()
    {
        // Law-3 pin for the filter integrator (dt/τ with the saturation
        // clamp): the same 40 sim-years of a CONSTANT instantaneous input at
        // dt 10 / 5 / 2.5 — successive refinements roughly halve the
        // terminal filter deviation. Migration-only on a two-settlement world
        // whose populations barely move (tiny counts, big distance) keeps the
        // input near-constant without rigging private state.
        SimConfig cfg = TestConfigs.Sim();
        var finals = new double[3];
        double[] dts = [10.0, 5.0, 2.5];
        for (int i = 0; i < dts.Length; i++)
        {
            WorldState world = MigrationTestWorld.TwoSettlements(
                sourceCounts: 5, destCounts: 5, destFood: 100_000, travelCost: 1000.0);
            world.SmoothedAttractiveness.Add(new SmoothedAttractivenessRow(new SettlementId(0), 0.0));
            world.SmoothedAttractiveness.Add(new SmoothedAttractivenessRow(new SettlementId(1), 0.0));
            var exec = new TurnExecutor(FlatEra(dts[i]), [SystemCatalog.Migration(cfg)]);
            world = exec.Run(world, (int)(40.0 / dts[i]));
            finals[i] = world.SmoothedAttractiveness[1].Value;
        }
        double l1Coarse = Math.Abs(finals[0] - finals[1]);
        double l1Fine = Math.Abs(finals[1] - finals[2]);
        Assert.True(l1Coarse > 0 && l1Fine > 0, "dt-halving produced no deviation — vacuous");
        double ratio = l1Coarse / l1Fine;
        Assert.True(ratio is >= 1.4 and <= 3.5,
            $"filter convergence ratio {ratio:F2} outside [1.4, 3.5] "
            + $"(S: dt10 {finals[0]:F4}, dt5 {finals[1]:F4}, dt2.5 {finals[2]:F4})");
    }
}

/// <summary>Shared hand-world builder for the stability tests: two settlements,
/// uniform cohort counts each, food endowment at the destination, symmetric
/// links. Deficit rows zeroed; distances explicit.</summary>
internal static class MigrationTestWorld
{
    public static WorldState TwoSettlements(
        long sourceCounts, long destCounts, long destFood, double travelCost = 20.0)
    {
        var world = new WorldState(7);
        var ledger = new Ledger(world.LedgerFlows);
        long[] perSettlement = [sourceCounts, destCounts];
        for (int s = 0; s < 2; s++)
        {
            var id = new SettlementId(s);
            world.Settlements.Add(new SettlementRow(id, SiteCell: s, FoundedTurn: 0));
            for (int c = 0; c < Cohorts.Count; c++)
            {
                int row = world.Buckets.Add(new BucketRow(
                    id, new CultureId(1), new ReligionId(1), new ClassId(1),
                    c, Conserved.Zero, 0.0, 0.0, 0.0, 0.0));
                if (perSettlement[s] > 0)
                {
                    ledger.Flow(ref world.Buckets.Ref(row).Count, ConservedQuantityIds.Population,
                        ReasonIds.InitialEndowment, perSettlement[s],
                        FlowDirection.Source, OverdrawPolicy.Throw);
                }
            }
            world.FoodStores.Add(new FoodStoreRow(id, Conserved.Zero, 0.0, 0.0));
            world.ConsumptionDeficits.Add(new ConsumptionDeficitRow(id, 0.0, 0));
        }
        if (destFood > 0)
        {
            new Ledger(world.LedgerFlows).Flow(ref world.FoodStores.Ref(1).Store,
                ConservedQuantityIds.Food, ReasonIds.InitialEndowment, destFood,
                FlowDirection.Source, OverdrawPolicy.Throw);
        }
        world.SettlementDistances.Add(new SettlementDistanceRow(new SettlementId(0), new SettlementId(1), travelCost));
        world.SettlementDistances.Add(new SettlementDistanceRow(new SettlementId(1), new SettlementId(0), travelCost));
        return world;
    }
}
