using Sim.Ui.Headless;
using Xunit;

namespace Sim.Ui.Tests;

/// <summary>
/// BATCH 3 WS3 (W2 review F3) — THE MAP FRAME-COST TOOL'S DEFAULT FITS THE CYLINDER. The tool founds a high-count world with
/// the simulation's own founding; on the batch-3 cylinder the seed-42 canonical world sites at most 362 settlements (the
/// flat world's default of 400 aborted <c>sim-ui --frame-cost map</c>, exit 134). The default must found exactly that many
/// settlements on the shipped worldgen, and a count past the siting capacity is refused as the caller's argument error,
/// naming the capacity the siting reports — never an unhandled abort.
/// </summary>
public class CylinderFrameCostTests
{
    [Fact]
    public void TheDefaultSettlementCount_FoundsOnTheCanonicalCylinder()
    {
        UiSession s = MapFrameCost.StartHighCount(MapFrameCost.DefaultSettlements);
        Assert.True(s.World.Terrain!.WrapX);
        Assert.Equal(1024, s.World.Terrain.Size);
        Assert.Equal(MapFrameCost.DefaultSettlements, s.World.Settlements.Count);
    }

    [Fact]
    public void ACountPastTheSitingCapacity_IsAnArgumentError_NamingTheCapacity()
    {
        ArgumentException e = Assert.Throws<ArgumentException>(() => MapFrameCost.StartHighCount(2000));
        Assert.Equal("settlements", e.ParamName);
        Assert.Contains("--settlements 2000:", e.Message, StringComparison.Ordinal);
        Assert.Contains("could only place 362 of 2000", e.Message, StringComparison.Ordinal);
        Assert.IsType<InvalidOperationException>(e.InnerException);
    }
}
