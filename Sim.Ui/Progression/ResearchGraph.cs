using Sim.Core.Systems.Research;

namespace Sim.Ui.Progression;

/// <summary>How a prerequisite edge participates in its target's expression.
/// <see cref="And"/>: the prerequisite holds on EVERY path that satisfies the expression
/// (Predicate.MustHoldAtoms). <see cref="Or"/>: it is one alternative among several.</summary>
public enum EdgeKind { And = 1, Or = 2 }

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
/// <see cref="ResearchNode.PrerequisiteNodes"/>, classified AND/OR from the expression itself.
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

    private ResearchGraph(ResearchContent content, ResearchTree tree, GraphVertex[] vertices, GraphEdge[] edges, int own, int[] vertexOf)
    {
        Content = content; Tree = tree; Vertices = vertices; Edges = edges; OwnCount = own; _vertexOf = vertexOf;
    }

    public int VertexOf(int contentIndex) => contentIndex >= 0 && contentIndex < _vertexOf.Length ? _vertexOf[contentIndex] : -1;
    public ResearchNode Node(int vertex) => Content.Nodes[Vertices[vertex].ContentIndex];

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
        for (int v = 0; v < own; v++)
        {
            ResearchNode node = content.Nodes[vertices[v].ContentIndex];
            IReadOnlyList<int> must = node.Prerequisite?.MustHoldAtoms() ?? [];
            foreach (int p in node.PrerequisiteNodes)
            {
                bool and = false;
                for (int k = 0; k < must.Count; k++) if (must[k] == p) { and = true; break; }
                edges.Add(new GraphEdge(vertexOf[p], v, and ? EdgeKind.And : EdgeKind.Or));
            }
        }
        return new ResearchGraph(content, tree, [.. vertices], [.. edges], own, vertexOf);
    }
}
