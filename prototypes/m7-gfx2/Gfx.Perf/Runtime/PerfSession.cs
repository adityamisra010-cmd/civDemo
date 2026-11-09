using System.Diagnostics;
using System.Globalization;
using Gfx.Engine.Diagnostics;
using Gfx.Engine.Effects;
using Gfx.Engine.Rendering;
using Gfx.Engine.Scene;
using Gfx.Perf.Analysis;
using Gfx.Perf.Measurement;
using Gfx.Perf.Output;
using Gfx.Perf.Scenes;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;

namespace Gfx.Perf.Runtime;

/// <summary>A frame source driven by a host (the windowed <see cref="BenchGame"/> or the headless test driver).</summary>
public interface IBenchDriver
{
    bool Done { get; }
    string WindowTitle { get; }
    /// <summary>Renders this frame's content offscreen; returns the image to show in the window (or null).</summary>
    RenderTarget2D? RenderFrame(bool? windowActive);
    /// <summary>Called after the host drew the preview into the back buffer (CPU ticks spent on it).</summary>
    void AfterBlit(long blitTicks);
    /// <summary>Called after the host presented (CPU ticks spent inside Present/SwapBuffers).</summary>
    void AfterPresent(long presentTicks);
}

/// <summary>What to run: the suite (configurations × rounds), slot bindings and output location.</summary>
public sealed class PerfPlan
{
    public required SuiteSpec Suite { get; init; }
    public required IReadOnlyDictionary<char, SceneBinding> Bindings { get; init; }
    public required string OutDir { get; init; }
    public string Mode { get; init; } = "";
    public bool Capture { get; init; } = true;
    public IReadOnlyList<string> Args { get; init; } = Array.Empty<string>();
    /// <summary>Test hook: after this many rendered frames (all runs), the host is asked to close the window (-1 = never).</summary>
    public int SimulateCloseAfterFrames { get; init; } = -1;
}

/// <summary>
/// Runs a <see cref="PerfPlan"/> one frame at a time. Per run: load the bound scene (timed), apply the
/// configuration (resolution, shadow setting, LOD multiplier, crowd size), render warm-up frames, then the
/// measured frames along the slot's fixed camera path, then collect the remaining GPU timestamps, compute the
/// statistics and flags, append the CSV row and optionally save a JPEG. The scene is rendered by the engine's
/// <see cref="ForwardRenderer"/>, unchanged by the harness (since the render1 merge: the G2 frame, unless the
/// scene's switches select the G1 frame); the harness only wraps the scene in a <see cref="ContentTap"/>.
/// GPU marker order per frame: frame.begin, update.end, [shadow.casters.begin, shadow.casters.end] (once per
/// shadow map: once in the G1 frame, once per cascade in the G2 frame), opaque.begin, opaque.end, render.end,
/// present.blit.end, frame.end. Each run gets a new renderer, so G2 auto exposure starts primed on the run's first
/// warm-up frame; the capture frame re-primes it (<see cref="RenderCaptureFrame"/>).
/// </summary>
public sealed class PerfSession : IBenchDriver, IDisposable
{
    public const string MarkUpdateEnd = "update.end", MarkRenderEnd = "render.end", MarkBlitEnd = "present.blit.end";
    enum Phase { Warmup, Measure, Drain, Capture }

    sealed class RunState
    {
        public required RunResult Result;
        public required DemoScene Scene;
        public required ForwardRenderer Renderer;
        public required RenderTarget2D Target;
        public required ContentTap Tap;
        public required ICameraPath Path;
        public required SceneKnobs Knobs;
        public Phase Phase;
        public int FrameInPhase;
        public readonly List<FrameRecord> Frames = new();
        public readonly Dictionary<long, int> IndexById = new();
        public readonly Dictionary<string, List<double>> NamedCpu = new(StringComparer.Ordinal);
        public readonly Dictionary<string, List<double>> NamedGpu = new(StringComparer.Ordinal);
        public readonly Dictionary<string, List<long>> Counters = new(StringComparer.Ordinal);
        public long LastStartTicks, FirstStartTicks, RunStartTicks;
        public FrameRecord Pending; public bool HasPending, PendingMeasured;
        public SystemProbes.CpuWindow? Cpu;
        public int Gen0, Gen1, Gen2; public long AllocStart;
        public long MeasureStartTicks, MeasureEndTicks;
        public long WsPeak, GcPeak;
        public long[]? NvxStart; public long NvxMinAvail = -1, AtiTexMin = -1, AtiVboMin = -1;
        public long[]? NvxEnd;
    }

