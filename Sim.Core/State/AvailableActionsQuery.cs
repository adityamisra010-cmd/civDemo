using System.Collections.Immutable;
using System.Globalization;
using Sim.Core.Kernel;
using Sim.Core.Systems;
using Sim.Core.Systems.Ages;
using Sim.Core.Systems.Research;

namespace Sim.Core.State;

/// <summary>The domain an available action belongs to. The enum value is the domain's ordinal in the
/// action surface's ordering key (domain ordinal, stable id).</summary>
public enum ActionDomain
{
    Labour = 1,
    Research = 2,
    Age = 3,
    Construction = 4,
    Roads = 5,
    Military = 6,
    /// <summary>Extension point: the tax edict (stream S1, OrderKind 5) is wired here at reconciliation.</summary>
    Governance = 7,
    /// <summary>ADR-033 D6 (stream S3): found a specialized university of a type at a settlement.</summary>
    Institutions = 8,
    Standing = 9,
    /// <summary>R1: a crafting recipe the civilization KNOWS (content-declared recipe entity, research-gated or
    /// baseline). It has no order of its own — the Crafting labour share runs it — so it is a capability entry
    /// whose targets are the settlements where it runs now.</summary>
    Production = 10,
    /// <summary>R2a: the Trade capability (research-gated; content sim.json trade.entity). No order — the existing
    /// TradeArbitrageSystem moves goods along connected routes once it is legal — so a capability entry whose
    /// targets are the issuer's settlements where trade is legal now.</summary>
    Trade = 11,
    /// <summary>M5 S1d (D-051 §5, ADR-036): found a settlement (OrderKind 9) from a settlement the issuer controls whose
    /// SOURCE-side founding arms hold now; the site is chosen per cell in the UI's placement mode, by the same predicate.</summary>
    Colonization = 12,
    /// <summary>WS1 (D-054, OrderKind 11): cancel one COMMITTED entry of a settlement's construction queue (an entry
    /// accepted on an earlier turn — not one ordered this turn, and not one whose cancel is already queued) where the
    /// issuer may cancel it now (ConstructionQuery.CancelOrderValid, what ConstructionSystem applies at delivery).
    /// Id = settlement × 2^32 + slot. None for an empty queue.</summary>
    ConstructionQueue = 13,
    /// <summary>WS2 (D-054, OrderKind 12): set the Empire's standing building/roads material split (ResourceAllocationRow).
    /// ONE descriptor (Id 1), listed exactly when the Roads one is (the split matters only to an Empire that can order
    /// road works) and the predicate ConstructionSystem applies at delivery admits an order
    /// (ConstructionQuery.AllocationOrderValid: the switch on, a roster Empire). Its own domain, not Roads: every Roads
    /// descriptor is a road-class development (Id = the edge type; the road-class card and the Roads block read it so).</summary>
    ResourceAllocation = 14,
    /// <summary>M6 Track EX (D-062 D9; ADR-044): the ACTIONS menu — per settlement the issuer controls, Explore surroundings
    /// (OrderKind 14; Id = settlement × 2) with fog on, and Prospect (OrderKind 15; Id = settlement × 2 + 1) with concealment
    /// on, each with its cost and, when the predicate DiscoverySystem applies refuses it now, the reason as the blocker
    /// (radius cap, already surveyed, not enough grain or tools, already ordered this turn).</summary>
    Discovery = 15,
}

/// <summary>Whether an available action is something the issuer can ORDER now, or a STANDING baseline
/// capability that works automatically and has no order (migration, settlement founding by famine flight,
/// basic shelter and paths, basic fighting).</summary>
public enum ActionKind
{
    Order = 1,
    Standing = 2,
}

/// <summary>What an <see cref="ActionTarget"/> designates.</summary>
public enum ActionTargetKind
{
    /// <summary>A settlement (Id = settlement id).</summary>
    Settlement = 1,
    /// <summary>A labour sector (Id = Sectors.Farming..Construction).</summary>
    Sector = 2,
    /// <summary>A research node (Id = its stable research.json key).</summary>
    ResearchNode = 3,
    /// <summary>An Age-surge emphasis (Id = ages.json surge key).</summary>
    Surge = 4,
    /// <summary>A construction project (Id = goods.json project id).</summary>
    Project = 5,
    /// <summary>An inter-city route (Id = A × 2^32 + B, the settlement pair A &lt; B).</summary>
    Route = 6,
    /// <summary>A military formation (Id = MilitaryUnitRow.Id).</summary>
    Formation = 7,
}

/// <summary>
/// One thing an action can be aimed at, with what a UI shows beside it: <see cref="Cost"/> (a research
/// node's EffectiveCost), <see cref="Progress"/> (its retained RP), <see cref="Current"/> (the active
/// target), a <see cref="Detail"/> line, and a per-target <see cref="Blocker"/> when the target is legal
/// but not affordable this turn.
/// </summary>
public sealed record ActionTarget(
    ActionTargetKind Kind, long Id, string Label,
    double? Cost = null, double? Progress = null, bool Current = false, string? Detail = null, string? Blocker = null);

/// <summary>
/// WHY an action exists — its knowledge provenance. <see cref="Baseline"/>: the baseline capabilities
/// (research.json baseline ids) that provide it with zero research. <see cref="Entities"/>: the research
/// entities whose knowledge eligibility it rests on (an entity with a null requirement — the granary — is
/// eligible with zero research). <see cref="Nodes"/> / <see cref="NodeNames"/>: the issuer's COMPLETED nodes
/// those entities' requirements name (key order) — the research that made it appear. Value equality.
/// </summary>
public sealed record ActionProvenance(
    ImmutableArray<string> Baseline, ImmutableArray<string> Entities,
    ImmutableArray<ResearchNodeId> Nodes, ImmutableArray<string> NodeNames)
{
    /// <summary>No knowledge provenance (an action of a system that needs no research entity).</summary>
    public static ActionProvenance None { get; } = new([], [], [], []);

    /// <summary>Whether completed research made the action appear (it names at least one completed node).</summary>
    public bool Researched => !Nodes.IsDefaultOrEmpty;

    public bool Equals(ActionProvenance? other) =>
        other is not null && AvailableActionsQuery.Same(Baseline, other.Baseline) && AvailableActionsQuery.Same(Entities, other.Entities)
        && AvailableActionsQuery.Same(Nodes, other.Nodes) && AvailableActionsQuery.Same(NodeNames, other.NodeNames);

    public override int GetHashCode() => HashCode.Combine(Baseline.Length, Entities.Length, Nodes.Length); // gate:allow-gethashcode — equality plumbing, never logic input
}

/// <summary>
/// ONE ACTION THE CIVILIZATION CAN TAKE NOW (ADR-033 D2), shaped so a UI renders it without re-deriving
/// anything. (<see cref="Domain"/>, <see cref="Id"/>) is the stable composite identity and the ordering key;
/// <see cref="Key"/> is the same identity as readable text. <see cref="Kind"/>: ORDER (issuable now, through
/// <see cref="Order"/> aimed at <see cref="Targets"/>) or STANDING (an automatic baseline capability with no
/// order). <see cref="Blocker"/> is a TRANSIENT reason it cannot complete this turn although it is legal
/// ("needs 40 timber (has 12)") — information, never a locked future control: a locked action is not listed
/// at all. <see cref="Provenance"/> says which baseline capability or which completed knowledge made it
/// available. Value equality (targets and provenance compared element by element).
/// </summary>
public sealed record ActionDescriptor(
    ActionDomain Domain, long Id, ActionKind Kind, string Key, string Label, OrderKind? Order,
    ImmutableArray<ActionTarget> Targets, string? Blocker, ActionProvenance Provenance, string? Detail = null)
{
    /// <summary>Whether a transient blocker stops it completing this turn.</summary>
    public bool IsBlocked => Blocker is not null;

    public bool Equals(ActionDescriptor? other) =>
        other is not null && Domain == other.Domain && Id == other.Id && Kind == other.Kind
        && string.Equals(Key, other.Key, StringComparison.Ordinal) && string.Equals(Label, other.Label, StringComparison.Ordinal)
        && Order == other.Order && AvailableActionsQuery.Same(Targets, other.Targets)
        && string.Equals(Blocker, other.Blocker, StringComparison.Ordinal) && Provenance.Equals(other.Provenance)
        && string.Equals(Detail, other.Detail, StringComparison.Ordinal);

    public override int GetHashCode() => HashCode.Combine(Domain, Id); // gate:allow-gethashcode — equality plumbing, never logic input
}

