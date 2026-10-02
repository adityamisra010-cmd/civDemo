using System.Globalization;
using Sim.Core.Kernel;
using Sim.Core.State;
using Sim.Core.Systems.Research;
using Sim.Ui.Render;
using Sim.Ui.ViewModel;
using static Sim.Ui.Progression.ProgressionPalette;
using Rgba = Sim.Ui.Art.ParchmentPalette.Rgba;

namespace Sim.Ui.Progression;

/// <summary>The two sibling trees under KNOWLEDGE &amp; TECHNOLOGY (ruling 11).</summary>
public enum TreeTab { Technology = 0, Civics = 1 }

public enum HitKind { Lens, Tab, SetTarget, Close, Minimap, LaneToggle, Frontier, Fit, MinimapToggle }

public readonly record struct HitRegion(RectD Rect, HitKind Kind, int Arg);

/// <summary>What a click asks the host to do. The screen never mutates state: a research
/// choice is returned as the SetResearchTarget order the existing factory built.</summary>
public sealed record ProgressionCommand(bool Close, OrderRecord? Order, ResearchNodeId? Node)
{
    public static readonly ProgressionCommand None = new(false, null, null);
}

/// <summary>
/// THE KNOWLEDGE &amp; TECHNOLOGY PROGRESSION SCREEN — full-screen, headless and pure. It owns
/// only UI state (lens, tab, camera, hover, selection); it reads the world through
/// <see cref="ResearchSnapshot"/> (ResearchQuery) and paints into a backend-agnostic
/// <see cref="DrawList"/> that the ImGui backend replays in the game and
/// <see cref="SvgWriter"/> replays for previews. Clicks return orders; they never write.
/// </summary>
public sealed class ProgressionScreen
{
    public const double LensBarH = 58, TabBarH = 50, DetailW = 404, ColumnHeaderH = 34, ControlRowH = 38, HeaderH = ColumnHeaderH + ControlRowH;

    public ResearchContent Content { get; }
    /// <summary>Whether the overview minimap is shown (toggled by its button).</summary>
    public bool MinimapVisible { get; set; } = true;
    public PolityId Polity { get; }
    public ResearchGraph[] Graphs { get; }
    public TreeLayout[] Layouts { get; }
    public ProgressionCamera[] Cameras { get; } = [new(), new()];

    public Lens Lens { get; set; } = Lens.KnowledgeAndTechnology;
    public TreeTab Tab { get; set; } = TreeTab.Technology;
    public ResearchSnapshot? Snapshot { get; private set; }
    public LensPage? Page { get; private set; }
    /// <summary>Selected / hovered CONTENT index, or -1.</summary>
    public int Selected { get; set; } = -1;
    public int Hovered { get; private set; } = -1;
    /// <summary>A target ordered this turn, awaiting End Turn (UI hint only).</summary>
    public int PendingTarget { get; private set; } = -1;

    private IReadOnlyWorldState? _world;
    private readonly bool[] _framed = new bool[2];
    private List<HitRegion> _hits = [];
    private double _w = 1280, _h = 800;

    public ProgressionScreen(ResearchContent content, PolityId polity)
    {
        Content = content;
        Polity = polity;
        Graphs = [ResearchGraph.Build(content, ResearchTree.Technology), ResearchGraph.Build(content, ResearchTree.Civics)];
        Layouts = [ResearchTreeLayout.Compute(Graphs[0]), ResearchTreeLayout.Compute(Graphs[1])];
        _collapsed = [new bool[Layouts[0].Lanes.Count], new bool[Layouts[1].Lanes.Count]];
        _ageRanges = [ResearchTreeLayout.ColumnAgeRanges(Layouts[0]), ResearchTreeLayout.ColumnAgeRanges(Layouts[1])];
    }

    private readonly bool[][] _collapsed;
    private readonly (int Lo, int Hi)[][] _ageRanges;

    public bool IsLaneCollapsed(int lane) => _collapsed[(int)Tab][lane];

    /// <summary>Collapse or expand one lane of the current tree (UI state only). The layout
    /// is recomputed; the camera keeps the lane's header where it was on screen.</summary>
    public void ToggleLane(int lane)
    {
        int t = (int)Tab;
        double before = Layouts[t].Lanes[lane].Y;
        _collapsed[t][lane] = !_collapsed[t][lane];
        Layouts[t] = ResearchTreeLayout.Compute(Graphs[t], Layouts[t].Options, _collapsed[t]);
        double after = Layouts[t].Lanes[lane].Y;
        Camera.Set(Camera.Zoom, Camera.PanX, Camera.PanY + (after - before));
        if (Selected >= 0 && Content.Nodes[Selected].Tree == (ResearchTree)(t + 1) && Layout.Placed[Graph.VertexOf(Selected)].Hidden) Selected = -1;
        RectD c = Canvas;
        Camera.Clamp(Layout.Width, Layout.Height, c.W, c.H);
        Camera.Set(Camera.TargetZoom, Camera.TargetPanX, Camera.TargetPanY);
    }

    /// <summary>The frontier node of the current tree: the research target when it lies in this
    /// tree, else the available node in the leftmost column (ties: lowest vertex). -1 if none.</summary>
    public int FrontierNode()
    {
        if (Snapshot is null) return -1;
        if (Snapshot.TargetIndex is int t && Content.Nodes[t].Tree == (ResearchTree)((int)Tab + 1)) return t;
        int focus = -1, bestCol = int.MaxValue;
        for (int v = 0; v < Graph.OwnCount; v++)
        {
            int ci = Graph.Vertices[v].ContentIndex;
            if (Snapshot.Nodes[ci].Available && Layout.Placed[v].Column < bestCol) { bestCol = Layout.Placed[v].Column; focus = ci; }
        }
        return focus;
    }

    /// <summary>Glide to the frontier (expanding its lane if it was collapsed) and select it.</summary>
    public void JumpToFrontier(bool jump = false)
    {
        int f = FrontierNode();
        if (f < 0) { FitAll(); return; }
        int lane = Layout.Placed[Graph.VertexOf(f)].Lane;
        if (IsLaneCollapsed(lane)) ToggleLane(lane);
        Focus(f, jump);
    }

    public ResearchGraph Graph => Graphs[(int)Tab];
    public TreeLayout Layout => Layouts[(int)Tab];
    public ProgressionCamera Camera => Cameras[(int)Tab];
    public IReadOnlyList<HitRegion> Hits => _hits;
    public RectD Canvas => Lens == Lens.KnowledgeAndTechnology
        ? new RectD(0, LensBarH + TabBarH + HeaderH, Math.Max(100, _w - DetailW), Math.Max(100, _h - LensBarH - TabBarH - HeaderH))
        : new RectD(0, LensBarH, _w, Math.Max(100, _h - LensBarH));

