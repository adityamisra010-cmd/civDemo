using System.Collections.Immutable;
using Sim.Core;
using Sim.Core.Kernel;
using Sim.Core.State;
using Sim.Core.Systems;
using Sim.Core.Systems.Research;
using Sim.Core.Worldgen;
using Sim.Tests.TestUtil;

namespace Sim.Tests.State;

/// <summary>
/// R2a — TRADE IS A RESEARCH UNLOCK (Director decisions 1, 13, 15 RESEARCH). The gate is content: research.json node
/// `trade` → entity `activity.trade` → sim.json trade.entity; the ONE predicate is TradeQuery (TradeArbitrageSystem
/// applies CanTrade per pair on PREV; AvailableActionsQuery lists KnowsTrade). Trade has no order of its own (the
/// existing arbitrage moves goods automatically once legal), so "initiation" is the research directive, and a
/// hand-built directive naming an unavailable Trade node is refused by ResearchSystem.
/// </summary>
public class TradeResearchUnlockTests
{
    private static readonly SimConfig Cfg = TestConfigs.Sim();
    private static readonly ResearchContent Research = TestConfigs.Research();
    private static readonly PolityId Player = new(1);

    private static readonly Lazy<WorldState> DevSolo = new(() => WorldFounding.Found(TestConfigs.DevWorldgen(), Cfg, 42));
    private static readonly Lazy<WorldState> DevDuo = new(() => WorldFounding.Found(TestConfigs.DevWorldgen() with { AiEmpires = 1 }, Cfg, 42));

    /// <summary>A founded dev world whose realm knows its crafts (goods to trade — R1), not yet trade.</summary>
    private static WorldState Crafting() => TestConfigs.KnowRecipes(DevSolo.Value.Clone(), Cfg);

    private static WorldState Know(WorldState w, PolityId polity, params string[] nodeIds)
    {
        foreach (string id in WithAncestors(nodeIds))
        {
            ResearchNodeId key = Research.Nodes[Research.IndexOfId(id)].Key;
            if (!ResearchQuery.IsCompleted(w, polity, key)) w.ResearchCompleted.Add(new ResearchCompletedRow(polity, key));
        }
        return w;
    }

    private static string[] WithAncestors(params string[] nodeIds)
    {
        var seen = new bool[Research.Nodes.Count];
        var stack = new Stack<int>();
        foreach (string id in nodeIds) stack.Push(Research.IndexOfId(id));
        while (stack.Count > 0)
        {
            int i = stack.Pop();
            if (seen[i]) continue;
            seen[i] = true;
            foreach (int p in Research.Nodes[i].PrerequisiteNodes) stack.Push(p);
        }
        var result = new List<string>();
        for (int i = 0; i < seen.Length; i++) if (seen[i]) result.Add(Research.Nodes[i].Id);
        return [.. result];
    }

    private static TurnExecutor Executor(OrderLog? log = null)
    {
        using var era = Sim.Data.DataFiles.OpenEraPacing();
        using var pipe = Sim.Data.DataFiles.OpenPipeline();
        return new TurnExecutor(EraTableLoader.Load(era), PipelineLoader.Load(pipe, SystemCatalog.All(Cfg, TestConfigs.Worldgen())), log);
    }

    private static WorldState Step(WorldState w, params OrderRecord[] orders)
    {
        var log = new OrderLog();
        foreach (OrderRecord o in orders) log.Append(o);
        return Executor(log).Step(w);
    }

    /// <summary>Total units moved by trade over <paramref name="turns"/> order-free turns.</summary>
    private static long TradedOver(ref WorldState w, int turns)
    {
        TurnExecutor exec = Executor();
        long moved = 0;
        for (int t = 0; t < turns; t++)
        {
            w = exec.Step(w);
            for (int i = 0; i < w.TradeFlows.Count; i++) moved += w.TradeFlows[i].Quantity;
        }
        return moved;
    }

