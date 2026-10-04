using Sim.Core.Kernel;
using Sim.Core.State;
using Sim.Core.Systems.Research;
using Sim.Ui.ViewModel;
using Xunit;

namespace Sim.Ui.Tests;

/// <summary>
/// ADR-033 D2 at the SESSION seam — "one predicate, two callers": every UI order emitter is guarded by the
/// SAME predicate the simulation applies when it consumes the order, so the UI never logs an order the
/// simulation would ignore, and a session log the UI wrote always passes replay validation. The hazard this
/// closes (stream S2's report): the labour emitter did not check control, so a click on an AI settlement was
/// logged, ignored live, and made replay / `sim inspect` validation throw.
/// </summary>
public class ActionEmitterGuardTests
{
    private static readonly PolityId Me = UiPlayer.Empire;

    private static UiSession Duo() => UiSession.Start(42, sizeOverridePx: 256, settlementsOverride: 4, aiEmpiresOverride: 1);

    private static int ForeignSettlement(IReadOnlyWorldState w)
    {
        for (int i = 0; i < w.Settlements.Count; i++)
            if (!EmpireQuery.ControlsSettlement(w, Me, w.Settlements[i].Id)) return w.Settlements[i].Id.Value;
        throw new InvalidOperationException("the AI empire holds no settlement");
    }

    private static WorldState Knowing(WorldState w, ResearchContent research, params string[] nodeIds)
    {
        WorldState c = w.Clone();
        foreach (string id in nodeIds) c.ResearchCompleted.Add(new ResearchCompletedRow(Me, research.Nodes[research.IndexOfId(id)].Key));
        return c;
    }

    [Fact]
    public void Labour_AndConstruction_AreRefusedForASettlementThePlayerDoesNotControl_AndTheLogReplays()
    {
        UiSession s = Duo();
        int foreign = ForeignSettlement(s.World);
        int mine = EmpireQuery.TryGetCapital(s.World, Me, out SettlementId cap) ? cap.Value : throw new InvalidOperationException();
        int granary = s.Config.Goods!.Projects![0].Id;

        // Refused by the predicates the systems apply (LabourActivities.CanAllocate; ConstructionQuery.IsProjectAvailable).
        Assert.False(LabourActivities.CanAllocate(s.World, Me, new SettlementId(foreign)));
        Assert.False(s.EmitSectorOrders([20, 20, 20, 20, 20], foreign));
        Assert.False(s.EmitConstructionOrder(foreign, granary));
        Assert.Equal(0, s.Orders.Count);

        // Accepted where the player rules: a project short of materials is LEGAL (it waits at the queue head).
        Assert.True(s.EmitSectorOrders([20, 20, 20, 20, 20], mine));
        Assert.True(s.EmitConstructionOrder(mine, granary));
        Assert.Equal(OrderKind.EnqueueConstruction, s.Orders[s.Orders.Count - 1].Kind);
        Assert.Equal(ConstructionQuery.EnqueueOrder(s.World, Me, new SettlementId(mine), granary), s.Orders[s.Orders.Count - 1]);
        Assert.False(s.EmitConstructionOrder(mine, 9999));   // goods.json defines no such project

        var hashes = new List<string>();
        for (int t = 0; t < 4; t++) { s.EndTurn(); hashes.Add(WorldHash.ComputeHex(s.World)); }
        bool queuedOrBuilt = ConstructionQuery.Built(s.World, new SettlementId(mine), granary) > 0;
        for (int i = 0; i < s.World.ConstructionQueue.Count; i++)
            if (s.World.ConstructionQueue[i].Settlement.Value == mine && s.World.ConstructionQueue[i].ProjectId == granary) queuedOrBuilt = true;
        Assert.True(queuedOrBuilt);   // the order was delivered and applied (queued, or already built)

        // The log the UI wrote (player + AI orders) passes replay validation and reproduces hash for hash.
        WorldState world = UiFounding.Found(42, 256, 4, aiEmpiresOverride: 1);
        OrderValidation.ValidateAgainstWorld(s.Orders, world);
        TurnExecutor exec = UiSession.BuildProductionExecutor(s.Orders);
        for (int t = 0; t < hashes.Count; t++)
        {
            world = exec.Step(world);
            Assert.Equal(hashes[t], WorldHash.ComputeHex(world));
        }
    }

