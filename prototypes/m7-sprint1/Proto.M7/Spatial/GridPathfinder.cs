namespace Proto.M7.Spatial;

/// <summary>Result of a grid search. Cells = linear cell indices start..goal (empty when not found).</summary>
public sealed record PathResult(bool Found, long Cost, int[] Cells, int Expanded);

/// <summary>
/// Integer A* on a <see cref="TerrainGrid"/> (spec §3).
/// - 8-connected. Orthogonal step cost = (cost[a] + cost[b]) * S / 2 (exact: S is even).
///   Diagonal step cost = floor((cost[a] + cost[b]) * S * DiagNum / (2 * DiagDen)), DiagNum/DiagDen = 1414/1000 (sqrt 2,
///   exact integer constant, documented). Units: cost x1000 times mm ("milli cost-mm"); time on open ground at speed v mm/s
///   is cost/v ms (see <see cref="PathMovement"/>).
/// - No corner cutting: a diagonal step is allowed only if BOTH orthogonally adjacent cells are passable. This is exactly
///   what makes the cell-centre polyline consistent with the supercover (the diagonal centre-to-centre segment passes through
///   the shared corner, whose corner rule visits both side cells) — proven by test.
/// - Heuristic = octile distance x m, m = the minimum cost over the passable cells PRESENT IN THIS GRID (recomputed by
///   RefreshCosts; any lower bound is admissible, and the grid minimum is the tightest one that is still exact for this grid):
///   h = m*S*(dmax - dmin) + dmin * floor(m*S*DiagNum/DiagDen). Every
///   orthogonal step costs &gt;= m*S and every diagonal step &gt;= floor(2m*S*DiagNum/(2*DiagDen)), so h is admissible; with
///   m*S &lt;= diagUnit &lt;= 2*m*S it is also consistent (a metric), so a closed cell is never reopened.
/// - Open list = explicit binary min-heap keyed by the TOTAL order (f, h, cellIndex). Equal keys can only be duplicate
///   entries of the same cell with the same g (lazy deletion), so extraction order is a pure function of the key set.
/// - Parent rule: a cell's parent changes on strictly smaller g, or on equal g from a parent with a smaller cell index
///   (canonical tie-break). Together these make the returned path independent of neighbour enumeration / insertion order
///   (tie-dense test permutes the neighbour order).
/// The instance holds reusable work arrays (stamp-cleared); it is NOT thread-safe. Pure function of (grid, costs, start, goal).
/// [TR] CLIMB (lane TR, optional): with a <see cref="TerrainEffects"/> every step a-&gt;b also costs
/// TerrainEffects.ClimbCost(height[b] - height[a]) (Naismith: ascent costs, descent is free), so a path over a hill is dearer
/// than the same path on the flat and routes around steep ground when that is cheaper. The term is &gt;= 0, so the heuristic
/// (a lower bound of the climb-free cost) stays admissible and consistent. Without it (null) costs are exactly Slice C's.
/// </summary>
public sealed class GridPathfinder
{
    public const long DiagNum = 1414;
    public const long DiagDen = 1000;

    private static readonly int[] DefaultDx = { -1, 1, 0, 0, -1, 1, -1, 1 };
    private static readonly int[] DefaultDy = { 0, 0, -1, 1, -1, -1, 1, 1 };

    private readonly TerrainGrid _grid;
    private readonly TerrainCostTable _costs;
    private readonly int[] _dx = new int[8], _dy = new int[8];
    private readonly long[] _cellCost;        // per cell: cost (0 = impassable)
    private readonly long[] _g;
    private readonly int[] _parent;
    private readonly int[] _seen;             // stamp: g/parent valid
    private readonly int[] _closed;           // stamp: closed
    private int _stamp;
    private long _minCost;
    private readonly TerrainEffects? _climb;   // [TR] null = Slice C costs
    private readonly long[] _cellHeight;       // [TR] per cell ground height (climb term)

    // Heap (struct-of-arrays).
    private long[] _hf = new long[1024], _hh = new long[1024];
    private int[] _hi = new int[1024];
    private int _hn;