    /// <summary>Re-read the world (cheap to call every frame: rebuilds only when the world changed).</summary>
    public void Refresh(IReadOnlyWorldState world)
    {
        if (ReferenceEquals(world, _world) && Snapshot is not null) return;
        if (Snapshot is not null && world.Clock.Turn != Snapshot.Turn) PendingTarget = -1;
        _world = world;
        Snapshot = ResearchSnapshot.Build(world, Content, Polity);
        Page = Lenses.Page(Lens, world, Content, Polity);
    }

    public void Resize(double width, double height) { _w = width; _h = height; }

    // ------------------------------------------------------------------ input

    public void PointerMove(double x, double y) => Hovered = NodeAt(x, y);

    /// <summary>Content index of the card under a screen point on the current tree, or -1.</summary>
    public int NodeAt(double x, double y)
    {
        if (Lens != Lens.KnowledgeAndTechnology) return -1;
        RectD c = Canvas;
        if (!c.Contains(x, y) ||  (MinimapVisible && MinimapRect().Contains(x, y))) return -1;
        int v = Layout.HitTest(Camera.ToWorldX(x, c.X), Camera.ToWorldY(y, c.Y));
        return v < 0 ? -1 : Graph.Vertices[v].ContentIndex;
    }

    public void Wheel(double x, double y, double notches)
    {
        RectD c = Canvas;
        if (Lens != Lens.KnowledgeAndTechnology || !c.Contains(x, y)) return;
        Camera.ZoomAt(x - c.X, y - c.Y, Math.Pow(1.18, notches));
        Camera.Clamp(Layout.Width, Layout.Height, c.W, c.H);
    }

    public void Drag(double dx, double dy)
    {
        if (Lens != Lens.KnowledgeAndTechnology) return;
        Camera.Drag(dx, dy);
        RectD c = Canvas;
        Camera.Clamp(Layout.Width, Layout.Height, c.W, c.H);
    }

    public void ScrollBy(double dx, double dy)
    {
        if (Lens != Lens.KnowledgeAndTechnology) return;
        Camera.ScrollBy(dx, dy);
        RectD c = Canvas;
        Camera.Clamp(Layout.Width, Layout.Height, c.W, c.H);
    }

    public bool Advance(double dt) => Camera.Advance(dt);

    /// <summary>
    /// A click at a screen point. Chrome first (lens, tab, close, the detail panel's button,
    /// the minimap), then the tree: a card is SELECTED, and if the node is available to the
    /// player now, the SetResearchTarget order is built by <see cref="ResearchOrderFactory"/>
    /// and returned for the host to log. Locked or completed nodes only select.
    /// </summary>
    public ProgressionCommand Click(double x, double y)
    {
        foreach (HitRegion h in _hits)
        {
            if (!h.Rect.Contains(x, y)) continue;
            switch (h.Kind)
            {
                case HitKind.Close: return new ProgressionCommand(true, null, null);
                case HitKind.Lens: SetLens((Lens)h.Arg); return ProgressionCommand.None;
                case HitKind.Tab: Tab = (TreeTab)h.Arg; return ProgressionCommand.None;
                case HitKind.SetTarget: return Issue(h.Arg);
                case HitKind.LaneToggle: ToggleLane(h.Arg); return ProgressionCommand.None;
                case HitKind.Frontier: JumpToFrontier(); return ProgressionCommand.None;
                case HitKind.Fit: FitAll(); return ProgressionCommand.None;
                case HitKind.MinimapToggle: MinimapVisible = !MinimapVisible; return ProgressionCommand.None;
                case HitKind.Minimap:
                {
                    RectD m = h.Rect;
                    RectD c = Canvas;
                    Camera.CenterOn((x - m.X) / m.W * Layout.Width, (y - m.Y) / m.H * Layout.Height, c.W, c.H);
                    return ProgressionCommand.None;
                }
            }
        }
        int node = NodeAt(x, y);
        if (node < 0) return ProgressionCommand.None;
        Selected = node;
        return Issue(node);
    }

    private ProgressionCommand Issue(int node)
    {
        if (_world is null || Snapshot is null) return ProgressionCommand.None;
        ResearchNodeView v = Snapshot.Nodes[node];
        if (!v.Available || v.IsTarget || PendingTarget == node) return ProgressionCommand.None;
        OrderRecord? order = ResearchOrderFactory.SetTarget(_world, Content, Polity, v.Key);
        if (order is null) return ProgressionCommand.None;
        PendingTarget = node;
        return new ProgressionCommand(false, order, v.Key);
    }

    public void SetLens(Lens lens)
    {
        Lens = lens;
        if (_world is not null) Page = Lenses.Page(lens, _world, Content, Polity);
    }

    /// <summary>Select a node by id and glide to it (used by previews and keyboard focus).</summary>
    public void Focus(int contentIndex, bool jump = false)
    {
        Selected = contentIndex;
        Tab = Content.Nodes[contentIndex].Tree == ResearchTree.Civics ? TreeTab.Civics : TreeTab.Technology;
        PlacedVertex p = Layout.Placed[Graph.VertexOf(contentIndex)];
        RectD c = Canvas;
        Camera.CenterOn(p.CenterX, p.CenterY, c.W, c.H);
        Camera.Clamp(Layout.Width, Layout.Height, c.W, c.H);
        if (jump) Camera.Set(Camera.TargetZoom, Camera.TargetPanX, Camera.TargetPanY);
        _framed[(int)Tab] = true;
    }

    /// <summary>Fit the whole tree in the canvas.</summary>
    public void FitAll()
    {
        RectD c = Canvas;
        double z = Math.Max(ProgressionCamera.MinZoom, Math.Min(1.0, Math.Min(c.W / Layout.Width, c.H / Layout.Height)));
        Camera.Set(z, (Layout.Width - c.W / z) / 2.0, -8.0 / z);   // top-aligned: the trunk starts under the header
        _framed[(int)Tab] = true;
    }

    private void FrameFirstTime()
    {
        if (_framed[(int)Tab] || Snapshot is null) return;
        _framed[(int)Tab] = true;
        // Open on the frontier: the current target, else the leftmost available node.
        int focus = FrontierNode();
        if (focus < 0) { Camera.Set(0.8, 0, 0); return; }
        int prev = Selected;
        Focus(focus, jump: true);
        Selected = prev;
    }

    // ------------------------------------------------------------------ paint

    public DrawList Paint(double width, double height, ITextMeasure m)
    {
        Resize(width, height);
        var d = new DrawList();
        _hits = [];
        d.Rect(new RectD(0, 0, width, height), Field);
        if (Snapshot is null) return d;

        if (Lens == Lens.KnowledgeAndTechnology)
        {
            FrameFirstTime();
            PaintTree(d, m);
            PaintTabBar(d, m);
            PaintDetail(d, m);
        }
        else PaintLensPage(d, m);
        PaintLensBar(d, m);
        return d;
    }

    private static string Num(double v) => Math.Round(v).ToString("#,0", CultureInfo.InvariantCulture);
    private static string Pct(double f) => Math.Round(f * 100).ToString("0", CultureInfo.InvariantCulture) + "%";

