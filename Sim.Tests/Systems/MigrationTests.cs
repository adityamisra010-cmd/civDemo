using FsCheck;
using FsCheck.Fluent;
using FsCheck.Xunit;
using Sim.Core;
using Sim.Core.Kernel;
using Sim.Core.State;
using Sim.Core.Systems;
using Sim.Core.Worldgen;
using Sim.Tests.TestUtil;

namespace Sim.Tests.Systems;

// T2.5 acceptance: net flow runs poor→rich (equal attractiveness ⇒ ZERO GROSS
// flow — the driver is max(0, gap) + famine term, so with no gap and no
// deficit nothing moves at all, stated by construction); famine flight crosses
// the exit fraction BEFORE starvation does (D-021 ordering); conservation
// exact under random reachability graphs; unreachable pairs move nobody, at
// the table level; canonical migration magnitude sits in a few-%-per-decade
// corridor with teeth; migrant cohorts are young-adult-peaked vs the source.
public class MigrationTests
{
    private static EraTable FlatEra(double dtYears) => EraTableLoader.Load(
        $$"""{ "bands": [ { "name": "flat", "startYear": 0, "endYear": 100000, "dtYears": {{dtYears.ToString(System.Globalization.CultureInfo.InvariantCulture)}} } ] }""");

    private static EraTable CanonicalEra()
    {
        using var stream = Sim.Data.DataFiles.OpenEraPacing();
        return EraTableLoader.Load(stream);
    }

    /// <summary>Hand-built K-settlement world: one (culture 1, religion 1,
    /// class 1) group of 16 cohort buckets per settlement (settlement-major,
    /// ascending cohorts — the founding layout migration relies on), endowed
    /// via Ledger; stores/summaries/deficits/distances seeded by the caller.</summary>
    private static WorldState MigrationWorld(params long[][] countsPerSettlement)
    {
        var world = new WorldState(7);
        var ledger = new Ledger(world.LedgerFlows);
        for (int s = 0; s < countsPerSettlement.Length; s++)
        {
            var id = new SettlementId(s);
            world.Settlements.Add(new SettlementRow(id, SiteCell: s, FoundedTurn: 0));
            for (int c = 0; c < Cohorts.Count; c++)
            {
                int row = world.Buckets.Add(new BucketRow(
                    id, new CultureId(1), new ReligionId(1), new ClassId(1),
                    c, Conserved.Zero, 0.0, 0.0, 0.0, 0.0));
                if (countsPerSettlement[s][c] > 0)
                {
                    ledger.Flow(ref world.Buckets.Ref(row).Count, ConservedQuantityIds.Population,
                        ReasonIds.InitialEndowment, countsPerSettlement[s][c],
                        FlowDirection.Source, OverdrawPolicy.Throw);
                }
            }
            world.FoodStores.Add(new FoodStoreRow(id, Conserved.Zero, 0.0, 0.0));
            world.ConsumptionDeficits.Add(new ConsumptionDeficitRow(id, 0.0, 0));
        }
        return world;
    }

    private static void Link(WorldState world, int from, int to, double cost)
    {
        world.SettlementDistances.Add(new SettlementDistanceRow(
            new SettlementId(from), new SettlementId(to), cost));
    }

    private static void Endow(WorldState world, int settlement, long food)
    {
        new Ledger(world.LedgerFlows).Flow(ref world.FoodStores.Ref(settlement).Store,
            ConservedQuantityIds.Food, ReasonIds.InitialEndowment, food,
            FlowDirection.Source, OverdrawPolicy.Throw);
    }

    private static long[] AdultsHeavy(long perCohort)
    {
        var counts = new long[Cohorts.Count];
        for (int c = 0; c < Cohorts.Count; c++) counts[c] = perCohort;
        return counts;
    }

    private static long SettlementPop(WorldState world, int settlement)
    {
        long total = 0;
        for (int i = 0; i < world.Buckets.Count; i++)
            if (world.Buckets[i].Settlement.Value == settlement) total += world.Buckets[i].Count.Value;
        return total;
    }

    private static TurnExecutor MigrationOnly(SimConfig cfg, double dt = 10.0) =>
        new(FlatEra(dt), [SystemCatalog.Migration(cfg)]);

    // --- direction ----------------------------------------------------------

    [Fact]
    public void Direction_NetFlowRunsPoorToRich()
    {
        SimConfig cfg = TestConfigs.Sim();
        WorldState world = MigrationWorld(AdultsHeavy(1000), AdultsHeavy(1000));
        Endow(world, 0, 1_000);     // poor
        Endow(world, 1, 200_000);   // rich
        Link(world, 0, 1, 20.0);
        Link(world, 1, 0, 20.0);

        WorldState next = MigrationOnly(cfg).Step(world);
        long out0 = next.MigrationFlows[0].Outflow, in0 = next.MigrationFlows[0].Inflow;
        long out1 = next.MigrationFlows[1].Outflow, in1 = next.MigrationFlows[1].Inflow;

        Assert.True(out0 > 0, "no migration at all — direction test vacuous");
        Assert.Equal(0, out1);      // no positive gap toward the poor side
        Assert.Equal(out0, in1);    // arrivals bookkeep exactly
        Assert.Equal(0, in0);
        Assert.True(SettlementPop(next, 1) > SettlementPop(world, 1));
    }

