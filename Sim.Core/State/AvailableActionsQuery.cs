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
    /// <summary>Extension point: institution actions (stream S3, ADR-033 D6) attach here.</summary>
    Institutions = 8,
    Standing = 9,
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
/// <item><b>Governance</b>, <b>Institutions</b> — documented extension points (see <see cref="Governance"/>
///   and <see cref="Institutions"/>); they list nothing in this build.</item>
/// <item><b>Standing</b> — the research.json baseline capabilities flagged simulated.</item>
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
        Roads(world, cfg, polity, actions);
        Military(world, cfg, polity, actions);
        Governance(world, cfg, polity, actions);
        Institutions(world, cfg, polity, actions);
        Standing(cfg, actions);
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
    /// (Id 2) when one is set. Research runs only for registered Empires.</summary>
    public static void Research(IReadOnlyWorldState world, SimConfig cfg, PolityId polity, List<ActionDescriptor> into)
    {
        if (cfg.Research is not { } content || !EmpireQuery.TryGetCommandSource(world, polity, out _)) return;
        bool[] available = ResearchQuery.AvailableMask(content, ResearchQuery.CompletedMask(world, content, polity));
        bool hasTarget = ResearchQuery.TryGetTarget(world, polity, out ResearchNodeId current);

        var targets = ImmutableArray.CreateBuilder<ActionTarget>();
        for (int i = 0; i < available.Length; i++)
        {
            if (!available[i]) continue;
            targets.Add(NodeTarget(world, content, polity, i, hasTarget && content.Nodes[i].Key.Value == current.Value));
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
    /// ConstructionSystem applies to the order — Id = settlement × 2^32 + project id, with the materials (and,
    /// when the turn length is known, capacity) the settlement lacks as its blocker.</summary>
    public static void Construction(
        IReadOnlyWorldState world, SimConfig cfg, PolityId polity, ActionQueryContext context, List<ActionDescriptor> into)
    {
        if (cfg.Goods is not { } goods || goods.Projects is not { Length: > 0 } projects) return;
        ImmutableArray<string> builders = ConstructionBaseline(cfg.Research);
        for (int s = 0; s < world.Settlements.Count; s++)
        {
            SettlementId settlement = world.Settlements[s].Id;
            foreach (ConstructionProjectEntry project in projects)
            {
                if (!ConstructionQuery.IsProjectAvailable(world, cfg, polity, settlement, project.Id)) continue;
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
                    ConstructionQuery.Blocker(world, goods, settlement, project, context.NextDtYears),
                    ProjectProvenance(world, cfg.Research, polity, project, builders), detail));
            }
        }
    }

    private static string ProjectName(ResearchContent? research, ConstructionProjectEntry project)
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

    // ------------------------------------------------------------------ Roads (OrderKind 8)

    /// <summary>Develop roads (Id = the target class's edge type) when RoadDevelopmentQuery.Plan — the selection
    /// RoadDevelopmentSystem applies — is non-empty: some route is eligible for a class the issuer knows. Targets
    /// = the plan's routes in rank order; blocker = the first route's cost when the issuer can pay none of it.</summary>
    public static void Roads(IReadOnlyWorldState world, SimConfig cfg, PolityId polity, List<ActionDescriptor> into)
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
        SettlementId[] payers = RoadDevelopmentQuery.PayingSettlements(world, polity, first.Route.A, first.Route.B);
        string? blocker = RoadDevelopmentQuery.AffordableFraction(world.GoodStocks, payers, first.Cost) > 0.0
            ? null : "needs " + Costs(goods, first.Cost) + " for the first route";

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
    /// EXTENSION POINT — GOVERNANCE (ADR-033 D4, stream S1). Lists nothing in this build: no tax edict exists
    /// here. At reconciliation the orchestrator wires ONE Order descriptor (domain Governance, Id 1,
    /// OrderKind.SetTaxRate = 5, target = the issuing polity) iff <c>Governance.CanLevyTax(world, cfg, polity)</c>
    /// — the research-gated predicate SetTaxRate's world validation calls (one predicate, two callers).
    /// </summary>
    public static void Governance(IReadOnlyWorldState world, SimConfig cfg, PolityId polity, List<ActionDescriptor> into)
    {
        _ = world;
        _ = cfg;
        _ = polity;
        _ = into;
    }

    /// <summary>
    /// EXTENSION POINT — INSTITUTIONS (ADR-033 D6, stream S3). Lists nothing in this build: no institution
    /// table exists here. University projects are construction projects linked to their research entity in
    /// goods.json, so the Construction contributor lists them as soon as the entity is knowledge-eligible;
    /// any institution-specific action attaches here, through the institutions domain's own predicate.
    /// </summary>
    public static void Institutions(IReadOnlyWorldState world, SimConfig cfg, PolityId polity, List<ActionDescriptor> into)
    {
        _ = world;
        _ = cfg;
        _ = polity;
        _ = into;
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
