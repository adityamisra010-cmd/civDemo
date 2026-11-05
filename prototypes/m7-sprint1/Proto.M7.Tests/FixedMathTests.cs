using Proto.M7.Fixed;

namespace Proto.M7.Tests;

/// <summary>Spec §2.1. Golden values were computed independently with Python arbitrary-precision integers
/// (scratchpad m7s1/work/B/goldens.py) — integer ops are platform-independent; these literals pin it.</summary>
public class FixedMathTests
{
    public static IEnumerable<object[]> MulDivGolden() => new[]
    {
        new object[] { 7L, 3L, 2L, 10L, 11L },
        new object[] { -7L, 3L, 2L, -11L, -10L },
        new object[] { 7L, -3L, 2L, -11L, -10L },
        new object[] { -7L, -3L, 2L, 10L, 11L },
        new object[] { long.MaxValue, long.MaxValue, long.MaxValue, long.MaxValue, long.MaxValue },
        new object[] { long.MaxValue, 3L, 7L, 3952873730080618203L, 3952873730080618203L },
        new object[] { long.MinValue, 1L, 3L, -3074457345618258603L, -3074457345618258602L },
        new object[] { 311040000000L, 1L, 3L, 103680000000L, 103680000000L },
        new object[] { 123456789012345L, 987654321L, 1000000007L, 121932630271299L, 121932630271300L },
        new object[] { -123456789012345L, 987654321L, 1000000007L, -121932630271300L, -121932630271299L },
        new object[] { 1L, 1L, 1L, 1L, 1L },
        new object[] { 0L, 5L, 3L, 0L, 0L },
    };

    [Theory, MemberData(nameof(MulDivGolden))]
    public void MulDiv_golden_table(long a, long b, long c, long floor, long ceil)
    {
        Assert.Equal(floor, FixedMath.MulDivFloor(a, b, c));
        Assert.Equal(ceil, FixedMath.MulDivCeil(a, b, c));
    }

    [Fact]
    public void MulDiv_satisfies_the_defining_floor_and_ceil_inequalities()
    {
        for (int i = 0; i < 20000; i++)
        {
            long a = (long)Rng.CounterRng.Draw(4, 1, i, 0, 0, 0) >> (int)Rng.CounterRng.NextBelow(4, 4, i, 0, 0, 0, 40);
            long b = (long)Rng.CounterRng.Draw(4, 2, i, 0, 0, 0) >> 40;
            long c = (1L << 24) + (long)Rng.CounterRng.NextBelow(4, 3, i, 0, 0, 0, 1_000_000_007); // |a*b| < 2^86, c >= 2^24: result fits
            Int128 p = (Int128)a * b;
            Int128 f = FixedMath.MulDivFloor(a, b, c), g = FixedMath.MulDivCeil(a, b, c);
            Assert.True(f * c <= p && p < (f + 1) * c, $"floor({a}*{b}/{c})");
            Assert.True(g * c >= p && p > (g - 1) * c, $"ceil({a}*{b}/{c})");
            Assert.True(g - f == (p % c == 0 ? 0 : 1));
        }
    }

    [Fact]
    public void MulDiv_rejects_nonpositive_divisor_and_overflowing_result()
    {
        Assert.Throws<ArgumentOutOfRangeException>(() => FixedMath.MulDivFloor(1, 1, 0));
        Assert.Throws<ArgumentOutOfRangeException>(() => FixedMath.MulDivCeil(1, 1, -1));
        Assert.Throws<OverflowException>(() => FixedMath.MulDivFloor(long.MaxValue, 2, 1));
        Assert.Throws<OverflowException>(() => FixedMath.MulDivCeil(long.MinValue, 2, 1));
    }

    [Fact]
    public void DivFloor_DivCeil_match_mathematical_definition_for_all_sign_combinations()
    {
        for (long a = -60; a <= 60; a++)
        for (long b = -9; b <= 9; b++)
        {
            if (b == 0) continue;
            long f = FixedMath.DivFloor(a, b), c = FixedMath.DivCeil(a, b);
            // floor: largest q with q*b <= a (b>0) / q*b >= a (b<0); verified by the defining inequalities.
            if (b > 0) { Assert.True(f * b <= a && a < (f + 1) * b, $"floor {a}/{b}"); Assert.True(c * b >= a && a > (c - 1) * b, $"ceil {a}/{b}"); }
            else { Assert.True(f * b >= a && a > (f + 1) * b, $"floor {a}/{b}"); Assert.True(c * b <= a && a < (c - 1) * b, $"ceil {a}/{b}"); }
            Assert.Equal(a - b * f, FixedMath.ModFloor(a, b));
        }
        Assert.Equal(-4, FixedMath.DivFloor(-7, 2));
        Assert.Equal(-3, FixedMath.DivCeil(-7, 2));
        Assert.Throws<DivideByZeroException>(() => FixedMath.DivFloor(1, 0));
        Assert.Throws<OverflowException>(() => FixedMath.DivFloor(long.MinValue, -1));
    }

