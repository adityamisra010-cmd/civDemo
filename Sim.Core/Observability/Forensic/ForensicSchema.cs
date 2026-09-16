namespace Sim.Core.Observability.Forensic;

/// <summary>
/// THE FORENSIC RECORD LAYER — tag, record kinds, and the two vocabularies a
/// reviewer needs before he reads a single value: the EVIDENCE TAG every answer
/// carries, and the LIMITATION catalogue that says what this evidence cannot
/// establish.
///
/// WHY A SEPARATE ARTIFACT. Every other file a session writes is closed to
/// extension against the code that reads it: `SessionManifest.Read` is a tag
/// WHITELIST that throws on an unknown vintage, `SessionTrace.Parse` demands
/// exactly six columns and throws with the line number, the order log pins
/// IoVersion 1, and the chronicle has no schema at all. Moving the telemetry
/// tag breaks the byte-identity that tag promises. So the forensic record is its
/// OWN file with its OWN version, and <see cref="Sim.Core.Kernel.CanonicalSchema.Version"/>
/// stays at 24: no world hash moves, no golden moves, no existing reader breaks.
///
/// WHAT IT IS NOT. It is not state. Nothing here is serialized into
/// <see cref="Sim.Core.State.WorldState"/>, read by a system, or reachable from
/// the executor. It holds no world. Like every record in this namespace it is
/// built AFTER a step from worlds it does not retain.
/// </summary>
public static class ForensicSchema
{
    /// <summary>The tag on every line. Versioned INDEPENDENTLY of schema 24 and
    /// of telemetry/v2 — this file's vintage says nothing about theirs.</summary>
    public const string Schema = "forensic/v1";

    /// <summary>The identity record: first line of the file, one per run.</summary>
    public const string RecRun = "run";

    /// <summary>The closing record: last line of the file, one per run. ITS
    /// ABSENCE IS INFORMATION — a file with no close line describes a session
    /// that did not close cleanly, which no existing artifact can express.</summary>
    public const string RecClose = "close";

    /// <summary>The hash algorithm <see cref="Sim.Core.Kernel.WorldHash"/> runs,
    /// named so a reader never has to guess what a 64-hex string is.</summary>
    public const string HashAlgorithm = "sha256/canonical-stream";

    /// <summary>
    /// STATED FACT, not an opinion: <c>CanonicalSchema.Write</c> begins at the
    /// seed, so a world hash does NOT self-identify the schema version that
    /// produced it. Two builds at different schema versions produce different
    /// hashes for the same world and neither hash says so. The forensic record
    /// carries the schema version beside the hash precisely because the hash
    /// cannot carry it itself, and making the hash self-identifying would change
    /// the hashed bytes of every world — a change to the frozen kernel contract.
    /// </summary>
    public const bool HashCoversSchemaVersion = false;

    /// <summary>The dt rule, stated because it is the one turn-boundary fact a
    /// reader most often gets wrong: dt is chosen at TURN START from the era
    /// band in force, every rate is integrated with it, and the clock advances
    /// LAST (TurnExecutor.Step).</summary>
    public const string DtRule =
        "dt is chosen at TURN START from the era band in force at the pre-step sim day, every rate is "
        + "integrated with dtYears, and the clock advances LAST — so the dt recorded on a turn is the dt "
        + "that turn integrated, not the one the next turn will use (TurnExecutor.Step).";

    /// <summary>How the RNG streams are derived, so a reader knows the seed is
    /// the whole of the randomness provenance.</summary>
    public const string RngDerivation =
        "every stream is derived lazily from splitmix64(worldSeed, systemId, regionId) through RngRegistry; "
        + "the seed on this record is therefore the complete randomness provenance of the run.";

    // ---------------------------------------------------------------------
    // THE LIMITATION CATALOGUE — what this evidence CANNOT establish.
    // ---------------------------------------------------------------------

    /// <summary>Token for the pairwise-migration limitation.</summary>
    public const string LimitMigrationPairwise = "migration-destination-not-persisted";

    /// <summary>
    /// THE DIRECTOR'S RULING, VERBATIM AND WITHOUT SOFTENING. The migration loop
    /// retains AGGREGATES ONLY: one Inflow and one Outflow scalar per settlement
    /// per turn. Individual migration destination attribution CANNOT be
    /// reconstructed from this build's artifacts, and this layer will not
    /// pretend otherwise — it does not infer destinations from attractiveness,
    /// rank destinations as historical fact, reconstruct probabilistically, add
    /// a synthetic category, or treat aggregate inflow beside aggregate outflow
    /// as proof of pairwise movement. The correct query result is this string.
    /// </summary>
    public const string MigrationPairwiseAnswer =
        "UNAVAILABLE: pairwise migration destination was not persisted by this build.";

