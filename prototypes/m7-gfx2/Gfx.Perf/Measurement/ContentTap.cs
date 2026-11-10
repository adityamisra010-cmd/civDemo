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
/// <para>
/// Stage env's optional passes are found by the renderer on the content it is handed, so the tap implements
/// <see cref="ITransparentContent"/> and <see cref="IPlanarReflectionContent"/> and forwards both to <see cref="Inner"/>
/// (inert when the inner content has neither: no draw, no marker). Env scenes draw all their water in the transparent
/// pass; without the forwarding a tapped env frame had no water and no reflection pass. The mirrored opaque redraw of the
/// planar reflection and the transparent pass are the named segments <see cref="SegReflection"/> and
/// <see cref="SegTransparent"/>, so the opaque markers and counts stay those of the main scene pass. In the GPU pass
/// split the reflection pass (its clear, sky and redraw) therefore falls in the "clear_sky" span and the scene-colour
/// copy and water in the "post" span; the two segments give the redraw and the water on their own.
/// </para>
/// </summary>
public sealed class ContentTap : IRenderContent, ITransparentContent, IPlanarReflectionContent, IPerfMarkerSink
{
    public const string MarkCastersBegin = "shadow.casters.begin", MarkCastersEnd = "shadow.casters.end";
    public const string MarkOpaqueBegin = "opaque.begin", MarkOpaqueEnd = "opaque.end";
    public const string SegReflection = "reflection", SegTransparent = "transparent";

    readonly GpuTimer? _gpu;
    readonly Dictionary<string, long> _segStart = new(StringComparer.Ordinal);
    readonly Dictionary<string, long> _segTicks = new(StringComparer.Ordinal);
    readonly Dictionary<string, long> _counters = new(StringComparer.Ordinal);
    int _reflectionCalls;

    public IRenderContent Inner { get; set; }
    public long CastersTicks { get; private set; }
    public long OpaqueTicks { get; private set; }
    /// <summary>Ticks of the FIRST content call of the frame (the G1 crowd prepares its instances there).</summary>
    public long FirstCallTicks { get; private set; }
    public int CastersCalls { get; private set; }
    /// <summary>Main scene pass calls only (the planar-reflection redraw is the named segment <see cref="SegReflection"/>).</summary>
    public int OpaqueCalls { get; private set; }

    public ContentTap(IRenderContent inner, GpuTimer? gpu) { Inner = inner; _gpu = gpu; }

    public void ResetFrame()
    {
        CastersTicks = OpaqueTicks = FirstCallTicks = 0; CastersCalls = OpaqueCalls = 0; _reflectionCalls = 0;
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
        bool first = CastersCalls == 0 && OpaqueCalls == 0 && _reflectionCalls == 0;
        if (rc.IsReflectionPass)
        {
            // stage env: the planar reflection redraws the opaque content mirrored, before the main scene pass
            BeginSegment(SegReflection);
            long r0 = Stopwatch.GetTimestamp();
            Inner.DrawOpaque(rc);
            if (first) FirstCallTicks = Stopwatch.GetTimestamp() - r0;
            _reflectionCalls++;
            EndSegment(SegReflection);
            return;
        }
        _gpu?.Mark(MarkOpaqueBegin);
        long t0 = Stopwatch.GetTimestamp();
        Inner.DrawOpaque(rc);
        long dt = Stopwatch.GetTimestamp() - t0;
        if (first) FirstCallTicks = dt;
        OpaqueTicks += dt; OpaqueCalls++;
        _gpu?.Mark(MarkOpaqueEnd);
    }

    // ---- stage env's optional passes, forwarded so a tapped frame draws what the untapped frame draws ----
    public bool NeedsSceneColor(RenderContext rc) => Inner is ITransparentContent t && t.NeedsSceneColor(rc);

    public void DrawTransparent(RenderContext rc)
    {
        if (Inner is not ITransparentContent t) return;
        BeginSegment(SegTransparent);
        t.DrawTransparent(rc);
        EndSegment(SegTransparent);
    }

    public Microsoft.Xna.Framework.Vector4? ReflectionPlane(RenderContext rc) => Inner is IPlanarReflectionContent p ? p.ReflectionPlane(rc) : null;
    public float ReflectionScale => Inner is IPlanarReflectionContent p ? p.ReflectionScale : 1f;

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
