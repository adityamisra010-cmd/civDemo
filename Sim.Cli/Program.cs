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
    "corridors" => Cli.Guard(() => Cli.CorridorsCmd(args)),
    "research" => Cli.Guard(() => ResearchCli.Run(args)),
    "split-check" => Cli.Guard(() => SplitCheckCli.Run(args)),
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
                  sim run --seed S --turns N [--founded [--size PX] [--settlements N] [--ai-empires N]]
                          [--report] [--save-at K --save PATH] [--orders PATH]
                          [--hash-log PATH] [--emit-session DIR] [--split-report PATH]
                  sim run --from-save SAVE --turns N [--size PX] [--ai-empires N] [--seed S] [--founded]
                          [--report] [--save-at K --save PATH] [--orders PATH]
                          [--hash-log PATH] [--split-report PATH]
                  (run, replay and inspect take --without-discovery: sim.json discovery and fog off —
                   the attribution twin of the M6 discovery layer, for legs measured before it; ADR-044;
                   and --without-tranche2: the pre-tranche-2 research content, the D-063 twin;
                   and --without-economy: every goods.json economy packet off, the ADR-056 twin)
                  sim split-check REPORT [--control CONTROL] [--expect-hold-at K]
                                  [--twin TWIN --order-at T:B]
                  sim hash SAVEFILE [--size PX]
                  sim diff A.bin B.bin [--size PX]
                  sim replay --seed S --orders PATH --turns N
                          [--founded [--size PX] [--settlements N] [--ai-empires N]] [--hash-log PATH]
                          [--report-jsonl PATH [--report-every N]] [--emit-session DIR]
                  sim inspect --manifest runs/session-STAMP.json [--turn N] [--window K]
                          [--report-jsonl PATH] [--telemetry OUT.jsonl]
                          [--settlement ID --turn N]
                          [--answer TOPIC [--settlement ID]]
                          [--explain KIND --settlement ID --turn N [--class C]]

                --answer reads the SAVED record (telemetry + trace + forensic +
                manifest) and answers without re-running the simulation. Every
                answer is tagged KNOWN / DERIVABLE / NOT RECORDED, and a NOT
                RECORDED answer is never promoted. TOPIC is one of: world,
                settlements, polities, movements, resources, happiness,
                migration, artisan, events, hashes, limits, all.

                --explain routes the causal layer headlessly. KIND is one of:
                happiness, needs, migration, chain. It needs the (prev, next)
                world pair, which nothing persists, so it is RECONSTRUCTED by
                replaying to that turn — the output says so on every answer.
                  sim bench --seed S --turns N [--founded [--settlements N]] [--json]
                          [--without-discovery] [--without-tranche2] [--without-economy]
                  sim autoplay --seeds N --turns T --metrics OUT.json [--seed-base S]
                  sim worldgen --seed S [--stats] [--size PX]
                  sim corridors --metrics nightly-metrics.json
                  sim research --seed S --turns N [--settlements N] [--size PX]
                          [--orders PATH] [--polity P] [--node ID]
                          [--auto cheapest] [--emit-orders PATH]

                research (ADR-029) runs the FOUNDED world and prints the Glass Box
                research report for one polity (default 1): the Research Point pool, the
                active target, completed knowledge per tree and subtree, the research
                stage, available nodes, partial progress, Eurekas and university cost
                modifiers. --node ID adds one node's full breakdown. --auto cheapest
                is a MEASUREMENT DRIVER, not an AI: whenever the polity has no target
                it appends a SetResearchTarget order for the cheapest available node
                (EffectiveCost, then key) to the order log, so the run exercises the
                real order pathway. --emit-orders writes that log for `sim run
                --founded --orders` replay.

                --founded: run the production world (M2: worldgen + settlements +
                pop/food/pathbuild pipeline) instead of the M0 toy world. Labor
                orders (kind 2) require --founded. --size replays a session
                played on a non-canonical world size (runs/orders-*-sPX.bin);
                --settlements overrides siting.settlementCount (D-029 — the
                first-reign fixture replays at --settlements 1; a non-canonical
                count is recorded as runs/orders-*-nN.bin). --ai-empires N founds N
                AI-commanded Empires instead of worldgen.json's aiEmpires (default 0;
                ADR-033 D5). A founded `run` appends every AI polity's orders each turn
                (AiOrders: research, Age advance, roads, construction, tax — through the
                player's own order constructors) to the run's order log, as the UI does;
                that includes the AI polity every revolt founds mid-run, with or without
                --ai-empires. `replay` never produces orders, it replays the log, so give
                it the same --ai-empires and the log the run wrote (--emit-session).

                hash/diff on a FOUNDED save regenerate its terrain from the
                seed in the header (ADR-008: terrain is not in the stream) at
                the canonical size, or --size PX. diff (T4.19/CR-013) walks
                both canonical streams in schema order and reports the first
                divergent table/row/field (doubles as R text + 64-bit pattern)
                and a per-table summary; exit 0 identical, 1 different. A
                schema-v32, -v33 or -v34 save is migrated on load and says so
                in one line on stderr; stdout is unchanged in shape. v32: its
                terrain is regenerated flat and the four tables added since
                v32 (ConstructionProgress, ResourceAllocations,
                ConstructionHolds, CivicsPoints) are added empty. v33:
                ConstructionHolds and CivicsPoints are added empty. v34:
                CivicsPoints is added empty.

                --from-save continues a saved world in this process (D-055):
                --turns N more steps; --save-at is an absolute turn after the
                save's. The seed and the world kind are the save's (--seed and
                --founded are refused if they contradict it); --size is needed
                for a save made at a non-canonical size; --ai-empires, if given,
                must equal the number of AI Empires the save was founded with
                (AI polities a revolt founded are not counted); --settlements and
                --emit-session are refused: a continued run writes no session
                record. --orders must be the PLAYER's log: its records stamped
                before the save's turn are dropped, and a log with any record
                stamped at or after that turn by a polity other than the player
                is refused (an emitted run log whose AI orders reach the save's
                turn: the continued run issues them itself, so they would be
                delivered twice). Order validation reads the regenerated
                founding world, as the uninterrupted run does.

                --split-report PATH (founded worlds only) writes, per step and
                per Empire, the building/roads split and the D-055 set-asides as
                construction computed them beside what building work and road
                works actually drew (JSONL, split-report/v1). It changes no
                state: the hashes and saves are the same without it.
                split-check judges such a report (properties P0-P12; --control:
                the report of the default-split twin run from the same world;
                --expect-hold-at K: world K holds a set-aside that binds a
                younger head; --twin TWIN --order-at T:B: TWIN is the run
                without the kind-12 order stamped T that set b = B, and the
                order changed building work's draw in its own step) and exits
                1 naming every property that fails.
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

        /// <summary>M6 Track EX (ADR-044 §12): `--without-discovery` (run, replay) — the shipped rules with sim.json discovery and
        /// fog switched off (DiscoveryQuery.WithoutDiscovery, the attribution twin TestConfigs.PreDiscovery's switch), so a
        /// scripted leg whose premises predate the discovery layer (the D-055 allocation leg, the --from-save rigs) runs the
        /// world those premises were measured on. A session emitted under it replays only under it. ADR-055 (the R4 rule, as
        /// TestConfigs.PreDiscovery): the newer military layer is off under it too — in the rules and at the founding (the
        /// warband stays the ADR-031 token), so a `--without-discovery` leg is the in-process PreDiscovery twin's world.</summary>
        internal static bool DiscoveryOff { get; private set; }

        /// <summary>D-063 (tranche 2): `--without-tranche2` (run, replay, inspect) — the shipped rules with the research
        /// content replaced by the pre-tranche-2 file (DataFiles.OpenResearchPreTranche2, the attribution twin
        /// TestConfigs.PreTranche2's content), so a scripted leg whose premises predate tranche 2 (the D-055 allocation leg)
        /// runs the world those premises were measured on. A session emitted under it replays only under it.</summary>
        internal static bool Tranche2Off { get; private set; }

        /// <summary>ADR-056 (the M7 economy packets): `--without-economy` (run, replay, inspect) — the shipped rules with every
        /// goods.json economy packet disabled (GoodsConfig.WithoutPacket, the attribution twin TestConfigs.PreEconomyTrack's
        /// switch), applied at founding too (the packets add stock and deposit rows), so a session or save recorded before the
        /// packets — or a scripted leg whose premises predate them — runs the world it was recorded on. A session emitted
        /// under it replays only under it.</summary>
        internal static bool EconomyOff { get; private set; }

        /// <summary>Removes `--without-discovery`, `--without-tranche2` and `--without-economy` from <paramref name="args"/>
        /// and records them.</summary>
        internal static string[] TakeDiscoverySwitch(string[] args)
        {
            if (Array.IndexOf(args, "--without-discovery") >= 0) DiscoveryOff = true;
            if (Array.IndexOf(args, "--without-tranche2") >= 0) Tranche2Off = true;
            if (Array.IndexOf(args, "--without-economy") >= 0) EconomyOff = true;
            return Array.FindAll(args, a => a != "--without-discovery" && a != "--without-tranche2" && a != "--without-economy");
        }

        /// <summary>ADR-056: <paramref name="cfg"/> with every economy packet disabled when `--without-economy` was given.</summary>
        internal static Sim.Core.Systems.SimConfig ApplyEconomySwitch(Sim.Core.Systems.SimConfig cfg)
        {
            return EconomyOff ? Sim.Core.State.EconomyKnowledge.WithoutEconomy(cfg) : cfg;
        }

        internal static Sim.Core.Systems.SimConfig SimCfg() => ApplyLayerSwitches(ShippedSimCfg());

        /// <summary>The recorded layer switches applied to <paramref name="cfg"/>: tranche 2 first (content), then discovery.</summary>
        internal static Sim.Core.Systems.SimConfig ApplyLayerSwitches(Sim.Core.Systems.SimConfig cfg)
        {
            cfg = ApplyEconomySwitch(cfg);   // ADR-056 `--without-economy`
            if (Tranche2Off)
            {
                using var goods = Sim.Data.DataFiles.OpenGoods();
                using var pre = Sim.Data.DataFiles.OpenResearchPreTranche2();
                using var reader = new StreamReader(pre);
                cfg = cfg with
                {
                    Research = Sim.Core.Systems.Research.ResearchContentLoader.Load(reader.ReadToEnd(),
                        Sim.Core.Systems.GoodsConfigLoader.Load(goods)),
                };
            }
            return DiscoveryOff
                ? Sim.Core.State.RecruitmentQuery.WithoutMilitary(Sim.Core.State.DiscoveryQuery.WithoutDiscovery(cfg)) : cfg;
        }

        private static Sim.Core.Systems.SimConfig ShippedSimCfg()
        {
            using var simStream = Sim.Data.DataFiles.OpenSim();
            using var needsStream = Sim.Data.DataFiles.OpenNeeds();
            using var goodsStream = Sim.Data.DataFiles.OpenGoods();
            using var researchStream = Sim.Data.DataFiles.OpenResearch();
            using var agesStream = Sim.Data.DataFiles.OpenAges();
            using var familiesStream = Sim.Data.DataFiles.OpenUnitFamilies();
            return Sim.Core.Systems.SimConfigLoader.Load(simStream, needsStream, goodsStream, researchStream,
                agesStream, familiesStream);
        }

        internal static TurnExecutor Executor(OrderLog? orders, bool founded = false, Sim.Core.Systems.SimConfig? cfg = null)
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
                PipelineLoader.Load(pipeStream, SystemCatalog.All(cfg ?? SimCfg(), wg)),
                orders);
        }

        /// <summary>The starting world: M0 toy genesis, or the founded production world. <paramref name="wrapXOverride"/>
        /// (D-054): <see cref="HeadlessFounding.Found"/>'s topology override — `sim inspect` founds a session recorded
        /// before v33 flat. <paramref name="withoutMilitary"/> (ADR-055): found with the military layer off (the warband a
        /// token) — `sim inspect` of a session recorded before v39; `--without-discovery` implies it.</summary>
        internal static WorldState StartWorld(
            ulong seed, bool founded, int? sizeOverridePx = null, int? settlementsOverride = null,
            int? aiEmpiresOverride = null, bool? wrapXOverride = null, bool withoutMilitary = false) =>
            founded ? HeadlessFounding.Found(seed, sizeOverridePx, settlementsOverride, aiEmpiresOverride, wrapXOverride,
                withoutMilitary || DiscoveryOff) : Genesis(seed);

        internal static int? AiEmpiresOpt(Options opts, bool founded)
        {
            long n = opts.LongOr("--ai-empires", -1);
            if (n < 0) return null;
            if (!founded) throw new CliUsageException("--ai-empires requires --founded");
            return (int)n;
        }

        internal static int? SizeOpt(Options opts, bool founded)
        {
            long size = opts.LongOr("--size", -1);
            if (size < 0) return null;
            if (!founded) throw new CliUsageException("--size requires --founded");
            return (int)size;
        }

        internal static int? SettlementsOpt(Options opts, bool founded)
        {
            long n = opts.LongOr("--settlements", -1);
            if (n < 0) return null;
            if (!founded) throw new CliUsageException("--settlements requires --founded");
            if (n < 1) throw new CliUsageException($"--settlements must be >= 1, got {n}");
            return (int)n;
        }

        /// <summary>
        /// A BARE order log (`run --orders`, `run --from-save --orders`, `replay --orders`, `research --orders`): loaded
        /// and range-validated, and — D-056 — refused when it carries a legacy slot-only cancel (kind 11), which no trace
        /// can check here (<see cref="ReplayGuard.RefuseUnverifiable"/>). The refusal happens before a world is built or
        /// any output file is opened. A session's own log is read by <see cref="LoadSessionOrders"/> instead.
        /// </summary>
        internal static OrderLog LoadOrders(string path)
        {
            OrderLog log = LoadSessionOrders(path);
            RefuseOtherSchemasSessionLog(path);   // D-057
            ReplayGuard.RefuseUnverifiable(log, "order log " + path,
                "Re-issue the cancels with this build (it records the entry each one names, kind 13), or inspect the "
                + "session they were recorded in with `sim inspect --manifest`, which checks them against its trace.");
            return log;
        }

        /// <summary>
        /// D-057 — a bare-log runner given THE LOG OF A SESSION RECORDED UNDER ANOTHER SCHEMA (the `session-X.json` beside
        /// `orders-X.bin` names it and records another schema version) refuses it, before a world is built or any file is
        /// opened. The log's orders name that game's settlements and polities by id; these runners found this build's world
        /// and check no trace, so nothing shows the ids name the recorded entities — and `--emit-session` would turn the
        /// result into a valid-looking session of this build. `sim inspect` and the game's resume check every order against
        /// the session's trace (ReplayProof). A log with no session beside it, or one of a session of this schema, is
        /// replayed as this build's own run: such a runner asserts nothing about a recording (D-057 §4).
        /// </summary>
        private static void RefuseOtherSchemasSessionLog(string path)
        {
            string name = Path.GetFileName(path);
            if (!name.StartsWith("orders-", StringComparison.Ordinal) || !name.EndsWith(".bin", StringComparison.Ordinal)) return;
            string manifestPath = Path.Combine(Path.GetDirectoryName(Path.GetFullPath(path)) ?? "",
                "session-" + name["orders-".Length..^".bin".Length] + ".json");
            if (!File.Exists(manifestPath)) return;
            SessionManifest manifest;
            using (FileStream file = File.OpenRead(manifestPath)) manifest = SessionManifest.Read(file, manifestPath);
            if (!string.Equals(manifest.OrdersFile, name, StringComparison.Ordinal)
                || manifest.SchemaVersion == CanonicalSchema.Version) return;
            throw new OrderValidationException(
                $"order log {path} is the log of session {Path.GetFileName(manifestPath)}, recorded under save schema " +
                $"v{manifest.SchemaVersion.ToString(CultureInfo.InvariantCulture)} (this build is " +
                $"v{CanonicalSchema.Version.ToString(CultureInfo.InvariantCulture)}). Its orders name that game's settlements " +
                "and polities by id, and this command replays them into this build's world without checking the session's " +
                "trace, so it could apply them to different settlements — and a session emitted from it would look like a " +
                "valid session of this build. Nothing was replayed and no file was written. Inspect the session with `sim " +
                $"inspect --manifest {manifestPath}` (it checks every order against the session's trace), or resume it in the game.");
        }

        /// <summary>A session's order log, for `sim inspect`: loaded and range-validated only — its kind-11 cancels are
        /// checked against the session trace as they are delivered (<see cref="ReplayGuard.CheckBeforeDelivery"/>).</summary>
        internal static OrderLog LoadSessionOrders(string path)
        {
            using var stream = File.OpenRead(path);
            return OrderLog.Load(stream);
        }

        // --- commands ---------------------------------------------------------

        internal static int Run(string[] args)
        {
            args = TakeDiscoverySwitch(args);
            var opts = Options.Parse(args, flags: ["--report", "--founded"],
                valued: ["--seed", "--turns", "--save-at", "--save", "--orders", "--hash-log", "--size",
                         "--settlements", "--emit-session", "--ai-empires", "--from-save", "--split-report"]);
            // D-055 / ADR-040: continue a saved world in this process (FromSave's header states every rule).
            if (opts.Get("--from-save") is { } fromSave) return FromSave.Run(opts, fromSave);
            ulong seed = opts.Seed();
            int turns = opts.Turns();
            bool founded = opts.Has("--founded");
            int? sizePx = SizeOpt(opts, founded);
            int? settlements = SettlementsOpt(opts, founded);
            int? aiEmpires = AiEmpiresOpt(opts, founded);
            long saveAt = opts.LongOr("--save-at", -1);
            string? savePath = opts.Get("--save");
            if (saveAt >= 0 && savePath is null)
                throw new CliUsageException("--save-at requires --save PATH");
            if (savePath is not null && saveAt < 0)
                throw new CliUsageException("--save requires --save-at K");
            if (saveAt == 0 || saveAt > turns)
                throw new CliUsageException($"--save-at must be in 1..{turns}, got {saveAt}");
            if (opts.Get("--split-report") is not null && !founded)
                throw new CliUsageException("--split-report requires --founded (the M0 toy world has no Empire to split for)");

            OrderLog? loaded = opts.Get("--orders") is { } op ? LoadOrders(op) : null;
            WorldState world = StartWorld(seed, founded, sizePx, settlements, aiEmpires);
            // World-dependent order validation happens HERE — before turn 1,
            // never mid-run (payload ranges were already checked at load).
            if (loaded is not null) OrderValidation.ValidateAgainstWorld(loaded, world);
            WorldState turnZero = world;

            // ADR-033 D5: a founded world runs the AI order producer each turn, headless as in
            // the UI (UiSession.EndTurn calls AiOrders.Append on every turn, unconditionally).
            // The executor reads a RUN LOG that receives, before each step, the loaded log's
            // rows for that turn (in their order) followed by the AI's orders — the log the
            // session record writes and `sim replay` reproduces.
            // M5 hardening H4 (2026-10-05): this was gated on an AI polity existing AT FOUNDING.
            // That went stale at R3: every revolt founds a new AI polity mid-run (D-048), so a
            // run without --ai-empires never drove a revolted settlement's polity while the UI
            // does — the same seed and player orders gave a different world. Every founded run
            // now takes the run-log path. A world that never holds an AI polity issues no AI
            // order, so its hashes are unchanged (the ci.yml founded legs reproduce).
            Sim.Core.Systems.SimConfig? aiCfg = founded ? SimCfg() : null;
            // M5 S1c: the AI's Found Settlement orders are judged with the worldgen siting config — the one the
            // founded pipeline's ColonizationSystem is built from (Executor above loads the same file).
            Sim.Core.Worldgen.WorldgenConfig? aiWorldgen = founded ? CliRecipes.Worldgen() : null;
            OrderLog? orders = aiCfg is not null ? new OrderLog() : loaded;
            var executor = Executor(orders, founded);

            var hashLog = opts.Get("--hash-log") is not null ? new List<string>(turns) : null;
            using SessionEmitter? session = Emitter(opts, seed, founded, sizePx, settlements, aiEmpires, orders, world);
            using Stream? report = opts.Get("--split-report") is { } reportPath ? File.Create(reportPath) : null;
            Sim.Core.Observability.SplitProbe? probe = report is not null ? new(report, SimCfg()) : null;

            world = Steps(turns, world, turnZero, executor, loaded, orders, aiCfg, aiWorldgen, hashLog, session, probe, saveAt, savePath);
            CloseEmitter(session);

            if (hashLog is not null) WriteHashLog(opts.Get("--hash-log")!, hashLog);
            Console.WriteLine($"run complete: seed {seed}, {turns} turns, hash {WorldHash.ComputeHex(world)}");
            if (opts.Has("--report")) Report(world);
            return 0;
        }

        /// <summary>
        /// THE STEP LOOP of `sim run` — a run from founding and a continued one (`--from-save`, D-055 / ADR-040) alike:
        /// <paramref name="turns"/> steps from <paramref name="world"/>. A founded run (<paramref name="aiCfg"/> set) feeds the
        /// executor's RUN LOG <paramref name="orders"/>, before each step, the loaded log's rows for that turn and then the AI's
        /// orders (ADR-033 D5); ADR-034's delivery check reads <paramref name="turnZero"/>. The save is written when the
        /// stepped world's turn is <paramref name="saveAt"/> (an absolute turn: for a run from founding, the step index).
        /// <paramref name="probe"/>, when given (`--split-report`), observes each step through the executor's read-only window
        /// on the batch the executor delivers (its log's batch for the turn) — it changes no state, so the hashes and the save
        /// are the same with and without it.
        /// </summary>
        internal static WorldState Steps(
            int turns, WorldState world, WorldState turnZero, TurnExecutor executor, OrderLog? loaded, OrderLog? orders,
            Sim.Core.Systems.SimConfig? aiCfg, Sim.Core.Worldgen.WorldgenConfig? aiWorldgen, List<string>? hashLog,
            SessionEmitter? session, Sim.Core.Observability.SplitProbe? probe, long saveAt, string? savePath)
        {
            for (int t = 1; t <= turns; t++)
            {
                WorldState previous = world;
                if (aiCfg is not null)
                {
                    if (loaded is not null)
                    {
                        OrderBatch batch = loaded.BatchFor(previous.Clock.Turn);
                        for (int b = 0; b < batch.Count; b++) orders!.Append(batch[b]);
                    }
                    AiOrders.Append(orders!, previous, aiCfg, aiWorldgen!);
                }
                if (loaded is not null) OrderValidation.ValidateAtDelivery(loaded.BatchFor(previous.Clock.Turn), turnZero, previous); // ADR-034
                world = probe is null
                    ? executor.Step(previous)
                    : probe.Step(executor, previous, orders?.BatchFor(previous.Clock.Turn) ?? OrderBatch.Empty);
                hashLog?.Add(WorldHash.ComputeHex(world));
                session?.Observe(previous, world);
                if (world.Clock.Turn == saveAt)
                {
                    using var save = File.Create(savePath!);
                    Snapshot.Save(world, save);
                }
            }
            return world;
        }

        /// <summary>
        /// --emit-session DIR: writes the FULL session record for a headless run
        /// — manifest, orders, trace, telemetry and the forensic record — using
        /// the same writers a played session uses. Before this, no CLI verb
        /// could write any of them, so a reproducible forensic run was not
        /// possible headlessly at all.
        ///
        /// It requires --founded: a session record describes the production
        /// world, and `sim inspect` rebuilds from a manifest with founded:true.
        /// Emitting a toy run's metadata would produce a manifest that names a
        /// world the reader cannot rebuild.
        ///
        /// STRICTLY AN OBSERVER. Returning null when the flag is absent is what
        /// keeps the Step loop identical: there is no emitting branch inside it.
        /// </summary>
        private static SessionEmitter? Emitter(
            Options opts, ulong seed, bool founded, int? sizePx, int? settlements, int? aiEmpires,
            OrderLog? orders, WorldState start)
        {
            if (opts.Get("--emit-session") is not { } dir) return null;
            if (!founded) throw new CliUsageException("--emit-session requires --founded");
            return new SessionEmitter(
                dir, seed, sizePx, settlements, orders ?? new OrderLog(), SimCfg(), start,
                CliBuildInfo.Sha, CliBuildInfo.Date,
                System.Runtime.InteropServices.RuntimeInformation.RuntimeIdentifier, aiEmpires);
        }

        private static void CloseEmitter(SessionEmitter? session)
        {
            if (session is null) return;
            session.Close();
            Console.WriteLine($"session record: {session.ManifestPath}  (run {session.RunId})");
            Console.WriteLine($"  inspect it with: sim inspect --manifest {session.ManifestPath}");
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

        /// <summary>Loads a save (<see cref="SaveFiles.Load(string, long, out string?)"/>: terrain regenerated when the
        /// stream says it had one; a schema-v32 save regenerated flat, D-054 / ADR-039). A save the load MIGRATED gets
        /// its one-line note on STDERR (<see cref="SaveFiles.MigrationNote"/>): stdout stays exactly the hash / the diff
        /// a script parses.</summary>
        private static WorldState LoadSave(string path, long sizePx)
        {
            WorldState world = SaveFiles.Load(path, sizePx, out string? note);
            if (note is not null) Console.Error.WriteLine(note);
            return world;
        }

        internal static int Replay(string[] args)
        {
            args = TakeDiscoverySwitch(args);
            var opts = Options.Parse(args, flags: ["--founded"],
                valued: ["--seed", "--turns", "--orders", "--hash-log", "--size", "--settlements",
                         "--report-jsonl", "--report-every", "--emit-session", "--ai-empires"]);
            ulong seed = opts.Seed();
            int turns = opts.Turns();
            bool founded = opts.Has("--founded");
            int? sizePx = SizeOpt(opts, founded);
            int? settlements = SettlementsOpt(opts, founded);
            int? aiEmpires = AiEmpiresOpt(opts, founded);
            string ordersPath = opts.Get("--orders")
                ?? throw new CliUsageException("replay requires --orders PATH");

            OrderLog orders = LoadOrders(ordersPath);
            var executor = Executor(orders, founded);
            // A replay never produces orders (ADR-033 D5): the AI's decisions are already in the log.
            WorldState world = StartWorld(seed, founded, sizePx, settlements, aiEmpires);
            OrderValidation.ValidateAgainstWorld(orders, world);
            WorldState turnZero = world;
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
            // D-057: the session record first — it refuses a directory that already holds this run's record before any
            // output file (the report's included) is created.
            using SessionEmitter? session = Emitter(opts, seed, founded, sizePx, settlements, aiEmpires, orders, world);
            using Stream? report = reportPath is not null ? File.Create(reportPath) : null;
            Sim.Core.Systems.SimConfig? reportCfg = report is not null ? SimCfg() : null;

            for (int t = 1; t <= turns; t++)
            {
                WorldState previous = world;
                OrderValidation.ValidateAtDelivery(orders.BatchFor(previous.Clock.Turn), turnZero, previous); // ADR-034
                world = executor.Step(previous);
                hashLog?.Add(WorldHash.ComputeHex(world));
                session?.Observe(previous, world);
                if (report is not null && t % reportEvery == 0)
                    ReplayReport.WriteTurn(report, world, reportCfg!);
            }
            CloseEmitter(session);

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
            args = TakeDiscoverySwitch(args);   // M6 EX: a session emitted under `--without-discovery` is inspected under it
            var opts = Options.Parse(args, flags: [],
                valued: ["--manifest", "--turn", "--window", "--report-jsonl", "--telemetry", "--settlement",
                         "--answer", "--explain", "--class"]);

            string manifestPath = opts.Get("--manifest")
                ?? throw new CliUsageException("inspect requires --manifest PATH");
            string dir = Path.GetDirectoryName(Path.GetFullPath(manifestPath)) ?? ".";

            // --- P3: ANSWER FROM THE SAVED RECORD, WITHOUT RE-RUNNING ANYTHING.
            // Every other path through this verb replays the whole session to
            // answer anything at all. That is the wrong instrument for a
            // forensic question: what happened is established by the evidence
            // the session left behind, not by a fresh run that could differ from
            // it. --answer therefore returns BEFORE the replay below, and every
            // line it prints carries its own KNOWN / DERIVABLE / NOT RECORDED
            // tag. The reader itself lives in Sim.Core/Observability, which the
            // read-isolation gate allowlists by path prefix.
            if (opts.Get("--answer") is { } topic)
            {
                return AnswerFromRecord(manifestPath, topic, SettlementArg(opts));
            }

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
                + (manifest.Settlements is { } n ? $", settlements {n.ToString(CultureInfo.InvariantCulture)}" : "")
                + (manifest.AiEmpires is { } ai ? $", AI empires {ai.ToString(CultureInfo.InvariantCulture)}" : ""));
            // ADR-022: say where the trace was recorded and where this replay
            // runs BEFORE the verdict, so a cross-platform hash divergence
            // (CR-013 §8, expected from turn 2) is not read as a determinism
            // defect. RuntimeIdentifier is provenance printed for a human; it
            // is never an input to the replay.
            // Fix round 2 (D-054, verifier RC1-1): ONE condition for the platform lines and the verdict below — the
            // trace's hashes are compared only under this build's schema, so only then is there a comparison for the
            // platform lines to classify. The recipe line is SessionSchemaNotice.Reproduce: a session recorded before
            // v33 is not reproduced by this build's `sim replay` (it founds the cylinder), so the line names `sim inspect`.
            // Final nits (verifier RC2-1): the condition is SessionSchemaNotice.HashesChecked, which Reproduce keys on too.
            // Final nits (verifier RC2-2): an EMPTY trace has no comparison to classify and nothing to reproduce — the
            // plain platform line, then the empty-trace line, and no recipe (it named `sim inspect`, which then said
            // there was nothing to inspect).
            bool hashesChecked = SessionSchemaNotice.HashesChecked(manifest.SchemaVersion);
            Console.WriteLine(PlatformNotice.For(
                manifest.Platform, System.Runtime.InteropServices.RuntimeInformation.RuntimeIdentifier, hashesChecked && turns > 0));

            if (turns == 0)
            {
                Console.WriteLine();
                Console.WriteLine($"no turns recorded in {manifest.TraceFile} — nothing to inspect.");
                return 0;
            }

            Console.WriteLine(SessionSchemaNotice.Reproduce(manifest, Path.GetFileName(manifestPath), turns));

            OrderLog orders = LoadSessionOrders(Path.Combine(dir, manifest.OrdersFile));
            // ADR-056: a session whose manifest records no economy packet (every session recorded before them) is rebuilt
            // and replayed with every packet off — founded and stepped as it was played (UiSession.Resume's rule).
            if (manifest.RecordedWithoutEconomy) EconomyOff = true;
            // Fix round 1 (D-054, verifier F1): a session recorded before v33 was played on a FLAT map, so it is rebuilt
            // flat (TopologyFlags.RecordedFlat — UiSession.Resume's rule); before, it was rebuilt on the shipped cylinder.
            WorldState world = StartWorld(
                manifest.Seed, founded: true, manifest.SizePx, manifest.Settlements, manifest.AiEmpires,
                wrapXOverride: TopologyFlags.RecordedFlat(manifest.SchemaVersion) ? false : null,
                withoutMilitary: Sim.Core.State.RecruitmentQuery.RecordedWithoutMilitary(manifest.SchemaVersion));
            OrderValidation.ValidateAgainstWorld(orders, world);
            WorldState turnZero = world;

            // M6 Track EX (ADR-044 §7): a session recorded before v35 is replayed without the discovery layer
            // (DiscoveryQuery.RecordedWithoutDiscovery — UiSession.Resume's rule).
            Sim.Core.Systems.SimConfig cfg = Sim.Core.State.DiscoveryQuery.RecordedWithoutDiscovery(manifest.SchemaVersion)
                ? Sim.Core.State.DiscoveryQuery.WithoutDiscovery(SimCfg()) : SimCfg();
            // ADR-055: a session recorded before v39 was played without the military layer (its warband a token).
            if (Sim.Core.State.RecruitmentQuery.RecordedWithoutMilitary(manifest.SchemaVersion))
                cfg = Sim.Core.State.RecruitmentQuery.WithoutMilitary(cfg);
            var executor = Executor(orders, founded: true, cfg);
            int grain = cfg.Goods?.GrainId ?? 0;

            // D-056: the per-turn report is built in memory and written after the replay, so a refusal by the legacy-cancel
            // guard below (or by ADR-034's delivery check) leaves no partial file behind.
            string? reportPath = opts.Get("--report-jsonl");
            using MemoryStream? report = reportPath is not null ? new MemoryStream() : null;

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

            // P3 --explain needs the (prev, next) pair for ONE turn. Only that
            // pair is retained, and only when asked for: holding every world
            // would defeat the point of an observer that retains none.
            long explainAt = opts.Get("--explain") is not null && opts.Get("--turn") is not null
                ? opts.LongOr("--turn", -1) : -1;
            WorldState? explainPrev = null;
            WorldState? explainNext = null;

            var replayed = new List<SessionTrace.Row>((int)turns + 1)
            {
                SessionTrace.Parse([SessionTrace.Line(world, grain)], "replay")[0],
            };
            string recordedBy = $"build {manifest.BuildSha}, {manifest.BuildDate}, schema v{manifest.SchemaVersion.ToString(CultureInfo.InvariantCulture)}";
            int rosterMax = 0;
            for (int p = 0; p < turnZero.Polities.Count; p++) rosterMax = Math.Max(rosterMax, turnZero.Polities[p].Id.Value);
            // D-057: the identity proof — every settlement and polity a batch names must be shown to be the recorded one
            // (the trace's rows, in the recorded schema's form first), and a kind-11 batch must be delivered to the recorded
            // world (D-056); otherwise inspect stops here, with nothing written.
            var proof = new ReplayProof(played, manifest.SchemaVersion, rosterMax, "session " + Path.GetFileName(manifestPath),
                recordedBy, "Nothing was replayed past that batch and no report file was written. Inspect the session with the "
                + "build that recorded it (" + recordedBy + ").");
            for (long t = 1; t <= turns; t++)
            {
                WorldState prev = world;
                proof.BeforeDelivery(orders.BatchFor(prev.Clock.Turn), prev);
                OrderValidation.ValidateAtDelivery(orders.BatchFor(prev.Clock.Turn), turnZero, prev); // ADR-034
                world = executor.Step(prev);
                replayed.Add(SessionTrace.Parse([SessionTrace.Line(world, grain)], "replay")[0]);
                if (report is not null) ReplayReport.WriteTurn(report, world, cfg);
                log?.Observe(prev, world, cfg,
                    Sim.Core.Observability.OrderApplied.For(orders, prev.Clock.Turn));
                if (t == explainAt) { explainPrev = prev; explainNext = world; }
            }

            proof.Observe(world);   // the last world, which no batch is delivered to

            Console.WriteLine();
            // Fix round 1 (D-054, verifier F1): UiSession.Resume's S0 rule, headless — the trace's hashes cover the
            // canonical stream of the schema that recorded them, so across schemas none can equal a replayed one. Comparing
            // them printed "REPRODUCTION FAILED at turn 0", a determinism verdict on a schema difference; the line now says
            // the trace was not checked, and on what map the session was replayed.
            // D-057: under another READABLE schema the trace IS checked — in that schema's form (ReplayProof), up to the
            // first turn that differs — and the line says how far the replay reproduces the recorded game.
            Console.WriteLine(hashesChecked
                ? Divergence(played, replayed)
                : CanonicalSchema.IsReadable(manifest.SchemaVersion)
                    ? SessionSchemaNotice.CheckedInRecordedForm(manifest.SchemaVersion, proof.ProvenTurn, proof.FirstDifference, turns)
                    : SessionSchemaNotice.NotChecked(manifest.SchemaVersion));

            long? focus = opts.Get("--turn") is not null ? opts.LongOr("--turn", 0) : null;
            int window = (int)opts.LongOr("--window", 3);
            if (window < 0) throw new CliUsageException("--window must be >= 0");

            if (focus is { } turn) ReportTurn(replayed, orders, turn, window);
            else ReportOrderTurns(replayed, orders, window);

            if (reportPath is not null)
            {
                File.WriteAllBytes(reportPath, report!.ToArray());
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
                int settlementId = SettlementArg(opts)!.Value;
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

            // --- P3: the causal layer, HEADLESS. Until this packet
            // Sim.Core/Observability/Explain had ZERO references from this
            // project: the deepest causal machinery in the tree could be seen
            // only in a screenshot of the UI. This routes it, and nothing more —
            // no new state, no new arithmetic, no new mechanics. It needs the
            // (prev, next) pair, which this build persists nowhere, so it is
            // RECONSTRUCTED by the replay above and the output says so.
            if (opts.Get("--explain") is { } kind)
            {
                if (focus is not { } explainTurn)
                    throw new CliUsageException("--explain needs --turn N (the turn to explain)");
                if (SettlementArg(opts) is not { } explainSettlement)
                    throw new CliUsageException("--explain needs --settlement ID");
                if (explainPrev is null || explainNext is null)
                    throw new CliUsageException(
                        $"turn {explainTurn.ToString(CultureInfo.InvariantCulture)} was not replayed "
                        + $"(this session reached turn {turns.ToString(CultureInfo.InvariantCulture)})");

                Console.WriteLine();
                Console.Write(Sim.Core.Observability.Forensic.ExplainPrinter.Render(
                    kind, explainPrev, explainNext, cfg,
                    new Sim.Core.State.SettlementId(explainSettlement),
                    new Sim.Core.State.ClassId((int)opts.LongOr("--class", 0)),
                    explainTurn));
            }
            return 0;
        }

        /// <summary>--settlement ID, parsed once.</summary>
        private static int? SettlementArg(Options opts)
        {
            if (opts.Get("--settlement") is not { } raw) return null;
            if (!int.TryParse(raw, NumberStyles.None, CultureInfo.InvariantCulture, out int id))
                throw new CliUsageException($"--settlement must be a settlement id, got '{raw}'");
            return id;
        }

        /// <summary>
        /// Prints the tagged answers for one topic. The query surface lives in
        /// Sim.Core/Observability; this is flag parsing and printing.
        /// </summary>
        private static int AnswerFromRecord(string manifestPath, string topic, int? settlement)
        {
            var inspector = Sim.Core.Observability.Forensic.SessionInspector.Open(manifestPath);
            Sim.Core.Observability.Forensic.Answer[] answers;
            try
            {
                answers = inspector.Answer(topic, settlement);
            }
            catch (ArgumentException e)
            {
                throw new CliUsageException(e.Message);
            }

            int notRecorded = 0;
            for (int i = 0; i < answers.Length; i++)
            {
                Sim.Core.Observability.Forensic.Answer a = answers[i];
                if (a.Evidence == Sim.Core.Observability.Forensic.Evidence.NotRecorded) notRecorded++;
                Console.WriteLine();
                Console.WriteLine(a.Header);
                Console.WriteLine("  basis: " + a.Basis);
                for (int j = 0; j < a.Lines.Length; j++) Console.WriteLine("  " + a.Lines[j]);
            }
            Console.WriteLine();
            Console.WriteLine($"{answers.Length.ToString(CultureInfo.InvariantCulture)} answer(s); "
                + $"{notRecorded.ToString(CultureInfo.InvariantCulture)} tagged NOT RECORDED. "
                + "A NOT RECORDED answer is never promoted to DERIVABLE.");
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
                // D-056: a kind-13 cancel's Amount is a packed identity; it is printed as what it names.
                string amount = o.Kind == OrderKind.CancelConstructionEntry
                    ? OrderValidation.DescribeCancelTarget(o.Amount)
                    : o.Amount.ToString("0.###", CultureInfo.InvariantCulture);
                Console.WriteLine($"  ORDER  {o.Kind} target {o.TargetId.ToString(CultureInfo.InvariantCulture)} "
                    + $"= {amount}  "
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
            // M6 closure (track FOG; ADR-044 dated note 2026-10-28): bench takes the layer switches too, so the discovery
            // layer's cost is measured against its attribution twin (`--without-discovery`, which by the R4 rule also turns
            // the newer military layer off — the per-system "discovery" phase is the layer's own cost).
            args = TakeDiscoverySwitch(args);
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

        /// <summary>
        /// M4 closure — THE NIGHTLY'S CORRIDOR REPORT, IN TESTED CODE.
        ///
        /// Replaces the untested `jq` expression that `ci.yml` carried. That expression
        /// printed a quarantined corridor's measured range beside its recorded window and
        /// never compared them, so a corridor drifting clean out of its own window printed
        /// exactly like one sitting inside it. Its gate short-circuited on
        /// `quarantine.active`, so nothing downstream looked either. The comparison now
        /// lives in <see cref="CorridorStatus"/>, which the test suite exercises directly —
        /// the T3.12 lesson ("nothing checked the checker") applied one level up.
        ///
        /// EXIT CODE IS UNCHANGED IN MEANING: 1 only for a liveness failure or a
        /// NON-quarantined corridor out of band. A quarantined corridor reports and does
        /// not gate, whatever its window says (T3.12's mechanism, enforced in
        /// <see cref="CorridorStatus.Gates"/>). This step moves no band, no window and no
        /// quarantine flag.
        /// </summary>
        internal static int CorridorsCmd(string[] args)
        {
            var opts = Options.Parse(args, flags: [], valued: ["--metrics"]);
            string metricsPath = opts.Get("--metrics")
                ?? throw new CliUsageException("corridors requires --metrics FILE.json");

            using var corridorStream = Sim.Data.DataFiles.OpenCorridors();
            using var corridorDoc = System.Text.Json.JsonDocument.Parse(corridorStream);
            using var metricsDoc = System.Text.Json.JsonDocument.Parse(File.ReadAllText(metricsPath));

            System.Text.Json.JsonElement seeds = metricsDoc.RootElement.GetProperty("seeds");
            if (seeds.GetArrayLength() == 0)
            {
                Console.Error.WriteLine("corridors: metrics file carries no seeds — vacuous");
                return 1;
            }

            // Fixed, explicit corridor list: an added corridor must be added HERE too, which
            // is deliberate — a corridor that silently escapes the sweep is the defect.
            string[] keys = ["densityPerArableKm2", "migrationGrossPerDecade"];

            Console.WriteLine("── corridor status ─────────────────────────────────────────");
            bool gated = false, needsReading = false;

            for (int k = 0; k < keys.Length; k++)
            {
                string key = keys[k];
                System.Text.Json.JsonElement c =
                    corridorDoc.RootElement.GetProperty("canonical").GetProperty(key);

                double lo = double.PositiveInfinity, hi = double.NegativeInfinity;
                foreach (System.Text.Json.JsonElement s in seeds.EnumerateArray())
                {
                    double v = s.GetProperty("derived").GetProperty(key).GetDouble();
                    if (v < lo) lo = v;
                    if (v > hi) hi = v;
                }

                System.Text.Json.JsonElement band = c.GetProperty("band");
                double bandLo = band[0].GetDouble(), bandHi = band[1].GetDouble();

                bool qActive = false;
                double wLo = double.NaN, wHi = double.NaN;
                string owner = "";
                if (c.TryGetProperty("quarantine", out System.Text.Json.JsonElement q))
                {
                    qActive = q.TryGetProperty("active", out System.Text.Json.JsonElement a)
                        && a.GetBoolean();
                    if (q.TryGetProperty("window", out System.Text.Json.JsonElement w)
                        && w.GetArrayLength() == 2)
                    {
                        wLo = w[0].GetDouble();
                        wHi = w[1].GetDouble();
                    }
                    if (q.TryGetProperty("owner", out System.Text.Json.JsonElement o))
                        owner = o.GetString() ?? "";
                }

                CorridorVerdict verdict =
                    CorridorStatus.Classify(lo, hi, bandLo, bandHi, qActive, wLo, wHi);
                gated |= CorridorStatus.Gates(verdict);
                needsReading |= CorridorStatus.NeedsReading(verdict);

                // For a quarantined corridor, say where it stands against BOTH objects.
                // The band is the TARGET and the window is the RECORDED DEVIATION; they are
                // different things, and printing only one of them is how the last blind spot
                // got made. Counted per seed, not just as an envelope, because an envelope
                // hides how many seeds are out.
                int inBand = 0, inWindow = 0, n = 0;
                foreach (System.Text.Json.JsonElement s in seeds.EnumerateArray())
                {
                    double v = s.GetProperty("derived").GetProperty(key).GetDouble();
                    n++;
                    if (v >= bandLo && v <= bandHi) inBand++;
                    if (qActive && !double.IsNaN(wLo) && v >= wLo && v <= wHi) inWindow++;
                }

                string measured = Interval(lo, hi);
                string line = $"{CorridorStatus.Word(verdict),-14} {key}  measured {measured}  band {Interval(bandLo, bandHi)}";
                if (qActive) line += $"  window {Interval(wLo, wHi)}";
                Console.WriteLine(line);
                Console.WriteLine($"               per seed: {inBand}/{n} inside the TARGET band"
                    + (qActive ? $"; {inWindow}/{n} inside the recorded window" : ""));

                if (CorridorStatus.NeedsReading(verdict))
                {
                    double overshoot =
                        CorridorStatus.WindowOvershootFraction(lo, hi, wLo, wHi);
                    Console.WriteLine(
                        "               ^^ the quarantined corridor has LEFT its recorded window"
                        + (double.IsNaN(overshoot)
                            ? " (no usable window recorded)"
                            : $" by {(overshoot * 100.0).ToString("F4", CultureInfo.InvariantCulture)} %"));
                    if (owner.Length > 0) Console.WriteLine($"               owner: {owner}");
                }
            }

            // Liveness, unchanged from the jq the step replaces.
            foreach (System.Text.Json.JsonElement s in seeds.EnumerateArray())
            {
                if (s.GetProperty("finalPopulation").GetInt64() > 0) continue;
                Console.Error.WriteLine("corridors: an extinct world in the sweep — vacuous");
                gated = true;
            }

            Console.WriteLine("────────────────────────────────────────────────────────────");
            if (needsReading)
            {
                Console.WriteLine(
                    "QUARANTINE DRIFT — a quarantined corridor left its recorded window. This does");
                Console.WriteLine(
                    "NOT gate (T3.12): the corridor reports. It needs a director ruling, not a re-band.");
            }
            if (gated)
            {
                Console.Error.WriteLine("NIGHTLY CORRIDOR BREACH — inspect the metrics artifact");
                return 1;
            }
            return 0;
        }

        private static string Interval(double lo, double hi) =>
            "[" + lo.ToString("R", CultureInfo.InvariantCulture) + ", "
                + hi.ToString("R", CultureInfo.InvariantCulture) + "]";

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

        internal static void Report(WorldState world)
        {
            var biomass = ConservationAuditor.AuditQuantity(world, ConservedQuantityIds.Biomass);
            var goods = ConservationAuditor.AuditQuantity(world, ConservedQuantityIds.ToyGood);
            Console.WriteLine($"turn {world.Clock.Turn}, year {world.Clock.WorldDateYears.ToString("F1", CultureInfo.InvariantCulture)} since epoch");
            Console.WriteLine($"biomass: stocks {biomass.StockTotal} (conserved: {biomass.IsConserved})");
            Console.WriteLine($"toyGood: stocks {goods.StockTotal} (conserved: {goods.IsConserved})");
        }

        internal static void WriteHashLog(string path, List<string> lines)
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
        /// <param name="wrapXOverride">D-054 (fix round 1): found the world with this east-west topology instead of
        /// worldgen.json's <c>topology.wrapX</c> — UiFounding.Found's seam, the same override. `sim inspect` sets it false
        /// for a session recorded before v33 (TopologyFlags.RecordedFlat); no command line sets it.</param>
        /// <param name="withoutMilitary">ADR-055: found with sim.json's military layer off (RecruitmentQuery.WithoutMilitary) —
        /// the founding warband stays the ADR-031 token that holds no one. `sim inspect` sets it for a session recorded before
        /// v39 (RecruitmentQuery.RecordedWithoutMilitary); `--without-discovery` sets it (the R4 rule).</param>
        public static WorldState Found(
            ulong seed, int? sizeOverridePx = null, int? settlementsOverride = null, int? aiEmpiresOverride = null,
            bool? wrapXOverride = null, bool withoutMilitary = false)
        {
            Sim.Core.Worldgen.WorldgenConfig wgCfg;
            using (var stream = Sim.Data.DataFiles.OpenWorldgen())
            {
                wgCfg = Sim.Core.Worldgen.WorldgenConfigLoader.Load(stream);
            }
            if (sizeOverridePx is { } sz) wgCfg = wgCfg with { SizePx = sz };
            // ADR-033 D5: worldgen.json's aiEmpires (default 0) unless overridden (sim run/replay --ai-empires).
            if (aiEmpiresOverride is { } ai) wgCfg = wgCfg with { AiEmpires = ai };
            if (wrapXOverride is { } wrap) wgCfg = wgCfg with { Topology = new Sim.Core.Worldgen.TopologyConfig(wrap) };
            Sim.Core.Systems.SimConfig simCfg;
            using (var stream = Sim.Data.DataFiles.OpenSim())
            using (var needs = Sim.Data.DataFiles.OpenNeeds())
            using (var goods = Sim.Data.DataFiles.OpenGoods())
            using (var research = Sim.Data.DataFiles.OpenResearch())
            using (var ages = Sim.Data.DataFiles.OpenAges())
            using (var families = Sim.Data.DataFiles.OpenUnitFamilies())
            {
                simCfg = Sim.Core.Systems.SimConfigLoader.Load(stream, needs, goods, research, ages, families);
            }
            if (withoutMilitary) simCfg = Sim.Core.State.RecruitmentQuery.WithoutMilitary(simCfg);
            simCfg = Cli.ApplyEconomySwitch(simCfg);   // ADR-056: `--without-economy` founds the pre-packet roster
            return Sim.Core.Worldgen.WorldFounding.Found(wgCfg, simCfg, seed, settlementsOverride);
        }
    }
}
