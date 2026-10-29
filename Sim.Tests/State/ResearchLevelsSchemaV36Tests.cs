using Sim.Cli;
using Sim.Core.Kernel;
using Sim.Core.State;
using Sim.Tests.TestUtil;

namespace Sim.Tests.State;

/// <summary>
/// ADR-049 (D-063 R4, CR-024) — SCHEMA v36: section 67 ResearchLevels (Polity, Node, Level; 12 bytes) appended after
/// SettlementSurveys; the readable SET {32, 33, 34, 35, 36} through the one helper; the v35 → v36 migration ("empty
/// tables": no level existed under v35); the Snapshot.Load refusals of a level table no rule writes (a level outside
/// [1, MaxRepeatableLevel], a second row for one (Polity, Node)).
/// Constitution rule: a new serialized row type ships a POPULATED-table test (exact ExpectedLength, a bit-exact round trip,
/// hash equality) — here with a city-state's negative reserved key and the extreme legal level.
/// </summary>
public class ResearchLevelsSchemaV36Tests
{
    /// <summary>Four level rows in first-completion order: an Empire, a city-state's reserved local key (−1 − 4 = −5), a
    /// second node of the first Empire, and the cap level on an extreme polity id.</summary>
    private static WorldState Populated()
    {
        var w = new WorldState(36);
        w.ResearchLevels.Add(new ResearchLevelRow(new PolityId(1), new ResearchNodeId(419), 3));
        w.ResearchLevels.Add(new ResearchLevelRow(SettlementKnowledge.LocalHolder(new SettlementId(4)), new ResearchNodeId(1990), 1));
        w.ResearchLevels.Add(new ResearchLevelRow(new PolityId(1), new ResearchNodeId(420), 27));
        w.ResearchLevels.Add(new ResearchLevelRow(new PolityId(int.MaxValue), new ResearchNodeId(int.MaxValue), ResearchQuery.MaxRepeatableLevel));
        return w;
    }

    private static byte[] Saved(WorldState w)
    {
        using var ms = new MemoryStream();
        Snapshot.Save(w, ms);
        return ms.ToArray();
    }

