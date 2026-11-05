using Gfx.Engine.Cameras;
using Gfx.Engine.Diagnostics;
using Gfx.Engine.Effects;
using Gfx.Engine.Scene;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;

namespace Gfx.Engine.Rendering;

public sealed class RenderSettings
{
    public bool Shadows { get; set; } = true;
    public int ShadowMapSize { get; set; } = 2048;
    public float Exposure { get; set; } = 0.85f;
    public bool Tonemap { get; set; } = true;
    /// <summary>Shadow fit sphere (world). Default: 150 m around the camera target.</summary>
    public Vector3? ShadowFocus { get; set; }
    public float ShadowRadius { get; set; } = 150f;
    public Vector4 FogParams { get; set; } = new(0.0015f, 0.02f, 0f, 0.85f);
}

/// <summary>
/// Frame: [shadow depth pass] → HDR scene target (sky, then opaque content) → post (exposure, ACES, gamma) into the
/// output target (or the back buffer when null). HDR target is HalfVector4 when the device accepts it, else Color
/// (then highlights clip before tonemapping — <see cref="HdrFormat"/> reports which).
/// </summary>
public sealed class ForwardRenderer : IDisposable
{
    readonly GraphicsDevice _gd;
    readonly EffectLibrary _fx;
    RenderTarget2D? _hdr;
    ShadowMap? _shadow;
    public RenderContext Context { get; }
    public FrameStats Stats { get; }
    public RenderSettings Settings { get; } = new();
    public SurfaceFormat HdrFormat { get; private set; } = SurfaceFormat.HalfVector4;

    public ForwardRenderer(GraphicsDevice gd, EffectLibrary fx, FrameStats? stats = null)
    {
        _gd = gd; _fx = fx; Stats = stats ?? new FrameStats();
        Context = new RenderContext { Device = gd, Effects = fx, Stats = Stats };
    }

    public ShadowMap? ShadowMap => _shadow;

    void EnsureTargets(int w, int h)
    {
        if (_hdr != null && _hdr.Width == w && _hdr.Height == h) return;
        _hdr?.Dispose();
        try { _hdr = new RenderTarget2D(_gd, w, h, false, HdrFormat, DepthFormat.Depth24Stencil8); }
        catch (Exception) when (HdrFormat != SurfaceFormat.Color)
        {
            HdrFormat = SurfaceFormat.Color;
            _hdr = new RenderTarget2D(_gd, w, h, false, HdrFormat, DepthFormat.Depth24Stencil8);
        }
    }

    /// <summary>Renders one frame of <paramref name="content"/> into <paramref name="output"/> (back buffer if null).</summary>
    public void Render(IRenderContent content, CameraPose camera, SunState sun, RenderTarget2D? output, float timeSeconds = 0)
    {
        int w = output?.Width ?? _gd.PresentationParameters.BackBufferWidth;
        int h = output?.Height ?? _gd.PresentationParameters.BackBufferHeight;
        EnsureTargets(w, h);
        var rc = Context;
        rc.Camera = camera; rc.Sun = sun; rc.TimeSeconds = timeSeconds; rc.FogParams = Settings.FogParams;
        rc.View = camera.View; rc.Projection = camera.Projection(w / (float)h); rc.ViewProj = rc.View * rc.Projection;
        rc.Frustum = new Frustum(rc.ViewProj);
        for (int i = 0; i < 4; i++) _gd.Textures[i] = null;

        // 1. shadow depth
        if (Settings.Shadows)
        {
            if (_shadow == null || _shadow.Size != Settings.ShadowMapSize) { _shadow?.Dispose(); _shadow = new ShadowMap(_gd, Settings.ShadowMapSize); }
            _shadow.Fit(sun.DirectionToLight, Settings.ShadowFocus ?? camera.Target, Settings.ShadowRadius);
            rc.LightViewProj = _shadow.LightViewProj; rc.ShadowParams = _shadow.Params(true); rc.ShadowTexture = null;
            rc.ShadowNormalOffset = _shadow.NormalOffsetWorld;
            _gd.SetRenderTarget(_shadow.Target);
            _gd.Clear(ClearOptions.Target | ClearOptions.DepthBuffer, Color.White, 1f, 0);
            _gd.DepthStencilState = DepthStencilState.Default; _gd.RasterizerState = RasterizerState.CullNone; _gd.BlendState = BlendState.Opaque;
            rc.IsShadowPass = true;
            content.DrawShadowCasters(rc);
            rc.IsShadowPass = false;
            rc.ShadowTexture = _shadow.Target;
        }
        else { rc.LightViewProj = Matrix.Identity; rc.ShadowParams = Vector4.Zero; rc.ShadowTexture = null; }

        // 2. HDR scene: sky then opaque
        _gd.SetRenderTarget(_hdr);
        _gd.Clear(ClearOptions.Target | ClearOptions.DepthBuffer | ClearOptions.Stencil, Color.Black, 1f, 0);
        _gd.BlendState = BlendState.Opaque; _gd.DepthStencilState = DepthStencilState.None; _gd.RasterizerState = RasterizerState.CullNone;
        var sky = _fx.Get(EffectNames.Sky);
        sky.Parameters["InvViewProj"].SetValue(Matrix.Invert(rc.ViewProj));
        sky.Parameters["SkyZenith"].SetValue(new Vector4(sun.SkyZenith, 1));
        sky.Parameters["SkyHorizon"].SetValue(new Vector4(sun.SkyHorizon, 1));
        rc.Apply(sky);
        _gd.DrawUserPrimitives(PrimitiveType.TriangleList, FullScreen.Triangle, 0, 1);
        Stats.RecordDraw(1, 1);
        _gd.DepthStencilState = DepthStencilState.Default;
        content.DrawOpaque(rc);

        // 3. post → output
        _gd.SetRenderTarget(output);
        _gd.DepthStencilState = DepthStencilState.None; _gd.BlendState = BlendState.Opaque;
        for (int i = 0; i < 4; i++) _gd.Textures[i] = null;
        var post = _fx.Get(EffectNames.Post);
        post.Parameters["SceneTex"].SetValue(_hdr);
        post.Parameters["PostParams"].SetValue(new Vector4(Settings.Exposure, Settings.Tonemap ? 1 : 0, 0, 0));
        post.CurrentTechnique.Passes[0].Apply();
        _gd.SamplerStates[0] = SamplerState.PointClamp;
        _gd.DrawUserPrimitives(PrimitiveType.TriangleList, FullScreen.Triangle, 0, 1);
        Stats.RecordDraw(1, 1);
        _gd.Textures[0] = null;
        _gd.DepthStencilState = DepthStencilState.Default;
    }

    public void Dispose() { _hdr?.Dispose(); _shadow?.Dispose(); }
}
