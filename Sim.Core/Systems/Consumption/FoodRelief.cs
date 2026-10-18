namespace Sim.Core.Systems.Consumption;

/// <summary>
/// One settlement's claim on its polity's spare grain in a shortage (intra-polity grain relief, D-053 §2):
/// <paramref name="Required"/> is its nutritional requirement this turn (ConsumptionDeficitRow.DemandUnits,
/// whole person-year-equivalents), <paramref name="Unmet"/> what its own store could not supply after
/// substitution, and <paramref name="Severity"/> its PREV food classification (FoodStateKind as an int,
/// Normal 0 … Famine 3). Severity is ONLY a tie-break between two claims at the SAME exact unmet fraction, and it
/// is one turn LAGGED: it is the state the previous step classified, not the state the demographic kernel will
/// apply to this step's deficit (an abandonment ordered this turn still reads Normal here). It says how badly a
/// settlement went short last turn — FoodState records no duration, so it says nothing about how long.
/// </summary>
public readonly record struct FoodClaim(int Settlement, long Required, long Unmet, int Severity);

/// <summary>
/// INTRA-POLITY GRAIN RELIEF — the pure rule that divides a polity's spare grain among its hungry
/// settlements in a shortage (D-053 §2; a partial step toward D-051 §6, not the central stockpile; sim.json
/// <c>consumption.empireFoodAllocation</c>; the caller is ConsumptionSystem.RelieveShortages).
///
/// THE URGENCY METRIC is the NOMINAL DEFICIT, the unmet fraction of a settlement's own nutritional
/// requirement, f = Unmet / Required. It is not a new quantity: it is exactly the deficit ratio d the
/// simulation already records (ConsumptionDeficitRow.DeficitRatio), and it is the INPUT to
/// <see cref="Sim.Core.State.FoodState.EffectiveDeficit"/>, which is what starvation mortality and fertility
/// suppression read. It is NOT the effective deficit, and what that means is stated: WITHIN ONE FOOD STATE
/// the effective deficit is monotone non-decreasing in d, so ranking by d ranks exactly by starvation risk;
/// ACROSS FOOD STATES it does not — FAMINE reads the whole d while NORMAL/STRESS/SEVERE first absorb a
/// shortfall a (shipped 0.20) and read (d − a)/(1 − a), so a FAMINE-class settlement at d = 0.25 (effective
/// 0.25) ranks BELOW a NORMAL one at d = 0.30 (effective 0.125). The PREV food state enters only as the
/// one-turn-lagged tie-break of the composite key below. This is a stated choice, put to the
/// Director: the food state the kernel applies is classified from tables other systems write in this same
/// step (the disaster applied in this step's production, the sector row in force), so ranking by the
/// effective deficit would need a cross-system read or a one-turn-lagged guess. Proportional-to-demand
/// division would hand a large, mildly short city more than a small, starving village; equal shares would
/// ignore need altogether.
///
/// THE RULE: MAX-MIN WATER-FILLING ON f. Food goes first to the settlement facing the largest unmet
/// fraction, bringing it down to the next-worst, then both together to the next, and so on, until the
/// food runs out or everyone is fed. Continuously, every settlement ends at min(f_i, L) for the one level
/// L at which the food is exactly spent (L = 0: everyone fed). So the worst-off is always served first,
/// and no settlement ends worse-off than another unless it started better-off.
///
/// WHOLE UNITS. Grain is a conserved <c>long</c>; a share is floor(u_i − L·R_i), computed EXACTLY in
/// integer arithmetic (the level is the rational (U_k − A)/R_k of the served group; no double touches the
/// allocation). The fewer-than-k units the floors leave go ONE AT A TIME, each to the claim ranked first
/// by the COMPOSITE KEY: (remaining unmet fraction DESC — compared exactly by cross-multiplication
/// u'_i·R_j vs u'_j·R_i — then PREV severity DESC, then settlement id ASC). Rounding is therefore bounded
/// and stated: every grant is within one unit of its continuous share, so a served settlement ends within
/// one unit of the level, and no settlement is left worse than a served one by more than one unit of each.
///
/// GUARANTEES (each asserted by Sim.Tests/Systems/EmpireFoodAllocationTests): Σ grants ≤ min(available,
/// Σ unmet); 0 ≤ grant_i ≤ Unmet_i; with available ≥ Σ unmet every claim is met in full; with available
/// = 0 nothing moves; the result is a function of the MULTISET of claims (ids are unique, the composite
/// key is a total order, so input order cannot matter — law 5); no double, no dictionary, no LINQ.
/// </summary>
public static class FoodRelief
{
    /// <summary>
    /// Divides <paramref name="available"/> whole units among <paramref name="claims"/> by max-min
    /// water-filling on the unmet fraction. Returns one grant per claim, index-aligned with the input.
    /// Throws on a malformed claim (Required ≤ 0, Unmet outside [1, Required], negative severity,
    /// duplicate settlement id) or a negative supply — the caller never forms one.
    /// </summary>
    public static long[] Allocate(ReadOnlySpan<FoodClaim> claims, long available)
    {
        if (available < 0) throw new ArgumentOutOfRangeException(nameof(available), available, "food supply is never negative.");
        int n = claims.Length;
        var grants = new long[n];
        if (n == 0) return grants;

        long totalUnmet = 0;
        for (int i = 0; i < n; i++)
        {
            FoodClaim c = claims[i];
            if (c.Required <= 0 || c.Unmet <= 0 || c.Unmet > c.Required || c.Severity < 0)
                throw new ArgumentException(
                    $"food claim for settlement {c.Settlement} is malformed (required {c.Required}, unmet {c.Unmet}, severity {c.Severity}).",
                    nameof(claims));
            totalUnmet = checked(totalUnmet + c.Unmet);
        }

        // Ids must be unique (the composite key is a total order only then), checked before any early return.
        var ids = new int[n];
        for (int i = 0; i < n; i++) ids[i] = claims[i].Settlement;
        Array.Sort(ids);
        for (int i = 1; i < n; i++)
            if (ids[i - 1] == ids[i]) throw new ArgumentException($"settlement {ids[i]} claims twice.", nameof(claims));

        long budget = Math.Min(available, totalUnmet);
        if (budget == 0) return grants;
        if (budget == totalUnmet)
        {
            // Enough for everyone: every claim met in full (L = 0).
            for (int i = 0; i < n; i++) grants[i] = claims[i].Unmet;
            return grants;
        }

        // Rank by the composite key on the ORIGINAL unmet fraction. Ties in f do not change the
        // continuous shares (a tie at the boundary sits exactly at the level and receives 0), so the
        // order only has to be total — which (f, severity, id) is, given unique ids.
        FoodClaim[] copy = claims.ToArray();
        var order = new int[n];
        for (int i = 0; i < n; i++) order[i] = i;
        Array.Sort(order, (x, y) => Compare(copy[x], copy[x].Unmet, copy[y], copy[y].Unmet));

        // The served group: the smallest prefix k whose level L_k = (U_k − A) / R_k is at or above the next
        // claim's fraction (or the whole list). Every quantity is an exact integer.
        Int128 unmetSum = 0, requiredSum = 0, excess = 0;
        int k = 0;
        while (k < n)
        {
            FoodClaim c = copy[order[k]];
            unmetSum += c.Unmet;
            requiredSum += c.Required;
            k++;
            excess = unmetSum - budget;                     // P = U_k − A (the level is P / R_k)
            if (k == n) break;
            FoodClaim next = copy[order[k]];
            // L_k ≥ f_next  ⇔  P · R_next ≥ u_next · R_k   (and L_k ≥ 0)
            if (excess >= 0 && checked(excess * next.Required) >= checked((Int128)next.Unmet * requiredSum)) break;
        }
        if (excess <= 0)
            throw new InvalidOperationException("food allocation: the served group's level is not positive although supply is short.");

        long given = 0;
        for (int j = 0; j < k; j++)
        {
            int i = order[j];
            FoodClaim c = copy[i];
            // grant = floor(u_i − L·R_i) = floor((u_i·R_k − P·R_i) / R_k), non-negative because f_i ≥ L.
            Int128 numerator = checked((Int128)c.Unmet * requiredSum - excess * c.Required);
            if (numerator < 0)
                throw new InvalidOperationException($"food allocation: settlement {c.Settlement} sits below the water level.");
            long share = (long)(numerator / requiredSum);
            if (share > c.Unmet)
                throw new InvalidOperationException($"food allocation: settlement {c.Settlement} granted more than it lacks.");
            grants[i] = share;
            given = checked(given + share);
        }

        // The floors leave fewer than k whole units; each goes to the claim ranked first on what it STILL
        // lacks. A claim that received a unit drops below the level and so never outranks one that did not.
        long leftover = budget - given;
        if (leftover < 0 || leftover >= k)
            throw new InvalidOperationException($"food allocation: {leftover} leftover units for a served group of {k}.");
        for (; leftover > 0; leftover--)
        {
            int best = -1;
            for (int i = 0; i < n; i++)
            {
                if (copy[i].Unmet - grants[i] <= 0) continue;
                if (best < 0 || Compare(copy[i], copy[i].Unmet - grants[i], copy[best], copy[best].Unmet - grants[best]) < 0)
                    best = i;
            }
            if (best < 0) throw new InvalidOperationException("food allocation: leftover units with every claim met.");
            grants[best]++;
        }
        return grants;
    }

    /// <summary>
    /// THE COMPOSITE KEY, as a comparison where "less" means "served first": remaining unmet fraction
    /// DESC (exact: <paramref name="ux"/>·R_y vs <paramref name="uy"/>·R_x, never a double), then PREV
    /// severity DESC, then settlement id ASC — a stable integer tie-break, so the order is total.
    /// </summary>
    public static int Compare(FoodClaim x, long ux, FoodClaim y, long uy)
    {
        Int128 lhs = (Int128)ux * y.Required, rhs = (Int128)uy * x.Required;
        if (lhs != rhs) return lhs > rhs ? -1 : 1;
        if (x.Severity != y.Severity) return x.Severity > y.Severity ? -1 : 1;
        return x.Settlement.CompareTo(y.Settlement);
    }
}
