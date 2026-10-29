namespace Sim.Core.Worldgen;

/// <summary>
/// Deterministic settlement siting (T1.4; T3.1 worldgen refresh): argmax over
/// land cells of fertility × water-access × (1 + jitter), where water-access
/// falls linearly from 1 at the shoreline OR RIVERBANK to 0 at the TUNE
/// cutoff (siting.waterAccessCutoffPx). T3.1(a): the access BFS seeds from
/// river cells as well as sea — before that fix a fertile interior river
/// valley scored ~0 access and the missing settlement type (inland riverine)
/// could never win. T3.1(c): the seeded multiplicative score jitter
/// (siting.scoreJitter) lets lower-scored sites win sometimes, so founding
/// varies with the world seed instead of always picking the twelve
/// near-identical coastal argmaxes.
///
/// DETERMINISM: the argmax uses the composite key (score DESC, cell id ASC) —
/// a total order, per the constitution's tie-break rule. Scanning cells in
/// ascending id with a strictly-greater comparison implements exactly that key:
/// among equal scores the lowest id wins. The tie-dense siting test pins this.
/// The jitter is a pure SplitMix64 hash of (seed, cell) — twin worlds stay
/// byte-identical.
///
/// Pure function of its inputs — no state, no caches (T1.3 purity mandate).
/// </summary>
public static class SettlementSiting
{
    /// <summary>Salt keeping the siting-jitter hash space disjoint from every
    /// worldgen noise salt.</summary>
    private const ulong JitterSalt = 0x53495445_00000003UL;

    /// <summary>Chooses the site cell for a new settlement on this terrain.</summary>
    public static int ChooseSite(TerrainSet terrain, SitingConfig cfg) =>
        ChooseSite(terrain.Fertility, terrain.Water, terrain.Size, cfg.WaterAccessCutoffPx,
            terrain.Rivers, terrain.WrapX);

    /// <summary>The T3.1(c) score jitter for one cell: 1 + j·u with u ∈ [−1,1]
    /// derived by SplitMix64 from (seed, cell) — deterministic, twin-safe.</summary>
    public static double Jitter(ulong seed, int cell, double amplitude)
    {
        if (amplitude <= 0.0) return 1.0;
        ulong h = Kernel.SplitMix64.Mix(seed ^ JitterSalt ^ (ulong)(uint)cell * 0x9E3779B97F4A7C15UL);
        double u = (h >> 11) * (1.0 / (1UL << 53)) * 2.0 - 1.0;   // [−1, 1)
        return 1.0 + amplitude * u;
    }

    /// <summary>
    /// D-025 (T2.3): iterative top-score siting of <paramref name="count"/>
    /// settlements with a minimum TRAVEL-TIME spacing between accepted sites.
    /// Each pick is the argmax by the composite key (score DESC, cell id ASC)
    /// over cells still eligible; after each acceptance the spacing exclusion
    /// grows. TWO-STAGE spacing check (documented shape):
    ///   1. PREFILTER — a spacing-budget-CAPPED Dijkstra from each accepted
    ///      site's lattice origin: any node the capped expansion never reaches
    ///      is trivially far enough (travel &gt; MinSpacingKm), so the
    ///      exact check touches only the local region around accepted sites
    ///      (the straight-line px reasoning, made exact by the cap).
    ///   2. EXACT — a cell is blocked iff its lattice node's min travel cost
    ///      from any accepted site is &lt; MinSpacingKm.
    /// Sites are returned in PICK ORDER — settlement id i is the i-th pick
    /// (best first), the deterministic identity every consumer relies on.
    /// Throws when the terrain cannot host <paramref name="count"/> sites at
    /// this spacing (an actionable config/terrain mismatch, never a silent
    /// shortfall).
    /// </summary>
    /// <summary>
    /// The UNJITTERED candidate-score raster (T3.1c): REGION fertility (box
    /// sum over ±fertilityRadiusPx — carrying capacity is a catchment
    /// property, and a fertile POINT in a barren region founded colonies that
    /// crashed 90% within five turns) × water-or-river access. Water cells
    /// score 0. Single source of truth for ChooseSites and the siting tests.
    /// </summary>
    public static double[] CandidateScores(TerrainSet terrain, SitingConfig cfg)
    {
        int size = terrain.Size;
        ReadOnlySpan<double> water = terrain.Water;
        int[] waterDistance = WaterDistanceBfs(water, terrain.Rivers, size, terrain.WrapX);
        double[] region = BoxSum(terrain.Fertility, size, cfg.FertilityRadiusPx, terrain.WrapX);
        var scores = new double[size * size];
        for (int i = 0; i < scores.Length; i++)
        {
            if (water[i] >= 0.5) continue;
            double access = WaterAccess(waterDistance[i], cfg.WaterAccessCutoffPx);
            if (access <= 0.0) continue;
            scores[i] = region[i] * access;
        }
        return scores;
    }

