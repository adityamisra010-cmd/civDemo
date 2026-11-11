using Gfx.Engine.Effects;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;

namespace Gfx.Engine.Rendering;

/// <summary>
/// The G-buffer-lite targets of the G2 scene pass and the screen-space passes that read them (stage render2;
/// PRESENTATION ONLY): half-resolution GTAO, a 4×4 depth-aware blur, and the full-resolution apply pass that upsamples
/// the AO (depth-aware), marches the contact shadows and multiplies the HDR scene by
/// 1 − ambient share·(1 − AO) − direct share·(1 − contact visibility). See <see cref="ScreenSpaceEffects"/> for the
/// shaders and <see cref="Mgfx.GBufferLayout"/> for what the scene writes.
/// </summary>
public sealed class ScreenSpaceLighting : IDisposable
{
    readonly GraphicsDevice _gd;
    RenderTarget2D? _depth, _split, _vel, _ao0, _ao1;
    readonly RenderTargetBinding[] _bindings = new RenderTargetBinding[4];

    /// <summary>Multiplies the destination colour by the source colour (alpha kept).</summary>
    public static readonly BlendState Multiply = new()
    {
        ColorSourceBlend = Blend.Zero, ColorDestinationBlend = Blend.SourceColor,
        AlphaSourceBlend = Blend.Zero, AlphaDestinationBlend = Blend.One,
    };

    public ScreenSpaceLighting(GraphicsDevice gd) { _gd = gd; }

    /// <summary>Full-resolution linear view depth (m) of the last G2 scene pass (R32F).</summary>
    public RenderTarget2D? Depth => _depth;
    /// <summary>Full-resolution light shares of the last G2 scene pass (r ambient, g direct, b reported).</summary>
    public RenderTarget2D? Split => _split;
    /// <summary>Full-resolution object motion of the last G2 scene pass (xy = NDC motion, z = 1 where an effect wrote it).</summary>
    public RenderTarget2D? Velocity => _vel;
    /// <summary>Half-resolution AO (r) and depth (b) after the blur (g is 1: contact shadows are marched at full resolution in the apply pass).</summary>
    public RenderTarget2D? Ao => _ao1;
    public int Width => _depth?.Width ?? 0;
    public int Height => _depth?.Height ?? 0;
    /// <summary>Last parameters given to the GTAO pass (diagnostics and tests).</summary>
    public Vector4 LastProjInfo { get; private set; }
    public Vector3 LastSunView { get; private set; }

    public void Ensure(int w, int h)
    {
        if (_depth != null && _depth.Width == w && _depth.Height == h) return;
        _depth?.Dispose(); _split?.Dispose(); _vel?.Dispose(); _ao0?.Dispose(); _ao1?.Dispose();
        // PreserveContents: the depth and shares are read after the scene target is re-bound (MonoGame clears a
        // DiscardContents target on every bind)
        _depth = new RenderTarget2D(_gd, w, h, false, SurfaceFormat.Single, DepthFormat.None, 0, RenderTargetUsage.PreserveContents);
        _split = new RenderTarget2D(_gd, w, h, false, SurfaceFormat.Color, DepthFormat.None, 0, RenderTargetUsage.PreserveContents);
        _vel = new RenderTarget2D(_gd, w, h, false, SurfaceFormat.HalfVector4, DepthFormat.None, 0, RenderTargetUsage.PreserveContents);
        int hw = Math.Max(1, (w + 1) / 2), hh = Math.Max(1, (h + 1) / 2);
        _ao0 = new RenderTarget2D(_gd, hw, hh, false, SurfaceFormat.HalfVector4, DepthFormat.None);
        _ao1 = new RenderTarget2D(_gd, hw, hh, false, SurfaceFormat.HalfVector4, DepthFormat.None);
    }

