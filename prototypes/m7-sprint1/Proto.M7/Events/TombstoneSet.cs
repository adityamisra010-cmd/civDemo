namespace Proto.M7.Events;

/// <summary>
/// Cancellation set keyed by event id (Seq). Seqs are dense from 0, so a growable bit array is exact,
/// O(1), and has no iteration-order question at all (spec §2.3: no HashSet).
/// </summary>
public sealed class TombstoneSet
{
    private ulong[] _bits = new ulong[16];

    public void Add(long seq)
    {
        if (seq < 0) throw new ArgumentOutOfRangeException(nameof(seq));
        long word = seq >> 6;
        if (word >= _bits.Length)
        {
            long n = _bits.Length;
            while (n <= word) n *= 2;
            Array.Resize(ref _bits, checked((int)n));
        }
        _bits[word] |= 1UL << (int)(seq & 63);
    }

    public bool Contains(long seq)
    {
        if (seq < 0) return false;
        long word = seq >> 6;
        if (word >= _bits.Length) return false;
        return (_bits[word] & (1UL << (int)(seq & 63))) != 0;
    }
}
