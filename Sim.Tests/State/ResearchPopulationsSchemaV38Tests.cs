using Sim.Cli;
using Sim.Core.Kernel;
using Sim.Core.State;
using Sim.Tests.TestUtil;

namespace Sim.Tests.State;

/// <summary>
/// ADR-054 — SCHEMA v38: section 69 ResearchPopulations (Polity, Population; 12 bytes) appended after ResearchReserves; the
/// readable SET {32, …, 38} through the one helper; the v37 → v38 migration ("empty tables": a holder's first step after the
/// load reads its current population); the Snapshot.Load refusals of a population table no rule writes (unsorted or
/// duplicate Polity, a negative population). Constitution rule: a new serialized row type ships a POPULATED-table test
/// (exact ExpectedLength, a bit-exact round trip, hash equality) — here with a city state's negative reserved key, an empty
/// (0) holder, an ordinary Empire and the extreme long on an extreme polity id.
/// </summary>
public class ResearchPopulationsSchemaV38Tests
{
    /// <summary>Four rows in strictly ascending Polity: a city state's reserved local key (−1 − 6 = −7), an Empire whose
    /// settlements are empty (0), an Empire of 12 345 people, and long.MaxValue on int.MaxValue.</summary>
    private static WorldState Populated()
    {
        var w = new WorldState(38);
        w.ResearchPopulations.Add(new ResearchPopulationRow(SettlementKnowledge.LocalHolder(new SettlementId(6)), 2_345L));
        w.ResearchPopulations.Add(new ResearchPopulationRow(new PolityId(1), 0L));
        w.ResearchPopulations.Add(new ResearchPopulationRow(new PolityId(2), 12_345L));
        w.ResearchPopulations.Add(new ResearchPopulationRow(new PolityId(int.MaxValue), long.MaxValue));
        return w;
    }

    private static byte[] Saved(WorldState w)
    {
        using var ms = new MemoryStream();
        Snapshot.Save(w, ms);
        return ms.ToArray();
    }

