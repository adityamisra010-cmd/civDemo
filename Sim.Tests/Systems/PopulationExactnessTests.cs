using FsCheck;
using FsCheck.Fluent;
using FsCheck.Xunit;
using Sim.Core;
using Sim.Core.Kernel;
using Sim.Core.State;
using Sim.Core.Systems;
using Sim.Core.Worldgen;
using Sim.Tests.TestUtil;

namespace Sim.Tests.Systems;

// T1.5 adversarial hardening, migrated to the cohort model at T2.1: per-flow
// EXACTNESS coverage. The T1.5 adversarial pass proved behavioral suites have
// no teeth against wrong-but-ledgered amounts — an aging leak laundered
// through Deaths+Births flows and a dropped death remainder both passed all
// 126 tests. These tests pin every demographic/food flow to an independently
// hand-computed expectation, exact long equality, no epsilon. The cohort
// migration keeps every guard and adds slot-advance aging and the famine
// age-selectivity multipliers to the pinned surface.
public class PopulationExactnessTests
{
    private static EraTable FlatEra(double dtYears) => EraTableLoader.Load(
        $$"""{ "bands": [ { "name": "flat", "startYear": 0, "endYear": 100000, "dtYears": {{dtYears.ToString(System.Globalization.CultureInfo.InvariantCulture)}} } ] }""");

    /// <summary>One settlement, single (culture 1, religion 1, class 1) group,
    /// 16 cohort rows endowed via Ledger from <paramref name="counts"/>.</summary>
    internal static WorldState BucketWorld(long[] counts)
    {
        var world = new WorldState(7);
        var settlement = new SettlementId(0);
        world.Settlements.Add(new SettlementRow(settlement, SiteCell: 0, FoundedTurn: 0));
        var ledger = new Ledger(world.LedgerFlows);
        for (int c = 0; c < Cohorts.Count; c++)
        {
            int row = world.Buckets.Add(new BucketRow(
                settlement, new CultureId(1), new ReligionId(1), new ClassId(1),
                c, Conserved.Zero, 0.0, 0.0, 0.0, 0.0));
            if (counts[c] > 0)
            {
                ledger.Flow(ref world.Buckets.Ref(row).Count, ConservedQuantityIds.Population,
                    ReasonIds.InitialEndowment, counts[c], FlowDirection.Source, OverdrawPolicy.Throw);
            }
        }
        return world;
    }

    /// <summary>Two-group world (vacuity-lens hardening): classes 1 and 2 in
    /// the same settlement/culture/religion, each with its own 16 cohort rows
    /// (contiguous runs, class 1 first — the founding layout).</summary>
    internal static WorldState TwoGroupWorld(long[] class1, long[] class2)
    {
        var world = new WorldState(7);
        var settlement = new SettlementId(0);
        world.Settlements.Add(new SettlementRow(settlement, SiteCell: 0, FoundedTurn: 0));
        var ledger = new Ledger(world.LedgerFlows);
        for (int cls = 1; cls <= 2; cls++)
        {
            long[] counts = cls == 1 ? class1 : class2;
            for (int c = 0; c < Cohorts.Count; c++)
            {
                int row = world.Buckets.Add(new BucketRow(
                    settlement, new CultureId(1), new ReligionId(1), new ClassId(cls),
                    c, Conserved.Zero, 0.0, 0.0, 0.0, 0.0));
                if (counts[c] > 0)
                {
                    ledger.Flow(ref world.Buckets.Ref(row).Count, ConservedQuantityIds.Population,
                        ReasonIds.InitialEndowment, counts[c], FlowDirection.Source, OverdrawPolicy.Throw);
                }
            }
        }
        return world;
    }

    private static long[] Uniform(long perCohort)
    {
        var counts = new long[Cohorts.Count];
        Array.Fill(counts, perCohort);
        return counts;
    }

    private static double[] Zeros() => new double[Cohorts.Count];

    private static double[] Filled(double v)
    {
        var a = new double[Cohorts.Count];
        Array.Fill(a, v);
        return a;
    }

    /// <summary>Config with every demographic rate zeroed — aging (structural,
    /// derived from dt alone) is the only thing left moving people.</summary>
    private static SimConfig AgingOnly(SimConfig cfg) => cfg with
    {
        Demographics = cfg.Demographics with
        {
            FertilityPerPersonPerYear = Zeros(),
            MortalityPerYear = Zeros(),
            StarvationMortalityMaxPerYear = 0.0,
        },
    };

