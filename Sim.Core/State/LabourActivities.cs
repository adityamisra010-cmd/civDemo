using System.Collections.Immutable;
using Sim.Core.Systems;
using Sim.Core.Systems.Research;

namespace Sim.Core.State;

/// <summary>
/// ADR-033 D1 — ONE ACTIVITY IDENTITY of a labour sector: the baseline identity a sector expresses with
/// zero completed nodes, or a researched identity (an activity entity) that has become knowledge-eligible.
/// <see cref="Baseline"/> names the baseline capabilities that provide a baseline identity; for a researched
/// one, <see cref="Entity"/> is the activity entity and <see cref="Nodes"/> / <see cref="NodeNames"/> the
/// issuer's COMPLETED nodes named in its requirement — the knowledge that made it appear (key order).
/// </summary>
public sealed record LabourIdentity(
    string Id, string Name, bool Researched, ImmutableArray<string> Baseline, string? Entity,
    ImmutableArray<ResearchNodeId> Nodes, ImmutableArray<string> NodeNames)
{
    /// <summary>Value equality, the arrays compared element by element (ImmutableArray's own is by reference).</summary>
    public bool Equals(LabourIdentity? other) =>
        other is not null && string.Equals(Id, other.Id, StringComparison.Ordinal)
        && string.Equals(Name, other.Name, StringComparison.Ordinal) && Researched == other.Researched
        && AvailableActionsQuery.Same(Baseline, other.Baseline) && string.Equals(Entity, other.Entity, StringComparison.Ordinal)
        && AvailableActionsQuery.Same(Nodes, other.Nodes) && AvailableActionsQuery.Same(NodeNames, other.NodeNames);

    public override int GetHashCode() => HashCode.Combine(Id, Researched); // gate:allow-gethashcode — equality plumbing, never logic input
}

/// <summary>
/// ADR-033 D1 — ONE LABOUR SECTOR OF ONE CONTROLLED SETTLEMENT, presented as the activity it expresses
/// NOW. The sector itself is baseline and never gated: <see cref="Share"/> is its normalized labour share
/// (the settlement's allocation row, or the never-ordered default) and the goods it produces are exactly
/// what production does with that labour. Only the IDENTITY is knowledge-derived: <see cref="Identities"/>
/// is the baseline identity unless a researched identity that REPLACES it is knowledge-eligible, followed by
/// every knowledge-eligible researched identity in content order; <see cref="Label"/> joins their names.
/// <see cref="ActionId"/> = settlement × 8 + sector — the SectorAllocation order's TargetId packing and the
/// labour descriptor's id in <see cref="AvailableActionsQuery"/>.
/// </summary>
public sealed record LabourActivity(
    SettlementId Settlement, int Sector, string SectorId, double Share, string Label,
    ImmutableArray<LabourIdentity> Identities, ImmutableArray<GoodId> Goods, ImmutableArray<string> GoodNames)
{
    /// <summary>Whether any identity shown is a researched one.</summary>
    public bool Researched
    {
        get
        {
            foreach (LabourIdentity i in Identities) if (i.Researched) return true;
            return false;
        }
    }

    /// <summary>The SectorAllocation order's packed target (and the labour action's stable id).</summary>
    public int ActionId => LabourActivities.PackTarget(Settlement, Sector);

    /// <summary>Value equality, the arrays compared element by element (ImmutableArray's own is by reference).</summary>
    public bool Equals(LabourActivity? other) =>
        other is not null && Settlement == other.Settlement && Sector == other.Sector
        && string.Equals(SectorId, other.SectorId, StringComparison.Ordinal) && Share.Equals(other.Share)
        && string.Equals(Label, other.Label, StringComparison.Ordinal) && AvailableActionsQuery.Same(Identities, other.Identities)
        && AvailableActionsQuery.Same(Goods, other.Goods) && AvailableActionsQuery.Same(GoodNames, other.GoodNames);

    public override int GetHashCode() => HashCode.Combine(Settlement.Value, Sector); // gate:allow-gethashcode — equality plumbing, never logic input
}

