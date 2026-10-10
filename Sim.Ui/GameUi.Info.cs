using ImGuiNET;
using Microsoft.Xna.Framework.Input;
using Sim.Core.State;
using Sim.Ui.Info;
using Sim.Ui.ViewModel;
using RectD = Sim.Ui.Render.RectD;

namespace Sim.Ui;

/// <summary>
/// M5 POLISH — SHIFT + LEFT CLICK UNIVERSAL INFO (directive §9). Every painter registers what it draws that can be
/// named (<see cref="InfoRegistry"/>); a left click with Shift held on a registered region opens that subject's card
/// — built by <see cref="InfoQuery.Card"/> from content and the simulation's own predicates — pinned in the right-hand
/// column (over the map: the context panel's place; in the research screen: the detail column). Shift+click NEVER
/// issues an order and never performs the plain click (inspecting an available node does not target it; inspecting
/// "Build" does not enqueue); a plain click behaves exactly as before. A painted hint, "Shift+click: details", follows
/// the pointer over an inspectable region (painted on the foreground list: the game draws no ImGui tooltips). Escape
/// closes the card before anything else. "Show in research tree" focuses the node (ProgressionScreen.Focus).
/// </summary>
public sealed partial class GameUi
{
    private readonly InfoPanel _inspector = new();
    private bool _shift;
    private bool _infoDown;
    private int _infoDownX, _infoDownY;
    private double _infoContentHeight;
    private (Section, InfoSubject?) _infoBodyKey;

    /// <summary>The inspectable regions of the last frame (what the player saw).</summary>
    public InfoRegistry InfoRegistry { get; } = new();

    /// <summary>The pinned info card (its subject, card and history).</summary>
    public InfoPanel Inspector => _inspector;

    /// <summary>The hover hint painted last frame ("Shift+click: details"), or null.</summary>
    public string? InfoHint { get; private set; }

    /// <summary>Where the hover hint was painted last frame.</summary>
    public RectD? InfoHintRect { get; private set; }

    /// <summary>Opens (pins) the card for <paramref name="subject"/>; it is built at the next draw. UI state only.</summary>
    public void OpenInfo(InfoSubject subject) => _inspector.Open(subject);

    /// <summary>The research figure's subject: the current target's node, else research itself.</summary>
    private InfoSubject ResearchChipSubject() =>
        _session.Config.Research is { } r && ResearchQuery.TryGetTarget(_world, UiPlayer.Empire, out ResearchNodeId t) && r.IndexOf(t) >= 0
            ? InfoSubject.Node(t) : InfoSubject.Research;

    /// <summary>The Age figure's subject: the next Age (what it takes), or the current one at the final Age.</summary>
    private InfoSubject AgeChipSubject()
    {
        if (_session.Config.Ages is not { } ages) return InfoSubject.OfAge(1);
        return AgeQuery.NextAge(_world, ages, UiPlayer.Empire) is { } next ? InfoSubject.OfAge(next.Key)
            : InfoSubject.OfAge(AgeQuery.CurrentAge(_world, ages, UiPlayer.Empire));
    }

    // ================================================================== input

    /// <summary>Escape closes the card first (before the formation card, the Age panel, a section, the trees, or exit).</summary>
    private bool UpdateInfoKeys(KeyboardState keyboard, ImGuiIOPtr io)
    {
        if (!IsActive || !_inspector.IsOpen || _age.FlowOpen || io.WantCaptureKeyboard) return false;
        if (!keyboard.IsKeyDown(Keys.Escape) || _lastKeyboard.IsKeyDown(Keys.Escape)) return false;
        _inspector.Close();
        return true;
    }

    /// <summary>A Shift+click (press and release within 4 px) on a PASSIVE region — a text line, a map token — opens
    /// its card. True when it did (the click is then nobody else's).</summary>
    private bool UpdateInfoClick(Microsoft.Xna.Framework.Input.MouseState mouse)
    {
        if (!IsActive) return false;
        if (mouse.LeftButton == ButtonState.Pressed && _lastMouse.LeftButton == ButtonState.Released)
        {
            _infoDown = true;
            _infoDownX = mouse.X;
            _infoDownY = mouse.Y;
        }
        if (mouse.LeftButton != ButtonState.Released || _lastMouse.LeftButton != ButtonState.Pressed) return false;
        bool click = _infoDown && Math.Abs(mouse.X - _infoDownX) <= 4 && Math.Abs(mouse.Y - _infoDownY) <= 4;
        _infoDown = false;
        if (!click || !_shift || InfoRegistry.HitTest(mouse.X, mouse.Y) is not { Passive: true } hit) return false;
        OpenInfo(hit.Subject);
        return true;
    }

