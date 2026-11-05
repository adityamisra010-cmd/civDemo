using System.Text.RegularExpressions;
using Xunit.Abstractions;

namespace Proto.DetMath.Tests;

/// <summary>
/// Platform-independent correctness pins for DetMath. Every assertion here compares against either
/// (a) a correctly-rounded value from an arbitrary-precision oracle, stored as bit patterns in
///     vectors/cr-vectors.txt (generator: vectors/gen-cr-vectors.py, Python decimal at 70-130 digits), or
/// (b) an IEEE-754 correctly-rounded BASIC operation (x*x, 1/x, sqrt x), or
/// (c) a mathematical identity / special value fixed by C99 Annex F.
/// None of them depends on the host libm, so the same tests are the per-function Windows/Linux
/// equality check once they run on a Windows runner (UNTESTED in this sprint).
/// </summary>
public sealed class DetMathCorrectnessTests
{
    private readonly ITestOutputHelper _out;
    public DetMathCorrectnessTests(ITestOutputHelper output) { _out = output; }

    [Theory]
    [InlineData("exp")]
    [InlineData("log")]
    [InlineData("pow")]
    [InlineData("cos")]
    public void CorrectlyRoundedVectors_AreReproducedBitForBit(string fn)
    {
        var vectors = CrVectors.All.Where(v => v.Fn == fn).ToArray();
        Assert.True(vectors.Length > 1000, $"too few {fn} vectors: {vectors.Length}");
        int mismatches = 0;
        var first = new List<string>();
        foreach (var v in vectors)
        {
            double got = CrVectors.Eval(v);
            if (!Ulp.SameBits(got, v.Cr))
            {
                mismatches++;
                if (first.Count < 10)
                    first.Add($"{fn}({v.X:R}, {v.Y:R}) = {got:R} [{Ulp.Hex(got)}], CR {v.Cr:R} [{Ulp.Hex(v.Cr)}]");
            }
        }
        _out.WriteLine($"{fn}: {vectors.Length} CR vectors, {mismatches} mismatches");
        foreach (string s in first) _out.WriteLine(s);
        Assert.True(mismatches == 0, string.Join("\n", first));
    }

    /// <summary>Records (does not require) how often the HOST libm is correctly rounded on the same vectors,
    /// and that the host Math.* equals the glibc column the generator recorded (same libm on Linux).</summary>
    [Fact]
    public void HostLibm_AgainstCorrectlyRounded_IsRecorded()
    {
        foreach (string fn in new[] { "exp", "log", "pow", "cos" })
        {
            var vectors = CrVectors.All.Where(v => v.Fn == fn).ToArray();
            int hostCr = 0, hostEqGlibcColumn = 0;
            foreach (var v in vectors)
            {
                double h = CrVectors.EvalLibm(v);
                if (Ulp.SameBits(h, v.Cr)) hostCr++;
                if (Ulp.SameBits(h, v.Glibc)) hostEqGlibcColumn++;
            }
            _out.WriteLine($"{fn}: host Math.* correctly rounded on {hostCr}/{vectors.Length}; equal to recorded glibc column on {hostEqGlibcColumn}/{vectors.Length} (OS {Environment.OSVersion.Platform})");
            if (OperatingSystem.IsLinux()) Assert.Equal(vectors.Length, hostEqGlibcColumn);
        }
    }

    public static IEnumerable<object[]> SpecialArgs()
    {
        double[] xs =
        {
            0.0, -0.0, 1.0, -1.0, 2.0, -2.0, 0.5, -0.5, 3.0, -3.0, double.PositiveInfinity, double.NegativeInfinity,
            double.NaN, double.Epsilon, -double.Epsilon, double.MaxValue, -double.MaxValue, 1e-310, 709.79, 710.0,
            -746.0, -745.0, 1.0000000000000002, 0.9999999999999999, 268435456.0, 268435457.0,
        };
        foreach (double x in xs) yield return new object[] { x };
    }

    [Theory]
    [MemberData(nameof(SpecialArgs))]
    public void UnaryFunctions_SpecialValues_MatchIeeeSemantics(double x)
    {
        AssertSame(Math.Exp(x), DetMath.Exp(x), $"Exp({x:R})");
        AssertSame(Math.Log(x), DetMath.Log(x), $"Log({x:R})");
        if (Math.Abs(x) <= 268435456.0 || double.IsNaN(x) || double.IsInfinity(x))
            AssertSame(Math.Cos(x), DetMath.Cos(x), $"Cos({x:R})");
    }

