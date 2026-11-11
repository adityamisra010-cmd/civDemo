using System.Diagnostics;
using System.Globalization;
using System.Text;
using Proto.M7.Tactical;

// [MOR2] Lane MOR2: D-083 DB1's in-battle entry charge (TacticalRules.EntryMoraleCost, default 0) measured on lane P's TUNING
// battery (20 scenarios x seeds 1-80), off and on, in BOTH resolvers, with rule P1 (lane P's pre-committed parity check) for each
// setting. "Off" is (EntryMoraleCost 0, LocalWinMoraleRise 0), the shipped defaults; "on" is (cost, rise) as given, so the entry
// charge can be measured alone (rise 0) or together with lane MOR's "fight it won" (rise 200, the joint switch-on the morale proposal
// §11.6 proposes). REPORTING ONLY: Stopwatch / double / Parallel give wall time and percentages; every case is an independent pure
// function of (scenario, seed, rules), stored by index, so the schedule cannot change a number. The held-out ACCEPTANCE battery is not
// run (lane P's record; D-079 Q4 is OPEN). Usage:
//   entry-parity [cost=50] [rise=0] [threads=2] [out.md]
static class EntryBench
{
    static string F(double v) => v.ToString("F1", CultureInfo.InvariantCulture);
    static string Pm(long permille) => (permille / 10.0).ToString("F1", CultureInfo.InvariantCulture);

    static BattleSetup With(PCase c, long cost, long rise)
    {
        var s = ParityBatteryP.Build(c.Scenario, c.Seed);
        return s with { Rules = s.Rules with { EntryMoraleCost = cost, LocalWinMoraleRise = rise } };
    }

    static POutcome[] Tactical(PCase[] cases, long cost, long rise, int threads, out long entries)
    {
        var o = new POutcome[cases.Length];
        var n = new long[cases.Length];
        Parallel.For(0, cases.Length, new ParallelOptions { MaxDegreeOfParallelism = threads }, i =>
        {
            var b = new TacticalBattle(With(cases[i], cost, rise), new KnowledgeAi(), new KnowledgeAi());
            var r = b.Run();
            foreach (var fr in r.Formations) if (!fr.Conserved) throw new InvalidOperationException("conservation");
            o[i] = POutcome.Of(r, 0);
            n[i] = b.EntryCharges.Count;
        });
        entries = n.Sum();
        return o;
    }

    static POutcome[] Auto(PCase[] cases, long cost, long rise, AutoCoefficients c, out long entries)
    {
        var o = new POutcome[cases.Length];
        entries = 0;
        for (int i = 0; i < cases.Length; i++)
        {
            var b = new AutoBattle(With(cases[i], cost, rise), c);
            o[i] = POutcome.Of(b.Run(), 0);
            entries += b.EntryCharges.Count;
        }
        return o;
    }

    static PStats StatsOf(PScenario s, PCase[] cases, POutcome[] o)
    {
        var l = new List<POutcome>();
        for (int i = 0; i < cases.Length; i++) if (cases[i].Scenario == s) l.Add(o[i]);
        return PStats.Of(l);
    }

