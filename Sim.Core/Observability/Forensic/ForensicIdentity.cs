using System.Globalization;
using System.Reflection;
using System.Security.Cryptography;
using System.Text;

namespace Sim.Core.Observability.Forensic;

/// <summary>
/// IDENTITY AND PROVENANCE, computed from content and never from a clock.
///
/// THE DEFECT THIS CLOSES. The only identity a session has today is the wall
/// clock stamp in its filenames. Two otherwise-identical sessions therefore
/// carry different identities, the same session's files are bound to each other
/// by that stamp alone, and a companion file swapped for a same-named one is
/// undetectable. Worse, a wall-clock identity cannot be RECOMPUTED, so nothing
/// can ever check it.
///
/// The run id here is a pure function of what defines the world: the seed, the
/// founding overrides, the canonical schema version, the digest of every
/// embedded configuration resource, and the digest of the order log. Recompute
/// it from a manifest, an orders file and this build's configs and a mismatch
/// becomes a NAMED finding — "this orders log is not the one that session
/// played", or "this build's configuration differs from the recorded one" —
/// instead of surfacing downstream as a determinism failure that is not one.
///
/// DELIBERATELY EXCLUDED, each for a stated reason:
///   - THE WALL CLOCK, so the id is reproducible. The filename may still carry
///     a stamp; the IDENTITY FIELD does not.
///   - THE BUILD SHA, because it is the literal string "dev" on every locally
///     built binary, which would collide every locally played session with
///     every other. The build is recorded beside the id, not inside it.
///   - THE TURN COUNT, which is an outcome and not an input; it lives on the
///     close record.
///
/// NOTHING HERE READS A CLOCK, A RUNTIME OR A RANDOM SOURCE.
/// </summary>
public static class ForensicIdentity
{
    /// <summary>The preimage recipe, printed IN the artifact so the id can be
    /// recomputed by someone who has only the file.</summary>
    public const string RunIdInputs =
        "sha256( \"civ-sim/forensic/v1\" | seed | sizePx | settlements | founded | canonicalSchemaVersion "
        + "| configDigest | ordersDigest ), each field length-prefixed and UTF-8 encoded, first 16 lowercase "
        + "hex characters. No wall clock, no build sha, no turn count.";

    /// <summary>Lowercase hex of a SHA-256 over the bytes.</summary>
    public static string Sha256Hex(ReadOnlySpan<byte> bytes)
    {
        Span<byte> digest = stackalloc byte[32];
        SHA256.HashData(bytes, digest);
        return Convert.ToHexStringLower(digest);
    }

    /// <summary>Lowercase hex of a SHA-256 over a stream, read to the end.</summary>
    public static string Sha256Hex(Stream stream)
    {
        ArgumentNullException.ThrowIfNull(stream);
        return Convert.ToHexStringLower(SHA256.HashData(stream));
    }

    /// <summary>Lowercase hex of the SHA-256 of a file, or null when it is
    /// absent — an absent companion is stated, never guessed at.</summary>
    public static string? Sha256HexOfFile(string path)
    {
        if (!File.Exists(path)) return null;
        using FileStream file = File.OpenRead(path);
        return Sha256Hex(file);
    }

    /// <summary>
    /// Every embedded configuration resource of a content assembly, by content,
    /// in ORDINAL NAME ORDER — a fixed order, never the assembly's own, so the
    /// digest below is stable. Reads resources; reads no world and no clock.
    /// </summary>
    public static ConfigResource[] ConfigResources(Assembly contentAssembly, string prefix = "Sim.Data.")
    {
        ArgumentNullException.ThrowIfNull(contentAssembly);
        string[] names = contentAssembly.GetManifestResourceNames();
        Array.Sort(names, StringComparer.Ordinal);

        int kept = 0;
        for (int i = 0; i < names.Length; i++)
            if (names[i].StartsWith(prefix, StringComparison.Ordinal)) kept++;

        var resources = new ConfigResource[kept];
        int j = 0;
        for (int i = 0; i < names.Length; i++)
        {
            if (!names[i].StartsWith(prefix, StringComparison.Ordinal)) continue;
            using Stream? stream = contentAssembly.GetManifestResourceStream(names[i]);
            if (stream is null)
            {
                resources[j++] = new ConfigResource(names[i][prefix.Length..], 0, "");
                continue;
            }
            using var buffer = new MemoryStream();
            stream.CopyTo(buffer);
            byte[] bytes = buffer.ToArray();
            resources[j++] = new ConfigResource(names[i][prefix.Length..], bytes.LongLength, Sha256Hex(bytes));
        }
        return resources;
    }

    /// <summary>One digest over the ordered (file, sha256) pairs — the whole
    /// configuration of the build, as one comparable value.</summary>
    public static string ConfigDigest(ConfigResource[] resources)
    {
        ArgumentNullException.ThrowIfNull(resources);
        var sb = new StringBuilder();
        for (int i = 0; i < resources.Length; i++)
            Field(sb, resources[i].File + "=" + resources[i].Sha256);
        return Sha256Hex(Encoding.UTF8.GetBytes(sb.ToString()));
    }

    /// <summary>
    /// The digest of an order log AS IT STANDS. Taken over the log's own
    /// serialized bytes through its own writer, so it is the identity of the
    /// artifact a replay would be handed, not of a second encoding of it.
    /// </summary>
    public static string OrdersDigest(Sim.Core.Kernel.OrderLog orders)
    {
        ArgumentNullException.ThrowIfNull(orders);
        using var buffer = new MemoryStream();
        orders.Save(buffer);
        return Sha256Hex(buffer.ToArray());
    }

    /// <summary>
    /// THE RUN ID: first 16 lowercase hex characters of the SHA-256 over the
    /// length-prefixed, fixed-order preimage described by <see cref="RunIdInputs"/>.
    /// A pure function of its arguments — the same inputs give the same id on
    /// any machine, on any day, on any build.
    /// </summary>
    public static string RunId(
        ulong seed, int? sizePx, int? settlements, bool founded,
        int canonicalSchemaVersion, string configDigest, string ordersDigest)
    {
        var sb = new StringBuilder();
        Field(sb, "civ-sim/forensic/v1");
        Field(sb, seed.ToString(CultureInfo.InvariantCulture));
        Field(sb, sizePx is { } px ? px.ToString(CultureInfo.InvariantCulture) : "absent");
        Field(sb, settlements is { } n ? n.ToString(CultureInfo.InvariantCulture) : "absent");
        Field(sb, founded ? "1" : "0");
        Field(sb, canonicalSchemaVersion.ToString(CultureInfo.InvariantCulture));
        Field(sb, configDigest);
        Field(sb, ordersDigest);
        return Sha256Hex(Encoding.UTF8.GetBytes(sb.ToString()))[..16];
    }

    /// <summary>Length-prefixed, so no two different field lists can ever
    /// produce the same preimage by concatenation.</summary>
    private static void Field(StringBuilder sb, string value)
        => sb.Append(value.Length.ToString(CultureInfo.InvariantCulture)).Append(':').Append(value).Append('|');
}
