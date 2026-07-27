using Sim.Core.State;

namespace Sim.Core.Systems.Consumption;

/// <summary>
/// T3.5 — the population measures a D-035 basket is denominated against, in
/// ONE place so ConsumptionSystem (which charges the basket) and
/// NeedsGrievanceSystem (which credits it) cannot drift apart. Pure: reads a
/// read-only world, returns numbers, holds nothing.
///
/// TWO MEASURES, and the distinction is physical rather than cosmetic:
/// NUTRITIONAL persons are cohort-weighted (D-015 — a child eats less than an
/// adult, and the weights are a nutritional table); HEADS are people. Shelter
/// and Comfort are denominated per head because a child needs a whole roof and
/// a whole coat. Using the nutritional weights for them would silently import a
/// dietary fact into a housing equation.
/// </summary>
public static class BasketDemand
{
    /// <summary>Both population measures for one (settlement, class), from
    /// PREV bucket counts (law 3, §3.2).</summary>
    public static void Persons(
        IReadOnlyWorldState prev, SettlementId settlement, ClassId cls,
        double[] cohortWeights, out double nutritional, out double heads)
    {
        nutritional = 0.0;
        heads = 0.0;
        for (int i = 0; i < prev.Buckets.Count; i++)
        {
            BucketRow bucket = prev.Buckets[i];
            if (bucket.Settlement != settlement || bucket.Class != cls) continue;
            nutritional += cohortWeights[bucket.CohortIdx] * bucket.Count.Value;
            heads += bucket.Count.Value;
        }
    }
}
