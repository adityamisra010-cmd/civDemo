namespace Sim.Ui.Trees;

/// <summary>World-space geometry of the layout, in "graph units" (1 unit = 1 screen pixel
/// at zoom 1). Tuning these is presentation, not content.</summary>
public sealed record LayoutOptions(
    double ColumnWidth = 214.0, double RowHeight = 74.0, double NodeWidth = 172.0, double NodeHeight = 54.0,
    double BandHeaderHeight = 24.0, double BandPadding = 14.0, double Margin = 36.0, double LensGutter = 132.0,
    int Sweeps = 4);

/// <summary>A node's placed box. <see cref="Column"/> is its layer; <see cref="Band"/> the
/// index of its primary lens in lens order; <see cref="Slot"/> its row inside the band.</summary>
public sealed record NodeBox(int Node, int Column, int Band, int Slot, double X, double Y, double W, double H)
{
    public double CenterX => X + W / 2.0;
    public double CenterY => Y + H / 2.0;
    public double Right => X + W;
    public double Bottom => Y + H;
    public bool Contains(double x, double y) => x >= X && x <= X + W && y >= Y && y <= Y + H;
}

/// <summary>One horizontal band: the lens a row of the graph belongs to.</summary>
public sealed record BandBox(string LensId, int Band, double Y, double Height, int Rows);

public sealed record TreeLayoutResult(
    IReadOnlyList<NodeBox> Boxes, IReadOnlyList<BandBox> Bands, int Columns, double Width, double Height, LayoutOptions Options)
{
    public NodeBox Box(int node) => Boxes[node];

    /// <summary>The node under a world point, or -1. Scans in node order; boxes never overlap.</summary>
    public int HitTest(double x, double y)
    {
        for (int i = 0; i < Boxes.Count; i++) if (Boxes[i].Contains(x, y)) return i;
        return -1;
    }
}

