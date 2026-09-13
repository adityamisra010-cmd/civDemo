using Sim.Core;
using Sim.Core.Observability;
using Sim.Core.State;
using Sim.Core.Systems;
using Sim.Tests.TestUtil;

namespace Sim.Tests.Observability;

/// <summary>
/// T4.20 — THE TWO FOOD-LEGIBILITY DERIVATIONS, against §0 of
/// docs/observability-architecture.md. <c>FoodProduced</c> is SUMMED (an
/// integer sum of READ <c>LastProducedUnits</c> rows) and <c>FoodBalance</c> is
/// DIFFERENCED (<c>FoodProduced − DemandUnits</c>, both READ/SUMMED longs).
/// Neither may be a second implementation of anything, so the tests here prove
/// three things and not merely that the arithmetic closes:
///
///   1. the identity holds EXACTLY, no epsilon, on every settlement-turn of two
///      populated multi-settlement 300-turn worlds;
///   2. FoodProduced is ONE definition, not two: it equals the sum over the
///      food goods AND it is the same numerator ClassMobilitySystem forms for
///      the published food-surplus ratio — checked through that published
///      variable, which also pins the ONE-TURN LAG the UI must label;
///   3. the observer READS: change a simulation row and the record follows,
///      unit for unit, with no re-derivation of its own.
/// </summary>
public class FoodFlowTests
{
    private static void AssertBalanceIdentity(ObservationLog log, string world)
    {
        int surplus = 0, deficit = 0, multiGood = 0;
        for (int i = 0; i < log.Observations.Count; i++)
        {
            TurnObservation o = log.Observations[i];
            for (int s = 0; s < o.Settlements.Length; s++)
            {
                FoodSection f = o.Settlements[s].Food;

                // DIFFERENCED — exact long arithmetic, no epsilon.
                Assert.Equal(f.FoodProduced - f.DemandUnits, f.FoodBalance);

                // SUMMED — over the food goods the record itself carries.
                long sum = 0;
                int nonZero = 0;
                for (int g = 0; g < f.FoodGoods.Length; g++)
                {
                    sum += f.FoodGoods[g].Produced;
                    if (f.FoodGoods[g].Produced > 0) nonZero++;
                }
                Assert.Equal(sum, f.FoodProduced);
                if (nonZero > 1) multiGood++;
                if (f.FoodBalance > 0) surplus++;
                else if (f.FoodBalance < 0) deficit++;
            }
        }
        Assert.True(surplus > 100, $"{world}: only {surplus} surplus settlement-turns");
        // MEASURED on this tree: founded-300 carries 32 deficit settlement-turns,
        // driven-300 many more. The floor is set to the smaller measured figure so
        // the test stays non-vacuous on both arms without pinning either.
        Assert.True(deficit > 20, $"{world}: only {deficit} deficit settlement-turns");
        Assert.True(multiGood > 100, $"{world}: only {multiGood} settlement-turns produced more than one food good");
    }

    [Fact]
    public void Founded300_BalanceIsExactlyProducedMinusRequired()
        => AssertBalanceIdentity(ObservedWorlds.Founded300.Value.Log, "founded");

    [Fact]
    public void Driven300_BalanceIsExactlyProducedMinusRequired()
        => AssertBalanceIdentity(ObservedWorlds.Driven300.Value.Log, "driven");

    /// <summary>ONE DEFINITION. ClassMobilitySystem publishes
    /// food_surplus_ratio = (Σ over food goods of PREV LastProducedUnits) /
    /// PREV DemandUnits (ClassMobility/ClassMobilitySystem.cs:131-135). Those
    /// two prev-world quantities are exactly the PREVIOUS record's FoodProduced
    /// and DemandUnits. So the ratio the simulation published on turn t must
    /// reproduce, bit for bit as a double, from the record of turn t−1 — which
    /// proves both that the observer's numerator is the system's numerator and
    /// that the ratio is ONE TURN BEHIND the same record's FoodBalance.</summary>
    private static void AssertRatioIsLastTurnsProducedOverRequired(ObservationLog log, string world)
    {
        int checkedTurns = 0, disagreeWithSameTurn = 0;
        for (int i = 1; i < log.Observations.Count; i++)
        {
            TurnObservation prevObs = log.Observations[i - 1];
            TurnObservation obs = log.Observations[i];
            for (int s = 0; s < obs.Settlements.Length; s++)
            {
                SettlementRecord r = obs.Settlements[s];
                SettlementRecord? p = null;
                for (int q = 0; q < prevObs.Settlements.Length; q++)
                    if (prevObs.Settlements[q].Settlement == r.Settlement) { p = prevObs.Settlements[q]; break; }
                if (p is null) continue;
                double ratio = r.Economy.FoodSurplusRatio;
                if (double.IsNaN(ratio)) continue;
                if (p.Food.DemandUnits <= 0) continue;

                Assert.Equal(p.Food.FoodProduced / (double)p.Food.DemandUnits, ratio);
                checkedTurns++;

                // The SAME turn's ratio would be a different number: that is the
                // lag the UI has to label, measured rather than asserted.
                if (r.Food.DemandUnits > 0
                    && r.Food.FoodProduced / (double)r.Food.DemandUnits != ratio) disagreeWithSameTurn++;
            }
        }
        Assert.True(checkedTurns > 1000, $"{world}: only {checkedTurns} settlement-turns compared");
        Assert.True(disagreeWithSameTurn > 100,
            $"{world}: the lag was invisible - only {disagreeWithSameTurn} settlement-turns differ from the same-turn ratio");
    }

