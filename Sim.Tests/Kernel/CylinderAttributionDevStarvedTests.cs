using Sim.Core.Pathing;
using Sim.Core.State;
using Sim.Core.Systems;
using Sim.Tests.TestUtil;
using R = Sim.Tests.TestUtil.CylinderAttributionRig;

namespace Sim.Tests.Kernel;

/// <summary>
/// W1 F7 (batch 4; D-055, 2026-10-16; design §2 rig #1, the primary half) — INTRA-POLITY GRAIN RELIEF (D-053 §2) attributed
/// turn-exactly on the dev CYLINDER's starved rig: the D-025 dev preset (256 px, four settlements, seed 42) as shipped
/// (topology.wrapX), with ExplainRigs' STARVED orders — settlement 0 ordered to zero farming and herding at turn 1, so it
/// eats its store down while its three siblings hold spare grain. ON = the shipped config; OFF = the single-layer twin
/// <see cref="TestConfigs.PreEmpireFoodAllocation"/> (every other layer on). The first action is read off the ON run's
/// grain books (<see cref="R.ReliefMovedGrain"/>), never a hash. (ExplainRigs.Starved itself stays FLAT and relief-off: it
/// pins explanation text, not this feature.)
/// SEAM EXPOSURE (MEASURED, recorded in D-055): the dev world's four settlements have 0 seam-crossing pairs and 0 wrapped
/// donor orders, so relief's donor order is not exercised across the seam by this rig (a topology-blind, raw-dx donor order
/// passes it: T2 step 2, MEASURED). Relief's seam half is CylinderAttributionSeamTests.Relief_… (SeamWorld).
/// THE POLITY BOUNDARY (T2 step 3): every one-polity relief rig passes a relief that sources grain from ANY settlement
/// nearest-first (MEASURED: that mutant survives this class's first fact, the driven rig and the SeamWorld rig), so the
/// second fact starves a player settlement of the dev AI cylinder whose nearest settlement is the AI's, and pins the other
/// polity's stores and books equal to the relief-off twin's on the first relief world.
/// </summary>
public class CylinderAttributionDevStarvedTests
{
    // ---------------------------------------------------------------------------------------------------------------
    // MEASURED (2026-10-16, D-055; batch 4 track T2 step 1) on base-b3 + the frozen T1a patch (pinned tree 48e7c13b…,
    // patch sha256 01179819…), Release, by the agent writing this block (probe log t2-out/probe-measure.txt): the ON run's
    // grain books first show relief in world 3 (the step that first reads the zero harvest; 10 relief steps in 12 turns),
    // the relief-off run never; first different world 3; write set {ConsumptionDeficits, GoodStocks, LedgerFlows}; seam
    // exposure at world 0: 0 pairs, 0 wrapped donor orders. Equal to the batch-3 recon's cylinder value (P5a: 3).
    // ---------------------------------------------------------------------------------------------------------------
    private const int FirstRelief = 3;
    private const int Horizon = FirstRelief + 2;
    private static readonly string[] Writes = ["ConsumptionDeficits", "GoodStocks", "LedgerFlows"];

    [Fact]
    public void Relief_OnTheDevStarvedCylinder_DivergesExactlyAtTheFirstReliefTransfer()
    {
        SimConfig on = TestConfigs.Sim(), off = TestConfigs.PreEmpireFoodAllocation(on);
        Assert.True(on.Consumption.EmpireFoodAllocation);
        Assert.False(off.Consumption.EmpireFoodAllocation);
        R.Arm live = R.Run(R.DevStarved(), "on", on, Horizon, keep: [0, FirstRelief - 1, FirstRelief], probes: [R.ReliefMovedGrain]);
        R.Arm twin = R.Run(R.DevStarved(), "relief-off", off, Horizon, keep: [FirstRelief], probes: [R.ReliefMovedGrain]);

        R.AssertWorld0Identical(live, twin);
        Assert.Equal(FirstRelief, live.FirstHeld(R.ReliefMovedGrain.Name));    // the independent first action (vacuity: > 0)
        Assert.Equal(-1L, twin.FirstHeld(R.ReliefMovedGrain.Name));
        Assert.Equal(FirstRelief, R.FirstDifferent(live, twin));
        string[] written = R.WriteSet(live.At(FirstRelief), twin.At(FirstRelief));
        Assert.Equal(Writes, written);
        R.AssertNoTopologyTable(written);

        // The relieved settlement is the starved one: settlement 0's grain books close the step into the first relief
        // world with grain eaten that it neither held nor harvested, and every sibling's books close it non-negative.
        WorldState before = live.At(FirstRelief - 1), after = live.At(FirstRelief);
        Assert.True(R.GrainResidual(before, after, new SettlementId(Sim.Tests.Observability.ExplainRigs.Target), on) < 0);
        for (int i = 0; i < after.Settlements.Count; i++)
            if (after.Settlements[i].Id.Value != Sim.Tests.Observability.ExplainRigs.Target)
                Assert.True(R.GrainResidual(before, after, after.Settlements[i].Id, on) >= 0);

        R.AssertSameSeries(live, R.Run(R.DevStarved(), "on-again", TestConfigs.Sim(), Horizon));
        WorldState w0 = live.At(0);
        Assert.Equal((0, 0), (R.SeamCrossingPairs(w0), R.WrappedDonorOrders(w0)));
    }

