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
        PolityId player = LaborOrderFactory.PlayerEmpire;

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
        Assert.Equal(-1, ResearchOrderFactory.ClearTarget(7, player).TargetId);
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
        Assert.True(ResearchQuery.TryGetTarget(session.World, LaborOrderFactory.PlayerEmpire, out ResearchNodeId target)
                    || ResearchQuery.IsCompleted(session.World, LaborOrderFactory.PlayerEmpire, root));
        if (!ResearchQuery.IsCompleted(session.World, LaborOrderFactory.PlayerEmpire, root)) Assert.Equal(root, target);
        Assert.True(ResearchQuery.Progress(session.World, LaborOrderFactory.PlayerEmpire, root) > 0.0
                    || ResearchQuery.IsCompleted(session.World, LaborOrderFactory.PlayerEmpire, root));
    }
}
