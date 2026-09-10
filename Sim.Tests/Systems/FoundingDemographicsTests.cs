using Sim.Core;
using Sim.Core.Kernel;
using Sim.Core.State;
using Sim.Core.Systems;
using Sim.Core.Worldgen;
using Sim.Tests.TestUtil;

namespace Sim.Tests.Systems;

// T4.19 lane C — THE FOUNDING COHORT VECTOR IS THE KERNEL'S OWN STABLE AGE
// STRUCTURE, AND THAT IS RE-DERIVED HERE ON EVERY RUN RATHER THAN TRUSTED.
//
// The defect this closes (docs/m4-population-transient-investigation.md, the
// T4.18 diagnosis the director accepted): the old `founding.cohortCounts`
// placed 17.5 % of the founding population in the 60+ cohorts, where the
// shipped mortality schedule runs 0.107–0.305/yr, so every founded world opened
// by shedding ~22 % of its people in three turns to births-minus-deaths alone —
// an initialization artefact that looked like a famine and was not one.
//
// The ruling forbade every shortcut (no startup modifier, no suppressed deaths,
// no equation change) and required the ONE honest fix: found the world on the
// age structure the shipped rates support. That structure is MEASURED from
// `DemographicsSystem` run alone (deficit 0), never derived on paper — because a
// paper derivation of the ADR-011 micro-kernel (person-years birth kernel,
// in-step infant deaths, fractional slot-advance aging) would be a second
// implementation that could disagree with the first. The vector in sim.json
// is that measurement apportioned to the unchanged total of 400 by
// largest-remainder rounding, and the first test below fails the moment the
// data file and the kernel stop agreeing — a fertility or mortality retune
// that forgets the founding vector re-opens the transient, and this is what
// says so.
public class FoundingDemographicsTests
{
    private const long FoundingTotal = 400;

    private static EraTable FlatEra(double dtYears) => EraTableLoader.Load(
        $$"""{ "bands": [ { "name": "flat", "startYear": 0, "endYear": 100000, "dtYears": {{dtYears.ToString(System.Globalization.CultureInfo.InvariantCulture)}} } ] }""");

    /// <summary>The production demographics kernel and NOTHING else: with no
    /// consumption system in the pipeline the deficit table is never written,
    /// so the PREV-read deficit is exactly 0 on every turn — the fed regime.</summary>
    private static TurnExecutor DemographicsAlone(SimConfig cfg, double dtYears) =>
        new(FlatEra(dtYears), PipelineLoader.Load(
            """{ "pipeline": ["demographics"] }""", SystemCatalog.All(cfg)));

    private static long[] CohortVector(WorldState world)
    {
        var v = new long[Cohorts.Count];
        for (int i = 0; i < world.Buckets.Count; i++)
            v[world.Buckets[i].CohortIdx] += world.Buckets[i].Count.Value;
        return v;
    }

    private static long Total(long[] v)
    {
        long t = 0;
        foreach (long x in v) t += x;
        return t;
    }

    /// <summary>
    /// Largest-remainder apportionment (Hamilton's method): every slot gets the
    /// floor of its exact share of <paramref name="total"/>, and the leftover
    /// units go one each to the slots with the largest fractional remainders.
    /// TIE-BREAK IS THE LOWEST INDEX (constitution rule: any ordering over
    /// double-valued scores carries a stable integer tie-break; the tie-dense
    /// test below proves it). Test-side only — this is the executable form of
    /// the derivation recorded in sim.json's `_docCohortCounts`, not sim code.
    /// </summary>
    internal static long[] Apportion(double[] weights, long total)
    {
        int n = weights.Length;
        double sum = 0.0;
        for (int i = 0; i < n; i++) sum += weights[i];
        var result = new long[n];
        var remainder = new double[n];
        long used = 0;
        for (int i = 0; i < n; i++)
        {
            double exact = weights[i] / sum * total;
            result[i] = (long)Math.Floor(exact);
            remainder[i] = exact - result[i];
            used += result[i];
        }
        // Rank by (remainder desc, index asc) — a composite key, never the
        // bare double; equal remainders resolve to the lower index.
        var order = new int[n];
        for (int i = 0; i < n; i++) order[i] = i;
        Array.Sort(order, (a, b) =>
        {
            int byRemainder = remainder[b].CompareTo(remainder[a]);
            return byRemainder != 0 ? byRemainder : a.CompareTo(b);
        });
        for (long k = 0; k < total - used; k++) result[order[k]]++;
        return result;
    }

