using System.Globalization;

namespace Gfx.Perf;

/// <summary>Parsed benchmark command line. <see cref="Parse"/> returns null (exit code 2) on any bad option.</summary>
public sealed class PerfOptions
{
    public string Mode { get; private set; } = "";        // scene | sweep | suite | gpu-selftest | list-configs | help
    public string? Target { get; private set; }           // slot or scene spec | sweep name | suite name
    public string? Bind { get; private set; }
    public IReadOnlyList<char>? Slots { get; private set; }
    public (int w, int h)? Size { get; private set; }
    public float? Lod { get; private set; }
    public ShadowMode? Shadows { get; private set; }
    public int? Crowd { get; private set; }
    public int? Rounds { get; private set; }
    public int? Warmup { get; private set; }
    public int? Frames { get; private set; }
    public double? WarmupSeconds { get; private set; }
    public string? Out { get; private set; }
    public string? Log { get; private set; }
    public bool NoCapture { get; private set; }
    public bool RequireNvidia { get; private set; }
    public (int w, int h) Window { get; private set; } = (960, 540);
    public bool Vsync { get; private set; }
    public int SimulateCloseAfter { get; private set; } = -1;

    static readonly string[] ModeOptions = { "--scene", "--sweep", "--suite", "--gpu-selftest", "--list-configs", "--perf-help" };

    /// <summary>True when the command line asks for the benchmark harness (Gfx.Demo forwards it here).</summary>
    public static bool Handles(string[] args) => args.Any(a => ModeOptions.Contains(a, StringComparer.Ordinal));

    public const string Usage = @"Benchmark harness (G2 perf stage; Victus benchmark, DR48):
  --suite <smoke|quick|standard>    smoke: A-E at 1280x720, 10+30 frames; quick: A-E at 1920x1080, 60+300 frames;
                                    standard: 39 configurations x 3 rounds, 90 warm-up + 600 measured frames each
  --sweep <resolution|lod|shadows|crowd>   one sweep (baseline included), 1 round
  --scene <A-E|scene-spec>          one configuration (defaults: 1920x1080, LOD x1, default shadows)
  --gpu-selftest                    GL strings, timer queries, VRAM info, NVML, effect compile, one slot-A frame
  --list-configs                    print the configurations of the chosen --suite/--sweep/--scene and exit (no GPU)
options:
  --bind A=units-closeup,B=crowd:1000,...   map scene slots to scenes (registered name from --list, or crowd:N)
  --slots A,C        restrict a suite/sweep to these slots      --size WxH  --lod MULT  --shadows off|1024|default|4096
  --crowd N          (--scene on a crowd binding)              --rounds N  --warmup N  --frames N  --warmup-seconds S
                     (standard/quick/sweeps/--scene warm up for at least 90 frames AND 2 s; --warmup N alone drops the 2 s)
  --out DIR          results directory (default ./results); with --gpu-selftest: a path prefix for .txt/.json/.jpg
  --log FILE         also append all console output to FILE    --no-capture   skip the JPEG captures
  --require-nvidia   refuse (exit 3) unless GL_VENDOR/GL_RENDERER is NVIDIA
  --window WxH       preview window size (default 960x540; the measured resolution is --size, rendered offscreen)
  --vsync            request vsync (default off; for checking the VSYNC flag only)
exit codes: 0 finished, 1 error, 2 bad options, 3 refused (--require-nvidia on another GPU), 4 window closed early (results kept)
Result files are labelled VALID_VICTUS_RTX4050 only when GL_RENDERER names the NVIDIA RTX 4050; anything else is INVALID_NOT_RTX4050.";

