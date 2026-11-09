using System.Globalization;
using System.Reflection;
using Gfx.Engine.Diagnostics;
using Gfx.Engine.Scene;
using Gfx.Proc.Presentation;
using Gfx.Units;

namespace Gfx.Perf.Scenes;

/// <summary>Per-frame scene counters the harness can read without touching scene code (-1 = not present).</summary>
public readonly record struct SceneCounters(int Lod0, int Lod1, int Lod2, int Culled, int Chunks, int Props)
{
    public static readonly SceneCounters None = new(-1, -1, -1, -1, -1, -1);
}

/// <summary>
/// Finds the G1 presentation components a scene holds (its <see cref="SoldierCrowd"/>s and
/// <see cref="TerrainScenery"/>s, through fields and properties of any visibility) to read LOD counters and to
/// apply the LOD-distance multiplier through their public knobs. Presentation only: nothing here touches the
/// authoritative grid, the battle trace or any simulation value. Scenes that implement
/// <see cref="IPerfLodScalable"/> (later G2 code) are scaled through that instead.
/// </summary>
public sealed class SceneKnobs
{
    public IReadOnlyList<SoldierCrowd> Crowds { get; }
    public IReadOnlyList<TerrainScenery> Sceneries { get; }
    public IReadOnlyList<IPerfLodScalable> Scalables { get; }

    SceneKnobs(List<SoldierCrowd> c, List<TerrainScenery> s, List<IPerfLodScalable> sc) { Crowds = c; Sceneries = s; Scalables = sc; }

    /// <summary>Call after <c>scene.Load</c> (the components are created there).</summary>
    public static SceneKnobs Discover(DemoScene scene)
    {
        var crowds = new List<SoldierCrowd>(); var sceneries = new List<TerrainScenery>(); var scal = new List<IPerfLodScalable>();
        if (scene is IPerfLodScalable self) scal.Add(self);
        const BindingFlags all = BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.DeclaredOnly;
        for (var t = scene.GetType(); t != null && t != typeof(object); t = t.BaseType)
        {
            foreach (var f in t.GetFields(all))
                Add(SafeGet(() => f.GetValue(scene)));
            foreach (var p in t.GetProperties(all))
                if (p.GetIndexParameters().Length == 0 && p.GetMethod != null) Add(SafeGet(() => p.GetValue(scene)));
        }
        return new SceneKnobs(crowds, sceneries, scal);

        void Add(object? v)
        {
            switch (v)
            {
                case SoldierCrowd c when !crowds.Any(x => ReferenceEquals(x, c)): crowds.Add(c); break;
                case TerrainScenery s when !sceneries.Any(x => ReferenceEquals(x, s)): sceneries.Add(s); break;
            }
            if (v is IPerfLodScalable ps && !ReferenceEquals(v, scene) && !scal.Any(x => ReferenceEquals(x, ps))) scal.Add(ps);
        }
    }

    static object? SafeGet(Func<object?> get) { try { return get(); } catch (Exception) { return null; } }

    /// <summary>Scales every presentation LOD distance by <paramref name="m"/>; returns what changed (for the record).</summary>
    public IReadOnlyList<string> ApplyLodMultiplier(float m)
    {
        var log = new List<string>();
        if (m == 1f) return log;
        var inv = CultureInfo.InvariantCulture;
        foreach (var s in Scalables) log.Add(s.ScaleLodDistances(m));
        foreach (var c in Crowds)
        {
            float a = c.Lod0Distance, b = c.Lod1Distance;
            c.Lod0Distance = a * m; c.Lod1Distance = b * m;
            log.Add(string.Format(inv, "crowd LOD0 {0:0.#}->{1:0.#} m, LOD1 {2:0.#}->{3:0.#} m", a, c.Lod0Distance, b, c.Lod1Distance));
        }
        foreach (var s in Sceneries)
        {
            var d = s.Lod.Distances; var before = string.Join('/', d.Select(x => x.ToString("0.#", inv)));
            for (int i = 0; i < d.Length; i++) d[i] *= m;
            log.Add("terrain chunk LOD " + before + "->" + string.Join('/', d.Select(x => x.ToString("0.#", inv))) + " m");
            foreach (var g in s.PropGroups)
                for (int i = 0; i < g.Distances.Length; i++) g.Distances[i] *= m;
            float sc = s.ShadowCastDistance; s.ShadowCastDistance = sc * m;
            log.Add(string.Format(inv, "props LOD/cull distances x{0:0.##} ({1} groups), prop shadow-cast distance {2:0.#}->{3:0.#} m", m, s.PropGroups.Count, sc, s.ShadowCastDistance));
        }
        if (log.Count == 0) log.Add("no LOD knob found in this scene (the multiplier has no effect)");
        return log;
    }

    public SceneCounters Read()
    {
        if (Crowds.Count == 0 && Sceneries.Count == 0) return SceneCounters.None;
        int l0 = -1, l1 = -1, l2 = -1, cu = -1, ch = -1, pr = -1;
        if (Crowds.Count > 0) { l0 = l1 = l2 = cu = 0; foreach (var c in Crowds) { l0 += c.Lod0Count; l1 += c.Lod1Count; l2 += c.Lod2Count; cu += c.Culled; } }
        if (Sceneries.Count > 0) { ch = pr = 0; foreach (var s in Sceneries) { ch += s.LastVisibleChunks; pr += s.LastPropInstances; } }
        return new SceneCounters(l0, l1, l2, cu, ch, pr);
    }
}
