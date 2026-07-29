using Sim.Core.State;
using Sim.Core.Systems;
using Sim.Core.Worldgen;
using Sim.Tests.TestUtil;

namespace Sim.Tests.Systems;

/// <summary>
/// T3.6b test-power pin (ADR-017): the founding-endowment SPREAD is load-
/// bearing state, and a silent regression to lockstep must fail a test —
/// the original queue item went stale precisely because nothing re-measured
/// it (T3.1c discharged the lockstep and the item sat unmeasured for two
/// milestones). Asserts the canonical config's realised founding-population
/// CV across the twelve settlements clears a floor, on two seeds.
///
/// THE FLOOR AND ITS MARGINS, stated at the point of choice (the T3.4d
/// asymmetric-margin discipline): measured clean values at amplitude 0.69
/// are CV 0.302 (seed 42) / 0.388 (seed 7); at the OLD amplitude 0.25 they
/// were 0.115 / 0.143 (Item 0 table); at amplitude 0 the CV is exactly 0.
/// Floor 0.22 sits ~27% below the weaker clean value (must-pass margin
/// ~0.08 absolute) and ~55% above the strongest regression value
/// (must-fire margin ~0.08 absolute against a revert-to-0.25, unbounded
/// against jitter-deleted). The weaker margin is the must-fire side against
/// a 0.25 revert — stated here so a future endowment retune knows which
/// side is thin. PROVEN RED (§7.4) against BOTH regressions: amplitude → 0
/// (jitter deleted) and amplitude → 0.25 (silent revert), each measured.
/// </summary>
public class FoundingVariationPinTests
{
    [Theory]
    [InlineData(42ul)]
    [InlineData(7ul)]
    public void FoundingPopulationSpread_ClearsTheVarianceFloor(ulong seed)
    {
        SimConfig cfg = TestConfigs.Sim();
        WorldState world = WorldFounding.Found(TestConfigs.Worldgen(), cfg, seed);
        int n = world.Settlements.Count;
        var pop = new double[n];
        for (int i = 0; i < world.Buckets.Count; i++)
            pop[world.Buckets[i].Settlement.Value] += world.Buckets[i].Count.Value;

        double mean = 0;
        foreach (double p in pop) mean += p;
        mean /= n;
        double sq = 0;
        foreach (double p in pop) sq += (p - mean) * (p - mean);
        double cv = Math.Sqrt(sq / (n - 1)) / mean;

        Assert.True(cv >= 0.22,
            $"founding-population CV across {n} settlements is {cv:F3} (seed {seed}) — below the "
            + "0.22 variance floor. Either founding.endowmentJitter regressed (0.69 shipped, "
            + "ADR-017/RC-1) or the jitter path is broken; lockstep founding is the T2.13 defect "
            + "this pin exists to keep dead.");
    }
}