    private void PaintLensBar(DrawList d, ITextMeasure m)
    {
        d.Rect(new RectD(0, 0, _w, LensBarH), Chrome);
        d.Line(0, LensBarH - 0.5, _w, LensBarH - 0.5, GoldDim, 1);
        d.Text(20, 10, "PROGRESSION", 11, GoldDim, TextAlign.Left, FontRole.Caps);
        d.Text(20, 26, "Turn " + Snapshot!.Turn.ToString(CultureInfo.InvariantCulture), 17, Text, TextAlign.Left, FontRole.Heading);

        double x0 = 150, right = _w - 150;
        double size = 13;
        double total = 0;
        foreach (Lens l0 in Lenses.All) total += m.Width(Lenses.Label(l0), size, FontRole.Caps) + 34;
        if (total > right - x0) { size *= (right - x0) / total; total = right - x0; }
        double extra = (right - x0 - total) / Lenses.All.Count;
        double lx0 = x0;
        for (int i = 0; i < Lenses.All.Count; i++)
        {
            Lens l = Lenses.All[i];
            double slotW = m.Width(Lenses.Label(l), size, FontRole.Caps) + 34 + extra;
            var r = new RectD(lx0 + 3, 8, slotW - 6, LensBarH - 16);
            lx0 += slotW;
            bool on = l == Lens;
            LensStatus status = l == Lens.KnowledgeAndTechnology ? LensStatus.Functional
                : l == Lens.Industry ? LensStatus.NotYetSimulated : LensStatus.PartialData;
            d.Rect(r, on ? ChromeRaised : null, on ? Gold : null, 1, 4);
            if (on) d.Rect(new RectD(r.X + 10, r.Bottom - 3, r.W - 20, 3), Gold);
            Rgba col = on ? Gold : status == LensStatus.NotYetSimulated ? TextDim : TextSoft;
            d.Text(r.CenterX, r.Y + 7, Ink.Fit(m, Lenses.Label(l), size, r.W - 8, FontRole.Caps), size, col, TextAlign.Center, FontRole.Caps);
            string sub = status switch { LensStatus.Functional => "simulated", LensStatus.PartialData => "partial", _ => "not yet simulated" };
            d.Text(r.CenterX, r.Y + 24, sub, 10.5, on ? TextSoft : TextDim, TextAlign.Center, FontRole.Body);
            _hits.Add(new HitRegion(r, HitKind.Lens, (int)l));
        }

        d.Text(_w - 64, 12, Snapshot.PointsPerTurn.ToString("0.0", CultureInfo.InvariantCulture), 18, Cyan, TextAlign.Right, FontRole.Numeric);
        d.Text(_w - 64, 34, "research / turn", 10.5, TextSoft, TextAlign.Right);
        var close = new RectD(_w - 52, 12, 36, 34);
        d.Rect(close, ChromeRaised, Hairline, 1, 4);
        d.Line(close.X + 12, close.Y + 11, close.Right - 12, close.Bottom - 11, Text, 2);
        d.Line(close.Right - 12, close.Y + 11, close.X + 12, close.Bottom - 11, Text, 2);
        _hits.Add(new HitRegion(close, HitKind.Close, 0));
    }

    private void PaintTabBar(DrawList d, ITextMeasure m)
    {
        double y = LensBarH;
        d.Rect(new RectD(0, y, _w, TabBarH), Rgba.Hex(0x0C1117));
        d.Line(0, y + TabBarH - 0.5, _w, y + TabBarH - 0.5, Hairline, 1);
        ResearchSnapshot s = Snapshot!;
        string[] names = ["TECHNOLOGY", "CIVICS"];
        string[] counts = [s.CompletedTechnology + " / " + Content.TechnologyCount, s.CompletedCivics + " / " + Content.CivicsCount];
        for (int i = 0; i < 2; i++)
        {
            var r = new RectD(16 + i * 196, y + 7, 186, TabBarH - 14);
            bool on = (int)Tab == i;
            d.Rect(r, on ? ChromeRaised : null, on ? GoldDim : Hairline, 1, 5);
            d.Text(r.X + 14, r.Y + 9, names[i], 14, on ? Gold : TextSoft, TextAlign.Left, FontRole.Caps);
            d.Text(r.Right - 12, r.Y + 10, counts[i], 13, on ? Text : TextDim, TextAlign.Right, FontRole.Numeric);
            _hits.Add(new HitRegion(r, HitKind.Tab, i));
        }

        // The current target capsule.
        double cx = 420;
        var cap = new RectD(cx, y + 7, Math.Min(420, _w - DetailW - cx - 20), TabBarH - 14);
        if (cap.W > 160)
        {
            d.Rect(cap, Rgba.Hex(0x0F2229), A(0x5FD3E6, 0.5), 1, 5);
            if (s.TargetIndex is int t)
            {
                ResearchNodeView v = s.Nodes[t];
                d.Text(cap.X + 12, cap.Y + 4, "RESEARCHING", 9.5, Cyan, TextAlign.Left, FontRole.Caps);
                d.Text(cap.X + 12, cap.Y + 16, Ink.Fit(m, v.Name, 14, cap.W - 150, FontRole.Heading), 14, Text, TextAlign.Left, FontRole.Heading);
                var bar = new RectD(cap.Right - 128, cap.Y + 21, 76, 6);
                d.Bar(bar, v.Fraction, Cyan, A(0x5FD3E6, 0.5), A(0x000000, 0.4));
                d.Text(cap.Right - 12, cap.Y + 13, Pct(v.Fraction), 13, Text, TextAlign.Right, FontRole.Numeric);
            }
            else
            {
                d.Text(cap.X + 12, cap.Y + 10, PendingTarget >= 0
                    ? "Target ordered: " + s.Nodes[PendingTarget].Name + " - applies at End Turn"
                    : "No research target - research points are idle. Choose an available node.", 12.5,
                    PendingTarget >= 0 ? Cyan : Amber, TextAlign.Left);
            }
        }

        // Legend.
        double lx = _w - DetailW + 14;
        (string, Rgba, Rgba)[] keys = [("Done", CompletedFill, Gold), ("Target", TargetFill, Cyan), ("Open", AvailableFill, AvailableEdge), ("Locked", LockedFill, LockedEdge)];
        foreach ((string label, Rgba fill, Rgba edge) in keys)
        {
            d.Rect(new RectD(lx, y + 18, 14, 12), fill, edge, 1.5, 2);
            d.Text(lx + 19, y + 16, label, 11.5, TextSoft);
            lx += 26 + m.Width(label, 11.5, FontRole.Body) + 8;
        }
        d.Line(lx, y + 24, lx + 22, y + 24, TextSoft, 1.6);
        d.Text(lx + 26, y + 16, "all of", 11.5, TextSoft);
        lx += 30 + m.Width("all of", 11.5, FontRole.Body) + 8;
        d.Line(lx, y + 24, lx + 22, y + 24, TextSoft, 1.6, (4, 3));
        d.Text(lx + 26, y + 16, "one of", 11.5, TextSoft);
    }

    // ---- the tree canvas