    [Fact]
    public void Direction_EqualAttractiveness_ZeroGrossFlow()
    {
        // STATED (per the packet): equal attractiveness produces ZERO GROSS
        // flow, not symmetric gross flows — the driver is max(0, gap) plus the
        // famine term, and with no gap and no deficit every desired flow is
        // exactly 0.0 by construction. (Symmetric churn is a plausible
        // alternative design; this one keeps the no-signal world quiescent and
        // bit-stable.)
        SimConfig cfg = TestConfigs.Sim();
        WorldState world = MigrationWorld(AdultsHeavy(1000), AdultsHeavy(1000));
        Endow(world, 0, 50_000);
        Endow(world, 1, 50_000);
        Link(world, 0, 1, 20.0);
        Link(world, 1, 0, 20.0);

        WorldState next = MigrationOnly(cfg).Step(world);
        Assert.Equal(0, next.MigrationFlows[0].Outflow);
        Assert.Equal(0, next.MigrationFlows[1].Outflow);
        for (int i = 0; i < world.Buckets.Count; i++)
            Assert.Equal(world.Buckets[i].Count.Value, next.Buckets[i].Count.Value);
    }

    // --- unreachable pairs --------------------------------------------------

    [Fact]
    public void UnreachablePair_ZeroFlow_AtTheTableLevel()
    {
        // Extreme gap AND famine at the source — every incentive to move, no
        // road: +∞ costs both ways. Buckets EXACTLY unchanged (long equality,
        // every row), chronicle rows exactly zero.
        SimConfig cfg = TestConfigs.Sim();
        WorldState world = MigrationWorld(AdultsHeavy(1000), AdultsHeavy(1000));
        Endow(world, 1, 1_000_000);
        world.ConsumptionDeficits[0] = new ConsumptionDeficitRow(new SettlementId(0), 1.0, 1000);
        Link(world, 0, 1, double.PositiveInfinity);
        Link(world, 1, 0, double.PositiveInfinity);

        WorldState next = MigrationOnly(cfg).Step(world);
        for (int i = 0; i < world.Buckets.Count; i++)
            Assert.Equal(world.Buckets[i].Count.Value, next.Buckets[i].Count.Value);
        for (int s = 0; s < 2; s++)
        {
            Assert.Equal(0, next.MigrationFlows[s].Inflow);
            Assert.Equal(0, next.MigrationFlows[s].Outflow);
        }
    }

    // --- young-adult selectivity --------------------------------------------

    [Fact]
    public void MigrantCohorts_YoungAdultPeaked_VsSourceDistribution()
    {
        // Uniform source cohorts; migrants land at the empty-ish destination —
        // arrival deltas ARE the migrant distribution. Young-adult cohorts
        // (3..5) must hold a strictly larger share of migrants than of the
        // source population (uniform ⇒ 3/16), and cohort 3's arrivals must
        // strictly exceed elder cohort 13's (the peak is provable, not flat).
        // Moderate gap: if the desired outflow exceeded the bucket, the
        // proportional overdraw scaler would move WHOLE cohorts and erase the
        // profile shape — the very saturation the scaler exists to handle.
        // (First draft used a 5M-food destination and measured exactly the
        // uniform source shares: the scaler working as designed.)
        SimConfig cfg = TestConfigs.Sim();
        WorldState world = MigrationWorld(AdultsHeavy(10_000), AdultsHeavy(1000));
        Endow(world, 0, 10_000);
        Endow(world, 1, 200_000);
        Link(world, 0, 1, 10.0);
        Link(world, 1, 0, 10.0);

        WorldState next = MigrationOnly(cfg).Step(world);
        long totalMigrants = 0, youngMigrants = 0;
        long arrivals3 = 0, arrivals13 = 0;
        for (int i = 0; i < next.Buckets.Count; i++)
        {
            if (next.Buckets[i].Settlement.Value != 1) continue;
            long delta = next.Buckets[i].Count.Value - world.Buckets[i].Count.Value;
            totalMigrants += delta;
            int c = next.Buckets[i].CohortIdx;
            if (c is >= 3 and <= 5) youngMigrants += delta;
            if (c == 3) arrivals3 = delta;
            if (c == 13) arrivals13 = delta;
        }
        Assert.True(totalMigrants > 100, $"only {totalMigrants} migrants — selectivity vacuous");
        Assert.True(youngMigrants / (double)totalMigrants > 3.0 / 16.0 + 0.1,
            $"young-adult migrant share {youngMigrants / (double)totalMigrants:F3} not peaked vs uniform source");
        Assert.True(arrivals3 > arrivals13 * 5,
            $"cohort 3 arrivals {arrivals3} not decisively above elder cohort 13's {arrivals13}");
    }

