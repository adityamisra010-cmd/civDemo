using System.Globalization;
using System.Reflection;
using Gfx.Engine.Hosting;
using Gfx.Engine.Scene;
using Gfx.Perf.Analysis;
using Gfx.Perf.Output;
using Gfx.Perf.Runtime;
using Gfx.Perf.Scenes;

namespace Gfx.Perf;

/// <summary>
/// Benchmark entry point. Gfx.Demo forwards <c>--suite/--sweep/--scene/--gpu-selftest/--list-configs</c> here.
/// Exit codes: 0 finished, 1 error, 2 bad options, 3 refused (--require-nvidia on another GPU), 4 window closed
/// early (results kept).
/// </summary>
public static class PerfCli
{
    public const string HarnessVersion = "g2-perf-1";
    public const int ExitOk = 0, ExitError = 1, ExitBadOptions = 2, ExitRefused = 3, ExitWindowClosed = 4;

    public static bool Handles(string[] args) => PerfOptions.Handles(args);

    /// <summary>Builds the plan without touching the GPU (also used by --list-configs and the tests).</summary>
    public static PerfPlan? BuildPlan(PerfOptions o, IReadOnlyList<SceneRegistry.Entry> registry, IReadOnlyList<string> args, out string? error)
    {
        var map = SlotBindings.Parse(o.Bind, out error);
        if (map == null) return null;
        var bindings = new SortedDictionary<char, SceneBinding>();
        SuiteSpec? suite;
        switch (o.Mode)
        {
            case "suite": suite = Suites.ByName(o.Target!); break;
            case "sweep": suite = Suites.Sweep(o.Target!, o.Slots); break;
            case "scene":
            {
                string t = o.Target!;
                char slot; string? spec = null;
                if (t.Length == 1 && Suites.Slots.Contains(char.ToUpperInvariant(t[0]))) slot = char.ToUpperInvariant(t[0]);
                else
                {
                    // A scene spec: run it on the slot it is the default for, else on slot C's path (orbit of its first shot).
                    slot = SlotBindings.Defaults.FirstOrDefault(kv => kv.Value == t).Key;
                    if (slot == default) slot = t.StartsWith("crowd:", StringComparison.Ordinal) ? 'B' : 'C';
                    spec = t;
                }
                var dict = new SortedDictionary<char, string>(map.ToDictionary(k => k.Key, v => v.Value));
                if (spec != null) dict[slot] = spec;
                map = dict;
                var (w, h) = o.Size ?? (Suites.BaseW, Suites.BaseH);
                var cfg = new RunConfig($"scene-{slot}-{w}x{h}", "single", slot, w, h, o.Lod ?? 1f, o.Shadows ?? ShadowMode.Default, o.Crowd, slot.ToString(), true);
                suite = new SuiteSpec("scene", 1, Suites.StandardWarmup, Suites.StandardFrames, new[] { cfg }, Suites.StandardWarmupSeconds);
                break;
            }
            case "gpu-selftest": suite = new SuiteSpec("gpu-selftest", 1, 0, 1, Array.Empty<RunConfig>()); break;
            default: error = "no mode"; return null;
        }
        if (suite == null) { error = "unknown suite or sweep"; return null; }
        if (o.Slots != null && o.Mode == "suite")
            suite = suite with { Configs = suite.Configs.Where(c => o.Slots.Contains(c.Slot)).ToArray() };
        if (o.Mode == "suite" && o.Slots != null && suite.Configs.Count == 0) { error = "--slots removed every configuration"; return null; }
        suite = suite with
        {
            Rounds = o.Rounds ?? suite.Rounds, Warmup = o.Warmup ?? suite.Warmup, Frames = o.Frames ?? suite.Frames,
            WarmupMinSeconds = o.WarmupSeconds ?? (o.Warmup != null ? 0 : suite.WarmupMinSeconds),
        };
        foreach (var (slot, spec) in map)
        {
            var b = SlotBindings.Resolve(slot, spec, registry, out error);
            if (b == null) return null;
            bindings[slot] = b;
        }
        foreach (var c in suite.Configs)
            if (c.Crowd != null && !bindings[c.Slot].SupportsCrowd)
            { error = $"configuration {c.Id} sets a crowd size, but slot {c.Slot} is bound to '{bindings[c.Slot].Spec}' (use a crowd:N binding, or --slots without {c.Slot})"; return null; }
        error = null;
        return new PerfPlan
        {
            Suite = suite, Bindings = bindings, OutDir = Path.GetFullPath(o.Out ?? "results"), Mode = o.Mode + (o.Target != null ? ":" + o.Target : ""),
            Capture = !o.NoCapture, Args = args.ToArray(), SimulateCloseAfterFrames = o.SimulateCloseAfter,
        };
    }

