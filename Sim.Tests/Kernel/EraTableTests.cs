using Sim.Core.Kernel;

namespace Sim.Tests.Kernel;

// T0.4 acceptance, part 1: the full-campaign tick-through — EXACTLY 1,630 turns,
// landing exactly on the 2100 CE epoch day, with per-band turn counts matching
// D-006 (250/400/300/200/120/160/200).
public class EraTableTests
{
    private static EraTable LoadCanonical()
    {
        using var stream = Sim.Data.DataFiles.OpenEraPacing();
        return EraTableLoader.Load(stream);
    }

    [Fact]
    public void CanonicalTable_LoadsWithExpectedBandGeometry()
    {
        var table = LoadCanonical();
        Assert.Equal(7, table.Bands.Length);
        Assert.Equal(0, table.CampaignStartDay);
        // 4000 BCE → 2100 CE = 6,100 years × 360 days
        Assert.Equal(6100L * 360L, table.CampaignEndDay);
        // Spot the first band: Neolithic, 2,500 years at dt 10y = 3,600 days.
        Assert.Equal("Neolithic", table.Bands[0].Name);
        Assert.Equal(3600L, table.Bands[0].DtDays);
    }

    [Fact]
    public void FullCampaignTickThrough_Exactly1630Turns_LandsOn2100Exactly()
    {
        var table = LoadCanonical();
        var clock = new SimClock(Turn: 0, SimDays: table.CampaignStartDay, DtDays: 0);

        var perBand = new long[table.Bands.Length];
        while (clock.SimDays < table.CampaignEndDay)
        {
            long startDay = clock.SimDays;
            clock = table.AdvanceTurn(clock);

            for (int i = 0; i < table.Bands.Length; i++)
            {
                if (startDay >= table.Bands[i].StartDay && startDay < table.Bands[i].EndDay)
                {
                    perBand[i]++;
                    break;
                }
            }
        }

        Assert.Equal(1630L, clock.Turn);                       // exactly, no tolerance
        Assert.Equal(table.CampaignEndDay, clock.SimDays);     // lands exactly on 2100
        Assert.Equal([250L, 400L, 300L, 200L, 120L, 160L, 200L], perBand); // D-006
    }

    [Fact]
    public void DtSelection_UsesBandContainingTurnStart_BoundariesExact()
    {
        var table = LoadCanonical();
        // Last day of Neolithic (one dt before the 1500 BCE edge) still uses 10y.
        long edge = 2500L * 360L; // 1500 BCE epoch day
        Assert.Equal(3600L, table.DtDaysAt(edge - 3600L));
        // First day of Bronze/Iron uses 5y — the boundary is unambiguous.
        Assert.Equal(1800L, table.DtDaysAt(edge));
        // Outside the campaign: loud failure.
        Assert.Throws<ArgumentOutOfRangeException>(() => table.DtDaysAt(table.CampaignEndDay));
        Assert.Throws<ArgumentOutOfRangeException>(() => table.DtDaysAt(-1));
    }

    [Fact]
    public void SimClock_DerivesDtYearsAndWorldDate()
    {
        var clock = new SimClock(Turn: 3, SimDays: 900L * 360L, DtDays: 180);
        Assert.Equal(0.5, clock.DtYears);
        Assert.Equal(900.0, clock.WorldDateYears);
    }
}
