using Proto.M7.Econ;
using Sim.Core.Pathing;
using Sim.Core.State;
using Sim.Core.Systems;

namespace Proto.M7.Econ.Tests;

/// <summary>Movement food: every parameter read from the shipped data; arithmetic pinned; terrain enters via the real lattice.</summary>
public class MovementFoodTests
{
    [Fact]
    public void Parameters_AreTheShippedValues_ReadNotTyped()
    {
        MovementParams p = MovementParams.From(EconWorld.SimCfg, Worlds.Turn1Seed42);
        Assert.Equal(1.0, p.RationGrainPerPersonYear);      // sim.json military.upkeepGrainPerPersonYear
        Assert.Equal(0.16, p.HaulGrainPerBulkCostUnit);     // sim.json trade.costPerBulkCostUnit
        Assert.Equal(1.0, p.GrainBulkPerUnit);              // goods.json grain bulkPerUnit
        Assert.Equal(40.0, p.BaselineKmPerDay);             // sim.json roads.baselineKmPerDay
        Assert.Equal(16.0, p.KmPerCostUnit);                // worldgen kmPerPx 4.0 x lattice stride 4
        Assert.Equal(360, p.DaysPerYear);                   // SimClock.YearDays
        Assert.Equal(0.2, p.RiverCostFactor);               // sim.json transport.riverCostFactor
    }

    [Fact]
    public void FromRouteCost_PinnedArithmetic()
    {
        MovementParams p = MovementParams.From(EconWorld.SimCfg, Worlds.Turn1Seed42);
        // 10 cost units = 160 km-eq = 4 days; 1,000 people eat ceil(1000 x 4/360) = ceil(11.11) = 12;
        // FULL haul ceil(12 x 1.0 x 10 x 0.16) = ceil(19.2) = 20; MEAN haul ceil(6 x 1.6) = ceil(9.6) = 10.
        MovementEstimate full = MovementFood.FromRouteCost(p, 5, 10.0, 100.0, 1_000, HaulBasis.FullLoad);
        MovementEstimate mean = MovementFood.FromRouteCost(p, 5, 10.0, 100.0, 1_000, HaulBasis.MeanLoad);
        Assert.Equal(160.0, full.RouteKmEquivalent);
        Assert.Equal(4.0, full.TravelDays);
        Assert.Equal(12, full.RationUnits);
        Assert.Equal(20, full.HaulUnits);
        Assert.Equal(32, full.TotalUnits);
        Assert.Equal(10, mean.HaulUnits);
        Assert.Equal(1.6, full.TerrainFactor, 12);
        Assert.Equal(full.HaulUnits, full.IncrementalUnits);   // rations are upkeep the soldier eats anyway
    }

    [Fact]
    public void ZeroPeopleOrZeroDistance_CostNothing()
    {
        MovementParams p = MovementParams.From(EconWorld.SimCfg, Worlds.Turn1Seed42);
        Assert.Equal(0, MovementFood.FromRouteCost(p, 1, 0.0, 0.0, 50_000, HaulBasis.FullLoad).TotalUnits);
        Assert.Equal(0, MovementFood.FromRouteCost(p, 9, 30.0, 300.0, 0, HaulBasis.FullLoad).TotalUnits);
    }

    [Fact]
    public void Bill_IsMonotoneInRouteCost_AndHaulGrowsFasterThanRations()
    {
        MovementParams p = MovementParams.From(EconWorld.SimCfg, Worlds.Turn1Seed42);
        long lastRations = 0, lastHaul = 0;
        for (int c = 1; c <= 200; c++)
        {
            MovementEstimate e = MovementFood.FromRouteCost(p, 2, c, 16.0 * c, 10_000, HaulBasis.FullLoad);
            Assert.True(e.RationUnits >= lastRations && e.HaulUnits >= lastHaul);
            lastRations = e.RationUnits; lastHaul = e.HaulUnits;
        }
        // Rations ∝ cost, haul ∝ rations × cost ∝ cost²: doubling the route (100 -> 200 units) roughly quadruples the haul.
        MovementEstimate a = MovementFood.FromRouteCost(p, 2, 100, 1600, 10_000, HaulBasis.FullLoad);
        MovementEstimate b = MovementFood.FromRouteCost(p, 2, 200, 3200, 10_000, HaulBasis.FullLoad);
        Assert.InRange(b.RationUnits, 2 * a.RationUnits - 1, 2 * a.RationUnits + 1);
        // Exact: haul = ceil(rations x bulk x cost x 0.16) on the rounded-up rations (1112 and 2223 here).
        Assert.Equal(1112, a.RationUnits);
        Assert.Equal(2223, b.RationUnits);
        Assert.Equal(17792, a.HaulUnits);    // ceil(1112 x 100 x 0.16)
        Assert.Equal(71136, b.HaulUnits);    // ceil(2223 x 200 x 0.16) — within 0.05 % of 4 x 17792
        Assert.InRange(b.HaulUnits, 4 * a.HaulUnits - 40, 4 * a.HaulUnits + 40);
    }

