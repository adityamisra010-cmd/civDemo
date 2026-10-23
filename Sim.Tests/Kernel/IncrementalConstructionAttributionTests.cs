using Sim.Core;
using Sim.Core.Kernel;
using Sim.Core.State;
using Sim.Core.Systems;
using Sim.Core.Worldgen;
using Sim.Tests.TestUtil;

namespace Sim.Tests.Kernel;

/// <summary>
/// D-054 WS1 (2026-10-14) — THE ATTRIBUTION CONTROLS for incremental construction and schema v33.
///
/// Two things move the batch-2 pins, and they are separated by measurement, not by argument:
/// <list type="number">
/// <item>LAYOUT: schema v33 appends two tables (ConstructionProgress, ResourceAllocations). A world that writes no row into
///   either serializes to the v32 stream plus two empty count prefixes, so its v32 projection
///   (<see cref="SchemaProjection.V32"/>, which ASSERTS the two tables are empty) must return the batch-2 hash byte for
///   byte. The founded, driven, toy and first-reign worlds issue no construction order: they move for the layout ALONE.</item>
/// <item>BEHAVIOUR (rewritten at integration R1, 2026-10-14, to the MEASURED fact — C2 review F6): incremental construction
///   (sim.json construction.incremental, the WS1 switch) does NOT move the AI world. The AI orders construction, but only
///   when its purse covers the project's remaining inputs, so every head it orders finishes in the step it is delivered:
///   with the switch on and off (<see cref="TestConfigs.PreIncrementalConstruction"/>) the 300-turn AI worlds are
///   byte-identical on every turn, no queue entry or progress row survives any step, and the two order logs are equal
///   (<see cref="AiWorldSeed42Turn300_MovedForTheV33LayoutAlone_TheIncrementalRuleNeverActsThere"/> pins that absence; the
///   AI world moved for the v33 layout alone, and on the shipped cylinder for the cylinder too). WS1's per-layer proof
///   therefore runs where the rule DOES act: the dev founded world (seed 42, 256 px) with ONE player order — a granary at
///   the capital the empire cannot pay for whole — where the switch-on and switch-off worlds are byte-identical until the
///   first step whose ON world holds a progress row (the order's delivery step, the one state the whole-or-nothing rule
///   cannot produce) and differ from that very step on
///   (<see cref="PerLayer_IncrementalConstruction_APlayersGranaryDivergesExactlyAtTheFirstStepItIsPartBuilt"/>).</item>
/// </list>
/// The batch-2 constants are the pins the integration worktree carried before D-054 (DrivenGoldenTests.Golden,
/// SnapshotTests.FoundedGoldenHash, AiFoundingAttributionTests.S1cTurn300, SnapshotTests' toy golden, FirstReignTests.PostR1Golden).
///
/// BATCH-3 INTEGRATION (2026-10-14): these are the SINGLE-LAYER controls of the SimConfig half (the v33 layout and WS1/WS2),
/// so every world they compare with a batch-2 literal is founded with the OTHER half — the cylinder, WS3 — off
/// (TestConfigs.PreD054Worldgen, the flat pre-batch-3 world; the PreD054 pair rule). The toy world has no terrain. The
/// cylinder's own single-layer control is WrapAttributionTests; both halves together, D054PairAttributionTests.
/// </summary>
public class IncrementalConstructionAttributionTests
{
    private const string Batch2Founded = "937577ffb0e73c2b051d8d7832cfb5028c2154d01c9afef4d976b71ccb5e7f54";
    private const string Batch2Driven = "4f4e0b81a5886201fb3cebbbe74802e8757e15355f6793a320e467276934b4c6";
    private const string Batch2ToySeed42Turn200 = "5994e97838d275d3dd2a82cc6d0df1e23c70d2a9bbe412f4ba44538e043db180";
    private const string Batch2FirstReignTurn40 = "467f2cb6f5e8ff8171840de57dc44999d2f0347b3b8af8c9ef8451e199d22abb";

