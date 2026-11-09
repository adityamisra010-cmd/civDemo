using System.Diagnostics;
using Gfx.Engine.Assets;
using Gfx.Engine.Cameras;
using Gfx.Engine.Diagnostics;
using Gfx.Engine.Effects;
using Gfx.Engine.Lighting;
using Gfx.Engine.Scene;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;

namespace Gfx.Engine.Rendering;

/// <summary>
/// Two frame paths. <b>G1</b> (all <see cref="RenderFeatures"/> switches at G1 values): [shadow depth pass] → HDR scene
/// target (sky, then opaque content) → post (exposure, ACES fit, gamma), exactly as G1 rendered.
/// <b>G2</b> (stage render1): analytic atmosphere (sky table, physically reddened sun, sky ambient) → image-based
/// lighting (sky or HDRI) → cascaded shadow atlas → HDR scene (analytic sky with sun disc, then opaque content with
/// PBR shading, aerial perspective and height fog) → bloom chain → metering and temporal adaptation → composite
/// (exposure, tonemapper, 3D LUT) → FXAA; stills can average N jittered frames instead of FXAA
/// (<see cref="RenderStill"/>). HDR target is HalfVector4 when the device accepts it, else Color (then highlights
/// clip before tonemapping — <see cref="HdrFormat"/> reports which). PRESENTATION ONLY.
/// </summary>
public sealed class ForwardRenderer : IDisposable
{
    readonly GraphicsDevice _gd;
    readonly EffectLibrary _fx;
    RenderTarget2D? _hdr;
    ShadowMap? _shadow;
    // G2
    CascadedShadowMap? _csm;
    SkyAtmosphere? _sky;
    EnvironmentLighting? _env;
    RenderTarget2D[] _bloom = Array.Empty<RenderTarget2D>();
    RenderTarget2D? _ldr, _accum;
    readonly RenderTarget2D?[] _exposure = new RenderTarget2D?[2];
    int _expIdx; bool _hasExposure;
    Texture2D? _lutTex; bool _lutIsHouse;
    float _lastTime = float.NaN;
    SamplerState? _aniso; int _anisoLevel;
    bool _warnedHdri;

    static readonly BlendState AddBlend = new()
    {
        ColorSourceBlend = Blend.One, ColorDestinationBlend = Blend.One, AlphaSourceBlend = Blend.One, AlphaDestinationBlend = Blend.One,
    };

