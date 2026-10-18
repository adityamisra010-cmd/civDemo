using System.Security.Cryptography;
using Sim.Core.Kernel;
using Sim.Core.State;

namespace Sim.Tests.TestUtil;

/// <summary>
/// D-054 / ADR-039 — THE v32 PROJECTION of a v33 world: the canonical stream as schema v32 wrote it. Schema v33 APPENDED
/// two tables after TaxGrievances (section 59 ConstructionProgress, section 60 ResourceAllocations) and changed no
/// existing row, so a world whose two v33 tables are empty serializes to the v32 stream followed by exactly two empty
/// count prefixes (8 zero bytes). Dropping them returns the v32 stream byte for byte; its SHA-256 is the hash a v32 build
/// reported (WorldHash is SHA-256 over this stream — the version number and the snapshot's topology trailer are not in
/// it). Every pin measured before v33 compares through it; a populated v33 table is a behaviour change the projection
/// must not hide, so <see cref="V32"/> asserts the strip removed nothing.
/// </summary>
public static class SchemaProjection
{
    /// <summary>The two tables v33 appended (ConstructionProgress, ResourceAllocations).</summary>
    public const int V33TableCount = 2;

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

    /// <summary>The world's hash at schema v32: a Clone with the v33 tables stripped, re-serialized, the two empty v33
    /// count prefixes dropped (asserted zero), hashed. <paramref name="v33RowsRemoved"/> counts what the strip removed.</summary>
    public static string HashAtSchemaV32(WorldState world, out int v33RowsRemoved)
    {
        WorldState stripped = world.Clone();
        v33RowsRemoved = StripV33(stripped);
        return HashDroppingTrailer(Serialize(stripped), V33TableCount);
    }

    /// <summary>A pre-v33 pin's comparison form: the v32 hash of a world with NO v33 row (asserted — a world with
    /// incremental-construction progress or a material split is a behaviour change, never a layout one).</summary>
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