    // ADR-043 (2026-10-22) CAUSE BLOCK: was TestConfigs.Sim(). Track CP (the two research lanes and Civics Points) is NEWER
    // than every layer this class attributes, so by the R4 rule its controls run on the pre-CP world. M6 Track EX (same day):
    // so is the discovery layer (under the shipped concealment the dev capital's empire gathers no stone for the per-layer
    // rig's granary in 60 turns — MEASURED: "the granary was not finished in both worlds"). Both off: TestConfigs.PreM6, whose
    // v32 projection is the batch-2 world exactly as before.
    private static SimConfig Shipped => TestConfigs.PreM6(TestConfigs.Sim());

    [Fact]
    public void TheShippedContent_SwitchesIncrementalConstructionOn_AndEveryOlderTwinStripsIt()
    {
        SimConfig shipped = Shipped;
        Assert.True(shipped.Construction!.Incremental);                       // sim.json construction.incremental
        Assert.True(ConstructionQuery.Incremental(shipped));
        Assert.False(ConstructionQuery.Incremental(TestConfigs.PreIncrementalConstruction(shipped)));
        Assert.False(ConstructionQuery.Incremental(TestConfigs.PreD054Sim(shipped)));
        // The switch twin changes the switch — and WS2's, which needs it (D-054: the loader refuses allocationRatios without
        // incremental) — and nothing else in the construction block.
        // D-055 (2026-10-16) CAUSE BLOCK: the expected block gains Reservations = false. Cause: the D-055 reservations
        // switch (sim.json construction.reservations, shipped true) is NEWER than WS1, so by the R4 rule this twin strips
        // it too (the loader refuses reservations without incremental). Nothing else in the block moved.
        // D-058 (2026-10-19) CAUSE BLOCK: the expected block gains ContinueWithinTurn = false. Cause: the D-058 switch
        // (sim.json construction.continueWithinTurn, shipped true; the Director's "build one at a time" ruling) is newer
        // still, and the loader refuses it without incremental, so by the R4 rule this twin strips it as well.
        Assert.Equal(shipped.Construction with { Incremental = false, AllocationRatios = false, Reservations = false, ContinueWithinTurn = false },
            TestConfigs.PreIncrementalConstruction(shipped).Construction);
        // The R4 rule: every older SimConfig twin strips the newer D-054 batch too — and the D-055 reservations switch.
        foreach (SimConfig twin in new[]
                 {
                     TestConfigs.PreIncrementalConstruction(shipped), TestConfigs.PreD054Sim(shipped),
                     TestConfigs.PreD053(shipped), TestConfigs.PreForager(shipped), TestConfigs.PreR2b(shipped),
                     TestConfigs.PreTimeAveragedHarvest(shipped), TestConfigs.PreTaxAge(shipped), TestConfigs.PreAiFounding(shipped),
                     TestConfigs.PreTradeKnowledge(shipped), TestConfigs.PreRecipeKnowledge(shipped),
                 })
        {
            Assert.False(ConstructionQuery.Incremental(twin));
            Assert.False(twin.Construction!.Reservations);
            Assert.False(Reservations.Enabled(twin));
            Assert.False(TestConfigs.PreIncrementalConstruction(TestConfigs.Sim()).Research!.Tuning.TwoLanes);   // ADR-043: R4
            Assert.False(twin.Research!.Tuning.TwoLanes);   // ADR-043 (2026-10-22): every older twin strips Track CP too
        }
    }

