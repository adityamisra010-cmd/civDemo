using Sim.Core;
using Sim.Core.Kernel;
using Sim.Core.State;
using Sim.Core.Systems;
using Sim.Core.Worldgen;
using Sim.Tests.TestUtil;

namespace Sim.Tests.Systems;

// T2.13 (M2 exit HELD — director packet): the COLLAPSING-WORLD stability
// teeth. The T2.8 detectors validated migration on HEALTHY worlds; the
// director's exit session showed a starved-cluster regime none of them
// covered: an emptied food-less settlement became the world's strongest
// magnet (land per capita), famine flight funneled refugees INTO the famine,
// and dying hamlets circulated 40–70% of their population every turn for 150+
// consecutive turns. Every test here reproduces that REGIME (director-style
// farm-0% orders on a founded world) and asserts the pattern cannot recur.
// VACUITY DISCIPLINE (adversarial pass, commit 966109b as the pre-fix pin):
// the three collapse detectors were each run against PRE-FIX mechanics and
// FAILED there — no-starving-gain ("10 migrants walked INTO settlement 0"),
// dead-stays-dead ("RESURRECTED to 3"), and the surge detector at streak 13
// vs the bar of 8 (the ADVERSARIAL PASS caught the first rig as vacuous for
// this detector — dev-scale populations collapse in short waves, so the ×30
// founding scale below is load-bearing, not decorative). Post-fix measured
// streaks: 1/3/0/0. SmallN_HealthyHamlets is NOT a T2.13 detector — it is
// born green on both trees (deficit-free, viability ≡ 1) and stands as a
// T2.8 gap-cap regression pin under the T2.13 config shape.
public class CollapseStabilityTests
{
    private const ulong Seed = 42;

    private static TurnExecutor Executor(SimConfig cfg, OrderLog orders)
    {
        using var eraStream = Sim.Data.DataFiles.OpenEraPacing();
        using var pipeStream = Sim.Data.DataFiles.OpenPipeline();
        return new TurnExecutor(EraTableLoader.Load(eraStream),
            PipelineLoader.Load(pipeStream, SystemCatalog.All(cfg)), orders);
    }

    /// <summary>Director-style collapse schedule on the dev world (N = 4):
    /// settlement 0 stops farming at turn 1, settlement 1 at turn 8 —
    /// settlements 2 and 3 stay healthy as the only viable destinations. The
    /// rig TUNE shrinks the founding store to 4,000 (the dev world's default
    /// surplus is so large relative to its demand that famine would otherwise
    /// take thousands of turns to arrive — the director's canonical session
    /// reached it because populations were 30x larger).</summary>
    private static OrderLog CollapseOrders()
    {
        var log = new OrderLog();
        log.Append(new OrderRecord(1, ActorId: 1, OrderKind.LaborAllocation, 0, 0.0));
        log.Append(new OrderRecord(8, ActorId: 1, OrderKind.LaborAllocation, 1, 0.0));
        return log;
    }

    private sealed record TurnRow(
        long[] Pop, long[] Store, long[] Harvest, double[] Deficit,
        long[] Inflow, long[] Outflow);