    public static int Run(string[] args, Assembly root)
    {
        CultureInfo.DefaultThreadCurrentCulture = CultureInfo.InvariantCulture;
        CultureInfo.CurrentCulture = CultureInfo.InvariantCulture;
        var o = PerfOptions.Parse(args, out var err);
        if (o == null) { Console.Error.WriteLine("bad options: " + err); Console.Error.WriteLine(PerfOptions.Usage); return ExitBadOptions; }
        if (o.Mode == "help") { Console.WriteLine(PerfOptions.Usage); return ExitOk; }
        var registry = SceneRegistry.Discover(root);
        var plan = BuildPlan(o, registry, args, out err);
        if (plan == null) { Console.Error.WriteLine("bad options: " + err); return ExitBadOptions; }
        if (o.ListOnly)
        {
            Console.WriteLine($"{plan.Suite.Name}: {plan.Suite.Configs.Count} configurations x {plan.Suite.Rounds} round(s), {plan.Suite.Warmup} warm-up + {plan.Suite.Frames} measured frames"
                + (plan.Suite.WarmupMinSeconds > 0 ? $" (warm-up at least {plan.Suite.WarmupMinSeconds.ToString("0.#", CultureInfo.InvariantCulture)} s)" : ""));
            foreach (var b in plan.Bindings.Values) Console.WriteLine($"slot {b.Slot} ({SlotBindings.SlotTitle(b.Slot)}) = {b.Spec}");
            foreach (var c in plan.Suite.Configs) Console.WriteLine("  " + c);
            return ExitOk;
        }

        HeadlessEnvironment.Configure(); // Linux without a display only: SDL offscreen + Mesa software GL
        bool selftest = o.Mode == "gpu-selftest";
        string prefix = Path.GetFullPath(o.Out ?? Path.Combine("results", "selftest"));
        using var log = new PerfLog(o.Log);
        log.Line($"CivSim G2 benchmark harness {HarnessVersion} - {string.Join(' ', args)}");
        PerfSession? session = null; GpuSelfTest? st = null; int exit = ExitError; bool refused = false;
        try
        {
            using var game = new BenchGame(o.Window.w, o.Window.h, o.Vsync, root, (g, gd, fx, warm, effectsMs, deviceMs) =>
            {
                if (selftest)
                {
                    st = new GpuSelfTest(gd, fx, log, prefix, warm, effectsMs, deviceMs, plan.Bindings['A'], o.RequireNvidia, o.Vsync);
                    return st;
                }
                foreach (var (name, e) in warm.Where(x => x.error != null)) log.Line($"effect {name} FAILED to compile: {e!.Message}");
                session = PerfSession.Start(gd, fx, plan, log, o.Vsync, effectsMs, deviceMs);
                session.SimulateCloseRequested += () => g.PushQuitEvent();
                if (o.RequireNvidia && !Validity.IsNvidia(session.Info.Vendor, session.Info.Renderer))
                {
                    refused = true;
                    log.Line("REFUSED (exit 3): --require-nvidia, but GL_RENDERER is '" + session.Info.Renderer + "'. Nothing was measured.");
                    session.Finish("refused: not an NVIDIA GPU", ExitRefused);
                }
                return session;
            });
            game.OnEnd = g =>
            {
                if (session is { Done: false } s)
                {
                    if (g.Failure != null) s.Finish("error: " + g.Failure.GetType().Name + ": " + g.Failure.Message, ExitError);
                    else s.Finish("window closed early", ExitWindowClosed);
                }
            };
            game.Run();
            game.End();
            if (game.Failure != null) log.Line("ERROR: " + game.Failure);
            if (selftest) exit = st?.ExitCode ?? ExitError;
            else if (refused) exit = ExitRefused;
            else if (session != null) exit = session.ExitCode;
            if (game.Failure != null && exit == ExitOk) exit = ExitError;
        }
        catch (Exception e)
        {
            log.Line("ERROR: " + e);
            try { session?.Finish("error: " + e.GetType().Name + ": " + e.Message, ExitError); } catch (Exception e2) { log.Line("could not write results: " + e2.Message); }
            exit = session?.Done == true && session.ExitCode == ExitWindowClosed ? ExitWindowClosed : ExitError;
        }
        log.Line($"exit code {exit}");
        return exit;
    }
}