    [Fact]
    public void SchemaV36_PopulatedResearchLevels_ExpectedLengthBitExactRoundTripAndHashEquality()
    {
        Assert.True(CanonicalSchema.Version >= 36);   // v37 (ADR-051) appends section 68 after this one
        WorldState world = Populated();
        Assert.Null(ResearchQuery.LevelsInvariantViolation(world.ResearchLevels));   // a table the rule could write
        byte[] bytes = SchemaProjection.Serialize(world);
        Assert.Equal(CanonicalSchema.ExpectedLength(world), bytes.Length);
        Assert.Equal(4 * 12, CanonicalSchema.ExpectedLength(world) - CanonicalSchema.ExpectedLength(new WorldState(36)));   // 12 bytes a row

        // Section 67: its count prefix, then each row as Polity, Node, Level (little-endian int32s) — followed since v37 only by
        // the empty section-68 (ResearchReserves) count prefix, and since v38 (ADR-054) the empty section-69 (ResearchPopulations) one,
        // and since v39 (ADR-055) the four empty military sections 70–73.
        Assert.Equal(new byte[24], bytes[^24..]);
        int start = bytes.Length - 16 - 4 - 4 - 4 - 4 * 12;
        Assert.Equal(4, BitConverter.ToInt32(bytes, start));
        Assert.Equal(1, BitConverter.ToInt32(bytes, start + 4));
        Assert.Equal(419, BitConverter.ToInt32(bytes, start + 8));
        Assert.Equal(3, BitConverter.ToInt32(bytes, start + 12));
        Assert.Equal(-5, BitConverter.ToInt32(bytes, start + 16));
        Assert.Equal(1990, BitConverter.ToInt32(bytes, start + 20));
        Assert.Equal(27, BitConverter.ToInt32(bytes, start + 4 + 2 * 12 + 8));
        Assert.Equal(ResearchQuery.MaxRepeatableLevel, BitConverter.ToInt32(bytes, bytes.Length - 16 - 12));

        using (var ms = new MemoryStream(bytes))
        using (var reader = new BinaryReader(ms))
        {
            WorldState back = CanonicalSchema.Read(reader);
            Assert.True(WorldStates.StateEquals(world, back), "round trip drifted");
            Assert.Equal(WorldHash.ComputeHex(world), WorldHash.ComputeHex(back));
            for (int i = 0; i < world.ResearchLevels.Count; i++) Assert.Equal(world.ResearchLevels[i], back.ResearchLevels[i]);
            Assert.Equal(bytes, SchemaProjection.Serialize(back));                  // bit-exact re-serialization
        }

        // Every field is hashed, the order is hashed, and the clone is independent.
        string h = WorldHash.ComputeHex(world);
        WorldState[] variants = [world.Clone(), world.Clone(), world.Clone(), world.Clone()];
        variants[0].ResearchLevels[0] = variants[0].ResearchLevels[0] with { Polity = new PolityId(2) };
        variants[1].ResearchLevels[0] = variants[1].ResearchLevels[0] with { Node = new ResearchNodeId(418) };
        variants[2].ResearchLevels[0] = variants[2].ResearchLevels[0] with { Level = 4 };
        (variants[3].ResearchLevels[0], variants[3].ResearchLevels[2]) = (variants[3].ResearchLevels[2], variants[3].ResearchLevels[0]);
        foreach (WorldState v in variants) Assert.NotEqual(h, WorldHash.ComputeHex(v));
        Assert.Equal(3, world.ResearchLevels[0].Level);
        Assert.False(WorldStates.StateEquals(world, variants[2]));

        // Through a SNAPSHOT: it loads bit-exactly and re-saves to the identical bytes.
        byte[] file = Saved(world);
        Assert.Equal(CanonicalSchema.Version, BitConverter.ToInt32(file, 8));
        WorldState loaded = Snapshot.Load(new MemoryStream(file));
        Assert.True(WorldStates.StateEquals(world, loaded));
        Assert.Equal(file, Saved(loaded));
    }

    /// <summary>THE v35 → v36 MIGRATION: a v35 file is the v36 file of a level-less world with its header version 35 and the
    /// empty section-67 count prefix absent. It loads with an EMPTY ResearchLevels table and the same world otherwise; its
    /// v35 projection is the hash a v35 build printed for it.</summary>
    [Fact]
    public void AV35Snapshot_LoadsWithAnEmptyLevelsTable_AndItsV35ProjectionIsItsOwnStreamHash()
    {
        var w = new WorldState(9);
        w.CivicsPoints.Add(new CivicsPointsRow(new PolityId(1), 5, 5, 0));          // populated v35 tables before the cut
        w.ResearchCompleted.Add(new ResearchCompletedRow(new PolityId(1), new ResearchNodeId(419)));
        byte[] v37 = Saved(w);
        // header (8 magic + 4 version + 8 seed + 8 turn) | stream | 4-byte topology trailer: drop the empty section-67 and
        // section-68 (v37, ADR-051) prefixes, and section 69's (v38, ADR-054).
        // section-68 (v37, ADR-051) prefixes.
        byte[] v35 = [.. v37[..^32], .. v37[^4..]];   // ADR-055 (v39): + sections 70–73
        BitConverter.GetBytes(35).CopyTo(v35, 8);
        WorldState loaded = Snapshot.Load(new MemoryStream(v35));
        Assert.Equal(0, loaded.ResearchLevels.Count);
        Assert.True(WorldStates.StateEquals(w, loaded));
        byte[] v35Stream = v35[(8 + 4 + 8 + 8)..^4];
        Assert.Equal(Convert.ToHexStringLower(System.Security.Cryptography.SHA256.HashData(v35Stream)), SchemaProjection.V35(loaded));
        Assert.Equal(SchemaProjection.V35(loaded), WorldHash.ComputeHexAsSchema(loaded, 35));
        Assert.Equal(12 + 16, CanonicalSchema.BytesAppendedSince(loaded, 35));           // sections 67, 68, 69 and 70–73, empty
        Assert.Equal(12 + 16 + 5 * 4 + 28, CanonicalSchema.BytesAppendedSince(loaded, 34));   // + sections 62–66, one CP row
        // The migrated repeatable reads level 1 through its completed row.
        Assert.Equal(1, ResearchQuery.Level(loaded, new PolityId(1), new ResearchNodeId(419)));
        Assert.Contains("ResearchLevels was added empty", SaveFiles.MigrationNote("x", 35));
        foreach (int v in new[] { 32, 33, 34 }) Assert.Contains("ResearchLevels", SaveFiles.MigrationNote("x", v));
    }