    private static List<TurnRow> RunCollapse(int turns)
    {
        SimConfig cfg = TestConfigs.Sim();
        // Rig TUNEs: founding populations ×30 (the director's canonical
        // settlements were ~30× the dev counts — the sustained-surge churn
        // needs director-scale populations to manifest pre-fix) and a small
        // founding store (the dev surplus otherwise defers famine by
        // millennia).
        var scaled = new long[Cohorts.Count];
        for (int c = 0; c < Cohorts.Count; c++) scaled[c] = cfg.Founding.CohortCounts[c] * 30;
        cfg = cfg with
        {
            Founding = cfg.Founding with { FoodStore = 4000, CohortCounts = scaled },
        };
        TurnExecutor exec = Executor(cfg, CollapseOrders());
        WorldState world = WorldFounding.Found(TestConfigs.DevWorldgen(), cfg, Seed, null);
        int n = world.Settlements.Count;
        var rows = new List<TurnRow>(turns);
        for (int t = 1; t <= turns; t++)
        {
            world = exec.Step(world);
            var row = new TurnRow(new long[n], new long[n], new long[n], new double[n], new long[n], new long[n]);
            for (int s = 0; s < n; s++)
            {
                SettlementId id = world.Settlements[s].Id;
                for (int b = 0; b < world.Buckets.Count; b++)
                    if (world.Buckets[b].Settlement == id) row.Pop[s] += world.Buckets[b].Count.Value;
                for (int f = 0; f < world.FoodStores.Count; f++)
                    if (world.FoodStores[f].Settlement == id)
                    { row.Store[s] = world.FoodStores[f].Store.Value; row.Harvest[s] = world.FoodStores[f].LastHarvestUnits; break; }
                for (int d = 0; d < world.ConsumptionDeficits.Count; d++)
                    if (world.ConsumptionDeficits[d].Settlement == id)
                    { row.Deficit[s] = world.ConsumptionDeficits[d].DeficitRatio; break; }
                for (int mf = 0; mf < world.MigrationFlows.Count; mf++)
                    if (world.MigrationFlows[mf].Settlement == id)
                    { row.Inflow[s] = world.MigrationFlows[mf].Inflow; row.Outflow[s] = world.MigrationFlows[mf].Outflow; break; }
            }
            rows.Add(row);
        }
        return rows;
    }

    [Fact]
    public void CollapsingWorld_NoStarvingSettlementGainsPopulation()
    {
        // THE DIRECTOR'S OBSERVATION, as permanent teeth: a settlement whose
        // store AND harvest were zero with a severe deficit must never gain
        // net population — arrivals into a famine were the inverted
        // incentive; with destination viability they are structurally zero
        // (and famine fertility suppression zeroes births), so population at
        // such a settlement can only fall.
        List<TurnRow> rows = RunCollapse(200);
        bool famineSeen = false;
        for (int t = 1; t < rows.Count; t++)
        {
            TurnRow prev = rows[t - 1], cur = rows[t];
            for (int s = 0; s < prev.Pop.Length; s++)
            {
                if (prev.Store[s] > 0 || prev.Harvest[s] > 0 || prev.Deficit[s] < 0.9) continue;
                famineSeen = true;
                Assert.True(cur.Pop[s] <= prev.Pop[s],
                    $"turn {t + 1}: settlement {s} GREW {prev.Pop[s]} -> {cur.Pop[s]} with zero " +
                    $"food and deficit {prev.Deficit[s]:F2} — starvation magnetism is back");
                Assert.True(cur.Inflow[s] == 0,
                    $"turn {t + 1}: {cur.Inflow[s]} migrants walked INTO settlement {s} at zero food " +
                    $"and severe deficit — destination viability is not gating");
            }
        }
        Assert.True(famineSeen, "no zero-food severe famine ever occurred — collapse rig vacuous");
    }

    [Fact]
    public void CollapsingWorld_NoSustainedSurgeChurn()
    {
        // The chronicle pattern that held the exit: surge-scale out-migration
        // (≥ 20% of start-of-turn population, populations ≥ 10) every single
        // turn, indefinitely — the director logged ~150 consecutive turns at
        // 40–70%. Honest flight is a SURGE THAT ENDS: the settlement empties
        // or stabilizes. Bar: no settlement sustains surge-scale outflow for
        // more than 8 CONSECUTIVE turns. Measured anchors at the ×30 rig
        // scale: PRE-fix 13 (the circulation refilled the settlement each
        // turn), post-fix 3 — the bar sits between with margin both ways.
        List<TurnRow> rows = RunCollapse(200);
        int n = rows[0].Pop.Length;
        long totalOut = 0;
        for (int s = 0; s < n; s++)
        {
            int streak = 0, maxStreak = 0;
            for (int t = 1; t < rows.Count; t++)
            {
                long startPop = rows[t - 1].Pop[s];
                long outflow = rows[t].Outflow[s];
                totalOut += outflow;
                bool surge = startPop >= 10 && outflow * 5 >= startPop; // ≥ 20%
                streak = surge ? streak + 1 : 0;
                maxStreak = Math.Max(maxStreak, streak);
            }
            Assert.True(maxStreak <= 8,
                $"settlement {s}: surge-scale outflow for {maxStreak} consecutive turns — " +
                $"the refugee-circulation churn is back");
        }
        Assert.True(totalOut > 0, "no migration at all through a two-settlement collapse — rig vacuous");
    }

