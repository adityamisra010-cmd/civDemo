using Sim.Core.State;
using Sim.Core.Systems;
using Sim.Core.Systems.Consumption;
using Sim.Core.Systems.Demographics;
using Sim.Core.Systems.Migration;

namespace Sim.Core.Observability;

/// <summary>One observed step: the world record and every settlement record.</summary>
public sealed record TurnObservation(TurnRecord Turn, SettlementRecord[] Settlements);

/// <summary>
/// T4.19 — THE OBSERVER: <c>(prev, next, cfg, orders) → TurnObservation</c>, a
/// pure function of two worlds it does not retain (§7: records are never a
/// source of truth; they can always be rebuilt by replaying the order log
/// through this same function, which is what <c>sim inspect --telemetry</c> does).
///
/// IT READS THROUGH <see cref="IReadOnlyWorldState"/> ON BOTH SIDES, so a write
/// does not compile — the same guarantee the kernel gives systems about Prev
/// (kernel contract §3.1, proven by check-readonly-proof.sh). It is consulted by
/// NO system: nothing in Sim.Core/Systems references this namespace, no
/// pipeline object holds a log, and the step call is byte-identical with and
/// without observation (asserted in Sim.Tests/Observability, not assumed).
///
/// NO SECOND IMPLEMENTATION OF ANY FORMULA (§0, ReplayReport precedent). Every
/// number is READ from a row, SUMMED over conserved longs, DIFFERENCED from the
/// cumulative ledger, a RESIDUAL of a stated identity, or RECOMPUTED through a
/// public static the simulation itself calls (<see cref="SettlementHappiness"/>,
/// <see cref="Sectors.Share"/>, <see cref="BandViews"/>). Where the simulation
/// records nothing the record carries a "GAP:" string, never a number.
///
/// TABLES ARE WALKED IN INDEX ORDER, never through a Dictionary; the only sort
/// is over integer reason ids. The output is therefore a deterministic function
/// of its inputs and the JSONL written from it is byte-identical across runs
/// (asserted).
/// </summary>
public static class Observer
{
    public const string ColonistsDepartedIdentity =
        "Opening + Births - Deaths + Inflow - Outflow - Closing; absorbs ONLY colonization "
        + "(the party leaves by Ledger.Transfer and no row records it); 0 on every turn no "
        + "settlement was founded; negative on a founding record (the party ARRIVED)";

    public const string StoreLossesIdentity =
        "GrainOpening + Harvest - Eaten - GrainClosing; absorbs spoilage + granary overflow "
        + "(unsplit per settlement) AND any appropriation transfer AND colony provisions; "
        + "sum over all records == ledger spoilage + overflow on every turn; negative means "
        + "grain ARRIVED by an unrecorded transfer (provisions on a founding record, a raid otherwise)";

    private const string BuiltDecayedGap =
        "GAP: HousingSystem records only the dwelling stock and LastMaintenanceFraction; "
        + "the per-settlement built/decayed split is a world ledger total (see TurnRecord.Dwellings)";

    private const string PairwiseGap =
        "GAP: the From->To flow matrix, damping, viability products and gap scale are "
        + "computed inside MigrationSystem and discarded; only per-settlement totals are recorded";

    /// <summary>T4.21-5: the plan carries the destination's desired inflow in
    /// adult-equivalents as ONE number over BOTH channels (MigrationPlan
    /// DesiredInflowAe) — it is what the vacancy bound compares against, so the
    /// planner never forms the split. Stating it is the honest answer; forming
    /// it here would be an observer-side re-derivation of a quantity the
    /// simulation does not compute.</summary>
    internal const string InflowAeByChannelGap =
        "GAP: MigrationPlan.DesiredInflowAe is the adult-equivalent inflow the vacancy bound tests, summed "
        + "over the gap AND flight channels together (MigrationSystem.Plan, the vacancy-bound loop); the "
        + "planner forms no per-channel adult-equivalent split, so none is recorded. The per-channel HEAD "
        + "counts are recorded (gapIn, flightIn).";

