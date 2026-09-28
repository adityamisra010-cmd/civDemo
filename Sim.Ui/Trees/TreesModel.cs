using Sim.Ui.Art.Glyphs;

namespace Sim.Ui.Trees;

// THE TREES + AGES — THE RESOLVED CONTENT MODEL (docs/architecture/the-trees-ui.md §3,
// docs/architecture/the-trees-content-schema.md).
//
// Everything in this file is EDITABLE CONTENT after validation: what the graph and the
// Ages ARE (names, types, relationships, state vocabularies, glyph bindings). None of it
// is runtime state — a node's current state, progress and research points come from a
// read-only state source (TreesStateSources.cs), never from here. None of it is
// simulation semantics either: a relationship is a visualization/data concept, and the
// `layering` flag only tells the LAYOUT which edges run left to right.
//
// Collections are kept in AUTHORING ORDER (the order the Director wrote them), and every
// lookup is by id through a dictionary that is never iterated, so every consumer —
// layout, painter, SVG writer — is deterministic.

/// <summary>Which axis a state describes: research (the ring), realization (the fill and
/// the stage pips), or an Age milestone.</summary>
public enum StateTrack { Research, Realization, Milestone }

/// <summary>What fills a node's silhouette in a given state.</summary>
public enum WashMode { None, Full, ResearchProgress, RealizationProgress, MilestoneProgress }

/// <summary>How a state reads in the research UI (researchable / researching / completed).</summary>
public enum ResearchRole { None, Researchable, Researching, Completed }

/// <summary>How an authored edge sentence maps onto the left-to-right flow:
/// Forward "A enables B" flows A→B; Reverse "A requires B" flows B→A; None is undirected.</summary>
public enum RelationFlow { Forward, Reverse, None }

public enum StrokeStyle { Solid, Dashed, Dotted }

/// <summary>Completion of an Age milestone, as reported by the state source.</summary>
public enum MilestoneCompletion { NotStarted, Partial, Complete }

/// <summary>A content diagnostic. Errors stop the content from loading; warnings are shown.</summary>
public enum DiagnosticSeverity { Warning, Error }

public sealed record ContentDiagnostic(DiagnosticSeverity Severity, string File, string Path, string Message)
{
    public override string ToString() => $"{Severity.ToString().ToUpperInvariant()} {File} {Path}: {Message}";
}

// --- The Trees -------------------------------------------------------------------

/// <summary>One of the player-facing lenses (KNOWLEDGE … APPLICATIONS). A lens is a VIEW
/// onto the one graph, never a separate tree.</summary>
public sealed record LensDef(string Id, string Name, int Order, GlyphDomain Mark, string ShortDescription);

public sealed record StateDef(
    string Id, string Name, StateTrack Track, GlyphState GlyphState, int Stage,
    WashMode Wash, ResearchRole ResearchRole, string Legend);

/// <summary>The ordered states a node type may occupy ("not every node needs every state").</summary>
public sealed record StateSetDef(string Id, string Description, IReadOnlyList<string> States);

public sealed record NodeTypeDef(
    string Id, string Name, GlyphBase Base, GlyphDomain Mark, string StateSet, string Description);

public sealed record RelationKindDef(
    string Id, string Name, string Verb, RelationFlow Flow, bool Layering,
    StrokeStyle Stroke, string Ink, bool Arrow, string Description);

/// <summary>A research cost as CONTENT. Null points = "TBD"; the UI never computes one.</summary>
public sealed record ResearchCostDef(long? Points, string Note);

/// <summary>One node of the one graph. <see cref="Domain"/> is its primary lens;
/// <see cref="AlsoIn"/> lists the other lenses it appears in.</summary>
public sealed record NodeDef(
    string Id, string Domain, IReadOnlyList<string> AlsoIn, string Type, string Name,
    string ShortDescription, string LongDescription,
    GlyphBase Base, GlyphDomain Mark, EraRegister Era, string VisualStyleNote,
    string? AgeFrom, string? AgeTo, IReadOnlyList<string> AgeRefs,
    ResearchCostDef? ResearchCost, IReadOnlyList<string> HistoricalReferences,
    string DirectorNotes, string? MilestoneRef, int? ColumnHint, bool Placeholder, int ContentIndex);