    /// <summary>The scene pass's targets: the HDR colour (with its depth buffer) plus the depth and share targets.</summary>
    public RenderTargetBinding[] SceneBindings(RenderTarget2D hdr)
    {
        _bindings[0] = new RenderTargetBinding(hdr);
        _bindings[1] = new RenderTargetBinding(_depth!);
        _bindings[2] = new RenderTargetBinding(_split!);
        _bindings[3] = new RenderTargetBinding(_vel!);
        return _bindings;
    }

    /// <summary>
    /// x = 1/P.M11, y = 1/P.M22 (view-space reconstruction), zw = the projection's NDC jitter (−P.M31, −P.M32) for a
    /// symmetric perspective with a sub-pixel translation folded in.
    /// </summary>
    public static Vector4 ProjInfo(in Matrix proj) => new(1f / proj.M11, 1f / proj.M22, -proj.M31, -proj.M32);

    /// <summary>The rotation offset of the 4×4 pattern for frame (or still sample) <paramref name="frame"/>.</summary>
    public static float FrameRotation(int frame) => frame <= 0 ? 0f : (frame * 0.618034f) % 1f;

    /// <summary>
    /// Runs GTAO + contact shadows and the blur (half resolution), then multiplies <paramref name="hdr"/> (re-bound on
    /// its own) by the apply term. Call after the opaque scene pass, before transparent content.
    /// </summary>
    public void Run(RenderContext rc, RenderTarget2D hdr, RenderFeatures f, int frame) => Run(rc, hdr, f, frame, 0);

    /// <summary>The passes with the apply pass writing a debug term (2 AO, 3 contact) into <paramref name="target"/> instead.</summary>
    public void RunDebug(RenderContext rc, RenderTarget2D? target, RenderFeatures f, int frame, int term) => Run(rc, target, f, frame, term);

    void Run(RenderContext rc, RenderTarget2D? hdr, RenderFeatures f, int frame, int debugTerm)
    {
        if (_depth == null || _ao0 == null || _ao1 == null || _split == null) return;
        int w = _depth.Width, h = _depth.Height, hw = _ao0.Width, hh = _ao0.Height;
        var full = new Vector4(w, h, 1f / w, 1f / h);
        var half = new Vector4(hw, hh, 1f / hw, 1f / hh);
        var proj = ProjInfo(rc.Projection);
        var sunView = Vector3.Normalize(Vector3.TransformNormal(rc.Sun.DirectionToLight, rc.View));
        LastProjInfo = proj; LastSunView = sunView;
        _gd.BlendState = BlendState.Opaque; _gd.DepthStencilState = DepthStencilState.None; _gd.RasterizerState = RasterizerState.CullNone;
        for (int i = 0; i < 4; i++) _gd.Textures[i] = null;

        // 1. GTAO + contact shadows (half resolution)
        var g = rc.Effects.Get(SsEffectNames.Gtao);
        _gd.SetRenderTarget(_ao0);
        g.Parameters["DepthTex"].SetValue(_depth);
        g.Parameters["ProjInfo"].SetValue(proj);
        g.Parameters["FullSize"].SetValue(full);
        g.Parameters["HalfSize"].SetValue(half);
        g.Parameters["AoParams"].SetValue(new Vector4(f.AoRadius, 1f / MathF.Max(f.AoFadeStart, 1f), MathF.Max(4f, hh / 6f), FrameRotation(frame)));
        g.Parameters["ContactParams"].SetValue(new Vector4(f.ContactLength, f.ContactThickness, f.ContactShadows ? 1 : 0, f.Ssao ? 1 : 0));
        g.Parameters["SunView"].SetValue(new Vector4(sunView, 0));
        g.CurrentTechnique.Passes[0].Apply();
        _gd.SamplerStates[0] = SamplerState.PointClamp;
        _gd.DrawUserPrimitives(PrimitiveType.TriangleList, FullScreen.Triangle, 0, 1);
        rc.Stats.RecordDraw(1, 1);
        _gd.Textures[0] = null;

        // 2. 4x4 depth-aware blur (half resolution)
        var b = rc.Effects.Get(SsEffectNames.AoBlur);
        _gd.SetRenderTarget(_ao1);
        b.Parameters["AoTex"].SetValue(_ao0);
        b.Parameters["HalfSize"].SetValue(half);
        b.CurrentTechnique.Passes[0].Apply();
        _gd.SamplerStates[0] = SamplerState.PointClamp;
        _gd.DrawUserPrimitives(PrimitiveType.TriangleList, FullScreen.Triangle, 0, 1);
        rc.Stats.RecordDraw(1, 1);
        _gd.Textures[0] = null;

        // 3. apply onto the HDR scene (multiplicative blend)
        var a = rc.Effects.Get(SsEffectNames.AoApply);
        _gd.SetRenderTarget(hdr);
        _gd.BlendState = debugTerm > 0 ? BlendState.Opaque : Multiply; _gd.DepthStencilState = DepthStencilState.None;
        a.Parameters["AoTex"].SetValue(_ao1);
        a.Parameters["DepthTex"].SetValue(_depth);
        a.Parameters["SplitTex"].SetValue(_split);
        a.Parameters["HalfSize"].SetValue(half);
        a.Parameters["FullSize"].SetValue(full);
        a.Parameters["ApplyParams"].SetValue(new Vector4(f.Ssao ? f.AoStrength : 0f, f.ContactShadows ? 1f : 0f, f.Ssao ? 0.3f : 0f, debugTerm));
        a.Parameters["ProjInfo"].SetValue(proj);
        a.Parameters["SunView"].SetValue(new Vector4(sunView, 0));
        a.Parameters["ContactParams"].SetValue(new Vector4(f.ContactLength, f.ContactThickness, f.ContactShadows ? 1 : 0, FrameRotation(frame) * 64f));
        a.CurrentTechnique.Passes[0].Apply();
        _gd.SamplerStates[0] = SamplerState.PointClamp; _gd.SamplerStates[1] = SamplerState.PointClamp; _gd.SamplerStates[2] = SamplerState.PointClamp;
        _gd.DrawUserPrimitives(PrimitiveType.TriangleList, FullScreen.Triangle, 0, 1);
        rc.Stats.RecordDraw(1, 1);
        _gd.BlendState = BlendState.Opaque;
        for (int i = 0; i < 4; i++) _gd.Textures[i] = null;
        _gd.DepthStencilState = DepthStencilState.Default;
    }

