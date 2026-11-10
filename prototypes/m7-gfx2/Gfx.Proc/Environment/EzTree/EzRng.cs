// Port of ez-tree's random-number generator (src/lib/rng.js), MIT, Copyright (c) 2024 Daniel Greenheck
// (github.com/dgreenheck/ez-tree @ dcf309bd86bd521083d9c70f01f2de45fdc7c457; pinned in assets/manifest.tsv).
// PRESENTATION ONLY: it seeds procedural tree shapes and never touches simulation state.
namespace Gfx.Proc.Environment.EzTree;

/// <summary>
/// ez-tree's multiply-with-carry generator, reproducing the JavaScript semantics bit for bit: the state words are
/// JavaScript ToInt32 values (signed), <c>&gt;&gt; 16</c> is an arithmetic shift of the signed word, products are exact
/// before the ToInt32 wrap, and the 32-bit output is ToUint32 of <c>(m_z &lt;&lt; 16) + (m_w &amp; 65535)</c> divided by
/// 2^32. Verified against an independent Python oracle (<c>scripts/ez-rng-oracle.py</c>, fixture
/// <c>fixtures/ez-rng-oracle.txt</c>) and, while porting, against the original JavaScript under Node.
/// </summary>
public sealed class EzRng
{
    int _w, _z;

    /// <summary>JavaScript <c>(123456789 + seed) &amp; 0xffffffff</c> and <c>(987654321 - seed) &amp; 0xffffffff</c>.</summary>
    public EzRng(long seed)
    {
        _w = ToInt32(123456789L + seed);
        _z = ToInt32(987654321L - seed);
    }

    /// <summary>Current state words (for tests).</summary>
    public (int w, int z) State => (_w, _z);

    /// <summary>JavaScript ToInt32 of an integer value: wrap modulo 2^32 into the signed range.</summary>
    static int ToInt32(long v) => unchecked((int)v);

    /// <summary>The next raw 32-bit output, before the division by 2^32.</summary>
    public uint NextUInt32()
    {
        _z = ToInt32(36969L * (_z & 65535) + (_z >> 16));
        _w = ToInt32(18000L * (_w & 65535) + (_w >> 16));
        // JS: ((m_z << 16) + (m_w & 65535)) >>> 0 — the shift is int32 (wraps), the sum is exact, then ToUint32
        long sum = (long)unchecked(_z << 16) + (_w & 65535);
        return unchecked((uint)sum);
    }

    /// <summary>JavaScript <c>random(max = 1, min = 0)</c>: <c>(max - min) * (u32 / 2^32) + min</c> in double precision.</summary>
    public double Random(double max = 1, double min = 0)
    {
        double result = NextUInt32() / 4294967296.0;
        return (max - min) * result + min;
    }
}
