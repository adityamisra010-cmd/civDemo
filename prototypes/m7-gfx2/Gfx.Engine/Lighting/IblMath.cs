using Gfx.Engine.Atmosphere;

namespace Gfx.Engine.Lighting;

/// <summary>Equirectangular float RGB image (row-major, 3 floats per texel, row 0 = straight up).</summary>
public sealed class EquirectImage
{
    public int Width { get; }
    public int Height { get; }
    public float[] Rgb { get; }

    public EquirectImage(int width, int height, float[]? rgb = null)
    {
        Width = width; Height = height; Rgb = rgb ?? new float[width * height * 3];
        if (Rgb.Length != width * height * 3) throw new ArgumentException("size mismatch");
    }

    /// <summary>Unit direction of texel centre (x, y); matches the shader's <c>equirectUv</c> (u = 0.5 looks along −Z).</summary>
    public Vector3d Direction(double u, double v)
    {
        double phi = (u - 0.5) * 2 * Math.PI, theta = v * Math.PI;
        double st = Math.Sin(theta);
        return new Vector3d(st * Math.Sin(phi), Math.Cos(theta), -st * Math.Cos(phi));
    }

    public static (double u, double v) Uv(Vector3d d) =>
        (Math.Atan2(d.X, -d.Z) / (2 * Math.PI) + 0.5, Math.Acos(Math.Clamp(d.Y, -1, 1)) / Math.PI);

    /// <summary>Bilinear sample (u wraps, v clamps), texel centres at (i + 0.5) / size.</summary>
    public Rgb Sample(double u, double v)
    {
        double fx = u * Width - 0.5, fy = Math.Clamp(v * Height - 0.5, 0, Height - 1);
        int x0 = (int)Math.Floor(fx), y0 = (int)fy;
        double tx = fx - x0, ty = fy - y0;
        int y1 = Math.Min(y0 + 1, Height - 1);
        x0 = ((x0 % Width) + Width) % Width; int x1 = (x0 + 1) % Width;
        Rgb T(int x, int y) { int o = (y * Width + x) * 3; return new Rgb(Rgb[o], Rgb[o + 1], Rgb[o + 2]); }
        return (T(x0, y0) * (1 - tx) + T(x1, y0) * tx) * (1 - ty) + (T(x0, y1) * (1 - tx) + T(x1, y1) * tx) * ty;
    }

    public Rgb Sample(Vector3d d) { var (u, v) = Uv(d); return Sample(u, v); }

    /// <summary>2×2 box downsample (both sizes must be even).</summary>
    public EquirectImage Half()
    {
        int w = Math.Max(1, Width / 2), h = Math.Max(1, Height / 2);
        var o = new EquirectImage(w, h);
        for (int y = 0; y < h; y++)
            for (int x = 0; x < w; x++)
                for (int c = 0; c < 3; c++)
                {
                    int x0 = Math.Min(2 * x, Width - 1), x1 = Math.Min(2 * x + 1, Width - 1), y0 = Math.Min(2 * y, Height - 1), y1 = Math.Min(2 * y + 1, Height - 1);
                    o.Rgb[(y * w + x) * 3 + c] = 0.25f * (Rgb[(y0 * Width + x0) * 3 + c] + Rgb[(y0 * Width + x1) * 3 + c]
                                                        + Rgb[(y1 * Width + x0) * 3 + c] + Rgb[(y1 * Width + x1) * 3 + c]);
                }
        return o;
    }

    /// <summary>Area-average resample to an exact smaller size (integer factor or not).</summary>
    public EquirectImage Resize(int w, int h)
    {
        var o = new EquirectImage(w, h);
        double sx = (double)Width / w, sy = (double)Height / h;
        Parallel.For(0, h, y =>
        {
            int ya = (int)(y * sy), yb = Math.Max(ya + 1, (int)((y + 1) * sy));
            for (int x = 0; x < w; x++)
            {
                int xa = (int)(x * sx), xb = Math.Max(xa + 1, (int)((x + 1) * sx));
                double r = 0, g = 0, b = 0; int n = 0;
                for (int yy = ya; yy < Math.Min(yb, Height); yy++)
                    for (int xx = xa; xx < Math.Min(xb, Width); xx++)
                    {
                        int i = (yy * Width + xx) * 3; r += Rgb[i]; g += Rgb[i + 1]; b += Rgb[i + 2]; n++;
                    }
                int oi = (y * w + x) * 3;
                o.Rgb[oi] = (float)(r / n); o.Rgb[oi + 1] = (float)(g / n); o.Rgb[oi + 2] = (float)(b / n);
            }
        });
        return o;
    }
}

/// <summary>
/// Image-based lighting maths (CPU, deterministic: Hammersley sequences, no random numbers). GGX prefiltering with
/// filtered importance sampling (Křivánek and Colbert 2008), irradiance spherical harmonics (Ramamoorthi and
/// Hanrahan 2001), and the split-sum environment BRDF table (Karis 2013) with height-correlated Smith visibility.
/// Original implementation from the published formulas.
/// </summary>
public static class IblMath
{
    public const int SpecularRoughLevels = 6; // mip k holds roughness k / 5

