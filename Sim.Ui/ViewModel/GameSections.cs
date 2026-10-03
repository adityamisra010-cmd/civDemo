namespace Sim.Ui.ViewModel;

/// <summary>
/// T4.18 — the contextual sections, and the rule that only one is open.
/// T4.19 lane B — the roster reworked around the glass box.
///
/// The old screen gave every subsystem a permanent bordered rectangle, so the
/// director read six windows at equal volume whether or not he was thinking
/// about any of them. These are the same bodies of information behind ONE
/// mechanism: a row of buttons, one open panel, and a close that returns a clean
/// world. Nothing was removed — <see cref="Section.None"/> is simply a real
/// state, which is what the old layout had no way to express.
///
/// T4.19: the T4.18 POPULATION and MARKET sections became TABS of one
/// SETTLEMENT section (every line they drew is still reachable, asserted by the
/// roster test), and TURN — the audit of the last End Turn — leads the roster
/// because "what changed" is the question the director asks first after every
/// step. The order follows the packet's reading path: what changed (TURN),
/// where and why (SETTLEMENT), what I can do (POLICY).
/// </summary>
public enum Section
{
    /// <summary>Nothing open: the world, the status band and the verbs. The
    /// default, and the state the director returns to.</summary>
    None = 0,

    /// <summary>The audit of the last End Turn: what changed, where, why —
    /// from the latest TurnRecord (docs/observability-architecture.md §2).</summary>
    Turn = 1,

    /// <summary>The selected settlement, tabbed: overview, population, food,
    /// economy (the former MARKET), grievance, migration, orders — from its
    /// SettlementRecord (§3) and the explain queries (§5, §6).</summary>
    Settlement = 2,

    /// <summary>The player's decisions — the labour allocation, its history
    /// and its observed (never attributed) consequences.</summary>
    Policy = 3,

    /// <summary>Trade: what moved between settlements, or why nothing did;
    /// plus the world GoodAccount table from the TurnRecord.</summary>
    Economy = 4,

    /// <summary>The chronicle, newest last.</summary>
    Annals = 5,

    /// <summary>One graph surface with a selectable metric.</summary>
    Trends = 6,

    /// <summary>Build identity, seed, camera, art provenance, session files —
    /// the glass-box footer, diagnostic rather than play information.</summary>
    More = 7,

    /// <summary>ADR-033 D9 — the PLAYER's view of the selected settlement, in plain language
    /// (<see cref="PlayerViews.Settlement"/>). Labelled SETTLEMENT; the record tabs of
    /// <see cref="Settlement"/> are its developer counterpart (RECORDS).</summary>
    Place = 8,

    /// <summary>ADR-033 D9 — the player's view of the empire (<see cref="PlayerViews.Empire"/>).</summary>
    Empire = 9,

    /// <summary>ADR-033 D6/D9 — the empire's institutions (<see cref="PlayerViews.Institutions"/>).</summary>
    Institutions = 10,

    /// <summary>ADR-033 D9 — the developer surfaces (TURN audit, RECORDS, ECONOMY tables, BUILD) behind
    /// one button, shown only when the developer toggle is on. Its tabs are <see cref="GameSections.DeveloperTabs"/>.</summary>
    Developer = 11,
}

/// <summary>The SETTLEMENT section's tabs — a row of buttons inside the panel,
/// one open at a time, UI state only.</summary>
public enum SettlementTab
{
    Overview = 0,
    Population = 1,
    Food = 2,
    /// <summary>The former MARKET section: goods, prices, decomposition, price series.</summary>
    Economy = 3,
    /// <summary>The centre of the packet: happiness factors, per-class grievance,
    /// the primary, its contributors, their chains, the lever.</summary>
    Grievance = 4,
    Migration = 5,
    Orders = 6,
}

/// <summary>The section roster as data — labels and order, so the navigation
/// row and its test read from one list rather than two.</summary>
public static class GameSections
{
    /// <summary>ADR-033 D9 — the PLAYER's command bar, in navigation order: the place, the empire, what
    /// I can do, the institutions, the history, the trends. Every section here speaks plain language; none is
    /// a record dump.</summary>
    public static IReadOnlyList<Section> PlayerOrder { get; } =
    [
        Section.Place, Section.Empire, Section.Policy, Section.Institutions,
        Section.Annals, Section.Trends,
    ];

    /// <summary>ADR-033 D9 — the developer surfaces, kept intact, reached as the tabs of
    /// <see cref="Section.Developer"/>: the TURN audit, the settlement RECORDS, the ECONOMY tables, BUILD.</summary>
    public static IReadOnlyList<Section> DeveloperTabs { get; } =
    [
        Section.Turn, Section.Settlement, Section.Economy, Section.More,
    ];

    /// <summary>Every command-bar slot the layout must hold: the player roster plus the developer button.
    /// The geometry (ChromeGeometry.NavButton) is sized for this, so turning the developer toggle on never
    /// overflows the bar.</summary>
    public static IReadOnlyList<Section> Order { get; } =
    [
        Section.Place, Section.Empire, Section.Policy, Section.Institutions,
        Section.Annals, Section.Trends, Section.Developer,
    ];

    /// <summary>The command bar's roster: the player sections, plus DEV when the developer toggle is on.
    /// The digit keys follow it (<see cref="OnDigit(Section, int, bool)"/>).</summary>
    public static IReadOnlyList<Section> Roster(bool developer) => developer ? Order : PlayerOrder;

