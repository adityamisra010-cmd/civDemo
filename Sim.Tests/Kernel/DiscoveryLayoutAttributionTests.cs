using Sim.Core;
using Sim.Core.Kernel;
using Sim.Core.State;
using Sim.Core.Systems;
using Sim.Core.Worldgen;
using Sim.Tests.Systems;
using Sim.Tests.TestUtil;

namespace Sim.Tests.Kernel;

/// <summary>
/// M6 Track EX (D-062; ADR-044 §12; 2026-10-22) — THE ATTRIBUTION OF EVERY GOLDEN THE DISCOVERY LAYER MOVED. Schema v35
/// appends four tables (sections 63–66: KnownExtents, Explorations, SettlementSightings, SettlementSurveys) and changes no
/// existing row; on top of the layout, the shipped fog and concealment change behaviour wherever DiscoverySystem runs (the
/// seed discs and sightings it writes every step; concealed stone, clay and ore that no consumer reads until surveyed; the
/// AI's Explore lane). Each golden below is run TWICE on the same founding, orders and executor: as shipped (its re-pinned
/// v35 value) and under <see cref="TestConfigs.PreDiscovery"/> (sim.json discovery.enabled and fog.enabled false — a
/// single-layer switch-off twin). The twin writes no discovery row, so its v34 projection
/// (<see cref="SchemaProjection.V34"/>, which ASSERTS the four tables empty) must return the v34 build's literal byte for
/// byte — the whole movement is the discovery layer's. The toy pipeline has no DiscoverySystem, so its golden moves for
/// the layout alone (the live world's own v34 projection is the old value).
/// </summary>
public class DiscoveryLayoutAttributionTests
{
    // The v34 goldens, as the v34 build (D-058, base of M6) pinned them.
    internal const string V34FoundedGolden = "0f29efc733024c451b7ebc2e8435fdd8a6032d158922430cb290778a98056d3c";
    internal const string V34ToyGolden = "2e6d446161e165475c753a8962f5b8f9e0845807c911f41be5ba5d982bed5ef7";
    internal const string V34DrivenGolden = "eb88663ac944da5b4bed584e60fa786ce90d1fc9cc6777906576ac608887e893";
    internal const string V34FirstReignGolden = "f7ca60a52362a7da30c73be82ad8ecd56aeeb68f932b2ad98d8d3e76e3219057";
    internal const string V34AiTurn300 = "25f4799d581d8de72dc15d487bcba54cb9db399208aba065963eb1f257e9fd10";

    // The same worlds under the switch-off twin, RAW (the v34 stream plus the four empty v35 count prefixes): what the older
    // attribution rigs, which run with the discovery layer off (the R4 rule), compare their ON arm's last world with.
    internal const string PreDiscoveryFoundedGolden = "PENDING_FOUNDED";
    internal const string PreDiscoveryAiTurn300 = "PENDING_AI";

    private static SimConfig Off() => TestConfigs.PreDiscovery(TestConfigs.Sim());

    /// <summary>(1) the twin's v34 projection is the v34 literal (asserting no discovery row), (2) the live raw hash is the
    /// re-pinned v35 value, (3) the two differ, (4) the twin's stream ends in the four empty v35 count prefixes.</summary>
    private static void AssertMovedForDiscovery(WorldState twin, WorldState live, string v34, string v35, string? twinRaw = null)
    {
        Assert.Equal(v34, SchemaProjection.V34(twin));
        if (twinRaw is not null)
            Assert.True(twinRaw == WorldHash.ComputeHex(twin), "the twin's raw hash is " + WorldHash.ComputeHex(twin) + "; pinned " + twinRaw);
        byte[] stream = SchemaProjection.Serialize(twin);
        Assert.Equal(new byte[4 * SchemaProjection.V35TableCount], stream[^(4 * SchemaProjection.V35TableCount)..]);
        string raw = WorldHash.ComputeHex(live);
        Assert.True(v35 == raw, "the live v35 hash is " + raw + "; pinned " + v35);
        Assert.NotEqual(v34, v35);
    }

    [Fact]
    public void FoundedGoldenSeed42Turn300_MovedForTheDiscoveryLayer_TheTwinIsV34ByteForByte()
    {
        WorldState live = SnapshotTests.RunFoundedGolden();
        WorldState twin = SnapshotTests.RunFoundedGolden(Off());
        AssertMovedForDiscovery(twin, live, V34FoundedGolden, SnapshotTests.FoundedGoldenHash, PreDiscoveryFoundedGolden);
        // The live world's discovery rows are real (the seed discs and sightings every step writes): not a layout move.
        Assert.True(live.KnownExtents.Count > 0 && live.SettlementSightings.Count > 0);
        // The older links, reached through the twin (they were asserted on the live world before v35).
        Assert.Equal(ReservationsLayoutAttributionTests.Batch3FoundedGolden, SchemaProjection.V33(twin));
        Assert.Equal(SnapshotTests.FoundedGoldenHashAtV32, SchemaProjection.V32(twin));
    }

    [Fact]
    public void ToyGoldenSeed42Turn200_MovedForTheV35LayoutAlone()
    {
        WorldState live = SnapshotTests.CanonicalExecutor().Run(SnapshotTests.Genesis(42), 200);
        // The toy pipeline runs no DiscoverySystem: the live world IS its own twin, and the four empty prefixes are the move.
        AssertMovedForDiscovery(live, live, V34ToyGolden, SnapshotTests.ToyGoldenHash);
    }

    [Fact]
    public void DrivenGoldenSeed42Turn300_MovedForTheDiscoveryLayer_TheTwinIsV34ByteForByte()
    {
        (WorldState live, _) = DrivenGoldenTests.RunDriven(300);
        (WorldState twin, _) = DrivenGoldenTests.RunDriven(300, Off());
        AssertMovedForDiscovery(twin, live, V34DrivenGolden, DrivenGoldenTests.Golden);
    }

    [Fact]
    public void FirstReignTurn40_MovedForTheDiscoveryLayer_TheTwinIsV34ByteForByte()
    {
        WorldState live = FirstReignTests.Replay(40, out _);
        WorldState twin = FirstReignTests.Replay(40, out _, Off());
        AssertMovedForDiscovery(twin, live, V34FirstReignGolden, FirstReignTests.PostR1Golden);
    }

    [Fact]
    public void AiWorldSeed42Turn300_MovedForTheDiscoveryLayer_TheTwinIsV34ByteForByte()
    {
        WorldState live = ReservationsLayoutAttributionTests.RunAiWorld(TestConfigs.Sim(), out _, holdFree: false);
        WorldState twin = ReservationsLayoutAttributionTests.RunAiWorld(Off(), out _);
        AssertMovedForDiscovery(twin, live, V34AiTurn300, AiFoundingAttributionTests.S1cTurn300, PreDiscoveryAiTurn300);
    }
}