/// <summary>
/// What the query needs besides the world: the not-yet-stepped orders of the current turn (so a decision
/// already queued — an Age advance — is not offered twice) and the length in years of the turn about to be
/// played (construction capacity is adult-years per turn; without it the capacity blocker is not reported).
/// </summary>
public sealed record ActionQueryContext(IReadOnlyList<OrderRecord>? Queued = null, double? NextDtYears = null)
{
    public static ActionQueryContext None { get; } = new();

    /// <summary>The context of the step the next End Turn plays: the era table's dt at the world's date.</summary>
    public static ActionQueryContext ForNextStep(IReadOnlyWorldState world, EraTable era, IReadOnlyList<OrderRecord>? queued = null) =>
        new(queued, era.DtDaysAt(world.Clock.SimDays) / (double)SimClock.YearDays);
}

/// <summary>
/// ADR-033 D2 — "WHAT CAN THIS CIVILIZATION DO NOW?" One pure, read-only aggregation of DOMAIN-OWNED
/// predicates. It owns no state, no cache and no system; it is not a CapabilitySystem and not an action
/// manager. Every contributor asks the domain that owns the rule, through the SAME function that domain's
/// order consumer calls (one predicate, two callers — so the UI can never offer an order the simulation would
/// reject, and an order the UI never offered is rejected on the same rule):
///
/// <list type="bullet">
/// <item><b>Labour</b> (OrderKind 3) — <see cref="LabourActivities"/>: every sector of every settlement the
///   issuer may allocate (<see cref="LabourActivities.CanAllocate"/>, which PathBuildSystem applies), shown as
///   the activity it expresses now (research.json sectorActivities; ADR-033 D1).</item>
/// <item><b>Research</b> (OrderKind 6) — <see cref="ResearchQuery"/>: set a target among the AVAILABLE nodes
///   (<see cref="ResearchQuery.AvailableMask"/>, what ResearchSystem applies), with costs, retained progress
///   and the current target; clear the target when one is set.</item>
/// <item><b>Age</b> (OrderKind 7) — <see cref="AgeQuery"/>: advance when <see cref="AgeQuery.CheckAdvance(IReadOnlyWorldState, AgeContent, PolityId, int, int)"/>
///   accepts it (what AgeTransitionSystem applies) and no advance is already pending this turn.</item>
/// <item><b>Construction</b> (OrderKind 4) — <see cref="ConstructionQuery"/>: each project
///   <see cref="ConstructionQuery.IsProjectAvailable"/> admits (what ConstructionSystem applies), with its
///   materials and capacity blockers.</item>
/// <item><b>Roads</b> (OrderKind 8) — <see cref="RoadDevelopmentQuery.Plan(IReadOnlyWorldState, ResearchContent, RoadsConfig, GoodsConfig, PolityId, double)"/>,
///   the selection RoadDevelopmentSystem applies (transport is FROZEN: read only).</item>
/// <item><b>Military</b> (Standing) — <see cref="MilitaryQuery"/>: the roster, its family lines and Age
///   identities. No military order exists, so none is offered.</item>
/// <item><b>Governance</b> (OrderKind 5) — the tax edict, iff the predicate GovernanceSystem applies holds
///   (<see cref="Governance.CanLevyTax"/>: the Taxation knowledge AND the minimum Age, H2; <see cref="Governance"/>).</item>
/// <item><b>Institutions</b> (OrderKind 4, ADR-033 D6) — found a specialized university of type T at settlement
///   S, listed exactly where <see cref="ConstructionQuery.IsProjectAvailable"/> admits the founding project (the
///   predicate ConstructionSystem applies), with the founding viability, materials and capacity as its
///   transient blocker (<see cref="Institutions"/>). The Construction contributor does not list founding
///   projects, so nothing is listed twice.</item>
/// <item><b>Colonization</b> (OrderKind 9, M5 S1d) — found a settlement from each settlement the issuer controls for which
///   the SOURCE-side arms of <see cref="FoundingQuery"/> hold (control, enough people, enough food —
///   <see cref="FoundingQuery.Departure"/>, the function ColonizationSystem applies); the per-cell arms are judged per site
///   by the same predicate (the UI's placement mode).</item>
/// <item><b>Standing</b> — the research.json baseline capabilities flagged simulated.</item>
/// <item><b>Production</b> (R1; no order — the Crafting share runs it) — every goods.json recipe the issuer
///   KNOWS (<see cref="CraftingQuery.IsKnownBy"/>: its content-declared recipe entity is knowledge-eligible), with
///   the settlements where <see cref="CraftingQuery.IsRecipeAvailable(IReadOnlyWorldState, SimConfig, SettlementId, RecipeEntry)"/>
///   — the predicate ProductionSystem applies — admits it now as targets. Iterates content: a new recipe and
///   its entity flow here with no code change.</item>
/// </list>
///
/// ONLY AVAILABLE ACTIONS ARE LISTED: a locked future action is never returned (the research trees show
/// future knowledge and what it unlocks). A polity that is not on a non-empty roster can issue no order and
/// gets nothing. ORDER: the composite key (domain ordinal, stable integer id) — <see cref="Compare"/>; no
/// ordering over doubles, no dictionary iteration, no LINQ (law 5).
/// </summary>
public static class AvailableActionsQuery
{
    /// <summary>Every action available to <paramref name="polity"/> now, in (domain, id) order.</summary>
    public static ImmutableArray<ActionDescriptor> For(
        IReadOnlyWorldState world, SimConfig cfg, PolityId polity, ActionQueryContext? context = null)
    {
        context ??= ActionQueryContext.None;
        // An actor must be a registered Empire (OrderValidation's rule; the toy world with no roster is exempt).
        if (world.Polities.Count > 0 && !EmpireQuery.TryGetCommandSource(world, polity, out _)) return [];

        var actions = new List<ActionDescriptor>();
        Labour(world, cfg, polity, actions);
        Research(world, cfg, polity, actions);
        Age(world, cfg, polity, context, actions);
        Construction(world, cfg, polity, context, actions);
        Roads(world, cfg, polity, context, actions);
        Military(world, cfg, polity, actions);
        Governance(world, cfg, polity, actions);
        Institutions(world, cfg, polity, context, actions);
        Standing(cfg, actions);
        Production(world, cfg, polity, actions);
        Trade(world, cfg, polity, actions);
        Colonization(world, cfg, polity, context, actions);
        ConstructionQueue(world, cfg, polity, context, actions);
        Discovery(world, cfg, polity, context, actions);
        return Order(actions);
    }

    /// <summary>THE ordering key: domain ordinal, then the stable integer id (both ascending).</summary>
    public static int Compare(ActionDescriptor x, ActionDescriptor y)
    {
        if (x.Domain != y.Domain) return ((int)x.Domain).CompareTo((int)y.Domain);
        return x.Id.CompareTo(y.Id);
    }

    /// <summary>The descriptors in (domain, id) order. Each contributor emits unique ids within its domain, so
    /// the key is total and the result is independent of the input order.</summary>
    public static ImmutableArray<ActionDescriptor> Order(IReadOnlyList<ActionDescriptor> actions)
    {
        var sorted = new ActionDescriptor[actions.Count];
        for (int i = 0; i < sorted.Length; i++) sorted[i] = actions[i];
        Array.Sort(sorted, Compare);
        return [.. sorted];
    }

    // ------------------------------------------------------------------ Labour (OrderKind 3)

