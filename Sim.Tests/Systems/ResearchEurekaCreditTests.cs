using Sim.Core.Kernel;
using Sim.Core.State;
using Sim.Core.Systems.Research;
using static Sim.Tests.TestUtil.ResearchRigs;

namespace Sim.Tests.Systems;

/// <summary>
/// The acceleration-credit model (ADR-029 §7 and addendum A; D-045 §5–§6; finalization rulings 2–4):
/// <list type="bullet">
/// <item>each Eureka credits weight × BASE cost once (default weight 0.40 ÷ N), partial as its
///   conditions fire, independent of the target;</item>
/// <item>Eureka and foreign exposure draw on ONE pool per node: at most 40 % of BaseCost together,
///   and at most the remaining EffectiveCost; nothing overflows; provenance is kept per source;</item>
/// <item>EffectiveCost = max(0.20 × BaseCost, BaseCost × modifiers), and a node whose credits alone
///   reach it completes with no RP spent (credit-only completion is intended).</item>
/// </list>
/// Node x (key 12, requires a, base cost 100) carries the Eurekas; goods: timber 4, stone 5, clay 6,
/// fiber 9. Every expected value is exact.
/// </summary>
public class ResearchEurekaCreditTests
{
    private static readonly PolityId P1 = new(Player);
    private const string Timber = "stock_timber > 0", Stone = "stock_stone > 0", Clay = "stock_clay > 0", Fiber = "stock_fiber > 0";

    private static ResearchContent WithX(params Eu[] eurekas) => WithX(100.0, eurekas);

    private static ResearchContent WithX(double cost, params Eu[] eurekas)
    {
        Spec spec = Standard();
        spec.Technologies.Add(new Node(12, "x", "a", Cost: cost, Eurekas: eurekas));
        return spec.Load();
    }

    private static WorldState Known(params (int Good, long Qty)[] stocks) => WithCompleted(PlayerWorld(stocks), 1);

    // A fresh copy: the executor double-buffers, so a returned world is never mutated in place.
    private static WorldState AddStock(WorldState w, int good) => Stock(w.Clone(), 0, good, 10);

    private static WorldState Offer(WorldState w, double offered)
    {
        WorldState c = w.Clone();
        c.ResearchExposures.Clear();
        c.ResearchExposures.Add(new ResearchExposureRow(P1, Key(12), offered));
        return c;
    }

    private static int FiredOnX(WorldState w)
    {
        int rows = 0;
        for (int i = 0; i < w.ResearchEurekas.Count; i++) if (w.ResearchEurekas[i].Node == Key(12)) rows++;
        return rows;
    }

    private static double Eureka(WorldState w) => ResearchQuery.CreditedBySource(w, P1, Key(12), AccelerationSource.Eureka);
    private static double Exposure(WorldState w) => ResearchQuery.CreditedBySource(w, P1, Key(12), AccelerationSource.ForeignExposure);

    // ------------------------------------------------------------------ Eureka: 40 % of base, partial, idempotent

    [Fact]
    public void Single_AFullySatisfiedOneConditionEureka_CreditsExactly40PercentOfBaseCost()
    {
        ResearchContent content = WithX(new Eu("Timber is held", Timber));
        WorldState w1 = Executor(content).Step(Known((4, 50)));
        Assert.Equal(40.0, Progress(w1, 12));
        Assert.Equal(40.0, Eureka(w1));
        Assert.Equal(new ResearchQuery.EurekaProgress(1, 1, 1, 0.4, 0.4), ResearchQuery.EurekaProgressOf(w1, content, P1, Key(12)));
    }

