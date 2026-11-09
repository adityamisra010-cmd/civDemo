using System.Globalization;
using Microsoft.Xna.Framework;

namespace Gfx.Engine.Rendering;

/// <summary>Sky model. <see cref="Legacy"/> is G1's two-colour gradient.</summary>
public enum SkyMode { Legacy, Analytic }

/// <summary>Display transform after exposure. <see cref="AcesFit"/> is G1's (Narkowicz 2015 fit).</summary>
public enum ToneMapper { AcesFit, PbrNeutral, AcesHill, AgX, Linear }

/// <summary>Where image-based lighting comes from.</summary>
public enum IblSource { None, Sky, Hdri }

/// <summary>Shadow technique. <see cref="Single"/> is G1's one 2048² map around the camera target.</summary>
public enum ShadowTechnique { Single, Cascaded }

/// <summary>Interactive anti-aliasing (stills use jittered accumulation, <see cref="RenderFeatures.StillSamples"/>).</summary>
public enum AntiAliasing { None, Fxaa }

/// <summary>
/// G2 renderer feature switches (stage render1). Every feature can be switched off for A/B captures and costs:
/// <c>Gfx.Demo --capture … --set name=value</c>. <see cref="G1"/> turns everything back to G1's behaviour: the G1
/// frame path renders, and it matches pristine G1's own captures to within 1/255 on a few pixels per shot (the G1
/// shading now shares its shaders with the G2 branches; measured figures in reports/render1.md).
/// PRESENTATION ONLY: nothing here is read by any simulation code.
/// </summary>
public sealed class RenderFeatures
{
    // atmosphere
    public SkyMode Sky { get; set; } = SkyMode.Analytic;
    public bool AerialPerspective { get; set; } = true;
    public bool FarField { get; set; } = true;
    /// <summary>Haze multiplier on the aerosol (Mie) density; scenes may set a biome default.</summary>
    public float Haze { get; set; } = 1f;
    // shading
    public bool Pbr { get; set; } = true;
    public IblSource Ibl { get; set; } = IblSource.Sky;
    /// <summary>Equirectangular Radiance .hdr for <see cref="IblSource.Hdri"/>; null = the default CC0 panorama.</summary>
    public string? HdriPath { get; set; }
    public float HdriRotationDeg { get; set; }
    public bool ShowHdriBackground { get; set; }
    // shadows
    public ShadowTechnique ShadowTechnique { get; set; } = ShadowTechnique.Cascaded;
    public int Cascades { get; set; } = 4;
    public int CascadeSize { get; set; } = 2048;
    public float ShadowDistance { get; set; } = 1600f;
    // post
    public ToneMapper ToneMapper { get; set; } = ToneMapper.PbrNeutral;
    public bool AutoExposure { get; set; } = true;
    /// <summary>Exposure compensation in EV on top of the metered exposure.</summary>
    public float ExposureBiasEv { get; set; }
    public bool Bloom { get; set; } = true;
    public float BloomStrength { get; set; } = 0.045f;
    public bool ColorGrade { get; set; } = true;
    public AntiAliasing Aa { get; set; } = AntiAliasing.Fxaa;
    /// <summary>Jittered accumulation samples for deliverable stills (1 = off). Deterministic Halton(2,3) sequence.</summary>
    public int StillSamples { get; set; } = 1;
    // terrain and water
    public bool PhotoTerrain { get; set; } = true;
    public int Anisotropy { get; set; } = 16;
    public bool HexTiling { get; set; } = true;
    public bool MacroVariation { get; set; } = true;
    public bool WaterRippleFade { get; set; } = true;

