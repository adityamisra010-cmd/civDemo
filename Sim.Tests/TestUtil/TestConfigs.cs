using Sim.Core.Systems;
using Sim.Core.Worldgen;

namespace Sim.Tests.TestUtil;

/// <summary>Canonical data-file configs, loaded fresh per call (records are immutable).
///
/// D-055 (2026-10-16) — THE RESERVATIONS SWITCH (sim.json construction.reservations, schema v34) AND THE TWINS. Its
/// attribution control is <see cref="PreReservations"/>. By the R4 rule (an older layer twin strips every NEWER layer)
/// the CHAINED twins strip it: <see cref="PreIncrementalConstruction"/> (the loader refuses reservations without
/// incremental), and through it <see cref="PreD054Sim"/>, <see cref="PreD053"/> and every older chained twin
/// (PreForager, PreR2b, PreTradeKnowledge, PreRecipeKnowledge, PreTimeAveragedHarvest, PreTaxAge, PreAiFounding). The
/// SINGLE-LAYER helpers keep reservations ON — <see cref="PreEmpireConstructionStockpile"/>,
/// <see cref="PreEmpireFoodAllocation"/>, <see cref="PreGranaryEffects"/> and <see cref="PreAllocationRatios"/> (and
/// the F7 single-switch helpers, <see cref="SingleSwitchTwins"/>): each switches exactly one layer off. <see cref="PreAllocationRatios"/> is the RECORDED
/// R4 EXCEPTION (D-055): although reservations are newer than the split, the split's twin keeps them, because the split's
/// default (b = 100) must equal its absence and holds are not part of the split — a reservations-stripping twin would
/// make "no split" differ from "the default split" wherever a hold row is published. Do not "fix" it into the chain.
/// </summary>
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

    /// <summary>H2 (2026-10-05) — THE TAX AGE GATE STRIPPED: sim.json governance.taxationMinAge removed, so the tax
    /// edict is gated by the Taxation knowledge alone (the R5 gate). The AI-world attribution control
    /// (AiEmpireIntegrationTests) runs on it and returns the pre-H2 levy turns. The whole D-053 batch (2026-10-13, newer
    /// than H2) is stripped with it (<see cref="PreD053"/>) — the R4 rule: an older layer twin strips EVERY newer layer.</summary>
    public static SimConfig PreTaxAge(SimConfig cfg)
    {
        cfg = PreD053(cfg);
        return cfg.Governance is { } g ? cfg with { Governance = g with { TaxationMinAge = null } } : cfg;
    }

    /// <summary>R4 (2026-10-04) — THE FORAGER LAYER STRIPPED: sim.json farming.preCultivation switched OFF (the
    /// shipped state through R3). It is the NEWEST layer, so every older layer twin (PreR2b, PreTradeKnowledge,
    /// PreRecipeKnowledge) strips it as well and keeps returning its pre-R4 constant; on its own it returns the
    /// R3 pins byte for byte (IntegratedPinAttributionTests' R4 controls).
    /// The D-053 batch (2026-10-13: the construction purse §1, intra-polity grain relief §2, the granary effects §3), by
    /// the same rule: all three layers are NEWER than R4, so this twin — and through it every older layer twin
    /// (PreR2b, PreTradeKnowledge, PreRecipeKnowledge, IntegratedPinAttributionTests' PreR1/Unarmed) — strips them too
    /// (<see cref="PreD053"/>); without that the pre-R4 world would carry post-R4 layers and could not return its R3
    /// pins. The R4 controls are therefore no longer literally one layer: the twin differs from the live world by the
    /// forager layer AND the batch, and the batch-only link of the chain is each D-053 track's switch-off control (the
    /// batch off returns de86ace), with the per-layer turn-exact divergence beside it.</summary>
    public static SimConfig PreForager(SimConfig cfg)
    {
        cfg = PreD053(cfg);
        return cfg.Farming.PreCultivation is { } pre
            ? cfg with { Farming = cfg.Farming with { PreCultivation = pre with { Enabled = false } } }
            : cfg;
    }

    /// <summary>D-051 ruling 1(b) (2026-10-10) — TIME-AVERAGED DECADE WEATHER STRIPPED: sim.json
    /// harvestVariance.timeAveragedHarvest switched OFF, so a turn's harvest multiplier is again the single point
    /// draw exp(x − σ²/2) with ρ = exp(−dt/τ) and a row-less settlement starts at x = 0. The switch exists only as
    /// this attribution control: on its own it returns the pre-1(b) pins byte for byte
    /// (IntegratedPinAttributionTests' D-051 1(b) controls). The whole D-053 batch (newer than 1(b)) is stripped with it
    /// (<see cref="PreD053"/>), the R4 rule: an older layer twin strips every newer layer — so the 1(b) controls are
    /// 1(b) plus the batch, and the batch-only link is each D-053 track's switch-off control.</summary>
    public static SimConfig PreTimeAveragedHarvest(SimConfig cfg) =>
        PreD053(cfg) with { HarvestVariance = cfg.HarvestVariance with { TimeAveragedHarvest = false } };

    /// <summary>M5 S1c (2026-10-11) — THE AI FOUNDING POLICY SWITCHED OFF: sim.json colonization.aiFounding.enabled
    /// false, so no AI polity issues a Found Settlement order. On its own it returns every pre-S1c AI pin byte for byte
    /// (AiFoundingAttributionTests; AiEmpireIntegrationTests' S1c control); every other world is untouched by the
    /// switch (the policy acts only through the orders it adds). The whole D-053 batch (2026-10-13, newer than S1c) is
    /// stripped with it (<see cref="PreD053"/>), the R4 rule: an older layer twin strips every newer layer.</summary>
    public static SimConfig PreAiFounding(SimConfig cfg)
    {
        cfg = PreD053(cfg);
        return cfg.Colonization?.AiFounding is { } ai
            ? cfg with { Colonization = cfg.Colonization with { AiFounding = ai with { Enabled = false } } }
            : cfg;
    }

    /// <summary>D-053 §2 (2026-10-13) — INTRA-POLITY GRAIN RELIEF SWITCHED OFF: sim.json
    /// consumption.empireFoodAllocation false, so every settlement eats from its own store alone and a hungry
    /// settlement is never fed from its polity's spare grain. With the batch's other two layers off as well
    /// (<see cref="PreD053"/>) it returns the pre-relief (de86ace) world byte for byte (EmpireFoodAllocationTests'
    /// switch-off controls); on the combined tree, the combined world against this twin first differs exactly at the
    /// first relief (the per-layer proof). Also the config of every rig that
    /// measures a PER-SETTLEMENT food rule (abandonment famine, the starved explain rigs, the revolt harness):
    /// relief removes those regimes only while the polity has spare grain.</summary>
    public static SimConfig PreEmpireFoodAllocation(SimConfig cfg) =>
        cfg with { Consumption = cfg.Consumption with { EmpireFoodAllocation = false } };

    /// <summary>D-051 §6 applied to construction (D-053 §1, the Director's 2026-10-13 instruction, item 1) — THE CONSTRUCTION PURSE SWITCHED OFF:
    /// sim.json construction.empireStockpile false, so a construction project is checked against and paid from its own
    /// settlement's stock alone (ConstructionQuery.Holders = the settlement), exactly as at de86ace. With the batch's other
    /// two layers off as well (<see cref="PreD053"/>) it returns the pre-purse (de86ace) world byte for byte
    /// (EmpireConstructionStockpileTests' attribution controls); on the combined tree, the combined world against this twin
    /// first differs exactly at the delivery of the first construction order the purse changes (the per-layer proof). Roads
    /// are not switched by it (they drew on the civilization's purse before, ADR-032).</summary>
    public static SimConfig PreEmpireConstructionStockpile(SimConfig cfg) =>
        cfg.Construction is { } c ? cfg with { Construction = c with { EmpireStockpile = false } } : cfg;

    /// <summary>M5 item 5 (2026-10-13) — THE GRANARY EFFECTS SWITCHED OFF: sim.json granaries.enabled false, so the local
    /// farm-output factor and the empire-wide famine-intensity factor are the literal 1.0 and neither ProductionSystem nor
    /// DemographicsSystem multiplies by them. With the batch's other two layers off as well (<see cref="PreD053"/>) it
    /// returns the pre-granary-effects (de86ace) pins byte for byte (GranaryAttributionTests); on the combined tree, the
    /// combined world against this twin first differs exactly one step after a granary first acts (the per-layer proof).
    /// A world with no granary is untouched by the switch either way.</summary>
    public static SimConfig PreGranaryEffects(SimConfig cfg) =>
        cfg.Granaries is { } g ? cfg with { Granaries = g with { Enabled = false } } : cfg;

    /// <summary>D-053 (2026-10-13) — THE BATCH OFF: all three of its layers switched off at once —
    /// <see cref="PreEmpireConstructionStockpile"/> (§1, the construction purse), <see cref="PreEmpireFoodAllocation"/> (§2,
    /// intra-polity grain relief) and <see cref="PreGranaryEffects"/> (§3, the granary effects). It returns the base commit
    /// de86ace's world byte for byte (each track's switch-off control keeps its de86ace literal on it), and it is what
    /// every OLDER attribution twin strips (the R4 rule: an older layer twin strips every newer layer — PreForager and its
    /// chain, PreTimeAveragedHarvest, PreTaxAge, PreAiFounding). Which layer moved a pin is the per-layer proof beside each
    /// switch-off control: the combined world against the combined world with only that layer off, first differing exactly
    /// at the turn that layer first acts. By the same R4 rule it strips the NEWER D-054 batch too (<see cref="PreD054Sim"/>:
    /// incremental construction, WS1), so every pre-D-053 twin is a pre-D-054 twin as well.</summary>
    public static SimConfig PreD053(SimConfig cfg) =>
        PreGranaryEffects(PreEmpireFoodAllocation(PreEmpireConstructionStockpile(PreD054Sim(cfg))));

    /// <summary>D-054 WS1 (2026-10-14) — INCREMENTAL CONSTRUCTION SWITCHED OFF: sim.json construction.incremental false, so
    /// the queue head resolves by M4-D's whole-or-nothing gate exactly as in batch 2 (ConstructionSystem's "THE SWITCH OFF"),
    /// no progress row is written, the AI neither cancels stalled heads nor applies its WS1 ordering checks, and the
    /// construction blocker keeps its per-turn labour clause. It also switches off WS2 (the building/roads split, which
    /// needs incremental construction: the loader refuses allocationRatios without it). With it, a world's v32 projection
    /// (SchemaProjection.HashAtSchemaV32) returns the batch-2 references byte for byte. D-055 (2026-10-16): it switches
    /// off reservations too (the newer layer, the R4 rule; the loader refuses reservations without incremental), so it
    /// still returns them exactly as before.</summary>
    public static SimConfig PreIncrementalConstruction(SimConfig cfg)
    {
        cfg = PreM6(cfg);   // M6 (2026-10-22): the R4 rule — both newer M6 layers off (Track EX discovery, Track CP)
        return cfg.Construction is { } c
            ? cfg with { Construction = c with { Incremental = false, AllocationRatios = false, Reservations = false, ContinueWithinTurn = false } }
            : cfg;
    }

    /// <summary>M6 Track EX (D-062, ADR-044; 2026-10-22) — DISCOVERY AND FOG SWITCHED OFF: sim.json discovery.enabled and
    /// fog.enabled false, so no deposit is concealed (every consumer reads DepositRow.Abundance), PolityKnowledge.Of answers
    /// all-known, the founding known-extent arm never fires, Explore and Prospect are refused, the AI issues neither, and
    /// DiscoverySystem writes nothing — sections 63–66 stay EMPTY. A world stepped under it is the pre-M6 (v34) world plus
    /// four empty count prefixes, so its v34 projection (<see cref="SchemaProjection.V34"/>) is the v34 hash byte for byte:
    /// every pin the slice moves is attributed through it. A single-layer twin; by the R4 rule every OLDER twin strips it
    /// too (<see cref="PreIncrementalConstruction"/>, <see cref="PreReservations"/>, and through them PreD054Sim, PreD053
    /// and every twin built on it).</summary>
    public static SimConfig PreDiscovery(SimConfig cfg)
    {
        if (cfg.Discovery is { } d) cfg = cfg with { Discovery = d with { Enabled = false } };
        if (cfg.Fog is { } f) cfg = cfg with { Fog = f with { Enabled = false } };
        return cfg;
    }

    /// <summary>M6 (D-062; 2026-10-22) — THE WHOLE M6 SLICE OFF: Track EX (<see cref="PreDiscovery"/>: sections 63–66 stay
    /// empty) and Track CP (<see cref="PreTrackCp"/>: section 62 stays empty, the v34 research content). A world stepped under
    /// it is the v34 world plus the five empty v35 count prefixes, so its v34 projection (<see cref="SchemaProjection.V34"/>,
    /// which strips and asserts empty sections 62–66) is the v34 hash byte for byte. Every OLDER chained twin strips it (the
    /// R4 rule), and every pre-M6 attribution rig runs on it.</summary>
    public static SimConfig PreM6(SimConfig cfg) => PreTrackCp(PreDiscovery(cfg));

    /// <summary>D-054 WS2 (2026-10-14) — THE BUILDING/ROADS SPLIT SWITCHED OFF: sim.json construction.allocationRatios
    /// false, so kind-12 orders are ignored at delivery, no ResourceAllocationRow is written, and building work goes first
    /// with road works getting what it leaves (WS1 alone). A world whose Empires never set a split (every AI world, and
    /// every world with no kind-12 order) is byte-identical with it on or off: the default b = 100 cannot bind. D-055: it
    /// keeps reservations ON — the recorded R4 exception (the class header).</summary>
    public static SimConfig PreAllocationRatios(SimConfig cfg) =>
        cfg.Construction is { } c ? cfg with { Construction = c with { AllocationRatios = false } } : cfg;

    /// <summary>D-055 (2026-10-16; the Director's batch-4 instruction, item 1) — RESERVATIONS SWITCHED OFF: sim.json
    /// construction.reservations false, so construction clears ConstructionHolds every step and publishes nothing, building
    /// demand reverts to the heads' draws only, and road development subtracts nothing — the batch-3 rule exactly. A world
    /// stepped under it is batch 3's world plus the empty section-61 count prefix, so its v33 projection
    /// (SchemaProjection.V33) is the batch-3 hash byte for byte: every pin a hold moves is attributed through it. A
    /// single-layer twin (only the reservations switch changes). D-058 (2026-10-19): by the R4 rule (an older layer twin
    /// strips every newer layer) it also switches off the newer continueWithinTurn layer, so a PreReservations world is
    /// still batch 3's byte for byte.</summary>
    public static SimConfig PreReservations(SimConfig cfg)
    {
        cfg = PreM6(cfg);   // M6 (2026-10-22): the R4 rule — both newer M6 layers off (Track EX discovery, Track CP)
        return cfg.Construction is { } c ? cfg with { Construction = c with { Reservations = false, ContinueWithinTurn = false } } : cfg;
    }

    /// <summary>D-058 (2026-10-19; the Director's ruling "build one at a time … 7 built, 1 under construction, 1 not
    /// started") — CONTINUATION WITHIN THE TURN SWITCHED OFF: sim.json construction.continueWithinTurn false, so a completing
    /// head's leftover capacity never reaches the next entry of its queue in the same step (the promoted entry is a head from
    /// the NEXT step) — the batch-4 rule exactly. A SINGLE-LAYER twin (only this switch changes): every pin D-058 moves is
    /// attributed through it.</summary>
    // ADR-043 (2026-10-22): deliberately NOT R4-chained to Track CP — it is D-058's SINGLE-LAYER twin, compared turn by turn
    // with the live run (AllocationScenarioD058AttributionTests: hash-identical through world 118), so only its own switch
    // may differ. The chained twins (PreReservations, PreIncrementalConstruction and older) strip Track CP.
    public static SimConfig PreContinueWithinTurn(SimConfig cfg) =>
        cfg.Construction is { } c ? cfg with { Construction = c with { ContinueWithinTurn = false } } : cfg;

    /// <summary>D-054 (2026-10-14) — THE SIMCONFIG HALF OF THE BATCH OFF: WS1 and WS2 switched off. The worldgen half (the
    /// cylinder, WS3) is <see cref="PreD054Worldgen(WorldgenConfig)"/>: PreD054 is a PAIR, and a control that founds a world
    /// uses both (<see cref="FoundPreD054"/>). Every older SimConfig twin strips this half (<see cref="PreD053"/>).</summary>
    public static SimConfig PreD054Sim(SimConfig cfg) => PreAllocationRatios(PreIncrementalConstruction(cfg));

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
    // ======================================================================
    // ADR-043 (D-062, 2026-10-22) — TRACK CP: THE RESEARCH STAGE GATE AND CIVICS POINTS. Two content layers of
    // research.json, each with its own single-layer twin, and the pair (the v34 research content exactly).
    // ======================================================================

    /// <summary>ADR-043 — CIVICS POINTS SWITCHED OFF: research.json tuning.civicsPoints removed, so the engine is the v34
    /// ONE-POOL model (one target in either tree, funded by RP; no CivicsPoints row is ever written). A single-layer twin:
    /// the stage gate stays as shipped.</summary>
    public static SimConfig PreCivicsPoints(SimConfig cfg) => cfg.Research is null ? cfg : cfg with { Research = NoCivicsPoints.Value };

    /// <summary>ADR-043 — THE OLD RESEARCH STAGE: researchStage.requires with the civic atom legal_code_roman restored
    /// (the v34 gate). A single-layer twin: Civics Points stay on.</summary>
    public static SimConfig PreStageGate(SimConfig cfg) => cfg.Research is null ? cfg : cfg with { Research = OldStage.Value };

    /// <summary>ADR-043 — TRACK CP OFF: both layers at once (the v34 research content byte for byte in behaviour), so a
    /// world stepped under it is the v34 world plus the empty section-62 count prefix — its v34 projection
    /// (SchemaProjection.V34) is the v34 hash. Every OLDER chained twin strips it (the R4 rule).</summary>
    public static SimConfig PreTrackCp(SimConfig cfg) => cfg.Research is null ? cfg : cfg with { Research = V34Research.Value };

    /// <summary>The v34 stage expression (ADR-043 dropped its last atom).</summary>
    public const string V34StageRequires = "medicine_hippocratic AND (geometry_axiomatic OR algebra) AND stamp_seal AND legal_code_roman";

    /// <summary>ADR-043: undoes Track CP's research.json changes on a parsed document — <paramref name="civics"/> removes
    /// tuning.civicsPoints, <paramref name="stage"/> restores the v34 stage expression.</summary>
    public static void UndoTrackCp(System.Text.Json.Nodes.JsonObject doc, bool civics, bool stage)
    {
        if (civics)
        {
            doc["tuning"]!.AsObject().Remove("civicsPoints");
            doc["tuning"]!.AsObject().Remove("_civicsPoints");
        }
        if (stage) doc["researchStage"]!["requires"] = V34StageRequires;
    }

    private static global::Sim.Core.Systems.Research.ResearchContent LoadResearchVariant(bool civics, bool stage)
    {
        using var stream = global::Sim.Data.DataFiles.OpenResearch();
        var doc = System.Text.Json.Nodes.JsonNode.Parse(stream)!.AsObject();
        UndoTrackCp(doc, civics, stage);
        using var goods = global::Sim.Data.DataFiles.OpenGoods();
        return global::Sim.Core.Systems.Research.ResearchContentLoader.Load(doc.ToJsonString(), GoodsConfigLoader.Load(goods));
    }

    private static readonly Lazy<global::Sim.Core.Systems.Research.ResearchContent> NoCivicsPoints = new(() => LoadResearchVariant(true, false));
    private static readonly Lazy<global::Sim.Core.Systems.Research.ResearchContent> OldStage = new(() => LoadResearchVariant(false, true));
    private static readonly Lazy<global::Sim.Core.Systems.Research.ResearchContent> V34Research = new(() => LoadResearchVariant(true, true));

    public static SimConfig PreTradeKnowledge(SimConfig cfg) =>
        PreForager(cfg) with { Research = PreR2aResearch.Value, Trade = PreForager(cfg).Trade with { Entity = null } };

    /// <summary>ADR-043: the pre-R2a research content WITH Track CP kept as shipped — what a SINGLE-LAYER trade twin of a
    /// two-lane config carries (SingleSwitchTwins.PreTradeKnowledgeOnly), so only the R2a layer differs.</summary>
    public static global::Sim.Core.Systems.Research.ResearchContent PreR2aResearchKeepingTrackCp() => PreR2aResearchWithCp.Value;

    private static readonly Lazy<global::Sim.Core.Systems.Research.ResearchContent> PreR2aResearchWithCp = new(() => LoadPreR2a(undoTrackCp: false));

    private static readonly Lazy<global::Sim.Core.Systems.Research.ResearchContent> PreR2aResearch = new(() => LoadPreR2a(undoTrackCp: true));

    private static global::Sim.Core.Systems.Research.ResearchContent LoadPreR2a(bool undoTrackCp)
    {
        using var stream = global::Sim.Data.DataFiles.OpenResearch();
        var doc = System.Text.Json.Nodes.JsonNode.Parse(stream)!.AsObject();
        var techs = doc["technologies"]!.AsArray();
        for (int i = techs.Count - 1; i >= 0; i--) if ((string?)techs[i]!["id"] == "trade") techs.RemoveAt(i);
        var ents = doc["entities"]!.AsArray();
        for (int i = ents.Count - 1; i >= 0; i--) if ((string?)ents[i]!["id"] == "activity.trade") ents.RemoveAt(i);
        doc["tuning"]!.AsObject().Remove("cityStatePaceFraction");
        if (undoTrackCp) UndoTrackCp(doc, civics: true, stage: true);   // ADR-043: the R4 rule — an older twin strips every newer layer
        using var goods = global::Sim.Data.DataFiles.OpenGoods();
        using var sim = global::Sim.Data.DataFiles.OpenSim();
        using var needs = global::Sim.Data.DataFiles.OpenNeeds();
        SimConfig plain = SimConfigLoader.Load(sim, needs, goods);
        return global::Sim.Core.Systems.Research.ResearchContentLoader.Load(doc.ToJsonString(), plain.Goods);
    }

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

    /// <summary>The raw canonical worldgen.json text (for loader-rejection tests).</summary>
    public static string WorldgenJson()
    {
        using var stream = global::Sim.Data.DataFiles.OpenWorldgen();
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

    // ======================================================================
    // BATCH 3 (D-054) — THE WORLDGEN HALF OF THE ATTRIBUTION CHAIN. WS3's switch lives in worldgen.json
    // (topology.wrapX), not sim.json, so no SimConfig twin can strip it: a test that FOUNDS a world must strip it
    // on the WorldgenConfig it founds with. PreD054 is therefore a PAIR — this worldgen half, and the SimConfig
    // half (the construction track's PreD054Sim, which the older SimConfig twins strip, the R4 rule). Every
    // attribution control that founds a world founds it through PreD054Worldgen (or FoundPreD054), so with both
    // halves off (and the v32 schema projection) it returns the batch-2 pins byte for byte.
    // ======================================================================

    /// <summary>Batch 3 WS3 — THE CYLINDER SWITCHED OFF: worldgen.json topology removed (absent = the flat square
    /// raster). Worldgen.Generate then takes the pre-batch-3 code path at every stage — the four-sided edge taper,
    /// the non-periodic noise (x / (size − 1)), border sinks on all four edges, no x wrap anywhere — so the terrain's
    /// bytes and ContentHash are the pre-batch-3 ones, and so is every world founded on it
    /// (WrapAttributionTests pins it).</summary>
    public static WorldgenConfig PreCylinderWorldgen(WorldgenConfig cfg) => cfg with { Topology = null };

    /// <summary>D-054 (batch 3) — the WORLDGEN half of the batch-off pair: <see cref="PreCylinderWorldgen"/> (WS3 is
    /// the batch's only worldgen layer). Pair it with the sim half on every attribution control that founds a world.</summary>
    public static WorldgenConfig PreD054Worldgen(WorldgenConfig cfg) => PreCylinderWorldgen(cfg);

    /// <summary>The canonical worldgen with the batch-3 worldgen layer stripped (<see cref="PreD054Worldgen(WorldgenConfig)"/>
    /// of <see cref="Worldgen"/>).</summary>
    public static WorldgenConfig PreD054Worldgen() => PreD054Worldgen(Worldgen());

    /// <summary>
    /// D-054 — founds a world with the WHOLE batch off, BOTH halves of the pair (integration, 2026-10-14):
    /// <see cref="Sim.Core.Worldgen.WorldFounding.Found"/> on <see cref="PreD054Worldgen(WorldgenConfig)"/> of
    /// <paramref name="worldgen"/> (default: the canonical config) with <see cref="PreD054Sim"/> of <paramref name="sim"/>
    /// (idempotent: PreD053 and the older twins already strip it). The caller runs the pipeline with the same pair
    /// (<see cref="PreD054Sim"/> of its config, and a worldgen whose topology is off — only Worldgen.Generate reads it).
    /// </summary>
    public static global::Sim.Core.State.WorldState FoundPreD054(
        ulong seed, SimConfig sim, WorldgenConfig? worldgen = null, int? settlementsOverride = null) =>
        global::Sim.Core.Worldgen.WorldFounding.Found(PreD054Worldgen(worldgen ?? Worldgen()), PreD054Sim(sim), seed, settlementsOverride);
}
