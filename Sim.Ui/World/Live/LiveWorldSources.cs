using System.Globalization;
using Sim.Core.State;
using Sim.Core.Systems;
using Sim.Ui.ViewModel;

namespace Sim.Ui.World.Live;

/// <summary>
/// THE LIVE ADAPTERS — the authoritative plug-in point (docs/architecture/world-visualization.md §3).
///
/// Each maps rows the simulation ALREADY holds onto the view's reports, read-only, through
/// <see cref="IReadOnlyWorldState"/> (a view whose tables do not compile a write). They derive
/// no gameplay rule: a settlement's population is the sum of its buckets' conserved counts
/// (what the HUD shows), its size is the simulation's own SizeTier (D-017), its polity is the
/// controller EmpireQuery answers, a structure is an M4-D StructureRow (a COUNT per settlement
/// and project), a resource is a deposit, the infrastructure is the built network graph, a
/// person is a living notable at their settlement.
///
/// What the simulation does NOT hold — institutions beyond the M4-D projects, armies, roles
/// for people, cultural groups, a knowledge model — has no live report: the map shows nothing
/// rather than inventing it. Every live report's visibility is NotModelled (the live view is
/// omniscient because there is no knowledge model, D-040 B1 — not because anything is known).
///
/// This is the only namespace of the world view that sees a Sim.Core type (pinned by
/// WorldViewBoundaryTests). Keys are the authoritative ids, zero-padded to ten digits so
/// that ordinal order is numeric order (the (score, id) tie-break agrees with the game's).
/// </summary>
public static class LiveWorld
{
    public const string Label = "LIVE simulation";
    public const string Observer = "omniscient - the simulation has no knowledge model yet (D-040 B1)";
    public const string VisibilityNote = "visibility not modelled: omniscient view";

    /// <param name="world">The current authoritative world (the session's, read-only).</param>
    /// <param name="settlementName">The session's name registry (settlement id → name).</param>
    /// <param name="goods">The loaded goods registry (project and good names).</param>
    /// <param name="latticeNodeCenter">Lattice node → world point, from the host's lattice
    /// (the game builds it for the map); null when not available, and then no network is reported.</param>
    public static WorldSources Sources(Func<IReadOnlyWorldState> world, Func<int, string> settlementName, GoodsConfig goods,
        Func<int, WorldPoint>? latticeNodeCenter)
    {
        IInfrastructureViewSource network = latticeNodeCenter is null
            ? new EmptySource("no lattice geometry supplied by the host")
            : new LiveNetworkSource(world, latticeNodeCenter);
        return new WorldSources(Label, IsPlaceholder: false, Observer,
            new LiveSettlementSource(world, settlementName),
            new LivePolitySource(world),
            new LiveStructureSource(world, goods, settlementName),
            network,
            new LiveResourceSource(world, goods),
            new LiveNotableSource(world));
    }

    /// <summary>An authoritative id as a key: ten digits, invariant, zero-padded.</summary>
    public static string Key(int id) => id.ToString("D10", CultureInfo.InvariantCulture);

    internal static string N(long v) => v.ToString(CultureInfo.InvariantCulture);

    /// <summary>A settlement's map position (its site cell centre — the same point the game's
    /// marker uses), or null when the row or the terrain is missing. Never a (0,0) stand-in.</summary>
    internal static WorldPoint? SettlementPosition(IReadOnlyWorldState w, SettlementId id)
    {
        if (w.Terrain is null) return null;
        for (int i = 0; i < w.Settlements.Count; i++)
            if (w.Settlements[i].Id.Value == id.Value)
            {
                LineGeometry.Vertex v = OverlayMeshes.SettlementPosition(w.Settlements[i], w.Terrain.Size);
                return new WorldPoint(v.X, v.Y);
            }
        return null;
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
                _cache = new Snapshot<T>(_sequence, LiveWorld.Label, false, Build(w), Key);
            }
            return _cache;
        }
    }

    protected abstract IEnumerable<T> Build(IReadOnlyWorldState w);
    protected abstract string Key(T item);
}

