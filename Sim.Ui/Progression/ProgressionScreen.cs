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

public enum HitKind { Lens, Tab, SetTarget, Close, Minimap, LaneToggle, Frontier, Fit, MinimapToggle, Reset, AgeOpen }

public readonly record struct HitRegion(RectD Rect, HitKind Kind, int Arg);

/// <summary>What a click asks the host to do. The screen never mutates state: a research
/// choice is returned as the SetResearchTarget order the existing factory built.</summary>
public sealed record ProgressionCommand(bool Close, OrderRecord? Order, ResearchNodeId? Node)
{
    /// <summary>The Age header chip was clicked: the host opens the Age surface (ADR-031).</summary>
    public bool OpenAge { get; init; }

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
    /// <summary>The player's Age status (ADR-031) for the header chip, or null when not set / no Age content.</summary>
    public Sim.Ui.Ages.AgePanelModel? Age { get; set; }

    private IReadOnlyWorldState? _world;
    private readonly bool[] _framed = new bool[2];
    private List<HitRegion> _hits = [];
    private double _w = 1280, _h = 800;

    public ProgressionScreen(ResearchContent content, PolityId polity)
    {
        Content = content;
        Polity = polity;
        Graphs = [ResearchGraph.Build(content, ResearchTree.Technology), ResearchGraph.Build(content, ResearchTree.Civics)];
        var o = new TreeLayoutOptions(ViewportWidth: TreeViewportWidth);
        Layouts = [ResearchTreeLayout.Compute(Graphs[0], o), ResearchTreeLayout.Compute(Graphs[1], o)];
        _collapsed = [new bool[Layouts[0].Lanes.Count], new bool[Layouts[1].Lanes.Count]];
    }

    private readonly bool[][] _collapsed;

    /// <summary>Width of the vertical overview strip docked at the canvas's right edge.</summary>
    public const double StripW = 64;

    /// <summary>The width the tree must fit at zoom 1: the canvas minus the overview strip. The
    /// layout is recomputed whenever this changes, so there is never horizontal overflow.</summary>
    public double TreeViewportWidth => Math.Max(100, _w - DetailW) - (MinimapVisible ? StripW : 0);

    private void EnsureLayouts()
    {
        double vw = TreeViewportWidth;
        for (int t = 0; t < 2; t++)
            if (Layouts[t].Options.ViewportWidth != vw)
                Layouts[t] = ResearchTreeLayout.Compute(Graphs[t], Layouts[t].Options with { ViewportWidth = vw }, _collapsed[t]);
    }

    public bool IsLaneCollapsed(int lane) => _collapsed[(int)Tab][lane];

    /// <summary>Collapse or expand one lane of the current tree (UI state only). The collapsed
    /// lane shrinks to a strip and its width goes to the other lanes; the vertical scroll stays.</summary>
    public void ToggleLane(int lane)
    {
        int t = (int)Tab;
        _collapsed[t][lane] = !_collapsed[t][lane];
        Layouts[t] = ResearchTreeLayout.Compute(Graphs[t], Layouts[t].Options, _collapsed[t]);
        if (Selected >= 0 && Content.Nodes[Selected].Tree == (ResearchTree)(t + 1) && Layout.Placed[Graph.VertexOf(Selected)].Hidden) Selected = -1;
        ClampCamera();
        Camera.Set(Camera.TargetZoom, Camera.TargetPanX, Camera.TargetPanY);
    }

    private void ClampCamera()
    {
        RectD c = Canvas;
        Camera.Clamp(Layout.Width, Layout.Height, TreeViewportWidth, c.H);
    }

    /// <summary>Whether the drawing fits the tree viewport's width at the current zoom (always
    /// true at zoom ≤ 1): then horizontal motion is ignored — one scroll axis.</summary>
    public bool WidthFits => Layout.Width * Camera.TargetZoom <= TreeViewportWidth + 0.5;

    /// <summary>The DEFAULT-SCROLL node of the current tree (content index): the frontier of the
    /// least-developed branch (<see cref="ResearchTreeLayout.LeastDevelopedFrontier"/>), or -1.</summary>
    public int DefaultFrontierNode()
    {
        if (Snapshot is null) return -1;
        var done = new bool[Graph.Vertices.Count];
        for (int v = 0; v < done.Length; v++) done[v] = Snapshot.Nodes[Graph.Vertices[v].ContentIndex].State == NodeState.Completed;
        int f = ResearchTreeLayout.LeastDevelopedFrontier(Layout, done);
        return f < 0 ? -1 : Graph.Vertices[f].ContentIndex;
    }

