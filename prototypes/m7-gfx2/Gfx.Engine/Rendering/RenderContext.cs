using System.Runtime.CompilerServices;
using Gfx.Engine.Cameras;
using Gfx.Engine.Diagnostics;
using Gfx.Engine.Effects;
using Gfx.Engine.Scene;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;

namespace Gfx.Engine.Rendering;

/// <summary>What a scene draws. The renderer calls the shadow pass first (when enabled), then the opaque pass.</summary>
public interface IRenderContent
{
    /// <summary>
    /// Draw shadow casters with DEPTH effects (e.g. <c>InstancedDepth</c>). <c>rc.IsShadowPass</c> is true. With
    /// cascaded shadows this is called once per cascade (<c>rc.ShadowCascade</c>, culling frustum
    /// <c>rc.ShadowFrustum</c>); <c>rc.Frustum</c> stays the camera's.
    /// </summary>
    void DrawShadowCasters(RenderContext rc);
    /// <summary>Draw lit geometry into the HDR scene target (sky already drawn).</summary>
    void DrawOpaque(RenderContext rc);
}

/// <summary>Per-frame state passed to content. <see cref="Apply"/> fills every standard parameter an effect declares.</summary>
public sealed class RenderContext
{
    public required GraphicsDevice Device { get; init; }
    public required EffectLibrary Effects { get; init; }
    public required FrameStats Stats { get; init; }
    public CameraPose Camera { get; internal set; }
    public Matrix View { get; internal set; }
    public Matrix Projection { get; internal set; }
    public Matrix ViewProj { get; internal set; }
    public Frustum Frustum { get; internal set; }
    public SunState Sun { get; internal set; }
    public Matrix LightViewProj { get; internal set; } = Matrix.Identity;
    public Vector4 ShadowParams { get; internal set; }
    /// <summary>World-space normal offset (m) applied to the shadow lookup position.</summary>
    public float ShadowNormalOffset { get; internal set; }
    public Texture2D? ShadowTexture { get; internal set; }
    public bool IsShadowPass { get; internal set; }
    /// <summary>x density /m, y height falloff /m, z base height m, w max opacity.</summary>
    public Vector4 FogParams { get; set; } = new(0.0015f, 0.02f, 0f, 0.85f);
    public float TimeSeconds { get; internal set; }
    Texture2D? _bones; Vector4 _boneSize;

    // ---------------------------------------------------------------- G2 (stage render1)
    /// <summary>The renderer's feature switches for this frame (read-only for content).</summary>
    public RenderFeatures Features { get; internal set; } = RenderFeatures.G1();
    /// <summary>True when the frame runs the G2 path (any G2 feature on).</summary>
    public bool IsG2Frame { get; internal set; }
    /// <summary>During a cascaded shadow pass: the cascade index and its light frustum (for caster culling).</summary>
    public int ShadowCascade { get; internal set; } = -1;
    public Frustum? ShadowFrustum { get; internal set; }
    /// <summary>x PBR, y IBL, z aerial perspective, w cascaded shadows (all 0 in the G1 frame).</summary>
    public Vector4 LightModel { get; internal set; }
    public Vector4 IblParams { get; internal set; }
    public Vector4 SkySun { get; internal set; }
    public Vector4 Aerial { get; internal set; }
    public Vector4 CsmAtlas { get; internal set; }
    /// <summary>Aerial-perspective source terms (Rayleigh, Mie, multiple scattering) and height-fog lighting.</summary>
    public Vector4 AerialR { get; internal set; }
    public Vector4 AerialM { get; internal set; }
    public Vector4 AerialMs { get; internal set; }
    public Vector4 FogSun { get; internal set; }
    public Vector4 FogAmb { get; internal set; }
    public Vector4[] Sh { get; } = new Vector4[9];
    public Vector4[] CsmScale { get; } = new Vector4[4];
    public Vector4[] CsmOffset { get; } = new Vector4[4];
    public Texture2D? IblSpecular { get; internal set; }
    public Texture2D? BrdfLut { get; internal set; }
    public Texture2D? SkyView { get; internal set; }
    /// <summary>Sampler for material slots 2–12: anisotropic (G2) or bilinear wrap (G1).</summary>
    public SamplerState MaterialSampler { get; internal set; } = SamplerState.LinearWrap;

    static readonly SamplerState IblSampler = new()
    {
        Filter = TextureFilter.Linear, AddressU = TextureAddressMode.Wrap, AddressV = TextureAddressMode.Clamp, AddressW = TextureAddressMode.Clamp,
    };

    /// <summary>Bind a bone texture for subsequent <c>SkinnedLit</c>/<c>SkinnedDepth</c> draws.</summary>
    public void BindBones(BoneTexture bones) { _bones = bones.Texture; _boneSize = bones.SizeParam; }

    sealed class StdParams
    {
        public EffectParameter? ViewProj, LightViewProj, CameraPos, SunDir, SunColor, AmbientSky, AmbientGround, FogColor, FogParams,
            ShadowParams, ShadowOffset, ShadowMap, Bones, BoneTexSize, LightModel, IblParams, SkySun, Aerial, CsmAtlas, IblSpec, BrdfLut, SkyView,
            AerialR, AerialM, AerialMs, FogSun, FogAmb;
        public EffectParameter?[] Sh = new EffectParameter?[9], CsmScale = new EffectParameter?[4], CsmOffset = new EffectParameter?[4];
        public bool HasG2;
    }

    // MonoGame's parameter lookup by name is a linear scan; cache the standard parameters per effect.
    static readonly ConditionalWeakTable<Effect, StdParams> Cache = new();

