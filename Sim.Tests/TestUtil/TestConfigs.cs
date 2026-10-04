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
        cfg = PreForager(cfg);
        GoodsConfig goods = cfg.Goods ?? throw new ArgumentException("no goods content", nameof(cfg));
        var recipes = new RecipeEntry[goods.Recipes.Length];
        for (int i = 0; i < recipes.Length; i++) recipes[i] = goods.Recipes[i] with { Entity = null };
        return cfg with { Goods = goods with { Recipes = recipes } };
    }

    // ======================================================================
    // M5 R2b — THE ATTRIBUTION TWINS. Each strips ONE R2b layer as content/config (the T4.21-4 / R1
    // content-twin precedent), so IntegratedPinAttributionTests can prove each layer's movement and every
    // older layer control keeps running on the pre-R2b world.
    // ======================================================================

    /// <summary>R2b weather layer stripped: the harvest-weather kernel reads the road-aware travel costs
    /// again (harvestVariance.spatialDistance "travelCost").</summary>
    public static SimConfig PreWeatherGeography(SimConfig cfg) =>
        cfg with { HarvestVariance = cfg.HarvestVariance with { SpatialDistance = "travelCost" } };

    /// <summary>R2b unrest layer stripped: Dignity unbound again and no unrest section — grievance drives
    /// nothing and the tax injures no need (the pre-R2b needs.json).</summary>
    public static SimConfig PreUnrest(SimConfig cfg)
    {
        NeedsConfig needs = cfg.Needs ?? throw new ArgumentException("no needs content", nameof(cfg));
        var entries = new NeedEntry[needs.Needs.Length];
        for (int i = 0; i < entries.Length; i++)
            entries[i] = needs.Needs[i].FromTaxBurden ? needs.Needs[i] with { Bound = false, Source = null } : needs.Needs[i];
        return cfg with { Needs = needs with { Needs = entries, Unrest = null } };
    }

    /// <summary>R2b Age-military layer stripped: every formations fact counts any identity again (the
    /// founding warband line satisfies every Age's military milestone, as before R2b).</summary>
    public static SimConfig PreAgeMilitary(SimConfig cfg)
    {
        global::Sim.Core.Systems.Ages.AgeContent ages = cfg.Ages ?? throw new ArgumentException("no age content", nameof(cfg));
        var defs = new global::Sim.Core.Systems.Ages.AgeDefinition[ages.Ages.Count];
        for (int a = 0; a < defs.Length; a++)
        {
            global::Sim.Core.Systems.Ages.AgeDefinition d = ages.Ages[a];
            if (d.Entry is not { } e) { defs[a] = d; continue; }
            defs[a] = d with { Entry = e with { Core = Relax(e.Core), Supporting = Relax(e.Supporting) } };
        }
        var relaxed = new global::Sim.Core.Systems.Ages.AgeContent
        {
            Ages = defs, Categories = ages.Categories, Surges = ages.Surges, FoundingAge = ages.FoundingAge,
            FoundingAgeBasis = ages.FoundingAgeBasis, SurgeShape = ages.SurgeShape, Status = ages.Status,
        };
        return cfg with { Ages = relaxed };

        static global::Sim.Core.Systems.Ages.AgeMilestone[] Relax(IReadOnlyList<global::Sim.Core.Systems.Ages.AgeMilestone> list)
        {
            var result = new global::Sim.Core.Systems.Ages.AgeMilestone[list.Count];
            for (int i = 0; i < result.Length; i++)
                result[i] = list[i] with { Fact = list[i].Fact with { IdentityKeys = null, MinIdentityAge = 0 }, Pending = null };
            return result;
        }
    }

    /// <summary>Every R2b layer stripped (weather geography, unrest/Dignity, Age military realization).</summary>
    public static SimConfig PreR2b(SimConfig cfg) => PreAgeMilitary(PreUnrest(PreWeatherGeography(PreForager(cfg))));

    /// <summary>R4 (2026-10-04) — THE FORAGER LAYER STRIPPED: sim.json farming.preCultivation switched OFF (the
    /// shipped state through R3). It is the NEWEST layer, so every older layer twin (PreR2b, PreTradeKnowledge,
    /// PreRecipeKnowledge) strips it as well and keeps returning its pre-R4 constant; on its own it returns the
    /// R3 pins byte for byte (IntegratedPinAttributionTests' R4 controls).</summary>
    public static SimConfig PreForager(SimConfig cfg) =>
        cfg.Farming.PreCultivation is { } pre
            ? cfg with { Farming = cfg.Farming with { PreCultivation = pre with { Enabled = false } } }
            : cfg;

    /// <summary>R1: completes, for EVERY polity of <paramref name="w"/>, the knowledge closure (the named nodes
    /// and all their prerequisite ancestors) of every research-gated goods.json recipe — the world of a
    /// civilization that already knows its crafts. For tests whose subject is the goods economy (crafted goods,
    /// trade, merchants), not research. Data rows only (ResearchCompleted); returns the same world.</summary>
    public static global::Sim.Core.State.WorldState KnowRecipes(global::Sim.Core.State.WorldState w, SimConfig cfg)
    {
        var research = cfg.Research!;
        var seen = new bool[research.Nodes.Count];
        var stack = new Stack<int>();
        foreach (RecipeEntry r in cfg.Goods!.Recipes)
        {
            if (r.Entity is null) continue;
            foreach (int a in research.Entities[research.EntityIndexOf(r.Entity)].NodeAtoms) stack.Push(a);
        }
        while (stack.Count > 0)
        {
            int i = stack.Pop();
            if (seen[i]) continue;
            seen[i] = true;
            foreach (int p in research.Nodes[i].PrerequisiteNodes) stack.Push(p);
        }
        for (int p = 0; p < w.Polities.Count; p++)
            for (int i = 0; i < seen.Length; i++)
                if (seen[i]) w.ResearchCompleted.Add(new global::Sim.Core.State.ResearchCompletedRow(w.Polities[p].Id, research.Nodes[i].Key));
        return w;
    }

    /// <summary>R2a: <paramref name="cfg"/> as it was BEFORE the R2a layer — the research content without the
    /// `trade` node, the `activity.trade` entity and tuning.cityStatePaceFraction (no city-state research), and
    /// sim.json trade.entity null (trade ungated). A pure data change: the attribution controls run every older
    /// layer control on PreR1(PreR2a) so each pre-R2a constant is unmoved, and the R2a controls prove that removing
    /// the layer returns the pre-R2a pins byte for byte.</summary>
    public static SimConfig PreTradeKnowledge(SimConfig cfg) =>
        PreForager(cfg) with { Research = PreR2aResearch.Value, Trade = PreForager(cfg).Trade with { Entity = null } };

    private static readonly Lazy<global::Sim.Core.Systems.Research.ResearchContent> PreR2aResearch = new(() =>
    {
        using var stream = global::Sim.Data.DataFiles.OpenResearch();
        var doc = System.Text.Json.Nodes.JsonNode.Parse(stream)!.AsObject();
        var techs = doc["technologies"]!.AsArray();
        for (int i = techs.Count - 1; i >= 0; i--) if ((string?)techs[i]!["id"] == "trade") techs.RemoveAt(i);
        var ents = doc["entities"]!.AsArray();
        for (int i = ents.Count - 1; i >= 0; i--) if ((string?)ents[i]!["id"] == "activity.trade") ents.RemoveAt(i);
        doc["tuning"]!.AsObject().Remove("cityStatePaceFraction");
        using var goods = global::Sim.Data.DataFiles.OpenGoods();
        using var sim = global::Sim.Data.DataFiles.OpenSim();
        using var needs = global::Sim.Data.DataFiles.OpenNeeds();
        SimConfig plain = SimConfigLoader.Load(sim, needs, goods);
        return global::Sim.Core.Systems.Research.ResearchContentLoader.Load(doc.ToJsonString(), plain.Goods);
    });

    /// <summary>R2a: the knowledge closure of the Trade capability (sim.json trade.entity), completed for EVERY
    /// polity of <paramref name="w"/> — for tests whose subject is the trade economy, not research.</summary>
    public static global::Sim.Core.State.WorldState KnowTrade(global::Sim.Core.State.WorldState w, SimConfig cfg)
    {
        var research = cfg.Research!;
        if (cfg.Trade.Entity is null) return w;
        var seen = new bool[research.Nodes.Count];
        var stack = new Stack<int>();
        foreach (int a in research.Entities[research.EntityIndexOf(cfg.Trade.Entity)].NodeAtoms) stack.Push(a);
        while (stack.Count > 0)
        {
            int i = stack.Pop();
            if (seen[i]) continue;
            seen[i] = true;
            // OR-prerequisites: the closure takes every alternative — knowledge rows only, the subject is trade.
            foreach (int q in research.Nodes[i].PrerequisiteNodes) stack.Push(q);
        }
        for (int p = 0; p < w.Polities.Count; p++)
            for (int i = 0; i < seen.Length; i++)
                if (seen[i] && !global::Sim.Core.State.ResearchQuery.IsCompleted(w, w.Polities[p].Id, research.Nodes[i].Key))
                    w.ResearchCompleted.Add(new global::Sim.Core.State.ResearchCompletedRow(w.Polities[p].Id, research.Nodes[i].Key));
        return w;
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
