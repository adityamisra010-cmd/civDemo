using Proto.M7.Fixed;

namespace Proto.M7.Rng;

/// <summary>
/// Counter-based, order-independent RNG (spec §2.6; O-6 RNG-consumption mechanism).
///
/// A draw is a PURE FUNCTION of (seed, stream, a, b, c, index): there is no mutable stream state, so the
/// value an event receives depends only on WHO draws for WHAT (e.g. a = entity id, b = instant ms,
/// c = purpose, index = k-th draw for that purpose), never on how many draws other events made before it.
/// Reordering or adding unrelated events cannot change anyone's draws.
///
/// Mixer: the SplitMix64 finalizer with Vigna's reference constants, COPIED from
/// Sim.Core/Kernel/SplitMix64.cs (GoldenGamma 0x9E3779B97F4A7C15, multipliers 0xBF58476D1CE4E5B9 and
/// 0x94D049BB133111EB, shifts 30/27/31). Fold: h0 = Mix(seed + G); for the i-th input word w_i
/// (i = 1..5: stream, a, b, c, index): h_i = Mix(h_{i-1} ^ Mix(w_i + (i+1)*G)). The per-position offset (i+1)*G
/// keeps (a=x, b=y) and (a=y, b=x) distinct. All arithmetic is mod 2^64 (this file is the ONLY place in
/// Proto.M7 allowed to use unchecked arithmetic — spec §1 allowlist by filename).
///
/// Integration note (report, not implemented): campaign randomness must flow through RngRegistry with state in
/// WorldState; the path is a battle seed DERIVED from a RngRegistry stream, then counter draws keyed as above.
/// That needs O-14 + kernel sign-off.
/// </summary>
public static class CounterRng
{
    public const ulong GoldenGamma = 0x9E3779B97F4A7C15UL;
    public const ulong Mul1 = 0xBF58476D1CE4E5B9UL;
    public const ulong Mul2 = 0x94D049BB133111EBUL;

    /// <summary>SplitMix64 finalizer (bijective 64-bit mix).</summary>
    public static ulong Mix(ulong z)
    {
        unchecked
        {
            z = (z ^ (z >> 30)) * Mul1;
            z = (z ^ (z >> 27)) * Mul2;
            return z ^ (z >> 31);
        }
    }

    private static ulong Fold(ulong h, ulong w, ulong position)
    {
        unchecked { return Mix(h ^ Mix(w + position * GoldenGamma)); }
    }

    /// <summary>The raw 64-bit draw for (seed, stream, a, b, c, index).</summary>
    public static ulong Draw(ulong seed, uint stream, long a, long b, long c, uint index)
    {
        unchecked
        {
            ulong h = Mix(seed + GoldenGamma);
            h = Fold(h, stream, 2);
            h = Fold(h, (ulong)a, 3);
            h = Fold(h, (ulong)b, 4);
            h = Fold(h, (ulong)c, 5);
            h = Fold(h, index, 6);
            return h;
        }
    }

    /// <summary>
    /// Retry draws for rejection sampling. attempt 0 IS Draw(...); attempt k &gt; 0 folds k in a 7th position, so
    /// retries never alias another consumer's (index+1) draw. (Deviation from the spec's literal "index+k",
    /// taken under O-6: index+k would collide with a caller that uses index+1 for its next purpose.)
    /// </summary>
    public static ulong DrawAttempt(ulong seed, uint stream, long a, long b, long c, uint index, uint attempt)
    {
        ulong h = Draw(seed, stream, a, b, c, index);
        return attempt == 0 ? h : Fold(h, attempt, 7);
    }

    /// <summary>Uniform integer in [0, n), unbiased (rejection of the top partial bucket), n &gt; 0.</summary>
    public static ulong NextBelow(ulong seed, uint stream, long a, long b, long c, uint index, ulong n)
    {
        if (n == 0) throw new ArgumentOutOfRangeException(nameof(n));
        unchecked
        {
            // Largest multiple of n that fits: accept x < limit where limit = 2^64 - (2^64 mod n).
            ulong rem = (0UL - n) % n;            // == 2^64 mod n
            for (uint k = 0; ; k++)
            {
                ulong x = DrawAttempt(seed, stream, a, b, c, index, k);
                if (rem == 0 || x < 0UL - rem) return x % n;
            }
        }
    }

    /// <summary>Bernoulli(p) for a Q32 probability: true iff the top 32 bits of the draw are &lt; p.Raw. p = 1 is certain, p = 0 impossible.</summary>
    public static bool Chance(ulong seed, uint stream, long a, long b, long c, uint index, Q32 p)
    {
        if (!p.IsProbability) throw new ArgumentOutOfRangeException(nameof(p));
        ulong u = Draw(seed, stream, a, b, c, index) >> 32;   // uniform in [0, 2^32)
        return (long)u < p.Raw;
    }
}
