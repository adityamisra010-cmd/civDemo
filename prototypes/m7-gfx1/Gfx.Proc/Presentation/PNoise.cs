namespace Gfx.Proc.Presentation;

/// <summary>
/// PRESENTATION noise (float). Seeded integer hash → lattice values, so results are reproducible per seed, but this
/// code is visual only and never feeds the authoritative grid. Optional lattice period makes it tileable.
/// </summary>
public static class PNoise
{
    public static uint Hash(uint x)
    {
        unchecked { x ^= x >> 16; x *= 0x7feb352d; x ^= x >> 15; x *= 0x846ca68b; x ^= x >> 16; return x; }
    }
    public static uint Hash(int x, int y, uint seed) => Hash(unchecked((uint)x * 0x8da6b343u ^ (uint)y * 0xd8163841u ^ seed * 0xcb1ab31fu));
    /// <summary>Uniform [0,1) from a hash.</summary>
    public static float U01(uint h) => (h & 0xFFFFFF) / 16777216f;

    static float Lat(int x, int y, int period, uint seed)
    {
        if (period > 0) { x = ((x % period) + period) % period; y = ((y % period) + period) % period; }
        return U01(Hash(x, y, seed));
    }

    /// <summary>Value noise [0,1] at lattice coordinates (u, v); tileable with the given lattice period (0 = none).</summary>
    public static float Value(float u, float v, uint seed, int period = 0)
    {
        int x = (int)MathF.Floor(u), y = (int)MathF.Floor(v);
        float fx = u - x, fy = v - y;
        fx = fx * fx * (3 - 2 * fx); fy = fy * fy * (3 - 2 * fy);
        float a = Lat(x, y, period, seed), b = Lat(x + 1, y, period, seed), c = Lat(x, y + 1, period, seed), d = Lat(x + 1, y + 1, period, seed);
        return a + (b - a) * fx + (c - a) * fy + (a - b - c + d) * fx * fy;
    }

    /// <summary>fBm in [0,1]; with period &gt; 0 each octave doubles the period, so the sum stays tileable.</summary>
    public static float Fbm(float u, float v, uint seed, int octaves, int period = 0)
    {
        float sum = 0, amp = 1, norm = 0;
        for (int o = 0; o < octaves; o++)
        {
            sum += amp * Value(u, v, seed + (uint)o * 101u, period);
            norm += amp; amp *= 0.5f; u *= 2; v *= 2; if (period > 0) period *= 2;
        }
        return sum / norm;
    }
}
