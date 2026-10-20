using System.Security.Cryptography;
using Sim.Core.State;

namespace Sim.Core.Kernel;

/// <summary>
/// SHA-256 over the canonical state stream (§3.8) — the single function behind
/// saves, the determinism harness (T0.8), and golden-run regression. The save
/// header is NOT part of the hash; two states hash equal iff their canonical
/// streams are byte-identical.
/// </summary>
public static class WorldHash
{
    public static byte[] Compute(WorldState world)
    {
        using var buffer = new MemoryStream();
        using (var writer = new BinaryWriter(buffer, System.Text.Encoding.UTF8, leaveOpen: true))
        {
            CanonicalSchema.Write(world, writer);
        }
        buffer.Position = 0;
        return SHA256.HashData(buffer.ToArray());
    }

    /// <summary>Lowercase hex form, for golden constants and reports.</summary>
    public static string ComputeHex(WorldState world) => Convert.ToHexStringLower(Compute(world));

    /// <summary>
    /// D-056 — the hash a build of schema <paramref name="version"/> (a member of the readable set,
    /// <see cref="CanonicalSchema.IsReadable"/>) reported for this world's tables: SHA-256 over the canonical stream less
    /// the sections appended since that version (<see cref="CanonicalSchema.BytesAppendedSince"/>). Equal to
    /// <see cref="ComputeHex"/> for <see cref="CanonicalSchema.Version"/>. Equality with a recorded hash proves every table
    /// that version serialized is byte-identical (the tables it lacks are not compared): what a replay guard needs to know
    /// that a recorded order means here what it meant when it was recorded (ReplayGuard).
    /// </summary>
    public static string ComputeHexAsSchema(WorldState world, int version)
    {
        int drop = CanonicalSchema.BytesAppendedSince(world, version);
        using var buffer = new MemoryStream();
        using (var writer = new BinaryWriter(buffer, System.Text.Encoding.UTF8, leaveOpen: true))
        {
            CanonicalSchema.Write(world, writer);
        }
        return Convert.ToHexStringLower(SHA256.HashData(buffer.GetBuffer().AsSpan(0, (int)buffer.Length - drop)));
    }
}