    /// <summary>
    /// D-055 (2026-10-16) — THE SHIPPED CONTENT SWITCHES RESERVATIONS ON, and the twin enumeration of the batch-4 design
    /// (X2): <see cref="TestConfigs.PreReservations"/> changes exactly that switch; the SINGLE-LAYER helpers keep it on
    /// (PreEmpireConstructionStockpile, PreEmpireFoodAllocation, PreGranaryEffects and PreAllocationRatios — the last the
    /// recorded R4 exception: the split's default must equal its absence); the chained twins strip it (above).
    /// </summary>
    [Fact]
    public void TheShippedContent_SwitchesReservationsOn_ThePreReservationsTwinChangesOnlyIt_AndTheSingleLayerHelpersKeepIt()
    {
        SimConfig shipped = Shipped;
        Assert.True(shipped.Construction!.Reservations);                      // sim.json construction.reservations
        Assert.True(Reservations.Enabled(shipped));
        SimConfig off = TestConfigs.PreReservations(shipped);
        // D-058 (2026-10-19) CAUSE BLOCK: the expected block gains ContinueWithinTurn = false. Cause: the D-058 switch is
        // NEWER than reservations, so by the R4 rule this twin strips it too — a PreReservations world stays batch 3's
        // byte for byte (its v33 projection). The single-layer helpers below keep both; PreContinueWithinTurn is D-058's
        // own single-layer twin (ContinueWithinTurnTests).
        Assert.Equal(shipped.Construction with { Reservations = false, ContinueWithinTurn = false }, off.Construction);
        Assert.Equal(shipped with { Construction = off.Construction }, off);  // nothing else in the config moved
        Assert.False(Reservations.Enabled(off));
        foreach (SimConfig single in new[]
                 {
                     TestConfigs.PreEmpireConstructionStockpile(shipped), TestConfigs.PreEmpireFoodAllocation(shipped),
                     TestConfigs.PreGranaryEffects(shipped), TestConfigs.PreAllocationRatios(shipped),
                 })
            Assert.True(Reservations.Enabled(single));
        // The predicate needs incremental construction too: a code-built config with the switch on and incremental off is
        // inert (the loader refuses it — ConstructionHoldsSchemaV34Tests.TheLoader_RefusesReservationsWithoutIncrementalConstruction_AndDoesNotRequireTheEmpirePurse).
        Assert.False(Reservations.Enabled(shipped with { Construction = shipped.Construction with { Incremental = false } }));
        Assert.False(Reservations.Enabled(shipped with { Construction = null }));
    }

    [Fact]
    public void FoundedGoldenSeed42Turn300_MovedForTheV33LayoutAlone()
    {
        WorldState live = SnapshotTests.RunFoundedGolden(Shipped, TestConfigs.PreD054Worldgen());
        Assert.Equal(Batch2Founded, SchemaProjection.V32(live));           // no v33 row; the rest is batch 2's world
        Assert.NotEqual(Batch2Founded, WorldHash.ComputeHex(live));        // ...and the two empty prefixes did move the pin
        Assert.Equal(Batch2Founded, SchemaProjection.V32(SnapshotTests.RunFoundedGolden(TestConfigs.PreD054Sim(Shipped), TestConfigs.PreD054Worldgen())));
    }

    [Fact]
    public void DrivenGoldenSeed42Turn300_MovedForTheV33LayoutAlone()
    {
        (WorldState live, _) = DrivenGoldenTests.RunDriven(300, Shipped, TestConfigs.PreD054Worldgen());
        Assert.Equal(Batch2Driven, SchemaProjection.V32(live));
        Assert.NotEqual(Batch2Driven, WorldHash.ComputeHex(live));
        (WorldState off, _) = DrivenGoldenTests.RunDriven(300, TestConfigs.PreD054Sim(Shipped), TestConfigs.PreD054Worldgen());
        Assert.Equal(Batch2Driven, SchemaProjection.V32(off));
    }

