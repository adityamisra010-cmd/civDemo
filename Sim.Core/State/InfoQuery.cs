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
    /// <summary>M5 P8: a terrain cell of the map: Id = its cell index (y × size + x), Settlement = the source settlement the
    /// founding arms and the route are judged from (-1 = the capital).</summary>
    Cell = 15,
    /// <summary>M5 S1d: the Found Settlement order FROM a settlement (D-051 §5): Id = the source settlement's id.</summary>
    Founding = 16,
    /// <summary>WS2 (D-054): the Empire's building/roads material split — the order that sets it (kind 12) and the rule it
    /// sets (singleton).</summary>
    MaterialsSplit = 17,
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
    public static InfoSubject OfCell(int cell, int source = -1) => new(InfoKind.Cell, cell, null, source);
    public static InfoSubject OfFounding(int source) => new(InfoKind.Founding, source);
    /// <summary>WS2 (D-054): the building/roads material split.</summary>
    public static InfoSubject MaterialsSplit => new(InfoKind.MaterialsSplit);
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
    /// this world, player and context). <paramref name="knowledge"/> is what the player knows (P7, the one knowledge seam;
    /// null = <see cref="PolityKnowledge.Of"/>, all-known until S2a): a formation the player does not know gets the card of
    /// an id that names nothing, and a map cell the player does not know is refused. <paramref name="site"/> is the
    /// terrain half of a site assessment (P8; <see cref="SiteAssessment.Prepare"/> with the founding path's worldgen
    /// config) — a map cell's card without it is titled and refused as usual and otherwise reads "not loaded".
    /// M5 S1d review: <paramref name="batch"/> is the founding batch a map cell's card is judged against (the placement
    /// mode's — this turn's queued founding orders; <see cref="SiteAssessment.Assess"/>), or null for the standing
    /// settlements alone. Only a map cell's card reads it.</summary>
    public static InfoCard Card(
        IReadOnlyWorldState world, SimConfig cfg, PolityId player, InfoSubject subject,
        ActionQueryContext? context = null, Func<int, string>? settlementName = null, ImmutableArray<ActionDescriptor>? actions = null,
        PolityKnowledge? knowledge = null, SiteAssessment.Prepared? site = null, SiteAssessment.Batch? batch = null)
    {
        ArgumentNullException.ThrowIfNull(world);
        ArgumentNullException.ThrowIfNull(cfg);
        if (knowledge is not null && knowledge.Polity.Value != player.Value)
            throw new ArgumentException("the knowledge must be the player's", nameof(knowledge));
        var c = new Ctx(world, cfg, player, context ?? ActionQueryContext.None, settlementName, actions,
            knowledge ?? PolityKnowledge.Of(world, player, cfg), site, batch);
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
            case InfoKind.Cell: CellCard(c, b); break;
            case InfoKind.Founding: FoundingCard(c, b); break;
            case InfoKind.MaterialsSplit: MaterialsSplitCard(c, b); break;
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
        var c = new Ctx(world, cfg, polity, ActionQueryContext.None, null, null, PolityKnowledge.Of(world, polity, cfg));
        int target = -1;
        return Chain(c, good, 0, ref target);
    }

    /// <summary>The research node the <see cref="SourceChain"/> of <paramref name="good"/> ends at (content index), or -1.</summary>
    public static int SourceChainNode(IReadOnlyWorldState world, SimConfig cfg, PolityId polity, string good)
    {
        var c = new Ctx(world, cfg, polity, ActionQueryContext.None, null, null, PolityKnowledge.Of(world, polity, cfg));
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

        // ADR-049: a levelled repeatable is never "known" — it stays open for its next level.
        bool levelled = ResearchQuery.IsLevelled(r, i);
        bool done = c.Done[i] && !levelled, available = c.Available(i);
        bool target = IsTarget(c, n);
        double cost = ResearchQuery.EffectiveCost(c.W, r, c.P, i);
        // ADR-043: a civic is paid in Civics Points in the two-lane model (CP a turn = the accrual of a step of this length).
        bool cp = r.Tuning.TwoLanes && n.Tree == ResearchTree.Civics;
        // rp-cp-lifecycle k5: the CP the NEXT step accrues (the context's dt), not the last step's.
        double rp = cp ? ResearchQuery.CivicsPointsMilli(c.W, r, c.P, c.NextDtYears) / 1000.0 : ResearchQuery.ResearchPointPool(c.W, r, c.P);
        string points = cp ? " civics points" : " research points";
        // rp-cp-lifecycle k2/k3: with destination routing the node a lane FUNDS is a route node (or an assist), not only
        // the destination row — ResearchQuery.Route's Current, the planner's own answer.
        ResearchRoute? funding = r.Tuning.DestinationRouting && !done && available ? FundingRoute(c, i) : null;
        if (done) { b.Status = InfoStatus.Known; b.StatusLine = "Known: your scholars have completed it."; }
        else if (funding is { } fr && (fr.Assisting || fr.Destination != n.Key))
        {
            b.Status = InfoStatus.Researching;
            b.StatusLine = "Being researched" + Toward(c, fr) + ": " + N0(ResearchQuery.Progress(c.W, c.P, n.Key)) + " of " + N0(cost) + points + ", +" + N1(rp) + " a turn.";
        }
        else if (target && available)
        {
            b.Status = InfoStatus.Researching;
            b.StatusLine = "Being researched: " + N0(ResearchQuery.Progress(c.W, c.P, n.Key)) + " of " + N0(cost) + points + ", +" + N1(rp) + " a turn.";
        }
        else if (available)
        {
            b.Status = InfoStatus.Researchable;
            b.StatusLine = "Open to research now: " + N0(cost) + points + (rp > 0 ? " (about " + Inv((long)Math.Ceiling(Math.Max(0.0, cost - ResearchQuery.Progress(c.W, c.P, n.Key)) / rp)) + " turns at the current rate)." : ".");
        }
        else { b.Status = InfoStatus.Locked; b.StatusLine = "Locked: " + N0(cost) + points + " once it opens."; }
        if (levelled && c.Done[i])
            b.StatusLine = "Researched " + Inv(ResearchQuery.Level(c.W, c.P, n.Key)) + "x; next level: " + b.StatusLine;

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
            // ADR-048: the Age sweep, reported apart from the prerequisites (never as one).
            if (ResearchQuery.SweepBlock(r, c.Done, i) is { Blocked: true } sweep)
                b.WhyLocked.Add(new InfoLink("Age " + Inv(sweep.CohortAge) + " sweep: " + Inv(sweep.Outstanding.Length) +
                    " node" + (sweep.Outstanding.Length == 1 ? "" : "s") + " of Age " + Inv(sweep.CohortAge) + " still outstanding", null, false));
            if (n.IsRecursive && n.Tree == ResearchTree.Civics && !ResearchQuery.FinalAgeCivicsExhausted(r, c.Done))
                b.WhyLocked.Add(new InfoLink("Recursive: it waits for every finite civic of the final Age", null, false));   // ADR-049
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
        // ADR-047 / ADR-049: its realized effects and level effects — each read by the operand's owning system. CR-025: an
        // operand whose computed value no simulation system consumes is listed as not simulated, and does not make the node
        // simulated (since the ruling the loader refuses such a realized effect; the branch stays as the display's own guard).
        if (KnowledgeEffects.IsOn(r))
            foreach (RealizedEffect e in n.RealizedEffects)
            {
                KnowledgeOperandInfo op = KnowledgeOperands.All[(int)e.Operand];
                string label = (op.Direction == OperandDirection.Higher ? "+" : "-") + N1(e.Delta * 100.0) + "%: " + op.Meaning;
                if (!op.ConsumedInPlay)
                {
                    b.Enables.Add(new InfoLink(label + " (not simulated yet)", null, false,
                        "not simulated in this build: " + op.NotConsumed + " (" + op.Id + ")"));
                    continue;
                }
                simulated = true;
                b.Enables.Add(new InfoLink(label, null, done, "read by " + op.ReadBy + " (" + op.Id + ")"));
            }
        if (levelled && n.Levels is { } rule)
            foreach (RepeatableLevelEffect e in rule.Effects)
            {
                simulated = true;
                b.Enables.Add(new InfoLink("Each level closes " + N1(e.GapFraction * 100.0) + "% of the remaining gap: " +
                    RepeatableOperands.Get(e.Operand).Meaning, null, c.Done[i], e.Operand));
            }
        // CR-025 ruling (D-063 R1 amendment): its declared FUTURE effects — named, never counted as simulated.
        foreach (FutureEffect e in n.FutureEffects)
            b.Enables.Add(new InfoLink(FutureLabel(e), null, false, FutureNote(e)));
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

        // Its action: set it as the target (Id 1, available now) or — ADR-048, routing on — as a destination (Id 4).
        foreach (int id in new[] { 1, 4 })
            if (b.Action is null && Find(c, ActionDomain.Research, id) is { } set)
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
            if (Deposit(c, s, g.Id) > 0.0) deposits++;
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

    /// <summary>The construction sector's knowledge-derived label for the viewer — what the Labour block and the queue's
    /// status line call it (LabourActivities.CapabilityLabel; "Building" when nothing names it).</summary>
    private static string BuildingLabel(Ctx c)
    {
        string label = c.R is null ? "" : LabourActivities.CapabilityLabel(c.R, c.Done, Sectors.Construction);
        return label.Length == 0 ? "Building" : label;
    }

    private static void ProjectCard(Ctx c, B b)
    {
        if (c.Cfg.Goods?.ProjectById((int)b.Subject.Id) is not { } p) { b.Unknown("a construction project with this id"); return; }
        int where = DefaultSettlement(c, b.Subject.Settlement);
        bool founds = InstitutionContent.FoundsInstitution(p);
        b.Title = ProjectName(c, p);
        b.Kind = "Construction project" + (founds ? " - founds an institution" : "") + (where >= 0 ? " at " + c.Name(where) : "");
        bool incremental = ConstructionQuery.Incremental(c.Cfg);
        b.What = (incremental ? "Built through the construction queue, a little each turn, from " : "Built through the construction queue from ")
            + Materials(p) + " and " + Num(p.LaborRequired) + " adult-years of construction labour.";
        // D-053 §1: the purse's claims are the world's queue plus the turn's pending orders the intake would accept — the
        // queue the listing reads (ConstructionQuery.QueueWithPending), so the card and the listing never disagree. WS1: with
        // the pending cancels applied first and each entry's REMAINING needs (its progress in the same pending book).
        IReadOnlyTable<ConstructionQueueRow> claims = ConstructionQuery.QueueWithPending(c.W, c.Cfg, c.Q.Queued, out IReadOnlyTable<ConstructionProgressRow> claimsProgress);
        ProjectAvailability av = where >= 0 ? ConstructionQuery.Availability(c.W, c.Cfg, c.P, new SettlementId(where), p.Id, claims, claimsProgress) : ProjectAvailability.UnknownSettlement;
        // WS1: the entry of this project already queued here (its lowest slot), whose progress the card reports.
        long progress = 0;
        ConstructionQueueRow? queued = null;
        if (where >= 0)
            foreach (ConstructionQueueRow row in ConstructionQuery.Queue(c.W, new SettlementId(where)))
                if (row.ProjectId == p.Id) { progress = ConstructionQuery.ProgressOf(c.W, row); queued = row; break; }
        // WS1 (C1 review F6): that entry is itself one of the claims in the pending book (unless a pending cancel removes it);
        // its Met asks whether the purse covers what IT still needs after every OTHER claim, so its own claim is set aside.
        ConstructionQueueRow? ownClaim = incremental && queued is { } qe
            && ConstructionQuery.IndexOfSlot(claims, qe.Settlement, qe.Slot) is var at && at >= 0 && claims[at].ProjectId == qe.ProjectId
            ? qe : null;
        // D-055 (batch-4 design §1.6, T1b): what the hold rule SETS ASIDE ahead of this entry — THE walk on the world and the
        // pending orders (SetAsideQuery.Read, HeadStatus's own setup), on the viewer's OWN books only (P7: a foreign purse's
        // set-asides are never read). For the entry when it is its settlement's head in the walk: the earlier heads of its
        // purse — their draws and set-asides for Met and for the blocker, named apart (T1b fix round, finding F4: the blocker
        // read the set-asides alone, so a head Met could not cover had no wait line); for a would-be new order (or a queued
        // entry behind its settlement's head): every set-aside of the purse. Only where the settlement's purse is the viewer's
        // OWN (SetAsideQuery.ViewersOwnPurse — fix round, finding F8: in a world with two control rows for one place, a higher
        // controller never reads the lowest controller's purse). Null with the rule off: batch 3's card exactly.
        SetAsideReading? holds = null;
        long[] aheadMet = [], aheadHeld = [], aheadDrawn = [];
        int holdBefore = -1, holdPurse = 0;
        if (incremental && where >= 0 && Reservations.Enabled(c.Cfg) && SetAsideQuery.ViewersOwnPurse(c.W, c.Cfg, c.P, new SettlementId(where)))
        {
            holds = SetAsideQuery.Read(c.W, c.Cfg, c.Q.Queued, c.Q.NextDtYears ?? c.W.Clock.DtYears);
            holdPurse = ConstructionQuery.PurseKey(c.W, c.Cfg, new SettlementId(where));
            // D-058: the queued entry's own place in the walk — its settlement's head, or an entry promoted within the step.
            int h = queued is { } qe0 ? SetAsideQuery.EntryIndexOf(holds.Walk, qe0) : -1;
            if (h >= 0)
            {
                holdBefore = h;
                aheadMet = SetAsideQuery.Ahead(holds.Walk, h, withDraws: true);
                aheadHeld = SetAsideQuery.Ahead(holds.Walk, h, withDraws: false);
                aheadDrawn = SetAsideQuery.DrawnAhead(holds.Walk, h);
            }
            else aheadMet = aheadHeld = SetAsideQuery.HeldIn(holds.Walk, holdPurse);
        }
        string? blocker = av == ProjectAvailability.Available
            ? holds is null
                ? ConstructionQuery.BlockerSeenBy(c.W, c.Cfg, c.P, new SettlementId(where), p, c.Q.NextDtYears, progress)
                : ConstructionQuery.BlockerSeenBy(c.W, c.Cfg, c.P, new SettlementId(where), p, c.Q.NextDtYears, progress, aheadHeld, aheadDrawn)
            : null;
        // The guard refused because the empire HOLDS the crafted input but its queued projects already claim it: say so.
        string? claimedReason = av == ProjectAvailability.InputUnobtainable
            ? ConstructionQuery.ClaimedReason(c.W, c.Cfg, new SettlementId(where), p, claims, claimsProgress, c.Name) : null;
        b.Status = av switch
        {
            ProjectAvailability.Available => blocker is null ? InfoStatus.Available : InfoStatus.Blocked,
            ProjectAvailability.NotKnowledgeEligible => InfoStatus.Locked,
            ProjectAvailability.InputUnobtainable => InfoStatus.Locked,
            _ => InfoStatus.Known,
        };
        b.StatusLine = av switch
        {
            ProjectAvailability.Available when incremental => "Can be ordered now. Work goes on each turn as far as materials and builders allow; "
                + "what has gone in is never lost while it waits, only if the project is cancelled."
                + (blocker is null ? "" : " It will wait for " + WithoutNeeds(blocker) + "."),
            ProjectAvailability.Available => blocker is null ? "Can be built now." : "You may order it; it waits until it " + blocker + ".",
            ProjectAvailability.NotKnowledgeEligible => "Not yet known.",
            ProjectAvailability.InputUnobtainable => claimedReason is { } held
                ? "Cannot be ordered yet: it " + held + "."
                : "Cannot be ordered yet: it needs "
                    + (ConstructionQuery.UnobtainableInput(c.W, c.Cfg, new SettlementId(where), p, claims)?.Name ?? "an input")
                    + ", which " + c.Name(where) + " cannot make yet.",
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

        // REQUIREMENTS: materials (held in the settlement's PURSE — D-051 §6, D-053 §1: construction checks and draws the
        // Empire's settlements together where the switch is on and an Empire rules it, its own stock otherwise; read as the
        // viewer may, ConstructionQuery.HoldersSeenBy, so a foreign settlement shows its own stock only — P7), labour, viability.
        bool empire = where >= 0 && ConstructionQuery.DrawsOnEmpireSeenBy(c.W, c.Cfg, c.P, new SettlementId(where));
        // A CRAFTED input (tools) is read as the obtainability guard reads it (D-053 §1): Met against the UNCLAIMED holding —
        // the purse less what the projects queued in it already claim — with a note naming the claims; a raw input keeps the
        // pooled holding (the guard's raw reading). Only on the viewer's own books (the pooled purse is shown only there).
        foreach (ProjectInput i in p.Inputs)
        {
            int good = c.Cfg.Goods!.IdOf(i.Good);
            long held = PurseHeld(c, where, good);
            string has = (empire ? "empire has " : "has ") + Inv(held);
            long need = incremental ? ConstructionQuery.Remaining(i.Qty, progress) : i.Qty;
            long claimed = empire && ConstructionQuery.IsCraftedGood(c.Cfg.Goods!, good)
                ? ConstructionQuery.Claimed(claims, claimsProgress, c.Cfg.Goods!, ConstructionQuery.Holders(c.W, c.Cfg, new SettlementId(where)), new GoodId(good)) : 0;
            long own = claimed > 0 && ownClaim is { } oc
                ? ConstructionQuery.Remaining(ConstructionQuery.InputOf(c.Cfg.Goods!, p, new GoodId(good)), ConstructionQuery.ProgressOf(claimsProgress, oc)) : 0;
            // D-055: what the hold rule takes ahead of this entry (0 with the rule off, or on a foreign settlement); the
            // claims (counts, read by the guard) and the set-asides overlap — a head's claim IS its set-aside — so Met reads the
            // larger of the two deductions, never their sum.
            long aside = SetAsideQuery.At(aheadMet, good);
            long usable = held - Math.Max(claimed - own, aside);
            // WS1: claims are counts, not set-asides — the card says so in those words (never "reserved"). D-055: with the hold
            // rule on, a head's claim IS set aside, so the count is stated bare and the set-asides are named after it — but only
            // for the part of the claim the purse sets aside (T1b fix round, finding F9): a claim nothing sets aside (a non-head
            // entry's) keeps batch 3's words, a partly covered one says how much is not set aside. On a purse that is not the
            // viewer's own (holds null) nothing is said of set-asides.
            long purseAside = holds is null ? -1 : SetAsideQuery.At(SetAsideQuery.HeldIn(holds.Walk, holdPurse), good);
            const string canUse = ": earlier projects in the building order, and road works under the split, can use it first)";
            string notSetAside = !Reservations.Enabled(c.Cfg) || purseAside == 0 ? " (this does not set it aside" + canUse
                : purseAside > 0 && purseAside < claimed ? " (" + Inv(claimed - purseAside) + " of it is not set aside" + canUse
                : "";
            string note = has + (!empire && where >= 0 ? " at " + c.Name(where) : "")
                + (claimed > 0
                    ? incremental
                        ? "; " + Inv(claimed) + " counted toward projects already queued at " + ClaimedPlaces(c, claims, claimsProgress, where, good)
                            + notSetAside
                        : "; " + Inv(claimed) + " claimed by projects queued at " + ClaimedPlaces(c, claims, claimsProgress, where, good)
                            + ", " + Inv(Math.Max(0, usable)) + " unclaimed"
                    : "")
                + (holds is not null ? SetAsideNote(c, holds.Walk, holdPurse, good, holdBefore) : "");
            string label = incremental && progress > 0
                ? Inv(i.Qty) + " " + i.Good + " - " + Inv(ConstructionQuery.Delivered(i.Qty, progress)) + " in, " + Inv(need) + " to go"
                : Inv(i.Qty) + " " + i.Good;
            // Met as the obtainability guard reads it: a crafted input against its UNCLAIMED holding, a raw one against the
            // purse (claimed is 0 for a raw good) — WS1: against the REMAINING need, and for an entry already queued here
            // against every claim but its own.
            b.Requirements.Add(new InfoLink(label, InfoSubject.OfGood(good), usable >= need, note));
            if (held < need && Chain(c, i.Good, 0, ref b.TreeNode) is { } chain)
                b.WhyLocked.Add(new InfoLink(Inv(need) + " " + i.Good + " (" + has + "): " + chain, InfoSubject.OfGood(good), false));
        }
        double? cap = where >= 0 && c.Q.NextDtYears is { } dt && dt > 0.0 ? ConstructionQuery.CapacityAdultYears(c.W, c.Cfg, new SettlementId(where), dt) : null;
        if (incremental)
        {
            // WS1: labour is a PACE, not a gate — an estimate of how much a turn and how many turns, never a refusal.
            double left = p.LaborRequired * (double)(ConstructionQuery.ProgressUnits - progress) / ConstructionQuery.ProgressUnits;
            string pace = cap is { } cp && cp > 0.0
                ? "about " + Num(cp) + " adult-years a turn here; about " + Inv((long)Math.Ceiling(left / cp)) + " more turns of building work (an estimate)"
                : cap is not null ? "no builders here this turn - give " + BuildingLabel(c) + " a share in Labour" : "the construction share of labour";
            b.Requirements.Add(new InfoLink(Num(p.LaborRequired) + " adult-years of construction labour", InfoSubject.Sector(Sectors.Construction, where),
                cap is { } cm ? cm > 0.0 : null, pace));
        }
        else
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
        {
            // M5 item 5 (D-053 §3): the granary's two real effects, stated from the very functions the simulation
            // multiplies by (State.GranaryEffects) — LOCAL farm output and EMPIRE-WIDE famine intensity, each with its
            // next step, its strict-diminishing horizon and its ceiling. Neither is storage, and the card never says it is.
            if (p.Id == GranaryEffects.ProjectId(c.Cfg) && GranaryEffects.Active(c.Cfg) is { } granaries)
            {
                // The counts are the player's own books only (P7): read where the player rules the settlement.
                bool own = where >= 0 && EmpireQuery.ControlsSettlement(c.W, c.P, new SettlementId(where));
                long? m = own ? GranaryEffects.LocalGranaries(c.W, c.Cfg, new SettlementId(where)) : null;
                long? n = own ? GranaryEffects.EmpireGranaries(c.W, c.Cfg, new SettlementId(where)) : null;
                // C1: the farm factor is the literal 1.0 while the settlement gathers wild food — say so (own books only).
                bool? wild = own ? LabourActivities.HarvestsWildFood(c.W, c.Cfg, new SettlementId(where)) : null;
                b.Enables.Add(new InfoLink(GranaryFarmLabel, null, null, GranaryFarmEffect(granaries, m, wild)));
                b.Enables.Add(new InfoLink(GranaryFamineLabel, null, null, GranaryFamineEffect(granaries, n)));
            }
            b.Enables.Add(new InfoLink(LevyServicesLabel, null, null,
                "once a tax levy exists, each one built eases how heavily the levy is felt here, as far as your administration reaches"));
        }
        b.RealizedBy = incremental
            ? "ConstructionSystem builds it from the queue a little each turn, as far as its builders and the next units of its "
              + "materials allow, oldest order first; what has gone in stays while it waits. Cancelling a queued one loses what has "
              + "gone in - nothing is refunded. A part-built one does nothing; a built structure holds no goods - it is counted, not "
              + "stocked - and in this build only what is listed under what it enables reads it."
            : "ConstructionSystem builds it from the queue. A built structure holds no goods - it is counted, not stocked - and in this build only what is listed under what it enables reads it.";
        if (b.Status is InfoStatus.Locked or InfoStatus.Blocked)
            b.Summary = b.Status == InfoStatus.Locked ? claimedReason ?? "needs " + NeedsList(c, b.WhyLocked) : blocker;
        if (b.TreeNode < 0) b.TreeNode = ProjectTreeTarget(c, p, where);
    }

    /// <summary>
    /// D-055 (batch-4 design §1.6, T1b): the card note naming what the hold rule SETS ASIDE of <paramref name="good"/> ahead of
    /// the card's entry — "; 22 set aside for S0's granary, ahead in the building order" (several heads: "…for S0's granary
    /// and S2's workshop, …", in the building order) — the heads of <paramref name="purse"/> among the first
    /// <paramref name="before"/> heads of the walk (all of them for −1: a would-be new order). For a queued head (before ≥ 0;
    /// T1b fix round, finding F4) what the heads ahead DRAW this step is named first, since its Met counts it: "; 5 used this
    /// turn by S0's Granary and 10 set aside for S1's Granary, ahead in the building order". "" when there is neither.
    /// </summary>
    private static string SetAsideNote(Ctx c, HoldsWalkResult walk, int purse, int good, int before)
    {
        string used = before >= 0 ? Units(c, SetAsideQuery.Drawers(walk, purse, good, before), " used this turn by ") : "";
        string aside = Units(c, SetAsideQuery.Holders(walk, purse, good, before), " set aside for ");
        if (used.Length == 0 && aside.Length == 0) return "";
        return "; " + (used.Length > 0 && aside.Length > 0 ? used + " and " + aside : used + aside) + ", ahead in the building order";
    }

    /// <summary>"22 set aside for S0's granary and S2's workshop" — the heads' units summed and their names in the building
    /// order, joined by <paramref name="verb"/>; "" for no head.</summary>
    private static string Units(Ctx c, (QueueEntryRef Head, long Units)[] heads, string verb)
    {
        if (heads.Length == 0) return "";
        long total = 0;
        var names = new List<string>(heads.Length);
        foreach ((QueueEntryRef head, long units) in heads)
        {
            total = checked(total + units);
            ConstructionProjectEntry? project = c.Cfg.Goods!.ProjectById(head.ProjectId);
            names.Add(c.Name(head.Settlement.Value) + "'s " + (project is null ? "project " + Inv(head.ProjectId) : ProjectName(c, project)));
        }
        string list = names.Count == 1 ? names[0] : string.Join(", ", names.GetRange(0, names.Count - 1)) + " and " + names[^1];
        return Inv(total) + verb + list;
    }

    /// <summary>The places (ascending id, by the card's names) whose queued projects claim <paramref name="good"/> in the
    /// purse of settlement <paramref name="where"/> (ConstructionQuery.ClaimedAt).</summary>
    private static string ClaimedPlaces(Ctx c, IReadOnlyTable<ConstructionQueueRow> claims, IReadOnlyTable<ConstructionProgressRow> progress, int where, int good)
    {
        SettlementId[] at = ConstructionQuery.ClaimedAt(claims, progress, c.Cfg.Goods!, ConstructionQuery.Holders(c.W, c.Cfg, new SettlementId(where)), new GoodId(good));
        var names = new string[at.Length];
        for (int i = 0; i < at.Length; i++) names[i] = c.Name(at[i].Value);
        return string.Join(", ", names);
    }

    /// <summary>A blocker ("needs 40 timber (empire has 0)") as what a project will wait for ("40 timber (empire has 0)").</summary>
    public static string WithoutNeeds(string blocker) =>
        blocker.StartsWith("needs ", StringComparison.Ordinal) ? blocker["needs ".Length..] : blocker;

    /// <summary>What the purse of settlement <paramref name="where"/> holds of <paramref name="good"/> as the card's viewer
    /// may read it (ConstructionQuery.HoldersSeenBy — the very holding ConstructionSystem gates and draws on where the
    /// viewer rules the settlement); 0 with no settlement.</summary>
    private static long PurseHeld(Ctx c, int where, int good) =>
        where < 0 ? 0 : EmpireStockpile.Available(c.W.GoodStocks, ConstructionQuery.HoldersSeenBy(c.W, c.Cfg, c.P, new SettlementId(where)), new GoodId(good));

    /// <summary>M5 item 5: the label of a granary card's LOCAL farm-output line (subject-less, in WHAT IT ENABLES).</summary>
    public const string GranaryFarmLabel = "Farm output here";

    /// <summary>M5 item 5: the label of a granary card's EMPIRE-WIDE famine line (subject-less, in WHAT IT ENABLES).</summary>
    public const string GranaryFamineLabel = "Famine relief across the civilization";

    /// <summary>The label of a public work's levy-offset line (subject-less, in WHAT IT ENABLES).</summary>
    public const string LevyServicesLabel = "Public services under a levy";

    /// <summary>
    /// M5 item 5 (D-053 §3): what a granary does to farm output at the settlement it stands in —
    /// GranaryEffects.FarmYieldFactor on CULTIVATED grain (never wild food), stated with the current count of STANDING
    /// granaries <paramref name="m"/> (null when the player does not rule the place: no count is shown), the next
    /// granary's step, the horizon below which each further one adds strictly less (GranaryEffects.FarmStrictlyLessBelow,
    /// measured on the factor itself) and the ceiling. Never a storage claim. While the settlement still GATHERS WILD FOOD
    /// (<paramref name="harvestsWildFood"/> true — LabourActivities.HarvestsWildFood, the predicate ProductionSystem.Farm
    /// skips the factor on) the factor there is 1.0, so the line says "no effect while this settlement gathers wild food"
    /// and states the effect only as what it becomes once the settlement cultivates; null (a place the player does not
    /// rule) states the effect without either claim.
    /// </summary>
    public static string GranaryFarmEffect(GranariesConfig g, long? m, bool? harvestsWildFood = null)
    {
        string effect = GranaryFarmEffectCultivating(g, m, harvestsWildFood != true);
        return harvestsWildFood == true ? "no effect while this settlement gathers wild food; once it cultivates: " + effect : effect;
    }

    private static string GranaryFarmEffectCultivating(GranariesConfig g, long? m, bool inForce)
    {
        string cap = "+" + GranaryEffects.Percent(g.FarmYieldBonusMax) + "%";
        string tail = "each further one adds strictly less below about " + Inv(GranaryEffects.FarmStrictlyLessBelow(g))
            + " here, nothing that shows after; never more than " + cap + " in all";
        if (m is not { } built || built <= 0)
            return "farm output here +" + GranaryEffects.FarmBonusPercent(g, 1) + "% with a first standing granary - it saves part of the "
                + "cultivated crop lost after the harvest (never wild food); " + tail;
        string now = "+" + GranaryEffects.FarmBonusPercent(g, built) + "%";
        string step = "+" + GranaryEffects.Percent(GranaryEffects.FarmYieldFactor(g, built + 1) - GranaryEffects.FarmYieldFactor(g, built)) + "%";
        return "farm output here " + now + (inForce ? " now" : "") + " from " + Inv(built) + (built == 1 ? " standing granary" : " standing granaries")
            + "; one more adds " + step + " - " + tail;
    }

    /// <summary>
    /// M5 item 5 (D-053 §3): what a granary does to famine across its civilization — GranaryEffects.FamineIntensityFactor
    /// on the EFFECTIVE deficit, i.e. the shortfall that drives starvation and birth suppression in a severe shortage or a
    /// famine, stated with the civilization's current count of STANDING granaries <paramref name="n"/> (null: not shown),
    /// the next granary's step, the strict-diminishing horizon and the ceiling. It claims a cut to that SHORTFALL only:
    /// lost births do not fall by the same percentage (fertility suppression is already total at dEff ≥ 1/3), and no
    /// grain moves (organised relief, not stored grain).
    /// </summary>
    public static string GranaryFamineEffect(GranariesConfig g, long? n)
    {
        string cap = "-" + GranaryEffects.Percent(g.FamineReductionMax) + "%";
        const string What = "the shortfall behind starvation and birth suppression in a severe shortage or a famine, in every settlement of the civilization";
        const string How = " - organised relief, no grain moves; births recover only where the eased shortfall falls below the depth that halts them";
        string tail = "each further one adds strictly less below about " + Inv(GranaryEffects.FamineStrictlyLessBelow(g))
            + ", nothing that shows after; never more than " + cap + " in all";
        if (n is not { } built || built <= 0)
            return What + ": -" + GranaryEffects.FamineReductionPercent(g, 1) + "% with a first standing granary anywhere in it; " + tail + How;
        string now = "-" + GranaryEffects.FamineReductionPercent(g, built) + "%";
        string step = "-" + GranaryEffects.Percent(GranaryEffects.FamineIntensityFactor(g, built) - GranaryEffects.FamineIntensityFactor(g, built + 1)) + "%";
        return What + ": " + now + " now from " + Inv(built) + (built == 1 ? " standing granary" : " standing granaries")
            + "; one more adds " + step + " - " + tail + How;
    }

    /// <summary>
    /// M5 item 5: the build button's brief form of the LOCAL effect, as a multiplier (the action surface states no
    /// percentages): the RESULTING TOTAL harvest factor the settlement holds with one more STANDING granary —
    /// <paramref name="m"/> standing there now; queued granaries are not counted — and the ceiling.
    /// "with one more standing granary here: cultivated harvest x1.03 in total (never above x1.05)". While the settlement
    /// still gathers wild food (<paramref name="harvestsWildFood"/>; C1 — the factor is 1.0 there) it says so first:
    /// "no effect while this settlement gathers wild food; once it cultivates, with one more standing granary here: …".
    /// </summary>
    public static string GranaryFarmBrief(GranariesConfig g, long m, bool harvestsWildFood = false) =>
        (harvestsWildFood ? "no effect while this settlement gathers wild food; once it cultivates, " : "")
        + "with one more standing granary here: cultivated harvest x" + F2(GranaryEffects.FarmYieldFactor(g, Math.Max(0, m) + 1))
        + " in total (never above x" + F2(1.0 + g.FarmYieldBonusMax) + ")";

    /// <summary>
    /// M5 item 5: the build button's brief form of the EMPIRE-WIDE effect, as a multiplier: the RESULTING TOTAL
    /// famine-shortfall factor the civilization holds with one more STANDING granary — <paramref name="n"/> standing in it
    /// now; queued granaries are not counted — and the floor.
    /// "with one more standing granary in the civilization: famine shortfall x0.90 in total (never below x0.75)".
    /// </summary>
    public static string GranaryFamineBrief(GranariesConfig g, long n) =>
        "with one more standing granary in the civilization: famine shortfall x" + F2(GranaryEffects.FamineIntensityFactor(g, Math.Max(0, n) + 1))
        + " in total (never below x" + F2(1.0 - g.FamineReductionMax) + ")";

    private static string F2(double v) => v.ToString("0.00", CultureInfo.InvariantCulture);

    private static int ProjectTreeTarget(Ctx c, ConstructionProjectEntry p, int where)
    {
        if (c.R is not { } r) return -1;
        foreach (string? id in new[] { p.Entity, p.Founds?.Entity })
            if (id is not null && r.EntityIndexOf(id) is int e and >= 0 && EntityTreeTarget(c, e) is int t and >= 0) return t;
        foreach (ProjectInput i in p.Inputs)
        {
            long held = PurseHeld(c, where, c.Cfg.Goods!.IdOf(i.Good));
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
        // P7: a formation the player does not know reads exactly as an id that names nothing — no field of it is read.
        if (found is not { } unit || !c.K.KnowsUnit(unit)) { b.Unknown("a formation with this id"); return; }
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

    // ================================================================== map cell (M5 P8)

    /// <summary>
    /// M5 P8 — A MAP CELL, from <see cref="SiteAssessment"/>: the simulation's one composite (the unjittered siting score)
    /// decomposed into its two factors with its rank against the floor, then — each in its own section, each labelled
    /// "not part of the score" — the founding predicate's arms from a source, the route, the expected deposits, discovery /
    /// presence / access / extraction, the would-be catchment, the terrain fields and the nearest known settlements, and
    /// what the simulation does not model. No weight is invented. A cell the player does not know is refused: nothing of it
    /// is read (its title says only that it is undiscovered). THE PLAYER'S CLASS (ADR-037 §7): unless the player sees the
    /// whole world, every value that would read beyond what it knows is shown as "not judged" (the assessment withholds
    /// it), and the card never narrates a settlement the player does not know.
    /// </summary>
    private static void CellCard(Ctx c, B b)
    {
        int cell = b.Subject.Id is >= 0 and <= int.MaxValue ? (int)b.Subject.Id : -1;
        if (c.W.Terrain is not { } terrain || cell < 0 || cell >= terrain.Size * terrain.Size) { b.Unknown("map cell with this index"); return; }
        int size = terrain.Size;
        double kmPerPx = terrain.KmPerPx;
        b.Kind = "Map cell - " + Num(kmPerPx) + " km square";
        b.Effect = InfoEffect.Simulated;
        b.RealizedBy = "Read-only: inspecting a place never changes the world and never queues an order. Each value is the simulation's own"
            + " function's (SettlementSiting, FoundingQuery, WorldFounding, Pathfinder and the catchment's partition), called, never restated.";
        if (!c.K.KnowsCell(cell))
        {
            // THE REFUSAL (P7 seam): nothing of an undiscovered cell is read — not even whether it is land.
            b.Title = "Undiscovered ground";
            b.What = "You have not discovered this place, so nothing about it is shown.";
            b.Status = InfoStatus.Unknown;
            b.StatusLine = "Undiscovered.";
            return;
        }
        b.Title = (terrain.Water[cell] >= 0.5 ? "Water" : "Land") + " at " + Inv(cell % size) + ", " + Inv(cell / size);
        if (c.Site is not { } site || !site.IsFor(terrain)) { b.Inert("siting configuration (the founding path's worldgen)"); return; }

        SiteAssessment.Report r = SiteAssessment.Assess(c.W, c.Cfg, site, c.K, c.P, cell, b.Subject.Settlement, batch: c.Batch);
        string from = r.Source >= 0 ? c.Name(r.Source) : "";
        b.What = "What the simulation holds for this place. Its siting score is the one composite the simulation ranks sites by;"
            + " everything after it is shown separately and is NOT part of that score - no weights are invented.";
        b.Status = InfoStatus.Known;
        b.StatusLine = (r.Verdict is { } verdict ? FoundingLine(verdict, from, r.RefusedByOrder) : FoundingNotJudged) + ".";

        // ---- 1. THE COMPOSITE: the simulation's siting score, its two factors, its rank against the floor.
        int side = 2 * r.FertilityRadiusPx + 1;
        string composite = "The simulation's own composite (SettlementSiting.CandidateScores, without the founding jitter): turn-zero siting, emergent"
            + " colonies and the AI's founding pick rank sites by it. Two factors, multiplied; nothing else is in it.";
        string box = "Fertility summed over the " + Inv(side) + " x " + Inv(side) + " cells around this one (" + Num(side * kmPerPx) + " km square)";
        var score = new List<InfoLink>
        {
            r.ScoreUnseen
                ? new InfoLink("Siting score: not judged", null, null, composite + " Not judged: a factor's reach includes ground you have not discovered.")
                : new InfoLink("Siting score " + Num3(r.Score) + " = region fertility " + Num3(r.RegionFertility) + " x water access " + Num3(r.WaterAccess),
                    null, r.SeesAll ? r.Candidate : null, composite + (r.IsWater ? " Water scores 0." : "")),
            r.RegionFertilityUnseen
                ? new InfoLink("Region fertility: not judged", null, null, box + " - they include ground you have not discovered.")
                : new InfoLink("Region fertility " + Num3(r.RegionFertility), null, null, box + "."),
            r.WaterAccessUnseen
                ? new InfoLink("Water access: not judged", null, null, "The nearest water or river may lie in ground you have not discovered.")
                : new InfoLink("Water access " + Num3(r.WaterAccess), null, null, WaterAccessNote(r)),
            !r.ScoreUnseen && !(r.Score > 0.0)
                ? new InfoLink("No positive score", null, false, r.IsWater ? "Water." : "No water or river within " + Inv(r.WaterAccessCutoffPx) + " cells.")
                : !r.SeesAll
                    ? new InfoLink("Rank among the places with a positive score: not judged", null, null, UnseenWholeMap)
                    : new InfoLink("Scores above " + Pct(r.ScoresBelow / (double)r.PositiveScores) + " of the " + N0(r.PositiveScores) + " places with a positive score"),
            !r.SeesAll
                ? new InfoLink("Score floor: not judged - the " + Pct(r.FloorPercentile) + " point of the positive scores", null, null, UnseenWholeMap)
                : new InfoLink("Score floor " + Num3(r.Floor) + " - the " + Pct(r.FloorPercentile) + " point of the positive scores", null, r.Candidate,
                    r.Candidate ? "This place is at or above it: a candidate site." : "Land under the floor, or water, is never a site (SettlementSiting.IsFrontierCandidate)."),
        };
        b.More.Add(new InfoSection("The simulation's siting score", [.. score]));

        // ---- 2. THE FOUNDING PREDICATE, arm by arm (not part of the score).
        var arms = new List<InfoLink>
        {
            r.Verdict is { } v
                ? new InfoLink("FoundingQuery: " + FoundingLine(v, from, r.RefusedByOrder), null, v == FoundingVerdict.Legal)
                : new InfoLink("FoundingQuery: " + FoundingNotJudged, null, null),
        };
        foreach (SiteAssessment.FoundingArm a in r.Arms) arms.Add(new InfoLink(ArmLabel(c, r, a.Arm, from), null, a.Holds, ArmNote(c, r, a)));
        b.More.Add(new InfoSection("Founding here" + (from.Length > 0 ? " from " + from : "") + " - not part of the score", [.. arms]));

        // ---- 3. THE ROUTE from the source, and access from any settlement of the player's (not part of the score).
        var route = new List<InfoLink>();
        if (r.Source < 0) route.Add(new InfoLink("You hold no settlement to set out from", null, false));
        else if (!r.SeesAll)
            route.Add(new InfoLink("Route from " + from + ": not judged", null, null,
                "Pathfinder.FindPath runs over the whole traversal lattice and every road, and you do not see all of them."));
        else if (r.Route is { Found: true } path)
            route.Add(new InfoLink("Route from " + from + ": " + Inv(path.Nodes.Length - 1) + " steps, travel cost " + Num(path.TotalCost)
                + " (about " + N0(r.RouteKm) + " km of ideal ground)", null, true,
                "Pathfinder.FindPath over the traversal lattice and its roads (RouteQuery) - the route a founding party's order is judged on."
                + " A cost unit buys at most " + Num(kmPerPx * Pathing.TraversalLattice.DefaultStride) + " km, less on rough ground."));
        else route.Add(new InfoLink("No land route from " + from, null, false));
        route.Add(!r.SeesAll ? new InfoLink("Reachable from your settlements: not judged", null, null)
            : new InfoLink(r.AccessibleFrom >= 0 ? "Reachable from your settlements (from " + c.Name(r.AccessibleFrom) + ")" : "Reachable from none of your settlements",
                null, r.AccessibleFrom >= 0));
        int others = 0;
        foreach (SettlementId mine in c.Controlled)
        {
            if (mine.Value == r.Source) continue;
            if (others++ == 8) break;
            route.Add(new InfoLink("Set out from " + c.Name(mine.Value) + " instead", InfoSubject.OfCell(cell, mine.Value)));
        }
        b.More.Add(new InfoSection("Route - not part of the score", [.. route]));

        // ---- 4. DEPOSITS a settlement founded here would get: expected per good (not part of the score).
        var deposits = new List<InfoLink>();
        int present = 0;
        if (r.DepositsUnavailable is { } why) deposits.Add(new InfoLink("Not evaluated: " + why, null, null));
        else
        {
            deposits.Add(new InfoLink("Hinterland within " + Num(r.HinterlandRadiusPx * kmPerPx) + " km: mean moisture " + Num3(r.MeanMoisture)
                + ", mean height above sea " + Num3(r.MeanElevation), null, null,
                "WorldFounding.HinterlandMeans over the land in that disc. Each deposit good's abundance is one channel of these (goods.json depositChannel)."));
            foreach (SiteAssessment.DepositPreview d in r.Deposits)
            {
                if (d.ChannelValue > 0.0) present++;
                string amount = d.Spread > 0.0
                    ? Num3(d.Low) + " to " + Num3(d.High) + " (expected " + Num3(d.ChannelValue) + ")"
                    : Num3(d.ChannelValue) + " (expected)";
                string note = "channel: " + d.Channel + " " + Num3(d.ChannelValue)
                    + (d.Spread > 0.0 ? "; a seeded spread of up to " + Pct(d.Spread) + " either way, keyed to the settlement's id, not to the place" : "")
                    + (d.IfFoundedNext is { } x ? "; if founded next: " + Num3(x) : "")
                    + "; worked by " + SectorLabel(c, d.Herded ? Sectors.Herding : Sectors.Extraction) + " labour";
                deposits.Add(new InfoLink(Cap(GoodName(c.Cfg.Goods, d.Good.Value)) + ": " + amount, InfoSubject.OfGood(d.Good.Value), d.ChannelValue > 0.0, note));
            }
            if (r.RollWithheld is { } withheld) deposits.Add(new InfoLink("The exact roll for the next settlement is not shown: " + withheld, null, null));
        }
        b.More.Add(new InfoSection("Deposits a settlement here would get (expected) - not part of the score", [.. deposits]));

        // ---- 5. DISCOVERY, PRESENCE, ACCESS, EXTRACTION — four different questions (not part of the score).
        b.More.Add(new InfoSection("Discovered, present, reachable, workable - not part of the score",
        [
            new("Discovered", null, true, c.K.AllKnown
                ? "Every place is known until a discovered extent is stored (ADR-037); the founding predicate has no known-extent arm yet."
                : "In your known extent."),
            new("Present", null, r.DepositsUnavailable is null ? present > 0 : null, r.DepositsUnavailable is null
                ? Inv(present) + " of " + Inv(r.Deposits.Length) + " deposit goods are expected above 0 here."
                : "Not evaluated."),
            !r.SeesAll
                ? new InfoLink("Reachable", null, null, "Not judged: routes run over the whole lattice and every road, and you do not see all of them.")
                : new InfoLink("Reachable", null, r.AccessibleFrom >= 0, r.AccessibleFrom >= 0
                    ? "A land route from " + c.Name(r.AccessibleFrom) + "."
                    : "No land route from any settlement of yours."),
            new("Workable", null, r.Source >= 0 ? r.ColonyWorkable : null, r.Source < 0 ? "No source settlement."
                : r.ColonyWorkable
                    ? "A colony takes its source's ruler, so its herding and extraction labour would be yours to set (LabourActivities); production works a deposit with no research gate."
                    : "A colony from " + from + " would not be yours, so its labour would not be yours to set."),
        ]));

        // ---- 6. THE LAND it would work: the would-be catchment (not part of the score).
        b.More.Add(new InfoSection("The land a settlement here would work - not part of the score",
        [
            r.WouldBeArableKm2 is { } arable
                ? new InfoLink(N0(arable) + " sq km of fertility-weighted arable land over " + Inv(r.WouldBeNodes) + " lattice nodes", null, null,
                    "Pathfinder.Partition with this site added - the catchment the recompute after a founding here would give it, if nothing else changed first.")
                : new InfoLink("Not evaluated: " + r.CatchmentUnavailable, null, null),
            r.ClaimedBy >= 0 ? new InfoLink("In " + c.Name(r.ClaimedBy) + "'s catchment", null, false)
                : r.SeesAll ? new InfoLink("Unclaimed: in no settlement's catchment", null, true)
                : new InfoLink("Claim: not judged", null, null, UnseenClaim),
        ]));

        // ---- 7. THE TERRAIN FIELDS at the cell (not part of the score).
        b.More.Add(new InfoSection("This place - not part of the score",
        [
            new((r.Elevation >= r.SeaLevel ? "Height above sea " + Num3(r.Elevation - r.SeaLevel) : "Below sea level by " + Num3(r.SeaLevel - r.Elevation))
                + " (elevation " + Num3(r.Elevation) + ", sea level " + Num3(r.SeaLevel) + ")"),
            new("Temperature " + Num(r.TemperatureC) + " C"),
            new("Moisture " + Num3(r.Moisture) + ", fertility " + Num3(r.Fertility)),
            new("Movement cost " + Num3(r.MovementCost) + " a cell"),
            new(r.River ? "A river runs here" : "No river here"),
        ]));

        // ---- 8. THE NEAREST SETTLEMENTS the player knows, straight line (not part of the score).
        var near = new List<InfoLink>();
        foreach (SiteAssessment.Nearby n in r.Nearest)
        {
            string whose = !EmpireQuery.TryGetController(c.W, n.Id, out PolityId ruler) ? "under no civilization"
                : ruler.Value == c.P.Value ? "yours" : "another civilization's";
            near.Add(new InfoLink(c.Name(n.Id.Value) + ": " + N0(n.Km) + " km", null, null, whose + (n.Id.Value == r.OccupiedBy ? "; it stands here" : "")));
        }
        if (near.Count == 0) near.Add(new InfoLink("None you know"));
        b.More.Add(new InfoSection("Nearest settlements you know (straight line) - not part of the score", [.. near]));

        // ---- 9. WHAT THE SIMULATION DOES NOT MODEL here (data gaps, stated rather than filled).
        b.More.Add(new InfoSection("Not simulated",
        [
            new("A resource quantity per place", null, null, "Deposits are written per settlement when it is founded; the values above are what that function gives a settlement here."),
            new("Resource discovery", null, null, "No research reveals or unlocks a raw resource, and production works deposits with no research gate."),
            new("Resource availability classes and regional richness (D-051 section 2)", null, null, "Ratified, not built: they will change the deposit function this card calls."),
            new("A land claim other than a catchment", null, null, "No system writes claims."),
            new("Farmland taken by a settlement's own footprint (D-009)", null, null, "Not built: the arable figure is the whole catchment's."),
        ]));
    }

    // ================================================================== Found Settlement (M5 S1d)

    /// <summary>
    /// M5 S1d — THE FOUND SETTLEMENT ORDER FROM ONE SETTLEMENT (D-051 §5): what the order does, the SOURCE-side arms of
    /// THE predicate read on this world (the source is yours; the party <see cref="FoundingQuery.AllocateParty"/> would form;
    /// the provisions <see cref="FoundingQuery.ProvisionsShare"/> would send — <see cref="FoundingQuery.Departure"/>, the
    /// function the intake applies), whether one is already ordered this turn, and that the PLACE is chosen on the map,
    /// where each place's card judges the rest of the predicate. It carries the live descriptor when
    /// <see cref="AvailableActionsQuery.Colonization"/> lists it. A settlement the player does not know names nothing.
    /// </summary>
    private static void FoundingCard(Ctx c, B b)
    {
        int src = b.Subject.Id is >= 0 and <= int.MaxValue ? (int)b.Subject.Id : -1;
        var id = new SettlementId(src);
        bool stands = false;
        for (int i = 0; i < c.W.Settlements.Count && src >= 0; i++) if (c.W.Settlements[i].Id == id) { stands = true; break; }
        if (!stands || !c.K.KnowsSettlement(id)) { b.Unknown("settlement you know with this id"); return; }
        string from = c.Name(src);
        b.Title = "Found a settlement";
        b.Kind = "An order from " + from + " - settlers leave it with provisions and found a settlement where you choose";
        b.What = "A party of " + from + "'s people sets out with its share of the grain and founds a settlement on a place you choose on the map; "
            + "the settlement is yours, as its source is. People and grain move only through the Ledger: nothing is created.";
        b.Effect = InfoEffect.Simulated;
        b.RealizedBy = "ColonizationSystem founds it at End Turn (the settlement stands the next turn) when FoundingQuery.Check answers Legal "
            + "on the world it was ordered from - the predicate the placement mode and the AI ask too.";
        ActionDescriptor? action = Find(c, ActionDomain.Colonization, src);
        b.Action = action;

        bool mine = EmpireQuery.ControlsSettlement(c.W, c.P, id);
        // D-060: the party and its provisions are figures of the source's BOOKS (its people, its grain store) — read only
        // where the viewer may read them (PolityKnowledge.ReadsInternals); another empire's source names neither.
        bool books = c.K.ReadsInternals(id);
        long party = 0, provisions = 0;
        FoundingVerdict departure = books
            ? FoundingQuery.Departure(c.W.Buckets, c.W.GoodStocks, c.Cfg, id, null, out party, FoundingQuery.PartyFraction(c.W, c.Cfg, id))
            : FoundingVerdict.NotControlled;
        if (books && c.Cfg.Goods is { } goods) provisions = FoundingQuery.ProvisionsShare(c.W.GoodStocks, c.W.Buckets, id, new GoodId(goods.GrainId), party);
        bool queued = c.Q.Queued is { } q && AvailableActionsQuery.FoundingQueued(q, c.P, id);
        string grain = GoodName(c.Cfg.Goods, c.Cfg.Goods?.GrainId ?? -1);
        b.Requirements.Add(new InfoLink(from + " is yours", null, mine));
        if (books)
        {
            b.Requirements.Add(new InfoLink("A party: " + N0(party) + " people would leave", null, mine ? party >= 1 : null,
                "FoundingQuery.AllocateParty: " + Pct(FoundingQuery.PartyFraction(c.W, c.Cfg, id)) + " of the source's people, every group keeping one."));
            b.Requirements.Add(new InfoLink("Provisions: " + N0(provisions) + " " + grain + " would go with them", null, mine && party >= 1 ? provisions >= 1 : null,
                "FoundingQuery.ProvisionsShare: the party's per-head share of the source's grain store."));
        }
        else b.Requirements.Add(new InfoLink("A party and its provisions: not known to you", null, null,
            from + "'s people and grain store are its own empire's books."));
        b.Requirements.Add(new InfoLink("The place: chosen on the map", null, null,
            "Each place's card judges the rest - on the map, land, at or above the siting floor, no settlement there or too near, in no catchment, a land route from " + from + "."));

        if (action is not null)
        {
            b.Status = InfoStatus.Available;
            b.StatusLine = "Available: choose a place on the map (POLICY - Choose a site..., or F).";
            b.Summary = action.Detail;
        }
        else
        {
            b.Status = InfoStatus.Locked;
            b.StatusLine = !mine ? "Not available: " + from + " is not yours to send settlers from."
                : queued ? "Already ordered this turn: the settlers leave " + from + " at End Turn."
                : departure == FoundingVerdict.NoParty ? "Not available: " + from + " cannot spare a party."
                : departure == FoundingVerdict.NoProvisions ? "Not available: " + from + " cannot provision a party."
                : c.W.Terrain is null ? "Not available: this world has no map to settle." : "Not available.";
        }
    }

    /// <summary>P8 (the player's class): the predicate's answer when an arm it reaches depends on what the player has not seen.</summary>
    private const string FoundingNotJudged = "not judged - whether a Found Settlement order would be accepted here depends on what you have not seen";

    private const string UnseenWholeMap = "Not judged: it is taken over every place on the map, including ground you have not discovered.";

    private const string UnseenClaim = "Not judged: no settlement you know claims it, and whether another does depends on what you have not seen.";

    /// <summary>The founding predicate's verdict in the player's words (the place card's status line; M5 S1d: the placement mode's
    /// hint and bar say the same).</summary>
    public static string FoundingLine(FoundingVerdict v, string from) => v switch
    {
        FoundingVerdict.Legal => "a Found Settlement order from " + from + " would be accepted",
        FoundingVerdict.UnknownSource => "you hold no settlement to send settlers from",
        FoundingVerdict.NotControlled => from + " is not yours to send settlers from",
        FoundingVerdict.OffMap => "off the map",
        FoundingVerdict.Water => "water - no settlement can stand here",
        FoundingVerdict.PoorSite => "below the siting floor - not a site",
        FoundingVerdict.Occupied => "a settlement already stands here",
        FoundingVerdict.TooClose => "too close to a standing settlement",
        FoundingVerdict.Claimed => "inside a settlement's catchment",
        FoundingVerdict.NoParty => from + " cannot spare a party",
        FoundingVerdict.NoProvisions => from + " cannot provision a party",
        FoundingVerdict.Unreachable => "no land route from " + from,
        FoundingVerdict.UnknownSite => "ground you have not discovered - explore it first",
        _ => "not judged",
    };

    /// <summary>M5 S1d review: <see cref="FoundingLine(FoundingVerdict, string)"/>, except that a refusal only an order queued
    /// this turn causes (<paramref name="byOrder"/>: <see cref="SiteAssessment.Batch.RefusesByOrder"/>) is told as such — no
    /// settlement stands there yet. The place card's status line and the placement mode's hint and bar say the same.</summary>
    public static string FoundingLine(FoundingVerdict v, string from, bool byOrder) => byOrder ? v switch
    {
        FoundingVerdict.Occupied => "a settlement you ordered this turn will stand here",
        FoundingVerdict.TooClose => "too close to a settlement you ordered this turn",
        _ => FoundingLine(v, from),
    } : FoundingLine(v, from);

    private static string ArmLabel(Ctx c, SiteAssessment.Report r, FoundingVerdict arm, string from) => arm switch
    {
        FoundingVerdict.UnknownSource => r.Source >= 0 ? "Settlers set out from " + from : "A settlement to send settlers from",
        FoundingVerdict.NotControlled => (from.Length > 0 ? from : "The source") + " is yours",
        FoundingVerdict.OffMap => "On the map",
        FoundingVerdict.Water => "Land",
        FoundingVerdict.PoorSite => "At or above the score floor",
        FoundingVerdict.Occupied => r.OrderedSites > 0 ? "No settlement stands or is ordered here" : "No settlement stands here",
        FoundingVerdict.TooClose => "Outside every settlement's spacing" + (r.OrderedSites > 0 ? ", those you ordered this turn included" : "")
            + " (" + Num(r.MinSpacingKm) + " km of travel on ideal ground)",
        FoundingVerdict.Claimed => "In no settlement's catchment",
        FoundingVerdict.NoParty => "A party: " + N0(r.Party) + " people would leave",
        FoundingVerdict.NoProvisions => "Provisions: " + N0(r.Provisions) + " " + GoodName(c.Cfg.Goods, c.Cfg.Goods?.GrainId ?? -1) + " would go with them",
        FoundingVerdict.Unreachable => "A land route" + (from.Length > 0 ? " from " + from : ""),
        FoundingVerdict.UnknownSite => "Ground you have discovered",
        _ => arm.ToString(),
    };

    private static string? ArmNote(Ctx c, SiteAssessment.Report r, SiteAssessment.FoundingArm a)
    {
        if (a.Unseen)
            return a.Arm switch
            {
                FoundingVerdict.PoorSite => UnseenWholeMap,
                FoundingVerdict.Occupied => "Not judged: this place is not in sight now.",
                FoundingVerdict.TooClose => "Not judged: the rule measures travel to every settlement over the lattice, and you do not see all of it.",
                FoundingVerdict.Claimed => UnseenClaim,
                FoundingVerdict.Unreachable => "Not judged: the route runs over the whole lattice and every road, and you do not see all of them.",
                _ => "Not judged: it depends on what you have not seen.",
            };
        if (a.Holds is null) return "Not judged: an earlier arm decides it.";
        // OccupiedBy / ClaimedBy name only a settlement the player knows (SiteAssessment: every one, while it sees them all).
        return a.Arm switch
        {
            FoundingVerdict.Occupied when r.OccupiedBy >= 0 => "The site of " + c.Name(r.OccupiedBy) + ".",
            FoundingVerdict.Occupied when r.OccupiedByOrder => "A settlement you ordered this turn will stand here: its order is queued for End Turn.",
            FoundingVerdict.TooClose when r.TooCloseByOrder => "A settlement you ordered this turn will stand " + Num(r.BatchSpacingCost)
                + " travel-cost units away; the rule needs " + Num(r.MinSpacingCost) + ".",
            FoundingVerdict.TooClose => double.IsPositiveInfinity(r.SpacingCost)
                ? "No standing site within the spacing reach."
                : "The nearest standing site is " + Num(r.SpacingCost) + " travel-cost units away; the rule needs " + Num(r.MinSpacingCost) + ".",
            FoundingVerdict.Claimed when r.ClaimedBy >= 0 => "In " + c.Name(r.ClaimedBy) + "'s catchment.",
            FoundingVerdict.NoParty => "FoundingQuery.AllocateParty: " + Pct(FoundingQuery.PartyFraction(c.Cfg)) + " of the source's people, every group keeping one.",
            FoundingVerdict.NoProvisions => "FoundingQuery.ProvisionsShare: the party's per-head share of the source's grain store.",
            _ => null,
        };
    }

    private static string WaterAccessNote(SiteAssessment.Report r)
    {
        string falloff = "1 at a shore or riverbank, falling to 0 at " + Inv(r.WaterAccessCutoffPx) + " cells (" + Num(r.WaterAccessCutoffPx * r.KmPerPx) + " km)";
        return r.WaterDistancePx == int.MaxValue
            ? falloff + "; no water or river can be reached from here."
            : r.WaterDistancePx < 0
                ? falloff + "; no water or river lies within " + Inv(r.WaterAccessCutoffPx) + " cells of here."
                : falloff + "; this place is " + Inv(r.WaterDistancePx) + " cells (" + Num(r.WaterDistancePx * r.KmPerPx) + " km) from water.";
    }

    // ================================================================== tax edict

    /// <summary>
    /// WS2 (D-054) — THE BUILDING/ROADS MATERIAL SPLIT: the rule (State.ResourceAllocation) in words, the split in force for
    /// the issuer (its ResourceAllocationRow, or the default "building first"), and the order that sets it — the
    /// descriptor the query lists (ActionDomain.ResourceAllocation), whose predicate is the one ConstructionSystem applies
    /// (ConstructionQuery.AllocationOrderValid). It reads only the issuer's own row.
    /// </summary>
    private static void MaterialsSplitCard(Ctx c, B b)
    {
        b.Title = "Materials for building and roads";
        b.Kind = "Policy - a standing order: how building work and road works share the empire's materials";
        string goods = SplitGoods(c.Cfg);
        b.What = "On a turn you order road works, building work is guaranteed its share of the empire's " + goods
            + " and road works the rest. Whatever one side does not need, the other may use; on turns without road works, "
            + "building work may use all of it. The shares are guaranteed shares of what the empire holds when building "
            + "work starts, never amounts spent regardless of need: nothing is used that the work does not need. "
            + "Dwellings take their own settlement's timber before either.";
        b.Effect = InfoEffect.Simulated;
        b.RealizedBy = "ConstructionSystem applies the split (SetResourceAllocation) to each turn's building work - the grant "
            + "of each good is the guaranteed share plus the road share road works do not need - and RoadDevelopmentSystem "
            + "pays later in the turn from what is left.";
        if (!ResourceAllocation.Enabled(c.Cfg)) { b.Inert("building/roads material split (sim.json construction.allocationRatios)"); return; }
        int pct = ResourceAllocation.EffectivePercent(c.W, c.P);
        string inForce = pct == ResourceAllocation.DefaultBuildingsPercent
            ? Reservations.Enabled(c.Cfg)
                ? "Building first (default): road works get only what is left after building work uses or sets aside what it needs."
                : "Building first (default): road works get only what building work leaves."
            : "In force: building work keeps " + SplitShares(pct) + ".";
        b.Action = Find(c, ActionDomain.ResourceAllocation, 1);
        if (b.Action is not null)
        {
            b.Status = InfoStatus.Available;
            b.StatusLine = inForce;
        }
        else
        {
            b.Status = InfoStatus.Known;
            b.StatusLine = inForce + " It matters once your empire can order road works.";
        }
    }

    /// <summary>The goods the split covers, from content (project inputs and road materials), in good-id order.</summary>
    private static string SplitGoods(SimConfig cfg)
    {
        int[] ids = ResourceAllocation.EligibleGoodIds(cfg);
        if (ids.Length == 0 || cfg.Goods is not { } goods) return "building materials";
        var names = new List<string>(ids.Length);
        foreach (int id in ids) names.Add(goods.ById(id).Name);
        return names.Count == 1 ? names[0] : string.Join(", ", names, 0, names.Count - 1) + " and " + names[^1];
    }

    /// <summary>"7 shares in 10, road works 3" (or "73 shares in 100, road works 27") — never a percent sign.</summary>
    public static string SplitShares(int buildingsPercent) => buildingsPercent % 10 == 0
        ? Inv(buildingsPercent / 10) + " shares in 10, road works " + Inv((100 - buildingsPercent) / 10)
        : Inv(buildingsPercent) + " shares in 100, road works " + Inv(100 - buildingsPercent);

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
        // ADR-051 (tuning.researchReserve): the reserve and what the next step does with it; null with the switch off.
        ReserveStatus? reserve = ResearchQuery.Reserve(c.W, r, c.P);
        int open = 0;
        for (int i = 0; i < r.Nodes.Count; i++) if (c.Available(i)) open++;
        ResearchNodeId t = default;
        bool held = r.Tuning.TwoLanes
            ? ResearchQuery.TryGetTarget(c.W, r, c.P, ResearchTree.Technology, out t)   // ADR-043: the Technology lane
            : ResearchQuery.TryGetTarget(c.W, c.P, out t);
        if (r.Tuning.DestinationRouting)
        {
            // rp-cp-lifecycle k2–k4: with destination routing the card reports what the step FUNDS — the lane's route
            // (ResearchQuery.Route, the planner's own answer) — never the destination row's progress in its place. In the
            // one-pool model the one destination's route is reported on its own tree's lane.
            ResearchTree lane = !r.Tuning.TwoLanes && held && r.IndexOf(t) is int hi and >= 0 ? r.Nodes[hi].Tree : ResearchTree.Technology;
            ResearchRoute route = c.RouteOn(lane);
            switch (ResearchQuery.Activity(route))
            {
                case LaneActivity.Researching:
                case LaneActivity.Assisting:
                {
                    int fi = r.IndexOf(route.Current!.Value);
                    b.Status = InfoStatus.Researching;
                    b.StatusLine = "Researching " + r.Nodes[fi].Name + Toward(c, route) + ": " + N0(ResearchQuery.Progress(c.W, c.P, route.Current.Value))
                        + " of " + N0(ResearchQuery.EffectiveCost(c.W, r, c.P, fi)) + " research points, +" + N1(rp) + " a turn"
                        + (reserve is { } rs ? "; " + rs.Funding : "") + ".";
                    bool own = route.Destination is { } own0 && (!route.Assisting || own0 != route.AssistedDestination);
                    if (route.Assisting && own)
                        b.StatusLine += " Your destination " + NameOf(r, route.Destination) + " waits on the " + OtherTreeName(route.Lane) + " tree.";
                    b.TreeNode = fi;
                    if (!route.Assisting && route.Destination == route.Current) b.Enables.Add(NodeLink(c, fi, "the current target"));
                    else
                    {
                        b.Enables.Add(NodeLink(c, fi, route.Assisting ? "researched now, assisting the " + OtherTreeName(route.Lane) + " tree" : "researched now"));
                        if (own) b.Enables.Add(NodeLink(c, r.IndexOf(route.Destination!.Value)));   // its state reads "your destination"
                    }
                    b.Action = Find(c, ActionDomain.Research, own ? 2 : 1);
                    break;
                }
                case LaneActivity.Waiting:
                {
                    int di = r.IndexOf(route.Destination!.Value);
                    b.Status = InfoStatus.Blocked;
                    b.StatusLine = Waiting(c, route) + "; " + (reserve is { } rs ? rs.Unused : N1(rp) + " research points go unused this turn") + ".";
                    b.TreeNode = di;
                    b.Enables.Add(NodeLink(c, di));   // its state reads "your destination"
                    b.Action = Find(c, ActionDomain.Research, 2);
                    b.Summary = "waiting - research points unused this turn";
                    break;
                }
                default:
                    Idle();
                    break;
            }
        }
        else if (held && r.IndexOf(t) is int ti and >= 0)
        {
            b.Status = InfoStatus.Researching;
            b.StatusLine = "Researching " + r.Nodes[ti].Name + ": " + N0(ResearchQuery.Progress(c.W, c.P, t)) + " of "
                + N0(ResearchQuery.EffectiveCost(c.W, r, c.P, ti)) + " research points, +" + N1(rp) + " a turn"
                + (reserve is { } rs ? "; " + rs.Funding : "") + ".";
            b.TreeNode = ti;
            b.Enables.Add(NodeLink(c, ti, "the current target"));
            b.Action = Find(c, ActionDomain.Research, 2);
        }
        else Idle();
        if (r.Tuning.TwoLanes)
        {
            // ADR-043: the two lanes, stated — RP fund the technology target; banked Civics Points fund the civics target.
            b.Kind = "Knowledge - research points a turn fund the technology target; civics points, banked, fund the civics target";
            CivicsPointsRow account = ResearchQuery.CivicsPointsAccount(c.W, c.P);
            // rp-cp-lifecycle k5: the CP the NEXT step accrues (the era table's dt at the world's date, the context's), not the
            // dt of the step that produced this world.
            double cp = ResearchQuery.CivicsPointsMilli(c.W, r, c.P, c.NextDtYears) / 1000.0;
            string civic;
            if (r.Tuning.DestinationRouting)
            {
                ResearchRoute route = c.RouteOn(ResearchTree.Civics);
                switch (ResearchQuery.Activity(route))
                {
                    case LaneActivity.Researching:
                    case LaneActivity.Assisting:
                    {
                        int fi = r.IndexOf(route.Current!.Value);
                        string points = N0(ResearchQuery.Progress(c.W, c.P, route.Current.Value)) + " of " + N0(ResearchQuery.EffectiveCost(c.W, r, c.P, fi)) + " civics points";
                        civic = !route.Assisting && route.Destination == route.Current
                            ? "civics target " + r.Nodes[fi].Name + ": " + points
                            : "researching " + r.Nodes[fi].Name + Toward(c, route) + ": " + points;
                        if (route.Assisting && route.Destination is { } own && own != route.AssistedDestination)
                            civic += " (your civics destination " + NameOf(r, own) + " waits on the Technology tree)";
                        break;
                    }
                    case LaneActivity.Waiting:
                    {
                        string waiting = Waiting(c, route);
                        civic = char.ToLowerInvariant(waiting[0]) + waiting[1..] + ", civics points stay banked";
                        break;
                    }
                    default:
                        civic = "no civics target";
                        break;
                }
            }
            else
                civic = ResearchQuery.TryGetTarget(c.W, r, c.P, ResearchTree.Civics, out ResearchNodeId ct) && r.IndexOf(ct) is int ci and >= 0
                    ? "civics target " + r.Nodes[ci].Name + ": " + N0(ResearchQuery.Progress(c.W, c.P, ct)) + " of " + N0(ResearchQuery.EffectiveCost(c.W, r, c.P, ci)) + " civics points"
                    : "no civics target";
            b.StatusLine += " Civics: " + civic + "; " + N1(account.BalanceMilli / 1000.0) + " civics points banked, +" + N1(cp) + " a turn.";
        }

        void Idle()
        {
            b.Status = InfoStatus.Available;
            b.StatusLine = "Idle: " + (reserve is { } rs ? rs.Unused : N1(rp) + " research points a turn go unused") + "; " + Inv(open) + " subjects are open.";
            b.Action = Find(c, ActionDomain.Research, 1);
            b.Summary = "idle - choose a research target";
        }
    }

    /// <summary>rp-cp-lifecycle k2/k3: what a funding lane's current node is FOR — "" when it is the destination itself,
    /// " (toward D)" for a route node, " (assisting the Civics tree toward D)" for a free-lane assist.</summary>
    private static string Toward(Ctx c, ResearchRoute route) =>
        route.Assisting ? " (assisting the " + OtherTreeName(route.Lane) + " tree toward " + NameOf(c.R!, route.AssistedDestination) + ")"
        : route.Destination == route.Current ? ""
        : " (toward " + NameOf(c.R!, route.Destination) + ")";

    /// <summary>rp-cp-lifecycle k4: a waiting lane — "Waiting on the Civics tree: TD needs CV first" (up to three of the
    /// other tree's outstanding route nodes, ResearchQuery.WaitingOn), or, when the route holds none, that no node of the
    /// route can be researched now.</summary>
    private static string Waiting(Ctx c, ResearchRoute route)
    {
        ResearchContent r = c.R!;
        List<ResearchNodeId> on = ResearchQuery.WaitingOn(r, route);
        string dest = NameOf(r, route.Destination);
        if (on.Count == 0) return "Waiting: no node on the route to " + dest + " can be researched now";
        var names = new List<string>(3);
        for (int k = 0; k < on.Count && k < 3; k++) names.Add(NameOf(r, on[k]));
        string list = string.Join(", ", names) + (on.Count > 3 ? " and " + Inv(on.Count - 3) + " more" : "");
        return "Waiting on the " + OtherTreeName(route.Lane) + " tree: " + dest + " needs " + list + " first";
    }

    private static string OtherTreeName(ResearchTree lane) => lane == ResearchTree.Civics ? "Technology" : "Civics";

    private static string NameOf(ResearchContent r, ResearchNodeId? key) =>
        key is { } k && r.IndexOf(k) is int i and >= 0 ? r.Nodes[i].Name : "-";

    /// <summary>The lane route whose next step funds node <paramref name="i"/> (destination routing on), or null: its own
    /// tree's lane in the two-lane model; either route in the one-pool model (its one lane funds both trees).</summary>
    private static ResearchRoute? FundingRoute(Ctx c, int i)
    {
        ResearchNode n = c.R!.Nodes[i];
        ResearchRoute own = c.RouteOn(n.Tree);
        if (own.Current == n.Key) return own;
        if (c.R.Tuning.TwoLanes) return null;
        ResearchRoute other = c.RouteOn(n.Tree == ResearchTree.Civics ? ResearchTree.Technology : ResearchTree.Civics);
        return other.Current == n.Key ? other : null;
    }

    /// <summary>Whether the node is the polity's target on its own lane (ADR-043; in the one-pool model, the one target).</summary>
    private static bool IsTarget(Ctx c, ResearchNode n) =>
        c.R is { } r && (r.Tuning.TwoLanes
            ? ResearchQuery.TryGetTarget(c.W, r, c.P, n.Tree, out ResearchNodeId lt) && lt.Value == n.Key.Value
            : ResearchQuery.TryGetTarget(c.W, c.P, out ResearchNodeId t) && t.Value == n.Key.Value);

    // ================================================================== shared: knowledge links and the tree target

    private static InfoLink NodeLink(Ctx c, int i, string? extra = null)
    {
        ResearchNode n = c.R!.Nodes[i];
        bool target = IsTarget(c, n);
        // rp-cp-lifecycle k2: with destination routing "being researched" is the node a lane funds (ResearchQuery.Route);
        // a destination not yet open is "your destination".
        bool funded = c.R.Tuning.DestinationRouting ? !c.Done[i] && c.Available(i) && FundingRoute(c, i) is not null : target;
        string state = c.Done[i] ? "known" : funded ? "being researched" : target ? "your destination" : c.Available(i) ? "open to research now" : "locked";
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

    /// <summary>CR-025 ruling: the card line of a declared future effect — "Future (M7 Battle Layer - …): …, not simulated
    /// in this build".</summary>
    public static string FutureLabel(FutureEffect e) =>
        "Future (" + e.Milestone + " " + e.System + "): " + e.Description + ", not simulated in this build";

    /// <summary>CR-025 ruling: the note under a future-effect line — who will realize it, and the operand it would act on.</summary>
    private static string FutureNote(FutureEffect e) =>
        (string.Equals(e.Milestone, ResearchContentLoader.UnscheduledMilestone, StringComparison.Ordinal)
            ? "declared future effect: its system is design intent with no milestone yet"
            : "declared future effect: arrives with " + e.Milestone)
        + (e.Operand is { } op ? " (" + KnowledgeOperands.Of(op).Id + ")" : "");

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
        foreach (SettlementId s in c.Controlled) if (Held(c.W, s, id) > 0 || Deposit(c, s, id) > 0.0) return true;
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

    /// <summary>M6 EX (ADR-044): a deposit as the settlement's controller knows it — an undiscovered concealed row reads 0
    /// (State.DepositConcealment, the one predicate).</summary>
    private static double Deposit(Ctx c, SettlementId s, int good) => DepositConcealment.KnownAbundance(c.W, c.Cfg, s, good);

    /// <summary>The sector that works a deposit good: production's own classification (<see cref="LabourActivities.IsHerded"/>,
    /// the rule LabourActivities.GoodsOf files a deposit under) — Herding/fishing for livestock and fish, Extraction for
    /// every other deposit. P7: read from CONTENT, never by scanning settlements (it used to ask GoodsOf of the first
    /// settlement anywhere holding the deposit — a foreign one included — and fell back to Extraction when none did).</summary>
    private static int DepositSector(Ctx c, int good) =>
        c.Cfg.Goods is { } goods && LabourActivities.IsHerded(goods, good) ? Sectors.Herding : Sectors.Extraction;

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
        /// <summary>P7: what the player knows (the one knowledge seam).</summary>
        public readonly PolityKnowledge K;
        /// <summary>P8: the terrain half of a site assessment (map cells), or null.</summary>
        public readonly SiteAssessment.Prepared? Site;
        /// <summary>M5 S1d review: the founding batch a map cell's card is judged against, or null.</summary>
        public readonly SiteAssessment.Batch? Batch;
        private ImmutableArray<ActionDescriptor>? _actions;
        private SettlementId[]? _controlled;
        private ResearchRoute? _technologyRoute, _civicsRoute;

        public Ctx(IReadOnlyWorldState w, SimConfig cfg, PolityId p, ActionQueryContext q, Func<int, string>? name, ImmutableArray<ActionDescriptor>? actions,
            PolityKnowledge k, SiteAssessment.Prepared? site = null, SiteAssessment.Batch? batch = null)
        {
            W = w; Cfg = cfg; P = p; Q = q; NameFn = name; _actions = actions; K = k; Site = site; Batch = batch;
            R = cfg.Research;
            Done = R is null ? [] : ResearchQuery.CompletedMask(w, R, p);
            Stage = R is not null && ResearchQuery.StageReached(R, Done);
        }

        public ImmutableArray<ActionDescriptor> Actions => _actions ??= AvailableActionsQuery.For(W, Cfg, P, Q);
        /// <summary>rp-cp-lifecycle k5: the length in years of the step the next End Turn plays — the context's (the era
        /// table's dt at the world's date, <see cref="ActionQueryContext.ForNextStep"/>); without one, the clock's last dt.</summary>
        public double NextDtYears => Q.NextDtYears ?? W.Clock.DtYears;
        /// <summary>ADR-048: a lane's route (<see cref="ResearchQuery.Route"/>), computed once per card.</summary>
        public ResearchRoute RouteOn(ResearchTree lane) => lane == ResearchTree.Civics
            ? _civicsRoute ??= ResearchQuery.Route(W, R!, P, ResearchTree.Civics)
            : _technologyRoute ??= ResearchQuery.Route(W, R!, P, ResearchTree.Technology);
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
