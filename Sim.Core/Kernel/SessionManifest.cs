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
    string TraceFile)
{
    /// <summary>The schema tag, so a reader can tell which vintage produced a
    /// file it did not write.</summary>
    public const string Schema = "session-manifest/v1";

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
        json.WriteEndObject();
        json.Flush();
    }

    /// <summary>
    /// Reads a manifest back. Throws with the offending file named when the tag
    /// is missing or wrong — a reader that silently accepts an unknown vintage
    /// would replay the wrong world and report the difference as a finding.
    /// </summary>
    public static SessionManifest Read(Stream input, string describedAs)
    {
        using JsonDocument doc = JsonDocument.Parse(input);
        JsonElement root = doc.RootElement;

        string? schema = root.TryGetProperty("schema", out JsonElement s) ? s.GetString() : null;
        if (schema != Schema)
        {
            throw new InvalidDataException(
                $"{describedAs} is not a {Schema} file (found '{schema ?? "no schema tag"}').");
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
            TraceFile: root.GetProperty("traceFile").GetString() ?? "");
    }

    private static int? Nullable(JsonElement root, string name)
        => root.TryGetProperty(name, out JsonElement e) && e.ValueKind == JsonValueKind.Number
            ? e.GetInt32()
            : null;
}