    /// <summary>
    /// THE WATER-ACCESS FACTOR of one cell (T3.1a), the second factor of
    /// <see cref="CandidateScores"/>: 1 at the shoreline or riverbank, falling
    /// linearly to 0 at <paramref name="cutoffPx"/> BFS steps, floored at 0
    /// beyond it. <paramref name="waterDistancePx"/> is the cell's entry in
    /// <see cref="WaterDistanceBfs"/>. M5 S1a: made public (no behaviour change)
    /// so a read-only consumer can show a candidate's score decomposed into
    /// its two factors without restating the formula. The floor is the
    /// primitive overload's `access &lt; 0 → 0`; <see cref="CandidateScores"/>
    /// skips `access &lt;= 0`, which reads the floored value identically.
    /// </summary>
    public static double WaterAccess(int waterDistancePx, int cutoffPx)
    {
        double access = 1.0 - waterDistancePx / (double)cutoffPx;
        return access < 0.0 ? 0.0 : access;
    }

    /// <summary>Box-filter sum over (2r+1)² neighbourhoods via an integral
    /// image — O(cells), edge-clamped, deterministic (pure prefix sums).
    /// With the fertility raster and r = siting.fertilityRadiusPx it is the
    /// REGION-FERTILITY factor of <see cref="CandidateScores"/>. M5 S1a: made
    /// public (no behaviour change) for the same read-only decomposition as
    /// <see cref="WaterAccess"/>. r ≤ 0 returns a copy of the field.
    ///
    /// WS3 (<paramref name="wrapX"/>): on a cylinder the window is MODULAR in x — a window that crosses the seam is
    /// the sum of its two in-map pieces, [x − r + size, size − 1] and [0, x + r − size], each read from the SAME
    /// integral image; y stays clamped. A window that does not cross the seam takes the unchanged expression, so its
    /// value is bit-identical to the flat one (pinned). A window as wide as the map (2r + 1 ≥ size) covers every
    /// column exactly once — clamped to the map width, never double-counted.</summary>
    public static double[] BoxSum(ReadOnlySpan<double> field, int size, int r, bool wrapX = false)
    {
        if (r <= 0) return field.ToArray();
        // Integral image: I[y+1][x+1] = Σ field[0..y][0..x].
        var integral = new double[(size + 1) * (size + 1)];
        for (int y = 0; y < size; y++)
        {
            double rowSum = 0.0;
            for (int x = 0; x < size; x++)
            {
                rowSum += field[y * size + x];
                integral[(y + 1) * (size + 1) + (x + 1)] =
                    integral[y * (size + 1) + (x + 1)] + rowSum;
            }
        }
        var sums = new double[size * size];
        bool full = wrapX && 2 * (long)r + 1 >= size;   // the window spans every column: once each
        for (int y = 0; y < size; y++)
        {
            int y0 = Math.Max(0, y - r), y1 = Math.Min(size - 1, y + r);
            for (int x = 0; x < size; x++)
            {
                if (!wrapX || full || (x - r >= 0 && x + r <= size - 1))
                {
                    // Flat, a full-width wrapped window, or an interior window: the unchanged expression.
                    int x0 = full ? 0 : Math.Max(0, x - r), x1 = full ? size - 1 : Math.Min(size - 1, x + r);
                    sums[y * size + x] = Rect(integral, size, x0, x1, y0, y1);
                }
                else if (x - r < 0)
                {
                    // Crosses the seam westward: [x − r + size, size − 1] ∪ [0, x + r].
                    sums[y * size + x] = Rect(integral, size, x - r + size, size - 1, y0, y1)
                                       + Rect(integral, size, 0, x + r, y0, y1);
                }
                else
                {
                    // Crosses the seam eastward: [x − r, size − 1] ∪ [0, x + r − size].
                    sums[y * size + x] = Rect(integral, size, x - r, size - 1, y0, y1)
                                       + Rect(integral, size, 0, x + r - size, y0, y1);
                }
            }
        }
        return sums;
    }

