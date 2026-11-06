namespace Proto.M7.WarEcon;

/// <summary>
/// The two food-allocation interpretations the Director has deliberately NOT chosen between (DIR5 §2.11; D-079 DR27, Q3;
/// plan revision OD-3). There is NO default: 0 is invalid and every caller names one. WarEcon implements both and the
/// report measures both; it selects neither.
/// </summary>
public enum FoodOrdering : byte
{
    /// <summary>Civilian requirements -> military -> growth.</summary>
    CivilianMilitaryGrowth = 1,
    /// <summary>Civilian requirements -> growth -> military.</summary>
    CivilianGrowthMilitary = 2,
}

/// <summary>
/// One army's request on its polity's granary for one War Pulse, in DEPOT terms (what leaves the granary; carriage loss
/// already grossed up). Standing = food the army eats even when halted; Movement = the extra that marching costs (exertion,
/// working fodder); Restock = refilling the army's own train beyond this pulse; Wages = pay in kind (no coinage).
/// Ordered by the composite key (Priority ascending, ArmyId ascending): a total order with a stable integer tie-break.
/// </summary>
public readonly record struct MilitaryFoodRequest(int ArmyId, int Priority, long Standing, long Movement, long Restock, long Wages);

public readonly record struct MilitaryFoodGrant(int ArmyId, int Priority, long Standing, long Movement, long Restock, long Wages)
{
    public long Total => checked(Standing + Movement + Restock + Wages);
}

/// <summary>The furthest constraint stage reached (DIR5 §2.11 tiers).</summary>
public enum FoodStage : byte
{
    Unconstrained = 0,
    /// <summary>Tier 1: growth received less than its demand; every military standing and movement request was met.</summary>
    GrowthReduced = 1,
    /// <summary>Tier 2: a movement request was cut; every standing request was met.</summary>
    MovementRestricted = 2,
    /// <summary>Tier 3 (last resort): a standing request was cut — soldiers go short of the home supply.</summary>
    StandingShort = 3,
    /// <summary>The granary cannot cover the civilian reserve even with no military draw at all; the military gets nothing.</summary>
    CivilianShortfall = 4,
}

/// <summary>
/// The decision record. CONSERVATION (exact, long): CivilianProtected + GrowthLeft + MilitaryGranted + Unallocated == Free.
/// GrowthLeft is the stock left in the granary for growth (growth itself is realised at the campaign settlement).
/// </summary>
public sealed record FoodAllocation(
    FoodOrdering Ordering, long Free, long CivilianReserve, long CivilianProtected, long CivilianShortfall,
    long GrowthDemand, long GrowthLeft, long MilitaryRequested, long MilitaryGranted, long Unallocated, FoodStage Stage,
    MilitaryFoodGrant[] Grants)
{
    public long GrowthCut => GrowthDemand - GrowthLeft;
}

