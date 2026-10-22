using System.Text.Json.Nodes;
using Sim.Core;
using Sim.Core.Kernel;
using Sim.Core.State;
using Sim.Core.Systems;
using Sim.Core.Worldgen;
using Sim.Tests.TestUtil;

namespace Sim.Tests.Kernel;

/// <summary>
/// Batch 3 WS3 (D-054) — THE ATTRIBUTION CONTROL FOR THE CYLINDER. worldgen.json topology.wrapX is the switch; with it
/// off (<see cref="TestConfigs.PreCylinderWorldgen"/>) every stage of worldgen takes the pre-batch-3 code path and every
/// sim topology site sees a flat TerrainSet, so the terrain's bytes and every world founded on it must be the batch-2
/// tree's BYTE FOR BYTE. The literals below were MEASURED on the batch-2 base build (de86ace + the batch-2 tree, built
/// from base-b2.tar in a scratch directory): `sim worldgen --seed S [--size 256]` for the terrain hashes, and the
/// batch-2 goldens (SnapshotTests.FoundedGoldenHash, the AI world AiFoundingAttributionTests pins as S1cTurn300, and
/// DrivenGoldenTests.Golden) as pinned there.
///
/// With the switch ON every terrain differs, so every world differs FROM TURN 0: the terrain's ContentHash is folded
/// into every WorldHash (CanonicalSchema position 2b) — the first divergent turn of every founded golden is 0, and the
/// cause of every such move is the cylinder (this control).
///
/// BATCH-3 INTEGRATION (2026-10-14): this is the SINGLE-LAYER control of the worldgen half. The SimConfig half (schema
/// v33's two appended tables, WS1, WS2) stays ON here, so the batch-2 literals compare through the v32 projection
/// (SchemaProjection.V32 — the two v33 tables dropped, asserted empty: no order-less or AI world here part-builds or sets
/// a split). The SimConfig half's own control is IncrementalConstructionAttributionTests; both halves together,
/// D054PairAttributionTests.
/// </summary>
[Trait("suite", "determinism")]
public class WrapAttributionTests
{
    /// <summary>`sim worldgen --seed S` on the batch-2 base build (1024 px).</summary>
    private const string FlatTerrain42 = "2419e027d68d776cf851372d3b3a312b289f3e7912f94e0550f5ae0fb461396e";
    private const string FlatTerrain7 = "244c25b630575cff0a0025b2dbf1defbcc546e87da141a84f771e577262bdfa9";

    /// <summary>`sim worldgen --seed S --size 256` on the batch-2 base build.</summary>
    private const string FlatTerrain42Dev = "76c73cf4af075e7beb04e3222b654adaecac4092e8e71a70c1c5ebbecd86aec1";
    private const string FlatTerrain7Dev = "5ff2619ae7d9d2389fb4f239300779bd080034fa23efc0ba538d9db3ced04e35";

    /// <summary>The batch-2 goldens (de86ace + batch 2).</summary>
    private const string Batch2Founded = "937577ffb0e73c2b051d8d7832cfb5028c2154d01c9afef4d976b71ccb5e7f54";
    private const string Batch2AiTurn300 = "b2e4f333c25aca4acf3cff69fcb0da83e59d18eb90efa8e968dc51a2344597d0";
    private const string Batch2Driven = "4f4e0b81a5886201fb3cebbbe74802e8757e15355f6793a320e467276934b4c6";

    private static string Hex(byte[] h) => Convert.ToHexString(h).ToLowerInvariant();

    [Fact]
    public void TheShippedWorldgen_IsACylinder()
    {
        WorldgenConfig shipped = TestConfigs.Worldgen();
        Assert.True(shipped.Topology is { WrapX: true }, "worldgen.json topology.wrapX is not ON");
        Assert.Null(TestConfigs.PreCylinderWorldgen(shipped).Topology);
        // The switch is read from the file, not defaulted: the raw JSON carries it.
        using var s = Sim.Data.DataFiles.OpenWorldgen();
        JsonNode doc = JsonNode.Parse(s)!;
        Assert.True((bool)doc["topology"]!["wrapX"]!);
    }

    [Theory]
    [InlineData(42UL, 1024, FlatTerrain42)]
    [InlineData(7UL, 1024, FlatTerrain7)]
    [InlineData(42UL, 256, FlatTerrain42Dev)]
    [InlineData(7UL, 256, FlatTerrain7Dev)]
    public void WithTheCylinderOff_TheTerrainIsTheBatch2TerrainByteForByte(ulong seed, int size, string flatHash)
    {
        WorldgenConfig shipped = TestConfigs.Worldgen() with { SizePx = size };
        TerrainSet flat = Sim.Core.Worldgen.Worldgen.Generate(TestConfigs.PreCylinderWorldgen(shipped), seed);
        Assert.False(flat.WrapX);
        Assert.Equal(flatHash, Hex(flat.ContentHash));
        // An explicit false is the same flat world as an absent section.
        TerrainSet explicitFalse = Sim.Core.Worldgen.Worldgen.Generate(shipped with { Topology = new TopologyConfig(false) }, seed);
        Assert.Equal(flatHash, Hex(explicitFalse.ContentHash));

        TerrainSet wrapped = Sim.Core.Worldgen.Worldgen.Generate(shipped, seed);
        Assert.True(wrapped.WrapX);
        Assert.NotEqual(flatHash, Hex(wrapped.ContentHash));
    }

