using Sim.Core;
using Sim.Core.Kernel;
using Sim.Core.Pathing;
using Sim.Core.State;
using Sim.Core.Systems;
using Sim.Core.Worldgen;
using Sim.Ui.ViewModel;
using Xunit;

namespace Sim.Ui.Tests;

// T1.8 automated acceptance: view-model level — slider→order payload exactness,
// HUD numbers/formatting from a founded state, overlay world→screen transforms.
// No Game, no window; the executor runs headless exactly as the CLI does.
public class HudViewModelTests
{
    private static SimConfig SimCfg()
    {
        using var stream = global::Sim.Data.DataFiles.OpenSim();
        return SimConfigLoader.Load(stream);
    }

    private static WorldgenConfig DevCfg()
    {
        using var stream = global::Sim.Data.DataFiles.OpenWorldgen();
        return WorldgenConfigLoader.Load(stream) with { SizePx = 256 };
    }

    private static TurnExecutor Executor(SimConfig cfg, OrderLog? orders = null)
    {
        using var eraStream = global::Sim.Data.DataFiles.OpenEraPacing();
        using var pipeStream = global::Sim.Data.DataFiles.OpenPipeline();
        return new TurnExecutor(
            EraTableLoader.Load(eraStream),
            PipelineLoader.Load(pipeStream, SystemCatalog.All(cfg)), orders);
    }

    // --- slider → order payload ----------------------------------------------

    [Fact]
    public void SliderOrder_PayloadExact()
    {
        OrderRecord order = LaborOrderFactory.Create(
            currentTurn: 17, new SettlementId(0), farmPct: 35);
        Assert.Equal(17, order.Turn);
        Assert.Equal(LaborOrderFactory.UiActorId, order.ActorId);
        Assert.Equal(OrderKind.LaborAllocation, order.Kind);
        Assert.Equal(0, order.TargetId);
        Assert.Equal(35.0, order.Amount); // integer percent → exact double

        Assert.Throws<ArgumentOutOfRangeException>(
            () => LaborOrderFactory.Create(1, new SettlementId(0), 101));
        Assert.Throws<ArgumentOutOfRangeException>(
            () => LaborOrderFactory.Create(1, new SettlementId(0), -1));
    }

    [Fact]
    public void SliderOrder_RoundTripsTheOrderLog_AndSteersTheSim()
    {
        // The full UI order path headless: factory → log → save/load → executor.
        SimConfig cfg = SimCfg();
        var orders = new OrderLog();
        orders.Append(LaborOrderFactory.Create(2, new SettlementId(0), 50));

        using var buffer = new MemoryStream();
        orders.Save(buffer);
        buffer.Position = 0;
        OrderLog loaded = OrderLog.Load(buffer); // load-time validation passes

        TurnExecutor exec = Executor(cfg, loaded);
        WorldState world = WorldFounding.Found(DevCfg(), cfg, 42);
        for (int t = 1; t <= 3; t++) world = exec.Step(world);
        Assert.Equal(0.5, world.LaborAllocations[0].FarmShare);
    }

    // --- HUD numbers from a founded state ------------------------------------

    [Fact]
    public void HudModel_FoundedState_NumbersAndFormattingExact()
    {
        SimConfig cfg = SimCfg();
        WorldState world = WorldFounding.Found(DevCfg(), cfg, 42);

        HudModel hud = HudModel.From(world, previousHarvestTotal: 0);
        Assert.Equal(cfg.Founding.Children, hud.Children);
        Assert.Equal(cfg.Founding.Adults, hud.Adults);
        Assert.Equal(cfg.Founding.Elders, hud.Elders);
        Assert.Equal(cfg.Founding.Children + cfg.Founding.Adults + cfg.Founding.Elders,
            hud.TotalPopulation);
        Assert.Equal(cfg.Founding.FoodStore, hud.FoodStore);
        Assert.Equal(0, hud.LastHarvest);          // nothing harvested pre-turn-1
        Assert.Equal(100.0, hud.FarmSharePct);     // never-ordered default
        Assert.Equal(0, hud.Turn);
        Assert.Equal(-4000, hud.Year);

        Assert.Equal("pop 400  (child 130 / adult 200 / elder 70)", hud.PopulationLine);
        Assert.Equal("food 6000  (last harvest +0)", hud.FoodLine);
        Assert.Equal("labor 100% farm / 0% path", hud.SplitLine);
        Assert.Equal("turn 0   year -4000", hud.ClockLine);
    }

    [Fact]
    public void HudModel_LastHarvest_IsTheDeltaAcrossEndTurn()
    {
        // Mirrors the Game's End Turn bookkeeping: previous cumulative total is
        // carried; the HUD shows the per-turn delta, exactly the Harvest flow.
        SimConfig cfg = SimCfg();
        TurnExecutor exec = Executor(cfg);
        WorldState world = WorldFounding.Found(DevCfg(), cfg, 42);

        long previous = HudModel.From(world, 0).HarvestTotal;
        for (int t = 1; t <= 3; t++)
        {
            world = exec.Step(world);
            HudModel hud = HudModel.From(world, previous);
            Assert.Equal(hud.HarvestTotal - previous, hud.LastHarvest);
            Assert.True(hud.LastHarvest >= 0);
            previous = hud.HarvestTotal;
        }
        // By turn 3 the catchment landed in Prev and farming produced: nonzero.
        Assert.True(previous > 0, "no harvest by turn 3 — vacuous HUD delta");
    }

