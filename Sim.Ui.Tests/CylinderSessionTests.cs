using Microsoft.Xna.Framework.Input;
using Sim.Core.Kernel;
using Sim.Core.State;
using Sim.Ui.Headless;
using Sim.Ui.Render;
using Sim.Ui.ViewModel;
using Sim.Ui.World;
using Xunit;

namespace Sim.Ui.Tests;

/// <summary>
/// BATCH 3 WS3 — THE LIVE UI ON A CYLINDER (the canonical world is wrapped: worldgen.json topology.wrapX). The game's
/// camera wraps exactly when the terrain does; it opens at its floor (one circumference across the window) CENTRED on the
/// player's opening settlement; opening and closing a context panel at the floor changes neither the zoom nor the band nor
/// the lens's layers; a drag across the seam in the live UI is continuous with x normalised; a click on a settlement cut by
/// the window's edge selects it at either half; and none of it changes the world hash or the order log.
/// </summary>
[Collection("ImGui context")]
public class CylinderSessionTests : IDisposable
{
    private static string Assets() => Path.Combine(AppContext.BaseDirectory, "assets");

    private readonly string _root = Path.Combine(Path.GetTempPath(),
        "sim-ui-cylinder-" + Environment.ProcessId.ToString(System.Globalization.CultureInfo.InvariantCulture) + "-" + Guid.NewGuid().ToString("N"));

    public void Dispose()
    {
        try { if (Directory.Exists(_root)) Directory.Delete(_root, recursive: true); }
        catch (IOException) { }
        catch (UnauthorizedAccessException) { }
        GC.SuppressFinalize(this);
    }

    private UiFrameHarness Start(string name, UiSession? session = null, int w = 1280, int h = 800) =>
        UiFrameHarness.Start(session ?? UiSession.Start(42), Path.Combine(_root, name), Assets(), width: w, height: h);

    [Fact]
    public void TheGame_OpensAtTheFloor_CentredOnTheOpeningSettlement()
    {
        using UiFrameHarness h = Start("open");
        Camera cam = h.Ui.Camera;
        Assert.True(h.Ui.World.Terrain!.WrapX);
        Assert.True(cam.WrapX);
        Assert.Equal(1280.0 / cam.WorldSize, cam.Zoom);   // the floor: one circumference across the window
        int opening = h.Ui.SelectedSettlement;
        Assert.True(opening >= 0);
        SettlementRow row = default;
        for (int i = 0; i < h.Ui.World.Settlements.Count; i++)
            if (h.Ui.World.Settlements[i].Id.Value == opening) row = h.Ui.World.Settlements[i];
        LineGeometry.Vertex pos = OverlayMeshes.SettlementPosition(row, h.Ui.World.Terrain.Size);
        Assert.Equal(pos.X, cam.CenterX, 9);
        double halfView = h.Ui.MapRect.H / 2.0 / cam.Zoom;
        Assert.Equal(Math.Clamp(pos.Y, halfView, cam.WorldSize - halfView), cam.CenterY, 9);
        // It is on screen, drawn, and hit where it is drawn.
        (double sx, double sy) = cam.WorldToScreenNearest(pos.X, pos.Y, h.Ui.MapRect);
        Assert.True(h.Ui.MapRect.Contains(sx, sy));
        Assert.Contains(opening, h.Ui.LensFrame!.SettlementsDrawn);
        Assert.Equal(WorldZoom.World, h.Ui.LensFrame.Zoom);   // the canonical floor (4096 km) is the World plateau
        Assert.True(h.Ui.LensFrame.Detail.IsPlateau);
    }

    [Fact]
    public void AtTheFloor_OpeningAndClosingAPanel_ChangesNeitherZoomNorBandNorLayers()
    {
        using UiFrameHarness h = Start("panel");
        Camera cam = h.Ui.Camera;
        double zoom = cam.Zoom;
        LensFrame closed = h.Ui.LensFrame!;
        Assert.True(h.ClickControl("nav:Policy"));
        h.Idle(1);
        Assert.True(h.Ui.ContextShown);
        Assert.Equal(zoom, cam.Zoom);
        Assert.Equal(closed.Zoom, h.Ui.LensFrame!.Zoom);
        Assert.Equal(closed.Detail, h.Ui.LensFrame.Detail);
        Assert.Same(closed.Layers, h.Ui.LensFrame.Layers);
        h.Key(Keys.Escape);
        h.Idle(1);
        Assert.False(h.Ui.ContextShown);
        Assert.Equal(zoom, cam.Zoom);
        Assert.Equal(closed.Detail, h.Ui.LensFrame!.Detail);
        Assert.Empty(h.Problems);
    }

