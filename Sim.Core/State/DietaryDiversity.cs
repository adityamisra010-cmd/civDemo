using Sim.Core.Systems;
using Sim.Core.Systems.Consumption;

namespace Sim.Core.State;

/// <summary>
/// M4 — DIETARY DIVERSITY: how evenly spread the food a settlement ACTUALLY ATE
/// this turn was, and the small welfare contribution that evenness makes to
/// derived happiness. A PURE, DERIVED READING — no table, no row, no schema
/// change, no memory. Ask it twice about the same world and it answers the same.
///
/// THE QUANTITY. Over the ELIGIBLE SUSTENANCE GOODS (<see cref="BasketBook.FoodGoods"/>
/// — the distinct, ascending set of goods any class basket wants for Sustenance),
/// with cᵢ = this settlement's ACTUAL CONSUMED units of good i this turn and
/// pᵢ = cᵢ / Σc:
///
///     D = (1 − Σ pᵢ²) / (1 − 1/N),   N = the number of eligible Sustenance goods
///
/// — the NORMALISED SIMPSON diversity index, bounded 0 ≤ D ≤ 1: 0 when one food
/// supplies everything, 1 when all N are eaten in equal measure. The bonus is
/// <c>5 × D</c>, bounded 0..5, and it is ADDITIVE HAPPINESS POINTS inside the
/// existing 0..100 bound — not a percentage, not a multiplier, and not an input
/// to production, survival, migration or demographics.
///
/// THE INPUT IS EATEN, NOT WANTED. cᵢ is <see cref="GoodStockRow.LastConsumptionEatenUnits"/>:
/// the POST-CLAMP figure returned by the ledger flow that actually emptied the
/// store (ConsumptionSystem.cs:222-229), not the PRE-CLAMP
/// <see cref="GoodStockRow.LastConsumptionDemandUnits"/> beside it. So this
/// measures production, demand, stock and endowment only insofar as they show up
/// in what was swallowed. ConsumptionSystem zeroes both fields on every store row
/// each turn (ConsumptionSystem.cs:116-121), so a settlement that ate nothing
/// reads 0 rather than last turn's meal — which is what makes the empty-basket
/// rule below a property of the data and not an accident.
///
/// THE EMPTY BASKET SCORES ZERO, and this is the non-negotiable one. Σc ≤ 0 gives
/// D = 0 and a bonus of 0. Dietary diversity can never pay a starving settlement:
/// a term that is ADDED to happiness must be absent exactly when there is nothing
/// to be happy about. <see cref="SettlementHappiness.Of"/> additionally refuses to
/// add anything at all once its base score has reached zero, so total deprivation
/// stays at EXACTLY 0.
///
/// ═══ THIS IS NOT D-035-A VARIETY. ═══
/// <see cref="Sim.Core.Systems.NeedsGrievance.NeedsAggregation.VarietyFactor"/> is
/// a different quantity and the two must never be treated as duplicate bonuses.
/// D-035-A is a CONCENTRATION PENALTY INSIDE the satisfaction equation: its input
/// is the DECLARED basket scaled by fill, measured against the fixed nutritional
/// standard H*, it MULTIPLIES satisfaction, it can only ever REDUCE it, and it
/// returns 1.0 — no penalty — on an empty basket, because the quantity term it
/// multiplies is already zero. It lands in NeedSatisfactions and Grievance, which
/// under D-021 drive no behaviour before M5. It answers "how monotonous is this
/// household's diet relative to a decent one, and how much grievance does that
/// earn". Dietary diversity answers "how evenly spread was what this settlement
/// actually ate, and how much small welfare does that add". Different input
/// (fill-scaled declared basket vs actual eaten units), different formula
/// (Herfindahl excess over H*, weight 0.15, vs normalised Simpson), different N
/// (one class's declared basket length vs the eligible Sustenance good count),
/// different arithmetic role (multiplicative penalty vs additive point
/// contribution), OPPOSITE ZERO CASE (1.0 vs 0.0), different destination
/// (grievance vs happiness). Neither is the other's duplicate and neither may be
/// reimplemented in terms of the other. D-035-A's prohibition on a variety BONUS
/// MODIFIER is not contradicted: that forbids adding a variety bonus TO
/// SATISFACTION, which this does not do — it is a separate derived reading and
/// enters no satisfaction equation. See docs/d035-needs-aggregation.md.
///
/// DETERMINISM (§15). The eligible goods are a pre-sorted ascending array fixed at
/// construction, accumulation runs over them in that fixed order, no dictionary or
/// set is enumerated, there is no logarithm and no transcendental of any kind, and
/// the only floating-point operations are division, multiplication and addition of
/// doubles over long-valued inputs.
/// </summary>
public static class DietaryDiversity
{
    /// <summary>The bonus ceiling in HAPPINESS POINTS at D = 1 (director ruling,
    /// M4 §2). Additive inside the existing 0..100 bound; not a percentage.</summary>
    public const double MaxBonusPoints = 5.0;

    /// <summary>The upper bound on how many distinct Sustenance goods a stack
    /// buffer must hold. The shipped registry declares three; this is slack, and
    /// it is checked rather than assumed.</summary>
    public const int MaxEligibleFoodGoods = 64;

