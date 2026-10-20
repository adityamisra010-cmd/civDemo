using Sim.Core.Kernel;
using Sim.Core.Pathing;
using Sim.Core.State;
using Sim.Core.Systems;
using Sim.Core.Systems.Roads;
using Sim.Core.Worldgen;
using Sim.Tests.TestUtil;

namespace Sim.Tests.Pathing;

/// <summary>
/// Batch 3 WS3 (W1 fix F9) — ONE LATTICE GEOMETRY. The lattice-node of a terrain cell is a function of the TERRAIN's
/// size: on a flat world whose size the lattice stride does not divide (250 px at stride 4: 62 nodes per side, two
/// orphan columns and rows), the lattice covers <c>Size × Stride</c> = 248 px of a 250-px raster. Pathfinder's road
/// overlay restated the terrain size as <c>lattice.Size × lattice.Stride</c>, so it read a 250-px site cell as a 248-px
/// one — the wrong row and column — and anchored every travelled road lane to the wrong lattice nodes. Every geometry
/// restatement now routes through <see cref="MapTopology.LatticeGrid"/> and <see cref="MapTopology.NodeOfCell"/> on the
/// terrain's own size. (Pre-existing at de86ace, reachable only on a flat non-stride world: a cylinder refuses those
/// sizes, so since batch 3 only the flat override — tools and tests — can build one.)
/// </summary>
public class NonStrideLatticeGeometryTests
{
    private static readonly SimConfig Cfg = TestConfigs.Sim();
    private static readonly RoadsConfig Roads = Cfg.Roads!;

    [Fact]
    public void OnAFlatNonStrideWorld_ATravelledRoadLane_JoinsItsSettlementsOwnLatticeNodes()
    {
        WorldgenConfig wg = TestConfigs.PreCylinderWorldgen(TestConfigs.DevWorldgen()) with { SizePx = 250 };
        WorldState w = WorldFounding.Found(wg, Cfg, 42);
        TerrainSet t = w.Terrain!;
        Assert.False(t.WrapX);
        TraversalLattice lattice = TraversalLattice.Build(t, Cfg.Transport.RiverCostFactor);
        Assert.Equal(250, t.Size);
        Assert.NotEqual(t.Size, lattice.Size * lattice.Stride);   // the rig: orphan columns exist
        double kmPerCost = LatticeGeometry.KmPerCostUnitOnIdealGround(lattice);

        // One road per settlement pair as long as walking there, so its lane (speed factor < 1) is the cheaper way.
        int checkedPairs = 0;
        for (int a = 0; a < w.Settlements.Count; a++)
            for (int b = a + 1; b < w.Settlements.Count; b++)
            {
                int na = LatticeMap.OriginLatticeNode(lattice, t.Size, w.Settlements[a].SiteCell);
                int nb = LatticeMap.OriginLatticeNode(lattice, t.Size, w.Settlements[b].SiteCell);
                if (na == nb) continue;
                Pathfinder.PathResult walk = Pathfinder.FindPath(lattice, w, na, nb);
                if (!walk.Found) continue;
                WorldState roaded = w.Clone();
                int cls = EdgeTypes.RoadClasses[^1];
                var row = new TransportEdgeRow(0, w.Settlements[a].Id, w.Settlements[b].Id, cls,
                    TransportModes.Road, TransportEdgeStates.Complete,
                    RoadPerformance.EffectiveCapacity(Roads, cls, cls, 0.0), walk.TotalCost * kmPerCost,
                    0, 1, 1, cls, 0.0, RoadPerformance.EffectiveCostFactor(Roads, cls, cls, 0.0));
                roaded.TransportEdges.Add(row);
                double lane = RoadPerformance.TravelCostUnits(row, kmPerCost);
                Assert.True(lane < walk.TotalCost, "the rig's road is not the cheaper way");

                // The lane joins THE SETTLEMENTS' OWN nodes, so the route between them costs at most the lane.
                Pathfinder.PathResult viaRoad = Pathfinder.FindPath(lattice, roaded, na, nb);
                Assert.True(viaRoad.Found);
                Assert.True(viaRoad.TotalCost <= lane + 1e-9 * lane,
                    $"pair {a}-{b}: route {viaRoad.TotalCost} > lane {lane} (walk {walk.TotalCost}) — the lane is anchored elsewhere");
                checkedPairs++;
            }
        Assert.True(checkedPairs >= 3, "the rig checked almost no pair");
    }

    [Fact]
    public void OriginLatticeNode_RefusesATerrainSizeThatIsNotTheLatticesOwn()
    {
        // A terrain size whose lattice grid is not this lattice's is another raster: refused, not silently read as one.
        // (The restatement BuildOverlay used, Size × Stride = 248, has the SAME grid as 250 — 62 nodes of 4 px — so no
        // check can tell it apart; that is why the overlay now passes the terrain's own size, which the test above pins.)
        TerrainSet t250 = Sim.Core.Worldgen.Worldgen.Generate(TestConfigs.PreCylinderWorldgen(TestConfigs.DevWorldgen()) with { SizePx = 250 }, 42);
        TraversalLattice l250 = TraversalLattice.Build(t250, Cfg.Transport.RiverCostFactor);
        int cell = 100 * 250 + 120;
        ArgumentException e = Assert.Throws<ArgumentException>(() => LatticeMap.OriginLatticeNode(l250, l250.Size * l250.Stride - 4, cell));
        Assert.Contains("not the terrain this lattice was built from", e.Message, StringComparison.Ordinal);
        int node = LatticeMap.OriginLatticeNode(l250, 250, cell);
        Assert.True(node >= 0 && node < l250.NodeCount);
    }
}
