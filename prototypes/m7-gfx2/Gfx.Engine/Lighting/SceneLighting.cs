using Gfx.Engine.Assets;
using Gfx.Engine.Atmosphere;
using Gfx.Engine.Scene;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using Microsoft.Xna.Framework.Graphics.PackedVector;

namespace Gfx.Engine.Lighting;

/// <summary>
/// GPU side of the analytic sky (PRESENTATION ONLY): owns the <see cref="AtmosphereModel"/>, rebuilds the sky-view
/// table when the sun moves or the haze changes, and derives the G2 lighting terms (physically reddened sun,
/// sky ambient, horizon colours) that replace G1's hand-tuned <see cref="SunModel"/> colours.
/// </summary>
public sealed class SkyAtmosphere : IDisposable
{
    /// <summary>Altitude (m) the sky table is computed for; the battle scenes live between 0 and a few hundred metres.</summary>
    public const double CameraAltitude = 150;
    readonly GraphicsDevice _gd;
    static readonly object CacheGate = new();
    static AtmosphereModel? _cachedModel;
    AtmosphereModel _model = null!;
    Texture2D? _skyView;
    float[] _table = Array.Empty<float>();
    Vector3 _sunDir = new(float.NaN); double _haze = double.NaN; bool _night;
    /// <summary>Increments whenever the table changes (IBL rebuilds from it).</summary>
    public int Version { get; private set; }
    public Texture2D SkyView => _skyView ?? throw new InvalidOperationException("Update first");
    /// <summary>True sun direction the table was built for (the shader's azimuth reference).</summary>
    public Vector3 TableSunDir { get; private set; }
    public Rgb SunIrradiance { get; private set; }
    public Rgb SkyIrradiance { get; private set; }
    /// <summary>Extinction per metre at the camera altitude (aerial perspective).</summary>
    public Vector3 AerialExtinction { get; private set; }
    public float LastBuildMs { get; private set; }

    public SkyAtmosphere(GraphicsDevice gd) { _gd = gd; }

    static AtmosphereModel Model(double haze)
    {
        lock (CacheGate)
        {
            if (_cachedModel == null || Math.Abs(_cachedModel.Haze - Math.Max(0.05, haze)) > 1e-6) _cachedModel = new AtmosphereModel(haze);
            return _cachedModel;
        }
    }

    /// <summary>Rebuilds the table when the true sun moved by more than ~0.05° or the haze changed.</summary>
    public void Update(in SunState sun, float haze)
    {
        var dir = Vector3.Normalize(sun.SunDirectionOrLight);
        if (!float.IsNaN(_sunDir.X) && Vector3.Dot(dir, _sunDir) > 0.9999996f && Math.Abs(haze - _haze) < 1e-6 && sun.IsNight == _night) return;
        var sw = System.Diagnostics.Stopwatch.StartNew();
        _model = Model(haze);
        _sunDir = dir; _haze = haze; _night = sun.IsNight; TableSunDir = dir;
        _table = _model.SkyViewTable(CameraAltitude, Vector3d.From(dir), out var eSky);
        if (sun.IsNight)
        {   // moonlit floor: a dim, blue-weighted sky so night scenes are not black (artistic, presentation only)
            var moon = sun.LightColor;
            for (int y = 0; y < AtmosphereModel.SkyH; y++)
            {
                double el = AtmosphereModel.VToElevation((y + 0.5) / AtmosphereModel.SkyH);
                double k = 0.06 * (el > 0 ? 0.6 + 0.4 * (1 - Math.Sin(el)) : 0.3);
                for (int x = 0; x < AtmosphereModel.SkyW; x++)
                {
                    int o = (y * AtmosphereModel.SkyW + x) * 4;
                    _table[o] += (float)(moon.X * k * 0.6); _table[o + 1] += (float)(moon.Y * k * 0.8); _table[o + 2] += (float)(moon.Z * k);
                }
            }
            eSky += new Rgb(moon.X * 0.06 * Math.PI * 0.6, moon.Y * 0.06 * Math.PI * 0.8, moon.Z * 0.06 * Math.PI);
        }
        SkyIrradiance = eSky;
        SunIrradiance = _model.SunIrradiance(CameraAltitude, dir.Y);
        AerialExtinction = _model.ExtinctionAt(CameraAltitude).ToVector3();
        _skyView ??= new Texture2D(_gd, AtmosphereModel.SkyW, AtmosphereModel.SkyH, false, SurfaceFormat.HalfVector4);
        _skyView.SetData(FloatTextures.ToHalf(_table));
        Version++;
        LastBuildMs = (float)sw.Elapsed.TotalMilliseconds;
    }

