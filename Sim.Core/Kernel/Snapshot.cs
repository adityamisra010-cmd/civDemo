using Sim.Core.State;

namespace Sim.Core.Kernel;

/// <summary>Raised when a save cannot be loaded, with an actionable message.</summary>
public sealed class SnapshotFormatException(string message) : Exception(message);

/// <summary>
/// Versioned binary snapshots (D-008, §3.8): header {magic, version, seed, turn}
/// followed by the canonical state stream. No migration code — saves break
/// between milestones by design; seed + order-log replay is the recovery path.
/// </summary>
public static class Snapshot
{
    /// <summary>"CIVSNAP\0" — identifies a civ-sim save.</summary>
    private static ReadOnlySpan<byte> Magic => "CIVSNAP\0"u8;

    public static void Save(WorldState world, Stream destination)
    {
        using var writer = new BinaryWriter(destination, System.Text.Encoding.UTF8, leaveOpen: true);
        writer.Write(Magic);
        writer.Write(CanonicalSchema.Version);
        writer.Write(world.Seed);
        writer.Write(world.Clock.Turn);
        CanonicalSchema.Write(world, writer);
    }

    public static WorldState Load(Stream source)
    {
        using var reader = new BinaryReader(source, System.Text.Encoding.UTF8, leaveOpen: true);

        Span<byte> magic = stackalloc byte[8];
        if (reader.Read(magic) != 8 || !magic.SequenceEqual(Magic))
            throw new SnapshotFormatException(
                "not a civ-sim save: bad magic (expected CIVSNAP header).");

        int version = reader.ReadInt32();
        if (version != CanonicalSchema.Version)
            throw new SnapshotFormatException(
                $"save is schema version {version}, this build reads only version " +
                $"{CanonicalSchema.Version}; saves break between milestones by design (D-008) — " +
                "no migration exists. Re-derive the world via seed + order-log replay.");

        ulong headerSeed = reader.ReadUInt64();
        long headerTurn = reader.ReadInt64();

        WorldState world = CanonicalSchema.Read(reader);

        // Header is redundant with the stream — treat disagreement as corruption.
        if (world.Seed != headerSeed || world.Clock.Turn != headerTurn)
            throw new SnapshotFormatException(
                $"save is corrupt: header (seed {headerSeed}, turn {headerTurn}) disagrees with " +
                $"state stream (seed {world.Seed}, turn {world.Clock.Turn}).");

        return world;
    }
}
