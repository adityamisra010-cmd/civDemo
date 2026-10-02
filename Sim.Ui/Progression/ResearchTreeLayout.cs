using Sim.Core.Systems.Research;

namespace Sim.Ui.Progression;

/// <summary>World-space geometry of the layout (1 unit = 1 screen pixel at zoom 1).
/// Presentation tuning, not content.</summary>
public sealed record TreeLayoutOptions(
    double ColumnWidth = 236.0, double RowHeight = 84.0, double CardWidth = 196.0, double CardHeight = 68.0,
    double AnchorHeight = 30.0, double LaneHeader = 30.0, double LaneGap = 26.0, double Margin = 40.0,
    double AgeHeader = 34.0, double LeftGutter = 150.0, int Sweeps = 4);

/// <summary>A placed vertex. <see cref="Lane"/> indexes <see cref="TreeLayout.Lanes"/>.</summary>
public sealed record PlacedVertex(int Vertex, int Column, int Lane, int Slot, double X, double Y, double W, double H)
{
    public double CenterX => X + W / 2.0;
    public double CenterY => Y + H / 2.0;
    public double Right => X + W;
    public double Bottom => Y + H;
    public bool Contains(double x, double y) => x >= X && x <= X + W && y >= Y && y <= Y + H;
}

/// <summary>One horizontal lane: the Main trunk, a subtree, a Civics domain, or the
/// external-anchor lane. <see cref="Branch"/> is the content subtree index (-1 = trunk/none).</summary>
public sealed record LaneBox(int Index, string Id, string Name, int Branch, bool External, double Y, double Height, int Rows);

/// <summary>A run of consecutive columns whose dominant Age label is the same.</summary>
public sealed record AgeSpan(string Age, string Label, int FirstColumn, int LastColumn, double X0, double X1);

public sealed record TreeLayout(
    ResearchGraph Graph, IReadOnlyList<PlacedVertex> Placed, IReadOnlyList<LaneBox> Lanes,
    IReadOnlyList<AgeSpan> Ages, int Columns, double Width, double Height, TreeLayoutOptions Options)
{
    /// <summary>The vertex under a world point, or -1. Boxes never overlap.</summary>
    public int HitTest(double x, double y)
    {
        for (int i = 0; i < Placed.Count; i++) if (Placed[i].Contains(x, y)) return i;
        return -1;
    }
}

