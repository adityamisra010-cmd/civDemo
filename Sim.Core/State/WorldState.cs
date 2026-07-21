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
/// Row of the biomass table — owned by GrowthSystem (M0 toy). Biomass is a
/// conserved stock (law 1, mutated only via Ledger); GrowthRemainder is the D-004
/// per-entity remainder accumulator (the fractional part of integration carried
/// between turns, deterministically — no stochastic rounding). It lives in the row
/// so it travels with Clone and snapshots (systems are stateless).
/// </summary>
/// <remarks>Field-based (not a record struct) so Ledger can take `ref` to the stock.</remarks>
public struct BiomassRow(RegionId region, Conserved biomass, double growthRemainder) : IEquatable<BiomassRow>
{
    public RegionId Region = region;
    public Conserved Biomass = biomass;
    public double GrowthRemainder = growthRemainder;

    public readonly bool Equals(BiomassRow other) =>
        Region == other.Region && Biomass == other.Biomass && GrowthRemainder.Equals(other.GrowthRemainder);
    public override readonly bool Equals(object? obj) => obj is BiomassRow other && Equals(other);
    public override readonly int GetHashCode() => Region.Value; // gate:allow-gethashcode — equality plumbing, never logic input
}

/// <summary>Row of the toy-goods table — owned by TradeSystem (M0 toy). A conserved stock per region.</summary>
/// <remarks>Field-based (not a record struct) so Ledger can take `ref` to the stock.</remarks>
public struct GoodsRow(RegionId region, Conserved amount) : IEquatable<GoodsRow>
{
    public RegionId Region = region;
    public Conserved Amount = amount;

    public readonly bool Equals(GoodsRow other) => Region == other.Region && Amount == other.Amount;
    public override readonly bool Equals(object? obj) => obj is GoodsRow other && Equals(other);
    public override readonly int GetHashCode() => Region.Value; // gate:allow-gethashcode — equality plumbing, never logic input
}

/// <summary>
/// A node of the built transport network (T1.3), anchored at a traversal-lattice
/// node. Created empty by worldgen; WRITE ownership is assigned at T1.6
/// (PathBuildSystem) — until then no system owns these tables.
/// </summary>
public record struct NetworkNodeRow(NetworkNodeId Id, int LatticeNode);

/// <summary>
/// An edge of the built transport network (T1.3): a fast lane between two network
/// nodes' lattice anchors. Cost is the TOTAL traversal cost of the edge (in the
/// lattice's cost·px units), fixed when the edge is built.
/// </summary>
public record struct NetworkEdgeRow(NetworkEdgeId Id, NetworkNodeId A, NetworkNodeId B, int EdgeType, double Cost);

/// <summary>A settlement (T1.4): site is a TERRAIN cell index. The table never assumes one row.</summary>
public record struct SettlementRow(SettlementId Id, int SiteCell, long FoundedTurn);

/// <summary>
/// Network bookkeeping (T1.4): a single row holding the network revision counter.
/// Worldgen initializes it to 0; PathBuildSystem owns incrementing it from T1.6.
/// CatchmentSystem recomputes only when this revision moves (D-016).
/// </summary>
public record struct NetworkMetaRow(int Revision);

/// <summary>One lattice node inside a settlement's catchment (owned by CatchmentSystem).</summary>
public record struct CatchmentNodeRow(SettlementId Settlement, int LatticeNode, double TravelCost);

/// <summary>
/// Per-settlement catchment summary (owned by CatchmentSystem): node count,
/// effective farmland, the network revision it was computed against, and the turn
/// of the last recompute (the D-016 skip-proof observable).
/// </summary>
public record struct CatchmentSummaryRow(
    SettlementId Settlement, int NodeCount, double EffectiveFarmland,
    int NetworkRevision, long LastRecomputeTurn);

