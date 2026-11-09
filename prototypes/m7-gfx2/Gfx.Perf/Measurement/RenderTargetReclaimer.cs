using System.Reflection;
using Microsoft.Xna.Framework.Graphics;

namespace Gfx.Perf.Measurement;

/// <summary>
/// Workaround for a GL resource leak in MonoGame 3.8.5 DesktopGL (pinned; found by this stage's 117-run suite).
/// <c>GraphicsDevice.PlatformDeleteRenderTarget</c> does all of its clean-up inside <c>if (GLColorBuffer != 0)</c>,
/// and <c>PlatformCreateRenderTarget</c> sets <c>GLColorBuffer</c> only for MULTISAMPLED targets. Disposing an
/// ordinary <see cref="RenderTarget2D"/> therefore never deletes its depth/stencil renderbuffers nor the framebuffer
/// objects cached for it (<c>glFramebuffers</c>, <c>glResolveFramebuffers</c>; their keys also keep the disposed
/// target object alive), and since a framebuffer still references the colour texture, GL keeps that storage too.
/// Measured on llvmpipe: about 47 MB per benchmark run at 1280x720 (renderer HDR and shadow targets plus the output
/// target), linear over 25 runs. <see cref="Reclaim"/> does for those targets what MonoGame does for MSAA ones: it
/// deletes the cached framebuffers of every DISPOSED render target and its renderbuffers. Targets still alive (or
/// currently bound) are never touched. GL thread only.
/// </summary>
public static class RenderTargetReclaimer
{
    public sealed record Result(bool Available, string? Failure, int Framebuffers, int Renderbuffers, int Targets)
    {
        public static Result Unavailable(string why) => new(false, why, 0, 0, 0);
    }

    static readonly FieldInfo? Fbos = typeof(GraphicsDevice).GetField("glFramebuffers", BindingFlags.Instance | BindingFlags.NonPublic);
    static readonly FieldInfo? ResolveFbos = typeof(GraphicsDevice).GetField("glResolveFramebuffers", BindingFlags.Instance | BindingFlags.NonPublic);
    static readonly Type? IRenderTarget = typeof(GraphicsDevice).Assembly.GetType("Microsoft.Xna.Framework.Graphics.IRenderTarget");
    static readonly PropertyInfo? ColorRb = IRenderTarget?.GetProperty("GLColorBuffer");
    static readonly PropertyInfo? DepthRb = IRenderTarget?.GetProperty("GLDepthBuffer");
    static readonly PropertyInfo? StencilRb = IRenderTarget?.GetProperty("GLStencilBuffer");

    static Dictionary<RenderTargetBinding[], int>? Cache(FieldInfo? f, GraphicsDevice gd) => f?.GetValue(gd) as Dictionary<RenderTargetBinding[], int>;

    /// <summary>Framebuffer-cache entries that reference at least one disposed render target (0 after <see cref="Reclaim"/>).</summary>
    public static int StaleFramebuffers(GraphicsDevice gd)
    {
        int n = 0;
        foreach (var c in new[] { Cache(Fbos, gd), Cache(ResolveFbos, gd) })
            if (c != null) n += c.Keys.Count(HasDisposed);
        return n;
    }

    /// <summary>Entries in MonoGame's framebuffer cache (all targets, alive or not); -1 when it cannot be read.</summary>
    public static int CachedFramebuffers(GraphicsDevice gd) => Cache(Fbos, gd)?.Count ?? -1;

    static bool HasDisposed(RenderTargetBinding[] k) => k.Any(b => b.RenderTarget is { IsDisposed: true });

    public static Result Reclaim(GraphicsDevice gd, GlApi gl)
    {
        var fbos = Cache(Fbos, gd);
        if (fbos == null || IRenderTarget == null || ColorRb == null || DepthRb == null || StencilRb == null)
            return Result.Unavailable("MonoGame internals not found (glFramebuffers / IRenderTarget): the workaround is inactive");
        var resolve = Cache(ResolveFbos, gd);
        var dead = new List<Texture>();
        int fb = 0, rb = 0;
        foreach (var cache in resolve == null ? new[] { fbos } : new[] { fbos, resolve })
        {
            var stale = cache.Keys.Where(HasDisposed).ToArray();
            foreach (var key in stale)
            {
                gl.DeleteFramebuffer(cache[key]);
                cache.Remove(key);
                fb++;
                foreach (var b in key)
                    if (b.RenderTarget is { IsDisposed: true } t && !dead.Any(x => ReferenceEquals(x, t))) dead.Add(t);
            }
        }
        foreach (var t in dead)
        {
            if (!IRenderTarget.IsInstanceOfType(t)) continue;
            if ((int)ColorRb.GetValue(t)! != 0) continue; // MSAA target: MonoGame already deleted its renderbuffers
            int depth = (int)DepthRb.GetValue(t)!, stencil = (int)StencilRb.GetValue(t)!;
            if (stencil != 0 && stencil != depth) { gl.DeleteRenderbuffer(stencil); rb++; }
            if (depth != 0) { gl.DeleteRenderbuffer(depth); rb++; }
            DepthRb.SetValue(t, 0); StencilRb.SetValue(t, 0);
        }
        gl.DrainErrors();
        return new Result(true, null, fb, rb, dead.Count);
    }
}
