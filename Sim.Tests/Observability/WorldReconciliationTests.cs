using Sim.Core.Kernel;
using Sim.Core.Observability;
using Sim.Core.State;
using Sim.Tests.TestUtil;

namespace Sim.Tests.Observability;

/// <summary>
/// §2 — RECONCILIATION IS A TEST, NOT A HOPE. Opening + ΣSources − ΣSinks ==
/// Closing, EXACTLY, for every conserved quantity on every one of 300 turns, on
/// the founded and the driven worlds — and the named accounts (population,
/// grain, dwellings, every good) close through their named reasons alone, so a
/// new reason appearing on a quantity would surface as a discrepancy here
/// rather than being absorbed.
///
/// NON-VACUITY is pinned to MEASURED turns, not to prose: on both worlds
/// births, natural deaths, harvest and spoilage are non-zero on turn 2 and
/// migrants on turn 3 — what decides that last one is the vacancy bound's null
/// arm (ADR-025 §3.5c: turn 1 is the zero-harvest endowment turn, so every
/// settlement reads N_lim = 0 and V = 0 on turn 2 and the bound refuses every
/// gap flow world-wide for that one turn; docs/queue.md carries it as a
/// measured finding). The rarer flows (starvation, trade, decay) are pinned to
/// their first measured turn, or, where they never occur inside the horizon, to
/// their ABSENCE on every one of the 300 turns — an unpinned absence is not
/// coverage. A reconciliation over a world where nothing flowed proves 0 == 0.
/// </summary>
public class WorldReconciliationTests
{
    private static void AssertEveryTurnReconciles(ObservationLog log, string world)
    {
        Assert.Equal(300, log.Observations.Count);
        for (int i = 0; i < log.Observations.Count; i++)
        {
            TurnRecord t = log.Observations[i].Turn;
            Assert.Equal(i + 1, t.Turn);
            for (int s = 0; s < t.Stocks.Length; s++)
            {
                StockRecord st = t.Stocks[s];
                Assert.True(st.Reconciles, $"{world} turn {t.Turn}: {st.Name} discrepancy {st.Discrepancy}");
                Assert.Equal(0, st.Discrepancy);
                long sourced = 0, sunk = 0;
                for (int k = 0; k < st.Sources.Length; k++) sourced += st.Sources[k].Units;
                for (int k = 0; k < st.Sinks.Length; k++) sunk += st.Sinks[k].Units;
                Assert.Equal(st.Closing, st.Opening + sourced - sunk);
            }
            Assert.True(t.Population.Reconciles, $"{world} turn {t.Turn}: population discrepancy {t.Population.Discrepancy}");
            Assert.True(t.Grain.Reconciles, $"{world} turn {t.Turn}: grain discrepancy {t.Grain.Discrepancy}");
            Assert.True(t.Dwellings.Reconciles, $"{world} turn {t.Turn}: dwellings discrepancy {t.Dwellings.Discrepancy}");
            Assert.Equal(13, t.Goods.Length);   // 14 goods, grain excluded
            for (int g = 0; g < t.Goods.Length; g++)
                Assert.True(t.Goods[g].Reconciles, $"{world} turn {t.Turn}: {t.Goods[g].Name} discrepancy {t.Goods[g].Discrepancy}");

            // The identity, spelled out on the named accounts — not just the flag.
            Assert.Equal(t.Population.Closing,
                t.Population.Opening + t.Population.Births - t.Population.NaturalDeaths - t.Population.Starvation);
            Assert.Equal(t.Grain.Closing,
                t.Grain.Opening + t.Grain.Endowment + t.Grain.Harvest - t.Grain.Eaten - t.Grain.Spoilage - t.Grain.Overflow);
            Assert.Equal(t.Dwellings.Closing, t.Dwellings.Opening + t.Dwellings.Built - t.Dwellings.Decayed);
            Assert.Equal(0, t.Grain.Endowment);   // endowment is a founding-time source, never a step's

            // Causes are the same identity read as a delta.
            Assert.Equal(t.Causes.PopulationDelta, t.Causes.PopulationExplained);
            Assert.Equal(t.Causes.GrainDelta, t.Causes.GrainExplained);

            // Population, dwellings and all 14 goods are present; the toy stocks are not.
            Assert.Equal(16, t.Stocks.Length);
            Assert.Equal(ConservedQuantityIds.Population.Value, t.Stocks[0].Quantity);
            Assert.Equal(ConservedQuantityIds.Dwellings.Value, t.Stocks[1].Quantity);
            for (int s = 1; s < t.Stocks.Length; s++)
                Assert.True(t.Stocks[s].Quantity > t.Stocks[s - 1].Quantity, "stocks ascend by quantity id");
        }
    }

