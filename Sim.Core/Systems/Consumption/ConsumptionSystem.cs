using Sim.Core.Kernel;
using Sim.Core.State;

namespace Sim.Core.Systems.Consumption;

/// <summary>
/// Writable handles to ConsumptionSystem's tables (built by SystemCatalog only).
/// GoodStocks is the SHARED conserved stock: Consumption debits it (Eaten) and
/// owns ConsumeRemainder; Production credits it — see SystemCatalog.
/// </summary>
public readonly record struct ConsumptionTables(
    Table<GoodStockRow> GoodStocks, Table<ConsumptionDeficitRow> Deficits);

/// <summary>
/// Consumption (T1.5; cohortized T2.1; T3.5 — D-035 class baskets).
///
/// M3/T3.5 REPLACES the single grain flow with a CLASS-WEIGHTED BASKET over
/// three needs: Sustenance (grain / livestock / fish), Shelter (timber / stone
/// as dwelling upkeep) and Comfort (pottery / cloth). Every line is data
/// (needs.json `baskets`, resolved through <see cref="BasketBook"/>) at a
/// per-person-per-sim-year RATE integrated with dtYears — no per-turn amounts
/// anywhere (law 3). Peasants and artisans differ because their basket lines
/// differ, not because a coefficient is applied to one of them.
///
/// TWO POPULATION MEASURES, deliberately. Sustenance demand is COHORT-WEIGHTED
/// (D-015: a child eats less than an adult). Shelter and Comfort demand is per
/// HEAD — a child needs a roof over him at full size, and the cohort weights
/// are a nutritional table, not a general-purpose person-size.
///
/// STAPLE SUBSTITUTION. Food is denominated in person-year-equivalents, so the
/// basket says WHAT is eaten and never how much nutrition a person needs.
/// Unmet non-staple food demand falls back on the staple (grain): a household
/// short of fish eats more bread rather than starving while grain sits in the
/// store. Monotony is then paid in the D-035-A variety term inside satisfaction
/// rather than in calories — which is the point of D-035-A, not a softening of
/// it. Substituted demand is REAL demand and is published as such, so the price
/// solver sees the pressure a failed fishery puts on the grain market.
///
/// RATIONING ACROSS CLASSES is proportional to demand: one flow per good, and
/// the settlement-wide fill ratio applies to every class alike. There is no
/// money at M3 (D-035-C path 2's purse is the M3.6/M3.7 market), so the
/// alternative would be an arbitrary priority order between classes — a
/// mechanism nobody chose, expressed as an iteration order. Proportional is the
/// honest statement of "no priced competition yet"; T3.6/T3.7 replace it.
///
/// Running AFTER Production is deliberate: the clamp applies to the post-harvest
/// store — this turn's harvest is eaten this turn. Reading its own shared Next
/// stock is lawful (owned-table access); everything else reads Prev.
///
/// REMAINDER SEMANTICS (documented): ConsumeRemainder carries only the sub-unit
/// fraction of demand. A clamp shortfall is NOT carried forward as remainder —
/// hunger is recorded in the deficit ratio, not banked as future double-eating.
///
/// DEFICIT RATIO (unchanged in meaning, T2.7/T2.13 consumers depend on it): the
/// unmet fraction of the settlement's NUTRITIONAL requirement — total food
/// person-year-equivalents obtained over total required, substitution included
/// once and only once. It is not affected by Shelter or Comfort: famine is a
/// food fact.
/// STATELESS: config is immutable tuning, not state.
/// </summary>
public sealed class ConsumptionSystem : ISimSystem<ConsumptionTables>
{
    public static readonly SystemId WellKnownId = new(6);
    public const string Name = "consumption";

    /// <summary>The Sustenance need id — see <see cref="BasketBook.SustenanceNeedId"/>.
    /// It lives on the shared book so that no system has to reference another
    /// system to learn it (law 6).</summary>
    private const int SustenanceNeedId = BasketBook.SustenanceNeedId;

