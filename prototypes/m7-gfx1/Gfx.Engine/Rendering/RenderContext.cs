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
    /// <summary>Draw shadow casters with DEPTH effects (e.g. <c>InstancedDepth</c>). <c>rc.IsShadowPass</c> is true.</summary>
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

    /// <summary>Bind a bone texture for subsequent <c>SkinnedLit</c>/<c>SkinnedDepth</c> draws.</summary>
    public void BindBones(BoneTexture bones) { _bones = bones.Texture; _boneSize = bones.SizeParam; }

    /// <summary>
    /// Sets every standard parameter the effect declares (unknown names are skipped), applies pass 0 and
    /// sets sampler states: slots 0–1 point-clamp (shadow depth, bone texture), 2–7 linear-wrap (materials).
    /// Set your own effect-specific parameters before calling.
    /// </summary>
    public void Apply(Effect e)
    {
        var p = e.Parameters;
        p["ViewProj"]?.SetValue(IsShadowPass ? LightViewProj : ViewProj);
        p["LightViewProj"]?.SetValue(LightViewProj);
        p["CameraPos"]?.SetValue(new Vector4(Camera.Position, 1));
        p["SunDir"]?.SetValue(new Vector4(Sun.DirectionToLight, 0));
        p["SunColor"]?.SetValue(new Vector4(Sun.LightColor, 1));
        p["AmbientSky"]?.SetValue(new Vector4(Sun.AmbientSky, 1));
        p["AmbientGround"]?.SetValue(new Vector4(Sun.AmbientGround, 1));
        p["FogColor"]?.SetValue(new Vector4(Sun.FogColor, 1));
        p["FogParams"]?.SetValue(FogParams);
        p["ShadowParams"]?.SetValue(ShadowParams);
        p["ShadowOffset"]?.SetValue(new Vector4(ShadowNormalOffset, 0, 0, 0));
        if (p["ShadowMap"] is { } sm) sm.SetValue(IsShadowPass ? null : ShadowTexture);
        if (p["Bones"] is { } bt) { bt.SetValue(_bones); p["BoneTexSize"]?.SetValue(_boneSize); }
        e.CurrentTechnique.Passes[0].Apply();
        var ss = Device.SamplerStates;
        ss[0] = SamplerState.PointClamp; ss[1] = SamplerState.PointClamp;
        for (int i = GlslLib.FirstMaterialSlot; i < 8; i++) ss[i] = SamplerState.LinearWrap;
    }
}
