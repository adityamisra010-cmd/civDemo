namespace Sim.Ui.ViewModel;

/// <summary>A clickable figure on the always-visible chrome: the status band's
/// two world totals and the selection card's four settlement readings.</summary>
public enum ExplainFigure
{
    WorldPopulation,
    WorldFood,
    SettlementPopulation,
    SettlementFood,
    SettlementHappiness,
    SettlementGrievance,
}

/// <summary>Which TURN audit line opens expanded when a route lands there.</summary>
public enum AuditExpand
{
    None,
    Population,
    Grain,
}

/// <summary>Where a figure's click lands: the section, the SETTLEMENT tab when
/// the section is SETTLEMENT (ignored otherwise), the TURN line to expand when
/// the section is TURN, and whether the Grievance tab opens with the happiness
/// factors unfolded.</summary>
public readonly record struct ExplainRoute(
    Section Section, SettlementTab Tab, AuditExpand Expand, bool ShowHappinessFactors);

/// <summary>
/// T4.19 lane B (B1) — CLICK-TO-EXPLAIN as a pure table: figure → surface.
///
/// The map from a number the director can see to the panel that decomposes
/// it is DATA, not six scattered click handlers, so a test can walk the whole
/// <see cref="ExplainFigure"/> enum and prove every clickable figure has a
/// destination — a figure that is clickable and goes nowhere is the defect
/// this shape prevents. The world totals route to the TURN audit (world-level
/// accounts, §2); the settlement readings route to the SETTLEMENT tabs (§3,
/// §5, §6). Nothing here reads state; the renderer applies the route.
/// </summary>
public static class ExplainRouting
{
    public static ExplainRoute For(ExplainFigure figure) => figure switch
    {
        ExplainFigure.WorldPopulation => new(Section.Turn, SettlementTab.Overview, AuditExpand.Population, false),
        ExplainFigure.WorldFood => new(Section.Turn, SettlementTab.Overview, AuditExpand.Grain, false),
        ExplainFigure.SettlementPopulation => new(Section.Settlement, SettlementTab.Population, AuditExpand.None, false),
        ExplainFigure.SettlementFood => new(Section.Settlement, SettlementTab.Food, AuditExpand.None, false),
        ExplainFigure.SettlementHappiness => new(Section.Settlement, SettlementTab.Grievance, AuditExpand.None, true),
        ExplainFigure.SettlementGrievance => new(Section.Settlement, SettlementTab.Grievance, AuditExpand.None, false),
        _ => throw new ArgumentOutOfRangeException(nameof(figure), figure, "figure has no explain route"),
    };
}
