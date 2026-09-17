using Sim.Core.Observability.Explain;
using Sim.Core.State;
using Sim.Core.Systems;
using Sim.Core.Systems.Consumption;
using Sim.Core.Systems.NeedsGrievance;

namespace Sim.Tests.Observability;

/// <summary>
/// T4.19 A2-FIX — the public pure functions the explain queries RECOMPUTE
/// through are the SYSTEM'S: NeedsGrievanceSystem.Step calls exactly these on
/// what it reads, and the observer calls the same ones on the same reads. The
/// extraction changed no behaviour (measured: the 50-turn founded hash at seed
/// 42 is identical before and after; every golden, snapshot and pin passes
/// unmoved), so what these tests pin is the SEMANTICS of each function — the
/// property the system ought to have, not the fact that two copies agree.
///
/// KILL RECORD (D2). The verifier's mutant — the chain's COPY of Fill, case 3
/// replaced by 1.0 — survived the whole first-cut suite because nothing tied
/// that copy to the system. That mutant is no longer expressible: the chain
/// has no copy, it calls NeedsGrievanceSystem.Fill on the row it cites. The
/// equivalent mutation is therefore the system's own case 3 → 1.0, applied in
/// a separate tree built from this commit and run against the Observability +
/// NeedsGrievanceTests filter (41 tests, 3 s clean, bounded at 15 min):
/// 6 fail — <see cref="Fill_ThreeCases_OnRealRows_AndTheQuotientIsPinned"/>
/// (0.4 expected, 1.0 returned), <see cref="ChainFill_IsTheSystemsFill_OnTheStarvedWorld"/>
/// (the grain fill 436/3078 is asserted against the quotient of the two READ
/// links beside it), ExplainGrievanceTests.Starved_PrimaryIsSustenance_AndChainShowsHarvestBelowEaten
/// (Sustenance satisfaction no longer below the gate floor), and three
/// pre-existing NeedsGrievanceTests (Satisfaction_ClampsAtBothEnds,
/// AntiTautology_SatisfactionRESPONDSToTheStandard_PerturbedEitherWay,
/// Famine_RaisesGrievance_InTheStarvationWindow). Dead on semantic teeth in
/// both the observer and the system, not on a golden.
/// </summary>
public class ExplainRecomputedFunctionTests
{
    private const int Peasant = 1;
    private static readonly SettlementId Target = new(ExplainRigs.Target);

    [Fact]
    public void Fill_ThreeCases_OnRealRows_AndTheQuotientIsPinned()
    {
        (SimConfig cfg, List<WorldState> worlds) = ExplainRigs.Fed(1);
        WorldState w = worlds[1];
        var grain = new GoodId(cfg.Goods!.GrainId);
        int g = GoodStockIndex.IndexOf(w.GoodStocks, Target, grain);
        Assert.True(g >= 0 && w.GoodStocks[g].Amount.Value > 0, "rig: settlement 0 holds no grain after one turn");
        GoodStockRow stocked = w.GoodStocks[g];

        // Case 3 — positive demand: eaten / demanded, clamped. The quotient is
        // the definition, pinned on four points including both clamp ends.
        Assert.Equal(0.4, NeedsGrievanceSystem.Fill(stocked with { LastConsumptionDemandUnits = 10, LastConsumptionEatenUnits = 4 }));
        Assert.Equal(0.0, NeedsGrievanceSystem.Fill(stocked with { LastConsumptionDemandUnits = 10, LastConsumptionEatenUnits = 0 }));
        Assert.Equal(1.0, NeedsGrievanceSystem.Fill(stocked with { LastConsumptionDemandUnits = 10, LastConsumptionEatenUnits = 10 }));
        Assert.Equal(1.0, NeedsGrievanceSystem.Fill(stocked with { LastConsumptionDemandUnits = 10, LastConsumptionEatenUnits = 15 }));
        Assert.Equal(436.0 / 3078.0, NeedsGrievanceSystem.Fill(stocked with { LastConsumptionDemandUnits = 3078, LastConsumptionEatenUnits = 436 }));

        // Case 2 — demand quantised to zero: the STOCK discriminates.
        Assert.Equal(1.0, NeedsGrievanceSystem.Fill(stocked with { LastConsumptionDemandUnits = 0, LastConsumptionEatenUnits = 0 }));
        var empty = new GoodStockRow(Target, grain, Conserved.Zero, 0.0, 0.0, lastConsumptionDemandUnits: 0);
        Assert.Equal(0L, Conserved.Zero.Value);
        Assert.Equal(0.0, NeedsGrievanceSystem.Fill(in empty));
        Assert.Equal(0.0, NeedsGrievanceSystem.Fill(empty with { LastConsumptionDemandUnits = -1 }));

        // Case 1 — no row at all: 1.0 through the world overload; a row that
        // exists routes to the row overload (same value, same row).
        var absent = new GoodId(int.MaxValue);
        Assert.Equal(-1, GoodStockIndex.IndexOf(w.GoodStocks, Target, absent));
        Assert.Equal(1.0, NeedsGrievanceSystem.Fill(w, Target, absent));
        Assert.Equal(NeedsGrievanceSystem.Fill(in stocked), NeedsGrievanceSystem.Fill(w, Target, grain));
    }

