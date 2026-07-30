using System.Globalization;
using Sim.Core.State;

namespace Sim.Ui.ViewModel;

/// <summary>
/// T3.9a item 4: the five-sector labor split as five labelled rows (bar
/// fraction + label), replacing the single squeezed text line. DISPLAY ONLY —
/// the farm-% slider and its order path are untouched (controls are T3.9b).
/// Fractions come from Sectors.Share — the same normalization the sim reads —
/// so the panel cannot disagree with the simulation about who works where.
/// PURE view-model: doubles and strings, no MonoGame/ImGui types.
/// </summary>
public sealed record SectorBarRow(string Name, double Fraction, string Label);

public static class SectorBarModel
{
    /// <summary>Fixed D-032 sector order, ids aligned with Sectors.*.</summary>
    public static readonly string[] SectorNames =
        ["farming", "herding", "extraction", "crafting", "construction"];

    public static IReadOnlyList<SectorBarRow> Rows(in SectorAllocationRow allocation)
    {
        var rows = new List<SectorBarRow>(Sectors.Count);
        for (int s = 0; s < Sectors.Count; s++)
        {
            double share = Sectors.Share(allocation, s);
            rows.Add(new SectorBarRow(SectorNames[s], share,
                string.Create(CultureInfo.InvariantCulture, $"{SectorNames[s]} {share * 100.0:F0}%")));
        }
        return rows;
    }
}