    readonly GraphicsDevice _gd;
    readonly EffectLibrary _fx;
    readonly PerfPlan _plan;
    readonly PerfLog _log;
    readonly GlApi? _gl;
    readonly GpuTimer? _timer;
    readonly Nvml? _nvml;
    readonly NvmlSampler? _sampler;
    readonly List<(int round, RunConfig cfg)> _queue = new();
    readonly ResultWriter _writer;
    readonly SpriteBatch _sprites;
    int _next; long _frameCounter, _framesRendered;
    RunState? _run, _retiring;
    readonly long _sessionStart = Stopwatch.GetTimestamp();

    public SessionInfo Info { get; }
    public double EffectsWarmupMs { get; }
    public double DeviceMs { get; }
    public List<RunResult> Results { get; } = new();
    public bool Done { get; private set; }
    public string Status { get; private set; } = "running";
    public int ExitCode { get; private set; }
    public int RunErrors { get; private set; }
    public string WindowTitle { get; private set; } = "CivSim benchmark";
    public string FileBase => _writer.FileBase;
    /// <summary>GL objects reclaimed after each run by <see cref="RenderTargetReclaimer"/> (MonoGame 3.8.5 render-target leak).</summary>
    public int ReclaimedFramebuffers { get; private set; }
    public int ReclaimedRenderbuffers { get; private set; }
    public string? ReclaimFailure { get; private set; }
    /// <summary>Raised when <see cref="PerfPlan.SimulateCloseAfterFrames"/> is reached (the host closes its window).</summary>
    public event Action? SimulateCloseRequested;

    PerfSession(GraphicsDevice gd, EffectLibrary fx, PerfPlan plan, PerfLog log, GlApi? gl, GpuTimer? timer, Nvml? nvml, SessionInfo info,
        double effectsMs, double deviceMs)
    {
        _gd = gd; _fx = fx; _plan = plan; _log = log; _gl = gl; _timer = timer; _nvml = nvml; Info = info;
        EffectsWarmupMs = effectsMs; DeviceMs = deviceMs;
        if (nvml != null) _sampler = new NvmlSampler(nvml);
        for (int r = 1; r <= plan.Suite.Rounds; r++)
            foreach (var c in plan.Suite.Configs) _queue.Add((r, c));
        _sprites = new SpriteBatch(gd);
        _writer = new ResultWriter(plan, info, effectsMs, deviceMs);
    }

    /// <summary>Creates the session on the GL thread (device ready, effects warmed up) and prints the banner.</summary>
    public static PerfSession Start(GraphicsDevice gd, EffectLibrary fx, PerfPlan plan, PerfLog log, bool vsyncRequested, double effectsMs, double deviceMs)
    {
        var gl = GlApi.TryCreate(out var glFailure);
        GpuTimer? timer = null;
        if (gl is { HasTimerQueries: true })
        {
            try { timer = new GpuTimer(gl); }
            catch (Exception e) { glFailure = "GpuTimer: " + e.Message; }
        }
        else if (gl != null) glFailure ??= "GL timer queries not supported by this context";
        var nvml = Nvml.TryOpen(out var nvmlStatus);
        var info = SessionInfo.Collect(gd, gl, glFailure, nvml, nvmlStatus, vsyncRequested);
        var s = new PerfSession(gd, fx, plan, log, gl, timer, nvml, info, effectsMs, deviceMs);
        log.Line(info.Validity.Banner(info.Renderer));
        log.Line(info.ToText());
        log.Line(string.Format(CultureInfo.InvariantCulture,
            "suite {0}: {1} configurations x {2} round(s) = {3} runs, {4} warm-up (at least {8:0.#} s) + {5} measured frames each; effects warm-up {6:F0} ms; results -> {7}",
            plan.Suite.Name, plan.Suite.Configs.Count, plan.Suite.Rounds, s._queue.Count, plan.Suite.Warmup, plan.Suite.Frames, effectsMs,
            Path.Combine(plan.OutDir, s.FileBase + ".*"), plan.Suite.WarmupMinSeconds));
        foreach (var b in plan.Bindings.Values.OrderBy(b => b.Slot))
            log.Line($"slot {b.Slot} ({SlotBindings.SlotTitle(b.Slot)}) = {b.Spec}: {SlotBindings.StandInGap(b.Slot, b.Spec)}");
        if (!info.Validity.ValidVictus) log.Line("every timing below: " + info.TimingLabel);
        return s;
    }

    static double Ms(long ticks) => ticks * 1000.0 / Stopwatch.Frequency;

    // ------------------------------------------------------------------ frame loop

