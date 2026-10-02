using Sim.Core.Kernel;
using Sim.Core.State;
using Sim.Tests.TestUtil;

namespace Sim.Tests.State;

/// <summary>
/// ADR-029 §3 — schema v26. Constitution rule: every new serialized row type ships a
/// POPULATED-table test with an exact ExpectedLength, a bit-exact round trip and hash
/// equality. The seven research tables (five from ADR-029, ResearchCredits and
/// ResearchExposures from addendum A) are populated with more than one polity, keys
/// out of order, a denormal, a negative zero, a NaN payload and a non-dyadic double,
/// all of which any normalisation would move.
/// </summary>
public class ResearchSchemaTests
{
    private static WorldState Populated()
    {
        var w = new WorldState(11);
        w.ResearchTargets.Add(new ResearchTargetRow(new PolityId(3), new ResearchNodeId(1004)));
        w.ResearchTargets.Add(new ResearchTargetRow(new PolityId(1), new ResearchNodeId(17)));
        w.ResearchProgress.Add(new ResearchProgressRow(new PolityId(1), new ResearchNodeId(301), 0.29720704310868246));
        w.ResearchProgress.Add(new ResearchProgressRow(new PolityId(3), new ResearchNodeId(2), 5e-324));
        w.ResearchProgress.Add(new ResearchProgressRow(new PolityId(1), new ResearchNodeId(4), -0.0));
        w.ResearchProgress.Add(new ResearchProgressRow(new PolityId(1), new ResearchNodeId(9), BitConverter.Int64BitsToDouble(0x7FF8_0000_0000_1234)));
        w.ResearchCompleted.Add(new ResearchCompletedRow(new PolityId(1), new ResearchNodeId(1)));
        w.ResearchCompleted.Add(new ResearchCompletedRow(new PolityId(3), new ResearchNodeId(1001)));
        w.ResearchCompleted.Add(new ResearchCompletedRow(new PolityId(1), new ResearchNodeId(2)));
        w.ResearchEurekas.Add(new ResearchEurekaRow(new PolityId(1), new ResearchNodeId(11), 1));
        w.ResearchEurekas.Add(new ResearchEurekaRow(new PolityId(3), new ResearchNodeId(11), 0));
        w.ResearchCostModifiers.Add(new ResearchCostModifierRow(new PolityId(1), 2, 0.625));
        w.ResearchCostModifiers.Add(new ResearchCostModifierRow(new PolityId(3), 5, 1.0));
        w.ResearchCredits.Add(new ResearchCreditRow(new PolityId(3), new ResearchNodeId(1004), 2, 0.1));
        w.ResearchCredits.Add(new ResearchCreditRow(new PolityId(1), new ResearchNodeId(11), 1, 5e-324));
        w.ResearchCredits.Add(new ResearchCreditRow(new PolityId(1), new ResearchNodeId(11), 2, BitConverter.Int64BitsToDouble(0x7FF8_0000_0000_4321)));
        w.ResearchExposures.Add(new ResearchExposureRow(new PolityId(3), new ResearchNodeId(301), -0.0));
        w.ResearchExposures.Add(new ResearchExposureRow(new PolityId(1), new ResearchNodeId(2), 1672.0000000000002));
        return w;
    }

