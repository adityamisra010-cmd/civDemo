using System.Globalization;
using Sim.Core;
using Sim.Core.Kernel;
using Sim.Core.Observability;
using Sim.Core.State;
using Sim.Core.Systems;
using Sim.Ui.ViewModel;

namespace Sim.Ui;

/// <summary>
/// The played session as a TESTABLE seam (T1.9 adversarial hardening): order
/// stamping, executor construction, End Turn, and session-log persistence all
/// live HERE — SimUiGame and Program.cs only call it. The adversarial pass
/// showed the previous shape let order-timing drift (Clock.Turn+1 stamping),
/// pipeline drift (a UI-only preset), and stamped-filename drift all ship with
/// every test green, because the real call sites were private on a MonoGame
/// Game class no test instantiates. The UiSessionReplay test now drives THIS
/// code end-to-end and replays its log hash-for-hash.
/// </summary>
public sealed class UiSession
{
    public WorldState World { get; private set; }
    public OrderLog Orders { get; }
    private readonly TurnExecutor _executor;

    // T2.9 chronicle-lite: names + event detection + annal prose. UI-side
    // history in the D-028 sense — replay through the same UiSession rebuilds
    // it; it never touches WorldState.
    private readonly Sim.Core.Chronicle.ChronicleConfig _chronicleCfg;
    private readonly Sim.Core.Chronicle.ChronicleCollector _chronicle;
    private readonly List<string> _annals = [];
    private int _renderedEvents;

    /// <summary>Settlement id → name (deterministic from world seed; ADR-001
    /// registry, never sim rows).</summary>
    public Sim.Core.Chronicle.NameRegistry Names { get; }

    /// <summary>The annals, oldest first (the panel renders newest LAST).</summary>
    public IReadOnlyList<string> AnnalLines => _annals;

    // THE SESSION RECORD (T4.17). The seed and overrides are kept because a
    // session log without them is UNREPLAYABLE — the world cannot be rebuilt,
    // so nothing downstream of it can run. They are written to the manifest.
    private readonly ulong _seed;
    private readonly int? _sizePx;
    private readonly int? _settlements;
    private readonly int _grainGoodId;
    private readonly List<string> _trace = [SessionTrace.Header];

    // T4.19 — THE GLASS BOX. One TurnRecord + SettlementRecord[] per End Turn,
    // built from (prev, next, cfg, ordersApplied) after the step. The log holds
    // records only — never a world — and nothing in the executor or any system
    // can reach it, so it cannot change a hash; that is also MEASURED, not
    // assumed (TelemetryTests: hash equality with and without a log).
    private readonly SimConfig _simCfg;

    /// <summary>The observation history the UI graphs and panels read through
    /// (docs/observability-architecture.md §7: one read seam).</summary>
    public IObservationHistory Observations => _observations;
    private readonly ObservationLog _observations = new();

    /// <summary>T4.19 lane B: the world the last End Turn STEPPED FROM, or null
    /// before the first. The explain queries are pure functions of (prev, next,
    /// cfg) — GrievanceExplanation, CausalChain, MigrationExplanation — and the
    /// observation log deliberately retains no world (§7), so the session holds
    /// the one previous world the UI needs. Safe to hold: TurnExecutor.Step
    /// never mutates prev and returns a fresh clone, so this reference is the
    /// same immutable pair the observer read. Nothing in the executor or any
    /// system can reach it.</summary>
    public WorldState? PreviousWorld { get; private set; }

    /// <summary>The loaded SimConfig — the same object the observer and the
    /// executor recipe load from the data files, exposed so the explain
    /// queries the UI runs read the registry the simulation ran with.</summary>
    public SimConfig Config => _simCfg;

    /// <summary>The live turn trace, one line per observed turn beneath the
    /// header — what the world looked like as it was actually played.</summary>
    public IReadOnlyList<string> TraceLines => _trace;

    /// <summary>The last turn this session reached — what a replay of its log
    /// must be run for.</summary>
    public long TurnsPlayed => World.Clock.Turn;

    /// <summary>T2.10, D-028: the graphs' ring buffer — UI-side history,
    /// captured per observed turn alongside the chronicle.</summary>
    public ViewModel.HistoryBuffer History { get; } = new();