    /// <summary>Why it is unavailable, for the reviewer who asks.</summary>
    public const string MigrationPairwiseWhy =
        "MigrationSystem carries a single MigrationRemainder accumulator per bucket row, mutated across the "
        + "whole destination loop, so the amount sent to any one destination depends on every destination "
        + "considered before it; only the FINAL remainder is serialized and it does not decompose. The move "
        + "itself is a Ledger.Transfer, which by its own contract records no flow entry. What survives the "
        + "step is MigrationFlowRow.Inflow and .Outflow — two scalars per settlement, netted across every "
        + "partner, every cohort, every class and both channels. Closing this needs a new authoritative "
        + "serialized row (From, To, Class, Cohort, Moved), which is a schema change and a director ruling, "
        + "NOT an observability change.";

    /// <summary>Token for the happiness-decomposition limitation.</summary>
    public const string LimitHappinessDecomposition = "happiness-decomposition-not-public";

    /// <summary>
    /// THE HAPPINESS BOUNDARY, stated as file:line so it can be checked rather
    /// than believed. What the simulation exposes publicly is what this layer
    /// reports and nothing more: SettlementHappiness.Of (the authoritative
    /// value), .Factors (the two factor values), .FoodSufficiency,
    /// .HousingSufficiency, .IsRevoltReady, .RevoltThreshold, and the READ
    /// branch labels that follow from which rows are PRESENT. The per-factor
    /// weights, the normalisation, the floor and the span are NOT public, and
    /// copying them into an observer would be exactly the drifting second
    /// implementation the observability contract forbids. So a happiness value
    /// other than an exact 0 or 100 is reported, not decomposed.
    /// </summary>
    public const string HappinessDecompositionWhy =
        "Sim.Core/State/SettlementHappiness.cs: WeightOf is `private static` (:234); the need-id mapping is "
        + "`private const int SustenanceNeedId` (:226) and `ShelterNeedId` (:227); and the floor, the span "
        + "and the normalisation are inline locals inside Of (:169-219), reachable through no public member. "
        + "Raw factor, normalised factor, per-factor weight, per-factor contribution and the aggregate are "
        + "therefore NOT DERIVABLE from any public function on stored state. This layer reports the "
        + "authoritative value from SettlementHappiness.Of, the factor values from .Factors, and the branch "
        + "labels that follow from row presence. It does not manufacture a decomposition.";

    /// <summary>Token for the granary/StoreLosses limitation.</summary>
    public const string LimitStoreLosses = "store-losses-unsplit";

    /// <summary>
    /// StoreLosses is ONE residual absorbing FOUR mechanisms. Its identity is
    /// carried on the record itself (SettlementRecord.FoodSection), and the
    /// split is not available: ConsumptionSystem.BoundStore is `private static`
    /// and its annualGrainDemand argument is built AT THE CALL SITE from two
    /// transient locals, so no public function can be called with it.
    /// </summary>
    public const string StoreLossesWhy =
        "StoreLosses is a RESIDUAL of a stated identity and absorbs spoilage, granary overflow, "
        + "appropriation and colony provisions in ONE number. ConsumptionSystem.BoundStore is `private "
        + "static` (:285) and its annualGrainDemand argument is assembled at the call site (:200-203) from "
        + "transient locals, so the split cannot be recomputed through any public function. World level is "
        + "exact by ledger reason; the per-settlement split is not recorded.";

    /// <summary>Token for the AI limitation.</summary>
    public const string LimitNoAi = "no-ai-at-m4";

    /// <summary>Token for the gate-verdict limitation.</summary>
    public const string LimitNoGateProducer = "no-gate-verdict-producer";
}

/// <summary>
/// THE RECONSTRUCTION CONTRACT, as a TYPE rather than a convention.
///
/// Every answer this layer gives carries one of these three tags, and the type
/// system is what stops the third silently becoming the second: a reader cannot
/// print a value without printing its tag, because the tag is not a comment or a
/// string the caller may forget — it is a field of the answer.
///
/// The failure this prevents is specific and has a name in this repository's
/// history: an observer that reconstructs a quantity the simulation did not
/// record, and presents the reconstruction as evidence. NOT RECORDED must never
/// be converted into DERIVABLE, however plausible the derivation.
/// </summary>
public enum Evidence
{
    /// <summary>The value was READ from a stored record. It is what the
    /// simulation actually wrote. No inference of any kind.</summary>
    Known = 0,

    /// <summary>The value was computed from stored records by a stated,
    /// exact rule — a sum, a difference, a residual of a named identity, or a
    /// call to a PUBLIC simulation function on stored state. The rule is
    /// printed with the answer so it can be checked.</summary>
    Derivable = 1,

    /// <summary>The simulation did not persist what would be needed. The answer
    /// is the limitation, not a number. This tag is terminal: nothing in this
    /// layer promotes it.</summary>
    NotRecorded = 2,
}

/// <summary>Printing the tag — one word, fixed spelling, so a script can grep
/// for it and a human cannot mistake one tag for another.</summary>
public static class EvidenceTag
{
    public static string Of(Evidence evidence) => evidence switch
    {
        Evidence.Known => "KNOWN",
        Evidence.Derivable => "DERIVABLE",
        Evidence.NotRecorded => "NOT RECORDED",
        _ => throw new ArgumentOutOfRangeException(nameof(evidence), evidence, "unknown evidence tag"),
    };
}
