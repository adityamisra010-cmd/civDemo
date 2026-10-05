using Sim.Core.Kernel;
using Sim.Core.State;
using Sim.Tests.TestUtil;

namespace Sim.Tests.State;

/// <summary>
/// ADR-032 — schema v29. Constitution rule: every new serialized row type ships a POPULATED-table
/// test with an exact ExpectedLength, a bit-exact round trip and hash equality. The two transport
/// tables are populated with two SEPARATE physical routes between one pair, rows out of id order, a negative
/// zero, a NaN payload, extreme integers and a non-dyadic length.
/// </summary>
public class TransportSchemaTests
{
    private static WorldState Populated()
    {
        var w = new WorldState(29);
        w.TransportEdges.Add(new TransportEdgeRow(7, new SettlementId(1), new SettlementId(4), EdgeTypes.PavedRoad,
            TransportModes.Road, TransportEdgeStates.Complete, 600_000, 123.45000000000002, 0, 31, 44,
            EdgeTypes.Highway, 0.30000000000000004, 0.4729729729729730));
        w.TransportEdges.Add(new TransportEdgeRow(2, new SettlementId(1), new SettlementId(4), EdgeTypes.Trackway,
            TransportModes.Road, TransportEdgeStates.Complete, 150_000, -0.0, 0, 9, 9,
            EdgeTypes.Trackway, -0.0, 0.85));
        w.TransportEdges.Add(new TransportEdgeRow(3, new SettlementId(0), new SettlementId(2), EdgeTypes.Highway,
            TransportModes.Road, TransportEdgeStates.Complete, long.MaxValue,
            BitConverter.Int64BitsToDouble(0x7FF8_0000_0000_1234), int.MinValue, long.MinValue, 5,
            int.MaxValue, BitConverter.Int64BitsToDouble(0x7FF8_0000_0000_4321), double.Epsilon));
        w.RoadDevelopments.Add(new RoadDevelopmentRow(43, new PolityId(2), 7, new SettlementId(1), new SettlementId(4),
            EdgeTypes.BuiltRoad, EdgeTypes.PavedRoad, 1, 900, 494, 0.1, 0.30000000000000004));
        w.RoadDevelopments.Add(new RoadDevelopmentRow(8, new PolityId(1), 2, new SettlementId(1), new SettlementId(4),
            EdgeTypes.DirtPath, EdgeTypes.Trackway, 2, 0, 1, -0.0, 1.0));
        return w;
    }

    [Fact]
    public void SchemaV29_PopulatedTransportTables_LengthAndRoundTripExact()
    {
        Assert.Equal(32, CanonicalSchema.Version);   // v31: ADR-033 D6/D10 Institutions + ConstructionLabor (v32: H2 TaxGrievances)
        WorldState world = Populated();

        using var ms = new MemoryStream();
        using (var writer = new BinaryWriter(ms, System.Text.Encoding.UTF8, leaveOpen: true))
            CanonicalSchema.Write(world, writer);
        Assert.Equal(CanonicalSchema.ExpectedLength(world), ms.Length);
        // The tables' own contribution: edges 3×80, developments 2×68.
        Assert.Equal(3 * 80 + 2 * 68, CanonicalSchema.ExpectedLength(world) - CanonicalSchema.ExpectedLength(new WorldState(29)));

        ms.Position = 0;
        using var reader = new BinaryReader(ms);
        WorldState back = CanonicalSchema.Read(reader);
        Assert.True(WorldStates.StateEquals(world, back), "round-trip drifted");
        Assert.Equal(WorldHash.ComputeHex(world), WorldHash.ComputeHex(back));

        for (int i = 0; i < world.TransportEdges.Count; i++)
        {
            TransportEdgeRow a = world.TransportEdges[i], b = back.TransportEdges[i];
            Assert.Equal((a.Id, a.A, a.B, a.EdgeType, a.Mode, a.State, a.CapacityTonnesPerYear, a.Condition, a.BuiltTurn, a.UpgradedTurn),
                         (b.Id, b.A, b.B, b.EdgeType, b.Mode, b.State, b.CapacityTonnesPerYear, b.Condition, b.BuiltTurn, b.UpgradedTurn));
            Assert.Equal(BitConverter.DoubleToInt64Bits(a.LengthKm), BitConverter.DoubleToInt64Bits(b.LengthKm));
            Assert.Equal(a.TargetClass, b.TargetClass);
            Assert.Equal(BitConverter.DoubleToInt64Bits(a.Modernization), BitConverter.DoubleToInt64Bits(b.Modernization));
            Assert.Equal(BitConverter.DoubleToInt64Bits(a.CostFactor), BitConverter.DoubleToInt64Bits(b.CostFactor));
        }
        Assert.Equal(0x7FF8_0000_0000_4321, BitConverter.DoubleToInt64Bits(back.TransportEdges[2].Modernization));
        Assert.True(double.IsNegative(back.TransportEdges[1].Modernization));
        Assert.True(double.IsNegative(back.RoadDevelopments[1].ProgressBefore));
        Assert.True(double.IsNegative(back.TransportEdges[1].LengthKm));                                    // −0.0 survived
        Assert.Equal(0x7FF8_0000_0000_1234, BitConverter.DoubleToInt64Bits(back.TransportEdges[2].LengthKm)); // NaN payload survived
        // The multigraph survives serialization: both parallel edges between 1 and 4, in row order.
        Assert.Equal([7, 2], TransportQuery.EdgesBetween(back, new SettlementId(4), new SettlementId(1)).Select(e => e.Id));
        Assert.Equal(world.RoadDevelopments[0], back.RoadDevelopments[0]);
        Assert.Equal(world.RoadDevelopments[1], back.RoadDevelopments[1]);
    }

    [Fact]
    public void SchemaV29_EmptyTransportTables_AreTwoCountPrefixes()
    {
        var w = new WorldState(29);
        Assert.Equal(0, w.TransportEdges.Count);
        Assert.Equal(0, w.RoadDevelopments.Count);
        using var ms = new MemoryStream();
        using (var writer = new BinaryWriter(ms, System.Text.Encoding.UTF8, leaveOpen: true))
            CanonicalSchema.Write(w, writer);
        Assert.Equal(CanonicalSchema.ExpectedLength(w), ms.Length);
        // The stream ends with the two zero count prefixes (8 bytes).
        byte[] bytes = ms.ToArray();
        Assert.All(bytes[^8..], b => Assert.Equal(0, b));
    }

    [Fact]
    public void TransportEdgeRow_CarriesNoOwner_AndRoadDevelopmentRowOnlyTheIssuer()
    {
        // Ruling 6: no civilization-owner field on a road. The log row names the ISSUING polity
        // (who ordered and paid) — it is not ownership of the edge.
        foreach (var f in typeof(TransportEdgeRow).GetProperties())
            Assert.NotEqual(typeof(PolityId), f.PropertyType);
        Assert.DoesNotContain(typeof(TransportEdgeRow).GetProperties(), p => p.Name.Contains("Owner", StringComparison.Ordinal));
    }
}
