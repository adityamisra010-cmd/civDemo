using ImGuiNET;
using Microsoft.Xna.Framework.Input;
using Sim.Core.State;
using Sim.Ui.ViewModel;
using Sim.Ui.World;
using RectD = Sim.Ui.Render.RectD;

namespace Sim.Ui;

/// <summary>
/// M5 S1d (D-051 §5, ADR-036) — THE SETTLEMENT-PLACEMENT MODE on the map. Entered from the action surface's "Choose a
/// site..." (the Found Settlement action the query lists for an owned source whose source-side arms hold) or with F (the
/// same block's source); left with Escape, the bar's Cancel, an End Turn, or once an order was appended.
/// <list type="bullet">
/// <item><b>Hover</b>: the KNOWN cell under the pointer over open map is judged by THE predicate (<see cref="PlacementMode"/>,
///   through <see cref="FoundSettlementOrderFactory"/>), its footprint is painted in the verdict's ink and a painted hint
///   beside the pointer says why (never an ImGui tooltip); once the pointer rests on it, the route from the source is drawn
///   and the P8 place card — judged from the source: the predicate arm by arm, the route and its cost, the party and the
///   provisions — is docked in the right-hand column. Undiscovered ground is neither hoverable nor selectable (P7).</item>
/// <item><b>Two steps</b>: a click SELECTS a place (an illegal one too, so its reason stays on the bar); Confirm (or Enter)
///   asks <see cref="UiSession.EmitFoundSettlement"/> — the ONE guarded emitter — which appends nothing for an illegal
///   place and the bar says why.</item>
/// </list>
/// UI state only: hovering, selecting, the camera and the card change no hash and queue nothing (pinned).
/// </summary>
public sealed partial class GameUi
{
    private readonly PlacementMode _placement = new();
    private int _placementCard = -1;
    private PanelRect _placementBarRect;

    /// <summary>The placement mode (UI state).</summary>
    public PlacementMode Placement => _placement;

    /// <summary>The placement hint painted last frame (its lines joined), or null.</summary>
    public string? PlacementHint { get; private set; }

    /// <summary>Where the placement hint was painted last frame.</summary>
    public RectD? PlacementHintRect { get; private set; }

    /// <summary>Where the placement bar was drawn last frame (empty while the mode is off).</summary>
    public PanelRect PlacementBarRect => _placementBarRect;

    /// <summary>Enters the placement mode from <paramref name="source"/> when the query lists the Found Settlement action for
    /// it (the action surface's model); true when it did. UI state only.</summary>
    public bool BeginPlacement(int source)
    {
        if (_actionModel is not { } model) return false;
        foreach (ActionDescriptor a in model.Actions)
        {
            if (a.Domain != ActionDomain.Colonization || a.Id != source) continue;
            if (_placement.Active) EndPlacement();
            _placement.Begin(source, _session.Names.Name(source), a.Detail ?? "");
            SelectUnit(-1);   // a click on the map now chooses a place: the formation card is put away
            return true;
        }
        return false;
    }

    /// <summary>F: enters the mode from the action surface's founding source (the selection when listed, else the capital,
    /// else the first listed), or leaves it.</summary>
    private void TogglePlacement()
    {
        if (_placement.Active) { EndPlacement(); return; }
        if (_actionModel?.Founding is { } founding) BeginPlacement(founding.Source.Value);
    }

    /// <summary>Leaves the mode; the place card it docked is put away with it.</summary>
    private void EndPlacement()
    {
        int source = _placement.Source;
        _placement.Cancel();
        if (_inspector.Subject is { Kind: InfoKind.Cell } subject && subject.Settlement == source && source >= 0) _inspector.Close();
        _placementCard = -1;
        _placementBarRect = default;
    }

    /// <summary>Escape cancels and Enter confirms while the mode is on (before the info card's Escape: the card is the
    /// mode's). Not while the research screen or the modal Age flow has the keys. True when the key was the mode's.</summary>
    private bool UpdatePlacementKeys(KeyboardState keyboard, ImGuiIOPtr io)
    {
        if (!_placement.Active || !IsActive || _progressionOpen || _age.FlowOpen || io.WantCaptureKeyboard) return false;
        if (Edge(keyboard, Keys.Escape)) { EndPlacement(); return true; }
        if (Edge(keyboard, Keys.Enter)) { ConfirmPlacement(); return true; }
        return false;
    }

