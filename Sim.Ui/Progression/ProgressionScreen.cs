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

public enum HitKind { Lens, Tab, SetTarget, Close, Minimap, LaneToggle, Frontier, Fit, MinimapToggle, Reset, AgeOpen, CloseDetail, Panel }

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
public sealed partial class ProgressionScreen
{
    // UR-4 (M5 polish, UI readability): the screen's strips at the 1080p reference (UI scale 1) — tall enough for their
    // text at the type scale's roles (nothing below Caption); every length is × Scale.
    public const double LensBarRef = 66, TabBarRef = 58, ColumnHeaderRef = 40, ControlRowRef = 44, DetailRef = 440, StripRef = 64;

    /// <summary>The window width (at UI scale 1) from which the detail panel is DOCKED beside the tree; below it the
    /// panel is an overlay DRAWER that opens over the tree's right edge when a node is selected (UR-4).</summary>
    public const double DockMinWidth = 1600;

    /// <summary>The UI scale the screen is set at (UR-1): every strip, card, gap and role size is × this.</summary>
    public double Scale
    {
        get => _scale;
        set { double v = Math.Clamp(value, 0.5, 4.0); if (v != _scale) { _scale = v; EnsureLayouts(); } }
    }
    private double _scale = 1.0;

    public double LensBarH => LensBarRef * _scale;
    public double TabBarH => TabBarRef * _scale;
    /// <summary>The sticky lane header: one row of lane chips.</summary>
    public double ColumnHeaderH => ColumnHeaderRef * _scale;
    public double ControlRowH => ControlRowRef * _scale;
    public double HeaderH => ColumnHeaderH + ControlRowH;
    public double DetailW => Math.Min(DetailRef * _scale, Math.Max(200, _w - 160 * _scale));

    /// <summary>Whether the detail panel is docked beside the tree (wide windows) rather than an overlay drawer.</summary>
    public bool Docked => _w >= DockMinWidth * _scale;

    /// <summary>Whether the overlay drawer is open (narrow windows: a node of the tree on show is selected).</summary>
    public bool DrawerOpen => !Docked && Lens == Lens.KnowledgeAndTechnology && Selected >= 0 && InTab(Selected);

    /// <summary>Whether a content node belongs to the tree on show.</summary>
    private bool InTab(int contentIndex) => Content.Nodes[contentIndex].Tree == (ResearchTree)((int)Tab + 1);

    /// <summary>The node the detail panel shows: the hovered card, else the selected node of the tree on show, or -1.</summary>
    public int DetailNode => Hovered >= 0 ? Hovered : Selected >= 0 && InTab(Selected) ? Selected : -1;

    /// <summary>The detail panel's rect: docked, the column right of the tree under the tab bar; as a drawer, the
    /// same width over the tree's right edge — left of the overview strip, under the lane header and control row (all
    /// of which stay usable).</summary>
    public RectD DetailRect => Docked
        ? new RectD(_w - DetailW, LensBarH + TabBarH, DetailW, Math.Max(100, _h - LensBarH - TabBarH))
        : new RectD(_w - (MinimapVisible ? StripW : 0) - DetailW, LensBarH + TabBarH + HeaderH, DetailW, Math.Max(100, _h - LensBarH - TabBarH - HeaderH));

    /// <summary>Closes the overlay drawer (deselects); false when there was none.</summary>
    public bool CloseDrawer()
    {
        if (!DrawerOpen) return false;
        Selected = -1;
        return true;
    }

