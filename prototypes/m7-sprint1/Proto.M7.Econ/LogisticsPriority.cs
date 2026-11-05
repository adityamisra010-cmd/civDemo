namespace Proto.M7.Econ;

/// <summary>One requested military move for the decision period. <c>Draw</c> = the grain the WHOLE move consumes
/// (MovementEstimate.TotalUnits), <c>Days</c> its length; a move may be SHORTENED to fewer days, drawing pro rata.
/// Ordered by the composite key (Priority ascending, Id ascending) — a total order with a stable integer tie-break.</summary>
public readonly record struct MoveRequest(long Id, int Priority, long Draw, long Days);

public enum MoveOutcome
{
    Granted = 1,
    Shortened = 2,
    Denied = 3,
}

/// <summary>The decision on one move: granted grain and days (≤ requested), and why.</summary>
public readonly record struct MoveDecision(long Id, int Priority, long Requested, long RequestedDays, long Granted, long GrantedDays, MoveOutcome Outcome);

/// <summary>The furthest stage the automatic priority had to reach this period.</summary>
public enum ConstraintStage
{
    /// <summary>Everything asked for was granted.</summary>
    Unconstrained = 0,
    /// <summary>Population growth was reduced (stage 1); every military move was still granted in full.</summary>
    GrowthReduced = 1,
    /// <summary>Growth is at zero and military moves were shortened or denied (stage 2).</summary>
    MilitaryRestricted = 2,
    /// <summary>Free grain does not cover the civilian requirement even with no growth and no movement: a shortfall that
    /// exists WITHOUT any military draw (military got nothing; the shortfall is not caused by the military).</summary>
    CivilianShortfall = 3,
}

/// <summary>
/// The decision record. CONSERVATION (exact, longs): CivilianGranted + GrowthGranted + MilitaryGranted + Unallocated == Free.
/// MilitaryCausedStarvation is structurally 0 and asserted: the allocator never grants a military unit out of the civilian
/// requirement.
/// </summary>
public sealed record LogisticsDecision(
    long Free,
    long CivilianRequirement,
    long CivilianGranted,
    long CivilianShortfall,
    long GrowthDemand,
    long GrowthGranted,
    long MilitaryRequested,
    long MilitaryGranted,
    long Unallocated,
    ConstraintStage Stage,
    MoveDecision[] Moves)
{
    public long GrowthCut => GrowthDemand - GrowthGranted;
    public long MilitaryCausedStarvation => 0;
}

