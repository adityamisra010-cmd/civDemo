using Sim.Core.Kernel;
using Sim.Core.Pathing;
using Sim.Core.State;
using Sim.Core.Worldgen;

namespace Sim.Core.Systems.Colonization;

/// <summary>
/// M5 S1c (ADR-036 §8; D-051 §5: "The AI uses the same order and the same predicates") — THE PROVISIONAL AI
/// FOUNDING POLICY (the RoadDevelopmentPolicy / AiConstructionPolicy shape). It is NOT a system and writes no state:
/// it returns a FoundSettlement ORDER built by <see cref="FoundingQuery.FoundOrder"/> — the constructor the player's
/// order uses — for a site <see cref="FoundingQuery.Check(IReadOnlyWorldState, SimConfig, SettlementSiting.FrontierSiting, ReadOnlySpan{double}, ReadOnlySpan{int}, PolityId, SettlementId, int)"/>
/// answers Legal for, which ColonizationSystem applies to the very same order on the very same world. There is no
/// AI-only legality, no AI-only verb and no AI-only write path: the session appends the order to the log, the system
/// founds it (or refuses it) exactly as it does the player's, and a replay of the log reproduces without this class.
///
/// THE RULE, stated so nothing about it is hidden. For an AI-commanded polity, all of:
/// <list type="number">
/// <item><b>The switch</b>: sim.json <c>colonization.aiFounding.enabled</c> (absent section = inert).</item>
/// <item><b>The interval</b> (TUNE <c>minYearsBetweenFoundings</c>, 350 years): the polity's NEWEST settlement — the
///   largest <see cref="SettlementRow.FoundedTurn"/> among those it controls, by any path — was founded at least that
///   many years ago. Measured as a LOWER bound, (turns elapsed) × (the current turn length): the era table only ever
///   shortens turns, so the gate never opens early. At most one new settlement per polity per interval.</item>
/// <item><b>The source</b>: a settlement the polity controls, with at least <c>minSourcePopulation</c> people (TUNE,
///   500: the turn-zero founding size ÷ (1 − the party fraction)), FED — <see cref="FoodState"/> Normal or Stress,
///   nobody starving — and for which FoundingQuery's own people and food arms hold
///   (<see cref="FoundingQuery.Departure"/> on the world's tables). Sources are tried in the composite key
///   (population DESC, settlement id ASC) — integers only.</item>
/// <item><b>The site</b>: the argmax of the UNJITTERED siting score (<see cref="SettlementSiting.CandidateScores"/>,
///   the frontier's raster) by the composite key (score DESC, cell ASC) over the cells
///   <see cref="FoundingQuery.Check(IReadOnlyWorldState, SimConfig, SettlementSiting.FrontierSiting, ReadOnlySpan{double}, ReadOnlySpan{int}, PolityId, SettlementId, int)"/>
///   answers Legal for from that source (<see cref="BestSiteFor(IReadOnlyWorldState, SimConfig, SettlementSiting.FrontierSiting, TerrainSet, Sites, PolityId, SettlementId, SiteCheck)"/>).
///   A PRE-FILTER mirrors four of the predicate's per-cell arms — frontier candidate (on the map, land, score &gt; 0
///   and not below the floor), unobstructed (not a standing site, outside every spacing exclusion), unclaimed (origin
///   node in no catchment) and reachable (origin node in the source's connected component on the road-aware lattice:
///   one uncapped Dijkstra flood with <see cref="Pathfinder.Isochrone"/>, the same edge set and relaxation
///   <see cref="Pathfinder.FindPath"/> expands, so a node is flooded iff FindPath finds it). Each is a NECESSARY
///   condition of Legal on the same inputs, so the pre-filter only ever skips cells the predicate would refuse; it
///   spares the route search and decides nothing. THE PREDICATE DECIDES: the pre-filter's argmax is put to it, and
///   when it answers Legal that cell is the argmax over the Legal cells too (a subset's member that tops the superset
///   tops the subset). When it REFUSES — an arm the pre-filter does not mirror binds — the scan is repeated asking the
///   predicate of every cell it would take, so the SAME source falls through to its NEXT-BEST Legal cell; the next
///   source is tried only when no cell of this one is Legal. The AI cannot emit an order the predicate refuses, and
///   any arm the predicate gains narrows the pick with no change here (pinned: AiFoundingPolicyTests, an emulated
///   known-extent arm). An arm the predicate LOOSENS must be loosened in the pre-filter too, or the AI skips cells it
///   may take (the oracle and bind tests catch it).</item>
/// </list>
/// The first source (in key order) with a legal site founds; one order per polity per turn at most.
///
/// THE SAME STEP, SEVERAL POLITIES. Orders are judged at delivery on PREV with the step's occupancy and spacing grown
/// by every earlier founding of the batch (ColonizationSystem.FoundOrdered). <see cref="Sites"/> carries that state
/// across the polities of ONE producer call (roster order), growing it with each order issued exactly as the system
/// grows it with each founding — so a later AI polity never picks a site an earlier one's colony will exclude. Player
/// orders that precede the AI's in the batch are not visible to the AI (it does not read the player's orders); such an
/// order may make an AI order TooClose at delivery, and then the system refuses it like any other.
///
/// KNOWN INFORMATION-ACCESS LEAK (ADR-036 §8): the AI ranks every cell of the map. No known-extent state exists until
/// the fog-of-war packets (S2a/S2b, awaiting the Director's fog ruling); FoundingQuery's known-extent arm is inert, so
/// the predicate admits what the AI reads. When the arm lands IN FoundingQuery, the AI's PICK narrows to the known
/// extent with no change here, because a refused pick falls through to the same source's next-best Legal cell (pinned
/// with an emulated arm).
///
/// P7 (the information class, Director: "the AI cannot use information it has not legitimately discovered"): THE SITE
/// SCAN goes through the one knowledge seam, <see cref="PolityKnowledge"/>. Its first pre-filter arm is
/// <see cref="PolityKnowledge.KnowsCell"/> for the issuer, so a cell outside the issuer's known extent is never admitted
/// and never raises the ranking's bar: an UNKNOWN cell is never picked, whatever it scores (pinned by metamorphic tests
/// with a knowledge stub: raising every unknown cell's own score leaves the pick unchanged, and whatever the terrain
/// under unknown nodes the pick is the oracle's best KNOWN legal cell). The pick is NOT a function of known terrain only:
/// a known cell's VALUATION is the frontier raster's (<see cref="SettlementSiting.CandidateScores"/>), and that raster
/// integrates terrain under unknown nodes — region fertility is a box sum over ±fertilityRadiusPx (3 px, so it reaches
/// the neighbouring 4-px nodes), water access is a BFS over the whole map (cutoff 24 px), and the candidate floor
/// (<see cref="SettlementSiting.ScoreFloor"/>) is the median (scoreFloorPercentile 0.5) of every positive-scoring cell. Terrain the issuer has not discovered
/// therefore moves known cells' scores, the floor, and the pick between KNOWN cells (MEASURED, P7 review: hiding only
/// the eight nodes around the dev rig's pick and zeroing their fertility moves the pick 36463 → 42559, both known).
/// Which valuation a polity may hold of a known cell whose neighbourhood it has not discovered is S2b's (ADR-036 §8
/// item (h), ADR-037 §6); the channel is quarantined, not hidden (PolityKnowledgeTests). <see cref="PolityKnowledge.Of"/>
/// is all-known until S2a stores an extent, so today the arm admits every cell and the order is byte-identical. What else
/// the seam does not narrow, recorded rather than hidden: the reachability flood still crosses the issuer's whole
/// connected component (a geography graph, not a ranking — whether travel through unknown land is admissible is S2b's),
/// and the standing-site occupancy, spacing and claimed masks are the predicate's own rule arms (mirrored so the
/// pre-filter never skips a Legal cell), read over every settlement as FoundingQuery reads them.
///
/// Deterministic: roster, settlement-table and cell order; composite keys with integer tie-breaks (tie-dense test:
/// AiFoundingPolicyTests); no RNG, no clock, no dictionary, no LINQ (law 5).
/// </summary>
public static class AiFoundingPolicy
{
    /// <summary>
    /// THE PREDICATE as the policy asks it — the shape of FoundingQuery.Check's explicit overload (the batch's standing
    /// sites as spans). Production passes <see cref="FoundingQuery.Check(IReadOnlyWorldState, SimConfig, SettlementSiting.FrontierSiting, ReadOnlySpan{double}, ReadOnlySpan{int}, PolityId, SettlementId, int)"/>
    /// and nothing else (<see cref="Decide(IReadOnlyWorldState, SimConfig, PolityId, Sites)"/>); the parameter is the
    /// seam a test uses to give the predicate an arm the pre-filter does not mirror, proving the scan defers to the
    /// predicate rather than to its own copy of today's arms.
    /// </summary>
    public delegate FoundingVerdict SiteCheck(
        IReadOnlyWorldState world, SimConfig cfg, SettlementSiting.FrontierSiting frontier,
        ReadOnlySpan<double> spacing, ReadOnlySpan<int> occupied,
        PolityId issuer, SettlementId source, int siteCell);

