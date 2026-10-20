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
/// R4 (ledger-crash fix): the STAPLE's remainder may also carry a SUBSTITUTION
/// CREDIT (a negative value). Pass 1 hands the staple each non-staple food's
/// EXACT shortfall, a signed quantity: a sub-unit fraction lent to the staple
/// while it is banked in the non-staple's own remainder, repaid (negative) on
/// the turn that remainder pays out as a whole unit. When the repayment exceeds
/// the staple's whole request (a settlement whose demand has collapsed), the
/// staple asks for nothing and the unabsorbed credit is CARRIED in its
/// remainder, to be settled against its next request — never converted into a
/// negative ledger amount (which Ledger.Flow rightly refuses) and never dropped.
///
/// DEFICIT RATIO (unchanged in meaning, T2.7/T2.13 consumers depend on it): the
/// unmet fraction of the settlement's NUTRITIONAL requirement — total food
/// person-year-equivalents obtained over total required, substitution included
/// once and only once. It is not affected by Shelter or Comfort: famine is a
/// food fact.
///
/// INTRA-POLITY GRAIN RELIEF IN A SHORTAGE (sim.json
/// consumption.empireFoodAllocation, ON in shipped content). AUTHORITY: D-053 §2
/// (docs/d053-batch2-playtest-rulings.md — the Director's 2026-10-13 instruction,
/// item 2: food allocation in a shortage), which D-053 §5 records as superseding
/// D-052 §11's "no partial pooling / no relief-pooling substitute" for this item.
/// It is a PARTIAL step toward D-051 §6's ratified direction ("one central
/// stockpile per civilization … including food") and NOT that stockpile: every
/// store, its spoilage and its granary bound stay per settlement, and grain moves
/// only in a shortage, only to a hungry settlement, only inside one polity.
/// After every settlement has eaten from its own store, a settlement a polity
/// controls whose GRAIN ROW IS EMPTY and whose nutritional requirement is still
/// unmet is a CLAIMANT; every settlement of that polity whose grain row still
/// holds grain is a DONOR, and all of that remaining grain is the polity's supply.
/// The EMPTY-GRAIN-ROW rule is part of the claimant test, stated here because it
/// is not implied by "short of food": a settlement can be one unit short BESIDE
/// a stocked granary (the staple's floating-point request lands a hair below a
/// whole unit and the floor banks that unit in ConsumeRemainder, or a negative
/// substitution credit lowers the request) — such a settlement is a DONOR, never
/// a claimant. The supply is divided among the claimants by MAX-MIN WATER-FILLING
/// ON THE NOMINAL DEFICIT d = u/R (FoodRelief), drawn from the donors
/// nearest-first (ADR-032's order) by Ledger.Transfer and eaten at once by
/// Ledger.Flow (Eaten). THE METRIC IS THE NOMINAL DEFICIT — the unmet fraction of
/// the nutritional requirement, the ConsumptionDeficitRow ratio, and the INPUT to
/// FoodState's effective deficit — not the effective deficit. Within one food
/// state it ranks exactly by starvation risk; ACROSS food states it does not (a
/// FAMINE-class settlement at d = 0.25 has effective deficit 0.25, a NORMAL or
/// SEVERE one at d = 0.30 has 0.125 at the shipped absorbable shortfall 0.20), and
/// the PREV food state is only a one-turn-lagged tie-break. The choice is stated:
/// the effective deficit depends on the food state the demographic kernel
/// classifies from tables other systems write in THIS step (the disaster applied
/// in this step's production, the sector row in force), which Consumption cannot
/// know without a cross-system read or a one-turn-lagged guess. Relief happens
/// BEFORE spoilage and the granary bound, which stay physical and per settlement
/// exactly as before. A settlement no polity controls is its own store. The
/// deficit ratio keeps its meaning and now counts the food the settlement received.
/// SWITCH OFF (or absent): byte for byte the per-settlement store. SWITCH ON with
/// no claimant moves no grain, but it is NOT byte-identical to OFF BY
/// CONSTRUCTION: ON eats every settlement before it bounds any, so in a hand-built
/// world a good's FIRST-USE LedgerFlow row can be appended in a different order
/// (a later settlement's first fish Eaten row before an earlier one's Spoilage
/// row) — no grain moves, the world hash differs. The shipped founded and AI
/// goldens are identical ON and OFF (MEASURED; EmpireFoodAllocationTests). See
/// RelieveShortages.
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
        int count = prev.Settlements.Count;

        ReadOnlySpan<GoodId> basketGoods = _baskets.Goods;
        Span<double> exactDemand = stackalloc double[basketGoods.Length];
        Span<long> demandedUnits = stackalloc long[basketGoods.Length];
        Span<long> eatenUnits = stackalloc long[basketGoods.Length];

        // D-053 §2 — INTRA-POLITY GRAIN RELIEF IN A SHORTAGE (sim.json consumption.empireFoodAllocation; a
        // partial step toward D-051 §6, not the central stockpile). OFF (or absent): the per-settlement loop
        // below runs exactly as it always has, own store then bound, settlement by settlement — byte for byte
        // the pre-relief step. ON: three phases, each in the same fixed row order:
        //   P1  every settlement eats from its OWN store (unchanged arithmetic) and its requirement R and
        //       food obtained F are kept; nothing is bounded yet;
        //   P2  within each polity, the grain still held by its settlements goes to those with an EMPTY grain
        //       row and an unmet requirement (FoodRelief: max-min water-filling on the nominal deficit u/R),
        //       drawn nearest-first by Ledger.Transfer and eaten by Ledger.Flow (Eaten) at the recipient —
        //       see RelieveShortages;
        //   P3  spoilage and the granary bound, exactly as before, on the post-relief stores.
        // Relief comes BEFORE bounding for the reason the header gives for eating before bounding: grain a
        // starving sibling could have eaten must not first be destroyed as one settlement's overflow.
        bool pooled = _cfg.Consumption.EmpireFoodAllocation;
        long[]? required = pooled ? new long[count] : null;
        long[]? obtained = pooled ? new long[count] : null;
        double[]? annualGrain = pooled ? new double[count] : null;

        // Ascending settlement-row order — the fixed iteration order (law 5).
        for (int s = 0; s < count; s++)
        {
            SettlementId settlement = prev.Settlements[s].Id;
            EatOwnStore(ctx, stores, deficits, s, settlement, dt, basketGoods, exactDemand, demandedUnits, eatenUnits,
                out long requiredUnits, out long foodObtainedUnits, out double annualGrainDemand);

            if (!pooled)
            {
                // --- T4.2 (B-2): STORE BOUNDING, applied AFTER eating -------------
                // Order matters and is stated: people eat from the store BEFORE it
                // spoils and BEFORE it overflows. The reverse order would starve a
                // settlement whose granary was full at the moment of harvest, which
                // is an artefact of evaluation order rather than of scarcity.
                //
                // Grain only. B-2a's base layer is STORED GRAIN; every other good's
                // bounding is enrichment and out of this packet's fence.
                BoundStore(ctx, stores, settlement, _grain, annualGrainDemand, dt, _cfg.Consumption);
                continue;
            }
            required![s] = requiredUnits;
            obtained![s] = foodObtainedUnits;
            annualGrain![s] = annualGrainDemand;
        }
        if (!pooled) return;

        RelieveShortages(ctx, stores, deficits, required!, obtained!);

        for (int s = 0; s < count; s++)
            BoundStore(ctx, stores, prev.Settlements[s].Id, _grain, annualGrain![s], dt, _cfg.Consumption);
    }

    /// <summary>
    /// P1 — one settlement eats its basket from its OWN store: the observational signals zeroed, the
    /// basket demand formed, pass 1 (every good but the staple) and pass 2 (the staple plus substitution),
    /// and its ConsumptionDeficitRow written. This is the pre-§6 per-settlement body, moved verbatim; it
    /// returns the requirement R (whole units), the food obtained F (whole units, Σ eaten over the food
    /// goods), and the annual grain demand the store bound is sized from.
    /// </summary>
    private void EatOwnStore(
        SimContext<ConsumptionTables> ctx, Table<GoodStockRow> stores, Table<ConsumptionDeficitRow> deficits,
        int s, SettlementId settlement, double dt, ReadOnlySpan<GoodId> basketGoods,
        Span<double> exactDemand, Span<long> demandedUnits, Span<long> eatenUnits,
        out long requiredUnits, out long foodObtainedUnits, out double annualGrainDemand)
    {
        IReadOnlyWorldState prev = ctx.Prev;

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
        long obtainedUnits = 0;
        for (int g = 0; g < basketGoods.Length; g++)
        {
            if (basketGoods[g] == _grain) continue;
            Consume(ctx, stores, settlement, basketGoods[g], exactDemand[g],
                out demandedUnits[g], out eatenUnits[g]);
            if (IsFood(basketGoods[g]))
            {
                foodObtained += eatenUnits[g];
                obtainedUnits = checked(obtainedUnits + eatenUnits[g]);
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
            obtainedUnits = checked(obtainedUnits + eatenUnits[g]);
        }

        // --- deficit: the unmet fraction of the NUTRITIONAL requirement ---
        // Denominator is the base requirement, NOT the sum of per-good
        // demands: substituted demand is the same nutrition asked for a
        // second time in another good, and counting it twice would report a
        // famine in a settlement that ate its fill of bread.
        requiredUnits = ConservedMath.WholeUnits(
            nutritionalRequirement, $"nutritional requirement (settlement {settlement.Value})");
        double ratio = requiredUnits > 0
            ? Math.Clamp((requiredUnits - foodObtained) / requiredUnits, 0.0, 1.0)
            : 0.0;
        var deficitRow = new ConsumptionDeficitRow(settlement, ratio, requiredUnits);
        if (s < deficits.Count) deficits[s] = deficitRow;
        else deficits.Add(deficitRow);

        foodObtainedUnits = obtainedUnits;
        annualGrainDemand = (exactDemand[IndexOfGood(basketGoods, _grain)] + nonStapleShortfall)
                            / Math.Max(dt, double.Epsilon);
    }

    /// <summary>
    /// P2 — INTRA-POLITY GRAIN RELIEF IN A SHORTAGE. AUTHORITY: D-053 §2 (the Director's 2026-10-13
    /// instruction, item 2; D-053 §5 records that it supersedes D-052 §11's "no partial pooling / no
    /// relief-pooling substitute" for this item), a partial step toward D-051 §6's ratified central stockpile —
    /// not that stockpile: stores stay per settlement and only a shortage moves grain. Per polity (ascending id;
    /// the controller is <see cref="EmpireQuery.TryGetController"/> on PREV, the LOWEST polity holding a row for
    /// the place, a function of the row SET — so a place carrying two Controls rows belongs to exactly one polity
    /// here, and the draw re-checks it):
    /// <list type="bullet">
    /// <item><b>Who gives.</b> Every settlement it controls whose grain row still holds grain after its own
    ///   eating; all of that grain is the polity's supply. Nothing is lost in the move.</item>
    /// <item><b>Who receives — THE EMPTY-GRAIN-ROW RULE.</b> Every settlement it controls with an unmet
    ///   requirement u = R − F &gt; 0 AND an EMPTY grain row. Substitution already routed every food shortfall
    ///   onto grain, so a settlement short of food has normally eaten its grain to zero. The empty-row condition
    ///   is STATED, not assumed, because it is not always so: when the staple's floating-point request lands a
    ///   hair below a whole number (k − 4.5e−13), the floor defers one unit to the banked remainder
    ///   (≈ 0.99999999999954, paid out next turn) while R floors the exact requirement, so F = R − 1 beside a
    ///   stocked granary — MEASURED on 6 of the founded seed-42 golden's 3,600 settlement-turns; a negative
    ///   substitution credit in the staple's ConsumeRemainder lowers the request the same way. Such a settlement
    ///   is a DONOR, not a claimant (its own grain is there; the shortfall is bookkeeping) — pinned by
    ///   EmpireFoodAllocationTests.System_OneUnitShortBesideAStockedGranary_IsADonor_NotAClaimant. A settlement
    ///   with no grain row neither gives nor receives (Consumption never appends rows).</item>
    /// <item><b>How much.</b> <see cref="FoodRelief.Allocate"/>: max-min water-filling on the NOMINAL deficit
    ///   u/R (not the effective deficit — see the type header for why, and what that ranks correctly and what it
    ///   does not), whole units, Σ ≤ min(supply, Σ u), each ≤ its own u.</item>
    /// <item><b>From where.</b> Recipients in ascending settlement id (a fixed order, independent of row order);
    ///   each draws its grant from the donors NEAREST-FIRST to itself, ties by id — ADR-032's documented draw
    ///   order (<see cref="RoadDevelopmentQuery.PayingSettlements(IReadOnlyWorldState, PolityId, SettlementId, SettlementId)"/>
    ///   with both endpoints at the recipient), keeping only places this polity CONTROLS by the rule above — by
    ///   Ledger.Transfer, then EATS it by Ledger.Flow (Eaten), so its grain row's LastConsumptionEatenUnits and
    ///   its deficit ratio, (R − (F + a)) / R, say what it actually ate.</item>
    /// </list>
    /// THE ±1-UNIT INTERPLAY WITH ConsumeRemainder (derived from the arithmetic, stated rather than hidden).
    /// Relief tops a recipient up toward R = floor(exact nutritional requirement) — the deficit ratio's own
    /// denominator — and never touches the staple's ConsumeRemainder (the banked sub-unit fraction, or a negative
    /// substitution credit). Against the remainder-carrying demand series the recipient can therefore end at most
    /// one unit short of what its remainder-adjusted request asked this turn (recorded deficit 0, because the ratio
    /// is measured against R), or eat at most one unit more than that series over this turn and the next when the
    /// banked fraction or credit settles. At most ±1 unit per relief event; never a conservation error, since
    /// every unit moves by the Ledger.
    /// A settlement no polity controls is its own store and is never touched. Nothing here is a new demographic
    /// rule: starvation, fertility suppression, flight and FoodState read the deficit ratio exactly as before; the
    /// ratio now reflects the food the settlement received.
    /// </summary>
    private void RelieveShortages(
        SimContext<ConsumptionTables> ctx, Table<GoodStockRow> stores, Table<ConsumptionDeficitRow> deficits,
        long[] required, long[] obtained)
    {
        IReadOnlyWorldState prev = ctx.Prev;
        int count = prev.Settlements.Count;
        if (count < 2 || prev.Controls.Count == 0) return;

        var controller = new int[count];
        var polities = new List<int>();
        for (int s = 0; s < count; s++)
        {
            controller[s] = EmpireQuery.TryGetController(prev, prev.Settlements[s].Id, out PolityId p) ? p.Value : -1;
            if (controller[s] >= 0 && !polities.Contains(controller[s])) polities.Add(controller[s]);
        }
        polities.Sort();

        var claims = new List<FoodClaim>();
        var claimRows = new List<int>();
        for (int pi = 0; pi < polities.Count; pi++)
        {
            int polity = polities[pi];
            claims.Clear();
            claimRows.Clear();
            long supply = 0;
            for (int s = 0; s < count; s++)
            {
                if (controller[s] != polity) continue;
                SettlementId id = prev.Settlements[s].Id;
                int g = GoodStockIndex.IndexOf(stores, id, _grain);
                if (g < 0) continue;
                long held = stores[g].Amount.Value;
                if (held > 0)
                {
                    supply = checked(supply + held);
                    continue;
                }
                long unmet = required[s] - obtained[s];
                if (required[s] <= 0 || unmet <= 0) continue;
                int severity = (int)FoodState.Of(prev, id, _cfg, out _);
                claims.Add(new FoodClaim(id.Value, required[s], unmet, severity));
                claimRows.Add(s);
            }
            if (claims.Count == 0 || supply == 0) continue;

            long[] grants = FoodRelief.Allocate(claims.ToArray(), supply);

            // Execute in ascending settlement id (a fixed order independent of row order).
            var byId = new int[claims.Count];
            for (int i = 0; i < byId.Length; i++) byId[i] = i;
            Array.Sort(byId, (x, y) => claims[x].Settlement.CompareTo(claims[y].Settlement));
            var polityId = new PolityId(polity);
            for (int k = 0; k < byId.Length; k++)
            {
                int c = byId[k];
                long grant = grants[c];
                if (grant == 0) continue;
                int s = claimRows[c];
                SettlementId recipient = prev.Settlements[s].Id;
                int to = GoodStockIndex.IndexOf(stores, recipient, _grain);

                SettlementId[] draw = RoadDevelopmentQuery.PayingSettlements(prev, polityId, recipient, recipient);
                long left = grant;
                for (int d = 0; d < draw.Length && left > 0; d++)
                {
                    if (draw[d] == recipient) continue;
                    if (!EmpireQuery.TryGetController(prev, draw[d], out PolityId owner) || owner.Value != polity) continue;
                    int from = GoodStockIndex.IndexOf(stores, draw[d], _grain);
                    if (from < 0) continue;
                    long take = Math.Min(left, stores[from].Amount.Value);
                    if (take <= 0) continue;
                    ctx.Ledger.Transfer(ref stores.Ref(from).Amount, ref stores.Ref(to).Amount, take, OverdrawPolicy.Throw);
                    left -= take;
                }
                if (left != 0)
                    throw new InvalidOperationException(
                        $"food relief: polity {polity}'s spare grain was counted but did not cover settlement {recipient.Value}'s grant.");

                ctx.Ledger.Flow(ref stores.Ref(to).Amount, ConservedQuantityIds.OfGood(_grain), ReasonIds.Eaten,
                    grant, FlowDirection.Sink, OverdrawPolicy.Throw);
                ref GoodStockRow row = ref stores.Ref(to);
                row.LastConsumptionEatenUnits = checked(row.LastConsumptionEatenUnits + grant);

                long fed = checked(obtained[s] + grant);
                double ratio = Math.Clamp((required[s] - (double)fed) / required[s], 0.0, 1.0);
                deficits[s] = deficits[s] with { DeficitRatio = ratio };
            }
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
        if (want < 0.0)
        {
            // R4 — the violated invariant was "a consumption request is never negative". A net
            // substitution CREDIT (see the header's remainder semantics) larger than this good's
            // whole request is not demand: nothing is asked of the store, nothing flows, and the
            // credit is carried exactly so the demand ledger stays whole. Unreachable for any
            // non-staple good (its exact demand and remainder are both non-negative).
            row.ConsumeRemainder = want;
            row.LastConsumptionDemandUnits = 0;
            row.LastConsumptionEatenUnits = 0;
            return;
        }
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
    /// remainder. `WholeUnits` FLOORS (`ConservedMath.cs` — "by floor, the D-004
    /// remainder convention"), so a store small enough that its annual spoilage
    /// is under ONE WHOLE UNIT does not spoil at all. That is a deliberate
    /// simplification of the base layer — a remainder field would change the
    /// serialized row shape, which is out of this packet's fence — and its only
    /// effect is at stores of order 1/(rate*dt) units, i.e. single digits.
    ///
    /// (This paragraph previously said `WholeUnits` "rounds" and put the
    /// threshold at half a unit. It floors. The correction is stated rather than
    /// silently applied because the flooring direction is load-bearing below.)
    ///
    /// WHY THE CAPACITY CEILING IS ENFORCED ONLY WHEN IT IS REPRESENTABLE.
    /// `WholeUnits` converts a FLOW, under a remainder convention that banks the
    /// fraction elsewhere. A capacity is not a flow — it is a THRESHOLD, and it
    /// has no remainder bank, so flooring biases it downward with nothing to
    /// carry the loss. At a demand small enough that
    /// `GranaryYearsOfDemand × annualGrainDemand < 1`, the floor collapses a
    /// genuinely POSITIVE capacity to zero, `over` becomes the whole store, and
    /// the settlement's entire grain holding is destroyed as overflow in one
    /// turn — while a settlement at literally zero demand is skipped by the
    /// guard and keeps its grain indefinitely. A settlement down to its last
    /// person was thus treated more harshly than a dead one, which is not a
    /// physical rule anyone chose.
    ///
    /// The ceiling is therefore enforced only when it is representable as a
    /// positive whole number of units. Below that the ceiling is not enforced —
    /// exactly the stance this code already takes when there is no demand at all
    /// to size a granary from. No constant, parameter or rule is introduced: the
    /// sub-unit case simply joins the no-basis case that was already shipped.
    /// Wherever capacity ≥ 1 — every settlement in every canonical world — this
    /// changes nothing, which is why no golden moves.
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
            // `capacity > 0`: a ceiling that floors to zero is not representable
            // and is not enforced (see the header). Without this, `over` is the
            // ENTIRE store and the whole holding is destroyed.
            if (capacity > 0 && over > 0)
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
