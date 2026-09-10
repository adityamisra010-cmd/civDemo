using System.Globalization;
using Sim.Cli;

// sim — headless runner (T0.9). A scripting surface: deterministic output, exit
// code 0 on success, nonzero on any failure or mismatch. Plain argument parsing
// (D-003: no new packages).
return args.Length == 0 ? Cli.Usage() : args[0] switch
{
    "run" => Cli.Guard(() => Cli.Run(args)),
    "hash" => Cli.Guard(() => Cli.Hash(args)),
    "diff" => Cli.Guard(() => Cli.Diff(args)),
    "replay" => Cli.Guard(() => Cli.Replay(args)),
    "inspect" => Cli.Guard(() => Cli.Inspect(args)),
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
                  sim hash SAVEFILE [--size PX]
                  sim diff A.bin B.bin [--size PX]
                  sim replay --seed S --orders PATH --turns N
                          [--founded [--size PX] [--settlements N]] [--hash-log PATH]
                          [--report-jsonl PATH [--report-every N]]
                  sim inspect --manifest runs/session-STAMP.json [--turn N] [--window K]
                          [--report-jsonl PATH] [--telemetry OUT.jsonl]
                          [--settlement ID --turn N]
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

                hash/diff on a FOUNDED save regenerate its terrain from the
                seed in the header (ADR-008: terrain is not in the stream) at
                the canonical size, or --size PX. diff (T4.19/CR-013) walks
                both canonical streams in schema order and reports the first
                divergent table/row/field (doubles as R text + 64-bit pattern)
                and a per-table summary; exit 0 identical, 1 different.
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
            using var goodsStream = Sim.Data.DataFiles.OpenGoods();
            return Sim.Core.Systems.SimConfigLoader.Load(simStream, needsStream, goodsStream);
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
            // T4.4: the founded pipeline gets the WORLDGEN config too, because
            // frontier siting is a terrain question. The toy preset passes null
            // and colonization no-ops there (no terrain to colonise).
            Sim.Core.Worldgen.WorldgenConfig? wg = null;
            if (founded)
            {
                using var wgStream = Sim.Data.DataFiles.OpenWorldgen();
                wg = Sim.Core.Worldgen.WorldgenConfigLoader.Load(wgStream);
            }
            return new TurnExecutor(
                EraTableLoader.Load(eraStream),
                PipelineLoader.Load(pipeStream, SystemCatalog.All(SimCfg(), wg)),
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
            string[] files = Positional(args, 1, out string[] rest);
            var opts = Options.Parse(rest, flags: [], valued: ["--size"]);
            WorldState world = LoadSave(files[0], opts.LongOr("--size", -1));
            Console.WriteLine(WorldHash.ComputeHex(world));
            return 0;
        }

        /// <summary>T4.19 lane E (CR-013 §6): first divergent field between two
        /// saves, then the per-table extent. Exit 0 identical, 1 different.</summary>
        internal static int Diff(string[] args)
        {
            string[] files = Positional(args, 2, out string[] rest);
            var opts = Options.Parse(rest, flags: [], valued: ["--size"]);
            long size = opts.LongOr("--size", -1);
            WorldState a = LoadSave(files[0], size);
            WorldState b = LoadSave(files[1], size);
            Console.WriteLine($"A: {files[0]}  turn {a.Clock.Turn.ToString(CultureInfo.InvariantCulture)}  hash {WorldHash.ComputeHex(a)}");
            Console.WriteLine($"B: {files[1]}  turn {b.Clock.Turn.ToString(CultureInfo.InvariantCulture)}  hash {WorldHash.ComputeHex(b)}");
            SnapshotDiff.Result result = SnapshotDiff.Compare(a, b);
            SnapshotDiff.Print(result, Console.Out);
            return result.Identical ? 0 : 1;
        }

        /// <summary>The first <paramref name="count"/> non-option arguments after
        /// the verb, in order; <paramref name="rest"/> is the verb plus every
        /// remaining argument, shaped for <see cref="Options.Parse"/>.</summary>
        private static string[] Positional(string[] args, int count, out string[] rest)
        {
            var positional = new List<string>(count);
            var remaining = new List<string>(args.Length) { args[0] };
            for (int i = 1; i < args.Length; i++)
            {
                if (positional.Count < count && !args[i].StartsWith("--", StringComparison.Ordinal))
                    positional.Add(args[i]);
                else
                    remaining.Add(args[i]);
            }
            if (positional.Count != count)
                throw new CliUsageException($"{args[0]} takes exactly {count} file argument(s)");
            rest = remaining.ToArray();
            return positional.ToArray();
        }

        // Snapshot header layout (Snapshot.Save): magic 8 | version 4 | seed 8 |
        // turn 8, then the canonical stream: seed 8 | clock 24 | terrain flag 1.
        private const int SaveHeaderSeedOffset = 8 + 4;
        private const int SaveTerrainFlagOffset = 8 + 4 + 8 + 8 + 8 + 24;

        /// <summary>
        /// Loads a save, regenerating its terrain when the stream says it had one.
        /// Terrain is not serialized (ADR-008); Snapshot.Load demands the
        /// regenerated TerrainSet and validates its content hash against the one
        /// the save recorded, so a wrong size or worldgen config fails loudly
        /// rather than hashing a different world. The seed and the flag are read
        /// from fixed header offsets BEFORE Load so that a toy save (no terrain)
        /// is loaded plainly and a founded save gets exactly one worldgen.
        /// </summary>
        private static WorldState LoadSave(string path, long sizePx)
        {
            byte[] bytes = File.ReadAllBytes(path);
            Sim.Core.Worldgen.TerrainSet? terrain = null;
            if (bytes.Length > SaveTerrainFlagOffset && bytes[SaveTerrainFlagOffset] != 0)
            {
                ulong seed = BitConverter.ToUInt64(bytes, SaveHeaderSeedOffset);
                if (!BitConverter.IsLittleEndian) throw new InvalidOperationException("big-endian host: header peek assumes little-endian saves");
                Sim.Core.Worldgen.WorldgenConfig wgCfg;
                using (var wgStream = Sim.Data.DataFiles.OpenWorldgen())
                    wgCfg = Sim.Core.Worldgen.WorldgenConfigLoader.Load(wgStream);
                if (sizePx >= 0) wgCfg = wgCfg with { SizePx = (int)sizePx };
                terrain = Sim.Core.Worldgen.Worldgen.Generate(wgCfg, seed);
            }
            using var stream = new MemoryStream(bytes, writable: false);
            return Snapshot.Load(stream, terrain);
        }

        internal static int Replay(string[] args)
        {
            var opts = Options.Parse(args, flags: ["--founded"],
                valued: ["--seed", "--turns", "--orders", "--hash-log", "--size", "--settlements",
                         "--report-jsonl", "--report-every"]);
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

            // T3.12a — THE DIAGNOSTIC REPORTER. Strictly an OBSERVER: the report
            // stream is written FROM the post-step world and never feeds back.
            // The step call below is byte-identical whether or not reporting is
            // on — there is no reporting branch inside it, which is what makes
            // the determinism assertion in ReplayReportTests structural rather
            // than merely observed.
            string? reportPath = opts.Get("--report-jsonl");
            int reportEvery = (int)opts.LongOr("--report-every", 1);
            if (reportEvery < 1) throw new CliUsageException("--report-every must be >= 1");
            using Stream? report = reportPath is not null ? File.Create(reportPath) : null;
            Sim.Core.Systems.SimConfig? reportCfg = report is not null ? SimCfg() : null;

            for (int t = 1; t <= turns; t++)
            {
                world = executor.Step(world);
                hashLog?.Add(WorldHash.ComputeHex(world));
                if (report is not null && t % reportEvery == 0)
                    ReplayReport.WriteTurn(report, world, reportCfg!);
            }
            if (hashLog is not null) WriteHashLog(opts.Get("--hash-log")!, hashLog);
            Console.WriteLine($"replay complete: seed {seed}, {turns} turns, hash {WorldHash.ComputeHex(world)}");
            if (reportPath is not null)
                Console.WriteLine($"report: {reportPath} ({new FileInfo(reportPath).Length} bytes, every {reportEvery} turn(s))");
            return 0;
        }


        /// <summary>
        /// T4.17 — WHAT HAPPENED IN A PLAYED SESSION.
        ///
        /// Point it at a session manifest and it rebuilds that exact world from
        /// the recorded seed, replays the director's own order log into it, and
        /// reports. It answers the question a playtest actually generates —
        /// "around turn 85 I set farming to 50% and the population did something
        /// strange" — by showing the orders that landed on that turn beside the
        /// turns either side of it, so the order and its consequence are on the
        /// same screen.
        ///
        /// IT ALSO CHECKS THE SESSION AGAINST ITSELF. The trace carries the
        /// hash the DIRECTOR'S machine computed on each turn; the replay
        /// computes its own. If any turn disagrees, the session did not
        /// reproduce and the first disagreeing turn is named. That check is the
        /// reason the trace is written live rather than derived here — a replay
        /// compared only against itself proves nothing about the session it
        /// claims to reconstruct.
        /// </summary>
        internal static int Inspect(string[] args)
        {
            var opts = Options.Parse(args, flags: [],
                valued: ["--manifest", "--turn", "--window", "--report-jsonl", "--telemetry", "--settlement"]);

            string manifestPath = opts.Get("--manifest")
                ?? throw new CliUsageException("inspect requires --manifest PATH");
            string dir = Path.GetDirectoryName(Path.GetFullPath(manifestPath)) ?? ".";

            SessionManifest manifest;
            using (FileStream file = File.OpenRead(manifestPath))
            {
                manifest = SessionManifest.Read(file, manifestPath);
            }

            string tracePath = Path.Combine(dir, manifest.TraceFile);
            IReadOnlyList<SessionTrace.Row> played = File.Exists(tracePath)
                ? SessionTrace.Parse(File.ReadLines(tracePath), tracePath)
                : [];
            // The trace's last turn is how far the session actually got. With no
            // trace there is nothing to replay TO, and guessing a turn count
            // would silently report a different session than the one played.
            long turns = played.Count > 0 ? played[^1].Turn : 0;

            Console.WriteLine($"session   {Path.GetFileName(manifestPath)}");
            Console.WriteLine($"  started {manifest.StartedAt}   build {manifest.BuildSha} ({manifest.BuildDate})   schema v{manifest.SchemaVersion}");
            Console.WriteLine($"  world   seed {manifest.Seed.ToString(CultureInfo.InvariantCulture)}"
                + (manifest.SizePx is { } px ? $", size {px.ToString(CultureInfo.InvariantCulture)}" : "")
                + (manifest.Settlements is { } n ? $", settlements {n.ToString(CultureInfo.InvariantCulture)}" : ""));
            Console.WriteLine($"  reproduce with: {manifest.ReplayCommand(turns)}");

            if (turns == 0)
            {
                Console.WriteLine();
                Console.WriteLine($"no turns recorded in {manifest.TraceFile} — nothing to inspect.");
                return 0;
            }

            OrderLog orders = LoadOrders(Path.Combine(dir, manifest.OrdersFile));
            WorldState world = StartWorld(
                manifest.Seed, founded: true, manifest.SizePx, manifest.Settlements);
            OrderValidation.ValidateAgainstWorld(orders, world);

            var executor = Executor(orders, founded: true);
            Sim.Core.Systems.SimConfig cfg = SimCfg();
            int grain = cfg.Goods?.GrainId ?? 0;

            string? reportPath = opts.Get("--report-jsonl");
            using Stream? report = reportPath is not null ? File.Create(reportPath) : null;

            // T4.19 — THE GLASS BOX, HEADLESS. --telemetry rebuilds every turn's
            // record by replaying the log through the same observer the played
            // session used (§7: records are a pure function of the replay, so
            // they are never a source of truth); --settlement ID --turn N prints
            // one settlement's record in full. Both are observers of the
            // post-step pair and neither feeds back: the Step call is identical
            // whether or not a log is attached.
            string? telemetryPath = opts.Get("--telemetry");
            string? settlementArg = opts.Get("--settlement");
            Sim.Core.Observability.ObservationLog? log =
                telemetryPath is not null || settlementArg is not null
                    ? new Sim.Core.Observability.ObservationLog() : null;

            var replayed = new List<SessionTrace.Row>((int)turns + 1)
            {
                SessionTrace.Parse([SessionTrace.Line(world, grain)], "replay")[0],
            };
            for (long t = 1; t <= turns; t++)
            {
                WorldState prev = world;
                world = executor.Step(prev);
                replayed.Add(SessionTrace.Parse([SessionTrace.Line(world, grain)], "replay")[0]);
                if (report is not null) ReplayReport.WriteTurn(report, world, cfg);
                log?.Observe(prev, world, cfg,
                    Sim.Core.Observability.OrderApplied.For(orders, prev.Clock.Turn));
            }

            Console.WriteLine();
            Console.WriteLine(Divergence(played, replayed));

            long? focus = opts.Get("--turn") is not null ? opts.LongOr("--turn", 0) : null;
            int window = (int)opts.LongOr("--window", 3);
            if (window < 0) throw new CliUsageException("--window must be >= 0");

            if (focus is { } turn) ReportTurn(replayed, orders, turn, window);
            else ReportOrderTurns(replayed, orders, window);

            if (reportPath is not null)
            {
                Console.WriteLine();
                Console.WriteLine($"full per-turn state: {reportPath} ({new FileInfo(reportPath).Length} bytes)");
            }

            if (telemetryPath is not null)
            {
                using (FileStream file = File.Create(telemetryPath))
                {
                    Sim.Core.Observability.TelemetryWriter.WriteAll(file, log!);
                }
                Console.WriteLine();
                Console.WriteLine($"telemetry: {telemetryPath} ({new FileInfo(telemetryPath).Length} bytes, "
                    + $"{log!.Observations.Count.ToString(CultureInfo.InvariantCulture)} turns)");
            }

            if (settlementArg is not null)
            {
                if (!int.TryParse(settlementArg, NumberStyles.None, CultureInfo.InvariantCulture, out int settlementId))
                    throw new CliUsageException($"--settlement must be a settlement id, got '{settlementArg}'");
                if (focus is not { } at)
                    throw new CliUsageException("--settlement ID needs --turn N (the turn whose record to print)");
                Sim.Core.Observability.TurnObservation? observation = log!.At(at);
                Sim.Core.Observability.SettlementRecord? record = log.Settlement(at, settlementId);
                if (observation is null)
                    throw new CliUsageException($"turn {at.ToString(CultureInfo.InvariantCulture)} was not replayed "
                        + $"(records exist for turns 1..{turns.ToString(CultureInfo.InvariantCulture)})");
                if (record is null)
                    throw new CliUsageException($"settlement {settlementId.ToString(CultureInfo.InvariantCulture)} "
                        + $"has no record on turn {at.ToString(CultureInfo.InvariantCulture)} (not yet founded, or no such id)");
                Console.WriteLine();
                using Stream stdout = Console.OpenStandardOutput();
                Sim.Core.Observability.TelemetryWriter.WriteSettlement(stdout, record, observation.Turn);
            }
            return 0;
        }

        /// <summary>
        /// Compares what the director's machine recorded against what this
        /// replay computed, and names the FIRST turn they disagree on. A
        /// mismatch is a determinism finding, not a reporting nicety, so it is
        /// stated as one.
        /// </summary>
        private static string Divergence(
            IReadOnlyList<SessionTrace.Row> played, IReadOnlyList<SessionTrace.Row> replayed)
        {
            if (played.Count == 0) return "no live trace — replay not cross-checked.";

            int common = Math.Min(played.Count, replayed.Count);
            for (int i = 0; i < common; i++)
            {
                if (played[i].Hash == replayed[i].Hash) continue;
                return $"REPRODUCTION FAILED at turn {played[i].Turn.ToString(CultureInfo.InvariantCulture)}: "
                    + $"the session recorded {played[i].Hash[..12]}…, this replay computed {replayed[i].Hash[..12]}…. "
                    + "Every turn before it matches, so that turn is where the two diverge.";
            }

            return played.Count == replayed.Count
                ? $"reproduction VERIFIED: {common.ToString(CultureInfo.InvariantCulture)} turns, hash-for-hash."
                : $"reproduction verified for the {common.ToString(CultureInfo.InvariantCulture)} turns both cover "
                    + $"(trace has {played.Count.ToString(CultureInfo.InvariantCulture)}, replay {replayed.Count.ToString(CultureInfo.InvariantCulture)}).";
        }

        /// <summary>The turns AROUND a turn of interest, with the orders that
        /// landed on it — the order and its consequence on one screen.</summary>
        private static void ReportTurn(
            IReadOnlyList<SessionTrace.Row> rows, OrderLog orders, long turn, int window)
        {
            Console.WriteLine();
            Console.WriteLine($"--- turn {turn.ToString(CultureInfo.InvariantCulture)} "
                + $"(+/- {window.ToString(CultureInfo.InvariantCulture)}) ---");
            PrintOrdersAt(orders, turn);
            Console.WriteLine();
            PrintRows(rows, turn - window, turn + window, turn);
        }

        /// <summary>With no turn named, report every turn an order was issued
        /// on — those are the turns a director made a decision, and therefore
        /// the ones worth looking at.</summary>
        private static void ReportOrderTurns(
            IReadOnlyList<SessionTrace.Row> rows, OrderLog orders, int window)
        {
            long previous = long.MinValue;
            int reported = 0;
            for (int i = 0; i < orders.Count; i++)
            {
                long turn = orders[i].Turn;
                if (turn == previous) continue;
                previous = turn;
                if (++reported > 20)
                {
                    Console.WriteLine();
                    Console.WriteLine("… more order turns follow; name one with --turn N.");
                    return;
                }
                ReportTurn(rows, orders, turn, window);
            }

            if (reported == 0)
            {
                Console.WriteLine();
                Console.WriteLine("no orders were issued in this session — it was played on End Turn alone.");
                Console.WriteLine();
                PrintRows(rows, rows[0].Turn, rows[^1].Turn, focus: -1);
            }
        }

        private static void PrintOrdersAt(OrderLog orders, long turn)
        {
            int found = 0;
            for (int i = 0; i < orders.Count; i++)
            {
                OrderRecord o = orders[i];
                if (o.Turn != turn) continue;
                found++;
                Console.WriteLine($"  ORDER  {o.Kind} target {o.TargetId.ToString(CultureInfo.InvariantCulture)} "
                    + $"= {o.Amount.ToString("0.###", CultureInfo.InvariantCulture)}  "
                    + $"(Empire {o.ActorId.ToString(CultureInfo.InvariantCulture)})");
            }
            if (found == 0) Console.WriteLine("  (no orders issued on this turn)");
        }

        /// <summary>
        /// The trace rows in a range, with the turn-on-turn CHANGE beside each
        /// value. The delta column is the point: "population 8,339" says little,
        /// "population 8,339 (-1,204)" is the glitch the director saw.
        /// </summary>
        private static void PrintRows(
            IReadOnlyList<SessionTrace.Row> rows, long from, long to, long focus)
        {
            Console.WriteLine("   turn     year   population            food   settlements");
            for (int i = 0; i < rows.Count; i++)
            {
                if (rows[i].Turn < from || rows[i].Turn > to) continue;
                bool first = i == 0;
                long dPop = first ? 0 : rows[i].Population - rows[i - 1].Population;
                long dFood = first ? 0 : rows[i].Food - rows[i - 1].Food;
                Console.WriteLine(
                    (rows[i].Turn == focus ? " > " : "   ")
                    + rows[i].Turn.ToString(CultureInfo.InvariantCulture).PadLeft(5)
                    + rows[i].Year.ToString(CultureInfo.InvariantCulture).PadLeft(9)
                    + rows[i].Population.ToString("N0", CultureInfo.InvariantCulture).PadLeft(12)
                    + (first ? "" : Signed(dPop)).PadLeft(10)
                    + rows[i].Food.ToString("N0", CultureInfo.InvariantCulture).PadLeft(14)
                    + (first ? "" : Signed(dFood)).PadLeft(12)
                    + rows[i].Settlements.ToString(CultureInfo.InvariantCulture).PadLeft(8));
            }
        }

        private static string Signed(long delta) => delta == 0
            ? "0"
            : (delta > 0 ? "+" : "") + delta.ToString("N0", CultureInfo.InvariantCulture);

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

            // T3.11 Item 3 — THE STATE FOOTPRINT, for the m0-kernel-spec §3.2
            // clone-size claim ("at M0-M9 scale this is a few MB"). GOV-2 §5
            // recorded the measurement as OWED and named `sim bench` as the
            // instrument; the instrument did not report it, so it does now.
            // The clone phase's allocatedBytes IS the per-turn clone cost —
            // TurnExecutor brackets exactly `prev.Clone()` with
            // GC.GetAllocatedBytesForCurrentThread. Bucket rows are called out
            // by name because the projection that makes the claim urgent is
            // about them (DENSE founding vs the ~150k world-wide cap).
            long cloneBytes = 0;
            foreach (BenchObserver.Phase p in observer.Phases)
                if (p.Name == "clone") cloneBytes = p.AllocatedBytes;
            long clonePerTurn = turns > 0 ? cloneBytes / turns : 0;
            int bucketRows = world.Buckets.Count;

            if (opts.Has("--json"))
            {
                // Machine-readable variant — the future perf-gate input (see README).
                var doc = new
                {
                    seed,
                    turns,
                    totalMs,
                    bucketRows,
                    cloneBytesPerTurn = clonePerTurn,
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
                Console.WriteLine(string.Create(System.Globalization.CultureInfo.InvariantCulture,
                    $"state: bucket_rows {bucketRows}, clone_bytes_per_turn {clonePerTurn} " +
                    $"({clonePerTurn / 1048576.0:F3} MiB)"));
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
            using (var goods = Sim.Data.DataFiles.OpenGoods())
            {
                simCfg = Sim.Core.Systems.SimConfigLoader.Load(stream, needs, goods);
            }
            return Sim.Core.Worldgen.WorldFounding.Found(wgCfg, simCfg, seed, settlementsOverride);
        }
    }
}
