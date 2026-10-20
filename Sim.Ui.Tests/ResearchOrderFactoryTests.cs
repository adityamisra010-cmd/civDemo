using Xunit;
using Sim.Core.Kernel;
using Sim.Core.State;
using Sim.Core.Systems.Research;
using Sim.Ui.ViewModel;

namespace Sim.Ui.Tests;

/// <summary>
/// ADR-029 / D-044 R17 — the UI chooses research only through the order pathway. The
/// factory is pure; it refuses what the simulation would ignore; and the session appends
/// the order, which the next End Turn applies.
/// </summary>
public class ResearchOrderFactoryTests
{
    [Fact]
    public void SetTarget_BuildsKindSixForAnAvailableNode_AndRefusesLockedOrUnknownOnes()
    {
        var session = Sim.Ui.UiSession.Start(42, sizeOverridePx: 256, settlementsOverride: 4);
        ResearchContent content = session.Config.Research!;
        PolityId player = UiPlayer.Empire;

        ResearchNodeId root = content.Nodes[content.IndexOfId("cordage")].Key;
        OrderRecord order = ResearchOrderFactory.SetTarget(session.World, content, player, root)!.Value;
        Assert.Equal(OrderKind.SetResearchTarget, order.Kind);
        Assert.Equal(root.Value, order.TargetId);
        Assert.Equal(player.Value, order.ActorId);
        Assert.Equal(session.World.Clock.Turn, order.Turn);
        Assert.Equal(0.0, order.Amount);

        ResearchNodeId locked = content.Nodes[content.IndexOfId("general_ai")].Key;
        Assert.Null(ResearchOrderFactory.SetTarget(session.World, content, player, locked));
        Assert.Null(ResearchOrderFactory.SetTarget(session.World, content, player, new ResearchNodeId(99_999)));
        OrderRecord clear = ResearchOrderFactory.ClearTarget(session.World, player);
        Assert.Equal((-1, session.World.Clock.Turn, OrderKind.SetResearchTarget), (clear.TargetId, clear.Turn, clear.Kind));

        // ADR-033 D5: the factory IS the core builder the AI uses — one constructor, two callers.
        Assert.Equal(ResearchQuery.TargetOrder(session.World, content, player, root), ResearchOrderFactory.SetTarget(session.World, content, player, root));
        Assert.Equal(ResearchQuery.ClearTargetOrder(session.World, player), clear);
    }

    [Fact]
    public void Session_EmitsThroughTheOrderLog_AndTheNextEndTurnAppliesIt()
    {
        var session = Sim.Ui.UiSession.Start(42, sizeOverridePx: 256, settlementsOverride: 4);
        ResearchContent content = session.Config.Research!;
        int before = session.Orders.Count;
        string worldHash = WorldHash.ComputeHex(session.World);

        Assert.False(session.EmitResearchOrder(content.Nodes[content.IndexOfId("general_ai")].Key));
        Assert.Equal(before, session.Orders.Count); // refused: nothing logged

        ResearchNodeId root = content.Nodes[content.IndexOfId("cordage")].Key;
        Assert.True(session.EmitResearchOrder(root));
        Assert.Equal(before + 1, session.Orders.Count);
        Assert.Equal(worldHash, WorldHash.ComputeHex(session.World)); // the UI wrote no state

        session.EndTurn();
        Assert.True(ResearchQuery.TryGetTarget(session.World, UiPlayer.Empire, out ResearchNodeId target)
                    || ResearchQuery.IsCompleted(session.World, UiPlayer.Empire, root));
        if (!ResearchQuery.IsCompleted(session.World, UiPlayer.Empire, root)) Assert.Equal(root, target);
        Assert.True(ResearchQuery.Progress(session.World, UiPlayer.Empire, root) > 0.0
                    || ResearchQuery.IsCompleted(session.World, UiPlayer.Empire, root));
    }
}
