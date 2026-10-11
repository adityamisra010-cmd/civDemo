using System.Collections.Immutable;
using System.Globalization;
using System.Text;
using System.Text.Json;
using Sim.Core.State;
using Sim.Core.Systems;
using Sim.Core.Systems.Research;

namespace Sim.Ui.ViewModel;

/// <summary>One sector whose researched identity, when the simulation's yield reads it, opens an overlay layer
/// (UI content <c>sectorLayers</c>): the research.json <c>sectorActivities[].sector</c> name and the P9 overlay layer key.</summary>
public sealed record SectorLayer(string Sector, string Layer);

/// <summary>
/// M5 map workstream P10 — the 'now usable' shortcut's UI CONTENT (<c>Sim.Ui/UiContent/discovery-shortcuts.json</c>):
/// how many turns a shortcut is offered (<see cref="ExpiryTurns"/>, a documented TUNE value) and which overlay layer a
/// sector's researched identity opens (<see cref="SectorLayers"/>). Presentation only — nothing in the simulation reads it.
/// </summary>
public sealed record DiscoveryShortcutContent(int ExpiryTurns, IReadOnlyList<SectorLayer> SectorLayers);

/// <summary>What became usable: a crafting recipe (its research entity is goods.json <c>recipes[].entity</c>), or a
/// sector's researched activity identity (research.json <c>sectorActivities</c>).</summary>
public enum NowUsableKind { Recipe = 1, Activity = 2 }

/// <summary>
/// ONE 'NOW USABLE' SHORTCUT, as the step that fired it found it: the clock of the state that step wrote
/// (<see cref="Turn"/>; PlayerTurn — the number of the turn just played), the research entity whose knowledge eligibility
/// the player's completed knowledge gained (<see cref="Entity"/> index, <see cref="EntityId"/>, its content name), the
/// deposit goods it made usable (none for an activity: it is the land), the P9 overlay layer keys it opens, the clock
/// turn it is gone at (<see cref="ExpiresAt"/> = Turn + the content's expiryTurns) and its text: a headline and the lines
/// that keep PERMISSION TO USE (what fired) apart from KNOWLEDGE OF THE RESOURCE (not simulated). Self-contained: the
/// history is a record of what each step found, rebuilt by replay, never re-derived from a later world.
/// </summary>
public sealed record NowUsable(
    long Turn, int Entity, string EntityId, string Name, NowUsableKind Kind,
    ImmutableArray<int> Goods, ImmutableArray<string> GoodNames, ImmutableArray<string> Layers, long ExpiresAt,
    string Headline, string Permission, string? Extraction, string Knowledge)
{
    /// <summary>Whether the shortcut is offered on a world whose clock reads <paramref name="clockTurn"/>: from the turn
    /// it was stamped until the clock reaches <see cref="ExpiresAt"/> — turn-exact, gone AT ExpiresAt.</summary>
    public bool ActiveAt(long clockTurn) => clockTurn >= Turn && clockTurn < ExpiresAt;

    /// <summary>The explanation, in order: the permission, the extraction note (a recipe), the knowledge note.</summary>
    public IReadOnlyList<string> Lines => Extraction is { } x ? [Permission, x, Knowledge] : [Permission, Knowledge];

    /// <summary>The entry as one canonical line (what the resume-rebuild test compares).</summary>
    public string Canonical()
    {
        var sb = new StringBuilder();
        sb.Append(Turn.ToString(CultureInfo.InvariantCulture)).Append('|').Append(Entity.ToString(CultureInfo.InvariantCulture))
            .Append('|').Append(EntityId).Append('|').Append(Name).Append('|').Append((int)Kind);
        sb.Append("|g=");
        for (int i = 0; i < Goods.Length; i++) sb.Append(i > 0 ? "," : "").Append(Goods[i].ToString(CultureInfo.InvariantCulture)).Append(':').Append(GoodNames[i]);
        sb.Append("|l=").Append(string.Join(",", Layers));
        sb.Append("|x=").Append(ExpiresAt.ToString(CultureInfo.InvariantCulture));
        sb.Append('|').Append(Headline).Append('|').Append(Permission).Append('|').Append(Extraction ?? "").Append('|').Append(Knowledge);
        return sb.ToString();
    }

    /// <summary>Value equality, the arrays compared element by element (ImmutableArray's own is by reference).</summary>
    public bool Equals(NowUsable? other) => other is not null && string.Equals(Canonical(), other.Canonical(), StringComparison.Ordinal);

    public override int GetHashCode() => HashCode.Combine(Turn, Entity); // gate:allow-gethashcode — equality plumbing, never logic input
}

