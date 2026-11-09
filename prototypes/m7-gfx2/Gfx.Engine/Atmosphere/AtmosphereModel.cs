using Microsoft.Xna.Framework;

namespace Gfx.Engine.Atmosphere;

/// <summary>Small double-precision RGB triple for the CPU atmosphere integrals.</summary>
public readonly record struct Rgb(double R, double G, double B)
{
    public static Rgb operator +(Rgb a, Rgb b) => new(a.R + b.R, a.G + b.G, a.B + b.B);
    public static Rgb operator -(Rgb a, Rgb b) => new(a.R - b.R, a.G - b.G, a.B - b.B);
    public static Rgb operator *(Rgb a, Rgb b) => new(a.R * b.R, a.G * b.G, a.B * b.B);
    public static Rgb operator *(Rgb a, double s) => new(a.R * s, a.G * s, a.B * s);
    public static Rgb operator *(double s, Rgb a) => new(a.R * s, a.G * s, a.B * s);
    public static Rgb operator /(Rgb a, Rgb b) => new(a.R / b.R, a.G / b.G, a.B / b.B);
    public static Rgb Exp(Rgb a) => new(Math.Exp(a.R), Math.Exp(a.G), Math.Exp(a.B));
    public static Rgb Splat(double v) => new(v, v, v);
    public static readonly Rgb Zero = new(0, 0, 0);
    public double Luminance => 0.2126 * R + 0.7152 * G + 0.0722 * B;
    public Vector3 ToVector3() => new((float)R, (float)G, (float)B);
}

/// <summary>
/// Clear-sky atmosphere ("Bruneton-lite", PRESENTATION ONLY). Physically based single scattering of Rayleigh and Mie
/// particles plus ozone absorption, ray-marched on the CPU against a precomputed transmittance table, with the
/// isotropic multiple-scattering approximation of Hillaire (2020, "A Scalable and Production Ready Sky and Atmosphere
/// Rendering Technique", §5.5). Coefficients are the Earth values published in Bruneton (2017) / Hillaire (2020).
/// Original implementation; no code was copied. Output: a sky-view table (azimuth from the sun × elevation) that
/// the sky shader, aerial perspective and image-based lighting all sample, so ground haze converges on the real
/// horizon colour (no grey wall beyond the map).
/// <para>Units: radiance and irradiance in scene units where the top-of-atmosphere sun irradiance is
/// <see cref="SolarIrradiance"/> (white-balanced so the sun at 60° elevation is neutral white).</para>
/// </summary>
public sealed class AtmosphereModel
{
    public const double GroundRadius = 6_360_000, TopRadius = 6_460_000;
    static readonly Rgb RayleighScattering = new(5.802e-6, 13.558e-6, 33.1e-6);
    const double RayleighScaleHeight = 8000;
    const double MieScattering = 3.996e-6, MieAbsorption = 4.40e-6, MieScaleHeight = 1200, MieG = 0.8;
    static readonly Rgb OzoneAbsorption = new(0.650e-6, 1.881e-6, 0.085e-6);
    const double OzoneCenter = 25_000, OzoneHalfWidth = 15_000;

    public const int TransW = 256, TransH = 64, MsSize = 32;
    public const int SkyW = 192, SkyH = 108;
    /// <summary>Scene-unit sun irradiance at the top of the atmosphere before white balance (G1's noon sun was ≈3).</summary>
    public const double SolarIrradiance = 3.6;

    /// <summary>Aerosol density multiplier (1 clear, 2–4 hazy or dusty).</summary>
    public double Haze { get; }
    public Rgb GroundAlbedo { get; }
    /// <summary>Per-channel top-of-atmosphere sun irradiance after white balance.</summary>
    public Rgb SunIrradianceTop { get; }

    readonly Rgb[] _trans = new Rgb[TransW * TransH];
    readonly Rgb[] _ms = new Rgb[MsSize * MsSize];

