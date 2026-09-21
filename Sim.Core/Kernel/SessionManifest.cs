using System.Globalization;
using System.Text;
using System.Text.Json;

namespace Sim.Core.Kernel;

/// <summary>
/// THE REPRODUCTION CONTRACT for a played session.
///
/// THE GAP IT CLOSES, stated as the defect it is. An orders `.bin` records what
/// the director ordered and on which turn — but NOT which world he ordered it
/// in. The seed lived only in the argv of a process that has since exited, so a
/// session log was, strictly, unreplayable: the one input needed to rebuild the
/// world was the one input nobody wrote down. `sim replay` has always been able
/// to reproduce a session; there was simply no way to know what to hand it.
/// Every other observability tool in the repo — ReplayReport included — sat
/// behind that missing number.
///
/// SO THIS IS DELIBERATELY SMALL. It is not a savegame and it is not state: it
/// is the argv of the session plus the identity of the build that ran it, which
/// together are exactly enough to reconstruct the world and replay the log into
/// it. Snapshot save/load already exists for state (`sim hash`, `Snapshot`), and
/// duplicating any of it here would create a second copy of the world free to
/// disagree with the first.
///
/// WHY IT LIVES IN Sim.Core AND NOT Sim.Ui. Two apps have to agree on it: the
/// one that PLAYS a session writes it, and the one that ANALYSES a session reads
/// it. Sim.Cli cannot reference Sim.Ui (a MonoGame WinExe), so a shape they both
/// depend on belongs beneath both. It holds no world state, runs no logic, and
/// nothing in the pipeline consults it.
///
/// NO WALL CLOCK IS READ HERE. `StartedAt` is a string the CALLER supplies —
/// Sim.Ui reads the clock, which is legal there (ADR-009) and banned in this
/// project. The stamp is provenance for a human, never an input to anything.
///
/// NO RUNTIME IS INTERROGATED HERE EITHER. `Platform` (v2, ADR-022 / CR-013)
/// is likewise a string the caller supplies — Sim.Ui hands over
/// `RuntimeInformation.RuntimeIdentifier`. It exists because the determinism
/// promise is scoped to ONE reference platform and a session played elsewhere
/// is expected to diverge from a reference replay (CR-013 §8: last-ulp libm
/// differences in Exp/Sqrt from turn 2); a reader that does not know where a
/// trace was recorded cannot tell a platform divergence from a determinism
/// defect, and `sim inspect` would report the first as the second — which is
/// exactly what happened to the director's first real session (CR-013 §3).
/// </summary>
public sealed record SessionManifest(
    ulong Seed,
    int? SizePx,
    int? Settlements,
    int SchemaVersion,
    string BuildSha,
    string BuildDate,
    string StartedAt,
    string OrdersFile,
    string ChronicleFile,
    string TraceFile,
    // T4.19: the telemetry JSONL beside the other four. A MANIFEST field, not
    // schema — CanonicalSchema.Version is untouched, and a manifest written
    // before T4.19 reads back with an empty TelemetryFile rather than failing,
    // because the session it describes is still fully reproducible without it
    // (the telemetry is a pure function of the replay, §7).
    string TelemetryFile = "",
    // v2 (ADR-022): the runtime identifier of the machine that PLAYED the
    // session, e.g. "win-x64", "linux-x64", "ubuntu.24.04-x64". Supplied by
    // the caller; compared against ReferencePlatform by the reader. A v1
    // file reads back as PlatformNotRecorded, never as a guess.
    string Platform = SessionManifest.PlatformNotRecorded,
    // m4-forensic P1: the forensic record beside the other five. A MANIFEST
    // FIELD, NOT A SCHEMA MOVE — the tag stays session-manifest/v2 deliberately.
    // `Read` is a tag WHITELIST that throws on an unknown vintage, so a v3 file
    // would be REJECTED by every binary already built, including the shipped UI
    // and any previously built `sim inspect`. An ADDITIVE key inside v2 is read
    // by those binaries exactly as before (they ask for the keys they know by
    // name and ignore the rest), and reads back here as an empty string on any
    // manifest written before this packet — the session it describes is still
    // fully reproducible without it, exactly as TelemetryFile argued.
    string ForensicFile = "")
{
    /// <summary>The schema tag, so a reader can tell which vintage produced a
    /// file it did not write. v2 added `platform`.</summary>
    public const string Schema = "session-manifest/v2";

    /// <summary>The previous tag. Still READABLE: a v1 session is fully
    /// reproducible without a platform field — the world is a function of seed
    /// and order log — so refusing it would lose real sessions to a provenance
    /// column. It reads back with <see cref="PlatformNotRecorded"/>.</summary>
    public const string SchemaV1 = "session-manifest/v1";

    /// <summary>
    /// THE REFERENCE PLATFORM (ADR-022, CR-013 ruling: options 1 + 3). The
    /// determinism promise — one world per (seed, order log) — is defined on
    /// Linux x64: the goldens, the replay evidence and CI's pins are all
    /// produced there. Any other platform is SUPPORTED for play and produces
    /// session records that reproduce on that machine, under surveillance
    /// (.github/workflows/xplat-surveillance.yml), never as the canonical
    /// artifact. The value is the portable .NET RID the reference CI runner
    /// reports (measured, CR-013 §8.4: `RID: linux-x64` on ubuntu-latest).
    /// </summary>
    public const string ReferencePlatform = "linux-x64";

    /// <summary>What a v1 manifest's Platform reads back as.</summary>
    public const string PlatformNotRecorded = "not recorded (pre-v2 session)";

    /// <summary>
    /// Whether a recorded platform string IS the reference platform. Pure
    /// string logic — this type reads no runtime information.
    ///
    /// Two spellings are accepted, both MEASURED to produce the reference
    /// hashes and no more than those two:
    ///   - "linux-x64" — the portable RID Microsoft's build reports; the CI
    ///     runner that pins every golden (CR-013 §8.4, run 34419607514).
    ///   - "ubuntu.24.04-x64" — the distro-qualified RID the Ubuntu-archive SDK
    ///     reports on the remote-session container, whose turn-1/2/3 saves
    ///     equal the linux-x64 runner's hash for hash (CR-013 §8.2 vs §8.4).
    ///     This exact spelling, not the ubuntu.* family: 22.04 has not been
    ///     measured and so is not on the list.
    /// Any other RID — including other glibc distributions that the .NET RID
    /// graph would resolve to linux-x64 — is reported as NOT the reference,
    /// because nobody has measured it. The set grows by measurement (add the
    /// RID here with the run that produced the reference hashes), not by
    /// reasoning about RID inheritance: the CR-013 divergence lives in libm,
    /// which the RID graph says nothing about.
    /// </summary>
    public static bool IsReferencePlatform(string platform)
        => platform == ReferencePlatform || platform == MeasuredUbuntuRid;

    /// <summary>The one distro-qualified RID measured equal to the reference
    /// (CR-013 §8.2 vs §8.4). Exactly this spelling: a rule that accepted the
    /// whole ubuntu.* family would be reasoning about RID inheritance, which
    /// the comment above says this set does not do.</summary>
    public const string MeasuredUbuntuRid = "ubuntu.24.04-x64";

    /// <summary>Whether the platform is known at all — false for a v1 file.</summary>
    public static bool IsPlatformRecorded(string platform)
        => platform.Length > 0 && platform != PlatformNotRecorded;

    /// <summary>
    /// The `sim replay` invocation that reproduces this session, ready to paste.
    /// Generated from the recorded arguments rather than typed by hand, so it
    /// cannot describe a session other than this one.
    ///
    /// `--turns` is NOT included: the manifest is written at LAUNCH, before any
    /// turn is played, and a turn count guessed at launch would be a lie. The
    /// reader supplies it — `sim inspect` takes it from the trace, which knows
    /// how far the session actually got.
    /// </summary>
    public string ReplayCommand(long turns)
    {
        var sb = new StringBuilder("sim replay --founded --seed ");
        sb.Append(Seed.ToString(CultureInfo.InvariantCulture));
        if (SizePx is { } px) sb.Append(" --size ").Append(px.ToString(CultureInfo.InvariantCulture));
        if (Settlements is { } n) sb.Append(" --settlements ").Append(n.ToString(CultureInfo.InvariantCulture));
        sb.Append(" --orders ").Append(OrdersFile);
        sb.Append(" --turns ").Append(turns.ToString(CultureInfo.InvariantCulture));
        return sb.ToString();
    }

    /// <summary>Writes the manifest as indented JSON — indented because a human
    /// opens this file to find out what he played.</summary>
    public void Write(Stream output)
    {
        using var json = new Utf8JsonWriter(output, new JsonWriterOptions { Indented = true });
        json.WriteStartObject();
        json.WriteString("schema", Schema);
        json.WriteNumber("seed", Seed);
        if (SizePx is { } px) json.WriteNumber("sizePx", px); else json.WriteNull("sizePx");
        if (Settlements is { } n) json.WriteNumber("settlements", n); else json.WriteNull("settlements");
        json.WriteNumber("schemaVersion", SchemaVersion);
        json.WriteString("buildSha", BuildSha);
        json.WriteString("buildDate", BuildDate);
        json.WriteString("startedAt", StartedAt);
        json.WriteString("ordersFile", OrdersFile);
        json.WriteString("chronicleFile", ChronicleFile);
        json.WriteString("traceFile", TraceFile);
        json.WriteString("telemetryFile", TelemetryFile);
        json.WriteString("platform", Platform);
        json.WriteString("forensicFile", ForensicFile);
        json.WriteEndObject();
        json.Flush();
    }

    /// <summary>
    /// Reads a manifest back — v2, or v1 with the platform reported as not
    /// recorded. Throws with the offending file named when the tag is missing
    /// or unknown — a reader that silently accepts an unknown vintage would
    /// replay the wrong world and report the difference as a finding.
    /// </summary>
    public static SessionManifest Read(Stream input, string describedAs)
    {
        using JsonDocument doc = JsonDocument.Parse(input);
        JsonElement root = doc.RootElement;

        string? schema = root.TryGetProperty("schema", out JsonElement s) ? s.GetString() : null;
        if (schema != Schema && schema != SchemaV1)
        {
            throw new InvalidDataException(
                $"{describedAs} is not a {Schema} (or {SchemaV1}) file (found '{schema ?? "no schema tag"}').");
        }

        return new SessionManifest(
            Seed: root.GetProperty("seed").GetUInt64(),
            SizePx: Nullable(root, "sizePx"),
            Settlements: Nullable(root, "settlements"),
            SchemaVersion: root.GetProperty("schemaVersion").GetInt32(),
            BuildSha: root.GetProperty("buildSha").GetString() ?? "",
            BuildDate: root.GetProperty("buildDate").GetString() ?? "",
            StartedAt: root.GetProperty("startedAt").GetString() ?? "",
            OrdersFile: root.GetProperty("ordersFile").GetString() ?? "",
            ChronicleFile: root.GetProperty("chronicleFile").GetString() ?? "",
            TraceFile: root.GetProperty("traceFile").GetString() ?? "",
            TelemetryFile: root.TryGetProperty("telemetryFile", out JsonElement tf) ? tf.GetString() ?? "" : "",
            // A v1 file has no platform column; a v2 file that somehow lacks
            // one is read the same way rather than invented.
            Platform: root.TryGetProperty("platform", out JsonElement pl) && pl.GetString() is { Length: > 0 } p
                ? p : PlatformNotRecorded,
            // Absent on every manifest written before this packet, and on any
            // session that wrote no forensic record. Empty, never invented.
            ForensicFile: root.TryGetProperty("forensicFile", out JsonElement ff) ? ff.GetString() ?? "" : "");
    }

    private static int? Nullable(JsonElement root, string name)
        => root.TryGetProperty(name, out JsonElement e) && e.ValueKind == JsonValueKind.Number
            ? e.GetInt32()
            : null;
}