    private static long FlowTotal(WorldState world, ConservedQuantityId quantity, ReasonId reason, bool sunk)
    {
        for (int i = 0; i < world.LedgerFlows.Count; i++)
        {
            LedgerFlowRow row = world.LedgerFlows[i];
            if (row.Quantity == quantity && row.Reason == reason)
                return sunk ? row.TotalSunk : row.TotalSourced;
        }
        return 0;
    }

    private static long Floor(double v) => (long)Math.Floor(v);

    private static long TotalPop(WorldState world)
    {
        long total = 0;
        for (int i = 0; i < world.Buckets.Count; i++) total += world.Buckets[i].Count.Value;
        return total;
    }

    [Fact]
    public void Demographics_SingleStep_EveryFlowAndCohort_HandComputedExact()
    {
        // One demographics step at dt = 2.5 (k = 0, f = 0.5 — no sink clamps
        // bind at these rates, and dt < cohort width keeps every newborn in
        // cohort 0) with a seeded deficit of 0.25. Every flow amount and final
        // cohort count is re-derived here from config × PREV counts through
        // the PINNED operation order (births → deaths → starvation → aging),
        // including the famine age multipliers and the fractional slot-advance
        // (floor(0.5 × prev) of each cohort moves one slot; the transfer is
        // sized from PREV, so it clamps to the post-mortality bucket — the
        // dead do not age; no clamp binds here). A doubled birth flow, a
        // swapped cohort rate, an ignored starvation multiplier, a mislabeled
        // reason, or an aging leak all break at least one exact equality.
        SimConfig cfg = TestConfigs.Sim();
        DemographicsConfig d = cfg.Demographics;
        const double dt = 2.5, deficit = 0.25;
        var prev = new long[Cohorts.Count];
        for (int c = 0; c < Cohorts.Count; c++) prev[c] = 10001 + 7 * c; // odd, distinct
        WorldState world = BucketWorld(prev);
        world.ConsumptionDeficits.Add(new ConsumptionDeficitRow(new SettlementId(0), deficit));

        var exec = new TurnExecutor(FlatEra(dt), [SystemCatalog.Demographics(cfg)]);
        WorldState next = exec.Step(world);

        // Births: Σ fertility[c] × prev[c] × dt — all into cohort 0 (dt < width).
        double birthsPerYear = 0.0;
        for (int c = 0; c < Cohorts.Count; c++)
            birthsPerYear += d.FertilityPerPersonPerYear[c] * prev[c];
        double birthsExact = birthsPerYear * dt;
        long born = Floor(birthsExact);

        // Deaths and starvation per cohort (no clamp binds at these magnitudes).
        var deaths = new long[Cohorts.Count];
        var starved = new long[Cohorts.Count];
        for (int c = 0; c < Cohorts.Count; c++)
        {
            deaths[c] = Floor(d.MortalityPerYear[c] * prev[c] * dt);
            double mult = c < Cohorts.FirstAdult ? d.StarvationChildMultiplier
                : c >= Cohorts.FirstElder ? d.StarvationElderMultiplier : 1.0;
            starved[c] = Floor(d.StarvationMortalityMaxPerYear * deficit * mult * prev[c] * dt);
        }

        // Aging recurrence (k = 0, f = 0.5): out_c = floor(0.5 × prev_c) moves
        // ONE slot up; 75+ absorbs and never moves out.
        var avail = new long[Cohorts.Count];
        var outMove = new long[Cohorts.Count];
        for (int c = 0; c < Cohorts.Count; c++)
        {
            avail[c] = prev[c] + (c == 0 ? born : 0) - deaths[c] - starved[c];
            outMove[c] = c == Cohorts.Count - 1 ? 0 : Floor(0.5 * prev[c]);
            Assert.True(outMove[c] <= avail[c], $"clamp bound at cohort {c} — test design broken");
        }
        for (int c = 0; c < Cohorts.Count; c++)
        {
            long arrivals = c >= 1 ? outMove[c - 1] : 0;
            Assert.Equal(avail[c] - outMove[c] + arrivals, next.Buckets[c].Count.Value);
        }

        // Ledger rows: exact per-reason attribution — and aging appears NOWHERE
        // (it is a Transfer; a Flow-pair regression inflates Births/Deaths).
        long deathsTotal = 0, starvedTotal = 0;
        for (int c = 0; c < Cohorts.Count; c++) { deathsTotal += deaths[c]; starvedTotal += starved[c]; }
        Assert.Equal(born, FlowTotal(next, ConservedQuantityIds.Population, ReasonIds.Births, sunk: false));
        Assert.Equal(deathsTotal, FlowTotal(next, ConservedQuantityIds.Population, ReasonIds.Deaths, sunk: true));
        Assert.Equal(starvedTotal, FlowTotal(next, ConservedQuantityIds.Population, ReasonIds.Starvation, sunk: true));
        Assert.Equal(0, FlowTotal(next, ConservedQuantityIds.Population, ReasonIds.Births, sunk: true));
        Assert.Equal(0, FlowTotal(next, ConservedQuantityIds.Population, ReasonIds.Deaths, sunk: false));

        // Remainders: exactly the sub-unit fractions of the same products —
        // bit-exact (a dropped remainder writes 0.0; the odd counts make every
        // asserted fraction nonzero, e.g. aging 0.5 × odd → .5).
        Assert.Equal(birthsExact - born, next.Buckets[0].BirthRemainder);
        Assert.Equal(d.MortalityPerYear[0] * prev[0] * dt - deaths[0], next.Buckets[0].DeathRemainder);
        Assert.Equal(0.5 * prev[4] - outMove[4], next.Buckets[4].AgingRemainder);
        Assert.Equal(
            d.StarvationMortalityMaxPerYear * deficit * d.StarvationElderMultiplier * prev[15] * dt - starved[15],
            next.Buckets[15].StarvationRemainder);
    }

