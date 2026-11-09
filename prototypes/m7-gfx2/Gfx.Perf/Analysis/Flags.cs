using System.Globalization;
using System.Text.RegularExpressions;
using Gfx.Perf.Measurement;

namespace Gfx.Perf.Analysis;

public static class FlagNames
{
    // The six measurement-condition flags (names are part of the result format).
    public const string OnBattery = "ON_BATTERY";
    public const string WindowNotFocused = "WINDOW_NOT_FOCUSED";
    public const string ThermalOrPowerThrottle = "THERMAL_OR_POWER_THROTTLE";
    public const string VsyncOrFrameCapSuspected = "VSYNC_OR_FRAME_CAP_SUSPECTED";
    public const string GpuTimerFramesDropped = "GPU_TIMER_FRAMES_DROPPED";
    public const string Gen2GcDuringMeasurement = "GEN2_GC_DURING_MEASUREMENT";
    // Additional flags (this harness).
    public const string GpuTimersUnavailable = "GPU_TIMERS_UNAVAILABLE";
    public const string GpuTimerNonMonotonic = "GPU_TIMER_NON_MONOTONIC";
    public const string SoftwareRenderer = "SOFTWARE_RENDERER";
    public const string IntelIgpu = "INTEL_IGPU";
    public const string NotRtx4050 = "NOT_RTX4050";
    public const string DebugBuild = "DEBUG_BUILD";

    public static readonly string[] Measurement = { OnBattery, WindowNotFocused, ThermalOrPowerThrottle, VsyncOrFrameCapSuspected, GpuTimerFramesDropped, Gen2GcDuringMeasurement };
}

/// <summary>Everything the per-run flags are computed from (pure data; see <see cref="Flags.Evaluate"/>).</summary>
public sealed record FlagInputs(
    double[] FrameMs, double CpuBusyP50, double GpuP50,
    bool TimersAvailable, long TimerFramesDropped, int FramesWithoutGpuTime, int NonMonotonicFrames,
    bool?[] PowerOnBattery, bool?[] FrameActive,
    NvmlSample[]? Nvml, int Gen2Collections);

public sealed record FlagResult(IReadOnlyList<string> Raised, IReadOnlyList<string> NotEvaluated, IReadOnlyList<string> Notes);

public static class Flags
{
    /// <summary>
    /// Common display refresh rates and frame-cap values (Hz) a pinned frame interval is compared with. Half rates of
    /// the usual panels (60 → 30, 120 → 60, 144 → 72) are already in the list; arbitrary half rates are not, because
    /// with them almost every interval between 10 and 20 ms would be "near" some period.
    /// </summary>
    public static readonly int[] RefreshRates = { 24, 30, 48, 50, 60, 72, 75, 90, 100, 120, 144, 165, 180, 240, 360 };

