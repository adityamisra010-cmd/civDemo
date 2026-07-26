using Sim.Core.Kernel;
using Sim.Core.State;

namespace Sim.Core.Systems.Production;

/// <summary>
/// Writable handles to ProductionSystem's tables (built by SystemCatalog only).
/// GoodStocks is the SHARED conserved stock (see SystemCatalog): Production
/// credits outputs (Harvest for grain, Produced for everything else), debits
/// recipe inputs (InputsConsumed) and farm-tool depreciation (ToolWear), and
/// owns ProduceRemainder plus the input rows' ConsumeRemainder for its flows;
/// Consumption debits grain (Eaten) with its own remainder.
/// </summary>
public readonly record struct ProductionTables(Table<GoodStockRow> GoodStocks);

/// <summary>
/// Production (T3.3, D-032) — the successor of FarmingSystem (T1.5/T1.8),
/// which it REPLACES in pipeline slot "production" (WellKnownId 5): the M2
/// grain monoculture generalizes to four working sectors over the D-031 goods
/// roster. The player allocates labor across SECTORS (the SectorAllocations
/// row, D-032); WITHIN a sector, what gets produced is decided by the sim from
/// local endowment — and, from T3.4, relative prices. AT T3.3 PRICES DO NOT
/// EXIST YET, so every within-sector weighting below is the flat-price special
/// case (endowment-only); the header of each block says what T3.4 changes.
///
/// LABOR MODEL (stated because it is a deliberate simplification): sector
/// pools are CLASS-BLIND — pool = normalizedShare × ALL adults. Classes act
/// through recipe availability gates (the D-020 `requires` predicates over
/// artisan_share) and, from T3.5, consumption baskets — not through separate
/// labor pools. The M2 couplings this replaces (farm labor = peasant adults;
/// artisans as a farm-yield multiplier and weighted construction labor) were
/// mandated scaffolding, demolished this packet.
///
/// SECTORS, in fixed order (law 5 — arrays, registry order, no dictionaries):
///  - FARMING → grain, via the T1.8 Leontief amended for REAL TOOLS:
///      harvest/yr = min(arableKm2 × YieldPerArableKm2PerYear,
///                       farmLabor × OutputPerFarmerPerYear × toolFactor)
///    toolFactor = 1 + ToolYieldBonusMax × equipRatio, where equipRatio =
///    min(1, toolStock / (farmLabor × ToolsPerFarmerToEquip)) — the M2
///    artisan-share multiplier is GONE; the bonus now comes from a conserved
///    stock the settlement must craft and replace. Equipped farmers wear tools
///    out at ToolWearPerEquippedFarmerPerYear (Ledger sink ToolWear), so the
///    bonus DEPLETES: stock raises yield, use consumes stock (law 2 — a
///    mechanism, not a modifier). Grain keeps reason Harvest and
///    LastProducedUnits (food_surplus_ratio and migration's anyFood gate
///    depend on the grain row exactly as at M2).
///  - HERDING/FISHING → livestock and fish, rate = pool × w_g ×
///    OutputPerHerderPerYear × abundance_g from the founding DepositRow;
///    w_g = abundance_g / Σ abundance (flat-price endowment weighting; T3.4
///    re-weights by price). No deposit rows ⇒ nothing produced.
///  - EXTRACTION → every deposit-bearing raw good, same shape with
///    OutputPerExtractorPerYear. Labor follows abundance AND yields more on
///    rich ground, so output concentrates as abundance² / Σ — stated, not
///    hidden: workers go where the seam is rich, and the rich seam also gives
///    more per digger. This concentration is the comparative-advantage
///    precondition doing its job.
///  - CRAFTING → executes the D-031 recipe book. A recipe is AVAILABLE when
///    its D-020 `requires` predicate (if any) is true against the settlement's
///    PREV published variables — a knowledge/class gate, never a date (law 4).
///    Crafting labor splits EQUALLY among available recipes (flat-price case;
///    T3.4 weights by output price). Per recipe, output is Leontief-limited by
///    labor (pool / LaborPerOutput) and by EVERY input stock (stock_i /
///    PerOutput_i) — output scales with labor and inputs and NEVER exceeds
///    what the inputs on hand can make: production from nothing is
///    structurally impossible, not merely discouraged. Inputs leave via
///    InputsConsumed against each input row's ConsumeRemainder; outputs enter
///    via Produced against the output row's ProduceRemainder. The whole-unit
///    discipline (D-004): exact output x is capped by min_i(stock_i /
///    PerOutput_i), so each input's rounded sink is ≤ its stock
///    (floor(x·per + rem) < stock + 1) and OverdrawPolicy.Throw is safe by
///    construction.
///  - CONSTRUCTION is NOT here: its pool feeds PathBuild (and housing from
///    T3.8) — one sector, consumed by a different system, same row.
///
/// ONE-TURN LAG (§3.2): catchment summary, sector shares, buckets, deposits
/// and variables are all read from Prev — an order lands one turn before it
/// steers output, exactly as at T1.6. All rates per-sim-year × dtYears
/// (law 3). STATELESS: config is immutable tuning; remainders live in rows.
/// </summary>
public sealed class ProductionSystem : ISimSystem<ProductionTables>
{
    public static readonly SystemId WellKnownId = new(5);
    public const string Name = "production";