    [Fact]
    public void NewbornCohortSpread_Dt10_SplitsHalfIntoCohortOne_Exact()
    {
        // The dt = 10 newborn spread, pinned semantically (goldens alone would
        // let a "credit everything to cohort 0" regression hide behind a
        // blind re-pin): a 10-year turn's newborns end it aged 0..10, so
        // cohorts 0 and 1 each receive exactly half. Fertility only, one
        // step: cohort 5 is the only fertile source (rate 0.10), everyone
        // else zeroed; prev[5] = 2000 → births exact = 0.10 × 2000 × 10 =
        // 2000, → 1000 into cohort 0 and 1000 into cohort 1 BEFORE aging
        // jumps them two slots (k = 2): they land in cohorts 2 and 3.
        SimConfig cfg = TestConfigs.Sim();
        double[] fertility = Zeros();
        fertility[5] = 0.10;
        cfg = AgingOnly(cfg) with
        {
            Demographics = AgingOnly(cfg).Demographics with { FertilityPerPersonPerYear = fertility },
        };
        var counts = new long[Cohorts.Count];
        counts[5] = 2000;
        var exec = new TurnExecutor(FlatEra(10.0), [SystemCatalog.Demographics(cfg)]);
        WorldState next = exec.Step(BucketWorld(counts));

        Assert.Equal(2000, FlowTotal(next, ConservedQuantityIds.Population, ReasonIds.Births, sunk: false));
        // Aging (k = 2, amounts sized from PREV) moves only the 2000 of cohort
        // 5 (→ 7); newborns' cohorts had prev = 0, so they STAY where credited.
        Assert.Equal(1000, next.Buckets[0].Count.Value);
        Assert.Equal(1000, next.Buckets[1].Count.Value);
        Assert.Equal(2000, next.Buckets[7].Count.Value);
        Assert.Equal(0, next.Buckets[5].Count.Value);
    }

    [Fact]
    public void SlotAdvanceAging_WholeAndFractionalSlots_Exact()
    {
        // The dt-correctness core of D-026 aging. Aging-only config (no other
        // flow interferes), distinct per-cohort counts so a swapped destination
        // is visible.
        SimConfig cfg = AgingOnly(TestConfigs.Sim());
        var counts = new long[Cohorts.Count];
        for (int c = 0; c < Cohorts.Count; c++) counts[c] = 1000 + 100 * c;

        // dt = 10 → k = 2, f = 0: every cohort jumps exactly two slots; 75+ absorbs.
        WorldState w10 = new TurnExecutor(FlatEra(10.0), [SystemCatalog.Demographics(cfg)])
            .Step(BucketWorld(counts));
        Assert.Equal(0, w10.Buckets[0].Count.Value);
        Assert.Equal(0, w10.Buckets[1].Count.Value);
        for (int c = 2; c < Cohorts.Count - 1; c++)
            Assert.Equal(counts[c - 2], w10.Buckets[c].Count.Value);
        Assert.Equal(counts[13] + counts[14] + counts[15], w10.Buckets[15].Count.Value);

        // dt = 5 → k = 1, f = 0: one slot.
        WorldState w5 = new TurnExecutor(FlatEra(5.0), [SystemCatalog.Demographics(cfg)])
            .Step(BucketWorld(counts));
        Assert.Equal(0, w5.Buckets[0].Count.Value);
        for (int c = 1; c < Cohorts.Count - 1; c++)
            Assert.Equal(counts[c - 1], w5.Buckets[c].Count.Value);
        Assert.Equal(counts[14] + counts[15], w5.Buckets[15].Count.Value);

        // dt = 2.5 → k = 0, f = 0.5: exactly half of each cohort advances one
        // slot (floor + remainder; these counts are even, so no fraction left).
        WorldState w25 = new TurnExecutor(FlatEra(2.5), [SystemCatalog.Demographics(cfg)])
            .Step(BucketWorld(counts));
        Assert.Equal(counts[0] / 2, w25.Buckets[0].Count.Value);
        for (int c = 1; c < Cohorts.Count - 1; c++)
            Assert.Equal(counts[c] - counts[c] / 2 + counts[c - 1] / 2, w25.Buckets[c].Count.Value);
        Assert.Equal(counts[15] + counts[14] / 2, w25.Buckets[15].Count.Value);

        // All three conserve exactly and leave no source/sink footprint.
        foreach (WorldState w in new[] { w10, w5, w25 })
        {
            Assert.Equal(TotalPop(w), FlowTotal(w, ConservedQuantityIds.Population, ReasonIds.InitialEndowment, sunk: false));
            Assert.True(ConservationAuditor.IsConserved(w, out string report), report);
        }
    }