/// <summary>
/// ADR-033 D1/D2 — THE LABOUR SURFACE, derived. Pure and read-only: no state, no cache, no system. It
/// answers "what does each of my settlements' labour do, and what is it called?" from authoritative state
/// (the control relation, the allocation rows, the deposits, the published variables and the COMPLETED
/// research the issuer holds) and content (research.json <c>sectorActivities</c>, goods.json). Research
/// changes the identity a sector is shown under, never what it produces (production reads no research).
///
/// <see cref="CanAllocate"/> is THE predicate for OrderKind 3 (SectorAllocation): PathBuildSystem accepts an
/// allocation order iff it holds on PREV, and <see cref="For"/> lists a settlement's sectors iff it holds on
/// the world asked about — one predicate, two callers (ADR-033 D2), so the UI never offers an allocation the
/// simulation would ignore.
///
/// Deterministic: settlements in table order, sectors 0..4, goods in registry order, entities in content
/// order; no dictionary, no LINQ (law 5).
/// </summary>
public static class LabourActivities
{
    /// <summary>D-032 target packing (settlement × 8 + sector), decoded by PathBuildSystem as &gt;&gt; 3 / &amp; 7.</summary>
    public static int PackTarget(SettlementId settlement, int sector) => settlement.Value * 8 + sector;

    /// <summary>
    /// THE OrderKind 3 predicate: the settlement exists and the issuer CONTROLS it (D-037; the control relation
    /// is authoritative, never the actor id taken on trust). A world with no Controls at all — a hand-built
    /// toy — has nothing to check against and treats every settlement as the issuer's (the ConstructionSystem
    /// and RoadDevelopmentQuery precedent).
    /// </summary>
    public static bool CanAllocate(IReadOnlyWorldState world, PolityId issuer, SettlementId settlement)
    {
        bool exists = false;
        for (int s = 0; s < world.Settlements.Count; s++)
            if (world.Settlements[s].Id.Value == settlement.Value) { exists = true; break; }
        if (!exists) return false;
        return world.Controls.Count == 0 || EmpireQuery.ControlsSettlement(world, issuer, settlement);
    }

    /// <summary>R2a — whether a researched identity that REPLACES the sector's baseline identity is knowledge-eligible
    /// for <paramref name="completed"/> (content: sectorActivities; for Farming, activity.farming). The
    /// pre-cultivation food yield reads this through the settlement's knowledge — no node id in code.</summary>
    public static bool SectorReplacedByResearch(ResearchContent content, bool[] completed, int sector)
    {
        foreach (SectorResearchedIdentity r in content.SectorActivities[sector].Researched)
            if (r.Mode == SectorActivityMode.Replaces && ResearchQuery.IsKnowledgeEligible(content, r.Entity, completed)) return true;
        return false;
    }

    /// <summary>R3 (Director R2-final §15) — the TRUTHFUL label of what a sector actually does with this knowledge:
    /// the baseline identity's name while no researched identity replaces it, otherwise, for each eligible
    /// researched identity, the names of the completed nodes that made it eligible (e.g. a settlement that knows only
    /// root-crop cultivation reads as that, not as the generic entity name "Farming"). Derived from the same
    /// identities <see cref="For"/> reports; no node id in code.</summary>
    public static string CapabilityLabel(ResearchContent content, bool[] completed, int sector)
    {
        ArgumentNullException.ThrowIfNull(content);
        ArgumentNullException.ThrowIfNull(completed);
        (ImmutableArray<LabourIdentity> identities, _) = Identities(content, completed, sector);
        string label = "";
        foreach (LabourIdentity id in identities)
        {
            string part = id.Researched && id.NodeNames.Length > 0 ? string.Join(" / ", id.NodeNames) : id.Name;
            label = label.Length == 0 ? part : label + " · " + part;
        }
        return label;
    }

    /// <summary>The settlements whose labour the issuer may allocate, in settlement-table order.</summary>
    public static SettlementId[] ControlledSettlements(IReadOnlyWorldState world, PolityId issuer)
    {
        var result = new List<SettlementId>();
        for (int s = 0; s < world.Settlements.Count; s++)
        {
            SettlementId id = world.Settlements[s].Id;
            if (CanAllocate(world, issuer, id)) result.Add(id);
        }
        return [.. result];
    }

