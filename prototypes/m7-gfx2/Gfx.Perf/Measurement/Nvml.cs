using System.Diagnostics;
using System.Runtime.InteropServices;
using System.Text;

namespace Gfx.Perf.Measurement;

/// <summary>One NVML reading. Fields that the driver does not report are -1 (NaN for doubles).</summary>
public readonly record struct NvmlSample(
    long Ticks, int GfxClockMHz, int SmClockMHz, int MemClockMHz, int TempC, double PowerW, double PowerLimitW,
    ulong ThrottleReasons, bool ThrottleKnown, int GpuUtilPct, int MemUtilPct, long MemUsedBytes, long MemTotalBytes, int PState);

/// <summary>
/// NVIDIA Management Library, loaded dynamically (nvml.dll on Windows, libnvidia-ml.so.1 on Linux) and only read
/// from. Absent on non-NVIDIA machines and in this container; every value is then reported as unavailable.
/// UNTESTED on real NVIDIA hardware in this stage (no GPU here): every call is defensive.
/// </summary>
public sealed unsafe class Nvml : IDisposable
{
    // nvmlClocksThrottleReasons / nvmlClocksEventReasons bits
    public const ulong GpuIdle = 0x1, ApplicationsClocksSetting = 0x2, SwPowerCap = 0x4, HwSlowdown = 0x8, SyncBoost = 0x10,
        SwThermalSlowdown = 0x20, HwThermalSlowdown = 0x40, HwPowerBrakeSlowdown = 0x80, DisplayClockSetting = 0x100;
    /// <summary>Reasons that mean the GPU is held below its clocks by power or temperature.</summary>
    public const ulong ThermalOrPowerMask = SwPowerCap | HwSlowdown | SwThermalSlowdown | HwThermalSlowdown | HwPowerBrakeSlowdown;

    readonly IntPtr _lib; readonly IntPtr _dev;
    readonly delegate* unmanaged<int> _shutdown;
    readonly delegate* unmanaged<IntPtr, int, uint*, int> _clock;
    readonly delegate* unmanaged<IntPtr, int, uint*, int> _temp;
    readonly delegate* unmanaged<IntPtr, uint*, int> _power, _powerLimit;
    readonly delegate* unmanaged<IntPtr, ulong*, int> _throttle;
    readonly delegate* unmanaged<IntPtr, uint*, int> _util;   // nvmlUtilization_t { uint gpu; uint memory; }
    readonly delegate* unmanaged<IntPtr, ulong*, int> _mem;   // nvmlMemory_t { ulong total; ulong free; ulong used; }
    readonly delegate* unmanaged<IntPtr, int*, int> _pstate;
    bool _disposed;

    public string LibraryPath { get; }
    public string DriverVersion { get; }
    public string NvmlVersion { get; }
    public string DeviceName { get; }
    public int DeviceIndex { get; }
    public int DeviceCount { get; }