    [Fact]
    public void RemainderAccumulation_SubUnitMortality_ProducesDeathsOverTime()
    {
        // The remainder-drop mutant escaped every magnitude test (single-step
        // flow amounts are unchanged when the fraction is discarded). This is
        // the semantic guard, on the ABSORBING cohort (75+ never ages out, so
        // the remainder trail stays with the people): 10 people at 0.006/yr,
        // dt = 10 — each turn's exact mortality is 0.06 × prev < 1, so deaths
        // EXIST ONLY through remainder accumulation. Hand-walked recurrence
        // (rate applies to the SHRINKING prev count; exact_t = 0.06·prev + rem):
        //   t1 0.60→0  t2 1.20→1  t3 0.74→0  t4 1.28→1  t5 0.76→0
        //   t6 1.24→1  t7 0.66→0  t8 1.08→1  t9 0.44→0   — 4 deaths, 6 alive.
        // A dropped remainder floors every turn to zero and never kills anyone.
        SimConfig cfg = TestConfigs.Sim();
        double[] mortality = Zeros();
        mortality[15] = 0.006;
        cfg = AgingOnly(cfg) with
        {
            Demographics = AgingOnly(cfg).Demographics with { MortalityPerYear = mortality },
        };
        var counts = new long[Cohorts.Count];
        counts[15] = 10;
        var exec = new TurnExecutor(FlatEra(10.0), [SystemCatalog.Demographics(cfg)]);
        WorldState world = exec.Run(BucketWorld(counts), 9);

        Assert.Equal(4, FlowTotal(world, ConservedQuantityIds.Population, ReasonIds.Deaths, sunk: true));
        Assert.Equal(6, world.Buckets[15].Count.Value);
    }

    [Fact]
    public void Aging_LeavesNoFlowFootprint_TotalPopulationExactlyConserved()
    {
        // Aging-only config: fertility, mortality, starvation all zero. Aging
        // is Ledger.Transfers — people MOVE but never source or sink. Ten
        // steps: the total is exactly constant every step and the Births/
        // Deaths/Starvation ledger totals stay exactly zero. An aging
        // implementation booked as compensating flows (even a balanced pair)
        // fails here.
        SimConfig cfg = AgingOnly(TestConfigs.Sim());
        var exec = new TurnExecutor(FlatEra(10.0), [SystemCatalog.Demographics(cfg)]);
        WorldState world = BucketWorld(Uniform(1000));
        const long total = 16000;

        for (int t = 1; t <= 10; t++)
        {
            world = exec.Step(world);
            Assert.Equal(total, TotalPop(world)); // exact, every step
            foreach (ReasonId reason in new[] { ReasonIds.Births, ReasonIds.Deaths, ReasonIds.Starvation })
            {
                Assert.Equal(0, FlowTotal(world, ConservedQuantityIds.Population, reason, sunk: false));
                Assert.Equal(0, FlowTotal(world, ConservedQuantityIds.Population, reason, sunk: true));
            }
        }

        // Aging really ran: everyone has reached the absorbing 75+ cohort.
        Assert.Equal(total, world.Buckets[15].Count.Value);
    }

