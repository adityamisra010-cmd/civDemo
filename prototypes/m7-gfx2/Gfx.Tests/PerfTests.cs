using System.Diagnostics;
using System.Globalization;
using System.Text.Json;
using Gfx.Engine.Cameras;
using Gfx.Engine.Rendering;
using Gfx.Engine.Scene;
using Gfx.Perf;
using Gfx.Perf.Analysis;
using Gfx.Perf.Measurement;
using Gfx.Perf.Output;
using Gfx.Perf.Runtime;
using Gfx.Perf.Scenes;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;

namespace Gfx.Tests;

/// <summary>G2 perf stage: suites, options, labels, statistics, flags and output format (no GPU needed).</summary>
public class PerfUnitTests
{
    static IReadOnlyList<SceneRegistry.Entry> Registry => SceneRegistry.Discover(typeof(Gfx.Demo.Program).Assembly);

    [Fact]
    public void StandardSuiteIs39UniqueConfigurationsInTheDocumentedSweeps()
    {
        var s = Suites.Standard();
        Assert.Equal(39, s.Configs.Count);
        Assert.Equal((3, 90, 600, 2.0), (s.Rounds, s.Warmup, s.Frames, s.WarmupMinSeconds));
        Assert.Equal(39, s.Configs.Select(c => c.Id).Distinct().Count());
        Assert.Equal(39, s.Configs.Select(c => (c.Slot, c.Width, c.Height, c.LodMult, c.Shadows, c.Crowd, c.Path)).Distinct().Count());
        Assert.Equal(15, s.Configs.Count(c => c.Sweep == "resolution"));
        Assert.Equal(8, s.Configs.Count(c => c.Sweep == "lod"));
        Assert.Equal(9, s.Configs.Count(c => c.Sweep == "shadows"));
        Assert.Equal(7, s.Configs.Count(c => c.Sweep == "crowd"));
        // every slot at every resolution; LOD on B and C without the x1 baseline; shadows on A, C, E without "default"
        foreach (var slot in Suites.Slots)
            foreach (var (w, h) in Suites.Resolutions)
                Assert.Single(s.Configs, c => c.Sweep == "resolution" && c.Slot == slot && c.Width == w && c.Height == h);
        Assert.All(s.Configs.Where(c => c.Sweep == "lod"), c => { Assert.Contains(c.Slot, new[] { 'B', 'C' }); Assert.NotEqual(1f, c.LodMult); });
        Assert.All(s.Configs.Where(c => c.Sweep == "shadows"), c => { Assert.Contains(c.Slot, new[] { 'A', 'C', 'E' }); Assert.NotEqual(ShadowMode.Default, c.Shadows); });
        Assert.Equal(new int?[] { 500, 1000, 2000, 4000, 8000, 12000, 16000 }, s.Configs.Where(c => c.Sweep == "crowd").Select(c => c.Crowd).ToArray());
        Assert.All(s.Configs.Where(c => c.Sweep != "resolution"), c => Assert.Equal((1920, 1080), (c.Width, c.Height)));
        Assert.Equal(new[] { "crowd-16000", "lod-C-x0.25", "lod-C-x4", "res-A-1920x1080", "res-B-1920x1080", "res-C-1920x1080", "res-D-1920x1080", "res-E-1920x1080" },
            s.Configs.Where(c => c.Capture).Select(c => c.Id).OrderBy(x => x, StringComparer.Ordinal).ToArray());
    }

    [Fact]
    public void QuickAndSmokeSuitesRunEverySlotOnce()
    {
        foreach (var s in new[] { Suites.Quick(), Suites.Smoke() })
        {
            Assert.Equal(Suites.Slots, s.Configs.Select(c => c.Slot).ToArray());
            Assert.Equal(1, s.Rounds);
        }
        Assert.All(Suites.Smoke().Configs, c => Assert.Equal((1280, 720), (c.Width, c.Height)));
        Assert.All(Suites.Quick().Configs, c => Assert.Equal((1920, 1080), (c.Width, c.Height)));
    }

    [Theory]
    [InlineData("--suite standard", "suite", "standard")]
    [InlineData("--sweep lod --slots C", "sweep", "lod")]
    [InlineData("--scene C --size 2560x1440 --lod 0.5 --shadows 4096", "scene", "C")]
    [InlineData("--scene crowd:3000", "scene", "crowd:3000")]
    [InlineData("--gpu-selftest --out results/selftest-launcher --require-nvidia", "gpu-selftest", null)]
    [InlineData("--list-configs", "suite", "standard")]
    public void ValidCommandLinesParse(string line, string mode, string? target)
    {
        var o = PerfOptions.Parse(line.Split(' '), out var err);
        Assert.True(o != null, err);
        Assert.Equal(mode, o!.Mode);
        Assert.Equal(target, o.Target);
    }

    [Theory]
    [InlineData("--suite")]
    [InlineData("--suite nightly")]
    [InlineData("--sweep fog")]
    [InlineData("--suite smoke --scene A")]
    [InlineData("--suite smoke --size 1920x1080")]
    [InlineData("--scene A --size 1920*1080")]
    [InlineData("--scene A --lod -1")]
    [InlineData("--scene A --shadows 512")]
    [InlineData("--suite smoke --slots F")]
    [InlineData("--suite smoke --frames 0")]
    [InlineData("--suite smoke --what")]
    [InlineData("--suite smoke stray")]
    [InlineData("--gpu-selftest --list-configs")]
    [InlineData("")]
    public void BadCommandLinesAreRejected(string line)
    {
        var args = line.Length == 0 ? Array.Empty<string>() : line.Split(' ');
        Assert.Null(PerfOptions.Parse(args, out var err));
        Assert.False(string.IsNullOrEmpty(err));
    }

    [Fact]
    public void BindingsOverrideDefaultsAndRejectUnknownSlotsAndScenes()
    {
        var m = SlotBindings.Parse("A=avatar-turntable, c=units-wide5k", out var err)!;
        Assert.Null(err);
        Assert.Equal("avatar-turntable", m['A']);
        Assert.Equal("units-wide5k", m['C']);
        Assert.Equal("crowd:1000", m['B']);
        Assert.Null(SlotBindings.Parse("F=battlefield", out err)); Assert.Contains("unknown slot", err);
        Assert.Null(SlotBindings.Parse("A", out err));
        Assert.NotNull(SlotBindings.Resolve('A', "battlefield", Registry, out err));
        Assert.Null(SlotBindings.Resolve('A', "no-such-scene", Registry, out err)); Assert.Contains("neither", err);
        Assert.Null(SlotBindings.Resolve('B', "crowd:0", Registry, out err));
        Assert.True(SlotBindings.Resolve('B', "crowd:2500", Registry, out _)!.SupportsCrowd);
        // the default bindings all resolve against the scenes Gfx.Demo ships
        foreach (var (slot, spec) in SlotBindings.Defaults) Assert.NotNull(SlotBindings.Resolve(slot, spec, Registry, out _));
    }