    /// <summary>Whether <paramref name="section"/> is a developer surface (a DEV tab or DEV itself).</summary>
    public static bool IsDeveloper(Section section) =>
        section == Section.Developer || section == Section.Turn || section == Section.Settlement
        || section == Section.Economy || section == Section.More;

    /// <summary>
    /// Where a click-to-explain route lands for the current mode. With the developer toggle on, a developer
    /// route opens DEV on that tab (the explanation the route names, unchanged). With it off the player is
    /// sent to the plain-language home of the same figure: the world figures to EMPIRE, the settlement figures
    /// to SETTLEMENT. Returns the section to open and the DEV tab to show.
    /// </summary>
    public static (Section Open, Section DeveloperTab) Resolve(Section routed, bool developer, Section currentTab)
    {
        if (!IsDeveloper(routed) || routed == Section.Developer) return (routed, currentTab);
        if (developer) return (Section.Developer, routed);
        return (routed == Section.Turn || routed == Section.Economy ? Section.Empire : Section.Place, currentTab);
    }

    /// <summary>The section to show when the developer toggle changes: turning it off closes a developer
    /// surface (the player bar no longer has its button); anything else stays open.</summary>
    public static Section OnDeveloperToggle(Section current, bool developerNow) =>
        !developerNow && IsDeveloper(current) ? Section.None : current;

    /// <summary>The SETTLEMENT tabs in their button order.</summary>
    public static IReadOnlyList<SettlementTab> Tabs { get; } =
    [
        SettlementTab.Overview, SettlementTab.Population, SettlementTab.Food, SettlementTab.Economy,
        SettlementTab.Grievance, SettlementTab.Migration, SettlementTab.Orders,
    ];

    /// <summary>The button label for a section.</summary>
    public static string Label(Section section) => section switch
    {
        Section.Turn => "TURN",
        Section.Settlement => "RECORDS",
        Section.Policy => "POLICY",
        Section.Economy => "ECONOMY",
        Section.Annals => "ANNALS",
        Section.Trends => "TRENDS",
        Section.More => "BUILD",
        Section.Place => "SETTLEMENT",
        Section.Empire => "EMPIRE",
        Section.Institutions => "INSTITUTIONS",
        Section.Developer => "DEV",
        Section.None => "",
        _ => "",
    };

    /// <summary>The open panel's heading.</summary>
    public static string Title(Section section) => section switch
    {
        Section.Turn => "Turn audit",
        Section.Settlement => "Settlement records",
        Section.Policy => "Policy",
        Section.Economy => "Economy tables",
        Section.Annals => "Annals",
        Section.Trends => "Trends",
        Section.More => "Build",
        Section.Place => "Settlement",
        Section.Empire => "Empire",
        Section.Institutions => "Institutions",
        Section.Developer => "Developer",
        Section.None => "",
        _ => "",
    };

    /// <summary>The tab button label.</summary>
    public static string TabLabel(SettlementTab tab) => tab switch
    {
        SettlementTab.Overview => "Overview",
        SettlementTab.Population => "Population",
        SettlementTab.Food => "Food",
        SettlementTab.Economy => "Economy",
        SettlementTab.Grievance => "Grievance",
        SettlementTab.Migration => "Migration",
        SettlementTab.Orders => "Orders",
        _ => "",
    };

    /// <summary>
    /// Clicking a section's button opens it — or CLOSES it when it is already
    /// open, so the same button both enters and leaves. That is what makes
    /// returning to a clean world one click rather than a hunt for an X.
    /// </summary>
    public static Section Toggle(Section current, Section clicked)
        => current == clicked ? Section.None : clicked;

    /// <summary>The section a digit key opens: 1 is the first of
    /// <see cref="Order"/>, 7 the last; any other digit opens nothing
    /// (returns <see cref="Section.None"/>, which the caller must NOT apply as
    /// a close — see <see cref="OnDigit"/>).</summary>
    public static Section ForDigit(int digit) => ForDigit(digit, developer: true);

    /// <summary>The section a digit key opens on the roster of the current mode.</summary>
    public static Section ForDigit(int digit, bool developer)
    {
        IReadOnlyList<Section> roster = Roster(developer);
        return digit >= 1 && digit <= roster.Count ? roster[digit - 1] : Section.None;
    }

    /// <summary>A digit key OPENS its section (the packet's wording), and
    /// leaves the state alone when the digit has no section. It never closes:
    /// Escape does that, so the two keys have one meaning each.</summary>
    public static Section OnDigit(Section current, int digit) => OnDigit(current, digit, developer: true);

    /// <summary><see cref="OnDigit(Section, int)"/> on the roster of the current mode.</summary>
    public static Section OnDigit(Section current, int digit, bool developer)
    {
        Section target = ForDigit(digit, developer);
        return target == Section.None ? current : target;
    }

    /// <summary>Escape closes the open panel. With nothing open it is the
    /// caller's to interpret (T4.18 exits the game on it); the second return
    /// says which case applied so a single key press is never both.</summary>
    public static (Section Next, bool ClosedAPanel) OnEscape(Section current) =>
        current == Section.None ? (Section.None, false) : (Section.None, true);
}