    private readonly SimConfig _cfg;
    private readonly BasketBook _baskets;
    private readonly GoodId _grain;
    private readonly ClassId[] _classes;

    public ConsumptionSystem(SimConfig cfg)
    {
        _cfg = cfg;
        GoodsConfig goods = cfg.Goods
            ?? throw new ArgumentException("ConsumptionSystem requires SimConfig.Goods (goods.json) — consumption eats goods stocks at M3.");
        NeedsConfig needs = cfg.Needs
            ?? throw new ArgumentException("ConsumptionSystem requires SimConfig.Needs (needs.json) — the D-035 baskets are what it consumes at T3.5.");
        _baskets = new BasketBook(needs, goods);
        _grain = new GoodId(goods.GrainId);

        var classes = new List<ClassId>();
        foreach (BasketLine line in _baskets.Lines)
            if (!classes.Contains(line.Class)) classes.Add(line.Class);
        classes.Sort(static (a, b) => a.Value.CompareTo(b.Value));
        _classes = [.. classes];
    }

    public SystemId Id => WellKnownId;

    public void Step(SimContext<ConsumptionTables> ctx)
    {
        IReadOnlyWorldState prev = ctx.Prev;
        Table<GoodStockRow> stores = ctx.Owned.GoodStocks;
        Table<ConsumptionDeficitRow> deficits = ctx.Owned.Deficits;
        double dt = ctx.DtYears;

        ReadOnlySpan<GoodId> basketGoods = _baskets.Goods;
        Span<double> exactDemand = stackalloc double[basketGoods.Length];
        Span<long> demandedUnits = stackalloc long[basketGoods.Length];
        Span<long> eatenUnits = stackalloc long[basketGoods.Length];

        // Ascending settlement-row order — the fixed iteration order (law 5).
        for (int s = 0; s < prev.Settlements.Count; s++)
        {
            SettlementId settlement = prev.Settlements[s].Id;

            // Zero every good's observational signals before writing this
            // turn's (T3.3 staleness precedent: a stale observational field
            // reads as a live one). This covers goods OUTSIDE the baskets too —
            // their consumption demand this turn is genuinely zero.
            for (int i = 0; i < stores.Count; i++)
            {
                if (stores[i].Settlement != settlement) continue;
                stores.Ref(i).LastConsumptionDemandUnits = 0;
                stores.Ref(i).LastConsumptionEatenUnits = 0;
            }

            exactDemand.Clear();
            demandedUnits.Clear();
            eatenUnits.Clear();

            // --- basket demand, per (class, need, good) ----------------------
            double nutritionalRequirement = 0.0;   // person-year-equivalents this turn
            for (int c = 0; c < _classes.Length; c++)
            {
                ClassId cls = _classes[c];
                BasketDemand.Persons(prev, settlement, cls, _cfg.Consumption.CohortWeights,
                    out double nutritional, out double heads);
                if (nutritional <= 0.0 && heads <= 0.0) continue;

                foreach (BasketLine line in _baskets.Lines)
                {
                    if (line.Class != cls) continue;
                    double persons = line.Need == SustenanceNeedId ? nutritional : heads;
                    double units = persons * line.PerPersonYear * dt;
                    if (line.Need == SustenanceNeedId) nutritionalRequirement += units;
                    exactDemand[IndexOfGood(basketGoods, line.Good)] += units;
                }
            }

            // --- pass 1: every basket good EXCEPT the staple -----------------
            // The staple goes last because it absorbs the substituted demand of
            // whatever the other food goods could not supply.
            double foodObtained = 0.0, nonStapleShortfall = 0.0;
            for (int g = 0; g < basketGoods.Length; g++)
            {
                if (basketGoods[g] == _grain) continue;
                Consume(ctx, stores, settlement, basketGoods[g], exactDemand[g],
                    out demandedUnits[g], out eatenUnits[g]);
                if (IsFood(basketGoods[g]))
                {
                    foodObtained += eatenUnits[g];
                    // EXACT demand minus units obtained, not integer demand minus
                    // units obtained: a settlement with no livestock row at all
                    // demands livestock and receives none, and that whole
                    // shortfall must reach the staple. Differencing the rounded
                    // integers would drop it silently — the settlement would
                    // simply eat 10% less and be recorded as in deficit while
                    // grain sat in the store.
                    nonStapleShortfall += exactDemand[g] - eatenUnits[g];
                }
            }

            // --- pass 2: the staple, base demand + substitution ---------------
            for (int g = 0; g < basketGoods.Length; g++)
            {
                if (basketGoods[g] != _grain) continue;
                Consume(ctx, stores, settlement, _grain, exactDemand[g] + nonStapleShortfall,
                    out demandedUnits[g], out eatenUnits[g]);
                foodObtained += eatenUnits[g];
            }

            // --- deficit: the unmet fraction of the NUTRITIONAL requirement ---
            // Denominator is the base requirement, NOT the sum of per-good
            // demands: substituted demand is the same nutrition asked for a
            // second time in another good, and counting it twice would report a
            // famine in a settlement that ate its fill of bread.
            long requiredUnits = ConservedMath.WholeUnits(
                nutritionalRequirement, $"nutritional requirement (settlement {settlement.Value})");
            double ratio = requiredUnits > 0
                ? Math.Clamp((requiredUnits - foodObtained) / requiredUnits, 0.0, 1.0)
                : 0.0;
            var deficitRow = new ConsumptionDeficitRow(settlement, ratio, requiredUnits);
            if (s < deficits.Count) deficits[s] = deficitRow;
            else deficits.Add(deficitRow);

            // --- T4.2 (B-2): STORE BOUNDING, applied AFTER eating -------------
            // Order matters and is stated: people eat from the store BEFORE it
            // spoils and BEFORE it overflows. The reverse order would starve a
            // settlement whose granary was full at the moment of harvest, which
            // is an artefact of evaluation order rather than of scarcity.
            //
            // Grain only. B-2a's base layer is STORED GRAIN; every other good's
            // bounding is enrichment and out of this packet's fence.
            BoundStore(ctx, stores, settlement, _grain,
                annualGrainDemand: (exactDemand[IndexOfGood(basketGoods, _grain)]
                                    + nonStapleShortfall) / Math.Max(dt, double.Epsilon),
                dt: dt, cfg: _cfg.Consumption);
        }
    }

