using Sim.Core.Kernel;
using Sim.Core.State;
using Sim.Core.Systems;
using Sim.Core.Worldgen;
using Sim.Ui.ViewModel;
using Xunit;

namespace Sim.Ui.Tests;

// T2.4: selection view-model — hit-test math (zoom extremes, nearest-with-id
// tie-break), Tab cycling, palette determinism/distinctness, territory-fill
// disjointness, and the slider→order pipe carrying the SELECTED id exactly.
public class SelectionTests
{
    private static WorldgenConfig DevCfg()
    {
        using var stream = Sim.Data.DataFiles.OpenWorldgen();
        WorldgenConfig c = WorldgenConfigLoader.Load(stream);
        return c with { SizePx = 256, Siting = c.Siting with { SettlementCount = 4 } };
    }

    private static SimConfig SimCfg()
    {
        using var stream = Sim.Data.DataFiles.OpenSim();
        using var needs = Sim.Data.DataFiles.OpenNeeds();
        return SimConfigLoader.Load(stream, needs);
    }

    /// <summary>Hand-built world: settlements at chosen site cells on a bare
    /// 256² terrain (selection math needs positions, not gameplay).</summary>
    private static WorldState MarkerWorld(params int[] siteCells)
    {
        var world = new WorldState(7)
        {
            Terrain = Sim.Core.Worldgen.Worldgen.Generate(DevCfg(), 42),
        };
        for (int i = 0; i < siteCells.Length; i++)
            world.Settlements.Add(new SettlementRow(new SettlementId(i), siteCells[i], 0));
        return world;
    }

    // --- hit test -----------------------------------------------------------

    [Fact]
    public void HitTest_AtDefaultZoom_HitsTheMarker_MissesBeyondRadius()
    {
        // Site cell 100 + 100*256 → world (100.5, 100.5); camera 1:1 centered
        // at 128: screen = world − 128 + viewport/2.
        WorldState world = MarkerWorld(100 + 100 * 256);
        var cam = new Camera(256);
        cam.Clamp(512, 512);
        (double sx, double sy) = cam.WorldToScreen(100.5, 100.5, 512, 512);

        Assert.Equal(0, SettlementSelection.HitTest(world, cam, sx, sy, 512, 512));
        Assert.Equal(0, SettlementSelection.HitTest(world, cam,
            sx + SettlementSelection.HitRadiusPx - 0.5, sy, 512, 512));
        Assert.Equal(-1, SettlementSelection.HitTest(world, cam,
            sx + SettlementSelection.HitRadiusPx + 0.5, sy, 512, 512));
    }

    [Fact]
    public void HitTest_ZoomExtremes_ScreenRadiusHolds()
    {
        // Markers are constant SCREEN size, so the hit radius is constant in
        // screen px at every zoom — clickable at the world-fit minimum, exact
        // at 32×. Pin both extremes.
        WorldState world = MarkerWorld(100 + 100 * 256);
        var cam = new Camera(256);

        // Max zoom (32×), zoomed onto the marker.
        cam.ZoomAt(256, 256, 1000.0, 512, 512); // clamps to MaxZoom
        Assert.Equal(32.0, cam.Zoom);
        // Re-center near the marker so it is on screen.
        (double wx, double wy) = (100.5, 100.5);
        // Pan so that the marker sits at the viewport center: current center
        // is clamped; compute its screen pos and pan the delta.
        (double msx, double msy) = cam.WorldToScreen(wx, wy, 512, 512);
        cam.Pan(256 - msx, 256 - msy, 512, 512);
        (msx, msy) = cam.WorldToScreen(wx, wy, 512, 512);
        Assert.Equal(0, SettlementSelection.HitTest(world, cam, msx + 6, msy, 512, 512));
        Assert.Equal(-1, SettlementSelection.HitTest(world, cam,
            msx + SettlementSelection.HitRadiusPx + 1, msy, 512, 512));

        // Min zoom (world fits): a whole settlement block is sub-marker-size on
        // screen, but the screen-radius floor keeps it clickable.
        var wide = new Camera(256);
        wide.ZoomAt(100, 100, 1e-6, 200, 200); // slam to the world-fit minimum
        Assert.True(wide.Zoom < 1.0);
        (double s2x, double s2y) = wide.WorldToScreen(wx, wy, 200, 200);
        Assert.Equal(0, SettlementSelection.HitTest(world, wide, s2x + 6, s2y, 200, 200));
    }