    [Fact]
    public void SchemaV38_PopulatedResearchPopulations_ExpectedLengthBitExactRoundTripAndHashEquality()
    {
        Assert.Equal(39, CanonicalSchema.Version);   // v39: ADR-055 the four military tables — the schema bump alone
        WorldState world = Populated();
        Assert.Null(ResearchQuery.PopulationsInvariantViolation(world.ResearchPopulations));   // a table the rule could write
        byte[] bytes = SchemaProjection.Serialize(world);
        Assert.Equal(CanonicalSchema.ExpectedLength(world), bytes.Length);
        Assert.Equal(4 * 12, CanonicalSchema.ExpectedLength(world) - CanonicalSchema.ExpectedLength(new WorldState(38)));   // 12 bytes a row

        // Section 69: its count prefix, then each row as Polity (int32), Population (int64), little-endian — followed since v39
        // (ADR-055) by the four empty military count prefixes (sections 70–73).
        Assert.Equal(new byte[16], bytes[^16..]);
        int start = bytes.Length - 16 - 4 - 4 * 12;
        Assert.Equal(4, BitConverter.ToInt32(bytes, start));
        Assert.Equal(-7, BitConverter.ToInt32(bytes, start + 4));
        Assert.Equal(2_345L, BitConverter.ToInt64(bytes, start + 8));
        Assert.Equal(1, BitConverter.ToInt32(bytes, start + 16));
        Assert.Equal(0L, BitConverter.ToInt64(bytes, start + 20));
        Assert.Equal(2, BitConverter.ToInt32(bytes, start + 28));
        Assert.Equal(12_345L, BitConverter.ToInt64(bytes, start + 32));
        Assert.Equal(int.MaxValue, BitConverter.ToInt32(bytes, start + 40));
        Assert.Equal(long.MaxValue, BitConverter.ToInt64(bytes, bytes.Length - 16 - 8));

        using (var ms = new MemoryStream(bytes))
        using (var reader = new BinaryReader(ms))
        {
            WorldState back = CanonicalSchema.Read(reader);
            Assert.True(WorldStates.StateEquals(world, back), "round trip drifted");
            Assert.Equal(WorldHash.ComputeHex(world), WorldHash.ComputeHex(back));
            for (int i = 0; i < world.ResearchPopulations.Count; i++) Assert.Equal(world.ResearchPopulations[i], back.ResearchPopulations[i]);
            Assert.Equal(bytes, SchemaProjection.Serialize(back));                  // bit-exact re-serialization
        }

        // Every field is hashed, the order is hashed, and the clone is independent.
        string h = WorldHash.ComputeHex(world);
        WorldState[] variants = [world.Clone(), world.Clone(), world.Clone()];
        variants[0].ResearchPopulations[2] = variants[0].ResearchPopulations[2] with { Polity = new PolityId(3) };
        variants[1].ResearchPopulations[2] = variants[1].ResearchPopulations[2] with { Population = 12_346L };
        (variants[2].ResearchPopulations[1], variants[2].ResearchPopulations[2]) = (variants[2].ResearchPopulations[2], variants[2].ResearchPopulations[1]);
        foreach (WorldState v in variants) Assert.NotEqual(h, WorldHash.ComputeHex(v));
        Assert.Equal(12_345L, world.ResearchPopulations[2].Population);
        Assert.False(WorldStates.StateEquals(world, variants[1]));

        // Through a SNAPSHOT: it loads bit-exactly and re-saves to the identical bytes.
        byte[] file = Saved(world);
        Assert.Equal(CanonicalSchema.Version, BitConverter.ToInt32(file, 8));   // v38; v39 (ADR-055)
        WorldState loaded = Snapshot.Load(new MemoryStream(file));
        Assert.True(WorldStates.StateEquals(world, loaded));
        Assert.Equal(file, Saved(loaded));
        Assert.True(ResearchQuery.TryGetStoredPopulation(loaded, SettlementKnowledge.LocalHolder(new SettlementId(6)), out long p));
        Assert.Equal(2_345L, p);
        Assert.False(ResearchQuery.TryGetStoredPopulation(loaded, new PolityId(3), out _));
    }

    /// <summary>THE v37 → v38 MIGRATION: a v37 file is the v38 file of a world with no stored population, its header version 37
    /// and the empty section-69 count prefix absent. It loads with an EMPTY ResearchPopulations table and the same world
    /// otherwise; its v37 projection is the hash a v37 build printed for it.</summary>
    [Fact]
    public void AV37Snapshot_LoadsWithAnEmptyPopulationTable_AndItsV37ProjectionIsItsOwnStreamHash()
    {
        var w = new WorldState(9);
        w.CivicsPoints.Add(new CivicsPointsRow(new PolityId(1), 5, 5, 0));          // populated older tables before the cut
        w.ResearchReserves.Add(new ResearchReserveRow(new PolityId(1), 7L));
        byte[] v38 = Saved(w);
        // header (8 magic + 4 version + 8 seed + 8 turn) | stream | 4-byte topology trailer: drop the empty section-69 prefix
        // (and, ADR-055, the four empty section 70–73 prefixes after it).
        byte[] v37 = [.. v38[..^24], .. v38[^4..]];
        BitConverter.GetBytes(37).CopyTo(v37, 8);
        WorldState loaded = Snapshot.Load(new MemoryStream(v37));
        Assert.Equal(0, loaded.ResearchPopulations.Count);
        Assert.True(WorldStates.StateEquals(w, loaded));
        byte[] v37Stream = v37[(8 + 4 + 8 + 8)..^4];
        Assert.Equal(Convert.ToHexStringLower(System.Security.Cryptography.SHA256.HashData(v37Stream)), SchemaProjection.V37(loaded));
        Assert.Equal(SchemaProjection.V37(loaded), WorldHash.ComputeHexAsSchema(loaded, 37));
        Assert.Equal(4 + 16, CanonicalSchema.BytesAppendedSince(loaded, 37));   // ADR-055: + sections 70–73
        Assert.Equal(4 + 16 + 4 + 12, CanonicalSchema.BytesAppendedSince(loaded, 36));   // + section 68 with its one reserve row
        Assert.Contains("ResearchPopulations was added empty", SaveFiles.MigrationNote("x", 37));
        foreach (int v in new[] { 32, 33, 34, 35, 36 }) Assert.Contains("ResearchPopulations", SaveFiles.MigrationNote("x", v));
        // A migrated holder has no stored population: its first step reads its current population (ADR-054 §3).
        Assert.False(ResearchQuery.TryGetStoredPopulation(loaded, new PolityId(1), out _));
    }

