using System.Globalization;
using System.Text;
using System.Text.Json;
using Gfx.Perf.Analysis;
using Gfx.Perf.Measurement;
using Gfx.Perf.Runtime;
using Gfx.Perf.Scenes;

namespace Gfx.Perf.Output;

/// <summary>Console + optional log file (flushed per line, so a crash or a closed window keeps everything).</summary>
public sealed class PerfLog : IDisposable
{
    readonly StreamWriter? _file;
    readonly object _gate = new();
    public string? FilePath { get; }

    public PerfLog(string? path)
    {
        if (string.IsNullOrEmpty(path)) return;
        var dir = Path.GetDirectoryName(Path.GetFullPath(path));
        if (!string.IsNullOrEmpty(dir)) Directory.CreateDirectory(dir);
        _file = new StreamWriter(new FileStream(path, FileMode.Append, FileAccess.Write, FileShare.Read), new UTF8Encoding(false)) { NewLine = "\r\n" };
        FilePath = path;
    }

    public void Line(string s)
    {
        lock (_gate)
        {
            Console.WriteLine(s);
            if (_file != null) { _file.WriteLine(s.Replace("\n", "\r\n", StringComparison.Ordinal)); _file.Flush(); }
        }
    }

    public void Dispose() { lock (_gate) _file?.Dispose(); }
}

/// <summary>
/// Result files: <c>perf-&lt;yyyyMMdd-HHmmss&gt;-&lt;LABEL&gt;.csv</c> (one row per run, appended as each run ends),
/// <c>.json</c> (everything, incl. per-frame series) and <c>-summary.txt</c>. LABEL is VALID_VICTUS_RTX4050 only
/// when GL_RENDERER names the RTX 4050, INVALID_NOT_RTX4050 otherwise.
/// </summary>
public sealed class ResultWriter : IDisposable
{
    static readonly CultureInfo Inv = CultureInfo.InvariantCulture;
    readonly PerfPlan _plan; readonly SessionInfo _info; readonly double _effectsMs, _deviceMs;
    StreamWriter? _csv;
    IReadOnlyList<string> _notes = Array.Empty<string>();
    public string FileBase { get; }
    public string CsvPath => Path.Combine(_plan.OutDir, FileBase + ".csv");
    public string JsonPath => Path.Combine(_plan.OutDir, FileBase + ".json");
    public string SummaryPath => Path.Combine(_plan.OutDir, FileBase + "-summary.txt");

    public ResultWriter(PerfPlan plan, SessionInfo info, double effectsMs, double deviceMs)
    {
        _plan = plan; _info = info; _effectsMs = effectsMs; _deviceMs = deviceMs;
        FileBase = FileBaseName(DateTime.ParseExact(info.StartedLocal[..19], "yyyy-MM-dd HH:mm:ss", Inv), info.Validity);
        Directory.CreateDirectory(plan.OutDir);
    }

    public static string FileBaseName(DateTime localStart, Validity v) => "perf-" + localStart.ToString("yyyyMMdd-HHmmss", Inv) + "-" + v.Label;

    // ------------------------------------------------------------------ CSV

    static string D(double v, string fmt = "0.###") => double.IsNaN(v) || double.IsInfinity(v) ? "" : v.ToString(fmt, Inv);
    static string Q(string s) => s.IndexOfAny(new[] { ',', '"', '\n', '\r' }) >= 0 ? "\"" + s.Replace("\"", "\"\"", StringComparison.Ordinal) + "\"" : s;

