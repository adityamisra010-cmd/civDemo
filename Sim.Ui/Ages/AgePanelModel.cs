using Sim.Core.Kernel;
using Sim.Core.State;
using Sim.Core.Systems.Ages;

namespace Sim.Ui.Ages;

/// <summary>The three states of the capital's Age panel (D-047 Part 4 D), plus the two honest
/// edge cases: no Age content loaded, and the final Age (nothing further to enter).</summary>
public enum AgePanelState
{
    NoContent = 0,
    NotEligible = 1,
    /// <summary>Eligible and not yet ordered: "Advancement available — optional".</summary>
    Eligible = 2,
    /// <summary>An AdvanceAge order is queued this turn: "Advancing to X next turn".</summary>
    Pending = 3,
    FinalAge = 4,
}

/// <summary>One milestone line as the panel shows it (name, plain description, met, and the
/// observed quantity against its threshold — real state read through AgeQuery).</summary>
/// <remarks>M5 R2b: <c>Pending</c> carries ages.json's pending note (e.g. Battle Layer (M7) recruitment) — the panel labels
/// such a milestone "pending" instead of implying the player can satisfy it today.</remarks>
public sealed record MilestoneLine(string Id, string Name, string Description, bool Met, long Observed, long Threshold, string Owner,
    string? Pending = null);

/// <summary>The supporting milestones of one of the five categories, and whether the category is
/// covered (at least one of its milestones holds).</summary>
public sealed record CategoryGroup(AgeMilestoneCategory Category, string Name, bool Covered, IReadOnlyList<MilestoneLine> Milestones);

/// <summary>
/// THE CAPITAL'S AGE PANEL as a pure model (ADR-031 §4; D-047 Part 4 D). Every field is read
/// through <see cref="AgeQuery"/> over the real world and the session's queued orders — the same
/// statics the Age systems compute with, so the panel's "eligible" is the simulation's eligible.
/// Read-only: it builds nothing and writes nothing.
/// </summary>
public sealed record AgePanelModel(
    AgePanelState State,
    int CurrentAge, string CurrentAgeName,
    int? NextAge, string? NextAgeName,
    IReadOnlyList<MilestoneLine> Core,
    IReadOnlyList<CategoryGroup> Categories,
    int CoreMet, int CoreTotal, int SupportingMet, int SupportingRequired,
    int CategoriesCovered, int CategoriesRequired,
    IReadOnlyList<string> Remaining,
    PendingAgeAdvance? Pending,
    string? PendingSurgeName,
    string? CurrentSurgeName,
    long EnteredTurn,
    IReadOnlyList<AgeTransitionRow> Transitions)
{
    /// <summary>Whether the ADVANCE AGE button is active. Only true when eligible and nothing is
    /// pending — never a misleading active button (Part 4 D).</summary>
    public bool CanAdvance => State == AgePanelState.Eligible;

    /// <summary>The roman numeral used on banners and chips (I..IX).</summary>
    public static string Numeral(int age) => age switch
    {
        1 => "I", 2 => "II", 3 => "III", 4 => "IV", 5 => "V", 6 => "VI", 7 => "VII", 8 => "VIII", 9 => "IX", _ => "?",
    };

    public static AgePanelModel Empty { get; } = new(
        AgePanelState.NoContent, 0, "Ages not loaded", null, null, [], [], 0, 0, 0, 0, 0, 0, [], null, null, null, 0, []);

    /// <summary>Builds the panel for <paramref name="polity"/> from the world and the not-yet-stepped
    /// orders (<see cref="UiSession.QueuedOrders"/>).</summary>
    public static AgePanelModel Build(
        IReadOnlyWorldState world, AgeContent? ages, IReadOnlyList<OrderRecord> queued, PolityId polity)
    {
        if (ages is null) return Empty;
        AgeStatus status = AgeQuery.Status(world, ages, polity);
        PendingAgeAdvance? pending = AgeQuery.PendingAdvance(world, ages, queued, polity);
        AgeEligibilityReport? report = status.Eligibility;

        var core = new List<MilestoneLine>();
        var groups = new List<CategoryGroup>();
        if (report is not null)
        {
            foreach (MilestoneStatus m in report.Core) core.Add(Line(m));
            for (int k = 1; k <= 5; k++)
            {
                var cat = (AgeMilestoneCategory)k;
                var lines = new List<MilestoneLine>();
                foreach (MilestoneStatus m in report.Supporting)
                    if (m.Milestone.Category == cat) lines.Add(Line(m));
                groups.Add(new CategoryGroup(cat, ages.Category(cat).Name, (report.CategoryMask & (1 << (k - 1))) != 0, lines));
            }
        }

        AgePanelState state = report is null ? AgePanelState.FinalAge
            : pending is not null ? AgePanelState.Pending
            : report.Eligible ? AgePanelState.Eligible
            : AgePanelState.NotEligible;

        return new AgePanelModel(
            state, status.Current.Key, status.Current.Name, status.Next?.Key, status.Next?.Name,
            core, groups,
            report?.CoreMet ?? 0, report?.CoreTotal ?? 0, report?.SupportingMet ?? 0, report?.SupportingRequired ?? 0,
            report?.CategoriesCovered.Count ?? 0, report?.CategoriesRequired ?? 0,
            report?.Remaining ?? [], pending, pending?.Surge.Name, status.Surge?.Name, status.EnteredTurn,
            AgeQuery.Transitions(world, polity));
    }

    private static MilestoneLine Line(MilestoneStatus m) =>
        new(m.Milestone.Id, m.Milestone.Name, m.Milestone.Description, m.Met, m.Observed, m.Threshold, m.Milestone.Fact.Owner,
            m.Milestone.Pending);
}

