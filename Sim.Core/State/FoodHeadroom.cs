using Sim.Core.Systems.Consumption;

namespace Sim.Core.State;

/// <summary>
/// T4.21-1 (§3.6a) — ONE definition of the FOOD-INFLUX LIMIT, shared by
/// Demographics (the headroom growth cap), Migration (the vacancy bound) and the
/// observer, so no two callers can drift onto two readings of one quantity.
/// Static, pure, non-serialized; every read is a table-order scan over the
/// world passed in, which callers pass as PREV.
///
/// WHAT IT IS. The number of adult-equivalent persons per year that last turn's
/// FEEDABLE food influx feeds — feedable, not the sum of food goods: the exact
/// mirror of ConsumptionSystem's one-directional substitution. At the limit
/// population the staple covers its own basket share plus every non-staple
/// shortfall; a non-staple can cover only its own share (a livestock surplus is
/// not bread). With S the staple's LastProducedUnits, NS_g each non-staple food
/// good's LastProducedUnits and b_g its realised basket share
/// (LastConsumptionDemandUnits / DemandUnits — the STANDING population's mix),
/// the feedable food at the limit population X is the fixed point of
/// Consumption's pass 1 / pass 2 evaluated at demand X:
///
///     X = S + Σ_g min(NS_g, b_g · X)
///
/// solved by the deterministic short/surplus partition: start with every
/// non-staple in surplus, X = (S + Σ_short NS_g) / (1 − Σ_surplus b_g); move
/// every surplus good with NS_g &lt; b_g·X to short; repeat until no move. X only
/// falls, so the result is order-independent; the scan runs in registry order
/// (BasketBook.FoodGoods, ascending good id) and sorts no doubles. When every
/// non-staple is in surplus the closed form is X = S/(1 − Σ b_g) = S/b_staple
/// (Libur t110: 7458/0.9 ≈ 8287, ρ_eff = 1.32); when every non-staple is short,
/// X = S + Σ NS_g. The conservative form S + Σ min(NS_g, b_g·D) under-states the
/// limit by Σ b_g·(X − D) — about 10% of the headroom in the Default mix — so the
/// exact fixed point is used.
///
/// ABSOLUTE, NOT A RATIO. N_lim = X / dt_prev, with dt_prev read from the same
/// PREV turn's SettlementVitalsRow.DtYears so an era-boundary turn reads the dt
/// that produced X, not the current one. Equivalently N_lim = N_nutr(t−1) × X/D.
/// Weather (and a disaster) is inside S — the realised multiplier; the V ≥ 0
/// clamp makes a bad draw harmless to the cap.
///
/// NULL / IDENTITY ARM: +∞ whenever the inputs are absent — no deficit row,
/// DemandUnits == 0 (every hand-built exactness rig: `new ConsumptionDeficitRow(s, d)`
/// defaults DemandUnits to 0), no vitals row, or NO CATCHMENT SUMMARY ROW — so
/// every founding turn and every existing rig is untouched by construction, not
/// by tuning.
///
/// THE CATCHMENT ARM (T4.21-4, RULE 2; ADR-025 §null-arm, ADR-026 §null-arm).
/// The null arm's meaning is "the influx is UNMEASURED", and a settlement whose
/// catchment has never been computed could not have produced: `ProductionSystem.Farm`
/// reads `prev.CatchmentSummaries` for its arable (ProductionSystem.cs:211-216), so
/// with no row the land side is 0 and the staple harvest is 0 — an S of 0 that is a
/// STRUCTURAL UNAVAILABILITY of the measurement, not a measured zero capacity.
/// Keyed on ROW ABSENCE ONLY — never on "production == 0", which is the ABANDONED
/// settlement's genuine zero influx and must keep N_lim = 0 (D_Cap_NoGrowthOnAGranary
/// and the abandonment semantics depend on it; CatchmentSystem emits a row per
/// settlement in `prev.Settlements` regardless of labour or sectors, so an abandoned
/// settlement in a real world always has one).
///
/// MEASURED SCOPE OF THIS ARM (T4.21-4, canonical founded seed 42, 300 turns): it
/// does NOT restore the turn-2 gap flows. Catchment is pipeline entry 1 and writes
/// into NEXT, so the turn-1 world — which is PREV on turn 2 — already carries a row
/// for every founded settlement; the turn-2 refusal comes from that row being
/// PRESENT alongside the turn-1 zero staple harvest, which this key is forbidden to
/// read. The arm therefore fires only where a settlement is genuinely younger than
/// its first catchment recompute. See ADR-025 §null-arm for the residual and the
/// queue line that carries it.
/// </summary>
public static class FoodHeadroom
{
    /// <summary>N_lim in adult-equivalent persons per year; +∞ on the null arm.
    /// <paramref name="cohortWeights"/> is part of the shared signature so the
    /// three callers pass one argument list; only <see cref="Vacancy"/> reads it.</summary>
    public static double Limit(
        IReadOnlyWorldState prev, SettlementId s, double[] cohortWeights, BasketBook baskets)
    {
        _ = cohortWeights;

        // ROW ABSENCE of the catchment summary ⇒ the influx is unmeasured (see the
        // type comment). Deliberately NOT "arable == 0" and NOT "production == 0":
        // an abandoned settlement keeps its row and keeps N_lim = 0.
        bool haveCatchment = false;
        for (int i = 0; i < prev.CatchmentSummaries.Count; i++)
        {
            if (prev.CatchmentSummaries[i].Settlement != s) continue;
            haveCatchment = true;
            break;
        }
        if (!haveCatchment) return double.PositiveInfinity;

        long demand = 0;
        bool haveDemand = false;
        for (int i = 0; i < prev.ConsumptionDeficits.Count; i++)
        {
            if (prev.ConsumptionDeficits[i].Settlement != s) continue;
            demand = prev.ConsumptionDeficits[i].DemandUnits;
            haveDemand = true;
            break;
        }
        if (!haveDemand || demand <= 0) return double.PositiveInfinity;

        double dtPrev = 0.0;
        bool haveVitals = false;
        for (int i = 0; i < prev.SettlementVitals.Count; i++)
        {
            if (prev.SettlementVitals[i].Settlement != s) continue;
            dtPrev = prev.SettlementVitals[i].DtYears;
            haveVitals = true;
            break;
        }
        if (!haveVitals || !(dtPrev > 0.0)) return double.PositiveInfinity;

        return FeedableAtLimit(prev, s, baskets, demand) / dtPrev;
    }