    [Fact]
    public void Pow_SpecialValueGrid_MatchesIeeeSemantics()
    {
        double[] v =
        {
            0.0, -0.0, 1.0, -1.0, 2.0, -2.0, 0.5, -0.5, 3.0, -3.0, 2.5, -2.5, double.PositiveInfinity,
            double.NegativeInfinity, double.NaN, double.Epsilon, double.MaxValue, 1e300, -1e300, 9007199254740992.0,
            -9007199254740992.0, 1e20, -1e20,
        };
        int checkedCount = 0;
        foreach (double x in v)
        foreach (double y in v)
        {
            AssertSame(Math.Pow(x, y), DetMath.Pow(x, y), $"Pow({x:R}, {y:R})");
            checkedCount++;
        }
        _out.WriteLine($"{checkedCount} special (x, y) pairs bit-identical to Math.Pow");
    }

    [Fact]
    public void Pow_BasicOperationIdentities_AreExact()
    {
        // A correctly rounded pow must equal the correctly rounded IEEE basic operation:
        // x^2 = fl(x*x), x^-1 = fl(1/x), x^0.5 = fl(sqrt x), x^1 = x. Semantic, oracle-free.
        var rng = new TestRng(2026_11_04);
        for (int i = 0; i < 200_000; i++)
        {
            double x = rng.LogU(1e-150, 1e150);
            Assert.Equal(x * x, DetMath.Pow(x, 2.0));
            Assert.Equal(1.0 / x, DetMath.Pow(x, -1.0));
            Assert.Equal(Math.Sqrt(x), DetMath.Pow(x, 0.5));
            Assert.Equal(x, DetMath.Pow(x, 1.0));
            Assert.Equal(-DetMath.Pow(x, 3.0), DetMath.Pow(-x, 3.0));   // odd integer exponent keeps the sign
            Assert.Equal(DetMath.Pow(x, 2.0), DetMath.Pow(-x, 2.0));    // even integer exponent drops it
        }
    }

    [Fact]
    public void Pow_ExactMidpointResults_TieDense_RoundToEven()
    {
        // Odd x in (2^26.5, 2^27): x^2 is an odd integer in [2^53, 2^54) — EXACTLY halfway between two
        // doubles. Ties-to-even is the only correct answer and equals fl(x*x). Likewise x^3 for odd x with
        // x^3 in [2^53, 2^54) and (2^52+1)^0.5, which lies 2^-108 (relative) below a midpoint.
        int ties = 0;
        for (long x = 94906267; x < 134217728; x += 2 * 997)
        {
            double xd = x;
            Assert.Equal(xd * xd, DetMath.Pow(xd, 2.0));
            ties++;
        }
        for (long x = 208064; x <= 262143; x++)
        {
            long cube = x * x * x;
            if (cube < (1L << 53) || cube >= (1L << 54) || (x & 1) == 0) continue;
            double expected = (double)cube;                      // exact integer -> correctly rounded conversion
            Assert.Equal(expected, DetMath.Pow(x, 3.0));
            ties++;
        }
        Assert.Equal(67108864.0, DetMath.Pow(4503599627370497.0, 0.5));
        // (2^52+1)^2.5 = 2^130 (1 + 2.5 eps + 1.875 eps^2 ...): just ABOVE the midpoint, so the correctly
        // rounded result is 2^130 (1 + 3 eps) = 0x4810000000000003 (decimal oracle, 80 digits). glibc 2.39
        // returns ...02 here (measured on this host): a recorded case where the host libm is not correctly rounded.
        Assert.Equal(0x4810000000000003L, BitConverter.DoubleToInt64Bits(DetMath.Pow(4503599627370497.0, 2.5)));
        Assert.True(ties > 20_000, $"only {ties} tie cases");
    }