/// <summary>
/// M5 map workstream P10 — THE 'NOW USABLE' HISTORY: every shortcut the session's steps fired, oldest first. Kept in
/// <see cref="UiSession"/> and fed by its Step — the one step live play and Resume's replay share — so the history is a
/// pure function of the order log: a resumed session rebuilds it identically (UI-side history in the D-028 sense, like
/// the annals; it never touches the world and no hash moves).
/// </summary>
public sealed class NowUsableHistory(DiscoveryShortcutContent content)
{
    private readonly List<NowUsable> _entries = [];

    /// <summary>The UI content the history was built with.</summary>
    public DiscoveryShortcutContent Content { get; } = content ?? throw new ArgumentNullException(nameof(content));

    /// <summary>Every shortcut fired, oldest first (within one step, in research entity order).</summary>
    public IReadOnlyList<NowUsable> Entries => _entries;

    /// <summary>Appends the shortcuts the step <paramref name="prev"/> → <paramref name="next"/> fired for
    /// <paramref name="player"/> (<see cref="DiscoveryShortcuts.Transitions"/>).</summary>
    public void Observe(IReadOnlyWorldState prev, IReadOnlyWorldState next, SimConfig cfg, PolityId player) =>
        _entries.AddRange(DiscoveryShortcuts.Transitions(prev, next, cfg, Content, player));

    /// <summary>The shortcuts offered on a world whose clock reads <paramref name="clockTurn"/>, oldest first, each with its
    /// index in <see cref="Entries"/>.</summary>
    public IReadOnlyList<(int Index, NowUsable Entry)> Active(long clockTurn)
    {
        var r = new List<(int, NowUsable)>();
        for (int i = 0; i < _entries.Count; i++) if (_entries[i].ActiveAt(clockTurn)) r.Add((i, _entries[i]));
        return r;
    }

    /// <summary>The history as canonical lines, oldest first.</summary>
    public IReadOnlyList<string> CanonicalLines()
    {
        var r = new List<string>(_entries.Count);
        foreach (NowUsable e in _entries) r.Add(e.Canonical());
        return r;
    }
}

/// <summary>
/// M5 map workstream P10 — THE RESOURCE 'NOW USABLE' SHORTCUT'S TRIGGER (the Director's default for workstream part 6:
/// the simulation has NO research that unlocks or reveals a resource — extraction is ungated, ADR-033 D1 — so the
/// shortcut fires on a REAL eligibility transition only). Pure and read-only: no state, no cache, no write.
///
/// <para>A step FIRES a shortcut for the player when the player's completed knowledge (ResearchQuery.CompletedMask) makes
/// a content-declared entity knowledge-eligible that it did not make eligible before the step, AND the simulation's own
/// predicate for that entity's effect gains at least one settlement the player holds — so the shortcut never claims a
/// change the economy is not making:</para>
/// <list type="bullet">
/// <item>a RECIPE: a goods.json recipe linked to a research entity (<c>recipes[].entity</c>) whose knowledge
///   (CraftingQuery.IsKnown — the one recipe knowledge predicate ProductionSystem computes with, over the settlement's
///   knowledge, CraftingQuery.KnowledgeOf) it gains, among whose inputs are DEPOSIT goods (goods.json
///   <c>depositChannel</c>) that no recipe the player knew before the step consumed: those goods are what is NOW USABLE,
///   and their channels are the overlay layers opened (a good some known recipe already consumed was usable before and
///   is not claimed; a recipe all of whose deposit inputs were in use fires nothing);</item>
/// <item>an ACTIVITY: a sector's researched identity (research.json <c>sectorActivities</c>) for a sector the UI content
///   maps to a layer (<c>sectorLayers</c>), when the production yield reads it — the farming sector's, through
///   LabourActivities.HarvestsWildFood (sim.json farming.preCultivation on): a settlement the player holds stops
///   harvesting wild food. Every other sector's identity is a label only (ADR-033 D1: no research gates herding or
///   extraction) and never fires, whatever the content maps.</item>
/// </list>
/// <para>No research node or entity id is named here: every entity comes from goods.json and research.json, every layer
/// from the UI content and goods.json channels. The text says 'now usable', never 'discovered', and keeps two things
/// apart: PERMISSION TO USE (the eligibility that fired) and KNOWLEDGE OF THE RESOURCE (not simulated — every deposit is
/// known wherever its ground is known).</para>
/// <para>Deterministic: recipes in goods.json order, sectors in content order, the result in research entity order (ties
/// cannot occur: an entity fires at most once per step); no dictionary, no LINQ, invariant formatting.</para>
/// </summary>
public static class DiscoveryShortcuts
{
    /// <summary>The embedded UI content's resource name.</summary>
    public const string ContentResource = "Sim.Ui.discovery-shortcuts.json";