    /// <summary>
    /// Every sector of every settlement the issuer may allocate, as the activity it expresses now:
    /// settlement-table order, then sector 0..4. With no research content attached (a config loaded without
    /// research.json) a sector is shown under its sector name — there is no knowledge to derive an identity
    /// from — and production is unchanged either way.
    /// </summary>
    public static ImmutableArray<LabourActivity> For(IReadOnlyWorldState world, SimConfig cfg, PolityId polity)
    {
        SettlementId[] settlements = ControlledSettlements(world, polity);
        if (settlements.Length == 0) return [];
        ResearchContent? content = cfg.Research;
        bool[]? completed = content is null ? null : ResearchQuery.CompletedMask(world, content, polity);

        // The identities depend on the polity's knowledge only, so they are derived once per sector.
        var identities = new ImmutableArray<LabourIdentity>[Sectors.Count];
        var labels = new string[Sectors.Count];
        for (int sector = 0; sector < Sectors.Count; sector++)
            (identities[sector], labels[sector]) = Identities(content, completed, sector);

        var result = ImmutableArray.CreateBuilder<LabourActivity>(settlements.Length * Sectors.Count);
        foreach (SettlementId settlement in settlements)
        {
            SectorAllocationRow shares = AllocationOf(world, settlement);
            for (int sector = 0; sector < Sectors.Count; sector++)
            {
                (ImmutableArray<GoodId> goods, ImmutableArray<string> names) = GoodsOf(world, cfg.Goods, settlement, sector, cfg.Research);
                result.Add(new LabourActivity(
                    settlement, sector, ResearchContentLoader.SectorIds[sector], Sectors.Share(shares, sector),
                    labels[sector], identities[sector], goods, names));
            }
        }
        return result.MoveToImmutable();
    }

    /// <summary>The labour activity of one settlement's sector, or null when the issuer may not allocate it.</summary>
    public static LabourActivity? Of(IReadOnlyWorldState world, SimConfig cfg, PolityId polity, SettlementId settlement, int sector)
    {
        if (sector < 0 || sector >= Sectors.Count) throw new ArgumentOutOfRangeException(nameof(sector), sector, "unknown sector");
        foreach (LabourActivity a in For(world, cfg, polity))
            if (a.Settlement.Value == settlement.Value && a.Sector == sector) return a;
        return null;
    }

    /// <summary>The settlement's allocation row, or the never-ordered default — exactly what production reads.</summary>
    public static SectorAllocationRow AllocationOf(IReadOnlyWorldState world, SettlementId settlement)
    {
        for (int i = 0; i < world.SectorAllocations.Count; i++)
            if (world.SectorAllocations[i].Settlement == settlement) return world.SectorAllocations[i];
        return Sectors.Default(settlement);
    }

