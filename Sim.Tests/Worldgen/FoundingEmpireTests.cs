using Sim.Core;
using Sim.Core.Kernel;
using Sim.Core.State;
using Sim.Core.Systems;
using Sim.Core.Worldgen;
using Sim.Tests.TestUtil;

namespace Sim.Tests.Worldgen;

/// <summary>
/// M4-C — the founded world's initial Empire, driven through the REAL
/// WorldFounding.Found path rather than by hand-assembling rows. Hand-built
/// worlds already pass the M4-A tests; what was never proven is that FOUNDING
/// produces one, which is the whole content of this packet.
/// </summary>
public class FoundingEmpireTests
{
    private static WorldState Found(int? settlements = null, ulong seed = 42)
        => WorldFounding.Found(TestConfigs.Worldgen(), TestConfigs.Sim(), seed, settlements);

    /// <summary>The Empire a founded world hands the player.</summary>
    private static PolityId PlayerOf(WorldState world)
    {
        for (int i = 0; i < world.Polities.Count; i++)
        {
            if (world.Polities[i].Source == CommandSource.Player) return world.Polities[i].Id;
        }

        throw new InvalidOperationException("founded world has no player-commanded Empire");
    }

    [Fact]
    public void SingleSettlementFounding_ProducesOnePlayerEmpireControllingItsOneSettlement()
    {
        WorldState world = Found(settlements: 1);

        Assert.Equal(1, world.Settlements.Count);
        Assert.Equal(1, world.Polities.Count);
        Assert.Equal(CommandSource.Player, world.Polities[0].Source);

        PolityId player = PlayerOf(world);
        Assert.Equal(1, world.Controls.Count);
        Assert.Equal(player, world.Controls[0].Polity);
        Assert.Equal(world.Settlements[0].Id, world.Controls[0].Place);

        Assert.Equal(1, world.Capitals.Count);
        Assert.Equal(player, world.Capitals[0].Polity);
        Assert.Equal(world.Settlements[0].Id, world.Capitals[0].Place);

        // ...and the derived view agrees, which is what systems will consume.
        Assert.Equal(1, EmpireQuery.ControlledCount(world, player));
        Assert.True(EmpireQuery.TryGetCapital(world, player, out SettlementId seat));
        Assert.Equal(world.Settlements[0].Id, seat);
        Assert.False(EmpireQuery.IsExtinct(world, player));
        Assert.True(EmpireQuery.IsPlayerCommanded(world, player));
    }

    [Fact]
    public void MultiSettlementFounding_ProducesONEEmpireWithManySettlementsAndONECapital()
    {
        const int n = 4;
        WorldState world = Found(settlements: n);

        Assert.Equal(n, world.Settlements.Count);
        Assert.Equal(1, world.Polities.Count);      // ONE Empire, not N
        Assert.Equal(n, world.Controls.Count);
        Assert.Equal(1, world.Capitals.Count);      // ONE capital, not N

        PolityId player = PlayerOf(world);
        for (int s = 0; s < world.Settlements.Count; s++)
        {
            SettlementId place = world.Settlements[s].Id;
            Assert.True(EmpireQuery.ControlsSettlement(world, player, place),
                $"settlement {place.Value} is not controlled by the founding Empire");
        }

        Assert.Equal(n, EmpireQuery.ControlledCount(world, player));
        Assert.True(EmpireQuery.TryGetCapital(world, player, out SettlementId seat));
        Assert.Equal(world.Settlements[0].Id, seat);   // the FIRST founded site
    }