    /// <summary>CSV columns: name and value extractor (one row per run).</summary>
    public static readonly (string name, Func<RunResult, SessionInfo, string> get)[] Columns =
    {
        ("label", (r, i) => i.Validity.Label),
        ("run", (r, i) => r.RunIndex.ToString(Inv)),
        ("round", (r, i) => r.Round.ToString(Inv)),
        ("config_id", (r, i) => r.Config.Id),
        ("sweep", (r, i) => r.Config.Sweep),
        ("slot", (r, i) => r.Config.Slot.ToString()),
        ("scene", (r, i) => r.SceneSpec),
        ("camera_path", (r, i) => r.Config.Path),
        ("width", (r, i) => r.Config.Width.ToString(Inv)),
        ("height", (r, i) => r.Config.Height.ToString(Inv)),
        ("lod_mult", (r, i) => r.Config.LodMult.ToString("0.##", Inv)),
        ("shadows", (r, i) => r.Config.ShadowLabel),
        ("shadow_map", (r, i) => r.ShadowMapSize.ToString(Inv)),
        ("crowd", (r, i) => r.Config.Crowd?.ToString(Inv) ?? ""),
        ("status", (r, i) => r.Status),
        ("warmup_frames", (r, i) => r.WarmupFrames.ToString(Inv)),
        ("frames", (r, i) => r.Frames.Length.ToString(Inv)),
        ("load_ms", (r, i) => D(r.LoadMs, "0.#")),
        ("first_frame_ms", (r, i) => D(r.FirstFrameMs, "0.##")),
        ("frame_ms_p50", (r, i) => D(r.FrameMs.P50)),
        ("frame_ms_p95", (r, i) => D(r.FrameMs.P95)),
        ("frame_ms_p99", (r, i) => D(r.FrameMs.P99)),
        ("frame_ms_mean", (r, i) => D(r.FrameMs.Mean)),
        ("frame_ms_max", (r, i) => D(r.FrameMs.Max)),
        ("fps_mean", (r, i) => D(1000.0 / r.FrameMs.Mean, "0.#")),
        ("fps_p99_low", (r, i) => D(1000.0 / r.FrameMs.P99, "0.#")),
        ("cpu_ms_p50", (r, i) => D(r.CpuMs.P50)),
        ("cpu_ms_p95", (r, i) => D(r.CpuMs.P95)),
        ("cpu_ms_p99", (r, i) => D(r.CpuMs.P99)),
        ("gpu_ms_p50", (r, i) => D(r.GpuMs.P50)),
        ("gpu_ms_p95", (r, i) => D(r.GpuMs.P95)),
        ("gpu_ms_p99", (r, i) => D(r.GpuMs.P99)),
        ("gpu_frames", (r, i) => r.GpuMs.Count.ToString(Inv)),
        ("gpu_upload_ms_p50", (r, i) => D(r.GpuUploadMs.P50)),
        ("gpu_shadow_clear_ms_p50", (r, i) => D(r.GpuShadowClearMs.P50)),
        ("gpu_shadow_casters_ms_p50", (r, i) => D(r.GpuCastersMs.P50)),
        ("gpu_clear_sky_ms_p50", (r, i) => D(r.GpuSkyMs.P50)),
        ("gpu_opaque_ms_p50", (r, i) => D(r.GpuOpaqueMs.P50)),
        ("gpu_post_ms_p50", (r, i) => D(r.GpuPostMs.P50)),
        ("gpu_present_blit_ms_p50", (r, i) => D(r.GpuBlitMs.P50)),
        ("cpu_update_ms_p50", (r, i) => D(r.UpdateMs.P50)),
        ("cpu_render_ms_p50", (r, i) => D(r.RenderMs.P50)),
        ("cpu_shadow_casters_ms_p50", (r, i) => D(r.CastersCpuMs.P50)),
        ("cpu_opaque_ms_p50", (r, i) => D(r.OpaqueCpuMs.P50)),
        ("cpu_first_pass_ms_p50", (r, i) => D(r.FirstCallCpuMs.P50)),
        ("cpu_blit_ms_p50", (r, i) => D(r.BlitCpuMs.P50)),
        ("present_ms_p50", (r, i) => D(r.PresentMs.P50)),
        ("present_ms_p95", (r, i) => D(r.PresentMs.P95)),
        ("harness_ms_p50", (r, i) => D(r.HarnessMs.P50)),
        ("anim_cpu_ms_p50", (r, i) => D(r.UpdateMs.P50)),
        ("draws_mean", (r, i) => D(r.DrawsMean, "0.#")),
        ("draws_max", (r, i) => r.DrawsMax.ToString(Inv)),
        ("tris_mean", (r, i) => D(r.TrisMean, "0")),
        ("tris_max", (r, i) => r.TrisMax.ToString(Inv)),
        ("instances_mean", (r, i) => D(r.InstancesMean, "0")),
        ("soldiers_lod0_mean", (r, i) => D(r.Lod0Mean, "0.#")),
        ("soldiers_lod1_mean", (r, i) => D(r.Lod1Mean, "0.#")),
        ("soldiers_lod2_mean", (r, i) => D(r.Lod2Mean, "0.#")),
        ("soldiers_culled_mean", (r, i) => D(r.CulledMean, "0.#")),
        ("terrain_chunks_mean", (r, i) => D(r.ChunksMean, "0.#")),
        ("prop_instances_mean", (r, i) => D(r.PropsMean, "0")),
        ("working_set_peak_mb", (r, i) => D(r.WorkingSetPeak / 1048576.0, "0.#")),
        ("private_bytes_end_mb", (r, i) => r.MemEnd.PrivateBytes < 0 ? "" : D(r.MemEnd.PrivateBytes / 1048576.0, "0.#")),
        ("gc_heap_peak_mb", (r, i) => D(r.GcHeapPeak / 1048576.0, "0.#")),
        ("alloc_mb_per_frame", (r, i) => D(r.AllocatedMbPerFrame, "0.####")),
        ("gc_gen0", (r, i) => r.Gen0.ToString(Inv)),
        ("gc_gen1", (r, i) => r.Gen1.ToString(Inv)),
        ("gc_gen2", (r, i) => r.Gen2.ToString(Inv)),
        ("vram_source", (r, i) => r.Vram?.Source ?? ""),
        ("vram_dedicated_mb", (r, i) => r.Vram is { DedicatedKb: > 0 } v ? D(v.DedicatedKb / 1024.0, "0") : ""),
        ("vram_used_max_mb", (r, i) => r.Vram is { } v ? D(v.UsedMaxMb, "0") : ""),
        ("vram_evictions", (r, i) => r.Vram is { EvictionsDelta: >= 0 } v ? v.EvictionsDelta.ToString(Inv) : ""),
        ("app_gpu_census_mb", (r, i) => r.CensusEnd is { Available: true } c ? D(c.TotalBytes / 1048576.0, "0.#") : ""),
        ("app_textures_mb", (r, i) => r.CensusEnd is { Available: true } c ? D(c.TextureBytes / 1048576.0, "0.#") : ""),
        ("app_render_targets_mb", (r, i) => r.CensusEnd is { Available: true } c ? D(c.RenderTargetBytes / 1048576.0, "0.#") : ""),
        ("app_buffers_mb", (r, i) => r.CensusEnd is { Available: true } c ? D((c.VertexBytes + c.IndexBytes) / 1048576.0, "0.#") : ""),
        ("process_cpu_pct_all_cores", (r, i) => D(r.ProcessCpuPctAll, "0.#")),
        ("process_cpu_pct_one_core", (r, i) => D(r.ProcessCpuPctOne, "0.#")),
        ("system_cpu_pct", (r, i) => D(r.SystemCpuPct, "0.#")),
        ("gpu_util_pct_mean", (r, i) => r.Nvml is { } n ? D(n.GpuUtilMean, "0.#") : ""),
        ("gpu_clock_mhz_mean", (r, i) => r.Nvml is { } n ? D(n.GfxClockMean, "0") : ""),
        ("gpu_clock_mhz_min", (r, i) => r.Nvml is { GfxClockMin: >= 0 } n ? n.GfxClockMin.ToString(Inv) : ""),
        ("gpu_temp_c_max", (r, i) => r.Nvml is { TempMax: >= 0 } n ? n.TempMax.ToString(Inv) : ""),
        ("gpu_power_w_mean", (r, i) => r.Nvml is { } n ? D(n.PowerMeanW, "0.#") : ""),
        ("gpu_power_limit_w", (r, i) => r.Nvml is { } n ? D(n.PowerLimitW, "0.#") : ""),
        ("gpu_pstates", (r, i) => r.Nvml?.PStates ?? ""),
        ("gpu_throttle_reasons", (r, i) => r.Nvml is { Samples: > 0 } n ? Nvml.ReasonNames(n.ThrottleUnion) : ""),
        ("gpu_throttle_fraction", (r, i) => r.Nvml is { } n ? D(n.ThrottleFraction, "0.##") : ""),
        ("on_battery", (r, i) => r.PowerStart.OnBattery == true || r.PowerEnd.OnBattery == true ? "1" : r.PowerStart.OnBattery.HasValue || r.PowerEnd.OnBattery.HasValue ? "0" : ""),
        ("gpu_timer_dropped", (r, i) => r.TimerDropped.ToString(Inv)),
        ("frames_without_gpu_time", (r, i) => r.FramesWithoutGpu.ToString(Inv)),
        ("flags", (r, i) => string.Join(';', r.Flags.Raised)),
        ("wall_s", (r, i) => D(r.WallSeconds, "0.#")),
        ("renderer", (r, i) => i.Renderer),
    };

