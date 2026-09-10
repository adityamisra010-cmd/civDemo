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
    /// <summary>In navigation order: the audit first, then the place, then the
    /// decision. The digit keys 1..7 follow this same order (<see cref="ForDigit"/>).</summary>
    public static IReadOnlyList<Section> Order { get; } =
    [
        Section.Turn, Section.Settlement, Section.Policy, Section.Economy,
        Section.Annals, Section.Trends, Section.More,
    ];

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
        Section.Settlement => "SETTLEMENT",
        Section.Policy => "POLICY",
        Section.Economy => "ECONOMY",
        Section.Annals => "ANNALS",
        Section.Trends => "TRENDS",
        Section.More => "MORE",
        Section.None => "",
        _ => "",
    };

    /// <summary>The open panel's heading.</summary>
    public static string Title(Section section) => section switch
    {
        Section.Turn => "Turn audit",
        Section.Settlement => "Settlement",
        Section.Policy => "Policy",
        Section.Economy => "Economy",
        Section.Annals => "Annals",
        Section.Trends => "Trends",
        Section.More => "Build",
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
    public static Section ForDigit(int digit) =>
        digit >= 1 && digit <= Order.Count ? Order[digit - 1] : Section.None;

    /// <summary>A digit key OPENS its section (the packet's wording), and
    /// leaves the state alone when the digit has no section. It never closes:
    /// Escape does that, so the two keys have one meaning each.</summary>
    public static Section OnDigit(Section current, int digit)
    {
        Section target = ForDigit(digit);
        return target == Section.None ? current : target;
    }

    /// <summary>Escape closes the open panel. With nothing open it is the
    /// caller's to interpret (T4.18 exits the game on it); the second return
    /// says which case applied so a single key press is never both.</summary>
    public static (Section Next, bool ClosedAPanel) OnEscape(Section current) =>
        current == Section.None ? (Section.None, false) : (Section.None, true);
}