    static StdParams Lookup(Effect e) => Cache.GetValue(e, ef =>
    {
        var p = ef.Parameters;
        var s = new StdParams
        {
            ViewProj = p["ViewProj"], LightViewProj = p["LightViewProj"], CameraPos = p["CameraPos"], SunDir = p["SunDir"], SunColor = p["SunColor"],
            AmbientSky = p["AmbientSky"], AmbientGround = p["AmbientGround"], FogColor = p["FogColor"], FogParams = p["FogParams"],
            ShadowParams = p["ShadowParams"], ShadowOffset = p["ShadowOffset"], ShadowMap = p["ShadowMap"], Bones = p["Bones"], BoneTexSize = p["BoneTexSize"],
            LightModel = p["LightModel"], IblParams = p["IblParams"], SkySun = p["SkySun"], Aerial = p["Aerial"], CsmAtlas = p["CsmAtlas"],
            IblSpec = p["IblSpec"], BrdfLut = p["BrdfLut"], SkyView = p["SkyView"],
            AerialR = p["AerialR"], AerialM = p["AerialM"], AerialMs = p["AerialMs"], FogSun = p["FogSun"], FogAmb = p["FogAmb"],
        };
        for (int i = 0; i < 9; i++) s.Sh[i] = p["Sh" + i];
        for (int i = 0; i < 4; i++) { s.CsmScale[i] = p["CsmScale" + i]; s.CsmOffset[i] = p["CsmOffset" + i]; }
        s.HasG2 = s.LightModel != null || s.SkySun != null;
        return s;
    });

    /// <summary>
    /// Sets every standard parameter the effect declares (unknown names are skipped), applies pass 0 and
    /// sets sampler states: slots 0–1 point-clamp (shadow depth, bone texture), 2–12 the material sampler
    /// (G1 frame: linear-wrap on 2–7 only, as in G1), 13 IBL (trilinear, wrap/clamp), 14–15 linear-clamp.
    /// Set your own effect-specific parameters before calling.
    /// </summary>
    public void Apply(Effect e)
    {
        var p = Lookup(e);
        p.ViewProj?.SetValue(IsShadowPass ? LightViewProj : ViewProj);
        p.LightViewProj?.SetValue(LightViewProj);
        p.CameraPos?.SetValue(new Vector4(Camera.Position, 1));
        p.SunDir?.SetValue(new Vector4(Sun.DirectionToLight, 0));
        p.SunColor?.SetValue(new Vector4(Sun.LightColor, 1));
        p.AmbientSky?.SetValue(new Vector4(Sun.AmbientSky, 1));
        p.AmbientGround?.SetValue(new Vector4(Sun.AmbientGround, 1));
        p.FogColor?.SetValue(new Vector4(Sun.FogColor, 1));
        p.FogParams?.SetValue(FogParams);
        p.ShadowParams?.SetValue(ShadowParams);
        p.ShadowOffset?.SetValue(new Vector4(ShadowNormalOffset, 0, 0, 0));
        p.ShadowMap?.SetValue(IsShadowPass ? null : ShadowTexture);
        if (p.Bones != null) { p.Bones.SetValue(_bones); p.BoneTexSize?.SetValue(_boneSize); }
        if (p.HasG2)
        {
            p.LightModel?.SetValue(LightModel);
            p.IblParams?.SetValue(IblParams);
            p.SkySun?.SetValue(SkySun);
            p.Aerial?.SetValue(Aerial);
            p.CsmAtlas?.SetValue(CsmAtlas);
            p.AerialR?.SetValue(AerialR); p.AerialM?.SetValue(AerialM); p.AerialMs?.SetValue(AerialMs);
            p.FogSun?.SetValue(FogSun); p.FogAmb?.SetValue(FogAmb);
            for (int i = 0; i < 9; i++) p.Sh[i]?.SetValue(Sh[i]);
            for (int i = 0; i < 4; i++) { p.CsmScale[i]?.SetValue(CsmScale[i]); p.CsmOffset[i]?.SetValue(CsmOffset[i]); }
            // Force MonoGame to re-bind these slots: textures created or updated mid-frame (lazy loads) re-bind GL
            // texture units behind MonoGame's binding cache, which would leave a stale texture on the unit.
            var tx = Device.Textures;
            tx[GlslLib.IblSpecSlot] = null; tx[GlslLib.BrdfLutSlot] = null; tx[GlslLib.SkyViewSlot] = null;
            p.IblSpec?.SetValue(IsG2Frame && !IsShadowPass ? IblSpecular : null);
            p.BrdfLut?.SetValue(IsG2Frame && !IsShadowPass ? BrdfLut : null);
            p.SkyView?.SetValue(IsG2Frame && !IsShadowPass ? SkyView : null);
        }
        e.CurrentTechnique.Passes[0].Apply();
        var ss = Device.SamplerStates;
        ss[0] = SamplerState.PointClamp; ss[1] = SamplerState.PointClamp;
        if (!IsG2Frame)
        {
            for (int i = GlslLib.FirstMaterialSlot; i < 8; i++) ss[i] = SamplerState.LinearWrap;
            return;
        }
        for (int i = GlslLib.FirstMaterialSlot; i <= GlslLib.LastMaterialSlot; i++) ss[i] = MaterialSampler;
        ss[GlslLib.IblSpecSlot] = IblSampler; ss[GlslLib.BrdfLutSlot] = SamplerState.LinearClamp; ss[GlslLib.SkyViewSlot] = SamplerState.LinearClamp;
    }
}
