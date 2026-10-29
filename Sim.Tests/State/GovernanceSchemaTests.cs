using Sim.Core.Kernel;
using Sim.Core.State;
using Sim.Tests.TestUtil;

namespace Sim.Tests.State;

/// <summary>
/// ADR-033 D4 — schema v30 (TaxPolicies). Constitution rule: every new serialized row type ships a
/// POPULATED-table test with an exact ExpectedLength, a bit-exact round trip and hash equality. The
/// table is populated out of polity order, with a policy at the two bounds, a negative zero and a
/// NaN payload (a corrupt rate must survive serialization bit for bit; the readers neutralise it),
/// beside a control row whose Strength is a non-dyadic reach — the field the governing loop writes.
/// </summary>
public class GovernanceSchemaTests
{
    private static WorldState Populated()
    {
        var w = new WorldState(30);
        w.TaxPolicies.Add(new TaxPolicyRow(new PolityId(3), 0.35000000000000003));
        w.TaxPolicies.Add(new TaxPolicyRow(new PolityId(1), 1.0));
        w.TaxPolicies.Add(new TaxPolicyRow(new PolityId(2), -0.0));
        w.TaxPolicies.Add(new TaxPolicyRow(new PolityId(int.MaxValue), BitConverter.Int64BitsToDouble(0x7FF8_0000_0000_0C0D)));
        w.Controls.Add(new ControlRow(new PolityId(1), new SettlementId(4), Math.Exp(-37.5 / 25.0)));
        return w;
    }

    [Fact]
    public void SchemaV30_PopulatedTaxPolicyTable_LengthRoundTripAndHashExact()
    {
        Assert.Equal(39, CanonicalSchema.Version);   // v31: ADR-033 D6/D10 Institutions + ConstructionLabor (v32: H2 TaxGrievances) (v33: D-054 ConstructionProgress + ResourceAllocations) (v34: D-055 ConstructionHolds — 2026-10-16 cause: the schema bump alone, the version number this file pins) (v35: ADR-043 CivicsPoints + ADR-044 discovery tables 63–66 — 2026-10-22 cause: the schema bump alone) (v36: ADR-049 ResearchLevels — the schema bump alone) (v37: ADR-051 ResearchReserves — the schema bump alone) (v39: ADR-055 the four military tables — the schema bump alone) (v38: ADR-054 ResearchPopulations — the schema bump alone)
        WorldState world = Populated();

        using var ms = new MemoryStream();
        using (var writer = new BinaryWriter(ms, System.Text.Encoding.UTF8, leaveOpen: true))
            CanonicalSchema.Write(world, writer);
        Assert.Equal(CanonicalSchema.ExpectedLength(world), ms.Length);
        // The populated rows' own contribution: 4 tax rows × 12 bytes + one control row × 16.
        Assert.Equal(4 * 12 + 16, CanonicalSchema.ExpectedLength(world) - CanonicalSchema.ExpectedLength(new WorldState(30)));

        ms.Position = 0;
        using var reader = new BinaryReader(ms);
        WorldState back = CanonicalSchema.Read(reader);
        Assert.True(WorldStates.StateEquals(world, back), "round-trip drifted");
        Assert.Equal(WorldHash.ComputeHex(world), WorldHash.ComputeHex(back));

        Assert.Equal(world.TaxPolicies.Count, back.TaxPolicies.Count);
        for (int i = 0; i < world.TaxPolicies.Count; i++)
        {
            Assert.Equal(world.TaxPolicies[i].Polity, back.TaxPolicies[i].Polity);   // row order kept, not sorted
            Assert.Equal(BitConverter.DoubleToInt64Bits(world.TaxPolicies[i].Rate), BitConverter.DoubleToInt64Bits(back.TaxPolicies[i].Rate));
        }
        Assert.True(double.IsNegative(back.TaxPolicies[2].Rate));                                   // −0.0 survived
        Assert.Equal(0x7FF8_0000_0000_0C0D, BitConverter.DoubleToInt64Bits(back.TaxPolicies[3].Rate)); // NaN payload survived
        Assert.Equal(BitConverter.DoubleToInt64Bits(Math.Exp(-1.5)), BitConverter.DoubleToInt64Bits(back.Controls[0].Strength));

        // The readers neutralise the corrupt and signed rows rather than propagating them.
        Assert.Equal(0.0, Governance.NominalTaxRate(back, new PolityId(int.MaxValue)));   // NaN reads 0
        Assert.Equal(0.0, Governance.NominalTaxRate(back, new PolityId(2)));               // −0.0 clamps to 0
        Assert.Equal(1.0, Governance.NominalTaxRate(back, new PolityId(1)));
        Assert.Equal(0.0, Governance.NominalTaxRate(back, new PolityId(9)));               // no row: the zero default
    }