    public void AppendRow(RunResult r)
    {
        if (_csv == null)
        {
            _csv = new StreamWriter(new FileStream(CsvPath, FileMode.Create, FileAccess.Write, FileShare.Read), new UTF8Encoding(false)) { NewLine = "\r\n" };
            _csv.WriteLine(string.Join(',', Columns.Select(c => c.name)));
        }
        _csv.WriteLine(string.Join(',', Columns.Select(c => Q(c.get(r, _info)))));
        _csv.Flush();
    }

    // ------------------------------------------------------------------ JSON + summary

    public IReadOnlyList<string> Finish(IReadOnlyList<RunResult> runs, string status, int exitCode, int notRun, double wallSeconds, IReadOnlyList<string>? notes = null)
    {
        if (_csv == null) AppendHeaderOnly();
        _csv?.Flush();
        _notes = notes ?? Array.Empty<string>();
        WriteJson(runs, status, exitCode, notRun, wallSeconds);
        File.WriteAllText(SummaryPath, Summary(runs, status, exitCode, notRun, wallSeconds).Replace("\n", "\r\n", StringComparison.Ordinal), new UTF8Encoding(false));
        return new[] { CsvPath, JsonPath, SummaryPath };
    }

    void AppendHeaderOnly()
    {
        _csv = new StreamWriter(new FileStream(CsvPath, FileMode.Create, FileAccess.Write, FileShare.Read), new UTF8Encoding(false)) { NewLine = "\r\n" };
        _csv.WriteLine(string.Join(',', Columns.Select(c => c.name)));
    }

