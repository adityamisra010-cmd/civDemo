using System.Text.Json.Nodes;
using Sim.Core.Systems;
using Sim.Core.Systems.Research;

namespace Sim.Tests.Systems;

/// <summary>
/// D-063 R6 (tranche-2 contract §5) — tuning.pacingScale: BaseCost = content cost × the scale of the node's tree and content
/// Age. Absent is scale 1 (BaseCost = content cost exactly, the twin); a malformed scale is refused at load.
/// </summary>
public class ResearchPacingScaleTests
{
    private static JsonObject Shipped()
    {
        using var stream = Sim.Data.DataFiles.OpenResearch();
        return JsonNode.Parse(stream)!.AsObject();
    }

    private static ResearchContent Load(JsonObject doc)
    {
        using var goods = Sim.Data.DataFiles.OpenGoods();
        return ResearchContentLoader.Load(doc.ToJsonString(), GoodsConfigLoader.Load(goods));
    }

    private static JsonArray Scales(params double[] v) => new([.. v.Select(x => (JsonNode)x)]);

    [Fact]
    public void Absent_BaseCostIsTheContentCostExactly()
    {
        JsonObject doc = Shipped();
        doc["tuning"]!.AsObject().Remove("pacingScale");
        ResearchContent c = Load(doc);
        Assert.Null(c.Tuning.Pacing);
        foreach (ResearchNode n in c.Nodes) Assert.Equal(n.CostRationale.ContentCost, n.BaseCost);
    }

    [Fact]
    public void Present_EachNodeTakesItsTreeAndAgeScale_DistinctPerCell()
    {
        // Distinct scale per (tree, Age) cell, so a lookup that swaps trees or is off by one Age cannot pass.
        double[] tech = [0.11, 0.12, 0.13, 0.14, 0.15, 0.16, 0.17, 0.18, 0.19];
        double[] civ = [0.21, 0.22, 0.23, 0.24, 0.25, 0.26, 0.27, 0.28, 0.29];
        JsonObject doc = Shipped();
        doc["tuning"]!["pacingScale"] = new JsonObject { ["technology"] = Scales(tech), ["civics"] = Scales(civ) };
        ResearchContent c = Load(doc);
        var seen = new bool[2, 9];
        foreach (ResearchNode n in c.Nodes)
        {
            int a = n.AgeNumber - 1, t = n.Tree == ResearchTree.Technology ? 0 : 1;
            Assert.Equal(n.CostRationale.ContentCost * (t == 0 ? tech : civ)[a], n.BaseCost);
            seen[t, a] = true;
        }
        Assert.True(seen[0, 0] && seen[1, 0], "both trees have Age 1 nodes");
    }

    [Theory]
    [InlineData(8, 0.5)]
    [InlineData(10, 0.5)]
    [InlineData(9, 0.0)]
    [InlineData(9, -1.0)]
    [InlineData(9, 4.5)]
    public void Malformed_IsRefused(int length, double value)
    {
        JsonObject doc = Shipped();
        doc["tuning"]!["pacingScale"] = new JsonObject
        {
            ["technology"] = Scales([.. Enumerable.Repeat(value, length)]),
            ["civics"] = Scales([.. Enumerable.Repeat(0.5, 9)]),
        };
        Assert.Throws<ResearchContentException>(() => Load(doc));
    }
}
