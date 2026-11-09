using System.Diagnostics;
using System.Globalization;
using System.Reflection;
using System.Runtime.InteropServices;
using Gfx.Engine.Diagnostics;
using Gfx.Perf.Analysis;
using Gfx.Perf.Measurement;
using Microsoft.Xna.Framework.Graphics;

namespace Gfx.Perf.Runtime;

/// <summary>Machine, GPU and process facts captured once per session (all read-only).</summary>
public sealed class SessionInfo
{
    public required string Vendor { get; init; }
    public required string Renderer { get; init; }
    public required string GlVersion { get; init; }
    public required string Glsl { get; init; }
    public required int ExtensionCount { get; init; }
    public required bool TimerQueries { get; init; }
    public required int TimestampBits { get; init; }
    public required bool NvxMemInfo { get; init; }
    public required bool AtiMemInfo { get; init; }
    public required string? GlFailure { get; init; }
    public required Validity Validity { get; init; }
    public required string MonoGame { get; init; }
    public required string Profile { get; init; }
    public required string CpuName { get; init; }
    public required int LogicalCpus { get; init; }
    public required long TotalMemoryBytes { get; init; }
    public required string Os { get; init; }
    public required string Runtime { get; init; }
    public required string ProcessPath { get; init; }
    public required string HostKind { get; init; }
    public required bool? ExportsNvOptimus { get; init; }
    public required bool? ExportsAmdPowerXpress { get; init; }
    public required string WindowsPowerMode { get; init; }
    public required PowerState Power { get; init; }
    public required string NvmlStatus { get; init; }
    public required string? NvmlDriver { get; init; }
    public required string? NvmlDevice { get; init; }
    public required string BuildConfiguration { get; init; }
    public required string HarnessVersion { get; init; }
    public required string SourceCommit { get; init; }
    public required int BackBufferWidth { get; init; }
    public required int BackBufferHeight { get; init; }
    public required bool VsyncRequested { get; init; }
    public required string StartedLocal { get; init; }
    public required string StartedUtc { get; init; }

    public bool IsSoftware => Validity.IsSoftware(Renderer);
    public bool IsIntel => Validity.Kind == "intel-igpu";
    public bool IsDebugBuild => BuildConfiguration.Contains("Debug", StringComparison.OrdinalIgnoreCase);
    /// <summary>Label stamped on every timing line printed or written by this session.</summary>
    public string TimingLabel => Validity.ValidVictus ? "VALID_VICTUS_RTX4050 (" + Renderer + ")"
        : IsSoftware ? "llvmpipe, not Victus evidence" : "INVALID: " + Renderer + ", not Victus evidence";

    /// <summary>Session-level flags (renderer and build), added to every run's flags.</summary>
    public IReadOnlyList<string> SessionFlags()
    {
        var l = new List<string>();
        if (!Validity.ValidVictus) l.Add(FlagNames.NotRtx4050);
        if (IsSoftware) l.Add(FlagNames.SoftwareRenderer);
        if (IsIntel) l.Add(FlagNames.IntelIgpu);
        if (IsDebugBuild) l.Add(FlagNames.DebugBuild);
        return l;
    }

    public static string BuildConfig()
    {
        var a = typeof(SessionInfo).Assembly;
        var cfg = a.GetCustomAttribute<AssemblyConfigurationAttribute>()?.Configuration ?? "unknown";
        return cfg;
    }

    /// <summary>Commit the build was made from, when the package script stamped it (env CIVSIM_SOURCE_COMMIT at build time), else "unknown".</summary>
    public static string Commit()
    {
        var a = typeof(SessionInfo).Assembly;
        foreach (var m in a.GetCustomAttributes<AssemblyMetadataAttribute>())
            if (m.Key == "SourceCommit" && !string.IsNullOrWhiteSpace(m.Value)) return m.Value!;
        return "unknown";
    }

