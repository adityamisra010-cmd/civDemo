namespace Sim.Ui.ViewModel;

/// <summary>One panel's first-use default rectangle, in screen pixels.</summary>
public readonly record struct PanelRect(string Title, float X, float Y, float Width, float Height);

/// <summary>
/// T3.9a-b item 4: the default panel layout as DATA — pure rects, no ImGui,
/// proven non-overlapping headless (PanelLayoutTests) so the director's gate
/// never re-discovers panel overlap at the default window size.
///
/// TARGET RESOLUTION: 1280×800 — the project's default window size, the
/// honest anchor: it is what every gate build opens at. SimUiGame's ctor
/// reads DesignWidth/Height from HERE, so the tested layout and the actual
/// window cannot drift apart. A user-resized window keeps these first-use
/// defaults (panels stay within the top-left 1280×800 region until moved).
///
/// GEOMETRY: 12 px outer margin, ≥12 px inter-panel gaps. The HUD takes the
/// left column at a FIXED default size and scrolls when its content exceeds
/// it — the pre-polish HUD was AlwaysAutoResize with no height cap, so it
/// grew past the 800 px window bottom and under the Annals at y=560 (the
/// T3.9a gate Q4 overlap). Graphs over Market take the right column; the
/// Annals sits bottom-centre, narrow enough to leave the map readable above
/// it and collapsible to its title bar for routine play.
///
/// These rects are FIRST-USE defaults only (ImGuiCond.FirstUseEver at every
/// consumer): the user's in-session drags/resizes/collapses always win.
/// </summary>
public static class PanelLayout
{
    public const int DesignWidth = 1280;
    public const int DesignHeight = 800;

    public static readonly PanelRect Hud = new("civ-sim", 12, 12, 440, 776);
    public static readonly PanelRect Graphs = new("Graphs", 828, 12, 440, 448);
    public static readonly PanelRect Market = new("Market", 828, 472, 440, 316);
    public static readonly PanelRect Annals = new("Annals", 464, 560, 352, 228);

    public static IReadOnlyList<PanelRect> All { get; } = [Hud, Graphs, Market, Annals];

    /// <summary>Strict-interior intersection: true iff the shared area is
    /// positive. Edge-adjacent rects (shared boundary only) do not overlap.</summary>
    public static bool Overlap(in PanelRect a, in PanelRect b) =>
        a.X < b.X + b.Width && b.X < a.X + a.Width &&
        a.Y < b.Y + b.Height && b.Y < a.Y + a.Height;
}
