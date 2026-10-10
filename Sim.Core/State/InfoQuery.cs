using System.Collections.Immutable;
using System.Globalization;
using Sim.Core.Systems;
using Sim.Core.Systems.Ages;
using Sim.Core.Systems.ClassMobility;
using Sim.Core.Systems.Research;

namespace Sim.Core.State;

/// <summary>What an <see cref="InfoSubject"/> designates (M5 polish, directive §9 — "Shift + Left Click universal info").</summary>
public enum InfoKind
{
    /// <summary>A research node: Id = its stable research.json key.</summary>
    ResearchNode = 1,
    /// <summary>A research.json registry entity: Ref = its id (a building, unit, institution, activity, recipe…).</summary>
    Entity = 2,
    /// <summary>A baseline (zero-research) capability: Ref = its research.json baseline id.</summary>
    Baseline = 3,
    /// <summary>An Age milestone: Id = the Age it is an entry requirement of, Ref = the milestone id.</summary>
    AgeMilestone = 4,
    /// <summary>An Age: Id = its key (1..9).</summary>
    Age = 5,
    /// <summary>A labour sector: Id = the sector (Sectors.Farming..Construction), Settlement = where (-1 = the capital).</summary>
    LabourSector = 6,
    /// <summary>A good: Id = its goods.json id.</summary>
    Good = 7,
    /// <summary>A construction project: Id = its goods.json id, Settlement = where (-1 = the capital).</summary>
    Project = 8,
    /// <summary>A crafting recipe: Ref = its goods.json recipe name.</summary>
    Recipe = 9,
    /// <summary>A road class: Id = its sim.json edge type.</summary>
    RoadClass = 10,
    /// <summary>A military formation: Id = its MilitaryUnitRow id.</summary>
    Formation = 11,
    /// <summary>The tax edict (singleton).</summary>
    TaxEdict = 12,
    /// <summary>A specialised-university type: Id = its research.json universityTypes key.</summary>
    UniversityType = 13,
    /// <summary>Research itself — the shared pool and its one target (singleton; the status band's research chip).</summary>
    Learning = 14,
}

/// <summary>
/// THE STABLE KEY of an inspectable object: content keys and ids or row ids only — never display text and never a
/// content index that reordering would shift. Value equality (Ref compared ordinally).
/// </summary>
public readonly record struct InfoSubject(InfoKind Kind, long Id = 0, string? Ref = null, int Settlement = -1)
{
    public static InfoSubject Node(ResearchNodeId key) => new(InfoKind.ResearchNode, key.Value);
    public static InfoSubject OfEntity(string id) => new(InfoKind.Entity, 0, id);
    public static InfoSubject OfBaseline(string id) => new(InfoKind.Baseline, 0, id);
    public static InfoSubject Milestone(int age, string id) => new(InfoKind.AgeMilestone, age, id);
    public static InfoSubject OfAge(int age) => new(InfoKind.Age, age);
    public static InfoSubject Sector(int sector, int settlement = -1) => new(InfoKind.LabourSector, sector, null, settlement);
    public static InfoSubject OfGood(int id) => new(InfoKind.Good, id);
    public static InfoSubject OfProject(int id, int settlement = -1) => new(InfoKind.Project, id, null, settlement);
    public static InfoSubject OfRecipe(string name) => new(InfoKind.Recipe, 0, name);
    public static InfoSubject OfRoadClass(int edgeType) => new(InfoKind.RoadClass, edgeType);
    public static InfoSubject OfFormation(int unitId) => new(InfoKind.Formation, unitId);
    public static InfoSubject Tax => new(InfoKind.TaxEdict);
    public static InfoSubject OfUniversity(int typeKey) => new(InfoKind.UniversityType, typeKey);
    public static InfoSubject Research => new(InfoKind.Learning);
}

/// <summary>Where a subject stands for the issuer now — each value the reading of the simulation's own predicate.</summary>
public enum InfoStatus
{
    /// <summary>Known: a completed node, a knowledge-eligible entity, a simulated baseline, an existing object.</summary>
    Known = 1,
    /// <summary>An ORDER the issuer can give now (AvailableActionsQuery lists it, no transient blocker).</summary>
    Available = 2,
    /// <summary>An order the issuer can give now that cannot complete this turn (its transient blocker).</summary>
    Blocked = 3,
    /// <summary>A research node open to research now (ResearchQuery.IsAvailable).</summary>
    Researchable = 4,
    /// <summary>The current research target.</summary>
    Researching = 5,
    /// <summary>Locked: knowledge (or another legality condition) is missing.</summary>
    Locked = 6,
    /// <summary>The knowledge is held but the Age the capability needs is not entered (the tax edict's gate).</summary>
    NeedsAge = 7,
    /// <summary>An Age milestone that holds now.</summary>
    Met = 8,
    /// <summary>An Age milestone that does not hold yet.</summary>
    NotMet = 9,
    /// <summary>An Age milestone whose mechanism is not built yet (ages.json pending).</summary>
    Pending = 10,
    /// <summary>A baseline capability no system simulates in this build.</summary>
    NotSimulated = 11,
    /// <summary>The content the subject needs is not loaded (e.g. no governance section).</summary>
    Inert = 12,
    /// <summary>The subject names nothing in this content.</summary>
    Unknown = 13,
}

/// <summary>What completing a research node does in this build (directive §9: "knowledge only — no simulated effect").</summary>
public enum InfoEffect
{
    /// <summary>Something the simulation runs reads it (an entity a system realizes, an Age milestone, a gate).</summary>
    Simulated = 1,
    /// <summary>Its only effect is to open further research (it is a prerequisite of other nodes).</summary>
    ResearchOnly = 2,
    /// <summary>Nothing reads it: its unlocks are described, not simulated.</summary>
    KnowledgeOnly = 3,
}

/// <summary>One line of a card: a label (plain name), the subject it links to (navigable), whether it holds for
/// the issuer now, and a note (where it lives, its state, why it matters).</summary>
public sealed record InfoLink(string Label, InfoSubject? Subject = null, bool? Met = null, string? Note = null);

/// <summary>A kind-specific block of a card (e.g. "Could become" for a labour sector). Value equality.</summary>
public sealed record InfoSection(string Heading, ImmutableArray<InfoLink> Lines)
{
    public bool Equals(InfoSection? other) =>
        other is not null && string.Equals(Heading, other.Heading, StringComparison.Ordinal) && AvailableActionsQuery.Same(Lines, other.Lines);

    public override int GetHashCode() => HashCode.Combine(Heading.Length, Lines.IsDefault ? 0 : Lines.Length); // gate:allow-gethashcode — equality plumbing, never logic input
}

/// <summary>
/// ONE INFO CARD — the answers to the Director's questions about one subject (directive §9):
/// WHAT IS IT (<see cref="Title"/>, <see cref="Kind"/>, <see cref="What"/>), WHAT ENABLES IT (<see cref="EnabledBy"/>,
/// <see cref="RequirementSource"/> verbatim from content), WHICH RESEARCH NODE (<see cref="ResearchNode"/> — the "show in
/// research tree" target), PREREQUISITES (<see cref="Prerequisites"/> of that node), RESOURCE / INSTITUTION /
/// INFRASTRUCTURE REQUIREMENTS (<see cref="Requirements"/>), CURRENT STATUS (<see cref="Status"/>, <see cref="StatusLine"/>),
/// WHY LOCKED (<see cref="WhyLocked"/>, and <see cref="Summary"/> in one line), WHAT IT ENABLES (<see cref="Enables"/>,
/// <see cref="RealizedBy"/>, <see cref="Effect"/>), what is described but not simulated (<see cref="KnowledgeOnly"/>), and the
/// live action descriptor when <see cref="AvailableActionsQuery"/> lists one (<see cref="Action"/>). Value equality.
/// </summary>
public sealed record InfoCard(
    InfoSubject Subject, string Title, string Kind, string What,
    InfoStatus Status, string StatusLine, string? Summary,
    ImmutableArray<InfoLink> EnabledBy, string? RequirementSource,
    ResearchNodeId? ResearchNode, string? ResearchNodeName,
    ImmutableArray<InfoLink> Prerequisites, ImmutableArray<InfoLink> Requirements, ImmutableArray<InfoLink> WhyLocked,
    ImmutableArray<InfoLink> Enables, ImmutableArray<string> KnowledgeOnly, string RealizedBy, InfoEffect Effect,
    ImmutableArray<InfoSection> More, ActionDescriptor? Action)
{
    public bool Equals(InfoCard? o) =>
        o is not null && Subject == o.Subject && S(Title, o.Title) && S(Kind, o.Kind) && S(What, o.What)
        && Status == o.Status && S(StatusLine, o.StatusLine) && S(Summary, o.Summary)
        && AvailableActionsQuery.Same(EnabledBy, o.EnabledBy) && S(RequirementSource, o.RequirementSource)
        && ResearchNode == o.ResearchNode && S(ResearchNodeName, o.ResearchNodeName)
        && AvailableActionsQuery.Same(Prerequisites, o.Prerequisites) && AvailableActionsQuery.Same(Requirements, o.Requirements)
        && AvailableActionsQuery.Same(WhyLocked, o.WhyLocked) && AvailableActionsQuery.Same(Enables, o.Enables)
        && AvailableActionsQuery.Same(KnowledgeOnly, o.KnowledgeOnly) && S(RealizedBy, o.RealizedBy) && Effect == o.Effect
        && AvailableActionsQuery.Same(More, o.More) && Equals(Action, o.Action);

    public override int GetHashCode() => HashCode.Combine(Subject.Kind, Subject.Id, Title.Length); // gate:allow-gethashcode — equality plumbing, never logic input

    private static bool S(string? a, string? b) => string.Equals(a, b, StringComparison.Ordinal);
}

/// <summary>
/// M5 POLISH — "WHAT IS THIS, AND WHAT DOES IT TAKE?" (directive §9 Shift + Left Click universal info, §10 research
/// discoverability). One pure, read-only aggregation that answers the Director's questions about any inspectable
/// object, built ONLY from (a) content READ as authored — research.json nodes, entities (<c>requires</c> is the
/// authoritative direction; <c>unlocks</c> is its load-validated reverse index), <c>sectorActivities</c> and
/// <c>baseline</c>; goods.json goods, recipes and projects (<c>entity</c>, <c>founds</c>, inputs); sim.json road classes,
/// <c>trade.entity</c>, <c>governance.taxationRequires</c> / <c>taxationMinAge</c>; ages.json milestone facts;
/// unit-families.json <c>realizedBy</c> — and (b) values RECOMPUTED by the PUBLIC predicates the simulation itself
/// calls: <see cref="ResearchQuery"/> (IsAvailable, Prerequisites, IsKnowledgeEligible, RequirementMet),
/// <see cref="ConstructionQuery"/> (Availability, Blocker), <see cref="CraftingQuery"/> (IsKnownBy, IsRecipeAvailable,
/// Variable), <see cref="LabourActivities"/> (For, HarvestsWildFood), <see cref="Governance.GateOf"/>,
/// <see cref="AgeQuery"/> (Milestone, Evaluate), <see cref="RoadDevelopmentQuery.IsClassKnown"/>,
/// <see cref="TradeQuery.KnowsTrade"/>, <see cref="AvailableActionsQuery.For"/> (the live action descriptor).
///
/// NO SECOND DATABASE: no id → text table, and no content id appears in this file (Sim.Tests InfoQueryTests scans
/// the source against every loaded id). I-23: a card REPORTS conditions — it never proposes a lever the simulation
/// lacks, and an "opens" entry nothing realizes is labelled "not built in this build".
///
/// A QUERY, NOT A SYSTEM (the AvailableActionsQuery precedent, ADR-033 D2): no state, no cache, never serialized,
/// consulted by no system, outside the pipeline. Deterministic: content and table order, no dictionary, no LINQ,
/// invariant formatting; the one argmax over doubles (a condition's best live value) is the composite key (value,
/// settlement id) with the lower id winning a tie.
/// </summary>
public static class InfoQuery
{
    /// <summary>The card for <paramref name="subject"/>, as <paramref name="player"/> sees it in <paramref name="world"/>.
    /// <paramref name="context"/> is the action query's (the next step's dt for the construction capacity blocker);
    /// <paramref name="settlementName"/> names settlements (display only); <paramref name="actions"/> may pass the
    /// player's <see cref="AvailableActionsQuery.For"/> result when the caller already holds it (it must be the one for
    /// this world, player and context).</summary>
    public static InfoCard Card(
        IReadOnlyWorldState world, SimConfig cfg, PolityId player, InfoSubject subject,
        ActionQueryContext? context = null, Func<int, string>? settlementName = null, ImmutableArray<ActionDescriptor>? actions = null)
    {
        ArgumentNullException.ThrowIfNull(world);
        ArgumentNullException.ThrowIfNull(cfg);
        var c = new Ctx(world, cfg, player, context ?? ActionQueryContext.None, settlementName, actions);
        var b = new B(subject);
        switch (subject.Kind)
        {
            case InfoKind.ResearchNode: NodeCard(c, b); break;
            case InfoKind.Entity: EntityCard(c, b); break;
            case InfoKind.Baseline: BaselineCard(c, b); break;
            case InfoKind.AgeMilestone: MilestoneCard(c, b); break;
            case InfoKind.Age: AgeCard(c, b); break;
            case InfoKind.LabourSector: SectorCard(c, b); break;
            case InfoKind.Good: GoodCard(c, b); break;
            case InfoKind.Project: ProjectCard(c, b); break;
            case InfoKind.Recipe: RecipeCard(c, b); break;
            case InfoKind.RoadClass: RoadCard(c, b); break;
            case InfoKind.Formation: FormationCard(c, b); break;
            case InfoKind.TaxEdict: TaxCard(c, b); break;
            case InfoKind.UniversityType: UniversityCard(c, b); break;
            case InfoKind.Learning: LearningCard(c, b); break;
            default: b.Unknown("this kind of object"); break;
        }
        return b.Build(c);
    }

