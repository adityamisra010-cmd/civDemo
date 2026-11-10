using System.Diagnostics;
using System.Globalization;
using System.Text;
using Proto.M7.Tactical;

// [AP] Lane AP: what D-082 DA6 / DA7 (the equal-sides rule, EqualSides) changes on lane P's parity battery. REPORTING ONLY:
// Stopwatch / double / Parallel give wall time and percentages; every case is an independent pure function of (scenario, seed,
// rules), stored by index, so the schedule cannot change a number. Rule P1 (ParityCriteriaP1) is used exactly as committed; only
// the WINNER the outcomes carry differs between the bases (POutcomeWinner). Lane P's acceptance stays OPEN (D-079 Q4): nothing
// here accepts or tunes anything. Usage:
//   equalsides derive <acceptance.csv> <control.csv> <tuning.csv> [out.md]
//       tactical outcomes from lane P's cached CSVs (the HoldsField basis), the other bases derived per case through
//       EqualSides.Decide (the resolvers' own function); the AutoResolver run directly on every basis.
//   equalsides verify <acceptance.csv> [threads=2]
//       re-runs the tactical acceptance battles on this tree: the HoldsField digest must equal the CSV's, and the Reported digest
//       read from the resolver's own result must equal the derived one.
//   equalsides mor <rise> <tuning|acceptance> <csv of that set> [threads=2] [out.md]
//       for information (H-MOR §6): lane MOR's in-battle rule at the given coefficient together with DA6 / DA7, both resolvers.
static class EqualSidesBench
{
    static string F(double v) => v.ToString("F1", CultureInfo.InvariantCulture);
    static string Pm(long permille) => (permille / 10.0).ToString("F1", CultureInfo.InvariantCulture);

    public static void Run(string[] args)
    {
        switch (args.Length > 1 ? args[1] : "")
        {
            case "derive": Derive(args); break;
            case "verify": Verify(args); break;
            case "mor": Mor(args); break;
            default: throw new ArgumentException("mode: derive | verify | mor");
        }
    }

    /// <summary>The tactical winner on a basis, derived from a cached outcome (HoldsField basis) through the resolvers' own function.
    /// For the TacticalResolver HoldsField = ReportedDecisive on every land battle (checked: a decided end always leaves the other
    /// side Active), so the equal-sides winner is EqualSides.Decide on (reason, the scenario's roles).</summary>
    static POutcome DeriveTactical(PCase c, POutcome held, POutcomeWinner basis, bool standoff)
    {
        bool decided = held.Reason is BattleEndReason.SideEliminated or BattleEndReason.SideDisengaged;
        if (decided && held.Winner < 0) throw new InvalidOperationException($"tactical {c}: a decided end without a field holder");
        if (!decided && held.Winner >= 0) throw new InvalidOperationException($"tactical {c}: an undecided end with a field holder");
        int w = basis switch
        {
            POutcomeWinner.HoldsField or POutcomeWinner.ReportedDecisive => held.Winner,
            POutcomeWinner.Reported => decided ? held.Winner
                : EqualSides.Decide(held.Reason, -1, ParityBatteryP.DefenderOf(c.Scenario), -1, standoff).Winner,
            _ => throw new ArgumentOutOfRangeException(nameof(basis)),
        };
        return held with { Winner = w };
    }

    static POutcome[] AutoAll(PCase[] cases, AutoCoefficients model, POutcomeWinner basis, bool standoff, long rise = 0)
    {
        var o = new POutcome[cases.Length];
        for (int i = 0; i < cases.Length; i++)
        {
            var s = ParityBatteryP.Build(cases[i].Scenario, cases[i].Seed);
            if (standoff || rise != 0) s = s with { Rules = s.Rules with { EqualSidesAtStandoff = standoff, LocalWinMoraleRise = rise } };
            o[i] = POutcome.Of(new AutoBattle(s, model).Run(), 0, basis);
        }
        return o;
    }

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

    sealed record Basis(string Name, POutcome[] Tac, POutcome[] Auto, POutcome[] Ctl, POutcome[] Tun);

