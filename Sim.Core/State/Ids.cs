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
