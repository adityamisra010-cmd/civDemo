namespace Sim.Core.State;

/// <summary>
/// The variable registry (T2.2, D-020): the CODE-side mapping between the
/// names predicates use and the integer ids stored in VariableRow (ADR-001:
/// names never live in sim rows). Delivered incrementally per D-027 — an entry
/// exists only once a system actually publishes it. Registration order is the
/// deterministic iteration order.
///
/// M2 variables (published per settlement, per turn, by ClassMobilitySystem):
///  - food_surplus_ratio: LAST turn's harvest units over LAST turn's integer
///    demand units (Farming's LastHarvestUnits / Consumption's DemandUnits,
///    both read from Prev). Demand of zero → ratio 0.0 (an empty settlement
///    signals no surplus, so specialists can never emerge in one).
///  - artisan_share: artisan adults / total adults of the settlement
///    (band views over Prev buckets); zero adults → 0.0.
///
/// M3 variables:
///  - population: the settlement's total people (all classes, all cohorts).
///
/// REGISTRY LAW — SCALE INVARIANCE (T3.1 precedent, learned the hard way):
/// published variables that are all RATIOS are SCALE-INVARIANT, and no amount
/// of founding-size variation can differentiate behaviour through them. At
/// T3.1 twelve settlements were given jittered founding endowments to break
/// the T2.4 emergence lockstep, and they went on emerging their artisans in
/// the same year anyway — because a labor-limited food_surplus_ratio is
/// adultShare × output / consumption, identical at every size (measured
/// 3.5 ± 0.1 across all twelve). ANY emergence predicate that needs scale
/// sensitivity MUST publish an absolute quantity, not another ratio.
/// </summary>
public static class Variables
{
    public const int FoodSurplusRatio = 1;
    public const int ArtisanShare = 2;

    /// <summary>
    /// T3.1(c): total settlement population (all classes, all cohorts, Prev
    /// buckets) — the first SCALE-DEPENDENT published variable, and the
    /// registry's answer to the scale-invariance law above.
    ///
    /// THE EXTENT OF THE MARKET (Smith, Wealth of Nations I.iii: "the
    /// division of labour is limited by the extent of the market"). A
    /// craftsman can only specialize where enough neighbours want what he
    /// makes; a hamlet of eighty supports no full-time potter however good
    /// its harvest. So the artisan emergence predicate reads
    /// `food_surplus_ratio > X && population > Y`: surplus says the food
    /// exists to feed a specialist, population says a market exists to buy
    /// from him. Both are necessary and neither suffices — which is also why
    /// crossing times now depend on ln(foundingSize)/growth and the founding
    /// jitter finally spreads them.
    ///
    /// This is a LOCAL-demand proxy: it counts the settlement's own people.
    /// Once trade exists (T3.6) the honest measure is trade-connected demand
    /// — a town joined to neighbours by cheap transport commands a larger
    /// market than its own head-count implies. Queued.
    /// </summary>
    public const int Population = 3;

    /// <summary>Known names in registration order (parallel to ids 1..N).</summary>
    public static readonly string[] Names = ["food_surplus_ratio", "artisan_share", "population"];

    /// <summary>Name → id; −1 when unknown (callers own the actionable error).</summary>
    public static int IdOf(string name)
    {
        for (int i = 0; i < Names.Length; i++)
            if (string.Equals(Names[i], name, StringComparison.Ordinal)) return i + 1;
        return -1;
    }

    public static string KnownList() => string.Join(", ", Names);
}
