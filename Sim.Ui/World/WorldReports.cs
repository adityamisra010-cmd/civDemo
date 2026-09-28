namespace Sim.Ui.World;

// THE WORLD VIEW BOUNDARY (docs/architecture/world-visualization.md §2).
//
// Everything a map layer draws arrives here as a REPORT: a plain, immutable statement by a
// source of what exists and what can be seen of it from outside. The renderer never decides
// that a gameplay entity exists — it only depicts reports. There are six narrow sources
// (settlements, polities, structures, infrastructure, resources, mobile agents); each
// returns immutable snapshots, and none has a setter or a command.
//
// Two kinds of source implement them today:
//   · LIVE adapters (Sim.Ui/World/Live) over the authoritative IReadOnlyWorldState, for what
//     the simulation already holds: settlements (with their SizeTier), polities and control,
//     the M4-D structures, the deposits, the built network, notables.
//   · the DEMO source (PLACEHOLDER, ui-content/world/demo-world.json) for what it does not
//     hold yet: institutions, armies, roles for people, cultural groups.
// A scene is built from ONE bundle — all live or all demo — so a demo entity can never be
// drawn on the real map as if the simulation had produced it.

/// <summary>What kind of thing an id names. Part of the id: a settlement "12" and a
/// structure "12" are different entities.</summary>
public enum WorldEntityKind { Settlement = 0, Structure = 1, InfraNode = 2, InfraEdge = 3, Resource = 4, Agent = 5 }

/// <summary>
/// A STABLE entity id: the kind plus the key the SOURCE assigned (the authoritative id for a
/// live source, zero-padded so ordinal order is numeric order; the content key for the demo).
/// The view never mints ids for entities, so an entity keeps its id for as long as its source
/// reports it. Ordered by (kind, key ordinal) — the stable tie-break every ranking ends in.
/// </summary>
public readonly record struct WorldEntityId(WorldEntityKind Kind, string Key) : IComparable<WorldEntityId>
{
    public int CompareTo(WorldEntityId other)
    {
        int k = ((int)Kind).CompareTo((int)other.Kind);
        return k != 0 ? k : string.CompareOrdinal(Key, other.Key);
    }

    public override string ToString() => Kind switch
    {
        WorldEntityKind.Settlement => "settlement:",
        WorldEntityKind.Structure => "structure:",
        WorldEntityKind.InfraNode => "node:",
        WorldEntityKind.InfraEdge => "edge:",
        WorldEntityKind.Resource => "resource:",
        _ => "agent:",
    } + Key;
}

/// <summary>A continuous world position (double X, double Y) in world units (the live map's
/// unit is one terrain cell) — never a tile index.</summary>
public readonly record struct WorldPoint(double X, double Y);

/// <summary>
/// What a source says about whether the viewer can see an entity. The renderer never
/// computes visibility. The ruled shape of the future model is a computed per-polity extent
/// with separate, lagged position and strength channels (D-040 B1, D-039 A2/B3) — not a flag
/// — so this is a PRESENTATION vocabulary a source maps onto, not that model:
/// <list type="bullet">
/// <item><see cref="NotModelled"/>: the source has no knowledge model (the live simulation
/// today): drawn, and the details say "visibility not modelled — omniscient view".</item>
/// <item><see cref="Visible"/>: drawn normally.</item>
/// <item><see cref="Remembered"/>: drawn ghosted and not selectable (last known).</item>
/// <item><see cref="Hidden"/>: neither drawn nor hit-tested nor counted.</item>
/// </list></summary>
public enum ReportedVisibility { NotModelled = 0, Visible = 1, Remembered = 2, Hidden = 3 }

/// <summary>Which relation a report's polity key names. The simulation keeps these distinct
/// (a settlement's CONTROLLER, a notable's ALLEGIANCE — D-037/D-042); the details panel
/// prints the relation, never a generic "owner".</summary>
public enum PolityRelation { None = 0, Controller = 1, Allegiance = 2, Owner = 3 }

/// <summary>A named number a source reports (capacity, maturity, staff, served, grade,
/// population, sizeTier, …). Morphology CONTENT chooses which one drives a visual stage, so a
/// future authoritative input needs a content edit, not a renderer change.</summary>
public readonly record struct ReportedInput(string Name, double Value);

/// <summary>A named categorical value a source reports ("maturity" = "ESTABLISHED", …).
/// Content may map its values onto visual stages through an ordinal table.</summary>
public readonly record struct ReportedLabel(string Name, string Value);

internal static class ReportLookup
{
    public static double? Input(IReadOnlyList<ReportedInput> inputs, string name)
    {
        for (int i = 0; i < inputs.Count; i++) if (string.Equals(inputs[i].Name, name, StringComparison.Ordinal)) return inputs[i].Value;
        return null;
    }

