using System.Text.Json;

namespace Sim.Core.Observability.Forensic;

/// <summary>
/// THE FORENSIC WRITER — JSONL, append-only, one record per line, tagged
/// <see cref="ForensicSchema.Schema"/> on every line so a file found alone
/// identifies its own vintage.
///
/// APPEND BY CONSTRUCTION, like the telemetry after P0: no header line, every
/// line self-describing, each record written whole and flushed before the next
/// begins. The caller owns the stream (the ReplayReport precedent): a failure to
/// write cannot leave a half-stepped world behind, because the world is not here.
///
/// THE NULL POLICY IS STRUCTURAL, NOT A CONVENTION. A value the simulation did
/// not record is written as JSON `null` beside a sibling `...State` string
/// saying which limitation applies. There is NO path in this writer that can
/// emit a non-finite double as a string, a sentinel integer standing for
/// absence, or a bare zero standing for "no row". This is a DELIBERATE
/// divergence from telemetry/v2, which writes the strings "NaN" / "Infinity";
/// that convention is not touched here, because changing it would move the
/// telemetry tag and break the byte-identity that tag promises.
/// </summary>
public static class ForensicWriter
{
    /// <summary>Writes the run record as the file's first line.</summary>
    public static void WriteRun(Stream output, ForensicRunRecord run)
    {
        ArgumentNullException.ThrowIfNull(output);
        ArgumentNullException.ThrowIfNull(run);

        using var json = new Utf8JsonWriter(output, new JsonWriterOptions { Indented = false });
        json.WriteStartObject();
        Envelope(json, ForensicSchema.RecRun, run.RunId, turn: 0, repro: true);

        json.WriteString("runIdInputs", run.RunIdInputs);
        json.WriteNumber("seed", run.Seed);
        NullableInt(json, "sizePx", run.SizePx, "default");
        NullableInt(json, "settlementsOverride", run.SettlementsOverride, "default");
        json.WriteBoolean("founded", run.Founded);

        json.WriteNumber("canonicalSchemaVersion", run.CanonicalSchemaVersion);
        json.WriteString("hashAlgorithm", run.HashAlgorithm);
        json.WriteBoolean("hashCoversSchemaVersion", run.HashCoversSchemaVersion);

        json.WriteString("buildSha", run.BuildSha);
        json.WriteString("buildDate", run.BuildDate);
        json.WriteBoolean("buildShaRecorded", run.BuildShaRecorded);
        json.WriteString("platform", run.Platform);

        json.WriteStartArray("config");
        for (int i = 0; i < run.Config.Length; i++)
        {
            json.WriteStartObject();
            json.WriteString("file", run.Config[i].File);
            json.WriteNumber("bytes", run.Config[i].Bytes);
            json.WriteString("sha256", run.Config[i].Sha256);
            json.WriteEndObject();
        }
        json.WriteEndArray();
        json.WriteString("configDigest", run.ConfigDigest);
        json.WriteString("ordersDigest", run.OrdersDigest);

        json.WriteStartArray("pipeline");
        for (int i = 0; i < run.Pipeline.Length; i++)
        {
            json.WriteStartObject();
            json.WriteNumber("position", run.Pipeline[i].Position);
            json.WriteNumber("systemId", run.Pipeline[i].SystemId);
            json.WriteString("name", run.Pipeline[i].Name);
            json.WriteEndObject();
        }
        json.WriteEndArray();

        json.WriteStartArray("eraBands");
        for (int i = 0; i < run.EraBands.Length; i++)
        {
            json.WriteStartObject();
            json.WriteString("name", run.EraBands[i].Name);
            json.WriteNumber("startDay", run.EraBands[i].StartDay);
            json.WriteNumber("endDay", run.EraBands[i].EndDay);
            json.WriteNumber("dtDays", run.EraBands[i].DtDays);
            json.WriteEndObject();
        }
        json.WriteEndArray();
        json.WriteString("dtRule", run.DtRule);
        json.WriteString("rngDerivation", run.RngDerivation);

        json.WriteNumber("aiEmpiresConfigured", run.AiEmpiresConfigured);
        json.WriteString("aiNote", run.AiNote);

        NullableString(json, "terrainContentHash", run.TerrainContentHash, run.TerrainState);

        json.WriteStartObject("artifactSchemas");
        json.WriteNumber("canonical", run.Schemas.CanonicalSchemaVersion);
        json.WriteString("sessionManifest", run.Schemas.SessionManifest);
        json.WriteString("telemetry", run.Schemas.Telemetry);
        json.WriteString("traceHeader", run.Schemas.TraceHeader);
        json.WriteString("forensic", run.Schemas.Forensic);
        json.WriteEndObject();

        // repro:false — the one wall-clock value in the record, supplied by the
        // caller (this assembly reads no clock) and excluded from the run id.
        NullableString(json, "startedAt", run.StartedAt, run.StartedAtState ?? "not-recorded");

        json.WriteStartArray("limitations");
        for (int i = 0; i < run.Limitations.Length; i++)
        {
            json.WriteStartObject();
            json.WriteString("token", run.Limitations[i].Token);
            json.WriteString("what", run.Limitations[i].What);
            json.WriteString("why", run.Limitations[i].Why);
            if (run.Limitations[i].Answer is { } a) json.WriteString("answer", a);
            else json.WriteNull("answer");
            json.WriteEndObject();
        }
        json.WriteEndArray();

        json.WriteEndObject();
        json.Flush();
        output.WriteByte((byte)'\n');
        output.Flush();
    }

