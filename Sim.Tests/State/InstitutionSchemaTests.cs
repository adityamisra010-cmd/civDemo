using Sim.Core.Kernel;
using Sim.Core.State;
using Sim.Tests.TestUtil;

namespace Sim.Tests.State;

/// <summary>
/// ADR-033 D6 + D10 — schema v31 (Institutions, ConstructionLabor). Constitution rule: every new serialized row
/// type ships a POPULATED-table test with an exact ExpectedLength, a bit-exact round trip and hash equality
/// (the T1.1/T1.3 precedent: an empty table proves nothing). Both tables are populated out of id order, with a
/// maturity at both bounds, a negative zero, a subnormal, a non-dyadic value and a NaN payload (a corrupt
/// maturity must survive serialization bit for bit), an owner of 0 (founded stateless) and int.MaxValue ids.
/// </summary>
public class InstitutionSchemaTests
{
    private static WorldState Populated()
    {
        var w = new WorldState(31);
        w.Institutions.Add(new InstitutionRow(7, new PolityId(2), new SettlementId(5), 3, 412, 1.0 - Math.Exp(-3.0 / 16.0)));
        w.Institutions.Add(new InstitutionRow(1, new PolityId(1), new SettlementId(0), 2, 0, 0.0));
        w.Institutions.Add(new InstitutionRow(int.MaxValue, new PolityId(0), new SettlementId(int.MaxValue), 5, long.MaxValue, 1.0));
        w.Institutions.Add(new InstitutionRow(3, new PolityId(1), new SettlementId(5), 1, 9, -0.0));
        w.Institutions.Add(new InstitutionRow(4, new PolityId(1), new SettlementId(2), 4, 10, double.Epsilon));
        w.Institutions.Add(new InstitutionRow(5, new PolityId(3), new SettlementId(2), 2, 11, BitConverter.Int64BitsToDouble(0x7FF8_0000_0000_0D06)));
        w.ConstructionLabor.Add(new ConstructionLaborRow(new SettlementId(4), 240.0));
        w.ConstructionLabor.Add(new ConstructionLaborRow(new SettlementId(1), 2.0000000000000004));
        w.ConstructionLabor.Add(new ConstructionLaborRow(new SettlementId(int.MaxValue), -0.0));
        return w;
    }

    [Fact]
    public void SchemaV31_PopulatedInstitutionTables_LengthRoundTripAndHashExact()
    {
        Assert.Equal(32, CanonicalSchema.Version);   // v32: H2 TaxGrievances
        WorldState world = Populated();

        using var ms = new MemoryStream();
        using (var writer = new BinaryWriter(ms, System.Text.Encoding.UTF8, leaveOpen: true))
            CanonicalSchema.Write(world, writer);
        Assert.Equal(CanonicalSchema.ExpectedLength(world), ms.Length);
        // The populated rows' own contribution: 6 institution rows × 32 bytes + 3 labour rows × 12 bytes.
        Assert.Equal(6 * 32 + 3 * 12, CanonicalSchema.ExpectedLength(world) - CanonicalSchema.ExpectedLength(new WorldState(31)));

        ms.Position = 0;
        using var reader = new BinaryReader(ms);
        WorldState back = CanonicalSchema.Read(reader);
        Assert.True(WorldStates.StateEquals(world, back), "round-trip drifted");
        Assert.Equal(WorldHash.ComputeHex(world), WorldHash.ComputeHex(back));

        Assert.Equal(world.Institutions.Count, back.Institutions.Count);
        for (int i = 0; i < world.Institutions.Count; i++)
        {
            InstitutionRow a = world.Institutions[i], b = back.Institutions[i];
            Assert.Equal((a.Id, a.Polity, a.Settlement, a.Type, a.FoundedTurn), (b.Id, b.Polity, b.Settlement, b.Type, b.FoundedTurn));
            Assert.Equal(BitConverter.DoubleToInt64Bits(a.Maturity), BitConverter.DoubleToInt64Bits(b.Maturity));
        }
        Assert.Equal([7, 1, int.MaxValue, 3, 4, 5], Ids(back));                                       // row order kept, not sorted
        Assert.True(double.IsNegative(back.Institutions[3].Maturity));                                // −0.0 survived
        Assert.Equal(double.Epsilon, back.Institutions[4].Maturity);                                  // the subnormal survived
        Assert.Equal(0x7FF8_0000_0000_0D06, BitConverter.DoubleToInt64Bits(back.Institutions[5].Maturity)); // NaN payload survived
        for (int i = 0; i < world.ConstructionLabor.Count; i++)
        {
            Assert.Equal(world.ConstructionLabor[i].Settlement, back.ConstructionLabor[i].Settlement);
            Assert.Equal(BitConverter.DoubleToInt64Bits(world.ConstructionLabor[i].LastLaborUsed),
                BitConverter.DoubleToInt64Bits(back.ConstructionLabor[i].LastLaborUsed));
        }
    }