    [Fact]
    public void FamineAgeSelectivity_ChildAndElderMultipliers_Provable()
    {
        // Acceptance criterion: famine kills the young and the old first, as a
        // MECHANISM (the multiplier sits inside the starvation resolution
        // equation). Uniform cohorts, full deficit, starvation only, one step:
        //   child cohorts:  floor(0.12 × 1.5 × 10000 × 2.5) = 4500 each (×3)
        //   adult cohorts:  floor(0.12 × 1.0 × 10000 × 2.5) = 3000 each (×9)
        //   elder cohorts:  floor(0.12 × 1.3 × 10000 × 2.5) = 3900 each (×4)
        // The aggregate Starvation sink pins the multipliers (an ignored
        // multiplier books 48000, not 56100), and the absorbing cohort pins
        // the per-row amount.
        SimConfig cfg = TestConfigs.Sim();
        cfg = cfg with
        {
            Demographics = cfg.Demographics with
            {
                FertilityPerPersonPerYear = Zeros(),
                MortalityPerYear = Zeros(),
            },
        };
        WorldState world = BucketWorld(Uniform(10000));
        world.ConsumptionDeficits.Add(new ConsumptionDeficitRow(new SettlementId(0), 1.0));
        var exec = new TurnExecutor(FlatEra(2.5), [SystemCatalog.Demographics(cfg)]);
        WorldState next = exec.Step(world);

        Assert.Equal(3 * 4500 + 9 * 3000 + 4 * 3900,
            FlowTotal(next, ConservedQuantityIds.Population, ReasonIds.Starvation, sunk: true));
        // Absorbing cohort, exact: survivors 10000−3900 = 6100, plus cohort
        // 14's slot-advance arrivals floor(0.5 × 10000) = 5000 (sized from
        // PREV; under its post-starvation 6100 available, so no clamp).
        Assert.Equal(6100 + 5000, next.Buckets[15].Count.Value);
    }

    [Fact]
    public void ClampShortfall_NeverBanked_StarvedCohortOwesNoFutureDeaths()
    {
        // Famine floor semantics on the absorbing cohort: requested starvation
        // deaths exceed the bucket — it clamps to exactly 0 people short and
        // the shortfall is DISCARDED, not banked. After repopulating, the next
        // step sinks only the newly computed amount.
        SimConfig cfg = AgingOnly(TestConfigs.Sim()) with { };
        cfg = cfg with
        {
            Demographics = cfg.Demographics with
            {
                StarvationMortalityMaxPerYear = TestConfigs.Sim().Demographics.StarvationMortalityMaxPerYear,
            },
        };
        const double dt = 10.0;
        var exec = new TurnExecutor(FlatEra(dt), [SystemCatalog.Demographics(cfg)]);
        var counts = new long[Cohorts.Count];
        counts[15] = 5;
        WorldState world = BucketWorld(counts);
        world.ConsumptionDeficits.Add(new ConsumptionDeficitRow(new SettlementId(0), 1.0));

        // Phase 1: requested = floor(0.12 × 1.0 × 1.3 × 5 × 10) = 7 > 5 → clamp
        // to 5; remainder keeps only the sub-unit fraction 0.8.
        world = exec.Step(world);
        Assert.Equal(0, world.Buckets[15].Count.Value);
        Assert.Equal(5, FlowTotal(world, ConservedQuantityIds.Population, ReasonIds.Starvation, sunk: true));
        Assert.True(world.Buckets[15].StarvationRemainder is >= 0.0 and < 1.0,
            $"shortfall banked: remainder {world.Buckets[15].StarvationRemainder}");

        // Phase 2: repopulate to 10, soften the deficit to 0.25 → requested =
        // floor(0.12 × 0.25 × 1.3 × 10 × 10 + 0.8) = floor(4.7) = 4. The
        // Starvation total rises by EXACTLY 4 — no phase-1 debt executes
        // against the living.
        var ledger = new Ledger(world.LedgerFlows);
        ledger.Flow(ref world.Buckets.Ref(15).Count, ConservedQuantityIds.Population,
            ReasonIds.InitialEndowment, 10, FlowDirection.Source, OverdrawPolicy.Throw);
        world.ConsumptionDeficits[0] = new ConsumptionDeficitRow(new SettlementId(0), 0.25);
        world = exec.Step(world);
        Assert.Equal(5 + 4, FlowTotal(world, ConservedQuantityIds.Population, ReasonIds.Starvation, sunk: true));
        Assert.Equal(6, world.Buckets[15].Count.Value);
    }