    public AtmosphereModel(double haze = 1.0, double groundAlbedo = 0.25)
    {
        Haze = Math.Max(0.05, haze); GroundAlbedo = Rgb.Splat(groundAlbedo);
        Parallel.For(0, TransH, y => { for (int x = 0; x < TransW; x++) _trans[y * TransW + x] = ComputeTransmittance(TransAltitude(y), TransMu(x)); });
        SunIrradianceTop = Rgb.Splat(SolarIrradiance) / Transmittance(0, Math.Sin(MathHelper.ToRadians(60)));
        Parallel.For(0, MsSize, y => { for (int x = 0; x < MsSize; x++) _ms[y * MsSize + x] = ComputeMultipleScattering(MsAltitude(y), MsMu(x)); });
    }

    // ---------------------------------------------------------------- medium
    double MieDensity(double h) => Math.Exp(-h / MieScaleHeight) * Haze;

    /// <summary>Scattering and extinction coefficients (/m) at altitude h (m).</summary>
    public (Rgb rayleighScat, double mieScat, Rgb extinction) Medium(double h)
    {
        h = Math.Max(0, h);
        double r = Math.Exp(-h / RayleighScaleHeight), m = MieDensity(h);
        double o = Math.Max(0, 1 - Math.Abs(h - OzoneCenter) / OzoneHalfWidth);
        var rs = RayleighScattering * r; double ms = MieScattering * m;
        var ext = rs + Rgb.Splat((MieScattering + MieAbsorption) * m) + OzoneAbsorption * o;
        return (rs, ms, ext);
    }

    /// <summary>Extinction per metre at altitude h (used for aerial perspective).</summary>
    public Rgb ExtinctionAt(double h) => Medium(h).extinction;

    public static double RayleighPhase(double cosT) => 3.0 / (16 * Math.PI) * (1 + cosT * cosT);
    public static double MiePhase(double cosT)
    {
        const double g = MieG, g2 = g * g;
        return 3.0 / (8 * Math.PI) * ((1 - g2) * (1 + cosT * cosT)) / ((2 + g2) * Math.Pow(1 + g2 - 2 * g * cosT, 1.5));
    }

    // ---------------------------------------------------------------- geometry
    static double DistanceToTop(double r, double mu)
    {
        double d = r * r * (mu * mu - 1) + TopRadius * TopRadius;
        return Math.Max(0, -r * mu + Math.Sqrt(Math.Max(0, d)));
    }

    /// <summary>Distance to the ground along (r, mu), or −1 when the ray misses the planet.</summary>
    static double DistanceToGround(double r, double mu)
    {
        if (mu >= 0) return -1;
        double d = r * r * (mu * mu - 1) + GroundRadius * GroundRadius;
        if (d < 0) return -1;
        double t = -r * mu - Math.Sqrt(d);
        return t >= 0 ? t : -1;
    }

    // ---------------------------------------------------------------- transmittance table
    // x: mu = sign(s)·s² with s uniform in [-1, 1] (fine steps near the horizon, where sunsets happen)
    // y: h = H·t² with t uniform in [0, 1] (fine steps near the ground)
    static double TransMu(int x) { double s = -1 + 2.0 * x / (TransW - 1); return Math.Sign(s) * s * s; }
    static double TransAltitude(int y) { double t = y / (double)(TransH - 1); return (TopRadius - GroundRadius) * t * t; }

    Rgb ComputeTransmittance(double h, double mu)
    {
        double r = GroundRadius + h;
        if (DistanceToGround(r, mu) >= 0) return Rgb.Zero; // the planet blocks the sun
        double len = DistanceToTop(r, mu);
        const int N = 160;
        var depth = Rgb.Zero;
        for (int i = 0; i < N; i++)
        {
            // quadratic spacing: most samples in the dense air near the start
            double a = (double)i / N, b = (double)(i + 1) / N;
            double t0 = a * a * len, t1 = b * b * len, tm = 0.5 * (t0 + t1);
            double hh = Math.Sqrt(r * r + tm * tm + 2 * r * mu * tm) - GroundRadius;
            depth += Medium(hh).extinction * (t1 - t0);
        }
        return Rgb.Exp(depth * -1);
    }

