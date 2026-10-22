using Sim.Core.Systems;
using Sim.Tests.TestUtil;
using R = Sim.Tests.TestUtil.CylinderAttributionRig;

namespace Sim.Tests.Kernel;

/// <summary>
/// W1 F7 (batch 4; D-055, 2026-10-16; design §2 rig #17, the founded half; the AI half is
/// CylinderAttributionAi1024Tests.ReliefAbsence_…) — THE RELIEF-ABSENCE CONTROL on the SHIPPED CYLINDER: on the canonical
/// order-less founded world (seed 42, 1024 px, worldgen.json as shipped: topology.wrapX) intra-polity grain relief never
/// acts — no settlement ever eats grain it neither held nor harvested (<see cref="R.ReliefMovedGrain"/>, the grain books) —
/// so the world with relief switched off (<see cref="TestConfigs.PreEmpireFoodAllocation"/>, every other layer on) is the
/// shipped world on every one of the 300 turns. DETERMINISM is the existing pinned run of the same world: the ON arm's world
/// 300 is SnapshotTests.FoundedGoldenHash. M6 Track EX (2026-10-22): both arms run with the NEWER discovery layer off
/// (TestConfigs.PreDiscovery, the R4 rule), so world 300 is DiscoveryLayoutAttributionTests.PreDiscoveryFoundedGolden — the
/// v34 founded golden plus the four empty v35 count prefixes (that test proves its v34 projection is 0f29efc7…). The flat control (EmpireFoodAllocationTests.Switch_Off_FoundedGolden_…, the
/// de86ace literal on the flat world) is unchanged.
/// SEAM EXPOSURE (MEASURED): 0 seam-crossing pairs and 0 wrapped donor orders at world 0 and at world 300 — and relief,
/// never acting, reads no donor order here at all; the seam half of relief is CylinderAttributionSeamTests.
/// THE TWO OTHER FOUNDED ABSENCE CONTROLS (T2 fix round, finding INVENTORY-TWO-ABSENCE-CONTROLS): the flat-only
/// GranaryAttributionTests.TheFoundedGolden_IsUnmovedByTheGranaryEffects_OnOrOff and EmpireConstructionStockpileTests.
/// WithTheBatchOff_TheFoundedGoldenIsDe86aceByteForByte_AndThePurseMovesItNot get their cylinder twins here, against the same
/// shared ON arm (one 300-turn run, a class fixture): no granary ever stands and no construction is ever queued on the
/// order-less world, so the granary-off and the purse-off worlds are the shipped world on every turn. (The flat originals
/// are unchanged.)
/// </summary>
public class CylinderAttributionReliefAbsenceTests : IClassFixture<CylinderAttributionReliefAbsenceTests.Shipped>
{
    // ---------------------------------------------------------------------------------------------------------------
    // MEASURED (2026-10-16, D-055; batch 4 track T2 step 2) on base-b3 + the frozen T1a patch (pinned tree 48e7c13b…,
    // patch sha256 01179819…), Release, by the agent writing this block (probe log t2-out/s2/probe-a.txt, B3): relief never
    // moves grain in 300 turns; the relief-off arm never differs; world 300 is 0f29efc7… = SnapshotTests.FoundedGoldenHash.
    // Equal to the batch-3 recon's cylinder measurement (f7-flat-only §5 P1: "relief never moves grain … ON = OFF every turn").
    // MEASURED (D-055, 2026-10-16; batch 4 track T2 FIX ROUND, run 2026-10-17) on base-b3 + T1a + T2, Release, by the agent
    // writing this block (this class's run, t2-out/fix): no granary stands, neither granary factor differs from 1 and no
    // construction queue row survives a step in the 300 shipped turns; the granary-off and the purse-off arms never differ from it, and each one's world 300 is
    // 0f29efc7… (equal to the T2 verifier's probe: "granaryOff=0f29efc7… purseOff=0f29efc7… structures=0/0").
    // ---------------------------------------------------------------------------------------------------------------
    private const int Horizon = 300;

    /// <summary>The shared ON arm: the shipped config on the founded cylinder, 300 turns, every absence probe evaluated.</summary>
    public sealed class Shipped
    {
        public SimConfig On { get; } = TestConfigs.PreDiscovery(TestConfigs.Sim());
        public R.Arm Live { get; }

        public Shipped() =>
            Live = R.Run(R.Founded1024(), "on", On, Horizon, keep: [0, Horizon],
                probes: [R.ReliefMovedGrain, R.GranaryStands, R.GranaryActs, R.QueueRowSurvives]);
    }

