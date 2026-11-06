namespace Proto.M7.Tactical;

/// <summary>
/// [P] Calibration of <see cref="AutoCoefficients"/> on the lane-P TUNING cases only (tuning scenarios x tuning seeds 1-40;
/// <see cref="ParityBatteryP.IsTuningCase"/> is enforced: any other case is refused). The objective is the rule-P1 gaps in
/// units of their bands (computed on the tuning samples, n = 40 per side), so every check weighs the same at its own
/// tolerance: per scenario, sum over the checks of 1000 x gap / band, plus 4000 per failed check and 6000 for a wrong modal
/// victor (the penalties make a passed check worth more than a smaller gap elsewhere). Integer; deterministic coordinate descent (the Sprint 1 procedure: the current value is kept unless a candidate
/// is strictly better; among new candidates the earlier grid entry wins).
/// </summary>
public static class ParityCalibrationP
{
    public static long ScenarioLoss(PStats t, PStats a, long startA, long startB)
    {
        var c = ParityCriteriaP1.Check(t, a, startA, startB, 2_000);
        long l = 0;
        l += 1000 * c.WinGapA / c.WinBand + 1000 * c.WinGapB / c.WinBand + 1000 * c.WinGapDraw / c.WinBand;
        l += 1000 * c.CasAGap / c.CasABand + 1000 * c.CasBGap / c.CasBBand + 1000 * c.DurGap / c.DurBand;
        l += 500 * c.PulseMedianGap;
        if (!c.ModalOk) l += 6000;
        if (!c.WinOk) l += 4000;
        if (!c.CasAOk) l += 4000;
        if (!c.CasBOk) l += 4000;
        if (!c.DurOk) l += 4000;
        if (!c.PulsesOk) l += 4000;
        return l;
    }

    /// <summary>Total loss of auto outcomes against tactical outcomes (aligned by case index) over the scenarios present.</summary>
    public static long Loss(PCase[] cases, POutcome[] tactical, POutcome[] auto)
    {
        if (cases.Length != tactical.Length || cases.Length != auto.Length) throw new ArgumentException("misaligned");
        long total = 0;
        var seen = new List<PScenario>();
        foreach (var c in cases) if (!seen.Contains(c.Scenario)) seen.Add(c.Scenario);
        foreach (var s in seen)
        {
            var t = new List<POutcome>(); var a = new List<POutcome>();
            for (int i = 0; i < cases.Length; i++) if (cases[i].Scenario == s) { t.Add(tactical[i]); a.Add(auto[i]); }
            total = checked(total + ScenarioLoss(PStats.Of(t), PStats.Of(a), ParityBatteryP.RoleStart(s, 0), ParityBatteryP.RoleStart(s, 1)));
        }
        return total;
    }

    /// <summary>Loss of one coefficient set; refuses any non-tuning case (held-out isolation).</summary>
    public static long TotalLoss(AutoCoefficients c, PCase[] cases, POutcome[] tactical)
    {
        foreach (var x in cases)
            if (!ParityBatteryP.IsTuningCase(x)) throw new InvalidOperationException($"calibration refused a non-tuning case {x}");
        var auto = new POutcome[cases.Length];
        for (int i = 0; i < cases.Length; i++) auto[i] = ParityBatteryP.RunAuto(cases[i], c);
        return Loss(cases, tactical, auto);
    }

    /// <summary>The Sprint 1 axes plus the lane-P doctrine coefficients (model 2).</summary>
    public static CoefficientAxis[] DefaultAxes()
    {
        var l = new List<CoefficientAxis>(AutoCalibration.DefaultAxes());
        l.Add(new CoefficientAxis("HoldEngagePermille", new long[] { 50, 100, 150, 200, 300, 400, 500, 600, 800, 1000 }, c => c.HoldEngagePermille, (c, v) => c with { HoldEngagePermille = v }));
        l.Add(new CoefficientAxis("WoundBufferPermille", new long[] { 0, 25, 50, 75, 100, 150, 200, 300 }, c => c.WoundBufferPermille, (c, v) => c with { WoundBufferPermille = v }));
        l.Add(new CoefficientAxis("RangedPerExposedPermille", new long[] { 1000, 1500, 2000, 3000, 4000, 6000, 1_000_000 }, c => c.RangedPerExposedPermille, (c, v) => c with { RangedPerExposedPermille = v }));
        l.Add(new CoefficientAxis("ConvergenceMobilityPermille", new long[] { 0, 250, 500, 750, 1000 }, c => c.ConvergenceMobilityPermille, (c, v) => c with { ConvergenceMobilityPermille = v }));
        l.Add(new CoefficientAxis("MaxAttackersPerHolderPermille", new long[] { 500, 750, 1000, 1250, 1500, 2000, 3000, 1_000_000 }, c => c.MaxAttackersPerHolderPermille, (c, v) => c with { MaxAttackersPerHolderPermille = v }));
        return l.ToArray();
    }

    public static (AutoCoefficients Fitted, long Loss, List<FitStep> Steps, int Evaluations) Fit(
        AutoCoefficients start, CoefficientAxis[] axes, PCase[] cases, POutcome[] tactical, int maxSweeps)
    {
        var cur = start;
        long best = TotalLoss(cur, cases, tactical);
        var steps = new List<FitStep>();
        int evals = 1;
        for (int sweep = 1; sweep <= maxSweeps; sweep++)
        {
            bool changed = false;
            foreach (var ax in axes)
            {
                long curV = ax.Get(cur), bestV = curV;
                foreach (long v in ax.Candidates)
                {
                    if (v == curV) continue;
                    var cand = ax.Set(cur, v);
                    try { cand.Validate(); } catch (ArgumentOutOfRangeException) { continue; }
                    long l = TotalLoss(cand, cases, tactical);
                    evals++;
                    if (l < best) { best = l; bestV = v; }
                }
                if (bestV != curV)
                {
                    cur = ax.Set(cur, bestV);
                    steps.Add(new FitStep(sweep, ax.Name, curV, bestV, best));
                    changed = true;
                }
            }
            if (!changed) break;
        }
        return (cur, best, steps, evals);
    }
}