    [Fact]
    public void TheReadableSet_Includes32To36_ThroughOneHelper_AndEveryReaderAgrees()
    {
        // v37 (ADR-051) extends the set by 37; ResearchReservesSchemaV37Tests pins the whole set.
        foreach (int v in new[] { 32, 33, 34, 35, 36 }) Assert.True(CanonicalSchema.IsReadable(v));
        foreach (int v in new[] { 31, 0, 27, -1, int.MaxValue }) Assert.False(CanonicalSchema.IsReadable(v));
        foreach (int v in new[] { 31, 0 })
            Assert.Throws<ArgumentOutOfRangeException>(() => CanonicalSchema.Read(new BinaryReader(new MemoryStream(new byte[64])), v, out _));
        Assert.Equal(SnapshotDiff.Layout.Length - 6, SnapshotDiff.LayoutFor(36).Length);   // v38 (ADR-054): and section 69
        Assert.Equal("ResearchLevels", SnapshotDiff.LayoutFor(36)[^1].Name);
        Assert.Equal("SettlementSurveys", SnapshotDiff.LayoutFor(35)[^1].Name);
        Assert.Equal("ConstructionHolds", SnapshotDiff.LayoutFor(34)[^1].Name);
        foreach (int v in new[] { 32, 33, 34, 35, 36 }) Assert.NotNull(SaveFiles.MigrationNote("x", v));
        foreach (int v in new[] { CanonicalSchema.Version, 31 }) Assert.Null(SaveFiles.MigrationNote("x", v));

        // A populated stream walks to its end with the level table's row count read back (section 67, before 68 and 69).
        byte[] bytes = SchemaProjection.Serialize(Populated());
        SnapshotDiff.Result self = SnapshotDiff.CompareStreams(bytes, CanonicalSchema.Version, bytes, CanonicalSchema.Version);
        Assert.True(self.Identical);
        Assert.Equal(bytes.Length, self.BytesWalkedA);
        // ADR-055 (v39): the four military tables (sections 70–73) follow section 69 — the level table is seventh from the end.
        Assert.Equal(("ResearchLevels", 4), (self.Tables[^(3 + SchemaProjection.V39TableCount)].Table, self.Tables[^(3 + SchemaProjection.V39TableCount)].RowsA));
    }

    private static string Refusal(params ResearchLevelRow[] rows)
    {
        var w = new WorldState(5);
        foreach (ResearchLevelRow r in rows) w.ResearchLevels.Add(r);
        return Assert.Throws<SnapshotFormatException>(() => Snapshot.Load(new MemoryStream(Saved(w)))).Message;
    }

    [Fact]
    public void Load_RefusesALevelsTableNoRuleWrites()
    {
        var p = new PolityId(1);
        Assert.Contains("outside", Refusal(new ResearchLevelRow(p, new ResearchNodeId(419), 0)));
        Assert.Contains("outside", Refusal(new ResearchLevelRow(p, new ResearchNodeId(419), ResearchQuery.MaxRepeatableLevel + 1)));
        Assert.Contains("second row", Refusal(new(p, new ResearchNodeId(419), 1), new(p, new ResearchNodeId(419), 2)));
        Assert.Contains("schema v36 invariant", Refusal(new ResearchLevelRow(p, new ResearchNodeId(419), -3)));
    }
}