/// <summary>
/// THE LAYOUT — a deterministic layered drawing of one research tree, generated from content.
///  · COLUMNS: a node's content Depth, raised if needed so every in-tree prerequisite sits in an
///    earlier column (longest-path layering); unused columns are then compressed out. Ages are column metadata only (law 4: no gate).
///  · LANES: Technology = Main trunk + the content's subtrees in content order; Civics = one
///    lane per domain (ordinal order). Cross-tree prerequisites get their own top lane.
///  · ORDER inside a (lane, column) cell: barycentre sweeps over predecessors with the composite
///    key (barycentre, vertex index) — ties break on an integer, never on sort stability.
/// Ported in spirit from the Trees UI foundation's TreeLayout (claude/civdemo-work-b1z2y4).
/// </summary>
public static class ResearchTreeLayout
{
    public static TreeLayout Compute(ResearchGraph g, TreeLayoutOptions? options = null)
    {
        TreeLayoutOptions o = options ?? new TreeLayoutOptions();
        int n = g.Vertices.Count;
        ResearchContent content = g.Content;

        // ---- lanes
        var lanes = new List<(string Id, string Name, int Branch, bool External)>();
        bool anyExternal = n > g.OwnCount;
        if (anyExternal)
            lanes.Add(("external", g.Tree == ResearchTree.Civics ? "From the Technology tree" : "From the Civics tree", -1, true));
        var civicDomains = new List<string>();
        if (g.Tree == ResearchTree.Technology)
        {
            lanes.Add(("main", "Main Trunk", -1, false));
            for (int b = 0; b < content.Branches.Count; b++) lanes.Add((content.Branches[b].Id, content.Branches[b].Name, b, false));
        }
        else
        {
            for (int v = 0; v < g.OwnCount; v++)
                if (!civicDomains.Contains(g.Node(v).Domain)) civicDomains.Add(g.Node(v).Domain);
            civicDomains.Sort(StringComparer.Ordinal);
            foreach (string d in civicDomains) lanes.Add((d, Title(d), -1, false));
        }
        int laneBase = anyExternal ? 1 : 0;
        var laneOf = new int[n];
        for (int v = 0; v < n; v++)
        {
            if (g.Vertices[v].External) { laneOf[v] = 0; continue; }
            ResearchNode node = g.Node(v);
            laneOf[v] = g.Tree == ResearchTree.Technology
                ? laneBase + (node.Branch < 0 ? 0 : 1 + node.Branch)
                : laneBase + civicDomains.IndexOf(node.Domain);
        }

        // ---- columns: depth, relaxed so in-tree prerequisites precede (bounded passes).
        var column = new int[n];
        for (int v = 0; v < g.OwnCount; v++) column[v] = Math.Max(0, g.Node(v).Depth);
        for (int pass = 0; pass < n + 1; pass++)
        {
            bool changed = false;
            foreach (GraphEdge e in g.Edges)
            {
                if (g.Vertices[e.From].External) continue;
                if (column[e.To] < column[e.From] + 1) { column[e.To] = column[e.From] + 1; changed = true; }
            }
            if (!changed) break;
        }
        // External anchors: one column before their earliest dependent.
        for (int v = g.OwnCount; v < n; v++)
        {
            int min = int.MaxValue;
            foreach (GraphEdge e in g.Edges) if (e.From == v) min = Math.Min(min, column[e.To]);
            column[v] = min == int.MaxValue ? 0 : Math.Max(0, min - 1);
        }
        // Compress columns no vertex uses (the Civics tree's depths are sparse); order is kept.
        int maxCol = 0;
        for (int v = 0; v < n; v++) maxCol = Math.Max(maxCol, column[v]);
        var used = new bool[maxCol + 1];
        for (int v = 0; v < n; v++) used[column[v]] = true;
        var dense = new int[maxCol + 1];
        int columns = 0;
        for (int c = 0; c <= maxCol; c++) { dense[c] = columns; if (used[c]) columns++; }
        for (int v = 0; v < n; v++) column[v] = dense[column[v]];
        columns = Math.Max(1, columns);

        // ---- cells and barycentre ordering
        var cells = new List<int>[lanes.Count, columns];
        for (int l = 0; l < lanes.Count; l++) for (int c = 0; c < columns; c++) cells[l, c] = [];
        for (int v = 0; v < n; v++) cells[laneOf[v], column[v]].Add(v);

        var preds = new List<int>[n];
        var succs = new List<int>[n];
        for (int v = 0; v < n; v++) { preds[v] = []; succs[v] = []; }
        foreach (GraphEdge e in g.Edges) { preds[e.To].Add(e.From); succs[e.From].Add(e.To); }

        var slot = new int[n];
        var laneRank = new double[n];   // global vertical rank: lane offset + slot
        void Recompute()
        {
            for (int l = 0; l < lanes.Count; l++)
                for (int c = 0; c < columns; c++)
                    for (int s = 0; s < cells[l, c].Count; s++) { slot[cells[l, c][s]] = s; laneRank[cells[l, c][s]] = l * 1000.0 + s; }
        }
        Recompute();
        for (int sweep = 0; sweep < o.Sweeps; sweep++)
        {
            bool forward = sweep % 2 == 0;
            for (int ci = 0; ci < columns; ci++)
            {
                int c = forward ? ci : columns - 1 - ci;
                for (int l = 0; l < lanes.Count; l++)
                {
                    List<int> cell = cells[l, c];
                    if (cell.Count < 2) continue;
                    var keyed = new (double Bary, int V)[cell.Count];
                    for (int s = 0; s < cell.Count; s++)
                    {
                        int v = cell[s];
                        List<int> nb = forward ? preds[v] : succs[v];
                        double bary = laneRank[v];
                        if (nb.Count > 0)
                        {
                            double sum = 0;
                            foreach (int u in nb) sum += laneRank[u];
                            bary = sum / nb.Count;
                        }
                        keyed[s] = (bary, v);
                    }
                    SortByBarycentre(keyed);
                    for (int s = 0; s < keyed.Length; s++) cell[s] = keyed[s].V;
                }
                Recompute();
            }
        }

        // ---- geometry
        var laneBoxes = new LaneBox[lanes.Count];
        double y = o.Margin + o.AgeHeader;
        for (int l = 0; l < lanes.Count; l++)
        {
            int rows = 1;
            for (int c = 0; c < columns; c++) rows = Math.Max(rows, cells[l, c].Count);
            double rowH = lanes[l].External ? o.AnchorHeight + 14.0 : o.RowHeight;
            double h = o.LaneHeader + rows * rowH;
            laneBoxes[l] = new LaneBox(l, lanes[l].Id, lanes[l].Name, lanes[l].Branch, lanes[l].External, y, h, rows);
            y += h + o.LaneGap;
        }
        double height = y - o.LaneGap + o.Margin;
        double width = o.Margin * 2 + o.LeftGutter + columns * o.ColumnWidth;

        var placed = new PlacedVertex[n];
        for (int v = 0; v < n; v++)
        {
            LaneBox lane = laneBoxes[laneOf[v]];
            bool ext = g.Vertices[v].External;
            double rowH = ext ? o.AnchorHeight + 14.0 : o.RowHeight;
            double h = ext ? o.AnchorHeight : o.CardHeight;
            double x = o.Margin + o.LeftGutter + column[v] * o.ColumnWidth + (o.ColumnWidth - o.CardWidth) / 2.0;
            double vy = lane.Y + o.LaneHeader + slot[v] * rowH + (rowH - h) / 2.0;
            placed[v] = new PlacedVertex(v, column[v], laneOf[v], slot[v], x, vy, o.CardWidth, h);
        }

        return new TreeLayout(g, placed, laneBoxes, AgeSpans(g, column, columns, o), columns, width, height, o);
    }

