using Sim.Core;
using Sim.Core.Kernel;
using Sim.Core.State;
using Sim.Core.Systems;
using Sim.Core.Systems.Ages;
using Sim.Core.Systems.Construction;
using Sim.Core.Systems.Research;
using Sim.Core.Systems.Roads;
using Sim.Core.Worldgen;
using Sim.Tests.TestUtil;
using static Sim.Tests.TestUtil.ResearchRigs;

namespace Sim.Tests.Systems;

/// <summary>
/// ADR-033 D5 — the AI acts through the SAME orders: AiOrders, the one deterministic producer, and its
/// policies (research, construction; the existing Age and road policies). Each policy's rule is pinned,
/// including the composite-key argmin over doubles with its tie-dense test (house rule), and that no order
/// is ever produced for a player-commanded polity or in a world without AI Empires.
/// </summary>
public class AiPolicyTests
{
    private static readonly SimConfig Cfg = TestConfigs.Sim();
    private static readonly ResearchContent Research = TestConfigs.Research();
    private static readonly PolityId Human = new(1);
    private static readonly PolityId Rival = new(2);

    private static readonly Lazy<WorldState> DevSolo = new(() => WorldFounding.Found(TestConfigs.DevWorldgen(), Cfg, 42));
    private static readonly Lazy<WorldState> DevDuo = new(() =>
        WorldFounding.Found(TestConfigs.DevWorldgen() with { AiEmpires = 1 }, Cfg, 42));

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

    private static TurnExecutor Production(OrderLog? orders = null)
    {
        using var eraStream = Sim.Data.DataFiles.OpenEraPacing();
        using var pipeStream = Sim.Data.DataFiles.OpenPipeline();
        return new TurnExecutor(EraTableLoader.Load(eraStream),
            PipelineLoader.Load(pipeStream, SystemCatalog.All(Cfg, TestConfigs.Worldgen())), orders);
    }

    // ------------------------------------------------------------------ research: composite key (tie-dense)

    [Fact]
    public void AiResearch_Cheapest_IsTheCompositeKey_EffectiveCostThenKey_TieDense()
    {
        // Five roots: a (2500), b, c, e (1250 each — a DENSE tie), d (625, behind a). A strictly lower cost
        // beats a lower key; among bit-equal costs the LOWER key wins, whatever else is available.
        var spec = new Spec { Stage = "a" };
        spec.Technologies.AddRange([
            new Node(1, "a"), new Node(2, "b", Cost: 1250.0), new Node(3, "c", Cost: 1250.0),
            new Node(4, "d", "a", Cost: 625.0), new Node(5, "e", Cost: 1250.0),
        ]);
        spec.Civics.Add(new Node(1001, "law", "a"));
        ResearchContent content = spec.Load();
        WorldState w = World([Player, 2], [Player, 2], 10_000);
        var polity = new PolityId(2);
        bool[] completed = ResearchQuery.CompletedMask(w, content, polity);
        bool[] available = ResearchQuery.AvailableMask(content, completed);
        Assert.Equal([true, true, true, false, true, false], available);   // a b c e roots; d and law wait on a

        Assert.Equal(content.IndexOf(new ResearchNodeId(2)), AiResearchPolicy.Cheapest(w, content, polity, available, null)); // b
        bool[] noB = [true, false, true, true, true, true];
        Assert.Equal(content.IndexOf(new ResearchNodeId(3)), AiResearchPolicy.Cheapest(w, content, polity, available, noB));   // c
        bool[] onlyE = [false, false, false, false, true, false];
        Assert.Equal(content.IndexOf(new ResearchNodeId(5)), AiResearchPolicy.Cheapest(w, content, polity, available, onlyE)); // e
        Assert.Equal(-1, AiResearchPolicy.Cheapest(w, content, polity, new bool[available.Length], null));

        // Once a is complete, d (625) is available and strictly cheapest, though its key is higher than b's.
        w.ResearchCompleted.Add(new ResearchCompletedRow(polity, new ResearchNodeId(1)));
        available = ResearchQuery.AvailableMask(content, ResearchQuery.CompletedMask(w, content, polity));
        Assert.Equal(content.IndexOf(new ResearchNodeId(4)), AiResearchPolicy.Cheapest(w, content, polity, available, null));
        // ...and the policy's order is the player's own order constructor's.
        // The rig content carries none of the canonical capability goals' node ids (road classes, the tax gate,
        // the university entities — ADR-033 B), so they are switched off: this test pins the argmin alone.
        SimConfig cfg = Cfg with { Research = content, Ages = null, Roads = null, Governance = null, Institutions = null };
        OrderRecord order = AiResearchPolicy.Decide(w, cfg, polity)!.Value;
        Assert.Equal(ResearchQuery.TargetOrder(w, content, polity, new ResearchNodeId(4)), order);
        Assert.Null(AiResearchPolicy.Decide(w, cfg, new PolityId(Player)));   // the human is never touched
    }