    [Fact]
    public void TwoGroups_AgingOnly_PerGroupTotalsInvariant_NoCrossTalk()
    {
        // Vacuity-lens hardening: the auditor and reconciliation are TOTALS-
        // only, so a Transfer misrouted into the OTHER class's cohort (a
        // SameGroup/FindInGroup bug) conserves perfectly and passes them.
        // Two populated groups, aging-only, ten dt = 10 steps: each group's
        // total must be exactly invariant — any cross-group leak shifts both.
        SimConfig cfg = AgingOnly(TestConfigs.Sim());
        var g1 = new long[Cohorts.Count];
        var g2 = new long[Cohorts.Count];
        for (int c = 0; c < Cohorts.Count; c++) { g1[c] = 1000 + 13 * c; g2[c] = 500 + 7 * c; }
        long total1 = 0, total2 = 0;
        for (int c = 0; c < Cohorts.Count; c++) { total1 += g1[c]; total2 += g2[c]; }

        var exec = new TurnExecutor(FlatEra(10.0), [SystemCatalog.Demographics(cfg)]);
        WorldState world = TwoGroupWorld(g1, g2);
        for (int t = 1; t <= 10; t++)
        {
            world = exec.Step(world);
            long sum1 = 0, sum2 = 0;
            for (int i = 0; i < world.Buckets.Count; i++)
            {
                if (world.Buckets[i].Class.Value == 1) sum1 += world.Buckets[i].Count.Value;
                else sum2 += world.Buckets[i].Count.Value;
            }
            Assert.Equal(total1, sum1);
            Assert.Equal(total2, sum2);
        }
    }

    // --- FsCheck: conservation exact across all cohort flows -----------------

    [Property(MaxTest = 100)]
    public Property CohortFlows_ArbitraryStatesAndDeficits_ConserveExactly()
    {
        // Acceptance criterion (T2.1): conservation exact across ALL cohort
        // flows, FsCheck over arbitrary cohort populations and famine depths.
        // Three canonical-config demographics steps at Neolithic dt; after
        // each, the audit identity Σ stocks + Σ sunk − Σ sourced == 0 must
        // hold EXACTLY, and the person-exact reconciliation from flows alone
        // must equal the live total.
        // Vacuity-lens hardening: dt drawn from all three aging regimes
        // (k=2/f=0, k=1/f=0, k=0/f=0.5), and the world carries an EMPTY
        // second class — which must stay exactly empty (no births from zero
        // people, no cross-group arrivals): the cross-group leak detector the
        // totals-only audit cannot be.
        Gen<long> countGen = Gen.Choose(0, 200_000).Select(v => (long)v);
        Gen<long[]> stateGen = countGen.ArrayOf(Cohorts.Count);
        Gen<int> deficitPctGen = Gen.Choose(0, 100);
        Gen<int> dtIdxGen = Gen.Choose(0, 2);
        return Prop.ForAll(stateGen.ToArbitrary(), deficitPctGen.ToArbitrary(), dtIdxGen.ToArbitrary(),
            (counts, deficitPct, dtIdx) =>
        {
            SimConfig cfg = TestConfigs.Sim();
            double dt = dtIdx == 0 ? 10.0 : dtIdx == 1 ? 5.0 : 2.5;
            WorldState world = TwoGroupWorld(counts, new long[Cohorts.Count]);
            world.ConsumptionDeficits.Add(
                new ConsumptionDeficitRow(new SettlementId(0), deficitPct / 100.0));
            var exec = new TurnExecutor(FlatEra(dt), [SystemCatalog.Demographics(cfg)]);
            for (int t = 0; t < 3; t++)
            {
                world = exec.Step(world);
                if (!ConservationAuditor.IsConserved(world, out string report))
                    return false.Label($"turn {t + 1}: {report}");
                for (int i = 0; i < world.Buckets.Count; i++)
                {
                    if (world.Buckets[i].Class.Value == 2 && world.Buckets[i].Count.Value != 0)
                        return false.Label(
                            $"turn {t + 1}: empty class gained {world.Buckets[i].Count.Value} people in cohort {world.Buckets[i].CohortIdx}");
                }
            }
            long endow = FlowTotal(world, ConservedQuantityIds.Population, ReasonIds.InitialEndowment, sunk: false);
            long births = FlowTotal(world, ConservedQuantityIds.Population, ReasonIds.Births, sunk: false);
            long deaths = FlowTotal(world, ConservedQuantityIds.Population, ReasonIds.Deaths, sunk: true);
            long starved = FlowTotal(world, ConservedQuantityIds.Population, ReasonIds.Starvation, sunk: true);
            return (TotalPop(world) == checked(endow + births - deaths - starved))
                .Label("person-exact reconciliation from flows alone failed");
        });
    }

    // --- food-loop exactness + dt-correctness -------------------------------

