using System.Globalization;

namespace Gfx.Perf;

public enum ShadowMode { Off, S1024, Default, S4096 }

/// <summary>One measured configuration. <see cref="Path"/> names the fixed, frame-based camera path.</summary>
public sealed record RunConfig(string Id, string Sweep, char Slot, int Width, int Height, float LodMult, ShadowMode Shadows, int? Crowd, string Path, bool Capture)
{
    public string ShadowLabel => Shadows switch { ShadowMode.Off => "off", ShadowMode.S1024 => "1024", ShadowMode.S4096 => "4096", _ => "default" };
    public string Resolution => string.Create(CultureInfo.InvariantCulture, $"{Width}x{Height}");
    public string LodLabel => "x" + LodMult.ToString("0.##", CultureInfo.InvariantCulture);
    public override string ToString() => string.Create(CultureInfo.InvariantCulture,
        $"{Id}: slot {Slot} {Resolution} lod {LodLabel} shadows {ShadowLabel}{(Crowd is int n ? " crowd " + n : "")} path {Path}");
}

/// <summary>
/// A suite: configurations × rounds, a fixed number of measured frames and a warm-up of at least
/// <see cref="Warmup"/> frames AND at least <see cref="WarmupMinSeconds"/> seconds (GPU clocks ramp up after each
/// scene load; on a fast GPU 90 frames can take well under a second). Warm-up frames are never measured, so the
/// time-based part does not change which frames are measured.
/// </summary>
public sealed record SuiteSpec(string Name, int Rounds, int Warmup, int Frames, IReadOnlyList<RunConfig> Configs, double WarmupMinSeconds = 0);

/// <summary>
/// Suite and sweep definitions. Standard = 39 configurations × 3 rounds (rounds interleaved, so thermal drift
/// spreads over every configuration): resolution 15 (A–E × 1280x720/1920x1080/2560x1440), LOD 8 (B, C ×
/// 0.25/0.5/2/4; ×1 is the 1080p baseline), shadows 9 (A, C, E × off/1024/4096; "default" is the baseline) and
/// crowd 7 (500 … 16,000 soldiers on slot B's crowd scene, whole-army camera).
/// </summary>
public static class Suites
{
    public static readonly char[] Slots = { 'A', 'B', 'C', 'D', 'E' };
    public static readonly (int w, int h)[] Resolutions = { (1280, 720), (1920, 1080), (2560, 1440) };
    public static readonly float[] LodMultipliers = { 0.25f, 0.5f, 1f, 2f, 4f };
    public static readonly ShadowMode[] ShadowModes = { ShadowMode.Off, ShadowMode.S1024, ShadowMode.Default, ShadowMode.S4096 };
    public static readonly int[] CrowdSizes = { 500, 1000, 2000, 4000, 8000, 12000, 16000 };
    public static readonly char[] LodSlots = { 'B', 'C' };
    public static readonly char[] ShadowSlots = { 'A', 'C', 'E' };
    public const int BaseW = 1920, BaseH = 1080;
    public const int StandardRounds = 3, StandardWarmup = 90, StandardFrames = 600;
    public const double StandardWarmupSeconds = 2.0;

    static string F(float v) => v.ToString("0.##", CultureInfo.InvariantCulture);

    public static RunConfig Baseline(char slot, bool capture = true, int w = BaseW, int h = BaseH) =>
        new($"res-{slot}-{w}x{h}", "resolution", slot, w, h, 1f, ShadowMode.Default, null, slot.ToString(), capture);

    public static IEnumerable<RunConfig> ResolutionSweep(IEnumerable<char> slots) =>
        from s in slots from r in Resolutions
        select new RunConfig($"res-{s}-{r.w}x{r.h}", "resolution", s, r.w, r.h, 1f, ShadowMode.Default, null, s.ToString(), r.w == BaseW);

    public static IEnumerable<RunConfig> LodSweep(IEnumerable<char> slots, bool includeBaseline) =>
        from s in slots from m in LodMultipliers where includeBaseline || m != 1f
        select new RunConfig($"lod-{s}-x{F(m)}", "lod", s, BaseW, BaseH, m, ShadowMode.Default, null, s.ToString(), s == 'C' && (m == 0.25f || m == 4f));

    public static IEnumerable<RunConfig> ShadowSweep(IEnumerable<char> slots, bool includeBaseline) =>
        from s in slots from m in ShadowModes where includeBaseline || m != ShadowMode.Default
        select new RunConfig($"shd-{s}-{Label(m)}", "shadows", s, BaseW, BaseH, 1f, m, null, s.ToString(), false);

    public static IEnumerable<RunConfig> CrowdSweep() =>
        CrowdSizes.Select(n => new RunConfig($"crowd-{n}", "crowd", 'B', BaseW, BaseH, 1f, ShadowMode.Default, n, "B-wide", n == CrowdSizes[^1]));

    static string Label(ShadowMode m) => m switch { ShadowMode.Off => "off", ShadowMode.S1024 => "1024", ShadowMode.S4096 => "4096", _ => "default" };

    public static SuiteSpec Standard() => new("standard", StandardRounds, StandardWarmup, StandardFrames,
        ResolutionSweep(Slots).Concat(LodSweep(LodSlots, false)).Concat(ShadowSweep(ShadowSlots, false)).Concat(CrowdSweep()).ToArray(), StandardWarmupSeconds);

    public static SuiteSpec Quick() => new("quick", 1, 60, 300, Slots.Select(s => Baseline(s)).ToArray(), StandardWarmupSeconds);

    public static SuiteSpec Smoke() => new("smoke", 1, 10, 30, Slots.Select(s => Baseline(s, true, 1280, 720) with { Id = $"smoke-{s}-1280x720" }).ToArray());

    public static SuiteSpec? ByName(string name) => name switch { "standard" => Standard(), "quick" => Quick(), "smoke" => Smoke(), _ => null };

    /// <summary>A single sweep on its default slots (or <paramref name="slots"/>), baseline included.</summary>
    public static SuiteSpec? Sweep(string name, IReadOnlyList<char>? slots)
    {
        IEnumerable<RunConfig>? c = name switch
        {
            "resolution" => ResolutionSweep(slots ?? Slots),
            "lod" => LodSweep(slots ?? LodSlots, true),
            "shadows" => ShadowSweep(slots ?? ShadowSlots, true),
            "crowd" => CrowdSweep(),
            _ => null,
        };
        return c == null ? null : new SuiteSpec("sweep-" + name, 1, StandardWarmup, StandardFrames, c.Select(x => x with { Capture = false }).ToArray(), StandardWarmupSeconds);
    }

    public static readonly string[] SweepNames = { "resolution", "lod", "shadows", "crowd" };
}
