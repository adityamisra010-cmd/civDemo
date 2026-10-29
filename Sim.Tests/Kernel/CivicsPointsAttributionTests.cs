using Sim.Core;
using Sim.Core.Kernel;
using Sim.Core.State;
using Sim.Core.Systems;
using Sim.Core.Worldgen;
using Sim.Tests.Systems;
using Sim.Tests.TestUtil;

namespace Sim.Tests.Kernel;

/// <summary>
/// ADR-043 (D-062, the Director's 2026-10-22 ruling) — THE ATTRIBUTION OF THE TRACK-CP RE-PINS (the
/// ReservationsLayoutAttributionTests precedent). Track CP changes two research.json layers — the research stage drops its
/// civic atom, and Civics Points split research into two lanes (tuning.civicsPoints) — and schema v35 appends section 62
/// (CivicsPoints) after ConstructionHolds without changing any existing row. Every golden world carries at least one
/// polity with population, so it accrues CP from turn 1 (its account row exists) and its research lanes change: each golden
/// moves by VALUE, not by layout alone.
///
/// For each re-pinned golden this class asserts, MEASURED on this tree:
/// <list type="number">
/// <item>the live (shipped) world's raw hash is the re-pinned value;</item>
/// <item>the SWITCH-OFF TWIN (TestConfigs.PreTrackCp: civicsPoints removed and the v34 stage restored) writes no CP row,
///   and its v34 projection (SchemaProjection.V34 — section 62 dropped, asserted EMPTY) is the v34 pin byte for byte —
///   so Track CP is the entire movement;</item>
/// <item>the two single-layer twins: Civics Points off alone (TestConfigs.PreCivicsPoints) also returns the v34 pin, and
///   the stage gate off alone (TestConfigs.PreStageGate) returns the live value — so on every golden world the CIVICS
///   POINTS layer is the whole cause and the stage gate moves nothing (no golden world reaches the research stage by
///   its horizon).</item>
/// </list>
/// The toy golden's world has no research content and no polity: it moves for the v35 LAYOUT ALONE (one empty count
/// prefix), and its v34 projection is the v34 value.
/// </summary>
public class CivicsPointsAttributionTests
{
    /// <summary>The v34 goldens, as the v34 (D-055/D-058) build pinned them.</summary>
    internal const string V34FoundedGolden = "0f29efc733024c451b7ebc2e8435fdd8a6032d158922430cb290778a98056d3c";
    internal const string V34ToyGolden = "2e6d446161e165475c753a8962f5b8f9e0845807c911f41be5ba5d982bed5ef7";
    internal const string V34DrivenGolden = "eb88663ac944da5b4bed584e60fa786ce90d1fc9cc6777906576ac608887e893";
    internal const string V34FirstReignGolden = "f7ca60a52362a7da30c73be82ad8ecd56aeeb68f932b2ad98d8d3e76e3219057";
    internal const string V34AiTurn300 = "25f4799d581d8de72dc15d487bcba54cb9db399208aba065963eb1f257e9fd10";

    private static void AssertTrackCpIsTheWholeMove(
        Func<SimConfig, WorldState> run, string v34, string v35, bool stageGateMoves = false)
    {
        // ADR-055 (v39, the R4 rule) CAUSE BLOCK: the "live" arm is the military switch-off twin (TestConfigs.PreMilitary — the
        // founding warband's personnel and upkeep are newer than Track CP), and v35 is its v38 pin (MilitaryAttributionTests.V38*,
        // the raw pin before v39) reached through SchemaProjection.V37.
        WorldState live = run(TestConfigs.PreMilitary(TestConfigs.Sim()));
        Assert.True(live.CivicsPoints.Count > 0, "the live world accrues Civics Points (an account row)");
        Assert.Null(ResearchQuery.CivicsPointsInvariantViolation(live.CivicsPoints));
        Assert.Equal(v35, SchemaProjection.V38(live));                         // (1) the re-pinned value (the whole M6 slice on)

        // M6 merge (2026-10-22/23): Track EX (the discovery layer, sections 63–66) and Track TREE (the research-content tranche,
        // ADR-045) are the slice's other parts, so the CP layers are attributed on the EX-OFF, TRANCHE-OFF world
        // (TestConfigs.PreDiscovery, TestConfigs.PreTreeTranche); the EX part is DiscoveryLayoutAttributionTests.
        SimConfig shipped = TestConfigs.PreTreeTranche(TestConfigs.PreDiscovery(TestConfigs.Sim()));
        WorldState cpOn = run(shipped);
        Assert.True(cpOn.CivicsPoints.Count > 0);

        WorldState off = run(TestConfigs.PreTrackCp(shipped));
        Assert.Equal(0, off.CivicsPoints.Count);
        Assert.Equal(v34, SchemaProjection.V34(off));                          // (2) both tracks off = the v34 world

        WorldState cpOff = run(TestConfigs.PreCivicsPoints(shipped));
        Assert.Equal(v34, SchemaProjection.V34(cpOff));                        // (3a) the CP layer alone is the CP move
        WorldState stageOff = run(TestConfigs.PreStageGate(shipped));
        if (stageGateMoves) Assert.NotEqual(WorldHash.ComputeHex(cpOn), WorldHash.ComputeHex(stageOff));
        else Assert.Equal(WorldHash.ComputeHex(cpOn), WorldHash.ComputeHex(stageOff));   // (3b) the stage gate moves nothing here
        Assert.NotEqual(v34, v35);
    }