    /// <summary>Median over a configuration's completed rounds.</summary>
    public sealed record Aggregate(string ConfigId, RunConfig Config, string Scene, int Reps, double FrameP50, double FrameP95, double FrameP99,
        double FpsMean, double GpuP50, double GpuP95, double CpuP50, double CpuP95, double DrawsMean, double TrisMean, double LoadMs,
        double SpreadPct, IReadOnlyList<string> Flags);

    public static IReadOnlyList<Aggregate> AggregateRuns(IEnumerable<RunResult> runs)
    {
        var ok = runs.Where(r => r.Status == "ok").ToArray();
        var order = new List<string>();
        foreach (var r in ok) if (!order.Contains(r.Config.Id)) order.Add(r.Config.Id);
        var list = new List<Aggregate>();
        foreach (var id in order)
        {
            var g = ok.Where(r => r.Config.Id == id).ToArray();
            double M(Func<RunResult, double> f) => Stats.Median(g.Select(f));
            var p50s = g.Select(r => r.FrameMs.P50).Where(v => !double.IsNaN(v)).ToArray();
            double spread = p50s.Length > 1 ? 100.0 * (p50s.Max() - p50s.Min()) / Stats.Median(p50s) : double.NaN;
            var flags = g.SelectMany(r => r.Flags.Raised).Distinct().OrderBy(x => x, StringComparer.Ordinal).ToArray();
            list.Add(new Aggregate(id, g[0].Config, g[0].SceneSpec, g.Length, M(r => r.FrameMs.P50), M(r => r.FrameMs.P95), M(r => r.FrameMs.P99),
                M(r => 1000.0 / r.FrameMs.Mean), M(r => r.GpuMs.P50), M(r => r.GpuMs.P95), M(r => r.CpuMs.P50), M(r => r.CpuMs.P95),
                M(r => r.DrawsMean), M(r => r.TrisMean), M(r => r.LoadMs), spread, flags));
        }
        return list;
    }