/// <summary>
/// Cumulative source/sink counterweights per (quantity, reason) — written only by
/// Ledger.Flow (§3.6). These rows make conservation exactly auditable:
/// Σ stocks + Σ TotalSunk − Σ TotalSourced = 0 per quantity, at every turn.
/// </summary>
public record struct LedgerFlowRow(ConservedQuantityId Quantity, ReasonId Reason, long TotalSourced, long TotalSunk);

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
    Worldgen.TerrainSet? Terrain { get; }
    IReadOnlyTable<RegionRow> Regions { get; }
    IReadOnlyTable<RngStreamRow> RngStreams { get; }
    IReadOnlyTable<RainfallRow> Rainfall { get; }
    IReadOnlyTable<BiomassRow> Biomass { get; }
    IReadOnlyTable<GoodsRow> Goods { get; }
    IReadOnlyTable<LedgerFlowRow> LedgerFlows { get; }
    IReadOnlyTable<NetworkNodeRow> NetworkNodes { get; }
    IReadOnlyTable<NetworkEdgeRow> NetworkEdges { get; }
    IReadOnlyTable<SettlementRow> Settlements { get; }
    IReadOnlyTable<NetworkMetaRow> NetworkMeta { get; }
    IReadOnlyTable<CatchmentNodeRow> CatchmentNodes { get; }
    IReadOnlyTable<CatchmentSummaryRow> CatchmentSummaries { get; }
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

    /// <summary>
    /// Immutable terrain rasters (ADR-008): set once after worldgen, shared by
    /// REFERENCE across Clone (immutable data needs no double buffer). The
    /// terrain content hash is folded into WorldHash by the canonical schema.
    /// </summary>
    public Worldgen.TerrainSet? Terrain { get; set; }

    public Table<RegionRow> Regions { get; }

    /// <summary>PCG32 stream states (§3.5) — owned by the kernel's RngRegistry.</summary>
    public Table<RngStreamRow> RngStreams { get; }

    /// <summary>Per-region rainfall — owned by WeatherSystem (M0 toy).</summary>
    public Table<RainfallRow> Rainfall { get; }

    /// <summary>Per-region biomass stock — owned by GrowthSystem (M0 toy).</summary>
    public Table<BiomassRow> Biomass { get; }

    /// <summary>Per-region toy-good stock — owned by TradeSystem (M0 toy).</summary>
    public Table<GoodsRow> Goods { get; }

    /// <summary>Source/sink counterweights — written only by Ledger (§3.6).</summary>
    public Table<LedgerFlowRow> LedgerFlows { get; }

    /// <summary>Transport-network nodes (T1.3) — write ownership assigned at T1.6.</summary>
    public Table<NetworkNodeRow> NetworkNodes { get; }

    /// <summary>Transport-network edges (T1.3) — write ownership assigned at T1.6.</summary>
    public Table<NetworkEdgeRow> NetworkEdges { get; }

    /// <summary>Settlements (T1.4) — created by worldgen founding; never assumes one row.</summary>
    public Table<SettlementRow> Settlements { get; }

    /// <summary>Network revision counter (T1.4/D-016) — initialized by worldgen, incremented by PathBuild (T1.6).</summary>
    public Table<NetworkMetaRow> NetworkMeta { get; }

    /// <summary>Catchment membership (T1.4) — owned by CatchmentSystem (derived state, D-016).</summary>
    public Table<CatchmentNodeRow> CatchmentNodes { get; }

    /// <summary>Catchment summaries (T1.4) — owned by CatchmentSystem (derived state, D-016).</summary>
    public Table<CatchmentSummaryRow> CatchmentSummaries { get; }

    IReadOnlyTable<RegionRow> IReadOnlyWorldState.Regions => Regions;
    IReadOnlyTable<RngStreamRow> IReadOnlyWorldState.RngStreams => RngStreams;
    IReadOnlyTable<RainfallRow> IReadOnlyWorldState.Rainfall => Rainfall;
    IReadOnlyTable<BiomassRow> IReadOnlyWorldState.Biomass => Biomass;
    IReadOnlyTable<GoodsRow> IReadOnlyWorldState.Goods => Goods;
    IReadOnlyTable<LedgerFlowRow> IReadOnlyWorldState.LedgerFlows => LedgerFlows;
    IReadOnlyTable<NetworkNodeRow> IReadOnlyWorldState.NetworkNodes => NetworkNodes;
    IReadOnlyTable<NetworkEdgeRow> IReadOnlyWorldState.NetworkEdges => NetworkEdges;
    IReadOnlyTable<SettlementRow> IReadOnlyWorldState.Settlements => Settlements;
    IReadOnlyTable<NetworkMetaRow> IReadOnlyWorldState.NetworkMeta => NetworkMeta;
    IReadOnlyTable<CatchmentNodeRow> IReadOnlyWorldState.CatchmentNodes => CatchmentNodes;
    IReadOnlyTable<CatchmentSummaryRow> IReadOnlyWorldState.CatchmentSummaries => CatchmentSummaries;

    public WorldState(ulong seed = 0UL)
    {
        Seed = seed;
        Regions = new Table<RegionRow>();
        RngStreams = new Table<RngStreamRow>();
        Rainfall = new Table<RainfallRow>();
        Biomass = new Table<BiomassRow>();
        Goods = new Table<GoodsRow>();
        LedgerFlows = new Table<LedgerFlowRow>();
        NetworkNodes = new Table<NetworkNodeRow>();
        NetworkEdges = new Table<NetworkEdgeRow>();
        Settlements = new Table<SettlementRow>();
        NetworkMeta = new Table<NetworkMetaRow>();
        CatchmentNodes = new Table<CatchmentNodeRow>();
        CatchmentSummaries = new Table<CatchmentSummaryRow>();
    }

    private WorldState(
        ulong seed, Kernel.SimClock clock, Table<RegionRow> regions, Table<RngStreamRow> rngStreams,
        Table<RainfallRow> rainfall, Table<BiomassRow> biomass, Table<GoodsRow> goods,
        Table<LedgerFlowRow> ledgerFlows, Table<NetworkNodeRow> networkNodes,
        Table<NetworkEdgeRow> networkEdges, Table<SettlementRow> settlements,
        Table<NetworkMetaRow> networkMeta, Table<CatchmentNodeRow> catchmentNodes,
        Table<CatchmentSummaryRow> catchmentSummaries)
    {
        Seed = seed;
        Clock = clock;
        Regions = regions;
        RngStreams = rngStreams;
        Rainfall = rainfall;
        Biomass = biomass;
        Goods = goods;
        LedgerFlows = ledgerFlows;
        NetworkNodes = networkNodes;
        NetworkEdges = networkEdges;
        Settlements = settlements;
        NetworkMeta = networkMeta;
        CatchmentNodes = catchmentNodes;
        CatchmentSummaries = catchmentSummaries;
    }

    /// <summary>
    /// Full deep copy — the §3.2 double-buffer step (Prev → Next). The copy shares no
    /// mutable state with the source: mutating one never affects the other. Every
    /// field of WorldState MUST be carried here; the clone round-trip tests guard this.
    /// </summary>
    public WorldState Clone() =>
        new(Seed, Clock, Regions.Clone(), RngStreams.Clone(), Rainfall.Clone(), Biomass.Clone(),
            Goods.Clone(), LedgerFlows.Clone(), NetworkNodes.Clone(), NetworkEdges.Clone(),
            Settlements.Clone(), NetworkMeta.Clone(), CatchmentNodes.Clone(),
            CatchmentSummaries.Clone())
        {
            Terrain = Terrain, // ADR-008: immutable — reference shared, never copied
        };
}
