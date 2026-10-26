using Sim.Core.Kernel;
using Sim.Core.State;
using Sim.Tests.TestUtil;

namespace Sim.Tests.State;

/// <summary>
/// H2 (Director 2026-10-05; docs/d049-taxation-and-revolt-model.md) — schema v32 (TaxGrievances: the levy's grievance
/// per (settlement, class) population segment). Constitution rule: every new serialized row type ships a POPULATED-table
/// test with an exact ExpectedLength, a bit-exact round trip and hash equality (the T1.1/T1.3 precedent: an empty table
/// proves nothing). Populated out of key order, with a negative zero, a subnormal, a non-dyadic value, a value past the
/// uprising level and a NaN payload (a corrupt stock must survive serialization bit for bit), and int.MaxValue keys.
/// </summary>
public class TaxGrievanceSchemaTests
{
    private static WorldState Populated()
    {
        var w = new WorldState(32);
        w.TaxGrievances.Add(new TaxGrievanceRow(new SettlementId(5), new ClassId(2), 21.43));
        w.TaxGrievances.Add(new TaxGrievanceRow(new SettlementId(0), new ClassId(1), 0.1 + 0.2));
        w.TaxGrievances.Add(new TaxGrievanceRow(new SettlementId(int.MaxValue), new ClassId(int.MaxValue), -0.0));
        w.TaxGrievances.Add(new TaxGrievanceRow(new SettlementId(2), new ClassId(3), double.Epsilon));
        w.TaxGrievances.Add(new TaxGrievanceRow(new SettlementId(2), new ClassId(1), BitConverter.Int64BitsToDouble(0x7FF8_0000_0000_0D07)));
        return w;
    }

    [Fact]
    public void SchemaV32_PopulatedTaxGrievances_LengthRoundTripAndHashExact()
    {
        Assert.Equal(37, CanonicalSchema.Version);   // v33: D-054 ConstructionProgress + ResourceAllocations (v34: D-055 ConstructionHolds — 2026-10-16 cause: the schema bump alone, the version number this file pins) (v35: ADR-043 CivicsPoints + ADR-044 discovery tables 63–66 — 2026-10-22 cause: the schema bump alone) (v36: ADR-049 ResearchLevels — the schema bump alone) (v37: ADR-051 ResearchReserves — the schema bump alone)
        WorldState world = Populated();

        using var ms = new MemoryStream();
        using (var writer = new BinaryWriter(ms, System.Text.Encoding.UTF8, leaveOpen: true))
            CanonicalSchema.Write(world, writer);
        Assert.Equal(CanonicalSchema.ExpectedLength(world), ms.Length);
        // The populated rows' own contribution: 5 rows × (settlement 4 + class 4 + value bits 8).
        Assert.Equal(5 * 16, CanonicalSchema.ExpectedLength(world) - CanonicalSchema.ExpectedLength(new WorldState(32)));

        // TaxGrievances was the LAST block through v32; v33 (D-054) appends ConstructionProgress and ResourceAllocations
        // after it — here two EMPTY count prefixes (V33Trailer). Before them the stream ends with the last row's value
        // bits, preceded by its class and settlement; the count prefix precedes the five rows.
        // D-055 (2026-10-16) CAUSE BLOCK: v34 appends ConstructionHolds after them — one more EMPTY count prefix, so the
        // trailer is 3 × 4 bytes (was 2 × 4). The layout alone; every row offset below is unchanged relative to it.
        byte[] bytes = ms.ToArray();
        // ADR-043 + ADR-044 (v35, 2026-10-22) CAUSE BLOCK: CivicsPoints and the four discovery tables follow, EMPTY — 8 × 4
        // (was 3 × 4). The layout alone.
        // ADR-049 (v36) CAUSE BLOCK: ResearchLevels follows, EMPTY — 9 × 4. The layout alone.
        // ADR-051 (v37) CAUSE BLOCK: ResearchReserves follows, EMPTY — 10 × 4. The layout alone.
        const int V33Trailer = 10 * 4;
        Assert.All(bytes[^V33Trailer..], x => Assert.Equal(0, x));
        Assert.Equal(0x7FF8_0000_0000_0D07, BitConverter.ToInt64(bytes, bytes.Length - V33Trailer - 8));
        Assert.Equal(1, BitConverter.ToInt32(bytes, bytes.Length - V33Trailer - 12));
        Assert.Equal(2, BitConverter.ToInt32(bytes, bytes.Length - V33Trailer - 16));
        Assert.Equal(5, BitConverter.ToInt32(bytes, bytes.Length - V33Trailer - 4 - 5 * 16));

        ms.Position = 0;
        using var reader = new BinaryReader(ms);
        WorldState back = CanonicalSchema.Read(reader);
        Assert.True(WorldStates.StateEquals(world, back), "round-trip drifted");
        Assert.Equal(WorldHash.ComputeHex(world), WorldHash.ComputeHex(back));
        Assert.Equal(world.TaxGrievances.Count, back.TaxGrievances.Count);
        for (int i = 0; i < world.TaxGrievances.Count; i++)
        {
            TaxGrievanceRow a = world.TaxGrievances[i], b = back.TaxGrievances[i];
            Assert.Equal((a.Settlement, a.Class), (b.Settlement, b.Class));                    // row order kept, not sorted
            Assert.Equal(BitConverter.DoubleToInt64Bits(a.Value), BitConverter.DoubleToInt64Bits(b.Value));
        }
        Assert.True(double.IsNegative(back.TaxGrievances[2].Value));                             // −0.0 survived
        Assert.Equal(double.Epsilon, back.TaxGrievances[3].Value);                               // the subnormal survived
        Assert.Equal(0x7FF8_0000_0000_0D07, BitConverter.DoubleToInt64Bits(back.TaxGrievances[4].Value)); // NaN payload survived
    }

    [Fact]
    public void SchemaV32_OneUlpOfLevyGrievance_IsVisibleInTheHash_AndAnEmptyTableIsOneZeroPrefix()
    {
        WorldState a = Populated();
        WorldState b = a.Clone();
        b.TaxGrievances[0] = b.TaxGrievances[0] with { Value = Math.BitIncrement(b.TaxGrievances[0].Value) };
        Assert.NotEqual(WorldHash.ComputeHex(a), WorldHash.ComputeHex(b));
        WorldState c = a.Clone();
        c.TaxGrievances[1] = c.TaxGrievances[1] with { Class = new ClassId(2) };
        Assert.NotEqual(WorldHash.ComputeHex(a), WorldHash.ComputeHex(c));   // the segment key is state

        var empty = new WorldState(32);
        using var ems = new MemoryStream();
        using (var writer = new BinaryWriter(ems, System.Text.Encoding.UTF8, leaveOpen: true))
            CanonicalSchema.Write(empty, writer);
        Assert.All(ems.ToArray()[^4..], x => Assert.Equal(0, x));
        Assert.Equal(CanonicalSchema.ExpectedLength(empty), ems.Length);
    }

    [Fact]
    public void SchemaV32_SnapshotSaveLoad_AndClone_KeepTheRowsBitExact_AndIndependent()
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
        clone.TaxGrievances[0] = clone.TaxGrievances[0] with { Value = 1.0 };
        Assert.False(WorldStates.StateEquals(world, clone));   // the comparer sees the table ...
        Assert.Equal(21.43, world.TaxGrievances[0].Value);     // ... and the copy shares nothing
    }
}
