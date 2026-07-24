using System.Globalization;
using Sim.Cli;

// sim — headless runner (T0.9). A scripting surface: deterministic output, exit
// code 0 on success, nonzero on any failure or mismatch. Plain argument parsing
// (D-003: no new packages).
return args.Length == 0 ? Cli.Usage() : args[0] switch
{
    "run" => Cli.Guard(() => Cli.Run(args)),
    "hash" => Cli.Guard(() => Cli.Hash(args)),
    "replay" => Cli.Guard(() => Cli.Replay(args)),
    "bench" => Cli.Guard(() => Cli.Bench(args)),
    "autoplay" => Cli.Guard(() => Cli.Autoplay(args)),
    "worldgen" => Cli.Guard(() => Cli.WorldgenCmd(args)),
    _ => Cli.Usage($"unknown command '{args[0]}'"),
};

namespace Sim.Cli
{
    using Sim.Core;
    using Sim.Core.Kernel;
    using Sim.Core.State;

    internal static class Cli
    {
        internal static int Guard(Func<int> command)
        {
            try
            {
                return command();
            }
            catch (CliUsageException e)
            {
                return Usage(e.Message);
            }
            catch (Exception e)
            {
                Console.Error.WriteLine($"error: {e.Message}");
                return 2;
            }
        }

        internal static int Usage(string? error = null)
        {
            if (error is not null) Console.Error.WriteLine($"error: {error}");
            Console.Error.WriteLine("""
                usage:
                  sim run --seed S --turns N [--founded [--size PX] [--settlements N]]
                          [--report] [--save-at K --save PATH] [--orders PATH]
                          [--hash-log PATH]
                  sim hash SAVEFILE
                  sim replay --seed S --orders PATH --turns N
                          [--founded [--size PX] [--settlements N]] [--hash-log PATH]
                  sim bench --seed S --turns N [--founded [--settlements N]] [--json]
                  sim autoplay --seeds N --turns T --metrics OUT.json [--seed-base S]
                  sim worldgen --seed S [--stats] [--size PX]

                --founded: run the production world (M2: worldgen + settlements +
                pop/food/pathbuild pipeline) instead of the M0 toy world. Labor
                orders (kind 2) require --founded. --size replays a session
                played on a non-canonical world size (runs/orders-*-sPX.bin);
                --settlements overrides siting.settlementCount (D-029 — the
                first-reign fixture replays at --settlements 1; a non-canonical
                count is recorded as runs/orders-*-nN.bin).
                """);
            return 1;
        }

        // --- world/executor construction (the canonical M0 toy configuration) ---

        private static WorldState Genesis(ulong seed)
        {
            var world = new WorldState(seed);
            world.Regions.Add(new RegionRow(new RegionId(0)));
            world.Regions.Add(new RegionRow(new RegionId(1)));
            return world;
        }

        private static Sim.Core.Systems.SimConfig SimCfg()
        {
            using var simStream = Sim.Data.DataFiles.OpenSim();
            using var needsStream = Sim.Data.DataFiles.OpenNeeds();
            return Sim.Core.Systems.SimConfigLoader.Load(simStream, needsStream);
        }

        private static TurnExecutor Executor(OrderLog? orders, bool founded = false)
        {
            using var eraStream = Sim.Data.DataFiles.OpenEraPacing();
            // Default = toy preset + toy world: keeps the cross-process
            // determinism CI job exercising the M0 systems it has always pinned.
            // --founded (T1.6) runs the production preset; T1.9 pins its
            // golden and extends the harness.
            using var pipeStream = founded
                ? Sim.Data.DataFiles.OpenPipeline()
                : Sim.Data.DataFiles.OpenPipelineToy();
            return new TurnExecutor(
                EraTableLoader.Load(eraStream),
                PipelineLoader.Load(pipeStream, SystemCatalog.All(SimCfg())),
                orders);
        }

        /// <summary>The starting world: M0 toy genesis, or the founded production world.</summary>
        private static WorldState StartWorld(
            ulong seed, bool founded, int? sizeOverridePx = null, int? settlementsOverride = null) =>
            founded ? HeadlessFounding.Found(seed, sizeOverridePx, settlementsOverride) : Genesis(seed);

        private static int? SizeOpt(Options opts, bool founded)
        {
            long size = opts.LongOr("--size", -1);
            if (size < 0) return null;
            if (!founded) throw new CliUsageException("--size requires --founded");
            return (int)size;
        }

