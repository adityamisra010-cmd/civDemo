namespace Gfx.Chars.Crowd;

/// <summary>
/// Level-of-detail rule of the instanced crowd (stage render2; PRESENTATION ONLY). Levels: 0 = the outfit's "lod1"
/// mesh (about 3,200 triangles), 1 = "lod2" (about 1,600), 2 = the crowd bake's "lod3" (about 700). The level follows
/// the soldier's projected height in pixels (so it adapts to resolution and field of view): level 0 at or above
/// <see cref="Lod1Px"/>, level 1 down to <see cref="Lod2Px"/>, level 2 below. Around each threshold a band of
/// ±<see cref="FadeBand"/> (relative) draws both levels with complementary dither masks: the outgoing (finer) level
/// keeps the pixels where the dither is at or above the fade, the incoming level the others, so every pixel shows
/// exactly one level and nothing pops. <see cref="Bias"/> scales the projected height first (2 = keep finer levels to
/// twice the distance).
/// </summary>
public sealed record CrowdLodSettings(float Lod1Px = 80f, float Lod2Px = 32f, float FadeBand = 0.15f, float Bias = 1f)
{
    public static readonly CrowdLodSettings Default = new();

    /// <summary>Projected height (pixels) of an object <paramref name="heightM"/> tall at <paramref name="distance"/> m,
    /// for a projection whose M22 is <paramref name="projM22"/> (= 1/tan(fovY/2)) on a target <paramref name="viewportH"/> high.</summary>
    public static float ProjectedPx(float heightM, float distance, float projM22, float viewportH)
        => heightM * projM22 * viewportH / (2f * MathF.Max(distance, 1e-3f));

    static float Smooth(float t) { t = Math.Clamp(t, 0f, 1f); return t * t * (3 - 2 * t); }

    /// <summary>
    /// Writes the level(s) for a soldier of projected height <paramref name="px"/>: one entry (level, 0, 0), or two
    /// entries (finer, f, +1) and (coarser, f, −1) inside a fade band. <paramref name="force"/> ≥ 0 forces that level
    /// (no fade). Returns the number of entries.
    /// </summary>
    public int Assign(float px, bool fade, int force, Span<(int level, float fade, float sign)> outp)
    {
        if (force >= 0) { outp[0] = (Math.Min(force, 2), 0, 0); return 1; }
        float p = px * Bias;
        if (!fade)
        {
            outp[0] = (p >= Lod1Px ? 0 : p >= Lod2Px ? 1 : 2, 0, 0);
            return 1;
        }
        float hi0 = Lod1Px * (1 + FadeBand), lo0 = Lod1Px * (1 - FadeBand);
        float hi1 = Lod2Px * (1 + FadeBand), lo1 = Lod2Px * (1 - FadeBand);
        if (p >= hi0) { outp[0] = (0, 0, 0); return 1; }
        if (p > lo0) { float f = Smooth((hi0 - p) / (hi0 - lo0)); outp[0] = (0, f, +1); outp[1] = (1, f, -1); return 2; }
        if (p >= hi1) { outp[0] = (1, 0, 0); return 1; }
        if (p > lo1) { float f = Smooth((hi1 - p) / (hi1 - lo1)); outp[0] = (1, f, +1); outp[1] = (2, f, -1); return 2; }
        outp[0] = (2, 0, 0); return 1;
    }

    /// <summary>The one level a shadow or reflection pass uses (no cross-fade; one level coarser than the view's, at least 1).</summary>
    public int ShadowLevel(float px, int force) => Math.Max(1, Math.Min(2, (force >= 0 ? force : (px * Bias >= Lod1Px ? 0 : px * Bias >= Lod2Px ? 1 : 2)) + 1));
}
