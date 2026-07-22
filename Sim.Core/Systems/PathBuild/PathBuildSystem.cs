using Sim.Core.Kernel;
using Sim.Core.Pathing;
using Sim.Core.State;
using Sim.Core.Worldgen;

namespace Sim.Core.Systems.PathBuild;

/// <summary>
/// Writable handles to PathBuildSystem's own tables (built by SystemCatalog
/// only). The network write ownership deferred since T1.3 lands HERE: nodes,
/// edges, and the revision counter (worldgen only initializes revision 0).
/// </summary>
public readonly record struct PathBuildTables(
    Table<LaborAllocationRow> Allocations, Table<PathProgressRow> Progress,
    Table<NetworkNodeRow> Nodes, Table<NetworkEdgeRow> Edges, Table<NetworkMetaRow> Meta);

/// <summary>
/// PathBuild (T1.6) — the player's hand touches the world. Two jobs, in order:
///
/// 1. ORDER CONSUMPTION: each LaborAllocationOrder in this turn's batch upserts
///    the target settlement's LaborAllocations row (FarmShare = Amount/100),
///    applied in log order — the last order for a settlement in a turn wins.
///    Farming and this system's own accrual read the row from PREV (§3.2), so
///    an order steers yields exactly one turn after it lands.
///
/// 2. THE BUILD LOOP: path labor accrues into the banked-progress row at
///    LaborPerAdultPerYear × pathShare × PREV adults × dtYears (law 3). When
///    the bank covers the next segment's build cost, the segment is laid: one
///    lattice step from the settlement's frontier along the A* route toward
///    the TARGET — the best catchment-unreached passable lattice node by the
///    composite key (block fertility DESC, node id ASC; constitution tie-break
///    rule, tie-dense test shipped). Laying a segment appends a NetworkEdgeRow
///    (dirt path; traversal cost = StepCost × DirtPathSpeedFactor — the fast
///    lane), creates anchor NetworkNodeRows as needed, decrements the bank, and
///    increments the network revision — which triggers the D-016 catchment
///    recompute NEXT turn (the T1.4 lag, now driven by gameplay). Multiple
///    segments may complete in one large-dt turn; steps along an already-built
///    own edge advance the frontier free of charge (the path never re-buys
///    itself when the route bends).
///
/// Build cost comes from the movement-cost field: StepCost × BuildCostMultiplier
/// (TUNE) — hard terrain is hard to build through. Water nodes are impassable
/// and never targets; candidates are restricted to the frontier's passable
/// component (the global best may sit across the sea — boats are a later
/// milestone, and an unreachable target must not stall the loop).
///
/// SATURATION (observed, accepted): once the frontier stands ON the best
/// still-unreached node, more edges cannot help — the catchment is then bound
/// by its travel BUDGET, not by the path (a dirt path halves travel cost, so
/// its reach tops out near 2× the walking radius). The loop idles there and
/// banked labor accrues unspent; later milestones spend it on better road
/// tiers. Banked labor is NOT conserved stock (a rate accumulator, like D-004
/// remainders). STATELESS: config is immutable tuning, not state.
/// </summary>
public sealed class PathBuildSystem(SimConfig cfg) : ISimSystem<PathBuildTables>
{
    public static readonly SystemId WellKnownId = new(8);
    public const string Name = "pathbuild";

    private readonly SimConfig _cfg = cfg;

    public SystemId Id => WellKnownId;