    public RenderTarget2D? RenderFrame(bool? windowActive)
    {
        if (Done) return null;
        if (_retiring != null) { DisposeRun(_retiring); _retiring = null; }
        if (_run == null)
        {
            if (!StartNextRun()) { Finish("finished"); return null; }
            return null; // the load frame shows nothing; warm-up starts next frame
        }
        var r = _run;
        try
        {
            switch (r.Phase)
            {
                case Phase.Warmup:
                case Phase.Measure:
                    return RenderPathFrame(r, windowActive);
                case Phase.Drain:
                    Drain(r);
                    FinalizeRun(r);
                    if (_plan.Capture && r.Result.Config.Capture && r.Result.Round == 1) { r.Phase = Phase.Capture; return null; }
                    EndRun(r);
                    return null;
                case Phase.Capture:
                    var shown = CaptureFrame(r);
                    EndRun(r);
                    return shown;
            }
        }
        catch (Exception e) when (e is not OutOfMemoryException)
        {
            RunFailed(r, e);
        }
        return null;
    }

    RenderTarget2D RenderPathFrame(RunState r, bool? active)
    {
        bool measuring = r.Phase == Phase.Measure;
        long t0 = Stopwatch.GetTimestamp();
        if (r.FirstStartTicks == 0) r.FirstStartTicks = t0;
        else if (double.IsNaN(r.Result.FirstFrameMs)) r.Result.FirstFrameMs = Ms(t0 - r.FirstStartTicks);
        float u = r.FrameInPhase / (float)Math.Max(1, _plan.Suite.Frames);
        long id = ++_frameCounter;
        _timer?.BeginFrame(id);
        r.Tap.ResetFrame();
        long t1, t2;
        var stats = r.Renderer.Stats;
        PerfMarkers.Sink = r.Tap;
        try
        {
            var pf = r.Path.At(u);
            r.Scene.Update(pf.SceneTime);
            _timer?.Mark(MarkUpdateEnd);
            t1 = Stopwatch.GetTimestamp();
            stats.BeginFrame();
            r.Renderer.Render(r.Tap, pf.Camera, SunModel.At(pf.Hour), r.Target, pf.SceneTime);
            stats.EndFrame();
            _timer?.Mark(MarkRenderEnd);
            t2 = Stopwatch.GetTimestamp();
        }
        finally { PerfMarkers.Sink = null; }

        var rec = FrameRecord.New(id);
        rec.IntervalMs = r.LastStartTicks == 0 ? double.NaN : Ms(t0 - r.LastStartTicks);
        r.LastStartTicks = t0;
        rec.UpdateMs = Ms(t1 - t0); rec.RenderMs = Ms(t2 - t1);
        rec.CastersMs = Ms(r.Tap.CastersTicks); rec.OpaqueMs = Ms(r.Tap.OpaqueTicks); rec.FirstCallMs = Ms(r.Tap.FirstCallTicks);
        rec.Draws = stats.LastDrawCalls; rec.Tris = stats.LastTriangles; rec.Instances = stats.LastInstances;
        rec.Counters = r.Knobs.Read(); rec.Active = active;
        if (measuring)
        {
            foreach (var (n, ms) in r.Tap.SegmentsMs()) Add(r.NamedCpu, n, ms);
            foreach (var (n, v) in r.Tap.Counters()) { if (!r.Counters.TryGetValue(n, out var l)) r.Counters[n] = l = new List<long>(); l.Add(v); }
        }
        r.Pending = rec; r.HasPending = true; r.PendingMeasured = measuring;
        return r.Target;
    }

    static void Add(Dictionary<string, List<double>> d, string k, double v) { if (!d.TryGetValue(k, out var l)) d[k] = l = new List<double>(); l.Add(v); }

    public void AfterBlit(long blitTicks)
    {
        if (_timer is { InFrame: true }) { _timer.Mark(MarkBlitEnd); _timer.EndFrame(flush: true); }
        else if (_gl != null && _timer == null) _gl.Flush();
        if (_run is { HasPending: true } r) r.Pending.BlitMs = Ms(blitTicks);
    }

    public void AfterPresent(long presentTicks)
    {
        if (_run is not { HasPending: true } r) return;
        long h0 = Stopwatch.GetTimestamp();
        var rec = r.Pending; r.HasPending = false;
        rec.PresentMs = Ms(presentTicks);
        int idx = -1;
        if (r.PendingMeasured) { idx = r.Frames.Count; r.IndexById[rec.FrameId] = idx; r.Frames.Add(rec); }
        CollectGpu(r);
        r.FrameInPhase++;
        _framesRendered++;
        if (r.Phase == Phase.Measure)
        {
            if (r.FrameInPhase % 60 == 0) SampleMemory(r, includePrivate: false);
            if (r.FrameInPhase % 120 == 0) SampleVram(r);
        }
        if (r.Phase == Phase.Warmup && WarmupDone(r)) BeginMeasure(r);
        else if (r.Phase == Phase.Measure && r.FrameInPhase >= _plan.Suite.Frames) EndMeasure(r);
        if (idx >= 0) { var f = r.Frames[idx]; f.HarnessMs = Ms(Stopwatch.GetTimestamp() - h0); r.Frames[idx] = f; }
        WindowTitle = Title(r);
        if (_plan.SimulateCloseAfterFrames > 0 && _framesRendered == _plan.SimulateCloseAfterFrames) SimulateCloseRequested?.Invoke();
    }

