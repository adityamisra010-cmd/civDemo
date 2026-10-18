using Sim.Core;
using Sim.Core.Kernel;
using Sim.Core.State;
using Sim.Core.Systems;
using Sim.Core.Worldgen;
using Sim.Tests.TestUtil;

namespace Sim.Tests.Kernel;

/// <summary>
/// M5 S1c (ADR-036 §8) — THE ATTRIBUTION CONTROL FOR EVERY AI PIN THE AI FOUNDING POLICY MOVES (the
/// IntegratedPinAttributionTests discipline: a moved pin is attributed by MEASUREMENT, not by assertion).
///
/// The policy is the packet's only behaviour change, and it acts only through the orders it adds to the log. With
/// sim.json <c>colonization.aiFounding.enabled = false</c> it issues nothing — so the AI world must return, BYTE FOR BYTE,
/// the hash the pre-S1c tree produced. If it does, the switch is the entire delta: no other change of this packet (the
/// producer's new worldgen parameter, the lazy site state, the config section) reaches the simulation. With the switch
/// on, the world must be the same world up to the first founding's delivery and diverge exactly there — stamped turn t,
/// first different state t+1 — so every moved AI pin downstream is attributable to the AI's colonies and to nothing else.
///
/// THE WORLD: the canonical founded seed-42 world (1024², twelve settlements) with worldgen aiEmpires overridden to 1,
/// the production pipeline, and AiOrders appended before every step — exactly `sim run --founded --seed 42 --turns 300
/// --ai-empires 1` (the AiEmpireIntegrationTests harness, documented there as CLI-identical).
///
/// D-053 (2026-10-13; the integration of its three layers, 2026-10-14). The batch is NEWER than S1c and moves the AI world,
/// so by the R4 rule the control's OFF twin (TestConfigs.PreAiFounding) strips the whole batch too, and the S1c
/// turn-exact proof runs on the PRE-BATCH pair: the AI founding policy ON with the batch off (TestConfigs.PreD053) against
/// it OFF with the batch off — exactly the S1c-era worlds, whose hashes are the historical literals below. The live
/// (shipped) world is the third arm, pinned as S1cTurn300 with its re-pin record.
/// </summary>
[Trait("suite", "determinism")]
public class AiFoundingAttributionTests
{
    private const int Turns = 300;

    /// <summary>PRE-S1C PIN — the turn-300 hash of the AI world on the base commit 3d8aacc (S1b head), MEASURED by
    /// `sim run --founded --seed 42 --turns 300 --ai-empires 1` built from that tree.</summary>
    private const string PreS1cTurn300 = "a416ce4b2386962c4fcd640fc1bf7f56119f529626572859480e8980b4ce6867";

    /// <summary>The same run with the AI founding policy on and the D-053 batch off (TestConfigs.PreD053) — the S1c value,
    /// which is also the base commit de86ace's (MEASURED by `sim run --founded --seed 42 --turns 300 --ai-empires 1` there).
    /// Cause (vs PreS1cTurn300): 7 AI Found Settlement orders by turn 300 (stamped 35, 71, 107, 143, 179, 215, 286).</summary>
    private const string S1cPreD053Turn300 = "aa7a87a98370c24815d478877a3f4f862838fde51a33fd5d0841157ca73066a7";

    /// <summary>THE LIVE PIN: the shipped world (every layer on), = `sim run --founded --seed 42 --turns 300 --ai-empires 1`
    /// on this tree. RE-PIN (integration of the D-053 batch, 2026-10-14; VALUE): aa7a87a98370c248… -> b2e4f333c25aca4a….
    /// CAUSE: the combined batch — the construction purse (§1: the AI's first granary is ordered at turn 4 from its Empire's
    /// purse instead of turn 9 from one settlement's own stock, worlds first differ at turn 5) and the granary effects (§3:
    /// a granary first acts at turn 221, worlds first differ at 222); intra-polity relief (§2) never moves grain on this
    /// world. ATTRIBUTION (MEASURED on this tree, in-test harness): the batch off (PreD053) returns the OLD value
    /// aa7a87a98370c248… byte for byte (S1cPreD053Turn300, asserted below); purse off alone fd40bbafc5d9de65…; relief off
    /// alone b2e4f333… (= the live value: relief does not move it); granary effects off alone f9e69db77f396d00…. The per-layer
    /// turn-exact proofs are EmpireConstructionStockpileTests' and GranaryAttributionTests' AI-world controls.</summary>
    private const string S1cTurn300 = "b2e4f333c25aca4acf3cff69fcb0da83e59d18eb90efa8e968dc51a2344597d0";

