using System.Text.Json.Nodes;
using Sim.Core;
using Sim.Core.Kernel;
using Sim.Core.State;
using Sim.Core.Systems;
using Sim.Core.Worldgen;
using Sim.Tests.TestUtil;

namespace Sim.Tests.Kernel;

/// <summary>
/// D-054 (batch 3, integration 2026-10-14) — THE PreD054 PAIR, BOTH HALVES. Batch 3 has two switch homes: sim.json
/// (construction.incremental, WS1; construction.allocationRatios, WS2 — stripped by <see cref="TestConfigs.PreD054Sim"/>)
/// and worldgen.json (topology.wrapX, WS3 — stripped by <see cref="TestConfigs.PreD054Worldgen(WorldgenConfig)"/>), plus
/// schema v33's two appended tables, which the v32 projection (<see cref="SchemaProjection.V32"/>, asserting both tables
/// empty) removes. With both halves off and the projection, every batch-2 reference must reproduce BYTE FOR BYTE on the
/// merged tree — the five goldens the batch-2 integration worktree pinned:
/// <list type="bullet">
/// <item>founded: SnapshotTests.FoundedGoldenHash (seed 42, 1024², N = 12, 300 no-order turns);</item>
/// <item>AI: AiFoundingAttributionTests.S1cTurn300 (`sim run --founded --seed 42 --turns 300 --ai-empires 1`);</item>
/// <item>driven: DrivenGoldenTests.Golden (the driven order batch, 300 turns);</item>
/// <item>toy: SnapshotTests' toy golden (the M0 two-region world, toy pipeline, 200 turns — no terrain, so only the
///   sim half applies);</item>
/// <item>first reign: FirstReignTests.PostR1Golden (the director session, N = 1, 40 turns).</item>
/// </list>
/// Each single-layer control lives beside its layer: WrapAttributionTests (the cylinder alone),
/// IncrementalConstructionAttributionTests (the v33 layout and WS1 alone).
/// </summary>
[Trait("suite", "determinism")]
public class D054PairAttributionTests
{
    private const string Batch2Founded = "937577ffb0e73c2b051d8d7832cfb5028c2154d01c9afef4d976b71ccb5e7f54";
    private const string Batch2AiTurn300 = "b2e4f333c25aca4acf3cff69fcb0da83e59d18eb90efa8e968dc51a2344597d0";
    private const string Batch2Driven = "4f4e0b81a5886201fb3cebbbe74802e8757e15355f6793a320e467276934b4c6";
    private const string Batch2ToySeed42Turn200 = "5994e97838d275d3dd2a82cc6d0df1e23c70d2a9bbe412f4ba44538e043db180";
    private const string Batch2FirstReignTurn40 = "467f2cb6f5e8ff8171840de57dc44999d2f0347b3b8af8c9ef8451e199d22abb";

    /// <summary>The sim half off: the shipped SimConfig with WS1 and WS2 stripped.</summary>
    private static SimConfig Sim0 => TestConfigs.PreD054Sim(TestConfigs.Sim());

    /// <summary>The worldgen half off: the shipped worldgen with the cylinder stripped.</summary>
    private static WorldgenConfig Wg0 => TestConfigs.PreD054Worldgen();

    private static TurnExecutor Production(SimConfig cfg, OrderLog orders)
    {
        using var era = Sim.Data.DataFiles.OpenEraPacing();
        using var pipe = Sim.Data.DataFiles.OpenPipeline();
        return new TurnExecutor(EraTableLoader.Load(era), PipelineLoader.Load(pipe, SystemCatalog.All(cfg, Wg0)), orders);
    }

