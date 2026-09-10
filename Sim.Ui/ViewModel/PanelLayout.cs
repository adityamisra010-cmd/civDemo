namespace Sim.Ui.ViewModel;

/// <summary>One panel's rectangle, in screen pixels.</summary>
public readonly record struct PanelRect(string Title, float X, float Y, float Width, float Height);

/// <summary>
/// T4.18 — THE GAME SCREEN, as data.
///
/// WHAT WAS WRONG. Five windows — HUD, Graphs, Market, Annals, Trade — were
/// permanently open, non-overlapping, and between them covered essentially the
/// whole 1280×800 viewport. The HUD alone was 440×776: a third of the width,
/// the full height, always. The world the game is ABOUT was whatever pixels the
/// dashboard had not claimed, and every subsystem shouted at equal volume
/// whether or not the director was thinking about it. The layout was proven
/// non-overlapping, which is exactly the wrong success criterion — it certified
/// a full screen as correct.
///
/// THE PRINCIPLE THIS REPLACES IT WITH. The simulation can know everything; the
/// screen should show only what the player needs right now. So the world gets
/// the screen, a thin band of always-true status sits above it, the verbs sit
/// below it, and every analytical surface — policy, economy, population,
/// market, annals, trends — is ONE contextual panel that the director opens and
/// closes. One mechanism, one at a time, and closing it returns a clean world.
///
/// NOTHING WAS REMOVED. Every line the old five windows drew still exists; it
/// moved behind the section that owns it. Fewer things visible at once, not
/// fewer capabilities.
///
/// FIXED CHROME, NOT FLOATING WINDOWS. The bars and the context panel are
/// positioned every frame rather than at first use: they are the frame of the
/// game, and a frame that can be dragged into the middle of the map and lost is
/// not a frame. The old rects were FirstUseEver defaults, which is why panel
/// overlap was a thing that could be re-discovered at all.
///
/// TARGET RESOLUTION 1280×800 — the project's default window, and what every
/// gate build opens at. SimUiGame reads DesignWidth/Height from HERE, so the
/// tested layout and the actual window cannot drift apart.
/// </summary>
public static class PanelLayout
{
    public const int DesignWidth = 1280;
    public const int DesignHeight = 800;

    /// <summary>Outer margin and inter-element gap, one number so the spacing
    /// is uniform by construction rather than by five separate decisions.</summary>
    public const float Margin = 12;

    /// <summary>The always-true world state: year, population, settlements,
    /// food. Full width, deliberately shallow — status is a band, not a
    /// column.</summary>
    public static readonly PanelRect Status = new("##status", 0, 0, DesignWidth, 48);

    /// <summary>The verbs and the section navigation. Full width at the foot,
    /// where a strategy game's controls live.</summary>
    public static readonly PanelRect Command = new("##command", 0, DesignHeight - 56, DesignWidth, 56);

    /// <summary>
    /// The selected settlement, floating over the map at the top left: the one
    /// piece of contextual detail worth keeping visible while looking at the
    /// world, because selection is how every other panel is aimed.
    /// </summary>
    /// T4.19 lane B: 104 → 132 px tall. The card carries a third data line —
    /// happiness and grievance, the two figures the packet makes clickable —
    /// and at the 17 px numeric face with 7 px item spacing three data lines
    /// under the title end at y ≈ 110 inside the window, past the old 104.
    /// Still under a tenth of the map band (pinned).
    public static readonly PanelRect Selection =
        new("##selection", Margin, Status.Height + Margin, 268, 132);

    /// <summary>
    /// The contextual panel — policy, economy, population, market, annals or
    /// trends, whichever is open, and NOTHING when none is. Right-hand column,
    /// between the bars.
    /// </summary>
    public static readonly PanelRect Context = new("##context",
        DesignWidth - 396 - Margin, Status.Height + Margin,
        396, DesignHeight - Status.Height - Command.Height - (Margin * 2));

    /// <summary>The chrome that is always on screen. The context panel is NOT
    /// here: its whole point is that it is usually absent.</summary>
    public static IReadOnlyList<PanelRect> Always { get; } = [Status, Command, Selection];

    /// <summary>Everything that can be on screen at once — the chrome plus one
    /// open section.</summary>
    public static IReadOnlyList<PanelRect> All { get; } = [Status, Command, Selection, Context];

    /// <summary>
    /// The map area left clear when no section is open: the full viewport less
    /// the two bars. The Selection card floats INSIDE this — it is a small
    /// overlay on the world, not a column carved out of it — so it is not
    /// subtracted here.
    /// </summary>
    public static float ClearMapArea =>
        DesignWidth * (DesignHeight - Status.Height - Command.Height);

    /// <summary>The same, with a section open.</summary>
    public static float MapAreaWithContextOpen =>
        (DesignWidth - Context.Width - Margin) * (DesignHeight - Status.Height - Command.Height);

    /// <summary>Strict-interior intersection: true iff the shared area is
    /// positive. Edge-adjacent rects (shared boundary only) do not overlap.</summary>
    public static bool Overlap(in PanelRect a, in PanelRect b) =>
        a.X < b.X + b.Width && b.X < a.X + a.Width &&
        a.Y < b.Y + b.Height && b.Y < a.Y + a.Height;
}
