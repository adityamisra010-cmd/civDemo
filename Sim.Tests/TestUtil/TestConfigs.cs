using Sim.Core.Systems;
using Sim.Core.Worldgen;

namespace Sim.Tests.TestUtil;

/// <summary>Canonical data-file configs, loaded fresh per call (records are immutable).</summary>
public static class TestConfigs
{
    /// <summary>The canonical four-file config (ADR-029: research.json rides with it,
    /// so every production pipeline built from this runs the research engine exactly as
    /// the CLI and UI do). sim/needs/goods load fresh per call; the research content,
    /// which is immutable and about 480 KB, is parsed and validated ONCE per test process.</summary>
    /// <summary>R1 (research → gameplay unlock pipeline): <paramref name="cfg"/> with every goods.json
    /// recipe's research-entity link removed — the PRE-R1 twin, in which every recipe is knowledge-free
    /// exactly as before the recipe gate. A pure data change: the attribution controls run the layer
    /// controls on this twin (the T4.21-4 precedent — a layout control must not be asked to absorb a
    /// behaviour change), and IntegratedPinAttributionTests' recipe-knowledge controls prove that removing
    /// the links returns the pre-R1 pins byte for byte.</summary>
    public static SimConfig PreRecipeKnowledge(SimConfig cfg)
    {
        GoodsConfig goods = cfg.Goods ?? throw new ArgumentException("no goods content", nameof(cfg));
        var recipes = new RecipeEntry[goods.Recipes.Length];
        for (int i = 0; i < recipes.Length; i++) recipes[i] = goods.Recipes[i] with { Entity = null };
        return cfg with { Goods = goods with { Recipes = recipes } };
    }

    public static SimConfig Sim()
    {
        using var stream = global::Sim.Data.DataFiles.OpenSim();
        using var needs = global::Sim.Data.DataFiles.OpenNeeds();
        using var goods = global::Sim.Data.DataFiles.OpenGoods();
        return SimConfigLoader.Load(stream, needs, goods) with
        {
            Research = CanonicalResearch.Value,
            Ages = CanonicalProgression.Value.Ages,
            UnitFamilies = CanonicalProgression.Value.UnitFamilies,
        };
    }

    // ADR-031: ages.json + unit-families.json, parsed and validated once per process against
    // the canonical research/goods/class content (both are immutable).
    private static readonly Lazy<SimConfig> CanonicalProgression = new(() =>
    {
        using var stream = global::Sim.Data.DataFiles.OpenSim();
        using var needs = global::Sim.Data.DataFiles.OpenNeeds();
        using var goods = global::Sim.Data.DataFiles.OpenGoods();
        using var ages = global::Sim.Data.DataFiles.OpenAges();
        using var families = global::Sim.Data.DataFiles.OpenUnitFamilies();
        SimConfig cfg = SimConfigLoader.Load(stream, needs, goods) with { Research = CanonicalResearch!.Value };
        return SimConfigLoader.WithProgression(cfg, ages, families);
    });

    /// <summary>The canonical Age content (shared, immutable).</summary>
    public static global::Sim.Core.Systems.Ages.AgeContent Ages() => CanonicalProgression.Value.Ages!;

    /// <summary>The canonical unit-family content (shared, immutable).</summary>
    public static global::Sim.Core.Systems.Ages.UnitFamilyContent UnitFamilies() => CanonicalProgression.Value.UnitFamilies!;

    private static readonly Lazy<global::Sim.Core.Systems.Research.ResearchContent> CanonicalResearch = new(() =>
    {
        using var goods = global::Sim.Data.DataFiles.OpenGoods();
        using var research = global::Sim.Data.DataFiles.OpenResearch();
        return global::Sim.Core.Systems.Research.ResearchContentLoader.Load(research, GoodsConfigLoader.Load(goods));
    });

    /// <summary>The canonical research content (shared, immutable).</summary>
    public static global::Sim.Core.Systems.Research.ResearchContent Research() => CanonicalResearch.Value;

    /// <summary>The raw canonical research.json text (for loader-rejection tests).</summary>
    public static string ResearchJson()
    {
        using var stream = global::Sim.Data.DataFiles.OpenResearch();
        using var reader = new StreamReader(stream);
        return reader.ReadToEnd();
    }

    /// <summary>T4.7: the canonical `transport.riverCostFactor` — the required
    /// TraversalLattice.Build argument, so rig-local lattices are built on the
    /// same cost model production uses.</summary>
    public static double RiverCostFactor() => Sim().Transport.RiverCostFactor;

    /// <summary>The raw canonical sim.json text (for loader-rejection tests).</summary>
    public static string SimJson()
    {
        using var stream = global::Sim.Data.DataFiles.OpenSim();
        using var reader = new StreamReader(stream);
        return reader.ReadToEnd();
    }

    public static WorldgenConfig Worldgen()
    {
        using var stream = global::Sim.Data.DataFiles.OpenWorldgen();
        return WorldgenConfigLoader.Load(stream);
    }

    /// <summary>The D-015 dev preset: canonical worldgen at 256², N = 4
    /// settlements (D-025 dev preset) for fast tests.</summary>
    public static WorldgenConfig DevWorldgen()
    {
        WorldgenConfig cfg = Worldgen();
        return cfg with { SizePx = 256, Siting = cfg.Siting with { SettlementCount = 4 } };
    }
}
