using Gfx.Perf.Analysis;
using Gfx.Perf.Measurement;
using Gfx.Perf.Scenes;

namespace Gfx.Perf.Runtime;

/// <summary>One measured frame. Times in ms; GPU fields are NaN until (unless) the timestamps came back.</summary>
public struct FrameRecord
{
    public long FrameId;
    public double IntervalMs, UpdateMs, RenderMs, CastersMs, OpaqueMs, FirstCallMs, BlitMs, PresentMs, HarnessMs;
    public double GpuMs, GpuUploadMs, GpuShadowClearMs, GpuCastersMs, GpuSkyMs, GpuOpaqueMs, GpuPostMs, GpuBlitMs;
    public bool GpuReceived, GpuMonotonic;
    public int Draws; public long Tris, Instances;
    public SceneCounters Counters;
    public bool? Active;
    /// <summary>CPU work for the frame: scene update + render submission + preview blit (present wait excluded).</summary>
    public readonly double CpuMs => UpdateMs + RenderMs + BlitMs;

    public static FrameRecord New(long id) => new()
    {
        FrameId = id, GpuMs = double.NaN, GpuUploadMs = double.NaN, GpuShadowClearMs = double.NaN, GpuCastersMs = double.NaN,
        GpuSkyMs = double.NaN, GpuOpaqueMs = double.NaN, GpuPostMs = double.NaN, GpuBlitMs = double.NaN, Counters = SceneCounters.None,
    };

    /// <summary>Fills the GPU fields from the frame's timestamp markers (see <see cref="PerfSession"/> for the marker order).</summary>
    public void ApplyGpu(GpuFrame g, Dictionary<string, List<double>>? namedGpu)
    {
        GpuReceived = true; GpuMonotonic = g.Monotonic;
        if (!g.Monotonic) return;
        static double Ms(long ns) => ns / 1e6;
        long upd = g.At(PerfSession.MarkUpdateEnd), cb = g.At(ContentTap.MarkCastersBegin), ce = g.At(ContentTap.MarkCastersEnd);
        long ob = g.At(ContentTap.MarkOpaqueBegin), oe = g.At(ContentTap.MarkOpaqueEnd), re = g.At(PerfSession.MarkRenderEnd), be = g.At(PerfSession.MarkBlitEnd);
        if (upd < 0 || ob < 0 || oe < 0 || re < 0) return;
        long end = be >= 0 ? be : re;
        GpuMs = Ms(end); GpuUploadMs = Ms(upd);
        if (cb >= 0 && ce >= 0) { GpuShadowClearMs = Ms(cb - upd); GpuCastersMs = Ms(ce - cb); GpuSkyMs = Ms(ob - ce); }
        else { GpuShadowClearMs = 0; GpuCastersMs = 0; GpuSkyMs = Ms(ob - upd); }
        GpuOpaqueMs = Ms(oe - ob); GpuPostMs = Ms(re - oe); GpuBlitMs = be >= 0 ? Ms(be - re) : double.NaN;
        if (namedGpu == null) return;
        for (int i = 0; i < g.Names.Length; i++)
        {
            if (!g.Names[i].StartsWith("seg.begin:", StringComparison.Ordinal)) continue;
            string name = g.Names[i][10..];
            for (int j = i + 1; j < g.Names.Length; j++)
                if (g.Names[j] == "seg.end:" + name)
                {
                    if (!namedGpu.TryGetValue(name, out var l)) namedGpu[name] = l = new List<double>();
                    l.Add(Ms(g.TimesNs[j] - g.TimesNs[i]));
                    break;
                }
        }
    }
}

