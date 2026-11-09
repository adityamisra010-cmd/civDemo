namespace Gfx.Engine.Diagnostics;

/// <summary>
/// Receiver of named presentation-side performance markers. The perf harness (Gfx.Perf) installs one on the GL
/// thread while it measures; otherwise there is none and every <see cref="PerfMarkers"/> call is a no-op.
/// PRESENTATION ONLY: markers observe rendering, they never feed anything back into it or into the simulation.
/// </summary>
public interface IPerfMarkerSink
{
    /// <summary>Start of a named segment (the harness records a GPU timestamp and a CPU timestamp).</summary>
    void BeginSegment(string name);
    /// <summary>End of the segment opened with the same name.</summary>
    void EndSegment(string name);
    /// <summary>A per-frame counter (summed when reported several times in one frame), e.g. "soldiers.lod0".</summary>
    void AddCounter(string name, long value);
}

/// <summary>
/// Implemented by scenes or presentation components that expose LOD distances, so the perf harness can run its
/// LOD-distance sweep (×0.25 … ×4) on them. Returns a one-line description of what changed. Presentation only.
/// </summary>
public interface IPerfLodScalable
{
    string ScaleLodDistances(float multiplier);
}

/// <summary>
/// Optional instrumentation hook for renderer and scene code: <c>using (PerfMarkers.Segment("terrain")) { … }</c>
/// and <c>PerfMarkers.Counter("impostors", n)</c>. The G1 renderer and scenes do not call it (they are measured
/// unchanged as the BEFORE baseline); later G2 code may, so the harness can split passes into named segments.
/// The sink is per thread (the GL thread that installed it), so tests on other threads never see it.
/// </summary>
public static class PerfMarkers
{
    [ThreadStatic] static IPerfMarkerSink? _sink;

    /// <summary>The sink for the calling thread (null = markers are no-ops). Set by the perf harness only.</summary>
    public static IPerfMarkerSink? Sink { get => _sink; set => _sink = value; }

    public static SegmentScope Segment(string name)
    {
        var s = _sink;
        s?.BeginSegment(name);
        return new SegmentScope(s, name);
    }

    public static void Counter(string name, long value) => _sink?.AddCounter(name, value);

    public readonly struct SegmentScope : IDisposable
    {
        readonly IPerfMarkerSink? _s; readonly string _name;
        internal SegmentScope(IPerfMarkerSink? s, string name) { _s = s; _name = name; }
        public void Dispose() => _s?.EndSegment(_name);
    }
}
