using Sim.Core.Kernel;
using Sim.Core.State;
using Sim.Core.Systems;
using Sim.Tests.TestUtil;
using R = Sim.Tests.TestUtil.CylinderAttributionRig;

namespace Sim.Tests.Kernel;

/// <summary>
/// W1 F7 (batch 4; D-055, 2026-10-16; design §2 rigs #5 (T2 step 1), #14 and the AI half of #17 (T2 step 2)) — H2's TAX AGE
/// GATE (sim.json governance.taxationMinAge) attributed
/// turn-exactly on the SHIPPED CYLINDER: the 1024-px AI world (seed 42, twelve settlements, worldgen aiEmpires 1, AiOrders
/// appended before every step — exactly `sim run --founded --seed 42 --ai-empires 1`), founded and stepped by both arms on
/// worldgen.json as shipped (topology.wrapX). It is the only rig for the gate: the 256-px dev AI world never levies in 450
/// turns, gate or no gate (MEASURED; recon P5f). ON = the shipped config (the class fixture, the ONE shared AI-1024 ON arm
/// of the F7 rigs); OFF = the NON-chained <see cref="SingleSwitchTwins.PreTaxGateOnly"/>. The first action is read off the
/// gate-off run's order log (its first SetTaxRate), never a hash; the AI's logs agree before it, and at it except for the
/// levy itself; and the gate binds there (the AI is below the gate's Age when it levies ungated). DETERMINISM is the
/// existing pinned run of the same world: the ON arm's world 300 is AiFoundingAttributionTests.S1cTurn300 (the CLI's
/// value). M6 Track EX (2026-10-22): both arms run with the NEWER discovery layer off (TestConfigs.PreDiscovery, the R4
/// rule), so the ON arm's world 300 is DiscoveryLayoutAttributionTests.PreDiscoveryAiTurn300 — the v34 S1cTurn300 plus the
/// four empty v35 count prefixes (that test proves its v34 projection is 25f4799d…). The flat leg (AiEmpireIntegrationTests.WithTheTaxAgeGateStripped_…, PreTaxAge on the flat world) is unchanged.
/// The tax gate reads no distance: its seam exposure is recorded as "no distance read".
/// THE ABSENCE RIGS (#14, #17) reuse the same shared ON arm. On this world neither incremental construction (WS1) nor grain
/// relief ever acts: every AI construction order completes in its delivery step, so no queue row survives a step and no
/// progress or hold row is ever written, and no settlement ever eats grain it neither held nor harvested. Each is proved
/// against its own OFF arm (<see cref="TestConfigs.PreIncrementalConstruction"/>, the R4 chained twin that also strips the
/// split and reservations, which need it; <see cref="TestConfigs.PreEmpireFoodAllocation"/>) — the same world on every turn
/// to the pinned run's turn 300 — with the feature's own predicate never holding on the ON arm through its whole horizon.
/// The flat legs (IncrementalConstructionAttributionTests.AiWorldSeed42Turn300_…, EmpireFoodAllocationTests.Switch_Off_AiWorld_…)
/// are unchanged. Seam exposure: 0 seam-crossing pairs and 0 wrapped donor orders at world 0 and at the horizon (MEASURED).
/// </summary>
[Trait("suite", "determinism")]
public class CylinderAttributionAi1024Tests : IClassFixture<CylinderAttributionAi1024Tests.Arms>
{
    // ---------------------------------------------------------------------------------------------------------------
    // MEASURED (2026-10-16, D-055; batch 4 track T2 step 1) on base-b3 + the frozen T1a patch (pinned tree 48e7c13b…,
    // patch sha256 01179819…), Release, by the agent writing this block (probe log t2-out/probe-measure.txt, 340-turn
    // lockstep run): the gate-off AI's first SetTaxRate is stamped 336 while its Age is 2 (the gate is Age 3); the shipped
    // AI has no SetTaxRate in 340 turns; first different world 337 (= 336 + 1); write set {TaxPolicies}; the logs agree to
    // k = 150 (the gate-off levy at 336); the ON arm's world 300 is S1cTurn300 (25f4799d…); seam exposure 0 seam-crossing
    // pairs at world 0 (12 settlements) and at world 340 (19). Equal to the batch-3 recon's cylinder value (P3: 336/337).
    // ---------------------------------------------------------------------------------------------------------------
    private const long GateOffFirstLevy = 336;
    private const int TaxDiverges = (int)GateOffFirstLevy + 1;
    private const int Horizon = TaxDiverges + 2;
    private const int PinnedRunTurn = 300;
    private static readonly PolityId Rival = new(2);
    private static readonly string[] TaxWrites = ["TaxPolicies"];