    public static TurnObservation Observe(
        IReadOnlyWorldState prev, IReadOnlyWorldState next, SimConfig cfg, OrderApplied[] orders)
    {
        ArgumentNullException.ThrowIfNull(cfg.Goods);
        ArgumentNullException.ThrowIfNull(cfg.Needs);
        ArgumentNullException.ThrowIfNull(orders);

        // THE SIMULATION'S OWN FOOD SET (§0 RECOMPUTED). BasketBook is the
        // sanctioned shared pure reader of needs.json + goods.json - neither a
        // system nor a channel between systems, and already read from this
        // namespace by CausalChain (Explain/CausalChain.cs:593-597). Its
        // FoodGoods span is EXACTLY the set ConsumptionSystem substitutes into
        // the staple (ConsumptionSystem.cs:328-333) and ClassMobilitySystem
        // sums for its surplus numerator (ClassMobilitySystem.cs:131-135): the
        // goods carrying a Sustenance basket line. Selecting by goods.json's
        // "category":"food" string instead would be a SECOND rule that happens
        // to agree on shipped data and would diverge the moment a Sustenance
        // line was tuned away - and a FoodBalance that subtracted a good no
        // longer denominated in person-year-equivalents would not be
        // dimensionally sound. Built ONCE per observed step, not per settlement.
        var baskets = new BasketBook(cfg.Needs!, cfg.Goods!);

        // T4.21-5 (spec §3.11): the migration planner, called ONCE per observed
        // step through the PUBLIC static MigrationSystem.Step itself consumes,
        // on the SAME prev at the SAME dt. next.Clock.DtYears IS the dt the step
        // integrated (TurnExecutor.Step: the clock is stamped with the dtDays
        // the pipeline ran on), so this is not a second choice of dt either.
        // The observer indexes the plan; it recomputes nothing from it.
        MigrationPlan plan = MigrationSystem.Plan(prev, cfg, next.Clock.DtYears, baskets);
        int[] planRow = PlanRowBySettlementId(prev);

        // Settlement records first: the turn record's appropriation detector
        // reads their store-loss residuals.
        int prevCount = prev.Settlements.Count;
        int founded = 0;
        for (int s = 0; s < next.Settlements.Count; s++)
            if (!HasSettlement(prev, next.Settlements[s].Id)) founded++;
        var settlements = new SettlementRecord[prevCount + founded];
        for (int s = 0; s < prevCount; s++)
            settlements[s] = Settlement(prev, next, cfg, baskets, plan, planRow, orders, prev.Settlements[s].Id, founded: false);
        int f = prevCount;
        for (int s = 0; s < next.Settlements.Count; s++)
        {
            SettlementId id = next.Settlements[s].Id;
            if (HasSettlement(prev, id)) continue;
            settlements[f++] = Settlement(prev, next, cfg, baskets, plan, planRow, orders, id, founded: true);
        }

        bool unattributed = false;
        for (int i = 0; i < settlements.Length; i++)
            if (!settlements[i].Founded && settlements[i].Food.StoreLosses < 0) unattributed = true;

        return new TurnObservation(Turn(prev, next, cfg, orders, unattributed), settlements);
    }

    // ------------------------------------------------------------------ §2 --

    private static TurnRecord Turn(
        IReadOnlyWorldState prev, IReadOnlyWorldState next, SimConfig cfg,
        OrderApplied[] orders, bool unattributedGrainTransfer)
    {
        GoodsConfig goods = cfg.Goods!;
        int grain = goods.GrainId;

        // Every quantity that can carry stock, ascending id: the four fixed ids
        // then the goods (registry ids are strictly ascending, GoodsConfig doc).
        var stocks = new List<StockRecord>(4 + goods.Goods.Length);
        AddIfPresent(stocks, prev, next, ConservedQuantityIds.Biomass, "biomass");
        AddIfPresent(stocks, prev, next, ConservedQuantityIds.ToyGood, "toyGood");
        AddIfPresent(stocks, prev, next, ConservedQuantityIds.Population, "population");
        AddIfPresent(stocks, prev, next, ConservedQuantityIds.Dwellings, "dwellings");
        for (int g = 0; g < goods.Goods.Length; g++)
        {
            GoodEntry good = goods.Goods[g];
            AddIfPresent(stocks, prev, next, ConservedQuantityIds.OfGood(new GoodId(good.Id)), good.Name);
        }

        StockRecord pop = Find(stocks, ConservedQuantityIds.Population);
        StockRecord grainStock = Find(stocks, ConservedQuantityIds.OfGood(new GoodId(grain)));
        StockRecord dwell = Find(stocks, ConservedQuantityIds.Dwellings);

        long births = Leg(pop.Sources, ReasonIds.Births);
        long deaths = Leg(pop.Sinks, ReasonIds.Deaths);
        long starvation = Leg(pop.Sinks, ReasonIds.Starvation);
        var population = Account(pop.Opening, pop.Closing, births - deaths - starvation,
            (o, c, r, d) => new PopulationAccount(o, births, deaths, starvation, c, r, d));

        long endowment = Leg(grainStock.Sources, ReasonIds.InitialEndowment);
        long harvest = Leg(grainStock.Sources, ReasonIds.Harvest);
        long eaten = Leg(grainStock.Sinks, ReasonIds.Eaten);
        long spoilage = Leg(grainStock.Sinks, ReasonIds.Spoilage);
        long overflow = Leg(grainStock.Sinks, ReasonIds.GranaryOverflow);
        var grainAccount = Account(grainStock.Opening, grainStock.Closing,
            endowment + harvest - eaten - spoilage - overflow,
            (o, c, r, d) => new GrainAccount(o, endowment, harvest, eaten, spoilage, overflow, c, r, d));

        long built = Leg(dwell.Sources, ReasonIds.HousingBuilt);
        long decayed = Leg(dwell.Sinks, ReasonIds.HousingDecayed);
        var dwellings = Account(dwell.Opening, dwell.Closing, built - decayed,
            (o, c, r, d) => new DwellingsAccount(o, built, decayed, c, r, d));

        var goodAccounts = new List<GoodAccount>(goods.Goods.Length);
        for (int g = 0; g < goods.Goods.Length; g++)
        {
            GoodEntry good = goods.Goods[g];
            if (good.Id == grain) continue;
            StockRecord st = Find(stocks, ConservedQuantityIds.OfGood(new GoodId(good.Id)));
            long produced = Leg(st.Sources, ReasonIds.Produced);
            long inputs = Leg(st.Sinks, ReasonIds.InputsConsumed);
            long wear = Leg(st.Sinks, ReasonIds.ToolWear);
            long ate = Leg(st.Sinks, ReasonIds.Eaten);
            long housingMat = Leg(st.Sinks, ReasonIds.HousingMaterials);
            long constructionMat = Leg(st.Sinks, ReasonIds.ConstructionMaterials);
            goodAccounts.Add(Account(st.Opening, st.Closing,
                produced - inputs - wear - ate - housingMat - constructionMat,
                (o, c, r, d) => new GoodAccount(good.Id, good.Name, o, produced, inputs, wear, ate,
                    housingMat, constructionMat, c, r, d)));
        }

        long migrants = 0;
        for (int i = 0; i < next.MigrationFlows.Count; i++) migrants += next.MigrationFlows[i].Inflow;
        long tradeUnits = 0;
        for (int i = 0; i < next.TradeFlows.Count; i++) tradeUnits += next.TradeFlows[i].Quantity;
        int controlLost = 0;
        for (int i = 0; i < prev.Controls.Count; i++)
        {
            ControlRow row = prev.Controls[i];
            bool still = false;
            for (int j = 0; j < next.Controls.Count; j++)
                if (next.Controls[j].Polity == row.Polity && next.Controls[j].Place == row.Place) { still = true; break; }
            if (!still) controlLost++;
        }
        var flows = new FlowsSummary(
            migrants, next.Settlements.Count - prev.Settlements.Count, controlLost,
            tradeUnits, next.TradeFlows.Count, unattributedGrainTransfer);

        var policy = new PolicyInForce[prev.Settlements.Count];
        for (int s = 0; s < prev.Settlements.Count; s++)
        {
            SettlementId id = prev.Settlements[s].Id;
            policy[s] = new PolicyInForce(id.Value, Shares(PolicyHistory.RowOrDefault(prev.SectorAllocations, id, out _)));
        }

        var causes = new CausesRecord(
            pop.Closing - pop.Opening, births - deaths - starvation,
            grainStock.Closing - grainStock.Opening, endowment + harvest - eaten - spoilage - overflow);

        return new TurnRecord(
            next.Clock.Turn, next.Clock.WorldDateYears, next.Clock.DtYears,
            stocks.ToArray(), population, grainAccount, dwellings, goodAccounts.ToArray(),
            flows, orders, policy, causes);
    }

