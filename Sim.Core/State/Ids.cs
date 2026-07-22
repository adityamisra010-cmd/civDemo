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

    /// <summary>People (T1.5) — every person a conserved long from here to the space age.</summary>
    public static readonly ConservedQuantityId Population = new(3);

    /// <summary>Food (T1.5) — 1 unit = 1 person-year of sustenance (D-015 data constants).</summary>
    public static readonly ConservedQuantityId Food = new(4);
}

/// <summary>Well-known flow reasons (M0 toy registry).</summary>
public static class ReasonIds
{
    public static readonly ReasonId Growth = new(1);
    public static readonly ReasonId InitialEndowment = new(2);

    // T1.5 — the moral bookkeeping reasons. Aging is deliberately absent: band
    // aging moves people BETWEEN stocks via Ledger.Transfer, which conserves by
    // construction and records no source/sink row.
    public static readonly ReasonId Harvest = new(3);
    public static readonly ReasonId Eaten = new(4);
    public static readonly ReasonId Births = new(5);
    public static readonly ReasonId Deaths = new(6);
    public static readonly ReasonId Starvation = new(7);
}

/// <summary>Identifies a culture registry entry (T2.1, D-026/D-027 — one placeholder at M2).</summary>
public readonly record struct CultureId(int Value) : IComparable<CultureId>
{
    public int CompareTo(CultureId other) => Value.CompareTo(other.Value);
}

/// <summary>Identifies a religion registry entry (T2.1 — one placeholder at M2).</summary>
public readonly record struct ReligionId(int Value) : IComparable<ReligionId>
{
    public int CompareTo(ReligionId other) => Value.CompareTo(other.Value);
}

/// <summary>Identifies a social-class registry entry (T2.1, D-018/D-027 — Peasants + Artisans at M2).</summary>
public readonly record struct ClassId(int Value) : IComparable<ClassId>
{
    public int CompareTo(ClassId other) => Value.CompareTo(other.Value);
}

/// <summary>
/// The cohort structure (T2.1, D-026): 16 five-year age cohorts, 0–4 … 75+.
/// Cohort index is data, not a calendar gate — capability never derives from it.
/// The child/adult/elder BAND VIEWS (UI + labor) are derived sums over cohort
/// ranges chosen to match the retired M1 bands exactly (0–14 / 15–59 / 60+):
/// see <see cref="BandViews"/>. The last cohort (75+) is absorbing — people
/// leave it only by dying.
/// </summary>
public static class Cohorts
{
    public const int Count = 16;
    public const double WidthYears = 5.0;

    /// <summary>First cohort of the derived adult band (15–19).</summary>
    public const int FirstAdult = 3;

    /// <summary>First cohort of the derived elder band (60–64).</summary>
    public const int FirstElder = 12;
}
