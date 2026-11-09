using System.Diagnostics;
using Gfx.Engine.Diagnostics;
using Gfx.Engine.Rendering;

namespace Gfx.Perf.Measurement;

/// <summary>
/// Pass-level probe that wraps a scene as the <see cref="IRenderContent"/> handed to the engine's
/// <see cref="ForwardRenderer"/>, which the harness does not change. The renderer calls <c>DrawShadowCasters</c>
/// (shadow pass: once in the G1 frame, once per cascade in the G2 frame) and <c>DrawOpaque</c> (HDR pass, after
/// the sky); the tap forwards each call untouched and brackets it with GPU timestamps and CPU stopwatch readings. It is also the <see cref="IPerfMarkerSink"/> for named segments and counters that
/// instrumented (later G2) code may report. It draws nothing itself and changes no GL state.
/// </summary>
public sealed class ContentTap : IRenderContent, IPerfMarkerSink
{
    public const string MarkCastersBegin = "shadow.casters.begin", MarkCastersEnd = "shadow.casters.end";
    public const string MarkOpaqueBegin = "opaque.begin", MarkOpaqueEnd = "opaque.end";

    readonly GpuTimer? _gpu;
    readonly Dictionary<string, long> _segStart = new(StringComparer.Ordinal);
    readonly Dictionary<string, long> _segTicks = new(StringComparer.Ordinal);
    readonly Dictionary<string, long> _counters = new(StringComparer.Ordinal);

    public IRenderContent Inner { get; set; }
    public long CastersTicks { get; private set; }
    public long OpaqueTicks { get; private set; }
    /// <summary>Ticks of the FIRST content call of the frame (the G1 crowd prepares its instances there).</summary>
    public long FirstCallTicks { get; private set; }
    public int CastersCalls { get; private set; }
    public int OpaqueCalls { get; private set; }

    public ContentTap(IRenderContent inner, GpuTimer? gpu) { Inner = inner; _gpu = gpu; }

    public void ResetFrame()
    {
        CastersTicks = OpaqueTicks = FirstCallTicks = 0; CastersCalls = OpaqueCalls = 0;
        _segStart.Clear(); _segTicks.Clear(); _counters.Clear();
    }

    public void DrawShadowCasters(RenderContext rc)
    {
        _gpu?.Mark(MarkCastersBegin);
        long t0 = Stopwatch.GetTimestamp();
        Inner.DrawShadowCasters(rc);
        long dt = Stopwatch.GetTimestamp() - t0;
        if (CastersCalls == 0 && OpaqueCalls == 0) FirstCallTicks = dt;
        CastersTicks += dt; CastersCalls++;
        _gpu?.Mark(MarkCastersEnd);
    }

    public void DrawOpaque(RenderContext rc)
    {
        _gpu?.Mark(MarkOpaqueBegin);
        long t0 = Stopwatch.GetTimestamp();
        Inner.DrawOpaque(rc);
        long dt = Stopwatch.GetTimestamp() - t0;
        if (CastersCalls == 0 && OpaqueCalls == 0) FirstCallTicks = dt;
        OpaqueTicks += dt; OpaqueCalls++;
        _gpu?.Mark(MarkOpaqueEnd);
    }

    // ---- IPerfMarkerSink (named segments from instrumented code; G1 code reports none) ----
    public void BeginSegment(string name)
    {
        _gpu?.Mark("seg.begin:" + name);
        _segStart[name] = Stopwatch.GetTimestamp();
    }

    public void EndSegment(string name)
    {
        long now = Stopwatch.GetTimestamp();
        if (_segStart.Remove(name, out long t0))
            _segTicks[name] = (_segTicks.TryGetValue(name, out long acc) ? acc : 0) + (now - t0);
        _gpu?.Mark("seg.end:" + name);
    }

    public void AddCounter(string name, long value) => _counters[name] = (_counters.TryGetValue(name, out long v) ? v : 0) + value;

    /// <summary>This frame's named CPU segments (ms), sorted by name.</summary>
    public IReadOnlyList<(string name, double ms)> SegmentsMs()
    {
        var keys = _segTicks.Keys.ToArray(); Array.Sort(keys, StringComparer.Ordinal);
        return keys.Select(k => (k, _segTicks[k] * 1000.0 / Stopwatch.Frequency)).ToArray();
    }

    /// <summary>This frame's counters, sorted by name.</summary>
    public IReadOnlyList<(string name, long value)> Counters()
    {
        var keys = _counters.Keys.ToArray(); Array.Sort(keys, StringComparer.Ordinal);
        return keys.Select(k => (k, _counters[k])).ToArray();
    }
}