    [Fact]
    public void ExactPoints_AreExact()
    {
        Assert.Equal(1.0, DetMath.Exp(0.0));
        Assert.Equal(1.0, DetMath.Exp(-0.0));
        Assert.Equal(0.0, DetMath.Log(1.0));
        Assert.Equal(1.0, DetMath.Cos(0.0));
        // log(2^k) = k ln2 correctly rounded; k ln2 in double-double is exact enough that the
        // product of k with the correctly rounded ln2 differs only in the last bit -> compare to the vectors' oracle value.
        Assert.Equal(0.6931471805599453, DetMath.Log(2.0));
        Assert.Equal(-0.6931471805599453, DetMath.Log(0.5));
        Assert.Equal(2.718281828459045, DetMath.Exp(1.0));
        Assert.Equal(1024.0, DetMath.Pow(2.0, 10.0));
        Assert.Equal(1.0 / 1024.0, DetMath.Pow(2.0, -10.0));
        Assert.Equal(double.Epsilon, DetMath.Pow(2.0, -1074.0));
        Assert.Equal(0.0, DetMath.Pow(2.0, -1076.0));
        Assert.Equal(double.Epsilon, DetMath.Exp(-744.44007192138122)); // ~ 1.0000000000000002 * 2^-1074 region
        Assert.Equal(-1.0, DetMath.Cos(Math.PI));
    }

    [Fact]
    public void Cos_IsEven_And_ThrowsOutsideDocumentedDomain()
    {
        var rng = new TestRng(11);
        for (int i = 0; i < 100_000; i++)
        {
            double x = rng.Lin(-1e6, 1e6);
            Assert.Equal(DetMath.Cos(x), DetMath.Cos(-x));
        }
        Assert.Throws<ArgumentOutOfRangeException>(() => DetMath.Cos(268435457.0));
        Assert.Throws<ArgumentOutOfRangeException>(() => DetMath.Cos(-1e300));
    }

    [Fact]
    public void ExpAndLog_AreMonotone_OverConsecutiveDoubles()
    {
        // A correctly rounded function of a monotone function is monotone (non-strict).
        double[] starts = { -745.0, -700.0, -50.0, -1.0, -1e-9, 0.0, 1e-9, 0.34657359027997264, 1.0, 50.0, 700.0 };
        foreach (double s in starts)
        {
            double x = s, prev = DetMath.Exp(x);
            for (int i = 0; i < 20_000; i++)
            {
                x = Math.BitIncrement(x);
                double e = DetMath.Exp(x);
                Assert.True(e >= prev, $"Exp not monotone at {x:R}");
                prev = e;
            }
        }
        double[] logStarts = { double.Epsilon, 1e-300, 0.7071067811865475, 0.9999999, 1.0, 1.4142135623730951, 1e8 };
        foreach (double s in logStarts)
        {
            double x = s, prev = DetMath.Log(x);
            for (int i = 0; i < 20_000; i++)
            {
                x = Math.BitIncrement(x);
                double l = DetMath.Log(x);
                Assert.True(l >= prev, $"Log not monotone at {x:R}");
                prev = l;
            }
        }
    }

    [Fact]
    public void SourceIsLibmFree_NoMathCallsNoFloatNoFma()
    {
        // The library may use ONLY IEEE basic operations (incl. the correctly rounded Math.Sqrt) and
        // BitConverter bit casts (spec §7).
        string dir = Path.Combine(RepoPaths.SprintRoot(), "Proto.DetMath");
        var banned = new Regex(@"\bMath\.(?!Sqrt\b)|\bMathF\b|\bfloat\b|FusedMultiplyAdd|System\.Runtime\.Intrinsics|\bdecimal\b|\bRandom\b|DateTime");
        int files = 0;
        foreach (string path in Directory.GetFiles(dir, "*.cs", SearchOption.AllDirectories))
        {
            if (path.Contains($"{Path.DirectorySeparatorChar}obj{Path.DirectorySeparatorChar}") ||
                path.Contains($"{Path.DirectorySeparatorChar}bin{Path.DirectorySeparatorChar}")) continue;
            files++;
            string[] lines = File.ReadAllLines(path);
            for (int i = 0; i < lines.Length; i++)
            {
                string code = lines[i];
                int c = code.IndexOf("//", StringComparison.Ordinal);
                if (c >= 0) code = code.Substring(0, c);
                Assert.False(banned.IsMatch(code), $"{Path.GetFileName(path)}:{i + 1}: {lines[i].Trim()}");
            }
        }
        Assert.True(files >= 2);
    }

    private static void AssertSame(double expected, double actual, string what)
    {
        Assert.True(Ulp.SameBits(expected, actual),
            $"{what}: expected {expected:R} [{Ulp.Hex(expected)}], DetMath {actual:R} [{Ulp.Hex(actual)}]");
    }
}