    static void Derive(string[] args)
    {
        var (acc, tacAcc) = ParityPBench.Load(args[2]);
        var (ctl, tacCtl) = ParityPBench.Load(args[3]);
        var (tun, tacTun) = ParityPBench.Load(args[4]);
        string? md = args.Length > 5 ? args[5] : null;
        var sb = new StringBuilder();
        void W(string s) { Console.WriteLine(s); sb.AppendLine(s); }
        var model = new AutoCoefficients();
        POutcome[] T(PCase[] cs, POutcome[] held, POutcomeWinner b, bool st) { var r = new POutcome[cs.Length]; for (int i = 0; i < cs.Length; i++) r[i] = DeriveTactical(cs[i], held[i], b, st); return r; }
        var sw = Stopwatch.StartNew();
        var aHeld = AutoAll(acc, model, POutcomeWinner.HoldsField, false);
        var aDec = AutoAll(acc, model, POutcomeWinner.ReportedDecisive, false);
        var aRep = AutoAll(acc, model, POutcomeWinner.Reported, false);
        var aRepSt = AutoAll(acc, model, POutcomeWinner.Reported, true);
        sw.Stop();
        var bases = new[]
        {
            new Basis("B0 as committed: HoldsField (T) vs HoldsField (Au)", tacAcc, aHeld, tacCtl, tacTun),
            new Basis("B1 reported, decided ends only (lane MOR before DA6/DA7), both", T(acc, tacAcc, POutcomeWinner.ReportedDecisive, false), aDec,
                      T(ctl, tacCtl, POutcomeWinner.ReportedDecisive, false), T(tun, tacTun, POutcomeWinner.ReportedDecisive, false)),
            new Basis("B2 reported with DA6/DA7 in BOTH resolvers (READING AP-2 (a), applied)", T(acc, tacAcc, POutcomeWinner.Reported, false), aRep,
                      T(ctl, tacCtl, POutcomeWinner.Reported, false), T(tun, tacTun, POutcomeWinner.Reported, false)),
            new Basis("B3 DA6/DA7 in the AutoResolver ONLY (READING AP-2 (b)): T decided-only vs Au reported", T(acc, tacAcc, POutcomeWinner.ReportedDecisive, false), aRep,
                      T(ctl, tacCtl, POutcomeWinner.ReportedDecisive, false), T(tun, tacTun, POutcomeWinner.ReportedDecisive, false)),
            new Basis("B4 B2 + the Standoff extension ON in both (DA6-R PROPOSAL)", T(acc, tacAcc, POutcomeWinner.Reported, true), aRepSt,
                      T(ctl, tacCtl, POutcomeWinner.Reported, true), T(tun, tacTun, POutcomeWinner.Reported, true)),
        };
        W($"equalsides derive: {acc.Length} acceptance cases, control {ctl.Length}, tuning {tun.Length}; auto model 2 defaults, 4 bases x {acc.Length} battles in {F(sw.Elapsed.TotalSeconds)} s");
        W($"tactical acceptance digest (cached CSV, HoldsField): {ParityPCsv.Digest(acc, tacAcc)}");
        W($"auto acceptance digest (this tree, HoldsField): {ParityPCsv.Digest(acc, aHeld)}");
        W($"BATTERY HASH (HoldsField basis, this tree): {ParityPBench.BatteryHash(acc, tacAcc, aHeld)}");
        // what the rule touches: per resolver, the cases whose winner differs between the bases
        int Diff(POutcome[] x, POutcome[] y) { int n = 0; for (int i = 0; i < x.Length; i++) if (x[i].Winner != y[i].Winner) n++; return n; }
        int NonWinner(POutcome[] x, POutcome[] y) { int n = 0; for (int i = 0; i < x.Length; i++) if (x[i] with { Winner = 0 } != y[i] with { Winner = 0 }) n++; return n; }
        W($"cases whose winner differs (acceptance): T B0->B1 {Diff(bases[0].Tac, bases[1].Tac)}, B1->B2 {Diff(bases[1].Tac, bases[2].Tac)}, B2->B4 {Diff(bases[2].Tac, bases[4].Tac)}; "
          + $"Au B0->B1 {Diff(aHeld, aDec)}, B1->B2 {Diff(aDec, aRep)}, B2->B4 {Diff(aRep, aRepSt)}");
        W($"cases whose non-winner fields differ between any two bases: T {NonWinner(bases[0].Tac, bases[4].Tac)}, Au {NonWinner(aHeld, aRepSt)} (must be 0: the rule changes the reported winner only)");
        var reasons = new SortedDictionary<string, int>(StringComparer.Ordinal);
        for (int i = 0; i < acc.Length; i++)
        {
            string k = $"{ParityBatteryP.Get(acc[i].Scenario).Name} T {tacAcc[i].Reason} / Au {aHeld[i].Reason}";
            if (tacAcc[i].Winner < 0 || aHeld[i].Winner < 0 || aRep[i].Winner != aHeld[i].Winner) reasons[k] = reasons.TryGetValue(k, out int v) ? v + 1 : 1;
        }
        W("");
        W("undecided or re-decided acceptance cases by (scenario, tactical reason / auto reason): count");
        foreach (var kv in reasons) W($"  {kv.Key}: {kv.Value}");
        W("");
        foreach (var b in bases) Table(b, acc, ctl, tun, W);
        W("### Verdict flips against B0 (scenario: check list B0 -> basis)");
        foreach (var b in bases.Skip(1))
        {
            W($"- {b.Name}:");
            foreach (var s in ScenariosIn(acc))
            {
                var c0 = Check(s, acc, bases[0].Tac, bases[0].Auto);
                var c1 = Check(s, acc, b.Tac, b.Auto);
                if (c0.FailedList() != c1.FailedList())
                    W($"  {ParityBatteryP.Get(s).Name}: {(c0.Pass ? "PASS" : c0.FailedList())} -> {(c1.Pass ? "PASS" : c1.FailedList())}");
            }
        }
        if (md is not null) File.WriteAllText(md, sb.ToString());
    }