    public static SessionInfo Collect(GraphicsDevice gd, GlApi? gl, string? glFailure, Nvml? nvml, string nvmlStatus, bool vsync)
    {
        var si = SysInfo.Collect(gd);
        string vendor = gl?.GetString(GlApi.GL_VENDOR) is { Length: > 0 } v ? v : si.GlVendor;
        string renderer = gl?.GetString(GlApi.GL_RENDERER) is { Length: > 0 } r ? r : si.GlRenderer;
        string version = gl?.GetString(GlApi.GL_VERSION) is { Length: > 0 } ver ? ver : si.GlVersion;
        string glsl = gl?.GetString(GlApi.GL_SHADING_LANGUAGE_VERSION) is { Length: > 0 } g ? g : si.GlslVersion;
        var (nv, amd) = SystemProbes.HostExportsGpuHints();
        string exe = Environment.ProcessPath ?? "unknown";
        string host = Path.GetFileNameWithoutExtension(exe).Equals("CivSimBench", StringComparison.OrdinalIgnoreCase) ? "launcher (CivSimBench.exe)"
            : Path.GetFileNameWithoutExtension(exe).Equals("dotnet", StringComparison.OrdinalIgnoreCase) ? "dotnet muxer" : "plain exe (.NET apphost)";
        var now = DateTimeOffset.Now; // presentation tooling: the run's wall-clock stamp for file names, never a sim input
        return new SessionInfo
        {
            Vendor = vendor, Renderer = renderer, GlVersion = version, Glsl = glsl,
            ExtensionCount = gl?.Extensions.Count ?? 0, TimerQueries = gl?.HasTimerQueries ?? false, TimestampBits = gl?.TimestampBits() ?? 0,
            NvxMemInfo = gl?.HasExtension("GL_NVX_gpu_memory_info") ?? false, AtiMemInfo = gl?.HasExtension("GL_ATI_meminfo") ?? false,
            GlFailure = glFailure, Validity = Validity.Classify(vendor, renderer), MonoGame = si.MonoGameVersion, Profile = si.Profile.ToString(),
            CpuName = SystemProbes.CpuName(), LogicalCpus = Environment.ProcessorCount, TotalMemoryBytes = si.TotalMemoryBytes,
            Os = RuntimeInformation.OSDescription, Runtime = RuntimeInformation.FrameworkDescription, ProcessPath = exe, HostKind = host,
            ExportsNvOptimus = nv, ExportsAmdPowerXpress = amd, WindowsPowerMode = SystemProbes.WindowsPowerMode(), Power = SystemProbes.ReadPower(),
            NvmlStatus = nvmlStatus, NvmlDriver = nvml?.DriverVersion, NvmlDevice = nvml?.DeviceName,
            BuildConfiguration = BuildConfig(), HarnessVersion = PerfCli.HarnessVersion, SourceCommit = Commit(),
            BackBufferWidth = gd.PresentationParameters.BackBufferWidth, BackBufferHeight = gd.PresentationParameters.BackBufferHeight,
            VsyncRequested = vsync,
            StartedLocal = now.ToString("yyyy-MM-dd HH:mm:ss zzz", CultureInfo.InvariantCulture),
            StartedUtc = now.UtcDateTime.ToString("yyyy-MM-ddTHH:mm:ssZ", CultureInfo.InvariantCulture),
        };
    }

    public string ToText()
    {
        var inv = CultureInfo.InvariantCulture;
        var l = new List<string>
        {
            "GL_VENDOR      " + Vendor,
            "GL_RENDERER    " + Renderer,
            "GL_VERSION     " + GlVersion,
            "GLSL           " + Glsl,
            "validity       " + Validity.Label + " - " + Validity.Reason,
            "timer queries  " + (TimerQueries ? $"yes ({TimestampBits}-bit GL_TIMESTAMP)" : "NO" + (GlFailure != null ? " (" + GlFailure + ")" : "")),
            "VRAM info      " + (NvxMemInfo ? "GL_NVX_gpu_memory_info" : AtiMemInfo ? "GL_ATI_meminfo" : "unavailable") + (IsSoftware ? " (llvmpipe reports system-memory figures, not VRAM)" : ""),
            "NVML           " + NvmlStatus + (NvmlDriver != null ? $" driver {NvmlDriver}, device {NvmlDevice}" : ""),
            "host           " + HostKind + " - " + ProcessPath,
            "Optimus export " + (ExportsNvOptimus is bool b ? (b ? "NvOptimusEnablement exported" : "not exported") : "n/a (not Windows)")
                + (ExportsAmdPowerXpress is bool c ? (c ? ", AmdPowerXpressRequestHighPerformance exported" : "") : ""),
            "power          " + (Power.OnBattery is bool ob ? (ob ? "ON BATTERY" : "AC power") : "unknown") + " (" + Power.Detail + ")",
            "Windows mode   " + WindowsPowerMode,
            "CPU            " + CpuName + string.Create(inv, $" ({LogicalCpus} logical)"),
            "RAM            " + string.Create(inv, $"{TotalMemoryBytes / (1024.0 * 1024 * 1024):F1} GiB visible to .NET"),
            "OS / runtime   " + Os + " / " + Runtime,
            "MonoGame       " + MonoGame + " (" + Profile + ")",
            "build          " + BuildConfiguration + ", harness " + HarnessVersion + ", source commit " + SourceCommit,
            "window         " + string.Create(inv, $"{BackBufferWidth}x{BackBufferHeight} preview, vsync requested {(VsyncRequested ? "ON" : "off")}"),
            "started        " + StartedLocal + " (" + StartedUtc + ")",
        };
        return string.Join('\n', l);
    }
}