    private static ImmutableArray<ActionDescriptor> Actions(IReadOnlyWorldState w, PolityId p) => AvailableActionsQuery.For(w, Cfg, p);
    private static bool ListsTrade(IReadOnlyWorldState w, PolityId p) => Actions(w, p).Any(a => a.Domain == ActionDomain.Trade);
    private static ResearchNodeId TradeKey => Research.Nodes[Research.IndexOfId("trade")].Key;

    [Fact]
    public void T01_Trade_UnavailableBeforeResearch_QueryPredicateAndSimulation()
    {
        WorldState w = Crafting();
        Assert.False(TradeQuery.KnowsTrade(w, Cfg, Player));
        Assert.False(ListsTrade(w, Player));
        Assert.False(TradeQuery.CanTrade(w, Cfg, w.Settlements[0].Id, w.Settlements[1].Id));
        Assert.Equal(0, TradedOver(ref w, 30));   // crafted goods exist, price gaps exist — nothing moves
    }

    [Fact]
    public void T02_Trade_AvailableAfterResearch_ListedAndGoodsMove()
    {
        WorldState w = Know(Crafting(), Player, "trade");
        Assert.True(TradeQuery.KnowsTrade(w, Cfg, Player));
        ActionDescriptor a = Actions(w, Player).Single(x => x.Domain == ActionDomain.Trade);
        Assert.Equal(ActionKind.Standing, a.Kind);
        Assert.Equal("Trade", a.Label);
        Assert.Contains("activity.trade", a.Provenance.Entities);
        Assert.Contains("Trade", a.Provenance.NodeNames);
        Assert.Equal(w.Settlements.Count, a.Targets.Length);
        Assert.True(TradedOver(ref w, 30) > 0, "the existing TradeArbitrageSystem moved nothing once trade was known");
    }

    [Fact]
    public void T03_Trade_PrerequisiteChain_IsEnforcedOnTheRealGraph()
    {
        int node = Research.IndexOfId("trade");
        int entity = Research.EntityIndexOf("activity.trade");
        bool[] Mask(params string[] ids)
        {
            var m = new bool[Research.Nodes.Count];
            foreach (string id in WithAncestors(ids)) m[Research.IndexOfId(id)] = true;
            return m;
        }
        Assert.Equal("token_counting AND (donkey OR camel OR sail_square)", Research.Nodes[node].Prerequisite!.Source);
        Assert.False(ResearchQuery.PrerequisitesMet(Research, node, Mask("token_counting")));   // counting without a carrier
        Assert.False(ResearchQuery.PrerequisitesMet(Research, node, Mask("donkey")));           // a carrier without counting
        foreach (string carrier in new[] { "donkey", "camel", "sail_square" })
            Assert.True(ResearchQuery.PrerequisitesMet(Research, node, Mask("token_counting", carrier)), carrier);
        // The prerequisites alone do NOT grant the capability: the node itself must be completed.
        Assert.False(ResearchQuery.IsKnowledgeEligible(Research, entity, Mask("token_counting", "donkey")));
        Assert.True(ResearchQuery.IsKnowledgeEligible(Research, entity, Mask("trade")));
        // Placement (Director decision 13): Bronze-Age trunk technology, depth 6, cost from the banded model.
        Assert.Equal("A3", Research.Nodes[node].Age);
        Assert.Equal(ResearchTree.Technology, Research.Nodes[node].Tree);
        Assert.True(Research.Nodes[node].IsTrunk);
        Assert.Equal(880.0, Research.Nodes[node].BaseCost);
    }

    [Fact]
    public void T04_HandBuiltDirective_ForAnUnavailableTradeNode_IsRefused_AndNoTradeOccurs()
    {
        WorldState w = Step(Crafting());
        OrderRecord injected = OrderRecord.From(w.Clock.Turn, Player, OrderKind.SetResearchTarget, TradeKey.Value, 0.0);
        Assert.Null(ResearchQuery.TargetOrder(w, Research, Player, TradeKey));   // the constructor refuses it
        WorldState next = Step(w, injected);
        Assert.DoesNotContain(Enumerable.Range(0, next.ResearchTargets.Count), i => next.ResearchTargets[i].Node.Value == TradeKey.Value);
        Assert.Equal(0, next.TradeFlows.Count);
        Assert.False(ListsTrade(next, Player));
    }

