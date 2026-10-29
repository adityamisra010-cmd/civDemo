using Sim.Core.Kernel;
using Sim.Core.State;
using Sim.Core.Systems;
using Sim.Tests.Kernel;
using Sim.Tests.Systems;
using Sim.Tests.TestUtil;

namespace Sim.Tests.Military;

/// <summary>
/// ADR-055 (schema v39) — THE ATTRIBUTION OF THE GOLDEN RE-PINS. Schema v39 appends four tables (sections 70–73:
/// FormationPersonnel, Reequips, ReequipEscrow, ReequipLog) after ResearchPopulations (v38, ADR-054), and the military layer
/// (sim.json military, on as shipped) gives the founding warband its people (20 adults moved from the capital by
/// Ledger.Transfer), feeds them (grain sunk per person-year × dtYears) and lets the AI recruit. So:
/// <list type="bullet">
/// <item>every FOUNDED golden (founded, driven, first reign, the AI world) moves by VALUE: the SWITCH-OFF TWIN
///   (TestConfigs.PreMilitary — the warband the ADR-031 token, nothing eaten, every military order refused; the research
///   switches as shipped) writes no row in sections 70–73, and its v38 projection (SchemaProjection.V38: those sections
///   stripped, ASSERTED EMPTY, their prefixes dropped — and, independently, WorldHash.ComputeHexAsSchema at 38) is the v38
///   pin (the research track's raw pin) byte for byte: the military layer is the whole move;</item>
/// <item>the toy golden (no unit-family content, no polity) and every world already under an older twin (the R4 rule: every
///   Pre* twin strips the newer military layer — the pre-M6 and pre-tranche-2 worlds) move for the v39 LAYOUT ALONE: four
///   empty count prefixes.</item>
/// </list>
/// The V38* values below are the raw pins before v39 (the research track's, ADR-054). Old → new raw pins:
/// SnapshotTests.ToyGoldenHash, FoundedGoldenHash, DrivenGoldenTests.Golden, FirstReignTests.PostR1Golden,
/// AiFoundingAttributionTests.S1cTurn300 and PreTranche2Turn300, DiscoveryLayoutAttributionTests.PreM6FoundedGolden and
/// PreM6AiTurn300, ci.yml FOUNDED_GOLDEN.
/// </summary>
public class MilitaryAttributionTests
{
    internal const string V38ToyGolden = "175f55bee2f87547a4ea942c1af8c6649c1cb681d3c9addb81f6805149417e53";
    internal const string V38FoundedGolden = "1f9f3d4f8bfa19918306794849d29ff24fc2a875cfd4459b3f6fa3dabf51b957";
    internal const string V38DrivenGolden = "f23e0dcd49b012d0302580eb529ca5169818dc591ff66a5c62d03c29f6ca12f8";
    internal const string V38FirstReignGolden = "389e05266085018bb7f9c69e32b5c89979f7321a7dc2c5c9461b4342b42c5e27";
    internal const string V38AiTurn300 = "02500b4afbddc2effca05efc86759677caff75a7d029784cd0b12eeafa5cf748";
    internal const string V38PreTranche2AiTurn300 = "51b33ea0b49cd92c68e44db10fe4a5f19a0d1aee307fc5cee6f0dce02d95e8cd";
    internal const string V38PreM6FoundedGolden = "0dadbdfa26c8adc8901fb0436097b4bc174a2d9da00f194e1052a6160218c098";
    internal const string V38PreM6AiTurn300 = "5258fe169be0f096c80c2a6abcdbbc86d8eac12de91b539d86e2afdaad04c166";

