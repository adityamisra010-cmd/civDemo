using System.Text.Json;
using Sim.Core.Kernel;
using Sim.Core.Observability.Forensic;

namespace Sim.Tests.Forensic;

/// <summary>
/// P1 — IDENTITY AND PROVENANCE. The properties a reviewer's trust rests on:
/// the run id is derived from CONTENT and not from a clock, so it can be
/// recomputed and therefore checked; every value that could be absent is
/// explicitly null beside a state that says why; the limitation catalogue is
/// carried IN the artifact; and none of it moves the canonical schema (v25 since T4.21-1).
/// </summary>
public class ForensicIdentityTests
{
    private static readonly System.Reflection.Assembly Content = typeof(Sim.Data.DataFiles).Assembly;

    private static ForensicRunRecord Run(
        ulong seed = 42, int? sizePx = 256, int? settlements = 4, OrderLog? orders = null,
        string buildSha = "abc123", string? startedAt = "2026-09-16 10:00:00+00:00")
        => ForensicSession.BuildRun(
            seed, sizePx, settlements, founded: true,
            contentAssembly: Content,
            orders: orders ?? new OrderLog(),
            era: Sim.Cli.CliRecipes.Era(),
            pipeline: Sim.Cli.CliRecipes.ProductionPipeline(),
            aiEmpiresConfigured: Sim.Cli.CliRecipes.Worldgen().AiEmpires,
            terrainContentHash: null,
            buildSha: buildSha, buildDate: "2026-09-16", platform: "linux-x64",
            startedAt: startedAt);

    [Fact]
    public void TheRunIdIsContentDerived_NotAWallClock_AndIsTHEREFOREReproducible()
    {
        // THE DEFECT THIS CLOSES. The only identity a session had was the wall
        // clock stamp in its filenames: two identical sessions carried different
        // identities, and no identity could ever be RECOMPUTED, so nothing could
        // check it. Two builds of the same record, minutes apart, must agree.
        ForensicRunRecord a = Run(startedAt: "2026-09-16 10:00:00+00:00");
        ForensicRunRecord b = Run(startedAt: "2031-01-01 23:59:59-08:00");
        Assert.Equal(a.RunId, b.RunId);
        Assert.Equal(16, a.RunId.Length);
        Assert.All(a.RunId, c => Assert.True((c >= '0' && c <= '9') || (c >= 'a' && c <= 'f')));

        // ...and the build sha is excluded, because it is the literal "dev" on
        // every locally built binary and would collide every local session.
        Assert.Equal(a.RunId, Run(buildSha: "dev").RunId);
        Assert.Equal(a.RunId, Run(buildSha: "0123456789abcdef").RunId);
    }

    [Fact]
    public void TheRunIdMovesWithEVERYTHINGThatDefinesTheWorld()
    {
        string baseline = Run().RunId;
        Assert.NotEqual(baseline, Run(seed: 43).RunId);
        Assert.NotEqual(baseline, Run(sizePx: 512).RunId);
        Assert.NotEqual(baseline, Run(sizePx: null).RunId);
        Assert.NotEqual(baseline, Run(settlements: 5).RunId);
        Assert.NotEqual(baseline, Run(settlements: null).RunId);

        var ordered = new OrderLog();
        ordered.Append(new OrderRecord(0, 1, OrderKind.LaborAllocation, 0, 50.0));
        Assert.NotEqual(baseline, Run(orders: ordered).RunId);
    }

    [Fact]
    public void TheRunIdIsRECOMPUTABLEFromItsStatedInputs()
    {
        // The point of a content-derived id: a reviewer holding the artifact can
        // recompute it from the manifest, the orders file and this build's
        // configs. A mismatch is then a NAMED finding — "this is not the orders
        // log that session played" — instead of surfacing downstream as a
        // reproduction failure, which reads as a determinism defect and is not.
        ForensicRunRecord run = Run();
        ConfigResource[] config = ForensicIdentity.ConfigResources(Content);
        string recomputed = ForensicIdentity.RunId(
            42, 256, 4, founded: true,
            canonicalSchemaVersion: CanonicalSchema.Version,
            configDigest: ForensicIdentity.ConfigDigest(config),
            ordersDigest: ForensicIdentity.OrdersDigest(new OrderLog()));
        Assert.Equal(run.RunId, recomputed);
        Assert.Equal(run.ConfigDigest, ForensicIdentity.ConfigDigest(config));
    }

    [Fact]
    public void EveryEmbeddedConfigResourceIsRecordedByCONTENT_inAFixedOrder()
    {
        // The largest provenance hole in the shipped set: all nine content files
        // are compiled into Sim.Data and nothing recorded their bytes, so a
        // session played from a locally built binary (buildSha "dev") recorded
        // NO recoverable configuration at all.
        ConfigResource[] config = ForensicIdentity.ConfigResources(Content);
        Assert.Equal(9, config.Length);
        for (int i = 1; i < config.Length; i++)
            Assert.True(string.CompareOrdinal(config[i - 1].File, config[i].File) < 0,
                "config resources must be in ordinal name order, or the digest is not stable");
        foreach (ConfigResource c in config)
        {
            Assert.True(c.Bytes > 0);
            Assert.Equal(64, c.Sha256.Length);
        }
        Assert.Contains(config, c => c.File == "sim.json");
        Assert.Contains(config, c => c.File == "goods.json");
        Assert.Contains(config, c => c.File == "worldgen.json");
    }

