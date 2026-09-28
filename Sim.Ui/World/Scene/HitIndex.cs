namespace Sim.Ui.World.Scene;

/// <summary>A clickable shape: a circle (R &gt; 0) or a rectangle (W, H), in screen pixels.
/// <see cref="ByDistance"/> marks a region LARGER than what is drawn (a settlement's 44 px
/// target around a small core): among those, the nearer anchor wins, as SettlementSelection does.</summary>
public readonly record struct HitRegion(WorldEntityId Id, int Priority, double X, double Y, double R, double W, double H, double AnchorX, double AnchorY,
    bool ByDistance = false)
{
    public bool Contains(double x, double y) =>
        R > 0 ? (x - X) * (x - X) + (y - Y) * (y - Y) <= R * R : x >= X && x < X + W && y >= Y && y < Y + H;
}

/// <summary>
/// THE HIT INDEX — every clickable region of a frame, and one total ranking over them.
/// A click is ADMITTED by any region that contains it. Admitted candidates rank by class
/// priority DESC, and the classes follow the LAYER order (a later layer is drawn over an earlier
/// one): stacked labels, agents, resources, structures, infrastructure, settlements. Within a
/// class the region is what is drawn, and painters draw each layer in DESCENDING id order, so
/// the LOWEST id is on top at any shared pixel — and the lowest id wins: what is clicked is what
/// is seen. The one exception is a region larger than its drawing (ByDistance: a settlement's
/// standard-size target), ranked (squared distance ASC, id ASC) — the constitution's composite
/// key. Ghosted (remembered) and hidden entities register no region.
/// </summary>
public sealed class HitIndex
{
    public const int LabelPriority = 6, AgentPriority = 5, ResourcePriority = 4, StructurePriority = 3, NodePriority = 2, EdgePriority = 2,
        SettlementPriority = 1;

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
            double dx = r.AnchorX - x, dy = r.AnchorY - y, d = r.ByDistance ? dx * dx + dy * dy : 0.0;
            bool better = best is not HitRegion b
                || r.Priority > b.Priority
                || (r.Priority == b.Priority && (d < bestD || (d == bestD && r.Id.CompareTo(b.Id) < 0)));
            if (better)
            {
                best = r;
                bestD = d;
            }
        }
        return best?.Id;
    }
}