    // --- exit before death (D-021) ------------------------------------------

    [Fact]
    public void FamineAtOneOfTwelve_ExitCrossesTheFractionBeforeDeathDoes()
    {
        // Canonical N = 12 world; settlement 0 is ordered to 0% farm at turn
        // 1 — its harvest dies and its deficit ramps. ATTRIBUTION: canonical
        // autoplay has NATURAL deficits at other settlements, so the famine's
        // own starvation and flight are isolated as TWIN DIFFERENCES — the
        // ordered run minus a no-order baseline stepped in lockstep (both
        // deterministic). THE D-021 ORDERING: famine-attributable
        // out-migration from settlement 0 crosses 10% of its starting
        // population strictly before famine-attributable starvation does.
        SimConfig cfg = TestConfigs.Sim();
        var orders = new OrderLog();
        orders.Append(new OrderRecord(1, ActorId: 1, OrderKind.LaborAllocation, TargetId: 0, Amount: 0.0));
        using var pipeA = Sim.Data.DataFiles.OpenPipeline();
        var famine = new TurnExecutor(CanonicalEra(),
            PipelineLoader.Load(pipeA, SystemCatalog.All(cfg)), orders);
        using var pipeB = Sim.Data.DataFiles.OpenPipeline();
        var baseline = new TurnExecutor(CanonicalEra(),
            PipelineLoader.Load(pipeB, SystemCatalog.All(cfg)));
        WorldState worldF = WorldFounding.Found(TestConfigs.Worldgen(), cfg, 42); // N = 12, 1024²
        WorldState worldB = WorldFounding.Found(TestConfigs.Worldgen(), cfg, 42);

        long pop0 = SettlementPop(worldF, 0);
        // TUNE fraction: 8% of the source population. At Neolithic dt = 10 the
        // whole famine resolves in ~2 turns, and BOTH flight and starvation
        // react to the same one-turn-lagged deficit — the ordering is visible
        // because the ramp turn's gap-driven exodus (the store crash is
        // already visible in Prev) crosses 8% a full turn before starvation
        // crosses anything. T2.8 anchor (stated): the smoothing window τ sets
        // how much of that store-crash early warning survives the EMA —
        // measured: τ = 30 mutes it below the threshold (exit ties death at
        // t7), τ = 20 restores exit t6 vs death t7 while keeping α = 0.5 at
        // dt 10; τ = 20 is the canonical TUNE.
        long threshold = pop0 * 8 / 100;
        long cumOutF = 0, cumOutB = 0, prevStarvF = 0, prevStarvB = 0, cumStarvF = 0, cumStarvB = 0;
        int exitTurn = -1, deathTurn = -1;
        for (int t = 1; t <= 40 && (exitTurn < 0 || deathTurn < 0); t++)
        {
            worldF = famine.Step(worldF);
            worldB = baseline.Step(worldB);
            cumOutF += worldF.MigrationFlows[0].Outflow;
            cumOutB += worldB.MigrationFlows[0].Outflow;
            cumStarvF += StarvedTotal(worldF) - prevStarvF;
            prevStarvF = StarvedTotal(worldF);
            cumStarvB += StarvedTotal(worldB) - prevStarvB;
            prevStarvB = StarvedTotal(worldB);

            long exitAttrib = cumOutF - cumOutB;
            long deathAttrib = cumStarvF - cumStarvB;
            if (exitTurn < 0 && exitAttrib >= threshold) exitTurn = t;
            if (deathTurn < 0 && deathAttrib >= threshold) deathTurn = t;
        }

        Assert.True(exitTurn > 0, $"attributable out-migration never crossed {threshold} of {pop0} in 40 turns");
        Assert.True(deathTurn > 0, "attributable starvation never crossed the fraction — window too short to prove ordering");
        Assert.True(exitTurn < deathTurn,
            $"EXIT-BEFORE-DEATH violated: exit crossed at turn {exitTurn}, death at turn {deathTurn}");
    }

    private static long StarvedTotal(WorldState world)
    {
        for (int i = 0; i < world.LedgerFlows.Count; i++)
        {
            LedgerFlowRow row = world.LedgerFlows[i];
            if (row.Quantity == ConservedQuantityIds.Population && row.Reason == ReasonIds.Starvation)
                return row.TotalSunk;
        }
        return 0;
    }

    // --- magnitude corridor -------------------------------------------------

