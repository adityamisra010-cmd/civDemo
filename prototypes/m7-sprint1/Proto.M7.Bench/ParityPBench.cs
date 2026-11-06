using System.Diagnostics;
using System.Globalization;
using System.Security.Cryptography;
using System.Text;
using Proto.M7.Tactical;

// [P] Lane P parity battery bench (DIR5 §3.1). Stopwatch / double / Parallel are for REPORTING and wall-clock only: every
// case is an independent pure function of (scenario, seed), results are stored by case index, so the schedule cannot change
// a number. Usage:
//   parityp sanity [seeds=2] [threads=2]                       tactical only, every scenario, a few seeds; deployment check
//   parityp run <tuning|acceptance|control> <out.csv> [threads=2]  tactical outcomes of a seed set -> CSV cache
//   parityp eval <tactical.csv> [model=new|sprint1]            auto on the same cases, rule-P1 checks against the CSV
//   parityp fit <tuning.csv> [sweeps=4]                        coordinate descent on the TUNING cases only
//   parityp accept <acceptance.csv> <control.csv> <tuning.csv> [model=new|sprint1] [out.md] [out.csv]
static class ParityPBench
{
    static string F(double v) => v.ToString("F1", CultureInfo.InvariantCulture);
    static string Pm(long permille) => (permille / 10.0).ToString("F1", CultureInfo.InvariantCulture);

    public static void Run(string[] args)
    {
        string mode = args.Length > 1 ? args[1] : "sanity";
        switch (mode)
        {
            case "sanity": Sanity(args); break;
            case "run": RunSet(args); break;
            case "eval": Eval(args); break;
            case "fit": Fit(args); break;
            case "accept": Accept(args); break;
            case "diag": Diag(args); break;
            case "subset": SubsetDigests(); break;
            case "selfcheck": SelfCheck(args); break;
            default: throw new ArgumentException("mode: sanity | run | eval | fit | accept");
        }
    }

    static int Arg(string[] a, int i, int dflt) => a.Length > i ? int.Parse(a[i], CultureInfo.InvariantCulture) : dflt;

    /// <summary>"new" | "sprint1", optionally followed by ";Axis=value;..." overrides (axes of ParityCalibrationP.DefaultAxes).</summary>
    public static AutoCoefficients Model(string spec)
    {
        var parts = spec.Split(';');
        var c = parts[0] switch
        {
            "new" => new AutoCoefficients(),
            "sprint1" => AutoCoefficients.Sprint1,
            _ => throw new ArgumentException("model must be new or sprint1"),
        };
        var axes = ParityCalibrationP.DefaultAxes();
        for (int i = 1; i < parts.Length; i++)
        {
            var kv = parts[i].Split('=');
            var ax = axes.Single(a => a.Name == kv[0]);
            c = ax.Set(c, long.Parse(kv[1], CultureInfo.InvariantCulture));
        }
        c.Validate();
        return c;
    }

    // ------------------------------------------------------------------ sanity

    static void Sanity(string[] args)
    {
        int seeds = Arg(args, 2, 2), threads = Arg(args, 3, 2);
        Console.WriteLine($"parityp sanity: seeds/scenario={seeds} threads={threads} (tuning scenarios: tuning seeds 1..; held-out: control seeds 9001..)");
        foreach (var d in ParityBatteryP.All)
        {
            foreach (bool mir in new[] { false, true })
            {
                ulong seed = d.HeldOut ? (mir ? 9002UL : 9001UL) : (mir ? 2UL : 1UL);
                var v = DeploymentZones.Check(ParityBatteryP.Build(d.Id, seed));
                if (!v.Accepted) Console.WriteLine($"  DEPLOYMENT {d.Name} mirrored={mir}: {v.Refusal} entity {v.EntityId}");
            }
            var cases = ParityBatteryP.Cases(new[] { d.Id }, d.HeldOut ? ParityBatteryP.FirstControlSeed : 1, (d.HeldOut ? ParityBatteryP.FirstControlSeed : 1) + (ulong)seeds - 1);
            var o = new POutcome[cases.Length]; var ms = new double[cases.Length]; var ev = new long[cases.Length];
            Parallel.For(0, cases.Length, new ParallelOptions { MaxDegreeOfParallelism = threads }, i =>
            {
                var t0 = Stopwatch.GetTimestamp();
                var r = ParityBatteryP.RunTacticalResult(cases[i]);
                ms[i] = Stopwatch.GetElapsedTime(t0).TotalMilliseconds;
                o[i] = POutcome.Of(r, 0); ev[i] = r.EventsExecuted;
            });
            Console.WriteLine($"  {d.Name,-26} {(d.HeldOut ? "H" : "T")} " + string.Join(" | ", o.Select((x, i) => $"s{cases[i].Seed}: win {x.Winner} casA {x.CasA} casB {x.CasB} pulses {x.Pulses} dur {x.DurationMs / 1000}s {x.Reason} ({F(ms[i])} ms, {ev[i]} ev)")));
        }
    }

