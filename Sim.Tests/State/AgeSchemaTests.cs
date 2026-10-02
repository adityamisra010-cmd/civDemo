using Sim.Core.Kernel;
using Sim.Core.State;
using Sim.Tests.TestUtil;

namespace Sim.Tests.State;

/// <summary>
/// ADR-031 — schema v28. Constitution rule: every new serialized row type ships a
/// POPULATED-table test with an exact ExpectedLength, a bit-exact round trip and hash
/// equality. The five Age/military tables are populated with more than one polity, rows
/// out of key order, a denormal, a negative zero, a NaN payload and a non-dyadic double.
/// </summary>
public class AgeSchemaTests
{
    private static WorldState Populated()
    {
        var w = new WorldState(13);
        w.AgeStates.Add(new AgeStateRow(new PolityId(3), 4, 120, 2, 120));
        w.AgeStates.Add(new AgeStateRow(new PolityId(1), 2, 7, 6, 7));
        w.AgeEligibility.Add(new AgeEligibilityRow(new PolityId(1), 3, 41, 1, 1, 3, 3, 0b10101, 2, true));
        w.AgeEligibility.Add(new AgeEligibilityRow(new PolityId(3), 5, 41, 1, 2, 0, 3, 0, 3, false));
        w.AgeTransitions.Add(new AgeTransitionRow(new PolityId(1), 1, 2, 6, 6, 7));
        w.AgeTransitions.Add(new AgeTransitionRow(new PolityId(3), 3, 4, 2, 119, 120));
        w.MilitaryUnits.Add(new MilitaryUnitRow(9, new PolityId(3), 3, 302, new SettlementId(4), 17.5, -0.0, 5e-324, 2));
        w.MilitaryUnits.Add(new MilitaryUnitRow(2, new PolityId(1), 2, 201, new SettlementId(-1), 0.1,
            BitConverter.Int64BitsToDouble(0x7FF8_0000_0000_5678), 3.0000000000000004, 0));
        w.UnitConversions.Add(new UnitConversionRow(7, 2, new PolityId(1), 3, 301, 3, 301, 1, 2, 3));
        w.UnitConversions.Add(new UnitConversionRow(120, 9, new PolityId(3), 2, 204, 3, 307, 7, 8, 2));
        return w;
    }

    [Fact]
    public void SchemaV28_PopulatedAgeTables_LengthAndRoundTripExact()
    {
        Assert.Equal(31, CanonicalSchema.Version);   // v31: ADR-033 D6/D10 Institutions + ConstructionLabor
        WorldState world = Populated();

        using var ms = new MemoryStream();
        using (var writer = new BinaryWriter(ms, System.Text.Encoding.UTF8, leaveOpen: true))
            CanonicalSchema.Write(world, writer);
        Assert.Equal(CanonicalSchema.ExpectedLength(world), ms.Length);
        // The tables' own contribution: states 2×28, eligibility 2×41, transitions 2×32,
        // units 2×48, conversions 2×44.
        Assert.Equal(2 * 28 + 2 * 41 + 2 * 32 + 2 * 48 + 2 * 44,
            CanonicalSchema.ExpectedLength(world) - CanonicalSchema.ExpectedLength(new WorldState(13)));

        ms.Position = 0;
        using var reader = new BinaryReader(ms);
        WorldState back = CanonicalSchema.Read(reader);
        Assert.True(WorldStates.StateEquals(world, back), "round-trip drifted");
        Assert.Equal(WorldHash.ComputeHex(world), WorldHash.ComputeHex(back));

        for (int i = 0; i < world.MilitaryUnits.Count; i++)
        {
            MilitaryUnitRow a = world.MilitaryUnits[i], b = back.MilitaryUnits[i];
            Assert.Equal(a.Id, b.Id);
            Assert.Equal(a.Owner, b.Owner);
            Assert.Equal(a.Family, b.Family);
            Assert.Equal(a.Identity, b.Identity);
            Assert.Equal(a.Location, b.Location);
            Assert.Equal(BitConverter.DoubleToInt64Bits(a.X), BitConverter.DoubleToInt64Bits(b.X));
            Assert.Equal(BitConverter.DoubleToInt64Bits(a.Y), BitConverter.DoubleToInt64Bits(b.Y));
            Assert.Equal(BitConverter.DoubleToInt64Bits(a.Experience), BitConverter.DoubleToInt64Bits(b.Experience));
            Assert.Equal(a.Army, b.Army);
        }
        Assert.True(double.IsNegative(back.MilitaryUnits[0].Y));                                         // −0.0 survived
        Assert.Equal(0x7FF8_0000_0000_5678, BitConverter.DoubleToInt64Bits(back.MilitaryUnits[1].Y));    // NaN payload survived
        Assert.Equal(new SettlementId(-1), back.MilitaryUnits[1].Location);
        Assert.Equal(world.AgeStates[0], back.AgeStates[0]);
        Assert.Equal(world.AgeStates[1], back.AgeStates[1]);
        Assert.Equal(world.AgeEligibility[0], back.AgeEligibility[0]);
        Assert.Equal(world.AgeEligibility[1], back.AgeEligibility[1]);
        Assert.Equal(world.AgeTransitions[1], back.AgeTransitions[1]);
        Assert.Equal(world.UnitConversions[0], back.UnitConversions[0]);
        Assert.Equal(world.UnitConversions[1], back.UnitConversions[1]);
    }

