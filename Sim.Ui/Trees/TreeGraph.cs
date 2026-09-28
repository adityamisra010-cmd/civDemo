namespace Sim.Ui.Trees;

/// <summary>One resolved edge. <see cref="From"/>/<see cref="To"/> are the AUTHORED ends
/// ("From &lt;verb&gt; To"); <see cref="FlowFrom"/>/<see cref="FlowTo"/> are the same ends in
/// left-to-right flow order (swapped for a Reverse kind such as requires).</summary>
public sealed record GraphEdge(
    int Index, int From, int To, int FlowFrom, int FlowTo, RelationKindDef Kind, string Note, string Origin)
{
    public bool Directed => Kind.Flow != RelationFlow.None;
}

/// <summary>
/// THE ONE GRAPH. Every lens is a view onto it; nothing here knows about seven trees.
/// Nodes are indexed in authoring order; adjacency lists are sorted by edge index, so
/// every traversal visits in a fixed order and every query returns a sorted result.
/// Read-only after construction.
/// </summary>
public sealed class TreeGraph
{
    private readonly Dictionary<string, int> _index;
    private readonly int[][] _flowOut;       // edge indices leaving a node along the flow
    private readonly int[][] _flowIn;        // edge indices entering a node along the flow
    private readonly int[][] _incident;      // every edge touching a node, sorted
    private readonly string[][] _lenses;     // node → lens ids (primary first)

    public TreesDocument Content { get; }
    public IReadOnlyList<NodeDef> Nodes => Content.Nodes;
    public IReadOnlyList<GraphEdge> Edges { get; }

    public TreeGraph(TreesDocument content)
    {
        Content = content;
        _index = new Dictionary<string, int>(StringComparer.Ordinal);
        for (int i = 0; i < content.Nodes.Count; i++) _index[content.Nodes[i].Id] = i;

        var edges = new GraphEdge[content.Edges.Count];
        for (int i = 0; i < edges.Length; i++)
        {
            EdgeDef e = content.Edges[i];
            RelationKindDef k = content.Kind(e.Kind);
            int a = _index[e.From], b = _index[e.To];
            (int fa, int fb) = k.Flow == RelationFlow.Reverse ? (b, a) : (a, b);
            edges[i] = new GraphEdge(i, a, b, fa, fb, k, e.Note, e.Origin);
        }
        Edges = edges;

        int n = content.Nodes.Count;
        var outs = new List<int>[n];
        var ins = new List<int>[n];
        var inc = new List<int>[n];
        for (int i = 0; i < n; i++) { outs[i] = []; ins[i] = []; inc[i] = []; }
        foreach (GraphEdge e in edges)
        {
            outs[e.FlowFrom].Add(e.Index);
            ins[e.FlowTo].Add(e.Index);
            inc[e.From].Add(e.Index);
            inc[e.To].Add(e.Index);
        }
        _flowOut = outs.Select(l => l.ToArray()).ToArray();
        _flowIn = ins.Select(l => l.ToArray()).ToArray();
        _incident = inc.Select(l => l.ToArray()).ToArray();
        _lenses = content.Nodes.Select(x => new[] { x.Domain }.Concat(x.AlsoIn.Where(l => l != x.Domain)).ToArray()).ToArray();
    }

    public int Count => Content.Nodes.Count;
    public int IndexOf(string id) => _index[id];
    public bool TryIndexOf(string id, out int index) => _index.TryGetValue(id, out index);
    public NodeDef Node(int index) => Content.Nodes[index];
    public NodeTypeDef TypeOf(int index) => Content.NodeType(Content.Nodes[index].Type);

    /// <summary>The lenses a node appears in, primary first.</summary>
    public IReadOnlyList<string> LensesOf(int index) => _lenses[index];
    public bool InLens(int index, string lensId) => Array.IndexOf(_lenses[index], lensId) >= 0;

    /// <summary>Every edge touching the node, in edge order.</summary>
    public IReadOnlyList<int> Incident(int index) => _incident[index];
    public IReadOnlyList<int> FlowOut(int index) => _flowOut[index];
    public IReadOnlyList<int> FlowIn(int index) => _flowIn[index];

