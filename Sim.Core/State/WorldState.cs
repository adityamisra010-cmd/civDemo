namespace Sim.Core.State;

/// <summary>
/// Row of the region table. Regions are the spatial anchor every M0 system keys on
/// (weather/growth in T0.5, trade in T0.6). Real world-substrate state arrives with
/// M1 worldgen; at M0 a region is just its identity.
/// </summary>
public readonly record struct RegionRow(RegionId Id);

/// <summary>
/// Row of the rainfall table — owned by WeatherSystem (M0 toy). Rainfall is an
/// environmental state in mm/year, redrawn each turn from the system's RNG stream;
/// a rate-like double, not a conserved stock (law 7).
/// </summary>
public record struct RainfallRow(RegionId Region, double RainfallMmPerYear);

/// <summary>
/// Row of the biomass table — owned by GrowthSystem (M0 toy). Biomass is a `long`
/// stock; GrowthRemainder is the D-004 per-entity remainder accumulator (the
/// fractional part of integration carried between turns, deterministically — no
/// stochastic rounding). It lives in the row so it travels with Clone and
/// snapshots (systems are stateless).
/// </summary>
public record struct BiomassRow(RegionId Region, long Biomass, double GrowthRemainder);

/// <summary>
/// State of one PCG32 stream (kernel contract §3.5: stream states live inside
/// WorldState so save/load/replay preserve randomness exactly). Keyed by
/// (System, Region); State/Inc are the canonical PCG32 pair (Inc always odd).
/// Unmanaged row per ADR-001 — travels with the double-buffer clone for free.
/// </summary>
public record struct RngStreamRow(SystemId System, RegionId Region, ulong State, ulong Inc);

/// <summary>
/// Read-only view of the world (kernel contract §3.1). Systems read the previous
/// turn's state exclusively through this interface; it exposes only
/// <see cref="IReadOnlyTable{T}"/> views, so no mutation compiles. Writable access
/// exists solely via typed handles to a system's own tables, constructed by the
/// kernel (SimContext, lands in T0.5).
/// </summary>
public interface IReadOnlyWorldState
{
    ulong Seed { get; }
    Kernel.SimClock Clock { get; }
    IReadOnlyTable<RegionRow> Regions { get; }
    IReadOnlyTable<RngStreamRow> RngStreams { get; }
    IReadOnlyTable<RainfallRow> Rainfall { get; }
    IReadOnlyTable<BiomassRow> Biomass { get; }
}

/// <summary>
/// The single source of truth (kernel contract §3.1): plain data — a set of tables,
/// each owned by exactly one system. RNG stream states join in T0.3 and the clock in
/// T0.4; every added field MUST be included in <see cref="Clone"/> (the double-buffer
/// copy, §3.2) — the clone round-trip tests guard this.
/// </summary>
public sealed class WorldState : IReadOnlyWorldState
{
    /// <summary>World seed (D-007/D-008: root of all stream derivation and replay).</summary>
    public ulong Seed { get; }

    /// <summary>The clock (§3.4, ADR-002) — advanced by the kernel, once per turn.</summary>
    public Kernel.SimClock Clock { get; set; }

    public Table<RegionRow> Regions { get; }

    /// <summary>PCG32 stream states (§3.5) — owned by the kernel's RngRegistry.</summary>
    public Table<RngStreamRow> RngStreams { get; }

    /// <summary>Per-region rainfall — owned by WeatherSystem (M0 toy).</summary>
    public Table<RainfallRow> Rainfall { get; }

    /// <summary>Per-region biomass stock — owned by GrowthSystem (M0 toy).</summary>
    public Table<BiomassRow> Biomass { get; }

    IReadOnlyTable<RegionRow> IReadOnlyWorldState.Regions => Regions;
    IReadOnlyTable<RngStreamRow> IReadOnlyWorldState.RngStreams => RngStreams;
    IReadOnlyTable<RainfallRow> IReadOnlyWorldState.Rainfall => Rainfall;
    IReadOnlyTable<BiomassRow> IReadOnlyWorldState.Biomass => Biomass;

    public WorldState(ulong seed = 0UL)
    {
        Seed = seed;
        Regions = new Table<RegionRow>();
        RngStreams = new Table<RngStreamRow>();
        Rainfall = new Table<RainfallRow>();
        Biomass = new Table<BiomassRow>();
    }

    private WorldState(
        ulong seed, Kernel.SimClock clock, Table<RegionRow> regions,
        Table<RngStreamRow> rngStreams, Table<RainfallRow> rainfall, Table<BiomassRow> biomass)
    {
        Seed = seed;
        Clock = clock;
        Regions = regions;
        RngStreams = rngStreams;
        Rainfall = rainfall;
        Biomass = biomass;
    }

    /// <summary>
    /// Full deep copy — the §3.2 double-buffer step (Prev → Next). The copy shares no
    /// mutable state with the source: mutating one never affects the other. Every
    /// field of WorldState MUST be carried here; the clone round-trip tests guard this.
    /// </summary>
    public WorldState Clone() =>
        new(Seed, Clock, Regions.Clone(), RngStreams.Clone(), Rainfall.Clone(), Biomass.Clone());
}