    public static FlagResult Evaluate(FlagInputs i)
    {
        var raised = new List<string>(); var ne = new List<string>(); var notes = new List<string>();
        var inv = CultureInfo.InvariantCulture;

        // ON_BATTERY
        var known = i.PowerOnBattery.Where(b => b.HasValue).ToArray();
        if (known.Length == 0) ne.Add(FlagNames.OnBattery + " (power state unknown on this platform)");
        else if (known.Any(b => b == true)) { raised.Add(FlagNames.OnBattery); notes.Add("running on battery during this run: laptop GPUs clock down on battery"); }

        // WINDOW_NOT_FOCUSED
        var act = i.FrameActive.Where(b => b.HasValue).ToArray();
        if (act.Length == 0) ne.Add(FlagNames.WindowNotFocused + " (no window: headless driver)");
        else
        {
            int inactive = act.Count(b => b == false);
            if (inactive > 0) { raised.Add(FlagNames.WindowNotFocused); notes.Add(string.Format(inv, "window not focused in {0} of {1} measured frames", inactive, act.Length)); }
        }

        // THERMAL_OR_POWER_THROTTLE
        if (i.Nvml is null) ne.Add(FlagNames.ThermalOrPowerThrottle + " (NVML unavailable)");
        else
        {
            var withReasons = i.Nvml.Where(s => s.ThrottleKnown).ToArray();
            if (withReasons.Length == 0) ne.Add(FlagNames.ThermalOrPowerThrottle + " (no NVML samples with clock-event reasons in the measured window)");
            else
            {
                int hits = withReasons.Count(s => (s.ThrottleReasons & Nvml.ThermalOrPowerMask) != 0);
                if (hits > 0)
                {
                    raised.Add(FlagNames.ThermalOrPowerThrottle);
                    ulong all = 0; foreach (var s in withReasons) all |= s.ThrottleReasons & Nvml.ThermalOrPowerMask;
                    notes.Add(string.Format(inv, "power/thermal clock limits in {0} of {1} NVML samples ({2})", hits, withReasons.Length, Nvml.ReasonNames(all)));
                }
            }
        }

        // VSYNC_OR_FRAME_CAP_SUSPECTED
        if (i.FrameMs.Length < 20) ne.Add(FlagNames.VsyncOrFrameCapSuspected + " (fewer than 20 measured frames)");
        else if (VsyncSuspected(i.FrameMs, i.CpuBusyP50, i.GpuP50, out var why)) { raised.Add(FlagNames.VsyncOrFrameCapSuspected); notes.Add(why); }

        // GPU timers
        if (!i.TimersAvailable) raised.Add(FlagNames.GpuTimersUnavailable);
        else
        {
            if (i.TimerFramesDropped > 0 || i.FramesWithoutGpuTime > 0)
            {
                raised.Add(FlagNames.GpuTimerFramesDropped);
                notes.Add(string.Format(inv, "{0} measured frames have no GPU time ({1} dropped by the query ring)", i.FramesWithoutGpuTime, i.TimerFramesDropped));
            }
            if (i.NonMonotonicFrames > 0) { raised.Add(FlagNames.GpuTimerNonMonotonic); notes.Add(string.Format(inv, "{0} frames had non-monotonic GPU timestamps (excluded)", i.NonMonotonicFrames)); }
        }

        // GEN2_GC_DURING_MEASUREMENT
        if (i.Gen2Collections > 0) { raised.Add(FlagNames.Gen2GcDuringMeasurement); notes.Add(string.Format(inv, "{0} gen-2 garbage collection(s) inside the measured frames", i.Gen2Collections)); }

        return new FlagResult(raised, ne, notes);
    }

    /// <summary>
    /// A frame interval pinned to a display period (or twice it) with little spread, while the app's own CPU and
    /// GPU work is well below the interval, means something other than the app paces the frames: vsync, a driver
    /// frame cap or a blocking present.
    /// </summary>
    public static bool VsyncSuspected(double[] frameMs, double cpuBusyP50, double gpuP50, out string why)
    {
        var s = frameMs.Where(v => !double.IsNaN(v)).ToArray(); Array.Sort(s);
        why = "";
        if (s.Length < 20) return false;
        double f = Stats.NearestRank(s, 50), p10 = Stats.NearestRank(s, 10), p90 = Stats.NearestRank(s, 90);
        if (f <= 0) return false;
        double spread = (p90 - p10) / f;
        double bestRel = double.MaxValue; int bestHz = 0;
        foreach (int hz in RefreshRates)
        {
            double period = 1000.0 / hz, rel = Math.Abs(f - period) / period;
            if (rel < bestRel) { bestRel = rel; bestHz = hz; }
        }
        double work = Math.Max(double.IsNaN(cpuBusyP50) ? 0 : cpuBusyP50, double.IsNaN(gpuP50) ? 0 : gpuP50);
        double wait = (f - work) / f;
        bool suspected = bestRel < 0.03 && spread < 0.10 && wait > 0.20;
        if (suspected)
            why = string.Format(CultureInfo.InvariantCulture,
                "frame interval p50 {0:F2} ms is within {1:P1} of {2} Hz{3} with p10-p90 spread {4:P0}, while CPU work p50 {5:F2} ms and GPU p50 {6} leave {7:P0} of the frame waiting",
                f, bestRel, bestHz, "", spread, cpuBusyP50, double.IsNaN(gpuP50) ? "n/a" : gpuP50.ToString("F2", CultureInfo.InvariantCulture) + " ms", wait);
        return suspected;
    }
}

