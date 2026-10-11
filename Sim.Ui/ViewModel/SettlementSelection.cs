using Sim.Core.State;
using Sim.Ui.Render;
using Sim.Ui.World;

namespace Sim.Ui.ViewModel;

/// <summary>
/// Settlement selection (T2.4, pure view-model — testable headless).
/// Selection is UI STATE ONLY: it never touches WorldState, never serializes,
/// never affects the sim except through the orders the HUD emits for it.
///
/// P3 (M5 map workstream): THE CLICK HITS WHAT WAS DRAWN. The targets are the
/// rects the world lens actually painted last frame (<see cref="LensFrame.SettlementHits"/>):
/// each settlement's footprint — the population mark at World zoom, the built
/// footprint closer, so the target grows with the footprint (up to 150 px) — and
/// its name plate wherever the lens set it. The legacy targets this replaces (a
/// constant 22 px circle about the site cell, and name rects measured to the
/// right of a 20 px marker the map no longer draws) are deleted.
/// </summary>
public static class SettlementSelection
{
    /// <summary>
    /// D-A2 (docs/art-gate-defects.md): the minimum click target this UI
    /// commits to. 44 px is the Apple HIG minimum target size (44 pt) and
    /// WCAG 2.5.5 Target Size (Enhanced); the previous effective target
    /// (22 px: 7 px half-marker + 4 px slop, doubled) sat below even WCAG
    /// 2.5.8's 24 px AA minimum, and the director's practice judgement is
    /// that it failed. The target is DERIVED from the named standard,
    /// not tuned to feel: a footprint painted smaller is grown to it.
    /// </summary>
    public const double MinTargetDiameterPx = 44.0;

    /// <summary>Half the minimum target: a footprint's target reaches at least
    /// this far from its mark's centre along each axis.</summary>
    public const double HitRadiusPx = MinTargetDiameterPx / 2.0;

    /// <summary>The click target of a painted footprint: the footprint's box, grown
    /// about its centre to at least <see cref="MinTargetDiameterPx"/> a side (D-A2).
    /// The mark is the affordance; the target size is the standard's.</summary>
    public static RectD Target(RectD footprint)
    {
        double w = Math.Max(footprint.W, MinTargetDiameterPx), h = Math.Max(footprint.H, MinTargetDiameterPx);
        return new RectD(footprint.CenterX - w / 2, footprint.CenterY - h / 2, w, h);
    }

    /// <summary>Whether (x, y) lies in <paramref name="r"/>, edges included (a click on the
    /// plate's or the target's outline is a click on it).</summary>
    public static bool Inside(RectD r, double x, double y) => x >= r.X && x <= r.Right && y >= r.Y && y <= r.Bottom;

    /// <summary>The settlement under a screen click in the last painted lens frame, or −1
    /// (no frame painted yet: −1).</summary>
    public static int HitTest(LensFrame? frame, double clickSx, double clickSy) =>
        frame is null ? -1 : HitTest(frame.SettlementHits, clickSx, clickSy);

    /// <summary>
    /// The settlement under a screen click, or −1.
    ///
    /// ADMISSION is by either painted shape: inside the footprint's target
    /// (<see cref="Target"/>) or inside the settlement's name plate.
    ///
    /// RANKING is always the composite key (mark-centre screen distance
    /// ASC, settlement id ASC), regardless of which shape admitted the
    /// candidate. Stated rule for every overlap: a click inside two name
    /// plates, or inside one footprint and another settlement's plate,
    /// selects the settlement whose MARK CENTRE is nearer; a bit-exact
    /// distance tie selects the lower id. One continuous key, not a
    /// shape-priority rule, so the ordering is total and stable — whatever
    /// the order of <paramref name="hits"/> — the constitution's (score, id)
    /// tie-break, pinned by a tie-dense test.
    /// </summary>
    public static int HitTest(IReadOnlyList<SettlementHit> hits, double clickSx, double clickSy)
    {
        int bestId = -1;
        double bestDistSq = double.PositiveInfinity;
        for (int i = 0; i < hits.Count; i++)
        {
            SettlementHit h = hits[i];
            if (!Inside(Target(h.Footprint), clickSx, clickSy) && !Inside(h.NamePlate, clickSx, clickSy)) continue;
            double dx = h.X - clickSx, dy = h.Y - clickSy;
            double distSq = dx * dx + dy * dy;
            if (bestId < 0 || distSq < bestDistSq || (distSq == bestDistSq && h.Id < bestId))
            {
                bestDistSq = distSq;
                bestId = h.Id;
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