    [Fact]
    public void CrowdSweepNeedsACrowdBindingOnSlotB()
    {
        var o = PerfOptions.Parse(new[] { "--suite", "standard", "--bind", "B=units-clash" }, out _)!;
        Assert.Null(PerfCli.BuildPlan(o, Registry, Array.Empty<string>(), out var err));
        Assert.Contains("crowd", err);
        var o2 = PerfOptions.Parse(new[] { "--suite", "standard", "--bind", "B=units-clash", "--slots", "A,C,D,E" }, out _)!;
        var plan = PerfCli.BuildPlan(o2, Registry, Array.Empty<string>(), out err);
        Assert.True(plan != null, err);
        Assert.DoesNotContain(plan!.Suite.Configs, c => c.Slot == 'B');
    }

    [Theory]
    [InlineData("NVIDIA Corporation", "NVIDIA GeForce RTX 4050 Laptop GPU/PCIe/SSE2", true, "nvidia-rtx4050")]
    [InlineData("NVIDIA Corporation", "NVIDIA GeForce RTX 4060 Laptop GPU/PCIe/SSE2", false, "nvidia-other")]
    [InlineData("Intel", "Intel(R) Iris(R) Xe Graphics", false, "intel-igpu")]
    [InlineData("Mesa", "llvmpipe (LLVM 20.1.2, 256 bits)", false, "software")]
    [InlineData("Microsoft Corporation", "GDI Generic", false, "software")]
    [InlineData("AMD", "AMD Radeon(TM) Graphics", false, "other")]
    [InlineData("Mesa", "RTX 4050 spoof without the vendor", false, "other")]
    public void OnlyTheRtx4050IsVictusEvidence(string vendor, string renderer, bool valid, string kind)
    {
        var v = Validity.Classify(vendor, renderer);
        Assert.Equal(valid, v.ValidVictus);
        Assert.Equal(kind, v.Kind);
        Assert.Equal(valid ? "VALID_VICTUS_RTX4050" : "INVALID_NOT_RTX4050", v.Label);
        Assert.Equal(valid ? "perf-20261109-071500-VALID_VICTUS_RTX4050" : "perf-20261109-071500-INVALID_NOT_RTX4050",
            ResultWriter.FileBaseName(new DateTime(2026, 11, 9, 7, 15, 0), v));
        if (!valid) Assert.Contains("INVALID - NOT VICTUS EVIDENCE", v.Banner(renderer));
    }

    [Fact]
    public void PercentilesAreNearestRankLikeG1FrameStats()
    {
        var a = Enumerable.Range(0, 997).Select(i => (i * 7919 % 1000) / 10.0).ToArray();
        var d = Stats.Of(a);
        foreach (var (p, got) in new[] { (50.0, d.P50), (95.0, d.P95), (99.0, d.P99) })
            Assert.Equal(Gfx.Engine.Diagnostics.FrameStats.Percentile(a, p), got);
        // tie-dense: 90 equal values and 10 larger ones
        var t = Enumerable.Repeat(5.0, 90).Concat(Enumerable.Range(1, 10).Select(i => 5.0 + i)).ToArray();
        var dt = Stats.Of(t);
        Assert.Equal(5.0, dt.P50); Assert.Equal(10.0, dt.P95); Assert.Equal(14.0, dt.P99); Assert.Equal(15.0, dt.Max);
        Assert.Equal(Dist.Empty.Count, Stats.Of(new[] { double.NaN }).Count);
        Assert.Equal(2.5, Stats.Median(new[] { 4.0, 1.0, 2.0, 3.0 }));
    }

    [Fact]
    public void HistogramPutsEveryFrameInExactlyOneBand()
    {
        var frames = new[] { 1.0, 1000.0 / 240, 5.0, 8.0, 10.0, 16.0, 1000.0 / 60, 20.0, 40.0, 75.0, 250.0, double.NaN };
        var h = Stats.Histogram(frames);
        Assert.Equal(Stats.HistogramLabels.Length, h.Length);
        Assert.Equal(11, h.Sum());
        Assert.Equal(1, h[0]);                 // 1.0 ms only; exactly 1000/240 belongs to the slower band
        Assert.Equal(2, h[1]);                 // 4.17, 5.0
        Assert.Equal(2, h[5]);                 // exactly 16.67 and 20 ms: the 30-60 fps band
        Assert.Equal(1, h[8]);                 // 250 ms
    }

    static double[] Series(int n, Func<int, double> f) => Enumerable.Range(0, n).Select(f).ToArray();

    [Fact]
    public void VsyncIsSuspectedOnlyWhenTheIntervalIsPinnedAndTheAppIsIdle()
    {
        var pinned60 = Series(300, i => 16.667 + (i % 5 - 2) * 0.05);
        Assert.True(Flags.VsyncSuspected(pinned60, 3.1, 4.2, out var why));
        Assert.Contains("60 Hz", why);
        var pinned144 = Series(300, i => 6.944 + (i % 3 - 1) * 0.02);
        Assert.True(Flags.VsyncSuspected(pinned144, 1.0, 2.0, out _));
        Assert.False(Flags.VsyncSuspected(pinned60, 3.0, 16.2, out _));      // GPU-bound at ~60 fps: not a cap
        Assert.False(Flags.VsyncSuspected(Series(300, i => 12.3 + (i % 7) * 0.01), 2, 3, out _)); // not near a refresh period
        Assert.False(Flags.VsyncSuspected(Series(300, i => 10 + (i % 13)), 2, 3, out _));          // wide spread
        Assert.False(Flags.VsyncSuspected(Series(10, _ => 16.667), 1, 1, out _));                   // too few frames
    }

    static FlagInputs Clean() => new(Series(100, i => 9 + (i % 11) * 0.37), 4, 8.5, true, 0, 0, 0,
        new bool?[] { false, false }, Enumerable.Repeat<bool?>(true, 100).ToArray(),
        new[] { new NvmlSample(0, 2000, 2000, 8000, 70, 60, 90, 0x1, true, 95, 40, 1L << 30, 6L << 30, 0) }, 0);