    Nvml(IntPtr lib, string path)
    {
        _lib = lib; LibraryPath = path;
        IntPtr F(string name) => NativeLibrary.TryGetExport(lib, name, out var p) ? p : IntPtr.Zero;
        var init = F("nvmlInit_v2"); if (init == IntPtr.Zero) init = F("nvmlInit");
        if (init == IntPtr.Zero) throw new InvalidOperationException("nvmlInit not exported");
        int rc = ((delegate* unmanaged<int>)init)();
        if (rc != 0) throw new InvalidOperationException("nvmlInit returned " + rc);
        _shutdown = (delegate* unmanaged<int>)F("nvmlShutdown");
        DriverVersion = Str(F("nvmlSystemGetDriverVersion"));
        NvmlVersion = Str(F("nvmlSystemGetNVMLVersion"));
        var count = F("nvmlDeviceGetCount_v2"); if (count == IntPtr.Zero) count = F("nvmlDeviceGetCount");
        uint n = 0;
        if (count != IntPtr.Zero) ((delegate* unmanaged<uint*, int>)count)(&n);
        DeviceCount = (int)n;
        var byIndex = F("nvmlDeviceGetHandleByIndex_v2"); if (byIndex == IntPtr.Zero) byIndex = F("nvmlDeviceGetHandleByIndex");
        var getName = F("nvmlDeviceGetName");
        DeviceName = "unknown"; DeviceIndex = -1;
        var buf = stackalloc byte[256];
        for (uint i = 0; i < n && byIndex != IntPtr.Zero; i++)
        {
            IntPtr d = IntPtr.Zero;
            if (((delegate* unmanaged<uint, IntPtr*, int>)byIndex)(i, &d) != 0) continue;
            string name = "unknown";
            if (getName != IntPtr.Zero)
            {
                if (((delegate* unmanaged<IntPtr, byte*, uint, int>)getName)(d, buf, 256) == 0) name = Marshal.PtrToStringAnsi((IntPtr)buf) ?? "unknown";
            }
            // Prefer the RTX 4050 (the reference GPU); otherwise the first device.
            if (_dev == IntPtr.Zero || name.Contains("4050", StringComparison.Ordinal)) { _dev = d; DeviceName = name; DeviceIndex = (int)i; }
        }
        if (_dev == IntPtr.Zero) throw new InvalidOperationException("no NVML device");
        _clock = (delegate* unmanaged<IntPtr, int, uint*, int>)F("nvmlDeviceGetClockInfo");
        _temp = (delegate* unmanaged<IntPtr, int, uint*, int>)F("nvmlDeviceGetTemperature");
        _power = (delegate* unmanaged<IntPtr, uint*, int>)F("nvmlDeviceGetPowerUsage");
        _powerLimit = (delegate* unmanaged<IntPtr, uint*, int>)F("nvmlDeviceGetEnforcedPowerLimit");
        var thr = F("nvmlDeviceGetCurrentClocksEventReasons");
        if (thr == IntPtr.Zero) thr = F("nvmlDeviceGetCurrentClocksThrottleReasons");
        _throttle = (delegate* unmanaged<IntPtr, ulong*, int>)thr;
        _util = (delegate* unmanaged<IntPtr, uint*, int>)F("nvmlDeviceGetUtilizationRates");
        _mem = (delegate* unmanaged<IntPtr, ulong*, int>)F("nvmlDeviceGetMemoryInfo");
        _pstate = (delegate* unmanaged<IntPtr, int*, int>)F("nvmlDeviceGetPerformanceState");
    }

    static string Str(IntPtr fn)
    {
        if (fn == IntPtr.Zero) return "unavailable";
        var buf = stackalloc byte[96];
        return ((delegate* unmanaged<byte*, uint, int>)fn)(buf, 96) == 0 ? Marshal.PtrToStringAnsi((IntPtr)buf) ?? "" : "unavailable";
    }

    /// <summary>Loads and initializes NVML, or returns null with the reason.</summary>
    public static Nvml? TryOpen(out string reason)
    {
        var candidates = new List<string>();
        if (OperatingSystem.IsWindows())
        {
            candidates.Add(Path.Combine(Environment.SystemDirectory, "nvml.dll"));
            var pf = Environment.GetEnvironmentVariable("ProgramW6432") ?? Environment.GetFolderPath(Environment.SpecialFolder.ProgramFiles);
            candidates.Add(Path.Combine(pf, "NVIDIA Corporation", "NVSMI", "nvml.dll"));
            candidates.Add("nvml.dll");
        }
        else if (OperatingSystem.IsLinux()) { candidates.Add("libnvidia-ml.so.1"); candidates.Add("libnvidia-ml.so"); }
        var why = new StringBuilder();
        foreach (var c in candidates)
        {
            if (!NativeLibrary.TryLoad(c, out var h)) { why.Append(c).Append(": not loadable; "); continue; }
            try { reason = "ok"; return new Nvml(h, c); }
            catch (Exception e) { why.Append(c).Append(": ").Append(e.Message).Append("; "); NativeLibrary.Free(h); }
        }
        reason = candidates.Count == 0 ? "NVML is not supported on this OS" : "NVML unavailable (" + why.ToString().TrimEnd(' ', ';') + ")";
        return null;
    }

