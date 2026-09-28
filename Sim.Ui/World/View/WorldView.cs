using Sim.Ui.Art.Glyphs;
using Sim.Ui.World.Content;

namespace Sim.Ui.World.View;

// THE WORLD VIEW MODEL (docs/architecture/world-visualization.md §4): what the reports look
// like once the visual morphology has been applied — stages chosen, composition slots
// assigned, aggregation decided, agents anchored. Every value here is either a REPORTED value
// (carried through untouched in the report) or a VISUAL choice (stage, slot, cluster, ink),
// and the inspector labels which is which. Nothing here is written back anywhere.

/// <summary>Which visual stage a report was given, and why — for the details panel.</summary>
/// <param name="Index">0-based stage.</param>
/// <param name="Count">How many stages the morphology defines.</param>
/// <param name="Name">What the stage is called on screen.</param>
/// <param name="Driver">The input or label that chose it.</param>
/// <param name="Reported">False when the driver was not reported (then the base stage is drawn).</param>
/// <param name="Explanation">One Latin-1 line: the driver, its value and the view threshold.</param>
public sealed record StageChoice(int Index, int Count, string Name, string Driver, bool Reported, string Explanation);

/// <summary>A settlement's composed sprite: its stage, its visible residential blocks (sprite
/// units) and its composition slots.</summary>
public sealed record SettlementView(
    WorldEntityId Id, SettlementReport Report, string? PolityInk, string? PolityName, StageChoice Stage,
    IReadOnlyList<LotGeometry> Blocks, bool Drawable)
{
    public string Key => Report.Key;
}

/// <summary>
/// A reported structure after morphology and composition. <see cref="Slot"/> is where it is
/// drawn INSIDE its settlement's sprite (sprite units, never world coordinates); null when it
/// is collapsed into its type's <see cref="ClusterView"/> (aggregation) or when its settlement
/// is not reported.
/// </summary>
public sealed record StructureView(
    WorldEntityId Id, StructureReport Report, VisualType Type, bool TypeKnown, StageChoice Stage, GlyphSpec Icon,
    string? StateName, SpecializationMark? Specialization, string? PolityInk, LotGeometry? Slot, string? ClusterKey,
    bool Drawable);

/// <summary>The one token that stands for a settlement's structures of one visual type beyond
/// the individually drawn ones (provisional aggregation, D-038 H8). Selecting it selects
/// <see cref="HitTarget"/>, its first member that is not merely remembered; when every member is
/// remembered the token is drawn ghosted and cannot be selected.</summary>
public sealed record ClusterView(
    string Key, string SettlementKey, VisualType Type, LotGeometry Slot, IReadOnlyList<WorldEntityId> Members,
    long TotalMultiplicity, GlyphSpec Icon, WorldEntityId? HitTarget, bool Ghost);

public sealed record NodeView(WorldEntityId Id, InfraNodeReport Report, NodeType? Type, bool Drawable);

public sealed record EdgeView(
    WorldEntityId Id, InfraEdgeReport Report, InfraType Type, bool TypeKnown, InfraStage Stage, StageChoice StageChoice,
    WorldPoint A, WorldPoint B, bool Drawable);

/// <summary>A reported resource at its settlement; <see cref="Index"/> of <see cref="RowCount"/>
/// places it in the row drawn under the settlement (both count every report, whatever its
/// visibility, so a hidden one leaves a gap rather than shifting the rest).</summary>
public sealed record ResourceView(WorldEntityId Id, ResourceReport Report, ResourceType? Type, int Index, int RowCount, bool Drawable);

public enum AgentAnchor { Position = 0, Graph = 1, Settlement = 2, Unresolved = 3 }

/// <summary>A reported mobile entity: ONE token whatever its count. <see cref="World"/> is its
/// world point — reported directly, projected from its graph location, or its settlement's
/// point (then <see cref="AttachIndex"/> is its fan lot beside the settlement, from the stable
/// hash of (settlement, agent) — see WorldViewBuilder.FanLots).</summary>
public sealed record AgentView(
    WorldEntityId Id, AgentReport Report, AgentType Type, bool TypeKnown, string? PolityInk, AgentAnchor Anchor,
    WorldPoint World, int AttachIndex, string? CountLabel, string? CountNoun, double? HeadingDeg, bool Drawable);

/// <summary>One line of the civilization summary: reported structures of one visual type.</summary>
public sealed record SummaryLine(string VisualType, string Name, long Reported, int Settlements);