    [Fact]
    public void Founded_Seed42_300Turns_EveryQuantityReconcilesEveryTurn()
    {
        ObservationLog log = ObservedWorlds.Founded300.Value.Log;
        AssertEveryTurnReconciles(log, "founded");

        // NON-VACUITY, MEASURED ON THE MERGED T4.21-2 + T4.21-3 TREE: turn 2
        // carries four of the five flows and turn 3 the fifth.
        TurnRecord t2 = log.At(2)!.Turn;
        Assert.True(t2.Population.Births > 0, "no births on turn 2");
        Assert.True(t2.Population.NaturalDeaths > 0, "no deaths on turn 2");
        // MEASURED, and what decides it is the vacancy bound's null arm
        // (ADR-025 §3.5c): on turn 2 every destination reads a zero vacancy —
        // turn 1 is the endowment turn with zero harvest, so N_lim = 0 for that
        // one turn — and the bound refuses every gap flow world-wide; turn 3
        // moves 252 (merged tree; 256 on the T4.21-2 branch alone).
        Assert.Equal(0, t2.Flows.MigrantsMoved);
        Assert.True(log.At(3)!.Turn.Flows.MigrantsMoved > 0, "no migration on turn 3");
        Assert.True(t2.Grain.Harvest > 0, "no harvest on turn 2");
        Assert.True(t2.Grain.Spoilage > 0, "no spoilage on turn 2");
        Assert.True(log.At(1)!.Turn.Grain.Overflow > 0, "no granary overflow on turn 1");
        // RE-MEASURED ON THE MERGED TREE (CR-015 / ADR-026 + ADR-025): the
        // founded world starves NOBODY in 300 turns (55 pre-packet, 57 on the
        // T4.21-2 branch, none on the T4.21-3 branch) — the effective deficit
        // makes a weather-sized shortfall STRESS, which starves no one. The
        // starvation flow is asserted identically ZERO on every turn so the
        // absence is pinned, not left unobserved. First trade, MEASURED: 41
        // pre-packet, 21 on the T4.21-3 branch alone, 28 here.
        for (int t = 1; t <= 300; t++)
            Assert.Equal(0, log.At(t)!.Turn.Population.Starvation);
        Assert.True(log.At(28)!.Turn.Flows.TradeUnits > 0, "no trade on turn 28");
        Assert.True(log.At(1)!.Turn.Dwellings.Built > 0, "nothing built on turn 1");
        Assert.Equal(0, t2.Flows.SettlementsFounded);
        Assert.Equal(12, log.At(300)!.Settlements.Length);
    }