    /// <summary>CPU lookup of the table with the shader's exact mapping (bilinear, clamped).</summary>
    public Rgb Sample(Vector3d d)
    {
        double el = Math.Asin(Math.Clamp(d.Y, -1, 1));
        double v = AtmosphereModel.ElevationToV(el);
        double hl = Math.Sqrt(d.X * d.X + d.Z * d.Z), sl = Math.Sqrt(TableSunDir.X * TableSunDir.X + TableSunDir.Z * TableSunDir.Z);
        double ca = hl > 1e-5 && sl > 1e-5 ? (d.X * TableSunDir.X + d.Z * TableSunDir.Z) / (hl * sl) : 1;
        double u = Math.Acos(Math.Clamp(ca, -1, 1)) / Math.PI;
        int W = AtmosphereModel.SkyW, H = AtmosphereModel.SkyH;
        double fx = Math.Clamp(u * W - 0.5, 0, W - 1), fy = Math.Clamp(v * H - 0.5, 0, H - 1);
        int x0 = (int)fx, y0 = (int)fy, x1 = Math.Min(x0 + 1, W - 1), y1 = Math.Min(y0 + 1, H - 1);
        double tx = fx - x0, ty = fy - y0;
        Rgb T(int x, int y) { int o = (y * W + x) * 4; return new Rgb(_table[o], _table[o + 1], _table[o + 2]); }
        return (T(x0, y0) * (1 - tx) + T(x1, y0) * tx) * (1 - ty) + (T(x0, y1) * (1 - tx) + T(x1, y1) * tx) * ty;
    }

    /// <summary>The sky (and lit ground below the horizon) as an equirect image, without the sun disc.</summary>
    public EquirectImage ToEquirect(int w, int h)
    {
        var img = new EquirectImage(w, h);
        Parallel.For(0, h, y =>
        {
            for (int x = 0; x < w; x++)
            {
                var c = Sample(img.Direction((x + 0.5) / w, (y + 0.5) / h));
                int o = (y * w + x) * 3;
                img.Rgb[o] = (float)c.R; img.Rgb[o + 1] = (float)c.G; img.Rgb[o + 2] = (float)c.B;
            }
        });
        return img;
    }

    /// <summary>
    /// In-scatter source terms of the air layer at the camera's altitude for the current key light (homogeneous-layer
    /// aerial perspective, L_in = J·(1 − T)): J(θ) = R·P_R(θ) + M·P_M(θ) + Ms. Also the height-fog lighting: FogSun (light
    /// × single-scattering albedo, w = Henyey-Greenstein g) and FogAmb (ambient radiance).
    /// </summary>
    public (Vector4 r, Vector4 m, Vector4 ms, Vector4 fogSun, Vector4 fogAmb) AerialTerms(in SunState derived)
    {
        var (rs, mieS, ext) = _model.Medium(CameraAltitude);
        var light = new Rgb(derived.LightColor.X, derived.LightColor.Y, derived.LightColor.Z);
        var mieRgb = Rgb.Splat(mieS);
        var r = rs / ext * light; var m = mieRgb / ext * light;
        var psi = _model.MultipleScattering(CameraAltitude, Math.Clamp(TableSunDir.Y, -1, 1));
        var ms = (rs + mieRgb) / ext * psi * _model.SunIrradianceTop;
        const double albedo = 0.9;
        var skyE = SkyIrradiance; float sunUp = MathF.Max(derived.DirectionToLight.Y, 0);
        var ground = (light * sunUp + skyE) * (0.15 / Math.PI);
        var amb = (skyE * (1 / Math.PI) * 0.5 + ground * 0.5) * albedo;
        return (new Vector4(r.ToVector3(), 0), new Vector4(m.ToVector3(), 0), new Vector4(ms.ToVector3(), 0),
                new Vector4((light * albedo).ToVector3(), 0.55f), new Vector4(amb.ToVector3(), 0));
    }

    /// <summary>
    /// The G2 sun state: same light direction, but the key light is the atmosphere-transmitted sun (or the moon at
    /// night), ambient terms come from the sky's irradiance and the sky/fog colours from the table.
    /// </summary>
    public SunState Derive(in SunState g1)
    {
        var up = new Vector3d(0, 1, 0);
        Vector3 light = g1.IsNight || g1.DirectionToLight != g1.SunDirectionOrLight ? g1.LightColor : SunIrradiance.ToVector3();
        var zenith = Sample(up).ToVector3();
        Rgb horizonSum = Rgb.Zero;
        for (int i = 0; i < 16; i++)
        {
            double a = i * Math.PI / 8;
            horizonSum += Sample(new Vector3d(Math.Sin(a) * 0.999, 0.035, Math.Cos(a) * 0.999).Normalized());
        }
        var horizon = (horizonSum * (1.0 / 16)).ToVector3();
        var ambSky = (SkyIrradiance * (1 / Math.PI)).ToVector3();
        // ground bounce: grass/soil albedo ≈ 0.15 lit by sun and sky
        float sunUp = MathF.Max(g1.DirectionToLight.Y, 0);
        var ambGround = 0.15f / MathF.PI * (light * sunUp + SkyIrradiance.ToVector3());
        return g1 with { LightColor = light, AmbientSky = ambSky, AmbientGround = ambGround, SkyZenith = zenith, SkyHorizon = horizon, FogColor = horizon };
    }

