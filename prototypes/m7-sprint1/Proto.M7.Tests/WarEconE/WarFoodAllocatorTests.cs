using Proto.M7.WarEcon;
using static Proto.M7.Tests.TestSupport;

namespace Proto.M7.Tests.WarEconE;

/// <summary>
/// Lane E: the automatic food priority (DIR5 §2.11: growth reduces first, movement is restricted before starvation,
/// starvation is the last resort) under BOTH food orderings, which the Director has not chosen between (DR27).
/// </summary>
public class WarFoodAllocatorTests
{
    private static MilitaryFoodRequest Req(int id, long s, long m, long r = 0, long w = 0, int prio = 0) => new(id, prio, s, m, r, w);

    [Fact]
    public void Ordering_HasNoDefault_ZeroIsRefused()
    {
        Assert.Throws<ArgumentOutOfRangeException>(() => WarFoodAllocator.Allocate(default, 100, 0, 0, Array.Empty<MilitaryFoodRequest>()));
        Assert.Throws<ArgumentOutOfRangeException>(() => WarFoodAllocator.Allocate((FoodOrdering)3, 100, 0, 0, Array.Empty<MilitaryFoodRequest>()));
    }

    /// <summary>
    /// CivilianMilitaryGrowth: sweep the granary from 0 to well above every demand. At every point the allocation conserves
    /// exactly, never touches the civilian reserve, and the cut order is exactly DIR5's tiers: growth is reduced while every
    /// standing and movement request is met (tier 1); movement is cut only when growth already gets nothing (tier 2); standing
    /// is cut only when every movement request gets nothing (tier 3, last resort).
    /// </summary>
    [Fact]
    public void CivilianMilitaryGrowth_CutOrderIsGrowthThenMovementThenStanding_OverAFullSweep()
    {
        const long civ = 1000, growth = 300;
        var reqs = new[] { Req(0, 200, 120), Req(1, 150, 80) };
        long allMil = 200 + 120 + 150 + 80;
        FoodStage last = FoodStage.Unconstrained;
        for (long free = civ + allMil + growth + 50; free >= 0; free--)
        {
            var a = WarFoodAllocator.Allocate(FoodOrdering.CivilianMilitaryGrowth, free, civ, growth, reqs);
            Assert.Equal(free, a.CivilianProtected + a.GrowthLeft + a.MilitaryGranted + a.Unallocated);
            Assert.Equal(Math.Min(free, civ), a.CivilianProtected);
            Assert.True(a.MilitaryGranted <= Math.Max(0, free - civ));
            long s = a.Grants.Sum(g => g.Standing), m = a.Grants.Sum(g => g.Movement);
            if (a.GrowthLeft > 0) { Assert.Equal(350, s); Assert.Equal(200, m); }                    // growth is cut first, while all military is met
            if (m < 200) Assert.Equal(0, a.GrowthLeft);                                              // movement cut => growth already 0
            if (s < 350) Assert.Equal(0, m);                                                         // standing cut => movement already 0
            Assert.True(a.Stage >= last, $"stage must only escalate as the granary shrinks (free {free})");
            last = a.Stage;
        }
        Assert.Equal(FoodStage.CivilianShortfall, last);
        // The exact thresholds: growth reduced below civ+mil+growth, movement below civ+standing+movement, standing below civ+standing.
        Assert.Equal(FoodStage.Unconstrained, WarFoodAllocator.Allocate(FoodOrdering.CivilianMilitaryGrowth, civ + allMil + growth, civ, growth, reqs).Stage);
        Assert.Equal(FoodStage.GrowthReduced, WarFoodAllocator.Allocate(FoodOrdering.CivilianMilitaryGrowth, civ + allMil + growth - 1, civ, growth, reqs).Stage);
        Assert.Equal(FoodStage.GrowthReduced, WarFoodAllocator.Allocate(FoodOrdering.CivilianMilitaryGrowth, civ + allMil, civ, growth, reqs).Stage);
        Assert.Equal(FoodStage.MovementRestricted, WarFoodAllocator.Allocate(FoodOrdering.CivilianMilitaryGrowth, civ + allMil - 1, civ, growth, reqs).Stage);
        Assert.Equal(FoodStage.MovementRestricted, WarFoodAllocator.Allocate(FoodOrdering.CivilianMilitaryGrowth, civ + 350, civ, growth, reqs).Stage);
        Assert.Equal(FoodStage.StandingShort, WarFoodAllocator.Allocate(FoodOrdering.CivilianMilitaryGrowth, civ + 349, civ, growth, reqs).Stage);
        Assert.Equal(FoodStage.CivilianShortfall, WarFoodAllocator.Allocate(FoodOrdering.CivilianMilitaryGrowth, civ - 1, civ, growth, reqs).Stage);
    }

