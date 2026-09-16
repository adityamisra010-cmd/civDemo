namespace Sim.Core.Observability.Forensic;

/// <summary>One embedded configuration resource, by CONTENT rather than by name.
/// The largest provenance hole in the shipped artifact set is that all nine
/// content files are compiled into Sim.Data and nothing records their bytes: a
/// session played from a locally built binary records `buildSha = "dev"` and so
/// records no recoverable configuration at all. This closes it with a pure READ
/// of config already in memory.</summary>
public sealed record ConfigResource(string File, long Bytes, string Sha256);

/// <summary>One system in the turn pipeline, at its position. The ORDER is data
/// loaded at startup (pipeline.json) and is persisted in no existing artifact,
/// although every value in every other artifact depends on it.</summary>
public sealed record PipelineEntry(int Position, int SystemId, string Name);

/// <summary>One era band: the dt schedule is what makes every per-year rate in
/// the run mean what it means.</summary>
public sealed record EraBandRecord(string Name, long StartDay, long EndDay, long DtDays);

/// <summary>
/// A companion artifact, bound BY CONTENT. The manifest names its companions by
/// FILENAME only, so swapping a same-named file is undetectable and surfaces
/// downstream as "REPRODUCTION FAILED" — which reads as a determinism defect and
/// is not one. <see cref="Sha256"/> and <see cref="Bytes"/> are null, with
/// <see cref="State"/> saying why, when the file was not present at close.
/// </summary>
public sealed record ArtifactRef(string Role, string File, long? Bytes, string? Sha256, string State);

/// <summary>Every artifact vintage in force for this run, in one place, because
/// they are independent of each other and a reader who knows one does not know
/// the rest.</summary>
public sealed record ArtifactSchemas(
    int CanonicalSchemaVersion,
    string SessionManifest,
    string Telemetry,
    string TraceHeader,
    string Forensic);

/// <summary>
/// One thing this evidence CANNOT establish, carried IN the artifact. A
/// reviewer's first question is what the record is unable to tell him, and today
/// he can only answer it by discovering silence. <see cref="Answer"/>, when
/// present, is the exact string a query for this quantity must return — so the
/// limitation is not merely documented, it is the literal answer.
/// </summary>
public sealed record ForensicLimitation(string Token, string What, string Why, string? Answer);

/// <summary>
/// THE RUN RECORD — first line of the forensic file, one per run. Everything
/// here is a READ of information already in memory at launch, or a digest of it.
/// No simulation state is read, written or recomputed.
/// </summary>
public sealed record ForensicRunRecord(
    string RunId,                       // DERIVED — content-addressed, see ForensicIdentity
    string RunIdInputs,                 // the exact preimage recipe, so the id can be recomputed
    ulong Seed,
    int? SizePx,
    int? SettlementsOverride,
    bool Founded,
    int CanonicalSchemaVersion,
    string HashAlgorithm,
    bool HashCoversSchemaVersion,
    string BuildSha,
    string BuildDate,
    bool BuildShaRecorded,              // false when buildSha is the local "dev" fallback
    string Platform,
    ConfigResource[] Config,
    string ConfigDigest,
    string OrdersDigest,                // digest of the order log AS IT STOOD when this record was written
    PipelineEntry[] Pipeline,
    EraBandRecord[] EraBands,
    string DtRule,
    string RngDerivation,
    int AiEmpiresConfigured,            // READ WorldgenConfig.AiEmpires — emitted EXPLICITLY, never omitted
    string AiNote,
    string? TerrainContentHash,
    string TerrainState,
    ArtifactSchemas Schemas,
    string? StartedAt,                  // repro:false — wall clock, supplied by the caller, never read here
    string? StartedAtState,
    ForensicLimitation[] Limitations);

/// <summary>
/// THE CLOSE RECORD — last line, one per run. Its ABSENCE is the signal that the
/// session did not close cleanly: no other artifact in the set can express that.
/// </summary>
public sealed record ForensicCloseRecord(
    string RunId,
    long TurnsReached,
    string? FinalWorldHash,             // CALLER-SUPPLIED: the hash the session itself computed
    string FinalWorldHashState,
    ArtifactRef[] Artifacts,
    string GatesState);                 // no gate-verdict producer exists in this build — stated, not faked