    /// <param name="neighbourOrder">Optional permutation of 0..7 over the default direction list (W,E,N,S,NW,NE,SW,SE) — test hook to prove order independence.</param>
    /// <param name="climb">[TR] Optional Naismith climb term (lane TR); null = Slice C step costs exactly.</param>
    public GridPathfinder(TerrainGrid grid, TerrainCostTable costs, ReadOnlySpan<int> neighbourOrder = default, TerrainEffects? climb = null)
    {
        _grid = grid; _costs = costs; _climb = climb;
        if (neighbourOrder.IsEmpty) { DefaultDx.CopyTo(_dx, 0); DefaultDy.CopyTo(_dy, 0); }
        else
        {
            if (neighbourOrder.Length != 8) throw new ArgumentException("neighbour order must be a permutation of 0..7");
            var used = new bool[8];
            for (int k = 0; k < 8; k++)
            {
                int d = neighbourOrder[k];
                if (d < 0 || d > 7 || used[d]) throw new ArgumentException("neighbour order must be a permutation of 0..7");
                used[d] = true; _dx[k] = DefaultDx[d]; _dy[k] = DefaultDy[d];
            }
        }
        int n = grid.CellCount;
        _cellCost = new long[n];
        _cellHeight = new long[n];
        _g = new long[n]; _parent = new int[n]; _seen = new int[n]; _closed = new int[n];
        RefreshCosts();
    }

    /// <summary>Re-reads terrain classes into the cost cache (call after editing the grid).</summary>
    public void RefreshCosts()
    {
        long min = _costs.MinPassableCost;          // fallback when the grid has no passable cell
        long gridMin = long.MaxValue;
        for (int i = 0; i < _cellCost.Length; i++)
        {
            long c = _costs.Cost(_grid.ClassAtIndex(i));
            _cellCost[i] = c;
            _cellHeight[i] = _grid.HeightAtIndex(i);
            if (c != 0 && c < gridMin) gridMin = c;
        }
        _minCost = gridMin == long.MaxValue ? min : gridMin;
    }

    /// <summary>The heuristic's cost scale m (minimum passable cost present in the grid).</summary>
    public long HeuristicCostScale => _minCost;

    public long OrthStep(long ca, long cb) => (ca + cb) * _grid.CellSizeMm / 2;
    public long DiagStep(long ca, long cb) => (ca + cb) * _grid.CellSizeMm * DiagNum / (2 * DiagDen);

    /// <summary>[TR] Climb term of a step from cell index a to its neighbour b (0 without a climb table or when not ascending).</summary>
    public long ClimbStep(int a, int b) => _climb is null ? 0 : _climb.ClimbCost(_cellHeight[b] - _cellHeight[a]);

    public long Heuristic(int cx, int cy, int gx, int gy)
    {
        long ddx = Math.Abs(cx - gx), ddy = Math.Abs(cy - gy);
        long dmin = Math.Min(ddx, ddy), dmax = Math.Max(ddx, ddy);
        long m = _minCost, s = _grid.CellSizeMm;
        return m * s * (dmax - dmin) + dmin * (m * s * DiagNum / DiagDen);
    }

