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

    /// <summary>F3 (M5 hardening): the smallest window the game allows. The window is user-resizable and the chrome
    /// follows it, but the command bar's row (End Turn, the section buttons, the territory toggle) is laid out at fixed
    /// offsets; below this width its last control was drawn past the window's right edge. SimUiGame snaps the window
    /// back to this size; the playability gate's "narrow window" state plays the game at exactly this size and the
    /// harness fails any frame whose command-bar control leaves the window.</summary>
    public const int MinWindowWidth = 1080;   // measured: the territory toggle ends at x = 1056.6 (H1 fonts), + Margin
    public const int MinWindowHeight = 640;

    /// <summary>Outer margin and inter-element gap, one number so the spacing
    /// is uniform by construction rather than by five separate decisions.</summary>
    public const float Margin = 12;

    /// <summary>The always-true world state: year, population, settlements,
    /// food. Full width, deliberately shallow — status is a band, not a
    /// column. M5 polish UR-3: 48 → 52 px, so the 24 px primary figures and the
    /// frame-height chips (research, Age) sit above the bottom-edge rule instead
    /// of on it.</summary>
    public static readonly PanelRect Status = new("##status", 0, 0, DesignWidth, 52);

    /// <summary>The verbs and the section navigation. Full width at the foot,
    /// where a strategy game's controls live. M5 polish UR-3: 56 → 60 px for the
    /// 36 px End Turn (the primary verb) and its Margin above and below.</summary>
    public static readonly PanelRect Command = new("##command", 0, DesignHeight - 60, DesignWidth, 60);

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
    /// ADR-033 integration item 4: 132 → 160 px — happiness and grievance now sit on lines of their own,
    /// because side by side they overflowed the 268 px card ("happiness 100.0 grievanc…").
    /// M5 polish UR-3: 268 → 320 px wide (the population line measured 252.5 px against 240 px of content), its
    /// title the settlement's name in the title face; the height is the card's MEASURED content in the game
    /// (GameUi: title row, rule, one row per figure line, wrapped) — this is the nominal four-line card.
    public static readonly PanelRect Selection =
        new("##selection", Margin, Status.Height + Margin, SelectionWidth, 172);

    /// <summary>The selection card's width at UI scale 1 (UR-3).</summary>
    public const float SelectionWidth = 320;

    /// <summary>
    /// The contextual panel — policy, economy, population, market, annals or
    /// trends, whichever is open, and NOTHING when none is. Right-hand column,
    /// between the bars. M5 polish UR-3: its width follows the window —
    /// <see cref="ContextWidth"/> — 420 px at the design window (was 396 at every size).
    /// </summary>
    public static readonly PanelRect Context = new("##context",
        DesignWidth - ContextMinWidth - Margin, Status.Height + Margin,
        ContextMinWidth, DesignHeight - Status.Height - Command.Height - (Margin * 2));

    /// <summary>The context panel's width bounds at UI scale 1 (UR-3).</summary>
    public const float ContextMinWidth = 420, ContextMaxWidth = 560;

    /// <summary>The context panel's share of the window width before the bounds apply (UR-3).</summary>
    public const float ContextShare = 0.25f;

    /// <summary>UR-3 — THE CONTEXT PANEL'S WIDTH RULE: a quarter of the window, never narrower than 420 px × s nor
    /// wider than 560 px × s (1280 → 420, 1920 → 480, 2560 → 640 at s 1.375).</summary>
    public static float ContextWidth(float windowWidth, float scale = 1f) =>
        Math.Clamp(ContextShare * windowWidth, ContextMinWidth * scale, ContextMaxWidth * scale);

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