    [Fact]
    public void Driven_Seed42_300Turns_EveryQuantityReconcilesEveryTurn()
    {
        ObservationLog log = ObservedWorlds.Driven300.Value.Log;
        AssertEveryTurnReconciles(log, "driven");

        TurnRecord t2 = log.At(2)!.Turn;
        // THE AIM IS UNCHANGED — the driven world must exercise all five flows
        // early — and it is RE-AIMED AT THE TURNS THAT ACTUALLY DO, measured on
        // the merged tree: four of them on turn 2, migration on turn 3. What
        // decides the fifth is the vacancy bound's null arm (ADR-025 §3.5c): the
        // founded world's turn-1 harvest is zero ⇒ N_lim = 0 ⇒ V = 0 on turn 2,
        // so every gap flow is refused world-wide for that one turn (see the
        // founded pin above; docs/queue.md carries it as a measured finding).
        Assert.True(t2.Population.Births > 0 && t2.Population.NaturalDeaths > 0
            && t2.Grain.Harvest > 0 && t2.Grain.Spoilage > 0,
            "turn 2 of the driven world does not carry the four non-migration flows");
        Assert.Equal(0, t2.Flows.MigrantsMoved);
        Assert.True(log.At(3)!.Turn.Flows.MigrantsMoved > 0, "no migration on turn 3 of the driven world");
        // RE-MEASURED ON THE MERGED TREE: first starvation 7 pre-packet -> 8
        // here; trade on 7 holds.
        Assert.True(log.At(8)!.Turn.Population.Starvation > 0, "no starvation on turn 8");
        Assert.True(log.At(7)!.Turn.Flows.TradeUnits > 0, "no trade on turn 7");
        // T4.19-A (CR-014 ruled): the first-decay sample RE-MEASURED, 25 -> 48.
        // The 25 was read on the pre-lane-C founding vector (measured on the
        // lane-obs tree 8f59166, where it still holds). On lane C's vector the
        // first decayed dwelling is on turn 48 — and it is 48 on BOTH the
        // unfixed cap (probed to turn 212, before its 213 throw) and the
        // corrected cap, with identical readings on turns 7, 25 and 48. The
        // cap change therefore contributes nothing to this sample; the
        // founding vector is its whole cause. Starvation on 7 and trade on 7
        // hold on every arm. Reconciliation above is asserted on all 300
        // turns; this is the non-vacuity sample only.
        // RE-MEASURED ON THE MERGED TREE: NO dwelling decays in 300 driven
        // turns (48 on the T4.19-A tree, 64 on the T4.21-2 branch, 82 on the
        // T4.21-3 branch). The sample cannot be re-aimed at a turn that does not
        // exist, so the coverage is kept in the only honest form: the decay flow
        // is asserted identically ZERO on every one of the 300 turns, so the
        // absence is PINNED rather than left unobserved, and the dwellings
        // account's non-vacuity is carried by the Built assert above. What
        // decides it is the joint trajectory of ADR-025 and ADR-026 (goldens).
        for (int t = 1; t <= 300; t++)
            Assert.Equal(0, log.At(t)!.Turn.Dwellings.Decayed);
        // The driven world's goods economy is live: crafted goods are produced
        // AND consumed as inputs, which is what makes the per-good accounts
        // non-trivial (pottery on turn 5, MEASURED on the merged tree: 1829
        // produced, 845 eaten, 0 sunk as inputs; 1464 / 638 pre-packet).
        GoodAccount pottery = Array.Find(log.At(5)!.Turn.Goods, g => g.Name == "pottery")!;
        Assert.True(pottery.Produced > 0 && pottery.Eaten + pottery.InputsConsumed > 0,
            "pottery neither produced nor consumed on turn 5");
    }

    [Fact]
    public void ADiscrepancy_IsReportedAsTheIntegerRemainder_NeverRoundedAway()
    {
        // TEETH. A conserved stock moved without a ledger row is exactly the
        // defect §2 exists to name. Build one: source one person into a bucket
        // of `next` through a Ledger bound to a SCRATCH flow table, so the
        // world's own ledger never sees it. The population account must fail by
        // exactly 1 — and the generic stock record with it.
        WorldState prev = ObservedWorlds.Founded();
        WorldState next = prev.Clone();
        next.Clock = new SimClock(prev.Clock.Turn + 1, prev.Clock.SimDays + 3600, 3600);
        var scratch = new Ledger(new Table<LedgerFlowRow>());
        scratch.Flow(ref next.Buckets.Ref(0).Count, ConservedQuantityIds.Population, ReasonIds.Births,
            1, FlowDirection.Source, OverdrawPolicy.Throw);

        TurnObservation o = Observer.Observe(prev, next, TestConfigs.Sim(), []);

        Assert.False(o.Turn.Population.Reconciles);
        Assert.Equal(1, o.Turn.Population.Discrepancy);
        StockRecord pop = Array.Find(o.Turn.Stocks, s => s.Quantity == ConservedQuantityIds.Population.Value)!;
        Assert.False(pop.Reconciles);
        Assert.Equal(1, pop.Discrepancy);
        Assert.True(o.Turn.Grain.Reconciles);   // the untouched quantity still closes
        Assert.Equal(1, o.Turn.Causes.PopulationDelta - o.Turn.Causes.PopulationExplained);
    }
}