    [Fact]
    public void SchemaV31_AMaturityOrLabourChange_ChangesTheHash_AndTheStreamEndsWithTheTwoTables()
    {
        WorldState a = Populated();
        WorldState b = a.Clone();
        b.Institutions[0] = b.Institutions[0] with
        {
            Maturity = BitConverter.Int64BitsToDouble(BitConverter.DoubleToInt64Bits(b.Institutions[0].Maturity) + 1),
        };
        Assert.NotEqual(WorldHash.ComputeHex(a), WorldHash.ComputeHex(b));    // one ulp of maturity is visible
        WorldState c = a.Clone();
        c.ConstructionLabor[0] = c.ConstructionLabor[0] with { LastLaborUsed = 239.99999999999997 };
        Assert.NotEqual(WorldHash.ComputeHex(a), WorldHash.ComputeHex(c));    // and of published labour
        WorldState d = a.Clone();
        d.Institutions[1] = d.Institutions[1] with { Polity = new PolityId(2) };
        Assert.NotEqual(WorldHash.ComputeHex(a), WorldHash.ComputeHex(d));    // the owner is state

        // The two tables are the LAST v31 blocks (appended after TaxPolicies, in that order): the stream ends with the
        // last labour row (settlement, labour bits), preceded by the labour count and the last institution row.
        // H2 (v32, 2026-10-05): one table now follows them — TaxGrievances, EMPTY here — so the v31 blocks end 4
        // bytes (its zero count prefix) before the stream does.
        using var ms = new MemoryStream();
        using (var writer = new BinaryWriter(ms, System.Text.Encoding.UTF8, leaveOpen: true))
            CanonicalSchema.Write(a, writer);
        byte[] bytes = ms.ToArray();
        const int v32Trailer = 4;
        Assert.Equal(0, BitConverter.ToInt32(bytes, bytes.Length - v32Trailer));
        int end = bytes.Length - v32Trailer;
        Assert.Equal(BitConverter.DoubleToInt64Bits(-0.0), BitConverter.ToInt64(bytes, end - 8));
        Assert.Equal(int.MaxValue, BitConverter.ToInt32(bytes, end - 12));
        int labourBlock = 4 + 3 * 12;
        Assert.Equal(3, BitConverter.ToInt32(bytes, end - labourBlock));
        Assert.Equal(0x7FF8_0000_0000_0D06, BitConverter.ToInt64(bytes, end - labourBlock - 8));

        // An empty world's stream ends with the zero count prefixes (the two v31 ones, then v32's).
        var empty = new WorldState(31);
        using var ems = new MemoryStream();
        using (var writer = new BinaryWriter(ems, System.Text.Encoding.UTF8, leaveOpen: true))
            CanonicalSchema.Write(empty, writer);
        Assert.All(ems.ToArray()[^12..], x => Assert.Equal(0, x));
        Assert.Equal(CanonicalSchema.ExpectedLength(empty), ems.Length);
    }

    [Fact]
    public void SchemaV31_SnapshotSaveLoad_AndClone_KeepTheRowsBitExact_AndIndependent()
    {
        WorldState world = Populated();
        using var ms = new MemoryStream();
        Snapshot.Save(world, ms);
        ms.Position = 0;
        WorldState back = Snapshot.Load(ms);
        Assert.True(WorldStates.StateEquals(world, back));
        Assert.Equal(WorldHash.ComputeHex(world), WorldHash.ComputeHex(back));

        WorldState clone = world.Clone();
        Assert.True(WorldStates.StateEquals(world, clone));
        clone.Institutions[0] = clone.Institutions[0] with { Maturity = 0.5 };
        clone.ConstructionLabor.Clear();
        Assert.False(WorldStates.StateEquals(world, clone));                  // the comparer sees both tables...
        Assert.Equal(6, world.Institutions.Count);                            // ...and the copy shares nothing
        Assert.Equal(3, world.ConstructionLabor.Count);
        Assert.Equal(1.0 - Math.Exp(-3.0 / 16.0), world.Institutions[0].Maturity);
    }

    private static int[] Ids(WorldState w)
    {
        var ids = new int[w.Institutions.Count];
        for (int i = 0; i < ids.Length; i++) ids[i] = w.Institutions[i].Id;
        return ids;
    }
}