    // ================================================================== public helpers (the UI's discoverability lines)

    /// <summary>
    /// THE NODE TO RESEARCH NEXT toward <paramref name="node"/> (a content index): the node itself when it is AVAILABLE,
    /// else the first AVAILABLE node met by a depth-first walk of its not-completed prerequisites in source order; -1
    /// when nothing on the way is open (the research stage closes it, or it is completed). Deterministic: source order.
    /// </summary>
    public static int NextResearchToward(ResearchContent content, bool[] completed, int node)
    {
        ArgumentNullException.ThrowIfNull(content);
        ArgumentNullException.ThrowIfNull(completed);
        if (node < 0 || node >= content.Nodes.Count) return -1;
        bool stage = ResearchQuery.StageReached(content, completed);
        var seen = new bool[content.Nodes.Count];
        return Walk(node);

        int Walk(int n)
        {
            if (seen[n] || completed[n]) return -1;
            seen[n] = true;
            if (ResearchQuery.IsAvailable(content, n, completed, stage)) return n;
            foreach (int p in content.Nodes[n].PrerequisiteNodes)
            {
                int r = Walk(p);
                if (r >= 0) return r;
            }
            return -1;
        }
    }

    /// <summary>
    /// A D-020 condition as the player reads it: the content's own text, then each registered variable it reads in
    /// plain words with its LIVE value — the best (highest) reading across the issuer's settlements, the composite key
    /// (value, settlement id) so a tie goes to the lower id — and each <c>stock_&lt;good&gt;</c> quantity with the most
    /// held. E.g. "artisan_share > 0.05 - share of the adults who are artisans: 0.012 at best (Bigen)".
    /// </summary>
    public static string ConditionReading(
        IReadOnlyWorldState world, SimConfig cfg, PolityId polity, string source, Func<int, string>? settlementName = null)
    {
        ArgumentNullException.ThrowIfNull(world);
        ArgumentNullException.ThrowIfNull(cfg);
        if (string.IsNullOrEmpty(source)) return source ?? "";
        SettlementId[] mine = LabourActivities.ControlledSettlements(world, polity);
        var parts = new List<string>();
        for (int v = 0; v < Variables.Names.Length; v++)
        {
            if (!HasToken(source, Variables.Names[v])) continue;
            (double best, int at) = BestVariable(world, mine, v + 1);
            parts.Add(Variables.Describe(v + 1) + ": " + (at < 0 ? "no settlement of yours" : Num3(best) + " at best ("
                + (settlementName is null ? "settlement " + Inv(at) : settlementName(at)) + ")"));
        }
        if (cfg.Goods is { } goods && cfg.Research is { } research)
            for (int q = 0; q < research.QuantityGoods.Count; q++)
            {
                int good = research.QuantityGoods[q].Value;
                string name = GoodName(goods, good);
                // The research dialect's quantity name (ResearchContentLoader: "stock_" + the good's name, '-' as '_').
                if (!HasToken(source, "stock_" + name.Replace('-', '_'))) continue;
                long held = 0;
                foreach (SettlementId s in mine) held = Math.Max(held, Held(world, s, good));
                parts.Add(name + " held: " + Inv(held) + " at most in one settlement");
            }
        return parts.Count == 0 ? source : source + " - " + string.Join("; ", parts);
    }

    /// <summary>The best (highest) value of variable <paramref name="varId"/> across <paramref name="settlements"/>,
    /// and where: the composite key (value, settlement id), a tie going to the LOWER id. (-inf, -1) when none.</summary>
    public static (double Value, int Settlement) BestVariable(IReadOnlyWorldState world, IReadOnlyList<SettlementId> settlements, int varId)
    {
        double best = double.NegativeInfinity;
        int at = -1;
        foreach (SettlementId s in settlements)
        {
            double x = CraftingQuery.Variable(world, s, varId);
            if (at < 0 || x > best || (x == best && s.Value < at)) { best = x; at = s.Value; }
        }
        return (best, at);
    }

    /// <summary>
    /// WHERE A GOOD COMES FROM, as one line ("tools &lt;- Toolmaking &lt;- bronze &lt;- Bronze casting &lt;- Tin bronze
    /// (research, Age III)"): read from goods.json (which recipe outputs it, its inputs) and recomputed knowledge
    /// (CraftingQuery.IsKnownBy) and holdings (deposits, stocks in the issuer's settlements). It follows the first
    /// input that has no source, stopping at a good the issuer can obtain (a deposit or a stock), at a recipe whose
    /// research is missing (naming the node), or at a good nothing produces. Null when the good is obtainable now.
    /// </summary>
    public static string? SourceChain(IReadOnlyWorldState world, SimConfig cfg, PolityId polity, string good)
    {
        var c = new Ctx(world, cfg, polity, ActionQueryContext.None, null, null);
        int target = -1;
        return Chain(c, good, 0, ref target);
    }

    /// <summary>The research node the <see cref="SourceChain"/> of <paramref name="good"/> ends at (content index), or -1.</summary>
    public static int SourceChainNode(IReadOnlyWorldState world, SimConfig cfg, PolityId polity, string good)
    {
        var c = new Ctx(world, cfg, polity, ActionQueryContext.None, null, null);
        int target = -1;
        Chain(c, good, 0, ref target);
        return target;
    }

    // ================================================================== research node

    private static void NodeCard(Ctx c, B b)
    {
        if (c.R is not { } r) { b.Inert("research content"); return; }
        int i = r.IndexOf(new ResearchNodeId((int)b.Subject.Id));
        if (i < 0) { b.Unknown("a research node with this key"); return; }
        ResearchNode n = r.Nodes[i];
        b.Title = n.Name;
        b.Kind = Where(r, n) + " research - " + AgeText(c, n.Age);
        b.What = n.Description;
        b.TreeNode = i;

        bool done = c.Done[i], available = c.Available(i);
        bool target = ResearchQuery.TryGetTarget(c.W, c.P, out ResearchNodeId t) && t.Value == n.Key.Value;
        double cost = ResearchQuery.EffectiveCost(c.W, r, c.P, i);
        double rp = ResearchQuery.ResearchPointPool(c.W, r, c.P);
        if (done) { b.Status = InfoStatus.Known; b.StatusLine = "Known: your scholars have completed it."; }
        else if (target && available)
        {
            b.Status = InfoStatus.Researching;
            b.StatusLine = "Being researched: " + N0(ResearchQuery.Progress(c.W, c.P, n.Key)) + " of " + N0(cost) + " research points, +" + N1(rp) + " a turn.";
        }
        else if (available)
        {
            b.Status = InfoStatus.Researchable;
            b.StatusLine = "Open to research now: " + N0(cost) + " research points" + (rp > 0 ? " (about " + Inv((long)Math.Ceiling(Math.Max(0.0, cost - ResearchQuery.Progress(c.W, c.P, n.Key)) / rp)) + " turns at the current rate)." : ".");
        }
        else { b.Status = InfoStatus.Locked; b.StatusLine = "Locked: " + N0(cost) + " research points once it opens."; }

        // ENABLED BY: its prerequisites (each with its completion), the expression verbatim.
        IReadOnlyList<int> must = n.Prerequisite?.MustHoldAtoms() ?? [];
        foreach (int p in n.PrerequisiteNodes) b.EnabledBy.Add(NodeLink(c, p, Contains(must, p) ? "required" : "one of"));
        if (n.PrerequisiteNodes.Count == 0) b.EnabledBy.Add(new InfoLink("No prerequisite: a root of its tree", null, true));
        b.RequirementSource = n.Prerequisite?.Source;

        // WHY LOCKED: each prerequisite not yet completed; the research stage; a recursive node's subtree.
        if (!done && !available)
        {
            if (!ResearchQuery.PrerequisitesMet(r, i, c.Done))
                foreach (int p in n.PrerequisiteNodes)
                    if (!c.Done[p]) b.WhyLocked.Add(NodeLink(c, p, Contains(must, p) ? "required" : "one of"));
            if (n.Branch >= 0 && !c.Stage)
                b.WhyLocked.Add(new InfoLink("Its subtree is closed until the research stage is reached (" + r.Stage.Source.Replace('_', ' ') + ")", null, false));
            if (n.IsRecursive && n.Branch >= 0 && !ResearchQuery.SubtreeExhausted(r, n.Branch, c.Done))
                b.WhyLocked.Add(new InfoLink("Recursive: it waits for every finite node of its subtree", null, false));
        }

        // WHAT IT ENABLES: entities (each with what realizes it), dependents, Age milestones, the tax edict, the stage.
        bool simulated = false;
        foreach (int e in n.UnlockedEntities)
        {
            (string note, bool realized) = Realization(c, e);
            simulated |= realized;
            b.Enables.Add(new InfoLink(EntityName(r.Entities[e]), InfoSubject.OfEntity(r.Entities[e].Id), c.Eligible(e), KindName(r.Entities[e].Kind) + " - " + note));
        }
        foreach ((int age, AgeMilestone m) in MilestonesNaming(c, n.Key.Value))
        {
            simulated = true;
            b.Enables.Add(new InfoLink(m.Name, InfoSubject.Milestone(age, m.Id), AgeQuery.Milestone(c.W, c.P, m).Met,
                "Age " + Numeral(age) + " " + (m.Core ? "core" : "supporting") + " milestone"));
        }
        if (TaxAtoms(c) is { } tax && Contains(tax.AtomIds, i))
        {
            simulated = true;
            b.Enables.Add(new InfoLink("Tax edict", InfoSubject.Tax, Governance.CanLevyTax(c.W, c.Cfg, c.P), TaxAgeNote(c)));
        }
        if (Contains(r.Stage.AtomIds, i))
        {
            simulated = true;
            b.Enables.Add(new InfoLink("The research stage (opens the five specialised subtrees together)", null, c.Stage, r.Stage.Source.Replace('_', ' ')));
        }
        foreach (int d in n.Dependents) b.Enables.Add(NodeLink(c, d, "leads to"));

        foreach (string s in n.Capabilities) b.KnowledgeOnly.Add(s);
        foreach (string s in n.Techniques) b.KnowledgeOnly.Add(s);
        foreach (string s in n.Applications) b.KnowledgeOnly.Add(s);

        b.Effect = simulated ? InfoEffect.Simulated : n.Dependents.Count > 0 ? InfoEffect.ResearchOnly : InfoEffect.KnowledgeOnly;
        b.RealizedBy = b.Effect switch
        {
            InfoEffect.KnowledgeOnly => "Knowledge only - no simulated effect in this build: nothing the simulation runs reads it, and it opens no further research.",
            InfoEffect.ResearchOnly => "No simulated effect of its own in this build: it opens further research (listed under what it enables).",
            _ => "Read by the simulation: see what it enables.",
        };

        if (Find(c, ActionDomain.Research, 1) is { } set)
            foreach (ActionTarget at in set.Targets)
                if (at.Kind == ActionTargetKind.ResearchNode && at.Id == n.Key.Value) { b.Action = set; break; }
    }

    // ================================================================== registry entity