    public static string? Label(IReadOnlyList<ReportedLabel> labels, string name)
    {
        for (int i = 0; i < labels.Count; i++) if (string.Equals(labels[i].Name, name, StringComparison.Ordinal)) return labels[i].Value;
        return null;
    }
}

/// <summary>A settlement. <see cref="SourceId"/> is the authoritative numeric id when the
/// source is the live simulation (null for the demo): the ONLY way the game may mirror a
/// world selection onto its own settlement selection — never by parsing a key.</summary>
public sealed record SettlementReport(
    string Key, string DisplayName, string? PolityKey, PolityRelation Relation, WorldPoint Position,
    IReadOnlyList<ReportedInput> Inputs, ReportedVisibility Visibility, string Note, long? SourceId = null)
{
    public double? Input(string name) => ReportLookup.Input(Inputs, name);
}

/// <summary>A polity. <see cref="InkSeed"/> is a stable number the view maps onto a map ink
/// (live: the polity id), so a polity's colour depends on the polity alone — never on how
/// many others are listed.</summary>
public sealed record PolityReport(string Key, string DisplayName, long InkSeed, string Note);

/// <summary>
/// A building or institution — a CAPABILITY OF ITS SETTLEMENT (D-038 H2): it has no world
/// coordinates of its own, only the settlement it belongs to. Where it is drawn inside the
/// settlement's composed sprite is a VISUAL choice (docs/architecture/world-visualization.md
/// §6). <see cref="Established"/> is the source's ordinal of when the entity was first
/// reported (composition priority only; null when the source has none — the live M4-D
/// counts). <see cref="Multiplicity"/> lets one report stand for several identical
/// structures (the live StructureRow is a count per settlement and project); it is drawn as
/// ONE token with a ×n badge and is never expanded into invented per-building ids.
/// </summary>
public sealed record StructureReport(
    string Key, string DisplayName, string VisualType, string SettlementKey, string? PolityKey, PolityRelation Relation,
    long? Established, long Multiplicity, IReadOnlyList<ReportedInput> Inputs, IReadOnlyList<ReportedLabel> Labels,
    string? Specialization, string? State, ReportedVisibility Visibility, string Note)
{
    public double? Input(string name) => ReportLookup.Input(Inputs, name);
    public string? Label(string name) => ReportLookup.Label(Labels, name);
}

/// <summary>A typed node of the one infrastructure graph (D-009 ¶2: junctions, city gates,
/// ports, stations, airports).</summary>
public sealed record InfraNodeReport(
    string Key, string DisplayName, string NodeType, WorldPoint Position, string? SettlementKey,
    ReportedVisibility Visibility, string Note);

/// <summary>A typed edge of the one infrastructure graph (D-009 ¶2), between two reported
/// nodes. <see cref="VisualType"/> names the mode (road, rail, canal, …) for the content.</summary>
public sealed record InfraEdgeReport(
    string Key, string DisplayName, string VisualType, string NodeA, string NodeB, string? PolityKey, PolityRelation Relation,
    IReadOnlyList<ReportedInput> Inputs, ReportedVisibility Visibility, string Note)
{
    public double? Input(string name) => ReportLookup.Input(Inputs, name);
}

/// <summary>A resource at a settlement (live: a deposit — the good and its abundance).</summary>
public sealed record ResourceReport(
    string Key, string DisplayName, string ResourceType, string SettlementKey, IReadOnlyList<ReportedInput> Inputs,
    ReportedVisibility Visibility, string Note)
{
    public double? Input(string name) => ReportLookup.Input(Inputs, name);
}

/// <summary>Where on the infrastructure graph an agent is: an edge and a fraction along it
/// (0 at NodeA). The ruled shape for armies (D-009: they march on the network graph); a
/// future live adapter reports this and the view projects it to a map point.</summary>
public readonly record struct AgentGraphLocation(string EdgeKey, double Fraction);

/// <summary>
/// A mobile entity — a formation, a fleet, a person, a group — as ONE map entity.
/// <see cref="Count"/> is the reported number it stands for (personnel, ships, members); the
/// map never draws one token per person. Location is exactly one of: a continuous
/// <see cref="Position"/>, a <see cref="GraphLocation"/>, or an
/// <see cref="AttachedSettlementKey"/> (a person the simulation places only "at a
/// settlement", like a notable). <see cref="Members"/> optionally names who a group is.
/// </summary>
public sealed record AgentReport(
    string Key, string DisplayName, string DisplayType, string? PolityKey, PolityRelation Relation,
    WorldPoint? Position, AgentGraphLocation? GraphLocation, string? AttachedSettlementKey,
    double? HeadingDeg, long? Count, string? CountNoun, IReadOnlyList<string> Members,
    ReportedVisibility Visibility, string Note);