    /// <summary>
    /// The batch's standing sites, shared by every polity of one <see cref="AiOrders.For"/> call: the frontier
    /// (the same <see cref="SettlementSiting.PrepareFrontier"/> ColonizationSystem builds), the occupied site cells and
    /// the spacing exclusion seeded from the world's settlements and grown by every founding order already issued,
    /// and the claimed-node mask. Built lazily — a turn on which no polity passes the cheap trigger arms never pays
    /// for the frontier.
    /// </summary>
    public sealed class Sites
    {
        private readonly IReadOnlyWorldState _world;
        private readonly Func<SettlementSiting.FrontierSiting?> _frontierOf;
        private bool _built;
        private SettlementSiting.FrontierSiting? _frontier;
        private int[] _occupied = [];
        private double[] _spacing = [];
        private bool[] _claimed = [];

        /// <summary>The batch over <paramref name="world"/>, whose frontier is FoundingQuery.Frontier(world, cfg,
        /// worldgen) — the same PrepareFrontier ColonizationSystem builds from the pipeline's worldgen config.</summary>
        public Sites(IReadOnlyWorldState world, SimConfig cfg, WorldgenConfig worldgen)
        {
            _world = world;
            _frontierOf = () => FoundingQuery.Frontier(world, cfg, worldgen);
        }