    [Fact]
    public void ThePipelineORDERAndTheEraTableAreRecorded_becauseNoOtherArtifactHasThem()
    {
        ForensicRunRecord run = Run();
        Assert.NotEmpty(run.Pipeline);
        for (int i = 0; i < run.Pipeline.Length; i++) Assert.Equal(i, run.Pipeline[i].Position);
        Assert.Equal("catchment", run.Pipeline[0].Name);
        Assert.NotEmpty(run.EraBands);
        Assert.All(run.EraBands, b => Assert.True(b.DtDays > 0));
        Assert.Contains("clock advances LAST", run.DtRule);
    }

    [Fact]
    public void TheAiCountIsEmittedEXPLICITLY_includingWhenItIsZero()
    {
        // "There is no AI" is a recorded fact about the configuration, and a
        // different thing from an AI decision that went unrecorded. An omitted
        // field could not tell those apart.
        ForensicRunRecord run = Run();
        Assert.Equal(0, run.AiEmpiresConfigured);
        Assert.Contains("no AI actor", run.AiNote);
    }

    [Fact]
    public void TheHashIsNAMED_andTheRecordStatesThatItDoesNotSelfIdentifyItsSchema()
    {
        ForensicRunRecord run = Run();
        Assert.Equal("sha256/canonical-stream", run.HashAlgorithm);
        Assert.False(run.HashCoversSchemaVersion);
        Assert.Equal(25, run.CanonicalSchemaVersion);
        Assert.Equal(25, CanonicalSchema.Version);   // v25: T4.21-1's Disasters table (the forensic record carries it, never covers it)
        Assert.Equal(25, run.Schemas.CanonicalSchemaVersion);
        Assert.Equal("telemetry/v2", run.Schemas.Telemetry);
        Assert.Equal("session-manifest/v2", run.Schemas.SessionManifest);
        Assert.Equal("forensic/v1", run.Schemas.Forensic);
    }

    [Fact]
    public void NullsAreEXPLICIT_neverNaNStrings_neverSentinels_andAlwaysPairedWithAState()
    {
        using var buffer = new MemoryStream();
        ForensicWriter.WriteRun(buffer, Run(sizePx: null, settlements: null, startedAt: null));
        string line = System.Text.Encoding.UTF8.GetString(buffer.ToArray()).TrimEnd('\n');

        Assert.DoesNotContain("\"NaN\"", line);
        Assert.DoesNotContain("\"Infinity\"", line);
        Assert.DoesNotContain("\"-Infinity\"", line);

        using JsonDocument doc = JsonDocument.Parse(line);
        JsonElement root = doc.RootElement;
        foreach (string name in new[] { "sizePx", "settlementsOverride", "terrainContentHash", "startedAt" })
        {
            Assert.Equal(JsonValueKind.Null, root.GetProperty(name).ValueKind);
            Assert.False(string.IsNullOrWhiteSpace(root.GetProperty(name + "State").GetString()));
        }
        // -1 as an absence sentinel does not appear anywhere in this record.
        Assert.DoesNotContain(":-1,", line);
    }

    [Fact]
    public void TheLimitationCatalogueIsCarriedINTheArtifact_withTheMigrationAnswerVERBATIM()
    {
        // A reviewer's first question is what this evidence CANNOT establish.
        // Today he can only answer it by discovering silence.
        ForensicRunRecord run = Run();
        ForensicLimitation migration = Assert.Single(
            run.Limitations, l => l.Token == ForensicSchema.LimitMigrationPairwise);
        Assert.Equal(
            "UNAVAILABLE: pairwise migration destination was not persisted by this build.",
            migration.Answer);
        Assert.Contains("MigrationRemainder", migration.Why);

        ForensicLimitation happiness = Assert.Single(
            run.Limitations, l => l.Token == ForensicSchema.LimitHappinessDecomposition);
        Assert.Null(happiness.Answer);
        // The boundary is stated as file:line so it can be CHECKED, not believed.
        Assert.Contains("SettlementHappiness.cs", happiness.Why);
        Assert.Contains("private", happiness.Why);

        Assert.Contains(run.Limitations, l => l.Token == ForensicSchema.LimitStoreLosses);
        Assert.Contains(run.Limitations, l => l.Token == ForensicSchema.LimitNoAi);
        Assert.Contains(run.Limitations, l => l.Token == ForensicSchema.LimitNoGateProducer);
    }