    private static void EntityCard(Ctx c, B b)
    {
        if (c.R is not { } r) { b.Inert("research content"); return; }
        int e = b.Subject.Ref is { } id ? r.EntityIndexOf(id) : -1;
        if (e < 0) { b.Unknown("a research entity with this id"); return; }
        ResearchEntity ent = r.Entities[e];
        bool ok = c.Eligible(e);
        b.Title = EntityName(ent);
        b.Kind = KindName(ent.Kind);
        b.RequirementSource = ent.Requirement?.Source;
        b.Status = ok ? InfoStatus.Known : InfoStatus.Locked;

        EntityAtoms(c, e, b.EnabledBy, 0, new bool[r.Entities.Count], null);
        if (ent.Requirement is null) b.EnabledBy.Add(new InfoLink("No research needed: known from the start", null, true));
        foreach (string u in ent.Unresolved)
            b.WhyLocked.Add(new InfoLink("Names \"" + u + "\", which is not in this build's knowledge (it reads as not known)", null, false));
        if (!ok) EntityWhyLocked(c, e, b.WhyLocked, 0, new bool[r.Entities.Count]);
        b.TreeNode = EntityTreeTarget(c, e);
        b.StatusLine = ok
            ? ent.Requirement is null ? "Known from the start - no research needed." : "Known" + Provenance(c, e) + "."
            : "Not yet known.";

        var realized = new List<string>();
        bool unit = false;
        Consumers(c, ent, e, b.Enables, realized, b.Requirements, ref unit);
        b.RealizedBy = realized.Count > 0 ? "In this build: " + string.Join("; ", realized) + "."
            : unit ? "Recruitment, movement and battle arrive with the Battle Layer (M7): no system creates this unit in this build."
            : "Not built in this build - knowledge eligibility only: no system realizes it yet.";
        b.Effect = realized.Count > 0 ? InfoEffect.Simulated : InfoEffect.KnowledgeOnly;
        b.What = KindName(ent.Kind) + ". " + b.RealizedBy;
        if (!ok) b.Summary = "needs " + NeedsList(c, b.WhyLocked);

        if (c.Cfg.Trade.Entity is { } trade && string.Equals(trade, ent.Id, StringComparison.Ordinal))
            b.Action = Find(c, ActionDomain.Trade, 1);
    }

    // ================================================================== baseline capability

    private static void BaselineCard(Ctx c, B b)
    {
        if (c.R is not { } r) { b.Inert("research content"); return; }
        int bi = -1;
        for (int i = 0; i < r.Baseline.Count; i++)
            if (string.Equals(r.Baseline[i].Id, b.Subject.Ref, StringComparison.Ordinal)) { bi = i; break; }
        if (bi < 0) { b.Unknown("a baseline capability with this id"); return; }
        ResearchBaselineCapability cap = r.Baseline[bi];
        b.Title = cap.Name;
        b.Kind = "Baseline capability - every civilization starts with it";
        b.What = cap.ProvidedBy;
        b.Status = cap.Simulated ? InfoStatus.Known : InfoStatus.NotSimulated;
        b.StatusLine = cap.Simulated ? "Known from the start; it works on its own, with no order." : "Known from the start, but not simulated in this build.";
        b.EnabledBy.Add(new InfoLink("No research needed: a baseline capability", null, true));
        b.RealizedBy = cap.Simulated ? "Simulated: " + cap.ProvidedBy : "Not simulated in this build: " + cap.ProvidedBy;
        b.Effect = cap.Simulated ? InfoEffect.Simulated : InfoEffect.KnowledgeOnly;
        for (int s = 0; s < r.SectorActivities.Count; s++)
            foreach (int x in r.SectorActivities[s].Baseline.Baseline)
                if (x == bi)
                    b.Enables.Add(new InfoLink(r.SectorActivities[s].Baseline.Name, InfoSubject.Sector(s), true,
                        "the " + SectorWord(s) + " sector's activity before research changes it"));
        if (cap.Simulated) b.Action = Find(c, ActionDomain.Standing, bi);
    }

    // ================================================================== Age milestone

    private static void MilestoneCard(Ctx c, B b)
    {
        if (c.Cfg.Ages is not { } ages) { b.Inert("Age content"); return; }
        int age = (int)b.Subject.Id;
        if (age < 1 || age > AgeContent.AgeCount || ages.Age(age).Entry is not { } entry || FindMilestone(entry, b.Subject.Ref) is not { } m)
        { b.Unknown("an Age milestone with this id"); return; }
        MilestoneStatus st = AgeQuery.Milestone(c.W, c.P, m);
        b.Title = m.Name;
        b.Kind = "Age " + Numeral(age) + " (" + ages.Age(age).Name + ") " + (m.Core ? "core" : "supporting") + " milestone - "
            + ages.Category(m.Category).Name;
        b.What = m.Description;
        b.Status = st.Met ? InfoStatus.Met : m.Pending is not null ? InfoStatus.Pending : InfoStatus.NotMet;
        string count = st.Threshold > 1 ? Inv(st.Observed) + " of " + Inv(st.Threshold) : st.Met ? "done" : "not yet";
        b.StatusLine = st.Met ? (st.Threshold > 1 ? "Met (" + count + ")." : "Met.")
            : m.Pending is { } pend ? "Pending: " + pend
            : st.Threshold > 1 ? "Not yet met (" + count + ")." : "Not yet met.";
        b.Enables.Add(new InfoLink("Age " + Numeral(age) + " - " + ages.Age(age).Name, InfoSubject.OfAge(age), null,
            m.Core ? "a core milestone: every one is required to enter it" : "a supporting milestone: " + Inv(entry.SupportingRequired)
                + " supporting milestones, covering " + Inv(entry.MinCategories) + " categories, are required"));

        AgeMilestoneFact f = m.Fact;
        switch (f.Kind)
        {
            case MilestoneFactKind.Research when c.R is { } r:
            {
                int first = -1, open = -1;
                foreach (int key in f.NodeKeys)
                {
                    int i = r.IndexOf(new ResearchNodeId(key));
                    if (i < 0) continue;
                    b.EnabledBy.Add(NodeLink(c, i, f.NodeKeys.Count > 1 ? "any one of these" : "research"));
                    if (first < 0) first = i;
                    if (open < 0 && !c.Done[i]) open = i;
                }
                b.RequirementSource = "research: " + string.Join(" or ", f.NodeIds);
                b.TreeNode = open >= 0 ? open : first;
                if (!st.Met && open >= 0)
                {
                    b.WhyLocked.Add(NodeLink(c, open, "research it"));
                    int next = NextResearchToward(r, c.Done, open);
                    if (next >= 0)
                        b.More.Add(new InfoSection("Research next", [NodeLink(c, next, next == open ? "open now" : "open now, on the way to " + r.Nodes[open].Name)]));
                    b.Summary = "research " + r.Nodes[open].Name + (next >= 0 && next != open ? " (first " + r.Nodes[next].Name + ")" : "");
                }
                break;
            }
            case MilestoneFactKind.ResearchCount:
                b.EnabledBy.Add(new InfoLink("Completed research: " + Inv(f.Min) + " nodes" + (f.Tree is { } tree ? " of the " + tree + " tree" : ""), null, st.Met, Inv(st.Observed) + " so far"));
                break;
            case MilestoneFactKind.Structures:
                if (c.Cfg.Goods?.ProjectById(f.Ref) is { } project)
                {
                    b.EnabledBy.Add(new InfoLink(ProjectName(c, project), InfoSubject.OfProject(project.Id), st.Met, "build at least " + Inv(f.Min)));
                    b.TreeNode = ProjectTreeTarget(c, project, DefaultSettlement(c, -1));
                }
                else b.EnabledBy.Add(new InfoLink("Any completed structure: at least " + Inv(f.Min), null, st.Met));
                break;
            case MilestoneFactKind.GoodStock when c.Cfg.Goods is { } goods:
                b.EnabledBy.Add(new InfoLink(Cap(GoodName(goods, f.Ref)), InfoSubject.OfGood(f.Ref), st.Met,
                    "at least " + Inv(f.Min) + " held across your settlements (" + Inv(st.Observed) + " now)"));
                break;
            case MilestoneFactKind.Formations:
                b.EnabledBy.Add(new InfoLink("Formations realized in this Age or later", null, st.Met, m.Pending ?? "owned formations"));
                break;
            default:
                b.EnabledBy.Add(new InfoLink(FactWords(f), null, st.Met, Inv(st.Observed) + " now; measured by the " + f.Owner + " system"));
                break;
        }
        if (!st.Met && f.Kind != MilestoneFactKind.Research)
            b.WhyLocked.Add(new InfoLink(m.Pending is not null ? "Its mechanism is not built yet: " + m.Pending : "Not reached yet: " + count, null, false));
        b.Effect = InfoEffect.Simulated;
        b.RealizedBy = "Read by the Age evaluator (AgeQuery) from the " + f.Owner + " system's published state.";
        if (!st.Met && b.Summary is null) b.Summary = m.Pending is not null ? "pending: its mechanism is not built yet" : "not reached yet: " + count;
    }

    private static AgeMilestone? FindMilestone(AgeEntryRequirements entry, string? id)
    {
        foreach (AgeMilestone m in entry.Core) if (string.Equals(m.Id, id, StringComparison.Ordinal)) return m;
        foreach (AgeMilestone m in entry.Supporting) if (string.Equals(m.Id, id, StringComparison.Ordinal)) return m;
        return null;
    }

    private static string FactWords(AgeMilestoneFact f) => f.Kind switch
    {
        MilestoneFactKind.Population => "People in your settlements: at least " + Inv(f.Min),
        MilestoneFactKind.Settlements => "Settlements you control: at least " + Inv(f.Min),
        MilestoneFactKind.Dwellings => "Dwellings in your settlements: at least " + Inv(f.Min),
        MilestoneFactKind.ClassActive => "Settlements where the " + (f.RefName ?? "class") + " class is active: at least " + Inv(f.Min),
        MilestoneFactKind.TradeVolume => "Goods moved in or out of your settlements last turn: at least " + Inv(f.Min),
        _ => "At least " + Inv(f.Min),
    };

    // ================================================================== Age

    private static void AgeCard(Ctx c, B b)
    {
        if (c.Cfg.Ages is not { } ages) { b.Inert("Age content"); return; }
        int age = (int)b.Subject.Id;
        if (age < 1 || age > AgeContent.AgeCount) { b.Unknown("an Age with this key"); return; }
        int current = AgeQuery.CurrentAge(c.W, ages, c.P);
        AgeDefinition def = ages.Age(age);
        b.Title = "Age " + Numeral(age) + " - " + def.Name;
        b.Kind = "An Age of your civilization";
        b.What = age == ages.FoundingAge ? "The Age a civilization is founded in." : "Entered by the Advance Age order, once its entry milestones hold; never by a date.";
        b.Effect = InfoEffect.Simulated;
        if (age <= current)
        {
            b.Status = InfoStatus.Known;
            b.StatusLine = age == current ? "Your civilization is in this Age." : "Your civilization has passed through this Age.";
        }
        else if (age == current + 1)
        {
            AgeEligibilityReport? report = AgeQuery.Evaluate(c.W, ages, c.P);
            ActionDescriptor? advance = Find(c, ActionDomain.Age, age);
            b.Action = advance;
            b.Status = advance is not null ? InfoStatus.Available : report is { Eligible: true } ? InfoStatus.Met : InfoStatus.NotMet;
            b.StatusLine = advance is not null ? "Eligible: you may advance now (optional)."
                : report is { Eligible: true } ? "Eligible; an advance is already ordered this turn." : "Not yet eligible.";
            if (report is not null)
            {
                foreach (string line in report.Remaining) b.WhyLocked.Add(new InfoLink(line, null, false));
                if (!report.Eligible) b.Summary = "not yet eligible: " + string.Join("; ", report.Remaining);
            }
        }
        else
        {
            b.Status = InfoStatus.Locked;
            b.StatusLine = "Ages are entered one at a time: Age " + Numeral(current + 1) + " comes first.";
            b.Summary = "enter Age " + Numeral(current + 1) + " first";
        }
        if (def.Entry is { } entry)
        {
            foreach (AgeMilestone m in entry.Core)
                b.EnabledBy.Add(new InfoLink(m.Name, InfoSubject.Milestone(age, m.Id), AgeQuery.Milestone(c.W, c.P, m).Met, "core: required"));
            foreach (AgeMilestone m in entry.Supporting)
                b.EnabledBy.Add(new InfoLink(m.Name, InfoSubject.Milestone(age, m.Id), AgeQuery.Milestone(c.W, c.P, m).Met,
                    "supporting - " + ages.Category(m.Category).Name + (m.Pending is not null ? " (pending)" : "")));
            b.RequirementSource = "every core milestone, " + Inv(entry.SupportingRequired) + " supporting milestones, " + Inv(entry.MinCategories) + " categories covered";
        }
        if (c.Cfg.Governance?.TaxationMinAge is { } taxAge && taxAge == age)
            b.Enables.Add(new InfoLink("Tax edict", InfoSubject.Tax, Governance.CanLevyTax(c.W, c.Cfg, c.P), "operational from this Age, with its knowledge"));
        if (MilitaryQuery.Units(c.W, c.P).Length > 0 && age > current)
            b.Enables.Add(new InfoLink("Your formations modernize for free when you enter it", null, null, "unit-family lines"));
        b.RealizedBy = "Entered by the AdvanceAge order (AgeTransitionSystem) when AgeQuery finds the entry milestones met.";
        if (b.Status == InfoStatus.NotMet)
        {
            // The first unmet core research milestone points the tree at its node.
            foreach (InfoLink l in b.EnabledBy)
                if (l.Met == false && l.Subject is { } ms && FindMilestone(def.Entry!, ms.Ref) is { Core: true } m
                    && m.Fact.Kind == MilestoneFactKind.Research && c.R is { } r)
                {
                    foreach (int key in m.Fact.NodeKeys)
                    {
                        int i = r.IndexOf(new ResearchNodeId(key));
                        if (i >= 0 && !c.Done[i]) { b.TreeNode = i; break; }
                    }
                    if (b.TreeNode >= 0) break;
                }
        }
    }