    public RenderContext Context { get; }
    public FrameStats Stats { get; }
    public RenderSettings Settings { get; } = new();
    public SurfaceFormat HdrFormat { get; private set; } = SurfaceFormat.HalfVector4;
    public SkyAtmosphere? Sky => _sky;
    public EnvironmentLighting? Environment => _env;
    public CascadedShadowMap? Cascades => _csm;
    /// <summary>When true, the next G2 frame meters exposure without temporal smoothing (set for each capture).</summary>
    public bool ResetAdaptation { get; set; } = true;
    /// <summary>Per-pass wall time with a GPU sync after each pass (only when <see cref="ProfilePasses"/>). llvmpipe here.</summary>
    public SortedDictionary<string, double> PassMs { get; } = new(StringComparer.Ordinal);
    public bool ProfilePasses { get; set; }
    /// <summary>Far-plane distance (m) used by G2 frames with the far field on (the far field ends at 30 km).</summary>
    public const float FarFieldReach = 40_000f;
    /// <summary>G2 multiplier on the scene's G1 height-fog density (the aerial perspective adds the distance haze).</summary>
    public const float HeightFogScale = 0.35f;
    /// <summary>Exposure multiplier of the last G2 frame when auto exposure is off (diagnostics).</summary>
    public float LastManualExposure { get; private set; }

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
        if (Settings.Features.IsG1Frame) { RenderG1(content, camera, sun, output, timeSeconds); return; }
        RenderG2(content, camera, sun, output, timeSeconds, Vector2.Zero, -1f, reuseShadows: false, meter: true);
    }

    /// <summary>
    /// A deliverable still: with <see cref="RenderFeatures.StillSamples"/> &gt; 1 (G2), averages that many frames with a
    /// deterministic Halton(2,3) sub-pixel jitter (exposure metered once, shadows rendered once), in linear display
    /// space. Otherwise identical to <see cref="Render"/>.
    /// </summary>
    public void RenderStill(IRenderContent content, CameraPose camera, SunState sun, RenderTarget2D output, float timeSeconds = 0)
    {
        ResetAdaptation = true;
        int n = Settings.Features.StillSamples;
        if (Settings.Features.IsG1Frame || n <= 1) { Render(content, camera, sun, output, timeSeconds); return; }
        int w = output.Width, h = output.Height;
        if (_accum == null || _accum.Width != w || _accum.Height != h)
        {
            _accum?.Dispose();
            // PreserveContents: MonoGame clears a DiscardContents target every time it is bound (XNA emulation)
            _accum = new RenderTarget2D(_gd, w, h, false, SurfaceFormat.HalfVector4, DepthFormat.None, 0, RenderTargetUsage.PreserveContents);
        }
        _gd.SetRenderTarget(_accum);
        _gd.Clear(Color.Transparent);
        for (int i = 0; i < n; i++)
        {
            var j = Jitter(i);
            RenderG2(content, camera, sun, output, timeSeconds, j, 1f / n, reuseShadows: i > 0, meter: i == 0);
        }
        var copy = _fx.Get(G2EffectNames.Copy);
        _gd.SetRenderTarget(output);
        _gd.BlendState = BlendState.Opaque; _gd.DepthStencilState = DepthStencilState.None;
        copy.Parameters["Src"].SetValue(_accum);
        copy.Parameters["CopyParams"].SetValue(new Vector4(1, 1, 0, 0));
        copy.CurrentTechnique.Passes[0].Apply();
        _gd.SamplerStates[0] = SamplerState.PointClamp;
        _gd.DrawUserPrimitives(PrimitiveType.TriangleList, FullScreen.Triangle, 0, 1);
        Stats.RecordDraw(1, 1);
        _gd.Textures[0] = null;
        _gd.DepthStencilState = DepthStencilState.Default;
    }

    /// <summary>Deterministic sub-pixel jitter of sample i: Halton bases 2 and 3, centred (pixels in [−0.5, 0.5)).</summary>
    public static Vector2 Jitter(int i) => new(Halton(i + 1, 2) - 0.5f, Halton(i + 1, 3) - 0.5f);

    public static float Halton(int index, int b)
    {
        float f = 1, r = 0;
        while (index > 0) { f /= b; r += f * (index % b); index /= b; }
        return r;
    }

    // ------------------------------------------------------------------------------------------------ G1 frame
    void RenderG1(IRenderContent content, CameraPose camera, SunState sun, RenderTarget2D? output, float timeSeconds)
    {
        int w = output?.Width ?? _gd.PresentationParameters.BackBufferWidth;
        int h = output?.Height ?? _gd.PresentationParameters.BackBufferHeight;
        EnsureTargets(w, h);
        var rc = Context;
        rc.Features = Settings.Features; rc.IsG2Frame = false; rc.LightModel = Vector4.Zero; rc.ShadowCascade = -1; rc.ShadowFrustum = null;
        rc.MaterialSampler = SamplerState.LinearWrap;
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

    // ------------------------------------------------------------------------------------------------ G2 frame
    readonly Stopwatch _passSw = new();
    void PassBegin() { if (ProfilePasses) { _passSw.Restart(); } }
    void PassEnd(string name, RenderTarget2D? syncTarget)
    {
        if (!ProfilePasses) return;
        if (syncTarget != null)
        {
            var one = new Color[1];
            if (syncTarget.Format == SurfaceFormat.Color) syncTarget.GetData(0, new Rectangle(0, 0, 1, 1), one, 0, 1);
            else
            {
                var hv = new Microsoft.Xna.Framework.Graphics.PackedVector.HalfVector4[1];
                if (syncTarget.Format == SurfaceFormat.HalfVector4) syncTarget.GetData(0, new Rectangle(0, 0, 1, 1), hv, 0, 1);
                else { var f = new float[1]; syncTarget.GetData(0, new Rectangle(0, 0, 1, 1), f, 0, 1); }
            }
        }
        PassMs[name] = PassMs.GetValueOrDefault(name) + _passSw.Elapsed.TotalMilliseconds;
    }

    SamplerState MaterialSampler(int aniso)
    {
        if (aniso <= 1) return SamplerState.LinearWrap;
        if (_aniso == null || _anisoLevel != aniso)
        {
            _aniso = new SamplerState { Filter = TextureFilter.Anisotropic, MaxAnisotropy = aniso, AddressU = TextureAddressMode.Wrap, AddressV = TextureAddressMode.Wrap };
            _anisoLevel = aniso;
        }
        return _aniso;
    }

    void RenderG2(IRenderContent content, CameraPose camera, SunState sun, RenderTarget2D? output, float timeSeconds,
        Vector2 jitterPx, float accumWeight, bool reuseShadows, bool meter)
    {
        var f = Settings.Features;
        int w = output?.Width ?? _gd.PresentationParameters.BackBufferWidth;
        int h = output?.Height ?? _gd.PresentationParameters.BackBufferHeight;
        EnsureTargets(w, h);
        var rc = Context;
        rc.Features = f; rc.IsG2Frame = true; rc.ShadowCascade = -1; rc.ShadowFrustum = null;
        rc.MaterialSampler = MaterialSampler(f.Anisotropy);
        float aspect = w / (float)h;
        rc.Camera = camera; rc.TimeSeconds = timeSeconds; rc.FogParams = Settings.FogParams;
        rc.View = camera.View;
        // the far field reaches 30 km: G1 scenes set 3-4 km far planes, which would cut it off mid-landscape
        var viewCam = f.FarField && camera.Far < FarFieldReach ? camera with { Far = FarFieldReach } : camera;
        var proj = viewCam.Projection(aspect);
        rc.Frustum = new Frustum(rc.View * proj);
        if (jitterPx != Vector2.Zero) proj *= Matrix.CreateTranslation(jitterPx.X * 2f / w, jitterPx.Y * 2f / h, 0);
        rc.Projection = proj; rc.ViewProj = rc.View * proj;
        for (int i = 0; i < 16; i++) _gd.Textures[i] = null;

        // 1. atmosphere and image-based lighting
        PassBegin();
        bool analytic = f.Sky == SkyMode.Analytic;
        var s2 = sun;
        if (analytic)
        {
            _sky ??= new SkyAtmosphere(_gd);
            _sky.Update(sun, f.Haze);
            s2 = _sky.Derive(sun);
        }
        rc.Sun = s2;
        _env ??= new EnvironmentLighting(_gd);
        bool ibl = analytic && f.Ibl != IblSource.None;
        if (ibl)
        {
            string? hdri = f.Ibl == IblSource.Hdri ? (f.HdriPath ?? AssetRoot.Raw(AssetRoot.DefaultHdri)) : null;
            if (hdri != null && File.Exists(hdri)) _env.UpdateFromHdri(hdri, _sky!, f.HdriRotationDeg);
            else
            {
                if (hdri != null && !_warnedHdri) { Console.Error.WriteLine($"[render] HDRI not found: {hdri}; using the analytic sky for IBL"); _warnedHdri = true; }
                _env.UpdateFromSky(_sky!);
            }
            rc.IblSpecular = _env.Specular;
            for (int i = 0; i < 9; i++) rc.Sh[i] = _env.Sh[i];
        }
        else rc.IblSpecular = null;
        rc.BrdfLut = _env.Brdf;
        rc.IblParams = new Vector4(_env.MaxLod, 1, 1, 0);
        rc.SkyView = analytic ? _sky!.SkyView : null;
        rc.SkySun = analytic ? new Vector4(_sky!.TableSunDir, 1) : Vector4.Zero;
        bool aerial = analytic && f.AerialPerspective;
        // w: G1's height-fog density is scaled down; the physically based aerial perspective carries the long range
        rc.Aerial = analytic ? new Vector4(_sky!.AerialExtinction, HeightFogScale) : Vector4.Zero;
        if (analytic) (rc.AerialR, rc.AerialM, rc.AerialMs, rc.FogSun, rc.FogAmb) = _sky!.AerialTerms(s2);
        bool csm = Settings.Shadows && f.ShadowTechnique == ShadowTechnique.Cascaded;
        rc.LightModel = new Vector4(f.Pbr ? 1 : 0, ibl ? 1 : 0, aerial ? 1 : 0, csm ? 1 : 0);
        // texture uploads above re-bind GL units behind MonoGame's binding cache: make every slot re-bind
        for (int i = 0; i < 16; i++) _gd.Textures[i] = null;
        PassEnd("atmosphere+ibl", null);

        // 2. shadows
        PassBegin();
        if (Settings.Shadows && csm)
        {
            int tile = f.CascadeSize;
            if (_csm == null || _csm.TileSize != tile) { _csm?.Dispose(); _csm = new CascadedShadowMap(_gd, tile); }
            if (!reuseShadows)
            {
                _csm.Fit(camera, aspect, s2.DirectionToLight, f.Cascades, f.ShadowDistance, Settings.ShadowCasterBounds);
                _gd.SetRenderTarget(_csm.Atlas);
                _gd.Clear(ClearOptions.Target | ClearOptions.DepthBuffer, Color.White, 1f, 0);
                _gd.DepthStencilState = DepthStencilState.Default; _gd.RasterizerState = RasterizerState.CullNone; _gd.BlendState = BlendState.Opaque;
                rc.IsShadowPass = true; rc.ShadowTexture = null;
                for (int k = 0; k < _csm.Count; k++)
                {
                    rc.LightViewProj = _csm.ViewProj[k]; rc.ShadowCascade = k; rc.ShadowFrustum = new Frustum(_csm.ViewProj[k]);
                    _gd.Viewport = _csm.TileViewport(k);
                    content.DrawShadowCasters(rc);
                }
                rc.IsShadowPass = false; rc.ShadowCascade = -1; rc.ShadowFrustum = null;
            }
            rc.LightViewProj = _csm.ViewProj[0];
            rc.ShadowParams = new Vector4(1f / (_csm.TileSize * 2), 0, 1, 1);
            rc.ShadowNormalOffset = _csm.NormalOffsetWorld;
            rc.CsmAtlas = _csm.AtlasParams;
            for (int k = 0; k < 4; k++) { rc.CsmScale[k] = _csm.Scale[k]; rc.CsmOffset[k] = _csm.Offset[k]; }
            rc.ShadowTexture = _csm.Atlas;
            PassEnd("shadows", reuseShadows ? null : _csm.Atlas);
        }
        else if (Settings.Shadows)
        {
            if (_shadow == null || _shadow.Size != Settings.ShadowMapSize) { _shadow?.Dispose(); _shadow = new ShadowMap(_gd, Settings.ShadowMapSize); }
            if (!reuseShadows)
            {
                _shadow.Fit(s2.DirectionToLight, Settings.ShadowFocus ?? camera.Target, Settings.ShadowRadius);
                rc.LightViewProj = _shadow.LightViewProj; rc.ShadowTexture = null;
                _gd.SetRenderTarget(_shadow.Target);
                _gd.Clear(ClearOptions.Target | ClearOptions.DepthBuffer, Color.White, 1f, 0);
                _gd.DepthStencilState = DepthStencilState.Default; _gd.RasterizerState = RasterizerState.CullNone; _gd.BlendState = BlendState.Opaque;
                rc.IsShadowPass = true;
                content.DrawShadowCasters(rc);
                rc.IsShadowPass = false;
            }
            rc.LightViewProj = _shadow.LightViewProj; rc.ShadowParams = _shadow.Params(true);
            rc.ShadowNormalOffset = _shadow.NormalOffsetWorld; rc.ShadowTexture = _shadow.Target;
            PassEnd("shadows", reuseShadows ? null : _shadow.Target);
        }
        else { rc.LightViewProj = Matrix.Identity; rc.ShadowParams = Vector4.Zero; rc.ShadowTexture = null; }

        // 3. HDR scene: sky, then opaque content
        PassBegin();
        _gd.SetRenderTarget(_hdr);
        _gd.Clear(ClearOptions.Target | ClearOptions.DepthBuffer | ClearOptions.Stencil, Color.Black, 1f, 0);
        _gd.BlendState = BlendState.Opaque; _gd.DepthStencilState = DepthStencilState.None; _gd.RasterizerState = RasterizerState.CullNone;
        if (analytic)
        {
            var sky = _fx.Get(G2EffectNames.SkyAnalytic);
            sky.Parameters["InvViewProj"].SetValue(Matrix.Invert(rc.ViewProj));
            const float sunRadius = 0.0047f * 1.3f;
            float cosR = MathF.Cos(sunRadius);
            var disc = s2.LightColor / (MathF.PI * sunRadius * sunRadius);
            float peak = MathF.Max(disc.X, MathF.Max(disc.Y, disc.Z));
            if (peak > 4000f) disc *= 4000f / peak;
            sky.Parameters["SunDisc"].SetValue(new Vector4(cosR, 1 - cosR, 0, 0));
            sky.Parameters["SunDiscColor"].SetValue(new Vector4(disc, 1));
            bool bg = f.ShowHdriBackground && _env.Background != null && f.Ibl == IblSource.Hdri;
            sky.Parameters["SkyMode"].SetValue(new Vector4(bg ? 1 : 0, 0, 0, 0));
            sky.Parameters["Backdrop"]?.SetValue(bg ? _env.Background : null);
            rc.Apply(sky);
            _gd.SamplerStates[GlslLib.FirstMaterialSlot] = SamplerState.LinearWrap;
        }
        else
        {
            var sky = _fx.Get(EffectNames.Sky);
            sky.Parameters["InvViewProj"].SetValue(Matrix.Invert(rc.ViewProj));
            sky.Parameters["SkyZenith"].SetValue(new Vector4(s2.SkyZenith, 1));
            sky.Parameters["SkyHorizon"].SetValue(new Vector4(s2.SkyHorizon, 1));
            rc.Apply(sky);
        }
        _gd.DrawUserPrimitives(PrimitiveType.TriangleList, FullScreen.Triangle, 0, 1);
        Stats.RecordDraw(1, 1);
        _gd.DepthStencilState = DepthStencilState.Default;
        content.DrawOpaque(rc);
        PassEnd("scene", _hdr);

        // 4. post
        Post(output, w, h, accumWeight, meter, timeSeconds, f, analytic);
    }

    void EnsurePostTargets(int w, int h, bool fxaa)
    {
        int levels = 0; int bw = w, bh = h;
        while (levels < 6 && bw >= 16 && bh >= 16) { bw /= 2; bh /= 2; levels++; }
        if (_bloom.Length != levels || (levels > 0 && (_bloom[0].Width != w / 2 || _bloom[0].Height != h / 2)))
        {
            foreach (var b in _bloom) b.Dispose();
            _bloom = new RenderTarget2D[levels];
            bw = w; bh = h;
            // PreserveContents: the up-chain re-binds each level and adds onto its downsampled content
            for (int i = 0; i < levels; i++) { bw = Math.Max(1, bw / 2); bh = Math.Max(1, bh / 2); _bloom[i] = new RenderTarget2D(_gd, bw, bh, false, SurfaceFormat.HalfVector4, DepthFormat.None, 0, RenderTargetUsage.PreserveContents); }
        }
        for (int i = 0; i < 2; i++) _exposure[i] ??= new RenderTarget2D(_gd, 1, 1, false, SurfaceFormat.HalfVector4, DepthFormat.None);
        if (fxaa && (_ldr == null || _ldr.Width != w || _ldr.Height != h))
        {
            _ldr?.Dispose();
            _ldr = new RenderTarget2D(_gd, w, h, false, SurfaceFormat.Color, DepthFormat.None);
        }
    }

    void FullScreenDraw(Effect e)
    {
        e.CurrentTechnique.Passes[0].Apply();
        _gd.DrawUserPrimitives(PrimitiveType.TriangleList, FullScreen.Triangle, 0, 1);
        Stats.RecordDraw(1, 1);
    }

    void Post(RenderTarget2D? output, int w, int h, float accumWeight, bool meter, float time, RenderFeatures f, bool analytic)
    {
        bool accumulate = accumWeight > 0;
        bool fxaa = f.Aa == AntiAliasing.Fxaa && !accumulate;
        EnsurePostTargets(w, h, fxaa);
        _gd.DepthStencilState = DepthStencilState.None; _gd.RasterizerState = RasterizerState.CullNone;
        for (int i = 0; i < 16; i++) _gd.Textures[i] = null;
        bool needChain = (f.Bloom || f.AutoExposure) && _bloom.Length > 0;

        // bloom down chain (also feeds the meter)
        PassBegin();
        if (needChain)
        {
            var down = _fx.Get(G2EffectNames.BloomDown);
            Texture2D src = _hdr!;
            for (int i = 0; i < _bloom.Length; i++)
            {
                _gd.SetRenderTarget(_bloom[i]); _gd.BlendState = BlendState.Opaque;
                down.Parameters["Src"].SetValue(src);
                down.Parameters["SrcTexel"].SetValue(new Vector4(1f / src.Width, 1f / src.Height, 0, 0));
                down.Parameters["BloomParams"].SetValue(new Vector4(i == 0 ? 1 : 0, 0, 0, 0));
                down.CurrentTechnique.Passes[0].Apply();
                _gd.SamplerStates[0] = SamplerState.LinearClamp;
                _gd.DrawUserPrimitives(PrimitiveType.TriangleList, FullScreen.Triangle, 0, 1);
                Stats.RecordDraw(1, 1);
                src = _bloom[i];
            }
        }
        PassEnd("bloom-down", needChain ? _bloom[^1] : null);

        // metering + adaptation
        PassBegin();
        if (f.AutoExposure && needChain && meter)
        {
            float dt = float.IsNaN(_lastTime) ? 0 : Math.Clamp(time - _lastTime, 0f, 0.5f);
            _lastTime = time;
            bool history = _hasExposure && !ResetAdaptation;
            var e = _fx.Get(G2EffectNames.Exposure);
            int src = Math.Min(3, _bloom.Length - 1);
            var prev = _exposure[_expIdx]!; var dst = _exposure[1 - _expIdx]!;
            _gd.SetRenderTarget(dst); _gd.BlendState = BlendState.Opaque;
            e.Parameters["Src"].SetValue(_bloom[src]);
            e.Parameters["Prev"].SetValue(history ? prev : null);
            e.Parameters["ExposureParams"].SetValue(new Vector4(1 - MathF.Exp(-dt * 1.2f), 1 - MathF.Exp(-dt * 2.5f), 0.8f, MathF.Log2(0.12f)));
            e.Parameters["ExposureRange"].SetValue(new Vector4(-12f, 6f, history ? 1 : 0, 0));
            e.CurrentTechnique.Passes[0].Apply();
            _gd.SamplerStates[0] = SamplerState.LinearClamp; _gd.SamplerStates[1] = SamplerState.PointClamp;
            _gd.DrawUserPrimitives(PrimitiveType.TriangleList, FullScreen.Triangle, 0, 1);
            Stats.RecordDraw(1, 1);
            _expIdx = 1 - _expIdx; _hasExposure = true; ResetAdaptation = false;
            _gd.Textures[1] = null;
        }
        PassEnd("exposure", f.AutoExposure && meter ? _exposure[_expIdx] : null);

        // bloom up chain
        PassBegin();
        if (f.Bloom && _bloom.Length > 0)
        {
            var up = _fx.Get(G2EffectNames.BloomUp);
            for (int i = _bloom.Length - 1; i >= 1; i--)
            {
                _gd.SetRenderTarget(_bloom[i - 1]); _gd.BlendState = AddBlend;
                up.Parameters["Src"].SetValue(_bloom[i]);
                up.Parameters["SrcTexel"].SetValue(new Vector4(1f / _bloom[i].Width, 1f / _bloom[i].Height, 0, 0));
                up.Parameters["BloomParams"].SetValue(new Vector4(0, 1, 0, 0));
                up.CurrentTechnique.Passes[0].Apply();
                _gd.SamplerStates[0] = SamplerState.LinearClamp;
                _gd.DrawUserPrimitives(PrimitiveType.TriangleList, FullScreen.Triangle, 0, 1);
                Stats.RecordDraw(1, 1);
            }
            _gd.BlendState = BlendState.Opaque;
        }
        PassEnd("bloom-up", f.Bloom && _bloom.Length > 0 ? _bloom[0] : null);

        // composite: exposure, bloom, tonemap, grade
        PassBegin();
        var comp = _fx.Get(G2EffectNames.Composite);
        RenderTarget2D? target = accumulate ? _accum : (fxaa ? _ldr : output);
        _gd.SetRenderTarget(target);
        _gd.BlendState = accumulate ? AddBlend : BlendState.Opaque;
        if (f.ColorGrade && (_lutTex == null || !_lutIsHouse)) { _lutTex?.Dispose(); _lutTex = ColorGradeLut.House().CreateTexture(_gd); _lutIsHouse = true; }
        float manual = Settings.Exposure * (f.Pbr ? 2.35f : 1f);
        LastManualExposure = manual;
        float comp2 = f.AutoExposure ? Settings.Exposure / 0.85f : 1f;
        bool bloom = f.Bloom && _bloom.Length > 0;
        comp.Parameters["SceneTex"].SetValue(_hdr);
        comp.Parameters["BloomTex"].SetValue(bloom ? _bloom[0] : null);
        comp.Parameters["ExposureTex"].SetValue(f.AutoExposure && _hasExposure ? _exposure[_expIdx] : null);
        comp.Parameters["LutTex"].SetValue(f.ColorGrade ? _lutTex : null);
        comp.Parameters["CompositeParams"].SetValue(new Vector4(manual, f.AutoExposure && _hasExposure ? 1 : 0, bloom ? f.BloomStrength : 0, ToneMapperId(f.ToneMapper)));
        comp.Parameters["CompositeParams2"].SetValue(new Vector4(f.ColorGrade ? 1 : 0, 32, accumulate ? 1 : 0, comp2 * MathF.Pow(2f, f.ExposureBiasEv)));
        // y: the up-chain leaves the sum of one blurred copy per level in bloom[0]; the composite averages them
        comp.Parameters["AccumWeight"].SetValue(new Vector4(accumulate ? accumWeight : 1, bloom ? 1f / _bloom.Length : 1, 0, 0));
        comp.CurrentTechnique.Passes[0].Apply();
        _gd.SamplerStates[0] = SamplerState.PointClamp; _gd.SamplerStates[1] = SamplerState.LinearClamp;
        _gd.SamplerStates[2] = SamplerState.PointClamp; _gd.SamplerStates[3] = SamplerState.LinearClamp;
        _gd.DrawUserPrimitives(PrimitiveType.TriangleList, FullScreen.Triangle, 0, 1);
        Stats.RecordDraw(1, 1);
        _gd.BlendState = BlendState.Opaque;
        for (int i = 0; i < 4; i++) _gd.Textures[i] = null;
        PassEnd("composite", target);

        // FXAA
        if (fxaa)
        {
            PassBegin();
            var fx = _fx.Get(G2EffectNames.Fxaa);
            _gd.SetRenderTarget(output);
            fx.Parameters["Src"].SetValue(_ldr);
            fx.Parameters["FxaaParams"].SetValue(new Vector4(1f / w, 1f / h, 0.75f, 0.125f));
            fx.CurrentTechnique.Passes[0].Apply();
            _gd.SamplerStates[0] = SamplerState.LinearClamp;
            _gd.DrawUserPrimitives(PrimitiveType.TriangleList, FullScreen.Triangle, 0, 1);
            Stats.RecordDraw(1, 1);
            _gd.Textures[0] = null;
            PassEnd("fxaa", output);
        }
        _gd.DepthStencilState = DepthStencilState.Default;
    }

    static int ToneMapperId(ToneMapper t) => t switch
    {
        ToneMapper.AcesFit => 0, ToneMapper.PbrNeutral => 1, ToneMapper.AcesHill => 2, ToneMapper.AgX => 3, _ => 4,
    };

    public void Dispose()
    {
        _hdr?.Dispose(); _shadow?.Dispose(); _csm?.Dispose(); _sky?.Dispose(); _env?.Dispose();
        foreach (var b in _bloom) b.Dispose();
        _ldr?.Dispose(); _accum?.Dispose(); _exposure[0]?.Dispose(); _exposure[1]?.Dispose(); _lutTex?.Dispose();
    }
}