    [Fact]
    public void EachMeasurementConditionRaisesItsFlag()
    {
        Assert.Empty(Flags.Evaluate(Clean()).Raised);
        Assert.Equal(new[] { FlagNames.OnBattery }, Flags.Evaluate(Clean() with { PowerOnBattery = new bool?[] { false, true } }).Raised);
        var a = Clean().FrameActive.ToArray(); a[50] = false;
        Assert.Equal(new[] { FlagNames.WindowNotFocused }, Flags.Evaluate(Clean() with { FrameActive = a }).Raised);
        Assert.Equal(new[] { FlagNames.ThermalOrPowerThrottle },
            Flags.Evaluate(Clean() with { Nvml = new[] { new NvmlSample(0, 1500, 1500, 8000, 87, 80, 80, Nvml.SwPowerCap | Nvml.SwThermalSlowdown, true, 99, 50, 1, 6, 0) } }).Raised);
        Assert.Equal(new[] { FlagNames.VsyncOrFrameCapSuspected }, Flags.Evaluate(Clean() with { FrameMs = Series(100, i => 16.667 + (i % 3) * 0.01), GpuP50 = 3, CpuBusyP50 = 2 }).Raised);
        Assert.Equal(new[] { FlagNames.GpuTimerFramesDropped }, Flags.Evaluate(Clean() with { FramesWithoutGpuTime = 3, TimerFramesDropped = 3 }).Raised);
        Assert.Equal(new[] { FlagNames.Gen2GcDuringMeasurement }, Flags.Evaluate(Clean() with { Gen2Collections = 1 }).Raised);
        Assert.Equal(new[] { FlagNames.GpuTimersUnavailable }, Flags.Evaluate(Clean() with { TimersAvailable = false }).Raised);
        // GPU idle alone (0x1) is not a power/thermal limit
        Assert.DoesNotContain(FlagNames.ThermalOrPowerThrottle, Flags.Evaluate(Clean()).Raised);
        // unknowns are reported as not evaluated, never as passing
        var u = Flags.Evaluate(Clean() with { PowerOnBattery = new bool?[] { null, null }, FrameActive = new bool?[] { null }, Nvml = null });
        Assert.Empty(u.Raised);
        Assert.Equal(3, u.NotEvaluated.Count);
    }

    [Fact]
    public void ThrottleReasonBitsHaveNames()
    {
        Assert.Equal("none", Nvml.ReasonNames(0));
        Assert.Equal("SW_POWER_CAP|HW_THERMAL", Nvml.ReasonNames(Nvml.SwPowerCap | Nvml.HwThermalSlowdown));
        Assert.Equal("GPU_IDLE|0x1000", Nvml.ReasonNames(Nvml.GpuIdle | 0x1000));
        Assert.Equal(0UL, Nvml.ThermalOrPowerMask & (Nvml.GpuIdle | Nvml.ApplicationsClocksSetting | Nvml.SyncBoost | Nvml.DisplayClockSetting));
    }

    [Fact]
    public void CsvRowsHaveOneInvariantFieldPerColumn()
    {
        var cfg = Suites.Baseline('C');
        var r = new RunResult { RunIndex = 7, Round = 2, Config = cfg, SceneSpec = "battlefield", StandInGap = "gap, with comma", Status = "error: x, \"y\"" };
        r.FrameMs = Stats.Of(new[] { 10.25, 11.5, 12.75 }); r.LoadMs = 1234.5678;
        var info = FakeInfo("Mesa", "llvmpipe (LLVM 20.1.2, 256 bits)");
        var fields = ResultWriter.Columns.Select(c => c.get(r, info)).ToArray();
        Assert.Equal(ResultWriter.Columns.Length, fields.Length);
        Assert.Equal(ResultWriter.Columns.Length, ResultWriter.Columns.Select(c => c.name).Distinct().Count());
        Assert.Equal("INVALID_NOT_RTX4050", fields[0]);
        Assert.Equal("11.5", fields[Array.FindIndex(ResultWriter.Columns, c => c.name == "frame_ms_p50")]);
        Assert.Equal("1234.6", fields[Array.FindIndex(ResultWriter.Columns, c => c.name == "load_ms")]);
        Assert.Equal("", fields[Array.FindIndex(ResultWriter.Columns, c => c.name == "gpu_ms_p50")]); // NaN is an empty cell, never "NaN"
        Assert.DoesNotContain(fields, f => f.Contains("NaN", StringComparison.Ordinal));
    }

    internal static SessionInfo FakeInfo(string vendor, string renderer) => new()
    {
        Vendor = vendor, Renderer = renderer, GlVersion = "4.5", Glsl = "4.50", ExtensionCount = 0, TimerQueries = true, TimestampBits = 64,
        NvxMemInfo = false, AtiMemInfo = false, GlFailure = null, Validity = Validity.Classify(vendor, renderer), MonoGame = "3.8.5", Profile = "HiDef",
        CpuName = "cpu", LogicalCpus = 4, TotalMemoryBytes = 1, Os = "os", Runtime = "rt", ProcessPath = "p", HostKind = "plain exe (.NET apphost)",
        ExportsNvOptimus = null, ExportsAmdPowerXpress = null, WindowsPowerMode = "n/a", Power = new PowerState(null, "n/a"), NvmlStatus = "n/a",
        NvmlDriver = null, NvmlDevice = null, BuildConfiguration = "Release", HarnessVersion = PerfCli.HarnessVersion, SourceCommit = "abc",
        BackBufferWidth = 960, BackBufferHeight = 540, VsyncRequested = false, StartedLocal = "2026-11-09 07:15:00 +00:00", StartedUtc = "2026-11-09T07:15:00Z",
    };

    /// <summary>
    /// Regression (found by the Wine run of the Windows package): the battle-trace fixture must travel with the
    /// build output, or a published package cannot load slot C. The copy next to the app must equal the repo file.
    /// </summary>
    [Fact]
    public void BuildOutputCarriesTheBattleTraceFixture()
    {
        var local = Path.Combine(AppContext.BaseDirectory, "fixtures", Gfx.Units.BattleTrace.DefaultFixture);
        Assert.True(File.Exists(local), local);
        Assert.Equal(local, Gfx.Units.BattleTrace.FindFixture(Gfx.Units.BattleTrace.DefaultFixture));
        var repo = Gfx.Units.BattleTrace.FindFixture(Gfx.Units.BattleTrace.DefaultFixture, Path.GetDirectoryName(Path.GetDirectoryName(Path.GetDirectoryName(Path.GetDirectoryName(AppContext.BaseDirectory.TrimEnd(Path.DirectorySeparatorChar))))));
        Assert.Equal(Convert.ToHexString(System.Security.Cryptography.SHA256.HashData(File.ReadAllBytes(repo))),
            Convert.ToHexString(System.Security.Cryptography.SHA256.HashData(File.ReadAllBytes(local))));
    }