    private UiSession(
        WorldState world, TurnExecutor executor, OrderLog orders,
        ulong seed, int? sizePx, int? settlements, SimConfig simCfg)
    {
        World = world;
        _executor = executor;
        Orders = orders;
        _seed = seed;
        _sizePx = sizePx;
        _settlements = settlements;
        _simCfg = simCfg;
        _grainGoodId = simCfg.Goods?.GrainId ?? 0;
        using (var stream = Sim.Data.DataFiles.OpenChronicle())
        {
            _chronicleCfg = Sim.Core.Chronicle.ChronicleConfigLoader.Load(stream);
        }
        Names = Sim.Core.Chronicle.NameRegistry.Build(_chronicleCfg, world.Seed, world);
        _chronicle = new Sim.Core.Chronicle.ChronicleCollector(_chronicleCfg);
        ObserveChronicle(); // founding events fire on first sight
        History.Capture(World); // the founding sample (turn 0)
        CaptureTrace();         // turn 0 — the world before any order lands
    }

    private void ObserveChronicle()
    {
        _chronicle.Observe(World);
        for (; _renderedEvents < _chronicle.Events.Count; _renderedEvents++)
            _annals.Add(Sim.Core.Chronicle.ChronicleProse.Render(
                _chronicle.Events[_renderedEvents], _chronicleCfg, Names));
    }

    /// <summary>Founds the world and builds the PRODUCTION executor + a fresh log.</summary>
    public static UiSession Start(
        ulong seed, int? sizeOverridePx = null, int? settlementsOverride = null)
    {
        var orders = new OrderLog();
        SimConfig simCfg;
        using (var stream = Sim.Data.DataFiles.OpenSim())
        using (var needs = Sim.Data.DataFiles.OpenNeeds())
        using (var goods = Sim.Data.DataFiles.OpenGoods())
        {
            simCfg = SimConfigLoader.Load(stream, needs, goods);
        }
        return new UiSession(
            UiFounding.Found(seed, sizeOverridePx, settlementsOverride),
            BuildProductionExecutor(orders), orders,
            seed, sizeOverridePx, settlementsOverride, simCfg);
    }

    /// <summary>
    /// The UI's executor recipe — canonical era + PRODUCTION pipeline preset +
    /// full system catalog. Public so the replay-equivalence test pins it; any
    /// UI-only preset/era drift breaks that test, not a played session.
    /// </summary>
    public static TurnExecutor BuildProductionExecutor(OrderLog orders)
        => new(ProductionEra(), ProductionPipeline(), orders);

    /// <summary>The era table the production executor is built from. Split out
    /// of BuildProductionExecutor so the forensic record can name the dt
    /// schedule the run actually used without a second loading recipe: the
    /// executor and the record read the SAME function.</summary>
    public static EraTable ProductionEra()
    {
        using var stream = Sim.Data.DataFiles.OpenEraPacing();
        return EraTableLoader.Load(stream);
    }

    /// <summary>The production pipeline, IN ORDER. The system execution order is
    /// data loaded at startup and is persisted in no existing artifact, although
    /// every value in every other artifact depends on it — so the forensic run
    /// record reads it from here, the same array the executor is constructed
    /// with.</summary>
    public static SystemRegistration[] ProductionPipeline()
    {
        SimConfig simCfg;
        using (var stream = Sim.Data.DataFiles.OpenSim())
        using (var needs = Sim.Data.DataFiles.OpenNeeds())
        using (var goods = Sim.Data.DataFiles.OpenGoods())
        {
            simCfg = SimConfigLoader.Load(stream, needs, goods);
        }
        using var pipe = Sim.Data.DataFiles.OpenPipeline();
        using var wgStream = Sim.Data.DataFiles.OpenWorldgen();
        return PipelineLoader.Load(pipe, SystemCatalog.All(
            simCfg, Sim.Core.Worldgen.WorldgenConfigLoader.Load(wgStream)));
    }

    /// <summary>The worldgen configuration the production world is founded from
    /// — read by the forensic record for the AI-empire count, which is reported
    /// explicitly including when it is zero.</summary>
    public static Sim.Core.Worldgen.WorldgenConfig ProductionWorldgen()
    {
        using var wgStream = Sim.Data.DataFiles.OpenWorldgen();
        return Sim.Core.Worldgen.WorldgenConfigLoader.Load(wgStream);
    }

