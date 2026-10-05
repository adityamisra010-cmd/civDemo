using Sim.Core;
using Sim.Core.State;
using Sim.Core.Systems;
using Sim.Tests.TestUtil;
using Xunit.Abstractions;
using static Sim.Tests.Systems.TaxPressureMeasurement;

namespace Sim.Tests.Systems;

/// <summary>
/// F1 (2026-10-05, the H2 verifier's BLOCKER; docs/d049 §13) — REBELS WITHHOLD THE LEVY, NOT THEIR OWN BREAD. A
/// segment in revolt withholds the work the levy compels; it does not stop feeding itself. Before F1 the rebels'
/// withholding (1 − r × withheld, withheld → 1) reached every product, food included, so at a 99–100 % levy a FINAL
/// settlement (which cannot revolt away, D-048 ruling 5) starved to extinction and an ordinary capital lost most of its
/// people to famine before its rebels carried it. Measured on the full pipeline (dev world, seed 42, canonical era
/// table, levy from turn 0, A3, crafts known); assertions are properties, not turn pins.
/// </summary>
public class TaxRebelSubsistenceTests(ITestOutputHelper output)
{
    private static UnrestTuning U => TestConfigs.Sim().Needs!.Unrest!;

    [Theory]
    [InlineData(99.0)]
    [InlineData(100.0)]
    public void AFinalSettlement_UnderASustainedTotalLevy_IsNotStarvedToExtinctionByItsOwnRebels(double percent)
    {
        List<Reading> r = Run(Condition.Natural, percent, 120, settlements: 1, colonize: false);
        long peak = r.Max(x => x.Population);
        foreach (Reading x in r.Where((_, i) => i % 10 == 9))
            output.WriteLine($"t{x.Turn} pop {x.Population} rebels {x.Rebels:F3} out x{x.Output:F3} deficit {x.FoodDeficit:F3} T {x.MeanT:F1}");
        // The levy does bite: the segments pass their tipping point and rebel (the property under test is reached).
        Assert.Contains(r, x => x.Rebels > 0.5);
        Assert.All(r, x => Assert.True(x.Controlled, $"turn {x.Turn}: the final settlement revolted away"));
        Assert.All(r, x => Assert.True(x.Population > 0, $"turn {x.Turn}: the final settlement died out"));
        // The rebels' own subsistence is never withheld: the place keeps at least a quarter of its peak population.
        Assert.True(r[^1].Population * 4 >= peak, $"final settlement starved: {r[^1].Population} of a peak {peak}");
        int risen = r.FindIndex(x => x.Rebels > 0.5);
        Assert.True(r[^1].Population >= r[risen].Population, $"the rebellion shrank the final settlement: {r[risen].Population} -> {r[^1].Population}");
    }

    [Theory]
    [InlineData(99.0)]
    [InlineData(100.0)]
    public void AnOrdinaryCapital_IsCarriedByItsRebels_NotEmptiedByFamineFirst(double percent)
    {
        List<Reading> r = Run(Condition.Natural, percent, 40);
        int lost = r.FindIndex(x => !x.Controlled);
        Assert.True(lost > 0, "the ordinary capital never changed hands under a sustained extreme levy");
        long peak = r.Take(lost).Max(x => x.Population);
        output.WriteLine($"lost at turn {lost + 1}: pop {r[lost - 1].Population} of a peak {peak}");
        Assert.True(r[lost - 1].Population * 4 >= peak * 3, $"famine emptied the capital before the rising: {r[lost - 1].Population} of {peak}");
    }

    /// <summary>The reader itself: with every segment in full revolt under a total levy, non-food output is struck to 0
    /// but food output stays at the untaxed baseline; with no drag the food factor IS the extraction multiplier.</summary>
    [Fact]
    public void TheFoodFactor_IsFlooredAtTheUntaxedBaseline_AndEqualsExtractionWithoutDrag()
    {
        SimConfig cfg = TestConfigs.Sim();
        (WorldState w, PolityId p) = GovernanceRigs.Founded();
        GovernanceRigs.Grant(w, p);
        SettlementId seat = GovernanceRigs.Seat(w, p);
        w.TaxPolicies.Add(new TaxPolicyRow(p, 1.0));
        Assert.Equal(Governance.ExtractionMultiplier(w, seat, cfg), Governance.FoodOutputMultiplier(w, seat, cfg));
        Assert.Equal(Governance.OutputMultiplier(w, seat, cfg), Governance.FoodOutputMultiplier(w, seat, cfg));
        for (int i = 0; i < w.Grievances.Count; i++)
            if (w.Grievances[i].Settlement == seat)
                w.TaxGrievances.Add(new TaxGrievanceRow(seat, w.Grievances[i].Class, 10.0 * U.UprisingGrievance));
        Assert.Equal(0.0, Governance.OutputMultiplier(w, seat, cfg));
        Assert.Equal(1.0, Governance.FoodOutputMultiplier(w, seat, cfg));
    }
}
