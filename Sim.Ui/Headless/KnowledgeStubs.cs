using Sim.Core.State;
using Sim.Ui.ViewModel;

namespace Sim.Ui.Headless;

/// <summary>
/// M5 map workstream P7 — THE KNOWLEDGE STUB the leak tests and the headless gate's hidden-rival rig see a world
/// through. Harness code (it ships with the exe only because <c>--smoke</c> does): the ONE production-side caller of
/// <see cref="PolityKnowledge.Restricted"/>, pinned so by the source guard (Sim.Ui.Tests KnowledgeLeakTests). It never
/// changes the world, an order or a hash — only what the UI shows.
/// </summary>
public static class KnowledgeStubs
{
    /// <summary>
    /// What <paramref name="player"/> knows of <paramref name="world"/> when <paramref name="hidden"/> is hidden whole:
    /// every lattice node under one of that polity's settlements (its site's block and its catchment nodes) or one of
    /// its formations is unknown and unseen; every other node is known and visible — except that a node holding another
    /// polity's (or a stateless) settlement site is never hidden, so exactly the one polity disappears. Recomputed from
    /// each world it is given (UiSession calls it after every step), so a colony the polity founds is hidden too.
    /// </summary>
    public static PlayerKnowledge HidePolity(IReadOnlyWorldState world, PolityId player, PolityId hidden) =>
        Over(world, player, HiddenMask(world, player, hidden));

    /// <summary>The known-node mask of <see cref="HidePolity"/> (true = known and visible), one entry per lattice node.
    /// A test that perturbs the hidden polity in ways that move its own footprint (a control change) keeps THIS mask
    /// fixed and applies it to each world with <see cref="Over"/>.</summary>
    public static bool[] HiddenMask(IReadOnlyWorldState world, PolityId player, PolityId hidden)
    {
        ArgumentNullException.ThrowIfNull(world);
        if (world.Terrain is not { } terrain) throw new ArgumentException("the stub needs a world with terrain", nameof(world));
        if (hidden.Value == player.Value) throw new ArgumentException("the player cannot be hidden from itself", nameof(hidden));
        PolityKnowledge all = PolityKnowledge.Of(world, player);   // the grid geometry only (NodeOfCell / NodeAt)
        (int lsize, _) = PolityKnowledge.Grid(terrain.Size);
        var hide = new bool[lsize * lsize];
        var keep = new bool[lsize * lsize];
        for (int i = 0; i < world.Settlements.Count; i++)
        {
            SettlementRow row = world.Settlements[i];
            int node = all.NodeOfCell(row.SiteCell);
            if (node < 0) continue;
            if (Holds(world, hidden, row.Id)) hide[node] = true;
            else keep[node] = true;
        }
        for (int i = 0; i < world.CatchmentNodes.Count; i++)
        {
            CatchmentNodeRow row = world.CatchmentNodes[i];
            if ((uint)row.LatticeNode < (uint)hide.Length && Holds(world, hidden, row.Settlement)) hide[row.LatticeNode] = true;
        }
        for (int i = 0; i < world.MilitaryUnits.Count; i++)
        {
            MilitaryUnitRow u = world.MilitaryUnits[i];
            if (u.Owner.Value != hidden.Value) continue;
            int node = all.NodeAt(u.X, u.Y);
            if (node >= 0) hide[node] = true;
        }
        var known = new bool[hide.Length];
        for (int n = 0; n < known.Length; n++) known[n] = !hide[n] || keep[n];
        return known;
    }

    /// <summary>What <paramref name="player"/> knows of <paramref name="world"/> under a fixed known-node mask (known and
    /// visible alike).</summary>
    public static PlayerKnowledge Over(IReadOnlyWorldState world, PolityId player, bool[] known)
    {
        ArgumentNullException.ThrowIfNull(known);
        return PlayerKnowledge.From(world, PolityKnowledge.Restricted(world, player, known, known));
    }

    /// <summary>The settlements <paramref name="polity"/> controls in <paramref name="world"/>, in table order.</summary>
    public static IReadOnlyList<int> SettlementsOf(IReadOnlyWorldState world, PolityId polity)
    {
        var ids = new List<int>();
        for (int i = 0; i < world.Settlements.Count; i++)
            if (Holds(world, polity, world.Settlements[i].Id)) ids.Add(world.Settlements[i].Id.Value);
        return ids;
    }

    private static bool Holds(IReadOnlyWorldState world, PolityId polity, SettlementId s) =>
        EmpireQuery.TryGetController(world, s, out PolityId ruler) && ruler.Value == polity.Value;
}
