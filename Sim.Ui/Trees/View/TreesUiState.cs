using Sim.Ui.Render;

namespace Sim.Ui.Trees.View;

public enum TreesTab { Trees, Ages, Gallery }

/// <summary>What the canvas emphasises around the selected node.</summary>
public enum FocusMode
{
    None,
    /// <summary>Everything upstream along layering edges — "why is this where it is?".</summary>
    Prerequisites,
    /// <summary>Everything downstream — "what does this lead to?".</summary>
    Downstream,
    /// <summary>Both: the node's full dependency lineage.</summary>
    Lineage,
}

/// <summary>Filtered-out nodes are either dimmed in place (the graph keeps its shape) or hidden.</summary>
public enum FilterMode { Dim, Hide }

/// <summary>
/// PAN AND ZOOM for the graph canvas. Pure arithmetic over world units (the layout's) and
/// screen pixels; the world point under the cursor stays put when zooming, which is what
/// makes zoom feel anchored.
/// </summary>
public sealed class GraphCamera
{
    public const double MinZoom = 0.3;
    public const double MaxZoom = 2.4;

    public double Zoom { get; private set; } = 1.0;
    /// <summary>The world point shown at the viewport's top-left corner.</summary>
    public double OffsetX { get; private set; }
    public double OffsetY { get; private set; }

    public (double X, double Y) WorldToScreen(double wx, double wy, RectD viewport) =>
        (viewport.X + (wx - OffsetX) * Zoom, viewport.Y + (wy - OffsetY) * Zoom);

    public (double X, double Y) ScreenToWorld(double sx, double sy, RectD viewport) =>
        (OffsetX + (sx - viewport.X) / Zoom, OffsetY + (sy - viewport.Y) / Zoom);

    public void Pan(double dxScreen, double dyScreen)
    {
        OffsetX -= dxScreen / Zoom;
        OffsetY -= dyScreen / Zoom;
    }

    public void ZoomAt(double sx, double sy, RectD viewport, double factor)
    {
        (double wx, double wy) = ScreenToWorld(sx, sy, viewport);
        Zoom = System.Math.Clamp(Zoom * factor, MinZoom, MaxZoom);
        OffsetX = wx - (sx - viewport.X) / Zoom;
        OffsetY = wy - (sy - viewport.Y) / Zoom;
    }

    /// <summary>Fit a world rectangle into the viewport (with a margin), centred.</summary>
    public void Fit(double wx, double wy, double ww, double wh, RectD viewport, double margin = 12.0)
    {
        double zx = (viewport.W - 2 * margin) / System.Math.Max(1.0, ww);
        double zy = (viewport.H - 2 * margin) / System.Math.Max(1.0, wh);
        Zoom = System.Math.Clamp(System.Math.Min(zx, zy), MinZoom, MaxZoom);
        CenterOn(wx + ww / 2.0, wy + wh / 2.0, viewport);
    }

    public void CenterOn(double wx, double wy, RectD viewport)
    {
        OffsetX = wx - viewport.W / 2.0 / Zoom;
        OffsetY = wy - viewport.H / 2.0 / Zoom;
    }

    public void Set(double zoom, double offsetX, double offsetY)
    {
        Zoom = System.Math.Clamp(zoom, MinZoom, MaxZoom);
        OffsetX = offsetX;
        OffsetY = offsetY;
    }
}

/// <summary>A UI action — the ONLY vocabulary a click can produce. Every action changes UI
/// state (what is shown), never the world. Host actions (close, reload, preview-step) are
/// returned to the host, which owns those concerns.</summary>
public abstract record TreesAction;
public sealed record SetTabAction(TreesTab Tab) : TreesAction;
public sealed record SetLensAction(string? LensId) : TreesAction;
public sealed record ToggleStateFilterAction(string StateId) : TreesAction;
public sealed record SetAgeFilterAction(string? AgeId) : TreesAction;
public sealed record SetTypeFilterAction(string? TypeId) : TreesAction;
public sealed record SetFilterModeAction(FilterMode Mode) : TreesAction;
public sealed record ClearFiltersAction : TreesAction;
public sealed record SelectNodeAction(int? Node, bool Center = false) : TreesAction;
public sealed record SetFocusAction(FocusMode Mode) : TreesAction;
public sealed record ZoomAction(double Factor) : TreesAction;
public sealed record FitAction : TreesAction;
public sealed record SelectMilestoneAction(string? MilestoneId) : TreesAction;
/// <summary>Show another Age in the Age view (null = the current Age).</summary>
public sealed record SetViewedAgeAction(string? AgeId) : TreesAction;
public sealed record ToggleLegendAction : TreesAction;
/// <summary>Jump from an Age milestone to its node in the graph.</summary>
public sealed record OpenNodeAction(string NodeId) : TreesAction;
/// <summary>Host: step the PLACEHOLDER state script (preview only; never reaches the simulation).</summary>
public sealed record PreviewStepAction(int Delta) : TreesAction;
public sealed record ReloadContentAction : TreesAction;
public sealed record CloseAction : TreesAction;
/// <summary>Host: give keyboard focus to the search box.</summary>
public sealed record FocusSearchAction : TreesAction;