internal sealed class LiveSettlementSource(Func<IReadOnlyWorldState> world, Func<int, string> name)
    : LiveSnapshotCache<SettlementReport>(world), ISettlementViewSource
{
    protected override string Key(SettlementReport r) => r.Key;

    protected override IEnumerable<SettlementReport> Build(IReadOnlyWorldState w)
    {
        int n = w.Settlements.Count;
        var items = new List<SettlementReport>(n);
        for (int s = 0; s < n; s++)
        {
            SettlementId id = w.Settlements[s].Id;
            if (LiveWorld.SettlementPosition(w, id) is not WorldPoint position) continue;
            long population = 0;
            for (int b = 0; b < w.Buckets.Count; b++)
                if (w.Buckets[b].Settlement.Value == id.Value) population += w.Buckets[b].Count.Value;
            var inputs = new List<ReportedInput> { new("population", population) };
            for (int c = 0; c < w.CatchmentSummaries.Count; c++)
                if (w.CatchmentSummaries[c].Settlement.Value == id.Value) { inputs.Add(new("sizeTier", w.CatchmentSummaries[c].SizeTier)); break; }
            for (int h = 0; h < w.Housing.Count; h++)
                if (w.Housing[h].Settlement.Value == id.Value) { inputs.Add(new("dwellings", w.Housing[h].Dwellings.Value)); break; }
            string? polity = EmpireQuery.TryGetController(w, id, out PolityId p) ? LiveWorld.Key(p.Value) : null;
            items.Add(new SettlementReport(LiveWorld.Key(id.Value), name(id.Value), polity,
                polity is null ? PolityRelation.None : PolityRelation.Controller, position, inputs, ReportedVisibility.NotModelled,
                "population = sum of bucket counts; sizeTier = CatchmentSummary.SizeTier; dwellings = Housing; polity = controller; "
                + LiveWorld.VisibilityNote, id.Value));
        }
        return items;
    }
}

internal sealed class LivePolitySource(Func<IReadOnlyWorldState> world)
    : LiveSnapshotCache<PolityReport>(world), IPolityViewSource
{
    protected override string Key(PolityReport r) => r.Key;

    protected override IEnumerable<PolityReport> Build(IReadOnlyWorldState w)
    {
        var items = new List<PolityReport>(w.Polities.Count);
        for (int i = 0; i < w.Polities.Count; i++)
        {
            PolityRow p = w.Polities[i];
            string name = "Polity #" + LiveWorld.N(p.Id.Value) + (p.Source == CommandSource.Player ? " (player)" : "");
            items.Add(new PolityReport(LiveWorld.Key(p.Id.Value), name, p.Id.Value, "the simulation names no polities yet: id shown"));
        }
        return items;
    }
}

/// <summary>The M4-D structures: one report per StructureRow with a positive count. The row
/// is a count per (settlement, project) — so the report is ONE entity with that multiplicity,
/// never expanded into invented per-building ids; it has no Established ordinal and no
/// polity (M4-D: the queue and the structures belong to the settlement).</summary>
internal sealed class LiveStructureSource(Func<IReadOnlyWorldState> world, GoodsConfig goods, Func<int, string> name)
    : LiveSnapshotCache<StructureReport>(world), IStructureViewSource
{
    protected override string Key(StructureReport r) => r.Key;

    protected override IEnumerable<StructureReport> Build(IReadOnlyWorldState w)
    {
        var items = new List<StructureReport>();
        for (int i = 0; i < w.Structures.Count; i++)
        {
            StructureRow row = w.Structures[i];
            if (row.Count <= 0) continue;
            string project = goods.ProjectById(row.ProjectId)?.Name ?? "project-" + LiveWorld.N(row.ProjectId);
            string display = char.ToUpperInvariant(project[0]) + project[1..] + " at " + name(row.Settlement.Value);
            items.Add(new StructureReport(
                LiveWorld.Key(row.Settlement.Value) + "-p" + LiveWorld.Key(row.ProjectId), display, project,
                LiveWorld.Key(row.Settlement.Value), null, PolityRelation.None, null, row.Count, [], [], null, null,
                ReportedVisibility.NotModelled,
                "M4-D StructureRow: a count per (settlement, project), owned by the settlement; no per-building identity, "
                + "no establishment order, no state; " + LiveWorld.VisibilityNote));
        }
        return items;
    }
}

/// <summary>Deposits (T3.2) as resources at their settlement.</summary>
internal sealed class LiveResourceSource(Func<IReadOnlyWorldState> world, GoodsConfig goods)
    : LiveSnapshotCache<ResourceReport>(world), IResourceViewSource
{
    protected override string Key(ResourceReport r) => r.Key;

    protected override IEnumerable<ResourceReport> Build(IReadOnlyWorldState w)
    {
        var items = new List<ResourceReport>(w.Deposits.Count);
        for (int i = 0; i < w.Deposits.Count; i++)
        {
            DepositRow d = w.Deposits[i];
            string good = GoodName(d.Good.Value);
            items.Add(new ResourceReport(LiveWorld.Key(d.Settlement.Value) + "-g" + LiveWorld.Key(d.Good.Value),
                char.ToUpperInvariant(good[0]) + good[1..] + " deposit", good, LiveWorld.Key(d.Settlement.Value),
                [new ReportedInput("abundance", d.Abundance)], ReportedVisibility.NotModelled,
                "DepositRow: the founding-rolled extraction abundance; " + LiveWorld.VisibilityNote));
        }
        return items;
    }

    private string GoodName(int id)
    {
        foreach (GoodEntry g in goods.Goods) if (g.Id == id) return g.Name;
        return "good-" + LiveWorld.N(id);
    }
}

