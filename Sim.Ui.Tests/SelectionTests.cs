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
        using var goods = Sim.Data.DataFiles.OpenGoods();
        // T3.1 note: these view-model tests pin EXACT founded strings, so the
        // canonical endowment jitter is zeroed — formatting is the contract
        // here; the jittered founding has its own pins in Sim.Tests.
        SimConfig cfg = SimConfigLoader.Load(stream, needs, goods);
        return cfg with { Founding = cfg.Founding with { EndowmentJitter = 0.0 } };
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
    //
    // P3 (M5 map workstream): the hit test reads the geometry the world lens PAINTED (LensFrame.SettlementHits), so
    // every test below paints the marker world through the real WorldLens and clicks the frame. Ported from the
    // world-and-camera HitTest (deleted with the legacy targets it computed — a constant 22 px circle and name rects
    // beside a 20 px marker the map no longer draws); each keeps its property on the painted geometry, and the one
    // pin that had to move says so where it moved.

    /// <summary>Paints <paramref name="world"/> through the camera into a <paramref name="w"/> x <paramref name="h"/>
    /// window, at the level of detail the camera's zoom gives (exactly as GameUi does), and returns the frame.</summary>
    private static Sim.Ui.World.LensFrame PaintFrame(WorldState world, Camera cam, int w, int h)
    {
        var p = Sim.Ui.World.WorldProjection.Build(world, SimCfg(), id => "S" + id.ToString(System.Globalization.CultureInfo.InvariantCulture), new PolityId(0));
        Sim.Ui.World.MapDetail level = Sim.Ui.World.MapDetail.At(cam.Zoom, w, h, p.KmPerPx);   // P2: the game's continuous detail
        return Sim.Ui.World.WorldLens.Paint(new Sim.Ui.Render.DrawList(), Sim.Ui.Render.ApproxTextMeasure.Instance, p, level,
            (x, y) => cam.WorldToScreen(x, y, w, h), cam.Zoom, new Sim.Ui.Render.RectD(0, 0, w, h));
    }

    [Fact]
    public void HitTest_AtDefaultZoom_HitsTheMarker_MissesBeyondRadius()
    {
        // Site cell 100 + 100*256 → world (100.5, 100.5); camera at the fit
        // (zoom 2) centered at 128: screen = (world − 128) × 2 + viewport/2.
        // PIN MOVED DELIBERATELY (P2: the level of detail is keyed on the visible km). Old: this camera was the World
        // band (it shows the whole world: share 1.0) and drew the population mark (14 px across with its halo). New:
        // this 256-px dev world spans 256 × 4 = 1024 km, all of it visible — the REGIONAL band by km (a 1024-km world
        // never shows the World band) — and the lens draws the built footprint at its 14 px floor (28 px across).
        // The property pinned is unchanged: the drawn mark, whichever it is, is grown to the 44 px target.
        WorldState world = MarkerWorld(100 + 100 * 256);
        var cam = new Camera(256);
        cam.Clamp(512, 512);
        (double sx, double sy) = cam.WorldToScreen(100.5, 100.5, 512, 512);
        Sim.Ui.World.LensFrame f = PaintFrame(world, cam, 512, 512);
        Assert.Equal(Sim.Ui.World.WorldZoom.Regional, f.Zoom);
        Assert.True(Assert.Single(f.SettlementHits).Footprint.W < SettlementSelection.MinTargetDiameterPx);   // grown, not its own

        Assert.Equal(0, SettlementSelection.HitTest(f, sx, sy));
        Assert.Equal(0, SettlementSelection.HitTest(f,
            sx + SettlementSelection.HitRadiusPx - 0.5, sy));
        Assert.Equal(-1, SettlementSelection.HitTest(f,
            sx + SettlementSelection.HitRadiusPx + 0.5, sy));
    }

    [Fact]
    public void HitTest_ZoomExtremes_TheTargetIsTheDrawnFootprint_NeverUnderTheMinimum()
    {
        // The target is what the lens drew at each zoom: never under the 44 px
        // minimum (clickable at the world-fit minimum), and as large as the
        // footprint it painted (exact at 32×). Pin both extremes.
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
        Sim.Ui.World.LensFrame close = PaintFrame(world, cam, 512, 512);
        Assert.Equal(Sim.Ui.World.WorldZoom.Settlement, close.Zoom);
        Sim.Ui.World.SettlementHit hit = Assert.Single(close.SettlementHits);
        double reach = hit.Footprint.W / 2;   // the footprint drawn: (2 world px) x 32 = 64 px radius
        Assert.Equal(64.0, reach);
        Assert.Equal(0, SettlementSelection.HitTest(close, msx + 6, msy));
        // PIN MOVED (P3), old -> new: a click 23 px right of the mark MISSED at 32× (the constant 22 px radius), though
        // the footprint the lens drew there reaches 64 px — the defect the packet fixes. Now the drawn footprint is the
        // target: its edge hits, one pixel past it misses.
        Assert.Equal(0, SettlementSelection.HitTest(close, msx + SettlementSelection.HitRadiusPx + 1, msy));
        Assert.Equal(0, SettlementSelection.HitTest(close, msx + reach, msy));
        Assert.Equal(-1, SettlementSelection.HitTest(close, msx + reach + 1, msy));

        // Min zoom (world fits): a whole settlement block is sub-marker-size on
        // screen, but the minimum target keeps it clickable.
        var wide = new Camera(256);
        wide.ZoomAt(100, 100, 1e-6, 200, 200); // slam to the world-fit minimum
        Assert.True(wide.Zoom < 1.0);
        (double s2x, double s2y) = wide.WorldToScreen(wx, wy, 200, 200);
        Sim.Ui.World.LensFrame far = PaintFrame(world, wide, 200, 200);
        Assert.Equal(0, SettlementSelection.HitTest(far, s2x + 6, s2y));
        Assert.Equal(0, SettlementSelection.HitTest(far, s2x + SettlementSelection.HitRadiusPx - 0.5, s2y));
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
        Sim.Ui.World.LensFrame f = PaintFrame(world, cam, 512, 512);

        Assert.Equal(0, SettlementSelection.HitTest(f, sax - 0.3, say));
        Assert.Equal(1, SettlementSelection.HitTest(f, sbx + 0.3, say));

        // EXACT tie (tie-dense, constitution rule): three co-located markers —
        // identical screen distance, every id — the LOWEST id wins.
        WorldState stacked = MarkerWorld(a, a, a);
        Assert.Equal(0, SettlementSelection.HitTest(PaintFrame(stacked, cam, 512, 512), sax, say));
        // And a tie between only ids 1 and 2 (id 0 far away) picks 1.
        WorldState pair = MarkerWorld(0, a, a);
        Assert.Equal(1, SettlementSelection.HitTest(PaintFrame(pair, cam, 512, 512), sax, say));
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
        world = new TurnExecutor(era, [Sim.Core.SystemCatalog.Catchment(cfg)]).Step(world);

        var lattice = Sim.Core.Pathing.TraversalLattice.Build(world.Terrain!, cfg.Transport.RiverCostFactor);
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
    public void EmitSectorOrders_CarriesTheSelectedId_Exactly()
    {
        var session = Sim.Ui.UiSession.Start(42, sizeOverridePx: 256, settlementsOverride: 4);
        Assert.True(session.EmitSectorOrders([35, 0, 0, 0, 65], 2));
        Assert.True(session.EmitSectorOrders([80, 0, 0, 0, 20], 1));
        Assert.False(session.EmitSectorOrders([50, 0, 0, 0, 50], 99)); // ghost settlement: NOTHING emitted

        Assert.Equal(2 * Sectors.Count, session.Orders.Count);
        for (int s = 0; s < Sectors.Count; s++)
        {
            Assert.Equal(2 * 8 + s, session.Orders[s].TargetId);
            Assert.Equal(1 * 8 + s, session.Orders[Sectors.Count + s].TargetId);
        }
        Assert.Equal(35.0, session.Orders[0].Amount);
        Assert.Equal(80.0, session.Orders[Sectors.Count].Amount);
    }

    [Fact]
    public void TwoSettlements_RuledDifferently_BothObey()
    {
        // The gate criterion, agent-level: two settlements ordered differently
        // in one session both follow their own orders (the director proves it
        // visually; this proves it in state).
        var session = Sim.Ui.UiSession.Start(42, sizeOverridePx: 256, settlementsOverride: 4);
        Assert.True(session.EmitSectorOrders([30, 0, 0, 0, 70], 1));
        Assert.True(session.EmitSectorOrders([70, 0, 0, 0, 30], 2));
        session.EndTurn(); // both orders deliver on the very next End Turn

        double share1 = -1, share2 = -1, share3 = -1;
        for (int i = 0; i < session.World.SectorAllocations.Count; i++)
        {
            SectorAllocationRow row = session.World.SectorAllocations[i];
            if (row.Settlement.Value == 1) share1 = row.Farming;
            if (row.Settlement.Value == 2) share2 = row.Farming;
            if (row.Settlement.Value == 3) share3 = row.Farming;
        }
        Assert.Equal(0.30, share1);
        Assert.Equal(0.70, share2);
        Assert.Equal(-1, share3); // never ordered → no row (default 1.0 applies)

        // And the HUD reads each settlement's own split.
        Assert.Equal(30.0, Sim.Ui.ViewModel.HudModel.From(session.World, 1).FarmingPct);
        Assert.Equal(70.0, Sim.Ui.ViewModel.HudModel.From(session.World, 2).FarmingPct);
        Assert.Equal(55.0, Sim.Ui.ViewModel.HudModel.From(session.World, 3).FarmingPct, 9); // unruled: T3.5b default
    }

    [Fact]
    public void HudStrings_NonZeroSettlement_Exact()
    {
        SimConfig cfg = SimCfg();
        WorldState world = WorldFounding.Found(DevCfg(), cfg, 42);
        HudModel hud = HudModel.From(world, selectedSettlementId: 2);

        // P7 (raw settlement ids hidden from player text): an unnamed selection is "Unnamed settlement" (was "Settlement 2").
        Assert.Equal("Unnamed settlement", hud.TitleLine);
        // T4.19 lane C: bands of the kernel's own stable founding vector
        // (sim.json founding._docCohortCounts); 130/200/70 was the old
        // 17.5%-elder endowment that opened the world in a 22% collapse.
        Assert.Equal("pop 400  (child 164 / adult 225 / elder 11)", hud.PopulationLine);
        Assert.Equal("food 6000  (last harvest +0)", hud.FoodLine);
        Assert.Equal("labor 55% farm / 15% herd / 10% mine / 12% craft / 8% build", hud.SplitLine); // T3.5b default mix
        Assert.Equal("world pop 1600  (4 settlements)", hud.WorldLine);
        Assert.Equal(2, hud.SettlementId);
    }

    // --- D-A2: derived target size + clickable labels (docs/art-gate-defects.md)

    [Fact]
    public void HitRadius_IsDerivedFromTheNamedTargetStandard()
    {
        // The 44 px minimum target (Apple HIG / WCAG 2.5.5 Enhanced) is the
        // ratified reference; the radius is exactly half of it. This fails if
        // anyone re-couples the radius to marker size + slop.
        Assert.Equal(44.0, SettlementSelection.MinTargetDiameterPx);
        Assert.Equal(SettlementSelection.MinTargetDiameterPx / 2.0, SettlementSelection.HitRadiusPx);
        // The mark stays a visual choice INSIDE the target (P3: ported from the legacy 20 px MarkerScreenPx constant —
        // the map draws no such marker now): a footprint painted smaller than the minimum is grown to it about its
        // centre, and one painted larger is its own target.
        var small = new Sim.Ui.Render.RectD(93, 93, 14, 14);
        Assert.Equal(new Sim.Ui.Render.RectD(78, 78, 44, 44), SettlementSelection.Target(small));
        var large = new Sim.Ui.Render.RectD(10, 20, 128, 128);
        Assert.Equal(large, SettlementSelection.Target(large));
    }

    [Fact]
    public void HitTest_NamePlate_SelectsItsSettlement_AndOnlyInsideThePlate()
    {
        // One settlement at Regional zoom: its name plate sits under the
        // footprint, beyond the footprint's target. A click inside the plate
        // selects it; just outside misses. (Ported from the legacy label rect:
        // the rect is now the plate the lens painted, not one measured beside
        // a marker.) Proven RED by ignoring the plate in HitTest.
        // PIN MOVED DELIBERATELY (P2: bands by visible km). Old: zoom 6 was Regional on this 256-px world (a third of it
        // in view). New: zoom 6 shows 512 / 6 × 4 = 341 km — Settlement — so the Regional scenario is taken at the fit,
        // zoom 2 (1024 km, the middle of the Regional plateau): the same band, the same plate-under-the-footprint geometry.
        int a = 100 + 100 * 256;
        WorldState world = MarkerWorld(a);
        var cam = new Camera(256);
        cam.Clamp(512, 512);
        cam.ZoomAt(256, 256, 2.0 / cam.Zoom, 512, 512);
        (double sx, double sy) = cam.WorldToScreen(100.5, 100.5, 512, 512);
        Sim.Ui.World.LensFrame f = PaintFrame(world, cam, 512, 512);
        Assert.Equal(Sim.Ui.World.WorldZoom.Regional, f.Zoom);
        Sim.Ui.World.SettlementHit hit = Assert.Single(f.SettlementHits);
        Sim.Ui.Render.RectD plate = hit.NamePlate, target = SettlementSelection.Target(hit.Footprint);
        double py = plate.CenterY;
        Assert.True(py > target.Bottom, "the plate's middle row lies under the footprint's target");

        // Inside the plate, outside the footprint's target:
        Assert.Equal(0, SettlementSelection.HitTest(f, plate.CenterX, py));
        Assert.Equal(0, SettlementSelection.HitTest(f, plate.Right, py));
        // One pixel right of the plate (and below the target): dead ground again.
        Assert.Equal(-1, SettlementSelection.HitTest(f, plate.Right + 1, py));
        // A frame whose plate is empty: the label region is dead (the same hit, no plate).
        Sim.Ui.World.SettlementHit[] noPlate = [hit with { NamePlate = default }];
        Assert.Equal(-1, SettlementSelection.HitTest(noPlate, plate.CenterX, py));
        Assert.Equal(0, SettlementSelection.HitTest(noPlate, sx, sy));
    }

    /// <summary>A hand-built hit: the mark at (x, y), a footprint <paramref name="r"/> px about it, and a plate.</summary>
    private static Sim.Ui.World.SettlementHit Hit(int id, double x, double y, Sim.Ui.Render.RectD plate, double r = 7) =>
        new(id, x, y, new Sim.Ui.Render.RectD(x - r, y - r, 2 * r, 2 * r), plate);

    private static Sim.Ui.Render.RectD Plate(double x0, double y0, double x1, double y1) => new(x0, y0, x1 - x0, y1 - y0);

    [Fact]
    public void HitTest_OverlapRanking_IsMarkerCentreDistance_ThenId_TieDense()
    {
        // The STATED rule: admission by footprint target or name plate, ranking
        // ALWAYS by (mark-centre distance ASC, id ASC). (Ported from the legacy
        // label rects: the same geometry, as painted-hit records.)
        double s0x = 228.5, s0y = 228.5, s1x = s0x + 4;   // ids 0, 1; 4 px apart
        Sim.Ui.Render.RectD far = Plate(-1000, -1000, -999, -999);

        // Both plates cover the SAME strip far to the right of both marks.
        double rx0 = s1x + 60, rx1 = s1x + 160;
        Sim.Ui.World.SettlementHit[] hits =
        [
            Hit(0, s0x, s0y, Plate(rx0, s0y - 8, rx1, s0y + 8)),
            Hit(1, s1x, s0y, Plate(rx0, s0y - 8, rx1, s0y + 8)),
        ];
        // Click inside both plates: settlement 1's mark centre is nearer.
        Assert.Equal(1, SettlementSelection.HitTest(hits, rx0 + 10, s0y));

        // One footprint target vs another's plate: click 10 px right of mark 1
        // (inside its 44 px target) while ALSO inside settlement 0's plate —
        // mark 1's centre is nearer, so 1 wins whichever shape admitted it.
        Sim.Ui.World.SettlementHit[] wide =
        [
            Hit(0, s0x, s0y, Plate(s0x, s0y - 8, s1x + 20, s0y + 8)),
            Hit(1, s1x, s0y, far),
        ];
        Assert.Equal(1, SettlementSelection.HitTest(wide, s1x + 10, s0y));

        // TIE-DENSE: three co-located settlements sharing one plate — bit-exact
        // identical distances at every id; lowest id wins. And with id 0
        // excluded from admission (its plate elsewhere, click outside every
        // target), the tie among {1, 2} picks 1.
        Sim.Ui.Render.RectD shared = Plate(s0x + 60, s0y - 8, s0x + 160, s0y + 8);
        Sim.Ui.World.SettlementHit[] stacked = [Hit(0, s0x, s0y, shared), Hit(1, s0x, s0y, shared), Hit(2, s0x, s0y, shared)];
        Assert.Equal(0, SettlementSelection.HitTest(stacked, s0x + 100, s0y));
        Sim.Ui.World.SettlementHit[] sharedNo0 = [Hit(0, s0x, s0y, far), Hit(1, s0x, s0y, shared), Hit(2, s0x, s0y, shared)];
        Assert.Equal(1, SettlementSelection.HitTest(sharedNo0, s0x + 100, s0y));
    }

    /// <summary>
    /// P3 — TIE-DENSE AND ORDER-FREE: the ranking is the composite key (distance², id), never the order of the frame's
    /// hits. Ten settlements stacked on ONE mark (every distance bit-identical) in every rotation and in reverse: the
    /// lowest admitted id wins, whatever comes first. Then ties at a non-zero distance: eight marks on a circle about
    /// the click at bit-identical squared distances (the four axis points and the four (±3, ±4) points at radius 5),
    /// listed in descending id — the lowest id wins; and a strictly nearer higher id beats them all.
    /// </summary>
    [Fact]
    public void HitTest_TieDense_LowestIdWins_WhateverTheHitOrder()
    {
        Sim.Ui.Render.RectD none = default;
        var stack = new List<Sim.Ui.World.SettlementHit>();
        for (int id = 10; id < 20; id++) stack.Add(Hit(id, 300, 300, none));
        for (int rot = 0; rot < stack.Count; rot++)
        {
            var order = new List<Sim.Ui.World.SettlementHit>();
            for (int i = 0; i < stack.Count; i++) order.Add(stack[(i + rot) % stack.Count]);
            Assert.Equal(10, SettlementSelection.HitTest(order, 301, 302));
            order.Reverse();
            Assert.Equal(10, SettlementSelection.HitTest(order, 301, 302));
        }

        (double Dx, double Dy)[] ring = [(5, 0), (-5, 0), (0, 5), (0, -5), (3, 4), (-3, 4), (3, -4), (-3, -4)];
        var circle = new List<Sim.Ui.World.SettlementHit>();
        for (int i = 0; i < ring.Length; i++) circle.Add(Hit(100 - i, 300 + ring[i].Dx, 300 + ring[i].Dy, none));
        foreach ((double dx, double dy) in ring) Assert.Equal(25.0, dx * dx + dy * dy);   // the squared distances are exact
        Assert.Equal(100 - ring.Length + 1, SettlementSelection.HitTest(circle, 300, 300));
        circle.Reverse();
        Assert.Equal(100 - ring.Length + 1, SettlementSelection.HitTest(circle, 300, 300));
        circle.Add(Hit(500, 302, 301, none));   // nearer (squared distance 5), though its id is the highest
        Assert.Equal(500, SettlementSelection.HitTest(circle, 300, 300));
        // No frame painted yet: nothing is under any click.
        Assert.Equal(-1, SettlementSelection.HitTest((Sim.Ui.World.LensFrame?)null, 300, 300));
    }
}