    /// <summary>One Order per (settlement, sector) the issuer may allocate: Id = settlement × 8 + sector (the
    /// SectorAllocation packing), labelled with the activity the sector expresses now.</summary>
    public static void Labour(IReadOnlyWorldState world, SimConfig cfg, PolityId polity, List<ActionDescriptor> into)
    {
        foreach (LabourActivity a in LabourActivities.For(world, cfg, polity))
        {
            var baseline = ImmutableArray.CreateBuilder<string>();
            var entities = ImmutableArray.CreateBuilder<string>();
            var nodes = new List<(ResearchNodeId Key, string Name)>();
            foreach (LabourIdentity i in a.Identities)
            {
                foreach (string b in i.Baseline) if (!baseline.Contains(b)) baseline.Add(b);
                if (i.Entity is { } e) entities.Add(e);
                for (int k = 0; k < i.Nodes.Length; k++)
                    if (!ContainsKey(nodes, i.Nodes[k])) nodes.Add((i.Nodes[k], i.NodeNames[k]));
            }
            nodes.Sort(static (x, y) => x.Key.Value.CompareTo(y.Key.Value));
            into.Add(new ActionDescriptor(
                ActionDomain.Labour, a.ActionId, ActionKind.Order,
                $"labour.{Inv(a.Settlement.Value)}.{a.SectorId}", a.Label, OrderKind.SectorAllocation,
                [SettlementTarget(a.Settlement), new ActionTarget(ActionTargetKind.Sector, a.Sector, a.SectorId)],
                null, Provenance(baseline.ToImmutable(), entities.ToImmutable(), nodes),
                $"{Percent(a.Share)} of labour" + (a.GoodNames.Length > 0 ? " · " + string.Join(", ", a.GoodNames) : "")));
        }
    }

    // ------------------------------------------------------------------ Research (OrderKind 6)

    /// <summary>Set a research target (Id 1) among the AVAILABLE nodes — the mask ResearchSystem applies — with
    /// each node's EffectiveCost, retained progress and whether it is the current target; clear the target
    /// (Id 2) when one is set. Research runs only for registered Empires. ADR-043 (two-lane model): Current marks either
    /// lane's target; Id 2 ("research.clear-target") clears the TECHNOLOGY lane and Id 3 ("research.clear-civics-target")
    /// the CIVICS lane, each listed only while its lane holds a target.</summary>
    public static void Research(IReadOnlyWorldState world, SimConfig cfg, PolityId polity, List<ActionDescriptor> into)
    {
        if (cfg.Research is not { } content || !EmpireQuery.TryGetCommandSource(world, polity, out _)) return;
        bool[] available = ResearchQuery.AvailableMask(content, ResearchQuery.CompletedMask(world, content, polity));
        bool twoLanes = content.Tuning.TwoLanes;
        ResearchNodeId current = default, civic = default;
        bool hasTarget = twoLanes
            ? ResearchQuery.TryGetTarget(world, content, polity, ResearchTree.Technology, out current)
            : ResearchQuery.TryGetTarget(world, polity, out current);
        bool hasCivic = twoLanes && ResearchQuery.TryGetTarget(world, content, polity, ResearchTree.Civics, out civic);

        var targets = ImmutableArray.CreateBuilder<ActionTarget>();
        for (int i = 0; i < available.Length; i++)
        {
            if (!available[i]) continue;
            int key = content.Nodes[i].Key.Value;
            targets.Add(NodeTarget(world, content, polity, i, (hasTarget && key == current.Value) || (hasCivic && key == civic.Value)));
        }
        if (targets.Count > 0)
            into.Add(new ActionDescriptor(
                ActionDomain.Research, 1, ActionKind.Order, "research.set-target", "Set research target",
                OrderKind.SetResearchTarget, targets.ToImmutable(), null, ActionProvenance.None,
                $"{Inv(targets.Count)} available"));
        if (hasTarget && content.IndexOf(current) is int index and >= 0)
            into.Add(new ActionDescriptor(
                ActionDomain.Research, 2, ActionKind.Order, "research.clear-target", "Clear research target",
                OrderKind.SetResearchTarget, [NodeTarget(world, content, polity, index, true)], null, ActionProvenance.None));
        if (hasCivic && content.IndexOf(civic) is int civicIndex and >= 0)
            into.Add(new ActionDescriptor(
                ActionDomain.Research, 3, ActionKind.Order, "research.clear-civics-target", "Clear civics target",
                OrderKind.SetResearchTarget, [NodeTarget(world, content, polity, civicIndex, true)], null, ActionProvenance.None));
    }

    private static ActionTarget NodeTarget(IReadOnlyWorldState world, ResearchContent content, PolityId polity, int index, bool current)
    {
        ResearchNode n = content.Nodes[index];
        string where = n.Tree == ResearchTree.Civics ? "Civics"
            : n.Branch < 0 ? "Technology" : "Technology · " + content.Branches[n.Branch].Name;
        return new ActionTarget(ActionTargetKind.ResearchNode, n.Key.Value, n.Name,
            Cost: ResearchQuery.EffectiveCost(world, content, polity, index),
            Progress: ResearchQuery.Progress(world, polity, n.Key), Current: current, Detail: where);
    }

    // ------------------------------------------------------------------ Age (OrderKind 7)

    /// <summary>Advance to the next Age (Id = that Age's key) when AgeQuery.CheckAdvance accepts it — the check
    /// AgeTransitionSystem applies — and no advance is already queued this turn; targets = the surge choices.</summary>
    public static void Age(IReadOnlyWorldState world, SimConfig cfg, PolityId polity, ActionQueryContext context, List<ActionDescriptor> into)
    {
        if (cfg.Ages is not { } ages || ages.Surges.Count == 0) return;
        if (AgeQuery.NextAge(world, ages, polity) is not { } next) return;
        if (AgeQuery.CheckAdvance(world, ages, polity, next.Key, ages.Surges[0].Key) != AdvanceRejection.None) return;
        if (context.Queued is { } queued && AgeQuery.PendingAdvance(world, ages, queued, polity) is not null) return;
        var surges = ImmutableArray.CreateBuilder<ActionTarget>(ages.Surges.Count);
        foreach (AgeSurge s in ages.Surges)
            surges.Add(new ActionTarget(ActionTargetKind.Surge, s.Key, s.Name, Detail: s.Description));
        into.Add(new ActionDescriptor(
            ActionDomain.Age, next.Key, ActionKind.Order, $"age.advance.{Inv(next.Key)}", $"Advance to {next.Name}",
            OrderKind.AdvanceAge, surges.MoveToImmutable(), null, ActionProvenance.None,
            "Eligible: the next turn begins in the new Age"));
    }

    // ------------------------------------------------------------------ Construction (OrderKind 4)

