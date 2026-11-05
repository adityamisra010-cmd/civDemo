using Proto.M7.Spatial;

namespace Proto.M7.Tactical;

/// <summary>
/// Exit-distance field for one side: multi-source integer Dijkstra from every PASSABLE cell on the side's exit edge, with
/// exactly the A* step costs and the no-corner-cutting rule of <see cref="GridPathfinder"/> (Slice C), so a descent step is
/// a step the planner would take. Unreachable = <see cref="Unreachable"/> (a Routing entity there has no path =&gt; Surrender).
/// Heap key (dist, cellIndex) is a total order; <see cref="NextCell"/> picks the neighbour with minimal (dist, cellIndex),
/// so descent is deterministic and independent of enumeration order. Terrain is static during a battle: built once.
/// </summary>
public sealed class EdgeField
{
    public const long Unreachable = long.MaxValue;
    private static readonly int[] Dx = { -1, 1, 0, 0, -1, 1, -1, 1 };
    private static readonly int[] Dy = { 0, 0, -1, 1, -1, -1, 1, 1 };

    private readonly TerrainGrid _grid;
    private readonly long[] _cost;
    private readonly long[] _dist;
    private readonly GridPathfinder _pf;
    public ExitEdge Edge { get; }

    public EdgeField(TerrainGrid grid, TerrainCostTable costs, ExitEdge edge)
    {
        _grid = grid; Edge = edge;
        int n = grid.CellCount, W = grid.Width, H = grid.Height;
        _cost = new long[n];
        for (int i = 0; i < n; i++) _cost[i] = costs.Cost(grid.ClassAtIndex(i));
        _dist = new long[n];
        Array.Fill(_dist, Unreachable);
        var pf = new GridPathfinder(grid, costs);
        _pf = pf;
        var hd = new List<long>(); var hi = new List<int>();
        for (int i = 0; i < n; i++)
            if (_cost[i] != 0 && OnEdge(i % W, i / W)) { _dist[i] = 0; Push(hd, hi, 0, i); }
        while (hd.Count > 0)
        {
            Pop(hd, hi, out long d, out int c);
            if (d != _dist[c]) continue;
            int x = c % W, y = c / W;
            for (int k = 0; k < 8; k++)
            {
                int nx = x + Dx[k], ny = y + Dy[k];
                if (nx < 0 || ny < 0 || nx >= W || ny >= H) continue;
                int ni = ny * W + nx;
                if (_cost[ni] == 0) continue;
                long step;
                if (Dx[k] != 0 && Dy[k] != 0)
                {
                    if (_cost[y * W + nx] == 0 || _cost[ny * W + x] == 0) continue;
                    step = pf.DiagStep(_cost[c], _cost[ni]);
                }
                else step = pf.OrthStep(_cost[c], _cost[ni]);
                long nd = d + step;
                if (nd < _dist[ni]) { _dist[ni] = nd; Push(hd, hi, nd, ni); }
            }
        }
    }

    public bool OnEdge(long cx, long cy) => Edge switch
    {
        ExitEdge.West => cx == 0,
        ExitEdge.East => cx == _grid.Width - 1,
        ExitEdge.North => cy == 0,
        _ => cy == _grid.Height - 1,
    };

    public long DistanceAt(int cellIndex) => _dist[cellIndex];

    /// <summary>Distance in mm from a point to the exit edge LINE (the inactivity rule's exit reference).</summary>
    public long LineDistanceMm(Vec2L p) => Edge switch
    {
        ExitEdge.West => Math.Max(0, p.X),
        ExitEdge.East => Math.Max(0, _grid.Width * _grid.CellSizeMm - 1 - p.X),
        ExitEdge.North => Math.Max(0, p.Y),
        _ => Math.Max(0, _grid.Height * _grid.CellSizeMm - 1 - p.Y),
    };

    /// <summary>
    /// The descent neighbour of a reachable, non-edge cell, or -1: among legal steps to a neighbour n with
    /// dist[n] &lt; dist[c], the minimal composite key (dist[n] + step(c, n), n) — the first step of a shortest path, ties
    /// broken by the lower cell index (CLAUDE.md stable integer tie-break).
    /// </summary>
    public int NextCell(int c)
    {
        long dc = _dist[c];
        if (dc == Unreachable || dc == 0) return -1;
        int W = _grid.Width, H = _grid.Height, x = c % W, y = c / W;
        int best = -1; long bk = 0;
        for (int k = 0; k < 8; k++)
        {
            int nx = x + Dx[k], ny = y + Dy[k];
            if (nx < 0 || ny < 0 || nx >= W || ny >= H) continue;
            int ni = ny * W + nx;
            if (_cost[ni] == 0 || _dist[ni] >= dc) continue;
            bool diag = Dx[k] != 0 && Dy[k] != 0;
            if (diag && (_cost[y * W + nx] == 0 || _cost[ny * W + x] == 0)) continue;
            long key = _dist[ni] + (diag ? _pf.DiagStep(_cost[c], _cost[ni]) : _pf.OrthStep(_cost[c], _cost[ni]));
            if (best < 0 || key < bk || (key == bk && ni < best)) { bk = key; best = ni; }
        }
        return best;
    }

    private static bool Less(long da, int ia, long db, int ib) => da < db || (da == db && ia < ib);

    private static void Push(List<long> hd, List<int> hi, long d, int i)
    {
        hd.Add(d); hi.Add(i);
        int k = hd.Count - 1;
        while (k > 0)
        {
            int p = (k - 1) >> 1;
            if (!Less(d, i, hd[p], hi[p])) break;
            hd[k] = hd[p]; hi[k] = hi[p]; k = p;
        }
        hd[k] = d; hi[k] = i;
    }

    private static void Pop(List<long> hd, List<int> hi, out long d, out int i)
    {
        d = hd[0]; i = hi[0];
        int n = hd.Count - 1;
        long ld = hd[n]; int li = hi[n];
        hd.RemoveAt(n); hi.RemoveAt(n);
        if (n == 0) return;
        int k = 0;
        while (true)
        {
            int c = 2 * k + 1;
            if (c >= n) break;
            if (c + 1 < n && Less(hd[c + 1], hi[c + 1], hd[c], hi[c])) c++;
            if (!Less(hd[c], hi[c], ld, li)) break;
            hd[k] = hd[c]; hi[k] = hi[c]; k = c;
        }
        hd[k] = ld; hi[k] = li;
    }
}