    private static T Account<T>(long opening, long closing, long netFlow, Func<long, long, bool, long, T> make)
    {
        long discrepancy = closing - (opening + netFlow);
        return make(opening, closing, discrepancy == 0, discrepancy);
    }

    /// <summary>Adds the quantity's stock record when anything carries it in prev
    /// or next, or the ledger has a row for it. A quantity with no carrier and no
    /// row (the toy stocks on a founded world) is simply not present — an empty
    /// account would read as "reconciles" and prove nothing.</summary>
    private static void AddIfPresent(
        List<StockRecord> into, IReadOnlyWorldState prev, IReadOnlyWorldState next,
        ConservedQuantityId q, string name)
    {
        long opening = Carriers(prev, q, out bool anyPrev);
        long closing = Carriers(next, q, out bool anyNext);
        bool anyLedger = false;
        for (int i = 0; i < next.LedgerFlows.Count; i++)
            if (next.LedgerFlows[i].Quantity == q) { anyLedger = true; break; }
        if (!anyPrev && !anyNext && !anyLedger) return;
        into.Add(Stock(prev, next, q, name, opening, closing));
    }

    private static StockRecord Stock(
        IReadOnlyWorldState prev, IReadOnlyWorldState next, ConservedQuantityId q, string name,
        long opening, long closing)
    {
        // Ledger rows are only ever appended (Ledger.AddFlowRow), so every prev
        // row is in next; a next row absent from prev differenced against 0.
        var sources = new List<FlowLeg>();
        var sinks = new List<FlowLeg>();
        for (int i = 0; i < next.LedgerFlows.Count; i++)
        {
            LedgerFlowRow row = next.LedgerFlows[i];
            if (row.Quantity != q) continue;
            long prevSourced = 0, prevSunk = 0;
            for (int j = 0; j < prev.LedgerFlows.Count; j++)
            {
                LedgerFlowRow p = prev.LedgerFlows[j];
                if (p.Quantity != q || p.Reason != row.Reason) continue;
                prevSourced = p.TotalSourced;
                prevSunk = p.TotalSunk;
                break;
            }
            string reason = ReasonNames.Of(row.Reason.Value);
            sources.Add(new FlowLeg(row.Reason.Value, reason, row.TotalSourced - prevSourced));
            sinks.Add(new FlowLeg(row.Reason.Value, reason, row.TotalSunk - prevSunk));
        }
        FlowLeg[] src = sources.ToArray();
        FlowLeg[] snk = sinks.ToArray();
        // Ascending reason id — an integer key, so the order is total.
        Array.Sort(src, static (a, b) => a.Reason.CompareTo(b.Reason));
        Array.Sort(snk, static (a, b) => a.Reason.CompareTo(b.Reason));
        long sourced = 0, sunk = 0;
        for (int i = 0; i < src.Length; i++) sourced += src[i].Units;
        for (int i = 0; i < snk.Length; i++) sunk += snk[i].Units;
        long discrepancy = closing - (opening + sourced - sunk);
        return new StockRecord(q.Value, name, opening, closing, src, snk, discrepancy == 0, discrepancy);
    }

