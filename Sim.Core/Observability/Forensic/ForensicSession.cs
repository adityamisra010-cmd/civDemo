using System.Reflection;
using Sim.Core.Kernel;

namespace Sim.Core.Observability.Forensic;

/// <summary>
/// THE ONE ASSEMBLER of the forensic artifact, shared by the played session
/// (Sim.Ui) and the headless one (Sim.Cli). There is deliberately no second
/// implementation: a headless forensic run that built its own record would be
/// free to disagree with a played one about the same world, which is exactly the
/// class of defect this layer exists to remove.
///
/// EVERYTHING HERE IS A READ of information already in memory at launch — the
/// seed and overrides the caller founded with, the configuration resources
/// compiled into the content assembly, the pipeline the executor was built from,
/// the era table, the worldgen config, the order log, and the terrain's own
/// content hash off the world. No simulation state is written, no formula is
/// recomputed, and nothing here can be reached from the executor or a system.
///
/// THE WALL CLOCK IS THE CALLER'S, as on <see cref="SessionManifest"/>: this
/// assembly reads no clock. Sim.Ui hands one over (legal there, ADR-009);
/// Sim.Cli may not read one at all, so a headless run records the absence
/// explicitly rather than inventing a stamp.
/// </summary>
public static class ForensicSession
{
    /// <summary>The local build's fallback build sha, which is not an identity.</summary>
    public const string DevBuildSha = "dev";

    /// <summary>What a headless run records where a played run records a wall
    /// clock: the absence, with its reason, never a manufactured stamp.</summary>
    public const string NoWallClockState =
        "not-recorded — Sim.Cli is sim code and may not read a wall clock (banned-constructs gate); "
        + "this run's identity is the content-derived runId, which needs no timestamp.";

    /// <summary>
    /// Builds the run record. Pure: same inputs, same record, on any machine.
    /// </summary>
    public static ForensicRunRecord BuildRun(
        ulong seed,
        int? sizePx,
        int? settlements,
        bool founded,
        Assembly contentAssembly,
        OrderLog orders,
        EraTable era,
        SystemRegistration[] pipeline,
        int aiEmpiresConfigured,
        byte[]? terrainContentHash,
        string buildSha,
        string buildDate,
        string platform,
        string? startedAt)
    {
        ArgumentNullException.ThrowIfNull(contentAssembly);
        ArgumentNullException.ThrowIfNull(orders);
        ArgumentNullException.ThrowIfNull(era);
        ArgumentNullException.ThrowIfNull(pipeline);

        ConfigResource[] config = ForensicIdentity.ConfigResources(contentAssembly);
        string configDigest = ForensicIdentity.ConfigDigest(config);
        string ordersDigest = ForensicIdentity.OrdersDigest(orders);
        string runId = ForensicIdentity.RunId(
            seed, sizePx, settlements, founded, CanonicalSchema.Version, configDigest, ordersDigest);

        var entries = new PipelineEntry[pipeline.Length];
        for (int i = 0; i < pipeline.Length; i++)
            entries[i] = new PipelineEntry(i, pipeline[i].Id.Value, pipeline[i].Name);

        ReadOnlySpan<EraTable.Band> bands = era.Bands;
        var eraBands = new EraBandRecord[bands.Length];
        for (int i = 0; i < bands.Length; i++)
            eraBands[i] = new EraBandRecord(bands[i].Name, bands[i].StartDay, bands[i].EndDay, bands[i].DtDays);

        return new ForensicRunRecord(
            RunId: runId,
            RunIdInputs: ForensicIdentity.RunIdInputs,
            Seed: seed,
            SizePx: sizePx,
            SettlementsOverride: settlements,
            Founded: founded,
            CanonicalSchemaVersion: CanonicalSchema.Version,
            HashAlgorithm: ForensicSchema.HashAlgorithm,
            HashCoversSchemaVersion: ForensicSchema.HashCoversSchemaVersion,
            BuildSha: buildSha,
            BuildDate: buildDate,
            BuildShaRecorded: buildSha.Length > 0 && buildSha != DevBuildSha,
            Platform: platform,
            Config: config,
            ConfigDigest: configDigest,
            OrdersDigest: ordersDigest,
            Pipeline: entries,
            EraBands: eraBands,
            DtRule: ForensicSchema.DtRule,
            RngDerivation: ForensicSchema.RngDerivation,
            AiEmpiresConfigured: aiEmpiresConfigured,
            AiNote: AiNote(aiEmpiresConfigured),
            TerrainContentHash: terrainContentHash is { Length: > 0 }
                ? Convert.ToHexStringLower(terrainContentHash)
                : null,
            TerrainState: terrainContentHash is { Length: > 0 }
                ? "recorded"
                : "terrain-absent — this world carries no TerrainSet (a toy world), so there is no terrain "
                    + "content hash to record.",
            Schemas: new ArtifactSchemas(
                CanonicalSchemaVersion: CanonicalSchema.Version,
                SessionManifest: SessionManifest.Schema,
                Telemetry: TelemetryWriter.Schema,
                TraceHeader: SessionTrace.Header,
                Forensic: ForensicSchema.Schema),
            StartedAt: startedAt,
            StartedAtState: startedAt is { Length: > 0 } ? "recorded" : NoWallClockState,
            Limitations: Limitations());
    }

