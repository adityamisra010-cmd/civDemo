using Sim.Core.Kernel;
using Sim.Core.Observability.Forensic;
using Sim.Core.State;

namespace Sim.Tests.Forensic;

/// <summary>
/// P1 — THE HEADLESS SESSION RECORD (`--emit-session`).
///
/// Before this packet a headless run wrote NONE of the five session files, and
/// `sim inspect` — the only verb that can read a session — REQUIRES a manifest
/// and takes its turn count from the trace beside it. A reproducible forensic
/// run was therefore impossible from the shipped CLI without hand-authoring
/// session metadata. These tests pin that the emitted set is complete, valid,
/// bound to itself by content, and IDENTICAL across two invocations — the last
/// being the property a wall-clock identity can never have.
/// </summary>
public class SessionEmitterTests
{
    private static string Emit(string tag, int turns, out string runId)
    {
        string dir = Path.Combine(Path.GetTempPath(), "civsim-p1-" + tag);
        if (Directory.Exists(dir)) Directory.Delete(dir, recursive: true);

        Sim.Core.Systems.SimConfig cfg;
        using (Stream sim = Sim.Data.DataFiles.OpenSim())
        using (Stream needs = Sim.Data.DataFiles.OpenNeeds())
        using (Stream goods = Sim.Data.DataFiles.OpenGoods())
        {
            cfg = Sim.Core.Systems.SimConfigLoader.Load(sim, needs, goods);
        }
        var orders = new OrderLog();
        WorldState world = Sim.Cli.HeadlessFounding.Found(42, 256, 4);
        var executor = new TurnExecutor(Sim.Cli.CliRecipes.Era(), Sim.Cli.CliRecipes.ProductionPipeline(), orders);

        using (var emitter = new Sim.Cli.SessionEmitter(
            dir, 42, 256, 4, orders, cfg, world, "testsha", "testdate", "linux-x64"))
        {
            for (int t = 1; t <= turns; t++)
            {
                WorldState prev = world;
                world = executor.Step(prev);
                emitter.Observe(prev, world);
            }
            emitter.Close();
            runId = emitter.RunId;
        }
        return dir;
    }

    [Fact]
    public void EVERYRunGetsAVALIDManifest_thatNamesTheForensicRecord()
    {
        string dir = Emit("manifest", 5, out string runId);
        try
        {
            string manifestPath = Path.Combine(dir, "session-" + runId + ".json");
            Assert.True(File.Exists(manifestPath));

            using FileStream file = File.OpenRead(manifestPath);
            SessionManifest m = SessionManifest.Read(file, manifestPath);
            Assert.Equal(42UL, m.Seed);
            Assert.Equal(256, m.SizePx);
            Assert.Equal(4, m.Settlements);
            Assert.Equal(CanonicalSchema.Version, m.SchemaVersion);
            Assert.Equal("forensic-" + runId + ".jsonl", m.ForensicFile);
            // The one manifest field this packet adds, named for the report.
            Assert.Equal("orders-" + runId + ".bin", m.OrdersFile);
            Assert.Equal("trace-" + runId + ".csv", m.TraceFile);
            Assert.Equal("telemetry-" + runId + ".jsonl", m.TelemetryFile);
            // And the whole set is on disk, where the manifest says it is.
            Assert.True(File.Exists(Path.Combine(dir, m.OrdersFile)));
            Assert.True(File.Exists(Path.Combine(dir, m.TraceFile)));
            Assert.True(File.Exists(Path.Combine(dir, m.TelemetryFile)));
            Assert.True(File.Exists(Path.Combine(dir, m.ForensicFile)));
        }
        finally { Directory.Delete(dir, recursive: true); }
    }