    public static (double x, double y) Hammersley(int i, int n)
    {
        uint b = (uint)i;
        b = (b << 16) | (b >> 16);
        b = ((b & 0x55555555u) << 1) | ((b & 0xAAAAAAAAu) >> 1);
        b = ((b & 0x33333333u) << 2) | ((b & 0xCCCCCCCCu) >> 2);
        b = ((b & 0x0F0F0F0Fu) << 4) | ((b & 0xF0F0F0F0u) >> 4);
        b = ((b & 0x00FF00FFu) << 8) | ((b & 0xFF00FF00u) >> 8);
        return ((double)i / n, b * 2.3283064365386963e-10);
    }

    /// <summary>GGX half vector around +Z (tangent space) for alpha = roughness².</summary>
    static Vector3d SampleGgx(double u1, double u2, double a)
    {
        double phi = 2 * Math.PI * u1;
        double cosT = Math.Sqrt((1 - u2) / (1 + (a * a - 1) * u2)), sinT = Math.Sqrt(1 - cosT * cosT);
        return new Vector3d(sinT * Math.Cos(phi), sinT * Math.Sin(phi), cosT);
    }

    static double DGgx(double noh, double a) { double a2 = a * a, f = (noh * a2 - noh) * noh + 1; return a2 / (Math.PI * f * f); }

    static double VSmithCorrelated(double nov, double nol, double a)
    {
        double a2 = a * a;
        double gv = nol * Math.Sqrt(nov * nov * (1 - a2) + a2), gl = nov * Math.Sqrt(nol * nol * (1 - a2) + a2);
        return 0.5 / (gv + gl);
    }

    static (Vector3d t, Vector3d b) Basis(Vector3d n)
    {
        var up = Math.Abs(n.Y) < 0.999 ? new Vector3d(0, 1, 0) : new Vector3d(1, 0, 0);
        var t = Cross(up, n).Normalized();
        return (t, Cross(n, t));
    }

    static Vector3d Cross(Vector3d a, Vector3d b) => new(a.Y * b.Z - a.Z * b.Y, a.Z * b.X - a.X * b.Z, a.X * b.Y - a.Y * b.X);

    /// <summary>
    /// Specular mip chain for an equirect source: level 0 is the source resampled to <paramref name="baseW"/> ×
    /// baseW/2 (mirror); level k ≤ 5 is GGX-prefiltered for roughness k/5; deeper levels are box-downsampled.
    /// </summary>
    public static List<EquirectImage> PrefilterSpecular(EquirectImage source, int baseW = 256, int samples = 96)
    {
        var levels = new List<EquirectImage>();
        var src0 = source.Width == baseW ? source : source.Resize(baseW, baseW / 2);
        // source mip pyramid for filtered importance sampling
        var pyramid = new List<EquirectImage> { src0 };
        while (pyramid[^1].Width > 4) pyramid.Add(pyramid[^1].Half());
        levels.Add(src0);
        int w = baseW / 2;
        for (int k = 1; w >= 1 && levels.Count < 32; k++)
        {
            if (k >= SpecularRoughLevels) { levels.Add(levels[^1].Half()); w /= 2; if (w < 1) break; continue; }
            double rough = k / (double)(SpecularRoughLevels - 1), a = rough * rough;
            int h = Math.Max(1, w / 2);
            var outImg = new EquirectImage(w, h);
            int n = k == 1 ? samples : samples;
            double texelSolid = 4 * Math.PI / (baseW * (baseW / 2.0));
            var dirs = new (Vector3d h, double pdfLod)[n];
            for (int i = 0; i < n; i++)
            {
                var (u1, u2) = Hammersley(i, n);
                var hv = SampleGgx(u1, u2, a);
                double pdf = DGgx(hv.Z, a) / 4; // N = V: pdf(L) = D(NoH)·NoH / (4·VoH) = D / 4
                double omegaS = 1.0 / (n * pdf);
                dirs[i] = (hv, Math.Max(0, 0.5 * Math.Log2(omegaS / texelSolid) + 1));
            }
            var img = outImg; int ww = w;
            Parallel.For(0, h, y =>
            {
                for (int x = 0; x < ww; x++)
                {
                    var N = img.Direction((x + 0.5) / ww, (y + 0.5) / h);
                    var (t, b) = Basis(N);
                    Rgb sum = Rgb.Zero; double wsum = 0;
                    foreach (var (hv, lod) in dirs)
                    {
                        var H = new Vector3d(t.X * hv.X + b.X * hv.Y + N.X * hv.Z, t.Y * hv.X + b.Y * hv.Y + N.Y * hv.Z, t.Z * hv.X + b.Z * hv.Y + N.Z * hv.Z);
                        double voh = Vector3d.Dot(N, H);
                        var L = new Vector3d(2 * voh * H.X - N.X, 2 * voh * H.Y - N.Y, 2 * voh * H.Z - N.Z);
                        double nol = Vector3d.Dot(N, L);
                        if (nol <= 0) continue;
                        sum += SamplePyramid(pyramid, L, lod) * nol; wsum += nol;
                    }
                    var c = wsum > 0 ? sum * (1 / wsum) : Rgb.Zero;
                    int o = (y * ww + x) * 3;
                    img.Rgb[o] = (float)c.R; img.Rgb[o + 1] = (float)c.G; img.Rgb[o + 2] = (float)c.B;
                }
            });
            levels.Add(outImg);
            if (w == 1) break;
            w /= 2;
        }
        return levels;
    }

