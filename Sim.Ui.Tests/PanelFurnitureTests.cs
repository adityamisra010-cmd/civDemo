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

    // ------------------------------------------------------------------
    // D-A1 gate round 2: the COMPOSITE tests. Every earlier test certified a
    // PIECE (generator seamless, uv density exact) and none certified the
    // RESULT; the gate build showed ONE lozenge on every panel because the
    // ImGui renderer sampled under LinearClamp inherited from the previous
    // SpriteBatch pass. These tests assert the composed draw one layer below
    // the GPU: the pure model of (draw rect, uv extent, sampler address mode)
    // → visible lozenge centers, with the address mode read from the ACTUAL
    // SamplerState the renderer configures (ImGuiRenderer.TextureSampler),
    // and the uv extent from the ACTUAL HeaderRuleUv the draw call uses.
    //
    // UNCOVERED, stated per the packet: the real GPU sampling itself — that
    // MonoGame honours SamplerStates[0] for BasicEffect draws, and that
    // RenderDrawData applies TextureSampler on the device. That last hop is
    // not inspectable headless (ImGui draw lists need a GraphicsDevice); it
    // takes one eyeball on the gate build. Everything computable before that
    // hop is asserted here.
    // ------------------------------------------------------------------

    private static PanelFurniture.AddressModeX RendererAddressModeX()
        => Sim.Ui.ImGuiIntegration.ImGuiRenderer.TextureSampler.AddressU
               == Microsoft.Xna.Framework.Graphics.TextureAddressMode.Wrap
           ? PanelFurniture.AddressModeX.Wrap
           : PanelFurniture.AddressModeX.Clamp;

    [Fact]
    public void ImGuiSampler_Wraps_BothAxes()
    {
        // The parchment background tiles in BOTH axes (uv = size/128); the
        // header rule tiles in x. Both require wrap addressing.
        var s = Sim.Ui.ImGuiIntegration.ImGuiRenderer.TextureSampler;
        Assert.Equal(Microsoft.Xna.Framework.Graphics.TextureAddressMode.Wrap, s.AddressU);
        Assert.Equal(Microsoft.Xna.Framework.Graphics.TextureAddressMode.Wrap, s.AddressV);
    }

    [Theory]
    // 256 px screen period (drawH 8 × nativeW 1024 / nativeH 32); lozenge at
    // period centre 128 + 256k. Directors' gate widths:
    [InlineData(705f, new double[] { 128, 384, 640 })]
    [InlineData(1283f, new double[] { 128, 384, 640, 896, 1152 })]
    // one narrow panel: a single lozenge, still centred at 128
    [InlineData(300f, new double[] { 128 })]
    public void ComposedDraw_LozengeRepeats_AtPredictedCenters(
        float drawWidth, double[] expectedCenters)
    {
        double[] centers = PanelFurniture.HeaderRuleVisibleDiamondCenters(
            Sim.Ui.Art.HeaderRuleBaker.NativeWidth, Sim.Ui.Art.HeaderRuleBaker.NativeHeight,
            drawWidth, PanelFurniture.HeaderRuleScreenHeightPx,
            RendererAddressModeX());
        Assert.Equal(expectedCenters, centers);
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