    /// <summary>The HUD slider's release handler: ONE order, stamped with the
    /// CURRENT turn (§3.9: delivered to the very next End Turn step), targeting
    /// the FIRST settlement — the pre-selection shorthand the T1.9 replay
    /// tests pin; the selection HUD calls the targeted overload below.</summary>
    public void EmitLaborOrder(int farmPct)
    {
        if (World.Settlements.Count == 0) return;
        EmitLaborOrder(farmPct, World.Settlements[0].Id.Value);
    }

    /// <summary>T2.4: the targeted form — the slider orders the SELECTED
    /// settlement. An id not present in the world emits NOTHING (an order for
    /// a ghost settlement would poison the log at replay validation).</summary>
    public void EmitLaborOrder(int farmPct, int settlementId)
    {
        for (int i = 0; i < World.Settlements.Count; i++)
        {
            if (World.Settlements[i].Id.Value == settlementId)
            {
                Orders.Append(LaborOrderFactory.Create(
                    World.Clock.Turn, World.Settlements[i].Id, farmPct));
                return;
            }
        }
    }

    /// <summary>
    /// T3.9b: the five-sector control's submit handler — a BATCH of five
    /// SectorAllocation orders (D-032, the order kind T3.3 already shipped),
    /// all stamped with the CURRENT turn, targeting the SELECTED settlement.
    /// Same ghost-id rule as the labor order: an id not present in the world
    /// emits NOTHING, because an order for a settlement that does not exist
    /// poisons the log at replay validation.
    /// Returns true when the batch was appended, so the caller can leave the
    /// widget alone on refusal rather than pretending the order landed.
    /// </summary>
    public bool EmitSectorOrders(ReadOnlySpan<int> weights, int settlementId)
    {
        if (!SectorOrderFactory.CanSubmit(weights)) return false;
        for (int i = 0; i < World.Settlements.Count; i++)
        {
            if (World.Settlements[i].Id.Value != settlementId) continue;
            IReadOnlyList<OrderRecord> batch = SectorOrderFactory.Create(
                World.Clock.Turn, World.Settlements[i].Id, weights);
            for (int b = 0; b < batch.Count; b++) Orders.Append(batch[b]);
            return true;
        }
        return false;
    }

    /// <summary>End Turn: the executor steps synchronously (m1 spec §3);
    /// the chronicle observes the new state (detection is read-only).
    ///
    /// T4.19: the observation log is fed the (prev, next) pair AFTER the step.
    /// It runs before the chronicle here, and the order is immaterial: both are
    /// read-only over the same two worlds and neither reads the other, so
    /// swapping them changes no record and no hash. The orders handed to it
    /// are exactly the batch the executor delivered — the log rows whose Turn
    /// == prev.Clock.Turn (TurnExecutor.Step's BatchFor) — with their log
    /// indices, so a policy change carries its order number.</summary>
    public void EndTurn()
    {
        WorldState prev = World;
        World = _executor.Step(prev);
        PreviousWorld = prev;
        _observations.Observe(prev, World, _simCfg, OrderApplied.For(Orders, prev.Clock.Turn));
        ObserveChronicle();
        History.Capture(World);
        CaptureTrace();
    }

    /// <summary>One trace line for the world as it now stands. Called AFTER the
    /// step, like every other observer here.</summary>
    private void CaptureTrace() => _trace.Add(SessionTrace.Line(World, _grainGoodId));

    /// <summary>The chronicle.txt path twinned with a session log path:
    /// same stamp, `chronicle-` prefix, `.txt`.</summary>
    public static string ChroniclePath(string sessionLogPath) =>
        Path.Combine(Path.GetDirectoryName(sessionLogPath) ?? "",
            Path.GetFileNameWithoutExtension(sessionLogPath)
                .Replace("orders-", "chronicle-") + ".txt");

    /// <summary>The trace path twinned with a session log path: same stamp,
    /// `trace-` prefix, `.csv`.</summary>
    public static string TracePath(string sessionLogPath) =>
        Path.Combine(Path.GetDirectoryName(sessionLogPath) ?? "",
            Path.GetFileNameWithoutExtension(sessionLogPath)
                .Replace("orders-", "trace-") + ".csv");