    /// <summary>max(0, Limit − N_nutr), N_nutr = Σ cohortWeights[c] × count over
    /// the settlement's PREV buckets (every class). +∞ when Limit is +∞.</summary>
    public static double Vacancy(
        IReadOnlyWorldState prev, SettlementId s, double[] cohortWeights, BasketBook baskets)
    {
        double limit = Limit(prev, s, cohortWeights, baskets);
        if (double.IsPositiveInfinity(limit)) return limit;

        double nutritional = 0.0;
        for (int i = 0; i < prev.Buckets.Count; i++)
        {
            BucketRow bucket = prev.Buckets[i];
            if (bucket.Settlement != s) continue;
            nutritional += cohortWeights[bucket.CohortIdx] * bucket.Count.Value;
        }
        return Math.Max(0.0, limit - nutritional);
    }

    /// <summary>X — feedable food units at the limit population, this turn's
    /// dt inside (the fixed point above). Exposed for the observer and the tests
    /// that pin the substitution mirror; +∞ when the basket leaves the staple no
    /// share at all (Σ b_g ≥ 1, unreachable with the shipped baskets).</summary>
    public static double FeedableAtLimit(
        IReadOnlyWorldState prev, SettlementId s, BasketBook baskets, long demand)
    {
        ReadOnlySpan<GoodId> foods = baskets.FoodGoods;
        GoodId staple = baskets.Staple;
        int n = foods.Length;

        // Per food good (registry order): produced units and realised share.
        // Fixed-size locals — at most a handful of food goods; no allocation.
        Span<double> produced = n <= 16 ? stackalloc double[n] : new double[n];
        Span<double> share = n <= 16 ? stackalloc double[n] : new double[n];
        Span<bool> surplus = n <= 16 ? stackalloc bool[n] : new bool[n];
        double stapleProduced = 0.0;
        for (int g = 0; g < n; g++)
        {
            long lastProduced = 0, lastDemand = 0;
            int row = GoodStockIndex.IndexOf(prev.GoodStocks, s, foods[g]);
            if (row >= 0)
            {
                lastProduced = prev.GoodStocks[row].LastProducedUnits;
                lastDemand = prev.GoodStocks[row].LastConsumptionDemandUnits;
            }
            if (foods[g] == staple)
            {
                stapleProduced = lastProduced;
                surplus[g] = false;      // the staple is never a "non-staple in surplus"
                continue;
            }
            produced[g] = lastProduced;
            share[g] = lastDemand / (double)demand;
            surplus[g] = true;
        }

        // The repeat-until-no-move partition: X only falls, so at most n moves.
        double x = 0.0;
        for (int pass = 0; pass <= n; pass++)
        {
            double numerator = stapleProduced;
            double surplusShare = 0.0;
            for (int g = 0; g < n; g++)
            {
                if (foods[g] == staple) continue;
                if (surplus[g]) surplusShare += share[g];
                else numerator += produced[g];
            }
            double stapleShare = 1.0 - surplusShare;
            if (!(stapleShare > 0.0)) return double.PositiveInfinity;
            x = numerator / stapleShare;

            bool moved = false;
            for (int g = 0; g < n; g++)
            {
                if (foods[g] == staple || !surplus[g]) continue;
                if (produced[g] < share[g] * x) { surplus[g] = false; moved = true; }
            }
            if (!moved) break;
        }
        return x;
    }
}
