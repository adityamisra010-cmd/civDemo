using System.Globalization;
using Sim.Core.Kernel;
using Sim.Core.Observability;
using Sim.Core.Observability.Forensic;
using Sim.Core.State;

namespace Sim.Cli;

/// <summary>
/// --emit-session: A HEADLESS SESSION RECORD.
///
/// THE HOLE THIS CLOSES. A played session (Sim.Ui) writes five files; a headless
/// CLI run wrote NONE of them. `--telemetry` existed only on `sim inspect`,
/// which REQUIRES a manifest and takes its turn count from the trace beside it —
/// so producing any of this headlessly meant hand-authoring session metadata by
/// hand first. A reproducible forensic run, the exact thing an external reviewer
/// needs, was not possible from the shipped CLI at all.
///
/// NO SECOND IMPLEMENTATION. Every file here is written by the SAME writer the
/// played session uses — SessionManifest.Write, SessionTrace.Line,
/// OrderLog.Save, TelemetryWriter.WriteTurn, ForensicSession — so a headless
/// record and a played one cannot disagree about the same world.
///
/// NAMED BY RUN ID, NOT BY A CLOCK. Sim.Cli is sim code and may not read a wall
/// clock (the banned-constructs gate enforces it), and a wall-clock name would
/// be the wrong choice here anyway: the run id is content-derived, so two
/// invocations with the same seed, overrides and order log produce the same
/// filenames, and a set that should be identical IS identical.
///
/// STRICTLY AN OBSERVER. It is handed each post-step world and reads it. The
/// Step call is byte-identical whether or not a session is being emitted.
/// </summary>
public sealed class SessionEmitter : IDisposable
{
    private readonly string _dir;
    private readonly string _stem;
    private readonly int _grain;
    private readonly Sim.Core.Systems.SimConfig _cfg;
    private readonly OrderLog _orders;
    private readonly ObservationLog _log = new();
    private readonly List<string> _trace = [SessionTrace.Header];
    private FileStream? _telemetry;
    private long _turns;
    private string _finalHash = "";

    public string RunId { get; }

    public SessionEmitter(
        string directory, ulong seed, int? sizePx, int? settlements,
        OrderLog orders, Sim.Core.Systems.SimConfig cfg, WorldState start,
        string buildSha, string buildDate, string platform)
    {
        _dir = directory;
        _orders = orders;
        _cfg = cfg;
        _grain = cfg.Goods?.GrainId ?? 0;
        Directory.CreateDirectory(_dir);

        ForensicRunRecord run = ForensicSession.BuildRun(
            seed: seed,
            sizePx: sizePx,
            settlements: settlements,
            founded: true,
            contentAssembly: typeof(Sim.Data.DataFiles).Assembly,
            orders: orders,
            era: CliRecipes.Era(),
            pipeline: CliRecipes.ProductionPipeline(),
            aiEmpiresConfigured: CliRecipes.Worldgen().AiEmpires,
            terrainContentHash: start.Terrain?.ContentHash,
            buildSha: buildSha,
            buildDate: buildDate,
            platform: platform,
            startedAt: null);              // no wall clock here, and the record says so
        RunId = run.RunId;
        _stem = RunId;

        // The identity goes down FIRST, before a turn is played, for the
        // manifest's own reason: a run that dies mid-way is still identified.
        ForensicSession.WriteRun(Path.Combine(_dir, "forensic-" + _stem + ".jsonl"), run);

        var manifest = new SessionManifest(
            Seed: seed,
            SizePx: sizePx,
            Settlements: settlements,
            SchemaVersion: CanonicalSchema.Version,
            BuildSha: buildSha,
            BuildDate: buildDate,
            StartedAt: NoWallClock,
            OrdersFile: "orders-" + _stem + ".bin",
            ChronicleFile: "chronicle-" + _stem + ".txt",
            TraceFile: "trace-" + _stem + ".csv",
            TelemetryFile: "telemetry-" + _stem + ".jsonl",
            Platform: platform,
            ForensicFile: "forensic-" + _stem + ".jsonl");
        using (FileStream file = File.Create(Path.Combine(_dir, "session-" + _stem + ".json")))
            manifest.Write(file);

        using (FileStream file = File.Create(Path.Combine(_dir, manifest.OrdersFile)))
            orders.Save(file);

        // Turn 0: the world before any order lands, exactly as a played session
        // records it (the trace includes turn 0; the telemetry does not, because
        // there is no step to observe yet).
        Capture(start);
    }

    /// <summary>What a headless manifest records where a played one records a
    /// stamp. Stated, never invented.</summary>
    public const string NoWallClock = "not recorded (headless run: Sim.Cli reads no wall clock)";

    /// <summary>Observe one completed step: the trace line, then the telemetry
    /// record, appended — never rewritten (P0).</summary>
    public void Observe(WorldState prev, WorldState next)
    {
        TurnObservation observation = _log.Observe(prev, next, _cfg, OrderApplied.For(_orders, prev.Clock.Turn));
        _telemetry ??= new FileStream(
            Path.Combine(_dir, "telemetry-" + _stem + ".jsonl"),
            FileMode.Create, FileAccess.Write, FileShare.Read);
        TelemetryWriter.WriteTurn(_telemetry, observation);
        _telemetry.Flush();
        Capture(next);
    }

    private void Capture(WorldState world)
    {
        _trace.Add(SessionTrace.Line(world, _grain));
        _turns = world.Clock.Turn;
        _finalHash = WorldHash.ComputeHex(world);
    }

    /// <summary>Finishes the set: the trace, then the forensic CLOSE record,
    /// which content-hashes every companion and therefore must be written last.</summary>
    public void Close()
    {
        File.WriteAllText(Path.Combine(_dir, "trace-" + _stem + ".csv"), string.Join("\n", _trace) + "\n");
        _telemetry?.Flush(flushToDisk: true);
        _telemetry?.Dispose();
        _telemetry = null;

        ForensicSession.WriteClose(
            Path.Combine(_dir, "forensic-" + _stem + ".jsonl"),
            new ForensicCloseRecord(
                RunId: RunId,
                TurnsReached: _turns,
                FinalWorldHash: _finalHash,
                FinalWorldHashState: "recorded",
                Artifacts: ForensicSession.Companions(
                    _dir,
                    ("manifest", "session-" + _stem + ".json"),
                    ("orders", "orders-" + _stem + ".bin"),
                    ("trace", "trace-" + _stem + ".csv"),
                    ("chronicle", "chronicle-" + _stem + ".txt"),
                    ("telemetry", "telemetry-" + _stem + ".jsonl")),
                GatesState: "not-recorded — no gate-verdict producer exists in this build"));
    }

    /// <summary>The manifest a reader should be pointed at.</summary>
    public string ManifestPath => Path.Combine(_dir, "session-" + _stem + ".json");

    public void Dispose()
    {
        _telemetry?.Dispose();
        _telemetry = null;
    }
}