    [Fact]
    public void CollapsingWorld_DeadStaysDead_NoResurrectionCycle()
    {
        // The resurrection cycle (replay finding): last person dies → demand
        // 0 → deficit READS 0.00 → the food-less ruin turns viability-1 with
        // astronomical land-per-capita → a colonist wave arrives, breeds one
        // turn on the stale signal, starves, dies → repeat every ~9 turns.
        // With the absolute food gate, a ruin with no store and no harvest
        // receives NOBODY: once a settlement dies food-less it stays empty.
        List<TurnRow> rows = RunCollapse(200);
        int n = rows[0].Pop.Length;
        var deadSince = new int[n];
        Array.Fill(deadSince, -1);
        bool anyDeath = false;
        for (int t = 0; t < rows.Count; t++)
        {
            for (int s = 0; s < n; s++)
            {
                TurnRow cur = rows[t];
                if (deadSince[s] < 0 && cur.Pop[s] == 0 && cur.Store[s] == 0 && cur.Harvest[s] == 0)
                { deadSince[s] = t; anyDeath = true; }
                else if (deadSince[s] >= 0)
                {
                    Assert.True(cur.Pop[s] == 0,
                        $"turn {t + 1}: settlement {s} died food-less at turn {deadSince[s] + 1} and was " +
                        $"RESURRECTED to {cur.Pop[s]} — colonists walked into a ruin with nothing to eat");
                }
            }
        }
        Assert.True(anyDeath, "no settlement ever died — resurrection detector vacuous");
    }

    [Fact]
    public void SmallN_HealthyHamlets_NoPerpetualChurn()
    {
        // SMALL-N regime (director diagnosis 1): two FED hamlets, populations
        // in the tens — per-capita attractiveness is integer-noise-dominated
        // and relative flows are large. The stabilized system may rebalance,
        // but must not churn: over 100 turns the TOTAL gross migration must
        // stay under one full population turnover, and net flows must not
        // alternate (the small-N ping-pong cousin).
        SimConfig cfg = TestConfigs.Sim();
        WorldState world = MigrationTestWorld.TwoSettlements(
            sourceCounts: 2, destCounts: 1, destFood: 50_000, travelCost: 20.0);
        // Source food too — BOTH healthy (the rig helper endows the dest).
        new Ledger(world.LedgerFlows).Flow(ref world.FoodStores.Ref(0).Store,
            ConservedQuantityIds.Food, ReasonIds.InitialEndowment, 50_000,
            FlowDirection.Source, OverdrawPolicy.Throw);
        var exec = new TurnExecutor(FlatEra(10.0), [SystemCatalog.Migration(cfg)]);

        long gross = 0;
        var net = new long[100];
        long startPop = 32 + 16; // 16 cohorts × (2 + 1)
        for (int t = 0; t < 100; t++)
        {
            world = exec.Step(world);
            net[t] = world.MigrationFlows[0].Inflow - world.MigrationFlows[0].Outflow;
            gross += world.MigrationFlows[0].Outflow + world.MigrationFlows[1].Outflow;
        }
        Assert.True(gross <= startPop,
            $"small-N hamlets churned {gross} gross moves from a standing population of {startPop} " +
            $"in 100 turns — perpetual small-N circulation");
        int alternations = MigrationStabilityTests.ConsecutiveAlternationCount(net);
        Assert.True(alternations <= 10,
            $"{alternations} consecutive net-flow alternations across 100 small-N turns — ping-pong cousin");
    }

    private static EraTable FlatEra(double dtYears) => EraTableLoader.Load(
        $$"""{ "bands": [ { "name": "flat", "startYear": 0, "endYear": 100000, "dtYears": {{dtYears.ToString(System.Globalization.CultureInfo.InvariantCulture)}} } ] }""");
}
