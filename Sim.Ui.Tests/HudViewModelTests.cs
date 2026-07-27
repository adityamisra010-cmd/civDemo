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
        using var needs = global::Sim.Data.DataFiles.OpenNeeds();
        using var goods = global::Sim.Data.DataFiles.OpenGoods();
        // T3.1 note: these view-model tests pin EXACT founded strings, so the
        // canonical endowment jitter is zeroed — formatting is the contract
        // here; the jittered founding has its own pins in Sim.Tests.
        SimConfig cfg = SimConfigLoader.Load(stream, needs, goods);
        return cfg with { Founding = cfg.Founding with { EndowmentJitter = 0.0 } };
    }

    private static WorldgenConfig DevCfg()
    {
        using var stream = global::Sim.Data.DataFiles.OpenWorldgen();
        return WorldgenConfigLoader.Load(stream) is { } c
            ? c with { SizePx = 256, Siting = c.Siting with { SettlementCount = 4 } } // D-025 dev preset
            : throw new InvalidOperationException();
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
        Assert.Equal(0.5, world.SectorAllocations[0].Farming);
    }

    // --- HUD numbers from a founded state ------------------------------------

    [Fact]
    public void HudModel_FoundedState_NumbersAndFormattingExact()
    {
        SimConfig cfg = SimCfg();
        WorldState world = WorldFounding.Found(DevCfg(), cfg, 42);

        HudModel hud = HudModel.From(world, selectedSettlementId: 0);
        // Band views over the founding cohort counts (T2.1): 0-2 / 3-11 / 12-15.
        long children = 0, adults = 0, elders = 0, total = 0;
        for (int c = 0; c < Sim.Core.State.Cohorts.Count; c++)
        {
            long n = cfg.Founding.CohortCounts[c];
            total += n;
            if (c < Sim.Core.State.Cohorts.FirstAdult) children += n;
            else if (c < Sim.Core.State.Cohorts.FirstElder) adults += n;
            else elders += n;
        }
        Assert.Equal(children, hud.Children);
        Assert.Equal(adults, hud.Adults);
        Assert.Equal(elders, hud.Elders);
        Assert.Equal(total, hud.TotalPopulation);
        Assert.Equal(cfg.Founding.FoodStore, hud.FoodStore);
        Assert.Equal(0, hud.LastHarvest);          // nothing harvested pre-turn-1
        Assert.Equal(100.0, hud.FarmingPct);     // never-ordered default
        Assert.Equal(0, hud.Turn);
        Assert.Equal(-4000, hud.Year);

        // EVERY string handed to ImGui, exact (T1.8 re-gate finding 2: the
        // SplitLine's '%' was printf-mangled into garbage by ImGui.Text; the
        // HUD must render these via TextUnformatted, and these are the exact
        // strings it hands over — '%' characters deliberately included).
        Assert.Equal("Settlement 0", hud.TitleLine);
        Assert.Equal("pop 400  (child 130 / adult 200 / elder 70)", hud.PopulationLine);
        Assert.Equal("food 6000  (last harvest +0)", hud.FoodLine);
        Assert.Equal("labor 100% farm / 0% herd / 0% mine / 0% craft / 0% build", hud.SplitLine);
        Assert.Contains('%', hud.SplitLine); // the printf trap, pinned on purpose
        Assert.Equal("world pop 1600  (4 settlements)", hud.WorldLine); // T2.4, dev N=4
        Assert.Equal("turn 0   year -4000", hud.ClockLine);
        Assert.Equal("seed 42   fps 60", HudModel.StatusLine(42, 60.4));
        Assert.Equal("camera (128, 128) zoom 1.00x", HudModel.CameraLine(128.0, 128.0, 1.0));
    }

    [Fact]
    public void HudModel_NeedsBlock_BoundValueAndNotYetSimulatedLabels()
    {
        // T2.6/T3.5: the needs block renders the FULL D-018 ladder in registry
        // order — every BOUND need with its satisfaction value, every unbound
        // need honestly labeled "not yet simulated" — plus the grievance line.
        // Driven through one production turn so satisfaction rows exist.
        //
        // T3.5 anchors, all mechanism rather than accident. Sustenance reads
        // 0.89, not 1.00: the settlement is fully fed but eats grain alone, and
        // D-035-A prices monotony INSIDE satisfaction. Shelter and Comfort read
        // 0.00: an all-farming settlement cuts no timber and weaves no cloth,
        // so those baskets are empty — which is the mechanism working, and is
        // reported as a finding in the packet handback, not hidden here.
        SimConfig cfg = SimCfg();
        Sim.Core.Systems.NeedsConfig needs = cfg.Needs!;
        WorldState world = WorldFounding.Found(DevCfg(), cfg, 42);
        var orders = new Sim.Core.Kernel.OrderLog();
        world = UiSession.BuildProductionExecutor(orders).Step(world);

        HudModel hud = HudModel.From(world, selectedSettlementId: 0, needs);
        Assert.NotNull(hud.NeedLines);
        Assert.Equal(8, hud.NeedLines!.Count);
        // TURN 1 READS THE FOUNDING PREV, which published no consumption yet.
        // A published demand of zero is NOT read as "fully supplied": the T3.5
        // review found that sentinel reporting empty timber and pottery stores
        // as perfect Shelter and Comfort, and Fill now uses the STOCK as the
        // discriminator. Founding stocks these goods at zero, so they correctly
        // read 0.00 from the very first turn instead of enjoying one free turn
        // of phantom supply. Sustenance reads 0.85 — full calories, since the
        // missing non-staples substitute into grain, times the full D-035-A
        // monotony penalty for a diet that is grain and nothing else.
        Assert.Equal("Sustenance: 0.85", hud.NeedLines[0]);
        Assert.Equal("Shelter: 0.00", hud.NeedLines[1]);
        Assert.Equal("Comfort: 0.00", hud.NeedLines[5]);
        for (int i = 0; i < 8; i++)
        {
            if (needs.Needs[i].Bound) continue;
            Assert.Equal($"{needs.Needs[i].Name}: not yet simulated", hud.NeedLines[i]);
        }

        // Without the registry (legacy callers) the block is simply absent.
        Assert.Empty(HudModel.From(world, 0).NeedLines!);
    }

    [Fact]
    public void HudModel_TurnZero_SatisfactionUnpublished_ReadsNotYetMeasured_Then1FromTurn1On()
    {
        // Director gate finding (T2.9): the founding-day HUD showed
        // "Sustenance: 0.00" with full stores and zero deficit. Diagnosis:
        // UNPUBLISHED, not mis-wired — satisfaction rows are rebuilt per turn
        // by NeedsGrievance, so at turn 0 the table is empty and the old HUD
        // fabricated a default 0. PIN: turn 0 renders "not yet measured";
        // from turn 1 ONWARD it renders a real measured value. T3.5: that value
        // is 0.89 rather than 1.00 for a grain-only diet — the pin is that the
        // HUD reports what the sim computed instead of inventing a default,
        // which is what the finding was about.
        SimConfig cfg = SimCfg();
        Sim.Core.Systems.NeedsConfig needs = cfg.Needs!;
        WorldState world = WorldFounding.Found(DevCfg(), cfg, 42);
        Assert.Equal(0, world.NeedSatisfactions.Count); // the diagnosis, pinned

        HudModel turn0 = HudModel.From(world, 0, needs);
        Assert.Equal("Sustenance: not yet measured", turn0.NeedLines![0]);

        TurnExecutor exec = Executor(cfg);
        for (int t = 1; t <= 5; t++)
        {
            world = exec.Step(world);
            HudModel hud = HudModel.From(world, 0, needs);
            // 0.85 from the first turn on: full calories (the missing
            // non-staples substitute into grain) times the full D-035-A monotony
            // penalty for a grain-only diet. Before the T3.5 review fixed Fill's
            // zero-demand sentinel, turn 1 read 0.89 — a phantom credit for
            // livestock and fish the settlement had no stock of.
            Assert.Equal("Sustenance: 0.85", hud.NeedLines![0]);
        }
    }

    [Fact]
    public void HudModel_LastHarvest_IsTheSelectedSettlementsPerTurnHarvest()
    {
        // T2.4 migration (deliberate): LastHarvest is now the SELECTED
        // settlement's GoodStockRow.LastProducedUnits — the per-settlement
        // observable Farming writes each turn (T2.2) — replacing the T1.8
        // UI-side global-ledger delta, which could not be per-settlement.
        SimConfig cfg = SimCfg();
        TurnExecutor exec = Executor(cfg);
        WorldState world = WorldFounding.Found(DevCfg(), cfg, 42);

        long anyHarvest = 0;
        for (int t = 1; t <= 3; t++)
        {
            world = exec.Step(world);
            for (int s = 0; s < world.Settlements.Count; s++)
            {
                HudModel hud = HudModel.From(world, world.Settlements[s].Id.Value);
                // T3.2: the grain row for settlement s (stocks are now
                // settlement-major × 14 goods, not one row per settlement).
                int grainRow = GoodStockIndex.IndexOf(
                    world.GoodStocks, world.Settlements[s].Id, new GoodId(1));
                Assert.Equal(world.GoodStocks[grainRow].LastProducedUnits, hud.LastHarvest);
                Assert.True(hud.LastHarvest >= 0);
                anyHarvest += hud.LastHarvest;
            }
        }
        // By turn 3 the catchment landed in Prev and farming produced: nonzero.
        Assert.True(anyHarvest > 0, "no harvest by turn 3 — vacuous HUD read");
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
    [Fact]
    public void Hud_ReportsNormalizedSectorShares_NotAFabricatedTwoWaySplit()
    {
        // T3.3 adversarial finding (empirically demonstrated): the HUD read the
        // RAW farming weight and derived path as (1 − farm). With any third
        // sector allocated it told the player half the workforce was building
        // roads while PathBuild banked exactly zero. The panel must agree with
        // the sim, so it now reads Sectors.Share — the same normalization
        // ProductionSystem and PathBuildSystem use.
        SimConfig cfg = SimCfg();
        WorldState world = WorldFounding.Found(DevCfg(), cfg, 42);
        world.SectorAllocations.Add(new SectorAllocationRow(
            new SettlementId(0), Farming: 0.5, Herding: 0.0, Extraction: 0.0,
            Crafting: 0.5, Construction: 0.0));

        HudModel hud = HudModel.From(world, 0);

        // Raw weights sum to 1.0 here, so farming is 50% — and CONSTRUCTION IS
        // ZERO, which the fabricated split reported as 50%.
        Assert.Equal(50.0, hud.FarmingPct);
        Assert.Equal(50.0, hud.CraftingPct);
        Assert.Equal(0.0, hud.ConstructionPct);
        Assert.Contains("0% build", hud.SplitLine);

        // NORMALIZATION: un-normalized raw weights must report the same shares
        // the sim computes, not the raw numbers.
        world.SectorAllocations[0] = new SectorAllocationRow(
            new SettlementId(0), Farming: 3.0, Herding: 1.0, Extraction: 0.0,
            Crafting: 0.0, Construction: 0.0);
        HudModel scaled = HudModel.From(world, 0);
        Assert.Equal(75.0, scaled.FarmingPct);
        Assert.Equal(25.0, scaled.HerdingPct);
    }
}

