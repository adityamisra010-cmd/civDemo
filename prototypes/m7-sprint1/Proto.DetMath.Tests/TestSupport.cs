using System.Globalization;

namespace Proto.DetMath.Tests;

/// <summary>Reproducible input generator for tests (SplitMix64; constants of Steele, Lea, Flood 2014,
/// identical to Sim.Core/Kernel/SplitMix64.cs). Test-only: never feeds simulation state.</summary>
internal sealed class TestRng
{
    private ulong _s;
    public TestRng(ulong seed) { _s = seed; }

    public ulong Next()
    {
        unchecked
        {
            _s += 0x9E3779B97F4A7C15UL;
            ulong z = _s;
            z = (z ^ (z >> 30)) * 0xBF58476D1CE4E5B9UL;
            z = (z ^ (z >> 27)) * 0x94D049BB133111EBUL;
            return z ^ (z >> 31);
        }
    }

    /// <summary>Uniform in [0, 1) with 53 random bits — the same mapping as the campaign's NextDouble.</summary>
    public double U() => (Next() >> 11) * (1.0 / 9007199254740992.0);
    public double Lin(double a, double b) => a + (b - a) * U();
    public double LogU(double a, double b) => Math.Exp(Math.Log(a) + (Math.Log(b) - Math.Log(a)) * U());
}

internal static class Ulp
{
    /// <summary>Distance in units-in-the-last-place between two finite doubles of any sign
    /// (via the monotone integer ordering of IEEE bit patterns). Equal values (incl. +0/-0) = 0.</summary>
    public static long Distance(double a, double b)
    {
        if (a == b) return 0;
        if (double.IsNaN(a) || double.IsNaN(b)) return long.MaxValue;
        long ia = Ordered(a), ib = Ordered(b);
        return ia > ib ? ia - ib : ib - ia;
    }

    private static long Ordered(double d)
    {
        long bits = BitConverter.DoubleToInt64Bits(d);
        return bits < 0 ? long.MinValue - bits : bits;
    }

    public static bool SameBits(double a, double b) =>
        BitConverter.DoubleToInt64Bits(a) == BitConverter.DoubleToInt64Bits(b) || (double.IsNaN(a) && double.IsNaN(b));

    public static string Hex(double d) => BitConverter.DoubleToInt64Bits(d).ToString("x16", CultureInfo.InvariantCulture);
}

internal sealed record CrVector(string Fn, double X, double Y, double Cr, double Glibc);

internal static class CrVectors
{
    private static CrVector[]? _all;

    public static CrVector[] All
    {
        get
        {
            if (_all != null) return _all;
            string path = Path.Combine(AppContext.BaseDirectory, "vectors", "cr-vectors.txt");
            var list = new List<CrVector>();
            foreach (string line in File.ReadAllLines(path))
            {
                if (line.Length == 0) continue;
                string[] f = line.Split(' ');
                list.Add(new CrVector(f[0], FromHex(f[1]), FromHex(f[2]), FromHex(f[3]), FromHex(f[4])));
            }
            _all = list.ToArray();
            return _all;
        }
    }

    private static double FromHex(string h) =>
        BitConverter.Int64BitsToDouble(unchecked((long)ulong.Parse(h, NumberStyles.HexNumber, CultureInfo.InvariantCulture)));

    public static double Eval(CrVector v) => v.Fn switch
    {
        "exp" => DetMath.Exp(v.X),
        "log" => DetMath.Log(v.X),
        "pow" => DetMath.Pow(v.X, v.Y),
        "cos" => DetMath.Cos(v.X),
        _ => throw new InvalidOperationException(v.Fn),
    };

    public static double EvalLibm(CrVector v) => v.Fn switch
    {
        "exp" => Math.Exp(v.X),
        "log" => Math.Log(v.X),
        "pow" => Math.Pow(v.X, v.Y),
        "cos" => Math.Cos(v.X),
        _ => throw new InvalidOperationException(v.Fn),
    };
}

internal static class RepoPaths
{
    /// <summary>The prototypes/m7-sprint1 folder (located by walking up to M7DetMath.slnx).</summary>
    public static string SprintRoot()
    {
        string? d = AppContext.BaseDirectory;
        while (d != null)
        {
            if (File.Exists(Path.Combine(d, "M7DetMath.slnx"))) return d;
            d = Path.GetDirectoryName(d.TrimEnd(Path.DirectorySeparatorChar));
        }
        throw new InvalidOperationException("M7DetMath.slnx not found above " + AppContext.BaseDirectory);
    }
}