    /// <summary>Each (settlement, project) ConstructionQuery.IsProjectAvailable admits — the predicate
    /// ConstructionSystem applies to the order, its purse's claims read from the world's queue plus the pending batch
    /// (<see cref="ActionQueryContext.Queued"/>) as the intake would queue it — Id = settlement × 2^32 + project id, with the materials the
    /// settlement's purse lacks (ConstructionQuery.Holders: its Empire's settlements together, or its own stock) — and, with
    /// incremental construction off, the capacity it lacks this turn — as its blocker (WS1: labour is a pace, not a
    /// blocker). Projects that FOUND an
    /// institution (the universities, ADR-033 D6) are listed by <see cref="Institutions"/> instead.</summary>
    public static void Construction(
        IReadOnlyWorldState world, SimConfig cfg, PolityId polity, ActionQueryContext context, List<ActionDescriptor> into)
    {
        if (cfg.Goods is not { } goods || goods.Projects is not { Length: > 0 } projects) return;
        ImmutableArray<string> builders = ConstructionBaseline(cfg.Research);
        // D-053 §1: the purse's claims are read from the world's queue PLUS the turn's pending orders the intake would
        // accept (ConstructionQuery.QueueWithPending) — so once a workshop is ordered this turn its ten tools are claimed,
        // and a second workshop the intake would refuse is not offered.
        IReadOnlyTable<ConstructionQueueRow> claims = ConstructionQuery.QueueWithPending(world, cfg, context.Queued, out IReadOnlyTable<ConstructionProgressRow> claimsProgress);
        SetAsideReading? holds = null;   // D-055: read once, when the first project is listed
        for (int s = 0; s < world.Settlements.Count; s++)
        {
            SettlementId settlement = world.Settlements[s].Id;
            foreach (ConstructionProjectEntry project in projects)
            {
                if (InstitutionContent.FoundsInstitution(project)) continue;   // the Institutions contributor's
                if (!ConstructionQuery.IsProjectAvailable(world, cfg, polity, settlement, project.Id, claims, claimsProgress)) continue;
                string name = ProjectName(cfg.Research, project);
                long built = ConstructionQuery.Built(world, settlement, project.Id);
                int queued = 0;
                foreach (ConstructionQueueRow row in ConstructionQuery.Queue(world, settlement)) if (row.ProjectId == project.Id) queued++;
                string detail = Inputs(project) + $" · {Num(project.LaborRequired)} adult-years"
                    + (built > 0 ? $" · built {Inv(built)}" : "") + (queued > 0 ? $" · {Inv(queued)} queued" : "");
                into.Add(new ActionDescriptor(
                    ActionDomain.Construction, ((long)settlement.Value << 32) | (uint)project.Id, ActionKind.Order,
                    $"construction.{Inv(settlement.Value)}.{project.Name}", $"Build {name}", OrderKind.EnqueueConstruction,
                    [SettlementTarget(settlement), new ActionTarget(ActionTargetKind.Project, project.Id, name)],
                    ConstructionQuery.Blocker(world, cfg, settlement, project, context.NextDtYears, 0,
                        NewOrderSetAside(world, cfg, context, settlement, ref holds)),
                    ProjectProvenance(world, cfg.Research, polity, project, builders), detail));
            }
        }
    }

    // ------------------------------------------------------------------ Construction queue: cancel (OrderKind 11, WS1)

    /// <summary>One descriptor per COMMITTED queue entry the issuer may cancel now (<see cref="ActionDomain.ConstructionQueue"/>):
    /// the entry is in the world's own queue (an entry ordered this turn is not — it "can be cancelled from next turn"), no
    /// cancel of it is already pending (<see cref="ConstructionQuery.PendingCancels"/>), and the order passes
    /// <see cref="ConstructionQuery.CancelOrderValid(IReadOnlyWorldState, OrderRecord)"/> — the predicate ConstructionSystem
    /// applies at delivery (one predicate, two callers). Detail: what has gone into it (forfeited by a cancel: no refund).</summary>
    public static void ConstructionQueue(
        IReadOnlyWorldState world, SimConfig cfg, PolityId polity, ActionQueryContext context, List<ActionDescriptor> into)
    {
        if (world.ConstructionQueue.Count == 0) return;
        QueueEntryRef[] pending = ConstructionQuery.PendingCancels(world, context.Queued);
        for (int i = 0; i < world.ConstructionQueue.Count; i++)
        {
            ConstructionQueueRow row = world.ConstructionQueue[i];
            OrderRecord order = ConstructionQuery.CancelOrder(world, polity, row.Settlement, row.Slot);
            if (!ConstructionQuery.CancelOrderValid(world, order)) continue;
            if (Array.IndexOf(pending, new QueueEntryRef(row.Settlement, row.Slot, row.ProjectId)) >= 0) continue;
            ConstructionProjectEntry? project = cfg.Goods?.ProjectById(row.ProjectId);
            string name = project is null ? "project " + Inv(row.ProjectId) : ProjectName(cfg.Research, project);
            long progress = ConstructionQuery.ProgressOf(world, row);
            string detail = project is null || progress == 0
                ? "nothing has gone into it yet"
                : ConstructionQuery.GoneIn(cfg.Goods!, project, progress) + " - cancelling loses it";
            into.Add(new ActionDescriptor(
                ActionDomain.ConstructionQueue, ((long)row.Settlement.Value << 32) | (uint)row.Slot, ActionKind.Order,
                $"construction.cancel.{Inv(row.Settlement.Value)}.{Inv(row.Slot)}", $"Cancel {name}", OrderKind.CancelConstructionEntry,
                [SettlementTarget(row.Settlement), new ActionTarget(ActionTargetKind.Project, row.ProjectId, name, Progress: progress)],
                null, ActionProvenance.None, detail));
        }
    }

    public static string ProjectName(ResearchContent? research, ConstructionProjectEntry project)
    {
        if (project.Entity is { } id && research is not null && research.EntityIndexOf(id) is int e and >= 0
            && research.Entities[e].Name is { Length: > 0 } name) return name;
        return project.Name;
    }

    private static ActionProvenance ProjectProvenance(
        IReadOnlyWorldState world, ResearchContent? research, PolityId polity, ConstructionProjectEntry project, ImmutableArray<string> builders)
    {
        if (project.Entity is not { } id || research is null || research.EntityIndexOf(id) is not (int e and >= 0))
            return new ActionProvenance(builders, [], [], []);
        bool[] completed = ResearchQuery.CompletedMask(world, research, polity);
        var nodes = new List<(ResearchNodeId Key, string Name)>();
        foreach (int atom in research.Entities[e].NodeAtoms)
            if (completed[atom] && !ContainsKey(nodes, research.Nodes[atom].Key)) nodes.Add((research.Nodes[atom].Key, research.Nodes[atom].Name));
        nodes.Sort(static (x, y) => x.Key.Value.CompareTo(y.Key.Value));
        return Provenance(builders, [id], nodes);
    }

    /// <summary>The baseline capabilities that provide the Construction sector's baseline identity (content:
    /// sectorActivities) — the labour projects are built with. Data, never an id in code.</summary>
    private static ImmutableArray<string> ConstructionBaseline(ResearchContent? research)
    {
        if (research is null) return [];
        var ids = ImmutableArray.CreateBuilder<string>();
        foreach (int b in research.SectorActivities[Sectors.Construction].Baseline.Baseline) ids.Add(research.Baseline[b].Id);
        return ids.ToImmutable();
    }

    /// <summary>
    /// D-055 (batch-4 design §1.6, T1b): what a would-be NEW order of a project at <paramref name="settlement"/> finds SET ASIDE
    /// in its purse — every set-aside of the purse (SetAsideQuery.HeldIn: every head is ahead of a new order in the building
    /// order), from THE walk on the world and the turn's pending orders (SetAsideQuery.Read, read once per contributor call
    /// into <paramref name="reading"/>). Null with the hold rule off (Reservations.Enabled): the blocker is batch 3's.
    /// </summary>
    private static long[]? NewOrderSetAside(
        IReadOnlyWorldState world, SimConfig cfg, ActionQueryContext context, SettlementId settlement, ref SetAsideReading? reading)
    {
        if (!Reservations.Enabled(cfg)) return null;
        reading ??= SetAsideQuery.Read(world, cfg, context.Queued, context.NextDtYears ?? world.Clock.DtYears);
        return SetAsideQuery.HeldIn(reading.Walk, ConstructionQuery.PurseKey(world, cfg, settlement));
    }

    // ------------------------------------------------------------------ Roads (OrderKind 8)

    /// <summary>Develop roads (Id = the target class's edge type) when RoadDevelopmentQuery.Plan — the selection
    /// RoadDevelopmentSystem applies — is non-empty: some route is eligible for a class the issuer knows. Targets
    /// = the plan's routes in rank order; blocker = the first route's cost when the issuer can pay none of it.</summary>
    public static void Roads(IReadOnlyWorldState world, SimConfig cfg, PolityId polity, List<ActionDescriptor> into) =>
        Roads(world, cfg, polity, ActionQueryContext.None, into);

