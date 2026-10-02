using Sim.Core.Systems;

namespace Sim.Core.State;

/// <summary>
/// ADR-032 — THE ONE ROAD-PERFORMANCE DEFINITION (the Director's final rulings 8 and 22).
///
/// A route modernized a fraction <c>f</c> of the way from class <c>old</c> to class
/// <c>target</c> travels at
/// <code>effective_speed = old_speed + f × (target_speed − old_speed)</code>
/// where a class's relative SPEED is 1 / speedFactor (sim.json roads.classes: speedFactor is
/// the class's travel cost per km as a fraction of the free baseline's). The effective
/// travel-cost multiplier is therefore <c>1 / effective_speed</c>: f = 0 gives exactly the
/// old class, f = 1 exactly the target, f = ½ the midpoint SPEED. Capacity interpolates the
/// same way (floored to whole tonnes).
///
/// RoadDevelopmentSystem evaluates this ONCE when it writes a route and stores the result on
/// the row (<see cref="TransportEdgeRow.CostFactor"/>, <see cref="TransportEdgeRow.CapacityTonnesPerYear"/>).
/// Every consumer then reads the stored value through <see cref="TravelKm"/> /
/// <see cref="TravelCostUnits"/>: Pathfinder's fast-lane overlay (and therefore CatchmentSystem's
/// partitions and SettlementDistances, and PathBuild's routing) and
/// <see cref="TransportQuery.EstimateFreight"/>. There is no second speed calculation anywhere.
/// Pure; no state; doubles only (law 7).
/// </summary>
public static class RoadPerformance
{
    /// <summary>Relative speed of a class (the baseline DirtPath is 1 at speedFactor 1).</summary>
    public static double Speed(RoadsConfig roads, int edgeType) => 1.0 / roads.ClassOf(edgeType)!.SpeedFactor;

    /// <summary>Effective relative speed of a route <paramref name="fraction"/> of the way from
    /// <paramref name="fromClass"/> to <paramref name="targetClass"/> (fraction clamped to [0,1]).</summary>
    public static double EffectiveSpeed(RoadsConfig roads, int fromClass, int targetClass, double fraction)
    {
        double f = Clamp01(fraction);
        double s0 = Speed(roads, fromClass), s1 = Speed(roads, targetClass);
        return f >= 1.0 ? s1 : s0 + f * (s1 - s0);
    }

    /// <summary>Effective travel-cost multiplier per km (1 / effective speed). Exactly the class's
    /// speedFactor at fraction 0 (old class) and at fraction 1 (target class).</summary>
    public static double EffectiveCostFactor(RoadsConfig roads, int fromClass, int targetClass, double fraction)
    {
        double f = Clamp01(fraction);
        if (f <= 0.0) return roads.ClassOf(fromClass)!.SpeedFactor;
        if (f >= 1.0) return roads.ClassOf(targetClass)!.SpeedFactor;
        return 1.0 / EffectiveSpeed(roads, fromClass, targetClass, f);
    }

    /// <summary>Effective capacity: old + floor(f × (target − old)), exact at both ends.</summary>
    public static long EffectiveCapacity(RoadsConfig roads, int fromClass, int targetClass, double fraction)
    {
        double f = Clamp01(fraction);
        long c0 = roads.ClassOf(fromClass)!.CapacityTonnesPerYear, c1 = roads.ClassOf(targetClass)!.CapacityTonnesPerYear;
        if (f <= 0.0) return c0;
        if (f >= 1.0) return c1;
        return c0 + (long)Math.Floor(f * (c1 - c0));
    }

    /// <summary>
    /// The fraction toward <paramref name="newTarget"/> that keeps a route's CURRENT effective
    /// speed when its modernization target is raised (a route part-way to PavedRoad when the
    /// issuer now knows Highway): performance is never lost, and the remaining work is costed
    /// against the new target. Same speed ⇒ same pathfinding cost, by construction.
    /// </summary>
    public static double RetargetFraction(RoadsConfig roads, int fromClass, int oldTarget, double fraction, int newTarget)
    {
        double now = EffectiveSpeed(roads, fromClass, oldTarget, fraction);
        double s0 = Speed(roads, fromClass), s2 = Speed(roads, newTarget);
        if (!(s2 > s0)) return 0.0;
        return Clamp01((now - s0) / (s2 - s0));
    }

    /// <summary>A built route's travel length in baseline-equivalent km: LengthKm × its stored
    /// effective cost factor. THE value pathfinding and freight estimation share.</summary>
    public static double TravelKm(in TransportEdgeRow edge) => edge.LengthKm * edge.CostFactor;

    /// <summary><see cref="TravelKm"/> in pathfinder cost units (km / km-per-cost-unit).</summary>
    public static double TravelCostUnits(in TransportEdgeRow edge, double kmPerCostUnit) => TravelKm(edge) / kmPerCostUnit;

    /// <summary>Does a route row take part in road travel (Road mode, complete)?</summary>
    public static bool IsTravelled(in TransportEdgeRow edge) =>
        edge.Mode == TransportModes.Road && edge.State == TransportEdgeStates.Complete;

    /// <summary>
    /// The combined transport-network revision catchments key their staleness on: PathBuild's
    /// lattice-network revision plus the length of the append-only road-development log. Both
    /// terms only grow, and every road modernization appends exactly one log row, so the sum
    /// moves on every network change of either kind and on nothing else. With no road
    /// development it equals the NetworkMeta revision exactly (worlds without roads unchanged).
    /// </summary>
    public static int NetworkRevision(IReadOnlyWorldState world) =>
        (world.NetworkMeta.Count == 0 ? 0 : world.NetworkMeta[0].Revision) + world.RoadDevelopments.Count;

    private static double Clamp01(double f) => f <= 0.0 ? 0.0 : f >= 1.0 ? 1.0 : f;
}