    /// <summary>Writes the close record as the file's last line.</summary>
    public static void WriteClose(Stream output, ForensicCloseRecord close)
    {
        ArgumentNullException.ThrowIfNull(output);
        ArgumentNullException.ThrowIfNull(close);

        using var json = new Utf8JsonWriter(output, new JsonWriterOptions { Indented = false });
        json.WriteStartObject();
        Envelope(json, ForensicSchema.RecClose, close.RunId, close.TurnsReached, repro: true);

        json.WriteNumber("turnsReached", close.TurnsReached);
        NullableString(json, "finalWorldHash", close.FinalWorldHash, close.FinalWorldHashState);

        json.WriteStartArray("artifacts");
        for (int i = 0; i < close.Artifacts.Length; i++)
        {
            ArtifactRef a = close.Artifacts[i];
            json.WriteStartObject();
            json.WriteString("role", a.Role);
            json.WriteString("file", a.File);
            if (a.Bytes is { } b) json.WriteNumber("bytes", b); else json.WriteNull("bytes");
            if (a.Sha256 is { } h) json.WriteString("sha256", h); else json.WriteNull("sha256");
            json.WriteString("state", a.State);
            json.WriteEndObject();
        }
        json.WriteEndArray();

        json.WriteStartArray("gates");
        json.WriteEndArray();
        json.WriteString("gatesState", close.GatesState);

        json.WriteEndObject();
        json.Flush();
        output.WriteByte((byte)'\n');
        output.Flush();
    }

    private static void Envelope(Utf8JsonWriter json, string rec, string runId, long turn, bool repro)
    {
        json.WriteString("v", ForensicSchema.Schema);
        json.WriteString("rec", rec);
        json.WriteString("run", runId);
        json.WriteNumber("turn", turn);
        json.WriteBoolean("repro", repro);
    }

    /// <summary>An integer that may be absent: explicit null plus the state that
    /// says why — never a sentinel like -1, which a reader cannot tell from a
    /// value.</summary>
    private static void NullableInt(Utf8JsonWriter json, string name, int? value, string absentState)
    {
        if (value is { } v)
        {
            json.WriteNumber(name, v);
            json.WriteString(name + "State", "recorded");
        }
        else
        {
            json.WriteNull(name);
            json.WriteString(name + "State", absentState);
        }
    }

    /// <summary>A string that may be absent: explicit null plus its state.</summary>
    private static void NullableString(Utf8JsonWriter json, string name, string? value, string absentState)
    {
        if (value is { Length: > 0 })
        {
            json.WriteString(name, value);
            json.WriteString(name + "State", "recorded");
        }
        else
        {
            json.WriteNull(name);
            json.WriteString(name + "State", absentState);
        }
    }
}