    /// <summary>All G1 behaviour (the BEFORE baseline inside the G2 code).</summary>
    public static RenderFeatures G1() => new()
    {
        Sky = SkyMode.Legacy, AerialPerspective = false, FarField = false, Pbr = false, Ibl = IblSource.None,
        ShadowTechnique = ShadowTechnique.Single, ToneMapper = ToneMapper.AcesFit, AutoExposure = false, Bloom = false,
        ColorGrade = false, Aa = AntiAliasing.None, StillSamples = 1, PhotoTerrain = false, Anisotropy = 1, HexTiling = false,
        MacroVariation = false, WaterRippleFade = false,
    };

    /// <summary>True when every switch is at its G1 value, so the frame uses the unchanged G1 path.</summary>
    public bool IsG1Frame =>
        Sky == SkyMode.Legacy && !AerialPerspective && !Pbr && Ibl == IblSource.None && ShadowTechnique == ShadowTechnique.Single
        && ToneMapper == ToneMapper.AcesFit && !AutoExposure && !Bloom && !ColorGrade && Aa == AntiAliasing.None && StillSamples <= 1;

    public RenderFeatures Clone() => (RenderFeatures)MemberwiseClone();

    /// <summary>Names accepted by <see cref="Set"/> (for --help and errors).</summary>
    public static readonly string[] Keys =
    {
        "sky=legacy|analytic", "aerial=0|1", "farfield=0|1", "haze=<x>", "pbr=0|1", "ibl=none|sky|hdri", "hdri=<path>",
        "hdrirot=<deg>", "hdribg=0|1", "shadows=single|csm", "cascades=1..4", "cascadesize=<px>", "shadowdist=<m>",
        "tonemap=acesfit|neutral|aces|agx|linear", "autoexposure=0|1", "ev=<bias>", "bloom=0|1", "bloomstrength=<x>",
        "grade=0|1", "aa=none|fxaa", "samples=<n>", "terrain=procedural|photo", "aniso=<1..16>", "hex=0|1", "macro=0|1",
        "ripplefade=0|1",
    };

    /// <summary>Applies one <c>key=value</c> switch (case-insensitive key). Throws on an unknown key or value.</summary>
    public void Set(string key, string value)
    {
        static bool B(string v) => v switch
        {
            "1" or "on" or "true" or "yes" => true,
            "0" or "off" or "false" or "no" => false,
            _ => throw new ArgumentException($"expected 0|1, got '{v}'"),
        };
        static float F(string v) => float.Parse(v, NumberStyles.Float, CultureInfo.InvariantCulture);
        static int I(string v) => int.Parse(v, NumberStyles.Integer, CultureInfo.InvariantCulture);
        string v = value.Trim();
        string lv = v.ToLowerInvariant();
        switch (key.Trim().ToLowerInvariant())
        {
            case "sky": Sky = lv switch { "legacy" => SkyMode.Legacy, "analytic" => SkyMode.Analytic, _ => throw Bad(key, v) }; break;
            case "aerial": AerialPerspective = B(lv); break;
            case "farfield": FarField = B(lv); break;
            case "haze": Haze = F(v); break;
            case "pbr": Pbr = B(lv); break;
            case "ibl": Ibl = lv switch { "none" or "off" or "0" => IblSource.None, "sky" => IblSource.Sky, "hdri" => IblSource.Hdri, _ => throw Bad(key, v) }; break;
            case "hdri": HdriPath = v; break;
            case "hdrirot": HdriRotationDeg = F(v); break;
            case "hdribg": ShowHdriBackground = B(lv); break;
            case "shadows": ShadowTechnique = lv switch { "single" => ShadowTechnique.Single, "csm" or "cascaded" => ShadowTechnique.Cascaded, _ => throw Bad(key, v) }; break;
            case "cascades": Cascades = Math.Clamp(I(v), 1, 4); break;
            case "cascadesize": CascadeSize = Math.Clamp(I(v), 256, 4096); break;
            case "shadowdist": ShadowDistance = F(v); break;
            case "tonemap":
                ToneMapper = lv switch
                {
                    "acesfit" or "g1" => ToneMapper.AcesFit, "neutral" or "pbrneutral" => ToneMapper.PbrNeutral, "aces" or "aceshill" => ToneMapper.AcesHill,
                    "agx" => ToneMapper.AgX, "linear" or "none" => ToneMapper.Linear, _ => throw Bad(key, v),
                };
                break;
            case "autoexposure": AutoExposure = B(lv); break;
            case "ev": ExposureBiasEv = F(v); break;
            case "bloom": Bloom = B(lv); break;
            case "bloomstrength": BloomStrength = F(v); break;
            case "grade": ColorGrade = B(lv); break;
            case "aa": Aa = lv switch { "none" or "off" or "0" => AntiAliasing.None, "fxaa" => AntiAliasing.Fxaa, _ => throw Bad(key, v) }; break;
            case "samples": StillSamples = Math.Clamp(I(v), 1, 256); break;
            case "terrain": PhotoTerrain = lv switch { "procedural" or "g1" => false, "photo" => true, _ => throw Bad(key, v) }; break;
            case "aniso": Anisotropy = Math.Clamp(I(v), 1, 16); break;
            case "hex": HexTiling = B(lv); break;
            case "macro": MacroVariation = B(lv); break;
            case "ripplefade": WaterRippleFade = B(lv); break;
            default: throw new ArgumentException($"unknown render switch '{key}'. Known: {string.Join(", ", Keys)}");
        }
    }

