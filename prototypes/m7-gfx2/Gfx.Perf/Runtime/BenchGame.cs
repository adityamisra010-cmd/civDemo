using System.Diagnostics;
using System.Reflection;
using System.Runtime.InteropServices;
using Gfx.Engine.Effects;
using Gfx.Perf.Measurement;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;

namespace Gfx.Perf.Runtime;

/// <summary>
/// The benchmark's own window and GL device (MonoGame <see cref="Game"/> loop, so window events, focus and the
/// close button work). Every frame: the driver renders offscreen at the measured resolution, the game draws a
/// letterboxed preview into the window's back buffer and presents. Vsync is off unless asked for, the fixed time
/// step is off and the game never sleeps when the window loses focus (so focus changes cannot pause the run; they
/// are flagged instead).
/// </summary>
public sealed class BenchGame : Game
{
    readonly GraphicsDeviceManager _gdm;
    readonly Assembly _root;
    readonly Func<BenchGame, GraphicsDevice, EffectLibrary, IReadOnlyList<(string name, Exception? error)>, double, double, IBenchDriver> _start;
    readonly long _created = Stopwatch.GetTimestamp();
    SpriteBatch? _sb; EffectLibrary? _fx; string _title = "";
    bool _ended;

    /// <summary>Called once, on the GL thread, when the game loop ends (normal end, closed window or failure) and before the driver is disposed.</summary>
    public Action<BenchGame>? OnEnd { get; set; }

    public IBenchDriver? Driver { get; private set; }
    public Exception? Failure { get; private set; }
    public bool VsyncRequested { get; }

    public BenchGame(int windowW, int windowH, bool vsync, Assembly root,
        Func<BenchGame, GraphicsDevice, EffectLibrary, IReadOnlyList<(string name, Exception? error)>, double, double, IBenchDriver> start)
    {
        _root = root; _start = start; VsyncRequested = vsync;
        _gdm = new GraphicsDeviceManager(this)
        {
            GraphicsProfile = GraphicsProfile.HiDef, PreferredBackBufferWidth = windowW, PreferredBackBufferHeight = windowH,
            SynchronizeWithVerticalRetrace = vsync, PreferredDepthStencilFormat = DepthFormat.None,
        };
        IsFixedTimeStep = false;
        InactiveSleepTime = TimeSpan.Zero;
        IsMouseVisible = true;
        Window.Title = "CivSim benchmark - starting";
    }

    static double Ms(long ticks) => ticks * 1000.0 / Stopwatch.Frequency;

    protected override void LoadContent()
    {
        try
        {
            double deviceMs = Ms(Stopwatch.GetTimestamp() - _created);
            _fx = new EffectLibrary(GraphicsDevice, _root);
            long e0 = Stopwatch.GetTimestamp();
            var warm = _fx.WarmupAll();
            double effectsMs = Ms(Stopwatch.GetTimestamp() - e0);
            _sb = new SpriteBatch(GraphicsDevice);
            Driver = _start(this, GraphicsDevice, _fx, warm, effectsMs, deviceMs);
        }
        catch (Exception e) { Failure = e; Exit(); }
    }

    protected override void Draw(GameTime gameTime)
    {
        if (Driver == null || Failure != null) { base.Draw(gameTime); return; }
        try
        {
            var img = Driver.RenderFrame(IsActive);
            long b0 = Stopwatch.GetTimestamp();
            GraphicsDevice.SetRenderTarget(null);
            GraphicsDevice.Clear(Color.Black);
            if (img != null && _sb != null)
            {
                var vp = GraphicsDevice.Viewport;
                float s = MathF.Min(vp.Width / (float)img.Width, vp.Height / (float)img.Height);
                int w = (int)(img.Width * s), h = (int)(img.Height * s);
                _sb.Begin(SpriteSortMode.Immediate, BlendState.Opaque, SamplerState.LinearClamp, DepthStencilState.None, RasterizerState.CullNone);
                _sb.Draw(img, new Rectangle((vp.Width - w) / 2, (vp.Height - h) / 2, w, h), Color.White);
                _sb.End();
            }
            Driver.AfterBlit(Stopwatch.GetTimestamp() - b0);
            if (Driver.WindowTitle != _title) { _title = Driver.WindowTitle; Window.Title = _title; }
        }
        catch (Exception e) { Failure = e; Exit(); }
        base.Draw(gameTime);
        if (Driver.Done) Exit();
    }

    protected override void EndDraw()
    {
        long p0 = Stopwatch.GetTimestamp();
        base.EndDraw();
        try { Driver?.AfterPresent(Stopwatch.GetTimestamp() - p0); }
        catch (Exception e) { Failure ??= e; Exit(); }
    }

    /// <summary>Test hook: queue an SDL_QUIT event, exactly what the window's close button produces for MonoGame.</summary>
    public unsafe bool PushQuitEvent()
    {
        var push = GlApi.SdlProc("SDL_PushEvent");
        if (push == IntPtr.Zero) return false;
        var ev = stackalloc byte[64];
        new Span<byte>(ev, 64).Clear();
        *(uint*)ev = 0x100; // SDL_QUIT
        return ((delegate* unmanaged<byte*, int>)push)(ev) == 1;
    }

    /// <summary>Runs <see cref="OnEnd"/> (once) while the device is still alive.</summary>
    public void End()
    {
        if (_ended) return;
        _ended = true;
        try { OnEnd?.Invoke(this); } catch (Exception e) { Failure ??= e; }
    }

    protected override void UnloadContent()
    {
        End();
        (Driver as IDisposable)?.Dispose();
        _sb?.Dispose(); _fx?.Dispose();
        base.UnloadContent();
    }
}

/// <summary>Drives a session on an existing device (tests use the shared headless GfxHost): no window, no present.</summary>
public static class HeadlessRunner
{
    public static PerfSession Run(GraphicsDevice gd, EffectLibrary fx, PerfPlan plan, Output.PerfLog log, int maxFrames = 1_000_000)
    {
        var s = PerfSession.Start(gd, fx, plan, log, vsyncRequested: false, effectsMs: double.NaN, deviceMs: double.NaN);
        int n = 0;
        while (!s.Done && n++ < maxFrames)
        {
            s.RenderFrame(null);
            s.AfterBlit(0);
            s.AfterPresent(0);
        }
        if (!s.Done) s.Finish("frame budget exhausted", 1);
        return s;
    }
}
