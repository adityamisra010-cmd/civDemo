using Sim.Core.Kernel;
using Sim.Core.State;

namespace Sim.Core.Systems.Housing;

/// <summary>Tables owned by <see cref="HousingSystem"/> (T3.8). GoodStocks is
/// the SANCTIONED SHARED STOCK (see SystemCatalog — housing is its fourth
/// holder); Housing is the dwelling stock itself.</summary>
public sealed record HousingTables(Table<HousingRow> Housing, Table<GoodStockRow> GoodStocks);

/// <summary>
/// T3.8 — THE HOUSING STOCK (director ruling: MAINTENANCE, NOT ABSTRACT
/// DECAY). Housing persists, and it consumes materials on an ongoing basis to
/// stay habitable; UNMET maintenance is what degrades it. Mathematically
/// equivalent to decay-with-upkeep; preferred because the resource draw is an
/// explicit mechanism, not a coefficient (law 2). This closes the T3.9a gate
/// finding: Shelter was satisfied from a current-period materials flow, so a
/// settlement that stopped building was instantly and completely homeless —
/// and Shelter is a Tier A gate need, so the flow artifact was the largest
/// grievance term in the world (docs/t3.8-review-record.md, measured).
///
/// THE STEP, per settlement, in table row order, all signals from PREV:
///  1. MAINTENANCE. Upkeep demand = dwellings × upkeep{timber,clay}/yr × dt.
///     The maintenance FRACTION m = min over materials of available/demand
///     (Leontief — roof and walls both needed; a settlement rich in timber
///     but out of clay keeps m of its houses whole, not a timber-weighted
///     average). Materials actually used (demand × m, deterministically
///     rounded) are SUNK via Ledger reason HousingMaterials — conservation of
///     materials exact.
///  2. DEGRADATION. The unmaintained share decays exponentially (exact
///     integration, ADR-016 family — decay is linear in the stock, so the
///     closed form is exp): dwellings ×= exp(−(1−m)·dt/τ). Whole units leave
///     via Ledger sink HousingDecayed with the fraction banked in
///     DecayRemainder (§3.3). Fully maintained housing (m = 1) persists
///     INDEFINITELY — rebuilt in place, the ruling's equivalence.
///  3. BUILD. Demand = population / personsPerDwelling; target = demand ×
///     surplusCapRatio (houses are built for households, not stockpiled —
///     the cap stops construction compounding into a dwelling mountain).
///     Buildable = min(deficit, construction-labor cap, timber cap, clay
///     cap), whole units via BuildRemainder; materials sunk (HousingMaterials)
///     and dwellings sourced (HousingBuilt) via Ledger.
///  4. PUBLISH. LastMaintenanceFraction (the H1 observable) and LastLaborUsed
///     (adult-years consumed by building) — PathBuild subtracts the latter
///     from its banked labor at the standard §3.2 one-turn lag, splitting the
///     construction pool without a system-to-system reference (law 6).
///
/// dt-CORRECTNESS (law 3): upkeep demands and the decay exponent integrate
/// with dtYears; the build labor budget is builders × dt adult-years.
/// DETERMINISM (law 5): settlements in table row order; goods found by id
/// scan; no RNG (no stream is registered for this system); no ordering over
/// doubles. STATELESS: config is immutable tuning; all persistence is in
/// HousingRow.
/// </summary>
public sealed class HousingSystem(SimConfig cfg) : ISimSystem<HousingTables>
{
    public static readonly SystemId WellKnownId = new(15);
    public const string Name = "housing";

    private readonly SimConfig _cfg = cfg;

    private readonly GoodId _timber = RequireGood(cfg.Goods, "timber");
    private readonly GoodId _clay = RequireGood(cfg.Goods, "clay");

    /// <summary>IdOf returns −1 for an unknown name — a silent −1 GoodId would
    /// make housing's material draws no-ops forever (the config-fails-quietly
    /// class), so absence refuses construction loudly.</summary>
    private static GoodId RequireGood(GoodsConfig? goods, string name)
    {
        if (goods is null)
            throw new ArgumentException(
                "HousingSystem requires SimConfig.Goods (goods.json) — dwellings are built from real goods.");
        int id = goods.IdOf(name);
        if (id < 0)
            throw new ArgumentException(
                $"goods.json has no good named '{name}' — housing builds and maintains dwellings from it.");
        return new GoodId(id);
    }

    public SystemId Id => WellKnownId;

