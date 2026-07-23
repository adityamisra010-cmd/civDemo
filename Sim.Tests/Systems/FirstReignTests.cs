using Sim.Core;
using Sim.Core.Kernel;
using Sim.Core.State;
using Sim.Core.Systems;
using Sim.Core.Worldgen;
using Sim.Tests.TestUtil;

namespace Sim.Tests.Systems;

// THE FIRST REIGN (T1.8 re-gate): the director's actual played session,
// replayed headless as a permanent fixture. This log exposed the ghost-harvest
// spec defect — 0% farm at turn 3 starved the settlement to extinction, then a
// 38% order at turn 18 harvested +15,858 food/turn into a dead world (333,018
// banked by turn 40, unbounded). Post-Leontief, this test pins the fixed
// trajectory: harvest collapses WITH the population and the dead world stays
// dead. Runs at the full 1024² canonical config, seed 42 — exactly the world
// the UI founds.
public class FirstReignTests
{
    private static OrderLog Fixture()
    {
        using var stream = File.OpenRead(
            Path.Combine(AppContext.BaseDirectory, "Fixtures", "first-reign-orders.bin"));
        return OrderLog.Load(stream);
    }

    private static WorldState Replay(int turns, out List<(long Pop, long Food, long Harvest)> trajectory)
    {
        SimConfig cfg = TestConfigs.Sim();
        using var eraStream = Sim.Data.DataFiles.OpenEraPacing();
        using var pipeStream = Sim.Data.DataFiles.OpenPipeline();
        var exec = new TurnExecutor(
            EraTableLoader.Load(eraStream),
            PipelineLoader.Load(pipeStream, SystemCatalog.All(cfg)), Fixture());
        // FULL 1024², N = 1 via the D-029 flag (T2.3): the fixture is a
        // single-settlement director session and replays at --settlements 1.
        WorldState world = WorldFounding.Found(TestConfigs.Worldgen(), cfg, 42, settlementsOverride: 1);
        OrderValidation.ValidateAgainstWorld(Fixture(), world);

        trajectory = [];
        for (int t = 1; t <= turns; t++)
        {
            world = exec.Step(world);
            long pop = 0;
            for (int i = 0; i < world.Buckets.Count; i++) pop += world.Buckets[i].Count.Value;
            long harvest = 0;
            for (int i = 0; i < world.LedgerFlows.Count; i++)
            {
                LedgerFlowRow row = world.LedgerFlows[i];
                if (row.Quantity == ConservedQuantityIds.Food && row.Reason == ReasonIds.Harvest)
                    harvest = row.TotalSourced;
            }
            trajectory.Add((pop, world.FoodStores[0].Store.Value, harvest));
            Assert.True(ConservationAuditor.IsConserved(world, out string report), $"turn {t}: {report}");
        }
        return world;
    }

