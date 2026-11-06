using System.Globalization;

namespace Proto.M7.Econ;

/// <summary>
/// M7 Phase C lane F command lines (dispatched from <see cref="Program.Main"/> when the first argument names one):
/// <list type="bullet">
/// <item><c>food-baseline --seeds 1,2,42,7,3,5 --max-turn 325 --twins 25,200 --otwin-horizon 20 --out-dir DIR</c> — steps the
/// REAL worlds and writes <c>DIR/baseline-seed{N}.txt</c> (<see cref="FoodBaseline.ToText"/>).</item>
/// <item><c>food-compare --in-dir DIR --out FILE.md [--csv FILE.csv] [--trade-csv FILE.csv] [--plan default|validation]</c> — runs the comparison battery on the saved series
/// and writes the generated tables (<see cref="FoodComparison"/>).</item>
/// <item><c>food-trace --in-dir DIR --seed N --army A --regime R --order CGM|CMG [--overflow RULE] …</c> — one scenario, one line per period.</item>
/// <item><c>food-bench --in-dir DIR [--seed 42] [--reps 7]</c> — a small wall-time bench (allocator call; one 300-turn run per order and rule).</item>
/// <item><c>food-pulse --in-dir DIR [--out FILE.md]</c> — verification round 2 (F2-1, F2-2): the settlement-period and march-leg
/// sensitivity tables (<see cref="FoodPulseSensitivity"/>).</item>
/// </list>
/// </summary>
public static class FoodComparisonCli
{
    private static readonly CultureInfo Inv = CultureInfo.InvariantCulture;

    public static bool TryRun(string[] args, out int exitCode)
    {
        exitCode = 0;
        if (args.Length == 0) return false;
        switch (args[0])
        {
            case "food-baseline":
                exitCode = Baseline(args);
                return true;
            case "food-compare":
                exitCode = Compare(args);
                return true;
            case "food-trace":
                exitCode = Trace(args);
                return true;
            case "food-bench":
                exitCode = Bench(args);
                return true;
            case "food-pulse":
                exitCode = Pulse(args);
                return true;
            default:
                return false;
        }
    }

    private static int Baseline(string[] args)
    {
        ulong[] seeds = List(Arg(args, "--seeds") ?? "1,2,42,7,3,5", s => ulong.Parse(s, Inv));
        long maxTurn = long.Parse(Arg(args, "--max-turn") ?? "325", Inv);
        long[] twins = List(Arg(args, "--twins") ?? "25,200", s => long.Parse(s, Inv));
        long horizon = long.Parse(Arg(args, "--otwin-horizon") ?? "20", Inv);
        string dir = Arg(args, "--out-dir") ?? ".";
        Directory.CreateDirectory(dir);
        for (int i = 0; i < seeds.Length; i++)
        {
            BaselineSeries s = FoodBaseline.Measure(seeds[i], maxTurn, twins, horizon);
            string path = Path.Combine(dir, "baseline-seed" + seeds[i].ToString(Inv) + ".txt");
            File.WriteAllText(path, FoodBaseline.ToText(s));
            Console.Out.Write("seed " + seeds[i].ToString(Inv) + ": " + s.Turns.Length.ToString(Inv) + " rows, " + s.ElapsedMs.ToString(Inv) + " ms -> " + path + "\n");
        }
        return 0;
    }

    private static int Compare(string[] args)
    {
        string dir = Arg(args, "--in-dir") ?? ".";
        string outPath = Arg(args, "--out") ?? "food-comparison.md";
        string? csvPath = Arg(args, "--csv");
        string? tradeCsvPath = Arg(args, "--trade-csv");
        string[] files = Directory.GetFiles(dir, "baseline-seed*.txt");
        Array.Sort(files, StringComparer.Ordinal);
        var series = new List<BaselineSeries>();
        for (int i = 0; i < files.Length; i++) series.Add(FoodBaseline.FromText(File.ReadAllText(files[i])));
        series.Sort((a, b) => a.Seed.CompareTo(b.Seed));
        long t0 = Environment.TickCount64;
        FoodPlan plan = (Arg(args, "--plan") ?? "default") switch
        {
            "default" => FoodComparison.DefaultPlan(),
            "validation" => FoodComparison.ValidationPlan(),
            string other => throw new ArgumentException("unknown --plan " + other),
        };
        FoodBattery battery = FoodComparison.RunBattery([.. series], plan);
        long ms = Environment.TickCount64 - t0;
        string md = FoodComparison.Report(battery, ms);
        File.WriteAllText(outPath, md);
        if (csvPath is not null) File.WriteAllText(csvPath, FoodComparison.Csv(battery));
        if (tradeCsvPath is not null) File.WriteAllText(tradeCsvPath, FoodComparison.TradeCsv(battery));
        Console.Out.Write("battery: " + battery.Runs.Count.ToString(Inv) + " runs, " + ms.ToString(Inv) + " ms, csv sha256 "
            + FoodComparison.CsvHash(battery) + ", trade csv sha256 " + FoodComparison.TradeCsvHash(battery) + " -> " + outPath + "\n");
        return 0;
    }