        private static int? SettlementsOpt(Options opts, bool founded)
        {
            long n = opts.LongOr("--settlements", -1);
            if (n < 0) return null;
            if (!founded) throw new CliUsageException("--settlements requires --founded");
            if (n < 1) throw new CliUsageException($"--settlements must be >= 1, got {n}");
            return (int)n;
        }

        private static OrderLog LoadOrders(string path)
        {
            using var stream = File.OpenRead(path);
            return OrderLog.Load(stream);
        }

        // --- commands ---------------------------------------------------------

        internal static int Run(string[] args)
        {
            var opts = Options.Parse(args, flags: ["--report", "--founded"],
                valued: ["--seed", "--turns", "--save-at", "--save", "--orders", "--hash-log", "--size", "--settlements"]);
            ulong seed = opts.Seed();
            int turns = opts.Turns();
            bool founded = opts.Has("--founded");
            int? sizePx = SizeOpt(opts, founded);
            int? settlements = SettlementsOpt(opts, founded);
            long saveAt = opts.LongOr("--save-at", -1);
            string? savePath = opts.Get("--save");
            if (saveAt >= 0 && savePath is null)
                throw new CliUsageException("--save-at requires --save PATH");
            if (savePath is not null && saveAt < 0)
                throw new CliUsageException("--save requires --save-at K");
            if (saveAt == 0 || saveAt > turns)
                throw new CliUsageException($"--save-at must be in 1..{turns}, got {saveAt}");

            OrderLog? orders = opts.Get("--orders") is { } op ? LoadOrders(op) : null;
            var executor = Executor(orders, founded);
            WorldState world = StartWorld(seed, founded, sizePx, settlements);
            // World-dependent order validation happens HERE — before turn 1,
            // never mid-run (payload ranges were already checked at load).
            if (orders is not null) OrderValidation.ValidateAgainstWorld(orders, world);

            var hashLog = opts.Get("--hash-log") is not null ? new List<string>(turns) : null;
            for (int t = 1; t <= turns; t++)
            {
                world = executor.Step(world);
                hashLog?.Add(WorldHash.ComputeHex(world));
                if (t == saveAt)
                {
                    using var save = File.Create(savePath!);
                    Snapshot.Save(world, save);
                }
            }

            if (hashLog is not null) WriteHashLog(opts.Get("--hash-log")!, hashLog);
            Console.WriteLine($"run complete: seed {seed}, {turns} turns, hash {WorldHash.ComputeHex(world)}");
            if (opts.Has("--report")) Report(world);
            return 0;
        }

        internal static int Hash(string[] args)
        {
            if (args.Length != 2) throw new CliUsageException("hash takes exactly one argument: SAVEFILE");
            using var stream = File.OpenRead(args[1]);
            WorldState world = Snapshot.Load(stream);
            Console.WriteLine(WorldHash.ComputeHex(world));
            return 0;
        }

        internal static int Replay(string[] args)
        {
            var opts = Options.Parse(args, flags: ["--founded"],
                valued: ["--seed", "--turns", "--orders", "--hash-log", "--size", "--settlements"]);
            ulong seed = opts.Seed();
            int turns = opts.Turns();
            bool founded = opts.Has("--founded");
            int? sizePx = SizeOpt(opts, founded);
            int? settlements = SettlementsOpt(opts, founded);
            string ordersPath = opts.Get("--orders")
                ?? throw new CliUsageException("replay requires --orders PATH");

            OrderLog orders = LoadOrders(ordersPath);
            var executor = Executor(orders, founded);
            WorldState world = StartWorld(seed, founded, sizePx, settlements);
            OrderValidation.ValidateAgainstWorld(orders, world);
            var hashLog = opts.Get("--hash-log") is not null ? new List<string>(turns) : null;
            for (int t = 1; t <= turns; t++)
            {
                world = executor.Step(world);
                hashLog?.Add(WorldHash.ComputeHex(world));
            }
            if (hashLog is not null) WriteHashLog(opts.Get("--hash-log")!, hashLog);
            Console.WriteLine($"replay complete: seed {seed}, {turns} turns, hash {WorldHash.ComputeHex(world)}");
            return 0;
        }