    [Fact]
    public void TheCloseRecordBindsEveryCompanionByCONTENT_andSaysSoWhenOneIsABSENT()
    {
        // G16: the manifest names its companions by FILENAME only, so a swapped
        // same-named file is undetectable and surfaces downstream as
        // "REPRODUCTION FAILED" — a determinism verdict on a provenance defect.
        string dir = Emit("binding", 4, out string runId);
        try
        {
            ForensicRecordFile record = ForensicRecordFile.Read(Path.Combine(dir, "forensic-" + runId + ".jsonl"));
            Assert.NotNull(record.Close);
            ForensicCloseRecord close = record.Close!;
            Assert.Equal(runId, close.RunId);
            Assert.Equal(4, close.TurnsReached);

            foreach (ArtifactRef a in close.Artifacts)
            {
                string full = Path.Combine(dir, a.File);
                if (a.Role == "chronicle")
                {
                    // Not written by a headless run: recorded as ABSENT with
                    // explicit nulls, never as a zero-byte artifact.
                    Assert.Null(a.Sha256);
                    Assert.Null(a.Bytes);
                    Assert.Contains("absent", a.State);
                    continue;
                }
                Assert.Equal("recorded", a.State);
                Assert.Equal(new FileInfo(full).Length, a.Bytes);
                Assert.Equal(ForensicIdentity.Sha256HexOfFile(full), a.Sha256);
            }
        }
        finally { Directory.Delete(dir, recursive: true); }
    }

    [Fact]
    public void TheFinalHashOnTheCloseRecordIsTheTRACEsOwnHashForThatTurn()
    {
        // The forensic record must not be able to disagree with the trace about
        // the same world: the hash is READ from the session's own trace line.
        string dir = Emit("hash", 6, out string runId);
        try
        {
            IReadOnlyList<SessionTrace.Row> rows = SessionTrace.Parse(
                File.ReadLines(Path.Combine(dir, "trace-" + runId + ".csv")), "trace");
            ForensicRecordFile record = ForensicRecordFile.Read(Path.Combine(dir, "forensic-" + runId + ".jsonl"));
            Assert.Equal(6, rows[^1].Turn);
            Assert.Equal(rows[^1].Hash, record.Close!.FinalWorldHash);
            Assert.Equal(rows[^1].Turn, record.Close.TurnsReached);
            // turn 0 included in the trace, as a played session records it
            Assert.Equal(7, rows.Count);
        }
        finally { Directory.Delete(dir, recursive: true); }
    }

    [Fact]
    public void TWOIdenticalHeadlessRunsProduceTheSAMEIdentityAndBYTEIDENTICALArtifacts()
    {
        // THE PROPERTY A WALL-CLOCK IDENTITY CANNOT HAVE. Two runs of the same
        // world with the same log are the same run, and the record says so.
        string a = Emit("determinism-a", 5, out string runA);
        string b = Emit("determinism-b", 5, out string runB);
        try
        {
            Assert.Equal(runA, runB);
            foreach (string kind in new[] { "forensic-{0}.jsonl", "trace-{0}.csv", "telemetry-{0}.jsonl", "session-{0}.json" })
            {
                string name = string.Format(System.Globalization.CultureInfo.InvariantCulture, kind, runA);
                Assert.Equal(
                    File.ReadAllBytes(Path.Combine(a, name)),
                    File.ReadAllBytes(Path.Combine(b, name)));
            }
        }
        finally
        {
            Directory.Delete(a, recursive: true);
            Directory.Delete(b, recursive: true);
        }
    }

    [Fact]
    public void TheTelemetryIsAPPENDED_oneLinePerTurn_andTheTraceCarriesTurnZero()
    {
        string dir = Emit("shape", 7, out string runId);
        try
        {
            Assert.Equal(7, File.ReadAllLines(Path.Combine(dir, "telemetry-" + runId + ".jsonl")).Length);
            // header + turn 0 + 7 played turns
            Assert.Equal(9, File.ReadAllLines(Path.Combine(dir, "trace-" + runId + ".csv")).Length);
        }
        finally { Directory.Delete(dir, recursive: true); }
    }
}