/// <summary>One source's report at one moment: immutable, sorted by key (ordinal), with a
/// duplicate key an error. <see cref="Sequence"/> is a CHANGE TOKEN only — it is never
/// drawn, hashed or serialized.</summary>
public sealed class Snapshot<T> where T : class
{
    public Snapshot(long sequence, string sourceLabel, bool isPlaceholder, IEnumerable<T> items, Func<T, string> key)
    {
        Sequence = sequence;
        SourceLabel = sourceLabel;
        IsPlaceholder = isPlaceholder;
        T[] sorted = items.ToArray();
        string[] keys = new string[sorted.Length];
        for (int i = 0; i < sorted.Length; i++) keys[i] = key(sorted[i]);
        Array.Sort(keys, sorted, StringComparer.Ordinal);
        for (int i = 1; i < keys.Length; i++)
            if (string.Equals(keys[i - 1], keys[i], StringComparison.Ordinal))
                throw new ArgumentException($"{sourceLabel}: duplicate key '{keys[i]}'");
        Items = sorted;
    }

    public long Sequence { get; }
    public string SourceLabel { get; }
    /// <summary>True for demo / placeholder data; the map labels it wherever it is drawn.</summary>
    public bool IsPlaceholder { get; }
    public IReadOnlyList<T> Items { get; }

    public static Snapshot<T> Empty(string label, bool placeholder, Func<T, string> key) => new(0, label, placeholder, [], key);
}

public interface ISettlementViewSource { Snapshot<SettlementReport> Current { get; } }
public interface IPolityViewSource { Snapshot<PolityReport> Current { get; } }
public interface IStructureViewSource { Snapshot<StructureReport> Current { get; } }
public interface IInfrastructureViewSource
{
    Snapshot<InfraNodeReport> Nodes { get; }
    Snapshot<InfraEdgeReport> Edges { get; }
}
public interface IResourceViewSource { Snapshot<ResourceReport> Current { get; } }
public interface IMobileAgentViewSource { Snapshot<AgentReport> Current { get; } }

/// <summary>
/// The six sources a scene reads — a bundle of references, not a manager: it owns nothing,
/// computes nothing and has no behaviour. <see cref="Label"/> names the bundle ("LIVE
/// simulation", "DEMO world") for the map's provenance banner; <see cref="Observer"/> says
/// whose knowledge the reports are (today always an omniscient observer — there is no
/// knowledge model yet, D-040 B1).
/// </summary>
public sealed record WorldSources(
    string Label, bool IsPlaceholder, string Observer,
    ISettlementViewSource Settlements, IPolityViewSource Polities, IStructureViewSource Structures,
    IInfrastructureViewSource Infrastructure, IResourceViewSource Resources, IMobileAgentViewSource Agents);

/// <summary>A source with nothing to report, and a label saying why (e.g. "no authoritative
/// producer: the simulation has no armies yet").</summary>
public sealed class EmptySource :
    ISettlementViewSource, IPolityViewSource, IStructureViewSource, IInfrastructureViewSource, IResourceViewSource, IMobileAgentViewSource
{
    // One immutable empty snapshot each, made once: an unchanged source returns the same
    // object, so a view cache keyed on snapshot identity never rebuilds for nothing.
    private readonly Snapshot<SettlementReport> _settlements;
    private readonly Snapshot<PolityReport> _polities;
    private readonly Snapshot<StructureReport> _structures;
    private readonly Snapshot<InfraNodeReport> _nodes;
    private readonly Snapshot<InfraEdgeReport> _edges;
    private readonly Snapshot<ResourceReport> _resources;
    private readonly Snapshot<AgentReport> _agents;

    public EmptySource(string label)
    {
        Label = label;
        _settlements = Snapshot<SettlementReport>.Empty(label, false, r => r.Key);
        _polities = Snapshot<PolityReport>.Empty(label, false, r => r.Key);
        _structures = Snapshot<StructureReport>.Empty(label, false, r => r.Key);
        _nodes = Snapshot<InfraNodeReport>.Empty(label, false, r => r.Key);
        _edges = Snapshot<InfraEdgeReport>.Empty(label, false, r => r.Key);
        _resources = Snapshot<ResourceReport>.Empty(label, false, r => r.Key);
        _agents = Snapshot<AgentReport>.Empty(label, false, r => r.Key);
    }

    public string Label { get; }
    Snapshot<SettlementReport> ISettlementViewSource.Current => _settlements;
    Snapshot<PolityReport> IPolityViewSource.Current => _polities;
    Snapshot<StructureReport> IStructureViewSource.Current => _structures;
    Snapshot<InfraNodeReport> IInfrastructureViewSource.Nodes => _nodes;
    Snapshot<InfraEdgeReport> IInfrastructureViewSource.Edges => _edges;
    Snapshot<ResourceReport> IResourceViewSource.Current => _resources;
    Snapshot<AgentReport> IMobileAgentViewSource.Current => _agents;
}
