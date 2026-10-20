namespace Sim.Ui;

/// <summary>How a string in the <see cref="FrameTextLog"/> reached the screen.</summary>
public enum FrameTextSource
{
    /// <summary>An ImGui text line the chrome submitted (<c>ImGui.TextUnformatted</c>, through <c>GameUi.Text</c>).</summary>
    ImGuiText,
    /// <summary>The VISIBLE label of an ImGui widget (a button, selectable, checkbox, radio button, plot label or
    /// plot overlay): the part before ImGui's "##" id separator.</summary>
    WidgetLabel,
    /// <summary>A run the chrome added straight to an ImGui draw list (<c>ImDrawList.AddText</c>, through
    /// <c>GameUi.AddText</c>).</summary>
    DrawListText,
    /// <summary>A <see cref="Render.TextCmd"/> that <see cref="ImGuiIntegration.DrawListImGuiBackend"/> replayed into
    /// ImGui (after its off-screen cull): the world lens's names, the screens, the cards, the era-styled runs.</summary>
    Replayed,
}

/// <summary>One string drawn in a frame: the surface that drew it (<see cref="FrameSurface"/>), how, and the text as
/// the UI handed it over (a styled run's semantic text, before the era's casing).</summary>
public readonly record struct FrameTextEntry(string Surface, FrameTextSource Source, string Text);

/// <summary>The surfaces <see cref="GameUi"/> draws text on, as recorded in <see cref="FrameTextEntry.Surface"/>.</summary>
public static class FrameSurface
{
    /// <summary>The world lens over the map (background list): settlement names, structures, formation tokens.</summary>
    public const string Lens = "lens";
    /// <summary>The Age panel, the ADVANCE AGE flow and the transition toast (foreground list).</summary>
    public const string Age = "age";
    /// <summary>The step's notices toast (foreground list).</summary>
    public const string Notices = "notices";
    /// <summary>The always-true status band.</summary>
    public const string Status = "status";
    /// <summary>The selection card (the selected settlement).</summary>
    public const string Selection = "selection";
    /// <summary>The formation card.</summary>
    public const string UnitCard = "unit-card";
    /// <summary>The contextual section panel (title, body).</summary>
    public const string Context = "context";
    /// <summary>The pinned info card over the map (the right-hand column).</summary>
    public const string Info = "info";
    /// <summary>The command bar.</summary>
    public const string Command = "command";
    /// <summary>The "Shift+click: details" hover hint.</summary>
    public const string Hint = "hint";
    /// <summary>The Knowledge &amp; Technology screen (and the info card pinned over its detail column).</summary>
    public const string Research = "research";
    /// <summary>P3: the map legend's tab (the legend's own text is the lens's: the lens paints it with the map).</summary>
    public const string Legend = "legend";
    /// <summary>P9: the overlay controls (the overlay's key is the lens's: the lens paints it with the map).</summary>
    public const string Overlay = "overlay";
    /// <summary>M5 S1d: the settlement-placement mode — its bar over the map and its hint beside the pointer.</summary>
    public const string Placement = "placement";
}

/// <summary>
/// THE FRAME'S TEXT, READ-ONLY (M5 workstream P6). Every string the UI put on screen in the last completed frame, in
/// draw order: the chrome's ImGui text lines and widget labels (recorded by GameUi's text helpers, the only place in
/// Sim.Ui allowed to call ImGui's text and widget functions — pinned by a source guard and by ImGui's own text log in
/// <c>FrameTextTests</c>), the runs the chrome adds straight to a draw list, and every <see cref="Render.TextCmd"/> the
/// DrawList backend replays (the world lens, the screens, the cards). It OBSERVES: nothing reads it back into the UI,
/// and the simulation never sees it. Tests use it to assert what the player can read — and, under fog, what they
/// cannot. A line ImGui clips out of a scrolled view is still recorded (it was submitted); a DrawList command the
/// backend culls off-screen is not (it was never replayed).
/// </summary>
public sealed class FrameTextLog
{
    private List<FrameTextEntry> _building = [];
    private List<FrameTextEntry> _last = [];

    /// <summary>The strings drawn by the last completed frame, in draw order.</summary>
    public IReadOnlyList<FrameTextEntry> Last => _last;

    public void BeginFrame() => _building = [];

    public void EndFrame() => _last = _building;

    /// <summary>Records <paramref name="text"/> (empty strings draw nothing and are not recorded).</summary>
    public void Add(string surface, FrameTextSource source, string text)
    {
        if (!string.IsNullOrEmpty(text)) _building.Add(new FrameTextEntry(surface, source, text));
    }

    /// <summary>The last frame's strings, optionally only those of one <paramref name="surface"/>.</summary>
    public IReadOnlyList<string> Texts(string? surface = null)
    {
        var r = new List<string>();
        foreach (FrameTextEntry e in _last) if (surface is null || e.Surface == surface) r.Add(e.Text);
        return r;
    }

    /// <summary>Whether any string of the last frame (of <paramref name="surface"/>, when given) contains
    /// <paramref name="token"/>, case-insensitively by default (an era style may set a run in capitals).</summary>
    public bool Contains(string token, string? surface = null, StringComparison comparison = StringComparison.OrdinalIgnoreCase)
    {
        foreach (FrameTextEntry e in _last)
            if ((surface is null || e.Surface == surface) && e.Text.Contains(token, comparison)) return true;
        return false;
    }
}
