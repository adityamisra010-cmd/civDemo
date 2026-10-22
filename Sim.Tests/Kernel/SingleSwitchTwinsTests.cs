using Sim.Core.Systems;
using Sim.Tests.TestUtil;

namespace Sim.Tests.Kernel;

/// <summary>
/// W1 F7 (batch 4; D-055, 2026-10-16) — the cylinder rigs' OFF arms are SINGLE-SWITCH twins
/// (<see cref="SingleSwitchTwins"/>): each must differ from the shipped config in its one member and nowhere else, and keep
/// every newer layer (reservations, incremental construction, the split, the purse, relief, the granary effects) on.
/// Restoring the one member returns a config EQUAL to the input (record equality: every other member is the same
/// reference), which is what "non-chained" means. An absent or already-off switch is refused, so no rig can run a twin
/// that is silently the ON config.
/// </summary>
public class SingleSwitchTwinsTests
{
    private static readonly SimConfig On = TestConfigs.Sim();

    [Fact]
    public void TheShippedConfig_HasEverySwitchTheTwinsTurnOff_AndEveryNewerLayerOn()
    {
        Assert.True(On.Farming.PreCultivation!.Enabled);
        Assert.True(On.HarvestVariance.TimeAveragedHarvest);
        Assert.Contains(On.Goods!.Recipes, r => r.Entity is not null);
        Assert.NotNull(On.Trade.Entity);
        Assert.True(On.Colonization!.AiFounding!.Enabled);
        Assert.NotNull(On.Governance!.TaxationMinAge);
        Assert.True(On.Construction!.Reservations && On.Construction.Incremental && On.Construction.EmpireStockpile);
        Assert.True(On.Consumption.EmpireFoodAllocation);
        Assert.True(On.Granaries!.Enabled);
    }

    [Fact]
    public void PreForagerOnly_SwitchesOnlyWildFoodGatheringOff()
    {
        SimConfig twin = SingleSwitchTwins.PreForagerOnly(On);
        Assert.False(twin.Farming.PreCultivation!.Enabled);
        Assert.Equal(On.Farming, twin.Farming with { PreCultivation = On.Farming.PreCultivation });
        Assert.Equal(On.Farming.PreCultivation, twin.Farming.PreCultivation with { Enabled = true });
        Assert.Equal(On, twin with { Farming = On.Farming });
        Assert.NotEqual(On, twin);
    }

    [Fact]
    public void PreTimeAveragedHarvestOnly_SwitchesOnlyTheDecadeAveragingOff()
    {
        SimConfig twin = SingleSwitchTwins.PreTimeAveragedHarvestOnly(On);
        Assert.False(twin.HarvestVariance.TimeAveragedHarvest);
        Assert.Equal(On.HarvestVariance, twin.HarvestVariance with { TimeAveragedHarvest = true });
        Assert.Equal(On, twin with { HarvestVariance = On.HarvestVariance });
        Assert.Throws<ArgumentException>(() => SingleSwitchTwins.PreTimeAveragedHarvestOnly(twin));
    }

    [Fact]
    public void PreRecipeKnowledgeOnly_RemovesOnlyTheRecipesResearchLinks()
    {
        SimConfig twin = SingleSwitchTwins.PreRecipeKnowledgeOnly(On);
        Assert.Equal(On.Goods!.Recipes.Length, twin.Goods!.Recipes.Length);
        for (int i = 0; i < On.Goods.Recipes.Length; i++)
        {
            Assert.Null(twin.Goods.Recipes[i].Entity);
            Assert.Equal(On.Goods.Recipes[i], twin.Goods.Recipes[i] with { Entity = On.Goods.Recipes[i].Entity });
        }
        Assert.Equal(On.Goods, twin.Goods with { Recipes = On.Goods.Recipes });
        Assert.Equal(On, twin with { Goods = On.Goods });
        Assert.Throws<ArgumentException>(() => SingleSwitchTwins.PreRecipeKnowledgeOnly(twin));
    }