    /// <summary>The conserved carriers of a quantity, summed: Biomass rows, the
    /// toy Goods rows, Buckets + Notables (Population), Housing (Dwellings), or
    /// GoodStocks for one good — the same carriers ConservationAuditor sums.</summary>
    private static long Carriers(IReadOnlyWorldState w, ConservedQuantityId q, out bool any)
    {
        long sum = 0;
        any = false;
        if (q == ConservedQuantityIds.Biomass)
        {
            for (int i = 0; i < w.Biomass.Count; i++) { sum += w.Biomass[i].Biomass.Value; any = true; }
        }
        else if (q == ConservedQuantityIds.ToyGood)
        {
            for (int i = 0; i < w.Goods.Count; i++) { sum += w.Goods[i].Amount.Value; any = true; }
        }
        else if (q == ConservedQuantityIds.Population)
        {
            for (int i = 0; i < w.Buckets.Count; i++) { sum += w.Buckets[i].Count.Value; any = true; }
            for (int i = 0; i < w.Notables.Count; i++) { sum += w.Notables[i].Count.Value; any = true; }
        }
        else if (q == ConservedQuantityIds.Dwellings)
        {
            for (int i = 0; i < w.Housing.Count; i++) { sum += w.Housing[i].Dwellings.Value; any = true; }
        }
        else if (ConservedQuantityIds.IsGood(q))
        {
            GoodId good = ConservedQuantityIds.GoodOf(q);
            for (int i = 0; i < w.GoodStocks.Count; i++)
                if (w.GoodStocks[i].Good == good) { sum += w.GoodStocks[i].Amount.Value; any = true; }
        }
        return sum;
    }

    private static StockRecord Find(List<StockRecord> stocks, ConservedQuantityId q)
    {
        for (int i = 0; i < stocks.Count; i++) if (stocks[i].Quantity == q.Value) return stocks[i];
        // Not carried anywhere and never flowed: an all-zero account, which
        // reconciles trivially and is reported as such (0 == 0 is not a hope).
        return new StockRecord(q.Value, "absent", 0, 0, [], [], true, 0);
    }

    private static long Leg(FlowLeg[] legs, ReasonId reason)
    {
        for (int i = 0; i < legs.Length; i++) if (legs[i].Reason == reason.Value) return legs[i].Units;
        return 0;
    }

    // ------------------------------------------------------------------ §3 --

    private static SettlementRecord Settlement(
        IReadOnlyWorldState prev, IReadOnlyWorldState next, SimConfig cfg, BasketBook baskets,
        MigrationPlan plan, int[] planRow, OrderApplied[] orders, SettlementId id, bool founded)
    {
        GoodsConfig goods = cfg.Goods!;
        var grain = new GoodId(goods.GrainId);
        long foundedTurn = 0;
        for (int i = 0; i < next.Settlements.Count; i++)
            if (next.Settlements[i].Id == id) { foundedTurn = next.Settlements[i].FoundedTurn; break; }
        int controller = EmpireQuery.TryGetController(next, id, out PolityId polity) ? polity.Value : -1;

        PopulationSection population = Population(prev, next, cfg, id);
        double[] shares = Shares(PolicyHistory.RowOrDefault(prev.SectorAllocations, id, out bool prevRow));

        var own = new List<OrderApplied>();
        for (int i = 0; i < orders.Length; i++) if (orders[i].Settlement == id.Value) own.Add(orders[i]);

        return new SettlementRecord(
            id.Value, foundedTurn, controller, founded,
            population,
            Food(prev, next, goods, baskets, grain, id),
            Housing(prev, next, cfg, id, population.Closing),
            Economy(next, cfg, id, shares, prevRow),
            Social(next, cfg, id),
            Migration(prev, next, grain, id),
            Policy(next, id, shares),
            own.ToArray(),
            FoodStateOf(prev, next, cfg, baskets, id),
            MigrationPlanOf(prev, next, cfg, plan, planRow, id));
    }