    /// <summary>T4.19: the telemetry path twinned with a session log path: same
    /// stamp, `telemetry-` prefix, `.jsonl`.</summary>
    public static string TelemetryPath(string sessionLogPath) =>
        Path.Combine(Path.GetDirectoryName(sessionLogPath) ?? "",
            Path.GetFileNameWithoutExtension(sessionLogPath)
                .Replace("orders-", "telemetry-") + ".jsonl");

    /// <summary>m4-forensic P1: the forensic record path twinned with a session
    /// log path: same stamp, `forensic-` prefix, `.jsonl`.</summary>
    public static string ForensicPath(string sessionLogPath) =>
        Path.Combine(Path.GetDirectoryName(sessionLogPath) ?? "",
            Path.GetFileNameWithoutExtension(sessionLogPath)
                .Replace("orders-", "forensic-") + ".jsonl");

    /// <summary>The manifest path twinned with a session log path: same stamp,
    /// `session-` prefix, `.json`.</summary>
    public static string ManifestPath(string sessionLogPath) =>
        Path.Combine(Path.GetDirectoryName(sessionLogPath) ?? "",
            Path.GetFileNameWithoutExtension(sessionLogPath)
                .Replace("orders-", "session-") + ".json");

    /// <summary>
    /// The reproduction contract for this session: the seed and overrides the
    /// world was built from, and the identity of the build that ran it. Written
    /// ONCE at launch — before a turn is played — because its whole job is to
    /// survive a session that ends in a crash.
    ///
    /// ADR-022 (CR-013 ruling): the manifest also records the PLATFORM that
    /// played the session — the .NET runtime identifier, read HERE because
    /// Sim.Ui may interrogate the runtime (ADR-009) and Sim.Core may not. A
    /// session played off the reference platform (Linux x64) is expected to
    /// diverge from a reference replay at the hash level from turn 2 (CR-013
    /// §8), and `sim inspect` can only say so if the trace's origin is on
    /// record; without it the director's real Windows session read as
    /// "REPRODUCTION FAILED", a determinism finding it was not.
    /// </summary>
    public SessionManifest Manifest(string startedAt, string sessionLogPath) =>
        new(Seed: _seed,
            SizePx: _sizePx,
            Settlements: _settlements,
            SchemaVersion: CanonicalSchema.Version,
            BuildSha: BuildInfo.Sha,
            BuildDate: BuildInfo.Date,
            StartedAt: startedAt,
            OrdersFile: Path.GetFileName(sessionLogPath),
            ChronicleFile: Path.GetFileName(ChroniclePath(sessionLogPath)),
            TraceFile: Path.GetFileName(TracePath(sessionLogPath)),
            TelemetryFile: Path.GetFileName(TelemetryPath(sessionLogPath)),
            Platform: System.Runtime.InteropServices.RuntimeInformation.RuntimeIdentifier,
            ForensicFile: Path.GetFileName(ForensicPath(sessionLogPath)));

    /// <summary>Writes the manifest beside the order log.</summary>
    public void ExportManifest(string startedAt, string sessionLogPath)
    {
        string path = ManifestPath(sessionLogPath);
        Directory.CreateDirectory(Path.GetDirectoryName(path)!);
        using FileStream file = File.Create(path);
        Manifest(startedAt, sessionLogPath).Write(file);
    }

    /// <summary>Exports the turn trace — header plus one line per observed
    /// turn, fixed \n like the chronicle.</summary>
    public void ExportTrace(string path)
    {
        Directory.CreateDirectory(Path.GetDirectoryName(path)!);
        File.WriteAllText(path, string.Join("\n", _trace) + "\n");
    }