// T1.9 — the UI half of the founding-equivalence wall: UiFounding (what
// Program.cs actually plays) must equal the canonical recipe (which
// Sim.Tests pins against the CLI's HeadlessFounding). Transitively, the UI
// and the headless replayer found ONE world.
public class UiFoundingEquivalenceTests
{
    [Fact]
    public void UiFounding_EqualsCanonical_SameSeedSameWorldHash()
    {
        Sim.Core.State.WorldState ui = Sim.Ui.UiFounding.Found(42, sizeOverridePx: null);
        Sim.Core.State.WorldState canonical;
        {
            Sim.Core.Worldgen.WorldgenConfig wg;
            using (var s = global::Sim.Data.DataFiles.OpenWorldgen())
                wg = Sim.Core.Worldgen.WorldgenConfigLoader.Load(s);
            Sim.Core.Systems.SimConfig sim;
            using (var s = global::Sim.Data.DataFiles.OpenSim())
            using (var n = global::Sim.Data.DataFiles.OpenNeeds())
            using (var g = global::Sim.Data.DataFiles.OpenGoods())
                sim = Sim.Core.Systems.SimConfigLoader.Load(s, n, g);
            canonical = Sim.Core.Worldgen.WorldFounding.Found(wg, sim, 42);
        }
        Assert.Equal(
            Sim.Core.Kernel.WorldHash.ComputeHex(canonical),
            Sim.Core.Kernel.WorldHash.ComputeHex(ui));
    }

