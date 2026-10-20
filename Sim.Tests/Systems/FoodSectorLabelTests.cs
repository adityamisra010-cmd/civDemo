using Sim.Core.State;
using Sim.Core.Systems.Research;
using Sim.Tests.TestUtil;

namespace Sim.Tests.Systems;

/// <summary>R3 (Director R2-final §15): the city-state food-sector label is derived from actual capability.</summary>
public class FoodSectorLabelTests
{
    private static bool[] Knowing(ResearchContent c, params string[] ids)
    {
        var mask = new bool[c.Nodes.Count];
        foreach (string id in ids) mask[c.IndexOfId(id)] = true;
        return mask;
    }

    [Fact]
    public void NoCultivation_ReadsAsTheBaselineIdentity()
    {
        ResearchContent c = TestConfigs.Research();
        Assert.Equal(c.SectorActivities[Sectors.Farming].Baseline.Name,
            LabourActivities.CapabilityLabel(c, Knowing(c), Sectors.Farming));
    }

    [Fact]
    public void RootCropOnly_ReadsAsRootCropCultivation_NotFarming()
    {
        ResearchContent c = TestConfigs.Research();
        string label = LabourActivities.CapabilityLabel(c, Knowing(c, "root_crop"), Sectors.Farming);
        Assert.Equal(c.Nodes[c.IndexOfId("root_crop")].Name, label);
        Assert.DoesNotContain("Farming", label);
    }

    [Fact]
    public void CerealCultivation_ReadsAsCerealCultivation()
    {
        ResearchContent c = TestConfigs.Research();
        Assert.Equal(c.Nodes[c.IndexOfId("cereal_cultivation")].Name,
            LabourActivities.CapabilityLabel(c, Knowing(c, "cereal_cultivation"), Sectors.Farming));
    }
}
