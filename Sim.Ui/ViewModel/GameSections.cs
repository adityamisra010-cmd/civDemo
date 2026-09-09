namespace Sim.Ui.ViewModel;

/// <summary>
/// T4.18 — the contextual sections, and the rule that only one is open.
///
/// The old screen gave every subsystem a permanent bordered rectangle, so the
/// director read six windows at equal volume whether or not he was thinking
/// about any of them. These are the same six bodies of information behind ONE
/// mechanism: a row of buttons, one open panel, and a close that returns a clean
/// world. Nothing was removed — <see cref="Section.None"/> is simply a real
/// state, which is what the old layout had no way to express.
/// </summary>
public enum Section
{
    /// <summary>Nothing open: the world, the status band and the verbs. The
    /// default, and the state the director returns to.</summary>
    None = 0,

    /// <summary>The player's decisions — the labour allocation and its
    /// immediate consequences.</summary>
    Policy = 1,

    /// <summary>Trade: what moved between settlements, or why nothing did.</summary>
    Economy = 2,

    /// <summary>Who lives here: cohorts, class needs, grievance.</summary>
    Population = 3,

    /// <summary>Goods, prices, the price decomposition and its series.</summary>
    Market = 4,

    /// <summary>The chronicle, newest last.</summary>
    Annals = 5,

    /// <summary>One graph surface with a selectable metric.</summary>
    Trends = 6,

    /// <summary>Build identity, seed, camera, art provenance — the glass-box
    /// footer, which is diagnostic rather than play information and no longer
    /// occupies the screen during play.</summary>
    More = 7,
}

/// <summary>The section roster as data — labels and order, so the navigation
/// row and its test read from one list rather than two.</summary>
public static class GameSections
{
    /// <summary>In navigation order. Policy leads because it is the only one
    /// the director ACTS in; the rest are ways of looking.</summary>
    public static IReadOnlyList<Section> Order { get; } =
    [
        Section.Policy, Section.Economy, Section.Population,
        Section.Market, Section.Annals, Section.Trends, Section.More,
    ];

    /// <summary>The button label for a section.</summary>
    public static string Label(Section section) => section switch
    {
        Section.Policy => "POLICY",
        Section.Economy => "ECONOMY",
        Section.Population => "POPULATION",
        Section.Market => "MARKET",
        Section.Annals => "ANNALS",
        Section.Trends => "TRENDS",
        Section.More => "MORE",
        Section.None => "",
        _ => "",
    };

    /// <summary>The open panel's heading.</summary>
    public static string Title(Section section) => section switch
    {
        Section.Policy => "Policy",
        Section.Economy => "Economy",
        Section.Population => "Population",
        Section.Market => "Market",
        Section.Annals => "Annals",
        Section.Trends => "Trends",
        Section.More => "Build",
        Section.None => "",
        _ => "",
    };

    /// <summary>
    /// Clicking a section's button opens it — or CLOSES it when it is already
    /// open, so the same button both enters and leaves. That is what makes
    /// returning to a clean world one click rather than a hunt for an X.
    /// </summary>
    public static Section Toggle(Section current, Section clicked)
        => current == clicked ? Section.None : clicked;
}
