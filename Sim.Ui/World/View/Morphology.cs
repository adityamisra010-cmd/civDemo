using System.Globalization;
using Sim.Ui.World.Content;

namespace Sim.Ui.World.View;

/// <summary>
/// VISUAL STAGE SELECTION — pure functions from a report and the content to a stage.
/// The rule is the same everywhere: the stage is the LAST one whose threshold is ≤ the
/// driver's value (inclusive); a driver that is not reported, not finite or negative draws
/// the base stage and says so. Nothing is substituted for a missing driver — not another
/// input, not the report's age, not the settlement's size — because each of those would be
/// a hidden maturity rule.
/// </summary>
public static class Morphology
{
    public static string Num(double v) =>
        v == Math.Floor(v) && Math.Abs(v) < 1e15 ? v.ToString("#,0", CultureInfo.InvariantCulture) : v.ToString("#,0.##", CultureInfo.InvariantCulture);

    /// <summary>The last index whose threshold is ≤ value (thresholds ascending, first 0).</summary>
    public static int LastAtOrBelow(IReadOnlyList<double> thresholds, double value)
    {
        int idx = 0;
        for (int i = 0; i < thresholds.Count; i++) if (thresholds[i] <= value) idx = i;
        return idx;
    }

    private static bool Usable(double? v, out double value)
    {
        value = v ?? 0;
        return v is double x && double.IsFinite(x) && x >= 0;
    }

    /// <param name="allowDemonstration">False for a live (non-placeholder) bundle: a driver whose
    /// thresholds are DEMONSTRATION values never stages live data, and a live settlement with no
    /// simulation input draws the base footprint UNNAMED ("sizeTier not reported").</param>
    public static StageChoice Settlement(SettlementReport r, SettlementStages content, bool allowDemonstration)
    {
        int n = content.Stages.Count;
        foreach (StageDriver d in content.Drivers)
        {
            if (!allowDemonstration && d.Provenance != "simulation") continue;
            if (!Usable(r.Input(d.Input), out double v)) continue;
            int idx = LastAtOrBelow(d.Thresholds, v);
            string name = d.NamedStages
                ? content.Stages[idx].Name
                : $"{d.Input} {Num(v)} (stage {idx + 1} of {n})";
            string why = d.Provenance == "simulation"
                ? $"from the simulation's {d.Input} = {Num(v)}, mapped one to one - not named by the view"
                : $"DEMONSTRATION threshold: {d.Input} {Num(v)} >= {Num(d.Thresholds[idx])} (view only)";
            return new StageChoice(idx, n, name, d.Input, true, why);
        }
        string drivers = string.Join(", ", content.Drivers.Where(x => allowDemonstration || x.Provenance == "simulation").Select(x => x.Input));
        return new StageChoice(0, n, allowDemonstration ? content.Stages[0].Name : $"base footprint ({drivers} not reported)", drivers, false,
            $"no stage input reported ({drivers}): base footprint");
    }

    /// <summary>The numeric score a stage rule reads from a structure report, or null.</summary>
    public static double? Score(StageRule rule, StructureReport r) =>
        rule.Input is string input
            ? string.Equals(input, WorldViewConstants.MultiplicityInput, StringComparison.Ordinal) ? r.Multiplicity : r.Input(input)
            : OrdinalOf(rule, r.Label(rule.Label!));

    public static double? OrdinalOf(StageRule rule, string? label)
    {
        if (label is null) return null;
        foreach ((string value, double score) in rule.Ordinals)
            if (string.Equals(value, label, StringComparison.Ordinal)) return score;
        return null;
    }

    public static StageChoice Structure(StructureReport r, VisualType type)
    {
        double? raw = Score(type.StageBy, r);
        string driver = type.StageBy.Describe;
        int n = type.Stages.Count;
        if (!Usable(raw, out double v))
            return new StageChoice(0, n, type.Stages[0].Name, driver, false,
                $"visual stage: base ({driver} not reported)");
        var mins = new double[n];
        for (int i = 0; i < n; i++) mins[i] = type.Stages[i].Min;
        int idx = LastAtOrBelow(mins, v);
        string shown = type.StageBy.Label is string lbl ? $"{lbl} {r.Label(lbl)} (ordinal {Num(v)})" : $"{driver} = {Num(v)}";
        return new StageChoice(idx, n, type.Stages[idx].Name, driver, true,
            $"{shown} >= view threshold {Num(mins[idx])}");
    }

    public static (InfraStage Stage, StageChoice Choice) Edge(InfraEdgeReport r, InfraType type)
    {
        int n = type.Stages.Count;
        double? raw = type.StageBy.Input is string input ? r.Input(input) : null;
        if (!Usable(raw, out double v))
            return (type.Stages[0], new StageChoice(0, n, type.Stages[0].Name, type.StageBy.Describe, false,
                $"visual stage: base ({type.StageBy.Describe} not reported)"));
        var mins = new double[n];
        for (int i = 0; i < n; i++) mins[i] = type.Stages[i].Min;
        int idx = LastAtOrBelow(mins, v);
        return (type.Stages[idx], new StageChoice(idx, n, type.Stages[idx].Name, type.StageBy.Describe, true,
            $"{type.StageBy.Describe} = {Num(v)} >= view threshold {Num(mins[idx])}"));
    }
}