    void WriteJson(IReadOnlyList<RunResult> runs, string status, int exitCode, int notRun, double wallSeconds)
    {
        using var fs = new FileStream(JsonPath, FileMode.Create, FileAccess.Write, FileShare.Read);
        using var w = new Utf8JsonWriter(fs, new JsonWriterOptions { Indented = true });
        w.WriteStartObject();
        w.WriteString("schema", "civsim-perf/1");
        w.WriteString("label", _info.Validity.Label);
        w.WriteBoolean("valid_victus_evidence", _info.Validity.ValidVictus);
        w.WriteString("validity_reason", _info.Validity.Reason);
        w.WriteString("timing_label", _info.TimingLabel);
        w.WriteString("status", status);
        w.WriteNumber("exit_code", exitCode);
        w.WriteNumber("runs_recorded", runs.Count);
        w.WriteNumber("runs_not_run", notRun);
        Num(w, "session_wall_s", wallSeconds);
        w.WriteStartArray("args"); foreach (var a in _plan.Args) w.WriteStringValue(a); w.WriteEndArray();
        w.WriteStartArray("session_notes"); foreach (var n in _notes) w.WriteStringValue(n); w.WriteEndArray();

        w.WriteStartObject("gpu");
        w.WriteString("vendor", _info.Vendor); w.WriteString("renderer", _info.Renderer); w.WriteString("version", _info.GlVersion); w.WriteString("glsl", _info.Glsl);
        w.WriteNumber("extensions", _info.ExtensionCount); w.WriteBoolean("timer_queries", _info.TimerQueries); w.WriteNumber("timestamp_bits", _info.TimestampBits);
        w.WriteBoolean("nvx_gpu_memory_info", _info.NvxMemInfo); w.WriteBoolean("ati_meminfo", _info.AtiMemInfo);
        if (_info.GlFailure != null) w.WriteString("gl_failure", _info.GlFailure);
        w.WriteString("validity_kind", _info.Validity.Kind);
        w.WriteString("nvml", _info.NvmlStatus);
        if (_info.NvmlDriver != null) { w.WriteString("nvml_driver", _info.NvmlDriver); w.WriteString("nvml_device", _info.NvmlDevice); }
        w.WriteEndObject();

        w.WriteStartObject("machine");
        w.WriteString("cpu", _info.CpuName); w.WriteNumber("logical_cpus", _info.LogicalCpus); w.WriteNumber("ram_bytes_visible", _info.TotalMemoryBytes);
        w.WriteString("os", _info.Os); w.WriteString("runtime", _info.Runtime); w.WriteString("monogame", _info.MonoGame); w.WriteString("profile", _info.Profile);
        w.WriteString("process", _info.ProcessPath); w.WriteString("host_kind", _info.HostKind);
        Bool(w, "exports_NvOptimusEnablement", _info.ExportsNvOptimus); Bool(w, "exports_AmdPowerXpressRequestHighPerformance", _info.ExportsAmdPowerXpress);
        w.WriteString("windows_power_mode", _info.WindowsPowerMode);
        Bool(w, "on_battery_at_start", _info.Power.OnBattery); w.WriteString("power_detail", _info.Power.Detail);
        w.WriteString("build", _info.BuildConfiguration); w.WriteString("harness_version", _info.HarnessVersion); w.WriteString("source_commit", _info.SourceCommit);
        w.WriteNumber("window_width", _info.BackBufferWidth); w.WriteNumber("window_height", _info.BackBufferHeight); w.WriteBoolean("vsync_requested", _info.VsyncRequested);
        w.WriteString("started_local", _info.StartedLocal); w.WriteString("started_utc", _info.StartedUtc);
        Num(w, "device_create_ms", _deviceMs); Num(w, "effects_warmup_ms", _effectsMs);
        w.WriteEndObject();

        w.WriteStartObject("suite");
        w.WriteString("name", _plan.Suite.Name); w.WriteString("mode", _plan.Mode);
        w.WriteNumber("configurations", _plan.Suite.Configs.Count); w.WriteNumber("rounds", _plan.Suite.Rounds);
        w.WriteNumber("warmup_frames", _plan.Suite.Warmup); w.WriteNumber("warmup_min_seconds", _plan.Suite.WarmupMinSeconds); w.WriteNumber("measured_frames", _plan.Suite.Frames);
        w.WriteStartObject("bindings");
        foreach (var b in _plan.Bindings.Values.OrderBy(b => b.Slot))
        {
            w.WriteStartObject(b.Slot.ToString());
            w.WriteString("slot", SlotBindings.SlotTitle(b.Slot)); w.WriteString("scene", b.Spec); w.WriteString("description", b.Description);
            w.WriteString("stand_in_gap", SlotBindings.StandInGap(b.Slot, b.Spec));
            w.WriteEndObject();
        }
        w.WriteEndObject();
        w.WriteStartArray("configs");
        foreach (var c in _plan.Suite.Configs) WriteConfig(w, c);
        w.WriteEndArray();
        w.WriteEndObject();

        w.WriteStartArray("aggregate");
        foreach (var a in AggregateRuns(runs))
        {
            w.WriteStartObject();
            w.WriteString("config_id", a.ConfigId); w.WriteString("scene", a.Scene); w.WriteNumber("reps", a.Reps);
            Num(w, "frame_ms_p50_median", a.FrameP50); Num(w, "frame_ms_p95_median", a.FrameP95); Num(w, "frame_ms_p99_median", a.FrameP99);
            Num(w, "fps_mean_median", a.FpsMean); Num(w, "gpu_ms_p50_median", a.GpuP50); Num(w, "gpu_ms_p95_median", a.GpuP95);
            Num(w, "cpu_ms_p50_median", a.CpuP50); Num(w, "cpu_ms_p95_median", a.CpuP95); Num(w, "draws_mean_median", a.DrawsMean);
            Num(w, "tris_mean_median", a.TrisMean); Num(w, "load_ms_median", a.LoadMs); Num(w, "rep_spread_pct_of_p50", a.SpreadPct);
            w.WriteStartArray("flags"); foreach (var f in a.Flags) w.WriteStringValue(f); w.WriteEndArray();
            w.WriteEndObject();
        }
        w.WriteEndArray();

        w.WriteStartArray("runs");
        foreach (var r in runs) WriteRun(w, r);
        w.WriteEndArray();
        w.WriteEndObject();
    }

    static void WriteConfig(Utf8JsonWriter w, RunConfig c)
    {
        w.WriteStartObject();
        w.WriteString("id", c.Id); w.WriteString("sweep", c.Sweep); w.WriteString("slot", c.Slot.ToString());
        w.WriteNumber("width", c.Width); w.WriteNumber("height", c.Height); w.WriteNumber("lod_mult", c.LodMult);
        w.WriteString("shadows", c.ShadowLabel);
        if (c.Crowd is int n) w.WriteNumber("crowd", n); else w.WriteNull("crowd");
        w.WriteString("camera_path", c.Path); w.WriteBoolean("capture", c.Capture);
        w.WriteEndObject();
    }

    static void Num(Utf8JsonWriter w, string name, double v) { if (double.IsNaN(v) || double.IsInfinity(v)) w.WriteNull(name); else w.WriteNumber(name, Math.Round(v, 4)); }
    static void Bool(Utf8JsonWriter w, string name, bool? v) { if (v is bool b) w.WriteBoolean(name, b); else w.WriteNull(name); }

    static void Dist(Utf8JsonWriter w, string name, Dist d)
    {
        w.WriteStartObject(name);
        w.WriteNumber("n", d.Count); Num(w, "p50", d.P50); Num(w, "p95", d.P95); Num(w, "p99", d.P99);
        Num(w, "mean", d.Mean); Num(w, "min", d.Min); Num(w, "max", d.Max); Num(w, "stddev", d.StdDev);
        w.WriteEndObject();
    }

