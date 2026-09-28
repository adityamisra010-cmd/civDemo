namespace Sim.Ui.World.Scene;

/// <summary>A clickable shape: a circle (R &gt; 0) or a rectangle (W, H), in screen pixels.</summary>
public readonly record struct HitRegion(WorldEntityId Id, int Priority, double X, double Y, double R, double W, double H, double AnchorX, double AnchorY)
{
    public bool Contains(double x, double y) =>
        R > 0 ? (x - X) * (x - X) + (y - Y) * (y - Y) <= R * R : x >= X && x < X + W && y >= Y && y < Y + H;
}

/// <summary>
/// THE HIT INDEX — every clickable region of a frame, and one total ranking over them.
/// A click is ADMITTED by any region that contains it; admitted candidates rank by
/// (class priority DESC, squared distance to the entity's anchor ASC, WorldEntityId ASC) —
/// the constitution's composite key with a stable id tie-break. Painters draw each layer in
/// DESCENDING id order, so the lowest id is on top — the same entity the ranking picks.
/// Ghosted (remembered) and hidden entities register no region.
/// </summary>
public sealed class HitIndex
{
    public const int AgentPriority = 5, StructurePriority = 4, ResourcePriority = 3, NodePriority = 2, SettlementPriority = 1, EdgePriority = 0;

    private readonly List<HitRegion> _regions = [];
    public IReadOnlyList<HitRegion> Regions => _regions;

    public void Add(HitRegion r) => _regions.Add(r);

    public WorldEntityId? HitTest(double x, double y)
    {
        HitRegion? best = null;
        double bestD = double.PositiveInfinity;
        foreach (HitRegion r in _regions)
        {
            if (!r.Contains(x, y)) continue;
            double dx = r.AnchorX - x, dy = r.AnchorY - y, d = dx * dx + dy * dy;
            if (best is not HitRegion b
                || r.Priority > b.Priority
                || (r.Priority == b.Priority && (d < bestD || (d == bestD && r.Id.CompareTo(b.Id) < 0))))
            {
                best = r;
                bestD = d;
            }
        }
        return best?.Id;
    }
}