    private readonly SimConfig _cfg;
    private readonly GoodId _grain;
    private readonly GoodId _tools;
    private readonly GoodId _livestock;
    private readonly GoodId _fish;

    /// <summary>Per-recipe parsed `requires` predicates (null = always
    /// available), aligned with cfg.Goods.Recipes by index — parsed once at
    /// construction (validation already proved they parse at load).</summary>
    private readonly ClassMobility.Predicate?[] _requires;

    public ProductionSystem(SimConfig cfg)
    {
        _cfg = cfg;
        GoodsConfig goods = cfg.Goods ?? throw new ArgumentException(
            "ProductionSystem requires SimConfig.Goods (goods.json) — production is the goods economy.");
        _grain = new GoodId(goods.GrainId);
        _tools = RequiredGood(goods, "tools");
        _livestock = RequiredGood(goods, "livestock");
        _fish = RequiredGood(goods, "fish");
        _requires = new ClassMobility.Predicate?[goods.Recipes.Length];
        for (int i = 0; i < goods.Recipes.Length; i++)
            _requires[i] = goods.Recipes[i].Requires is { } src
                ? ClassMobility.Predicate.Parse(src) : null;
    }

    private static GoodId RequiredGood(GoodsConfig goods, string name)
    {
        int id = goods.IdOf(name);
        return id > 0 ? new GoodId(id) : throw new ArgumentException(
            $"ProductionSystem requires a '{name}' good in goods.json (D-031 roster).");
    }

    public SystemId Id => WellKnownId;

    public void Step(SimContext<ProductionTables> ctx)
    {
        IReadOnlyWorldState prev = ctx.Prev;
        Table<GoodStockRow> stocks = ctx.Owned.GoodStocks;
        GoodsConfig goods = _cfg.Goods!;

        // LastProducedUnits is documented as "the units credited THIS turn", so it
        // must be ZEROED before the sectors run: a good that produces nothing
        // this turn has to report nothing, not last turn's figure. Without this
        // the success paths were the only writers, so a crafting run that ran
        // out of clay left the pottery row reporting its last good turn forever.
        // Grain was accidentally safe (farming always writes it), and grain is
        // the only consumer today — which is exactly why this had to be fixed
        // before T3.4/T3.5 start reading other goods' LastProduced.
        for (int i = 0; i < stocks.Count; i++)
        {
            stocks.Ref(i).LastProducedUnits = 0;
            // T3.4: the same staleness argument, for the same reason. An
            // observational field that is not written every turn reads as a
            // live measurement of a turn that never happened.
            stocks.Ref(i).LastInputDemandUnits = 0;
        }

        // Ascending settlement-row order — the fixed iteration order (law 5).
        for (int s = 0; s < prev.Settlements.Count; s++)
        {
            SettlementId settlement = prev.Settlements[s].Id;

            SectorAllocationRow shares = Sectors.Default(settlement);
            for (int i = 0; i < prev.SectorAllocations.Count; i++)
            {
                if (prev.SectorAllocations[i].Settlement == settlement)
                { shares = prev.SectorAllocations[i]; break; }
            }

            long adults = BandViews.Adults(prev.Buckets, settlement);

            Farm(ctx, prev, stocks, settlement,
                farmLabor: Sectors.Share(shares, Sectors.Farming) * adults);
            FromDeposits(ctx, prev, stocks, settlement,
                pool: Sectors.Share(shares, Sectors.Herding) * adults,
                perWorkerPerYear: _cfg.Production.OutputPerHerderPerYear,
                foodSector: true);
            FromDeposits(ctx, prev, stocks, settlement,
                pool: Sectors.Share(shares, Sectors.Extraction) * adults,
                perWorkerPerYear: _cfg.Production.OutputPerExtractorPerYear,
                foodSector: false);
            Craft(ctx, prev, stocks, goods, settlement,
                pool: Sectors.Share(shares, Sectors.Crafting) * adults);
        }
    }