    /// <summary><see cref="Roads(IReadOnlyWorldState, SimConfig, PolityId, List{ActionDescriptor})"/> for the step
    /// <paramref name="context"/> describes (its pending orders and dt): the blocker subtracts what that step's construction
    /// will set aside (T1b fix round, 2026-10-17, finding F2).</summary>
    public static void Roads(IReadOnlyWorldState world, SimConfig cfg, PolityId polity, ActionQueryContext context, List<ActionDescriptor> into)
    {
        if (cfg.Roads is not { } roads || cfg.Research is not { } research || cfg.Goods is not { } goods) return;
        RoadDevelopmentStep[] plan = RoadDevelopmentQuery.Plan(world, research, roads, goods, polity, 100.0);
        if (plan.Length == 0) return;
        int targetClass = plan[0].Route.TargetClass;
        string className = ClassName(research, roads, targetClass);

        var routes = ImmutableArray.CreateBuilder<ActionTarget>(plan.Length);
        foreach (RoadDevelopmentStep step in plan)
        {
            RouteStatus r = step.Route;
            routes.Add(new ActionTarget(ActionTargetKind.Route, ((long)r.A.Value << 32) | (uint)r.B.Value,
                $"Settlement {Inv(r.A.Value)} – Settlement {Inv(r.B.Value)}",
                Detail: $"{Num(r.LengthKm)} km · usage {Inv(r.Usage)} · {Percent(r.StartFraction)} modernized · needs {Costs(goods, step.Cost)}"));
        }
        RoadDevelopmentStep first = plan[0];
        // D-055 (T1b; T1a round 2's open item): the payers road development itself draws on — with the hold rule on, the
        // issuer's places less any another Empire's purse owns (ConstructionQuery.EmpirePursePlaces) — and what they hold less
        // what construction sets aside within them (RoadDevelopmentQuery.SetAsideWithin). T1b fix round (2026-10-17, finding
        // F2): the set-asides are THIS step's — the walk on the world and the context's pending orders (SetAsideQuery.StepTable;
        // the step runs construction before road development), so a pending cancel of a holder releases them and a pending
        // enqueue adds its own, as the step will; an estimate, like every queue reading. The rule off: PayingSettlements and
        // the whole holding, batch 3 exactly.
        SettlementId[] payers = ConstructionQuery.EmpirePursePlaces(world, cfg, polity,
            RoadDevelopmentQuery.PayingSettlements(world, polity, first.Route.A, first.Route.B));
        long[] have = RoadDevelopmentQuery.Holdings(world.GoodStocks, payers, first.Cost);
        IReadOnlyTable<ConstructionHoldRow> setAsides = SetAsideQuery.StepTable(world, cfg, context.Queued, context.NextDtYears ?? world.Clock.DtYears);
        var asideParts = new List<string>();
        for (int m = 0; m < first.Cost.Length; m++)
        {
            long[]? keep = null;
            long aside = RoadDevelopmentQuery.SetAsideWithin(world, cfg, setAsides, payers, first.Cost[m].Good, ref keep);
            have[m] = Math.Max(0, have[m] - aside);
            if (aside > 0) asideParts.Add(Inv(aside) + " " + goods.ById(first.Cost[m].Good.Value).Name);
        }
        string? blocker = RoadDevelopmentQuery.AffordableFraction(first.Cost, have) > 0.0
            ? null : "needs " + Costs(goods, first.Cost) + " for the first route"
                + (asideParts.Count == 0 ? "" : " (set aside for building projects: " + string.Join(", ", asideParts) + ")");

        int entity = roads.ClassOf(targetClass)?.Entity is { } eid ? research.EntityIndexOf(eid) : -1;
        ActionProvenance provenance = ActionProvenance.None;
        if (entity >= 0)
        {
            bool[] completed = ResearchQuery.CompletedMask(world, research, polity);
            var nodes = new List<(ResearchNodeId Key, string Name)>();
            foreach (int atom in research.Entities[entity].NodeAtoms)
                if (completed[atom]) nodes.Add((research.Nodes[atom].Key, research.Nodes[atom].Name));
            nodes.Sort(static (x, y) => x.Key.Value.CompareTo(y.Key.Value));
            provenance = Provenance([], [research.Entities[entity].Id], nodes);
        }
        into.Add(new ActionDescriptor(
            ActionDomain.Roads, targetClass, ActionKind.Order, $"roads.develop.{Inv(targetClass)}", $"Develop roads ({className})",
            OrderKind.DevelopRoads, routes.MoveToImmutable(), blocker, provenance,
            $"{Inv(plan.Length)} eligible route(s), heaviest-used first"));
        MaterialsSplit(world, cfg, polity, into);
    }

    // ------------------------------------------------------------------ Materials split (OrderKind 12, WS2)

    /// <summary>The descriptor of the building/roads material split (<see cref="ActionDomain.ResourceAllocation"/>, Id 1):
    /// called by the Roads contributor right after it lists road development, so it is listed exactly when that is (the
    /// split governs nothing for an Empire that cannot order road works), and only when the predicate ConstructionSystem
    /// applies at delivery (<see cref="ConstructionQuery.AllocationOrderValid"/>) admits the Empire's order and the Empire
    /// has a purse to split (Controls, a settlement it controls). Detail: the split in force (its row, or the default).</summary>
    private static void MaterialsSplit(IReadOnlyWorldState world, SimConfig cfg, PolityId polity, List<ActionDescriptor> into)
    {
        if (!Sim.Core.State.ResourceAllocation.Enabled(cfg) || world.Controls.Count == 0) return;
        if (LabourActivities.ControlledSettlements(world, polity).Length == 0) return;
        int b = Sim.Core.State.ResourceAllocation.EffectivePercent(world, polity);
        if (!ConstructionQuery.AllocationOrderValid(world, cfg, Sim.Core.State.ResourceAllocation.Order(world, polity, b))) return;
        string detail = b == Sim.Core.State.ResourceAllocation.DefaultBuildingsPercent
            ? Reservations.Enabled(cfg)
                ? "building first (default): road works get only what is left after building work uses or sets aside what it needs"
                : "building first (default): road works get only what building work leaves"
            : "on a turn road works are ordered, building work is guaranteed " + Inv(b) + " shares in 100 and road works "
              + Inv(100 - b) + "; whatever one side does not need, the other may use";
        into.Add(new ActionDescriptor(
            ActionDomain.ResourceAllocation, 1, ActionKind.Order, "allocation.materials", "Materials for building and roads",
            OrderKind.SetResourceAllocation, [], null, ActionProvenance.None, detail));
    }

    private static string ClassName(ResearchContent research, RoadsConfig roads, int edgeType)
    {
        if (roads.ClassOf(edgeType)?.Entity is { } id && research.EntityIndexOf(id) is int e and >= 0
            && research.Entities[e].Name is { Length: > 0 } name) return name;
        return "class " + Inv(edgeType);
    }

    // ------------------------------------------------------------------ Military (Standing)

    /// <summary>Basic fighting (Id 1, Standing): the issuer's formations with their family line and Age
    /// identity (MilitaryQuery). No military order exists, so it is a standing capability with no order.</summary>
    public static void Military(IReadOnlyWorldState world, SimConfig cfg, PolityId polity, List<ActionDescriptor> into)
    {
        MilitaryUnitRow[] units = MilitaryQuery.Units(world, polity);
        if (units.Length == 0) return;
        UnitFamilyContent? families = cfg.UnitFamilies;
        var targets = ImmutableArray.CreateBuilder<ActionTarget>(units.Length);
        foreach (MilitaryUnitRow u in units)
        {
            UnitIdentity? identity = families?.IdentityByKey(u.Identity);
            UnitFamily? family = families?.FamilyByKey(u.Family);
            string label = identity?.Name ?? "Formation " + Inv(u.Id);
            string detail = (family?.Name ?? "family " + Inv(u.Family))
                + (identity is not null ? $" · Age {Inv(identity.Age)} identity" : "")
                + $" · at settlement {Inv(u.Location.Value)}";
            targets.Add(new ActionTarget(ActionTargetKind.Formation, u.Id, label, Detail: detail));
        }
        into.Add(new ActionDescriptor(
            ActionDomain.Military, 1, ActionKind.Standing, "military.basic-fighting", "Basic fighting", null,
            targets.MoveToImmutable(), null, ActionProvenance.None,
            $"{Inv(units.Length)} formation(s); recruitment, movement and battle are not yet simulated"));
    }

    // ------------------------------------------------------------------ extension points

