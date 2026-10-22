using Sim.Core;
using Sim.Core.Kernel;
using Sim.Core.State;
using Sim.Core.Systems;
using Sim.Core.Worldgen;
using Sim.Tests.TestUtil;

namespace Sim.Tests.Kernel;

/// <summary>
/// M5 item 5 (granaries) — THE ATTRIBUTION CONTROL (the AiFoundingAttributionTests / IntegratedPinAttributionTests
/// discipline: a moved pin is attributed by MEASUREMENT). The granary effects are one layer behind one switch, sim.json
/// <c>granaries.enabled</c>; <see cref="TestConfigs.PreGranaryEffects"/> turns it off. They ship in ONE batch with two other
/// layers (D-053: the construction purse §1, intra-polity relief §2), so on the combined tree the control has two arms:
/// with the WHOLE batch off (<see cref="TestConfigs.PreD053"/>) every world is the base commit's (de86ace) world BYTE FOR
/// BYTE (the de86ace literals below); and the PER-LAYER proof — the combined world against the combined world with ONLY the
/// granary effects off — first differs exactly one step after a granary first acts. Switched on:
/// <list type="bullet">
/// <item>the founded seed-42 300-turn golden does not move at all — no order is issued, nothing is built, and with no
///   granary both factors are skipped;</item>
/// <item>the AI world (aiEmpires 1, the AI builds a granary in each of its settlements) is the granary-off world up to
///   the first turn a granary can ACT — one standing at a settlement that CULTIVATES (the farm factor never touches
///   wild-food gathering, C1), or in a civilization whose settlement has a positive dead-zone deficit (the famine
///   factor) — and diverges exactly one step later, the first step that reads such a granary from PREV. Granaries that
///   stand earlier at wild-food settlements change nothing. So every moved AI pin downstream is attributable to the
///   granary effects and to nothing else.</item>
/// </list>
/// THE WORLDS: the canonical founded seed-42 world (1024², twelve settlements), the production pipeline; the AI world
/// is the same with worldgen aiEmpires 1 and AiOrders appended before every step — exactly `sim run --founded --seed 42
/// --turns 300 [--ai-empires 1]` (the AiEmpireIntegrationTests harness, documented there as CLI-identical).
/// </summary>
[Trait("suite", "determinism")]
public class GranaryAttributionTests
{
    private const int Turns = 300;

    /// <summary>The founded golden at the base commit de86ace (SnapshotTests.PreD054FoundedGoldenHash) — and, since the
    /// no-order world builds nothing, ALSO the value with the granary effects on.</summary>
    private const string De86aceFounded = "937577ffb0e73c2b051d8d7832cfb5028c2154d01c9afef4d976b71ccb5e7f54";

    /// <summary>The AI world's turn-300 hash at the base commit de86ace, MEASURED by `sim run --founded --seed 42 --turns
    /// 300 --ai-empires 1` built from that tree (equal to AiFoundingAttributionTests.S1cTurn300 there).</summary>
    private const string De86aceAiTurn300 = "aa7a87a98370c24815d478877a3f4f862838fde51a33fd5d0841157ca73066a7";

    /// <summary>The same AI run on the COMBINED tree — the shipped batch, every layer on (= AiFoundingAttributionTests.S1cTurn300
    /// and the CLI's value). RE-PIN (integration, 2026-10-14; VALUE): 453ae2d413d7b6ed… (the granary track alone, on de86ace)
    /// -> b2e4f333c25aca4a…. CAUSE: the combined batch — the construction purse (§1) moves the AI's first granary order from
    /// turn 9 to turn 4 (it pays from its Empire's purse), and the granary effects (§3) act from turn 221. ATTRIBUTION
    /// (MEASURED on this tree, 300-turn AI world): batch off (PreD053) = aa7a87a98370c248… = de86ace; purse off alone =
    /// fd40bbafc5d9de65…; relief off alone = b2e4f333… (relief never moves grain on this world — no negative grain residual
    /// in 300 turns); granary effects off alone = f9e69db77f396d00…. So the purse and the granary effects move it, relief
    /// does not.</summary>
    private const string GranaryAiTurn300 = "b2e4f333c25aca4acf3cff69fcb0da83e59d18eb90efa8e968dc51a2344597d0";

    private static TurnExecutor Production(SimConfig cfg, OrderLog? orders = null)
    {
        using var eraStream = Sim.Data.DataFiles.OpenEraPacing();
        using var pipeStream = Sim.Data.DataFiles.OpenPipeline();
        return new TurnExecutor(EraTableLoader.Load(eraStream),
            PipelineLoader.Load(pipeStream, SystemCatalog.All(cfg, TestConfigs.PreD054Worldgen())), orders);
    }

    /// <summary>Whether a granary can ACT on the next step read from <paramref name="w"/>: the farm factor at a settlement
    /// with a standing granary that does not gather wild food, or the famine factor at a settlement whose dead-zone
    /// deficit is positive in a civilization holding a granary.</summary>
    private static bool AGranaryActs(IReadOnlyWorldState w, SimConfig cfg)
    {
        for (int i = 0; i < w.Settlements.Count; i++)
        {
            SettlementId s = w.Settlements[i].Id;
            if (GranaryEffects.LocalGranaries(w, cfg, s) > 0 && !LabourActivities.HarvestsWildFood(w, cfg, s)) return true;
            double d = FoodState.DeficitRatio(w, s);
            if (GranaryEffects.EmpireGranaries(w, cfg, s) > 0 && FoodState.EffectiveDeficit(d, FoodState.Of(w, s, cfg, out _), cfg) > 0.0)
                return true;
        }
        return false;
    }