    /// <summary>The digests the ParityPTests golden pins (5 scenarios x tuning seeds 1-4), for re-pinning with attribution.</summary>
    static void SubsetDigests()
    {
        var ids = new[] { PScenario.EqualInfantry, PScenario.HillCrest, PScenario.FordCrossing, PScenario.SniperScreen, PScenario.ForestHoldArchers };
        var cases = ParityBatteryP.Cases(ids, 1, 4);
        var tac = cases.Select(ParityBatteryP.RunTactical).ToArray();
        var auto = cases.Select(c => ParityBatteryP.RunAuto(c, new AutoCoefficients())).ToArray();
        Console.WriteLine($"subset tactical {Digest(cases, tac)}");
        Console.WriteLine($"subset auto     {Digest(cases, auto)}");
    }

    // ------------------------------------------------------------------ diagnostics (one case, both resolvers)

    static void Diag(string[] args)
    {
        var id = (PScenario)int.Parse(args[2], CultureInfo.InvariantCulture);
        ulong seed = ulong.Parse(args[3], CultureInfo.InvariantCulture);
        var model = Model(args.Length > 4 ? args[4] : "new");
        var setup = ParityBatteryP.Build(id, seed);
        var fids = setup.Formations.Select(f => f.Id).OrderBy(x => x).ToArray();
        Console.WriteLine($"diag {ParityBatteryP.Get(id).Name} seed {seed} mirrored={ParityBatteryP.Mirrored(seed)}");
        var tb = new TacticalBattle(setup, new KnowledgeAi(), new KnowledgeAi());
        tb.Start();
        var firstKnown = new Dictionary<(int, long), long>();
        long lastPulse = -1;
        while (true)
        {
            bool more = tb.Advance();
            long t = tb.Scheduler.NowMs;
            foreach (var e in tb.Entities())
                for (int side = 0; side < 2; side++)
                    if (e.Side != side && tb.EverDetected(side, e.Id) && !firstKnown.ContainsKey((side, e.FormationId))) firstKnown[(side, e.FormationId)] = t;
            if (tb.CurrentPulse != lastPulse || !more)
            {
                lastPulse = tb.CurrentPulse;
                var ents = tb.Entities();
                Console.WriteLine($"  T pulse {tb.CurrentPulse} t={t / 1000}s: " + string.Join(" | ", fids.Select(f =>
                    $"f{f} act {ents.Count(e => e.FormationId == f && e.State == EntityState.Active)} rout {ents.Count(e => e.FormationId == f && e.State == EntityState.Routing)} dead {ents.Count(e => e.FormationId == f && e.State == EntityState.Dead)} mor {tb.Morale(f)} dir {tb.Directive(f)} x {tb.Anchor(f).X / 1000}")));
            }
            if (!more) break;
        }
        var tr = tb.Result!;
        Console.WriteLine($"  T end {tr.Reason} holds {tr.HoldsField} pulses {tr.Pulses} t={tr.EndMs / 1000}s; first known (ms): " + string.Join(", ", firstKnown.OrderBy(kv => kv.Key).Select(kv => $"side{kv.Key.Item1}->f{kv.Key.Item2} {kv.Value / 1000}s")));
        foreach (var f in tr.Formations) Console.WriteLine($"    T f{f.FormationId}: start {f.Start} killed {f.Killed} surr {f.Surrendered} active {f.ActiveAtEnd} esc {f.Escaped} (routed {f.RoutedEscaped}) dealt {f.DamageDealt} taken {f.DamageTaken}");
        var ab = new AutoBattle(setup, model) { RecordLog = true };
        var ar = ab.Run();
        foreach (var g in ab.Log.GroupBy(r => r.Step / 30))
        {
            var last = g.GroupBy(r => r.FormationId).Select(x => x.Last());
            Console.WriteLine($"  A pulse {g.Key} : " + string.Join(" | ", last.Select(r => $"f{r.FormationId} alive {r.Alive} mor {r.Morale} x {r.PosMm / 1000} tgt {r.Target} mode {r.Mode}")));
        }
        Console.WriteLine($"  A end {ar.Reason} holds {ar.HoldsField} pulses {ar.Pulses} t={ar.EndMs / 1000}s; first known (step*2s): " + string.Join(", ", fids.SelectMany(f => new[] { 0, 1 }.Select(side => (side, f))).Where(x => setup.Formations.First(ff => ff.Id == x.f).Side != x.side).Select(x => $"side{x.side}->f{x.f} {ab.FirstKnownStep(x.side, x.f) * 2}s")));
        foreach (var f in ar.Formations) Console.WriteLine($"    A f{f.FormationId}: start {f.Start} killed {f.Killed} surr {f.Surrendered} active {f.ActiveAtEnd} esc {f.Escaped} (routed {f.RoutedEscaped}) dealt {f.DamageDealt} taken {f.DamageTaken}");
        var firstContact = ab.Log.FirstOrDefault(r => (r.Mode & 1) != 0);
        var firstShot = ab.Log.FirstOrDefault(r => (r.Mode & 2) != 0);
        Console.WriteLine($"  A first melee step {(firstContact.FormationId == 0 ? -1 : firstContact.Step)} first shot step {(firstShot.FormationId == 0 ? -1 : firstShot.Step)}");
    }

