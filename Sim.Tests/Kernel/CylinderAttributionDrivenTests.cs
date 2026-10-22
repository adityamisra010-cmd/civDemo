using Sim.Core.Kernel;
using Sim.Core.State;
using Sim.Core.Systems;
using Sim.Tests.TestUtil;
using R = Sim.Tests.TestUtil.CylinderAttributionRig;

namespace Sim.Tests.Kernel;

/// <summary>
/// W1 F7 (batch 4; D-055, 2026-10-16; design §2 rigs #1 (canonical half) and #9) — INTRA-POLITY GRAIN RELIEF (D-053 §2) and
/// R2a's TRADE KNOWLEDGE gate, attributed turn-exactly on the SHIPPED CYLINDER: the canonical driven world (seed 42, 1024 px,
/// the driven golden's turn-2 SectorAllocation batch), founded and stepped by both arms on worldgen.json as shipped
/// (topology.wrapX). ON = the shipped config; OFF = the single-layer twin (<see cref="TestConfigs.PreEmpireFoodAllocation"/>,
/// which keeps every other layer on) or the NON-chained <see cref="SingleSwitchTwins.PreTradeKnowledgeOnly"/>. Each first
/// action is read off the ON (relief) or the ungated (trade) run's own state, never off a hash: relief's from the grain
/// books (<see cref="R.ReliefMovedGrain"/>), trade's from the first TradeFlows row. The flat per-layer leg
/// (EmpireFoodAllocationTests.PerLayer_Relief_…, founded flat with WS1/WS2 off) and the R2a literal controls
/// (IntegratedPinAttributionTests) are unchanged.
/// SEAM EXPOSURE (MEASURED, recorded in D-055): on this world no pair of the player's settlements is nearer the short way
/// round the seam (0 seam-crossing pairs) and no settlement's nearest-first donor order changes if distances were read flat
/// (0) — so relief's donor order, the one distance relief reads, is not exercised across the seam here, and a
/// topology-blind (raw dx) relief passes this rig (T2 step 2, MEASURED); relief's seam half is
/// CylinderAttributionSeamTests.Relief_… (SeamWorld) and trade's is CylinderAttributionSeamTests.TradeKnowledge_… (SeamWorld).
/// </summary>
public class CylinderAttributionDrivenTests : IClassFixture<CylinderAttributionDrivenTests.Arms>
{
    // ---------------------------------------------------------------------------------------------------------------
    // MEASURED (2026-10-16, D-055; batch 4 track T2 step 1) on base-b3 + the frozen T1a patch (pinned tree 48e7c13b…,
    // patch sha256 01179819…), Release, by the agent writing this block (probe log t2-out/probe-measure.txt):
    //   relief: the ON run's grain books first show relief in the step 4 → 5 (world 5; 5 relief steps in 10 turns), the
    //   relief-off run never; first different world 5; write set {ConsumptionDeficits, GoodStocks, LedgerFlows}.
    //   trade knowledge: the ungated run's first TradeFlows row is in world 6 (the gated ON run has none in 10 turns);
    //   first different world 6 (world 5 identical); write set {GoodStocks, TradeFlows}.
    //   seam exposure at world 0: 0 seam-crossing pairs, 0 wrapped donor orders.
    // These equal the batch-3 recon's cylinder values (f7-flat-only §5: relief 5, trade 6): unchanged by T1a.
    // ---------------------------------------------------------------------------------------------------------------
    private const int FirstRelief = 5;
    private const int FirstTradeFlow = 6;
    private const int ReliefHorizon = FirstRelief + 2;
    private const int TradeHorizon = FirstTradeFlow + 2;
    private const int SeamCrossingPairs = 0;
    private const int WrappedDonorOrders = 0;
    private static readonly string[] ReliefWrites = ["ConsumptionDeficits", "GoodStocks", "LedgerFlows"];
    private static readonly string[] TradeWrites = ["GoodStocks", "TradeFlows"];

