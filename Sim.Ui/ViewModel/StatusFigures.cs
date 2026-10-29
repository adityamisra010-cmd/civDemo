using System.Globalization;
using Sim.Core.State;
using Sim.Ui.Actions;
using Sim.Ui.Ages;

namespace Sim.Ui.ViewModel;

/// <summary>A status band research figure (the Technology or the Civics lane): what is being learned, how fast, or that
/// the lane is idle.</summary>
public sealed record ResearchFigure(string Text, bool Idle)
{
    /// <summary>The figure without the node's name ("Tech 27% +12.3/turn", "Civics idle [C]"), for a status band too
    /// narrow for both lane figures in full (Director playtest 2026-10-29: the smallest window, 1080x640).</summary>
    public string Short { get; init; } = Text;

    private readonly string? _tiny;

    /// <summary>The shortest form, for a band still too narrow for <see cref="Short"/>: a lane in progress without its
    /// points ("Tech 27%"); every other state as <see cref="Short"/>.</summary>
    public string Tiny { get => _tiny ?? Short; init => _tiny = value; }
}

/// <summary>The status band's compact Age indicator: the full Age name and the eligibility summary. It opens
/// the capital's Age panel on demand — the panel no longer opens by itself over the map.</summary>
public sealed record AgeFigure(string Text, AgePanelState State)
{
    /// <summary>The indicator without the Age's name ("Age I - not yet"), for a narrow status band (UR-3).</summary>
    public string Short { get; init; } = Text;

    /// <summary>Whether the indicator should draw the eye: an advance is available.</summary>
    public bool Eligible => State == AgePanelState.Eligible;
}

/// <summary>
/// The status band's progression figures — Technology, Civics (Director playtest 2026-10-29) and the Age (audit E26:
/// research was visible only inside the trees; ADR-033 / directive "the Capital Age panel must not cover the map at
/// turn 1"). Pure strings from the same models the surfaces use — the action surface's research block (the query's
/// research descriptors, with its Civics line) and the Age panel model (AgeQuery) — so the band can never disagree with
/// the panels.
/// </summary>
public static class StatusFigures
{
    /// <summary>How many characters of a node's name the band shows before an ellipsis.</summary>
    public const int NameChars = 24;

    /// <summary>
    /// The Technology figure — also the band's way into the trees on the Technology tab (it replaces the separate
    /// "Knowledge [K]" button, so the band has room for the research state and the Age). With the two-lane model
    /// (ADR-043) it reads the Technology lane only and is labelled "Tech" — the Civics lane has its own figure,
    /// <see cref="Civics"/> (Director playtest 2026-10-29): "Tech idle - choose [K]", "Tech: Root and tuber cultivati...
    /// 27% +12.3/turn", "Tech: Cordage next turn", "Tech stops at End Turn", or "Tech waiting: Bronze casting" (a
    /// destination waiting on the other tree). In the one-pool model the one lane covers both trees and the label is
    /// "Research". "Knowledge [K]" when the surface lists no research action at all.
    /// </summary>
    public static ResearchFigure Research(ResearchBlock? research)
    {
        if (research is null) return new ResearchFigure("Knowledge [K]", false);
        string lane = research.Civics is null ? "Research" : "Tech";
        // UR-3: the idle figure is the turn-1 call to action — it says what to do, not only that nothing happens.
        if (research.Idle)
            return research.ClearQueued
                ? new ResearchFigure(lane + " stops at End Turn", true) { Short = lane + " stops" }
                : new ResearchFigure(lane + " idle - choose [K]" + ReserveTail(research), true) { Short = lane + " idle [K]" };
        // rp-cp-lifecycle k2–k4 (destination research): the figure is the node the step FUNDS — a route node, or the node
        // an assisting lane funds — and a target that waits on the other tree says so (its points go unused: attention).
        ResearchItem item = research.Funding?.Node ?? research.Effective!;
        string name = Name(item);
        if (research.Chosen is not null) return new ResearchFigure(lane + ": " + name + " next turn", false) { Short = lane + " chosen" };
        if (research.Waiting) return new ResearchFigure(lane + " waiting: " + name + ReserveTail(research), true) { Short = lane + " waiting" };
        // ADR-051: a turn that draws on the reserve shows the draw beside the pool.
        string draw = research.Reserve is { Action: ReserveAction.Draw, AmountMilli: > 0L } rs
            ? string.Create(CultureInfo.InvariantCulture, $" +{rs.AmountMilli / 1000.0:0.0} reserve") : "";
        string figures = string.Create(CultureInfo.InvariantCulture, $"{Percent(item):0}% +{research.PointsPerTurn:0.0}/turn");
        return new ResearchFigure(lane + ": " + name + " " + figures + draw, false)
            { Short = lane + " " + figures, Tiny = string.Create(CultureInfo.InvariantCulture, $"{lane} {Percent(item):0}%") };
    }