    /// <summary>
    /// CivilianGrowthMilitary: growth is protected before the military. The measured consequence (evidence for Q3, not a
    /// choice): over the band civ+growth+standing &lt;= free &lt; civ+growth+standing+movement, movement is cut while growth
    /// keeps its whole demand, i.e. DIR5 tier 1 does not hold for the military draw under this interpretation.
    /// </summary>
    [Fact]
    public void CivilianGrowthMilitary_ProtectsGrowth_AndCutsMovementWhileGrowthIsWhole()
    {
        const long civ = 1000, growth = 300;
        var reqs = new[] { Req(0, 200, 120), Req(1, 150, 80) };
        for (long free = civ + 550 + growth + 50; free >= 0; free--)
        {
            var a = WarFoodAllocator.Allocate(FoodOrdering.CivilianGrowthMilitary, free, civ, growth, reqs);
            Assert.Equal(free, a.CivilianProtected + a.GrowthLeft + a.MilitaryGranted + a.Unallocated);
            Assert.Equal(Math.Min(growth, Math.Max(0, free - civ)), a.GrowthLeft);                   // growth before any military unit
            long s = a.Grants.Sum(g => g.Standing), m = a.Grants.Sum(g => g.Movement);
            if (s < 350) Assert.Equal(0, m);                                                         // standing still before movement
        }
        var band = WarFoodAllocator.Allocate(FoodOrdering.CivilianGrowthMilitary, civ + growth + 350 + 100, civ, growth, reqs);
        Assert.Equal(FoodStage.MovementRestricted, band.Stage);
        Assert.Equal(growth, band.GrowthLeft);
        Assert.Equal(100, band.Grants.Sum(g => g.Movement));
        // The same granary under the other ordering: growth is cut instead and every move is whole.
        var other = WarFoodAllocator.Allocate(FoodOrdering.CivilianMilitaryGrowth, civ + growth + 350 + 100, civ, growth, reqs);
        Assert.Equal(FoodStage.GrowthReduced, other.Stage);
        Assert.Equal(200, other.Grants.Sum(g => g.Movement));
        Assert.Equal(200, other.GrowthLeft);
    }

    [Fact]
    public void CivilianShortfall_MilitaryGetsNothing_UnderBothOrderings()
    {
        foreach (var o in new[] { FoodOrdering.CivilianMilitaryGrowth, FoodOrdering.CivilianGrowthMilitary })
        {
            var a = WarFoodAllocator.Allocate(o, 999, 1000, 10, new[] { Req(0, 1, 1, 1, 1) });
            Assert.Equal(FoodStage.CivilianShortfall, a.Stage);
            Assert.Equal(0, a.MilitaryGranted);
            Assert.Equal(1, a.CivilianShortfall);
            Assert.Equal(0, a.GrowthLeft);
        }
    }

    [Fact]
    public void RestockAndWagesInKind_ComeAfterGrowth_UnderBothOrderings()
    {
        var a = WarFoodAllocator.Allocate(FoodOrdering.CivilianMilitaryGrowth, 100 + 50 + 30, 100, 50, new[] { Req(0, 10, 10, 40, 40) });
        Assert.Equal((10L, 10L, 10L, 0L), (a.Grants[0].Standing, a.Grants[0].Movement, a.Grants[0].Restock, a.Grants[0].Wages));
        Assert.Equal(50, a.GrowthLeft);
        var b = WarFoodAllocator.Allocate(FoodOrdering.CivilianGrowthMilitary, 100 + 50 + 30, 100, 50, new[] { Req(0, 10, 10, 40, 40) });
        Assert.Equal((10L, 10L, 10L, 0L), (b.Grants[0].Standing, b.Grants[0].Movement, b.Grants[0].Restock, b.Grants[0].Wages));
    }

    /// <summary>Tie-dense: eight armies with EQUAL priority and EQUAL requests, presented in permuted orders. Within each
    /// tier they are served in ascending army id: a prefix gets everything, one army gets the remainder, the rest nothing,
    /// and the result is identical for every permutation (composite key (Priority, ArmyId)).</summary>
    [Fact]
    public void TieDense_EqualPrioritiesAndRequests_ServedByIdRegardlessOfInputOrder()
    {
        var baseReqs = Enumerable.Range(0, 8).Select(i => Req(i * 3 + 1, 100, 60)).ToArray();
        MilitaryFoodGrant[]? first = null;
        for (ulong seed = 1; seed <= 12; seed++)
        {
            var perm = Permutation(baseReqs.Length, seed);
            var input = perm.Select(i => baseReqs[i]).ToArray();
            var a = WarFoodAllocator.Allocate(FoodOrdering.CivilianMilitaryGrowth, 50 + 800 + 130, 50, 0, input);
            Assert.Equal(baseReqs.Select(r => r.ArmyId), a.Grants.Select(g => g.ArmyId));            // ascending id
            Assert.All(a.Grants, g => Assert.Equal(100, g.Standing));                               // standing for all first
            Assert.Equal(new long[] { 60, 60, 10, 0, 0, 0, 0, 0 }, a.Grants.Select(g => g.Movement).ToArray());
            first ??= a.Grants;
            Assert.Equal(first, a.Grants);
        }
        // Priority dominates id: a lower priority number is served first even with a larger id.
        var p = WarFoodAllocator.Allocate(FoodOrdering.CivilianMilitaryGrowth, 100, 0, 0, new[] { Req(1, 0, 100, prio: 5), Req(9, 0, 100, prio: 2) });
        Assert.Equal(new long[] { 100, 0 }, p.Grants.Select(g => g.Movement).ToArray());
        Assert.Equal(new[] { 9, 1 }, p.Grants.Select(g => g.ArmyId).ToArray());
    }
}
