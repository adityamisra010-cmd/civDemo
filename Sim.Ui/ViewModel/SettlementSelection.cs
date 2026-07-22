using Sim.Core.State;

namespace Sim.Ui.ViewModel;

/// <summary>
/// Settlement selection (T2.4, pure view-model — testable headless).
/// Selection is UI STATE ONLY: it never touches WorldState, never serializes,
/// never affects the sim except through the orders the HUD emits for it.
///
/// HIT RADIUS, zoom-aware by construction: markers render at a CONSTANT
/// screen size (14 px — SimUiGame draws them in the untransformed screen-space
/// pass), so the hit test uses a constant SCREEN radius (marker half-size + a
/// 4 px slop ring). In world units that radius shrinks as the camera zooms in
/// — clickable at the world-fit minimum zoom, precise at 32× — which is
/// exactly the behavior "zoom-aware" names.
/// </summary>
public static class SettlementSelection
{
    /// <summary>Marker screen diameter (must match SimUiGame's markerPx).</summary>
    public const double MarkerScreenPx = 14.0;

    /// <summary>Hit radius in screen px: marker half-size + 4 px slop.</summary>
    public const double HitRadiusPx = MarkerScreenPx / 2.0 + 4.0;

    /// <summary>
    /// The settlement under a screen click, or −1. Composite key (screen
    /// distance ASC, settlement id ASC): the NEAREST marker within the radius
    /// wins; a bit-exact distance tie (overlapping markers) goes to the lower
    /// id — the constitution's stable tie-break, pinned by a tie-dense test.
    /// </summary>
    public static int HitTest(
        IReadOnlyWorldState world, Camera camera, double clickSx, double clickSy,
        int viewportW, int viewportH)
    {
        if (world.Terrain is null) return -1;
        int terrainSize = world.Terrain.Size;

        int bestId = -1;
        double bestDistSq = double.PositiveInfinity;
        for (int i = 0; i < world.Settlements.Count; i++)
        {
            LineGeometry.Vertex pos =
                OverlayMeshes.SettlementPosition(world.Settlements[i], terrainSize);
            (double sx, double sy) = camera.WorldToScreen(pos.X, pos.Y, viewportW, viewportH);
            double dx = sx - clickSx, dy = sy - clickSy;
            double distSq = dx * dx + dy * dy;
            if (distSq > HitRadiusPx * HitRadiusPx) continue;
            // Strictly-less on an ascending-id scan = (distance ASC, id ASC).
            if (distSq < bestDistSq)
            {
                bestDistSq = distSq;
                bestId = world.Settlements[i].Id.Value;
            }
        }
        return bestId;
    }

    /// <summary>
    /// Tab cycling: the next settlement in id order after <paramref name="currentId"/>,
    /// wrapping; with none selected (−1) or an unknown id, the first settlement.
    /// Settlement rows are in ascending id order by founding construction, but
    /// the scan derives the successor from the IDS, not the row order.
    /// </summary>
    public static int CycleNext(IReadOnlyWorldState world, int currentId)
    {
        if (world.Settlements.Count == 0) return -1;

        int lowest = int.MaxValue, bestAbove = int.MaxValue;
        for (int i = 0; i < world.Settlements.Count; i++)
        {
            int id = world.Settlements[i].Id.Value;
            if (id < lowest) lowest = id;
            if (id > currentId && id < bestAbove) bestAbove = id;
        }
        return bestAbove != int.MaxValue ? bestAbove : lowest;
    }
}