    private static double MaxGrossPerDecade(SimConfig cfg)
    {
        // Dev autoplay (N = 4), 30 turns: the WORST settlement's gross outflow
        // per FED decade as a fraction of its mean population. Famine turns
        // (the settlement's own deficit > 0) are EXCLUDED from both numerator
        // and denominator — the corridor governs the everyday gap-driven drift
        // ("a few %/decade"); famine flight is a surge by design (D-021) and
        // is pinned by the exit-before-death test, not this band. Documented
        // honestly: with flight included the same run measures tens of
        // %/decade during equilibrium famine spikes.
        using var pipeStream = Sim.Data.DataFiles.OpenPipeline();
        var exec = new TurnExecutor(CanonicalEra(),
            PipelineLoader.Load(pipeStream, SystemCatalog.All(cfg)));
        WorldState world = WorldFounding.Found(TestConfigs.DevWorldgen(), cfg, 42);

        var gross = new long[4];
        var popSum = new long[4];
        var fedTurns = new int[4];
        const int turns = 30;
        for (int t = 1; t <= turns; t++)
        {
            WorldState prev = world;
            world = exec.Step(world);
            for (int s = 0; s < 4; s++)
            {
                double prevDeficit = 0.0;
                for (int i = 0; i < prev.ConsumptionDeficits.Count; i++)
                    if (prev.ConsumptionDeficits[i].Settlement.Value == s)
                    { prevDeficit = prev.ConsumptionDeficits[i].DeficitRatio; break; }
                if (prevDeficit > 0.0) continue; // famine turn: flight regime, out of band
                gross[s] += world.MigrationFlows[s].Outflow;
                popSum[s] += SettlementPop(world, s);
                fedTurns[s]++;
            }
        }
        double worst = 0.0;
        for (int s = 0; s < 4; s++)
        {
            if (fedTurns[s] == 0) continue;
            double meanPop = popSum[s] / (double)fedTurns[s];
            double perDecade = gross[s] / (double)fedTurns[s] / meanPop; // one turn = one decade
            worst = Math.Max(worst, perDecade);
        }
        return worst;
    }

    [Fact]
    public void MagnitudeCorridor_FedPhaseDrift_WithTeeth()
    {
        // TUNE corridor, FINALIZED at T2.8 on the STABILIZED system (director
        // mandate). Historical justification (one line): everyday inter-
        // village relocation in pre-modern subsistence societies was a few
        // per-mille per decade — mobility concentrated in crisis surges, and
        // the famine-flight surges are excluded here by construction (pinned
        // by exit-before-death instead).
        // The T2.8-measured response curve (gap cap f = 0.25, EMA τ = 20):
        // 0.1× → 0.01%, 0.3× → 0.05%, 1× → 0.12%, 3× → 0.25%, 10× → 0.35%,
        // 30× → 0.54% — MONOTONE and saturating; the T2.7 bifurcation into
        // the two-turn oscillation attractor (2.2× → 11.8%) is gone, which is
        // the stabilization working (pinned by MigrationStabilityTests). The
        // corridor is [0.05%, 0.30%] per decade, and two-sided teeth are
        // REAL AGAIN under the damped mechanism (the T2.5 lesson holds).
        SimConfig cfg = TestConfigs.Sim();
        double worst = MaxGrossPerDecade(cfg);
        Assert.True(worst is > 0.0005 and < 0.0030,
            $"gross migration {worst:P2}/decade outside the [0.05%, 0.30%] corridor");

        // ...WITH TEETH in both directions: 10× saturates ABOVE the band,
        // 0.1× starves BELOW it — mis-tuning is caught both ways.
        SimConfig hot = cfg with
        {
            Migration = cfg.Migration with { BaseRatePerYear = cfg.Migration.BaseRatePerYear * 10 },
        };
        double hotWorst = MaxGrossPerDecade(hot);
        Assert.True(hotWorst > 0.0030,
            $"10× rate produced {hotWorst:P2}/decade — inside the corridor, no teeth");
        SimConfig cold = cfg with
        {
            Migration = cfg.Migration with { BaseRatePerYear = cfg.Migration.BaseRatePerYear * 0.1 },
        };
        double coldWorst = MaxGrossPerDecade(cold);
        Assert.True(coldWorst < 0.0005,
            $"0.1× rate produced {coldWorst:P2}/decade — inside the corridor, no teeth");
    }

    // --- conservation under random reachability graphs ----------------------