    /// <summary>
    /// The identities a sector expresses for this knowledge: the baseline identity unless a knowledge-eligible
    /// researched identity REPLACES it, then every knowledge-eligible researched identity in content order.
    /// </summary>
    private static (ImmutableArray<LabourIdentity> Identities, string Label) Identities(
        ResearchContent? content, bool[]? completed, int sector)
    {
        if (content is null || completed is null)
        {
            string name = ResearchContentLoader.SectorIds[sector];
            return ([new LabourIdentity(name, name, false, [], null, [], [])], name);
        }
        SectorActivityContent map = content.SectorActivities[sector];
        var eligible = new List<SectorResearchedIdentity>();
        bool replaced = false;
        foreach (SectorResearchedIdentity r in map.Researched)
        {
            if (!ResearchQuery.IsKnowledgeEligible(content, r.Entity, completed)) continue;
            eligible.Add(r);
            if (r.Mode == SectorActivityMode.Replaces) replaced = true;
        }

        var list = ImmutableArray.CreateBuilder<LabourIdentity>();
        if (!replaced)
        {
            var baseline = ImmutableArray.CreateBuilder<string>(map.Baseline.Baseline.Count);
            foreach (int b in map.Baseline.Baseline) baseline.Add(content.Baseline[b].Id);
            list.Add(new LabourIdentity(map.Baseline.Id, map.Baseline.Name, false, baseline.MoveToImmutable(), null, [], []));
        }
        foreach (SectorResearchedIdentity r in eligible)
        {
            ResearchEntity entity = content.Entities[r.Entity];
            // The knowledge that made it appear: the requirement's node atoms the issuer has completed, key order.
            var keys = new List<int>();
            foreach (int atom in entity.NodeAtoms) if (completed[atom]) keys.Add(atom);
            keys.Sort();   // node index order is key order (technologies then civics, keys ascending)
            var nodes = ImmutableArray.CreateBuilder<ResearchNodeId>(keys.Count);
            var names = ImmutableArray.CreateBuilder<string>(keys.Count);
            foreach (int k in keys) { nodes.Add(content.Nodes[k].Key); names.Add(content.Nodes[k].Name); }
            list.Add(new LabourIdentity(entity.Id, r.Label, true, [], entity.Id, nodes.MoveToImmutable(), names.MoveToImmutable()));
        }

        ImmutableArray<LabourIdentity> identities = list.ToImmutable();
        string label = identities[0].Name;
        for (int i = 1; i < identities.Length; i++) label += " · " + identities[i].Name;
        return (identities, label);
    }

    /// <summary>
    /// The goods a sector of this settlement produces, registry (or recipe) order — the same classification
    /// ProductionSystem applies (pinned against its measured output by LabourActivityTests): Farming → the
    /// numeraire (grain); Herding/fishing → the settlement's livestock and fish deposits with abundance &gt; 0;
    /// Extraction → its every other deposit with abundance &gt; 0; Crafting → the outputs of the recipes
    /// CraftingQuery.IsRecipeAvailable admits (the controller's knowledge + the D-020 gate; R1); Construction → no good
    /// (it builds dwellings, paths and projects).
    /// </summary>
    public static (ImmutableArray<GoodId> Goods, ImmutableArray<string> Names) GoodsOf(
        IReadOnlyWorldState world, GoodsConfig? goods, SettlementId settlement, int sector, ResearchContent? research = null)
    {
        if (goods is null) return ([], []);
        var ids = new List<int>();
        switch (sector)
        {
            case Sectors.Farming:
                ids.Add(goods.GrainId);
                break;
            case Sectors.Herding:
            case Sectors.Extraction:
            {
                int livestock = goods.IdOf("livestock"), fish = goods.IdOf("fish");
                bool food = sector == Sectors.Herding;
                foreach (GoodEntry g in goods.Goods)
                {
                    bool isFood = g.Id == livestock || g.Id == fish;
                    if (isFood != food) continue;
                    if (DepositAbundance(world, settlement, new GoodId(g.Id)) > 0.0) ids.Add(g.Id);
                }
                break;
            }
            case Sectors.Crafting:
            {
                // R1: THE recipe predicate ProductionSystem applies (knowledge + the D-020 gate).
                bool[]? knowledge = CraftingQuery.KnowledgeOf(world, research, settlement);
                foreach (RecipeEntry r in goods.Recipes)
                {
                    if (!CraftingQuery.IsRecipeAvailable(world, research, settlement, r, knowledge)) continue;
                    int output = goods.IdOf(r.Output.Good);
                    if (output >= 0 && !ids.Contains(output)) ids.Add(output);
                }
                break;
            }
        }
        var result = ImmutableArray.CreateBuilder<GoodId>(ids.Count);
        var names = ImmutableArray.CreateBuilder<string>(ids.Count);
        foreach (int id in ids) { result.Add(new GoodId(id)); names.Add(goods.ById(id).Name); }
        return (result.MoveToImmutable(), names.MoveToImmutable());
    }

    private static double DepositAbundance(IReadOnlyWorldState world, SettlementId settlement, GoodId good)
    {
        double sum = 0.0;
        for (int i = 0; i < world.Deposits.Count; i++)
        {
            DepositRow d = world.Deposits[i];
            if (d.Settlement == settlement && d.Good == good) sum += d.Abundance;
        }
        return sum;
    }
}