    /// <summary>The twin's world is the shipped world on every turn both reached. On a difference the failure is read off
    /// the state, not a hash: both arms are re-run to the first differing turn and the message names the tables that differ
    /// there (the measured write set).</summary>
    private static void AssertNeverDiffers(R.Arm live, R.Arm twin)
    {
        int t = R.FirstDifferent(live, twin);
        if (t < 0) return;
        R.Arm a = R.Run(live.Rig, live.Label, live.Cfg, t, keep: [t]);
        R.Arm b = R.Run(twin.Rig, twin.Label, twin.Cfg, t, keep: [t]);
        Assert.Fail($"{twin.Label}: the world first differs from the shipped one at turn {t}, in [{string.Join(", ", R.WriteSet(a.At(t), b.At(t)))}]");
    }

    private readonly Shipped _shipped;

    public CylinderAttributionReliefAbsenceTests(Shipped shipped) => _shipped = shipped;

    [Fact]
    public void ReliefAbsence_OnTheFoundedCylinder_ReliefNeverActs_AndTheReliefOffWorldIsTheSameEveryTurn()
    {
        SimConfig on = _shipped.On, off = TestConfigs.PreEmpireFoodAllocation(on);
        Assert.True(on.Consumption.EmpireFoodAllocation);
        Assert.False(off.Consumption.EmpireFoodAllocation);
        R.Arm live = _shipped.Live;
        R.Arm twin = R.Run(R.Founded1024(), "relief-off", off, Horizon);

        Assert.Equal(-1L, live.FirstHeld(R.ReliefMovedGrain.Name));          // relief has nothing to relieve, on every turn
        AssertNeverDiffers(live, twin);                                      // and switching it off changes nothing
        Assert.Equal(DiscoveryLayoutAttributionTests.PreDiscoveryFoundedGolden, live.Hashes[Horizon]); // determinism: the pinned founded golden
        foreach (int t in new[] { 0, Horizon })
            Assert.Equal((0, 0), (R.SeamCrossingPairs(live.At(t)), R.WrappedDonorOrders(live.At(t))));
    }

    /// <summary>The granary effects' founded absence control on the cylinder: the order-less world never builds a granary,
    /// so the granary-off world (<see cref="TestConfigs.PreGranaryEffects"/>, every other layer on) is the shipped world on
    /// every turn.</summary>
    [Fact]
    public void GranaryEffectsAbsence_OnTheFoundedCylinder_NoGranaryEverStands_AndTheGranaryOffWorldIsTheSameEveryTurn()
    {
        SimConfig on = _shipped.On, off = TestConfigs.PreGranaryEffects(on);
        Assert.True(on.Granaries!.Enabled);
        Assert.False(off.Granaries!.Enabled);
        R.Arm live = _shipped.Live;
        Assert.Equal(-1L, live.FirstHeld(R.GranaryStands.Name));             // nothing for the effects to act on, any turn,
        Assert.Equal(-1L, live.FirstHeld(R.GranaryActs.Name));               // and neither factor ever differs from 1
        Assert.Equal(0, live.At(Horizon).Structures.Count);
        R.Arm twin = R.Run(R.Founded1024(), "granary-off", off, Horizon);

        AssertNeverDiffers(live, twin);                                      // switching the effects off changes nothing
        Assert.Equal(DiscoveryLayoutAttributionTests.PreDiscoveryFoundedGolden, twin.Hashes[Horizon]); // determinism: the pinned founded golden
        Assert.Equal(DiscoveryLayoutAttributionTests.PreDiscoveryFoundedGolden, live.Hashes[Horizon]);
    }

    /// <summary>The construction purse's founded absence control on the cylinder: the order-less world (no player order, no
    /// AI) never queues a construction, so the purse-off world (<see cref="TestConfigs.PreEmpireConstructionStockpile"/>,
    /// every other layer on) is the shipped world on every turn.</summary>
    [Fact]
    public void ConstructionPurseAbsence_OnTheFoundedCylinder_NothingIsEverQueued_AndThePurseOffWorldIsTheSameEveryTurn()
    {
        SimConfig on = _shipped.On, off = TestConfigs.PreEmpireConstructionStockpile(on);
        Assert.True(on.Construction!.EmpireStockpile);
        Assert.False(off.Construction!.EmpireStockpile);
        R.Arm live = _shipped.Live;
        Assert.Equal(0, live.Log.Count);                                     // no order, no AI: nothing to pay for
        Assert.Equal(-1L, live.FirstHeld(R.QueueRowSurvives.Name));
        Assert.Equal(0, live.At(Horizon).Structures.Count);
        R.Arm twin = R.Run(R.Founded1024(), "purse-off", off, Horizon);

        AssertNeverDiffers(live, twin);                                      // switching the purse off changes nothing
        Assert.Equal(DiscoveryLayoutAttributionTests.PreDiscoveryFoundedGolden, twin.Hashes[Horizon]); // determinism: the pinned founded golden
        Assert.Equal(DiscoveryLayoutAttributionTests.PreDiscoveryFoundedGolden, live.Hashes[Horizon]);
    }
}
