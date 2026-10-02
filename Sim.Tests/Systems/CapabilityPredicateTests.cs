using Sim.Core;
using Sim.Core.Kernel;
using Sim.Core.State;
using Sim.Core.Systems;
using Sim.Core.Systems.Research;
using Sim.Core.Worldgen;
using Sim.Tests.TestUtil;

namespace Sim.Tests.Systems;

/// <summary>
/// ADR-033 D2/D3 — ONE PREDICATE, TWO CALLERS. For every order kind the action surface offers, the
/// consuming system accepts the order on the SAME function the query lists it by, so the UI never offers an
/// order the simulation would reject and an order the UI never offered is rejected on the same rule:
/// kind 2/3 LabourActivities.CanAllocate (PathBuildSystem), kind 4 ConstructionQuery.IsProjectAvailable
/// (ConstructionSystem), kind 6 ResearchQuery.IsAvailable / AvailableMask (ResearchSystem), kind 7
/// AgeQuery.CheckAdvance (AgeTransitionSystem), kind 8 RoadDevelopmentQuery.Plan (RoadDevelopmentSystem).
/// Kind 4 is in ConstructionAvailabilityTests; kind 8 in AvailableActionsQueryTests and AiPolicyTests.
/// </summary>
public class CapabilityPredicateTests
{
    private static readonly SimConfig Cfg = TestConfigs.Sim();
    private static readonly ResearchContent Research = TestConfigs.Research();
    private static readonly PolityId Player = new(1);
    private static readonly PolityId Rival = new(2);

    private static readonly Lazy<WorldState> DevSolo = new(() => WorldFounding.Found(TestConfigs.DevWorldgen(), Cfg, 42));
    private static readonly Lazy<WorldState> DevDuo = new(() =>
        WorldFounding.Found(TestConfigs.DevWorldgen() with { AiEmpires = 1 }, Cfg, 42));

    private static WorldState Solo() => DevSolo.Value.Clone();
    private static WorldState Duo() => DevDuo.Value.Clone();

    private static void Complete(WorldState w, PolityId polity, params string[] nodeIds)
    {
        foreach (string id in nodeIds)
            w.ResearchCompleted.Add(new ResearchCompletedRow(polity, Research.Nodes[Research.IndexOfId(id)].Key));
    }

    private static void TopUp(WorldState w, SettlementId s, string good, long target)
    {
        var id = new GoodId(Cfg.Goods!.IdOf(good));
        int idx = GoodStockIndex.IndexOf(w.GoodStocks, s, id);
        if (idx < 0) idx = w.GoodStocks.Add(new GoodStockRow(s, id, Conserved.Zero, 0.0, 0.0));
        long have = w.GoodStocks[idx].Amount.Value;
        if (have >= target) return;
        new Ledger(w.LedgerFlows).Flow(ref w.GoodStocks.Ref(idx).Amount, ConservedQuantityIds.OfGood(id),
            ReasonIds.InitialEndowment, target - have, FlowDirection.Source, OverdrawPolicy.Throw);
    }

    private static TurnExecutor Only(OrderLog orders, params SystemRegistration[] systems) =>
        new(ResearchRigs.FlatEra(10.0), systems, orders);

    // ------------------------------------------------------------------ kinds 2 and 3 — labour

