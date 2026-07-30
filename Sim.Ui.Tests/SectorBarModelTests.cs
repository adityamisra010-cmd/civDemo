using Sim.Core.State;
using Sim.Ui.ViewModel;
using Xunit;

namespace Sim.Ui.Tests;

// T3.9a item 4 acceptance: the five-sector split as labelled rows, DISPLAY
// ONLY, fractions from the SAME normalization the sim reads (Sectors.Share).
public class SectorBarModelTests
{
    [Fact]
    public void Rows_FiveSectors_FractionsAreTheSimsNormalizedShares()
    {
        var allocation = new SectorAllocationRow(new SettlementId(0),
            Farming: 0.55, Herding: 0.15, Extraction: 0.10, Crafting: 0.12, Construction: 0.08);
        IReadOnlyList<SectorBarRow> rows = SectorBarModel.Rows(allocation);
        Assert.Equal(Sectors.Count, rows.Count);
        for (int s = 0; s < Sectors.Count; s++)
        {
            Assert.Equal(SectorBarModel.SectorNames[s], rows[s].Name);
            Assert.Equal(Sectors.Share(allocation, s), rows[s].Fraction);
        }
        Assert.Equal(1.0, rows.Sum(r => r.Fraction), 12);
        Assert.Equal("farming 55%", rows[0].Label);
        Assert.Equal("construction 8%", rows[4].Label);
    }

    [Fact]
    public void Rows_UnnormalizedWeights_DisplayNormalized()
    {
        // Raw weights need not sum to 1 (orders write raw weights); the panel
        // shows normalized shares exactly as the sim consumes them.
        var allocation = new SectorAllocationRow(new SettlementId(0),
            Farming: 2.0, Herding: 1.0, Extraction: 1.0, Crafting: 0.0, Construction: 0.0);
        IReadOnlyList<SectorBarRow> rows = SectorBarModel.Rows(allocation);
        Assert.Equal(0.5, rows[0].Fraction);
        Assert.Equal(0.25, rows[1].Fraction);
        Assert.Equal("farming 50%", rows[0].Label);
        Assert.Equal("crafting 0%", rows[3].Label);
    }

    [Fact]
    public void Rows_AllZeroRow_YieldsZeroEverywhere_NeverNaN()
    {
        // Sectors.Share's documented conservative reading of a defensively
        // possible all-zero row: 0 for every sector. The panel inherits it.
        var allocation = new SectorAllocationRow(new SettlementId(0), 0, 0, 0, 0, 0);
        IReadOnlyList<SectorBarRow> rows = SectorBarModel.Rows(allocation);
        Assert.All(rows, r => Assert.Equal(0.0, r.Fraction));
        Assert.All(rows, r => Assert.EndsWith(" 0%", r.Label));
    }
}
