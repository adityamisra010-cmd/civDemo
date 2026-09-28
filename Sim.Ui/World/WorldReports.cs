namespace Sim.Ui.World;

// THE WORLD VIEW BOUNDARY (docs/architecture/world-visualization.md §2).
//
// Everything a map layer draws arrives here as a REPORT: a plain, immutable statement by a
// source of what exists and what it looks like from the outside. The renderer never
// decides that a gameplay entity exists — it only depicts reports. There are five narrow
// sources (settlements, polities, structures, infrastructure, mobile agents); each returns
// one immutable snapshot, and none has a setter or a command.
//
// Two kinds of source implement them today:
//   · LIVE adapters (Sim.Ui/World/Live) over the authoritative IReadOnlyWorldState, for the
//     things the simulation already holds: settlements, polities, notables, the network.
//   · the DEMO source (PLACEHOLDER, ui-content/world/demo-world.json) for the things it does
//     not hold yet: buildings, institutions, armies, roles, cultural groups.
// A scene is built from ONE bundle — all live or all demo — so demo entities can never be
// drawn on the real map as if the simulation had produced them.

/// <summary>What kind of thing an id names. Part of the id: a settlement "12" and a
/// structure "12" are different entities.</summary>
public enum WorldEntityKind { Settlement = 0, Structure = 1, Infrastructure = 2, Agent = 3 }

/// <summary>
/// A STABLE entity id: the kind plus the key the SOURCE assigned (the authoritative id for a
/// live source, the content id for the demo). The view never mints ids, so an entity keeps
/// its id for as long as its source reports it.
/// </summary>
public readonly record struct WorldEntityId(WorldEntityKind Kind, string Key) : IComparable<WorldEntityId>
{
    public int CompareTo(WorldEntityId other)
    {
        int k = Kind.CompareTo(other.Kind);
        return k != 0 ? k : string.CompareOrdinal(Key, other.Key);
    }

    public override string ToString() => $"{Kind.ToString().ToLowerInvariant()}:{Key}";
}

/// <summary>A continuous world position (double X, double Y) — never a tile index.</summary>
public readonly record struct WorldPoint(double X, double Y);

/// <summary>Whether the viewer may see an entity, AS REPORTED. The renderer never computes
/// visibility; a source without a visibility model reports <see cref="Visible"/> and says so.</summary>
public enum ReportedVisibility { Visible = 0, Fogged = 1, Hidden = 2 }

/// <summary>A named number a source reports about a structure or infrastructure element
/// (capacity, maturity, staff, served, construction, …). Morphology content chooses which
/// one drives a visual stage, so a future authoritative input needs no renderer change.</summary>
public readonly record struct ReportedInput(string Name, double Value);

public sealed record SettlementReport(
    string Key, string DisplayName, string? PolityKey, WorldPoint Position, long? Population,
    ReportedVisibility Visibility, string Note);

/// <summary>A polity (owner / affiliation). Its map colour is a VISUAL choice made by the
/// morphology content from the polity's order, not something a source reports.</summary>
public sealed record PolityReport(string Key, string DisplayName, string Note);

/// <summary>
/// A building or institution. <see cref="Position"/> is an AUTHORITATIVE placement when the
/// source has one (null today: the simulation places no buildings, and the view then places
/// it deterministically — a visual choice, documented). <see cref="Established"/> is the
/// source's ordinal of when it came to exist (placement priority only). <see cref="Multiplicity"/>
/// lets a source report one row standing for several identical institutions.
/// </summary>
public sealed record StructureReport(
    string Key, string DisplayName, string VisualType, string SettlementKey, string? PolityKey,
    WorldPoint? Position, long Established, long Multiplicity, IReadOnlyList<ReportedInput> Inputs,
    string? Specialization, string? State, ReportedVisibility Visibility, string Note)
{
    public double? Input(string name)
    {
        foreach (ReportedInput i in Inputs) if (i.Name == name) return i.Value;
        return null;
    }
}

/// <summary>An infrastructure element: a network link (a polyline) or a local work (one point).</summary>
public sealed record InfrastructureReport(
    string Key, string DisplayName, string VisualType, string? SettlementKey, string? PolityKey,
    IReadOnlyList<WorldPoint> Path, IReadOnlyList<ReportedInput> Inputs, ReportedVisibility Visibility, string Note)
{
    public double? Input(string name)
    {
        foreach (ReportedInput i in Inputs) if (i.Name == name) return i.Value;
        return null;
    }
}

/// <summary>
/// A mobile entity — a formation, a fleet, a person, a group — as ONE map entity.
/// <see cref="Count"/> is the reported number it stands for (personnel, members); the map
/// never draws one token per person. Position is continuous; heading is optional display.
/// </summary>
public sealed record AgentReport(
    string Key, string DisplayName, string DisplayType, string? PolityKey, WorldPoint Position,
    double? HeadingDeg, long? Count, string? CountNoun, ReportedVisibility Visibility, string Note);

/// <summary>One source's report at one moment: immutable, sorted by key, stamped with a
/// sequence that changes whenever the reported state changes.</summary>
public sealed class Snapshot<T> where T : class
{
    public Snapshot(long sequence, string sourceLabel, bool isPlaceholder, IEnumerable<T> items, Func<T, string> key)
    {
        Sequence = sequence;
        SourceLabel = sourceLabel;
        IsPlaceholder = isPlaceholder;
        Items = items.OrderBy(key, StringComparer.Ordinal).ToArray();
        var seen = new HashSet<string>(StringComparer.Ordinal);
        foreach (T item in Items)
            if (!seen.Add(key(item))) throw new ArgumentException($"{sourceLabel}: duplicate key '{key(item)}'");
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
public interface IInfrastructureViewSource { Snapshot<InfrastructureReport> Current { get; } }
public interface IMobileAgentViewSource { Snapshot<AgentReport> Current { get; } }

/// <summary>
/// The five sources a scene reads — a bundle of references, not a manager: it owns nothing,
/// computes nothing and has no behaviour. <see cref="Label"/> names the bundle ("LIVE
/// simulation", "DEMO world") for the map's provenance banner.
/// </summary>
public sealed record WorldSources(
    string Label, bool IsPlaceholder,
    ISettlementViewSource Settlements, IPolityViewSource Polities, IStructureViewSource Structures,
    IInfrastructureViewSource Infrastructure, IMobileAgentViewSource Agents);

/// <summary>A source with nothing to report, and a label saying why (e.g. "no authoritative
/// producer: the simulation has no buildings yet").</summary>
public sealed class EmptySource :
    IStructureViewSource, IInfrastructureViewSource, IMobileAgentViewSource, ISettlementViewSource, IPolityViewSource
{
    private readonly string _label;
    public EmptySource(string label) => _label = label;
    Snapshot<StructureReport> IStructureViewSource.Current => Snapshot<StructureReport>.Empty(_label, false, r => r.Key);
    Snapshot<InfrastructureReport> IInfrastructureViewSource.Current => Snapshot<InfrastructureReport>.Empty(_label, false, r => r.Key);
    Snapshot<AgentReport> IMobileAgentViewSource.Current => Snapshot<AgentReport>.Empty(_label, false, r => r.Key);
    Snapshot<SettlementReport> ISettlementViewSource.Current => Snapshot<SettlementReport>.Empty(_label, false, r => r.Key);
    Snapshot<PolityReport> IPolityViewSource.Current => Snapshot<PolityReport>.Empty(_label, false, r => r.Key);
}
