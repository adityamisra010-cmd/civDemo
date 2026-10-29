using Sim.Core.Kernel;
using Sim.Core.State;
using Sim.Core.Systems;
using Sim.Tests.Systems;
using Sim.Tests.TestUtil;

namespace Sim.Tests.Kernel;

/// <summary>
/// ADR-054 (schema v38; the Director's binding rule D-065 R6, 2026-10-27) — THE ATTRIBUTION OF THE GOLDEN RE-PINS. Two
/// research.json switches act (tuning.civicsPoints.perTurn: CP per turn from the RP base; tuning.populationAverageTurns 2: RP
/// and CP read the average population) and schema v38 appends section 69 (ResearchPopulations).
/// <list type="bullet">
/// <item><b>Research worlds</b> (founded, driven, first reign, the AI world): the live world moves by VALUE. Its SWITCH-OFF
///   TWIN (<see cref="TestConfigs.PrePerTurnCivics"/>: both switches off) writes no ResearchPopulations row, and its v37
///   projection (<see cref="SchemaProjection.V37"/>: section 69 stripped, asserted EMPTY) is the v37 pin byte for byte — so
///   ADR-054 is the whole movement. The older layout links (v36 …) are asserted on the twin
///   (ResearchReserveLayoutAttributionTests.LayoutAlone).</item>
/// <item><b>Worlds no ADR-054 switch acts on</b> (the toy world: no research; the PreM6 and pre-tranche-2 worlds: older
///   research content without the switches): the LAYOUT ALONE — one empty count prefix.</item>
/// </list>
/// The single-switch twins were measured on the AI world (2026-10-27, this tree): with only the average switch off
/// (<see cref="TestConfigs.PrePopulationAverageOnly"/>, CP per turn kept) the v37 projection is b4972200… ≠ OLD; with only
/// the per-turn switch off (<see cref="TestConfigs.PrePerTurnCivicsOnly"/>) the world carries ResearchPopulations rows — each
/// switch moves the world on its own (<see cref="AiWorldSeed42Turn300_EachSwitchMovesItAlone"/>).
/// </summary>
public class Adr054AttributionTests
{
    // The v37 pins — the raw values before schema v38 (each was the golden's pin at 0bf7626).
    internal const string V37ToyGolden = "1e2155736b9cfd2b9009dd4d8f667ae924a11b29e956d44cec5c17c8c50182a3";
    internal const string V37FoundedGolden = "8abc32b0d85ef6fc00d0b7476d976953e34f16d6a7e900bdbdd07e5c49f61e28";
    internal const string V37DrivenGolden = "af5be6a12fd1391d89b51602080bdda338b6cc1ba618c41a24b2928eb14f13b5";
    internal const string V37FirstReignGolden = "6b9571cd1c2f50fe22801104c99e75c55a1555cba6a8617e660c187b5a1a4fe5";
    internal const string V37AiTurn300 = "2a6a61d3dbd26dac52cc94e309924622c28b17dac2a2c94bca929812ac216e74";
    internal const string V37PreTranche2AiTurn300 = "b8646444d555ae50acf64acda63e6abd8cc184af658a35b10e1b533c0d9b59f2";
    internal const string V37PreM6FoundedGolden = "9ec11ace4ca354f4a70ad5b57831cc2f69ccbea3a00c9c17754b88856031c918";
    internal const string V37PreM6AiTurn300 = "b4f6616cd8ce23dbde6ca84668b94eab8793f665f2d5981823fa76fd2455a582";

    private static SimConfig Twin() => TestConfigs.PrePerTurnCivics(TestConfigs.Sim());

    /// <summary>(1) the live world is the re-pinned value and carries ResearchPopulations rows; (2) the switch-off twin writes
    /// none and is the v37 world byte for byte (its v36 link too); (3) old and new differ.</summary>
    private static void Adr054IsTheWholeMove(Func<SimConfig, WorldState> run, string v36, string v37, string raw)
    {
        // ADR-055 (v39, the R4 rule) CAUSE BLOCK: the "live" arm is the military switch-off twin (TestConfigs.PreMilitary — the
        // newer military layer moves every founded world by value), and raw is its v38 pin (MilitaryAttributionTests.V38*, the
        // raw pin before v39) reached through SchemaProjection.V38.
        WorldState live = run(TestConfigs.PreMilitary(TestConfigs.Sim()));
        Assert.True(live.ResearchPopulations.Count > 0, "the live world stores its holders' turn-start populations");
        Assert.Null(ResearchQuery.PopulationsInvariantViolation(live.ResearchPopulations));
        Assert.Equal(raw, SchemaProjection.V38(live));
        WorldState twin = run(Twin());
        Assert.Equal(0, twin.ResearchPopulations.Count);
        ResearchReserveLayoutAttributionTests.LayoutAlone(twin, v36, v37);
        Assert.NotEqual(v37, raw);
    }