        /// <summary>The batch over <paramref name="world"/> with a given <paramref name="frontier"/> (a caller that
        /// already holds one; the ranking tests' flattened score raster).</summary>
        public Sites(IReadOnlyWorldState world, SettlementSiting.FrontierSiting? frontier)
        {
            _world = world;
            _frontierOf = () => frontier;
        }

        /// <summary>The frontier, or null when the world has no terrain (toy and hand-built worlds).</summary>
        public SettlementSiting.FrontierSiting? Frontier { get { Build(); return _frontier; } }

        /// <summary>The site cells standing for the coming step's founding orders: the world's settlements, then each
        /// site this batch has already ordered, in order.</summary>
        public ReadOnlySpan<int> Occupied { get { Build(); return _occupied; } }

        /// <summary>The spacing exclusion over lattice nodes for those sites.</summary>
        public ReadOnlySpan<double> Spacing { get { Build(); return _spacing; } }

        /// <summary>Whether a lattice node lies in a settlement's catchment in the judged world (ADR-036 "claimed").</summary>
        public bool IsClaimed(int latticeNode) { Build(); return (uint)latticeNode < (uint)_claimed.Length && _claimed[latticeNode]; }

        /// <summary>Records an issued founding: the site is occupied and excludes its neighbourhood for every later
        /// polity of the batch — exactly the system's growth after an accepted founding.</summary>
        public void Accept(int siteCell)
        {
            Build();
            if (_frontier is null) return;
            SettlementSiting.AcceptFrontierSite(_frontier, _spacing, siteCell);
            var grown = new int[_occupied.Length + 1];
            Array.Copy(_occupied, grown, _occupied.Length);
            grown[^1] = siteCell;
            _occupied = grown;
        }

        private void Build()
        {
            if (_built) return;
            _built = true;
            _frontier = _world.Terrain is null ? null : _frontierOf();
            if (_frontier is null) return;
            _occupied = new int[_world.Settlements.Count];
            for (int i = 0; i < _occupied.Length; i++) _occupied[i] = _world.Settlements[i].SiteCell;
            _spacing = SettlementSiting.SeedSpacing(_frontier, _occupied);
            _claimed = new bool[_frontier.Lattice.NodeCount];
            for (int i = 0; i < _world.CatchmentNodes.Count; i++)
            {
                int node = _world.CatchmentNodes[i].LatticeNode;
                if ((uint)node < (uint)_claimed.Length) _claimed[node] = true;
            }
        }
    }

