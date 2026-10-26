using Sim.Cli;
using Sim.Core;
using Sim.Core.Kernel;
using Sim.Core.State;
using Sim.Core.Systems;
using Sim.Tests.TestUtil;

namespace Sim.Tests.State;

/// <summary>
/// ADR-043 (D-062, 2026-10-22) — SCHEMA v35, Track CP's part: section 62 CivicsPoints (Polity, BalanceMilli,
/// GeneratedMilli, SpentMilli; 28 bytes) appended after ConstructionHolds; the readable SET {32, 33, 34, 35} through the
/// one helper; the v34 → v35 migration ("empty tables": every bank starts at zero); the Snapshot.Load refusals of a CP
/// table no rule writes (unsorted or duplicate holders, a negative field, Balance ≠ Generated − Spent).
/// Constitution rule: a new serialized row type ships a POPULATED-table test (exact ExpectedLength, a bit-exact round
/// trip, hash equality) — here with a city-state's negative reserved key and long.MaxValue fields.
/// </summary>
public class CivicsPointsSchemaV35Tests
{
    /// <summary>Four accounts in ascending Polity order: a city-state's reserved local key (−1 − 4 = −5), two Empires, and
    /// an extreme row — each satisfying Balance == Generated − Spent.</summary>
    private static WorldState Populated()
    {
        var w = new WorldState(35);
        w.CivicsPoints.Add(new CivicsPointsRow(SettlementKnowledge.LocalHolder(new SettlementId(4)), 250, 250, 0));
        w.CivicsPoints.Add(new CivicsPointsRow(new PolityId(1), 1_500_000, 4_000_000, 2_500_000));
        w.CivicsPoints.Add(new CivicsPointsRow(new PolityId(2), 0, 7, 7));
        w.CivicsPoints.Add(new CivicsPointsRow(new PolityId(int.MaxValue), long.MaxValue, long.MaxValue, 0));
        return w;
    }

    private static byte[] Saved(WorldState w)
    {
        using var ms = new MemoryStream();
        Snapshot.Save(w, ms);
        return ms.ToArray();
    }