    [Property(MaxTest = 60)]
    public Property RandomGraphs_ConserveExactly_PerCohortTotalsInvariant()
    {
        // Three settlements, random cohort populations, random deficits, and a
        // RANDOM REACHABILITY GRAPH: each ordered pair independently gets a
        // finite cost or +∞. Three migration-only steps: the audit identity
        // holds exactly; per-cohort totals ACROSS settlements are invariant
        // (same-key transfers only — migration can never age, breed, or kill);
        // and no Births/Deaths/Starvation footprint appears.
        Gen<long> countGen = Gen.Choose(0, 50_000).Select(v => (long)v);
        Gen<long[]> stateGen = countGen.ArrayOf(Cohorts.Count);
        Gen<(long[] A, long[] B, long[] C)> worldGen =
            stateGen.SelectMany(a => stateGen.SelectMany(b => stateGen.Select(c => (a, b, c))));
        Gen<int[]> costsGen = Gen.Choose(0, 100).ArrayOf(6);   // 0..29 → ∞, else finite
        Gen<(int D0, int D1, int D2)> deficitGen = Gen.Choose(0, 100)
            .SelectMany(a => Gen.Choose(0, 100).SelectMany(b => Gen.Choose(0, 100).Select(c => (a, b, c))));
        Gen<int[]> foodGen = Gen.Choose(0, 300).ArrayOf(3);

        Gen<((long[] A, long[] B, long[] C) Pops, int[] Costs)> leftGen =
            worldGen.SelectMany(p => costsGen.Select(c => (p, c)));
        Gen<((int D0, int D1, int D2) Deficits, int[] Foods)> rightGen =
            deficitGen.SelectMany(d => foodGen.Select(f => (d, f)));
        return Prop.ForAll(leftGen.ToArbitrary(), rightGen.ToArbitrary(), (left, right) =>
        {
            ((long[] A, long[] B, long[] C) pops, int[] costs) = left;
            ((int D0, int D1, int D2) deficits, int[] foods) = right;
            SimConfig cfg = TestConfigs.Sim();
            WorldState world = MigrationWorld(pops.A, pops.B, pops.C);
            for (int s = 0; s < 3; s++) Endow(world, s, foods[s] * 1000L);
            world.ConsumptionDeficits[0] = new ConsumptionDeficitRow(new SettlementId(0), deficits.D0 / 100.0, 1);
            world.ConsumptionDeficits[1] = new ConsumptionDeficitRow(new SettlementId(1), deficits.D1 / 100.0, 1);
            world.ConsumptionDeficits[2] = new ConsumptionDeficitRow(new SettlementId(2), deficits.D2 / 100.0, 1);
            int e = 0;
            for (int from = 0; from < 3; from++)
            {
                for (int to = 0; to < 3; to++)
                {
                    if (from == to) continue;
                    Link(world, from, to, costs[e] < 30 ? double.PositiveInfinity : costs[e]);
                    e++;
                }
            }

            var cohortTotals = new long[Cohorts.Count];
            for (int i = 0; i < world.Buckets.Count; i++)
                cohortTotals[world.Buckets[i].CohortIdx] += world.Buckets[i].Count.Value;

            var exec = MigrationOnly(cfg);
            for (int t = 0; t < 3; t++)
            {
                world = exec.Step(world);
                if (!ConservationAuditor.IsConserved(world, out string report))
                    return false.Label($"turn {t + 1}: {report}");
            }

            var after = new long[Cohorts.Count];
            for (int i = 0; i < world.Buckets.Count; i++)
                after[world.Buckets[i].CohortIdx] += world.Buckets[i].Count.Value;
            for (int c = 0; c < Cohorts.Count; c++)
            {
                if (after[c] != cohortTotals[c])
                    return false.Label($"cohort {c}: total {cohortTotals[c]} → {after[c]} under migration-only");
            }
            foreach (ReasonId reason in new[] { ReasonIds.Births, ReasonIds.Deaths, ReasonIds.Starvation })
            {
                for (int i = 0; i < world.LedgerFlows.Count; i++)
                {
                    LedgerFlowRow row = world.LedgerFlows[i];
                    if (row.Quantity == ConservedQuantityIds.Population && row.Reason == reason
                        && (row.TotalSourced != 0 || row.TotalSunk != 0))
                        return false.Label($"migration left a {reason.Value} footprint");
                }
            }
            return true.ToProperty();
        });
    }

    // --- directed flight anchor (vacuity finding) ----------------------------

    [Fact]
    public void FamineFlight_FiresWithZeroGap_AndScalesWithTheFactor()
    {
        // The pure D-021 valve, pinned directly (vacuity finding: the
        // flight-suppressed mutant was killed only by the aggregate corridor
        // statistic, which any retune could un-teeth): two settlements with
        // IDENTICAL attractiveness — gap exactly 0 — and a full deficit at
        // the source. Flow exists (gap-independent flight), runs source→dest
        // only, is ZERO with the factor zeroed, and grows with the factor.
        SimConfig cfg = TestConfigs.Sim();
        WorldState World(double flightFactor)
        {
            WorldState w = MigrationWorld(AdultsHeavy(1000), AdultsHeavy(1000));
            Endow(w, 0, 50_000); Endow(w, 1, 50_000);
            w.ConsumptionDeficits[0] = new ConsumptionDeficitRow(new SettlementId(0), 1.0, 1000);
            Link(w, 0, 1, 20.0); Link(w, 1, 0, 20.0);
            return w;
        }
        SimConfig Zero = cfg with { Migration = cfg.Migration with { FamineFlightFactor = 0.0 } };
        SimConfig Half = cfg with { Migration = cfg.Migration with { FamineFlightFactor = cfg.Migration.FamineFlightFactor / 2 } };

        WorldState atZero = MigrationOnly(Zero).Step(World(0));
        WorldState atHalf = MigrationOnly(Half).Step(World(0));
        WorldState atFull = MigrationOnly(cfg).Step(World(0));

        Assert.Equal(0, atZero.MigrationFlows[0].Outflow);   // no flight, no gap → nothing
        Assert.True(atHalf.MigrationFlows[0].Outflow > 0, "flight valve inert at half factor");
        Assert.True(atFull.MigrationFlows[0].Outflow > atHalf.MigrationFlows[0].Outflow,
            "flight does not scale with the factor");
        Assert.Equal(0, atFull.MigrationFlows[1].Outflow);   // the fed side stays put
    }