    /// <summary>The integral-image sum over columns [x0, x1] × rows [y0, y1] — the four-term expression BoxSum has
    /// always used, in the same order.</summary>
    private static double Rect(double[] integral, int size, int x0, int x1, int y0, int y1) =>
        integral[(y1 + 1) * (size + 1) + (x1 + 1)]
        - integral[y0 * (size + 1) + (x1 + 1)]
        - integral[(y1 + 1) * (size + 1) + x0]
        + integral[y0 * (size + 1) + x0];

    /// <summary>
    /// T3.1(c) SCORE FLOOR: the jitter chooses among GOOD sites — cells whose
    /// unjittered score falls below this percentile of all positive-score land
    /// cells are ineligible. EXTRACTED at T4.4 so turn-zero siting and dynamic
    /// founding cannot drift apart: one definition, two callers.
    /// </summary>
    public static double ScoreFloor(ReadOnlySpan<double> scores, double percentile)
    {
        if (percentile <= 0.0) return 0.0;
        var positive = new List<double>();
        for (int i = 0; i < scores.Length; i++)
            if (scores[i] > 0.0) positive.Add(scores[i]);
        if (positive.Count == 0) return 0.0;
        positive.Sort();
        return positive[Math.Min(positive.Count - 1, (int)(positive.Count * percentile))];
    }

    public static int[] ChooseSites(
        TerrainSet terrain, SitingConfig cfg, int count, double riverCostFactor, ulong seed = 0UL)
    {
        int size = terrain.Size;
        ReadOnlySpan<double> water = terrain.Water;
        double[] scores = CandidateScores(terrain, cfg);
        var lattice = Pathing.TraversalLattice.Build(terrain, riverCostFactor);

        // T3.1(c) SCORE FLOOR: the jitter chooses among GOOD sites — cells
        // whose unjittered score falls below the configured percentile of all
        // positive-score land cells are ineligible (see SitingConfig).
        double floor = ScoreFloor(scores, cfg.ScoreFloorPercentile);

        // Min travel cost from any accepted site, per lattice node (∞ start).
        var spacing = new double[lattice.NodeCount];
        Array.Fill(spacing, double.PositiveInfinity);

        // T3.2b: the TUNE spacing is an IDEAL-GROUND distance in km; the
        // pathfinder wants cost units. One conversion, at the chokepoint.
        double minSpacingCostUnits =
            Pathing.LatticeGeometry.CostUnitsForIdealGroundKm(lattice, cfg.MinSpacingKm);
        var sites = new int[count];
        for (int pick = 0; pick < count; pick++)
        {
            int bestCell = -1;
            double bestScore = double.NegativeInfinity;
            for (int i = 0; i < scores.Length; i++)
            {
                if (water[i] >= 0.5) continue; // land candidates only
                double unjittered = scores[i];
                if (unjittered <= 0.0 || unjittered < floor) continue; // no access / below floor
                double score = unjittered * Jitter(seed, i, cfg.ScoreJitter);
                // Strictly-greater on an ascending-id scan = (score DESC, id ASC).
                // The spacing test runs only on strict improvements (the
                // lattice-origin lookup is the expensive part); a blocked
                // improver is skipped WITHOUT raising bestScore, so the pick
                // remains the true argmax over eligible cells.
                if (score <= bestScore) continue;
                if (pick > 0)
                {
                    int node = Pathing.LatticeMap.OriginLatticeNode(lattice, size, i);
                    if (spacing[node] < minSpacingCostUnits) continue; // stage-2 exact check
                }
                bestScore = score;
                bestCell = i;
            }
            if (bestCell < 0)
                throw new InvalidOperationException(
                    $"settlement siting could only place {pick} of {count} sites at " +
                    $"minSpacingKm {cfg.MinSpacingKm.ToString(System.Globalization.CultureInfo.InvariantCulture)} " +
                    "— terrain too small or spacing too large.");
            sites[pick] = bestCell;

            // Stage-1 prefilter: relax the spacing field with a capped Dijkstra
            // from the new site (no network overlay — spacing is a worldgen
            // property of raw terrain).
            int origin = Pathing.LatticeMap.OriginLatticeNode(lattice, size, bestCell);
            Pathing.Pathfinder.RelaxCappedFrom(lattice, origin, minSpacingCostUnits, spacing);
        }
        return sites;
    }