    [Fact]
    public void SchemaV35_PopulatedCivicsPoints_ExpectedLengthBitExactRoundTripAndHashEquality()
    {
        Assert.Equal(37, CanonicalSchema.Version);   // v36 (ADR-049 ResearchLevels): the schema bump alone (v37: ADR-051 ResearchReserves — the schema bump alone)
        WorldState world = Populated();
        Assert.Null(ResearchQuery.CivicsPointsInvariantViolation(world.CivicsPoints));   // a table the rule could write
        byte[] bytes = SchemaProjection.Serialize(world);
        Assert.Equal(CanonicalSchema.ExpectedLength(world), bytes.Length);
        Assert.Equal(4 * 28, CanonicalSchema.ExpectedLength(world) - CanonicalSchema.ExpectedLength(new WorldState(35)));   // 28 bytes a row

        // Section 62 was the LAST block: its count prefix, then each row as Polity, Balance, Generated, Spent (little-endian).
        // M6 merge (2026-10-22) CAUSE BLOCK: Track EX's four discovery tables (sections 63–66, EMPTY here) follow it — every
        // tail offset moves by their four 4-byte zero count prefixes (`ex`). The layout alone.
        const int ex = 4 * 4 + 4 + 4;   // + ADR-049's empty section-67 prefix (v36) + ADR-051's empty section-68 prefix (v37)
        for (int i = bytes.Length - ex; i < bytes.Length; i++) Assert.Equal(0, bytes[i]);
        int start = bytes.Length - ex - 4 - 4 * 28;
        Assert.Equal(4, BitConverter.ToInt32(bytes, start));
        Assert.Equal(-5, BitConverter.ToInt32(bytes, start + 4));
        Assert.Equal(250L, BitConverter.ToInt64(bytes, start + 4 + 4));
        Assert.Equal(1, BitConverter.ToInt32(bytes, start + 4 + 28));
        Assert.Equal(1_500_000L, BitConverter.ToInt64(bytes, start + 4 + 28 + 4));
        Assert.Equal(4_000_000L, BitConverter.ToInt64(bytes, start + 4 + 28 + 12));
        Assert.Equal(2_500_000L, BitConverter.ToInt64(bytes, start + 4 + 28 + 20));
        Assert.Equal(0L, BitConverter.ToInt64(bytes, bytes.Length - ex - 8));
        Assert.Equal(long.MaxValue, BitConverter.ToInt64(bytes, bytes.Length - ex - 16));

        using (var ms = new MemoryStream(bytes))
        using (var reader = new BinaryReader(ms))
        {
            WorldState back = CanonicalSchema.Read(reader);
            Assert.True(WorldStates.StateEquals(world, back), "round trip drifted");
            Assert.Equal(WorldHash.ComputeHex(world), WorldHash.ComputeHex(back));
            for (int i = 0; i < world.CivicsPoints.Count; i++) Assert.Equal(world.CivicsPoints[i], back.CivicsPoints[i]);
            Assert.Equal(bytes, SchemaProjection.Serialize(back));                  // bit-exact re-serialization
        }

        // Every field is hashed, and the clone is independent.
        string h = WorldHash.ComputeHex(world);
        WorldState[] variants = [world.Clone(), world.Clone(), world.Clone(), world.Clone()];
        variants[0].CivicsPoints[1] = variants[0].CivicsPoints[1] with { Polity = new PolityId(3) };
        variants[1].CivicsPoints[1] = variants[1].CivicsPoints[1] with { BalanceMilli = 1_500_001 };
        variants[2].CivicsPoints[1] = variants[2].CivicsPoints[1] with { GeneratedMilli = 4_000_001 };
        variants[3].CivicsPoints[1] = variants[3].CivicsPoints[1] with { SpentMilli = 2_500_001 };
        foreach (WorldState v in variants) Assert.NotEqual(h, WorldHash.ComputeHex(v));
        Assert.Equal(1_500_000L, world.CivicsPoints[1].BalanceMilli);
        Assert.False(WorldStates.StateEquals(world, variants[1]));

        // Through a SNAPSHOT: it loads bit-exactly and re-saves to the identical bytes.
        byte[] file = Saved(world);
        Assert.Equal(CanonicalSchema.Version, BitConverter.ToInt32(file, 8));   // v36 (ADR-049): this build saves v36
        WorldState loaded = Snapshot.Load(new MemoryStream(file));
        Assert.True(WorldStates.StateEquals(world, loaded));
        Assert.Equal(file, Saved(loaded));
    }

    /// <summary>THE v34 → v35 MIGRATION: a v34 file is the v35 file of a CP-less world with its header version 34 and the
    /// empty section-62 count prefix absent. It loads with an EMPTY CivicsPoints table (every bank at zero) and the same
    /// world otherwise; its v34 projection is the hash a v34 build printed for it.</summary>
    [Fact]
    public void AV34Snapshot_LoadsWithAnEmptyCivicsPointsTable_AndItsV34ProjectionIsItsOwnStreamHash()
    {
        WorldState w = new WorldState(9);
        w.ConstructionHolds.Add(new ConstructionHoldRow(1, 2, 3));        // a populated v34 table before the cut
        byte[] v35 = Saved(w);
        // header (8 magic + 4 version + 8 seed + 8 turn) | stream | 4-byte topology trailer
        // M6 merge: drop the five empty v35 count prefixes (sections 62–66, 20 bytes) before the trailer.
        byte[] v34 = [.. v35[..^32], .. v35[^4..]];   // v36 (ADR-049): and section 67's empty prefix; v37 (ADR-051): and 68's
        BitConverter.GetBytes(34).CopyTo(v34, 8);
        WorldState loaded = Snapshot.Load(new MemoryStream(v34));
        Assert.Equal(0, loaded.CivicsPoints.Count);
        Assert.True(WorldStates.StateEquals(w, loaded));
        byte[] v34Stream = v34[(8 + 4 + 8 + 8)..^4];
        Assert.Equal(Convert.ToHexStringLower(System.Security.Cryptography.SHA256.HashData(v34Stream)), SchemaProjection.V34(loaded));
        Assert.Equal(SchemaProjection.V34(loaded), WorldHash.ComputeHexAsSchema(loaded, 34));
        Assert.Equal(7 * 4, CanonicalSchema.BytesAppendedSince(loaded, 34));                 // M6 merge: sections 62–66; v36: 67; v37: 68
        Assert.Equal(7 * 4 + 4 + 16, CanonicalSchema.BytesAppendedSince(loaded, 33));       // + section 61 with its one 16-byte row
        Assert.NotNull(SaveFiles.MigrationNote("x", 34));
        Assert.Contains("CivicsPoints (every Civics Points bank starts at zero)", SaveFiles.MigrationNote("x", 34));
    }