    private static PopulationSection Population(
        IReadOnlyWorldState prev, IReadOnlyWorldState next, SimConfig cfg, SettlementId id)
    {
        long opening = People(prev, id, out _);
        long closing = People(next, id, out long notables);
        long births = 0, deaths = 0;
        for (int i = 0; i < next.SettlementVitals.Count; i++)
        {
            if (next.SettlementVitals[i].Settlement != id) continue;
            births = next.SettlementVitals[i].Births;
            deaths = next.SettlementVitals[i].Deaths;
            break;
        }
        long inflow = 0, outflow = 0;
        for (int i = 0; i < next.MigrationFlows.Count; i++)
        {
            if (next.MigrationFlows[i].Settlement != id) continue;
            inflow = next.MigrationFlows[i].Inflow;
            outflow = next.MigrationFlows[i].Outflow;
            break;
        }
        ClassEntry[] classes = cfg.Registries.Classes;
        var counts = new ClassCount[classes.Length];
        for (int c = 0; c < classes.Length; c++)
        {
            long n = 0;
            for (int i = 0; i < next.Buckets.Count; i++)
            {
                BucketRow b = next.Buckets[i];
                if (b.Settlement == id && b.Class.Value == classes[c].Id) n += b.Count.Value;
            }
            counts[c] = new ClassCount(classes[c].Id, classes[c].Name, n);
        }
        return new PopulationSection(
            opening, closing,
            BandViews.Children(next.Buckets, id), BandViews.Adults(next.Buckets, id), BandViews.Elders(next.Buckets, id),
            notables, births, deaths, inflow, outflow,
            opening + births - deaths + inflow - outflow - closing, ColonistsDepartedIdentity,
            counts);
    }

    private static long People(IReadOnlyWorldState w, SettlementId id, out long notables)
    {
        long sum = 0;
        for (int i = 0; i < w.Buckets.Count; i++)
            if (w.Buckets[i].Settlement == id) sum += w.Buckets[i].Count.Value;
        notables = 0;
        for (int i = 0; i < w.Notables.Count; i++)
            if (w.Notables[i].Settlement == id) notables += w.Notables[i].Count.Value;
        return sum + notables;
    }

    private static FoodSection Food(
        IReadOnlyWorldState prev, IReadOnlyWorldState next, GoodsConfig goods, BasketBook baskets,
        GoodId grain, SettlementId id)
    {
        int pi = GoodStockIndex.IndexOf(prev.GoodStocks, id, grain);
        int ni = GoodStockIndex.IndexOf(next.GoodStocks, id, grain);
        long opening = pi >= 0 ? prev.GoodStocks[pi].Amount.Value : 0;
        long closing = ni >= 0 ? next.GoodStocks[ni].Amount.Value : 0;
        long harvest = ni >= 0 ? next.GoodStocks[ni].LastProducedUnits : 0;
        long eaten = ni >= 0 ? next.GoodStocks[ni].LastConsumptionEatenUnits : 0;

        long demandUnits = 0;
        double deficit = 0.0;
        for (int i = 0; i < next.ConsumptionDeficits.Count; i++)
        {
            if (next.ConsumptionDeficits[i].Settlement != id) continue;
            demandUnits = next.ConsumptionDeficits[i].DemandUnits;
            deficit = next.ConsumptionDeficits[i].DeficitRatio;
            break;
        }

        // THE SIMULATION'S FOOD SET, not a second one: BasketBook.FoodGoods,
        // ascending by good id (BasketBook.cs:78-82). Every good here carries a
        // Sustenance basket line, so every unit counted below is a
        // person-year-equivalent of nutrition and commensurable with the
        // requirement in DemandUnits.
        ReadOnlySpan<GoodId> foodGoods = baskets.FoodGoods;
        var foods = new List<FoodGood>(foodGoods.Length);
        long obtained = 0;
        long producedTotal = 0;
        for (int g = 0; g < foodGoods.Length; g++)
        {
            GoodEntry good = goods.ById(foodGoods[g].Value);
            int idx = GoodStockIndex.IndexOf(next.GoodStocks, id, foodGoods[g]);
            long produced = 0, demand = 0, ate = 0;
            if (idx >= 0)
            {
                GoodStockRow row = next.GoodStocks[idx];
                produced = row.LastProducedUnits;
                demand = row.LastConsumptionDemandUnits;
                ate = row.LastConsumptionEatenUnits;
            }
            foods.Add(new FoodGood(good.Id, good.Name, produced, demand, ate));
            obtained += ate;
            // T4.20 FoodProduced: SUMMED over the same READ LastProducedUnits
            // this loop already carries per good. It is the SAME quantity
            // ClassMobilitySystem forms as its food-surplus numerator
            // (ClassMobility/ClassMobilitySystem.cs:131-135) - one definition,
            // not a second one, BY CONSTRUCTION and not by coincidence: the
            // loop iterates the same BasketBook.FoodGoods span that system
            // iterates. No coefficient, no threshold, no formula, just the
            // integer sum of rows the production system wrote.
            producedTotal += produced;
        }

        return new FoodSection(
            opening, closing, harvest, eaten,
            opening + harvest - eaten - closing, StoreLossesIdentity,
            demandUnits, deficit, foods.ToArray(), obtained,
            producedTotal,
            // T4.20 FoodBalance: DIFFERENCED. Both terms are per-turn totals in
            // person-year-equivalents, so the subtraction is dimensionally
            // sound and exact in long arithmetic. It is NOT the food surplus
            // RATIO: that variable is published by ClassMobilitySystem from the
            // PREVIOUS world and is therefore one turn behind this figure.
            producedTotal - demandUnits);
    }