        internal static int Bench(string[] args)
        {
            var opts = Options.Parse(args, flags: ["--json", "--founded"],
                valued: ["--seed", "--turns", "--settlements"]);
            ulong seed = opts.Seed();
            int turns = opts.Turns();
            bool founded = opts.Has("--founded");
            int? settlements = SettlementsOpt(opts, founded);

            var executor = Executor(null, founded);
            WorldState world = StartWorld(seed, founded, null, settlements);
            var observer = new BenchObserver();

            long start = System.Diagnostics.Stopwatch.GetTimestamp();
            for (int t = 0; t < turns; t++) world = executor.Step(world, observer);
            double totalMs = TicksToMs(System.Diagnostics.Stopwatch.GetTimestamp() - start);

            if (opts.Has("--json"))
            {
                // Machine-readable variant — the future perf-gate input (see README).
                var doc = new
                {
                    seed,
                    turns,
                    totalMs,
                    phases = observer.Phases.Select(p => new
                    {
                        name = p.Name,
                        totalMs = TicksToMs(p.Ticks),
                        allocatedBytes = p.AllocatedBytes,
                    }).ToArray(),
                };
                Console.WriteLine(System.Text.Json.JsonSerializer.Serialize(doc));
            }
            else
            {
                Console.WriteLine($"bench: seed {seed}, {turns} turns, total {Ms(totalMs)} ms");
                Console.WriteLine("phase        total_ms     alloc_bytes");
                foreach (BenchObserver.Phase p in observer.Phases)
                    Console.WriteLine($"{p.Name,-12} {Ms(TicksToMs(p.Ticks)),9}    {p.AllocatedBytes,12}");
            }
            return 0;
        }

        internal static int Autoplay(string[] args)
        {
            // T2.8: the calibration battery's data source. Each seed founds an
            // INDEPENDENT canonical world (production preset, no orders) and
            // runs T turns; the per-seed metrics object is deterministic — the
            // same (seed, turns) always emits the same bytes (fixed \n,
            // InvariantCulture via JSON). Schema: README.md §Autoplay metrics.
            var opts = Options.Parse(args, flags: [],
                valued: ["--seeds", "--turns", "--metrics", "--seed-base"]);
            long seeds = opts.LongOr("--seeds", -1);
            if (seeds < 1) throw new CliUsageException("--seeds N (positive integer) is required");
            int turns = opts.Turns();
            string metricsPath = opts.Get("--metrics")
                ?? throw new CliUsageException("autoplay requires --metrics OUT.json");
            long seedBase = opts.LongOr("--seed-base", 1);

            var results = new List<AutoplayMetrics>((int)seeds);
            for (long s = 0; s < seeds; s++)
            {
                ulong seed = (ulong)(seedBase + s);
                var executor = Executor(null, founded: true);
                WorldState world = StartWorld(seed, founded: true);
                var collector = new AutoplayCollector(seed);
                for (int t = 1; t <= turns; t++)
                {
                    world = executor.Step(world);
                    collector.Observe(world);
                }
                AutoplayMetrics m = collector.Finish(world);
                results.Add(m);
                Console.WriteLine($"seed {seed}: pop {m.FinalPopulation}, year " +
                    $"{m.FinalYear.ToString("F0", CultureInfo.InvariantCulture)}, hash {m.WorldHash}");
            }

            var doc = new
            {
                schema = "autoplay-metrics/v1",
                turns,
                seeds = results.Select(m => new
                {
                    seed = m.Seed,
                    worldHash = m.WorldHash,
                    finalPopulation = m.FinalPopulation,
                    finalYear = m.FinalYear,
                    settlementCount = m.SettlementCount,
                    arableKm2 = m.ArableKm2,
                    finalCohortTotals = m.FinalCohortTotals,
                    series = new
                    {
                        year = m.Year,
                        dtYears = m.DtYears,
                        population = m.Population,
                        births = m.Births,
                        deaths = m.Deaths,
                        starvationDeaths = m.StarvationDeaths,
                        migrationGross = m.MigrationGross,
                    },
                    derived = new
                    {
                        densityPerArableKm2 = CalibrationAnalysis.DensityPerArableKm2(m),
                        migrationGrossPerDecade = CalibrationAnalysis.MigrationGrossPerDecade(m),
                        crashCount = CalibrationAnalysis.Crashes(m, 0.20).Count,
                    },
                }).ToArray(),
            };
            File.WriteAllText(metricsPath, System.Text.Json.JsonSerializer.Serialize(doc,
                new System.Text.Json.JsonSerializerOptions { WriteIndented = false }) + "\n");
            Console.WriteLine($"metrics written: {metricsPath}");
            return 0;
        }

