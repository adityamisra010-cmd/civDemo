using System.IO;
using Sim.Core.Kernel;
using Sim.Core.State;
using Xunit;

namespace Sim.Tests.Kernel;

/// <summary>
/// T4.13 — the AGGREGATE law-1 path must span household goods, not just the
/// per-quantity one. Independent review found stage 1 had extended
/// <see cref="ConservationAuditor.AuditQuantity"/> only, leaving the
/// <see cref="ConservationAuditor.IsConserved"/> conjunction — the one every
/// harness actually gates on — blind to the new stock. This test is the guard
/// for that hole, and it is written to FAIL if the conjunction ever drops the
/// term again: it plants units with NO matching Ledger flow and requires the
/// AGGREGATE to report the violation.
/// </summary>
public class HouseholdGoodsAuditTests
{
    [Fact]
    public void UnledgeredHouseholdGoods_FailTheAGGREGATEAudit_NotOnlyThePerQuantityOne()
    {
        var world = new WorldState();
        // Units that never passed through the WORLD's ledger: the flow is written
        // to a throwaway table, so the stock rises while world.LedgerFlows stays
        // empty — precisely the shape a system that mutates a stock outside the
        // Ledger would produce.
        int planted = world.HouseholdGoods.Add(new HouseholdGoodsRow(
            new SettlementId(0), Conserved.Zero, 0.0, 0.0));
        var shadow = new Ledger(new Table<LedgerFlowRow>());
        shadow.Flow(ref world.HouseholdGoods.Ref(planted).Units,
            ConservedQuantityIds.HouseholdGoods, ReasonIds.HouseholdGoodsCrafted,
            5, FlowDirection.Source, OverdrawPolicy.Throw);
        Assert.Equal(0, world.LedgerFlows.Count);

        // The per-quantity audit sees it...
        Assert.False(ConservationAuditor
            .AuditQuantity(world, ConservedQuantityIds.HouseholdGoods).IsConserved);

        // ...and so must the aggregate, which is what the harnesses gate on.
        Assert.False(ConservationAuditor.IsConserved(world, out string report),
            "the AGGREGATE audit missed unledgered household goods — the hole "
            + "independent review found in T4.13 stage 1 has reopened");
        Assert.Contains("householdGoods", report);
    }

    [Fact]
    public void LedgeredHouseholdGoods_Balance_Exactly()
    {
        var world = new WorldState();
        var ledger = new Ledger(world.LedgerFlows);
        int row = world.HouseholdGoods.Add(new HouseholdGoodsRow(
            new SettlementId(0), Conserved.Zero, 0.0, 0.0));
        ledger.Flow(ref world.HouseholdGoods.Ref(row).Units,
            ConservedQuantityIds.HouseholdGoods, ReasonIds.HouseholdGoodsCrafted,
            7, FlowDirection.Source, OverdrawPolicy.Throw);
        ledger.Flow(ref world.HouseholdGoods.Ref(row).Units,
            ConservedQuantityIds.HouseholdGoods, ReasonIds.HouseholdGoodsWorn,
            3, FlowDirection.Sink, OverdrawPolicy.Throw);

        Assert.Equal(4, world.HouseholdGoods[0].Units.Value);
        Assert.True(ConservationAuditor.IsConserved(world, out string report), report);
    }
}