    /// <summary>
    /// Primitive overload (test surface for the tie-dense proof): fertility and
    /// water rasters as flat row-major spans. Returns the chosen cell id.
    /// Candidates are LAND cells only (water ≥ 0.5 is excluded), so the result
    /// is on land even if every score is 0. Throws if the raster has no land.
    /// </summary>
    public static int ChooseSite(
        ReadOnlySpan<double> fertility, ReadOnlySpan<double> water, int size, int waterAccessCutoffPx,
        ReadOnlySpan<double> rivers = default, bool wrapX = false)
    {
        if (fertility.Length != size * size || water.Length != size * size)
            throw new ArgumentException("fertility and water must be size*size long");

        int[] waterDistance = WaterDistanceBfs(water, rivers, size, wrapX);

        int bestCell = -1;
        double bestScore = double.NegativeInfinity;
        for (int i = 0; i < fertility.Length; i++)
        {
            if (water[i] >= 0.5) continue; // land candidates only
            double access = WaterAccess(waterDistance[i], waterAccessCutoffPx);
            double score = fertility[i] * access;
            // Strictly-greater on an ascending-id scan = (score DESC, id ASC).
            if (score > bestScore)
            {
                bestScore = score;
                bestCell = i;
            }
        }

        return bestCell >= 0 ? bestCell
            : throw new InvalidOperationException(
                "settlement siting found no land cell — terrain is all water.");
    }

    /// <summary>
    /// Multi-source BFS grid distance (px steps) from every water OR RIVER
    /// cell (T3.1a: a riverbank is water access — fertile interior river
    /// valleys were unreachable settlement types while only the sea seeded
    /// this) — the same fixed expansion order (sources row-major; W, E, N, S)
    /// as worldgen's moisture BFS, restated here because that helper is
    /// private to its stage. An empty rivers span means sea-only (the
    /// primitive test surface). Cells unreachable keep int.MaxValue.
    /// M5 S1a: public (no behaviour change) — the input of
    /// <see cref="WaterAccess"/> for a read-only score decomposition.
    /// WS3 (<paramref name="wrapX"/>): W and E wrap on a cylinder, in the same
    /// W, E, N, S order (<see cref="Pathing.MapTopology.NeighbourX"/>).
    /// </summary>
    public static int[] WaterDistanceBfs(
        ReadOnlySpan<double> water, ReadOnlySpan<double> rivers, int size, bool wrapX = false)
    {
        int cells = size * size;
        var dist = new int[cells];
        var queue = new int[cells];
        int head = 0, tail = 0;

        for (int i = 0; i < cells; i++)
        {
            bool source = water[i] >= 0.5 || (rivers.Length == cells && rivers[i] >= 0.5);
            if (source) { dist[i] = 0; queue[tail++] = i; }
            else dist[i] = int.MaxValue;
        }

        while (head < tail)
        {
            int i = queue[head++];
            int x = i % size, y = i / size, d = dist[i] + 1;
            int w = Pathing.MapTopology.NeighbourX(x, -1, size, wrapX), e = Pathing.MapTopology.NeighbourX(x, 1, size, wrapX);
            if (w >= 0 && dist[y * size + w] == int.MaxValue) { dist[y * size + w] = d; queue[tail++] = y * size + w; }
            if (e >= 0 && dist[y * size + e] == int.MaxValue) { dist[y * size + e] = d; queue[tail++] = y * size + e; }
            if (y > 0 && dist[i - size] == int.MaxValue) { dist[i - size] = d; queue[tail++] = i - size; }
            if (y < size - 1 && dist[i + size] == int.MaxValue) { dist[i + size] = d; queue[tail++] = i + size; }
        }
        return dist;
    }