    /// <summary>One settlement, a fixed catchment summary (farmland F), an
    /// endowed store, and static cohort counts: 100 in cohort 0 (child),
    /// 200 in cohort 5 (adult), 50 in cohort 15 (elder) — weights 0.6/1.0/0.7
    /// reproduce the T1.5 demand of 295/yr and 200 farming adults exactly.
    /// Executors here never include demographics, so the counts are static.</summary>
    private static WorldState FoodWorld(double farmland, long store)
    {
        var counts = new long[Cohorts.Count];
        counts[0] = 100; counts[5] = 200; counts[15] = 50;
        WorldState world = BucketWorld(counts);
        world.CatchmentSummaries.Add(new CatchmentSummaryRow(
            new SettlementId(0), NodeCount: 1, EffectiveFarmland: farmland,
            NetworkRevision: 0, LastRecomputeTurn: 0));
        int row = world.FoodStores.Add(new FoodStoreRow(
            new SettlementId(0), Conserved.Zero, 0.0, 0.0));
        new Ledger(world.LedgerFlows).Flow(ref world.FoodStores.Ref(row).Store,
            ConservedQuantityIds.Food, ReasonIds.InitialEndowment, store,
            FlowDirection.Source, OverdrawPolicy.Throw);
        return world;
    }

    private static double DemandPerYear(SimConfig cfg) =>
        cfg.Consumption.CohortWeights[0] * 100
        + cfg.Consumption.CohortWeights[5] * 200
        + cfg.Consumption.CohortWeights[15] * 50;                                  // 295.0/yr

    [Fact]
    public void FarmingAndConsumption_SingleStep_HandComputedExact()
    {
        SimConfig cfg = TestConfigs.Sim();
        const double dt = 10.0, farmland = 78.5;
        const long endow = 10000;
        var exec = new TurnExecutor(FlatEra(dt),
            [SystemCatalog.Farming(cfg), SystemCatalog.Consumption(cfg)]);
        WorldState next = exec.Step(FoodWorld(farmland, endow));

        // T1.6: farm share is the LaborAllocations row; this hand-built world
        // has none, so the never-ordered default of 1.0 applies.
        // T1.8 spec amendment: LEONTIEF — min(land side, labor side). Here the
        // 200 adults (cohort 5, the T2.1 band view) bind:
        // min(78.5×28=2198, 200×1×5=1000) → labor-limited.
        long harvest = Floor(Math.Min(
            farmland * cfg.Farming.YieldPerFarmlandPerYear,
            200 * 1.0 * cfg.Farming.OutputPerFarmerPerYear) * dt);                 // 10000
        long demand = Floor(DemandPerYear(cfg) * dt);                              // 2950

        Assert.Equal(harvest, FlowTotal(next, ConservedQuantityIds.Food, ReasonIds.Harvest, sunk: false));
        Assert.Equal(demand, FlowTotal(next, ConservedQuantityIds.Food, ReasonIds.Eaten, sunk: true));
        Assert.Equal(endow + harvest - demand, next.FoodStores[0].Store.Value);
        Assert.Equal(0.0, next.ConsumptionDeficits[0].DeficitRatio);
    }

    [Fact]
    public void Consumption_Clamp_StoreToExactZero_DeficitRatioExact()
    {
        // No farming in the pipeline; a small store. Demand = 2950 > 1000 →
        // eats exactly 1000, store EXACTLY 0, deficit exactly (2950−1000)/2950.
        SimConfig cfg = TestConfigs.Sim();
        const double dt = 10.0;
        var exec = new TurnExecutor(FlatEra(dt), [SystemCatalog.Consumption(cfg)]);
        WorldState next = exec.Step(FoodWorld(farmland: 0.0, store: 1000));

        long demand = Floor(DemandPerYear(cfg) * dt);
        Assert.Equal(0, next.FoodStores[0].Store.Value);
        Assert.Equal(1000, FlowTotal(next, ConservedQuantityIds.Food, ReasonIds.Eaten, sunk: true));
        Assert.Equal((demand - 1000) / (double)demand, next.ConsumptionDeficits[0].DeficitRatio);
    }