    private static HousingSection Housing(
        IReadOnlyWorldState prev, IReadOnlyWorldState next, SimConfig cfg, SettlementId id, long populationClosing)
    {
        double ppd = cfg.Housing.PersonsPerDwelling;
        long opening = 0;
        for (int i = 0; i < prev.Housing.Count; i++)
            if (prev.Housing[i].Settlement == id) { opening = prev.Housing[i].Dwellings.Value; break; }
        bool hasRow = false;
        long closing = 0;
        double maintenance = 0.0, labor = 0.0;
        for (int i = 0; i < next.Housing.Count; i++)
        {
            if (next.Housing[i].Settlement != id) continue;
            hasRow = true;
            closing = next.Housing[i].Dwellings.Value;
            maintenance = next.Housing[i].LastMaintenanceFraction;
            labor = next.Housing[i].LastLaborUsed;
            break;
        }
        return new HousingSection(
            hasRow, opening, closing, closing * ppd, populationClosing / ppd,
            SettlementHappiness.HousingSufficiency(next, id, cfg),
            maintenance, labor, BuiltDecayedGap);
    }

    private static EconomySection Economy(
        IReadOnlyWorldState next, SimConfig cfg, SettlementId id, double[] shares, bool prevRow)
    {
        GoodsConfig goods = cfg.Goods!;
        var readings = new GoodReading[goods.Goods.Length];
        for (int g = 0; g < goods.Goods.Length; g++)
        {
            GoodEntry good = goods.Goods[g];
            var gid = new GoodId(good.Id);
            int idx = GoodStockIndex.IndexOf(next.GoodStocks, id, gid);
            long stock = 0, produced = 0, inputDemand = 0, consumptionDemand = 0, eaten = 0;
            if (idx >= 0)
            {
                GoodStockRow row = next.GoodStocks[idx];
                stock = row.Amount.Value;
                produced = row.LastProducedUnits;
                inputDemand = row.LastInputDemandUnits;
                consumptionDemand = row.LastConsumptionDemandUnits;
                eaten = row.LastConsumptionEatenUnits;
            }
            double price = double.NaN;
            for (int i = 0; i < next.Prices.Count; i++)
                if (next.Prices[i].Settlement == id && next.Prices[i].Good == gid) { price = next.Prices[i].Price; break; }
            readings[g] = new GoodReading(good.Id, good.Name, stock, produced, inputDemand, consumptionDemand, eaten, price);
        }

        var tradeIn = new List<TradeLeg>();
        var tradeOut = new List<TradeLeg>();
        for (int i = 0; i < next.TradeFlows.Count; i++)
        {
            TradeFlowRow row = next.TradeFlows[i];
            if (row.To == id)
                tradeIn.Add(new TradeLeg(row.From.Value, row.Good.Value, GoodName(goods, row.Good.Value), row.Quantity));
            else if (row.From == id)
                tradeOut.Add(new TradeLeg(row.To.Value, row.Good.Value, GoodName(goods, row.Good.Value), row.Quantity));
        }

        double surplus = double.NaN, artisan = double.NaN, volume = double.NaN;
        for (int i = 0; i < next.Variables.Count; i++)
        {
            VariableRow v = next.Variables[i];
            if (v.Settlement != id) continue;
            if (v.VarId == Variables.FoodSurplusRatio) surplus = v.Value;
            else if (v.VarId == Variables.ArtisanShare) artisan = v.Value;
            else if (v.VarId == Variables.TradeVolume) volume = v.Value;
        }

        ClassEntry[] classes = cfg.Registries.Classes;
        var active = new ClassActive[classes.Length];
        for (int c = 0; c < classes.Length; c++)
        {
            int latch = 0;
            for (int i = 0; i < next.ClassStates.Count; i++)
                if (next.ClassStates[i].Settlement == id && next.ClassStates[i].Class.Value == classes[c].Id)
                { latch = next.ClassStates[i].Active; break; }
            active[c] = new ClassActive(classes[c].Id, classes[c].Name, latch);
        }

        return new EconomySection(
            readings, tradeIn.ToArray(), tradeOut.ToArray(), shares, prevRow,
            surplus, artisan, volume, active);
    }

    private static SocialSection Social(IReadOnlyWorldState next, SimConfig cfg, SettlementId id)
    {
        var factors = new double[SettlementHappiness.FactorCount];
        SettlementHappiness.Factors(next, id, cfg, factors);
        double happiness = SettlementHappiness.Of(next, id, cfg);

        var grievance = new List<GrievanceReading>();
        for (int i = 0; i < next.Grievances.Count; i++)
        {
            GrievanceRow row = next.Grievances[i];
            if (row.Settlement != id) continue;
            grievance.Add(new GrievanceReading(row.Class.Value, ClassName(cfg, row.Class.Value), row.Value));
        }
        var needs = new List<NeedReading>();
        for (int i = 0; i < next.NeedSatisfactions.Count; i++)
        {
            NeedSatisfactionRow row = next.NeedSatisfactions[i];
            if (row.Settlement != id) continue;
            needs.Add(new NeedReading(row.Class.Value, row.NeedId, NeedName(cfg, row.NeedId), row.Value));
        }
        return new SocialSection(happiness, factors, grievance.ToArray(), needs.ToArray());
    }

