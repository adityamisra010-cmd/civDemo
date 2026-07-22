using Sim.Core.Kernel;
using Sim.Core.Pathing;
using Sim.Core.State;
using Sim.Core.Worldgen;

namespace Sim.Core.Systems.Catchment;

/// <summary>Writable handles to CatchmentSystem's own tables (built by SystemCatalog only).</summary>
public readonly record struct CatchmentTables(
    Table<CatchmentNodeRow> Nodes, Table<CatchmentSummaryRow> Summaries,
    Table<SettlementDistanceRow> Distances);

/// <summary>
/// Catchment maintenance (T1.4, D-016): each settlement's catchment is the
/// travel-time isochrone (budget = TUNE TravelBudget) around its site on the
/// traversal lattice + network fast lanes. Catchments are DERIVED state held in
/// this system's owned tables — recomputed ONLY when they are stale: the
/// settlement set changed, or the network revision counter moved (worldgen
/// initializes it; PathBuild increments it from T1.6). Every other turn is a
/// no-op: the double-buffer clone carries last turn's rows forward untouched,
/// and CatchmentSummaryRow.LastRecomputeTurn is the observable proof of the
/// skip (it moves only on recompute).
///
/// ONE-TURN LAG (accepted, D-016): systems read Prev, so a revision bumped
/// during turn T is seen here at turn T+1 — catchments trail network changes
/// by exactly one turn. This is the kernel's standard coupling (§3.1), not a
/// bug; the D-016 end-to-end test pins the T+1 recompute.
///
/// First in the pipeline: downstream systems (population/food at T1.5) read
/// catchments computed this turn... from Prev, on the same one-turn-lag terms.
/// STATELESS: no instance fields; the lattice is rebuilt per recompute (it is
/// a pure function of immutable terrain — not state, not a cache; T1.3 mandate).
/// </summary>
public sealed class CatchmentSystem : ISimSystem<CatchmentTables>
{
    public static readonly SystemId WellKnownId = new(4);
    public const string Name = "catchment";

    /// <summary>
    /// TUNE: catchment travel budget, in lattice cost units (node cost ≈ 1 on
    /// flat land × stride units walked) — how far a settlement works its land.
    /// </summary>
    public const double TravelBudget = 15.0;

    public SystemId Id => WellKnownId;

    public void Step(SimContext<CatchmentTables> ctx)
    {
        IReadOnlyWorldState prev = ctx.Prev;

        // Terrain-less worlds (M0 harness, CLI toys) have no catchments — and no
        // stale check either: with no settlements the empty tables are correct.
        if (prev.Terrain is null || prev.Settlements.Count == 0 || prev.NetworkMeta.Count == 0)
            return;

        int revision = prev.NetworkMeta[0].Revision;
        if (!IsStale(prev, revision)) return; // D-016: no event → no recompute

        TraversalLattice lattice = TraversalLattice.Build(prev.Terrain);
        Table<CatchmentNodeRow> nodes = ctx.Owned.Nodes;
        Table<CatchmentSummaryRow> summaries = ctx.Owned.Summaries;
        nodes.Clear();      // derived tables: rebuilt wholesale on recompute
        summaries.Clear();

        // T2.3 (m2 spec §3): catchments PARTITION, never overlap — one
        // multi-source Dijkstra, every settlement a source, each node claimed
        // by the composite (travel cost, settlement id). One owner cell per
        // node ⇒ zero double-claims by construction; per-settlement farmland
        // sums only owned nodes, so no land is ever double-counted.
        Span<int> origins = prev.Settlements.Count <= 64
            ? stackalloc int[prev.Settlements.Count] : new int[prev.Settlements.Count];
        for (int s = 0; s < prev.Settlements.Count; s++)
            origins[s] = OriginLatticeNode(lattice, prev.Terrain.Size, prev.Settlements[s].SiteCell);
        Pathfinder.PartitionResult part = Pathfinder.Partition(lattice, prev, origins, TravelBudget);

        // Per settlement (ascending settlement order), rows in ascending node
        // id. SUMMATION ORDER IS DETERMINISM SURFACE: double addition is not
        // associative, so any reordering is a hash-visible behavior change.
        for (int s = 0; s < prev.Settlements.Count; s++)
        {
            SettlementRow settlement = prev.Settlements[s];
            double farmland = 0.0;
            int count = 0;
            for (int node = 0; node < part.Owner.Length; node++)
            {
                if (part.Owner[node] != s) continue;
                nodes.Add(new CatchmentNodeRow(settlement.Id, node, part.Cost[node]));
                farmland += BlockFertility(prev.Terrain, lattice, node);
                count++;
            }
            summaries.Add(new CatchmentSummaryRow(
                settlement.Id, count, farmland, revision,
                LastRecomputeTurn: prev.Clock.Turn));
        }

        // T2.5 (D-016 piggyback): pairwise settlement travel costs, recomputed
        // on exactly the same events as the catchments themselves. One FULL
        // (uncapped) Dijkstra per settlement over lattice + network fast
        // lanes; an unreached destination stores PositiveInfinity — the
        // by-construction zero-flow sentinel migration relies on. Rows in
        // ascending (From, To) order, From ≠ To.
        Table<SettlementDistanceRow> distances = ctx.Owned.Distances;
        distances.Clear();
        var costs = new double[prev.Settlements.Count][];
        for (int s = 0; s < prev.Settlements.Count; s++)
        {
            Pathfinder.IsochroneResult full = Pathfinder.Isochrone(
                lattice, prev, origins[s], double.MaxValue);
            var field = new double[lattice.NodeCount];
            Array.Fill(field, double.PositiveInfinity);
            for (int i = 0; i < full.Reached.Length; i++) field[full.Reached[i]] = full.Costs[i];
            costs[s] = field;
        }
        for (int from = 0; from < prev.Settlements.Count; from++)
        {
            for (int to = 0; to < prev.Settlements.Count; to++)
            {
                if (from == to) continue;
                distances.Add(new SettlementDistanceRow(
                    prev.Settlements[from].Id, prev.Settlements[to].Id, costs[from][origins[to]]));
            }
        }
    }

    /// <summary>
    /// Stale iff the summaries don't cover the settlement set 1:1 in order, or
    /// any summary was computed against a different network revision.
    /// </summary>
    private static bool IsStale(IReadOnlyWorldState prev, int revision)
    {
        if (prev.CatchmentSummaries.Count != prev.Settlements.Count) return true;
        for (int i = 0; i < prev.CatchmentSummaries.Count; i++)
        {
            CatchmentSummaryRow summary = prev.CatchmentSummaries[i];
            if (summary.Settlement != prev.Settlements[i].Id) return true;
            if (summary.NetworkRevision != revision) return true;
        }
        return false;
    }

    // Terrain↔lattice mapping moved to Pathing.LatticeMap at T1.6 (PathBuild
    // shares it; law 6 forbids a system-to-system reference). Thin delegates
    // keep the T1.4 public test surface stable.

    /// <inheritdoc cref="LatticeMap.OriginLatticeNode"/>
    public static int OriginLatticeNode(TraversalLattice lattice, int terrainSize, int siteCell) =>
        LatticeMap.OriginLatticeNode(lattice, terrainSize, siteCell);

    /// <inheritdoc cref="LatticeMap.BlockFertility"/>
    public static double BlockFertility(TerrainSet terrain, TraversalLattice lattice, int node) =>
        LatticeMap.BlockFertility(terrain, lattice, node);
}
