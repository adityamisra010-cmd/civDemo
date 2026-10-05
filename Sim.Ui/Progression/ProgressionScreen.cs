using System.Globalization;
using Sim.Core.Kernel;
using Sim.Core.State;
using Sim.Core.Systems.Research;
using Sim.Ui.Render;
using Sim.Ui.Theme;
using Sim.Ui.ViewModel;
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
    /// <summary>The era the screen is painted in (ADR-033 D8): handed in by the host, derived from the
    /// player's authoritative Age (<see cref="UiEras.Of"/>). The founding era until it is set.</summary>
    public EraTheme Theme { get; set; } = EraThemes.For(UiEra.Prehistoric);

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

    /// <summary>
    /// THE ERA'S HAND (ADR-033 D8). Every colour, frame, mark, typeface and density decision below
    /// comes from <see cref="Theme"/>; every rect and hit region comes from the layout and the
    /// era-INVARIANT measure, so the screen keeps the same regions, cards, controls and navigation in
    /// every era (continuity) while its material, edges, ornament, type and density evolve.
    /// </summary>
    public DrawList Paint(double width, double height, ITextMeasure m)
    {
        Resize(width, height);
        if (Snapshot is not null && Lens == Lens.KnowledgeAndTechnology) FrameFirstTime();
        var d = new DrawList();
        _hits = [];
        PanelFrame.Field(d, new RectD(0, 0, width, height), Theme, 1);
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
    private static Rgba A(Rgba c, double alpha) => ThemeColor.Alpha(c, alpha);
    private static Rgba Mix(Rgba a, Rgba b, double t) => ThemeColor.Mix(a, b, t);

    /// <summary>A selected tab / lens / toggle: the raised material warmed by the era's accent.</summary>
    private Rgba SelectedFill => Mix(Theme.Material.PanelRaised, Theme.Material.Accent, 0.14);

    private void PaintLensBar(DrawList d, ITextMeasure m)
    {
        EraTheme t = Theme;
        PanelFrame.Paint(d, new RectD(0, 0, _w, LensBarH), t, 101, FrameKind.Bar);
        d.Write(t, 20, 10, "PROGRESSION", 11, t.Ink.OnChromeAccent, TextAlign.Left, FontRole.Caps);
        d.Write(t, 20, 26, "Turn " + Sim.Ui.ViewModel.PlayerTurn.Current(Snapshot!.Turn).ToString(CultureInfo.InvariantCulture), 17, t.Ink.OnChrome, TextAlign.Left, FontRole.Heading);

        // Slot geometry from the era-invariant measure (the same slots in every era).
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
            if (on)
            {
                PanelFrame.Paint(d, r, t, 110 + i, FrameKind.Button, SelectedFill, t.Material.Accent, 1.1);
                d.Rect(new RectD(r.X + 10, r.Bottom - 4, r.W - 20, 2.5), t.Material.Accent);
            }
            Rgba col = on ? t.Ink.Text : status == LensStatus.NotYetSimulated ? t.Ink.OnChromeSoft : t.Ink.OnChrome;
            double ls = ThemeText.FitSize(m, t, Lenses.Label(l), size, r.W - 8, FontRole.Caps);
            d.Write(t, r.CenterX, r.Y + 6 + (size - ls) / 2, ThemeText.Fit(m, t, Lenses.Label(l), ls, r.W - 8, FontRole.Caps), ls, col, TextAlign.Center, FontRole.Caps);
            string sub = status switch { LensStatus.Functional => "simulated", LensStatus.PartialData => "partial", _ => "not yet simulated" };
            d.Write(t, r.CenterX, r.Y + 24, sub, 10.5, on ? t.Ink.TextSoft : t.Ink.OnChromeSoft, TextAlign.Center);
            _hits.Add(new HitRegion(r, HitKind.Lens, (int)l));
        }

        d.Write(t, _w - 64, 12, Snapshot.PointsPerTurn.ToString("0.0", CultureInfo.InvariantCulture), 18, t.Ink.OnChromeAccent, TextAlign.Right, FontRole.Numeric);
        d.Write(t, _w - 64, 34, "research / turn", 10.5, t.Ink.OnChromeSoft, TextAlign.Right);
        var close = new RectD(_w - 52, 12, 36, 34);
        PanelFrame.Paint(d, close, t, 120, FrameKind.Button);
        EraMarks.Close(d, t, close, t.Ink.Text, 121);
        _hits.Add(new HitRegion(close, HitKind.Close, 0));
    }

    private void PaintTabBar(DrawList d, ITextMeasure m)
    {
        EraTheme t = Theme;
        double y = LensBarH;
        PanelFrame.Paint(d, new RectD(0, y, _w, TabBarH), t, 102, FrameKind.Bar);
        ResearchSnapshot s = Snapshot!;
        string[] names = ["TECHNOLOGY", "CIVICS"];
        string[] counts = [s.CompletedTechnology + " / " + Content.TechnologyCount, s.CompletedCivics + " / " + Content.CivicsCount];
        for (int i = 0; i < 2; i++)
        {
            var r = new RectD(16 + i * 196, y + 7, 186, TabBarH - 14);
            bool on = (int)Tab == i;
            PanelFrame.Paint(d, r, t, 130 + i, FrameKind.Button, on ? SelectedFill : t.Material.Chrome, on ? t.Material.Accent : t.Ink.OnChromeSoft, on ? 1.1 : 0.8);
            d.Write(t, r.X + 14, r.Y + 9, names[i], 14, on ? t.Ink.Text : t.Ink.OnChrome, TextAlign.Left, FontRole.Caps);
            d.Write(t, r.Right - 12, r.Y + 10, counts[i], 13, on ? t.Ink.Text : t.Ink.OnChromeSoft, TextAlign.Right, FontRole.Numeric);
            _hits.Add(new HitRegion(r, HitKind.Tab, i));
        }

        // The current target capsule.
        double cx = 420;
        bool ageChip = Age is { State: not Sim.Ui.Ages.AgePanelState.NoContent };
        var cap = new RectD(cx, y + 7, Math.Min(ageChip ? 360 : 420, _w - DetailW - cx - 20), TabBarH - 14);
        if (ageChip) PaintAgeChip(d, m, new RectD(cap.Right + 12, y + 7, _w - DetailW - 20 - cap.Right - 12, TabBarH - 14));
        if (cap.W > 160)
        {
            PanelFrame.Paint(d, cap, t, 140, FrameKind.Chip, t.Semantic.ActiveFill, A(t.Semantic.Active, 0.7));
            if (s.TargetIndex is int ti)
            {
                ResearchNodeView v = s.Nodes[ti];
                d.Write(t, cap.X + 12, cap.Y + 4, "RESEARCHING", 9.5, t.Semantic.Active, TextAlign.Left, FontRole.Caps);
                d.Write(t, cap.X + 12, cap.Y + 16, ThemeText.Fit(m, t, v.Name, 14, cap.W - 150, FontRole.Heading), 14, t.Ink.Text, TextAlign.Left, FontRole.Heading);
                EraMarks.Progress(d, t, new RectD(cap.Right - 128, cap.Y + 19, 76, 8), v.Fraction, t.Semantic.Active, 141);
                d.Write(t, cap.Right - 12, cap.Y + 13, Pct(v.Fraction), 13, t.Ink.Text, TextAlign.Right, FontRole.Numeric);
            }
            else
            {
                d.Write(t, cap.X + 12, cap.Y + 10, ThemeText.Fit(m, t, PendingTarget >= 0
                    ? "Target ordered: " + s.Nodes[PendingTarget].Name + " - applies at End Turn"
                    : "No research target - research points are idle. Choose an available node.", 12.5, cap.W - 20), 12.5,
                    PendingTarget >= 0 ? t.Semantic.Active : t.Semantic.Progress, TextAlign.Left);
            }
        }

        // Legend: the four card states, as cards of this era.
        double lx = _w - DetailW + 14;
        (string, Rgba, Rgba)[] keys = [("Done", t.Semantic.CompletedFill, t.Semantic.Completed), ("Target", t.Semantic.ActiveFill, t.Semantic.Active),
            ("Open", t.Semantic.AvailableFill, t.Semantic.Available), ("Locked", t.Semantic.LockedFill, t.Semantic.Locked)];
        int k = 0;
        foreach ((string label, Rgba fill, Rgba edge) in keys)
        {
            PanelFrame.Paint(d, new RectD(lx, y + 17, 16, 14), t, 150 + k++, FrameKind.Chip, fill, edge, 1.4);
            d.Write(t, lx + 21, y + 16, label, 11.5, t.Ink.OnChromeSoft);
            lx += 28 + m.Width(t, label, 11.5) + 8;
        }
    }

    /// <summary>The Age header chip: current Age, progress toward the next, and its state; a click opens
    /// the Age surface (milestones, Advance Age). Read from <see cref="Age"/> (AgeQuery), never invented.</summary>
    private void PaintAgeChip(DrawList d, ITextMeasure m, RectD r)
    {
        if (r.W < 150 || Age is not { } a) return;
        EraTheme t = Theme;
        bool eligible = a.State == Sim.Ui.Ages.AgePanelState.Eligible;
        bool pending = a.State == Sim.Ui.Ages.AgePanelState.Pending;
        PanelFrame.Paint(d, r, t, 145, FrameKind.Chip, eligible ? Mix(t.Material.PanelRaised, t.Material.Accent, 0.18) : t.Material.PanelRaised,
            eligible ? t.Material.Accent : pending ? t.Semantic.Active : t.Material.AccentSoft, eligible ? 1.8 : 1.0);
        d.Write(t, r.X + 10, r.Y + 4, "AGE " + Sim.Ui.Ages.AgePanelModel.Numeral(a.CurrentAge), 9.5, t.Material.Accent, TextAlign.Left, FontRole.Caps);
        d.Write(t, r.X + 10, r.Y + 16, ThemeText.Fit(m, t, a.CurrentAgeName, 13, r.W * 0.40, FontRole.Heading), 13, t.Ink.Text, TextAlign.Left, FontRole.Heading);
        string right = a.State switch
        {
            Sim.Ui.Ages.AgePanelState.FinalAge => "final Age",
            Sim.Ui.Ages.AgePanelState.Eligible => "ADVANCE AGE available",
            Sim.Ui.Ages.AgePanelState.Pending => "advancing next turn",
            _ => "next: core " + a.CoreMet + "/" + a.CoreTotal + " - supp. " + a.SupportingMet + "/" + a.SupportingRequired,
        };
        FontRole role = eligible ? FontRole.Caps : FontRole.Body;
        d.Write(t, r.Right - 10, r.Y + 10, ThemeText.Fit(m, t, right, 11.5, r.W * 0.56, role), 11.5,
            eligible ? t.Material.Accent : pending ? t.Semantic.Active : t.Ink.TextSoft, TextAlign.Right, role);
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

    /// <summary>The completed share of each (lane, tier) cell — accumulated knowledge, which the lane
    /// segments' caps show from the Bronze era on. Index [lane][tier]; NaN where the cell is empty.</summary>
    public double[][] LaneTierCompletion()
    {
        TreeLayout L = Layout;
        var done = new int[L.Lanes.Count][];
        var all = new int[L.Lanes.Count][];
        for (int i = 0; i < done.Length; i++) { done[i] = new int[L.Columns]; all[i] = new int[L.Columns]; }
        for (int v = 0; v < Graph.OwnCount; v++)
        {
            PlacedVertex p = L.Placed[v];
            all[p.Lane][p.Column]++;
            if (Snapshot!.Nodes[Graph.Vertices[v].ContentIndex].State == NodeState.Completed) done[p.Lane][p.Column]++;
        }
        var r = new double[L.Lanes.Count][];
        for (int i = 0; i < r.Length; i++)
        {
            r[i] = new double[L.Columns];
            for (int c = 0; c < L.Columns; c++) r[i][c] = all[i][c] == 0 ? double.NaN : done[i][c] / (double)all[i][c];
        }
        return r;
    }

    private void PaintTree(DrawList d, ITextMeasure m)
    {
        EraTheme t = Theme;
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
        foreach (TierBand tb in L.Tiers)
        {
            double y0 = SY(tb.Y0), y1 = SY(tb.Y1);
            if (y1 < c.Y || y0 > c.Bottom) continue;
            if (tb.Tier % 2 == 1) d.Rect(new RectD(tx0, y0, tx1 - tx0, y1 - y0), A(t.Material.FieldAlt, 0.55));
            d.Line(tx0, y0 + 0.5, tx1, y0 + 0.5, A(t.Material.Hairline, 0.7), 1);
            if (z >= 0.45)
            {
                string label = "TIER " + (tb.Tier + 1).ToString(CultureInfo.InvariantCulture);
                double lx = SX(o.Margin + o.Spine);
                d.Write(t, lx, y0 + 5 * z, label, 11.5 * z, t.Material.Accent, TextAlign.Left, FontRole.Caps);
                string ages = ResearchTreeLayout.AgeRangeLabel((tb.Lo, tb.Hi));
                if (ages.Length > 0) d.Write(t, lx + m.Width(t, label, 11.5 * z, FontRole.Caps) + 12 * z, y0 + 5 * z, ages, 11.5 * z, t.Ink.TextSoft, TextAlign.Left);
            }
        }
        // Lane segments: each lane's share of a tier, tinted by its hue with a coloured cap and
        // its name, so a lane reads as one colour from tier to tier while lanes keep their order.
        // From the Bronze era on, the cap fills with the cell's completed share: accumulated
        // knowledge, visible at a glance.
        double[][] completion = t.Density.CardDetail >= 3 ? LaneTierCompletion() : [];
        foreach (LaneSegment sg in L.Segments)
        {
            TierBand tb = L.Tiers[sg.Tier];
            double y0 = SY(tb.Y0 + o.TierGap - 14), y1 = SY(tb.Y1 - o.RowGap / 2);
            if (y1 < c.Y || y0 > c.Bottom) continue;
            LaneBox lane = L.Lanes[sg.Lane];
            Rgba hue = t.Semantic.Lanes.Of(lane.Id);
            double x0 = SX(sg.X) - 4 * z, w = (sg.Width - o.Gutter) * z + 8 * z;
            var seg = new RectD(x0, y0, w, y1 - y0);
            PanelFrame.Paint(d, seg, t, 3000 + sg.Lane * 64 + sg.Tier, FrameKind.Chip, A(hue, 0.07), A(hue, 0.30), 0.8);
            var capR = new RectD(x0 + 6 * z, y0 + 1.5 * z, w - 12 * z, 2.4 * z);
            if (t.Edge.Corner == CornerStyle.Organic)
                d.Polyline(PanelFrame.Freehand(capR.X, capR.CenterY, capR.Right, capR.CenterY, 1.2 * z, sg.Lane * 64 + sg.Tier, 5), A(hue, 0.8), 2.6 * z);
            else d.Rect(capR, A(hue, completion.Length > 0 ? 0.30 : 0.75));
            if (completion.Length > 0 && completion[sg.Lane][sg.Tier] is double share && share > 0)
                d.Rect(new RectD(capR.X, capR.Y, capR.W * share, capR.H), A(hue, 0.95));
            if (z >= 0.45)
                d.Write(t, x0 + 8 * z, SY(tb.Y0 + o.TierGap - 25), ThemeText.Fit(m, t, lane.Name.ToUpperInvariant(), 10 * z, w - 12 * z, FontRole.Caps), 10 * z, hue, TextAlign.Left, FontRole.Caps);
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
            if (related[v] == 0) d.Rect(r, A(t.Material.Field, 0.5), null, 0, CornerRadius(z));
            else d.Polyline(PanelFrame.Outline(r, t, CardId(v), FrameKind.Card), related[v] == 1 ? t.Semantic.Prerequisite : t.Semantic.Dependent,
                Math.Max(2.0, t.Icons.StrokePx), closed: true);
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
                Rgba col = e.To == focusV ? t.Semantic.Prerequisite : t.Semantic.Dependent;
                double w = Math.Max(2.2, t.Icons.StrokePx) * Math.Max(0.6, z);
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
        PanelFrame.Paint(d, new RectD(c.X, hy, c.W, ColumnHeaderH), t, 103, FrameKind.Bar);
        // Lanes keep a fixed order; their chips share the header width in that order.
        double chipW = (c.W - 24 - 6 * (L.Lanes.Count - 1)) / L.Lanes.Count;
        foreach (LaneBox lane in L.Lanes)
            LaneChip(d, m, lane, c.X + 12 + lane.Index * (chipW + 6), hy + (ColumnHeaderH - 24) / 2, chipW);
        PaintControlRow(d, m);
        d.PopClip();
    }

    private double CornerRadius(double z) => Theme.Edge.Corner switch
    {
        CornerStyle.Organic => 8 * z, CornerStyle.Rounded => 7 * z, CornerStyle.Fine => 3 * z, _ => 0,
    };

    /// <summary>A card's stable frame seed: its vertex in its tree.</summary>
    private int CardId(int vertex) => 10_000 + (int)Tab * 4_000 + vertex;

    private void LaneChip(DrawList d, ITextMeasure m, LaneBox lane, double x, double y, double w)
    {
        EraTheme t = Theme;
        const double chipH = 24;
        Rgba hue = t.Semantic.Lanes.Of(lane.Id);
        var chip = new RectD(x, y, w, chipH);
        PanelFrame.Paint(d, chip, t, 200 + lane.Index, FrameKind.Chip, t.Material.PanelRaised, A(hue, 0.85), 1.0);
        // Disclosure triangle: right when collapsed, down when expanded.
        double tx = chip.X + 11, ty = chip.Y + chipH / 2;
        if (lane.Collapsed) d.Polygon([(tx - 3, ty - 5), (tx + 4, ty), (tx - 3, ty + 5)], hue);
        else d.Polygon([(tx - 5, ty - 3), (tx + 5, ty - 3), (tx, ty + 4)], hue);
        if (w > 60)
        {
            // From the Classical era on the chip also counts what is known: "done/total".
            string count = lane.Collapsed ? lane.NodeCount.ToString(CultureInfo.InvariantCulture) + " hidden"
                : t.Density.CardDetail >= 4 && !lane.External ? LaneDone(lane.Index).ToString(CultureInfo.InvariantCulture) + "/" + lane.NodeCount.ToString(CultureInfo.InvariantCulture)
                : lane.NodeCount.ToString(CultureInfo.InvariantCulture);
            double cw = m.Width(t, count, 11, FontRole.Numeric);
            // A navigation label: it shrinks to fit rather than lose a word.
            string name = lane.Name.ToUpperInvariant();
            double ns = ThemeText.FitSize(m, t, name, 12, w - cw - 36, FontRole.Caps);
            d.Write(t, chip.X + 22, chip.Y + 5 + (12 - ns) / 2, ThemeText.Fit(m, t, name, ns, w - cw - 36, FontRole.Caps), ns, hue, TextAlign.Left, FontRole.Caps);
            d.Write(t, chip.Right - 8, chip.Y + 6, count, 11, t.Ink.TextSoft, TextAlign.Right, FontRole.Numeric);
        }
        _hits.Add(new HitRegion(chip, HitKind.LaneToggle, lane.Index));
    }

    private int LaneDone(int lane)
    {
        int n = 0;
        for (int v = 0; v < Graph.OwnCount; v++)
            if (Layout.Placed[v].Lane == lane && Snapshot!.Nodes[Graph.Vertices[v].ContentIndex].State == NodeState.Completed) n++;
        return n;
    }

    /// <summary>The opaque control row: on the left, the tier (with its full Age names) at the top
    /// of the view; then the highlight legend; on the right, jump to the target, reset to the
    /// default view, fit the tree, show/hide the overview strip.</summary>
    private void PaintControlRow(DrawList d, ITextMeasure m)
    {
        EraTheme t = Theme;
        RectD c = Canvas;
        var row = new RectD(c.X, c.Y - ControlRowH, c.W, ControlRowH);
        PanelFrame.Paint(d, row, t, 104, FrameKind.Bar);
        bool hasTarget = Snapshot!.TargetIndex is int ti && Content.Nodes[ti].Tree == (ResearchTree)((int)Tab + 1);
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
            double w = m.Width(buttons[i].Label, 11, FontRole.Caps) + 20;   // era-invariant geometry
            x -= w;
            var r = new RectD(x, y, w, h);
            bool primary = buttons[i].Kind == HitKind.Frontier;
            PanelFrame.Paint(d, r, t, 170 + i, FrameKind.Button, primary ? t.Semantic.ActiveFill : buttons[i].On ? SelectedFill : t.Material.PanelRaised,
                primary ? t.Semantic.Active : buttons[i].On ? t.Material.AccentSoft : t.Material.Hairline, primary ? 1.1 : 0.9);
            double bs = ThemeText.FitSize(m, t, buttons[i].Label, 11, w - 6, FontRole.Caps);
            d.Write(t, r.X + w / 2, r.Y + 7 + (11 - bs) / 2, ThemeText.Fit(m, t, buttons[i].Label, bs, w - 6, FontRole.Caps), bs, primary ? t.Semantic.Active : t.Ink.Text, TextAlign.Center, FontRole.Caps);
            _hits.Add(new HitRegion(r, buttons[i].Kind, 0));
            x -= 6;
        }
        // Legend for the highlight (just left of the buttons).
        (string Label, Rgba Col, bool Dash)[] keys = [("requires", Mix(t.Semantic.Prerequisite, t.Ink.OnChrome, 0.3), false),
            ("leads to", Mix(t.Semantic.Dependent, t.Ink.OnChrome, 0.4), false), ("one of", t.Ink.OnChromeSoft, true)];
        double lx = x - 8;
        for (int i = keys.Length - 1; i >= 0; i--)
        {
            double tw = m.Width(t, keys[i].Label, 11);
            lx -= tw + 30;
            d.Line(lx, row.Y + 19, lx + 20, row.Y + 19, keys[i].Col, 2, keys[i].Dash ? (4, 3) : null);
            d.Write(t, lx + 24, row.Y + 12, keys[i].Label, 11, t.Ink.OnChromeSoft);
        }
        // Current tier at the top of the view, with its full Age names.
        TreeLayout L = Layout;
        int tier = L.TierAt(Camera.ToWorldY(c.Y + 1, c.Y));
        TierBand band = L.Tiers[tier];
        string head = "TIER " + (tier + 1).ToString(CultureInfo.InvariantCulture) + " of " + L.Columns.ToString(CultureInfo.InvariantCulture);
        d.Write(t, c.X + 12, row.Y + 11, head, 11.5, t.Ink.OnChromeAccent, TextAlign.Left, FontRole.Caps);
        double hx = c.X + 22 + m.Width(t, head, 11.5, FontRole.Caps);
        d.Write(t, hx, row.Y + 11, ThemeText.Fit(m, t, ResearchTreeLayout.AgeRangeLabel((band.Lo, band.Hi)), 11.5, Math.Max(0, lx - hx - 16)), 11.5, t.Ink.OnChromeSoft);
    }

    /// <summary>
    /// A RESEARCH CARD in the era's hand. Every era shows what the Director requires — the state
    /// (mark and fill), the name, the cost, the Age (numeral and full name), retained progress
    /// (notches → bar), and the cross-lane label; prerequisites, lock reasons and Eurekas are on
    /// hover and in the detail panel. Density adds the rest: Eureka pips (A2+), the
    /// prerequisite/dependent stubs (A3+), discounts (A5+), the estimate to complete (A7+).
    /// </summary>
    private void PaintCard(DrawList d, ITextMeasure m, int vertex, double x, double y, double z, bool focus)
    {
        EraTheme t = Theme;
        SemanticTokens s = t.Semantic;
        GraphVertex gv = Graph.Vertices[vertex];
        ResearchNodeView v = Snapshot!.Nodes[gv.ContentIndex];
        PlacedVertex p = Layout.Placed[vertex];
        double w = p.W * z, h = p.H * z;
        var r = new RectD(x, y, w, h);
        bool selected = gv.ContentIndex == Selected;
        int id = CardId(vertex);

        if (gv.External)
        {
            bool done = v.State == NodeState.Completed;
            PanelFrame.Paint(d, r, t, id, FrameKind.Chip, done ? s.CompletedFill : t.Material.PanelSunken, done ? s.Completed : t.Material.Hairline, 0.9);
            if (z > 0.3) d.Write(t, x + w / 2, y + h / 2 - 7 * z, ThemeText.Fit(m, t, v.Name, 12 * z, w - 16 * z), 12 * z, done ? t.Ink.Text : t.Ink.TextSoft, TextAlign.Center);
            return;
        }

        Rgba hue = ProgressionPalette.BranchOf(t, Content, Content.Nodes[gv.ContentIndex]);
        // The card is the era's material with the era's border (its identity across eras); the STATE
        // is its fill, an inner rule in the state's colour, and its mark (constant meanings).
        (Rgba fill, Rgba? inner, double iw) = v.State switch
        {
            NodeState.Completed => (s.CompletedFill, (Rgba?)s.Completed, 1.4),
            NodeState.CurrentTarget => (s.ActiveFill, s.Active, 2.2),
            NodeState.Available => (s.AvailableFill, s.Available, 1.6),
            NodeState.Partial => (s.LockedFill, s.Progress, 1.4),
            _ => (s.LockedFill, null, 0.0),
        };
        if (v.State == NodeState.CurrentTarget)
            d.Rect(new RectD(x - 5 * z, y - 5 * z, w + 10 * z, h + 10 * z), A(s.Active, 0.18), null, 0, CornerRadius(z) + 4 * z);
        if (PendingTarget == gv.ContentIndex)
            d.Polyline(PanelFrame.Outline(new RectD(x - 4 * z, y - 4 * z, w + 8 * z, h + 8 * z), t, id, FrameKind.Card), s.Active, 1.2, closed: true);
        Rgba border = v.State == NodeState.Locked ? A(t.Material.Border, 0.5) : t.Material.Border;
        bool hairline = t.Edge.Corner == CornerStyle.Fine;   // the modern card: one hairline, in the state's colour
        if (hairline && inner is Rgba hc && !(selected || focus)) border = hc;
        PanelFrame.Paint(d, r, t, id, FrameKind.Card, fill, selected || focus ? t.Material.BorderStrong : border,
            selected || focus ? 1.5 : hairline && inner is not null ? 1.6 : v.State == NodeState.Locked ? 0.8 : 1.0);
        if (inner is Rgba ic && !hairline)
        {
            double ii = (t.Edge.BorderPx + (t.Edge.DoubleRule ? 3.6 : 1.6)) * Math.Min(1.0, z);
            d.Polyline(PanelFrame.Outline(r.Inset(ii), t, id, FrameKind.Card), ic, Math.Max(1.0, iw * Math.Min(1.0, z)), closed: true);
        }

        // The lane stripe, inside the frame.
        double ins = PanelFrame.ContentInset(t, FrameKind.Card) * Math.Min(1.0, z);
        var stripe = new RectD(x + ins, y + ins + 2 * z, Math.Max(2.5, 4 * z), h - 2 * ins - 4 * z);
        Rgba stripeCol = v.State == NodeState.Locked ? A(hue, 0.45) : hue;
        if (t.Edge.Corner == CornerStyle.Organic)
            d.Polyline(PanelFrame.Freehand(stripe.CenterX, stripe.Y, stripe.CenterX, stripe.Bottom, 1.0 * z, id, 7), stripeCol, Math.Max(2.5, 4.2 * z));
        else d.Rect(stripe, stripeCol);

        if (z < 0.28) return;   // level of detail: colour only when far out
        bool dim = v.State == NodeState.Locked;
        double pad = ins + 9 * z;
        double nameSize = 14.5 * z;
        double iconR = 7 * z;
        double right = x + w - ins;
        d.Write(t, x + pad, y + 6 * z, ThemeText.Fit(m, t, v.Name, nameSize, w - pad - ins - 2 * iconR - 10 * z, FontRole.Heading), nameSize,
            dim ? t.Ink.TextSoft : t.Ink.Text, TextAlign.Left, FontRole.Heading);

        // State mark, top right.
        MarkKind kind = v.State switch
        {
            NodeState.Completed => MarkKind.Completed,
            NodeState.CurrentTarget => MarkKind.Active,
            NodeState.Available => MarkKind.Available,
            NodeState.Partial => MarkKind.Partial,
            _ => MarkKind.Locked,
        };
        EraMarks.State(d, t, right - iconR - 4 * z, y + ins + iconR + 3 * z, iconR, kind, v.Fraction, id, dim);

        if (z < 0.45) return;
        int detail = t.Density.CardDetail;
        double small = 12 * z;
        // Row 2: cost (or Known), any discount (A5+), Eureka pips on the right (A2+).
        string meta = v.State == NodeState.Completed ? "Known" : Num(v.EffectiveCost) + " RP";
        d.Write(t, x + pad, y + 27 * z, meta, small, dim ? t.Ink.TextDim : t.Ink.TextSoft, TextAlign.Left, FontRole.Numeric);
        if (detail >= 4 && v.EffectiveCost < v.BaseCost && v.State != NodeState.Completed)
            d.Write(t, x + pad + m.Width(t, meta, small, FontRole.Numeric) + 6 * z, y + 27 * z, "-" + Pct(1 - v.EffectiveCost / v.BaseCost), small, s.Positive, TextAlign.Left, FontRole.Numeric);
        if (detail >= 2)
        {
            int total = Math.Min(6, v.Eurekas.Count);
            for (int e = 0; e < total; e++)
            {
                bool fired = v.Eurekas[e].Fired;
                double px = right - 6 * z - (total - e) * 10 * z + 3.5 * z;
                d.Circle(px, y + 34 * z, 3.3 * z, fired ? s.Completed : null, fired ? null : (dim ? t.Ink.TextDim : t.Material.AccentSoft), 1.1 * z);
            }
        }

        // Row 3: the Age — numeral AND full name, always, with the row to itself.
        d.Write(t, x + pad, y + 44 * z, ThemeText.Fit(m, t, ResearchTreeLayout.AgeShort(v.Age), 10.5 * z, w - pad - ins - 6 * z), 10.5 * z,
            dim ? t.Ink.TextDim : t.Material.Accent, TextAlign.Left, FontRole.Body);

        // Row 4: dependency stubs (A3+), the target's estimate to complete (A7+) and the cross-lane
        // prerequisite label (every era).
        double fy = y + 61 * z, fx = x + pad;
        Rgba cc = dim ? t.Ink.TextDim : t.Ink.TextSoft;
        if (detail >= 3)
        {
            (int[] pre, int[] dep) = ResearchTreeLayout.Neighbours(Graph, vertex);
            d.Polygon([(fx, fy + 9 * z), (fx + 8 * z, fy + 9 * z), (fx + 4 * z, fy + 2 * z)], A(s.Prerequisite, dim ? 0.5 : 0.9));
            string ins2 = pre.Length.ToString(CultureInfo.InvariantCulture);
            d.Write(t, fx + 11 * z, fy, ins2, 11 * z, cc, TextAlign.Left, FontRole.Numeric);
            fx += 11 * z + m.Width(t, ins2, 11 * z, FontRole.Numeric) + 8 * z;
            d.Polygon([(fx, fy + 2 * z), (fx + 8 * z, fy + 2 * z), (fx + 4 * z, fy + 9 * z)], A(s.Dependent, dim ? 0.5 : 0.9));
            string outs = dep.Length.ToString(CultureInfo.InvariantCulture);
            d.Write(t, fx + 11 * z, fy, outs, 11 * z, cc, TextAlign.Left, FontRole.Numeric);
            fx += 11 * z + m.Width(t, outs, 11 * z, FontRole.Numeric) + 10 * z;
        }
        if (detail >= 5 && v.State == NodeState.CurrentTarget && Snapshot.PointsPerTurn > 0)
        {
            string est = "~" + Math.Ceiling(Math.Max(0, v.EffectiveCost - v.Progress) / Snapshot.PointsPerTurn).ToString("0", CultureInfo.InvariantCulture) + " t";
            d.Write(t, fx, fy, est, 11 * z, s.Active, TextAlign.Left, FontRole.Numeric);
            fx += m.Width(t, est, 11 * z, FontRole.Numeric) + 10 * z;
        }
        string cross = CrossLaneLabel(vertex);
        string tail = cross.Length > 0 ? cross
            : v.Lock.HasFlag(LockReason.ResearchStage) && !v.Lock.HasFlag(LockReason.MissingPrerequisites) ? "needs university" : "";
        if (tail.Length > 0)
        {
            double cw = right - 4 * z - fx;
            if (cw > 30 * z)
            {
                string fit = ThemeText.Fit(m, t, tail, 10.5 * z, cw - 10 * z);
                double tw2 = m.Width(t, fit, 10.5 * z) + 10 * z;
                var chip = new RectD(right - 4 * z - tw2, fy - 2 * z, tw2, 15 * z);
                Rgba chipCol = cross.Length > 0 ? s.Prerequisite : s.Progress;
                PanelFrame.Paint(d, chip, t, id + 500_000, FrameKind.Chip, A(t.Material.PanelRaised, 0.7), A(chipCol, dim ? 0.4 : 0.75), 0.8);
                d.Write(t, chip.X + 5 * z, fy, fit, 10.5 * z, cross.Length > 0 ? (dim ? t.Ink.TextDim : t.Ink.TextSoft) : s.Progress, TextAlign.Left);
            }
        }

        // Retained progress, in the era's representation (tally notches → graduated bar).
        if (v.Progress > 0 && v.State != NodeState.Completed)
        {
            double ph = t.Controls.Progress == ProgressStyle.Notches ? 6 * z : 3.5 * z;
            var bar = new RectD(x + pad - 2 * z, y + h - ins - ph - 2 * z, w - pad - ins - 2 * z, ph);
            EraMarks.Progress(d, t, bar, v.Fraction, v.State == NodeState.CurrentTarget ? s.Active : s.Progress, id);
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
        EraTheme t = Theme;
        RectD r = MinimapRect();
        d.Rect(new RectD(r.X - 6, c0Y(), StripW - 6, Canvas.H), t.Material.Field);
        PanelFrame.Paint(d, r.Inset(-3), t, 180, FrameKind.Chip, t.Material.PanelSunken, t.Material.Border, 0.8);
        double sx = r.W / Math.Max(1, Layout.Width), sy = r.H / Math.Max(1, Layout.Height);
        foreach (PlacedVertex p in Layout.Placed)
        {
            if (Graph.Vertices[p.Vertex].External || p.Hidden) continue;
            NodeState st = Snapshot!.Nodes[Graph.Vertices[p.Vertex].ContentIndex].State;
            Rgba col = st switch
            {
                NodeState.Completed => t.Semantic.Completed, NodeState.CurrentTarget => t.Semantic.Active,
                NodeState.Available => t.Semantic.Available, NodeState.Partial => t.Semantic.Progress, _ => A(t.Semantic.Locked, 0.55),
            };
            d.Rect(new RectD(r.X + p.X * sx, r.Y + p.Y * sy, Math.Max(1.2, p.W * sx), Math.Max(1.2, p.H * sy)), col);
        }
        (double _, double y0, double _, double y1) = VisibleWorld();
        double vy0 = Math.Clamp(r.Y + y0 * sy, r.Y, r.Bottom), vy1 = Math.Clamp(r.Y + y1 * sy, r.Y, r.Bottom);
        d.Rect(new RectD(r.X - 2, vy0, r.W + 4, Math.Max(2, vy1 - vy0)), A(t.Material.Accent, 0.12), t.Material.Accent, 1.2);
        _hits.Add(new HitRegion(r, HitKind.Minimap, 0));
    }

    private double c0Y() => Canvas.Y;

    // ---- the detail panel

    private void PaintDetail(DrawList d, ITextMeasure m)
    {
        EraTheme t = Theme;
        SemanticTokens sm = t.Semantic;
        var panel = new RectD(_w - DetailW, LensBarH + TabBarH, DetailW, _h - LensBarH - TabBarH);
        PanelFrame.Paint(d, panel, t, 190, FrameKind.Panel);
        d.PushClip(panel);
        int node = Hovered >= 0 ? Hovered : Selected;
        // Era-invariant placement (continuity): the content box clears every era's frame and ornament
        // band, so the panel's regions — and its button's hit rect — are the same in every era.
        double inset = PanelFrame.ContentInset(t, FrameKind.Panel);
        double x = panel.X + 22, w = DetailW - 44, y = panel.Y + 24;
        double L(double size) => t.Type.Line(size);
        if (node < 0)
        {
            ResearchSnapshot s = Snapshot!;
            d.Title(t, x, y, Graph.Tree == ResearchTree.Technology ? "The Technology Tree" : "The Civics Tree", 24, t.Material.Accent);
            y += L(24) + 10;
            string intro = Graph.Tree == ResearchTree.Technology
                ? "The main trunk and five specialised subtrees. The subtrees open together once the research stage is reached."
                : "Forms of social organisation. Adopted civics also appear under the INSTITUTIONS lens.";
            foreach (string line in ThemeText.Wrap(m, t, intro, 14, w)) { d.Write(t, x, y, line, 14, t.Ink.TextSoft); y += L(14); }
            y += 12;
            y = Stat(d, m, x, w, y, "Research stage", s.StageReached ? "reached" : "not yet reached", s.StageReached ? sm.Positive : sm.Progress);
            y = Stat(d, m, x, w, y, "Research per turn", s.PointsPerTurn.ToString("0.0", CultureInfo.InvariantCulture) + " RP", sm.Knowledge);
            y = Stat(d, m, x, w, y, "Completed", Graph.Tree == ResearchTree.Technology ? s.CompletedTechnology + " of " + Content.TechnologyCount : s.CompletedCivics + " of " + Content.CivicsCount, t.Ink.Text);
            int avail = 0;
            for (int v = 0; v < Graph.OwnCount; v++) if (s.Nodes[Graph.Vertices[v].ContentIndex].Available) avail++;
            y = Stat(d, m, x, w, y, "Available now", avail.ToString(CultureInfo.InvariantCulture), sm.Available);
            y += 14;
            foreach (string line in ThemeText.Wrap(m, t, "Hover a node for its details. Click an available node to make it the research target; the order applies at End Turn. Drag to pan, wheel to zoom.", 13, w))
            { d.Write(t, x, y, line, 13, t.Ink.TextDim); y += L(13); }
            d.PopClip();
            return;
        }

        ResearchNodeView n = Snapshot!.Nodes[node];
        Rgba hue = ProgressionPalette.BranchOf(t, Content, Content.Nodes[node]);
        d.Rect(new RectD(panel.X + inset, panel.Y + inset, DetailW - 2 * inset, 4), hue);
        d.Write(t, x, y, n.BranchName.ToUpperInvariant() + "  ·  " + ResearchTreeLayout.AgeLabel(n.Age).ToUpperInvariant(), 11.5, hue, TextAlign.Left, FontRole.Caps);
        y += L(11.5) + 3;
        bool firstLine = true;
        foreach (string line in ThemeText.Wrap(m, t, n.Name, 25, w, FontRole.Title))
        {
            if (firstLine) d.Title(t, x, y, line, 25, t.Ink.Text);
            else d.Write(t, x, y, line, 25, t.Ink.Text, TextAlign.Left, FontRole.Title);
            firstLine = false;
            y += L(25);
        }
        Rgba stc = n.State switch { NodeState.Completed => sm.Completed, NodeState.CurrentTarget => sm.Active, NodeState.Available => sm.Available, NodeState.Partial => sm.Progress, _ => sm.Locked };
        string stateLabel = ProgressionPalette.StateLabel(n.State).ToUpperInvariant();
        double sw = m.Width(t, stateLabel, 11, FontRole.Caps) + 18;
        PanelFrame.Paint(d, new RectD(x, y + 2, sw, 19), t, 191, FrameKind.Chip, Mix(t.Material.Panel, stc, 0.14), stc, 1.0);
        d.Write(t, x + 9, y + 5, stateLabel, 11, stc, TextAlign.Left, FontRole.Caps);
        if (PendingTarget == node) d.Write(t, x + sw + 10, y + 4, "target ordered - applies at End Turn", 12, sm.Active);
        y += 30;

        // Cost and progress.
        y = Heading(d, x, w, y, "COST", 1);
        y = Stat(d, m, x, w, y, "Base cost", Num(n.BaseCost) + " RP", t.Ink.TextSoft);
        foreach ((string uni, double f) in n.CostTerms)
            y = Stat(d, m, x, w, y, "  " + ResearchTreeLayout.Title(uni) + " university", "× " + f.ToString("0.00", CultureInfo.InvariantCulture), sm.Positive);
        y = Stat(d, m, x, w, y, "Effective cost" + (n.FloorBinds ? " (floor)" : ""), Num(n.EffectiveCost) + " RP", t.Ink.Text);
        if (n.State != NodeState.Completed)
        {
            EraMarks.Progress(d, t, new RectD(x, y + 3, w, t.Controls.Progress == ProgressStyle.Notches ? 11 : 8), n.Fraction,
                n.State == NodeState.CurrentTarget ? sm.Active : sm.Progress, 192);
            y += 18;
            string turns = Snapshot.PointsPerTurn > 0 ? "  ·  ~" + Math.Ceiling(Math.Max(0, n.EffectiveCost - n.Progress) / Snapshot.PointsPerTurn).ToString("0", CultureInfo.InvariantCulture) + " turns at current rate" : "";
            d.Write(t, x, y, Num(n.Progress) + " / " + Num(n.EffectiveCost) + " RP" + turns, 12, t.Ink.TextSoft, TextAlign.Left, FontRole.Numeric);
            y += L(12) + 6;
        }

        // Prerequisites and lock reasons.
        y = Heading(d, x, w, y, n.Prerequisites.Count == 0 ? "PREREQUISITES - none (root)" : "PREREQUISITES", 2);
        bool anyOr = false;
        foreach (PrereqView p in n.Prerequisites) if (p.Kind == EdgeKind.Or) anyOr = true;
        if (anyOr && n.PrerequisiteExpression is string expr)
        {
            foreach (string line in ThemeText.Wrap(m, t, expr.Replace("_", " "), 11.5, w, FontRole.Numeric)) { d.Write(t, x, y, line, 11.5, t.Ink.TextDim, TextAlign.Left, FontRole.Numeric); y += L(11.5); }
            y += 2;
        }
        int pi = 0;
        foreach (PrereqView p in n.Prerequisites)
        {
            EraMarks.Tick(d, t, x + 1, y + 3, 11, p.Completed, p.Completed ? sm.Completed : sm.Danger, 193 + pi++);
            // A prerequisite from another lane (or the other tree) names where it lives.
            int pv = Graph.VertexOf(p.ContentIndex), nv = Graph.VertexOf(node);
            string where = "";
            if (pv >= 0 && nv >= 0 && Layout.Placed[pv].Lane != Layout.Placed[nv].Lane)
                where = " (" + (Graph.Vertices[pv].External ? (Graph.Tree == ResearchTree.Civics ? "Technology" : "Civics") : Layout.Lanes[Layout.Placed[pv].Lane].Name) + ")";
            d.Write(t, x + 18, y + 1, ThemeText.Fit(m, t, p.Name + where, 14, w - 70), 14, p.Completed ? t.Ink.Text : t.Ink.TextSoft);
            d.Write(t, x + w, y + 3, p.Kind == EdgeKind.And ? "required" : "one of", 11, t.Ink.TextDim, TextAlign.Right);
            y += Math.Max(20, L(14));
        }
        if (n.Prerequisites.Count == 0) y += 4;
        if (n.Lock.HasFlag(LockReason.ResearchStage))
        {
            foreach (string line in ThemeText.Wrap(m, t, "Subtree closed: the research stage is not reached (" + Snapshot.StageExpression.Replace("_", " ") + ").", 12.5, w))
            { d.Write(t, x, y, line, 12.5, sm.Progress); y += L(12.5); }
        }
        if (n.Lock.HasFlag(LockReason.SubtreeNotExhausted))
        { d.Write(t, x, y, "Recursive: waits for every finite node of its subtree.", 12.5, sm.Progress); y += L(12.5); }
        y += 6;

        // Eureka.
        y = Heading(d, x, w, y, "EUREKA", 3);
        if (n.Eurekas.Count == 0) { d.Write(t, x, y, "This node has no Eureka.", 13, t.Ink.TextDim); y += L(13) + 8; }
        else
        {
            double credited = n.EurekaCredited + n.ForeignCredited;
            d.Write(t, x, y, ThemeText.Fit(m, t, "Acceleration credit " + Num(credited) + " of " + Num(n.EurekaCeiling) + " RP ceiling (" + Pct(n.EurekaCeiling / Math.Max(1e-9, n.BaseCost)) + " of base)", 12, w, FontRole.Numeric), 12, t.Ink.TextSoft, TextAlign.Left, FontRole.Numeric);
            y += L(12) + 2;
            double ce = Math.Max(1e-9, n.EurekaCeiling);
            if (t.Charts.Sophistication <= 1)
            {
                // A tally of the ceiling: the credited share as notches/counters.
                EraMarks.Progress(d, t, new RectD(x, y, w, 9), Math.Min(1, credited / ce), sm.Completed, 194);
            }
            else
            {
                var bar = new RectD(x, y, w, 8);
                d.Rect(bar, A(t.Material.PanelSunken, 0.6), t.Material.Hairline, 1, t.Edge.Corner == CornerStyle.Fine ? 2 : 0);
                d.Rect(new RectD(x, y, w * Math.Min(1, n.EurekaCredited / ce), 8), sm.Completed);
                d.Rect(new RectD(x + w * Math.Min(1, n.EurekaCredited / ce), y, w * Math.Min(1, n.ForeignCredited / ce), 8), sm.Lanes.Civics);
                if (t.Charts.Ticks)
                    for (int k = 1; k < 4; k++) d.Line(x + w * k / 4.0, y + 8, x + w * k / 4.0, y + 11, A(t.Material.Border, 0.6), 0.6);
            }
            y += 14;
            d.Write(t, x, y, ThemeText.Fit(m, t, "Eureka " + Num(n.EurekaCredited) + " RP  ·  foreign exposure " + Num(n.ForeignCredited) + " RP" + (n.ExposureOffered > 0 ? " (offered " + Num(n.ExposureOffered) + ")" : ""), 11.5, w, FontRole.Numeric), 11.5, t.Ink.TextDim, TextAlign.Left, FontRole.Numeric);
            y += L(11.5) + 6;
            int ei = 0;
            foreach (EurekaView e in n.Eurekas)
            {
                Rgba ec = e.Fired ? sm.Completed : e.HoldsNow == true ? sm.Positive : t.Ink.TextDim;
                EraMarks.State(d, t, x + 6, y + 9, 4.5, e.Fired ? MarkKind.Completed : MarkKind.Available, 0, 195 + ei++);
                List<string> lines = ThemeText.Wrap(m, t, e.Text, 13, w - 70);
                double ly0 = y;
                for (int k = 0; k < lines.Count && k < 3; k++) { d.Write(t, x + 18, y + 1, lines[k], 13, e.Fired ? t.Ink.Text : t.Ink.TextSoft); y += L(13); }
                d.Write(t, x + w, ly0 + 3, Pct(e.Weight) + " · " + Num(e.MaxCredit), 11, t.Ink.TextDim, TextAlign.Right, FontRole.Numeric);
                string status = e.Fired ? "fired - credited" : e.Condition is null ? "not evaluable yet (" + e.System + ")" : e.HoldsNow == true ? "holds now - fires when available" : "condition: " + e.Condition;
                foreach (string line in ThemeText.Wrap(m, t, status, 11, w - 18)) { d.Write(t, x + 18, y, line, 11, ec, TextAlign.Left); y += L(11); }
                y += 6;
            }
        }

        // University relevance.
        y = Heading(d, x, w, y, "UNIVERSITY RELEVANCE", 4);
        if (n.Universities.Count == 0) { d.Write(t, x, y, "None - main trunk and civics cost their base.", 13, t.Ink.TextDim); y += L(13) + 8; }
        foreach (UniversityView u in n.Universities)
        {
            d.Write(t, x, y, u.Name, 14, t.Ink.Text);
            d.Write(t, x + w, y + 2, u.Role, 11.5, u.Role == "primary" ? t.Material.Accent : t.Ink.TextDim, TextAlign.Right, FontRole.Caps);
            y += Math.Max(20, L(14));
        }
        if (n.Unlocks.Count > 0)
        {
            y = Heading(d, x, w, y + 4, "OPENS THE WAY TO", 5);
            foreach (string line in ThemeText.Wrap(m, t, string.Join(", ", n.Unlocks), 13, w)) { d.Write(t, x, y, line, 13, t.Ink.TextSoft); y += L(13); }
        }
        y += 8;
        foreach (string line in ThemeText.Wrap(m, t, n.Description, 13, w)) { if (y > panel.Bottom - 80) break; d.Write(t, x, y, line, 13, t.Ink.TextDim); y += L(13); }

        // Action.
        var btn = new RectD(x, panel.Bottom - 62, w, 44);
        bool can = n.Available && !n.IsTarget && PendingTarget != node;
        d.Rect(new RectD(panel.X + inset, btn.Y - 14, DetailW - 2 * inset, panel.Bottom - inset - btn.Y + 14), t.Material.Panel);
        PanelFrame.Paint(d, btn, t, 199, FrameKind.Button, can ? t.Semantic.ActiveFill : t.Material.PanelRaised, can ? sm.Active : t.Material.Hairline, can ? 1.3 : 0.9);
        string label = n.State switch
        {
            NodeState.Completed => "Known",
            NodeState.CurrentTarget => "Current research target",
            _ when PendingTarget == node => "Ordered - applies at End Turn",
            NodeState.Available => "Set as research target",
            _ => "Locked",
        };
        d.Write(t, btn.CenterX, btn.Y + 12, label.ToUpperInvariant(), 14, can ? sm.Active : t.Ink.TextDim, TextAlign.Center, FontRole.Caps);
        if (can) _hits.Add(new HitRegion(btn, HitKind.SetTarget, node));
        d.PopClip();
    }

    /// <summary>A section heading: capitals in the era's accent over the era's rule.</summary>
    private double Heading(DrawList d, double x, double w, double y, string text, int id)
    {
        d.Write(Theme, x, y, text, 11.5, Theme.Material.Accent, TextAlign.Left, FontRole.Caps);
        PanelFrame.Rule(d, new RectD(x, y + 16, w, 6), Theme, 900 + id);
        return y + 27;
    }

    private double Stat(DrawList d, ITextMeasure m, double x, double w, double y, string label, string value, Rgba valueColor)
    {
        d.Write(Theme, x, y, label, 14, Theme.Ink.TextSoft);
        d.Write(Theme, x + w, y + 1, value, 14, valueColor, TextAlign.Right, FontRole.Numeric);
        return y + Math.Max(20, Theme.Type.Line(14));
    }

    // ---- the other lenses

    private void PaintLensPage(DrawList d, ITextMeasure m)
    {
        EraTheme t = Theme;
        LensPage p = Page!;
        RectD c = Canvas;
        double x = c.X + 48, y = c.Y + 36, w = c.W - 96;
        d.Title(t, x, y, p.Title, 34, t.Material.Accent);
        y += 46;
        d.Write(t, x, y, ThemeText.Fit(m, t, p.Purpose, 16, w), 16, t.Ink.TextSoft);
        y += 30;
        Rgba sc = p.Status switch { LensStatus.Functional => t.Semantic.Positive, LensStatus.PartialData => t.Semantic.Progress, _ => t.Ink.TextDim };
        var note = new RectD(x, y, w, 40);
        PanelFrame.Paint(d, note, t, 400, FrameKind.Chip, Mix(t.Material.Panel, sc, 0.10), A(sc, 0.6), 1.0);
        d.Write(t, x + 16, y + 11, ThemeText.Fit(m, t, p.StatusNote, 14, w - 32), 14, sc);
        y += 64;
        if (p.Sections.Count == 0)
        {
            d.Write(t, c.CenterX, c.CenterY, "NOT YET SIMULATED", 22, t.Ink.TextDim, TextAlign.Center, FontRole.Caps);
            return;
        }
        double colW = (w - 24 * (p.Sections.Count - 1)) / p.Sections.Count;
        for (int s = 0; s < p.Sections.Count; s++)
        {
            LensSection sec = p.Sections[s];
            double sx = x + s * (colW + 24), sy = y;
            PanelFrame.Paint(d, new RectD(sx, sy, colW, c.Bottom - sy - 30), t, 410 + s, FrameKind.Panel);
            d.Write(t, sx + 18, sy + 16, ThemeText.Fit(m, t, sec.Heading.ToUpperInvariant(), 13, colW * 0.6, FontRole.Caps), 13, t.Material.Accent, TextAlign.Left, FontRole.Caps);
            if (sec.Note.Length > 0) d.Write(t, sx + colW - 18, sy + 17, sec.Note, 12, t.Ink.TextDim, TextAlign.Right);
            sy += 46;
            if (sec.Items.Count == 0) { d.Write(t, sx + 18, sy, "Nothing yet.", 14, t.Ink.TextDim); continue; }
            int k = 0;
            foreach (string item in sec.Items)
            {
                if (sy > c.Bottom - 60) { d.Write(t, sx + 18, sy, "...", 14, t.Ink.TextDim); break; }
                EraMarks.State(d, t, sx + 22, sy + 9, 3.2, MarkKind.Completed, 0, 420 + k++);
                d.Write(t, sx + 32, sy, ThemeText.Fit(m, t, item, 14, colW - 50), 14, t.Ink.Text);
                sy += Math.Max(21, t.Type.Line(14));
            }
        }
    }
}