    /// <summary>The verbatim knowledge statement every shortcut carries.</summary>
    public const string KnowledgeStatement =
        "Knowledge of the resource: not simulated - every deposit is known wherever its ground is known";

    /// <summary>The label a shortcut is offered under.</summary>
    public const string Label = "now usable";

    private static DiscoveryShortcutContent? _content;

    /// <summary>The shipped UI content (loaded once from the embedded <c>discovery-shortcuts.json</c>).</summary>
    public static DiscoveryShortcutContent Content
    {
        get
        {
            if (_content is { } c) return c;
            using Stream s = typeof(DiscoveryShortcuts).Assembly.GetManifestResourceStream(ContentResource)
                ?? throw new InvalidOperationException("the embedded UI content " + ContentResource + " is missing");
            return _content = Load(s);
        }
    }

    /// <summary>Parses and validates the UI content: <c>expiryTurns</c> at least 1; every <c>sectorLayers</c> entry names a
    /// sector and a layer, no sector twice.</summary>
    public static DiscoveryShortcutContent Load(Stream json)
    {
        ArgumentNullException.ThrowIfNull(json);
        using JsonDocument doc = JsonDocument.Parse(json);
        JsonElement root = doc.RootElement;
        if (!root.TryGetProperty("expiryTurns", out JsonElement e) || e.ValueKind != JsonValueKind.Number || !e.TryGetInt32(out int n))
            throw new InvalidDataException("discovery-shortcuts.json: expiryTurns (a whole number of turns) is required");
        if (n < 1) throw new InvalidDataException("discovery-shortcuts.json: expiryTurns must be at least 1, not " + n.ToString(CultureInfo.InvariantCulture));
        var layers = new List<SectorLayer>();
        if (root.TryGetProperty("sectorLayers", out JsonElement arr))
        {
            if (arr.ValueKind != JsonValueKind.Array) throw new InvalidDataException("discovery-shortcuts.json: sectorLayers must be an array");
            foreach (JsonElement item in arr.EnumerateArray())
            {
                string? sector = item.TryGetProperty("sector", out JsonElement s) && s.ValueKind == JsonValueKind.String ? s.GetString() : null;
                string? layer = item.TryGetProperty("layer", out JsonElement l) && l.ValueKind == JsonValueKind.String ? l.GetString() : null;
                if (string.IsNullOrEmpty(sector) || string.IsNullOrEmpty(layer))
                    throw new InvalidDataException("discovery-shortcuts.json: every sectorLayers entry needs a sector and a layer");
                foreach (SectorLayer x in layers)
                    if (string.Equals(x.Sector, sector, StringComparison.Ordinal))
                        throw new InvalidDataException("discovery-shortcuts.json: sector '" + sector + "' is listed twice");
                layers.Add(new SectorLayer(sector, layer));
            }
        }
        return new DiscoveryShortcutContent(n, layers);
    }