    /// <summary>The fill the chain shows IS NeedsGrievanceSystem.Fill on the row
    /// it cites, on a real stepped world where fills are strictly inside (0, 1)
    /// — and equals the quotient of the two READ links beside it, so a Fill that
    /// stops dividing is caught here even though the chain calls it.</summary>
    [Fact]
    public void ChainFill_IsTheSystemsFill_OnTheStarvedWorld()
    {
        (SimConfig cfg, List<WorldState> worlds, int first) = ExplainRigs.Starved(maxTurns: 12);
        Assert.Equal(3, first);
        WorldState prev = worlds[first], next = worlds[first + 1];
        var grain = new GoodId(cfg.Goods!.GrainId);

        int fills = 0, interior = 0;
        void Check(CausalChain chain, ChainNode fillNode, ChainNode eatenNode, ChainNode demandedNode)
        {
            foreach (Link fill in chain.Links)
            {
                if (fill.Node != fillNode) continue;
                fills++;
                Assert.Equal(LinkKind.Recomputed, fill.Kind);
                Assert.Contains("NeedsGrievanceSystem.Fill", fill.Note);
                if (fill.SourceIndex < 0) { Assert.Contains("No row", fill.Note); Assert.Equal(1.0, fill.Value); continue; }
                GoodStockRow row = prev.GoodStocks[fill.SourceIndex];
                Assert.Equal(NeedsGrievanceSystem.Fill(in row), fill.Value);
                Assert.Equal(NeedsGrievanceSystem.Fill(prev, Target, row.Good), fill.Value);
                // The READ pair beside it, by label prefix (the good's name), and their quotient.
                string good = fill.Label[..fill.Label.IndexOf(" fill", StringComparison.Ordinal)];
                Link eaten = Labelled(chain.Links, eatenNode, good + " eaten");
                Link demanded = Labelled(chain.Links, demandedNode, good + " demanded");
                Assert.Equal(fill.SourceIndex, eaten.SourceIndex);
                Assert.Equal(fill.SourceIndex, demanded.SourceIndex);
                if (demanded.Value > 0.0)
                {
                    Assert.Equal(Math.Clamp(eaten.Value / demanded.Value, 0.0, 1.0), fill.Value);
                    if (fill.Value > 0.0 && fill.Value < 1.0) interior++;
                }
            }
        }

        CausalChain sustenance = CausalChain.ForNeed(prev, next, cfg, Target, new ClassId(Peasant), BasketBook.SustenanceNeedId);
        Check(sustenance, ChainNode.FoodGoodFill, ChainNode.FoodGoodEaten, ChainNode.FoodGoodDemanded);
        CausalChain comfort = CausalChain.ForNeed(prev, next, cfg, Target, new ClassId(Peasant), 6);
        Check(comfort, ChainNode.ComfortGoodFill, ChainNode.ComfortGoodEaten, ChainNode.ComfortGoodDemanded);

        Assert.True(fills >= 5, $"vacuous: only {fills} fill links (grain, livestock, fish, pottery, cloth expected)");
        Assert.True(interior > 0, "vacuous: no fill was strictly between 0 and 1 on the drawdown turn");

        // The grain fill on the drawdown turn, measured: 531 eaten of 3790 demanded.
        // (T4.19 lane C re-pin from 436 / 3078: the founding cohort vector moved
        // the rig's founded population; T4.21-3 re-pin 3937 -> 3790 demanded, 531
        // eaten unchanged: the turn-2 headroom hold every founded world takes
        // leaves a smaller population demanding at turn 3, while what was eaten
        // is the store's whole content, the same endowment. The fill identity
        // below is what is asserted, the literals only say which world it was
        // measured on.)
        int g = GoodStockIndex.IndexOf(prev.GoodStocks, Target, grain);
        Assert.Equal(531, prev.GoodStocks[g].LastConsumptionEatenUnits);
        Assert.Equal(3790, prev.GoodStocks[g].LastConsumptionDemandUnits);
        Link grainFill = Labelled(sustenance.Links, ChainNode.FoodGoodFill, ExplainRowsName(cfg, grain) + " fill");
        Assert.Equal(531.0 / 3790.0, grainFill.Value);
        // ...and the satisfaction the SYSTEM published from that fill is below 1.
        Link s = ExplainGrievanceTests.Single(sustenance.Links, ChainNode.SustenanceSatisfaction);
        Assert.Equal(LinkKind.Read, s.Kind);
        Assert.True(s.Value < 0.5, $"Sustenance satisfaction {s.Value} on a 13% grain fill");
    }