    /// <summary>Transmittance from altitude h (m) towards zenith cosine mu, to space (0 when the ground is in the way).</summary>
    public Rgb Transmittance(double h, double mu)
    {
        double s = Math.Sign(mu) * Math.Sqrt(Math.Abs(Math.Clamp(mu, -1, 1)));
        double fx = (s + 1) / 2 * (TransW - 1);
        double fy = Math.Sqrt(Math.Clamp(h / (TopRadius - GroundRadius), 0, 1)) * (TransH - 1);
        return Bilinear(_trans, TransW, TransH, fx, fy);
    }

    static Rgb Bilinear(Rgb[] t, int w, int hgt, double fx, double fy)
    {
        fx = Math.Clamp(fx, 0, w - 1); fy = Math.Clamp(fy, 0, hgt - 1);
        int x0 = (int)fx, y0 = (int)fy, x1 = Math.Min(x0 + 1, w - 1), y1 = Math.Min(y0 + 1, hgt - 1);
        double tx = fx - x0, ty = fy - y0;
        var a = t[y0 * w + x0] * (1 - tx) + t[y0 * w + x1] * tx;
        var b = t[y1 * w + x0] * (1 - tx) + t[y1 * w + x1] * tx;
        return a * (1 - ty) + b * ty;
    }

    // ---------------------------------------------------------------- multiple scattering (Hillaire 2020 §5.5)
    static double MsMu(int x) => -1 + 2.0 * (x + 0.5) / MsSize;
    static double MsAltitude(int y) => (TopRadius - GroundRadius) * Math.Pow((y + 0.5) / MsSize, 2);

    Rgb ComputeMultipleScattering(double h, double muS)
    {
        double r = GroundRadius + h;
        var sun = new Vector3d(Math.Sqrt(Math.Max(0, 1 - muS * muS)), muS, 0);
        const int dirs = 64, steps = 20;
        var l2 = Rgb.Zero; var fms = Rgb.Zero;
        const double golden = 2.399963229728653; // Fibonacci sphere: deterministic, even coverage
        for (int k = 0; k < dirs; k++)
        {
            double y = 1 - 2 * (k + 0.5) / dirs, rad = Math.Sqrt(1 - y * y), phi = golden * k;
            var d = new Vector3d(Math.Cos(phi) * rad, y, Math.Sin(phi) * rad);
            double mu = d.Y;
            double tg = DistanceToGround(r, mu);
            double len = tg >= 0 ? tg : DistanceToTop(r, mu);
            var throughput = Rgb.Splat(1); var lumDir = Rgb.Zero; var fDir = Rgb.Zero;
            double dt = len / steps;
            for (int i = 0; i < steps; i++)
            {
                double t = (i + 0.5) * dt;
                var p = new Vector3d(d.X * t, r + d.Y * t, d.Z * t);
                double pr = p.Length; double ph = pr - GroundRadius;
                var (rs, mieS, ext) = Medium(ph);
                var scat = rs + Rgb.Splat(mieS);
                double muSp = Vector3d.Dot(p, sun) / pr;
                var tSun = Transmittance(ph, muSp);
                var stepT = Rgb.Exp(ext * -dt);
                // integrate S·σs over the step analytically against extinction
                var integ = (Rgb.Splat(1) - stepT) / Safe(ext);
                lumDir += throughput * integ * scat * tSun * (1 / (4 * Math.PI));
                fDir += throughput * integ * scat;
                throughput *= stepT;
            }
            if (tg >= 0)
            {
                var g = new Vector3d(d.X * tg, r + d.Y * tg, d.Z * tg);
                double muG = Vector3d.Dot(g, sun) / g.Length;
                lumDir += throughput * GroundAlbedo * Transmittance(0, muG) * (Math.Max(muG, 0) / Math.PI);
            }
            l2 += lumDir * (1.0 / dirs) * (4 * Math.PI) * (1 / (4 * Math.PI));
            fms += fDir * (1.0 / dirs) * (4 * Math.PI) * (1 / (4 * Math.PI));
        }
        // geometric series of higher orders: L2 / (1 - fms)
        return new Rgb(l2.R / (1 - Math.Min(fms.R, 0.99)), l2.G / (1 - Math.Min(fms.G, 0.99)), l2.B / (1 - Math.Min(fms.B, 0.99)));
    }