    [Fact]
    public void HitTest_OverlappingMarkers_NearestWins_ExactTieLowerId()
    {
        // Two settlements one cell apart (screen distance 1 px at 1:1): a
        // click nearer marker 1 selects 1 — nearest wins, not first-scanned.
        int a = 100 + 100 * 256, b = a + 1;
        WorldState world = MarkerWorld(a, b);
        var cam = new Camera(256);
        cam.Clamp(512, 512);
        (double sax, double say) = cam.WorldToScreen(100.5, 100.5, 512, 512);
        (double sbx, _) = cam.WorldToScreen(101.5, 100.5, 512, 512);

        Assert.Equal(0, SettlementSelection.HitTest(world, cam, sax - 0.3, say, 512, 512));
        Assert.Equal(1, SettlementSelection.HitTest(world, cam, sbx + 0.3, say, 512, 512));

        // EXACT tie (tie-dense, constitution rule): three co-located markers —
        // identical screen distance, every id — the LOWEST id wins.
        WorldState stacked = MarkerWorld(a, a, a);
        Assert.Equal(0, SettlementSelection.HitTest(stacked, cam, sax, say, 512, 512));
        // And a tie between only ids 1 and 2 (id 0 far away) picks 1.
        WorldState pair = MarkerWorld(0, a, a);
        Assert.Equal(1, SettlementSelection.HitTest(pair, cam, sax, say, 512, 512));
    }

    // --- Tab cycling --------------------------------------------------------

    [Fact]
    public void CycleNext_IdOrder_Wraps_AndRecoversFromNone()
    {
        WorldState world = MarkerWorld(10, 20, 30, 40);
        Assert.Equal(1, SettlementSelection.CycleNext(world, 0));
        Assert.Equal(2, SettlementSelection.CycleNext(world, 1));
        Assert.Equal(3, SettlementSelection.CycleNext(world, 2));
        Assert.Equal(0, SettlementSelection.CycleNext(world, 3));  // wrap
        Assert.Equal(0, SettlementSelection.CycleNext(world, -1)); // none → first
        Assert.Equal(0, SettlementSelection.CycleNext(world, 99)); // unknown → first
        Assert.Equal(-1, SettlementSelection.CycleNext(new WorldState(1), 0)); // empty world
    }

    // --- palette ------------------------------------------------------------

    [Fact]
    public void Palette_Deterministic_TwelveDistinct_WrapsById()
    {
        // Deterministic: same id, same color, always.
        for (int id = 0; id < 24; id++)
            Assert.Equal(SettlementPalette.Color(id), SettlementPalette.Color(id));

        // 12 distinct at a glance: pairwise Euclidean RGB distance ≥ 64 (the
        // interleaved-hue table's actual minimum — a regression that dulls two
        // slots toward each other fails loudly).
        for (int i = 0; i < SettlementPalette.Count; i++)
        {
            for (int j = i + 1; j < SettlementPalette.Count; j++)
            {
                (byte r1, byte g1, byte b1) = SettlementPalette.Color(i);
                (byte r2, byte g2, byte b2) = SettlementPalette.Color(j);
                double d = Math.Sqrt(
                    (r1 - r2) * (double)(r1 - r2) + (g1 - g2) * (double)(g1 - g2)
                    + (b1 - b2) * (double)(b1 - b2));
                Assert.True(d >= 64.0, $"palette {i} vs {j}: distance {d:F1} < 64");
            }
        }

        // Id 12 wraps to slot 0 (13th settlement reuses the first tint).
        Assert.Equal(SettlementPalette.Color(0), SettlementPalette.Color(12));
    }

    // --- territory fills ----------------------------------------------------