    public void Step(SimContext<PathBuildTables> ctx)
    {
        IReadOnlyWorldState prev = ctx.Prev;

        // 1. Orders — applied even before terrain checks: the allocation row is
        // plain state. Log order; unknown settlements were rejected up-front by
        // OrderValidation, so a miss here means a toy world (skip silently).
        for (int o = 0; o < ctx.Orders.Count; o++)
        {
            OrderRecord order = ctx.Orders[o];
            if (order.Kind != OrderKind.LaborAllocation) continue;
            if (!SettlementExists(prev, order.TargetId)) continue;
            Upsert(ctx.Owned.Allocations, new LaborAllocationRow(
                new SettlementId(order.TargetId), order.Amount / 100.0));
        }

        // 2. Build loop — needs terrain (toy worlds stop here).
        if (prev.Terrain is null || prev.Settlements.Count == 0 || ctx.Owned.Meta.Count == 0)
            return;

        TraversalLattice? lattice = null; // built lazily: most turns never need it
        for (int s = 0; s < prev.Settlements.Count; s++)
        {
            SettlementRow settlement = prev.Settlements[s];

            double farmShare = 1.0;
            for (int i = 0; i < prev.LaborAllocations.Count; i++)
            {
                if (prev.LaborAllocations[i].Settlement == settlement.Id)
                {
                    farmShare = prev.LaborAllocations[i].FarmShare;
                    break;
                }
            }
            double pathShare = 1.0 - farmShare;

            // T2.2: the slider still governs the whole workforce — the path
            // fraction of the pool is pathShare × (peasants + weight ×
            // artisans): artisans join PathBuild's pool at
            // ConstructionLaborWeight (TUNE), preserving the T1.6 invariant
            // that 100% farm banks exactly nothing. SCAFFOLDING (spec §1) —
            // M3's goods economy replaces this weighted-pool abstraction.
            var baseClass = new ClassId(_cfg.Registries.Classes[0].Id);
            long peasantAdults = Sim.Core.Systems.ClassMobility.ClassMobilitySystem
                .AdultsOfClass(prev.Buckets, settlement.Id, baseClass);
            long allAdults = BandViews.Adults(prev.Buckets, settlement.Id);
            double builders = pathShare * (peasantAdults
                              + _cfg.Mobility.ConstructionLaborWeight * (allAdults - peasantAdults));

            double accrual = _cfg.PathBuild.LaborPerAdultPerYear * builders * ctx.DtYears;
            int progressIdx = FindProgress(ctx.Owned.Progress, settlement.Id);
            if (progressIdx < 0)
            {
                if (accrual <= 0.0) continue; // nothing banked, nothing accruing
                progressIdx = ctx.Owned.Progress.Add(new PathProgressRow(
                    settlement.Id, Banked: 0.0, FrontierNode: -1));
            }

            ref PathProgressRow progress = ref ctx.Owned.Progress.Ref(progressIdx);
            progress.Banked += accrual;

            lattice ??= TraversalLattice.Build(prev.Terrain);
            if (progress.FrontierNode < 0)
                progress.FrontierNode = LatticeMap.OriginLatticeNode(
                    lattice, prev.Terrain.Size, settlement.SiteCell);

            BuildSegments(ctx, prev, lattice, settlement.Id, ref progress);
        }
    }

    private void BuildSegments(
        SimContext<PathBuildTables> ctx, IReadOnlyWorldState prev, TraversalLattice lattice,
        SettlementId settlement, ref PathProgressRow progress)
    {
        // Catchment-unreached set from PREV (stable within the turn — the
        // catchment itself only recomputes next turn, D-016).
        bool[] inCatchment = new bool[lattice.NodeCount];
        for (int i = 0; i < prev.CatchmentNodes.Count; i++)
        {
            if (prev.CatchmentNodes[i].Settlement == settlement)
                inCatchment[prev.CatchmentNodes[i].LatticeNode] = true;
        }

        // Candidates must share the frontier's passable component: the global
        // best may sit across the sea (unreachable until boats, later
        // milestones) and must not stall the build loop forever.
        bool[] reachable = PassableComponent(lattice, progress.FrontierNode);
        int target = ChooseTarget(lattice, prev.Terrain!, inCatchment, reachable);
        if (target < 0) return;

        // Safety bound: a frontier can advance at most once per lattice node.
        for (int guard = 0; guard < lattice.NodeCount; guard++)
        {
            if (progress.FrontierNode == target) return; // reached — catchment catches up next turn

            // Route over lattice + CURRENT Next network (this turn's own edges
            // included, so multi-segment turns extend the same chain).
            Pathfinder.PathResult route = Pathfinder.FindPath(
                lattice, NextNetworkView(ctx, prev), progress.FrontierNode, target);
            if (!route.Found || route.Nodes.Length < 2) return; // unreachable target: bank and wait

            int next = route.Nodes[1];

            // A step along an edge this settlement chain already built is free —
            // the path never re-buys itself when the route bends back.
            if (!EdgeExists(ctx.Owned.Edges, ctx.Owned.Nodes, progress.FrontierNode, next))
            {
                double segmentCost = lattice.StepCost(progress.FrontierNode, next)
                                     * _cfg.PathBuild.BuildCostMultiplier;
                if (progress.Banked < segmentCost) return; // keep banking

                int a = EnsureNode(ctx.Owned.Nodes, progress.FrontierNode);
                int b = EnsureNode(ctx.Owned.Nodes, next);
                ctx.Owned.Edges.Add(new NetworkEdgeRow(
                    new NetworkEdgeId(ctx.Owned.Edges.Count),
                    new NetworkNodeId(a), new NetworkNodeId(b), EdgeTypes.DirtPath,
                    Cost: lattice.StepCost(progress.FrontierNode, next)
                          * _cfg.PathBuild.DirtPathSpeedFactor));
                progress.Banked -= segmentCost;

                // The network changed: bump the revision (D-016 trigger).
                ref NetworkMetaRow meta = ref ctx.Owned.Meta.Ref(0);
                meta.Revision++;
            }

            progress.FrontierNode = next;
        }
    }

