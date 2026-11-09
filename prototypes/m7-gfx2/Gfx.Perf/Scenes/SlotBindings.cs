using System.Globalization;
using Gfx.Engine.Scene;
using Gfx.Units.Demo;

namespace Gfx.Perf.Scenes;

/// <summary>
/// Harness stand-in for a formation of N soldiers: G1's own synthetic battle line (<see cref="UnitsCrowdScene"/>,
/// the class behind <c>units-bench-1k</c>/<c>units-bench-15k</c>) with a configurable count. No new art and no
/// new layout: the count is the only difference. Not a registered demo scene.
/// </summary>
public sealed class PerfCrowdScene(int count) : UnitsCrowdScene
{
    protected override int Count => count;
    public int RequestedCount => count;
}

/// <summary>A resolved slot binding: the scene spec and how to build it.</summary>
public sealed record SceneBinding(char Slot, string Spec, string Description, bool SupportsCrowd, Func<int?, DemoScene> Create);

/// <summary>
/// Scene slots (D-078 QM8): A soldier close-up, B formation of 1,000, C large battlefield, D cinematic,
/// E campaign environment. Each slot is BOUND to a concrete scene spec: a registered demo scene name
/// (<c>--list</c>) or <c>crowd:N</c> (G1's synthetic battle line with N soldiers). The defaults are G1
/// stand-ins; later G2 stages rebind slots to their own scenes with <c>--bind A=name,…</c>.
/// </summary>
public static class SlotBindings
{
    public static readonly IReadOnlyDictionary<char, string> Defaults = new SortedDictionary<char, string>
    {
        ['A'] = "units-closeup",
        ['B'] = "crowd:1000",
        ['C'] = "battlefield",
        ['D'] = "battlefield-cine",
        ['E'] = "terrain-temperate",
    };

    public static string SlotTitle(char slot) => slot switch
    {
        'A' => "soldier close-up",
        'B' => "formation of 1,000",
        'C' => "large battlefield",
        'D' => "cinematic",
        'E' => "campaign environment",
        _ => "?",
    };

    /// <summary>What the G1 stand-in lacks against the slot's QM8 definition (said plainly in every report).</summary>
    public static string StandInGap(char slot, string spec) => (slot, spec) switch
    {
        ('A', "units-closeup") => "G1 low-poly procedural soldiers (kits x clips), flat ground; no faces, no authored asset",
        ('B', _) when spec.StartsWith("crowd:", StringComparison.Ordinal) => "G1 synthetic battle line, low-poly procedural soldiers on flat ground",
        ('C', "battlefield") => "G1 procedural terrain + scatter trees/rocks + 220 fixture soldiers (QM8 asks for thousands, structures and effects)",
        ('D', "battlefield-cine") => "G1 parametric avatars (surrender beat) on the procedural battlefield; placeholder faces and dress",
        ('E', "terrain-temperate") => "G1 procedural terrain only: there is NO settlement in G1 (QM8 E asks for a detailed settlement)",
        _ => "custom binding",
    };

    /// <summary>Parses "A=name,B=crowd:2000" over the defaults. Returns null and an error for bad input.</summary>
    public static IReadOnlyDictionary<char, string>? Parse(string? bind, out string? error)
    {
        var map = new SortedDictionary<char, string>(Defaults.ToDictionary(k => k.Key, v => v.Value));
        error = null;
        if (string.IsNullOrWhiteSpace(bind)) return map;
        foreach (var part in bind.Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries))
        {
            int eq = part.IndexOf('=');
            if (eq != 1 || part.Length < 3) { error = $"--bind: '{part}' is not SLOT=SCENE (slots A-E)"; return null; }
            char slot = char.ToUpperInvariant(part[0]);
            if (!Defaults.ContainsKey(slot)) { error = $"--bind: unknown slot '{part[0]}' (slots are A-E)"; return null; }
            map[slot] = part[(eq + 1)..].Trim();
        }
        return map;
    }

    /// <summary>Validates a spec against the registered scenes and returns its factory, or null with an error.</summary>
    public static SceneBinding? Resolve(char slot, string spec, IReadOnlyList<SceneRegistry.Entry> registry, out string? error)
    {
        error = null;
        if (spec.StartsWith("crowd:", StringComparison.Ordinal))
        {
            if (!int.TryParse(spec.AsSpan(6), NumberStyles.None, CultureInfo.InvariantCulture, out int n) || n < 1 || n > 100_000)
            { error = $"scene spec '{spec}': crowd:N needs 1 <= N <= 100000"; return null; }
            return new SceneBinding(slot, spec, $"G1 synthetic battle line, {n} soldiers (UnitsCrowdScene)", true, c => new PerfCrowdScene(c ?? n));
        }
        var e = registry.FirstOrDefault(r => r.Name == spec);
        if (e == null) { error = $"scene spec '{spec}' is neither a registered scene (--list) nor crowd:N"; return null; }
        return new SceneBinding(slot, spec, e.Description, false, c =>
        {
            if (c != null) throw new InvalidOperationException($"slot {slot} is bound to '{spec}', which has no crowd-size parameter");
            return e.Create();
        });
    }
}