    [Fact]
    public void Partial_FourEqualConditions_Credit10Each_AsEachIsNewlySatisfied_AndTheLastReachesExactly40()
    {
        ResearchContent content = WithX(
            new Eu("timber", Timber), new Eu("stone", Stone), new Eu("clay", Clay), new Eu("fiber", Fiber));
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
        Assert.Equal(40.0, Eureka(w));
        Assert.Equal(new ResearchQuery.EurekaProgress(4, 4, 4, 0.4, 0.4), ResearchQuery.EurekaProgressOf(w, content, P1, Key(12)));
        for (int t = 0; t < 20; t++) w = ex.Step(w);                 // every condition stays true for 20 turns: nothing more
        Assert.Equal(40.0, Progress(w, 12));
        Assert.Equal(4, FiredOnX(w));
    }

    [Fact]
    public void Partial_AwkwardShares_TheLastConditionStillLandsOnExactly40PercentOfBase()
    {
        // Three equal shares of 2 390 credited on three different turns: the full Eureka is EXACTLY
        // the pool ceiling, 0.4 × 2 390. (These three shares happen to sum exactly in binary; the
        // six-share test below is the case only the last-firing reconciliation makes exact.)
        ResearchContent content = WithX(2390.0, new Eu("timber", Timber), new Eu("stone", Stone), new Eu("clay", Clay));
        TurnExecutor ex = Executor(content);
        WorldState w = ex.Step(Known((4, 1)));
        w = ex.Step(AddStock(w, 5));
        w = ex.Step(AddStock(w, 6));
        Assert.Equal(0.4 * 2390.0, Eureka(w));
        Assert.Equal(0.4 * 2390.0, Progress(w, 12));
    }

    [Fact]
    public void Partial_SixEqualShares_TheNaiveSumMissesByAnUlp_TheReconciledLastShareLandsOnExactly40Percent()
    {
        // Six default shares of BaseCost 130: (0.4 / 6) × 130 added six times is 51.99999999999999 in
        // binary, not 52 (measured). Only the last-firing reconciliation against provenance — the
        // node's entitlement 0.4 × 130 minus what its Eurekas already credited — lands the full
        // Eureka on EXACTLY the pool ceiling (ADR-029 addendum A, R-23).
        ResearchContent content = WithX(130.0,
            new Eu("timber > 0", "stock_timber > 0"), new Eu("timber > 1", "stock_timber > 1"),
            new Eu("timber > 2", "stock_timber > 2"), new Eu("timber > 3", "stock_timber > 3"),
            new Eu("timber > 4", "stock_timber > 4"), new Eu("timber > 5", "stock_timber > 5"));
        WorldState w = Executor(content).Step(Known((4, 10)));
        Assert.Equal(6, FiredOnX(w));
        Assert.Equal(52.0, Eureka(w));
        Assert.Equal(52.0, Progress(w, 12));
    }

    [Fact]
    public void Partial_ConditionsSatisfiedTogether_StillCreditOncePerCondition_AndSumToExactly40()
    {
        ResearchContent content = WithX(
            new Eu("timber", Timber), new Eu("stone", Stone), new Eu("clay", Clay), new Eu("fiber", Fiber));
        WorldState w1 = Executor(content).Step(Known((4, 1), (5, 1), (6, 1), (9, 1)));
        Assert.Equal(40.0, Progress(w1, 12));
        Assert.Equal(4, FiredOnX(w1));
    }

    [Fact]
    public void Weights_AreExplicitFractionsOfBaseCost_AndTogetherNeverExceedTheCeiling()
    {
        ResearchContent content = WithX(new Eu("timber", Timber, Weight: 0.3), new Eu("stone", Stone, Weight: 0.1));
        TurnExecutor ex = Executor(content);
        WorldState w = ex.Step(Known((4, 1)));
        Assert.Equal(30.0, Progress(w, 12));
        w = ex.Step(AddStock(w, 5));
        Assert.Equal(40.0, Progress(w, 12));
        // A smaller total is allowed: weights 0.1 + 0.1 credit 20 and stop there.
        ResearchContent small = WithX(new Eu("timber", Timber, Weight: 0.1), new Eu("stone", Stone, Weight: 0.1));
        Assert.Equal(20.0, Progress(Executor(small).Step(Known((4, 1), (5, 1))), 12));
    }