    static Rgb Safe(Rgb e) => new(Math.Max(e.R, 1e-12), Math.Max(e.G, 1e-12), Math.Max(e.B, 1e-12));

    /// <summary>Multiple-scattering transfer Ψ (per unit top-of-atmosphere irradiance) at altitude h for sun zenith cosine muS.</summary>
    public Rgb MultipleScattering(double h, double muS)
    {
        double fx = (muS + 1) / 2 * MsSize - 0.5;
        double fy = Math.Sqrt(Math.Clamp(h / (TopRadius - GroundRadius), 0, 1)) * MsSize - 0.5;
        return Bilinear(_ms, MsSize, MsSize, fx, fy);
    }

    // ---------------------------------------------------------------- sky radiance
    /// <summary>
    /// Sky radiance (scene units) seen from altitude <paramref name="h"/> along <paramref name="viewDir"/> (y up) with the
    /// light (sun) towards <paramref name="sunDir"/>. Rays that hit the ground include the lit ground behind the air.
    /// </summary>
    public Rgb SkyRadiance(double h, Vector3d viewDir, Vector3d sunDir, int steps = 40) => SkyRadianceNormalized(h, viewDir, sunDir, Rgb.Zero, steps) * SunIrradianceTop;

    /// <summary>
    /// <see cref="SkyRadiance"/> per unit top-of-atmosphere irradiance. <paramref name="groundSkyIrradiance"/> (same
    /// units) lights the ground behind downward rays in addition to the sun.
    /// </summary>
    Rgb SkyRadianceNormalized(double h, Vector3d viewDir, Vector3d sunDir, Rgb groundSkyIrradiance, int steps = 40)
    {
        double r = GroundRadius + Math.Max(h, 1);
        double mu = viewDir.Y;
        double tg = DistanceToGround(r, mu);
        double len = tg >= 0 ? tg : DistanceToTop(r, mu);
        double cosT = Vector3d.Dot(viewDir, sunDir);
        double pR = RayleighPhase(cosT), pM = MiePhase(cosT);
        var sum = Rgb.Zero; var throughput = Rgb.Splat(1);
        double prev = 0;
        for (int i = 0; i < steps; i++)
        {
            double u = (i + 1.0) / steps;
            double t = u * u * len, dt = t - prev, tm = 0.5 * (t + prev); prev = t;
            var p = new Vector3d(viewDir.X * tm, r + viewDir.Y * tm, viewDir.Z * tm);
            double pr = p.Length, ph = pr - GroundRadius;
            var (rs, mieS, ext) = Medium(ph);
            double muS = Vector3d.Dot(p, sunDir) / pr;
            var tSun = Transmittance(ph, muS);
            var ms = MultipleScattering(ph, muS);
            var s = rs * (tSun * pR + ms) + Rgb.Splat(mieS) * (tSun * pM + ms);
            var stepT = Rgb.Exp(ext * -dt);
            sum += throughput * s * ((Rgb.Splat(1) - stepT) / Safe(ext));
            throughput *= stepT;
        }
        if (tg >= 0)
        {
            var g = new Vector3d(viewDir.X * tg, r + viewDir.Y * tg, viewDir.Z * tg);
            double muG = Vector3d.Dot(g, sunDir) / g.Length;
            sum += throughput * GroundAlbedo * (Transmittance(0, muG) * Math.Max(muG, 0) + groundSkyIrradiance) * (1 / Math.PI);
        }
        return sum;
    }

    /// <summary>Sun irradiance reaching altitude h for a sun at zenith cosine mu (0 below the horizon).</summary>
    public Rgb SunIrradiance(double h, double mu) => Transmittance(h, mu) * SunIrradianceTop;