    [Fact]
    public void ADragAcrossTheSeam_InTheLiveUi_IsContinuous_AndChangesNoHashOrOrder()
    {
        using UiFrameHarness h = Start("drag");
        Camera cam = h.Ui.Camera;
        string hash = WorldHash.ComputeHex(h.Ui.Session.World);
        int orders = h.Ui.Session.Orders.Count;
        RectD map = h.Ui.MapRect;
        // Put the centre 30 screen px east of the seam, then drag the world right by 60 px: the centre crosses x = 0 westward.
        cam.Stop();
        cam.Pan(-cam.DeltaX(cam.CenterX, 30.0 / cam.Zoom) * cam.Zoom, 0, map);
        h.Idle(1);
        double start = cam.CenterX;
        Assert.Equal(30.0 / cam.Zoom, start, 9);
        h.Drag(map.CenterX, map.CenterY, map.CenterX + 60, map.CenterY);
        h.Idle(1);
        Assert.InRange(cam.CenterX, 0.0, cam.WorldSize);
        Assert.Equal(-60.0 / cam.Zoom, cam.DeltaX(start, cam.CenterX), 6);
        Assert.True(cam.CenterX > cam.WorldSize / 2, "x was not normalised across the seam (west)");
        // And back east across it.
        h.Drag(map.CenterX, map.CenterY, map.CenterX - 120, map.CenterY);
        h.Idle(1);
        Assert.Equal(60.0 / cam.Zoom, cam.DeltaX(start, cam.CenterX), 6);
        Assert.True(cam.CenterX < cam.WorldSize / 2, "x was not normalised across the seam (east)");
        Assert.Equal(hash, WorldHash.ComputeHex(h.Ui.Session.World));
        Assert.Equal(orders, h.Ui.Session.Orders.Count);
        Assert.Empty(h.Problems);
    }

    /// <summary>
    /// Batch 3 R3 (W2 review F6) — THE BUILD PANEL'S CAMERA LINE IN THE LIVE UI READS THE NORMALISED X. The camera centred
    /// 0.4 world px west of the seam (x = W − 0.4) rounds to the seam, which on a cylinder is x = 0 — the line the
    /// developer panel draws must read "camera (0, …", never "camera (1024, …" (GameUi passes the wrap width to
    /// HudModel.CameraLine; the pure function is pinned by CylinderCameraTests, this pins the call site).
    /// </summary>
    [Fact]
    public void TheBuildPanelsCameraLine_ReadsZero_NotTheWorldWidth_JustWestOfTheSeam()
    {
        using UiFrameHarness h = Start("hud");
        Camera cam = h.Ui.Camera;
        Assert.True(cam.WrapX);
        h.Key(Keys.F12);                                     // the developer toggle: DEV joins the command bar
        Assert.True(h.ClickControl("nav:Developer"));
        Assert.True(h.ClickControl("dev-More"));             // BUILD: the glass-box footer with the camera line
        cam.Stop();
        cam.Pan(-cam.DeltaX(cam.CenterX, cam.WorldSize - 0.4) * cam.Zoom, 0, h.Ui.MapRect);
        h.Idle(1);
        Assert.Equal(cam.WorldSize - 0.4, cam.CenterX, 6);
        string zoom = cam.Zoom.ToString("0.00", System.Globalization.CultureInfo.InvariantCulture);
        IReadOnlyList<string> texts = h.Ui.FrameText.Texts();
        string line = Assert.Single(texts, t => t.StartsWith("camera (", StringComparison.Ordinal));
        Assert.StartsWith("camera (0, ", line, StringComparison.Ordinal);
        Assert.EndsWith(") zoom " + zoom + "x", line, StringComparison.Ordinal);
        Assert.Equal(HudModel.CameraLine(cam.CenterX, cam.CenterY, cam.Zoom, cam.WorldSize), line);
        Assert.NotEqual(HudModel.CameraLine(cam.CenterX, cam.CenterY, cam.Zoom), line);   // the flat line would read 1024
        Assert.Empty(h.Problems);
    }