    [Fact]
    public void FutureSystemEurekas_HoldTheirWeight_SoTheEvaluablePartAloneGivesItsOwnShare()
    {
        // One evaluable Eureka and one whose state belongs to a future system: equal weights 0.2 each.
        ResearchContent content = WithX(new Eu("timber", Timber), new Eu("an obsidian source in range", null));
        WorldState w1 = Executor(content).Step(Known((4, 1)));
        Assert.Equal(20.0, Progress(w1, 12));
        Assert.Equal(new ResearchQuery.EurekaProgress(1, 2, 1, 0.2, 0.4), ResearchQuery.EurekaProgressOf(w1, content, P1, Key(12)));
    }

    [Fact]
    public void Independent_TheEurekaCreditsItsOwnNode_WhateverTheTarget()
    {
        ResearchContent content = WithX(new Eu("timber", Timber));
        var orders = new OrderLog();
        orders.Append(Target(0, 2));                                         // target b
        WorldState w1 = Executor(content, orders).Step(Known((4, 1)));
        Assert.Equal(1000.0, Progress(w1, 2));
        Assert.Equal(40.0, Progress(w1, 12));
    }

    // ------------------------------------------------------------------ the shared pool, the cap, no overflow

    [Fact]
    public void NoOverflow_CreditIsCappedAtTheRemainingCost_AndNeverSpillsToAnotherNode()
    {
        ResearchContent content = WithX(new Eu("timber", Timber), new Eu("stone", Stone));
        WorldState w = Known((4, 1), (5, 1));
        w.ResearchProgress.Add(new ResearchProgressRow(P1, Key(12), 95.0));  // 5 remain; 40 would land
        WorldState w1 = Executor(content).Step(w);
        Assert.True(Done(w1, 12));
        Assert.Equal(5.0, Eureka(w1));                                       // provenance records what was PAID
        Assert.Equal(2, FiredOnX(w1));
        Assert.Equal(500.0, Progress(w1, 11));                               // w's own timber Eureka, 0.2 × 2 500 — nothing spilled from x
    }

    [Fact]
    public void SharedPool_ForeignExposureAndEureka_TogetherNeverExceed40PercentOfBase_ProvenanceKeptPerSource()
    {
        ResearchContent content = WithX(new Eu("timber", Timber));
        TurnExecutor ex = Executor(content);
        // Foreign exposure is offered first: 30 of the 40 pool.
        WorldState w = ex.Step(Offer(Known(), 30.0));
        Assert.Equal(30.0, Exposure(w));
        Assert.Equal(30.0, Progress(w, 12));
        // The Eureka fires later: it is worth 40, but only 10 of the pool is left.
        w = ex.Step(AddStock(w, 4));
        Assert.Equal(10.0, Eureka(w));
        Assert.Equal(40.0, Progress(w, 12));
        ResearchQuery.AccelerationPool pool = ResearchQuery.AccelerationPoolOf(w, content, P1, Key(12));
        Assert.Equal((40.0, 10.0, 30.0, 0.0), (pool.Ceiling, pool.Eureka, pool.ForeignExposure, pool.Headroom));
        // More exposure offered later pays nothing: the pool is full.
        w = ex.Step(Offer(w, 80.0));
        Assert.Equal(30.0, Exposure(w));
        Assert.Equal(40.0, Progress(w, 12));
    }