    // ---------------------------------------------------------------------------------------------------------------
    // MEASURED (2026-10-16, D-055; batch 4 track T2 step 2) on the same tree, by the agent writing this block (the class's
    // own runs; the batch-3 recon measured the same absences on this world, f7-flat-only §5 P2): the shipped AI's first
    // EnqueueConstruction is stamped 2; no queue row survives a step, no progress row and no hold row is written, and relief
    // never moves grain, on any of the 339 turns; the WS1-off and relief-off arms are the shipped world on every turn to 300.
    // ---------------------------------------------------------------------------------------------------------------
    private const int AbsenceHorizon = PinnedRunTurn;
    private const long FirstEnqueue = 2;

    /// <summary>THE SHARED AI-1024 ON ARM (design §2 budget: one ON arm for the AI-1024 rigs), stepped to the tax rig's
    /// horizon; its world 300 is the existing pinned run.</summary>
    public sealed class Arms
    {
        // ADR-043 / ADR-044 / ADR-045 (2026-10-22) CAUSE BLOCK: was TestConfigs.Sim(). The whole M6 slice (Track CP, the
        // discovery layer, the research-content tranche) is NEWER than every layer these rigs attribute (the H2 gate, WS1,
        // relief), so by the R4 rule the shared ON arm is the pre-M6 world (TestConfigs.PreM6) and every measured value below
        // stands; its turn-300 world is the v34 pinned run through SchemaProjection.V34.
        public SimConfig On { get; } = TestConfigs.PreM6(TestConfigs.Sim());
        public R.Arm Live { get; }

        public Arms() =>
            Live = R.Run(R.Ai1024(), "on", On, Horizon, keep: [0, (int)GateOffFirstLevy, TaxDiverges, PinnedRunTurn, Horizon],
                probes: [R.ReliefMovedGrain, R.QueueRowSurvives, R.ProgressRow, R.HoldRow]);
    }

    private readonly Arms _arms;

    public CylinderAttributionAi1024Tests(Arms arms) => _arms = arms;

    [Fact]
    public void TheSharedOnArm_IsTheShippedCylinder_AndReproducesThePinnedAiRun()
    {
        R.Arm live = _arms.Live;
        Assert.True(live.At(0).Terrain!.WrapX);
        Assert.Equal(2, live.At(0).Polities.Count);
        Assert.Equal(CivicsPointsAttributionTests.V34AiTurn300, SchemaProjection.V34(live.At(PinnedRunTurn)));   // ADR-043: the v34 run
    }

    /// <summary>#5 — the tax Age gate. With the gate stripped the AI levies at T while still below the gate's Age; the
    /// shipped AI does not levy in the horizon (its first levy is at 422, on its first A3 turn —
    /// AiEmpireIntegrationTests); the worlds are identical to T and differ from T + 1, the levy's delivery.</summary>
    [Fact]
    public void TaxAgeGate_OnTheAi1024Cylinder_DivergesExactlyAtTheUngatedLevysDelivery()
    {
        SimConfig on = _arms.On, off = SingleSwitchTwins.PreTaxGateOnly(on);
        int minAge = on.Governance!.TaxationMinAge!.Value;
        Assert.Null(off.Governance!.TaxationMinAge);
        R.Arm live = _arms.Live;
        R.Arm twin = R.Run(R.Ai1024(), "tax-gate-off", off, Horizon, keep: [TaxDiverges]);

        R.AssertWorld0Identical(live, twin);
        long levy = R.FirstStamp(twin.Log, OrderKind.SetTaxRate);
        Assert.Equal(GateOffFirstLevy, levy);                                                 // vacuity: it levies in the horizon
        Assert.Equal(-1L, R.FirstStamp(live.Log, OrderKind.SetTaxRate));                       // the gate holds it ON
        Assert.True(AgeQuery.CurrentAge(live.At((int)levy), TestConfigs.Ages(), Rival) < minAge,
            "the gate must bind: the ungated levy falls below the gate's Age");
        Assert.Equal(TaxDiverges, R.FirstDifferent(live, twin));
        R.AssertLogsEqualBefore(live.Log, twin.Log, levy);
        R.AssertLogsEqualAtExcept(twin.Log, live.Log, levy, OrderKind.SetTaxRate);
        string[] written = R.WriteSet(live.At(TaxDiverges), twin.At(TaxDiverges));
        Assert.Equal(TaxWrites, written);
        R.AssertNoTopologyTable(written);
    }

