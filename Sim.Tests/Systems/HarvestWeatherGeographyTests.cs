using Sim.Core;
using Sim.Core.Kernel;
using Sim.Core.State;
using Sim.Core.Systems;
using Sim.Core.Worldgen;
using Sim.Tests.TestUtil;

namespace Sim.Tests.Systems;

/// <summary>
/// M5 integration R2b — WEATHER DISTANCE (Director decision 9; item 15 WEATHER). The harvest-weather
/// spatial correlation is a proxy for PHYSICAL proximity, so it reads the straight-line distance between
/// site cells and never the road-aware SettlementDistances (docs/m5-governing-loop-port.md §5 B4).
/// </summary>
public class HarvestWeatherGeographyTests
{
    private static SimConfig Cfg => TestConfigs.Sim();

    private static WorldState Founded() => WorldFounding.Found(TestConfigs.DevWorldgen(), Cfg, 42, 6);

    private static TurnExecutor WeatherOnly() =>
        UniversityRigs.Only(new OrderLog(), 10.0, SystemCatalog.HarvestWeather(Cfg));

    /// <summary>A world whose road network says something very different: every pairwise travel cost
    /// halved (as a fast road network would) and one pair made unreachable (+∞, as a severed route).</summary>
    private static WorldState WithOtherRoadTopology(WorldState w)
    {
        WorldState other = w.Clone();
        Assert.True(other.SettlementDistances.Count > 0, "the rig needs a computed distance table to perturb");
        for (int i = 0; i < other.SettlementDistances.Count; i++)
        {
            SettlementDistanceRow d = other.SettlementDistances[i];
            other.SettlementDistances[i] = d with { TravelCost = i == 0 ? double.PositiveInfinity : d.TravelCost * 0.5 };
        }
        return other;
    }

    private static WorldState WithDistances(WorldState w)
    {
        // The founded world has no distance rows until catchment's first step: run it once.
        TurnExecutor catchment = UniversityRigs.Only(new OrderLog(), 10.0, SystemCatalog.Catchment(Cfg));
        return catchment.Step(w);
    }

    [Fact]
    public void ChangingTheRoadTopology_LeavesTheWeatherInputsAndOutputsIdentical()
    {
        WorldState w = WithDistances(Founded());
        WorldState roads = WithOtherRoadTopology(w);
        Assert.False(WorldStates.TableEquals(w.SettlementDistances, roads.SettlementDistances));

        // The geographic input itself does not read the table.
        double kmPerCost = TransportQuery.KmPerCostUnit(w);
        for (int i = 0; i < w.Settlements.Count; i++)
            for (int j = 0; j < w.Settlements.Count; j++)
            {
                Assert.True(GeographicDistance.TryIdealGroundCostUnits(w, w.Settlements[i].SiteCell, w.Settlements[j].SiteCell, kmPerCost, out double a));
                Assert.True(GeographicDistance.TryIdealGroundCostUnits(roads, roads.Settlements[i].SiteCell, roads.Settlements[j].SiteCell, kmPerCost, out double b));
                Assert.Equal(a, b);
            }

        // ...and so the weather the system draws is bit-identical, step after step.
        WorldState x = w, y = roads;
        for (int t = 0; t < 5; t++)
        {
            x = WeatherOnly().Step(x);
            y = WeatherOnly().Step(y);
            Assert.True(WorldStates.TableEquals(x.HarvestWeather, y.HarvestWeather), $"weather diverged at step {t + 1}");
        }
    }

    [Fact]
    public void TheKernel_IsAFunctionOfGeography_NearerSettlementsShareMoreWeather()
    {
        WorldState w = Founded();
        double kmPerCost = TransportQuery.KmPerCostUnit(w);
        Assert.True(kmPerCost > 0.0);
        int a = w.Settlements[0].SiteCell, b = w.Settlements[1].SiteCell;
        Assert.True(GeographicDistance.TryIdealGroundCostUnits(w, a, b, kmPerCost, out double ab));
        Assert.Equal(GeographicDistance.Km(w.Terrain!, a, b) / kmPerCost, ab);
        Assert.True(GeographicDistance.TryIdealGroundCostUnits(w, a, a, kmPerCost, out double self));
        Assert.Equal(0.0, self);
        // Straight-line km equals ADR-032's road-length invariant over the same two sites.
        Assert.Equal(RoadDevelopmentQuery.GeographicKm(w, w.Settlements[0].Id, w.Settlements[1].Id, double.NaN),
            GeographicDistance.Km(w.Terrain!, a, b), 9);
    }

    [Fact]
    public void ATerrainLessToyWorld_KeepsItsHandWrittenTable()
    {
        WorldState toy = Founded().Clone();
        toy.Terrain = null;
        Assert.False(GeographicDistance.TryIdealGroundCostUnits(toy, 0, 1, 1.0, out _));
        // ...so the kernel falls back to the table there: halving a hand-written cost moves the weather.
        GovernanceRigs.SetDistance(toy, toy.Settlements[0].Id, toy.Settlements[1].Id, 10.0);
        GovernanceRigs.SetDistance(toy, toy.Settlements[1].Id, toy.Settlements[0].Id, 10.0);
        WorldState near = toy.Clone();
        GovernanceRigs.SetDistance(near, near.Settlements[0].Id, near.Settlements[1].Id, 1.0);
        GovernanceRigs.SetDistance(near, near.Settlements[1].Id, near.Settlements[0].Id, 1.0);
        Assert.False(WorldStates.TableEquals(WeatherOnly().Step(toy).HarvestWeather, WeatherOnly().Step(near).HarvestWeather));
    }

    [Fact]
    public void WeatherIsReplayDeterministic_OverTheFullPipeline()
    {
        WorldState a = WorldFounding.Found(TestConfigs.DevWorldgen(), Cfg, 7, 4);
        WorldState b = WorldFounding.Found(TestConfigs.DevWorldgen(), Cfg, 7, 4);
        TurnExecutor ex = UniversityRigs.Production(Cfg);
        for (int t = 0; t < 12; t++) { a = ex.Step(a); b = ex.Step(b); }
        Assert.True(WorldStates.TableEquals(a.HarvestWeather, b.HarvestWeather));
        Assert.Equal(WorldHash.ComputeHex(a), WorldHash.ComputeHex(b));
    }
}