    /// <summary>The turn the first AI Found Settlement order is stamped (state 36 is the first that differs).</summary>
    private const long FirstFoundingStamp = 35;

    private static TurnExecutor Production(SimConfig cfg, OrderLog orders)
    {
        using var eraStream = Sim.Data.DataFiles.OpenEraPacing();
        using var pipeStream = Sim.Data.DataFiles.OpenPipeline();
        return new TurnExecutor(EraTableLoader.Load(eraStream),
            PipelineLoader.Load(pipeStream, SystemCatalog.All(cfg, TestConfigs.Worldgen())), orders);
    }

    [Fact]
    public void WithTheSwitchOff_TheAiWorldIsThePreS1cWorldByteForByte_AndOnItDivergesExactlyAtTheFirstFoundingsDelivery()
    {
        SimConfig shipped = TestConfigs.Sim();
        SimConfig on = TestConfigs.PreD053(shipped);                      // S1c ON, every newer (D-053) layer off
        SimConfig off = TestConfigs.PreAiFounding(shipped);               // S1c OFF, the batch off with it (R4)
        Assert.True(on.Colonization!.AiFounding!.Enabled);                // the S1c switch is ON in the ON arm
        Assert.False(off.Colonization!.AiFounding!.Enabled);
        WorldgenConfig wg = TestConfigs.Worldgen() with { AiEmpires = 1 };

        var offLog = new OrderLog();
        var onLog = new OrderLog();
        TurnExecutor offExec = Production(off, offLog), onExec = Production(on, onLog);
        WorldState wOff = WorldFounding.Found(wg, off, 42), wOn = WorldFounding.Found(wg, on, 42);
        long firstDifferent = -1;
        for (int t = 1; t <= Turns; t++)
        {
            AiOrders.Append(offLog, wOff, off, TestConfigs.Worldgen());
            AiOrders.Append(onLog, wOn, on, TestConfigs.Worldgen());
            wOff = offExec.Step(wOff);
            wOn = onExec.Step(wOn);
            if (firstDifferent < 0 && WorldHash.ComputeHex(wOff) != WorldHash.ComputeHex(wOn)) firstDifferent = wOn.Clock.Turn;
        }

        // OFF: no Found Settlement order, and the pre-S1c world returns byte for byte.
        for (int i = 0; i < offLog.Count; i++) Assert.NotEqual(OrderKind.FoundSettlement, offLog[i].Kind);
        Assert.Equal(PreS1cTurn300, WorldHash.ComputeHex(wOff));

        // ON: identical up to the first AI founding order's delivery, and different from the state it founds in.
        long firstStamp = -1;
        for (int i = 0; i < onLog.Count && firstStamp < 0; i++)
            if (onLog[i].Kind == OrderKind.FoundSettlement) firstStamp = onLog[i].Turn;
        Assert.Equal(FirstFoundingStamp, firstStamp);
        Assert.Equal(firstStamp + 1, firstDifferent);
        // Every order before the first founding is the same order in both logs (the policy added, it did not alter).
        int k = 0;
        for (int i = 0; i < onLog.Count && onLog[i].Turn <= firstStamp; i++)
        {
            if (onLog[i].Kind == OrderKind.FoundSettlement) continue;
            Assert.Equal(offLog[k++], onLog[i]);
        }
        Assert.True(k == offLog.Count || offLog[k].Turn > firstStamp, "the OFF log has an order the ON log lacks");
        Assert.Equal(S1cPreD053Turn300, WorldHash.ComputeHex(wOn));

        // THE LIVE ARM: the shipped world, every layer on (the re-pinned S1cTurn300).
        var liveLog = new OrderLog();
        TurnExecutor liveExec = Production(shipped, liveLog);
        WorldState live = WorldFounding.Found(wg, shipped, 42);
        for (int t = 1; t <= Turns; t++)
        {
            AiOrders.Append(liveLog, live, shipped, TestConfigs.Worldgen());
            live = liveExec.Step(live);
        }
        Assert.Equal(S1cTurn300, WorldHash.ComputeHex(live));
        Assert.NotEqual(S1cPreD053Turn300, S1cTurn300);
    }
}