    // ---- T4.4 DYNAMIC FOUNDING -------------------------------------------
    //
    // The SAME machinery as turn-zero siting, differing in exactly one thing:
    // at turn zero the spacing field starts +INFINITY everywhere because no
    // site exists yet, whereas a frontier pick must be spaced against every
    // settlement ALREADY standing. So the field is SEEDED from the existing
    // sites and the identical argmax runs over it.
    //
    // Nothing here is a second site-selection algorithm. `CandidateScores`,
    // `Jitter`, `ScoreFloor`, `TraversalLattice.Build`, `OriginLatticeNode`
    // and `RelaxCappedFrom` are the turn-zero functions, called unchanged —
    // which is also how T4.7's river-aware traversal stays authoritative:
    // the lattice is built with `riverCostFactor` exactly as `ChooseSites`
    // builds it, and bypassing it would require writing the second algorithm
    // this deliberately avoids.

    /// <summary>
    /// The parts of frontier siting that are PURE FUNCTIONS OF STATIC TERRAIN
    /// (ADR-008: terrain is neither serialized nor cloned, so these never
    /// change over a run). Built once and reused every turn — rebuilding a
    /// 1024² score raster and a traversal lattice per turn would be a hot-path
    /// cost for a result that cannot differ.
    /// </summary>
    public sealed class FrontierSiting
    {
        public required Pathing.TraversalLattice Lattice { get; init; }
        public required double[] Scores { get; init; }
        public required double Floor { get; init; }
        public required double MinSpacingCostUnits { get; init; }
        public required int Size { get; init; }
        /// <summary>D-066 A18 (M6 closure, track FOG): the siting config this frontier was prepared with — what the AI's
        /// known-terrain valuation (<see cref="State.KnownSiting"/>) recomputes the score from over the cells its polity
        /// knows. Null on a hand-built frontier (a test raster); such a frontier cannot be valued over a partial view.</summary>
        public SitingConfig? Siting { get; init; }
    }

    /// <summary>Prepare the terrain-invariant half of frontier siting (once per run).</summary>
    public static FrontierSiting PrepareFrontier(
        TerrainSet terrain, SitingConfig cfg, double riverCostFactor)
    {
        var lattice = Pathing.TraversalLattice.Build(terrain, riverCostFactor);
        double[] scores = CandidateScores(terrain, cfg);
        return new FrontierSiting
        {
            Lattice = lattice,
            Scores = scores,
            Floor = ScoreFloor(scores, cfg.ScoreFloorPercentile),
            // The ONE km→cost conversion, at the same chokepoint ChooseSites
            // uses. ADR-018's spacing floor is a TRAVEL-COST distance.
            MinSpacingCostUnits =
                Pathing.LatticeGeometry.CostUnitsForIdealGroundKm(lattice, cfg.MinSpacingKm),
            Size = terrain.Size,
            Siting = cfg,
        };
    }