    // ---------------------------------------------------------------------------------------------------------------
    // MEASURED (2026-10-16, D-055; batch 4 track T2 step 3) on base-b3 + the frozen T1a patch (pinned tree 48e7c13b…,
    // patch sha256 01179819…), Release, by the agent writing this block (probe log t2-out/s3/probes/probe3-run2.txt, P3a):
    // the dev AI cylinder (`sim run --founded --seed 42 --size 256 --settlements 4 --ai-empires 1`) with the player's
    // settlement 2 ordered to zero food at turn 1: settlements 0 and 2 are the player's, 1 and 3 the AI's; settlement 2's
    // nearest settlement the short way is the AI's settlement 3 (617 px², against the player's settlement 0 at 2,152), so
    // its nearest-first order over EVERY settlement is [2, 3, 0, 1] while its polity's donor order is [2, 0]. Relief first
    // moves grain in world 3 (settlement 2 eats 2,968 it neither held nor harvested), the relief-off twin never; first
    // different world 3; write set {ConsumptionDeficits, GoodStocks, LedgerFlows}; the AI's settlements 1 and 3 hold 302 and
    // 400 grain in world 3 in BOTH arms (443 at settlement 3 in world 2: grain a polity-blind relief would take first);
    // the logs agree before 3.
    // ---------------------------------------------------------------------------------------------------------------
    private const int PolityRigStarved = 2;
    private const int PolityRigFirstRelief = 3;
    private const int PolityRigHorizon = PolityRigFirstRelief + 2;
    private const long ForeignNearestHeldBefore = 443;

    /// <summary>The dev AI cylinder with the player's settlement 2 starved (rig #1, the POLITY BOUNDARY; T2 step 3): the
    /// shipped `--size 256 --settlements 4 --ai-empires 1` founding, the AI producer appended after the player's batch.</summary>
    private static R.RigWorld DevAiStarved() => new("dev-ai-starved-s2", TestConfigs.DevWorldgen() with { AiEmpires = 1 },
        TestConfigs.DevWorldgen(), 42, Ai: true, Script: _ => Sim.Tests.Observability.ExplainRigs.ZeroFoodOrders(PolityRigStarved));

