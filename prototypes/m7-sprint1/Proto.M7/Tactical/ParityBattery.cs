using Proto.M7.Fixed;

namespace Proto.M7.Tactical;

/// <summary>One battery run: (scenario, seed, orientation).</summary>
public readonly record struct ParityCase(ParityScenarioId Scenario, ulong Seed, bool Mirrored);

/// <summary>
/// The comparable outcome of one battle, integer only. Role A = side 0, role B = side 1 (see <see cref="ParityScenarios"/>).
/// Winner = the side holding the field (BattleResult.HoldsField: Active entities left on one side only), -1 = none.
/// CasA/CasB = personnel loss fraction (Killed + Surrendered) / Start of the role's formations, permille, floor.
/// </summary>
public readonly record struct ParityOutcome(int Winner, long CasA, long CasB, long Pulses, BattleEndReason Reason)
{
    public static ParityOutcome Of(BattleResult r)
    {
        long[] start = new long[2], loss = new long[2];
        foreach (var f in r.Formations) { start[f.Side] += f.Start; loss[f.Side] += f.PersonnelLoss; }
        return new ParityOutcome(r.HoldsField, FixedMath.MulDivFloor(loss[0], 1000, start[0]), FixedMath.MulDivFloor(loss[1], 1000, start[1]),
                                 r.Pulses, r.Reason);
    }
}

/// <summary>Five nearest-rank quantiles of an integer sample (permille ranks 100, 250, 500, 750, 900).</summary>
public readonly record struct Dist(long P10, long P25, long P50, long P75, long P90)
{
    /// <summary>
    /// Nearest-rank quantile: the value at sorted index ceil(q*n/1000) - 1 (clamped to [0, n-1]). For even n the median is
    /// the LOWER middle value. Exact integers; the input is copied and sorted (ascending, total order on long).
    /// </summary>
    public static long Quantile(long[] sortedAscending, long qPermille)
    {
        int n = sortedAscending.Length;
        if (n == 0) throw new ArgumentException("empty sample");
        long idx = FixedMath.DivCeil(checked(qPermille * n), 1000) - 1;
        return sortedAscending[(int)Math.Clamp(idx, 0, n - 1)];
    }

    public static Dist Of(long[] values)
    {
        var v = (long[])values.Clone();
        Array.Sort(v);
        return new Dist(Quantile(v, 100), Quantile(v, 250), Quantile(v, 500), Quantile(v, 750), Quantile(v, 900));
    }

    /// <summary>Inter-quartile half-width (P75 - P25) / 2, rounded up.</summary>
    public long IqrHalfCeil => FixedMath.DivCeil(P75 - P25, 2);
}

/// <summary>Distribution summary of one scenario's outcomes under one resolver.</summary>
public sealed record ScenarioStats(int N, int WinA, int WinB, int Draw, Dist CasA, Dist CasB, Dist Pulses, long StartA, long StartB)
{
    public long WinAPermille => FixedMath.MulDivFloor(WinA, 1000, N);
    public long WinBPermille => FixedMath.MulDivFloor(WinB, 1000, N);
    public long DrawPermille => FixedMath.MulDivFloor(Draw, 1000, N);

    /// <summary>Modal winner (0, 1, or -1 = draw) by composite key (count desc, then A &lt; B &lt; draw).</summary>
    public int ModalWinner => WinA >= WinB && WinA >= Draw ? 0 : WinB >= Draw ? 1 : -1;
    public long ModalPermille => FixedMath.MulDivFloor(Math.Max(WinA, Math.Max(WinB, Draw)), 1000, N);

    public static ScenarioStats Of(IReadOnlyList<ParityOutcome> o, long startA, long startB)
    {
        if (o.Count == 0) throw new ArgumentException("no outcomes");
        int a = 0, b = 0, d = 0;
        var ca = new long[o.Count]; var cb = new long[o.Count]; var p = new long[o.Count];
        for (int i = 0; i < o.Count; i++)
        {
            if (o[i].Winner == 0) a++; else if (o[i].Winner == 1) b++; else d++;
            ca[i] = o[i].CasA; cb[i] = o[i].CasB; p[i] = o[i].Pulses;
        }
        return new ScenarioStats(o.Count, a, b, d, Dist.Of(ca), Dist.Of(cb), Dist.Of(p), startA, startB);
    }
}

