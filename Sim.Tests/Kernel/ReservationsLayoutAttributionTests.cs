using Sim.Core;
using Sim.Core.Kernel;
using Sim.Core.State;
using Sim.Core.Systems;
using Sim.Core.Worldgen;
using Sim.Tests.Systems;
using Sim.Tests.TestUtil;

namespace Sim.Tests.Kernel;

/// <summary>
/// D-055 (2026-10-16; the Director's batch-4 instruction, item 1) — THE LAYOUT ATTRIBUTION OF THE SCHEMA-v34 RE-PINS. Schema
/// v34 appends ONE table, section 61 ConstructionHolds, after ResourceAllocations and changes no existing row. A world that
/// publishes no hold serializes to its v33 stream plus one empty count prefix (4 zero bytes), so every batch-3 hash golden
/// moves FOR THE LAYOUT ALONE — and its v33 projection (<see cref="SchemaProjection.V33"/>, which ASSERTS the strip removed
/// no hold row) must return the batch-3 value byte for byte. These are the five <c>*_MovedForTheV34LayoutAlone</c>
/// companions of the re-pinned goldens (SnapshotTests' toy and founded goldens and ci.yml FOUNDED_GOLDEN, DrivenGoldenTests,
/// FirstReignTests, AiFoundingAttributionTests' live AI arm): each runs the golden's world on the shipped config and the
/// shipped worldgen and asserts (1) the v33 projection is the batch-3 literal, (2) the raw hash is the re-pinned v34 value,
/// and (3) the two differ — so the 4-byte prefix is the whole movement.
///
/// The batch-3 literals are those the batch-3 build pinned (base-b3: de86ace + b3-final-v9). Batch-2 references stay
/// reachable through the PreD054 pair and <see cref="SchemaProjection.V32"/> (D054PairAttributionTests; V32 now drops
/// sections 59, 60 and 61 and asserts all three empty).
///
/// M6 Track EX (2026-10-22; ADR-044 §12): the v34 values these tests re-pinned are HISTORICAL now — schema v35 and the
/// shipped discovery layer moved every live golden again (DiscoveryLayoutAttributionTests). So each world here is run with
/// the discovery layer OFF (TestConfigs.PreDiscovery — the R4 rule: an older layer's attribution strips the newer layers)
/// and compared through SchemaProjection.V34 (the v34 hash, asserting no discovery row) to the v34 literal; the v33
/// projection of the same world still returns batch 3.
/// </summary>
public class ReservationsLayoutAttributionTests
{
    /// <summary>The batch-3 (schema v33) goldens, as the batch-3 build pinned them.</summary>
    internal const string Batch3FoundedGolden = "40115e28af11273b9e25c62d93817c48d6c9afe736a60a2ebc6e00428836f1b0";
    internal const string Batch3ToyGolden = "222280e50f5675575469d07ac11c243717db441ce89e4cd536ca873cb9fc5cd3";
    internal const string Batch3DrivenGolden = "fa1ce900ea15a9abb6f1205fc0728ccb9877676933e70f6610df951037511bc0";
    internal const string Batch3FirstReignGolden = "bccc8aed902d25873373926c3aa16a71f226dae01b0bbd3004bc004a48f8d2fa";
    internal const string Batch3AiTurn300 = "91836bcf92b14914489e3435f64f4104da8637883f0b615475be8233602f20f1";

    /// <summary>The empty section-61 count prefix: what the layout adds to a hold-less world's stream.</summary>
    private static void AssertLayoutAlone(WorldState world, string batch3, string v34)
    {
        Assert.Equal(0, world.ConstructionHolds.Count);
        Assert.Equal(batch3, SchemaProjection.V33(world));            // (1) the batch-3 hash, byte for byte (asserts 0 holds)
        string atV34 = SchemaProjection.V34(world);                    // M6 EX: the v34 hash (asserts no discovery row)
        Assert.True(v34 == atV34, "(2) the v34 hash is " + atV34 + "; pinned " + v34);   // the re-pinned v34 value, in full
        Assert.NotEqual(batch3, v34);                                  // (3) the prefix moved the pin
        byte[] stream = SchemaProjection.Serialize(world);
        Assert.Equal(new byte[4 + 4 * SchemaProjection.V35TableCount], stream[^(4 + 4 * SchemaProjection.V35TableCount)..]);   // + v35's four
    }

    private static SimConfig Pre() => TestConfigs.PreDiscovery(TestConfigs.Sim());

    [Fact]
    public void FoundedGoldenSeed42Turn300_MovedForTheV34LayoutAlone()
    {
        WorldState world = SnapshotTests.RunFoundedGolden(Pre());
        Assert.True(world.Terrain!.WrapX);                             // the shipped cylinder, as the golden runs it
        AssertLayoutAlone(world, Batch3FoundedGolden, DiscoveryLayoutAttributionTests.V34FoundedGolden);
    }

