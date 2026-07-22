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
/// </summary>
public static class Variables
{
    public const int FoodSurplusRatio = 1;
    public const int ArtisanShare = 2;

    /// <summary>Known names in registration order (parallel to ids 1..N).</summary>
    public static readonly string[] Names = ["food_surplus_ratio", "artisan_share"];

    /// <summary>Name → id; −1 when unknown (callers own the actionable error).</summary>
    public static int IdOf(string name)
    {
        for (int i = 0; i < Names.Length; i++)
            if (string.Equals(Names[i], name, StringComparison.Ordinal)) return i + 1;
        return -1;
    }

    public static string KnownList() => string.Join(", ", Names);
}