/// <summary>Notables (people, T4.8) attached to their settlement. A vacated slot (count 0 —
/// death, or the old row after a defection) is not a person and is not reported, so each
/// living NotableId appears once. The simulation gives people no map position and no role:
/// the location is "at settlement", the display type is the generic "person".</summary>
internal sealed class LiveNotableSource(Func<IReadOnlyWorldState> world)
    : LiveSnapshotCache<AgentReport>(world), IMobileAgentViewSource
{
    protected override string Key(AgentReport r) => r.Key;

    protected override IEnumerable<AgentReport> Build(IReadOnlyWorldState w)
    {
        var items = new List<AgentReport>();
        for (int i = 0; i < w.Notables.Count; i++)
        {
            NotableRow n = w.Notables[i];
            if (n.Count.Value <= 0) continue;
            items.Add(new AgentReport("notable-" + LiveWorld.Key(n.Id.Value), "Notable #" + LiveWorld.N(n.Id.Value), "person",
                LiveWorld.Key(n.Allegiance.Value), PolityRelation.Allegiance, null, null, LiveWorld.Key(n.Settlement.Value),
                null, null, null, [], ReportedVisibility.NotModelled,
                "NotableRow: a living notable at their settlement (no map position or role in the simulation yet); "
                + LiveWorld.VisibilityNote));
        }
        return items;
    }
}

/// <summary>The built transport network (T1.3): network nodes as junctions at their lattice
/// node's centre, network edges as transport links between them — the one graph (D-009).</summary>
internal sealed class LiveNetworkSource : IInfrastructureViewSource
{
    private readonly NodeCache _nodes;
    private readonly EdgeCache _edges;

    public LiveNetworkSource(Func<IReadOnlyWorldState> world, Func<int, WorldPoint> center)
    {
        _nodes = new NodeCache(world, center);
        _edges = new EdgeCache(world);
    }

    public Snapshot<InfraNodeReport> Nodes => _nodes.Current;
    public Snapshot<InfraEdgeReport> Edges => _edges.Current;

    private sealed class NodeCache(Func<IReadOnlyWorldState> world, Func<int, WorldPoint> center)
        : LiveSnapshotCache<InfraNodeReport>(world)
    {
        protected override string Key(InfraNodeReport r) => r.Key;

        protected override IEnumerable<InfraNodeReport> Build(IReadOnlyWorldState w)
        {
            var items = new List<InfraNodeReport>(w.NetworkNodes.Count);
            for (int i = 0; i < w.NetworkNodes.Count; i++)
            {
                NetworkNodeRow n = w.NetworkNodes[i];
                items.Add(new InfraNodeReport(LiveWorld.Key(n.Id.Value), "Network node #" + LiveWorld.N(n.Id.Value), "junction",
                    center(n.LatticeNode), null, ReportedVisibility.NotModelled, "NetworkNodeRow; " + LiveWorld.VisibilityNote));
            }
            return items;
        }
    }

    private sealed class EdgeCache(Func<IReadOnlyWorldState> world) : LiveSnapshotCache<InfraEdgeReport>(world)
    {
        protected override string Key(InfraEdgeReport r) => r.Key;

        protected override IEnumerable<InfraEdgeReport> Build(IReadOnlyWorldState w)
        {
            var items = new List<InfraEdgeReport>(w.NetworkEdges.Count);
            for (int i = 0; i < w.NetworkEdges.Count; i++)
            {
                NetworkEdgeRow e = w.NetworkEdges[i];
                items.Add(new InfraEdgeReport(LiveWorld.Key(e.Id.Value), "Transport link #" + LiveWorld.N(e.Id.Value), "transport-link",
                    LiveWorld.Key(e.A.Value), LiveWorld.Key(e.B.Value), null, PolityRelation.None,
                    [new ReportedInput("edgeType", e.EdgeType)], ReportedVisibility.NotModelled,
                    "NetworkEdgeRow: an edge of the built transport network; " + LiveWorld.VisibilityNote));
            }
            return items;
        }
    }
}