    [Fact]
    public void WithTheCylinderOff_TheFoundedGoldenIsTheBatch2Golden_AndOnItDiffersFromTurnZero()
    {
        WorldState flat = SnapshotTests.RunFoundedGolden(TestConfigs.Sim(), TestConfigs.PreD054Worldgen());
        Assert.Equal(Batch2Founded, SchemaProjection.V32(flat));
        WorldState flat0 = TestConfigs.FoundPreD054(42, TestConfigs.Sim());
        WorldState wrap0 = WorldFounding.Found(TestConfigs.Worldgen(), TestConfigs.Sim(), 42);
        Assert.NotEqual(WorldHash.ComputeHex(flat0), WorldHash.ComputeHex(wrap0));   // first divergent turn: 0 (terrain)
    }

    [Fact]
    public void WithTheCylinderOff_TheAiWorldIsTheBatch2AiWorld()
    {
        // ADR-045 (D-062, 2026-10-22): the R4 rule — this control attributes an OLDER layer, so it runs on the pre-tranche research content (TestConfigs.PreTreeTranche); the tranche's own control is ResearchTrancheAttributionTests.
        SimConfig cfg = TestConfigs.PreTreeTrancheSim();
        WorldgenConfig flatWg = TestConfigs.PreD054Worldgen() with { AiEmpires = 1 };
        var log = new OrderLog();
        using var era = Sim.Data.DataFiles.OpenEraPacing();
        using var pipe = Sim.Data.DataFiles.OpenPipeline();
        // The EmpireConstructionStockpileTests.RunAi recipe (= `sim run --founded --seed 42 --turns 300 --ai-empires 1`),
        // founded flat: the pipeline and the AI hold the canonical config, the world is founded with one AI Empire.
        var exec = new TurnExecutor(EraTableLoader.Load(era), PipelineLoader.Load(pipe, SystemCatalog.All(cfg, TestConfigs.PreD054Worldgen())), log);
        WorldState w = WorldFounding.Found(flatWg, cfg, 42);
        for (int t = 1; t <= 300; t++)
        {
            AiOrders.Append(log, w, cfg, TestConfigs.PreD054Worldgen());
            w = exec.Step(w);
        }
        Assert.Equal(Batch2AiTurn300, SchemaProjection.V32(w));
    }

    [Fact]
    public void WithTheCylinderOff_TheDrivenGoldenIsTheBatch2DrivenGolden()
    {
        (WorldState w, _) = DrivenGoldenTests.RunDriven(300, TestConfigs.Sim(), TestConfigs.PreD054Worldgen());
        Assert.Equal(Batch2Driven, SchemaProjection.V32(w));
    }

    [Fact]
    public void TheSystemsNeverReadTheTopologySwitch_OnlyTheTerrainsFlag()
    {
        // A wrapped terrain stepped by a pipeline built from a FLAT worldgen config is the same world as one stepped by
        // a pipeline built from the shipped (wrapped) config — the systems read TerrainSet.WrapX, never the config's
        // topology (the D-052 --size precedent: the pipeline may hold a config that does not describe the terrain).
        SimConfig cfg = TestConfigs.Sim();
        WorldState a = WorldFounding.Found(TestConfigs.DevWorldgen(), cfg, 42);
        WorldState b = WorldFounding.Found(TestConfigs.DevWorldgen(), cfg, 42);
        using var era1 = Sim.Data.DataFiles.OpenEraPacing();
        using var pipe1 = Sim.Data.DataFiles.OpenPipeline();
        var wrappedPipe = new TurnExecutor(EraTableLoader.Load(era1), PipelineLoader.Load(pipe1, SystemCatalog.All(cfg, TestConfigs.Worldgen())));
        using var era2 = Sim.Data.DataFiles.OpenEraPacing();
        using var pipe2 = Sim.Data.DataFiles.OpenPipeline();
        var flatPipe = new TurnExecutor(EraTableLoader.Load(era2), PipelineLoader.Load(pipe2, SystemCatalog.All(cfg, TestConfigs.PreD054Worldgen())));
        for (int t = 0; t < 20; t++) { a = wrappedPipe.Step(a); b = flatPipe.Step(b); }
        Assert.Equal(WorldHash.ComputeHex(a), WorldHash.ComputeHex(b));
    }
}