    /// <summary>Draws a debug view (<see cref="DebugView"/>) of the G-buffer-lite or the screen-space terms into the bound target.</summary>
    public void DrawDebug(RenderContext rc, DebugView view)
    {
        if (_depth == null || _split == null) return;
        var d = rc.Effects.Get(SsEffectNames.Debug);
        _gd.BlendState = BlendState.Opaque; _gd.DepthStencilState = DepthStencilState.None;
        d.Parameters["AoTex"].SetValue(_ao1);
        d.Parameters["DepthTex"].SetValue(_depth);
        d.Parameters["SplitTex"].SetValue(_split);
        int mode = view switch { DebugView.Depth => 1, DebugView.Ao => 2, DebugView.Contact => 3, DebugView.Cascades => 5, _ => 4 };
        d.Parameters["DebugParams"].SetValue(new Vector4(mode, 60f, 0, 0));
        d.CurrentTechnique.Passes[0].Apply();
        _gd.SamplerStates[0] = SamplerState.LinearClamp; _gd.SamplerStates[1] = SamplerState.PointClamp; _gd.SamplerStates[2] = SamplerState.PointClamp;
        _gd.DrawUserPrimitives(PrimitiveType.TriangleList, FullScreen.Triangle, 0, 1);
        rc.Stats.RecordDraw(1, 1);
        for (int i = 0; i < 4; i++) _gd.Textures[i] = null;
        _gd.DepthStencilState = DepthStencilState.Default;
    }

    public void Dispose() { _depth?.Dispose(); _split?.Dispose(); _vel?.Dispose(); _ao0?.Dispose(); _ao1?.Dispose(); }
}
