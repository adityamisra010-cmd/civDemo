using System.Diagnostics;
using System.Globalization;
using System.Runtime.InteropServices;

namespace Gfx.Perf.Measurement;

/// <summary>AC/battery state. <see cref="OnBattery"/> is null when the platform does not say.</summary>
public readonly record struct PowerState(bool? OnBattery, string Detail);

/// <summary>Machine facts and cheap system readings for the benchmark record (all read-only).</summary>
public static partial class SystemProbes
{
    // ---------- power ----------
    [StructLayout(LayoutKind.Sequential)]
    struct SystemPowerStatus { public byte ACLineStatus, BatteryFlag, BatteryLifePercent, SystemStatusFlag; public int BatteryLifeTime, BatteryFullLifeTime; }

    [LibraryImport("kernel32.dll")]
    [return: MarshalAs(UnmanagedType.Bool)]
    private static partial bool GetSystemPowerStatus(out SystemPowerStatus status);

    public static PowerState ReadPower()
    {
        try
        {
            if (OperatingSystem.IsWindows())
            {
                if (!GetSystemPowerStatus(out var s)) return new PowerState(null, "GetSystemPowerStatus failed");
                bool? onBattery = s.ACLineStatus switch { 0 => true, 1 => false, _ => null };
                string pct = s.BatteryLifePercent == 255 ? "unknown" : s.BatteryLifePercent.ToString(CultureInfo.InvariantCulture) + "%";
                return new PowerState(onBattery, $"ACLineStatus={s.ACLineStatus} battery={pct} flags=0x{s.BatteryFlag:X2}");
            }
            if (OperatingSystem.IsLinux())
            {
                const string root = "/sys/class/power_supply";
                if (!Directory.Exists(root)) return new PowerState(null, "no /sys/class/power_supply (container or desktop without supplies)");
                bool sawMains = false, mainsOnline = false, sawBattery = false; var parts = new List<string>();
                foreach (var d in Directory.GetDirectories(root).OrderBy(x => x, StringComparer.Ordinal))
                {
                    string type = ReadTrim(Path.Combine(d, "type")), online = ReadTrim(Path.Combine(d, "online"));
                    parts.Add($"{Path.GetFileName(d)}:{type}:{online}");
                    if (type == "Mains") { sawMains = true; if (online == "1") mainsOnline = true; }
                    if (type == "Battery") sawBattery = true;
                }
                bool? ob = sawMains ? !mainsOnline : (sawBattery ? null : (bool?)null);
                return new PowerState(ob, parts.Count == 0 ? "no power supplies listed" : string.Join(' ', parts));
            }
        }
        catch (Exception e) { return new PowerState(null, "power read failed: " + e.Message); }
        return new PowerState(null, "not supported on this OS");
    }

    static string ReadTrim(string path) { try { return File.Exists(path) ? File.ReadAllText(path).Trim() : ""; } catch (IOException) { return ""; } }

    // ---------- Windows power mode overlay (the Settings "Power mode" slider) ----------
    [LibraryImport("powrprof.dll")]
    private static partial uint PowerGetEffectiveOverlayScheme(out Guid overlay);

    public static string WindowsPowerMode()
    {
        if (!OperatingSystem.IsWindows()) return "n/a";
        try
        {
            if (PowerGetEffectiveOverlayScheme(out var g) != 0) return "unknown";
            return g.ToString("D") switch
            {
                "00000000-0000-0000-0000-000000000000" => "Balanced (no overlay)",
                "ded574b5-45a0-4f42-8737-46345c09c238" => "Best performance",
                "961cc777-2547-4f9d-8174-7d86181b8a7a" => "Better battery",
                "3af9b8d9-7c97-431d-ad78-34a8bfea439f" => "Best power efficiency",
                var other => "overlay " + other,
            };
        }
        catch (Exception e) when (e is DllNotFoundException or EntryPointNotFoundException) { return "unavailable"; }
    }

    // ---------- Optimus: does the host executable export NvOptimusEnablement? ----------
    [LibraryImport("kernel32.dll", EntryPoint = "GetModuleHandleW", StringMarshalling = StringMarshalling.Utf16)]
    private static partial IntPtr GetModuleHandle(string? name);

    [LibraryImport("kernel32.dll", StringMarshalling = StringMarshalling.Utf8)]
    private static partial IntPtr GetProcAddress(IntPtr module, string name);