    [Fact]
    public void ToyGoldenSeed42Turn200_AndFirstReignTurn40_MovedForTheV33LayoutAlone()
    {
        WorldState toy = SnapshotTests.CanonicalExecutor().Run(SnapshotTests.Genesis(42), 200);
        Assert.Equal(Batch2ToySeed42Turn200, SchemaProjection.V32(toy));
        Assert.NotEqual(Batch2ToySeed42Turn200, WorldHash.ComputeHex(toy));
        WorldState reign = Sim.Tests.Systems.FirstReignTests.Replay(40, out _, Shipped, TestConfigs.PreD054Worldgen());
        Assert.Equal(Batch2FirstReignTurn40, SchemaProjection.V32(reign));
        Assert.NotEqual(Batch2FirstReignTurn40, WorldHash.ComputeHex(reign));
    }

    private static TurnExecutor Production(SimConfig cfg, OrderLog orders, WorldgenConfig? wg = null)
    {
        using var era = Sim.Data.DataFiles.OpenEraPacing();
        using var pipe = Sim.Data.DataFiles.OpenPipeline();
        return new TurnExecutor(EraTableLoader.Load(era), PipelineLoader.Load(pipe, SystemCatalog.All(cfg, wg ?? TestConfigs.Worldgen())), orders);
    }

    private const string Batch2AiTurn300 = "b2e4f333c25aca4acf3cff69fcb0da83e59d18eb90efa8e968dc51a2344597d0";

    /// <summary>
    /// THE AI WORLD (`sim run --founded --seed 42 --turns 300 --ai-empires 1`, the AiFoundingAttributionTests harness) MOVED
    /// FOR THE v33 LAYOUT ALONE. MEASURED on this tree: with incremental construction on and off the two worlds are
    /// byte-identical on every one of the 300 turns, and no queue entry survives any step — the AI orders only when its purse
    /// covers the project's remaining inputs (its documented prudence) and every project it orders is finished in the step
    /// it is delivered, so the incremental rule never acts here (a pinned ABSENCE: it fails the moment a head waits). Its
    /// v32 projection is batch 2's AI hash, byte for byte.
    /// </summary>
    [Fact]
    public void AiWorldSeed42Turn300_MovedForTheV33LayoutAlone_TheIncrementalRuleNeverActsThere()
    {
        // ADR-045 (D-062): the R4 rule — this control attributes an OLDER layer, so it runs on the pre-M6 world (Shipped =
        // TestConfigs.PreM6: CP, discovery and the research-content tranche off).
        SimConfig on = Shipped, off = TestConfigs.PreIncrementalConstruction(on);
        WorldgenConfig wg = TestConfigs.PreD054Worldgen() with { AiEmpires = 1 };   // the cylinder off (the pair rule)
        var onLog = new OrderLog();
        var offLog = new OrderLog();
        TurnExecutor exOn = Production(on, onLog, TestConfigs.PreD054Worldgen()), exOff = Production(off, offLog, TestConfigs.PreD054Worldgen());
        WorldState a = WorldFounding.Found(wg, on, 42), b = WorldFounding.Found(wg, off, 42);
        int constructionOrders = 0;
        for (int t = 1; t <= 300; t++)
        {
            AiOrders.Append(onLog, a, on, TestConfigs.PreD054Worldgen());
            AiOrders.Append(offLog, b, off, TestConfigs.PreD054Worldgen());
            a = exOn.Step(a);
            b = exOff.Step(b);
            Assert.Equal(0, a.ConstructionQueue.Count);                         // nothing ever waits a step
            Assert.Equal(0, a.ConstructionProgress.Count);
            Assert.Equal(WorldHash.ComputeHex(b), WorldHash.ComputeHex(a));
        }
        Assert.Equal(offLog.Count, onLog.Count);
        for (int i = 0; i < onLog.Count; i++)
        {
            Assert.Equal(offLog[i], onLog[i]);
            if (onLog[i].Kind == OrderKind.EnqueueConstruction) constructionOrders++;
        }
        Assert.True(constructionOrders > 0, "the AI ordered no construction - the absence is vacuous");
        Assert.Equal(Batch2AiTurn300, SchemaProjection.V32(a));
        Assert.NotEqual(Batch2AiTurn300, WorldHash.ComputeHex(a));
    }

