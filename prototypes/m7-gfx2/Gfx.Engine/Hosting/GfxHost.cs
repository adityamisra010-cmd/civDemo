using System.Collections.Concurrent;
using System.Runtime.ExceptionServices;
using System.Runtime.InteropServices;
using Gfx.Engine.Effects;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;

namespace Gfx.Engine.Hosting;

/// <summary>Headless environment: on Linux without a display, select SDL's offscreen (EGL) driver + Mesa software GL.</summary>
public static class HeadlessEnvironment
{
    [DllImport("libc", EntryPoint = "setenv")] static extern int SetEnv(string name, string value, int overwrite);

    /// <summary>
    /// Sets SDL_VIDEODRIVER=offscreen and LIBGL_ALWAYS_SOFTWARE=1 in the NATIVE environment (SDL/Mesa read getenv;
    /// .NET's Environment.SetEnvironmentVariable does not reach it on Unix) unless already set. Linux only;
    /// when DISPLAY/WAYLAND_DISPLAY exist nothing is forced unless <paramref name="force"/>.
    /// </summary>
    public static void Configure(bool force = false)
    {
        if (!OperatingSystem.IsLinux()) return;
        bool hasDisplay = !string.IsNullOrEmpty(Environment.GetEnvironmentVariable("DISPLAY"))
                          || !string.IsNullOrEmpty(Environment.GetEnvironmentVariable("WAYLAND_DISPLAY"));
        if (hasDisplay && !force) return;
        Set("SDL_VIDEODRIVER", "offscreen");
        Set("LIBGL_ALWAYS_SOFTWARE", "1");
    }

    static void Set(string k, string v)
    {
        if (!string.IsNullOrEmpty(Environment.GetEnvironmentVariable(k))) return;
        Environment.SetEnvironmentVariable(k, v);
        try { SetEnv(k, v, 0); } catch (Exception) { /* no libc: leave it to the caller's environment */ }
    }
}

/// <summary>Minimal Game used only to own an SDL window + HiDef GL device.</summary>
internal sealed class HostGame : Game
{
    public HostGame(int w, int h)
    {
        _ = new GraphicsDeviceManager(this)
        {
            GraphicsProfile = GraphicsProfile.HiDef, PreferredBackBufferWidth = w, PreferredBackBufferHeight = h,
            SynchronizeWithVerticalRetrace = false, PreferredDepthStencilFormat = DepthFormat.Depth24Stencil8,
        };
        IsFixedTimeStep = false;
    }
}

/// <summary>
/// Process-wide headless GL device on ONE dedicated thread (GL contexts and MonoGame's "UI thread" are
/// thread-bound; MonoGame latches the first thread that touches it, so the host is a singleton and is never
/// recreated). ALL GPU work goes through <see cref="Invoke{T}"/>. Use <see cref="TryGetShared"/> in tests and
/// skip when it fails.
/// </summary>
public sealed class GfxHost
{
    static readonly object Gate = new();
    static GfxHost? _shared; static string? _failure;

    readonly BlockingCollection<Action> _queue = new();
    readonly Thread _thread;
    HostGame? _game;
    public GraphicsDevice Device => _game!.GraphicsDevice;
    /// <summary>Effect library over the Gfx.* assemblies reachable from the root given at creation.</summary>
    public EffectLibrary Effects { get; private set; } = null!;

    GfxHost(System.Reflection.Assembly root)
    {
        Exception? startError = null;
        using var ready = new ManualResetEventSlim();
        _thread = new Thread(() =>
        {
            try
            {
                _game = new HostGame(320, 180);
                _game.RunOneFrame(); // creates the device (Game.DoInitialize → CreateDevice)
                Effects = new EffectLibrary(_game.GraphicsDevice, root);
            }
            catch (Exception e) { startError = e; }
            ready.Set();
            if (startError != null) return;
            foreach (var work in _queue.GetConsumingEnumerable()) work();
        }) { IsBackground = true, Name = "GfxHost" };
        _thread.Start();
        ready.Wait();
        if (startError != null) ExceptionDispatchInfo.Capture(startError).Throw();
    }

    /// <summary>
    /// The shared host (created on first call). <paramref name="discoveryRoot"/> selects which Gfx.* assemblies'
    /// effects are registered (pass your test/demo assembly); only the FIRST call's root is used.
    /// </summary>
    public static bool TryGetShared(out GfxHost? host, out string? reason, System.Reflection.Assembly? discoveryRoot = null)
    {
        lock (Gate)
        {
            if (_shared == null && _failure == null)
            {
                HeadlessEnvironment.Configure();
                try { _shared = new GfxHost(discoveryRoot ?? System.Reflection.Assembly.GetCallingAssembly()); }
                catch (Exception e) { _failure = $"{e.GetType().Name}: {e.Message}"; }
            }
            host = _shared; reason = _failure;
            return host != null;
        }
    }

    public T Invoke<T>(Func<GraphicsDevice, T> work)
    {
        if (Thread.CurrentThread == _thread) return work(Device);
        T result = default!; ExceptionDispatchInfo? err = null;
        using var done = new ManualResetEventSlim();
        _queue.Add(() => { try { result = work(Device); } catch (Exception e) { err = ExceptionDispatchInfo.Capture(e); } finally { done.Set(); } });
        done.Wait();
        err?.Throw();
        return result;
    }

    public void Invoke(Action<GraphicsDevice> work) => Invoke<int>(gd => { work(gd); return 0; });
}