    [Fact]
    public void SchemaV30_ATaxPolicyChange_ChangesTheHash_AndTheStreamEndsWithTheTable()
    {
        WorldState a = Populated();
        WorldState b = a.Clone();
        b.TaxPolicies[0] = b.TaxPolicies[0] with { Rate = BitConverter.Int64BitsToDouble(BitConverter.DoubleToInt64Bits(0.35000000000000003) + 1) };
        Assert.NotEqual(WorldHash.ComputeHex(a), WorldHash.ComputeHex(b));   // one ulp is visible

        // The table was the LAST block at v30 (appended after RoadDevelopments). Since v31 (ADR-033 D6/D10) the
        // two institution-era tables follow it — Institutions, ConstructionLabor — which are EMPTY here, so the
        // populated stream ends with their two zero count prefixes (8 bytes) right after the last row's rate bits.
        // H2 (v32, 2026-10-05): TaxGrievances follows those, also EMPTY here — one more zero prefix (12 bytes in all).
        // D-054 (v33, 2026-10-14): ConstructionProgress and ResourceAllocations follow, EMPTY here — two more (20 in all).
        // D-055 (v34, 2026-10-16) CAUSE BLOCK: ConstructionHolds follows those, EMPTY here — one more (24 in all). The
        // layout alone; was 5 × 4.
        var empty = new WorldState(30);
        using var ms = new MemoryStream();
        using (var writer = new BinaryWriter(ms, System.Text.Encoding.UTF8, leaveOpen: true))
            CanonicalSchema.Write(a, writer);
        byte[] bytes = ms.ToArray();
        // ADR-043 + ADR-044 (v35, 2026-10-22) CAUSE BLOCK: CivicsPoints and the four discovery tables follow, EMPTY here —
        // 11 × 4 in all (was 6 × 4). The layout alone.
        // ADR-049 (v36) CAUSE BLOCK: ResearchLevels follows, EMPTY — 12 × 4. The layout alone.
        // ADR-051 (v37) CAUSE BLOCK: ResearchReserves follows, EMPTY — 13 × 4. The layout alone.
        // ADR-054 (v38) CAUSE BLOCK: ResearchPopulations follows, EMPTY — 14 × 4. The layout alone.
        const int V31Trailer = 18 * 4;   // ADR-055 (v39): + the four empty military tables (sections 70–73)
        Assert.All(bytes[^V31Trailer..], b => Assert.Equal(0, b));
        Assert.Equal(BitConverter.DoubleToInt64Bits(a.TaxPolicies[3].Rate), BitConverter.ToInt64(bytes, bytes.Length - V31Trailer - 8));
        Assert.Equal(int.MaxValue, BitConverter.ToInt32(bytes, bytes.Length - V31Trailer - 12));
        Assert.Equal(CanonicalSchema.ExpectedLength(empty) + 4 * 12 + 16, bytes.Length);
    }

    [Fact]
    public void SchemaV30_SnapshotSaveLoad_KeepsThePolicyBitExact()
    {
        WorldState world = Populated();
        using var ms = new MemoryStream();
        Snapshot.Save(world, ms);
        ms.Position = 0;
        WorldState back = Snapshot.Load(ms);
        Assert.True(WorldStates.StateEquals(world, back));
        Assert.Equal(WorldHash.ComputeHex(world), WorldHash.ComputeHex(back));
    }
}