    [Fact]
    public void Labour_IsRefusedForASettlementThatNoLongerObeys()
    {
        // A revolted settlement drops its control row (RevoltSystem); it no longer obeys, so the labour
        // control must refuse it exactly as PathBuildSystem would ignore the order.
        WorldState w = UiFounding.Found(42, 256, 4);
        SettlementId lost = w.Settlements[1].Id;
        var kept = new List<ControlRow>();
        for (int i = 0; i < w.Controls.Count; i++) if (w.Controls[i].Place != lost) kept.Add(w.Controls[i]);
        w.Controls.Clear();
        foreach (ControlRow row in kept) w.Controls.Add(row);
        UiSession s = UiSession.StartFrom(w, 42, 256, 4);
        Assert.False(s.EmitSectorOrders([20, 20, 20, 20, 20], lost.Value));
        Assert.Equal(0, s.Orders.Count);
        Assert.True(s.EmitSectorOrders([20, 20, 20, 20, 20], w.Settlements[0].Id.Value));
    }

    [Fact]
    public void Roads_AreRefusedUntilAClassIsKnown_OnceATurn_AndOnlyInRange()
    {
        UiSession fresh = UiSession.Start(42, sizeOverridePx: 256, settlementsOverride: 4);
        fresh.EndTurn();   // distances exist from turn 1
        Assert.False(fresh.EmitRoadOrder(50));   // no road class known: the selection is empty
        Assert.Equal(0, fresh.Orders.Count);

        ResearchContent research = fresh.Config.Research!;
        UiSession s = UiSession.StartFrom(Knowing(UiFounding.Found(42, 256, 4), research, "track_road"), 42, 256, 4);
        s.EndTurn();
        Assert.True(RoadOrderFactory.Preview(s.World, s.Config, Me, 50).Length > 0);
        Assert.False(s.EmitRoadOrder(0));
        Assert.False(s.EmitRoadOrder(100.5));
        Assert.False(s.EmitRoadOrder(double.NaN));
        Assert.True(s.EmitRoadOrder(50));
        Assert.Equal(RoadDevelopmentQuery.DevelopOrder(s.World, Me, 50), s.Orders[s.Orders.Count - 1]);
        int count = s.Orders.Count;
        Assert.False(s.EmitRoadOrder(30));   // the system applies only the first DevelopRoads per Empire per turn
        Assert.Equal(count, s.Orders.Count);
        s.EndTurn();
        Assert.True(s.EmitRoadOrder(30));    // a new turn, a new decision
    }

    [Fact]
    public void ClearResearch_IsRefusedWhenThereIsNothingToClear_AndOnceCleared()
    {
        UiSession s = UiSession.Start(42, sizeOverridePx: 256, settlementsOverride: 4);
        ResearchContent research = s.Config.Research!;
        Assert.False(s.ClearResearchTarget());   // no target, nothing chosen this turn
        Assert.Equal(0, s.Orders.Count);

        Assert.True(s.EmitResearchOrder(research.Nodes[research.IndexOfId("cordage")].Key));
        Assert.Equal(research.Nodes[research.IndexOfId("cordage")].Key.Value, s.QueuedResearchChoice());
        Assert.True(s.ClearResearchTarget());    // a clear after a choice made the same turn is a real decision
        Assert.Equal(-1, s.QueuedResearchChoice());
        Assert.False(s.ClearResearchTarget());   // already cleared this turn
        s.EndTurn();
        Assert.False(ResearchQuery.TryGetTarget(s.World, Me, out _));   // the LAST order of the turn won

        Assert.True(s.EmitResearchOrder(research.Nodes[research.IndexOfId("cordage")].Key));
        s.EndTurn();
        Assert.True(ResearchQuery.TryGetTarget(s.World, Me, out _));
        Assert.True(s.ClearResearchTarget());
        Assert.Equal(ResearchQuery.ClearTargetOrder(s.World, Me), s.Orders[s.Orders.Count - 1]);
        s.EndTurn();
        Assert.False(ResearchQuery.TryGetTarget(s.World, Me, out _));
    }

    [Fact]
    public void TheTaxEdict_IsRefusedUntilATaxationNodeIsKnown()
    {
        UiSession s = UiSession.Start(42, sizeOverridePx: 256, settlementsOverride: 4);
        Assert.False(s.EmitTaxOrder(20));
        ResearchContent research = s.Config.Research!;
        UiSession t = UiSession.StartFrom(Knowing(UiFounding.Found(42, 256, 4), research, "taxation"), 42, 256, 4);
        Assert.True(t.EmitTaxOrder(20));
        Assert.Equal(Governance.TaxOrder(t.World.Clock.Turn, Me, 20), t.Orders[0]);
    }
}
