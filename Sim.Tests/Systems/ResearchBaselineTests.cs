using Sim.Core;
using Sim.Core.Kernel;
using Sim.Core.State;
using Sim.Core.Systems.Research;
using Sim.Core.Worldgen;
using Sim.Tests.TestUtil;
using static Sim.Tests.TestUtil.ResearchRigs;

namespace Sim.Tests.Systems;

/// <summary>
/// Director ruling 2026-10-01 §1: a founded civilization starts with ZERO completed research
/// nodes, and zero completed nodes is not zero capability. The baseline capabilities live
/// OUTSIDE the research graph (no node, no completed row stands for them); the simulated ones
/// are realized by systems that read no research state. There is no Builder unit.
/// </summary>
public class ResearchBaselineTests
{
    private static readonly PolityId Player1 = new(Player);

    private static WorldState Found() => WorldFounding.Found(TestConfigs.DevWorldgen(), TestConfigs.Sim(), 42);

    private static TurnExecutor Executor()
    {
        using var era = Sim.Data.DataFiles.OpenEraPacing();
        using var pipe = Sim.Data.DataFiles.OpenPipeline();
        return new TurnExecutor(EraTableLoader.Load(era),
            PipelineLoader.Load(pipe, SystemCatalog.All(TestConfigs.Sim(), TestConfigs.DevWorldgen())), null);
    }

    [Fact]
    public void Founding_StartsWithZeroCompletedTechnologyOrCivicsNodes()
    {
        WorldState w = Found();
        Assert.Equal(0, w.ResearchCompleted.Count);
        Assert.Empty(ResearchQuery.CompletedNodes(w, TestConfigs.Research(), Player1));
    }

    [Fact]
    public void Baseline_IsDeclaredOutsideTheGraph_WithEveryRuledMinimumCapability()
    {
        ResearchContent content = TestConfigs.Research();
        IReadOnlyList<ResearchBaselineCapability> baseline = ResearchQuery.BaselineCapabilities(content);
        string[] ids = [.. baseline.Select(b => b.Id)];
        foreach (string required in new[]
                 {
                     "baseline.settlement_founding", "baseline.exploration", "baseline.basic_military",
                     "baseline.food_gathering", "baseline.construction",
                 })
            Assert.Contains(required, ids);
        foreach (ResearchBaselineCapability b in baseline)
            Assert.Equal(-1, content.IndexOfId(b.Id));                       // never a research node
        // No Builder: construction is internal, and no unit entity stands for it.
        Assert.Equal(-1, content.EntityIndexOf("unit.builder"));
        Assert.Contains("no Builder", baseline.Single(b => b.Id == "baseline.construction").ProvidedBy);
        // Honest about what is not simulated yet.
        Assert.False(baseline.Single(b => b.Id == "baseline.exploration").Simulated);
        Assert.False(baseline.Single(b => b.Id == "baseline.basic_military").Simulated);
    }

    [Fact]
    public void Baseline_TheSimulatedCapabilitiesWork_WithZeroCompletedNodes()
    {
        // No research order at all: nothing is ever completed, yet the civilization gathers food,
        // grows, builds and founds — its baseline needs no research node.
        WorldState start = Found();
        int settlements0 = start.Settlements.Count;
        long population0 = ResearchQuery.Population(start, Player1);
        TurnExecutor ex = Executor();
        WorldState w = start;
        for (int t = 0; t < 100; t++) w = ex.Step(w);
        Assert.Equal(0, w.ResearchCompleted.Count);
        Assert.True(ResearchQuery.Population(w, Player1) > population0, "the population should grow on baseline gathering");
        long stocked = 0;
        for (int i = 0; i < w.GoodStocks.Count; i++) stocked += w.GoodStocks[i].Amount.Value;
        Assert.True(stocked > 0, "baseline gathering should stock goods");
        Assert.True(w.Settlements.Count >= settlements0);
        // The systems that provide the simulated baseline are in the pipeline, ahead of research.
        using var pipe = Sim.Data.DataFiles.OpenPipeline();
        string pipeline = new StreamReader(pipe).ReadToEnd();
        foreach (string system in new[] { "production", "construction", "migration", "colonization" })
            Assert.True(pipeline.IndexOf($"\"{system}\"", StringComparison.Ordinal) < pipeline.IndexOf("\"research\"", StringComparison.Ordinal));
    }

    [Fact]
    public void Baseline_LoaderRejects_AnEmptyBaseline_AndABaselineIdThatIsAResearchNode()
    {
        Spec empty = Standard();
        empty.WithBaseline = false;
        Assert.Contains("baseline is empty", Assert.Throws<ResearchContentException>(empty.Load).Message);

        string json = Standard().Json().Replace("\"id\":\"baseline.settlement_founding\"", "\"id\":\"settlement_founding\"", StringComparison.Ordinal);
        Assert.Contains("must start with 'baseline.'",
            Assert.Throws<ResearchContentException>(() => ResearchContentLoader.Load(json, TestConfigs.Sim().Goods)).Message);
    }
}
