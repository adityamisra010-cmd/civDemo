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
                  sim run --seed S --turns N [--report] [--save-at K --save PATH]
                          [--orders PATH] [--hash-log PATH]
                  sim hash SAVEFILE
                  sim replay --seed S --orders PATH --turns N [--hash-log PATH]
                  sim bench --seed S --turns N [--json]
                  sim worldgen --seed S [--stats] [--size PX]
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

        private static TurnExecutor Executor(OrderLog? orders)
        {
            using var eraStream = Sim.Data.DataFiles.OpenEraPacing();
            using var pipeStream = Sim.Data.DataFiles.OpenPipeline();
            return new TurnExecutor(
                EraTableLoader.Load(eraStream),
                PipelineLoader.Load(pipeStream, SystemCatalog.All()),
                orders);
        }

        private static OrderLog LoadOrders(string path)
        {
            using var stream = File.OpenRead(path);
            return OrderLog.Load(stream);
        }

        // --- commands ---------------------------------------------------------

        internal static int Run(string[] args)
        {
            var opts = Options.Parse(args, flags: ["--report"],
                valued: ["--seed", "--turns", "--save-at", "--save", "--orders", "--hash-log"]);
            ulong seed = opts.Seed();
            int turns = opts.Turns();
            long saveAt = opts.LongOr("--save-at", -1);
            string? savePath = opts.Get("--save");
            if (saveAt >= 0 && savePath is null)
                throw new CliUsageException("--save-at requires --save PATH");
            if (savePath is not null && saveAt < 0)
                throw new CliUsageException("--save requires --save-at K");
            if (saveAt == 0 || saveAt > turns)
                throw new CliUsageException($"--save-at must be in 1..{turns}, got {saveAt}");

            OrderLog? orders = opts.Get("--orders") is { } op ? LoadOrders(op) : null;
            var executor = Executor(orders);
            WorldState world = Genesis(seed);

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
            var opts = Options.Parse(args, flags: [], valued: ["--seed", "--turns", "--orders", "--hash-log"]);
            ulong seed = opts.Seed();
            int turns = opts.Turns();
            string ordersPath = opts.Get("--orders")
                ?? throw new CliUsageException("replay requires --orders PATH");

            var executor = Executor(LoadOrders(ordersPath));
            WorldState world = Genesis(seed);
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
            var opts = Options.Parse(args, flags: ["--json"], valued: ["--seed", "--turns"]);
            ulong seed = opts.Seed();
            int turns = opts.Turns();

            var executor = Executor(null);
            WorldState world = Genesis(seed);
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