/// <summary>
/// The proposed MEASURABLE parity tolerance of one scenario, derived ONLY from the tactical resolver's own seed-to-seed
/// spread (spec §5). All permille / pulses, integer. <paramref name="Seeds"/> = n_eff, the number of INDEPENDENT seeds per
/// compared sample: the two orientations of one seed are strongly correlated in the tactical engine (run40a: same winner in
/// 34 of 40 equal-infantry pairs, identical outcome in all 40 archer pairs), so they count once.
///  - WinRate: |win-rate gap| of each role &lt;= 3 sigma of the difference of two independent binomial proportions at the
///    tactical rate p (Laplace-adjusted (wins + 1/2)/(n + 1), the role with the wider band): ISqrt(18 p (1000 - p) / n_eff).
///    3 sigma, not 2: the battery makes ~50 checks, so a 2-sigma band rejects an equal-in-distribution model by chance.
///  - CasA/CasB: |median casualty-fraction gap| &lt;= max(substantive band = tactical inter-quartile half-width; sampling floor
///    = 3 sigma of a difference of two medians, 3 * sqrt(2) * 0.929 * IQR / sqrt(n_eff) (0.929 = 1.2533/1.349: the normal
///    approximation of a median's standard error from the IQR); resolution floor = two entities of the role,
///    2 ceil(1000 / Start), since each median is quantised at one entity).
///  - Pulses: |median duration gap| &lt;= max(1, IQR half-width, the same sampling floor on the pulse IQR).
///  - Victor: when the tactical modal winner has a rate &gt;= 600 permille, the AutoResolver's modal winner must equal it.
/// </summary>
public sealed record ParityTolerance(long WinRate, long CasA, long CasB, long Pulses, int ModalWinner, bool ModalRequired)
{
    public const long ModalThresholdPermille = 600;

    /// <summary>ceil(3 * sqrt(2) * 0.92906 * iqr / sqrt(n)) = ceil(39417 * iqr / sqrt(n * 10^8)), integer.</summary>
    public static long MedianSamplingFloor(long iqr, int seeds)
    {
        if (seeds <= 0) throw new ArgumentOutOfRangeException(nameof(seeds));
        long root = (long)FixedMath.ISqrt((ulong)checked(seeds * 100_000_000L));
        return FixedMath.DivCeil(checked(39_417 * iqr), root);
    }

    public static ParityTolerance Derive(ScenarioStats tac, int seeds)
    {
        if (seeds <= 0) throw new ArgumentOutOfRangeException(nameof(seeds));
        long p = FixedMath.MulDivFloor(checked(2 * tac.WinA + 1), 1000, checked(2 * (tac.N + 1)));
        long pb = FixedMath.MulDivFloor(checked(2 * tac.WinB + 1), 1000, checked(2 * (tac.N + 1)));
        long pm = Math.Max(Math.Min(p, 1000 - p), Math.Min(pb, 1000 - pb));   // the wider of the two roles' bands
        long win = (long)FixedMath.ISqrt((ulong)FixedMath.MulDivFloor(checked(18 * pm), 1000 - pm, seeds));
        long CasTol(Dist d, long start) => Math.Max(Math.Max(d.IqrHalfCeil, MedianSamplingFloor(d.P75 - d.P25, seeds)), 2 * FixedMath.DivCeil(1000, start));
        long pul = Math.Max(1, Math.Max(tac.Pulses.IqrHalfCeil, MedianSamplingFloor(tac.Pulses.P75 - tac.Pulses.P25, seeds)));
        return new ParityTolerance(win, CasTol(tac.CasA, tac.StartA), CasTol(tac.CasB, tac.StartB), pul, tac.ModalWinner,
                                   tac.ModalPermille >= ModalThresholdPermille);
    }
}

/// <summary>Result of checking one resolver's stats against a reference under a tolerance.</summary>
public sealed record ParityCheck(long WinAGap, long WinBGap, long CasAGap, long CasBGap, long PulsesGap,
                                 bool WinOk, bool CasAOk, bool CasBOk, bool PulsesOk, bool VictorOk)
{
    public bool Pass => WinOk && CasAOk && CasBOk && PulsesOk && VictorOk;

    public static ParityCheck Of(ScenarioStats reference, ScenarioStats candidate, ParityTolerance tol)
    {
        long wa = Math.Abs(candidate.WinAPermille - reference.WinAPermille), wb = Math.Abs(candidate.WinBPermille - reference.WinBPermille);
        long ca = Math.Abs(candidate.CasA.P50 - reference.CasA.P50), cb = Math.Abs(candidate.CasB.P50 - reference.CasB.P50);
        long pu = Math.Abs(candidate.Pulses.P50 - reference.Pulses.P50);
        return new ParityCheck(wa, wb, ca, cb, pu, wa <= tol.WinRate && wb <= tol.WinRate, ca <= tol.CasA, cb <= tol.CasB, pu <= tol.Pulses,
                               !tol.ModalRequired || candidate.ModalWinner == tol.ModalWinner);
    }
}

