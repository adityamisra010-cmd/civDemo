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