    [Fact]
    public void CohortCounts_AreTheLargestRemainderRoundingOfTheKernelsOwnStableShape()
    {
        // START FROM A SHAPE THAT IS NOT THE ANSWER. A uniform vector (25 000
        // per cohort — ×1000 so integer flooring is invisible at four decimals)
        // converges to the kernel's stable shares only if the kernel drives it
        // there; starting from the shipped vector would prove persistence,
        // not convergence. Measured: shares are stationary to 4 decimals by
        // turn 8 from the OLD vector and identical at turns 50/100/200/400
        // (docs/m4-founding-demographics-correction.md §2).
        SimConfig cfg = TestConfigs.Sim();
        var uniform = new long[Cohorts.Count];
        Array.Fill(uniform, 25_000L);
        TurnExecutor exec = DemographicsAlone(cfg, dtYears: 10.0);
        WorldState world = PopulationExactnessTests.BucketWorld(uniform);

        for (int t = 1; t <= 50; t++) world = exec.Step(world);
        long[] at50 = CohortVector(world);
        for (int t = 51; t <= 100; t++) world = exec.Step(world);
        long[] at100 = CohortVector(world);

        // Convergence, measured not assumed: the shares moved by less than one
        // part in ten thousand between turns 50 and 100 …
        double total50 = Total(at50), total100 = Total(at100);
        for (int c = 0; c < Cohorts.Count; c++)
        {
            double drift = Math.Abs(at100[c] / total100 - at50[c] / total50);
            Assert.True(drift < 1e-4, $"cohort {c} share still drifting at turn 100: {drift}");
        }
        // … and the fed growth rate they settle to is the kernel's +0.076 %/yr
        // (ratified pre-modern fed growth ≈ 0.07 %/yr, T2.7), annualised over
        // the 500 sim-years between the two readings.
        double growthPerYear = Math.Pow(total100 / total50, 1.0 / 500.0) - 1.0;
        Assert.InRange(growthPerYear, 0.0007, 0.0008);

        // THE PIN: the shipped vector IS these shares apportioned to 400.
        var shares = new double[Cohorts.Count];
        for (int c = 0; c < Cohorts.Count; c++) shares[c] = at100[c];
        long[] derived = Apportion(shares, FoundingTotal);
        Assert.Equal(FoundingTotal, Total(cfg.Founding.CohortCounts));
        Assert.Equal(derived, cfg.Founding.CohortCounts);
    }