/// <summary>
/// THE AUTOMATIC PRIORITY for a polity's granary at a War Pulse start (DIR5 §2.11, "automatic, not player-controlled"):
/// when food is constrained, (1) population growth reduces first, (2) movement is then restricted before starvation
/// occurs, (3) starvation is the last resort. Pure integer function; no state, no RNG, no IEEE arithmetic.
///
/// ALGORITHM. Civilians first under both orderings: the civilian reserve (what civilians still need until the next
/// campaign settlement, net of the harvest still to come) is never available to the military. avail = Free − reserve.
///   CivilianMilitaryGrowth: every army's Standing (in key order), then every army's Movement, then growth keeps up to its
///     demand, then Restock, then Wages. A movement cut therefore implies growth got nothing, and a standing cut implies
///     every movement request got nothing: exactly tiers 1 → 2 → 3.
///   CivilianGrowthMilitary: growth keeps up to its demand first, then Standing, Movement, Restock, Wages. Under this
///     ordering tier 1 does NOT hold for the military draw (movement is cut while growth is protected); this is a measured
///     property of the interpretation (reports/H-E.md §5), not a defect, and it is not a choice.
/// Restock and in-kind Wages are not food the army eats this pulse, so under both orderings they come after growth.
/// Within each pass the armies are served in (Priority, ArmyId) order; a request that does not fit whole receives the
/// remainder and every later request in that pass receives nothing.
/// </summary>
public static class WarFoodAllocator
{
    public static FoodAllocation Allocate(FoodOrdering ordering, long free, long civilianReserve, long growthDemand,
                                          ReadOnlySpan<MilitaryFoodRequest> requests)
    {
        if (ordering != FoodOrdering.CivilianMilitaryGrowth && ordering != FoodOrdering.CivilianGrowthMilitary)
            throw new ArgumentOutOfRangeException(nameof(ordering), "a food ordering must be named (no default)");
        ArgumentOutOfRangeException.ThrowIfNegative(free);
        ArgumentOutOfRangeException.ThrowIfNegative(civilianReserve);
        ArgumentOutOfRangeException.ThrowIfNegative(growthDemand);

        MilitaryFoodRequest[] req = requests.ToArray();
        long requested = 0;
        for (int i = 0; i < req.Length; i++)
        {
            var r = req[i];
            if (r.Standing < 0 || r.Movement < 0 || r.Restock < 0 || r.Wages < 0) throw new ArgumentOutOfRangeException(nameof(requests));
            for (int j = 0; j < i; j++) if (req[j].ArmyId == r.ArmyId) throw new ArgumentException("duplicate army id in one allocation");
            requested = checked(requested + r.Standing + r.Movement + r.Restock + r.Wages);
        }
        Array.Sort(req, CompareKey);

        long civilianProtected = Math.Min(free, civilianReserve);
        long civilianShortfall = civilianReserve - civilianProtected;
        long avail = free - civilianProtected;

        var g = new long[req.Length, 4];
        long growthLeft = 0;
        if (civilianShortfall == 0)
        {
            if (ordering == FoodOrdering.CivilianGrowthMilitary) { growthLeft = Math.Min(avail, growthDemand); avail -= growthLeft; }
            avail = Pass(req, g, 0, avail);
            avail = Pass(req, g, 1, avail);
            if (ordering == FoodOrdering.CivilianMilitaryGrowth) { growthLeft = Math.Min(avail, growthDemand); avail -= growthLeft; }
            avail = Pass(req, g, 2, avail);
            avail = Pass(req, g, 3, avail);
        }

        var grants = new MilitaryFoodGrant[req.Length];
        long granted = 0;
        bool standingShort = false, movementShort = false;
        for (int i = 0; i < req.Length; i++)
        {
            grants[i] = new MilitaryFoodGrant(req[i].ArmyId, req[i].Priority, g[i, 0], g[i, 1], g[i, 2], g[i, 3]);
            granted = checked(granted + grants[i].Total);
            standingShort |= g[i, 0] < req[i].Standing;
            movementShort |= g[i, 1] < req[i].Movement;
        }
        FoodStage stage = civilianShortfall > 0 ? FoodStage.CivilianShortfall
            : standingShort ? FoodStage.StandingShort
            : movementShort ? FoodStage.MovementRestricted
            : growthLeft < growthDemand ? FoodStage.GrowthReduced
            : FoodStage.Unconstrained;

        var a = new FoodAllocation(ordering, free, civilianReserve, civilianProtected, civilianShortfall, growthDemand, growthLeft,
                                   requested, granted, avail, stage, grants);
        if (checked(a.CivilianProtected + a.GrowthLeft + a.MilitaryGranted + a.Unallocated) != free)
            throw new InvalidOperationException("food allocation does not conserve the free grain");
        if (a.MilitaryGranted > 0 && a.CivilianShortfall > 0)
            throw new InvalidOperationException("military granted while the civilian reserve is short");
        return a;
    }

    private static long Pass(MilitaryFoodRequest[] req, long[,] g, int tier, long avail)
    {
        for (int i = 0; i < req.Length; i++)
        {
            long want = tier switch { 0 => req[i].Standing, 1 => req[i].Movement, 2 => req[i].Restock, _ => req[i].Wages };
            long take = Math.Min(want, avail);
            g[i, tier] = take;
            avail -= take;
        }
        return avail;
    }

    /// <summary>The composite total order (Priority, ArmyId).</summary>
    public static int CompareKey(MilitaryFoodRequest a, MilitaryFoodRequest b)
    {
        int c = a.Priority.CompareTo(b.Priority);
        return c != 0 ? c : a.ArmyId.CompareTo(b.ArmyId);
    }
}