    private (double, double, double, double) VisibleWorld()
    {
        RectD c = Canvas;
        return (Camera.ToWorldX(c.X, c.X), Camera.ToWorldY(c.Y, c.Y), Camera.ToWorldX(c.Right, c.X), Camera.ToWorldY(c.Bottom, c.Y));
    }

    /// <summary>Vertices whose card intersects the visible world rect (off-screen culling).</summary>
    public List<int> VisibleVertices()
    {
        (double x0, double y0, double x1, double y1) = VisibleWorld();
        var result = new List<int>();
        foreach (PlacedVertex p in Layout.Placed)
            if (p.Right >= x0 && p.X <= x1 && p.Bottom >= y0 && p.Y <= y1) result.Add(p.Vertex);
        return result;
    }

    private void PaintTree(DrawList d, ITextMeasure m)
    {
        RectD c = Canvas;
        TreeLayout L = Layout;
        ProgressionCamera cam = Camera;
        double z = cam.Zoom;
        double SX(double wx) => cam.ToScreenX(wx, c.X);
        double SY(double wy) => cam.ToScreenY(wy, c.Y);
        (double vx0, double vy0, double vx1, double vy1) = VisibleWorld();
        TreeLayoutOptions o = L.Options;

        d.PushClip(c);
        // Depth columns: alternate shading (a column is a prerequisite depth, not an Age).
        for (int col = 0; col < L.Columns; col++)
        {
            double x0 = SX(o.Margin + o.LeftGutter + col * o.ColumnWidth);
            if (col % 2 == 1) d.Rect(new RectD(x0, c.Y, o.ColumnWidth * z, c.H), FieldBand);
        }
        // Lane bands with a header strip.
        foreach (LaneBox lane in L.Lanes)
        {
            Rgba hue = Branch(lane.Id);
            double y = SY(lane.Y), h = lane.Height * z;
            if (y > c.Bottom || y + h < c.Y) continue;
            d.Rect(new RectD(c.X, y, c.W, h), A(Hex(hue), lane.Collapsed ? 0.07 : 0.035));
            d.Rect(new RectD(c.X, y, c.W, o.LaneHeader * z), A(Hex(hue), 0.06));
            d.Line(c.X, y, c.Right, y, A(Hex(hue), 0.45), 1);
        }

        int focus = Hovered >= 0 ? Hovered : Selected;
        int focusV = focus >= 0 ? Graph.VertexOf(focus) : -1;

        // Edges (culled by their bounding box; edges into or out of a collapsed lane are hidden).
        ResearchSnapshot s0 = Snapshot!;
        for (int pass = 0; pass < 2; pass++)
            foreach (GraphEdge e in Graph.Edges)
            {
                bool hot = focusV >= 0 && (e.From == focusV || e.To == focusV);
                if ((pass == 1) != hot) continue;
                PlacedVertex a = L.Placed[e.From], b = L.Placed[e.To];
                if (a.Hidden || b.Hidden) continue;
                double ax = a.Right, ay = a.CenterY, bx = b.X, by = b.CenterY;
                if (Math.Max(ax, bx) < vx0 || Math.Min(ax, bx) > vx1 || Math.Max(ay, by) < vy0 || Math.Min(ay, by) > vy1) continue;
                bool done = s0.Nodes[Graph.Vertices[e.From].ContentIndex].State == NodeState.Completed;
                Rgba col = hot ? (done ? Gold : Cyan) : done ? A(0xB89A50, 0.5) : A(0x5A6878, 0.32);
                double w = (hot ? 2.4 : 1.2) * Math.Max(0.6, z);
                double dx = Math.Max(30, (bx - ax) * 0.5);
                (double, double)? dash = e.Kind == EdgeKind.Or ? (6.0 * Math.Max(0.6, z), 4.0 * Math.Max(0.6, z)) : null;
                d.Bezier((SX(ax), SY(ay)), (SX(ax + dx), SY(ay)), (SX(bx - dx), SY(by)), (SX(bx), SY(by)), col, w, dash);
                if (z > 0.35) d.Circle(SX(bx), SY(by), 2.6 * z, col);
            }

        foreach (int v in VisibleVertices())
            if (!L.Placed[v].Hidden) PaintCard(d, m, v, SX(L.Placed[v].X), SY(L.Placed[v].Y), z, v == focusV);

        // Lane chips: each lane's chip sits in its own header strip; the lane whose header has
        // scrolled above the view gets its chip pinned in the control row instead, so a chip never
        // covers a card. Each chip is the lane's collapse/expand control.
        double top = c.Y;
        _pinnedLane = null;
        double lastChipBottom = double.NegativeInfinity;
        foreach (LaneBox lane in L.Lanes)
        {
            double ly0 = SY(lane.Y), ly1 = SY(lane.Y + lane.Height);
            if (ly1 < top || ly0 > c.Bottom) continue;
            if (ly0 + 2 < top) _pinnedLane = lane;
            else
            {
                // Zoomed far out, lane strips get thinner than a chip: stack chips without overlap.
                double y = Math.Max(ly0 + Math.Max(1, (o.LaneHeader * z - 22) / 2), lastChipBottom + 3);
                if (y > c.Bottom) continue;
                LaneChip(d, m, lane, c.X + 10, y);
                lastChipBottom = y + 22;
            }
        }

        // Sticky column header: prerequisite depth tiers with the honest Age range of each.
        if (MinimapVisible) PaintMinimap(d);
        d.PopClip();

        // Header rows sit above the camera canvas (never over a card).
        double hy = c.Y - HeaderH;
        d.PushClip(new RectD(c.X, hy, c.W, HeaderH));
        d.Rect(new RectD(c.X, hy, c.W, ColumnHeaderH), Rgba.Hex(0x0C1117));
        (int Lo, int Hi)[] ages = _ageRanges[(int)Tab];
        for (int col = 0; col < L.Columns; col++)
        {
            double x0 = SX(o.Margin + o.LeftGutter + col * o.ColumnWidth), x1 = x0 + o.ColumnWidth * z;
            if (x1 < c.X || x0 > c.Right) continue;
            d.Line(x0, hy + 8, x0, hy + ColumnHeaderH - 8, Hairline, 1);
            if (x1 - x0 < 70)
            {
                if (x1 - x0 >= 20) d.Text((x0 + x1) / 2, hy + 10, (col + 1).ToString(CultureInfo.InvariantCulture), 11, GoldDim, TextAlign.Center, FontRole.Numeric);
                continue;
            }
            double cx = (x0 + x1) / 2;
            d.Text(cx, hy + 4, "TIER " + (col + 1).ToString(CultureInfo.InvariantCulture), 12, Gold, TextAlign.Center, FontRole.Caps);
            string range = ResearchTreeLayout.AgeRangeLabel(ages[col]);
            if (range.Length > 0) d.Text(cx, hy + 19, range, 10.5, TextSoft, TextAlign.Center, FontRole.Body);
        }
        d.Line(c.X, hy + ColumnHeaderH, c.Right, hy + ColumnHeaderH, GoldDim, 1);
        PaintControlRow(d, m);
        d.PopClip();
    }

