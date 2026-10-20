using Sim.Core.Systems;

namespace Sim.Tests.TestUtil;

/// <summary>
/// W1 F7 (batch 4; D-055, 2026-10-16) — THE NON-CHAINED SINGLE-SWITCH TWINS of the cylinder feature-attribution rigs
/// (CylinderAttribution*Tests, design §2). Each returns <paramref name="cfg"/> with EXACTLY ONE layer's switch off and
/// every other member kept by reference, so a rig's OFF arm differs from its ON arm (the shipped config,
/// <see cref="TestConfigs.Sim"/>) by that layer alone. They are NOT the R4-chained attribution twins of
/// <see cref="TestConfigs"/> (PreForager, PreTimeAveragedHarvest, PreRecipeKnowledge, PreTradeKnowledge, PreAiFounding,
/// PreTaxAge): those strip every NEWER layer as well (through PreD053 and PreD054Sim, reservations included), so their
/// divergence turn is the earliest-acting stripped layer's, not the layer's own. The departure from the R4 rule is
/// deliberate and recorded in D-055: these twins compare two LIVE runs on the shipped cylinder and need no historical
/// literal, which is the only reason the R4 rule exists. Reservations, incremental construction, the split, the purse,
/// relief and the granary effects all stay ON in every twin here.
///
/// Each throws when its switch is absent from <paramref name="cfg"/>: an absent switch would make the twin the ON config
/// itself and a rig vacuous. SingleSwitchTwinsTests proves each is single-switch (restoring the one member returns a
/// config equal to the input).
/// </summary>
public static class SingleSwitchTwins
{
    /// <summary>R4's forager layer alone: sim.json farming.preCultivation.enabled false (wild-food gathering off; the
    /// chained twin is <see cref="TestConfigs.PreForager"/>).</summary>
    public static SimConfig PreForagerOnly(SimConfig cfg)
    {
        PreCultivationConfig pre = cfg.Farming.PreCultivation
            ?? throw new ArgumentException("no farming.preCultivation section to switch off", nameof(cfg));
        return cfg with { Farming = cfg.Farming with { PreCultivation = pre with { Enabled = false } } };
    }

    /// <summary>D-051 1(b) alone: sim.json harvestVariance.timeAveragedHarvest false (a turn's harvest multiplier is the
    /// single point draw again; the chained twin is <see cref="TestConfigs.PreTimeAveragedHarvest"/>).</summary>
    public static SimConfig PreTimeAveragedHarvestOnly(SimConfig cfg)
    {
        if (!cfg.HarvestVariance.TimeAveragedHarvest)
            throw new ArgumentException("harvestVariance.timeAveragedHarvest is already off", nameof(cfg));
        return cfg with { HarvestVariance = cfg.HarvestVariance with { TimeAveragedHarvest = false } };
    }

    /// <summary>R1's recipe-knowledge gate alone: every goods.json recipe's research-entity link removed (every recipe is
    /// knowledge-free; the chained twin is <see cref="TestConfigs.PreRecipeKnowledge"/>).</summary>
    public static SimConfig PreRecipeKnowledgeOnly(SimConfig cfg)
    {
        GoodsConfig goods = cfg.Goods ?? throw new ArgumentException("no goods content", nameof(cfg));
        bool gated = false;
        var recipes = new RecipeEntry[goods.Recipes.Length];
        for (int i = 0; i < recipes.Length; i++)
        {
            gated |= goods.Recipes[i].Entity is not null;
            recipes[i] = goods.Recipes[i] with { Entity = null };
        }
        if (!gated) throw new ArgumentException("no recipe carries a research-entity link", nameof(cfg));
        return cfg with { Goods = goods with { Recipes = recipes } };
    }

    /// <summary>R2a's trade-knowledge layer alone: the research content without the R2a additions (the `trade` node, the
    /// `activity.trade` entity, tuning.cityStatePaceFraction — the content <see cref="TestConfigs.PreTradeKnowledge"/>
    /// carries) and sim.json trade.entity null (trade ungated). Only those two members change; the chained twin also
    /// strips every newer layer.</summary>
    public static SimConfig PreTradeKnowledgeOnly(SimConfig cfg)
    {
        if (cfg.Trade.Entity is null) throw new ArgumentException("trade.entity is already null (trade ungated)", nameof(cfg));
        return cfg with { Research = TestConfigs.PreTradeKnowledge(cfg).Research, Trade = cfg.Trade with { Entity = null } };
    }

    /// <summary>S1c alone: sim.json colonization.aiFounding.enabled false (no AI Found Settlement order; the chained twin is
    /// <see cref="TestConfigs.PreAiFounding"/>).</summary>
    public static SimConfig PreAiFoundingOnly(SimConfig cfg)
    {
        ColonizationConfig col = cfg.Colonization ?? throw new ArgumentException("no colonization section", nameof(cfg));
        AiFoundingConfig ai = col.AiFounding ?? throw new ArgumentException("no colonization.aiFounding section", nameof(cfg));
        return cfg with { Colonization = col with { AiFounding = ai with { Enabled = false } } };
    }

    /// <summary>H2's tax Age gate alone: sim.json governance.taxationMinAge removed (the edict is gated by the Taxation
    /// knowledge alone; the chained twin is <see cref="TestConfigs.PreTaxAge"/>).</summary>
    public static SimConfig PreTaxGateOnly(SimConfig cfg)
    {
        GovernanceConfig g = cfg.Governance ?? throw new ArgumentException("no governance section", nameof(cfg));
        if (g.TaxationMinAge is null) throw new ArgumentException("governance.taxationMinAge is already absent", nameof(cfg));
        return cfg with { Governance = g with { TaxationMinAge = null } };
    }
}
