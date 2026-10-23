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
    /// turn-exact proofs are EmpireConstructionStockpileTests' and GranaryAttributionTests' AI-world controls.
    /// BATCH 3 / D-054 RE-PIN (integration R1, 2026-10-14; VALUE + LAYOUT, MEASURED on this tree by the agent writing this
    /// line): b2e4f333c25aca4a… -> 91836bcf92b14914…. CAUSE: (1) schema v33 appends ConstructionProgress and
    /// ResourceAllocations, both EMPTY at turn 300 (the AI's purse prudence finishes every head in its delivery step and the
    /// AI sets no split); (2) the shipped world is a CYLINDER (worldgen.json topology.wrapX): the terrain regenerates
    /// x-periodic and is folded into every hash, so the AI world differs from turn 0. WS1/WS2 do not act. ATTRIBUTION
    /// (MEASURED, in-test harness): the sim half off on the cylinder (TestConfigs.PreD054Sim) -> the SAME raw 91836bcf…; the
    /// v32 projection of the live world -> <see cref="S1cTurn300AtV32"/> (0ce1d4d9…, the W1 stage's cylinder-only AI hash).
    /// CONTROLS: D054PairAttributionTests ("ai": both halves off + v32 = OLD), WrapAttributionTests.WithTheCylinderOff_TheAiWorldIsTheBatch2AiWorld
    /// (the cylinder off alone + v32 = OLD), IncrementalConstructionAttributionTests.AiWorldSeed42Turn300_MovedForTheV33LayoutAlone_TheIncrementalRuleNeverActsThere
    /// (flat: WS1 on/off byte-identical every turn, v32 = OLD). `sim run --founded --seed 42 --turns 300 --ai-empires 1` on
    /// this tree prints the NEW value.
    /// D-055 RE-PIN (2026-10-16; the Director's batch-4 instruction, item 1; LAYOUT ALONE, MEASURED on this tree by the
    /// agent writing this line): 91836bcf92b14914… -> 25f4799d581d8de7…. CAUSE: schema v34 appends ConstructionHolds
    /// (section 61), EMPTY at turn 300 and at every step before it (asserted: no step of this world ends with a hold row —
    /// the AI finishes every head in its delivery step), so the entire movement is one four-byte zero count prefix.
    /// CONTROL: ReservationsLayoutAttributionTests.AiWorldSeed42Turn300_MovedForTheV34LayoutAlone — the v33 projection
    /// (SchemaProjection.V33, asserting 0 hold rows) returns OLD byte for byte, and the PreReservations twin gives the same
    /// world. `sim run --founded --seed 42 --turns 300 --ai-empires 1` on this tree prints the NEW value.</summary>
    // ADR-043 RE-PIN (2026-10-22, D-062 Track CP; VALUE + LAYOUT, MEASURED on this tree by the agent writing this line):
    // 25f4799d581d8de7… -> 5623de7a2e550414…. CAUSE: schema v35 appends CivicsPoints (two Empire accounts) and the two
    // research lanes — the AI orders civics on their own lane, funded by Civics Points. CONTROL:
    // CivicsPointsAttributionTests.AiWorldSeed42Turn300_MovedForCivicsPointsAlone (TestConfigs.PreTrackCp and CP off
    // alone return OLD through SchemaProjection.V34; the stage gate off alone returns NEW).
    // M6 RE-PIN (2026-10-23, ONE re-pin of the whole slice; VALUE + LAYOUT, MEASURED on the merged tree by the agent writing
    // this line): 25f4799d581d8de7… (v34) -> 776d5d0c6418f6bc… (the intermediate pins 5623de7a… CP and 8378f993… CP+EX are
    // superseded). CAUSES (three, the M6 slice): Track CP (above; ADR-043); Track EX (ADR-044 with the coordinator's rulings)
    // — the discovery tables are populated, the concealed ores are unread until surveyed, the AI founds only on KNOWN sites,
    // explores (OrderKind 14) when its founding policy finds none and prospects its settlements for 40 grain (OrderKind
    // 15); Track TREE (ADR-045) — the research-content tranche: the AI's goal closures and costs changed, so from turn 0 it
    // targets other research (grinding_stone, then cereal_cultivation) and reaches the Neolithic far sooner. CONTROLS:
    // TheM6Slice_IsTheWholeMove_TheAiWorldWithTheSliceOffIsTheBatch4World (TestConfigs.PreM6 returns 25f4799d… through
    // SchemaProjection.V34), CivicsPointsAttributionTests.AiWorldSeed42Turn300_MovedForCivicsPointsAlone (CP on the
    // EX-off, tranche-off world) and DiscoveryLayoutAttributionTests.AiWorldSeed42Turn300_….
    internal const string S1cTurn300 = "776d5d0c6418f6bc609095b026ba14ae74978847000b2ca64877586b2e2114bc";

    /// <summary>Batch 3 (D-054, integration R1): THE CYLINDER-ALONE LINK — the live AI world through the v32 projection
    /// (the two v33 tables dropped, asserted EMPTY): the AI hash the W1 stage measured with the cylinder and no v33 layout.</summary>
    internal const string S1cTurn300AtV32 = "0ce1d4d9af1590351636b0aacd18c3400b110d59d04c7aa07bce9a9bbcf3a19d";

    /// <summary>The turn the first AI Found Settlement order is stamped (state 36 is the first that differs).</summary>
    private const long FirstFoundingStamp = 35;

    private static TurnExecutor Production(SimConfig cfg, OrderLog orders, WorldgenConfig? worldgen = null)
    {
        using var eraStream = Sim.Data.DataFiles.OpenEraPacing();
        using var pipeStream = Sim.Data.DataFiles.OpenPipeline();
        return new TurnExecutor(EraTableLoader.Load(eraStream),
            PipelineLoader.Load(pipeStream, SystemCatalog.All(cfg, worldgen ?? TestConfigs.PreD054Worldgen())), orders);
    }

    [Fact]
    public void WithTheSwitchOff_TheAiWorldIsThePreS1cWorldByteForByte_AndOnItDivergesExactlyAtTheFirstFoundingsDelivery()
    {
        SimConfig shipped = TestConfigs.Sim();
        SimConfig on = TestConfigs.PreD053(shipped);                      // S1c ON, every newer (D-053) layer off
        SimConfig off = TestConfigs.PreAiFounding(shipped);               // S1c OFF, the batch off with it (R4)
        Assert.True(on.Colonization!.AiFounding!.Enabled);                // the S1c switch is ON in the ON arm
        Assert.False(off.Colonization!.AiFounding!.Enabled);
        WorldgenConfig wg = TestConfigs.PreD054Worldgen() with { AiEmpires = 1 };

        var offLog = new OrderLog();
        var onLog = new OrderLog();
        TurnExecutor offExec = Production(off, offLog), onExec = Production(on, onLog);
        WorldState wOff = WorldFounding.Found(wg, off, 42), wOn = WorldFounding.Found(wg, on, 42);
        long firstDifferent = -1;
        for (int t = 1; t <= Turns; t++)
        {
            AiOrders.Append(offLog, wOff, off, TestConfigs.PreD054Worldgen());
            AiOrders.Append(onLog, wOn, on, TestConfigs.PreD054Worldgen());
            wOff = offExec.Step(wOff);
            wOn = onExec.Step(wOn);
            if (firstDifferent < 0 && WorldHash.ComputeHex(wOff) != WorldHash.ComputeHex(wOn)) firstDifferent = wOn.Clock.Turn;
        }

        // OFF: no Found Settlement order, and the pre-S1c world returns byte for byte.
        for (int i = 0; i < offLog.Count; i++) Assert.NotEqual(OrderKind.FoundSettlement, offLog[i].Kind);
        // D-054 (schema v33): the older literals compare through the v32 projection (the two empty v33 tables dropped).
        Assert.Equal(PreS1cTurn300, SchemaProjection.V32(wOff));

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
        Assert.Equal(S1cPreD053Turn300, SchemaProjection.V32(wOn));

        // THE LIVE ARM: the shipped world, every layer on (the re-pinned S1cTurn300). D-054 (integration R1): MOVED by the v33
        // layout AND the cylinder — the incremental rule never acts on this world
        // (IncrementalConstructionAttributionTests.AiWorldSeed42Turn300_MovedForTheV33LayoutAlone_…); the record is on S1cTurn300.
        var liveLog = new OrderLog();
        TurnExecutor liveExec = Production(shipped, liveLog, TestConfigs.Worldgen());
        // Batch 3 (D-054): the live arm founds the SHIPPED world (worldgen.json as shipped — a cylinder); the arms above are
        // attribution controls for older layers and found the flat, pre-batch-3 world (PreD054Worldgen, the R4 rule).
        WorldState live = WorldFounding.Found(TestConfigs.Worldgen() with { AiEmpires = 1 }, shipped, 42);
        for (int t = 1; t <= Turns; t++)
        {
            AiOrders.Append(liveLog, live, shipped, TestConfigs.Worldgen());
            live = liveExec.Step(live);
        }
        Assert.True(live.Terrain!.WrapX);
        Assert.Equal(S1cTurn300, WorldHash.ComputeHex(live));
        // ADR-043 (2026-10-22) CAUSE BLOCK: the live world now carries Civics Points accounts and the AI's civics lane
        // (behaviour, not layout), so the batch-3 cylinder-alone link is asserted on the pre-CP live world instead:
        // ReservationsLayoutAttributionTests.AiWorldSeed42Turn300_MovedForTheV34LayoutAlone (SchemaProjection.V32 of the
        // TestConfigs.PreTrackCp run = S1cTurn300AtV32).
        Assert.NotEqual(S1cPreD053Turn300, S1cTurn300);
    }

    /// <summary>M6 ATTRIBUTION CONTROL (D-062; ADR-043, ADR-044, ADR-045) — THE WHOLE-SLICE SWITCH-OFF TWIN: the live AI world
    /// (the shipped cylinder, `sim run --founded --seed 42 --turns 300 --ai-empires 1`) stepped with the whole M6 slice off
    /// (TestConfigs.PreM6: the pre-M6 research.json — Fixtures/research-pre-adr045.json, byte-identical to the file at
    /// 06e0153, before Track CP and the tranche — and the discovery layer off) writes nothing in sections 62–66 and returns
    /// the batch-4 (v34) pin byte for byte through SchemaProjection.V34, and the cylinder-alone link through the v32
    /// projection; with every layer on it is the re-pinned value. So the slice is the whole move.</summary>
    [Fact]
    public void TheM6Slice_IsTheWholeMove_TheAiWorldWithTheSliceOffIsTheBatch4World()
    {
        SimConfig pre = TestConfigs.PreM6Sim();
        Assert.NotSame(TestConfigs.Research(), pre.Research);
        Assert.Equal(426, pre.Research!.TechnologyCount);
        Assert.Equal(7, pre.Research.CivicsCount);
        var log = new OrderLog();
        TurnExecutor exec = Production(pre, log, TestConfigs.Worldgen());
        WorldState w = WorldFounding.Found(TestConfigs.Worldgen() with { AiEmpires = 1 }, pre, 42);
        for (int t = 1; t <= Turns; t++)
        {
            AiOrders.Append(log, w, pre, TestConfigs.Worldgen());
            w = exec.Step(w);
        }
        Assert.Equal(DiscoveryLayoutAttributionTests.PreM6AiTurn300, WorldHash.ComputeHex(w));
        Assert.Equal(CivicsPointsAttributionTests.V34AiTurn300, SchemaProjection.V34(w));
        Assert.Equal(S1cTurn300AtV32, SchemaProjection.V32(w));
        Assert.NotEqual(CivicsPointsAttributionTests.V34AiTurn300, S1cTurn300);
    }
}
