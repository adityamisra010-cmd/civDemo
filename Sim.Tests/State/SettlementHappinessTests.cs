using Sim.Core.Kernel;
using Sim.Core.State;
using Sim.Core.Systems;
using Sim.Tests.TestUtil;

namespace Sim.Tests.State;

/// <summary>
/// T4.13 — DERIVED HAPPINESS, 0..100.
///
/// The properties under test are the ones that make happiness a READING rather
/// than a stock: it is bounded, it is a pure function of the world, and the only
/// way to move it is to move a condition. The boundary cases the packet
/// enumerates each get a row, and total deprivation gets two — one for the value
/// and one for the revolt predicate — because "0 is reachable" is precisely the
/// property a satisfactionFloor silently destroys.
/// </summary>
public class SettlementHappinessTests
{
    private static SimConfig Cfg() => TestConfigs.Sim();

    /// <summary>
    /// A settlement with people, a stated food deficit and a stated dwelling
    /// stock — nothing else. Built by hand so each factor is set independently
    /// and the aggregation is the only thing under test.
    /// </summary>
    private static WorldState World(double deficitRatio, long dwellings, long people = 600)
    {
        var w = new WorldState(1);
        var s = new SettlementId(0);
        w.Settlements.Add(new SettlementRow(s, 0, 0));

        // Conserved stocks are born at zero and enter ONLY through the Ledger
        // (law 1) — a test world is no exception, and sourcing them here keeps
        // the conservation identity intact for the auditor.
        var ledger = new Ledger(w.LedgerFlows);

        w.Buckets.Add(new BucketRow(
            s, new CultureId(0), new ReligionId(0), new ClassId(0), 0,
            Conserved.Zero, 0.0, 0.0, 0.0, 0.0));
        if (people > 0)
        {
            ledger.Flow(ref w.Buckets.Ref(0).Count, ConservedQuantityIds.Population,
                ReasonIds.InitialEndowment, people, FlowDirection.Source, OverdrawPolicy.Throw);
        }

        w.ConsumptionDeficits.Add(new ConsumptionDeficitRow(s, deficitRatio, people));

        w.Housing.Add(new HousingRow(s, Conserved.Zero, 0.0, 0.0, 0.0, 0.0));
        if (dwellings > 0)
        {
            ledger.Flow(ref w.Housing.Ref(0).Dwellings, ConservedQuantityIds.Dwellings,
                ReasonIds.InitialEndowment, dwellings, FlowDirection.Source, OverdrawPolicy.Throw);
        }

        return w;
    }

    private static SettlementId S => new(0);

    [Fact]
    public void AFullyProvidedSettlementIsAtTheTopOfTheScale()
    {
        // 600 people at 6 per dwelling = 100 dwellings houses everyone exactly.
        WorldState w = World(deficitRatio: 0.0, dwellings: 100);
        Assert.Equal(SettlementHappiness.Max, SettlementHappiness.Of(w, S, Cfg()), 9);
    }

    [Fact]
    public void TotalDeprivationIsEXACTLYZero_SoTheRevoltConditionIsReachable()
    {
        // The whole point. If the D-035-B floor leaked into the scale this would
        // read ~5 and `happiness == 0` would be a predicate that can never fire.
        WorldState w = World(deficitRatio: 1.0, dwellings: 0);
        Assert.Equal(0.0, SettlementHappiness.Of(w, S, Cfg()), 9);
        Assert.True(SettlementHappiness.IsRevoltReady(w, S, Cfg()));
    }

    [Fact]
    public void AWellProvidedSettlementIsNotRevoltReady()
    {
        // Anti-vacuity companion to the test above: the predicate must
        // distinguish, not merely return true.
        WorldState w = World(deficitRatio: 0.0, dwellings: 100);
        Assert.False(SettlementHappiness.IsRevoltReady(w, S, Cfg()));
    }

    [Theory]
    [InlineData(0.25)]
    [InlineData(0.50)]
    [InlineData(0.75)]
    public void AFoodShortageLowersHappinessMonotonically(double deficit)
    {
        WorldState fed = World(deficitRatio: 0.0, dwellings: 100);
        WorldState hungry = World(deficitRatio: deficit, dwellings: 100);

        double a = SettlementHappiness.Of(fed, S, Cfg());
        double b = SettlementHappiness.Of(hungry, S, Cfg());

        Assert.True(b < a, $"deficit {deficit} did not lower happiness ({b} vs {a})");
        Assert.InRange(b, 0.0, SettlementHappiness.Max);
    }