    // ================================================================== labour sector

    private static void SectorCard(Ctx c, B b)
    {
        int sector = (int)b.Subject.Id;
        if (sector < 0 || sector >= Sectors.Count) { b.Unknown("a labour sector"); return; }
        int where = DefaultSettlement(c, b.Subject.Settlement);
        LabourActivity? a = where >= 0 ? LabourActivities.Of(c.W, c.Cfg, c.P, new SettlementId(where), sector) : null;
        ResearchContent? r = c.R;
        b.Title = a?.Label ?? (r is null ? ResearchContentLoader.SectorIds[sector] : r.SectorActivities[sector].Baseline.Name);
        b.Kind = "Labour - the " + SectorWord(sector) + " sector" + (where >= 0 ? " at " + c.Name(where) : "");
        b.Effect = InfoEffect.Simulated;
        b.RealizedBy = "ProductionSystem runs it with the share of labour you give it; research changes what it is called"
            + (sector == Sectors.Farming && c.Cfg.Farming.PreCultivation is { Enabled: true } ? " and, for farming, whether it harvests wild food or cultivated yields" : "") + ".";
        if (a is null)
        {
            b.Status = InfoStatus.Known;
            b.StatusLine = "You do not direct the labour here.";
        }
        else
        {
            b.Action = Find(c, ActionDomain.Labour, a.ActionId);
            b.Status = b.Action is null ? InfoStatus.Known : InfoStatus.Available;
            bool wild = sector == Sectors.Farming && LabourActivities.HarvestsWildFood(c.W, c.Cfg, new SettlementId(where));
            string makes = sector == Sectors.Construction ? "dwellings, paths and projects"
                : a.GoodNames.IsDefaultOrEmpty ? "nothing yet"
                : wild ? "wild food (" + string.Join(", ", a.GoodNames) + ")" : string.Join(", ", a.GoodNames);
            b.What = "Its labour makes " + makes + ".";
            b.StatusLine = Pct(a.Share) + " of the labour here.";
            if (sector == Sectors.Farming && c.Cfg.Farming.PreCultivation is { Enabled: true })
                b.StatusLine += LabourActivities.HarvestsWildFood(c.W, c.Cfg, new SettlementId(where))
                    ? " It harvests WILD food at forager rates: cultivated yields need farming knowledge."
                    : " Cultivated yields apply here.";
            foreach (GoodId g in a.Goods) b.Enables.Add(new InfoLink(Cap(GoodName(c.Cfg.Goods, g.Value)), InfoSubject.OfGood(g.Value), true, "made by this labour"));
            foreach (LabourIdentity id in a.Identities)
            {
                if (!id.Researched)
                    foreach (string bl in id.Baseline) b.EnabledBy.Add(BaselineLink(c, bl, id.Name + " - baseline"));
                else if (id.Entity is { } ent && r is not null && r.EntityIndexOf(ent) is int ei and >= 0)
                {
                    b.EnabledBy.Add(new InfoLink(id.Name, InfoSubject.OfEntity(ent), true, "activity, learned from " + string.Join(", ", id.NodeNames)));
                    for (int k = 0; k < id.Nodes.Length; k++)
                    {
                        int ni = r.IndexOf(id.Nodes[k]);
                        if (ni >= 0) b.EnabledBy.Add(NodeLink(c, ni, "made " + id.Name + " known"));
                    }
                }
            }
        }
        if (r is null) return;

        // What research can make of it: the researched identities not yet eligible, each with its requirement.
        SectorActivityContent map = r.SectorActivities[sector];
        var could = new List<InfoLink>();
        int firstEligible = -1, firstLocked = -1;
        foreach (SectorResearchedIdentity ri in map.Researched)
        {
            ResearchEntity ent = r.Entities[ri.Entity];
            if (c.Eligible(ri.Entity)) { if (firstEligible < 0) firstEligible = ri.Entity; continue; }
            if (firstLocked < 0) firstLocked = ri.Entity;
            var names = new List<string>();
            foreach (int atom in ent.NodeAtoms) names.Add(r.Nodes[atom].Name);
            could.Add(new InfoLink(ri.Label, InfoSubject.OfEntity(ent.Id), false,
                (ri.Mode == SectorActivityMode.Replaces ? "replaces " : "joins ") + map.Baseline.Name + " - needs "
                + (names.Count > 1 ? "one of: " : "") + string.Join(", ", names)));
        }
        if (could.Count > 0) b.More.Add(new InfoSection("Research can change it", [.. could]));
        b.TreeNode = firstEligible >= 0 ? EntityTreeTarget(c, firstEligible) : firstLocked >= 0 ? EntityTreeTarget(c, firstLocked) : -1;
    }

    // ================================================================== good

    private static void GoodCard(Ctx c, B b)
    {
        if (c.Cfg.Goods is not { } goods) { b.Inert("goods content"); return; }
        GoodEntry? g = null;
        foreach (GoodEntry x in goods.Goods) if (x.Id == b.Subject.Id) { g = x; break; }
        if (g is null) { b.Unknown("a good with this id"); return; }
        SettlementId[] mine = c.Controlled;
        long held = 0;
        int deposits = 0;
        foreach (SettlementId s in mine)
        {
            held += Held(c.W, s, g.Id);
            if (Deposit(c.W, s, g.Id) > 0.0) deposits++;
        }
        b.Title = Cap(g.Name);
        b.Kind = "Good - " + g.Category;
        b.What = g.Numeraire ? "The staple food and the unit prices are counted in." : Cap(g.Category) + " good.";
        b.Status = InfoStatus.Known;
        b.StatusLine = "Held across your settlements: " + N0(held) + (g.DepositChannel is not null ? "; deposits in " + Inv(deposits) + " of " + Inv(mine.Length) : "") + ".";
        b.Effect = InfoEffect.Simulated;
        b.RealizedBy = "A stock the Ledger moves: produced, consumed, built with and traded by the simulation.";

        // WHERE IT COMES FROM.
        if (g.Id == goods.GrainId)
            b.EnabledBy.Add(new InfoLink(SectorLabel(c, Sectors.Farming), InfoSubject.Sector(Sectors.Farming), true, "the farming sector's harvest"));
        else if (g.DepositChannel is not null)
        {
            int sector = DepositSector(c, g.Id);
            b.EnabledBy.Add(new InfoLink(SectorLabel(c, sector), InfoSubject.Sector(sector), deposits > 0,
                "worked where deposits exist: " + Inv(deposits) + " of your " + Inv(mine.Length) + " settlement(s)"));
        }
        foreach (RecipeEntry rec in goods.Recipes)
        {
            if (!string.Equals(rec.Output.Good, g.Name, StringComparison.Ordinal)) continue;
            bool known = CraftingQuery.IsKnownBy(c.W, c.R, c.P, rec);
            b.EnabledBy.Add(new InfoLink(RecipeName(c, rec), InfoSubject.OfRecipe(rec.Name), known, known ? "a craft you know" : "a craft you do not know yet" + RecipeNeeds(c, rec)));
        }
        string? chain = held > 0 || deposits > 0 ? null : Chain(c, g.Name, 0, ref b.TreeNode);
        if (chain is not null && !ObtainableByKnownCraft(c, g.Name))
        {
            b.WhyLocked.Add(new InfoLink("Not obtainable now: " + chain, null, false));
            b.Summary = chain;
        }

        // WHAT USES IT.
        foreach (RecipeEntry rec in goods.Recipes)
            foreach (RecipeInput i in rec.Inputs)
                if (string.Equals(i.Good, g.Name, StringComparison.Ordinal))
                    b.Enables.Add(new InfoLink(RecipeName(c, rec), InfoSubject.OfRecipe(rec.Name), CraftingQuery.IsKnownBy(c.W, c.R, c.P, rec), "a craft input: " + Num(i.PerOutput) + " a batch"));
        foreach (ConstructionProjectEntry p in goods.Projects ?? [])
            foreach (ProjectInput i in p.Inputs)
                if (string.Equals(i.Good, g.Name, StringComparison.Ordinal))
                    b.Enables.Add(new InfoLink(ProjectName(c, p), InfoSubject.OfProject(p.Id), null, "a building material: " + Inv(i.Qty)));
        if (c.Cfg.Roads is { } roads)
            foreach (RoadClassConfig rc in roads.Classes)
                foreach (RoadMaterialConfig mat in rc.MaterialsPerKm)
                    if (string.Equals(mat.Good, g.Name, StringComparison.Ordinal))
                        b.Enables.Add(new InfoLink(RoadName(c, rc), InfoSubject.OfRoadClass(rc.EdgeType), null, "road material: " + Num(mat.Qty) + " a km"));
        if (c.Cfg.Ages is { } ages)
            for (int age = 1; age <= AgeContent.AgeCount; age++)
                if (ages.Age(age).Entry is { } entry)
                    foreach (AgeMilestone m in All(entry))
                        if (m.Fact.Kind == MilestoneFactKind.GoodStock && m.Fact.Ref == g.Id)
                            b.Enables.Add(new InfoLink(m.Name, InfoSubject.Milestone(age, m.Id), AgeQuery.Milestone(c.W, c.P, m).Met, "Age " + Numeral(age) + " milestone: " + Inv(m.Fact.Min) + " held"));
    }

    // ================================================================== construction project