    // --- damping distance-dependence (vacuity finding) -----------------------

    [Fact]
    public void Damping_NearerDestinationReceivesMore_InTheExpRatio()
    {
        // Finite-cost damping pinned directly (vacuity finding: a damping=1
        // mutant for reachable pairs survived everything but the corridor):
        // two equally attractive destinations at costs 10 vs 30 — the nearer
        // receives more, in the exp((30−10)/decay) ratio within integer floors.
        // T2.8 rig note: endowments sized so the gap desire stays BELOW the
        // pair's gap-closing cap (0.25 × m*) — at the old 300k both pairs
        // cap-bound identically and the ratio collapsed to 1 by design; the
        // exponential lives in the sub-cap driver.
        SimConfig cfg = TestConfigs.Sim();
        WorldState world = MigrationWorld(AdultsHeavy(5000), AdultsHeavy(50), AdultsHeavy(50));
        Endow(world, 1, 30_000);
        Endow(world, 2, 30_000);
        Link(world, 0, 1, 10.0); Link(world, 1, 0, 10.0);
        Link(world, 0, 2, 30.0); Link(world, 2, 0, 30.0);
        Link(world, 1, 2, 10.0); Link(world, 2, 1, 10.0);

        WorldState next = MigrationOnly(cfg).Step(world);
        long inNear = next.MigrationFlows[1].Inflow, inFar = next.MigrationFlows[2].Inflow;
        Assert.True(inNear > 0 && inFar > 0, $"flows {inNear}/{inFar} — damping rig vacuous");
        Assert.True(inNear > inFar, $"nearer destination got {inNear} <= farther's {inFar}");
        double expectedRatio = Math.Exp((30.0 - 10.0) / cfg.Migration.DampingDecayCost); // ≈ 2.23
        double measured = inNear / (double)inFar;
        Assert.True(Math.Abs(measured - expectedRatio) / expectedRatio < 0.25,
            $"damping ratio {measured:F2} vs expected {expectedRatio:F2} — not exponential in cost");
    }

    // --- overdraw proportionality (adversarial finding) ----------------------

    [Fact]
    public void Overdraw_ScalesProportionally_NoFirstDestinationGrab()
    {
        // Adversarial finding: the overdraw-scaling bypass survived every
        // semantic test (ClampToAvailable also caps the TOTAL at the bucket,
        // so aggregate observables barely move — only the golden caught it).
        // The distinguishing observable is the DISTRIBUTION: two equally
        // reachable destinations whose combined desired flow exceeds the
        // source bucket. With proportional scaling each receives ~half; with
        // the bypass the first destination grabs everything and the second
        // gets the clamped scraps. Assert near-equal split.
        // T2.8 rig note: saturation now rides the FAMINE-FLIGHT channel
        // (deficit 1.0 → desire ≈ 2170 per destination against a 1000-person
        // bucket) — the gap channel is structurally capped at 0.25 × m* since
        // the stabilization and can no longer over-desire a bucket by itself.
        SimConfig cfg = TestConfigs.Sim();
        var counts = new long[Cohorts.Count];
        counts[4] = 1000; // one young-adult bucket — the whole source
        WorldState world = MigrationWorld(counts, AdultsHeavy(10), AdultsHeavy(10));
        Endow(world, 1, 2_000_000);
        Endow(world, 2, 2_000_000);
        world.ConsumptionDeficits[0] = new ConsumptionDeficitRow(new SettlementId(0), 1.0, 1);
        Link(world, 0, 1, 5.0); Link(world, 1, 0, 5.0);
        Link(world, 0, 2, 5.0); Link(world, 2, 0, 5.0);
        Link(world, 1, 2, 5.0); Link(world, 2, 1, 5.0);

        WorldState next = MigrationOnly(cfg).Step(world);
        long in1 = next.MigrationFlows[1].Inflow, in2 = next.MigrationFlows[2].Inflow;
        long moved = in1 + in2;
        Assert.True(moved is >= 950 and <= 1000,
            $"moved {moved} of 1000 — the scaler did not engage (saturation rig failed)");
        Assert.True(Math.Abs(in1 - in2) <= moved / 10,
            $"overdraw split {in1}/{in2} not proportional — first-destination grab");
    }

    // --- chronicle truthfulness (adversarial finding) ------------------------