    // ------------------------------------------------------------------ tactical CSV cache

    static (ulong, ulong, PScenario[]) SetOf(string set) => set switch
    {
        "tuning" => (ParityBatteryP.FirstTuningSeed, ParityBatteryP.LastTuningSeed, ParityBatteryP.TuningScenarios()),
        "acceptance" => (ParityBatteryP.FirstAcceptanceSeed, ParityBatteryP.LastAcceptanceSeed, ParityBatteryP.AllScenarios()),
        "control" => (ParityBatteryP.FirstControlSeed, ParityBatteryP.LastControlSeed, ParityBatteryP.HeldOutScenarios()),
        // [P fix r1, P-V6] the TUNING scenarios on the control seeds (never run before): an n = 160 tactical control sample, so the
        // attainability self-check of a tuning scenario faces the bands the candidate faced (n_C = 160), not the n = 80 ones
        "control-tuning" => (ParityBatteryP.FirstControlSeed, ParityBatteryP.LastControlSeed, ParityBatteryP.TuningScenarios()),
        _ => throw new ArgumentException("set must be tuning, acceptance, control or control-tuning"),
    };

    static void RunSet(string[] args)
    {
        string set = args[2], path = args[3];
        int threads = Arg(args, 4, 2);
        var (f, l, ids) = SetOf(set);
        var cases = ParityBatteryP.Cases(ids, f, l);
        var o = new POutcome[cases.Length]; var ms = new double[cases.Length]; var ev = new long[cases.Length];
        var sw = Stopwatch.StartNew();
        Parallel.For(0, cases.Length, new ParallelOptions { MaxDegreeOfParallelism = threads }, i =>
        {
            var t0 = Stopwatch.GetTimestamp();
            var r = ParityBatteryP.RunTacticalResult(cases[i]);
            foreach (var fr in r.Formations) if (!fr.Conserved) throw new InvalidOperationException("conservation");
            ms[i] = Stopwatch.GetElapsedTime(t0).TotalMilliseconds;
            o[i] = POutcome.Of(r, 0); ev[i] = r.EventsExecuted;
        });
        sw.Stop();
        var sb = new StringBuilder(ParityPCsv.Header).Append('\n');                  // [P fix r1] format shared with the parser (ParityPCsv)
        for (int i = 0; i < cases.Length; i++) sb.Append(ParityPCsv.Row(cases[i], o[i], ev[i], F(ms[i]))).Append('\n');
        File.WriteAllText(path, sb.ToString());
        Console.WriteLine($"parityp run {set}: {cases.Length} tactical battles, wall {F(sw.Elapsed.TotalSeconds)} s, sum {F(ms.Sum() / 1000)} s, threads {threads} -> {path}");
        Console.WriteLine($"tactical outcome digest (no timing column): {Digest(cases, o)}");
    }