    [Fact]
    public void PreTradeKnowledgeOnly_StripsOnlyTheR2aResearchAndTheTradeGate()
    {
        SimConfig twin = SingleSwitchTwins.PreTradeKnowledgeOnly(On);
        Assert.Null(twin.Trade.Entity);
        Assert.Equal(On.Trade, twin.Trade with { Entity = On.Trade.Entity });
        Assert.True(On.Research!.EntityIndexOf("activity.trade") >= 0);
        Assert.True(twin.Research!.EntityIndexOf("activity.trade") < 0);    // the R2a entity is gone with its node
        // ADR-043 (2026-10-22) CAUSE BLOCK: the chained PreTradeKnowledge now strips Track CP as well (the R4 rule), so the
        // single switch carries the pre-R2a content WITH Track CP as shipped (TestConfigs.PreR2aResearchKeepingTrackCp) —
        // only the R2a layer differs from On's research.
        Assert.Equal(TestConfigs.PreR2aResearchKeepingTrackCp(), twin.Research);
        Assert.True(twin.Research.Tuning.TwoLanes);
        Assert.False(TestConfigs.PreTradeKnowledge(On).Research!.Tuning.TwoLanes);
        Assert.Equal(On, twin with { Research = On.Research, Trade = On.Trade });
        Assert.Throws<ArgumentException>(() => SingleSwitchTwins.PreTradeKnowledgeOnly(twin));
    }

    [Fact]
    public void PreAiFoundingOnly_SwitchesOnlyTheAiFoundingPolicyOff()
    {
        SimConfig twin = SingleSwitchTwins.PreAiFoundingOnly(On);
        Assert.False(twin.Colonization!.AiFounding!.Enabled);
        Assert.Equal(On.Colonization, twin.Colonization with { AiFounding = On.Colonization!.AiFounding });
        Assert.Equal(On, twin with { Colonization = On.Colonization });
    }

    [Fact]
    public void PreTaxGateOnly_RemovesOnlyTheTaxAgeGate()
    {
        SimConfig twin = SingleSwitchTwins.PreTaxGateOnly(On);
        Assert.Null(twin.Governance!.TaxationMinAge);
        Assert.Equal(On.Governance, twin.Governance with { TaxationMinAge = On.Governance!.TaxationMinAge });
        Assert.Equal(On, twin with { Governance = On.Governance });
        Assert.Throws<ArgumentException>(() => SingleSwitchTwins.PreTaxGateOnly(twin));
    }

    /// <summary>The chained R4 twins are NOT single-switch: each also strips the newer layers (here: reservations and
    /// the purse), which is why the cylinder rigs do not use them.</summary>
    [Fact]
    public void TheChainedTwins_StripNewerLayers_TheSingleSwitchTwinsDoNot()
    {
        foreach (SimConfig chained in new[]
                 {
                     TestConfigs.PreForager(On), TestConfigs.PreTimeAveragedHarvest(On), TestConfigs.PreRecipeKnowledge(On),
                     TestConfigs.PreTradeKnowledge(On), TestConfigs.PreAiFounding(On), TestConfigs.PreTaxAge(On),
                 })
            Assert.False(chained.Construction!.Reservations || chained.Construction.EmpireStockpile);
        foreach (SimConfig single in new[]
                 {
                     SingleSwitchTwins.PreForagerOnly(On), SingleSwitchTwins.PreTimeAveragedHarvestOnly(On),
                     SingleSwitchTwins.PreRecipeKnowledgeOnly(On), SingleSwitchTwins.PreTradeKnowledgeOnly(On),
                     SingleSwitchTwins.PreAiFoundingOnly(On), SingleSwitchTwins.PreTaxGateOnly(On),
                 })
        {
            Assert.Same(On.Construction, single.Construction);
            Assert.Same(On.Consumption, single.Consumption);
            Assert.Same(On.Granaries, single.Granaries);
        }
    }
}
