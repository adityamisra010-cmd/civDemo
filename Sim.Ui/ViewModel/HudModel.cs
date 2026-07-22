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
        // T2.3 director ruling: the interim HUD shows SETTLEMENT 0 only —
        // selection and per-settlement rule arrive at T2.4. Band views over
        // the cohort buckets (T2.1), first settlement's food store.
        long children = 0, adults = 0, elders = 0, food = 0;
        if (world.Settlements.Count > 0)
        {
            SettlementId first = world.Settlements[0].Id;
            children = BandViews.Children(world.Buckets, first);
            adults = BandViews.Adults(world.Buckets, first);
            elders = BandViews.Elders(world.Buckets, first);
            for (int i = 0; i < world.FoodStores.Count; i++)
            {
                if (world.FoodStores[i].Settlement == first) { food = world.FoodStores[i].Store.Value; break; }
            }
        }

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

    /// <summary>Status/camera lines are formatted HERE too (T1.8 re-gate: every
    /// string handed to ImGui is view-model-owned and tested — the HUD renders
    /// them via TextUnformatted, never through printf-style Text parsing, after
    /// the SplitLine's '%' characters were mangled into garbage).</summary>
    public static string StatusLine(ulong seed, double fps) =>
        string.Create(CultureInfo.InvariantCulture, $"seed {seed}   fps {fps:F0}");

    public static string CameraLine(double centerX, double centerY, double zoom) =>
        string.Create(CultureInfo.InvariantCulture,
            $"camera ({centerX:F0}, {centerY:F0}) zoom {zoom:F2}x");
}