    public void Dispose() => _skyView?.Dispose();
}

/// <summary>
/// Image-based lighting resources (PRESENTATION ONLY): a GGX-prefiltered equirect mip chain (mip k = roughness k/5),
/// nine irradiance SH coefficients and the split-sum BRDF table. Sources: the analytic sky (rebuilt when the sky
/// changes) or an equirect Radiance HDR (the CC0 default panorama), whose sun is removed (the analytic sun light
/// carries it, with shadows) and whose azimuth is turned so its sun matches the analytic one.
/// </summary>
public sealed class EnvironmentLighting : IDisposable
{
    public const int SpecWidth = 256;
    readonly GraphicsDevice _gd;
    Texture2D? _spec, _brdf, _background;
    int _skyVersion = -1; string? _hdriKey;
    EquirectImage? _hdriSource; string? _hdriLoadedPath; double _hdriSunU = double.NaN;
    public Vector4[] Sh { get; } = new Vector4[9];
    public Texture2D Specular => _spec ?? throw new InvalidOperationException("Update first");
    public Texture2D Brdf => _brdf ??= CreateBrdf();
    /// <summary>The HDRI at 1024×512, turned like the lighting (only when an HDRI is the source).</summary>
    public Texture2D? Background => _background;
    public float MaxLod => IblMath.SpecularRoughLevels - 1;
    public string SourceDescription { get; private set; } = "none";
    public float LastBuildMs { get; private set; }

    public EnvironmentLighting(GraphicsDevice gd) { _gd = gd; }

    static float[]? _brdfData;
    static readonly object BrdfGate = new();
    Texture2D CreateBrdf()
    {
        lock (BrdfGate) _brdfData ??= IblMath.BrdfLut(32, 256);
        return FloatTextures.Half4(_gd, 32, 32, _brdfData);
    }

    /// <summary>Rebuilds from the analytic sky when its table changed.</summary>
    public void UpdateFromSky(SkyAtmosphere sky)
    {
        if (_skyVersion == sky.Version && _hdriKey == null && _spec != null) return;
        var sw = System.Diagnostics.Stopwatch.StartNew();
        var src = sky.ToEquirect(SpecWidth, SpecWidth / 2);
        Build(src);
        _skyVersion = sky.Version; _hdriKey = null;
        _background?.Dispose(); _background = null;
        SourceDescription = "analytic sky";
        LastBuildMs = (float)sw.Elapsed.TotalMilliseconds;
    }

    /// <summary>
    /// Builds from an equirect HDR file. Its brightest region (the sun) is clamped out, its azimuth is turned to put
    /// that sun at the analytic sun's azimuth, and it is scaled so its sky irradiance on level ground equals the
    /// analytic sky's (so the two IBL sources light the scene at comparable levels).
    /// </summary>
    public void UpdateFromHdri(string path, SkyAtmosphere sky, float extraRotationDeg)
    {
        string key = $"{path}|{sky.Version}|{extraRotationDeg}";
        if (_hdriKey == key && _spec != null) return;
        var sw = System.Diagnostics.Stopwatch.StartNew();
        if (_hdriSource == null || _hdriLoadedPath != path)
        {
            var full = RadianceHdr.Load(path);
            _hdriSource = full.Resize(1024, 512);
            _hdriSunU = BrightestU(_hdriSource);
            _hdriLoadedPath = path;
        }
        var src = _hdriSource;
        // turn: the HDRI's sun column goes to the analytic sun's azimuth
        var (sunU, _) = EquirectImage.Uv(Vector3d.From(sky.TableSunDir));
        double turn = _hdriSunU - sunU + extraRotationDeg / 360.0;
        var turned = Turn(src, turn);
        var clamped = ClampSun(turned);
        // level-ground irradiance of each source (upper hemisphere)
        double eH = UpperIrradiance(clamped), eS = sky.SkyIrradiance.Luminance;
        float scale = (float)(eH > 1e-9 ? eS / eH : 1);
        for (int i = 0; i < clamped.Rgb.Length; i++) clamped.Rgb[i] *= scale;
        Build(clamped.Resize(SpecWidth, SpecWidth / 2));
        _background?.Dispose();
        var bg = new float[clamped.Width * clamped.Height * 4];
        for (int i = 0; i < clamped.Width * clamped.Height; i++)
        {
            bg[4 * i] = turned.Rgb[3 * i] * scale; bg[4 * i + 1] = turned.Rgb[3 * i + 1] * scale; bg[4 * i + 2] = turned.Rgb[3 * i + 2] * scale; bg[4 * i + 3] = 1;
        }
        _background = FloatTextures.Half4(_gd, clamped.Width, clamped.Height, bg);
        _hdriKey = key; _skyVersion = -1;
        SourceDescription = $"HDRI {Path.GetFileName(path)} (sun clamped, turned {turn * 360:0}°, scaled ×{scale:0.###})";
        LastBuildMs = (float)sw.Elapsed.TotalMilliseconds;
    }

