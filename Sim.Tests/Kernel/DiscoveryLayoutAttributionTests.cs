using Sim.Core;
using Sim.Core.Kernel;
using Sim.Core.State;
using Sim.Core.Systems;
using Sim.Tests.Systems;
using Sim.Tests.TestUtil;

namespace Sim.Tests.Kernel;

/// <summary>
/// M6 Track EX (D-062; ADR-044 §12; 2026-10-22) — THE ATTRIBUTION OF THE GOLDEN MOVES TO THE DISCOVERY LAYER, beside Track
/// CP's (CivicsPointsAttributionTests). Schema v35 appends sections 62 (CivicsPoints, Track CP) and 63–66 (KnownExtents,
/// Explorations, SettlementSightings, SettlementSurveys — Track EX) and changes no existing row. Each golden is run
/// (a) under <see cref="TestConfigs.PreM6"/> — the whole slice off (Tracks CP, EX and TREE): it writes no row in sections 62–66, so its v34 projection
/// (<see cref="SchemaProjection.V34"/>, which strips sections 62–66 and ASSERTS them empty) is the v34 pin byte for byte
/// (CivicsPointsAttributionTests.V34*) — and, for the founded and AI worlds, its raw hash is pinned here (<c>PreM6*</c>: the
/// v34 stream plus five empty count prefixes), the value the older attribution rigs' ON arms (which run on PreM6, the R4
/// rule) compare with; (b) under <see cref="TestConfigs.PreTrackCp"/> ∘ <see cref="TestConfigs.PreTreeTranche"/> (M6 merge:
/// Track CP and the research-content tranche off, ADR-045) — Track EX ALONE on: the discovery layer writes rows
/// (the seed discs and sightings every step stores) and the world differs from (a) — the EX half of the move. The CP half,
/// measured on the EX-off world, is CivicsPointsAttributionTests; the live (both on) values are the goldens' own pins.
/// The toy pipeline runs no DiscoverySystem and no research: its world is its own twin and moves for the layout alone.
/// </summary>
public class DiscoveryLayoutAttributionTests
{
    // The worlds under TestConfigs.PreM6, RAW (the v34 stream plus the five empty v35 count prefixes, sections 62–66).
    // ADR-051 (schema v37) RE-PIN, LAYOUT ALONE: OLD b84449737c9fd909… -> NEW 9ec11ace4ca354f4… — the empty section-68
    // (ResearchReserves) count prefix; ResearchReserveLayoutAttributionTests proves SchemaProjection.V36 of the live world is OLD.
    internal const string PreM6FoundedGolden = "9ec11ace4ca354f4a70ad5b57831cc2f69ccbea3a00c9c17754b88856031c918";
    // ADR-051 (schema v37) RE-PIN, LAYOUT ALONE: OLD 994e3d1022d608b2… -> NEW b4f6616cd8ce23db… — the empty section-68
    // (ResearchReserves) count prefix; ResearchReserveLayoutAttributionTests proves SchemaProjection.V36 of the live world is OLD.
    internal const string PreM6AiTurn300 = "b4f6616cd8ce23dbde6ca84668b94eab8793f665f2d5981823fa76fd2455a582";

    private static SimConfig Off() => TestConfigs.PreM6(TestConfigs.Sim());
    private static SimConfig ExAlone() => TestConfigs.PreTrackCp(TestConfigs.PreTreeTranche(TestConfigs.Sim()));

    /// <summary>(a) the PreM6 twin is the v34 world (projection), with the pinned raw hash when given; (b) the EX-alone world
    /// writes discovery rows and no CP row, and differs from the twin.</summary>
    private static void AssertExMoves(WorldState twin, WorldState exAlone, string v34, string? twinRaw = null)
    {
        Assert.Equal(v34, SchemaProjection.V34(twin));
        Assert.Equal(new byte[4 * SchemaProjection.V35TableCount], SchemaProjection.Serialize(twin)[^(4 * SchemaProjection.V35TableCount)..]);
        if (twinRaw is not null)
            Assert.True(twinRaw == WorldHash.ComputeHex(twin), "the PreM6 twin's raw hash is " + WorldHash.ComputeHex(twin) + "; pinned " + twinRaw);
        Assert.Equal(0, exAlone.CivicsPoints.Count);
        Assert.True(exAlone.KnownExtents.Count > 0 && exAlone.SettlementSightings.Count > 0, "the discovery layer wrote no row");
        Assert.NotEqual(WorldHash.ComputeHex(twin), WorldHash.ComputeHex(exAlone));
    }

    [Fact]
    public void FoundedGoldenSeed42Turn300_TheDiscoveryLayerMovesIt_ThePreM6TwinIsV34ByteForByte()
    {
        WorldState twin = SnapshotTests.RunFoundedGolden(Off());
        AssertExMoves(twin, SnapshotTests.RunFoundedGolden(ExAlone()), CivicsPointsAttributionTests.V34FoundedGolden, PreM6FoundedGolden);
        ResearchReserveLayoutAttributionTests.LayoutAlone(twin, ResearchReserveLayoutAttributionTests.V36PreM6FoundedGolden, PreM6FoundedGolden);   // ADR-051 (v37)
        // The older links, reached through the twin (they were asserted on the live world before v35).
        Assert.Equal(ReservationsLayoutAttributionTests.Batch3FoundedGolden, SchemaProjection.V33(twin));
        Assert.Equal(SnapshotTests.FoundedGoldenHashAtV32, SchemaProjection.V32(twin));
    }

    [Fact]
    public void ToyGoldenSeed42Turn200_MovedForTheV35LayoutAlone()
    {
        WorldState live = SnapshotTests.CanonicalExecutor().Run(SnapshotTests.Genesis(42), 200);
        Assert.Equal(CivicsPointsAttributionTests.V34ToyGolden, SchemaProjection.V34(live));
        Assert.Equal(new byte[4 * SchemaProjection.V35TableCount], SchemaProjection.Serialize(live)[^(4 * SchemaProjection.V35TableCount)..]);
        Assert.Equal(SnapshotTests.ToyGoldenHash, WorldHash.ComputeHex(live));
    }

    [Fact]
    public void DrivenGoldenSeed42Turn300_TheDiscoveryLayerMovesIt_ThePreM6TwinIsV34ByteForByte() =>
        AssertExMoves(DrivenGoldenTests.RunDriven(300, Off()).World, DrivenGoldenTests.RunDriven(300, ExAlone()).World,
            CivicsPointsAttributionTests.V34DrivenGolden);

    [Fact]
    public void FirstReignTurn40_TheDiscoveryLayerMovesIt_ThePreM6TwinIsV34ByteForByte() =>
        AssertExMoves(FirstReignTests.Replay(40, out _, Off()), FirstReignTests.Replay(40, out _, ExAlone()),
            CivicsPointsAttributionTests.V34FirstReignGolden);

    [Fact]
    public void AiWorldSeed42Turn300_TheDiscoveryLayerMovesIt_ThePreM6TwinIsV34ByteForByte() =>
        AssertExMoves(CivicsPointsAttributionTests.RunAiWorld(Off()), CivicsPointsAttributionTests.RunAiWorld(ExAlone()),
            CivicsPointsAttributionTests.V34AiTurn300, PreM6AiTurn300);
}