    /// <summary>
    /// Seed the spacing exclusion field from every site already standing —
    /// the one thing turn-zero siting cannot do, because at turn zero there
    /// are none. Same capped Dijkstra, same cap, same raw-terrain lattice
    /// (spacing is a property of the ground, not of the built network).
    /// </summary>
    public static double[] SeedSpacing(FrontierSiting f, ReadOnlySpan<int> existingSiteCells)
    {
        var spacing = new double[f.Lattice.NodeCount];
        Array.Fill(spacing, double.PositiveInfinity);
        for (int i = 0; i < existingSiteCells.Length; i++)
        {
            int origin = Pathing.LatticeMap.OriginLatticeNode(f.Lattice, f.Size, existingSiteCells[i]);
            Pathing.Pathfinder.RelaxCappedFrom(f.Lattice, origin, f.MinSpacingCostUnits, spacing);
        }
        return spacing;
    }

    /// <summary>
    /// One frontier pick: the argmax by the composite key (score DESC, cell id
    /// ASC) over the cells <see cref="IsLegalFrontierCell"/> accepts — land,
    /// above the score floor, OUTSIDE every existing settlement's spacing
    /// exclusion, and NOT already a settlement's site cell.
    ///
    /// THE TWO REJECTIONS ARE SEPARATE AND BOTH ARE REQUIRED (director ruling,
    /// T4.4). Minimum travel-cost SPACING and exact SITE-CELL OCCUPANCY are
    /// different rules: spacing is evaluated on the candidate's ORIGIN LATTICE
    /// NODE (stride 4, ~16 km), so two distinct cells inside one node read the
    /// same spacing value, and the spacing test alone therefore cannot be
    /// relied on to keep site cells distinct. Turn-zero siting gets
    /// distinctness only INCIDENTALLY, from `minSpacingKm` being large and the
    /// test being strictly-less. Dynamic founding asserts it outright.
    ///
    /// M5 S1a: the per-cell legality is no longer inline. It is the ONE
    /// predicate <see cref="IsLegalFrontierCell"/>, which any other caller (an
    /// order validator, a placement preview) shares, and which is DEFINED as the
    /// conjunction of its two halves: <see cref="IsFrontierCandidate"/> (the
    /// static terrain rules) and <see cref="IsUnobstructedFrontierCell"/> (the
    /// occupancy and spacing rules, which depend on who already stands where).
    /// The scan calls the two halves in the order the T4.4 loop tested them —
    /// static half, then the strict-improvement test, then the dynamic half —
    /// so the lattice-origin lookup still runs only on strict improvements and
    /// the jitter hash only on candidates. That order is a cost choice and
    /// nothing more: every test is pure, a cell is taken iff it is legal AND
    /// strictly better than the best so far, and an illegal improver is skipped
    /// WITHOUT raising bestScore — so the pick is the (score DESC, cell id ASC)
    /// argmax over <see cref="IsLegalFrontierCell"/> cells. (Measured: calling
    /// the whole predicate after the score test returns the same sites but
    /// hashes the jitter for every cell, about twice the cost per call.)
    ///
    /// Returns −1 when no legal site exists. It does NOT throw: "the frontier
    /// is full" is an ordinary outcome of a per-turn system, not a config
    /// error, which is the other reason `ChooseSites` cannot be called here.
    /// </summary>
    public static int ChooseFrontierSite(
        FrontierSiting f, TerrainSet terrain, double[] spacing,
        ReadOnlySpan<int> occupiedSiteCells, double scoreJitter, ulong seed)
    {
        int bestCell = -1;
        double bestScore = double.NegativeInfinity;
        for (int i = 0; i < f.Scores.Length; i++)
        {
            if (!IsFrontierCandidate(f, terrain, i)) continue;             // land, score, floor
            double score = f.Scores[i] * Jitter(seed, i, scoreJitter);
            // Strictly-greater on an ascending scan = (score DESC, cell id ASC).
            if (score <= bestScore) continue;
            if (!IsUnobstructedFrontierCell(f, spacing, occupiedSiteCells, i)) continue; // occupancy, spacing
            bestScore = score;
            bestCell = i;
        }
        return bestCell;
    }