    /// <summary>
    /// The build target: best passable lattice node NOT in the settlement's
    /// catchment and inside the frontier's component, by the composite key
    /// (block fertility DESC, node id ASC) — an ascending-id scan with
    /// strictly-greater comparison implements exactly that total order
    /// (constitution rule; tie-dense test shipped). Returns −1 when every
    /// eligible node is already reached.
    /// Public and pure so the tie-dense test can drive it directly.
    /// </summary>
    public static int ChooseTarget(
        TraversalLattice lattice, TerrainSet terrain, bool[] excluded, bool[] eligible)
    {
        var nodeFertility = new double[lattice.NodeCount];
        for (int node = 0; node < lattice.NodeCount; node++)
            nodeFertility[node] = LatticeMap.BlockFertility(terrain, lattice, node);
        return ChooseTarget(lattice, nodeFertility, excluded, eligible);
    }

    /// <summary>Primitive overload (the tie-dense test surface): fertility per lattice node.</summary>
    public static int ChooseTarget(
        TraversalLattice lattice, ReadOnlySpan<double> nodeFertility, bool[] excluded, bool[] eligible)
    {
        int best = -1;
        double bestFertility = double.NegativeInfinity;
        for (int node = 0; node < lattice.NodeCount; node++)
        {
            if (excluded[node] || !eligible[node] || !lattice.IsPassable(node)) continue;
            // Strictly-greater on an ascending-id scan = (fertility DESC, id ASC).
            if (nodeFertility[node] > bestFertility)
            {
                bestFertility = nodeFertility[node];
                best = node;
            }
        }
        return best;
    }

    /// <summary>
    /// Passable 8-neighbor flood from <paramref name="origin"/> — fixed
    /// expansion order (ascending queue, W/E/N/S then diagonals), deterministic
    /// by construction. Pure; recomputed per build call (not state, not a cache).
    /// </summary>
    public static bool[] PassableComponent(TraversalLattice lattice, int origin)
    {
        var inComponent = new bool[lattice.NodeCount];
        if (!lattice.IsPassable(origin)) return inComponent;
        var queue = new int[lattice.NodeCount];
        int head = 0, tail = 0;
        inComponent[origin] = true;
        queue[tail++] = origin;
        Span<int> dx = [-1, 1, 0, 0, -1, 1, -1, 1];
        Span<int> dy = [0, 0, -1, 1, -1, -1, 1, 1];
        while (head < tail)
        {
            int node = queue[head++];
            (int x, int y) = lattice.Coords(node);
            for (int d = 0; d < 8; d++)
            {
                int nx = x + dx[d], ny = y + dy[d];
                if (nx < 0 || ny < 0 || nx >= lattice.Size || ny >= lattice.Size) continue;
                int nb = lattice.NodeId(nx, ny);
                if (inComponent[nb] || !lattice.IsPassable(nb)) continue;
                inComponent[nb] = true;
                queue[tail++] = nb;
            }
        }
        return inComponent;
    }

    // --- helpers ---------------------------------------------------------------

    /// <summary>
    /// Pathfinder reads the network through IReadOnlyWorldState; route over the
    /// NEXT network tables (this turn's appends included) with everything else
    /// from Prev. A minimal adapter, not a second state.
    /// </summary>
    private static IReadOnlyWorldState NextNetworkView(
        SimContext<PathBuildTables> ctx, IReadOnlyWorldState prev) =>
        new NetworkOverlayView(prev, ctx.Owned.Nodes, ctx.Owned.Edges);

