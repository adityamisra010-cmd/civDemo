using System.Globalization;
using Sim.Core.State;
using Sim.Core.Systems;

namespace Sim.Ui.ViewModel;

/// <summary>
/// The HUD's numbers, computed from a WorldState read (T1.8, pure view-model —
/// the formatting tests run against a founded state headless; T2.4: computed
/// for the SELECTED settlement, plus a world summary line). All formatting is
/// culture-invariant. LastHarvest is the selected settlement's
/// grain's GoodStockRow.LastProducedUnits (T3.2) — the per-settlement observable Farming has
/// written since T2.2, which retires the T1.8 UI-side global-ledger delta
/// (that hack predated a per-settlement harvest signal existing).
/// </summary>
/// <summary>Grain's D-031 registry id (goods.json; stable). The UI reads the
/// canonical id directly — revisit if the roster ever renumbers.</summary>
public static class UiGoods
{
    public static readonly Sim.Core.State.GoodId Grain = new(1);
}

public sealed record HudModel(
    int SettlementId, long Turn, long Year,
    long Children, long Adults, long Elders, long TotalPopulation,
    long FoodStore, long LastHarvest,
    // T3.3 (D-032): all five NORMALIZED sector shares, as percentages. The
    // pre-T3.3 pair was FarmSharePct + PathSharePct fabricated as (1 − farm),
    // which stopped being true the moment a third sector could be allocated:
    // the HUD reported "50% path" while PathBuild banked exactly zero. These
    // come from Sectors.Share — the same normalization the sim reads — so the
    // panel cannot disagree with the simulation about who is working where.
    double FarmingPct, double HerdingPct, double ExtractionPct,
    double CraftingPct, double ConstructionPct,
    long WorldPopulation, int SettlementCount,
    IReadOnlyList<string>? NeedLines = null, double GrievanceValue = 0.0,
    string? SettlementName = null)
{
    /// <summary>Builds the HUD for one selected settlement. An id not present
    /// in the world (or an empty world) yields the zeros the panel can render
    /// harmlessly — selection itself is pure UI state and never crashes the sim.
    /// T2.6: pass the D-018 needs registry to get the needs block — bound
    /// needs show their satisfaction, unbound render "not yet simulated"; the
    /// grievance value reads the settlement's first class row (values are
    /// identical across classes at M2 — settlement-wide inputs).</summary>
    public static HudModel From(
        IReadOnlyWorldState world, int selectedSettlementId, NeedsConfig? needs = null,
        string? settlementName = null)
    {
        var selected = new SettlementId(selectedSettlementId);
        bool exists = false;
        for (int i = 0; i < world.Settlements.Count; i++)
            if (world.Settlements[i].Id == selected) { exists = true; break; }

        long children = 0, adults = 0, elders = 0, food = 0, lastHarvest = 0;
        // Never-ordered default: all farming (Sectors.Default), so the HUD
        // shows what the sim would actually do with no allocation row.
        SectorAllocationRow allocation = Sectors.Default(new SettlementId(selectedSettlementId));
        if (exists)
        {
            children = BandViews.Children(world.Buckets, selected);
            adults = BandViews.Adults(world.Buckets, selected);
            elders = BandViews.Elders(world.Buckets, selected);
            for (int i = 0; i < world.GoodStocks.Count; i++)
            {
                if (world.GoodStocks[i].Settlement == selected && world.GoodStocks[i].Good == UiGoods.Grain)
                {
                    food = world.GoodStocks[i].Amount.Value;
                    lastHarvest = world.GoodStocks[i].LastProducedUnits;
                    break;
                }
            }
            for (int i = 0; i < world.SectorAllocations.Count; i++)
            {
                if (world.SectorAllocations[i].Settlement == selected)
                {
                    allocation = world.SectorAllocations[i];
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
                // T2.13: an extinct settlement's satisfaction is per-capita-
                // meaningless — "—", not a number (the system publishes no
                // rows for it either; this branch names the reason).
                if (exists && children + adults + elders == 0)
                {
                    needLines.Add(string.Create(CultureInfo.InvariantCulture, $"{need.Name}: —"));
                    continue;
                }
                // Absent row ≠ zero satisfaction: satisfaction rows are
                // rebuilt each turn by NeedsGrievance, so before the first
                // turn resolves NOTHING is published (Prev-lag, §3.2) — a
                // fabricated "0.00" would read as total deprivation on a
                // fully-stocked founding day (director gate finding, T2.9).
                double? value = null;
                for (int i = 0; i < world.NeedSatisfactions.Count; i++)
                {
                    NeedSatisfactionRow row = world.NeedSatisfactions[i];
                    if (row.Settlement == selected && row.NeedId == need.Id)
                    { value = row.Value; break; }
                }
                needLines.Add(value is { } v
                    ? string.Create(CultureInfo.InvariantCulture, $"{need.Name}: {v:F2}")
                    : string.Create(CultureInfo.InvariantCulture, $"{need.Name}: not yet measured"));
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
            FarmingPct: Sectors.Share(allocation, Sectors.Farming) * 100.0,
            HerdingPct: Sectors.Share(allocation, Sectors.Herding) * 100.0,
            ExtractionPct: Sectors.Share(allocation, Sectors.Extraction) * 100.0,
            CraftingPct: Sectors.Share(allocation, Sectors.Crafting) * 100.0,
            ConstructionPct: Sectors.Share(allocation, Sectors.Construction) * 100.0,
            WorldPopulation: worldPop,
            SettlementCount: world.Settlements.Count,
            NeedLines: needLines,
            GrievanceValue: grievance,
            SettlementName: settlementName);
    }

    /// <summary>T2.9: the chronicle name leads; the id stays for cross-
    /// referencing orders/logs. Name defaults keep pre-T2.9 tests valid.</summary>
    public string TitleLine => SettlementName is null
        ? string.Create(CultureInfo.InvariantCulture, $"Settlement {SettlementId}")
        : string.Create(CultureInfo.InvariantCulture, $"{SettlementName}  (settlement {SettlementId})");

    public string PopulationLine =>
        string.Create(CultureInfo.InvariantCulture,
            $"pop {TotalPopulation}  (child {Children} / adult {Adults} / elder {Elders})");

    public string FoodLine =>
        string.Create(CultureInfo.InvariantCulture,
            $"food {FoodStore}  (last harvest +{LastHarvest})");

    public string SplitLine =>
        string.Create(CultureInfo.InvariantCulture,
            $"labor {FarmingPct:F0}% farm / {HerdingPct:F0}% herd / {ExtractionPct:F0}% mine"
            + $" / {CraftingPct:F0}% craft / {ConstructionPct:F0}% build");

    public string WorldLine =>
        string.Create(CultureInfo.InvariantCulture,
            $"world pop {WorldPopulation}  ({SettlementCount} settlements)");

    public string ClockLine =>
        string.Create(CultureInfo.InvariantCulture, $"turn {Turn}   year {Year}");

    /// <summary>T2.6: the selected settlement's grievance stock — display only
    /// (the read-isolation doctrine: grievance drives no behavior until M5).
    /// T2.13 (director finding, ghost grievance): per-capita-meaningless at
    /// population zero — an extinct settlement reads "—", never a number
    /// (the sim-side stock is also zeroed on extinction; this branch keeps
    /// the panel honest even against a stale pre-extinction row).</summary>
    public string GrievanceLine => TotalPopulation == 0
        ? "grievance —"
        : string.Create(CultureInfo.InvariantCulture, $"grievance {GrievanceValue:F2}");

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