    /// <summary>In the research screen: a click on the pinned card's own controls, or a Shift+click on a node, a
    /// prerequisite row, an "opens" line… True when handled (the tree never sees it).</summary>
    private bool InfoClickInProgression(int x, int y)
    {
        RectD panel = InfoOverlayRect();
        if (_inspector.IsOpen && panel.Contains(x, y))
        {
            HandleInfo(_inspector.Click(x, y, panel));
            return true;
        }
        if (_shift && InfoRegistry.HitTest(x, y) is { } hit)
        {
            OpenInfo(hit.Subject);
            return true;
        }
        return false;
    }

    private bool InfoWheelInProgression(int x, int y, int wheel)
    {
        RectD panel = InfoOverlayRect();
        if (!_inspector.IsOpen || !panel.Contains(x, y)) return false;
        _inspector.ScrollBy(-wheel / 120.0 * 60.0, _infoContentHeight, panel.H - 40);
        return true;
    }

    private void HandleInfo(InfoCommand cmd)
    {
        switch (cmd.Kind)
        {
            case InfoCommandKind.Close: _inspector.Close(); break;
            case InfoCommandKind.Back: _inspector.Back(); break;
            case InfoCommandKind.Open: _inspector.Open(cmd.Subject); break;
            case InfoCommandKind.ShowInTree: ShowInTree(cmd.Node, closeInfo: true); break;
        }
    }

    /// <summary>Opens the research screen on <paramref name="key"/>'s tree, its lane open, the node selected and in view
    /// (ProgressionScreen.Focus). Moves the camera only.</summary>
    public void ShowInTree(ResearchNodeId key, bool closeInfo)
    {
        if (_session.Config.Research is not { } content || content.IndexOf(key) is not (int index and >= 0)) return;
        if (!_progressionOpen) ToggleProgression();
        Sim.Ui.Progression.ProgressionScreen screen = _progression!;
        screen.Resize(_viewportWidth, _viewportHeight);
        screen.Refresh(_world);
        if (screen.Lens != Sim.Ui.Progression.Lens.KnowledgeAndTechnology) screen.SetLens(Sim.Ui.Progression.Lens.KnowledgeAndTechnology);
        screen.Focus(index, jump: true);
        int v = screen.Graph.VertexOf(index);
        if (v >= 0 && screen.Layout.Placed[v].Hidden)
        {
            screen.ToggleLane(screen.Layout.Placed[v].Lane);
            screen.Focus(index, jump: true);
        }
        if (closeInfo) _inspector.Close();
    }

    /// <summary>[K] with the Age panel open: the tree opens at the research the Age's first unmet core milestone needs next.</summary>
    private void FocusAgeSuggestion()
    {
        if (AgePanelVisible && _age.Panel.SuggestedNodeKey is int key and >= 0) ShowInTree(new ResearchNodeId(key), closeInfo: false);
    }

    // ================================================================== registration

    /// <summary>Registers the last ImGui item's rect (optionally cut to the current window's visible region).</summary>
    private void RegisterItem(InfoSubject subject, string label, bool passive = false, bool clipToWindow = false)
    {
        System.Numerics.Vector2 a = ImGui.GetItemRectMin(), b = ImGui.GetItemRectMax();
        RegisterRect(new RectD(a.X, a.Y, b.X - a.X, b.Y - a.Y), subject, label, passive, clipToWindow);
    }

    /// <summary>Registers a screen rect (a row painted as several items — UR-3's statement and value column).</summary>
    private void RegisterRect(RectD r, InfoSubject subject, string label, bool passive = false, bool clipToWindow = false)
    {
        if (clipToWindow)
        {
            RectD win = CurrentWindowRect();
            if (!r.Intersects(win)) return;
            r = InfoRegistry.Intersect(r, win);
        }
        InfoRegistry.Add(r, subject, label, passive);
    }