/// <summary>
/// THE LAYOUT — a deterministic layered drawing of the one graph.
///
///  · COLUMNS come from the layering edges (longest path along the flow), so "what leads
///    to what" reads left to right. A node's <c>column</c> content hint is a minimum.
///    Non-layering edges (improves, supports, diffusesTo, relatedTo) never move a node:
///    feedback may point backward.
///  · BANDS are the lenses in lens order: the seven lenses are horizontal bands of ONE
///    drawing, and cross-lens edges cross the bands. A node is drawn once, in its primary
///    lens's band.
///  · Inside a (band, column) cell, nodes are ordered by barycentre sweeps over their
///    neighbours, with the composite key (barycentre, current slot, node index) — so ties
///    break on an integer and the result never depends on sort stability or hashing.
/// </summary>
public static class TreeLayout
{
    public static TreeLayoutResult Compute(TreeGraph g, LayoutOptions? options = null)
    {
        LayoutOptions o = options ?? new LayoutOptions();
        int n = g.Count;
        int[] column = Columns(g);

        IReadOnlyList<LensDef> lenses = g.Content.LensesInOrder();
        var bandOf = new int[n];
        for (int i = 0; i < n; i++)
        {
            bandOf[i] = 0;
            for (int b = 0; b < lenses.Count; b++) if (lenses[b].Id == g.Node(i).Domain) { bandOf[i] = b; break; }
        }
        int columns = n == 0 ? 1 : column.Max() + 1;

        // cells[band][col] = ordered node list.
        var cells = new List<int>[lenses.Count, columns];
        for (int b = 0; b < lenses.Count; b++) for (int c = 0; c < columns; c++) cells[b, c] = [];
        for (int i = 0; i < n; i++) cells[bandOf[i], column[i]].Add(i);

        // Neighbours over every directed edge (layering or not) — ordering only.
        var neighbours = new List<int>[n];
        for (int i = 0; i < n; i++) neighbours[i] = [];
        foreach (GraphEdge e in g.Edges)
        {
            neighbours[e.From].Add(e.To);
            neighbours[e.To].Add(e.From);
        }

        int[] bandRows = new int[lenses.Count];
        for (int b = 0; b < lenses.Count; b++)
        {
            int rows = 1;
            for (int c = 0; c < columns; c++) rows = System.Math.Max(rows, cells[b, c].Count);
            bandRows[b] = rows;
        }

        double[] globalRow = new double[n];
        void Reindex()
        {
            double offset = 0;
            for (int b = 0; b < lenses.Count; b++)
            {
                for (int c = 0; c < columns; c++)
                    for (int s = 0; s < cells[b, c].Count; s++) globalRow[cells[b, c][s]] = offset + s;
                offset += bandRows[b] + 1;
            }
        }
        Reindex();

        for (int sweep = 0; sweep < o.Sweeps; sweep++)
        {
            bool forward = sweep % 2 == 0;
            for (int step = 0; step < columns; step++)
            {
                int c = forward ? step : columns - 1 - step;
                for (int b = 0; b < lenses.Count; b++)
                {
                    List<int> cell = cells[b, c];
                    if (cell.Count < 2) continue;
                    var keyed = new (double Score, int Slot, int Node)[cell.Count];
                    for (int s = 0; s < cell.Count; s++)
                    {
                        int node = cell[s];
                        double sum = 0; int count = 0;
                        foreach (int m in neighbours[node])
                        {
                            bool side = forward ? column[m] < c : column[m] > c;
                            if (!side) continue;
                            sum += globalRow[m]; count++;
                        }
                        // No neighbour on this side: keep the node where it is, in the
                        // band's own row coordinates.
                        double score = count > 0 ? sum / count : globalRow[node];
                        keyed[s] = (score, s, node);
                    }
                    Array.Sort(keyed, (x, y) =>
                    {
                        int k = x.Score.CompareTo(y.Score);
                        if (k != 0) return k;
                        k = x.Slot.CompareTo(y.Slot);
                        return k != 0 ? k : x.Node.CompareTo(y.Node);
                    });
                    for (int s = 0; s < keyed.Length; s++) cell[s] = keyed[s].Node;
                }
                Reindex();
            }
        }

        // Geometry.
        var boxes = new NodeBox[n];
        var bands = new BandBox[lenses.Count];
        double y = o.Margin;
        double inset = (o.RowHeight - o.NodeHeight) / 2.0;
        for (int b = 0; b < lenses.Count; b++)
        {
            double height = o.BandHeaderHeight + bandRows[b] * o.RowHeight + o.BandPadding;
            bands[b] = new BandBox(lenses[b].Id, b, y, height, bandRows[b]);
            for (int c = 0; c < columns; c++)
            {
                List<int> cell = cells[b, c];
                for (int s = 0; s < cell.Count; s++)
                {
                    double x = o.Margin + o.LensGutter + c * o.ColumnWidth;
                    double ny = y + o.BandHeaderHeight + s * o.RowHeight + inset;
                    boxes[cell[s]] = new NodeBox(cell[s], c, b, s, x, ny, o.NodeWidth, o.NodeHeight);
                }
            }
            y += height;
        }
        double width = o.Margin * 2 + o.LensGutter + (columns - 1) * o.ColumnWidth + o.NodeWidth;
        return new TreeLayoutResult(boxes, bands, columns, width, y + o.Margin, o);
    }

    /// <summary>Longest-path layering over the LAYERING edges in flow order (Kahn's
    /// algorithm, smallest node index first), with the content's column hint as a floor.
    /// The loader has already rejected layering cycles; a defensive pass still terminates.</summary>
    internal static int[] Columns(TreeGraph g)
    {
        int n = g.Count;
        var indegree = new int[n];
        foreach (GraphEdge e in g.Edges) if (e.Directed && e.Kind.Layering) indegree[e.FlowTo]++;
        var ready = new SortedSet<int>();
        for (int i = 0; i < n; i++) if (indegree[i] == 0) ready.Add(i);
        var column = new int[n];
        for (int i = 0; i < n; i++) column[i] = g.Node(i).ColumnHint ?? 0;
        var done = new bool[n];
        while (ready.Count > 0)
        {
            int at = ready.Min;
            ready.Remove(at);
            done[at] = true;
            foreach (int ei in g.FlowOut(at))
            {
                GraphEdge e = g.Edges[ei];
                if (!e.Directed || !e.Kind.Layering) continue;
                column[e.FlowTo] = System.Math.Max(column[e.FlowTo], column[at] + 1);
                if (--indegree[e.FlowTo] == 0) ready.Add(e.FlowTo);
            }
        }
        return column;
    }
}