    [Fact]
    public void AiResearch_TargetsTheCheapestNodeOfItsGoalClosure_CoreAndCapabilityGoals_ThenAnythingCheapest()
    {
        // ADR-033 B: the goal closure is the next Age's core ancestors (A2: cereal_cultivation, so
        // {grinding_stone, cereal_cultivation}) UNION the ancestors of the requirements of the capability-gated
        // actions the AI uses (the next road class, the tax gate, the university founding). S2's core-only rule
        // is the first half and is unchanged.
        WorldState w = Duo();
        int grinding = Research.IndexOfId("grinding_stone"), cereal = Research.IndexOfId("cereal_cultivation");
        bool[] goal = AiResearchPolicy.CoreGoalClosure(w, Research, TestConfigs.Ages(), Rival);
        Assert.Equal([grinding, cereal], Enumerable.Range(0, goal.Length).Where(i => goal[i]).OrderBy(i => i));
        // Turn 0: grinding_stone (370) is still the cheapest available node of the union.
        Assert.Equal(Research.Nodes[grinding].Key.Value, AiResearchPolicy.Decide(w, Cfg, Rival)!.Value.TargetId);
        Assert.Null(AiResearchPolicy.Decide(w, Cfg, Human));

        // With grinding_stone known, the cheapest available node of the union is a CAPABILITY goal, not the core:
        // the 520-RP knapping pair, a bit-equal tie broken to the LOWER key (the composite key, tie-dense).
        Complete(w, Rival, "grinding_stone");
        bool[] completed = ResearchQuery.CompletedMask(w, Research, Rival);
        bool[] available = ResearchQuery.AvailableMask(Research, completed);
        bool[] union = AiResearchPolicy.GoalClosure(w, Cfg, Rival);
        int[] candidates = Enumerable.Range(0, available.Length).Where(i => available[i] && union[i]).ToArray();
        double min = candidates.Min(i => ResearchQuery.EffectiveCost(w, Research, Rival, i));
        int[] tied = candidates.Where(i => ResearchQuery.EffectiveCost(w, Research, Rival, i) == min).ToArray();
        Assert.True(tied.Length >= 2, "the canonical tie this pins has disappeared");
        int expected = tied.MinBy(i => Research.Nodes[i].Key.Value);
        Assert.Equal(Research.Nodes[expected].Key.Value, AiResearchPolicy.Decide(w, Cfg, Rival)!.Value.TargetId);
        Assert.False(goal[expected]);                                                   // not a core ancestor...
        Assert.True(AiResearchPolicy.CapabilityGoalClosure(w, Cfg, Rival)[expected]);   // ...a capability goal

        // A live target is kept (no order).
        w.ResearchTargets.Add(new ResearchTargetRow(Rival, Research.Nodes[cereal].Key));
        Assert.Null(AiResearchPolicy.Decide(w, Cfg, Rival));
        w.ResearchTargets.Clear();

        // With no goal left — core met, and no capability gate configured — the cheapest available node anywhere.
        Complete(w, Rival, "cereal_cultivation");
        SimConfig noGates = Cfg with { Roads = null, Governance = null, Institutions = null };
        Assert.DoesNotContain(true, AiResearchPolicy.CoreGoalClosure(w, Research, TestConfigs.Ages(), Rival));
        Assert.DoesNotContain(true, AiResearchPolicy.GoalClosure(w, noGates, Rival));
        ResearchNodeId? cheapest = ResearchQuery.CheapestAvailable(w, Research, Rival);
        Assert.Equal(cheapest!.Value.Value, AiResearchPolicy.Decide(w, noGates, Rival)!.Value.TargetId);
    }

    // ------------------------------------------------------------------ construction

