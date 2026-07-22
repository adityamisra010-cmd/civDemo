using System.Globalization;
using Sim.Core.State;

namespace Sim.Ui.ViewModel;

/// <summary>
/// The HUD's numbers, computed from a WorldState read (T1.8, pure view-model —
/// the formatting tests run against a founded state headless). All formatting
/// is culture-invariant. LastHarvest is a UI-side delta: the cumulative Harvest
/// flow total now minus the total when the previous turn ended (the caller
/// carries the previous total across End Turn).
/// </summary>
public sealed record HudModel(
    long Turn, long Year,
    long Children, long Adults, long Elders, long TotalPopulation,
    long FoodStore, long LastHarvest,
    double FarmSharePct, double PathSharePct,
    long HarvestTotal)
{
    public static HudModel From(IReadOnlyWorldState world, long previousHarvestTotal)
    {
        long children = 0, adults = 0, elders = 0;
        for (int i = 0; i < world.PopBands.Count; i++)
        {
            PopBandRow row = world.PopBands[i];
            if (row.Band == PopBands.Children) children += row.Count.Value;
            else if (row.Band == PopBands.Adults) adults += row.Count.Value;
            else elders += row.Count.Value;
        }

        long food = 0;
        for (int i = 0; i < world.FoodStores.Count; i++) food += world.FoodStores[i].Store.Value;

        long harvestTotal = 0;
        for (int i = 0; i < world.LedgerFlows.Count; i++)
        {
            LedgerFlowRow row = world.LedgerFlows[i];
            if (row.Quantity == ConservedQuantityIds.Food && row.Reason == ReasonIds.Harvest)
                harvestTotal += row.TotalSourced;
        }

        // Farm share: the first settlement's allocation row; never-ordered = 1.0.
        double farmShare = 1.0;
        if (world.Settlements.Count > 0)
        {
            SettlementId first = world.Settlements[0].Id;
            for (int i = 0; i < world.LaborAllocations.Count; i++)
            {
                if (world.LaborAllocations[i].Settlement == first)
                {
                    farmShare = world.LaborAllocations[i].FarmShare;
                    break;
                }
            }
        }

        return new HudModel(
            Turn: world.Clock.Turn,
            Year: -4000 + world.Clock.SimDays / 360, // presentation-only calendar (ADR-002)
            Children: children, Adults: adults, Elders: elders,
            TotalPopulation: children + adults + elders,
            FoodStore: food,
            LastHarvest: harvestTotal - previousHarvestTotal,
            FarmSharePct: farmShare * 100.0,
            PathSharePct: (1.0 - farmShare) * 100.0,
            HarvestTotal: harvestTotal);
    }

    public string PopulationLine =>
        string.Create(CultureInfo.InvariantCulture,
            $"pop {TotalPopulation}  (child {Children} / adult {Adults} / elder {Elders})");

    public string FoodLine =>
        string.Create(CultureInfo.InvariantCulture,
            $"food {FoodStore}  (last harvest +{LastHarvest})");

    public string SplitLine =>
        string.Create(CultureInfo.InvariantCulture,
            $"labor {FarmSharePct:F0}% farm / {PathSharePct:F0}% path");

    public string ClockLine =>
        string.Create(CultureInfo.InvariantCulture, $"turn {Turn}   year {Year}");
}