    [Fact]
    public void Apportion_TieDense_LowestIndexWins_AndTheTotalIsExact()
    {
        // Sixteen EQUAL weights over 403 units: every remainder is identical
        // (0.1875), so the three extra units must fall to indices 0, 1, 2 and
        // nowhere else. A tie-break on anything but the index — insertion order
        // of an unstable sort, say — would scatter them.
        var equal = new double[Cohorts.Count];
        Array.Fill(equal, 1.0);
        long[] r = Apportion(equal, 403);
        Assert.Equal(403, Total(r));
        for (int i = 0; i < Cohorts.Count; i++)
            Assert.Equal(i < 3 ? 26 : 25, r[i]);

        // Exactly-half remainders on every slot (16 equal weights, 8 units):
        // eight ties at 0.5, the eight LOWEST indices take the units.
        r = Apportion(equal, 8);
        Assert.Equal(8, Total(r));
        for (int i = 0; i < Cohorts.Count; i++)
            Assert.Equal(i < 8 ? 1 : 0, r[i]);

        // No remainders at all: nothing to distribute, floors are the answer.
        r = Apportion(equal, 400);
        for (int i = 0; i < Cohorts.Count; i++) Assert.Equal(25, r[i]);

        // Distinct remainders are ranked by SIZE, not position: the largest
        // fraction (index 3 at 0.8) beats a smaller one at a lower index.
        r = Apportion([1.9, 1.1, 1.0, 2.0], 6);
        Assert.Equal(new long[] { 2, 1, 1, 2 }, r);
        r = Apportion([1.2, 1.0, 1.0, 2.8], 6);
        Assert.Equal(new long[] { 1, 1, 1, 3 }, r);

        // The total is exact for every total in a sweep, and no slot ever
        // receives more than one unit above its floor.
        for (long total = 0; total <= 1000; total++)
        {
            long[] s = Apportion(equal, total);
            Assert.Equal(total, Total(s));
            long floor = total / Cohorts.Count;
            for (int i = 0; i < Cohorts.Count; i++)
                Assert.True(s[i] == floor || s[i] == floor + 1, $"total {total} slot {i} got {s[i]}");
        }
    }

    [Fact]
    public void FoundedWorld_OpensInsideItsStableRegime_NoInitializationCollapse()
    {
        // THE SEMANTIC PIN, on the world the director actually plays (canonical
        // 1024² N = 12, seed 42, the production pipeline, no orders). Before
        // the correction its opening was 5,140 → 4,330 → 4,041 → 3,987 — a
        // −15.8 % first turn and −22.4 % by turn 3, every person of it
        // births-minus-deaths (T4.18 §2). After: 5,143 → 5,245 → 5,193 → 5,191
        // → 5,239, measured — the largest excursion is +2.0 % and starvation
        // is exactly zero throughout (seeds 7, 123 and 2024 open +2.6 %,
        // +3.9 % and +4.1 % on turn 1, then grow at the fed rate). That
        // turn-1 bump is measured, not a mystery: with endowmentJitter 0 all
        // four seeds open 4,800 → 4,920, exactly the D-004 remainder warm-up
        // (16 death-remainder accumulators per settlement start at zero, so
        // the first turn's deaths floor low by ~8 per settlement, once); the
        // jittered shapes add the rest. The band is ±5 %: wide enough that
        // neither effect can trip it, far too narrow for a re-opened
        // transient (−13 % to −16 % on turn 1) to pass. A golden hash cannot
        // say WHY it moved; this can.
        SimConfig cfg = TestConfigs.Sim();
        using var eraStream = Sim.Data.DataFiles.OpenEraPacing();
        using var pipeStream = Sim.Data.DataFiles.OpenPipeline();
        var exec = new TurnExecutor(EraTableLoader.Load(eraStream),
            PipelineLoader.Load(pipeStream, SystemCatalog.All(cfg, TestConfigs.Worldgen())));
        WorldState world = WorldFounding.Found(TestConfigs.Worldgen(), cfg, 42);

        long founding = Total(CohortVector(world));
        for (int t = 1; t <= 4; t++)
        {
            world = exec.Step(world);
            long pop = Total(CohortVector(world));
            double ratio = pop / (double)founding;
            Assert.True(ratio is >= 0.95 and <= 1.05,
                $"turn {t}: population {pop} is {ratio:F4} of the founding {founding} — outside the stable regime");
            for (int i = 0; i < world.LedgerFlows.Count; i++)
            {
                LedgerFlowRow row = world.LedgerFlows[i];
                if (row.Quantity == ConservedQuantityIds.Population && row.Reason == ReasonIds.Starvation)
                    Assert.Equal(0, row.TotalSunk);
            }
        }
    }
}
