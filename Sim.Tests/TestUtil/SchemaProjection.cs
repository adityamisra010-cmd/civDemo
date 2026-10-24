using System.Security.Cryptography;
using Sim.Core.Kernel;
using Sim.Core.State;

namespace Sim.Tests.TestUtil;

/// <summary>
/// D-054 / ADR-039 — THE v32 PROJECTION of a current world: the canonical stream as schema v32 wrote it. Schema v33
/// APPENDED two tables after TaxGrievances (section 59 ConstructionProgress, section 60 ResourceAllocations) and changed
/// no existing row, so a world whose v33 tables are empty serializes to the v32 stream followed by their empty count
/// prefixes. Dropping them returns the v32 stream byte for byte; its SHA-256 is the hash a v32 build reported (WorldHash
/// is SHA-256 over this stream — the version number and the snapshot's topology trailer are not in it). Every pin
/// measured before v33 compares through it; a populated v33 table is a behaviour change the projection must not hide,
/// so <see cref="V32"/> asserts the strip removed nothing.
///
/// D-055 (schema v34, 2026-10-16) — THE v33 PROJECTION, the same construction one schema later. v34 APPENDED one table
/// after ResourceAllocations (section 61 ConstructionHolds) and changed no existing row, so a world with no hold row
/// serializes to the v33 stream plus one empty count prefix (4 zero bytes); <see cref="HashAtSchemaV33(WorldState, out int)"/>
/// drops it and returns the hash the batch-3 (v33) build reported. A hold row is BEHAVIOUR (reservations on), never
/// layout, so <see cref="V33"/> asserts the strip removed nothing — the batch-3 pins are reached through it, run under
/// TestConfigs.PreReservations where the world could hold. <see cref="V32"/> keeps working: it now drops sections 59,
/// 60 AND 61, and asserts all three empty.
///
/// ADR-043 (schema v35, 2026-10-22) — THE v34 PROJECTION, the same construction one schema later. v35 APPENDED one table
/// after ConstructionHolds (section 62 CivicsPoints) and changed no existing row, so a world with no CP account serializes
/// to the v34 stream plus one empty count prefix; <see cref="HashAtSchemaV34(WorldState, out int)"/> drops it and returns
/// the hash the v34 build reported. A CP row is BEHAVIOUR (the two-lane model on), never layout, so <see cref="V34"/>
/// asserts the strip removed nothing — the v34 pins are reached through it under TestConfigs.PreCivicsPoints, the
/// one-pool twin. <see cref="V33"/> and <see cref="V32"/> now also drop section 62 and assert it empty.
///
/// M6 Track EX (schema v35, 2026-10-22; merged with Track CP): v35 also appends the four discovery tables (sections 63–66)
/// after section 62; a discovery row is BEHAVIOUR (fog or concealment on), so every projection strips sections 62–66 and
/// asserts them empty — the v34 pins are reached under TestConfigs.PreM6 (both M6 tracks off).
///
/// ADR-049 (schema v36, D-063 R4) — THE v35 PROJECTION, the same construction one schema later. v36 APPENDED one table
/// after section 66 (section 67 ResearchLevels) and changed no existing row, so a world with no repeatable level serializes
/// to the v35 stream plus one empty count prefix; <see cref="HashAtSchemaV35(WorldState, out int)"/> drops it and returns
/// the hash the v35 build reported. A level row is BEHAVIOUR (tuning.repeatableLevels on), never layout, so
/// <see cref="V35"/> asserts the strip removed nothing — with the switch off (the shipped value on the t2-levels branch)
/// every v35 pin is reached through it unchanged. Every older projection now also drops section 67 and asserts it empty.
/// </summary>
public static class SchemaProjection
{
    /// <summary>The two tables v33 appended (ConstructionProgress, ResourceAllocations).</summary>
    public const int V33TableCount = 2;

    /// <summary>The one table v34 appended (ConstructionHolds).</summary>
    public const int V34TableCount = 1;

    /// <summary>The tables v35 appended: CivicsPoints (section 62, Track CP) and the four M6 EX discovery tables
    /// (sections 63–66: KnownExtents, Explorations, SettlementSightings, SettlementSurveys).</summary>
    public const int V35TableCount = 5;

    /// <summary>The table v36 appended: ResearchLevels (section 67, ADR-049).</summary>
    public const int V36TableCount = 1;

    /// <summary>Bytes one empty table contributes to the stream: its count prefix.</summary>
    private const int EmptyTableBytes = 4;

    /// <summary>Clears the two v33 tables IN PLACE (their entire footprint in the stream). Returns the rows removed.</summary>
    public static int StripV33(WorldState stripped)
    {
        int removed = stripped.ConstructionProgress.Count + stripped.ResourceAllocations.Count;
        stripped.ConstructionProgress.Clear();
        stripped.ResourceAllocations.Clear();
        return removed;
    }

    /// <summary>Clears the v34 table IN PLACE (its entire footprint in the stream). Returns the rows removed.</summary>
    public static int StripV34(WorldState stripped)
    {
        int removed = stripped.ConstructionHolds.Count;
        stripped.ConstructionHolds.Clear();
        return removed;
    }