    private static void ProjectCard(Ctx c, B b)
    {
        if (c.Cfg.Goods?.ProjectById((int)b.Subject.Id) is not { } p) { b.Unknown("a construction project with this id"); return; }
        int where = DefaultSettlement(c, b.Subject.Settlement);
        bool founds = InstitutionContent.FoundsInstitution(p);
        b.Title = ProjectName(c, p);
        b.Kind = "Construction project" + (founds ? " - founds an institution" : "") + (where >= 0 ? " at " + c.Name(where) : "");
        b.What = "Built through the construction queue from " + Materials(p) + " and " + Num(p.LaborRequired) + " adult-years of construction labour.";
        ProjectAvailability av = where >= 0 ? ConstructionQuery.Availability(c.W, c.Cfg, c.P, new SettlementId(where), p.Id) : ProjectAvailability.UnknownSettlement;
        string? blocker = av == ProjectAvailability.Available ? ConstructionQuery.Blocker(c.W, c.Cfg, new SettlementId(where), p, c.Q.NextDtYears) : null;
        b.Status = av switch
        {
            ProjectAvailability.Available => blocker is null ? InfoStatus.Available : InfoStatus.Blocked,
            ProjectAvailability.NotKnowledgeEligible => InfoStatus.Locked,
            _ => InfoStatus.Known,
        };
        b.StatusLine = av switch
        {
            ProjectAvailability.Available => blocker is null ? "Can be built now." : "You may order it; it waits until it " + blocker + ".",
            ProjectAvailability.NotKnowledgeEligible => "Not yet known.",
            ProjectAvailability.NotControlled => "You do not rule " + c.Name(where) + ".",
            _ => "No settlement to build it in.",
        };
        if (where >= 0)
        {
            ActionDescriptor? a = Find(c, founds ? ActionDomain.Institutions : ActionDomain.Construction, ((long)where << 32) | (uint)p.Id);
            b.Action = a;
        }

        // ENABLED BY: the project's entity (and the institution it founds), each expanded.
        if (c.R is { } r)
        {
            var ids = new List<string>(2);
            if (p.Entity is { } building) ids.Add(building);
            if (p.Founds is { } f) ids.Add(f.Entity);
            foreach (string id in ids)
            {
                int e = r.EntityIndexOf(id);
                if (e < 0) continue;
                b.EnabledBy.Add(EntityLink(c, e, KindName(r.Entities[e].Kind)));
                EntityAtoms(c, e, b.EnabledBy, 1, new bool[r.Entities.Count], EntityName(r.Entities[e]));
                if (r.Entities[e].Requirement is { } req) b.RequirementSource = b.RequirementSource is null ? req.Source : b.RequirementSource + " AND " + req.Source;
                if (!c.Eligible(e)) EntityWhyLocked(c, e, b.WhyLocked, 0, new bool[r.Entities.Count]);
            }
            if (b.EnabledBy.Count > 0 && b.RequirementSource is null)
                b.EnabledBy.Add(new InfoLink("No research needed: known from the start", null, true));
        }

        // REQUIREMENTS: materials (held in the settlement — construction draws its own stock), labour, viability.
        foreach (ProjectInput i in p.Inputs)
        {
            int good = c.Cfg.Goods!.IdOf(i.Good);
            long held = where >= 0 ? Held(c.W, new SettlementId(where), good) : 0;
            b.Requirements.Add(new InfoLink(Inv(i.Qty) + " " + i.Good, InfoSubject.OfGood(good), held >= i.Qty, "has " + Inv(held) + (where >= 0 ? " at " + c.Name(where) : "")));
            if (held < i.Qty && Chain(c, i.Good, 0, ref b.TreeNode) is { } chain)
                b.WhyLocked.Add(new InfoLink(Inv(i.Qty) + " " + i.Good + " (has " + Inv(held) + "): " + chain, InfoSubject.OfGood(good), false));
        }
        double? cap = where >= 0 && c.Q.NextDtYears is { } dt && dt > 0.0 ? ConstructionQuery.CapacityAdultYears(c.W, c.Cfg, new SettlementId(where), dt) : null;
        b.Requirements.Add(new InfoLink(Num(p.LaborRequired) + " adult-years of construction labour", InfoSubject.Sector(Sectors.Construction, where),
            cap is { } cy ? cy >= p.LaborRequired : null, cap is { } cz ? "about " + Num(cz) + " this turn (an estimate)" : "the construction share of labour"));
        if (founds && where >= 0 && InstitutionViability.ToFound(c.W, c.Cfg, new SettlementId(where)) is { Viable: false } v)
            b.Requirements.Add(new InfoLink("A town able to sustain it", null, false, "needs " + InstitutionViability.Shortfall(v)));
        if (blocker is not null && b.WhyLocked.Count == 0) b.WhyLocked.Add(new InfoLink("Waits: " + blocker, null, false));

        // WHAT IT ENABLES: exactly what reads a built structure in this build.
        b.Effect = InfoEffect.Simulated;
        if (c.Cfg.Ages is { } ages)
            for (int age = 1; age <= AgeContent.AgeCount; age++)
                if (ages.Age(age).Entry is { } entry)
                    foreach (AgeMilestone m in All(entry))
                        if (m.Fact.Kind == MilestoneFactKind.Structures && (m.Fact.Ref == p.Id || m.Fact.Ref < 0))
                            b.Enables.Add(new InfoLink(m.Name, InfoSubject.Milestone(age, m.Id), AgeQuery.Milestone(c.W, c.P, m).Met,
                                "Age " + Numeral(age) + " " + (m.Core ? "core" : "supporting") + " milestone (" + Inv(m.Fact.Min) + " built)"));
        if (founds)
        {
            int type = InstitutionContent.TypeKeyOf(c.R, p);
            if (InstitutionContent.TypeOf(c.R, type) is { } ut)
                b.Enables.Add(new InfoLink(ut.Name, InfoSubject.OfUniversity(ut.Key), null, "the institution it founds once its building stands"));
        }
        else
            b.Enables.Add(new InfoLink("Public services under a levy", null, null,
                "once a tax levy exists, each one built eases how heavily the levy is felt here, as far as your administration reaches"));
        b.RealizedBy = "ConstructionSystem builds it from the queue. A built structure holds no goods - it is counted, not stocked - and in this build only what is listed under what it enables reads it.";
        if (b.Status is InfoStatus.Locked or InfoStatus.Blocked) b.Summary = b.Status == InfoStatus.Locked ? "needs " + NeedsList(c, b.WhyLocked) : blocker;
        if (b.TreeNode < 0) b.TreeNode = ProjectTreeTarget(c, p, where);
    }

    private static int ProjectTreeTarget(Ctx c, ConstructionProjectEntry p, int where)
    {
        if (c.R is not { } r) return -1;
        foreach (string? id in new[] { p.Entity, p.Founds?.Entity })
            if (id is not null && r.EntityIndexOf(id) is int e and >= 0 && EntityTreeTarget(c, e) is int t and >= 0) return t;
        foreach (ProjectInput i in p.Inputs)
        {
            long held = where >= 0 ? Held(c.W, new SettlementId(where), c.Cfg.Goods!.IdOf(i.Good)) : 0;
            if (held >= i.Qty) continue;
            int target = -1;
            if (Chain(c, i.Good, 0, ref target) is not null && target >= 0) return target;
        }
        return -1;
    }

    // ================================================================== crafting recipe

    private static void RecipeCard(Ctx c, B b)
    {
        if (c.Cfg.Goods is not { } goods) { b.Inert("goods content"); return; }
        int ri = -1;
        for (int i = 0; i < goods.Recipes.Length; i++)
            if (string.Equals(goods.Recipes[i].Name, b.Subject.Ref, StringComparison.Ordinal)) { ri = i; break; }
        if (ri < 0) { b.Unknown("a recipe with this name"); return; }
        RecipeEntry rec = goods.Recipes[ri];
        bool known = CraftingQuery.IsKnownBy(c.W, c.R, c.P, rec);
        int runs = 0;
        foreach (SettlementId s in c.Controlled) if (CraftingQuery.IsRecipeAvailable(c.W, c.Cfg, s, rec)) runs++;
        b.Title = RecipeName(c, rec);
        b.Kind = "Craft - run by the crafting share of labour";
        b.What = RecipeText(rec) + ", " + Num(rec.LaborPerOutput) + " adult-years of crafting labour a batch.";
        b.Action = Find(c, ActionDomain.Production, ri + 1);
        b.Status = known ? InfoStatus.Known : InfoStatus.Locked;
        b.StatusLine = !known ? "Not yet known."
            : runs > 0 ? "Known; its condition holds in " + Inv(runs) + " of your " + Inv(c.Controlled.Length) + " settlement(s)."
            : "Known, but it runs in none of your settlements yet.";
        b.Effect = InfoEffect.Simulated;
        b.RealizedBy = "ProductionSystem runs it with the crafting share of labour where it is known and its condition holds; its output is capped by the inputs at hand.";

        if (c.R is { } r && rec.Entity is { } eid && r.EntityIndexOf(eid) is int e and >= 0)
        {
            EntityAtoms(c, e, b.EnabledBy, 0, new bool[r.Entities.Count], null);
            if (r.Entities[e].Requirement is null)
                foreach (int bl in r.SectorActivities[Sectors.Crafting].Baseline.Baseline)
                    b.EnabledBy.Add(BaselineLink(c, r.Baseline[bl].Id, "known from the start: " + r.SectorActivities[Sectors.Crafting].Baseline.Name));
            b.RequirementSource = r.Entities[e].Requirement?.Source;
            if (!known) EntityWhyLocked(c, e, b.WhyLocked, 0, new bool[r.Entities.Count]);
            b.TreeNode = EntityTreeTarget(c, e);
        }

        // INPUTS and the D-020 condition (with its live value).
        bool anyMissing = false;
        foreach (RecipeInput i in rec.Inputs)
        {
            int good = goods.IdOf(i.Good);
            bool obtainable = Obtainable(c, i.Good);
            b.Requirements.Add(new InfoLink(Num(i.PerOutput) + " " + i.Good + " a batch", InfoSubject.OfGood(good), obtainable,
                obtainable ? "at hand or obtainable" : "none at hand"));
            int target = -1;
            if (!obtainable && Chain(c, i.Good, 0, ref target) is { } chain)
            {
                anyMissing = true;
                b.WhyLocked.Add(new InfoLink("It cannot produce: " + chain, InfoSubject.OfGood(good), false));
                // A known craft whose input chain ends at research: the tree jump goes to that research.
                if (target >= 0 && (b.TreeNode < 0 || c.Done[b.TreeNode])) b.TreeNode = target;
            }
        }
        if (rec.Requires is { } cond)
            b.Requirements.Add(new InfoLink("Condition: " + ConditionReading(c.W, c.Cfg, c.P, cond, c.NameFn), null, runs > 0,
                runs > 0 ? "holds in " + Inv(runs) + " settlement(s)" : "holds in none of your settlements yet"));
        if (known && anyMissing) b.Summary = "known, but " + b.WhyLocked[^1].Label.Replace("It cannot produce: ", "cannot produce: ");
        else if (!known) b.Summary = "needs " + NeedsList(c, b.WhyLocked);

        int output = goods.IdOf(rec.Output.Good);
        b.Enables.Add(new InfoLink(Cap(rec.Output.Good), InfoSubject.OfGood(output), null, Inv(rec.Output.Qty) + " a batch"));
        foreach (ConstructionProjectEntry p in goods.Projects ?? [])
            foreach (ProjectInput i in p.Inputs)
                if (string.Equals(i.Good, rec.Output.Good, StringComparison.Ordinal))
                    b.Enables.Add(new InfoLink(ProjectName(c, p), InfoSubject.OfProject(p.Id), null, "needs " + Inv(i.Qty) + " " + i.Good));
        foreach (RecipeEntry other in goods.Recipes)
            foreach (RecipeInput i in other.Inputs)
                if (string.Equals(i.Good, rec.Output.Good, StringComparison.Ordinal))
                    b.Enables.Add(new InfoLink(RecipeName(c, other), InfoSubject.OfRecipe(other.Name), CraftingQuery.IsKnownBy(c.W, c.R, c.P, other), "uses " + i.Good));
    }

    // ================================================================== road class

    private static void RoadCard(Ctx c, B b)
    {
        if (c.Cfg.Roads?.ClassOf((int)b.Subject.Id) is not { } rc) { b.Unknown("a road class with this edge type"); return; }
        b.Title = RoadName(c, rc);
        b.Kind = "Road class (infrastructure)";
        b.What = "Routes of this class travel at x" + (1.0 / rc.SpeedFactor).ToString("0.00", CultureInfo.InvariantCulture)
            + " the speed of a dirt path and carry " + N0(rc.CapacityTonnesPerYear) + " t of freight a year.";
        b.Effect = InfoEffect.Simulated;
        b.RealizedBy = rc.Entity is null
            ? "The free baseline: paths form from use (PathBuildSystem); nothing is built."
            : "RoadDevelopmentSystem builds and modernizes routes to it through the Develop roads order, from real materials; faster routes shorten travel, which also extends your administration's reach.";
        foreach (RoadMaterialConfig m in rc.MaterialsPerKm)
        {
            int good = c.Cfg.Goods?.IdOf(m.Good) ?? -1;
            b.Requirements.Add(new InfoLink(Num(m.Qty) + " " + m.Good + " a km", good >= 0 ? InfoSubject.OfGood(good) : null, null, "paid by your settlements nearest the route"));
        }
        if (rc.Entity is null || c.R is not { } r)
        {
            b.Status = InfoStatus.Known;
            b.StatusLine = "Known from the start.";
            b.EnabledBy.Add(new InfoLink("No research needed: the free baseline", null, true));
            return;
        }
        int e = r.EntityIndexOf(rc.Entity);
        bool known = RoadDevelopmentQuery.IsClassKnown(c.W, r, c.Cfg.Roads!, c.P, rc.EdgeType);
        if (e >= 0)
        {
            EntityAtoms(c, e, b.EnabledBy, 0, new bool[r.Entities.Count], null);
            b.RequirementSource = r.Entities[e].Requirement?.Source;
            if (!known) EntityWhyLocked(c, e, b.WhyLocked, 0, new bool[r.Entities.Count]);
            b.TreeNode = EntityTreeTarget(c, e);
            b.Enables.Add(new InfoLink(EntityName(r.Entities[e]), InfoSubject.OfEntity(rc.Entity), known, "its knowledge entity"));
        }
        b.Action = Find(c, ActionDomain.Roads, rc.EdgeType);
        b.Status = b.Action is { } a ? (a.Blocker is null ? InfoStatus.Available : InfoStatus.Blocked) : known ? InfoStatus.Known : InfoStatus.Locked;
        b.StatusLine = b.Action is { } a2 ? (a2.Blocker is null ? "You may develop roads to this class now." : "You may order it; " + a2.Blocker + ".")
            : known ? "Known; no route is offered for it now (a better class is known, or no route is eligible)." : "Not yet known.";
        if (!known) b.Summary = "needs " + NeedsList(c, b.WhyLocked);
        else if (b.Action?.Blocker is { } blk) b.Summary = blk;
    }

    // ================================================================== formation