    [Fact]
    public void FoundedGoldenSeed42Turn300_MovedForCivicsPointsAlone() =>
        AssertTrackCpIsTheWholeMove(cfg => SnapshotTests.RunFoundedGolden(cfg), V34FoundedGolden, Sim.Tests.Military.MilitaryAttributionTests.V38FoundedGolden);

    [Fact]
    public void DrivenGoldenSeed42Turn300_MovedForCivicsPointsAlone() =>
        AssertTrackCpIsTheWholeMove(cfg => DrivenGoldenTests.RunDriven(300, cfg).World, V34DrivenGolden, Sim.Tests.Military.MilitaryAttributionTests.V38DrivenGolden);

    [Fact]
    public void FirstReignTurn40_MovedForCivicsPointsAlone() =>
        AssertTrackCpIsTheWholeMove(cfg => FirstReignTests.Replay(40, out _, cfg), V34FirstReignGolden, Sim.Tests.Military.MilitaryAttributionTests.V38FirstReignGolden);

    [Fact]
    public void AiWorldSeed42Turn300_MovedForCivicsPointsAlone() =>
        AssertTrackCpIsTheWholeMove(RunAiWorld, V34AiTurn300, Sim.Tests.Military.MilitaryAttributionTests.V38AiTurn300);

    [Fact]
    public void ToyGoldenSeed42Turn200_MovedForTheV35LayoutAlone()
    {
        WorldState world = SnapshotTests.CanonicalExecutor().Run(SnapshotTests.Genesis(42), 200);
        Assert.Equal(0, world.CivicsPoints.Count);
        Assert.Equal(V34ToyGolden, SchemaProjection.V34(world));               // the v34 value, byte for byte
        Assert.Equal(Sim.Tests.Military.MilitaryAttributionTests.V38ToyGolden, SchemaProjection.V38(world));   // ADR-055: the v38 pin (the raw pin before v39)
        Assert.NotEqual(V34ToyGolden, Sim.Tests.Military.MilitaryAttributionTests.V38ToyGolden);
        Assert.Equal(new byte[4 * SchemaProjection.V35TableCount], SchemaProjection.Serialize(world)[^(4 * SchemaProjection.V35TableCount)..]);   // the movement IS the empty v35 count prefixes (sections 62–66)
    }

    /// <summary>The AI world (`sim run --founded --seed 42 --turns 300 --ai-empires 1`, AiFoundingAttributionTests' live
    /// arm) on <paramref name="cfg"/>.</summary>
    internal static WorldState RunAiWorld(SimConfig cfg)
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
        }
        return w;
    }

    /// <summary>
    /// The per-layer TURN-EXACT proof on the AI world: the live world and the Track-CP-off twin are byte-identical in their
    /// v34 form up to the first step that writes a Civics Points account — step 0 → 1, where every Empire with population
    /// accrues — and from that step on the live stream carries the account (the layer acts from turn 1, by construction:
    /// CP accrue every step, target or not). The research lanes themselves first differ when the AI first issues a civic
    /// order on its own lane.
    /// </summary>
    [Fact]
    public void TheCpLayer_ActsFromTheFirstStep_TheTwinIsTheV34WorldAtTurnZero()
    {
        SimConfig live = TestConfigs.PreTreeTranche(TestConfigs.PreDiscovery(TestConfigs.Sim())), off = TestConfigs.PreTrackCp(live);   // M6 merge: on the EX-off, tranche-off world
        WorldgenConfig wg = TestConfigs.DevWorldgen();
        WorldState a = WorldFounding.Found(wg, live, 42), b = WorldFounding.Found(wg, off, 42);
        Assert.Equal(SchemaProjection.V34(b), SchemaProjection.V34(a));   // turn 0: no account yet, the same world
        TurnExecutor ea, eb;
        using (var era = Sim.Data.DataFiles.OpenEraPacing())
        using (var pipe = Sim.Data.DataFiles.OpenPipeline())
            ea = new TurnExecutor(EraTableLoader.Load(era), PipelineLoader.Load(pipe, SystemCatalog.All(live, wg)));
        using (var era = Sim.Data.DataFiles.OpenEraPacing())
        using (var pipe = Sim.Data.DataFiles.OpenPipeline())
            eb = new TurnExecutor(EraTableLoader.Load(era), PipelineLoader.Load(pipe, SystemCatalog.All(off, wg)));
        WorldState a1 = ea.Step(a), b1 = eb.Step(b);
        Assert.True(a1.CivicsPoints.Count > 0);
        Assert.Equal(0, b1.CivicsPoints.Count);
        // With no order in either log, the only difference after one step is the CP accounts: strip them and the v34
        // forms agree (no civic was available, so no lane differs yet).
        WorldState stripped = a1.Clone();
        SchemaProjection.StripV35(stripped);
        Assert.Equal(SchemaProjection.V34(b1), SchemaProjection.V34(stripped));
    }
}