        internal static int WorldgenCmd(string[] args)
        {
            var opts = Options.Parse(args, flags: ["--stats"], valued: ["--seed", "--size"]);
            ulong seed = opts.Seed();

            using var cfgStream = Sim.Data.DataFiles.OpenWorldgen();
            Sim.Core.Worldgen.WorldgenConfig cfg = Sim.Core.Worldgen.WorldgenConfigLoader.Load(cfgStream);
            if (opts.Get("--size") is { } sizeText)
            {
                if (!int.TryParse(sizeText, NumberStyles.None, CultureInfo.InvariantCulture, out int size) || size < 16)
                    throw new CliUsageException($"--size must be an integer >= 16, got '{sizeText}'");
                cfg = cfg with { SizePx = size };
            }

            long t0 = System.Diagnostics.Stopwatch.GetTimestamp();
            Sim.Core.Worldgen.TerrainSet terrain = Sim.Core.Worldgen.Worldgen.Generate(cfg, seed);
            double ms = TicksToMs(System.Diagnostics.Stopwatch.GetTimestamp() - t0);

            Console.WriteLine($"worldgen complete: seed {seed}, {cfg.SizePx}x{cfg.SizePx} @ " +
                $"{cfg.KmPerPx.ToString(CultureInfo.InvariantCulture)} km/px, {Ms(ms)} ms, " +
                $"terrain hash {Convert.ToHexStringLower(terrain.ContentHash)}");

            if (opts.Has("--stats"))
            {
                Console.WriteLine("field         min          max          mean");
                PrintFieldStats("elevation", terrain.Elevation);
                PrintFieldStats("water", terrain.Water);
                PrintFieldStats("temperature", terrain.Temperature);
                PrintFieldStats("moisture", terrain.Moisture);
                PrintFieldStats("fertility", terrain.Fertility);
                PrintFieldStats("movementCost", terrain.MovementCost);

                long landCells = 0;
                ReadOnlySpan<double> water = terrain.Water;
                for (int i = 0; i < water.Length; i++) if (water[i] < 0.5) landCells++;
                double landFraction = landCells / (double)water.Length;
                Console.WriteLine($"land fraction {landFraction.ToString("F4", CultureInfo.InvariantCulture)} " +
                    $"(target {cfg.LandFractionTarget.ToString(CultureInfo.InvariantCulture)}, " +
                    $"bounds {cfg.LandFractionMin.ToString(CultureInfo.InvariantCulture)}.." +
                    $"{cfg.LandFractionMax.ToString(CultureInfo.InvariantCulture)})");

                long riverCells = 0;
                ReadOnlySpan<double> riverMask = terrain.Rivers;
                for (int i = 0; i < riverMask.Length; i++) if (riverMask[i] >= 0.5) riverCells++;
                Console.WriteLine($"rivers: {terrain.RiverPolylineCount} polylines, {riverCells} cells " +
                    $"({(riverCells / (double)Math.Max(1, landCells)).ToString("F5", CultureInfo.InvariantCulture)} of land)");
                Console.WriteLine("fertility/river-adjacency correlation r=" +
                    Sim.Core.Worldgen.WorldgenStats.FertilityRiverCorrelation(terrain, cfg.Rivers.AdjacencyRadiusPx)
                        .ToString("F4", CultureInfo.InvariantCulture));
            }
            return 0;
        }

        private static void PrintFieldStats(string name, ReadOnlySpan<double> layer)
        {
            double min = double.MaxValue, max = double.MinValue, sum = 0.0;
            for (int i = 0; i < layer.Length; i++)
            {
                double v = layer[i];
                if (v < min) min = v;
                if (v > max) max = v;
                sum += v;
            }
            string F(double v) => v.ToString("F4", CultureInfo.InvariantCulture);
            Console.WriteLine($"{name,-12} {F(min),10}   {F(max),10}   {F(sum / layer.Length),10}");
        }

        // --- helpers ----------------------------------------------------------

        private static void Report(WorldState world)
        {
            var biomass = ConservationAuditor.AuditQuantity(world, ConservedQuantityIds.Biomass);
            var goods = ConservationAuditor.AuditQuantity(world, ConservedQuantityIds.ToyGood);
            Console.WriteLine($"turn {world.Clock.Turn}, year {world.Clock.WorldDateYears.ToString("F1", CultureInfo.InvariantCulture)} since epoch");
            Console.WriteLine($"biomass: stocks {biomass.StockTotal} (conserved: {biomass.IsConserved})");
            Console.WriteLine($"toyGood: stocks {goods.StockTotal} (conserved: {goods.IsConserved})");
        }

