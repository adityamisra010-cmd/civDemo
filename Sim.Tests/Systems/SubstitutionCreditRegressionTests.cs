using Sim.Core;
using Sim.Core.Kernel;
using Sim.Core.State;
using Sim.Core.Systems;
using Sim.Core.Worldgen;
using Sim.Tests.TestUtil;

namespace Sim.Tests.Systems;

/// <summary>
/// R4 item 2 — THE LEDGER CRASH (docs/r3-final-reconcile-record.md §1 "Defect found"): with the forager switch
/// ON, 100 founders per settlement and dev seed 42, ConsumptionSystem threw "Ledger amounts are never negative
/// (−1)" at turn 853. VIOLATED INVARIANT: a consumption request is never negative. The staple is asked for its
/// own demand PLUS each non-staple food's exact shortfall, a SIGNED quantity — a sub-unit fraction is lent to
/// the staple while it is banked in the non-staple's remainder and repaid (negative) when that remainder pays
/// out a whole unit. In a settlement whose demand had collapsed the repayment exceeded the staple's whole
/// request and was floored into a −1 ledger amount. FIX: the unabsorbed credit is carried in the staple's
/// remainder and settled against its next request (no clamp, nothing dropped); every non-negative request is
/// computed exactly as before, so no shipped world moves.
///
/// ADR-035 RE-RIG. The original 900-turn forager world reached the state only because every founded settlement
/// harvested nothing on its founding turn; with the founding-turn harvest restored (ADR-035), no dev seed 1–7
/// at founding stores 0 / 300 / 1500 carries a credit in 900 turns (measured), and its non-vacuity guard failed.
/// The state is therefore built DIRECTLY at the consumption level: one settlement whose demand has collapsed
/// (one adult, a short dt), a non-staple food whose banked remainder pays out a whole unit this turn, so its
/// signed shortfall (a repayment) exceeds the staple's whole request. Before e6b7e42 that request floored to
/// a −1 ledger amount and Ledger.Flow threw.
/// </summary>
public class SubstitutionCreditRegressionTests
{
    private static readonly SettlementId S0 = new(0);

    private static EraTable FlatEra(double dtYears) => EraTableLoader.Load(
        $$"""{ "bands": [ { "name": "flat", "startYear": 0, "endYear": 100000, "dtYears": {{dtYears.ToString(System.Globalization.CultureInfo.InvariantCulture)}} } ] }""");

    /// <summary>One adult, a stock row for every good, grain and livestock stocked; livestock's remainder
    /// banked just below one whole unit (the state an earlier turn's sub-unit eating leaves behind).</summary>
    private static WorldState CollapsedDemandRig(SimConfig cfg, double livestockBank)
    {
        var counts = new long[Cohorts.Count];
        counts[5] = 1;
        WorldState world = PopulationExactnessTests.BucketWorld(counts);
        var ledger = new Ledger(world.LedgerFlows);
        int livestock = cfg.Goods!.IdOf("livestock");
        foreach (GoodEntry g in cfg.Goods.Goods)
        {
            int row = world.GoodStocks.Add(new GoodStockRow(S0, new GoodId(g.Id), Conserved.Zero, 0.0, 0.0));
            long amount = g.Id == cfg.Goods.GrainId || g.Id == livestock ? 50 : 0;
            if (amount > 0)
                ledger.Flow(ref world.GoodStocks.Ref(row).Amount, ConservedQuantityIds.OfGood(new GoodId(g.Id)),
                    ReasonIds.InitialEndowment, amount, FlowDirection.Source, OverdrawPolicy.Throw);
            if (g.Id == livestock) world.GoodStocks.Ref(row).ConsumeRemainder = livestockBank;
        }
        return world;
    }

    private static GoodStockRow Row(WorldState w, int good)
    {
        for (int i = 0; i < w.GoodStocks.Count; i++)
            if (w.GoodStocks[i].Good.Value == good) return w.GoodStocks[i];
        throw new InvalidOperationException($"no row for good {good}");
    }

    [Fact]
    public void CollapsedDemand_RepaymentExceedsTheStaplesRequest_CreditCarried_NoNegativeLedgerAmount_AuditExact()
    {
        SimConfig cfg = TestConfigs.Sim();
        int grain = cfg.Goods!.GrainId;
        int livestock = cfg.Goods.IdOf("livestock");
        var exec = new TurnExecutor(FlatEra(0.1), [SystemCatalog.Consumption(cfg)]);
        WorldState world = CollapsedDemandRig(cfg, livestockBank: 0.999);

        WorldState next = exec.Step(world);

        // The crash state, reached and measured (non-vacuous): livestock paid out a whole unit, so its signed
        // shortfall is a repayment, and that repayment exceeded the staple's whole request — the staple's
        // request (its exact demand + shortfall + remainder) is NEGATIVE, which pre-e6b7e42 floored to −1.
        Assert.Equal(1L, Row(next, livestock).LastConsumptionEatenUnits);
        double credit = Row(next, grain).ConsumeRemainder;
        Assert.True(credit < 0.0, $"no substitution credit carried ({credit}) — the rig no longer reaches the crash state");
        Assert.True(Math.Floor(credit) <= -1.0, "the pre-fix floor would not have been a negative ledger amount");
        Assert.True(credit > -3.0, $"credit {credit} beyond the non-staple-count bound");
        // The fix: the staple asks for nothing, nothing flows, the credit is carried.
        Assert.Equal(0L, Row(next, grain).LastConsumptionDemandUnits);
        Assert.Equal(50L, Row(next, grain).Amount.Value);
        Assert.True(ConservationAuditor.IsConserved(next, out string report), report);

        // Settled against later requests: run on until the credit is repaid; never a negative stock, the
        // remainder stays in (−3, 1), the audit stays exact.
        bool settled = false;
        for (int t = 2; t <= 200 && !settled; t++)
        {
            next = exec.Step(next);
            for (int i = 0; i < next.GoodStocks.Count; i++)
            {
                GoodStockRow row = next.GoodStocks[i];
                Assert.True(row.Amount.Value >= 0, $"turn {t}: stock {row.Amount.Value} < 0");
                Assert.True(row.ConsumeRemainder is > -3.0 and < 1.0, $"turn {t}: remainder {row.ConsumeRemainder}");
                if (row.ConsumeRemainder < 0.0) Assert.Equal(grain, row.Good.Value);
            }
            settled = Row(next, grain).ConsumeRemainder >= 0.0;
            Assert.True(ConservationAuditor.IsConserved(next, out string r), $"turn {t}: {r}");
        }
        Assert.True(settled, "the carried credit was never settled against a later request");
    }
}
