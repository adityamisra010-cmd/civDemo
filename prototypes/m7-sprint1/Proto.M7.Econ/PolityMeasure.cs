using Sim.Core.Systems;
using Sim.Core.Systems.Consumption;
using Sim.Core.State;

namespace Proto.M7.Econ;

/// <summary>
/// One polity's economy at one turn boundary, READ from the state tables (nothing written). The "Last…" fields describe
/// the step that PRODUCED <c>world</c> (GoodStockRow.LastProducedUnits / LastConsumptionEatenUnits are zeroed and
/// rewritten every step; ConsumptionDeficitRow.DemandUnits is that step's civilian nutritional requirement).
/// </summary>
public sealed record PolitySnapshot(
    long Turn,
    int Polity,
    bool Player,
    int Settlements,
    long Population,
    long Adults,
    long Soldiers,
    long GrainStock,
    long FoodStock,
    long GrainProducedLastStep,
    long FoodProducedLastStep,
    long FoodEatenLastStep,
    long CivilianRequirementLastStep,
    double MaxDeficitRatio,
    double GrowthHeadroomPersonsPerYear,
    bool GrowthHeadroomUnbounded,
    double DtYears,
    long UnfedUnits)
{
    /// <summary>Food produced minus food eaten by civilians, per sim-year (before spoilage, granary cap and upkeep).</summary>
    public double FoodSurplusPerYear => DtYears > 0 ? (FoodProducedLastStep - FoodEatenLastStep) / DtYears : 0.0;

    /// <summary>Food produced per adult (15–59 in buckets) per sim-year — the average product of the labour force.</summary>
    public double FoodPerAdultYear => DtYears > 0 && Adults > 0 ? FoodProducedLastStep / DtYears / Adults : 0.0;

    /// <summary>Soldiers the annual surplus could feed at upkeepGrainPerPersonYear (1.0): surplus/yr ÷ 1.0.</summary>
    public double TroopsFromSurplus(double rationPerYear) => rationPerYear > 0 ? FoodSurplusPerYear / rationPerYear : 0.0;
}

public static class PolityMeasure
{
    /// <summary>A snapshot of every polity that controls at least one settlement, in ascending polity id.</summary>
    public static PolitySnapshot[] All(IReadOnlyWorldState world, SimConfig cfg)
    {
        var ids = new List<int>();
        for (int i = 0; i < world.Polities.Count; i++) ids.Add(world.Polities[i].Id.Value);
        ids.Sort();
        var result = new List<PolitySnapshot>();
        var baskets = new BasketBook(cfg.Needs!, cfg.Goods!);
        for (int i = 0; i < ids.Count; i++)
        {
            var p = new PolityId(ids[i]);
            if (EmpireQuery.ControlledCount(world, p) == 0) continue;
            result.Add(Of(world, cfg, baskets, p));
        }
        return [.. result];
    }

    public static PolitySnapshot Of(IReadOnlyWorldState world, SimConfig cfg, BasketBook baskets, PolityId polity)
    {
        GoodsConfig goods = cfg.Goods!;
        int grain = goods.GrainId;
        int settlements = 0;
        long population = 0, adults = 0, grainStock = 0, foodStock = 0, grainProduced = 0, foodProduced = 0, foodEaten = 0, demand = 0;
        double maxDeficit = 0.0, headroom = 0.0, dt = 0.0;
        bool unbounded = false;
        for (int s = 0; s < world.Settlements.Count; s++)
        {
            SettlementId sid = world.Settlements[s].Id;
            if (!EmpireQuery.ControlsSettlement(world, polity, sid)) continue;
            settlements++;
            for (int b = 0; b < world.Buckets.Count; b++)
            {
                BucketRow row = world.Buckets[b];
                if (row.Settlement != sid) continue;
                population = checked(population + row.Count.Value);
                if (BandViews.IsAdult(row.CohortIdx)) adults = checked(adults + row.Count.Value);
            }
            for (int g = 0; g < world.GoodStocks.Count; g++)
            {
                GoodStockRow row = world.GoodStocks[g];
                if (row.Settlement != sid) continue;
                bool isFood = goods.Contains(row.Good.Value) && goods.ById(row.Good.Value).Category == "food";
                if (row.Good.Value == grain)
                {
                    grainStock = checked(grainStock + row.Amount.Value);
                    grainProduced = checked(grainProduced + row.LastProducedUnits);
                }
                if (isFood)
                {
                    foodStock = checked(foodStock + row.Amount.Value);
                    foodProduced = checked(foodProduced + row.LastProducedUnits);
                    foodEaten = checked(foodEaten + row.LastConsumptionEatenUnits);
                }
            }
            for (int d = 0; d < world.ConsumptionDeficits.Count; d++)
            {
                if (world.ConsumptionDeficits[d].Settlement != sid) continue;
                demand = checked(demand + world.ConsumptionDeficits[d].DemandUnits);
                maxDeficit = Math.Max(maxDeficit, world.ConsumptionDeficits[d].DeficitRatio);
            }
            for (int v = 0; v < world.SettlementVitals.Count; v++)
                if (world.SettlementVitals[v].Settlement == sid) dt = Math.Max(dt, world.SettlementVitals[v].DtYears);
            double vacancy = FoodHeadroom.Vacancy(world, sid, cfg.Consumption.CohortWeights, baskets);
            if (double.IsPositiveInfinity(vacancy)) unbounded = true;
            else headroom += vacancy;
        }
        long soldiers = 0, unfed = 0;
        for (int i = 0; i < world.FormationPersonnel.Count; i++)
        {
            FormationPersonnelRow row = world.FormationPersonnel[i];
            int u = RecruitmentQuery.UnitIndex(world.MilitaryUnits, row.Unit);
            if (u < 0 || world.MilitaryUnits[u].Owner != polity) continue;
            soldiers = checked(soldiers + row.Count.Value);
            unfed = checked(unfed + row.UnfedUnits);
        }
        return new PolitySnapshot(world.Clock.Turn, polity.Value, EmpireQuery.IsPlayerCommanded(world, polity), settlements,
            population, adults, soldiers, grainStock, foodStock, grainProduced, foodProduced, foodEaten, demand, maxDeficit,
            headroom, unbounded, dt, unfed);
    }

    /// <summary>The world ledger's running total for (good or people, reason).</summary>
    public static long Ledger(IReadOnlyWorldState world, ConservedQuantityId quantity, ReasonId reason, bool sunk)
    {
        for (int i = 0; i < world.LedgerFlows.Count; i++)
            if (world.LedgerFlows[i].Quantity == quantity && world.LedgerFlows[i].Reason == reason)
                return sunk ? world.LedgerFlows[i].TotalSunk : world.LedgerFlows[i].TotalSourced;
        return 0;
    }
}