    /// <summary>
    /// GOVERNANCE (ADR-033 D4; wired at the S1×S2 reconciliation). Lists ONE Order descriptor — the tax edict
    /// (domain Governance, Id 1, <see cref="OrderKind.SetTaxRate"/>; the order's target is the issuing polity
    /// itself, so the descriptor carries no target) — iff <c>Governance.CanLevyTax(world, cfg, polity)</c>,
    /// the research- and Age-gated predicate GovernanceSystem applies and the AI valve obeys (one predicate, every
    /// caller). Nothing is listed before the polity knows the Taxation civic AND has entered sim.json
    /// governance.taxationMinAge (H2: A3), so turn 1 shows no tax control.
    /// Provenance: the completed nodes named by sim.json <c>governance.taxationRequires</c>, key order.
    /// Detail: the declared levy (or "no levy declared") and the realm's legitimacy.
    /// </summary>
    public static void Governance(IReadOnlyWorldState world, SimConfig cfg, PolityId polity, List<ActionDescriptor> into)
    {
        if (!global::Sim.Core.State.Governance.CanLevyTax(world, cfg, polity)) return;
        ResearchContent research = cfg.Research!;      // CanLevyTax holds only with research and governance content
        GovernanceConfig governance = cfg.Governance!;
        global::Sim.Core.Systems.ClassMobility.Predicate requirement = ResearchContentLoader.ParseRequirement(
            research, governance.TaxationRequires, "sim.json governance.taxationRequires");
        bool[] completed = ResearchQuery.CompletedMask(world, research, polity);
        var nodes = new List<(ResearchNodeId Key, string Name)>();
        foreach (int atom in requirement.AtomIds)
            if (atom >= 0 && atom < completed.Length && completed[atom])
                nodes.Add((research.Nodes[atom].Key, research.Nodes[atom].Name));
        nodes.Sort(static (x, y) => x.Key.Value.CompareTo(y.Key.Value));

        string levy = global::Sim.Core.State.Governance.HasPolicy(world, polity)
            ? "levy " + (global::Sim.Core.State.Governance.NominalTaxRate(world, polity) * 100.0).ToString("0.#", CultureInfo.InvariantCulture) + "%"
            : "no levy declared";
        string detail = levy + " · legitimacy "
            + global::Sim.Core.State.Governance.Legitimacy(world, polity, cfg).ToString("0.0", CultureInfo.InvariantCulture);
        into.Add(new ActionDescriptor(
            ActionDomain.Governance, 1, ActionKind.Order, "governance.tax-edict", "Set the tax levy",
            OrderKind.SetTaxRate, [], null, Provenance([], [], nodes), detail));
    }

    /// <summary>
    /// INSTITUTIONS (ADR-033 D6, stream S3) — "Found a &lt;type&gt; University" per (settlement, university type),
    /// listed EXACTLY where <see cref="ConstructionQuery.IsProjectAvailable"/> admits the type's founding
    /// project (goods.json <c>founds</c>) — the predicate ConstructionSystem applies to the EnqueueConstruction
    /// order (one predicate, two callers): the issuer controls the settlement and BOTH the building
    /// (<c>building.university</c>) and the institution (<c>inst.university</c>) are knowledge-eligible
    /// (ADR-028 LOCKED → AVAILABLE). A LOCKED type is not listed. Id = settlement × 2^32 + project id (the
    /// construction packing); targets = the settlement and the project; Order = EnqueueConstruction (the
    /// founding goes through the existing queue). Blocker = the transient reasons it cannot complete this turn,
    /// each computed by the function that gates it: the host's FOUNDING viability (the market and the food
    /// surplus — InstitutionViability.ToFound), the materials, and — when the turn length is known — the
    /// construction capacity. Provenance: both entities and the completed nodes their requirements name.
    /// Detail: the subtree it discounts, the cost, how many of the type the settlement hosts, and what one
    /// more mature university of the type would cut from the issuer's branch costs (diminishing returns).
    /// </summary>
    public static void Institutions(
        IReadOnlyWorldState world, SimConfig cfg, PolityId polity, ActionQueryContext context, List<ActionDescriptor> into)
    {
        if (cfg.Goods is not { } goods || goods.Projects is not { Length: > 0 } projects || cfg.Research is not { } research) return;
        ImmutableArray<string> builders = ConstructionBaseline(research);
        SetAsideReading? holds = null;   // D-055: read once, when the first founding is listed
        for (int s = 0; s < world.Settlements.Count; s++)
        {
            SettlementId settlement = world.Settlements[s].Id;
            foreach (ConstructionProjectEntry project in projects)
            {
                if (project.Founds is not { } founds) continue;
                if (InstitutionContent.TypeOf(research, InstitutionContent.TypeKeyOf(research, project)) is not { } type) continue;
                if (!ConstructionQuery.IsProjectAvailable(world, cfg, polity, settlement, project.Id)) continue;

                long here = 0;
                for (int i = 0; i < world.Institutions.Count; i++)
                    if (world.Institutions[i].Settlement == settlement && world.Institutions[i].Type == type.Key) here++;
                string detail = research.Branches[type.Branch].Name + " research"
                    + " · " + Inputs(project) + $" · {Num(project.LaborRequired)} adult-years"
                    + (here > 0 ? $" · {Inv(here)} here" : "");
                if (cfg.Institutions is { } institutions)
                {
                    double x = InstitutionEffects.MaturityWeighted(world, polity, type.Key);
                    double cut = institutions.Universities.MaxResearchCostReduction * InstitutionEffects.NextMarginal(x);
                    detail += $" · a mature one cuts {research.Branches[type.Branch].Name} costs by {Percent(cut)} more";
                }

                bool[] completed = ResearchQuery.CompletedMask(world, research, polity);
                var nodes = new List<(ResearchNodeId Key, string Name)>();
                var entities = ImmutableArray.CreateBuilder<string>();
                foreach (string id in project.Entity is { } building ? new[] { building, founds.Entity } : [founds.Entity])
                {
                    entities.Add(id);
                    if (research.EntityIndexOf(id) is not (int e and >= 0)) continue;
                    foreach (int atom in research.Entities[e].NodeAtoms)
                        if (completed[atom] && !ContainsKey(nodes, research.Nodes[atom].Key)) nodes.Add((research.Nodes[atom].Key, research.Nodes[atom].Name));
                }
                nodes.Sort(static (x, y) => x.Key.Value.CompareTo(y.Key.Value));

                into.Add(new ActionDescriptor(
                    ActionDomain.Institutions, ((long)settlement.Value << 32) | (uint)project.Id, ActionKind.Order,
                    $"institutions.{Inv(settlement.Value)}.{type.Id}", $"Found {type.Name}", OrderKind.EnqueueConstruction,
                    [SettlementTarget(settlement), new ActionTarget(ActionTargetKind.Project, project.Id, type.Name)],
                    ConstructionQuery.Blocker(world, cfg, settlement, project, context.NextDtYears, 0,
                        NewOrderSetAside(world, cfg, context, settlement, ref holds)),
                    Provenance(builders, entities.ToImmutable(), nodes), detail));
            }
        }
    }

    // ------------------------------------------------------------------ Standing baselines

    /// <summary>Every research.json baseline capability flagged SIMULATED (Id = its position in the content's
    /// baseline list): automatic, no order — the civilization does these with zero research.</summary>
    public static void Standing(SimConfig cfg, List<ActionDescriptor> into)
    {
        if (cfg.Research is not { } content) return;
        for (int i = 0; i < content.Baseline.Count; i++)
        {
            ResearchBaselineCapability b = content.Baseline[i];
            if (!b.Simulated) continue;
            into.Add(new ActionDescriptor(
                ActionDomain.Standing, i, ActionKind.Standing, "standing." + b.Id, b.Name, null, [], null,
                new ActionProvenance([b.Id], [], [], []), b.ProvidedBy));
        }
    }

    // ------------------------------------------------------------------ Production (R1; no order)