    [Fact]
    public void T05_AiCannotBypassTheTradeGate()
    {
        var ai = new PolityId(2);
        WorldState w = TestConfigs.KnowRecipes(DevDuo.Value.Clone(), Cfg);
        TurnExecutor exec = Executor();
        for (int t = 0; t < 20; t++)
        {
            OrderRecord[] orders = AiOrders.For(w, Cfg);
            Assert.DoesNotContain(orders, o => o.Kind == OrderKind.SetResearchTarget && o.TargetId == TradeKey.Value
                && !ResearchQuery.AvailableMask(Research, ResearchQuery.CompletedMask(w, Research, ai))[Research.IndexOfId("trade")]);
            var log = new OrderLog();
            foreach (OrderRecord o in orders) log.Append(o);
            w = Executor(log).Step(w);
            Assert.False(TradeQuery.KnowsTrade(w, Cfg, ai));
            Assert.False(ListsTrade(w, ai));
            Assert.Equal(0, w.TradeFlows.Count);
        }
    }

    [Fact]
    public void T06_TradeAvailability_SurvivesSaveLoad()
    {
        WorldState w = Step(Know(Crafting(), Player, "trade"));
        using var buffer = new MemoryStream();
        Snapshot.Save(w, buffer);
        buffer.Position = 0;
        WorldState loaded = Snapshot.Load(buffer, w.Terrain);
        Assert.True(AvailableActionsQuery.Same(Actions(w, Player), Actions(loaded, Player)));
        Assert.True(ListsTrade(loaded, Player));
        WorldState a = Step(w), b = Step(loaded);
        Assert.Equal(WorldHash.ComputeHex(a), WorldHash.ComputeHex(b));
    }

    [Fact]
    public void T07_TradeAvailability_SurvivesReplay_TurnByTurn()
    {
        WorldState start = Know(Crafting(), Player, "token_counting", "donkey");
        // Researched through the normal directive: the live run and its replay agree every turn on the hash, the
        // flows, and the action surface (Trade appears on the same turn in both).
        OrderRecord target = ResearchQuery.TargetOrder(start, Research, Player, TradeKey)!.Value;
        var log = new OrderLog();
        log.Append(target);
        WorldState a = start.Clone(), b = start.Clone();
        TurnExecutor ea = Executor(log), eb = Executor(log);
        long appearedA = -1, appearedB = -1;
        for (int t = 0; t < 90; t++)   // MEASURED: the dev realm makes ~13.7 RP/turn; Trade costs 880
        {
            a = ea.Step(a);
            b = eb.Step(b);
            Assert.Equal(WorldHash.ComputeHex(a), WorldHash.ComputeHex(b));
            if (appearedA < 0 && ListsTrade(a, Player)) appearedA = a.Clock.Turn;
            if (appearedB < 0 && ListsTrade(b, Player)) appearedB = b.Clock.Turn;
        }
        Assert.True(appearedA > 0, "Trade never completed in 90 turns");
        Assert.Equal(appearedA, appearedB);
    }

    [Fact]
    public void T09_FormalTrade_NeedsTheCapabilityAtBothEndpoints()
    {
        WorldState w = Know(Crafting(), Player, "trade");
        SettlementId a = w.Settlements[0].Id, b = w.Settlements[1].Id;
        Assert.True(TradeQuery.CanTrade(w, Cfg, a, b));
        // Remove b from the realm: an uncontrolled settlement that has not learned trade cannot be traded with.
        var kept = new List<ControlRow>();
        for (int i = 0; i < w.Controls.Count; i++) if (w.Controls[i].Place != b) kept.Add(w.Controls[i]);
        w.Controls.Clear();
        foreach (ControlRow c in kept) w.Controls.Add(c);
        Assert.False(TradeQuery.CanTrade(w, Cfg, a, b));
        Assert.False(TradeQuery.CanTrade(w, Cfg, b, a));
    }
}