    [Fact]
    public void TerritoryFills_PerSettlement_DisjointAndComplete()
    {
        // Founded dev world after one catchment step: each settlement's mesh
        // has exactly 6 vertices per owned node, and no lattice block appears
        // in two settlements' meshes (the partition, as geometry).
        SimConfig cfg = SimCfg();
        WorldState world = WorldFounding.Found(DevCfg(), cfg, 42);
        EraTable era;
        using (var stream = Sim.Data.DataFiles.OpenEraPacing()) era = EraTableLoader.Load(stream);
        world = new TurnExecutor(era, [Sim.Core.SystemCatalog.Catchment()]).Step(world);

        var lattice = Sim.Core.Pathing.TraversalLattice.Build(world.Terrain!);
        int stride = OverlayMeshes.LatticeStride(lattice, world.Terrain!.Size);
        LineGeometry.Vertex[][] fills =
            OverlayMeshes.BuildTerritoryFills(world, lattice.Size, stride);

        Assert.Equal(world.Settlements.Count, fills.Length);
        var blockOwners = new Dictionary<(double, double), int>();
        long totalNodes = 0;
        for (int s = 0; s < fills.Length; s++)
        {
            Assert.Equal(0, fills[s].Length % 6);
            Assert.Equal(world.CatchmentSummaries[s].NodeCount, fills[s].Length / 6);
            totalNodes += fills[s].Length / 6;
            for (int v = 0; v < fills[s].Length; v += 6)
            {
                (double, double) corner = (fills[s][v].X, fills[s][v].Y);
                Assert.False(blockOwners.TryGetValue(corner, out int other) && other != s,
                    $"block {corner} appears in settlements {other} and {s}");
                blockOwners[corner] = s;
            }
        }
        Assert.Equal(world.CatchmentNodes.Count, totalNodes);
        Assert.True(totalNodes > 0, "no territory at all — fills vacuous");
    }

    // --- the slider → order pipe -------------------------------------------

    [Fact]
    public void EmitLaborOrder_CarriesTheSelectedId_Exactly()
    {
        var session = Sim.Ui.UiSession.Start(42, sizeOverridePx: 256, settlementsOverride: 4);
        session.EmitLaborOrder(35, 2);
        session.EmitLaborOrder(80, 1);
        session.EmitLaborOrder(50, 99); // ghost settlement: NOTHING emitted

        Assert.Equal(2, session.Orders.Count);
        Assert.Equal(2, session.Orders[0].TargetId);
        Assert.Equal(35.0, session.Orders[0].Amount);
        Assert.Equal(1, session.Orders[1].TargetId);
        Assert.Equal(80.0, session.Orders[1].Amount);
    }

    [Fact]
    public void TwoSettlements_RuledDifferently_BothObey()
    {
        // The gate criterion, agent-level: two settlements ordered differently
        // in one session both follow their own orders (the director proves it
        // visually; this proves it in state).
        var session = Sim.Ui.UiSession.Start(42, sizeOverridePx: 256, settlementsOverride: 4);
        session.EmitLaborOrder(30, 1);
        session.EmitLaborOrder(70, 2);
        session.EndTurn(); // both orders deliver on the very next End Turn

        double share1 = -1, share2 = -1, share3 = -1;
        for (int i = 0; i < session.World.LaborAllocations.Count; i++)
        {
            LaborAllocationRow row = session.World.LaborAllocations[i];
            if (row.Settlement.Value == 1) share1 = row.FarmShare;
            if (row.Settlement.Value == 2) share2 = row.FarmShare;
            if (row.Settlement.Value == 3) share3 = row.FarmShare;
        }
        Assert.Equal(0.30, share1);
        Assert.Equal(0.70, share2);
        Assert.Equal(-1, share3); // never ordered → no row (default 1.0 applies)

        // And the HUD reads each settlement's own split.
        Assert.Equal(30.0, Sim.Ui.ViewModel.HudModel.From(session.World, 1).FarmSharePct);
        Assert.Equal(70.0, Sim.Ui.ViewModel.HudModel.From(session.World, 2).FarmSharePct);
        Assert.Equal(100.0, Sim.Ui.ViewModel.HudModel.From(session.World, 3).FarmSharePct);
    }

    [Fact]
    public void HudStrings_NonZeroSettlement_Exact()
    {
        SimConfig cfg = SimCfg();
        WorldState world = WorldFounding.Found(DevCfg(), cfg, 42);
        HudModel hud = HudModel.From(world, selectedSettlementId: 2);

        Assert.Equal("Settlement 2", hud.TitleLine);
        Assert.Equal("pop 400  (child 130 / adult 200 / elder 70)", hud.PopulationLine);
        Assert.Equal("food 6000  (last harvest +0)", hud.FoodLine);
        Assert.Equal("labor 100% farm / 0% path", hud.SplitLine);
        Assert.Equal("world pop 1600  (4 settlements)", hud.WorldLine);
        Assert.Equal(2, hud.SettlementId);
    }
}
