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
    {
        EraTable era;
        using (var stream = Sim.Data.DataFiles.OpenEraPacing())
        {
            era = EraTableLoader.Load(stream);
        }
        SimConfig simCfg;
        using (var stream = Sim.Data.DataFiles.OpenSim())
        using (var needs = Sim.Data.DataFiles.OpenNeeds())
        using (var goods = Sim.Data.DataFiles.OpenGoods())
        {
            simCfg = SimConfigLoader.Load(stream, needs, goods);
        }
        SystemRegistration[] pipeline;
        using (var stream = Sim.Data.DataFiles.OpenPipeline())
        {
            using var wgStream = Sim.Data.DataFiles.OpenWorldgen();
            pipeline = PipelineLoader.Load(stream, SystemCatalog.All(
                simCfg, Sim.Core.Worldgen.WorldgenConfigLoader.Load(wgStream)));
        }
        return new TurnExecutor(era, pipeline, orders);
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
            Platform: System.Runtime.InteropServices.RuntimeInformation.RuntimeIdentifier);

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

    /// <summary>T4.19: exports the telemetry — one JSONL line per observed turn,
    /// rewritten in full from the in-memory log on every save (like the trace),
    /// so the file on disk is always a complete prefix of the session and a crash
    /// mid-write loses at most the last End Turn. Byte-identical across two runs
    /// of the same session (TelemetryWriter, asserted).</summary>
    public void ExportTelemetry(string path)
    {
        Directory.CreateDirectory(Path.GetDirectoryName(path)!);
        using FileStream file = File.Create(path);
        TelemetryWriter.WriteAll(file, _observations);
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
