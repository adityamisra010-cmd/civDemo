using Sim.Core.Kernel;
using Sim.Core.State;

namespace Sim.Core.Systems.Farming;

/// <summary>
/// Writable handles to FarmingSystem's tables (built by SystemCatalog only).
/// FoodStores is the SHARED conserved stock: Farming credits it (Harvest) and
/// owns HarvestRemainder; Consumption debits it (Eaten) — see SystemCatalog.
/// </summary>
public readonly record struct FarmingTables(Table<FoodStoreRow> FoodStores);

/// <summary>
/// Farming (T1.5; production function amended at T1.8, director-sanctioned —
/// the T1.5 form had no labor factor and a depopulated settlement harvested
/// indefinitely, exposed by the first played session). LEONTIEF production:
///   harvest/yr = min(farmland × YieldPerFarmlandPerYear,
///                    adults × farmShare × OutputPerFarmerPerYear)
/// — the binding constraint is the scarcer of workable land (PREV catchment
/// summary, T1.4) and assigned farm labor (PREV adults × PREV allocation).
/// Zero adults → harvest EXACTLY zero (min against 0). Integrated × dtYears
/// (law 3), entering the store exclusively via Ledger.Flow with reason Harvest
/// (law 1); fractional yield converts to whole units through the D-004
/// remainder accumulator in the FoodStoreRow.
///
/// ONE-TURN LAG (§3.2, documented): farmland comes from Prev, so turn 1 (before
/// the first catchment recompute has landed in Prev) harvests nothing — the
/// founding food store covers the warm-up. Likewise a catchment grown by a new
/// path edge raises yield one turn after the catchment recomputes.
///
/// Labor share (T1.6): the settlement's PREV LaborAllocations row — set by the
/// LaborAllocationOrder via PathBuildSystem, so an order lands one turn before
/// yields shift (same §3.2 lag). No row = the never-ordered default of 1.0.
/// STATELESS: config is immutable tuning, not state.
/// </summary>
public sealed class FarmingSystem(SimConfig cfg) : ISimSystem<FarmingTables>
{
    public static readonly SystemId WellKnownId = new(5);
    public const string Name = "farming";

    private readonly SimConfig _cfg = cfg;

    public SystemId Id => WellKnownId;

    public void Step(SimContext<FarmingTables> ctx)
    {
        IReadOnlyWorldState prev = ctx.Prev;
        Table<FoodStoreRow> stores = ctx.Owned.FoodStores;

        // Ascending settlement-row order — the fixed iteration order (law 5).
        for (int s = 0; s < prev.Settlements.Count; s++)
        {
            SettlementId settlement = prev.Settlements[s].Id;
            int storeIndex = FindStore(stores, settlement);
            if (storeIndex < 0) continue; // no store row → founding never endowed one; nothing to credit

            // Effective farmland from the PREV catchment summary (one-turn lag).
            double farmland = 0.0;
            for (int i = 0; i < prev.CatchmentSummaries.Count; i++)
            {
                if (prev.CatchmentSummaries[i].Settlement == settlement)
                {
                    farmland = prev.CatchmentSummaries[i].EffectiveFarmland;
                    break;
                }
            }

            double farmShare = 1.0; // never-ordered default: all labor farms
            for (int i = 0; i < prev.LaborAllocations.Count; i++)
            {
                if (prev.LaborAllocations[i].Settlement == settlement)
                {
                    farmShare = prev.LaborAllocations[i].FarmShare;
                    break;
                }
            }

            // T2.2: farm labor is the PEASANT adults (base class — artisans are
            // specialists and do not farm); the artisan contribution enters as
            // the tool multiplier below. SCAFFOLDING (spec §1): the multiplier
            // is an abstraction of artisan output that M3's real goods economy
            // replaces — 1 + min(ToolYieldBonusCap, ToolYieldBonusSlope ×
            // artisanShare): monotone in the share, saturating, never above
            // 1 + cap.
            var baseClass = new ClassId(_cfg.Registries.Classes[0].Id);
            long peasantAdults = Sim.Core.Systems.ClassMobility.ClassMobilitySystem
                .AdultsOfClass(prev.Buckets, settlement, baseClass);
            long allAdults = BandViews.Adults(prev.Buckets, settlement);
            double artisanShare = allAdults > 0 ? (allAdults - peasantAdults) / (double)allAdults : 0.0;
            double toolMultiplier = 1.0 + Math.Min(
                _cfg.Mobility.ToolYieldBonusCap, _cfg.Mobility.ToolYieldBonusSlope * artisanShare);

            // Leontief: the scarcer of land capacity and assigned farm labor binds.
            double landSide = farmland * _cfg.Farming.YieldPerFarmlandPerYear;
            double laborSide = peasantAdults * farmShare
                               * _cfg.Farming.OutputPerFarmerPerYear * toolMultiplier;
            double ratePerYear = Math.Min(landSide, laborSide);

            ref FoodStoreRow row = ref stores.Ref(storeIndex);
            double exact = ratePerYear * ctx.DtYears + row.HarvestRemainder;
            long harvested = (long)Math.Floor(exact);
            ctx.Ledger.Flow(
                ref row.Store, ConservedQuantityIds.Food, ReasonIds.Harvest,
                harvested, FlowDirection.Source, OverdrawPolicy.Throw);
            row.HarvestRemainder = exact - harvested;
            row.LastHarvestUnits = harvested; // T2.2: numerator of food_surplus_ratio
        }
    }

    private static int FindStore(Table<FoodStoreRow> stores, SettlementId settlement)
    {
        for (int i = 0; i < stores.Count; i++)
            if (stores[i].Settlement == settlement) return i;
        return -1;
    }
}