    /// <summary>
    /// One Standing descriptor per goods.json recipe the issuer KNOWS (Id = recipe index + 1; content order):
    /// <see cref="CraftingQuery.IsKnownBy"/> — the recipe's research entity is knowledge-eligible (a null
    /// requirement is baseline). Targets: the issuer's settlements where the full recipe predicate ProductionSystem
    /// applies (<see cref="CraftingQuery.IsRecipeAvailable(IReadOnlyWorldState, SimConfig, SettlementId, RecipeEntry)"/>:
    /// knowledge AND the recipe's own D-020 condition) holds now. Blocker when it runs nowhere: the recipe's own
    /// condition, verbatim from content. Provenance: the Crafting sector's baseline, the entity, and the completed
    /// nodes its requirement names. A recipe that is not known is not listed (it is future knowledge in the trees).
    /// </summary>
    public static void Production(IReadOnlyWorldState world, SimConfig cfg, PolityId polity, List<ActionDescriptor> into)
    {
        if (cfg.Goods is not { } goods || goods.Recipes.Length == 0) return;
        ResearchContent? research = cfg.Research;
        ImmutableArray<string> crafters = CraftingBaseline(research);
        SettlementId[] places = LabourActivities.ControlledSettlements(world, polity);
        for (int r = 0; r < goods.Recipes.Length; r++)
        {
            RecipeEntry recipe = goods.Recipes[r];
            if (!CraftingQuery.IsKnownBy(world, research, polity, recipe)) continue;
            var targets = ImmutableArray.CreateBuilder<ActionTarget>();
            foreach (SettlementId s in places)
                if (CraftingQuery.IsRecipeAvailable(world, cfg, s, recipe)) targets.Add(SettlementTarget(s));
            string name = RecipeName(research, recipe);
            string detail = RecipeInputs(recipe) + " · crafting labour";
            string? blocker = targets.Count == 0 && recipe.Requires is { } cond ? "runs where " + cond + " (no settlement yet)" : null;
            into.Add(new ActionDescriptor(
                ActionDomain.Production, r + 1, ActionKind.Standing, "production." + recipe.Name, name, null,
                targets.ToImmutable(), blocker, EntityProvenance(world, research, polity, recipe.Entity, crafters), detail));
        }
    }

    // ------------------------------------------------------------------ Trade (R2a; no order)

    /// <summary>
    /// R2a — the Trade capability, listed iff the issuer KNOWS it (<see cref="TradeQuery.KnowsTrade"/>: the content's
    /// trade entity is knowledge-eligible for the issuer). Targets: the issuer's settlements where
    /// <see cref="TradeQuery.SettlementKnowsTrade"/> — the predicate TradeArbitrageSystem applies per endpoint —
    /// holds now. Not listed when the content names no trade entity (trade is then ungated and needs no entry) or
    /// before the research: a locked future action is never listed.
    /// </summary>
    public static void Trade(IReadOnlyWorldState world, SimConfig cfg, PolityId polity, List<ActionDescriptor> into)
    {
        if (cfg.Trade.Entity is not { } entity || cfg.Research is not { } research) return;
        if (!TradeQuery.KnowsTrade(world, cfg, polity)) return;
        var targets = ImmutableArray.CreateBuilder<ActionTarget>();
        foreach (SettlementId s in LabourActivities.ControlledSettlements(world, polity))
            if (TradeQuery.SettlementKnowsTrade(world, cfg, s)) targets.Add(SettlementTarget(s));
        int e = research.EntityIndexOf(entity);
        string name = e >= 0 && research.Entities[e].Name is { Length: > 0 } n ? n : "Trade";
        into.Add(new ActionDescriptor(
            ActionDomain.Trade, 1, ActionKind.Standing, "trade." + entity, name, null, targets.ToImmutable(), null,
            EntityProvenance(world, research, polity, entity, []),
            "goods flow between connected settlements that both know trade, when a price gap exceeds the transport cost"));
    }

    // ------------------------------------------------------------------ Colonization (OrderKind 9; M5 S1d)

    /// <summary>
    /// M5 S1d (D-051 §5, ADR-036) — FOUND A SETTLEMENT. One Order descriptor (Id = the SOURCE settlement's id, target =
    /// the source, <see cref="OrderKind.FoundSettlement"/>) per settlement the issuer controls for which the SOURCE-SIDE
    /// arms of THE predicate hold on the world asked about: arm 1 (the issuer controls the source —
    /// <see cref="EmpireQuery.ControlsSettlement"/>) and arms 5–6 (enough people, enough food —
    /// <see cref="FoundingQuery.Departure"/> on the world's tables, the function ColonizationSystem applies to the order).
    /// The PER-CELL arms (on the map, land, the floor, occupancy, spacing, the claim, the route — and the player's known
    /// extent) depend on the site, which the order names and this query cannot: they are judged per cell, by the same
    /// <see cref="FoundingQuery.Check(IReadOnlyWorldState, SimConfig, Worldgen.SettlementSiting.FrontierSiting, ReadOnlySpan{double}, ReadOnlySpan{int}, PolityId, SettlementId, int)"/>,
    /// in the UI's placement mode. NOT listed: for a world without terrain (no site can be judged); for a source with a
    /// FoundSettlement order of the issuer's already queued this turn (<see cref="ActionQueryContext.Queued"/>) — the
    /// intake founds only the first legal order per source per turn, so a second would be skipped (the Age precedent: a
    /// decision already queued is not offered twice). Detail: the party and its provisions —
    /// <see cref="FoundingQuery.AllocateParty"/> and <see cref="FoundingQuery.ProvisionsShare"/>, the numbers the order
    /// would move if it departed from this world (departure is live: ADR-036 §3).
    /// </summary>
    public static void Colonization(
        IReadOnlyWorldState world, SimConfig cfg, PolityId polity, ActionQueryContext context, List<ActionDescriptor> into)
    {
        if (world.Terrain is null || cfg.Goods is not { } goods) return;
        var grain = new GoodId(goods.GrainId);
        string grainName = goods.ById(goods.GrainId).Name;
        for (int s = 0; s < world.Settlements.Count; s++)
        {
            SettlementId source = world.Settlements[s].Id;
            if (!EmpireQuery.ControlsSettlement(world, polity, source)) continue;
            if (context.Queued is { } queued && FoundingQueued(queued, polity, source)) continue;
            if (FoundingQuery.Departure(world.Buckets, world.GoodStocks, cfg, source, null, out long party) != FoundingVerdict.Legal) continue;
            long provisions = FoundingQuery.ProvisionsShare(world.GoodStocks, world.Buckets, source, grain, party);
            into.Add(new ActionDescriptor(
                ActionDomain.Colonization, source.Value, ActionKind.Order, $"colonization.found.{Inv(source.Value)}", "Found a settlement",
                OrderKind.FoundSettlement, [SettlementTarget(source)], null, ActionProvenance.None,
                $"{Inv(party)} people and {Inv(provisions)} {grainName} would set out"));
        }
    }

