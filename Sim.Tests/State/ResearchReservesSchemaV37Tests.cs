using Sim.Cli;
using Sim.Core.Kernel;
using Sim.Core.State;
using Sim.Tests.TestUtil;

namespace Sim.Tests.State;

/// <summary>
/// ADR-051 — SCHEMA v37: section 68 ResearchReserves (Polity, ReserveMilli; 12 bytes) appended after ResearchLevels; the
/// readable SET {32, …, 37} through the one helper; the v36 → v37 migration ("empty tables": no reserve existed under v36);
/// the Snapshot.Load refusals of a reserve table no rule writes (unsorted or duplicate Polity, a negative reserve).
/// Constitution rule: a new serialized row type ships a POPULATED-table test (exact ExpectedLength, a bit-exact round trip,
/// hash equality) — here with a city state's negative reserved key, a zero (drawn-empty) row and the extreme long.
/// </summary>
public class ResearchReservesSchemaV37Tests
{
    /// <summary>Four reserve rows in strictly ascending Polity: a city state's reserved local key (−1 − 4 = −5), an Empire at 0
    /// (drawn empty, the row stays), an Empire with 3 turns of 1 000 RP, and long.MaxValue on an extreme polity id.</summary>
    private static WorldState Populated()
    {
        var w = new WorldState(37);
        w.ResearchReserves.Add(new ResearchReserveRow(SettlementKnowledge.LocalHolder(new SettlementId(4)), 1_234_567L));
        w.ResearchReserves.Add(new ResearchReserveRow(new PolityId(1), 0L));
        w.ResearchReserves.Add(new ResearchReserveRow(new PolityId(2), 3_000_000L));
        w.ResearchReserves.Add(new ResearchReserveRow(new PolityId(int.MaxValue), long.MaxValue));
        return w;
    }

    private static byte[] Saved(WorldState w)
    {
        using var ms = new MemoryStream();
        Snapshot.Save(w, ms);
        return ms.ToArray();
    }

    [Fact]
    public void SchemaV37_PopulatedResearchReserves_ExpectedLengthBitExactRoundTripAndHashEquality()
    {
        Assert.Equal(37, CanonicalSchema.Version);
        WorldState world = Populated();
        Assert.Null(ResearchQuery.ReservesInvariantViolation(world.ResearchReserves));   // a table the rule could write
        byte[] bytes = SchemaProjection.Serialize(world);
        Assert.Equal(CanonicalSchema.ExpectedLength(world), bytes.Length);
        Assert.Equal(4 * 12, CanonicalSchema.ExpectedLength(world) - CanonicalSchema.ExpectedLength(new WorldState(37)));   // 12 bytes a row

        // Section 68 is the LAST block: its count prefix, then each row as Polity (int32), ReserveMilli (int64), little-endian.
        int start = bytes.Length - 4 - 4 * 12;
        Assert.Equal(4, BitConverter.ToInt32(bytes, start));
        Assert.Equal(-5, BitConverter.ToInt32(bytes, start + 4));
        Assert.Equal(1_234_567L, BitConverter.ToInt64(bytes, start + 8));
        Assert.Equal(1, BitConverter.ToInt32(bytes, start + 16));
        Assert.Equal(0L, BitConverter.ToInt64(bytes, start + 20));
        Assert.Equal(2, BitConverter.ToInt32(bytes, start + 28));
        Assert.Equal(3_000_000L, BitConverter.ToInt64(bytes, start + 32));
        Assert.Equal(int.MaxValue, BitConverter.ToInt32(bytes, start + 40));
        Assert.Equal(long.MaxValue, BitConverter.ToInt64(bytes, bytes.Length - 8));

        using (var ms = new MemoryStream(bytes))
        using (var reader = new BinaryReader(ms))
        {
            WorldState back = CanonicalSchema.Read(reader);
            Assert.True(WorldStates.StateEquals(world, back), "round trip drifted");
            Assert.Equal(WorldHash.ComputeHex(world), WorldHash.ComputeHex(back));
            for (int i = 0; i < world.ResearchReserves.Count; i++) Assert.Equal(world.ResearchReserves[i], back.ResearchReserves[i]);
            Assert.Equal(bytes, SchemaProjection.Serialize(back));                  // bit-exact re-serialization
        }

        // Every field is hashed, the order is hashed, and the clone is independent.
        string h = WorldHash.ComputeHex(world);
        WorldState[] variants = [world.Clone(), world.Clone(), world.Clone()];
        variants[0].ResearchReserves[2] = variants[0].ResearchReserves[2] with { Polity = new PolityId(3) };
        variants[1].ResearchReserves[2] = variants[1].ResearchReserves[2] with { ReserveMilli = 3_000_001L };
        (variants[2].ResearchReserves[1], variants[2].ResearchReserves[2]) = (variants[2].ResearchReserves[2], variants[2].ResearchReserves[1]);
        foreach (WorldState v in variants) Assert.NotEqual(h, WorldHash.ComputeHex(v));
        Assert.Equal(3_000_000L, world.ResearchReserves[2].ReserveMilli);
        Assert.False(WorldStates.StateEquals(world, variants[1]));

        // Through a SNAPSHOT: it loads bit-exactly and re-saves to the identical bytes.
        byte[] file = Saved(world);
        Assert.Equal(37, BitConverter.ToInt32(file, 8));
        WorldState loaded = Snapshot.Load(new MemoryStream(file));
        Assert.True(WorldStates.StateEquals(world, loaded));
        Assert.Equal(file, Saved(loaded));
        Assert.Equal(1_234_567L, ResearchQuery.ReserveMilli(loaded, SettlementKnowledge.LocalHolder(new SettlementId(4))));
    }

