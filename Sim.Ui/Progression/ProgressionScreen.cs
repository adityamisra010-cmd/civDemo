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

public enum HitKind { Lens, Tab, SetTarget, Close, Minimap }

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
    public const double LensBarH = 58, TabBarH = 50, DetailW = 404;

    public ResearchContent Content { get; }
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
    }

    public ResearchGraph Graph => Graphs[(int)Tab];
    public TreeLayout Layout => Layouts[(int)Tab];
    public ProgressionCamera Camera => Cameras[(int)Tab];
    public IReadOnlyList<HitRegion> Hits => _hits;
    public RectD Canvas => Lens == Lens.KnowledgeAndTechnology
        ? new RectD(0, LensBarH + TabBarH, Math.Max(100, _w - DetailW), Math.Max(100, _h - LensBarH - TabBarH))
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
        if (!c.Contains(x, y) || MinimapRect().Contains(x, y)) return -1;
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
        if (jump) Camera.Set(Camera.TargetZoom, Camera.TargetPanX, Camera.TargetPanY);
        _framed[(int)Tab] = true;
    }

    /// <summary>Fit the whole tree in the canvas.</summary>
    public void FitAll()
    {
        RectD c = Canvas;
        double z = Math.Min(1.0, Math.Min(c.W / Layout.Width, c.H / Layout.Height));
        Camera.Set(z, (Layout.Width - c.W / z) / 2.0, (Layout.Height - c.H / z) / 2.0);
        _framed[(int)Tab] = true;
    }

    private void FrameFirstTime()
    {
        if (_framed[(int)Tab] || Snapshot is null) return;
        _framed[(int)Tab] = true;
        // Open on the frontier: the current target, else the leftmost available node.
        int focus = Snapshot.TargetIndex is int t && Content.Nodes[t].Tree == (ResearchTree)((int)Tab + 1) ? t : -1;
        if (focus < 0)
        {
            int bestCol = int.MaxValue;
            for (int v = 0; v < Graph.OwnCount; v++)
            {
                int ci = Graph.Vertices[v].ContentIndex;
                if (Snapshot.Nodes[ci].Available && Layout.Placed[v].Column < bestCol) { bestCol = Layout.Placed[v].Column; focus = ci; }
            }
        }
        RectD c = Canvas;
        if (focus < 0) { Camera.Set(0.8, 0, 0); return; }
        PlacedVertex p = Layout.Placed[Graph.VertexOf(focus)];
        double z = 0.8;
        Camera.Set(z, Math.Max(-40, p.CenterX - c.W * 0.35 / z), Math.Max(-40, p.CenterY - c.H * 0.5 / z));
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

        d.PushClip(c);
        // Age bands (alternate shading) and lane bands.
        for (int a = 0; a < L.Ages.Count; a++)
        {
            AgeSpan s = L.Ages[a];
            if (a % 2 == 1) d.Rect(new RectD(SX(s.X0), c.Y, (s.X1 - s.X0) * z, c.H), FieldBand);
        }
        foreach (LaneBox lane in L.Lanes)
        {
            Rgba hue = Branch(lane.Id);
            double y = SY(lane.Y), h = lane.Height * z;
            if (y > c.Bottom || y + h < c.Y) continue;
            d.Rect(new RectD(c.X, y, c.W, h), A(Hex(hue), 0.035));
            d.Line(c.X, y, c.Right, y, A(Hex(hue), 0.35), 1);
        }

        // Highlight set: the focus node's prerequisites and dependents.
        int focus = Hovered >= 0 ? Hovered : Selected;
        int focusV = focus >= 0 ? Graph.VertexOf(focus) : -1;

        // Edges (culled by their bounding box).
        ResearchSnapshot s0 = Snapshot!;
        for (int pass = 0; pass < 2; pass++)
            foreach (GraphEdge e in Graph.Edges)
            {
                bool hot = focusV >= 0 && (e.From == focusV || e.To == focusV);
                if ((pass == 1) != hot) continue;
                PlacedVertex a = L.Placed[e.From], b = L.Placed[e.To];
                double ax = a.Right, ay = a.CenterY, bx = b.X, by = b.CenterY;
                if (Math.Max(ax, bx) < vx0 || Math.Min(ax, bx) > vx1 || Math.Max(ay, by) < vy0 || Math.Min(ay, by) > vy1) continue;
                bool done = s0.Nodes[Graph.Vertices[e.From].ContentIndex].State == NodeState.Completed;
                Rgba col = hot ? (done ? Gold : Cyan) : done ? A(0xB89A50, 0.55) : A(0x5A6878, 0.45);
                double w = (hot ? 2.4 : 1.4) * Math.Max(0.6, z);
                double dx = Math.Max(30, (bx - ax) * 0.5);
                (double, double)? dash = e.Kind == EdgeKind.Or ? (6.0 * Math.Max(0.6, z), 4.0 * Math.Max(0.6, z)) : null;
                d.Bezier((SX(ax), SY(ay)), (SX(ax + dx), SY(ay)), (SX(bx - dx), SY(by)), (SX(bx), SY(by)), col, w, dash);
                if (z > 0.35) d.Circle(SX(bx), SY(by), 2.6 * z, col);
            }

        // Cards.
        foreach (int v in VisibleVertices()) PaintCard(d, m, v, SX(L.Placed[v].X), SY(L.Placed[v].Y), z, v == focusV);

        // Sticky Age header and lane labels.
        d.Rect(new RectD(c.X, c.Y, c.W, 26), A(0x0A0E13, 0.92));
        foreach (AgeSpan s in L.Ages)
        {
            double x0 = Math.Max(SX(s.X0), c.X), x1 = Math.Min(SX(s.X1), c.Right);
            if (x1 <= x0) continue;
            d.Line(SX(s.X0), c.Y + 4, SX(s.X0), c.Y + 22, GoldDim, 1);
            if (x1 - x0 > 50) d.Text((x0 + x1) / 2, c.Y + 5, Ink.Fit(m, s.Label.ToUpperInvariant(), 12, x1 - x0 - 10, FontRole.Caps), 12, Gold, TextAlign.Center, FontRole.Caps);
        }
        d.Line(c.X, c.Y + 26, c.Right, c.Y + 26, Hairline, 1);
        foreach (LaneBox lane in L.Lanes)
        {
            double y = SY(lane.Y) + 6;
            if (y < c.Y + 28 || y > c.Bottom - 20) y = Math.Clamp(y, c.Y + 30, c.Bottom - 20);
            if (SY(lane.Y + lane.Height) < c.Y + 30 || SY(lane.Y) > c.Bottom) continue;
            Rgba hue = Branch(lane.Id);
            double tw = m.Width(lane.Name.ToUpperInvariant(), 11.5, FontRole.Caps) + 26;
            d.Rect(new RectD(c.X + 8, y, tw, 20), A(0x0A0E13, 0.85), A(Hex(hue), 0.6), 1, 10);
            d.Circle(c.X + 18, y + 10, 4, hue);
            d.Text(c.X + 26, y + 3, lane.Name.ToUpperInvariant(), 11.5, hue, TextAlign.Left, FontRole.Caps);
        }

        PaintMinimap(d);
        d.PopClip();
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
        double nameSize = 14.5 * z;
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
        double small = 11.5 * z;
        string meta = ResearchTreeLayout.AgeLabel(v.Age) + "  ·  " + (v.State == NodeState.Completed ? "known" : Num(v.EffectiveCost) + " RP");
        d.Text(x + pad, y + 29 * z, meta, small, dim ? TextDim : TextSoft, TextAlign.Left, FontRole.Numeric);
        if (v.EffectiveCost < v.BaseCost && v.State != NodeState.Completed)
            d.Text(x + w - 10 * z, y + 29 * z, "-" + Pct(1 - v.EffectiveCost / v.BaseCost), small, Green, TextAlign.Right, FontRole.Numeric);

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

    public RectD MinimapRect()
    {
        RectD c = Canvas;
        double mw = 220, mh = Math.Max(40, mw * Layout.Height / Layout.Width);
        if (mh > 140) { mh = 140; mw = mh * Layout.Width / Layout.Height; }
        return new RectD(c.Right - mw - 18, c.Bottom - mh - 18, mw, mh);
    }

    private void PaintMinimap(DrawList d)
    {
        RectD r = MinimapRect();
        d.Rect(r.Inset(-4), A(0x0A0E13, 0.9), Hairline, 1, 4);
        double sx = r.W / Layout.Width, sy = r.H / Layout.Height;
        foreach (PlacedVertex p in Layout.Placed)
        {
            if (Graph.Vertices[p.Vertex].External) continue;
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
