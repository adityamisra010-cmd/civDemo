using Sim.Core.Systems;
using Sim.Core.Systems.NeedsGrievance;

namespace Sim.Core.State;

/// <summary>
/// One settlement's HAPPINESS on a 0..100 scale (T4.13, director ruling) — a
/// DERIVED READING, never a stock.
///
/// WHAT IT IS. Happiness is a proxy for how well a settlement's population is
/// materially provided for, recomputed from the world every time it is asked.
/// It does not accumulate, it does not decay, nothing integrates it, and it is
/// NOT SERIALIZED — there is no happiness row and no schema change. Ask it twice
/// about the same world and you get the same number; change the world and the
/// number changes with it, immediately and without memory. That is the whole
/// contract, and it is what makes "migration relieved the pressure" impossible
/// to fake: relief has to show up in the CONDITIONS or it does not show up here.
///
/// WHY IT IS NOT A STOCK, stated because the packet it replaces was. T4.13 was
/// originally "comfort-as-stock", a quantity depleted by use and replenished by
/// crafting. A stock can be granted: something hands you +5 and the population is
/// happier with no change in what it eats or where it sleeps. A derived reading
/// cannot — the only way to move it is to move a condition. The director's
/// prohibition on "migration occurred -> +X happiness" is therefore enforced by
/// the SHAPE of this type rather than by a rule someone has to remember.
///
/// WHAT IT READS, and the constraint that decides it. The obvious substrate is
/// NeedsGrievanceSystem's per-need satisfaction rows, which already express
/// exactly this idea and already aggregate by D-035-B. THIS TYPE DELIBERATELY
/// DOES NOT READ THEM. D-021 rules that grievance drives no BEHAVIOUR until M5,
/// and `scripts/check-read-isolation.sh` enforces that on the needs tables as
/// well as the grievance table. Since happiness feeds migration (a behaviour),
/// sourcing it from those rows would make needs state drive behaviour in M4 —
/// the precise thing D-021 defers. So happiness is computed from the PRIMARY
/// signals the needs system itself reads, using the SAME formulas, and the needs
/// tables stay isolated. The duplication is deliberate and is the cheaper of the
/// two prices; reusing the needs aggregate instead is a real improvement, and it
/// is a DIRECTOR'S CALL because it turns on D-021, not on engineering taste.
///
/// THE FACTORS, and the honesty about which exist. The director named food,
/// water, housing, clothing and taxation "where available". Measured against the
/// tree: FOOD and HOUSING are available and are used. WATER is not modelled at
/// all — there is no water good and no water need — so it is absent rather than
/// stubbed at 1.0, which would silently claim every settlement is well watered.
/// CLOTHING/comfort exists only as a basket-bound need computed inside the needs
/// system, so taking it would cross the D-021 line above; it is deferred with
/// the same reasoning. TAXATION arrives with M5, but NOT as a member of this
/// array — it is a burden on whatever provision exists rather than a provision
/// of its own, so it multiplies the aggregate; see <see cref="TaxSufficiency"/>.
/// Adding a provision factor later is one entry in <see cref="Factors"/> and its
/// weight — the extension seam is the array.
///
/// AGGREGATION IS NON-COMPENSATORY, and reuses the ratified equation rather than
/// inventing a second one: <see cref="NeedsAggregation.Aggregate"/>, D-035-B's
/// CES with sigma &lt; 1. That matters here more than anywhere. A weighted sum
/// would let a granary full of food buy off having nowhere to live; CES with
/// sigma = 0.5 makes the factors genuine COMPLEMENTS, so the worst factor
/// dominates and a settlement that is fed but unhoused is not "averagely fine".
/// This is a pure static function — not a system, not a table — so reading it
/// crosses no isolation boundary (law 6 governs systems referencing systems).
/// </summary>
public static class SettlementHappiness
{
    /// <summary>The scale. 0 is total deprivation, 100 is every factor fully met.</summary>
    public const double Max = 100.0;

    /// <summary>
    /// The revolt threshold (director ruling): happiness of exactly zero is a
    /// CONFIRMED revolt condition. Zero is reachable only when every factor is
    /// zero — an unfed, unhoused population — so this is not a near-miss band.
    /// </summary>
    public const double RevoltThreshold = 0.0;

    /// <summary>
    /// The factors, in registry order. Kept as an explicit enum-like block rather
    /// than an implicit tuple order so that adding "water" later is an additive
    /// change with a name attached, not a silent shift of array indices.
    /// </summary>
    public enum Factor
    {
        /// <summary>1 − the settlement's consumption deficit ratio.</summary>
        Food = 0,

        /// <summary>Dwelling capacity over population, capped at 1.</summary>
        Housing = 1,
    }

    /// <summary>
    /// How many PROVISION factors compose the reading today. Taxation is NOT one
    /// of them — see <see cref="TaxSufficiency"/> for why it multiplies the
    /// aggregate rather than joining it.
    /// </summary>
    public const int FactorCount = 2;