/// <summary>The whole view for one bundle at one moment. Immutable.</summary>
public sealed class WorldView
{
    private readonly Dictionary<string, SettlementView> _settlements;
    private readonly Dictionary<string, PolityReport> _polities;
    private readonly Dictionary<WorldEntityId, int> _index;

    internal WorldView(
        string sourceLabel, bool isPlaceholder, string observer, IReadOnlyList<PolityReport> polities,
        IReadOnlyList<SettlementView> settlements, IReadOnlyList<StructureView> structures, IReadOnlyList<ClusterView> clusters,
        IReadOnlyList<NodeView> nodes, IReadOnlyList<EdgeView> edges, IReadOnlyList<ResourceView> resources,
        IReadOnlyList<AgentView> agents, IReadOnlyList<SummaryLine> summary, IReadOnlyList<string> notes)
    {
        SourceLabel = sourceLabel;
        IsPlaceholder = isPlaceholder;
        Observer = observer;
        Polities = polities;
        Settlements = settlements;
        Structures = structures;
        Clusters = clusters;
        Nodes = nodes;
        Edges = edges;
        Resources = resources;
        Agents = agents;
        Summary = summary;
        Notes = notes;
        _settlements = new Dictionary<string, SettlementView>(StringComparer.Ordinal);
        foreach (SettlementView s in settlements) _settlements[s.Key] = s;
        _polities = new Dictionary<string, PolityReport>(StringComparer.Ordinal);
        foreach (PolityReport p in polities) _polities[p.Key] = p;
        _index = new Dictionary<WorldEntityId, int>();
        for (int i = 0; i < settlements.Count; i++) _index[settlements[i].Id] = i;
        for (int i = 0; i < structures.Count; i++) _index[structures[i].Id] = i;
        for (int i = 0; i < nodes.Count; i++) _index[nodes[i].Id] = i;
        for (int i = 0; i < edges.Count; i++) _index[edges[i].Id] = i;
        for (int i = 0; i < resources.Count; i++) _index[resources[i].Id] = i;
        for (int i = 0; i < agents.Count; i++) _index[agents[i].Id] = i;
    }

    public string SourceLabel { get; }
    public bool IsPlaceholder { get; }
    public string Observer { get; }
    public IReadOnlyList<PolityReport> Polities { get; }
    public IReadOnlyList<SettlementView> Settlements { get; }
    public IReadOnlyList<StructureView> Structures { get; }
    public IReadOnlyList<ClusterView> Clusters { get; }
    public IReadOnlyList<NodeView> Nodes { get; }
    public IReadOnlyList<EdgeView> Edges { get; }
    public IReadOnlyList<ResourceView> Resources { get; }
    public IReadOnlyList<AgentView> Agents { get; }
    public IReadOnlyList<SummaryLine> Summary { get; }
    /// <summary>What the builder could not depict and why (unresolved anchors, unknown types).</summary>
    public IReadOnlyList<string> Notes { get; }

    public SettlementView? Settlement(string key) => _settlements.TryGetValue(key, out SettlementView? s) ? s : null;
    public PolityReport? Polity(string? key) => key is not null && _polities.TryGetValue(key, out PolityReport? p) ? p : null;
    public bool Contains(WorldEntityId id) => _index.ContainsKey(id);

    public SettlementView? FindSettlement(WorldEntityId id) =>
        id.Kind == WorldEntityKind.Settlement && _index.TryGetValue(id, out int i) ? Settlements[i] : null;
    public StructureView? FindStructure(WorldEntityId id) =>
        id.Kind == WorldEntityKind.Structure && _index.TryGetValue(id, out int i) ? Structures[i] : null;
    public NodeView? FindNode(WorldEntityId id) =>
        id.Kind == WorldEntityKind.InfraNode && _index.TryGetValue(id, out int i) ? Nodes[i] : null;
    public EdgeView? FindEdge(WorldEntityId id) =>
        id.Kind == WorldEntityKind.InfraEdge && _index.TryGetValue(id, out int i) ? Edges[i] : null;
    public ResourceView? FindResource(WorldEntityId id) =>
        id.Kind == WorldEntityKind.Resource && _index.TryGetValue(id, out int i) ? Resources[i] : null;
    public AgentView? FindAgent(WorldEntityId id) =>
        id.Kind == WorldEntityKind.Agent && _index.TryGetValue(id, out int i) ? Agents[i] : null;
}