    void CollectGpu(RunState r)
    {
        if (_timer == null) return;
        _timer.Poll();
        while (_timer.TryDequeue(out var g))
            if (r.IndexById.TryGetValue(g.FrameId, out int i)) { var f = r.Frames[i]; f.ApplyGpu(g, r.NamedGpu); r.Frames[i] = f; }
    }

    string Title(RunState r) => string.Format(CultureInfo.InvariantCulture, "CivSim benchmark [{0}] run {1}/{2} round {3}: {4} {5}",
        Info.Validity.Label, r.Result.RunIndex, _queue.Count, r.Result.Round, r.Result.Config.Id,
        r.Phase == Phase.Warmup ? "warm-up frame " + r.FrameInPhase.ToString(CultureInfo.InvariantCulture)
            : r.Phase.ToString().ToLowerInvariant() + " " + r.FrameInPhase.ToString(CultureInfo.InvariantCulture) + "/" + _plan.Suite.Frames.ToString(CultureInfo.InvariantCulture));

    // ------------------------------------------------------------------ run lifecycle

    bool StartNextRun()
    {
        while (_next < _queue.Count)
        {
            var (round, cfg) = _queue[_next++];
            var b = _plan.Bindings[cfg.Slot];
            var res = new RunResult
            {
                RunIndex = _next, Round = round, Config = cfg, SceneSpec = cfg.Crowd is int n ? "crowd:" + n.ToString(CultureInfo.InvariantCulture) : b.Spec,
                StandInGap = SlotBindings.StandInGap(cfg.Slot, b.Spec), WarmupFrames = _plan.Suite.Warmup,
            };
            DemoScene? scene = null; ForwardRenderer? renderer = null; RenderTarget2D? rt = null;
            try
            {
                long l0 = Stopwatch.GetTimestamp();
                res.PowerStart = SystemProbes.ReadPower();
                scene = b.Create(cfg.Crowd);
                res.SceneType = scene.GetType().FullName ?? "?";
                scene.Load(_gd, _fx);
                renderer = new ForwardRenderer(_gd, _fx);
                scene.Configure(renderer.Settings);
                res.ShadowMapSize = ApplyShadowMode(renderer.Settings, cfg.Shadows);
                res.ShadowsOn = renderer.Settings.Shadows;
                res.RenderFeatures = renderer.Settings.Features.Describe();
                // A new renderer per run starts with auto exposure primed: the run's first warm-up frame meters its own
                // view instead of inheriting the previous configuration's exposure. Kept explicit in case renderers are reused.
                renderer.ResetAdaptation = true;
                var knobs = SceneKnobs.Discover(scene);
                res.Knobs = knobs.ApplyLodMultiplier(cfg.LodMult);
                rt = new RenderTarget2D(_gd, cfg.Width, cfg.Height, false, SurfaceFormat.Color, DepthFormat.None);
                var path = CameraPaths.For(cfg.Path, scene);
                res.PathDescription = path.Description;
                res.LoadMs = Ms(Stopwatch.GetTimestamp() - l0);
                res.CensusAfterLoad = MemoryCensus.Take(_gd);
                GC.Collect(); GC.WaitForPendingFinalizers(); GC.Collect();
                _run = new RunState { Result = res, Scene = scene, Renderer = renderer, Target = rt, Tap = new ContentTap(scene, _timer), Path = path, Knobs = knobs, Phase = Phase.Warmup, RunStartTicks = l0 };
                if (_plan.Suite.Warmup <= 0) BeginMeasure(_run);
                _log.Line(string.Format(CultureInfo.InvariantCulture, "[run {0}/{1}] round {2} {3} | {4} = {5} | load {6:F0} ms",
                    res.RunIndex, _queue.Count, round, cfg, b.Slot, res.SceneSpec, res.LoadMs));
                WindowTitle = Title(_run);
                return true;
            }
            catch (Exception e) when (e is not OutOfMemoryException)
            {
                rt?.Dispose(); renderer?.Dispose(); scene?.Dispose();
                res.Status = "error: " + e.GetType().Name + ": " + e.Message;
                RunErrors++;
                Results.Add(res);
                _writer.AppendRow(res);
                _log.Line($"[run {res.RunIndex}/{_queue.Count}] {cfg.Id} FAILED during load: {e}");
            }
        }
        return false;
    }

