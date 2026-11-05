using Proto.M7.Fixed;
using Proto.M7.Rng;

namespace Proto.M7.Tests;

/// <summary>Spec §2.6 — counter-based, order-independent RNG.</summary>
public class CounterRngTests
{
    [Fact]
    public void Mixer_constants_match_the_kernel_splitmix64()
    {
        // Sim.Core/Kernel/SplitMix64.cs: GoldenGamma 0x9E3779B97F4A7C15, multipliers 0xBF58476D1CE4E5B9 / 0x94D049BB133111EB.
        Assert.Equal(0x9E3779B97F4A7C15UL, CounterRng.GoldenGamma);
        Assert.Equal(0xBF58476D1CE4E5B9UL, CounterRng.Mul1);
        Assert.Equal(0x94D049BB133111EBUL, CounterRng.Mul2);
        // Reference splitmix64 stream from state 0: first output of Next() is Mix(GoldenGamma) = 0xE220A8397B1DCDAF (Vigna).
        Assert.Equal(0xE220A8397B1DCDAFUL, CounterRng.Mix(0x9E3779B97F4A7C15UL));
    }

    public static IEnumerable<object[]> DrawGolden() => new[]
    {
        new object[] { 0UL, 0U, 0L, 0L, 0L, 0U, 0x7483EE218CCDDB67UL },
        new object[] { 42UL, 1U, 7L, 311040000000L, 3L, 0U, 0xF58A3ED807BB4310UL },
        new object[] { 42UL, 1U, 7L, 311040000000L, 3L, 1U, 0x7E7BC752EB824E78UL },
        new object[] { 42UL, 1U, -1L, -5L, 9L, 4294967295U, 0x3796E3327FDAD9B9UL },
        new object[] { 18446744073709551615UL, 4294967295U, 9223372036854775807L, -9223372036854775808L, 0L, 17U, 0xFF4660F30D17077CUL },
    };

    /// <summary>Cross-platform golden table: values computed independently in Python (scratchpad m7s1/work/B/goldens.py).</summary>
    [Theory, MemberData(nameof(DrawGolden))]
    public void Draw_golden_table(ulong seed, uint stream, long a, long b, long c, uint index, ulong expected)
        => Assert.Equal(expected, CounterRng.Draw(seed, stream, a, b, c, index));

    [Fact]
    public void Draws_are_independent_of_evaluation_order()
    {
        const int n = 10_000;
        var natural = new ulong[n];
        for (int i = 0; i < n; i++) natural[i] = CounterRng.Draw(42, 3, i % 97, (i / 97) * 1000L, 5, (uint)(i % 3));
        foreach (ulong seed in new ulong[] { 1, 2, 3 })
        {
            var shuffled = new ulong[n];
            foreach (int i in TestSupport.Permutation(n, seed))
            {
                // Interleave unrelated consumers: they cannot perturb anyone's value (no mutable stream state).
                _ = CounterRng.Draw(42, 3, -1, i, 0, 0);
                shuffled[i] = CounterRng.Draw(42, 3, i % 97, (i / 97) * 1000L, 5, (uint)(i % 3));
            }
            Assert.Equal(natural, shuffled);
        }
    }

    [Fact]
    public void Inputs_are_position_sensitive()
    {
        ulong d = CounterRng.Draw(1, 2, 3, 4, 5, 6);
        Assert.NotEqual(d, CounterRng.Draw(1, 2, 4, 3, 5, 6));
        Assert.NotEqual(d, CounterRng.Draw(2, 2, 3, 4, 5, 6));
        Assert.NotEqual(d, CounterRng.Draw(1, 3, 3, 4, 5, 6));
        Assert.NotEqual(d, CounterRng.Draw(1, 2, 3, 4, 6, 6));
        Assert.NotEqual(d, CounterRng.Draw(1, 2, 3, 4, 5, 7));
        Assert.Equal(d, CounterRng.DrawAttempt(1, 2, 3, 4, 5, 6, 0));
        Assert.NotEqual(CounterRng.DrawAttempt(1, 2, 3, 4, 5, 6, 1), CounterRng.Draw(1, 2, 3, 4, 5, 7));
    }

    [Fact]
    public void NextBelow_is_in_range_and_uniform_by_chi_square()
    {
        const int buckets = 10, samples = 100_000;
        var counts = new long[buckets];
        for (int i = 0; i < samples; i++)
        {
            ulong v = CounterRng.NextBelow(9, 1, i, 0, 0, 0, buckets);
            Assert.True(v < buckets);
            counts[v]++;
        }
        double expected = samples / (double)buckets, chi2 = 0;
        foreach (long c in counts) chi2 += (c - expected) * (c - expected) / expected;
        Assert.True(chi2 < 27.877, $"chi-square {chi2} exceeds the p=0.001 critical value for 9 dof");
        Assert.Equal(0UL, CounterRng.NextBelow(9, 1, 0, 0, 0, 0, 1));
        Assert.Throws<ArgumentOutOfRangeException>(() => CounterRng.NextBelow(9, 1, 0, 0, 0, 0, 0));
    }

    [Fact]
    public void NextBelow_rejection_path_is_exercised_and_unbiased_at_the_worst_case_modulus()
    {
        // n = 2^63 + 1: 2^64 mod n = 2^63 - 1, so almost half of raw draws are rejected — the retry path runs.
        ulong n = (1UL << 63) + 1;
        int retried = 0, high = 0;
        for (int i = 0; i < 4000; i++)
        {
            ulong raw = CounterRng.Draw(5, 1, i, 0, 0, 0);
            if (raw >= 0UL - ((0UL - n) % n)) retried++;
            ulong v = CounterRng.NextBelow(5, 1, i, 0, 0, 0, n);
            Assert.True(v < n);
            if (v >= n / 2) high++;
        }
        Assert.InRange(retried, 1700, 2300);
        Assert.InRange(high, 1800, 2200);       // range check only: at n = 2^63+1 plain x % n is ALSO nearly uniform
        // (only 2 values get half weight), so this test cannot see modulo bias. Bias is pinned at n = 3*2^62 by
        // VerificationFixTests.NextBelow_is_unbiased_at_a_three_quarter_range_modulus (verify-B F3).
    }

    [Fact]
    public void Chance_edges_and_frequency()
    {
        int hits = 0;
        for (int i = 0; i < 30_000; i++)
        {
            Assert.True(CounterRng.Chance(3, 1, i, 0, 0, 0, Q32.One));
            Assert.False(CounterRng.Chance(3, 1, i, 0, 0, 0, Q32.Zero));
            if (CounterRng.Chance(3, 2, i, 0, 0, 0, Q32.FromRatio(1, 3))) hits++;
        }
        Assert.InRange(hits, 9700, 10300);    // 1/3 of 30,000 = 10,000; sd ~ 82
        Assert.Throws<ArgumentOutOfRangeException>(() => CounterRng.Chance(3, 1, 0, 0, 0, 0, new Q32(Q32.OneRaw + 1)));
    }
}