/// <summary>Is this run Victus evidence? Only when GL_RENDERER names the NVIDIA RTX 4050.</summary>
public sealed record Validity(bool ValidVictus, string Label, string Reason, string Kind)
{
    public const string ValidLabel = "VALID_VICTUS_RTX4050";
    public const string InvalidLabel = "INVALID_NOT_RTX4050";

    static readonly Regex Rtx4050 = new(@"RTX\s*4050", RegexOptions.IgnoreCase | RegexOptions.CultureInvariant);

    public static Validity Classify(string vendor, string renderer)
    {
        bool nvidia = renderer.Contains("NVIDIA", StringComparison.OrdinalIgnoreCase) || vendor.Contains("NVIDIA", StringComparison.OrdinalIgnoreCase);
        if (nvidia && Rtx4050.IsMatch(renderer))
            return new Validity(true, ValidLabel, "GL_RENDERER names the NVIDIA RTX 4050 (the Victus reference GPU)", "nvidia-rtx4050");
        if (IsSoftware(renderer))
            return new Validity(false, InvalidLabel, "software rasterizer (" + renderer + "): llvmpipe, not Victus evidence", "software");
        if (renderer.Contains("Intel", StringComparison.OrdinalIgnoreCase) || vendor.Contains("Intel", StringComparison.OrdinalIgnoreCase))
            return new Validity(false, InvalidLabel, "Intel integrated GPU (" + renderer + "): the RTX 4050 is NOT in use (Optimus picked the iGPU)", "intel-igpu");
        if (nvidia)
            return new Validity(false, InvalidLabel, "NVIDIA GPU, but not an RTX 4050 (" + renderer + ")", "nvidia-other");
        return new Validity(false, InvalidLabel, "not the RTX 4050 (" + vendor + " / " + renderer + ")", "other");
    }

    public static bool IsSoftware(string renderer) =>
        renderer.Contains("llvmpipe", StringComparison.OrdinalIgnoreCase) || renderer.Contains("softpipe", StringComparison.OrdinalIgnoreCase)
        || renderer.Contains("swrast", StringComparison.OrdinalIgnoreCase) || renderer.Contains("GDI Generic", StringComparison.OrdinalIgnoreCase)
        || renderer.Contains("Basic Render", StringComparison.OrdinalIgnoreCase);

    public static bool IsNvidia(string vendor, string renderer) =>
        renderer.Contains("NVIDIA", StringComparison.OrdinalIgnoreCase) || vendor.Contains("NVIDIA", StringComparison.OrdinalIgnoreCase);

    /// <summary>The loud banner printed on every non-Victus result (console, log, summary).</summary>
    public string Banner(string renderer)
    {
        if (ValidVictus)
            return "==== VALID_VICTUS_RTX4050: GL_RENDERER = " + renderer + " ====";
        var lines = new[]
        {
            "INVALID - NOT VICTUS EVIDENCE",
            "GL_RENDERER = " + renderer,
            Reason,
            "These numbers were NOT measured on the NVIDIA RTX 4050; result files are named " + InvalidLabel + ".",
        };
        int w = Math.Max(72, lines.Max(l => l.Length) + 6);
        var bar = new string('#', w);
        var sb = new System.Text.StringBuilder();
        sb.Append(bar).Append('\n');
        foreach (var l in lines) sb.Append("## ").Append(l.PadRight(w - 6)).Append(" ##\n");
        sb.Append(bar);
        return sb.ToString();
    }
}