    // --- P0: THE TELEMETRY WRITE PATH (m4-forensic, phase P0) ---------------
    // WHAT THIS REPLACED, and why it was a defect. ExportTelemetry used to be
    // File.Create + TelemetryWriter.WriteAll — it truncated the file and
    // re-serialized EVERY accumulated turn on EVERY End Turn. The bytes written
    // over an N-turn session were therefore O(N^2) in the record size, and the
    // last End Turn of a 650-turn session synchronously re-serialized the whole
    // artifact. It also made the window in which the file is INVALID as long as
    // the whole file: File.Create truncates first, so a process death mid-save
    // left a file missing every turn already played, not just the last one.
    //
    // THE FIX IS APPEND, and the shape of telemetry/v2 makes it exact: the file
    // is JSONL with NO header line (the schema tag rides on every line —
    // TelemetryWriter.Schema is written inside WriteTurn), so an append is a
    // legal telemetry file by construction and nothing has to be written once.
    // Each record is written in full and FLUSHED before the next begins, so a
    // completed prior record is always intact on disk; a death mid-record can
    // truncate only the record being written, and every line before it still
    // parses (asserted in Sim.Ui.Tests: TelemetryAppendTests).
    //
    // NO SIMULATION STATE IS TOUCHED. This method reads _observations and
    // writes a file. The two counters below are OBSERVER accounting — the
    // write-amplification instrument the packet was asked to measure.
    private string? _telemetryPath;
    private int _telemetryRecordsWritten;
    private long _telemetryBytesWritten;

    /// <summary>TOTAL BYTES this session has written to the telemetry file over
    /// its whole life — the write-amplification instrument. Under the append
    /// path this converges on the file's own size; under the old rewrite path
    /// it was the sum of every intermediate file size.</summary>
    public long TelemetryBytesWritten => _telemetryBytesWritten;

    /// <summary>How many observation records are already on disk.</summary>
    public int TelemetryRecordsWritten => _telemetryRecordsWritten;

    /// <summary>
    /// T4.19/P0: exports the telemetry INCREMENTALLY — on each call only the
    /// records observed since the last call are appended, in observation order,
    /// each written whole and flushed before the next. The file on disk stays a
    /// complete prefix of the session at all times.
    ///
    /// A path this session has not written before (or a file that has since
    /// disappeared) is written from the start, so the method is still a
    /// complete export for any caller that asks for one.
    /// </summary>
    public void ExportTelemetry(string path) => ExportTelemetry(path, flushToDisk: false);

    /// <summary>
    /// The explicit FINALIZE on exit: appends whatever remains and forces the
    /// bytes past the OS buffers, so the artifact is durable when the process
    /// ends deliberately rather than merely handed to the OS.
    /// </summary>
    public void FinalizeTelemetry(string path) => ExportTelemetry(path, flushToDisk: true);

    private void ExportTelemetry(string path, bool flushToDisk)
    {
        Directory.CreateDirectory(Path.GetDirectoryName(path)!);

        // Resume only onto the file this session has been appending to. Any
        // other path — or a file that vanished under us — restarts from record
        // zero, because appending a suffix onto a file we did not write would
        // produce an artifact that is not this session.
        bool resume = _telemetryPath is not null
            && string.Equals(_telemetryPath, path, StringComparison.Ordinal)
            && File.Exists(path);
        if (!resume)
        {
            _telemetryPath = path;
            _telemetryRecordsWritten = 0;
        }

        IReadOnlyList<TurnObservation> all = _observations.Observations;
        if (resume && _telemetryRecordsWritten >= all.Count && !flushToDisk) return;

        using var file = new FileStream(
            path,
            resume ? FileMode.Append : FileMode.Create,
            FileAccess.Write, FileShare.Read);
        for (int i = _telemetryRecordsWritten; i < all.Count; i++)
        {
            long before = file.Position;
            TelemetryWriter.WriteTurn(file, all[i]);
            file.Flush();                       // the record is whole on disk
            _telemetryRecordsWritten = i + 1;   // ...and only then counted
            _telemetryBytesWritten += file.Position - before;
        }
        if (flushToDisk) file.Flush(flushToDisk: true);
    }

    // --- P1: IDENTITY AND PROVENANCE ---------------------------------------
    // The sixth file. Written by the SAME assembler the headless CLI uses
    // (ForensicSession), so a played run and a headless one cannot disagree
    // about the same world. Everything in it is a READ of what is already in
    // memory at launch, plus content digests; no simulation state is touched,
    // and the run id is derived from content, never from the wall clock.

    /// <summary>The run id of this session's forensic record, once written.</summary>
    public string? ForensicRunId { get; private set; }

