using System.Text.Json;

namespace Sim.Core.Observability.Forensic;

/// <summary>
/// THE READER THE ARTIFACT SET HAS NEVER HAD.
///
/// Before this packet every artifact a session wrote was WRITE-ONLY: there is no
/// telemetry reader anywhere in the repository, so `sim inspect` answered every
/// question by replaying the entire session — tens of seconds for a long one —
/// and a file found on its own could be read only by eye. Adding fields without
/// adding a reader would have added data no tool can read.
///
/// STRICT ON THE TAG, TOLERANT OF KEYS — the SessionManifest.Read discipline. An
/// unknown vintage is refused loudly with the file named, because a reader that
/// silently accepts a file it does not understand reports the difference as a
/// finding. Unknown KEYS inside a known vintage are ignored, so a later version
/// that only adds fields stays readable.
/// </summary>
public sealed class ForensicRecordFile
{
    public ForensicRunRecord Run { get; }

    /// <summary>The close record, or null. NULL IS INFORMATION: a forensic file
    /// with no close line describes a session that did not close cleanly, and no
    /// other artifact in the set can express that.</summary>
    public ForensicCloseRecord? Close { get; }

    public string RunId => Run.RunId;

    private ForensicRecordFile(ForensicRunRecord run, ForensicCloseRecord? close)
    {
        Run = run;
        Close = close;
    }

    /// <summary>Reads a forensic JSONL file from disk.</summary>
    public static ForensicRecordFile Read(string path)
        => Parse(File.ReadLines(path), path);

    /// <summary>Reads a forensic record from its lines.</summary>
    public static ForensicRecordFile Parse(IEnumerable<string> lines, string describedAs)
    {
        ArgumentNullException.ThrowIfNull(lines);
        ForensicRunRecord? run = null;
        ForensicCloseRecord? close = null;

        int number = 0;
        foreach (string raw in lines)
        {
            number++;
            string line = raw.Trim();
            if (line.Length == 0) continue;

            using JsonDocument doc = JsonDocument.Parse(line);
            JsonElement root = doc.RootElement;

            string? tag = Str(root, "v");
            if (tag != ForensicSchema.Schema)
            {
                throw new InvalidDataException(
                    $"{describedAs} line {number}: not a {ForensicSchema.Schema} record "
                    + $"(found '{tag ?? "no v tag"}').");
            }

            string rec = Str(root, "rec") ?? "";
            if (rec == ForensicSchema.RecRun) run = ReadRun(root);
            else if (rec == ForensicSchema.RecClose) close = ReadClose(root);
            // Any other kind is a later vintage's business: ignored, not refused.
        }

        if (run is null)
            throw new InvalidDataException($"{describedAs}: no '{ForensicSchema.RecRun}' record — the file has no identity.");
        return new ForensicRecordFile(run, close);
    }

