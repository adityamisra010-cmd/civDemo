using System.Reflection;
using Gfx.Engine.Mgfx;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;

namespace Gfx.Engine.Effects;

/// <summary>
/// Implement in any Gfx.* library (public, parameterless constructor) to ship effects; the library is found by
/// <see cref="GfxDiscovery"/> from the root assembly (Gfx.Demo / Gfx.Tests reference your project).
/// </summary>
public interface IEffectSource
{
    IEnumerable<EffectBuilder> CreateEffects();
}

/// <summary>Per-device effect cache. Effects are generated from GLSL at runtime (no content pipeline, ADR-009).</summary>
public sealed class EffectLibrary : IDisposable
{
    readonly GraphicsDevice _gd;
    readonly SortedDictionary<string, EffectBuilder> _builders = new(StringComparer.Ordinal);
    readonly Dictionary<string, Effect> _effects = new(StringComparer.Ordinal);

    public EffectLibrary(GraphicsDevice gd, Assembly? discoveryRoot = null)
    {
        _gd = gd;
        foreach (var src in GfxDiscovery.Instances<IEffectSource>(discoveryRoot ?? typeof(EffectLibrary).Assembly))
            foreach (var b in src.CreateEffects()) Register(b);
    }

    public void Register(EffectBuilder builder)
    {
        if (!_builders.TryAdd(builder.Name, builder))
            throw new InvalidOperationException($"Effect '{builder.Name}' registered twice");
    }

    /// <summary>Registered names in ordinal order.</summary>
    public IReadOnlyList<string> Names => _builders.Keys.ToArray();
    public bool Contains(string name) => _builders.ContainsKey(name);
    public EffectBuilder Builder(string name) => _builders[name];

    public Effect Get(string name)
    {
        if (_effects.TryGetValue(name, out var e)) return e;
        if (!_builders.TryGetValue(name, out var b)) throw new KeyNotFoundException($"No effect '{name}'. Registered: {string.Join(", ", Names)}");
        e = b.Build(_gd);
        _effects[name] = e;
        return e;
    }

    /// <summary>
    /// Forces GLSL compile + link of every registered effect (MonoGame compiles on first draw) with a one-triangle
    /// draw into a scratch target. Returns per-effect errors (null = loaded). Also removes first-draw hitches.
    /// </summary>
    public IReadOnlyList<(string name, Exception? error)> WarmupAll()
    {
        var results = new List<(string, Exception?)>();
        var prev = _gd.GetRenderTargets();
        using var rt = new RenderTarget2D(_gd, 4, 4, false, SurfaceFormat.Color, DepthFormat.Depth24);
        _gd.SetRenderTarget(rt);
        var tri = new[] { new VertexPosition(new Vector3(-1, -1, 0)), new VertexPosition(new Vector3(3, -1, 0)), new VertexPosition(new Vector3(-1, 3, 0)) };
        foreach (var name in Names)
        {
            try
            {
                var e = Get(name);
                e.CurrentTechnique.Passes[0].Apply();
                for (int s = 0; s < 4; s++) _gd.SamplerStates[s] = SamplerState.PointClamp;
                _gd.DrawUserPrimitives(PrimitiveType.TriangleList, tri, 0, 1);
                results.Add((name, null));
            }
            catch (Exception ex) { results.Add((name, ex)); }
        }
        _gd.SetRenderTargets(prev);
        for (int s = 0; s < 4; s++) _gd.Textures[s] = null;
        return results;
    }

    public void Dispose()
    {
        foreach (var e in _effects.Values) e.Dispose();
        _effects.Clear();
    }
}

/// <summary>Reflection discovery over the Gfx.* assemblies reachable from a root assembly or deployed beside the app (ordinal order).</summary>
public static class GfxDiscovery
{
    public static IReadOnlyList<Assembly> Assemblies(Assembly root)
    {
        var seen = new SortedDictionary<string, Assembly>(StringComparer.Ordinal);
        var stack = new Stack<Assembly>(); stack.Push(root); stack.Push(typeof(GfxDiscovery).Assembly);
        // The compiler drops assembly references whose types are never used in code, so a ProjectReference alone
        // is not visible through GetReferencedAssemblies: also load every Gfx.*.dll next to the app.
        foreach (var f in Directory.GetFiles(AppContext.BaseDirectory, "Gfx.*.dll").OrderBy(x => x, StringComparer.Ordinal))
        {
            try { stack.Push(Assembly.LoadFrom(f)); } catch (BadImageFormatException) { }
        }
        while (stack.Count > 0)
        {
            var a = stack.Pop();
            string n = a.GetName().Name ?? "";
            if (!seen.TryAdd(n, a)) continue;
            foreach (var r in a.GetReferencedAssemblies())
                if (r.Name is { } rn && rn.StartsWith("Gfx.", StringComparison.Ordinal) && !seen.ContainsKey(rn))
                {
                    try { stack.Push(Assembly.Load(r)); } catch (FileNotFoundException) { }
                }
        }
        return seen.Values.ToArray();
    }

    /// <summary>Instantiates every public concrete T with a parameterless ctor, ordered by full type name.</summary>
    public static IReadOnlyList<T> Instances<T>(Assembly root) where T : class
    {
        var types = new List<Type>();
        foreach (var a in Assemblies(root))
            foreach (var t in a.GetExportedTypes())
                if (typeof(T).IsAssignableFrom(t) && t is { IsAbstract: false, IsInterface: false } && t.GetConstructor(Type.EmptyTypes) != null)
                    types.Add(t);
        types.Sort((x, y) => string.CompareOrdinal(x.FullName, y.FullName));
        return types.Select(t => (T)Activator.CreateInstance(t)!).ToArray();
    }
}