    [Fact]
    public void Founded300_FoodProducedIsClassMobilitysOwnNumerator()
        => AssertRatioIsLastTurnsProducedOverRequired(ObservedWorlds.Founded300.Value.Log, "founded");

    [Fact]
    public void Driven300_FoodProducedIsClassMobilitysOwnNumerator()
        => AssertRatioIsLastTurnsProducedOverRequired(ObservedWorlds.Driven300.Value.Log, "driven");

    /// <summary>THE OBSERVER READS. Observe one step, then change the very rows
    /// the record claims to copy — a food good's LastProducedUnits and the
    /// settlement's DemandUnits — and observe the SAME pair again. FoodProduced
    /// moves by exactly the injected delta and FoodBalance by the difference of
    /// the two deltas. A recomputing observer (one that re-derived production
    /// from sectors, labour or stocks) could not follow a row edit this way.</summary>
    [Fact]
    public void Observer_ReadsTheRows_SoAnEditToThemMovesTheRecordExactly()
    {
        SimConfig cfg = TestConfigs.Sim();
        WorldState prev = ObservedWorlds.Founded();
        WorldState next = ObservedWorlds.Executor(cfg, null).Step(prev);
        SettlementId id = next.Settlements[0].Id;

        SettlementRecord before = Find(Observer.Observe(prev, next, cfg, []), id);

        const long producedDelta = 777;
        const long demandDelta = 250;
        int edited = -1;
        for (int i = 0; i < next.GoodStocks.Count; i++)
        {
            if (next.GoodStocks[i].Settlement != id) continue;
            for (int g = 0; g < before.Food.FoodGoods.Length; g++)
            {
                if (next.GoodStocks[i].Good.Value != before.Food.FoodGoods[g].Good) continue;
                next.GoodStocks[i] = next.GoodStocks[i] with
                {
                    LastProducedUnits = next.GoodStocks[i].LastProducedUnits + producedDelta,
                };
                edited = i;
                break;
            }
            if (edited >= 0) break;
        }
        Assert.True(edited >= 0, "no food good row for settlement 0");
        for (int i = 0; i < next.ConsumptionDeficits.Count; i++)
            if (next.ConsumptionDeficits[i].Settlement == id)
            {
                next.ConsumptionDeficits[i] = new ConsumptionDeficitRow(
                    id, next.ConsumptionDeficits[i].DeficitRatio,
                    next.ConsumptionDeficits[i].DemandUnits + demandDelta);
                break;
            }

        SettlementRecord after = Find(Observer.Observe(prev, next, cfg, []), id);
        Assert.Equal(before.Food.FoodProduced + producedDelta, after.Food.FoodProduced);
        Assert.Equal(before.Food.DemandUnits + demandDelta, after.Food.DemandUnits);
        Assert.Equal(before.Food.FoodBalance + producedDelta - demandDelta, after.Food.FoodBalance);
        Assert.Equal(after.Food.FoodProduced - after.Food.DemandUnits, after.Food.FoodBalance);
    }

    /// <summary>EMPTY SETTLEMENTS ARE NOT INVALID NUMBERS. The founding rig
    /// strands every settlement but one with zero stocks and zero last harvest,
    /// and the colony founded on the first step has NO row of any kind (no
    /// stock, no deficit row, no population on prev). Every such record must
    /// read 0 — a long, always finite — and the identity must still close.</summary>
    [Fact]
    public void ZeroProductionAndNoRowSettlements_ReadZero_NotGarbage()
    {
        ObservationLog log = ObservedWorlds.Founding5.Value.Log;
        int founding = 0, strandedZero = 0;
        for (int i = 0; i < log.Observations.Count; i++)
        {
            TurnObservation o = log.Observations[i];
            for (int s = 0; s < o.Settlements.Length; s++)
            {
                SettlementRecord r = o.Settlements[s];
                FoodSection f = r.Food;
                Assert.Equal(f.FoodProduced - f.DemandUnits, f.FoodBalance);
                Assert.True(f.FoodProduced >= 0);
                if (f.FoodProduced == 0) strandedZero++;
                if (r.Founded)
                {
                    founding++;
                    Assert.Equal(0, r.Population.Opening);
                    Assert.Equal(0, f.FoodProduced);
                    Assert.Equal(0, f.DemandUnits);
                    Assert.Equal(0, f.FoodBalance);
                    // The food-good array is built from the GOODS REGISTRY, not
                    // from the stock table, so a settlement with no row still
                    // carries one entry per food good - reading 0, never absent
                    // and never invalid.
                    for (int g = 0; g < f.FoodGoods.Length; g++)
                    {
                        Assert.Equal(0, f.FoodGoods[g].Produced);
                        Assert.Equal(0, f.FoodGoods[g].Eaten);
                    }
                }
            }
        }
        Assert.True(founding >= 1, "the founding rig founded nothing");
        Assert.True(strandedZero > 0, "no settlement produced zero food - the zero case was never exercised");
        // MEASURED: in this rig the zero-production records are the founding ones
        // (no rows of any kind on prev); every stranded settlement still produces
        // during the step even with its stocks emptied.
    }

    private static SettlementRecord Find(TurnObservation o, SettlementId id)
    {
        for (int i = 0; i < o.Settlements.Length; i++)
            if (o.Settlements[i].Settlement == id.Value) return o.Settlements[i];
        throw new InvalidOperationException("no record for settlement " + id.Value.ToString(System.Globalization.CultureInfo.InvariantCulture));
    }
}
