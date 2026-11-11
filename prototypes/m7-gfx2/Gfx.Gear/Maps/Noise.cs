namespace Gfx.Gear.Maps;

/// <summary>
/// Deterministic noise for the map painters: an integer hash (no System.Random, no floating-point hashing), value
/// noise and gradient noise with quintic fades, fBm, ridged noise, and cellular (Worley) noise returning the nearest
/// cell's id and the two nearest distances. Pure functions of (coordinates, seed): the same texel always gets the same
/// value whatever order or thread computes it. PRESENTATION ONLY.
/// </summary>
public static class Noise
{
    /// <summary>32-bit integer hash of up to four ints (a lowbias32-style mixer).</summary>
    public static uint Hash(int x, int y = 0, int z = 0, uint seed = 0)
    {
        uint h = seed * 0x9E3779B9u + 0x7F4A7C15u;
        h ^= (uint)x * 0x85EBCA6Bu; h = Mix(h);
        h ^= (uint)y * 0xC2B2AE35u; h = Mix(h);
        h ^= (uint)z * 0x27D4EB2Fu; h = Mix(h);
        return h;
    }

    static uint Mix(uint x)
    {
        x ^= x >> 16; x *= 0x7FEB352Du;
        x ^= x >> 15; x *= 0x846CA68Bu;
        x ^= x >> 16;
        return x;
    }

    /// <summary>Uniform double in [0, 1) from a hash.</summary>
    public static double U01(uint h) => (h >> 8) * (1.0 / 16777216.0);
    public static double Rand(int x, int y = 0, int z = 0, uint seed = 0) => U01(Hash(x, y, z, seed));
    /// <summary>Uniform in [-1, 1).</summary>
    public static double Rand11(int x, int y = 0, int z = 0, uint seed = 0) => 2 * Rand(x, y, z, seed) - 1;

    static double Fade(double t) => t * t * t * (t * (t * 6 - 15) + 10);
    static int Fl(double v) => (int)Math.Floor(v);

    /// <summary>2D value noise in [0, 1].</summary>
    public static double Value(double x, double y, uint seed)
    {
        int ix = Fl(x), iy = Fl(y); double fx = x - ix, fy = y - iy;
        double a = Rand(ix, iy, 0, seed), b = Rand(ix + 1, iy, 0, seed), c = Rand(ix, iy + 1, 0, seed), d = Rand(ix + 1, iy + 1, 0, seed);
        double u = Fade(fx), v = Fade(fy);
        return a + (b - a) * u + (c - a) * v + (a - b - c + d) * u * v;
    }

    /// <summary>2D value noise that tiles with period (px, py) cells.</summary>
    public static double ValueTiled(double x, double y, int px, int py, uint seed)
    {
        int ix = Fl(x), iy = Fl(y); double fx = x - ix, fy = y - iy;
        int x0 = Mod(ix, px), x1 = Mod(ix + 1, px), y0 = Mod(iy, py), y1 = Mod(iy + 1, py);
        double a = Rand(x0, y0, 0, seed), b = Rand(x1, y0, 0, seed), c = Rand(x0, y1, 0, seed), d = Rand(x1, y1, 0, seed);
        double u = Fade(fx), v = Fade(fy);
        return a + (b - a) * u + (c - a) * v + (a - b - c + d) * u * v;
    }

    public static int Mod(int a, int m) { int r = a % m; return r < 0 ? r + m : r; }

    static double Grad(uint h, double x, double y)
    {
        // 8 gradient directions
        switch (h & 7)
        {
            case 0: return x + y; case 1: return x - y; case 2: return -x + y; case 3: return -x - y;
            case 4: return x; case 5: return -x; case 6: return y; default: return -y;
        }
    }

    /// <summary>2D gradient (Perlin-style) noise, roughly in [-1, 1].</summary>
    public static double Perlin(double x, double y, uint seed)
    {
        int ix = Fl(x), iy = Fl(y); double fx = x - ix, fy = y - iy;
        double n00 = Grad(Hash(ix, iy, 0, seed), fx, fy), n10 = Grad(Hash(ix + 1, iy, 0, seed), fx - 1, fy);
        double n01 = Grad(Hash(ix, iy + 1, 0, seed), fx, fy - 1), n11 = Grad(Hash(ix + 1, iy + 1, 0, seed), fx - 1, fy - 1);
        double u = Fade(fx), v = Fade(fy);
        double nx0 = n00 + (n10 - n00) * u, nx1 = n01 + (n11 - n01) * u;
        return (nx0 + (nx1 - nx0) * v) * 0.7071;
    }

