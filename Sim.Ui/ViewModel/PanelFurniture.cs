using System;

namespace Sim.Ui.ViewModel;

/// <summary>
/// Pure arithmetic for panel furniture (D-A1 fix, docs/art-gate-defects.md).
/// No ImGui, no MonoGame types — tested headless like the other view-models.
/// </summary>
public static class PanelFurniture
{
    /// <summary>
    /// The header rule's on-screen height in pixels. A DISPLAY POLICY, chosen
    /// (the thickness a divider rule occupies under a title bar), and
    /// deliberately NOT read from the asset: the asset's native height sets
    /// the SCALE of the artwork mapped into these pixels, never the other way
    /// round. The old code hardcoded 8f because the placeholder happened to
    /// be 8 px tall — the same class of fault as the denomination bugs this
    /// project has paid for twice (CR-002, CR-003): a literal that silently
    /// encodes one configuration.
    /// </summary>
    public const float HeaderRuleScreenHeightPx = 8f;

    /// <summary>
    /// UV extent for drawing the header rule so its ink weight is IDENTICAL
    /// at every panel width — the horizontal analogue of the parchment
    /// background's (width/128, height/128) tiling.
    ///
    /// The vertical mapping fixes a single uniform scale
    /// s = drawHeightPx / nativeHeight (full native height into the rule
    /// strip). The horizontal extent then shows drawWidthPx / s native texels
    /// and tiles the remainder: u = (drawWidthPx * nativeHeight) /
    /// (drawHeightPx * nativeWidth), v = 1.
    ///
    /// Ink-weight invariance follows by construction: texel density is
    /// nativeHeight / drawHeightPx in BOTH axes and contains no panel-width
    /// term. <see cref="HeaderRuleTexelDensity"/> exposes the density so the
    /// invariance is pinned by test rather than asserted in a comment.
    /// </summary>
    public static (float U, float V) HeaderRuleUv(
        int nativeWidth, int nativeHeight, float drawWidthPx, float drawHeightPx)
    {
        if (nativeWidth <= 0 || nativeHeight <= 0)
            throw new ArgumentOutOfRangeException(nameof(nativeWidth),
                "header rule asset has no pixels — the audit should have caught this upstream");
        if (drawWidthPx <= 0f || drawHeightPx <= 0f)
            throw new ArgumentOutOfRangeException(nameof(drawWidthPx),
                "degenerate draw rect for the header rule");

        float u = drawWidthPx * nativeHeight / (drawHeightPx * nativeWidth);
        return (u, 1f);
    }

    /// <summary>Horizontal texture addressing mode of the sampler the composed
    /// draw runs under. Mirrors MonoGame's TextureAddressMode without a
    /// MonoGame reference — the view-model stays headless; the test maps the
    /// renderer's actual configured SamplerState into this enum.</summary>
    public enum AddressModeX { Clamp, Wrap }

    /// <summary>
    /// D-A1 gate round 2 — the COMPOSITE model. The draw call in
    /// SimUiGame.DrawPanelFurniture issues ONE AddImage with uv (0,0)→(u,1),
    /// u from <see cref="HeaderRuleUv"/> (called here, so test and renderer
    /// share the computation). What that quad SHOWS depends on the sampler's
    /// address mode: under Wrap every 256-screen-px period repeats and carries
    /// the lozenge at its center; under Clamp every uv &gt; 1 re-samples the
    /// final texel column (plain heavy+hair rule, no lozenge), so the lozenge
    /// appears at most once. This function returns the screen-x centers
    /// (relative to the strip's left edge) at which the lozenge is visible.
    ///
    /// The screen period is drawHeightPx * nativeWidth / nativeHeight (the
    /// uniform scale s = drawHeightPx / nativeHeight applied to the full
    /// native width); the lozenge sits at the period's center, so centers are
    /// (k + 0.5) * period for each whole-or-partial period whose center fits
    /// inside the strip.
    /// </summary>
    public static double[] HeaderRuleVisibleDiamondCenters(
        int nativeWidth, int nativeHeight, float drawWidthPx, float drawHeightPx,
        AddressModeX addressMode)
    {
        // Period derived FROM the uv extent the draw call actually issues —
        // u = drawWidth / period, so period = drawWidth / u. If HeaderRuleUv
        // ever drifted, this model drifts with it, by construction.
        (float u, _) = HeaderRuleUv(nativeWidth, nativeHeight, drawWidthPx, drawHeightPx);
        double period = drawWidthPx / (double)u;
        var centers = new System.Collections.Generic.List<double>();
        for (int k = 0; ; k++)
        {
            double x = (k + 0.5) * period;
            if (x > drawWidthPx) break;
            // Under clamp, only texture-space uv <= 1 shows real art; the
            // lozenge of period k lives at uv = (k + 0.5) * period / drawWidth
            // * u = k + 0.5, so only k = 0 survives.
            if (addressMode == AddressModeX.Clamp && k > 0) break;
            centers.Add(x);
        }
        return centers.ToArray();
    }

    /// <summary>Native texels per screen pixel along one axis, for the rect
    /// and uv extent given. The D-A1 test asserts X and Y densities equal and
    /// panel-width-independent.</summary>
    public static (double X, double Y) HeaderRuleTexelDensity(
        int nativeWidth, int nativeHeight, float drawWidthPx, float drawHeightPx,
        float u, float v)
    {
        return (nativeWidth * (double)u / drawWidthPx,
                nativeHeight * (double)v / drawHeightPx);
    }
}
