using Sim.Core.Kernel;
using Sim.Core.State;
using Sim.Tests.Systems;
using Sim.Tests.TestUtil;

namespace Sim.Tests.Kernel;

/// <summary>
/// ADR-049 (schema v36, D-063 R4) — THE ATTRIBUTION OF THE GOLDEN RE-PINS TO THE LAYOUT ALONE. Section 67 (ResearchLevels)
/// adds one empty count prefix to every stream; with research.json tuning.repeatableLevels off (shipped) no level row is
/// ever written. Each re-pinned golden's v35 projection (<see cref="SchemaProjection.V35"/>: strip section 67, ASSERT it
/// empty, drop its prefix) is its v35 pin byte for byte — the values below, which were the raw pins before v36.
/// </summary>
public class ResearchLevelsLayoutAttributionTests
{
    internal const string V35ToyGolden = "f97432f0e1a7eb5e78c917ee5ab85b7de5e448d3e6f5ee3a6f3d4f3c08f12be6";
    internal const string V35FirstReignGolden = "e3a86ce6d3ed537161cdf842574ea49e99529be2bf6561229163c018c72ac273";
    internal const string V35DrivenGolden = "392e280665e78eefc09540244d7a240bcdbf12445543e703ff91e5d4d6db3d0c";
    internal const string V35FoundedGolden = "8ccbe80fe2be1130c4da27a0576c158d85ece64555a37709ef2dce61b5e5b63c";
    internal const string V35AiTurn300 = "b2201d255f74f4778af17bafe413345d186fca43f4360ba9fc43fea5c74d56e7";

    private static void Layout(WorldState live, string v35, string raw)
    {
        Assert.Equal(0, live.ResearchLevels.Count);
        Assert.Equal(v35, SchemaProjection.V35(live));
        Assert.Equal(raw, WorldHash.ComputeHex(live));
    }

    [Fact]
    public void ToyGoldenSeed42Turn200_MovedForTheV36LayoutAlone() =>
        Layout(SnapshotTests.CanonicalExecutor().Run(SnapshotTests.Genesis(42), 200), V35ToyGolden, SnapshotTests.ToyGoldenHash);

    [Fact]
    public void FirstReignTurn40_MovedForTheV36LayoutAlone() =>
        Layout(FirstReignTests.Replay(40, out _), V35FirstReignGolden, FirstReignTests.PostR1Golden);

    [Fact]
    public void DrivenGoldenSeed42Turn300_MovedForTheV36LayoutAlone() =>
        Layout(DrivenGoldenTests.RunDriven(300).World, V35DrivenGolden, DrivenGoldenTests.Golden);

    [Fact]
    public void FoundedGoldenSeed42Turn300_MovedForTheV36LayoutAlone() =>
        Layout(SnapshotTests.RunFoundedGolden(), V35FoundedGolden, SnapshotTests.FoundedGoldenHash);

    [Fact]
    public void AiWorldSeed42Turn300_MovedForTheV36LayoutAlone() =>
        Layout(CivicsPointsAttributionTests.RunAiWorld(TestConfigs.Sim()), V35AiTurn300, AiFoundingAttributionTests.S1cTurn300);
}