/// <summary>One surge emphasis card — qualitative only (ruling 14: no numeric effect exists, so
/// none is shown). <c>Shape</c> is the content's stated surge shape.</summary>
public sealed record SurgeCard(int Key, string Name, string Description, string Domain, string ExpectedEffect,
    IReadOnlyList<string> DownstreamSystems, string Shape);

/// <summary>One line of "Your military will modernize": a group of the player's real formations.</summary>
public sealed record ModernizationLine(string From, string To, string FamilyName, ModernizationOutcome Outcome, int Count)
{
    public bool Changes => From != To;
}

/// <summary>One family of the unit-progression graph across the transition.</summary>
public sealed record FamilyLine(string FamilyName, string? From, string? To, ModernizationOutcome Outcome);

/// <summary>
/// THE ADVANCE FLOW (D-047 Part 4 E/F): surge cards from <see cref="AgeQuery.SurgeOptions"/>, the
/// player's formations' modernization from <see cref="MilitaryQuery.ModernizationPreview"/> grouped
/// by <see cref="MilitaryQuery.Summarize"/> (the function the transition itself applies), and the
/// family graph's changes from <see cref="MilitaryQuery.FamilyLineChanges"/>. Nothing is a UI list:
/// remove a family from the content and it leaves this model. Confirming returns exactly
/// <see cref="AgeQuery.AdvanceOrder"/>.
/// </summary>
public sealed record AdvanceFlowModel(
    int FromAge, string FromAgeName, int ToAge, string ToAgeName,
    IReadOnlyList<SurgeCard> Surges,
    IReadOnlyList<ModernizationLine> Modernization,
    IReadOnlyList<FamilyLine> FamilyChanges,
    int Formations)
{
    public static AdvanceFlowModel? Build(IReadOnlyWorldState world, AgeContent? ages, UnitFamilyContent? families, PolityId polity)
    {
        if (ages is null || AgeQuery.NextAge(world, ages, polity) is not { } next) return null;
        int current = AgeQuery.CurrentAge(world, ages, polity);
        var surges = new List<SurgeCard>();
        foreach (AgeSurge s in AgeQuery.SurgeOptions(ages))
            surges.Add(new SurgeCard(s.Key, s.Name, s.Description, s.Domain, s.ExpectedEffect, s.DownstreamSystems, ages.SurgeShape));

        var modern = new List<ModernizationLine>();
        var lines = new List<FamilyLine>();
        int formations = 0;
        if (families is not null)
        {
            UnitConversionPlan[] plan = MilitaryQuery.ModernizationPreview(world, ages, families, polity);
            formations = plan.Length;
            foreach (ConversionSummary c in MilitaryQuery.Summarize(families, plan))
                modern.Add(new ModernizationLine(c.From.Name, c.To.Name,
                    families.FamilyByKey(c.To.FamilyKey)?.Name ?? "", c.Outcome, c.Count));
            foreach (FamilyLineChange f in MilitaryQuery.FamilyLineChanges(families, current, next.Key))
                lines.Add(new FamilyLine(f.Family.Name, f.From?.Name, f.To?.Name, f.Outcome));
        }
        return new AdvanceFlowModel(current, ages.Age(current).Name, next.Key, next.Name, surges, modern, lines, formations);
    }

    /// <summary>The order the confirm button dispatches: exactly <see cref="AgeQuery.AdvanceOrder"/>.</summary>
    public OrderRecord Order(IReadOnlyWorldState world, PolityId polity, int surgeKey) =>
        AgeQuery.AdvanceOrder(world, polity, ToAge, surgeKey);

    public static string OutcomeText(ModernizationOutcome o) => o switch
    {
        ModernizationOutcome.Converted => "modernizes",
        ModernizationOutcome.ConvertedGeneric => "line ends - converts to successor role",
        ModernizationOutcome.PreservedNotAutoModernize => "kept (does not modernize automatically)",
        _ => "kept (no successor in this Age yet)",
    };
}