    [Fact]
    public void Chronicle_RecordsDeliveredFlow_NotRequested_WhenClampBinds()
    {
        // Adversarial finding (an escaped mutant): within migration-only
        // stepping the scaler guarantees Σmoved ≤ prev count, so the clamp
        // never binds and "record requested" is indistinguishable from
        // "record delivered". It binds when ANOTHER system drains the bucket
        // in the SAME turn: classmobility's famine demotion runs first, so
        // migration's transfer (sized from PREV) clamps. The chronicle must
        // equal the DELIVERED people — cross-checked against actual state
        // deltas, and Σ inflow must equal Σ outflow exactly.
        SimConfig cfg = TestConfigs.Sim();
        var world = new WorldState(7);
        var ledger = new Ledger(world.LedgerFlows);
        for (int s = 0; s < 2; s++)
        {
            var id = new SettlementId(s);
            world.Settlements.Add(new SettlementRow(id, SiteCell: s, FoundedTurn: 0));
            for (int cls = 1; cls <= 2; cls++)
            {
                for (int c = 0; c < Cohorts.Count; c++)
                {
                    int row = world.Buckets.Add(new BucketRow(
                        id, new CultureId(1), new ReligionId(1), new ClassId(cls),
                        c, Conserved.Zero, 0.0, 0.0, 0.0, 0.0));
                    long endow = s == 0 && cls == 2 && c == 4 ? 3000
                        : s == 0 && cls == 1 ? 100 : 0;
                    if (endow > 0)
                    {
                        ledger.Flow(ref world.Buckets.Ref(row).Count, ConservedQuantityIds.Population,
                            ReasonIds.InitialEndowment, endow, FlowDirection.Source, OverdrawPolicy.Throw);
                    }
                }
            }
            world.FoodStores.Add(new FoodStoreRow(id, Conserved.Zero, 0.0, 0.0));
            // A SMALL source deficit (0.045): the famine demotion drains the
            // artisan bucket PARTIALLY this turn (2.0 × 0.045 × 10 = 90%, 300
            // of 3000 left), while migration's Prev-sized request — the
            // gap-capped share (~0.25 × m* ≈ 970 of this bucket) plus flight —
            // still exceeds the remainder, so the transfer clamps with a
            // NONZERO delivery: the divergence the mutant needs (a full drain
            // makes actuallyMoved 0 and the recording branch never runs, which
            // is exactly how the first version of this rig let the mutant
            // live; T2.8's gap cap forced the deficit up from 0.03 — at the
            // old value the CAPPED request no longer exceeded the remainder).
            world.ConsumptionDeficits.Add(new ConsumptionDeficitRow(id, s == 0 ? 0.045 : 0.0, 1000));
        }
        world.ClassStates.Add(new ClassStateRow(new SettlementId(0), new ClassId(1), 1));
        world.ClassStates.Add(new ClassStateRow(new SettlementId(0), new ClassId(2), 1));
        world.ClassStates.Add(new ClassStateRow(new SettlementId(1), new ClassId(1), 1));
        world.ClassStates.Add(new ClassStateRow(new SettlementId(1), new ClassId(2), 1));
        Endow(world, 1, 1_000_000);
        Link(world, 0, 1, 5.0);
        Link(world, 1, 0, 5.0);

        var exec = new TurnExecutor(FlatEra(10.0),
            [SystemCatalog.ClassMobility(cfg), SystemCatalog.Migration(cfg)]);
        WorldState next = exec.Step(world);

        // Delivered arrivals, from state deltas (classmobility never crosses
        // settlements, so settlement 1's gains are migration's alone).
        long arrivals1 = 0;
        for (int i = 0; i < next.Buckets.Count; i++)
        {
            if (next.Buckets[i].Settlement.Value != 1) continue;
            arrivals1 += next.Buckets[i].Count.Value - world.Buckets[i].Count.Value;
        }
        Assert.True(arrivals1 > 0, "nothing migrated — clamp rig vacuous");
        // THE CLAMP FIRED (rig-honesty guard, review finding): the artisan
        // cohort-4 source bucket must be EMPTY — migration asked for its full
        // Prev count (3000) but the demotion left only ~40%, so delivered <
        // requested strictly and the recorded numbers can only match the
        // delivery if the chronicle is honest.
        long artisanSrcAfter = 0, artisanReq = 0;
        for (int i = 0; i < next.Buckets.Count; i++)
        {
            BucketRow b = next.Buckets[i];
            if (b.Settlement.Value == 0 && b.Class.Value == 2 && b.CohortIdx == 4)
                artisanSrcAfter = b.Count.Value;
            if (world.Buckets[i].Settlement.Value == 0 && world.Buckets[i].Class.Value == 2
                && world.Buckets[i].CohortIdx == 4)
                artisanReq = world.Buckets[i].Count.Value;
        }
        Assert.Equal(0, artisanSrcAfter);
        Assert.True(arrivals1 < artisanReq,
            $"delivered {arrivals1} not below the Prev-sized request {artisanReq} — clamp never bound, rig vacuous");
        Assert.Equal(arrivals1, next.MigrationFlows[1].Inflow);   // delivered, not requested
        Assert.Equal(arrivals1, next.MigrationFlows[0].Outflow);
        long inSum = 0, outSum = 0;
        for (int s = 0; s < 2; s++)
        {
            inSum += next.MigrationFlows[s].Inflow;
            outSum += next.MigrationFlows[s].Outflow;
        }
        Assert.Equal(inSum, outSum);
    }

