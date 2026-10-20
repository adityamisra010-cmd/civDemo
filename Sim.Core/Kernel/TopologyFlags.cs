using Sim.Core.State;
using Sim.Core.Worldgen;

namespace Sim.Core.Kernel;

/// <summary>
/// ADR-039 / D-054 — THE SNAPSHOT'S TOPOLOGY TRAILER (schema v33): an int32 written after the canonical stream that
/// binds a save to its world's topology, which the terrain content hash does not cover. Bit 0 = <see cref="WrapX"/>
/// (the world is an east-west cylinder, WS3); every other bit is reserved and refused by Snapshot.Load. A terrain-less
/// world is flat (0).
///
/// ONE SEAM FOR THE WRAP FLAG. <see cref="TerrainWrapsX"/> is the only reader of a TerrainSet's topology here: it reads
/// <see cref="TerrainSet.WrapX"/> (set by Worldgen.Generate from worldgen.json topology.wrapX — the only reader of the
/// config switch; WS3). Wired at the batch-3 integration (2026-10-14): until then the construction track wrote 0 here.
/// Snapshot.Load enforces the agreement rule against the supplied terrain.
/// </summary>
public static class TopologyFlags
{
    /// <summary>Bit 0: the world wraps east-west.</summary>
    public const int WrapX = 1;

    /// <summary>Every bit this build defines; anything else in a trailer is refused.</summary>
    public const int Known = WrapX;

    /// <summary>The trailer value for <paramref name="world"/>: <see cref="WrapX"/> when its terrain wraps east-west,
    /// else 0 (always 0 for a terrain-less world).</summary>
    public static int Of(IReadOnlyWorldState world) => world.Terrain is { } terrain && TerrainWrapsX(terrain) ? WrapX : 0;

    /// <summary>Whether <paramref name="terrain"/> is an east-west cylinder: <see cref="TerrainSet.WrapX"/> (false for no
    /// terrain).</summary>
    public static bool TerrainWrapsX(TerrainSet? terrain) => terrain is { WrapX: true };

    /// <summary>The first canonical schema whose worlds can be east-west cylinders (D-054: the cylinder, WS3, and this
    /// trailer arrived together in v33).</summary>
    public const int FirstCylinderSchema = 33;

    /// <summary>
    /// Fix round 1 (D-054, verifier F1) — THE TOPOLOGY A RECORDED SCHEMA IMPLIES: every world recorded under a schema before
    /// <see cref="FirstCylinderSchema"/> was a flat square raster, so a save or session that schema recorded is rebuilt flat.
    /// One rule for every reader of a record: the CLI's .civsnap loader (a v32 file has no trailer), `sim inspect` and
    /// UiSession.Resume (a session manifest records only its schema). A record of v33 or later is rebuilt with the
    /// topology it records (a snapshot's trailer) or, for a session, the shipped worldgen it was founded with.
    /// </summary>
    public static bool RecordedFlat(int schemaVersion) => schemaVersion < FirstCylinderSchema;
}