    private static void FormationCard(Ctx c, B b)
    {
        MilitaryUnitRow? found = null;
        for (int i = 0; i < c.W.MilitaryUnits.Count; i++)
            if (c.W.MilitaryUnits[i].Id == b.Subject.Id) { found = c.W.MilitaryUnits[i]; break; }
        if (found is not { } unit) { b.Unknown("a formation with this id"); return; }
        UnitFamilyContent? fam = c.Cfg.UnitFamilies;
        UnitIdentity? identity = fam?.IdentityByKey(unit.Identity);
        UnitFamily? family = fam?.FamilyByKey(unit.Family);
        bool mine = unit.Owner.Value == c.P.Value;
        b.Title = identity?.Name ?? "Formation";
        b.Kind = "Military formation - " + (family?.Name ?? "family " + Inv(unit.Family)) + (mine ? "" : " (a rival's)");
        b.What = (family is not null ? family.Role.TrimEnd('.') + ". " : "") + (identity is not null ? "Its Age " + Numeral(identity.Age) + " form." : "");
        b.Status = InfoStatus.Known;
        b.StatusLine = (unit.Location.Value >= 0 ? "Stationed at " + c.Name(unit.Location.Value) + ". " : "In the field. ")
            + "It cannot be moved or given orders until the Battle Layer (M7).";
        b.Effect = InfoEffect.Simulated;
        b.RealizedBy = "Recruitment, movement and battle arrive with the Battle Layer (M7). In this build a formation exists from the founding and modernizes for free when its civilization enters a new Age (AgeTransitionSystem).";
        if (identity?.RealizedBy is { } rb && c.R is { } r && r.EntityIndexOf(rb) is int e and >= 0)
        {
            b.EnabledBy.Add(EntityLink(c, e, "the knowledge of this unit"));
            b.TreeNode = EntityTreeTarget(c, e);
        }
        else b.EnabledBy.Add(new InfoLink("No research: every civilization is founded with it", null, true));
        if (family is not null)
        {
            var line = new List<InfoLink>();
            foreach (UnitIdentity i in family.Line)
                if (!i.Branch) line.Add(new InfoLink(i.Name, null, unit.Identity == i.Key ? true : null, "Age " + Numeral(i.Age) + " form" + (unit.Identity == i.Key ? " (now)" : "")));
            if (line.Count > 0) b.More.Add(new InfoSection("Its line across the Ages", [.. line]));
        }
        if (c.Cfg.Ages is { } ages)
            for (int age = 1; age <= AgeContent.AgeCount; age++)
                if (ages.Age(age).Entry is { } entry)
                    foreach (AgeMilestone m in All(entry))
                        if (m.Fact.Kind == MilestoneFactKind.Formations && (m.Fact.Ref < 0 || m.Fact.Ref == unit.Family))
                            b.Enables.Add(new InfoLink(m.Name, InfoSubject.Milestone(age, m.Id), AgeQuery.Milestone(c.W, c.P, m).Met,
                                "Age " + Numeral(age) + " milestone" + (m.Pending is not null ? " - pending: Battle Layer (M7)" : "")));
        if (mine && Find(c, ActionDomain.Military, 1) is { } fighting)
            foreach (ActionTarget t in fighting.Targets)
                if (t.Kind == ActionTargetKind.Formation && t.Id == unit.Id) { b.Action = fighting; break; }
    }

    // ================================================================== tax edict

    private static void TaxCard(Ctx c, B b)
    {
        b.Title = "Tax edict";
        b.Kind = "Governance - an order: a declared levy on what your people produce";
        b.What = "A levy raises what your people produce and weighs on them; it reaches only as far as your administration does, and its burden builds over turns.";
        b.Effect = InfoEffect.Simulated;
        b.RealizedBy = "GovernanceSystem applies the declared levy (SetTaxRate) only while Governance.CanLevyTax holds - the same predicate this card reads.";
        TaxGate gate = Governance.GateOf(c.W, c.Cfg, c.P);
        if (gate == TaxGate.Inert || c.R is not { } r || c.Cfg.Governance is not { } gov) { b.Inert("governance and research content"); return; }
        Predicate req = TaxAtoms(c)!;
        int open = -1, first = -1;
        IReadOnlyList<int> must = req.MustHoldAtoms();
        foreach (int a in req.AtomIds)
        {
            if (a < 0 || a >= r.Nodes.Count) continue;
            b.EnabledBy.Add(NodeLink(c, a, Contains(must, a) ? "required" : "one of"));
            if (first < 0) first = a;
            if (open < 0 && !c.Done[a]) open = a;
        }
        int current = c.Cfg.Ages is { } ages ? AgeQuery.CurrentAge(c.W, ages, c.P) : 0;
        if (gov.TaxationMinAge is { } minAge)
            b.EnabledBy.Add(new InfoLink(AgeName(c, minAge), InfoSubject.OfAge(minAge), Governance.MeetsTaxationAge(c.W, c.Cfg, c.P),
                "the Age the edict needs (you are in Age " + Numeral(current) + ")"));
        b.RequirementSource = gov.TaxationRequires + (gov.TaxationMinAge is { } m0 ? " and Age " + Inv(m0) : "");
        b.TreeNode = open >= 0 ? open : first;
        switch (gate)
        {
            case TaxGate.NeedsKnowledge:
                b.Status = InfoStatus.Locked;
                b.StatusLine = "Not available: the knowledge is missing.";
                foreach (int a in req.AtomIds)
                    if (a >= 0 && a < r.Nodes.Count && !c.Done[a]) b.WhyLocked.Add(NodeLink(c, a, "research it"));
                if (!Governance.MeetsTaxationAge(c.W, c.Cfg, c.P) && gov.TaxationMinAge is { } m1)
                    b.WhyLocked.Add(new InfoLink("and enter " + AgeName(c, m1), InfoSubject.OfAge(m1), false, "you are in Age " + Numeral(current)));
                b.Summary = "needs " + TaxNeeds(c, req) + (gov.TaxationMinAge is { } m2 && !Governance.MeetsTaxationAge(c.W, c.Cfg, c.P) ? " and " + AgeName(c, m2) : "");
                break;
            case TaxGate.NeedsAge:
                b.Status = InfoStatus.NeedsAge;
                b.StatusLine = "Your scholars know it, but no levy can be raised before " + AgeName(c, gov.TaxationMinAge ?? 0) + ".";
                b.WhyLocked.Add(new InfoLink("Enter " + AgeName(c, gov.TaxationMinAge ?? 0), InfoSubject.OfAge(gov.TaxationMinAge ?? 0), false, "you are in Age " + Numeral(current)));
                b.Summary = TaxNeeds(c, req) + " is known; needs " + AgeName(c, gov.TaxationMinAge ?? 0);
                break;
            default:
                b.Action = Find(c, ActionDomain.Governance, 1);
                b.Status = InfoStatus.Available;
                b.StatusLine = Governance.HasPolicy(c.W, c.P)
                    ? "In force: a levy of " + Pct(Governance.NominalTaxRate(c.W, c.P)) + " is declared."
                    : "Available: no levy declared yet.";
                break;
        }
    }

    private static string TaxNeeds(Ctx c, Predicate req)
    {
        var names = new List<string>();
        foreach (int a in req.AtomIds)
            if (a >= 0 && a < c.R!.Nodes.Count) names.Add(c.R.Nodes[a].Name + " (" + TreeWord(c.R.Nodes[a]) + ")");
        return string.Join(" and ", names);
    }

    private static string TaxAgeNote(Ctx c) =>
        c.Cfg.Governance?.TaxationMinAge is { } m ? "the levy also needs " + AgeName(c, m) : "the levy";

    // ================================================================== university type

    private static void UniversityCard(Ctx c, B b)
    {
        if (c.R is not { } r || InstitutionContent.TypeOf(r, (int)b.Subject.Id) is not { } ut) { b.Unknown("a university type with this key"); return; }
        b.Title = ut.Name;
        b.Kind = "Institution - a specialised university (" + r.Branches[ut.Branch].Name + ")";
        b.What = "Founded when its building is completed in a town able to sustain it; as it matures it makes research in "
            + r.Branches[ut.Branch].Name + " cheaper.";
        b.Effect = InfoEffect.Simulated;
        b.RealizedBy = "ConstructionSystem builds its building; InstitutionsSystem founds it and matures it; ResearchSystem reads the cost factor.";
        int count = 0;
        foreach (InstitutionInstanceView v in InstitutionsQuery.Instances(c.W, c.Cfg, c.P)) if (v.TypeKey == ut.Key) count++;
        if (InstitutionContent.ProjectOfType(c.Cfg, ut.Key) is { } p)
        {
            int where = DefaultSettlement(c, b.Subject.Settlement);
            b.Enables.Add(new InfoLink(ProjectName(c, p), InfoSubject.OfProject(p.Id, where), null, "its founding project"));
            var inner = new B(InfoSubject.OfProject(p.Id, where));
            ProjectCard(c, inner);
            b.EnabledBy.AddRange(inner.EnabledBy);
            b.Requirements.AddRange(inner.Requirements);
            b.WhyLocked.AddRange(inner.WhyLocked);
            b.RequirementSource = inner.RequirementSource;
            b.TreeNode = inner.TreeNode;
            b.Action = inner.Action;
            b.Status = count > 0 ? InfoStatus.Known : inner.Status;
            b.StatusLine = count > 0 ? "Your empire has " + Inv(count) + "." : inner.StatusLine;
            b.Summary = count > 0 ? null : inner.Summary;
        }
        else { b.Status = count > 0 ? InfoStatus.Known : InfoStatus.Locked; b.StatusLine = count > 0 ? "Your empire has " + Inv(count) + "." : "No founding project in this build."; }
    }

    // ================================================================== research (the pool and its target)

    private static void LearningCard(Ctx c, B b)
    {
        if (c.R is not { } r) { b.Inert("research content"); return; }
        b.Title = "Research";
        b.Kind = "Knowledge - one pool of research points a turn, spent on one target";
        b.What = "Your people's research points come from your population each turn; they go to one research target, and progress is never lost when you stop or switch.";
        b.Effect = InfoEffect.Simulated;
        b.RealizedBy = "ResearchSystem spends the pool on the target you set (SetResearchTarget).";
        double rp = ResearchQuery.ResearchPointPool(c.W, r, c.P);
        int open = 0;
        for (int i = 0; i < r.Nodes.Count; i++) if (c.Available(i)) open++;
        if (ResearchQuery.TryGetTarget(c.W, c.P, out ResearchNodeId t) && r.IndexOf(t) is int ti and >= 0)
        {
            b.Status = InfoStatus.Researching;
            b.StatusLine = "Researching " + r.Nodes[ti].Name + ": " + N0(ResearchQuery.Progress(c.W, c.P, t)) + " of "
                + N0(ResearchQuery.EffectiveCost(c.W, r, c.P, ti)) + " research points, +" + N1(rp) + " a turn.";
            b.TreeNode = ti;
            b.Enables.Add(NodeLink(c, ti, "the current target"));
            b.Action = Find(c, ActionDomain.Research, 2);
        }
        else
        {
            b.Status = InfoStatus.Available;
            b.StatusLine = "Idle: " + N1(rp) + " research points a turn go unused; " + Inv(open) + " subjects are open.";
            b.Action = Find(c, ActionDomain.Research, 1);
            b.Summary = "idle - choose a research target";
        }
    }

    // ================================================================== shared: knowledge links and the tree target

    private static InfoLink NodeLink(Ctx c, int i, string? extra = null)
    {
        ResearchNode n = c.R!.Nodes[i];
        bool target = ResearchQuery.TryGetTarget(c.W, c.P, out ResearchNodeId t) && t.Value == n.Key.Value;
        string state = c.Done[i] ? "known" : target ? "being researched" : c.Available(i) ? "open to research now" : "locked";
        return new InfoLink(n.Name, InfoSubject.Node(n.Key), c.Done[i], TreeWord(n) + ", " + AgeText(c, n.Age) + " - " + state + (extra is null ? "" : "; " + extra));
    }

    private static InfoLink EntityLink(Ctx c, int e, string? note = null)
    {
        ResearchEntity ent = c.R!.Entities[e];
        bool ok = c.Eligible(e);
        return new InfoLink(EntityName(ent), InfoSubject.OfEntity(ent.Id), ok, (note is null ? "" : note + " - ") + (ok ? "known" : "not yet known"));
    }

    private static InfoLink BaselineLink(Ctx c, string id, string note)
    {
        string name = id;
        if (c.R is { } r) foreach (ResearchBaselineCapability cap in r.Baseline) if (string.Equals(cap.Id, id, StringComparison.Ordinal)) { name = cap.Name; break; }
        return new InfoLink(name, InfoSubject.OfBaseline(id), true, note);
    }