    /// <summary>
    /// #1 — RELIEF STAYS INSIDE ONE POLITY (D-053 §2: "only inside one polity"), turn-exact on the dev AI CYLINDER, where the
    /// starving settlement's NEAREST settlement belongs to ANOTHER polity: relief first acts at its measured turn, and the
    /// other polity's stores are exactly what they are in the relief-off twin — a relief that sourced grain from any
    /// settlement nearest-first would take the AI's settlement 3's grain first (T2 step 3's B1b mutant; it survives every
    /// one-polity relief rig).
    /// </summary>
    [Fact]
    public void Relief_NeverSourcesFromAnotherPolity_EvenWhenItsSettlementIsTheStarvingOnesNearest_DevAiCylinder()
    {
        SimConfig on = TestConfigs.Sim(), off = TestConfigs.PreEmpireFoodAllocation(on);
        int[] keep = [0, PolityRigFirstRelief - 1, PolityRigFirstRelief];
        R.Arm live = R.Run(DevAiStarved(), "on", on, PolityRigHorizon, keep: keep, probes: [R.ReliefMovedGrain]);
        R.Arm twin = R.Run(DevAiStarved(), "relief-off", off, PolityRigHorizon, keep: keep, probes: [R.ReliefMovedGrain]);

        // The premise, on world 0: the starving settlement's nearest settlement (the short way) is the other polity's.
        WorldState w0 = live.At(0);
        var starved = new SettlementId(PolityRigStarved);
        Assert.True(EmpireQuery.TryGetController(w0, starved, out PolityId player));
        int[] everyNearestFirst = NearestFirst(w0, starved);
        Assert.Equal(new[] { 2, 3, 0, 1 }, everyNearestFirst);
        Assert.True(EmpireQuery.TryGetController(w0, new SettlementId(everyNearestFirst[1]), out PolityId other));
        Assert.NotEqual(player.Value, other.Value);
        SettlementId[] donors = RoadDevelopmentQuery.PayingSettlements(w0, player, starved, starved);
        Assert.Equal(new[] { 2, 0 }, Array.ConvertAll(donors, d => d.Value));
        Assert.Equal((0, 0), (R.SeamCrossingPairs(w0), R.WrappedDonorOrders(w0)));

        // What relief does, read off the grain books (never a hash): first at its measured turn, the starved settlement is the
        // one relieved, and every settlement of the OTHER polity holds and books exactly what the relief-off twin does.
        Assert.Equal(PolityRigFirstRelief, live.FirstHeld(R.ReliefMovedGrain.Name));
        Assert.Equal(-1L, twin.FirstHeld(R.ReliefMovedGrain.Name));
        WorldState before = live.At(PolityRigFirstRelief - 1), a = live.At(PolityRigFirstRelief);
        WorldState beforeOff = twin.At(PolityRigFirstRelief - 1), b = twin.At(PolityRigFirstRelief);
        int grain = on.Goods!.GrainId;
        Assert.Equal(ForeignNearestHeldBefore, Stock(before, new SettlementId(everyNearestFirst[1]), grain));   // vacuity
        Assert.True(R.GrainResidual(before, a, starved, on) < 0, "the starved settlement was not the one relieved");
        int foreign = 0;
        for (int i = 0; i < a.Settlements.Count; i++)
        {
            SettlementId s = a.Settlements[i].Id;
            if (!EmpireQuery.TryGetController(before, s, out PolityId ruler) || ruler.Value == player.Value) continue;
            foreign++;
            Assert.Equal(Stock(b, s, grain), Stock(a, s, grain));
            Assert.Equal(R.GrainResidual(beforeOff, b, s, off), R.GrainResidual(before, a, s, on));
        }
        Assert.Equal(2, foreign);

        // The attribution template: world 0 identical, the first difference at the first relief, the measured write set
        // there with no topology table, the AI's logs equal before it, and a second ON run.
        R.AssertWorld0Identical(live, twin);
        Assert.Equal(PolityRigFirstRelief, R.FirstDifferent(live, twin));
        string[] written = R.WriteSet(a, b);
        Assert.Equal(Writes, written);
        R.AssertNoTopologyTable(written);
        R.AssertLogsEqualBefore(live.Log, twin.Log, PolityRigFirstRelief);
        R.AssertSameSeries(live, R.Run(DevAiStarved(), "on-again", TestConfigs.Sim(), PolityRigHorizon));
    }

    /// <summary>Every settlement of <paramref name="w"/> nearest-first to <paramref name="at"/> on the cylinder (squared pixels
    /// the short way round; ties by id), whoever rules it.</summary>
    private static int[] NearestFirst(IReadOnlyWorldState w, SettlementId at)
    {
        int size = w.Terrain!.Size, site = -1;
        for (int i = 0; i < w.Settlements.Count; i++) if (w.Settlements[i].Id == at) site = w.Settlements[i].SiteCell;
        var keys = new (long D, int Id)[w.Settlements.Count];
        for (int i = 0; i < keys.Length; i++)
            keys[i] = (MapTopology.SquaredPx(w.Settlements[i].SiteCell, site, size, wrap: true), w.Settlements[i].Id.Value);
        Array.Sort(keys, static (x, y) => x.D != y.D ? x.D.CompareTo(y.D) : x.Id.CompareTo(y.Id));
        var ids = new int[keys.Length];
        for (int i = 0; i < ids.Length; i++) ids[i] = keys[i].Id;
        return ids;
    }

    private static long Stock(IReadOnlyWorldState w, SettlementId s, int good)
    {
        int i = GoodStockIndex.IndexOf(w.GoodStocks, s, new GoodId(good));
        return i < 0 ? 0 : w.GoodStocks[i].Amount.Value;
    }
}