    [Fact]
    public void TheReadableSet_Is32_33_34_35_ThroughOneHelper_AndEveryReaderAgrees()
    {
        foreach (int v in new[] { 32, 33, 34, 35, 36, 37 }) Assert.True(CanonicalSchema.IsReadable(v));   // v36: ADR-049; v37: ADR-051
        foreach (int v in new[] { 31, 38, 0, 27, -1, int.MaxValue }) Assert.False(CanonicalSchema.IsReadable(v));
        foreach (int v in new[] { 31, 38, 0 })
            Assert.Throws<ArgumentOutOfRangeException>(() => CanonicalSchema.Read(new BinaryReader(new MemoryStream(new byte[64])), v, out _));
        Assert.Equal(SnapshotDiff.Layout.Length - 2, SnapshotDiff.LayoutFor(35).Length);   // v36 (ADR-049): section 67; v37 (ADR-051): 68
        Assert.Equal("CivicsPoints", SnapshotDiff.Layout[^7].Name);          // M6 merge: sections 63–66 follow it; v36: and 67; v37: and 68
        Assert.Equal("SettlementSurveys", SnapshotDiff.LayoutFor(35)[^1].Name);
        Assert.Equal("ConstructionHolds", SnapshotDiff.LayoutFor(34)[^1].Name);
        Assert.Equal("ResourceAllocations", SnapshotDiff.LayoutFor(33)[^1].Name);
        Assert.Equal("TaxGrievance", SnapshotDiff.LayoutFor(32)[^1].Name);
        Assert.Throws<ArgumentOutOfRangeException>(() => SnapshotDiff.LayoutFor(38));
        foreach (int v in new[] { 32, 33, 34, 35, 36 }) Assert.NotNull(SaveFiles.MigrationNote("x", v));
        foreach (int v in new[] { 37, 31, 38 }) Assert.Null(SaveFiles.MigrationNote("x", v));

        // A populated v35 stream walks to its end with the CP table's row count read back.
        byte[] bytes = SchemaProjection.Serialize(Populated());
        SnapshotDiff.Result self = SnapshotDiff.CompareStreams(bytes, CanonicalSchema.Version, bytes, CanonicalSchema.Version);
        Assert.True(self.Identical);
        Assert.Equal(bytes.Length, self.BytesWalkedA);
        Assert.Equal(("CivicsPoints", 4), (self.Tables[^7].Table, self.Tables[^7].RowsA));   // M6 merge: 63–66 follow it; v36: 67; v37: 68
    }

    private static string Refusal(params CivicsPointsRow[] rows)
    {
        var w = new WorldState(5);
        foreach (CivicsPointsRow r in rows) w.CivicsPoints.Add(r);
        return Assert.Throws<SnapshotFormatException>(() => Snapshot.Load(new MemoryStream(Saved(w)))).Message;
    }

    [Fact]
    public void Load_RefusesACivicsPointsTableNoRuleWrites()
    {
        Assert.Contains("ascending", Refusal(new(new PolityId(2), 0, 0, 0), new(new PolityId(1), 0, 0, 0)));
        Assert.Contains("ascending", Refusal(new(new PolityId(1), 0, 0, 0), new(new PolityId(1), 0, 0, 0)));
        Assert.Contains("negative", Refusal(new CivicsPointsRow(new PolityId(1), -1, 0, 1)));
        Assert.Contains("GeneratedMilli", Refusal(new CivicsPointsRow(new PolityId(1), 5, 10, 4)));
        Assert.Contains("schema v35 invariant", Refusal(new CivicsPointsRow(new PolityId(1), 5, 10, 4)));
    }
}