    /// <summary>An entity's requirement as links: its node atoms (source order), then its institution atoms, each
    /// expanded one level further (to a bounded depth; an institution named twice is expanded once).</summary>
    private static void EntityAtoms(Ctx c, int e, List<InfoLink> into, int depth, bool[] seen, string? via)
    {
        ResearchContent r = c.R!;
        ResearchEntity ent = r.Entities[e];
        IReadOnlyList<int> must = ent.Requirement?.MustHoldAtoms() ?? [];
        string prefix = via is null ? "" : "for " + via + "; ";
        foreach (int a in ent.NodeAtoms) into.Add(NodeLink(c, a, prefix + (Contains(must, a) ? "required" : "one of")));
        foreach (int inst in ent.InstitutionAtoms)
        {
            into.Add(EntityLink(c, inst, prefix + "an institution"));
            if (seen[inst] || depth >= 4) continue;
            seen[inst] = true;
            EntityAtoms(c, inst, into, depth + 1, seen, EntityName(r.Entities[inst]));
        }
    }

    /// <summary>Why an entity is not knowledge-eligible: each node atom not completed, each institution atom not
    /// eligible (and, beneath it, its own unmet atoms).</summary>
    private static void EntityWhyLocked(Ctx c, int e, List<InfoLink> into, int depth, bool[] seen)
    {
        ResearchContent r = c.R!;
        ResearchEntity ent = r.Entities[e];
        IReadOnlyList<int> must = ent.Requirement?.MustHoldAtoms() ?? [];
        foreach (int a in ent.NodeAtoms)
            if (!c.Done[a]) into.Add(NodeLink(c, a, (depth > 0 ? "for " + EntityName(ent) + "; " : "") + (Contains(must, a) ? "required" : "one of")));
        foreach (int inst in ent.InstitutionAtoms)
        {
            if (c.Eligible(inst) || seen[inst] || depth >= 4) continue;
            seen[inst] = true;
            into.Add(EntityLink(c, inst, "an institution it needs"));
            EntityWhyLocked(c, inst, into, depth + 1, seen);
        }
    }

    /// <summary>THE TREE TARGET of an entity (one deterministic rule): a KNOWN entity points at the first completed node
    /// of its requirement (its provenance); a locked one at the first node on its requirement that is open to research
    /// now, else at the first not-completed node, else the first. Institution atoms are expanded after the node atoms.
    /// -1 when the requirement names no node.</summary>
    private static int EntityTreeTarget(Ctx c, int e)
    {
        var nodes = new List<int>();
        Candidates(c, e, nodes, new bool[c.R!.Entities.Count], 0);
        if (nodes.Count == 0) return -1;
        if (c.Eligible(e)) { foreach (int n in nodes) if (c.Done[n]) return n; return nodes[0]; }
        foreach (int n in nodes) if (!c.Done[n] && c.Available(n)) return n;
        foreach (int n in nodes) if (!c.Done[n]) return n;
        return nodes[0];
    }

    private static void Candidates(Ctx c, int e, List<int> into, bool[] seen, int depth)
    {
        ResearchEntity ent = c.R!.Entities[e];
        foreach (int a in ent.NodeAtoms) if (!into.Contains(a)) into.Add(a);
        foreach (int inst in ent.InstitutionAtoms)
        {
            if (seen[inst] || depth >= 4) continue;
            seen[inst] = true;
            Candidates(c, inst, into, seen, depth + 1);
        }
    }

    private static string Provenance(Ctx c, int e)
    {
        var names = new List<string>();
        foreach (int a in c.R!.Entities[e].NodeAtoms) if (c.Done[a]) names.Add(c.R.Nodes[a].Name);
        return names.Count == 0 ? "" : " from " + string.Join(", ", names);
    }

    private static string NeedsList(Ctx c, List<InfoLink> why)
    {
        var parts = new List<string>();
        foreach (InfoLink l in why)
        {
            string part = l.Subject is { Kind: InfoKind.ResearchNode } s && c.R is { } r && r.IndexOf(new ResearchNodeId((int)s.Id)) is int i and >= 0
                ? l.Label + " (" + TreeWord(r.Nodes[i]) + ", " + AgeText(c, r.Nodes[i].Age) + ")"
                : l.Label;
            if (!parts.Contains(part)) parts.Add(part);
            if (parts.Count == 3) break;
        }
        return parts.Count == 0 ? "more knowledge" : string.Join(parts.Count == 2 ? " or " : ", ", parts) + (why.Count > 3 ? ", ..." : "");
    }

    /// <summary>What realizes an entity in this build — read from every content link that consumes an entity id.</summary>
    private static void Consumers(Ctx c, ResearchEntity ent, int e, List<InfoLink> enables, List<string> realized, List<InfoLink> reqs, ref bool unit)
    {
        ResearchContent r = c.R!;
        if (c.Cfg.Goods is { } goods)
        {
            foreach (ConstructionProjectEntry p in goods.Projects ?? [])
            {
                if (!string.Equals(p.Entity, ent.Id, StringComparison.Ordinal) && !string.Equals(p.Founds?.Entity, ent.Id, StringComparison.Ordinal)) continue;
                enables.Add(new InfoLink(ProjectName(c, p), InfoSubject.OfProject(p.Id), null, "construction project"));
                if (!realized.Contains("built through the construction queue")) realized.Add("built through the construction queue");
                if (reqs.Count == 0) foreach (ProjectInput i in p.Inputs) reqs.Add(new InfoLink(Inv(i.Qty) + " " + i.Good, InfoSubject.OfGood(goods.IdOf(i.Good)), null, "for " + ProjectName(c, p)));
            }
            foreach (RecipeEntry rec in goods.Recipes)
            {
                if (!string.Equals(rec.Entity, ent.Id, StringComparison.Ordinal)) continue;
                enables.Add(new InfoLink(RecipeName(c, rec), InfoSubject.OfRecipe(rec.Name), null, "a craft: " + RecipeText(rec)));
                realized.Add("a craft the crafting share of labour runs (" + RecipeName(c, rec) + ")");
                foreach (RecipeInput i in rec.Inputs) reqs.Add(new InfoLink(Num(i.PerOutput) + " " + i.Good + " a batch", InfoSubject.OfGood(goods.IdOf(i.Good)), Obtainable(c, i.Good), "craft input"));
            }
        }
        for (int s = 0; s < r.SectorActivities.Count; s++)
            foreach (SectorResearchedIdentity ri in r.SectorActivities[s].Researched)
            {
                if (ri.Entity != e) continue;
                enables.Add(new InfoLink(ri.Label, InfoSubject.Sector(s), c.Eligible(e),
                    "the " + SectorWord(s) + " sector's activity (" + (ri.Mode == SectorActivityMode.Replaces ? "replaces " : "joins ") + r.SectorActivities[s].Baseline.Name + ")"));
                realized.Add("the " + SectorWord(s) + " sector's activity" + (s == Sectors.Farming && ri.Mode == SectorActivityMode.Replaces
                    && c.Cfg.Farming.PreCultivation is { Enabled: true } ? ", with cultivated yields in place of wild-food gathering" : " (its name; production is unchanged)"));
            }
        if (c.Cfg.Roads is { } roads)
            foreach (RoadClassConfig rc in roads.Classes)
            {
                if (!string.Equals(rc.Entity, ent.Id, StringComparison.Ordinal)) continue;
                enables.Add(new InfoLink(RoadName(c, rc), InfoSubject.OfRoadClass(rc.EdgeType), null, "road class"));
                realized.Add("a road class the Develop roads order builds");
                foreach (RoadMaterialConfig m in rc.MaterialsPerKm) reqs.Add(new InfoLink(Num(m.Qty) + " " + m.Good + " a km", InfoSubject.OfGood(c.Cfg.Goods?.IdOf(m.Good) ?? -1), null, "road material"));
            }
        if (string.Equals(c.Cfg.Trade.Entity, ent.Id, StringComparison.Ordinal))
            realized.Add("trade between your settlements and partners that both know it (TradeArbitrageSystem)");
        if (c.Cfg.UnitFamilies is { } fam)
            foreach (UnitFamily f in fam.Families)
                foreach (UnitIdentity i in f.Line)
                    if (string.Equals(i.RealizedBy, ent.Id, StringComparison.Ordinal))
                    {
                        unit = true;
                        enables.Add(new InfoLink(i.Name, null, null, f.Name + " - recruitment arrives with the Battle Layer (M7)"));
                    }
        for (int o = 0; o < r.Entities.Count; o++)
            if (o != e && Contains(r.Entities[o].InstitutionAtoms, e))
                enables.Add(new InfoLink(EntityName(r.Entities[o]), InfoSubject.OfEntity(r.Entities[o].Id), c.Eligible(o), KindName(r.Entities[o].Kind) + " - needs it"));
    }

    /// <summary>The short realization note an "opens the way to" line carries, and whether a system realizes it.</summary>
    private static (string Note, bool Realized) Realization(Ctx c, int e)
    {
        var enables = new List<InfoLink>();
        var realized = new List<string>();
        var reqs = new List<InfoLink>();
        bool unit = false;
        Consumers(c, c.R!.Entities[e], e, enables, realized, reqs, ref unit);
        if (realized.Count > 0) return (realized[0], true);
        return unit ? ("recruitment arrives with the Battle Layer (M7)", false) : ("not built in this build", false);
    }

    private static IEnumerable<(int Age, AgeMilestone M)> MilestonesNaming(Ctx c, int key)
    {
        if (c.Cfg.Ages is not { } ages) yield break;
        for (int age = 1; age <= AgeContent.AgeCount; age++)
        {
            if (ages.Age(age).Entry is not { } entry) continue;
            foreach (AgeMilestone m in All(entry))
                if (m.Fact.Kind == MilestoneFactKind.Research && Contains(m.Fact.NodeKeys, key)) yield return (age, m);
        }
    }

    private static IEnumerable<AgeMilestone> All(AgeEntryRequirements entry)
    {
        foreach (AgeMilestone m in entry.Core) yield return m;
        foreach (AgeMilestone m in entry.Supporting) yield return m;
    }

    private static Predicate? TaxAtoms(Ctx c) =>
        c.Cfg.Governance is { } gov && c.R is { } r
            ? ResearchContentLoader.ParseRequirement(r, gov.TaxationRequires, "sim.json governance.taxationRequires") : null;

    // ================================================================== shared: where goods come from

    /// <summary>Whether the issuer can obtain <paramref name="good"/> now: a stock or a deposit in one of its settlements,
    /// or a KNOWN recipe that makes it from inputs it can obtain (bounded depth).</summary>
    private static bool Obtainable(Ctx c, string good, int depth = 0)
    {
        if (c.Cfg.Goods is not { } goods) return false;
        int id = goods.IdOf(good);
        if (id < 0) return false;
        foreach (SettlementId s in c.Controlled) if (Held(c.W, s, id) > 0 || Deposit(c.W, s, id) > 0.0) return true;
        if (id == goods.GrainId) return true;
        return depth < 4 && ObtainableByKnownCraft(c, good, depth);
    }

    private static bool ObtainableByKnownCraft(Ctx c, string good, int depth = 0)
    {
        if (c.Cfg.Goods is not { } goods) return false;
        foreach (RecipeEntry rec in goods.Recipes)
        {
            if (!string.Equals(rec.Output.Good, good, StringComparison.Ordinal) || !CraftingQuery.IsKnownBy(c.W, c.R, c.P, rec)) continue;
            bool all = true;
            foreach (RecipeInput i in rec.Inputs) if (!Obtainable(c, i.Good, depth + 1)) { all = false; break; }
            if (all) return true;
        }
        return false;
    }

    /// <summary>The source chain of a good the issuer cannot obtain now (see <see cref="SourceChain"/>); null when it can.
    /// <paramref name="target"/> receives the research node the chain ends at (unchanged when it ends elsewhere).</summary>
    private static string? Chain(Ctx c, string good, int depth, ref int target)
    {
        if (c.Cfg.Goods is not { } goods || Obtainable(c, good, 4)) return null;
        RecipeEntry? pick = null;
        foreach (RecipeEntry rec in goods.Recipes)
            if (string.Equals(rec.Output.Good, good, StringComparison.Ordinal) && CraftingQuery.IsKnownBy(c.W, c.R, c.P, rec)) { pick = rec; break; }
        if (pick is null)
            foreach (RecipeEntry rec in goods.Recipes)
                if (string.Equals(rec.Output.Good, good, StringComparison.Ordinal)) { pick = rec; break; }
        if (pick is null) return good + " (nothing makes it in this build)";
        string line = good + " <- " + RecipeName(c, pick);
        if (!CraftingQuery.IsKnownBy(c.W, c.R, c.P, pick))
        {
            if (c.R is { } r && pick.Entity is { } eid && r.EntityIndexOf(eid) is int e and >= 0 && EntityTreeTarget(c, e) is int n and >= 0)
            {
                target = n;
                return line + " <- " + r.Nodes[n].Name + " (research, " + AgeText(c, r.Nodes[n].Age) + ")";
            }
            return line + " (not known)";
        }
        if (depth >= 4) return line;
        foreach (RecipeInput i in pick.Inputs)
            if (!Obtainable(c, i.Good) && Chain(c, i.Good, depth + 1, ref target) is { } inner) return line + " <- " + inner;
        return line;
    }