    public PathResult FindPath(CellCoord start, CellCoord goal)
    {
        var grid = _grid;
        if (!grid.InBounds(start.X, start.Y) || !grid.InBounds(goal.X, goal.Y)) return new PathResult(false, 0, Array.Empty<int>(), 0);
        int si = grid.Index(start.X, start.Y), gi = grid.Index(goal.X, goal.Y);
        if (_cellCost[si] == 0 || _cellCost[gi] == 0) return new PathResult(false, 0, Array.Empty<int>(), 0);
        _stamp = checked(_stamp + 1);
        int stamp = _stamp;
        int W = grid.Width, H = grid.Height, gx = (int)goal.X, gy = (int)goal.Y;
        _hn = 0;
        _g[si] = 0; _parent[si] = -1; _seen[si] = stamp;
        long h0 = Heuristic((int)start.X, (int)start.Y, gx, gy);
        Push(h0, h0, si);
        int expanded = 0;
        while (_hn > 0)
        {
            Pop(out long f, out long h, out int ci);
            if (_closed[ci] == stamp) continue;
            if (f - h != _g[ci]) continue;                    // stale duplicate (a better g was pushed later)
            _closed[ci] = stamp;
            expanded++;
            if (ci == gi) return new PathResult(true, _g[gi], Reconstruct(gi), expanded);
            int x = ci % W, y = ci / W;
            long gc = _g[ci], cc = _cellCost[ci];
            for (int k = 0; k < 8; k++)
            {
                int ddx = _dx[k], ddy = _dy[k];
                int nx = x + ddx, ny = y + ddy;
                if (nx < 0 || ny < 0 || nx >= W || ny >= H) continue;
                int ni = ny * W + nx;
                long nc = _cellCost[ni];
                if (nc == 0 || _closed[ni] == stamp) continue;
                long step;
                if (ddx != 0 && ddy != 0)
                {
                    // No corner cutting: both orthogonal neighbours must be passable.
                    if (_cellCost[y * W + nx] == 0 || _cellCost[ny * W + x] == 0) continue;
                    step = DiagStep(cc, nc);
                }
                else step = OrthStep(cc, nc);
                if (_climb is not null) step += _climb.ClimbCost(_cellHeight[ni] - _cellHeight[ci]);   // [TR]
                long ng = gc + step;
                if (_seen[ni] != stamp || ng < _g[ni])
                {
                    _seen[ni] = stamp; _g[ni] = ng; _parent[ni] = ci;
                    long nh = Heuristic(nx, ny, gx, gy);
                    Push(ng + nh, nh, ni);
                }
                else if (ng == _g[ni] && ci < _parent[ni])
                {
                    _parent[ni] = ci;                          // canonical tie-break; key unchanged, no push needed
                }
            }
        }
        return new PathResult(false, 0, Array.Empty<int>(), expanded);
    }

    private int[] Reconstruct(int gi)
    {
        int len = 0;
        for (int c = gi; c != -1; c = _parent[c]) len++;
        var path = new int[len];
        for (int c = gi, k = len - 1; c != -1; c = _parent[c], k--) path[k] = c;
        return path;
    }

    private static bool Less(long fa, long ha, int ia, long fb, long hb, int ib)
        => fa < fb || (fa == fb && (ha < hb || (ha == hb && ia < ib)));

    private void Push(long f, long h, int i)
    {
        if (_hn == _hf.Length)
        {
            Array.Resize(ref _hf, _hn * 2); Array.Resize(ref _hh, _hn * 2); Array.Resize(ref _hi, _hn * 2);
        }
        int k = _hn++;
        while (k > 0)
        {
            int p = (k - 1) >> 1;
            if (!Less(f, h, i, _hf[p], _hh[p], _hi[p])) break;
            _hf[k] = _hf[p]; _hh[k] = _hh[p]; _hi[k] = _hi[p];
            k = p;
        }
        _hf[k] = f; _hh[k] = h; _hi[k] = i;
    }

    private void Pop(out long f, out long h, out int i)
    {
        f = _hf[0]; h = _hh[0]; i = _hi[0];
        int n = --_hn;
        if (n == 0) return;
        long lf = _hf[n], lh = _hh[n]; int li = _hi[n];
        int k = 0;
        while (true)
        {
            int c = 2 * k + 1;
            if (c >= n) break;
            if (c + 1 < n && Less(_hf[c + 1], _hh[c + 1], _hi[c + 1], _hf[c], _hh[c], _hi[c])) c++;
            if (!Less(_hf[c], _hh[c], _hi[c], lf, lh, li)) break;
            _hf[k] = _hf[c]; _hh[k] = _hh[c]; _hi[k] = _hi[c];
            k = c;
        }
        _hf[k] = lf; _hh[k] = lh; _hi[k] = li;
    }
}