/// <summary>
/// THE AUTOMATIC PRIORITY (Director, 2026-11-04 brief, Logistics): when grain is constrained, (1) population GROWTH is
/// reduced first, (2) then military movement is restricted, (3) military consumption never causes civilian starvation.
/// No player input. Pure integer function — no state, no RNG, no floating point.
///
/// ALGORITHM (one decision period):
/// <list type="number">
/// <item>Civilians first: CivilianGranted = min(Free, CivilianRequirement). Any remainder of the requirement is a shortfall
/// that exists with zero military draw (stage CivilianShortfall); military and growth then get nothing.</item>
/// <item>Surplus S = Free − CivilianRequirement. Military moves are served from S BEFORE growth — so a constraint lands on
/// growth first (stage GrowthReduced). Moves are served in (Priority, Id) order; a move that does not fit whole is
/// SHORTENED to the most whole days the remaining grain pays for (granted = ceil(days' × Draw / Days) ≤ remaining), and a
/// move for which not one day fits is DENIED (stage MilitaryRestricted). Once one move is shortened or denied every later
/// move in the order is denied (no lower-priority move overtakes a restricted higher-priority one).</item>
/// <item>Growth gets what is left, up to its demand — but nothing at all in a period where a move was restricted (the
/// sub-day residue of a shortened move stays in store); the rest is Unallocated (stays in store).</item>
/// </list>
/// The "explicit (open) rule" under which military draw MAY starve civilians is NOT implemented: whether such a rule exists
/// is an open Director question; this allocator implements only the never-starve branch.
/// </summary>
public static class LogisticsPriority
{
    public static LogisticsDecision Resolve(long freeGrain, long civilianRequirement, long growthHeadroomDemand, ReadOnlySpan<MoveRequest> moves)
    {
        ArgumentOutOfRangeException.ThrowIfNegative(freeGrain);
        ArgumentOutOfRangeException.ThrowIfNegative(civilianRequirement);
        ArgumentOutOfRangeException.ThrowIfNegative(growthHeadroomDemand);

        MoveRequest[] order = moves.ToArray();
        for (int i = 0; i < order.Length; i++)
        {
            if (order[i].Draw < 0 || order[i].Days <= 0)
                throw new ArgumentOutOfRangeException(nameof(moves), "a move needs Draw >= 0 and Days >= 1");
            for (int j = 0; j < i; j++)
                if (order[j].Id == order[i].Id) throw new ArgumentException("duplicate move id " + order[i].Id.ToString(System.Globalization.CultureInfo.InvariantCulture));
        }
        Array.Sort(order, CompareKey);

        long requested = 0;
        for (int i = 0; i < order.Length; i++) requested = checked(requested + order[i].Draw);

        long civilian = Math.Min(freeGrain, civilianRequirement);
        long shortfall = civilianRequirement - civilian;
        long remaining = freeGrain - civilian;   // = max(0, Free − R)

        var decisions = new MoveDecision[order.Length];
        long militaryGranted = 0;
        bool restricted = false;
        for (int i = 0; i < order.Length; i++)
        {
            MoveRequest m = order[i];
            long granted, days;
            MoveOutcome outcome;
            if (shortfall > 0 || restricted)
            {
                granted = 0; days = 0; outcome = MoveOutcome.Denied;
            }
            else if (m.Draw <= remaining)
            {
                granted = m.Draw; days = m.Days; outcome = MoveOutcome.Granted;
            }
            else
            {
                // The most whole days the remaining grain pays for: floor(remaining × Days / Draw) (Draw > remaining ≥ 0).
                days = (long)((Int128)remaining * m.Days / m.Draw);
                granted = days == 0 ? 0 : (long)(((Int128)days * m.Draw + m.Days - 1) / m.Days);   // ceil, ≤ remaining
                outcome = days == 0 ? MoveOutcome.Denied : MoveOutcome.Shortened;
                restricted = true;
            }
            remaining -= granted;
            militaryGranted = checked(militaryGranted + granted);
            decisions[i] = new MoveDecision(m.Id, m.Priority, m.Draw, m.Days, granted, days, outcome);
        }

        // A restricted period gives growth NOTHING (growth is cut to zero before any move is restricted): the sub-day
        // residue a shortened move could not use stays in store (Unallocated), it is not handed back to growth.
        long growth = restricted || shortfall > 0 ? 0 : Math.Min(remaining, growthHeadroomDemand);
        remaining -= growth;

        ConstraintStage stage = shortfall > 0 ? ConstraintStage.CivilianShortfall
            : restricted ? ConstraintStage.MilitaryRestricted
            : growth < growthHeadroomDemand ? ConstraintStage.GrowthReduced
            : ConstraintStage.Unconstrained;

        var decision = new LogisticsDecision(freeGrain, civilianRequirement, civilian, shortfall, growthHeadroomDemand, growth,
            requested, militaryGranted, remaining, stage, decisions);
        if (checked(decision.CivilianGranted + decision.GrowthGranted + decision.MilitaryGranted + decision.Unallocated) != freeGrain)
            throw new InvalidOperationException("allocation does not conserve the free grain");
        if (decision.MilitaryGranted > 0 && decision.CivilianGranted < civilianRequirement)
            throw new InvalidOperationException("military granted while civilians are short");
        return decision;
    }

    /// <summary>The composite total order (Priority, Id).</summary>
    public static int CompareKey(MoveRequest a, MoveRequest b)
    {
        int c = a.Priority.CompareTo(b.Priority);
        return c != 0 ? c : a.Id.CompareTo(b.Id);
    }
}