/// <summary>
/// The parity battery (spec §5): running cases under either resolver, integer statistics, the calibration loss and the
/// deterministic coordinate-descent fit of <see cref="AutoCoefficients"/>. No parallelism here (the bench parallelises by
/// case index outside the authoritative code; every case is an independent pure function of its inputs).
/// </summary>
public static class ParityBattery
{
    /// <summary>All cases for the given scenarios and seeds [firstSeed, lastSeed], BOTH orientations, in (scenario, seed,
    /// orientation) order.</summary>
    public static ParityCase[] Cases(IReadOnlyList<ParityScenarioId> scenarios, ulong firstSeed, ulong lastSeed)
    {
        var list = new List<ParityCase>();
        foreach (var s in scenarios)
            for (ulong seed = firstSeed; seed <= lastSeed; seed++)
            {
                list.Add(new ParityCase(s, seed, false));
                list.Add(new ParityCase(s, seed, true));
            }
        return list.ToArray();
    }

    /// <summary>
    /// The FIT half of a battery run: the cases with Seed &lt;= <paramref name="lastFitSeed"/> and their outcomes, in case order.
    /// This is the ONLY input the calibration may see (spec §5: fit on half the seeds, report on the other half); the bench
    /// calls it, and the fit-set isolation test calls it on arrays whose holdout entries are corrupted (verify-E E-V6).
    /// </summary>
    public static (ParityCase[] Cases, ParityOutcome[] Outcomes) FitHalf(ParityCase[] cases, ParityOutcome[] outcomes, ulong lastFitSeed)
    {
        if (cases.Length != outcomes.Length) throw new ArgumentException("cases and outcomes differ in length");
        var c = new List<ParityCase>();
        var o = new List<ParityOutcome>();
        for (int i = 0; i < cases.Length; i++)
            if (cases[i].Seed <= lastFitSeed) { c.Add(cases[i]); o.Add(outcomes[i]); }
        return (c.ToArray(), o.ToArray());
    }

    public static ParityScenarioId[] AllScenarioIds()
    {
        var ids = new ParityScenarioId[ParityScenarios.All.Count];
        for (int i = 0; i < ids.Length; i++) ids[i] = ParityScenarios.All[i].Id;
        return ids;
    }

    public static BattleResult RunTacticalResult(ParityCase c, TacticalRules? rules = null)
        => new TacticalBattle(ParityScenarios.Build(c.Scenario, c.Seed, c.Mirrored, rules), new KnowledgeAi(), new KnowledgeAi()).Run();

    public static ParityOutcome RunTactical(ParityCase c, TacticalRules? rules = null) => ParityOutcome.Of(RunTacticalResult(c, rules));

    public static ParityOutcome RunAuto(ParityCase c, AutoCoefficients coefficients, TacticalRules? rules = null)
        => ParityOutcome.Of(new AutoBattle(ParityScenarios.Build(c.Scenario, c.Seed, c.Mirrored, rules), coefficients).Run());

    public static (long A, long B) RoleStarts(ParityScenarioId s)
    {
        var setup = ParityScenarios.Build(s, 1, false);
        long a = 0, b = 0;
        foreach (var f in setup.Formations) { if (f.Side == 0) a += f.Count; else b += f.Count; }
        return (a, b);
    }

    /// <summary>Stats of one scenario over the subset of (cases, outcomes) selected by <paramref name="include"/>.</summary>
    public static ScenarioStats Stats(ParityScenarioId s, ParityCase[] cases, ParityOutcome[] outcomes, Func<ParityCase, bool> include)
    {
        var o = new List<ParityOutcome>();
        for (int i = 0; i < cases.Length; i++) if (cases[i].Scenario == s && include(cases[i])) o.Add(outcomes[i]);
        var (a, b) = RoleStarts(s);
        return ScenarioStats.Of(o, a, b);
    }

    /// <summary>Per-case victor agreement: number of cases (of scenario s, selected) where both resolvers name the same
    /// winner (draw = -1 counts as a value), and the number selected.</summary>
    public static (int Agree, int N) Agreement(ParityScenarioId s, ParityCase[] cases, ParityOutcome[] tac, ParityOutcome[] auto, Func<ParityCase, bool> include)
    {
        int agree = 0, n = 0;
        for (int i = 0; i < cases.Length; i++)
        {
            if (cases[i].Scenario != s || !include(cases[i])) continue;
            n++;
            if (tac[i].Winner == auto[i].Winner) agree++;
        }
        return (agree, n);
    }