    private static ForensicRunRecord ReadRun(JsonElement root)
    {
        var config = new List<ConfigResource>();
        foreach (JsonElement e in Array(root, "config"))
            config.Add(new ConfigResource(Str(e, "file") ?? "", Long(e, "bytes") ?? 0, Str(e, "sha256") ?? ""));

        var pipeline = new List<PipelineEntry>();
        foreach (JsonElement e in Array(root, "pipeline"))
            pipeline.Add(new PipelineEntry((int)(Long(e, "position") ?? 0), (int)(Long(e, "systemId") ?? 0), Str(e, "name") ?? ""));

        var bands = new List<EraBandRecord>();
        foreach (JsonElement e in Array(root, "eraBands"))
            bands.Add(new EraBandRecord(Str(e, "name") ?? "", Long(e, "startDay") ?? 0, Long(e, "endDay") ?? 0, Long(e, "dtDays") ?? 0));

        var limits = new List<ForensicLimitation>();
        foreach (JsonElement e in Array(root, "limitations"))
            limits.Add(new ForensicLimitation(Str(e, "token") ?? "", Str(e, "what") ?? "", Str(e, "why") ?? "", Str(e, "answer")));

        JsonElement schemas = root.TryGetProperty("artifactSchemas", out JsonElement sc) ? sc : default;

        return new ForensicRunRecord(
            RunId: Str(root, "run") ?? "",
            RunIdInputs: Str(root, "runIdInputs") ?? "",
            Seed: root.TryGetProperty("seed", out JsonElement sd) ? sd.GetUInt64() : 0UL,
            SizePx: (int?)Long(root, "sizePx"),
            SettlementsOverride: (int?)Long(root, "settlementsOverride"),
            Founded: Bool(root, "founded"),
            CanonicalSchemaVersion: (int)(Long(root, "canonicalSchemaVersion") ?? 0),
            HashAlgorithm: Str(root, "hashAlgorithm") ?? "",
            HashCoversSchemaVersion: Bool(root, "hashCoversSchemaVersion"),
            BuildSha: Str(root, "buildSha") ?? "",
            BuildDate: Str(root, "buildDate") ?? "",
            BuildShaRecorded: Bool(root, "buildShaRecorded"),
            Platform: Str(root, "platform") ?? "",
            Config: [.. config],
            ConfigDigest: Str(root, "configDigest") ?? "",
            OrdersDigest: Str(root, "ordersDigest") ?? "",
            Pipeline: [.. pipeline],
            EraBands: [.. bands],
            DtRule: Str(root, "dtRule") ?? "",
            RngDerivation: Str(root, "rngDerivation") ?? "",
            AiEmpiresConfigured: (int)(Long(root, "aiEmpiresConfigured") ?? 0),
            AiNote: Str(root, "aiNote") ?? "",
            TerrainContentHash: Str(root, "terrainContentHash"),
            TerrainState: Str(root, "terrainContentHashState") ?? "",
            Schemas: new ArtifactSchemas(
                (int)(Long(schemas, "canonical") ?? 0),
                Str(schemas, "sessionManifest") ?? "",
                Str(schemas, "telemetry") ?? "",
                Str(schemas, "traceHeader") ?? "",
                Str(schemas, "forensic") ?? ""),
            StartedAt: Str(root, "startedAt"),
            StartedAtState: Str(root, "startedAtState"),
            Limitations: [.. limits]);
    }

    private static ForensicCloseRecord ReadClose(JsonElement root)
    {
        var artifacts = new List<ArtifactRef>();
        foreach (JsonElement e in Array(root, "artifacts"))
            artifacts.Add(new ArtifactRef(
                Str(e, "role") ?? "", Str(e, "file") ?? "", Long(e, "bytes"), Str(e, "sha256"), Str(e, "state") ?? ""));

        return new ForensicCloseRecord(
            RunId: Str(root, "run") ?? "",
            TurnsReached: Long(root, "turnsReached") ?? 0,
            FinalWorldHash: Str(root, "finalWorldHash"),
            FinalWorldHashState: Str(root, "finalWorldHashState") ?? "",
            Artifacts: [.. artifacts],
            GatesState: Str(root, "gatesState") ?? "");
    }

    /// <summary>The limitation for a token, or null.</summary>
    public ForensicLimitation? Limitation(string token)
    {
        for (int i = 0; i < Run.Limitations.Length; i++)
            if (Run.Limitations[i].Token == token) return Run.Limitations[i];
        return null;
    }

    // --- JSON primitives: absent or explicitly null both read as absent -------

    private static string? Str(JsonElement e, string name)
        => e.ValueKind == JsonValueKind.Object
            && e.TryGetProperty(name, out JsonElement v)
            && v.ValueKind == JsonValueKind.String
            ? v.GetString() : null;

    private static long? Long(JsonElement e, string name)
        => e.ValueKind == JsonValueKind.Object
            && e.TryGetProperty(name, out JsonElement v)
            && v.ValueKind == JsonValueKind.Number
            ? v.GetInt64() : null;

    private static bool Bool(JsonElement e, string name)
        => e.ValueKind == JsonValueKind.Object
            && e.TryGetProperty(name, out JsonElement v)
            && v.ValueKind == JsonValueKind.True;

    private static IEnumerable<JsonElement> Array(JsonElement e, string name)
    {
        if (e.ValueKind != JsonValueKind.Object) yield break;
        if (!e.TryGetProperty(name, out JsonElement v) || v.ValueKind != JsonValueKind.Array) yield break;
        foreach (JsonElement item in v.EnumerateArray()) yield return item;
    }
}