    /// <summary>One good's flow out of one settlement's store, with the
    /// sub-unit remainder banked and both observational signals published.
    /// PRE-CLAMP demand is the price signal (T3.4/D-033): what a starving
    /// settlement could not buy is exactly what should move the price.</summary>
    private static void Consume(
        SimContext<ConsumptionTables> ctx, Table<GoodStockRow> stores,
        SettlementId settlement, GoodId good, double exact,
        out long demanded, out long eaten)
    {
        demanded = 0; eaten = 0;
        int index = GoodStockIndex.IndexOf(stores, settlement, good);
        if (index < 0) return;   // no row for this good here — nothing to eat from

        ref GoodStockRow row = ref stores.Ref(index);
        double want = exact + row.ConsumeRemainder;
        demanded = ConservedMath.WholeUnits(
            want, $"consumption demand (settlement {settlement.Value}, good {good.Value})");
        eaten = ctx.Ledger.Flow(
            ref row.Amount, ConservedQuantityIds.OfGood(good), ReasonIds.Eaten,
            demanded, FlowDirection.Sink, OverdrawPolicy.ClampToAvailable);
        row.ConsumeRemainder = want - demanded;   // sub-unit fraction only (see header)
        row.LastConsumptionDemandUnits = demanded;
        row.LastConsumptionEatenUnits = eaten;
    }

