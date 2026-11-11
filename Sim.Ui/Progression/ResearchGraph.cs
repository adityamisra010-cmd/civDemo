using Sim.Core.Systems.Research;

namespace Sim.Ui.Progression;

/// <summary>How a prerequisite edge participates in its target's expression (decided by <see cref="PrereqExpr.Kinds"/>).
/// <see cref="And"/> ("requires", solid): the prerequisite holds on EVERY path that satisfies the expression
/// (Predicate.MustHoldAtoms). <see cref="Or"/> ("one of", dashed): with the required ones it completes the expression on its
/// own — one alternative among several. <see cref="Grouped"/> (dotted; D-084, audit §4): it counts only together with other
/// prerequisites — a part of a grouped alternative, as plough_ard in seed_drill OR (plough_ard AND horse_collar) — so it is
/// neither required nor an alternative by itself; the grouping is the node's <see cref="ResearchGraph.Requirement"/>.</summary>
public enum EdgeKind { And = 1, Or = 2, Grouped = 3 }

/// <summary>One prerequisite edge, From (prerequisite) → To (dependent). Both are
/// GRAPH-LOCAL vertex indices (see <see cref="ResearchGraph.Vertices"/>).</summary>
public readonly record struct GraphEdge(int From, int To, EdgeKind Kind);

/// <summary>A drawn vertex: a node of this tree, or an EXTERNAL anchor — a node of the other
/// tree that a node of this tree names as a prerequisite (Civics names Technology nodes).
/// Anchors are drawn as small tokens so the edge is honest; they are not part of the tree.</summary>
public readonly record struct GraphVertex(int ContentIndex, bool External);

/// <summary>
/// ONE research tree as a drawable graph, generated from the loaded <see cref="ResearchContent"/>
/// (never from UI-owned JSON). Vertices are the tree's nodes in content order (key-ascending),
/// followed by external anchors in content order. Edges are exactly the union of every node's
/// <see cref="ResearchNode.PrerequisiteNodes"/>, each classified from the expression itself (<see cref="PrereqExpr.Kinds"/>),
/// and every node of the tree keeps its whole prerequisite expression (<see cref="Requirement"/>, D-084): the edges are the
/// atoms, the expression is how they combine.
/// Pure and deterministic: no dictionaries are iterated, no hashing.
/// </summary>
public sealed class ResearchGraph
{
    public ResearchContent Content { get; }
    public ResearchTree Tree { get; }
    public IReadOnlyList<GraphVertex> Vertices { get; }
    public IReadOnlyList<GraphEdge> Edges { get; }
    /// <summary>Number of vertices that belong to this tree (they come first).</summary>
    public int OwnCount { get; }
    private readonly int[] _vertexOf;   // content index → vertex, or -1
    private readonly PrereqExpr?[] _requirement;   // own vertex → its prerequisite expression (atoms are content indices)

    private ResearchGraph(ResearchContent content, ResearchTree tree, GraphVertex[] vertices, GraphEdge[] edges, int own, int[] vertexOf,
        PrereqExpr?[] requirement)
    {
        Content = content; Tree = tree; Vertices = vertices; Edges = edges; OwnCount = own; _vertexOf = vertexOf; _requirement = requirement;
    }

    public int VertexOf(int contentIndex) => contentIndex >= 0 && contentIndex < _vertexOf.Length ? _vertexOf[contentIndex] : -1;
    public ResearchNode Node(int vertex) => Content.Nodes[Vertices[vertex].ContentIndex];

    /// <summary>D-084: the whole prerequisite expression of an own vertex (null for a root or an external anchor) — the grouping
    /// its incoming edges stand for. Its atoms are content indices.</summary>
    public PrereqExpr? Requirement(int vertex) => vertex >= 0 && vertex < _requirement.Length ? _requirement[vertex] : null;

    public static ResearchGraph Build(ResearchContent content, ResearchTree tree)
    {
        int n = content.Nodes.Count;
        var vertexOf = new int[n];
        Array.Fill(vertexOf, -1);
        var vertices = new List<GraphVertex>();
        for (int i = 0; i < n; i++)
            if (content.Nodes[i].Tree == tree) { vertexOf[i] = vertices.Count; vertices.Add(new GraphVertex(i, false)); }
        int own = vertices.Count;

        // External anchors: prerequisites from the other tree, content order.
        var external = new bool[n];
        for (int v = 0; v < own; v++)
            foreach (int p in content.Nodes[vertices[v].ContentIndex].PrerequisiteNodes)
                if (vertexOf[p] < 0) external[p] = true;
        for (int i = 0; i < n; i++)
            if (external[i]) { vertexOf[i] = vertices.Count; vertices.Add(new GraphVertex(i, true)); }

        var edges = new List<GraphEdge>();
        var requirement = new PrereqExpr?[own];
        for (int v = 0; v < own; v++)
        {
            ResearchNode node = content.Nodes[vertices[v].ContentIndex];
            requirement[v] = PrereqExpr.Of(node);
            EdgeKind[] kinds = PrereqExpr.Kinds(node);
            for (int k = 0; k < kinds.Length; k++)
                edges.Add(new GraphEdge(vertexOf[node.PrerequisiteNodes[k]], v, kinds[k]));
        }
        return new ResearchGraph(content, tree, [.. vertices], [.. edges], own, vertexOf, requirement);
    }
}