    [Fact]
    public void ForeignExposureSeam_AnOfferIsPaidOnce_OnlyItsIncrementLater_FromTheSamePool()
    {
        ResearchContent content = WithX(new Eu("timber", Timber));
        TurnExecutor ex = Executor(content);
        WorldState w = ex.Step(Offer(Known(), 12.0));
        Assert.Equal(12.0, Exposure(w));
        w = ex.Step(w);                                                       // the same cumulative offer: paid already
        Assert.Equal(12.0, Exposure(w));
        w = ex.Step(Offer(w, 20.0));                                          // the offer grows: pay the 8 increment
        Assert.Equal(20.0, Exposure(w));
        Assert.Equal(20.0, Progress(w, 12));
        // The Eureka then fills the rest of the pool, not more: 20 of its 40.
        w = ex.Step(AddStock(w, 4));
        Assert.Equal(20.0, Eureka(w));
        Assert.Equal(40.0, Progress(w, 12));
        // No foreign-exposure source exists in the simulation: the seam is passive.
        Assert.Equal(0, Executor(content).Step(Known()).ResearchExposures.Count);
    }

    [Fact]
    public void FullEureka_IsFortyPercentOfBASE_AUniversityStyleModifierDoesNotShrinkIt()
    {
        // A university halves eng2's effective cost (100 → 50); the full Eureka is still 40, not 20.
        Spec spec = Standard();
        spec.Technologies.Add(new Node(12, "eng2", "a", Cost: 100.0, Branch: "engineering", Eurekas: [new Eu("timber", Timber)]));
        ResearchContent content = spec.Load();
        WorldState w = WithCompleted(PlayerWorld([(4, 1)]), 1, 2, 3);
        w.ResearchCostModifiers.Add(new ResearchCostModifierRow(P1, 3, 0.5));
        WorldState w1 = Executor(content).Step(w);
        Assert.Equal(40.0, Progress(w1, 12));
        Assert.Equal(50.0, ResearchQuery.EffectiveCost(w1, content, P1, content.IndexOfId("eng2")));
    }

    // ------------------------------------------------------------------ the floor and credit-only completion

    [Fact]
    public void Floor_NoModifierStackTakesEffectiveCostBelow20PercentOfBase()
    {
        Spec spec = Standard();
        spec.Technologies.Add(new Node(12, "eng2", "a", Cost: 100.0, Branch: "engineering"));
        ResearchContent content = spec.Load();
        WorldState w = WithCompleted(PlayerWorld(), 1, 2, 3);
        foreach (double f in new[] { 0.5, 0.5, 0.5, 0.5 }) w.ResearchCostModifiers.Add(new ResearchCostModifierRow(P1, 3, f));
        ResearchQuery.CostBreakdown b = ResearchQuery.EffectiveCostBreakdown(w, content, P1, Key(12));
        Assert.Equal((6.25, 20.0, 20.0, true), (b.Modified, b.Floor, b.EffectiveCost, b.FloorBinds));
        Assert.Equal(20.0, ResearchQuery.EffectiveCost(w, content, P1, content.IndexOfId("eng2")));
    }

    [Fact]
    public void CreditOnlyCompletion_WhenCreditCoversTheRemainingEffectiveCost_TheNodeCompletesWithNoRpSpent()
    {
        // eng2: base 100, modifiers take it to the 20 floor; its full Eureka (40) covers it. No target,
        // so no RP is spent at all — and the node completes this step, paid 20 (capped, no overflow).
        Spec spec = Standard();
        spec.Technologies.Add(new Node(12, "eng2", "a", Cost: 100.0, Branch: "engineering", Eurekas: [new Eu("timber", Timber)]));
        ResearchContent content = spec.Load();
        WorldState w = WithCompleted(PlayerWorld([(4, 1)]), 1, 2, 3);
        w.ResearchCostModifiers.Add(new ResearchCostModifierRow(P1, 3, 0.1));
        Assert.False(ResearchQuery.TryGetTarget(w, P1, out _));
        WorldState w1 = Executor(content).Step(w);
        Assert.True(Done(w1, 12));
        Assert.Equal(20.0, Eureka(w1));
        for (int r = 0; r < w1.ResearchProgress.Count; r++)                 // completion removed eng2's row
            Assert.NotEqual(Key(12), w1.ResearchProgress[r].Node);          // (w's own timber Eureka row remains)
    }
}