    /// <summary>
    /// The per-factor sufficiencies in [0,1], in <see cref="Factor"/> order —
    /// the EXPLANATION behind the score. A consumer that wants to say WHY a
    /// settlement is unhappy reads this, so the number never has to be taken on
    /// faith. Writes exactly <see cref="FactorCount"/> entries.
    /// </summary>
    public static void Factors(
        IReadOnlyWorldState world, SettlementId settlement, SimConfig cfg, Span<double> into)
    {
        if (into.Length < FactorCount)
        {
            throw new ArgumentException(
                $"happiness has {FactorCount} factors; the destination holds {into.Length}.",
                nameof(into));
        }

        into[(int)Factor.Food] = FoodSufficiency(world, settlement);
        into[(int)Factor.Housing] = HousingSufficiency(world, settlement, cfg);
    }

    /// <summary>
    /// FOOD: one minus the measured consumption deficit, clamped to [0,1].
    ///
    /// A settlement with NO deficit row reads 1.0 — no deficit has been measured,
    /// which is the same reading ConsumptionSystem's absence means everywhere
    /// else. It is not a claim that food is abundant; it is the absence of a
    /// shortfall, and treating it as 0 would make every world's first turn a
    /// famine before consumption has ever run.
    /// </summary>
    public static double FoodSufficiency(IReadOnlyWorldState world, SettlementId settlement)
    {
        for (int i = 0; i < world.ConsumptionDeficits.Count; i++)
        {
            if (world.ConsumptionDeficits[i].Settlement != settlement) continue;
            double deficit = world.ConsumptionDeficits[i].DeficitRatio;
            if (double.IsNaN(deficit)) return 0.0;   // unmeasurable is not "fine"
            return Math.Clamp(1.0 - deficit, 0.0, 1.0);
        }

        return 1.0;
    }

    /// <summary>
    /// HOUSING: dwelling capacity over population, capped at 1 — the SAME
    /// expression NeedsGrievanceSystem's Shelter need uses (T3.8), reproduced
    /// from the primary rows rather than read off its output, for the D-021
    /// reason in the type header. Nobody to house reads 1.0; people and no
    /// housing row at all reads 0.0, because they are genuinely unhoused.
    /// </summary>
    public static double HousingSufficiency(
        IReadOnlyWorldState world, SettlementId settlement, SimConfig cfg)
    {
        double personsPerDwelling = cfg.Housing?.PersonsPerDwelling
            ?? throw new ArgumentException(
                "SimConfig.Housing is not loaded — happiness reads persons-per-dwelling.",
                nameof(cfg));

        long pop = 0;
        for (int i = 0; i < world.Buckets.Count; i++)
            if (world.Buckets[i].Settlement == settlement) pop += world.Buckets[i].Count.Value;
        if (pop <= 0) return 1.0;

        for (int i = 0; i < world.Housing.Count; i++)
        {
            if (world.Housing[i].Settlement != settlement) continue;
            double capacity = world.Housing[i].Dwellings.Value * personsPerDwelling;
            return Math.Clamp(capacity / pop, 0.0, 1.0);
        }

        return 0.0;
    }

    /// <summary>
    /// M5 TAX BURDEN, as a sufficiency in [0,1]: one minus the EFFECTIVE rate.
    ///
    /// AN UNTAXED SETTLEMENT READS EXACTLY 1.0, and that is load-bearing rather
    /// than cosmetic. Every world founded before M5 — and every hand-built test
    /// world, and every settlement of an Empire that has never legislated a tax —
    /// has no policy row, so this factor contributes nothing and happiness is
    /// exactly what M4 computed. Returning 0.0 for "no policy on record" would
    /// instead read every such settlement as maximally burdened and, through the
    /// CES floor anchoring, could fire the revolt valve on turn one.
    ///
    /// The scale is deliberately linear and total: a state taking everything
    /// leaves nothing, and reads 0.0. That is reachable only at a declared rate of
    /// 100% AND full administrative reach — a real and rare corner, not a routine
    /// one, and one a government ought to be destroyed by.
    ///
    /// WHY THIS MULTIPLIES THE AGGREGATE INSTEAD OF JOINING IT AS A THIRD FACTOR.
    /// The first implementation made taxation a third CES factor, and the suite
    /// caught what that costs: an unfed, unhoused, UNTAXED settlement scored
    /// 2.31 rather than 0, because a third factor sitting at 1.0 lifts the
    /// floor-anchored aggregate off its floor. That silently disarms the ruled
    /// revolt condition — `happiness == 0` becomes unreachable, a dead predicate
    /// that reads as implemented — and it is the same class of mistake the
    /// anchoring comment in <see cref="Of"/> was written to prevent.
    ///
    /// As a MULTIPLIER the mechanism is exact in both directions. An untaxed
    /// realm multiplies by 1.0, so happiness is bit-identical to what M4
    /// computed and no pre-M5 world moves. A taxed one scales down everywhere on
    /// the curve, so the burden is felt at every level of provision rather than
    /// being traded off against a full granary. Total deprivation still lands on
    /// exactly 0 whatever the rate. This is a coefficient inside a resolution
    /// equation, which law 2 permits; it is NOT `happiness -= taxRate`, which the
    /// packet forbids and which would be a free-floating modifier.
    /// </summary>
    public static double TaxSufficiency(
        IReadOnlyWorldState world, SettlementId settlement, SimConfig cfg)
        => Math.Clamp(1.0 - Governance.EffectiveTaxRate(world, settlement, cfg), 0.0, 1.0);