    /// <summary>Farming: the Leontief with the REAL tool factor + tool wear.</summary>
    private void Farm(
        SimContext<ProductionTables> ctx, IReadOnlyWorldState prev,
        Table<GoodStockRow> stocks, SettlementId settlement, double farmLabor)
    {
        int grainRow = GoodStockIndex.IndexOf(stocks, settlement, _grain);
        if (grainRow < 0) return; // founding never endowed a store — nothing to credit
        // Non-positive labor guard, matching FromDeposits and Craft. Orders
        // validate weights into [0,100] so a legitimate run cannot get here with
        // negative labor, but a hand-edited or corrupted SNAPSHOT can (the
        // schema reads the five weights as raw doubles), and the failure mode
        // was an ArgumentOutOfRangeException out of Ledger rather than an
        // actionable message. Zero labor also means zero harvest and zero wear,
        // so returning early is the correct semantics either way.
        if (!(farmLabor > 0.0)) return; // NaN fails this too

        double arableKm2 = 0.0;
        for (int i = 0; i < prev.CatchmentSummaries.Count; i++)
        {
            if (prev.CatchmentSummaries[i].Settlement == settlement)
            { arableKm2 = prev.CatchmentSummaries[i].EffectiveArableKm2; break; }
        }

        // Tools equip farmers from the PREV tool stock (one-turn lag like every
        // other read): equipRatio in [0,1], factor saturates at full equipment.
        int toolRow = GoodStockIndex.IndexOf(stocks, settlement, _tools);
        long prevToolStock = 0;
        for (int i = 0; i < prev.GoodStocks.Count; i++)
        {
            if (prev.GoodStocks[i].Settlement == settlement && prev.GoodStocks[i].Good == _tools)
            { prevToolStock = prev.GoodStocks[i].Amount.Value; break; }
        }
        double toolsToEquip = farmLabor * _cfg.Production.ToolsPerFarmerToEquip;
        double equipRatio = toolsToEquip > 0.0
            ? Math.Min(1.0, prevToolStock / toolsToEquip) : 0.0;
        double toolFactor = 1.0 + _cfg.Production.ToolYieldBonusMax * equipRatio;

        double landSide = arableKm2 * _cfg.Farming.YieldPerArableKm2PerYear;
        double laborSide = farmLabor * _cfg.Farming.OutputPerFarmerPerYear * toolFactor;
        double ratePerYear = Math.Min(landSide, laborSide);

        ref GoodStockRow grain = ref stocks.Ref(grainRow);
        double exact = ratePerYear * ctx.DtYears + grain.ProduceRemainder;
        long harvested = ConservedMath.WholeUnits(exact, $"grain harvest (settlement {settlement.Value})");
        ctx.Ledger.Flow(ref grain.Amount, ConservedQuantityIds.OfGood(_grain), ReasonIds.Harvest,
            harvested, FlowDirection.Source, OverdrawPolicy.Throw);
        grain.ProduceRemainder = exact - harvested;
        grain.LastProducedUnits = harvested; // T2.2: numerator of food_surplus_ratio

        // Tool WEAR — the depletion half of the mechanism. Equipped farmers
        // (equipRatio × farmLabor) wear stock at the TUNE rate; the sink is
        // clamped to the stock (a settlement cannot wear out tools it no
        // longer has — the equip ratio already read the stock it used).
        if (toolRow >= 0 && equipRatio > 0.0)
        {
            ref GoodStockRow tools = ref stocks.Ref(toolRow);
            double wearExact = equipRatio * farmLabor
                * _cfg.Production.ToolWearPerEquippedFarmerPerYear * ctx.DtYears
                + tools.ConsumeRemainder;
            long worn = ConservedMath.WholeUnits(wearExact, $"tool wear (settlement {settlement.Value})");
            long sunk = ctx.Ledger.Flow(ref tools.Amount, ConservedQuantityIds.OfGood(_tools),
                ReasonIds.ToolWear, worn, FlowDirection.Sink, OverdrawPolicy.ClampToAvailable);
            tools.ConsumeRemainder = wearExact - worn;
            _ = sunk;
        }
    }

