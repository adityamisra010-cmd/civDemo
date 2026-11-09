using System.Diagnostics;
using System.Globalization;

namespace Gfx.Engine.Diagnostics;

/// <summary>
/// CPU frame time (Stopwatch around Begin/End; when the caller synchronises with the GPU inside the frame — e.g. a
/// readback — it includes GPU time) plus per-frame draw calls, triangles and instances. Percentiles: nearest rank.
/// </summary>
public sealed class FrameStats
{
    readonly Stopwatch _sw = new();
    readonly List<double> _frameMs = new();
    public int DrawCalls { get; private set; }
    public long Triangles { get; private set; }
    public long Instances { get; private set; }
    public int LastDrawCalls { get; private set; }
    public long LastTriangles { get; private set; }
    public long LastInstances { get; private set; }
    public IReadOnlyList<double> FrameMs => _frameMs;

    public void BeginFrame() { DrawCalls = 0; Triangles = 0; Instances = 0; _sw.Restart(); }
    public void RecordDraw(long triangles, long instances) { DrawCalls++; Triangles += triangles; Instances += instances; }
    public double EndFrame()
    {
        _sw.Stop(); double ms = _sw.Elapsed.TotalMilliseconds; _frameMs.Add(ms);
        LastDrawCalls = DrawCalls; LastTriangles = Triangles; LastInstances = Instances;
        return ms;
    }
    public void Reset() => _frameMs.Clear();

    public static double Percentile(IReadOnlyList<double> values, double p)
    {
        if (values.Count == 0) return double.NaN;
        var s = values.ToArray(); Array.Sort(s);
        int rank = (int)Math.Ceiling(p / 100.0 * s.Length);
        return s[Math.Clamp(rank - 1, 0, s.Length - 1)];
    }

    public (double p50, double p95, double p99, double mean) Summary()
        => (Percentile(_frameMs, 50), Percentile(_frameMs, 95), Percentile(_frameMs, 99), _frameMs.Count == 0 ? double.NaN : _frameMs.Average());

    /// <summary>One line; <paramref name="label"/> should state the renderer, e.g. "SOFTWARE RENDERING (llvmpipe)".</summary>
    public string Format(string label)
    {
        var (p50, p95, p99, mean) = Summary();
        return string.Format(CultureInfo.InvariantCulture,
            "[{0}] frames={1} ms p50={2:F2} p95={3:F2} p99={4:F2} mean={5:F2} | draws={6} tris={7} instances={8}",
            label, _frameMs.Count, p50, p95, p99, mean, LastDrawCalls, LastTriangles, LastInstances);
    }
}