    /// <summary>3D gradient noise (for solid textures in item space), roughly in [-1, 1].</summary>
    public static double Perlin3(double x, double y, double z, uint seed)
    {
        int ix = Fl(x), iy = Fl(y), iz = Fl(z); double fx = x - ix, fy = y - iy, fz = z - iz;
        double G(int a, int b, int c, double dx, double dy, double dz)
        {
            uint h = Hash(a, b, c, seed) % 12;
            return h switch
            {
                0 => dx + dy, 1 => -dx + dy, 2 => dx - dy, 3 => -dx - dy,
                4 => dx + dz, 5 => -dx + dz, 6 => dx - dz, 7 => -dx - dz,
                8 => dy + dz, 9 => -dy + dz, 10 => dy - dz, _ => -dy - dz,
            };
        }
        double u = Fade(fx), v = Fade(fy), w = Fade(fz);
        double x00 = G(ix, iy, iz, fx, fy, fz) + (G(ix + 1, iy, iz, fx - 1, fy, fz) - G(ix, iy, iz, fx, fy, fz)) * u;
        double x10 = G(ix, iy + 1, iz, fx, fy - 1, fz) + (G(ix + 1, iy + 1, iz, fx - 1, fy - 1, fz) - G(ix, iy + 1, iz, fx, fy - 1, fz)) * u;
        double x01 = G(ix, iy, iz + 1, fx, fy, fz - 1) + (G(ix + 1, iy, iz + 1, fx - 1, fy, fz - 1) - G(ix, iy, iz + 1, fx, fy, fz - 1)) * u;
        double x11 = G(ix, iy + 1, iz + 1, fx, fy - 1, fz - 1) + (G(ix + 1, iy + 1, iz + 1, fx - 1, fy - 1, fz - 1) - G(ix, iy + 1, iz + 1, fx, fy - 1, fz - 1)) * u;
        double y0 = x00 + (x10 - x00) * v, y1 = x01 + (x11 - x01) * v;
        return (y0 + (y1 - y0) * w) * 0.75;
    }

    /// <summary>fBm of gradient noise mapped to [0, 1] (approximately), <paramref name="octaves"/> octaves.</summary>
    public static double Fbm(double x, double y, int octaves, uint seed, double lacunarity = 2.0, double gain = 0.5)
    {
        double sum = 0, amp = 0.5, norm = 0, f = 1;
        for (int o = 0; o < octaves; o++)
        {
            sum += amp * Perlin(x * f, y * f, seed + (uint)o * 1013u);
            norm += amp; amp *= gain; f *= lacunarity;
        }
        return Math.Clamp(0.5 + 0.5 * sum / norm * 1.6, 0, 1);
    }

    public static double Fbm3(double x, double y, double z, int octaves, uint seed, double lacunarity = 2.0, double gain = 0.5)
    {
        double sum = 0, amp = 0.5, norm = 0, f = 1;
        for (int o = 0; o < octaves; o++)
        {
            sum += amp * Perlin3(x * f, y * f, z * f, seed + (uint)o * 1013u);
            norm += amp; amp *= gain; f *= lacunarity;
        }
        return Math.Clamp(0.5 + 0.5 * sum / norm * 1.6, 0, 1);
    }

    /// <summary>Ridged fBm in [0, 1] (sharp creases: scratches, wood figure).</summary>
    public static double Ridged(double x, double y, int octaves, uint seed)
    {
        double sum = 0, amp = 0.5, norm = 0, f = 1;
        for (int o = 0; o < octaves; o++)
        {
            double n = 1 - Math.Abs(Perlin(x * f, y * f, seed + (uint)o * 7919u));
            sum += amp * n * n; norm += amp; amp *= 0.5; f *= 2.0;
        }
        return sum / norm;
    }

    /// <summary>
    /// Cellular noise: the nearest feature point (one jittered point per cell, jitter in [0, jitter]) gives the cell id
    /// hash and distance f1; f2 is the second nearest. Distances in cell units.
    /// </summary>
    public static (double f1, double f2, uint id, double cx, double cy) Worley(double x, double y, uint seed, double jitter = 1.0)
    {
        int ix = Fl(x), iy = Fl(y);
        double f1 = 9, f2 = 9; uint id = 0; double cx = 0, cy = 0;
        for (int j = -1; j <= 1; j++)
            for (int i = -1; i <= 1; i++)
            {
                int gx = ix + i, gy = iy + j;
                uint h = Hash(gx, gy, 17, seed);
                double px = gx + 0.5 + (U01(h) - 0.5) * jitter, py = gy + 0.5 + (U01(Mix(h ^ 0x5bd1e995u)) - 0.5) * jitter;
                double d = Math.Sqrt((px - x) * (px - x) + (py - y) * (py - y));
                if (d < f1) { f2 = f1; f1 = d; id = h; cx = px; cy = py; }
                else if (d < f2) f2 = d;
            }
        return (f1, f2, id, cx, cy);
    }

    public static double Smoothstep(double e0, double e1, double x)
    {
        double t = Math.Clamp((x - e0) / (e1 - e0), 0, 1);
        return t * t * (3 - 2 * t);
    }

    public static double Lerp(double a, double b, double t) => a + (b - a) * t;
    public static double Saturate(double v) => v < 0 ? 0 : v > 1 ? 1 : v;
}