    public static IEnumerable<object[]> ISqrt64Golden() => new[]
    {
        new object[] { 0UL, 0UL }, new object[] { 1UL, 1UL }, new object[] { 2UL, 1UL }, new object[] { 3UL, 1UL },
        new object[] { 4UL, 2UL }, new object[] { 15UL, 3UL }, new object[] { 16UL, 4UL }, new object[] { 17UL, 4UL },
        new object[] { 99UL, 9UL }, new object[] { 100UL, 10UL }, new object[] { 101UL, 10UL },
        new object[] { 4294967295UL, 65535UL }, new object[] { 4294967296UL, 65536UL },
        new object[] { 4611686018427387904UL, 2147483648UL },
        new object[] { 18446744073709551615UL, 4294967295UL }, new object[] { 18446744073709551614UL, 4294967295UL },
        new object[] { 18446744065119617025UL, 4294967295UL }, new object[] { 18446744065119617024UL, 4294967294UL },
        new object[] { 12345678901234567890UL, 3513641828UL },
    };

    [Theory, MemberData(nameof(ISqrt64Golden))]
    public void ISqrt_ulong_golden_table(ulong n, ulong root) => Assert.Equal(root, FixedMath.ISqrt(n));

    public static IEnumerable<object[]> ISqrt128Golden() => new[]
    {
        new object[] { 18446744073709551615UL, 18446744073709551615UL, 18446744073709551615UL },
        new object[] { 9223372036854775808UL, 0UL, 13043817825332782212UL },
        new object[] { 18446744073709551614UL, 1UL, 18446744073709551615UL },
        new object[] { 18446744073709551614UL, 0UL, 18446744073709551614UL },
        new object[] { 5421010862427522170UL, 687399551400673280UL, 10000000000000000000UL },
        new object[] { 1UL, 0UL, 4294967296UL },
        new object[] { 1UL, 1UL, 4294967296UL },
    };

    [Theory, MemberData(nameof(ISqrt128Golden))]
    public void ISqrt_uint128_golden_table(ulong hi, ulong lo, ulong root) => Assert.Equal(root, FixedMath.ISqrt(new UInt128(hi, lo)));

    [Fact]
    public void ISqrt_floor_property_at_perfect_squares_plus_minus_one()
    {
        for (int i = 0; i < 20000; i++)
        {
            ulong k = Rng.CounterRng.NextBelow(7, 1, i, 0, 0, 0, 1UL << 32); // k < 2^32 so k*k fits ulong
            UInt128 sq = (UInt128)k * k;
            foreach (UInt128 n in new[] { sq == 0 ? sq : sq - 1, sq, sq + 1 })
            {
                if (n > ulong.MaxValue) continue;
                ulong r = FixedMath.ISqrt((ulong)n);
                Assert.True((UInt128)r * r <= n && n < (UInt128)(r + 1) * (r + 1), $"floor property fails at {n}");
            }
            ulong r2 = FixedMath.ISqrt(sq);
            Assert.Equal(k, r2);
            if (k > 0) Assert.Equal(k - 1, FixedMath.ISqrt((ulong)(sq - 1)));
        }
        // ulong.MaxValue: r = 2^32-1, (r+1)^2 = 2^64 > MaxValue (checked in 128 bits).
        ulong m = FixedMath.ISqrt(ulong.MaxValue);
        Assert.True((UInt128)m * m <= ulong.MaxValue && (UInt128)(m + 1) * (m + 1) > ulong.MaxValue);
        // 128-bit: random wide values.
        for (int i = 0; i < 5000; i++)
        {
            var n = new UInt128(Rng.CounterRng.Draw(8, 2, i, 0, 0, 0), Rng.CounterRng.Draw(8, 3, i, 0, 0, 0));
            ulong r = FixedMath.ISqrt(n);
            Assert.True((UInt128)r * r <= n);
            if (r < ulong.MaxValue) Assert.True((UInt128)(r + 1) * (r + 1) > n);
        }
    }

    [Fact]
    public void Q32_golden_table_and_semantics()
    {
        var table = new (long num, long den, long raw)[]
        {
            (1, 3, 1431655765L), (2, 3, 2863311530L), (1, 2, 2147483648L), (1, 1, 4294967296L),
            (0, 1, 0L), (7, 10, 3006477107L), (999999, 1000000, 4294963001L),
        };
        foreach (var (n, d, raw) in table) Assert.Equal(raw, Q32.FromRatio(n, d).Raw);
        var third = Q32.FromRatio(1, 3);
        Assert.Equal(954437176L, third.Mul(Q32.FromRatio(2, 3)).Raw);
        Assert.Equal(333L, third.Scale(1000));
        Assert.Equal(334L, third.ScaleCeil(1000));
        Assert.Equal(Q32.OneRaw, Q32.One.Raw);
        Assert.Equal(Q32.OneRaw - third.Raw, third.Complement().Raw);
        Assert.Equal(1000L, Q32.One.Scale(1000));               // 1.0 is exactly representable (the reason for the long raw)
        Assert.Throws<ArgumentOutOfRangeException>(() => Q32.Probability(4, 3));
        Assert.True(Q32.FromRatio(1, 3) < Q32.FromRatio(1, 2));
    }
}
