using System.Globalization;
using Sim.Core.State;
using Sim.Core.Systems;

namespace Sim.Ui.ViewModel;

/// <summary>
/// The HUD's numbers, computed from a WorldState read (T1.8, pure view-model —
/// the formatting tests run against a founded state headless; T2.4: computed
/// for the SELECTED settlement, plus a world summary line). All formatting is
/// culture-invariant. LastHarvest is the selected settlement's
/// FoodStoreRow.LastHarvestUnits — the per-settlement observable Farming has
/// written since T2.2, which retires the T1.8 UI-side global-ledger delta
/// (that hack predated a per-settlement harvest signal existing).
/// </summary>
public sealed record HudModel(
    int SettlementId, long Turn, long Year,
    long Children, long Adults, long Elders, long TotalPopulation,
    long FoodStore, long LastHarvest,
    double FarmSharePct, double PathSharePct,
    long WorldPopulation, int SettlementCount,
    IReadOnlyList<string>? NeedLines = null, double GrievanceValue = 0.0)
{
    /// <summary>Builds the HUD for one selected settlement. An id not present
    /// in the world (or an empty world) yields the zeros the panel can render
    /// harmlessly — selection itself is pure UI state and never crashes the sim.
    /// T2.6: pass the D-018 needs registry to get the needs block — bound
    /// needs show their satisfaction, unbound render "not yet simulated"; the
    /// grievance value reads the settlement's first class row (values are
    /// identical across classes at M2 — settlement-wide inputs).</summary>
    public static HudModel From(
        IReadOnlyWorldState world, int selectedSettlementId, NeedsConfig? needs = null)
    {
        var selected = new SettlementId(selectedSettlementId);
        bool exists = false;
        for (int i = 0; i < world.Settlements.Count; i++)
            if (world.Settlements[i].Id == selected) { exists = true; break; }

        long children = 0, adults = 0, elders = 0, food = 0, lastHarvest = 0;
        double farmShare = 1.0; // never-ordered default
        if (exists)
        {
            children = BandViews.Children(world.Buckets, selected);
            adults = BandViews.Adults(world.Buckets, selected);
            elders = BandViews.Elders(world.Buckets, selected);
            for (int i = 0; i < world.FoodStores.Count; i++)
            {
                if (world.FoodStores[i].Settlement == selected)
                {
                    food = world.FoodStores[i].Store.Value;
                    lastHarvest = world.FoodStores[i].LastHarvestUnits;
                    break;
                }
            }
            for (int i = 0; i < world.LaborAllocations.Count; i++)
            {
                if (world.LaborAllocations[i].Settlement == selected)
                {
                    farmShare = world.LaborAllocations[i].FarmShare;
                    break;
                }
            }
        }

        long worldPop = 0;
        for (int i = 0; i < world.Buckets.Count; i++) worldPop += world.Buckets[i].Count.Value;

        // T2.6 needs block: registry order; bound needs read the settlement's
        // satisfaction row (any class — equal at M2), unbound label honestly.
        var needLines = new List<string>();
        if (needs is not null && exists)
        {
            foreach (NeedEntry need in needs.Needs)
            {
                if (!need.Bound)
                {
                    needLines.Add(string.Create(CultureInfo.InvariantCulture,
                        $"{need.Name}: not yet simulated"));
                    continue;
                }
                double value = 0.0;
                for (int i = 0; i < world.NeedSatisfactions.Count; i++)
                {
                    NeedSatisfactionRow row = world.NeedSatisfactions[i];
                    if (row.Settlement == selected && row.NeedId == need.Id)
                    { value = row.Value; break; }
                }
                needLines.Add(string.Create(CultureInfo.InvariantCulture,
                    $"{need.Name}: {value:F2}"));
            }
        }
        double grievance = 0.0;
        if (exists)
        {
            for (int i = 0; i < world.Grievances.Count; i++)
            {
                if (world.Grievances[i].Settlement == selected)
                { grievance = world.Grievances[i].Value; break; }
            }
        }

        return new HudModel(
            SettlementId: selectedSettlementId,
            Turn: world.Clock.Turn,
            Year: -4000 + world.Clock.SimDays / 360, // presentation-only calendar (ADR-002)
            Children: children, Adults: adults, Elders: elders,
            TotalPopulation: children + adults + elders,
            FoodStore: food,
            LastHarvest: lastHarvest,
            FarmSharePct: farmShare * 100.0,
            PathSharePct: (1.0 - farmShare) * 100.0,
            WorldPopulation: worldPop,
            SettlementCount: world.Settlements.Count,
            NeedLines: needLines,
            GrievanceValue: grievance);
    }

    /// <summary>"Settlement N" until T2.9 names them (m2 spec, chronicle packet).</summary>
    public string TitleLine =>
        string.Create(CultureInfo.InvariantCulture, $"Settlement {SettlementId}");

    public string PopulationLine =>
        string.Create(CultureInfo.InvariantCulture,
            $"pop {TotalPopulation}  (child {Children} / adult {Adults} / elder {Elders})");

    public string FoodLine =>
        string.Create(CultureInfo.InvariantCulture,
            $"food {FoodStore}  (last harvest +{LastHarvest})");

    public string SplitLine =>
        string.Create(CultureInfo.InvariantCulture,
            $"labor {FarmSharePct:F0}% farm / {PathSharePct:F0}% path");

    public string WorldLine =>
        string.Create(CultureInfo.InvariantCulture,
            $"world pop {WorldPopulation}  ({SettlementCount} settlements)");

    public string ClockLine =>
        string.Create(CultureInfo.InvariantCulture, $"turn {Turn}   year {Year}");

    /// <summary>T2.6: the selected settlement's grievance stock — display only
    /// (the read-isolation doctrine: grievance drives no behavior until M5).</summary>
    public string GrievanceLine =>
        string.Create(CultureInfo.InvariantCulture, $"grievance {GrievanceValue:F2}");

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