    static void Series(Utf8JsonWriter w, string name, IEnumerable<double> v)
    {
        w.WriteStartArray(name);
        foreach (var x in v) { if (double.IsNaN(x)) w.WriteNullValue(); else w.WriteNumberValue(Math.Round(x, 3)); }
        w.WriteEndArray();
    }

    static void Census(Utf8JsonWriter w, string name, CensusResult? c)
    {
        if (c == null) { w.WriteNull(name); return; }
        w.WriteStartObject(name);
        w.WriteBoolean("available", c.Available);
        if (c.Failure != null) w.WriteString("failure", c.Failure);
        w.WriteNumber("textures", c.Textures); w.WriteNumber("texture_bytes", c.TextureBytes);
        w.WriteNumber("render_targets", c.RenderTargets); w.WriteNumber("render_target_bytes", c.RenderTargetBytes);
        w.WriteNumber("vertex_buffers", c.VertexBuffers); w.WriteNumber("vertex_bytes", c.VertexBytes);
        w.WriteNumber("index_buffers", c.IndexBuffers); w.WriteNumber("index_bytes", c.IndexBytes);
        w.WriteNumber("effects", c.Effects); w.WriteNumber("other_resources", c.OtherResources); w.WriteNumber("unknown_formats", c.UnknownFormats);
        w.WriteNumber("backbuffer_bytes_estimate", c.BackBufferBytes); w.WriteNumber("total_bytes", c.TotalBytes);
        w.WriteEndObject();
    }