    // ADR-056 §13 (the M7 merge) — CONTROL (b) of every founded re-pin: the research+economy pins, the raw v38 pins the
    // economy track measured on the research tree before the military layer existed (the packets' inert content rows on top
    // of the research pins above). TestConfigs.PreMilitary keeps the packets on, so its world is the research+economy v38
    // world: SchemaProjection.V38KeepingPackets and WorldHash.ComputeHexAsSchema(·, 38) return these; SchemaProjection.V38
    // (the packet rows stripped and asserted inert) returns the research pins above.
    internal const string V38EconFoundedGolden = "bd8a343b6683e19a59a06a3acb440cfd96f67b9ef4394738d9d4e8d171d16a6d";
    internal const string V38EconDrivenGolden = "d6862db037272aa332342503ed0da419147ec738b9e1cde4dabbdfeb3adb1db5";
    internal const string V38EconFirstReignGolden = "58f017059925b16ed4a57b5a5d7480e503b73ed0a6aa291249824dbe93f54b19";
    internal const string V38EconAiTurn300 = "56e8e8671880ab9594c356d40311a4eb793e619c55c45e6421895d19dab416b9";

    private static SimConfig Off() => TestConfigs.PreMilitary(TestConfigs.Sim());

    /// <summary>The twin wrote nothing in sections 70–73; its v38 form is the v38 pin by two routes; its raw stream is the v38
    /// stream plus sixteen zero bytes. ADR-056 §13: <paramref name="v38Econ"/> is the twin's v38 form WITH the economy packets'
    /// rows (the research+economy pin; a world without packet rows passes null: the two forms coincide).</summary>
    internal static void TwinIsTheV38World(WorldState twin, string v38, string? v38Econ = null)
    {
        v38Econ ??= v38;
        Assert.Equal((0, 0, 0, 0), (twin.FormationPersonnel.Count, twin.Reequips.Count, twin.ReequipEscrow.Count, twin.ReequipLog.Count));
        Assert.Equal(v38, SchemaProjection.V38(twin));
        Assert.Equal(v38Econ, SchemaProjection.V38KeepingPackets(twin));
        Assert.Equal(v38Econ, WorldHash.ComputeHexAsSchema(twin, 38));
        Assert.Equal(4 * SchemaProjection.V39TableCount, CanonicalSchema.BytesAppendedSince(twin, 38));
        Assert.Equal(new byte[4 * SchemaProjection.V39TableCount], SchemaProjection.Serialize(twin)[^(4 * SchemaProjection.V39TableCount)..]);
    }

    /// <summary>A founded golden: the live world is the re-pinned value and carries personnel; the switch-off twin is the v38
    /// world; the live world's own v38 form is refused (the personnel rows are behaviour, never layout).</summary>
    private static void MovedForTheMilitaryLayerAlone(Func<SimConfig, WorldState> run, string v38, string v38Econ, string raw)
    {
        WorldState live = run(TestConfigs.Sim());
        Assert.True(live.FormationPersonnel.Count > 0, "the live world's founding warband holds no one");
        Assert.Null(RecruitmentQuery.InvariantViolation(live));
        Assert.Equal(raw, WorldHash.ComputeHex(live));
        Assert.ThrowsAny<Exception>(() => SchemaProjection.V38(live));
        TwinIsTheV38World(run(Off()), v38, v38Econ);
        Assert.NotEqual(v38, raw);
        Assert.NotEqual(v38Econ, raw);
    }

    [Fact]
    public void FoundedGoldenSeed42Turn300_MovedForTheMilitaryLayerAlone() =>
        MovedForTheMilitaryLayerAlone(cfg => SnapshotTests.RunFoundedGolden(cfg), V38FoundedGolden, V38EconFoundedGolden, SnapshotTests.FoundedGoldenHash);

    [Fact]
    public void DrivenGoldenSeed42Turn300_MovedForTheMilitaryLayerAlone() =>
        MovedForTheMilitaryLayerAlone(cfg => DrivenGoldenTests.RunDriven(300, cfg).World, V38DrivenGolden, V38EconDrivenGolden, DrivenGoldenTests.Golden);

