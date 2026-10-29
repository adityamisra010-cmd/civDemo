using Sim.Core.Kernel;
using Sim.Core.State;
using Sim.Tests.TestUtil;

namespace Sim.Tests.Kernel;

/// <summary>
/// ADR-051 (schema v37) — THE ATTRIBUTION OF THE GOLDEN RE-PINS TO THE LAYOUT ALONE, and THE SWITCH-OFF IDENTITY. Section 68
/// (ResearchReserves) adds one empty count prefix to every stream; with research.json tuning.researchReserve absent (shipped)
/// no reserve row is ever written. Each re-pinned golden's v36 projection (<see cref="SchemaProjection.V36"/>: strip section
/// 68, ASSERT it empty, drop its prefix — and, independently, <see cref="WorldHash.ComputeHexAsSchema"/> at 36) is its v36 pin
/// byte for byte: the values below, which were the raw pins before v37. A hash over every other table equal to the old pin
/// means every state table is identical to before the change. The heavy worlds are asserted where they are already run:
/// ResearchLevelsLayoutAttributionTests (toy, first reign, driven, founded, the pre-tranche-2 AI world),
/// AiFoundingAttributionTests (the live AI world; the pre-M6 AI world) and DiscoveryLayoutAttributionTests (the pre-M6
/// founded world). Old → new raw pins: SnapshotTests.ToyGoldenHash, FoundedGoldenHash, DrivenGoldenTests.Golden,
/// FirstReignTests.PostR1Golden, AiFoundingAttributionTests.S1cTurn300 and PreTranche2Turn300,
/// DiscoveryLayoutAttributionTests.PreM6FoundedGolden and PreM6AiTurn300, ci.yml FOUNDED_GOLDEN.
/// </summary>
public class ResearchReserveLayoutAttributionTests
{
    internal const string V36ToyGolden = "f52a32981fca7cb9b5d747edca1111fce3fc6355addd341dde33bc63f8b332b1";
    internal const string V36FirstReignGolden = "f4073f424ddb853bd7997f528705e66be0eea50b667e6aadf4e09502c8e88f13";
    internal const string V36DrivenGolden = "96ddd58124bcbe8aedd439463caeac09ed93793619ab8ffb43f720b1beea86aa";
    internal const string V36FoundedGolden = "35bb235ff1eb59196e54d7a29177cf3a7881354897c93bb5e25260851674b707";
    internal const string V36AiTurn300 = "d6b4a3cfddea59d0c86cbc603338b40537769c3a0d0501b0c3bd5b534a43d7d2";
    internal const string V36PreTranche2AiTurn300 = "80ace28280e268fd827a83fc5804d9a15b47c5d2178f7f42e043cfd4f71582f1";
    internal const string V36PreM6FoundedGolden = "b84449737c9fd9099c300b0b8405a8fc4df532ba7a5c2d9e897e39a04e9d7804";
    internal const string V36PreM6AiTurn300 = "994e3d1022d608b2e348da34c8771e0fb31535cd8fd28fae96a3ae3d8a55c48e";

    /// <summary>The world wrote no reserve row, its v36 form is the v36 pin by two independent routes, the stream ends in the
    /// empty section-68 count prefix, and its v37 form is <paramref name="v37"/> — the raw pin before schema v38.
    /// ADR-054 (schema v38) RE-RIG: the world also wrote no ResearchPopulations row (SchemaProjection.V37 asserts it), so it
    /// is the v37 world plus the empty section-69 prefix; <paramref name="v37"/> is reached through the v37 projection and
    /// WorldHash.ComputeHexAsSchema(world, 37). A shipped research world is passed here as its ADR-054 switch-off twin
    /// (TestConfigs.PrePerTurnCivics) — Adr054AttributionTests.</summary>
    internal static void LayoutAlone(WorldState world, string v36, string v37)
    {
        Assert.Equal(0, world.ResearchReserves.Count);
        Assert.Equal(v36, SchemaProjection.V36(world));
        // ADR-056: the core route reads the world with the shipped economy packets' INERT rows removed (asserted inert), the
        // same strip SchemaProjection applies — every v36/v37 pin predates the packets.
        WorldState prePackets = EconomyProjection.StripShipped(world);
        Assert.Equal(v36, WorldHash.ComputeHexAsSchema(prePackets, 36));
        // ADR-055 (v39): sections 70–73 follow, empty too (the twin strips the military layer, the R4 rule).
        Assert.Equal(8 + 4 * SchemaProjection.V39TableCount, CanonicalSchema.BytesAppendedSince(prePackets, 36));   // sections 68–73, all empty
        Assert.Equal(new byte[8 + 4 * SchemaProjection.V39TableCount], SchemaProjection.Serialize(world)[^(8 + 4 * SchemaProjection.V39TableCount)..]);
        Assert.Equal(v37, SchemaProjection.V37(world));
        Assert.Equal(v37, WorldHash.ComputeHexAsSchema(prePackets, 37));
        Assert.NotEqual(v36, v37);
    }

    [Fact]
    public void ToyGoldenSeed42Turn200_MovedForTheV37LayoutAlone() =>
        LayoutAlone(SnapshotTests.CanonicalExecutor().Run(SnapshotTests.Genesis(42), 200), V36ToyGolden, Adr054AttributionTests.V37ToyGolden);

    [Fact]
    public void TheShippedContent_HasTheSwitchOff_AndEveryOlderPinIsReachedThroughTheV36Projection()
    {
        Assert.Null(TestConfigs.Sim().Research!.Tuning.Reserve);
        Assert.Equal(39, CanonicalSchema.Version);   // v39: ADR-055 the four military tables — the schema bump alone (v38: ADR-054 ResearchPopulations — the schema bump alone)
        // A world WITH a reserve row is behaviour, never layout: the v36 projection refuses it.
        var w = new WorldState(3);
        w.ResearchReserves.Add(new ResearchReserveRow(new PolityId(1), 1L));
        Assert.ThrowsAny<Exception>(() => SchemaProjection.V36(w));
        Assert.Equal(1, SchemaProjection.StripV37(w));
        Assert.Equal(WorldHash.ComputeHexAsSchema(new WorldState(3), 36), SchemaProjection.V36(w));
    }
}