    void WriteRun(Utf8JsonWriter w, RunResult r)
    {
        w.WriteStartObject();
        w.WriteNumber("run", r.RunIndex); w.WriteNumber("round", r.Round); w.WriteString("run_id", r.RunId);
        w.WritePropertyName("config"); WriteConfig(w, r.Config);
        w.WriteString("scene", r.SceneSpec); w.WriteString("scene_type", r.SceneType); w.WriteString("stand_in_gap", r.StandInGap);
        w.WriteString("camera_path", r.PathDescription); w.WriteString("status", r.Status);
        w.WriteBoolean("shadows_on", r.ShadowsOn); w.WriteNumber("shadow_map_size", r.ShadowMapSize);
        w.WriteStartArray("lod_knobs"); foreach (var k in r.Knobs) w.WriteStringValue(k); w.WriteEndArray();
        Num(w, "load_ms", r.LoadMs); Num(w, "first_frame_ms", r.FirstFrameMs); Num(w, "wall_s", r.WallSeconds);
        w.WriteNumber("warmup_frames", r.WarmupFrames); w.WriteNumber("measured_frames", r.Frames.Length);
        Dist(w, "frame_ms", r.FrameMs); Dist(w, "cpu_ms", r.CpuMs); Dist(w, "gpu_ms", r.GpuMs);
        w.WriteStartObject("cpu_sections_ms");
        Dist(w, "update_anim", r.UpdateMs); Dist(w, "render_submit", r.RenderMs); Dist(w, "shadow_casters", r.CastersCpuMs);
        Dist(w, "opaque", r.OpaqueCpuMs); Dist(w, "first_content_pass", r.FirstCallCpuMs); Dist(w, "preview_blit", r.BlitCpuMs);
        Dist(w, "present", r.PresentMs); Dist(w, "harness", r.HarnessMs);
        foreach (var (k, d) in r.NamedCpuSegments) Dist(w, "seg:" + k, d);
        w.WriteEndObject();
        w.WriteStartObject("gpu_passes_ms");
        Dist(w, "uploads", r.GpuUploadMs); Dist(w, "shadow_clear", r.GpuShadowClearMs); Dist(w, "shadow_casters", r.GpuCastersMs);
        Dist(w, "clear_sky", r.GpuSkyMs); Dist(w, "opaque", r.GpuOpaqueMs); Dist(w, "post", r.GpuPostMs); Dist(w, "present_blit", r.GpuBlitMs);
        foreach (var (k, d) in r.NamedGpuSegments) Dist(w, "seg:" + k, d);
        w.WriteEndObject();
        w.WriteStartObject("counters");
        Num(w, "draws_mean", r.DrawsMean); w.WriteNumber("draws_max", r.DrawsMax); Num(w, "triangles_mean", r.TrisMean); w.WriteNumber("triangles_max", r.TrisMax);
        Num(w, "instances_mean", r.InstancesMean); w.WriteNumber("instances_max", r.InstancesMax);
        Num(w, "soldiers_lod0_mean", r.Lod0Mean); Num(w, "soldiers_lod1_mean", r.Lod1Mean); Num(w, "soldiers_lod2_mean", r.Lod2Mean); Num(w, "soldiers_culled_mean", r.CulledMean);
        Num(w, "terrain_chunks_mean", r.ChunksMean); Num(w, "prop_instances_mean", r.PropsMean);
        foreach (var (k, cv) in r.NamedCountersMean) Num(w, "named:" + k, cv);
        w.WriteEndObject();
        w.WriteStartObject("frame_time_histogram");
        for (int i = 0; i < r.Histogram.Length && i < Stats.HistogramLabels.Length; i++) w.WriteNumber(Stats.HistogramLabels[i], r.Histogram[i]);
        w.WriteEndObject();
        w.WriteStartObject("memory");
        w.WriteNumber("working_set_start", r.MemStart.WorkingSet); w.WriteNumber("working_set_peak", r.WorkingSetPeak); w.WriteNumber("working_set_end", r.MemEnd.WorkingSet);
        w.WriteNumber("private_bytes_start", r.MemStart.PrivateBytes); w.WriteNumber("private_bytes_end", r.MemEnd.PrivateBytes);
        w.WriteNumber("gc_heap_peak", r.GcHeapPeak); w.WriteNumber("gc_committed_end", r.MemEnd.GcCommitted);
        Num(w, "allocated_mb_per_frame", r.AllocatedMbPerFrame);
        w.WriteNumber("gc_gen0", r.Gen0); w.WriteNumber("gc_gen1", r.Gen1); w.WriteNumber("gc_gen2", r.Gen2);
        w.WriteEndObject();
        w.WriteStartObject("vram");
        if (r.Vram is { } v)
        {
            w.WriteString("source", v.Source); w.WriteString("scope", "device-wide (all processes), KiB");
            w.WriteNumber("dedicated_kb", v.DedicatedKb); w.WriteNumber("total_available_kb", v.TotalAvailableKb);
            w.WriteNumber("current_available_start_kb", v.CurrentAvailableStartKb); w.WriteNumber("current_available_min_kb", v.CurrentAvailableMinKb);
            w.WriteNumber("evictions_delta", v.EvictionsDelta); w.WriteNumber("evicted_kb_delta", v.EvictedKbDelta);
            w.WriteNumber("ati_texture_free_min_kb", v.AtiTextureFreeMinKb); w.WriteNumber("ati_vbo_free_min_kb", v.AtiVboFreeMinKb);
            Num(w, "used_max_mb", v.UsedMaxMb);
            if (_info.IsSoftware) w.WriteString("note", "llvmpipe reports system-memory figures through these extensions; they are not VRAM");
        }
        w.WriteEndObject();
        Census(w, "app_gpu_census_after_load", r.CensusAfterLoad);
        Census(w, "app_gpu_census_end", r.CensusEnd);
        w.WriteNumber("monogame_framebuffer_cache_entries", r.FramebufferCacheEntries);
        w.WriteStartObject("utilization");
        Num(w, "process_cpu_pct_all_cores", r.ProcessCpuPctAll); Num(w, "process_cpu_pct_one_core", r.ProcessCpuPctOne); Num(w, "system_cpu_pct", r.SystemCpuPct);
        if (r.Nvml is { } n)
        {
            w.WriteNumber("nvml_samples", n.Samples); Num(w, "gpu_util_pct_mean", n.GpuUtilMean); Num(w, "gpu_clock_mhz_mean", n.GfxClockMean);
            w.WriteNumber("gpu_clock_mhz_min", n.GfxClockMin); w.WriteNumber("gpu_clock_mhz_max", n.GfxClockMax); w.WriteNumber("mem_clock_mhz_max", n.MemClockMax);
            w.WriteNumber("gpu_temp_c_max", n.TempMax); Num(w, "gpu_power_w_mean", n.PowerMeanW); Num(w, "gpu_power_w_max", n.PowerMaxW); Num(w, "gpu_power_limit_w", n.PowerLimitW);
            w.WriteNumber("gpu_mem_used_bytes_max", n.MemUsedMax); w.WriteNumber("gpu_mem_total_bytes", n.MemTotal); w.WriteString("pstates", n.PStates);
            w.WriteString("throttle_reasons", Nvml.ReasonNames(n.ThrottleUnion)); Num(w, "throttle_fraction", n.ThrottleFraction);
        }
        else w.WriteString("nvml", "unavailable");
        w.WriteEndObject();
        w.WriteStartObject("power");
        Bool(w, "on_battery_start", r.PowerStart.OnBattery); Bool(w, "on_battery_end", r.PowerEnd.OnBattery);
        w.WriteString("detail_start", r.PowerStart.Detail ?? ""); w.WriteString("detail_end", r.PowerEnd.Detail ?? "");
        w.WriteEndObject();
        w.WriteStartObject("gpu_timer");
        w.WriteBoolean("available", r.TimersAvailable); w.WriteNumber("dropped", r.TimerDropped);
        w.WriteNumber("frames_without_gpu_time", r.FramesWithoutGpu); w.WriteNumber("non_monotonic_frames", r.NonMonotonic);
        w.WriteEndObject();
        w.WriteStartArray("flags"); foreach (var f in r.Flags.Raised) w.WriteStringValue(f); w.WriteEndArray();
        w.WriteStartArray("flags_not_evaluated"); foreach (var f in r.Flags.NotEvaluated) w.WriteStringValue(f); w.WriteEndArray();
        w.WriteStartArray("flag_notes"); foreach (var f in r.Flags.Notes) w.WriteStringValue(f); w.WriteEndArray();
        if (r.CapturePath != null) w.WriteString("capture", Path.GetFileName(r.CapturePath));
        w.WriteStartObject("per_frame");
        Series(w, "frame_ms", r.Frames.Select(f => f.IntervalMs));
        Series(w, "cpu_ms", r.Frames.Select(f => f.CpuMs));
        Series(w, "gpu_ms", r.Frames.Select(f => f.GpuMs));
        Series(w, "present_ms", r.Frames.Select(f => f.PresentMs));
        w.WriteEndObject();
        w.WriteEndObject();
    }