    [Fact]
    public void IsTierAGate_MarksExactlySustenanceShelterSafety_AndTheObserverAgrees()
    {
        for (int id = -1; id <= 16; id++)
            Assert.Equal(id is 1 or 2 or 3, NeedsGrievanceSystem.IsTierAGate(id));

        (SimConfig cfg, List<WorldState> worlds) = ExplainRigs.Fed(2);
        GrievanceExplanation g = GrievanceExplanation.For(worlds[1], worlds[2], cfg, Target, new ClassId(Peasant));
        int gates = 0;
        foreach (NeedComponent n in g.Needs)
        {
            Assert.Equal(NeedsGrievanceSystem.IsTierAGate(n.NeedId), n.IsTierAGate);
            if (n.IsTierAGate) gates++;
        }
        Assert.Equal(3, gates);   // Sustenance, Shelter, Safety are all in the registry
    }

    /// <summary>Each arithmetic function pinned on hand values with exact
    /// equality — the expression it replaced, operand for operand.</summary>
    [Fact]
    public void GrievanceArithmetic_PinnedOnHandValues()
    {
        // turnover: (births + deaths) / pop / rowDt; a dt ≤ 0 row reads 0
        Assert.Equal(7 / 100.0 / 10.0, NeedsGrievanceSystem.TurnoverPerYear(3, 4, 100, 10.0));
        Assert.Equal(0.0, NeedsGrievanceSystem.TurnoverPerYear(3, 4, 100, 0.0));
        Assert.Equal(0.0, NeedsGrievanceSystem.TurnoverPerYear(3, 4, 100, -1.0));

        var tuning = new GrievanceTuning(BaseDecayPerYear: 0.01, InheritFraction: 0.6);
        Assert.Equal(0.01 + (1.0 - 0.6) * 0.1, NeedsGrievanceSystem.DecayRatePerYear(tuning, 0.1));
        Assert.Equal(0.01, NeedsGrievanceSystem.DecayRatePerYear(tuning, 0.0));

        // accrual: W × max(0, 1 − S); never negative above the expectation
        Assert.Equal(2.2 * (1.0 - 0.3), NeedsGrievanceSystem.AccrualPerYear(2.2, 0.3));
        Assert.Equal(0.0, NeedsGrievanceSystem.AccrualPerYear(2.2, 1.0));
        Assert.Equal(0.0, NeedsGrievanceSystem.AccrualPerYear(2.2, 1.5));

        // no bound need: the aggregate is the expectation and the accrual is exactly 0
        var agg = new AggregationTuning(Sigma: 0.5, SatisfactionFloor: 0.05, TierAFloor: 0.5, TierAGain: 2.0, TierACollapse: 1.0);
        double none = NeedsGrievanceSystem.AggregateSatisfaction([], [], [], agg, []);
        Assert.Equal(1.0, none);
        Assert.Equal(0.0, NeedsGrievanceSystem.AccrualPerYear(0.0, none));
        // one fully met need aggregates to 1 (no shortfall); one need at 0.5 is below 1
        double[] scratch = new double[1];
        Assert.Equal(1.0, NeedsGrievanceSystem.AggregateSatisfaction([1.0], [false], [1.0], agg, scratch));
        Assert.True(NeedsGrievanceSystem.AggregateSatisfaction([0.5], [false], [1.0], agg, scratch) < 1.0);

        // the Euler step: (prev + a·dt) − (d·prev)·dt, floored at 0
        Assert.Equal(1.5 * 10.0, NeedsGrievanceSystem.TurnAccrual(1.5, 10.0));
        Assert.Equal(0.02 * 10.0 * 10.0, NeedsGrievanceSystem.TurnDecay(0.02, 10.0, 10.0));
        Assert.Equal(Math.Max(0.0, 10.0 + 1.5 * 10.0 - 0.02 * 10.0 * 10.0), NeedsGrievanceSystem.StepGrievance(10.0, 1.5, 0.02, 10.0));
        Assert.Equal(0.0, NeedsGrievanceSystem.StepGrievance(10.0, 0.0, 0.5, 10.0));   // decay×dt = 5 > 1: bottoms out, never negative
        Assert.Equal(10.0, NeedsGrievanceSystem.StepGrievance(10.0, 0.0, 0.0, 10.0));  // nothing moves it
    }

    private static Link Labelled(Link[] links, ChainNode node, string label)
    {
        for (int i = 0; i < links.Length; i++)
            if (links[i].Node == node && string.Equals(links[i].Label, label, StringComparison.Ordinal)) return links[i];
        Assert.Fail($"no {node} link labelled '{label}'");
        return default;
    }

    private static string ExplainRowsName(SimConfig cfg, GoodId good)
    {
        foreach (GoodEntry e in cfg.Goods!.Goods) if (e.Id == good.Value) return e.Name;
        return "good " + good.Value.ToString(System.Globalization.CultureInfo.InvariantCulture);
    }
}