    /// <summary>The order the AI issues for <paramref name="polity"/> this turn, or null — with a fresh
    /// <see cref="Sites"/> (a single-polity caller).</summary>
    public static OrderRecord? Decide(IReadOnlyWorldState world, SimConfig cfg, WorldgenConfig worldgen, PolityId polity) =>
        Decide(world, cfg, polity, new Sites(world, cfg, worldgen));

    /// <summary>The order the AI issues for <paramref name="polity"/> this turn, or null. When it issues one, the
    /// site is recorded in <paramref name="sites"/> for the later polities of the batch. THE production entry: the
    /// predicate is <see cref="FoundingQuery.Check(IReadOnlyWorldState, SimConfig, SettlementSiting.FrontierSiting, ReadOnlySpan{double}, ReadOnlySpan{int}, PolityId, SettlementId, int)"/>.</summary>
    public static OrderRecord? Decide(IReadOnlyWorldState world, SimConfig cfg, PolityId polity, Sites sites) =>
        Decide(world, cfg, polity, sites, FoundingQuery.Check);

    /// <summary>As <see cref="Decide(IReadOnlyWorldState, SimConfig, PolityId, Sites)"/>, asking
    /// <paramref name="check"/> as the predicate (the test seam of <see cref="SiteCheck"/>). Sources are tried in key
    /// order; for each, the site is the argmax over the cells <paramref name="check"/> answers Legal for, on the world
    /// the order will be judged on with the batch's standing sites — so the issued order is Legal by construction, and
    /// the next source is tried only when NO cell of the current one is Legal.</summary>
    public static OrderRecord? Decide(IReadOnlyWorldState world, SimConfig cfg, PolityId polity, Sites sites, SiteCheck check) =>
        Decide(world, cfg, polity, sites, check, PolityKnowledge.Of(world, polity, cfg));

    /// <summary>As <see cref="Decide(IReadOnlyWorldState, SimConfig, PolityId, Sites, SiteCheck)"/>, with the issuer's
    /// knowledge given (P7: the seam's test entry — production passes <see cref="PolityKnowledge.Of"/>).</summary>
    public static OrderRecord? Decide(IReadOnlyWorldState world, SimConfig cfg, PolityId polity, Sites sites, SiteCheck check,
        PolityKnowledge knowledge)
    {
        ArgumentNullException.ThrowIfNull(knowledge);
        if (knowledge.Polity.Value != polity.Value) throw new ArgumentException("the knowledge must be the issuer's", nameof(knowledge));
        if (cfg.Colonization?.AiFounding is not { Enabled: true } ai) return null;
        if (!EmpireQuery.TryGetCommandSource(world, polity, out CommandSource source) || source != CommandSource.Ai) return null;
        if (!IsDue(world, ai, polity)) return null;

        SettlementId[] ranked = RankedSources(world, cfg, ai, polity);
        if (ranked.Length == 0) return null;
        if (world.Terrain is not { } terrain || sites.Frontier is not { } f) return null;

        for (int k = 0; k < ranked.Length; k++)
        {
            SettlementId from = ranked[k];
            int cell = BestSiteFor(world, cfg, f, terrain, sites, polity, from, check, knowledge);
            if (cell < 0) continue;   // no cell of this source is Legal: the next source
            sites.Accept(cell);
            return FoundingQuery.FoundOrder(world, polity, from, cell);
        }
        return null;
    }

    /// <summary>
    /// THE INTERVAL ARM: true when the polity's newest settlement (largest FoundedTurn among the settlements it
    /// controls) was founded at least <see cref="AiFoundingConfig.MinYearsBetweenFoundings"/> years before
    /// <paramref name="world"/>, measured as the LOWER bound (Turn − newest) × Clock.DtYears. False for a polity
    /// that controls nothing.
    /// </summary>
    public static bool IsDue(IReadOnlyWorldState world, AiFoundingConfig ai, PolityId polity)
    {
        long newest = -1;
        for (int i = 0; i < world.Settlements.Count; i++)
        {
            SettlementRow row = world.Settlements[i];
            // P7: control first — a settlement's founding turn is read only for the polity's own settlements.
            if (EmpireQuery.ControlsSettlement(world, polity, row.Id) && row.FoundedTurn > newest) newest = row.FoundedTurn;
        }
        if (newest < 0) return false;
        double elapsedYears = (world.Clock.Turn - newest) * world.Clock.DtYears;
        return elapsedYears >= ai.MinYearsBetweenFoundings;
    }