    [Fact]
    public void AiConstruction_GranaryFirst_OnePerIdleSettlement_OnlyWhereAvailableAndAffordable()
    {
        WorldState w = Duo();
        SettlementId[] rivals = LabourActivities.ControlledSettlements(w, Rival);
        Assert.Equal(2, rivals.Length);
        SettlementId a = rivals[0], b = rivals[1];
        Assert.Empty(AiConstructionPolicy.Decide(w, Cfg, Rival));          // founding endows no timber or stone

        foreach (string good in new[] { "timber", "stone", "tools" }) TopUp(w, a, good, 1_000);
        OrderRecord granary = Assert.Single(AiConstructionPolicy.Decide(w, Cfg, Rival));
        Assert.Equal(ConstructionQuery.EnqueueOrder(w, Rival, a, 1), granary);   // the player's constructor, granary first
        Assert.Empty(AiConstructionPolicy.Decide(w, Cfg, Human));                 // the human is never touched

        // A settlement with a waiting project gets nothing more; one that built the granary moves on to the workshop.
        w.ConstructionQueue.Add(new ConstructionQueueRow(a, 0, 1));
        Assert.Empty(AiConstructionPolicy.Decide(w, Cfg, Rival));
        w.ConstructionQueue.Clear();
        w.Structures.Add(new StructureRow(a, 1, 1));
        Assert.Equal(ConstructionQuery.EnqueueOrder(w, Rival, a, 2), Assert.Single(AiConstructionPolicy.Decide(w, Cfg, Rival)));
        w.Structures.Add(new StructureRow(a, 2, 1));
        Assert.Empty(AiConstructionPolicy.Decide(w, Cfg, Rival));                 // each project once per settlement

        // Two idle, stocked settlements: one order each, settlement-table order.
        foreach (string good in new[] { "timber", "stone" }) TopUp(w, b, good, 1_000);
        Assert.Equal([ConstructionQuery.EnqueueOrder(w, Rival, b, 1)], AiConstructionPolicy.Decide(w, Cfg, Rival));
    }

    // ------------------------------------------------------------------ the producer

    [Fact]
    public void AiOrders_ProducesNothingWithoutAnAiEmpire_SoNoGoldenCanMove()
    {
        WorldState solo = DevSolo.Value.Clone();
        Assert.False(AiOrders.HasAiPolity(solo));
        Assert.Empty(AiOrders.For(solo, Cfg));
        Assert.Empty(AiOrders.For(Production().Run(solo, 3), Cfg));
    }

    [Fact]
    public void AiOrders_OnlyAiActors_StampedWithTheCurrentTurn_DeterministicAndAppendable()
    {
        WorldState w = Duo();
        foreach (string good in new[] { "timber", "stone" })
            foreach (SettlementId s in LabourActivities.ControlledSettlements(w, Rival)) TopUp(w, s, good, 1_000);
        OrderRecord[] first = AiOrders.For(w, Cfg), second = AiOrders.For(w, Cfg);
        Assert.Equal(first, second);
        Assert.NotEmpty(first);
        Assert.All(first, o => Assert.Equal((Rival.Value, w.Clock.Turn), (o.ActorId, o.Turn)));
        // Research first, then construction (one per idle stocked settlement), in roster/settlement order.
        Assert.Equal(OrderKind.SetResearchTarget, first[0].Kind);
        Assert.Equal(2, first.Count(o => o.Kind == OrderKind.EnqueueConstruction));
        var log = new OrderLog();
        Assert.Equal(first.Length, AiOrders.Append(log, w, Cfg));
        Assert.Equal(first.Length, log.Count);
        // An extinct AI Empire decides nothing.
        w.Controls.Clear();
        w.Controls.Add(new ControlRow(Human, w.Settlements[0].Id, 1.0));
        Assert.Empty(AiOrders.For(w, Cfg));
    }

    [Fact]
    public void AiRoads_OnceAClassIsKnownAndAffordable_TheAiDevelopsARoad_ThroughTheSameOrderAndSystem()
    {
        // The founded world needs one step for catchment to publish the cached travel costs.
        WorldState w = Production().Step(Duo());
        Complete(w, Rival, AncestorsOf("track_road"));
        foreach (SettlementId s in LabourActivities.ControlledSettlements(w, Rival)) TopUp(w, s, "timber", 5_000);
        OrderRecord[] orders = AiOrders.For(w, Cfg);
        OrderRecord roads = Assert.Single(orders, o => o.Kind == OrderKind.DevelopRoads);
        Assert.Equal(RoadDevelopmentPolicy.Decide(w, Cfg, Rival), roads);
        // The rival's surface offers the same action the AI took.
        Assert.Contains(AvailableActionsQuery.For(w, Cfg, Rival), a => a.Domain == ActionDomain.Roads && a.Blocker is null);

        var log = new OrderLog();
        foreach (OrderRecord o in orders) log.Append(o);
        WorldState next = Production(log).Step(w);
        Assert.Contains(Enumerable.Range(0, next.RoadDevelopments.Count).Select(i => next.RoadDevelopments[i]),
            r => r.Polity.Value == Rival.Value);
    }

    private static string[] AncestorsOf(string node)
    {
        var seen = new bool[Research.Nodes.Count];
        var stack = new Stack<int>();
        stack.Push(Research.IndexOfId(node));
        while (stack.Count > 0)
        {
            int i = stack.Pop();
            if (seen[i]) continue;
            seen[i] = true;
            foreach (int p in Research.Nodes[i].PrerequisiteNodes) stack.Push(p);
        }
        return Enumerable.Range(0, seen.Length).Where(i => seen[i]).Select(i => Research.Nodes[i].Id).ToArray();
    }
}