/// <summary>NVML summary over a run's measured window (NaN / -1 when unavailable).</summary>
public sealed record NvmlSummary(int Samples, double GfxClockMean, int GfxClockMin, int GfxClockMax, int MemClockMax, int TempMax,
    double PowerMeanW, double PowerMaxW, double PowerLimitW, double GpuUtilMean, long MemUsedMax, long MemTotal, string PStates,
    ulong ThrottleUnion, double ThrottleFraction)
{
    public static NvmlSummary From(NvmlSample[] s)
    {
        if (s.Length == 0) return new NvmlSummary(0, double.NaN, -1, -1, -1, -1, double.NaN, double.NaN, double.NaN, double.NaN, -1, -1, "", 0, double.NaN);
        double Mean(IEnumerable<double> v) { var a = v.Where(x => !double.IsNaN(x) && x >= 0).ToArray(); return a.Length == 0 ? double.NaN : a.Average(); }
        int Max(IEnumerable<int> v) { var a = v.Where(x => x >= 0).ToArray(); return a.Length == 0 ? -1 : a.Max(); }
        int Min(IEnumerable<int> v) { var a = v.Where(x => x >= 0).ToArray(); return a.Length == 0 ? -1 : a.Min(); }
        ulong u = 0; int thr = 0, known = 0;
        foreach (var x in s) if (x.ThrottleKnown) { known++; u |= x.ThrottleReasons; if ((x.ThrottleReasons & Nvml.ThermalOrPowerMask) != 0) thr++; }
        var ps = s.Where(x => x.PState >= 0).Select(x => x.PState).Distinct().OrderBy(x => x).Select(x => "P" + x);
        return new NvmlSummary(s.Length, Mean(s.Select(x => (double)x.GfxClockMHz)), Min(s.Select(x => x.GfxClockMHz)), Max(s.Select(x => x.GfxClockMHz)),
            Max(s.Select(x => x.MemClockMHz)), Max(s.Select(x => x.TempC)), Mean(s.Select(x => x.PowerW)),
            s.Select(x => x.PowerW).Where(x => !double.IsNaN(x)).DefaultIfEmpty(double.NaN).Max(), s.Select(x => x.PowerLimitW).Where(x => !double.IsNaN(x)).DefaultIfEmpty(double.NaN).Max(),
            Mean(s.Select(x => (double)x.GpuUtilPct)), s.Select(x => x.MemUsedBytes).DefaultIfEmpty(-1).Max(), s.Select(x => x.MemTotalBytes).DefaultIfEmpty(-1).Max(),
            string.Join('/', ps), u, known == 0 ? double.NaN : thr / (double)known);
    }
}

/// <summary>GL_NVX_gpu_memory_info / GL_ATI_meminfo readings over a run (KiB; -1 = not reported). Device-wide, not per process.</summary>
public sealed record VramSummary(string Source, long DedicatedKb, long TotalAvailableKb, long CurrentAvailableStartKb, long CurrentAvailableMinKb,
    long EvictionsDelta, long EvictedKbDelta, long AtiTextureFreeMinKb, long AtiVboFreeMinKb)
{
    /// <summary>Device-wide VRAM in use at the low point (dedicated − min available), MiB; NaN when not reported.</summary>
    public double UsedMaxMb => DedicatedKb > 0 && CurrentAvailableMinKb >= 0 ? (DedicatedKb - CurrentAvailableMinKb) / 1024.0 : double.NaN;
}

public sealed class RunResult
{
    public required int RunIndex { get; init; }
    public required int Round { get; init; }
    public required RunConfig Config { get; init; }
    public required string SceneSpec { get; init; }
    public string SceneType { get; set; } = "?";
    public required string StandInGap { get; init; }
    public string PathDescription { get; set; } = "";
    public string Status { get; set; } = "ok";
    public double LoadMs { get; set; } = double.NaN;
    public double FirstFrameMs { get; set; } = double.NaN;
    public IReadOnlyList<string> Knobs { get; set; } = Array.Empty<string>();
    public bool ShadowsOn { get; set; }
    public int ShadowMapSize { get; set; }
    public string HdrFormat { get; set; } = "";
    public FrameRecord[] Frames { get; set; } = Array.Empty<FrameRecord>();
    public int WarmupFrames { get; set; }

