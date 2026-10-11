using ImGuiNET;
using Sim.Ui.ViewModel;
using Sim.Ui.World;

namespace Sim.Ui;

/// <summary>
/// M5 map workstream P9 — THE RESOURCE OVERLAY'S CONTROLS: one tab over the map's bottom-left corner ("OVERLAYS +"
/// unfolds the controls, "OVERLAYS -" folds them; it sits right of the left column's Age panel and formation card when they
/// show), a checkbox per overlay layer (each layer individually toggleable: the deposit channels, the siting score and its
/// two factors) and the opacity, in steps of 10 % from 10 % to 80 % (never opaque: the terrain always shows through).
/// LOOKING ONLY: every control is UI state — no order, no write; the lens paints the overlay and its key from the session's
/// bake (<see cref="UiSession.Overlay"/>).
/// </summary>
public sealed partial class GameUi
{
    /// <summary>The opacity steps (tenths): the lowest, the highest and the default.</summary>
    public const int OverlayOpacityMin = 1, OverlayOpacityMax = 8, OverlayOpacityDefault = 5;

    /// <summary>The overlays tab's control name.</summary>
    public const string OverlaysControl = "overlays";

    /// <summary>The opacity controls' names.</summary>
    public const string OverlayOpacityDown = "overlay-opacity-down", OverlayOpacityUp = "overlay-opacity-up";

    private bool[] _overlayShown = [];
    private int _overlayOpacity = OverlayOpacityDefault;
    private bool _overlayOpen;
    private PanelRect _overlayRect;

    /// <summary>P9: the overlay layers the controls list (the session's).</summary>
    public IReadOnlyList<OverlayLayer> OverlayLayers => _session.OverlayLayers;

    /// <summary>P9: whether overlay layer <paramref name="index"/> is shown.</summary>
    public bool OverlayShown(int index) => (uint)index < (uint)_overlayShown.Length && _overlayShown[index];

    /// <summary>P9: the overlay's opacity (0.1 .. 0.8).</summary>
    public double OverlayOpacity => _overlayOpacity / 10.0;

    /// <summary>P9: whether the overlay controls are unfolded.</summary>
    public bool OverlayControlsOpen => _overlayOpen;

    /// <summary>P9: the overlays tab's window as drawn in the last frame.</summary>
    public PanelRect OverlayTabRect => _overlayRect;

    private bool AnyOverlayShown()
    {
        foreach (bool b in _overlayShown) if (b) return true;
        return false;
    }

    private void EnsureOverlayState()
    {
        int n = _session.OverlayLayers.Count;
        if (_overlayShown.Length != n) _overlayShown = new bool[n];
    }

    /// <summary>The overlays tab's label — its name and the fold glyph of what a click does.</summary>
    private string OverlaysLabel() => "OVERLAYS " + (_overlayOpen ? ChromeGeometry.FoldUpGlyph : ChromeGeometry.UnfoldDownGlyph);

    /// <summary>The overlay controls' window: at the clear map's bottom-left, right of the left column's Age panel and
    /// formation card when they show; folded the tab alone, unfolded the controls above the tab's button.</summary>
    private PanelRect OverlayTab()
    {
        EnsureOverlayState();
        float s = Scale, margin = PanelLayout.Margin * s;
        float frameH = ImGui.GetFrameHeight(), gap = ImGui.GetStyle().ItemSpacing.Y;
        Sim.Ui.Render.RectD map = _mapRect;
        double left = map.X + margin;
        if (AgePanelVisible) left = Math.Max(left, AgePanelRect().Right + margin);
        if (_unitCard is not null) left = Math.Max(left, _unitCardRect.X + _unitCardRect.Width + margin);
        float bw = (float)Math.Ceiling(LabelWidth(OverlaysLabel()) + 2f * ImGui.GetStyle().FramePadding.X + 8f * s);
        float w = bw, h = frameH;
        if (_overlayOpen)
        {
            float box = frameH + ImGui.GetStyle().ItemInnerSpacing.X;
            foreach (OverlayLayer l in _session.OverlayLayers) w = Math.Max(w, box + LabelWidth(l.Name));
            w = Math.Max(w, LabelWidth(OpacityLabel()) + 2f * (frameH + 2f * ImGui.GetStyle().FramePadding.X) + 4f * ImGui.GetStyle().ItemSpacing.X);
            h += (_session.OverlayLayers.Count + 1) * (frameH + gap);
        }
        w = (float)Math.Ceiling(w + 2f * margin);
        h = (float)Math.Ceiling(h + 2f * margin);
        return new PanelRect("##overlays", (float)left, (float)map.Bottom - margin - h, w, h);
    }

