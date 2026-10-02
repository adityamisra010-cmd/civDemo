using System.Globalization;
using Sim.Ui.Actions;
using Sim.Ui.Ages;

namespace Sim.Ui.ViewModel;

/// <summary>The status band's research figure: what is being learned, how fast, or that research is idle.</summary>
public sealed record ResearchFigure(string Text, bool Idle);

/// <summary>The status band's compact Age indicator: the full Age name and the eligibility summary. It opens
/// the capital's Age panel on demand — the panel no longer opens by itself over the map.</summary>
public sealed record AgeFigure(string Text, AgePanelState State)
{
    /// <summary>Whether the indicator should draw the eye: an advance is available.</summary>
    public bool Eligible => State == AgePanelState.Eligible;
}

/// <summary>
/// The status band's two progression figures (audit E26: research was visible only inside the trees; ADR-033 /
/// directive "the Capital Age panel must not cover the map at turn 1"). Pure strings from the same models the
/// surfaces use — the action surface's research block (the query's research descriptors) and the Age panel
/// model (AgeQuery) — so the band can never disagree with the panels.
/// </summary>
public static class StatusFigures
{
    /// <summary>The research figure, or an empty text when the surface lists no research action at all.</summary>
    public static ResearchFigure Research(ResearchBlock? research)
    {
        if (research is null) return new ResearchFigure("", false);
        if (research.Idle)
            return new ResearchFigure(research.ClearQueued ? "research stops at End Turn" : "research idle - choose [K]", true);
        ResearchItem item = research.Effective!;
        if (research.Chosen is not null) return new ResearchFigure("research: " + item.Name + " (from the next turn)", false);
        return new ResearchFigure(string.Create(CultureInfo.InvariantCulture,
            $"research: {item.Name} {item.Progress:0}/{item.Cost:0} (+{research.PointsPerTurn:0.0}/turn)"), false);
    }

    /// <summary>The Age indicator: "Age I Prehistoric / Stone Age - next Age: not yet eligible (core 0/2 ...)".</summary>
    public static AgeFigure Age(AgePanelModel panel)
    {
        string head = "Age " + AgePanelModel.Numeral(panel.CurrentAge) + " " + panel.CurrentAgeName;
        string tail = panel.State switch
        {
            AgePanelState.NoContent => "",
            AgePanelState.FinalAge => " - the final Age",
            AgePanelState.Eligible => " - advance available",
            AgePanelState.Pending => " - advancing next turn",
            _ => string.Create(CultureInfo.InvariantCulture, $" - next Age not yet (core {panel.CoreMet}/{panel.CoreTotal})"),
        };
        return new AgeFigure(panel.State == AgePanelState.NoContent ? "" : head + tail, panel.State);
    }
}