    private sealed class NetworkOverlayView(
        IReadOnlyWorldState prev, Table<NetworkNodeRow> nodes, Table<NetworkEdgeRow> edges)
        : IReadOnlyWorldState
    {
        public ulong Seed => prev.Seed;
        public SimClock Clock => prev.Clock;
        public TerrainSet? Terrain => prev.Terrain;
        public IReadOnlyTable<RegionRow> Regions => prev.Regions;
        public IReadOnlyTable<RngStreamRow> RngStreams => prev.RngStreams;
        public IReadOnlyTable<RainfallRow> Rainfall => prev.Rainfall;
        public IReadOnlyTable<BiomassRow> Biomass => prev.Biomass;
        public IReadOnlyTable<GoodsRow> Goods => prev.Goods;
        public IReadOnlyTable<LedgerFlowRow> LedgerFlows => prev.LedgerFlows;
        public IReadOnlyTable<NetworkNodeRow> NetworkNodes => nodes;
        public IReadOnlyTable<NetworkEdgeRow> NetworkEdges => edges;
        public IReadOnlyTable<SettlementRow> Settlements => prev.Settlements;
        public IReadOnlyTable<NetworkMetaRow> NetworkMeta => prev.NetworkMeta;
        public IReadOnlyTable<CatchmentNodeRow> CatchmentNodes => prev.CatchmentNodes;
        public IReadOnlyTable<CatchmentSummaryRow> CatchmentSummaries => prev.CatchmentSummaries;
        public IReadOnlyTable<BucketRow> Buckets => prev.Buckets;
        public IReadOnlyTable<FoodStoreRow> FoodStores => prev.FoodStores;
        public IReadOnlyTable<ConsumptionDeficitRow> ConsumptionDeficits => prev.ConsumptionDeficits;
        public IReadOnlyTable<LaborAllocationRow> LaborAllocations => prev.LaborAllocations;
        public IReadOnlyTable<PathProgressRow> PathProgress => prev.PathProgress;
        public IReadOnlyTable<VariableRow> Variables => prev.Variables;
        public IReadOnlyTable<ClassStateRow> ClassStates => prev.ClassStates;
    }

    private static bool SettlementExists(IReadOnlyWorldState prev, int settlementId)
    {
        for (int i = 0; i < prev.Settlements.Count; i++)
            if (prev.Settlements[i].Id.Value == settlementId) return true;
        return false;
    }

    private static void Upsert(Table<LaborAllocationRow> allocations, LaborAllocationRow row)
    {
        for (int i = 0; i < allocations.Count; i++)
        {
            if (allocations[i].Settlement == row.Settlement) { allocations[i] = row; return; }
        }
        allocations.Add(row);
    }

    private static int FindProgress(Table<PathProgressRow> progress, SettlementId settlement)
    {
        for (int i = 0; i < progress.Count; i++)
            if (progress[i].Settlement == settlement) return i;
        return -1;
    }

    /// <summary>Network node anchored at the lattice node, created if absent; returns its id.</summary>
    private static int EnsureNode(Table<NetworkNodeRow> nodes, int latticeNode)
    {
        for (int i = 0; i < nodes.Count; i++)
            if (nodes[i].LatticeNode == latticeNode) return nodes[i].Id.Value;
        int id = nodes.Count;
        nodes.Add(new NetworkNodeRow(new NetworkNodeId(id), latticeNode));
        return id;
    }

    private static bool EdgeExists(
        Table<NetworkEdgeRow> edges, Table<NetworkNodeRow> nodes, int latticeA, int latticeB)
    {
        int idA = -1, idB = -1;
        for (int i = 0; i < nodes.Count; i++)
        {
            if (nodes[i].LatticeNode == latticeA) idA = nodes[i].Id.Value;
            if (nodes[i].LatticeNode == latticeB) idB = nodes[i].Id.Value;
        }
        if (idA < 0 || idB < 0) return false;
        for (int i = 0; i < edges.Count; i++)
        {
            NetworkEdgeRow e = edges[i];
            if ((e.A.Value == idA && e.B.Value == idB) || (e.A.Value == idB && e.B.Value == idA))
                return true;
        }
        return false;
    }
}