    /// <summary>
    /// Herding/fishing and extraction share one shape: the sector pool splits
    /// across this settlement's deposit-bearing goods ∝ abundance, and each
    /// good's rate is workers × perWorker × abundance. foodSector selects the
    /// two food deposits (livestock, fish) vs every raw deposit.
    /// </summary>
    private void FromDeposits(
        SimContext<ProductionTables> ctx, IReadOnlyWorldState prev,
        Table<GoodStockRow> stocks, SettlementId settlement,
        double pool, double perWorkerPerYear, bool foodSector)
    {
        if (pool <= 0.0) return;

        // Deposit rows are settlement-major, goods-registry order (T3.2) —
        // one pass collects this settlement's split weights deterministically.
        double abundanceSum = 0.0;
        for (int i = 0; i < prev.Deposits.Count; i++)
        {
            DepositRow d = prev.Deposits[i];
            if (d.Settlement != settlement || !InSector(d.Good, foodSector)) continue;
            abundanceSum += d.Abundance;
        }
        if (abundanceSum <= 0.0) return; // no endowment → no production (never from nothing)

        for (int i = 0; i < prev.Deposits.Count; i++)
        {
            DepositRow d = prev.Deposits[i];
            if (d.Settlement != settlement || !InSector(d.Good, foodSector)) continue;
            if (d.Abundance <= 0.0) continue;

            int row = GoodStockIndex.IndexOf(stocks, settlement, d.Good);
            if (row < 0) continue;

            double workers = pool * (d.Abundance / abundanceSum);
            double ratePerYear = workers * perWorkerPerYear * d.Abundance;

            ref GoodStockRow stock = ref stocks.Ref(row);
            double exact = ratePerYear * ctx.DtYears + stock.ProduceRemainder;
            long produced = ConservedMath.WholeUnits(
                exact, $"production of good {d.Good.Value} (settlement {settlement.Value})");
            ctx.Ledger.Flow(ref stock.Amount, ConservedQuantityIds.OfGood(d.Good),
                ReasonIds.Produced, produced, FlowDirection.Source, OverdrawPolicy.Throw);
            stock.ProduceRemainder = exact - produced;
            stock.LastProducedUnits = produced;
        }
    }

    private bool InSector(GoodId good, bool foodSector)
    {
        bool isFood = good == _livestock || good == _fish;
        return foodSector ? isFood : !isFood;
    }