    /// <summary>
    /// Builds the run record for this session. Wall clock is the CALLER'S, as on
    /// the manifest: Sim.Ui may read one (ADR-009) and hands it over.
    /// </summary>
    public Sim.Core.Observability.Forensic.ForensicRunRecord ForensicRun(string startedAt) =>
        Sim.Core.Observability.Forensic.ForensicSession.BuildRun(
            seed: _seed,
            sizePx: _sizePx,
            settlements: _settlements,
            founded: true,
            contentAssembly: typeof(Sim.Data.DataFiles).Assembly,
            orders: Orders,
            era: ProductionEra(),
            pipeline: ProductionPipeline(),
            aiEmpiresConfigured: ProductionWorldgen().AiEmpires,
            terrainContentHash: World.Terrain?.ContentHash,
            buildSha: BuildInfo.Sha,
            buildDate: BuildInfo.Date,
            platform: System.Runtime.InteropServices.RuntimeInformation.RuntimeIdentifier,
            startedAt: startedAt);

    /// <summary>Writes the forensic run record beside the order log, ONCE at
    /// launch — for the manifest's reason: a session that ends in a crash is
    /// still identified, because the identity is written before a turn is
    /// played.</summary>
    public void ExportForensicRun(string startedAt, string sessionLogPath)
    {
        ForensicRunId = Sim.Core.Observability.Forensic.ForensicSession.WriteRun(
            ForensicPath(sessionLogPath), ForensicRun(startedAt));
    }

    /// <summary>
    /// APPENDS the close record: how far the session got, the hash it itself
    /// computed for its final world, and the CONTENT hash of every companion
    /// artifact — so the set is bound by content and not by a shared filename
    /// stamp. A swapped same-named companion becomes a named finding instead of
    /// a reproduction failure that is not one.
    /// </summary>
    public void ExportForensicClose(string sessionLogPath)
    {
        if (ForensicRunId is not { } runId) return;   // no run record: nothing to close
        string dir = Path.GetDirectoryName(sessionLogPath) ?? "";
        // The final world hash is the one the TRACE already carries for this
        // turn — read from the line this session wrote, never recomputed here,
        // so the record cannot disagree with the trace about the same world.
        string[] last = _trace[^1].Split(',');
        Sim.Core.Observability.Forensic.ForensicSession.WriteClose(
            ForensicPath(sessionLogPath),
            new Sim.Core.Observability.Forensic.ForensicCloseRecord(
                RunId: runId,
                TurnsReached: World.Clock.Turn,
                FinalWorldHash: last[^1],
                FinalWorldHashState: "recorded — READ from this session's own trace line, not recomputed",
                Artifacts: Sim.Core.Observability.Forensic.ForensicSession.Companions(
                    dir,
                    ("manifest", Path.GetFileName(ManifestPath(sessionLogPath))),
                    ("orders", Path.GetFileName(sessionLogPath)),
                    ("trace", Path.GetFileName(TracePath(sessionLogPath))),
                    ("chronicle", Path.GetFileName(ChroniclePath(sessionLogPath))),
                    ("telemetry", Path.GetFileName(TelemetryPath(sessionLogPath)))),
                GatesState: "not-recorded — no gate-verdict producer exists in this build"));
    }

    /// <summary>Exports the annals — EXACTLY the panel's lines, one per line,
    /// fixed \n (byte-identical across identical runs).</summary>
    public void ExportChronicle(string path)
    {
        Directory.CreateDirectory(Path.GetDirectoryName(path)!);
        File.WriteAllText(path, string.Join("\n", _annals) + "\n");
    }

    /// <summary>
    /// Stamped session-log path (director UX ruling, T1.9): flat filename,
    /// lexicographic = chronological. A non-canonical size is recorded IN the
    /// name (…-s256.bin) so a preview session is never mistaken for a
    /// canonical-world log at replay time (`sim replay --founded --size PX`).
    /// </summary>
    public static string SessionLogPath(
        DateTime now, int? sizeOverridePx = null, int? settlementsOverride = null) =>
        Path.Combine("runs",
            "orders-" + now.ToString("yyyyMMdd-HHmmss", CultureInfo.InvariantCulture)
            + (sizeOverridePx is { } sz
                ? "-s" + sz.ToString(CultureInfo.InvariantCulture)
                : "")
            + (settlementsOverride is { } n
                ? "-n" + n.ToString(CultureInfo.InvariantCulture)
                : "")
            + ".bin");

    public void Save(string path)
    {
        Directory.CreateDirectory(Path.GetDirectoryName(path)!);
        using var stream = File.Create(path);
        Orders.Save(stream);
    }
}