    /// <summary>#14 — WS1 (incremental construction) is ABSENT on the AI world: the incremental rule can only act on a head
    /// that survives its delivery step, and none does — so the world with WS1 (and the layers that need it) switched off is
    /// the shipped world on every turn.</summary>
    [Fact]
    public void Ws1Absence_OnTheAi1024Cylinder_NoHeadSurvivesAStep_AndTheWs1OffWorldIsTheSameEveryTurn()
    {
        SimConfig on = _arms.On, off = TestConfigs.PreIncrementalConstruction(on);
        Assert.True(on.Construction!.Incremental);
        Assert.False(off.Construction!.Incremental);
        R.Arm live = _arms.Live;
        Assert.Equal(FirstEnqueue, R.FirstStamp(live.Log, OrderKind.EnqueueConstruction));     // vacuity: the AI builds
        Assert.Equal(-1L, live.FirstHeld(R.QueueRowSurvives.Name));                            // the premise, every turn
        Assert.Equal(-1L, live.FirstHeld(R.ProgressRow.Name));
        Assert.Equal(-1L, live.FirstHeld(R.HoldRow.Name));
        R.Arm twin = R.Run(R.Ai1024(), "ws1-off", off, AbsenceHorizon, keep: [AbsenceHorizon]);
        Assert.Equal(-1, R.FirstDifferent(live, twin));
        R.AssertLogsEqualBefore(live.Log, twin.Log, AbsenceHorizon);
        Assert.Equal(live.Hashes[AbsenceHorizon], twin.Hashes[AbsenceHorizon]);
        Assert.Equal(CivicsPointsAttributionTests.V34AiTurn300, SchemaProjection.V34(live.At(AbsenceHorizon)));   // ADR-043: the v34 run
        WorldState end = live.At(Horizon);
        Assert.Equal((0, 0), (R.SeamCrossingPairs(end), R.WrappedDonorOrders(end)));
    }

    /// <summary>#17 (AI half) — intra-polity grain relief is ABSENT on the AI world: no settlement ever eats grain it neither
    /// held nor harvested (the grain books, <see cref="R.ReliefMovedGrain"/>), and the relief-off world is the shipped world on
    /// every turn.</summary>
    [Fact]
    public void ReliefAbsence_OnTheAi1024Cylinder_ReliefNeverActs_AndTheReliefOffWorldIsTheSameEveryTurn()
    {
        SimConfig on = _arms.On, off = TestConfigs.PreEmpireFoodAllocation(on);
        Assert.True(on.Consumption.EmpireFoodAllocation);
        Assert.False(off.Consumption.EmpireFoodAllocation);
        R.Arm live = _arms.Live;
        Assert.Equal(-1L, live.FirstHeld(R.ReliefMovedGrain.Name));
        R.Arm twin = R.Run(R.Ai1024(), "relief-off", off, AbsenceHorizon);
        Assert.Equal(-1, R.FirstDifferent(live, twin));
        R.AssertLogsEqualBefore(live.Log, twin.Log, AbsenceHorizon);
        Assert.Equal(live.Hashes[AbsenceHorizon], twin.Hashes[AbsenceHorizon]);
        Assert.Equal(CivicsPointsAttributionTests.V34AiTurn300, SchemaProjection.V34(live.At(AbsenceHorizon)));   // ADR-043: the v34 run
    }
}
