using Sim.Core.Kernel;
using Sim.Core.Observability;
using Sim.Core.State;
using Sim.Tests.TestUtil;

namespace Sim.Tests.Observability;

/// <summary>
/// §3 — THE STORE-LOSS RESIDUAL, cross-checked against the ledger. StoreLosses
/// = GrainOpening + Harvest − Eaten − GrainClosing absorbs spoilage + granary
/// overflow (recorded per settlement by nothing, §8 gap 2) AND every unrecorded
/// grain transfer (provisions, raids). Because a transfer is zero-sum, the SUM
/// over all records equals Δledger(Spoilage) + Δledger(GranaryOverflow) on every
/// turn — founding turns included once the founding record is counted — and
/// that is what is asserted, to the unit, for 300 turns on two worlds.
///
/// APPROPRIATION is detected HONESTLY: a raid moves grain victim → raider by
/// Ledger.Transfer and no row records it (§8 gap 4), so the raider's residual
/// goes NEGATIVE while the world sum still closes. The turn is then LABELLED as
/// carrying an unrecorded transfer (FlowsSummary.UnattributedGrainTransfer);
/// nothing re-derives the raid rule to assert it did not fire.
/// </summary>
public class StoreLossTests
{
    private static void AssertSumClosesEveryTurn(ObservationLog log, string world, bool foundingExpected)
    {
        int spoiling = 0;
        for (int i = 0; i < log.Observations.Count; i++)
        {
            TurnObservation o = log.Observations[i];
            long losses = 0, harvest = 0, eaten = 0, opening = 0, closing = 0;
            for (int s = 0; s < o.Settlements.Length; s++)
            {
                SettlementRecord r = o.Settlements[s];
                Assert.Equal(Observer.StoreLossesIdentity, r.Food.StoreLossesIdentity);
                losses += r.Food.StoreLosses;
                harvest += r.Food.Harvest;
                eaten += r.Food.Eaten;
                opening += r.Food.GrainOpening;
                closing += r.Food.GrainClosing;
                if (!r.Founded)
                {
                    Assert.True(r.Food.StoreLosses >= 0,
                        $"{world} turn {o.Turn.Turn} settlement {r.Settlement}: negative store loss {r.Food.StoreLosses} with no founding — an unrecorded inbound transfer");
                }
                else Assert.True(foundingExpected, $"{world}: unexpected founding on turn {o.Turn.Turn}");
                if (r.Food.StoreLosses > 0) spoiling++;
            }
            Assert.Equal(o.Turn.Grain.Spoilage + o.Turn.Grain.Overflow, losses);
            Assert.False(o.Turn.Flows.UnattributedGrainTransfer);
            // The READ terms the residual leans on sum to the ledger's legs.
            Assert.Equal(o.Turn.Grain.Harvest, harvest);
            Assert.Equal(o.Turn.Grain.Eaten, eaten);
            Assert.Equal(o.Turn.Grain.Opening, opening);
            Assert.Equal(o.Turn.Grain.Closing, closing);
            // Sustenance is a basket: FoodObtained counts livestock and fish too,
            // and never less than the grain alone.
            for (int s = 0; s < o.Settlements.Length; s++)
            {
                Assert.Equal(3, o.Settlements[s].Food.FoodGoods.Length);
                Assert.True(o.Settlements[s].Food.FoodObtained >= o.Settlements[s].Food.Eaten);
            }
        }
        // MEASURED: 2,895 of 3,600 settlement-turns on the driven world lose
        // grain to the store (the quarry/workshop groups run leaner granaries);
        // the founded world loses on more. The bound guards vacuity, no more.
        Assert.True(spoiling > 2500, $"{world}: only {spoiling} settlement-turns lost anything to the store");
    }

    [Fact]
    public void Founded_SumOfStoreLosses_EqualsLedgerSpoilagePlusOverflow_EveryTurn()
        => AssertSumClosesEveryTurn(ObservedWorlds.Founded300.Value.Log, "founded", foundingExpected: false);

    [Fact]
    public void Driven_SumOfStoreLosses_EqualsLedgerSpoilagePlusOverflow_EveryTurn()
        => AssertSumClosesEveryTurn(ObservedWorlds.Driven300.Value.Log, "driven", foundingExpected: false);

    [Fact]
    public void OnAFoundingTurn_TheSumStillCloses_OnceTheColonyCarriesItsProvisions()
    {
        ObservationLog log = ObservedWorlds.Founding5.Value.Log;
        TurnObservation o = log.At(2)!;
        long losses = 0, prevOnly = 0;
        for (int s = 0; s < o.Settlements.Length; s++)
        {
            losses += o.Settlements[s].Food.StoreLosses;
            if (!o.Settlements[s].Founded) prevOnly += o.Settlements[s].Food.StoreLosses;
        }
        long provisions = o.Settlements[12].Food.GrainClosing;
        Assert.True(provisions > 0);
        Assert.Equal(o.Turn.Grain.Spoilage + o.Turn.Grain.Overflow, losses);
        // Without the founding record the prev settlements over-count by exactly
        // the provisions that left the source — which is why the founding record
        // carries them as a negative loss.
        Assert.Equal(o.Turn.Grain.Spoilage + o.Turn.Grain.Overflow + provisions, prevOnly);
        Assert.True(o.Turn.Grain.Spoilage > 0, "no spoilage on the founding turn");
        Assert.False(o.Turn.Flows.UnattributedGrainTransfer);
        for (long turn = 3; turn <= 6; turn++)
        {
            TurnObservation later = log.At(turn)!;
            long sum = 0;
            for (int s = 0; s < later.Settlements.Length; s++) sum += later.Settlements[s].Food.StoreLosses;
            Assert.Equal(later.Turn.Grain.Spoilage + later.Turn.Grain.Overflow, sum);
        }
    }

    [Fact]
    public void AnUnrecordedGrainTransfer_LabelsTheTurn_WhileTheWorldSumStillCloses()
    {
        // TEETH for the appropriation detector: move 50 grain from settlement 1
        // to settlement 0 by Ledger.Transfer — exactly what AppropriationSystem
        // does, and what no row records. The recipient's residual goes negative,
        // the turn is labelled, and the world-level sum is unmoved.
        WorldState prev = ObservedWorlds.Founded();
        WorldState next = prev.Clone();
        next.Clock = new SimClock(prev.Clock.Turn + 1, prev.Clock.SimDays + 3600, 3600);
        var grain = new GoodId(TestConfigs.Sim().Goods!.GrainId);
        int from = GoodStockIndex.IndexOf(next.GoodStocks, next.Settlements[1].Id, grain);
        int to = GoodStockIndex.IndexOf(next.GoodStocks, next.Settlements[0].Id, grain);
        new Ledger(next.LedgerFlows).Transfer(
            ref next.GoodStocks.Ref(from).Amount, ref next.GoodStocks.Ref(to).Amount, 50, OverdrawPolicy.Throw);

        TurnObservation o = Observer.Observe(prev, next, TestConfigs.Sim(), []);

        Assert.Equal(-50, o.Settlements[0].Food.StoreLosses);
        Assert.Equal(50, o.Settlements[1].Food.StoreLosses);
        Assert.True(o.Turn.Flows.UnattributedGrainTransfer);
        Assert.True(o.Turn.Grain.Reconciles);
        long sum = 0;
        for (int s = 0; s < o.Settlements.Length; s++) sum += o.Settlements[s].Food.StoreLosses;
        Assert.Equal(0, sum);   // no spoilage, no overflow, no rows: the transfer is zero-sum
    }
}