    [Fact]
    public void TheRecordROUNDTRIPSThroughItsOwnReader()
    {
        ForensicRunRecord run = Run();
        var close = new ForensicCloseRecord(
            run.RunId, 12, "ab12", "recorded",
            [new ArtifactRef("trace", "trace-x.csv", 100, "ff", "recorded"),
             new ArtifactRef("chronicle", "chronicle-x.txt", null, null, "absent — not present at close")],
            "not-recorded — no gate-verdict producer exists in this build");

        using var buffer = new MemoryStream();
        ForensicWriter.WriteRun(buffer, run);
        ForensicWriter.WriteClose(buffer, close);
        string text = System.Text.Encoding.UTF8.GetString(buffer.ToArray());

        ForensicRecordFile read = ForensicRecordFile.Parse(
            text.Split('\n', StringSplitOptions.RemoveEmptyEntries), "test");
        Assert.Equal(run.RunId, read.RunId);
        Assert.Equal(run.Seed, read.Run.Seed);
        Assert.Equal(run.ConfigDigest, read.Run.ConfigDigest);
        Assert.Equal(run.Config.Length, read.Run.Config.Length);
        Assert.Equal(run.Pipeline.Length, read.Run.Pipeline.Length);
        Assert.Equal(run.EraBands.Length, read.Run.EraBands.Length);
        Assert.Equal(run.Limitations.Length, read.Run.Limitations.Length);
        Assert.Equal(25, read.Run.CanonicalSchemaVersion);   // v25: T4.21-1 Disasters table
        Assert.NotNull(read.Close);
        Assert.Equal(12, read.Close!.TurnsReached);
        Assert.Null(read.Close.Artifacts[1].Sha256);
        Assert.Equal(
            ForensicSchema.MigrationPairwiseAnswer,
            read.Limitation(ForensicSchema.LimitMigrationPairwise)!.Answer);
    }

    [Fact]
    public void AMissingCLOSELineIsREADABLE_andIsItselfTheEvidenceOfAnUncleanClose()
    {
        // No existing artifact in the set can express "this session did not
        // finish". The forensic file can, by the absence of one line — and the
        // reader must survive that absence rather than refusing the file.
        using var buffer = new MemoryStream();
        ForensicWriter.WriteRun(buffer, Run());
        ForensicRecordFile read = ForensicRecordFile.Parse(
            System.Text.Encoding.UTF8.GetString(buffer.ToArray())
                .Split('\n', StringSplitOptions.RemoveEmptyEntries), "test");
        Assert.Null(read.Close);
    }

    [Fact]
    public void AnUnknownVINTAGEIsRefusedLOUDLY_withTheFileNamed()
    {
        // The SessionManifest.Read discipline: a reader that silently accepts a
        // file it does not understand reports the difference as a finding.
        InvalidDataException e = Assert.Throws<InvalidDataException>(
            () => ForensicRecordFile.Parse(["""{"v":"forensic/v99","rec":"run"}"""], "wrong.jsonl"));
        Assert.Contains("wrong.jsonl", e.Message);
        Assert.Contains("forensic/v99", e.Message);

        // ...but an unknown KEY inside a known vintage is ignored, so a later
        // version that only adds fields stays readable.
        using var buffer = new MemoryStream();
        ForensicWriter.WriteRun(buffer, Run());
        string line = System.Text.Encoding.UTF8.GetString(buffer.ToArray()).TrimEnd('\n');
        string widened = line[..^1] + ",\"somethingNew\":123}";
        Assert.Equal(Run().RunId, ForensicRecordFile.Parse([widened], "test").RunId);
    }

    [Fact]
    public void TheManifestGainsAFORENSICREFERENCEWithoutMovingItsTag()
    {
        // S22: SessionManifest.Read is a tag WHITELIST that throws on an unknown
        // vintage, so a v3 manifest would be REJECTED by every binary already
        // built. The reference is therefore an ADDITIVE key inside v2.
        var m = new SessionManifest(
            42, 256, 4, CanonicalSchema.Version, "sha", "date", "started",
            "orders-x.bin", "chronicle-x.txt", "trace-x.csv", "telemetry-x.jsonl",
            "linux-x64", "forensic-x.jsonl");
        Assert.Equal("session-manifest/v2", SessionManifest.Schema);

        using var buffer = new MemoryStream();
        m.Write(buffer);
        buffer.Position = 0;
        SessionManifest read = SessionManifest.Read(buffer, "test");
        Assert.Equal("forensic-x.jsonl", read.ForensicFile);
        Assert.Equal(m, read);

        // A manifest written BEFORE this packet has no such key and still reads.
        string[] lines = System.Text.Encoding.UTF8.GetString(buffer.ToArray()).Split('\n');
        var kept = new List<string>();
        for (int i = 0; i < lines.Length; i++)
        {
            if (lines[i].Contains("forensicFile", StringComparison.Ordinal))
            {
                // drop the key, and the comma that now dangles on the line above
                kept[^1] = kept[^1].TrimEnd().TrimEnd(',');
                continue;
            }
            kept.Add(lines[i]);
        }
        using var old = new MemoryStream(System.Text.Encoding.UTF8.GetBytes(string.Join('\n', kept)));
        Assert.Equal("", SessionManifest.Read(old, "old").ForensicFile);
    }
}
