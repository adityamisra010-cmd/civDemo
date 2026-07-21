namespace Sim.Core.State;

/// <summary>
/// Typed identifier structs (m0-kernel-spec §4 T0.2). Wrapping the raw int prevents
/// cross-domain id mixups at compile time (a SystemId cannot be passed where a
/// RegionId is expected). All ids are stable array indices assigned by the owning
/// table — never hashes (law 5: GetHashCode is not logic input).
/// IComparable is provided so ids can serve as deterministic sort keys.
/// </summary>
public readonly record struct SystemId(int Value) : IComparable<SystemId>
{
    public int CompareTo(SystemId other) => Value.CompareTo(other.Value);
}

public readonly record struct RegionId(int Value) : IComparable<RegionId>
{
    public int CompareTo(RegionId other) => Value.CompareTo(other.Value);
}

/// <summary>Identifies a conserved quantity (law 1): biomass, the toy good, later people/money/goods.</summary>
public readonly record struct ConservedQuantityId(int Value) : IComparable<ConservedQuantityId>
{
    public int CompareTo(ConservedQuantityId other) => Value.CompareTo(other.Value);
}

/// <summary>Identifies the cause of a source/sink flow (growth, initial endowment, later births/deaths/mint/burn).</summary>
public readonly record struct ReasonId(int Value) : IComparable<ReasonId>
{
    public int CompareTo(ReasonId other) => Value.CompareTo(other.Value);
}

/// <summary>Identifies a node of the built transport network (T1.3; anchored to a lattice node).</summary>
public readonly record struct NetworkNodeId(int Value) : IComparable<NetworkNodeId>
{
    public int CompareTo(NetworkNodeId other) => Value.CompareTo(other.Value);
}

/// <summary>Identifies an edge of the built transport network (T1.3).</summary>
public readonly record struct NetworkEdgeId(int Value) : IComparable<NetworkEdgeId>
{
    public int CompareTo(NetworkEdgeId other) => Value.CompareTo(other.Value);
}

/// <summary>Identifies a settlement (T1.4).</summary>
public readonly record struct SettlementId(int Value) : IComparable<SettlementId>
{
    public int CompareTo(SettlementId other) => Value.CompareTo(other.Value);
}

/// <summary>Network edge types (D-009: path → road → highway …; M1 ships dirt path only).</summary>
public static class EdgeTypes
{
    public const int DirtPath = 1;
}

/// <summary>Well-known conserved quantities (M0 toy registry — real registry is data, later milestone).</summary>
public static class ConservedQuantityIds
{
    public static readonly ConservedQuantityId Biomass = new(1);
    public static readonly ConservedQuantityId ToyGood = new(2);
}

/// <summary>Well-known flow reasons (M0 toy registry).</summary>
public static class ReasonIds
{
    public static readonly ReasonId Growth = new(1);
    public static readonly ReasonId InitialEndowment = new(2);
}