    /// <summary>
    /// THE SOURCE ARM: the settlements <paramref name="polity"/> controls that may send colonists this turn —
    /// at least <see cref="AiFoundingConfig.MinSourcePopulation"/> people, fed (<see cref="FoodState"/> Normal or
    /// Stress), and FoundingQuery's people and food arms holding on the world's tables — in the composite key
    /// (population DESC, settlement id ASC).
    /// </summary>
    public static SettlementId[] RankedSources(IReadOnlyWorldState world, SimConfig cfg, AiFoundingConfig ai, PolityId polity)
    {
        var ids = new List<SettlementId>();
        var pops = new List<long>();
        for (int i = 0; i < world.Settlements.Count; i++)
        {
            SettlementId s = world.Settlements[i].Id;
            if (!EmpireQuery.ControlsSettlement(world, polity, s)) continue;
            long pop = Population(world, s);
            if (pop < ai.MinSourcePopulation) continue;
            if (FoodState.Of(world, s, cfg, out _) > FoodStateKind.Stress) continue;
            if (FoundingQuery.Departure(world.Buckets, world.GoodStocks, cfg, s, null, out _, FoundingQuery.PartyFraction(world, cfg, s))
                != FoundingVerdict.Legal) continue;

            // Insertion by the composite key (population DESC, id ASC): integers only, a total order.
            int at = ids.Count;
            while (at > 0 && (pops[at - 1] < pop || (pops[at - 1] == pop && ids[at - 1].Value > s.Value))) at--;
            ids.Insert(at, s);
            pops.Insert(at, pop);
        }
        return [.. ids];
    }

    /// <summary>THE SITE ARM for one source with THE predicate (<see cref="FoundingQuery.Check(IReadOnlyWorldState, SimConfig, SettlementSiting.FrontierSiting, ReadOnlySpan{double}, ReadOnlySpan{int}, PolityId, SettlementId, int)"/>).</summary>
    public static int BestSiteFor(
        IReadOnlyWorldState world, SimConfig cfg, SettlementSiting.FrontierSiting f, TerrainSet terrain, Sites sites,
        PolityId issuer, SettlementId source) =>
        BestSiteFor(world, cfg, f, terrain, sites, issuer, source, FoundingQuery.Check);

    /// <summary>
    /// THE SITE ARM for one source: the composite-key argmax (<see cref="BestSite"/>) of the frontier's unjittered
    /// scores over the cells <paramref name="check"/> answers Legal for, issued by <paramref name="issuer"/> from
    /// <paramref name="source"/> with the batch's standing sites; −1 when no cell is Legal.
    /// <list type="number">
    /// <item>The PRE-FILTER argmax: candidate, unobstructed by the batch's standing sites, unclaimed, and in the
    ///   source's connected component — each one of the predicate's own arms on the same inputs, so it never refuses
    ///   a Legal cell.</item>
    /// <item>THE predicate on that pick. Legal: it is the answer (the argmax of a superset that lies in the subset is
    ///   the subset's argmax). This is every pick today — the pre-filter is exactly the predicate's per-cell arms and
    ///   RankedSources already held the source's people and food arms — so the predicate and its route search run
    ///   ONCE per source.</item>
    /// <item>Refused: the scan is repeated with the predicate in the admission. BestSite asks the admission only of
    ///   strict improvers and never raises its bar on a refused one, so the result is the argmax over the cells the
    ///   pre-filter AND the predicate admit — the same source's next-best Legal cell.</item>
    /// </list>
    /// </summary>
    public static int BestSiteFor(
        IReadOnlyWorldState world, SimConfig cfg, SettlementSiting.FrontierSiting f, TerrainSet terrain, Sites sites,
        PolityId issuer, SettlementId source, SiteCheck check) =>
        BestSiteFor(world, cfg, f, terrain, sites, issuer, source, check, PolityKnowledge.Of(world, issuer, cfg));