    /// <summary>At least the suite's warm-up frames AND its minimum warm-up time (a time condition always ends).</summary>
    bool WarmupDone(RunState r)
    {
        if (r.FrameInPhase < _plan.Suite.Warmup) return false;
        if (_plan.Suite.WarmupMinSeconds <= 0 || r.FirstStartTicks == 0) return true;
        return Ms(Stopwatch.GetTimestamp() - r.FirstStartTicks) >= _plan.Suite.WarmupMinSeconds * 1000;
    }

    void BeginMeasure(RunState r)
    {
        r.Result.WarmupFrames = r.FrameInPhase;
        r.Phase = Phase.Measure; r.FrameInPhase = 0;
        r.Gen0 = GC.CollectionCount(0); r.Gen1 = GC.CollectionCount(1); r.Gen2 = GC.CollectionCount(2);
        r.AllocStart = GC.GetTotalAllocatedBytes(false);
        r.Result.MemStart = SystemProbes.ReadMemory(includePrivate: true);
        r.WsPeak = r.Result.MemStart.WorkingSet; r.GcPeak = r.Result.MemStart.GcHeap;
        r.NvxStart = ReadNvx(); SampleVram(r);
        _timer?.ResetCounters();
        r.Cpu = new SystemProbes.CpuWindow();
        r.MeasureStartTicks = Stopwatch.GetTimestamp();
        // The first measured frame's interval would include this method's sampling: it is left out (NaN), so a run
        // of N measured frames has N-1 frame intervals.
        r.LastStartTicks = 0;
    }

    void EndMeasure(RunState r)
    {
        r.MeasureEndTicks = Stopwatch.GetTimestamp();
        var res = r.Result;
        if (r.Cpu != null) (res.ProcessCpuPctAll, res.ProcessCpuPctOne, res.SystemCpuPct) = r.Cpu.Stop();
        res.Gen0 = GC.CollectionCount(0) - r.Gen0; res.Gen1 = GC.CollectionCount(1) - r.Gen1; res.Gen2 = GC.CollectionCount(2) - r.Gen2;
        long alloc = GC.GetTotalAllocatedBytes(false) - r.AllocStart;
        res.AllocatedMbPerFrame = alloc / (1024.0 * 1024.0) / Math.Max(1, _plan.Suite.Frames);
        SampleMemory(r, includePrivate: true);
        res.MemEnd = SystemProbes.ReadMemory(includePrivate: true);
        SampleVram(r); r.NvxEnd = ReadNvx();
        res.PowerEnd = SystemProbes.ReadPower();
        r.Phase = Phase.Drain;
    }

    void SampleMemory(RunState r, bool includePrivate)
    {
        var m = SystemProbes.ReadMemory(includePrivate);
        r.WsPeak = Math.Max(r.WsPeak, m.WorkingSet); r.GcPeak = Math.Max(r.GcPeak, m.GcHeap);
    }

    long[]? ReadNvx()
    {
        if (_gl == null || !Info.NvxMemInfo) return null;
        return new long[]
        {
            _gl.GetKb(GlApi.GL_GPU_MEMORY_INFO_DEDICATED_VIDMEM_NVX), _gl.GetKb(GlApi.GL_GPU_MEMORY_INFO_TOTAL_AVAILABLE_MEMORY_NVX),
            _gl.GetKb(GlApi.GL_GPU_MEMORY_INFO_CURRENT_AVAILABLE_VIDMEM_NVX), _gl.GetKb(GlApi.GL_GPU_MEMORY_INFO_EVICTION_COUNT_NVX),
            _gl.GetKb(GlApi.GL_GPU_MEMORY_INFO_EVICTED_MEMORY_NVX),
        };
    }

    void SampleVram(RunState r)
    {
        if (_gl == null) return;
        static long Min(long acc, long v) => v < 0 ? acc : acc < 0 ? v : Math.Min(acc, v);
        if (Info.NvxMemInfo) r.NvxMinAvail = Min(r.NvxMinAvail, _gl.GetKb(GlApi.GL_GPU_MEMORY_INFO_CURRENT_AVAILABLE_VIDMEM_NVX));
        if (Info.AtiMemInfo)
        {
            r.AtiTexMin = Min(r.AtiTexMin, _gl.GetKb(GlApi.GL_TEXTURE_FREE_MEMORY_ATI));
            r.AtiVboMin = Min(r.AtiVboMin, _gl.GetKb(GlApi.GL_VBO_FREE_MEMORY_ATI));
        }
    }

    void Drain(RunState r)
    {
        if (_timer == null) return;
        _timer.FinishAll();
        CollectGpu(r);
    }

