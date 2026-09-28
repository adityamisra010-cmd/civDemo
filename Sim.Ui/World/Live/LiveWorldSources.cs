using Sim.Core.State;

namespace Sim.Ui.World.Live;

/// <summary>
/// THE LIVE ADAPTERS — the authoritative plug-in point (docs/architecture/world-visualization.md §3).
///
/// Each maps rows the simulation ALREADY holds onto the view's report types, read-only,
/// through <see cref="IReadOnlyWorldState"/> (a view whose tables do not compile a write).
/// They derive nothing that is a gameplay rule: a settlement's population is the sum of its
/// buckets' conserved counts (exactly what the HUD shows), its polity is the controller
/// <see cref="EmpireQuery.TryGetController"/> answers, a notable's place is its settlement.
///
/// This is the only namespace of the world view that sees a Sim.Core type (pinned by
/// WorldViewBoundaryTests). Everything the simulation does NOT hold — buildings, institutions,
/// armies, roles, cultural groups, fog — has no live adapter: <see cref="LiveWorld.Sources"/>
/// wires an <see cref="EmptySource"/> that says so, and the map shows nothing rather than
/// inventing it.
///
/// Snapshots are rebuilt only when the world object or its turn changes; between those the
/// same immutable snapshot is returned, so a frame never re-reads a table it already read.
/// </summary>
public static class LiveWorld
{
    public const string Label = "LIVE simulation";
    public const string NoVisibilityModel = "visibility: reported as visible — the simulation has no visibility model yet";

    /// <param name="world">The current authoritative world (the session's, read-only).</param>
    /// <param name="settlementName">The session's name registry (settlement id → name).</param>
    /// <param name="latticeNodeCenter">Lattice node → world point, from the host's lattice (the
    /// game builds it for the map); null when not available, and then no links are reported.</param>
    public static WorldSources Sources(Func<IReadOnlyWorldState> world, Func<int, string> settlementName,
        Func<int, WorldPoint>? latticeNodeCenter)
    {
        var settlements = new LiveSettlementSource(world, settlementName);
        return new WorldSources(Label, IsPlaceholder: false,
            settlements,
            new LivePolitySource(world),
            new EmptySource("no authoritative producer: the simulation has no buildings or institutions yet"),
            latticeNodeCenter is null
                ? new EmptySource("no lattice geometry supplied by the host")
                : new LiveNetworkSource(world, latticeNodeCenter),
            new LiveNotableSource(world));
    }

    internal static WorldPoint SettlementPosition(IReadOnlyWorldState w, SettlementId id)
    {
        int size = w.Terrain?.Size ?? 0;
        for (int i = 0; i < w.Settlements.Count; i++)
        {
            SettlementRow s = w.Settlements[i];
            if (s.Id.Value != id.Value) continue;
            return size > 0 ? new WorldPoint(s.SiteCell % size + 0.5, s.SiteCell / size + 0.5) : new WorldPoint(0, 0);
        }
        return new WorldPoint(0, 0);
    }
}

/// <summary>Caches one snapshot per (world object, turn).</summary>
internal abstract class LiveSnapshotCache<T> where T : class
{
    private readonly Func<IReadOnlyWorldState> _world;
    private object? _seenWorld;
    private long _seenTurn = long.MinValue;
    private long _sequence;
    private Snapshot<T>? _cache;

    protected LiveSnapshotCache(Func<IReadOnlyWorldState> world) => _world = world;

    public Snapshot<T> Current
    {
        get
        {
            IReadOnlyWorldState w = _world();
            if (_cache is null || !ReferenceEquals(w, _seenWorld) || w.Clock.Turn != _seenTurn)
            {
                _seenWorld = w;
                _seenTurn = w.Clock.Turn;
                _sequence++;
                _cache = Build(w, _sequence);
            }
            return _cache;
        }
    }

    protected abstract Snapshot<T> Build(IReadOnlyWorldState w, long sequence);
}

internal sealed class LiveSettlementSource : LiveSnapshotCache<SettlementReport>, ISettlementViewSource
{
    private readonly Func<int, string> _name;
    public LiveSettlementSource(Func<IReadOnlyWorldState> world, Func<int, string> name) : base(world) => _name = name;

    protected override Snapshot<SettlementReport> Build(IReadOnlyWorldState w, long sequence)
    {
        int n = w.Settlements.Count;
        var population = new long[n];
        for (int b = 0; b < w.Buckets.Count; b++)
        {
            BucketRow row = w.Buckets[b];
            for (int s = 0; s < n; s++)
                if (w.Settlements[s].Id.Value == row.Settlement.Value) { population[s] += row.Count.Value; break; }
        }
        var items = new List<SettlementReport>(n);
        for (int s = 0; s < n; s++)
        {
            SettlementId id = w.Settlements[s].Id;
            string? polity = EmpireQuery.TryGetController(w, id, out PolityId p) ? p.Value.ToString(System.Globalization.CultureInfo.InvariantCulture) : null;
            items.Add(new SettlementReport(
                id.Value.ToString(System.Globalization.CultureInfo.InvariantCulture), _name(id.Value), polity,
                LiveWorld.SettlementPosition(w, id), population[s], ReportedVisibility.Visible,
                "population = Σ bucket counts; polity = controller; " + LiveWorld.NoVisibilityModel));
        }
        return new Snapshot<SettlementReport>(sequence, LiveWorld.Label, false, items, r => r.Key);
    }
}

