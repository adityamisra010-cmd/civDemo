using Sim.Core.Systems.Research;

namespace Sim.Ui.Progression;

/// <summary>World-space geometry of the layout (1 unit = 1 screen pixel at zoom 1).
/// <see cref="ViewportWidth"/> is the canvas width the drawing must fit at zoom 1: the layout
/// never exceeds it, so the tree scrolls on ONE axis only (vertical). Presentation tuning, not content.
/// UR-4 (M5 polish, UI readability): the card is ~208–232 × 116 px at UI scale 1 (it was 150–214 × 84) so its name
/// is set at the BODY role on up to two lines, never cut; <see cref="MinCardWidth"/> is a floor the layout honours
/// at every width — a tier whose lanes do not fit side by side WRAPS its lanes into further rows
/// (<see cref="LaneWrapGap"/> apart) instead of shrinking the cards. <see cref="Scaled"/> grows every length with the
/// UI scale.</summary>
public sealed record TreeLayoutOptions(
    double ViewportWidth = 1196.0, double MinCardWidth = 200.0, double MaxCardWidth = 232.0,
    double CardHeight = 116.0, double AnchorHeight = 36.0, double RowGap = 16.0, double SlotGap = 10.0,
    double Gutter = 18.0, double TierGap = 74.0, double CollapsedWidth = 34.0, double Margin = 4.0,
    double Spine = 22.0, int Sweeps = 4, double LaneWrapGap = 42.0)
{
    /// <summary>The reference options with every length × <paramref name="scale"/> (the UI scale) and the given
    /// viewport width (already in screen px).</summary>
    public static TreeLayoutOptions Scaled(double scale, double viewportWidth)
    {
        var o = new TreeLayoutOptions();
        double s = Math.Max(0.5, scale);
        return o with
        {
            ViewportWidth = viewportWidth, MinCardWidth = o.MinCardWidth * s, MaxCardWidth = o.MaxCardWidth * s,
            CardHeight = o.CardHeight * s, AnchorHeight = o.AnchorHeight * s, RowGap = o.RowGap * s, SlotGap = o.SlotGap * s,
            Gutter = o.Gutter * s, TierGap = o.TierGap * s, CollapsedWidth = o.CollapsedWidth * s, Margin = o.Margin * s,
            Spine = o.Spine * s, LaneWrapGap = o.LaneWrapGap * s,
        };
    }
}

/// <summary>A placed vertex. <see cref="Column"/> is the vertex's TIER (prerequisite depth),
/// which runs top → bottom; <see cref="Row"/> is the wrapped row inside the tier band and
/// <see cref="Slot"/> the order inside its (lane, tier) cell. <see cref="Lane"/> indexes <see cref="TreeLayout.Lanes"/>.</summary>
public sealed record PlacedVertex(int Vertex, int Column, int Lane, int Slot, double X, double Y, double W, double H, bool Hidden = false, int Row = 0)
{
    public int Tier => Column;
    public double CenterX => X + W / 2.0;
    public double CenterY => Y + H / 2.0;
    public double Right => X + W;
    public double Bottom => Y + H;
    public bool Contains(double x, double y) => !Hidden && x >= X && x <= X + W && y >= Y && y <= Y + H;
}

/// <summary>One lane: the Main trunk, a subtree, a Civics domain, or the external-anchor lane.
/// Lanes keep a fixed left-to-right ORDER; where a lane sits inside each tier is its
/// <see cref="LaneSegment"/>. <see cref="Branch"/> is the content subtree index (-1 = trunk/none).</summary>
public sealed record LaneBox(int Index, string Id, string Name, int Branch, bool External, bool Collapsed = false, int NodeCount = 0);

/// <summary>The part of one tier band that one lane occupies: <see cref="Slots"/> cards across,
/// then a routing gutter. Zero width (and <see cref="Collapsed"/>) when the lane is collapsed. <see cref="Y0"/> is the
/// top of the segment's first card row and <see cref="Y1"/> the bottom of its lane row (UR-4: a tier whose lanes
/// wrap holds several lane rows; <see cref="RowBase"/> is the tier-wide index of this lane row's first card row).</summary>
public sealed record LaneSegment(int Lane, int Tier, double X, double Width, int Slots, double Y0, bool Collapsed = false,
    double Y1 = 0, int RowBase = 0)
{
    public double Right => X + Width;
}