    [Fact]
    public void TheShippedContent_SwitchesAllThreeBatch3LayersOn()
    {
        // Read from the shipped files themselves (not defaulted by a loader).
        JsonNode sim, worldgen;
        using (Stream s = Sim.Data.DataFiles.OpenSim()) sim = JsonNode.Parse(s)!;
        using (Stream s = Sim.Data.DataFiles.OpenWorldgen()) worldgen = JsonNode.Parse(s)!;
        Assert.True((bool)sim["construction"]!["incremental"]!, "sim.json construction.incremental is not ON");
        Assert.True((bool)sim["construction"]!["allocationRatios"]!, "sim.json construction.allocationRatios is not ON");
        Assert.True((bool)worldgen["topology"]!["wrapX"]!, "worldgen.json topology.wrapX is not ON");

        // ...and as loaded.
        SimConfig shipped = TestConfigs.Sim();
        Assert.True(shipped.Construction!.Incremental);
        Assert.True(shipped.Construction!.AllocationRatios);
        Assert.True(TestConfigs.Worldgen().Topology is { WrapX: true });

        // The pair strips exactly them.
        Assert.False(Sim0.Construction!.Incremental);
        Assert.False(Sim0.Construction!.AllocationRatios);
        Assert.Null(Wg0.Topology);
        Assert.False(TestConfigs.FoundPreD054(42, TestConfigs.Sim(), TestConfigs.DevWorldgen()).Terrain!.WrapX);
    }

    /// <summary>Both halves off plus the v32 projection: the batch-2 world, byte for byte.</summary>
    [Theory]
    [InlineData("founded")]
    [InlineData("ai")]
    [InlineData("driven")]
    [InlineData("toy")]
    [InlineData("first-reign")]
    public void WithBothHalvesOff_EveryBatch2ReferenceReproducesByteForByte_ThroughTheV32Projection(string reference)
    {
        (string expected, WorldState world) = reference switch
        {
            "founded" => (Batch2Founded, SnapshotTests.RunFoundedGolden(Sim0, Wg0)),
            "ai" => (Batch2AiTurn300, RunAi()),
            "driven" => (Batch2Driven, DrivenGoldenTests.RunDriven(300, Sim0, Wg0).World),
            "toy" => (Batch2ToySeed42Turn200, RunToy()),
            "first-reign" => (Batch2FirstReignTurn40, Sim.Tests.Systems.FirstReignTests.Replay(40, out _, Sim0, Wg0)),
            _ => throw new ArgumentOutOfRangeException(nameof(reference), reference, "unknown reference"),
        };
        if (world.Terrain is { } terrain) Assert.False(terrain.WrapX);       // founded flat (the worldgen half off)
        Assert.Equal(expected, SchemaProjection.V32(world));                  // asserts no v33 row was stripped
        Assert.NotEqual(expected, WorldHash.ComputeHex(world));               // the v33 layout alone moves the raw hash
    }

    /// <summary>The AiFoundingAttributionTests recipe, founded flat with the sim half off.</summary>
    private static WorldState RunAi()
    {
        SimConfig cfg = Sim0;
        var log = new OrderLog();
        TurnExecutor exec = Production(cfg, log);
        WorldState w = WorldFounding.Found(Wg0 with { AiEmpires = 1 }, cfg, 42);
        for (int t = 1; t <= 300; t++)
        {
            AiOrders.Append(log, w, cfg, Wg0);
            w = exec.Step(w);
        }
        return w;
    }

    /// <summary>SnapshotTests' toy golden world (Genesis + the toy pipeline, 200 turns) with the sim half off. It has no
    /// terrain, so the worldgen half has nothing to strip.</summary>
    private static WorldState RunToy()
    {
        using var era = Sim.Data.DataFiles.OpenEraPacing();
        using var pipe = Sim.Data.DataFiles.OpenPipelineToy();
        var exec = new TurnExecutor(EraTableLoader.Load(era), PipelineLoader.Load(pipe, SystemCatalog.All(Sim0)));
        WorldState world = exec.Run(SnapshotTests.Genesis(42), 200);
        Assert.Null(world.Terrain);
        return world;
    }
}