    [Fact]
    public void ToyGoldenSeed42Turn200_MovedForTheV34LayoutAlone()
    {
        WorldState world = SnapshotTests.CanonicalExecutor().Run(SnapshotTests.Genesis(42), 200);
        AssertLayoutAlone(world, Batch3ToyGolden, DiscoveryLayoutAttributionTests.V34ToyGolden);
    }

    [Fact]
    public void DrivenGoldenSeed42Turn300_MovedForTheV34LayoutAlone()
    {
        (WorldState world, _) = DrivenGoldenTests.RunDriven(300, Pre());
        AssertLayoutAlone(world, Batch3DrivenGolden, DiscoveryLayoutAttributionTests.V34DrivenGolden);
    }

    [Fact]
    public void FirstReignTurn40_MovedForTheV34LayoutAlone()
    {
        WorldState world = FirstReignTests.Replay(40, out _, Pre());
        AssertLayoutAlone(world, Batch3FirstReignGolden, DiscoveryLayoutAttributionTests.V34FirstReignGolden);
    }

    /// <summary>The AI world (`sim run --founded --seed 42 --turns 300 --ai-empires 1`, AiFoundingAttributionTests' live
    /// arm) — the one golden world that orders construction. No step of it ends with a queue head (measured on the dev AI
    /// world by the design review; asserted here on the canonical one every turn), so nothing is ever held and the v34
    /// layout is the whole movement; the PreReservations twin gives the same world byte for byte.</summary>
    [Fact]
    public void AiWorldSeed42Turn300_MovedForTheV34LayoutAlone()
    {
        WorldState live = RunAiWorld(Pre(), out int constructionOrders);
        Assert.True(constructionOrders > 0, "the AI ordered no construction - the absence of holds is vacuous");
        AssertLayoutAlone(live, Batch3AiTurn300, DiscoveryLayoutAttributionTests.V34AiTurn300);
        WorldState control = RunAiWorld(TestConfigs.PreReservations(TestConfigs.Sim()), out _);
        Assert.Equal(Batch3AiTurn300, SchemaProjection.V33(control));
        Assert.Equal(WorldHash.ComputeHex(live), WorldHash.ComputeHex(control));
    }

    internal static WorldState RunAiWorld(SimConfig cfg, out int constructionOrders, bool holdFree = true)
    {
        var log = new OrderLog();
        TurnExecutor exec;
        using (var era = Sim.Data.DataFiles.OpenEraPacing())
        using (var pipe = Sim.Data.DataFiles.OpenPipeline())
            exec = new TurnExecutor(EraTableLoader.Load(era), PipelineLoader.Load(pipe, SystemCatalog.All(cfg, TestConfigs.Worldgen())), log);
        WorldState w = WorldFounding.Found(TestConfigs.Worldgen() with { AiEmpires = 1 }, cfg, 42);
        for (int t = 1; t <= 300; t++)
        {
            AiOrders.Append(log, w, cfg, TestConfigs.Worldgen());
            w = exec.Step(w);
            if (holdFree) Assert.Equal(0, w.ConstructionHolds.Count);
        }
        constructionOrders = 0;
        for (int i = 0; i < log.Count; i++)
            if (log[i].Kind == OrderKind.EnqueueConstruction) constructionOrders++;
        return w;
    }

    /// <summary>§1.8 #25 (D-055, X2): the switch OFF is batch 3 byte for byte through the v33 projection — the founded golden
    /// world run under TestConfigs.PreReservations projects to the batch-3 literal, and (no queue, so no hold) equals the
    /// shipped world.</summary>
    [Fact]
    public void FoundedGoldenSeed42Turn300_UnderPreReservations_IsBatch3ThroughV33()
    {
        WorldState control = SnapshotTests.RunFoundedGolden(TestConfigs.PreReservations(TestConfigs.Sim()));
        Assert.Equal(Batch3FoundedGolden, SchemaProjection.V33(control));
        Assert.Equal(DiscoveryLayoutAttributionTests.V34FoundedGolden, SchemaProjection.V34(control));   // M6 EX: v34 = shipped then
    }

    /// <summary>§1.8 #25 (D-055, X2): the driven golden world under TestConfigs.PreReservations projects to its batch-3
    /// literal through V33.</summary>
    [Fact]
    public void DrivenGoldenSeed42Turn300_UnderPreReservations_IsBatch3ThroughV33()
    {
        (WorldState control, _) = DrivenGoldenTests.RunDriven(300, TestConfigs.PreReservations(TestConfigs.Sim()));
        Assert.Equal(Batch3DrivenGolden, SchemaProjection.V33(control));
        Assert.Equal(DiscoveryLayoutAttributionTests.V34DrivenGolden, SchemaProjection.V34(control));   // M6 EX: v34 = shipped then
    }
}