    /// <summary>
    /// The shortcuts the step <paramref name="prev"/> → <paramref name="next"/> fires for <paramref name="player"/> (see the
    /// type's remarks), in research entity order, each stamped <c>next.Clock.Turn</c>. Empty when the player's completed
    /// knowledge did not change.
    /// </summary>
    public static IReadOnlyList<NowUsable> Transitions(
        IReadOnlyWorldState prev, IReadOnlyWorldState next, SimConfig cfg, DiscoveryShortcutContent ui, PolityId player)
    {
        ArgumentNullException.ThrowIfNull(prev);
        ArgumentNullException.ThrowIfNull(next);
        ArgumentNullException.ThrowIfNull(cfg);
        ArgumentNullException.ThrowIfNull(ui);
        if (cfg.Research is not { } research) return [];
        if (ResearchQuery.CompletedBetween(prev, next, player).Length == 0) return [];   // no knowledge gained: no transition
        bool[] before = ResearchQuery.CompletedMask(prev, research, player);
        bool[] after = ResearchQuery.CompletedMask(next, research, player);
        long turn = next.Clock.Turn, expires = turn + ui.ExpiryTurns;
        var fired = new List<NowUsable>();

        // RECIPES — goods.json order.
        if (cfg.Goods is { } goods)
            foreach (RecipeEntry recipe in goods.Recipes)
            {
                if (recipe.Entity is null) continue;   // no knowledge requirement: it never transitions
                int entity = research.EntityIndexOf(recipe.Entity);
                if (entity < 0) continue;              // an entity the content does not define is never known
                if (CraftingQuery.IsKnown(research, recipe, before) || !CraftingQuery.IsKnown(research, recipe, after)) continue;
                var fresh = new List<GoodEntry>();
                foreach (RecipeInput input in recipe.Inputs)
                {
                    int id = goods.IdOf(input.Good);
                    if (id < 0) continue;
                    GoodEntry g = goods.ById(id);
                    if (g.DepositChannel is null || fresh.Contains(g) || ConsumedByKnownRecipe(goods, research, before, g.Name)) continue;
                    fresh.Add(g);
                }
                if (fresh.Count == 0) continue;        // every deposit input was already in use: nothing is newly usable
                RecipeEntry r = recipe;
                if (!AnyHeldSettlementGains(prev, next, player,
                        (w, s) => CraftingQuery.IsKnown(research, r, CraftingQuery.KnowledgeOf(w, research, s)))) continue;
                fired.Add(RecipeShortcut(turn, expires, research.Entities[entity], recipe, fresh));
            }

        // ACTIVITIES — the sectors the UI content maps, in its order.
        foreach (SectorLayer map in ui.SectorLayers)
        {
            SectorActivityContent? sector = null;
            foreach (SectorActivityContent s in research.SectorActivities)
                if (string.Equals(s.SectorId, map.Sector, StringComparison.Ordinal)) { sector = s; break; }
            if (sector is null) continue;
            foreach (SectorResearchedIdentity identity in sector.Researched)
            {
                if (ResearchQuery.IsKnowledgeEligible(research, identity.Entity, before)
                    || !ResearchQuery.IsKnowledgeEligible(research, identity.Entity, after)) continue;
                if (!YieldReadsKnowledge(prev, next, cfg, player, sector.Sector)) continue;
                fired.Add(ActivityShortcut(turn, expires, research.Entities[identity.Entity], cfg, map.Layer));
            }
        }

        // Research entity order (an entity fires at most once per step, so the key is unique).
        fired.Sort((a, b) => a.Entity.CompareTo(b.Entity));
        return fired;
    }

    /// <summary>
    /// The research entities a shortcut CAN come from under this content (entity order): the research entity of every
    /// goods.json recipe with a deposit input, and every researched identity of a sector the UI content maps. Whether one
    /// fires is still decided by <see cref="Transitions"/> (the transition, the newly usable goods, the yield predicate).
    /// </summary>
    public static IReadOnlyList<int> SourceEntities(SimConfig cfg, DiscoveryShortcutContent ui)
    {
        ArgumentNullException.ThrowIfNull(cfg);
        ArgumentNullException.ThrowIfNull(ui);
        var r = new List<int>();
        if (cfg.Research is not { } research) return r;
        if (cfg.Goods is { } goods)
            foreach (RecipeEntry recipe in goods.Recipes)
            {
                if (recipe.Entity is null || research.EntityIndexOf(recipe.Entity) is not (>= 0 and var e) || r.Contains(e)) continue;
                foreach (RecipeInput input in recipe.Inputs)
                    if (goods.IdOf(input.Good) is >= 0 and var id && goods.ById(id).DepositChannel is not null) { r.Add(e); break; }
            }
        foreach (SectorLayer map in ui.SectorLayers)
            foreach (SectorActivityContent s in research.SectorActivities)
                if (string.Equals(s.SectorId, map.Sector, StringComparison.Ordinal))
                    foreach (SectorResearchedIdentity id in s.Researched) if (!r.Contains(id.Entity)) r.Add(id.Entity);
        r.Sort();
        return r;
    }