    /// <summary>Elevation parameter of the sky-view table: v = 0.5 ± 0.5·sqrt(|elevation| / (π/2)), horizon at 0.5.</summary>
    public static double ElevationToV(double elevation) =>
        0.5 + 0.5 * Math.Sign(elevation) * Math.Sqrt(Math.Min(Math.Abs(elevation), Math.PI / 2) / (Math.PI / 2));

    public static double VToElevation(double v)
    {
        double s = (v - 0.5) * 2;
        return Math.Sign(s) * s * s * Math.PI / 2;
    }

    /// <summary>
    /// Sky-view table: <see cref="SkyW"/> × <see cref="SkyH"/> RGBA floats (A = 1). Column u = azimuth from the sun / π
    /// (0 = towards the sun, texel centres), row v as <see cref="ElevationToV"/> (row 0 = straight down).
    /// </summary>
    public float[] SkyViewTable(double cameraAltitude, Vector3d sunDir) => SkyViewTable(cameraAltitude, sunDir, out _);

    /// <summary>As <see cref="SkyViewTable(double, Vector3d)"/>, also returning the sky irradiance on level ground (scene units).</summary>
    public float[] SkyViewTable(double cameraAltitude, Vector3d sunDir, out Rgb skyIrradiance)
    {
        var data = new float[SkyW * SkyH * 4];
        double sunEl = Math.Asin(Math.Clamp(sunDir.Y, -1, 1));
        var s = new Vector3d(0, Math.Sin(sunEl), Math.Cos(sunEl));
        var rows = new Rgb[SkyH * SkyW];
        void Row(int y, Rgb groundSky)
        {
            double el = VToElevation((y + 0.5) / SkyH);
            for (int x = 0; x < SkyW; x++)
            {
                double az = (x + 0.5) / SkyW * Math.PI;
                // the sun sits at azimuth 0 in this frame: view = (sin az·cos el, sin el, cos az·cos el), sun = (0, sin, cos)
                var v = new Vector3d(Math.Sin(az) * Math.Cos(el), Math.Sin(el), Math.Cos(az) * Math.Cos(el));
                rows[y * SkyW + x] = SkyRadianceNormalized(cameraAltitude, v, s, groundSky);
            }
        }
        // 1. upper hemisphere; 2. its irradiance on level ground; 3. lower hemisphere (ground lit by sun + that sky)
        Parallel.For(SkyH / 2, SkyH, y => Row(y, Rgb.Zero));
        var e = Rgb.Zero;
        for (int y = SkyH / 2; y < SkyH; y++)
        {
            double el0 = Math.Max(0, VToElevation((double)y / SkyH)), el1 = VToElevation((y + 1.0) / SkyH);
            double el = 0.5 * (el0 + el1), w = Math.Sin(el) * Math.Cos(el) * (el1 - el0) * (Math.PI / SkyW) * 2;
            for (int x = 0; x < SkyW; x++) e += rows[y * SkyW + x] * w;
        }
        Parallel.For(0, SkyH / 2, y => Row(y, e));
        for (int i = 0; i < rows.Length; i++)
        {
            var L = rows[i] * SunIrradianceTop;
            data[4 * i] = (float)L.R; data[4 * i + 1] = (float)L.G; data[4 * i + 2] = (float)L.B; data[4 * i + 3] = 1;
        }
        skyIrradiance = e * SunIrradianceTop;
        return data;
    }
}

/// <summary>Minimal double 3-vector for the atmosphere integrals.</summary>
public readonly record struct Vector3d(double X, double Y, double Z)
{
    public double Length => Math.Sqrt(X * X + Y * Y + Z * Z);
    public static double Dot(Vector3d a, Vector3d b) => a.X * b.X + a.Y * b.Y + a.Z * b.Z;
    public Vector3d Normalized() { double l = Length; return new(X / l, Y / l, Z / l); }
    public static Vector3d From(Vector3 v) => new(v.X, v.Y, v.Z);
}