    /// <summary>
    /// M6 Track EX (D-062 D9; ADR-044) — THE ACTIONS MENU's descriptors: per settlement the issuer controls (settlement-table
    /// order), Explore (fog on) and Prospect (concealment on), judged by THE predicates DiscoverySystem applies
    /// (<see cref="DiscoveryQuery.CheckExplore(IReadOnlyWorldState, SimConfig, PolityId, SettlementId)"/>,
    /// <see cref="DiscoveryQuery.CheckProspect(IReadOnlyWorldState, SimConfig, PolityId, SettlementId)"/>) on the world's
    /// published stocks less its set-asides; a refusal is the descriptor's blocker, and an order of the same kind for the
    /// same settlement already queued this turn blocks it ("already ordered this turn": DiscoverySystem applies one).
    /// </summary>
    public static void Discovery(
        IReadOnlyWorldState world, SimConfig cfg, PolityId polity, ActionQueryContext context, List<ActionDescriptor> into)
    {
        bool fog = cfg.Fog is { Enabled: true } && world.Terrain is not null && cfg.Goods is not null;
        bool prospect = DepositConcealment.Enabled(cfg);
        if (!fog && !prospect) return;
        string grainName = cfg.Goods!.ById(cfg.Goods.GrainId).Name;
        long cost = prospect ? DiscoveryQuery.ProspectCost(world, cfg, polity) : 0;   // once (the requirement is parsed once)
        for (int s = 0; s < world.Settlements.Count; s++)
        {
            SettlementId id = world.Settlements[s].Id;
            if (!EmpireQuery.ControlsSettlement(world, polity, id)) continue;
            if (fog)
            {
                FogConfig f = cfg.Fog!;
                int radius = DiscoveryQuery.ExploredRadius(world, f, polity, id);
                ExploreVerdict v = DiscoveryQuery.CheckExplore(world, cfg, polity, id);
                string? blocker = context.Queued is { } q && Queued(q, polity, OrderKind.Explore, id)
                    ? "already ordered this turn" : DiscoveryQuery.ExploreBlocker(v, cfg);
                string detail = v == ExploreVerdict.AtCap
                    ? $"explored to {Inv(radius)} nodes (the limit)"
                    : $"radius {Inv(radius)} → {Inv(DiscoveryQuery.NextRadius(world, f, polity, id))} nodes, cost {Inv(DiscoveryQuery.ExploreCost(cfg))} {grainName}";
                into.Add(new ActionDescriptor(
                    ActionDomain.Discovery, (long)id.Value * 2, ActionKind.Order, $"discovery.explore.{Inv(id.Value)}",
                    "Explore surroundings", OrderKind.Explore, [SettlementTarget(id)], blocker, ActionProvenance.None, detail));
            }
            if (prospect)
            {
                ProspectVerdict v = DiscoveryQuery.CheckProspect(world, cfg, polity, id, cost);
                string? blocker = context.Queued is { } q && Queued(q, polity, OrderKind.Prospect, id)
                    ? "already ordered this turn" : DiscoveryQuery.ProspectBlocker(v, cost);
                into.Add(new ActionDescriptor(
                    ActionDomain.Discovery, (long)id.Value * 2 + 1, ActionKind.Order, $"discovery.prospect.{Inv(id.Value)}",
                    "Prospect", OrderKind.Prospect, [SettlementTarget(id)], blocker, ActionProvenance.None,
                    $"cost {Inv(cost)} {DepositConcealment.ProspectGood}"));
            }
        }
    }

    /// <summary>Whether <paramref name="queued"/> holds a <paramref name="kind"/> order of <paramref name="polity"/>'s for
    /// <paramref name="settlement"/>.</summary>
    public static bool Queued(IReadOnlyList<OrderRecord> queued, PolityId polity, OrderKind kind, SettlementId settlement)
    {
        for (int i = 0; i < queued.Count; i++)
            if (queued[i].Kind == kind && queued[i].ActorId == polity.Value && queued[i].TargetId == settlement.Value) return true;
        return false;
    }

    /// <summary>Whether <paramref name="queued"/> holds a FoundSettlement order of <paramref name="polity"/>'s from
    /// <paramref name="source"/>.</summary>
    public static bool FoundingQueued(IReadOnlyList<OrderRecord> queued, PolityId polity, SettlementId source)
    {
        for (int i = 0; i < queued.Count; i++)
            if (queued[i].Kind == OrderKind.FoundSettlement && queued[i].ActorId == polity.Value && queued[i].TargetId == source.Value) return true;
        return false;
    }

    private static string RecipeName(ResearchContent? research, RecipeEntry recipe)
    {
        if (recipe.Entity is { } id && research is not null && research.EntityIndexOf(id) is int e and >= 0
            && research.Entities[e].Name is { Length: > 0 } name) return name;
        return recipe.Name;
    }

    private static string RecipeInputs(RecipeEntry recipe)
    {
        var parts = new List<string>();
        foreach (RecipeInput i in recipe.Inputs) parts.Add($"{Num(i.PerOutput)} {i.Good}");
        return string.Join(" + ", parts) + $" → {Inv(recipe.Output.Qty)} {recipe.Output.Good}";
    }

    /// <summary>Provenance of an action resting on one research entity: the given baseline ids, the entity, and the
    /// issuer's completed nodes its requirement names (key order). No entity → the baseline alone.</summary>
    private static ActionProvenance EntityProvenance(
        IReadOnlyWorldState world, ResearchContent? research, PolityId polity, string? entityId, ImmutableArray<string> baseline)
    {
        if (entityId is not { } id || research is null || research.EntityIndexOf(id) is not (int e and >= 0))
            return new ActionProvenance(baseline, [], [], []);
        bool[] completed = ResearchQuery.CompletedMask(world, research, polity);
        var nodes = new List<(ResearchNodeId Key, string Name)>();
        foreach (int atom in research.Entities[e].NodeAtoms)
            if (completed[atom] && !ContainsKey(nodes, research.Nodes[atom].Key)) nodes.Add((research.Nodes[atom].Key, research.Nodes[atom].Name));
        nodes.Sort(static (x, y) => x.Key.Value.CompareTo(y.Key.Value));
        return Provenance(nodes.Count == 0 ? baseline : [], [id], nodes);
    }

    /// <summary>The baseline capabilities that provide the Crafting sector's baseline identity (content:
    /// sectorActivities). Data, never an id in code.</summary>
    private static ImmutableArray<string> CraftingBaseline(ResearchContent? research)
    {
        if (research is null) return [];
        var ids = ImmutableArray.CreateBuilder<string>();
        foreach (int b in research.SectorActivities[Sectors.Crafting].Baseline.Baseline) ids.Add(research.Baseline[b].Id);
        return ids.ToImmutable();
    }

    // ------------------------------------------------------------------ helpers

    /// <summary>
    /// Element-wise equality of two immutable arrays (default and empty are equal). USE THIS (or
    /// SequenceEqual) to compare two query results: ImmutableArray's own Equals compares the underlying
    /// array by REFERENCE, so two equal results of two calls are never ==. Every record this query returns
    /// has value equality, compared element by element through this function.
    /// </summary>
    public static bool Same<T>(ImmutableArray<T> a, ImmutableArray<T> b)
    {
        int n = a.IsDefault ? 0 : a.Length, m = b.IsDefault ? 0 : b.Length;
        if (n != m) return false;
        EqualityComparer<T> eq = EqualityComparer<T>.Default;
        for (int i = 0; i < n; i++) if (!eq.Equals(a[i], b[i])) return false;
        return true;
    }

    private static ActionProvenance Provenance(
        ImmutableArray<string> baseline, ImmutableArray<string> entities, List<(ResearchNodeId Key, string Name)> nodes)
    {
        var keys = ImmutableArray.CreateBuilder<ResearchNodeId>(nodes.Count);
        var names = ImmutableArray.CreateBuilder<string>(nodes.Count);
        foreach ((ResearchNodeId key, string name) in nodes) { keys.Add(key); names.Add(name); }
        return new ActionProvenance(baseline, entities, keys.MoveToImmutable(), names.MoveToImmutable());
    }

    private static bool ContainsKey(List<(ResearchNodeId Key, string Name)> nodes, ResearchNodeId key)
    {
        foreach ((ResearchNodeId k, _) in nodes) if (k.Value == key.Value) return true;
        return false;
    }

    private static ActionTarget SettlementTarget(SettlementId settlement) =>
        new(ActionTargetKind.Settlement, settlement.Value, "Settlement " + Inv(settlement.Value));

    private static string Inputs(ConstructionProjectEntry project)
    {
        var parts = new List<string>(project.Inputs.Length);
        foreach (ProjectInput input in project.Inputs) parts.Add($"{Inv(input.Qty)} {input.Good}");
        return string.Join(", ", parts);
    }

    private static string Costs(GoodsConfig goods, RoadMaterialCost[] cost)
    {
        if (cost.Length == 0) return "nothing";
        var parts = new List<string>(cost.Length);
        foreach (RoadMaterialCost c in cost) parts.Add($"{Inv(c.Units)} {goods.ById(c.Good.Value).Name}");
        return string.Join(", ", parts);
    }

    private static string Percent(double fraction) => (fraction * 100.0).ToString("0.#", CultureInfo.InvariantCulture) + "%";
    private static string Num(double v) => v.ToString("0.##", CultureInfo.InvariantCulture);
    private static string Inv(long v) => v.ToString(CultureInfo.InvariantCulture);
}