/// <summary>
/// THE TREES SCREEN'S UI STATE — tab, lens, filters, search, selection, focus, camera.
/// Presentation state only: it has no reference to the world or to a state source, so
/// nothing done here can change what the simulation holds.
/// </summary>
public sealed class TreesUiState
{
    public TreesTab Tab { get; set; } = TreesTab.Trees;
    /// <summary>The focused lens, or null for the whole civilization.</summary>
    public string? Lens { get; set; }
    public FilterMode FilterMode { get; set; } = FilterMode.Dim;
    /// <summary>Enabled state filters (empty = every state), kept sorted.</summary>
    public SortedSet<string> StateFilter { get; } = new(StringComparer.Ordinal);
    public string? AgeFilter { get; set; }
    public string? TypeFilter { get; set; }
    public string Search { get; set; } = "";
    public int? Selected { get; set; }
    public int? Hovered { get; set; }
    public FocusMode Focus { get; set; } = FocusMode.None;
    public string? SelectedMilestone { get; set; }
    /// <summary>The Age shown in the Age view, or null for the current Age.</summary>
    public string? ViewedAge { get; set; }
    /// <summary>The right-hand panel shows the legend (true) or the selected node's details.</summary>
    public bool LegendOpen { get; set; } = true;
    /// <summary>Scroll offset of the details panel, in pixels.</summary>
    public double DetailScroll { get; set; }
    public GraphCamera Camera { get; } = new();
    public bool CameraInitialised { get; set; }

    public bool AnyFilter => StateFilter.Count > 0 || AgeFilter is not null || TypeFilter is not null || Search.Length > 0;

    /// <summary>Apply a UI action. Returns false for host actions (close, reload, preview
    /// step, search focus), which the caller handles.</summary>
    public bool Apply(TreesAction action, TreeGraph graph, TreeLayoutResult layout, RectD canvas)
    {
        switch (action)
        {
            case SetTabAction t: Tab = t.Tab; return true;
            case SetLensAction l:
                Lens = l.LensId;
                if (l.LensId is null) InitialView(layout, canvas);   // readable; the fit button fits everything
                else
                {
                    BandBox? band = layout.Bands.FirstOrDefault(b => b.LensId == l.LensId);
                    if (band is not null)
                    {
                        // Keep the zoom and the horizontal position; bring the lens band
                        // to the top of the canvas.
                        Camera.Set(Camera.Zoom, Camera.OffsetX, band.Y - 8.0 / Camera.Zoom);
                    }
                }
                return true;
            case ToggleStateFilterAction s:
                if (!StateFilter.Remove(s.StateId)) StateFilter.Add(s.StateId);
                return true;
            case SetAgeFilterAction a: AgeFilter = a.AgeId; return true;
            case SetTypeFilterAction ty: TypeFilter = ty.TypeId; return true;
            case SetFilterModeAction m: FilterMode = m.Mode; return true;
            case ClearFiltersAction:
                StateFilter.Clear(); AgeFilter = null; TypeFilter = null; Search = ""; Focus = FocusMode.None;
                return true;
            case SelectNodeAction s:
                Selected = s.Node is int n && n >= 0 && n < graph.Count ? n : null;
                if (Selected is null) Focus = FocusMode.None;
                LegendOpen = Selected is null;
                DetailScroll = 0;
                if (s.Center && Selected is int sel)
                {
                    NodeBox b = layout.Box(sel);
                    Camera.CenterOn(b.CenterX, b.CenterY, canvas);
                }
                return true;
            case SetFocusAction f:
                Focus = Selected is null ? FocusMode.None : (Focus == f.Mode ? FocusMode.None : f.Mode);
                return true;
            case ZoomAction z: Camera.ZoomAt(canvas.CenterX, canvas.CenterY, canvas, z.Factor); return true;
            case FitAction: FitAll(layout, canvas); return true;
            case SelectMilestoneAction m: SelectedMilestone = m.MilestoneId; return true;
            case SetViewedAgeAction va: ViewedAge = va.AgeId; SelectedMilestone = null; return true;
            case ToggleLegendAction: LegendOpen = !LegendOpen; return true;
            case OpenNodeAction o:
                if (graph.TryIndexOf(o.NodeId, out int idx))
                {
                    Tab = TreesTab.Trees;
                    Selected = idx;
                    LegendOpen = false;
                    DetailScroll = 0;
                    NodeBox b = layout.Box(idx);
                    Camera.CenterOn(b.CenterX, b.CenterY, canvas);
                }
                return true;
            default:
                return false;
        }
    }

    public void FitAll(TreeLayoutResult layout, RectD canvas)
    {
        Camera.Fit(0, 0, layout.Width, layout.Height, canvas);
        CameraInitialised = true;
    }

    /// <summary>The opening view: every lens band visible top to bottom at a READABLE zoom
    /// (never below <see cref="ReadableZoom"/>), starting from the left edge — the graph
    /// reads left to right, so the player pans toward what comes later.</summary>
    public void InitialView(TreeLayoutResult layout, RectD canvas)
    {
        double z = System.Math.Clamp((canvas.H - 8.0) / System.Math.Max(1.0, layout.Height), ReadableZoom, 1.0);
        Camera.Set(z, 0.0, 0.0);
        CameraInitialised = true;
    }

    /// <summary>Below this zoom node names stop being legible at the minimum label size.</summary>
    public const double ReadableZoom = 0.55;
}