    // --- overlay transforms ---------------------------------------------------

    [Fact]
    public void SettlementMarker_WorldAnchor_TransformsToScreenExactly()
    {
        SimConfig cfg = SimCfg();
        WorldState world = WorldFounding.Found(DevCfg(), cfg, 42);
        int terrainSize = world.Terrain!.Size;

        LineGeometry.Vertex position = OverlayMeshes.SettlementPosition(
            world.Settlements[0], terrainSize);
        Assert.Equal(world.Settlements[0].SiteCell % terrainSize + 0.5, position.X);
        Assert.Equal(world.Settlements[0].SiteCell / terrainSize + 0.5, position.Y);

        var cam = new Camera(terrainSize);
        cam.ZoomAt(640, 400, 2.0, 1280, 800);
        (double sx, double sy) = cam.WorldToScreen(position.X, position.Y, 1280, 800);
        Assert.Equal((position.X - cam.CenterX) * cam.Zoom + 640.0, sx, precision: 9);
        Assert.Equal((position.Y - cam.CenterY) * cam.Zoom + 400.0, sy, precision: 9);
    }

    [Fact]
    public void PathMesh_BuildsQuadsAtLatticeAnchors_InEdgeOrder()
    {
        // Hand world: two network nodes on a known lattice, one edge.
        var world = new WorldState(1);
        world.NetworkNodes.Add(new NetworkNodeRow(new NetworkNodeId(0), LatticeNode: 5));   // (5,0) on 64-lattice
        world.NetworkNodes.Add(new NetworkNodeRow(new NetworkNodeId(1), LatticeNode: 69));  // (5,1)
        world.NetworkEdges.Add(new NetworkEdgeRow(
            new NetworkEdgeId(0), new NetworkNodeId(0), new NetworkNodeId(1), EdgeTypes.DirtPath, 1.0));

        LineGeometry.Vertex[] mesh = OverlayMeshes.BuildPaths(world, latticeSize: 64, stride: 4);
        Assert.Equal(6, mesh.Length); // one segment quad

        // Segment runs (22,2) → (22,6): all vertices bracket it by half a width.
        LineGeometry.Vertex a = OverlayMeshes.LatticeNodeCenter(5, 64, 4);
        LineGeometry.Vertex b = OverlayMeshes.LatticeNodeCenter(69, 64, 4);
        Assert.Equal(22.0, a.X); Assert.Equal(2.0, a.Y);
        Assert.Equal(22.0, b.X); Assert.Equal(6.0, b.Y);
        double half = OverlayMeshes.PathWidthWorldPx / 2.0;
        foreach (LineGeometry.Vertex v in mesh)
        {
            Assert.InRange(v.X, a.X - half - 1e-9, a.X + half + 1e-9);
            Assert.InRange(v.Y, a.Y - half - 1e-9, b.Y + half + 1e-9);
        }
    }

    [Fact]
    public void CatchmentFill_OneQuadPerReachedNode_CoveringItsBlock()
    {
        var world = new WorldState(1);
        world.CatchmentNodes.Add(new CatchmentNodeRow(new SettlementId(0), LatticeNode: 0, TravelCost: 0.0));
        world.CatchmentNodes.Add(new CatchmentNodeRow(new SettlementId(0), LatticeNode: 65, TravelCost: 1.0)); // (1,1)

        LineGeometry.Vertex[] mesh = OverlayMeshes.BuildCatchmentFill(world, latticeSize: 64, stride: 4);
        Assert.Equal(12, mesh.Length); // two node blocks × 6 vertices

        // First block covers world rect [0,4]×[0,4]; second [4,8]×[4,8].
        for (int i = 0; i < 6; i++)
        {
            Assert.InRange(mesh[i].X, 0.0, 4.0);
            Assert.InRange(mesh[i].Y, 0.0, 4.0);
        }
        for (int i = 6; i < 12; i++)
        {
            Assert.InRange(mesh[i].X, 4.0, 8.0);
            Assert.InRange(mesh[i].Y, 4.0, 8.0);
        }
    }

    [Fact]
    public void CatchmentFill_FromFoundedRun_MatchesCatchmentNodeCount()
    {
        // Founded world stepped once: the fill covers exactly the reached set.
        SimConfig cfg = SimCfg();
        TurnExecutor exec = Executor(cfg);
        WorldState world = exec.Step(WorldFounding.Found(DevCfg(), cfg, 42));
        Assert.True(world.CatchmentNodes.Count > 0);

        TraversalLattice lattice = TraversalLattice.Build(world.Terrain!);
        int stride = OverlayMeshes.LatticeStride(lattice, world.Terrain!.Size);
        LineGeometry.Vertex[] mesh = OverlayMeshes.BuildCatchmentFill(world, lattice.Size, stride);
        Assert.Equal(world.CatchmentNodes.Count * 6, mesh.Length);
    }
}
