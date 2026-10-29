using Sim.Core.Kernel;
using Sim.Core.State;
using Sim.Core.Systems;
using Sim.Tests.TestUtil;

namespace Sim.Tests.Kernel;

/// <summary>
/// ADR-058 (D-066 A7, track KNOW) — ATTRIBUTION OF THE KNOW LAYER: contact-carried foreign exposure (DiffusionSystem,
/// research.json tuning.foreignExposure) and the seven Eurekas made evaluable over deposits and site terrain.
///
/// MEASURED on the KNOW tree (base 0bf7626 + m6-base.patch + this track) by the agent writing this class, 2026-10-28:
/// <list type="bullet">
/// <item>the founded golden, the driven golden and the first reign are UNMOVED (their pin tests pass unchanged: no foreign
///   trade with a known partner and no newly evaluable Eureka acts in those worlds by their last turn);</item>
/// <item>the AI world (`sim run --founded --seed 42 --turns 300 --ai-empires 1`) MOVES: ac4554bf… -> a519b5c9… (economy
///   projection 3153b7e3… -> 5edcc3e0…). CAUSE: three newly evaluable Eurekas fire; the exposure writer writes no row by
///   turn 300 (the exposure-off twin is the live world). CONTROL: the switch-off twin (<see cref="TestConfigs.PreKnow"/>)
///   returns the base tree's pins byte for byte — the raw hash and its economy projection — so the layer is the whole
///   move.</item>
/// </list>
/// </summary>
public class KnowAttributionTests
{
    /// <summary>The base tree's AI-world pins (AiFoundingAttributionTests before this track).</summary>
    /// M6 integration: with D-066 A18 (track FOG) on, PreKnow's world is the FOG-only pin (control (a) of the combined pin).
    internal const string PreKnowS1cTurn300 = AiFoundingAttributionTests.FogOnlyTurn300;
    internal const string PreKnowS1cTurn300PreEconomy = AiFoundingAttributionTests.FogOnlyTurn300PreEconomy;


    [Fact]
    public void AiWorldSeed42Turn300_MovedForTheKnowLayerAlone_TheTwinIsTheBaseWorld()
    {
        WorldState twin = CivicsPointsAttributionTests.RunAiWorld(TestConfigs.PreKnow(TestConfigs.Sim()));
        Assert.Equal(0, twin.ResearchExposures.Count);
        Assert.Equal(PreKnowS1cTurn300PreEconomy, EconomyProjection.HashWithout(twin, TestConfigs.Sim()));
        Assert.Equal(PreKnowS1cTurn300, WorldHash.ComputeHex(twin));

        WorldState live = CivicsPointsAttributionTests.RunAiWorld(TestConfigs.Sim());
        Assert.Equal(AiFoundingAttributionTests.S1cTurn300, WorldHash.ComputeHex(live));
        Assert.NotEqual(PreKnowS1cTurn300, AiFoundingAttributionTests.S1cTurn300);

        Assert.Equal(AiFoundingAttributionTests.S1cTurn300PreEconomy, EconomyProjection.HashWithout(live, TestConfigs.Sim()));
        Assert.Equal(0, live.ResearchExposures.Count);   // MEASURED: no foreign trade with a sighted partner by turn 300
        Assert.Equal(3, live.ResearchEurekas.Count - twin.ResearchEurekas.Count);

        // Which half acts: the writer's switch alone off (the seven Eurekas kept) is the live world — the Eurekas are the move.
        WorldState eurekasOnly = CivicsPointsAttributionTests.RunAiWorld(TestConfigs.PreForeignExposure(TestConfigs.Sim()));
        Assert.Equal(AiFoundingAttributionTests.S1cTurn300, WorldHash.ComputeHex(eurekasOnly));
    }

    /// <summary>The layer ships on, and every older twin strips it (the R4 rule): their research content carries neither the
    /// writer's switch nor the seven conditions.</summary>
    [Fact]
    public void TheLayer_ShipsOn_AndEveryOlderTwinStripsIt()
    {
        Assert.True(DiffusionQuery.Enabled(TestConfigs.Sim().Research));
        Assert.False(DiffusionQuery.Enabled(TestConfigs.PreKnow(TestConfigs.Sim()).Research));
        Assert.False(DiffusionQuery.Enabled(TestConfigs.PreForeignExposure(TestConfigs.Sim()).Research));
        foreach (SimConfig older in new[]
                 {
                     TestConfigs.PreMilitary(TestConfigs.Sim()), TestConfigs.PreAiRecruitment(TestConfigs.Sim()),
                     TestConfigs.PreEconomyTrack(TestConfigs.Sim()), TestConfigs.PreEc1(TestConfigs.Sim()),
                     TestConfigs.PreM6(TestConfigs.Sim()), TestConfigs.PreDiscovery(TestConfigs.Sim()),
                     TestConfigs.PreTranche2(TestConfigs.Sim()), TestConfigs.PreTrackCp(TestConfigs.Sim()),
                     TestConfigs.PrePerTurnCivics(TestConfigs.Sim()), TestConfigs.PrePerTurnCivicsOnly(TestConfigs.Sim()),
                     TestConfigs.PrePopulationAverageOnly(TestConfigs.Sim()), TestConfigs.PreKnowledgeEffects(TestConfigs.Sim()),
                     TestConfigs.PreAiRoutes(TestConfigs.Sim()), TestConfigs.PreAiSupporting(TestConfigs.Sim()),
                     TestConfigs.PreTreeTranche(TestConfigs.Sim()), TestConfigs.PreReservations(TestConfigs.Sim()),
                 })
        {
            Assert.False(DiffusionQuery.Enabled(older.Research));
            if (older.Research is { } r && r.IndexOfId("fishing_hook") >= 0)
                Assert.False(r.Nodes[r.IndexOfId("fishing_hook")].Eurekas[0].EvaluableNow);
        }
    }
}