    /// <summary>The shared ON arm (the shipped config, to the longer horizon) and a second, independent ON run.</summary>
    public sealed class Arms
    {
        public SimConfig On { get; } = TestConfigs.PreDiscovery(TestConfigs.Sim());
        public R.Arm Live { get; }
        public R.Arm Again { get; }

        public Arms()
        {
            Live = R.Run(R.Driven1024(), "on", On, TradeHorizon, keep: [0, FirstRelief, FirstTradeFlow],
                probes: [R.ReliefMovedGrain, R.Trades]);
            Again = R.Run(R.Driven1024(), "on-again", TestConfigs.PreDiscovery(TestConfigs.Sim()), TradeHorizon);
        }
    }

    private readonly Arms _arms;

    public CylinderAttributionDrivenTests(Arms arms) => _arms = arms;

    /// <summary>#1 (canonical half) — relief. ON and relief-off are the same world up to the first step whose grain books
    /// show relief, and differ from that very world on.</summary>
    [Fact]
    public void Relief_OnTheShippedDrivenCylinder_DivergesExactlyAtTheFirstReliefTransfer()
    {
        SimConfig on = _arms.On, off = TestConfigs.PreEmpireFoodAllocation(on);
        Assert.True(on.Consumption.EmpireFoodAllocation);
        Assert.False(off.Consumption.EmpireFoodAllocation);
        R.Arm live = _arms.Live;
        R.Arm twin = R.Run(R.Driven1024(), "relief-off", off, ReliefHorizon, keep: [FirstRelief], probes: [R.ReliefMovedGrain]);

        R.AssertWorld0Identical(live, twin);
        Assert.Equal(FirstRelief, live.FirstHeld(R.ReliefMovedGrain.Name));     // the independent first action (vacuity: > 0)
        Assert.Equal(-1L, twin.FirstHeld(R.ReliefMovedGrain.Name));             // relief off: no settlement eats grain it lacks
        Assert.Equal(FirstRelief, R.FirstDifferent(live, twin));
        string[] written = R.WriteSet(live.At(FirstRelief), twin.At(FirstRelief));
        Assert.Equal(ReliefWrites, written);
        R.AssertNoTopologyTable(written);
        R.AssertSameSeries(live, _arms.Again);

        WorldState w0 = live.At(0);
        Assert.Equal((SeamCrossingPairs, WrappedDonorOrders), (R.SeamCrossingPairs(w0), R.WrappedDonorOrders(w0)));
    }

    /// <summary>#9 — R2a's trade-knowledge gate. ON (gated: the player's polity does not know Trade) never trades within the
    /// horizon; the ungated twin's first trade flow is the first differing world.</summary>
    [Fact]
    public void TradeKnowledge_OnTheShippedDrivenCylinder_DivergesExactlyAtTheUngatedArmsFirstTradeFlow()
    {
        SimConfig on = _arms.On, off = SingleSwitchTwins.PreTradeKnowledgeOnly(on);
        Assert.NotNull(on.Trade.Entity);
        Assert.Null(off.Trade.Entity);
        R.Arm live = _arms.Live;
        R.Arm twin = R.Run(R.Driven1024(), "trade-knowledge-off", off, TradeHorizon, keep: [FirstTradeFlow], probes: [R.Trades]);

        R.AssertWorld0Identical(live, twin);
        Assert.Equal(FirstTradeFlow, twin.FirstHeld(R.Trades.Name));    // the independent first action (vacuity: > 0)
        Assert.Equal(-1L, live.FirstHeld(R.Trades.Name));               // gated: no flow in the ON run's horizon
        Assert.Equal(0, live.At(FirstTradeFlow).TradeFlows.Count);
        Assert.Equal(FirstTradeFlow, R.FirstDifferent(live, twin));
        string[] written = R.WriteSet(live.At(FirstTradeFlow), twin.At(FirstTradeFlow));
        Assert.Equal(TradeWrites, written);
        R.AssertNoTopologyTable(written);
        R.AssertSameSeries(live, _arms.Again);
    }
}
