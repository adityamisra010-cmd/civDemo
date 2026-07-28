using Sim.Core.Kernel;
using Sim.Core.State;

namespace Sim.Core.Systems.Consumption;

/// <summary>
/// Writable handles to ConsumptionSystem's tables (built by SystemCatalog only).
/// FoodStores is the SHARED conserved stock: Consumption debits it (Eaten) and
/// owns EatenRemainder; Farming credits it (Harvest) — see SystemCatalog.
/// </summary>
public readonly record struct ConsumptionTables(
    Table<GoodStockRow> GoodStocks, Table<ConsumptionDeficitRow> Deficits);

/// <summary>
/// Consumption (T1.5, cohortized at T2.1): each settlement's cohort-weighted
/// demand — Σ cohortWeights[c] × count_c × dtYears over every bucket, counts
/// from Prev (law 3, §3.2) — leaves the food store via
/// Ledger.Flow with reason Eaten under ClampToAvailable: a settlement can only
/// eat what the store holds, so the store bottoms out at EXACTLY zero, never
/// negative. The unmet fraction is recorded as the turn's DeficitRatio in [0,1];
/// DemographicsSystem reads it from Prev NEXT turn (one-turn lag, documented).
///
/// Running AFTER Farming in the pipeline is deliberate: the clamp applies to the
/// post-harvest store — this turn's harvest is eaten this turn. Reading its own
/// shared Next stock is lawful (owned-table access); everything else reads Prev.
///
/// REMAINDER SEMANTICS (documented): EatenRemainder carries only the sub-unit
/// fraction of demand. A clamp shortfall is NOT carried forward as remainder —
/// hunger is recorded in the deficit ratio, not banked as future double-eating.
/// STATELESS: config is immutable tuning, not state.
/// </summary>
public sealed class ConsumptionSystem(SimConfig cfg) : ISimSystem<ConsumptionTables>
{
    public static readonly SystemId WellKnownId = new(6);
    public const string Name = "consumption";

    private readonly SimConfig _cfg = cfg;

    /// <summary>T3.2: demand eats from the GRAIN stock (the migrated M2
    /// FoodStore; the D-035 food basket arrives at T3.5).</summary>
    private readonly GoodId _grain = new(cfg.Goods?.GrainId
        ?? throw new ArgumentException("ConsumptionSystem requires SimConfig.Goods (goods.json) — consumption eats a grain stock at M3."));

    public SystemId Id => WellKnownId;

    public void Step(SimContext<ConsumptionTables> ctx)
    {
        IReadOnlyWorldState prev = ctx.Prev;
        Table<GoodStockRow> stores = ctx.Owned.GoodStocks;
        Table<ConsumptionDeficitRow> deficits = ctx.Owned.Deficits;

        // Ascending settlement-row order — the fixed iteration order (law 5).
        for (int s = 0; s < prev.Settlements.Count; s++)
        {
            SettlementId settlement = prev.Settlements[s].Id;

            // Cohort-weighted demand from PREV counts (per-year rate × dtYears).
            double demandPerYear = 0.0;
            for (int i = 0; i < prev.Buckets.Count; i++)
            {
                BucketRow bucket = prev.Buckets[i];
                if (bucket.Settlement != settlement) continue;
                demandPerYear += _cfg.Consumption.CohortWeights[bucket.CohortIdx] * bucket.Count.Value;
            }

            // Zero every good's consumption-demand signal before writing this
            // turn's (T3.3 staleness precedent): at M3 only grain is eaten, so
            // without this every other good would carry a permanent 0 that is
            // correct only by accident, and the accident ends at T3.5.
            for (int i = 0; i < stores.Count; i++)
                if (stores[i].Settlement == settlement)
                    stores.Ref(i).LastConsumptionDemandUnits = 0;

            int storeIndex = GoodStockIndex.IndexOf(stores, settlement, _grain);
            long demanded = 0, eaten = 0;
            if (storeIndex >= 0)
            {
                ref GoodStockRow row = ref stores.Ref(storeIndex);
                double exact = demandPerYear * ctx.DtYears + row.ConsumeRemainder;
                demanded = ConservedMath.WholeUnits(exact, $"consumption demand (settlement {settlement.Value})");
                eaten = ctx.Ledger.Flow(
                    ref row.Amount, ConservedQuantityIds.OfGood(_grain), ReasonIds.Eaten,
                    demanded, FlowDirection.Sink, OverdrawPolicy.ClampToAvailable);
                row.ConsumeRemainder = exact - demanded; // sub-unit fraction only (see header)
                // T3.4 (D-033): PRE-CLAMP demand is the price signal. What a
                // starving settlement could not buy is exactly what should move
                // the price, so this is `demanded`, never `eaten`.
                row.LastConsumptionDemandUnits = demanded;
            }

            // Deficit ratio for THIS turn (guarded: no demand → no deficit → no NaN).
            // DemandUnits (T2.2): the pre-clamp integer demand — the
            // denominator of the published food_surplus_ratio.
            double ratio = demanded > 0 ? (demanded - eaten) / (double)demanded : 0.0;
            var deficitRow = new ConsumptionDeficitRow(settlement, ratio, demanded);
            if (s < deficits.Count) deficits[s] = deficitRow;
            else deficits.Add(deficitRow);
        }
    }

}
