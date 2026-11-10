using Gfx.Proc.Environment.EzTree;
using Microsoft.Xna.Framework;

namespace Gfx.Proc.Environment.Trees;

/// <summary>Baked bark material sets (scripts/bake-env.py, ambientCG bark via ez-tree, CC0).</summary>
public enum BarkSet { Oak, Ash, Aspen, Pine, Shrub, Scrub }

/// <summary>Baked leaf cards (scripts/bake-env.py, ez-tree leaf photographs, MIT; aspen, scrub and juniper recoloured).</summary>
public enum LeafSet { Oak, Ash, Aspen, Pine, Scrub, Juniper }

/// <summary>
/// One species: which ez-tree presets grow its variants, its materials, its size range and its look. Heights are
/// metres; the ez-tree model is scaled so its top reaches the variant's height. PRESENTATION ONLY.
/// </summary>
public sealed record TreeSpecies(
    string Name, string[] Presets, BarkSet Bark, LeafSet Leaf, float MinHeight, float MaxHeight,
    Vector3 LeafTint, Vector3 BarkTint, Vector3 Transmission, bool IsShrub = false, int SeedVariants = 1,
    Func<EzTreeOptions, EzTreeOptions>? Modify = null)
{
    /// <summary>Oak: broad rounded crowns, deeply furrowed grey bark.</summary>
    public static readonly TreeSpecies Oak = new("oak", new[] { "oak_medium", "oak_large", "oak_small" }, BarkSet.Oak, LeafSet.Oak,
        15f, 23f, new(1.0f, 1.0f, 1.0f), new(1, 1, 1), new(0.55f, 0.75f, 0.22f), SeedVariants: 2, Modify: Summer);
    /// <summary>Ash: taller, lighter and more open crowns of pinnate leaves.</summary>
    public static readonly TreeSpecies Ash = new("ash", new[] { "ash_medium", "ash_large", "ash_small" }, BarkSet.Ash, LeafSet.Ash,
        16f, 25f, new(1.0f, 1.02f, 0.95f), new(1, 1, 1), new(0.60f, 0.80f, 0.25f), SeedVariants: 2, Modify: Summer);
    /// <summary>Aspen: slender pale trunks, light trembling foliage; a pioneer of forest edges and clearings.</summary>
    public static readonly TreeSpecies Aspen = new("aspen", new[] { "aspen_medium", "aspen_large", "aspen_small" }, BarkSet.Aspen, LeafSet.Aspen,
        11f, 19f, new(1.0f, 1.0f, 1.0f), new(1, 1, 1), new(0.65f, 0.82f, 0.30f), Modify: Summer);
    /// <summary>Pine: conical evergreen crowns with red-brown plated bark.</summary>
    public static readonly TreeSpecies Pine = new("pine", new[] { "pine_medium", "pine_large", "pine_small" }, BarkSet.Pine, LeafSet.Pine,
        16f, 27f, new(1.0f, 1.0f, 1.0f), new(1, 1, 1), new(0.40f, 0.55f, 0.18f), SeedVariants: 2);
    /// <summary>Temperate shrubs (hazel, hawthorn and similar) for forest mantles and hedges.</summary>
    public static readonly TreeSpecies Shrub = new("shrub", new[] { "bush_1", "bush_2" }, BarkSet.Shrub, LeafSet.Ash,
        2.0f, 4.0f, new(0.95f, 1.0f, 0.92f), new(1, 1, 1), new(0.58f, 0.78f, 0.24f), IsShrub: true, SeedVariants: 2);
    /// <summary>Arid maquis scrub: low, dense, dusty olive foliage.</summary>
    public static readonly TreeSpecies Scrub = new("scrub", new[] { "bush_1", "bush_2" }, BarkSet.Scrub, LeafSet.Scrub,
        0.9f, 2.0f, new(1.30f, 1.22f, 1.05f), new(1, 1, 1), new(0.40f, 0.40f, 0.26f), IsShrub: true, SeedVariants: 2,
        Modify: o => { o.LeafSize *= 0.75; o.LeafCount = Math.Round(o.LeafCount * 1.4); return o; });
    /// <summary>Arid juniper: evergreen, blue-grey-green scale foliage on a dense bush.</summary>
    public static readonly TreeSpecies Juniper = new("juniper", new[] { "bush_3" }, BarkSet.Scrub, LeafSet.Juniper,
        1.4f, 3.2f, new(1.20f, 1.18f, 1.12f), new(1, 1, 1), new(0.32f, 0.34f, 0.24f), IsShrub: true, SeedVariants: 2);
    /// <summary>Arid pine (Aleppo-like): sparse, open crowns over dry ground.</summary>
    public static readonly TreeSpecies AridPine = new("aridpine", new[] { "pine_small", "pine_medium" }, BarkSet.Pine, LeafSet.Pine,
        9f, 15f, new(1.0f, 1.0f, 0.88f), new(1, 1, 1), new(0.38f, 0.42f, 0.22f), SeedVariants: 1,
        Modify: o => { o.Children[0] = Math.Round(o.Children[0] * 0.6); o.LeafSize *= 0.9; return o; });

    /// <summary>Summer canopy: ez-tree's presets are open; a full summer crown carries about 1.5× the leaf cards, a bit larger.</summary>
    static EzTreeOptions Summer(EzTreeOptions o) { o.LeafCount = Math.Round(o.LeafCount * 1.5); o.LeafSize *= 1.12; return o; }

    public static readonly TreeSpecies[] Temperate = { Oak, Ash, Aspen, Pine, Shrub };
    public static readonly TreeSpecies[] Arid = { AridPine, Scrub, Juniper };

    /// <summary>The ez-tree options of variant <paramref name="index"/> (preset index × seed variant).</summary>
    public EzTreeOptions VariantOptions(int index)
    {
        string preset = Presets[index % Presets.Length];
        int seedVariant = index / Presets.Length;
        var o = EzTreePresets.Load(preset);
        o.Seed += seedVariant * 7919L;
        return Modify != null ? Modify(o) : o;
    }

    public int VariantCount => Presets.Length * SeedVariants;

    /// <summary>The variant's nominal height (m): presets ordered medium, large, small map to the middle, top and bottom of the range.</summary>
    public float VariantHeight(int index)
    {
        string preset = Presets[index % Presets.Length];
        float t = preset.EndsWith("_large", StringComparison.Ordinal) ? 0.85f : preset.EndsWith("_small", StringComparison.Ordinal) ? 0.2f : 0.55f;
        if (IsShrub) t = 0.35f + 0.3f * (index % Presets.Length) / Math.Max(1, Presets.Length - 1);
        t += 0.06f * (index / Presets.Length);
        return MathHelper.Lerp(MinHeight, MaxHeight, Math.Clamp(t, 0f, 1f));
    }
}