    static PCheck Check(PScenario s, PCase[] cases, POutcome[] tac, POutcome[] auto)
        => ParityCriteriaP1.Check(StatsOf(s, cases, tac), StatsOf(s, cases, auto), ParityBatteryP.RoleStart(s, 0), ParityBatteryP.RoleStart(s, 1), 2_000);

    static void Table(Basis b, PCase[] acc, PCase[] ctl, PCase[] tun, Action<string> W)
    {
        W($"### {b.Name}");
        W("");
        W("| scenario | held-out | defender | win A/B/- % T | win A/B/- % Au (band) | verdict | self-check T vs control |");
        W("|---|---|---:|---|---|---|---|");
        int pass = 0, passH = 0, passT = 0, nH = 0, nT = 0, self = 0;
        foreach (var s in ScenariosIn(acc))
        {
            var d = ParityBatteryP.Get(s);
            var t = StatsOf(s, acc, b.Tac); var a = StatsOf(s, acc, b.Auto);
            var ck = ParityCriteriaP1.Check(t, a, ParityBatteryP.RoleStart(s, 0), ParityBatteryP.RoleStart(s, 1), 2_000);
            var ctrl = d.HeldOut ? StatsOf(s, ctl, b.Ctl) : StatsOf(s, tun, b.Tun);
            var sc = ParityCriteriaP1.Check(t, ctrl, ParityBatteryP.RoleStart(s, 0), ParityBatteryP.RoleStart(s, 1), 2_000);
            if (ck.Pass) { pass++; if (d.HeldOut) passH++; else passT++; }
            if (d.HeldOut) nH++; else nT++;
            if (sc.Pass) self++;
            string Win(PStats x) => $"{Pm(1000L * x.WinA / x.N)}/{Pm(1000L * x.WinB / x.N)}/{Pm(1000L * x.Draw / x.N)}";
            W($"| {d.Name} | {(d.HeldOut ? "yes" : "no")} | {ParityBatteryP.DefenderOf(s)} | {Win(t)} | {Win(a)} ({Pm(ck.WinBand)}) | {(ck.Pass ? "PASS" : "FAIL(" + ck.FailedList() + ")")} | {(sc.Pass ? "PASS" : "FAIL(" + sc.FailedList() + ")")} |");
        }
        W($"scenarios passing: {pass}/{nH + nT} (held-out {passH}/{nH}, tuning on fresh seeds {passT}/{nT}); attainability self-check {self}/{nH + nT}");
        W("");
    }

    static void Verify(string[] args)
    {
        var (acc, tacAcc) = ParityPBench.Load(args[2]);
        int threads = args.Length > 3 ? int.Parse(args[3], CultureInfo.InvariantCulture) : 2;
        var held = new POutcome[acc.Length]; var rep = new POutcome[acc.Length]; var basis = new WinBasis[acc.Length];
        var sw = Stopwatch.StartNew();
        Parallel.For(0, acc.Length, new ParallelOptions { MaxDegreeOfParallelism = threads }, i =>
        {
            var r = ParityBatteryP.RunTacticalResult(acc[i]);
            foreach (var fr in r.Formations) if (!fr.Conserved) throw new InvalidOperationException("conservation");
            held[i] = POutcome.Of(r, 0, POutcomeWinner.HoldsField);
            rep[i] = POutcome.Of(r, 0, POutcomeWinner.Reported);
            basis[i] = r.WinBasis;
        });
        sw.Stop();
        var derived = new POutcome[acc.Length];
        for (int i = 0; i < acc.Length; i++) derived[i] = DeriveTactical(acc[i], tacAcc[i], POutcomeWinner.Reported, false);
        int[] byBasis = new int[4];
        foreach (var x in basis) byBasis[(int)x]++;
        Console.WriteLine($"equalsides verify: {acc.Length} tactical acceptance battles re-run on this tree, wall {F(sw.Elapsed.TotalSeconds)} s, threads {threads}");
        Console.WriteLine($"HoldsField digest: this tree {ParityPCsv.Digest(acc, held)}, cached CSV {ParityPCsv.Digest(acc, tacAcc)} -> {(ParityPCsv.Digest(acc, held) == ParityPCsv.Digest(acc, tacAcc) ? "EQUAL" : "DIFFERENT")}");
        Console.WriteLine($"Reported digest: resolver {ParityPCsv.Digest(acc, rep)}, derived {ParityPCsv.Digest(acc, derived)} -> {(ParityPCsv.Digest(acc, rep) == ParityPCsv.Digest(acc, derived) ? "EQUAL" : "DIFFERENT")}");
        Console.WriteLine($"win basis counts: None {byBasis[0]}, Decisive {byBasis[1]}, DefenderOnEqualSides {byBasis[2]}, AmbusherOnEqualSides {byBasis[3]}");
    }

