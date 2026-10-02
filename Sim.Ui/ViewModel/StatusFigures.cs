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
    /// <summary>How many characters of a node's name the band shows before an ellipsis.</summary>
    public const int NameChars = 24;

    /// <summary>
    /// The research figure — also the band's way into the trees (it replaces the separate "Knowledge [K]"
    /// button, so the band has room for the research state and the Age): "research idle [K]", "research: Root
    /// and tuber cultivati... 27% +12.3/turn", "research: Cordage next turn", "research stops at End Turn", or
    /// "Knowledge [K]" when the surface lists no research action at all.
    /// </summary>
    public static ResearchFigure Research(ResearchBlock? research)
    {
        if (research is null) return new ResearchFigure("Knowledge [K]", false);
        if (research.Idle)
            return new ResearchFigure(research.ClearQueued ? "research stops at End Turn" : "research idle [K]", true);
        ResearchItem item = research.Effective!;
        string name = item.Name.Length <= NameChars ? item.Name : item.Name[..(NameChars - 3)].TrimEnd() + "...";
        if (research.Chosen is not null) return new ResearchFigure("research: " + name + " next turn", false);
        double pct = item.Cost > 0 ? Math.Floor(item.Progress / item.Cost * 100.0) : 0.0;
        return new ResearchFigure(string.Create(CultureInfo.InvariantCulture,
            $"research: {name} {pct:0}% +{research.PointsPerTurn:0.0}/turn"), false);
    }

    /// <summary>The Age indicator: "Age I Prehistoric / Stone Age - not yet" (eligible for the next Age: not yet; the
    /// milestones are one click away, in the capital's Age panel).</summary>
    public static AgeFigure Age(AgePanelModel panel)
    {
        string head = "Age " + AgePanelModel.Numeral(panel.CurrentAge) + " " + panel.CurrentAgeName;
        string tail = panel.State switch
        {
            AgePanelState.NoContent => "",
            AgePanelState.FinalAge => " - final Age",
            AgePanelState.Eligible => " - advance available",
            AgePanelState.Pending => " - advancing",
            _ => " - not yet",
        };
        return new AgeFigure(panel.State == AgePanelState.NoContent ? "" : head + tail, panel.State);
    }
}
