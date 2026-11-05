using Gfx.Engine.Cameras;
using Gfx.Engine.Effects;
using Gfx.Engine.Rendering;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;

namespace Gfx.Engine.Scene;

/// <summary>Marks a <see cref="DemoScene"/> subclass for discovery by Gfx.Demo (<c>--capture name</c>, <c>--bench name</c>).</summary>
[AttributeUsage(AttributeTargets.Class, Inherited = false)]
public sealed class DemoSceneAttribute(string name, string description = "") : Attribute
{
    public string Name { get; } = name;
    public string Description { get; } = description;
}

/// <summary>A named still for <c>--capture</c>: camera, scene time and time of day.</summary>
public sealed record CaptureShot(string Name, CameraPose Camera, float HourOfDay = 10f, float SceneTime = 0f);

/// <summary>
/// Base for demo scenes. Lanes add scenes by subclassing in any Gfx.* assembly referenced by Gfx.Demo, with a
/// public parameterless constructor and <see cref="DemoSceneAttribute"/>. Lifecycle (all on the GL thread):
/// Load once → per frame Update(t) → renderer calls DrawShadowCasters/DrawOpaque.
/// </summary>
public abstract class DemoScene : IRenderContent, IDisposable
{
    protected GraphicsDevice Device { get; private set; } = null!;
    protected EffectLibrary Effects { get; private set; } = null!;

    public void Load(GraphicsDevice gd, EffectLibrary fx) { Device = gd; Effects = fx; OnLoad(); }
    protected abstract void OnLoad();

    /// <summary>Advance presentation state to absolute scene time (seconds). Deterministic for captures.</summary>
    public virtual void Update(float sceneTimeSeconds) { }

    public abstract IReadOnlyList<CaptureShot> Shots { get; }
    /// <summary>Starting point for the interactive tactical camera.</summary>
    public virtual Vector3 Focus => Vector3.Zero;
    /// <summary>Shadow fit sphere radius (m) around the camera target.</summary>
    public virtual float ShadowRadius => 150f;
    public virtual void Configure(RenderSettings settings) { settings.ShadowRadius = ShadowRadius; }

    public abstract void DrawShadowCasters(RenderContext rc);
    public abstract void DrawOpaque(RenderContext rc);
    public virtual void Dispose() { }
}

/// <summary>Finds <see cref="DemoSceneAttribute"/> scenes across the Gfx.* assemblies reachable from a root.</summary>
public static class SceneRegistry
{
    public sealed record Entry(string Name, string Description, Type Type)
    {
        public DemoScene Create() => (DemoScene)Activator.CreateInstance(Type)!;
    }

    public static IReadOnlyList<Entry> Discover(System.Reflection.Assembly root)
    {
        var list = new List<Entry>();
        foreach (var a in GfxDiscovery.Assemblies(root))
            foreach (var t in a.GetExportedTypes())
                if (typeof(DemoScene).IsAssignableFrom(t) && !t.IsAbstract &&
                    t.GetCustomAttributes(typeof(DemoSceneAttribute), false) is [DemoSceneAttribute attr, ..])
                    list.Add(new Entry(attr.Name, attr.Description, t));
        list.Sort((x, y) => string.CompareOrdinal(x.Name, y.Name));
        for (int i = 1; i < list.Count; i++)
            if (list[i].Name == list[i - 1].Name) throw new InvalidOperationException($"duplicate demo scene name '{list[i].Name}'");
        return list;
    }
}