    /// <summary>
    /// The settlement's happiness in [0, 100].
    ///
    /// Weights come from the needs registry so that food and shelter carry the
    /// same relative importance here as they do in D-018's ladder — happiness
    /// re-uses the ratified weighting instead of inventing a second opinion
    /// about how much a roof matters. Aggregation is D-035-B's CES.
    /// </summary>
    public static double Of(IReadOnlyWorldState world, SettlementId settlement, SimConfig cfg)
    {
        Span<double> factors = stackalloc double[FactorCount];
        Factors(world, settlement, cfg, factors);

        Span<double> weights = stackalloc double[FactorCount];
        weights[(int)Factor.Food] = WeightOf(cfg, SustenanceNeedId, 1.0);
        weights[(int)Factor.Housing] = WeightOf(cfg, ShelterNeedId, 0.9);

        AggregationTuning agg = cfg.Needs?.Aggregation
            ?? throw new ArgumentException(
                "SimConfig.Needs is not loaded — happiness reuses D-035-B's aggregation.",
                nameof(cfg));

        double aggregate = NeedsAggregation.Aggregate(
            factors, weights, agg.Sigma, agg.SatisfactionFloor);

        // NORMALIZE AGAINST TOTAL DEPRIVATION, and this is not cosmetic — it is
        // what makes the ruled revolt condition reachable at all.
        //
        // D-035-B's satisfactionFloor replaces a zero satisfaction with a small
        // positive number so that one absent need is "very bad, not infinitely
        // bad". A consequence nobody needed while this fed only grievance: the
        // aggregate of ALL-ZERO factors is not 0, it is the floor. Scaling that
        // raw aggregate by 100 would put a totally destitute settlement at 5.0
        // and leave `happiness == 0` UNREACHABLE — a dead predicate that reads
        // as implemented. Measured, not assumed: CES over identical inputs
        // returns that common value, so all-zero returns exactly the floor.
        //
        // Dropping the floor instead is worse. With rho < 0 the true CES limit
        // sends the aggregate to 0 the moment ANY single factor is 0, so a
        // fed-but-unhoused settlement would revolt — and colonists deliberately
        // "start homeless and build" (ColonizationSystem), which would revolt
        // every colony on the turn it is founded.
        //
        // So the floor stays, and the scale is anchored to it: the all-zero
        // aggregate maps to 0 and a fully-provided one to 100. Total deprivation
        // is the only way to reach zero, which is exactly the ruled condition.
        double floor = Math.Clamp(agg.SatisfactionFloor, 0.0, 1.0);
        double span = 1.0 - floor;
        double normalized = span > 0.0 ? (aggregate - floor) / span : aggregate;

        // M5: the tax burden scales the whole reading. See TaxSufficiency for why
        // it multiplies rather than joining the aggregate — an untaxed realm
        // multiplies by exactly 1.0, so nothing that predates M5 moves.
        double burdenScale = TaxSufficiency(world, settlement, cfg);

        return Math.Clamp(normalized * Max * burdenScale, 0.0, Max);
    }

    /// <summary>
    /// D-021's revolt condition (director ruling): happiness at zero. Published
    /// as a PREDICATE rather than executed here — this type reads the world and
    /// never writes it. Executing the revolt (losing control of the settlement)
    /// belongs to the system that owns the control relation, not to a reader.
    /// </summary>
    public static bool IsRevoltReady(
        IReadOnlyWorldState world, SettlementId settlement, SimConfig cfg)
        => Of(world, settlement, cfg) <= RevoltThreshold;

    // d018's frozen ladder ids. Named constants rather than literals so the
    // registry lookup below reads as what it is.
    private const int SustenanceNeedId = 1;
    private const int ShelterNeedId = 2;

    /// <summary>
    /// The registry weight for a need id, or <paramref name="fallback"/> when the
    /// registry does not carry it. The fallback is the shipped value and exists so
    /// a trimmed test registry cannot silently re-weight happiness to zero.
    /// </summary>
    private static double WeightOf(SimConfig cfg, int needId, double fallback)
    {
        NeedEntry[]? needs = cfg.Needs?.Needs;
        if (needs is null) return fallback;
        for (int i = 0; i < needs.Length; i++)
            if (needs[i].Id == needId) return needs[i].Weight;
        return fallback;
    }
}