    private LaneBox? _pinnedLane;

    private void LaneChip(DrawList d, ITextMeasure m, LaneBox lane, double x, double y)
    {
        const double chipH = 22;
        Rgba hue = Branch(lane.Id);
        string label = lane.Name.ToUpperInvariant();
        string count = lane.NodeCount.ToString(CultureInfo.InvariantCulture) + (lane.Collapsed ? " hidden" : "");
        double tw = m.Width(label, 12, FontRole.Caps) + m.Width(count, 11, FontRole.Numeric) + 64;
        var chip = new RectD(x, y, tw, chipH);
        d.Rect(chip, A(0x0A0E13, 0.94), A(Hex(hue), 0.8), 1, 11);
        // Disclosure triangle: right when collapsed, down when expanded.
        double tx = chip.X + 13, ty = chip.Y + chipH / 2;
        if (lane.Collapsed) d.Polygon([(tx - 3, ty - 5), (tx + 4, ty), (tx - 3, ty + 5)], hue);
        else d.Polygon([(tx - 5, ty - 3), (tx + 5, ty - 3), (tx, ty + 4)], hue);
        d.Text(chip.X + 24, chip.Y + 4, label, 12, hue, TextAlign.Left, FontRole.Caps);
        d.Text(chip.Right - 10, chip.Y + 5, count, 11, TextSoft, TextAlign.Right, FontRole.Numeric);
        _hits.Add(new HitRegion(chip, HitKind.LaneToggle, lane.Index));
    }

    /// <summary>The opaque control row under the tier header: on the left, the chip of the lane
    /// currently scrolled under the header (so its name stays readable and it can still be
    /// collapsed); on the right, jump to the frontier / target, fit the tree, show/hide the map.</summary>
    private void PaintControlRow(DrawList d, ITextMeasure m)
    {
        RectD c = Canvas;
        var row = new RectD(c.X, c.Y - ControlRowH, c.W, ControlRowH);
        d.Rect(row, Rgba.Hex(0x0F151C));
        d.Line(c.X, row.Bottom, c.Right, row.Bottom, Hairline, 1);
        if (_pinnedLane is LaneBox pl)
        {
            d.Text(c.X + 12, row.Y + 12, "LANE", 10.5, TextDim, TextAlign.Left, FontRole.Caps);
            LaneChip(d, m, pl, c.X + 52, row.Y + (ControlRowH - 22) / 2);
        }
        bool hasTarget = Snapshot!.TargetIndex is int t && Content.Nodes[t].Tree == (ResearchTree)((int)Tab + 1);
        (string Label, HitKind Kind, bool On)[] buttons =
        [
            (hasTarget ? "JUMP TO TARGET" : "JUMP TO FRONTIER", HitKind.Frontier, false),
            ("FIT TREE", HitKind.Fit, false),
            (MinimapVisible ? "HIDE MAP" : "SHOW MAP", HitKind.MinimapToggle, MinimapVisible),
        ];
        double x = c.Right - 12, h = 26, y = row.Y + (ControlRowH - h) / 2;
        for (int i = buttons.Length - 1; i >= 0; i--)
        {
            double w = m.Width(buttons[i].Label, 11.5, FontRole.Caps) + 24;
            x -= w;
            var r = new RectD(x, y, w, h);
            bool primary = buttons[i].Kind == HitKind.Frontier;
            d.Rect(r, primary ? A(0x5FD3E6, 0.16) : A(0x0A0E13, 0.92), primary ? Cyan : buttons[i].On ? GoldDim : Hairline, 1, 5);
            d.Text(r.X + w / 2, r.Y + 7, buttons[i].Label, 11.5, primary ? Cyan : Text, TextAlign.Center, FontRole.Caps);
            _hits.Add(new HitRegion(r, buttons[i].Kind, 0));
            x -= 8;
        }
    }

    private static uint Hex(Rgba c) => ((uint)c.R << 16) | ((uint)c.G << 8) | c.B;