    [Theory]
    [InlineData(0)]
    [InlineData(1)]
    [InlineData(2)]
    [InlineData(3)]
    [InlineData(4)]
    [InlineData(5)]
    [InlineData(6)]
    [InlineData(7)]
    [InlineData(8)]
    [InlineData(9)]
    public void SchemaV28_EachAgeTable_IsHashedAndCompared(int table)
    {
        // One field of one row of any Age/military table changes the hash and fails StateEquals,
        // so no table or field is silently left out of the stream or the equality helper.
        WorldState a = Populated(), b = Populated();
        switch (table)
        {
            case 0: b.AgeStates[1] = b.AgeStates[1] with { Surge = 5 }; break;
            case 1: b.AgeStates[0] = b.AgeStates[0] with { SurgeStartTurn = 121 }; break;
            case 2: b.AgeEligibility[0] = b.AgeEligibility[0] with { Eligible = false }; break;
            case 3: b.AgeEligibility[1] = b.AgeEligibility[1] with { CategoryMask = 1 }; break;
            case 4: b.AgeTransitions[0] = b.AgeTransitions[0] with { DecisionTurn = 7 }; break;
            case 5: b.MilitaryUnits[1] = b.MilitaryUnits[1] with { Experience = 3.0 }; break;
            case 6: b.MilitaryUnits[0] = b.MilitaryUnits[0] with { Army = 3 }; break;
            case 7: b.MilitaryUnits[0] = b.MilitaryUnits[0] with { X = 17.500000000000004 }; break;
            case 8: b.UnitConversions[1] = b.UnitConversions[1] with { ToIdentity = 305 }; break;
            default: b.UnitConversions[0] = b.UnitConversions[0] with { Outcome = 4 }; break;
        }
        Assert.NotEqual(WorldHash.ComputeHex(a), WorldHash.ComputeHex(b));
        Assert.False(WorldStates.StateEquals(a, b));
    }

    [Fact]
    public void SchemaV28_AgeTables_SurviveSnapshotSaveAndLoad_AndCloneIndependently()
    {
        WorldState world = Populated();
        using var ms = new MemoryStream();
        Snapshot.Save(world, ms);
        ms.Position = 0;
        WorldState loaded = Snapshot.Load(ms);
        Assert.True(WorldStates.StateEquals(world, loaded));
        Assert.Equal(WorldHash.ComputeHex(world), WorldHash.ComputeHex(loaded));

        WorldState clone = world.Clone();
        clone.MilitaryUnits[0] = clone.MilitaryUnits[0] with { Identity = 303 };
        clone.AgeStates.Add(new AgeStateRow(new PolityId(5), 2, 1, 1, 1));
        Assert.Equal(302, world.MilitaryUnits[0].Identity);
        Assert.Equal(2, world.AgeStates.Count);
    }
}