    [Fact]
    public void TheReadableSet_Is32To38_ThroughOneHelper_AndEveryReaderAgrees()
    {
        foreach (int v in new[] { 32, 33, 34, 35, 36, 37, 38 }) Assert.True(CanonicalSchema.IsReadable(v));
        Assert.True(CanonicalSchema.IsReadable(39));   // ADR-055
        foreach (int v in new[] { 31, 40, 0, 27, -1, int.MaxValue }) Assert.False(CanonicalSchema.IsReadable(v));
        foreach (int v in new[] { 31, 40, 0 })
            Assert.Throws<ArgumentOutOfRangeException>(() => CanonicalSchema.Read(new BinaryReader(new MemoryStream(new byte[64])), v, out _));
        Assert.Equal(SnapshotDiff.Layout.Length - 4, SnapshotDiff.LayoutFor(38).Length);   // ADR-055: sections 70–73 follow
        Assert.Equal("ResearchPopulations", SnapshotDiff.LayoutFor(38)[^1].Name);
        Assert.Equal("ResearchReserves", SnapshotDiff.LayoutFor(37)[^1].Name);
        Assert.Equal("ResearchLevels", SnapshotDiff.LayoutFor(36)[^1].Name);
        Assert.Throws<ArgumentOutOfRangeException>(() => SnapshotDiff.LayoutFor(40));
        foreach (int v in new[] { 32, 33, 34, 35, 36, 37, 38 }) Assert.NotNull(SaveFiles.MigrationNote("x", v));
        foreach (int v in new[] { 39, 31, 40 }) Assert.Null(SaveFiles.MigrationNote("x", v));

        // A populated v38 stream walks to its end with the population table's row count read back.
        byte[] bytes = SchemaProjection.Serialize(Populated());
        SnapshotDiff.Result self = SnapshotDiff.CompareStreams(bytes, CanonicalSchema.Version, bytes, CanonicalSchema.Version);
        Assert.True(self.Identical);
        Assert.Equal(bytes.Length, self.BytesWalkedA);
        Assert.Equal(("ResearchPopulations", 4), (self.Tables[^5].Table, self.Tables[^5].RowsA));   // ADR-055: four tables follow
    }

    private static string Refusal(params ResearchPopulationRow[] rows)
    {
        var w = new WorldState(5);
        foreach (ResearchPopulationRow r in rows) w.ResearchPopulations.Add(r);
        return Assert.Throws<SnapshotFormatException>(() => Snapshot.Load(new MemoryStream(Saved(w)))).Message;
    }

    [Fact]
    public void Load_RefusesAPopulationTableNoRuleWrites()
    {
        Assert.Contains("negative", Refusal(new ResearchPopulationRow(new PolityId(1), -1L)));
        Assert.Contains("ascending", Refusal(new(new PolityId(2), 5L), new(new PolityId(1), 5L)));
        Assert.Contains("ascending", Refusal(new(new PolityId(1), 5L), new(new PolityId(1), 6L)));
        Assert.Contains("schema v38 invariant", Refusal(new ResearchPopulationRow(new PolityId(1), -3L)));
    }
}