    private void PaintCard(DrawList d, ITextMeasure m, int vertex, double x, double y, double z, bool focus)
    {
        GraphVertex gv = Graph.Vertices[vertex];
        ResearchNodeView v = Snapshot!.Nodes[gv.ContentIndex];
        PlacedVertex p = Layout.Placed[vertex];
        double w = p.W * z, h = p.H * z;
        var r = new RectD(x, y, w, h);
        bool selected = gv.ContentIndex == Selected;

        if (gv.External)
        {
            bool done = v.State == NodeState.Completed;
            d.Rect(r, Rgba.Hex(0x161C24), done ? GoldDim : Hairline, 1, h / 2);
            if (z > 0.3) d.Text(x + w / 2, y + h / 2 - 7 * z, Ink.Fit(m, v.Name, 12 * z, w - 16 * z), 12 * z, done ? Gold : TextSoft, TextAlign.Center);
            return;
        }

        Rgba hue = BranchOf(Content, Content.Nodes[gv.ContentIndex]);
        (Rgba fill, Rgba edge, double ew) = v.State switch
        {
            NodeState.Completed => (CompletedFill, Gold, 1.4),
            NodeState.CurrentTarget => (TargetFill, Cyan, 2.6),
            NodeState.Available => (AvailableFill, AvailableEdge, 1.4),
            NodeState.Partial => (LockedFill, Amber, 1.4),
            _ => (LockedFill, LockedEdge, 1.0),
        };
        if (v.State == NodeState.CurrentTarget)
            d.Rect(new RectD(x - 5 * z, y - 5 * z, w + 10 * z, h + 10 * z), A(0x5FD3E6, 0.18), null, 0, 10 * z);
        if (PendingTarget == gv.ContentIndex)
            d.Rect(new RectD(x - 4 * z, y - 4 * z, w + 8 * z, h + 8 * z), null, Cyan, 1.2, 9 * z);
        d.Rect(r, fill, selected || focus ? Text : edge, selected ? 2.2 : ew, 6 * z);
        d.Rect(new RectD(x, y, 5 * z, h), v.State == NodeState.Locked ? A(Hex(hue), 0.45) : hue, null, 0, 2 * z);

        if (z < 0.28) return;   // level of detail: colour only when far out
        bool dim = v.State == NodeState.Locked;
        double pad = 12 * z;
        double nameSize = 15.5 * z;
        double iconW = 18 * z;
        d.Text(x + pad, y + 8 * z, Ink.Fit(m, v.Name, nameSize, w - pad - iconW - 8 * z, FontRole.Heading), nameSize,
            dim ? TextSoft : Text, TextAlign.Left, FontRole.Heading);

        // State icon, top right.
        double ix = x + w - 15 * z, iy = y + 15 * z;
        switch (v.State)
        {
            case NodeState.Completed:
                d.Circle(ix, iy, 7 * z, Gold);
                d.Line(ix - 3.4 * z, iy, ix - 0.8 * z, iy + 2.8 * z, CompletedFill, 1.8 * z);
                d.Line(ix - 0.8 * z, iy + 2.8 * z, ix + 3.8 * z, iy - 2.8 * z, CompletedFill, 1.8 * z);
                break;
            case NodeState.CurrentTarget:
                d.Circle(ix, iy, 7 * z, null, Cyan, 1.6 * z);
                d.Arc(ix, iy, 7 * z, 0, 360 * Math.Max(0.02, v.Fraction), Cyan, 3 * z);
                break;
            case NodeState.Locked:
            case NodeState.Partial:
                d.Rect(new RectD(ix - 5 * z, iy - 1 * z, 10 * z, 8 * z), dim ? TextDim : Amber, null, 0, 1.5 * z);
                d.Arc(ix, iy - 1 * z, 3.4 * z, 270, 180, dim ? TextDim : Amber, 1.6 * z);
                break;
            default:
                d.Circle(ix, iy, 4.5 * z, AvailableEdge);
                break;
        }

        if (z < 0.45) return;
        double small = 12.5 * z;
        string meta = v.State == NodeState.Completed ? "Known" : Num(v.EffectiveCost) + " RP";
        d.Text(x + pad, y + 30 * z, meta, small, dim ? TextDim : TextSoft, TextAlign.Left, FontRole.Numeric);
        // Age badge (per card: a column can hold several Ages, so the Age belongs to the node).
        string age = ResearchTreeLayout.AgeNumeral(v.Age);
        double bw = (m.Width(age, 10 * z, FontRole.Caps) + 12 * z);
        var badge = new RectD(x + w - 10 * z - bw, y + 29 * z, bw, 15 * z);
        d.Rect(badge, A(0x000000, 0.3), dim ? Hairline : GoldDim, 1, 7 * z);
        d.Text(badge.X + bw / 2, badge.Y + 2 * z, age, 10 * z, dim ? TextDim : Gold, TextAlign.Center, FontRole.Caps);
        if (v.EffectiveCost < v.BaseCost && v.State != NodeState.Completed)
            d.Text(badge.X - 6 * z, y + 30 * z, "-" + Pct(1 - v.EffectiveCost / v.BaseCost), small, Green, TextAlign.Right, FontRole.Numeric);

        // Eureka pips.
        int total = v.Eurekas.Count;
        double px = x + pad;
        for (int e = 0; e < total && e < 6; e++)
        {
            bool fired = v.Eurekas[e].Fired;
            d.Circle(px + e * 11 * z + 3.5 * z, y + h - 13 * z, 3.5 * z, fired ? Gold : null, fired ? null : (dim ? TextDim : GoldDim), 1.1 * z);
        }

        if (v.Progress > 0 && v.State != NodeState.Completed)
        {
            var bar = new RectD(x + pad + (total > 0 ? Math.Min(6, total) * 11 * z + 6 * z : 0), y + h - 16 * z, 0, 6 * z);
            bar = bar with { W = x + w - 10 * z - bar.X };
            d.Bar(bar, v.Fraction, v.State == NodeState.CurrentTarget ? Cyan : Amber, A(0x000000, 0.0), A(0x000000, 0.45));
        }
        else if (v.Lock.HasFlag(LockReason.ResearchStage) && !v.Lock.HasFlag(LockReason.MissingPrerequisites))
            d.Text(x + w - 10 * z, y + h - 19 * z, "needs university", 10.5 * z, Amber, TextAlign.Right);
    }

    /// <summary>The minimap docks in the canvas's bottom-right corner; if the selected or
    /// hovered card would sit under it there, it moves to the bottom-left corner instead, so it
    /// never covers the selection.</summary>
    public RectD MinimapRect()
    {
        RectD c = Canvas;
        double mw = 240, mh = Math.Max(40, mw * Layout.Height / Layout.Width);
        if (mh > 150) { mh = 150; mw = mh * Layout.Width / Layout.Height; }
        var right = new RectD(c.Right - mw - 18, c.Bottom - mh - 18, mw, mh);
        int f = Hovered >= 0 ? Hovered : Selected;
        if (f < 0 || Content.Nodes[f].Tree != (ResearchTree)((int)Tab + 1)) return right;
        PlacedVertex p = Layout.Placed[Graph.VertexOf(f)];
        double x0 = Camera.ToScreenX(p.X, c.X), y0 = Camera.ToScreenY(p.Y, c.Y);
        double x1 = Camera.ToScreenX(p.Right, c.X), y1 = Camera.ToScreenY(p.Bottom, c.Y);
        RectD g = right.Inset(-8);
        bool overlaps = x1 > g.X && x0 < g.Right && y1 > g.Y && y0 < g.Bottom;
        return overlaps ? right with { X = c.X + 18 } : right;
    }

    private void PaintMinimap(DrawList d)
    {
        RectD r = MinimapRect();
        d.Rect(r.Inset(-8), A(0x070A0E, 0.94), GoldDim, 1, 6);
        double sx = r.W / Layout.Width, sy = r.H / Layout.Height;
        foreach (PlacedVertex p in Layout.Placed)
        {
            if (Graph.Vertices[p.Vertex].External || p.Hidden) continue;
            NodeState st = Snapshot!.Nodes[Graph.Vertices[p.Vertex].ContentIndex].State;
            Rgba col = st switch { NodeState.Completed => Gold, NodeState.CurrentTarget => Cyan, NodeState.Available => AvailableEdge, NodeState.Partial => Amber, _ => Rgba.Hex(0x323B48) };
            d.Rect(new RectD(r.X + p.X * sx, r.Y + p.Y * sy, Math.Max(1.5, p.W * sx), Math.Max(1.5, p.H * sy)), col);
        }
        (double x0, double y0, double x1, double y1) = VisibleWorld();
        double vx0 = Math.Clamp(r.X + x0 * sx, r.X, r.Right), vx1 = Math.Clamp(r.X + x1 * sx, r.X, r.Right);
        double vy0 = Math.Clamp(r.Y + y0 * sy, r.Y, r.Bottom), vy1 = Math.Clamp(r.Y + y1 * sy, r.Y, r.Bottom);
        d.Rect(new RectD(vx0, vy0, Math.Max(2, vx1 - vx0), Math.Max(2, vy1 - vy0)), A(0xD8B866, 0.08), Gold, 1.2);
        _hits.Add(new HitRegion(r, HitKind.Minimap, 0));
    }

    // ---- the detail panel