    /// <summary>
    /// THE frontier-site legality predicate (M5 S1a, extracted unchanged from
    /// <see cref="ChooseFrontierSite"/>): a cell is a legal frontier site iff it
    /// is a <see cref="IsFrontierCandidate">candidate</see> AND
    /// <see cref="IsUnobstructedFrontierCell">unobstructed</see> —
    ///   1. land — water &lt; 0.5;
    ///   2. the UNJITTERED candidate score is &gt; 0 and not below the floor;
    ///   3. not already a settlement's site cell (<see cref="IsOccupied"/>);
    ///   4. its origin lattice node is not inside any standing settlement's
    ///      spacing exclusion (`spacing[node] &lt; MinSpacingCostUnits` rejects).
    /// A cell outside the map is not legal (the scan never asks; another caller may).
    ///
    /// What this does NOT check, stated because a caller could assume it: it is
    /// not a reachability rule (an unreachable cell keeps +∞ spacing and passes
    /// rule 4 — ColonizationTests documents the semantics) and not a catchment
    /// or claim rule. Pure: reads its arguments only, writes nothing.
    /// </summary>
    public static bool IsLegalFrontierCell(
        FrontierSiting f, TerrainSet terrain, ReadOnlySpan<double> spacing,
        ReadOnlySpan<int> occupiedSiteCells, int cell) =>
        IsFrontierCandidate(f, terrain, cell)
        && IsUnobstructedFrontierCell(f, spacing, occupiedSiteCells, cell);

    /// <summary>
    /// The STATIC half of <see cref="IsLegalFrontierCell"/> (rules 1–2): the cell
    /// is on the map, land, and its unjittered score is &gt; 0 and not below the
    /// floor. A pure function of terrain, so it never changes over a run. Each
    /// rule is the NEGATION of the original T4.4 rejection test, so a NaN reads
    /// exactly as it did inline.
    /// </summary>
    public static bool IsFrontierCandidate(FrontierSiting f, TerrainSet terrain, int cell)
    {
        if ((uint)cell >= (uint)f.Scores.Length) return false;
        if (terrain.Water[cell] >= 0.5) return false;                      // land only
        double unjittered = f.Scores[cell];
        return !(unjittered <= 0.0 || unjittered < f.Floor);               // score > 0, ≥ floor
    }

    /// <summary>
    /// The DYNAMIC half of <see cref="IsLegalFrontierCell"/> (rules 3–4): the cell
    /// is on the map, is not a standing settlement's exact site cell, and its
    /// origin lattice node is outside every spacing exclusion in
    /// <paramref name="spacing"/> (as <see cref="SeedSpacing"/> /
    /// <see cref="AcceptFrontierSite"/> build it). Written as the negation of the
    /// original `spacing[node] &lt; MinSpacingCostUnits` rejection.
    /// </summary>
    public static bool IsUnobstructedFrontierCell(
        FrontierSiting f, ReadOnlySpan<double> spacing, ReadOnlySpan<int> occupiedSiteCells, int cell)
    {
        if ((uint)cell >= (uint)f.Scores.Length) return false;
        if (IsOccupied(occupiedSiteCells, cell)) return false;             // exact site cell
        int node = Pathing.LatticeMap.OriginLatticeNode(f.Lattice, f.Size, cell);
        return !(spacing[node] < f.MinSpacingCostUnits);                   // travel-cost spacing
    }

    /// <summary>Exact site-cell occupancy: is <paramref name="cell"/> one of
    /// <paramref name="cells"/>? Linear scan — the settlement count is small and
    /// law 5 forbids a hash set here. M5 S1a: public (no behaviour change).</summary>
    public static bool IsOccupied(ReadOnlySpan<int> cells, int cell)
    {
        for (int i = 0; i < cells.Length; i++) if (cells[i] == cell) return true;
        return false;
    }

    /// <summary>Grow the exclusion field after a frontier site is accepted.</summary>
    public static void AcceptFrontierSite(FrontierSiting f, double[] spacing, int siteCell)
    {
        int origin = Pathing.LatticeMap.OriginLatticeNode(f.Lattice, f.Size, siteCell);
        Pathing.Pathfinder.RelaxCappedFrom(f.Lattice, origin, f.MinSpacingCostUnits, spacing);
    }
}
