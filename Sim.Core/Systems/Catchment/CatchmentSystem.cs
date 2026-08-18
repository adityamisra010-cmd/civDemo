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
/// Catchment maintenance (T1.4, D-016): each settlement's catchment is its
/// ECONOMIC HINTERLAND — the travel-cost isochrone around its site on the
/// traversal lattice + network fast lanes, with the budget derived from the
/// TUNE radius `catchment.hinterlandRadiusKm` (T3.2b: the budget moved out of
/// a code constant into tuning data, and is stated in km rather than in the
/// pathfinder's cost units so that it is a quantity a director can reason
/// about). Catchments are DERIVED state held in
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
public sealed class CatchmentSystem(SimConfig cfg) : ISimSystem<CatchmentTables>
{
    public static readonly SystemId WellKnownId = new(4);
    public const string Name = "catchment";

    private readonly SimConfig _cfg = cfg;

    public SystemId Id => WellKnownId;

    /// <summary>
    /// The pathfinder's cost budget for a given lattice — the TUNE hinterland
    /// radius (km, ideal ground) through the single conversion in
    /// <see cref="LatticeGeometry"/>. Public and pure so tests and the UI can
    /// reproduce the isochrone exactly without duplicating the conversion.
    /// </summary>
    public static double TravelBudgetCostUnits(SimConfig cfg, TraversalLattice lattice) =>
        LatticeGeometry.CostUnitsForIdealGroundKm(lattice, cfg.Catchment.HinterlandRadiusKm);

    /// <summary>T3.8: the QUANTIZED size step (0..4) — dwellings over
    /// sizeDwellingsRef, bucketed into fifths and saturating at 4. Public and
    /// pure so the staleness check, the budget, and the tests share one
    /// definition.</summary>
    public static int SizeTier(long dwellings, double dwellingsRef)
    {
        if (dwellings <= 0) return 0;
        double ratio = dwellings / dwellingsRef;
        if (ratio >= 1.0) return 4;
        return (int)(ratio * 4.0); // 0..3 below the reference scale
    }

    /// <summary>T3.8: a settlement's travel budget with its size bonus —
    /// base × (1 + sizeBonusMaxRatio × tier/4). A coefficient inside the
    /// budget equation (law 2), never a free-floating buff.</summary>
    public static double TierBudget(SimConfig cfg, TraversalLattice lattice, int tier) =>
        TravelBudgetCostUnits(cfg, lattice) * (1.0 + cfg.Catchment.SizeBonusMaxRatio * tier / 4.0);

    private static int TierOf(IReadOnlyWorldState prev, SimConfig cfg, SettlementId settlement)
    {
        for (int i = 0; i < prev.Housing.Count; i++)
            if (prev.Housing[i].Settlement == settlement)
                return SizeTier(prev.Housing[i].Dwellings.Value, cfg.Catchment.SizeDwellingsRef);
        return 0;
    }

    public void Step(SimContext<CatchmentTables> ctx)
    {
        IReadOnlyWorldState prev = ctx.Prev;

        // Terrain-less worlds (M0 harness, CLI toys) have no catchments — and no
        // stale check either: with no settlements the empty tables are correct.
        if (prev.Terrain is null || prev.Settlements.Count == 0 || prev.NetworkMeta.Count == 0)
            return;

        int revision = prev.NetworkMeta[0].Revision;
        if (!IsStale(prev, revision)) return; // D-016: no event → no recompute

        TraversalLattice lattice = TraversalLattice.Build(prev.Terrain, _cfg.Transport.RiverCostFactor);
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
        Span<double> budgets = prev.Settlements.Count <= 64
            ? stackalloc double[prev.Settlements.Count] : new double[prev.Settlements.Count];
        Span<int> tiers = prev.Settlements.Count <= 64
            ? stackalloc int[prev.Settlements.Count] : new int[prev.Settlements.Count];
        for (int s = 0; s < prev.Settlements.Count; s++)
        {
            origins[s] = OriginLatticeNode(lattice, prev.Terrain.Size, prev.Settlements[s].SiteCell);
            tiers[s] = TierOf(prev, _cfg, prev.Settlements[s].Id);
            budgets[s] = TierBudget(_cfg, lattice, tiers[s]); // T3.8 size bonus, per settlement
        }
        Pathfinder.PartitionResult part = Pathfinder.Partition(lattice, prev, origins, budgets);

        // Per settlement (ascending settlement order), rows in ascending node
        // id. SUMMATION ORDER IS DETERMINISM SURFACE: double addition is not
        // associative, so any reordering is a hash-visible behavior change.
        for (int s = 0; s < prev.Settlements.Count; s++)
        {
            SettlementRow settlement = prev.Settlements[s];
            // T3.2b: accumulated in FERTILITY-WEIGHTED km², not in fertility-
            // weighted nodes — the CR-002 denomination fix. The conversion is
            // per-node and inside LatticeMap, so the summary row leaves this
            // system already denominated and no consumer converts again.
            double arableKm2 = 0.0;
            int count = 0;
            for (int node = 0; node < part.Owner.Length; node++)
            {
                if (part.Owner[node] != s) continue;
                nodes.Add(new CatchmentNodeRow(settlement.Id, node, part.Cost[node]));
                arableKm2 += BlockArableKm2(prev.Terrain, lattice, node);
                count++;
            }
            summaries.Add(new CatchmentSummaryRow(
                settlement.Id, count, arableKm2, revision,
                LastRecomputeTurn: prev.Clock.Turn, SizeTier: tiers[s]));
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
    private bool IsStale(IReadOnlyWorldState prev, int revision)
    {
        if (prev.CatchmentSummaries.Count != prev.Settlements.Count) return true;
        for (int i = 0; i < prev.CatchmentSummaries.Count; i++)
        {
            CatchmentSummaryRow summary = prev.CatchmentSummaries[i];
            if (summary.Settlement != prev.Settlements[i].Id) return true;
            if (summary.NetworkRevision != revision) return true;
            // T3.8: a size-tier change is a recompute event (D-016 gate) —
            // the summary records the tier it was computed at.
            if (summary.SizeTier != TierOf(prev, _cfg, summary.Settlement)) return true;
        }
        return false;
    }

    // Terrain↔lattice mapping moved to Pathing.LatticeMap at T1.6 (PathBuild
    // shares it; law 6 forbids a system-to-system reference). Thin delegates
    // keep the T1.4 public test surface stable.

    /// <inheritdoc cref="LatticeMap.OriginLatticeNode"/>
    public static int OriginLatticeNode(TraversalLattice lattice, int terrainSize, int siteCell) =>
        LatticeMap.OriginLatticeNode(lattice, terrainSize, siteCell);

    /// <inheritdoc cref="LatticeMap.BlockMeanFertility"/>
    public static double BlockMeanFertility(TerrainSet terrain, TraversalLattice lattice, int node) =>
        LatticeMap.BlockMeanFertility(terrain, lattice, node);

    /// <inheritdoc cref="LatticeMap.BlockArableKm2"/>
    public static double BlockArableKm2(TerrainSet terrain, TraversalLattice lattice, int node) =>
        LatticeMap.BlockArableKm2(terrain, lattice, node);
}
