namespace Gfx.Proc.Authoritative;

/// <summary>
/// Integer-only hashing and value noise (Q16 fixed point). SplitMix64 finaliser as the lattice hash; smoothstep
/// interpolation in Q16; fBm by octave summation with integer weights. No floating point anywhere (scanned).
/// </summary>
public static class IntNoise
{
    public const int One = 65536;

    public static ulong SplitMix64(ulong x)
    {
        unchecked
        {
            x += 0x9E3779B97F4A7C15UL;
            x = (x ^ (x >> 30)) * 0xBF58476D1CE4E5B9UL;
            x = (x ^ (x >> 27)) * 0x94D049BB133111EBUL;
            return x ^ (x >> 31);
        }
    }

    public static ulong Hash(ulong seed, int x, int y) =>
        SplitMix64(seed ^ SplitMix64(unchecked(((ulong)(uint)x << 32) | (uint)y)));

    /// <summary>Lattice value in [0, 65535].</summary>
    public static int Lattice(ulong seed, int x, int y) => (int)(Hash(seed, x, y) >> 48);

    /// <summary>Uniform integer in [0, n).</summary>
    public static int Below(ulong h, int n) => (int)((h >> 33) % (ulong)n);

    public static int FloorDiv(int a, int b) { int q = a / b; if ((a % b != 0) && ((a < 0) != (b < 0))) q--; return q; }

    /// <summary>Smoothstep 3t²−2t³ for t in Q16 [0, 65536].</summary>
    public static long Smooth(long t) => ((t * t) >> 16) * (3L * One - 2 * t) >> 16;

    /// <summary>Value noise at integer sample (x, y) with lattice spacing <paramref name="period"/> samples; [0, 65535].</summary>
    public static int Value(ulong seed, int x, int y, int period)
    {
        int gx = FloorDiv(x, period), gy = FloorDiv(y, period);
        long fx = ((long)(x - gx * period) << 16) / period, fy = ((long)(y - gy * period) << 16) / period;
        long sx = Smooth(fx), sy = Smooth(fy);
        long a = Lattice(seed, gx, gy), b = Lattice(seed, gx + 1, gy), c = Lattice(seed, gx, gy + 1), d = Lattice(seed, gx + 1, gy + 1);
        long top = a + (((b - a) * sx) >> 16), bot = c + (((d - c) * sx) >> 16);
        return (int)(top + (((bot - top) * sy) >> 16));
    }

    /// <summary>fBm: octaves of <see cref="Value"/>, period halving and weight halving per octave; [0, 65535] centred on 32768.</summary>
    public static int Fbm(ulong seed, int x, int y, int basePeriod, int octaves)
    {
        long total = 0, norm = 0, w = 1024; int period = basePeriod;
        for (int o = 0; o < octaves; o++)
        {
            total += (Value(SplitMix64(seed + (ulong)o), x, y, period) - 32768L) * w;
            norm += w; w >>= 1; period = Math.Max(1, period >> 1);
        }
        return (int)Math.Clamp(total / norm + 32768, 0, 65535);
    }

    /// <summary>Ridged transform of fBm (sharp crests where fBm crosses its mean); [0, 65535].</summary>
    public static int Ridged(ulong seed, int x, int y, int basePeriod, int octaves)
    {
        int v = Fbm(seed, x, y, basePeriod, octaves);
        return Math.Clamp(65535 - 4 * Math.Abs(v - 32768), 0, 65535);
    }
}