        private static void WriteHashLog(string path, List<string> lines)
            => File.WriteAllText(path, string.Join("\n", lines) + "\n"); // fixed \n: byte-identical across runs

        private static double TicksToMs(long ticks)
            => ticks * 1000.0 / System.Diagnostics.Stopwatch.Frequency;

        private static string Ms(double ms) => ms.ToString("F2", CultureInfo.InvariantCulture);
    }

    internal sealed class CliUsageException(string message) : Exception(message);

    /// <summary>Plain deterministic option parsing: no packages, loud errors.</summary>
    internal sealed class Options
    {
        private readonly Dictionary<string, string?> _values = [];

        internal static Options Parse(string[] args, string[] flags, string[] valued)
        {
            var opts = new Options();
            for (int i = 1; i < args.Length; i++)
            {
                string arg = args[i];
                if (Array.IndexOf(flags, arg) >= 0) opts._values[arg] = null;
                else if (Array.IndexOf(valued, arg) >= 0)
                {
                    if (i + 1 >= args.Length) throw new CliUsageException($"{arg} requires a value");
                    opts._values[arg] = args[++i];
                }
                else throw new CliUsageException($"unknown option '{arg}'");
            }
            return opts;
        }

        internal bool Has(string flag) => _values.ContainsKey(flag);
        internal string? Get(string option) => _values.TryGetValue(option, out string? v) ? v : null;

        internal ulong Seed() => Get("--seed") is { } s && ulong.TryParse(s, NumberStyles.None, CultureInfo.InvariantCulture, out ulong v)
            ? v : throw new CliUsageException("--seed S (unsigned integer) is required");

        internal int Turns() => Get("--turns") is { } s && int.TryParse(s, NumberStyles.None, CultureInfo.InvariantCulture, out int v) && v > 0
            ? v : throw new CliUsageException("--turns N (positive integer) is required");

        internal long LongOr(string option, long fallback) => Get(option) is { } s
            ? long.TryParse(s, NumberStyles.None, CultureInfo.InvariantCulture, out long v)
                ? v : throw new CliUsageException($"{option} must be a non-negative integer, got '{s}'")
            : fallback;
    }

    /// <summary>Accumulates per-phase wall time and allocations in first-seen order.</summary>
    internal sealed class BenchObserver : Sim.Core.Kernel.ITurnObserver
    {
        internal sealed class Phase(string name)
        {
            public string Name { get; } = name;
            public long Ticks { get; set; }
            public long AllocatedBytes { get; set; }
        }

        private readonly List<Phase> _phases = [];

        public IReadOnlyList<Phase> Phases => _phases;

        public void OnPhase(string phase, long elapsedTimestampTicks, long allocatedBytes)
        {
            for (int i = 0; i < _phases.Count; i++)
            {
                if (_phases[i].Name == phase)
                {
                    _phases[i].Ticks += elapsedTimestampTicks;
                    _phases[i].AllocatedBytes += allocatedBytes;
                    return;
                }
            }
            _phases.Add(new Phase(phase) { Ticks = elapsedTimestampTicks, AllocatedBytes = allocatedBytes });
        }
    }
}

namespace Sim.Cli
{
    using Sim.Core.State;

    /// <summary>
    /// THE headless founding recipe (T1.9): canonical worldgen.json + sim.json +
    /// WorldFounding — what `sim run/replay/bench --founded` starts from. Public
    /// and pure so the founding-equivalence test pins it against Sim.Ui's
    /// recipe: UI-session replay is only real if both apps found IDENTICAL
    /// worlds from the same seed.
    /// </summary>
    public static class HeadlessFounding
    {
        public static WorldState Found(
            ulong seed, int? sizeOverridePx = null, int? settlementsOverride = null)
        {
            Sim.Core.Worldgen.WorldgenConfig wgCfg;
            using (var stream = Sim.Data.DataFiles.OpenWorldgen())
            {
                wgCfg = Sim.Core.Worldgen.WorldgenConfigLoader.Load(stream);
            }
            if (sizeOverridePx is { } sz) wgCfg = wgCfg with { SizePx = sz };
            Sim.Core.Systems.SimConfig simCfg;
            using (var stream = Sim.Data.DataFiles.OpenSim())
            using (var needs = Sim.Data.DataFiles.OpenNeeds())
            {
                simCfg = Sim.Core.Systems.SimConfigLoader.Load(stream, needs);
            }
            return Sim.Core.Worldgen.WorldFounding.Found(wgCfg, simCfg, seed, settlementsOverride);
        }
    }
}