    /// <summary>The second step: the session's guarded emitter appends the order, or nothing (the bar then says why).</summary>
    private void ConfirmPlacement()
    {
        int source = _placement.Source;
        string name = _placement.SourceName;
        if (!_placement.Confirm(_session)) return;
        if (_inspector.Subject is { Kind: InfoKind.Cell } subject && subject.Settlement == source) _inspector.Close();
        _placementCard = -1;
        _placementBarRect = default;
        SaveSession();
        RefreshHud();
        _notices.Show(["Settlers from " + name + " will set out at End Turn; their settlement is founded on the next turn."], "SETTLERS ORDERED");
    }

    /// <summary>The first step: a plain map click selects the place under the pointer (a known one only).</summary>
    private void PlacementClick(int x, int y)
    {
        if (_world.Terrain is not { } terrain) return;
        _placement.Select(_session, MapCells.At(_camera, _mapRect, x, y, terrain.Size));
    }

    /// <summary>The terrain cell under the pointer when it sees OPEN MAP (the place card's own conditions: inside the map
    /// rect, not over an ImGui window, the legend, the docked Age panel or the modal Age flow), else -1.</summary>
    private int CellUnderPointer()
    {
        if (!_active || _world.Terrain is not { } terrain || _age.FlowOpen) return -1;
        ImGuiIOPtr io = ImGui.GetIO();
        double px = io.MousePos.X, py = io.MousePos.Y;
        if (!_mapRect.Contains(px, py) || io.WantCaptureMouse || OnLegend(px, py)) return -1;
        if (AgePanelVisible && AgePanelRect().Contains(px, py)) return -1;
        return MapCells.At(_camera, _mapRect, px, py, terrain.Size);
    }

    /// <summary>The frame's placement work after the lens: the hovered place (judged), the docked card, the map marks.</summary>
    private void DrawPlacementOverMap()
    {
        if (!_placement.Active) return;
        _placement.Hover(_session, CellUnderPointer());
        int card = _placement.CardCell;
        if (card != _placementCard)
        {
            _placementCard = card;
            if (card >= 0)
            {
                InfoSubject subject = InfoSubject.OfCell(card, _placement.Source);
                if (_inspector.Subject != subject)
                {
                    _inspector.Close();   // the mode's card replaces whatever was docked; no history of the cells crossed
                    OpenInfo(subject);
                }
            }
        }
        _surface = FrameSurface.Placement;
        var d = new Sim.Ui.Render.DrawList();
        Camera cam = _camera;
        RectD map = _mapRect;
        PlacementOverlay.PaintMap(d, _frameTheme, _mapInk, (x, y) => cam.WorldToScreen(x, y, map), _world.Terrain?.Size ?? 0,
            _session.Sites?.Frontier.Lattice, _placement, Scale);
        Backend.Render(ImGui.GetBackgroundDrawList(), d);
    }

    /// <summary>The hint beside the pointer over a hovered place (painted on the foreground list, never a tooltip). Not
    /// while Shift is held: Shift+click inspects, and its own hint says so. True when it painted.</summary>
    private bool DrawPlacementHint()
    {
        PlacementHint = null;
        PlacementHintRect = null;
        if (!_placement.Active || _placement.Hovered < 0 || _shift || _progressionOpen || !_active) return false;
        IReadOnlyList<(string Text, Sim.Ui.Art.ParchmentPalette.Rgba Ink)> lines = PlacementOverlay.HintLines(_placement, _frameTheme, _session.Sites?.Frontier.Lattice);
        if (lines.Count == 0) return false;
        System.Numerics.Vector2 p = ImGui.GetIO().MousePos;
        _surface = FrameSurface.Placement;
        var d = new Sim.Ui.Render.DrawList();
        double size = Sim.Ui.Theme.TypeScale.Px(_frameTheme, Sim.Ui.Theme.TypeRole.Body, Sim.Ui.Render.FontRole.Body) * Scale;
        PlacementHintRect = PlacementOverlay.PaintHint(d, Backend, _frameTheme, p.X, p.Y, _viewportWidth, _viewportHeight, lines, size);
        var joined = new List<string>(lines.Count);
        foreach ((string text, _) in lines) joined.Add(text);
        PlacementHint = string.Join("\n", joined);
        Backend.Render(ImGui.GetForegroundDrawList(), d);
        return true;
    }