    /// <summary>One scenario, one line per allocation period (inspection only).</summary>
    private static int Trace(string[] args)
    {
        string dir = Arg(args, "--in-dir") ?? ".";
        ulong seed = ulong.Parse(Arg(args, "--seed") ?? "42", Inv);
        BaselineSeries s = FoodBaseline.FromText(File.ReadAllText(Path.Combine(dir, "baseline-seed" + seed.ToString(Inv) + ".txt")));
        FoodModelParams p = FoodModelParams.Shipped;
        long t0 = long.Parse(Arg(args, "--t0") ?? "25", Inv);
        int horizon = int.Parse(Arg(args, "--horizon") ?? "1", Inv);
        string regimeName = Arg(args, "--regime") ?? "NORMAL";
        RegimeSpec? regime = null;
        foreach (RegimeSpec r in FoodComparison.DefaultPlan().Regimes) if (r.Name == regimeName) regime = r;
        AllocationOrder order = (Arg(args, "--order") ?? "CMG") == "CGM" ? AllocationOrder.CivilianGrowthMilitary : AllocationOrder.CivilianMilitaryGrowth;
        FoodScenario sc = FoodComparison.Scenario(s, p, new EpochPlan(t0, horizon), Arg(args, "--army") ?? "PEACE",
            regime ?? throw new ArgumentException("unknown regime " + regimeName), order,
            Enum.Parse<OverflowRule>(Arg(args, "--overflow") ?? "CapBeforeMilitary"), Enum.Parse<UnfedConsequence>(Arg(args, "--consequence") ?? "None"),
            Enum.Parse<HaulBasis>(Arg(args, "--haul") ?? "FullLoad"), Enum.Parse<SettlementGranularity>(Arg(args, "--granularity") ?? "WarPulse"))
            with { PulseDays = long.Parse(Arg(args, "--pulse-days") ?? "0", Inv), MarchLegDays = long.Parse(Arg(args, "--leg-days") ?? "0", Inv) };
        int max = int.Parse(Arg(args, "--periods") ?? "60", Inv), n = 0;
        FoodRunResult res = FoodPulseModel.Run(s, sc, p, line => { if (n++ < max) Console.Out.Write(line + "\n"); });
        Console.Out.Write("final: civilians " + res.FinalCivilians.ToString(Inv) + ", soldiers " + res.FinalSoldiers.ToString(Inv) + ", stock "
            + res.FinalGrainStock.ToString(Inv) + ", hash " + res.StateHash + "\n");
        return 0;
    }

    /// <summary>Verification round 2: the pulse-length sensitivity tables (settlement period and march leg varied apart).</summary>
    private static int Pulse(string[] args)
    {
        string dir = Arg(args, "--in-dir") ?? ".";
        string[] files = Directory.GetFiles(dir, "baseline-seed*.txt");
        Array.Sort(files, StringComparer.Ordinal);
        var series = new List<BaselineSeries>();
        for (int i = 0; i < files.Length; i++) series.Add(FoodBaseline.FromText(File.ReadAllText(files[i])));
        series.Sort((a, b) => a.Seed.CompareTo(b.Seed));
        long t0 = Environment.TickCount64;
        string md = FoodPulseSensitivity.Report([.. series], FoodModelParams.Shipped);
        long ms = Environment.TickCount64 - t0;
        string? outPath = Arg(args, "--out");
        if (outPath is not null) File.WriteAllText(outPath, md);
        else Console.Out.Write(md);
        Console.Out.Write("food-pulse: " + ms.ToString(Inv) + " ms, sha256 " + Convert.ToHexStringLower(System.Security.Cryptography.SHA256.HashData(System.Text.Encoding.UTF8.GetBytes(md))) + "\n");
        return 0;
    }

