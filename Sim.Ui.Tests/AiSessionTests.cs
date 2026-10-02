using Sim.Core.Kernel;
using Sim.Core.State;
using Xunit;

namespace Sim.Ui.Tests;

/// <summary>
/// ADR-033 D5 at the SESSION seam: UiSession.EndTurn appends every AI Empire's orders (AiOrders) to the
/// session's order log before each step — the Age-only call it replaces is gone — and a session founded with
/// an AI-empire override records it, so its log replays into the same world hash for hash.
/// </summary>
public class AiSessionTests
{
    private static readonly PolityId Rival = new(2);

    [Fact]
    public void WithoutAnAiEmpire_EndTurnAppendsNothing()
    {
        var session = Sim.Ui.UiSession.Start(42, sizeOverridePx: 256, settlementsOverride: 4);
        for (int t = 0; t < 3; t++) session.EndTurn();
        Assert.Equal(0, session.Orders.Count);
        Assert.Null(session.AiEmpiresOverride);
        Assert.Null(session.Manifest("now", "runs/orders-x.bin").AiEmpires);
    }

    [Fact]
    public void WithAnAiEmpire_EndTurnAppendsItsOrders_DeliveredTheNextStep_AndTheSavedLogReplaysHashForHash()
    {
        var session = Sim.Ui.UiSession.Start(42, sizeOverridePx: 256, settlementsOverride: 4, aiEmpiresOverride: 1);
        Assert.True(EmpireQuery.TryGetCommandSource(session.World, Rival, out CommandSource source));
        Assert.Equal(CommandSource.Ai, source);
        var hashes = new List<string>();
        for (int t = 1; t <= 25; t++)
        {
            session.EndTurn();
            hashes.Add(WorldHash.ComputeHex(session.World));
            if (t == 1)
            {
                // TURN-EXACT DELIVERY: the AI's research order is stamped with turn 0 (the turn it was decided
                // in) and applied by the step 0 -> 1, so turn 1 already has the target.
                OrderRecord first = session.Orders[0];
                Assert.Equal((0L, Rival.Value, OrderKind.SetResearchTarget), (first.Turn, first.ActorId, first.Kind));
                Assert.True(ResearchQuery.TryGetTarget(session.World, Rival, out ResearchNodeId target));
                Assert.Equal(first.TargetId, target.Value);
            }
        }
        Assert.True(session.Orders.Count > 0);
        for (int i = 0; i < session.Orders.Count; i++) Assert.Equal(Rival.Value, session.Orders[i].ActorId);

        // The override is part of the world's identity: in the manifest, the replay command and the log name.
        SessionManifest m = session.Manifest("now", "runs/orders-x.bin");
        Assert.Equal(1, m.AiEmpires);
        Assert.Contains("--ai-empires 1", m.ReplayCommand(25));
        Assert.EndsWith("-s256-n4-a1.bin", Sim.Ui.UiSession.SessionLogPath(new DateTime(2026, 10, 2, 12, 0, 0), 256, 4, 1));
        Assert.Equal(1, session.ForensicRun("now").AiEmpiresConfigured);

        string logPath = Path.Combine(Path.GetTempPath(), $"orders-ai-replay-{Guid.NewGuid():N}.bin");
        session.Save(logPath);
        try
        {
            OrderLog loaded;
            using (var stream = File.OpenRead(logPath)) loaded = OrderLog.Load(stream);
            Assert.Equal(session.Orders.Count, loaded.Count);
            TurnExecutor exec = Sim.Ui.UiSession.BuildProductionExecutor(loaded);
            WorldState world = Sim.Ui.UiFounding.Found(42, 256, 4, aiEmpiresOverride: 1);
            OrderValidation.ValidateAgainstWorld(loaded, world);
            for (int t = 1; t <= hashes.Count; t++)
            {
                world = exec.Step(world);
                Assert.Equal(hashes[t - 1], WorldHash.ComputeHex(world));
            }
        }
        finally
        {
            File.Delete(logPath);
        }
    }
}