    /// <summary>Crafting: available recipes split the pool; Leontief on labor
    /// AND every input; inputs sink, outputs source, dt-correct throughout.</summary>
    private void Craft(
        SimContext<ProductionTables> ctx, IReadOnlyWorldState prev,
        Table<GoodStockRow> stocks, GoodsConfig goods,
        SettlementId settlement, double pool)
    {
        if (pool <= 0.0 || goods.Recipes.Length == 0) return;

        // Availability: the D-020 gate against PREV published variables —
        // identical wiring to ClassMobility's emergence evaluation.
        Span<bool> available = stackalloc bool[goods.Recipes.Length];
        int availableCount = 0;
        for (int r = 0; r < goods.Recipes.Length; r++)
        {
            available[r] = _requires[r] is not { } predicate
                || predicate.Evaluate(varId => ReadVariable(prev, settlement, varId));
            if (available[r]) availableCount++;
        }
        if (availableCount == 0) return;

        // Flat-price case (T3.3): equal labor split across available recipes.
        // T3.4 replaces this weighting with relative output prices — the
        // D-032 "sim decides within the sector" clause becoming price-driven.
        double laborPerRecipe = pool / availableCount;

        for (int r = 0; r < goods.Recipes.Length; r++)
        {
            if (!available[r]) continue;
            RecipeEntry recipe = goods.Recipes[r];

            var output = new GoodId(goods.IdOf(recipe.Output.Good));
            int outRow = GoodStockIndex.IndexOf(stocks, settlement, output);
            if (outRow < 0) continue;

            // Output rate the LABOR allows (outputs per year), then capped by
            // every input's stock — Leontief over labor and materials.
            double laborCapPerYear = recipe.LaborPerOutput > 0.0
                ? laborPerRecipe / recipe.LaborPerOutput : double.PositiveInfinity;
            double exactOutput = laborCapPerYear * ctx.DtYears * recipe.Output.Qty;

            // T3.4 (D-033): publish the input demand this recipe WANTED, from
            // labor alone, BEFORE any input cap binds. Demand that went unmet
            // is precisely the signal that must move a scarce good's price —
            // record only what was consumed and a shortage becomes invisible to
            // the market exactly when it matters most. Observational, never a
            // stock; infinite labor capacity (LaborPerOutput 0) publishes
            // nothing rather than a garbage magnitude.
            if (double.IsFinite(exactOutput) && exactOutput > 0.0)
            {
                for (int i = 0; i < recipe.Inputs.Length; i++)
                {
                    var want = new GoodId(goods.IdOf(recipe.Inputs[i].Good));
                    int wantRow = GoodStockIndex.IndexOf(stocks, settlement, want);
                    if (wantRow < 0) continue;
                    double wanted = exactOutput * (recipe.Inputs[i].PerOutput / recipe.Output.Qty);
                    if (double.IsFinite(wanted) && wanted > 0.0)
                        stocks.Ref(wantRow).LastInputDemandUnits += (long)wanted;
                }
            }

            for (int i = 0; i < recipe.Inputs.Length; i++)
            {
                var input = new GoodId(goods.IdOf(recipe.Inputs[i].Good));
                int inRow = GoodStockIndex.IndexOf(stocks, settlement, input);
                long stockAmount = inRow >= 0 ? stocks[inRow].Amount.Value : 0;
                double perOutput = recipe.Inputs[i].PerOutput / recipe.Output.Qty;
                exactOutput = Math.Min(exactOutput, perOutput > 0.0
                    ? stockAmount / perOutput : exactOutput);
            }
            if (exactOutput <= 0.0) continue; // no inputs (or no labor) → NOTHING, never from nothing

            // Inputs sink first (whole units through each input row's
            // ConsumeRemainder), then the output sources — order fixed so the
            // ledger audit reads inputs-before-outputs every turn.
            for (int i = 0; i < recipe.Inputs.Length; i++)
            {
                var input = new GoodId(goods.IdOf(recipe.Inputs[i].Good));
                int inRow = GoodStockIndex.IndexOf(stocks, settlement, input);
                if (inRow < 0) continue;
                double perOutput = recipe.Inputs[i].PerOutput / recipe.Output.Qty;
                ref GoodStockRow inStock = ref stocks.Ref(inRow);
                double exactIn = exactOutput * perOutput + inStock.ConsumeRemainder;
                long sunk = ConservedMath.WholeUnits(
                    exactIn, $"recipe '{recipe.Name}' input {input.Value} (settlement {settlement.Value})");
                ctx.Ledger.Flow(ref inStock.Amount, ConservedQuantityIds.OfGood(input),
                    ReasonIds.InputsConsumed, sunk, FlowDirection.Sink, OverdrawPolicy.Throw);
                inStock.ConsumeRemainder = exactIn - sunk;
            }

            ref GoodStockRow outStock = ref stocks.Ref(outRow);
            double exactOut = exactOutput + outStock.ProduceRemainder;
            long produced = ConservedMath.WholeUnits(
                exactOut, $"recipe '{recipe.Name}' output (settlement {settlement.Value})");
            ctx.Ledger.Flow(ref outStock.Amount, ConservedQuantityIds.OfGood(output),
                ReasonIds.Produced, produced, FlowDirection.Source, OverdrawPolicy.Throw);
            outStock.ProduceRemainder = exactOut - produced;
            outStock.LastProducedUnits = produced;
        }
    }

    /// <summary>PREV published variable for this settlement (0.0 when absent —
    /// turn-0 semantics identical to ClassMobility's emergence reads).</summary>
    private static double ReadVariable(IReadOnlyWorldState prev, SettlementId settlement, int varId)
    {
        for (int i = 0; i < prev.Variables.Count; i++)
        {
            if (prev.Variables[i].Settlement == settlement && prev.Variables[i].VarId == varId)
                return prev.Variables[i].Value;
        }
        return 0.0;
    }
}
