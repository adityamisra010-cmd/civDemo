using Sim.Core;
using Sim.Core.Kernel;
using Sim.Core.State;

namespace Sim.Tests.Kernel;

// T0.7 acceptance: orders provably reach state, and replay(seed, orderLog)
// reproduces the ordered run hash-for-hash (§3.9).
public class ReplayTests
{
    private static TurnExecutor Executor(OrderLog? orders = null)
    {
        using var eraStream = Sim.Data.DataFiles.OpenEraPacing();
        // Toy preset (T1.5): the SetRainBias order targets the retired toy
        // WeatherSystem; kernel-invariant replay coverage stays here (T1.9
        // extends replay to the production preset).
        using var pipeStream = Sim.Data.DataFiles.OpenPipelineToy();
        return new TurnExecutor(
            EraTableLoader.Load(eraStream),
            PipelineLoader.Load(pipeStream, SystemCatalog.All(TestUtil.TestConfigs.Sim())),
            orders);
    }

    private static WorldState Genesis(ulong seed)
    {
        var world = new WorldState(seed);
        world.Regions.Add(new RegionRow(new RegionId(0)));
        world.Regions.Add(new RegionRow(new RegionId(1)));
        return world;
    }

    private static OrderLog RainBiasLog() {
        var log = new OrderLog();
        // Delivered to the step executing from turn-5 state (Prev.Clock.Turn == 5).
        log.Append(new OrderRecord(Turn: 5, ActorId: 1, OrderKind.SetRainBias, TargetId: 0, Amount: 500.0));
        log.Append(new OrderRecord(Turn: 5, ActorId: 1, OrderKind.SetRainBias, TargetId: 1, Amount: -250.0));
        log.Append(new OrderRecord(Turn: 9, ActorId: 1, OrderKind.SetRainBias, TargetId: 0, Amount: 42.5));
        return log;
    }

    [Fact]
    public void SyntheticOrder_ProvablyReachesState()
    {
        // Same seed, with vs without the order: worlds must diverge — and only
        // from the ordered turn onward.
        var plain = Executor();
        var ordered = Executor(RainBiasLog());

        WorldState a = Genesis(42), b = Genesis(42);
        for (int turn = 0; turn < 5; turn++)
        {
            a = plain.Step(a);
            b = ordered.Step(b);
        }
        Assert.Equal(WorldHash.ComputeHex(a), WorldHash.ComputeHex(b)); // pre-order: identical

        a = plain.Step(a);
        b = ordered.Step(b);
        Assert.NotEqual(WorldHash.ComputeHex(a), WorldHash.ComputeHex(b)); // order landed
    }

    [Fact]
    public void Replay_SeedPlusOrderLog_ReproducesHashForHash()
    {
        // Original run with orders, recording per-turn hashes.
        var log = RainBiasLog();
        var original = Executor(log);
        var hashes = new List<string>();
        WorldState world = Genesis(42);
        for (int turn = 0; turn < 20; turn++)
        {
            world = original.Step(world);
            hashes.Add(WorldHash.ComputeHex(world));
        }

        // Serialize the log (the artifact a real replay would load)…
        using var buffer = new MemoryStream();
        log.Save(buffer);
        buffer.Position = 0;
        OrderLog reloaded = OrderLog.Load(buffer);

        // …and replay from seed + order log alone: hash-for-hash, every turn.
        var replay = Executor(reloaded);
        WorldState replayWorld = Genesis(42);
        for (int turn = 0; turn < 20; turn++)
        {
            replayWorld = replay.Step(replayWorld);
            Assert.Equal(hashes[turn], WorldHash.ComputeHex(replayWorld));
        }
    }

    [Fact]
    public void OrderLog_IsAppendOnly_InTurnOrder()
    {
        var log = new OrderLog();
        log.Append(new OrderRecord(5, 1, OrderKind.SetRainBias, 0, 1.0));
        var e = Assert.Throws<ArgumentException>(() =>
            log.Append(new OrderRecord(3, 1, OrderKind.SetRainBias, 0, 1.0)));
        Assert.Contains("append-only", e.Message);
    }

    [Fact]
    public void OrderLogIO_RejectsBadMagicAndVersion()
    {
        using var junk = new MemoryStream("garbage bytes here....."u8.ToArray());
        Assert.Throws<SnapshotFormatException>(() => OrderLog.Load(junk));

        var log = RainBiasLog();
        using var buffer = new MemoryStream();
        log.Save(buffer);
        byte[] bytes = buffer.ToArray();
        bytes[8] = 99; // version field, after 8-byte magic
        using var corrupted = new MemoryStream(bytes);
        var e = Assert.Throws<SnapshotFormatException>(() => OrderLog.Load(corrupted));
        Assert.Contains("version 99", e.Message);
    }
}