    private static long Granaries(IReadOnlyWorldState w, SimConfig cfg)
    {
        int project = GranaryEffects.ProjectId(cfg);
        long n = 0;
        for (int i = 0; i < w.Structures.Count; i++) if (w.Structures[i].ProjectId == project) n += w.Structures[i].Count;
        return n;
    }

    [Fact]
    public void TheFoundedGolden_IsUnmovedByTheGranaryEffects_OnOrOff()
    {
        // ADR-043 (2026-10-22) CAUSE BLOCK: was TestConfigs.Sim(). The literals here are de86ace / batch-2 values, older than
        // Track CP, so by the R4 rule every arm runs on the pre-CP world (TestConfigs.PreTrackCp); the granary switch is the
        // shipped one either way.
        SimConfig on = TestConfigs.PreTrackCp(TestConfigs.Sim());
        Assert.True(on.Granaries!.Enabled);                                   // the shipped content is the ON arm
        // D-054 (batch 3) — the PreD054 PAIR: every leg founds FLAT (TestConfigs.PreD054Worldgen, the worldgen half) and
        // every de86ace / batch-2 literal compares through the v32 projection (SchemaProjection.V32 — the two appended v33
        // tables dropped, asserted empty). The worlds built with WS1 off where the literal is a batch-2 value (the older
        // twins — PreD053 and every twin older than it — strip the sim half, PreD054Sim, already).
        WorldState wBatchOff = SnapshotTests.RunFoundedGolden(TestConfigs.PreD053(on), TestConfigs.PreD054Worldgen());
        Assert.Equal(De86aceFounded, SchemaProjection.V32(wBatchOff));       // the batch off: de86ace
        WorldState wOff = SnapshotTests.RunFoundedGolden(TestConfigs.PreGranaryEffects(on), TestConfigs.PreD054Worldgen());
        Assert.Equal(De86aceFounded, SchemaProjection.V32(wOff));
        WorldState wOn = SnapshotTests.RunFoundedGolden(on, TestConfigs.PreD054Worldgen());
        Assert.Equal(0, Granaries(wOn, on));                                  // nothing is built without an order
        Assert.Equal(De86aceFounded, SchemaProjection.V32(wOn));             // no order: WS1 never acts here either
        Assert.Equal(SnapshotTests.PreD054FoundedGoldenHash, SchemaProjection.V32(wOn));
    }

    [Fact]
    public void WithTheBatchOff_TheAiWorldIsTheDe86aceWorldByteForByte_AndTheGranaryLayerAloneDivergesTheStepAfterAGranaryFirstActs()
    {
        // D-054: the per-layer proof keeps its batch-2 meaning — both arms with WS1 off (TestConfigs.PreD054Sim).
        SimConfig on = TestConfigs.PreD054Sim(TestConfigs.Sim());
        SimConfig off = TestConfigs.PreGranaryEffects(on);
        Assert.False(off.Granaries!.Enabled);
        WorldgenConfig wg = TestConfigs.PreD054Worldgen() with { AiEmpires = 1 };

        // THE BATCH OFF: the base commit's AI world, byte for byte.
        SimConfig batchOff = TestConfigs.PreD053(on);
        var batchLog = new OrderLog();
        TurnExecutor batchExec = Production(batchOff, batchLog);
        WorldState wBatch = WorldFounding.Found(wg, batchOff, 42);
        for (int t = 1; t <= Turns; t++)
        {
            AiOrders.Append(batchLog, wBatch, batchOff, TestConfigs.PreD054Worldgen());
            wBatch = batchExec.Step(wBatch);
        }
        Assert.Equal(De86aceAiTurn300, SchemaProjection.V32(wBatch));

        // THE PER-LAYER PROOF: the combined world (ON) against the combined world with only the granary effects off.

        var offLog = new OrderLog();
        var onLog = new OrderLog();
        TurnExecutor offExec = Production(off, offLog), onExec = Production(on, onLog);
        WorldState wOff = WorldFounding.Found(wg, off, 42), wOn = WorldFounding.Found(wg, on, 42);
        long firstDifferent = -1, firstGranaryStands = -1, firstGranaryActs = -1;
        for (int t = 1; t <= Turns; t++)
        {
            AiOrders.Append(offLog, wOff, off, TestConfigs.PreD054Worldgen());
            AiOrders.Append(onLog, wOn, on, TestConfigs.PreD054Worldgen());
            wOff = offExec.Step(wOff);
            wOn = onExec.Step(wOn);
            if (firstGranaryStands < 0 && Granaries(wOn, on) > 0) firstGranaryStands = wOn.Clock.Turn;
            if (firstGranaryActs < 0 && AGranaryActs(wOn, on)) firstGranaryActs = wOn.Clock.Turn;
            if (firstDifferent < 0 && WorldHash.ComputeHex(wOff) != WorldHash.ComputeHex(wOn)) firstDifferent = wOn.Clock.Turn;
        }

        // ON: identical until a granary can act, different from the next step on (the first step whose PREV holds one).
        // The AI's first granaries stand at settlements that still gather wild food: they change nothing (C1).
        Assert.True(firstGranaryStands > 0, "the AI must build a granary in 300 turns");
        Assert.True(firstGranaryActs > firstGranaryStands,
            $"the first granaries stand at wild-food settlements and act later (stands {firstGranaryStands}, acts {firstGranaryActs})");
        Assert.Equal(firstGranaryActs + 1, firstDifferent);
        // The orders up to that step are the same orders (the layer adds no order and alters none before it acts).
        for (int i = 0; i < onLog.Count && onLog[i].Turn <= firstGranaryActs; i++) Assert.Equal(offLog[i], onLog[i]);
        Assert.Equal(GranaryAiTurn300, SchemaProjection.V32(wOn));          // the batch-2 AI world: WS1 off reproduces it
        Assert.NotEqual(De86aceAiTurn300, GranaryAiTurn300);
    }
}
