using System.Diagnostics;
using System.Globalization;
using System.Text;
using Proto.M7.Tactical;

// Slice E bench: the full parity battery (spec §5). Stopwatch/double/Parallel for REPORTING and wall-clock only: every case is
// an independent pure function of (scenario, seed, orientation), results are stored by case index, so the parallel schedule
// cannot change any number. Usage:
//   dotnet Proto.M7.Bench.dll parity [seeds=40] [threads=4] [out.md] [out.csv] [firstSeed=1] [fit|frozen]
// 'frozen' (verify-E E-V7) skips the calibration and uses the shipped AutoCoefficients defaults: run on seeds never used before
// (e.g. firstSeed 41) it is a confirmation of the frozen model AND the frozen tolerance rule, both fixed before the run.
static class ParityBench
{
    static string F(double v) => v.ToString("F1", CultureInfo.InvariantCulture);
    static string P(long permille) => (permille / 10.0).ToString("F1", CultureInfo.InvariantCulture);

    public static void Run(string[] args)
    {
        int seeds = args.Length > 1 ? int.Parse(args[1], CultureInfo.InvariantCulture) : 40;
        int threads = args.Length > 2 ? int.Parse(args[2], CultureInfo.InvariantCulture) : 4;
        string? mdPath = args.Length > 3 ? args[3] : null;
        string? csvPath = args.Length > 4 ? args[4] : null;
        ulong firstSeed = args.Length > 5 ? ulong.Parse(args[5], CultureInfo.InvariantCulture) : 1;
        bool frozen = args.Length > 6 && args[6] == "frozen";
        if (args.Length > 6 && !frozen && args[6] != "fit") throw new ArgumentException("mode must be 'fit' or 'frozen'");
        if (seeds < 2 || seeds % 2 != 0) throw new ArgumentException("seeds must be even (fit half / holdout half)");
        if (firstSeed < 1) throw new ArgumentException("firstSeed must be >= 1");
        var ids = ParityBattery.AllScenarioIds();
        ulong lastSeed = firstSeed + (ulong)seeds - 1;
        var cases = ParityBattery.Cases(ids, firstSeed, lastSeed);
        ulong half = (ulong)(seeds / 2);
        ulong lastFit = firstSeed + half - 1;
        bool IsFit(ParityCase c) => c.Seed <= lastFit;
        bool IsHold(ParityCase c) => c.Seed > lastFit;
        var md = new StringBuilder();
        void W(string s) { Console.WriteLine(s); md.AppendLine(s); }

        W($"Proto.M7.Bench parity: scenarios={ids.Length} seeds={firstSeed}-{lastSeed} mode={(frozen ? "frozen (no calibration; shipped coefficients)" : "fit")} orientations=2 cases={cases.Length} threads={threads} cpu={Environment.ProcessorCount} os={System.Runtime.InteropServices.RuntimeInformation.OSDescription} runtime={Environment.Version}");
        var total = Stopwatch.StartNew();

        // 1. Tactical runs (the expensive part).
        var tac = new ParityOutcome[cases.Length];
        var tacMs = new double[cases.Length];
        var tacEvents = new long[cases.Length];
        var sw = Stopwatch.StartNew();
        Parallel.For(0, cases.Length, new ParallelOptions { MaxDegreeOfParallelism = threads }, i =>
        {
            var t0 = Stopwatch.GetTimestamp();
            var r = ParityBattery.RunTacticalResult(cases[i]);
            tacMs[i] = Stopwatch.GetElapsedTime(t0).TotalMilliseconds;
            tac[i] = ParityOutcome.Of(r);
            tacEvents[i] = r.EventsExecuted;
            foreach (var f in r.Formations) if (!f.Conserved) throw new InvalidOperationException("conservation");
        });
        sw.Stop();
        double tacWall = sw.Elapsed.TotalMilliseconds, tacCpu = tacMs.Sum();
        W($"tactical: {cases.Length} battles, wall {F(tacWall / 1000)} s, sum of per-battle times {F(tacCpu / 1000)} s, mean {F(tacCpu / cases.Length)} ms/battle, max {F(tacMs.Max())} ms");

        // 2. Calibration on the FIT half only.
        var (fitCases, fitTac) = ParityBattery.FitHalf(cases, tac, lastFit);   // same split as IsFit; tested (verify-E E-V6)
        var start = new AutoCoefficients();
        var initial = new AutoCoefficients
        {
            MeleeEngagePermille = 600, MaxAttackersPerTargetPermille = 2000, RangedEngagePermille = 600, KillEfficiencyPermille = 1000, FortunePermille = 100,
            NoisePermille = 200, AmbushFirstStrikeSteps = 2, SurrenderPermille = 0,
        };
        sw.Restart();
        var (fitted, fitLoss, steps, evals) = frozen
            ? (start, ParityBattery.TotalLoss(start, fitCases, fitTac, ids), new List<FitStep>(), 1)
            : AutoCalibration.Fit(initial, AutoCalibration.DefaultAxes(), fitCases, fitTac, ids, 6);
        sw.Stop();
        double calMs = sw.Elapsed.TotalMilliseconds;
        long initialLoss = ParityBattery.TotalLoss(initial, fitCases, fitTac, ids);
        if (frozen) W($"calibration: SKIPPED (frozen mode): shipped AutoCoefficients defaults; loss on the first half {fitLoss}");
        else W($"calibration: coordinate descent from the pre-fit start, {evals} loss evaluations x {fitCases.Length} auto battles, {F(calMs / 1000)} s; fit loss {initialLoss} -> {fitLoss}");
        foreach (var s in steps) W($"  sweep {s.Sweep}: {s.Coefficient} {s.From} -> {s.To} (loss {s.Loss})");
        W($"fitted: Melee {fitted.MeleeEngagePermille} MaxAttackersPerTarget {fitted.MaxAttackersPerTargetPermille} Ranged {fitted.RangedEngagePermille} KillEff {fitted.KillEfficiencyPermille} Fortune {fitted.FortunePermille} Noise {fitted.NoisePermille} AmbushSteps {fitted.AmbushFirstStrikeSteps} Surrender {fitted.SurrenderPermille}");
        W($"shipped AutoCoefficients defaults equal the fitted values: {(fitted == start ? "YES" : "NO (update the defaults)")}");

        // 3. Auto runs with the fitted coefficients on every case.
        var auto = new ParityOutcome[cases.Length];
        sw.Restart();
        for (int i = 0; i < cases.Length; i++) auto[i] = ParityBattery.RunAuto(cases[i], fitted);
        sw.Stop();
        W($"auto: {cases.Length} battles, {F(sw.Elapsed.TotalMilliseconds)} ms ({F(sw.Elapsed.TotalMilliseconds * 1000 / cases.Length)} us/battle, single thread)");
        total.Stop();
        W($"battery total wall {F(total.Elapsed.TotalSeconds)} s");
        W("");

        // 4. Tables.
        W("### Tactical distributions (all seeds, both orientations; n per scenario = " + (cases.Length / ids.Length) + ")");
        W("");
        W("| scenario | A wins % | B wins % | draw % | cas A p10/p50/p90 % | cas B p10/p50/p90 % | pulses p10/p50/p90 | mean ms/battle |");
        W("|---|---:|---:|---:|---|---|---|---:|");
        foreach (var s in ids)
        {
            var t = ParityBattery.Stats(s, cases, tac, _ => true);
            double ms = Enumerable.Range(0, cases.Length).Where(i => cases[i].Scenario == s).Average(i => tacMs[i]);
            W($"| {ParityScenarios.Get(s).Name} | {P(t.WinAPermille)} | {P(t.WinBPermille)} | {P(t.DrawPermille)} | {P(t.CasA.P10)}/{P(t.CasA.P50)}/{P(t.CasA.P90)} | {P(t.CasB.P10)}/{P(t.CasB.P50)}/{P(t.CasB.P90)} | {t.Pulses.P10}/{t.Pulses.P50}/{t.Pulses.P90} | {F(ms)} |");
        }
        W("");
        W("### End reasons (tactical / auto, all cases)");
        W("");
        W("| scenario | tactical: elim / both-elim / side-diseng / both-diseng | auto: elim / both-elim / side-diseng / both-diseng |");
        W("|---|---|---|");
        foreach (var s in ids)
        {
            string R(ParityOutcome[] o) => string.Join(" / ", new[] { BattleEndReason.SideEliminated, BattleEndReason.BothEliminated, BattleEndReason.SideDisengaged, BattleEndReason.BothDisengaged }
                .Select(r => Enumerable.Range(0, cases.Length).Count(i => cases[i].Scenario == s && o[i].Reason == r)));
            W($"| {ParityScenarios.Get(s).Name} | {R(tac)} | {R(auto)} |");
        }
        W("");
        W("### Orientation check (tactical, all seeds): A win % as-built vs mirrored");
        W("");
        W("| scenario | as-built A wins % | mirrored A wins % | as-built cas A p50 % | mirrored cas A p50 % |");
        W("|---|---:|---:|---:|---:|");
        foreach (var s in ids)
        {
            var a = ParityBattery.Stats(s, cases, tac, c => !c.Mirrored);
            var m = ParityBattery.Stats(s, cases, tac, c => c.Mirrored);
            W($"| {ParityScenarios.Get(s).Name} | {P(a.WinAPermille)} | {P(m.WinAPermille)} | {P(a.CasA.P50)} | {P(m.CasA.P50)} |");
        }
        W("");

        foreach (var (label, sel) in new (string, Func<ParityCase, bool>)[] { ("FIT half (seeds " + firstSeed + "-" + lastFit + ")", IsFit), ("HOLDOUT half (seeds " + (lastFit + 1) + "-" + lastSeed + ")", IsHold) })
        {
            W($"### Tactical vs Auto — {label}, both orientations");
            W("");
            W("| scenario | A wins % T / Au | B wins % T / Au | draw % T / Au | cas A p10/p50/p90 % T | cas A p10/p50/p90 % Au | cas B p10/p50/p90 % T | cas B p10/p50/p90 % Au | pulses p50 T / Au (p10-p90 T) | per-case victor agreement % (indep. expectation %) |");
            W("|---|---|---|---|---|---|---|---|---|---|");
            foreach (var s in ids)
            {
                var t = ParityBattery.Stats(s, cases, tac, sel);
                var a = ParityBattery.Stats(s, cases, auto, sel);
                var (ag, n) = ParityBattery.Agreement(s, cases, tac, auto, sel);
                double exp = (t.WinA * (double)a.WinA + t.WinB * (double)a.WinB + t.Draw * (double)a.Draw) / ((double)t.N * a.N) * 100;
                W($"| {ParityScenarios.Get(s).Name} | {P(t.WinAPermille)} / {P(a.WinAPermille)} | {P(t.WinBPermille)} / {P(a.WinBPermille)} | {P(t.DrawPermille)} / {P(a.DrawPermille)} | {P(t.CasA.P10)}/{P(t.CasA.P50)}/{P(t.CasA.P90)} | {P(a.CasA.P10)}/{P(a.CasA.P50)}/{P(a.CasA.P90)} | {P(t.CasB.P10)}/{P(t.CasB.P50)}/{P(t.CasB.P90)} | {P(a.CasB.P10)}/{P(a.CasB.P50)}/{P(a.CasB.P90)} | {t.Pulses.P50} / {a.Pulses.P50} ({t.Pulses.P10}-{t.Pulses.P90}) | {F(ag * 100.0 / n)} ({F(exp)}) |");
            }
            W("");
        }

        W("### Proposed tolerance (derived from the TACTICAL fit half) and the HOLDOUT check");
        W("");
        W("Columns (all in percentage points except pulses): tolerance (win-rate band / cas A / cas B / pulses / modal victor required?), then the holdout gaps of Auto vs Tactical, and of Tactical-holdout vs Tactical-fit (self-consistency: the tolerance must accept the tactical resolver against itself).");
        W("");
        W("| scenario | tol win | tol casA | tol casB | tol pulses | modal victor | Auto gaps win A/B, casA, casB, pulses | Auto pass | Tac self gaps | Tac self pass |");
        W("|---|---:|---:|---:|---:|---|---|---|---|---|");
        int autoPass = 0, selfPass = 0;
        foreach (var s in ids)
        {
            var tf = ParityBattery.Stats(s, cases, tac, IsFit);
            var th = ParityBattery.Stats(s, cases, tac, IsHold);
            var ah = ParityBattery.Stats(s, cases, auto, IsHold);
            var tol = ParityTolerance.Derive(tf, (int)half);
            var ca = ParityCheck.Of(th, ah, tol);
            var cs = ParityCheck.Of(th, tf, tol);
            if (ca.Pass) autoPass++;
            if (cs.Pass) selfPass++;
            string Gaps(ParityCheck c) => $"{P(c.WinAGap)}/{P(c.WinBGap)}, {P(c.CasAGap)}, {P(c.CasBGap)}, {c.PulsesGap}";
            string Why(ParityCheck c) => c.Pass ? "PASS" : "FAIL(" + string.Join(",", new[] { c.WinOk ? null : "win", c.CasAOk ? null : "casA", c.CasBOk ? null : "casB", c.PulsesOk ? null : "pulses", c.VictorOk ? null : "victor" }.Where(x => x != null)) + ")";
            W($"| {ParityScenarios.Get(s).Name} | {P(tol.WinRate)} | {P(tol.CasA)} | {P(tol.CasB)} | {tol.Pulses} | {(tol.ModalRequired ? (tol.ModalWinner == 0 ? "A" : tol.ModalWinner == 1 ? "B" : "draw") : "no")} | {Gaps(ca)} | {Why(ca)} | {Gaps(cs)} | {Why(cs)} |");
        }
        W("");
        W($"holdout: Auto passes {autoPass}/{ids.Length} scenarios; Tactical-vs-itself passes {selfPass}/{ids.Length}");

        if (mdPath is not null) File.WriteAllText(mdPath, md.ToString());
        if (csvPath is not null)
        {
            var csv = new StringBuilder("scenario,seed,mirrored,half,tac_winner,tac_casA,tac_casB,tac_pulses,tac_reason,tac_events,tac_ms,auto_winner,auto_casA,auto_casB,auto_pulses,auto_reason\n");
            for (int i = 0; i < cases.Length; i++)
            {
                var c = cases[i];
                csv.Append(CultureInfo.InvariantCulture, $"{ParityScenarios.Get(c.Scenario).Name},{c.Seed},{(c.Mirrored ? 1 : 0)},{(IsFit(c) ? "fit" : "holdout")},{tac[i].Winner},{tac[i].CasA},{tac[i].CasB},{tac[i].Pulses},{tac[i].Reason},{tacEvents[i]},{F(tacMs[i])},{auto[i].Winner},{auto[i].CasA},{auto[i].CasB},{auto[i].Pulses},{auto[i].Reason}\n");
            }
            File.WriteAllText(csvPath, csv.ToString());
        }
    }
}