    [Fact]
    public void HudModel_ExtinctSettlement_GrievanceAndNeedsReadDash_NotNumbers()
    {
        // T2.13 (director exit-session finding, ghost grievance): a
        // population-zero settlement must read "\u2014" for every
        // per-capita-meaningless stat - even against a STALE nonzero
        // grievance row (the display branch is honest independently of the
        // sim-side zeroing).
        Sim.Core.Systems.SimConfig cfg;
        using (var st = global::Sim.Data.DataFiles.OpenSim())
        using (var nd = global::Sim.Data.DataFiles.OpenNeeds())
        using (var gd = global::Sim.Data.DataFiles.OpenGoods())
            cfg = Sim.Core.Systems.SimConfigLoader.Load(st, nd, gd);
        var world = new WorldState(11);
        var id = new SettlementId(0);
        world.Settlements.Add(new SettlementRow(id, SiteCell: 0, FoundedTurn: 0));
        world.Buckets.Add(new BucketRow(
            id, new CultureId(1), new ReligionId(1), new ClassId(1),
            5, Conserved.Zero, 0.0, 0.0, 0.0, 0.0)); // zero count: extinct
        world.Grievances.Add(new GrievanceRow(id, new ClassId(1), 3.75)); // stale ghost
        world.NeedSatisfactions.Add(new NeedSatisfactionRow(id, new ClassId(1), cfg.Needs!.Needs[0].Id, 0.6));

        HudModel hud = HudModel.From(world, 0, cfg.Needs);
        Assert.Equal(0, hud.TotalPopulation);
        Assert.Equal("grievance \u2014", hud.GrievanceLine);
        Assert.Equal($"{cfg.Needs!.Needs[0].Name}: \u2014", hud.NeedLines![0]);
    }

}
