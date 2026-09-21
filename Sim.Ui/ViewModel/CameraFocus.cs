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
    public static bool CenterOn(Camera camera, IReadOnlyWorldState world, int settlementId, int viewportW, int viewportH)
    {
        ArgumentNullException.ThrowIfNull(camera);
        ArgumentNullException.ThrowIfNull(world);
        if (world.Terrain is null) return false;
        for (int i = 0; i < world.Settlements.Count; i++)
        {
            if (world.Settlements[i].Id.Value != settlementId) continue;
            LineGeometry.Vertex pos = OverlayMeshes.SettlementPosition(world.Settlements[i], world.Terrain.Size);
            (double sx, double sy) = camera.WorldToScreen(pos.X, pos.Y, viewportW, viewportH);
            camera.Pan(viewportW / 2.0 - sx, viewportH / 2.0 - sy, viewportW, viewportH);
            return true;
        }
        return false;
    }
}