    [Fact]
    public void OrbitPathsAreFrameDeterministicAndKeepTheirDistance()
    {
        var shot = new CameraPose(new Vector3(30, 20, 40), new Vector3(5, 1, -2), 0.9f, 0.5f, 3000f);
        var p = new OrbitPath(shot, 2f, 11f, 0.5f, 10f, "t");
        float d0 = Vector3.Distance(shot.Position, shot.Target);
        for (int i = 0; i < 600; i += 37)
        {
            float u = i / 600f;
            var a = p.At(u); var b = p.At(u);
            Assert.Equal(a, b);
            Assert.Equal(d0, Vector3.Distance(a.Camera.Position, a.Camera.Target), 3);
            Assert.Equal(shot.Target, a.Camera.Target);
            Assert.Equal(2f + u * 10f, a.SceneTime, 4);
            Assert.Equal(11f, a.Hour);
        }
        Assert.Equal(shot.Position.X, p.At(0).Camera.Position.X, 3);
    }

    /// <summary>
    /// Integration pin (perf + render1): the G2 frame calls the shadow casters once per cascade, so the GPU casters span
    /// runs from the first begin to the last end and the passes still partition the frame; the G1 frame's single call
    /// splits as before.
    /// </summary>
    [Fact]
    public void GpuPassSplitSpansEveryShadowCascade()
    {
        string cb = ContentTap.MarkCastersBegin, ce = ContentTap.MarkCastersEnd, ob = ContentTap.MarkOpaqueBegin, oe = ContentTap.MarkOpaqueEnd;
        string upd = PerfSession.MarkUpdateEnd, re = PerfSession.MarkRenderEnd, be = PerfSession.MarkBlitEnd;
        const long ms = 1_000_000;
        // four cascades at 2-3, 3-4, 4-5 and 5-6 ms; opaque 7-9 ms; render end 10 ms; blit end 11 ms
        var g2 = new GpuFrame(1, new[] { "frame.begin", upd, cb, ce, cb, ce, cb, ce, cb, ce, ob, oe, re, be, "frame.end" },
            new[] { 0, 1 * ms, 2 * ms, 3 * ms, 3 * ms, 4 * ms, 4 * ms, 5 * ms, 5 * ms, 6 * ms, 7 * ms, 9 * ms, 10 * ms, 11 * ms, 11 * ms }, true);
        var f = FrameRecord.New(1);
        f.ApplyGpu(g2, null);
        Assert.Equal(1.0, f.GpuShadowClearMs, 9);
        Assert.Equal(4.0, f.GpuCastersMs, 9);
        Assert.Equal(1.0, f.GpuSkyMs, 9);
        Assert.Equal((2.0, 1.0, 1.0, 11.0), (f.GpuOpaqueMs, f.GpuPostMs, f.GpuBlitMs, f.GpuMs));
        Assert.Equal(f.GpuMs, f.GpuUploadMs + f.GpuShadowClearMs + f.GpuCastersMs + f.GpuSkyMs + f.GpuOpaqueMs + f.GpuPostMs + f.GpuBlitMs, 9);
        var g1 = new GpuFrame(2, new[] { "frame.begin", upd, cb, ce, ob, oe, re, be, "frame.end" },
            new[] { 0, 1 * ms, 2 * ms, 3 * ms, 7 * ms, 9 * ms, 10 * ms, 11 * ms, 11 * ms }, true);
        var h = FrameRecord.New(2);
        h.ApplyGpu(g1, null);
        Assert.Equal((1.0, 1.0, 4.0), (h.GpuShadowClearMs, h.GpuCastersMs, h.GpuSkyMs));
    }

    /// <summary>
    /// Integration pin (perf + render1): the shadow sweep sets the resolution of the shadow maps the frame really uses,
    /// G1's single map or each cascade of the G2 frame (whose default, 2048, equals G1's single map).
    /// </summary>
    [Fact]
    public void ShadowSweepSetsTheShadowMapsOfTheFrameInUse()
    {
        foreach (var (mode, size) in new[] { (ShadowMode.S1024, 1024), (ShadowMode.Default, 2048), (ShadowMode.S4096, 4096) })
        {
            var g2 = new RenderSettings();
            Assert.Equal(ShadowTechnique.Cascaded, g2.Features.ShadowTechnique);
            Assert.Equal(size, PerfSession.ApplyShadowMode(g2, mode));
            Assert.Equal(size, g2.Features.CascadeSize);
            Assert.True(g2.Shadows);
            var g1 = new RenderSettings { Features = RenderFeatures.G1() };
            Assert.Equal(size, PerfSession.ApplyShadowMode(g1, mode));
            Assert.Equal(size, g1.ShadowMapSize);
            Assert.True(g1.Shadows);
        }
        var off = new RenderSettings();
        Assert.Equal(0, PerfSession.ApplyShadowMode(off, ShadowMode.Off));
        Assert.False(off.Shadows);
    }
}

/// <summary>G2 perf stage on the shared headless device (llvmpipe here): timers, census, pass tap, sessions.</summary>
public class PerfDeviceTests
{
    static IReadOnlyList<SceneRegistry.Entry> Registry => SceneRegistry.Discover(typeof(Gfx.Demo.Program).Assembly);

    static string TempDir(string name)
    {
        var d = Path.Combine(Path.GetTempPath(), "g2perf-tests", name + "-" + Guid.NewGuid().ToString("N")[..8]);
        Directory.CreateDirectory(d);
        return d;
    }