    string Summary(IReadOnlyList<RunResult> runs, string status, int exitCode, int notRun, double wallSeconds)
    {
        var sb = new StringBuilder();
        void L(string s = "") => sb.Append(s).Append('\n');
        L(_info.Validity.Banner(_info.Renderer));
        L();
        L($"CivSim G2 benchmark summary ({FileBase})");
        L($"status {status}, exit code {exitCode}; runs recorded {runs.Count}, not run {notRun}; session {wallSeconds.ToString("F0", Inv)} s");
        L("every timing in this file: " + _info.TimingLabel);
        foreach (var n in _notes) L(n);
        L();
        L(_info.ToText());
        L();
        L($"suite {_plan.Suite.Name} ({_plan.Mode}): {_plan.Suite.Configs.Count} configurations x {_plan.Suite.Rounds} round(s), {_plan.Suite.Warmup} warm-up frames (and at least {_plan.Suite.WarmupMinSeconds.ToString("0.#", Inv)} s) + {_plan.Suite.Frames} measured frames each (warm-up excluded)");
        foreach (var b in _plan.Bindings.Values.OrderBy(b => b.Slot))
            L($"  slot {b.Slot} {SlotBindings.SlotTitle(b.Slot),-21} = {b.Spec,-18} {SlotBindings.StandInGap(b.Slot, b.Spec)}");
        L();
        L("Per configuration: median over rounds of each run's p50/p95/p99 (frame = interval between frame starts; GPU = timestamp");
        L("span frame.begin..present.blit.end; CPU = update + render submission + preview blit). spread = (max-min)/median of the rounds' p50.");
        L();
        L(string.Format(Inv, "{0,-20} {1,-17} {2,5} {3,8} {4,8} {5,8} {6,7} {7,8} {8,8} {9,8} {10,7} {11,10} {12,7}  {13}",
            "config", "scene", "reps", "p50 ms", "p95 ms", "p99 ms", "fps", "GPU p50", "GPU p95", "CPU p50", "draws", "tris", "spread", "flags"));
        foreach (var a in AggregateRuns(runs))
            L(string.Format(Inv, "{0,-20} {1,-17} {2,5} {3,8} {4,8} {5,8} {6,7} {7,8} {8,8} {9,8} {10,7} {11,10} {12,7}  {13}",
                a.ConfigId, a.Scene, a.Reps, D(a.FrameP50, "0.00"), D(a.FrameP95, "0.00"), D(a.FrameP99, "0.00"), D(a.FpsMean, "0.0"),
                D(a.GpuP50, "0.00"), D(a.GpuP95, "0.00"), D(a.CpuP50, "0.00"), D(a.DrawsMean, "0"), D(a.TrisMean, "0"),
                double.IsNaN(a.SpreadPct) ? "-" : D(a.SpreadPct, "0.0") + "%",
                string.Join(',', a.Flags.Where(f => f is not (FlagNames.NotRtx4050 or FlagNames.SoftwareRenderer)))));
        var failed = runs.Where(r => r.Status != "ok").ToArray();
        if (failed.Length > 0)
        {
            L();
            L("Runs that did not complete:");
            foreach (var r in failed) L($"  run {r.RunIndex} round {r.Round} {r.Config.Id}: {r.Status}");
        }
        L();
        L("Flags: ON_BATTERY, WINDOW_NOT_FOCUSED, THERMAL_OR_POWER_THROTTLE (NVML), VSYNC_OR_FRAME_CAP_SUSPECTED, GPU_TIMER_FRAMES_DROPPED,");
        L("GEN2_GC_DURING_MEASUREMENT qualify a run; the JSON lists, per run, each raised flag with a note and each flag that could not be evaluated.");
        L("VRAM from GL_NVX_gpu_memory_info is device-wide; app_gpu_census is this process's own MonoGame allocations (an estimate).");
        L("Numbers are only Victus evidence when the label is VALID_VICTUS_RTX4050.");
        return sb.ToString();
    }

    public void Dispose() { _csv?.Dispose(); _csv = null; }
}