    /// <summary>Whether a recipe the player's knowledge <paramref name="known"/> makes known consumes the good named
    /// <paramref name="good"/> (CraftingQuery.IsKnown: a recipe with no entity link, or a null requirement, is known).</summary>
    public static bool ConsumedByKnownRecipe(GoodsConfig goods, ResearchContent research, bool[] known, string good)
    {
        foreach (RecipeEntry r in goods.Recipes)
        {
            if (!CraftingQuery.IsKnown(research, r, known)) continue;
            foreach (RecipeInput i in r.Inputs) if (string.Equals(i.Good, good, StringComparison.Ordinal)) return true;
        }
        return false;
    }

    /// <summary>Whether the simulation's yield reads the sector's researched identity and it changed for a settlement the
    /// player holds: for the farming sector, LabourActivities.HarvestsWildFood — the ONE predicate ProductionSystem.Farm's
    /// yield choice reads — is true before the step and false after. No other sector's output reads its identity (ADR-033
    /// D1), so no other sector fires.</summary>
    public static bool YieldReadsKnowledge(IReadOnlyWorldState prev, IReadOnlyWorldState next, SimConfig cfg, PolityId player, int sector) =>
        sector == Sectors.Farming
        && AnyHeldSettlementGains(prev, next, player, (w, s) => !LabourActivities.HarvestsWildFood(w, cfg, s));

    /// <summary>Whether <paramref name="holds"/> is false before the step and true after it for at least one settlement the
    /// player holds after it (LabourActivities.ControlledSettlements, table order).</summary>
    private static bool AnyHeldSettlementGains(IReadOnlyWorldState prev, IReadOnlyWorldState next, PolityId player,
        Func<IReadOnlyWorldState, SettlementId, bool> holds)
    {
        foreach (SettlementId s in LabourActivities.ControlledSettlements(next, player))
            if (!holds(prev, s) && holds(next, s)) return true;
        return false;
    }

    private static NowUsable RecipeShortcut(long turn, long expires, ResearchEntity entity, RecipeEntry recipe, List<GoodEntry> fresh)
    {
        string name = NameOf(entity);
        var ids = ImmutableArray.CreateBuilder<int>(fresh.Count);
        var names = ImmutableArray.CreateBuilder<string>(fresh.Count);
        var layers = ImmutableArray.CreateBuilder<string>();
        foreach (GoodEntry g in fresh)
        {
            ids.Add(g.Id);
            names.Add(g.Name);
            if (!layers.Contains(g.DepositChannel!)) layers.Add(g.DepositChannel!);
        }
        string list = string.Join(", ", names);
        string where = recipe.Requires is { Length: > 0 } req ? " where " + req : " wherever crafting labour works";
        return new NowUsable(turn, entity.Index, entity.Id, name, NowUsableKind.Recipe, ids.MoveToImmutable(), names.MoveToImmutable(),
            layers.ToImmutable(), expires,
            Headline: list + " " + Label + " (" + name + ")",
            Permission: "Permission to use: " + name + " became known (your research) - it consumes " + list + where,
            Extraction: "Extraction needs no research: " + list + (fresh.Count == 1 ? " has" : " have") + " been worked since founding",
            Knowledge: KnowledgeStatement);
    }

    private static NowUsable ActivityShortcut(long turn, long expires, ResearchEntity entity, SimConfig cfg, string layer)
    {
        string name = NameOf(entity);
        double cultivated = cfg.Farming.YieldPerArableKm2PerYear;
        double wild = cfg.Farming.PreCultivation?.YieldPerArableKm2PerYear ?? cultivated;
        string ratio = wild > 0.0 ? " (x" + Num(cultivated / wild) + ")" : "";
        return new NowUsable(turn, entity.Index, entity.Id, name, NowUsableKind.Activity, [], [], [layer], expires,
            Headline: name + " " + Label + ": fertile land yields more food" + ratio,
            Permission: "Permission to use: " + name + " became known (your research) - your farmland yields " + Num(cultivated)
                + " instead of " + Num(wild) + " food per arable km2 a year from your next turn",
            Extraction: null,
            Knowledge: KnowledgeStatement);
    }

    private static string NameOf(ResearchEntity e) => e.Name is { Length: > 0 } n ? n : e.Id;

    private static string Num(double v) => v.ToString("0.##", CultureInfo.InvariantCulture);
}