    [GfxFact]
    public void GpuTimerReturnsEveryFramesMarkersInOrder()
    {
        TestGfx.Host.Invoke(gd =>
        {
            var gl = GlApi.TryCreate(out var why);
            Assert.True(gl != null, why);
            Assert.True(gl!.HasTimerQueries, "this device has no timer queries");
            using var timer = new GpuTimer(gl);
            using var rt = new RenderTarget2D(gd, 256, 128, false, SurfaceFormat.Color, DepthFormat.Depth24);
            for (int f = 1; f <= 12; f++)
            {
                timer.BeginFrame(f);
                gd.SetRenderTarget(rt);
                gd.Clear(Color.CornflowerBlue);
                timer.Mark("a");
                gd.Clear(Color.Black);
                timer.Mark("b");
                gd.SetRenderTarget(null);
                timer.EndFrame();
            }
            timer.FinishAll();
            var frames = new List<GpuFrame>();
            while (timer.TryDequeue(out var g)) frames.Add(g);
            Assert.Equal(12 - (int)timer.Dropped, frames.Count);
            Assert.True(frames.Count >= 8, $"only {frames.Count} of 12 frames came back");
            Assert.Equal(frames.Select(f => f.FrameId).OrderBy(x => x), frames.Select(f => f.FrameId).OrderBy(x => x).Distinct());
            foreach (var f in frames)
            {
                Assert.Equal(new[] { "frame.begin", "a", "b", "frame.end" }, f.Names);
                Assert.True(f.Monotonic);
                Assert.Equal(0, f.TimesNs[0]);
                Assert.True(f.At("frame.end") >= f.At("b"));
            }
        });
    }

    [GfxFact]
    public void CensusCountsANewRenderTargetAndBufferExactly()
    {
        TestGfx.Host.Invoke(gd =>
        {
            var before = MemoryCensus.Take(gd);
            Assert.True(before.Available, before.Failure);
            using var rt = new RenderTarget2D(gd, 64, 32, false, SurfaceFormat.Color, DepthFormat.Depth24);
            using var vb = new VertexBuffer(gd, typeof(VertexPositionColor), 100, BufferUsage.WriteOnly);
            var after = MemoryCensus.Take(gd);
            Assert.Equal(before.RenderTargets + 1, after.RenderTargets);
            Assert.Equal(before.RenderTargetBytes + 64 * 32 * 4 + 64 * 32 * 4, after.RenderTargetBytes);
            Assert.Equal(before.VertexBuffers + 1, after.VertexBuffers);
            Assert.Equal(before.VertexBytes + 100 * VertexPositionColor.VertexDeclaration.VertexStride, after.VertexBytes);
        });
    }

    /// <summary>
    /// Pins the MonoGame 3.8.5 bug (a disposed non-MSAA render target keeps its cached framebuffer) and the fix:
    /// Reclaim deletes it and the depth renderbuffer, and leaves a live target's framebuffer working.
    /// </summary>
    [GfxFact]
    public void ReclaimerDeletesWhatMonoGameLeaksForDisposedRenderTargets()
    {
        TestGfx.Host.Invoke(gd =>
        {
            var gl = GlApi.TryCreate(out var why);
            Assert.True(gl != null, why);
            RenderTargetReclaimer.Reclaim(gd, gl!); // start from a clean cache (other tests share this device)
            using var live = new RenderTarget2D(gd, 32, 32, false, SurfaceFormat.Color, DepthFormat.Depth24);
            var dead = new RenderTarget2D(gd, 64, 64, false, SurfaceFormat.Color, DepthFormat.Depth24Stencil8);
            foreach (var rt in new[] { live, dead }) { gd.SetRenderTarget(rt); gd.Clear(Color.Red); }
            gd.SetRenderTarget(null);
            dead.Dispose();
            Assert.Equal(1, RenderTargetReclaimer.StaleFramebuffers(gd)); // MonoGame kept the disposed target's framebuffer
            var res = RenderTargetReclaimer.Reclaim(gd, gl!);
            Assert.True(res.Available, res.Failure);
            Assert.Equal((1, 1, 1), (res.Framebuffers, res.Renderbuffers, res.Targets)); // packed depth-stencil = one renderbuffer
            Assert.Equal(0, RenderTargetReclaimer.StaleFramebuffers(gd));
            Assert.Equal(0u, gl!.GetError());
            gd.SetRenderTarget(live); gd.Clear(Color.Lime); gd.SetRenderTarget(null);
            var px = new Color[32 * 32]; live.GetData(px);
            Assert.All(px, c => Assert.Equal(Color.Lime, c)); // the live target still renders
        });
    }

    /// <summary>A multi-run session leaves no framebuffer of a disposed render target behind (each run disposes 3 targets).</summary>
    [GfxFact]
    public void RepeatedRunsLeaveNoStaleFramebuffers()
    {
        var dir = TempDir("leak");
        var cfg = new RunConfig("t-A", "single", 'A', 96, 54, 1f, ShadowMode.Default, null, "A", false);
        var plan = Plan(dir, new SuiteSpec("test", 3, 0, 2, new[] { cfg }), capture: false);
        PerfSession? s = null; int stale = -1;
        using var log = new PerfLog(null);
        TestGfx.Host.Invoke(gd => { s = HeadlessRunner.Run(gd, TestGfx.Host.Effects, plan, log); s.Dispose(); stale = RenderTargetReclaimer.StaleFramebuffers(gd); });
        Assert.Equal(3, s!.Results.Count);
        Assert.Null(s.ReclaimFailure);
        Assert.True(s.ReclaimedFramebuffers >= 6, $"reclaimed {s.ReclaimedFramebuffers} framebuffers over 3 runs");
        Assert.Equal(0, stale);
    }

    /// <summary>
    /// The harness must not change what is drawn: a frame through the tap equals the untapped frame, pixel for pixel,
    /// in the G1 frame (one shadow-caster call) and in the G2 frame of render1 (one call per cascade). Auto exposure is
    /// re-primed before every frame, so the frames are metered alike.
    /// </summary>
    [GfxFact]
    public void TappedRenderIsPixelIdenticalToTheG1Renderer()
    {
        TestGfx.Host.Invoke(gd =>
        {
            using var scene = new Gfx.Units.Demo.UnitsCloseupScene();
            scene.Load(gd, TestGfx.Host.Effects);
            using var rt = new RenderTarget2D(gd, 320, 180, false, SurfaceFormat.Color, DepthFormat.None);
            var shot = scene.Shots[0];
            var gl = GlApi.TryCreate(out _);
            using var timer = gl is { HasTimerQueries: true } ? new GpuTimer(gl) : null;
            long frameId = 0;
            foreach (bool g1Frame in new[] { true, false })
            {
                using var r = new ForwardRenderer(gd, TestGfx.Host.Effects);
                scene.Configure(r.Settings);
                if (g1Frame) r.Settings.Features = RenderFeatures.G1();
                Assert.Equal(g1Frame, r.Settings.Features.IsG1Frame);
                Assert.True(r.Settings.Shadows);
                Color[] Frame(IRenderContent c)
                {
                    scene.Update(shot.SceneTime);
                    r.ResetAdaptation = true;
                    r.Render(c, shot.Camera, SunModel.At(shot.HourOfDay), rt, shot.SceneTime);
                    var px = new Color[rt.Width * rt.Height]; rt.GetData(px); return px;
                }
                var direct1 = Frame(scene);
                var direct2 = Frame(scene);
                Assert.Equal(direct1, direct2); // the device itself is deterministic for this frame
                var inner = new CountingContent(scene);
                var tap = new ContentTap(inner, timer);
                timer?.BeginFrame(++frameId);
                var tapped = Frame(tap);
                timer?.EndFrame();
                Assert.Equal(direct1, tapped);
                int shadowMaps = g1Frame ? 1 : r.Cascades!.Count;
                Assert.True(g1Frame || shadowMaps > 1, $"G2 frame with {shadowMaps} cascade(s)");
                Assert.Equal((shadowMaps, 1), (tap.CastersCalls, tap.OpaqueCalls));
                Assert.Equal((shadowMaps, 1), (inner.Casters, inner.Opaque)); // every call reached the scene
                Assert.True(Gfx.Engine.Diagnostics.Capture.DistinctColors(tapped) > 100);
            }
        });
    }

