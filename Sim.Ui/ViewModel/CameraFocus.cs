using Sim.Core.State;

namespace Sim.Ui.ViewModel;

/// <summary>
/// T4.19 lane B — CENTRE THE CAMERA ON A SETTLEMENT, in the camera's own
/// vocabulary. <see cref="Camera"/> has no CenterOn and lane B does not add
/// one: the world position comes from <see cref="OverlayMeshes.SettlementPosition"/>
/// (the same function the marker pass draws with), its screen position from
/// <see cref="Camera.WorldToScreen"/>, and the move is a <see cref="Camera.Pan"/>
/// by the screen delta to the viewport centre — so the clamp the camera
/// already applies (never off the world's edge) is the clamp this gets, and
/// a settlement near the edge lands as close to the centre as the world
/// allows. Pure double math; testable headless like CameraTests.
/// </summary>
public static class CameraFocus
{
    /// <summary>Pans so the settlement sits at the viewport centre (or as near
    /// as the camera's clamp permits). Returns false for an id not in the world
    /// or a world without terrain; the camera is then untouched.</summary>
    public static bool CenterOn(Camera camera, IReadOnlyWorldState world, int settlementId, int viewportW, int viewportH) =>
        CenterOn(camera, world, settlementId, Camera.Window(viewportW, viewportH));

    /// <summary>P1: pans so the settlement sits at the centre of the MAP RECT — the clear map the chrome leaves, not
    /// the window (or as near as the camera's clamp against that rect permits). P2: IMMEDIATE — an ease in progress is
    /// ended where it is (<see cref="Camera.Stop"/>) first, so the camera is settled with the settlement centred.</summary>
    public static bool CenterOn(Camera camera, IReadOnlyWorldState world, int settlementId, Sim.Ui.Render.RectD view)
    {
        ArgumentNullException.ThrowIfNull(camera);
        ArgumentNullException.ThrowIfNull(world);
        if (Position(world, settlementId) is not { } pos) return false;
        camera.Stop();
        (double sx, double sy) = camera.WorldToScreen(pos.X, pos.Y, view);
        camera.Pan(view.CenterX - sx, view.CenterY - sy, view);
        return true;
    }

    /// <summary>P2: the EASED centring — the target view is panned so the settlement sits at the map rect's centre once
    /// the ease settles (as near as the clamp permits at the target zoom); the shown view glides there. The game's
    /// "select and centre" rows use it. Returns false (camera untouched) as <see cref="CenterOn(Camera, IReadOnlyWorldState, int, Sim.Ui.Render.RectD)"/>.</summary>
    public static bool GlideTo(Camera camera, IReadOnlyWorldState world, int settlementId, Sim.Ui.Render.RectD view)
    {
        ArgumentNullException.ThrowIfNull(camera);
        ArgumentNullException.ThrowIfNull(world);
        if (Position(world, settlementId) is not { } pos) return false;
        camera.GlidePan((camera.TargetCenterX - pos.X) * camera.TargetZoom, (camera.TargetCenterY - pos.Y) * camera.TargetZoom, view);
        return true;
    }

    private static LineGeometry.Vertex? Position(IReadOnlyWorldState world, int settlementId)
    {
        if (world.Terrain is null) return null;
        for (int i = 0; i < world.Settlements.Count; i++)
            if (world.Settlements[i].Id.Value == settlementId)
                return OverlayMeshes.SettlementPosition(world.Settlements[i], world.Terrain.Size);
        return null;
    }
}