    [Fact]
    public void LabourOrders_ApplyOnlyWhereTheIssuerRules_OnTheSamePredicateTheSurfaceListsBy()
    {
        WorldState w = Duo();
        SettlementId mine = w.Settlements[0].Id, theirs = w.Settlements[1].Id;
        Assert.True(LabourActivities.CanAllocate(w, Player, mine));
        Assert.False(LabourActivities.CanAllocate(w, Player, theirs));
        Assert.DoesNotContain(AvailableActionsQuery.For(w, Cfg, Player),
            a => a.Domain == ActionDomain.Labour && a.Targets[0].Id == theirs.Value);

        var orders = new OrderLog();
        orders.Append(OrderRecord.From(0, Player, OrderKind.SectorAllocation, LabourActivities.PackTarget(theirs, Sectors.Crafting), 100.0));
        orders.Append(OrderRecord.From(0, Player, OrderKind.LaborAllocation, theirs.Value, 30.0));
        orders.Append(OrderRecord.From(0, Player, OrderKind.SectorAllocation, LabourActivities.PackTarget(mine, Sectors.Crafting), 100.0));
        WorldState next = Only(orders, SystemCatalog.PathBuild(Cfg)).Step(w);
        Assert.Equal(Sectors.Default(theirs), LabourActivities.AllocationOf(next, theirs));   // ignored: not the issuer's
        Assert.Equal(1.0, LabourActivities.AllocationOf(next, mine).Crafting);              // applied: its own

        // The load-time pass rejects the same orders, by the same predicate, with an actionable message.
        foreach (OrderRecord bad in new[] { orders[0], orders[1] })
        {
            var log = new OrderLog();
            log.Append(bad);
            var e = Assert.Throws<OrderValidationException>(() => OrderValidation.ValidateAgainstWorld(log, w));
            Assert.Contains("does not control", e.Message, StringComparison.Ordinal);
        }
        var good = new OrderLog();
        good.Append(orders[2]);
        OrderValidation.ValidateAgainstWorld(good, w);
    }

    // ------------------------------------------------------------------ kind 6 — research

    [Fact]
    public void SetResearchTarget_TheOrderBuilderAndTheSystemRefuseTheSameNodes()
    {
        WorldState w = Solo();
        ResearchNodeId cereal = Research.Nodes[Research.IndexOfId("cereal_cultivation")].Key;     // locked: needs grinding_stone
        ResearchNodeId grinding = Research.Nodes[Research.IndexOfId("grinding_stone")].Key;       // available root
        Assert.Null(ResearchQuery.TargetOrder(w, Research, Player, cereal));
        Assert.NotNull(ResearchQuery.TargetOrder(w, Research, Player, grinding));
        ActionDescriptor set = AvailableActionsQuery.For(w, Cfg, Player).Single(a => a.Key == "research.set-target");
        Assert.DoesNotContain(set.Targets, t => t.Id == cereal.Value);
        Assert.Contains(set.Targets, t => t.Id == grinding.Value);

        // A raw order for the locked node changes nothing; the built order for the available one sets it.
        var orders = new OrderLog();
        orders.Append(OrderRecord.From(0, Player, OrderKind.SetResearchTarget, cereal.Value, 0.0));
        WorldState ignored = Only(orders, SystemCatalog.Research(Cfg)).Step(w);
        Assert.False(ResearchQuery.TryGetTarget(ignored, Player, out _));
        var orders2 = new OrderLog();
        orders2.Append(ResearchQuery.TargetOrder(w, Research, Player, grinding)!.Value);
        WorldState set2 = Only(orders2, SystemCatalog.Research(Cfg)).Step(w);
        Assert.True(ResearchQuery.TryGetTarget(set2, Player, out ResearchNodeId target));
        Assert.Equal(grinding, target);
    }

    // ------------------------------------------------------------------ kind 7 — Age advance

    [Fact]
    public void AdvanceAge_TheOfferedOrderIsTheOneTheTransitionApplies_NextTurn()
    {
        WorldState w = Solo();
        Complete(w, Player, "cereal_cultivation", "pottery_open_fired");
        ActionDescriptor offer = AvailableActionsQuery.For(w, Cfg, Player).Single(a => a.Domain == ActionDomain.Age);
        var orders = new OrderLog();
        orders.Append(AgeQuery.AdvanceOrder(w, Player, (int)offer.Id, (int)offer.Targets[0].Id));
        Assert.Equal(AdvanceRejection.None, AgeQuery.CheckAdvance(w, TestConfigs.Ages(), orders[0]));
        WorldState next = Only(orders, SystemCatalog.AgeEligibility(Cfg), SystemCatalog.AgeTransition(Cfg)).Step(w);
        Assert.Equal(2, AgeQuery.CurrentAge(next, TestConfigs.Ages(), Player));
        Assert.Empty(AvailableActionsQuery.For(next, Cfg, Player).Where(a => a.Domain == ActionDomain.Age));   // A3 not eligible
    }
}
