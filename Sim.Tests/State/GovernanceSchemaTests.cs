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
        Assert.Equal(32, CanonicalSchema.Version);   // v31: ADR-033 D6/D10 Institutions + ConstructionLabor (v32: H2 TaxGrievances)
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
        var empty = new WorldState(30);
        using var ms = new MemoryStream();
        using (var writer = new BinaryWriter(ms, System.Text.Encoding.UTF8, leaveOpen: true))
            CanonicalSchema.Write(a, writer);
        byte[] bytes = ms.ToArray();
        const int V31Trailer = 3 * 4;
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
