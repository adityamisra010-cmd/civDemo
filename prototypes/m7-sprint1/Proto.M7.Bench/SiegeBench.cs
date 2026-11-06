using System.Diagnostics;
using System.Globalization;
using System.Text;
using Proto.M7.Time;
using Proto.M7.War.Siege;

/// <summary>
/// Lane S: siege calibration table and the D-079 Q14 clock comparison (Stopwatch and the IEEE type for REPORTING ONLY).
/// Usage: dotnet Proto.M7.Bench.dll siege [battleMs=60000] [out.md]
///   - every calibration scenario under the War Pulse clock (T2's PROPOSED per-band lengths) and the battle-pulse clock,
///     with all three ShortfallPolicy values (the food-allocation reading is not chosen);
///   - outcome, duration, evaluations, events and wall time per run.
/// </summary>
static class SiegeBench
{
    static readonly long[] ProposedPulseDays = { 21, 14, 10, 7, 3, 2, 1 };

    /// <summary>Per-pulse trace of one scenario: dotnet Proto.M7.Bench.dll siegetrace key [battleMs|0]</summary>
    public static void Trace(string[] args)
    {
        var cal = CampaignCalendar.FromJsonFile(Path.Combine(AppContext.BaseDirectory, "fixtures", "era-pacing.json"));
        var table = new long[cal.Bands.Count];
        for (int i = 0; i < table.Length; i++) table[i] = ProposedPulseDays[i] * SimInstant.MsPerDay;
        var sched = new WarPulseSchedule(cal, table);
        var h = SiegeScenarios.Get(args[1]);
        long bms = args.Length > 2 ? long.Parse(args[2], CultureInfo.InvariantCulture) : 0;
        var clock = bms > 0 ? SiegeClock.Battle(bms) : SiegeClock.WarPulse;
        ulong seed = args.Length > 3 ? ulong.Parse(args[3], CultureInfo.InvariantCulture) : SiegeHarness.Seed;
        var w = SiegeHarness.Build(h, cal, sched, clock, ShortfallPolicy.ProRata, null, out var s, out long start, seed);
        var runner = new Proto.M7.Events.Runner(s, w, Proto.M7.Events.SignificantKinds.None, stopOnSignificant: false);
        runner.RunUntil(start + 3 * cal.TurnLengthMs(cal.TurnOf(new SimInstant(start))));
        static string Qs(long raw) => (raw / 4294967296.0).ToString("F3", CultureInfo.InvariantCulture);
        Console.WriteLine("day | H | HA | wall | works | M | MA | sick | nut | garrison | civ | att | food gm | water | munitions kg | missiles | D | Pa | ration");
        int every = Math.Max(1, w.PulseRecords.Count / 60);
        for (int i = 0; i < w.PulseRecords.Count; i++)
        {
            if (i % every != 0 && i != w.PulseRecords.Count - 1) continue;
            var r = w.PulseRecords[i];
            Console.WriteLine($"{(r.EndMs - start) / (double)SimInstant.MsPerDay:F1} | {Qs(r.HealthRaw)} | {Qs(r.AttackerHealthRaw)} | {Qs(r.WallRaw)} | {Qs(r.WorksRaw)} | {Qs(r.MoraleRaw)} | {Qs(r.AttackerMoraleRaw)} | {Qs(r.DiseaseRaw)} | {Qs(r.NutritionRaw)} | {r.Garrison} | {r.Civilians} | {r.Attackers} | {r.Food} | {r.Water} | {r.Munitions} | {r.Missiles} | {r.CapabilityMilli / 1000} | {r.AssaultPowerMilli / 1000} | {Qs(r.RationRaw)}");
        }
        foreach (var b in w.Battles)
            Console.WriteLine($"battle purpose={b.Purpose} day={(b.AtMs - start) / (double)SimInstant.MsPerDay:F1} A={b.AttackMilli / 1000} D={b.DefenceMilli / 1000} odds={Qs(b.OddsRaw)} fortune={Qs(b.FortuneRaw)} success={b.Success} lossA={b.AttackerLosses} lossD={b.DefenderLosses}");
        foreach (var e in w.Ends) Console.WriteLine($"END {e.Outcome} day={(e.AtMs - start) / (double)SimInstant.MsPerDay:F2} H={Qs(e.HealthRaw)} HA={Qs(e.AttackerHealthRaw)} mines={e.Mines}");
    }