    [Fact]
    public void AHousingShortageLowersHappiness()
    {
        WorldState housed = World(deficitRatio: 0.0, dwellings: 100);
        WorldState crowded = World(deficitRatio: 0.0, dwellings: 25);   // capacity 150 of 600

        Assert.True(SettlementHappiness.Of(crowded, S, Cfg())
                  < SettlementHappiness.Of(housed, S, Cfg()));
    }

    [Fact]
    public void CombinedShortagesAreWorseThanEither_NonCompensatory()
    {
        // The property CES buys and a weighted sum would not: a full granary
        // must NOT buy off having nowhere to live.
        double both = SettlementHappiness.Of(World(0.5, 25), S, Cfg());
        double foodOnly = SettlementHappiness.Of(World(0.5, 100), S, Cfg());
        double houseOnly = SettlementHappiness.Of(World(0.0, 25), S, Cfg());

        Assert.True(both < foodOnly);
        Assert.True(both < houseOnly);
    }

    [Fact]
    public void SurplusFoodDoesNotCompensateForAbsentHousing()
    {
        // Sharper form of the same law, and the one that would catch a switch to
        // a weighted sum: a settlement with perfect food and NO dwellings must
        // stay far below a settlement that is merely somewhat short of both.
        double fedButUnhoused = SettlementHappiness.Of(World(0.0, 0), S, Cfg());
        double mildlyShortOfBoth = SettlementHappiness.Of(World(0.2, 80), S, Cfg());

        Assert.True(fedButUnhoused < mildlyShortOfBoth,
            $"compensation leaked in: unhoused {fedButUnhoused} >= mixed {mildlyShortOfBoth}");
    }

    [Fact]
    public void HappinessIsBoundedAndDeterministic()
    {
        foreach (double d in new[] { 0.0, 0.3, 0.7, 1.0, 2.0, -1.0 })
        foreach (long dw in new long[] { 0, 10, 100, 10_000 })
        {
            WorldState w = World(d, dw);
            double first = SettlementHappiness.Of(w, S, Cfg());
            double again = SettlementHappiness.Of(w, S, Cfg());

            Assert.InRange(first, 0.0, SettlementHappiness.Max);
            Assert.Equal(first, again);          // exact: a pure function of state
        }
    }

    [Fact]
    public void HappinessIsNotAStock_ItFollowsTheConditionWithNoMemory()
    {
        // The defining difference from the comfort-as-stock design this replaces.
        // Starve the settlement, then restore it: happiness must return to its
        // ORIGINAL value exactly, carrying nothing forward from the bad turn.
        WorldState good = World(0.0, 100);
        double before = SettlementHappiness.Of(good, S, Cfg());

        WorldState bad = World(1.0, 0);
        Assert.True(SettlementHappiness.Of(bad, S, Cfg()) < before);

        WorldState restored = World(0.0, 100);
        Assert.Equal(before, SettlementHappiness.Of(restored, S, Cfg()));
    }

    [Fact]
    public void AnEmptySettlementIsNotReportedAsDeprived()
    {
        // Nobody to feed and nobody to house: the factors are vacuous, and a
        // zero here would revolt every empty row in the world.
        var w = new WorldState(1);
        var s = new SettlementId(0);
        w.Settlements.Add(new SettlementRow(s, 0, 0));

        Assert.Equal(SettlementHappiness.Max, SettlementHappiness.Of(w, s, Cfg()), 9);
        Assert.False(SettlementHappiness.IsRevoltReady(w, s, Cfg()));
    }

    [Fact]
    public void TheFactorsAreExplainable_AndMatchTheirPrimarySignals()
    {
        // Happiness must be able to say WHY, not just how much.
        WorldState w = World(deficitRatio: 0.25, dwellings: 50);   // capacity 300 of 600
        Span<double> f = stackalloc double[SettlementHappiness.FactorCount];
        SettlementHappiness.Factors(w, S, Cfg(), f);

        Assert.Equal(0.75, f[(int)SettlementHappiness.Factor.Food], 9);
        Assert.Equal(0.50, f[(int)SettlementHappiness.Factor.Housing], 9);
    }
}
