using System.Globalization;
using Sim.Core.Observability;
using Sim.Core.State;

namespace Sim.Ui.ViewModel;

/// <summary>One change and the observed turns after it.</summary>
public sealed record PolicyChangeView(long Turn, string Line, IReadOnlyList<string> Consequences);

/// <summary>One policy in the POLICY section's list. M4 has exactly one; M5's
/// taxation is the second entry, in this same shape.</summary>
public sealed record PolicyEntry(string Name, string Note);

/// <summary>
/// T4.19 lane B (B5, C3) — THE POLICY HISTORY for one settlement, from the
/// two authoritative sources and nothing else (docs/observability-architecture.md
/// §4): the PolicyChange list (what was declared, by whom, under which order)
/// and the per-turn PolicyState (what was in force). Pure: takes the lists
/// and the history seam, returns lines.
///
/// CONSEQUENCES ARE OBSERVED, NOT ATTRIBUTED. After each change the block
/// shows the settlement's record headline on the following turns — what
/// actually happened — labelled as observation. The simulation carries no
/// counterfactual, so "what changed BECAUSE of my decision" cannot be
/// answered by any record, and this model does not pretend to: the label is
/// part of every consequence block, not a footnote.
/// </summary>
public static class PolicyHistoryModel
{
    /// <summary>How many turns after a change the consequence block shows.</summary>
    public const int ConsequenceTurns = 5;

    /// <summary>The policies the section lists. One today.</summary>
    public static IReadOnlyList<PolicyEntry> Policies { get; } =
    [
        new PolicyEntry("labour allocation", "five-sector fixed-sum split per settlement (D-032, OrderKind.SectorAllocation)"),
    ];

    private static string Pct(double raw) => (raw * 100.0).ToString("F0", CultureInfo.InvariantCulture);

    public static string ActorName(int actor) => actor switch
    {
        -1 => "unattributed",
        LaborOrderFactory.UiActorId => "player",
        _ => "actor " + actor.ToString(CultureInfo.InvariantCulture),
    };

    /// <summary>CURRENT: declared beside effective, one line per sector, from
    /// the latest PolicyState of the settlement (null → the never-ordered default).</summary>
    public static IReadOnlyList<string> CurrentLines(PolicyState? state)
    {
        var lines = new List<string>(Sectors.Count + 1);
        for (int s = 0; s < Sectors.Count; s++)
        {
            double declared = state is null ? Sectors.Raw(Sectors.Default(new SettlementId(0)), s) : state.Declared[s];
            double effective = state is null ? Sectors.Share(Sectors.Default(new SettlementId(0)), s) : state.Effective[s];
            lines.Add(string.Create(CultureInfo.InvariantCulture,
                $"  {SectorBarModel.SectorNames[s],-13} declared {Pct(declared),3}%   effective {Pct(effective),3}%"));
        }
        lines.Add(state is null
            ? "  (no turn observed yet: the never-ordered default)"
            : string.Create(CultureInfo.InvariantCulture, $"  (in force on turn {state.Turn})"));
        return lines;
    }

    /// <summary>The settlement's latest PolicyState, or null.</summary>
    public static PolicyState? Latest(IReadOnlyList<PolicyState> states, int settlement)
    {
        PolicyState? latest = null;
        for (int i = 0; i < states.Count; i++)
            if (states[i].Settlement == settlement && (latest is null || states[i].Turn >= latest.Turn)) latest = states[i];
        return latest;
    }

    /// <summary>"turn 24 · farming 55% -> 73% · player · order #104".</summary>
    public static string ChangeLine(PolicyChange c) => string.Create(CultureInfo.InvariantCulture,
        $"turn {c.Turn} · {SectorBarModel.SectorNames[c.Sector]} {Pct(c.OldWeight)}% -> {Pct(c.NewWeight)}% · {ActorName(c.Actor)} · {(c.OrderIndex >= 0 ? "order #" + c.OrderIndex.ToString(CultureInfo.InvariantCulture) : "no order")}");

    /// <summary>HISTORY, newest first (the log is appended in turn order, so
    /// a reverse walk is newest-first without a sort).</summary>
    public static IReadOnlyList<PolicyChangeView> History(
        IReadOnlyList<PolicyChange> changes, IObservationHistory history, int settlement)
    {
        ArgumentNullException.ThrowIfNull(changes);
        ArgumentNullException.ThrowIfNull(history);
        var views = new List<PolicyChangeView>();
        for (int i = changes.Count - 1; i >= 0; i--)
        {
            PolicyChange c = changes[i];
            if (c.Settlement != settlement) continue;
            views.Add(new PolicyChangeView(c.Turn, ChangeLine(c), Consequences(history, settlement, c.Turn)));
        }
        return views;
    }

    /// <summary>The settlement's record headline on the turns AFTER
    /// <paramref name="changeTurn"/> (the turn the new weight first shows on
    /// the row; Production reads it the step after, so effects start at
    /// changeTurn + 1). Labelled observed, not attributed.</summary>
    public static IReadOnlyList<string> Consequences(IObservationHistory history, int settlement, long changeTurn)
    {
        var lines = new List<string>(ConsequenceTurns + 1) { "  observed on the turns after, not attributed (no counterfactual exists):" };
        int shown = 0;
        for (long t = changeTurn + 1; t <= history.LastTurn && shown < ConsequenceTurns; t++)
        {
            SettlementRecord? r = history.Settlement(t, settlement);
            if (r is null) continue;
            lines.Add(string.Create(CultureInfo.InvariantCulture,
                $"    turn {t}  pop {r.Population.Closing}  food {r.Food.GrainClosing}  deficit {r.Food.DeficitRatio:F2}  happiness {r.Social.Happiness:F1}"));
            shown++;
        }
        if (shown == 0) lines.Add("    (no turn observed after this change yet)");
        return lines;
    }

    /// <summary>The per-turn PolicyState table on request, newest first.</summary>
    public static IReadOnlyList<string> StateLines(IReadOnlyList<PolicyState> states, int settlement)
    {
        ArgumentNullException.ThrowIfNull(states);
        var lines = new List<string> { "turn   declared farm/herd/mine/craft/build   effective" };
        for (int i = states.Count - 1; i >= 0; i--)
        {
            PolicyState s = states[i];
            if (s.Settlement != settlement) continue;
            lines.Add(string.Create(CultureInfo.InvariantCulture,
                $"{s.Turn,4}   {Pct(s.Declared[0])}/{Pct(s.Declared[1])}/{Pct(s.Declared[2])}/{Pct(s.Declared[3])}/{Pct(s.Declared[4])}   {Pct(s.Effective[0])}/{Pct(s.Effective[1])}/{Pct(s.Effective[2])}/{Pct(s.Effective[3])}/{Pct(s.Effective[4])}"));
        }
        if (lines.Count == 1) lines.Add("(no turn observed yet)");
        return lines;
    }
}