    [Fact]
    public void FirstReignTurn40_MovedForTheMilitaryLayerAlone() =>
        MovedForTheMilitaryLayerAlone(cfg => FirstReignTests.Replay(40, out _, cfg), V38FirstReignGolden, V38EconFirstReignGolden, FirstReignTests.PostR1Golden);

    /// <summary>The AI world (`sim run --founded --seed 42 --turns 300 --ai-empires 1`): the military layer (personnel,
    /// upkeep and the AI's recruitment) is the whole move.</summary>
    [Fact]
    public void AiWorldSeed42Turn300_MovedForTheMilitaryLayerAlone() =>
        MovedForTheMilitaryLayerAlone(CivicsPointsAttributionTests.RunAiWorld, V38AiTurn300, V38EconAiTurn300, AiFoundingAttributionTests.S1cTurn300);

    [Fact]
    public void ToyGoldenSeed42Turn200_MovedForTheV39LayoutAlone()
    {
        WorldState toy = SnapshotTests.CanonicalExecutor().Run(SnapshotTests.Genesis(42), 200);
        TwinIsTheV38World(toy, V38ToyGolden);   // the toy world is its own twin: no unit-family content, no formation
        Assert.Equal(SnapshotTests.ToyGoldenHash, WorldHash.ComputeHex(toy));
        Assert.NotEqual(V38ToyGolden, SnapshotTests.ToyGoldenHash);
    }

    /// <summary>The worlds already under an older twin (the R4 rule strips the military layer with it) move for the v39
    /// layout alone: their raw pins are their v38 pins plus four empty prefixes.</summary>
    [Fact]
    public void ThePreM6AndPreTranche2Worlds_MovedForTheV39LayoutAlone()
    {
        WorldState founded = SnapshotTests.RunFoundedGolden(TestConfigs.PreM6(TestConfigs.Sim()));
        TwinIsTheV38World(founded, V38PreM6FoundedGolden);
        Assert.Equal(DiscoveryLayoutAttributionTests.PreM6FoundedGolden, WorldHash.ComputeHex(founded));
        WorldState pre2 = CivicsPointsAttributionTests.RunAiWorld(TestConfigs.PreTranche2(TestConfigs.Sim()));
        TwinIsTheV38World(pre2, V38PreTranche2AiTurn300);
        Assert.Equal(AiFoundingAttributionTests.PreTranche2Turn300, WorldHash.ComputeHex(pre2));
        WorldState pm6ai = CivicsPointsAttributionTests.RunAiWorld(TestConfigs.PreM6Sim());
        TwinIsTheV38World(pm6ai, V38PreM6AiTurn300);
        Assert.Equal(DiscoveryLayoutAttributionTests.PreM6AiTurn300, WorldHash.ComputeHex(pm6ai));
    }

    [Fact]
    public void TheSwitch_ShipsOn_AndEveryOlderTwinStripsIt_TheResearchSwitchesKept()
    {
        Assert.True(RecruitmentQuery.Enabled(TestConfigs.Sim()));
        Assert.False(RecruitmentQuery.Enabled(Off()));
        Assert.Same(TestConfigs.Sim().Research, Off().Research);   // a pre-military twin keeps the research content as shipped
        foreach (SimConfig older in new[]
                 {
                     TestConfigs.PreM6(TestConfigs.Sim()), TestConfigs.PreDiscovery(TestConfigs.Sim()),
                     TestConfigs.PreTranche2(TestConfigs.Sim()), TestConfigs.PreTrackCp(TestConfigs.Sim()),
                     TestConfigs.PreAllocationRatios(TestConfigs.Sim()), TestConfigs.PreReservations(TestConfigs.Sim()),
                     TestConfigs.PrePerTurnCivics(TestConfigs.Sim()), TestConfigs.PrePerTurnCivicsOnly(TestConfigs.Sim()),
                     TestConfigs.PrePopulationAverageOnly(TestConfigs.Sim()),
                 })
            Assert.False(RecruitmentQuery.Enabled(older));
        Assert.Equal(39, CanonicalSchema.Version);
    }
}