/// <summary>A typed relationship as authored: "<see cref="From"/> &lt;kind&gt; <see cref="To"/>".
/// <see cref="Origin"/> names where it was written ("edges", or the node field such as
/// "prerequisites" or "relatedIndustry").</summary>
public sealed record EdgeDef(string From, string To, string Kind, string Note, string Origin);

public sealed record TreesDocument(
    string Notice, bool Placeholder,
    IReadOnlyList<LensDef> Lenses, IReadOnlyList<StateDef> States, IReadOnlyList<StateSetDef> StateSets,
    IReadOnlyList<NodeTypeDef> NodeTypes, IReadOnlyList<RelationKindDef> RelationKinds,
    IReadOnlyList<NodeDef> Nodes, IReadOnlyList<EdgeDef> Edges)
{
    private readonly Dictionary<string, LensDef> _lenses = Lenses.ToDictionary(l => l.Id, StringComparer.Ordinal);
    private readonly Dictionary<string, StateDef> _states = States.ToDictionary(s => s.Id, StringComparer.Ordinal);
    private readonly Dictionary<string, StateSetDef> _sets = StateSets.ToDictionary(s => s.Id, StringComparer.Ordinal);
    private readonly Dictionary<string, NodeTypeDef> _types = NodeTypes.ToDictionary(t => t.Id, StringComparer.Ordinal);
    private readonly Dictionary<string, RelationKindDef> _kinds = RelationKinds.ToDictionary(k => k.Id, StringComparer.Ordinal);

    public LensDef Lens(string id) => _lenses[id];
    public StateDef State(string id) => _states[id];
    public bool TryState(string id, out StateDef state) => _states.TryGetValue(id, out state!);
    public StateSetDef StateSet(string id) => _sets[id];
    public NodeTypeDef NodeType(string id) => _types[id];
    public RelationKindDef Kind(string id) => _kinds[id];

    /// <summary>The states a node of <paramref name="typeId"/> may occupy, in order.</summary>
    public IReadOnlyList<string> StatesFor(string typeId) => StateSet(NodeType(typeId).StateSet).States;

    /// <summary>Lenses sorted by their <see cref="LensDef.Order"/>, ties by id.</summary>
    public IReadOnlyList<LensDef> LensesInOrder() =>
        Lenses.OrderBy(l => l.Order).ThenBy(l => l.Id, StringComparer.Ordinal).ToArray();
}

// --- Ages --------------------------------------------------------------------------

public sealed record MilestoneCategoryDef(string Id, string Name, bool Provisional);

/// <summary>Display metadata only — never a gate (CLAUDE.md law 4).</summary>
public sealed record DateRangeDef(string StartLabel, string EndLabel, long? StartYear, long? EndYear, string Note);

public sealed record AgeThemeDef(EraRegister Register, string Accent, string Motif);

/// <summary>A slot for the Age's Δt. Variable Δt is NOT implemented (DD-02): the slot
/// only carries what the Director later decides, for display.</summary>
public sealed record DeltaTPlaceholder(bool Placeholder, double? YearsPerTurn, string Note);

/// <summary>A slot for catch-up metadata. Catch-up is NOT implemented (DD-04).</summary>
public sealed record CatchUpPlaceholder(bool Placeholder, bool PathwayBased, string Note);

public sealed record AgeDef(
    string Id, string DisplayName, int Order, string ShortDescription, DateRangeDef DateRange,
    IReadOnlyList<string> Milestones, AgeThemeDef Theme, GlyphBase IconBase, GlyphDomain IconMark,
    string TransitionAnimation, string TransitionNote, DeltaTPlaceholder DeltaT, CatchUpPlaceholder CatchUp,
    string HistoricalContext, bool Placeholder);

public sealed record MilestoneDef(
    string Id, string Age, string Name, string Description, string Category, bool Mandatory,
    IReadOnlyList<string> Prerequisites, string EvidenceNotes, string HistoricalSource,
    IReadOnlyList<string> NodeRefs, bool Placeholder);