    private void PaintDetail(DrawList d, ITextMeasure m)
    {
        var panel = new RectD(_w - DetailW, LensBarH + TabBarH, DetailW, _h - LensBarH - TabBarH);
        d.Rect(panel, Chrome);
        d.Line(panel.X + 0.5, panel.Y, panel.X + 0.5, panel.Bottom, GoldDim, 1);
        d.PushClip(panel);
        int node = Hovered >= 0 ? Hovered : Selected;
        double x = panel.X + 22, w = DetailW - 44, y = panel.Y + 18;
        if (node < 0)
        {
            ResearchSnapshot s = Snapshot!;
            d.Text(x, y, Graph.Tree == ResearchTree.Technology ? "The Technology Tree" : "The Civics Tree", 24, Gold, TextAlign.Left, FontRole.Heading);
            y += 40;
            string intro = Graph.Tree == ResearchTree.Technology
                ? "The main trunk and five specialised subtrees. The subtrees open together once the research stage is reached."
                : "Forms of social organisation. Adopted civics also appear under the INSTITUTIONS lens.";
            foreach (string line in Ink.Wrap(m, intro, 14, w)) { d.Text(x, y, line, 14, TextSoft); y += 19; }
            y += 12;
            y = Stat(d, x, w, y, "Research stage", s.StageReached ? "reached" : "not yet reached", s.StageReached ? Green : Amber);
            y = Stat(d, x, w, y, "Research per turn", s.PointsPerTurn.ToString("0.0", CultureInfo.InvariantCulture) + " RP", Cyan);
            y = Stat(d, x, w, y, "Completed", Graph.Tree == ResearchTree.Technology ? s.CompletedTechnology + " of " + Content.TechnologyCount : s.CompletedCivics + " of " + Content.CivicsCount, Text);
            int avail = 0;
            for (int v = 0; v < Graph.OwnCount; v++) if (s.Nodes[Graph.Vertices[v].ContentIndex].Available) avail++;
            y = Stat(d, x, w, y, "Available now", avail.ToString(CultureInfo.InvariantCulture), AvailableEdge);
            y += 14;
            foreach (string line in Ink.Wrap(m, "Hover a node for its details. Click an available node to make it the research target; the order applies at End Turn. Drag to pan, wheel to zoom.", 13, w))
            { d.Text(x, y, line, 13, TextDim); y += 18; }
            d.PopClip();
            return;
        }

        ResearchNodeView n = Snapshot!.Nodes[node];
        Rgba hue = BranchOf(Content, Content.Nodes[node]);
        d.Rect(new RectD(panel.X + 1, panel.Y, DetailW - 1, 4), hue);
        d.Text(x, y, n.BranchName.ToUpperInvariant() + "  ·  " + ResearchTreeLayout.AgeLabel(n.Age).ToUpperInvariant(), 11.5, hue, TextAlign.Left, FontRole.Caps);
        y += 18;
        foreach (string line in Ink.Wrap(m, n.Name, 25, w, FontRole.Heading)) { d.Text(x, y, line, 25, Text, TextAlign.Left, FontRole.Heading); y += 30; }
        Rgba stc = n.State switch { NodeState.Completed => Gold, NodeState.CurrentTarget => Cyan, NodeState.Available => AvailableEdge, NodeState.Partial => Amber, _ => TextDim };
        d.Rect(new RectD(x, y + 2, m.Width(StateLabel(n.State).ToUpperInvariant(), 11, FontRole.Caps) + 18, 19), A(Hex(stc), 0.14), stc, 1, 9);
        d.Text(x + 9, y + 5, StateLabel(n.State).ToUpperInvariant(), 11, stc, TextAlign.Left, FontRole.Caps);
        if (PendingTarget == node) d.Text(x + m.Width(StateLabel(n.State).ToUpperInvariant(), 11, FontRole.Caps) + 28, y + 4, "target ordered - applies at End Turn", 12, Cyan);
        y += 30;

        // Cost and progress.
        y = Heading(d, x, w, y, "COST");
        y = Stat(d, x, w, y, "Base cost", Num(n.BaseCost) + " RP", TextSoft);
        foreach ((string uni, double f) in n.CostTerms)
            y = Stat(d, x, w, y, "  " + ResearchTreeLayout.Title(uni) + " university", "× " + f.ToString("0.00", CultureInfo.InvariantCulture), Green);
        y = Stat(d, x, w, y, "Effective cost" + (n.FloorBinds ? " (floor)" : ""), Num(n.EffectiveCost) + " RP", Text);
        if (n.State != NodeState.Completed)
        {
            d.Bar(new RectD(x, y + 4, w, 8), n.Fraction, n.State == NodeState.CurrentTarget ? Cyan : Amber, Hairline, A(0x000000, 0.5));
            y += 16;
            string turns = Snapshot.PointsPerTurn > 0 ? "  ·  ~" + Math.Ceiling(Math.Max(0, n.EffectiveCost - n.Progress) / Snapshot.PointsPerTurn).ToString("0", CultureInfo.InvariantCulture) + " turns at current rate" : "";
            d.Text(x, y, Num(n.Progress) + " / " + Num(n.EffectiveCost) + " RP" + turns, 12, TextSoft, TextAlign.Left, FontRole.Numeric);
            y += 22;
        }

        // Prerequisites and lock reasons.
        y = Heading(d, x, w, y, n.Prerequisites.Count == 0 ? "PREREQUISITES - none (root)" : "PREREQUISITES");
        bool anyOr = false;
        foreach (PrereqView p in n.Prerequisites) if (p.Kind == EdgeKind.Or) anyOr = true;
        if (anyOr && n.PrerequisiteExpression is string expr)
        {
            foreach (string line in Ink.Wrap(m, expr.Replace("_", " "), 11.5, w)) { d.Text(x, y, line, 11.5, TextDim, TextAlign.Left, FontRole.Numeric); y += 16; }
            y += 2;
        }
        foreach (PrereqView p in n.Prerequisites)
        {
            Rgba pc = p.Completed ? Gold : Red;
            d.Circle(x + 6, y + 9, 4.5, p.Completed ? pc : null, p.Completed ? null : pc, 1.4);
            d.Text(x + 18, y + 1, Ink.Fit(m, p.Name, 14, w - 70), 14, p.Completed ? Text : TextSoft);
            d.Text(x + w, y + 3, p.Kind == EdgeKind.And ? "required" : "one of", 11, TextDim, TextAlign.Right);
            y += 20;
        }
        if (n.Prerequisites.Count == 0) y += 4;
        if (n.Lock.HasFlag(LockReason.ResearchStage))
        {
            foreach (string line in Ink.Wrap(m, "Subtree closed: the research stage is not reached (" + Snapshot.StageExpression.Replace("_", " ") + ").", 12.5, w))
            { d.Text(x, y, line, 12.5, Amber); y += 17; }
        }
        if (n.Lock.HasFlag(LockReason.SubtreeNotExhausted))
        { d.Text(x, y, "Recursive: waits for every finite node of its subtree.", 12.5, Amber); y += 17; }
        y += 6;

        // Eureka.
        y = Heading(d, x, w, y, "EUREKA");
        if (n.Eurekas.Count == 0) { d.Text(x, y, "This node has no Eureka.", 13, TextDim); y += 26; }
        else
        {
            double credited = n.EurekaCredited + n.ForeignCredited;
            d.Text(x, y, "Acceleration credit " + Num(credited) + " of " + Num(n.EurekaCeiling) + " RP ceiling (" + Pct(n.EurekaCeiling / Math.Max(1e-9, n.BaseCost)) + " of base)", 12, TextSoft, TextAlign.Left, FontRole.Numeric);
            y += 18;
            var bar = new RectD(x, y, w, 8);
            d.Rect(bar, A(0x000000, 0.5), Hairline, 1, 2);
            double ce = Math.Max(1e-9, n.EurekaCeiling);
            d.Rect(new RectD(x, y, w * Math.Min(1, n.EurekaCredited / ce), 8), Gold);
            d.Rect(new RectD(x + w * Math.Min(1, n.EurekaCredited / ce), y, w * Math.Min(1, n.ForeignCredited / ce), 8), Rgba.Hex(0x9C7BD6));
            y += 14;
            d.Text(x, y, "Eureka " + Num(n.EurekaCredited) + " RP  ·  foreign exposure " + Num(n.ForeignCredited) + " RP" + (n.ExposureOffered > 0 ? " (offered " + Num(n.ExposureOffered) + ")" : ""), 11.5, TextDim, TextAlign.Left, FontRole.Numeric);
            y += 20;
            foreach (EurekaView e in n.Eurekas)
            {
                Rgba ec = e.Fired ? Gold : e.HoldsNow == true ? Green : TextDim;
                d.Circle(x + 6, y + 9, 4.5, e.Fired ? ec : null, e.Fired ? null : ec, 1.4);
                List<string> lines = Ink.Wrap(m, e.Text, 13, w - 70);
                for (int k = 0; k < lines.Count && k < 3; k++) { d.Text(x + 18, y + 1, lines[k], 13, e.Fired ? Text : TextSoft); y += 17; }
                d.Text(x + w, y - 17 * Math.Min(3, lines.Count) + 3, Pct(e.Weight) + " · " + Num(e.MaxCredit), 11, TextDim, TextAlign.Right, FontRole.Numeric);
                string status = e.Fired ? "fired - credited" : e.Condition is null ? "not evaluable yet (" + e.System + ")" : e.HoldsNow == true ? "holds now - fires when available" : "condition: " + e.Condition;
                foreach (string line in Ink.Wrap(m, status, 11, w - 18)) { d.Text(x + 18, y, line, 11, ec, TextAlign.Left); y += 14; }
                y += 6;
            }
        }

        // University relevance.
        y = Heading(d, x, w, y, "UNIVERSITY RELEVANCE");
        if (n.Universities.Count == 0) { d.Text(x, y, "None - main trunk and civics cost their base.", 13, TextDim); y += 26; }
        foreach (UniversityView u in n.Universities)
        {
            d.Text(x, y, u.Name, 14, Text);
            d.Text(x + w, y + 2, u.Role, 11.5, u.Role == "primary" ? Gold : TextDim, TextAlign.Right, FontRole.Caps);
            y += 20;
        }
        if (n.Unlocks.Count > 0)
        {
            y = Heading(d, x, w, y + 4, "OPENS THE WAY TO");
            foreach (string line in Ink.Wrap(m, string.Join(", ", n.Unlocks), 13, w)) { d.Text(x, y, line, 13, TextSoft); y += 17; }
        }
        y += 8;
        foreach (string line in Ink.Wrap(m, n.Description, 13, w)) { if (y > panel.Bottom - 80) break; d.Text(x, y, line, 13, TextDim); y += 17; }

        // Action.
        var btn = new RectD(x, panel.Bottom - 62, w, 44);
        bool can = n.Available && !n.IsTarget && PendingTarget != node;
        d.Rect(new RectD(panel.X + 1, btn.Y - 14, DetailW - 1, panel.Bottom - btn.Y + 14), Chrome);
        d.Rect(btn, can ? Rgba.Hex(0x1A3A44) : ChromeRaised, can ? Cyan : Hairline, 1.4, 6);
        string label = n.State switch
        {
            NodeState.Completed => "Known",
            NodeState.CurrentTarget => "Current research target",
            _ when PendingTarget == node => "Ordered - applies at End Turn",
            NodeState.Available => "Set as research target",
            _ => "Locked",
        };
        d.Text(btn.CenterX, btn.Y + 12, label.ToUpperInvariant(), 14, can ? Cyan : TextDim, TextAlign.Center, FontRole.Caps);
        if (can) _hits.Add(new HitRegion(btn, HitKind.SetTarget, node));
        d.PopClip();
    }

