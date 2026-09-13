using Sim.Core.Observability;
using Sim.Core.Observability.Explain;
using Sim.Core.State;
using Sim.Core.Systems;

namespace Sim.Ui.ViewModel;

/// <summary>The TURN section's content, built once per End Turn.</summary>
public sealed record TurnAuditView(
    string HeaderLine, string Digest,
    IReadOnlyList<AuditLine> Changed, IReadOnlyList<WhereRow> LargestPopulationDelta,
    IReadOnlyList<WhereRow> LargestDeficit, IReadOnlyList<string> Why, IReadOnlyList<string> GoodAccounts);

/// <summary>The SETTLEMENT section's tab bodies for the selected settlement.</summary>
public sealed record SettlementView(
    IReadOnlyList<string> Overview, IReadOnlyList<string> Population, IReadOnlyList<string> Food,
    IReadOnlyList<string> Economy, GrievanceView? Grievance, IReadOnlyList<string> Migration,
    IReadOnlyList<string> Orders,
    // T4.20: the additive food-flow block, drawn under Food. Separate from
    // Food so the existing store display and its pins are untouched.
    IReadOnlyList<string> FoodFlow);

/// <summary>The POLICY section's read-only halves.</summary>
public sealed record PolicyView(
    IReadOnlyList<string> Current, IReadOnlyList<PolicyChangeView> History, IReadOnlyList<string> States);

/// <summary>
/// T4.19 lane B — THE COMPOSITION ROOT for the glass-box panels: from a
/// session and a selection to every view model the renderer draws, in one
/// call, so SimUiGame only renders and a headless test can build the whole
/// screen's content the way each frame would and prove the world untouched.
///
/// Everything here READS: the observation history through its seam, the
/// session's current and previous worlds through the explain queries (pure
/// functions of (prev, next, cfg)), and the names registry. Nothing is
/// written anywhere, and the "building every view model five times changes
/// no hash" test is what pins that.
/// </summary>
public static class ScreenModels
{
    /// <summary>The TURN audit from the latest observation, or null before the first End Turn.</summary>
    public static TurnAuditView? TurnAudit(UiSession session)
    {
        ArgumentNullException.ThrowIfNull(session);
        IObservationHistory history = session.Observations;
        if (history.Observations.Count == 0) return null;
        TurnObservation latest = history.Observations[^1];
        Func<int, string> name = session.Names.Name;
        return new TurnAuditView(
            TurnAuditModel.HeaderLine(latest.Turn),
            TurnAuditModel.Digest(latest.Turn, latest.Settlements),
            TurnAuditModel.ChangedLines(latest.Turn, latest.Settlements, name),
            TurnAuditModel.LargestPopulationDelta(latest.Settlements, name),
            TurnAuditModel.LargestDeficit(latest.Settlements, name),
            TurnAuditModel.WhyLines(latest.Turn),
            TurnAuditModel.GoodAccountLines(latest.Turn));
    }

    /// <summary>The selected settlement's tabs from its latest record and the
    /// explain queries. Null when no turn has been observed or the settlement
    /// has no record on the latest turn.</summary>
    public static SettlementView? Settlement(UiSession session, int selected)
    {
        ArgumentNullException.ThrowIfNull(session);
        IObservationHistory history = session.Observations;
        if (history.Observations.Count == 0 || selected < 0) return null;
        SettlementRecord? r = history.Settlement(history.LastTurn, selected);
        if (r is null) return null;
        Func<int, string> name = session.Names.Name;
        WorldState next = session.World;
        WorldState? prev = session.PreviousWorld;
        SimConfig cfg = session.Config;
        var id = new SettlementId(selected);

        MigrationExplanation? migration = prev is null ? null : MigrationExplanation.For(prev, next, cfg, id);
        GrievanceView? grievance = prev is null ? null : Grievance(prev, next, cfg, id);

        return new SettlementView(
            SettlementInspectorModel.OverviewLines(r, name(selected)),
            SettlementInspectorModel.PopulationLines(r),
            SettlementInspectorModel.FoodLines(r),
            SettlementInspectorModel.EconomyLines(r, name),
            grievance,
            SettlementInspectorModel.MigrationLines(r, migration, name),
            SettlementInspectorModel.OrderLines(r),
            SettlementInspectorModel.FoodFlowLines(r, history.Observations[^1].Turn.DtYears));
    }

    /// <summary>The Grievance tab: happiness on next (the world on screen),
    /// one GrievanceExplanation per registry class with MEMBERS ON PREV — the
    /// same rule the query itself uses (GrievanceExplanation.ClassPopulation is
    /// the PREV bucket sum), because the needs system iterated PREV's members
    /// to write the row on next. A class that emptied THIS step therefore still
    /// appears, with the G the system wrote for it from those members; a class
    /// with nobody on prev has no published satisfaction row and nothing to
    /// explain. Every bound need's chain on demand.</summary>
    public static GrievanceView Grievance(
        IReadOnlyWorldState prev, IReadOnlyWorldState next, SimConfig cfg, SettlementId id)
    {
        HappinessExplanation happiness = HappinessExplanation.For(next, cfg, id);
        ClassEntry[] classes = cfg.Registries.Classes;
        var explanations = new List<GrievanceExplanation>(classes.Length);
        for (int c = 0; c < classes.Length; c++)
        {
            var cls = new ClassId(classes[c].Id);
            long pop = 0;
            for (int b = 0; b < prev.Buckets.Count; b++)
                if (prev.Buckets[b].Settlement == id && prev.Buckets[b].Class == cls) pop += prev.Buckets[b].Count.Value;
            if (pop == 0) continue;
            explanations.Add(GrievanceExplanation.For(prev, next, cfg, id, cls));
        }
        return GrievanceViewModel.Build(happiness, explanations,
            (classId, needId) => CausalChain.ForNeed(prev, next, cfg, id, new ClassId(classId), needId));
    }

    public static PolicyView Policy(UiSession session, int selected)
    {
        ArgumentNullException.ThrowIfNull(session);
        IObservationHistory history = session.Observations;
        return new PolicyView(
            PolicyHistoryModel.CurrentLines(PolicyHistoryModel.Latest(history.PolicyStates, selected)),
            PolicyHistoryModel.History(history.PolicyChanges, history, selected),
            PolicyHistoryModel.StateLines(history.PolicyStates, selected));
    }
}