    /// <summary>
    /// Calibration loss of one scenario (integer, permille units): 2(|dWinA| + |dWinB|) + |dCasA.P50| + |dCasB.P50|
    /// + (|dCasA.P10| + |dCasA.P90| + |dCasB.P10| + |dCasB.P90|)/2 + 100 |dPulses.P50|.
    /// </summary>
    public static long Loss(ScenarioStats t, ScenarioStats a)
    {
        long l = 2 * (Math.Abs(t.WinAPermille - a.WinAPermille) + Math.Abs(t.WinBPermille - a.WinBPermille));
        l += Math.Abs(t.CasA.P50 - a.CasA.P50) + Math.Abs(t.CasB.P50 - a.CasB.P50);
        l += (Math.Abs(t.CasA.P10 - a.CasA.P10) + Math.Abs(t.CasA.P90 - a.CasA.P90) + Math.Abs(t.CasB.P10 - a.CasB.P10) + Math.Abs(t.CasB.P90 - a.CasB.P90)) / 2;
        l += 100 * Math.Abs(t.Pulses.P50 - a.Pulses.P50);
        return l;
    }

    /// <summary>Total loss of a coefficient set over the given cases (tactical outcomes aligned by index).</summary>
    public static long TotalLoss(AutoCoefficients c, ParityCase[] cases, ParityOutcome[] tactical, IReadOnlyList<ParityScenarioId> scenarios)
    {
        var auto = new ParityOutcome[cases.Length];
        for (int i = 0; i < cases.Length; i++) auto[i] = RunAuto(cases[i], c);
        long total = 0;
        foreach (var s in scenarios)
            total = checked(total + Loss(Stats(s, cases, tactical, _ => true), Stats(s, cases, auto, _ => true)));
        return total;
    }
}

/// <summary>One coefficient of the fit: its name, the candidate grid, and how to read/write it.</summary>
public sealed record CoefficientAxis(string Name, long[] Candidates, Func<AutoCoefficients, long> Get, Func<AutoCoefficients, long, AutoCoefficients> Set);

/// <summary>One accepted step of the fit (for the report).</summary>
public readonly record struct FitStep(int Sweep, string Coefficient, long From, long To, long Loss);

/// <summary>
/// Deterministic calibration of <see cref="AutoCoefficients"/> by coordinate descent over integer candidate grids, minimising
/// <see cref="ParityBattery.TotalLoss"/> (integer) on the FIT cases only. Ties: the current value is kept when a candidate only
/// equals it; among new candidates the earlier grid entry wins (stable, input-order independent). Stops after a sweep with
/// no change or after <paramref name="maxSweeps"/>.
/// </summary>
public static class AutoCalibration
{
    public static CoefficientAxis[] DefaultAxes() => new[]
    {
        new CoefficientAxis("MeleeEngagePermille", new long[] { 50, 100, 150, 200, 300, 400, 500, 600, 800, 1000 }, c => c.MeleeEngagePermille, (c, v) => c with { MeleeEngagePermille = v }),
        new CoefficientAxis("MaxAttackersPerTargetPermille", new long[] { 500, 750, 1000, 1250, 1500, 2000, 3000, 1_000_000 }, c => c.MaxAttackersPerTargetPermille, (c, v) => c with { MaxAttackersPerTargetPermille = v }),
        new CoefficientAxis("RangedEngagePermille", new long[] { 50, 100, 200, 300, 400, 600, 800, 1000 }, c => c.RangedEngagePermille, (c, v) => c with { RangedEngagePermille = v }),
        new CoefficientAxis("KillEfficiencyPermille", new long[] { 300, 400, 500, 600, 700, 800, 900, 1000 }, c => c.KillEfficiencyPermille, (c, v) => c with { KillEfficiencyPermille = v }),
        new CoefficientAxis("FortunePermille", new long[] { 0, 25, 50, 100, 150, 200, 300, 400, 500, 600 }, c => c.FortunePermille, (c, v) => c with { FortunePermille = v }),
        new CoefficientAxis("NoisePermille", new long[] { 0, 100, 200, 300, 500 }, c => c.NoisePermille, (c, v) => c with { NoisePermille = v }),
        new CoefficientAxis("AmbushFirstStrikeSteps", new long[] { 0, 1, 2, 4, 8, 15 }, c => c.AmbushFirstStrikeSteps, (c, v) => c with { AmbushFirstStrikeSteps = v }),
        new CoefficientAxis("SurrenderPermille", new long[] { 0, 10, 25, 50, 100 }, c => c.SurrenderPermille, (c, v) => c with { SurrenderPermille = v }),
    };

    public static (AutoCoefficients Fitted, long Loss, List<FitStep> Steps, int Evaluations) Fit(
        AutoCoefficients start, CoefficientAxis[] axes, ParityCase[] fitCases, ParityOutcome[] fitTactical,
        IReadOnlyList<ParityScenarioId> scenarios, int maxSweeps)
    {
        var cur = start;
        long best = ParityBattery.TotalLoss(cur, fitCases, fitTactical, scenarios);
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
                    long l = ParityBattery.TotalLoss(cand, fitCases, fitTactical, scenarios);
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