    public void Step(SimContext<HousingTables> ctx)
    {
        IReadOnlyWorldState prev = ctx.Prev;
        HousingConfig h = _cfg.Housing;
        double dt = ctx.DtYears;
        Table<HousingRow> housing = ctx.Owned.Housing;
        Table<GoodStockRow> stocks = ctx.Owned.GoodStocks;

        for (int s = 0; s < prev.Settlements.Count; s++)
        {
            SettlementId settlement = prev.Settlements[s].Id;
            long pop = 0;
            for (int i = 0; i < prev.Buckets.Count; i++)
                if (prev.Buckets[i].Settlement == settlement) pop += prev.Buckets[i].Count.Value;

            int rowIdx = FindHousing(housing, settlement);
            if (rowIdx < 0)
            {
                if (pop <= 0) continue; // no people, no housing row to create
                rowIdx = housing.Add(new HousingRow(settlement, Conserved.Zero, 0.0, 0.0, 1.0, 0.0));
            }

            int timberIdx = FindStock(stocks, settlement, _timber);
            int clayIdx = FindStock(stocks, settlement, _clay);

            // ---- 1. maintenance -----------------------------------------
            long dwellings = housing[rowIdx].Dwellings.Value;
            double m = 1.0;
            if (dwellings > 0)
            {
                double demandT = dwellings * h.UpkeepTimberPerDwellingYear * dt;
                double demandC = dwellings * h.UpkeepClayPerDwellingYear * dt;
                double availT = timberIdx >= 0 ? stocks[timberIdx].Amount.Value : 0.0;
                double availC = clayIdx >= 0 ? stocks[clayIdx].Amount.Value : 0.0;
                double fillT = demandT > 0.0 ? Math.Min(1.0, availT / demandT) : 1.0;
                double fillC = demandC > 0.0 ? Math.Min(1.0, availC / demandC) : 1.0;
                m = Math.Min(fillT, fillC);
                // Draw what is USED (demand × m), deterministically rounded —
                // the sub-unit residue is < 1 material unit per turn and is
                // deliberately NOT banked (a materials remainder would be new
                // serialized state for dust; the maintenance FRACTION above is
                // continuous, so the mechanism loses nothing).
                SinkRounded(ctx, stocks, timberIdx, _timber, demandT * m);
                SinkRounded(ctx, stocks, clayIdx, _clay, demandC * m);

                // ---- 2. degradation of the unmaintained share -----------
                if (m < 1.0)
                {
                    ref HousingRow row = ref housing.Ref(rowIdx);
                    double lostExact = dwellings * (1.0 - Math.Exp(-(1.0 - m) * dt / h.TauYears))
                        + row.DecayRemainder;
                    long lost = ConservedMath.WholeUnits(lostExact,
                        $"housing decay (settlement {settlement.Value})");
                    row.DecayRemainder = lostExact - lost;
                    if (lost > 0)
                    {
                        ctx.Ledger.Flow(ref row.Dwellings, ConservedQuantityIds.Dwellings,
                            ReasonIds.HousingDecayed, lost, FlowDirection.Sink,
                            OverdrawPolicy.ClampToAvailable);
                    }
                }
            }

            // ---- 3. build against demand --------------------------------
            long adults = BandViews.Adults(prev.Buckets, settlement);
            SectorAllocationRow shares = Sectors.Default(settlement);
            for (int i = 0; i < prev.SectorAllocations.Count; i++)
                if (prev.SectorAllocations[i].Settlement == settlement)
                { shares = prev.SectorAllocations[i]; break; }
            double builderYears = Sectors.Share(shares, Sectors.Construction) * adults * dt;

            double laborUsed = 0.0;
            {
                ref HousingRow row = ref housing.Ref(rowIdx);
                double demand = pop / h.PersonsPerDwelling;
                double target = demand * h.SurplusCapRatio;
                double deficit = target - row.Dwellings.Value;
                if (deficit > 0.0 && pop > 0)
                {
                    double laborCap = h.BuildLaborAdultYearsPerDwelling > 0.0
                        ? builderYears / h.BuildLaborAdultYearsPerDwelling : deficit;
                    double timberCap = h.BuildTimberPerDwelling > 0.0
                        ? (timberIdx >= 0 ? stocks[timberIdx].Amount.Value : 0.0) / h.BuildTimberPerDwelling
                        : deficit;
                    double clayCap = h.BuildClayPerDwelling > 0.0
                        ? (clayIdx >= 0 ? stocks[clayIdx].Amount.Value : 0.0) / h.BuildClayPerDwelling
                        : deficit;
                    double buildExact = Math.Min(Math.Min(deficit, laborCap), Math.Min(timberCap, clayCap))
                        + row.BuildRemainder;
                    long built = ConservedMath.WholeUnits(buildExact,
                        $"housing build (settlement {settlement.Value})");
                    row.BuildRemainder = buildExact - built;
                    if (built > 0)
                    {
                        SinkRounded(ctx, stocks, timberIdx, _timber, built * h.BuildTimberPerDwelling);
                        SinkRounded(ctx, stocks, clayIdx, _clay, built * h.BuildClayPerDwelling);
                        ctx.Ledger.Flow(ref row.Dwellings, ConservedQuantityIds.Dwellings,
                            ReasonIds.HousingBuilt, built, FlowDirection.Source,
                            OverdrawPolicy.Throw);
                        // Never report more labor than the budget held (the
                        // remainder carry can push `built` one unit past what
                        // this turn's labor alone financed).
                        laborUsed = Math.Min(built * h.BuildLaborAdultYearsPerDwelling, builderYears);
                    }
                }
            }

            // ---- 4. publish the observables -----------------------------
            {
                ref HousingRow row = ref housing.Ref(rowIdx);
                row.LastMaintenanceFraction = m;
                row.LastLaborUsed = laborUsed;
            }
        }
    }

    private void SinkRounded(SimContext<HousingTables> ctx, Table<GoodStockRow> stocks,
        int idx, GoodId good, double exactAmount)
    {
        if (idx < 0 || exactAmount <= 0.0) return;
        long amount = (long)Math.Round(exactAmount);
        if (amount <= 0) return;
        ctx.Ledger.Flow(ref stocks.Ref(idx).Amount, ConservedQuantityIds.OfGood(good),
            ReasonIds.HousingMaterials, amount, FlowDirection.Sink, OverdrawPolicy.ClampToAvailable);
    }

    private static int FindHousing(Table<HousingRow> housing, SettlementId s)
    {
        for (int i = 0; i < housing.Count; i++)
            if (housing[i].Settlement == s) return i;
        return -1;
    }

    private static int FindStock(Table<GoodStockRow> stocks, SettlementId s, GoodId good)
    {
        for (int i = 0; i < stocks.Count; i++)
            if (stocks[i].Settlement == s && stocks[i].Good == good) return i;
        return -1;
    }
}