    /// <summary>
    /// Reported HONESTLY, as the director required: the configured count is
    /// emitted explicitly — including, and especially, when it is zero.
    /// </summary>
    private static string AiNote(int aiEmpires) => aiEmpires == 0
        ? "aiEmpiresConfigured is 0. There is no AI actor in this build: no system emits an order, and the "
            + "only polity carries CommandSource.Player. 'What the AI decided' has no subject here — the "
            + "zero is a recorded fact about the configuration, not a missing field."
        : "aiEmpiresConfigured is the count worldgen was configured with. This build records the "
            + "configuration only; per-actor AI decision records do not exist in it.";

    /// <summary>
    /// THE LIMITATION CATALOGUE, carried IN the artifact so a reviewer's first
    /// question — what can this evidence NOT establish — is answered by one
    /// command instead of by discovering silence.
    /// </summary>
    public static ForensicLimitation[] Limitations() =>
    [
        new ForensicLimitation(
            ForensicSchema.LimitMigrationPairwise,
            "Individual migration destination attribution: which settlement a given settlement's emigrants "
                + "moved TO, and how many went to each.",
            ForensicSchema.MigrationPairwiseWhy,
            ForensicSchema.MigrationPairwiseAnswer),
        new ForensicLimitation(
            ForensicSchema.LimitHappinessDecomposition,
            "The decomposition of a happiness value: raw factor, normalised factor, per-factor weight, "
                + "per-factor contribution, and the aggregate.",
            ForensicSchema.HappinessDecompositionWhy,
            null),
        new ForensicLimitation(
            ForensicSchema.LimitStoreLosses,
            "The per-settlement split of StoreLosses into spoilage, granary overflow, appropriation and "
                + "colony provisions.",
            ForensicSchema.StoreLossesWhy,
            null),
        new ForensicLimitation(
            ForensicSchema.LimitNoAi,
            "AI decisions.",
            "No system in this build emits an order; worldgen's aiEmpires defaults to 0 and the sole polity "
                + "carries CommandSource.Player. There is no AI decision to record, which is different from "
                + "an AI decision that went unrecorded.",
            null),
        new ForensicLimitation(
            ForensicSchema.LimitNoGateProducer,
            "Test and gate verdicts for the build that produced this run.",
            "No producer exists: CI runs a bare `dotnet test` with no structured log upload, and nothing in "
                + "any run artifact references a test run, a gate, a CI run id or a golden. The gates block "
                + "is therefore empty and says so; it is never populated with a claim nobody measured.",
            null),
    ];

    /// <summary>
    /// Opens (or creates) the forensic file and writes the run record as its
    /// first line. Returns the run id, which is the join key for everything
    /// else in the set.
    /// </summary>
    public static string WriteRun(string path, ForensicRunRecord run)
    {
        ArgumentNullException.ThrowIfNull(run);
        Directory.CreateDirectory(Path.GetDirectoryName(path)!);
        using var file = new FileStream(path, FileMode.Create, FileAccess.Write, FileShare.Read);
        ForensicWriter.WriteRun(file, run);
        file.Flush(flushToDisk: true);
        return run.RunId;
    }

    /// <summary>
    /// APPENDS the close record. Append, never rewrite: the run record already
    /// on disk must survive a session that never reaches this call, because the
    /// absence of a close line is itself the evidence that it did not.
    /// </summary>
    public static void WriteClose(string path, ForensicCloseRecord close)
    {
        ArgumentNullException.ThrowIfNull(close);
        Directory.CreateDirectory(Path.GetDirectoryName(path)!);
        using var file = new FileStream(path, FileMode.Append, FileAccess.Write, FileShare.Read);
        ForensicWriter.WriteClose(file, close);
        file.Flush(flushToDisk: true);
    }

    /// <summary>
    /// The companion set, bound BY CONTENT. Each role is hashed from the file as
    /// it stands at close; a file that is not there is recorded as absent with
    /// explicit nulls, never as a zero-byte artifact.
    /// </summary>
    public static ArtifactRef[] Companions(string directory, params (string Role, string File)[] files)
    {
        ArgumentNullException.ThrowIfNull(files);
        var refs = new ArtifactRef[files.Length];
        for (int i = 0; i < files.Length; i++)
        {
            string full = Path.Combine(directory, files[i].File);
            string? sha = ForensicIdentity.Sha256HexOfFile(full);
            refs[i] = sha is null
                ? new ArtifactRef(files[i].Role, files[i].File, null, null,
                    "absent — this file was not present beside the forensic record at close.")
                : new ArtifactRef(files[i].Role, files[i].File, new FileInfo(full).Length, sha, "recorded");
        }
        return refs;
    }
}