    [Fact]
    public void Slope_MakesTheSameDistanceDearer_OnTheRealLatticeRules()
    {
        // Two 1-D corridors on a 16x16 lattice with the worldgen.json pixel rule cost = baseCost 1.0 + slopeFactor 60 x slope
        // (a node's cost = its block mean): flat (slope 0) vs hills (slope 0.05 per pixel => 1 + 3 = 4.0).
        const int n = 16;
        Pathfinder.PathResult Path(double nodeCost)
        {
            var costs = new double[n * n];
            var open = new bool[n * n];
            for (int i = 0; i < costs.Length; i++) { costs[i] = nodeCost; open[i] = true; }
            TraversalLattice lat = TraversalLattice.FromCosts(n, costs, open, kmPerNode: 16.0);
            return Pathfinder.FindPath(lat, new WorldState(), lat.NodeId(0, 8), lat.NodeId(15, 8));
        }
        double slopeCost = EconWorld.WorldgenCfg.Movement.BaseCost + EconWorld.WorldgenCfg.Movement.SlopeFactor * 0.05;
        Pathfinder.PathResult flat = Path(EconWorld.WorldgenCfg.Movement.BaseCost), hills = Path(slopeCost);
        Assert.Equal(15.0, flat.TotalCost);
        Assert.Equal(15.0 * 4.0, hills.TotalCost, 9);
        MovementParams p = MovementParams.From(EconWorld.SimCfg, Worlds.Turn1Seed42);
        MovementEstimate f = MovementFood.FromRouteCost(p, flat.Nodes.Length, flat.TotalCost, 240.0, 1_000, HaulBasis.FullLoad);
        MovementEstimate h = MovementFood.FromRouteCost(p, hills.Nodes.Length, hills.TotalCost, 240.0, 1_000, HaulBasis.FullLoad);
        Assert.True(h.TravelDays == 4 * f.TravelDays && h.TotalUnits > f.TotalUnits);
    }

    [Fact]
    public void RoadClasses_AreStrictlyCheaperThanTheBaseline_InShippedOrder()
    {
        SimConfig cfg = EconWorld.SimCfg;
        MovementParams p = MovementParams.From(cfg, Worlds.Turn1Seed42);
        double last = double.PositiveInfinity;
        int[] classes = [EdgeTypes.DirtPath, EdgeTypes.Trackway, EdgeTypes.BuiltRoad, 4, 5];
        foreach (int c in classes)
        {
            double days = MovementFood.RoadDays(cfg, p, 400.0, c);
            Assert.True(days < last, "road class " + c + " is not faster than the previous one");
            last = days;
        }
        Assert.Equal(10.0, MovementFood.RoadDays(cfg, p, 400.0, EdgeTypes.DirtPath));   // 400 km / 40 km/day
        Assert.Equal(7.0, MovementFood.RoadDays(cfg, p, 400.0, EdgeTypes.BuiltRoad), 12); // speedFactor 0.7
    }

    [Fact]
    public void RouteCost_EqualsTheEnginesOwnPairwiseCost_AndDaysEqualTransportQuery()
    {
        // The prototype's route is the same Pathfinder on the same lattice the catchment's SettlementDistances come from, and its
        // days are TransportQuery.EstimateFreight's TransitDays (DirtPath baseline, no roads in an order-less world).
        WorldState w = Worlds.Turn1Seed42;
        SimConfig cfg = EconWorld.SimCfg;
        MovementParams p = MovementParams.From(cfg, w);
        TraversalLattice lattice = MovementFood.Lattice(w, p);
        int compared = 0;
        for (int i = 0; i < w.SettlementDistances.Count; i++)
        {
            SettlementDistanceRow row = w.SettlementDistances[i];
            if (row.From.Value != w.Settlements[0].Id.Value || !double.IsFinite(row.TravelCost)) continue;
            MovementEstimate e = MovementFood.Estimate(w, p, lattice, row.From, row.To, 1_000, HaulBasis.FullLoad);
            Assert.True(e.Reachable);
            Assert.Equal(row.TravelCost, e.RouteCostUnits, row.TravelCost * 1e-12);
            TransportQuery.FreightEstimate fr = TransportQuery.EstimateFreight(w, cfg.Roads!, row.From, row.To, 0.0);
            Assert.Equal(fr.TransitDays, e.TravelDays, fr.TransitDays * 1e-9);
            compared++;
        }
        Assert.True(compared >= 5, "expected several reachable pairs, compared " + compared);
    }
}