    /// <summary>
    /// THE PER-LAYER PROOF (turn-exact), on a world where the rule acts: the dev founded world (seed 42, 256 px, four
    /// settlements, one Empire) with ONE player order — a granary at the capital, stamped turn 0 — against the same world
    /// with ONLY incremental construction off. They are byte-identical until the first step whose ON world holds a progress
    /// row, and differ from that very step on: the order's delivery step, in which the ON head is part-built (the empire holds
    /// no stone at founding, and the step's quarrying is short of the granary's 20) while the switch-off head built nothing
    /// (whole or not at all). Both finish the granary in the end, the incremental one no later. (The canonical 1024 px
    /// world is no rig for this: MEASURED, its empire covers a granary within the step an order is delivered, so the rule
    /// never acts there either.)
    /// </summary>
    [Fact]
    public void PerLayer_IncrementalConstruction_APlayersGranaryDivergesExactlyAtTheFirstStepItIsPartBuilt()
    {
        SimConfig on = Shipped, off = TestConfigs.PreIncrementalConstruction(on);
        WorldState w0 = WorldFounding.Found(TestConfigs.DevWorldgen(), on, 42);
        var player = new PolityId(1);
        Assert.True(EmpireQuery.TryGetCapital(w0, player, out SettlementId capital));
        var stone = new GoodId(on.Goods!.IdOf("stone"));
        Assert.True(EmpireStockpile.Available(w0.GoodStocks, ConstructionQuery.Holders(w0, on, capital), stone) < 20,
            "rig: the empire must be short of the granary's stone at founding");
        var orders = new OrderLog();
        orders.Append(new OrderRecord(0, player.Value, OrderKind.EnqueueConstruction, capital.Value, 1.0));
        OrderValidation.ValidateAgainstWorld(orders, w0);
        TurnExecutor exOn = Production(on, orders, TestConfigs.DevWorldgen()), exOff = Production(off, orders, TestConfigs.DevWorldgen());
        WorldState a = w0, b = w0.Clone();
        long firstRow = -1, firstDifferent = -1, builtOn = -1, builtOff = -1;
        for (int t = 1; t <= 60 && (builtOn < 0 || builtOff < 0); t++)
        {
            a = exOn.Step(a);
            b = exOff.Step(b);
            Assert.Equal(0, b.ConstructionProgress.Count);
            if (firstRow < 0 && a.ConstructionProgress.Count > 0)
            {
                firstRow = a.Clock.Turn;
                Assert.Equal(1, a.ConstructionProgress.Count);
                ConstructionProgressRow row = a.ConstructionProgress[0];
                Assert.InRange(row.Progress, 1, ConstructionQuery.ProgressUnits - 1);   // part-built in its first step
                Assert.Equal(0, ConstructionQuery.Built(b, capital, 1));                 // whole or not at all: nothing
                Assert.Equal(1, b.ConstructionQueue.Count);                              // ...it waits
            }
            if (firstDifferent < 0 && WorldHash.ComputeHex(a) != WorldHash.ComputeHex(b)) firstDifferent = a.Clock.Turn;
            if (builtOn < 0 && ConstructionQuery.Built(a, capital, 1) > ConstructionQuery.Built(w0, capital, 1)) builtOn = a.Clock.Turn;
            if (builtOff < 0 && ConstructionQuery.Built(b, capital, 1) > ConstructionQuery.Built(w0, capital, 1)) builtOff = b.Clock.Turn;
        }
        Assert.Equal(1, firstRow);                       // the order's delivery step (stamped 0, applied 0 -> 1)
        Assert.Equal(firstRow, firstDifferent);
        Assert.True(builtOn > 0 && builtOff > 0, $"the granary was not finished in both worlds (on {builtOn}, off {builtOff})");
        Assert.True(builtOn <= builtOff, $"incremental finished later ({builtOn}) than whole-or-nothing ({builtOff})");
    }
}
