namespace Sim.Core.Worldgen;

/// <summary>
/// T1.2 hydrology: D8 flow over elevation → discharge accumulation → top-K river
/// polylines. Runs INSIDE worldgen, before the TerrainSet hash — one immutable
/// artifact, one hash.
///
/// SINK HANDLING (documented choice): priority-flood depression filling
/// (Barnes-2014 class) over a ROUTING COPY of elevation — the TerrainSet
/// elevation layer is never modified. The flood seeds from all water cells and
/// all map-border cells and raises every interior cell to at least its spill
/// elevation plus a tiny monotonicity epsilon, so every land cell has a strictly
/// descending D8 path to water or to the map border (border sinks are terminal
/// mouths — the "documented sink resolution" of the acceptance test).
///
/// DETERMINISM: every ordering is by explicit stable rule, never iteration
/// accident (the T1.3 mandate's precedent):
///  · priority queue pops by (filled elevation, then node id) — total order;
///  · neighbors visit in the fixed 8-offset order below;
///  · D8 direction ties break to the lowest neighbor id;
///  · accumulation processes cells sorted by (filled desc, id asc);
///  · mouths rank by (discharge desc, id asc); upstream tracing picks
///    (max discharge, then lowest id).
/// </summary>
public static class Hydrology
{
    /// <summary>Monotonicity epsilon added per fill step on the routing copy.</summary>
    private const double FillEpsilon = 1e-9;

    // Fixed neighbor order: W, E, N, S, NW, NE, SW, SE (cardinals first).
    private static readonly int[] Dx = [-1, 1, 0, 0, -1, 1, -1, 1];
    private static readonly int[] Dy = [0, 0, -1, 1, -1, -1, 1, 1];
    private static readonly double[] Dist = [1, 1, 1, 1, 1.4142135623730951, 1.4142135623730951, 1.4142135623730951, 1.4142135623730951];

    public sealed class Result
    {
        /// <summary>1.0 on river cells, else 0.0 (land only).</summary>
        public required double[] RiverMask { get; init; }

        /// <summary>Top-K polylines, each head→mouth as cell indices.</summary>
        public required int[][] Polylines { get; init; }

        /// <summary>D8 target per cell (-1 = terminal: water cell or border sink).</summary>
        public required int[] FlowTarget { get; init; }

        /// <summary>Cells drained through each cell (own cell included), land only.</summary>
        public required long[] Accumulation { get; init; }

        /// <summary>The hydrologically conditioned routing elevations (for tests; NOT terrain).</summary>
        public required double[] FilledElevation { get; init; }
    }

    public static Result Compute(double[] elevation, double[] water, int size, RiversConfig cfg)
    {
        int cells = size * size;
        double[] filled = PriorityFlood(elevation, water, size);
        int[] flowTarget = FlowTargets(filled, water, size);
        long[] accumulation = Accumulate(filled, water, flowTarget, size);
        int[][] polylines = TracePolylines(water, flowTarget, accumulation, size, cfg);

        var mask = new double[cells];
        foreach (int[] line in polylines)
            foreach (int cell in line)
                mask[cell] = 1.0;

        return new Result
        {
            RiverMask = mask,
            Polylines = polylines,
            FlowTarget = flowTarget,
            Accumulation = accumulation,
            FilledElevation = filled,
        };
    }

    // --- priority-flood fill ---------------------------------------------------

    private static double[] PriorityFlood(double[] elevation, double[] water, int size)
    {
        int cells = size * size;
        var filled = new double[cells];
        var visited = new bool[cells];
        var heap = new MinHeap(cells);

        // Seeds: all water cells and all border cells, in row-major (id) order.
        for (int i = 0; i < cells; i++)
        {
            int x = i % size, y = i / size;
            bool border = x == 0 || y == 0 || x == size - 1 || y == size - 1;
            if (water[i] >= 0.5 || border)
            {
                filled[i] = elevation[i];
                visited[i] = true;
                heap.Push(elevation[i], i);
            }
        }

        while (heap.Count > 0)
        {
            (double level, int i) = heap.Pop();
            int x = i % size, y = i / size;
            for (int d = 0; d < 8; d++)
            {
                int nx = x + Dx[d], ny = y + Dy[d];
                if (nx < 0 || ny < 0 || nx >= size || ny >= size) continue;
                int n = ny * size + nx;
                if (visited[n]) continue;
                visited[n] = true;
                // Raise to spill level + epsilon so descent is strict on land.
                filled[n] = elevation[n] > level ? elevation[n] : level + FillEpsilon;
                heap.Push(filled[n], n);
            }
        }
        return filled;
    }

    // --- D8 direction over the conditioned surface -----------------------------

    private static int[] FlowTargets(double[] filled, double[] water, int size)
    {
        int cells = size * size;
        var target = new int[cells];
        for (int i = 0; i < cells; i++)
        {
            if (water[i] >= 0.5) { target[i] = -1; continue; } // water is terminal

            int x = i % size, y = i / size;
            double bestGradient = 0.0;
            int best = -1;
            for (int d = 0; d < 8; d++)
            {
                int nx = x + Dx[d], ny = y + Dy[d];
                if (nx < 0 || ny < 0 || nx >= size || ny >= size) continue;
                int n = ny * size + nx;
                double gradient = (filled[i] - filled[n]) / Dist[d];
                // Strictly steeper wins; equal-gradient ties go to the LOWER id.
                if (gradient > bestGradient || (gradient == bestGradient && gradient > 0.0 && n < best))
                {
                    bestGradient = gradient;
                    best = n;
                }
            }
            // best == -1 only for border land cells that are their own spill
            // (documented terminal border sink).
            target[i] = best;
        }
        return target;
    }