    void FinalizeRun(RunState r)
    {
        var res = r.Result;
        var fr = r.Frames.ToArray();
        res.Frames = fr;
        res.WallSeconds = Ms(Stopwatch.GetTimestamp() - r.RunStartTicks) / 1000.0;
        res.FrameMs = Stats.Of(fr.Select(f => f.IntervalMs));
        res.CpuMs = Stats.Of(fr.Select(f => f.CpuMs));
        res.GpuMs = Stats.Of(fr.Select(f => f.GpuMs));
        res.UpdateMs = Stats.Of(fr.Select(f => f.UpdateMs));
        res.RenderMs = Stats.Of(fr.Select(f => f.RenderMs));
        res.CastersCpuMs = Stats.Of(fr.Select(f => f.CastersMs));
        res.OpaqueCpuMs = Stats.Of(fr.Select(f => f.OpaqueMs));
        res.FirstCallCpuMs = Stats.Of(fr.Select(f => f.FirstCallMs));
        res.BlitCpuMs = Stats.Of(fr.Select(f => f.BlitMs));
        res.PresentMs = Stats.Of(fr.Select(f => f.PresentMs));
        res.HarnessMs = Stats.Of(fr.Select(f => f.HarnessMs));
        res.GpuUploadMs = Stats.Of(fr.Select(f => f.GpuUploadMs));
        res.GpuShadowClearMs = Stats.Of(fr.Select(f => f.GpuShadowClearMs));
        res.GpuCastersMs = Stats.Of(fr.Select(f => f.GpuCastersMs));
        res.GpuSkyMs = Stats.Of(fr.Select(f => f.GpuSkyMs));
        res.GpuOpaqueMs = Stats.Of(fr.Select(f => f.GpuOpaqueMs));
        res.GpuPostMs = Stats.Of(fr.Select(f => f.GpuPostMs));
        res.GpuBlitMs = Stats.Of(fr.Select(f => f.GpuBlitMs));
        foreach (var k in r.NamedCpu.Keys.OrderBy(x => x, StringComparer.Ordinal)) res.NamedCpuSegments[k] = Stats.Of(r.NamedCpu[k]);
        foreach (var k in r.NamedGpu.Keys.OrderBy(x => x, StringComparer.Ordinal)) res.NamedGpuSegments[k] = Stats.Of(r.NamedGpu[k]);
        foreach (var k in r.Counters.Keys.OrderBy(x => x, StringComparer.Ordinal)) res.NamedCountersMean[k] = r.Counters[k].Average();
        res.Histogram = Stats.Histogram(fr.Select(f => f.IntervalMs));
        if (fr.Length > 0)
        {
            res.DrawsMean = fr.Average(f => f.Draws); res.DrawsMax = fr.Max(f => f.Draws);
            res.TrisMean = fr.Average(f => (double)f.Tris); res.TrisMax = fr.Max(f => f.Tris);
            res.InstancesMean = fr.Average(f => (double)f.Instances); res.InstancesMax = fr.Max(f => f.Instances);
            if (fr[0].Counters.Lod0 >= 0)
            {
                res.Lod0Mean = fr.Average(f => (double)f.Counters.Lod0); res.Lod1Mean = fr.Average(f => (double)f.Counters.Lod1);
                res.Lod2Mean = fr.Average(f => (double)f.Counters.Lod2); res.CulledMean = fr.Average(f => (double)f.Counters.Culled);
            }
            if (fr[0].Counters.Chunks >= 0) { res.ChunksMean = fr.Average(f => (double)f.Counters.Chunks); res.PropsMean = fr.Average(f => (double)f.Counters.Props); }
        }
        res.WorkingSetPeak = r.WsPeak; res.GcHeapPeak = r.GcPeak;
        res.Vram = new VramSummary(Info.NvxMemInfo ? "GL_NVX_gpu_memory_info" : Info.AtiMemInfo ? "GL_ATI_meminfo" : "unavailable",
            r.NvxStart?[0] ?? -1, r.NvxStart?[1] ?? -1, r.NvxStart?[2] ?? -1, r.NvxMinAvail,
            r.NvxStart != null && r.NvxEnd != null && r.NvxStart[3] >= 0 && r.NvxEnd[3] >= 0 ? r.NvxEnd[3] - r.NvxStart[3] : -1,
            r.NvxStart != null && r.NvxEnd != null && r.NvxStart[4] >= 0 && r.NvxEnd[4] >= 0 ? r.NvxEnd[4] - r.NvxStart[4] : -1,
            r.AtiTexMin, r.AtiVboMin);
        res.CensusEnd = MemoryCensus.Take(_gd);
        res.FramebufferCacheEntries = RenderTargetReclaimer.CachedFramebuffers(_gd);
        NvmlSample[]? nv = _sampler?.Between(r.MeasureStartTicks, r.MeasureEndTicks);
        res.Nvml = nv == null ? null : NvmlSummary.From(nv);
        res.TimersAvailable = _timer != null;
        res.TimerDropped = _timer?.Dropped ?? 0;
        res.FramesWithoutGpu = fr.Count(f => !f.GpuReceived);
        res.NonMonotonic = fr.Count(f => f.GpuReceived && !f.GpuMonotonic);
        var flags = Analysis.Flags.Evaluate(new FlagInputs(fr.Select(f => f.IntervalMs).ToArray(), res.CpuMs.P50, res.GpuMs.P50,
            res.TimersAvailable, res.TimerDropped, res.FramesWithoutGpu, res.NonMonotonic,
            new[] { res.PowerStart.OnBattery, res.PowerEnd.OnBattery }, fr.Select(f => f.Active).ToArray(), nv, res.Gen2));
        res.Flags = new FlagResult(Info.SessionFlags().Concat(flags.Raised).ToArray(), flags.NotEvaluated, flags.Notes);
        Results.Add(res);
        _writer.AppendRow(res);
        _log.Line(string.Format(CultureInfo.InvariantCulture,
            "    frame p50 {0:F2} p95 {1:F2} p99 {2:F2} ms ({3:F1} fps) | GPU p50 {4} | CPU p50 {5:F2} ms | draws {6:F0} tris {7:F0} | flags {8} [{9}]",
            res.FrameMs.P50, res.FrameMs.P95, res.FrameMs.P99, 1000.0 / res.FrameMs.Mean,
            double.IsNaN(res.GpuMs.P50) ? "n/a" : res.GpuMs.P50.ToString("F2", CultureInfo.InvariantCulture) + " ms",
            res.CpuMs.P50, res.DrawsMean, res.TrisMean, res.Flags.Raised.Count == 0 ? "none" : string.Join(',', res.Flags.Raised), Info.TimingLabel));
    }