    /// <summary>A small bench (wall time; reporting only, never a logic input): the allocator's Resolve per call for both
    /// orders, and one whole model run (seed, T0 = 25, 300 turns, WAR-1000, BAD-1s) per order and overflow rule.
    /// Median of <c>--reps</c> timed repetitions after warm-up (one pass for Resolve, three for a model run).</summary>
    private static int Bench(string[] args)
    {
        string dir = Arg(args, "--in-dir") ?? ".";
        ulong seed = ulong.Parse(Arg(args, "--seed") ?? "42", Inv);
        int reps = int.Parse(Arg(args, "--reps") ?? "7", Inv);
        BaselineSeries s = FoodBaseline.FromText(File.ReadAllText(Path.Combine(dir, "baseline-seed" + seed.ToString(Inv) + ".txt")));
        FoodModelParams p = FoodModelParams.Shipped;
        AllocationOrder[] orders = [AllocationOrder.CivilianGrowthMilitary, AllocationOrder.CivilianMilitaryGrowth];
        MoveRequest[] moves = [new MoveRequest(1, 0, 58, 21), new MoveRequest(2, 1, 496, 21)];
        const int calls = 1_000_000;
        for (int qi = 0; qi < orders.Length; qi++)
        {
            var t = new double[reps];
            long sink = 0;
            for (int r = -1; r < reps; r++)
            {
                long t0 = System.Diagnostics.Stopwatch.GetTimestamp();
                for (int i = 0; i < calls; i++) sink += LogisticsPriority.Resolve(400 + (i & 255), 210, 115, moves, orders[qi]).MilitaryGranted;
                double ns = System.Diagnostics.Stopwatch.GetElapsedTime(t0).TotalMilliseconds * 1e6 / calls;
                if (r >= 0) t[r] = ns;
            }
            Array.Sort(t);
            Console.Out.Write("Resolve " + FoodComparison.OrderTag(orders[qi]) + ": median " + t[reps / 2].ToString("F1", Inv) + " ns/call (" + reps.ToString(Inv)
                + " x " + calls.ToString(Inv) + " calls; checksum " + sink.ToString(Inv) + ")\n");
        }
        RegimeSpec bad = FoodComparison.DefaultPlan().Regimes[1];
        OverflowRule[] rules = [OverflowRule.CapBeforeMilitary, OverflowRule.TradeCredit];
        for (int oi = 0; oi < rules.Length; oi++)
            for (int qi = 0; qi < orders.Length; qi++)
            {
                FoodScenario sc = FoodComparison.Scenario(s, p, new EpochPlan(25, 300), "WAR-1000", bad, orders[qi], rules[oi], UnfedConsequence.None, HaulBasis.FullLoad);
                var t = new double[reps];
                string hash = "";
                for (int r = -3; r < reps; r++)   // 3 warm-ups (tiered JIT)
                {
                    long t0 = System.Diagnostics.Stopwatch.GetTimestamp();
                    hash = FoodPulseModel.Run(s, sc, p).StateHash;
                    if (r >= 0) t[r] = System.Diagnostics.Stopwatch.GetElapsedTime(t0).TotalMilliseconds;
                }
                Array.Sort(t);
                Console.Out.Write("Run 300 turns " + rules[oi] + " " + FoodComparison.OrderTag(orders[qi]) + ": median " + t[reps / 2].ToString("F1", Inv)
                    + " ms (min " + t[0].ToString("F1", Inv) + ", max " + t[reps - 1].ToString("F1", Inv) + "; hash " + hash[..16] + ")\n");
            }
        return 0;
    }

    private static string? Arg(string[] args, string name)
    {
        for (int i = 0; i + 1 < args.Length; i++) if (args[i] == name) return args[i + 1];
        return null;
    }

    private static T[] List<T>(string text, Func<string, T> parse)
    {
        string[] parts = text.Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);
        var r = new T[parts.Length];
        for (int i = 0; i < parts.Length; i++) r[i] = parse(parts[i]);
        return r;
    }
}