    /// <summary>THE v36 → v37 MIGRATION: a v36 file is the v37 file of a reserve-less world with its header version 36 and the
    /// empty section-68 count prefix absent. It loads with an EMPTY ResearchReserves table and the same world otherwise; its
    /// v36 projection is the hash a v36 build printed for it.</summary>
    [Fact]
    public void AV36Snapshot_LoadsWithAnEmptyReserveTable_AndItsV36ProjectionIsItsOwnStreamHash()
    {
        var w = new WorldState(9);
        w.CivicsPoints.Add(new CivicsPointsRow(new PolityId(1), 5, 5, 0));          // populated older tables before the cut
        w.ResearchLevels.Add(new ResearchLevelRow(new PolityId(1), new ResearchNodeId(419), 2));
        byte[] v37 = Saved(w);
        // header (8 magic + 4 version + 8 seed + 8 turn) | stream | 4-byte topology trailer: drop the empty section-68 prefix.
        byte[] v36 = [.. v37[..^8], .. v37[^4..]];
        BitConverter.GetBytes(36).CopyTo(v36, 8);
        WorldState loaded = Snapshot.Load(new MemoryStream(v36));
        Assert.Equal(0, loaded.ResearchReserves.Count);
        Assert.True(WorldStates.StateEquals(w, loaded));
        byte[] v36Stream = v36[(8 + 4 + 8 + 8)..^4];
        Assert.Equal(Convert.ToHexStringLower(System.Security.Cryptography.SHA256.HashData(v36Stream)), SchemaProjection.V36(loaded));
        Assert.Equal(SchemaProjection.V36(loaded), WorldHash.ComputeHexAsSchema(loaded, 36));
        Assert.Equal(4, CanonicalSchema.BytesAppendedSince(loaded, 36));
        Assert.Equal(4 + 4 + 12, CanonicalSchema.BytesAppendedSince(loaded, 35));   // + section 67 with its one level row
        Assert.Contains("ResearchReserves was added empty", SaveFiles.MigrationNote("x", 36));
        foreach (int v in new[] { 32, 33, 34, 35 }) Assert.Contains("ResearchReserves", SaveFiles.MigrationNote("x", v));
        // The reserve of a migrated holder reads 0 — no reserve existed under v36.
        Assert.Equal(0L, ResearchQuery.ReserveMilli(loaded, new PolityId(1)));
    }

    [Fact]
    public void TheReadableSet_Is32To37_ThroughOneHelper_AndEveryReaderAgrees()
    {
        foreach (int v in new[] { 32, 33, 34, 35, 36, 37 }) Assert.True(CanonicalSchema.IsReadable(v));
        foreach (int v in new[] { 31, 38, 0, 27, -1, int.MaxValue }) Assert.False(CanonicalSchema.IsReadable(v));
        foreach (int v in new[] { 31, 38, 0 })
            Assert.Throws<ArgumentOutOfRangeException>(() => CanonicalSchema.Read(new BinaryReader(new MemoryStream(new byte[64])), v, out _));
        Assert.Equal(SnapshotDiff.Layout.Length, SnapshotDiff.LayoutFor(37).Length);
        Assert.Equal("ResearchReserves", SnapshotDiff.Layout[^1].Name);
        Assert.Equal("ResearchLevels", SnapshotDiff.LayoutFor(36)[^1].Name);
        Assert.Equal("SettlementSurveys", SnapshotDiff.LayoutFor(35)[^1].Name);
        Assert.Throws<ArgumentOutOfRangeException>(() => SnapshotDiff.LayoutFor(38));
        foreach (int v in new[] { 32, 33, 34, 35, 36 }) Assert.NotNull(SaveFiles.MigrationNote("x", v));
        foreach (int v in new[] { 37, 31, 38 }) Assert.Null(SaveFiles.MigrationNote("x", v));

        // A populated v37 stream walks to its end with the reserve table's row count read back.
        byte[] bytes = SchemaProjection.Serialize(Populated());
        SnapshotDiff.Result self = SnapshotDiff.CompareStreams(bytes, 37, bytes, 37);
        Assert.True(self.Identical);
        Assert.Equal(bytes.Length, self.BytesWalkedA);
        Assert.Equal(("ResearchReserves", 4), (self.Tables[^1].Table, self.Tables[^1].RowsA));
    }

    private static string Refusal(params ResearchReserveRow[] rows)
    {
        var w = new WorldState(5);
        foreach (ResearchReserveRow r in rows) w.ResearchReserves.Add(r);
        return Assert.Throws<SnapshotFormatException>(() => Snapshot.Load(new MemoryStream(Saved(w)))).Message;
    }

    [Fact]
    public void Load_RefusesAReserveTableNoRuleWrites()
    {
        Assert.Contains("negative", Refusal(new ResearchReserveRow(new PolityId(1), -1L)));
        Assert.Contains("ascending", Refusal(new(new PolityId(2), 5L), new(new PolityId(1), 5L)));
        Assert.Contains("ascending", Refusal(new(new PolityId(1), 5L), new(new PolityId(1), 6L)));
        Assert.Contains("schema v37 invariant", Refusal(new ResearchReserveRow(new PolityId(1), -3L)));
    }
}