    [Fact]
    public void FarmingAndConsumption_DtCorrect_EqualHorizonTotalsAgreeAcrossDt()
    {
        // Law-3 designed test (adversarial finding: every prior test ran the
        // food loop at dt = 10 exactly, so a hardcoded per-turn amount was
        // invisible). Harvest and demand are LINEAR in dt with telescoping
        // remainders: cumulative flow to the same sim-year horizon must agree
        // across dt within 1 unit (IEEE summation drift across different step
        // counts), and match rate × horizon within 1. A dropped or doubled
        // DtYears in either system shifts totals by ~2× and fails loudly.
        SimConfig cfg = TestConfigs.Sim();
        const int horizonYears = 200;
        const double farmland = 78.5;
        // Leontief (T1.8): static 200 adults bind the labor side across all dts.
        double harvestPerYear = Math.Min(
            farmland * cfg.Farming.YieldPerFarmlandPerYear,
            200 * 1.0 * cfg.Farming.OutputPerFarmerPerYear);                       // 1000.0/yr
        double demandPerYear = DemandPerYear(cfg);                                 // 295.0/yr

        var harvested = new long[3];
        var eaten = new long[3];
        double[] dts = [10.0, 5.0, 2.5];
        for (int i = 0; i < dts.Length; i++)
        {
            var exec = new TurnExecutor(FlatEra(dts[i]),
                [SystemCatalog.Farming(cfg), SystemCatalog.Consumption(cfg)]);
            // Store large enough that the clamp never binds at any dt.
            WorldState world = exec.Run(FoodWorld(farmland, store: 1_000_000),
                (int)(horizonYears / dts[i]));
            harvested[i] = FlowTotal(world, ConservedQuantityIds.Food, ReasonIds.Harvest, sunk: false);
            eaten[i] = FlowTotal(world, ConservedQuantityIds.Food, ReasonIds.Eaten, sunk: true);
        }

        for (int i = 1; i < dts.Length; i++)
        {
            Assert.True(Math.Abs(harvested[i] - harvested[0]) <= 1,
                $"harvest not dt-linear: {harvested[0]} @dt10 vs {harvested[i]} @dt{dts[i]}");
            Assert.True(Math.Abs(eaten[i] - eaten[0]) <= 1,
                $"consumption not dt-linear: {eaten[0]} @dt10 vs {eaten[i]} @dt{dts[i]}");
        }
        Assert.True(Math.Abs(harvested[0] - (long)(harvestPerYear * horizonYears)) <= 1);
        Assert.True(Math.Abs(eaten[0] - (long)(demandPerYear * horizonYears)) <= 1);
    }

    // --- founding endowment pinning -----------------------------------------

    [Fact]
    public void Founding_EndowmentPinnedToConfig_StocksAndLedgerRowsExact()
    {
        // Adversarial finding (T1.5): nothing pinned the founding amounts — a
        // double endowment or band swap passed everything. Exact pins,
        // config-derived, extended at T2.1 to the bucket cross product: the
        // full culture × religion × class × cohort layout, the whole founding
        // population on the FIRST class, other classes at exactly zero.
        SimConfig cfg = TestConfigs.Sim();
        WorldState world = WorldFounding.Found(TestConfigs.DevWorldgen(), cfg, seed: 42);

        // T2.3: the dev preset founds N = 4 settlements, each with the SAME
        // 400-person profile and food store (equal-split policy, provisional).
        int n = world.Settlements.Count;
        Assert.Equal(4, n); // D-025 dev preset
        int groups = cfg.Registries.Cultures.Length * cfg.Registries.Religions.Length
                     * cfg.Registries.Classes.Length;
        Assert.Equal(n * groups * Cohorts.Count, world.Buckets.Count);

        long popTotal = 0;
        for (int i = 0; i < world.Buckets.Count; i++)
        {
            BucketRow row = world.Buckets[i];
            bool firstClass = row.Class.Value == cfg.Registries.Classes[0].Id;
            long expected = firstClass ? cfg.Founding.CohortCounts[row.CohortIdx] : 0;
            Assert.Equal(expected, row.Count.Value);
            popTotal += row.Count.Value;
        }
        // The layout is contiguous ascending cohort runs, settlements in id
        // order (the documented deterministic order every consumer may rely on).
        for (int i = 0; i < world.Buckets.Count; i++)
        {
            Assert.Equal(i % Cohorts.Count, world.Buckets[i].CohortIdx);
            Assert.Equal(i / (groups * Cohorts.Count), world.Buckets[i].Settlement.Value);
        }

        Assert.Equal(n, world.FoodStores.Count);
        for (int i = 0; i < n; i++)
            Assert.Equal(cfg.Founding.FoodStore, world.FoodStores[i].Store.Value);
        Assert.Equal(0, world.ConsumptionDeficits.Count);

        Assert.Equal(popTotal,
            FlowTotal(world, ConservedQuantityIds.Population, ReasonIds.InitialEndowment, sunk: false));
        Assert.Equal(cfg.Founding.FoodStore * n,
            FlowTotal(world, ConservedQuantityIds.Food, ReasonIds.InitialEndowment, sunk: false));
        Assert.Equal(0,
            FlowTotal(world, ConservedQuantityIds.Population, ReasonIds.InitialEndowment, sunk: true));
    }
}
