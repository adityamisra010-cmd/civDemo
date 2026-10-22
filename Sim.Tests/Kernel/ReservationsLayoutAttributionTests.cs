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
/// ADR-043 (2026-10-22): schema v35 and Track CP moved every golden again (CivicsPointsAttributionTests). The v34 layout
/// attribution here keeps running on the PRE-M6 world (TestConfigs.PreM6: Track CP and Track EX off — the R4 rule), whose
/// raw hash is its v34 hash plus the five empty v35 count prefixes (sections 62–66): so (2) compares the v34 projection with
/// the v34 value (CivicsPointsAttributionTests.V34*), and the stream ends with SIX empty prefixes.
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
        Assert.Equal(0, world.CivicsPoints.Count);                      // ADR-043: the pre-CP world (PreTrackCp)
        Assert.Equal(batch3, SchemaProjection.V33(world));            // (1) the batch-3 hash, byte for byte (asserts 0 holds)
        string raw = SchemaProjection.V34(world);                     // ADR-043: the v34 form (the empty section 62 dropped)
        Assert.True(v34 == raw, "(2) the v34 hash is " + raw + "; pinned " + v34);   // the re-pinned v34 value, in full
        Assert.NotEqual(batch3, v34);                                  // (3) the prefix moved the pin
        byte[] stream = SchemaProjection.Serialize(world);
        Assert.Equal(new byte[4 + 4 * SchemaProjection.V35TableCount], stream[^(4 + 4 * SchemaProjection.V35TableCount)..]);   // sections 61–66: six empty count prefixes
    }

    private static SimConfig PreM6World => TestConfigs.PreM6(TestConfigs.Sim());

    [Fact]
    public void FoundedGoldenSeed42Turn300_MovedForTheV34LayoutAlone()
    {
        WorldState world = SnapshotTests.RunFoundedGolden(PreM6World);
        Assert.True(world.Terrain!.WrapX);                             // the shipped cylinder, as the golden runs it
        AssertLayoutAlone(world, Batch3FoundedGolden, CivicsPointsAttributionTests.V34FoundedGolden);
    }

    [Fact]
    public void ToyGoldenSeed42Turn200_MovedForTheV34LayoutAlone()
    {
        WorldState world = SnapshotTests.CanonicalExecutor().Run(SnapshotTests.Genesis(42), 200);
        AssertLayoutAlone(world, Batch3ToyGolden, CivicsPointsAttributionTests.V34ToyGolden);
    }

    [Fact]
    public void DrivenGoldenSeed42Turn300_MovedForTheV34LayoutAlone()
    {
        (WorldState world, _) = DrivenGoldenTests.RunDriven(300, PreM6World);
        AssertLayoutAlone(world, Batch3DrivenGolden, CivicsPointsAttributionTests.V34DrivenGolden);
    }

    [Fact]
    public void FirstReignTurn40_MovedForTheV34LayoutAlone()
    {
        WorldState world = FirstReignTests.Replay(40, out _, PreM6World);
        AssertLayoutAlone(world, Batch3FirstReignGolden, CivicsPointsAttributionTests.V34FirstReignGolden);
    }

    /// <summary>The AI world (`sim run --founded --seed 42 --turns 300 --ai-empires 1`, AiFoundingAttributionTests' live
    /// arm) — the one golden world that orders construction. No step of it ends with a queue head (measured on the dev AI
    /// world by the design review; asserted here on the canonical one every turn), so nothing is ever held and the v34
    /// layout is the whole movement; the PreReservations twin gives the same world byte for byte.</summary>
    [Fact]
    public void AiWorldSeed42Turn300_MovedForTheV34LayoutAlone()
    {
        WorldState live = RunAiWorld(PreM6World, out int constructionOrders);
        Assert.True(constructionOrders > 0, "the AI ordered no construction - the absence of holds is vacuous");
        AssertLayoutAlone(live, Batch3AiTurn300, CivicsPointsAttributionTests.V34AiTurn300);
        Assert.Equal(AiFoundingAttributionTests.S1cTurn300AtV32, SchemaProjection.V32(live));   // the cylinder-alone link (moved here, ADR-043)
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
        Assert.Equal(CivicsPointsAttributionTests.V34FoundedGolden, SchemaProjection.V34(control));   // ADR-043: the v34 form
    }

    /// <summary>§1.8 #25 (D-055, X2): the driven golden world under TestConfigs.PreReservations projects to its batch-3
    /// literal through V33.</summary>
    [Fact]
    public void DrivenGoldenSeed42Turn300_UnderPreReservations_IsBatch3ThroughV33()
    {
        (WorldState control, _) = DrivenGoldenTests.RunDriven(300, TestConfigs.PreReservations(TestConfigs.Sim()));
        Assert.Equal(Batch3DrivenGolden, SchemaProjection.V33(control));
        Assert.Equal(CivicsPointsAttributionTests.V34DrivenGolden, SchemaProjection.V34(control));   // ADR-043: the v34 form
    }
}