    /// <summary>As <see cref="BestSiteFor(IReadOnlyWorldState, SimConfig, SettlementSiting.FrontierSiting, TerrainSet, Sites, PolityId, SettlementId, SiteCheck)"/>,
    /// with the issuer's knowledge given: the pre-filter's FIRST arm is <see cref="PolityKnowledge.KnowsCell"/> (P7), so
    /// only known cells are ranked.</summary>
    public static int BestSiteFor(
        IReadOnlyWorldState world, SimConfig cfg, SettlementSiting.FrontierSiting f, TerrainSet terrain, Sites sites,
        PolityId issuer, SettlementId source, SiteCheck check, PolityKnowledge knowledge)
    {
        ArgumentNullException.ThrowIfNull(knowledge);
        if (knowledge.Polity.Value != issuer.Value) throw new ArgumentException("the knowledge must be the issuer's", nameof(knowledge));
        int sourceCell = -1;
        for (int i = 0; i < world.Settlements.Count; i++)
            if (world.Settlements[i].Id == source) { sourceCell = world.Settlements[i].SiteCell; break; }
        if (sourceCell < 0) return -1;

        bool[] reachable = ReachableNodes(world, f, sourceCell);
        Func<int, bool> preFilter = cell =>
        {
            if (!knowledge.KnowsCell(cell)) return false;   // P7: the AI ranks only what it has discovered
            if (!SettlementSiting.IsFrontierCandidate(f, terrain, cell)) return false;
            if (!SettlementSiting.IsUnobstructedFrontierCell(f, sites.Spacing, sites.Occupied, cell)) return false;
            int node = LatticeMap.OriginLatticeNode(f.Lattice, f.Size, cell);
            return reachable[node] && !sites.IsClaimed(node);
        };
        int pick = BestSite(f.Scores, preFilter);
        if (pick < 0 || IsLegal(pick)) return pick;
        // THE predicate refused the pre-filter's pick: fall through to this source's next-best Legal cell.
        return BestSite(f.Scores, cell => preFilter(cell) && IsLegal(cell));

        bool IsLegal(int cell) =>
            check(world, cfg, f, sites.Spacing, sites.Occupied, issuer, source, cell) == FoundingVerdict.Legal;
    }

    /// <summary>The lattice nodes connected to <paramref name="sourceCell"/>'s origin node on the frontier lattice
    /// plus the world's network overlay: an uncapped <see cref="Pathfinder.Isochrone"/> — Dijkstra over the edge set
    /// and relaxation FindPath uses, so a node is here iff FindPath from the source finds it.</summary>
    public static bool[] ReachableNodes(IReadOnlyWorldState world, SettlementSiting.FrontierSiting f, int sourceCell)
    {
        var reachable = new bool[f.Lattice.NodeCount];
        int origin = LatticeMap.OriginLatticeNode(f.Lattice, f.Size, sourceCell);
        Pathfinder.IsochroneResult flood = Pathfinder.Isochrone(f.Lattice, world, origin, double.MaxValue);
        for (int i = 0; i < flood.Reached.Length; i++) reachable[flood.Reached[i]] = true;
        return reachable;
    }

    /// <summary>
    /// THE RANKING: the cell maximizing the composite key (score DESC, cell ASC) among the cells
    /// <paramref name="admits"/> accepts; −1 when it accepts none. An ascending cell scan that takes a cell only when
    /// its score is STRICTLY greater than the best so far and it is admitted implements exactly that key — among
    /// equal scores the lowest cell wins — and an inadmissible improver never raises the bar (the
    /// SettlementSiting.ChooseFrontierSite argument). A NaN score is never taken. Pure; <paramref name="admits"/> is
    /// asked only of strict improvers.
    /// </summary>
    public static int BestSite(ReadOnlySpan<double> scores, Func<int, bool> admits)
    {
        int bestCell = -1;
        double bestScore = double.NegativeInfinity;
        for (int cell = 0; cell < scores.Length; cell++)
        {
            double score = scores[cell];
            if (!(score > bestScore)) continue;
            if (!admits(cell)) continue;
            bestScore = score;
            bestCell = cell;
        }
        return bestCell;
    }

    private static long Population(IReadOnlyWorldState world, SettlementId s)
    {
        long n = 0;
        for (int i = 0; i < world.Buckets.Count; i++)
            if (world.Buckets[i].Settlement == s) n += world.Buckets[i].Count.Value;
        return n;
    }
}
