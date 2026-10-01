using Sim.Core.Kernel;
using Sim.Core.State;
using Sim.Core.Systems.Research;
using static Sim.Tests.TestUtil.ResearchRigs;

namespace Sim.Tests.Systems;

/// <summary>
/// The Eureka credit model of the Director ruling of 2026-10-01 §5–§6 (ADR-029 §7): a node
/// has ONE Eureka; its strings are its conditions; a fully satisfied Eureka credits EXACTLY
/// 40 % of BASE cost; each condition credits its normalized share, once; credit lands on
/// its own node, is capped at the remaining cost and never overflows. Node x (key 12,
/// requires a, base cost 100) carries the conditions; goods: timber 4, stone 5, clay 6,
/// fiber 9. Every expected value is exact.
/// </summary>
public class ResearchEurekaCreditTests
{
    private static readonly PolityId P1 = new(Player);
    private const string Timber = "stock_timber > 0", Stone = "stock_stone > 0", Clay = "stock_clay > 0", Fiber = "stock_fiber > 0";

    private static ResearchContent WithX(params Eu[] eurekas)
    {
        Spec spec = Standard();
        spec.Technologies.Add(new Node(12, "x", "a", Cost: 100.0, Eurekas: eurekas));
        return spec.Load();
    }

    private static WorldState Known(params (int Good, long Qty)[] stocks) => WithCompleted(PlayerWorld(stocks), 1);

    // A fresh copy: the executor double-buffers, so a returned world is never mutated in place.
    private static WorldState AddStock(WorldState w, int good) => Stock(w.Clone(), 0, good, 10);

    private static int RowsOnX(WorldState w)
    {
        int rows = 0;
        for (int i = 0; i < w.ResearchEurekas.Count; i++) if (w.ResearchEurekas[i].Node == Key(12)) rows++;
        return rows;
    }

    [Fact]
    public void Single_AFullySatisfiedOneConditionEureka_CreditsExactly40PercentOfBaseCost()
    {
        ResearchContent content = WithX(new Eu("circumstance: timber", Timber));
        WorldState w1 = Executor(content).Step(Known((4, 50)));
        Assert.Equal(40.0, Progress(w1, 12));
        Assert.Equal(new ResearchQuery.EurekaProgress(1, 1, 1.0, 1.0), ResearchQuery.EurekaProgressOf(w1, content, P1, Key(12)));
    }

    [Fact]
    public void Partial_FourEqualConditions_Credit10Each_AsEachIsNewlySatisfied_AndTheLastReachesExactly40()
    {
        ResearchContent content = WithX(
            new Eu("circumstance: timber", Timber), new Eu("circumstance: stone", Stone),
            new Eu("circumstance: clay", Clay), new Eu("circumstance: fiber", Fiber));
        TurnExecutor ex = Executor(content);
        WorldState w = ex.Step(Known((4, 50)));                     // 1/4
        Assert.Equal(10.0, Progress(w, 12));
        w = ex.Step(w);                                              // still 1/4: the held condition does not pay again
        Assert.Equal(10.0, Progress(w, 12));
        w = ex.Step(AddStock(w, 5));                                 // 2/4
        Assert.Equal(20.0, Progress(w, 12));
        w = ex.Step(AddStock(w, 6));                                 // 3/4
        Assert.Equal(30.0, Progress(w, 12));
        w = ex.Step(AddStock(w, 9));                                 // 4/4
        Assert.Equal(40.0, Progress(w, 12));
        Assert.Equal(new ResearchQuery.EurekaProgress(4, 4, 1.0, 1.0), ResearchQuery.EurekaProgressOf(w, content, P1, Key(12)));
        w = ex.Step(w);                                              // every condition still holds: nothing more, ever
        Assert.Equal(40.0, Progress(w, 12));
        Assert.Equal(4, RowsOnX(w));
    }

    [Fact]
    public void Partial_ConditionsSatisfiedTogether_StillCreditOncePerCondition_AndSumToExactly40()
    {
        ResearchContent content = WithX(
            new Eu("circumstance: timber", Timber), new Eu("circumstance: stone", Stone),
            new Eu("circumstance: clay", Clay), new Eu("circumstance: fiber", Fiber));
        WorldState w1 = Executor(content).Step(Known((4, 1), (5, 1), (6, 1), (9, 1)));
        Assert.Equal(40.0, Progress(w1, 12));
        Assert.Equal(4, RowsOnX(w1));
    }