    public static PerfOptions? Parse(string[] args, out string? error)
    {
        var o = new PerfOptions(); error = null;
        string? Need(ref int i, string name, out string? err)
        {
            err = null;
            if (i + 1 >= args.Length || args[i + 1].StartsWith("--", StringComparison.Ordinal)) { err = name + " needs a value"; return null; }
            return args[++i];
        }
        bool SetMode(string m, string? target, ref string? err)
        {
            if (o.Mode.Length > 0) { err = $"only one of --scene/--sweep/--suite/--gpu-selftest/--list-configs (got {o.Mode} and {m})"; return false; }
            o.Mode = m; o.Target = target; return true;
        }
        bool listConfigs = false;
        for (int i = 0; i < args.Length; i++)
        {
            string a = args[i]; string? v; string? err = null;
            switch (a)
            {
                case "--scene": case "--sweep": case "--suite":
                    v = Need(ref i, a, out err); if (v == null) { error = err; return null; }
                    if (!SetMode(a[2..], v, ref err)) { error = err; return null; }
                    break;
                case "--gpu-selftest": if (!SetMode("gpu-selftest", null, ref err)) { error = err; return null; } break;
                case "--perf-help": if (!SetMode("help", null, ref err)) { error = err; return null; } break;
                case "--list-configs": listConfigs = true; break;
                case "--bind": v = Need(ref i, a, out err); if (v == null) { error = err; return null; } o.Bind = v; break;
                case "--slots":
                {
                    v = Need(ref i, a, out err); if (v == null) { error = err; return null; }
                    var list = new List<char>();
                    foreach (var p in v.Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries))
                    {
                        if (p.Length != 1 || !Suites.Slots.Contains(char.ToUpperInvariant(p[0]))) { error = $"--slots: '{p}' is not a slot (A-E)"; return null; }
                        char c = char.ToUpperInvariant(p[0]); if (!list.Contains(c)) list.Add(c);
                    }
                    if (list.Count == 0) { error = "--slots: empty"; return null; }
                    o.Slots = list; break;
                }
                case "--size": case "--window":
                {
                    v = Need(ref i, a, out err); if (v == null) { error = err; return null; }
                    var p = v.Split('x');
                    if (p.Length != 2 || !int.TryParse(p[0], NumberStyles.None, CultureInfo.InvariantCulture, out int w) || !int.TryParse(p[1], NumberStyles.None, CultureInfo.InvariantCulture, out int h)
                        || w < 16 || h < 16 || w > 8192 || h > 8192) { error = $"{a}: '{v}' is not WxH (16..8192)"; return null; }
                    if (a == "--size") o.Size = (w, h); else o.Window = (w, h);
                    break;
                }
                case "--lod":
                    v = Need(ref i, a, out err); if (v == null) { error = err; return null; }
                    if (!float.TryParse(v, NumberStyles.Float, CultureInfo.InvariantCulture, out float m) || !(m >= 0.05f && m <= 20f)) { error = $"--lod: '{v}' is not a multiplier in 0.05..20"; return null; }
                    o.Lod = m; break;
                case "--shadows":
                    v = Need(ref i, a, out err); if (v == null) { error = err; return null; }
                    o.Shadows = v switch { "off" => ShadowMode.Off, "1024" => ShadowMode.S1024, "default" or "2048" => ShadowMode.Default, "4096" => ShadowMode.S4096, _ => null };
                    if (o.Shadows == null) { error = $"--shadows: '{v}' is not off|1024|default|4096"; return null; }
                    break;
                case "--crowd": case "--rounds": case "--warmup": case "--frames": case "--simulate-close-after":
                {
                    v = Need(ref i, a, out err); if (v == null) { error = err; return null; }
                    if (!int.TryParse(v, NumberStyles.None, CultureInfo.InvariantCulture, out int n)) { error = $"{a}: '{v}' is not a non-negative integer"; return null; }
                    switch (a)
                    {
                        case "--crowd": if (n < 1 || n > 100_000) { error = "--crowd: 1..100000"; return null; } o.Crowd = n; break;
                        case "--rounds": if (n < 1 || n > 50) { error = "--rounds: 1..50"; return null; } o.Rounds = n; break;
                        case "--warmup": if (n > 100_000) { error = "--warmup: 0..100000"; return null; } o.Warmup = n; break;
                        case "--frames": if (n < 1 || n > 1_000_000) { error = "--frames: 1..1000000"; return null; } o.Frames = n; break;
                        default: if (n < 1) { error = "--simulate-close-after: >= 1"; return null; } o.SimulateCloseAfter = n; break;
                    }
                    break;
                }
                case "--warmup-seconds":
                    v = Need(ref i, a, out err); if (v == null) { error = err; return null; }
                    if (!double.TryParse(v, NumberStyles.Float, CultureInfo.InvariantCulture, out double ws) || !(ws >= 0 && ws <= 600)) { error = $"--warmup-seconds: '{v}' is not 0..600"; return null; }
                    o.WarmupSeconds = ws; break;
                case "--out": v = Need(ref i, a, out err); if (v == null) { error = err; return null; } o.Out = v; break;
                case "--log": v = Need(ref i, a, out err); if (v == null) { error = err; return null; } o.Log = v; break;
                case "--no-capture": o.NoCapture = true; break;
                case "--require-nvidia": o.RequireNvidia = true; break;
                case "--vsync": o.Vsync = true; break;
                default:
                    error = a.StartsWith("--", StringComparison.Ordinal) ? $"unknown option '{a}'" : $"unexpected argument '{a}'";
                    return null;
            }
        }
        if (listConfigs)
        {
            if (o.Mode is "gpu-selftest" or "help") { error = "--list-configs needs --suite, --sweep or --scene"; return null; }
            if (o.Mode.Length == 0) { o.Mode = "suite"; o.Target = "standard"; }
            o.ListOnly = true;
        }
        if (o.Mode.Length == 0) { error = "no mode: give --suite, --sweep, --scene or --gpu-selftest"; return null; }
        if (o.Mode == "suite" && Suites.ByName(o.Target!) == null) { error = $"--suite: unknown suite '{o.Target}' (smoke|quick|standard)"; return null; }
        if (o.Mode == "sweep" && !Suites.SweepNames.Contains(o.Target!, StringComparer.Ordinal)) { error = $"--sweep: unknown sweep '{o.Target}' ({string.Join('|', Suites.SweepNames)})"; return null; }
        if (o.Mode != "scene" && (o.Size != null || o.Lod != null || o.Shadows != null || o.Crowd != null))
        { error = "--size/--lod/--shadows/--crowd apply to --scene only (suites and sweeps have fixed configurations)"; return null; }
        if (o.Mode == "scene" && o.Slots != null) { error = "--slots applies to --suite/--sweep"; return null; }
        return o;
    }

    public bool ListOnly { get; private set; }
}