    public static void Run(string[] args)
    {
        long cost = args.Length > 1 ? long.Parse(args[1], CultureInfo.InvariantCulture) : 50;
        long rise = args.Length > 2 ? long.Parse(args[2], CultureInfo.InvariantCulture) : 0;
        int threads = args.Length > 3 ? int.Parse(args[3], CultureInfo.InvariantCulture) : 2;
        string? md = args.Length > 4 ? args[4] : null;
        var sb = new StringBuilder();
        void W(string s) { Console.WriteLine(s); sb.AppendLine(s); }
        var cases = ParityBatteryP.TuningCases();
        var model = new AutoCoefficients();
        var sw = Stopwatch.StartNew();
        var tOff = Tactical(cases, 0, 0, threads, out long etOff);
        var tOn = Tactical(cases, cost, rise, threads, out long etOn);
        double tSec = sw.Elapsed.TotalSeconds;
        sw.Restart();
        var aOff = Auto(cases, 0, 0, model, out long eaOff);
        var aOn = Auto(cases, cost, rise, model, out long eaOn);
        double aSec = sw.Elapsed.TotalSeconds;
        W($"entry-parity: lane P TUNING battery, {cases.Length} cases x (off = defaults, on = EntryMoraleCost {cost}, LocalWinMoraleRise {rise}) x (tactical, auto model 2 defaults)");
        W($"wall: tactical {F(tSec)} s on {threads} threads, auto {F(aSec)} s single thread");
        W($"digests (lane P's ParityPCsv.Digest): tactical off {ParityPCsv.Digest(cases, tOff)}, on {ParityPCsv.Digest(cases, tOn)}");
        W($"                                      auto     off {ParityPCsv.Digest(cases, aOff)}, on {ParityPCsv.Digest(cases, aOn)}");
        W($"entry rows over the battery: tactical off {etOff} / on {etOn}; auto off {eaOff} / on {eaOn}");
        int tChanged = 0, aChanged = 0, tWin = 0, aWin = 0;
        for (int i = 0; i < cases.Length; i++)
        {
            if (tOff[i] != tOn[i]) tChanged++;
            if (aOff[i] != aOn[i]) aChanged++;
            if (tOff[i].Winner != tOn[i].Winner) tWin++;
            if (aOff[i].Winner != aOn[i].Winner) aWin++;
        }
        W($"cases whose outcome changed (any field): tactical {tChanged}, auto {aChanged}; whose winner changed: tactical {tWin}, auto {aWin}");
        W("");
        W("| scenario | T off win A/B/- % | T on | Au off | Au on | casA T off/on | casB T off/on | casA Au off/on | casB Au off/on | dur s T off/on | dur s Au off/on | P1 off | P1 on |");
        W("|---|---|---|---|---|---|---|---|---|---|---|---|---|");
        int passOff = 0, passOn = 0, n = 0;
        foreach (var s in ParityBatteryP.TuningScenarios())
        {
            var d = ParityBatteryP.Get(s);
            var to = StatsOf(s, cases, tOff); var tn = StatsOf(s, cases, tOn);
            var ao = StatsOf(s, cases, aOff); var an = StatsOf(s, cases, aOn);
            long sa = ParityBatteryP.RoleStart(s, 0), sbb = ParityBatteryP.RoleStart(s, 1);
            var ckOff = ParityCriteriaP1.Check(to, ao, sa, sbb, 2_000);
            var ckOn = ParityCriteriaP1.Check(tn, an, sa, sbb, 2_000);
            n++; if (ckOff.Pass) passOff++; if (ckOn.Pass) passOn++;
            string Win(PStats x) => $"{Pm(1000L * x.WinA / x.N)}/{Pm(1000L * x.WinB / x.N)}/{Pm(1000L * x.Draw / x.N)}";
            string M(long sum, int k) => Pm(PStats.MeanFloor(sum, k));
            W($"| {d.Name} | {Win(to)} | {Win(tn)} | {Win(ao)} | {Win(an)} | {M(to.CasASum, to.N)}/{M(tn.CasASum, tn.N)} | {M(to.CasBSum, to.N)}/{M(tn.CasBSum, tn.N)} | "
              + $"{M(ao.CasASum, ao.N)}/{M(an.CasASum, an.N)} | {M(ao.CasBSum, ao.N)}/{M(an.CasBSum, an.N)} | {F(to.DurSum / 1000.0 / to.N)}/{F(tn.DurSum / 1000.0 / tn.N)} | "
              + $"{F(ao.DurSum / 1000.0 / ao.N)}/{F(an.DurSum / 1000.0 / an.N)} | {(ckOff.Pass ? "PASS" : "FAIL(" + ckOff.FailedList() + ")")} | {(ckOn.Pass ? "PASS" : "FAIL(" + ckOn.FailedList() + ")")} |");
        }
        W($"rule P1 (tactical reference vs auto candidate, tuning seeds n = 80): off {passOff}/{n}, on {passOn}/{n}");
        if (md is not null) File.WriteAllText(md, sb.ToString());
    }
}