    /// <summary>Registers a DrawList painter's regions, cut to the current (scrolled) window's visible rect.</summary>
    private void RegisterInWindow(IReadOnlyList<InfoHit> hits) => InfoRegistry.AddRange(hits, CurrentWindowRect());

    /// <summary>What of the current window is actually shown (and hovered): its draw list's clip rect — the window's
    /// inner region cut by its parent's, so a region under a scrolled body's padding edge is not registered.</summary>
    private static RectD CurrentWindowRect()
    {
        ImDrawListPtr list = ImGui.GetWindowDrawList();
        System.Numerics.Vector2 a = list.GetClipRectMin(), b = list.GetClipRectMax();
        return new RectD(a.X, a.Y, Math.Max(0, b.X - a.X), Math.Max(0, b.Y - a.Y));
    }

    private void RegisterFormationTokens()
    {
        if (_lensFrame is null) return;
        double ts = UnitSelection.TokenSize(_lensFrame.Zoom) + 4;
        foreach (Sim.Ui.World.UnitPlacement up in _lensFrame.UnitPlacements)
            InfoRegistry.Add(new RectD(up.X - ts, up.Y - ts * 0.8, 2 * ts, 1.6 * ts), InfoSubject.OfFormation(up.Id), "formation", passive: true);
    }

    // ================================================================== the card over the map (the right-hand column)

    /// <summary>Draws the pinned card in the context panel's place when one is open. True when it did.</summary>
    private bool DrawInfoContext()
    {
        if (!_inspector.IsOpen) return false;
        IReadOnlyList<Sim.Core.Kernel.OrderRecord> queued = _session.QueuedOrders();
        _inspector.Ensure(_world, _session.Config, UiPlayer.Empire, ActionQueryContext.ForNextStep(_world, _eraTable, queued),
            _session.Names.Name, queued.Count);
        _drawListBackend ??= new Sim.Ui.ImGuiIntegration.DrawListImGuiBackend(_fonts);

        // UR-3 geometry (as DrawContextPanel): the placed, scaled context rect; the header row's title in the era's
        // title face; the close button flush right.
        PanelRect panel = ContextRect;
        float s = Scale;
        BeginChrome(panel);
        var element = new ChromeElement(panel, RulePlacement.UnderHeaderRow);
        DrawPanelFurniture(element);
        float frameHeight = ImGui.GetFrameHeight();
        ScreenRect close = ChromeGeometry.CloseButton(element, frameHeight, s);
        ScreenRect head = ChromeGeometry.HeaderRow(element, frameHeight, s);
        ImGui.SetCursorScreenPos(new System.Numerics.Vector2(head.X + 2f * s, head.Y));
        TitleRow("DETAILS", close.X - head.X - 10f * s, frameHeight);
        PlaceCursor(panel, close);
        ImGui.PushStyleVar(ImGuiStyleVar.ButtonTextAlign,
            new System.Numerics.Vector2(ChromeGeometry.CloseGlyphAlign, ChromeGeometry.CloseGlyphAlign));
        bool closeClicked = ImGui.Button(ChromeGeometry.CloseGlyph + "##info-close", Size(close));
        Controls.Record("info-close");
        ImGui.PopStyleVar();

        ImGui.SetCursorPosY(ChromeGeometry.ContentTop(element, frameHeight, s) - panel.Y);
        var bodyKey = (_openSection, _inspector.Subject);
        if (bodyKey != _infoBodyKey) ImGui.SetNextWindowScroll(System.Numerics.Vector2.Zero);
        _infoBodyKey = bodyKey;
        ImGui.BeginChild("info-body", System.Numerics.Vector2.Zero, ImGuiChildFlags.None, ImGuiWindowFlags.NoBackground);
        System.Numerics.Vector2 origin = ImGui.GetCursorScreenPos();
        float width = Math.Max(1f, ImGui.GetContentRegionAvail().X - 4f);
        var card = new Sim.Ui.Render.DrawList();
        double height = _inspector.Paint(card, _drawListBackend, _frameTheme, origin.X, origin.Y, width, showClose: false);
        ImGui.InvisibleButton("##info-card", new System.Numerics.Vector2(width, (float)Math.Max(1.0, height)));
        Controls.Record("info-card");
        RectD visible = CurrentWindowRect();
        if (ImGui.IsItemActivated())
        {
            System.Numerics.Vector2 p = ImGui.GetIO().MousePos;
            HandleInfo(_inspector.Click(p.X, p.Y, visible));
        }
        _drawListBackend.Render(ImGui.GetWindowDrawList(), card);
        ImGui.EndChild();
        ImGui.End();
        if (closeClicked) _inspector.Close();
        return true;
    }