    /// <summary>Applies a comma-separated list of <c>key=value</c> switches.</summary>
    public void SetMany(string list)
    {
        foreach (var part in list.Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries))
        {
            int eq = part.IndexOf('=');
            if (eq <= 0) throw new ArgumentException($"expected key=value, got '{part}'");
            Set(part[..eq], part[(eq + 1)..]);
        }
    }

    static ArgumentException Bad(string key, string v) => new($"bad value '{v}' for render switch '{key}'");

    /// <summary>One line naming every switch (for capture logs and legends).</summary>
    public string Describe() => string.Create(CultureInfo.InvariantCulture,
        $"sky={Sky} aerial={On(AerialPerspective)} farfield={On(FarField)} haze={Haze:0.##} pbr={On(Pbr)} ibl={Ibl} " +
        $"shadows={ShadowTechnique}x{(ShadowTechnique == ShadowTechnique.Cascaded ? Cascades : 1)} tonemap={ToneMapper} " +
        $"autoexposure={On(AutoExposure)} ev={ExposureBiasEv:0.##} bloom={On(Bloom)} grade={On(ColorGrade)} aa={Aa} samples={StillSamples} " +
        $"terrain={(PhotoTerrain ? "photo" : "procedural")} aniso={Anisotropy} hex={On(HexTiling)} macro={On(MacroVariation)} ripplefade={On(WaterRippleFade)}");

    static string On(bool b) => b ? "1" : "0";
}

/// <summary>Per-renderer settings. The first block is G1's; <see cref="Features"/> holds the G2 switches.</summary>
public sealed class RenderSettings
{
    public bool Shadows { get; set; } = true;
    public int ShadowMapSize { get; set; } = 2048;
    /// <summary>Manual exposure (G1 path and <c>autoexposure=0</c>). With auto exposure, Exposure/0.85 scales the metered value.</summary>
    public float Exposure { get; set; } = 0.85f;
    public bool Tonemap { get; set; } = true;
    /// <summary>Shadow fit sphere (world) for <see cref="ShadowTechnique.Single"/>. Default: around the camera target.</summary>
    public Vector3? ShadowFocus { get; set; }
    public float ShadowRadius { get; set; } = 150f;
    public Vector4 FogParams { get; set; } = new(0.0015f, 0.02f, 0f, 0.85f);
    /// <summary>World box that holds every shadow caster; cascades extend their light-space depth to cover it.</summary>
    public BoundingBox? ShadowCasterBounds { get; set; }
    public RenderFeatures Features { get; set; } = new();
}