    // --- accumulation ----------------------------------------------------------

    private static long[] Accumulate(double[] filled, double[] water, int[] flowTarget, int size)
    {
        int cells = size * size;
        var acc = new long[cells];
        var order = new int[cells];
        for (int i = 0; i < cells; i++) order[i] = i;

        // Highest conditioned elevation first; ties by ascending id — total order.
        Array.Sort(order, (a, b) =>
        {
            int byElev = filled[b].CompareTo(filled[a]);
            return byElev != 0 ? byElev : a.CompareTo(b);
        });

        for (int k = 0; k < cells; k++)
        {
            int i = order[k];
            if (water[i] >= 0.5) continue;
            acc[i] += 1; // the cell's own unit of drainage
            int t = flowTarget[i];
            if (t >= 0 && water[t] < 0.5) acc[t] += acc[i];
        }
        return acc;
    }

    // --- river extraction ------------------------------------------------------

    private static int[][] TracePolylines(
        double[] water, int[] flowTarget, long[] accumulation, int size, RiversConfig cfg)
    {
        int cells = size * size;
        long minAcc = (long)Math.Ceiling(cfg.MinAccumulationFraction * cells);
        if (minAcc < 1) minAcc = 1;

        // Mouths: land cells whose flow target is water or a terminal border sink.
        var mouths = new List<int>();
        for (int i = 0; i < cells; i++)
        {
            if (water[i] >= 0.5) continue;
            int t = flowTarget[i];
            if ((t == -1 || water[t] >= 0.5) && accumulation[i] >= minAcc) mouths.Add(i);
        }
        // Rank by (discharge desc, id asc); take the top K.
        mouths.Sort((a, b) =>
        {
            int byAcc = accumulation[b].CompareTo(accumulation[a]);
            return byAcc != 0 ? byAcc : a.CompareTo(b);
        });
        int count = Math.Min(cfg.Count, mouths.Count);

        // Upstream adjacency: for each cell, its inflow neighbors (built in id order).
        var inflowHead = new int[cells];
        var inflowNext = new int[cells];
        Array.Fill(inflowHead, -1);
        for (int i = cells - 1; i >= 0; i--)   // descending id → lists come out id-ascending
        {
            int t = flowTarget[i];
            if (t >= 0 && water[t] < 0.5)
            {
                inflowNext[i] = inflowHead[t];
                inflowHead[t] = i;
            }
        }

        var polylines = new int[count][];
        for (int m = 0; m < count; m++)
        {
            var line = new List<int>();
            int cell = mouths[m];
            while (true)
            {
                line.Add(cell);
                // Main stem continues up the (max discharge, lowest id) tributary.
                int bestUp = -1;
                long bestAcc = 0;
                for (int up = inflowHead[cell]; up != -1; up = inflowNext[up])
                {
                    if (accumulation[up] > bestAcc) { bestAcc = accumulation[up]; bestUp = up; }
                    // equal accumulation keeps the earlier (lower-id) candidate:
                    // lists are id-ascending, and only strictly-greater replaces.
                }
                if (bestUp == -1 || accumulation[bestUp] < minAcc) break;
                cell = bestUp;
            }
            line.Reverse(); // head → mouth
            polylines[m] = [.. line];
        }
        return polylines;
    }

    // --- deterministic binary min-heap on (key, id) ----------------------------

    private sealed class MinHeap(int capacity)
    {
        private double[] _keys = new double[capacity];
        private int[] _ids = new int[capacity];
        public int Count { get; private set; }

        public void Push(double key, int id)
        {
            if (Count == _keys.Length)
            {
                Array.Resize(ref _keys, _keys.Length * 2);
                Array.Resize(ref _ids, _ids.Length * 2);
            }
            int c = Count++;
            _keys[c] = key; _ids[c] = id;
            while (c > 0)
            {
                int parent = (c - 1) / 2;
                if (!Less(c, parent)) break;
                Swap(c, parent);
                c = parent;
            }
        }

        public (double Key, int Id) Pop()
        {
            (double key, int id) = (_keys[0], _ids[0]);
            Count--;
            _keys[0] = _keys[Count]; _ids[0] = _ids[Count];
            int c = 0;
            while (true)
            {
                int l = 2 * c + 1, r = l + 1, smallest = c;
                if (l < Count && Less(l, smallest)) smallest = l;
                if (r < Count && Less(r, smallest)) smallest = r;
                if (smallest == c) break;
                Swap(c, smallest);
                c = smallest;
            }
            return (key, id);
        }

        // Total order: (key, id) — no ties left to heap layout.
        private bool Less(int a, int b) =>
            _keys[a] < _keys[b] || (_keys[a] == _keys[b] && _ids[a] < _ids[b]);

        private void Swap(int a, int b)
        {
            (_keys[a], _keys[b]) = (_keys[b], _keys[a]);
            (_ids[a], _ids[b]) = (_ids[b], _ids[a]);
        }
    }
}