    [Fact]
    public void FirstReign_PostFix_HarvestDiesWithThePeople_NoFoodMountain()
    {
        WorldState final = Replay(40, out var trajectory);

        // T1.9 PIN — the director's first reign guards the Leontief fix
        // forever: the full 40-turn ordered trajectory is hash-pinned (a
        // founded-world ORDERED golden). Breaks loudly on any sim-behavior
        // change; update deliberately with a history line, never casually.
        //   v1 (T1.9, post-Leontief):
        //   6c32ed53d2d0a1d19753847ea23cd3c92b9d02ce51f32a6f3eea63e66627e246
        //   v2 (T2.1, D-026 cohort buckets — DELIBERATE, D-029): trajectory
        //   re-pins under the cohort demographics; the shape asserts below
        //   remain the load-bearing guard.
        //   v2 value: b4af3b3dff1cc62cf0b55f7a7234f2af4cc7c64bc91b98ca0b7a3107f53df504
        //   v3 (T2.2, D-020 class system — DELIBERATE): schema v8 + the class
        //   pipeline; the famine still extinguishes the settlement and the
        //   shape asserts stand unchanged.
        //   v3 value: 1a377e2e26cf5f0b83f75b3a8e509c290e385d33958711746247fee6d48fac44
        //   v4 (T2.5 — SCHEMA-ONLY): BucketRow gained MigrationRemainder and
        //   two empty tables joined the stream. At N = 1 migration NO-OPS (a
        //   flow needs a second settlement), so the TRAJECTORY is unchanged —
        //   the shape asserts below pass untouched; only the byte stream grew.
        //   v4 value: 8a108763b1476489895a5c34ff63ad2060d65ee8d476a6cf8df49c528f7c709c
        //   v5 (T2.7, historical demographic retune — DELIBERATE, behavior +
        //   schema v10): the pre-modern vital rates change every count on the
        //   trajectory, and famine fertility suppression halts conceptions
        //   during the director's engineered starvation. The SHAPE of the
        //   played session survives the retune — extinction still lands inside
        //   (5, 25], the dead world stays frozen, no food mountain — and the
        //   shape asserts below re-verified against the new trajectory.
        //   v5 value: d457c2042bdd462ce1f8f7ee432fb264607ec55e8ea5373a6969c7e7fd48fb2c
        //   v6 (T2.6 — OBSERVATIONAL TABLES ONLY): schema v11 + needsgrievance
        //   in the pipeline; the trajectory is unchanged (grievance accrues
        //   during the director's famine and is read by nothing) — the shape
        //   asserts below pass untouched; the byte stream gained the
        //   vitals/satisfaction/grievance rows.
        //   v6 value: fd02c400127ea8972ac271721637538fee371265ec31c38131963a36d87ef17e
        //   v7 (T2.8, migration stabilization): at N = 1 migration still moves
        //   nobody, but the system now WRITES its EMA filter row every turn
        //   (schema v12 state) — the byte stream changes; the trajectory does
        //   not, and the shape asserts below stand untouched.
        //   v7 value: 15f44bd9ac90febda378db5eb4299843da81e9fbcdc31b061853bc31448a6f6b
        //   v8 (T2.7b, ADR-011 exponential-survival micro-step kernel —
        //   DELIBERATE, behavior only): the micro-step integration changes
        //   every count on the director's famine trajectory. The SHAPE of the
        //   played session survives — extinction still lands inside (5, 25],
        //   the dead world stays frozen, no food mountain — and the shape
        //   asserts below re-verified against the new trajectory.
        const string golden = "e5c9df592206c2da34cd17f7dbdc5e046819bcf01102df3b4c79b713f97149c5";
        Assert.Equal(golden, WorldHash.ComputeHex(final));

        // SHAPE ASSERTS — the anti-blind-repin guard (adversarial pass): they
        // assert trajectory SEMANTICS, so a ghost-harvest revert plus a
        // mechanical golden re-pin still fails here. Never delete these as
        // "redundant with the golden".
        // The famine plays out (the director's 0%-farm order really starves).
        int extinctionTurn = trajectory.FindIndex(x => x.Pop == 0) + 1;
        Assert.True(extinctionTurn is > 5 and <= 25,
            $"extinction at turn {extinctionTurn} — outside the played session's shape");

        // Post-extinction, FOREVER: harvest total static (the turn-18 order
        // resurrects nothing), food static, population stays zero.
        long harvestAtDeath = trajectory[extinctionTurn - 1].Harvest;
        long foodAtDeath = trajectory[extinctionTurn - 1].Food;
        for (int t = extinctionTurn; t < trajectory.Count; t++)
        {
            Assert.Equal(0, trajectory[t].Pop);
            Assert.Equal(harvestAtDeath, trajectory[t].Harvest);
            Assert.Equal(foodAtDeath, trajectory[t].Food);
        }

        // The ghost food mountain never forms (pre-fix: 333,018 by turn 40).
        long maxFood = trajectory.Max(x => x.Food);
        Assert.True(maxFood < 100_000, $"food mountain formed: peak {maxFood}");
    }

    [Fact]
    public void FirstReign_ReplayIsTwinDeterministic()
    {
        WorldState a = Replay(25, out _);
        WorldState b = Replay(25, out _);
        Assert.Equal(WorldHash.ComputeHex(a), WorldHash.ComputeHex(b));
    }
}
