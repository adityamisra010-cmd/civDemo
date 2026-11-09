using System.Diagnostics;
using System.Globalization;
using System.Text;
using System.Text.Json;
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

/// <summary>
/// <c>--gpu-selftest</c>: which GPU did this process get (and is it the RTX 4050), do GL timer queries work, what do
/// the VRAM extensions and NVML report, do all G1 effects compile here (the GLSL 1.20 / vertex-texture-fetch /
/// HalfVector4 risk named in G1), and does one slot-A frame render. Writes &lt;prefix&gt;.txt, .json and .jpg and
/// prints a VICTUS_CHECK= line that run-benchmark.bat looks for.
/// </summary>
public sealed class GpuSelfTest : IBenchDriver, IDisposable
{
    readonly GraphicsDevice _gd; readonly EffectLibrary _fx; readonly PerfLog _log; readonly string _prefix;
    readonly IReadOnlyList<(string name, Exception? error)> _effects; readonly double _effectsMs, _deviceMs;
    readonly SceneBinding _slotA; readonly bool _requireNvidia; readonly bool _vsync;
    RenderTarget2D? _shown;

    public bool Done { get; private set; }
    public string WindowTitle { get; private set; } = "CivSim benchmark - GPU self-test";
    public int ExitCode { get; private set; }
    public SessionInfo? Info { get; private set; }

    public GpuSelfTest(GraphicsDevice gd, EffectLibrary fx, PerfLog log, string prefix, IReadOnlyList<(string, Exception?)> effects,
        double effectsMs, double deviceMs, SceneBinding slotA, bool requireNvidia, bool vsync)
    {
        _gd = gd; _fx = fx; _log = log; _prefix = prefix; _effects = effects; _effectsMs = effectsMs; _deviceMs = deviceMs;
        _slotA = slotA; _requireNvidia = requireNvidia; _vsync = vsync;
    }