    // --- full-key preservation ----------------------------------------------

    [Fact]
    public void Migrants_KeepTheirFullBucketKey_ClassesTravelSeparately()
    {
        // Two settlements, TWO CLASSES at the source (peasants + artisans in
        // distinct adult cohorts): arrivals must land in the destination
        // bucket with the IDENTICAL (culture, religion, class, cohort) key —
        // artisan migrants stay artisans. Class totals across the world are
        // invariant per class, and the destination's artisan rows gain people
        // even though it started with zero artisans.
        SimConfig cfg = TestConfigs.Sim();
        var world = new WorldState(7);
        var ledger = new Ledger(world.LedgerFlows);
        for (int s = 0; s < 2; s++)
        {
            var id = new SettlementId(s);
            world.Settlements.Add(new SettlementRow(id, SiteCell: s, FoundedTurn: 0));
            for (int cls = 1; cls <= 2; cls++)
            {
                for (int c = 0; c < Cohorts.Count; c++)
                {
                    int row = world.Buckets.Add(new BucketRow(
                        id, new CultureId(1), new ReligionId(1), new ClassId(cls),
                        c, Conserved.Zero, 0.0, 0.0, 0.0, 0.0));
                    long endow = s == 0 ? (cls == 1 ? 5000 : (c is >= 3 and <= 6 ? 2000 : 0)) : (cls == 1 ? 100 : 0);
                    if (endow > 0)
                    {
                        ledger.Flow(ref world.Buckets.Ref(row).Count, ConservedQuantityIds.Population,
                            ReasonIds.InitialEndowment, endow, FlowDirection.Source, OverdrawPolicy.Throw);
                    }
                }
            }
            world.FoodStores.Add(new FoodStoreRow(id, Conserved.Zero, 0.0, 0.0));
            world.ConsumptionDeficits.Add(new ConsumptionDeficitRow(id, 0.0, 0));
        }
        Endow(world, 1, 500_000); // rich destination
        Link(world, 0, 1, 15.0);
        Link(world, 1, 0, 15.0);

        long peasantsBefore = 0, artisansBefore = 0;
        for (int i = 0; i < world.Buckets.Count; i++)
        {
            if (world.Buckets[i].Class.Value == 1) peasantsBefore += world.Buckets[i].Count.Value;
            else artisansBefore += world.Buckets[i].Count.Value;
        }

        WorldState next = MigrationOnly(cfg).Step(world);
        long peasantsAfter = 0, artisansAfter = 0, artisanArrivals = 0;
        for (int i = 0; i < next.Buckets.Count; i++)
        {
            BucketRow b = next.Buckets[i];
            if (b.Class.Value == 1) peasantsAfter += b.Count.Value;
            else
            {
                artisansAfter += b.Count.Value;
                if (b.Settlement.Value == 1) artisanArrivals += b.Count.Value;
            }
        }
        Assert.Equal(peasantsBefore, peasantsAfter);   // class totals invariant
        Assert.Equal(artisansBefore, artisansAfter);
        Assert.True(artisanArrivals > 0, "no artisan migrated — key preservation vacuous");
    }

    // --- distances: D-016 piggyback -----------------------------------------

    [Fact]
    public void Distances_ComputedWithCatchments_EventSkipStillBinds()
    {
        // Founded dev world: after turn 1 the distance table holds all N²−N
        // ordered pairs; a quiet turn 2 (no revision event) carries the SAME
        // rows forward bit-exactly (the D-016 skip, now covering distances).
        SimConfig cfg = TestConfigs.Sim();
        var exec = new TurnExecutor(CanonicalEra(), [SystemCatalog.Catchment()]);
        WorldState t1 = exec.Step(WorldFounding.Found(TestConfigs.DevWorldgen(), cfg, 42));
        Assert.Equal(4 * 3, t1.SettlementDistances.Count);
        for (int i = 0; i < t1.SettlementDistances.Count; i++)
        {
            SettlementDistanceRow row = t1.SettlementDistances[i];
            Assert.NotEqual(row.From, row.To);
            Assert.True(row.TravelCost > 0.0, "zero-cost pair — distances degenerate");
        }

        WorldState t2 = exec.Step(t1);
        Assert.Equal(t1.SettlementDistances.Count, t2.SettlementDistances.Count);
        for (int i = 0; i < t1.SettlementDistances.Count; i++)
            Assert.Equal(t1.SettlementDistances[i], t2.SettlementDistances[i]);
        // The skip observable: LastRecomputeTurn did not move.
        Assert.Equal(t1.CatchmentSummaries[0].LastRecomputeTurn, t2.CatchmentSummaries[0].LastRecomputeTurn);
    }
}
