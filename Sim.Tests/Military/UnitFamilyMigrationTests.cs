using Sim.Core.Kernel;
using Sim.Core.State;
using Sim.Tests.TestUtil;
using static Sim.Tests.Military.MilitaryRigs;

namespace Sim.Tests.Military;

/// <summary>
/// ADR-055 §2 (unit charter §5.3) — THE SAVE MIGRATION OF THE TAXONOMY SPLITS: a pure (Family, Identity) map applied at load
/// to every MilitaryUnits row of a pre-v39 stream; a populated-table round trip before and after; the hash change attributed
/// to the migration alone (the switch-off twin reads the same stream unmigrated and returns the old hash); a refused unmapped
/// pair; unknown identities preserved (ADR-031 rule 5); and the MEASURED no-op on every shipped save.
/// </summary>
public class UnitFamilyMigrationTests
{
    [Fact]
    public void TheMap_IsTheCharters_AndAgreesWithTheContent()
    {
        Assert.Equal([(101, 1, 22), (907, 9, 18), (908, 9, 19), (1102, 11, 20), (1104, 11, 21)], UnitFamilyMigration.Map);
        foreach ((int identity, int oldFamily, int newFamily) in UnitFamilyMigration.Map)
        {
            Assert.Equal(newFamily, Families.IdentityByKey(identity)!.FamilyKey);
            Assert.NotNull(Families.FamilyByKey(oldFamily));
            Assert.Equal(UnitFamilyRemap.Remapped, UnitFamilyMigration.Remap(oldFamily, identity, out int to));
            Assert.Equal(newFamily, to);
            Assert.Equal(UnitFamilyRemap.Unchanged, UnitFamilyMigration.Remap(newFamily, identity, out to));
            Assert.Equal(newFamily, to);
        }
        Assert.Equal(UnitFamilyRemap.Unchanged, UnitFamilyMigration.Remap(3, 301, out int same));
        Assert.Equal(3, same);
        Assert.Equal(UnitFamilyRemap.Unchanged, UnitFamilyMigration.Remap(77, 7777, out same));   // unknown: preserved
        Assert.Equal(UnitFamilyRemap.Refused, UnitFamilyMigration.Remap(9, 1102, out _));
    }

    /// <summary>A v37 stream (this build's stream less the four empty v39 count prefixes) holding a formation of each rehomed
    /// identity under its OLD family, the warband, and an identity no content knows.</summary>
    private static (byte[] V37, WorldState Old) V37Stream()
    {
        var w = new WorldState(42);
        int id = 1;
        foreach ((int identity, int oldFamily, _) in UnitFamilyMigration.Map)
            w.MilitaryUnits.Add(new MilitaryUnitRow(id++, new PolityId(1), oldFamily, identity, new SettlementId(0), 1, 2, 0.5, 0));
        w.MilitaryUnits.Add(new MilitaryUnitRow(id++, new PolityId(1), 3, 301, new SettlementId(0), 1, 2, 0.0, 0));
        w.MilitaryUnits.Add(new MilitaryUnitRow(id++, new PolityId(2), 77, 7777, new SettlementId(1), 3, 4, 1.0, 9));
        w.UnitConversions.Add(new UnitConversionRow(3, 1, new PolityId(1), 1, 101, 1, 101, 1, 2, 3));
        byte[] v39 = SchemaProjection.Serialize(w);
        Assert.Equal(20, CanonicalSchema.BytesAppendedSince(w, 37));   // the empty v38 prefix and the four empty v39 prefixes
        return (v39[..^20], w);
    }

    private static WorldState Read(byte[] v37, bool migrate)
    {
        using var reader = new BinaryReader(new MemoryStream(v37));
        return CanonicalSchema.Read(reader, 37, out _, migrate);
    }