    /// <summary>An edge whose ends sit in different primary lenses — the relationships
    /// that make this one graph rather than seven trees.</summary>
    public bool IsCrossLens(GraphEdge e) => Node(e.From).Domain != Node(e.To).Domain;

    /// <summary>The direct upstream neighbours along layering edges: what the content says
    /// precedes this node. Used by the "why is it available?" panel.</summary>
    public IReadOnlyList<GraphEdge> DirectUpstream(int index, bool layeringOnly = true) =>
        _flowIn[index].Select(i => Edges[i]).Where(e => e.Directed && (!layeringOnly || e.Kind.Layering)).ToArray();

    public IReadOnlyList<GraphEdge> DirectDownstream(int index, bool layeringOnly = true) =>
        _flowOut[index].Select(i => Edges[i]).Where(e => e.Directed && (!layeringOnly || e.Kind.Layering)).ToArray();

    /// <summary>The transitive upstream closure along directed edges (layering only by
    /// default), excluding the node itself, sorted by node index.</summary>
    public int[] Upstream(int index, bool layeringOnly = true) => Closure(index, forward: false, layeringOnly);

    /// <summary>The transitive downstream closure — "what this leads to".</summary>
    public int[] Downstream(int index, bool layeringOnly = true) => Closure(index, forward: true, layeringOnly);

    private int[] Closure(int start, bool forward, bool layeringOnly)
    {
        var seen = new bool[Count];
        var queue = new Queue<int>();
        queue.Enqueue(start);
        seen[start] = true;
        while (queue.Count > 0)
        {
            int at = queue.Dequeue();
            foreach (int ei in forward ? _flowOut[at] : _flowIn[at])
            {
                GraphEdge e = Edges[ei];
                if (!e.Directed || (layeringOnly && !e.Kind.Layering)) continue;
                int next = forward ? e.FlowTo : e.FlowFrom;
                if (seen[next]) continue;
                seen[next] = true;
                queue.Enqueue(next);
            }
        }
        var result = new List<int>();
        for (int i = 0; i < Count; i++) if (seen[i] && i != start) result.Add(i);
        return result.ToArray();
    }

    /// <summary>Edges among a node set along directed flow (both ends inside).</summary>
    public int[] EdgesWithin(IReadOnlyCollection<int> nodes, bool layeringOnly = true)
    {
        var set = new bool[Count];
        foreach (int n in nodes) set[n] = true;
        return Edges.Where(e => e.Directed && (!layeringOnly || e.Kind.Layering) && set[e.FlowFrom] && set[e.FlowTo])
            .Select(e => e.Index).ToArray();
    }

    /// <summary>
    /// DEPENDENCY TRACE: the shortest directed path from <paramref name="from"/> to
    /// <paramref name="to"/> along any directed edge (BFS; the first-found parent wins and
    /// adjacency is in edge order, so the path is deterministic). Tries both directions and
    /// returns the node sequence in flow order, or an empty array when unconnected.
    /// </summary>
    public int[] Path(int from, int to)
    {
        int[] forward = Bfs(from, to);
        if (forward.Length > 0) return forward;
        return Bfs(to, from);
    }

    private int[] Bfs(int from, int to)
    {
        if (from == to) return [from];
        var parent = Enumerable.Repeat(-1, Count).ToArray();
        var queue = new Queue<int>();
        queue.Enqueue(from);
        parent[from] = from;
        while (queue.Count > 0)
        {
            int at = queue.Dequeue();
            foreach (int ei in _flowOut[at])
            {
                GraphEdge e = Edges[ei];
                if (!e.Directed) continue;
                int next = e.FlowTo;
                if (parent[next] >= 0) continue;
                parent[next] = at;
                if (next == to)
                {
                    var path = new List<int> { to };
                    for (int p = at; p != from; p = parent[p]) path.Add(p);
                    path.Add(from);
                    path.Reverse();
                    return path.ToArray();
                }
                queue.Enqueue(next);
            }
        }
        return [];
    }
}