    private static MigrationSection Migration(
        IReadOnlyWorldState prev, IReadOnlyWorldState next, GoodId grain, SettlementId id)
    {
        double push = 0.0;
        for (int i = 0; i < prev.ConsumptionDeficits.Count; i++)
            if (prev.ConsumptionDeficits[i].Settlement == id) { push = prev.ConsumptionDeficits[i].DeficitRatio; break; }
        double pull = double.NaN;
        var all = new AttractivenessReading[prev.SmoothedAttractiveness.Count];
        for (int i = 0; i < prev.SmoothedAttractiveness.Count; i++)
        {
            SmoothedAttractivenessRow row = prev.SmoothedAttractiveness[i];
            all[i] = new AttractivenessReading(row.Settlement.Value, row.Value);
            if (row.Settlement == id) pull = row.Value;
        }
        long stock = 0, lastHarvest = 0;
        int gi = GoodStockIndex.IndexOf(prev.GoodStocks, id, grain);
        if (gi >= 0) { stock = prev.GoodStocks[gi].Amount.Value; lastHarvest = prev.GoodStocks[gi].LastProducedUnits; }
        double unplaced = 0.0, remainder = 0.0;
        for (int i = 0; i < next.Buckets.Count; i++)
        {
            BucketRow b = next.Buckets[i];
            if (b.Settlement != id) continue;
            unplaced += b.UnplacedDeparture;
            remainder += b.UnplacedRemainder;
        }
        return new MigrationSection(
            push, pull, all, stock, lastHarvest,
            unplaced, remainder, PairwiseGap);
    }

    // ------------------------------------------------- §3.11 (T4.21-5) --

    /// <summary>
    /// The exceptional-famine readout. EVERY value is either READ from a row of
    /// the named world or RECOMPUTED by calling a PUBLIC static of the
    /// simulation on the SAME world the simulation called it on. No predicate,
    /// no threshold and no quotient is restated here — the one division
    /// (X / D) is the RATIO of two values this method obtained, not a re-derivation
    /// of either.
    /// </summary>
    private static FoodStateSection FoodStateOf(
        IReadOnlyWorldState prev, IReadOnlyWorldState next, SimConfig cfg, BasketBook baskets, SettlementId id)
    {
        bool prevPresent = HasSettlement(prev, id);

        // The classification in force for the step just observed (spec §3.11:
        // PREV at every caller — the deficit this step read, the disaster
        // multiplier its harvest carried, the sector row its production obeyed).
        FoodStateKind state = Sim.Core.State.FoodState.Of(prev, id, cfg, out FamineReason reason);
        double d = Sim.Core.State.FoodState.DeficitRatio(prev, id);
        double dEff = Sim.Core.State.FoodState.EffectiveDeficit(d, state, cfg);
        bool abandoned = Sim.Core.State.FoodState.IsAbandoned(prev, id);

        (bool applied, DisasterRow appliedRow) = Disaster(prev, id);
        (bool pending, DisasterRow pendingRow) = Disaster(next, id);

        bool weather = false;
        double weatherMultiplier = double.NaN;
        for (int i = 0; i < prev.HarvestWeather.Count; i++)
        {
            if (prev.HarvestWeather[i].Settlement != id) continue;
            weather = true;
            weatherMultiplier = prev.HarvestWeather[i].Multiplier;
            break;
        }

        double[] cohortWeights = cfg.Consumption.CohortWeights;
        double limit = FoodHeadroom.Limit(prev, id, cohortWeights, baskets);
        double vacancy = FoodHeadroom.Vacancy(prev, id, cohortWeights, baskets);

        // X / D. D is READ (the prev demand row); X is RECOMPUTED through
        // FoodHeadroom.FeedableAtLimit — the same fixed point Limit uses. With
        // no row, or D <= 0, the ratio HAS no denominator: NaN, which the
        // telemetry writes as "NaN" and which stays distinguishable from 0.
        long demand = 0;
        bool haveDemand = false;
        for (int i = 0; i < prev.ConsumptionDeficits.Count; i++)
        {
            if (prev.ConsumptionDeficits[i].Settlement != id) continue;
            demand = prev.ConsumptionDeficits[i].DemandUnits;
            haveDemand = true;
            break;
        }
        double surplusRatio = haveDemand && demand > 0
            ? FoodHeadroom.FeedableAtLimit(prev, id, baskets, demand) / demand
            : double.NaN;

        return new FoodStateSection(
            prevPresent, state, reason, d, dEff, abandoned,
            applied, appliedRow.Kind, appliedRow.Severity,
            // BOTH multipliers, named for what they are (T4.21-6). Multiplier is
            // THIS step's factor; AppliedMultiplier is the one the step that WROTE
            // the row applied, which is what FoodState.IsStruck — and so State and
            // Reason two lines up — is decided on. Recording only the first put a
            // "disaster: none applied" line beside a FAMINE classification.
            appliedRow.Multiplier, appliedRow.AppliedMultiplier, appliedRow.RemainingYears,
            pending, pendingRow.Kind, pendingRow.Severity, pendingRow.Multiplier, pendingRow.RemainingYears,
            weather, weatherMultiplier,
            limit, vacancy, surplusRatio,
            DemographicsSystem.Headroom(prev, id, cfg));
    }