    public static (PCase[] Cases, POutcome[] Outcomes) Load(string path) => ParityPCsv.ParseAll(File.ReadAllLines(path));   // [P fix r1]

    public static string Digest(PCase[] cases, POutcome[] o) => ParityPCsv.Digest(cases, o);

    // ------------------------------------------------------------------ evaluation

    static PStats StatsOf(PScenario s, PCase[] cases, POutcome[] o)
    {
        var l = new List<POutcome>();
        for (int i = 0; i < cases.Length; i++) if (cases[i].Scenario == s) l.Add(o[i]);
        return PStats.Of(l);
    }

    static PScenario[] ScenariosIn(PCase[] cases)
    {
        var l = new List<PScenario>();
        foreach (var c in cases) if (!l.Contains(c.Scenario)) l.Add(c.Scenario);
        return l.ToArray();
    }

    public static POutcome[] RunAutoAll(PCase[] cases, AutoCoefficients c)
    {
        var a = new POutcome[cases.Length];
        for (int i = 0; i < cases.Length; i++) a[i] = ParityBatteryP.RunAuto(cases[i], c);
        return a;
    }

    static void Eval(string[] args)
    {
        var (cases, tac) = Load(args[2]);
        var model = Model(args.Length > 3 ? args[3] : "new");
        var sw = Stopwatch.StartNew();
        var auto = RunAutoAll(cases, model);
        sw.Stop();
        Console.WriteLine($"parityp eval {args[2]} model={(args.Length > 3 ? args[3] : "new")}: {cases.Length} auto battles {F(sw.Elapsed.TotalMilliseconds)} ms; loss {ParityCalibrationP.Loss(cases, tac, auto)}");
        Table(cases, tac, auto, Console.WriteLine);
    }

    static void Table(PCase[] cases, POutcome[] tac, POutcome[] auto, Action<string> W)
    {
        W("| scenario | fam | n | win A/B/- % T | win A/B/- % Au (band) | casA mean T/Au (band) | casB mean T/Au (band) | dur mean s T/Au (band) | pulses p50 T/Au | case agree % (indep. %) | verdict |");
        W("|---|---|---:|---|---|---|---|---|---|---|---|");
        int pass = 0, n = 0;
        foreach (var s in ScenariosIn(cases))
        {
            var d = ParityBatteryP.Get(s);
            var t = StatsOf(s, cases, tac); var a = StatsOf(s, cases, auto);
            var ck = ParityCriteriaP1.Check(t, a, ParityBatteryP.RoleStart(s, 0), ParityBatteryP.RoleStart(s, 1), 2_000);
            int agree = 0, m = 0;
            for (int i = 0; i < cases.Length; i++) if (cases[i].Scenario == s) { m++; if (tac[i].Winner == auto[i].Winner) agree++; }
            double indep = (t.WinA * (double)a.WinA + t.WinB * (double)a.WinB + t.Draw * (double)a.Draw) / ((double)t.N * a.N) * 100;
            n++; if (ck.Pass) pass++;
            W($"| {d.Name} | {d.Family} | {t.N} | {Pm(1000L * t.WinA / t.N)}/{Pm(1000L * t.WinB / t.N)}/{Pm(1000L * t.Draw / t.N)} | {Pm(1000L * a.WinA / a.N)}/{Pm(1000L * a.WinB / a.N)}/{Pm(1000L * a.Draw / a.N)} ({Pm(ck.WinBand)}) | {Pm(PStats.MeanFloor(t.CasASum, t.N))}/{Pm(PStats.MeanFloor(a.CasASum, a.N))} ({Pm(ck.CasABand)}) | {Pm(PStats.MeanFloor(t.CasBSum, t.N))}/{Pm(PStats.MeanFloor(a.CasBSum, a.N))} ({Pm(ck.CasBBand)}) | {F(t.DurSum / 1000.0 / t.N)}/{F(a.DurSum / 1000.0 / a.N)} ({F(ck.DurBand / 1000.0)}) | {t.Pulses.P50}/{a.Pulses.P50} | {F(agree * 100.0 / m)} ({F(indep)}) | {(ck.Pass ? "PASS" : "FAIL(" + ck.FailedList() + ")")} |");
        }
        W($"scenarios passing: {pass}/{n}");
    }

    // ------------------------------------------------------------------ fit (tuning cases only)