    /// <summary>Seed battery: dotnet Proto.M7.Bench.dll siegeseeds [n=20] [battleMs|0] — outcome distribution and duration quantiles.</summary>
    public static void Seeds(string[] args)
    {
        int n = args.Length > 1 ? int.Parse(args[1], CultureInfo.InvariantCulture) : 20;
        long bms = args.Length > 2 ? long.Parse(args[2], CultureInfo.InvariantCulture) : 0;
        var cal = CampaignCalendar.FromJsonFile(Path.Combine(AppContext.BaseDirectory, "fixtures", "era-pacing.json"));
        var table = new long[cal.Bands.Count];
        for (int i = 0; i < table.Length; i++) table[i] = ProposedPulseDays[i] * SimInstant.MsPerDay;
        var sched = new WarPulseSchedule(cal, table);
        var clock = bms > 0 ? SiegeClock.Battle(bms) : SiegeClock.WarPulse;
        Console.WriteLine($"seed battery: n={n} clock={(bms > 0 ? bms + " ms battle pulse" : "war pulse")} policy=ProRata (seeds 1..n)");
        Console.WriteLine("| scenario | set | history | match | outcomes | days min | days median | days max | history days |");
        Console.WriteLine("|---|---|---|---|---|---|---|---|---|");
        foreach (var h in SiegeScenarios.All)
        {
            var days = new List<double>();
            var counts = new SortedDictionary<string, int>(StringComparer.Ordinal);
            int match = 0;
            for (int i = 1; i <= n; i++)
            {
                var r = SiegeHarness.Run(h, cal, sched, clock, ShortfallPolicy.ProRata, seed: (ulong)i);
                days.Add(r.DurationMs / (double)SimInstant.MsPerDay);
                string key = r.Outcome.ToString();
                counts[key] = counts.TryGetValue(key, out int c) ? c + 1 : 1;
                if (SiegeHarness.Matches(h, r.Outcome)) match++;
            }
            days.Sort();
            var outc = string.Join(", ", counts.Select(kv => $"{kv.Key} {kv.Value}"));
            string hist = h.HistoryDaysMin == h.HistoryDaysMax ? $"{h.HistoryDaysMin}" : $"{h.HistoryDaysMin}-{h.HistoryDaysMax}";
            Console.WriteLine($"| {h.Name} | {(h.Fit ? "fit" : "holdout")} | {string.Join("/", h.HistoryOutcomes)} | {match}/{n} | {outc} | {days[0]:F0} | {days[days.Count / 2]:F0} | {days[^1]:F0} | {hist} |");
        }
    }

    public static void Run(string[] args)
    {
        long battleMs = args.Length > 1 ? long.Parse(args[1], CultureInfo.InvariantCulture) : 60_000;
        string? outPath = args.Length > 2 ? args[2] : null;
        var cal = CampaignCalendar.FromJsonFile(Path.Combine(AppContext.BaseDirectory, "fixtures", "era-pacing.json"));
        var table = new long[cal.Bands.Count];
        for (int i = 0; i < table.Length; i++) table[i] = ProposedPulseDays[i] * SimInstant.MsPerDay;
        var sched = new WarPulseSchedule(cal, table);
        var sb = new StringBuilder();
        void P(string s) { Console.WriteLine(s); sb.AppendLine(s); }
        P($"siege bench: battleMs={battleMs} cpu={Environment.ProcessorCount}");
        P("| scenario | set | clock | policy | outcome | match | days | history days | pulses | evaluations | events | wall ms | assaults (failed) | mines | garrison end | civilians end | attackers end | starved | disease | killed | deserted |");
        P("|---|---|---|---|---|---|---|---|---|---|---|---|---|---|---|---|---|---|---|---|---|");
        var clocks = new[] { SiegeClock.WarPulse, SiegeClock.Battle(battleMs) };
        var policies = new[] { ShortfallPolicy.ProRata, ShortfallPolicy.CiviliansFirst, ShortfallPolicy.GarrisonFirst };
        bool onlyWar = args.Length > 3 && args[3] == "war";
        foreach (var h in SiegeScenarios.All)
        {
            foreach (var clock in clocks)
            {
                if (onlyWar && clock.Kind != SiegeClockKind.WarPulse) continue;
                foreach (var pol in policies)
                {
                    if (clock.Kind == SiegeClockKind.BattlePulse && pol != ShortfallPolicy.ProRata) continue;
                    var sw = Stopwatch.StartNew();
                    var r = SiegeHarness.Run(h, cal, sched, clock, pol);
                    sw.Stop();
                    bool match = SiegeHarness.Matches(h, r.Outcome);
                    string hist = h.HistoryDaysMin == h.HistoryDaysMax ? $"{h.HistoryDaysMin}" : $"{h.HistoryDaysMin}-{h.HistoryDaysMax}";
                    string clk = clock.Kind == SiegeClockKind.WarPulse ? "war pulse" : $"battle {battleMs / 1000} s";
                    P($"| {h.Name} | {(h.Fit ? "fit" : "holdout")} | {clk} | {pol} | {r.Outcome} | {(match ? "yes" : "NO")} | {r.DurationMs / (double)SimInstant.MsPerDay:F2} | {hist} | {r.Pulses} | {r.Evaluations} | {r.EventsExecuted} | {sw.Elapsed.TotalMilliseconds:F0} | {r.Assaults} ({r.FailedAssaults}) | {r.Mines} | {r.GarrisonEnd} | {r.CiviliansEnd} | {r.AttackersEnd} | {r.StarvedOrThirst} | {r.DiedOfDisease} | {r.Killed} | {r.Deserted} |");
                }
            }
        }
        if (outPath is not null) File.WriteAllText(outPath, sb.ToString());
    }
}