    static Rgb SamplePyramid(List<EquirectImage> p, Vector3d d, double lod)
    {
        lod = Math.Clamp(lod, 0, p.Count - 1);
        int l0 = (int)lod; int l1 = Math.Min(l0 + 1, p.Count - 1); double t = lod - l0;
        var (u, v) = EquirectImage.Uv(d);
        var a = p[l0].Sample(u, v);
        return t <= 0 ? a : a * (1 - t) + p[l1].Sample(u, v) * t;
    }

    /// <summary>
    /// Irradiance SH9, already convolved with the clamped cosine and multiplied by the basis constants, in the order the
    /// shader's <c>shIrradiance</c> uses: 1, y, z, x, xy, yz, 3z²−1, xz, x²−y². E(n) = Σ c_i·poly_i(n).
    /// </summary>
    public static Rgb[] IrradianceSh9(EquirectImage src)
    {
        var L = new Rgb[9];
        double dPhi = 2 * Math.PI / src.Width, dTheta = Math.PI / src.Height;
        for (int y = 0; y < src.Height; y++)
        {
            double theta = (y + 0.5) * dTheta, dw = dPhi * dTheta * Math.Sin(theta);
            for (int x = 0; x < src.Width; x++)
            {
                var d = src.Direction((x + 0.5) / src.Width, (y + 0.5) / src.Height);
                int o = (y * src.Width + x) * 3;
                var c = new Rgb(src.Rgb[o], src.Rgb[o + 1], src.Rgb[o + 2]) * dw;
                double X = d.X, Y = d.Y, Z = d.Z;
                L[0] += c * 0.282095;
                L[1] += c * (0.488603 * Y); L[2] += c * (0.488603 * Z); L[3] += c * (0.488603 * X);
                L[4] += c * (1.092548 * X * Y); L[5] += c * (1.092548 * Y * Z); L[6] += c * (0.315392 * (3 * Z * Z - 1));
                L[7] += c * (1.092548 * X * Z); L[8] += c * (0.546274 * (X * X - Y * Y));
            }
        }
        double a0 = Math.PI, a1 = 2 * Math.PI / 3, a2 = Math.PI / 4;
        double[] k = { 0.282095 * a0, 0.488603 * a1, 0.488603 * a1, 0.488603 * a1, 1.092548 * a2, 1.092548 * a2, 0.315392 * a2, 1.092548 * a2, 0.546274 * a2 };
        var o9 = new Rgb[9];
        for (int i = 0; i < 9; i++) o9[i] = L[i] * k[i];
        return o9;
    }

    /// <summary>Evaluates <see cref="IrradianceSh9"/> coefficients exactly as the shader does.</summary>
    public static Rgb EvalSh9(Rgb[] c, Vector3d n)
    {
        double x = n.X, y = n.Y, z = n.Z;
        return c[0] + c[1] * y + c[2] * z + c[3] * x + c[4] * (x * y) + c[5] * (y * z) + c[6] * (3 * z * z - 1) + c[7] * (x * z) + c[8] * (x * x - y * y);
    }

    /// <summary>
    /// Split-sum environment BRDF: size×size RGBA floats, x = N·V (texel centres), y = roughness, r = scale, g = bias
    /// (specular = F0·r + g), b = 0, a = 1.
    /// </summary>
    public static float[] BrdfLut(int size = 32, int samples = 256)
    {
        var data = new float[size * size * 4];
        Parallel.For(0, size, y =>
        {
            double rough = Math.Max((y + 0.5) / size, 0.02), a = rough * rough;
            for (int x = 0; x < size; x++)
            {
                double nov = (x + 0.5) / size;
                var V = new Vector3d(Math.Sqrt(1 - nov * nov), 0, nov);
                double A = 0, B = 0;
                for (int i = 0; i < samples; i++)
                {
                    var (u1, u2) = Hammersley(i, samples);
                    var H = SampleGgx(u1, u2, a);
                    double voh = Vector3d.Dot(V, H);
                    double nol = 2 * voh * H.Z - V.Z;
                    if (nol <= 0) continue;
                    double noh = H.Z;
                    double g = VSmithCorrelated(nov, nol, a) * 4 * nol * voh / noh;
                    double fc = Math.Pow(1 - voh, 5);
                    A += (1 - fc) * g; B += fc * g;
                }
                int o = (y * size + x) * 4;
                data[o] = (float)(A / samples); data[o + 1] = (float)(B / samples); data[o + 2] = 0; data[o + 3] = 1;
            }
        });
        return data;
    }
}