    static void Fit(string[] args)
    {
        var (cases, tac) = Load(args[2]);
        int sweeps = Arg(args, 3, 4);
        foreach (var c in cases)
            if (!ParityBatteryP.IsTuningCase(c)) throw new InvalidOperationException($"refused: case {c} is not a tuning case (held-out scenario or non-tuning seed)");
        var sw = Stopwatch.StartNew();
        var start = Model(args.Length > 4 ? args[4] : "new");
        Console.WriteLine($"start: {start}");
        var (fitted, loss, steps, evals) = ParityCalibrationP.Fit(start, ParityCalibrationP.DefaultAxes(), cases, tac, sweeps);
        sw.Stop();
        Console.WriteLine($"parityp fit: {evals} evaluations x {cases.Length} auto battles, {F(sw.Elapsed.TotalSeconds)} s, loss -> {loss}");
        foreach (var s in steps) Console.WriteLine($"  sweep {s.Sweep}: {s.Coefficient} {s.From} -> {s.To} (loss {s.Loss})");
        Console.WriteLine($"fitted: {fitted}");
        Console.WriteLine($"shipped defaults equal the fitted values: {(fitted == new AutoCoefficients() ? "YES" : "NO")}");
    }

    // ------------------------------------------------------------------ acceptance

    static void Accept(string[] args)
    {
        var (acc, tacAcc) = Load(args[2]);
        var (ctl, tacCtl) = Load(args[3]);
        var (tun, tacTun) = Load(args[4]);
        string modelName = args.Length > 5 ? args[5] : "new";
        string? mdPath = args.Length > 6 ? args[6] : null, csvPath = args.Length > 7 ? args[7] : null;
        var model = Model(modelName);
        var md = new StringBuilder();
        void W(string s) { Console.WriteLine(s); md.AppendLine(s); }
        foreach (var c in acc) if (c.Seed < ParityBatteryP.FirstAcceptanceSeed || c.Seed > ParityBatteryP.LastAcceptanceSeed) throw new InvalidOperationException("acceptance CSV holds a non-acceptance seed");
        var sw = Stopwatch.StartNew();
        var auto = RunAutoAll(acc, model);
        sw.Stop();
        W($"parityp accept model={modelName}: {acc.Length} acceptance cases ({ScenariosIn(acc).Length} scenarios x {acc.Length / ScenariosIn(acc).Length} seeds {ParityBatteryP.FirstAcceptanceSeed}-{ParityBatteryP.LastAcceptanceSeed}); auto {F(sw.Elapsed.TotalMilliseconds)} ms single thread");
        W($"coefficients: {model}");
        W($"tactical acceptance digest {Digest(acc, tacAcc)}; auto acceptance digest {Digest(acc, auto)}");
        W($"BATTERY HASH (tactical + auto, case order): {BatteryHash(acc, tacAcc, auto)}");
        W("");
        foreach (bool held in new[] { true, false })
        {
            var sel = acc.Where(c => ParityBatteryP.Get(c.Scenario).HeldOut == held).ToArray();
            var idx = Enumerable.Range(0, acc.Length).Where(i => ParityBatteryP.Get(acc[i].Scenario).HeldOut == held).ToArray();
            W($"### {(held ? "HELD-OUT scenarios" : "TUNING scenarios")} on the acceptance seeds (Tactical = T, Auto = Au)");
            W("");
            Table(sel, idx.Select(i => tacAcc[i]).ToArray(), idx.Select(i => auto[i]).ToArray(), W);
            W("");
        }
        W("### Attainability control: Tactical (acceptance seeds) vs Tactical (control sample: control seeds for held-out, tuning seeds for tuning scenarios)");
        W("");
        int selfPass = 0, selfN = 0;
        foreach (var s in ScenariosIn(acc))
        {
            bool held = ParityBatteryP.Get(s).HeldOut;
            var r = StatsOf(s, acc, tacAcc);
            var c = held ? StatsOf(s, ctl, tacCtl) : StatsOf(s, tun, tacTun);
            var ck = ParityCriteriaP1.Check(r, c, ParityBatteryP.RoleStart(s, 0), ParityBatteryP.RoleStart(s, 1), 2_000);
            selfN++; if (ck.Pass) selfPass++;
            W($"  {ParityBatteryP.Get(s).Name,-26} n={r.N}/{c.N}: {(ck.Pass ? "PASS" : "FAIL(" + ck.FailedList() + ")")} (win gaps {Pm(ck.WinGapA)}/{Pm(ck.WinGapB)}/{Pm(ck.WinGapDraw)} band {Pm(ck.WinBand)}; casA {Pm(ck.CasAGap)} / {Pm(ck.CasABand)}; casB {Pm(ck.CasBGap)} / {Pm(ck.CasBBand)}; dur {F(ck.DurGap / 1000.0)} / {F(ck.DurBand / 1000.0)} s; pulses {ck.PulseMedianGap})");
        }
        W($"attainability control passes {selfPass}/{selfN}");
        if (mdPath is not null) File.WriteAllText(mdPath, md.ToString());
        if (csvPath is not null)
        {
            var sb = new StringBuilder("scenario,name,family,heldOut,seed,mirrored,tac_winner,tac_casA,tac_casB,tac_pulses,tac_durationMs,tac_reason,auto_winner,auto_casA,auto_casB,auto_pulses,auto_durationMs,auto_reason\n");
            for (int i = 0; i < acc.Length; i++)
            {
                var d = ParityBatteryP.Get(acc[i].Scenario);
                sb.Append(CultureInfo.InvariantCulture, $"{(int)d.Id},{d.Name},{d.Family},{(d.HeldOut ? 1 : 0)},{acc[i].Seed},{(ParityBatteryP.Mirrored(acc[i].Seed) ? 1 : 0)},{tacAcc[i].Winner},{tacAcc[i].CasA},{tacAcc[i].CasB},{tacAcc[i].Pulses},{tacAcc[i].DurationMs},{tacAcc[i].Reason},{auto[i].Winner},{auto[i].CasA},{auto[i].CasB},{auto[i].Pulses},{auto[i].DurationMs},{auto[i].Reason}\n");
            }
            File.WriteAllText(csvPath, sb.ToString());
        }
    }

