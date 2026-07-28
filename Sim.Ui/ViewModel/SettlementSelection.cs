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
    /// <summary>
    /// D-A2 (docs/art-gate-defects.md): the minimum click target this UI
    /// commits to. 44 px is the Apple HIG minimum target size (44 pt) and
    /// WCAG 2.5.5 Target Size (Enhanced); the previous effective target
    /// (22 px: 7 px half-marker + 4 px slop, doubled) sat below even WCAG
    /// 2.5.8's 24 px AA minimum, and the director's practice judgement is
    /// that it failed. The radius is DERIVED from the named standard —
    /// target diameter / 2 — not tuned to feel.
    /// </summary>
    public const double MinTargetDiameterPx = 44.0;

    /// <summary>Marker screen diameter (must match SimUiGame's markerPx).
    /// A VISUAL choice, deliberately smaller than the hit target: the ink is
    /// the affordance; the target size is the standard's.</summary>
    public const double MarkerScreenPx = 20.0;

    /// <summary>Hit radius in screen px, derived from the named target
    /// standard — no longer marker-size + slop.</summary>
    public const double HitRadiusPx = MinTargetDiameterPx / 2.0;

    /// <summary>A settlement name label's screen rectangle, computed by the
    /// RENDERER (which owns font metrics) and handed IN, so this view-model
    /// stays pure — no ImGui type crosses this boundary. Aligned by
    /// settlement ROW index with the world's settlement table.</summary>
    public readonly record struct LabelRect(double X0, double Y0, double X1, double Y1)
    {
        public bool Contains(double x, double y) => x >= X0 && x <= X1 && y >= Y0 && y <= Y1;
    }

    /// <summary>
    /// The settlement under a screen click, or −1.
    ///
    /// ADMISSION is by either shape: within <see cref="HitRadiusPx"/> of the
    /// marker centre, or inside the settlement's own label rect (D-A2: the
    /// label region was previously dead to the mouse).
    ///
    /// RANKING is always the composite key (marker-centre screen distance
    /// ASC, settlement id ASC), regardless of which shape admitted the
    /// candidate. Stated rule for every overlap: a click inside two label
    /// rects, or inside one marker's radius and another settlement's label,
    /// selects the settlement whose MARKER CENTRE is nearer; a bit-exact
    /// distance tie selects the lower id. One continuous key, not a
    /// shape-priority rule, so the ordering is total and stable — the
    /// constitution's (score, id) tie-break, pinned by a tie-dense test.
    /// </summary>
    public static int HitTest(
        IReadOnlyWorldState world, Camera camera, double clickSx, double clickSy,
        int viewportW, int viewportH,
        System.Collections.Generic.IReadOnlyList<LabelRect>? labelRects = null)
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

            bool admitted = distSq <= HitRadiusPx * HitRadiusPx
                || (labelRects is not null && i < labelRects.Count
                    && labelRects[i].Contains(clickSx, clickSy));
            if (!admitted) continue;

            int id = world.Settlements[i].Id.Value;
            // Explicit (distance ASC, id ASC) — the tie-break no longer leans
            // on rows happening to ascend by id.
            if (distSq < bestDistSq || (distSq == bestDistSq && (bestId < 0 || id < bestId)))
            {
                bestDistSq = distSq;
                bestId = id;
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