    RenderTarget2D? CaptureFrame(RunState r)
    {
        var res = r.Result;
        RenderCaptureFrame(r.Renderer, r.Scene, r.Path.At(0.5f), r.Target);
        string dir = Path.Combine(_plan.OutDir, "captures");
        Directory.CreateDirectory(dir);
        string file = Path.Combine(dir, $"{res.Config.Id}-{Sanitize(res.SceneSpec)}-{Info.Validity.Label}.jpg");
        SaveJpeg(r.Target, file, 1280);
        res.CapturePath = file;
        _log.Line("    capture " + file + " (u=0.5 of the camera path; " + Info.TimingLabel + ")");
        return r.Target;
    }

    /// <summary>
    /// Renders the review capture of a run: the frame at <paramref name="pf"/>, after the measured frames, with the
    /// run's renderer. G2 auto exposure is re-primed first, so the capture is metered on its own view: without it the
    /// capture would keep the exposure the measured frames ended on (its scene time lies before theirs, so the
    /// adaptation step is zero).
    /// </summary>
    internal static void RenderCaptureFrame(ForwardRenderer renderer, DemoScene scene, PathFrame pf, RenderTarget2D target)
    {
        renderer.ResetAdaptation = true;
        scene.Update(pf.SceneTime);
        renderer.Render(scene, pf.Camera, SunModel.At(pf.Hour), target, pf.SceneTime);
    }

    /// <summary>
    /// Applies a configuration's shadow setting. The size is that of each shadow map: G1's single map
    /// (<see cref="RenderSettings.ShadowMapSize"/>) and, in the G2 frame, each cascade
    /// (<see cref="RenderFeatures.CascadeSize"/>; the default 2048 matches G1's single map). Returns the size the
    /// frame will use (0 = shadows off).
    /// </summary>
    internal static int ApplyShadowMode(RenderSettings s, ShadowMode mode)
    {
        switch (mode)
        {
            case ShadowMode.Off: s.Shadows = false; break;
            case ShadowMode.S1024: s.Shadows = true; s.ShadowMapSize = 1024; s.Features.CascadeSize = 1024; break;
            case ShadowMode.S4096: s.Shadows = true; s.ShadowMapSize = 4096; s.Features.CascadeSize = 4096; break;
        }
        if (!s.Shadows) return 0;
        return s.Features.ShadowTechnique == ShadowTechnique.Cascaded ? s.Features.CascadeSize : s.ShadowMapSize;
    }

    static string Sanitize(string s) => new(s.Select(c => char.IsLetterOrDigit(c) || c == '-' ? c : '-').ToArray());