    sealed class CountingContent(IRenderContent inner) : IRenderContent
    {
        public int Casters, Opaque;
        public void DrawShadowCasters(RenderContext rc) { Casters++; inner.DrawShadowCasters(rc); }
        public void DrawOpaque(RenderContext rc) { Opaque++; inner.DrawOpaque(rc); }
    }

    /// <summary>
    /// Integration pin (perf + render1): the review capture is metered on its own view. The run's renderer has adapted
    /// its auto exposure to other frames (here a camera looking up into the sky, at later scene times); the capture
    /// still equals the same frame from a fresh renderer, pixel for pixel.
    /// </summary>
    [GfxFact]
    public void CaptureFrameMetersItsOwnExposure()
    {
        TestGfx.Host.Invoke(gd =>
        {
            using var scene = new Gfx.Units.Demo.UnitsCloseupScene();
            scene.Load(gd, TestGfx.Host.Effects);
            using var rt = new RenderTarget2D(gd, 160, 90, false, SurfaceFormat.Color, DepthFormat.None);
            var shot = scene.Shots[0];
            var pf = new PathFrame(shot.Camera, shot.SceneTime, shot.HourOfDay);
            Color[] Pixels() { var px = new Color[rt.Width * rt.Height]; rt.GetData(px); return px; }

            using var fresh = new ForwardRenderer(gd, TestGfx.Host.Effects);
            scene.Configure(fresh.Settings);
            Assert.True(fresh.Settings.Features.AutoExposure && !fresh.Settings.Features.IsG1Frame);
            PerfSession.RenderCaptureFrame(fresh, scene, pf, rt);
            var expected = Pixels();

            using var used = new ForwardRenderer(gd, TestGfx.Host.Effects);
            scene.Configure(used.Settings);
            var up = shot.Camera with { Target = shot.Camera.Position + new Vector3(0, 30, 10) };
            for (int i = 0; i < 4; i++)
            {
                float t = shot.SceneTime + 1 + i / 30f;
                scene.Update(t);
                used.Render(scene, up, SunModel.At(shot.HourOfDay), rt, t);
            }
            PerfSession.RenderCaptureFrame(used, scene, pf, rt);
            Assert.Equal(expected, Pixels());
        });
    }

    static PerfPlan Plan(string outDir, SuiteSpec suite, IReadOnlyDictionary<char, SceneBinding>? bindings = null, bool capture = true)
    {
        var b = bindings ?? SlotBindings.Defaults.ToDictionary(kv => kv.Key, kv => SlotBindings.Resolve(kv.Key, kv.Value, Registry, out _)!);
        return new PerfPlan { Suite = suite, Bindings = b, OutDir = outDir, Mode = "test", Capture = capture };
    }

    /// <summary>RFC 4180 field split (quotes, doubled quotes).</summary>
    internal static List<string> CsvFields(string line)
    {
        var f = new List<string>(); var cur = new System.Text.StringBuilder(); bool q = false;
        for (int i = 0; i < line.Length; i++)
        {
            char c = line[i];
            if (q) { if (c == '"' && i + 1 < line.Length && line[i + 1] == '"') { cur.Append('"'); i++; } else if (c == '"') q = false; else cur.Append(c); }
            else if (c == '"') q = true;
            else if (c == ',') { f.Add(cur.ToString()); cur.Clear(); }
            else cur.Append(c);
        }
        f.Add(cur.ToString());
        return f;
    }

    [GfxFact]
    public void HeadlessSessionMeasuresARunAndWritesConsistentFiles()
    {
        var dir = TempDir("session");
        var cfg = new RunConfig("t-A", "single", 'A', 160, 90, 1f, ShadowMode.Default, null, "A", true);
        var plan = Plan(dir, new SuiteSpec("test", 1, 2, 6, new[] { cfg }));
        PerfSession? s = null;
        using var log = new PerfLog(Path.Combine(dir, "bench-log.txt"));
        TestGfx.Host.Invoke(gd => { s = HeadlessRunner.Run(gd, TestGfx.Host.Effects, plan, log); s.Dispose(); });
        Assert.NotNull(s);
        Assert.True(s!.Done);
        Assert.Equal("finished", s.Status);
        Assert.Equal(0, s.ExitCode);
        var r = Assert.Single(s.Results);
        Assert.Equal("ok", r.Status);
        Assert.Equal(6, r.Frames.Length);
        Assert.Equal(5, r.FrameMs.Count); // the first measured frame's interval starts at the last warm-up frame (headless: no present wait)
        Assert.True(r.DrawsMean > 3 && r.TrisMean > 1000, $"draws {r.DrawsMean} tris {r.TrisMean}");
        Assert.True(r.Lod0Mean > 0, "the close-up shows full-detail soldiers");
        Assert.Equal(r.Frames.Length - r.FramesWithoutGpu, r.GpuMs.Count);
        Assert.True(r.LoadMs > 0);
        Assert.Contains(FlagNames.SoftwareRenderer, r.Flags.Raised);
        // files
        var files = Directory.GetFiles(dir, "perf-*").Select(Path.GetFileName).OrderBy(x => x, StringComparer.Ordinal).ToArray();
        Assert.Equal(3, files.Length);
        Assert.All(files, f => Assert.Contains("-INVALID_NOT_RTX4050", f));
        var csv = File.ReadAllLines(Directory.GetFiles(dir, "perf-*.csv").Single());
        Assert.Equal(2, csv.Length);
        Assert.Equal(ResultWriter.Columns.Length, CsvFields(csv[0]).Count);
        Assert.Equal(ResultWriter.Columns.Length, CsvFields(csv[1]).Count);
        Assert.Equal(s.Info.Renderer, CsvFields(csv[1])[^1]); // e.g. "llvmpipe (LLVM 20.1.2, 256 bits)": a quoted field with a comma
        using var json = JsonDocument.Parse(File.ReadAllText(Directory.GetFiles(dir, "perf-*.json").Single()));
        var root = json.RootElement;
        Assert.Equal("INVALID_NOT_RTX4050", root.GetProperty("label").GetString());
        Assert.False(root.GetProperty("valid_victus_evidence").GetBoolean());
        var run = root.GetProperty("runs")[0];
        Assert.Equal(6, run.GetProperty("measured_frames").GetInt32());
        Assert.Equal(6, run.GetProperty("per_frame").GetProperty("frame_ms").GetArrayLength());
        Assert.Equal(r.RenderFeatures, run.GetProperty("render_features").GetString());
        Assert.StartsWith("sky=Analytic ", r.RenderFeatures); // the merged renderer's default: the G2 frame
        Assert.Contains("INVALID - NOT VICTUS EVIDENCE", File.ReadAllText(Directory.GetFiles(dir, "perf-*-summary.txt").Single()));
        Assert.Single(Directory.GetFiles(Path.Combine(dir, "captures"), "*.jpg"));
    }