    [Fact]
    public void AClickOnASettlementCutByTheWindowsEdge_SelectsItAtEitherHalf()
    {
        using UiFrameHarness h = Start("cut");
        Camera cam = h.Ui.Camera;
        WorldState w = h.Ui.World;
        int target = -1;
        for (int i = w.Settlements.Count - 1; i >= 0 && target < 0; i--)
            if (w.Settlements[i].Id.Value != h.Ui.SelectedSettlement) target = i;
        Assert.True(target >= 0);
        SettlementRow row = w.Settlements[target];
        LineGeometry.Vertex pos = OverlayMeshes.SettlementPosition(row, w.Terrain!.Size);
        // Centre the camera half a world away (x) and on the settlement's row (y): the window's two edges are its meridian.
        Assert.True(CameraFocus.CenterOn(cam, w, row.Id.Value, h.Ui.MapRect));
        cam.Pan(-cam.DeltaX(cam.CenterX, pos.X + cam.WorldSize / 2.0) * cam.Zoom, 0, h.Ui.MapRect);
        h.Idle(2);
        LensFrame f = h.Ui.LensFrame!;
        SettlementHit[] halves = f.SettlementHits.Where(x => x.Id == row.Id.Value).OrderBy(x => x.X).ToArray();
        Assert.Equal(2, halves.Length);
        Assert.Equal(h.Width, halves[1].X - halves[0].X, 6);   // one window apart: no point painted twice
        Assert.Equal(1, f.SettlementsDrawn.Count(id => id == row.Id.Value));
        Assert.True(f.Copies >= 1);
        h.Click(Math.Max(1, halves[0].X + 1), halves[0].Y);
        Assert.Equal(row.Id.Value, h.Ui.SelectedSettlement);
        h.Key(Keys.Tab);   // select something else, then the other half
        Assert.NotEqual(row.Id.Value, h.Ui.SelectedSettlement);
        h.Click(Math.Min(h.Width - 2, halves[1].X - 1), halves[1].Y);
        Assert.Equal(row.Id.Value, h.Ui.SelectedSettlement);
        Assert.Empty(h.Problems);
    }

    /// <summary>A RESIZE at the floor re-fits to the new window's floor (the window's width travels with the map rect into
    /// Camera.Reframe): one circumference across the new window, never repeated terrain, never a gap.</summary>
    [Fact]
    public void AResizeAtTheFloor_RefitsToTheNewWindowsFloor()
    {
        using UiFrameHarness h = Start("resize");
        Camera cam = h.Ui.Camera;
        Assert.Equal(1280.0 / cam.WorldSize, cam.Zoom);
        h.Gui.Resize(1920, 1009);
        h.Idle(2);
        Assert.Equal(1920.0, cam.WindowWidth);
        Assert.Equal(1920.0 / cam.WorldSize, cam.Zoom);
        RectD win = cam.ScreenRectToWorld(new RectD(0, 0, 1920, 1009), h.Ui.MapRect);
        Assert.Equal(cam.WorldSize, win.W, 9);
    }

    [Fact]
    public void AFlatWorld_KeepsTheFlatCamera()
    {
        using UiFrameHarness h = Start("flat", UiSession.Start(42, 256, 4, wrapXOverride: false));
        Assert.False(h.Ui.World.Terrain!.WrapX);
        Assert.False(h.Ui.Camera.WrapX);
        // The flat fit: the whole world in the map rect's shorter side, as before WS3.
        Assert.Equal(h.Ui.Camera.MinZoom(h.Ui.MapRect), h.Ui.Camera.Zoom);
        Assert.Equal(Math.Min(h.Ui.MapRect.W, h.Ui.MapRect.H) / 256.0, h.Ui.Camera.MinZoom(h.Ui.MapRect));
        Assert.Equal(0, h.Ui.LensFrame!.Copies);
    }
}