    static void Mor(string[] args)
    {
        long rise = long.Parse(args[2], CultureInfo.InvariantCulture);
        string set = args[3];
        var (cases, tacOff) = ParityPBench.Load(args[4]);
        int threads = args.Length > 5 ? int.Parse(args[5], CultureInfo.InvariantCulture) : 2;
        string? md = args.Length > 6 ? args[6] : null;
        var sb = new StringBuilder();
        void W(string s) { Console.WriteLine(s); sb.AppendLine(s); }
        var model = new AutoCoefficients();
        var tHeld = new POutcome[cases.Length]; var tRep = new POutcome[cases.Length];
        var sw = Stopwatch.StartNew();
        Parallel.For(0, cases.Length, new ParallelOptions { MaxDegreeOfParallelism = threads }, i =>
        {
            var s = ParityBatteryP.Build(cases[i].Scenario, cases[i].Seed);
            s = s with { Rules = s.Rules with { LocalWinMoraleRise = rise } };
            var r = new TacticalBattle(s, new KnowledgeAi(), new KnowledgeAi()).Run();
            foreach (var fr in r.Formations) if (!fr.Conserved) throw new InvalidOperationException("conservation");
            tHeld[i] = POutcome.Of(r, 0, POutcomeWinner.HoldsField);
            tRep[i] = POutcome.Of(r, 0, POutcomeWinner.Reported);
        });
        double tSec = sw.Elapsed.TotalSeconds;
        var aHeld0 = AutoAll(cases, model, POutcomeWinner.HoldsField, false);
        var aHeld = AutoAll(cases, model, POutcomeWinner.HoldsField, false, rise);
        var aRep = AutoAll(cases, model, POutcomeWinner.Reported, false, rise);
        var tRep0 = new POutcome[cases.Length];
        for (int i = 0; i < cases.Length; i++) tRep0[i] = DeriveTactical(cases[i], tacOff[i], POutcomeWinner.Reported, false);
        var aRep0 = AutoAll(cases, model, POutcomeWinner.Reported, false);
        W($"equalsides mor: lane MOR's LocalWinMoraleRise = {rise} on lane P's {set} set ({cases.Length} cases), both resolvers; tactical {F(tSec)} s on {threads} threads");
        W($"digests HoldsField: tactical off (cached) {ParityPCsv.Digest(cases, tacOff)}, on {ParityPCsv.Digest(cases, tHeld)}; auto off {ParityPCsv.Digest(cases, aHeld0)}, on {ParityPCsv.Digest(cases, aHeld)}");
        W("");
        W("| scenario | P1 HoldsField, rise 0 | P1 HoldsField, rise on | P1 Reported (DA6/DA7), rise 0 | P1 Reported (DA6/DA7), rise on |");
        W("|---|---|---|---|---|");
        int[] pass = new int[4]; int n = 0;
        foreach (var s in ScenariosIn(cases))
        {
            var cks = new[] { Check(s, cases, tacOff, aHeld0), Check(s, cases, tHeld, aHeld), Check(s, cases, tRep0, aRep0), Check(s, cases, tRep, aRep) };
            n++;
            for (int k = 0; k < 4; k++) if (cks[k].Pass) pass[k]++;
            string V(PCheck c) => c.Pass ? "PASS" : "FAIL(" + c.FailedList() + ")";
            W($"| {ParityBatteryP.Get(s).Name} | {V(cks[0])} | {V(cks[1])} | {V(cks[2])} | {V(cks[3])} |");
        }
        W($"scenarios passing: HoldsField rise 0 {pass[0]}/{n}, rise on {pass[1]}/{n}; Reported rise 0 {pass[2]}/{n}, rise on {pass[3]}/{n}");
        if (md is not null) File.WriteAllText(md, sb.ToString());
    }
}
