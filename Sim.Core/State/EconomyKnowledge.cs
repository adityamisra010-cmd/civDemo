using System.Collections.Concurrent;
using System.Runtime.CompilerServices;
using Sim.Core.Systems;
using Sim.Core.Systems.ClassMobility;
using Sim.Core.Systems.Research;

namespace Sim.Core.State;

/// <summary>
/// ADR-056 (the M7 economy track, D-065 R2) — THE KNOWLEDGE GATE OF THE ECONOMY PACKETS' CONTENT, authored in goods.json
/// rather than research.json. TC-1 keeps the research trees closed: no node and no entity is added for iron, steel,
/// horses or gunpowder. A recipe or a good therefore names EXISTING node ids in a <c>knowledge</c> expression, in the
/// research dialect (ADR-033 D4: node ids, AND/OR, no NOT), parsed by <see cref="ResearchContentLoader.ParseRequirement"/>
/// against the attached research content and evaluated by <see cref="ResearchQuery.RequirementMet"/> — the one knowledge
/// evaluator, so a gate in goods.json and an entity's gate in research.json can never disagree about the same knowledge.
///
/// <para><b>Meaning.</b> A recipe's knowledge joins its research entity (CraftingQuery.IsKnown): both must hold. A
/// DEPOSIT good's knowledge gates WORKING it: without it the settlement's extraction labour does not go to that deposit
/// (<see cref="IsWorkable"/>; the row still exists and is still shown — the ore was always there). A HERD good's
/// knowledge gates capture and breeding (ProductionSystem.Breed).</para>
///
/// <para><b>Met only by knowledge.</b> Unlike a base recipe's research entity — which reads "everything is known" where
/// nothing gates (a hand-built world with no control relation, or a config with no research content: the R1 precedent,
/// kept so the pre-R1 economy of those worlds stayed as it was) — a packet gate is NOT met without knowledge to meet it.
/// Packet content postdates knowledge gating and has no ungated past to preserve; letting it run where nothing is known
/// would grant a capability no computed state supports (law 4's reading). An expression the attached content cannot
/// parse (content swapped under the config, e.g. an older research file in an attribution twin) is never met either;
/// the canonical four-file load refuses one outright (SimConfigLoader.ValidateProjectsAgainstContent).</para>
///
/// <para>The parse is cached per research-content instance and expression text — a pure function of the two, so the
/// cache can change no outcome; lookups only, nothing iterates it (law 5).</para>
/// </summary>
public static class EconomyKnowledge
{
    private sealed class Parsed(Predicate? predicate)
    {
        public readonly Predicate? Predicate = predicate;
    }

    private static readonly ConditionalWeakTable<ResearchContent, ConcurrentDictionary<string, Parsed>> Cache = new();

    /// <summary>The parsed gate, or null when <paramref name="research"/> cannot parse it (never met).</summary>
    public static Predicate? Parse(ResearchContent research, string expression)
    {
        ConcurrentDictionary<string, Parsed> byText = Cache.GetValue(research, static _ => new(StringComparer.Ordinal));
        return byText.GetOrAdd(expression, static (text, content) =>
        {
            try { return new Parsed(ResearchContentLoader.ParseRequirement(content, text, "goods.json knowledge")); }
            catch (ResearchContentException) { return new Parsed(null); }
        }, research).Predicate;
    }

    /// <summary>Whether knowledge <paramref name="completed"/> satisfies the gate <paramref name="expression"/>: a null
    /// expression is no gate (true); otherwise only knowledge meets it — null content or a null mask never does.</summary>
    public static bool Met(ResearchContent? research, string? expression, bool[]? completed)
    {
        if (expression is null) return true;
        if (research is null || completed is null) return false;
        return Parse(research, expression) is { } p && ResearchQuery.RequirementMet(research, p, completed);
    }

    /// <summary>Whether a settlement whose knowledge mask is <paramref name="completed"/> may WORK the deposits of good
    /// <paramref name="good"/> (its goods.json <c>knowledge</c>; a good without one — every base good — always).</summary>
    public static bool IsWorkable(GoodsConfig goods, ResearchContent? research, int good, bool[]? completed)
    {
        for (int i = 0; i < goods.Goods.Length; i++)
            if (goods.Goods[i].Id == good) return Met(research, goods.Goods[i].Knowledge, completed);
        return true;
    }

    /// <summary>ADR-056: <paramref name="cfg"/> with every enabled economy packet switched off (GoodsConfig.WithoutPacket)
    /// — the pre-packet rules a session or save recorded before the packets replays under (`sim inspect`,
    /// UiSession.Resume, `--without-economy`).</summary>
    public static SimConfig WithoutEconomy(SimConfig cfg)
    {
        if (cfg.Goods is not { } goods) return cfg;
        foreach (PacketEntry p in goods.Packets ?? [])
            if (goods.IsPacketEnabled(p.Id)) goods = goods.WithoutPacket(p.Id);
        return ReferenceEquals(goods, cfg.Goods) ? cfg : WithGoods(cfg, goods);
    }

    /// <summary>ADR-056 §13: <paramref name="cfg"/> with its goods roster replaced by <paramref name="goods"/> — and the unit
    /// families' recruitment bills re-resolved against it (UnitFamilyContent.ResolvedAgainst), so a bill naming a good of a
    /// switched-off packet falls back to PLANNED (the identity blocked) instead of naming an id the roster lacks. THE one
    /// way a config's goods are swapped after loading (every economy twin, UiFounding's goodsOverride).</summary>
    public static SimConfig WithGoods(SimConfig cfg, GoodsConfig goods) =>
        cfg with { Goods = goods, UnitFamilies = cfg.UnitFamilies?.ResolvedAgainst(goods) };

    /// <summary>Convenience: <see cref="IsWorkable(GoodsConfig, ResearchContent?, int, bool[]?)"/> with the settlement's
    /// knowledge looked up (SettlementKnowledge.MaskOf, the mask recipes read).</summary>
    public static bool IsWorkable(IReadOnlyWorldState world, SimConfig? cfg, SettlementId settlement, int good)
    {
        if (cfg?.Goods is not { } goods) return true;
        string? gate = null;
        for (int i = 0; i < goods.Goods.Length; i++)
            if (goods.Goods[i].Id == good) { gate = goods.Goods[i].Knowledge; break; }
        if (gate is null) return true;
        return Met(cfg.Research, gate, SettlementKnowledge.MaskOf(world, cfg.Research, settlement));
    }
}