/// <summary>One horizontal tier band: a label strip of <see cref="TreeLayoutOptions.TierGap"/>
/// at <see cref="Y0"/>, then <see cref="Rows"/> wrapped card rows, ending at <see cref="Y1"/>.</summary>
public sealed record TierBand(int Tier, double Y0, double Y1, int Rows, int Lo, int Hi);

public sealed record TreeLayout(
    ResearchGraph Graph, IReadOnlyList<PlacedVertex> Placed, IReadOnlyList<LaneBox> Lanes,
    IReadOnlyList<LaneSegment> Segments, IReadOnlyList<TierBand> Tiers, int Columns, double Width, double Height, TreeLayoutOptions Options)
{
    /// <summary>The vertex under a world point, or -1. Boxes never overlap.</summary>
    public int HitTest(double x, double y)
    {
        for (int i = 0; i < Placed.Count; i++) if (Placed[i].Contains(x, y)) return i;
        return -1;
    }

    /// <summary>The tier band containing world y (the last band whose top is above y), or 0.</summary>
    public int TierAt(double y)
    {
        int t = 0;
        for (int i = 0; i < Tiers.Count; i++) if (Tiers[i].Y0 <= y) t = i;
        return t;
    }
}

/// <summary>
/// THE LAYOUT — a deterministic, SINGLE-SCROLL-AXIS drawing of one research tree, generated from content.
///  · TIERS run top → bottom: a node's content Depth, raised if needed so every in-tree prerequisite
///    sits in an earlier tier (longest-path layering); unused tiers are compressed out. Ages are
///    tier metadata only (law 4: no gate).
///  · LANES keep a fixed left-to-right order: Technology = Main trunk + the content's subtrees in
///    content order; Civics = one lane per domain (ordinal order); cross-tree prerequisites get
///    the leftmost lane. Each tier band shares the width among the lanes that HAVE nodes in that
///    tier (a <see cref="LaneSegment"/> each; most lanes are empty in most tiers), and a
///    (lane, tier) cell wraps into rows of its segment's slots. The width ALWAYS fits
///    <see cref="TreeLayoutOptions.ViewportWidth"/>: the tree scrolls vertically only.
///  · SLOTS per tier go greedily to the lane whose rows they cut most — minimise (max rows,
///    Σ rows), ties to the lowest lane index. Integers only, deterministic.
///  · ORDER inside a cell: barycentre sweeps with the composite key (barycentre, vertex index).
///  · EDGES are not drawn as long curves: <see cref="Route"/> gives an orthogonal path through the
///    row gaps and two reserved edge SPINES, which never passes under a card; the screen draws
///    them only for the hovered / selected node.
/// </summary>
public static class ResearchTreeLayout
{
    /// <param name="collapsed">Optional per-lane collapse flags (UI state): a collapsed lane shrinks
    /// to a narrow strip and its cards are placed Hidden; its width goes to the other lanes.</param>
    public static TreeLayout Compute(ResearchGraph g, TreeLayoutOptions? options = null, IReadOnlyList<bool>? collapsed = null)
    {
        TreeLayoutOptions o = options ?? new TreeLayoutOptions();
        int n = g.Vertices.Count;
        ResearchContent content = g.Content;

        // ---- lanes
        var lanes = new List<(string Id, string Name, int Branch, bool External)>();
        bool anyExternal = n > g.OwnCount;
        if (anyExternal)
            lanes.Add(("external", g.Tree == ResearchTree.Civics ? "From Technology" : "From Civics", -1, true));
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

        // ---- tiers: depth, relaxed so in-tree prerequisites precede (bounded passes).
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
        // External anchors: one tier before their earliest dependent (at least the first tier).
        for (int v = g.OwnCount; v < n; v++)
        {
            int min = int.MaxValue;
            foreach (GraphEdge e in g.Edges) if (e.From == v) min = Math.Min(min, column[e.To]);
            column[v] = min == int.MaxValue ? 0 : Math.Max(0, min - 1);
        }
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
        int L = lanes.Count;
        var cells = new List<int>[L, columns];
        for (int l = 0; l < L; l++) for (int c = 0; c < columns; c++) cells[l, c] = [];
        for (int v = 0; v < n; v++) cells[laneOf[v], column[v]].Add(v);

        var preds = new List<int>[n];
        var succs = new List<int>[n];
        for (int v = 0; v < n; v++) { preds[v] = []; succs[v] = []; }
        foreach (GraphEdge e in g.Edges) { preds[e.To].Add(e.From); succs[e.From].Add(e.To); }

        var slot = new int[n];
        var laneRank = new double[n];
        void Recompute()
        {
            for (int l = 0; l < L; l++)
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
                for (int l = 0; l < L; l++)
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

        // ---- per-tier slot allocation across the viewport width. Lane ORDER is fixed left to
        // right; each tier band gives its width only to the lanes that have nodes in it (most lanes
        // are empty in most tiers), so the drawing is compact without ever exceeding the width.
        var shut = new bool[L];
        var count = new int[L];
        for (int l = 0; l < L; l++)
        {
            shut[l] = collapsed is not null && l < collapsed.Count && collapsed[l];
            for (int c = 0; c < columns; c++) count[l] += cells[l, c].Count;
        }
        double inner = Math.Max(0, o.ViewportWidth - 2 * (o.Margin + o.Spine));
        // inner = Σ segments of one lane row = s·(card + gap) + k·(Gutter − gap) for k segments ≤ L and s ≤ S slots.
        // UR-4: S is what the width holds at the MINIMUM card width — never forced up to the lane count (that drove the
        // cards to 123 px at 1366×768). A tier with more lanes than slots wraps its lanes into further lane rows.
        double slotSpace = Math.Max(0, inner - L * (o.Gutter - o.SlotGap));
        int S = Math.Max(1, (int)Math.Floor(slotSpace / (o.MinCardWidth + o.SlotGap)));
        double card = Math.Max(24.0, Math.Min(o.MaxCardWidth, slotSpace / S - o.SlotGap));
        double x0 = o.Margin + o.Spine;

        double rowH = o.CardHeight + o.RowGap;
        var ranges = new (int Lo, int Hi)[columns];
        for (int v = 0; v < g.OwnCount; v++)
        {
            int k = AgeRank(g.Node(v).Age);
            if (k == int.MaxValue) continue;
            int c = column[v];
            ranges[c] = ranges[c].Lo == 0 ? (k, k) : (Math.Min(ranges[c].Lo, k), Math.Max(ranges[c].Hi, k));
        }
        var tiers = new TierBand[columns];
        var segments = new List<LaneSegment>();
        var segOf = new LaneSegment?[L, columns];
        double y = o.Margin;
        for (int c = 0; c < columns; c++)
        {
            var slots = new int[L];
            // The lanes present in this tier, in the fixed lane order; at most S of them share a lane row.
            var present = new List<int>();
            for (int l = 0; l < L; l++) if (!shut[l] && cells[l, c].Count > 0) present.Add(l);
            int groups = Math.Max(1, (present.Count + S - 1) / S);
            int per = present.Count == 0 ? 0 : (present.Count + groups - 1) / groups;
            double y0 = y;
            double gy = y + o.TierGap;
            int rowsTotal = 0;
            for (int l = 0; l < L; l++)
                if (cells[l, c].Count > 0 && shut[l])
                    segOf[l, c] = new LaneSegment(l, c, x0, 0, 0, gy, true, gy, 0);   // collapsed: zero width
            for (int grp = 0; grp < groups; grp++)
            {
                int from = grp * per, to = Math.Min(present.Count, from + per);
                if (from >= to) break;
                var members = new bool[L];
                int budget = S;
                for (int k = from; k < to; k++) { members[present[k]] = true; slots[present[k]] = 1; budget--; }
                while (budget > 0)
                {
                    // Give the next slot to the lane whose rows it reduces most: minimise (max rows, Σ rows),
                    // ties to the lowest lane index. Integers only.
                    (int Max, int Sum) best = TierCost(cells, slots, c, members);
                    int pick = -1;
                    for (int l = 0; l < L; l++)
                    {
                        if (!members[l] || slots[l] >= cells[l, c].Count) continue;
                        slots[l]++;
                        (int Max, int Sum) k = TierCost(cells, slots, c, members);
                        slots[l]--;
                        if (k.Max < best.Max || (k.Max == best.Max && k.Sum < best.Sum)) { best = k; pick = l; }
                    }
                    if (pick < 0) break;
                    slots[pick]++; budget--;
                }
                int rows = TierCost(cells, slots, c, members).Max;
                double x = x0;
                for (int l = 0; l < L; l++)
                {
                    if (!members[l]) continue;
                    double w = slots[l] * (card + o.SlotGap) - o.SlotGap + o.Gutter;
                    var seg = new LaneSegment(l, c, x, w, slots[l], gy, false, gy + rows * rowH, rowsTotal);
                    segments.Add(seg);
                    segOf[l, c] = seg;
                    x += w;
                }
                rowsTotal += rows;
                gy += rows * rowH;
                if (grp + 1 < groups) gy += o.LaneWrapGap;
            }
            y = gy;
            tiers[c] = new TierBand(c, y0, y, rowsTotal, ranges[c].Lo, ranges[c].Hi);
        }
        double height = y + o.Margin;
        double width = o.ViewportWidth;

        var laneBoxes = new LaneBox[L];
        for (int l = 0; l < L; l++)
            laneBoxes[l] = new LaneBox(l, lanes[l].Id, lanes[l].Name, lanes[l].Branch, lanes[l].External, shut[l], count[l]);

        var placed = new PlacedVertex[n];
        for (int v = 0; v < n; v++)
        {
            LaneSegment seg = segOf[laneOf[v], column[v]]!;
            TierBand t = tiers[column[v]];
            if (seg.Collapsed)
            {
                placed[v] = new PlacedVertex(v, column[v], laneOf[v], slot[v], seg.X, t.Y0, o.CollapsedWidth, 0, true);
                continue;
            }
            bool ext = g.Vertices[v].External;
            int row = slot[v] / seg.Slots, k = slot[v] % seg.Slots;
            double vx = seg.X + k * (card + o.SlotGap);
            double vy = seg.Y0 + row * rowH;
            placed[v] = new PlacedVertex(v, column[v], laneOf[v], slot[v], vx, vy, card, ext ? o.AnchorHeight : o.CardHeight, false, seg.RowBase + row);
        }

        return new TreeLayout(g, placed, laneBoxes, segments, tiers, columns, width, height, o);
    }

    /// <summary>(max rows, Σ rows) of one lane row of a tier for a slot assignment — the integer objective.</summary>
    private static (int Max, int Sum) TierCost(List<int>[,] cells, int[] slots, int c, bool[] members)
    {
        int mx = 0, sum = 0;
        for (int l = 0; l < slots.Length; l++)
        {
            if (slots[l] == 0 || !members[l]) continue;
            int r = (cells[l, c].Count + slots[l] - 1) / slots[l];
            mx = Math.Max(mx, r); sum += r;
        }
        return (mx, sum);
    }

    /// <summary>The composite key (barycentre, vertex index): ties break on the integer, so the
    /// order never depends on sort stability or the input order.</summary>
    public static void SortByBarycentre((double Bary, int V)[] keyed) =>
        Array.Sort(keyed, static (a, b) => a.Bary != b.Bary ? a.Bary.CompareTo(b.Bary) : a.V.CompareTo(b.V));

    // ------------------------------------------------------------------ edges

    /// <summary>
    /// The orthogonal route of one edge, never under a card. Horizontal runs use ROW GAPS (every
    /// lane's rows in a tier share them, so they hold no cards); vertical runs use either the
    /// prerequisite's own column (when it is in the last row of the tier just above a dependent in
    /// the first row: a short elbow) or one of the two SPINES — reserved channels at the left and
    /// right edges — whichever is nearer the pair. <paramref name="lane"/> offsets an edge inside
    /// a spine (0..4) so a bundle of highlighted edges stays readable. Empty if either end is hidden.
    /// </summary>
    public static (double X, double Y)[] Route(TreeLayout layout, GraphEdge e, int lane = 0)
    {
        PlacedVertex a = layout.Placed[e.From], b = layout.Placed[e.To];
        if (a.Hidden || b.Hidden) return [];
        TreeLayoutOptions o = layout.Options;
        double y2 = b.Y - o.RowGap / 2.0;   // the row gap (or tier strip floor) above b
        bool lastRow = a.Row == layout.Tiers[a.Column].Rows - 1;
        if (lastRow && b.Row == 0 && b.Column == a.Column + 1)
            return [(a.CenterX, a.Bottom), (a.CenterX, y2), (b.CenterX, y2), (b.CenterX, b.Y)];
        double y1 = a.Y + o.CardHeight + o.RowGap / 2.0;   // the row gap under a's row
        double off = (lane % 5) * 3.0;
        bool left = a.CenterX + b.CenterX <= layout.Width;
        double sx = left ? o.Margin + o.Spine / 2.0 - 6 + off : layout.Width - o.Margin - o.Spine / 2.0 + 6 - off;
        return [(a.CenterX, a.Bottom), (a.CenterX, y1), (sx, y1), (sx, y2), (b.CenterX, y2), (b.CenterX, b.Y)];
    }

    /// <summary>The DIRECT prerequisites and dependents of a vertex (graph-local indices,
    /// ascending, distinct) — exactly what hover highlighting lights up.</summary>
    public static (int[] Prerequisites, int[] Dependents) Neighbours(ResearchGraph g, int vertex)
    {
        var pre = new List<int>();
        var dep = new List<int>();
        foreach (GraphEdge e in g.Edges)
        {
            if (e.To == vertex && !pre.Contains(e.From)) pre.Add(e.From);
            if (e.From == vertex && !dep.Contains(e.To)) dep.Add(e.To);
        }
        pre.Sort(); dep.Sort();
        return ([.. pre], [.. dep]);
    }

    // ------------------------------------------------------------------ default scroll

    /// <summary>
    /// THE LEAST-DEVELOPED BRANCH and its frontier. For every non-external lane with nodes, the
    /// lane's FRONTIER is its not-yet-completed node (available, target, partial or locked) with
    /// the smallest key (tier, slot, vertex) — the earliest along the scroll axis. The least
    /// developed lane is the one whose frontier tier is smallest; ties go to the lowest lane
    /// index (stable lane order). Collapse state is ignored (it is UI state, tiers are content).
    /// Returns the frontier VERTEX, or -1 when every node of every lane is complete.
    /// </summary>
    public static int LeastDevelopedFrontier(TreeLayout layout, IReadOnlyList<bool> completedByVertex)
    {
        int best = -1, bestTier = int.MaxValue, bestLane = int.MaxValue;
        foreach (LaneBox lane in layout.Lanes)
        {
            if (lane.External) continue;
            int f = -1;
            for (int v = 0; v < layout.Graph.OwnCount; v++)
            {
                PlacedVertex p = layout.Placed[v];
                if (p.Lane != lane.Index || completedByVertex[v]) continue;
                if (f < 0 || p.Column < layout.Placed[f].Column
                    || (p.Column == layout.Placed[f].Column && (p.Slot < layout.Placed[f].Slot || (p.Slot == layout.Placed[f].Slot && v < f))))
                    f = v;
            }
            if (f < 0) continue;
            int t = layout.Placed[f].Column;
            if (t < bestTier || (t == bestTier && lane.Index < bestLane)) { best = f; bestTier = t; bestLane = lane.Index; }
        }
        return best;
    }

    // ------------------------------------------------------------------ Ages

    /// <summary>The Ages present in each tier as an honest RANGE (one tier can hold nodes of
    /// several Ages): per tier the lowest and highest Age rank, or (0, 0) for none.</summary>
    public static (int Lo, int Hi)[] ColumnAgeRanges(TreeLayout layout)
    {
        var r = new (int Lo, int Hi)[layout.Columns];
        for (int c = 0; c < layout.Columns; c++) r[c] = (layout.Tiers[c].Lo, layout.Tiers[c].Hi);
        return r;
    }

    /// <summary>Numeric rank of an Age label ("A3" → 3); labels of another form rank after all
    /// numbered Ages (int.MaxValue) so they never break the monotonic order.</summary>
    public static int AgeRank(string age) =>
        age.Length >= 2 && age[0] == 'A' && int.TryParse(age.AsSpan(1), System.Globalization.NumberStyles.None,
            System.Globalization.CultureInfo.InvariantCulture, out int k) ? k : int.MaxValue;

    /// <summary>The nine Ages' names (Director ledger): index = rank − 1.</summary>
    public static readonly IReadOnlyList<string> AgeNames =
    [
        "Prehistoric / Stone Age", "Neolithic / Agricultural", "Bronze Age", "Iron Age", "Classical / Imperial",
        "Medieval", "Early Modern", "Industrial", "Modern / Contemporary",
    ];

    /// <summary>Full name of an Age rank ("Bronze Age"), or "" outside 1..9.</summary>
    public static string AgeName(int rank) => rank >= 1 && rank <= AgeNames.Count ? AgeNames[rank - 1] : "";

    /// <summary>Short roman numeral of a numbered Age ("A3" → "III"); otherwise the label.</summary>
    public static string AgeNumeral(string age)
    {
        int k = AgeRank(age);
        return k >= 1 && k <= Roman.Length ? Roman[k - 1] : age;
    }

    /// <summary>"A2" → "Age II — Neolithic / Agricultural": never the numeral alone.</summary>
    public static string AgeLabel(string age)
    {
        int k = AgeRank(age);
        if (k >= 1 && k <= AgeNames.Count) return "Age " + Roman[k - 1] + " — " + AgeNames[k - 1];
        if (k >= 1 && k <= Roman.Length) return "Age " + Roman[k - 1];
        return age;
    }

    /// <summary>The card form: "II · Neolithic / Agricultural" — numeral plus the full name.</summary>
    public static string AgeShort(string age)
    {
        int k = AgeRank(age);
        return k >= 1 && k <= AgeNames.Count ? Roman[k - 1] + " · " + AgeNames[k - 1] : AgeLabel(age);
    }

    /// <summary>A tier's Age range with full names: "Age III — Bronze Age" or
    /// "Ages I–IV — Prehistoric / Stone Age to Iron Age"; "" for none.</summary>
    public static string AgeRangeLabel((int Lo, int Hi) range) =>
        range.Lo == 0 ? ""
        : range.Lo == range.Hi ? "Age " + Numeral(range.Lo) + " — " + AgeName(range.Lo)
        : "Ages " + Numeral(range.Lo) + "–" + Numeral(range.Hi) + " — " + AgeName(range.Lo) + " to " + AgeName(range.Hi);

    private static string Numeral(int k) => k >= 1 && k <= Roman.Length ? Roman[k - 1] : k.ToString(System.Globalization.CultureInfo.InvariantCulture);

    private static readonly string[] Roman = ["I", "II", "III", "IV", "V", "VI", "VII", "VIII", "IX", "X", "XI", "XII"];

    public static string Title(string id)
    {
        var parts = id.Split('_', StringSplitOptions.RemoveEmptyEntries);
        for (int i = 0; i < parts.Length; i++) parts[i] = char.ToUpperInvariant(parts[i][0]) + parts[i][1..];
        return string.Join(' ', parts);
    }
}