    public Dist FrameMs { get; set; } = Dist.Empty;
    public Dist CpuMs { get; set; } = Dist.Empty;
    public Dist GpuMs { get; set; } = Dist.Empty;
    public Dist UpdateMs { get; set; } = Dist.Empty;
    public Dist RenderMs { get; set; } = Dist.Empty;
    public Dist CastersCpuMs { get; set; } = Dist.Empty;
    public Dist OpaqueCpuMs { get; set; } = Dist.Empty;
    public Dist FirstCallCpuMs { get; set; } = Dist.Empty;
    public Dist BlitCpuMs { get; set; } = Dist.Empty;
    public Dist PresentMs { get; set; } = Dist.Empty;
    public Dist HarnessMs { get; set; } = Dist.Empty;
    public Dist GpuUploadMs { get; set; } = Dist.Empty;
    public Dist GpuShadowClearMs { get; set; } = Dist.Empty;
    public Dist GpuCastersMs { get; set; } = Dist.Empty;
    public Dist GpuSkyMs { get; set; } = Dist.Empty;
    public Dist GpuOpaqueMs { get; set; } = Dist.Empty;
    public Dist GpuPostMs { get; set; } = Dist.Empty;
    public Dist GpuBlitMs { get; set; } = Dist.Empty;
    public SortedDictionary<string, Dist> NamedCpuSegments { get; } = new(StringComparer.Ordinal);
    public SortedDictionary<string, Dist> NamedGpuSegments { get; } = new(StringComparer.Ordinal);
    public SortedDictionary<string, double> NamedCountersMean { get; } = new(StringComparer.Ordinal);
    public int[] Histogram { get; set; } = Array.Empty<int>();

    public double DrawsMean { get; set; } = double.NaN;
    public int DrawsMax { get; set; }
    public double TrisMean { get; set; } = double.NaN;
    public long TrisMax { get; set; }
    public double InstancesMean { get; set; } = double.NaN;
    public long InstancesMax { get; set; }
    public double Lod0Mean { get; set; } = double.NaN;
    public double Lod1Mean { get; set; } = double.NaN;
    public double Lod2Mean { get; set; } = double.NaN;
    public double CulledMean { get; set; } = double.NaN;
    public double ChunksMean { get; set; } = double.NaN;
    public double PropsMean { get; set; } = double.NaN;

    public SystemProbes.MemSample MemStart { get; set; }
    public SystemProbes.MemSample MemEnd { get; set; }
    public long WorkingSetPeak { get; set; }
    public long GcHeapPeak { get; set; }
    public int Gen0 { get; set; }
    public int Gen1 { get; set; }
    public int Gen2 { get; set; }
    public double AllocatedMbPerFrame { get; set; } = double.NaN;
    public VramSummary? Vram { get; set; }
    public CensusResult? CensusAfterLoad { get; set; }
    public CensusResult? CensusEnd { get; set; }
    /// <summary>Entries in MonoGame's framebuffer cache at the end of the run (stays flat when nothing leaks).</summary>
    public int FramebufferCacheEntries { get; set; } = -1;
    public double ProcessCpuPctAll { get; set; } = double.NaN;
    public double ProcessCpuPctOne { get; set; } = double.NaN;
    public double SystemCpuPct { get; set; } = double.NaN;
    public NvmlSummary? Nvml { get; set; }
    public PowerState PowerStart { get; set; }
    public PowerState PowerEnd { get; set; }
    public bool TimersAvailable { get; set; }
    public long TimerDropped { get; set; }
    public int FramesWithoutGpu { get; set; }
    public int NonMonotonic { get; set; }
    public FlagResult Flags { get; set; } = new(Array.Empty<string>(), Array.Empty<string>(), Array.Empty<string>());
    public string? CapturePath { get; set; }
    public double WallSeconds { get; set; }

    public string RunId => $"r{Round}-{Config.Id}";
}