    private string OpacityLabel() => "Opacity " + (_overlayOpacity * 10).ToString(System.Globalization.CultureInfo.InvariantCulture) + "%";

    /// <summary>P9: the overlay key's widest box at UI scale 1.</summary>
    public const float OverlayKeyMaxWidth = 400f;

    /// <summary>What the lens is asked to paint of the overlay this frame (null when no layer is shown).</summary>
    private OverlayPaint? OverlayForLens()
    {
        EnsureOverlayState();
        if (!AnyOverlayShown() || _session.Overlay is not { } bake) return null;
        return new OverlayPaint(bake, (bool[])_overlayShown.Clone(), OverlayOpacity, _session.OverlayStatus, OverlayKeyAt(OverlayTab()),
            id => _session.Names.Name(id));
    }

    /// <summary>Where the overlay's key goes: beside the overlays tab, its foot on the tab's, when the clear map leaves room
    /// there (left of where the legend can reach while it shows); otherwise above the tab.</summary>
    private OverlayKeyAnchor OverlayKeyAt(in PanelRect tab)
    {
        float s = Scale, margin = PanelLayout.Margin * s;
        Sim.Ui.Render.RectD map = _mapRect;
        double width = OverlayKeyMaxWidth * s;
        double limit = map.Right - margin - (_showLegend ? LegendMaxWidth * s + margin : 0);
        double room = limit - (tab.X + tab.Width + margin);
        return room >= 240 * s
            ? new OverlayKeyAnchor(tab.X + tab.Width + margin, tab.Y + tab.Height, Math.Min(width, room))
            : new OverlayKeyAnchor(tab.X, tab.Y - margin / 2, Math.Min(width, Math.Max(1, map.Right - margin - tab.X)));
    }

    /// <summary>
    /// P9: THE OVERLAY CONTROLS — the layer checkboxes and the opacity steps above the tab's button, which folds and
    /// unfolds them. Looking only: UI state, never an order.
    /// </summary>
    private void DrawOverlayTab()
    {
        EnsureOverlayState();
        if (_overlayShown.Length == 0) return;   // a world without terrain has no overlay
        _surface = FrameSurface.Overlay;
        _overlayRect = OverlayTab();
        float margin = PanelLayout.Margin * Scale;
        BeginChrome(_overlayRect);
        DrawPanelFurniture(new ChromeElement(_overlayRect, RulePlacement.TopEdge));
        ImGui.SetCursorPos(new System.Numerics.Vector2(margin, margin));
        bool fold = false, down = false, up = false;
        if (_overlayOpen)
        {
            IReadOnlyList<OverlayLayer> layers = _session.OverlayLayers;
            for (int i = 0; i < layers.Count; i++)
            {
                ImGui.SetCursorPosX(margin);
                bool shown = _overlayShown[i];
                if (Checkbox(layers[i].Name + "##" + layers[i].Control, ref shown)) _overlayShown[i] = shown;
                Controls.Record(layers[i].Control);
            }
            ImGui.SetCursorPosX(margin);
            Text(OpacityLabel());
            ImGui.SameLine();
            float frameH = ImGui.GetFrameHeight();
            ImGui.BeginDisabled(_overlayOpacity <= OverlayOpacityMin);
            down = Button("-##" + OverlayOpacityDown, new System.Numerics.Vector2(frameH, frameH));
            Controls.Record(OverlayOpacityDown);
            ImGui.EndDisabled();
            ImGui.SameLine();
            ImGui.BeginDisabled(_overlayOpacity >= OverlayOpacityMax);
            up = Button("+##" + OverlayOpacityUp, new System.Numerics.Vector2(frameH, frameH));
            Controls.Record(OverlayOpacityUp);
            ImGui.EndDisabled();
            ImGui.SetCursorPosX(margin);
        }
        fold = Button(OverlaysLabel() + "##" + OverlaysControl, new System.Numerics.Vector2(_overlayRect.Width - 2f * margin, ImGui.GetFrameHeight()));
        Controls.Record(OverlaysControl);
        ImGui.End();
        if (fold) _overlayOpen = !_overlayOpen;
        if (down) _overlayOpacity = Math.Max(OverlayOpacityMin, _overlayOpacity - 1);
        if (up) _overlayOpacity = Math.Min(OverlayOpacityMax, _overlayOpacity + 1);
    }
}