public sealed record AgesDocument(
    string Notice, bool Placeholder, IReadOnlyList<MilestoneCategoryDef> Categories,
    IReadOnlyList<AgeDef> Ages, IReadOnlyList<MilestoneDef> Milestones)
{
    private readonly Dictionary<string, AgeDef> _ages = Ages.ToDictionary(a => a.Id, StringComparer.Ordinal);
    private readonly Dictionary<string, MilestoneDef> _milestones = Milestones.ToDictionary(m => m.Id, StringComparer.Ordinal);
    private readonly Dictionary<string, MilestoneCategoryDef> _cats = Categories.ToDictionary(c => c.Id, StringComparer.Ordinal);

    public AgeDef Age(string id) => _ages[id];
    public bool TryAge(string id, out AgeDef age) => _ages.TryGetValue(id, out age!);
    public MilestoneDef Milestone(string id) => _milestones[id];
    public bool TryMilestone(string id, out MilestoneDef m) => _milestones.TryGetValue(id, out m!);
    public MilestoneCategoryDef Category(string id) => _cats[id];

    /// <summary>Ages sorted by order (unique — validated).</summary>
    public IReadOnlyList<AgeDef> InOrder() => Ages.OrderBy(a => a.Order).ToArray();

    /// <summary>The milestones of an Age, in the Age's listed order.</summary>
    public IReadOnlyList<MilestoneDef> MilestonesOf(string ageId) => Age(ageId).Milestones.Select(Milestone).ToArray();
}

// --- Gallery (building and unit placeholders) ----------------------------------------

public sealed record MaturityStageDef(string Id, string Name, int Stage, string Legend);
public sealed record StatusGlyphDef(string Id, string Name, GlyphState GlyphState, string Legend);
public sealed record VeterancyLevelDef(string Id, string Name, int Chevrons);
public sealed record BuildingDef(string Id, string Name, GlyphBase Base, GlyphDomain Mark, EraRegister Era, string Category, bool Placeholder);
public sealed record UnitDef(string Id, string Name, GlyphBase Base, GlyphDomain Mark, EraRegister Era, string Role, bool Placeholder);

public sealed record GalleryDocument(
    string Notice, IReadOnlyList<MaturityStageDef> MaturityStages, IReadOnlyList<StatusGlyphDef> OperationalStatuses,
    IReadOnlyList<StatusGlyphDef> UnitStates, IReadOnlyList<VeterancyLevelDef> VeterancyLevels,
    IReadOnlyList<BuildingDef> Buildings, IReadOnlyList<UnitDef> Units)
{
    public MaturityStageDef? Maturity(string? id) => id is null ? null : MaturityStages.FirstOrDefault(m => m.Id == id);
    public StatusGlyphDef? OperationalStatus(string id) => OperationalStatuses.FirstOrDefault(s => s.Id == id);
    public StatusGlyphDef? UnitState(string id) => UnitStates.FirstOrDefault(s => s.Id == id);
    public VeterancyLevelDef? Veterancy(string id) => VeterancyLevels.FirstOrDefault(v => v.Id == id);
}

// --- Animations (presentation only) -----------------------------------------------------

public enum AnimationKind { None, Pulse, FlashRing, Shimmer, PipFill, Sweep, FlowDots }

/// <summary>One presentation animation. <see cref="AppliesTo"/> ("node-state:researching",
/// "edge-kind:diffusesTo", …) names a STEADY state that loops; <see cref="Trigger"/>
/// ("node-enter:researched", "node-stage-up", …) names a TRANSITION that plays once.</summary>
public sealed record AnimationDef(
    string Id, AnimationKind Kind, string? AppliesTo, string? Trigger,
    double PeriodSeconds, double DurationSeconds, double Amplitude, int Dots, string Description);

public sealed record AnimationDocument(string Notice, IReadOnlyList<AnimationDef> Animations)
{
    /// <summary>The first animation whose AppliesTo equals <paramref name="key"/>, if any.</summary>
    public AnimationDef? Steady(string key) => Animations.FirstOrDefault(a => a.AppliesTo == key && a.Kind != AnimationKind.None);

    /// <summary>The first animation whose Trigger equals <paramref name="key"/>, if any.</summary>
    public AnimationDef? Transition(string key) => Animations.FirstOrDefault(a => a.Trigger == key && a.Kind != AnimationKind.None);
}

/// <summary>Everything the Trees/Ages UI reads from content, validated and resolved.</summary>
public sealed record TreesContentSet(
    TreesDocument Trees, AgesDocument Ages, GalleryDocument Gallery, AnimationDocument Animations,
    IReadOnlyList<ContentDiagnostic> Warnings, string SourceDescription);
