using Sim.Core.Systems;
using Sim.Core.Systems.Research;

namespace Sim.Ui.Tests;

/// <summary>D-063 (tranche 2, 2026-10-24): the Sim.Ui twin of Sim.Tests' TestConfigs.PreTranche2 and the CLI's
/// `--without-tranche2` — the config with its research content replaced by the pre-tranche-2 file
/// (Sim.Data.DataFiles.OpenResearchPreTranche2: the 496 / 70 node arrays, no realized effects, no Age sweep, no destination
/// routing, no repeatable levels, no pacing scale). For a UI test whose SUBJECT is not tranche 2 but whose premise was the
/// pre-tranche-2 content (a locked node a click only selects, a measured construction walk); tranche 2's own UI behaviour
/// has its own tests on the shipped content. Used through <see cref="UiSession.StartWith"/> /
/// <see cref="UiSession.StartFromWith"/>.</summary>
internal static class TestTranche2
{
    private static readonly Lazy<string> PreTranche2Json = new(() =>
    {
        using var s = Sim.Data.DataFiles.OpenResearchPreTranche2();
        using var reader = new StreamReader(s);
        return reader.ReadToEnd();
    });

    private static readonly Lazy<ResearchContent> PreTranche2Research = new(() =>
    {
        using var goods = Sim.Data.DataFiles.OpenGoods();
        return ResearchContentLoader.Load(PreTranche2Json.Value, GoodsConfigLoader.Load(goods));
    });

    private static readonly Lazy<SimConfig> PreTranche2Config = new(() => Off(UiFounding.ProductionConfig()));
    private static readonly Lazy<SimConfig> PreTranche2PreDiscoveryConfig = new(() => TestFog.Off(PreTranche2Config.Value));

    /// <summary><paramref name="cfg"/> with the research content replaced by the pre-tranche-2 file (the CLI's recipe).</summary>
    // ADR-056: the pre-tranche-2 world predates the economy packets too (the R4 rule).
    public static SimConfig Off(SimConfig cfg) => Sim.Core.State.EconomyKnowledge.WithoutEconomy(cfg with { Research = PreTranche2Research.Value });

    /// <summary>The production config on the pre-tranche-2 research content.</summary>
    public static SimConfig PreTranche2 => PreTranche2Config.Value;

    /// <summary>The production config on the pre-tranche-2 research content with the discovery layer off too.</summary>
    public static SimConfig PreTranche2PreDiscovery => PreTranche2PreDiscoveryConfig.Value;
}