    [Fact]
    public void SchemaV26_PopulatedResearchTables_LengthAndRoundTripExact()
    {
        Assert.Equal(29, CanonicalSchema.Version);
        WorldState world = Populated();

        using var ms = new MemoryStream();
        using (var writer = new BinaryWriter(ms, System.Text.Encoding.UTF8, leaveOpen: true))
            CanonicalSchema.Write(world, writer);
        Assert.Equal(CanonicalSchema.ExpectedLength(world), ms.Length);
        // The tables' own contribution: targets 2×8, progress 4×16, completed 3×8, fired Eurekas 2×12,
        // modifiers 2×16, credits 3×20, exposures 2×16.
        Assert.Equal(2 * 8 + 4 * 16 + 3 * 8 + 2 * 12 + 2 * 16 + 3 * 20 + 2 * 16,
            CanonicalSchema.ExpectedLength(world) - CanonicalSchema.ExpectedLength(new WorldState(11)));

        ms.Position = 0;
        using var reader = new BinaryReader(ms);
        WorldState back = CanonicalSchema.Read(reader);
        Assert.True(WorldStates.StateEquals(world, back), "round-trip drifted");
        Assert.Equal(WorldHash.ComputeHex(world), WorldHash.ComputeHex(back));

        for (int i = 0; i < world.ResearchProgress.Count; i++)
        {
            ResearchProgressRow a = world.ResearchProgress[i], b = back.ResearchProgress[i];
            Assert.Equal(a.Polity, b.Polity);
            Assert.Equal(a.Node, b.Node);
            Assert.Equal(BitConverter.DoubleToInt64Bits(a.Progress), BitConverter.DoubleToInt64Bits(b.Progress));
        }
        Assert.True(double.IsNegative(back.ResearchProgress[2].Progress)); // −0.0 survived
        Assert.Equal(0x7FF8_0000_0000_1234, BitConverter.DoubleToInt64Bits(back.ResearchProgress[3].Progress)); // NaN payload survived
        Assert.Equal(new ResearchNodeId(1004), back.ResearchTargets[0].Node);
        Assert.Equal(1, back.ResearchEurekas[0].Eureka);
        Assert.Equal(0, back.ResearchEurekas[1].Eureka);
        for (int i = 0; i < world.ResearchCredits.Count; i++)
        {
            ResearchCreditRow a = world.ResearchCredits[i], b = back.ResearchCredits[i];
            Assert.Equal(a.Polity, b.Polity);
            Assert.Equal(a.Node, b.Node);
            Assert.Equal(a.Source, b.Source);
            Assert.Equal(BitConverter.DoubleToInt64Bits(a.Amount), BitConverter.DoubleToInt64Bits(b.Amount));
        }
        Assert.Equal(0x7FF8_0000_0000_4321, BitConverter.DoubleToInt64Bits(back.ResearchCredits[2].Amount)); // NaN payload survived
        Assert.True(double.IsNegative(back.ResearchExposures[0].Offered));                                    // −0.0 survived
        Assert.Equal(1672.0000000000002, back.ResearchExposures[1].Offered);
        Assert.Equal(new ResearchNodeId(301), back.ResearchExposures[0].Node);
        Assert.Equal(5, back.ResearchCostModifiers[1].UniversityType);
        Assert.Equal(0.625, back.ResearchCostModifiers[0].Factor);
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
    public void SchemaV26_EachResearchTable_IsHashedAndCompared(int table)
    {
        // A world differing in ONE field of ONE row of any research table hashes differently
        // and fails StateEquals — so no table is silently left out of the stream or the
        // equality helper.
        WorldState a = Populated(), b = Populated();
        switch (table)
        {
            case 0: b.ResearchTargets[1] = b.ResearchTargets[1] with { Node = new ResearchNodeId(18) }; break;
            case 1: b.ResearchProgress[0] = b.ResearchProgress[0] with { Progress = 0.2972070431086825 }; break;
            case 2: b.ResearchCompleted[2] = b.ResearchCompleted[2] with { Node = new ResearchNodeId(3) }; break;
            case 3: b.ResearchEurekas[1] = b.ResearchEurekas[1] with { Eureka = 2 }; break;
            case 5: b.ResearchCredits[0] = b.ResearchCredits[0] with { Amount = 0.10000000000000002 }; break;
            case 6: b.ResearchCredits[1] = b.ResearchCredits[1] with { Source = 2 }; break;
            case 7: b.ResearchExposures[1] = b.ResearchExposures[1] with { Offered = 1672.0 }; break;
            case 8: b.ResearchExposures[1] = b.ResearchExposures[1] with { Polity = new PolityId(2) }; break;
            default: b.ResearchCostModifiers[0] = b.ResearchCostModifiers[0] with { Factor = 0.6250000000000001 }; break;
        }
        Assert.NotEqual(WorldHash.ComputeHex(a), WorldHash.ComputeHex(b));
        Assert.False(WorldStates.StateEquals(a, b));
    }

    [Fact]
    public void SchemaV26_ResearchTables_SurviveSnapshotSaveAndLoad_AndCloneIndependently()
    {
        WorldState world = Populated();
        using var ms = new MemoryStream();
        Snapshot.Save(world, ms);
        ms.Position = 0;
        WorldState loaded = Snapshot.Load(ms);
        Assert.True(WorldStates.StateEquals(world, loaded));

        WorldState clone = world.Clone();
        Assert.True(WorldStates.StateEquals(world, clone));
        clone.ResearchProgress[0] = clone.ResearchProgress[0] with { Progress = 9.0 };
        clone.ResearchCompleted.Add(new ResearchCompletedRow(new PolityId(1), new ResearchNodeId(5)));
        Assert.Equal(0.29720704310868246, world.ResearchProgress[0].Progress);
        Assert.Equal(3, world.ResearchCompleted.Count);
    }
}