    // ================================================================== the card in the research screen (the detail column)

    private RectD InfoOverlayRect()
    {
        // UR-4: the bars and the detail column are sized by the UI scale (instance values): the card takes the detail
        // panel's place — docked beside the tree, or the drawer's place over its right edge.
        if (_progression is { } screen) return screen.DetailRect;
        double w = _viewportWidth, h = _viewportHeight;
        double top = (Sim.Ui.Progression.ProgressionScreen.LensBarRef + Sim.Ui.Progression.ProgressionScreen.TabBarRef) * Scale;
        double dw = Sim.Ui.Progression.ProgressionScreen.DetailRef * Scale;
        return new RectD(w - dw, top, dw, Math.Max(100, h - top));
    }

    /// <summary>Paints the pinned card over the research screen's detail column, and registers the screen's regions
    /// (those it covers excepted).</summary>
    private void PaintInfoInProgression(Sim.Ui.Render.DrawList list, double width, double height)
    {
        RectD panel = InfoOverlayRect();
        if (_inspector.IsOpen && _drawListBackend is not null)
        {
            IReadOnlyList<Sim.Core.Kernel.OrderRecord> queued = _session.QueuedOrders();
            _inspector.Ensure(_world, _session.Config, UiPlayer.Empire, ActionQueryContext.ForNextStep(_world, _eraTable, queued),
                _session.Names.Name, queued.Count);
            Sim.Ui.Theme.PanelFrame.Paint(list, panel, _frameTheme, 7000, Sim.Ui.Theme.FrameKind.Panel);
            RectD body = panel.Inset(18);
            list.PushClip(body);
            _infoContentHeight = _inspector.Paint(list, _drawListBackend, _frameTheme, body.X, body.Y - _inspector.Scroll, body.W, showClose: true);
            list.PopClip();
        }
        if (_progression is not null) InfoRegistry.AddRange(_progression.InfoHits, null, _inspector.IsOpen ? panel : null);
        _ = width;
        _ = height;
    }

    // ================================================================== the hover hint

    /// <summary>The Age panel's regions (painted on the foreground list, so registered last: top-most), then the hint
    /// beside the pointer when it is over an inspectable region and not over the card itself.</summary>
    private void DrawInfoHint()
    {
        if (AgePanelVisible && !_progressionOpen) InfoRegistry.AddRange(_age.InfoHits);
        InfoHint = null;
        InfoHintRect = null;
        if (!_active) return;
        System.Numerics.Vector2 p = ImGui.GetIO().MousePos;
        if (p.X < 0 || p.Y < 0 || p.X >= _viewportWidth || p.Y >= _viewportHeight) return;
        if (_inspector.IsOpen)
        {
            PanelRect ctx = ContextRect;
            if (_progressionOpen ? InfoOverlayRect().Contains(p.X, p.Y)
                : p.X >= ctx.X && p.X < ctx.X + ctx.Width && p.Y >= ctx.Y && p.Y < ctx.Y + ctx.Height) return;
        }
        if (InfoRegistry.HitTest(p.X, p.Y) is null) return;
        _drawListBackend ??= new Sim.Ui.ImGuiIntegration.DrawListImGuiBackend(_fonts);
        var d = new Sim.Ui.Render.DrawList();
        InfoHintRect = InfoPanel.PaintHint(d, _drawListBackend, _frameTheme, p.X, p.Y, _viewportWidth, _viewportHeight);
        InfoHint = InfoPanel.HintText;
        _drawListBackend.Render(ImGui.GetForegroundDrawList(), d);
    }
}
