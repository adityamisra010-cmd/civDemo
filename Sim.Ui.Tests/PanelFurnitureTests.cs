using System;
using Sim.Ui.ViewModel;
using Xunit;

namespace Sim.Ui.Tests;

/// <summary>
/// D-A1 (docs/art-gate-defects.md): the header rule was drawn with uv
/// (0,0)-(1,1) across the whole panel width, stretching the native art and
/// changing its ink weight with panel size. These tests pin the corrected
/// arithmetic. Proven RED against the old behaviour: substituting the old
/// extent (u = 1 at every width) fails IsotropicDensity (density X 0.0009 vs
/// Y 128 at a 900 px panel) and fails WidthInvariance (density varies 4.5x
/// between 200 px and 900 px panels).
/// </summary>
public class PanelFurnitureTests
{
    // The director's real drop is 1536x1024; the placeholder is 64x8. Both
    // must obey the same law — the fix's whole point is that no dimension is
    // baked in.
    [Theory]
    [InlineData(1536, 1024)]
    [InlineData(64, 8)]
    [InlineData(300, 300)]
    public void HeaderRule_TexelDensity_IsIsotropic_AtEveryPanelWidth(int nw, int nh)
    {
        float h = PanelFurniture.HeaderRuleScreenHeightPx;
        foreach (float w in new[] { 188f, 388f, 888f })   // 200/400/900 px panels minus the 12 px inset
        {
            (float u, float v) = PanelFurniture.HeaderRuleUv(nw, nh, w, h);
            (double dx, double dy) = PanelFurniture.HeaderRuleTexelDensity(nw, nh, w, h, u, v);
            Assert.Equal(dy, dx, 3);   // same scale both axes: no stretch (3 dp — float32 uv rounds at ~1e-6 relative, ~3e-4 absolute at density 128)
        }
    }

    [Fact]
    public void HeaderRule_InkWeight_IsIdentical_At200And900PxPanels()
    {
        // "Visually identical ink weight" made checkable: the texel density
        // (native texels per screen pixel) is what sets stroke thickness on
        // screen. It must not contain a panel-width term.
        float h = PanelFurniture.HeaderRuleScreenHeightPx;
        (float uNarrow, float vNarrow) = PanelFurniture.HeaderRuleUv(1536, 1024, 188f, h);
        (float uWide, float vWide) = PanelFurniture.HeaderRuleUv(1536, 1024, 888f, h);
        (double dxN, double dyN) = PanelFurniture.HeaderRuleTexelDensity(1536, 1024, 188f, h, uNarrow, vNarrow);
        (double dxW, double dyW) = PanelFurniture.HeaderRuleTexelDensity(1536, 1024, 888f, h, uWide, vWide);
        Assert.Equal(dxN, dxW, 3);
        Assert.Equal(dyN, dyW, 3);
        // And the density is exactly the vertical mapping, nativeH / drawH —
        // the single uniform scale the design states.
        Assert.Equal(1024.0 / h, dyN, 3);
    }

    [Fact]
    public void HeaderRule_WiderPanel_TilesMoreTexels_NeverStretches()
    {
        // Monotone: doubling the panel width doubles the uv extent (more art
        // shown), rather than stretching the same texels wider. The old code
        // pinned u = 1 at every width; this fails against it.
        float h = PanelFurniture.HeaderRuleScreenHeightPx;
        (float u1, _) = PanelFurniture.HeaderRuleUv(1536, 1024, 400f, h);
        (float u2, _) = PanelFurniture.HeaderRuleUv(1536, 1024, 800f, h);
        Assert.Equal(u1 * 2f, u2, 5);
    }

    [Theory]
    [InlineData(0, 1024, 100f, 8f)]
    [InlineData(1536, 0, 100f, 8f)]
    [InlineData(1536, 1024, 0f, 8f)]
    [InlineData(1536, 1024, 100f, 0f)]
    public void HeaderRuleUv_RejectsDegenerateInputs(int nw, int nh, float w, float h)
    {
        Assert.Throws<ArgumentOutOfRangeException>(() => PanelFurniture.HeaderRuleUv(nw, nh, w, h));
    }
}