    public RenderTarget2D? RenderFrame(bool? windowActive)
    {
        if (Done) return _shown;
        var inv = CultureInfo.InvariantCulture;
        var dir = Path.GetDirectoryName(Path.GetFullPath(_prefix));
        if (!string.IsNullOrEmpty(dir)) Directory.CreateDirectory(dir);
        var gl = GlApi.TryCreate(out var glFailure);
        var nvml = Nvml.TryOpen(out var nvmlStatus);
        var info = SessionInfo.Collect(_gd, gl, glFailure, nvml, nvmlStatus, _vsync);
        Info = info;
        var lines = new List<string> { info.Validity.Banner(info.Renderer), "", info.ToText(), "" };
        var json = new Dictionary<string, object?>
        {
            ["schema"] = "civsim-gpu-selftest/1", ["label"] = info.Validity.Label, ["valid_victus_evidence"] = info.Validity.ValidVictus,
            ["validity_reason"] = info.Validity.Reason, ["vendor"] = info.Vendor, ["renderer"] = info.Renderer, ["version"] = info.GlVersion,
            ["glsl"] = info.Glsl, ["host_kind"] = info.HostKind, ["process"] = info.ProcessPath, ["exports_NvOptimusEnablement"] = info.ExportsNvOptimus,
            ["exports_AmdPowerXpressRequestHighPerformance"] = info.ExportsAmdPowerXpress, ["on_battery"] = info.Power.OnBattery,
            ["power_detail"] = info.Power.Detail, ["windows_power_mode"] = info.WindowsPowerMode, ["cpu"] = info.CpuName, ["os"] = info.Os,
            ["runtime"] = info.Runtime, ["build"] = info.BuildConfiguration, ["source_commit"] = info.SourceCommit,
            ["device_create_ms"] = Round(_deviceMs), ["effects_warmup_ms"] = Round(_effectsMs), ["nvml"] = nvmlStatus,
        };
        int failures = 0;

        // GL limits that matter for the G1 renderer (vertex texture fetch for GPU skinning; texture sizes).
        if (gl != null)
        {
            int? Get(uint p) => gl.GetIntegers(p)?[0];
            var vtf = Get(0x8B4C); var maxTex = Get(0x0D33); var maxRb = Get(0x84E8);
            lines.Add($"limits         GL_MAX_VERTEX_TEXTURE_IMAGE_UNITS={vtf?.ToString(inv) ?? "?"} GL_MAX_TEXTURE_SIZE={maxTex?.ToString(inv) ?? "?"} GL_MAX_RENDERBUFFER_SIZE={maxRb?.ToString(inv) ?? "?"}");
            json["max_vertex_texture_units"] = vtf; json["max_texture_size"] = maxTex; json["extensions"] = gl.Extensions.Count;
            string[] keys = { "GL_ARB_timer_query", "GL_NVX_gpu_memory_info", "GL_ATI_meminfo", "GL_ARB_texture_float", "GL_ARB_half_float_pixel", "GL_ARB_color_buffer_float", "GL_ARB_instanced_arrays", "GL_KHR_debug" };
            lines.Add("extensions     " + string.Join(' ', keys.Select(k => k + (gl.HasExtension(k) ? "=yes" : "=no"))));
            foreach (var k in keys) json["ext_" + k] = gl.HasExtension(k);
            if (info.NvxMemInfo)
            {
                long Kb(uint p) => gl.GetKb(p);
                long ded = Kb(GlApi.GL_GPU_MEMORY_INFO_DEDICATED_VIDMEM_NVX), tot = Kb(GlApi.GL_GPU_MEMORY_INFO_TOTAL_AVAILABLE_MEMORY_NVX), cur = Kb(GlApi.GL_GPU_MEMORY_INFO_CURRENT_AVAILABLE_VIDMEM_NVX);
                lines.Add(string.Format(inv, "VRAM (NVX)     dedicated {0} MiB, total available {1} MiB, currently free {2} MiB{3}", ded / 1024, tot / 1024, cur / 1024,
                    info.IsSoftware ? " (llvmpipe: system-memory figures, not VRAM)" : ""));
                json["nvx_dedicated_kb"] = ded; json["nvx_total_available_kb"] = tot; json["nvx_current_available_kb"] = cur;
            }
        }
        else { lines.Add("GL entry points: UNAVAILABLE (" + glFailure + ")"); failures++; }

        // NVML snapshot
        if (nvml != null)
        {
            var s = nvml.Sample();
            lines.Add(string.Format(inv, "NVML sample    {0}: {1} MHz gfx, {2} MHz mem, {3} C, {4:F1} W of {5:F1} W, P{6}, util {7}%, clock events {8}",
                nvml.DeviceName, s.GfxClockMHz, s.MemClockMHz, s.TempC, s.PowerW, s.PowerLimitW, s.PState, s.GpuUtilPct, Nvml.ReasonNames(s.ThrottleReasons)));
            json["nvml_device"] = nvml.DeviceName; json["nvml_driver"] = nvml.DriverVersion; json["nvml_gfx_clock_mhz"] = s.GfxClockMHz;
            json["nvml_temp_c"] = s.TempC; json["nvml_power_w"] = Round(s.PowerW); json["nvml_pstate"] = s.PState;
            nvml.Dispose();
        }

        // Effects (compiled by the host's warm-up)
        int bad = _effects.Count(e => e.error != null);
        lines.Add(string.Format(inv, "effects        {0} compiled, {1} failed ({2:F0} ms warm-up)", _effects.Count - bad, bad, _effectsMs));
        foreach (var (n, e) in _effects.Where(e => e.error != null)) lines.Add($"  FAILED {n}: {e!.Message}");
        json["effects_ok"] = _effects.Count - bad; json["effects_failed"] = bad;
        failures += bad;

        // One slot-A scene: 24 frames with GPU timestamps, then a JPEG of the last frame.
        try
        {
            using var scene = _slotA.Create(null);
            long l0 = Stopwatch.GetTimestamp();
            scene.Load(_gd, _fx);
            double loadMs = (Stopwatch.GetTimestamp() - l0) * 1000.0 / Stopwatch.Frequency;
            using var renderer = new ForwardRenderer(_gd, _fx);
            scene.Configure(renderer.Settings);
            _shown?.Dispose();
            _shown = new RenderTarget2D(_gd, 640, 360, false, SurfaceFormat.Color, DepthFormat.None);
            var path = CameraPaths.For("A", scene);
            using var timer = gl is { HasTimerQueries: true } ? new GpuTimer(gl) : null;
            var tap = new ContentTap(scene, timer);
            var gpuMs = new List<double>();
            for (int f = 0; f < 24; f++)
            {
                var pf = path.At(f / 24f);
                timer?.BeginFrame(f + 1);
                scene.Update(pf.SceneTime);
                timer?.Mark(PerfSession.MarkUpdateEnd);
                renderer.Render(tap, pf.Camera, SunModel.At(pf.Hour), _shown, pf.SceneTime);
                timer?.Mark(PerfSession.MarkRenderEnd);
                timer?.EndFrame();
                if (timer != null) { timer.Poll(); while (timer.TryDequeue(out var g)) gpuMs.Add(g.At(PerfSession.MarkRenderEnd) / 1e6); }
            }
            if (timer != null) { timer.FinishAll(); while (timer.TryDequeue(out var g)) gpuMs.Add(g.At(PerfSession.MarkRenderEnd) / 1e6); }
            var px = new Color[_shown.Width * _shown.Height]; _shown.GetData(px);
            int distinct = Gfx.Engine.Diagnostics.Capture.DistinctColors(px);
            var d = Stats.Of(gpuMs);
            lines.Add(string.Format(inv, "slot A render  {0} ({1}) 640x360: load {2:F0} ms, HDR target {3}, {4} distinct colours, GPU timer frames {5}/24 (dropped {6}), GPU p50 {7} [{8}]",
                _slotA.Spec, scene.GetType().Name, loadMs, renderer.HdrFormat, distinct, d.Count, timer?.Dropped ?? 0,
                double.IsNaN(d.P50) ? "n/a" : d.P50.ToString("F3", inv) + " ms", info.TimingLabel));
            json["slotA_scene"] = _slotA.Spec; json["slotA_load_ms"] = Round(loadMs); json["hdr_format"] = renderer.HdrFormat.ToString();
            json["distinct_colours"] = distinct; json["gpu_timer_frames"] = d.Count; json["gpu_ms_p50"] = Round(d.P50);
            if (distinct < 50) { lines.Add("  WARNING: the frame is nearly flat; rendering may be broken on this GPU"); failures++; }
            var jpg = _prefix + ".jpg";
            using (var fs = File.Create(jpg)) _shown.SaveAsJpeg(fs, _shown.Width, _shown.Height);
            lines.Add("capture        " + jpg);
        }
        catch (Exception e) when (e is not OutOfMemoryException)
        {
            lines.Add("slot A render  FAILED: " + e.GetType().Name + ": " + e.Message);
            json["slotA_error"] = e.ToString();
            failures++;
        }

        bool refused = _requireNvidia && !Validity.IsNvidia(info.Vendor, info.Renderer);
        ExitCode = refused ? 3 : failures > 0 ? 1 : 0;
        lines.Add("");
        lines.Add("VICTUS_CHECK=" + info.Validity.Label);
        lines.Add("HOST=" + (info.HostKind.StartsWith("launcher", StringComparison.Ordinal) ? "launcher" : "plain"));
        lines.Add("SELFTEST_FAILURES=" + failures.ToString(inv));
        if (refused) lines.Add("REFUSED: --require-nvidia and this process is not on an NVIDIA GPU (exit 3)");
        json["victus_check"] = info.Validity.Label; json["failures"] = failures; json["exit_code"] = ExitCode;

        var text = string.Join('\n', lines);
        File.WriteAllText(_prefix + ".txt", text.Replace("\n", "\r\n", StringComparison.Ordinal), new UTF8Encoding(false));
        WriteJson(_prefix + ".json", json);
        foreach (var l in lines) _log.Line(l);
        _log.Line("wrote " + _prefix + ".txt, .json, .jpg");
        Done = true;
        WindowTitle = "CivSim benchmark - GPU self-test: " + info.Validity.Label;
        return _shown;
    }

    static object? Round(double v) => double.IsNaN(v) || double.IsInfinity(v) ? null : Math.Round(v, 3);

    /// <summary>Flat JSON object without reflection (the Windows package is trimmed).</summary>
    static void WriteJson(string path, Dictionary<string, object?> map)
    {
        using var fs = new FileStream(path, FileMode.Create, FileAccess.Write, FileShare.Read);
        using var w = new Utf8JsonWriter(fs, new JsonWriterOptions { Indented = true });
        w.WriteStartObject();
        foreach (var k in map.Keys.OrderBy(x => x, StringComparer.Ordinal))
        {
            switch (map[k])
            {
                case null: w.WriteNull(k); break;
                case string s: w.WriteString(k, s); break;
                case bool b: w.WriteBoolean(k, b); break;
                case int i: w.WriteNumber(k, i); break;
                case long l: w.WriteNumber(k, l); break;
                case double d: w.WriteNumber(k, d); break;
                default: w.WriteString(k, Convert.ToString(map[k], CultureInfo.InvariantCulture)); break;
            }
        }
        w.WriteEndObject();
    }

    public void AfterBlit(long blitTicks) { }
    public void AfterPresent(long presentTicks) { }
    public void Dispose() { _shown?.Dispose(); _shown = null; }
}