    public NvmlSample Sample()
    {
        int Clock(int type) { if (_clock == null) return -1; uint v = 0; return _clock(_dev, type, &v) == 0 ? (int)v : -1; }
        int temp = -1; if (_temp != null) { uint v = 0; if (_temp(_dev, 0, &v) == 0) temp = (int)v; }
        double pw = double.NaN; if (_power != null) { uint v = 0; if (_power(_dev, &v) == 0) pw = v / 1000.0; }
        double pl = double.NaN; if (_powerLimit != null) { uint v = 0; if (_powerLimit(_dev, &v) == 0) pl = v / 1000.0; }
        ulong reasons = 0; bool known = false; if (_throttle != null) { ulong v = 0; if (_throttle(_dev, &v) == 0) { reasons = v; known = true; } }
        int gu = -1, mu = -1; if (_util != null) { uint* u = stackalloc uint[2]; if (_util(_dev, u) == 0) { gu = (int)u[0]; mu = (int)u[1]; } }
        long used = -1, total = -1; if (_mem != null) { ulong* m = stackalloc ulong[3]; if (_mem(_dev, m) == 0) { total = (long)m[0]; used = (long)m[2]; } }
        int ps = -1; if (_pstate != null) { int v = -1; if (_pstate(_dev, &v) == 0) ps = v; }
        return new NvmlSample(Stopwatch.GetTimestamp(), Clock(0), Clock(1), Clock(2), temp, pw, pl, reasons, known, gu, mu, used, total, ps);
    }

    /// <summary>Names of the set throttle/clock-event bits, e.g. "SW_POWER_CAP|HW_THERMAL".</summary>
    public static string ReasonNames(ulong r)
    {
        if (r == 0) return "none";
        var l = new List<string>();
        if ((r & GpuIdle) != 0) l.Add("GPU_IDLE");
        if ((r & ApplicationsClocksSetting) != 0) l.Add("APP_CLOCKS");
        if ((r & SwPowerCap) != 0) l.Add("SW_POWER_CAP");
        if ((r & HwSlowdown) != 0) l.Add("HW_SLOWDOWN");
        if ((r & SyncBoost) != 0) l.Add("SYNC_BOOST");
        if ((r & SwThermalSlowdown) != 0) l.Add("SW_THERMAL");
        if ((r & HwThermalSlowdown) != 0) l.Add("HW_THERMAL");
        if ((r & HwPowerBrakeSlowdown) != 0) l.Add("HW_POWER_BRAKE");
        if ((r & DisplayClockSetting) != 0) l.Add("DISPLAY_CLOCK");
        ulong known = GpuIdle | ApplicationsClocksSetting | SwPowerCap | HwSlowdown | SyncBoost | SwThermalSlowdown | HwThermalSlowdown | HwPowerBrakeSlowdown | DisplayClockSetting;
        if ((r & ~known) != 0) l.Add("0x" + (r & ~known).ToString("X", System.Globalization.CultureInfo.InvariantCulture));
        return string.Join('|', l);
    }

    public void Dispose()
    {
        if (_disposed) return;
        _disposed = true;
        try { if (_shutdown != null) _shutdown(); } catch (Exception) { }
        NativeLibrary.Free(_lib);
    }
}

/// <summary>Background NVML sampling (fixed period) so the GL thread never pays for NVML calls.</summary>
public sealed class NvmlSampler : IDisposable
{
    readonly Nvml _nvml; readonly Thread _thread; readonly int _periodMs;
    readonly List<NvmlSample> _samples = new();
    volatile bool _stop;

    public NvmlSampler(Nvml nvml, int periodMs = 200)
    {
        _nvml = nvml; _periodMs = periodMs;
        _thread = new Thread(Loop) { IsBackground = true, Name = "NvmlSampler", Priority = ThreadPriority.BelowNormal };
        _thread.Start();
    }

    void Loop()
    {
        while (!_stop)
        {
            try { var s = _nvml.Sample(); lock (_samples) _samples.Add(s); }
            catch (Exception) { /* keep sampling; a failed read is a missing sample */ }
            Thread.Sleep(_periodMs);
        }
    }

    /// <summary>Samples whose timestamp lies in [fromTicks, toTicks] (Stopwatch ticks).</summary>
    public NvmlSample[] Between(long fromTicks, long toTicks)
    {
        lock (_samples) return _samples.Where(s => s.Ticks >= fromTicks && s.Ticks <= toTicks).ToArray();
    }

    public void Dispose() { _stop = true; _thread.Join(2000); }
}
