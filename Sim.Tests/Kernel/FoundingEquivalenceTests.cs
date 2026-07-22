using Sim.Cli;
using Sim.Core;
using Sim.Core.Kernel;
using Sim.Core.State;
using Sim.Core.Systems;
using Sim.Core.Worldgen;
using Sim.Tests.TestUtil;

namespace Sim.Tests.Kernel;

// T1.9 — FOUNDING EQUIVALENCE, the load-bearing wall: UI-session replay is
// fiction unless the UI and the headless CLI found IDENTICAL worlds from the
// same seed. Both apps' recipes are extracted and public (Sim.Ui.UiFounding,
// Sim.Cli.HeadlessFounding); this file pins the CLI recipe and the canonical
// reference against each other, and Sim.Ui.Tests pins UiFounding against the
// SAME canonical reference — transitively, all three are one world.
public class FoundingEquivalenceTests
{
    /// <summary>The canonical reference recipe, stated independently here.</summary>
    private static WorldState CanonicalFounded(ulong seed) =>
        WorldFounding.Found(TestConfigs.Worldgen(), TestConfigs.Sim(), seed);

    [Fact]
    public void CliFounding_EqualsCanonical_SameSeedSameWorldHash()
    {
        WorldState cli = HeadlessFounding.Found(42);
        WorldState canonical = CanonicalFounded(42);
        Assert.Equal(WorldHash.ComputeHex(canonical), WorldHash.ComputeHex(cli));
        Assert.True(WorldStates.StateEquals(canonical, cli));
        Assert.NotNull(cli.Terrain);
        Assert.True(cli.Terrain!.ContentHash.AsSpan().SequenceEqual(canonical.Terrain!.ContentHash));
    }

    [Fact]
    public void CliFounding_SizeOverrideBranch_EqualsCanonicalAtThatSize()
    {
        // The --size replay hatch (adversarial finding: a session played on a
        // non-canonical size must be reproducible by the documented command).
        // T2.3: a 256² world cannot host the canonical 12 sites at canonical
        // spacing — the override branch is exercised with BOTH overrides, which
        // also pins the new --settlements plumbing end to end.
        WorldState cli = HeadlessFounding.Found(42, sizeOverridePx: 256, settlementsOverride: 4);
        WorldState canonical = WorldFounding.Found(
            TestConfigs.Worldgen() with { SizePx = 256 }, TestConfigs.Sim(), 42,
            settlementsOverride: 4);
        Assert.Equal(WorldHash.ComputeHex(canonical), WorldHash.ComputeHex(cli));
    }

    [Fact]
    public void CliFounding_DifferentSeeds_DifferentWorlds()
    {
        Assert.NotEqual(
            WorldHash.ComputeHex(HeadlessFounding.Found(42)),
            WorldHash.ComputeHex(HeadlessFounding.Found(43)));
    }

    [Fact]
    public void SimReplay_ConsumesAUiStampedLog_HashForHash()
    {
        // A UI session simulated end-to-end THROUGH ITS FILE FORMAT: orders
        // appended the way the HUD slider emits them, autosaved under the
        // stamped runs/ filename the UI writes, then replayed from disk by the
        // CLI's own founding + executor recipe — hash-for-hash.
        var sessionOrders = new OrderLog();
        var hashes = new List<string>();
        {
            TurnExecutor exec = ProductionExecutor(sessionOrders);
            WorldState world = HeadlessFounding.Found(42);
            for (int t = 1; t <= 30; t++)
            {
                if (t == 2) sessionOrders.Append(new OrderRecord(
                    world.Clock.Turn, ActorId: 1, OrderKind.LaborAllocation, 0, 55.0));
                if (t == 12) sessionOrders.Append(new OrderRecord(
                    world.Clock.Turn, ActorId: 1, OrderKind.LaborAllocation, 0, 20.0));
                world = exec.Step(world);
                hashes.Add(WorldHash.ComputeHex(world));
            }
        }

        // Stamped SHAPE with a per-run unique suffix (adversarial pass: a fixed
        // shared-temp name races across concurrent test processes).
        string logPath = Path.Combine(Path.GetTempPath(),
            $"orders-20260722-000000-{Guid.NewGuid():N}.bin");
        using (var save = File.Create(logPath)) sessionOrders.Save(save);
        try
        {
            OrderLog loaded;
            using (var stream = File.OpenRead(logPath)) loaded = OrderLog.Load(stream);

            TurnExecutor replayExec = ProductionExecutor(loaded);
            WorldState replayed = HeadlessFounding.Found(42);
            OrderValidation.ValidateAgainstWorld(loaded, replayed);
            for (int t = 1; t <= 30; t++)
            {
                replayed = replayExec.Step(replayed);
                Assert.Equal(hashes[t - 1], WorldHash.ComputeHex(replayed));
            }
        }
        finally
        {
            File.Delete(logPath);
        }
    }

    private static TurnExecutor ProductionExecutor(OrderLog orders)
    {
        using var eraStream = Sim.Data.DataFiles.OpenEraPacing();
        using var pipeStream = Sim.Data.DataFiles.OpenPipeline();
        return new TurnExecutor(
            EraTableLoader.Load(eraStream),
            PipelineLoader.Load(pipeStream, SystemCatalog.All(TestConfigs.Sim())), orders);
    }
}