    /// <summary>
    /// THE PLACEMENT BAR — over the top of the map: the source and its party, what is chosen and whether it is legal (or why
    /// the last Confirm appended nothing), and two buttons: Confirm (enabled only for a chosen place the predicate answers
    /// Legal for) and Cancel. Both recorded (<see cref="UiControls"/>).
    /// </summary>
    private void DrawPlacementBar()
    {
        if (!_placement.Active) { _placementBarRect = default; return; }
        _surface = FrameSurface.Placement;
        float s = Scale, margin = PanelLayout.Margin * s;
        float frameH = ImGui.GetFrameHeight();
        float padX = ImGui.GetStyle().WindowPadding.X;
        RectD map = _mapRect;
        float left = (float)Math.Max(map.X + margin, _selectionRect.X + _selectionRect.Width + margin);
        float width = (float)Math.Max(240f * s, Math.Min(560f * s, map.Right - margin - left));
        float x = (float)Math.Max(left, Math.Min(map.CenterX - width / 2f, map.Right - margin - width));
        float wrap = width - 2f * padX;

        var lines = new List<(string Text, Sim.Ui.Art.ParchmentPalette.Rgba Ink)> { (Cap(_placement.PartyLine) + ".", _frameTheme.Ink.Text) };
        if (_placement.Refusal is { } refusal) lines.Add((refusal, _frameTheme.TextInk.Danger));
        else if (_placement.Selected >= 0 && _placement.SelectedVerdict is { } v)
        {
            int size = _world.Terrain?.Size ?? 1;
            string where = "Chosen: the place at " + (_placement.Selected % size).ToString(System.Globalization.CultureInfo.InvariantCulture)
                + ", " + (_placement.Selected / size).ToString(System.Globalization.CultureInfo.InvariantCulture) + " - ";
            lines.Add((where + PlacementMode.ReasonText(v, _placement.SourceName, _placement.SeesAll) + ".", PlacementOverlay.VerdictInk(_frameTheme, v)));
            if (_session.Sites is { } site && _placement.RouteCell == _placement.Selected && _placement.RouteLine(site.Frontier.Lattice) is { } route)
                lines.Add((route + ".", _frameTheme.Ink.TextSoft));
        }
        else lines.Add(("Click a place you have discovered on the map to choose it; Escape cancels.", _frameTheme.Ink.TextSoft));

        ImFontPtr? body = RoleFont(Sim.Ui.Theme.TypeRole.Body);
        float spacing = ImGui.GetStyle().ItemSpacing.Y;
        var probe = new ChromeElement(new PanelRect("##placement-bar", 0, 0, width, 100), RulePlacement.UnderHeaderRow);
        float h = ChromeGeometry.ContentTop(probe, frameH, s);
        foreach ((string text, _) in lines) h += WrappedHeight(body, text, wrap) + spacing;
        h += frameH + spacing + margin;
        _placementBarRect = new PanelRect("##placement-bar", x, (float)map.Y + margin, width, (float)Math.Ceiling(h));
        var element = new ChromeElement(_placementBarRect, RulePlacement.UnderHeaderRow);

        BeginChrome(_placementBarRect);
        DrawPanelFurniture(element);
        ScreenRect head = ChromeGeometry.HeaderRow(element, frameH, s);
        ImGui.SetCursorScreenPos(new System.Numerics.Vector2(head.X + padX - margin, head.Y));
        TitleRow("Found a settlement from " + _placement.SourceName, width - 2f * padX, frameH);
        ImGui.SetCursorPosY(ChromeGeometry.ContentTop(element, frameH, s) - _placementBarRect.Y);
        foreach ((string text, Sim.Ui.Art.ParchmentPalette.Rgba ink) in lines)
        {
            ImGui.PushTextWrapPos(width - padX);
            ImGui.PushStyleColor(ImGuiCol.Text, Col(ink));
            Text(text);
            ImGui.PopStyleColor();
            ImGui.PopTextWrapPos();
        }
        bool can = _placement.CanConfirm;
        if (!can) ImGui.BeginDisabled();
        bool confirm = Button("Confirm##found-confirm");
        Controls.Record("found-confirm");
        if (!can) ImGui.EndDisabled();
        ImGui.SameLine();
        bool cancel = Button("Cancel##found-cancel");
        Controls.Record("found-cancel");
        ImGui.End();
        if (cancel) EndPlacement();
        else if (confirm) ConfirmPlacement();
    }

    private static string Cap(string s) => s.Length == 0 ? s : char.ToUpperInvariant(s[0]) + s[1..];
}
