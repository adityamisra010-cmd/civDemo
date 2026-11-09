namespace Gfx.Perf.Analysis;

/// <summary>Distribution summary of one metric over the measured frames (NaN values are excluded).</summary>
public sealed record Dist(int Count, double P50, double P95, double P99, double Mean, double Min, double Max, double StdDev)
{
    public static readonly Dist Empty = new(0, double.NaN, double.NaN, double.NaN, double.NaN, double.NaN, double.NaN, double.NaN);
}

public static class Stats
{
    /// <summary>
    /// Nearest-rank percentile (the same definition as G1's FrameStats.Percentile): the smallest value such that at
    /// least p % of the samples are ≤ it. <paramref name="sorted"/> must be ascending and non-empty.
    /// </summary>
    public static double NearestRank(double[] sorted, double p)
    {
        int rank = (int)Math.Ceiling(p / 100.0 * sorted.Length);
        return sorted[Math.Clamp(rank - 1, 0, sorted.Length - 1)];
    }

    public static Dist Of(IEnumerable<double> values)
    {
        var a = values.Where(v => !double.IsNaN(v)).ToArray();
        if (a.Length == 0) return Dist.Empty;
        Array.Sort(a);
        double mean = 0; foreach (var v in a) mean += v; mean /= a.Length;
        double ss = 0; foreach (var v in a) ss += (v - mean) * (v - mean);
        return new Dist(a.Length, NearestRank(a, 50), NearestRank(a, 95), NearestRank(a, 99), mean, a[0], a[^1], a.Length > 1 ? Math.Sqrt(ss / (a.Length - 1)) : 0);
    }

    /// <summary>Median of a small set (e.g. the repetitions of one configuration); NaN when empty.</summary>
    public static double Median(IEnumerable<double> values)
    {
        var a = values.Where(v => !double.IsNaN(v)).ToArray();
        if (a.Length == 0) return double.NaN;
        Array.Sort(a);
        return a.Length % 2 == 1 ? a[a.Length / 2] : (a[a.Length / 2 - 1] + a[a.Length / 2]) / 2;
    }

    /// <summary>Frame-time histogram edges (ms): FPS bands ≥240, ≥144, ≥120, ≥90, ≥60, ≥30, ≥20, ≥10, &lt;10.</summary>
    public static readonly double[] HistogramEdgesMs = { 1000.0 / 240, 1000.0 / 144, 1000.0 / 120, 1000.0 / 90, 1000.0 / 60, 1000.0 / 30, 1000.0 / 20, 1000.0 / 10 };
    public static readonly string[] HistogramLabels = { ">=240fps", "144-240fps", "120-144fps", "90-120fps", "60-90fps", "30-60fps", "20-30fps", "10-20fps", "<10fps" };

    /// <summary>Counts of frame times per FPS band (a frame exactly on an edge belongs to the slower band).</summary>
    public static int[] Histogram(IEnumerable<double> frameMs)
    {
        var h = new int[HistogramEdgesMs.Length + 1];
        foreach (var v in frameMs)
        {
            if (double.IsNaN(v)) continue;
            int b = 0;
            while (b < HistogramEdgesMs.Length && v >= HistogramEdgesMs[b]) b++;
            h[b]++;
        }
        return h;
    }
}