    /// <summary>Fraction of the canvas height above the default-scroll frontier card.</summary>
    public const double DefaultFrontierAt = 0.28;

    /// <summary>Scroll (without selecting) so the least-developed branch's frontier is in view.</summary>
    public void ScrollToLeastDeveloped(bool jump = true)
    {
        int f = DefaultFrontierNode();
        RectD c = Canvas;
        double z = Camera.TargetZoom;
        double py = f < 0 ? 0 : Layout.Placed[Graph.VertexOf(f)].Y - c.H * DefaultFrontierAt / z;
        Camera.Set(z, Camera.TargetPanX, py);
        ClampCamera();
        if (jump) Camera.Set(Camera.TargetZoom, Camera.TargetPanX, Camera.TargetPanY);
    }

    /// <summary>The hovered (else selected) node's DIRECT prerequisites and dependents as content
    /// indices, ascending — exactly the cards and edges the highlight lights up.</summary>
    public (int[] Prerequisites, int[] Dependents) Highlight()
    {
        int focus = Hovered >= 0 ? Hovered : Selected;
        if (focus < 0 || Graph.VertexOf(focus) < 0) return ([], []);
        (int[] pre, int[] dep) = ResearchTreeLayout.Neighbours(Graph, Graph.VertexOf(focus));
        int[] Map(int[] vs) { var r = new int[vs.Length]; for (int i = 0; i < vs.Length; i++) r[i] = Graph.Vertices[vs[i]].ContentIndex; Array.Sort(r); return r; }
        return (Map(pre), Map(dep));
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

    public void Resize(double width, double height) { _w = width; _h = height; EnsureLayouts(); }

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

    /// <summary>Pixels the view scrolls per wheel notch.</summary>
    public const double WheelStepPx = 120;

    /// <summary>The wheel SCROLLS along the single axis (vertical); zoom is <see cref="WheelZoom"/>.</summary>
    public void Wheel(double x, double y, double notches)
    {
        RectD c = Canvas;
        if (Lens != Lens.KnowledgeAndTechnology || !c.Contains(x, y)) return;
        ScrollBy(0, -notches * WheelStepPx);
    }

    /// <summary>Zoom about the pointer (Ctrl + wheel in the game). Zoom stays available; above
    /// zoom 1 the drawing may become wider than the view, the only case horizontal panning works.</summary>
    public void WheelZoom(double x, double y, double notches)
    {
        RectD c = Canvas;
        if (Lens != Lens.KnowledgeAndTechnology || !c.Contains(x, y)) return;
        Camera.ZoomAt(x - c.X, y - c.Y, Math.Pow(1.18, notches));
        ClampCamera();
    }

    public void Drag(double dx, double dy)
    {
        if (Lens != Lens.KnowledgeAndTechnology) return;
        Camera.Drag(WidthFits ? 0 : dx, dy);
        ClampCamera();
    }

    public void ScrollBy(double dx, double dy)
    {
        if (Lens != Lens.KnowledgeAndTechnology) return;
        Camera.ScrollBy(WidthFits ? 0 : dx, dy);
        ClampCamera();
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
                case HitKind.AgeOpen: return new ProgressionCommand(false, null, null) { OpenAge = true };
                case HitKind.Lens: SetLens((Lens)h.Arg); return ProgressionCommand.None;
                case HitKind.Tab: Tab = (TreeTab)h.Arg; return ProgressionCommand.None;
                case HitKind.SetTarget: return Issue(h.Arg);
                case HitKind.LaneToggle: ToggleLane(h.Arg); return ProgressionCommand.None;
                case HitKind.Frontier: JumpToFrontier(); return ProgressionCommand.None;
                case HitKind.Fit: FitAll(); return ProgressionCommand.None;
                case HitKind.Reset: ResetView(); return ProgressionCommand.None;
                case HitKind.MinimapToggle: MinimapVisible = !MinimapVisible; EnsureLayouts(); ClampCamera(); return ProgressionCommand.None;
                case HitKind.Minimap:
                {
                    // The overview strip is one-dimensional: a click scrolls to that height.
                    RectD m = h.Rect;
                    RectD c = Canvas;
                    Camera.CenterOn(Layout.Width / 2.0, (y - m.Y) / m.H * Layout.Height, TreeViewportWidth, c.H);
                    ClampCamera();
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
        Camera.CenterOn(p.CenterX, p.CenterY, TreeViewportWidth, c.H);
        ClampCamera();
        if (jump) Camera.Set(Camera.TargetZoom, Camera.TargetPanX, Camera.TargetPanY);
        _framed[(int)Tab] = true;
    }

    /// <summary>Fit the whole tree in the canvas (an overview zoom; the width still fits).</summary>
    public void FitAll()
    {
        RectD c = Canvas;
        double tw = TreeViewportWidth;
        double z = Math.Max(ProgressionCamera.MinZoom, Math.Min(1.0, Math.Min(tw / Layout.Width, c.H / Layout.Height)));
        Camera.Set(z, (Layout.Width - tw / z) / 2.0, -8.0 / z);   // top-aligned
        ClampCamera();
        Camera.Set(Camera.TargetZoom, Camera.TargetPanX, Camera.TargetPanY);
        _framed[(int)Tab] = true;
    }

    /// <summary>Back to the default view: zoom 1, scrolled to the least-developed frontier.</summary>
    public void ResetView()
    {
        Camera.Set(1.0, 0, Camera.TargetPanY);
        ScrollToLeastDeveloped();
        _framed[(int)Tab] = true;
    }

    private void FrameFirstTime()
    {
        if (_framed[(int)Tab] || Snapshot is null) return;
        _framed[(int)Tab] = true;
        // Open at zoom 1 (the width fits) scrolled to the LEAST-DEVELOPED branch's frontier —
        // not the target, not the most advanced research: what lags is what the player sees first.
        Camera.Set(1.0, 0, 0);
        ScrollToLeastDeveloped();
    }

    // ------------------------------------------------------------------ paint

    public DrawList Paint(double width, double height, ITextMeasure m)
    {
        Resize(width, height);
        if (Snapshot is not null && Lens == Lens.KnowledgeAndTechnology) FrameFirstTime();
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
        bool ageChip = Age is { State: not Sim.Ui.Ages.AgePanelState.NoContent };
        var cap = new RectD(cx, y + 7, Math.Min(ageChip ? 360 : 420, _w - DetailW - cx - 20), TabBarH - 14);
        if (ageChip) PaintAgeChip(d, m, new RectD(cap.Right + 12, y + 7, _w - DetailW - 20 - cap.Right - 12, TabBarH - 14));
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
    }

    /// <summary>The Age header chip: current Age, progress toward the next, and its state; a click opens
    /// the Age surface (milestones, Advance Age). Read from <see cref="Age"/> (AgeQuery), never invented.</summary>
    private void PaintAgeChip(DrawList d, ITextMeasure m, RectD r)
    {
        if (r.W < 150 || Age is not { } a) return;
        bool eligible = a.State == Sim.Ui.Ages.AgePanelState.Eligible;
        bool pending = a.State == Sim.Ui.Ages.AgePanelState.Pending;
        d.Rect(r, eligible ? Rgba.Hex(0x2A2312) : ChromeRaised, eligible ? Gold : pending ? Cyan : GoldDim, eligible ? 1.8 : 1, 5);
        d.Text(r.X + 10, r.Y + 4, "AGE " + Sim.Ui.Ages.AgePanelModel.Numeral(a.CurrentAge), 9.5, Gold, TextAlign.Left, FontRole.Caps);
        d.Text(r.X + 10, r.Y + 16, Ink.Fit(m, a.CurrentAgeName, 13, r.W * 0.5, FontRole.Heading), 13, Text, TextAlign.Left, FontRole.Heading);
        string right = a.State switch
        {
            Sim.Ui.Ages.AgePanelState.FinalAge => "final Age",
            Sim.Ui.Ages.AgePanelState.Eligible => "ADVANCE AGE available",
            Sim.Ui.Ages.AgePanelState.Pending => "advancing next turn",
            _ => "next: core " + a.CoreMet + "/" + a.CoreTotal + " - supp. " + a.SupportingMet + "/" + a.SupportingRequired,
        };
        d.Text(r.Right - 10, r.Y + 10, Ink.Fit(m, right, 11.5, r.W * 0.48), 11.5, eligible ? Gold : pending ? Cyan : TextSoft, TextAlign.Right, eligible ? FontRole.Caps : FontRole.Body);
        _hits.Add(new HitRegion(r, HitKind.AgeOpen, 0));
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

    /// <summary>Cross-lane prerequisites of a vertex (another lane, or the other tree), in edge order.</summary>
    public List<int> CrossLanePrerequisites(int vertex)
    {
        var r = new List<int>();
        int lane = Layout.Placed[vertex].Lane;
        foreach (GraphEdge e in Graph.Edges)
            if (e.To == vertex && Layout.Placed[e.From].Lane != lane && !r.Contains(e.From)) r.Add(e.From);
        return r;
    }

    /// <summary>The chip text naming a card's cross-lane prerequisites, e.g.
    /// "needs Bronze working (Metallurgy)" or "needs 3: Engineering, Military"; "" when none.</summary>
    public string CrossLaneLabel(int vertex)
    {
        List<int> cross = CrossLanePrerequisites(vertex);
        if (cross.Count == 0) return "";
        string LaneName(int v) => Layout.Lanes[Layout.Placed[v].Lane].External
            ? (Graph.Tree == ResearchTree.Civics ? "Technology" : "Civics") : Layout.Lanes[Layout.Placed[v].Lane].Name;
        if (cross.Count == 1) return "needs " + Graph.Node(cross[0]).Name + " (" + LaneName(cross[0]) + ")";
        var names = new List<string>();
        foreach (int v in cross) if (!names.Contains(LaneName(v))) names.Add(LaneName(v));
        return "needs " + cross.Count.ToString(CultureInfo.InvariantCulture) + ": " + string.Join(", ", names);
    }

    private void PaintTree(DrawList d, ITextMeasure m)
    {
        RectD c = Canvas;
        TreeLayout L = Layout;
        ProgressionCamera cam = Camera;
        double z = cam.Zoom;
        double SX(double wx) => cam.ToScreenX(wx, c.X);
        double SY(double wy) => cam.ToScreenY(wy, c.Y);
        TreeLayoutOptions o = L.Options;
        double tx0 = SX(0), tx1 = SX(L.Width);

        d.PushClip(c);
        // Horizontal tier bands: alternate shading and a label strip with the full Age names.
        foreach (TierBand t in L.Tiers)
        {
            double y0 = SY(t.Y0), y1 = SY(t.Y1);
            if (y1 < c.Y || y0 > c.Bottom) continue;
            if (t.Tier % 2 == 1) d.Rect(new RectD(tx0, y0, tx1 - tx0, y1 - y0), A(0x1A2330, 0.35));
            d.Line(tx0, y0 + 0.5, tx1, y0 + 0.5, A(0x8C7742, 0.45), 1);
            if (z >= 0.45)
            {
                string label = "TIER " + (t.Tier + 1).ToString(CultureInfo.InvariantCulture);
                double lx = SX(o.Margin + o.Spine);
                d.Text(lx, y0 + 5 * z, label, 11.5 * z, Gold, TextAlign.Left, FontRole.Caps);
                string ages = ResearchTreeLayout.AgeRangeLabel((t.Lo, t.Hi));
                if (ages.Length > 0) d.Text(lx + m.Width(label, 11.5 * z, FontRole.Caps) + 12 * z, y0 + 5 * z, ages, 11.5 * z, TextSoft, TextAlign.Left);
            }
        }
        // Lane segments: each lane's share of a tier, tinted by its hue with a coloured cap and
        // its name, so a lane reads as one colour from tier to tier while lanes keep their order.
        foreach (LaneSegment sg in L.Segments)
        {
            TierBand t = L.Tiers[sg.Tier];
            double y0 = SY(t.Y0 + o.TierGap - 14), y1 = SY(t.Y1 - o.RowGap / 2);
            if (y1 < c.Y || y0 > c.Bottom) continue;
            LaneBox lane = L.Lanes[sg.Lane];
            Rgba hue = Branch(lane.Id);
            double x0 = SX(sg.X) - 4 * z, w = (sg.Width - o.Gutter) * z + 8 * z;
            d.Rect(new RectD(x0, y0, w, y1 - y0), A(Hex(hue), 0.05), A(Hex(hue), 0.16), 1, 5 * z);
            d.Rect(new RectD(x0, y0, w, 2 * z), A(Hex(hue), 0.75));
            if (z >= 0.45)
                d.Text(x0 + 6 * z, SY(t.Y0 + o.TierGap - 25), Ink.Fit(m, lane.Name.ToUpperInvariant(), 10 * z, w - 10 * z, FontRole.Caps), 10 * z, hue, TextAlign.Left, FontRole.Caps);
        }

        int focus = Hovered >= 0 ? Hovered : Selected;
        int focusV = focus >= 0 ? Graph.VertexOf(focus) : -1;
        if (focusV >= 0 && L.Placed[focusV].Hidden) focusV = -1;
        var related = new int[Graph.Vertices.Count];   // 0 none, 1 prerequisite, 2 dependent
        if (focusV >= 0)
        {
            (int[] pre, int[] dep) = ResearchTreeLayout.Neighbours(Graph, focusV);
            foreach (int v in dep) related[v] = 2;
            foreach (int v in pre) related[v] = 1;
        }

        // Cards first; with a focus, everything unrelated is dimmed.
        foreach (int v in VisibleVertices())
        {
            PlacedVertex p = L.Placed[v];
            if (p.Hidden) continue;
            double x = SX(p.X), y = SY(p.Y);
            PaintCard(d, m, v, x, y, z, v == focusV);
            if (focusV < 0) continue;
            var r = new RectD(x - 2, y - 2, p.W * z + 4, p.H * z + 4);
            if (v == focusV) continue;
            if (related[v] == 0) d.Rect(r, A(0x0E1319, 0.66), null, 0, 7 * z);
            else d.Rect(r, null, related[v] == 1 ? PrereqHi : DependentHi, 2.0, 7 * z);
        }

        // Edges: ONLY the focused node's, routed orthogonally through row gaps and lane gutters,
        // prerequisites in one colour, dependents in another. No permanent spaghetti.
        int routed = 0;
        if (focusV >= 0)
            foreach (GraphEdge e in Graph.Edges)
            {
                if (e.From != focusV && e.To != focusV) continue;
                (double X, double Y)[] pts = ResearchTreeLayout.Route(L, e, routed++);
                if (pts.Length == 0) continue;
                Rgba col = e.To == focusV ? PrereqHi : DependentHi;
                double w = 2.2 * Math.Max(0.6, z);
                (double, double)? dash = e.Kind == EdgeKind.Or ? (6.0 * Math.Max(0.6, z), 4.0 * Math.Max(0.6, z)) : null;
                for (int k = 0; k + 1 < pts.Length; k++)
                    d.Line(SX(pts[k].X), SY(pts[k].Y), SX(pts[k + 1].X), SY(pts[k + 1].Y), col, w, dash);
                double ex = SX(pts[^1].X), ey = SY(pts[^1].Y);
                double ah = 6 * Math.Max(0.6, z);
                d.Polygon([(ex - ah, ey - ah * 1.3), (ex + ah, ey - ah * 1.3), (ex, ey)], col);
            }

        if (MinimapVisible) PaintMinimap(d);
        d.PopClip();

        // Header rows sit above the camera canvas (never over a card): the sticky lane header
        // (lane names and collapse chips — fixed, since the tree never scrolls sideways) and the
        // control row.
        double hy = c.Y - HeaderH;
        d.PushClip(new RectD(c.X, hy, c.W, HeaderH));
        d.Rect(new RectD(c.X, hy, c.W, ColumnHeaderH), Rgba.Hex(0x0C1117));
        // Lanes keep a fixed order; their chips share the header width in that order.
        double chipW = (c.W - 24 - 6 * (L.Lanes.Count - 1)) / L.Lanes.Count;
        foreach (LaneBox lane in L.Lanes)
            LaneChip(d, m, lane, c.X + 12 + lane.Index * (chipW + 6), hy + (ColumnHeaderH - 24) / 2, chipW);
        d.Line(c.X, hy + ColumnHeaderH, c.Right, hy + ColumnHeaderH, GoldDim, 1);
        PaintControlRow(d, m);
        d.PopClip();
    }

    private void LaneChip(DrawList d, ITextMeasure m, LaneBox lane, double x, double y, double w)
    {
        const double chipH = 24;
        Rgba hue = Branch(lane.Id);
        var chip = new RectD(x, y, w, chipH);
        d.Rect(chip, A(0x0A0E13, 0.94), A(Hex(hue), 0.8), 1, 6);
        // Disclosure triangle: right when collapsed, down when expanded.
        double tx = chip.X + 11, ty = chip.Y + chipH / 2;
        if (lane.Collapsed) d.Polygon([(tx - 3, ty - 5), (tx + 4, ty), (tx - 3, ty + 5)], hue);
        else d.Polygon([(tx - 5, ty - 3), (tx + 5, ty - 3), (tx, ty + 4)], hue);
        if (w > 60)
        {
            string count = lane.NodeCount.ToString(CultureInfo.InvariantCulture) + (lane.Collapsed ? " hidden" : "");
            double cw = m.Width(count, 11, FontRole.Numeric);
            d.Text(chip.X + 22, chip.Y + 5, Ink.Fit(m, lane.Name.ToUpperInvariant(), 12, w - cw - 36, FontRole.Caps), 12, hue, TextAlign.Left, FontRole.Caps);
            d.Text(chip.Right - 8, chip.Y + 6, count, 11, TextSoft, TextAlign.Right, FontRole.Numeric);
        }
        _hits.Add(new HitRegion(chip, HitKind.LaneToggle, lane.Index));
    }

    /// <summary>The opaque control row: on the left, the tier (with its full Age names) at the top
    /// of the view; then the highlight legend; on the right, jump to the target, reset to the
    /// default view, fit the tree, show/hide the overview strip.</summary>
    private void PaintControlRow(DrawList d, ITextMeasure m)
    {
        RectD c = Canvas;
        var row = new RectD(c.X, c.Y - ControlRowH, c.W, ControlRowH);
        d.Rect(row, Rgba.Hex(0x0F151C));
        d.Line(c.X, row.Bottom, c.Right, row.Bottom, Hairline, 1);
        bool hasTarget = Snapshot!.TargetIndex is int t && Content.Nodes[t].Tree == (ResearchTree)((int)Tab + 1);
        (string Label, HitKind Kind, bool On)[] buttons =
        [
            (hasTarget ? "JUMP TO TARGET" : "JUMP TO FRONTIER", HitKind.Frontier, false),
            ("LAGGING BRANCH", HitKind.Reset, false),
            ("FIT", HitKind.Fit, false),
            (MinimapVisible ? "HIDE MAP" : "SHOW MAP", HitKind.MinimapToggle, MinimapVisible),
        ];
        double x = c.Right - 12, h = 26, y = row.Y + (ControlRowH - h) / 2;
        for (int i = buttons.Length - 1; i >= 0; i--)
        {
            double w = m.Width(buttons[i].Label, 11, FontRole.Caps) + 20;
            x -= w;
            var r = new RectD(x, y, w, h);
            bool primary = buttons[i].Kind == HitKind.Frontier;
            d.Rect(r, primary ? A(0x5FD3E6, 0.16) : A(0x0A0E13, 0.92), primary ? Cyan : buttons[i].On ? GoldDim : Hairline, 1, 5);
            d.Text(r.X + w / 2, r.Y + 7, buttons[i].Label, 11, primary ? Cyan : Text, TextAlign.Center, FontRole.Caps);
            _hits.Add(new HitRegion(r, buttons[i].Kind, 0));
            x -= 6;
        }
        // Legend for the highlight (just left of the buttons).
        (string Label, Rgba Col, bool Dash)[] keys = [("requires", PrereqHi, false), ("leads to", DependentHi, false), ("one of", TextSoft, true)];
        double lx = x - 8;
        for (int i = keys.Length - 1; i >= 0; i--)
        {
            double tw = m.Width(keys[i].Label, 11, FontRole.Body);
            lx -= tw + 30;
            d.Line(lx, row.Y + 19, lx + 20, row.Y + 19, keys[i].Col, 2, keys[i].Dash ? (4, 3) : null);
            d.Text(lx + 24, row.Y + 12, keys[i].Label, 11, TextSoft);
        }
        // Current tier at the top of the view, with its full Age names.
        TreeLayout L = Layout;
        int tier = L.TierAt(Camera.ToWorldY(c.Y + 1, c.Y));
        TierBand band = L.Tiers[tier];
        string head = "TIER " + (tier + 1).ToString(CultureInfo.InvariantCulture) + " of " + L.Columns.ToString(CultureInfo.InvariantCulture);
        d.Text(c.X + 12, row.Y + 11, head, 11.5, Gold, TextAlign.Left, FontRole.Caps);
        double hx = c.X + 22 + m.Width(head, 11.5, FontRole.Caps);
        d.Text(hx, row.Y + 11, Ink.Fit(m, ResearchTreeLayout.AgeRangeLabel((band.Lo, band.Hi)), 11.5, Math.Max(0, lx - hx - 16)), 11.5, TextSoft);
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
        double pad = 11 * z;
        double nameSize = 14.5 * z;
        double iconW = 18 * z;
        d.Text(x + pad, y + 7 * z, Ink.Fit(m, v.Name, nameSize, w - pad - iconW - 8 * z, FontRole.Heading), nameSize,
            dim ? TextSoft : Text, TextAlign.Left, FontRole.Heading);

        // State icon, top right.
        double ix = x + w - 14 * z, iy = y + 14 * z;
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
        double small = 12 * z;
        // Row 2: cost (or Known) and any discount; Eureka pips on the right.
        string meta = v.State == NodeState.Completed ? "Known" : Num(v.EffectiveCost) + " RP";
        d.Text(x + pad, y + 27 * z, meta, small, dim ? TextDim : TextSoft, TextAlign.Left, FontRole.Numeric);
        if (v.EffectiveCost < v.BaseCost && v.State != NodeState.Completed)
            d.Text(x + pad + m.Width(meta, small, FontRole.Numeric) + 6 * z, y + 27 * z, "-" + Pct(1 - v.EffectiveCost / v.BaseCost), small, Green, TextAlign.Left, FontRole.Numeric);
        int total = Math.Min(6, v.Eurekas.Count);
        for (int e = 0; e < total; e++)
        {
            bool fired = v.Eurekas[e].Fired;
            double px = x + w - 10 * z - (total - e) * 10 * z + 3.5 * z;
            d.Circle(px, y + 34 * z, 3.3 * z, fired ? Gold : null, fired ? null : (dim ? TextDim : GoldDim), 1.1 * z);
        }

        // Row 3: the Age — numeral AND full name, always.
        d.Text(x + pad, y + 44 * z, Ink.Fit(m, ResearchTreeLayout.AgeShort(v.Age), 10.5 * z, w - pad - 8 * z), 10.5 * z,
            dim ? TextDim : GoldDim, TextAlign.Left, FontRole.Body);

        // Row 4: dependency counts (in / out) and the cross-lane prerequisite port.
        (int[] pre, int[] dep) = ResearchTreeLayout.Neighbours(Graph, vertex);
        double fy = y + 62 * z, fx = x + pad;
        Rgba cc = dim ? TextDim : TextSoft;
        d.Polygon([(fx, fy + 9 * z), (fx + 8 * z, fy + 9 * z), (fx + 4 * z, fy + 2 * z)], A(Hex(PrereqHi), dim ? 0.5 : 0.9));
        string ins = pre.Length.ToString(CultureInfo.InvariantCulture);
        d.Text(fx + 11 * z, fy, ins, 11 * z, cc, TextAlign.Left, FontRole.Numeric);
        fx += 11 * z + m.Width(ins, 11 * z, FontRole.Numeric) + 8 * z;
        d.Polygon([(fx, fy + 2 * z), (fx + 8 * z, fy + 2 * z), (fx + 4 * z, fy + 9 * z)], A(Hex(DependentHi), dim ? 0.5 : 0.9));
        string outs = dep.Length.ToString(CultureInfo.InvariantCulture);
        d.Text(fx + 11 * z, fy, outs, 11 * z, cc, TextAlign.Left, FontRole.Numeric);
        fx += 11 * z + m.Width(outs, 11 * z, FontRole.Numeric) + 10 * z;
        string cross = CrossLaneLabel(vertex);
        string tail = cross.Length > 0 ? cross
            : v.Lock.HasFlag(LockReason.ResearchStage) && !v.Lock.HasFlag(LockReason.MissingPrerequisites) ? "needs university" : "";
        if (tail.Length > 0)
        {
            double cw = x + w - 8 * z - fx;
            if (cw > 30 * z)
            {
                string fit = Ink.Fit(m, tail, 10.5 * z, cw - 10 * z);
                double tw2 = m.Width(fit, 10.5 * z, FontRole.Body) + 10 * z;
                var chip = new RectD(x + w - 8 * z - tw2, fy - 2 * z, tw2, 15 * z);
                Rgba chipCol = cross.Length > 0 ? PrereqHi : Amber;
                d.Rect(chip, A(0x000000, 0.3), A(Hex(chipCol), dim ? 0.35 : 0.7), 1, 7 * z);
                d.Text(chip.X + 5 * z, fy, fit, 10.5 * z, cross.Length > 0 ? (dim ? TextDim : TextSoft) : Amber, TextAlign.Left);
            }
        }

        if (v.Progress > 0 && v.State != NodeState.Completed)
        {
            var bar = new RectD(x + 6 * z, y + h - 5 * z, w - 12 * z, 3 * z);
            d.Bar(bar, v.Fraction, v.State == NodeState.CurrentTarget ? Cyan : Amber, A(0x000000, 0.0), A(0x000000, 0.45));
        }
    }

    /// <summary>The overview strip: a narrow vertical bar docked at the canvas's right edge,
    /// OUTSIDE the tree viewport (the layout is fitted to the width left of it), so it never
    /// covers a card. It maps the single scroll axis: a click scrolls to that height.</summary>
    public RectD MinimapRect()
    {
        RectD c = Canvas;
        return new RectD(c.X + TreeViewportWidth + 10, c.Y + 10, StripW - 18, c.H - 20);
    }

    private void PaintMinimap(DrawList d)
    {
        RectD r = MinimapRect();
        d.Rect(new RectD(r.X - 6, c0Y(), StripW - 6, Canvas.H), Rgba.Hex(0x0A0E13));
        d.Rect(r.Inset(-3), A(0x070A0E, 0.94), GoldDim, 1, 4);
        double sx = r.W / Math.Max(1, Layout.Width), sy = r.H / Math.Max(1, Layout.Height);
        foreach (PlacedVertex p in Layout.Placed)
        {
            if (Graph.Vertices[p.Vertex].External || p.Hidden) continue;
            NodeState st = Snapshot!.Nodes[Graph.Vertices[p.Vertex].ContentIndex].State;
            Rgba col = st switch { NodeState.Completed => Gold, NodeState.CurrentTarget => Cyan, NodeState.Available => AvailableEdge, NodeState.Partial => Amber, _ => Rgba.Hex(0x323B48) };
            d.Rect(new RectD(r.X + p.X * sx, r.Y + p.Y * sy, Math.Max(1.2, p.W * sx), Math.Max(1.2, p.H * sy)), col);
        }
        (double _, double y0, double _, double y1) = VisibleWorld();
        double vy0 = Math.Clamp(r.Y + y0 * sy, r.Y, r.Bottom), vy1 = Math.Clamp(r.Y + y1 * sy, r.Y, r.Bottom);
        d.Rect(new RectD(r.X - 2, vy0, r.W + 4, Math.Max(2, vy1 - vy0)), A(0xD8B866, 0.10), Gold, 1.2);
        _hits.Add(new HitRegion(r, HitKind.Minimap, 0));
    }

    private double c0Y() => Canvas.Y;

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
            // A prerequisite from another lane (or the other tree) names where it lives.
            int pv = Graph.VertexOf(p.ContentIndex), nv = Graph.VertexOf(node);
            string where = "";
            if (pv >= 0 && nv >= 0 && Layout.Placed[pv].Lane != Layout.Placed[nv].Lane)
                where = " (" + (Graph.Vertices[pv].External ? (Graph.Tree == ResearchTree.Civics ? "Technology" : "Civics") : Layout.Lanes[Layout.Placed[pv].Lane].Name) + ")";
            d.Text(x + 18, y + 1, Ink.Fit(m, p.Name + where, 14, w - 70), 14, p.Completed ? Text : TextSoft);
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