    [Fact]
    public void TheShippedContent_HasBothSwitchesOn_AndTheTwinHasBothOff()
    {
        Assert.Equal(39, CanonicalSchema.Version);   // v39: ADR-055 the four military tables — the schema bump alone
        var shipped = TestConfigs.Sim().Research!.Tuning;
        Assert.True(shipped.CivicsPoints is { Enabled: true, PerTurn: true });
        Assert.Equal(2, shipped.PopulationAverageTurns);
        var twin = Twin().Research!.Tuning;
        Assert.True(twin.CivicsPoints is { Enabled: true, PerTurn: false, CoefficientPerYear: 0.008, Exponent: 0.699 });
        Assert.Equal(1, twin.PopulationAverageTurns);
        // The per-turn CP recalibration moved the CIVICS pacing scale only; the twin restores it (technology identical).
        for (int a = 1; a <= 9; a++)
        {
            Assert.Equal(TestConfigs.PreAdr054CivicsScale[a - 1], twin.Pacing!.Of(Sim.Core.Systems.Research.ResearchTree.Civics, a));
            Assert.Equal(shipped.Pacing!.Of(Sim.Core.Systems.Research.ResearchTree.Technology, a), twin.Pacing.Of(Sim.Core.Systems.Research.ResearchTree.Technology, a));
        }
        Assert.NotEqual(TestConfigs.PreAdr054CivicsScale[3], shipped.Pacing!.Of(Sim.Core.Systems.Research.ResearchTree.Civics, 4));
        // A world WITH a population row is behaviour, never layout: the v37 projection refuses it.
        var w = new WorldState(3);
        w.ResearchPopulations.Add(new ResearchPopulationRow(new PolityId(1), 10L));
        Assert.ThrowsAny<Exception>(() => SchemaProjection.V37(w));
        Assert.Equal(1, SchemaProjection.StripV38(w));
        Assert.Equal(WorldHash.ComputeHexAsSchema(new WorldState(3), 37), SchemaProjection.V37(w));
    }

    [Fact]
    public void ToyGoldenSeed42Turn200_MovedForTheV38LayoutAlone()
    {
        WorldState world = SnapshotTests.CanonicalExecutor().Run(SnapshotTests.Genesis(42), 200);
        Assert.Equal(0, world.ResearchPopulations.Count);
        Assert.Equal(V37ToyGolden, SchemaProjection.V37(world));
        Assert.Equal(V37ToyGolden, WorldHash.ComputeHexAsSchema(world, 37));
        Assert.Equal(4 + 16, CanonicalSchema.BytesAppendedSince(world, 37));   // ADR-055 (v39): + sections 70–73, empty
        Assert.Equal(new byte[20], SchemaProjection.Serialize(world)[^20..]);
        Assert.Equal(Sim.Tests.Military.MilitaryAttributionTests.V38ToyGolden, SchemaProjection.V38(world));   // ADR-055: the raw pin before v39
        Assert.NotEqual(V37ToyGolden, Sim.Tests.Military.MilitaryAttributionTests.V38ToyGolden);
    }

    [Fact]
    public void FoundedGoldenSeed42Turn300_MovedForAdr054Alone() =>
        Adr054IsTheWholeMove(cfg => SnapshotTests.RunFoundedGolden(cfg), ResearchReserveLayoutAttributionTests.V36FoundedGolden,
            V37FoundedGolden, Sim.Tests.Military.MilitaryAttributionTests.V38FoundedGolden);

    [Fact]
    public void DrivenGoldenSeed42Turn300_MovedForAdr054Alone() =>
        Adr054IsTheWholeMove(cfg => DrivenGoldenTests.RunDriven(300, cfg).World, ResearchReserveLayoutAttributionTests.V36DrivenGolden,
            V37DrivenGolden, Sim.Tests.Military.MilitaryAttributionTests.V38DrivenGolden);

    [Fact]
    public void FirstReignTurn40_MovedForAdr054Alone() =>
        Adr054IsTheWholeMove(cfg => FirstReignTests.Replay(40, out _, cfg), ResearchReserveLayoutAttributionTests.V36FirstReignGolden,
            V37FirstReignGolden, Sim.Tests.Military.MilitaryAttributionTests.V38FirstReignGolden);

    [Fact]
    public void AiWorldSeed42Turn300_MovedForAdr054Alone() =>
        Adr054IsTheWholeMove(CivicsPointsAttributionTests.RunAiWorld, ResearchReserveLayoutAttributionTests.V36AiTurn300,
            V37AiTurn300, Sim.Tests.Military.MilitaryAttributionTests.V38AiTurn300);

    /// <summary>Each switch acts on its own (single-switch twins on the AI world, values MEASURED 2026-10-27): with only the
    /// average off (CP per turn kept) the world carries no population row and its v37 projection is neither OLD nor the live
    /// value — per-turn CP alone moves it; with only per-turn CP off (the average kept) it carries rows and is a third world.</summary>
    [Fact]
    public void AiWorldSeed42Turn300_EachSwitchMovesItAlone()
    {
        WorldState averageOff = CivicsPointsAttributionTests.RunAiWorld(TestConfigs.PrePopulationAverageOnly(TestConfigs.Sim()));
        Assert.Equal(0, averageOff.ResearchPopulations.Count);
        Assert.Equal(AiAverageOffV37, SchemaProjection.V37(averageOff));
        WorldState perTurnOff = CivicsPointsAttributionTests.RunAiWorld(TestConfigs.PrePerTurnCivicsOnly(TestConfigs.Sim()));
        Assert.True(perTurnOff.ResearchPopulations.Count > 0);
        Assert.Equal(AiPerTurnOffRaw, SchemaProjection.V38(perTurnOff));   // ADR-055: the twin strips the military layer (R4); its v38 form
        foreach (string other in new[] { V37AiTurn300, Sim.Tests.Military.MilitaryAttributionTests.V38AiTurn300 })
        {
            Assert.NotEqual(AiAverageOffV37, other);
            Assert.NotEqual(AiPerTurnOffRaw, other);
        }
    }

    internal const string AiAverageOffV37 = "b4972200b36da29c51174d875ed1410165d988b41b07e780929adecd31e204ff";
    internal const string AiPerTurnOffRaw = "79615ffd5c02e5188132b5c5c28792c2197a9050605a7c509b690d9f87ab691f";
}
