namespace Gfx.Engine.Mgfx;

/// <summary>
/// The G-buffer-lite written by every pixel program next to its colour (stage render2; PRESENTATION ONLY). The G2 scene
/// pass binds three targets: 0 the HDR colour, 1 an R32F target = linear view depth in metres (the clip-space
/// w of the fragment, <c>1/gl_FragCoord.w</c>; <see cref="SkyDepth"/> where only the sky was drawn), 2 an RGBA8 target
/// with r = the share of the final colour that is ambient (sky) light, g = the share that is direct (sun) light, b = 1
/// when the effect reported its ambient term, a = 0.2 × (1 + the shadow cascade used, plus the blend fraction towards
/// the next one) for the cascade debug view (0 = no cascade lookup). Screen-space ambient occlusion darkens only the ambient share and contact
/// shadows only the direct share (a forward renderer has no separate light buffers; the shares let a post pass apply
/// them to the right light after the fact).
/// <para>How an effect reports its light: the engine's lighting library calls <c>gbAmbient(ambient)</c> in
/// <c>shadePbrFull</c>, and <c>applyFog(c, wpos)</c> records the surface radiance <c>c</c> and its transmittance to the
/// camera. Effects with their own lighting call <c>gbAmbient</c> with their ambient term. An effect that never reports
/// leaves both shares at 0, so AO and contact shadows leave its pixels alone (conservative).</para>
/// <para>Target 3 (RGBA16F) is the object motion for temporal anti-aliasing: effects that move vertices on their own
/// (skinned crowds; <see cref="EffectBuilder.Velocity"/>) write xy = this frame's unjittered NDC position minus the
/// previous frame's and z = 1, from the varyings <c>gb_vcur</c> and <c>gb_vprev</c> their vertex stage sets; every other
/// effect writes z = 0 and the resolve reprojects that pixel through its depth (camera motion only).</para>
/// <para>Blended effects (<see cref="EffectBuilder.BlendedGBuffer"/>: water, additive cornea) write zeros to target 1
/// and (0, 0, 0, alpha) to target 2, so the depth under them is kept and the shares of the surface below are scaled by
/// (1 − alpha) with MonoGame's premultiplied alpha blend, and left as they are with additive blending.</para>
/// </summary>
public static class GBufferLayout
{
    /// <summary>Linear depth written by the sky (m): beyond every far plane the renderer uses.</summary>
    public const float SkyDepth = 1.0e6f;

    /// <summary>Globals and reporting functions, prepended to every pixel program.</summary>
    public const string Globals = @"
// ---- G-buffer-lite (stage render2): light shares reported by the lighting code
vec3 gb_amb; vec3 gb_surf; vec3 gb_T; float gb_rep; float gb_casc;
void gb_init() { gb_amb = vec3(0.0); gb_surf = vec3(-1.0); gb_T = vec3(1.0); gb_rep = 0.0; gb_casc = 0.0; }
void gbAmbient(vec3 a) { gb_amb += a; gb_rep = 1.0; }
void gbSurface(vec3 c, vec3 t) { if (gb_surf.x < 0.0) { gb_surf = c; gb_T = t; } }
";

    /// <summary>main() epilogue of a surface effect.</summary>
    public const string SurfaceEpilogue = @"
    vec3 gbL = vec3(0.2126, 0.7152, 0.0722);
    float gbLum = dot(max(gl_FragColor.rgb, vec3(0.0)), gbL);
    float gbFa = 0.0, gbFd = 0.0;
    if (gb_rep > 0.5 && gb_surf.x >= 0.0 && gbLum > 1e-9) {
        float la = dot(max(gb_amb * gb_T, vec3(0.0)), gbL);
        float ls = dot(max(gb_surf * gb_T, vec3(0.0)), gbL);
        gbFa = clamp(la / gbLum, 0.0, 1.0);
        gbFd = clamp(max(ls - la, 0.0) / gbLum, 0.0, 1.0 - gbFa);
    }
    gl_FragData[1] = vec4(1.0 / gl_FragCoord.w, 0.0, 0.0, 1.0);
    gl_FragData[2] = vec4(gbFa, gbFd, gb_rep, gb_casc * 0.2);
    gl_FragData[3] = GB_VELOCITY;
";

    /// <summary>The velocity output of an effect that sets <c>gb_vcur</c>/<c>gb_vprev</c> (replaces GB_VELOCITY).</summary>
    public const string VelocityOut = "vec4(gb_vcur.xy / gb_vcur.w - gb_vprev.xy / gb_vprev.w, 1.0, 1.0)";
    /// <summary>The velocity output of every other effect: none (the resolve uses the depth).</summary>
    public const string NoVelocity = "vec4(0.0)";

    /// <summary>main() epilogue of the sky.</summary>
    public const string SkyEpilogue = @"
    gl_FragData[1] = vec4(1.0e6, 0.0, 0.0, 1.0);
    gl_FragData[2] = vec4(0.0, 0.0, 0.0, 1.0);
    gl_FragData[3] = vec4(0.0);
";

    /// <summary>main() epilogue of a blended effect (keeps the depth below; scales the shares below by 1 − alpha).</summary>
    public const string BlendedEpilogue = @"
    gl_FragData[1] = vec4(0.0);
    gl_FragData[2] = vec4(0.0, 0.0, 0.0, clamp(gl_FragColor.a, 0.0, 1.0));
    gl_FragData[3] = vec4(0.0);
";
}