    /// <summary>Whether a screen point on the canvas is covered by the overview strip or the open drawer (a card under
    /// it is neither hovered nor clicked).</summary>
    public bool Obscured(double x, double y) =>
        (MinimapVisible && MinimapRect().Contains(x, y)) || (DrawerOpen && DetailRect.Contains(x, y));

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
        TreeLayoutOptions o = TreeLayoutOptions.Scaled(_scale, TreeViewportWidth);
        Layouts = [ResearchTreeLayout.Compute(Graphs[0], o), ResearchTreeLayout.Compute(Graphs[1], o)];
        _collapsed = [new bool[Layouts[0].Lanes.Count], new bool[Layouts[1].Lanes.Count]];
        _layoutScale = _scale;
    }

    private readonly bool[][] _collapsed;
    private double _layoutScale;

    /// <summary>Width of the vertical overview strip docked at the canvas's right edge.</summary>
    public double StripW => StripRef * _scale;

    /// <summary>The width the tree must fit at zoom 1: the canvas (the window, less the docked detail panel) minus the
    /// overview strip. The layout is recomputed whenever this changes, so there is never horizontal overflow.</summary>
    public double TreeViewportWidth => Math.Max(100, Docked ? _w - DetailW : _w) - (MinimapVisible ? StripW : 0);

    private void EnsureLayouts()
    {
        if (Layouts is null) return;
        double vw = TreeViewportWidth;
        bool rescale = _layoutScale != _scale;
        for (int t = 0; t < 2; t++)
            if (rescale || Layouts[t].Options.ViewportWidth != vw)
                Layouts[t] = ResearchTreeLayout.Compute(Graphs[t], TreeLayoutOptions.Scaled(_scale, vw), _collapsed[t]);
        _layoutScale = _scale;
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
        ? new RectD(0, LensBarH + TabBarH + HeaderH, Math.Max(100, Docked ? _w - DetailW : _w), Math.Max(100, _h - LensBarH - TabBarH - HeaderH))
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

    /// <summary>Tracks the pointer: the card under it is HOVERED, and the chrome's controls answer hover (UR-4).</summary>
    public void PointerMove(double x, double y)
    {
        _pointer = (x, y);
        Hovered = NodeAt(x, y);
    }

    private (double X, double Y)? _pointer;

    /// <summary>Whether the pointer is over <paramref name="r"/> (a DrawList control's hover state).</summary>
    private bool Over(RectD r) => _pointer is (double px, double py) && r.Contains(px, py);

    /// <summary>Content index of the card under a screen point on the current tree, or -1.</summary>
    public int NodeAt(double x, double y)
    {
        if (Lens != Lens.KnowledgeAndTechnology) return -1;
        RectD c = Canvas;
        if (!c.Contains(x, y) || Obscured(x, y)) return -1;
        int v = Layout.HitTest(Camera.ToWorldX(x, c.X), Camera.ToWorldY(y, c.Y));
        return v < 0 ? -1 : Graph.Vertices[v].ContentIndex;
    }

    /// <summary>Pixels the view scrolls per wheel notch (× the UI scale).</summary>
    public const double WheelStepPx = 120;

    /// <summary>The wheel SCROLLS along the single axis (vertical); zoom is <see cref="WheelZoom"/>. Over the detail
    /// panel (docked or drawer) it scrolls the panel instead.</summary>
    public void Wheel(double x, double y, double notches)
    {
        if (Lens != Lens.KnowledgeAndTechnology) return;
        if ((Docked || DrawerOpen) && DetailRect.Contains(x, y)) { _detailScroll = Math.Max(0, _detailScroll - notches * WheelStepPx * 0.5 * _scale); return; }
        RectD c = Canvas;
        if (!c.Contains(x, y)) return;
        ScrollBy(0, -notches * WheelStepPx * _scale);
    }

    private double _detailScroll;
    private int _detailNode = int.MinValue;

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
        // Later-painted regions lie on top (the drawer over the tree): the last region under the point answers.
        for (int i = _hits.Count - 1; i >= 0; i--)
        {
            HitRegion h = _hits[i];
            if (!h.Rect.Contains(x, y)) continue;
            switch (h.Kind)
            {
                case HitKind.Panel: return ProgressionCommand.None;
                case HitKind.CloseDetail: Selected = -1; return ProgressionCommand.None;
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
        _info = [];
        PanelFrame.Field(d, new RectD(0, 0, width, height), Theme, 1);
        if (Snapshot is null) return d;

        if (Lens == Lens.KnowledgeAndTechnology)
        {
            FrameFirstTime();
            PaintTree(d, m);
            PaintTabBar(d, m);
            PaintDetail(d, m);
            PaintHoverTip(d, m);
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

    /// <summary>A hovered control: the raised material warmed a little more (the state table's +22 % accent).</summary>
    private Rgba HoverFill => Mix(Theme.Material.PanelRaised, Theme.Material.Accent, 0.22);

    // UR-4: every run is set by ROLE (TypeScale) × the UI scale, never by a literal; ThemeText.Write applies the era's
    // size scale on top (never below 1). Geometry — every rect and hit region — comes from constants × the UI scale and
    // the era-INVARIANT measure, so the regions are the same in every era (continuity).
    private double Px(TypeRole role, FontRole font = FontRole.Body) => TypeScale.Px(Theme, role, font) * _scale;
    private double Sp(double v) => v * _scale;
    private double Line(double designPx) => Theme.Type.Line(designPx);

    /// <summary>The era-invariant width estimate of a capitals label at the Caption role (geometry only): the unstyled
    /// run widened for the widest era's tracking and weight.</summary>
    private double CapsGeometryWidth(ITextMeasure m, string label) =>
        m.Width(label, TypeScale.Px(TypeRole.Caption, TypeFace.Garamond, caps: true) * _scale, FontRole.Caps) * 1.18;

    /// <summary>The design size at which the SET run fits <paramref name="width"/>: its own size when it fits, else
    /// smaller — but never below the Caption floor of the run's face (UR-4: no run is set below Caption).</summary>
    private double FitFloor(ITextMeasure m, string text, double design, double width, FontRole role = FontRole.Body)
    {
        EraTheme t = Theme;
        double floorDesign = TypeScale.Floor(t.Type.For(role).Face) * _scale / t.Type.SizeScale;
        if (design <= floorDesign) return design;
        double fit = ThemeText.FitSize(m, t, text, design, width, role, Math.Min(1.0, floorDesign / design));
        return Math.Max(floorDesign, fit);
    }

    /// <summary>Writes a run fitted to <paramref name="width"/> (shrunk to the floor, then ellipsised as a last resort).</summary>
    private void WriteFit(DrawList d, ITextMeasure m, double x, double y, string text, double design, double width, Rgba color,
        TextAlign align = TextAlign.Left, FontRole role = FontRole.Body)
    {
        double size = FitFloor(m, text, design, width, role);
        d.Write(Theme, x, y, ThemeText.Fit(m, Theme, text, size, width, role), size, color, align, role);
    }

    // The text inks of this theme (derived once per theme: TextInks darkens each pigment to its floor).
    private EraTheme? _inkTheme;
    private TextInks _ink = null!;
    private Rgba _availableInk, _completedInk, _lockedInk, _activeInk, _progressInk;

    /// <summary>The token floor of a card's state-line ink on the panel (UR-7, measured on the rendered frame).</summary>
    public const double StateInkFloor = 8.0;
    private TextInks Ink
    {
        get
        {
            if (!ReferenceEquals(_inkTheme, Theme))
            {
                _inkTheme = Theme;
                _ink = Theme.TextInk;
                // The state lines are the card's sentence at the body role in a regular weight: the family's ink
                // darkened to 8:1 on the panel (the 5.5:1 text inks measured ~3:1 as rendered, thin strokes on the
                // state fills) — dark, but still its family's hue; a locked card's reason in the body ink (its lock
                // mark is the non-colour cue).
                _availableInk = TextInks.Darken(Theme.Semantic.Available, Theme.Ink.Text, Theme.Material.Panel, StateInkFloor);
                _completedInk = TextInks.Darken(Theme.Semantic.Completed, Theme.Ink.Text, Theme.Material.Panel, StateInkFloor);
                _activeInk = TextInks.Darken(Theme.Semantic.Active, Theme.Ink.Text, Theme.Material.Panel, StateInkFloor);
                _progressInk = TextInks.Darken(Theme.Semantic.Progress, Theme.Ink.Text, Theme.Material.Panel, StateInkFloor);
                _lockedInk = Theme.Ink.Text;
            }
            return _ink;
        }
    }

    /// <summary>The lane's hue as a TEXT ink (a lane name is words, not a mark).</summary>
    private Rgba LaneInk(Rgba hue) => TextInks.For(hue, Theme);

    private void PaintLensBar(DrawList d, ITextMeasure m)
    {
        EraTheme t = Theme;
        double barH = LensBarH;
        PanelFrame.Paint(d, new RectD(0, 0, _w, barH), t, 101, FrameKind.Bar);
        double caps = Px(TypeRole.Caption, FontRole.Caps);
        d.Write(t, Sp(20), Sp(9), "PROGRESSION", caps, t.Ink.OnChromeAccent, TextAlign.Left, FontRole.Caps);
        d.Write(t, Sp(20), Sp(9) + Line(caps) + Sp(1), "Turn " + Sim.Ui.ViewModel.PlayerTurn.Current(Snapshot!.Turn).ToString(CultureInfo.InvariantCulture),
            Px(TypeRole.Heading, FontRole.Heading), t.Ink.OnChrome, TextAlign.Left, FontRole.Heading);

        // The research rate and the close button on the right.
        var close = new RectD(_w - Sp(54), (barH - Sp(38)) / 2, Sp(38), Sp(38));
        PanelFrame.Paint(d, close, t, 120, FrameKind.Button, Over(close) ? HoverFill : null, Over(close) ? t.Material.BorderStrong : null);
        EraMarks.Close(d, t, close, t.Ink.Text, 121);
        _hits.Add(new HitRegion(close, HitKind.Close, 0));
        double rx = close.X - Sp(14);
        d.Write(t, rx, Sp(6), Snapshot.PointsPerTurn.ToString("0.0", CultureInfo.InvariantCulture), Px(TypeRole.Kpi, FontRole.Numeric),
            t.Ink.OnChromeAccent, TextAlign.Right, FontRole.Numeric);
        d.Write(t, rx, Sp(6) + Line(Px(TypeRole.Kpi, FontRole.Numeric)) - Sp(4), "research / turn", Px(TypeRole.Caption), t.Ink.OnChromeSoft, TextAlign.Right);

        // Lens slots between the two blocks, geometry from the era-invariant measure (the same slots in every era).
        double x0 = Sp(150), right = _w - Sp(190);
        double sub = Px(TypeRole.Caption);
        var want = new double[Lenses.All.Count];
        var subs = new string[Lenses.All.Count];
        double total = 0;
        for (int i = 0; i < want.Length; i++)
        {
            Lens l = Lenses.All[i];
            LensStatus status = l == Lens.KnowledgeAndTechnology ? LensStatus.Functional
                : l == Lens.Industry ? LensStatus.NotYetSimulated : LensStatus.PartialData;
            subs[i] = status switch { LensStatus.Functional => "simulated", LensStatus.PartialData => "partial", _ => "not yet simulated" };
            want[i] = Math.Max(CapsGeometryWidth(m, Lenses.Label(l)), m.Width(subs[i], TypeScale.Px(TypeRole.Caption, TypeFace.Garamond) * _scale, FontRole.Body) * 1.08) + Sp(24);
            total += want[i];
        }
        // Too narrow for every label and its status word: the status words go (each lens page states its status).
        bool compact = total > right - x0;
        if (compact)
        {
            total = 0;
            for (int i = 0; i < want.Length; i++) { want[i] = CapsGeometryWidth(m, Lenses.Label(Lenses.All[i])) + Sp(18); total += want[i]; }
        }
        double extra = Math.Max(0, (right - x0 - total) / want.Length);
        double squeeze = total > right - x0 ? (right - x0) / total : 1.0;
        double lx0 = x0;
        for (int i = 0; i < Lenses.All.Count; i++)
        {
            Lens l = Lenses.All[i];
            double slotW = want[i] * squeeze + extra;
            var r = new RectD(lx0 + Sp(3), Sp(7), slotW - Sp(6), barH - Sp(14));
            lx0 += slotW;
            bool on = l == Lens;
            bool hover = !on && Over(r);
            bool notYet = l == Lens.Industry;
            if (on)
            {
                PanelFrame.Paint(d, r, t, 110 + i, FrameKind.Button, SelectedFill, t.Material.Accent, 1.1);
                d.Rect(new RectD(r.X + Sp(10), r.Bottom - Sp(4), r.W - Sp(20), Sp(3)), t.Material.Accent);
            }
            else if (hover) PanelFrame.Paint(d, r, t, 110 + i, FrameKind.Button, Mix(t.Material.Chrome, t.Material.Accent, 0.25), t.Material.AccentSoft, 0.9);
            Rgba col = on ? t.Ink.Text : notYet ? t.Ink.OnChromeSoft : t.Ink.OnChrome;
            double ly = compact ? r.Y + (r.H - t.Type.Size(caps)) / 2 - Sp(2) : r.Y + Sp(5);
            WriteFit(d, m, r.CenterX, ly, Lenses.Label(l), caps, r.W - Sp(8), col, TextAlign.Center, FontRole.Caps);
            if (!compact)
                WriteFit(d, m, r.CenterX, r.Y + Sp(5) + Line(caps), subs[i], sub, r.W - Sp(8), on ? t.Ink.TextSoft : t.Ink.OnChromeSoft, TextAlign.Center);
            _hits.Add(new HitRegion(r, HitKind.Lens, (int)l));
        }
    }

    private void PaintTabBar(DrawList d, ITextMeasure m)
    {
        EraTheme t = Theme;
        double y = LensBarH, barH = TabBarH;
        PanelFrame.Paint(d, new RectD(0, y, _w, barH), t, 102, FrameKind.Bar);
        ResearchSnapshot s = Snapshot!;
        string[] names = ["TECHNOLOGY", "CIVICS"];
        string[] counts = [s.CompletedTechnology + " / " + Content.TechnologyCount, s.CompletedCivics + " / " + Content.CivicsCount];
        double tabW = Sp(226), tabH = barH - Sp(12);
        double heading = Px(TypeRole.Heading, FontRole.Caps), data = Px(TypeRole.Data, FontRole.Numeric);
        for (int i = 0; i < 2; i++)
        {
            var r = new RectD(Sp(14) + i * (tabW + Sp(8)), y + Sp(6), tabW, tabH);
            bool on = (int)Tab == i;
            bool hover = !on && Over(r);
            PanelFrame.Paint(d, r, t, 130 + i, FrameKind.Button, on ? SelectedFill : hover ? Mix(t.Material.Chrome, t.Material.Accent, 0.25) : t.Material.Chrome,
                on ? t.Material.Accent : hover ? t.Material.AccentSoft : t.Ink.OnChromeSoft, on ? 1.1 : 0.8);
            if (on) d.Rect(new RectD(r.X + Sp(10), r.Bottom - Sp(4), r.W - Sp(20), Sp(3)), t.Material.Accent);
            double cw = m.Width(t, counts[i], data, FontRole.Numeric);
            WriteFit(d, m, r.X + Sp(14), r.Y + (r.H - t.Type.Size(heading)) / 2 - Sp(2), names[i], heading, r.W - cw - Sp(36), on ? t.Ink.Text : t.Ink.OnChrome, TextAlign.Left, FontRole.Caps);
            d.Write(t, r.Right - Sp(12), r.Y + (r.H - t.Type.Size(data)) / 2 - Sp(2), counts[i], data, on ? t.Ink.Text : t.Ink.OnChromeSoft, TextAlign.Right, FontRole.Numeric);
            _hits.Add(new HitRegion(r, HitKind.Tab, i));
        }

        // The current target capsule, the Age chip and (where there is room) the state legend, left to right.
        double cx = Sp(14) + 2 * tabW + Sp(8) + Sp(16);
        double rest = _w - cx - Sp(14);
        bool ageChip = Age is { State: not Sim.Ui.Ages.AgePanelState.NoContent };
        double capW = Math.Min(Sp(470), ageChip ? rest * 0.56 : rest);
        double chipW = ageChip ? Math.Min(Sp(400), rest - capW - Sp(12)) : 0;
        double legendW = Sp(330);
        bool legend = rest - capW - (ageChip ? chipW + Sp(12) : 0) - Sp(16) >= legendW;
        var cap = new RectD(cx, y + Sp(5), capW, barH - Sp(10));
        if (cap.W > Sp(160)) PaintCapsule(d, m, cap);
        if (ageChip && chipW >= Sp(180)) PaintAgeChip(d, m, new RectD(cap.Right + Sp(12), y + Sp(5), chipW, barH - Sp(10)));
        if (!legend) return;

        // Legend: the four card states, as cards of this era, right-aligned.
        (string, Rgba, Rgba)[] keys = [("Known", t.Semantic.CompletedFill, t.Semantic.Completed), ("Target", t.Semantic.ActiveFill, t.Semantic.Active),
            ("Available", t.Semantic.AvailableFill, t.Semantic.Available), ("Locked", t.Semantic.LockedFill, t.Semantic.Locked)];
        double cap0 = Px(TypeRole.Caption);
        double lw = 0;
        foreach ((string label, _, _) in keys) lw += Sp(22) + m.Width(t, label, cap0) + Sp(14);
        double lx = _w - Sp(14) - lw;
        int k = 0;
        foreach ((string label, Rgba fill, Rgba edge) in keys)
        {
            PanelFrame.Paint(d, new RectD(lx, y + (barH - Sp(16)) / 2, Sp(18), Sp(16)), t, 150 + k++, FrameKind.Chip, fill, edge, 1.4);
            d.Write(t, lx + Sp(22), y + (barH - t.Type.Size(cap0)) / 2 - Sp(2), label, cap0, t.Ink.OnChromeSoft);
            lx += Sp(22) + m.Width(t, label, cap0) + Sp(14);
        }
    }

    /// <summary>The research capsule: the target with its progress, or — the turn-1 call to action — a warning that
    /// research points are idle, in the progress family's TEXT ink with a mark (it read as disabled at 2.41:1).</summary>
    private void PaintCapsule(DrawList d, ITextMeasure m, RectD cap)
    {
        EraTheme t = Theme;
        ResearchSnapshot s = Snapshot!;
        double caps = Px(TypeRole.Caption, FontRole.Caps), body = Px(TypeRole.Body, FontRole.Heading);
        if (s.TargetIndex is int ti)
        {
            PanelFrame.Paint(d, cap, t, 140, FrameKind.Chip, t.Semantic.ActiveFill, A(t.Semantic.Active, 0.8), 1.0);
            ResearchNodeView v = s.Nodes[ti];
            d.Write(t, cap.X + Sp(12), cap.Y + Sp(3), "RESEARCHING", caps, Ink.Active, TextAlign.Left, FontRole.Caps);
            string pct = Pct(v.Fraction);
            double data = Px(TypeRole.Data, FontRole.Numeric);
            double pw = m.Width(t, pct, data, FontRole.Numeric);
            WriteFit(d, m, cap.X + Sp(12), cap.Y + Sp(3) + Line(caps) - Sp(2), v.Name, body, cap.W - Sp(24) - Sp(96) - pw, t.Ink.Text, TextAlign.Left, FontRole.Heading);
            EraMarks.Progress(d, t, new RectD(cap.Right - Sp(18) - pw - Sp(82), cap.CenterY - Sp(4), Sp(76), Sp(8)), v.Fraction, t.Semantic.Active, 141);
            d.Write(t, cap.Right - Sp(12), cap.Y + (cap.H - t.Type.Size(data)) / 2 - Sp(2), pct, data, t.Ink.Text, TextAlign.Right, FontRole.Numeric);
            return;
        }
        bool ordered = PendingTarget >= 0;
        PanelFrame.Paint(d, cap, t, 140, FrameKind.Chip, ordered ? t.Semantic.ActiveFill : t.Material.PanelRaised,
            ordered ? A(t.Semantic.Active, 0.8) : t.Semantic.Progress, ordered ? 1.0 : 1.6);
        double bodySize = Px(TypeRole.Body);
        double ty = cap.Y + (cap.H - t.Type.Size(bodySize)) / 2 - Sp(3);
        if (ordered)
        {
            WriteFit(d, m, cap.X + Sp(12), ty, "Target ordered: " + s.Nodes[PendingTarget].Name + " - applies at End Turn", bodySize, cap.W - Sp(24), t.Ink.Text);
            return;
        }
        // "!" in a ring, the warning mark, then the sentence in the body ink.
        double r0 = Sp(10), mx = cap.X + Sp(12) + r0;
        d.Circle(mx, cap.CenterY, r0, Ink.Progress, null);
        d.Write(t, mx, cap.CenterY - t.Type.Size(Px(TypeRole.Caption, FontRole.Heading)) * 0.62, "!", Px(TypeRole.Caption, FontRole.Heading), t.Material.Panel, TextAlign.Center, FontRole.Heading);
        WriteFit(d, m, mx + r0 + Sp(10), ty, "Research idle - choose an available node", bodySize, cap.Right - Sp(12) - (mx + r0 + Sp(10)), t.Ink.Text);
    }

    /// <summary>The Age header chip: current Age, progress toward the next, and its state; a click opens
    /// the Age surface (milestones, Advance Age). Read from <see cref="Age"/> (AgeQuery), never invented.</summary>
    private void PaintAgeChip(DrawList d, ITextMeasure m, RectD r)
    {
        if (Age is not { } a) return;
        EraTheme t = Theme;
        bool eligible = a.State == Sim.Ui.Ages.AgePanelState.Eligible;
        bool pending = a.State == Sim.Ui.Ages.AgePanelState.Pending;
        bool hover = Over(r);
        PanelFrame.Paint(d, r, t, 145, FrameKind.Chip, eligible ? Mix(t.Material.PanelRaised, t.Material.Accent, hover ? 0.28 : 0.18) : hover ? HoverFill : t.Material.PanelRaised,
            eligible ? t.Material.Accent : pending ? t.Semantic.Active : hover ? t.Material.BorderStrong : t.Material.AccentSoft, eligible ? 1.8 : 1.0);
        double caps = Px(TypeRole.Caption, FontRole.Caps);
        string numeral = "AGE " + Sim.Ui.Ages.AgePanelModel.Numeral(a.CurrentAge);
        d.Write(t, r.X + Sp(12), r.Y + Sp(3), numeral, caps, Ink.Accent, TextAlign.Left, FontRole.Caps);
        string right = a.State switch
        {
            Sim.Ui.Ages.AgePanelState.FinalAge => "final Age",
            Sim.Ui.Ages.AgePanelState.Eligible => "ADVANCE AGE available",
            Sim.Ui.Ages.AgePanelState.Pending => "advancing next turn",
            _ => "next: core " + a.CoreMet + "/" + a.CoreTotal + " - supp. " + a.SupportingMet + "/" + a.SupportingRequired,
        };
        FontRole role = eligible ? FontRole.Caps : FontRole.Body;
        double rs = eligible ? caps : Px(TypeRole.Caption);
        double nw = m.Width(t, numeral, caps, FontRole.Caps);
        WriteFit(d, m, r.Right - Sp(12), r.Y + Sp(3), right, rs, r.W - Sp(36) - nw, eligible ? Ink.Accent : pending ? Ink.Active : t.Ink.TextSoft, TextAlign.Right, role);
        WriteFit(d, m, r.X + Sp(12), r.Y + Sp(3) + Line(caps) - Sp(2), a.CurrentAgeName, Px(TypeRole.Body, FontRole.Heading), r.W - Sp(24), t.Ink.Text, TextAlign.Left, FontRole.Heading);
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
        double caps = Px(TypeRole.Caption, FontRole.Caps) * z, capt = Px(TypeRole.Caption) * z;
        bool labels = Px(TypeRole.Caption) * z >= TypeScale.Floor(t.Type.Body.Face) * _scale * 0.7;

        d.PushClip(c);
        // Horizontal tier bands: alternate shading and a label strip with the full Age names.
        foreach (TierBand tb in L.Tiers)
        {
            double y0 = SY(tb.Y0), y1 = SY(tb.Y1);
            if (y1 < c.Y || y0 > c.Bottom) continue;
            if (tb.Tier % 2 == 1) d.Rect(new RectD(tx0, y0, tx1 - tx0, y1 - y0), A(t.Material.FieldAlt, 0.55));
            d.Line(tx0, y0 + 0.5, tx1, y0 + 0.5, A(t.Material.Hairline, 0.7), 1);
            if (labels)
            {
                string label = "TIER " + (tb.Tier + 1).ToString(CultureInfo.InvariantCulture);
                double lx = SX(o.Margin + o.Spine);
                d.Write(t, lx, y0 + Sp(9) * z, label, caps, TextInks.Darken(t.Material.Accent, t.Ink.Text, t.Material.Field, TextInks.Floor), TextAlign.Left, FontRole.Caps);
                // The tier's Ages in full, at the body role: the cards below show only the numeral (UR-4).
                string ages = ResearchTreeLayout.AgeRangeLabel((tb.Lo, tb.Hi));
                double ax = lx + m.Width(t, label, caps, FontRole.Caps) + Sp(14) * z;
                double body = Px(TypeRole.Body) * z;
                // (The body ink: it sits on the darker field, where the soft ink measured 2.6:1 as rendered.)
                if (ages.Length > 0) WriteFit(d, m, ax, y0 + Sp(5) * z - (t.Type.Size(body) - t.Type.Size(caps)) * 0.6, ages, body, Math.Max(Sp(40), tx1 - ax - Sp(12)), t.Ink.Text);
            }
        }
        // Lane segments: each lane's share of a tier (or of one of its wrapped lane rows), tinted by its hue with a
        // coloured cap and its name, so a lane reads as one colour from tier to tier while lanes keep their order.
        // From the Bronze era on, the cap fills with the cell's completed share: accumulated knowledge, at a glance.
        double[][] completion = t.Density.CardDetail >= 3 ? LaneTierCompletion() : [];
        foreach (LaneSegment sg in L.Segments)
        {
            double y0 = SY(sg.Y0 - Sp(12)), y1 = SY(sg.Y1 - o.RowGap / 2);
            if (y1 < c.Y || SY(sg.Y0 - Sp(40)) > c.Bottom) continue;
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
            if (labels)
                WriteFit(d, m, x0 + Sp(8) * z, SY(sg.Y0 - Sp(40)), lane.Name, Px(TypeRole.Body, FontRole.Heading) * z, w - Sp(12) * z,
                    TextInks.Darken(hue, t.Ink.Text, t.Material.Field, TextInks.Floor), TextAlign.Left, FontRole.Heading);   // on the field, not the panel
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

        // Cards first. With a focus, the unrelated cards RECEDE — their fill and frame fade a quarter toward the field
        // — but their words keep their ink (UR-2: a veil may dim fills and frames, never text). Related cards are
        // outlined in the prerequisite / dependent colour.
        int hoveredV = Hovered >= 0 ? Graph.VertexOf(Hovered) : -1;
        foreach (int v in VisibleVertices())
        {
            PlacedVertex p = L.Placed[v];
            if (p.Hidden) continue;
            double x = SX(p.X), y = SY(p.Y);
            bool recede = focusV >= 0 && v != focusV && related[v] == 0;
            PaintCard(d, m, v, x, y, z, v == hoveredV, recede);
            if (focusV < 0 || v == focusV || recede) continue;
            var r = new RectD(x - 2, y - 2, p.W * z + 4, p.H * z + 4);
            d.Polyline(PanelFrame.Outline(r, t, CardId(v), FrameKind.Card), related[v] == 1 ? t.Semantic.Prerequisite : t.Semantic.Dependent,
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
        double gap = Sp(6);
        double chipW = (c.W - Sp(24) - gap * (L.Lanes.Count - 1)) / L.Lanes.Count;
        foreach (LaneBox lane in L.Lanes)
            LaneChip(d, m, lane, c.X + Sp(12) + lane.Index * (chipW + gap), hy + (ColumnHeaderH - Sp(30)) / 2, chipW);
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
        double chipH = Sp(30);
        Rgba hue = t.Semantic.Lanes.Of(lane.Id);
        var chip = new RectD(x, y, w, chipH);
        bool hover = Over(chip);
        PanelFrame.Paint(d, chip, t, 200 + lane.Index, FrameKind.Chip, hover ? HoverFill : t.Material.PanelRaised, A(hue, hover ? 1.0 : 0.85), hover ? 1.4 : 1.0);
        // Disclosure triangle: right when collapsed, down when expanded.
        double tx = chip.X + Sp(12), ty = chip.Y + chipH / 2;
        if (lane.Collapsed) d.Polygon([(tx - Sp(3), ty - Sp(5)), (tx + Sp(4), ty), (tx - Sp(3), ty + Sp(5))], hue);
        else d.Polygon([(tx - Sp(5), ty - Sp(3)), (tx + Sp(5), ty - Sp(3)), (tx, ty + Sp(4))], hue);
        // A navigation label (UR-4: mixed case at the caption role in the lane's TEXT ink): the full name, shrinking
        // to the floor rather than losing a word; the count only where it fits beside it.
        string count = lane.Collapsed ? lane.NodeCount.ToString(CultureInfo.InvariantCulture) + " hidden"
            : t.Density.CardDetail >= 4 && !lane.External ? LaneDone(lane.Index).ToString(CultureInfo.InvariantCulture) + "/" + lane.NodeCount.ToString(CultureInfo.InvariantCulture)
            : lane.NodeCount.ToString(CultureInfo.InvariantCulture);
        double ns = Px(TypeRole.Caption, FontRole.Heading), cs = Px(TypeRole.Caption, FontRole.Numeric);
        double cw = m.Width(t, count, cs, FontRole.Numeric);
        double nameW = m.Width(t, lane.Name, ns, FontRole.Heading);
        bool showCount = nameW + cw + Sp(40) <= w;
        double ty0 = chip.Y + (chipH - t.Type.Size(ns)) / 2 - Sp(2);
        WriteFit(d, m, chip.X + Sp(22), ty0, lane.Name, ns, w - Sp(28) - (showCount ? cw + Sp(10) : 0), LaneInk(hue), TextAlign.Left, FontRole.Heading);
        if (showCount) d.Write(t, chip.Right - Sp(8), chip.Y + (chipH - t.Type.Size(cs)) / 2 - Sp(2), count, cs, t.Ink.TextSoft, TextAlign.Right, FontRole.Numeric);
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
        double caps = Px(TypeRole.Caption, FontRole.Caps), capt = Px(TypeRole.Caption);
        double x = c.Right - Sp(12), h = Sp(32), y = row.Y + (ControlRowH - h) / 2;
        for (int i = buttons.Length - 1; i >= 0; i--)
        {
            double w = CapsGeometryWidth(m, buttons[i].Label) + Sp(22);   // era-invariant geometry
            x -= w;
            var r = new RectD(x, y, w, h);
            bool primary = buttons[i].Kind == HitKind.Frontier;
            bool hover = Over(r);
            Rgba fill = primary ? (hover ? Mix(t.Semantic.ActiveFill, t.Semantic.Active, 0.22) : t.Semantic.ActiveFill)
                : hover ? HoverFill : buttons[i].On ? SelectedFill : t.Material.PanelRaised;
            PanelFrame.Paint(d, r, t, 170 + i, FrameKind.Button, fill,
                primary ? t.Semantic.Active : hover ? t.Material.BorderStrong : buttons[i].On ? t.Material.AccentSoft : t.Material.Hairline, primary ? 1.3 : 0.9);
            WriteFit(d, m, r.X + w / 2, r.Y + (h - t.Type.Size(caps)) / 2 - Sp(2), buttons[i].Label, caps, w - Sp(8), primary ? Ink.Active : t.Ink.Text, TextAlign.Center, FontRole.Caps);
            _hits.Add(new HitRegion(r, buttons[i].Kind, 0));
            x -= Sp(6);
        }
        // Current tier at the top of the view, with its full Age names, on the left.
        TreeLayout L = Layout;
        int tier = L.TierAt(Camera.ToWorldY(c.Y + 1, c.Y));
        TierBand band = L.Tiers[tier];
        string head = "TIER " + (tier + 1).ToString(CultureInfo.InvariantCulture) + " of " + L.Columns.ToString(CultureInfo.InvariantCulture);
        double ty = row.Y + (ControlRowH - t.Type.Size(caps)) / 2 - Sp(2);
        d.Write(t, c.X + Sp(12), ty, head, caps, t.Ink.OnChromeAccent, TextAlign.Left, FontRole.Caps);
        double hx = c.X + Sp(24) + m.Width(t, head, caps, FontRole.Caps);
        // Legend for the highlight (just left of the buttons), where the row has room for it beside the tier's Ages.
        (string Label, Rgba Col, bool Dash)[] keys = [("requires", Mix(t.Semantic.Prerequisite, t.Ink.OnChrome, 0.3), false),
            ("leads to", Mix(t.Semantic.Dependent, t.Ink.OnChrome, 0.4), false), ("one of", t.Ink.OnChromeSoft, true)];
        double legendW = 0;
        foreach ((string label, _, _) in keys) legendW += m.Width(t, label, capt) + Sp(34);
        double lx = x - Sp(8);
        if (lx - legendW - hx >= Sp(260))
        {
            for (int i = keys.Length - 1; i >= 0; i--)
            {
                double tw = m.Width(t, keys[i].Label, capt);
                lx -= tw + Sp(34);
                d.Line(lx, row.CenterY, lx + Sp(22), row.CenterY, keys[i].Col, Sp(2), keys[i].Dash ? (Sp(4), Sp(3)) : null);
                d.Write(t, lx + Sp(27), row.Y + (ControlRowH - t.Type.Size(capt)) / 2 - Sp(2), keys[i].Label, capt, t.Ink.OnChromeSoft);
            }
        }
        WriteFit(d, m, hx, row.Y + (ControlRowH - t.Type.Size(capt)) / 2 - Sp(2), ResearchTreeLayout.AgeRangeLabel((band.Lo, band.Hi)), capt,
            Math.Max(0, lx - hx - Sp(16)), t.Ink.OnChromeSoft);
    }

    /// <summary>The share a receding card's fill and frame fade toward the field while another node has the focus.</summary>
    public const double RecedeFade = 0.25;

    /// <summary>The card's state line (UR-4): the one sentence a card owes the player — why it is locked ("Needs
    /// Controlled fire"), how long it would take ("Available · ~9 turns"), how far it is ("Researching · 13% · ~7
    /// turns"), or that it is known. Read from the snapshot (ResearchQuery), never invented.</summary>
    public string StateLine(int contentIndex)
    {
        ResearchSnapshot s = Snapshot!;
        ResearchNodeView v = s.Nodes[contentIndex];
        string Turns(double remaining) => s.PointsPerTurn > 0
            ? "~" + Math.Ceiling(Math.Max(0, remaining) / s.PointsPerTurn).ToString("0", CultureInfo.InvariantCulture) + " turns" : "";
        static string Join(params string[] parts)
        {
            var kept = new List<string>(parts.Length);
            foreach (string part in parts) if (part.Length > 0) kept.Add(part);
            return string.Join(" · ", kept);
        }
        switch (v.State)
        {
            case NodeState.Completed: return "Known";
            case NodeState.CurrentTarget: return Join("Researching", Pct(v.Fraction), Turns(v.EffectiveCost - v.Progress));
            case NodeState.Available:
                return PendingTarget == contentIndex ? "Ordered - applies at End Turn" : Join("Available", Turns(v.EffectiveCost - v.Progress));
        }
        string why = LockLine(contentIndex);
        return v.State == NodeState.Partial ? Join(Pct(v.Fraction) + " kept", why) : why;
    }

    /// <summary>Why a node is locked, in a few words: the first missing prerequisite (and where it lives, when it is
    /// in another lane or the other tree), the research stage, or the subtree a recursive node waits for.</summary>
    public string LockLine(int contentIndex)
    {
        ResearchNodeView v = Snapshot!.Nodes[contentIndex];
        if (v.Lock.HasFlag(LockReason.MissingPrerequisites))
        {
            PrereqView? first = null;
            int missing = 0;
            bool anyAndMissing = false;
            foreach (PrereqView p in v.Prerequisites) if (!p.Completed && p.Kind == EdgeKind.And) anyAndMissing = true;
            foreach (PrereqView p in v.Prerequisites)
            {
                if (p.Completed || (anyAndMissing && p.Kind != EdgeKind.And)) continue;
                first ??= p;
                missing++;
            }
            if (first is not null)
            {
                string where = "";
                int pv = Graph.VertexOf(first.ContentIndex), nv = Graph.VertexOf(contentIndex);
                if (pv < 0 || (nv >= 0 && Layout.Placed[pv].Lane != Layout.Placed[nv].Lane))
                    where = pv < 0 || Graph.Vertices[pv].External ? (Graph.Tree == ResearchTree.Civics ? " (Technology)" : " (Civics)")
                        : " (" + Layout.Lanes[Layout.Placed[pv].Lane].Name + ")";
                string lead = anyAndMissing ? "Needs " : "Needs one of: ";
                return lead + first.Name + where + (missing > 1 ? (anyAndMissing ? " +" + (missing - 1).ToString(CultureInfo.InvariantCulture) : " / ...") : "");
            }
        }
        if (v.Lock.HasFlag(LockReason.ResearchStage)) return "Needs university";
        if (v.Lock.HasFlag(LockReason.SubtreeNotExhausted)) return "Waits for its whole subtree";
        return "Locked";
    }

    /// <summary>The state line's TEXT ink: the state's family, darkened to its floor (never the pigment).</summary>
    private Rgba StateInk(NodeState state)
    {
        _ = Ink;
        return state switch
        {
            NodeState.Completed => _completedInk,
            NodeState.CurrentTarget => _activeInk,
            NodeState.Available => _availableInk,
            NodeState.Partial => _progressInk,
            _ => _lockedInk,
        };
    }

    /// <summary>The widths a card's name may take (UR-4): its first line beside the state mark, its second line under
    /// it — for a card <paramref name="cardW"/> wide (world px) at UI scale <paramref name="scale"/> and zoom <paramref name="z"/>.</summary>
    public static (double W1, double W2) NameWidths(EraTheme t, double cardW, double scale, double z = 1.0)
    {
        double ins = PanelFrame.ContentInset(t, FrameKind.Card) * Math.Min(1.0, z);
        double pad = ins + 11 * scale * z, iconR = 7.5 * scale * z;
        return (cardW * z - ins - 2 * iconR - 8 * scale * z - pad, cardW * z - ins - 4 * scale * z - pad);
    }

    /// <summary>Splits a name into at most two lines: as many words as fit <paramref name="w1"/> on the first (beside
    /// the state mark), the rest on the second (<paramref name="w2"/>). A name too long even for two lines keeps every
    /// word but the last line is ellipsised — no shipped name needs it at the card's minimum width (pinned).</summary>
    public static (string First, string Second) TwoLines(ITextMeasure m, EraTheme t, string name, double size, double w1, double w2, FontRole role)
    {
        if (m.Width(t, name, size, role) <= w1) return (name, "");
        // Break opportunities: after a space (dropped) or after a hyphen (kept) — "Basic (Gilchrist-" / "Thomas) process".
        var breaks = new List<int>();
        for (int i = 0; i < name.Length - 1; i++) if (name[i] == ' ' || name[i] == '-') breaks.Add(i);
        string first = "", second = name;
        foreach (int b in breaks)
        {
            string cand = name[..(name[b] == '-' ? b + 1 : b)];
            if (m.Width(t, cand, size, role) > w1) break;
            first = cand;
            second = name[(b + 1)..];
        }
        if (first.Length == 0)
        {
            // No break fits beside the mark: the first word alone on the first line.
            int b = breaks.Count > 0 ? breaks[0] : name.Length;
            first = name[..Math.Min(name.Length, name[Math.Min(b, name.Length - 1)] == '-' ? b + 1 : b)];
            second = b < name.Length ? name[(b + 1)..] : "";
        }
        // Prefer the split whose second line fits when the greedy one does not (a shorter first line moves words down).
        if (m.Width(t, second, size, role) > w2)
            foreach (int b in breaks)
            {
                string f = name[..(name[b] == '-' ? b + 1 : b)], s2 = name[(b + 1)..];
                if (m.Width(t, f, size, role) <= w1 && m.Width(t, s2, size, role) <= w2) { first = f; second = s2; }
            }
        return (first, second);
    }

    /// <summary>
    /// A RESEARCH CARD in the era's hand (UR-4). Three rows the player reads, at sizes from the type scale: the NAME at
    /// the body role in the heading face, whole, on up to two lines; the COST and the Age numeral at the data role; and
    /// the STATE LINE at the body role in the state's text ink. The state is also its fill (Known, Target, Available
    /// and Locked are distinct surfaces), its inner rule and its mark. Density adds the rest: Eureka pips (A2+), the
    /// prerequisite/dependent stubs (A3+), discounts (A5+). The Age's full name is the tier strip's and the detail
    /// panel's (and the hover tip's in a narrow window); the card keeps the numeral beside the cost.
    /// </summary>
    private void PaintCard(DrawList d, ITextMeasure m, int vertex, double x, double y, double z, bool hovered, bool recede = false)
    {
        EraTheme t = Theme;
        SemanticTokens s = t.Semantic;
        GraphVertex gv = Graph.Vertices[vertex];
        ResearchNodeView v = Snapshot!.Nodes[gv.ContentIndex];
        PlacedVertex p = Layout.Placed[vertex];
        double w = p.W * z, h = p.H * z;
        bool selected = gv.ContentIndex == Selected;
        // Hover lifts the card a little (the state table's 2 px lift); the hit rect stays where it is.
        if (hovered && !selected) y -= Sp(2) * Math.Min(1.0, z);
        var r = new RectD(x, y, w, h);
        int id = CardId(vertex);
        InfoCardHit(r, v);   // M5 polish: Shift+click a card for its info card

        if (gv.External)
        {
            bool done = v.State == NodeState.Completed;
            PanelFrame.Paint(d, r, t, id, FrameKind.Chip, done ? s.CompletedFill : t.Material.PanelSunken, done ? s.Completed : hovered ? t.Material.BorderStrong : t.Material.Hairline, hovered ? 1.4 : 0.9);
            if (Px(TypeRole.Body) * z >= TypeScale.Floor(t.Type.Body.Face) * _scale * 0.7)
            {
                double size = Px(TypeRole.Body) * z;
                double fs = FitFloor(m, v.Name, size, w - Sp(16) * z);
                d.Write(t, x + w / 2, y + (h - t.Type.Size(fs)) / 2 - Sp(2) * z, ThemeText.Fit(m, t, v.Name, fs, w - Sp(16) * z), fs, done ? t.Ink.Text : t.Ink.TextSoft, TextAlign.Center);
            }
            return;
        }

        Rgba hue = ProgressionPalette.BranchOf(t, Content, Content.Nodes[gv.ContentIndex]);
        // THE STATE TABLE: the card is the era's material with the era's border (its identity across eras); the STATE
        // is its fill, an inner rule in the state's colour, its mark and its state line (constant meanings).
        (Rgba fill, Rgba? inner, double iw) = v.State switch
        {
            NodeState.Completed => (s.CompletedFill, (Rgba?)s.Completed, 1.4),
            NodeState.CurrentTarget => (s.ActiveFill, s.Active, 2.4),
            NodeState.Available => (s.AvailableFill, s.Available, 1.8),
            NodeState.Partial => (s.LockedFill, s.Progress, 1.4),
            _ => (s.LockedFill, null, 0.0),
        };
        if (v.State == NodeState.CurrentTarget)
            d.Rect(new RectD(x - 5 * z, y - 5 * z, w + 10 * z, h + 10 * z), A(s.Active, 0.18), null, 0, CornerRadius(z) + 4 * z);
        if (PendingTarget == gv.ContentIndex)
            d.Polyline(PanelFrame.Outline(new RectD(x - 4 * z, y - 4 * z, w + 8 * z, h + 8 * z), t, id, FrameKind.Card), s.Active, 1.2, closed: true);
        Rgba border = v.State == NodeState.Locked ? A(t.Material.Border, 0.55) : t.Material.Border;
        bool hairline = t.Edge.Corner == CornerStyle.Fine;   // the modern card: one hairline, in the state's colour
        if (hairline && inner is Rgba hc && !(selected || hovered)) border = hc;
        if (recede)
        {
            fill = ThemeColor.Mix(fill, t.Material.Field, RecedeFade);
            border = A(border, border.A / 255.0 * (1.0 - RecedeFade * 1.6));
            if (inner is Rgba ri) inner = A(ri, 1.0 - RecedeFade * 1.6);
        }
        // Selected: the strongest border (2.5 px); hovered: the strong border, one step lighter.
        PanelFrame.Paint(d, r, t, id, FrameKind.Card, fill, selected || hovered ? t.Material.BorderStrong : border,
            selected ? 2.5 : hovered ? 1.6 : hairline && inner is not null ? 1.6 : v.State == NodeState.Locked ? 0.8 : 1.0);
        if (inner is Rgba ic && !hairline)
        {
            double ii = (t.Edge.BorderPx + (t.Edge.DoubleRule ? 3.6 : 1.6)) * Math.Min(1.0, z);
            d.Polyline(PanelFrame.Outline(r.Inset(ii), t, id, FrameKind.Card), ic, Math.Max(1.0, iw * Math.Min(1.0, z)), closed: true);
        }

        // The lane stripe, inside the frame.
        double ins = PanelFrame.ContentInset(t, FrameKind.Card) * Math.Min(1.0, z);
        var stripe = new RectD(x + ins, y + ins + 2 * z, Math.Max(2.5, Sp(4) * z), h - 2 * ins - 4 * z);
        Rgba stripeCol = v.State == NodeState.Locked ? A(hue, 0.45) : hue;
        if (t.Edge.Corner == CornerStyle.Organic)
            d.Polyline(PanelFrame.Freehand(stripe.CenterX, stripe.Y, stripe.CenterX, stripe.Bottom, 1.0 * z, id, 7), stripeCol, Math.Max(2.5, Sp(4.2) * z));
        else d.Rect(stripe, stripeCol);

        if (z < 0.28) return;   // level of detail: colour only when far out
        bool dim = v.State == NodeState.Locked;
        double pad = ins + Sp(11) * z;
        double iconR = Sp(7.5) * z;
        double right = x + w - ins;
        double nameSize = Px(TypeRole.Body, FontRole.Heading) * z;
        double lead = t.Type.Size(nameSize) * 1.0;   // a tight leading: the two lines are one name
        double top = y + ins + Sp(2) * z;
        (double w1, double w2) = NameWidths(t, p.W, _scale, z);
        (string l1, string l2) = TwoLines(m, t, v.Name, nameSize, w1, w2, FontRole.Heading);
        Rgba nameInk = dim ? t.Ink.TextSoft : t.Ink.Text;
        d.Write(t, x + pad, top, l1, nameSize, nameInk, TextAlign.Left, FontRole.Heading);
        if (l2.Length > 0)
            d.Write(t, x + pad, top + lead, ThemeText.Fit(m, t, l2, nameSize, w2, FontRole.Heading), nameSize, nameInk, TextAlign.Left, FontRole.Heading);

        // State mark, top right.
        MarkKind kind = v.State switch
        {
            NodeState.Completed => MarkKind.Completed,
            NodeState.CurrentTarget => MarkKind.Active,
            NodeState.Available => MarkKind.Available,
            NodeState.Partial => MarkKind.Partial,
            _ => MarkKind.Locked,
        };
        EraMarks.State(d, t, right - iconR - 4 * z, y + ins + iconR + 4 * z, iconR, kind, v.Fraction, id, dim);

        if (z < 0.6) return;   // level of detail: the name only when zoomed out
        int detail = t.Density.CardDetail;
        double data = Px(TypeRole.Data, FontRole.Numeric) * z;
        double costY = top + 2 * lead + Sp(5) * z;
        // Row 3: the cost and the Age numeral (UR-4: the data role), any discount (A5+), the stubs on the right (A3+).
        double stubsW = 0;
        int[] pre = [], dep = [];
        double small = Px(TypeRole.Caption, FontRole.Numeric) * z;
        if (detail >= 3)
        {
            (pre, dep) = ResearchTreeLayout.Neighbours(Graph, vertex);
            stubsW = Sp(11) * z * 2 + m.Width(t, pre.Length.ToString(CultureInfo.InvariantCulture), small, FontRole.Numeric)
                + m.Width(t, dep.Length.ToString(CultureInfo.InvariantCulture), small, FontRole.Numeric) + Sp(14) * z;
        }
        string numeral = ResearchTreeLayout.AgeNumeral(v.Age);
        string cost = Num(v.EffectiveCost) + " RP";
        string discount = detail >= 4 && v.EffectiveCost < v.BaseCost && v.State != NodeState.Completed ? "-" + Pct(1 - v.EffectiveCost / v.BaseCost) : "";
        double room = right - Sp(4) * z - stubsW - (x + pad);
        string meta = cost + "  ·  Age " + numeral;
        double dw = discount.Length > 0 ? m.Width(t, discount, data, FontRole.Numeric) + Sp(6) * z : 0;
        if (m.Width(t, meta, data, FontRole.Numeric) + dw > room) meta = cost + " · " + numeral;
        // The cost is data the player compares across cards: the body ink (the soft ink measured 4.3:1 as rendered).
        d.Write(t, x + pad, costY, meta, data, t.Ink.Text, TextAlign.Left, FontRole.Numeric);
        if (discount.Length > 0)
            d.Write(t, x + pad + m.Width(t, meta, data, FontRole.Numeric) + Sp(6) * z, costY, discount, data, Ink.Positive, TextAlign.Left, FontRole.Numeric);
        if (detail >= 3)
        {
            double fy = costY + (t.Type.Size(data) - t.Type.Size(small)) / 2, fx = right - Sp(4) * z - stubsW + Sp(8) * z;
            double tri = Sp(8) * z, tc = fy + t.Type.Size(small) * 0.55;
            d.Polygon([(fx, tc + tri * 0.45), (fx + tri, tc + tri * 0.45), (fx + tri / 2, tc - tri * 0.45)], A(s.Prerequisite, dim ? 0.55 : 0.95));
            string ins2 = pre.Length.ToString(CultureInfo.InvariantCulture);
            d.Write(t, fx + Sp(11) * z, fy, ins2, small, t.Ink.TextSoft, TextAlign.Left, FontRole.Numeric);
            fx += Sp(11) * z + m.Width(t, ins2, small, FontRole.Numeric) + Sp(6) * z;
            d.Polygon([(fx, tc - tri * 0.45), (fx + tri, tc - tri * 0.45), (fx + tri / 2, tc + tri * 0.45)], A(s.Dependent, dim ? 0.55 : 0.95));
            d.Write(t, fx + Sp(11) * z, fy, dep.Length.ToString(CultureInfo.InvariantCulture), small, t.Ink.TextSoft, TextAlign.Left, FontRole.Numeric);
        }

        // Row 4: the state line (the body role, the state's text ink), Eureka pips at its right end (A2+).
        double stateY = costY + t.Type.Size(data) * 1.24;
        double pipsW = 0;
        if (detail >= 2 && v.Eurekas.Count > 0)
        {
            int total = Math.Min(6, v.Eurekas.Count);
            pipsW = total * 10 * z + Sp(6) * z;
            double py = stateY + t.Type.Size(Px(TypeRole.Body) * z) * 0.62;
            for (int e = 0; e < total; e++)
            {
                bool fired = v.Eurekas[e].Fired;
                double px = right - 6 * z - (total - e) * 10 * z + 3.5 * z;
                d.Circle(px, py, 3.3 * z, fired ? s.Completed : null, fired ? null : (dim ? t.Ink.TextSoft : t.Material.AccentSoft), 1.1 * z);
            }
        }
        string line = StateLine(gv.ContentIndex);
        double bodySize = Px(TypeRole.Body) * z;
        double lw = right - Sp(4) * z - pipsW - (x + pad);
        // A long reason steps down to the secondary role before it loses a word (the detail panel has it whole).
        double ls = Math.Max(Px(TypeRole.Secondary) * z, ThemeText.FitSize(m, t, line, bodySize, lw, FontRole.Body, Px(TypeRole.Secondary) / Px(TypeRole.Body)));
        d.Write(t, x + pad, stateY + (t.Type.Size(bodySize) - t.Type.Size(ls)) * 0.7, ThemeText.Fit(m, t, line, ls, lw), ls, StateInk(v.State), TextAlign.Left, FontRole.Body);

        // Retained progress, in the era's representation (tally notches → graduated bar), along the card's foot.
        if (v.Progress > 0 && v.State != NodeState.Completed)
        {
            double ph = t.Controls.Progress == ProgressStyle.Notches ? 6 * z : 3.5 * z;
            var bar = new RectD(x + pad - 2 * z, y + h - ins - ph - 1 * z, w - pad - ins - 2 * z, ph);
            EraMarks.Progress(d, t, bar, v.Fraction, v.State == NodeState.CurrentTarget ? s.Active : s.Progress, id);
        }
    }

    /// <summary>The overview strip: a narrow vertical bar docked at the canvas's right edge,
    /// OUTSIDE the tree viewport (the layout is fitted to the width left of it), so it never
    /// covers a card. It maps the single scroll axis: a click scrolls to that height.</summary>
    public RectD MinimapRect()
    {
        RectD c = Canvas;
        return new RectD(c.X + TreeViewportWidth + Sp(10), c.Y + Sp(10), StripW - Sp(18), c.H - Sp(20));
    }

    private void PaintMinimap(DrawList d)
    {
        EraTheme t = Theme;
        RectD r = MinimapRect();
        d.Rect(new RectD(r.X - Sp(6), c0Y(), StripW - Sp(6), Canvas.H), t.Material.Field);
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

    /// <summary>
    /// THE DETAIL PANEL (UR-4): docked beside the tree in a wide window, an overlay DRAWER over the tree's right edge
    /// below <see cref="DockMinWidth"/> (opened by selecting a node, closed by its × or Escape). Its content is in
    /// DECISION order — the node and its state, the COST and the turns it would take, the action right under them,
    /// what it ENABLES, what it REQUIRES (and why it is locked), its Eurekas, the universities that cheapen it, and
    /// what it is — at the body role, and it SCROLLS (the wheel over it) rather than dropping anything.
    /// </summary>
    private void PaintDetail(DrawList d, ITextMeasure m)
    {
        if (!Docked && !DrawerOpen) return;
        EraTheme t = Theme;
        RectD panel = DetailRect;
        if (!Docked)
        {
            // The drawer's shadow over the tree, then the drawer.
            for (int k = 1; k <= 4; k++) d.Rect(new RectD(panel.X - Sp(3) * k, panel.Y, Sp(3), panel.H), A(t.Ink.Text, 0.05 * (5 - k)));
        }
        PanelFrame.Paint(d, panel, t, 190, FrameKind.Panel);
        _hits.Add(new HitRegion(panel, HitKind.Panel, 0));
        int node = DetailNode;
        if (node != _detailNode) { _detailNode = node; _detailScroll = 0; }
        double x = panel.X + Sp(22), w = panel.W - Sp(44);
        var view = new RectD(panel.X + Sp(6), panel.Y + Sp(8), panel.W - Sp(12), panel.H - Sp(16));
        d.PushClip(view);
        _infoClip = view;   // M5 polish: the detail's inspectable regions are cut to its scrolled view
        double y0 = panel.Y + Sp(20) - _detailScroll;
        double end = node < 0 ? PaintIntro(d, m, x, w, y0) : PaintNode(d, m, node, x, w, y0, view);
        _infoClip = null;
        d.PopClip();
        // Scrolling: the content's height against the view; a bar and a "more below" mark when it overflows.
        double content = end + _detailScroll - (panel.Y + Sp(20)) + Sp(24);
        double maxScroll = Math.Max(0, content - (view.H - Sp(12)));
        if (_detailScroll > maxScroll) _detailScroll = maxScroll;
        if (maxScroll > 0)
        {
            var track = new RectD(panel.Right - Sp(10), view.Y + Sp(4), Sp(4), view.H - Sp(8));
            d.Rect(track, A(t.Material.Hairline, 0.5), null, 0, Sp(2));
            double frac = view.H / Math.Max(view.H, content), at = _detailScroll / Math.Max(1, maxScroll);
            double th = Math.Max(Sp(24), track.H * frac);
            d.Rect(new RectD(track.X, track.Y + (track.H - th) * at, track.W, th), t.Material.AccentSoft, null, 0, Sp(2));
            if (_detailScroll < maxScroll - 0.5)
            {
                double cap = Px(TypeRole.Caption);
                var more = new RectD(panel.X + Sp(16), view.Bottom - t.Type.Size(cap) - Sp(10), panel.W - Sp(40), t.Type.Size(cap) + Sp(8));
                d.Rect(more, A(t.Material.Panel, 0.94));
                d.Write(t, more.CenterX, more.Y + Sp(2), "more below - scroll", cap, t.Ink.TextSoft, TextAlign.Center);
            }
        }
        if (!Docked)
        {
            var close = new RectD(panel.Right - Sp(46), panel.Y + Sp(10), Sp(34), Sp(34));
            PanelFrame.Paint(d, close, t, 198, FrameKind.Button, Over(close) ? HoverFill : null, Over(close) ? t.Material.BorderStrong : null);
            EraMarks.Close(d, t, close, t.Ink.Text, 197);
            _hits.Add(new HitRegion(close, HitKind.CloseDetail, 0));
        }
    }

    /// <summary>The panel with no node in focus (docked only): the tree, its numbers, and how to use it.</summary>
    private double PaintIntro(DrawList d, ITextMeasure m, double x, double w, double y)
    {
        EraTheme t = Theme;
        SemanticTokens sm = t.Semantic;
        ResearchSnapshot s = Snapshot!;
        double title = Px(TypeRole.Title, FontRole.Title), body = Px(TypeRole.Body);
        d.Title(t, x, y, Graph.Tree == ResearchTree.Technology ? "The Technology Tree" : "The Civics Tree", title, Ink.Accent);
        y += FlowText.Slot(TypeRole.Title, _scale) + Sp(8);
        string intro = Graph.Tree == ResearchTree.Technology
            ? "The main trunk and five specialised subtrees. The subtrees open together once the research stage is reached."
            : "Forms of social organisation. Adopted civics also appear under the INSTITUTIONS lens.";
        foreach (string line in FlowText.Wrap(m, intro, TypeRole.Body, _scale, w)) { d.Write(t, x, y, line, body, t.Ink.TextSoft); y += FlowText.Slot(TypeRole.Body, _scale); }
        y += Sp(12);
        y = Stat(d, m, x, w, y, "Research stage", s.StageReached ? "reached" : "not yet reached", s.StageReached ? Ink.Positive : Ink.Progress);
        y = Stat(d, m, x, w, y, "Research per turn", s.PointsPerTurn.ToString("0.0", CultureInfo.InvariantCulture) + " RP", Ink.Knowledge);
        y = Stat(d, m, x, w, y, "Completed", Graph.Tree == ResearchTree.Technology ? s.CompletedTechnology + " of " + Content.TechnologyCount : s.CompletedCivics + " of " + Content.CivicsCount, t.Ink.Text);
        int avail = 0;
        for (int v = 0; v < Graph.OwnCount; v++) if (s.Nodes[Graph.Vertices[v].ContentIndex].Available) avail++;
        y = Stat(d, m, x, w, y, "Available now", avail.ToString(CultureInfo.InvariantCulture), _availableInk);
        y += Sp(14);
        foreach (string line in FlowText.Wrap(m, "Hover a node for its details. Click an available node to make it the research target; the order applies at End Turn. Shift+click any node or name for its full card. The wheel scrolls; Ctrl + wheel zooms.", TypeRole.Body, _scale, w))
        { d.Write(t, x, y, line, body, t.Ink.TextSoft); y += FlowText.Slot(TypeRole.Body, _scale); }
        return y;
    }

    /// <summary>One node in decision order; returns the y under the last line.</summary>
    private double PaintNode(DrawList d, ITextMeasure m, int node, double x, double w, double y, RectD view)
    {
        EraTheme t = Theme;
        SemanticTokens sm = t.Semantic;
        ResearchSnapshot s = Snapshot!;
        ResearchNodeView n = s.Nodes[node];
        double caps = Px(TypeRole.Caption, FontRole.Caps), body = Px(TypeRole.Body), data = Px(TypeRole.Data, FontRole.Numeric);
        double second = Px(TypeRole.Secondary), title = Px(TypeRole.Title, FontRole.Title);
        Rgba hue = ProgressionPalette.BranchOf(t, Content, Content.Nodes[node]);
        d.Rect(new RectD(x - Sp(10), y - Sp(12), w + Sp(20), Sp(4)), hue);
        // The node: branch and Age in full (the card shows the numeral), its name, its state.
        if (!Docked) w -= Sp(40);   // clear the drawer's close button on the first lines
        WriteFit(d, m, x, y, n.BranchName.ToUpperInvariant(), caps, w, LaneInk(hue), TextAlign.Left, FontRole.Caps);
        y += FlowText.Slot(TypeRole.Caption, _scale, caps: true);
        WriteFit(d, m, x, y, ResearchTreeLayout.AgeLabel(n.Age).ToUpperInvariant(), caps, w, Ink.Accent, TextAlign.Left, FontRole.Caps);
        y += FlowText.Slot(TypeRole.Caption, _scale, caps: true);
        y += Sp(2);
        bool firstLine = true;
        foreach (string line in FlowText.Wrap(m, n.Name, TypeRole.Title, _scale, w, FontRole.Title))
        {
            if (firstLine) d.Title(t, x, y, line, title, t.Ink.Text);
            else d.Write(t, x, y, line, title, t.Ink.Text, TextAlign.Left, FontRole.Title);
            firstLine = false;
            y += FlowText.Slot(TypeRole.Title, _scale);
        }
        if (!Docked) w += Sp(40);
        y += Sp(4);
        Rgba stc = n.State switch { NodeState.Completed => sm.Completed, NodeState.CurrentTarget => sm.Active, NodeState.Available => sm.Available, NodeState.Partial => sm.Progress, _ => sm.Locked };
        string stateLabel = ProgressionPalette.StateLabel(n.State).ToUpperInvariant();
        double sw = m.Width(t, stateLabel, caps, FontRole.Caps) + Sp(20);
        PanelFrame.Paint(d, new RectD(x, y, sw, t.Type.Size(caps) + Sp(10)), t, 191, FrameKind.Chip, Mix(t.Material.Panel, stc, 0.14), stc, 1.0);
        d.Write(t, x + Sp(10), y + Sp(3), stateLabel, caps, StateInk(n.State), TextAlign.Left, FontRole.Caps);
        y += FlowText.Slot(TypeRole.Caption, _scale, caps: true) + Sp(10);
        foreach (string line in FlowText.Wrap(m, StateLine(node), TypeRole.Body, _scale, w))
        { d.Write(t, x, y, line, body, StateInk(n.State)); y += FlowText.Slot(TypeRole.Body, _scale); }
        y += Sp(6);

        // COST, the turns it would take, and the action directly under them.
        y = Heading(d, m, x, w, y, "COST", 1);
        y = Stat(d, m, x, w, y, "Base cost", Num(n.BaseCost) + " RP", t.Ink.TextSoft);
        foreach ((string uni, double f) in n.CostTerms)
            y = Stat(d, m, x, w, y, "  " + ResearchTreeLayout.Title(uni) + " university", "× " + f.ToString("0.00", CultureInfo.InvariantCulture), Ink.Positive);
        y = Stat(d, m, x, w, y, "Effective cost" + (n.FloorBinds ? " (floor)" : ""), Num(n.EffectiveCost) + " RP", t.Ink.Text);
        if (n.State != NodeState.Completed)
        {
            EraMarks.Progress(d, t, new RectD(x, y + Sp(4), w, t.Controls.Progress == ProgressStyle.Notches ? Sp(11) : Sp(8)), n.Fraction,
                n.State == NodeState.CurrentTarget ? sm.Active : sm.Progress, 192);
            y += Sp(20);
            string turns = s.PointsPerTurn > 0 ? "  ·  ~" + Math.Ceiling(Math.Max(0, n.EffectiveCost - n.Progress) / s.PointsPerTurn).ToString("0", CultureInfo.InvariantCulture) + " turns at the current rate" : "";
            foreach (string line in FlowText.Wrap(m, Num(n.Progress) + " / " + Num(n.EffectiveCost) + " RP" + turns, TypeRole.Data, _scale, w, FontRole.Numeric))
            { d.Write(t, x, y, line, data, t.Ink.TextSoft, TextAlign.Left, FontRole.Numeric); y += FlowText.Slot(TypeRole.Data, _scale); }
        }
        y += Sp(8);
        y = PaintAction(d, m, n, node, x, w, y, view) + Sp(14);

        // ENABLES: what completing it gives — the capabilities, techniques and applications the content declares, the
        // entities it unlocks — and the nodes it leads to (all read from the content, nothing invented).
        y = Heading(d, m, x, w, y, "ENABLES", 5);
        ResearchNode cn = Content.Nodes[node];
        bool any = false;
        // M5 polish (§10): with the session's config, the node's InfoQuery card paints what it opens (each entity and
        // what realizes it), the nodes it leads to (each inspectable) and what is knowledge only — in place of the
        // bare "Unlocks:" / "Leads to:" lists. A preview (no config) keeps the lists.
        bool discovery = NodeCard(node) is not null;
        foreach ((string label, IReadOnlyList<string> items) in new (string, IReadOnlyList<string>)[]
                     { ("", cn.Capabilities), ("Techniques: ", cn.Techniques), ("Applications: ", cn.Applications), ("Unlocks: ", n.Unlocks) })
        {
            if (items.Count == 0) continue;
            if (discovery && label == "Unlocks: ") continue;
            any = true;
            foreach (string line in FlowText.Wrap(m, label + string.Join(label.Length == 0 ? "; " : ", ", items), TypeRole.Body, _scale, w)) { d.Write(t, x, y, line, body, t.Ink.Text); y += FlowText.Slot(TypeRole.Body, _scale); }
            y += Sp(4);
        }
        var leads = new List<string>();
        int nvx = Graph.VertexOf(node);
        if (nvx >= 0)
        {
            (int[] _, int[] deps) = ResearchTreeLayout.Neighbours(Graph, nvx);
            foreach (int v in deps) if (!Graph.Vertices[v].External) leads.Add(Graph.Node(v).Name);
        }
        if (discovery)
        {
            double before = y;
            y = PaintDiscovery(d, m, node, x, w, y);
            if (y > before + Sp(6) + 0.01) any = true;
            leads.Clear();   // the card's LEADS TO names them
        }
        if (leads.Count > 0)
            foreach (string line in FlowText.Wrap(m, "Leads to: " + string.Join(", ", leads), TypeRole.Body, _scale, w)) { d.Write(t, x, y, line, body, t.Ink.TextSoft); y += FlowText.Slot(TypeRole.Body, _scale); }
        if (!any && leads.Count == 0) { d.Write(t, x, y, "Nothing further in this tree yet.", body, t.Ink.TextSoft); y += FlowText.Slot(TypeRole.Body, _scale); }
        y += Sp(8);

        // REQUIRES: the prerequisites (marked done or missing; a prerequisite in another lane names it) and why it is locked.
        y = Heading(d, m, x, w, y, n.Prerequisites.Count == 0 ? "REQUIRES - nothing (a root)" : "REQUIRES", 2);
        bool anyOr = false;
        foreach (PrereqView p in n.Prerequisites) if (p.Kind == EdgeKind.Or) anyOr = true;
        if (anyOr && n.PrerequisiteExpression is string expr)
        {
            foreach (string line in FlowText.Wrap(m, expr.Replace("_", " "), TypeRole.Caption, _scale, w, FontRole.Numeric))
            { d.Write(t, x, y, line, Px(TypeRole.Caption, FontRole.Numeric), t.Ink.TextSoft, TextAlign.Left, FontRole.Numeric); y += FlowText.Slot(TypeRole.Caption, _scale); }
            y += Sp(2);
        }
        int pi = 0;
        foreach (PrereqView p in n.Prerequisites)
        {
            EraMarks.Tick(d, t, x + 1, y + Sp(5), Sp(12), p.Completed, p.Completed ? sm.Completed : sm.Danger, 193 + pi++);
            int pv = Graph.VertexOf(p.ContentIndex), nv = Graph.VertexOf(node);
            string where = "";
            if (pv >= 0 && nv >= 0 && Layout.Placed[pv].Lane != Layout.Placed[nv].Lane)
                where = " (" + (Graph.Vertices[pv].External ? (Graph.Tree == ResearchTree.Civics ? "Technology" : "Civics") : Layout.Lanes[Layout.Placed[pv].Lane].Name) + ")";
            string kind = p.Kind == EdgeKind.And ? "required" : "one of";
            double kw = FlowText.Width(m, kind, TypeRole.Secondary, _scale) + Sp(8);
            List<string> lines = FlowText.Wrap(m, p.Name + where, TypeRole.Body, _scale, w - Sp(22) - kw);
            d.Write(t, x + w, y + Sp(2), kind, second, t.Ink.TextSoft, TextAlign.Right);
            double nameW = 0;
            foreach (string line in lines) nameW = Math.Max(nameW, FlowText.Width(m, line, TypeRole.Body, _scale));
            InfoNode(new RectD(x + Sp(22), y, Math.Min(w - Sp(22) - kw, nameW), lines.Count * FlowText.Slot(TypeRole.Body, _scale)), p.ContentIndex);
            foreach (string line in lines) { d.Write(t, x + Sp(22), y, line, body, p.Completed ? t.Ink.Text : t.Ink.TextSoft); y += FlowText.Slot(TypeRole.Body, _scale); }
        }
        if (n.Lock.HasFlag(LockReason.ResearchStage))
            foreach (string line in FlowText.Wrap(m, "Subtree closed: the research stage is not reached (" + s.StageExpression.Replace("_", " ") + ").", TypeRole.Body, _scale, w))
            { d.Write(t, x, y, line, body, Ink.Progress); y += FlowText.Slot(TypeRole.Body, _scale); }
        if (n.Lock.HasFlag(LockReason.SubtreeNotExhausted))
            foreach (string line in FlowText.Wrap(m, "Recursive: waits for every finite node of its subtree.", TypeRole.Body, _scale, w))
            { d.Write(t, x, y, line, body, Ink.Progress); y += FlowText.Slot(TypeRole.Body, _scale); }
        y += Sp(8);

        // EUREKA.
        y = Heading(d, m, x, w, y, "EUREKA", 3);
        if (n.Eurekas.Count == 0) { d.Write(t, x, y, "This node has no Eureka.", body, t.Ink.TextSoft); y += FlowText.Slot(TypeRole.Body, _scale) + Sp(8); }
        else
        {
            double credited = n.EurekaCredited + n.ForeignCredited;
            foreach (string line in FlowText.Wrap(m, "Acceleration credit " + Num(credited) + " of " + Num(n.EurekaCeiling) + " RP ceiling (" + Pct(n.EurekaCeiling / Math.Max(1e-9, n.BaseCost)) + " of base)", TypeRole.Secondary, _scale, w))
            { d.Write(t, x, y, line, second, t.Ink.TextSoft); y += FlowText.Slot(TypeRole.Secondary, _scale); }
            y += Sp(2);
            double ce = Math.Max(1e-9, n.EurekaCeiling);
            if (t.Charts.Sophistication <= 1)
                EraMarks.Progress(d, t, new RectD(x, y, w, Sp(9)), Math.Min(1, credited / ce), sm.Completed, 194);
            else
            {
                var bar = new RectD(x, y, w, Sp(8));
                d.Rect(bar, A(t.Material.PanelSunken, 0.6), t.Material.Hairline, 1, t.Edge.Corner == CornerStyle.Fine ? 2 : 0);
                d.Rect(new RectD(x, y, w * Math.Min(1, n.EurekaCredited / ce), Sp(8)), sm.Completed);
                d.Rect(new RectD(x + w * Math.Min(1, n.EurekaCredited / ce), y, w * Math.Min(1, n.ForeignCredited / ce), Sp(8)), sm.Lanes.Civics);
                if (t.Charts.Ticks)
                    for (int k = 1; k < 4; k++) d.Line(x + w * k / 4.0, y + Sp(8), x + w * k / 4.0, y + Sp(11), A(t.Material.Border, 0.6), 0.6);
            }
            y += Sp(16);
            foreach (string line in FlowText.Wrap(m, "Eureka " + Num(n.EurekaCredited) + " RP  ·  foreign exposure " + Num(n.ForeignCredited) + " RP" + (n.ExposureOffered > 0 ? " (offered " + Num(n.ExposureOffered) + ")" : ""), TypeRole.Secondary, _scale, w))
            { d.Write(t, x, y, line, second, t.Ink.TextSoft); y += FlowText.Slot(TypeRole.Secondary, _scale); }
            y += Sp(6);
            int ei = 0;
            foreach (EurekaView e in n.Eurekas)
            {
                Rgba ec = e.Fired ? _completedInk : e.HoldsNow == true ? Ink.Positive : t.Ink.TextSoft;
                EraMarks.State(d, t, x + Sp(6), y + Sp(11), Sp(5), e.Fired ? MarkKind.Completed : MarkKind.Available, 0, 195 + ei++);
                string weight = Pct(e.Weight) + " · " + Num(e.MaxCredit);
                double ww = FlowText.Width(m, weight, TypeRole.Caption, _scale, FontRole.Numeric) + Sp(10);
                d.Write(t, x + w, y + Sp(3), weight, Px(TypeRole.Caption, FontRole.Numeric), t.Ink.TextSoft, TextAlign.Right, FontRole.Numeric);
                foreach (string line in FlowText.Wrap(m, e.Text, TypeRole.Body, _scale, w - Sp(20) - ww))
                { d.Write(t, x + Sp(20), y, line, body, e.Fired ? t.Ink.Text : t.Ink.TextSoft); y += FlowText.Slot(TypeRole.Body, _scale); }
                string status = e.Fired ? "fired - credited" : e.Condition is null ? "not evaluable yet (" + e.System + ")" : e.HoldsNow == true ? "holds now - fires when available" : "condition: " + ConditionText(e.Condition);
                foreach (string line in FlowText.Wrap(m, status, TypeRole.Secondary, _scale, w - Sp(20))) { d.Write(t, x + Sp(20), y, line, second, ec, TextAlign.Left); y += FlowText.Slot(TypeRole.Secondary, _scale); }
                y += Sp(6);
            }
        }

        // UNIVERSITY relevance.
        y = Heading(d, m, x, w, y, "UNIVERSITY", 4);
        if (n.Universities.Count == 0) { d.Write(t, x, y, "None - main trunk and civics cost their base.", body, t.Ink.TextSoft); y += FlowText.Slot(TypeRole.Body, _scale) + Sp(8); }
        foreach (UniversityView u in n.Universities)
        {
            d.Write(t, x, y, u.Name, body, t.Ink.Text);
            d.Write(t, x + w, y + Sp(3), u.Role, caps, u.Role == "primary" ? Ink.Accent : t.Ink.TextSoft, TextAlign.Right, FontRole.Caps);
            y += FlowText.Slot(TypeRole.Body, _scale);
        }
        y += Sp(8);

        // What it is.
        y = Heading(d, m, x, w, y, "ABOUT", 6);
        foreach (string line in FlowText.Wrap(m, n.Description, TypeRole.Body, _scale, w)) { d.Write(t, x, y, line, body, t.Ink.TextSoft); y += FlowText.Slot(TypeRole.Body, _scale); }
        return y;
    }

    /// <summary>The panel's action, right under the cost (UR-4): SET AS RESEARCH TARGET as the primary control when the
    /// node is available; otherwise a disabled plate that says why (known, the target already, ordered, or what it
    /// still needs). Its hit region exists only where it is visible in the panel.</summary>
    private double PaintAction(DrawList d, ITextMeasure m, ResearchNodeView n, int node, double x, double w, double y, RectD view)
    {
        EraTheme t = Theme;
        var btn = new RectD(x, y, w, Sp(46));
        bool can = n.Available && !n.IsTarget && PendingTarget != node;
        bool hover = can && Over(btn) && view.Contains(btn.CenterX, btn.CenterY);
        string label = n.State switch
        {
            NodeState.Completed => "Known",
            NodeState.CurrentTarget => "Current research target",
            _ when PendingTarget == node => "Ordered - applies at End Turn",
            NodeState.Available => "Set as research target",
            _ => "Locked",
        };
        double caps = Px(TypeRole.Body, FontRole.Caps);
        if (can)
        {
            PanelFrame.Paint(d, btn, t, 199, FrameKind.Button, hover ? Mix(t.Semantic.ActiveFill, t.Semantic.Active, 0.30) : Mix(t.Semantic.ActiveFill, t.Semantic.Active, 0.12),
                t.Semantic.Active, hover ? 2.2 : 1.8);
            WriteFit(d, m, btn.CenterX, btn.Y + (btn.H - t.Type.Size(caps)) / 2 - Sp(2), label.ToUpperInvariant(), caps, btn.W - Sp(16), t.Ink.Text, TextAlign.Center, FontRole.Caps);
            if (view.Contains(btn.CenterX, btn.CenterY)) _hits.Add(new HitRegion(btn, HitKind.SetTarget, node));
        }
        else
        {
            // The disabled plate: sunken, a dashed hairline, a lock where it is locked, the label in the soft ink.
            d.Rect(btn, A(t.Material.PanelSunken, 0.6));
            Rgba hl = t.Material.Hairline;
            (double, double) dash = (Sp(5), Sp(4));
            d.Line(btn.X, btn.Y, btn.Right, btn.Y, hl, 1, dash); d.Line(btn.X, btn.Bottom, btn.Right, btn.Bottom, hl, 1, dash);
            d.Line(btn.X, btn.Y, btn.X, btn.Bottom, hl, 1, dash); d.Line(btn.Right, btn.Y, btn.Right, btn.Bottom, hl, 1, dash);
            double lw = m.Width(t, label.ToUpperInvariant(), caps, FontRole.Caps);
            double lx = btn.CenterX - lw / 2;
            if (n.State is NodeState.Locked or NodeState.Partial)
            {
                EraMarks.State(d, t, lx - Sp(16), btn.CenterY, Sp(7), MarkKind.Locked, 0, 196, dim: true);
                lx += Sp(6);
            }
            d.Write(t, lx, btn.Y + (btn.H - t.Type.Size(caps)) / 2 - Sp(2), label.ToUpperInvariant(), caps, t.Ink.TextSoft, TextAlign.Left, FontRole.Caps);
        }
        InfoNode(btn, node);   // M5 polish: Shift+click on the button shows the node's card, never an order
        return btn.Bottom;
    }

    /// <summary>A section heading: capitals at the heading role in the accent's TEXT ink, over a hairline.</summary>
    private double Heading(DrawList d, ITextMeasure m, double x, double w, double y, string text, int id)
    {
        EraTheme t = Theme;
        double hs = Px(TypeRole.Heading, FontRole.Caps);
        WriteFit(d, m, x, y, text, hs, w, Ink.Accent, TextAlign.Left, FontRole.Caps);
        y += FlowText.Slot(TypeRole.Heading, _scale, caps: true) + Sp(1);
        d.Line(x, y, x + w, y, A(t.Material.Hairline, 0.9), Math.Max(1, _scale));
        _ = id;
        return y + Sp(8);
    }

    private double Stat(DrawList d, ITextMeasure m, double x, double w, double y, string label, string value, Rgba valueColor)
    {
        EraTheme t = Theme;
        double body = Px(TypeRole.Body), data = Px(TypeRole.Data, FontRole.Numeric);
        double vw = m.Width(t, value, data, FontRole.Numeric);
        WriteFit(d, m, x, y, label, body, w - vw - Sp(12), t.Ink.TextSoft);
        d.Write(t, x + w, y + (t.Type.Size(body) - t.Type.Size(data)) / 2, value, data, valueColor, TextAlign.Right, FontRole.Numeric);
        return y + FlowText.Slot(TypeRole.Body, _scale);
    }

    /// <summary>
    /// THE HOVER TIP (narrow windows, UR-4): with no docked panel, the hovered card's full name, its Age in full and
    /// its state line appear beside it (the card itself shows the Age numeral); a click opens the drawer.
    /// </summary>
    private void PaintHoverTip(DrawList d, ITextMeasure m)
    {
        if (Docked || Hovered < 0 || Hovered == Selected || _pointer is not (double px, double py)) return;
        EraTheme t = Theme;
        ResearchNodeView n = Snapshot!.Nodes[Hovered];
        double body = Px(TypeRole.Body), name = Px(TypeRole.Body, FontRole.Heading), cap = Px(TypeRole.Caption);
        string age = ResearchTreeLayout.AgeLabel(n.Age);
        string state = StateLine(Hovered);
        double w = Math.Min(Sp(420), Math.Max(m.Width(t, n.Name, name, FontRole.Heading), Math.Max(m.Width(t, age, cap), m.Width(t, state, body))) + Sp(28));
        double h = Line(name) + Line(cap) + Line(body) + Sp(18);
        RectD c = Canvas;
        double x = Math.Min(px + Sp(18), c.Right - w - Sp(8)), y = Math.Min(py + Sp(22), c.Bottom - h - Sp(8));
        if (x < c.X + Sp(8)) x = c.X + Sp(8);
        var r = new RectD(x, y, w, h);
        d.Rect(new RectD(r.X + Sp(3), r.Y + Sp(4), r.W, r.H), A(t.Ink.Text, 0.18), null, 0, Sp(4));
        PanelFrame.Paint(d, r, t, 189, FrameKind.Chip, t.Material.Panel, t.Material.BorderStrong, 1.2);
        double ty = r.Y + Sp(8);
        WriteFit(d, m, r.X + Sp(14), ty, n.Name, name, r.W - Sp(28), t.Ink.Text, TextAlign.Left, FontRole.Heading); ty += Line(name);
        WriteFit(d, m, r.X + Sp(14), ty, age, cap, r.W - Sp(28), Ink.Accent); ty += Line(cap);
        d.Write(t, r.X + Sp(14), ty, ThemeText.Fit(m, t, state, body, r.W - Sp(28)), body, StateInk(n.State));
    }

    // ---- the other lenses

    private void PaintLensPage(DrawList d, ITextMeasure m)
    {
        EraTheme t = Theme;
        LensPage p = Page!;
        RectD c = Canvas;
        double x = c.X + Sp(48), y = c.Y + Sp(30), w = c.W - Sp(96);
        double display = Px(TypeRole.Display, FontRole.Title), body = Px(TypeRole.Body), heading = Px(TypeRole.Heading, FontRole.Caps);
        d.Title(t, x, y, p.Title, display, Ink.Accent);
        y += Line(display) + Sp(4);
        foreach (string line in ThemeText.Wrap(m, t, p.Purpose, body, w)) { d.Write(t, x, y, line, body, t.Ink.TextSoft); y += Line(body); }
        y += Sp(10);
        Rgba sc = p.Status switch { LensStatus.Functional => t.Semantic.Positive, LensStatus.PartialData => t.Semantic.Progress, _ => t.Ink.TextSoft };
        Rgba si = p.Status switch { LensStatus.Functional => Ink.Positive, LensStatus.PartialData => Ink.Progress, _ => t.Ink.TextSoft };
        List<string> noteLines = ThemeText.Wrap(m, t, p.StatusNote, body, w - Sp(32));
        var note = new RectD(x, y, w, noteLines.Count * Line(body) + Sp(20));
        PanelFrame.Paint(d, note, t, 400, FrameKind.Chip, Mix(t.Material.Panel, sc, 0.10), A(sc, 0.7), 1.0);
        double ny = y + Sp(9);
        foreach (string line in noteLines) { d.Write(t, x + Sp(16), ny, line, body, si); ny += Line(body); }
        y = note.Bottom + Sp(22);
        if (p.Sections.Count == 0)
        {
            d.Write(t, c.CenterX, c.CenterY, "NOT YET SIMULATED", Px(TypeRole.Display, FontRole.Caps), t.Ink.TextSoft, TextAlign.Center, FontRole.Caps);
            return;
        }
        double colW = (w - Sp(24) * (p.Sections.Count - 1)) / p.Sections.Count;
        for (int s = 0; s < p.Sections.Count; s++)
        {
            LensSection sec = p.Sections[s];
            double sx = x + s * (colW + Sp(24)), sy = y;
            PanelFrame.Paint(d, new RectD(sx, sy, colW, c.Bottom - sy - Sp(30)), t, 410 + s, FrameKind.Panel);
            double nw = sec.Note.Length > 0 ? m.Width(t, sec.Note, Px(TypeRole.Caption)) + Sp(16) : 0;
            WriteFit(d, m, sx + Sp(18), sy + Sp(16), sec.Heading.ToUpperInvariant(), heading, colW - Sp(36) - nw, Ink.Accent, TextAlign.Left, FontRole.Caps);
            if (sec.Note.Length > 0) d.Write(t, sx + colW - Sp(18), sy + Sp(18), sec.Note, Px(TypeRole.Caption), t.Ink.TextSoft, TextAlign.Right);
            sy += Sp(16) + Line(heading) + Sp(10);
            if (sec.Items.Count == 0) { d.Write(t, sx + Sp(18), sy, "Nothing yet.", body, t.Ink.TextSoft); continue; }
            int k = 0;
            for (int i = 0; i < sec.Items.Count; i++)
            {
                if (sy > c.Bottom - Sp(60) - Line(body))
                {
                    // Never a silent drop: the column says how many more there are.
                    d.Write(t, sx + Sp(18), sy, "+ " + (sec.Items.Count - i).ToString(CultureInfo.InvariantCulture) + " more", body, t.Ink.TextSoft);
                    break;
                }
                if (sec.Subjects is { } subjects && i < subjects.Count && subjects[i] is { } subject)
                    InfoAdd(new RectD(sx + Sp(36), sy, Math.Min(colW - Sp(54), m.Width(t, sec.Items[i], body)), Line(body)), subject, sec.Items[i]);
                EraMarks.State(d, t, sx + Sp(24), sy + t.Type.Size(body) * 0.6, Sp(3.4), MarkKind.Completed, 0, 420 + k++);
                WriteFit(d, m, sx + Sp(36), sy, sec.Items[i], body, colW - Sp(54), t.Ink.Text);
                sy += Line(body);
            }
        }
    }
}
