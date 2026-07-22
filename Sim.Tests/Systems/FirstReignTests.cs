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
        WorldState world = WorldFounding.Found(TestConfigs.Worldgen(), cfg, 42); // FULL 1024²
        OrderValidation.ValidateAgainstWorld(Fixture(), world);

        trajectory = [];
        for (int t = 1; t <= turns; t++)
        {
            world = exec.Step(world);
            long pop = 0;
            for (int i = 0; i < world.PopBands.Count; i++) pop += world.PopBands[i].Count.Value;
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
        //   v1 (T1.9, post-Leontief): pinned below.
        //   v1 value: 6c32ed53d2d0a1d19753847ea23cd3c92b9d02ce51f32a6f3eea63e66627e246
        const string golden = "6c32ed53d2d0a1d19753847ea23cd3c92b9d02ce51f32a6f3eea63e66627e246";
        Assert.Equal(golden, WorldHash.ComputeHex(final));

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