    /// <summary>The composite key (barycentre, vertex index): ties break on the integer, so the
    /// order never depends on sort stability or the input order.</summary>
    public static void SortByBarycentre((double Bary, int V)[] keyed) =>
        Array.Sort(keyed, static (a, b) => a.Bary != b.Bary ? a.Bary.CompareTo(b.Bary) : a.V.CompareTo(b.V));

    /// <summary>The dominant Age label of each column (most frequent; tie → ordinal-smallest),
    /// merged into runs. Columns with no own node inherit the previous column's Age.</summary>
    private static AgeSpan[] AgeSpans(ResearchGraph g, int[] column, int columns, TreeLayoutOptions o)
    {
        var dominant = new string?[columns];
        for (int c = 0; c < columns; c++)
        {
            var ages = new List<string>();
            var counts = new List<int>();
            for (int v = 0; v < g.OwnCount; v++)
            {
                if (column[v] != c) continue;
                string a = g.Node(v).Age;
                int k = ages.IndexOf(a);
                if (k < 0) { ages.Add(a); counts.Add(1); } else counts[k]++;
            }
            string? best = null; int bestCount = 0;
            for (int k = 0; k < ages.Count; k++)
                if (counts[k] > bestCount || (counts[k] == bestCount && string.CompareOrdinal(ages[k], best) < 0)) { best = ages[k]; bestCount = counts[k]; }
            dominant[c] = best ?? (c > 0 ? dominant[c - 1] : null);
        }
        var spans = new List<AgeSpan>();
        int start = 0;
        for (int c = 1; c <= columns; c++)
        {
            if (c < columns && dominant[c] == dominant[start]) continue;
            string age = dominant[start] ?? "";
            spans.Add(new AgeSpan(age, AgeLabel(age), start, c - 1,
                o.Margin + o.LeftGutter + start * o.ColumnWidth, o.Margin + o.LeftGutter + c * o.ColumnWidth));
            start = c;
        }
        return [.. spans];
    }

    private static readonly string[] Roman = ["I", "II", "III", "IV", "V", "VI", "VII", "VIII", "IX", "X", "XI", "XII"];

    /// <summary>"A3" → "Age III"; any other label is shown as written.</summary>
    public static string AgeLabel(string age)
    {
        if (age.Length >= 2 && age[0] == 'A' && int.TryParse(age.AsSpan(1), System.Globalization.NumberStyles.None,
                System.Globalization.CultureInfo.InvariantCulture, out int k) && k >= 1 && k <= Roman.Length)
            return "Age " + Roman[k - 1];
        return age;
    }

    public static string Title(string id)
    {
        var parts = id.Split('_', StringSplitOptions.RemoveEmptyEntries);
        for (int i = 0; i < parts.Length; i++) parts[i] = char.ToUpperInvariant(parts[i][0]) + parts[i][1..];
        return string.Join(' ', parts);
    }
}