    /// <summary>Saves <paramref name="src"/> as JPEG, downscaled (bilinear) to at most <paramref name="maxWidth"/> wide.</summary>
    public void SaveJpeg(RenderTarget2D src, string file, int maxWidth)
    {
        RenderTarget2D? tmp = null;
        try
        {
            var outRt = src;
            if (src.Width > maxWidth)
            {
                int w = maxWidth, h = (int)Math.Round(src.Height * (maxWidth / (double)src.Width));
                tmp = new RenderTarget2D(_gd, w, h, false, SurfaceFormat.Color, DepthFormat.None);
                _gd.SetRenderTarget(tmp);
                _sprites.Begin(SpriteSortMode.Immediate, BlendState.Opaque, SamplerState.LinearClamp, DepthStencilState.None, RasterizerState.CullNone);
                _sprites.Draw(src, new Rectangle(0, 0, w, h), Color.White);
                _sprites.End();
                _gd.SetRenderTarget(null);
                outRt = tmp;
            }
            using var fs = File.Create(file);
            outRt.SaveAsJpeg(fs, outRt.Width, outRt.Height);
        }
        finally { tmp?.Dispose(); }
    }

    void EndRun(RunState r) { _retiring = r; _run = null; }

    void DisposeRun(RunState r)
    {
        try { r.Target.Dispose(); } catch (Exception) { }
        try { r.Renderer.Dispose(); } catch (Exception) { }
        try { r.Scene.Dispose(); } catch (Exception) { }
        Reclaim();
        GC.Collect(); GC.WaitForPendingFinalizers(); GC.Collect();
    }

    /// <summary>Deletes the framebuffers and renderbuffers MonoGame 3.8.5 leaves behind for disposed render targets.</summary>
    void Reclaim()
    {
        if (_gl == null) { ReclaimFailure ??= "no GL entry points"; return; }
        try
        {
            var res = RenderTargetReclaimer.Reclaim(_gd, _gl);
            if (!res.Available) ReclaimFailure ??= res.Failure;
            ReclaimedFramebuffers += res.Framebuffers; ReclaimedRenderbuffers += res.Renderbuffers;
        }
        catch (Exception e) { ReclaimFailure ??= e.GetType().Name + ": " + e.Message; }
    }

    void RunFailed(RunState r, Exception e)
    {
        PerfMarkers.Sink = null;
        if (_timer is { InFrame: true }) _timer.EndFrame();
        r.Result.Status = "error: " + e.GetType().Name + ": " + e.Message;
        RunErrors++;
        r.Result.Frames = r.Frames.ToArray();
        Results.Add(r.Result);
        _writer.AppendRow(r.Result);
        _log.Line($"[run {r.Result.RunIndex}/{_queue.Count}] {r.Result.Config.Id} FAILED: {e}");
        EndRun(r);
    }

    // ------------------------------------------------------------------ end of session

    /// <summary>Normal end (all runs done) or an abort (window closed: exit 4; error: exit 1). Writes JSON, CSV and summary.</summary>
    public void Finish(string status, int exitCode = -1)
    {
        if (Done) return;
        if (_run is { } r)
        {
            r.Result.Status = "aborted (" + status + ")";
            r.Result.Frames = r.Frames.ToArray();
            Results.Add(r.Result);
            EndRun(r);
        }
        if (_retiring != null) { DisposeRun(_retiring); _retiring = null; }
        Status = status;
        ExitCode = exitCode >= 0 ? exitCode : RunErrors > 0 ? 1 : 0;
        int notRun = _queue.Count - _next;
        var notes = new List<string>
        {
            ReclaimFailure == null
                ? string.Format(CultureInfo.InvariantCulture, "MonoGame 3.8.5 render-target leak workaround: {0} framebuffers and {1} renderbuffers of disposed render targets deleted after the runs", ReclaimedFramebuffers, ReclaimedRenderbuffers)
                : "MonoGame 3.8.5 render-target leak workaround NOT active: " + ReclaimFailure + " (memory grows by each run's render targets)",
        };
        var files = _writer.Finish(Results, status, ExitCode, notRun, Ms(Stopwatch.GetTimestamp() - _sessionStart) / 1000.0, notes);
        _log.Line(notes[0]);
        _log.Line($"status: {status}; exit code {ExitCode}; runs recorded {Results.Count}, not run {notRun}, run errors {RunErrors}");
        foreach (var f in files) _log.Line("wrote " + f);
        if (!Info.Validity.ValidVictus) _log.Line(Info.Validity.Banner(Info.Renderer));
        Done = true;
    }

    public void Dispose()
    {
        if (_run != null) DisposeRun(_run);
        if (_retiring != null) DisposeRun(_retiring);
        _run = _retiring = null;
        _sampler?.Dispose(); _nvml?.Dispose(); _timer?.Dispose(); _sprites.Dispose(); _writer.Dispose();
    }
}
