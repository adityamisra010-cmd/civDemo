using System.Text;

namespace Sim.Ui.World;

/// <summary>
/// P12a (M5 map workstream) — THE ONE HASH the world lens uses to vary a settlement's composition (the preferred lot of
/// each part, the rotation of each dwelling ring). Defined byte for byte, so it is the same in every process and on every
/// platform — unlike <c>string.GetHashCode</c>, which .NET randomises per process:
/// <code>
///   bytes = UTF-8(a) + 0x00 + UTF-8(b)          (b omitted for the one-part form)
///   h     = FNV-1a 64 (offset 14695981039346656037, prime 1099511628211, unchecked)
///   h     = fmix64(h)                             (the MurmurHash3 finaliser)
/// </code>
/// fmix64 matters: plain FNV-1a's low bits depend only on the low bits of each byte, so keys differing in one digit
/// ("institution:1" / "institution:9") would share a lot modulo a small ring. Ported, algorithm only, from
/// origin/claude/civdemo-work-b1z2y4 (Sim.Ui/World/StableHash.cs); pinned by literal vectors computed by an independent
/// implementation (<c>SettlementCompositionTests.StableHash_IsPinnedByIndependentVectors</c>).
/// Presentation only: nothing in the simulation reads it.
/// </summary>
public static class StableHash
{
    public const ulong FnvOffset = 14695981039346656037UL;
    public const ulong FnvPrime = 1099511628211UL;

    public static ulong Fnv1a64(ReadOnlySpan<byte> bytes)
    {
        ulong h = FnvOffset;
        foreach (byte b in bytes) { h ^= b; h = unchecked(h * FnvPrime); }
        return h;
    }

    public static ulong Fmix64(ulong k)
    {
        k ^= k >> 33;
        k = unchecked(k * 0xff51afd7ed558ccdUL);
        k ^= k >> 33;
        k = unchecked(k * 0xc4ceb9fe1a85ec53UL);
        k ^= k >> 33;
        return k;
    }

    public static ulong Of(string a) => Fmix64(Fnv1a64(Encoding.UTF8.GetBytes(a)));

    public static ulong Of(string a, string b)
    {
        byte[] ba = Encoding.UTF8.GetBytes(a), bb = Encoding.UTF8.GetBytes(b);
        var all = new byte[ba.Length + 1 + bb.Length];
        ba.CopyTo(all, 0);
        all[ba.Length] = 0;
        bb.CopyTo(all, ba.Length + 1);
        return Fmix64(Fnv1a64(all));
    }

    /// <summary>A non-negative index below <paramref name="n"/> (unsigned modulo: never negative).</summary>
    public static int Index(ulong h, int n) => n <= 0 ? 0 : (int)(h % (ulong)n);
}