    void Build(EquirectImage src)
    {
        var levels = IblMath.PrefilterSpecular(src, SpecWidth, 96);
        _spec ??= new Texture2D(_gd, SpecWidth, SpecWidth / 2, true, SurfaceFormat.HalfVector4);
        for (int l = 0; l < Math.Min(levels.Count, _spec.LevelCount); l++)
        {
            var img = levels[l];
            var d = new HalfVector4[img.Width * img.Height];
            for (int i = 0; i < d.Length; i++) d[i] = new HalfVector4(Fin(img.Rgb[3 * i]), Fin(img.Rgb[3 * i + 1]), Fin(img.Rgb[3 * i + 2]), 1);
            _spec.SetData(l, null, d, 0, d.Length);
        }
        var sh = IblMath.IrradianceSh9(src.Resize(64, 32));
        for (int i = 0; i < 9; i++) Sh[i] = new Vector4((float)sh[i].R, (float)sh[i].G, (float)sh[i].B, 0);
    }

    static float Fin(float v) => float.IsFinite(v) ? Math.Clamp(v, 0f, 65000f) : 0f;

    static double BrightestU(EquirectImage img)
    {
        // column of the brightest 9×9-averaged spot in the upper hemisphere
        var small = img.Resize(128, 64); double best = -1, bu = 0.5;
        for (int y = 1; y < 32; y++)
            for (int x = 0; x < 128; x++)
            {
                int o = (y * 128 + x) * 3;
                double l = 0.2126 * small.Rgb[o] + 0.7152 * small.Rgb[o + 1] + 0.0722 * small.Rgb[o + 2];
                if (l > best) { best = l; bu = (x + 0.5) / 128; }
            }
        return bu;
    }

    /// <summary>Turns an equirect by <paramref name="turns"/> of a revolution (column shift, nearest column).</summary>
    static EquirectImage Turn(EquirectImage src, double turns)
    {
        int shift = (int)Math.Round(turns * src.Width);
        shift = ((shift % src.Width) + src.Width) % src.Width;
        var o = new EquirectImage(src.Width, src.Height);
        for (int y = 0; y < src.Height; y++)
            for (int x = 0; x < src.Width; x++)
            {
                int sx = (x + shift) % src.Width;
                Array.Copy(src.Rgb, (y * src.Width + sx) * 3, o.Rgb, (y * src.Width + x) * 3, 3);
            }
        return o;
    }

    /// <summary>Clamps texels brighter than 16× the 99th-percentile luminance (removes the sun from the IBL).</summary>
    static EquirectImage ClampSun(EquirectImage src)
    {
        int n = src.Width * src.Height; var lum = new float[n];
        for (int i = 0; i < n; i++) lum[i] = 0.2126f * src.Rgb[3 * i] + 0.7152f * src.Rgb[3 * i + 1] + 0.0722f * src.Rgb[3 * i + 2];
        var sorted = (float[])lum.Clone(); Array.Sort(sorted);
        float cap = 16f * sorted[(int)(0.99 * (n - 1))];
        var o = new EquirectImage(src.Width, src.Height, (float[])src.Rgb.Clone());
        for (int i = 0; i < n; i++)
            if (lum[i] > cap) { float s = cap / lum[i]; o.Rgb[3 * i] *= s; o.Rgb[3 * i + 1] *= s; o.Rgb[3 * i + 2] *= s; }
        return o;
    }

    static double UpperIrradiance(EquirectImage img)
    {
        double e = 0, dPhi = 2 * Math.PI / img.Width, dTheta = Math.PI / img.Height;
        for (int y = 0; y < img.Height / 2; y++)
        {
            double theta = (y + 0.5) * dTheta, w = Math.Cos(theta) * Math.Sin(theta) * dPhi * dTheta;
            for (int x = 0; x < img.Width; x++)
            {
                int o = (y * img.Width + x) * 3;
                e += w * (0.2126 * img.Rgb[o] + 0.7152 * img.Rgb[o + 1] + 0.0722 * img.Rgb[o + 2]);
            }
        }
        return e;
    }

    public void Dispose() { _spec?.Dispose(); _brdf?.Dispose(); _background?.Dispose(); }
}