    /// <summary>Clears the v35 table IN PLACE (its entire footprint in the stream). Returns the rows removed.</summary>
    public static int StripV35(WorldState stripped)
    {
        int removed = stripped.CivicsPoints.Count + stripped.KnownExtents.Count + stripped.Explorations.Count
            + stripped.SettlementSightings.Count + stripped.SettlementSurveys.Count;
        stripped.CivicsPoints.Clear();
        stripped.KnownExtents.Clear();
        stripped.Explorations.Clear();
        stripped.SettlementSightings.Clear();
        stripped.SettlementSurveys.Clear();
        return removed;
    }

    /// <summary>Clears the v36 table IN PLACE (its entire footprint in the stream). Returns the rows removed.</summary>
    public static int StripV36(WorldState stripped)
    {
        int removed = stripped.ResearchLevels.Count;
        stripped.ResearchLevels.Clear();
        return removed;
    }

    /// <summary>The world's hash at schema v35: a Clone with the v36 table stripped, re-serialized, its empty count prefix
    /// dropped (asserted zero), hashed. <paramref name="v36RowsRemoved"/> counts what the strip removed.</summary>
    public static string HashAtSchemaV35(WorldState world, out int v36RowsRemoved)
    {
        WorldState stripped = world.Clone();
        v36RowsRemoved = StripV36(stripped);
        return HashDroppingTrailer(Serialize(stripped), V36TableCount);
    }

    /// <summary>A v35 pin's comparison form: the v35 hash of a world with NO repeatable level (asserted — a level row is
    /// tuning.repeatableLevels' behaviour, never layout).</summary>
    public static string V35(WorldState world)
    {
        string hash = HashAtSchemaV35(world, out int removed);
        Xunit.Assert.Equal(0, removed);
        return hash;
    }

    /// <summary>The world's hash at schema v34: a Clone with the v36 and v35 tables stripped, re-serialized, their empty
    /// count prefixes dropped (asserted zero), hashed. <paramref name="v35RowsRemoved"/> counts what the strip removed.</summary>
    public static string HashAtSchemaV34(WorldState world, out int v35RowsRemoved)
    {
        WorldState stripped = world.Clone();
        v35RowsRemoved = StripV36(stripped) + StripV35(stripped);
        return HashDroppingTrailer(Serialize(stripped), V35TableCount + V36TableCount);
    }

    /// <summary>A v34 pin's comparison form: the v34 hash of a world with NO Civics Points account (asserted — an account
    /// is the two-lane model's behaviour, never layout).</summary>
    public static string V34(WorldState world)
    {
        string hash = HashAtSchemaV34(world, out int removed);
        Xunit.Assert.Equal(0, removed);
        return hash;
    }

    /// <summary>The world's hash at schema v33: a Clone with the v35 and v34 tables stripped, re-serialized, its empty count
    /// prefix dropped (asserted zero), hashed. <paramref name="v34RowsRemoved"/> counts what the strip removed.</summary>
    public static string HashAtSchemaV33(WorldState world, out int v34RowsRemoved)
    {
        WorldState stripped = world.Clone();
        v34RowsRemoved = StripV36(stripped) + StripV35(stripped) + StripV34(stripped);
        return HashDroppingTrailer(Serialize(stripped), V34TableCount + V35TableCount + V36TableCount);
    }

    /// <summary>A batch-3 (v33) pin's comparison form: the v33 hash of a world with NO hold row (asserted — a published
    /// hold is a behaviour change, never a layout one).</summary>
    public static string V33(WorldState world)
    {
        string hash = HashAtSchemaV33(world, out int removed);
        Xunit.Assert.Equal(0, removed);
        return hash;
    }

    /// <summary>The world's hash at schema v32: a Clone with the v34 and v33 tables stripped, re-serialized, their three
    /// empty count prefixes dropped (asserted zero), hashed. <paramref name="v33RowsRemoved"/> counts what the strip
    /// removed from all three tables.</summary>
    public static string HashAtSchemaV32(WorldState world, out int v33RowsRemoved)
    {
        WorldState stripped = world.Clone();
        v33RowsRemoved = StripV36(stripped) + StripV35(stripped) + StripV34(stripped) + StripV33(stripped);
        return HashDroppingTrailer(Serialize(stripped), V33TableCount + V34TableCount + V35TableCount + V36TableCount);
    }

    /// <summary>A pre-v33 pin's comparison form: the v32 hash of a world with NO v33 or v34 row (asserted — a world with
    /// incremental-construction progress, a material split or a hold is a behaviour change, never a layout one).</summary>
    public static string V32(WorldState world)
    {
        string hash = HashAtSchemaV32(world, out int removed);
        Xunit.Assert.Equal(0, removed);
        return hash;
    }

    /// <summary>The canonical stream of <paramref name="world"/>.</summary>
    public static byte[] Serialize(WorldState world)
    {
        using var buffer = new MemoryStream();
        using (var writer = new BinaryWriter(buffer, System.Text.Encoding.UTF8, leaveOpen: true))
            CanonicalSchema.Write(world, writer);
        return buffer.ToArray();
    }

    /// <summary>SHA-256 (lower hex) of <paramref name="full"/> with its last <paramref name="dropTrailingTables"/> empty
    /// count prefixes removed — every dropped byte asserted 0, so what is dropped really is empty tables.</summary>
    public static string HashDroppingTrailer(byte[] full, int dropTrailingTables)
    {
        int drop = dropTrailingTables * EmptyTableBytes;
        for (int i = full.Length - drop; i < full.Length; i++)
            Xunit.Assert.Equal(0, full[i]);
        return Convert.ToHexStringLower(SHA256.HashData(full.AsSpan(0, full.Length - drop)));
    }
}
