using Sim.Core;
using Sim.Core.Kernel;
using Sim.Core.State;
using Sim.Core.Systems;
using Sim.Core.Worldgen;
using Sim.Tests.Observability;
using Sim.Tests.TestUtil;

namespace Sim.Tests.Systems;

/// <summary>
/// ADR-035 §6 (P-F0) — a newly created bucket row's D-004 DEATH accumulator starts at its stationary mean, 0.5, at
/// turn-zero founding AND at frontier founding, so the row's first integer reconciliation ROUNDS instead of
/// flooring. Before this, founding seeded 0.0 and the first reconciliation floored every row, leaving ~8 people per
/// settlement alive who had died in the exact micro-state; they died on turn 2 (the D-004 warm-up half of the
/// turn-2 dip).
/// </summary>
public class FoundingRemainderTests
{
    private static EraTable FlatEra() => EraTableLoader.Load(
        """{ "bands": [ { "name": "flat", "startYear": 0, "endYear": 100000, "dtYears": 10 } ] }""");

    [Fact]
    public void TurnZeroFounding_SeedsEveryRowsDeathRemainderAtOneHalf_AndNothingElse()
    {
        WorldState w = WorldFounding.Found(TestConfigs.Worldgen(), TestConfigs.Sim(), 42UL);
        Assert.True(w.Buckets.Count > 0);
        for (int i = 0; i < w.Buckets.Count; i++)
        {
            BucketRow b = w.Buckets[i];
            Assert.Equal(0.5, b.DeathRemainder);
            Assert.Equal(WorldFounding.FoundingDeathRemainder, b.DeathRemainder);
            Assert.Equal(0.0, b.BirthRemainder);
            Assert.Equal(0.0, b.AgingRemainder);
            Assert.Equal(0.0, b.StarvationRemainder);
        }
    }

    [Fact]
    public void FrontierFounding_SeedsTheColonysNewRowsAtOneHalf()
    {
        // The stranded-source rig: settlement 12 is founded on the first full step.
        ObservedWorlds.Run run = ObservedWorlds.FoundingRun(1);
        WorldState w = run.Final;
        var colony = new SettlementId(12);
        bool founded = false;
        for (int i = 0; i < w.Settlements.Count; i++) founded |= w.Settlements[i].Id == colony;
        Assert.True(founded, "settlement 12 was not founded — rig vacuous");
        int rows = 0;
        for (int i = 0; i < w.Buckets.Count; i++)
        {
            if (w.Buckets[i].Settlement != colony) continue;
            rows++;
            Assert.Equal(WorldFounding.FoundingDeathRemainder, w.Buckets[i].DeathRemainder);
        }
        Assert.True(rows > 0, "the colony has no bucket rows — rig vacuous");
    }

    /// <summary>The arithmetic the seed buys, on a deaths-only rig (the oldest cohort alone: no births, nothing
    /// ages in): with the accumulator at r0 the integer deaths are floor(exact + r0) and the new accumulator is
    /// exact + r0 − deaths, so exact = deaths + r' − r0 is recovered from the rows. Seeded at 0.5 the first
    /// reconciliation ROUNDS exact deaths (|deaths − exact| ≤ ½); seeded at 0 it FLOORS them.</summary>
    [Theory]
    [InlineData(37L)]
    [InlineData(101L)]
    [InlineData(250L)]
    [InlineData(999L)]
    public void TheFirstReconciliation_Rounds_AtTheFoundingSeed_AndFloors_AtZero(long elders)
    {
        SimConfig cfg = TestConfigs.Sim();
        var exec = new TurnExecutor(FlatEra(), [SystemCatalog.Demographics(cfg)]);
        double Recovered(double r0, out long deaths)
        {
            var counts = new long[Cohorts.Count];
            counts[Cohorts.Count - 1] = elders;
            WorldState w = PopulationExactnessTests.BucketWorld(counts);
            int row = Cohorts.Count - 1;
            w.Buckets.Ref(row).DeathRemainder = r0;
            WorldState next = exec.Step(w);
            deaths = elders - next.Buckets[row].Count.Value;
            return deaths + next.Buckets[row].DeathRemainder - r0;
        }

        double exactSeeded = Recovered(WorldFounding.FoundingDeathRemainder, out long seeded);
        double exactZero = Recovered(0.0, out long floored);
        Assert.Equal(exactZero, exactSeeded, 9);                       // the same exact deaths in both arms
        Assert.True(seeded > 0, "no deaths — rig vacuous");
        Assert.Equal((long)Math.Floor(exactZero), floored);           // zero seed: a floor
        Assert.Equal((long)Math.Floor(exactSeeded + 0.5), seeded);    // founding seed: round half up
        Assert.True(Math.Abs(seeded - exactSeeded) <= 0.5 + 1e-9);
    }
}