    /// <summary>
    /// Whether the process image exports the Optimus / PowerXpress hints (true only under the CivSimBench.exe
    /// launcher; a .NET apphost cannot export data). Null off Windows.
    /// </summary>
    public static (bool? nv, bool? amd) HostExportsGpuHints()
    {
        if (!OperatingSystem.IsWindows()) return (null, null);
        try
        {
            var exe = GetModuleHandle(null);
            return (GetProcAddress(exe, "NvOptimusEnablement") != IntPtr.Zero, GetProcAddress(exe, "AmdPowerXpressRequestHighPerformance") != IntPtr.Zero);
        }
        catch (Exception) { return (null, null); }
    }

    // ---------- CPU ----------
    public static string CpuName()
    {
        try
        {
            if (OperatingSystem.IsWindows())
            {
                using var k = Microsoft.Win32.Registry.LocalMachine.OpenSubKey(@"HARDWARE\DESCRIPTION\System\CentralProcessor\0");
                return (k?.GetValue("ProcessorNameString") as string)?.Trim() ?? "unknown";
            }
            if (OperatingSystem.IsLinux() && File.Exists("/proc/cpuinfo"))
                foreach (var line in File.ReadLines("/proc/cpuinfo"))
                    if (line.StartsWith("model name", StringComparison.Ordinal)) return line[(line.IndexOf(':') + 1)..].Trim();
        }
        catch (Exception) { }
        return "unknown";
    }

    [LibraryImport("kernel32.dll")]
    [return: MarshalAs(UnmanagedType.Bool)]
    private static partial bool GetSystemTimes(out long idle, out long kernel, out long user);

    /// <summary>Cumulative system CPU times (busy, total) in platform units, or null.</summary>
    public static (double busy, double total)? SystemCpuTimes()
    {
        try
        {
            if (OperatingSystem.IsWindows())
            {
                if (!GetSystemTimes(out long idle, out long kernel, out long user)) return null;
                double total = kernel + user; // kernel time includes idle
                return (total - idle, total);
            }
            if (OperatingSystem.IsLinux() && File.Exists("/proc/stat"))
            {
                var first = File.ReadLines("/proc/stat").First();
                var f = first.Split(' ', StringSplitOptions.RemoveEmptyEntries).Skip(1).Select(x => double.Parse(x, CultureInfo.InvariantCulture)).ToArray();
                double idle = f[3] + (f.Length > 4 ? f[4] : 0), total = f.Sum();
                return (total - idle, total);
            }
        }
        catch (Exception) { }
        return null;
    }

    /// <summary>Process CPU utilization between two readings, as % of ALL logical CPUs and as % of one CPU.</summary>
    public sealed class CpuWindow
    {
        readonly TimeSpan _proc0; readonly long _t0; readonly (double busy, double total)? _sys0;
        public CpuWindow() { _proc0 = Process.GetCurrentProcess().TotalProcessorTime; _t0 = Stopwatch.GetTimestamp(); _sys0 = SystemCpuTimes(); }
        public (double processPctOfAll, double processPctOfOneCpu, double systemPct) Stop()
        {
            double wall = (Stopwatch.GetTimestamp() - _t0) / (double)Stopwatch.Frequency;
            double cpu = (Process.GetCurrentProcess().TotalProcessorTime - _proc0).TotalSeconds;
            double one = wall > 0 ? 100.0 * cpu / wall : double.NaN;
            double sys = double.NaN;
            var s1 = SystemCpuTimes();
            if (_sys0 is { } a && s1 is { } b && b.total > a.total) sys = 100.0 * (b.busy - a.busy) / (b.total - a.total);
            return (one / Environment.ProcessorCount, one, sys);
        }
    }

    // ---------- process memory ----------
    public readonly record struct MemSample(long WorkingSet, long GcHeap, long GcCommitted, long PrivateBytes);

    public static MemSample ReadMemory(bool includePrivate)
    {
        long priv = -1;
        if (includePrivate) { try { using var p = Process.GetCurrentProcess(); priv = p.PrivateMemorySize64; } catch (Exception) { } }
        var gi = GC.GetGCMemoryInfo();
        return new MemSample(Environment.WorkingSet, GC.GetTotalMemory(false), gi.TotalCommittedBytes, priv);
    }
}