    /// <summary>
    /// The warm-up lasts at least its minimum time (here 3 s at 160x90: many more frames than the 2-frame minimum); the
    /// measured frames do not change. Since the render1 merge the first frame of each run's new renderer builds the
    /// sky table and the IBL on the CPU (about 1.25 s in a Debug build on llvmpipe here), so the minimum time is 3 s,
    /// not 1 s: the frames after the first must still fill most of it.
    /// </summary>
    [GfxFact]
    public void WarmupLastsAtLeastItsMinimumTime()
    {
        var dir = TempDir("warmup");
        var cfg = new RunConfig("t-A", "single", 'A', 160, 90, 1f, ShadowMode.Off, null, "A", false);
        var plan = Plan(dir, new SuiteSpec("test", 1, 2, 4, new[] { cfg }, WarmupMinSeconds: 3.0), capture: false);
        PerfSession? s = null;
        using var log = new PerfLog(null);
        var sw = Stopwatch.StartNew();
        TestGfx.Host.Invoke(gd => { s = HeadlessRunner.Run(gd, TestGfx.Host.Effects, plan, log); s.Dispose(); });
        var r = Assert.Single(s!.Results);
        Assert.Equal("ok", r.Status);
        Assert.Equal(4, r.Frames.Length);
        Assert.True(r.WarmupFrames >= 5, $"warm-up frames {r.WarmupFrames}");
        Assert.True(sw.Elapsed.TotalSeconds >= 3.0);
    }

    /// <summary>Semantic pin of the LOD sweep: larger multipliers move soldiers to finer LODs, smaller ones to the proxy.</summary>
    [GfxFact]
    public void LodMultiplierShiftsTheCrowdLodMix()
    {
        var dir = TempDir("lod");
        var configs = new[] { 0.25f, 1f, 4f }.Select(m => new RunConfig("lod-" + m.ToString(CultureInfo.InvariantCulture), "lod", 'B', 160, 90, m, ShadowMode.Off, null, "B", false)).ToArray();
        var b = SlotBindings.Defaults.ToDictionary(kv => kv.Key, kv => SlotBindings.Resolve(kv.Key, kv.Key == 'B' ? "crowd:500" : kv.Value, Registry, out _)!);
        var plan = Plan(dir, new SuiteSpec("test", 1, 0, 2, configs), b, capture: false);
        PerfSession? s = null;
        using var log = new PerfLog(null);
        TestGfx.Host.Invoke(gd => { s = HeadlessRunner.Run(gd, TestGfx.Host.Effects, plan, log); s.Dispose(); });
        var r = s!.Results;
        Assert.Equal(3, r.Count);
        Assert.All(r, x => Assert.Equal("ok", x.Status));
        Assert.True(r[0].Lod0Mean <= r[1].Lod0Mean && r[1].Lod0Mean < r[2].Lod0Mean, $"lod0 {r[0].Lod0Mean} {r[1].Lod0Mean} {r[2].Lod0Mean}");
        Assert.True(r[0].Lod2Mean > r[1].Lod2Mean && r[1].Lod2Mean >= r[2].Lod2Mean, $"lod2 {r[0].Lod2Mean} {r[1].Lod2Mean} {r[2].Lod2Mean}");
        Assert.Empty(r[1].Knobs);
        Assert.Contains(r[2].Knobs, k => k.Contains("LOD0 40->160", StringComparison.Ordinal));
        Assert.All(r, x => Assert.Equal(500, (int)(x.Lod0Mean + x.Lod1Mean + x.Lod2Mean + x.CulledMean)));
    }

    /// <summary>Rendering never influences the simulation inputs: a harness run with LOD x4 leaves grid and trace hashes unchanged.</summary>
    [GfxFact]
    public void HarnessRunLeavesTheAuthoritativeGridAndTraceUnchanged()
    {
        var dir = TempDir("invariance");
        Gfx.Demo.Scenes.BattlefieldScene? captured = null;
        var b = SlotBindings.Defaults.ToDictionary(kv => kv.Key, kv => SlotBindings.Resolve(kv.Key, kv.Value, Registry, out _)!);
        b['C'] = new SceneBinding('C', "battlefield", "test capture", false, _ => captured = new Gfx.Demo.Scenes.BattlefieldScene());
        var cfg = new RunConfig("inv-C", "lod", 'C', 160, 90, 4f, ShadowMode.Default, null, "C", false);
        var plan = Plan(dir, new SuiteSpec("test", 1, 1, 3, new[] { cfg }), b, capture: false);
        string grid0 = Gfx.Proc.Authoritative.BattleTerrainGenerator.Generate(Gfx.Proc.Scenes.TerrainTemperateScene.FixtureInputs).HashHex();
        string trace0 = Gfx.Units.BattleTrace.Load(Gfx.Units.BattleTrace.FindFixture(Gfx.Units.BattleTrace.DefaultFixture)).Hash();
        PerfSession? s = null;
        using var log = new PerfLog(null);
        TestGfx.Host.Invoke(gd => { s = HeadlessRunner.Run(gd, TestGfx.Host.Effects, plan, log); s.Dispose(); });
        Assert.Equal("ok", Assert.Single(s!.Results).Status);
        Assert.NotNull(captured);
        Assert.Equal(grid0, captured!.Grid.HashHex());
        Assert.Equal(trace0, captured.Trace.Hash());
        Assert.Contains(s.Results[0].Knobs, k => k.StartsWith("terrain chunk LOD 300/700/6000->1200/2800/24000", StringComparison.Ordinal));
    }
}

