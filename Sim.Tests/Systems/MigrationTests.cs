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
        // crosses anything.
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
    public void MagnitudeCorridor_FewPercentPerDecade_WithTeeth()
    {
        // TUNE corridor: the worst settlement's fed-turn gross outflow within
        // [3%, 8%] per decade in dev autoplay (measured rate-response curve:
        // 0.1× → 0.18%, 0.3× → 0.49%, 1× → 4.61%, 3× → 2.07%, 10× → 2.14%).
        SimConfig cfg = TestConfigs.Sim();
        double worst = MaxGrossPerDecade(cfg);
        Assert.True(worst is > 0.03 and < 0.08,
            $"gross migration {worst:P2}/decade outside the [3%, 8%] corridor");

        // ...WITH TEETH, and a finding worth stating: the response curve is
        // NON-MONOTONE — a 10× rate fails the corridor from BELOW, because
        // over-hot migration equalizes attractiveness so thoroughly that the
        // steady gap-driven drift collapses (self-equalization homeostasis).
        // A 0.1× rate fails low too. The corridor therefore detects
        // mis-tuning in BOTH directions.
        SimConfig hot = cfg with
        {
            Migration = cfg.Migration with { BaseRatePerYear = cfg.Migration.BaseRatePerYear * 10 },
        };
        double hotWorst = MaxGrossPerDecade(hot);
        Assert.True(hotWorst < 0.03 || hotWorst > 0.08,
            $"10× rate produced {hotWorst:P2}/decade — inside the corridor, no teeth");
        SimConfig cold = cfg with
        {
            Migration = cfg.Migration with { BaseRatePerYear = cfg.Migration.BaseRatePerYear * 0.1 },
        };
        double coldWorst = MaxGrossPerDecade(cold);
        Assert.True(coldWorst < 0.03,
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