    /// <summary>[P fix r1, P-V6] parityp selfcheck &lt;reference.csv&gt; &lt;control.csv&gt;: rule P1 of the tactical reference sample
    /// against an independent tactical control sample, per scenario present in both (the attainability check), printing every
    /// band including the win band. Reporting only; the pre-committed acceptance rule is unchanged.</summary>
    static void SelfCheck(string[] args)
    {
        var (rc, ro) = Load(args[2]);
        var (cc, co) = Load(args[3]);
        int pass = 0, n = 0;
        foreach (var s in ScenariosIn(rc))
        {
            bool inControl = false;
            foreach (var c in cc) if (c.Scenario == s) { inControl = true; break; }
            if (!inControl) continue;
            var r = StatsOf(s, rc, ro); var c2 = StatsOf(s, cc, co);
            var ck = ParityCriteriaP1.Check(r, c2, ParityBatteryP.RoleStart(s, 0), ParityBatteryP.RoleStart(s, 1), 2_000);
            n++; if (ck.Pass) pass++;
            Console.WriteLine($"  {ParityBatteryP.Get(s).Name,-26} n={r.N}/{c2.N}: {(ck.Pass ? "PASS" : "FAIL(" + ck.FailedList() + ")")} (win gaps {Pm(ck.WinGapA)}/{Pm(ck.WinGapB)}/{Pm(ck.WinGapDraw)} band {Pm(ck.WinBand)}; casA {Pm(ck.CasAGap)} / {Pm(ck.CasABand)}; casB {Pm(ck.CasBGap)} / {Pm(ck.CasBBand)}; dur {F(ck.DurGap / 1000.0)} / {F(ck.DurBand / 1000.0)} s; pulses {ck.PulseMedianGap})");
        }
        Console.WriteLine($"self-check passes {pass}/{n}; reference digest {Digest(rc, ro)}; control digest {Digest(cc, co)}");
    }

    public static string BatteryHash(PCase[] cases, POutcome[] tac, POutcome[] auto)
    {
        var sb = new StringBuilder();
        for (int i = 0; i < cases.Length; i++)
            sb.Append(CultureInfo.InvariantCulture,
                $"{(int)cases[i].Scenario},{cases[i].Seed},{tac[i].Winner},{tac[i].CasA},{tac[i].CasB},{tac[i].Pulses},{tac[i].DurationMs},{(int)tac[i].Reason},{auto[i].Winner},{auto[i].CasA},{auto[i].CasB},{auto[i].Pulses},{auto[i].DurationMs},{(int)auto[i].Reason};");
        return Convert.ToHexString(SHA256.HashData(Encoding.ASCII.GetBytes(sb.ToString())));
    }
}