/// <summary>G2 perf stage end to end in a child process (own window/device via the offscreen SDL driver): exit codes and files.</summary>
public class PerfProcessTests
{
    static (int code, string output) RunDemo(params string[] args)
    {
        string dll = typeof(Gfx.Demo.Program).Assembly.Location;
        var psi = new ProcessStartInfo(Environment.GetEnvironmentVariable("DOTNET_HOST_PATH") ?? "dotnet")
        {
            RedirectStandardOutput = true, RedirectStandardError = true, UseShellExecute = false,
            WorkingDirectory = Path.GetDirectoryName(dll)!,
        };
        psi.ArgumentList.Add(dll);
        foreach (var a in args) psi.ArgumentList.Add(a);
        psi.Environment["SDL_VIDEODRIVER"] = "offscreen";
        psi.Environment["LIBGL_ALWAYS_SOFTWARE"] = "1";
        using var p = Process.Start(psi)!;
        var stdout = p.StandardOutput.ReadToEndAsync(); var stderr = p.StandardError.ReadToEndAsync();
        if (!p.WaitForExit(300_000)) { p.Kill(true); throw new TimeoutException("Gfx.Demo did not exit within 300 s: " + string.Join(' ', args)); }
        return (p.ExitCode, stdout.Result + stderr.Result);
    }

    static string TempDir(string name)
    {
        var d = Path.Combine(Path.GetTempPath(), "g2perf-tests", name + "-" + Guid.NewGuid().ToString("N")[..8]);
        Directory.CreateDirectory(d);
        return d;
    }

    [Fact]
    public void BadOptionsExitWithCode2()
    {
        Assert.Equal(2, RunDemo("--suite", "nightly").code);
        Assert.Equal(2, RunDemo("--scene", "A", "--lod", "banana").code);
        var (code, outp) = RunDemo("--suite", "standard", "--list-configs");
        Assert.Equal(0, code);
        Assert.Contains("standard: 39 configurations x 3 round(s)", outp);
    }

    [GfxFact]
    public void SmokeSlotAFinishesWithInvalidLabelledFiles()
    {
        var dir = TempDir("smoke");
        var (code, outp) = RunDemo("--suite", "smoke", "--slots", "A", "--warmup", "2", "--frames", "5", "--out", dir, "--log", Path.Combine(dir, "bench-log.txt"));
        Assert.True(code == 0, outp);
        Assert.Contains("INVALID - NOT VICTUS EVIDENCE", outp);
        var names = Directory.GetFiles(dir).Select(Path.GetFileName).ToArray();
        Assert.Single(names, n => n!.StartsWith("perf-", StringComparison.Ordinal) && n.EndsWith("-INVALID_NOT_RTX4050.json", StringComparison.Ordinal));
        Assert.Single(names, n => n!.EndsWith("-INVALID_NOT_RTX4050.csv", StringComparison.Ordinal));
        Assert.Single(names, n => n!.EndsWith("-INVALID_NOT_RTX4050-summary.txt", StringComparison.Ordinal));
        Assert.Contains("bench-log.txt", names);
        Assert.Single(Directory.GetFiles(Path.Combine(dir, "captures"), "*.jpg"));
        using var json = JsonDocument.Parse(File.ReadAllText(Directory.GetFiles(dir, "perf-*.json").Single()));
        Assert.Equal("finished", json.RootElement.GetProperty("status").GetString());
        Assert.DoesNotContain(json.RootElement.GetProperty("runs")[0].GetProperty("flags").EnumerateArray(), f => f.GetString() == "VALID_VICTUS_RTX4050");
        Assert.Equal("INVALID_NOT_RTX4050", json.RootElement.GetProperty("label").GetString());
    }

    [GfxFact]
    public void RequireNvidiaIsRefusedWithCode3OnThisGpu()
    {
        var dir = TempDir("refuse");
        var (code, outp) = RunDemo("--suite", "smoke", "--require-nvidia", "--out", dir);
        Assert.True(code == 3, outp);
        Assert.Contains("REFUSED", outp);
    }

    /// <summary>A real SDL_QUIT (what the close button produces) during run 2: exit 4, run 1 kept, run 2 marked aborted.</summary>
    [GfxFact]
    public void ClosingTheWindowEarlyExitsWithCode4AndKeepsResults()
    {
        var dir = TempDir("close");
        var (code, outp) = RunDemo("--suite", "smoke", "--slots", "A,B", "--warmup", "1", "--frames", "4", "--simulate-close-after", "8", "--out", dir);
        Assert.True(code == 4, outp);
        using var json = JsonDocument.Parse(File.ReadAllText(Directory.GetFiles(dir, "perf-*.json").Single()));
        Assert.Equal("window closed early", json.RootElement.GetProperty("status").GetString());
        var runs = json.RootElement.GetProperty("runs").EnumerateArray().ToArray();
        Assert.Equal("ok", runs[0].GetProperty("status").GetString());
        Assert.StartsWith("aborted", runs[^1].GetProperty("status").GetString());
        Assert.Equal(2, File.ReadAllLines(Directory.GetFiles(dir, "perf-*.csv").Single()).Length); // header + the completed run
    }

    [GfxFact]
    public void GpuSelfTestWritesItsReport()
    {
        var dir = TempDir("selftest");
        var prefix = Path.Combine(dir, "selftest-plain-exe");
        var (code, outp) = RunDemo("--gpu-selftest", "--out", prefix);
        Assert.True(code == 0, outp);
        var txt = File.ReadAllText(prefix + ".txt");
        Assert.Contains("VICTUS_CHECK=INVALID_NOT_RTX4050", txt);
        Assert.Contains("GL_RENDERER", txt);
        Assert.Contains("SELFTEST_FAILURES=0", txt);
        Assert.True(File.Exists(prefix + ".jpg"));
        using var json = JsonDocument.Parse(File.ReadAllText(prefix + ".json"));
        Assert.Equal("INVALID_NOT_RTX4050", json.RootElement.GetProperty("victus_check").GetString());
        Assert.Equal(0, json.RootElement.GetProperty("effects_failed").GetInt32());
    }
}
