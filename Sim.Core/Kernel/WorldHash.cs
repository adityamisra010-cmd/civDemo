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
}