    [Fact]
    public void TheFoundedEmpireIsTheActorOfAnOrderThroughTheExistingPathway()
    {
        // M4-B bound OrderRecord.ActorId to PolityId. This closes the loop: the
        // Empire that FOUNDING produced is a valid order actor on the real
        // validation path, with no new command pathway and no CommandSource
        // branch anywhere in it.
        WorldState world = Found(settlements: 2);
        PolityId player = PlayerOf(world);

        OrderRecord order = OrderRecord.From(
            turn: 0, player, OrderKind.LaborAllocation, world.Settlements[0].Id.Value, 60);

        Assert.Equal(player.Value, order.ActorId);
        Assert.Equal(player, order.Actor);

        var log = new OrderLog();
        log.Append(order);
        OrderValidation.ValidateAgainstWorld(log, world);   // must not throw

        // The roster is now POPULATED, so the M4-B actor check is live rather
        // than dormant — an unregistered Empire must now be rejected here.
        var bogus = new OrderLog();
        bogus.Append(OrderRecord.From(0, new PolityId(999), OrderKind.LaborAllocation,
            world.Settlements[0].Id.Value, 60));
        Assert.Throws<OrderValidationException>(
            () => OrderValidation.ValidateAgainstWorld(bogus, world));
    }

    [Fact]
    public void FoundingIsDeterministic_IncludingTheEmpireRows()
    {
        WorldState a = Found(settlements: 3);
        WorldState b = Found(settlements: 3);

        Assert.Equal(WorldHash.ComputeHex(a), WorldHash.ComputeHex(b));
        Assert.True(WorldStates.StateEquals(a, b));
        Assert.Equal(a.Polities[0].Id, b.Polities[0].Id);
        Assert.Equal(a.Capitals[0].Place, b.Capitals[0].Place);
    }

    [Fact]
    public void TheEmpireSurvivesCloneAndCanonicalRoundTrip()
    {
        WorldState world = Found(settlements: 3);
        PolityId player = PlayerOf(world);

        WorldState clone = world.Clone();
        Assert.True(WorldStates.StateEquals(world, clone));
        Assert.Equal(3, EmpireQuery.ControlledCount(clone, player));
        Assert.True(EmpireQuery.TryGetCapital(clone, player, out _));

        // SAVE/LOAD through the real Snapshot path. Terrain is deliberately NOT
        // in the stream (ADR-008), so a save is loaded against a regenerated
        // TerrainSet — hashing the raw CanonicalSchema output of a founded world
        // against a bare Read would compare a terrain-bearing world with a
        // terrain-less one and fail for a reason that has nothing to do with the
        // Empire rows.
        using var ms = new MemoryStream();
        Snapshot.Save(world, ms);
        ms.Position = 0;
        WorldState back = Snapshot.Load(ms, Sim.Core.Worldgen.Worldgen.Generate(TestConfigs.Worldgen(), 42));

        Assert.True(WorldStates.StateEquals(world, back), "save/load drifted");
        Assert.Equal(WorldHash.ComputeHex(world), WorldHash.ComputeHex(back));

        // The Empire survived the wire, field by field.
        Assert.Equal(1, back.Polities.Count);
        Assert.Equal(CommandSource.Player, back.Polities[0].Source);
        Assert.Equal(3, EmpireQuery.ControlledCount(back, player));
        Assert.True(EmpireQuery.TryGetCapital(back, player, out SettlementId seat));
        Assert.Equal(world.Settlements[0].Id, seat);
        Assert.True(EmpireQuery.IsPlayerCommanded(back, player));
        Assert.False(EmpireQuery.IsExtinct(back, player));
    }

    [Fact]
    public void AnOrderFromTheFoundedEmpireReplaysIdentically()
    {
        // Same founding, same order, twice through the real executor.
        static string RunOnce()
        {
            WorldState world = Found(settlements: 2);
            PolityId player = PlayerOf(world);

            var orders = new OrderLog();
            orders.Append(OrderRecord.From(
                0, player, OrderKind.LaborAllocation, world.Settlements[0].Id.Value, 60));
            OrderValidation.ValidateAgainstWorld(orders, world);

            using var eraStream = Sim.Data.DataFiles.OpenEraPacing();
            using var pipeStream = Sim.Data.DataFiles.OpenPipeline();
            var exec = new TurnExecutor(
                EraTableLoader.Load(eraStream),
                PipelineLoader.Load(pipeStream,
                    SystemCatalog.All(TestConfigs.Sim(), TestConfigs.Worldgen())),
                orders);
            return WorldHash.ComputeHex(exec.Run(world, 5));
        }

        Assert.Equal(RunOnce(), RunOnce());
    }
}