    [Fact]
    public void MultiCondition_AStringWithParts_SplitsItsShareEqually_AcrossIndependentlySatisfiableParts()
    {
        // Two strings, equal weight: the B string's half splits into two quarters (10 each);
        // the A string is the other half (20).
        ResearchContent content = WithX(
            new Eu("circumstance: timber and stone", null, Parts: [Timber, Stone]), new Eu("circumstance: clay", Clay));
        ResearchEureka b = content.Nodes[content.IndexOfId("x")].Eurekas[0];
        Assert.Equal(EurekaCategory.MultiCondition, b.Category);
        Assert.Equal(2, b.Conditions.Count);
        TurnExecutor ex = Executor(content);
        WorldState w = ex.Step(Known((5, 1)));                       // stone: one part of the B string
        Assert.Equal(10.0, Progress(w, 12));
        Assert.True(ResearchQuery.EurekaFired(w, P1, Key(12), 0, 1));
        Assert.False(ResearchQuery.EurekaFired(w, P1, Key(12), 0, 0));
        w = ex.Step(AddStock(AddStock(w, 4), 6));                    // timber and clay
        Assert.Equal(40.0, Progress(w, 12));
    }

    [Fact]
    public void Weights_AreNormalizedDeterministically_AndNeverExceedTheFull40()
    {
        ResearchContent content = WithX(new Eu("circumstance: timber", Timber, Weight: 3.0), new Eu("circumstance: stone", Stone, Weight: 1.0));
        ResearchEureka[] e = [.. content.Nodes[content.IndexOfId("x")].Eurekas];
        Assert.Equal(0.75, e[0].Share);
        Assert.Equal(0.25, e[1].Share);
        TurnExecutor ex = Executor(content);
        WorldState w = ex.Step(Known((4, 1)));
        Assert.Equal(30.0, Progress(w, 12));
        w = ex.Step(AddStock(w, 5));
        Assert.Equal(40.0, Progress(w, 12));
    }

    [Fact]
    public void FutureSystemAndAuthoringStrings_HoldTheirShare_SoTheEvaluablePartAloneCannotReach40()
    {
        // One evaluable string and one C string: the C string keeps its half until a future
        // system can evaluate it. Implied (D) and dead (E) strings are not circumstances: no share.
        ResearchContent content = WithX(
            new Eu("circumstance: timber", Timber), new Eu("circumstance: gold", null, "no-state-carrier"),
            new Eu("circumstance: a", null, "implied-by-prerequisites"));
        ResearchEureka[] e = [.. content.Nodes[content.IndexOfId("x")].Eurekas];
        Assert.Equal(0.5, e[0].Share);
        Assert.Equal(0.5, e[1].Share);
        Assert.Equal(0.0, e[2].Share);
        WorldState w1 = Executor(content).Step(Known((4, 1)));
        Assert.Equal(20.0, Progress(w1, 12));
        Assert.Equal(new ResearchQuery.EurekaProgress(1, 1, 0.5, 0.5), ResearchQuery.EurekaProgressOf(w1, content, P1, Key(12)));
    }

    [Fact]
    public void Cap_CreditIsCappedAtTheRemainingCost_AndNeverOverflowsToAnotherNode()
    {
        ResearchContent content = WithX(new Eu("circumstance: timber", Timber), new Eu("circumstance: stone", Stone));
        WorldState w = Known((4, 1), (5, 1));
        w.ResearchProgress.Add(new ResearchProgressRow(P1, Key(12), 95.0));  // 5 remain; 40 would land
        WorldState w1 = Executor(content).Step(w);
        Assert.True(Done(w1, 12));
        Assert.Equal(0.0, Progress(w1, 12));                                 // the row is gone with the completion
        Assert.Equal(2, RowsOnX(w1));
        // Nothing spilled: the only other progress is w's own timber condition (500 of its 2 500).
        Assert.Equal(500.0, Progress(w1, 11));
        Assert.Single(w1.ResearchProgress.ToArrayForTest());
    }

    [Fact]
    public void Base_TheCreditIs40PercentOfBaseCost_EvenWhenAUniversityLowersTheEffectiveCost()
    {
        // Director ruling §5: "40 % of BASE technology cost". A university halves eng2's effective
        // cost (100 -> 50); the full Eureka is still 40, not 20.
        Spec spec = Standard();
        spec.Technologies.Add(new Node(12, "eng2", "a", Cost: 100.0, Branch: "engineering", Eurekas: [new Eu("circumstance: timber", Timber)]));
        ResearchContent content = spec.Load();
        WorldState w = WithCompleted(PlayerWorld([(4, 1)]), 1, 2, 3);
        w.ResearchCostModifiers.Add(new ResearchCostModifierRow(P1, 3, 0.5));
        WorldState w1 = Executor(content).Step(w);
        Assert.Equal(40.0, Progress(w1, 12));
        Assert.Equal(50.0, ResearchQuery.EffectiveCost(w1, content, P1, content.IndexOfId("eng2")));
    }

    [Fact]
    public void Independent_TheEurekaCreditsItsOwnNode_WhateverTheTarget()
    {
        ResearchContent content = WithX(new Eu("circumstance: timber", Timber));
        var orders = new OrderLog();
        orders.Append(Target(0, 2));                                         // target b
        WorldState w1 = Executor(content, orders).Step(Known((4, 1)));
        Assert.Equal(1000.0, Progress(w1, 2));
        Assert.Equal(40.0, Progress(w1, 12));
    }
}
