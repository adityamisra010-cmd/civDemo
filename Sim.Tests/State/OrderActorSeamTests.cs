using Sim.Core.Kernel;
using Sim.Core.State;

namespace Sim.Tests.State;

/// <summary>
/// M4-B — the strategic actor in the order seam. These pin the DISTINCTION the
/// packet is about: an order's actor is the issuing Empire's PolityId, and the
/// command source is a separate dimension that names who supplied the intent.
/// Collapsing the two is the failure mode, so most of these assert that the two
/// dimensions move independently.
/// </summary>
public class OrderActorSeamTests
{
    [Fact]
    public void AnOrdersActorResolvesToThePolityId_AndIsTheSameIntOnTheWire()
    {
        // The typed view is a PROJECTION of the serialized int, not a parallel
        // identity — so the two can never disagree and there is nothing to sync.
        var order = OrderRecord.From(
            turn: 3, new PolityId(7), OrderKind.LaborAllocation, targetId: 2, amount: 50);

        Assert.Equal(7, order.ActorId);
        Assert.Equal(new PolityId(7), order.Actor);
        Assert.Equal(order.ActorId, order.Actor.Value);
    }

    [Fact]
    public void ActorIdentityIsNotDerivedFromCommandSource()
    {
        // Two Empires under the SAME command source stay different actors, and
        // one Empire keeps its identity when the command source changes. If actor
        // were a player/AI marker, one of these two would collapse.
        var world = new WorldState();
        world.Polities.Add(new PolityRow(new PolityId(7), CommandSource.Ai));
        world.Polities.Add(new PolityRow(new PolityId(12), CommandSource.Ai));

        var a = OrderRecord.From(1, new PolityId(7), OrderKind.LaborAllocation, 0, 50);
        var b = OrderRecord.From(1, new PolityId(12), OrderKind.LaborAllocation, 0, 50);
        Assert.NotEqual(a.Actor, b.Actor);

        // Same actor, command source flipped: identity is unmoved.
        world.Polities[0] = new PolityRow(new PolityId(7), CommandSource.Player);
        Assert.Equal(new PolityId(7), a.Actor);
        Assert.True(EmpireQuery.IsPlayerCommanded(world, a.Actor));
        Assert.False(EmpireQuery.IsPlayerCommanded(world, b.Actor));
    }

    [Fact]
    public void TwoDifferentPolitiesRemainDistinctOrderActors_ThroughSaveAndLoad()
    {
        // §10 — the distinction has to survive the wire, not just live in memory.
        var log = new OrderLog();
        log.Append(OrderRecord.From(1, new PolityId(1), OrderKind.LaborAllocation, 0, 40));
        log.Append(OrderRecord.From(1, new PolityId(2), OrderKind.LaborAllocation, 1, 60));

        using var ms = new MemoryStream();
        log.Save(ms);
        ms.Position = 0;
        OrderLog back = OrderLog.Load(ms);

        Assert.Equal(2, back.Count);
        Assert.Equal(new PolityId(1), back[0].Actor);
        Assert.Equal(new PolityId(2), back[1].Actor);
        Assert.NotEqual(back[0].Actor, back[1].Actor);
    }

    [Fact]
    public void ValidationRejectsAnOrderFromAnEmpireTheWorldDoesNotHave()
    {
        // Actor existence is world-dependent, so it belongs at this layer and not
        // at load (§5). A settlement must exist too, or the pre-existing target
        // check fires first and this would prove nothing.
        var world = new WorldState();
        world.Settlements.Add(new SettlementRow(new SettlementId(0), 0, 0));
        world.Polities.Add(new PolityRow(new PolityId(1), CommandSource.Player));

        var log = new OrderLog();
        log.Append(OrderRecord.From(0, new PolityId(99), OrderKind.LaborAllocation, 0, 50));

        OrderValidationException ex = Assert.Throws<OrderValidationException>(
            () => OrderValidation.ValidateAgainstWorld(log, world));
        Assert.Contains("99", ex.Message);
    }

    [Fact]
    public void ValidationAcceptsAnOrderFromARegisteredEmpire()
    {
        var world = new WorldState();
        world.Settlements.Add(new SettlementRow(new SettlementId(0), 0, 0));
        world.Polities.Add(new PolityRow(new PolityId(1), CommandSource.Player));

        var log = new OrderLog();
        log.Append(OrderRecord.From(0, new PolityId(1), OrderKind.LaborAllocation, 0, 50));

        OrderValidation.ValidateAgainstWorld(log, world);   // must not throw
    }

    [Fact]
    public void AnEmptyRosterLeavesTheActorCheckDormant_WhichIsWhyEveryExistingLogStillLoads()
    {
        // The limitation, pinned rather than left implicit: nothing seeds a roster
        // yet, so worlds with no registered Empire accept any actor. This test
        // exists so the day worldgen seeds one, the change in behaviour is visible
        // here rather than as a mystery failure in an unrelated fixture.
        var world = new WorldState();
        world.Settlements.Add(new SettlementRow(new SettlementId(0), 0, 0));
        Assert.Equal(0, world.Polities.Count);

        var log = new OrderLog();
        log.Append(OrderRecord.From(0, new PolityId(12345), OrderKind.LaborAllocation, 0, 50));

        OrderValidation.ValidateAgainstWorld(log, world);   // dormant: must not throw
    }

    [Fact]
    public void ControlAnswersWhetherTheIssuingEmpireCommandsTheTarget()
    {
        // §6: the primitive a permission check will ask. The answer comes from the
        // D-037 control relation, never from the actor id taken on trust — so an
        // Empire naming a settlement it does not control is answerable as false
        // rather than being implicitly true because it said so.
        var world = new WorldState();
        var mine = new PolityId(1);
        var theirs = new PolityId(2);
        world.Controls.Add(new ControlRow(mine, new SettlementId(10), 1.0));
        world.Controls.Add(new ControlRow(theirs, new SettlementId(20), 1.0));

        var order = OrderRecord.From(0, mine, OrderKind.LaborAllocation, 20, 50);

        Assert.True(EmpireQuery.ControlsSettlement(world, order.Actor, new SettlementId(10)));
        Assert.False(EmpireQuery.ControlsSettlement(world, order.Actor, new SettlementId(20)));
        Assert.True(EmpireQuery.ControlsSettlement(world, theirs, new SettlementId(20)));
    }

    [Fact]
    public void ControlAnswerDoesNotDependOnControlRowInsertionOrder()
    {
        // §11 — the derivation is a property of the control SET.
        var ascending = new WorldState();
        var descending = new WorldState();
        var polity = new PolityId(3);
        int[] places = [5, 6, 7];
        for (int i = 0; i < places.Length; i++)
        {
            ascending.Controls.Add(new ControlRow(polity, new SettlementId(places[i]), 1.0));
            descending.Controls.Add(
                new ControlRow(polity, new SettlementId(places[places.Length - 1 - i]), 1.0));
        }

        for (int i = 0; i < places.Length; i++)
        {
            var place = new SettlementId(places[i]);
            Assert.Equal(
                EmpireQuery.ControlsSettlement(ascending, polity, place),
                EmpireQuery.ControlsSettlement(descending, polity, place));
        }

        Assert.False(EmpireQuery.ControlsSettlement(ascending, polity, new SettlementId(99)));
        Assert.False(EmpireQuery.ControlsSettlement(descending, polity, new SettlementId(99)));
    }
}