    [Fact]
    public void AV37Stream_IsRemappedAtLoad_ThePopulatedTableRoundTripsBeforeAndAfter_AndTheHashMoveIsTheMigrationAlone()
    {
        (byte[] v37, WorldState old) = V37Stream();
        string oldV37Hash = Convert.ToHexStringLower(System.Security.Cryptography.SHA256.HashData(v37));

        // BEFORE (the switch-off twin): read unmigrated, it is the v37 world byte for byte.
        WorldState before = Read(v37, migrate: false);
        Assert.True(WorldStates.TableEquals(old.MilitaryUnits, before.MilitaryUnits));
        Assert.Equal(oldV37Hash, WorldHash.ComputeHexAsSchema(before, 37));
        Assert.Equal(oldV37Hash, SchemaProjection.V37(before));

        // AFTER: every rehomed row moved family, key unchanged; the warband, the unknown identity and the log untouched.
        WorldState after = Read(v37, migrate: true);
        for (int i = 0; i < UnitFamilyMigration.Map.Count; i++)
        {
            Assert.Equal(UnitFamilyMigration.Map[i].NewFamily, after.MilitaryUnits[i].Family);
            Assert.Equal(old.MilitaryUnits[i] with { Family = UnitFamilyMigration.Map[i].NewFamily }, after.MilitaryUnits[i]);
        }
        Assert.Equal(old.MilitaryUnits[5], after.MilitaryUnits[5]);
        Assert.Equal(old.MilitaryUnits[6], after.MilitaryUnits[6]);
        Assert.True(WorldStates.TableEquals(old.UnitConversions, after.UnitConversions));   // history is never rewritten
        Assert.NotEqual(oldV37Hash, WorldHash.ComputeHexAsSchema(after, 37));

        // The migrated world is a v39 world: populated round trip, bit exact, and a second load does not migrate again.
        byte[] v39 = SchemaProjection.Serialize(after);
        Assert.Equal(CanonicalSchema.ExpectedLength(after), v39.Length);
        using var reader = new BinaryReader(new MemoryStream(v39));
        WorldState again = CanonicalSchema.Read(reader);
        Assert.True(WorldStates.StateEquals(after, again));
        Assert.Equal(WorldHash.ComputeHex(after), WorldHash.ComputeHex(again));
        Assert.Equal(v39, SchemaProjection.Serialize(again));
    }

    [Fact]
    public void AnUnmappedRehomedPair_IsRefused_ByTheReaderAndBySnapshotLoad()
    {
        var w = new WorldState(42);
        w.MilitaryUnits.Add(new MilitaryUnitRow(1, new PolityId(1), 9, 1102, new SettlementId(0), 0, 0, 0, 0));   // helicopters under naval_combat
        byte[] v37 = SchemaProjection.Serialize(w)[..^16];
        Assert.Contains("no rule maps it", Assert.Throws<InvalidDataException>(() => Read(v37, migrate: true)).Message);
        Assert.Equal(1102, Read(v37, migrate: false).MilitaryUnits[0].Identity);   // the twin reads it as written

        using var file = new MemoryStream();
        using (var writer = new BinaryWriter(file, System.Text.Encoding.UTF8, leaveOpen: true))
        {
            writer.Write("CIVSNAP\0"u8);
            writer.Write(37);
            writer.Write(w.Seed);
            writer.Write(w.Clock.Turn);
            writer.Write(v37);
            writer.Write(0);   // topology trailer
        }
        file.Position = 0;
        Assert.Contains("no rule maps it", Assert.Throws<SnapshotFormatException>(() => Snapshot.Load(file)).Message);
    }

    [Theory]
    [InlineData("v32-seed42-turn40.civsnap")]
    [InlineData("v33-seed42-turn2.civsnap")]
    public void EveryShippedSave_HoldsNoRehomedRow_SoTheMigrationIsANoOp_Measured(string fixture)
    {
        byte[] file = File.ReadAllBytes(Path.Combine(AppContext.BaseDirectory, "Fixtures", fixture));
        int version = BitConverter.ToInt32(file, 8);
        Assert.True(version < 39);
        byte[] stream = file[28..];
        WorldState raw, migrated;
        using (var r = new BinaryReader(new MemoryStream(stream))) raw = CanonicalSchema.Read(r, version, out _, migrateUnitFamilies: false);
        using (var r = new BinaryReader(new MemoryStream(stream))) migrated = CanonicalSchema.Read(r, version, out _, migrateUnitFamilies: true);
        Assert.True(raw.MilitaryUnits.Count > 0);   // the founding warbands are there
        for (int i = 0; i < raw.MilitaryUnits.Count; i++)
            Assert.Equal(UnitFamilyRemap.Unchanged, UnitFamilyMigration.Remap(raw.MilitaryUnits[i].Family, raw.MilitaryUnits[i].Identity, out _));
        Assert.Equal(WorldHash.ComputeHex(raw), WorldHash.ComputeHex(migrated));
    }
}