internal sealed class LivePolitySource : LiveSnapshotCache<PolityReport>, IPolityViewSource
{
    public LivePolitySource(Func<IReadOnlyWorldState> world) : base(world) { }

    protected override Snapshot<PolityReport> Build(IReadOnlyWorldState w, long sequence)
    {
        var items = new List<PolityReport>(w.Polities.Count);
        for (int i = 0; i < w.Polities.Count; i++)
        {
            PolityRow p = w.Polities[i];
            string key = p.Id.Value.ToString(System.Globalization.CultureInfo.InvariantCulture);
            string name = "Polity #" + key + (p.Source == CommandSource.Player ? " (player)" : "");
            items.Add(new PolityReport(key, name, "the simulation names no polities yet: id shown"));
        }
        return new Snapshot<PolityReport>(sequence, LiveWorld.Label, false, items, r => r.Key);
    }
}

/// <summary>Notables (people, T4.8) as person agents at their settlement. A vacated slot
/// (count 0: died or defected, per NotableRow) is not a person and is not reported. The
/// simulation gives people no continuous position and no role yet, so the location is the
/// settlement's and the display type is the generic "person".</summary>
internal sealed class LiveNotableSource : LiveSnapshotCache<AgentReport>, IMobileAgentViewSource
{
    public LiveNotableSource(Func<IReadOnlyWorldState> world) : base(world) { }

    protected override Snapshot<AgentReport> Build(IReadOnlyWorldState w, long sequence)
    {
        var items = new List<AgentReport>();
        for (int i = 0; i < w.Notables.Count; i++)
        {
            NotableRow n = w.Notables[i];
            if (n.Count.Value <= 0) continue;
            string key = "notable-" + n.Id.Value.ToString(System.Globalization.CultureInfo.InvariantCulture);
            items.Add(new AgentReport(key, "Notable #" + n.Id.Value.ToString(System.Globalization.CultureInfo.InvariantCulture), "person",
                n.Allegiance.Value.ToString(System.Globalization.CultureInfo.InvariantCulture),
                LiveWorld.SettlementPosition(w, n.Settlement), null, n.Count.Value, null, ReportedVisibility.Visible,
                "position = the notable's settlement (no continuous position or role in the simulation yet); " + LiveWorld.NoVisibilityModel));
        }
        return new Snapshot<AgentReport>(sequence, LiveWorld.Label, false, items, r => r.Key);
    }
}

/// <summary>The built transport network (NetworkEdges, T1.3) as transport-link infrastructure.</summary>
internal sealed class LiveNetworkSource : LiveSnapshotCache<InfrastructureReport>, IInfrastructureViewSource
{
    private readonly Func<int, WorldPoint> _center;
    public LiveNetworkSource(Func<IReadOnlyWorldState> world, Func<int, WorldPoint> center) : base(world) => _center = center;

    protected override Snapshot<InfrastructureReport> Build(IReadOnlyWorldState w, long sequence)
    {
        var items = new List<InfrastructureReport>(w.NetworkEdges.Count);
        for (int i = 0; i < w.NetworkEdges.Count; i++)
        {
            NetworkEdgeRow e = w.NetworkEdges[i];
            int a = LatticeNode(w, e.A), b = LatticeNode(w, e.B);
            if (a < 0 || b < 0) continue;
            string key = "edge-" + e.Id.Value.ToString(System.Globalization.CultureInfo.InvariantCulture);
            items.Add(new InfrastructureReport(key, "Transport link #" + e.Id.Value.ToString(System.Globalization.CultureInfo.InvariantCulture),
                "transport-link", null, null, [_center(a), _center(b)],
                [new ReportedInput("edgeType", e.EdgeType)], ReportedVisibility.Visible,
                "an edge of the built transport network; " + LiveWorld.NoVisibilityModel));
        }
        return new Snapshot<InfrastructureReport>(sequence, LiveWorld.Label, false, items, r => r.Key);
    }

    private static int LatticeNode(IReadOnlyWorldState w, NetworkNodeId id)
    {
        for (int i = 0; i < w.NetworkNodes.Count; i++)
            if (w.NetworkNodes[i].Id.Value == id.Value) return w.NetworkNodes[i].LatticeNode;
        return -1;
    }
}