    /// <summary>
    /// The eligible Sustenance goods — distinct, ASCENDING by good id — written
    /// into <paramref name="into"/>; returns how many. This is the same set
    /// <see cref="BasketBook.FoodGoods"/> publishes and it is derived the same
    /// way, from the basket lines whose need is <see cref="BasketBook.SustenanceNeedId"/>.
    /// It is DATA-DEPENDENT: N is whatever needs.json declares, never a literal 3.
    /// </summary>
    public static int EligibleFoodGoods(SimConfig cfg, Span<GoodId> into)
    {
        ArgumentNullException.ThrowIfNull(cfg);
        NeedsConfig needs = cfg.Needs
            ?? throw new ArgumentException(
                "SimConfig.Needs is not loaded — dietary diversity reads the Sustenance baskets.",
                nameof(cfg));
        GoodsConfig goods = cfg.Goods
            ?? throw new ArgumentException(
                "SimConfig.Goods is not loaded — dietary diversity resolves basket goods to ids.",
                nameof(cfg));

        int n = 0;
        BasketEntry[] entries = needs.Baskets.Entries;
        for (int i = 0; i < entries.Length; i++)
        {
            if (entries[i].Need != BasketBook.SustenanceNeedId) continue;
            int id = goods.IdOf(entries[i].Good);
            if (id < 0) continue;   // BasketBook is the loader that rejects this loudly

            // Insertion into an ascending, duplicate-free run. No set, no sort
            // callback, no hash — the order is the array's own and is stable.
            int at = 0;
            while (at < n && into[at].Value < id) at++;
            if (at < n && into[at].Value == id) continue;
            if (n >= into.Length)
            {
                throw new ArgumentException(
                    $"more than {into.Length} distinct Sustenance goods are declared; "
                    + "the destination span is too small.", nameof(into));
            }
            for (int k = n; k > at; k--) into[k] = into[k - 1];
            into[at] = new GoodId(id);
            n++;
        }

        return n;
    }

    /// <summary>
    /// This settlement's ACTUAL CONSUMED units of each eligible good this turn,
    /// in <paramref name="eligible"/> order; returns the total. Conserved
    /// quantities, so <c>long</c> (law 7). Reads only the post-clamp eaten field.
    /// </summary>
    public static long Consumed(
        IReadOnlyWorldState world, SettlementId settlement,
        ReadOnlySpan<GoodId> eligible, Span<long> into)
    {
        ArgumentNullException.ThrowIfNull(world);
        if (into.Length < eligible.Length)
        {
            throw new ArgumentException(
                $"{eligible.Length} eligible goods do not fit in {into.Length} slots.", nameof(into));
        }

        for (int i = 0; i < eligible.Length; i++) into[i] = 0;

        long total = 0;
        for (int r = 0; r < world.GoodStocks.Count; r++)
        {
            GoodStockRow row = world.GoodStocks[r];
            if (row.Settlement != settlement) continue;
            long eaten = row.LastConsumptionEatenUnits;
            if (eaten <= 0) continue;
            for (int i = 0; i < eligible.Length; i++)
            {
                if (eligible[i] != row.Good) continue;
                into[i] += eaten;
                total += eaten;
                break;
            }
        }

        return total;
    }

    /// <summary>
    /// THE AUTHORITATIVE CALCULATION — normalised Simpson diversity over the
    /// consumption shares, bounded [0,1]. The one place the formula exists;
    /// every consumer, observer and UI calls this rather than restating it.
    ///
    /// <paramref name="consumed"/> is in eligible-good order and
    /// <paramref name="total"/> is its sum. Total ≤ 0 (the empty basket) returns
    /// 0. N ≤ 1 returns 0 — a single eligible good has no diversity dimension,
    /// and the (1 − 1/N) denominator is zero there.
    /// </summary>
    public static double Index(ReadOnlySpan<long> consumed, long total)
    {
        int n = consumed.Length;
        if (n <= 1) return 0.0;
        if (total <= 0) return 0.0;

        double denom = 1.0 - (1.0 / n);
        if (denom <= 0.0) return 0.0;

        double sumOfSquares = 0.0;
        for (int i = 0; i < n; i++)
        {
            if (consumed[i] <= 0) continue;
            double p = (double)consumed[i] / total;
            sumOfSquares += p * p;
        }

        // The clamp is for ROUNDOFF ONLY: Σp² is exactly 1 for a single food and
        // exactly 1/n for a perfectly even one, and double arithmetic can land a
        // few ulps either side of both. It is not a substitute for the rules above.
        return Math.Clamp((1.0 - sumOfSquares) / denom, 0.0, 1.0);
    }

    /// <summary>This settlement's dietary diversity in [0,1], from the world.</summary>
    public static double Of(IReadOnlyWorldState world, SettlementId settlement, SimConfig cfg)
    {
        Span<GoodId> eligible = stackalloc GoodId[MaxEligibleFoodGoods];
        int n = EligibleFoodGoods(cfg, eligible);
        if (n <= 1) return 0.0;

        Span<long> consumed = stackalloc long[MaxEligibleFoodGoods];
        long total = Consumed(world, settlement, eligible[..n], consumed[..n]);
        return Index(consumed[..n], total);
    }

    /// <summary>
    /// The happiness POINTS this settlement's dietary diversity contributes:
    /// <c>5 × D</c>, bounded 0..5. Additive; the caller owns the 0..100 bound and
    /// owns the deprivation guard.
    /// </summary>
    public static double Bonus(double index) =>
        Math.Clamp(MaxBonusPoints * Math.Clamp(index, 0.0, 1.0), 0.0, MaxBonusPoints);

    /// <summary>The bonus, from the world.</summary>
    public static double BonusOf(IReadOnlyWorldState world, SettlementId settlement, SimConfig cfg) =>
        Bonus(Of(world, settlement, cfg));
}