    private static double Heading(DrawList d, double x, double w, double y, string text)
    {
        d.Text(x, y, text, 11.5, GoldDim, TextAlign.Left, FontRole.Caps);
        d.Line(x, y + 18, x + w, y + 18, Hairline, 1);
        return y + 26;
    }

    private static double Stat(DrawList d, double x, double w, double y, string label, string value, Rgba valueColor)
    {
        d.Text(x, y, label, 14, TextSoft);
        d.Text(x + w, y + 1, value, 14, valueColor, TextAlign.Right, FontRole.Numeric);
        return y + 21;
    }

    // ---- the other lenses

    private void PaintLensPage(DrawList d, ITextMeasure m)
    {
        LensPage p = Page!;
        RectD c = Canvas;
        double x = c.X + 48, y = c.Y + 36, w = c.W - 96;
        d.Text(x, y, p.Title, 34, Gold, TextAlign.Left, FontRole.Heading);
        y += 46;
        d.Text(x, y, p.Purpose, 16, TextSoft);
        y += 30;
        Rgba sc = p.Status switch { LensStatus.Functional => Green, LensStatus.PartialData => Amber, _ => TextDim };
        var note = new RectD(x, y, w, 40);
        d.Rect(note, A(Hex(sc), 0.08), A(Hex(sc), 0.5), 1, 6);
        d.Text(x + 16, y + 11, Ink.Fit(m, p.StatusNote, 14, w - 32), 14, sc);
        y += 64;
        if (p.Sections.Count == 0)
        {
            d.Text(c.CenterX, c.CenterY, "NOT YET SIMULATED", 22, TextDim, TextAlign.Center, FontRole.Caps);
            return;
        }
        double colW = (w - 24 * (p.Sections.Count - 1)) / p.Sections.Count;
        for (int s = 0; s < p.Sections.Count; s++)
        {
            LensSection sec = p.Sections[s];
            double sx = x + s * (colW + 24), sy = y;
            d.Rect(new RectD(sx, sy, colW, c.Bottom - sy - 30), ChromeRaised, Hairline, 1, 6);
            d.Text(sx + 18, sy + 14, sec.Heading.ToUpperInvariant(), 13, Gold, TextAlign.Left, FontRole.Caps);
            if (sec.Note.Length > 0) d.Text(sx + colW - 18, sy + 15, sec.Note, 12, TextDim, TextAlign.Right);
            sy += 44;
            if (sec.Items.Count == 0) { d.Text(sx + 18, sy, "Nothing yet.", 14, TextDim); continue; }
            foreach (string item in sec.Items)
            {
                if (sy > c.Bottom - 60) { d.Text(sx + 18, sy, "...", 14, TextDim); break; }
                d.Circle(sx + 22, sy + 9, 2.5, GoldDim);
                d.Text(sx + 32, sy, Ink.Fit(m, item, 14, colW - 50), 14, Text);
                sy += 21;
            }
        }
    }
}