    /// <summary>
    /// T4.2 — B-2 store bounding, base layer: SPOILAGE then GRANARY CAPACITY.
    ///
    /// BOTH ARE LOSSES THROUGH THE LEDGER with their own reasons, never silent
    /// clamps: law 1 holds exactly, the stock stays `long`, and the audit trail
    /// answers "where did the grain go?" with two distinct answers.
    ///
    /// SPOILAGE is integrated as a SURVIVAL EXPONENTIAL, `1 - exp(-rate*dt)`,
    /// not as `rate*dt`. At the canonical dt of 10 sim-years a linear reading of
    /// 0.08/yr would destroy 80% of the store in one turn and 100% at dt = 12.5
    /// — the CR-001 dt-fragility exactly. The exponential form is dt-invariant:
    /// two 5-year steps and one 10-year step remove the same fraction.
    ///
    /// CAPACITY is denominated in YEARS OF THE SETTLEMENT'S OWN ANNUAL GRAIN
    /// DEMAND, so it scales with population and there is no per-world constant
    /// to tune. A settlement that grows builds granaries; one that empties has
    /// less to hold. The overflow is a LOSS, not a refusal to harvest: the
    /// harvest already happened, and grain that will not fit is grain that rots
    /// in the open.
    ///
    /// SUB-UNIT PRECISION, stated rather than hidden: neither loss banks a
    /// remainder. `WholeUnits` rounds, so a store small enough that its annual
    /// spoilage is under half a unit does not spoil at all. That is a deliberate
    /// simplification of the base layer — a remainder field would change the
    /// serialized row shape, which is out of this packet's fence — and its only
    /// effect is at stores of order 1/(2*rate*dt) units, i.e. single digits.
    /// </summary>
    private static void BoundStore(
        SimContext<ConsumptionTables> ctx, Table<GoodStockRow> stores,
        SettlementId settlement, GoodId good, double annualGrainDemand,
        double dt, ConsumptionConfig cfg)
    {
        int index = GoodStockIndex.IndexOf(stores, settlement, good);
        if (index < 0) return;
        ref GoodStockRow row = ref stores.Ref(index);

        // 1. SPOILAGE — a fraction of what is actually held.
        long held = row.Amount.Value;
        if (held > 0 && cfg.GrainSpoilagePerYear > 0.0 && dt > 0.0)
        {
            double lostFraction = 1.0 - Math.Exp(-cfg.GrainSpoilagePerYear * dt);
            long spoiled = ConservedMath.WholeUnits(
                held * lostFraction, $"grain spoilage (settlement {settlement.Value})");
            if (spoiled > 0)
            {
                ctx.Ledger.Flow(
                    ref row.Amount, ConservedQuantityIds.OfGood(good), ReasonIds.Spoilage,
                    spoiled, FlowDirection.Sink, OverdrawPolicy.ClampToAvailable);
            }
        }

        // 2. GRANARY CAPACITY — what is left must fit.
        if (cfg.GranaryYearsOfDemand > 0.0 && annualGrainDemand > 0.0)
        {
            long capacity = ConservedMath.WholeUnits(
                cfg.GranaryYearsOfDemand * annualGrainDemand,
                $"granary capacity (settlement {settlement.Value})");
            long over = row.Amount.Value - capacity;
            if (over > 0)
            {
                ctx.Ledger.Flow(
                    ref row.Amount, ConservedQuantityIds.OfGood(good), ReasonIds.GranaryOverflow,
                    over, FlowDirection.Sink, OverdrawPolicy.ClampToAvailable);
            }
        }
    }

    private bool IsFood(GoodId good)
    {
        foreach (BasketLine line in _baskets.Lines)
            if (line.Good == good && line.Need == SustenanceNeedId) return true;
        return false;
    }

    private static int IndexOfGood(ReadOnlySpan<GoodId> goods, GoodId good)
    {
        for (int i = 0; i < goods.Length; i++) if (goods[i] == good) return i;
        return -1;
    }
}