    /// <summary>The settlement's DisasterRow in the world passed in, and whether
    /// it had one. The absent row reads as the identity the simulation reads:
    /// multiplier 1.0, nothing pending (DisasterSystem: "absent → 1.0").</summary>
    private static (bool Present, DisasterRow Row) Disaster(IReadOnlyWorldState w, SettlementId id)
    {
        for (int i = 0; i < w.Disasters.Count; i++)
            if (w.Disasters[i].Settlement == id) return (true, w.Disasters[i]);
        return (false, new DisasterRow(id, 0, 0.0, 0.0, 1.0, 1.0));
    }

    /// <summary>prev settlement id → its row index in the plan's arrays, −1 for
    /// an id prev does not carry. An array, not a dictionary (law 5).</summary>
    private static int[] PlanRowBySettlementId(IReadOnlyWorldState prev)
    {
        int maxId = 0;
        for (int s = 0; s < prev.Settlements.Count; s++)
            maxId = Math.Max(maxId, prev.Settlements[s].Id.Value);
        var index = new int[maxId + 1];
        Array.Fill(index, -1);
        for (int s = 0; s < prev.Settlements.Count; s++) index[prev.Settlements[s].Id.Value] = s;
        return index;
    }

    /// <summary>
    /// The planner's decisions for this settlement, INDEXED out of the plan the
    /// simulation's own static built. The only value not indexed is φ at cohort
    /// profile 1, and it is obtained by CALLING MigrationSystem's two statics —
    /// <c>FlightFractionOf</c> (the one expression Plan multiplies per bucket)
    /// and <c>FlightHazardScale</c> (the one product K) — never by restating
    /// either. A settlement absent from prev was never planned: PlanRecorded
    /// false and every reading NaN, because "not planned" is not "planned to
    /// zero".
    /// </summary>
    private static MigrationPlanSection MigrationPlanOf(
        IReadOnlyWorldState prev, IReadOnlyWorldState next, SimConfig cfg,
        MigrationPlan plan, int[] planRow, SettlementId id)
    {
        double dt = next.Clock.DtYears;
        int i = id.Value >= 0 && id.Value < planRow.Length ? planRow[id.Value] : -1;
        if (i < 0)
        {
            return new MigrationPlanSection(
                false, dt,
                double.NaN, double.NaN, double.NaN, double.NaN, double.NaN, double.NaN, double.NaN,
                double.NaN, double.NaN, double.NaN, double.NaN, double.NaN, double.NaN, double.NaN,
                InflowAeByChannelGap);
        }

        double omega = plan.ExitOpenness[i];
        double phiPrime = MigrationSystem.FlightFractionOf(
            1.0, MigrationSystem.FlightHazardScale(cfg.Migration), omega, plan.Deficit[i], plan.DtYears);

        return new MigrationPlanSection(
            true, dt,
            omega, phiPrime, plan.FlightBound[i], plan.FlightOut[i],
            plan.GapOut[i], plan.GapOutflowCap[i], plan.SrcScale[i],
            plan.FlightIn[i], plan.GapIn[i], plan.GapInflowCap[i], plan.DestScale[i],
            plan.VacancyCap[i], plan.DesiredInflowAe[i], plan.VacancyScale[i],
            InflowAeByChannelGap);
    }

    private static PolicySection Policy(IReadOnlyWorldState next, SettlementId id, double[] effective)
    {
        SectorAllocationRow row = PolicyHistory.RowOrDefault(next.SectorAllocations, id, out bool present);
        var declared = new double[Sectors.Count];
        for (int s = 0; s < Sectors.Count; s++) declared[s] = Sectors.Raw(row, s);
        return new PolicySection(declared, present, effective);
    }

    // ------------------------------------------------------------ helpers --

    private static double[] Shares(in SectorAllocationRow row)
    {
        var shares = new double[Sectors.Count];
        for (int s = 0; s < Sectors.Count; s++) shares[s] = Sectors.Share(row, s);
        return shares;
    }

    private static bool HasSettlement(IReadOnlyWorldState w, SettlementId id)
    {
        for (int i = 0; i < w.Settlements.Count; i++) if (w.Settlements[i].Id == id) return true;
        return false;
    }

    private static string GoodName(GoodsConfig goods, int id)
    {
        for (int i = 0; i < goods.Goods.Length; i++) if (goods.Goods[i].Id == id) return goods.Goods[i].Name;
        return "good-" + id.ToString(System.Globalization.CultureInfo.InvariantCulture);
    }

    private static string ClassName(SimConfig cfg, int id)
    {
        ClassEntry[] classes = cfg.Registries.Classes;
        for (int i = 0; i < classes.Length; i++) if (classes[i].Id == id) return classes[i].Name;
        return "class-" + id.ToString(System.Globalization.CultureInfo.InvariantCulture);
    }

    private static string NeedName(SimConfig cfg, int id)
    {
        NeedEntry[] needs = cfg.Needs!.Needs;
        for (int i = 0; i < needs.Length; i++) if (needs[i].Id == id) return needs[i].Name;
        return "need-" + id.ToString(System.Globalization.CultureInfo.InvariantCulture);
    }
}
