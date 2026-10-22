using Sim.Core.State;

namespace Sim.Core.Kernel;

/// <summary>Raised when a save cannot be loaded, with an actionable message.</summary>
public sealed class SnapshotFormatException(string message) : Exception(message);

/// <summary>
/// Versioned binary snapshots (D-008, §3.8). THE v33 FILE LAYOUT (ADR-039):
/// <c>header {magic "CIVSNAP\0", int32 version, uint64 seed, int64 turn} | canonical state stream (sections 1..60) |
/// int32 topology</c> — the topology trailer (<see cref="TopologyFlags"/>: bit 0 = the world wraps east-west; 0 for a
/// terrain-less world) binds the save to its world's topology, which the terrain content hash does not cover. The
/// trailer is outside the canonical stream, so the world hash never sees it.
///
/// THE FIRST EXPLICIT MIGRATION (D-054 / ADR-039, superseding "no migration code"): <see cref="Load"/> accepts version
/// 33 and version 32. A v32 file is the header plus a stream that ends after section 58 and NO trailer: it is read by
/// CanonicalSchema.Read(reader, 32, …), its two v33 tables are left empty (exact — under v32 no construction entry had
/// partial progress and no Empire had a split), and it loads as a FLAT world (wrap 0), so the caller must supply a flat
/// TerrainSet. Every other version is refused with the D-008 message: seed + order-log replay remains the recovery
/// path between milestones.
///
/// D-055 (schema v34): THE v34 FILE LAYOUT is the v33 one with the canonical stream running to section 61
/// (ConstructionHolds). <see cref="Load"/> accepts the readable SET {32, 33, 34} (CanonicalSchema.IsReadable, the one
/// helper): a v33 file's stream ends after section 60 and its ConstructionHolds table is left empty — exact in every
/// pipeline the executor composes (D-056: road development's read of the table is a declared same-step edge, so
/// construction clears and rebuilds it before road development reads it; the shipped order is pinned by
/// ConstructionHoldsSchemaV34Tests.TheShippedPipeline_RunsConstructionBeforeRoadDevelopment, and the road-first order in
/// which a v33 load and a v34 save of the same state continued differently is refused by TurnExecutor) — and it carries
/// the topology trailer as a v34 file does. A loaded ConstructionHolds table that no rule could have written (unsorted or duplicate (Purse, Good) keys,
/// Units ≤ 0, Good &lt; 0) is refused (State.Reservations.InvariantViolation).
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
        writer.Write(TopologyFlags.Of(world));   // v33 trailer (ADR-039)
    }

    /// <summary>
    /// Loads a save. Terrain is not stored in saves (ADR-008): a save of a world
    /// WITH terrain records only its content hash — pass the regenerated
    /// <paramref name="terrain"/> (from the same seed + worldgen config) and it is
    /// validated against the stored hash and attached; a mismatch or a missing
    /// argument fails actionably.
    /// </summary>
    public static WorldState Load(Stream source, Worldgen.TerrainSet? terrain = null)
    {
        using var reader = new BinaryReader(source, System.Text.Encoding.UTF8, leaveOpen: true);

        Span<byte> magic = stackalloc byte[8];
        if (reader.Read(magic) != 8 || !magic.SequenceEqual(Magic))
            throw new SnapshotFormatException(
                "not a civ-sim save: bad magic (expected CIVSNAP header).");

        int version;
        ulong headerSeed;
        long headerTurn;
        try
        {
            version = reader.ReadInt32();
            if (CanonicalSchema.IsReadable(version))
            {
                headerSeed = reader.ReadUInt64();
                headerTurn = reader.ReadInt64();
            }
            else (headerSeed, headerTurn) = (0UL, 0L);
        }
        catch (EndOfStreamException)
        {
            throw new SnapshotFormatException("save is truncated: its header (version, seed, turn) is incomplete."); // D-056
        }
        if (!CanonicalSchema.IsReadable(version))
            throw new SnapshotFormatException(
                $"save is schema version {version}, this build reads only versions " +
                $"32, 33 and 34 (migrated) and {CanonicalSchema.Version}; saves break " +
                "between milestones by design (D-008) — no migration exists for this version. Re-derive the world " +
                "via seed + order-log replay.");

        WorldState world;
        byte[]? expectedTerrainHash;
        try
        {
            world = CanonicalSchema.Read(reader, version, out expectedTerrainHash);
        }
        catch (Exception e) when (e is EndOfStreamException or InvalidDataException)
        {
            // D-056: a stream that ends inside its tables, or a row count no writer emits, is a corrupt save — reported as
            // one, never as a bare reader exception.
            throw new SnapshotFormatException("save is truncated or corrupt: its state stream cannot be read to the end ("
                + (e is EndOfStreamException ? "it ends inside a table" : e.Message.TrimEnd('.')) + ").");
        }

        // The topology trailer (ADR-039; v33 and v34 alike); a v32 save has none and is flat.
        int topology = 0;
        if (version >= 33)
        {
            try
            {
                topology = reader.ReadInt32();
            }
            catch (EndOfStreamException)
            {
                throw new SnapshotFormatException(
                    "save is truncated: the schema-v33 topology trailer after the state stream is missing.");
            }
            if ((topology & ~TopologyFlags.Known) != 0)
                throw new SnapshotFormatException(
                    $"save carries unknown topology bits 0x{topology:X8}; this build knows only bit 0 (east-west wrap).");
            if ((topology & TopologyFlags.WrapX) != 0 && expectedTerrainHash is null)
                throw new SnapshotFormatException(
                    "save is corrupt: it records an east-west wrapping world but was made without terrain — only a " +
                    "world with terrain can wrap.");
        }

        // D-056: nothing follows the last field (the stream for v32, the topology trailer from v33): bytes after it mean
        // the file is not one this or any build wrote.
        if (reader.Read(new byte[1], 0, 1) > 0)
            throw new SnapshotFormatException(
                "save is corrupt: it carries bytes after its last field (" + (version >= 33 ? "the topology trailer" : "the state stream") + ").");

        AssertProgressInvariant(world);

        // Header is redundant with the stream — treat disagreement as corruption.
        if (world.Seed != headerSeed || world.Clock.Turn != headerTurn)
            throw new SnapshotFormatException(
                $"save is corrupt: header (seed {headerSeed}, turn {headerTurn}) disagrees with " +
                $"state stream (seed {world.Seed}, turn {world.Clock.Turn}).");

        if (expectedTerrainHash is not null)
        {
            if (terrain is null)
                throw new SnapshotFormatException(
                    "save was made against generated terrain (ADR-008): regenerate the TerrainSet " +
                    "from the same seed + worldgen config and pass it to Snapshot.Load.");
            if (!terrain.ContentHash.AsSpan().SequenceEqual(expectedTerrainHash))
                throw new SnapshotFormatException(
                    "terrain mismatch: the provided TerrainSet's content hash does not equal the " +
                    "hash this save was made against — wrong seed or worldgen config.");
            bool savedWraps = (topology & TopologyFlags.WrapX) != 0;
            if (savedWraps != TopologyFlags.TerrainWrapsX(terrain))
                throw new SnapshotFormatException(
                    (savedWraps
                        ? "topology mismatch: the save was made on an east-west WRAPPING world but the provided terrain is flat"
                        : "topology mismatch: the save was made on a FLAT world (every schema-v32 save is flat) but the provided terrain wraps east-west")
                    + " — regenerate the TerrainSet with the matching worldgen topology (worldgen.json topology.wrapX) and pass it to Snapshot.Load.");
            world.Terrain = terrain;
        }
        else if (terrain is not null)
        {
            throw new SnapshotFormatException(
                "save was made without terrain, but a TerrainSet was provided — refusing to " +
                "attach terrain the saved state never saw.");
        }

        return world;
    }

    /// <summary>
    /// The v33 construction invariant (ConstructionQuery.ProgressInvariantViolation — the one statement ConstructionSystem
    /// also asserts at the end of every step): queue slots unique per settlement; every progress row in [0, N − 1], unique
    /// per (Settlement, Slot) and matched by a queue row with the same (Settlement, Slot, ProjectId). A save breaking it was
    /// not written by ConstructionSystem and is refused rather than loaded into a state no rule produces (a progress of N
    /// would never complete, a negative one would draw more than the cost, a duplicate row would throw mid-step).
    /// </summary>
    private static void AssertProgressInvariant(WorldState world)
    {
        if (ConstructionQuery.ProgressInvariantViolation(world.ConstructionQueue, world.ConstructionProgress) is { } broken)
            throw new SnapshotFormatException("save is corrupt: " + broken + " (schema v33 invariant).");
        if (ResourceAllocation.InvariantViolation(world.ResourceAllocations) is { } split)
            throw new SnapshotFormatException("save is corrupt: " + split + " (schema v33 invariant).");
        // D-055 (v34): the published holds as only construction writes them — ascending unique (Purse, Good), Units > 0,
        // Good ≥ 0. The table is never an input (construction rebuilds it before road development reads it — D-056, an
        // edge TurnExecutor enforces), and a save no rule wrote is refused rather than loaded, as the progress and split
        // tables are.
        if (Reservations.InvariantViolation(world.ConstructionHolds) is { } holds)
            throw new SnapshotFormatException("save is corrupt: " + holds + " (schema v34 invariant).");
        // M6 EX (v35, ADR-044): the four discovery tables as only DiscoverySystem writes them — strictly ascending keys,
        // no zero extent word, radii and turns in range. A save no rule wrote is refused.
        if (DiscoveryQuery.InvariantViolation(world) is { } discovery)
            throw new SnapshotFormatException("save is corrupt: " + discovery + " (schema v35 invariant).");
    }
}