    /// <summary>
    /// The Civics figure (ADR-043 two-lane model; Director playtest 2026-10-29: civics had no way in but a tab inside
    /// the trees) — the band's way into the trees on the Civics tab, [C]. It reads the action surface's Civics line
    /// (<see cref="ResearchBlock.Civics"/>), so it can never disagree with it: "Civics idle - choose [C]", "Civics stops
    /// at End Turn", "Civics: Oral tradition next turn", "Civics waiting: Taxation" (the civic waits on the Technology
    /// tree; Civics Points stay banked), or "Civics: Oral tradition 27% +3.1/turn". Null when there is no Civics lane
    /// (no research content, or the one-pool model) — the band then draws no Civics figure.
    /// </summary>
    public static ResearchFigure? Civics(ResearchBlock? research)
    {
        if (research?.Civics is not { } c) return null;
        if (c.Idle)
            return c.ClearQueued
                ? new ResearchFigure("Civics stops at End Turn", true) { Short = "Civics stops" }
                : new ResearchFigure("Civics idle - choose [C]", true) { Short = "Civics idle [C]" };
        ResearchItem item = c.Funding?.Node ?? c.Effective!;   // rp-cp-lifecycle k3: an assisting lane names the civic it funds
        string name = Name(item);
        if (c.Chosen is not null) return new ResearchFigure("Civics: " + name + " next turn", false) { Short = "Civics chosen" };
        if (c.Waiting) return new ResearchFigure("Civics waiting: " + name, true) { Short = "Civics waiting" };
        string figures = string.Create(CultureInfo.InvariantCulture, $"{Percent(item):0}% +{c.PointsPerTurn:0.0}/turn");
        return new ResearchFigure("Civics: " + name + " " + figures, false)
            { Short = "Civics " + figures, Tiny = string.Create(CultureInfo.InvariantCulture, $"Civics {Percent(item):0}%") };
    }

    /// <summary>A node's name cut to <see cref="NameChars"/> with an ellipsis.</summary>
    private static string Name(ResearchItem item) =>
        item.Name.Length <= NameChars ? item.Name : item.Name[..(NameChars - 3)].TrimEnd() + "...";

    /// <summary>Whole per cent of the funded node's cost reached (floored).</summary>
    private static double Percent(ResearchItem item) => item.Cost > 0 ? Math.Floor(item.Progress / item.Cost * 100.0) : 0.0;

    /// <summary>ADR-051: " - reserve Y/Z" (whole RP) for a lane whose RP go to the reserve; "" with the switch off.</summary>
    private static string ReserveTail(ResearchBlock research) => research.Reserve is { } rs
        ? string.Create(CultureInfo.InvariantCulture, $" - reserve {Math.Floor(rs.ReserveMilli / 1000.0):0}/{Math.Floor(rs.CapMilli / 1000.0):0}")
        : "";

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
        bool none = panel.State == AgePanelState.NoContent;
        return new AgeFigure(none ? "" : head + tail, panel.State) { Short = none ? "" : "Age " + AgePanelModel.Numeral(panel.CurrentAge) + tail };
    }
}