    private static string RecipeNeeds(Ctx c, RecipeEntry rec)
    {
        if (c.R is not { } r || rec.Entity is not { } eid || r.EntityIndexOf(eid) is not (int e and >= 0) || EntityTreeTarget(c, e) is not (int n and >= 0)) return "";
        return ": needs " + r.Nodes[n].Name + " (research, " + AgeText(c, r.Nodes[n].Age) + ")";
    }

    // ================================================================== shared: names and words

    private static string Where(ResearchContent r, ResearchNode n) =>
        n.Tree == ResearchTree.Civics ? "Civics" : n.Branch < 0 ? "Technology (main trunk)" : "Technology (" + r.Branches[n.Branch].Name + ")";

    private static string TreeWord(ResearchNode n) => n.Tree == ResearchTree.Civics ? "Civics" : "Technology";

    /// <summary>"A3" → "Age III" (the node's Age relevance — metadata only; nothing gates on it).</summary>
    private static string AgeText(Ctx c, string age)
    {
        int k = age.Length > 1 && (age[0] == 'A' || age[0] == 'a') && int.TryParse(age.AsSpan(1), NumberStyles.Integer, CultureInfo.InvariantCulture, out int v) ? v : 0;
        return k >= 1 && k <= 9 ? "Age " + Numeral(k) : age;
    }

    private static string AgeName(Ctx c, int age) =>
        c.Cfg.Ages is { } ages && age >= 1 && age <= AgeContent.AgeCount
            ? (ages.Age(age).Name.StartsWith("the ", StringComparison.OrdinalIgnoreCase) ? "" : "the ") + ages.Age(age).Name + " (Age " + Numeral(age) + ")"
            : "Age " + Numeral(age);

    private static string Numeral(int age) => age switch
    {
        1 => "I", 2 => "II", 3 => "III", 4 => "IV", 5 => "V", 6 => "VI", 7 => "VII", 8 => "VIII", 9 => "IX", _ => Inv(age),
    };

    private static string KindName(ResearchEntityKind k) => k switch
    {
        ResearchEntityKind.Building => "Building",
        ResearchEntityKind.Infrastructure => "Infrastructure",
        ResearchEntityKind.Institution => "Institution",
        ResearchEntityKind.Unit => "Unit",
        ResearchEntityKind.Activity => "Activity",
        ResearchEntityKind.Project => "Project",
        ResearchEntityKind.Recipe => "Craft",
        _ => "Entity",
    };

    private static string EntityName(ResearchEntity e) => e.Name is { Length: > 0 } n ? n : e.Id;

    private static string SectorWord(int s) => s switch
    {
        Sectors.Farming => "farming",
        Sectors.Herding => "herding and fishing",
        Sectors.Extraction => "extraction",
        Sectors.Crafting => "crafting",
        _ => "construction",
    };

    private static string SectorLabel(Ctx c, int sector)
    {
        int where = DefaultSettlement(c, -1);
        if (where >= 0 && LabourActivities.Of(c.W, c.Cfg, c.P, new SettlementId(where), sector) is { } a) return a.Label;
        return c.R is { } r ? r.SectorActivities[sector].Baseline.Name : ResearchContentLoader.SectorIds[sector];
    }

    private static string ProjectName(Ctx c, ConstructionProjectEntry p)
    {
        if (p.Founds is { } f && c.R is { } r0 && InstitutionContent.TypeOf(r0, InstitutionContent.TypeKeyOf(r0, p)) is { } ut) return ut.Name;
        if (p.Entity is { } id && c.R is { } r && r.EntityIndexOf(id) is int e and >= 0 && r.Entities[e].Name is { Length: > 0 } name) return name;
        return Cap(p.Name);
    }

    private static string RecipeName(Ctx c, RecipeEntry rec)
    {
        if (rec.Entity is { } id && c.R is { } r && r.EntityIndexOf(id) is int e and >= 0 && r.Entities[e].Name is { Length: > 0 } name) return name;
        return Cap(rec.Name);
    }

    private static string RoadName(Ctx c, RoadClassConfig rc)
    {
        if (rc.Entity is { } id && c.R is { } r && r.EntityIndexOf(id) is int e and >= 0 && r.Entities[e].Name is { Length: > 0 } name) return name;
        return rc.EdgeType == EdgeTypes.DirtPath ? "Dirt paths" : "Road class " + Inv(rc.EdgeType);
    }

    private static string RecipeText(RecipeEntry rec)
    {
        var parts = new List<string>();
        foreach (RecipeInput i in rec.Inputs) parts.Add(Num(i.PerOutput) + " " + i.Good);
        return string.Join(" + ", parts) + " -> " + Inv(rec.Output.Qty) + " " + rec.Output.Good;
    }

    private static string Materials(ConstructionProjectEntry p)
    {
        var parts = new List<string>();
        foreach (ProjectInput i in p.Inputs) parts.Add(Inv(i.Qty) + " " + i.Good);
        return parts.Count == 0 ? "no materials" : string.Join(", ", parts);
    }

    private static string GoodName(GoodsConfig? goods, int id)
    {
        if (goods is not null) foreach (GoodEntry g in goods.Goods) if (g.Id == id) return g.Name;
        return "good " + Inv(id);
    }

    private static int DefaultSettlement(Ctx c, int settlement)
    {
        if (settlement >= 0) return settlement;
        if (EmpireQuery.TryGetCapital(c.W, c.P, out SettlementId cap)) return cap.Value;
        return c.Controlled.Length > 0 ? c.Controlled[0].Value : -1;
    }

    private static long Held(IReadOnlyWorldState w, SettlementId s, int good)
    {
        for (int i = 0; i < w.GoodStocks.Count; i++)
            if (w.GoodStocks[i].Settlement.Value == s.Value && w.GoodStocks[i].Good.Value == good) return w.GoodStocks[i].Amount.Value;
        return 0;
    }

    private static double Deposit(IReadOnlyWorldState w, SettlementId s, int good)
    {
        double sum = 0.0;
        for (int i = 0; i < w.Deposits.Count; i++)
            if (w.Deposits[i].Settlement.Value == s.Value && w.Deposits[i].Good.Value == good) sum += w.Deposits[i].Abundance;
        return sum;
    }

    /// <summary>The sector that works a deposit good: production's own classification (LabourActivities.GoodsOf), asked
    /// of the first settlement holding the deposit — Herding/fishing when it lists the good, else Extraction.</summary>
    private static int DepositSector(Ctx c, int good)
    {
        for (int s = 0; s < c.W.Settlements.Count; s++)
        {
            SettlementId id = c.W.Settlements[s].Id;
            if (Deposit(c.W, id, good) <= 0.0) continue;
            (ImmutableArray<GoodId> herded, _) = LabourActivities.GoodsOf(c.W, c.Cfg.Goods, id, Sectors.Herding, c.R);
            foreach (GoodId g in herded) if (g.Value == good) return Sectors.Herding;
            return Sectors.Extraction;
        }
        return Sectors.Extraction;
    }

    private static ActionDescriptor? Find(Ctx c, ActionDomain domain, long id)
    {
        foreach (ActionDescriptor a in c.Actions) if (a.Domain == domain && a.Id == id) return a;
        return null;
    }

    /// <summary>Whether <paramref name="token"/> occurs in <paramref name="source"/> as a whole identifier.</summary>
    private static bool HasToken(string source, string token)
    {
        int at = 0;
        while ((at = source.IndexOf(token, at, StringComparison.Ordinal)) >= 0)
        {
            bool before = at == 0 || !IsIdent(source[at - 1]);
            bool after = at + token.Length >= source.Length || !IsIdent(source[at + token.Length]);
            if (before && after) return true;
            at += token.Length;
        }
        return false;
    }

    private static bool IsIdent(char ch) => char.IsLetterOrDigit(ch) || ch == '_' || ch == '-';

    private static bool Contains(IReadOnlyList<int> list, int value)
    {
        for (int i = 0; i < list.Count; i++) if (list[i] == value) return true;
        return false;
    }

    private static string Cap(string s) => s.Length == 0 ? s : char.ToUpperInvariant(s[0]) + s[1..];
    private static string Inv(long v) => v.ToString(CultureInfo.InvariantCulture);
    private static string Num(double v) => v.ToString("0.##", CultureInfo.InvariantCulture);
    private static string Num3(double v) => v.ToString("0.###", CultureInfo.InvariantCulture);
    private static string N0(double v) => v.ToString("#,0", CultureInfo.InvariantCulture);
    private static string N1(double v) => v.ToString("#,0.#", CultureInfo.InvariantCulture);
    private static string Pct(double f) => (f * 100.0).ToString("0.#", CultureInfo.InvariantCulture) + "%";

    // ================================================================== plumbing

    /// <summary>One card's inputs, computed once per call: the completed mask, the stage, the action list (lazily).</summary>
    private sealed class Ctx
    {
        public readonly IReadOnlyWorldState W;
        public readonly SimConfig Cfg;
        public readonly PolityId P;
        public readonly ResearchContent? R;
        public readonly bool[] Done;
        public readonly bool Stage;
        public readonly ActionQueryContext Q;
        public readonly Func<int, string>? NameFn;
        private ImmutableArray<ActionDescriptor>? _actions;
        private SettlementId[]? _controlled;

        public Ctx(IReadOnlyWorldState w, SimConfig cfg, PolityId p, ActionQueryContext q, Func<int, string>? name, ImmutableArray<ActionDescriptor>? actions)
        {
            W = w; Cfg = cfg; P = p; Q = q; NameFn = name; _actions = actions;
            R = cfg.Research;
            Done = R is null ? [] : ResearchQuery.CompletedMask(w, R, p);
            Stage = R is not null && ResearchQuery.StageReached(R, Done);
        }

        public ImmutableArray<ActionDescriptor> Actions => _actions ??= AvailableActionsQuery.For(W, Cfg, P, Q);
        public SettlementId[] Controlled => _controlled ??= LabourActivities.ControlledSettlements(W, P);
        public string Name(int s) => NameFn is null ? "settlement " + s.ToString(CultureInfo.InvariantCulture) : NameFn(s);
        public bool Eligible(int e) => ResearchQuery.IsKnowledgeEligible(R!, e, Done);
        public bool Available(int n) => ResearchQuery.IsAvailable(R!, n, Done, Stage);
    }

    /// <summary>A card under construction.</summary>
    private sealed class B(InfoSubject subject)
    {
        public readonly InfoSubject Subject = subject;
        public string Title = "", Kind = "", What = "", StatusLine = "", RealizedBy = "";
        public string? Summary, RequirementSource;
        public InfoStatus Status = InfoStatus.Unknown;
        public InfoEffect Effect = InfoEffect.Simulated;
        public int TreeNode = -1;
        public ActionDescriptor? Action;
        public readonly List<InfoLink> EnabledBy = [], Requirements = [], WhyLocked = [], Enables = [];
        public readonly List<string> KnowledgeOnly = [];
        public readonly List<InfoSection> More = [];

        public void Unknown(string what)
        {
            Title = "Unknown";
            Kind = "Not in this build";
            What = "This content defines no " + what + ".";
            Status = InfoStatus.Unknown;
            StatusLine = "Unknown.";
        }

        public void Inert(string what)
        {
            if (Title.Length == 0) Title = "Not loaded";
            What = What.Length == 0 ? "This session has no " + what + " loaded." : What;
            Status = InfoStatus.Inert;
            StatusLine = "Inert: no " + what + " is loaded.";
        }

        public InfoCard Build(Ctx c)
        {
            ResearchNodeId? node = null;
            string? nodeName = null;
            var prereqs = new List<InfoLink>();
            if (TreeNode >= 0 && c.R is { } r && TreeNode < r.Nodes.Count)
            {
                ResearchNode n = r.Nodes[TreeNode];
                node = n.Key;
                nodeName = n.Name;
                if (Subject.Kind != InfoKind.ResearchNode)
                {
                    IReadOnlyList<int> must = n.Prerequisite?.MustHoldAtoms() ?? [];
                    foreach (int p in n.PrerequisiteNodes) prereqs.Add(NodeLink(c, p, "a prerequisite of " + n.Name + " - " + (Contains(must, p) ? "required" : "one of")));
                }
            }
            return new InfoCard(Subject, Title, Kind, What, Status, StatusLine, Summary,
                [.. EnabledBy], RequirementSource, node, nodeName, [.. prereqs], [.. Requirements], [.. WhyLocked],
                [.. Enables], [.. KnowledgeOnly], RealizedBy, Effect, [.. More], Action);
        }
    }
}
